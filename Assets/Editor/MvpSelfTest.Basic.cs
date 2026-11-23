using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// MvpSelfTest 의 기본 검사 (logic · judge · lobby · start) 이에요. 실행 방법과 그룹 설명은 MvpSelfTest.cs 에 있어요.
static partial class MvpSelfTest
{
    // 0) 코드 단위 확인 (화면 없이)
    static void LogicSteps()
    {
        group = "logic";
        Add(0f, "효과음 파일", () =>
        {
            // Sfx 의 효과음 · 배경음 이름마다 Resources/Sound 에 파일이 있어야 해요
            foreach (FieldInfo f in typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(f.GetValue(null) is string name) || !(name.StartsWith("sfx_") || name.StartsWith("bgm_"))) continue;
                Check(Resources.Load<AudioClip>("Sound/" + name) != null, (name.StartsWith("bgm_") ? "배경음 " : "효과음 ") + name);
            }
        });
        Add(1.0f, "기본 함수", () =>
        {
            Check(GameManager.Josa("가방", "을", "를") == "을" && GameManager.Josa("방망이", "을", "를") == "를", "조사(을/를) 고르기");
            Check(LocalJudge.Classify(ColorMixer.Mix(0, 1, 1)) == (int)InkColor.Green + 1, "노랑 1 : 파랑 1 = 초록");
            Check(LocalJudge.Classify(ColorMixer.Mix(1, 1, 0)) == (int)InkColor.Orange + 1, "빨강 1 : 노랑 1 = 주황");
            Check(LocalJudge.Classify(ColorMixer.Mix(1, 1, 1)) == (int)InkColor.Black + 1, "빨강 1 : 노랑 1 : 파랑 1 = 검정");
            Check(LocalJudge.Classify(ColorMixer.Mix(2, 2, 1)) == (int)InkColor.Brown + 1, "빨강 2 : 노랑 2 : 파랑 1 = 갈색");
            Check(ColorMixer.Mix(4, 4, 2).Equals(ColorMixer.Mix(2, 2, 1)), "넣은 횟수가 달라도 비율이 같으면 같은 색");
            foreach (StageData s in Stages())
            {
                string prompt = OpenAIJudge.BuildPrompt(s);
                bool all = s.features.Length == 1;      // 1챕터는 물건마다 형태 하나
                foreach (Feature f in s.features) all &= prompt.Contains("featureId: " + f.id);
                all &= s.wrongSprites.Length == 1;      // 형태 하나라 정답 또는 전체 오답뿐이에요.
                for (int i = 0; i < s.wrongSprites.Length; i++) all &= s.wrongSprites[i] != null;
                Check(all && s.originalSprite != null, $"{s.Title} 데이터 (그림 + 특징 1개 + 전체 오답 1장 + 프롬프트)");
            }
            // 5챕터 : 단계마다 요청 물건 · 맞설 물건 한 쌍
            ChapterData curse = ChapterOf(5);
            Check(curse != null && curse.rule == ChapterRule.Curse && curse.ItemCount == 3 && curse.counterItems != null && curse.counterItems.Length == 3,
                  "5챕터 데이터 (저주 규칙, 단계 3개, 맞설 물건 3개)");
            if (curse != null && curse.items != null && curse.counterItems != null)
                for (int i = 0; i < curse.ItemCount && i < curse.counterItems.Length; i++)
                    Check(curse.items[i] != null && curse.counterItems[i] != null && curse.items[i].originalSprite != null && curse.counterItems[i].originalSprite != null,
                          $"5챕터 {i + 1}단계 : 요청 물건과 맞설 물건에 그림이 있다");
            bool chain = Progress.IsOpen(1);
            for (int n = 2; n <= 5; n++) chain &= Progress.IsOpen(n) == Progress.IsCleared(n - 1);
            Check(chain, "1챕터는 늘 열려 있고, 다른 챕터는 앞 챕터를 깨야 열린다");
            ChapterData c1 = ChapterOf(1);
            Check(c1 != null && c1.penMode == ColorMode.BlackOnly && !c1.hasMagnifier && c1.guide == ChapterGuide.Pen && c1.candies == 3,
                  "1챕터 : 검정 펜, 돋보기 없음, 펜 안내, 사탕 3개");
            for (int n = 2; n <= 4; n++)
            {
                ChapterData c = ChapterOf(n);
                bool ok = c != null && c.rule == ChapterRule.Normal && c.ItemCount == 3 && c.playBackground != null && c.photo != null;
                if (ok)
                    foreach (StageData it in c.items)
                    {
                        ok &= it != null && it.originalSprite != null && it.features.Length == (n == 2 ? 2 : 3)
                              && it.wrongSprites.Length == (n == 2 ? 3 : 7);
                        if (ok) foreach (Sprite w in it.wrongSprites) ok &= w != null;
                        ok &= it.ServerStageKey.StartsWith($"room-{n}-");
                    }
                Check(ok, $"{n}챕터 데이터 (물건 3개 + 특징 {(n == 2 ? 2 : 3)}개 + 모든 O/X 조합 그림 + 서버 이름 room-{n}-N)");
            }
            ChapterData c2 = ChapterOf(2), c3 = ChapterOf(3), c4 = ChapterOf(4);
            Check(c2 != null && c2.penMode == ColorMode.BlackOnly && c2.guide == ChapterGuide.Magnifier && c2.windowFire, "2챕터 : 검정 펜, 돋보기 안내, 창밖 도깨비불");
            Check(c3 != null && c3.penMode == ColorMode.Primaries && c3.guide == ChapterGuide.Mix && c3.dokkaebiMood == 1, "3챕터 : 삼원색 선택, 색 안내, 주황 머리");
            Check(c4 != null && c4.penMode == ColorMode.MixTwo && c4.candies == 0 && !c4.CanRetry && c4.guide == ChapterGuide.None && c4.dokkaebiMood == 2 && c4.endEyes, "4챕터 : 두 색 혼합, 사탕 0개, 안내 없음, 붉은 머리, 끝에 빨간 눈");
        });
        Add(0.1f, "모든 판정 조합의 그림 연결", () =>
        {
            // 배열 인덱스가 아니라 파일의 O/X 이름과 비교해 뒤바뀐 연결을 잡아요.
            string[][] masks = {
                new[] { "O", "X" },
                new[] { "OO", "OX", "XO", "XX" },
                new[] { "OOO", "OOX", "OXO", "OXX", "XOO", "XOX", "XXO", "XXX" }
            };
            string[] legacy = { "Bat_Wrong_X", "Bag_Wrong_XX", "Mat_Wrong_XXX", "Gimbap_Wrong_XXXX", "Friend_Wrong_XXXXX" };
            for (int chapterNumber = 1; chapterNumber <= 5; chapterNumber++)
            {
                ChapterData c = ChapterOf(chapterNumber);
                StageData[] items = chapterNumber == 5 ? c.counterItems : c.items;
                for (int item = 0; item < items.Length; item++)
                {
                    StageData stage = items[item];
                    foreach (string mask in masks[stage.features.Length - 1])
                    {
                        bool[] present = System.Array.ConvertAll(mask.ToCharArray(), x => x == 'O');
                        Sprite sprite = stage.SpriteFor(present);
                        string expected = mask.IndexOf('X') < 0 ? stage.originalSprite.name
                                        : chapterNumber == 1 ? legacy[item] : stage.name + "_Wrong_" + mask;
                        Check(sprite != null && sprite.name == expected, $"{stage.name} {mask} → {expected}");
                    }
                }
            }
            Check(ChapterOf(5).penMode == ColorMode.MixFree, "5챕터는 세 색 혼합");
        });
    }

    static void JudgeSteps()
    {
        // 0-2) (참고) 팀 아트 원본·엉뚱한 물건 그림을 자동 판정에 넣어 보기
        //   팀 그림은 굵은 테두리·명암이 있는 완성 그림이라, 손그림용 자동 판정과 꼭 맞지는 않아요.
        //   그래서 통과/실패로 세지 않고 결과만 기록해요. (실제 판정 검사는 아래 손그림 · 판정 전수 검사)
        group = "judge";
        Add(0.1f, "자동 판정", () =>
        {
            LocalJudge.TestOverride = null;
            foreach (StageData s in Stages())
            {
                OpenAIJudge.Verdict v = LocalJudge.Judge(s, LoadReadable(s.originalSprite));
                string miss = "";
                for (int i = 0; i < v.present.Length; i++) if (!v.present[i]) miss += $" {s.features[i].id}({v.notes[i]})";
                Info(miss == "", $"자동 판정 : {s.Title} 원본 = 전부 그림" + (miss == "" ? "" : " / 빠뜨렸다고 본 것:" + miss));
            }
        });

        // 0-3) 손그림 판정 : 사람이 마우스로 그린 것처럼 삐뚤고, 조금 끊기고, 성기게 칠한 그림
        Add(0.1f, "손그림 판정", () =>
        {
            StageData[] all = Stages();
            // 1챕터는 물건마다 형태 하나. 제대로 그린 손그림은 모두 통과해야 해요
            ExpectJudge(all[0], HandBat(spikes: true), -1, "손그림 방망이 (선 손잡이 + 타원 머리 + 선 돌기)");
            ExpectJudge(all[0], HandCloudBat(), -1, "손그림 방망이 (구름처럼 볼록한 머리)");
            ExpectJudge(all[1], HandBag(), -1, "손그림 가방 (손잡이 + 주머니 + 줄무늬)");
            ExpectJudge(all[2], HandMat(bands: true), -1, "손그림 돗자리");
            ExpectJudge(all[3], HandGimbap(), -1, "손그림 김밥");
            ExpectJudge(all[4], HandFriend(), -1, "손그림 친구");
            // 돌기가 없는 방망이는 방망이 형태가 아니에요 (그 특징일 때만 확인)
            if (all[0].features.Length == 1 && all[0].features[0].check == AutoCheck.BatShape)
                ExpectJudge(all[0], HandBat(spikes: false), 0, "손그림 방망이, 돌기 없음");
        });
    }

    static void LobbySteps()
    {
        // 0-4) 로비 : 씬 파일이 빌드에 들어 있어야 챕터 선택에서 Game 씬을 열 수 있어요 (Play 에 들어가지 않아요)
        group = "lobby";
        Add(0.1f, "로비 씬 파일", () =>
        {
            var inBuild = new List<string>();
            foreach (EditorBuildSettingsScene sc in EditorBuildSettings.scenes) if (sc.enabled) inBuild.Add(Path.GetFileNameWithoutExtension(sc.path));
            Check(File.Exists("Assets/Scenes/Lobby.unity"), "Assets/Scenes/Lobby.unity 파일이 있다");
            Check(inBuild.Contains(ChapterSelect.LobbyScene), "빌드 설정에 로비 씬이 들어 있다");
            Check(inBuild.Contains(ChapterSelect.GameScene), "빌드 설정에 게임 씬이 들어 있다");
            Check(inBuild.Count > 0 && inBuild[0] == ChapterSelect.LobbyScene, "로비 씬이 맨 앞 (처음 열리는 씬)");
            Check(Progress.IsOpen(1) && Progress.IsOpen(2) == Progress.IsCleared(1) && Progress.IsOpen(5) == Progress.IsCleared(4), "앞 챕터를 깨야 다음 챕터가 열린다");
            var lobbyMgr = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI_Grayscale.mat");
            Check(lobbyMgr != null && lobbyMgr.shader != null && lobbyMgr.shader.name == "FindDokkaebi/UI Grayscale", "못 연 챕터 사진을 흑백으로 그리는 재질이 있다");
        });
    }

    static void StartSteps()
    {
        // 1) 시작 화면
        group = "start";
        // 모든 대사를 실제 대사 상자 크기로 줄을 나눠 봐요 : 상자를 넘치지 않고, 마지막 줄에 짧은 조각만 남지 않아야 해요
        Add(0.2f, "대사 줄바꿈", () =>
        {
            var line = Find("DialoguePanel/DialogueBox/Line").GetComponent<TMP_Text>();
            Find("DialoguePanel").gameObject.SetActive(true);      // 꺼진 화면에서는 줄을 나누지 않아서 잠깐 켜요
            var shortTail = new List<string>();
            var overflow = new List<string>();
            int count = 0;
            foreach (string l in GameScript.AllLines())
            {
                count++;
                line.text = DialogueBox.KeepTailTogether(line, l);
                line.ForceMeshUpdate(true);
                TMP_TextInfo info = line.textInfo;
                if (info.lineCount > 1 && info.lineInfo[info.lineCount - 1].visibleCharacterCount < DialogueBox.MinLastLine) shortTail.Add(l);
                if (line.isTextOverflowing || line.preferredHeight > line.rectTransform.rect.height + 1f) overflow.Add(l);
            }
            Check(shortTail.Count == 0, $"대사 {count}줄 : 마지막 줄에 짧은 조각만 남지 않는다" + (shortTail.Count > 0 ? " / " + string.Join(" | ", shortTail) : ""));
            Check(overflow.Count == 0, $"대사 {count}줄 : 대사 상자를 넘치지 않는다" + (overflow.Count > 0 ? " / " + string.Join(" | ", overflow) : ""));
            Check(count > 50 && line.textInfo.lineCount >= 1, $"대사 {count}줄을 상자 크기로 나눠 봤다");
            Gm().StartGame();      // 줄 재기에 쓴 대사 상자를 챕터 첫 대사로 되돌려요 (게임 씬은 대화로 바로 시작해요)
        });
        Add(0.2f, "게임 시작", () =>
        {
            SetPrivate(Gm(), "roamLoopSeconds", 0.05f);     // 검사에서는 찾는 동작을 기다리지 않아요
            Check(IsOn("DialoguePanel") && !IsOn("HUD"), "게임 씬이 열리면 1챕터 대화로 바로 시작한다 (HUD 숨김)");
            Check(Find("Background").GetComponent<Image>().sprite != null, "배경이 있다");
            Check(TryFind("TitlePanel") == null && TryFind("ChapterPanel") == null, "게임 씬에는 시작 · 챕터 선택 화면이 없다");
            Check(Find("JudgePanel/DokkaebiEnter/Roam/Step/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Think", "판정 대기는 고민하는 도깨비");
            Check(TryFind("HUD/HeartGroup") != null && TryFind("ChoosePanel") != null && Find("CurseEndPanel") != null, "5챕터 화면(하트 · 고르기 · 엔딩)이 씬에 있다");
            Check(!IsOn("HUD/HeartGroup") && !IsOn("ChoosePanel"), "1챕터에서는 하트와 고르기 화면이 안 보인다");
            // 소리 설정 : 열고, 볼륨을 바꾸면 저장되고, 닫으면 그대로예요
            float bgmBefore = Sfx.BgmVolume, sfxBefore = Sfx.SfxVolume;
            Gm().ToggleSettings();
            Check(IsOn("SettingsPanel") && Time.timeScale == 0f, "톱니를 누르면 소리 설정 창이 열리고 게임이 멈춘다");
            Check(IsOn("SettingsPanel/SettingsCard/SettingsMain") && !IsOn("SettingsPanel/SettingsCard/SettingsConfirm"), "처음에는 볼륨 줄만 보인다");
            var bgmSlider = Find("SettingsPanel/SettingsCard/SettingsMain/BgmSlider").GetComponent<Slider>();
            var sfxSlider = Find("SettingsPanel/SettingsCard/SettingsMain/SfxSlider").GetComponent<Slider>();
            Check(Mathf.Approximately(bgmSlider.value, bgmBefore) && Mathf.Approximately(sfxSlider.value, sfxBefore), "슬라이더는 저장된 볼륨에서 시작한다");
            bgmSlider.value = 0.3f;
            sfxSlider.value = 0.6f;
            Check(Mathf.Approximately(Sfx.BgmVolume, 0.3f) && Mathf.Approximately(PlayerPrefs.GetFloat("volume_bgm"), 0.3f)
                  && Mathf.Approximately(PlayerPrefs.GetFloat("volume_sfx"), 0.6f), "볼륨을 바꾸면 PlayerPrefs 에 저장된다");
            LayoutOnly("settings");
            // 나가기 : 누르면 [취소] [나가기] 로 바뀌고, 취소하면 돌아와요 (나가기 자체는 로비 씬을 불러서 누르지 않아요)
            Gm().AskExit();
            Check(!IsOn("SettingsPanel/SettingsCard/SettingsMain") && IsOn("SettingsPanel/SettingsCard/SettingsConfirm"), "나가기를 누르면 취소 · 나가기 확인으로 바뀐다");
            LayoutOnly("settings_confirm");
            Gm().CancelExit();
            Check(IsOn("SettingsPanel/SettingsCard/SettingsMain") && !IsOn("SettingsPanel/SettingsCard/SettingsConfirm"), "취소하면 볼륨 설정으로 돌아온다");
            Gm().CloseSettings();
            Check(!IsOn("SettingsPanel") && Time.timeScale == 1f, "닫으면 다시 진행");
            Sfx.BgmVolume = bgmBefore;      // 사람이 정해 둔 볼륨으로 돌려놔요
            Sfx.SfxVolume = sfxBefore;
            Shot("start");
        });
    }
}

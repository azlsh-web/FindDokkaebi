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

// MvpSelfTest 의 5챕터 검사 (chapter5) 이에요. 실행 방법과 그룹 설명은 MvpSelfTest.cs 에 있어요.
static partial class MvpSelfTest
{
    // 결과 화면 한 장면 넘기기 : 대사가 타이핑 중이면 첫 터치는 글자만 다 보여 주니 한 번 더 눌러요
    static void Next()
    {
        string before = Plain("ResultPanel/DialogueBox/Line");
        Gm().ResultTap();
        if (IsOn("ResultPanel") && Plain("ResultPanel/DialogueBox/Line") == before) Gm().ResultTap();
    }

    static void Chapter5Steps()
    {
        // ───────────── 5챕터 : 도깨비의 저주 ─────────────
        //   판정은 LocalJudge.TestOverride 로 미리 정해 넣어요 (5챕터 물건은 자동 판정 기준이 아직 자리표시라서)
        //   엔딩 화면 뒤의 마지막 탭은 로비 씬을 불러서 검사가 끝나 버리니 누르지 않아요
        group = "chapter5";
        Add(0.5f, "5챕터 시작", () =>
        {
            StartChapter(5);
            ChapterData c5 = ChapterOf(5);
            Check(GetChapterNumber() == 5, "로비에서 5챕터를 고르면 5챕터가 시작된다");
            Check(IsOn("DialoguePanel") && IsOn("DialoguePanel/Friend") && !IsOn("HUD"), "시작 대사 : 친구가 서 있고 위 줄은 숨는다");
            Check(GetInt("hearts") == 3, "하트 3개로 시작");
            Check(!GetBool("dark"), "처음에는 밝은 숲");
            Shot("c5_dialogue");
            Then(() =>
            {
                // A3 : 단계 대화에서 친구가 귓속말로 맞설 물건을 일러 준다 (친구 얼굴)
                string friendLine = GameScript.CurseStepLines(0)[1].text;
                for (int k = 0; k < 80 && !IsOn("ChoosePanel") && Plain("DialoguePanel/DialogueBox/Line") != friendLine; k++) Gm().DialogueTap();
                Check(Plain("DialoguePanel/DialogueBox/Line") == friendLine
                      && Find("DialoguePanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_FriendFace_Normal",
                      "친구가 귓속말로 맞설 물건을 일러 준다 (친구 얼굴)");
                TapToChoose();
                Check(GetBool("dark"), "간식 이야기를 꺼내면 숲이 어두워지고 도깨비가 흑화한다");
                var darkFace = (Sprite)typeof(GameManager).GetField("faceDokkaebi", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());
                Check(darkFace != null && darkFace.name.EndsWith("_Dark"), "흑화한 뒤에는 대사 상자 얼굴도 흑화 얼굴 (B1)");
                Check(IsOn("ChoosePanel") && !IsOn("DialoguePanel"), "대사가 끝나면 고르기 화면");
            });
        });
        Add(0.3f, "5챕터 고르기", () =>
        {
            ChapterData c5 = ChapterOf(5);
            bool leftCounter = GetBool("leftIsCounter");
            Sprite left = Find("ChoosePanel/ChooseCard_0/Item").GetComponent<Image>().sprite;
            Sprite right = Find("ChoosePanel/ChooseCard_1/Item").GetComponent<Image>().sprite;
            Sprite request = c5.items[0].originalSprite, counter = c5.counterItems[0].originalSprite;
            Check(left != null && right != null && left != right, "그림 2장이 나온다");
            Check((left == counter && right == request) || (left == request && right == counter) , "두 그림은 요청 물건과 맞설 물건");
            Check(left == (leftCounter ? counter : request), "왼쪽 그림이 맞설 물건인지는 leftIsCounter 와 같다");
            Check(IsOn("ChoosePanel/ChooseCard_0") && IsOn("ChoosePanel/ChooseCard_1"), "그림 카드 두 장이 보인다");
            Check(Find("ChoosePanel/ChooseCard_0").GetComponent<Image>().sprite.name == "Draw_Paper", "고르기 카드는 쪽지 종이 질감 (B9)");
            Shot("c5_choose");
            Then(() =>
            {
                Gm().ChooseItem(CounterSide());
                Check(IsOn("ObservePanel") && !IsOn("ChoosePanel"), "고르면 떠올리기로 간다");
                Check(GetBool("chosenCounter"), "맞설 물건을 골랐다");
                Check(Find("ObservePanel/Card/TargetImage").GetComponent<Image>().sprite == c5.counterItems[0].originalSprite, "고른 물건을 보여 준다");
            });
        });
        // 맞설 물건을 맞힘 : "제법이군" → 다음 단계 (채점표 없음)
        Add(0.4f, "5챕터 맞설 물건 그리기", () =>
        {
            Check(IsOn("DrawPanel") && IsOn("HUD/HeartGroup") && LitHearts() == 3, "그리기 : 위 줄에 주인공 하트 3개");
            Check(!IsOn("HUD/CandyRow"), "5챕터에는 사탕이 없다");
            Check(IsOn("DrawPanel/JamOverlay"), "맞설 물건을 그릴 때는 방해 연출이 덮인다");
            Check(IsOn("DrawPanel/JamOverlay/Peek"), "방해 연출 : 흑화 도깨비가 쪽지 위에 나타난다");
            var doodler = Find("DrawPanel/JamOverlay").GetComponent<JamDoodler>();
            Check(doodler != null && doodler.Phrases.Length > 0 && doodler.Phrases[0].Contains(ChapterOf(5).counterItems[0].targetName),
                  "방해 글씨 : \"네가 (그리는 물건)을 그리려는 걸 다 알고 있어\"");
            Check(Find("DrawPanel/JamOverlay").GetSiblingIndex() < Find("DrawPanel/ToolBar").GetSiblingIndex(), "방해 막은 도구 줄 · 팔레트 아래");
            Check(!IsOn("DrawPanel/PeekGuide"), "돋보기 시범은 2챕터에서만");
            Check(GetInt("magnifiers") == 1, "돋보기는 챕터마다 1번");
            DrawStage("c5", true);
            Expect(ChapterOf(5).counterItems[0], -2);       // 전부 그림
            Shot("c5_draw");
            Then(() =>
            {
                Check(Find("DrawPanel/JamOverlay").GetComponent<JamDoodler>().MarksMade > 0, "방해 : 그리는 동안 도깨비가 쪽지에 낙서를 한다");
                Gm().Submit();
                Check(Plain("DialoguePanel/DialogueBox/Line") == GameScript.CurseGiveBoy, "쪽지를 들이밀 때 주인공 대사 (사탕 없음)");
            });
        }, () => IsOn("DrawPanel"));
        Add(1.0f, "5챕터 판정 대기", () =>
        {
            Check(IsOn("JudgePanel/CurseJudge") && !IsOn("JudgePanel/DokkaebiEnter"), "5챕터 판정 대기 : 찾아다니는 도깨비 대신 빛나는 쪽지");
            Shot("c5_judge");
        }, () => { PassGive(); return IsOn("JudgePanel"); });
        Add(0.3f, "5챕터 맞설 물건 성공", () =>
        {
            Check(GetInt("hearts") == 3 && LitHearts() == 3, "맞설 물건을 맞히면 하트는 그대로");
            Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseCounterHitLine(0), "도깨비가 \"제법이군\"");
            Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == ChapterOf(5).counterItems[0].originalSprite, "진짜 맞설 물건이 나왔다");
            Check(!IsOn("ResultPanel/ChoiceRow"), "다시 · 포기 버튼이 없다");
            Shot("c5_counter_hit");
            Then(() =>
            {
                Next();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseBoyHit, "주인공이 기뻐한다");
                Next();
                Check(!IsOn("ComparePanel") && IsOn("DialoguePanel") && GetInt("stageIndex") == 1, "채점표 없이 다음 단계 대화");
                TapToChoose();
                Check(IsOn("ChoosePanel"), "2단계도 고르기");
                Gm().ChooseItem(1 - CounterSide());       // 이번에는 요청 물건
                Check(!GetBool("chosenCounter") && IsOn("ResultPanel") && !IsOn("ObservePanel"), "요청 물건을 고르면 그리지 않고 바로 결과");
                Check(GetInt("hearts") == 2 && LitHearts() == 2, "요청 물건을 고르면 하트 -1");
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseTakeLine(1), "도깨비가 요청 물건을 가져간다");
                Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == ChapterOf(5).items[1].originalSprite, "가져가는 물건이 보인다");
            });
        }, () => ResultShown());
        Add(0.3f, "5챕터 요청 물건 빼앗김", () =>
        {
            Shot("c5_take");
            Then(() =>
            {
                Next();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseBoyTaken, "주인공 속마음");
                Next();
                Check(IsOn("ChoosePanel") && GetInt("stageIndex") == 1, "같은 단계 고르기로 돌아간다");
                Check(!GetBool("friendLost"), "하트가 남으면 친구는 그대로");
                Gm().ChooseItem(CounterSide());
                Check(GetBool("chosenCounter") && IsOn("ObservePanel"), "이번엔 맞설 물건");
            });
        }, () => ResultShown());
        // 맞설 물건을 틀림 : 점프스케어 + 하트 -1 → 요청 물건으로 하트 0 → 친구 영혼 → 게임오버
        Add(0.4f, "5챕터 맞설 물건 틀리게 그리기", () =>
        {
            Check(LitHearts() == 2, "하트 2개");
            DrawStage("c5w", false);
            Expect(ChapterOf(5).counterItems[1], -1);       // 전부 틀림
            Then(() => Gm().Submit());
        }, () => IsOn("DrawPanel"));
        Add(0.3f, "5챕터 맞설 물건 실패", () =>
        {
            Check(GetInt("hearts") == 1 && LitHearts() == 1, "맞설 물건을 틀리면 하트 -1");
            Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseCounterWrong, "도깨비가 비웃는다");
            Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == ChapterOf(5).counterItems[1].AllWrongSprite, "오답 그림 1번이 나왔다");
            Check(!GetBool("gameOverPending"), "하트가 남으면 게임오버가 아니다");
            Shot("c5_counter_fail");
            Then(() =>
            {
                Next();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseBoyWrong, "주인공이 버틴다");
                Next();
                Check(IsOn("ChoosePanel") && GetInt("stageIndex") == 1 && !IsOn("ComparePanel"), "같은 단계 고르기로 돌아간다");
                Gm().ChooseItem(1 - CounterSide());
                Check(GetInt("hearts") == 0, "마지막 하트를 잃었다");
            });
        }, () => ResultShown());
        Add(0.3f, "5챕터 하트 0", () =>
        {
            Next();
            Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseSoulTaken, "하트가 0이면 도깨비가 친구 영혼을 데려간다");
            Check(GetBool("friendLost") && GetBool("gameOverPending"), "친구 영혼을 빼앗기고 게임오버 예약");
            Check(IsOn("ResultPanel/SoulFriend") && !Find("ResultPanel/ReturnedItem").GetComponent<Image>().enabled, "물건 대신 친구가 나온다");
        }, () => ResultShown());
        Add(1.5f, "5챕터 친구 영혼", () =>
        {
            Check(IsOn("ResultPanel/Soul"), "친구가 쓰러지고 영혼이 빠져나간다");
            Shot("c5_soul");
            Then(() => Next());
        });
        Add(0.3f, "5챕터 게임오버", () =>
        {
            Check(IsOn("CurseEndPanel") && GetInt("curseEnd") == 1, "대사를 넘기면 게임오버 화면");
            Check(IsOn("CurseEndPanel/Silhouette") && !IsOn("CurseEndPanel/Boy") && !IsOn("HUD"), "게임오버는 실루엣만, 위 줄 없음");
            Check(IsOn("CurseEndPanel/PileSoul") && IsOn("CurseEndPanel/Dokkaebi") && !IsOn("CurseEndPanel/CandyJar"),
                  "게임오버 : 더미 위에 친구 영혼, 더미 앞에 도깨비, 사탕 통 없음");
            Check(Progress.IsCleared(5), "엔딩을 보면 5챕터를 깬 것으로 친다");
            Shot("c5_gameover");
        }, () => IsOn("CurseEndPanel"));

        // 3단계를 다 이김 → 해피 엔딩 (3단계에서 시작해서 맞설 물건을 한 번 맞힘)
        Add(0.5f, "5챕터 해피 엔딩 준비", () =>
        {
            StartChapter(5);
            TapToChoose();
            Invoke(Gm(), "StartStage", 2);
            TapToChoose();
            Gm().ChooseItem(CounterSide());
        });
        Add(0.4f, "5챕터 해피 엔딩 그리기", () =>
        {
            DrawStage("c5h", false);
            Expect(ChapterOf(5).counterItems[2], -2);
            Then(() => Gm().Submit());
        }, () => IsOn("DrawPanel"));
        Add(0.3f, "5챕터 해피 엔딩", () =>
        {
            Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseCounterHitLine(2), "마지막 단계 : 도깨비가 무너진다");
            Then(() => { Next(); Next(); });
        }, () => ResultShown());
        Add(0.5f, "5챕터 해피 엔딩 화면", () =>
        {
            Check(IsOn("CurseEndPanel") && GetInt("curseEnd") == 2, "3단계를 다 이기면 해피 엔딩");
            Check(!IsOn("HUD"), "엔딩에는 위 줄이 없다");
            Check(Plain("CurseEndPanel/DialogueBox/Line") == (GetBool("batLost") ? GameScript.CurseHappyNoBat : GameScript.CurseHappy), "해피 엔딩 대사");
            Check(!IsOn("CurseEndPanel/Dokkaebi"), "정화된 도깨비가 뛰어서 도망간 뒤에 대사가 나온다");
            Check(IsOn("CurseEndPanel/CandyJar") && Find("CurseEndPanel/CandyJar").GetComponent<CandyJar>().Shown == GetInt("hearts"),
                  "남은 하트가 사탕 통에 사탕으로 담긴다");
            Shot("c5_happy");
        }, () => IsOn("CurseEndPanel") && IsOn("CurseEndPanel/DialogueBox"));
        Add(0.3f, "5챕터 마지막 사진 저장", () => { });     // 사진은 다음 프레임에 저장돼요. 바로 끝나면 안 찍혀요
    }
}

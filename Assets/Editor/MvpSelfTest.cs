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

// ─────────────────────────────────────────────────────────────
// MvpSelfTest : 게임을 "자동으로 처음부터 끝까지" 플레이해 보는 검사 도구 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 실행 방법
//   - 메뉴 Tools → 찾아라도깨비 → 자동 플레이 테스트
//   - 또는 Temp/mvp_selftest.flag 파일이 생기면 자동 실행 (Claude 가 원격으로 검사할 때 사용)
//
// 일부만 돌리기 (고친 곳만 빠르게 확인, 커밋 전에는 전체를 한 번)
//   - 메뉴 Tools → 찾아라도깨비 → 부분 테스트
//   - 또는 flag 파일에 그룹 이름을 적어요 (예: stage3 ending). 비어 있으면 전체예요.
//   logic  : 조사 · 색 섞기 · 단계 데이터         judge  : 손그림 판정
//            (이 둘만 고르면 Play 에 들어가지 않아요)
//   title  : 시작 화면                            stage1 ~ stage5 : 그 단계의 화면 흐름
//   ending : 이야기 종료 · 방망이를 못 찾은 경우
//   단계나 엔딩만 돌릴 때는 앞 단계를 건너뛰고 그 자리에서 시작해요 (EntryStep).
//
// 하는 일
//   Play 모드에 들어가서 사람이 버튼을 누르는 대신 GameManager·ToolBar 의 함수를 차례로 부르고,
//   마우스로 그리는 대신 그림판에 가짜 드래그 이벤트를 보내요.
//   화면마다 "맞게 바뀌었는지" 확인하고 Game 창 스크린샷을 찍어요.
//   AI 는 부르지 않아요 (OpenAIJudge.DisableAI = true).
//   - 자동 판정(LocalJudge) 자체는 원본·엉뚱한 물건 그림으로 따로 검사해요.
//   - 화면 흐름 검사에서는 "이번엔 이 특징을 빠뜨렸다"는 결과를 미리 정해서 넣어요 (LocalJudge.TestOverride).
//
// 시나리오
//   시작 → 1~5단계 (각 단계 : 대화 → 떠올리기 → 쪽지 쓰기 → 건네기 → 부분 성공 → 이어 그리기 → 성공 → 채점표 → 다음 단계)
//   + 1단계 : AI 오류 처리(간식 돌려주기), 모두 지우기/되돌리기
//   + 1단계 : 도깨비가 돋보기 쓰는 법을 보여 줌 (공짜, 향이 안 탐) → 게임 전체 1번을 씀
//   + 2단계 : 두 번째로 틀리면 무료 돋보기를 받아서 씀 → 사탕 3개를 다 써서 포기
//   + 3단계 : 향이 다 타면 자동 제출
//   + 4·5단계 : 혼색 팔레트 (노랑 1:파랑 1=초록, 빨강 2:노랑 2:파랑 1=갈색, 되돌리기)
//   → 이야기 종료 화면 기록 확인
//   + 방망이를 못 찾았을 때 : 이야기 종료까지 도깨비가 빈손이고 소풍 장면에 대파가 놓이는지
//
// 결과
//   - Console 에 [SelfTest] 전체 PASS / FAIL n개
//   - Temp/SelfTest/result.txt 와 스크린샷 PNG 들 (Temp 는 Git 에 안 올라가요)
//
// ※ Play 에 들어가면 스크립트가 다시 로드돼서 static 변수가 초기화돼요.
//   그래서 "테스트 중인지"는 SessionState(에디터가 켜져 있는 동안 유지되는 저장소)에 적어둬요.
[InitializeOnLoad]
static class MvpSelfTest
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_selftest.flag";
    const string OutDir = "Temp/SelfTest";
    const string RunningKey = "MvpSelfTest.Running";
    const string GroupsKey = "MvpSelfTest.Groups";
    static readonly string[] AllGroups = { "logic", "judge", "title", "stage1", "stage2", "stage3", "stage4", "stage5", "ending" };

    // 한 단계 = (앞 단계 후 기다릴 초, 이름, 할 일, [조건])
    //   until 이 있으면 그 조건이 참이 될 때까지(최대 20초) 기다린 뒤 실행해요.
    //   Unity 창이 뒤에 있으면 게임 시계가 느려져서, 정해진 초 대신 "화면이 바뀔 때까지" 기다리는 게 안전해요.
    struct Step { public float wait; public string name; public Action run; public Func<bool> until; public string group; }
    static double untilDeadline = -1;

    static List<Step> steps;
    static int index;
    static double nextTime;
    static double nextFlagCheck;
    static bool refreshed;
    static readonly List<string> report = new List<string>();
    static int failCount;
    static int shotCount;
    static Action pending;
    static double pendingTime;
    static string scope = "";       // 부분 실행이면 결과 줄에 "부분(그룹) " 을 적어요

    static MvpSelfTest()
    {
        EditorApplication.update += PollFlag;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/찾아라도깨비/자동 플레이 테스트")]
    public static void Run() => RunGroups("all");

    [MenuItem("Tools/찾아라도깨비/부분 테스트/규칙 (logic)")] static void RunLogic() => RunGroups("logic");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/손그림 판정 (judge)")] static void RunJudge() => RunGroups("judge");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/시작 화면 (title)")] static void RunTitle() => RunGroups("title");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/1단계 (stage1)")] static void RunStage1() => RunGroups("stage1");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/2단계 (stage2)")] static void RunStage2() => RunGroups("stage2");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/3단계 (stage3)")] static void RunStage3() => RunGroups("stage3");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/4단계 (stage4)")] static void RunStage4() => RunGroups("stage4");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/5단계 (stage5)")] static void RunStage5() => RunGroups("stage5");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/이야기 종료 (ending)")] static void RunEnding() => RunGroups("ending");

    // text : 돌릴 그룹 이름들 (빈칸 · 쉼표로 구분). 비어 있거나 all 이면 전체
    static void RunGroups(string text)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var names = new List<string>(text.ToLowerInvariant().Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        if (names.Count == 0 || names.Contains("all")) names = new List<string>(AllGroups);
        string unknown = string.Join(" ", names.FindAll(n => Array.IndexOf(AllGroups, n) < 0));

        // Game 씬이 열려 있지 않으면 열기
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        // 지난 결과 지우기
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);

        // 이름을 잘못 적으면 아무것도 안 돌고 통과한 것처럼 보이지 않게, 실패로 남겨요
        if (unknown != "")
        {
            string message = $"[SelfTest] FAIL 없는 그룹 : {unknown} (쓸 수 있는 이름 : {string.Join(" ", AllGroups)})";
            File.WriteAllText(Path.Combine(OutDir, "result.txt"), message);
            Debug.LogWarning(message);
            return;
        }

        // 화면이 필요 없는 그룹만 고르면 Play 에 들어가지 않아요
        if (names.TrueForAll(n => n == "logic" || n == "judge"))
        {
            Begin(names.ToArray());
            foreach (Step s in steps)
            {
                try { s.run(); }
                catch (Exception e) { Fail($"{s.name}: 예외 {e.GetType().Name} {e.Message}"); }
            }
            index = steps.Count;
            Finish();
            return;
        }

        // Unity 창이 뒤에 있어도 게임이 멈추지 않게 (Project Settings → Player → Run In Background)
        PlayerSettings.runInBackground = true;

        SessionState.SetString(GroupsKey, string.Join(" ", names));
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void PollFlag()
    {
        if (EditorApplication.timeSinceStartup < nextFlagCheck) return;
        nextFlagCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isCompiling) return;

        // 누가 Play 중이면 먼저 멈추고, 멈춘 뒤 다음 확인 때 테스트 시작 (테스트 중일 땐 건드리지 않음)
        if (EditorApplication.isPlaying)
        {
            if (steps == null || index >= steps.Count) EditorApplication.ExitPlaymode();
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Unity 가 다른 창 뒤에 있으면 바뀐 코드를 아직 안 읽었을 수 있어요. 옛날 코드를 검사하지 않게 먼저 새로 읽어요.
        if (!refreshed)
        {
            refreshed = true;
            AssetDatabase.Refresh();
            nextFlagCheck = EditorApplication.timeSinceStartup + 2.0;   // 컴파일이 시작될 시간
            return;
        }
        refreshed = false;
        string groups = File.ReadAllText(Flag);
        File.Delete(Flag);
        RunGroups(groups);
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(RunningKey, false)) return;
        SessionState.SetBool(RunningKey, false);

        Begin(SessionState.GetString(GroupsKey, "").Split(' '));
        nextTime = EditorApplication.timeSinceStartup + Math.Max(steps[0].wait, 1.0);   // 게임이 준비될 시간
        EditorApplication.update += Tick;
    }

    static void Begin(string[] groups)
    {
        OpenAIJudge.DisableAI = true;  // 테스트에서는 AI 를 부르지 않음 (요금 X)
        report.Clear();
        failCount = 0;
        shotCount = 0;
        pending = null;
        Application.logMessageReceived += OnLog;
        bool whole = groups.Length == AllGroups.Length;
        scope = whole ? "" : $"부분({string.Join(" ", groups)}) ";
        steps = whole ? BuildSteps() : Pick(BuildSteps(), groups);
        index = 0;
    }

    // 고른 그룹의 단계만 남겨요. 단계 · 엔딩은 앞 그룹 없이 시작할 수 있게 입구를 끼워 넣어요
    static List<Step> Pick(List<Step> all, string[] groups)
    {
        var picked = new List<Step>();
        string last = null;
        foreach (Step s in all)
        {
            if (Array.IndexOf(groups, s.group) < 0) continue;
            if (s.group != last && (s.group.StartsWith("stage") || s.group == "ending")) picked.Add(EntryStep(s.group));
            last = s.group;
            picked.Add(s);
        }
        return picked;
    }

    static Step EntryStep(string group)
    {
        if (group != "ending")
        {
            int number = int.Parse(group.Substring("stage".Length));
            return new Step { wait = 0.3f, name = group + " 입구", group = group, run = () => StartAt(number) };
        }
        return new Step
        {
            wait = 0.3f, name = "ending 입구", group = group, run = () =>
            {
                // 전체 실행이 끝났을 때와 같은 기록 : 2단계만 엉뚱한 물건, 나머지는 찾음. 남은 사탕 2 · 0 · 2 · 2 · 2
                StartAt(1);
                StageData[] stages = Stages();
                var hidden = BindingFlags.NonPublic | BindingFlags.Instance;
                var items = (Sprite[])typeof(GameManager).GetField("itemsFound", hidden).GetValue(Gm());
                var candiesLeft = (int[])typeof(GameManager).GetField("candiesLeft", hidden).GetValue(Gm());
                for (int i = 0; i < stages.Length; i++)
                {
                    items[i] = i == 1 ? stages[i].wrongSprites[stages[i].wrongSprites.Length - 1] : stages[i].originalSprite;
                    candiesLeft[i] = i == 1 ? 0 : 2;
                }
                SetPrivate(Gm(), "stageIndex", stages.Length - 1);
                Gm().NextStage();
            }
        };
    }

    static void StartAt(int stageNumber)
    {
        SetPrivate(Gm(), "roamLoopSeconds", 0.05f);     // 검사에서는 찾는 동작을 기다리지 않아요
        SetPrivate(Gm(), "editorStartStage", stageNumber);
        Gm().StartGame();
    }

    // 정해진 시간이 지나면 다음 단계 실행
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }
        double now = EditorApplication.timeSinceStartup;

        // 스크린샷은 프레임이 끝날 때 저장돼요. 찍은 뒤 할 일(Then)은 1초 뒤에 따로 실행해요.
        if (pending != null)
        {
            if (now < pendingTime) return;
            Action p = pending;
            pending = null;
            try { p(); }
            catch (Exception e) { Fail($"{steps[index - 1].name}(다음 동작): 예외 {e.GetType().Name} {e.Message}"); }
            if (index >= steps.Count) Finish(); else nextTime = now + steps[index].wait;
            return;
        }

        if (now < nextTime) return;

        Step s = steps[index];
        if (s.until != null)
        {
            if (untilDeadline < 0) untilDeadline = now + 20.0;
            bool ready;
            try { ready = s.until(); } catch { ready = false; }
            if (!ready && now < untilDeadline) return;   // 아직이면 다음 프레임에 다시 확인
            untilDeadline = -1;
        }
        try { s.run(); }
        catch (Exception e) { Fail($"{s.name}: 예외 {e.GetType().Name} {e.Message}"); }

        index++;
        if (pending != null) { pendingTime = now + 1.0; return; }
        if (index >= steps.Count) { Finish(); return; }
        nextTime = now + steps[index].wait;
    }

    // 스크린샷을 찍은 "다음에" 할 일 예약
    static void Then(Action action) { pending = action; }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        OpenAIJudge.DisableAI = false;
        LocalJudge.TestOverride = null;
        pending = null;
        int passCount = report.FindAll(l => l.StartsWith("  PASS")).Count;
        string summary = failCount == 0 ? $"[SelfTest] {(scope == "" ? "전체 " : scope)}PASS ({passCount}개)" : $"[SelfTest] {scope}FAIL {failCount}개 / PASS {passCount}개";
        report.Add(summary);
        Directory.CreateDirectory(OutDir);
        File.WriteAllLines(Path.Combine(OutDir, "result.txt"), report);
        if (failCount == 0) Debug.Log(summary); else Debug.LogWarning(summary + "\n" + string.Join("\n", report));
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    // 게임 도중 에러 로그가 나오면 실패로 기록
    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            if (!message.StartsWith("[SelfTest]")) Fail("Console 에러: " + message);
        }
    }

    // ───────────── 검사 시나리오 ─────────────

    static List<Step> BuildSteps()
    {
        var list = new List<Step>();
        string group = "logic";     // 아래 Add 들이 속할 그룹
        void Add(float wait, string name, Action run, Func<bool> until = null) => list.Add(new Step { wait = wait, name = name, run = run, until = until, group = group });
        // 제출한 뒤 건네기 대사를 끝까지 넘겨요 (주인공 → 먹기 → 읽기 → 찾으러 가기)
        void PassGive() { if (IsOn("DialoguePanel/GiveNote") || (IsOn("DialoguePanel") && IsOn("HUD"))) Gm().DialogueTap(); }
        bool ResultShown() { PassGive(); return IsOn("ResultPanel") && Find("ResultPanel/ReturnedItem").localScale.x > 0.99f; } // 결과 + 뿅 애니메이션 끝

        // 0) 코드 단위 확인 (화면 없이)
        Add(0f, "효과음 파일", () =>
        {
            // Sfx 의 효과음 이름마다 Resources/Sound 에 파일이 있어야 해요 (배경음은 아직 없어서 빼요)
            foreach (FieldInfo f in typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(f.GetValue(null) is string name) || !name.StartsWith("sfx_")) continue;
                Check(Resources.Load<AudioClip>("Sound/" + name) != null, "효과음 " + name);
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
                bool all = true;
                foreach (Feature f in s.features) all &= prompt.Contains("featureId: " + f.id);
                all &= s.wrongSprites.Length == 1 << s.features.Length;
                for (int i = 1; i < s.wrongSprites.Length; i++) all &= s.wrongSprites[i] != null;
                Check(all && s.originalSprite != null, $"{s.Title} 데이터 (그림 + 특징 {s.features.Length}개 + 프롬프트)");
            }
        });

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
            ExpectJudge(all[0], HandBat(spikes: true), -1, "손그림 방망이 (선 손잡이 + 타원 머리 + 선 돌기)");
            ExpectJudge(all[0], HandBat(spikes: false), 0, "손그림 방망이, 돌기 없음");
            ExpectJudge(all[0], HandCloudBat(), -1, "손그림 방망이 (구름처럼 볼록한 머리)");
            ExpectJudge(all[1], HandBag(), -1, "손그림 가방 (손잡이 + 주머니 + 줄무늬)");
            ExpectJudge(all[1], HandBag(stripes: false), 1, "손그림 가방, 줄무늬 없음");
            ExpectJudge(all[2], HandMat(bands: true), -1, "손그림 돗자리 (파랑·빨강·노랑·파랑 띠, 쓱쓱 칠한 색)");
            ExpectJudgeMiss(all[2], HandMat(bands: false), new[] { 1, 2 }, "손그림 돗자리, 띠 없이 파랑 한 가지");
            ExpectJudge(all[3], HandGimbap(), -1, "손그림 김밥");
            ExpectJudge(all[4], HandFriend(), -1, "손그림 친구");
            // 흐름 검사에서 "하나 빠뜨림" 으로 제출하는 손그림이 정말 그 특징만 빠졌는지
            ExpectJudge(all[2], HandMat(bands: true, endsBlue: false), 2, "손그림 돗자리, 맨 아래 띠가 빨강");
            ExpectJudge(all[3], HandGimbap(green: false), 3, "손그림 김밥, 시금치가 파랑");
            ExpectJudge(all[4], HandFriend(brownHair: false), 4, "손그림 친구, 머리가 갈색이 아니라 주황");
        });

        // 1) 시작 화면
        group = "title";
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
            Find("DialoguePanel").gameObject.SetActive(false);
        });
        Add(0.2f, "시작 화면", () =>
        {
            SetPrivate(Gm(), "roamLoopSeconds", 0.05f);     // 검사에서는 찾는 동작을 기다리지 않아요
            Check(IsOn("TitlePanel") && !IsOn("HUD"), "시작 화면 (HUD 숨김)");
            Check(Find("Background").GetComponent<Image>().sprite.name == "BG_Start", "시작 화면 배경은 StartBG");
            Check(Find("TitlePanel/TitleLogo") != null && Find("TitlePanel/StartButton").GetComponent<Button>() != null, "시작 화면 물건은 따로 올려져 있다");
            Check(Text("TitlePanel/StartButton/Label") == "게임 시작하기", "시작 버튼은 게임 시작하기");
            Check(Find("TitlePanel").Find("TitleTagline") == null && Find("TitlePanel").Find("TitleGear") == null, "문구와 설정 버튼이 없다");
            Check(Find("JudgePanel/DokkaebiEnter/Roam/Step/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Think", "판정 대기는 고민하는 도깨비");
            Shot("title");
            Then(() => Gm().StartGame());
        });

        StageData[] stages = Stages();
        for (int si = 0; si < stages.Length; si++)
        {
            int s = si;                 // 람다 안에서 쓸 복사본
            StageData st = stages[s];
            bool last = s == stages.Length - 1;
            group = $"stage{s + 1}";

            // 대화 → 떠올리기
            Add(1.2f, $"{st.Title} 대화", () =>
            {
                Check(IsOn("DialoguePanel") && Find("Background").GetComponent<Image>().sprite.name == "BG_Room", $"{st.Title} 대화 화면 (배경은 BG_Room)");
                Check(!IsOn("HUD"), "대화 중에는 간식과 일시정지를 숨긴다");
                Check(IsOn("DialoguePanel/Boy"), "주인공이 보인다");
                Check(!IsOn("DialoguePanel/GiveNote"), "대화 중에는 쪽지가 없다");
                if (s == 0)
                {
                    Check(!IsOn("DialoguePanel/Dokkaebi"), "맨 처음에는 주인공 혼자 말한다");
                    Check(IsOn("DialoguePanel/Opening/Bag") && !IsOn("DialogueDim"), "오프닝은 어둡게 하지 않고 준비물이 보인다");
                }
                else Check(!IsOn("DialoguePanel/Opening"), "오프닝 그림은 1단계에만 나온다");
                if (s == 1) Check(Text("DialoguePanel/DialogueBox/Line").StartsWith("방망이도 찾았으니") || s != 1, "방망이를 찾았으면 2단계 첫 대사가 달라진다");
                Shot($"s{s + 1}_dialogue");
                Then(() =>
                {
                    if (s == 0)
                    {
                        for (int k = 0; k < 40 && !IsOn("DialoguePanel/Dokkaebi"); k++) Gm().DialogueTap();
                        int line = (int)Gm().GetType().GetField("dialogueLine", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());
                        Check(line == GameScript.OpeningAloneLines, "도깨비는 주인공 혼잣말이 끝난 뒤 등장한다");
                        Check(!IsOn("DialoguePanel/Opening") && IsOn("DialogueDim"), "도깨비가 나오면 오프닝 그림이 사라지고 어두워진다");
                    }
                    for (int k = 0; k < 40 && !IsOn("ObservePanel"); k++) Gm().DialogueTap();
                });
            });

            // 관찰 3초
            Add(0.4f, $"{st.Title} 관찰", () =>
            {
                Check(IsOn("ObservePanel") && Find("ObservePanel/Card/TargetImage").GetComponent<Image>().sprite == st.originalSprite, "원본 그림을 보여준다");
                Check(IsOn("ObservePanel/CountEdge/CountText"), "숫자 하나");
                Check(IsOn("ObservePanel/DokkaebiRecall") == (s == 0) && IsOn("ObservePanel/BoyRecall") == (s != 0), "1단계는 도깨비, 그 뒤는 주인공이 떠올린다");
                Check(Find("DialoguePanel") != null && !IsOn("DialoguePanel"), "떠올리는 장면은 대사 없이 나온다");
                if (s == 0) Shot($"s{s + 1}_observe");
            });

            if (s == 0)
            {
                // 1단계 첫 그리기 : 도깨비가 돋보기 쓰는 법을 보여 줘요. 시범은 공짜이고 그동안 향이 타지 않아요
                float timeAtGuide = 0f;
                Add(3.0f, "1단계 돋보기 시범", () =>
                {
                    Check(IsOn("DrawPanel/PeekGuide") && Text("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MagnifierShow, "그리기를 시작하면 도깨비가 돋보기를 눌러 보라고 한다");
                    Check(IsOn("HUD/MagnifierBadge") && Text("HUD/MagnifierBadge/Count") == "1", "돋보기 오른쪽 아래에 쓸 수 있는 횟수 1");
                    Check(Find("HUD").Find("ChocolateRow") == null, "맨 위 줄에 초콜릿이 없다");
                    Check(Find("HUD/CandyRow").childCount == 3 && CandiesLit() == 3, $"맨 위 줄에 사탕 3개 ({CandiesLit()})");
                    timeAtGuide = GetFloat("timeLeft");
                    // 사탕이 아직 날아 들어오는 중이면 겹쳐 보여서, 제자리에 둔 뒤 찍어요
                    foreach (SimpleAnim a in (SimpleAnim[])typeof(GameManager).GetField("snackAnims", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()))
                        a.enabled = false;
                    Shot("s1_peek_guide");
                    Then(() =>
                    {
                        Check(Mathf.Abs(GetFloat("timeLeft") - timeAtGuide) < 0.01f, "시범 대사 중에는 향이 타지 않는다");
                        Gm().PeekGuideTap();     // 글자 다 보이기
                        Gm().PeekGuideTap();     // 넘기기
                        Check(IsOn("DrawPanel/PeekGuide") && !IsOn("DrawPanel/PeekGuide/DialogueBox") && !IsOn("DrawPanel/MagnifierPeek"),
                              "대사를 넘기면 대사는 사라지고 아직 둘러보지 않는다");
                        Check(IsOn("HUD/MagnifierHighlight"), "맨 위 돋보기 둘레를 도깨비불이 돈다");
                        Gm().PeekGuideTap();
                        Check(!IsOn("DrawPanel/MagnifierPeek") && !IsOn("DrawPanel/PeekGuide/DialogueBox"), "화면을 눌러도 넘어가지 않는다 (돋보기를 직접 눌러야 해요)");
                    });
                }, () => IsOn("DrawPanel"));
                Add(0.8f, "1단계 돋보기 시범 누르기", () =>
                {
                    var fire = (RectTransform)Find("HUD/MagnifierHighlight/Orbit/Fire_0");
                    Invoke(Gm(), "SpinFires");
                    Check(IsOn("HUD/MagnifierHighlight/Orbit/Fire_2/Flame") && fire.anchoredPosition.magnitude > 60f, "도깨비불 셋이 돋보기 둘레에 있다");
                    Shot("s1_peek_highlight");
                    Then(() =>
                    {
                        Gm().MagnifierTap();
                        Check(!IsOn("HUD/MagnifierHighlight"), "돋보기를 누르면 강조가 사라진다");
                        Check(!IsOn("DrawPanel/MagnifierPeek") && Text("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MagnifierHow,
                              "돋보기를 누르면 바로 열지 않고 몇 초 동안 어떻게 보는지 말한다");
                        Shot("s1_peek_how");
                        Then(() =>
                        {
                            Gm().PeekGuideTap();     // 글자 다 보이기
                            Gm().PeekGuideTap();     // 넘기기
                            Check(IsOn("DrawPanel/MagnifierPeek") && !IsOn("DrawPanel/PeekGuide"), "대사를 넘기면 둘러본다");
                            Check(Gm().MagnifiersReady == 1, "시범은 공짜 (게임 전체 1번이 그대로)");
                            Check(Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>().RingFill > 0.9f, "처음에는 렌즈 둘레 띠가 가득");
                        });
                    });
                });
                Add(1.5f, "1단계 돋보기 시범 남은 시간", () =>
                {
                    var peek = Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>();
                    Check(peek.Active && peek.RingFill > 0.01f && peek.RingFill < 0.85f, $"시간이 지나면 띠가 줄어든다 ({peek.RingFill:0.00})");
                    Check(Mathf.Abs(peek.RingFill - peek.Left / 5f) < 0.05f, "띠는 남은 시간만큼 남아 있다");
                    Shot("s1_peek_count");
                });
                Add(2.0f, "1단계 돋보기 시범 끝", () =>
                {
                    Check(!IsOn("DrawPanel/MagnifierPeek"), "5초 뒤 저절로 닫힌다");
                    Check(IsOn("DrawPanel/PeekGuide") && Text("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MagnifierRule, "닫히면 도깨비가 한 번만 더 빌려준다고 말한다");
                    Check(Mathf.Abs(GetFloat("timeLeft") - timeAtGuide) < 0.01f, "시범으로 둘러보는 동안에도 향이 타지 않는다");
                    Shot("s1_peek_rule");
                    Then(() =>
                    {
                        Gm().PeekGuideTap();     // 글자 다 보이기
                        Gm().PeekGuideTap();     // 닫기
                        Check(!IsOn("DrawPanel/PeekGuide"), "대사를 누르면 그리기로 돌아간다");
                    });
                }, () => !IsOn("DrawPanel/MagnifierPeek"));
            }

            // 그리기 + 도구
            int candiesAtDraw = 0;      // 이번 단계 그리기를 시작할 때 남은 사탕
            Add(s == 0 ? 0.3f : 3.0f, $"{st.Title} 그리기", () =>
            {
                Check(IsOn("DrawPanel") && !IsOn("ObservePanel"), "3초 뒤 그리기 화면");
                Check(!IsOn("DrawPanel/PeekGuide"), "돋보기 시범은 1단계 첫 그리기에서만");
                candiesAtDraw = GetInt("candies");
                Check(candiesAtDraw == 3, $"{st.Title} 사탕 3개로 시작");
                bool primaries = st.colorMode != ColorMode.BlackOnly;
                bool mixing = st.colorMode == ColorMode.MixTwo || st.colorMode == ColorMode.MixFree;
                Check(IsOn("DrawPanel/ToolBar/ColorRow/Color_빨강") == primaries, $"빨강 버튼은 {(primaries ? "보임" : "숨김")}");
                Check(IsOn("DrawPanel/ToolBar/ColorRow/MixOpen") == mixing, $"색 섞기 버튼은 {(mixing ? "보임" : "숨김")}");
                Check(!Find("DrawPanel/SubmitButton").GetComponent<Button>().interactable, "빈 쪽지면 제출 비활성");
                Check(Text("DrawPanel/Guide") == GameScript.NoteText(s), "쪽지 글이 적혀 있다");
                DrawStage(st);
            }, () => IsOn("DrawPanel"));
            if (s == 0)
            {
                // 돋보기 : 게임 전체 1번을 여기서 써요
                float drawTimeAtPeek = 0f;
                Add(0.8f, "1단계 돋보기", () =>
                {
                    Check(IsOn("HUD/MagnifierButton") && !IsOn("DrawPanel/MagnifierPeek"), "맨 위 왼쪽에 돋보기");
                    drawTimeAtPeek = GetFloat("timeLeft");
                    Gm().MagnifierTap();
                    Check(IsOn("DrawPanel/MagnifierPeek") && GetInt("magnifiers") == 0, "누르면 바로 게임 전체 1번을 쓰고 둘러본다");
                    Check(!IsOn("HUD/MagnifierBadge") && !Find("HUD/MagnifierButton").GetComponent<Button>().interactable, "다 쓰면 숫자가 사라지고 돋보기가 흐려진다");
                    Check(CandiesLit() == candiesAtDraw, "돋보기는 사탕을 쓰지 않는다");
                    Check(Find("DrawPanel/MagnifierPeek/Lens/Hole/View/Original").GetComponent<Image>().sprite == st.originalSprite, "렌즈 안에는 원본");
                }, () => IsOn("DrawPanel"));
                Add(0.3f, "1단계 돋보기 렌즈", () =>
                {
                    var peek = Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>();
                    Check(peek.Active && peek.Left > 0f, "5초가 지나기 전에는 둘러보는 중");
                    Vector3 view = Find("DrawPanel/MagnifierPeek/Lens/Hole/View").position;
                    Find("DrawPanel/MagnifierPeek/Lens").position += new Vector3(80f, -60f, 0f);
                    Invoke(peek, "KeepView");
                    Check((Find("DrawPanel/MagnifierPeek/Lens/Hole/View").position - view).sqrMagnitude < 0.01f, "렌즈를 움직여도 원본은 그림판 자리에 그대로");
                    Shot("s1_magnifier");
                });
                Add(5.0f, "1단계 돋보기 끝", () =>
                {
                    float now = GetFloat("timeLeft");
                    Check(!IsOn("DrawPanel/MagnifierPeek"), "5초 뒤 저절로 닫힌다");
                    Check(drawTimeAtPeek - now > 4.5f, $"둘러보는 동안에도 향이 탄다 ({drawTimeAtPeek - now:0.0}초)");
                    Check(IsOn("DrawPanel") && Board().HasDrawn, "그리던 그림 그대로 그리기로 돌아온다");
                    Gm().MagnifierTap();
                    Check(!IsOn("DrawPanel/MagnifierPeek"), "다 쓰면 더 누를 수 없다");
                });
            }
            Add(0.8f, $"{st.Title} 그린 뒤", () =>
            {
                Check(Board().HasDrawn && Find("DrawPanel/SubmitButton").GetComponent<Button>().interactable, "그리면 제출 활성");
                Check(Find("DrawPanel/ToolBar/ToolRow/Undo/Button").GetComponent<Button>().interactable, "그리면 되돌리기 활성");
                Check(CandiesLit() == GetInt("candies"), $"위 줄에 남은 사탕 {GetInt("candies")}개");
                Check(IsOn("HUD/MagnifierBadge") == (Gm().MagnifiersReady > 0), $"돋보기 숫자는 쓸 수 있을 때만 ({Gm().MagnifiersReady})");
                Check(((RectTransform)Find("HUD/CandyRow/Candy_1")).anchoredPosition.x - ((RectTransform)Find("HUD/CandyRow/Candy_0")).anchoredPosition.x > 50f, "간식이 제자리로 들어왔다");
                if (st.colorMode == ColorMode.MixTwo || st.colorMode == ColorMode.MixFree)
                    Check(IsOn("DrawPanel/ToolBar/ColorRow/MixedSlot/Frame"), "섞은 색이 선택돼 있다");
                if (s == 0)
                {
                    Board().Clear();
                    Check(!Board().HasDrawn, "모두 지우기");
                    Board().Undo();
                    Check(Board().HasDrawn, "되돌리기로 지운 그림 복구");

                    // 그림판 밖으로 나갔다가 다른 곳으로 들어오기 : 왼쪽으로 나가서 아래로 돌아 들어옴
                    Board().Clear();
                    Stroke(V(0.3f, 0.5f), V(-0.3f, 0.5f), V(-0.3f, -0.3f), V(0.5f, -0.3f), V(0.5f, 0.3f));
                    Check(!Ink(0.25f, 0.25f), "밖으로 나간 곳과 들어온 곳이 직선으로 이어지지 않는다");
                    Check(Ink(0.002f, 0.5f) && Ink(0.5f, 0.002f), "선은 그림판 가장자리까지 그려진다");
                    Check(Ink(0.5f, 0.2f), "다시 들어온 뒤의 선은 그려진다");
                    Board().Undo();
                    Check(!Board().HasDrawn, "되돌리기로 빈 쪽지가 되면 빈 쪽지로 인식");
                    Board().Undo();
                    Check(Board().HasDrawn, "그 전 그림으로 되돌리기");
                }
                // 도구 확인이 끝나면, 이번 결과(강제로 넣는 판정)에 맞는 손그림으로 바꿔서 제출해요.
                PutSketch(st, allDrawn: false);
                Shot($"s{s + 1}_draw");
                Expect(st, s == 1 ? -1 : st.features.Length - 1); // 2단계는 전부 빠뜨림, 나머지는 마지막 특징만 빠뜨림
                if (s == 2)
                {
                    // 3단계 : 향이 다 타면 자동 제출되는지 (남은 시간을 0.3초로 줄여 봄)
                    SetPrivate(Gm(), "timeLeft", 30f);
                    new GameObject("SelfTestShot").AddComponent<ShotHelper>().path = Path.Combine(OutDir, "extra_incense_8s.png");
                    Then(() => SetPrivate(Gm(), "timeLeft", 0.3f));
                }
                else Then(() =>
                {
                    Gm().Submit();
                    if (s != 0) return;
                    // 건네기 : 그림이 적힌 쪽지 → 위쪽 사탕이 날아가며 하나 줄어요
                    Check(IsOn("DialoguePanel/GiveNote") && IsOn("HUD") && CandiesLit() == candiesAtDraw, "건네기 : 쪽지가 보이고 아직 사탕은 그대로");
                    Check(Find("DialoguePanel/GiveNote/MyDrawing").GetComponent<RawImage>().texture == Board().Texture, "쪽지에 내가 그린 그림");
                    Check(Text("DialoguePanel/GiveNote/Text") == GameScript.NoteText(s), "쪽지에 글이 적혀 있다");
                    Shot("s1_give");
                    Gm().DialogueTap();
                    Gm().DialogueTap();
                    Check(CandiesLit() == candiesAtDraw - 1 && IsOn("DialoguePanel/SnackOut"), "건네면 사탕 -1, 사탕이 도깨비에게 날아간다");
                });
            });

            // 판정 대기
            Add(s == 2 ? 1.0f : 0.3f, $"{st.Title} 판정", () =>
            {
                if (s == 2) Check(((RectTransform)Find("HUD/TimerGroup/Incense/IncenseFx_Tip")).anchoredPosition.y < 150f, "향이 다 타면 불꽃이 아래까지 내려온다");
                Check(IsOn("JudgePanel"), s == 2 ? "향이 다 타서 자동 제출됐다" : "판정 화면");
                Check(CandiesLit() == candiesAtDraw - 1, $"제출하면 사탕 -1 ({CandiesLit()})");
                Check(Find("HUD/CandyRow/Candy_0").GetComponent<Image>().color.a < 0.9f, "사탕은 왼쪽부터 흐려진다");
                Check(IsOn("JudgePanel/MyDrawingBack/MyDrawing"), "내 그림이 떠 있다");
                if (s == 0) Shot("s1_judge");

                if (s == 0)
                {
                    // AI 오류가 나면 사탕을 돌려주고, 대사를 누르면 그림 그대로 돌아가요
                    Invoke(Gm(), "OnJudgeError", "테스트용 가짜 오류");
                    Check(CandiesLit() == candiesAtDraw, "오류가 나면 사탕을 돌려준다");
                    Check(IsOn("ResultPanel/DialogueBox") && !IsOn("ResultPanel/ChoiceRow"), "도깨비가 오류를 말한다");
                    Shot("s1_ai_error");
                    Then(() =>
                    {
                        Gm().ResultTap();
                        Gm().ResultTap();
                        Check(IsOn("DrawPanel") && Board().HasDrawn, "그리던 그림 그대로 돌아왔다");
                        Gm().Submit();
                    });
                }
            }, () => { PassGive(); return IsOn("JudgePanel") && ((RectTransform)Find("JudgePanel/DokkaebiEnter")).anchoredPosition.x > -95f; });

            if (s == 1)
            {
                // 2단계 : 하나도 못 맞혀서 사탕 3개를 다 씀. 두 번째로 틀리면 무료 돋보기를 받아요
                Add(0.3f, "2단계 1번째 실패", () =>
                {
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.wrongSprites[st.wrongSprites.Length - 1], "1번째 : 하나도 못 맞히면 완전히 다른 물건");
                    TapResult();
                    Gm().ResultTap();
                    Gm().ResultTap();
                    Check(IsOn("ResultPanel/ChoiceRow") && !IsOn("ResultPanel/GiftMagnifier"), "처음 틀렸을 때는 무료 돋보기가 없다");
                    Gm().Retry();
                    Gm().Submit();
                }, () => ResultShown() && CandiesLit() == 2);
                int readyBeforeGift = 0;
                Add(0.3f, "2단계 2번째 실패 : 무료 돋보기", () =>
                {
                    readyBeforeGift = Gm().MagnifiersReady;
                    TapResult();
                    Gm().ResultTap();
                    Gm().ResultTap();
                    Check(Text("ResultPanel/DialogueBox/Line") == GameScript.MagnifierGift, "두 번째로 틀리면 도깨비가 돋보기를 빌려준다");
                    Check(IsOn("ResultPanel/GiftMagnifier") && !IsOn("ResultPanel/ChoiceRow"), "돋보기가 도깨비에게서 나타난다");
                    Check(Gm().MagnifiersReady == readyBeforeGift + 1, "무료 기회 +1");
                    Shot("s2_gift");
                }, () => ResultShown() && CandiesLit() == 1);
                Add(1.0f, "2단계 무료 돋보기 도착", () =>
                {
                    Check(IsOn("HUD/MagnifierBadge") && Text("HUD/MagnifierBadge/Count") == (readyBeforeGift + 1).ToString(), $"맨 위 돋보기에 도착하면 숫자가 {readyBeforeGift + 1}");
                    Check(IsOn("HUD/MagnifierSparkles"), "도착하면 반짝인다");
                    Shot("s2_gift_landed");
                    Then(() =>
                    {
                        for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                        Check(IsOn("ResultPanel/ChoiceRow"), "그 뒤에 다시 · 포기 버튼");
                        Gm().Retry();
                        int global = GetInt("magnifiers");
                        Gm().MagnifierTap();
                        Check(IsOn("DrawPanel/MagnifierPeek") && GetInt("magnifiers") == global && Gm().MagnifiersReady == global, "무료 기회부터 쓴다");
                        Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>().Stop();
                        Gm().Submit();
                    });
                }, () => !IsOn("ResultPanel/GiftMagnifier"));
                Add(0.3f, "2단계 사탕 0개", () =>
                {
                    Check(CandiesLit() == 0, "사탕 0개");
                    Check(!IsOn("ResultPanel/GiftMagnifier"), "무료 기회는 단계마다 한 번");
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.wrongSprites[st.wrongSprites.Length - 1], "0개 맞히면 완전히 다른 물건");
                    TapResult();
                    Check(!IsOn("ResultPanel/ChoiceRow"), "사탕이 없으면 다시 · 포기 버튼이 없다");
                    Check(Text("ResultPanel/DialogueBox/Line").Contains("사탕이 다 떨어졌어"), "사탕이 다 떨어졌다고 주인공이 말한다");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_BoyFace_Sad", "사탕이 떨어지면 우는 얼굴");
                    Shot("s2_out_of_snacks");
                    Then(() =>
                    {
                        Gm().ResultTap();   // 글자 다 보이기
                        Gm().ResultTap();   // 채점표로
                        Check(IsOn("ComparePanel"), "대사를 누르면 채점표로 넘어간다");
                    });
                }, () => ResultShown() && CandiesLit() == 0);
            }
            else
            {
                // 마지막 특징만 빠뜨림 → 그 조합의 물건 (1단계는 기준이 하나라 곧 전부 틀림)
                Add(0.3f, $"{st.Title} 부분 성공", () =>
                {
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.wrongSprites[1], "판정 조합에 맞는 물건이 왔다");
                    if (s == 0) Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_NoBat", "1단계를 틀리면 도깨비는 빈손");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Tease", "틀리면 도깨비는 놀리는 얼굴");
                    if (s == 0 || s == 4) Shot($"s{s + 1}_result_partial");
                    Action next = () =>
                    {
                        TapResult();
                        Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_BoyFace_Sad", "엉뚱한 물건이 오면 주인공은 우는 얼굴");
                        for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                        Check(IsOn("ResultPanel/ChoiceRow/RetryButton") && IsOn("ResultPanel/ChoiceRow/GiveUpButton"), "대사 뒤에 다시 · 포기 버튼");
                        Check(!IsOn("ResultPanel/DialogueBox"), "버튼은 대사 상자 자리에 나온다");
                        if (s == 0) Shot("s1_choice");
                        Gm().Retry();
                    };
                    if (s == 0 || s == 4) Then(next); else next();
                }, ResultShown);

                // 다시 → 성공
                Add(0.6f, $"{st.Title} 다시 그리기", () =>
                {
                    Check(IsOn("DrawPanel") && Board().HasDrawn, "다시 그리기는 이전 그림에서 이어진다");
                    PutSketch(st, allDrawn: true);
                    Expect(st, -2);     // 이번엔 전부 그림
                    Gm().Submit();
                });
                Add(0.3f, $"{st.Title} 성공", () =>
                {
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.originalSprite, "진짜 물건이 왔다");
                    if (s == 0) Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_NoBat", "1단계를 맞히면 도깨비는 빈손으로 방망이를 내민다");
                    else Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Swing", "성공하면 방망이를 휘두른다");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Proud", "맞히면 도깨비는 뿌듯한 얼굴");
                    CheckOneBat("결과 : 도깨비 대사");
                    TapResult();
                    if (s == 0) Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Normal"
                                      && !Find("ResultPanel/ReturnedItem").GetComponent<Image>().enabled, "1단계를 맞히면 주인공이 말할 때 도깨비가 방망이를 집어 든다");
                    CheckOneBat("결과 : 주인공 대사");
                    Check(!IsOn("ResultPanel/ChoiceRow"), "성공하면 다시 · 포기 버튼이 없다");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_BoyFace_Happy", "성공하면 기쁜 표정");
                    Shot($"s{s + 1}_result_success");
                    Then(() =>
                    {
                        Gm().ResultTap();
                        Gm().ResultTap();
                        Check(IsOn("ComparePanel"), "대사를 누르면 채점표로 넘어간다");
                    });
                }, () => ResultShown() && Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.originalSprite);
            }

            // 채점표 → 다음 단계
            Add(1.5f, $"{st.Title} 채점표", () =>
            {
                Check(IsOn("ComparePanel"), "채점표");
                int marks = 0;
                for (int k = 1; k <= 5; k++)
                    if (IsOn($"ComparePanel/Paper/Row_{k}"))
                        marks += IsOn($"ComparePanel/Paper/Row_{k}/Circle") != IsOn($"ComparePanel/Paper/Row_{k}/Slash") ? 1 : 0;
                Check(marks == st.features.Length, "번호마다 동그라미 또는 빗금");
                if (s == 1) Check(!IsOn("ComparePanel/Paper/Row_1/Circle") && IsOn("ComparePanel/Paper/Row_1/Slash"), "틀리면 빨간 빗금");
                else Check(IsOn("ComparePanel/Paper/Row_1/Circle"), "맞으면 초록 동그라미");
                Check(!(bool)typeof(GameManager).GetField("freePeek", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()), "단계가 끝나면 무료 기회는 사라진다");
                if (s <= 1 || last) Shot($"s{s + 1}_compare");
                if (s <= 1 || last) Then(() => Gm().NextStage());
                else Gm().NextStage();
            });
        }

        // 이야기 종료
        group = "ending";
        Add(3.2f, "이야기 종료", () =>
        {
            Check(IsOn("EndingPanel") && !IsOn("HUD"), "이야기 종료 화면");
            Check(Find("Background").GetComponent<Image>().sprite.name == "BG_Picnic", "소풍 배경");
            Check(Find("EndingPanel/BagSlot").GetComponent<Image>().sprite == stages[1].wrongSprites[stages[1].wrongSprites.Length - 1], "포기한 2단계는 받은 엉뚱한 물건이 놓인다");
            Check(Find("EndingPanel/MatSlot").GetComponent<Image>().sprite == stages[2].originalSprite, "찾은 물건은 진짜가 놓인다");
            Check(Find("EndingPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Normal" && !IsOn("EndingPanel/BatSlot"),
                  "방망이를 찾았으면 도깨비 손에만 있고 돗자리 방망이 자리는 비어 있다");
            int candiesLeft = SumInts("candiesLeft");
            Check(ShownCount("EndingPanel/Candy_") == candiesLeft, $"단계마다 남은 사탕 {candiesLeft}개가 돗자리에 모여 있다");
            Check(Find("EndingPanel").Find("Chocolate_1") == null, "초콜릿은 없다");
            Shot("ending");
            Then(() =>
            {
                Gm().EndingTap();
                Check(IsOn("EndingPanel/DialogueBox"), "화면을 누르면 도깨비가 물어본다");
                Shot("ending_ask");
                Then(() =>
                {
                    Gm().EndingTap();
                    Gm().EndingTap();
                });
            });
        });
        Add(0.3f, "처음으로", () =>
        {
            Check(IsOn("TitlePanel") && Find("Background").GetComponent<Image>().sprite.name == "BG_Start", "처음으로 돌아왔다 (배경도 StartBG)");
        });

        // 1단계를 못 찾으면 이야기가 끝날 때까지 도깨비가 빈손이고, 소풍 장면에 대파가 놓여요
        Add(0.3f, "방망이를 못 찾음", () =>
        {
            Sprite Shown(string path) => Find(path).GetComponent<Image>().sprite;
            const string NoBat = "Char_Dokkaebi_NoBat";
            Gm().StartGame();
            Invoke(Gm(), "ShowResult", new object[] { new[] { false }, null });
            Gm().ShowCompare();
            Gm().NextStage();
            Check(IsOn("DialoguePanel"), "채점표 다음은 바로 2단계 대화");
            Check(Shown("DialoguePanel/Dokkaebi/Image").name == NoBat, "방망이를 못 찾으면 2단계 대화에서 빈손");
            Invoke(Gm(), "ShowResult", new object[] { new[] { true, true }, null });
            Check(Shown("ResultPanel/Dokkaebi").name == NoBat, "다음 단계를 맞혀도 빈손");
            TapResult();
            Check(Shown("ResultPanel/Dokkaebi").name == NoBat, "주인공이 말할 때도 빈손");
            Gm().ShowCompare();
            SetPrivate(Gm(), "stageIndex", stages.Length - 1);
            Gm().NextStage();
            Check(IsOn("EndingPanel") && Shown("EndingPanel/Dokkaebi").name == NoBat, "이야기 종료에서도 빈손");
            Check(Shown("EndingPanel/BatSlot") == stages[0].wrongSprites[1], "소풍 장면에는 대파가 놓인다");
            for (int k = 0; k < 3; k++) Gm().EndingTap();
            Check(IsOn("TitlePanel"), "빈손으로 끝나도 처음으로 돌아간다");
        });

        return list;
    }

    // 단계에 맞게 그리기 (색 단계면 색 바꿔가며, 혼색 단계면 팔레트로 색을 만들어서)
    static void DrawStage(StageData st)
    {
        ToolBar tb = Find("DrawPanel/ToolBar").GetComponent<ToolBar>();
        tb.SelectSize(1);
        tb.SelectColor(0);
        Stroke(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0.8f, 0.8f), new Vector2(0.2f, 0.8f), new Vector2(0.2f, 0.2f));

        if (st.colorMode != ColorMode.BlackOnly)
        {
            tb.SelectSize(2);
            tb.SelectColor(1); Stroke(new Vector2(0.3f, 0.7f), new Vector2(0.45f, 0.7f));
            tb.SelectColor(2); Stroke(new Vector2(0.55f, 0.7f), new Vector2(0.7f, 0.7f));
            tb.SelectColor(3); Stroke(new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.35f));
        }
        if (st.colorMode == ColorMode.MixTwo || st.colorMode == ColorMode.MixFree)
        {
            ColorMixer mixer = Find("DrawPanel/MixerPanel").GetComponent<ColorMixer>();
            tb.OpenMixer();
            Check(IsOn("DrawPanel/MixerPanel"), "색 섞기를 누르면 팔레트가 열린다");
            if (st.colorMode == ColorMode.MixTwo)
            {
                mixer.AddPaint(1); mixer.AddPaint(2);
                mixer.AddPaint(0); // 두 색 혼합 단계에서는 세 번째 물감이 안 들어가야 함
                Check(mixer.ResultColor == ColorMixer.Mix(0, 1, 1), "두 색 혼합 : 세 번째 물감은 안 들어간다");
            }
            else
            {
                mixer.AddPaint(0); mixer.AddPaint(1); mixer.AddPaint(2);
                Check(mixer.ResultColor == ColorMixer.Mix(1, 1, 1), "자유 혼합 : 세 색을 한 번씩 넣으면 검은색에 가깝다");
                Check(MixerPreview() != mixer.ResultColor, "물감을 넣어도 색이 한 번에 바뀌지 않는다");
                Check(Find("DrawPanel/MixerPanel/PaintDrop") != null, "물감을 누르면 물방울이 뿌려진다");
                mixer.AddPaint(0); mixer.AddPaint(1);
                Check(mixer.ResultColor == ColorMixer.Mix(2, 2, 1), "자유 혼합 : 2:2:1 로 넣으면 갈색");
                mixer.SkipAnimation();
                mixer.UndoPaint();
                Check(mixer.ResultColor == ColorMixer.Mix(2, 1, 1), "되돌리기 : 마지막에 넣은 물감이 빠진다");
                Check(mixer.DropCount > 0, "되돌리기 : 물방울이 물감으로 돌아간다");
                Check(MixerPreview() != mixer.ResultColor, "되돌리기 : 색이 한 번에 바뀌지 않는다");
                mixer.AddPaint(1);
            }
            mixer.SkipAnimation();
            Check(MixerPreview() == mixer.ResultColor, "움직임이 끝나면 만든 색이 보인다");
            // 그리기가 막 시작돼서 간식이 아직 날아 들어오는 중일 수 있어요. 날아오는 사탕끼리 잠깐 겹친 걸
            // 배치 문제로 잡지 않게, 간식 움직임을 끝낸(제자리에 둔) 뒤 검사해요.
            foreach (SimpleAnim a in (SimpleAnim[])typeof(GameManager).GetField("snackAnims", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()))
                a.enabled = false;
            LayoutOnly($"s{st.stageNumber}_mixer");                     // 팔레트가 열린 화면도 배치 검사
            mixer.Apply();
            Check(!IsOn("DrawPanel/MixerPanel"), "섞어서 칠하면 팔레트가 닫힌다");
            var gmPause = Object.FindFirstObjectByType<GameManager>();
            gmPause.TogglePause();                                          // 일시정지 화면도 배치 검사
            Check(IsOn("PausePanel"), "일시정지 버튼을 누르면 일시정지 화면");
            LayoutOnly($"s{st.stageNumber}_pause");
            gmPause.TogglePause();
            Check(!IsOn("PausePanel") && Time.timeScale == 1f, "계속하기를 누르면 다시 진행");
            Stroke(new Vector2(0.35f, 0.5f), new Vector2(0.65f, 0.5f));
        }
    }

    // ───────────── 도우미 ─────────────

    // 팔레트 미리보기 칸의 지금 색
    static Color MixerPreview() => Find("DrawPanel/MixerPanel/PreviewEdge/Preview").GetComponent<UnityEngine.UI.Image>().color;

    static GameManager Gm() => Object.FindFirstObjectByType<GameManager>();

    // 결과 화면에서 도깨비 대사를 넘겨 주인공 대사까지 (글자 다 보이기 + 다음)
    static void TapResult()
    {
        Gm().ResultTap();
        Gm().ResultTap();
    }

    internal static StageData[] Stages()
    {
        var so = new SerializedObject(Gm());
        SerializedProperty p = so.FindProperty("stages");
        var arr = new StageData[p.arraySize];
        for (int i = 0; i < arr.Length; i++) arr[i] = (StageData)p.GetArrayElementAtIndex(i).objectReferenceValue;
        return arr;
    }

    // Canvas 아래에서 이름 경로로 찾기 (꺼져 있는 오브젝트도 찾을 수 있어요)
    //   게임 화면들은 Canvas/Frame(시안 크기 화면) 안에, 시작·일시정지 화면은 Canvas 바로 아래에 있어요.
    internal static Transform Find(string path)
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        Transform t = canvas.Find("Frame/" + path) ?? canvas.Find(path);
        if (t == null) throw new Exception("오브젝트 없음: " + path);
        return t;
    }

    internal static bool IsOn(string path) => Find(path).gameObject.activeInHierarchy;
    static string Text(string path) => Find(path).GetComponent<TMP_Text>().text;
    static DrawingBoard Board() => Find("DrawPanel/Board").GetComponent<DrawingBoard>();

    // 또렷하게 보이는 사탕 수 = 남은 수
    static int CandiesLit() => Lit("HUD/CandyRow");
    static int Lit(string row)
    {
        int lit = 0;
        foreach (Image icon in Find(row).GetComponentsInChildren<Image>())
            if (icon.color.a > 0.9f) lit++;
        return lit;
    }

    static int SumInts(string field)
    {
        int sum = 0;
        foreach (int v in (int[])typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm())) sum += v;
        return sum;
    }

    static int GetInt(string field) =>
        (int)typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());

    static float GetFloat(string field) =>
        (float)typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());

    // 이름이 prefix1, prefix2 … 인 것 중 켜져 있는 수 (이야기 종료의 남은 간식)
    static int ShownCount(string prefix)
    {
        int n = 0;
        for (int k = 1; k <= 10; k++) if (IsOn(prefix + k)) n++;
        return n;
    }
    static int Count(string text, string word) => text.Split(new[] { word }, StringSplitOptions.None).Length - 1;

    // 다음 판정 결과를 미리 정해 두기. miss = 빠뜨릴 특징 번호 (-1 = 전부 빠뜨림, -2 = 전부 그림)
    internal static void Expect(StageData stage, int miss)
    {
        LocalJudge.TestOverride = st =>
        {
            int n = st.features.Length;
            var v = new OpenAIJudge.Verdict { present = new bool[n], notes = new string[n] };
            for (int i = 0; i < n; i++) v.present[i] = miss == -2 || (miss >= 0 && i != miss);
            return v;
        };
    }

    // 손그림 판정 확인. miss = 빠져야 하는 특징 번호 (-1 = 전부 그림이어야 함)
    static void ExpectJudge(StageData stage, Texture2D tex, int miss, string what) =>
        ExpectJudgeMiss(stage, tex, miss < 0 ? new int[0] : new[] { miss }, what);

    // 빠뜨렸다고 나와야 하는 특징이 여러 개일 때 (예: 띠가 없으면 띠 색도 없음)
    static void ExpectJudgeMiss(StageData stage, Texture2D tex, int[] miss, string what)
    {
        LocalJudge.TestOverride = null;
        OpenAIJudge.Verdict v = LocalJudge.Judge(stage, tex);
        bool ok = true;
        string detail = "";
        for (int i = 0; i < v.present.Length; i++)
        {
            bool want = System.Array.IndexOf(miss, i) < 0;
            if (v.present[i] != want) { ok = false; detail += $" {stage.features[i].id}({v.notes[i]})"; }
        }
        Check(ok, $"손그림 판정 : {what}" + (ok ? "" : " / 틀린 것:" + detail));
    }

    // 손그림은 HandSketch.cs 로 만들어요 (판정 전수 검사와 같이 씀)
    internal static readonly Color32 InkBlack = HandSketch.Black;

    static Color32 C(int i) => ToolBar.Palette[i];          // 0 검정, 1 빨강, 2 노랑, 3 파랑
    internal static Vector2 V(float x, float y) => new Vector2(x, y);

    internal static Texture2D HandBat(bool spikes)
    {
        var s = new HandSketch()
            .Line(InkBlack, 4, 2f, V(248, 40), V(252, 140), V(256, 246))               // 한 줄로 그은 손잡이
            .Ellipse(InkBlack, 4, V(256, 335), 58, 88, gapDeg: 14f);                  // 살짝 안 닫힌 타원 머리
        if (spikes)
        {
            foreach (float a in new[] { 25f, 70f, 115f, 160f })
            {
                Vector2 dir = V(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 edge = V(256, 335) + new Vector2(dir.x * 58, dir.y * 88);
                s.Line(InkBlack, 4, 1f, edge, edge + dir * 55);                       // 선으로 그은 돌기
            }
        }
        return s.Tex();
    }

    // 시안 그림처럼 동그라미 여러 개를 이어 그린 구름 머리
    internal static Texture2D HandCloudBat()
    {
        var s = new HandSketch().Line(InkBlack, 4, 2f, V(248, 40), V(252, 140), V(256, 246));
        foreach (float a in new[] { 20f, 75f, 130f, 185f, 250f, 330f })
        {
            Vector2 c = V(256, 330) + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 58;
            s.Ellipse(InkBlack, 4, c, 38, 38);
        }
        return s.Tex();
    }

    internal static Texture2D HandBag(bool stripes = true)
    {
        var s = new HandSketch()
            .Line(InkBlack, 4, 2f, V(100, 60), V(412, 64), V(408, 330), V(104, 326), V(100, 60))   // 몸체
            .Line(InkBlack, 4, 2f, V(190, 328), V(200, 400), V(256, 440), V(312, 400), V(322, 328)) // 손잡이
            .Line(InkBlack, 4, 2f, V(170, 90), V(342, 92), V(340, 210), V(168, 206), V(170, 90));  // 주머니
        if (stripes)
            s.Line(InkBlack, 3, 1.5f, V(190, 120), V(322, 124))
             .Line(InkBlack, 3, 1.5f, V(188, 150), V(320, 152))
             .Line(InkBlack, 3, 1.5f, V(190, 180), V(322, 178));                                   // 줄무늬 3줄
        return s.Tex();
    }

    internal static Texture2D HandMat(bool bands, bool endsBlue = true)
    {
        var s = new HandSketch();
        if (bands)
        {
            s.Scribble(C(3), 6, 85, 325, 430, 372, 16);     // 맨 위 파랑 (성기게)
            s.Scribble(C(1), 6, 85, 268, 430, 315, 16);     // 빨강
            s.Scribble(C(2), 6, 85, 211, 430, 258, 16);     // 노랑
            s.Scribble(C(endsBlue ? 3 : 1), 6, 85, 158, 430, 201, 16);    // 맨 아래 파랑 (endsBlue 가 false 면 빨강)
        }
        else s.Scribble(C(3), 6, 85, 170, 430, 370, 16);    // 띠 없이 파랑 한 가지
        s.Line(InkBlack, 3, 3f, V(70, 150), V(440, 145), V(445, 385), V(65, 390), V(70, 150)); // 외곽
        return s.Tex();
    }

    internal static Texture2D HandGimbap(bool green = true)
    {
        Color32 mixedGreen = new Color32(40, 160, 60, 255);  // 노랑 + 파랑 으로 섞은 초록
        var s = new HandSketch()
            .Ellipse(InkBlack, 9, V(256, 256), 200, 196, gapDeg: 8f)
            .Scribble(C(1), 6, 160, 280, 240, 340, 14)
            .Scribble(C(2), 6, 280, 280, 360, 340, 14);
        s.Scribble(green ? mixedGreen : C(3), 6, 210, 170, 310, 230, 14);   // 안 섞으면 파랑
        return s.Tex();
    }

    internal static Texture2D HandFriend(bool brownHair = true)
    {
        Color32 orange = new Color32(245, 140, 30, 255), brown = new Color32(130, 80, 40, 255);   // 섞어서 만든 주황 · 갈색
        return new HandSketch()
            .Ellipse(InkBlack, 4, V(256, 400), 55, 55)                                     // 머리
            .Ellipse(InkBlack, 4, V(178, 395), 22, 28)
            .Ellipse(InkBlack, 4, V(334, 395), 22, 28)                                     // 양 갈래
            .Scribble(brownHair ? brown : orange, 8, 200, 405, 312, 458, 9)                // 머리카락
            .Scribble(orange, 8, 165, 225, 347, 335, 9)                                    // 주황 상의 (꼼꼼히)
            .Scribble(orange, 8, 110, 250, 165, 320, 9)
            .Scribble(orange, 8, 347, 250, 402, 320, 9)                                    // 팔
            .Scribble(C(3), 8, 175, 70, 235, 215, 9)
            .Scribble(C(3), 8, 280, 70, 340, 215, 9)                                       // 두 다리
            .Line(InkBlack, 5, 1.5f, V(240, 175), V(272, 175), V(272, 205), V(240, 205), V(240, 175))   // 천 조각
            .Tex();
    }

    // Sprite 원본 PNG 를 픽셀을 읽을 수 있는 텍스처로 불러오기 (자동 판정 검사용)
    static Texture2D LoadReadable(Sprite sprite)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        return tex;
    }

    // 흐름 검사에서 제출할 손그림 : allDrawn = 특징 전부, 아니면 그 단계 첫 제출의 기대 결과대로
    //   1 방망이 돌기 없음 · 2 가방 특징 없는 낙서 · 3 돗자리 맨 아래 띠 빨강 · 4 김밥 시금치가 파랑 · 5 친구 머리가 주황
    static void PutSketch(StageData st, bool allDrawn)
    {
        Texture2D tex;
        switch (st.stageNumber)
        {
            case 1: tex = allDrawn ? HandCloudBat() : HandBat(spikes: false); break;
            case 2: tex = allDrawn ? HandBag() : new HandSketch().Line(InkBlack, 4, 3f, V(120, 300), V(200, 250), V(260, 310), V(330, 240), V(400, 290)).Tex(); break;
            case 3: tex = HandMat(bands: true, endsBlue: allDrawn); break;
            case 4: tex = HandGimbap(green: allDrawn); break;
            default: tex = HandFriend(brownHair: allDrawn); break;
        }
        LoadSketch(tex);
    }

    // 손그림을 쪽지에 올려요 (흐름 사진 도구도 같이 써요)
    internal static void LoadSketch(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        var paper = new Color32(253, 252, 245, 255);                 // 손그림의 흰 종이 → 쪽지 종이 색
        for (int i = 0; i < px.Length; i++)
            if (px[i].r == 255 && px[i].g == 255 && px[i].b == 255) px[i] = paper;
        Board().LoadPixels(px);
    }

    // 그림판 비율 좌표(0~1)의 픽셀에 잉크가 있는지 (종이 색이 아니면 잉크)
    static bool Ink(float u, float v)
    {
        Texture2D t = Board().Texture;
        Color32 c = t.GetPixel(Mathf.Clamp((int)(u * t.width), 0, t.width - 1), Mathf.Clamp((int)(v * t.height), 0, t.height - 1));
        return !(c.r == 253 && c.g == 252 && c.b == 245);
    }

    // 그림판 위 비율 좌표(0~1)를 따라 가짜 드래그 (0~1 밖이면 그림판 밖)
    static void Stroke(params Vector2[] uv)
    {
        DrawingBoard board = Board();
        var rt = (RectTransform)board.transform;
        var c = new Vector3[4];
        rt.GetWorldCorners(c); // Overlay Canvas 는 월드 좌표 = 화면 좌표
        Vector2 Pt(Vector2 p) => new Vector2(Mathf.Lerp(c[0].x, c[2].x, p.x), Mathf.Lerp(c[0].y, c[2].y, p.y));

        var e = new PointerEventData(EventSystem.current) { position = Pt(uv[0]) };
        board.OnPointerDown(e);
        for (int i = 1; i < uv.Length; i++)
        {
            // 중간 점을 촘촘히 보내서 진짜 드래그처럼
            for (int k = 1; k <= 10; k++)
            {
                e.position = Pt(Vector2.Lerp(uv[i - 1], uv[i], k / 10f));
                board.OnDrag(e);
            }
        }
        board.OnPointerUp(e);
    }

    // private 함수·변수 건드리기 (테스트에서만)
    internal static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }

    internal static void SetPrivate(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    static void Check(bool ok, string what)
    {
        report.Add((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failCount++;
    }

    // 참고용 결과 (실패로 세지 않아요)
    static void Info(bool ok, string what)
    {
        report.Add((ok ? "  INFO  O " : "  INFO  X ") + what);
    }

    static void Fail(string what)
    {
        report.Add("  FAIL  " + what);
        failCount++;
    }

    // Game 창 스크린샷 : 게임 프레임이 끝나는 순간에 찍는 도우미를 잠깐 붙여요.
    // (ScreenCapture.CaptureScreenshot 은 에디터에서 "다음에 그려지는 화면"을 저장해서 한 장씩 밀렸어요)
    // 스크린샷 없이 배치 검사만 (잠깐 열렸다 닫히는 화면)
    static void LayoutOnly(string name)
    {
        List<string> layout = MvpLayoutCheck.Check(name);
        Check(layout.Count == 0, $"배치 검사 : {name}" + (layout.Count == 0 ? " (겹침 · 넘침 없음)" : $" 문제 {layout.Count}개"));
        foreach (string line in layout) report.Add("        " + line);
    }

    // 방망이는 한 화면에 하나만 보여야 해요 (도깨비가 든 방망이 + 바닥 · 돗자리의 방망이 그림을 함께 세요).
    //   채점표는 원본을 옆에 놓고 비교하는 종이라서 세지 않아요.
    static void CheckOneBat(string where)
    {
        Sprite batItem = Stages()[0].originalSprite;
        Transform compare = Find("ComparePanel");
        int count = 0;
        foreach (Image img in GameObject.Find("Canvas").GetComponentsInChildren<Image>())   // 켜진 것만 찾아요
        {
            if (!img.enabled || img.sprite == null || img.transform.IsChildOf(compare)) continue;
            string n = img.sprite.name;
            if (img.sprite == batItem || n == "Char_Dokkaebi_Normal" || n == "Char_Dokkaebi_Swing") count++;
        }
        Check(count <= 1, $"방망이는 한 화면에 하나만 : {where}" + (count <= 1 ? "" : $" ({count}개 보임)"));
    }

    static void Shot(string name)
    {
        CheckOneBat(name);
        // 화면 배치 숫자 검사 (걸쳐 겹침 · 글자 겹침 · 글자 넘침 · 화면 밖)
        List<string> layout = MvpLayoutCheck.Check(name);
        Check(layout.Count == 0, $"배치 검사 : {name}" + (layout.Count == 0 ? " (겹침 · 넘침 없음)" : $" 문제 {layout.Count}개"));
        foreach (string line in layout) report.Add("        " + line);
        shotCount++;
        var helper = new GameObject("SelfTestShot").AddComponent<ShotHelper>();
        helper.path = Path.Combine(OutDir, $"{shotCount:00}_{name}.png");
    }

    class ShotHelper : MonoBehaviour
    {
        public string path;

        System.Collections.IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) { Destroy(gameObject); yield break; } // Game 창이 안 그려지는 순간이면 건너뜀
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(gameObject);
        }
    }
}

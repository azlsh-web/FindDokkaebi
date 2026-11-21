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
//   lobby  : 로비 씬 파일이 빌드에 들어 있는지 (Play 에 들어가지 않아요)
//   start  : 게임 시작 · 설정 창 · 대사 줄바꿈
//   stage1 ~ stage5 : 1챕터의 물건 1 ~ 5 의 화면 흐름
//   ending : 1챕터 이야기 종료 · 방망이를 못 찾은 경우
//   chapter2 : 2챕터 (방 시작 장면 · 창밖 도깨비불 · 돋보기 시범 · 검정 펜 · 무료 돋보기 · 방 끝 장면 · 3챕터 열림)
//   chapter3 : 3챕터 (주황 머리 도깨비 · 색 안내 · 섞기 펜)
//   chapter4 : 4챕터 (사탕 0개 · 쪽지만 건네기 · 틀리면 바로 채점표 · 끝 독백과 빨간 눈)
//   chapter5 : 5챕터 (고르기 → 맞설 물건 성공 → 요청 물건 하트 -1 → 맞설 물건 실패 하트 -1 → 하트 0 게임오버 → 3단계 해피 엔딩)
//   물건이나 엔딩만 돌릴 때는 앞 물건을 건너뛰고 그 자리에서 시작해요 (EntryStep).
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
//   Game 씬이 열리면 1챕터가 바로 시작돼요 (ChapterSelect.Current 기본값 1).
//   1챕터 물건 1~5 (각 물건 : 대화 → 떠올리기 → 쪽지 쓰기 → 건네기 → 틀림 → 이어 그리기 → 성공 → 채점표 → 다음)
//   + 물건 1 : AI 오류 처리(간식 돌려주기), 모두 지우기/되돌리기
//   + 물건 1 : 도깨비가 돋보기 쓰는 법을 보여 줌 (공짜, 향이 안 탐) → 챕터 전체 1번을 씀
//   + 물건 2 : 두 번째로 틀리면 무료 돋보기를 받아서 씀 → 사탕 3개를 다 써서 포기 (틀릴 때마다 오답 그림 1 · 2 · 3)
//   + 물건 3 : 향이 다 타면 자동 제출
//   + 물건 4·5 : 혼색 팔레트 (노랑 1:파랑 1=초록, 빨강 2:노랑 2:파랑 1=갈색, 되돌리기)
//   → 이야기 종료 화면 기록 확인 → 도깨비 인사 (마지막 탭은 로비 씬을 불러서 눌러 보지 않아요)
//   + 방망이를 못 찾았을 때 : 이야기 종료까지 도깨비가 빈손이고 소풍 장면에 대파가 놓이는지
//   + 5챕터 : 시작 대사 → 고르기(그림 2장) → 떠올리기 → 그리기 → 판정은 미리 정해 넣기 (LocalJudge.TestOverride)
//   ※ 검사가 저장된 진행(Progress)을 건드려요. 시작 전에 적어 두었다가 끝나면 되돌려요.
//
// 결과
//   - Console 에 [SelfTest] 전체 PASS / FAIL n개
//   - Temp/SelfTest/result.txt 와 스크린샷 PNG 들 (Temp 는 Git 에 안 올라가요)
//
// ※ Play 에 들어가면 스크립트가 다시 로드돼서 static 변수가 초기화돼요.
//   그래서 "테스트 중인지"는 SessionState(에디터가 켜져 있는 동안 유지되는 저장소)에 적어둬요.
[InitializeOnLoad]
static partial class MvpSelfTest
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_selftest.flag";
    const string OutDir = "Temp/SelfTest";
    const string RunningKey = "MvpSelfTest.Running";
    const string GroupsKey = "MvpSelfTest.Groups";
    static readonly string[] AllGroups = { "logic", "judge", "lobby", "start", "stage1", "stage2", "stage3", "stage4", "stage5", "ending", "chapter2", "chapter3", "chapter4", "chapter5" };
    const string ProgressKey = "MvpSelfTest.Progress";

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
    [MenuItem("Tools/찾아라도깨비/부분 테스트/로비 씬 (lobby)")] static void RunLobby() => RunGroups("lobby");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/게임 시작 (start)")] static void RunStart() => RunGroups("start");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/1챕터 물건 1 (stage1)")] static void RunStage1() => RunGroups("stage1");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/1챕터 물건 2 (stage2)")] static void RunStage2() => RunGroups("stage2");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/1챕터 물건 3 (stage3)")] static void RunStage3() => RunGroups("stage3");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/1챕터 물건 4 (stage4)")] static void RunStage4() => RunGroups("stage4");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/1챕터 물건 5 (stage5)")] static void RunStage5() => RunGroups("stage5");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/이야기 종료 (ending)")] static void RunEnding() => RunGroups("ending");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/2챕터 (chapter2)")] static void RunChapter2() => RunGroups("chapter2");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/3챕터 (chapter3)")] static void RunChapter3() => RunGroups("chapter3");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/4챕터 (chapter4)")] static void RunChapter4() => RunGroups("chapter4");
    [MenuItem("Tools/찾아라도깨비/부분 테스트/5챕터 (chapter5)")] static void RunChapter5() => RunGroups("chapter5");

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

        SaveProgress();     // 검사가 저장된 진행을 건드리니 먼저 적어 둬요 (끝나면 되돌려요)

        // 화면이 필요 없는 그룹만 고르면 Play 에 들어가지 않아요
        if (names.TrueForAll(n => n == "logic" || n == "judge" || n == "lobby"))
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
                // 전체 실행이 끝났을 때와 같은 기록 : 2번째 물건만 엉뚱한 물건(3번째 오답), 나머지는 찾음
                StartAt(1);
                StageData[] stages = Stages();
                var hidden = BindingFlags.NonPublic | BindingFlags.Instance;
                var items = (Sprite[])typeof(GameManager).GetField("itemsFound", hidden).GetValue(Gm());
                for (int i = 0; i < stages.Length; i++)
                    items[i] = i == 1 ? stages[i].WrongSpriteFor(3) : stages[i].originalSprite;
                SetPrivate(Gm(), "stageIndex", stages.Length - 1);
                Gm().NextStage();
            }
        };
    }

    // 1챕터의 물건 stageNumber 번부터 시작
    static void StartAt(int stageNumber)
    {
        ChapterSelect.Current = 1;
        SetPrivate(Gm(), "roamLoopSeconds", 0.05f);     // 검사에서는 찾는 동작을 기다리지 않아요
        SetPrivate(Gm(), "editorStartStage", stageNumber);
        Gm().StartGame();
    }

    // 로비에서 고른 것처럼 챕터를 정하고 그 챕터를 처음부터 시작
    static void StartChapter(int number)
    {
        ChapterSelect.Current = number;
        SetPrivate(Gm(), "roamLoopSeconds", 0.05f);
        SetPrivate(Gm(), "editorStartStage", 1);
        Gm().StartGame();
    }

    // 진행 저장 (PlayerPrefs) 을 적어 두고 되돌리기 : 검사가 챕터를 깬 것으로 만들어 버리지 않게
    static void SaveProgress()
    {
        string text = "";
        for (int i = 1; i <= 5; i++) text += Progress.IsCleared(i) ? "1" : "0";
        text += Progress.BatKnown ? "1" : "0";
        text += Progress.BatFound ? "1" : "0";
        SessionState.SetString(ProgressKey, text);
    }

    static void RestoreProgress()
    {
        string text = SessionState.GetString(ProgressKey, "");
        if (text.Length != 7) return;
        Progress.ResetAll();
        for (int i = 1; i <= 5; i++) if (text[i - 1] == '1') Progress.MarkCleared(i);
        if (text[5] == '1') Progress.BatFound = text[6] == '1';
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
        RestoreProgress();
        ChapterSelect.Current = 1;
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
    //   그룹별 단계는 MvpSelfTest.Basic · Chapter1 · Rooms · Chapter5 .cs 에 있어요. 도우미는 MvpSelfTest.Helpers.cs 예요.

    static List<Step> list;         // 만들고 있는 단계 목록
    static string group;            // 아래 Add 들이 속할 그룹
    static void Add(float wait, string name, Action run, Func<bool> until = null) => list.Add(new Step { wait = wait, name = name, run = run, until = until, group = group });
    // 제출한 뒤 건네기 대사를 끝까지 넘겨요 (주인공 → 먹기 → 읽기 → 찾으러 가기)
    static void PassGive() { if (IsOn("DialoguePanel/GiveNote") || (IsOn("DialoguePanel") && IsOn("HUD"))) Gm().DialogueTap(); }
    static bool ResultShown() { PassGive(); return IsOn("ResultPanel") && Find("ResultPanel/ReturnedItem").localScale.x > 0.99f; } // 결과 + 뿅 애니메이션 끝

    static List<Step> BuildSteps()
    {
        list = new List<Step>();
        LogicSteps();
        JudgeSteps();
        LobbySteps();
        StartSteps();
        Chapter1Steps();
        RoomSteps();
        Chapter5Steps();
        return list;
    }
}

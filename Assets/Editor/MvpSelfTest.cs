using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ─────────────────────────────────────────────────────────────
// MvpSelfTest : 게임을 "자동으로 한 바퀴" 플레이해 보는 검사 도구 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 실행 방법
//   - 메뉴 Tools → 찾아라도깨비 → 자동 플레이 테스트
//   - 또는 Temp/mvp_selftest.flag 파일이 생기면 자동 실행 (Claude 가 원격으로 검사할 때 사용)
//
// 하는 일
//   Play 모드에 들어가서 사람이 버튼을 누르는 대신 GameManager 의 함수를 차례로 부르고,
//   마우스로 그리는 대신 그림판에 가짜 드래그 이벤트를 보내요.
//   단계마다 "화면이 맞게 바뀌었는지" 확인하고 Game 창 스크린샷을 찍어요.
//
// 결과
//   - Console 에 [SelfTest] PASS / FAIL 로 표시
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

    // 한 단계 = (앞 단계 후 기다릴 초, 이름, 할 일)
    struct Step { public float wait; public string name; public Action run; }

    static List<Step> steps;
    static int index;
    static double nextTime;
    static double nextFlagCheck;
    static readonly List<string> report = new List<string>();
    static int failCount;
    static int shotCount;

    static MvpSelfTest()
    {
        EditorApplication.update += PollFlag;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/찾아라도깨비/자동 플레이 테스트")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Game 씬이 열려 있지 않으면 열기
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        // 지난 결과 지우기
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);

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
        File.Delete(Flag);
        Run();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!SessionState.GetBool(RunningKey, false)) return;
        SessionState.SetBool(RunningKey, false);

        report.Clear();
        failCount = 0;
        shotCount = 0;
        Application.logMessageReceived += OnLog;
        steps = BuildSteps();
        index = 0;
        nextTime = EditorApplication.timeSinceStartup + steps[0].wait;
        EditorApplication.update += Tick;
    }

    // 정해진 시간이 지나면 다음 단계 실행
    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }
        double now = EditorApplication.timeSinceStartup;

        // 스크린샷은 "프레임 끝"에 찍혀서, 같은 프레임에 버튼을 누르면 다음 화면이 찍혀요.
        // 그래서 찍은 뒤 할 일(Then)은 1초 뒤에 따로 실행해요.
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
        try { s.run(); }
        catch (Exception e) { Fail($"{s.name}: 예외 {e.GetType().Name} {e.Message}"); }

        index++;
        if (pending != null) { pendingTime = now + 1.0; return; }
        if (index >= steps.Count) { Finish(); return; }
        nextTime = now + steps[index].wait;
    }

    static Action pending;
    static double pendingTime;

    // 스크린샷을 찍은 "다음에" 할 일 예약
    static void Then(Action action) { pending = action; }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        pending = null;
        string summary = failCount == 0 ? "[SelfTest] 전체 PASS" : $"[SelfTest] FAIL {failCount}개";
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
        void Add(float wait, string name, Action run) => list.Add(new Step { wait = wait, name = name, run = run });

        // 1) 도입 화면
        Add(1.0f, "도입 화면", () =>
        {
            Check(IsOn("IntroPanel"), "도입 패널이 켜져 있다");
            Check(Text("HUD/SnackText") == "간식 3개", "간식 3개로 시작 (" + Text("HUD/SnackText") + ")");
            Check(Text("IntroPanel/Story").Length > 0, "도입 이야기가 채워져 있다");
            Shot("intro");
            Then(() => Gm().StartObserve());
        });

        // 2) 관찰 화면
        Add(0.5f, "관찰 화면", () =>
        {
            Check(IsOn("ObservePanel"), "관찰 패널이 켜졌다");
            Check(Find("ObservePanel/TargetImage").GetComponent<Image>().sprite != null, "원본 그림이 보인다");
            Shot("observe");
        });

        // 3) 3초 뒤 그리기 화면
        Add(3.0f, "그리기 화면", () =>
        {
            Check(IsOn("DrawPanel") && !IsOn("ObservePanel"), "3초 뒤 그리기 패널로 넘어갔다");
            Check(Text("HUD/TimerText").StartsWith("향"), "향 타이머가 보인다 (" + Text("HUD/TimerText") + ")");
            Check(!Board().HasDrawn, "빈 쪽지로 시작");
            Check(!Find("DrawPanel/SubmitButton").GetComponent<Button>().interactable, "빈 쪽지면 제출 버튼 비활성");
            Shot("draw_empty");
            Then(() => DrawBat(Board(), spikes: false));
        });

        // 4) 그린 뒤 제출 버튼 활성화
        Add(0.3f, "그리기 후", () =>
        {
            Check(Board().HasDrawn, "그림판에 선이 그려졌다");
            Check(Find("DrawPanel/SubmitButton").GetComponent<Button>().interactable, "그리면 제출 버튼 활성");
            Shot("draw_done");
            Then(() => Gm().Submit());
        });

        // 5) 판정 화면
        Add(0.3f, "판정 화면", () =>
        {
            Check(IsOn("JudgePanel"), "판정 패널이 켜졌다");
            Check(Text("HUD/SnackText") == "간식 2개", "제출하면 간식 1개 사용 (" + Text("HUD/SnackText") + ")");
            Check(Text("JudgePanel/FeatureToggle1/Label").Contains("손잡이"), "체크박스에 특징 이름이 적혀 있다");
            SetToggles(true, true, false); // F3(돌기) 빠뜨림
            Shot("judge");
            Then(() => Gm().OnJudgeConfirmed());
        });

        // 6) 부분 성공 결과
        Add(0.3f, "부분 성공", () =>
        {
            Check(IsOn("ResultPanel"), "결과 패널이 켜졌다");
            Check(Text("ResultPanel/ResultTitle").Contains("엉뚱한"), "2/3 맞히면 엉뚱한 물건 (" + Text("ResultPanel/ResultTitle") + ")");
            Check(IsOn("ResultPanel/RetryButton"), "다시 그리기 버튼이 보인다");
            Shot("result_partial");
            Then(() => Gm().Retry());
        });

        // 7) 다시 그리기 → 전부 맞힘
        Add(0.3f, "다시 그리기", () =>
        {
            Check(IsOn("DrawPanel"), "다시 그리기는 원본 없이 바로 그리기 화면");
            Check(!Board().HasDrawn, "다시 그리면 쪽지가 비어 있다");
            DrawBat(Board(), spikes: true);
            Gm().Submit();
            SetToggles(true, true, true);
            Gm().OnJudgeConfirmed();
        });

        // 8) 성공 결과
        Add(0.3f, "성공", () =>
        {
            Check(Text("HUD/SnackText") == "간식 1개", "간식이 1개 남았다 (" + Text("HUD/SnackText") + ")");
            Check(Text("ResultPanel/ResultTitle").Contains("찾아왔다"), "3/3 맞히면 성공 (" + Text("ResultPanel/ResultTitle") + ")");
            Check(!IsOn("ResultPanel/RetryButton"), "성공하면 다시 그리기 버튼 숨김");
            Shot("result_success");
            Then(() => Gm().ShowCompare());
        });

        // 9) 비교 화면
        Add(0.3f, "비교 화면", () =>
        {
            Check(IsOn("ComparePanel"), "비교 패널이 켜졌다");
            Check(Find("ComparePanel/MyDrawing").GetComponent<RawImage>().texture != null, "내 그림이 보인다");
            Check(Find("ComparePanel/Original").GetComponent<Image>().sprite != null, "원본이 보인다");
            Shot("compare");
            Then(() => Gm().Restart());
        });

        // 10) 처음부터 다시 → 간식 다 쓰기
        Add(0.3f, "처음부터 다시", () =>
        {
            Check(IsOn("IntroPanel"), "도입 화면으로 돌아왔다");
            Check(Text("HUD/SnackText") == "간식 3개", "간식이 3개로 채워졌다");
            Gm().StartObserve();
        });
        Add(3.5f, "간식 다 쓰기", () =>
        {
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) Gm().Retry();
                Gm().Submit();
                SetToggles(false, false, false);
                Gm().OnJudgeConfirmed();
            }
        });
        Add(0.3f, "간식 0개", () =>
        {
            Check(Text("HUD/SnackText") == "간식 0개", "간식 0개 (" + Text("HUD/SnackText") + ")");
            Check(Text("ResultPanel/ResultTitle").Contains("아무것도"), "0/3 이면 아무것도 못 찾음");
            Check(!IsOn("ResultPanel/RetryButton"), "간식이 없으면 다시 그리기 버튼 숨김");
            Check(Text("ResultPanel/FeatureList").Contains("떨어졌어요"), "간식이 다 떨어졌다고 알려준다");
            Shot("out_of_snacks");
            Then(() => { }); // 마지막 스크린샷이 저장될 때까지 Play 를 잠깐 유지
        });

        return list;
    }

    // ───────────── 도우미 ─────────────

    static GameManager Gm() => Object.FindFirstObjectByType<GameManager>();

    // Canvas 아래에서 이름 경로로 찾기 (꺼져 있는 오브젝트도 찾을 수 있어요)
    static Transform Find(string path)
    {
        Transform t = GameObject.Find("Canvas").transform.Find(path);
        if (t == null) throw new Exception("오브젝트 없음: " + path);
        return t;
    }

    static bool IsOn(string path) => Find(path).gameObject.activeInHierarchy;
    static string Text(string path) => Find(path).GetComponent<TMP_Text>().text;
    static DrawingBoard Board() => Find("DrawPanel/Board").GetComponent<DrawingBoard>();

    static void SetToggles(params bool[] on)
    {
        for (int i = 0; i < on.Length; i++) Find($"JudgePanel/FeatureToggle{i + 1}").GetComponent<Toggle>().isOn = on[i];
    }

    // 마우스 대신 가짜 드래그로 방망이 그리기 (손잡이 + 머리 원 + 돌기)
    static void DrawBat(DrawingBoard board, bool spikes)
    {
        var rt = (RectTransform)board.transform;
        var c = new Vector3[4];
        rt.GetWorldCorners(c); // Overlay Canvas 는 월드 좌표 = 화면 좌표
        Vector2 Pt(float u, float v) => new Vector2(Mathf.Lerp(c[0].x, c[2].x, u), Mathf.Lerp(c[0].y, c[2].y, v));

        var strokes = new List<List<Vector2>>();
        strokes.Add(new List<Vector2> { Pt(0.5f, 0.1f), Pt(0.5f, 0.5f) }); // 손잡이
        var circle = new List<Vector2>();
        for (int i = 0; i <= 36; i++)
        {
            float a = i * 10f * Mathf.Deg2Rad;
            circle.Add(Pt(0.5f + 0.2f * Mathf.Cos(a), 0.7f + 0.2f * Mathf.Sin(a)));
        }
        strokes.Add(circle); // 머리
        if (spikes)
        {
            strokes.Add(new List<Vector2> { Pt(0.4f, 0.87f), Pt(0.35f, 0.97f), Pt(0.46f, 0.9f) });
            strokes.Add(new List<Vector2> { Pt(0.6f, 0.87f), Pt(0.65f, 0.97f), Pt(0.54f, 0.9f) });
        }

        foreach (var stroke in strokes)
        {
            var e = new PointerEventData(EventSystem.current) { position = stroke[0] };
            board.OnPointerDown(e);
            for (int i = 1; i < stroke.Count; i++) { e.position = stroke[i]; board.OnDrag(e); }
        }
    }

    static void Check(bool ok, string what)
    {
        string line = (ok ? "  PASS  " : "  FAIL  ") + what;
        report.Add(line);
        if (!ok) failCount++;
    }

    static void Fail(string what)
    {
        report.Add("  FAIL  " + what);
        failCount++;
    }

    // Game 창 스크린샷 (프레임이 끝날 때 저장돼요)
    static void Shot(string name)
    {
        shotCount++;
        // ScreenCapture.CaptureScreenshot 은 에디터에서 "다음에 그려지는 화면"을 저장해서 한 장씩 밀렸어요.
        // 그래서 게임 프레임이 끝나는 순간(WaitForEndOfFrame)에 직접 찍는 도우미를 잠깐 붙여서 찍어요.
        var helper = new GameObject("SelfTestShot").AddComponent<ShotHelper>();
        helper.path = Path.Combine(OutDir, $"{shotCount:00}_{name}.png");
    }

    // 테스트 중에만 잠깐 생겼다가 사라지는 스크린샷 도우미
    class ShotHelper : MonoBehaviour
    {
        public string path;

        System.Collections.IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(gameObject);
        }
    }
}

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
// 하는 일
//   Play 모드에 들어가서 사람이 버튼을 누르는 대신 GameManager·ToolBar 의 함수를 차례로 부르고,
//   마우스로 그리는 대신 그림판에 가짜 드래그 이벤트를 보내요.
//   화면마다 "맞게 바뀌었는지" 확인하고 Game 창 스크린샷을 찍어요.
//   AI 는 부르지 않아요 (OpenAIJudge.DisableAI = true).
//   - 자동 판정(LocalJudge) 자체는 원본·엉뚱한 물건 그림으로 따로 검사해요.
//   - 화면 흐름 검사에서는 "이번엔 이 특징을 빠뜨렸다"는 결과를 미리 정해서 넣어요 (LocalJudge.TestOverride).
//
// 시나리오
//   시작 → 1~5단계 (각 단계 : 부분 성공 → 다시 그리기 → 성공 → 비교 → 다음 단계)
//   + 1단계 : AI 오류 처리(간식 돌려주기), 모두 지우기/되돌리기
//   + 2단계 : 간식 3개를 다 써서 포기
//   + 3단계 : 향이 다 타면 자동 제출
//   + 4·5단계 : 혼색 팔레트 (노랑+파랑=초록, 빨강+노랑+파랑=갈색)
//   → 이야기 종료 화면 기록 확인
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

    // 한 단계 = (앞 단계 후 기다릴 초, 이름, 할 일, [조건])
    //   until 이 있으면 그 조건이 참이 될 때까지(최대 20초) 기다린 뒤 실행해요.
    //   Unity 창이 뒤에 있으면 게임 시계가 느려져서, 정해진 초 대신 "화면이 바뀔 때까지" 기다리는 게 안전해요.
    struct Step { public float wait; public string name; public Action run; public Func<bool> until; }
    static double untilDeadline = -1;

    static List<Step> steps;
    static int index;
    static double nextTime;
    static double nextFlagCheck;
    static readonly List<string> report = new List<string>();
    static int failCount;
    static int shotCount;
    static Action pending;
    static double pendingTime;

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

        // Unity 창이 뒤에 있어도 게임이 멈추지 않게 (Project Settings → Player → Run In Background)
        PlayerSettings.runInBackground = true;

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

        OpenAIJudge.DisableAI = true;  // 테스트에서는 AI 를 부르지 않음 (요금 X)
        report.Clear();
        failCount = 0;
        shotCount = 0;
        pending = null;
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
        string summary = failCount == 0 ? $"[SelfTest] 전체 PASS ({passCount}개)" : $"[SelfTest] FAIL {failCount}개 / PASS {passCount}개";
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
        void Add(float wait, string name, Action run, Func<bool> until = null) => list.Add(new Step { wait = wait, name = name, run = run, until = until });
        bool ResultShown() => IsOn("ResultPanel") && Find("ResultPanel/ReturnedItem").localScale.x > 0.99f; // 결과 + 뿅 애니메이션 끝

        // 0) 코드 단위 확인 (화면 없이)
        Add(1.0f, "기본 함수", () =>
        {
            Check(GameManager.Josa("가방", "을", "를") == "을" && GameManager.Josa("방망이", "을", "를") == "를", "조사(을/를) 고르기");
            Check(ColorMixer.MixTable(false, true, true, out _, out string g) && g == "초록", "노랑 + 파랑 = 초록");
            Check(ColorMixer.MixTable(true, true, true, out _, out string b) && b == "갈색", "빨강 + 노랑 + 파랑 = 갈색");
            foreach (StageData s in Stages())
            {
                string prompt = OpenAIJudge.BuildPrompt(s);
                bool all = true;
                foreach (Feature f in s.features) all &= prompt.Contains("featureId: " + f.id) && f.wrongSprite != null;
                Check(all && s.originalSprite != null, $"{s.Title} 데이터 (그림 + 특징 {s.features.Length}개 + 프롬프트)");
            }
        });

        // 0-2) 자동 판정(LocalJudge) : 원본은 전부 "그림", 엉뚱한 물건 Fn 은 Fn 이 "빠뜨림"이어야 해요
        Add(0.1f, "자동 판정", () =>
        {
            LocalJudge.TestOverride = null;
            foreach (StageData s in Stages())
            {
                OpenAIJudge.Verdict v = LocalJudge.Judge(s, LoadReadable(s.originalSprite));
                string miss = "";
                for (int i = 0; i < v.present.Length; i++) if (!v.present[i]) miss += $" {s.features[i].id}({v.notes[i]})";
                Check(miss == "", $"자동 판정 : {s.Title} 원본 = 전부 그림" + (miss == "" ? "" : " / 빠뜨렸다고 본 것:" + miss));
                for (int i = 0; i < s.features.Length; i++)
                {
                    OpenAIJudge.Verdict w = LocalJudge.Judge(s, LoadReadable(s.features[i].wrongSprite));
                    Check(!w.present[i], $"자동 판정 : {s.Title} {s.features[i].id} 빠진 그림 → {s.features[i].id} 빠뜨림 ({w.notes[i]})");
                }
            }
        });

        // 0-3) 손그림 판정 : 사람이 마우스로 그린 것처럼 삐뚤고, 조금 끊기고, 성기게 칠한 그림
        Add(0.1f, "손그림 판정", () =>
        {
            StageData[] all = Stages();
            ExpectJudge(all[0], HandBat(spikes: true), -1, "손그림 방망이 (선 손잡이 + 타원 머리 + 선 돌기)");
            ExpectJudge(all[0], HandBat(spikes: false), 2, "손그림 방망이, 돌기 없음");
            ExpectJudge(all[1], HandBag(), -1, "손그림 가방 (손잡이 + 주머니 + 줄무늬)");
            ExpectJudge(all[2], HandMat(cross: true), -1, "손그림 돗자리 (삐뚤한 선 + 쓱쓱 칠한 색)");
            ExpectJudge(all[2], HandMat(cross: false), 1, "손그림 돗자리, 십자선 없음");
            ExpectJudge(all[3], HandGimbap(), -1, "손그림 김밥");
            ExpectJudge(all[4], HandFriend(), -1, "손그림 친구");
        });

        // 1) 시작 화면
        Add(0.2f, "시작 화면", () =>
        {
            Check(IsOn("TitlePanel") && !IsOn("HUD"), "시작 화면 (HUD 숨김)");
            Shot("title");
            Then(() => Gm().StartGame());
        });

        StageData[] stages = Stages();
        for (int si = 0; si < stages.Length; si++)
        {
            int s = si;                 // 람다 안에서 쓸 복사본
            StageData st = stages[s];
            bool last = s == stages.Length - 1;

            // 단계 도입
            Add(0.3f, $"{st.Title} 도입", () =>
            {
                Check(IsOn("StageIntroPanel"), $"{st.Title} 도입 화면");
                Check(Text("HUD/SnackText") == "간식 3개", $"{st.Title} 간식 3개로 시작 ({Text("HUD/SnackText")})");
                Check(Text("StageIntroPanel/RulesCard/Rules").Contains("새로 배울 규칙"), "규칙 안내가 있다");
                Shot($"s{s + 1}_intro");
                Then(() => Gm().StartObserve());
            });

            // 관찰 3초
            Add(0.4f, $"{st.Title} 관찰", () =>
            {
                Check(IsOn("ObservePanel") && Find("ObservePanel/Card/TargetImage").GetComponent<Image>().sprite == st.originalSprite, "원본 그림을 보여준다");
                if (s == 0) Shot($"s{s + 1}_observe");
            });

            // 그리기 + 도구
            Add(3.0f, $"{st.Title} 그리기", () =>
            {
                Check(IsOn("DrawPanel") && !IsOn("ObservePanel"), "3초 뒤 그리기 화면");
                bool primaries = st.colorMode != ColorMode.BlackOnly;
                bool mixing = st.colorMode == ColorMode.MixTwo || st.colorMode == ColorMode.MixFree;
                Check(IsOn("DrawPanel/ToolBar/ColorRow/Color_빨강") == primaries, $"빨강 버튼은 {(primaries ? "보임" : "숨김")}");
                Check(IsOn("DrawPanel/ToolBar/ColorRow/MixOpen") == mixing, $"색 섞기 버튼은 {(mixing ? "보임" : "숨김")}");
                Check(!Find("DrawPanel/SubmitButton").GetComponent<Button>().interactable, "빈 쪽지면 제출 비활성");
                DrawStage(st);
            }, () => IsOn("DrawPanel"));
            Add(0.3f, $"{st.Title} 그린 뒤", () =>
            {
                Check(Board().HasDrawn && Find("DrawPanel/SubmitButton").GetComponent<Button>().interactable, "그리면 제출 활성");
                Check(Find("DrawPanel/ToolBar/ToolRow/Undo/Button").GetComponent<Button>().interactable, "그리면 되돌리기 활성");
                if (st.colorMode == ColorMode.MixTwo || st.colorMode == ColorMode.MixFree)
                    Check(IsOn("DrawPanel/ToolBar/ColorRow/MixedSlot/Frame"), "섞은 색이 선택돼 있다");
                if (s == 0)
                {
                    Board().Clear();
                    Check(!Board().HasDrawn, "모두 지우기");
                    Board().Undo();
                    Check(Board().HasDrawn, "되돌리기로 지운 그림 복구");
                }
                Shot($"s{s + 1}_draw");
                Expect(st, s == 1 ? -1 : st.features.Length - 1); // 2단계는 전부 빠뜨림, 나머지는 마지막 특징만 빠뜨림
                if (s == 2)
                {
                    // 3단계 : 향이 다 타면 자동 제출되는지 (남은 시간을 0.3초로 줄여 봄)
                    Then(() => SetPrivate(Gm(), "timeLeft", 0.3f));
                }
                else Then(() => Gm().Submit());
            });

            // 판정 화면
            Add(s == 2 ? 1.0f : 0.3f, $"{st.Title} 판정", () =>
            {
                Check(IsOn("JudgePanel"), s == 2 ? "향이 다 타서 자동 제출됐다" : "판정 화면 (쪽지와 간식을 건넴)");
                Check(Text("HUD/SnackText") == "간식 2개", $"제출하면 간식 -1 ({Text("HUD/SnackText")})");
                Check(Text("JudgePanel/Status").Contains("들여다보는"), "도깨비가 쪽지를 들여다보며 판정 중");
                Check(GameObject.Find("Canvas").transform.Find("JudgePanel/TestInput") == null, "체크박스(수동 판정)가 없다");

                if (s == 0)
                {
                    // AI 오류가 나면 간식을 돌려주고 그림 그대로 다시 제출
                    Invoke(Gm(), "OnJudgeError", "테스트용 가짜 오류");
                    Check(Text("HUD/SnackText") == "간식 3개", "오류가 나면 간식을 돌려준다");
                    Check(IsOn("JudgePanel/BackToDrawButton"), "다시 제출 버튼이 보인다");
                    Shot("s1_ai_error");
                    Then(() =>
                    {
                        Gm().BackToDraw();
                        Check(IsOn("DrawPanel") && Board().HasDrawn, "그리던 그림 그대로 돌아왔다");
                        Gm().Submit();
                    });
                }
            }, () => IsOn("JudgePanel"));

            if (s == 1)
            {
                // 2단계 : 하나도 못 맞혀서 간식 3개를 다 씀 → 포기
                for (int k = 1; k <= 2; k++)
                {
                    int kk = k;
                    Add(0.3f, $"2단계 {kk}번째 실패", () =>
                    {
                        Check(Text("ResultPanel/ResultTitle").Contains("아무것도"), $"{kk}번째 : 하나도 못 맞히면 아무것도 못 찾음");
                        Gm().Retry();
                        Gm().Submit();
                    }, ResultShown);
                }
                Add(0.3f, "2단계 간식 0개", () =>
                {
                    Check(Text("HUD/SnackText") == "간식 0개", "간식 0개");
                    Check(Text("ResultPanel/ResultTitle").Contains("아무것도"), "0개 맞히면 아무것도 못 찾음");
                    Check(!IsOn("ResultPanel/RetryButton"), "간식이 없으면 다시 그리기 숨김");
                    Check(Text("ResultPanel/SnackLeft").Contains("떨어졌어요"), "간식이 다 떨어졌다고 알려준다");
                    Shot("s2_out_of_snacks");
                    Then(() => Gm().ShowCompare());
                }, () => ResultShown() && Text("HUD/SnackText") == "간식 0개");
            }
            else
            {
                // 부분 성공 : 마지막 특징만 빠뜨림 → 그 특징이 빠진 물건
                Add(0.3f, $"{st.Title} 부분 성공", () =>   // 도깨비 고민 1.5초 + 물건이 뿅 커지는 0.45초 뒤 확인·촬영
                {
                    Check(Text("ResultPanel/ResultTitle").Contains("엉뚱한"), "하나 빠뜨리면 엉뚱한 물건");
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.features[st.features.Length - 1].wrongSprite, "빠뜨린 특징에 맞는 물건이 왔다");
                    Check(IsOn("ResultPanel/RetryButton"), "다시 그리기 버튼");
                    if (s == 0 || s == 4) Shot($"s{s + 1}_result_partial");
                    if (s == 0 || s == 4) Then(() => Gm().Retry()); else Gm().Retry();
                }, ResultShown);

                // 다시 그리기 → 성공
                Add(0.3f, $"{st.Title} 다시 그리기", () =>
                {
                    Check(IsOn("DrawPanel") && !Board().HasDrawn, "다시 그리기는 빈 쪽지 (원본 안 보여줌)");
                    DrawStage(st);
                    Expect(st, -2);     // 이번엔 전부 그림
                    Gm().Submit();
                });
                Add(0.3f, $"{st.Title} 성공", () =>
                {
                    Check(Text("ResultPanel/ResultTitle").Contains("찾아왔다"), "전부 맞히면 성공");
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.originalSprite, "진짜 물건이 왔다");
                    Check(!IsOn("ResultPanel/RetryButton"), "성공하면 다시 그리기 숨김");
                    Shot($"s{s + 1}_result_success");
                    Then(() => Gm().ShowCompare());
                }, () => ResultShown() && Text("ResultPanel/ResultTitle").Contains("찾아왔다"));
            }

            // 원본 비교 → 다음 단계
            Add(0.3f, $"{st.Title} 비교", () =>
            {
                Check(IsOn("ComparePanel"), "비교 화면");
                Check(Text("ComparePanel/NextButton/Label") == (last ? "오늘의 이야기 마치기" : "다음 단계로"), "다음 버튼 글자");
                if (s == 0 || last) Shot($"s{s + 1}_compare");
                if (s == 0 || last) Then(() => Gm().NextStage()); else Gm().NextStage();
            });
        }

        // 이야기 종료
        Add(0.3f, "이야기 종료", () =>
        {
            Check(IsOn("EndingPanel") && !IsOn("HUD"), "이야기 종료 화면");
            string rec = Text("EndingPanel/RecordCard/Records");
            Check(Count(rec, "찾았어요") == 4 && Count(rec, "포기했어요") == 1, "기록 : 4단계 찾음 + 2단계 포기");
            Check(Count(rec, "남은 간식") == 5, "기록에 단계별 남은 간식이 있다");
            Shot("ending");
            Then(() => Gm().BackToTitle());
        });
        Add(0.3f, "처음으로", () =>
        {
            Check(IsOn("TitlePanel"), "처음으로 돌아왔다");
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
                mixer.TogglePick(1); mixer.TogglePick(2);
                mixer.TogglePick(0); // 두 색 혼합 단계에서는 세 번째 재료는 안 골라져야 함
                Check(Text("DrawPanel/MixerPanel/Recipe") == "노랑 + 파랑 = 초록", "두 색 혼합 : " + Text("DrawPanel/MixerPanel/Recipe"));
            }
            else
            {
                mixer.TogglePick(0); mixer.TogglePick(1); mixer.TogglePick(2);
                Check(Text("DrawPanel/MixerPanel/Recipe") == "빨강 + 노랑 + 파랑 = 갈색", "자유 혼합 : " + Text("DrawPanel/MixerPanel/Recipe"));
            }
            mixer.Apply();
            Check(!IsOn("DrawPanel/MixerPanel"), "섞어서 칠하면 팔레트가 닫힌다");
            Stroke(new Vector2(0.35f, 0.5f), new Vector2(0.65f, 0.5f));
        }
    }

    // ───────────── 도우미 ─────────────

    static GameManager Gm() => Object.FindFirstObjectByType<GameManager>();

    static StageData[] Stages()
    {
        var so = new SerializedObject(Gm());
        SerializedProperty p = so.FindProperty("stages");
        var arr = new StageData[p.arraySize];
        for (int i = 0; i < arr.Length; i++) arr[i] = (StageData)p.GetArrayElementAtIndex(i).objectReferenceValue;
        return arr;
    }

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
    static int Count(string text, string word) => text.Split(new[] { word }, StringSplitOptions.None).Length - 1;

    // 다음 판정 결과를 미리 정해 두기. miss = 빠뜨릴 특징 번호 (-1 = 전부 빠뜨림, -2 = 전부 그림)
    static void Expect(StageData stage, int miss)
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
    static void ExpectJudge(StageData stage, Texture2D tex, int miss, string what)
    {
        LocalJudge.TestOverride = null;
        OpenAIJudge.Verdict v = LocalJudge.Judge(stage, tex);
        bool ok = true;
        string detail = "";
        for (int i = 0; i < v.present.Length; i++)
        {
            bool want = i != miss;
            if (v.present[i] != want) { ok = false; detail += $" {stage.features[i].id}({v.notes[i]})"; }
        }
        Check(ok, $"손그림 판정 : {what}" + (ok ? "" : " / 틀린 것:" + detail));
    }

    // ── 손그림 만들기 도구 (512x512, 아래가 y=0) ──
    static readonly Color32 PaperWhite = new Color32(255, 255, 255, 255);
    static readonly Color32 InkBlack = new Color32(0, 0, 0, 255);

    class Sketch
    {
        public readonly Color32[] px = new Color32[512 * 512];
        public Sketch() { for (int i = 0; i < px.Length; i++) px[i] = PaperWhite; }

        // 점들을 따라 선 긋기 (wobble = 손떨림 크기)
        public Sketch Line(Color32 c, int r, float wobble, params Vector2[] pts)
        {
            for (int k = 1; k < pts.Length; k++)
            {
                float len = Vector2.Distance(pts[k - 1], pts[k]);
                for (int t = 0; t <= len; t++)
                {
                    Vector2 q = Vector2.Lerp(pts[k - 1], pts[k], t / Mathf.Max(1f, len));
                    q += new Vector2(Mathf.Sin(q.y * 0.07f + k), Mathf.Cos(q.x * 0.06f + k)) * wobble;
                    Dot(q, r, c);
                }
            }
            return this;
        }

        // 타원 테두리 (gapDeg 만큼 비워서 살짝 안 닫힌 손그림처럼)
        public Sketch Ellipse(Color32 c, int r, Vector2 center, float rx, float ry, float gapDeg = 0f)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            for (float a = gapDeg; a <= 360f; a += 4f)
                pts.Add(center + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * rx, Mathf.Sin(a * Mathf.Deg2Rad) * ry));
            return Line(c, r, 1.5f, pts.ToArray());
        }

        // 네모 안을 지그재그로 쓱쓱 칠하기 (gap 이 클수록 성기게)
        public Sketch Scribble(Color32 c, int r, float x0, float y0, float x1, float y1, float gap)
        {
            var pts = new System.Collections.Generic.List<Vector2>();
            bool right = true;
            for (float y = y0; y <= y1; y += gap) { pts.Add(new Vector2(right ? x0 : x1, y)); pts.Add(new Vector2(right ? x1 : x0, y + gap * 0.5f)); right = !right; }
            return Line(c, r, 2f, pts.ToArray());
        }

        void Dot(Vector2 q, int r, Color32 c)
        {
            int cx = Mathf.RoundToInt(q.x), cy = Mathf.RoundToInt(q.y);
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if (x >= 0 && y >= 0 && x < 512 && y < 512 && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) px[y * 512 + x] = c;
        }

        public Texture2D Tex()
        {
            var t = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }

    static Color32 C(int i) => ToolBar.Palette[i];          // 0 검정, 1 빨강, 2 노랑, 3 파랑
    static Vector2 V(float x, float y) => new Vector2(x, y);

    static Texture2D HandBat(bool spikes)
    {
        var s = new Sketch()
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

    static Texture2D HandBag()
    {
        return new Sketch()
            .Line(InkBlack, 4, 2f, V(100, 60), V(412, 64), V(408, 330), V(104, 326), V(100, 60))   // 몸체
            .Line(InkBlack, 4, 2f, V(190, 328), V(200, 400), V(256, 440), V(312, 400), V(322, 328)) // 손잡이
            .Line(InkBlack, 4, 2f, V(170, 90), V(342, 92), V(340, 210), V(168, 206), V(170, 90))   // 주머니
            .Line(InkBlack, 3, 1.5f, V(190, 120), V(322, 124))
            .Line(InkBlack, 3, 1.5f, V(188, 150), V(320, 152))
            .Line(InkBlack, 3, 1.5f, V(190, 180), V(322, 178))                                     // 줄무늬 3줄
            .Tex();
    }

    static Texture2D HandMat(bool cross)
    {
        var s = new Sketch()
            .Scribble(C(1), 6, 90, 285, 235, 370, 22)      // 왼쪽 위 빨강 (성기게)
            .Scribble(C(2), 6, 280, 285, 425, 370, 22)     // 오른쪽 위 노랑
            .Scribble(C(3), 6, 90, 165, 235, 250, 22)      // 왼쪽 아래 파랑
            .Scribble(C(3), 6, 280, 165, 425, 250, 22)     // 오른쪽 아래 파랑
            .Line(InkBlack, 3, 3f, V(70, 150), V(440, 145), V(445, 385), V(65, 390), V(70, 150)); // 외곽
        if (cross)
        {
            s.Line(InkBlack, 3, 3f, V(255, 150), V(262, 270), V(258, 388));     // 삐뚤한 세로선
            s.Line(InkBlack, 3, 3f, V(68, 268), V(250, 262), V(442, 266));      // 삐뚤한 가로선
        }
        return s.Tex();
    }

    static Texture2D HandGimbap()
    {
        Color32 green = new Color32(40, 160, 60, 255);       // 노랑 + 파랑 으로 섞은 초록
        return new Sketch()
            .Ellipse(InkBlack, 9, V(256, 256), 200, 196, gapDeg: 8f)
            .Scribble(C(1), 6, 160, 280, 240, 340, 14)
            .Scribble(C(2), 6, 280, 280, 360, 340, 14)
            .Scribble(green, 6, 210, 170, 310, 230, 14)
            .Tex();
    }

    static Texture2D HandFriend()
    {
        Color32 green = new Color32(40, 160, 60, 255), brown = new Color32(130, 80, 40, 255);
        return new Sketch()
            .Ellipse(InkBlack, 4, V(256, 400), 55, 55)                                     // 머리
            .Scribble(green, 8, 165, 225, 347, 335, 9)                                     // 초록 상의 (꼼꼼히)
            .Scribble(green, 8, 110, 250, 165, 320, 9)
            .Scribble(green, 8, 347, 250, 402, 320, 9)                                     // 팔
            .Line(InkBlack, 4, 1f, V(165, 258), V(347, 260))
            .Line(InkBlack, 4, 1f, V(165, 285), V(347, 287))
            .Line(InkBlack, 4, 1f, V(165, 312), V(347, 310))                               // 검은 가로 줄무늬
            .Scribble(brown, 8, 175, 70, 235, 215, 9)
            .Scribble(brown, 8, 280, 70, 340, 215, 9)                                      // 갈색 두 다리
            .Tex();
    }

    // Sprite 원본 PNG 를 픽셀을 읽을 수 있는 텍스처로 불러오기 (자동 판정 검사용)
    static Texture2D LoadReadable(Sprite sprite)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        return tex;
    }

    // 그림판 위 비율 좌표(0~1)를 따라 가짜 드래그
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
    static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }

    static void SetPrivate(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    static void Check(bool ok, string what)
    {
        report.Add((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failCount++;
    }

    static void Fail(string what)
    {
        report.Add("  FAIL  " + what);
        failCount++;
    }

    // Game 창 스크린샷 : 게임 프레임이 끝나는 순간에 찍는 도우미를 잠깐 붙여요.
    // (ScreenCapture.CaptureScreenshot 은 에디터에서 "다음에 그려지는 화면"을 저장해서 한 장씩 밀렸어요)
    static void Shot(string name)
    {
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

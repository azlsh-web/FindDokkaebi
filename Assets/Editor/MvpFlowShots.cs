using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// ─────────────────────────────────────────────────────────────
// MvpFlowShots : 게임 플로우 사진을 전부 찍는 도구 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 실행 방법 : 메뉴 Tools → 찾아라도깨비 → 게임 플로우 사진 찍기 (또는 Temp/mvp_flowshots.flag)
//
// AI 는 부르지 않아요 (OpenAIJudge.DisableAI = true). 판정 결과는 LocalJudge.TestOverride 로 미리 정해요.
// 화면 전환이 끝나 멈춘 뒤에 찍고, 파일과 설명을 Temp/FlowShots 에 남겨요.
//   manifest.tsv : 파일 이름, 구역, 설명, 그때 나온 대사
//   done.txt     : 끝났다는 표시와 오류 내용
// 문서로 만드는 스크립트 : Tools/docs/make_flow_doc.py
[InitializeOnLoad]
static class MvpFlowShots
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_flowshots.flag";
    const string OutDir = "Temp/FlowShots";
    const string RunningKey = "MvpFlowShots.Running";
    static double nextFlagCheck;
    static bool refreshed;      // 코드가 바뀌어 컴파일되면 스크립트가 다시 불려서 false 로 돌아가요

    static MvpFlowShots()
    {
        EditorApplication.update += PollFlag;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/찾아라도깨비/게임 플로우 사진 찍기")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);
        PlayerSettings.runInBackground = true;
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void PollFlag()
    {
        if (EditorApplication.timeSinceStartup < nextFlagCheck) return;
        nextFlagCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;

        // Unity 가 다른 창 뒤에 있으면 바뀐 코드를 아직 안 읽었을 수 있어요. 먼저 새로 읽고, 다음 확인 때 찍어요.
        if (!refreshed)
        {
            refreshed = true;
            AssetDatabase.Refresh();
            nextFlagCheck = EditorApplication.timeSinceStartup + 5.0;   // 컴파일이 시작될 시간을 줘요 (Play 중에 컴파일되면 촬영이 끊겨요)
            return;
        }
        refreshed = false;
        File.Delete(Flag);
        Run();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
        SessionState.SetBool(RunningKey, false);
        OpenAIJudge.DisableAI = true;
        new GameObject("FlowShots").AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        readonly List<string> manifest = new List<string>();
        readonly List<string> errors = new List<string>();
        int shotCount;
        string section = "";
        GameManager gm;
        StageData[] stages;
        bool finished;

        void Start()
        {
            Application.logMessageReceived += OnLog;
            StartCoroutine(Play());
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors.Add(message);
            if (type == LogType.Exception) Finish();   // 코루틴이 멈췄으니 여기서 끝내요
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            OpenAIJudge.DisableAI = false;
            LocalJudge.TestOverride = null;
            File.WriteAllLines(Path.Combine(OutDir, "manifest.tsv"), manifest);
            string summary = $"[FlowShots] 사진 {shotCount}장, 오류 {errors.Count}개";
            errors.Insert(0, summary);
            File.WriteAllLines(Path.Combine(OutDir, "done.txt"), errors);
            Debug.Log(summary);
            EditorApplication.ExitPlaymode();
        }

        // ───────────── 도우미 ─────────────

        static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(seconds);
        static bool IsOn(string path) => MvpSelfTest.IsOn(path);

        // 조건이 참이 될 때까지 (최대 20초)
        IEnumerator Until(System.Func<bool> ready)
        {
            float end = Time.realtimeSinceStartup + 20f;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            if (!ready()) errors.Add($"기다렸지만 화면이 바뀌지 않았어요 (구역 {section}, {shotCount}장 뒤)");
        }

        // 지금 켜진 대사 상자들의 글자를 끝까지 보이게
        static void FinishBoxes()
        {
            foreach (DialogueBox box in Object.FindObjectsByType<DialogueBox>(FindObjectsSortMode.None)) box.FinishTyping();
        }

        static string CurrentLine()
        {
            foreach (DialogueBox box in Object.FindObjectsByType<DialogueBox>(FindObjectsSortMode.None))
            {
                Transform line = box.transform.Find("Line");
                if (line != null) return line.GetComponent<TMP_Text>().text;
            }
            return "";
        }

        // 움직임이 잦아들기를 기다렸다가 찍어요
        IEnumerator Snap(string caption, float settle = 0.6f)
        {
            yield return Wait(settle);
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            shotCount++;
            string file = $"{shotCount:000}.png";
            File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
            Destroy(tex);
            manifest.Add($"{file}\t{section}\t{caption}\t{CurrentLine().Replace("\n", " ")}");
        }

        static bool ResultShown() => IsOn("ResultPanel") && MvpSelfTest.Find("ResultPanel/ReturnedItem").localScale.x > 0.99f;

        static Texture2D Scribble() => new HandSketch()
            .Line(MvpSelfTest.InkBlack, 4, 3f, MvpSelfTest.V(120, 300), MvpSelfTest.V(200, 250), MvpSelfTest.V(260, 310), MvpSelfTest.V(330, 240), MvpSelfTest.V(400, 290))
            .Tex();

        static Texture2D PartialSketch(int s)
        {
            switch (s)
            {
                case 1: return MvpSelfTest.HandBag(stripes: false);
                case 2: return MvpSelfTest.HandMat(bands: true, endsBlue: false);
                case 3: return MvpSelfTest.HandGimbap(green: false);
                default: return MvpSelfTest.HandFriend(brownHair: false);
            }
        }

        static Texture2D RightSketch(int s)
        {
            switch (s)
            {
                case 0: return MvpSelfTest.HandCloudBat();
                case 1: return MvpSelfTest.HandBag();
                case 2: return MvpSelfTest.HandMat(bands: true);
                case 3: return MvpSelfTest.HandGimbap();
                default: return MvpSelfTest.HandFriend();
            }
        }

        // 제출하고 결과가 뜰 때까지. miss : -1 전부 틀림, -2 전부 맞음, 그 밖에는 빠뜨린 기준 번호
        IEnumerator SubmitAndWait(StageData st, int miss, string judgeCaption)
        {
            MvpSelfTest.Expect(st, miss);
            gm.Submit();
            if (judgeCaption != null) yield return GiveShots();
            else PassGive();
            if (judgeCaption != null) yield return Snap(judgeCaption, 1.5f);
            yield return Until(ResultShown);
        }

        // 건네기 대사를 사진 없이 넘겨요
        void PassGive()
        {
            for (int i = 0; i < 8 && IsOn("DialoguePanel"); i++) { FinishBoxes(); gm.DialogueTap(); }
        }

        // 건네기 : 주인공 → 먹기 → 읽기
        IEnumerator GiveShots()
        {
            string[] names = { "건네기 : 쪽지와 간식", "건네기 : 도깨비가 간식을 먹음", "건네기 : 도깨비가 쪽지를 읽음" };
            for (int i = 0; i < names.Length; i++)
            {
                yield return Wait(0.5f);
                FinishBoxes();
                yield return Snap(names[i]);
                FinishBoxes();
                gm.DialogueTap();
            }
        }

        // 도깨비 대사 → 주인공 대사까지 찍어요
        IEnumerator ResultLines(string name)
        {
            yield return Wait(0.5f);
            FinishBoxes();
            yield return Snap($"{name} : 도깨비가 물건을 가져옴");
            FinishBoxes();
            gm.ResultTap();
            yield return Wait(0.3f);
            FinishBoxes();
            yield return Snap($"{name} : 주인공의 반응", 0.4f);
            FinishBoxes();
            gm.ResultTap();
        }

        // ───────────── 전체 흐름 ─────────────

        IEnumerator Play()
        {
            Random.InitState(7);
            yield return Wait(1f);
            gm = Object.FindFirstObjectByType<GameManager>();
            stages = MvpSelfTest.Stages();
            MvpSelfTest.SetPrivate(gm, "roamLoopSeconds", 0.05f);
            MvpSelfTest.SetPrivate(gm, "thinkSeconds", 2.5f);   // 판정 대기 사진을 찍을 시간

            // 로비(시작 · 챕터 선택)는 Lobby 씬이라 여기서는 찍지 않아요. Game 씬은 1챕터로 바로 시작해요
            section = "시작";
            ChapterSelect.Current = 1;
            gm.StartGame();

            for (int s = 0; s < stages.Length; s++) yield return PlayStage(s);

            yield return PlayEndings();
            Finish();
        }

        IEnumerator PlayStage(int s)
        {
            StageData st = stages[s];
            section = $"1챕터 {s + 1}번째 물건 {st.targetName}";

            // 대화 (모든 대사는 터치로 넘겨요)
            bool batLost = (bool)typeof(GameManager).GetField("batLost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gm);
            GameScript.Line[] lines = GameScript.IntroLines(s, batLost);
            for (int i = 0; i < lines.Length; i++)
            {
                yield return Wait(0.5f);
                FinishBoxes();
                yield return Snap($"시작 대화 {i + 1}");
                FinishBoxes();
                gm.DialogueTap();
            }

            yield return Snap("떠올리기 3초", 0.7f);

            // 그리기
            yield return Until(() => IsOn("DrawPanel"));
            if (s == 0)
            {
                // 1챕터 첫 그리기 : 도깨비가 펜 쓰는 법을 알려 줘요 (그려 보기 → 되돌리기 → 다 그렸어!, 그동안 시간이 멈춰요)
                yield return Snap("사탕 3개가 오른쪽에서 들어오고, 도깨비가 쪽지에 그려 보라고 함", 0.2f);
                FinishBoxes();
                gm.PeekGuideTap();
                yield return Snap("대사를 넘기면 : 도깨비불이 쪽지 둘레를 돎", 0.6f);
                MvpSelfTest.LoadSketch(Scribble());
                yield return Wait(0.3f);
                FinishBoxes();
                yield return Snap("쪽지에 그리면 : 되돌리기를 알려 줌", 0.4f);
                gm.PeekGuideTap();
                yield return Snap("대사를 넘기면 : 도깨비불이 되돌리기 둘레를 돎", 0.6f);
                MvpSelfTest.Find("DrawPanel/Desk/Paper/Board").GetComponent<DrawingBoard>().Undo();
                yield return Wait(0.3f);
                FinishBoxes();
                yield return Snap("되돌리면 : 다 그렸어! 를 알려 줌", 0.4f);
                gm.PeekGuideTap();
                yield return Snap("대사를 넘기면 : 도깨비불이 다 그렸어! 둘레를 돎 (화면을 누르면 끝)", 0.6f);
                gm.PeekGuideTap();
            }
            yield return Snap("그리기 시작 : 빈 쪽지", 1.2f);

            if (s == 0)
            {
                gm.ToggleSettings();
                yield return Snap("톱니 : 소리 설정 (배경음 · 효과음, 여는 동안 멈춤)", 0.4f);
                gm.ToggleSettings();
                MvpSelfTest.SetPrivate(gm, "timeLeft", 8f);
                yield return Snap("향이 10초 아래로 남았을 때", 0.8f);
                MvpSelfTest.SetPrivate(gm, "timeLeft", 60f);

            }

            // ① 전부 틀림 → 다시
            MvpSelfTest.LoadSketch(s == 0 ? MvpSelfTest.HandBat(spikes: false) : Scribble());
            yield return Snap("그림을 그린 상태");
            yield return SubmitAndWait(st, -1, "판정 대기 : 도깨비가 찾으러 다님");
            yield return ResultLines("전부 틀림");
            yield return Snap("다시 · 포기 버튼", 0.5f);
            gm.Retry();

            if (s == 0)
            {
                // 판정 오류 : 간식을 돌려주고 그림으로 돌아가요
                yield return Snap("다시 : 이전 그림에 이어 그리기", 0.8f);
                gm.Submit();
                PassGive();
                yield return Wait(0.8f);
                MvpSelfTest.Invoke(gm, "OnJudgeError", "사진용 가짜 오류");
                yield return Wait(0.4f);
                FinishBoxes();
                yield return Snap("판정 오류 : 간식을 돌려줌");
                FinishBoxes();
                gm.ResultTap();
            }
            else
            {
                // ② 일부 틀림 → 포기하면 채점표
                yield return Snap("다시 : 이전 그림에 이어 그리기", 0.8f);
                MvpSelfTest.SetPrivate(gm, "candies", 2);      // 사진을 위해 사탕을 채워 다시 · 포기 버튼이 나오게 해요
                MvpSelfTest.Invoke(gm, "UpdateSnacks");
                MvpSelfTest.LoadSketch(PartialSketch(s));
                yield return SubmitAndWait(st, st.features.Length - 1, null);
                yield return ResultLines("일부 틀림");
                if (IsOn("ResultPanel/GiftMagnifier"))
                {
                    // 이 단계에서 두 번째로 틀렸어요 : 무료 돋보기 (2단계만 사진을 찍어요)
                    if (s == 1)
                    {
                        yield return Wait(0.5f);
                        FinishBoxes();
                        yield return Snap("두 번째로 틀리면 : 도깨비가 손에 쥔 돋보기를 빌려줌", 0.3f);
                        yield return Snap("무료 돋보기가 손을 떠나 맨 위 돋보기로 날아가는 중", 1.2f);
                        yield return Snap("도착하면 숫자가 늘고 반짝임", 0.7f);
                    }
                    FinishBoxes();
                    gm.ResultTap();
                }
                yield return Snap("다시 · 포기 버튼 (사탕이 남음)", 0.5f);
                gm.ShowCompare();
                yield return Snap("포기 : 채점표 (동그라미와 빗금)", 2.8f);
                gm.Retry();     // 사진을 위해 같은 단계를 이어 가요

                if (s == 1)
                {
                    // ③ 사탕을 다 썼을 때
                    yield return Wait(0.5f);
                    MvpSelfTest.SetPrivate(gm, "candies", 1);
                    MvpSelfTest.LoadSketch(Scribble());
                    yield return SubmitAndWait(st, -1, null);
                    yield return ResultLines("사탕을 다 씀");
                    yield return Snap("사탕이 없으면 버튼 없이 채점표", 2.8f);
                    MvpSelfTest.SetPrivate(gm, "candies", 1);
                    gm.Retry();
                }
                yield return Wait(0.5f);
            }

            // ④ 성공
            MvpSelfTest.LoadSketch(RightSketch(s));
            yield return SubmitAndWait(st, -2, null);
            yield return ResultLines("전부 맞음");
            yield return Snap("채점표 : 전부 동그라미", 2.8f);

            if (s == 0)
            {
                // 1단계를 못 찾은 경우 : 채점표 → 2단계부터 끝까지 빈손
                section = "1번째 물건을 못 찾았을 때";
                var hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                ((bool[])typeof(GameManager).GetField("cleared", hidden).GetValue(gm))[0] = false;
                MvpSelfTest.SetPrivate(gm, "lastPresent", new[] { false });
                gm.ShowCompare();
                yield return Snap("채점표 : 빗금", 2.8f);
                gm.NextStage();
                yield return Wait(0.5f);
                FinishBoxes();
                yield return Snap("2단계 대화 : 도깨비가 빈손", 0.8f);
                MvpSelfTest.Invoke(gm, "ShowResult", new object[] { new[] { true }, null });
                yield return Until(ResultShown);
                yield return Wait(0.3f);
                FinishBoxes();
                yield return Snap("다음 단계를 맞혀도 빈손", 0.6f);

                // 사진을 위해 방망이를 찾은 흐름으로 돌아가요
                MvpSelfTest.SetPrivate(gm, "batLost", false);
                MvpSelfTest.Invoke(gm, "StartStage", 1);
            }
            else if (s < stages.Length - 1) gm.NextStage();
        }

        IEnumerator PlayEndings()
        {
            section = "이야기 종료";
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var items = (Sprite[])typeof(GameManager).GetField("itemsFound", flags).GetValue(gm);

            // 전부 찾았을 때 : 소풍 장면
            for (int i = 0; i < stages.Length; i++) items[i] = stages[i].originalSprite;
            gm.NextStage();
            yield return Snap("전부 찾았을 때 : 소풍 장면", 3.8f);
            gm.EndingTap();
            yield return Wait(0.3f);
            FinishBoxes();
            yield return Snap("화면을 누르면 도깨비가 다음에 또 온다고 인사함 (한 번 더 누르면 로비로 가서 여기서 멈춰요)", 0.4f);

            // 포기한 물건이 있을 때 : 받은 엉뚱한 물건이 그대로 놓여요
            yield return Wait(0.5f);
            MvpSelfTest.SetPrivate(gm, "batLost", true);
            for (int i = 0; i < stages.Length; i++)
                items[i] = i == 2 ? stages[2].originalSprite : stages[i].AllWrongSprite;
            MvpSelfTest.Invoke(gm, "ShowEnding");
            yield return Snap("포기한 물건이 있을 때 : 엉뚱한 물건이 놓이고 도깨비는 빈손", 3.5f);

            for (int n = 2; n <= 4; n++) yield return PlayRoomChapter(n);
            yield return PlayChapter5();
        }

        // 2 · 3 · 4챕터 : 방 시작 장면 → 물건 3개 → 방 끝 장면 (판정은 미리 정해 넣어요)
        IEnumerator PlayRoomChapter(int number)
        {
            section = $"{number}챕터";
            ChapterSelect.Current = number;
            gm.StartGame();
            ChapterData c = MvpSelfTest.ChapterOf(number);
            var toolBar = MvpSelfTest.Find("DrawPanel/ToolBar").GetComponent<ToolBar>();

            for (int s = 0; s < c.ItemCount; s++)
            {
                StageData st = c.items[s];
                section = $"{number}챕터 {s + 1}번째 물건 {st.targetName}";
                GameScript.Line[] lines = GameScript.RoomIntroLines(number, s);
                for (int i = 0; i < lines.Length; i++)
                {
                    yield return Wait(0.5f);
                    FinishBoxes();
                    yield return Snap(s == 0 && i < GameScript.RoomOpeningLength(number) ? $"시작 장면 {i + 1}" : $"물건 대화 {i + 1 - (s == 0 ? GameScript.RoomOpeningLength(number) : 0)}");
                    FinishBoxes();
                    gm.DialogueTap();
                }
                yield return Snap("떠올리기 3초", 0.7f);
                yield return Until(() => IsOn("DrawPanel"));

                if (s == 0 && c.guide == ChapterGuide.Magnifier)
                {
                    // 2챕터 첫 그리기 : 도깨비가 돋보기 쓰는 법을 보여 줘요 (공짜, 그동안 시간이 멈춰요)
                    yield return Snap("사탕 3개가 들어오고, 도깨비가 돋보기를 눌러 보라고 함", 0.2f);
                    FinishBoxes();
                    gm.PeekGuideTap();
                    yield return Snap("대사를 넘기면 : 대사가 사라지고 도깨비불이 돋보기 둘레를 돎", 0.6f);
                    gm.MagnifierTap();
                    yield return Wait(0.3f);
                    FinishBoxes();
                    yield return Snap("돋보기를 누르면 : 5초 동안, 렌즈를 끌어 보는 법을 알려 줌", 0.4f);
                    gm.PeekGuideTap();
                    yield return Snap("대사를 넘기면 : 렌즈로 둘러봄 (둘레 띠가 가득)", 0.3f);
                    yield return Snap("남은 시간만큼 띠가 줄어듦", 1.9f);
                    yield return Until(() => IsOn("DrawPanel/PeekGuide"));
                    yield return Wait(0.3f);
                    FinishBoxes();
                    yield return Snap("돋보기 시범 끝 : 이번 챕터에서 한 번만 더 빌려준다고 알려 줌", 0.4f);
                    gm.PeekGuideTap();
                    gm.PeekGuideTap();
                    yield return Snap("그리기 시작 : 빈 쪽지 (검정 펜, 돋보기 1번)", 0.8f);
                    gm.MagnifierTap();
                    MvpSelfTest.Find("DrawPanel/MagnifierPeek/Lens").position += new Vector3(-120f, 160f, 0f);
                    yield return Snap("돋보기 : 렌즈를 끌어 원본을 둘러봄 (챕터 1번을 씀)", 0.5f);
                    MvpSelfTest.Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>().Stop();
                }
                else if (s == 0 && c.guide == ChapterGuide.Mix)
                {
                    // 3챕터 첫 그리기 : 색 안내 (붓 → 팔레트)
                    yield return Snap("도깨비가 붓을 눌러 보라고 함", 0.4f);
                    FinishBoxes();
                    gm.PeekGuideTap();
                    yield return Snap("대사를 넘기면 : 도깨비불이 붓 둘레를 돎", 0.6f);
                    toolBar.BrushTap();
                    yield return Wait(0.4f);
                    FinishBoxes();
                    yield return Snap("붓을 누르면 : 팔레트가 열리고, 하나만 써도 섞어도 된다고 알려 줌 (대사는 위쪽)", 0.4f);
                    gm.PeekGuideTap();
                    toolBar.PaintTap(0);
                    yield return Snap("색 섞기 : 물감이 붓 끝으로 날아가는 중", 0.16f);
                    var mixer = MvpSelfTest.Find("DrawPanel/ToolBar").GetComponent<ColorMixer>();
                    toolBar.PaintTap(0); toolBar.PaintTap(1); toolBar.PaintTap(1); toolBar.PaintTap(2);
                    mixer.SkipAnimation();
                    yield return Snap("색 섞기 팔레트 : 넣은 횟수와 섞인 붓 끝 색");
                    toolBar.ClosePanel();
                }
                else if (s == 0)
                {
                    yield return Snap("그리기 시작 : 안내 없음" + (c.candies == 0 ? " (사탕 0개 : 바구니가 비었어요)" : ""), 1.2f);
                }

                MvpSelfTest.LoadSketch(Scribble());
                if (s == 0 && c.candies == 0)
                {
                    // 4챕터 : 사탕 없이 쪽지만 건네고, 한 번만 그려요. 틀리면 바로 채점표
                    MvpSelfTest.Expect(st, -1);
                    gm.Submit();
                    string[] names = { "건네기 : 사탕 없이 쪽지만", "건네기 : 도깨비가 바로 쪽지를 읽음" };
                    foreach (string n in names)
                    {
                        yield return Wait(0.5f);
                        FinishBoxes();
                        yield return Snap(n);
                        FinishBoxes();
                        gm.DialogueTap();
                    }
                    yield return Until(ResultShown);
                    yield return ResultLines("틀림 (다시 그릴 수 없음)");
                    yield return Snap("사탕이 없어서 다시 · 포기 없이 바로 채점표", 2.8f);
                }
                else if (s == 0)
                {
                    yield return SubmitAndWait(st, -1, "판정 대기 : 도깨비가 찾으러 다님");
                    yield return ResultLines("틀림");
                    yield return Snap("다시 · 포기 버튼", 0.5f);
                    gm.Retry();
                    if (c.guide == ChapterGuide.Magnifier)
                    {
                        yield return SubmitAndWait(st, -1, null);
                        yield return ResultLines("두 번째 틀림");
                        yield return Wait(0.5f);
                        FinishBoxes();
                        yield return Snap("두 번째로 틀리면 : 도깨비가 손에 쥔 돋보기를 빌려줌", 0.3f);
                        yield return Snap("도착하면 숫자가 늘고 반짝임", 1.9f);
                        FinishBoxes();
                        gm.ResultTap();
                        gm.Retry();
                    }
                    MvpSelfTest.LoadSketch(Scribble());
                    yield return SubmitAndWait(st, -2, null);
                    yield return ResultLines("맞음");
                    yield return Snap("채점표 : 전부 동그라미", 2.8f);
                }
                else
                {
                    yield return SubmitAndWait(st, -2, null);
                    yield return ResultLines("맞음");
                    yield return Snap("채점표", 2.8f);
                }
                gm.NextStage();
            }

            section = $"{number}챕터 끝 장면";
            yield return Snap("찾은 물건 3개가 방에 놓임", 3.0f);
            GameScript.Line[] endLines = GameScript.RoomEndLines(number);
            for (int i = 0; i < endLines.Length; i++)
            {
                gm.EndingTap();
                yield return Wait(0.4f);
                FinishBoxes();
                yield return Snap(endLines.Length > 1 && i == endLines.Length - 1
                    ? "주인공 속마음 : 도깨비가 떠나고 창 안에 빨간 눈 (한 번 더 누르면 로비로 가서 여기서 멈춰요)"
                    : "화면을 누르면 도깨비 인사" + (endLines.Length == 1 ? " (한 번 더 누르면 로비로 가서 여기서 멈춰요)" : ""), 1.2f);
                FinishBoxes();
            }
        }

        // 5챕터 : C-56 흐름과 연출. 실제 AI 대신 판정 결과를 미리 정해 넣어요.
        void ChooseCurse(bool counter)
        {
            bool left = (bool)typeof(GameManager).GetField("leftIsCounter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gm);
            gm.ChooseItem(left == counter ? 0 : 1);
        }

        void PassCurseIntro()
        {
            for (int k = 0; k < 80 && !IsOn("ChoosePanel"); k++) { FinishBoxes(); gm.DialogueTap(); }
        }

        IEnumerator PlayChapter5()
        {
            section = "5챕터";
            ChapterSelect.Current = 5;
            gm.StartGame();
            ChapterData c5 = MvpSelfTest.ChapterOf(5);
            yield return Wait(0.5f);
            FinishBoxes();
            yield return Snap("5챕터 시작 대화 : 친구와 도깨비");
            PassCurseIntro();
            yield return Wait(0.4f);
            yield return Snap("고르기 : 그림 두 장 (좌우는 섞여요)", 0.3f);
            ChooseCurse(true);
            yield return Until(() => IsOn("DrawPanel"));
            yield return Snap("맞설 물건 그리기 : 방해 연출과 하트 3개", 0.8f);
            MvpSelfTest.LoadSketch(Scribble());
            MvpSelfTest.Expect(c5.counterItems[0], -2);
            gm.Submit();
            FinishBoxes();
            yield return Snap("쪽지를 들이밀기 : 사탕 없이 맞섬", 0.4f);
            FinishBoxes();
            gm.DialogueTap();
            yield return Until(() => IsOn("JudgePanel"));
            yield return Snap("판정 대기 : 빛나는 쪽지와 흑화 도깨비", 0.4f);
            yield return Until(ResultShown);
            yield return ResultLines("맞설 물건 성공 : 하트 3개 유지");

            // 다음 단계에서 요청 물건을 고르면 그리지 않고 빼앗겨요. 하트 2개.
            PassCurseIntro();
            ChooseCurse(false);
            yield return Until(ResultShown);
            yield return ResultLines("요청 물건 빼앗김 : 그리기 없이 하트 2개");
            yield return Snap("같은 단계 고르기로 돌아옴", 0.3f);

            // 맞설 물건 실패 → 점프스케어 + 하트 1개 → 같은 단계 고르기.
            ChooseCurse(true);
            yield return Until(() => IsOn("DrawPanel"));
            MvpSelfTest.LoadSketch(Scribble());
            yield return SubmitAndWait(c5.counterItems[1], -1, null);
            yield return Snap("맞설 물건 실패 : 점프스케어와 하트 1개", 0.05f);
            yield return ResultLines("맞설 물건 실패 : 하트 1개");

            // 요청 물건을 다시 골라 하트 0. 친구가 쓰러지고 영혼을 빼앗겨요.
            ChooseCurse(false);
            yield return Until(ResultShown);
            FinishBoxes();
            gm.ResultTap();
            FinishBoxes();
            yield return Snap("하트 0 : 친구가 쓰러지고 영혼이 빠져나감", 1.3f);
            yield return Wait(1.4f);
            FinishBoxes();
            gm.ResultTap();
            FinishBoxes();
            yield return Snap("게임오버 : 친구 영혼을 빼앗긴 뒤", 0.5f);

            // 해피 엔딩 화면. 배드 엔딩은 C-56에서 없앴어요.
            MvpSelfTest.Invoke(gm, "ShowCurseEnd", 2);
            FinishBoxes();
            yield return Snap("해피 엔딩 : 세 단계를 다 이긴 뒤", 0.5f);
            ChapterSelect.Current = 1;
        }
    }
}

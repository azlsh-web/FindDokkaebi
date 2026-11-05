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
            if (judgeCaption != null) yield return Snap(judgeCaption, 1.5f);
            yield return Until(ResultShown);
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

            section = "시작";
            yield return Snap("시작 화면");
            gm.StartGame();

            for (int s = 0; s < stages.Length; s++) yield return PlayStage(s);

            yield return PlayEndings();
            Finish();
        }

        IEnumerator PlayStage(int s)
        {
            StageData st = stages[s];
            section = $"{st.stageNumber}단계 {st.targetName}";

            // 대화
            GameScript.Line[] lines = GameScript.IntroLines(s);
            for (int i = 0; i < lines.Length; i++)
            {
                yield return Wait(0.5f);
                FinishBoxes();
                yield return Snap($"시작 대화 {i + 1}");
                FinishBoxes();
                gm.DialogueTap();
            }

            // 쪽지 → 관찰
            yield return Wait(0.4f);
            FinishBoxes();
            yield return Snap("도깨비의 쪽지 : 판정 기준");
            FinishBoxes();
            gm.DialogueTap();
            yield return Snap("관찰 3초", 0.7f);

            // 그리기
            yield return Until(() => IsOn("DrawPanel"));
            if (s == 0) yield return Snap("간식이 왼쪽에서 들어오는 중", 0.15f);
            yield return Snap("그리기 시작 : 빈 쪽지", 1.2f);

            if (s == 0)
            {
                gm.TogglePause();
                yield return Snap("일시정지", 0.4f);
                gm.TogglePause();
                MvpSelfTest.SetPrivate(gm, "timeLeft", 8f);
                yield return Snap("향이 10초 아래로 남았을 때", 0.8f);
                MvpSelfTest.SetPrivate(gm, "timeLeft", 60f);
            }

            if (st.colorMode == ColorMode.MixTwo || st.colorMode == ColorMode.MixFree)
            {
                var toolBar = MvpSelfTest.Find("DrawPanel/ToolBar").GetComponent<ToolBar>();
                var mixer = MvpSelfTest.Find("DrawPanel/MixerPanel").GetComponent<ColorMixer>();
                toolBar.OpenMixer();
                if (st.colorMode == ColorMode.MixTwo) { mixer.TogglePick(1); mixer.TogglePick(2); }
                else { mixer.TogglePick(0); mixer.TogglePick(1); mixer.TogglePick(2); }
                yield return Snap("색 섞기 팔레트");
                mixer.Apply();
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
                MvpSelfTest.LoadSketch(PartialSketch(s));
                yield return SubmitAndWait(st, st.features.Length - 1, null);
                yield return ResultLines("일부 틀림");
                yield return Snap("다시 · 포기 버튼 (간식 1개 남음)", 0.5f);
                gm.ShowCompare();
                yield return Snap("포기 : 채점표 (동그라미와 빗금)", 2.8f);
                gm.Retry();     // 사진을 위해 같은 단계를 이어 가요

                if (s == 1)
                {
                    // ③ 간식을 다 썼을 때
                    yield return Wait(0.5f);
                    MvpSelfTest.LoadSketch(Scribble());
                    yield return SubmitAndWait(st, -1, null);
                    yield return ResultLines("간식을 다 씀");
                    yield return Snap("간식이 없으면 버튼 없이 채점표", 2.8f);
                    MvpSelfTest.SetPrivate(gm, "snacks", 1);
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
                // 1단계를 못 찾은 경우 : 채점표 → 대파 → 뛰어나감 → 방망이를 들고 돌아옴
                section = "1단계를 못 찾았을 때";
                ((bool[])typeof(GameManager).GetField("cleared", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gm))[0] = false;
                MvpSelfTest.SetPrivate(gm, "lastPresent", new[] { false });
                gm.ShowCompare();
                yield return Snap("채점표 : 빗금", 2.8f);
                gm.NextStage();
                yield return Wait(0.5f);
                FinishBoxes();
                yield return Snap("대파를 든 도깨비");
                FinishBoxes();
                gm.OopsTap();
                yield return Wait(0.2f);
                FinishBoxes();
                yield return Snap("도깨비가 뛰어나감", 0.3f);
                yield return Until(() => IsOn("OopsPanel/Back"));
                yield return Wait(0.3f);
                FinishBoxes();
                yield return Snap("방망이를 들고 돌아옴", 0.8f);
                FinishBoxes();
                gm.OopsTap();
            }
            else if (s < stages.Length - 1) gm.NextStage();
        }

        IEnumerator PlayEndings()
        {
            section = "이야기 종료";
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var snacksLeft = (int[])typeof(GameManager).GetField("snacksLeft", flags).GetValue(gm);
            var items = (Sprite[])typeof(GameManager).GetField("itemsFound", flags).GetValue(gm);

            // 전부 찾았을 때
            int[] left = { 2, 1, 2, 1, 2 };
            for (int i = 0; i < stages.Length; i++) { snacksLeft[i] = left[i]; items[i] = stages[i].originalSprite; }
            gm.NextStage();
            yield return Snap("전부 찾았을 때 : 소풍 장면, 물건 옆에 남은 간식", 3.5f);
            gm.EndingTap();
            yield return Wait(0.3f);
            FinishBoxes();
            yield return Snap("화면을 누르면 도깨비가 물어봄", 0.4f);
            FinishBoxes();
            gm.EndingTap();

            // 포기한 단계가 있을 때 : 받은 엉뚱한 물건이 그대로 놓여요
            yield return Wait(0.5f);
            int[] left2 = { 0, 0, 1, 0, 0 };
            for (int i = 0; i < stages.Length; i++)
            {
                snacksLeft[i] = left2[i];
                Sprite[] wrong = stages[i].wrongSprites;
                items[i] = i == 0 ? stages[0].originalSprite : i == 2 ? stages[2].originalSprite : wrong[wrong.Length - 1];
            }
            MvpSelfTest.Invoke(gm, "ShowEnding");
            yield return Snap("포기한 단계가 있을 때 : 엉뚱한 물건이 그대로 놓임", 3.5f);
        }
    }
}

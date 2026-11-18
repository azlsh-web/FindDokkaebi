using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// ─────────────────────────────────────────────────────────────
// MvpJudgeCases : 자동 판정 "전수 검사" (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 단계마다 형태를 그렸는지/빠뜨렸는지 손그림으로 만들어서,
// 실제 게임 화면에서 제출 → 결과 화면을 캡처하고, 기대한 판정과 실제 판정을 비교해요.
//   소풍 다섯 단계에서 통과와 실패 각 1가지, 방망이는 굵은 펜으로도 검사해요.
//
// 실행 : 메뉴 Tools → 찾아라도깨비 → 판정 전수 검사  (또는 Temp/mvp_judgecases.flag 파일)
// 결과 : Temp/JudgeCases/  (결과 화면 캡처 PNG, 손그림 PNG, results.csv)
[InitializeOnLoad]
static class MvpJudgeCases
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_judgecases.flag";
    const string OutDir = "Temp/JudgeCases";
    const string RunningKey = "MvpJudgeCases.Running";

    class Case
    {
        public int stage;           // 0~4
        public bool[] drawn;        // 특징마다 그렸는지
        public bool[] expected;     // 기대 판정
        public bool[] judged;       // 실제 판정
        public string[] notes;
        public string shot, drawing;
        public int pen = 4;         // 펜 반지름 (게임의 굵은 펜 = 12)
        public string returned;     // 결과 화면에 실제로 나온 물건 그림 이름
    }

    static List<Case> cases;
    static int index, phase, currentStage = -1;
    static double waitUntil, deadline;
    static double nextFlagCheck;

    static MvpJudgeCases()
    {
        EditorApplication.update += PollFlag;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/찾아라도깨비/판정 전수 검사")]
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
        if (!File.Exists(Flag) || EditorApplication.isCompiling) return;
        if (EditorApplication.isPlaying)
        {
            if (cases == null) EditorApplication.ExitPlaymode();   // 누가 플레이 중이면 먼저 멈춤
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Flag);
        Run();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
        SessionState.SetBool(RunningKey, false);
        OpenAIJudge.DisableAI = true;        // AI 대신 LocalJudge (요금 X)
        LocalJudge.TestOverride = null;
        cases = BuildCases(Stages(Object.FindFirstObjectByType<GameManager>()).Length);
        index = 0; phase = 0; currentStage = -1;
        waitUntil = EditorApplication.timeSinceStartup + 1.0;
        EditorApplication.update += Tick;
    }

    // ───────────── 한 경우씩 : 그림 넣기 → 제출 → 결과 화면 캡처 ─────────────

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish(); return; }
        try { Step(); }
        catch (Exception e)
        {
            Debug.LogError("[JudgeCases] 검사 중 오류로 멈춤: " + e);   // 매 프레임 같은 오류가 쌓이지 않게 바로 끝냄
            Finish();
        }
    }

    static void Step()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < waitUntil) return;
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        Case c = cases[index];
        StageData stage = Stages(gm)[c.stage];

        if (phase == 0)
        {
            if (currentStage < 0)
            {
                gm.StartGame();
                SetField(gm, "peekShown", true);
                SetField(gm, "mixShown", true);
            }
            if (c.stage != currentStage)
            {
                Call(gm, "StartStage", c.stage);
                currentStage = c.stage;
            }
            SetField(gm, "thinkSeconds", 0.15f);   // 검사에서는 도깨비 고민을 짧게
            SetField(gm, "roamLoopSeconds", 0.05f);
            SetField(gm, "candies", 10);            // 사탕이 떨어지지 않게
            Call(gm, "StartDraw", false);

            Texture2D tex = Draw(c.stage, c.drawn[0], c.pen);
            Find("DrawPanel/Desk/Paper/Board").GetComponent<DrawingBoard>().LoadPixels(tex.GetPixels32());
            OpenAIJudge.Verdict v = LocalJudge.Judge(stage, tex);   // 게임과 같은 판정 함수 (리포트 기록용)
            c.judged = v.present;
            c.notes = v.notes;
            string bits = Bits(c.drawn);
            c.drawing = $"{index:000}_s{c.stage + 1}_{bits}_draw.png";
            File.WriteAllBytes(Path.Combine(OutDir, c.drawing), tex.EncodeToPNG());
            gm.Submit();
            phase = 1;
            deadline = now + 20.0;                // 결과가 안 뜨면 20초 뒤 그냥 넘어감
            return;
        }

        if (phase == 1)
        {
            bool shown = Find("ResultPanel").gameObject.activeInHierarchy &&
                         Find("ResultPanel/ReturnedItem").localScale.x > 0.99f;
            if (!shown && now < deadline) return;
            Sprite item = Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite;
            c.returned = item != null ? item.name : "";
            c.shot = $"{index:000}_s{c.stage + 1}_{Bits(c.drawn)}_result.png";
            new GameObject("CaseShot").AddComponent<ShotHelper>().path = Path.Combine(OutDir, c.shot);
            phase = 2;
            waitUntil = now + 0.3;
            return;
        }

        // phase 2 : 다음 경우로
        index++;
        phase = 0;
        if (index >= cases.Count) { Finish(); return; }
        waitUntil = now + 0.1;
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        OpenAIJudge.DisableAI = false;
        if (cases != null) WriteCsv();
        int pass = 0, total = 0;
        if (cases != null) foreach (Case c in cases) { if (c.judged == null) continue; total++; if (Same(c.expected, c.judged)) pass++; }
        Debug.Log($"[JudgeCases] {pass}/{total} 경우 기대와 같음 → {OutDir}/results.csv");
        cases = null;
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
    }

    static void WriteCsv()
    {
        var sb = new StringBuilder("index,stage,drawn,expected,judged,pass,shot,drawing,notes,pen,returned\n");
        for (int i = 0; i < cases.Count; i++)
        {
            Case c = cases[i];
            if (c.judged == null) continue;
            string notes = string.Join(" | ", c.notes ?? new string[0]).Replace("\"", "'");
            sb.Append($"{i},{c.stage + 1},{Bits(c.drawn)},{Bits(c.expected)},{Bits(c.judged)},{(Same(c.expected, c.judged) ? "PASS" : "FAIL")},{c.shot},{c.drawing},\"{notes}\",{c.pen},{c.returned}\n");
        }
        File.WriteAllText(Path.Combine(OutDir, "results.csv"), sb.ToString(), new UTF8Encoding(true));
    }

    // ───────────── 경우의 수 만들기 ─────────────

    static List<Case> BuildCases(int stageCount)
    {
        var list = new List<Case>();
        for (int s = 0; s < stageCount; s++)
        {
            foreach (bool shape in new[] { true, false })
            {
                var drawn = new[] { shape };
                list.Add(new Case { stage = s, drawn = drawn, expected = drawn });
                if (s == 0) list.Add(new Case { stage = s, drawn = drawn, expected = drawn, pen = 12 });
            }
        }
        return list;
    }

    static Texture2D Draw(int stage, bool shape, int pen)
    {
        if (stage != 0) return MvpSelfTest.ShapeSketch(stage, shape);

        var k = HandSketch.Black;
        var s = new HandSketch();
        Vector2 c = new Vector2(256, 335);
        s.Line(k, pen, 2f, new Vector2(250, 40), new Vector2(253, 140), new Vector2(256, c.y - 84));
        s.Ellipse(k, pen, c, 58, 88, gapDeg: 14f);
        if (shape)
        {
            foreach (float angle in new[] { 25f, 70f, 115f, 160f })
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 edge = c + new Vector2(dir.x * 58, dir.y * 88);
                s.Line(k, pen, 1f, edge, edge + dir * 55);
            }
        }
        return s.Tex();
    }

    // ───────────── 도우미 ─────────────

    static StageData[] Stages(GameManager gm)
    {
        var so = new SerializedObject(gm);
        SerializedProperty p = so.FindProperty("stages");
        var arr = new StageData[p.arraySize];
        for (int i = 0; i < arr.Length; i++) arr[i] = (StageData)p.GetArrayElementAtIndex(i).objectReferenceValue;
        return arr;
    }

    static Transform Find(string path)
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        return canvas.Find("Frame/" + path) ?? canvas.Find(path);      // 게임 화면은 Canvas/Frame 안에
    }
    static string Bits(bool[] b) { var sb = new StringBuilder(); foreach (bool x in b) sb.Append(x ? 'O' : 'X'); return sb.ToString(); }
    static bool Same(bool[] a, bool[] b) { for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }

    static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

    static void SetField(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    // 게임 프레임이 끝나는 순간에 Game 화면 캡처
    class ShotHelper : MonoBehaviour
    {
        public string path;
        System.Collections.IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex != null) { File.WriteAllBytes(path, tex.EncodeToPNG()); Destroy(tex); }
            Destroy(gameObject);
        }
    }
}

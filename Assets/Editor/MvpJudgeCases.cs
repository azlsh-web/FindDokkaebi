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
// 단계마다 특징을 그렸는지/빠뜨렸는지의 모든 경우의 수를 손그림으로 만들어서,
// 실제 게임 화면에서 제출 → 결과 화면을 캡처하고, 기대한 판정과 실제 판정을 비교해요.
//   1 방망이 2³=8 · 2 가방 2⁴=16 · 3 돗자리 2⁵=32 · 4 김밥 2⁵=32 · 5 친구 2⁴=16  → 모두 104가지
//
// 실행 : 메뉴 Tools → 찾아라도깨비 → 판정 전수 검사  (또는 Temp/mvp_judgecases.flag 파일)
// 결과 : Temp/JudgeCases/  (결과 화면 캡처 PNG, 손그림 PNG, results.csv)
//        → Docs/판정_전수검사_리포트.pdf 는 이 결과로 만들어요.
//
// 기대 판정 규칙 (기획서 부록 B 기준)
//   - 방망이 F2 "손잡이보다 굵은 머리" 는 손잡이가 없으면 인정 안 함 → 기대 = F1 그림 && F2 그림
//   - 가방 F4 "주머니 안 가로 줄무늬" 는 주머니가 없으면 인정 안 함 → 기대 = F3 그림 && F4 그림
//   - 나머지는 그린 그대로가 기대 판정
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
        cases = BuildCases();
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
            if (currentStage < 0) gm.StartGame();     // 기록용 배열 준비 (게임 시작 버튼과 같음)
            if (c.stage != currentStage)
            {
                Call(gm, "StartStage", c.stage);
                currentStage = c.stage;
            }
            SetField(gm, "thinkSeconds", 0.15f);   // 검사에서는 도깨비 고민을 짧게
            SetField(gm, "snacks", 3);             // 간식이 떨어지지 않게
            Call(gm, "StartDraw", false);

            Texture2D tex = Draw(c.stage, c.drawn, c.pen);
            Find("DrawPanel/Board").GetComponent<DrawingBoard>().LoadPixels(tex.GetPixels32());
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
        var sb = new StringBuilder("index,stage,drawn,expected,judged,pass,shot,drawing,notes,pen\n");
        for (int i = 0; i < cases.Count; i++)
        {
            Case c = cases[i];
            if (c.judged == null) continue;
            string notes = string.Join(" | ", c.notes ?? new string[0]).Replace("\"", "'");
            sb.Append($"{i},{c.stage + 1},{Bits(c.drawn)},{Bits(c.expected)},{Bits(c.judged)},{(Same(c.expected, c.judged) ? "PASS" : "FAIL")},{c.shot},{c.drawing},\"{notes}\",{c.pen}\n");
        }
        File.WriteAllText(Path.Combine(OutDir, "results.csv"), sb.ToString(), new UTF8Encoding(true));
    }

    // ───────────── 경우의 수 만들기 ─────────────

    static List<Case> BuildCases()
    {
        int[] featureCount = { 3, 4, 5, 5, 4 };
        var list = new List<Case>();
        for (int s = 0; s < featureCount.Length; s++)
        {
            int n = featureCount[s];
            for (int mask = (1 << n) - 1; mask >= 0; mask--)   // 전부 그림 → … → 전부 빠뜨림
            {
                var drawn = new bool[n];
                for (int i = 0; i < n; i++) drawn[i] = (mask & (1 << (n - 1 - i))) != 0;
                var exp = (bool[])drawn.Clone();
                if (s == 0) exp[1] = drawn[0] && drawn[1];      // 손잡이 없으면 "손잡이보다 굵은 머리" 불인정
                if (s == 1) exp[3] = drawn[2] && drawn[3];      // 주머니 없으면 "주머니 안 줄무늬" 불인정
                if (s == 2 && !drawn[1]) { exp[2] = false; exp[3] = false; }   // 띠가 없으면(한 가지 색) 두·세 번째 띠 색도 없음
                list.Add(new Case { stage = s, drawn = drawn, expected = exp });
                if (s == 0) list.Add(new Case { stage = s, drawn = drawn, expected = exp, pen = 12 });   // 방망이는 굵은 펜으로도
            }
        }
        return list;
    }

    static Color32 Pal(int i) => ToolBar.Palette[i];           // 0 검정 1 빨강 2 노랑 3 파랑
    static readonly Color32 Green = new Color32(40, 160, 60, 255), Brown = new Color32(130, 80, 40, 255);
    static readonly Color32 Purple = new Color32(130, 50, 160, 255), Orange = new Color32(245, 140, 30, 255);
    static Vector2 V(float x, float y) => new Vector2(x, y);

    // 단계와 특징 on/off 에 맞는 손그림
    static Texture2D Draw(int stage, bool[] f, int pen = 4)
    {
        var k = HandSketch.Black;
        var s = new HandSketch();
        switch (stage)
        {
            case 0: // 방망이 : F1 손잡이, F2 굵은 머리, F3 돌기
            {
                // 머리 : 굵음 = 타원 / 안 굵음 = 손잡이와 같은 굵기의 막대 (손잡이가 없으면 작은 동그라미)
                Vector2 c = f[0] ? V(256, 335) : V(256, 260);
                float rx = f[1] ? 58 : (f[0] ? 3 : 32), ry = f[1] ? 88 : (f[0] ? 88 : 32);
                if (f[0]) s.Line(k, pen, 2f, V(250, 40), V(253, 140), V(256, c.y - ry + 4));
                s.Ellipse(k, pen, c, rx, ry, gapDeg: 14f);
                if (f[2])
                {
                    foreach (float a in new[] { 25f, 70f, 115f, 160f })
                    {
                        Vector2 dir = V(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                        Vector2 edge = c + new Vector2(dir.x * rx, dir.y * ry);
                        s.Line(k, pen, 1f, edge, edge + dir * 55);
                    }
                }
                break;
            }
            case 1: // 가방 : F1 네모 몸체, F2 손잡이, F3 주머니, F4 주머니 안 줄무늬
            {
                if (f[0]) s.Line(k, 4, 2f, V(100, 60), V(412, 64), V(408, 330), V(104, 326), V(100, 60));
                else s.Ellipse(k, 4, V(256, 195), 160, 138, gapDeg: 6f);
                if (f[1]) s.Line(k, 4, 2f, V(190, 328), V(200, 400), V(256, 440), V(312, 400), V(322, 328));
                if (f[2]) s.Line(k, 4, 2f, V(172, 92), V(340, 94), V(338, 210), V(170, 206), V(172, 92));
                if (f[3])
                {
                    s.Line(k, 3, 1.5f, V(192, 122), V(320, 124));
                    s.Line(k, 3, 1.5f, V(190, 150), V(318, 152));
                    s.Line(k, 3, 1.5f, V(192, 180), V(320, 178));
                }
                break;
            }
            case 2: // 돗자리 : F1 네모, F2 띠 네 줄, F3 두 번째 빨강, F4 세 번째 노랑, F5 양 끝 파랑
            {
                Color32 end = f[4] ? Pal(3) : Orange, second = f[2] ? Pal(1) : Green, third = f[3] ? Pal(2) : Purple;
                float y0 = f[0] ? 170 : 215, y1 = f[0] ? 370 : 325;              // 둥근 돗자리는 위아래로 좁게 칠함
                if (f[1])
                {
                    s.Scribble(end, 6, 85, y0, 168, y1, 16);
                    s.Scribble(second, 6, 180, y0, 255, y1, 16);
                    s.Scribble(third, 6, 265, y0, 340, y1, 16);
                    s.Scribble(end, 6, 352, y0, 430, y1, 16);
                }
                else s.Scribble(end, 6, 85, y0, 430, y1, 16);                    // 띠 없이 한 가지 색
                if (f[0]) s.Line(k, 3, 3f, V(70, 150), V(440, 145), V(445, 385), V(65, 390), V(70, 150));
                else s.Ellipse(k, 3, V(256, 268), 195, 128, gapDeg: 6f);
                break;
            }
            case 3: // 김밥 : F1 검은 테두리, F2 흰 밥, F3 빨강, F4 노랑, F5 초록
            {
                if (!f[1]) s.ScribbleCircle(Brown, 8, V(256, 256), 192, 9);           // 밥을 갈색으로 칠함
                if (f[2]) s.Scribble(Pal(1), 6, 160, 280, 240, 340, 14);
                if (f[3]) s.Scribble(Pal(2), 6, 280, 280, 360, 340, 14);
                s.Scribble(f[4] ? Green : Pal(3), 6, 210, 170, 310, 230, 14);         // 초록 또는 파랑 재료
                s.Ellipse(f[0] ? k : Orange, 9, V(256, 256), 200, 196, gapDeg: 8f);   // 검정 또는 주황 테두리
                break;
            }
            default: // 친구 : F1 사람, F2 갈색 머리, F3 주황 상의, F4 파란 하의
            {
                Color32 top = f[2] ? Orange : Green, pants = f[3] ? Pal(3) : Purple;
                s.Ellipse(k, 4, V(256, 400), 55, 55);
                s.Scribble(f[1] ? Brown : k, 8, 200, 405, 312, 458, 9);               // 머리카락 (갈색 또는 검정)
                s.Scribble(top, 8, 165, 225, 347, 335, 9);
                s.Scribble(top, 8, 110, 250, 165, 320, 9);
                s.Scribble(top, 8, 347, 250, 402, 320, 9);
                if (f[0])
                {
                    s.Scribble(pants, 8, 175, 70, 235, 215, 9);                       // 두 다리
                    s.Scribble(pants, 8, 280, 70, 340, 215, 9);
                }
                else
                {
                    s.Scribble(pants, 8, 205, 150, 307, 220, 9);                      // 허수아비 : 짧은 하의 + 막대 하나
                    s.Line(k, 7, 0.5f, V(256, 150), V(256, 40));
                }
                break;
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

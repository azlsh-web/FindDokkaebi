using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// MvpPlayRecorder : 게임을 처음부터 끝까지 플레이하는 모습을 녹화 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → 플레이 영상 녹화] (또는 Temp/mvp_record.flag)
//   → Play 모드에서 버튼을 누르고 펜으로 그리는 것을 그대로 보여주며 5단계를 끝까지 플레이해요.
//   → Temp/Recording/frame_00001.jpg … 로 매 프레임 저장 (초당 24장, 게임 시간 기준이라 끊김 없음)
//   → ffmpeg 로 묶어서 mp4 영상으로 만들어요.
// 판정은 진짜예요 : 그린 그림을 LocalJudge 가 실제로 판정해요. (AI 는 부르지 않음)
// 1단계는 일부러 돌기를 빠뜨려서 "엉뚱한 방망이"를 받은 뒤 다시 그려요.
[InitializeOnLoad]
static class MvpPlayRecorder
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_record.flag";
    const string RunningKey = "MvpPlayRecorder.Running";
    public const string OutDir = "Temp/Recording";
    static double nextCheck;

    static MvpPlayRecorder()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        File.Delete(Flag);
        Run();
    }

    [MenuItem("Tools/찾아라도깨비/플레이 영상 녹화")]
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

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningKey, false))
        {
            SessionState.SetBool(RunningKey, false);
            OpenAIJudge.DisableAI = true;
            LocalJudge.TestOverride = null;
            var go = new GameObject("PlayDirector");
            var d = go.AddComponent<PlayDirector>();
            d.cursorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Circle.png");
        }
        if (change == PlayModeStateChange.EnteredEditMode) OpenAIJudge.DisableAI = false;
    }
}

// 실제로 화면을 조작하는 감독 (Play 모드 안에서만 존재)
class PlayDirector : MonoBehaviour
{
    public Sprite cursorSprite;
    const int Fps = 24;

    GameManager gm;
    RectTransform cursor;
    int frame;
    bool recording = true;

    IEnumerator Start()
    {
        Time.captureFramerate = Fps;   // 게임 시간이 한 프레임에 1/24초씩 → 저장이 느려도 영상은 매끄럽게
        gm = FindFirstObjectByType<GameManager>();
        MakeCursor();
        StartCoroutine(Grab());
        yield return Script();
        recording = false;
        yield return null;
        yield return null;
        Time.captureFramerate = 0;
        File.WriteAllText(Path.Combine(MvpPlayRecorder.OutDir, "done.txt"), frame.ToString());
        EditorApplication.ExitPlaymode();
    }

    // 매 프레임 끝에 화면 저장
    IEnumerator Grab()
    {
        while (recording)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) continue;
            frame++;
            File.WriteAllBytes(Path.Combine(MvpPlayRecorder.OutDir, $"frame_{frame:00000}.jpg"), tex.EncodeToJPG(88));
            Destroy(tex);
        }
    }

    // ───────────── 플레이 시나리오 ─────────────

    IEnumerator Script()
    {
        yield return Wait(2.5f);                                          // 시작 화면
        yield return Click("TitlePanel/StartButton");

        // 1단계 방망이 : 처음엔 돌기를 빠뜨림 → 엉뚱한 방망이 → 다시 그리기
        yield return IntroAndObserve(3.5f);
        yield return Tool("ToolRow/Size_1");
        yield return Line(V(250, 60), V(253, 150), V(256, 245));                         // 손잡이
        yield return Ellipse(V(256, 335), 60, 90, 14f);                                  // 머리
        yield return Click("DrawPanel/SubmitButton");
        yield return WaitResult(3.5f);
        yield return Click("ResultPanel/RetryButton");
        yield return Wait(0.5f);
        yield return Line(V(250, 60), V(253, 150), V(256, 245));
        yield return Ellipse(V(256, 335), 60, 90, 14f);
        foreach (float a in new[] { 25f, 70f, 115f, 160f })                              // 이번엔 돌기까지
        {
            Vector2 dir = V(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            Vector2 edge = V(256, 335) + new Vector2(dir.x * 60, dir.y * 90);
            yield return Line(edge, edge + dir * 55);
        }
        yield return SubmitAndCompare();

        // 2단계 가방
        yield return IntroAndObserve(3f);
        yield return Tool("ToolRow/Size_1");
        yield return Line(V(100, 60), V(412, 64), V(408, 330), V(104, 326), V(100, 60));
        yield return Line(V(190, 328), V(200, 400), V(256, 440), V(312, 400), V(322, 328));
        yield return Line(V(172, 92), V(340, 94), V(338, 210), V(170, 206), V(172, 92));
        yield return Tool("ToolRow/Size_0");
        yield return Line(V(194, 122), V(318, 124));
        yield return Line(V(192, 150), V(316, 152));
        yield return Line(V(194, 180), V(318, 178));
        yield return SubmitAndCompare();

        // 3단계 돗자리 : 빨강·노랑·파랑
        yield return IntroAndObserve(3f);
        yield return Tool("ToolRow/Size_1");
        yield return Line(V(70, 150), V(440, 145), V(445, 385), V(65, 390), V(70, 150));
        yield return Line(V(255, 150), V(262, 270), V(258, 388));
        yield return Line(V(68, 268), V(250, 262), V(442, 266));
        yield return Tool("ToolRow/Size_2");
        yield return Tool("ColorRow/Color_빨강");
        yield return Scribble(95, 285, 235, 372, 24);
        yield return Tool("ColorRow/Color_노랑");
        yield return Scribble(280, 285, 425, 372, 24);
        yield return Tool("ColorRow/Color_파랑");
        yield return Scribble(95, 162, 235, 250, 24);
        yield return Scribble(280, 162, 425, 250, 24);
        yield return SubmitAndCompare();

        // 4단계 김밥 : 노랑 + 파랑 = 초록
        yield return IntroAndObserve(3f);
        yield return Tool("ToolRow/Size_2");
        yield return Ellipse(V(256, 256), 200, 196, 6f);
        yield return Tool("ColorRow/Color_빨강");
        yield return Scribble(165, 282, 240, 340, 16);
        yield return Tool("ColorRow/Color_노랑");
        yield return Scribble(280, 282, 355, 340, 16);
        yield return Mix("Material_노랑", "Material_파랑");
        yield return Scribble(212, 172, 305, 228, 16);
        yield return SubmitAndCompare();

        // 5단계 친구 : 초록 상의(두 색) + 갈색 하의(세 색)
        yield return IntroAndObserve(3f);
        yield return Tool("ToolRow/Size_1");
        yield return Ellipse(V(256, 400), 55, 55, 0f);
        yield return Mix("Material_노랑", "Material_파랑");
        yield return Tool("ToolRow/Size_2");
        yield return Scribble(165, 228, 347, 334, 14);
        yield return Scribble(112, 252, 162, 320, 14);
        yield return Scribble(350, 252, 400, 320, 14);
        yield return Tool("ColorRow/Color_검정");
        yield return Tool("ToolRow/Size_1");
        yield return Line(V(165, 258), V(347, 260));
        yield return Line(V(165, 285), V(347, 287));
        yield return Line(V(165, 312), V(347, 310));
        yield return Mix("Material_빨강", "Material_노랑", "Material_파랑");
        yield return Tool("ToolRow/Size_2");
        yield return Scribble(178, 72, 236, 214, 14);
        yield return Scribble(282, 72, 340, 214, 14);
        yield return SubmitAndCompare();

        yield return Wait(4f);                                            // 이야기 종료 화면
    }

    // 도입 화면 보기 → 기억할 모습 보기 → 3초 관찰 → 그리기 화면
    IEnumerator IntroAndObserve(float readSeconds)
    {
        yield return Wait(readSeconds);
        yield return Click("StageIntroPanel/ObserveButton");
        while (!On("DrawPanel")) yield return null;
        yield return Wait(0.5f);
    }

    IEnumerator SubmitAndCompare()
    {
        yield return Wait(0.4f);
        yield return Click("DrawPanel/SubmitButton");
        yield return WaitResult(3f);
        yield return Click("ResultPanel/CompareButton");
        yield return Wait(2.5f);
        yield return Click("ComparePanel/NextButton");
    }

    IEnumerator WaitResult(float hold)
    {
        while (!On("ResultPanel")) yield return null;
        yield return Wait(hold);
    }

    // 혼색 : 팔레트 열기 → 재료 고르기 → 섞어서 칠하기
    IEnumerator Mix(params string[] materials)
    {
        yield return Tool("ColorRow/MixOpen");
        yield return Wait(0.4f);
        foreach (string m in materials) { yield return Click("DrawPanel/MixerPanel/MaterialRow/" + m + "/Button"); yield return Wait(0.35f); }
        yield return Wait(0.6f);
        yield return Click("DrawPanel/MixerPanel/ApplyButton");
        yield return Wait(0.3f);
    }

    IEnumerator Tool(string path) => Click("DrawPanel/ToolBar/" + path + "/Button");

    // ───────────── 커서 · 클릭 · 그리기 ─────────────

    void MakeCursor()
    {
        var canvas = GameObject.Find("Canvas").transform;
        var go = new GameObject("PenCursor", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas, false);
        var img = go.GetComponent<Image>();
        img.sprite = cursorSprite;
        img.color = new Color(0.29f, 0.17f, 0.09f, 0.55f);
        img.raycastTarget = false;
        cursor = go.GetComponent<RectTransform>();
        cursor.sizeDelta = new Vector2(46, 46);
        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(go.transform, false);
        var di = dot.GetComponent<Image>();
        di.sprite = cursorSprite; di.color = Color.white; di.raycastTarget = false;
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);
        cursor.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.3f, 0);
    }

    void Update() { if (cursor != null) cursor.SetAsLastSibling(); }

    IEnumerator MoveCursor(Vector3 to, float seconds)
    {
        Vector3 from = cursor.position;
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0, 1, t / seconds);
            cursor.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        cursor.position = to;
    }

    IEnumerator Click(string path)
    {
        Transform t = Find(path);
        if (t == null) { Debug.LogWarning("[녹화] 버튼 없음: " + path); yield break; }
        var rt = (RectTransform)t;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        yield return MoveCursor((c[0] + c[2]) * 0.5f, 0.45f);
        cursor.localScale = Vector3.one * 0.75f;                       // 꾹 누르는 느낌
        yield return Wait(0.12f);
        t.GetComponent<Button>().onClick.Invoke();
        cursor.localScale = Vector3.one;
        yield return Wait(0.2f);
    }

    // 쪽지 좌표(512x512, 아래가 0) → 화면 좌표
    Vector2 ToScreen(Vector2 p)
    {
        var rt = (RectTransform)Find("DrawPanel/Board");
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return new Vector2(Mathf.Lerp(c[0].x, c[2].x, p.x / 512f), Mathf.Lerp(c[0].y, c[2].y, p.y / 512f));
    }

    // 점들을 따라 실제 드래그 (속도 : 초당 약 700 쪽지 픽셀)
    IEnumerator Line(params Vector2[] pts)
    {
        DrawingBoard board = Find("DrawPanel/Board").GetComponent<DrawingBoard>();
        yield return MoveCursor(ToScreen(pts[0]), 0.3f);
        var e = new PointerEventData(EventSystem.current) { position = ToScreen(pts[0]) };
        board.OnPointerDown(e);
        float perFrame = 700f / Fps;
        for (int i = 1; i < pts.Length; i++)
        {
            float len = Vector2.Distance(pts[i - 1], pts[i]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / perFrame));
            for (int s = 1; s <= steps; s++)
            {
                Vector2 q = Vector2.Lerp(pts[i - 1], pts[i], s / (float)steps);
                q += new Vector2(Mathf.Sin(q.y * 0.07f + i), Mathf.Cos(q.x * 0.06f + i)) * 1.5f;   // 살짝 손떨림
                e.position = ToScreen(q);
                cursor.position = e.position;
                board.OnDrag(e);
                yield return null;
            }
        }
        board.OnPointerUp(e);
    }

    IEnumerator Ellipse(Vector2 center, float rx, float ry, float gapDeg)
    {
        var pts = new List<Vector2>();
        for (float a = gapDeg; a <= 360f; a += 8f) pts.Add(center + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * rx, Mathf.Sin(a * Mathf.Deg2Rad) * ry));
        yield return Line(pts.ToArray());
    }

    IEnumerator Scribble(float x0, float y0, float x1, float y1, float gap)
    {
        var pts = new List<Vector2>();
        bool right = true;
        for (float y = y0; y <= y1; y += gap) { pts.Add(V(right ? x0 : x1, y)); pts.Add(V(right ? x1 : x0, y + gap * 0.5f)); right = !right; }
        yield return Line(pts.ToArray());
    }

    // ───────────── 도우미 ─────────────

    static Vector2 V(float x, float y) => new Vector2(x, y);
    static Transform Find(string path)
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        return canvas.Find("Frame/" + path) ?? canvas.Find(path);      // 게임 화면은 Canvas/Frame 안에
    }
    static bool On(string path) { Transform t = Find(path); return t != null && t.gameObject.activeInHierarchy; }
    static WaitForSeconds Wait(float s) => new WaitForSeconds(s);
}

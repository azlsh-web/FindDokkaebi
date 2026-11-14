using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// MvpPlayRecorder : 게임을 처음부터 끝까지 플레이하는 모습을 녹화 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → 플레이 영상 녹화] (또는 Temp/mvp_record.flag)
//   → Play 모드에서 사람이 하는 속도로 버튼을 누르고 펜으로 그리며 5단계를 끝까지 플레이해요.
//     대사는 글자가 다 나온 뒤 읽을 시간을 두고 넘겨요.
//   → Temp/Recording/frame_00001.jpg … 로 매 프레임 저장 (초당 24장, 게임 시간 기준이라 끊김 없음)
//   → 게임 소리는 Temp/Recording/audio.f32 (AudioRenderer, 녹화하는 동안 스피커로는 안 나와요)
//   → python Tools/sound/make_play_video.py 로 묶어서 소리 있는 mp4 를 만들어요.
// 판정은 진짜예요 : 그린 그림을 LocalJudge 가 실제로 판정해요. (AI 는 부르지 않음)
// 써 보는 기능 : 소리 설정(시작 화면 · 게임 중), 잠긴 챕터, 돋보기 시범, 되돌리기 · 다시 하기,
//   틀린 뒤 다시, 두 번 틀려 받은 무료 돋보기와 렌즈 끌기, 쪽지 키우기 · 돌리기와 맞춤, 포기,
//   색 섞기 시범, 물감 빼기, 이야기 종료 인사
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
            d.cursorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Common_Circle.png");
        }
        if (change == PlayModeStateChange.EnteredEditMode) OpenAIJudge.DisableAI = false;
    }
}

// 실제로 화면을 조작하는 감독 (Play 모드 안에서만 존재)
// 시간은 모두 "녹화 프레임" 으로 세요. 소리 설정 창이 열리면 게임 시간이 멈추기(timeScale 0) 때문이에요.
class PlayDirector : MonoBehaviour
{
    public Sprite cursorSprite;
    const int Fps = 24;
    const float DrawSpeed = 420f;        // 펜 속도 (초당 쪽지 픽셀) : 사람이 천천히 그리는 정도

    GameManager gm;
    RectTransform cursor, cursor2;
    int frame;
    bool recording = true;
    FileStream audioOut;
    int channels;
    float bgmBefore, sfxBefore;

    IEnumerator Start()
    {
        Time.captureFramerate = Fps;   // 게임 시간이 한 프레임에 1/24초씩 → 저장이 느려도 영상은 매끄럽게
        gm = FindFirstObjectByType<GameManager>();
        bgmBefore = Sfx.BgmVolume;
        sfxBefore = Sfx.SfxVolume;
        channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        audioOut = new FileStream(Path.Combine(MvpPlayRecorder.OutDir, "audio.f32"), FileMode.Create);
        File.WriteAllText(Path.Combine(MvpPlayRecorder.OutDir, "audio.txt"), $"{AudioSettings.outputSampleRate} {channels}");
        AudioRenderer.Start();
        cursor = MakeCursor();
        StartCoroutine(Grab());
        yield return Script();
        recording = false;
        yield return null;
        yield return null;
        AudioRenderer.Stop();
        audioOut.Close();
        Sfx.BgmVolume = bgmBefore;      // 사람이 정해 둔 볼륨으로 돌려놔요
        Sfx.SfxVolume = sfxBefore;
        Time.captureFramerate = 0;
        File.WriteAllText(Path.Combine(MvpPlayRecorder.OutDir, "done.txt"), frame.ToString());
        EditorApplication.ExitPlaymode();
    }

    void OnDestroy()
    {
        if (audioOut != null) { AudioRenderer.Stop(); audioOut.Close(); audioOut = null; }
        Sfx.BgmVolume = bgmBefore;
        Sfx.SfxVolume = sfxBefore;
    }

    // 매 프레임 끝에 화면과 그 프레임의 소리를 저장
    IEnumerator Grab()
    {
        while (recording)
        {
            yield return new WaitForEndOfFrame();
            int n = AudioRenderer.GetSampleCountForCaptureFrame();
            if (n > 0)
            {
                var buf = new NativeArray<float>(n * channels, Allocator.Temp);
                AudioRenderer.Render(buf);
                byte[] bytes = buf.Reinterpret<byte>(4).ToArray();
                audioOut.Write(bytes, 0, bytes.Length);
                buf.Dispose();
            }
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) continue;
            frame++;
            // 시나리오가 어딘가에서 멈추면 사진이 끝없이 쌓여요 (한 장 약 0.2MB). 20분을 넘으면 멈춰요.
            if (frame > Fps * 60 * 20)
            {
                Debug.LogError("[녹화] 20분이 넘도록 끝나지 않아 멈춰요. 시나리오가 기다리는 화면을 확인해 주세요.");
                Time.captureFramerate = 0;
                EditorApplication.ExitPlaymode();
                yield break;
            }
            File.WriteAllBytes(Path.Combine(MvpPlayRecorder.OutDir, $"frame_{frame:00000}.jpg"), tex.EncodeToJPG(88));
            Destroy(tex);
        }
    }

    // ───────────── 플레이 시나리오 ─────────────

    IEnumerator Script()
    {
        // 시작 화면 : 둘러본 뒤 톱니로 소리 설정을 열어 배경음 · 효과음을 줄였다 되돌려요
        yield return Wait(3.5f);
        yield return Click("TitlePanel/SettingsButton");
        yield return Wait(1.2f);
        yield return Slide("SettingsPanel/SettingsCard/BgmSlider", 0.3f);
        yield return Wait(1.5f);
        yield return Slide("SettingsPanel/SettingsCard/BgmSlider", bgmBefore);
        yield return Wait(0.6f);
        yield return Slide("SettingsPanel/SettingsCard/SfxSlider", 0.4f);
        yield return Slide("SettingsPanel/SettingsCard/SfxSlider", sfxBefore);
        yield return Wait(0.8f);
        yield return Click("SettingsPanel/SettingsCard/CloseButton");
        yield return Wait(1.5f);
        yield return Click("TitlePanel/StartButton");

        // 챕터 선택 : 잠긴 챕터를 눌러 보면 자물쇠가 흔들려요 → 소풍
        yield return Wait(2.5f);
        yield return Click("ChapterPanel/Chapter_3");
        yield return Wait(1.5f);
        yield return Click("ChapterPanel/Chapter_2");
        yield return Wait(1.5f);
        yield return Click("ChapterPanel/Chapter_1");

        // ── 1단계 방망이 : 돋보기 시범 → 돌기를 빠뜨려 엉뚱한 방망이 → 다시 그리기 ──
        yield return IntroAndObserve();
        yield return Wait(1.0f);
        yield return ReadAndTap("DrawPanel/PeekGuide", "DrawPanel/PeekGuide/TapButton");   // 돋보기를 눌러 봐 → 도깨비불이 돌아요
        yield return Wait(1.8f);
        yield return Click("HUD/MagnifierButton");                                        // 보는 법을 알려 줘요
        yield return ReadAndTap("DrawPanel/PeekGuide", "DrawPanel/PeekGuide/TapButton");   // 넘기면 5초 둘러보기
        yield return DragLens(5.4f);
        yield return ReadAndTap("DrawPanel/PeekGuide", "DrawPanel/PeekGuide/TapButton");   // 한 번만 더 빌려줄게
        yield return Wait(0.8f);
        yield return Tool("Size_1");
        yield return Line(V(250, 60), V(253, 150), V(256, 245));                           // 손잡이
        yield return Ellipse(V(256, 335), 60, 90, 14f);                                    // 머리
        yield return Wait(0.6f);
        yield return Click("DrawPanel/ToolBar/Undo");                                      // 되돌리기 : 머리가 사라져요
        yield return Wait(1.0f);
        yield return Click("DrawPanel/ToolBar/Redo");                                      // 다시 하기 : 머리가 돌아와요
        yield return Wait(1.0f);
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Wait(1.2f);
        yield return Click("ResultPanel/ChoiceRow/RetryButton");
        yield return Wait(1.5f);
        foreach (float a in new[] { 25f, 70f, 115f, 160f })                                // 이번엔 돌기까지
        {
            Vector2 dir = V(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            Vector2 edge = V(256, 335) + new Vector2(dir.x * 60, dir.y * 90);
            yield return Line(edge, edge + dir * 55);
        }
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Compare();

        // ── 2단계 가방 : 줄무늬를 빠뜨려 두 번 틀림 → 무료 돋보기 → 쪽지를 키워 줄무늬 → 맞춤 ──
        yield return IntroAndObserve();
        yield return Tool("Size_1");
        yield return Line(V(100, 60), V(412, 64), V(408, 330), V(104, 326), V(100, 60));       // 몸체
        yield return Line(V(190, 328), V(200, 400), V(256, 440), V(312, 400), V(322, 328));     // 손잡이
        yield return Line(V(170, 90), V(342, 92), V(340, 210), V(168, 206), V(170, 90));       // 주머니
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Wait(1.0f);
        yield return Click("ResultPanel/ChoiceRow/RetryButton");
        yield return Wait(1.5f);
        yield return Tool("Size_0");
        yield return Line(V(190, 180), V(322, 178));                                           // 줄무늬 한 줄만
        yield return Submit();
        yield return ResultUntilChoice();                                                      // 두 번째로 틀리면 도깨비가 돋보기를 건네줘요
        yield return Wait(1.0f);
        yield return Click("ResultPanel/ChoiceRow/RetryButton");
        yield return Wait(1.5f);
        yield return Click("HUD/MagnifierButton");                                            // 받은 돋보기로 둘러보기
        yield return DragLens(5.4f);
        yield return Wait(1.0f);
        yield return Pinch(1.45f, -10f, V(0, -150));                                           // 두 손가락으로 쪽지를 키우고 살짝 돌려요
        yield return Wait(0.8f);
        yield return Line(V(190, 120), V(322, 124));
        yield return Line(V(188, 150), V(320, 152));
        yield return Wait(0.8f);
        yield return Click("DrawPanel/FitButton");                                            // 흐트러지면 뜨는 전체 화면 버튼 : 쪽지가 다시 똑바로 가득
        yield return Wait(1.2f);
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Compare();

        // ── 3단계 돗자리 : 그리다가 소리 설정 (여는 동안 향이 멈춰요) ──
        yield return IntroAndObserve();
        yield return Tool("Size_1");
        yield return Line(V(70, 150), V(440, 145), V(445, 385), V(65, 390), V(70, 150));        // 외곽
        yield return Wait(0.8f);
        yield return Click("HUD/SettingsButton");
        yield return Wait(1.2f);
        yield return Slide("SettingsPanel/SettingsCard/BgmSlider", 0.5f);
        yield return Wait(1.2f);
        yield return Slide("SettingsPanel/SettingsCard/BgmSlider", bgmBefore);
        yield return Wait(0.8f);
        yield return Click("SettingsPanel/SettingsCard/CloseButton");
        yield return Wait(1.0f);
        yield return Tool("Size_2");
        yield return Tool("Color_파랑");
        yield return Scribble(85, 325, 430, 372, 14);                                          // 맨 위 파랑
        yield return Tool("Color_빨강");
        yield return Scribble(85, 268, 430, 315, 14);
        yield return Tool("Color_노랑");
        yield return Scribble(85, 211, 430, 258, 14);
        yield return Tool("Color_파랑");
        yield return Scribble(85, 158, 430, 201, 14);                                          // 맨 아래 파랑
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Compare();

        // ── 4단계 김밥 : 시금치를 깜빡 → 틀림 → 포기 ──
        yield return IntroAndObserve();
        yield return Tool("Size_2");
        yield return Ellipse(V(256, 256), 200, 196, 6f);
        yield return Tool("Color_빨강");
        yield return Scribble(165, 282, 240, 340, 14);
        yield return Tool("Color_노랑");
        yield return Scribble(280, 282, 355, 340, 14);
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Wait(1.5f);
        yield return Click("ResultPanel/ChoiceRow/GiveUpButton");                             // 포기 : 바로 채점표로
        yield return Compare();

        // ── 5단계 친구 : 색 섞기 시범 → 물감 빼기 → 갈색 머리 · 주황 상의 · 파란 바지 ──
        yield return IntroAndObserve();
        yield return Wait(1.0f);
        yield return ReadAndTap("DrawPanel/MixGuide", "DrawPanel/MixGuide/TapButton");     // 붓을 눌러 봐 → 도깨비불이 붓 둘레를 돌아요
        yield return Wait(1.8f);
        yield return Click("DrawPanel/ToolBar/Brush/Button");                              // 빨강을 두 번 넣어 보라고 해요
        yield return ReadAndTap("DrawPanel/MixGuide", "DrawPanel/MixGuide/TapButton");     // 넘기면 팔레트가 열리고 도깨비불이 빨강 둘레를 돌아요
        yield return Wait(1.2f);
        yield return Mix("Color_빨강", "Color_빨강");
        yield return Wait(1.2f);
        yield return ReadAndTap("DrawPanel/MixGuide", "DrawPanel/MixGuide/TapButton");     // 노랑도 두 번
        yield return Wait(1.0f);
        yield return Mix("Color_노랑", "Color_노랑");
        yield return Wait(1.2f);
        yield return ReadAndTap("DrawPanel/MixGuide", "DrawPanel/MixGuide/TapButton");     // 파랑은 한 번
        yield return Wait(1.0f);
        yield return Mix("Color_파랑");                                                    // 붓 끝이 갈색
        yield return Wait(1.4f);
        yield return ReadAndTap("DrawPanel/MixGuide", "DrawPanel/MixGuide/TapButton");     // 많이 넣은 색 쪽으로 바뀐다는 대사
        yield return ReadAndTap("DrawPanel/MixGuide", "DrawPanel/MixGuide/TapButton");     // 세 가지를 똑같이 넣으면 검정
        yield return Wait(0.8f);
        // 5단계는 검정 물감이 없어요 : 안내에서 만든 갈색으로 머리카락부터 칠하고, 검정은 세 가지를 섞어서 만들어요
        yield return Tool("Size_2");
        yield return Scribble(200, 405, 312, 458, 11);                                      // 머리카락 (안내에서 만든 갈색)
        yield return QuickMix("Color_빨강", "Color_노랑", "Color_파랑");                      // 검정은 섞어서
        yield return Tool("Size_1");
        yield return Ellipse(V(256, 400), 55, 55, 0f);                                      // 머리
        yield return Ellipse(V(178, 395), 22, 28, 0f);
        yield return Ellipse(V(334, 395), 22, 28, 0f);                                      // 양 갈래
        yield return Line(V(240, 175), V(272, 175), V(272, 205), V(240, 205), V(240, 175));  // 천 조각 (검정일 때 같이)
        yield return Tool("Size_2");
        yield return QuickMix("Color_빨강", "Color_노랑", "Color_파랑");                      // 파랑까지 넣었다가
        yield return Tool("PaintUndo");                                                     // 빼서 주황으로
        yield return Wait(0.8f);
        yield return Scribble(165, 225, 347, 335, 14);                                      // 주황 상의
        yield return Scribble(110, 250, 165, 320, 14);
        yield return Scribble(347, 250, 402, 320, 14);                                      // 팔
        yield return Mix("Color_파랑");
        yield return Scribble(175, 70, 235, 215, 14);
        yield return Scribble(280, 70, 340, 215, 14);                                       // 두 다리
        Debug.Log("[녹화] 5단계 제출 전 남은 향 : " + typeof(GameManager).GetField("timeLeft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(gm));
        yield return Submit();
        yield return ResultUntilChoice();
        yield return Compare();

        // ── 이야기 종료 : 물건이 하나씩 놓이고, 도깨비 인사 → 챕터 선택 ──
        while (!On("EndingPanel")) yield return null;
        yield return Wait(7f);
        yield return Click("EndingPanel/TapButton");
        yield return ReadAndTap("EndingPanel", "EndingPanel/TapButton");
        yield return Wait(3f);
    }

    // 도입 대사를 읽으며 넘기기 → 기억할 모습 3초 관찰 → 그리기 화면
    IEnumerator IntroAndObserve()
    {
        while (!On("ObservePanel") && !On("DrawPanel"))
        {
            if (On("DialoguePanel")) yield return ReadAndTap("DialoguePanel", "DialoguePanel/TapButton");
            else yield return null;
        }
        while (!On("DrawPanel")) yield return null;
        yield return Wait(1.2f);
    }

    // 다 그렸어! → 쪽지와 사탕 건네기 대사 → 판정 기다리기
    IEnumerator Submit()
    {
        yield return Wait(1.0f);
        yield return Click("DrawPanel/ToolBar/SubmitButton");
        while (!On("DialoguePanel") && !On("JudgePanel") && !On("ResultPanel")) yield return null;
        while (On("DialoguePanel")) yield return ReadAndTap("DialoguePanel", "DialoguePanel/TapButton");
        while (!On("ResultPanel")) yield return null;
        yield return Wait(1.5f);                                        // 도깨비가 가져온 물건 보기
    }

    // 결과 대사를 읽으며 넘겨요. 틀리면 다시 · 포기 버튼에서, 맞으면 채점표에서 멈춰요
    IEnumerator ResultUntilChoice()
    {
        while (On("ResultPanel") && !On("ResultPanel/ChoiceRow"))
            yield return ReadAndTap("ResultPanel", "ResultPanel/TapButton");
    }

    // 채점표를 보고 다음 단계로
    IEnumerator Compare()
    {
        while (!On("ComparePanel")) yield return null;
        yield return Wait(5f);
        yield return Click("ComparePanel/TapButton");
    }

    // 대사 글자가 다 나올 때까지 기다리고, 글 길이만큼 읽은 뒤 터치
    IEnumerator ReadAndTap(string panel, string tap)
    {
        DialogueBox box = ActiveBox(panel);
        for (int i = 0; i < Fps && box == null; i++) { yield return null; box = ActiveBox(panel); }
        if (box != null)
        {
            while (box.IsTyping) yield return null;
            var text = box.GetComponentInChildren<TMP_Text>();
            int len = text != null ? text.GetParsedText().Length : 20;
            yield return Wait(Mathf.Clamp(1.0f + len * 0.06f, 1.6f, 4.5f));
        }
        else yield return Wait(1.5f);
        yield return Click(tap);
    }

    static DialogueBox ActiveBox(string panel)
    {
        Transform p = Find(panel);
        if (p == null || !p.gameObject.activeInHierarchy) return null;
        foreach (var b in p.GetComponentsInChildren<DialogueBox>(false)) return b;
        return null;
    }

    // 혼색 : 팔레트를 열고 물감을 차례로 넣어요 (누를 때마다 붓 끝에서 섞여요)
    // 손에 익은 색(검정)은 조금 빠르게 섞어요
    IEnumerator QuickMix(params string[] paints)
    {
        foreach (string m in paints) { yield return Tool(m); yield return Wait(0.25f); }
        yield return Wait(0.4f);
    }

    IEnumerator Mix(params string[] paints)
    {
        foreach (string m in paints) { yield return Tool(m); yield return Wait(0.6f); }
        yield return Wait(0.8f);
    }

    // 팔레트 단추 : 닫혀 있으면 붓을 눌러 열고 눌러요. 색 단추는 테두리 안쪽, 굵기 · 물감 빼기 단추는 바로 아래
    IEnumerator Tool(string name)
    {
        if (!On("DrawPanel/ToolBar/Palette")) { yield return Click("DrawPanel/ToolBar/Brush/Button"); yield return Wait(0.5f); }
        string path = "DrawPanel/ToolBar/Palette/Card/" + name;
        if (name.StartsWith("Color_") || name == "PaintUndo") path += "/Edge/Button";
        else path += "/Button";
        yield return Click(path);
        yield return Wait(0.25f);
    }

    // 소리 설정 슬라이더를 손으로 끌어요 (값이 바뀌면 게임 볼륨도 바로 바뀌어요)
    IEnumerator Slide(string path, float target)
    {
        Transform t = Find(path);
        if (t == null) { Debug.LogWarning("[녹화] 슬라이더 없음: " + path); yield break; }
        var s = t.GetComponent<Slider>();
        yield return MoveCursor(s.handleRect.position, 0.5f);
        cursor.localScale = Vector3.one * 0.75f;
        float from = s.value;
        int n = Mathf.Max(1, Mathf.RoundToInt(0.9f * Fps));
        for (int i = 1; i <= n; i++)
        {
            s.value = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, i / (float)n));
            cursor.position = s.handleRect.position;
            yield return null;
        }
        cursor.localScale = Vector3.one;
        yield return Wait(0.3f);
    }

    // 둘러보는 동안 렌즈를 끌어 원본 여기저기를 봐요
    IEnumerator DragLens(float seconds)
    {
        var peek = FindFirstObjectByType<MagnifierPeek>(FindObjectsInactive.Include);
        var board = (RectTransform)Find("DrawPanel/Desk/Paper/Board");
        Vector3 Pt(float u, float v)
        {
            var c = new Vector3[4];
            board.GetWorldCorners(c);
            return Vector3.Lerp(Vector3.Lerp(c[0], c[3], u), Vector3.Lerp(c[1], c[2], u), v);
        }
        Vector3[] path = { Pt(0.5f, 0.5f), Pt(0.5f, 0.25f), Pt(0.35f, 0.6f), Pt(0.5f, 0.78f), Pt(0.65f, 0.55f), Pt(0.5f, 0.5f) };
        int total = Mathf.RoundToInt(seconds * Fps);
        yield return MoveCursor(path[0], 0.3f);
        var e = new PointerEventData(EventSystem.current) { position = path[0] };
        if (peek != null && peek.Active) peek.OnPointerDown(e);
        for (int i = 0; i < total && peek != null && peek.Active; i++)
        {
            float k = i / (float)total * (path.Length - 1);
            int seg = Mathf.Min((int)k, path.Length - 2);
            Vector3 p = Vector3.Lerp(path[seg], path[seg + 1], Mathf.SmoothStep(0, 1, k - seg));
            cursor.position = p;
            e.position = p;
            peek.OnDrag(e);
            yield return null;
        }
        while (peek != null && peek.Active) yield return null;
    }

    // 두 손가락으로 쪽지를 키우고 돌리기 (손가락 자리에 커서 두 개)
    IEnumerator Pinch(float scale, float angle, Vector2 offset)
    {
        var view = FindFirstObjectByType<PaperView>();
        if (view == null) yield break;
        var board = (RectTransform)Find("DrawPanel/Desk/Paper/Board");
        var c = new Vector3[4];
        board.GetWorldCorners(c);
        Vector3 mid = (c[0] + c[2]) * 0.5f;
        float spread0 = Vector3.Distance(c[0], c[2]) * 0.12f;
        cursor2 = MakeCursor();
        yield return MoveCursor(mid + Vector3.right * spread0, 0.4f);
        cursor2.position = mid - Vector3.right * spread0;
        cursor.localScale = cursor2.localScale = Vector3.one * 0.75f;
        float s0 = view.Scale, a0 = view.Angle;
        Vector2 o0 = view.Offset;                       // 처음은 전체 화면 자리에서
        int n = Mathf.RoundToInt(1.4f * Fps);
        for (int i = 1; i <= n; i++)
        {
            float k = Mathf.SmoothStep(0, 1, i / (float)n);
            float s = Mathf.Lerp(s0, scale, k), a = Mathf.Lerp(a0, angle, k);
            view.SetView(s, a, Vector2.Lerp(o0, offset, k));
            Vector3 dir = Quaternion.Euler(0, 0, a) * Vector3.right * spread0 * s / s0;
            Vector3 center = mid + (Vector3)(offset * k) * ((RectTransform)board.root).localScale.x;
            cursor.position = center + dir;
            cursor2.position = center - dir;
            yield return null;
        }
        cursor.localScale = Vector3.one;
        Destroy(cursor2.gameObject);
        yield return Wait(0.4f);
    }

    // ───────────── 커서 · 클릭 · 그리기 ─────────────

    RectTransform MakeCursor()
    {
        var canvas = GameObject.Find("Canvas").transform;
        var go = new GameObject("PenCursor", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas, false);
        var img = go.GetComponent<Image>();
        img.sprite = cursorSprite;
        img.color = new Color(0.29f, 0.17f, 0.09f, 0.55f);
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(46, 46);
        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(go.transform, false);
        var di = dot.GetComponent<Image>();
        di.sprite = cursorSprite; di.color = Color.white; di.raycastTarget = false;
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(14, 14);
        rt.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.3f, 0);
        return rt;
    }

    void Update()
    {
        if (cursor != null) cursor.SetAsLastSibling();
        if (cursor2 != null) cursor2.SetAsLastSibling();
    }

    IEnumerator MoveCursor(Vector3 to, float seconds)
    {
        Vector3 from = cursor.position;
        int n = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
        for (int i = 1; i <= n; i++)
        {
            cursor.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, i / (float)n));
            yield return null;
        }
    }

    IEnumerator Click(string path)
    {
        Transform t = Find(path);
        if (t == null) { Debug.LogWarning("[녹화] 버튼 없음: " + path); yield break; }
        var button = t.GetComponent<Button>() ?? t.GetComponentInChildren<Button>();
        if (button == null) { Debug.LogWarning("[녹화] 버튼 컴포넌트 없음: " + path); yield break; }
        var rt = (RectTransform)button.transform;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        yield return MoveCursor((c[0] + c[2]) * 0.5f, 0.55f);
        cursor.localScale = Vector3.one * 0.75f;                       // 꾹 누르는 느낌
        yield return Wait(0.15f);
        button.onClick.Invoke();
        cursor.localScale = Vector3.one;
        yield return Wait(0.3f);
    }

    // 512x512 손그림 좌표(아래가 0) → 화면 좌표. 정사각형을 그림판 세로 가운데에 놓아요.
    Vector2 ToScreen(Vector2 p)
    {
        var rt = (RectTransform)Find("DrawPanel/Desk/Paper/Board");
        Rect r = rt.rect;   // 쪽지가 돌아가 있어도 맞게
        float w = DrawingBoard.TextureWidth, h = DrawingBoard.TextureHeight;
        return rt.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, p.x / w), Mathf.Lerp(r.yMin, r.yMax, (p.y + (h - w) / 2f) / h), 0f));
    }

    // 점들을 따라 실제 드래그
    IEnumerator Line(params Vector2[] pts)
    {
        DrawingBoard board = Find("DrawPanel/Desk/Paper/Board").GetComponent<DrawingBoard>();
        yield return MoveCursor(ToScreen(pts[0]), 0.4f);
        var e = new PointerEventData(EventSystem.current) { position = ToScreen(pts[0]) };
        board.OnPointerDown(e);
        float perFrame = DrawSpeed / Fps;
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
        yield return Wait(0.2f);
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

    // 게임 시간이 멈춰도(소리 설정 창) 녹화 프레임으로 세요
    static IEnumerator Wait(float s)
    {
        int n = Mathf.RoundToInt(s * Fps);
        for (int i = 0; i < n; i++) yield return null;
    }
}

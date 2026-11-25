using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// MvpPlayRecorder : 챕터마다 플레이하는 모습을 녹화 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → 플레이 영상 녹화 (6개 전부)] 또는 Temp/mvp_record.flag
//   flag 파일이 비어 있으면 6개 전부, 이름을 적으면 그것만 (예: "1챕터" 또는 "5챕터_해피 5챕터_게임오버")
//   → 로비 시작 화면 → 챕터 사진 → 큰 사진을 눌러 들어가서, 사람이 하는 속도로 끝까지 플레이해요.
//     엔딩 뒤 로비 챕터 선택 화면이 뜨면 2초 뒤에 끝나요.
//   → Temp/Recording/<이름>/frame_00001.jpg … (초당 24장, 게임 시간 기준이라 끊김 없음)
//   → 게임 소리는 Temp/Recording/<이름>/audio.f32 (AudioRenderer, 녹화하는 동안 스피커로는 안 나와요)
//   → python Tools/sound/make_play_video.py 로 묶어서 Recordings/플레이영상_<이름>.mp4
// 판정은 하지 않아요 : AI 도 LocalJudge 계산도 안 쓰고, PlayPlans 에 정해 둔 O/X 를 그대로 띄워요.
// 그림은 그 O/X 에 맞게 손으로 정해 둔 낙서(PlayDoodles)를 HumanMotion 이 사람 손처럼 그려요.
// 다시 그릴 때는 맞은 획은 남기고 달라진 획만 되돌려서 다시 그려요 (되돌리기는 빠르게 톡톡).
// 녹화하는 동안만 앞 챕터를 깬 것으로 해 두고, 끝나면 저장 값을 원래대로 돌려놔요.
[InitializeOnLoad]
static class MvpPlayRecorder
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string Flag = "Temp/mvp_record.flag";
    const string QueueKey = "MvpPlayRecorder.Queue";
    const string CurrentKey = "MvpPlayRecorder.Current";
    const string SavedKey = "MvpPlayRecorder.SavedProgress";
    public const string OutRoot = "Temp/Recording";
    static double nextCheck;
    static bool refreshed;

    static MvpPlayRecorder()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    public static string OutDir => Path.Combine(OutRoot, SessionState.GetString(CurrentKey, "녹화"));

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        // Unity 가 다른 창 뒤에 있으면 바뀐 코드를 아직 안 읽었을 수 있어요. 먼저 새로 읽고 다음 확인 때 시작해요.
        if (!refreshed) { refreshed = true; AssetDatabase.Refresh(); nextCheck = EditorApplication.timeSinceStartup + 2.0; return; }
        refreshed = false;
        string names = File.ReadAllText(Flag).Trim();
        File.Delete(Flag);
        Run(names);
    }

    [MenuItem("Tools/찾아라도깨비/플레이 영상 녹화 (6개 전부)")]
    static void RunAll() => Run("");

    /// <summary>names : 빈칸으로 나눈 계획 이름. 비우면 전부</summary>
    public static void Run(string names)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var list = new List<string>();
        foreach (var p in PlayPlans.All)
            if (string.IsNullOrEmpty(names) || (" " + names + " ").Contains(" " + p.name + " ")) list.Add(p.name);
        if (list.Count == 0) { Debug.LogError("[녹화] 그런 이름의 계획이 없어요: " + names); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetString(QueueKey, string.Join(" ", list));
        Next();
    }

    static void Next()
    {
        string queue = SessionState.GetString(QueueKey, "").Trim();
        if (queue == "") { Debug.Log("[녹화] 모두 끝났어요. python Tools/sound/make_play_video.py 로 묶어요."); return; }
        string[] parts = queue.Split(' ');
        SessionState.SetString(CurrentKey, parts[0]);
        SessionState.SetString(QueueKey, string.Join(" ", parts, 1, parts.Length - 1));
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(LobbyPath);
        PlayerSettings.runInBackground = true;
        SaveProgress();
        PlayPlans.Plan plan = PlayPlans.Find(parts[0]);
        Progress.ResetAll();
        for (int c = 1; c < plan.chapter; c++) Progress.MarkCleared(c);
        if (plan.chapter > 1) Progress.BatFound = true;      // 1챕터 영상처럼 방망이를 찾은 것으로
        SessionState.SetBool("MvpPlayRecorder.Running", true);
        Debug.Log("[녹화] 시작 : " + parts[0]);
        EditorApplication.EnterPlaymode();
    }

    // 사람이 쌓아 둔 진행 저장 값 (녹화 앞뒤로)
    static void SaveProgress()
    {
        if (SessionState.GetString(SavedKey, "") != "") return;      // 여러 개를 이어 찍을 때는 처음 값을 지켜요
        string s = "";
        for (int c = 1; c <= 5; c++) s += Progress.IsCleared(c) ? "1" : "0";
        s += Progress.BatKnown ? (Progress.BatFound ? "F" : "L") : "-";
        SessionState.SetString(SavedKey, s);
    }

    static void RestoreProgress()
    {
        string s = SessionState.GetString(SavedKey, "");
        if (s.Length != 6) return;
        Progress.ResetAll();
        for (int c = 1; c <= 5; c++) if (s[c - 1] == '1') Progress.MarkCleared(c);
        if (s[5] != '-') Progress.BatFound = s[5] == 'F';
        SessionState.EraseString(SavedKey);
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("MvpPlayRecorder.Running", false))
        {
            SessionState.SetBool("MvpPlayRecorder.Running", false);
            OpenAIJudge.DisableAI = true;
            var go = new GameObject("PlayDirector");
            Object.DontDestroyOnLoad(go);
            var d = go.AddComponent<PlayDirector>();
            d.plan = PlayPlans.Find(SessionState.GetString(CurrentKey, ""));
            d.outDir = OutDir;
            d.cursorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Common_Circle.png");
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            OpenAIJudge.DisableAI = false;
            LocalJudge.TestOverride = null;
            if (!File.Exists(Path.Combine(OutDir, "done.txt")) && SessionState.GetString(SavedKey, "") != "")
            {
                Debug.LogWarning("[녹화] 중간에 멈췄어요. 남은 녹화는 하지 않아요.");
                SessionState.SetString(QueueKey, "");
            }
            if (SessionState.GetString(QueueKey, "").Trim() == "") RestoreProgress();
            else EditorApplication.delayCall += Next;
        }
    }
}

// ─────────────────────────────────────────────────────────────
// PlayPlans : 영상마다 무엇을 할지
// ─────────────────────────────────────────────────────────────
// 물건 하나 = "시도>시도>…". 시도는 O/X (F1부터) 와 +할 일.
//   +penguide 펜 안내(1챕터)  +magguide 돋보기 시범(2챕터)  +mixguide 색 섞기 안내(3챕터)
//   +magnifier 챕터 돋보기  +gift 두 번 틀려 받은 무료 돋보기  +pinch 쪽지 키우기 · 맞춤
//   +settings 그리다가 소리 설정 창  +paintundo 물감 하나 더 넣었다가 빼기
//   giveup : 다시 대신 포기.   5챕터 : R = 요청 물건 고르기(빼앗김), O/X = 맞설 물건 그리기
static class PlayPlans
{
    public class Plan
    {
        public string name;
        public int chapter;
        public string[] items;
        public Plan(string name, int chapter, params string[] items) { this.name = name; this.chapter = chapter; this.items = items; }
    }

    public static readonly Plan[] All =
    {
        new Plan("1챕터", 1, "X+penguide>O", "O+settings", "X>O", "O", "X>O"),
        new Plan("2챕터", 2, "XX+magguide>OX>OO+gift", "XX+magnifier>OX>OO+gift", "XX>OX>OO+gift+pinch"),
        new Plan("3챕터", 3, "XXX+mixguide>OOX>OOO+gift", "XXX>giveup", "XXX+magnifier>OXO>OOO+gift"),
        new Plan("4챕터", 4, "XXX", "OOX+magnifier", "OOO+paintundo"),
        new Plan("5챕터_해피", 5, "R>OOO", "OOX+magnifier>OOO", "OOO"),
        new Plan("5챕터_게임오버", 5, "OOO+magnifier", "R>XOX>R"),
    };

    public static Plan Find(string name)
    {
        foreach (var p in All) if (p.name == name) return p;
        return null;
    }

    public static string Combo(string attempt) => attempt.Split('+')[0];
    public static bool Has(string attempt, string tag) => System.Array.IndexOf(attempt.Split('+'), tag) > 0;
    public static bool[] Parse(string combo)
    {
        var r = new bool[combo.Length];
        for (int i = 0; i < combo.Length; i++) r[i] = combo[i] == 'O';
        return r;
    }

    /// <summary>같은 그림이 같은 손놀림으로 나오게 (계획 · 물건 · 시도마다 고정)</summary>
    public static int Seed(string plan, int item, int attempt)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in plan) h = h * 31 + c;
            return h * 31 * 31 + item * 31 + attempt + 7;
        }
    }

    public static ChapterData Chapter(int n)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ChapterData", new[] { "Assets/Data" }))
        {
            var c = AssetDatabase.LoadAssetAtPath<ChapterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.number == n) return c;
        }
        return null;
    }

    /// <summary>그릴 물건 : 보통 챕터는 items[i], 5챕터는 맞설 물건 counterItems[i]</summary>
    public static StageData StageOf(ChapterData c, int item) => c.rule == ChapterRule.Curse ? c.counterItems[item] : c.items[item];

    /// <summary>쪽지에 놓는 자리 : 물건마다 같게 (다시 그려도 남긴 획과 맞아요). 5챕터는 고를 때마다 새 쪽지라 시도마다</summary>
    public static int PlaceSeed(Plan p, int item, int attempt) => Seed(p.name, item, p.chapter == 5 ? attempt : 0) + 101;

    /// <summary>미리 보기용 : 영상에서 그릴 그림 전부 (previous : 같은 쪽지에 앞서 그린 조합, 없으면 null)</summary>
    public static IEnumerable<(string plan, StageData stage, ColorMode mode, string combo, int seed, int itemSeed, string previous)> AllDrawings()
    {
        foreach (var p in All)
        {
            ChapterData c = Chapter(p.chapter);
            for (int i = 0; i < p.items.Length; i++)
            {
                string[] attempts = p.items[i].Split('>');
                string previous = null;
                for (int a = 0; a < attempts.Length; a++)
                {
                    string combo = Combo(attempts[a]);
                    if (combo == "R" || combo == "giveup") continue;
                    yield return (p.name, StageOf(c, i), c.penMode, combo, Seed(p.name, i, a), PlaceSeed(p, i, a), previous);
                    if (c.rule != ChapterRule.Curse) previous = combo;
                }
            }
        }
    }
}

// 실제로 화면을 조작하는 감독 (Play 모드 안에서만 존재, 씬이 바뀌어도 남아요)
// 시간은 모두 "녹화 프레임" 으로 세요. 소리 설정 창이 열리면 게임 시간이 멈추기(timeScale 0) 때문이에요.
class PlayDirector : MonoBehaviour
{
    public Sprite cursorSprite;
    public PlayPlans.Plan plan;
    public string outDir;
    const int Fps = 24;

    GameManager gm;
    ChapterData chapter;
    RectTransform cursor, cursor2;
    Canvas overlay;
    int frame;
    bool recording = true;
    FileStream audioOut;
    int channels;
    float bgmBefore, sfxBefore;
    System.Random rng;
    string pendingCombo;
    readonly List<string> onBoard = new List<string>();   // 쪽지에 남아 있는 획 이름 (그린 순서)
    string brushKey;                                       // 지금 붓에 묻은 물감

    static readonly BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    IEnumerator Start()
    {
        Time.captureFramerate = Fps;   // 게임 시간이 한 프레임에 1/24초씩 → 저장이 느려도 영상은 매끄럽게
        rng = new System.Random(PlayPlans.Seed(plan.name, 99, 99));
        chapter = PlayPlans.Chapter(plan.chapter);
        LocalJudge.TestOverride = stage =>
        {
            bool[] present = PlayPlans.Parse(pendingCombo);
            var notes = new string[present.Length];
            for (int i = 0; i < notes.Length; i++) notes[i] = "(녹화) 정해 둔 결과";
            return new OpenAIJudge.Verdict { present = present, notes = notes };
        };
        bgmBefore = Sfx.BgmVolume;
        sfxBefore = Sfx.SfxVolume;
        channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        audioOut = new FileStream(Path.Combine(outDir, "audio.f32"), FileMode.Create);
        File.WriteAllText(Path.Combine(outDir, "audio.txt"), $"{AudioSettings.outputSampleRate} {channels}");
        AudioRenderer.Start();
        MakeOverlay();
        cursor = MakeCursor();
        StartCoroutine(Grab());
        yield return Script();
        recording = false;
        yield return null;
        yield return null;
        AudioRenderer.Stop();
        audioOut.Close();
        audioOut = null;
        Sfx.BgmVolume = bgmBefore;      // 사람이 정해 둔 볼륨으로 돌려놔요
        Sfx.SfxVolume = sfxBefore;
        Time.captureFramerate = 0;
        File.WriteAllText(Path.Combine(outDir, "done.txt"), frame.ToString());
        Debug.Log($"[녹화] {plan.name} 끝 : {frame}장 ({frame / (float)Fps / 60f:0.0}분)");
        EditorApplication.ExitPlaymode();
    }

    void OnDestroy()
    {
        if (audioOut != null) { AudioRenderer.Stop(); audioOut.Close(); audioOut = null; }
        Sfx.BgmVolume = bgmBefore;
        Sfx.SfxVolume = sfxBefore;
        Time.captureFramerate = 0;
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
            // 시나리오가 어딘가에서 멈추면 사진이 끝없이 쌓여요. 15분을 넘으면 멈춰요.
            if (frame > Fps * 60 * 15)
            {
                Debug.LogError("[녹화] 15분이 넘도록 끝나지 않아 멈춰요. 시나리오가 기다리는 화면을 확인해 주세요.");
                Time.captureFramerate = 0;
                EditorApplication.ExitPlaymode();
                yield break;
            }
            File.WriteAllBytes(Path.Combine(outDir, $"frame_{frame:00000}.jpg"), tex.EncodeToJPG(88));
            Destroy(tex);
        }
    }

    // ───────────── 시나리오 ─────────────

    IEnumerator Script()
    {
        // 로비 : 시작 화면 → 챕터 사진 → 큰 사진
        yield return Wait(2.2f);
        yield return Tap("StartTap");
        yield return Wait(1.6f);
        yield return Click(Btn("ChapterTap", plan.chapter - 1));
        yield return Wait(1.8f);
        yield return Click(Btn("PopupPlay"));
        while ((gm = FindFirstObjectByType<GameManager>()) == null) yield return null;
        yield return Wait(1.0f);

        for (int i = 0; i < plan.items.Length; i++)
        {
            if (chapter.rule == ChapterRule.Curse) yield return CurseStep(i);
            else yield return Item(i);
        }

        // 엔딩 → 로비
        if (chapter.rule == ChapterRule.Curse)
        {
            while (!On("CurseEndPanel")) yield return Advance(() => On("CurseEndPanel"));
            yield return Wait(4.5f);
            for (int i = 0; i < Fps * 15 && Btn("CurseEndTap", quiet: true) == null; i++) yield return null;   // 도깨비가 떠나는 연출이 끝나면 눌러요
            yield return Tap("CurseEndTap");
            yield return ReadAndTap("CurseEndPanel", "CurseEndTap");
        }
        else if (chapter.number == 1)
        {
            while (!On("EndingPanel")) yield return null;
            yield return Wait(6.5f);
            yield return Tap("EndingTap");
            yield return ReadAndTap("EndingPanel", "EndingTap");
        }
        else
        {
            while (!On("RoomEndPanel")) yield return null;
            yield return Wait(4f);
            int guard = 0;
            while (SceneManager.GetActiveScene().name != ChapterSelect.LobbyScene && guard++ < 12)
            {
                if (ActiveBox("RoomEndPanel") != null) yield return ReadAndTap("RoomEndPanel", "RoomEndTap");
                else { yield return Tap("RoomEndTap"); yield return Wait(0.6f); }
            }
        }
        while (SceneManager.GetActiveScene().name != ChapterSelect.LobbyScene) yield return null;
        yield return Wait(2f);
    }

    // 보통 챕터 물건 하나 : 시도 → 다시/포기 → 채점표
    IEnumerator Item(int index)
    {
        string[] attempts = plan.items[index].Split('>');
        StageData stage = chapter.items[index];
        for (int a = 0; a < attempts.Length; a++)
        {
            if (PlayPlans.Combo(attempts[a]) == "giveup") break;
            if (a == 0) yield return Advance(() => On("DrawPanel"));
            else while (!On("DrawPanel")) yield return null;
            yield return DrawAttempt(stage, attempts[a], PlayPlans.Seed(plan.name, index, a), PlayPlans.PlaceSeed(plan, index, a), retry: a > 0);
            yield return Advance(() => On("ResultPanel/ChoiceRow") || On("ComparePanel"));
            if (On("ResultPanel/ChoiceRow"))
            {
                yield return Wait(Human(0.9f));
                bool giveUp = a + 1 >= attempts.Length || PlayPlans.Combo(attempts[a + 1]) == "giveup";
                yield return Click(Btn(giveUp ? "ShowCompare" : "Retry"));
            }
        }
        while (!On("ComparePanel")) yield return Advance(() => On("ComparePanel"));
        yield return Wait(Human(4.5f));
        yield return Tap("NextStage");
    }

    // 5챕터 단계 하나 : 고르기 (R 요청 물건 / O·X 맞설 물건 그리기) 를 차례로
    IEnumerator CurseStep(int index)
    {
        string[] picks = plan.items[index].Split('>');
        for (int a = 0; a < picks.Length; a++)
        {
            yield return Advance(() => On("ChoosePanel"));
            yield return Wait(Human(1.8f));                                     // 두 그림을 비교해 봐요
            bool leftCounter = (bool)typeof(GameManager).GetField("leftIsCounter", Any).GetValue(gm);
            bool counter = PlayPlans.Combo(picks[a]) != "R";
            int side = counter == leftCounter ? 0 : 1;
            yield return Click(Btn("ChooseItem", side));
            if (!counter) { yield return Wait(1f); continue; }
            while (!On("DrawPanel")) yield return null;
            yield return DrawAttempt(chapter.counterItems[index], picks[a], PlayPlans.Seed(plan.name, index, a), PlayPlans.PlaceSeed(plan, index, a), retry: false);
            yield return Wait(1f);
        }
        // 마지막 결과 대사는 다음 단계(또는 엔딩)로 넘어갈 때까지 읽어요
        bool last = index + 1 >= plan.items.Length;
        if (!last) yield return Advance(() => On("ChoosePanel") || On("CurseEndPanel"));
    }

    // 대사 · 결과 대사를 읽으며 넘겨요. done 이 참이 되면 멈춰요
    IEnumerator Advance(System.Func<bool> done)
    {
        while (!done())
        {
            // 대사 상자가 떠 있어도 넘기기 버튼이 없으면(쪽지 건네기 연출 등) 저절로 넘어가길 기다려요
            if (ActiveBox("DialoguePanel") != null && Btn("DialogueTap", quiet: true) != null) yield return ReadAndTap("DialoguePanel", "DialogueTap");
            else if (On("ResultPanel") && !On("ResultPanel/ChoiceRow") && ActiveBox("ResultPanel") != null && Btn("ResultTap", quiet: true) != null) yield return ReadAndTap("ResultPanel", "ResultTap");
            else yield return null;
        }
    }

    // ───────────── 그리기 한 번 ─────────────

    IEnumerator DrawAttempt(StageData stage, string attempt, int seed, int placeSeed, bool retry)
    {
        var drawRng = new System.Random(seed);
        string combo = PlayPlans.Combo(attempt);
        if (!retry) { onBoard.Clear(); brushKey = null; }      // 새 쪽지 · 처음 붓(검정)
        var strokes = PlayDoodles.For(stage, combo, chapter.penMode);
        if (strokes == null)
        {
            Debug.LogError($"[녹화] 낙서가 없어요 : {stage.name}/{combo}. PlayDoodles 에 넣어 주세요.");
            EditorApplication.ExitPlaymode();
            yield break;
        }
        PlayDoodles.Place(strokes, placeSeed);
        int keep = 0;
        while (keep < onBoard.Count && keep < strokes.Count && onBoard[keep] == strokes[keep].key) keep++;
        yield return Wait(Human(1.0f));

        // 안내
        if (PlayPlans.Has(attempt, "magguide")) yield return MagnifierGuide();
        if (PlayPlans.Has(attempt, "mixguide")) yield return MixGuide();
        if (PlayPlans.Has(attempt, "penguide"))
        {
            yield return WaitBox("DrawPanel/PeekGuide");
            yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");  // 쪽지에 그려 봐 → 도깨비불이 쪽지 둘레를 돌아요
            yield return Wait(Human(1.0f));
        }

        // 다시 그리기 : 맞은 획은 남기고, 달라진 획만 되돌리기를 톡톡 눌러 지워요
        if (retry) yield return UndoStrokes(onBoard.Count - keep);
        onBoard.RemoveRange(keep, onBoard.Count - keep);

        if (PlayPlans.Has(attempt, "magnifier") || PlayPlans.Has(attempt, "gift"))
        {
            yield return Click(Btn("MagnifierTap"));
            yield return DragLens();
            yield return Wait(Human(0.7f));
        }
        if (PlayPlans.Has(attempt, "pinch"))
        {
            yield return Pinch(1.4f, -9f, new Vector2(0, -140));
            yield return Wait(Human(0.8f));
            yield return Click(Btn("FitTap"));                              // 흐트러지면 뜨는 전체 화면 버튼
            yield return Wait(Human(0.9f));
        }

        float speed = HumanMotion.Speed(strokes, keep, drawRng, out float secs);
        Debug.Log($"[녹화] {stage.name} {combo} : 남긴 획 {keep}, 새로 {strokes.Count - keep}획, 약 {secs:0.0}초");

        int size = Toolbar().SelectedSize;
        bool penGuide = PlayPlans.Has(attempt, "penguide");
        bool paintUndo = PlayPlans.Has(attempt, "paintundo");
        int half = keep + (strokes.Count - keep) / 2;
        for (int k = keep; k < strokes.Count; k++)
        {
            SketchStroke s = strokes[k];
            int wantSize = s.fill ? PlayDoodles.FillSize : PlayDoodles.LineSize;
            bool startBlack = brushKey == null && s.paint.IsStartBlack && IsBlack(Toolbar().CurrentColor);
            bool newPaint = s.paint.Key != brushKey && !startBlack;
            if (newPaint || wantSize != size)
            {
                if (newPaint)
                {
                    yield return Paint(s.paint.taps);
                    if (paintUndo && s.fill)
                    {
                        // 하나 더 넣었다가 빼요 : 물감 빼기
                        yield return Paint(new[] { s.paint.taps[0] });
                        yield return Wait(Human(0.5f));
                        yield return PaletteClick("PaintUndo");
                        yield return Wait(Human(0.8f));
                        paintUndo = false;
                    }
                }
                if (wantSize != size) yield return PaletteClick("SelectSize", wantSize);
                yield return Wait(Human(0.3f));
            }
            brushKey = s.paint.Key;
            size = wantSize;
            yield return DrawStroke(s, speed, drawRng);
            onBoard.Add(s.key);

            if (penGuide && k == keep) yield return PenGuide();
            if (PlayPlans.Has(attempt, "settings") && k == half) yield return Settings();
            yield return Wait(Pause(drawRng));
        }

        // 다 그렸어!
        yield return Wait(Human(0.9f));
        pendingCombo = combo;
        yield return Click(Btn("Submit"));
    }

    static bool IsBlack(Color c) => c.r < 0.1f && c.g < 0.1f && c.b < 0.1f;

    float Pause(System.Random r) => r.NextDouble() < 0.12 ? 0.6f + (float)r.NextDouble() * 0.5f : 0.1f + (float)r.NextDouble() * 0.3f;

    IEnumerator DrawStroke(SketchStroke s, float speed, System.Random r)
    {
        DrawingBoard board = Board();
        var frames = HumanMotion.Stroke(s, s.fill ? speed * 1.25f : speed, Fps, r);
        Vector2 start = ToScreen(frames[0][0]);
        yield return MoveTo(start, 0.8f);
        cursor.localScale = Vector3.one * 0.8f;
        var e = new PointerEventData(EventSystem.current) { position = start, button = PointerEventData.InputButton.Left };
        board.OnPointerDown(e);
        foreach (var f in frames)
        {
            foreach (var p in f)
            {
                e.position = ToScreen(p);
                board.OnDrag(e);
            }
            cursor.position = e.position;
            yield return null;
        }
        board.OnPointerUp(e);
        cursor.localScale = Vector3.one;
    }

    // 팔레트를 열고 물감을 차례로 눌러요 (누를 때마다 붓 끝에서 섞여요)
    IEnumerator Paint(string[] taps)
    {
        // 두 색 섞기 챕터 : 앞에 섞은 두 물감 때문에 다른 물감이 잠겨 있으면, 사람처럼 검정을 먼저 눌러 붓을 비워요
        if (taps.Length > 0 && taps[0] != "Color_검정")
        {
            if (!Toolbar().PanelOpen) { yield return Click(Btn("BrushTap")); yield return Wait(Human(0.45f)); }
            Button first = Btn("PaintTap", taps[0] == "Color_빨강" ? 0 : taps[0] == "Color_노랑" ? 1 : 2, quiet: true);
            if (first != null && !first.interactable && Btn("BlackTap", quiet: true) != null)
            {
                yield return Tap("BlackTap");
                yield return Wait(Human(0.5f));
            }
        }
        foreach (string t in taps)
        {
            string m = t == "Color_검정" ? "BlackTap" : "PaintTap";
            int arg = t == "Color_검정" ? int.MinValue : t == "Color_빨강" ? 0 : t == "Color_노랑" ? 1 : 2;
            yield return PaletteClick(m, arg);
            yield return Wait(Human(0.35f));
        }
        yield return Wait(Human(0.5f));          // 붓 끝 색이 바뀌는 걸 봐요
    }

    // 팔레트 단추는 팔레트가 열린 뒤에 찾아요 (닫혀 있으면 안 보여서 못 찾아요)
    IEnumerator PaletteClick(string method, int arg = int.MinValue)
    {
        if (!Toolbar().PanelOpen) { yield return Click(Btn("BrushTap")); yield return Wait(Human(0.45f)); }
        yield return Tap(method, arg);
    }

    // 되돌리기 n 번 : 손가락을 버튼 위에 둔 채 톡톡톡 빠르게
    IEnumerator UndoStrokes(int n)
    {
        DrawingBoard board = Board();
        if (n <= 0 || !board.CanUndo) yield break;
        Button undo = Btn("Undo");
        yield return Click(undo);
        for (int i = 1; i < n && board.CanUndo; i++)
        {
            cursor.localScale = Vector3.one * 0.78f;
            yield return Wait(0.04f);
            if (undo.isActiveAndEnabled && undo.interactable) undo.onClick.Invoke();
            cursor.localScale = Vector3.one;
            yield return Wait(0.07f + (float)rng.NextDouble() * 0.05f);
        }
        yield return Wait(Human(0.4f));
    }

    // ───────────── 안내 · 기능 ─────────────

    // 1챕터 펜 안내 : (첫 획을 그린 뒤) "되돌려 봐" → 되돌리기 → "다 그렸으면" → 화면 터치 → 다시 하기로 첫 획을 살려요
    IEnumerator PenGuide()
    {
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");
        yield return Wait(Human(0.6f));
        yield return Click(Btn("Undo"));
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");
        yield return Wait(Human(1.4f));                                     // 도깨비불이 다 그렸어! 를 가리켜요
        yield return Tap("PeekGuideTap");
        yield return Wait(Human(0.8f));
        yield return Click(Btn("Redo"));                                    // 지운 첫 획을 다시 살려요
        yield return Wait(Human(0.6f));
    }

    IEnumerator MagnifierGuide()
    {
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");      // 돋보기를 눌러 봐
        yield return Wait(Human(1.5f));
        yield return Click(Btn("MagnifierTap"));
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");      // 보는 법 → 5초 둘러보기
        yield return DragLens();
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");      // 한 번만 더 빌려줄게
        yield return Wait(Human(0.8f));
    }

    IEnumerator MixGuide()
    {
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");      // 붓을 눌러 봐
        yield return Wait(Human(1.0f));
        yield return Click(Btn("BrushTap"));
        yield return WaitBox("DrawPanel/PeekGuide");
        yield return ReadAndTap("DrawPanel/PeekGuide", "PeekGuideTap");      // 삼원색을 하나씩
        yield return Wait(Human(0.8f));
    }

    IEnumerator WaitBox(string panel)
    {
        for (int i = 0; i < Fps * 4 && ActiveBox(panel) == null; i++) yield return null;
    }

    // 소리 설정 창 : 배경음을 줄였다가 돌려놔요 (여는 동안 향이 멈춰요)
    IEnumerator Settings()
    {
        yield return Wait(Human(0.5f));
        yield return Click(Btn("ToggleSettings"));
        yield return Wait(Human(1.2f));
        Slider bgm = null;
        foreach (var s in FindObjectsByType<Slider>(FindObjectsSortMode.None))
            if (s.isActiveAndEnabled && s.onValueChanged.GetPersistentEventCount() > 0 && s.onValueChanged.GetPersistentMethodName(0) == "SetBgmVolume") bgm = s;
        if (bgm != null)
        {
            yield return Slide(bgm, Mathf.Max(0.15f, bgmBefore * 0.45f));
            yield return Wait(Human(1.0f));
            yield return Slide(bgm, bgmBefore);
        }
        yield return Wait(Human(0.7f));
        yield return Click(Btn("CloseSettings"));
        yield return Wait(Human(0.8f));
    }

    IEnumerator Slide(Slider s, float target)
    {
        yield return MoveTo(s.handleRect.position, 1f);
        cursor.localScale = Vector3.one * 0.8f;
        float from = s.value;
        int n = Mathf.Max(1, Mathf.RoundToInt((0.7f + 0.4f * (float)rng.NextDouble()) * Fps));
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)n;
            s.value = Mathf.Lerp(from, target, u * u * u * (10f - 15f * u + 6f * u * u));
            cursor.position = s.handleRect.position + new Vector3(0, Mathf.Sin(u * 7f) * 2f, 0);
            yield return null;
        }
        cursor.localScale = Vector3.one;
    }

    // 둘러보는 동안 렌즈를 끌어 원본 여기저기를 봐요 (천천히 헤매듯이)
    IEnumerator DragLens()
    {
        var peek = FindFirstObjectByType<MagnifierPeek>(FindObjectsInactive.Include);
        for (int i = 0; i < Fps && (peek == null || !peek.Active); i++) yield return null;
        if (peek == null || !peek.Active) yield break;
        var board = (RectTransform)Board().transform;
        var c = new Vector3[4];
        board.GetWorldCorners(c);
        Vector3 Pt(float u, float v) => Vector3.Lerp(Vector3.Lerp(c[0], c[3], u), Vector3.Lerp(c[1], c[2], u), v);
        float sx = (float)rng.NextDouble() * 50f, sy = (float)rng.NextDouble() * 50f;
        Vector3 first = Pt(0.5f, 0.5f);
        yield return MoveTo(first, 1f);
        var e = new PointerEventData(EventSystem.current) { position = first };
        peek.OnPointerDown(e);
        float t = 0;
        Vector3 p = first;
        while (peek != null && peek.Active)
        {
            t += 1f / Fps;
            Vector3 goal = Pt(0.5f + (Mathf.PerlinNoise(sx, t * 0.35f) - 0.5f) * 0.75f, 0.5f + (Mathf.PerlinNoise(sy, t * 0.35f) - 0.5f) * 0.85f);
            p = Vector3.Lerp(p, goal, 0.18f);
            cursor.position = p;
            e.position = p;
            peek.OnDrag(e);
            yield return null;
        }
    }

    // 두 손가락으로 쪽지를 키우고 돌리기 (손가락 자리에 커서 두 개)
    IEnumerator Pinch(float scale, float angle, Vector2 offset)
    {
        var view = FindFirstObjectByType<PaperView>();
        if (view == null) yield break;
        var board = (RectTransform)Board().transform;
        var c = new Vector3[4];
        board.GetWorldCorners(c);
        Vector3 mid = (c[0] + c[2]) * 0.5f;
        float spread0 = Vector3.Distance(c[0], c[2]) * 0.12f;
        cursor2 = MakeCursor();
        yield return MoveTo(mid + Vector3.right * spread0, 1f);
        cursor2.position = mid - Vector3.right * spread0;
        cursor.localScale = cursor2.localScale = Vector3.one * 0.8f;
        float s0 = view.Scale, a0 = view.Angle;
        Vector2 o0 = view.Offset;
        int n = Mathf.RoundToInt(1.5f * Fps);
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)n;
            float k = u * u * u * (10f - 15f * u + 6f * u * u);
            float s = Mathf.Lerp(s0, scale, k), a = Mathf.Lerp(a0, angle, k);
            view.SetView(s, a, Vector2.Lerp(o0, offset, k));
            Vector3 dir = Quaternion.Euler(0, 0, a) * Vector3.right * spread0 * s / s0;
            Vector3 center = mid + (Vector3)(offset * k) * board.root.localScale.x;
            cursor.position = center + dir;
            cursor2.position = center - dir + new Vector3(0, Mathf.Sin(u * 5f) * 3f, 0);
            yield return null;
        }
        cursor.localScale = Vector3.one;
        Destroy(cursor2.gameObject);
        yield return Wait(0.4f);
    }

    // ───────────── 대사 읽기 ─────────────

    // 대사 글자가 다 나올 때까지 기다리고, 글 길이만큼 읽은 뒤 터치
    IEnumerator ReadAndTap(string panel, string tapMethod)
    {
        DialogueBox box = ActiveBox(panel);
        for (int i = 0; i < Fps && box == null; i++) { yield return null; box = ActiveBox(panel); }
        if (box != null)
        {
            while (box != null && box.IsTyping) yield return null;
            var text = box != null ? box.GetComponentInChildren<TMP_Text>() : null;
            int len = text != null ? text.GetParsedText().Length : 20;
            yield return Wait(Mathf.Clamp(0.9f + len * 0.065f, 1.5f, 4.8f) * (0.9f + 0.25f * (float)rng.NextDouble()));
        }
        else yield return Wait(Human(1.4f));
        yield return Tap(tapMethod);
    }

    static DialogueBox ActiveBox(string panel)
    {
        Transform p = Find(panel);
        if (p == null || !p.gameObject.activeInHierarchy) return null;
        foreach (var b in p.GetComponentsInChildren<DialogueBox>(false)) return b;
        return null;
    }

    // ───────────── 커서 · 클릭 ─────────────

    void MakeOverlay()
    {
        var go = new GameObject("RecorderCursor", typeof(Canvas));
        DontDestroyOnLoad(go);
        overlay = go.GetComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 32000;
    }

    RectTransform MakeCursor()
    {
        float k = Screen.height / 1920f;        // 세로 화면 기준 크기
        var go = new GameObject("PenCursor", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(overlay.transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = cursorSprite;
        img.color = new Color(0.29f, 0.17f, 0.09f, 0.55f);
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60, 60) * k;
        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(go.transform, false);
        var di = dot.GetComponent<Image>();
        di.sprite = cursorSprite; di.color = Color.white; di.raycastTarget = false;
        dot.GetComponent<RectTransform>().sizeDelta = new Vector2(18, 18) * k;
        rt.position = new Vector3(Screen.width * 0.55f, Screen.height * 0.3f, 0);
        return rt;
    }

    IEnumerator MoveTo(Vector2 to, float speedScale)
    {
        foreach (var p in HumanMotion.Move(cursor.position, to, Fps, rng, speedScale))
        {
            cursor.position = p;
            yield return null;
        }
    }

    // 버튼 누르기 : 가운데가 아니라 버튼 안 아무 데나, 살짝 꾹
    IEnumerator Click(Button button, bool quick = false)
    {
        if (button == null) { Debug.LogWarning("[녹화] 누를 버튼이 없어요"); yield break; }
        var rt = (RectTransform)button.transform;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector2 size = c[2] - c[0];
        Vector2 p = (Vector2)((c[0] + c[2]) * 0.5f) + new Vector2(((float)rng.NextDouble() - 0.5f) * size.x * 0.4f, ((float)rng.NextDouble() - 0.5f) * size.y * 0.4f);
        if (size.x > Screen.width * 0.7f && size.y > Screen.height * 0.5f) p = ScreenTapPoint();
        if (!quick) yield return MoveTo(p, 1f);
        cursor.localScale = Vector3.one * 0.78f;
        yield return Wait(0.06f + (float)rng.NextDouble() * 0.1f);
        if (button.isActiveAndEnabled && button.interactable) button.onClick.Invoke();
        else Debug.LogWarning("[녹화] 누를 수 없는 버튼 : " + button.name);
        cursor.localScale = Vector3.one;
        yield return Wait(0.2f + (float)rng.NextDouble() * 0.15f);
    }

    // 화면 아무 데나 누르는 버튼 (대사 넘기기 등) : 대사 상자 근처 아래쪽을 눌러요
    // 버튼이 막 나타나는 중일 수 있어요 (화면 전환 · 연출) : 4초까지 기다렸다가 눌러요
    IEnumerator Tap(string method, int arg = int.MinValue)
    {
        Button b = null;
        for (int i = 0; i < Fps * 4; i++)
        {
            b = Btn(method, arg, quiet: true);
            if (b != null && b.interactable) break;
            yield return null;
        }
        if (b == null) Debug.LogWarning($"[녹화] 버튼 없음 : {method}");
        yield return Click(b);
    }

    Vector2 ScreenTapPoint() => new Vector2(Screen.width * (0.4f + 0.25f * (float)rng.NextDouble()), Screen.height * (0.14f + 0.12f * (float)rng.NextDouble()));

    void Update()
    {
        if (cursor != null) cursor.SetAsLastSibling();
        if (cursor2 != null) cursor2.SetAsLastSibling();
    }

    /// <summary>onClick 에 연결된 함수 이름(과 숫자)으로 보이는 버튼을 찾아요. 화면 배치가 바뀌어도 그대로 써요</summary>
    static Button Btn(string method, int arg = int.MinValue, bool quiet = false)
    {
        Button fallback = null;
        foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (!b.isActiveAndEnabled) continue;
            int n = b.onClick.GetPersistentEventCount();
            for (int i = 0; i < n; i++)
            {
                if (b.onClick.GetPersistentMethodName(i) != method) continue;
                if (arg != int.MinValue)
                {
                    var so = new SerializedObject(b);
                    var prop = so.FindProperty($"m_OnClick.m_PersistentCalls.m_Calls.Array.data[{i}].m_Arguments.m_IntArgument");
                    if (prop == null || prop.intValue != arg) continue;
                }
                if (b.interactable) return b;
                fallback = b;
            }
        }
        if (fallback == null && !quiet) Debug.LogWarning($"[녹화] 버튼 없음 : {method}({(arg == int.MinValue ? "" : arg.ToString())})");
        return fallback;
    }

    // ───────────── 도우미 ─────────────

    DrawingBoard Board() => FindFirstObjectByType<DrawingBoard>();
    ToolBar Toolbar() => FindFirstObjectByType<ToolBar>();

    // 512x512 손그림 좌표(아래가 0) → 화면 좌표. 정사각형을 그림판 세로 가운데에 놓아요.
    Vector2 ToScreen(Vector2 p)
    {
        var rt = (RectTransform)Board().transform;
        Rect r = rt.rect;   // 쪽지가 돌아가 있어도 맞게
        float w = DrawingBoard.TextureWidth, h = DrawingBoard.TextureHeight;
        return rt.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, p.x / w), Mathf.Lerp(r.yMin, r.yMax, (p.y + (h - w) / 2f) / h), 0f));
    }

    static Transform Find(string path)
    {
        var canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null) return null;
        Transform canvas = canvasGo.transform;
        return canvas.Find("Frame/" + path) ?? canvas.Find(path);      // 게임 화면은 Canvas/Frame 안에
    }
    static bool On(string path) { Transform t = Find(path); return t != null && t.gameObject.activeInHierarchy; }

    float Human(float s) => s * (0.8f + 0.4f * (float)rng.NextDouble());

    // 게임 시간이 멈춰도(소리 설정 창) 녹화 프레임으로 세요
    static IEnumerator Wait(float s)
    {
        int n = Mathf.RoundToInt(s * Fps);
        for (int i = 0; i < n; i++) yield return null;
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// MvpPlayAt : 원하는 단계부터 바로 Play (에디터 전용)
// ─────────────────────────────────────────────────────────────
// Temp/mvp_play.flag 에 적어 두면 바로 Play 해요.
//   4    → 1챕터의 4번째 물건(김밥)부터
//   c5   → 5챕터 (도깨비의 저주) 처음부터
// 씬 파일은 저장하지 않아요. Play 를 끝내면 시작 물건을 원래 값으로 되돌려요.
[InitializeOnLoad]
static class MvpPlayAt
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_play.flag";
    const string RestoreKey = "MvpPlayAt.Restore";
    const string ChapterKey = "MvpPlayAt.Chapter";
    static double nextCheck;

    static MvpPlayAt()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isCompiling) return;
        if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string text = File.ReadAllText(Flag).Trim().ToLowerInvariant();
        File.Delete(Flag);
        bool curse = text.StartsWith("c");
        int.TryParse(curse ? text.Substring(1) : text, out int number);
        Play(curse ? 1 : Mathf.Clamp(number, 1, 5), curse ? Mathf.Clamp(number, 1, 5) : 1);
    }

    static SerializedObject Manager()
    {
        var gm = Object.FindObjectOfType<GameManager>();
        return gm ? new SerializedObject(gm) : null;
    }

    static void Play(int stage, int chapter)
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        var so = Manager();
        if (so == null) { Debug.LogError("[PlayAt] GameManager 를 찾지 못했어요"); return; }
        var prop = so.FindProperty("editorStartStage");
        SessionState.SetInt(RestoreKey, prop.intValue);
        prop.intValue = stage;
        so.ApplyModifiedPropertiesWithoutUndo();
        SessionState.SetInt(ChapterKey, chapter);
        Debug.Log($"[PlayAt] {chapter}챕터 {stage}번째 물건부터 Play");
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            // 로비에서 챕터를 고른 것처럼 (GameManager 는 첫 프레임에 이 값을 읽어요)
            ChapterSelect.Current = SessionState.GetInt(ChapterKey, 1);
            SessionState.EraseInt(ChapterKey);
            return;
        }
        if (change != PlayModeStateChange.EnteredEditMode) return;
        int old = SessionState.GetInt(RestoreKey, -1);
        if (old < 0) return;
        SessionState.EraseInt(RestoreKey);
        var so = Manager();
        if (so == null) return;
        so.FindProperty("editorStartStage").intValue = old;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

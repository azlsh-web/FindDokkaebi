using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// MvpPlayAt : 원하는 단계부터 바로 Play (에디터 전용)
// ─────────────────────────────────────────────────────────────
// Temp/mvp_play.flag 에 단계 번호(1~5)를 적어 두면 그 단계부터 Play 해요. 예) 4 → 김밥(색 섞기)
// 씬 파일은 저장하지 않아요. Play 를 끝내면 시작 단계를 원래 값으로 되돌려요.
[InitializeOnLoad]
static class MvpPlayAt
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string Flag = "Temp/mvp_play.flag";
    const string RestoreKey = "MvpPlayAt.Restore";
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
        int.TryParse(File.ReadAllText(Flag).Trim(), out int stage);
        File.Delete(Flag);
        Play(Mathf.Clamp(stage, 1, 5));
    }

    static SerializedObject Manager()
    {
        var gm = Object.FindObjectOfType<GameManager>();
        return gm ? new SerializedObject(gm) : null;
    }

    static void Play(int stage)
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
        Debug.Log($"[PlayAt] {stage}단계부터 Play");
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
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

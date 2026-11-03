using System.IO;
using UnityEditor;

// ─────────────────────────────────────────────────────────────
// MvpAutoBuild : 씬 조립 도구(MvpSceneBuilder)를 "자동으로" 실행해 주는 장치
// ─────────────────────────────────────────────────────────────
// 언제 실행되나
//   1) Game 씬이 아직 없을 때 → 스크립트를 불러오면 자동으로 한 번 조립
//   2) Temp/mvp_rebuild.flag 파일이 생겼을 때 → 씬을 다시 조립 (Claude 가 원격으로 다시 만들 때 사용)
//      Temp 폴더는 Git 에 올라가지 않는 임시 폴더예요.
// - [InitializeOnLoad] = Unity 에디터가 스크립트를 불러올 때마다 static 생성자를 실행.
// - EditorApplication.update = 에디터가 켜져 있는 동안 계속 불리는 함수 목록.
//   여기에 1초마다 flag 파일이 있는지 확인하는 함수를 등록해요.
[InitializeOnLoad]
static class MvpAutoBuild
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string RebuildFlag = "Temp/mvp_rebuild.flag";
    static double nextCheck;

    static MvpAutoBuild()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;

        // 플레이 중이거나 컴파일 중이면 건드리지 않기
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

        bool needScene = !File.Exists(ScenePath);
        bool rebuildAsked = File.Exists(RebuildFlag);
        if (!needScene && !rebuildAsked) return;

        if (rebuildAsked) File.Delete(RebuildFlag);
        MvpSceneBuilder.Build();
    }
}

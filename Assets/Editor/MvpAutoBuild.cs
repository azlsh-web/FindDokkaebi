using System.IO;
using UnityEditor;

// ─────────────────────────────────────────────────────────────
// MvpAutoBuild : Game 씬이 아직 없으면, 스크립트가 다시 로드될 때 자동으로 한 번 만들어 줘요.
// ─────────────────────────────────────────────────────────────
// - [InitializeOnLoad] = Unity 에디터가 스크립트를 불러올 때마다 이 클래스의 static 생성자를 실행.
// - delayCall = 에디터가 준비를 다 마친 "다음 순간"에 실행 (바로 하면 씬 작업이 실패할 수 있어요).
// - Game.unity 가 이미 있으면 아무것도 안 해요. 즉, 처음 한 번만 동작합니다.
//   (메뉴 Tools → 찾아라도깨비 → MVP 씬 만들기 를 직접 눌러도 같은 결과예요.)
[InitializeOnLoad]
static class MvpAutoBuild
{
    static MvpAutoBuild()
    {
        if (File.Exists("Assets/Scenes/Game.unity")) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists("Assets/Scenes/Game.unity")) return;
            MvpSceneBuilder.Build();
        };
    }
}

using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// MvpWebGLBuild : 브라우저에서 바로 하는 웹 버전(WebGL) 만들기 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → 웹(WebGL) 빌드] (또는 Temp/mvp_webgl.flag 파일)
//   → Builds/WebGL 폴더에 index.html + Build/ 파일들이 생겨요.
//   → 팀원에게는 이 폴더를 올린 웹 페이지 링크만 주면, 설치 없이 브라우저에서 플레이할 수 있어요.
//
// 설정
//   - Gzip 압축 + "압축 풀기 대체(decompressionFallback)" : 서버 설정 없이 아무 곳에 올려도 열리게
//   - 빌드가 끝나면 원래 플랫폼(Windows)으로 돌아가요.
//   - Builds 폴더는 결과물이라 Git 에 올리지 않아요 (.gitignore)
[InitializeOnLoad]
static class MvpWebGLBuild
{
    const string Flag = "Temp/mvp_webgl.flag";
    const string OutDir = "Builds/WebGL";
    const string ResultFile = "Temp/mvp_webgl_result.txt";
    static double nextCheck;

    static MvpWebGLBuild()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2.0;
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        File.Delete(Flag);
        Build();
    }

    [MenuItem("Tools/찾아라도깨비/웹(WebGL) 빌드")]
    public static void Build()
    {
        BuildTarget before = EditorUserBuildSettings.activeBuildTarget;
        BuildTargetGroup beforeGroup = BuildPipeline.GetBuildTargetGroup(before);

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;   // 서버가 gzip 헤더를 안 붙여도 열리게
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.runInBackground = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Game.unity" },
            locationPathName = OutDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary sum = report.summary;
        string line = $"{sum.result} size={sum.totalSize / 1024 / 1024}MB time={sum.totalTime.TotalSeconds:0}s errors={sum.totalErrors}";
        File.WriteAllText(ResultFile, line);
        Debug.Log("[WebGL 빌드] " + line + " → " + OutDir);

        // 원래 플랫폼으로 되돌리기
        if (before != BuildTarget.WebGL) EditorUserBuildSettings.SwitchActiveBuildTarget(beforeGroup, before);
    }
}

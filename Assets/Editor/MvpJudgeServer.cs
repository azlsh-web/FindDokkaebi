using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// MvpJudgeServer : 웹 게임용 AI 판정 중계 서버(Server/judge)와 Unity 를 이어 주는 도구 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 1) 프롬프트 내보내기 : StageData 5개의 판정 프롬프트(OpenAIJudge.BuildPrompt)를 Server/judge/stages.json 으로
//      → 서버는 게임이 보낸 단계 번호로 이 프롬프트를 골라요. (프롬프트를 고치면 다시 내보내고 서버를 다시 배포)
// 2) 서버 주소 : Server/judge/server_url.txt 에 적힌 주소를 씬의 OpenAIJudge.serverUrl 에 넣어요 (씬 만들 때)
//
// 메뉴 [Tools → 찾아라도깨비 → 판정 서버용 프롬프트 내보내기] (또는 Temp/mvp_prompts.flag 파일)
// 웹 빌드할 때도 자동으로 내보내요.
[InitializeOnLoad]
static class MvpJudgeServer
{
    const string Flag = "Temp/mvp_prompts.flag";
    public const string PromptFile = "Server/judge/stages.json";
    const string UrlFile = "Server/judge/server_url.txt";
    static double nextCheck;

    static MvpJudgeServer()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2.0;
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        File.Delete(Flag);
        ExportPrompts();
    }

    /// <summary>판정 서버 주소 (server_url.txt 가 없으면 빈 글자 = 서버 안 씀)</summary>
    public static string ServerUrl => File.Exists(UrlFile) ? File.ReadAllText(UrlFile).Trim() : "";

    [MenuItem("Tools/찾아라도깨비/판정 서버용 프롬프트 내보내기")]
    public static void ExportPrompts()
    {
        StageData[] stages = AssetDatabase.FindAssets("t:StageData")
            .Select(g => AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null)
            .OrderBy(s => s.stageNumber)
            .ToArray();

        var sb = new StringBuilder("{\n");
        for (int i = 0; i < stages.Length; i++)
        {
            sb.Append("  \"").Append(stages[i].stageNumber).Append("\": ").Append(Json(OpenAIJudge.BuildPrompt(stages[i])));
            sb.Append(i < stages.Length - 1 ? ",\n" : "\n");
        }
        sb.Append("}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(PromptFile));
        File.WriteAllText(PromptFile, sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"[판정 서버] 프롬프트 {stages.Length}개를 내보냈어요 → {PromptFile}");
    }

    // 글자를 JSON 문자열로
    static string Json(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\t') sb.Append("\\t");
            else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// MvpFontBake : 팀 시안 글꼴(Jua)을 TextMeshPro 글꼴로 굽기 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → 글꼴 굽기 (Jua)] (또는 Temp/mvp_font.flag 파일)
//   1) 게임에 나오는 글자를 모아요 : 스크립트(.cs) 속 글자 + 단계 데이터(이야기 · 규칙 · 특징) + 숫자 · 기호
//   2) Assets/Fonts/Jua-Regular.ttf 로 그 글자들만 SDF 그림(아틀라스)에 미리 그려 둬요 (Static)
//      → 웹에서도 글꼴 파일을 실시간으로 읽지 않아 빠르고 가벼워요
//   3) 아틀라스에 없는 글자(예: AI 판정 근거에 새로 나온 글자)는 예전 글꼴(Noto Sans KR)로 대신 보여요 (fallback)
// 글꼴 라이선스 : SIL Open Font License (Assets/Fonts/Jua-OFL.txt) — 게임에 넣어 배포해도 돼요
[InitializeOnLoad]
static class MvpFontBake
{
    const string Flag = "Temp/mvp_font.flag";
    const string TtfPath = "Assets/Fonts/Jua-Regular.ttf";
    public const string AssetPath = "Assets/Fonts/Jua-Regular SDF.asset";
    const string FallbackPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
    static double nextCheck;

    static MvpFontBake()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 2.0;
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        File.Delete(Flag);
        Bake();
    }

    [MenuItem("Tools/찾아라도깨비/글꼴 굽기 (Jua)")]
    public static void Bake()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (ttf == null) { Debug.LogError("[글꼴] " + TtfPath + " 가 없어요"); return; }
        string chars = CollectChars();

        // 90pt 로 그리고 테두리 여유 9, 4096 아틀라스 (모자라면 여러 장)
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 4096, 4096,
                                                         AtlasPopulationMode.Dynamic, true);
        fa.name = "Jua-Regular SDF";
        fa.TryAddCharacters(chars, out string missing);
        fa.atlasPopulationMode = AtlasPopulationMode.Static;        // 이제 더 그리지 않고 구운 그대로

        var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
        if (fallback != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

        AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(fa, AssetPath);
        for (int i = 0; i < fa.atlasTextures.Length; i++)
        {
            fa.atlasTextures[i].name = $"Jua-Regular Atlas {i}";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
        }
        fa.material.name = "Jua-Regular Atlas Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int miss = string.IsNullOrEmpty(missing) ? 0 : missing.Length;
        string line = $"글자 {chars.Length}개를 구웠어요 (아틀라스 {fa.atlasTextures.Length}장, Jua 에 없는 글자 {miss}개 → Noto 로 대신)";
        File.WriteAllText("Temp/mvp_font_result.txt", line);
        Debug.Log("[글꼴] " + line + " → " + AssetPath);
    }

    // 게임에 나오는 글자 모으기
    static string CollectChars()
    {
        var set = new HashSet<char>();
        void AddText(string s) { if (s != null) foreach (char c in s) set.Add(c); }

        for (char c = ' '; c <= '~'; c++) set.Add(c);              // 영문 · 숫자 · 기호
        AddText("·…「」→←↑↓✓×○●□■♡★☆！？：（）、。“”‘’");
        foreach (string dir in new[] { "Assets/Scripts", "Assets/Editor" })
            foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                AddText(File.ReadAllText(f, Encoding.UTF8));
        foreach (string guid in AssetDatabase.FindAssets("t:StageData"))
        {
            var s = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guid));
            AddText(s.targetName); AddText(s.introStory); AddText(s.newRule); AddText(s.learnedRules); AddText(s.targetDescription);
            foreach (Feature f in s.features) { AddText(f.name); AddText(f.criteria); }
        }
        // 한글 음절(가~힣) · 영문 · 숫자 · 기호만 (글자가 아닌 제어 문자 등은 빼요)
        const string marks = "·…「」→←↑↓✓×○●□■♡★☆！？：（）、。“”‘’";
        var keep = set.Where(c => (c >= ' ' && c <= '~') || (c >= '가' && c <= '힣') || marks.IndexOf(c) >= 0)
                      .OrderBy(c => c);
        return new string(keep.ToArray());
    }
}

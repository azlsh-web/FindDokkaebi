using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// MvpGameViewSize : Game 창 크기를 바꿔서 여러 폰 비율로 확인 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// Temp/mvp_gameview.flag 에 "1080x2400" 처럼 적으면 Game 창을 그 크기로 바꿔요 (없으면 만들어서 골라요).
// 긴 폰(20:9) 1080x2400, 기준 1080x1920 등. 그다음 자동 테스트를 돌리면 그 크기로 사진이 찍혀요.
// Unity 가 숨겨 둔 기능이라 리플렉션으로 불러요. Unity 버전이 바뀌어 안 되면 Game 창 위쪽 크기 메뉴에서 직접 골라요.
[InitializeOnLoad]
static class MvpGameViewSize
{
    const string Flag = "Temp/mvp_gameview.flag";
    static double nextCheck;

    static MvpGameViewSize()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isCompiling) return;
        string text = File.ReadAllText(Flag).Trim();
        File.Delete(Flag);
        var parts = text.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h))
        {
            Debug.LogWarning("[GameViewSize] 크기를 읽지 못했어요 : " + text);
            return;
        }
        try { Select(w, h); Debug.Log($"[GameViewSize] Game 창 크기 : {w}x{h}"); }
        catch (Exception e) { Debug.LogWarning("[GameViewSize] 바꾸지 못했어요 : " + e.Message); }
    }

    static void Select(int w, int h)
    {
        var asm = typeof(Editor).Assembly;
        var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = single.GetProperty("instance").GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        var groupT = group.GetType();
        int total = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            object size = groupT.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            var st = size.GetType();
            bool fixedRes = st.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution";
            if (fixedRes && (int)st.GetProperty("width").GetValue(size) == w && (int)st.GetProperty("height").GetValue(size) == h) { index = i; break; }
        }
        if (index < 0)
        {
            var sizeT = asm.GetType("UnityEditor.GameViewSize");
            var kindT = asm.GetType("UnityEditor.GameViewSizeType");
            object kind = Enum.Parse(kindT, "FixedResolution");
            object size = Activator.CreateInstance(sizeT, kind, w, h, $"{w}x{h}");
            groupT.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = total;
        }
        var gameViewT = asm.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(gameViewT);
        gameViewT.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                 .Invoke(view, new object[] { index, null });
    }
}

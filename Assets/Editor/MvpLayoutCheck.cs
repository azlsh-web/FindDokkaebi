using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// MvpLayoutCheck : 화면 배치를 "숫자로" 검사 (에디터 전용, 자동 플레이 테스트가 화면마다 불러요)
// ─────────────────────────────────────────────────────────────
// 눈으로 보는 대신 실제 좌표로 계산해요. 그림은 투명한 곳을 빼고 "보이는 픽셀"만 봐요.
//   1) 걸쳐 겹침 : 한 요소가 다른 요소의 가장자리에 걸쳐 일부만 겹친 것
//                  (카드 위의 글자처럼 다른 요소 안에 완전히 들어간 것은 의도한 배치라 괜찮아요)
//   2) 글자 겹침 : 글자끼리 겹친 것
//   3) 글자 넘침 : 글자가 자기 상자보다 큰 것
//   4) 화면 밖   : 시안 화면(Frame) 밖으로 나간 것
// 결과는 문제마다 한 줄씩 (요소 경로, 겹친 넓이, 좌표). 좌표 단위 = 화면 1080 폭 기준
static class MvpLayoutCheck
{
    const float Step = 4f;           // 4 단위마다 점을 찍어서 겹침을 세요
    const float MinArea = 60f;       // 이보다 작게 겹친 건 무시 (안티에일리어싱 가장자리 등)

    // 시안 그림 자체가 원래 겹쳐 그려진 것 (시안과 같으면 괜찮아요)
    static readonly string[][] Intended =
    {
        new[] { "DrawPanel/BoardFrame", "DrawPanel/Clip" },
        new[] { "ObservePanel/Sparkle", "ObservePanel/Count" },
    };

    class Item
    {
        public Graphic g;
        public string path;
        public Rect r;              // 그려지는 자리 (preserveAspect 면 실제 그림 크기)
        public Texture2D alpha;     // 그림 투명도 (없으면 상자 전체가 보이는 것)
        public bool text;
        public int count = -1;      // 보이는 점 수 (처음 필요할 때 셈)
    }

    static int Count(Item it)
    {
        if (it.count >= 0) return it.count;
        int n = 0;
        for (float y = it.r.yMin + Step / 2; y < it.r.yMax; y += Step)
        for (float x = it.r.xMin + Step / 2; x < it.r.xMax; x += Step)
            if (Visible(it, x, y)) n++;
        return it.count = n;
    }

    static readonly Dictionary<Sprite, Texture2D> cache = new Dictionary<Sprite, Texture2D>();

    public static List<string> Check(string screen)
    {
        var problems = new List<string>();
        Canvas.ForceUpdateCanvases();
        var canvasGo = GameObject.Find("Canvas");
        Transform canvas = canvasGo.transform;
        float scale = canvasGo.GetComponent<Canvas>().scaleFactor;
        var frame = (RectTransform)canvas.Find("Frame");
        Rect frameR = WorldRect(frame, scale);

        var items = new List<Item>();
        foreach (Graphic g in canvas.GetComponentsInChildren<Graphic>(false))
        {
            if (!g.enabled || g.color.a < 0.05f || g.name == "Background") continue;
            var item = new Item { g = g, path = Path(g.transform, canvas), r = WorldRect(g.rectTransform, scale) };
            if (g is TMP_Text t)
            {
                if (string.IsNullOrWhiteSpace(t.text)) continue;
                t.ForceMeshUpdate();
                item.text = true;
                item.r = TextRect(t, scale);
                if (item.r.width <= 0) continue;
                // 3) 글자 넘침
                Rect box = WorldRect(t.rectTransform, scale);
                if (!t.enableAutoSizing)
                {
                    Vector2 pref = t.GetPreferredValues(t.text, t.rectTransform.rect.width, 0);
                    bool wrap = t.textWrappingMode != TextWrappingModes.NoWrap;
                    if (pref.y > t.rectTransform.rect.height + 2 || (!wrap && pref.x > t.rectTransform.rect.width + 2))
                        problems.Add($"[{screen}] 글자 넘침 : {item.path} \"{Short(t.text)}\" 필요 {pref.x:0}x{pref.y:0} > 상자 {t.rectTransform.rect.width:0}x{t.rectTransform.rect.height:0}");
                }
            }
            else if (g is Image img)
            {
                if (img.sprite == null && img.color.a < 0.05f) continue;
                if (img.sprite != null)
                {
                    item.alpha = Alpha(img.sprite);
                    if (img.preserveAspect && img.type == Image.Type.Simple) item.r = Fit(item.r, img.sprite.rect.size);
                }
                if (img.type == Image.Type.Filled) item.alpha = null;      // 향 막대 등 : 상자 그대로
            }
            items.Add(item);
        }

        // 가려진 것 빼기 (그려지는 순서 = 목록 순서, 뒤에 있는 것이 위)
        //   a) 화면 전체를 덮는 어두운 막(일시정지 등)이 있으면 그 아래 것들은 비교하지 않아요 (팝업 뒤)
        //   b) 위에 그려진 불투명한 그림이 97% 이상 덮고 있으면 안 보이는 것이라 빼요 (색 섞기 팔레트 아래 도구 등)
        int modal = -1;
        for (int i = 0; i < items.Count; i++)
        {
            Item it = items[i];
            if (it.text || it.g.name == "TitleArt") continue;
            Rect inter = Intersect(it.r, frameR);
            if (inter.width * inter.height >= frameR.width * frameR.height * 0.95f && it.g.color.a >= 0.3f) modal = i;
        }
        if (modal >= 0) items.RemoveRange(0, modal + 1);
        var hidden = new HashSet<Item>();
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            Item a = items[i], top = items[j];
            if (top.text || top.g.color.a < 0.9f || hidden.Contains(a)) continue;
            Rect inter = Intersect(a.r, top.r);
            if (inter.width < Step || inter.height < Step) continue;
            int under = 0;
            for (float y = inter.yMin + Step / 2; y < inter.yMax; y += Step)
            for (float x = inter.xMin + Step / 2; x < inter.xMax; x += Step)
                if (Visible(a, x, y) && Visible(top, x, y)) under++;
            if (under >= Count(a) * 0.97f && Count(a) > 0 && !a.g.transform.IsChildOf(top.g.transform)) hidden.Add(a);
        }
        items.RemoveAll(hidden.Contains);

        // 4) 화면 밖 (시작 화면 · 일시정지 화면은 화면 전체를 덮는 그림이라 제외)
        foreach (Item it in items)
        {
            if (it.path.StartsWith("TitlePanel") || it.path.StartsWith("PausePanel")) continue;
            Rect o = Opaque(it);
            if (o.width <= 0) continue;
            if (o.xMin < frameR.xMin - 2 || o.xMax > frameR.xMax + 2 || o.yMin < frameR.yMin - 2 || o.yMax > frameR.yMax + 2)
                problems.Add($"[{screen}] 화면 밖 : {it.path} 보이는 부분 {Fmt(o, frameR)}");
        }

        // 1) 걸쳐 겹침 · 2) 글자 겹침
        for (int i = 0; i < items.Count; i++)
        for (int j = i + 1; j < items.Count; j++)
        {
            Item a = items[i], b = items[j];
            if (a.g.transform.IsChildOf(b.g.transform) || b.g.transform.IsChildOf(a.g.transform)) continue;
            if (a.g.transform.parent == b.g.transform.parent && IsComposite(a.g.transform.parent)) continue;
            if (IsIntended(a.path, b.path)) continue;
            Rect inter = Intersect(a.r, b.r);
            if (inter.width < Step || inter.height < Step) continue;

            int both = 0;
            for (float y = inter.yMin + Step / 2; y < inter.yMax; y += Step)
            for (float x = inter.xMin + Step / 2; x < inter.xMax; x += Step)
                if (Visible(a, x, y) && Visible(b, x, y)) both++;
            int aAll = Count(a), bAll = Count(b);
            float area = both * Step * Step;
            if (area < MinArea) continue;
            if (a.text && b.text)
            {
                problems.Add($"[{screen}] 글자 겹침 : {a.path} ↔ {b.path} ({area:0})");
                continue;
            }
            // 한쪽이 다른 쪽 안에 거의 다 들어가 있으면(97% 이상) 의도한 "위에 올린" 배치
            float aInside = both / (float)Mathf.Max(1, aAll), bInside = both / (float)Mathf.Max(1, bAll);
            if (aInside >= 0.97f || bInside >= 0.97f) continue;
            problems.Add($"[{screen}] 걸쳐 겹침 : {a.path} ↔ {b.path} 겹친 넓이 {area:0} (각각 {aInside * 100:0}% / {bInside * 100:0}% 겹침)");
        }
        return problems;
    }

    // 버튼 묶음처럼 여러 그림이 한 덩어리인 것 (고른 표시 고리 + 버튼 그림 등)
    static bool IsComposite(Transform parent)
    {
        if (parent == null) return false;
        return parent.Find("Button") != null && parent.Find("Frame") != null
            || parent.name.StartsWith("Size_") || parent.name == "MixOpen";
    }

    static bool IsIntended(string a, string b)
    {
        foreach (string[] pair in Intended)
            if (a.StartsWith(pair[0]) && b.StartsWith(pair[1]) || a.StartsWith(pair[1]) && b.StartsWith(pair[0])) return true;
        return false;
    }

    static bool Visible(Item it, float x, float y)
    {
        if (!it.r.Contains(new Vector2(x, y))) return false;
        if (it.alpha == null) return true;
        float u = (x - it.r.xMin) / it.r.width, v = (y - it.r.yMin) / it.r.height;
        return it.alpha.GetPixelBilinear(u, v).a > 0.3f;
    }

    static Rect Opaque(Item it)
    {
        if (it.alpha == null) return it.r;
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        for (float y = it.r.yMin + Step / 2; y < it.r.yMax; y += Step)
        for (float x = it.r.xMin + Step / 2; x < it.r.xMax; x += Step)
        {
            if (!Visible(it, x, y)) continue;
            x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y);
        }
        return x0 > x1 ? new Rect(0, 0, 0, 0) : Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static Texture2D Alpha(Sprite s)
    {
        if (cache.TryGetValue(s, out Texture2D t)) return t;
        string file = AssetDatabase.GetAssetPath(s);
        t = null;
        if (File.Exists(file))
        {
            t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(file));
        }
        cache[s] = t;
        return t;
    }

    static Rect WorldRect(RectTransform rt, float scale)
    {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x / scale, c[0].y / scale, c[2].x / scale, c[2].y / scale);
    }

    static Rect TextRect(TMP_Text t, float scale)
    {
        Bounds b = t.textBounds;
        if (b.size.x <= 0 || b.size.y <= 0) return new Rect(0, 0, 0, 0);
        Vector3 p0 = t.transform.TransformPoint(b.min), p1 = t.transform.TransformPoint(b.max);
        return Rect.MinMaxRect(Mathf.Min(p0.x, p1.x) / scale, Mathf.Min(p0.y, p1.y) / scale, Mathf.Max(p0.x, p1.x) / scale, Mathf.Max(p0.y, p1.y) / scale);
    }

    static Rect Fit(Rect r, Vector2 size)
    {
        float k = Mathf.Min(r.width / size.x, r.height / size.y);
        Vector2 s = size * k;
        return new Rect(r.center.x - s.x / 2, r.center.y - s.y / 2, s.x, s.y);
    }

    static Rect Intersect(Rect a, Rect b) =>
        Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));

    static string Path(Transform t, Transform root)
    {
        string p = t.name;
        for (Transform x = t.parent; x != null && x != root; x = x.parent)
            if (x.name != "Frame") p = x.name + "/" + p;
        return p;
    }

    // 화면 위쪽 가운데 기준 좌표(시안과 같은 방향)로 보여 주기
    static string Fmt(Rect r, Rect frame) =>
        $"x {r.xMin - frame.xMin:0}~{r.xMax - frame.xMin:0}, y {frame.yMax - r.yMax:0}~{frame.yMax - r.yMin:0}";

    static string Short(string s) => s.Length > 18 ? s.Substring(0, 18).Replace("\n", " ") + "…" : s.Replace("\n", " ");
}

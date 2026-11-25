using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// PlayDoodles : 플레이 영상에서 그릴 손 낙서 (에디터 전용, 녹화용)
// ─────────────────────────────────────────────────────────────
// 물건 × 판정 조합(PlayPlans 의 O/X)마다 아이가 그릴 법한 낙서를 손으로 정해 뒀어요.
//   O 인 기준은 그 특징이 보이게, X 인 기준은 빠뜨리거나 다르게 그려요.
//   획마다 이름(key)이 있어요. 다시 그릴 때 앞 시도와 같은 획은 남기고, 다른 획부터 되돌려 다시 그려요.
// 좌표 : 쪽지 정사각형 512 기준, 왼쪽 아래가 (0,0) (MvpPlayRecorder 의 ToScreen 과 같아요)
// 미리 보기 : 메뉴 [Tools → 찾아라도깨비 → 녹화 그림 미리 보기] 또는 Temp/mvp_tracepreview.flag
//   → Temp/TracePreview/*.png (왼쪽 결과 그림, 오른쪽 낙서)

/// <summary>붓 색 하나 : 누를 팔레트 단추 순서와 그 결과 색</summary>
class SketchPaint
{
    public string[] taps;           // 예) { "Color_빨강", "Color_노랑" }, 검정 단추는 { "Color_검정" }
    public Color32 color;
    public bool IsStartBlack;        // 처음 붓 색(검정) 그대로
    public string Key => string.Join("+", taps);
}

class SketchStroke
{
    public string key;              // 획 이름 : 다시 그릴 때 같은 획은 남겨요
    public List<Vector2> pts = new List<Vector2>();
    public bool fill;               // 굵은 붓 (칠하기 · 굵은 획)
    public bool closed;
    public SketchPaint paint;
    public float Length { get { float l = 0; for (int i = 1; i < pts.Count; i++) l += Vector2.Distance(pts[i - 1], pts[i]); return l; } }
}

static class PlayDoodles
{
    public const int LineSize = 1;      // 선 : 보통 굵기 (반지름 6)
    public const int FillSize = 2;      // 칠하기 : 굵은 붓 (반지름 11)
    static readonly string[] PaintNames = { "Color_빨강", "Color_노랑", "Color_파랑" };

    /// <summary>처음 붓 색 (검정). 자유 혼합 챕터는 검정 물감이 없어서 처음 붓 그대로 써요</summary>
    public static SketchPaint StartBlack(ColorMode mode) => new SketchPaint
    {
        taps = mode == ColorMode.MixFree ? new[] { PaintNames[0], PaintNames[1], PaintNames[2] } : new[] { "Color_검정" },
        color = ToolBar.Palette[0],
        IsStartBlack = true,
    };

    // ───────────── 만들기 도구 ─────────────

    static ColorMode mode;
    static List<SketchStroke> cur;
    static SketchPaint Ink => StartBlack(mode);
    static Vector2 V(float x, float y) => new Vector2(x, y);

    /// <summary>물감 : 빨강 · 노랑 · 파랑을 몇 번 넣는지. 삼원색 챕터는 한 가지만</summary>
    static SketchPaint Mix(int r, int y, int b)
    {
        if (mode == ColorMode.Primaries)
        {
            int i = r > 0 ? 0 : y > 0 ? 1 : 2;
            return new SketchPaint { taps = new[] { PaintNames[i] }, color = ToolBar.Palette[i + 1] };
        }
        var order = new List<(int n, int i)> { (r, 0), (y, 1), (b, 2) };
        order.Sort((p, q) => p.n != q.n ? q.n.CompareTo(p.n) : p.i.CompareTo(q.i));     // 많이 넣는 물감부터
        var taps = new List<string>();
        foreach (var (n, i) in order) for (int k = 0; k < n; k++) taps.Add(PaintNames[i]);
        return new SketchPaint { taps = taps.ToArray(), color = ColorMixer.Mix(r, y, b) };
    }

    static SketchStroke Add(string key, List<Vector2> pts, bool closed, bool fill, SketchPaint paint)
    {
        var s = new SketchStroke { key = key, pts = pts, closed = closed, fill = fill, paint = paint };
        cur.Add(s);
        return s;
    }

    static void Line(string key, params Vector2[] p) => Add(key, new List<Vector2>(p), false, false, Ink);
    static void Line(string key, List<Vector2> p) => Add(key, p, false, false, Ink);
    static void Shape(string key, params Vector2[] p) { var l = new List<Vector2>(p) { p[0] }; Add(key, l, true, false, Ink); }
    static void Shape(string key, List<Vector2> p) { p.Add(p[0]); Add(key, p, true, false, Ink); }
    static void ColorLine(string key, SketchPaint paint, List<Vector2> p) => Add(key, p, false, false, paint);
    static void Thick(string key, SketchPaint paint, params Vector2[] p) => Add(key, new List<Vector2>(p), false, true, paint);

    /// <summary>타원 둘레 (start 도에서 시작해 한 바퀴)</summary>
    static List<Vector2> EllPts(float cx, float cy, float rx, float ry, float start = 100f, int n = 28)
    {
        var l = new List<Vector2>();
        for (int k = 0; k <= n; k++)
        {
            float a = (start + 360f * k / n) * Mathf.Deg2Rad;
            l.Add(V(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
        }
        return l;
    }

    static void Ell(string key, float cx, float cy, float rx, float ry, float start = 100f)
        => Add(key, EllPts(cx, cy, rx, ry, start, rx + ry < 50 ? 14 : 28), true, false, Ink);

    /// <summary>호 : a0 도에서 a1 도까지</summary>
    static List<Vector2> ArcPts(float cx, float cy, float rx, float ry, float a0, float a1, int n = 18)
    {
        var l = new List<Vector2>();
        for (int k = 0; k <= n; k++)
        {
            float a = Mathf.Lerp(a0, a1, k / (float)n) * Mathf.Deg2Rad;
            l.Add(V(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
        }
        return l;
    }

    static List<Vector2> RoundRectPts(float x0, float y0, float x1, float y1, float r)
    {
        var l = new List<Vector2>();
        l.AddRange(ArcPts(x0 + r, y1 - r, r, r, 90, 180, 4));
        l.AddRange(ArcPts(x0 + r, y0 + r, r, r, 180, 270, 4));
        l.AddRange(ArcPts(x1 - r, y0 + r, r, r, 270, 360, 4));
        l.AddRange(ArcPts(x1 - r, y1 - r, r, r, 0, 90, 4));
        return l;
    }

    /// <summary>돌기가 난 구름 둘레 (돌기 n 개)</summary>
    static List<Vector2> BumpyPts(float cx, float cy, float rx, float ry, int bumps)
    {
        var l = new List<Vector2>();
        for (int k = 0; k <= 90; k++)
        {
            float t = k / 90f * Mathf.PI * 2f, a = t + Mathf.PI * 0.55f;
            float r = 0.86f + 0.26f * Mathf.Abs(Mathf.Sin(bumps * t / 2f));
            l.Add(V(cx + Mathf.Cos(a) * rx * r, cy + Mathf.Sin(a) * ry * r));
        }
        return l;
    }

    static List<Vector2> StarPts(float cx, float cy, float R)
    {
        var l = new List<Vector2>();
        for (int k = 0; k <= 10; k++)
        {
            float a = (90f + 36f * k) * Mathf.Deg2Rad, r = k % 2 == 0 ? R : R * 0.42f;
            l.Add(V(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
        }
        return l;
    }

    /// <summary>둥글게 부푼 네모 (베개)</summary>
    static List<Vector2> PuffyPts(float cx, float cy, float a, float b)
    {
        var l = new List<Vector2>();
        for (int k = 0; k <= 40; k++)
        {
            float t = (100f + 360f * k / 40f) * Mathf.Deg2Rad, c = Mathf.Cos(t), s = Mathf.Sin(t);
            l.Add(V(cx + a * Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c)), cy + b * Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s))));
        }
        return l;
    }

    /// <summary>점 a → b 를 따라가되, 모양 안(inside)에 든 부분만</summary>
    static List<Vector2> Clip(System.Func<Vector2, bool> inside, Vector2 a, Vector2 b)
    {
        var l = new List<Vector2>();
        int n = Mathf.CeilToInt(Vector2.Distance(a, b) / 6f);
        for (int k = 0; k <= n; k++) { Vector2 p = Vector2.Lerp(a, b, k / (float)n); if (inside(p)) l.Add(p); }
        return l;
    }

    static System.Func<Vector2, bool> InEll(float cx, float cy, float rx, float ry)
        => q => (q.x - cx) * (q.x - cx) / (rx * rx) + (q.y - cy) * (q.y - cy) / (ry * ry) <= 1f;
    static System.Func<Vector2, bool> InRect(float x0, float y0, float x1, float y1)
        => q => q.x >= x0 && q.x <= x1 && q.y >= y0 && q.y <= y1;
    static System.Func<Vector2, bool> InPoly(List<Vector2> poly) => q =>
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    };
    static System.Func<Vector2, bool> And(System.Func<Vector2, bool> a, System.Func<Vector2, bool> b) => q => a(q) && b(q);

    /// <summary>칠하기 : 모양 안을 굵은 붓으로 지그재그 한 획 (아이처럼 가장자리가 조금 삐져나와요)</summary>
    static void Fill(string key, SketchPaint paint, System.Func<Vector2, bool> inside, Rect r, bool vertical = false, float Inset = 14f)
    {
        const float Gap = 17f;      // Inset 14 : 굵은 붓 반지름(11)보다 안쪽이라 검은 선이 덮이지 않아요
        var pts = new List<Vector2>();
        bool forward = true;
        float aMin = vertical ? r.xMin : r.yMin, aMax = vertical ? r.xMax : r.yMax;
        float bMin = vertical ? r.yMin : r.xMin, bMax = vertical ? r.yMax : r.xMax;
        for (float a = aMin + Inset + 4f; a <= aMax - Inset; a += Gap)
        {
            float bestS = 0, bestE = -1, s = float.NaN;
            for (float b = bMin; b <= bMax + 0.01f; b += 2f)
            {
                bool inn = inside(vertical ? V(a, b) : V(b, a));
                if (inn && float.IsNaN(s)) s = b;
                bool last = b + 2f > bMax + 0.01f;
                if ((!inn || last) && !float.IsNaN(s))
                {
                    float e = inn ? b : b - 2f;
                    if (e - s > bestE - bestS) { bestS = s; bestE = e; }
                    s = float.NaN;
                }
            }
            bestS += Inset; bestE -= Inset;
            if (bestE - bestS < 4f) continue;
            Vector2 A = vertical ? V(a, bestS) : V(bestS, a), B = vertical ? V(a, bestE) : V(bestE, a);
            if (forward) { pts.Add(A); pts.Add(B); } else { pts.Add(B); pts.Add(A); }
            forward = !forward;
        }
        if (pts.Count >= 2) Add(key, pts, false, true, paint);
    }

    // ───────────── 낙서 목록 ─────────────

    /// <summary>물건 · 조합의 낙서 (그리는 순서대로). 정해 두지 않은 조합이면 null</summary>
    public static List<SketchStroke> For(StageData stage, string combo, ColorMode penMode)
    {
        mode = penMode;
        cur = new List<SketchStroke>();
        bool ok = Build(stage.name, combo);
        var result = cur;
        cur = null;
        return ok ? result : null;
    }

    static bool Build(string name, string combo)
    {
        switch (name + "/" + combo)
        {
            // ── 1챕터 (검정 펜) ──
            case "Stage1_Bat/X":
                Shape("handle", V(238, 58), V(274, 58), V(280, 240), V(246, 240));
                Ell("headSmooth", 262, 338, 72, 96);                                  // 매끈한 타원 머리 : 돌기 없음
                return true;
            case "Stage1_Bat/O":
                Shape("handle", V(238, 58), V(274, 58), V(280, 240), V(246, 240));
                Shape("headBumpy", BumpyPts(262, 338, 70, 92, 7));                    // 돌기가 난 머리
                return true;
            case "Stage2_Bag/O":
                Shape("body", RoundRectPts(110, 70, 402, 330, 26));
                Line("handle", ArcPts(256, 330, 70, 88, 180, 0));
                Shape("pocket", RoundRectPts(180, 110, 332, 225, 12));
                Line("flap", V(182, 196), V(330, 196));
                return true;
            case "Stage3_Mat/X":
                Ell("matRound", 256, 256, 175, 150);                                  // 동그란 돗자리
                return true;
            case "Stage3_Mat/O":
                Shape("matQuad", V(80, 140), V(420, 120), V(440, 360), V(100, 385));  // 비스듬한 네모
                Line("matLine1", V(98, 205), V(424, 186));
                Line("matLine2", V(106, 300), V(432, 283));
                return true;
            case "Stage4_Gimbap/O":
                Ell("nori", 256, 256, 150, 145);
                Ell("rice", 256, 256, 108, 104, 250);
                Shape("filling1", V(205, 300), V(250, 312), V(246, 262), V(208, 258));   // 재료 : 단무지 · 시금치 · 계란
                Shape("filling2", V(268, 300), V(312, 268), V(266, 248));
                Ell("filling3", 262, 205, 34, 16, 160);
                return true;
            case "Stage5_Friend/X":
                Friend();
                var bob = new List<Vector2> { V(184, 288) };                           // 단발
                bob.AddRange(ArcPts(256, 340, 74, 74, 180, 0));
                bob.Add(V(330, 288));
                Line("bob", bob);
                return true;
            case "Stage5_Friend/O":
                Friend();
                Line("cap", ArcPts(256, 340, 72, 72, 165, 15));
                Ell("tailL", 166, 368, 30, 42);                                       // 양 갈래
                Ell("tailR", 346, 368, 30, 42, 80);
                return true;

            // ── 2챕터 (검정 펜, 모양 + 무늬) ──
            case "Ch2_Socks/XX":
                Shape("sockRect", V(190, 80), V(310, 80), V(310, 440), V(190, 440));  // 그냥 네모
                Line("vline1", V(225, 420), V(225, 105));
                Line("vline2", V(250, 105), V(250, 420));                              // 세로줄
                Line("vline3", V(275, 420), V(275, 105));
                return true;
            case "Ch2_Socks/OX":
                Sock();
                Ell("dot1", 225, 390, 11, 11);                                        // 점무늬
                Ell("dot2", 268, 340, 11, 11);
                Ell("dot3", 222, 280, 11, 11);
                Ell("dot4", 266, 228, 11, 11);
                Ell("dot5", 330, 130, 11, 11);
                return true;
            case "Ch2_Socks/OO":
                Sock();
                Line("stripe1", V(194, 395), V(296, 395));                             // 가로 줄무늬
                Line("stripe2", V(296, 340), V(194, 340));
                Line("stripe3", V(194, 285), V(296, 285));
                return true;
            case "Ch2_Umbrella/XX":
                Ell("circle", 256, 256, 150, 150);
                Line("uline1", V(128, 300), V(384, 300));
                Line("uline2", V(381, 220), V(131, 220));
                return true;
            case "Ch2_Umbrella/OX":
                Umbrella();
                Line("ustripe1", V(135, 330), V(377, 330));                            // 줄무늬 (물방울 아님)
                Line("ustripe2", V(395, 285), V(117, 285));
                return true;
            case "Ch2_Umbrella/OO":
                Umbrella();
                Ell("drop1", 185, 305, 13, 13);                                       // 물방울
                Ell("drop2", 256, 368, 13, 13);
                Ell("drop3", 327, 305, 13, 13);
                Ell("drop4", 256, 292, 13, 13);
                return true;
            case "Ch2_Pillow/XX":
                Ell("circle", 256, 256, 150, 150);
                Line("pline1", V(128, 310), V(384, 310));                              // 가로줄만
                Line("pline2", V(394, 256), V(118, 256));
                Line("pline3", V(129, 200), V(383, 200));
                return true;
            case "Ch2_Pillow/OX":
                PillowWithLines();
                return true;
            case "Ch2_Pillow/OO":
                PillowWithLines();
                Line("vline1", V(180, 367), V(180, 145));                              // 세로줄까지 : 체크무늬
                Line("vline2", V(256, 145), V(256, 367));
                Line("vline3", V(332, 367), V(332, 145));
                return true;

            // ── 3챕터 (빨강 · 노랑 · 파랑) ──
            case "Ch3_PartyHat/XXX":
                Shape("hatBox", RoundRectPts(140, 90, 372, 370, 40));                 // 둥근 네모, 띠 없음
                Fill("fillAllY", Mix(0, 1, 0), InRect(140, 90, 372, 370), new Rect(140, 90, 232, 280));
                return true;
            case "Ch3_PartyHat/OOX":
                Hat();
                HatFills(Mix(0, 0, 1), Mix(0, 1, 0), Mix(1, 0, 0));                     // 위 파랑 · 아래 빨강 : 거꾸로
                return true;
            case "Ch3_PartyHat/OOO":
                Hat();
                HatFills(Mix(1, 0, 0), Mix(0, 1, 0), Mix(0, 0, 1));
                return true;
            case "Ch3_Balloons/XXX":
                Shape("box1", V(90, 300), V(190, 300), V(190, 400), V(90, 400));      // 네모 풍선
                Line("string1", V(140, 300), V(128, 80));
                Shape("box2", V(206, 320), V(306, 320), V(306, 420), V(206, 420));
                Line("string2", V(256, 320), V(256, 80));                              // 끈이 따로따로
                Shape("box3", V(322, 300), V(422, 300), V(422, 400), V(322, 400));
                Line("string3", V(372, 300), V(386, 80));
                var red = Mix(1, 0, 0);                                                // 셋 다 빨강
                Fill("fill1", red, InRect(90, 300, 190, 400), new Rect(90, 300, 100, 100));
                Fill("fill2", red, InRect(206, 320, 306, 420), new Rect(206, 320, 100, 100));
                Fill("fill3", red, InRect(322, 300, 422, 400), new Rect(322, 300, 100, 100));
                return true;
            case "Ch3_GiftBox/XXX":
                Ell("boxRound", 256, 230, 150, 140);                                  // 동그란 상자, 리본 · 별 없음
                Fill("fillRed", Mix(1, 0, 0), InEll(256, 230, 150, 140), new Rect(106, 90, 300, 280));
                return true;
            case "Ch3_GiftBox/OXO":
                Gift();
                var y1 = Mix(0, 1, 0);
                ColorLine("deco1", y1, EllPts(172, 250, 9, 9, 100, 10));               // 노란 점 (별 아님)
                ColorLine("deco2", y1, EllPts(340, 150, 9, 9, 100, 10));
                ColorLine("deco3", y1, EllPts(172, 130, 9, 9, 100, 10));
                return true;
            case "Ch3_GiftBox/OOO":
                Gift();
                var y2 = Mix(0, 1, 0);
                ColorLine("star1", y2, StarPts(172, 200, 44));                         // 노란 별
                ColorLine("star2", y2, StarPts(340, 200, 44));
                return true;

            // ── 4챕터 (두 가지 섞기) ──
            case "Ch4_Tangerine/XXX":
                Shape("box", RoundRectPts(130, 100, 382, 360, 24));                   // 네모 몸통, 잎 없음
                Fill("fillY", Mix(0, 1, 0), InRect(130, 100, 382, 360), new Rect(130, 100, 252, 260));
                return true;
            case "Ch4_Grapes/OOX":
                float[][] rows = { new[] { 370f, 166, 226, 286, 346 }, new[] { 318f, 196, 256, 316 }, new[] { 266f, 226, 286 }, new[] { 214f, 256 } };
                int g = 0;
                var bunch = new List<Vector2> { V(122, 400), V(390, 400), V(256, 176) };
                Thick("stem", Mix(0, 1, 1), V(256, 400), V(260, 440), V(290, 468));      // 초록 줄기
                Fill("fillBlue", Mix(0, 0, 1), InPoly(bunch), new Rect(120, 175, 272, 240));    // 보라 대신 파랑 : 먼저 칠하고
                g = 0;
                foreach (var row in rows)                                                       // 그 위에 알을 검정으로
                    for (int i = 1; i < row.Length; i++) Ell("grape" + (++g), row[i], row[0], 30, 28, 90 + g * 37);
                return true;
            case "Ch4_Candy/OOO":
                var inC = InEll(256, 256, 110, 110);
                System.Func<Vector2, float> u = q => q.x + 0.45f * (q.y - 256f);
                Ell("candy", 256, 256, 110, 110);
                Shape("wrapL", V(148, 256), V(72, 312), V(66, 200));
                Shape("wrapR", V(364, 256), V(440, 312), V(446, 200));
                Line("stripe1", Clip(inC, V(216 - 0.45f * 110, 366), V(216 + 0.45f * 110, 146)));
                Line("stripe2", Clip(inC, V(296 + 0.45f * 110, 146), V(296 - 0.45f * 110, 366)));
                var orange = Mix(1, 1, 0);
                var bounds = new Rect(146, 146, 220, 220);
                Fill("fillA", orange, And(inC, q => u(q) < 216), bounds);
                Fill("fillC", orange, And(inC, q => u(q) > 296), bounds);
                Fill("fillB", Mix(0, 1, 1), And(inC, q => u(q) > 216 && u(q) < 296), bounds);
                return true;

            // ── 5챕터 (자유 섞기) ──
            case "Ch5_Bujeok/OOO":
                Shape("paper", RoundRectPts(170, 40, 342, 470, 8));
                Fill("fillPaper", Mix(1, 3, 0), InRect(170, 40, 342, 470), new Rect(170, 40, 172, 430), vertical: true);
                var brown = Mix(3, 1, 1);
                Thick("main", brown, V(256, 428), V(250, 300), V(258, 170), V(253, 80));
                Thick("cross", brown, V(200, 390), V(312, 378));
                Thick("diag1", brown, V(208, 334), V(302, 268));
                Thick("diag2", brown, V(304, 334), V(212, 262));
                Thick("zig", brown, V(204, 205), V(250, 232), V(300, 196), V(256, 160), V(306, 122));
                return true;
            case "Ch5_DakpiBowl/OOX":
                Bowl();
                Fill("blood", Mix(1, 0, 0), InEll(256, 330, 170, 42), new Rect(86, 288, 340, 84));   // 새빨강
                return true;
            case "Ch5_DakpiBowl/OOO":
                Bowl();
                Fill("blood", Mix(2, 1, 1), InEll(256, 330, 170, 42), new Rect(86, 288, 340, 84));   // 어두운 붉은 갈색
                return true;
            case "Ch5_DakpiBowl/XOX":
                Shape("boxBowl", V(110, 90), V(402, 90), V(402, 340), V(110, 340));   // 네모 상자
                Line("top", V(112, 300), V(400, 300));
                Line("band1", V(400, 240), V(112, 240));
                Line("band2", V(112, 170), V(400, 170));
                Fill("blue", Mix(0, 0, 1), InRect(110, 300, 402, 340), new Rect(110, 300, 292, 40));
                return true;
            case "Ch5_PatBag/OOO":
                var bag = new List<Vector2> { V(225, 352), V(150, 300), V(110, 200), V(140, 100), V(256, 60), V(372, 100), V(402, 200), V(362, 300),
                                              V(287, 352), V(312, 410), V(282, 394), V(256, 420), V(230, 394), V(200, 410) };
                Shape("bag", bag);
                Line("tie", V(214, 352), V(298, 352));
                Ell("knot", 256, 354, 13, 10);
                Line("tail1", V(250, 346), V(228, 310));
                Line("tail2", V(262, 346), V(286, 308));
                Fill("fillBag", Mix(2, 1, 1), And(InPoly(bag), q => q.y < 298), new Rect(105, 55, 302, 245));
                return true;
        }
        return false;
    }

    // ── 여러 조합이 같이 쓰는 부분 (같은 이름의 획은 다시 그릴 때 남겨요) ──

    static void Friend()
    {
        Ell("face", 256, 330, 70, 75);
        Line("eyeL", V(230, 348), V(232, 330));
        Line("eyeR", V(282, 348), V(284, 330));
        Line("mouth", ArcPts(256, 318, 22, 12, 200, 340, 8));
        Line("body", V(256, 255), V(256, 130));
        Line("arms", V(186, 212), V(256, 226), V(326, 212));
        Line("legs", V(210, 42), V(256, 130), V(302, 42));
    }

    static void Sock()
    {
        Shape("sock", V(190, 440), V(300, 440), V(300, 205), V(360, 172), V(398, 132), V(394, 92), V(360, 72),
                      V(250, 72), V(206, 98), V(190, 150));
    }

    static void Umbrella()
    {
        var roof = ArcPts(256, 250, 160, 170, 180, 0);
        for (int k = 0; k < 3; k++) roof.AddRange(ArcPts(362.7f - k * 106.7f, 250, 53.3f, 22, 360, 180, 8));
        Shape("roof", roof);
        var handle = new List<Vector2> { V(256, 250), V(256, 95) };
        handle.AddRange(ArcPts(230, 95, 26, 30, 360, 180, 8));
        Line("handle", handle);
    }

    static void PillowWithLines()
    {
        Shape("pillow", PuffyPts(256, 256, 180, 125));
        Line("hline1", V(92, 320), V(420, 320));
        Line("hline2", V(420, 256), V(92, 256));
        Line("hline3", V(92, 192), V(420, 192));
    }

    static readonly System.Func<Vector2, bool> InCone = q => q.y >= 78 && q.y <= 452 && Mathf.Abs(q.x - 256) <= 138f * (452 - q.y) / 374f;

    static void Hat()
    {
        Shape("cone", V(256, 452), V(118, 78), V(394, 78));
        Line("band1", V(211, 330), V(301, 330));
        Line("band2", V(349, 200), V(163, 200));
    }

    static void HatFills(SketchPaint top, SketchPaint mid, SketchPaint bottom)
    {
        Fill("top_" + top.Key, top, And(InCone, q => q.y > 330), new Rect(200, 330, 112, 122));
        Fill("mid_" + mid.Key, mid, And(InCone, q => q.y > 200 && q.y < 330), new Rect(160, 200, 192, 130));
        Fill("bot_" + bottom.Key, bottom, And(InCone, q => q.y < 200), new Rect(115, 78, 282, 122));
    }

    static void Gift()
    {
        Shape("box", V(110, 70), V(402, 70), V(402, 320), V(110, 320));
        Line("ribbonL", V(236, 70), V(236, 320));
        Line("ribbonR", V(276, 320), V(276, 70));
        Ell("bowL", 214, 352, 42, 26, 0);
        Ell("bowR", 298, 352, 42, 26, 180);
        var blue = Mix(0, 0, 1);
        Fill("blueL", blue, InRect(110, 70, 236, 320), new Rect(110, 70, 126, 250));
        Fill("blueR", blue, InRect(276, 70, 402, 320), new Rect(276, 70, 126, 250));
        Fill("ribbonRed", Mix(1, 0, 0), InRect(232, 70, 280, 320), new Rect(232, 70, 48, 250), vertical: true, Inset: 4f);   // 리본은 선 사이를 꽉
    }

    static void Bowl()
    {
        Ell("rim", 256, 330, 170, 42, 180);
        Line("bowlBody", ArcPts(256, 330, 170, 210, 180, 360));
        var inBowl = And(InEll(256, 330, 170, 210), q => q.y < 320);
        var b1 = new List<Vector2>(); var b2 = new List<Vector2>();
        for (float x = 90; x <= 422; x += 8)
        {
            float k = 1f - (x - 256f) * (x - 256f) / (170f * 170f);
            Vector2 p1 = V(x, 268 - 16 * k), p2 = V(x, 205 - 16 * k);
            if (inBowl(p1)) b1.Add(p1);
            if (inBowl(p2)) b2.Add(p2);
        }
        b2.Reverse();
        Line("band1", b1);
        Line("band2", b2);
    }

    static int Hash(string s) { unchecked { int h = 23; foreach (char ch in s) h = h * 31 + ch; return h; } }

    // ───────────── 쪽지에 놓기 ─────────────

    /// <summary>그림 전체를 조금 옮기고 · 줄이고 · 돌려요 (물건마다 같게 : 다시 그려도 남긴 획과 맞아요)</summary>
    public static void Place(List<SketchStroke> strokes, int itemSeed)
    {
        var rng = new System.Random(itemSeed);
        float scale = 0.9f + 0.08f * (float)rng.NextDouble();
        float angle = ((float)rng.NextDouble() * 2 - 1) * 4f * Mathf.Deg2Rad;
        Vector2 off = V((float)rng.NextDouble() * 20 - 10, (float)rng.NextDouble() * 20 - 10);
        Vector2 c = V(256, 256);
        float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
        foreach (var s in strokes)
        {
            var r = new System.Random(itemSeed ^ Hash(s.key));
            Vector2 so = V((float)r.NextDouble() * 5 - 2.5f, (float)r.NextDouble() * 5 - 2.5f);   // 획마다 조금씩 어긋나게
            for (int i = 0; i < s.pts.Count; i++)
            {
                Vector2 p = (s.pts[i] - c) * scale;
                s.pts[i] = c + V(p.x * cs - p.y * sn, p.x * sn + p.y * cs) + off + so;
            }
        }
    }
}

// ─────────────────────────────────────────────────────────────
// HumanMotion : 사람 손 같은 움직임 (펜 선 · 커서 이동)
// ─────────────────────────────────────────────────────────────
static class HumanMotion
{
    /// <summary>선 하나 → 프레임마다 손이 지나간 점들 (한 프레임 안에서도 촘촘히 : 실제 마우스처럼 여러 번 끌어요)</summary>
    public static List<List<Vector2>> Stroke(SketchStroke s, float speed, int fps, System.Random rng)
    {
        // 1) 촘촘하게 (2px)
        var dense = new List<Vector2>();
        for (int i = 1; i < s.pts.Count; i++)
        {
            Vector2 a = s.pts[i - 1], b = s.pts[i];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
            for (int k = (i == 1 ? 0 : 1); k <= n; k++) dense.Add(Vector2.Lerp(a, b, k / (float)n));
        }
        if (dense.Count < 2) dense.Add(dense[0] + Vector2.right);
        // 2) 끝을 조금 못 닫거나 조금 지나쳐요
        if (s.closed)
        {
            if (rng.NextDouble() < 0.5) { int cut = Mathf.Min(dense.Count / 6, 2 + rng.Next(5)); dense.RemoveRange(dense.Count - cut, cut); }
            else { int over = 2 + rng.Next(5); for (int k = 1; k <= over && k < dense.Count; k++) dense.Add(dense[k]); }
        }
        // 3) 손떨림 : 느린 흔들림 + 잔떨림 (선 방향의 수직으로)
        float seedA = (float)rng.NextDouble() * 100f, seedB = (float)rng.NextDouble() * 100f;
        float amp = s.fill ? 3.2f : 1.9f;
        var arc = new float[dense.Count];
        for (int i = 1; i < dense.Count; i++) arc[i] = arc[i - 1] + Vector2.Distance(dense[i - 1], dense[i]);
        var wob = new Vector2[dense.Count];
        for (int i = 0; i < dense.Count; i++)
        {
            Vector2 dir = (dense[Mathf.Min(i + 1, dense.Count - 1)] - dense[Mathf.Max(i - 1, 0)]).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            float slow = (Mathf.PerlinNoise(seedA, arc[i] / 70f) - 0.5f) * 2f * amp;
            float fast = (Mathf.PerlinNoise(seedB, arc[i] / 11f) - 0.5f) * 1.2f;
            wob[i] = dense[i] + nrm * (slow + fast);
        }
        // 4) 속도 : 천천히 시작 → 빨라짐 → 모서리에서 느려짐 → 끝에서 느려짐
        var v = new float[dense.Count];
        float total = arc[dense.Count - 1];
        float seedC = (float)rng.NextDouble() * 100f;
        for (int i = 0; i < dense.Count; i++)
        {
            int a = Mathf.Max(0, i - 4), b = Mathf.Min(dense.Count - 1, i + 4), m = i;
            float turn = 0f;
            if (b - m > 0 && m - a > 0)
                turn = Vector2.Angle(dense[m] - dense[a], dense[b] - dense[m]) * Mathf.Deg2Rad;
            float corner = 1f / (1f + (s.fill ? 0.8f : 1.7f) * turn);
            float ramp = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(arc[i] / 30f)) * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01((total - arc[i]) / 22f));
            float drift = 0.85f + 0.3f * Mathf.PerlinNoise(seedC, arc[i] / 90f);
            v[i] = speed * Mathf.Max(0.3f, corner) * ramp * drift;
        }
        // 5) 프레임으로 나누기
        var frames = new List<List<Vector2>>();
        float dt = 1f / fps, t = 0f;
        var cur = new List<Vector2> { wob[0] };
        for (int i = 1; i < dense.Count; i++)
        {
            t += Vector2.Distance(dense[i - 1], dense[i]) / Mathf.Max(30f, v[i]);
            cur.Add(wob[i]);
            if (t >= dt) { frames.Add(cur); cur = new List<Vector2>(); t -= dt; }
        }
        if (cur.Count > 0) frames.Add(cur);
        return frames;
    }

    /// <summary>커서 옮기기 : 살짝 둥근 곡선, 가속 · 감속, 가끔 조금 지나쳤다가 돌아와요. 화면 좌표</summary>
    public static List<Vector2> Move(Vector2 from, Vector2 to, int fps, System.Random rng, float speedScale = 1f)
    {
        var path = new List<Vector2>();
        float d = Vector2.Distance(from, to);
        if (d < 2f) { path.Add(to); return path; }
        float seconds = (0.22f + 0.13f * Mathf.Log(1f + d / 45f, 2f)) * (0.85f + 0.3f * (float)rng.NextDouble()) * speedScale;
        bool overshoot = d > 160f && rng.NextDouble() < 0.3;
        Vector2 dir = (to - from) / d, nrm = new Vector2(-dir.y, dir.x);
        Vector2 end = overshoot ? to + dir * (6f + 10f * (float)rng.NextDouble()) + nrm * (float)(rng.NextDouble() * 6 - 3) : to;
        float bend = d * (0.04f + 0.14f * (float)rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1 : 1);
        Vector2 c1 = Vector2.Lerp(from, end, 0.3f) + nrm * bend, c2 = Vector2.Lerp(from, end, 0.7f) + nrm * bend * 0.6f;
        int n = Mathf.Max(2, Mathf.RoundToInt(seconds * fps));
        float seed = (float)rng.NextDouble() * 50f;
        for (int i = 1; i <= n; i++)
        {
            float u = i / (float)n;
            float s = u * u * u * (10f - 15f * u + 6f * u * u);        // 최소 저크 (사람 팔의 속도 모양)
            float k = 1 - s;
            Vector2 p = k * k * k * from + 3 * k * k * s * c1 + 3 * k * s * s * c2 + s * s * s * end;
            p += new Vector2(Mathf.PerlinNoise(seed, u * 6f) - 0.5f, Mathf.PerlinNoise(seed + 9f, u * 6f) - 0.5f) * 1.6f * (1 - s);
            path.Add(p);
        }
        if (overshoot)
        {
            int m = Mathf.Max(2, Mathf.RoundToInt((0.12f + 0.08f * (float)rng.NextDouble()) * fps));
            for (int i = 1; i <= m; i++) { float u = i / (float)m; path.Add(Vector2.Lerp(end, to, u * u * (3 - 2 * u))); }
        }
        return path;
    }


    /// <summary>펜 속도 (쪽지 px/초) : 보통은 천천히, 그릴 게 많으면 빨라져서 한 장이 7초쯤에 끝나요 (from 번째 획부터 그려요)</summary>
    public static float Speed(List<SketchStroke> strokes, int from, System.Random rng, out float seconds)
    {
        float len = 0, overhead = 0;
        string paint = null;
        for (int i = from; i < strokes.Count; i++)
        {
            var s = strokes[i];
            len += s.Length * (s.fill ? 0.8f : 1f);
            overhead += 0.35f;                                  // 다음 획까지 옮기기 + 잠깐 멈춤
            if (s.paint != null && s.paint.Key != paint && !s.paint.IsStartBlack) overhead += 0.6f + 0.35f * s.paint.taps.Length;
            if (s.paint != null) paint = s.paint.Key;
        }
        float speed = 520f * (0.9f + 0.2f * (float)rng.NextDouble());
        float drawTime = Mathf.Max(2.5f, 7f - overhead);
        if (len * 1.35f / speed > drawTime) speed = Mathf.Min(1500f, len * 1.35f / drawTime);
        seconds = overhead + len * 1.35f / speed;
        return speed;
    }
}

// ─────────────────────────────────────────────────────────────
// 미리 보기 : 영상에서 그릴 낙서를 png 로 (왼쪽 결과 그림, 오른쪽 낙서)
// ─────────────────────────────────────────────────────────────
[InitializeOnLoad]
static class DoodlePreview
{
    const string Flag = "Temp/mvp_tracepreview.flag";
    static double nextCheck;
    static bool refreshed;
    static DoodlePreview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (!File.Exists(Flag) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        // Unity 가 다른 창 뒤에 있으면 바뀐 코드를 아직 안 읽었을 수 있어요. 먼저 새로 읽고 다음 확인 때 시작해요.
        if (!refreshed) { refreshed = true; AssetDatabase.Refresh(); nextCheck = EditorApplication.timeSinceStartup + 2.0; return; }
        refreshed = false;
        File.Delete(Flag);
        Run();
    }

    [MenuItem("Tools/찾아라도깨비/녹화 그림 미리 보기")]
    public static void Run()
    {
        string dir = "Temp/TracePreview";
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        int count = 0, missing = 0;
        var log = new System.Text.StringBuilder();
        foreach (var d in PlayPlans.AllDrawings())
        {
            var strokes = PlayDoodles.For(d.stage, d.combo, d.mode);
            if (strokes == null) { Debug.LogError($"[녹화 그림] 낙서가 없어요 : {d.stage.name}/{d.combo}"); missing++; continue; }
            PlayDoodles.Place(strokes, d.itemSeed);
            int keep = 0;
            if (d.previous != null)
            {
                var prev = PlayDoodles.For(d.stage, d.previous, d.mode);
                while (prev != null && keep < prev.Count && keep < strokes.Count && prev[keep].key == strokes[keep].key) keep++;
                if (prev != null) log.AppendLine($"{d.plan} {d.stage.name} {d.previous}→{d.combo} : 되돌리기 {prev.Count - keep}번, 새로 {strokes.Count - keep}획");
            }
            var rng = new System.Random(d.seed);
            float speed = HumanMotion.Speed(strokes, keep, rng, out float secs);
            var px = new Color32[512 * 1024];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            Sprite sprite = d.stage.SpriteFor(PlayPlans.Parse(d.combo));
            if (sprite != null) Blit(px, sprite);
            int[] radius = { 3, 6, 11 };
            foreach (var s in strokes)
            {
                Color32 c = s.paint != null ? s.paint.color : new Color32(0, 0, 0, 255);
                int r = radius[s.fill ? PlayDoodles.FillSize : PlayDoodles.LineSize];
                Vector2? last = null;
                foreach (var frame in HumanMotion.Stroke(s, speed, 24, rng))
                    foreach (var p in frame)
                    {
                        if (last.HasValue) Seg(px, last.Value, p, r, c); else Dot(px, p, r, c);
                        last = p;
                    }
            }
            var tex = new Texture2D(1024, 512);
            tex.SetPixels32(px);
            tex.Apply();
            count++;
            File.WriteAllBytes($"{dir}/{count:00}_{d.plan}_{d.stage.name}_{d.combo}_{secs:0.0}s_{strokes.Count - keep}획.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        File.WriteAllText($"{dir}/다시그리기.txt", log.ToString());
        Debug.Log($"[녹화 그림] 미리 보기 {count}장 → {dir}" + (missing > 0 ? $" (낙서 없음 {missing}개)" : ""));
    }

    static void Blit(Color32[] px, Sprite sprite)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        Color32[] src = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        for (int y = 0; y < 512; y++)
            for (int x = 0; x < 512; x++)
            {
                Color32 c = src[(y * h / 512) * w + x * w / 512];
                float a = c.a / 255f;
                px[y * 1024 + x] = new Color32((byte)(c.r * a + 255 * (1 - a)), (byte)(c.g * a + 255 * (1 - a)), (byte)(c.b * a + 255 * (1 - a)), 255);
            }
        Object.DestroyImmediate(tex);
    }

    static void Seg(Color32[] px, Vector2 a, Vector2 b, int r, Color32 c)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
        for (int i = 1; i <= n; i++) Dot(px, Vector2.Lerp(a, b, i / (float)n), r, c);
    }

    static void Dot(Color32[] px, Vector2 p, int r, Color32 c)
    {
        int cx = Mathf.RoundToInt(p.x) + 512, cy = Mathf.RoundToInt(p.y);
        for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if (x >= 512 && x < 1024 && y >= 0 && y < 512 && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) px[y * 1024 + x] = c;
    }
}

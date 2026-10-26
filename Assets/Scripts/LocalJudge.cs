using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// LocalJudge : 인터넷·AI 없이 Unity 안에서 그림을 "자동으로" 판정하는 스크립트
// ─────────────────────────────────────────────────────────────
// 기획서 6장 "대체 기술 : 색은 HSV 분석으로 확인하고, 형태는 템플릿 매칭으로 비교"
//   - OpenAI 키가 없을 때 GameManager 가 이걸로 판정해요. (체크박스 같은 수동 판정은 없어요)
//   - 결과 모양은 AI 판정과 똑같이 Verdict(present[], notes[]) 라서 결과 화면은 그대로예요.
//
// 원리 (3단계)
//   1) 쪽지 512x512 픽셀을 4x4 씩 묶어 128x128 칸으로 줄이고,
//      칸마다 HSV(색상·채도·밝기)로 "흰색/검정/빨강/노랑/파랑/초록/주황/보라/갈색" 중 하나로 분류해요.
//   2) 모양 계산 도구 : 그림이 있는 칸(잉크), 선으로 닫힌 안쪽(구멍), 덩어리(연결된 칸들), 줄 단위 길이 …
//   3) 특징마다 StageData 의 Feature.check 에 적힌 계산을 해서 present(그림)/absent(빠뜨림)를 정해요.
//
// ※ 규칙으로 하는 판정이라 AI 보다 서툰 그림에 약해요. 기준 숫자는 아래 각 함수의 const 로 조절해요.
// ※ 좌표 : x 는 왼쪽→오른쪽, y 는 아래→위 (텍스처 기준). 그래서 "위쪽" = y 가 큰 쪽이에요.
public static class LocalJudge
{
    /// <summary>자동 플레이 테스트에서만 사용 : 정해진 결과를 돌려주게 할 때 (게임에서는 항상 null)</summary>
    public static Func<StageData, OpenAIJudge.Verdict> TestOverride;

    const int N = 128;              // 줄인 칸 수 (가로·세로)
    const int White = 0, Black = 1; // 칸 분류 번호. 2부터는 InkColor + 1

    // ───────────── 바깥에서 부르는 함수 ─────────────

    /// <summary>그림을 판정해서 특징마다 그렸는지(present)와 이유(notes)를 돌려줘요.</summary>
    public static OpenAIJudge.Verdict Judge(StageData stage, Texture2D drawing)
    {
        if (TestOverride != null) return TestOverride(stage);

        var pic = new Picture(drawing.GetPixels32(), drawing.width, drawing.height);
        int n = stage.features.Length;
        var verdict = new OpenAIJudge.Verdict { present = new bool[n], notes = new string[n] };

        if (pic.inkCount < 20)
        {
            for (int i = 0; i < n; i++) verdict.notes[i] = "쪽지에 그려진 것이 거의 없어요.";
            return verdict;
        }

        for (int i = 0; i < n; i++)
        {
            verdict.present[i] = Check(pic, stage.features[i], out string note);
            verdict.notes[i] = note;
        }
        return verdict;
    }

    // 특징 하나 계산 : Feature.check 에 따라 알맞은 함수로
    static bool Check(Picture p, Feature f, out string note)
    {
        switch (f.check)
        {
            case AutoCheck.LongHandle: return p.Bat().LongHandle(out note);
            case AutoCheck.ThickHead: return p.Bat().ThickHead(out note);
            case AutoCheck.Spikes: return p.Bat().Spikes(out note);
            case AutoCheck.RectOutline: return RectOutline(p, out note);
            case AutoCheck.HandleOnTop: return HandleOnTop(p, out note);
            case AutoCheck.InnerPocket: return InnerPocket(p, out note);
            case AutoCheck.PocketStripes: return PocketStripes(p, out note);
            case AutoCheck.CrossLines: return CrossLines(p, out note);
            case AutoCheck.ColorAt: return ColorAt(p, f.area, f.color, out note);
            case AutoCheck.RoundBlackRing: return RoundBlackRing(p, out note);
            case AutoCheck.WhiteInside: return WhiteInside(p, out note);
            case AutoCheck.ColorInside: return ColorInside(p, f.color, out note);
            case AutoCheck.PersonShape: return PersonShape(p, out note);
            case AutoCheck.ShirtStripes: return ShirtStripes(p, out note);
            case AutoCheck.ShirtColor: return ShirtColor(p, f.color, out note);
            case AutoCheck.PantsColor: return PantsColor(p, f.color, out note);
        }
        note = "판정 방법이 정해지지 않았어요.";
        return false;
    }

    // ═════════════ 1) 칸 분류 (HSV) ═════════════

    /// <summary>픽셀 하나를 HSV 로 색 분류. 반환값 : 0 흰색, 1 검정, 2~ InkColor+1</summary>
    public static int Classify(Color32 c)
    {
        if (c.a < 128) return White;                         // 투명 = 종이
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h *= 360f;
        if (v < 0.22f) return Black;                          // 아주 어두우면 검정
        if (s < 0.25f) return v > 0.6f ? White : Black;       // 채도가 낮으면 흰색/회색/검정
        if (h < 12f || h >= 340f) return Ink(InkColor.Red);
        if (h < 45f) return v < 0.65f ? Ink(InkColor.Brown) : Ink(InkColor.Orange); // 같은 색상이라도 어두우면 갈색
        if (h < 72f) return Ink(InkColor.Yellow);
        if (h < 170f) return Ink(InkColor.Green);
        if (h < 255f) return Ink(InkColor.Blue);
        return Ink(InkColor.Purple);
    }

    static int Ink(InkColor c) => (int)c + 1;
    static readonly string[] ColorNames = { "검정", "빨강", "노랑", "파랑", "초록", "주황", "보라", "갈색" };
    static string Name(int cls) => cls <= 0 ? "흰색" : ColorNames[cls - 1];
    static string Name(InkColor c) => ColorNames[(int)c];
    static bool Chromatic(int cls) => cls >= 2;   // 검정·흰색이 아닌 "색"

    // ═════════════ 2) 그림 정보 + 모양 계산 도구 ═════════════

    /// <summary>판정용으로 줄인 그림 한 장</summary>
    class Picture
    {
        public readonly int[] cls = new int[N * N];     // 칸 색 분류
        public readonly float[] val = new float[N * N]; // 칸 밝기(V) 평균
        public readonly bool[] ink;                     // 흰색이 아닌 칸
        public readonly bool[] black;                   // 검정 칸
        public readonly int inkCount;
        public readonly Box inkBox;                     // 그림 전체가 들어가는 네모
        public readonly bool[] silhouette;              // 잉크 + 선으로 닫힌 안쪽 (속을 채운 모양)
        public readonly List<Blob> inkHoles;            // 잉크 선으로 닫힌 안쪽 영역들 (큰 것부터, 틈 메운 선 기준)
        public readonly List<Blob> rawHoles;            // 같은 것, 원래 선 기준 (틈이 있으면 안 닫혀요)

        BatShape bat;

        public Picture(Color32[] px, int w, int h)
        {
            int bw = w / N, bh = h / N;
            var counts = new int[10];
            for (int cy = 0; cy < N; cy++)
            for (int cx = 0; cx < N; cx++)
            {
                Array.Clear(counts, 0, counts.Length);
                float vSum = 0f;
                for (int y = cy * bh; y < (cy + 1) * bh; y++)
                for (int x = cx * bw; x < (cx + 1) * bw; x++)
                {
                    Color32 c = px[y * w + x];
                    counts[Classify(c)]++;
                    Color.RGBToHSV(c.a < 128 ? (Color)Color.white : (Color)c, out _, out _, out float v);
                    vSum += v;
                }
                // 4x4 중 2픽셀 이상이 색이면 그 칸은 "가장 많은 색"
                int best = White, bestCount = 0, nonWhite = 0;
                for (int k = 1; k < counts.Length; k++)
                {
                    nonWhite += counts[k];
                    if (counts[k] > bestCount) { best = k; bestCount = counts[k]; }
                }
                int i = cy * N + cx;
                cls[i] = nonWhite >= 2 ? best : White;
                val[i] = vSum / (bw * bh);
            }

            ink = Mask(i => cls[i] != White);
            black = Mask(i => cls[i] == Black);
            foreach (bool b in ink) if (b) inkCount++;
            inkBox = Box.Of(ink);
            // 손으로 그린 선은 조금씩 끊겨 있어서, 틈을 메운 선(CloseGaps)으로 닫힌 안쪽을 찾아요
            inkHoles = Holes(CloseGaps(ink), out silhouette);
            rawHoles = Holes(ink, out _);
        }

        public BatShape Bat() => bat ??= new BatShape(this);

        public bool[] Mask(Func<int, bool> test)
        {
            var m = new bool[N * N];
            for (int i = 0; i < m.Length; i++) m[i] = test(i);
            return m;
        }
    }

    /// <summary>네모 범위 (칸 좌표, 양 끝 포함)</summary>
    struct Box
    {
        public int xMin, xMax, yMin, yMax;
        public bool valid;
        public int W => xMax - xMin + 1;
        public int H => yMax - yMin + 1;
        public int Area => W * H;
        public float CX => (xMin + xMax) * 0.5f;
        public float CY => (yMin + yMax) * 0.5f;

        public static Box Of(bool[] mask)
        {
            var b = new Box { xMin = N, yMin = N, xMax = -1, yMax = -1 };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                if (!mask[y * N + x]) continue;
                b.xMin = Math.Min(b.xMin, x); b.xMax = Math.Max(b.xMax, x);
                b.yMin = Math.Min(b.yMin, y); b.yMax = Math.Max(b.yMax, y);
            }
            b.valid = b.xMax >= 0;
            return b;
        }

        public Box Shrink(int d) => new Box { xMin = xMin + d, xMax = xMax - d, yMin = yMin + d, yMax = yMax - d, valid = valid && W > 2 * d && H > 2 * d };
        public bool Contains(int x, int y) => x >= xMin && x <= xMax && y >= yMin && y <= yMax;
    }

    /// <summary>서로 이어진 칸 덩어리</summary>
    class Blob
    {
        public readonly List<int> cells = new List<int>();
        public Box box;
        public int Count => cells.Count;
    }

    // 이어진 덩어리 찾기 (diagonal = 대각선도 이어진 걸로 볼지)
    static List<Blob> Blobs(bool[] mask, bool diagonal)
    {
        var seen = new bool[N * N];
        var list = new List<Blob>();
        var stack = new Stack<int>();
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start]) continue;
            var blob = new Blob();
            var bm = new bool[N * N];
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                blob.cells.Add(i);
                bm[i] = true;
                int x = i % N, y = i / N;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!diagonal && dx != 0 && dy != 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                    int j = ny * N + nx;
                    if (mask[j] && !seen[j]) { seen[j] = true; stack.Push(j); }
                }
            }
            blob.box = Box.Of(bm);
            list.Add(blob);
        }
        list.Sort((a, b) => b.Count.CompareTo(a.Count)); // 큰 덩어리부터
        return list;
    }

    // 선(wall)으로 닫힌 안쪽 영역 찾기 : 바깥 테두리에서 물을 부어서 안 닿는 빈 칸 = 구멍
    static List<Blob> Holes(bool[] wall, out bool[] filled)
    {
        var outside = new bool[N * N];
        var stack = new Stack<int>();
        for (int k = 0; k < N; k++)
        {
            foreach (int i in new[] { k, (N - 1) * N + k, k * N, k * N + N - 1 })
            {
                if (!wall[i] && !outside[i]) { outside[i] = true; stack.Push(i); }
            }
        }
        while (stack.Count > 0)
        {
            int i = stack.Pop();
            int x = i % N, y = i / N;
            Visit(x + 1, y); Visit(x - 1, y); Visit(x, y + 1); Visit(x, y - 1);
        }
        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= N || y >= N) return;
            int j = y * N + x;
            if (wall[j] || outside[j]) return;
            outside[j] = true;
            stack.Push(j);
        }

        var hole = new bool[N * N];
        filled = new bool[N * N];
        for (int i = 0; i < hole.Length; i++)
        {
            hole[i] = !wall[i] && !outside[i];
            filled[i] = wall[i] || hole[i];
        }
        return Blobs(hole, false);
    }

    // 틈 메우기 (닫힘 연산) : 2칸 두껍게 → 2칸 얇게. 약 16픽셀까지 끊긴 선이 이어지고, 선 굵기는 거의 그대로예요.
    static bool[] CloseGaps(bool[] mask) => Shrink(Grow(mask, 2), 2);

    // 알맹이만 남기기 (열림 연산) : 2칸 얇게 → 2칸 두껍게. 펜으로 그은 가는 선(돌기·손잡이 선)은 사라지고 속이 찬 부분만 남아요.
    static bool[] Core(bool[] mask) => Grow(Shrink(mask, 2), 2);

    // r 칸만큼 두껍게
    static bool[] Grow(bool[] mask, int r)
    {
        var t = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            if (!mask[y * N + x]) continue;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < N && ny < N) t[ny * N + nx] = true;
            }
        }
        return t;
    }

    // r 칸만큼 얇게 (주변 r 칸이 전부 칠해진 칸만 남김)
    static bool[] Shrink(bool[] mask, int r)
    {
        var t = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            bool all = true;
            for (int dy = -r; dy <= r && all; dy++)
            for (int dx = -r; dx <= r && all; dx++)
            {
                int nx = x + dx, ny = y + dy;
                all = nx >= 0 && ny >= 0 && nx < N && ny < N && mask[ny * N + nx];
            }
            t[y * N + x] = all;
        }
        return t;
    }

    // 칸 마스크를 상하좌우로 1칸씩 두껍게 (작은 틈 메우기)
    static bool[] Thicken(bool[] mask)
    {
        var t = new bool[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            if (!mask[y * N + x]) continue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < N && ny < N) t[ny * N + nx] = true;
            }
        }
        return t;
    }

    static int CountIn(bool[] mask, Box b)
    {
        int n = 0;
        for (int y = Math.Max(0, b.yMin); y <= Math.Min(N - 1, b.yMax); y++)
        for (int x = Math.Max(0, b.xMin); x <= Math.Min(N - 1, b.xMax); x++)
            if (mask[y * N + x]) n++;
        return n;
    }

    // 네모 안에서 가장 많은 "색"(검정·흰색 제외)과 그 개수
    static int DominantColor(Picture p, Box b, out int count, out int chromaticTotal)
    {
        var counts = new int[10];
        chromaticTotal = 0;
        for (int y = Math.Max(0, b.yMin); y <= Math.Min(N - 1, b.yMax); y++)
        for (int x = Math.Max(0, b.xMin); x <= Math.Min(N - 1, b.xMax); x++)
        {
            int c = p.cls[y * N + x];
            if (!Chromatic(c)) continue;
            counts[c]++;
            chromaticTotal++;
        }
        int best = White;
        count = 0;
        for (int k = 2; k < counts.Length; k++) if (counts[k] > count) { best = k; count = counts[k]; }
        return best;
    }

    // 네모의 네 모서리가 얼마나 채워져 있는지 (네모 모양이면 4개 다 채워짐, 동그라미면 비어 있음)
    static int FilledCorners(bool[] shape, Box b)
    {
        int s = Math.Max(2, (int)(Math.Min(b.W, b.H) * 0.18f));
        Box[] corners =
        {
            new Box { xMin = b.xMin, xMax = b.xMin + s - 1, yMin = b.yMin, yMax = b.yMin + s - 1 },
            new Box { xMin = b.xMax - s + 1, xMax = b.xMax, yMin = b.yMin, yMax = b.yMin + s - 1 },
            new Box { xMin = b.xMin, xMax = b.xMin + s - 1, yMin = b.yMax - s + 1, yMax = b.yMax },
            new Box { xMin = b.xMax - s + 1, xMax = b.xMax, yMin = b.yMax - s + 1, yMax = b.yMax },
        };
        int filled = 0;
        foreach (Box c in corners) if (CountIn(shape, c) >= s * s * 0.5f) filled++;
        return filled;
    }

    // 몸체 네모
    //   - 기본 : 속을 채운 모양 중 가장 큰 덩어리의 네모 (돗자리처럼 안이 칸으로 나뉘어도 전체 외곽)
    //   - 가방처럼 위에 손잡이가 붙어 있으면, 가장 큰 닫힌 안쪽(몸체 안)이 전체의 절반 이상 → 그걸 몸체로
    static Box BodyBox(Picture p, out bool fromHole)
    {
        List<Blob> blobs = Blobs(p.silhouette, true);
        Box whole = blobs.Count > 0 ? blobs[0].box : p.inkBox;
        if (p.inkHoles.Count > 0 && p.inkHoles[0].box.Area >= whole.Area * 0.5f)
        {
            fromHole = true;
            return p.inkHoles[0].box;
        }
        fromHole = false;
        return whole;
    }

    // 네모 안에서 "가로로 길게 그은 줄"이 몇 줄인지 (줄무늬 세기)
    static int CountHorizontalLines(Picture p, Box r, float minRunRatio)
    {
        if (!r.valid) return 0;
        int lines = 0, thick = 0;
        for (int y = r.yMin; y <= r.yMax + 1; y++)
        {
            bool isLine = false;
            if (y <= r.yMax)
            {
                int run = 0, best = 0;
                for (int x = r.xMin; x <= r.xMax; x++)
                {
                    run = p.ink[y * N + x] ? run + 1 : 0;
                    best = Math.Max(best, run);
                }
                isLine = best >= r.W * minRunRatio;
            }
            if (isLine) thick++;
            else
            {
                if (thick > 0 && thick <= Math.Max(2, r.H * 0.3f)) lines++; // 너무 두꺼우면 줄이 아니라 칠한 면
                thick = 0;
            }
        }
        return lines;
    }

    // ═════════════ 3) 특징별 계산 ═════════════

    // ── 방망이 : 길쭉한 방향(주축)을 따라 16토막으로 나눠 굵기를 재요 ──
    class BatShape
    {
        const int Bins = 16;
        readonly float[] thickness = new float[Bins];      // 토막별 굵기 (돌기·선 포함)
        readonly float[] coreThickness = new float[Bins];  // 토막별 "알맹이" 굵기 (가는 선을 지운 뒤)
        readonly float[] spreadMin = new float[Bins], spreadMax = new float[Bins];   // 토막 안 점들이 옆으로 퍼진 범위
        readonly float length, maxThick;
        readonly int handleFrom = -1, handleTo = -1;       // 손잡이 토막 구간
        readonly bool wholeThin;                           // 전체가 가늘고 긴 막대 (머리 없이 손잡이만)
        readonly bool headAtLow;                           // 머리가 축의 앞쪽(낮은 값)에 있는지
        readonly Vector2 mean, axis;
        readonly float aMin, binLen;
        readonly List<Vector2> pts = new List<Vector2>(), corePts = new List<Vector2>();

        int BinOf(Vector2 v) => Mathf.Clamp((int)((Vector2.Dot(v - mean, axis) - aMin) / binLen), 0, Bins - 1);

        public BatShape(Picture p)
        {
            bool[] core = Core(p.silhouette);
            for (int i = 0; i < N * N; i++)
            {
                if (p.silhouette[i]) pts.Add(new Vector2(i % N, i / N));
                if (core[i]) corePts.Add(new Vector2(i % N, i / N));
            }
            foreach (Vector2 v in pts) mean += v;
            mean /= Mathf.Max(1, pts.Count);

            // 주축 = 점들이 가장 길게 퍼진 방향 (공분산으로 구함)
            float cxx = 0, cyy = 0, cxy = 0;
            foreach (Vector2 v in pts) { Vector2 d = v - mean; cxx += d.x * d.x; cyy += d.y * d.y; cxy += d.x * d.y; }
            float angle = 0.5f * Mathf.Atan2(2 * cxy, cxx - cyy);
            axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            aMin = float.MaxValue;
            float aMax = float.MinValue;
            foreach (Vector2 v in pts) { float a = Vector2.Dot(v - mean, axis); aMin = Mathf.Min(aMin, a); aMax = Mathf.Max(aMax, a); }
            length = aMax - aMin + 1;
            binLen = length / Bins;
            Vector2 side = new Vector2(-axis.y, axis.x);                       // 주축에 수직인 방향
            for (int k = 0; k < Bins; k++) { spreadMin[k] = float.MaxValue; spreadMax[k] = float.MinValue; }
            foreach (Vector2 v in pts)
            {
                int k = BinOf(v);
                thickness[k] += 1f / binLen;                                    // 토막 안 칸 수 ÷ 토막 길이 = 평균 굵기
                float b = Vector2.Dot(v - mean, side);
                spreadMin[k] = Mathf.Min(spreadMin[k], b); spreadMax[k] = Mathf.Max(spreadMax[k], b);
            }
            foreach (Vector2 v in corePts) coreThickness[BinOf(v)] += 1f / binLen;

            // 머리 = 알맹이가 가장 굵은 쪽 (알맹이가 없으면 그냥 가장 굵은 쪽)
            int kMax = 0, kCore = 0;
            for (int k = 0; k < Bins; k++)
            {
                if (thickness[k] > thickness[kMax]) kMax = k;
                if (coreThickness[k] > coreThickness[kCore]) kCore = k;
            }
            maxThick = thickness[kMax];
            headAtLow = (coreThickness[kCore] > 0 ? kCore : kMax) < Bins / 2;

            // 머리 반대쪽 끝에서부터 머리 쪽으로 가면서 "가는 토막"이 이어지는 구간 = 손잡이
            //   가늘다 = 가장 굵은 곳의 45% 이하 (동그라미 가장자리처럼 점점 굵어지는 곳은 금방 넘어서요)
            int end = headAtLow ? Bins - 1 : 0;     // 손잡이 끝 토막
            int step = headAtLow ? -1 : 1;          // 머리 쪽으로 한 칸
            int k2 = end;
            //   한 가닥 = 옆으로 퍼진 폭이 굵기의 2.5배 이하 (돌기처럼 여러 선이 흩어져 있으면 손잡이가 아님)
            while (k2 >= 0 && k2 < Bins && thickness[k2] > 0 && thickness[k2] <= maxThick * 0.45f
                   && spreadMax[k2] - spreadMin[k2] + 1 <= Mathf.Max(3f, thickness[k2] * 2.5f)) k2 += step;
            if (Math.Abs(k2 - end) >= 4) { handleFrom = end; handleTo = k2 - step; }

            // 굵은 곳이 없이 전체가 가늘고 길면(길이가 굵기의 4배 이상) 그 자체가 손잡이
            wholeThin = handleFrom < 0 && length >= maxThick * 4f;
        }

        bool HasHandle => wholeThin || (handleFrom >= 0 && length >= maxThick * 1.2f);

        public bool LongHandle(out string note)
        {
            if (HasHandle) { note = "가늘고 길게 뻗은 손잡이가 있어요."; return true; }
            note = length < maxThick * 1.2f ? "전체 모양이 길쭉하지 않아요." : "가늘고 긴 손잡이 부분이 보이지 않아요.";
            return false;
        }

        public bool ThickHead(out string note)
        {
            if (!HasHandle) { note = "손잡이와 이어진 머리를 찾지 못했어요."; return false; }

            // 손잡이 굵기 = 손잡이 토막 굵기의 가운데 값 (막대 전체가 손잡이면 전체의 가운데 값)
            var hs = new List<float>();
            if (wholeThin) { foreach (float t in thickness) if (t > 0) hs.Add(t); }
            else for (int k = Math.Min(handleFrom, handleTo); k <= Math.Max(handleFrom, handleTo); k++) hs.Add(thickness[k]);
            hs.Sort();
            float handle = Mathf.Max(0.5f, hs[hs.Count / 2]);

            // 머리 굵기 = 가는 선(돌기)을 지운 "알맹이"의 가장 굵은 곳 → 돌기 때문에 굵어 보이는 걸 막아요
            float head = 0f;
            foreach (float t in coreThickness) head = Mathf.Max(head, t);
            float ratio = head / handle;
            if (ratio >= 1.8f) { note = $"머리가 손잡이보다 약 {ratio:0.0}배 굵어요."; return true; }
            note = head <= 0f ? "손잡이보다 굵은 머리가 보이지 않아요." : $"머리가 손잡이보다 별로 굵지 않아요 (약 {ratio:0.0}배).";
            return false;
        }

        bool HeadSide(Vector2 v)
        {
            if (!HasHandle || wholeThin) return true;          // 손잡이가 없으면 전부 머리로 봐요
            int k = BinOf(v);
            return headAtLow ? k < Bins * 0.55f : k >= Bins * 0.45f;
        }

        public bool Spikes(out string note)
        {
            // 머리의 "알맹이"(속이 찬 부분) 둘레보다 바깥으로 튀어나온 방향 = 돌기
            var head = new List<Vector2>();
            foreach (Vector2 v in pts) if (HeadSide(v)) head.Add(v);
            var coreHead = new List<Vector2>();
            foreach (Vector2 v in corePts) if (HeadSide(v)) coreHead.Add(v);
            if (head.Count < 30) { note = "머리 부분을 찾지 못했어요."; return false; }

            bool useCore = coreHead.Count >= 30;
            Vector2 c = Vector2.zero;
            foreach (Vector2 v in useCore ? coreHead : head) c += v;
            c /= useCore ? coreHead.Count : head.Count;

            const int A = 36;                      // 10도씩 36방향
            var rFull = new float[A];
            var rCore = new float[A];
            int Dir(Vector2 d) => ((int)(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 360f) / 10) % A;
            foreach (Vector2 v in head) { Vector2 d = v - c; int a = Dir(d); rFull[a] = Mathf.Max(rFull[a], d.magnitude); }
            foreach (Vector2 v in coreHead) { Vector2 d = v - c; int a = Dir(d); rCore[a] = Mathf.Max(rCore[a], d.magnitude); }

            var sorted = (float[])(useCore ? rCore : rFull).Clone();
            Array.Sort(sorted);
            float median = sorted[A / 2];

            Vector2 toHandle = headAtLow ? axis : -axis;
            float handleAngle = Mathf.Atan2(toHandle.y, toHandle.x) * Mathf.Rad2Deg;
            bool skipHandle = HasHandle && !wholeThin;
            float axisAngle = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            var peak = new bool[A];
            for (int a = 0; a < A; a++)
            {
                float deg = a * 10f + 5f;
                bool nearHandle = skipHandle && Mathf.Abs(Mathf.DeltaAngle(deg, handleAngle)) < 45f;
                // 막대 모양이면 막대의 양 끝(위·아래)은 원래 멀어서 돌기로 세지 않아요
                bool alongStick = wholeThin && (Mathf.Abs(Mathf.DeltaAngle(deg, axisAngle)) < 45f || Mathf.Abs(Mathf.DeltaAngle(deg, axisAngle + 180f)) < 45f);
                if (nearHandle || alongStick) continue;
                peak[a] = useCore
                    ? rFull[a] - rCore[a] > Mathf.Max(3f, median * 0.15f)   // 알맹이보다 확실히 튀어나옴
                    : rFull[a] > median * 1.18f;                            // (알맹이가 없을 때) 평소보다 18% 이상 멂
            }
            int spikes = 0;
            for (int a = 0; a < A; a++) if (peak[a] && !peak[(a + A - 1) % A]) spikes++;
            if (spikes == 0 && peak[0]) spikes = 1; // 한 바퀴 전부 튀어나온 경우

            if (spikes >= 2) { note = $"머리 둘레에 뾰족한 돌기가 {spikes}개 있어요."; return true; }
            note = spikes == 1 ? "머리의 돌기가 1개뿐이에요." : "머리 둘레가 매끈해서 돌기가 보이지 않아요.";
            return false;
        }
    }

    // ── 가방 · 돗자리 : 네모 외곽 ──
    static bool RectOutline(Picture p, out string note)
    {
        Box b = BodyBox(p, out _);
        if (b.W < 6 || b.H < 6) { note = "외곽 모양을 찾지 못했어요."; return false; }
        int corners = FilledCorners(p.silhouette, b);
        if (corners >= 3) { note = "네 모서리가 있는 네모 외곽이에요."; return true; }
        note = "외곽이 네모가 아니라 둥글거나 다른 모양이에요.";
        return false;
    }

    // ── 가방 : 몸체 위 손잡이 ──
    static bool HandleOnTop(Picture p, out string note)
    {
        Box b = BodyBox(p, out bool fromHole);
        int margin = fromHole ? 7 : 2;                         // 몸체 윗선 두께는 빼고 그 위만 봐요
        var above = new Box { xMin = b.xMin - b.W / 10, xMax = b.xMax + b.W / 10, yMin = b.yMax + margin, yMax = N - 1, valid = true };
        int n = CountIn(p.ink, above);
        if (n >= 12) { note = "몸체 위쪽에 손잡이가 그려져 있어요."; return true; }
        note = "몸체 위쪽에 손잡이가 보이지 않아요.";
        return false;
    }

    // 몸체 안에 따로 닫힌 작은 영역들 = 주머니 안쪽
    //   - 원래 선 기준으로 먼저 찾고(틈 메우기를 하면 줄무늬와 주머니 테두리 사이가 메워져 주머니가 작아 보여요),
    //     없으면 틈 메운 선 기준으로 찾아요.
    //   - 주머니가 몸체 아래쪽에 붙어 있으면 몸체 안쪽 영역이 잘려 보일 수 있어서, "가운데가 몸체 안"이면 인정해요.
    static bool FindPocket(Picture p, out Box pocket)
    {
        pocket = default;
        Box body = BodyBox(p, out bool fromHole);
        if (!fromHole) return false;
        Box near = body.Shrink(-4);   // 몸체보다 4칸 넓게
        foreach (List<Blob> holes in new[] { p.rawHoles, p.inkHoles })
        {
            var inner = new Box { xMin = N, yMin = N, xMax = -1, yMax = -1 };
            foreach (Blob h in holes)
            {
                if (h.box.xMin == body.xMin && h.box.xMax == body.xMax && h.box.yMin == body.yMin && h.box.yMax == body.yMax) continue; // 몸체 자신
                if (h.Count < body.Area * 0.01f) continue;
                if (h.box.Area >= body.Area * 0.85f) continue;                                  // 몸체만 한 것은 주머니가 아님
                if (!near.Contains((int)h.box.CX, (int)h.box.CY)) continue;                     // 가운데가 몸체 안
                if (!near.Contains(h.box.xMin, h.box.yMin) || !near.Contains(h.box.xMax, h.box.yMax)) continue;
                inner.xMin = Math.Min(inner.xMin, h.box.xMin); inner.xMax = Math.Max(inner.xMax, h.box.xMax);
                inner.yMin = Math.Min(inner.yMin, h.box.yMin); inner.yMax = Math.Max(inner.yMax, h.box.yMax);
            }
            inner.valid = inner.xMax >= 0;
            if (inner.valid && inner.Area >= body.Area * 0.05f) { pocket = inner; return true; }
        }
        return false;
    }

    static bool InnerPocket(Picture p, out string note)
    {
        if (FindPocket(p, out _)) { note = "몸체 안에 닫힌 네모 주머니가 있어요."; return true; }
        note = "몸체 안에 따로 닫힌 주머니가 보이지 않아요.";
        return false;
    }

    static bool PocketStripes(Picture p, out string note)
    {
        // 기획서 기준 : "주머니 외곽 안에" 가로줄이 있어야 해요. 주머니 밖에만 있는 줄은 인정하지 않아요.
        if (!FindPocket(p, out Box r)) { note = "주머니가 없어서 주머니 안 줄무늬도 없어요."; return false; }
        int lines = CountHorizontalLines(p, r, 0.45f);
        if (lines >= 2) { note = $"주머니 안에 가로 줄이 {lines}줄 있어요."; return true; }
        note = lines == 1 ? "가로 줄이 1줄뿐이에요." : "주머니 안에 가로 줄무늬가 보이지 않아요.";
        return false;
    }

    // ── 돗자리 : 십자선 ──
    static bool CrossLines(Picture p, out string note)
    {
        Box o = BodyBox(p, out _);
        if (o.W < 8 || o.H < 8) { note = "돗자리 모양을 찾지 못했어요."; return false; }
        FindCross(p, o, out int bestCol, out int bestRow, out float colRatio, out float rowRatio);
        bool v = colRatio >= 0.55f, h = rowRatio >= 0.55f;
        if (v && h) { note = "가로선과 세로선이 엇갈려 네 칸으로 나뉘어요."; return true; }
        note = !v && !h ? "안쪽을 나누는 선이 없어요." : (v ? "세로선만 있고 가로선이 없어요." : "가로선만 있고 세로선이 없어요.");
        return false;
    }

    // 가운데 50% 안에서 검정이 가장 길게 이어진 세로줄·가로줄
    static void FindCross(Picture p, Box o, out int bestCol, out int bestRow, out float colRatio, out float rowRatio)
    {
        bestCol = (int)o.CX; bestRow = (int)o.CY;
        int bestColN = 0, bestRowN = 0;
        const int Wobble = 2;   // 삐뚤어도 봐주는 폭 (칸)
        for (int x = o.xMin + o.W / 4; x <= o.xMax - o.W / 4; x++)
        {
            int n = 0;
            for (int y = o.yMin; y <= o.yMax; y++)
            {
                for (int dx = -Wobble; dx <= Wobble; dx++)
                {
                    int xx = x + dx;
                    if (xx >= 0 && xx < N && p.black[y * N + xx]) { n++; break; }
                }
            }
            if (n > bestColN) { bestColN = n; bestCol = x; }
        }
        for (int y = o.yMin + o.H / 4; y <= o.yMax - o.H / 4; y++)
        {
            int n = 0;
            for (int x = o.xMin; x <= o.xMax; x++)
            {
                for (int dy = -Wobble; dy <= Wobble; dy++)
                {
                    int yy = y + dy;
                    if (yy >= 0 && yy < N && p.black[yy * N + x]) { n++; break; }
                }
            }
            if (n > bestRowN) { bestRowN = n; bestRow = y; }
        }
        colRatio = bestColN / (float)o.H;
        rowRatio = bestRowN / (float)o.W;
    }

    // ── 돗자리 : 칸별 색 ──
    static bool ColorAt(Picture p, Area area, InkColor color, out string note)
    {
        Box o = BodyBox(p, out _);
        FindCross(p, o, out int cx, out int cy, out float cr, out float rr);
        if (cr < 0.4f) cx = (int)o.CX;   // 선이 없으면 가운데로 나눠요
        if (rr < 0.4f) cy = (int)o.CY;
        var tl = new Box { xMin = o.xMin, xMax = cx - 1, yMin = cy + 1, yMax = o.yMax, valid = true };
        var tr = new Box { xMin = cx + 1, xMax = o.xMax, yMin = cy + 1, yMax = o.yMax, valid = true };
        var bl = new Box { xMin = o.xMin, xMax = cx - 1, yMin = o.yMin, yMax = cy - 1, valid = true };
        var br = new Box { xMin = cx + 1, xMax = o.xMax, yMin = o.yMin, yMax = cy - 1, valid = true };

        bool QuadIs(Box q, out string got)
        {
            int dom = DominantColor(p, q, out int cnt, out int total);
            got = total == 0 ? "칠하지 않음" : Name(dom);
            return dom == Ink(color) && cnt >= q.Area * 0.03f && cnt >= total * 0.5f;
        }

        string target = Name(color);
        if (area == Area.BottomBoth)
        {
            bool a = QuadIs(bl, out string ga), b = QuadIs(br, out string gb);
            if (a && b) { note = $"아래 두 칸이 모두 {target}이에요."; return true; }
            note = $"아래 칸이 {ga} / {gb} 예요. ({target}이어야 해요)";
            return false;
        }
        string where = area == Area.TopLeft ? "왼쪽 위" : "오른쪽 위";
        if (QuadIs(area == Area.TopLeft ? tl : tr, out string g)) { note = $"{where} 칸이 {target}이에요."; return true; }
        note = $"{where} 칸이 {g}이에요. ({target}이어야 해요)";
        return false;
    }

    // ── 김밥 : 둥근 검은 테두리와 그 안쪽 ──
    static bool BlackRing(Picture p, out Blob inside)
    {
        List<Blob> holes = Holes(CloseGaps(p.black), out bool[] filled);   // 끊긴 테두리도 닫힌 걸로
        Box blackBox = Box.Of(p.black);
        inside = holes.Count > 0 && blackBox.valid && holes[0].Count >= blackBox.Area * 0.15f ? holes[0] : null;
        if (inside == null) return false;
        var insideMask = new bool[N * N];
        foreach (int i in inside.cells) insideMask[i] = true;
        return FilledCorners(insideMask, inside.box) <= 1;   // 안쪽 영역의 모서리가 비어 있으면 둥근 모양
    }

    static bool RoundBlackRing(Picture p, out string note)
    {
        if (BlackRing(p, out Blob inside)) { note = "가장 바깥에 둥근 검은 테두리가 있어요."; return true; }
        note = inside == null ? "검은 테두리가 닫혀 있지 않거나 없어요." : "검은 테두리가 둥글지 않아요.";
        return false;
    }

    // 테두리 안쪽 영역 (테두리가 없으면 그림 전체 네모)
    static List<int> InsideCells(Picture p)
    {
        BlackRing(p, out Blob inside);
        if (inside != null) return inside.cells;
        // 검은 테두리가 없으면 : 속을 채운 모양 중 가장 큰 덩어리 (다른 색 테두리 안쪽)
        List<Blob> blobs = Blobs(p.silhouette, true);
        if (blobs.Count > 0) return blobs[0].cells;
        var list = new List<int>();
        for (int y = p.inkBox.yMin; y <= p.inkBox.yMax; y++)
        for (int x = p.inkBox.xMin; x <= p.inkBox.xMax; x++) list.Add(y * N + x);
        return list;
    }

    static bool WhiteInside(Picture p, out string note)
    {
        List<int> cells = InsideCells(p);
        int white = 0;
        foreach (int i in cells) if (p.cls[i] == White) white++;
        float ratio = white / (float)Mathf.Max(1, cells.Count);
        if (ratio >= 0.35f) { note = $"테두리 안쪽의 {ratio * 100:0}%가 흰 밥이에요."; return true; }
        note = "테두리 안쪽이 흰색이 아니라 다른 색으로 칠해져 있어요.";
        return false;
    }

    static bool ColorInside(Picture p, InkColor color, out string note)
    {
        List<int> cells = InsideCells(p);
        int n = 0;
        foreach (int i in cells) if (p.cls[i] == Ink(color)) n++;
        string target = Name(color);
        if (n >= Mathf.Max(6, cells.Count * 0.005f)) { note = $"안쪽에 {target} 재료가 있어요."; return true; }
        note = $"안쪽에 {target} 재료가 보이지 않아요.";
        return false;
    }

    // ── 친구 : 사람 모양 · 상의 · 하의 ──
    static bool PersonShape(Picture p, out string note)
    {
        // 사람 = 세로로 길고, 맨 아래에 따로 떨어진 두 다리(또는 두 발)가 있어요.
        // (머리가 큰 귀여운 그림체도 있어서 머리 크기는 보지 않아요)
        Box b = p.inkBox;
        float aspect = b.H / (float)b.W;

        int legRows = 0, rows = 0;
        for (int y = b.yMin; y <= b.yMin + Math.Max(1, b.H * 12 / 100); y++)
        {
            int runs = 0;
            bool prev = false;
            for (int x = b.xMin; x <= b.xMax; x++)
            {
                bool cur = p.ink[y * N + x];
                if (cur && !prev) runs++;
                prev = cur;
            }
            rows++;
            if (runs >= 2) legRows++;
        }

        bool tall = aspect >= 1.1f;
        bool legs = legRows >= rows * 0.5f;
        if (tall && legs) { note = "세로로 서 있고 두 다리가 있는 사람 모습이에요."; return true; }
        note = !tall ? "사람처럼 세로로 긴 모양이 아니에요." : "두 다리가 보이지 않아요.";
        return false;
    }

    // 같은 색끼리만 이어진 덩어리들 (초록 상의와 갈색 바지가 붙어 있어도 따로 나눠요)
    static List<Blob> ColorBlobs(Picture p)
    {
        var all = new List<Blob>();
        for (int c = 2; c < 10; c++)
        {
            int cc = c;
            all.AddRange(Blobs(p.Mask(i => p.cls[i] == cc), true));
        }
        all.Sort((a, b) => b.Count.CompareTo(a.Count));
        return all;
    }

    // 상의 = 높이 35%~75% 사이에 가운데가 있는 색 덩어리 중 가장 넓은 것 (소매까지 있어서 제일 넓어요)
    static bool FindShirt(Picture p, out Blob shirt)
    {
        Box b = p.inkBox;
        shirt = null;
        foreach (Blob blob in ColorBlobs(p))
        {
            if (blob.Count < b.Area * 0.02f) break;              // 큰 것부터라 이후는 더 작음
            float fromTop = (b.yMax - blob.box.CY) / b.H;         // 위에서부터 몇 % 지점인지
            if (fromTop < 0.35f || fromTop > 0.75f) continue;
            if (shirt == null || blob.box.W > shirt.box.W) shirt = blob;
        }
        return shirt != null;
    }

    // 하의 = 상의보다 아래에 있는 가장 큰 색 덩어리
    static bool FindPants(Picture p, Blob shirt, out Blob pants)
    {
        Box b = p.inkBox;
        pants = null;
        float limit = shirt != null ? shirt.box.CY : b.yMin + b.H * 0.4f;
        foreach (Blob blob in ColorBlobs(p))
        {
            if (blob.Count < b.Area * 0.01f) break;
            if (shirt != null && blob.cells[0] == shirt.cells[0]) continue;
            if (blob.box.CY >= limit) continue;                   // 상의보다 위(또는 같은 높이)는 제외
            pants = blob;                                         // 큰 것부터 보니 처음 맞는 게 가장 큼
            break;
        }
        return pants != null;
    }

    static bool ShirtStripes(Picture p, out string note)
    {
        // 상의 안에서 "주변보다 확실히 어두운 칸"이 가로로 길게 이어진 줄을 세요.
        // (검은 선으로 그어도, 진한 색으로 칠해도 줄무늬로 인정)
        if (!FindShirt(p, out Blob shirt)) { note = "칠한 상의를 찾지 못했어요."; return false; }
        var vs = new List<float>();
        foreach (int i in shirt.cells) vs.Add(p.val[i]);
        vs.Sort();
        float dark = vs[vs.Count / 2] - 0.15f;                    // 상의 보통 밝기보다 0.15 이상 어두우면 "줄"

        Box r = shirt.box;
        int y0 = r.yMin + Math.Max(1, r.H / 10), y1 = r.yMax - Math.Max(1, r.H / 10);
        int lines = 0, thick = 0;
        for (int y = y0; y <= y1 + 1; y++)
        {
            bool isLine = false;
            if (y <= y1)
            {
                int run = 0, best = 0;
                for (int x = r.xMin; x <= r.xMax; x++)
                {
                    int i = y * N + x;
                    bool d = p.cls[i] != White && p.val[i] < dark;
                    run = d ? run + 1 : 0;
                    best = Math.Max(best, run);
                }
                isLine = best >= r.W * 0.3f;
            }
            if (isLine) thick++;
            else
            {
                if (thick > 0 && thick <= Math.Max(2, r.H * 0.3f)) lines++;
                thick = 0;
            }
        }
        if (lines >= 2) { note = $"상의에 가로 줄무늬가 {lines}줄 있어요."; return true; }
        note = lines == 1 ? "상의의 가로 줄이 1줄뿐이에요." : "상의에 가로 줄무늬가 보이지 않아요.";
        return false;
    }

    static bool ShirtColor(Picture p, InkColor color, out string note)
    {
        string target = Name(color);
        if (!FindShirt(p, out Blob shirt)) { note = "칠한 상의를 찾지 못했어요."; return false; }
        int dom = DominantColor(p, shirt.box, out _, out _);
        if (dom == Ink(color)) { note = $"상의가 {target}이에요."; return true; }
        note = $"상의가 {Name(dom)}이에요. ({target}이어야 해요)";
        return false;
    }

    static bool PantsColor(Picture p, InkColor color, out string note)
    {
        string target = Name(color);
        FindShirt(p, out Blob shirt);
        if (!FindPants(p, shirt, out Blob pants)) { note = "칠한 하의를 찾지 못했어요."; return false; }
        int dom = DominantColor(p, pants.box, out _, out _);
        if (dom == Ink(color)) { note = $"하의가 {target}이에요."; return true; }
        note = $"하의가 {Name(dom)}이에요. ({target}이어야 해요)";
        return false;
    }
}

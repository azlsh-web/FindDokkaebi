using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// HandSketch : 사람이 마우스로 그린 것 같은 "손그림"을 코드로 만드는 도구 (에디터 전용, 검사용)
// ─────────────────────────────────────────────────────────────
// - 쪽지와 같은 512x512 흰 종이에, 살짝 떨리는 선 · 살짝 안 닫힌 동그라미 · 쓱쓱 칠한 색을 그려요.
// - 자동 플레이 테스트(MvpSelfTest)와 판정 전수 검사(MvpJudgeCases)가 같이 써요.
// - 좌표는 텍스처 기준 : 왼쪽 아래가 (0,0), 오른쪽 위가 (512,512)
class HandSketch
{
    public const int Size = 512;
    public static readonly Color32 Paper = new Color32(255, 255, 255, 255);
    public static readonly Color32 Black = new Color32(0, 0, 0, 255);

    public readonly Color32[] px = new Color32[Size * Size];

    public HandSketch()
    {
        for (int i = 0; i < px.Length; i++) px[i] = Paper;
    }

    /// <summary>점들을 따라 선 긋기. r = 펜 반지름, wobble = 손떨림 크기(픽셀)</summary>
    public HandSketch Line(Color32 c, int r, float wobble, params Vector2[] pts)
    {
        for (int k = 1; k < pts.Length; k++)
        {
            float len = Vector2.Distance(pts[k - 1], pts[k]);
            for (int t = 0; t <= len; t++)
            {
                Vector2 q = Vector2.Lerp(pts[k - 1], pts[k], t / Mathf.Max(1f, len));
                q += new Vector2(Mathf.Sin(q.y * 0.07f + k), Mathf.Cos(q.x * 0.06f + k)) * wobble;
                Dot(q, r, c);
            }
        }
        return this;
    }

    /// <summary>타원 테두리. gapDeg 만큼 비워서 살짝 안 닫힌 손그림처럼</summary>
    public HandSketch Ellipse(Color32 c, int r, Vector2 center, float rx, float ry, float gapDeg = 0f)
    {
        var pts = new List<Vector2>();
        for (float a = gapDeg; a <= 360f; a += 4f)
            pts.Add(center + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * rx, Mathf.Sin(a * Mathf.Deg2Rad) * ry));
        return Line(c, r, 1.5f, pts.ToArray());
    }

    /// <summary>네모 안을 지그재그로 쓱쓱 칠하기 (gap 이 클수록 성기게)</summary>
    public HandSketch Scribble(Color32 c, int r, float x0, float y0, float x1, float y1, float gap)
    {
        var pts = new List<Vector2>();
        bool right = true;
        for (float y = y0; y <= y1; y += gap)
        {
            pts.Add(new Vector2(right ? x0 : x1, y));
            pts.Add(new Vector2(right ? x1 : x0, y + gap * 0.5f));
            right = !right;
        }
        return Line(c, r, 2f, pts.ToArray());
    }

    /// <summary>동그라미 안을 지그재그로 꼼꼼히 칠하기</summary>
    public HandSketch ScribbleCircle(Color32 c, int r, Vector2 center, float radius, float gap)
    {
        var pts = new List<Vector2>();
        bool right = true;
        for (float y = -radius; y <= radius; y += gap)
        {
            float half = Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y));
            pts.Add(center + new Vector2(right ? -half : half, y));
            pts.Add(center + new Vector2(right ? half : -half, y + gap * 0.5f));
            right = !right;
        }
        return Line(c, r, 1f, pts.ToArray());
    }

    void Dot(Vector2 q, int r, Color32 c)
    {
        int cx = Mathf.RoundToInt(q.x), cy = Mathf.RoundToInt(q.y);
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
            if (x >= 0 && y >= 0 && x < Size && y < Size && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                px[y * Size + x] = c;
    }

    public Texture2D Tex()
    {
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        return t;
    }
}

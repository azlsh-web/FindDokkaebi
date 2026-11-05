using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// ─────────────────────────────────────────────────────────────
// ColorMixer : 혼색 팔레트 (4단계 김밥부터 등장)
// ─────────────────────────────────────────────────────────────
// 실제 팔레트처럼 물감을 한 번씩 짜 넣으면서 섞인 색을 눈으로 보고 찾아요.
//   색 섞기 버튼 → 팔레트가 열림 → 물감(빨강·노랑·파랑)을 누를 때마다 큰 동그라미로 뿌려지고 색이 천천히 바뀜
//   → 되돌리기로 마지막에 넣은 물감을 뺄 수 있음 → [섞어서 칠하기] → 만든 색이 선택됨
//   - 4단계(두 색 혼합) : 물감을 2가지까지
//   - 5단계(자유 혼합)  : 물감을 3가지까지
//
// 왜 RGB 평균을 안 쓰나요?
//   물감은 빛과 달라서, 노랑+파랑을 평균 내면 칙칙한 회색이 나와요.
//   그래서 대표 비율의 색(Anchors)을 정해 두고, 그 사이 비율은 Mix 에서 이어서 계산해요.
//   - 빨강 : 노랑 : 파랑 = 1:1:1 은 검은색에 가깝고, 2:2:1 이 갈색이에요.
public class ColorMixer : MonoBehaviour
{
    [Header("물감 버튼 (순서 고정 : 빨강, 노랑, 파랑)")]
    [SerializeField] Button[] materialButtons;

    [Header("미리보기")]
    [SerializeField] Image previewSwatch;           // 섞인 색 칸
    [SerializeField] Button undoButton;             // 마지막에 넣은 물감 빼기
    [SerializeField] Button applyButton;            // 섞어서 칠하기

    /// <summary>색을 다 섞었을 때 알려줄 곳 (ToolBar 가 등록해요)</summary>
    public Action<Color> OnMixed;

    static readonly Color EmptyColor = Color.white;

    // 물감 색은 도구 줄의 색과 같아요
    static readonly Color32 Red = ToolBar.Palette[1], Yellow = ToolBar.Palette[2], Blue = ToolBar.Palette[3];
    static readonly Color32 Orange = new Color32(245, 140, 30, 255);
    static readonly Color32 Green = new Color32(40, 160, 60, 255);
    static readonly Color32 Purple = new Color32(130, 50, 160, 255);
    static readonly Color32 Brown = new Color32(130, 80, 40, 255);
    static readonly Color32 NearBlack = new Color32(35, 30, 30, 255);

    // 세 물감이 모두 들어갔을 때 기준이 되는 색들 (빨강, 노랑, 파랑 비율 → 색)
    static readonly (Vector3 ratio, Color32 color)[] Anchors =
    {
        (new Vector3(1, 0, 0), Red), (new Vector3(0, 1, 0), Yellow), (new Vector3(0, 0, 1), Blue),
        (new Vector3(1, 1, 0), Orange), (new Vector3(0, 1, 1), Green), (new Vector3(1, 0, 1), Purple),
        (new Vector3(2, 2, 1), Brown), (new Vector3(1, 1, 1), NearBlack),
    };

    const float FlightSeconds = 0.32f;      // 물감이 큰 동그라미까지 날아가는 시간
    const float BlendSeconds = 0.9f;        // 섞인 색으로 천천히 바뀌는 시간
    const float SplashSeconds = 0.35f;      // 닿은 자리에서 튄 물방울이 사라지는 시간
    const float PopSeconds = 0.2f;          // 되돌아온 물감이 살짝 커졌다 돌아오는 시간

    readonly List<GameObject> drops = new List<GameObject>();   // 지금 화면에 있는 물방울
    Coroutine blending;

    /// <summary>지금까지 넣은 물감으로 만든 색 (화면의 색은 이 색을 향해 천천히 바뀌어요)</summary>
    public Color ResultColor => resultColor;

    /// <summary>지금 화면에 있는 물방울 수 (자동 테스트용)</summary>
    public int DropCount => drops.Count;

    readonly int[] counts = new int[3];                 // 물감별로 넣은 횟수
    readonly List<int> history = new List<int>();       // 넣은 순서 (되돌리기용)
    int maxKinds = 2;                                   // 최대 몇 가지 물감까지 넣을 수 있는지
    Color resultColor;

    /// <summary>팔레트 열기. maxKinds = 2(두 색 혼합) 또는 3(자유 혼합)</summary>
    public void Open(int maxKinds)
    {
        this.maxKinds = maxKinds;
        for (int i = 0; i < counts.Length; i++) counts[i] = 0;
        history.Clear();
        gameObject.SetActive(true);
        Refresh();
        previewSwatch.color = EmptyColor;
    }

    /// <summary>[버튼] 닫기</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>[버튼] 물감 한 번 넣기. index : 0 빨강, 1 노랑, 2 파랑</summary>
    public void AddPaint(int index)
    {
        if (IsLocked(index)) return;
        counts[index]++;
        history.Add(index);
        Refresh();
        StartCoroutine(ThrowPaint(index));
    }

    /// <summary>[버튼] 마지막에 넣은 물감 빼기</summary>
    public void UndoPaint()
    {
        if (history.Count == 0) return;
        int index = history[history.Count - 1];
        counts[index]--;
        history.RemoveAt(history.Count - 1);
        Refresh();
        StartBlend();
        StartCoroutine(ReturnPaint(index));
    }

    /// <summary>움직임을 바로 끝내요 (사진 · 자동 테스트용)</summary>
    public void SkipAnimation()
    {
        StopAllCoroutines();
        blending = null;
        ClearDrops();
        foreach (Button material in materialButtons)
            if (material != null) material.transform.parent.localScale = Vector3.one;
        if (previewSwatch != null) previewSwatch.color = resultColor;
    }

    // 팔레트가 닫히면 날아가던 물방울을 치우고 색도 끝난 상태로 둬요
    void OnDisable()
    {
        SkipAnimation();
    }

    /// <summary>[버튼] 섞어서 칠하기</summary>
    public void Apply()
    {
        if (CountKinds() < 2) return;
        OnMixed?.Invoke(resultColor);
        Close();
    }

    void Refresh()
    {
        for (int i = 0; i < materialButtons.Length; i++)
        {
            bool locked = IsLocked(i);
            materialButtons[i].interactable = !locked;
            materialButtons[i].transform.parent.GetComponent<CanvasGroup>().alpha = locked ? 0.3f : 1f;  // 못 누르는 물감은 흐리게
        }

        resultColor = history.Count > 0 ? (Color)Mix(counts[0], counts[1], counts[2]) : EmptyColor;
        undoButton.interactable = history.Count > 0;
        applyButton.interactable = CountKinds() >= 2;
    }

    // ───────────── 움직임 ─────────────

    // 누른 물감에서 물방울 여러 개가 큰 동그라미로 날아가고, 닿으면 튀면서 색이 바뀌기 시작해요
    IEnumerator ThrowPaint(int index)
    {
        Color paint = ToolBar.Palette[index + 1];
        Vector2 from = CenterOf(materialButtons[index].GetComponent<RectTransform>());
        Vector2 to = CenterOf(previewSwatch.rectTransform);

        for (int i = 0; i < 5; i++) StartCoroutine(FlyDrop(paint, from, to + Random.insideUnitCircle * 42f, i * 0.035f, i == 0 ? 34f : Random.Range(16f, 26f)));

        yield return new WaitForSecondsRealtime(FlightSeconds);
        StartBlend();
        for (int i = 0; i < 7; i++) StartCoroutine(SplashDrop(paint, to, i * Mathf.PI * 2f / 7f + Random.Range(-0.2f, 0.2f)));
    }

    // 되돌리기 : 큰 동그라미에서 물방울이 나와 원래 물감으로 돌아가고, 물감이 살짝 커졌다 돌아와요
    IEnumerator ReturnPaint(int index)
    {
        Color paint = ToolBar.Palette[index + 1];
        RectTransform material = materialButtons[index].GetComponent<RectTransform>();
        Vector2 from = CenterOf(previewSwatch.rectTransform);
        Vector2 to = CenterOf(material);

        for (int i = 0; i < 5; i++) StartCoroutine(FlyDrop(paint, from + Random.insideUnitCircle * 42f, to + Random.insideUnitCircle * 14f, i * 0.035f, i == 0 ? 34f : Random.Range(16f, 26f)));

        yield return new WaitForSecondsRealtime(FlightSeconds);
        Transform holder = material.parent;
        for (float t = 0f; t < PopSeconds; t += Time.unscaledDeltaTime)
        {
            holder.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / PopSeconds * Mathf.PI));
            yield return null;
        }
        holder.localScale = Vector3.one;
    }

    IEnumerator FlyDrop(Color paint, Vector2 from, Vector2 to, float delay, float size)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        RectTransform drop = NewDrop(paint, size);
        float arc = Random.Range(70f, 110f);
        for (float t = 0f; t < FlightSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / FlightSeconds;
            drop.localPosition = Vector2.Lerp(from, to, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * arc);
            yield return null;
        }
        RemoveDrop(drop);
    }

    IEnumerator SplashDrop(Color paint, Vector2 center, float angle)
    {
        RectTransform drop = NewDrop(paint, Random.Range(10f, 18f));
        Image image = drop.GetComponent<Image>();
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float reach = Random.Range(105f, 135f);
        for (float t = 0f; t < SplashSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / SplashSeconds;
            drop.localPosition = center + direction * Mathf.Lerp(55f, reach, 1f - (1f - k) * (1f - k));
            Color faded = paint;
            faded.a = 1f - k;
            image.color = faded;
            yield return null;
        }
        RemoveDrop(drop);
    }

    // 지금 보이는 색에서 만들어진 색으로 천천히 바꾸기
    void StartBlend()
    {
        if (blending != null) StopCoroutine(blending);
        blending = StartCoroutine(Blend());
    }

    IEnumerator Blend()
    {
        Color start = previewSwatch.color;
        for (float t = 0f; t < BlendSeconds; t += Time.unscaledDeltaTime)
        {
            previewSwatch.color = Color.Lerp(start, resultColor, Mathf.SmoothStep(0f, 1f, t / BlendSeconds));
            yield return null;
        }
        previewSwatch.color = resultColor;
        blending = null;
    }

    RectTransform NewDrop(Color paint, float size)
    {
        var go = new GameObject("PaintDrop", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        drops.Add(go);
        Image image = go.GetComponent<Image>();
        image.sprite = previewSwatch.sprite;
        image.color = paint;
        image.raycastTarget = false;     // 물방울이 버튼 누르기를 막지 않게
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);
        rt.localPosition = new Vector3(0f, 10000f, 0f);
        return rt;
    }

    void RemoveDrop(RectTransform drop)
    {
        drops.Remove(drop.gameObject);
        Destroy(drop.gameObject);
    }

    void ClearDrops()
    {
        foreach (GameObject drop in drops) Destroy(drop);
        drops.Clear();
    }

    // 버튼 · 동그라미의 가운데를 팔레트 카드 안의 위치로
    Vector2 CenterOf(RectTransform rt) => transform.InverseTransformPoint(rt.TransformPoint(rt.rect.center));

    // 넣을 수 있는 물감 가짓수를 다 채우면, 아직 안 넣은 물감은 못 눌러요
    bool IsLocked(int index) => counts[index] == 0 && CountKinds() >= maxKinds;

    int CountKinds()
    {
        int n = 0;
        foreach (int c in counts) if (c > 0) n++;
        return n;
    }

    /// <summary>물감을 넣은 횟수로 섞인 색 계산. 횟수의 비율만 결과에 영향을 줘요</summary>
    public static Color32 Mix(int red, int yellow, int blue)
    {
        int total = red + yellow + blue;
        if (total <= 0) return Color.white;

        // 두 가지만 섞으면 두 물감과 그 사이 색을 잇는 선 위에서 골라요 (어두워지지 않게)
        if (blue == 0) return Between(Red, Orange, Yellow, red, yellow);
        if (red == 0) return Between(Yellow, Green, Blue, yellow, blue);
        if (yellow == 0) return Between(Red, Purple, Blue, red, blue);

        // 세 가지를 섞으면 기준 색들을 비율이 가까운 만큼 더 많이 반영해요
        Vector3 ratio = new Vector3(red, yellow, blue) / total;
        Vector3 sum = Vector3.zero;
        float weightSum = 0f;
        foreach (var (anchorRatio, color) in Anchors)
        {
            float distance = Vector3.Distance(ratio, anchorRatio / (anchorRatio.x + anchorRatio.y + anchorRatio.z));
            if (distance < 0.000001f) return color;
            float weight = 1f / (distance * distance * distance * distance);
            weightSum += weight;
            sum += new Vector3(color.r, color.g, color.b) * weight;
        }
        sum /= weightSum;
        return new Color32((byte)Mathf.RoundToInt(sum.x), (byte)Mathf.RoundToInt(sum.y), (byte)Mathf.RoundToInt(sum.z), 255);
    }

    // first 물감 → middle(1:1) → second 물감 사이의 색. firstCount·secondCount 의 비율로 정해요
    static Color32 Between(Color32 first, Color32 middle, Color32 second, int firstCount, int secondCount)
    {
        float t = (float)secondCount / (firstCount + secondCount);
        return t < 0.5f ? Color32.Lerp(first, middle, t * 2f) : Color32.Lerp(middle, second, t * 2f - 1f);
    }
}

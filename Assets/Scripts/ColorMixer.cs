using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// ─────────────────────────────────────────────────────────────
// ColorMixer : 붓 색 (팔레트 물감 → 붓 끝)
// ─────────────────────────────────────────────────────────────
// 붓 끝 색이 곧 지금 그리는 색이에요. 팔레트에서 색을 고르면 물방울이 그 색에서 붓 끝으로 날아가
// 첨벙 튀고, 붓 끝 색이 천천히 바뀌어요.
//   - 검정 · (섞지 않는 단계의) 빨강 · 노랑 · 파랑 : 그 색 하나로 (SetPure)
//   - 섞는 단계(4 · 5단계) : 빨강 · 노랑 · 파랑 물감을 누를 때마다 하나씩 넣어져 섞여요 (AddPaint).
//     물감 옆 숫자 = 넣은 횟수, ↶ = 마지막에 넣은 물감 빼기 (물방울이 붓 끝에서 그 물감으로 돌아가요).
//     섞은 색으로 한 번 그린 뒤(MarkUsed) 물감을 누르면 새로 섞기 시작해요.
//   - 4단계(두 색 혼합) : 물감을 2가지까지, 5단계(자유 혼합) : 3가지까지
//
// 왜 RGB 평균을 안 쓰나요?
//   물감은 빛과 달라서, 노랑+파랑을 평균 내면 칙칙한 회색이 나와요.
//   그래서 대표 비율의 색(Anchors)을 정해 두고, 그 사이 비율은 Mix 에서 이어서 계산해요.
//   - 빨강 : 노랑 : 파랑 = 1:1:1 은 검은색에 가깝고, 2:2:1 이 갈색이에요.
public class ColorMixer : MonoBehaviour
{
    [Header("팔레트 버튼")]
    [SerializeField] RectTransform blackButton;
    [SerializeField] Button[] materialButtons;      // 빨강, 노랑, 파랑 (순서 고정)
    [SerializeField] Button undoButton;             // 마지막에 넣은 물감 빼기 (섞는 단계만)
    [SerializeField] TMPro.TMP_Text[] countTexts;   // 물감 옆 넣은 횟수

    [Header("붓")]
    [SerializeField] Image previewSwatch;           // 붓 끝 (지금 색)
    [SerializeField] RectTransform splashPoint;     // 물방울이 닿는 붓 끝 자리
    [SerializeField] RectTransform splashTarget;    // 첨벙할 때 살짝 커졌다 돌아오는 붓
    [SerializeField] Sprite dropSprite;             // 물방울 (동그라미)

    /// <summary>붓 색이 바뀌면 바로 알려 줘요 (그림판 펜 색). 화면의 붓 끝은 천천히 바뀌어요</summary>
    public Action<Color> OnChanged;

    // 물감 색은 도구의 색과 같아요
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

    const float FlightSeconds = 0.36f;      // 물방울이 붓 끝까지 날아가는 시간
    const float BlendSeconds = 0.6f;        // 붓 끝 색이 천천히 바뀌는 시간
    const float SplashSeconds = 0.35f;      // 첨벙 튄 물방울이 사라지는 시간
    const float PopSeconds = 0.22f;         // 붓 · 물감이 살짝 커졌다 돌아오는 시간

    readonly List<GameObject> drops = new List<GameObject>();   // 지금 화면에 있는 물방울
    Coroutine blending;

    readonly int[] counts = new int[3];                 // 물감별로 넣은 횟수
    readonly List<int> history = new List<int>();       // 넣은 순서 (되돌리기용)
    bool mixing;                                        // 섞는 단계인지
    int maxKinds = 3;
    int pureIndex;                                      // 섞지 않을 때 고른 색 (Palette 번호), 섞는 중이면 -1
    bool used;                                          // 섞은 색으로 그렸는지 (그 뒤 물감을 누르면 새로 섞어요)
    Color resultColor = Color.black;

    /// <summary>지금 붓 색 (화면의 붓 끝은 이 색을 향해 천천히 바뀌어요)</summary>
    public Color ResultColor => resultColor;

    /// <summary>섞지 않고 고른 색 번호 (0 검정 ~ 3 파랑). 섞은 색이면 -1</summary>
    public int PureIndex => pureIndex;

    /// <summary>지금 화면에 있는 물방울 수 (자동 테스트용)</summary>
    public int DropCount => drops.Count;

    /// <summary>물감별로 넣은 횟수 (자동 테스트용)</summary>
    public int CountOf(int paint) => counts[paint];

    /// <summary>단계 시작 : 검정 붓으로. mixing = 섞는 단계, maxKinds = 섞을 수 있는 물감 가짓수</summary>
    public void Setup(bool mixing, int maxKinds)
    {
        this.mixing = mixing;
        this.maxKinds = maxKinds;
        ClearPaints();
        pureIndex = 0;
        resultColor = Color.black;
        SkipAnimation();
        Refresh();
    }

    /// <summary>섞은 색으로 그렸어요 : 다음에 물감을 누르면 새로 섞기 시작해요</summary>
    public void MarkUsed()
    {
        if (history.Count > 0) used = true;
    }

    /// <summary>[버튼] 검정 · 섞지 않는 색 : 그 색 하나로. index 는 Palette 번호 (0 검정, 1 빨강, 2 노랑, 3 파랑)</summary>
    public void SetPure(int index)
    {
        ClearPaints();
        pureIndex = index;
        resultColor = ToolBar.Palette[index];
        Refresh();
        RectTransform from = index == 0 ? blackButton : materialButtons[index - 1].GetComponent<RectTransform>();
        StartCoroutine(ThrowPaint(resultColor, from));
    }

    /// <summary>[버튼] 물감 한 번 넣기. index : 0 빨강, 1 노랑, 2 파랑. 섞지 않는 단계면 그 색 하나로</summary>
    public void AddPaint(int index)
    {
        if (!mixing) { SetPure(index + 1); return; }
        if (used || pureIndex >= 0) ClearPaints();      // 그렸거나 다른 색을 쓰던 중이면 새로 섞기
        if (IsLocked(index)) return;
        pureIndex = -1;
        counts[index]++;
        history.Add(index);
        Refresh();
        StartCoroutine(ThrowPaint(ToolBar.Palette[index + 1], materialButtons[index].GetComponent<RectTransform>()));
    }

    /// <summary>[버튼] 마지막에 넣은 물감 빼기 : 물방울이 붓 끝에서 그 물감으로 돌아가요</summary>
    public void UndoPaint()
    {
        if (history.Count == 0) return;
        int index = history[history.Count - 1];
        counts[index]--;
        history.RemoveAt(history.Count - 1);
        used = false;
        if (history.Count == 0) { pureIndex = 0; resultColor = Color.black; }     // 다 빼면 검정 붓으로
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
        if (splashTarget != null) splashTarget.localScale = Vector3.one;
        if (previewSwatch != null) previewSwatch.color = resultColor;
    }

    void OnDisable() => SkipAnimation();

    void ClearPaints()
    {
        for (int i = 0; i < counts.Length; i++) counts[i] = 0;
        history.Clear();
        used = false;
    }

    void Refresh()
    {
        for (int i = 0; i < materialButtons.Length; i++)
        {
            bool locked = mixing && IsLocked(i);
            materialButtons[i].interactable = !locked;
            var group = materialButtons[i].transform.parent.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = locked ? 0.3f : 1f;      // 못 넣는 물감은 흐리게
        }
        if (history.Count > 0) resultColor = Mix(counts[0], counts[1], counts[2]);
        if (undoButton != null) undoButton.interactable = history.Count > 0;
        if (countTexts != null)
            for (int i = 0; i < countTexts.Length && i < counts.Length; i++)
            {
                countTexts[i].text = counts[i].ToString();
                countTexts[i].transform.parent.gameObject.SetActive(counts[i] > 0);    // 숫자 동그라미는 넣었을 때만
            }
        OnChanged?.Invoke(resultColor);
    }

    // ───────────── 움직임 ─────────────

    // 고른 색에서 물방울이 붓 끝으로 날아가고, 닿으면 첨벙 튀며 붓 끝 색이 바뀌어요
    IEnumerator ThrowPaint(Color paint, RectTransform source)
    {
        Vector2 from = CenterOf(source);
        Vector2 to = CenterOf(splashPoint);
        for (int i = 0; i < 4; i++) StartCoroutine(FlyDrop(paint, from, to + Random.insideUnitCircle * 10f, i * 0.04f, i == 0 ? 30f : Random.Range(14f, 22f)));
        yield return new WaitForSecondsRealtime(FlightSeconds);
        StartBlend();
        StartCoroutine(Pop(splashTarget));
        for (int i = 0; i < 7; i++) StartCoroutine(SplashDrop(paint, to, i * Mathf.PI * 2f / 7f + Random.Range(-0.2f, 0.2f)));
    }

    // 되돌리기 : 붓 끝에서 물방울이 나와 원래 물감으로 돌아가고, 물감이 살짝 커졌다 돌아와요
    IEnumerator ReturnPaint(int index)
    {
        Color paint = ToolBar.Palette[index + 1];
        RectTransform material = materialButtons[index].GetComponent<RectTransform>();
        Vector2 from = CenterOf(splashPoint);
        Vector2 to = CenterOf(material);
        for (int i = 0; i < 4; i++) StartCoroutine(FlyDrop(paint, from + Random.insideUnitCircle * 10f, to + Random.insideUnitCircle * 12f, i * 0.04f, i == 0 ? 30f : Random.Range(14f, 22f)));
        yield return new WaitForSecondsRealtime(FlightSeconds);
        StartCoroutine(Pop(material.parent));
    }

    IEnumerator Pop(Transform target)
    {
        if (target == null) yield break;
        for (float t = 0f; t < PopSeconds; t += Time.unscaledDeltaTime)
        {
            target.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / PopSeconds * Mathf.PI));
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    IEnumerator FlyDrop(Color paint, Vector2 from, Vector2 to, float delay, float size)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        RectTransform drop = NewDrop(paint, size);
        float arc = Random.Range(90f, 140f);
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
        float reach = Random.Range(70f, 100f);
        for (float t = 0f; t < SplashSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / SplashSeconds;
            drop.localPosition = center + direction * Mathf.Lerp(20f, reach, 1f - (1f - k) * (1f - k));
            Color faded = paint;
            faded.a = 1f - k;
            image.color = faded;
            yield return null;
        }
        RemoveDrop(drop);
    }

    // 붓 끝 색을 지금 색에서 새 색으로 천천히
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
        image.sprite = dropSprite;
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

    // 버튼 · 붓 끝의 가운데를 이 오브젝트 안의 위치로
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

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// JamDoodler : 5챕터 맞설 물건을 그릴 때 흑화 도깨비의 방해 (기획서 : 빨간 펜 낙서 · 지우기 · "다 알고 있어" 글씨)
// ─────────────────────────────────────────────────────────────
//   켜지면(OnEnable) 시작하고 꺼지면(OnDisable) 낙서를 다 치워요.
//   - 낙서 : 도깨비가 펜 끝이 되어 쪽지 위를 지그재그로 빨갛게 그어요 (실시간으로 선이 늘어나요)
//   - 글씨 : 도깨비 옆에 빨간 글씨가 한 글자씩 써져요 (Phrases 에서 차례로)
//   - 지우기 : 하얀 얼룩이 번져 그림 일부를 가려요 (가리기만 해요. 진짜 지우지 않아요)
//   - 놀래키기 : 가끔 도깨비가 가운데로 크게 튀어나오고 붉은 막이 번쩍해요
//   전부 쪽지 위에 덮기만 해요. 내 그림 · 판정은 그대로이고 터치도 통과해요 (C-47 · C-56)
public class JamDoodler : MonoBehaviour
{
    [SerializeField] RectTransform peek;        // 흑화 도깨비
    [SerializeField] Image tint;                // 붉은 막
    [SerializeField] TMP_FontAsset font;        // 글씨 글꼴
    [SerializeField] Sprite blob;               // 지우개 얼룩 (동그라미)

    /// <summary>도깨비가 쪽지에 쓸 말 (GameManager 가 그릴 물건 이름을 넣어 채워요)</summary>
    public string[] Phrases = { "그만둬!" };

    static readonly Color Ink = new Color32(210, 28, 28, 235);
    const float TintBase = 0.18f;

    RectTransform area;
    readonly List<GameObject> marks = new List<GameObject>();
    float flash;            // 놀래키기 때 붉은 막이 번쩍 (0~1)
    int phrase;

    void Awake() => area = (RectTransform)transform;

    void OnEnable()
    {
        Clear();
        phrase = 0;
        flash = 0f;
        HidePeek();
        StartCoroutine(Run());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        Clear();
        HidePeek();
    }

    void Update()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);
        Color c = tint.color;
        c.a = TintBase + 0.06f * Mathf.Sin(Time.time * 3f) + 0.45f * flash;
        tint.color = c;
    }

    IEnumerator Run()
    {
        float nextScare = Time.time + Random.Range(8f, 12f);
        int turn = 0;
        while (true)
        {
            if (Time.time >= nextScare)
            {
                yield return Scare();
                nextScare = Time.time + Random.Range(15f, 20f);
            }
            else
            {
                // 낙서 → 글씨 → 낙서 → 지우기 … 순서로 돌아가요
                switch (turn++ % 4)
                {
                    case 0: case 2: yield return Scribble(); break;
                    case 1: yield return Write(); break;
                    default: yield return Smudge(); break;
                }
            }
            yield return new WaitForSeconds(Random.Range(0.5f, 1.2f));      // 그리기를 열자마자 첫 낙서부터 시작해요
        }
    }

    // ───────── 낙서 : 지그재그 빨간 선을 펜 끝(도깨비)이 따라 그어요 ─────────
    IEnumerator Scribble()
    {
        Vector2 size = area.rect.size;
        Vector2 center = new Vector2(Random.Range(0.25f, 0.75f) * size.x, -Random.Range(0.2f, 0.8f) * size.y);
        float reach = size.x * Random.Range(0.18f, 0.3f);
        var line = NewMark("JamLine");
        Vector2 prev = center + Random.insideUnitCircle * reach;
        ShowPeek(prev, 0.55f);
        Sfx.Play(Sfx.Interfere);
        int points = Random.Range(6, 10);
        float thick = size.x * 0.014f;
        for (int i = 0; i < points; i++)
        {
            Vector2 next = center + Random.insideUnitCircle * reach;
            // 선 한 토막이 펜 끝을 따라 늘어나요
            var seg = NewImage("Seg", line.transform, null, Ink);
            seg.rectTransform.pivot = new Vector2(0f, 0.5f);
            seg.rectTransform.anchoredPosition = prev;
            Vector2 d = next - prev;
            seg.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            const float SegTime = 0.09f;
            for (float t = 0f; t < SegTime; t += Time.deltaTime)
            {
                float k = t / SegTime;
                seg.rectTransform.sizeDelta = new Vector2(d.magnitude * k + thick, thick);
                MovePeek(prev + d * k);
                yield return null;
            }
            seg.rectTransform.sizeDelta = new Vector2(d.magnitude + thick, thick);
            prev = next;
        }
        HidePeekSoon();
        StartCoroutine(FadeAway(line, 2.5f));
    }

    // ───────── 글씨 : 한 글자씩 써져요 ─────────
    IEnumerator Write()
    {
        if (Phrases == null || Phrases.Length == 0) yield break;
        string text = Phrases[phrase++ % Phrases.Length];
        Vector2 size = area.rect.size;
        var go = NewMark("JamText");
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size.x * 0.8f, size.y * 0.2f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(Random.Range(0.4f, 0.6f) * size.x, -Random.Range(0.2f, 0.8f) * size.y);
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-10f, 10f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.color = Ink;
        tmp.fontSize = size.x * (text.Length > 12 ? 0.065f : 0.1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        tmp.maxVisibleCharacters = 0;
        ShowPeek(rt.anchoredPosition + new Vector2(-rt.sizeDelta.x * 0.45f, rt.sizeDelta.y * 0.35f), 0.5f);
        for (int i = 1; i <= text.Length; i++)
        {
            tmp.maxVisibleCharacters = i;
            MovePeek(rt.anchoredPosition + new Vector2(rt.sizeDelta.x * (i / (float)text.Length - 0.5f) * 0.9f, rt.sizeDelta.y * 0.35f));
            yield return new WaitForSeconds(text[i - 1] == ' ' ? 0.03f : 0.08f);
        }
        HidePeekSoon();
        StartCoroutine(FadeAway(go, 3f));
    }

    // ───────── 지우기 : 하얀 얼룩이 번져 그림을 가려요 ─────────
    IEnumerator Smudge()
    {
        Vector2 size = area.rect.size;
        var go = NewMark("JamSmudge");
        var img = go.AddComponent<Image>();
        img.sprite = blob;
        img.color = new Color(1f, 1f, 1f, 0.93f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(Random.Range(0.25f, 0.75f) * size.x, -Random.Range(0.25f, 0.75f) * size.y);
        float full = size.x * Random.Range(0.22f, 0.32f);
        ShowPeek(rt.anchoredPosition, 0.5f);
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            rt.sizeDelta = new Vector2(full * k, full * 0.8f * k);
            MovePeek(rt.anchoredPosition + new Vector2(Mathf.Sin(t * 30f), Mathf.Cos(t * 23f)) * full * 0.25f);   // 박박 문질러요
            yield return null;
        }
        HidePeekSoon();
        StartCoroutine(FadeAway(go, 2.5f));
    }

    // ───────── 놀래키기 : 가운데로 크게 튀어나와요 ─────────
    IEnumerator Scare()
    {
        Vector2 size = area.rect.size;
        peek.anchoredPosition = new Vector2(size.x * 0.5f, -size.y * 0.45f);
        Sfx.Play(Sfx.Scare);
        flash = 1f;
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float s = t < 0.12f ? Mathf.Lerp(0.2f, 3.4f, t / 0.12f) : 3.4f - 0.3f * Mathf.Sin((t - 0.12f) * 25f) * (1f - t / 0.9f);
            peek.localScale = Vector3.one * s;
            peek.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 60f) * 10f * (1f - t / 0.9f));
            yield return null;
        }
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            peek.localScale = Vector3.one * Mathf.Lerp(3.4f, 0f, t / 0.25f);
            yield return null;
        }
        HidePeek();
    }

    // ───────── 도깨비 (펜 끝) ─────────
    float peekSize;
    Coroutine hiding;

    void ShowPeek(Vector2 at, float size)
    {
        if (hiding != null) { StopCoroutine(hiding); hiding = null; }
        peekSize = size;
        peek.anchoredPosition = at;
        peek.localScale = Vector3.one * size;
    }

    void MovePeek(Vector2 at)
    {
        peek.anchoredPosition = at + new Vector2(0f, area.rect.size.x * 0.05f);   // 펜 끝 위에 살짝
        peek.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.time * 14f) * 8f);
        peek.localScale = Vector3.one * peekSize;
    }

    void HidePeekSoon() => hiding = StartCoroutine(Shrink());

    IEnumerator Shrink()
    {
        Vector3 from = peek.localScale;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            peek.localScale = Vector3.Lerp(from, Vector3.zero, t / 0.3f);
            yield return null;
        }
        HidePeek();
        hiding = null;
    }

    void HidePeek()
    {
        if (peek == null) return;
        peek.localScale = Vector3.zero;
        peek.localEulerAngles = Vector3.zero;
    }

    // ───────── 낙서 조각 만들기 · 치우기 ─────────
    GameObject NewMark(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(area, false);
        rt.SetSiblingIndex(peek.GetSiblingIndex());     // 도깨비보다 아래
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        marks.Add(go);
        return go;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var img = go.GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0f, 1f);
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    IEnumerator FadeAway(GameObject go, float hold)
    {
        yield return new WaitForSeconds(hold);
        var cg = go.GetComponent<CanvasGroup>();
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            cg.alpha = 1f - t;
            yield return null;
        }
        marks.Remove(go);
        Destroy(go);
    }

    void Clear()
    {
        foreach (GameObject go in marks) if (go != null) Destroy(go);
        marks.Clear();
    }

    /// <summary>테스트용 : 지금 쪽지 위에 남아 있는 낙서 수</summary>
    public int MarkCount => marks.Count;
}

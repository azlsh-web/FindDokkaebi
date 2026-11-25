using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// JamDoodler : 5챕터 맞설 물건을 그릴 때 흑화 도깨비의 방해 (기획서 : 빨간 펜 낙서 · 지우기 · "다 알고 있어" 글씨)
// ─────────────────────────────────────────────────────────────
//   켜지면(OnEnable) 시작하고 꺼지면(OnDisable) 낙서를 다 치워요.
//   방해는 한 번에 하나씩, 사이에 2초쯤 조용한 틈을 둬요 (정신없지 않게). 대신 하나하나가 내 그림을 노려요.
//   - 낙서 : 도깨비가 펜 끝이 되어 내가 방금 그린 자리 위를 지그재그로 빨갛게 그어요
//   - 글씨 : 빨간 글씨가 여러 줄로, 글자마다 크기 · 기울기 · 높이가 제각각 삐뚤빼뚤 써지고 부들부들 떨려요
//   - 지우기 : 하얀 얼룩이 내가 방금 그린 자리에 번져 가려요 (가리기만 해요. 진짜 지우지 않아요)
//   - 도깨비불 : 붉은 도깨비불 셋이 쪽지 안을 천천히 맴돌다, 하나가 내가 그리던 자리로 휙 날아와 크게 일렁이며 가려요
//   - 놀래키기 : 가끔 도깨비가 가운데로 크게 튀어나오고 붉은 막이 번쩍해요
//   전부 쪽지 위에 덮기만 해요. 내 그림 · 판정은 그대로이고 터치도 통과해요 (C-47 · C-56)
public class JamDoodler : MonoBehaviour
{
    [SerializeField] RectTransform peek;        // 흑화 도깨비
    [SerializeField] Image tint;                // 붉은 막
    [SerializeField] TMP_FontAsset font;        // 글씨 글꼴
    [SerializeField] Sprite blob;               // 지우개 얼룩 (동그라미)
    [SerializeField] RectTransform[] fires;     // 붉은 도깨비불 (빛 + 불꽃)
    [SerializeField] DrawingBoard board;        // 내가 그리는 자리를 노려요

    /// <summary>도깨비가 쪽지에 쓸 말 (GameManager 가 그릴 물건 이름을 넣어 채워요)</summary>
    public string[] Phrases = { "그만둬!" };

    static readonly Color Ink = new Color32(210, 28, 28, 235);
    const float TintBase = 0.18f;
    const float CalmMin = 1.8f, CalmMax = 2.8f;     // 방해와 방해 사이 조용한 틈 (초)

    RectTransform area;
    Canvas canvas;
    readonly List<GameObject> marks = new List<GameObject>();
    float flash;            // 놀래키기 때 붉은 막이 번쩍 (0~1)
    int phrase;
    int dashing = -1;       // 내 그림으로 날아간 도깨비불 번호 (없으면 -1)
    float dashScale = 1f;

    void Awake()
    {
        area = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        Clear();
        phrase = 0;
        MarksMade = 0;
        flash = 0f;
        dashing = -1;
        HidePeek();
        if (fires != null) for (int i = 0; i < fires.Length; i++) fires[i].anchoredPosition = Orbit(i);
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
        MoveFires();
    }

    IEnumerator Run()
    {
        float nextScare = Time.time + Random.Range(10f, 14f);
        int turn = 0;
        yield return new WaitForSeconds(0.3f);      // 그리기를 열자마자 첫 낙서부터 시작해요
        while (true)
        {
            if (Time.time >= nextScare)
            {
                yield return Scare();
                nextScare = Time.time + Random.Range(16f, 22f);
            }
            else
            {
                // 낙서 → 도깨비불 → 글씨 → 지우기 → 도깨비불 … 순서로 하나씩
                switch (turn++ % 5)
                {
                    case 0: yield return Scribble(); break;
                    case 1: case 4: yield return FireDash(); break;
                    case 2: yield return Write(); break;
                    default: yield return Smudge(); break;
                }
            }
            yield return new WaitForSeconds(Random.Range(CalmMin, CalmMax));
        }
    }

    // ───────── 노릴 자리 : 방금 그린 자리 (아직 안 그렸으면 아무 데나) ─────────
    Vector2 Target()
    {
        Vector2 size = area.rect.size;
        Vector2 p;
        if (board != null && Time.time - board.LastDrawTime < 8f
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, board.LastScreenPoint, Cam(), out Vector2 local))
            p = local - new Vector2(area.rect.xMin, area.rect.yMax) + Random.insideUnitCircle * size.x * 0.05f;
        else
            p = new Vector2(Random.Range(0.3f, 0.7f) * size.x, -Random.Range(0.3f, 0.7f) * size.y);
        p.x = Mathf.Clamp(p.x, size.x * 0.15f, size.x * 0.85f);
        p.y = Mathf.Clamp(p.y, -size.y * 0.85f, -size.y * 0.15f);
        return p;
    }

    Camera Cam() => canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    // ───────── 낙서 : 지그재그 빨간 선을 펜 끝(도깨비)이 따라 그어요 ─────────
    IEnumerator Scribble()
    {
        Vector2 size = area.rect.size;
        Vector2 center = Target();
        float reach = size.x * Random.Range(0.16f, 0.24f);
        var line = NewMark("JamLine");
        Vector2 prev = center + Random.insideUnitCircle * reach;
        ShowPeek(prev, 0.6f);
        Sfx.Play(Sfx.Interfere);
        int points = Random.Range(9, 13);
        float thick = size.x * 0.018f;
        for (int i = 0; i < points; i++)
        {
            Vector2 next = center + Random.insideUnitCircle * reach;
            // 선 한 토막이 펜 끝을 따라 늘어나요
            var seg = NewImage("Seg", line.transform, null, Ink);
            seg.rectTransform.pivot = new Vector2(0f, 0.5f);
            seg.rectTransform.anchoredPosition = prev;
            Vector2 d = next - prev;
            seg.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            const float SegTime = 0.08f;
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
        StartCoroutine(FadeAway(line, 3f));
    }

    // ───────── 글씨 : 여러 줄로 삐뚤빼뚤, 한 글자씩 써지고 부들부들 떨려요 ─────────
    IEnumerator Write()
    {
        if (Phrases == null || Phrases.Length == 0) yield break;
        string text = Phrases[phrase++ % Phrases.Length];
        Vector2 size = area.rect.size;
        var go = NewMark("JamText");
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size.x * 0.62f, size.y * 0.42f);     // 좁게 : 한 줄로 길게 늘어서지 않고 여러 줄로 꺾여요
        rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 at = Target();
        at.x = Mathf.Clamp(at.x, size.x * 0.34f, size.x * 0.66f);
        at.y = Mathf.Clamp(at.y, -size.y * 0.75f, -size.y * 0.25f);
        rt.anchoredPosition = at;
        rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(-8f, 8f));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.richText = true;
        tmp.text = Creepy(text);
        tmp.color = Color.white;
        tmp.fontSize = size.x * 0.095f;
        tmp.lineSpacing = -12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();
        var info = tmp.textInfo;
        ShowPeek(CharAt(tmp, 0), 0.55f);
        Sfx.Play(Sfx.Interfere);
        StartCoroutine(Tremble(tmp));
        int count = info.characterCount;
        for (int i = 0; i < count; i++)
        {
            tmp.maxVisibleCharacters = i + 1;
            MovePeek(CharAt(tmp, i));
            char ch = info.characterInfo[i].character;
            yield return new WaitForSeconds(ch == ' ' ? 0.03f : Random.Range(0.07f, 0.14f));    // 꾹꾹 눌러 쓰듯 들쭉날쭉
        }
        HidePeekSoon();
        StartCoroutine(FadeAway(go, 3.2f));
    }

    // 글자마다 크기 · 기울기 · 높이 · 진하기를 제각각 (TMP 서식 글자)
    static string Creepy(string text)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        foreach (char ch in text)
        {
            if (ch == ' ') { sb.Append(' '); continue; }
            int sz = Random.Range(65, 165);
            float rot = Random.Range(-32f, 32f);
            float off = Random.Range(-0.3f, 0.3f);
            string col = Random.value < 0.3f ? "#7A0A0E" : "#D21C1C";
            sb.Append("<size=").Append(sz).Append("%><rotate=").Append(rot.ToString("0", inv))
              .Append("><voffset=").Append(off.ToString("0.00", inv)).Append("em><color=").Append(col).Append('>').Append(ch);
        }
        return sb.ToString();
    }

    // 글자 i 의 자리 (쪽지 왼쪽 위 기준), 펜 끝은 글자 위에
    Vector2 CharAt(TMP_Text tmp, int i)
    {
        var info = tmp.textInfo;
        if (info.characterCount == 0) return ((RectTransform)tmp.transform).anchoredPosition;
        var c = info.characterInfo[Mathf.Clamp(i, 0, info.characterCount - 1)];
        Vector3 mid = (c.bottomLeft + c.topRight) * 0.5f;
        Vector2 local = area.InverseTransformPoint(tmp.transform.TransformPoint(mid));
        return local - new Vector2(area.rect.xMin, area.rect.yMax);
    }

    // 글씨가 사라질 때까지 부들부들 : 글자 하나하나가 따로 움찔거리고, 글씨 전체도 떨려요
    IEnumerator Tremble(TMP_Text tmp)
    {
        var rt = (RectTransform)tmp.transform;
        Vector2 home = rt.anchoredPosition;
        float tilt = rt.localEulerAngles.z;
        float jitter = area.rect.size.x * 0.008f;
        while (tmp != null)
        {
            rt.anchoredPosition = home + Random.insideUnitCircle * 4f;
            rt.localEulerAngles = new Vector3(0f, 0f, tilt + Random.Range(-1.2f, 1.2f));
            tmp.ForceMeshUpdate();
            var info = tmp.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                Vector3[] verts = info.meshInfo[c.materialReferenceIndex].vertices;
                Vector3 shift = Random.insideUnitCircle * jitter;
                for (int v = 0; v < 4; v++) verts[c.vertexIndex + v] += shift;
            }
            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            yield return new WaitForSeconds(0.06f);
        }
    }

    // ───────── 지우기 : 하얀 얼룩이 내 그림 위에 번져 가려요 ─────────
    IEnumerator Smudge()
    {
        Vector2 size = area.rect.size;
        var go = NewMark("JamSmudge");
        var img = go.AddComponent<Image>();
        img.sprite = blob;
        img.color = new Color(1f, 1f, 1f, 0.95f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Target();
        float full = size.x * Random.Range(0.32f, 0.42f);
        ShowPeek(rt.anchoredPosition, 0.55f);
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
            rt.sizeDelta = new Vector2(full * k, full * 0.8f * k);
            MovePeek(rt.anchoredPosition + new Vector2(Mathf.Sin(t * 30f), Mathf.Cos(t * 23f)) * full * 0.25f);   // 박박 문질러요
            yield return null;
        }
        HidePeekSoon();
        StartCoroutine(FadeAway(go, 3f));
    }

    // ───────── 도깨비불 : 쪽지 안을 맴돌다, 하나가 내 그림으로 날아와 일렁이며 가려요 ─────────
    Vector2 Orbit(int i)
    {
        Vector2 size = area.rect.size;
        float a = -Time.time * 0.5f + i * Mathf.PI * 2f / fires.Length;
        return new Vector2(size.x * (0.5f + 0.36f * Mathf.Cos(a)),
                           -size.y * (0.5f + 0.36f * Mathf.Sin(a)) + Mathf.Sin(Time.time * 2.3f + i) * 14f);
    }

    void MoveFires()
    {
        if (fires == null) return;
        float follow = 1f - Mathf.Exp(-5f * Time.deltaTime);       // 날아갔던 불은 부드럽게 제자리로
        for (int i = 0; i < fires.Length; i++)
        {
            if (i != dashing) fires[i].anchoredPosition = Vector2.Lerp(fires[i].anchoredPosition, Orbit(i), follow);
            float big = i == dashing ? dashScale : 1f;
            float flicker = 1f + 0.08f * Mathf.Sin(Time.time * 9f + i * 2f);
            fires[i].localScale = new Vector3(big / flicker, big * flicker, 1f);
        }
    }

    IEnumerator FireDash()
    {
        if (fires == null || fires.Length == 0) yield break;
        int i = Random.Range(0, fires.Length);
        dashing = i;
        var fire = fires[i];
        Vector2 from = fire.anchoredPosition;
        Vector2 to = Target();
        Sfx.Play(Sfx.Interfere);
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)          // 휙 날아와요
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            fire.anchoredPosition = Vector2.Lerp(from, to, k);
            dashScale = Mathf.Lerp(1f, 1.7f, k);
            yield return null;
        }
        for (float t = 0f; t < 1.8f; t += Time.deltaTime)           // 내 그림 위에서 크게 일렁여요
        {
            fire.anchoredPosition = to + new Vector2(Mathf.Sin(t * 11f) * 26f, Mathf.Cos(t * 7f) * 18f);
            dashScale = 1.7f + 0.12f * Mathf.Sin(t * 13f);
            yield return null;
        }
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            dashScale = Mathf.Lerp(1.7f, 1f, t / 0.3f);
            yield return null;
        }
        dashScale = 1f;
        dashing = -1;
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
        rt.SetSiblingIndex(peek.GetSiblingIndex());     // 도깨비 · 도깨비불보다 아래
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;
        marks.Add(go);
        MarksMade++;
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

    /// <summary>테스트용 : 켜진 뒤 지금까지 쪽지에 남긴 낙서 · 글씨 · 얼룩 수 (사라진 것도 셈)</summary>
    public int MarksMade { get; private set; }
}

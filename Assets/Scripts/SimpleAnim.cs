using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// SimpleAnim : 코드로 만든 간단한 움직임 (애니메이터 없이)
// ─────────────────────────────────────────────────────────────
// 오브젝트에 붙이고 Inspector 에서 kind 만 고르면, 켜질 때마다 처음부터 움직여요.
// 움직임이 끝나거나 꺼지면 원래 자리 · 크기로 돌아와요.
// 캐릭터는 정신없지 않게 잔잔하게 움직이고, 등장 · 퇴장 때를 빼면 화면 밖으로 나가지 않아요.
public class SimpleAnim : MonoBehaviour
{
    public enum Kind
    {
        None,
        Sway,       // 좌우로 갸웃갸웃
        Pop,        // 0 → 살짝 크게 → 원래 크기
        Swing,      // 한 번 휙 휘두르기
        Gentle,     // 위아래로 살짝 (말할 때)
        Shake,      // 좌우로 놀란 듯 한 번
        FootSway,   // 발을 땅에 붙이고 살랑살랑
        Roam,       // 좌우로 천천히 오가기 (양 끝에서 잠깐 멈춤)
        Step,       // 걸음마다 살짝 흔들리기
        Dust,       // 먼지가 피어올랐다 사라지기
        Float,      // 둥실둥실 떠 있기
        Look,       // 잠깐 나타났다 사라지기 (물음표)
        FadeIn,     // 화면 전환 : 서서히 나타나기
        CardIn,     // 제자리에서 살짝 커지며 나타나기
        SlideRight, // 왼쪽 화면 밖에서 오른쪽으로 들어오기 (간식)
        EnterLeft,  // 왼쪽에서 들어오기
        EnterRight, // 오른쪽에서 들어오기
        RunOff,     // 오른쪽 화면 밖으로 뛰어나가기
        RunBack,    // 오른쪽 화면 밖에서 돌아오기
        SnackOut,   // 간식이 위에서 도깨비에게 날아가기
        NoteGive,   // 쪽지가 작아지며 도깨비에게 건네지기
        Vanish,     // 살짝 커졌다가 사라지기 (없어지는 물건)
        NumPop,     // 숫자가 바뀔 때 커졌다 돌아오기
        Spark,      // 반짝임
        Rise,       // 아래에서 살짝 올라오기
        Land,       // 위에서 날아와 자리 잡기 (물건)
        Fall,       // 꽃가루 떨어지기
        FillIn,     // 채워지며 그려지기 (채점 동그라미 · 빗금)
    }

    public Kind kind = Kind.Sway;

    [Tooltip("켜진 뒤 기다리는 시간(초)")]
    public float delay = 0f;

    [Tooltip("Sway·Gentle·FootSway 등 반복 움직임의 빠르기")]
    public float speed = 3f;

    [Tooltip("Sway·Swing : 기울어지는 각도(도)")]
    public float angle = 8f;

    [Tooltip("움직임에 걸리는 시간(초). 반복 움직임은 한 바퀴 시간")]
    public float popTime = 0.45f;

    [Tooltip("Roam · Enter · Rise 등에서 움직이는 거리")]
    public float distance = 180f;

    RectTransform rt;
    CanvasGroup group;
    Graphic graphic;
    Image image;
    Vector2 basePos;
    float baseAlpha = 1f;
    bool captured;
    float t;

    void Cache()
    {
        if (rt == null) rt = transform as RectTransform;
        if (group == null) group = GetComponent<CanvasGroup>();
        if (graphic == null) graphic = GetComponent<Graphic>();
        if (image == null) image = GetComponent<Image>();
    }

    void OnEnable()
    {
        Cache();
        basePos = rt != null ? rt.anchoredPosition : Vector2.zero;
        baseAlpha = graphic != null ? graphic.color.a : 1f;
        captured = true;
        t = 0f;
        Apply();
    }

    void OnDisable() => ResetPose();

    /// <summary>이 움직임을 처음부터 다시 (kind 를 바꿀 수도 있어요)</summary>
    public void Play(Kind newKind)
    {
        Cache();
        if (!isActiveAndEnabled)
        {
            // 꺼져 있었으면 켜질 때(OnEnable) 원래 자리를 기억하고 처음부터 움직여요
            kind = newKind;
            enabled = true;
            return;
        }
        ResetPose();    // 움직이던 중이면 원래 자리로 돌린 뒤 다시 시작
        kind = newKind;
        t = 0f;
        Apply();
    }

    public void Play() => Play(kind);

    /// <summary>움직임이 끝나고 돌아갈 기본 투명도를 바꿔요 (흐리게 그리는 그림)</summary>
    public void SetBaseAlpha(float a)
    {
        Cache();
        baseAlpha = a;
        captured = true;
        SetAlpha(a);
    }

    void ResetPose()
    {
        if (!captured) return;
        // 레이아웃이 자리를 정하는 쪽지(CardIn)와 크기만 바뀌는 움직임은 자리를 건드리지 않아요
        bool moves = kind != Kind.CardIn && kind != Kind.Pop && kind != Kind.NumPop && kind != Kind.FadeIn && kind != Kind.FillIn;
        if (rt != null && moves) rt.anchoredPosition = basePos;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.Euler(0f, 0f, kind == Kind.CardIn ? -2f : 0f);
        SetAlpha(baseAlpha);
        if (image != null && kind == Kind.FillIn) image.fillAmount = 1f;
    }

    void SetAlpha(float a)
    {
        if (group != null) group.alpha = a;
        else if (graphic != null)
        {
            Color c = graphic.color;
            c.a = a;
            graphic.color = c;
        }
    }

    void Update()
    {
        t += Time.deltaTime;
        Apply();
    }

    static float Ease(float p) => 1f - (1f - p) * (1f - p);

    void Move(float dx, float dy)
    {
        if (rt != null) rt.anchoredPosition = basePos + new Vector2(dx, dy);
    }

    void Apply()
    {
        float x = t - delay;
        float p = Mathf.Clamp01(x / Mathf.Max(0.0001f, popTime));
        bool waiting = x < 0f;

        switch (kind)
        {
            case Kind.Sway:
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * speed) * angle);
                break;

            case Kind.Pop:
                transform.localScale = Vector3.one * (waiting ? 0f
                    : p < 0.65f ? Mathf.Lerp(0f, 1.12f, p / 0.65f) : Mathf.Lerp(1.12f, 1f, (p - 0.65f) / 0.35f));
                break;

            case Kind.Swing:
                {
                    float a = p < 0.33f ? Mathf.Lerp(0f, angle, p / 0.33f)
                            : p < 0.66f ? Mathf.Lerp(angle, -angle, (p - 0.33f) / 0.33f)
                            : Mathf.Lerp(-angle, 0f, (p - 0.66f) / 0.34f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, a);
                    break;
                }

            case Kind.Gentle:
                Move(0f, Mathf.Abs(Mathf.Sin(Mathf.Max(0f, x) * Mathf.PI / Mathf.Max(0.01f, popTime))) * distance);
                break;

            case Kind.Shake:
                Move(p >= 1f ? 0f : Mathf.Sin(p * Mathf.PI * 4f) * distance * (1f - p), 0f);
                break;

            case Kind.FootSway:
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Mathf.Max(0f, x) * 2f * Mathf.PI / popTime) * angle);
                break;

            case Kind.Roam:
                {
                    // 한 바퀴 : 36% 오른쪽으로 → 14% 멈춤 → 36% 왼쪽으로 → 14% 멈춤
                    float c = Mathf.Repeat(Mathf.Max(0f, x) / popTime, 1f);
                    float k = c < 0.36f ? Mathf.SmoothStep(0f, 1f, c / 0.36f)
                            : c < 0.5f ? 1f
                            : c < 0.86f ? Mathf.SmoothStep(1f, 0f, (c - 0.5f) / 0.36f) : 0f;
                    Move(k * distance, 0f);
                    break;
                }

            case Kind.Step:
                {
                    float s = Mathf.Sin(t * 2f * Mathf.PI / popTime);
                    Move(0f, Mathf.Abs(s) * distance);
                    transform.localRotation = Quaternion.Euler(0f, 0f, s * angle);
                    break;
                }

            case Kind.Dust:
                {
                    float c = Mathf.Repeat(t / popTime, 1f);
                    transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, c);
                    Move(0f, c * distance);
                    SetAlpha((1f - c) * 0.45f);
                    break;
                }

            case Kind.Float:
                {
                    float s = Mathf.Sin(t * 2f * Mathf.PI / popTime);
                    Move(0f, (s * 0.5f + 0.5f) * distance);
                    transform.localRotation = Quaternion.Euler(0f, 0f, s * angle);
                    break;
                }

            case Kind.Look:
                {
                    float c = Mathf.Repeat(t / popTime, 1f);
                    SetAlpha(c > 0.5f && c < 0.9f ? baseAlpha : 0f);
                    break;
                }

            case Kind.FadeIn:
                SetAlpha(Mathf.Lerp(0f, 1f, p));
                break;

            case Kind.CardIn:
                {
                    float s = p < 0.7f ? Mathf.Lerp(0.92f, 1.02f, p / 0.7f) : Mathf.Lerp(1.02f, 1f, (p - 0.7f) / 0.3f);
                    transform.localScale = Vector3.one * s;
                    transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
                    SetAlpha(Mathf.Clamp01(p / 0.7f));
                    break;
                }

            case Kind.SlideRight:
                {
                    float k = p < 0.8f ? Mathf.Lerp(-distance, 18f, Ease(p / 0.8f)) : Mathf.Lerp(18f, 0f, (p - 0.8f) / 0.2f);
                    Move(k, 0f);
                    SetAlpha(waiting ? 0f : baseAlpha * Mathf.Clamp01(p / 0.15f));
                    if (p >= 1f) enabled = false;       // 다 들어오면 끝 (쓴 간식 흐리게 하는 색을 덮어쓰지 않게)
                    break;
                }

            case Kind.EnterLeft:
                Move(Mathf.Lerp(-distance, 0f, Ease(p)), 0f);
                break;

            case Kind.EnterRight:
            case Kind.RunBack:
                Move(Mathf.Lerp(distance, 0f, Ease(p)), 0f);
                break;

            case Kind.RunOff:
                Move(Mathf.Lerp(0f, distance, p * p), 0f);
                break;

            case Kind.SnackOut:
                Move(Mathf.Lerp(0f, 60f, p * p), Mathf.Lerp(0f, -990f, p * p));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, p);
                SetAlpha(p < 0.85f ? 1f : (1f - p) / 0.15f);
                break;

            case Kind.NoteGive:
                Move(Mathf.Lerp(0f, 210f, p * p), Mathf.Lerp(0f, -750f, p * p));
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, p);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 10f, p));
                SetAlpha(1f - p * p);
                break;

            case Kind.Vanish:
                transform.localScale = Vector3.one * (waiting ? 1f
                    : p < 0.35f ? Mathf.Lerp(1f, 1.15f, p / 0.35f) : Mathf.Lerp(1.15f, 0f, (p - 0.35f) / 0.65f));
                break;

            case Kind.NumPop:
                transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Ease(p));
                break;

            case Kind.Spark:
                {
                    float c = waiting ? 0f : Mathf.Repeat(x / popTime, 1f);
                    transform.localScale = Vector3.one * (c < 0.4f ? Mathf.Lerp(0f, 1.2f, c / 0.4f) : Mathf.Lerp(1.2f, 0.7f, (c - 0.4f) / 0.6f));
                    SetAlpha(waiting ? 0f : c < 0.4f ? c / 0.4f : 1f - (c - 0.4f) / 0.6f);
                    break;
                }

            case Kind.Rise:
                Move(0f, Mathf.Lerp(-distance, 0f, Ease(p)));
                SetAlpha(p);
                break;

            case Kind.Land:
                {
                    float k = p < 0.8f ? Mathf.Lerp(1f, 0f, Ease(p / 0.8f)) : 0f;
                    Move(0f, k * distance);
                    float a = p < 0.8f ? Mathf.Lerp(-30f, 5f, p / 0.8f) : Mathf.Lerp(5f, 0f, (p - 0.8f) / 0.2f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, a);
                    SetAlpha(waiting ? 0f : baseAlpha * Mathf.Clamp01(p / 0.25f));
                    break;
                }

            case Kind.Fall:
                {
                    float c = Mathf.Repeat(Mathf.Max(0f, x) / popTime, 1f);
                    Move(0f, Mathf.Lerp(120f, -1980f, c));
                    transform.localRotation = Quaternion.Euler(0f, 0f, c * 540f);
                    break;
                }

            case Kind.FillIn:
                if (image != null) image.fillAmount = waiting ? 0f : p;
                break;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// MagnifierPeek : 돋보기로 원본을 잠깐 둘러보기
// ─────────────────────────────────────────────────────────────
// 그리기 화면 위를 어둡게 덮고, 돋보기 렌즈 안에만 원본이 보여요.
// 화면을 누르거나 끌면 렌즈가 손가락을 따라가요. 원본은 그림판 자리에 고정돼 있어서
// 렌즈를 움직이면 원본의 다른 부분이 보여요. 정해진 시간이 지나면 저절로 닫혀요.
// 보는 동안 돋보기 테두리의 호박색 띠가 시계 방향으로 줄어들어 원래 크림색이 드러나요.
// 마지막 1초에는 띠가 코랄색으로 은은하게 숨 쉬어요. 1초마다 똑딱 소리가 나요.
//
// 이 오브젝트(덮개)가 켜져 있는 동안에는 덮개가 누르기를 모두 받아서 그림을 그릴 수 없어요.
// 시간은 Time.deltaTime 으로 줄어서, 일시정지하면 같이 멈춰요.
public class MagnifierPeek : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] RectTransform area;    // 그림판 자리 : 원본이 놓이고, 렌즈 가운데가 이 안에 머물러요
    [SerializeField] RectTransform lens;    // 돋보기 그림 (기준점 = 렌즈 구멍 가운데)
    [SerializeField] RectTransform view;    // 렌즈 안에 보이는 종이와 원본 (그림판 자리에 고정)
    [SerializeField] Image original;
    [SerializeField] SimpleAnim lensAnim;
    [SerializeField] Image timeRing;        // 테두리의 크림색 띠를 오린 고리 (Filled, Radial360)

    static readonly Color RingColor = new Color32(236, 160, 58, 255);       // 호박색
    static readonly Color RingLastColor = new Color32(242, 119, 107, 255);  // 마지막 1초 : 코랄

    float left;
    float total;
    int lastCount;

    /// <summary>둘러보는 중인지</summary>
    public bool Active => gameObject.activeSelf;

    /// <summary>남은 시간 (초)</summary>
    public float Left => left;

    /// <summary>원본을 넣고 seconds 초 동안 둘러보기</summary>
    public void Begin(Sprite sprite, float seconds)
    {
        original.sprite = sprite;
        left = seconds;
        total = Mathf.Max(0.01f, seconds);
        gameObject.SetActive(true);
        lens.position = Center;             // 그림판 가운데에서 시작
        KeepView();
        if (lensAnim != null) lensAnim.Play(SimpleAnim.Kind.Pop);
        lastCount = 0;
        ShowTime();
    }

    /// <summary>남은 초 (5 → 4 → … → 1)</summary>
    public int Count => Mathf.Clamp(Mathf.CeilToInt(left), 1, Mathf.CeilToInt(total));

    /// <summary>띠가 남은 비율 (1 → 0)</summary>
    public float RingFill => timeRing != null ? timeRing.fillAmount : 0f;

    void ShowTime()
    {
        if (timeRing != null)
        {
            timeRing.fillAmount = Mathf.Clamp01(left / total);
            // 마지막 1초는 호박색 ↔ 코랄을 천천히 오가요
            float warn = left <= 1f ? 0.5f + 0.5f * Mathf.Sin((1f - left) * Mathf.PI * 4f - Mathf.PI * 0.5f) : 0f;
            timeRing.color = Color.Lerp(RingColor, RingLastColor, warn);
        }
        if (Count == lastCount) return;
        lastCount = Count;
        Sfx.Play(Sfx.Count);
    }

    public void Stop()
    {
        gameObject.SetActive(false);
    }

    void Update()
    {
        left -= Time.deltaTime;
        if (left <= 0f) { Stop(); return; }
        ShowTime();
    }

    void LateUpdate() => KeepView();

    Vector3 Center => area.TransformPoint(area.rect.center);

    // 렌즈가 움직여도 원본은 그림판 자리에 그대로 있어요 (view 의 기준점은 가운데)
    void KeepView()
    {
        view.position = Center;
    }

    public void OnPointerDown(PointerEventData e) => MoveTo(e);
    public void OnDrag(PointerEventData e) => MoveTo(e);

    void MoveTo(PointerEventData e)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out Vector2 local)) return;
        Rect r = area.rect;
        local.x = Mathf.Clamp(local.x, r.xMin, r.xMax);
        local.y = Mathf.Clamp(local.y, r.yMin, r.yMax);
        lens.position = area.TransformPoint(local);
        KeepView();
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────
// PaperView : 책상 위 쪽지를 두 손가락으로 키우고 · 돌리고 · 옮기기 (ibis Paint 처럼)
// ─────────────────────────────────────────────────────────────
// - 한 손가락은 그리기만 해요 (DrawingBoard). 두 번째 손가락이 닿으면 그리던 선을 취소하고 종이를 움직여요.
// - 손가락을 모두 떼면 다시 그릴 수 있어요.
// - 종이는 처음에 책상 위에 똑바로 놓여요. 움직였으면 오른쪽 위에 "맞춤" 버튼이 떠서 처음 자리로 돌아가요.
// - 그림의 실제 크기(512x604)는 그대로예요. 화면에서 보이는 크기와 각도만 바뀌어서 판정과는 상관없어요.
// - 에디터에서는 마우스 휠로 키우고 줄여 볼 수 있어요.
public class PaperView : MonoBehaviour
{
    [SerializeField] RectTransform paper;       // 쪽지 (그림판 · 쪽지 글이 함께 움직여요)
    [SerializeField] DrawingBoard board;
    [SerializeField] GameObject fitButton;      // 맞춤 : 처음 자리로
    [SerializeField] float minScale = 0.6f;
    [SerializeField] float maxScale = 3f;
    // 비키기 : 팔레트가 뜨면(1) 쪽지를 처음 자리로 (팔레트와 안 겹쳐요),
    //   색 섞기 시범처럼 아래 띠까지 위로 올라오면(2) 쪽지를 작게 줄여 그 위로
    [SerializeField] float[] squeezeScale = { 1f, 1f, 0.86f };
    [SerializeField] float[] squeezeLift = { 0f, 0f, 300f };

    Vector3 homePos;
    bool homeSaved;

    // 지금 보기 (squeeze 가 아닐 때 사용자가 정한 것)
    Vector3 userPos;
    float userScale = 1f, userAngle;

    int squeeze;
    bool squeezed => squeeze > 0;
    bool gesturing;
    Vector2 startA, startB;
    Vector3 startPos;
    float startScale, startAngle;

    /// <summary>두 손가락으로 종이를 움직이는 중인지</summary>
    public bool Gesturing => gesturing;

    /// <summary>처음 자리 그대로인지</summary>
    public bool IsHome => Mathf.Abs(userScale - 1f) < 0.01f && Mathf.Abs(Mathf.DeltaAngle(userAngle, 0f)) < 0.5f
                          && (userPos - homePos).sqrMagnitude < 4f;

    public float Scale => userScale;
    public float Angle => userAngle;

    void SaveHome()
    {
        if (homeSaved) return;
        homeSaved = true;
        homePos = paper.localPosition;
        userPos = homePos;
    }

    /// <summary>처음 자리로 (새 쪽지마다, 맞춤 버튼)</summary>
    public void ResetView()
    {
        SaveHome();
        userPos = homePos;
        userScale = 1f;
        userAngle = 0f;
        Apply(true);
    }

    /// <summary>[버튼] 맞춤</summary>
    public void FitTap()
    {
        Sfx.Play(Sfx.Tap);
        ResetView();
    }

    /// <summary>보기를 직접 정하기 (자동 테스트 · 사진용)</summary>
    public void SetView(float scale, float angle, Vector2 offset)
    {
        SaveHome();
        userScale = Mathf.Clamp(scale, minScale, maxScale);
        userAngle = angle;
        userPos = homePos + (Vector3)offset;
        Apply(true);
    }

    /// <summary>아래에 띠가 뜨는 동안 쪽지를 위로 비켜요 (가리는 것 없이). 0 = 원래 보기, 1 = 붓 띠, 2 = 아래 줄까지 올라옴</summary>
    public void Squeeze(int level)
    {
        SaveHome();
        squeeze = Mathf.Clamp(level, 0, squeezeScale.Length - 1);
    }

    /// <summary>지금 비키는 단계</summary>
    public int SqueezeLevel => squeeze;

    void Update()
    {
        SaveHome();
        ReadTouches();
        ReadMouseWheel();
        Apply(false);
        if (fitButton != null) fitButton.SetActive(!IsHome && !squeezed && !gesturing);
    }

    // 손가락 두 개가 닿아 있으면 그 두 점을 따라 종이를 움직여요
    void ReadTouches()
    {
        var screen = Touchscreen.current;
        int count = 0;
        Vector2 a = default, b = default;
        if (screen != null)
        {
            foreach (var t in screen.touches)
            {
                if (!t.press.isPressed) continue;
                if (count == 0) a = t.position.ReadValue();
                else if (count == 1) b = t.position.ReadValue();
                count++;
            }
        }

        if (count >= 2 && !squeezed)
        {
            if (!gesturing) BeginGesture(a, b);
            else MoveGesture(a, b);
        }
        else if (count == 0 && gesturing)
        {
            gesturing = false;
            board.Locked = false;           // 손가락을 다 떼면 다시 그릴 수 있어요
        }
    }

    void BeginGesture(Vector2 a, Vector2 b)
    {
        gesturing = true;
        board.CancelStroke();               // 첫 손가락이 긋던 선은 없던 일로
        board.Locked = true;
        startA = a; startB = b;
        startPos = userPos; startScale = userScale; startAngle = userAngle;
    }

    void MoveGesture(Vector2 a, Vector2 b)
    {
        var parent = (RectTransform)paper.parent;
        Vector2 v0 = startB - startA, v1 = b - a;
        if (v0.magnitude < 1f) return;
        float scale = Mathf.Clamp(startScale * v1.magnitude / v0.magnitude, minScale, maxScale);
        float angle = startAngle + Vector2.SignedAngle(v0, v1);

        // 처음 두 손가락 가운데 있던 종이 위의 점이, 지금 두 손가락 가운데에 오도록
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, (startA + startB) * 0.5f, null, out Vector2 c0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, (a + b) * 0.5f, null, out Vector2 c1);
        Vector2 offset = Quaternion.Euler(0f, 0f, -startAngle) * ((Vector3)c0 - startPos) / startScale;
        Vector2 now = (Vector2)(Quaternion.Euler(0f, 0f, angle) * offset) * scale;
        userScale = scale;
        userAngle = angle;
        userPos = ClampPos((Vector3)(c1 - now));
    }

    // 에디터 · 마우스 : 휠로 키우고 줄이기
    void ReadMouseWheel()
    {
        var mouse = Mouse.current;
        if (mouse == null || squeezed) return;
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f) return;
        userScale = Mathf.Clamp(userScale * (wheel > 0f ? 1.1f : 1f / 1.1f), minScale, maxScale);
    }

    // 종이가 책상 밖으로 다 나가 버리지 않게 가운데가 책상 안에 머물러요
    Vector3 ClampPos(Vector3 p)
    {
        Rect r = ((RectTransform)paper.parent).rect;
        p.x = Mathf.Clamp(p.x, r.xMin + 120f, r.xMax - 120f);
        p.y = Mathf.Clamp(p.y, r.yMin + 120f, r.yMax - 120f);
        return p;
    }

    // 목표 보기로 (instant 가 아니면 부드럽게 따라가요)
    void Apply(bool instant)
    {
        Vector3 pos = squeezed ? homePos + new Vector3(0f, squeezeLift[squeeze], 0f) : userPos;
        float scale = squeezed ? squeezeScale[squeeze] : userScale;
        float angle = squeezed ? 0f : userAngle;
        if (instant || gesturing)
        {
            paper.localPosition = pos;
            paper.localScale = Vector3.one * scale;
            paper.localRotation = Quaternion.Euler(0f, 0f, angle);
            return;
        }
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
        paper.localPosition = Vector3.Lerp(paper.localPosition, pos, k);
        paper.localScale = Vector3.one * Mathf.Lerp(paper.localScale.x, scale, k);
        paper.localRotation = Quaternion.Slerp(paper.localRotation, Quaternion.Euler(0f, 0f, angle), k);
    }
}

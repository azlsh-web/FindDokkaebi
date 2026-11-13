using UnityEngine;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────
// PaperView : 책상 위 쪽지를 두 손가락으로 키우고 · 돌리고 · 옮기기 (ibis Paint 처럼)
// ─────────────────────────────────────────────────────────────
// - 한 손가락은 그리기만 해요 (DrawingBoard). 두 번째 손가락이 닿으면 그리던 선을 취소하고 종이를 움직여요.
// - 손가락을 모두 떼면 다시 그릴 수 있어요.
// - 종이는 처음에 책상 위에 똑바로 놓여요.
// - 오른쪽 위 "전체 화면" 버튼 : 누르면 쪽지가 똑바로 서서 그리는 자리(fullArea)를 가득 채우고, 키우기 · 돌리기 · 옮기기가 멈춰요.
//   한 번 더 누르면 누르기 전 보기로 돌아가요. 새 쪽지는 언제나 전체 화면이 아닌 처음 자리에서 시작해요.
// - 그림의 실제 크기(512x604)는 그대로예요. 화면에서 보이는 크기와 각도만 바뀌어서 판정과는 상관없어요.
// - 에디터에서는 마우스 휠로 키우고 줄이고, 오른쪽 버튼을 누른 채 끌면 쪽지 가운데를 중심으로 돌아가요.
public class PaperView : MonoBehaviour
{
    [SerializeField] RectTransform paper;       // 쪽지 (그림판 · 쪽지 글이 함께 움직여요)
    [SerializeField] DrawingBoard board;
    [SerializeField] GameObject fitButton;      // 전체 화면 켜고 끄기
    [SerializeField] RectTransform fullArea;    // 전체 화면일 때 쪽지가 채우는 자리 (위 HUD 와 아래 띠 사이)
    [SerializeField] float fullMargin = 20f;
    [SerializeField] float minScale = 0.6f;
    [SerializeField] float maxScale = 3f;

    Vector3 homePos;
    bool homeSaved;

    // 지금 보기 (squeeze 가 아닐 때 사용자가 정한 것)
    Vector3 userPos;
    float userScale = 1f, userAngle;

    // 전체 화면 (켜기 전 보기를 기억해요)
    bool full;
    Vector3 savedPos;
    float savedScale, savedAngle;

    // 에디터 · 마우스 오른쪽 끌기로 돌리기
    bool mouseRotating;
    float lastMouseAngle;

    bool gesturing;
    Vector2 startA, startB;
    Vector3 startPos;
    float startScale, startAngle;

    /// <summary>두 손가락으로 종이를 움직이는 중인지</summary>
    public bool Gesturing => gesturing;

    /// <summary>전체 화면인지 (이때는 키우기 · 돌리기 · 옮기기가 멈춰요)</summary>
    public bool IsFull => full;

    public float Scale => userScale;
    public float Angle => userAngle;

    void SaveHome()
    {
        if (homeSaved) return;
        homeSaved = true;
        homePos = paper.localPosition;
        userPos = homePos;
    }

    /// <summary>처음 자리로 (새 쪽지마다). 전체 화면도 꺼요</summary>
    public void ResetView()
    {
        SaveHome();
        full = false;
        userPos = homePos;
        userScale = 1f;
        userAngle = 0f;
        Apply(true);
    }

    /// <summary>[버튼] 전체 화면 켜고 끄기</summary>
    public void FitTap()
    {
        Sfx.Play(Sfx.Tap);
        SetFull(!full);
    }

    /// <summary>전체 화면 켜기 · 끄기 (끄면 켜기 전 보기로)</summary>
    public void SetFull(bool on)
    {
        SaveHome();
        if (on == full) return;
        full = on;
        if (on)
        {
            savedPos = userPos; savedScale = userScale; savedAngle = userAngle;
            FullView(out userPos, out userScale);
            userAngle = 0f;
        }
        else
        {
            userPos = savedPos; userScale = savedScale; userAngle = savedAngle;
        }
    }

    // fullArea 를 쪽지 비율 그대로 가장 크게 채우는 자리와 크기
    void FullView(out Vector3 pos, out float scale)
    {
        var parent = (RectTransform)paper.parent;
        if (fullArea == null) { pos = homePos; scale = 1f; return; }
        var c = new Vector3[4];
        fullArea.GetWorldCorners(c);
        Vector3 a = parent.InverseTransformPoint(c[0]), b = parent.InverseTransformPoint(c[2]);
        pos = (a + b) * 0.5f;
        pos.z = homePos.z;
        Rect r = paper.rect;
        scale = Mathf.Min((Mathf.Abs(b.x - a.x) - fullMargin * 2f) / r.width, (Mathf.Abs(b.y - a.y) - fullMargin * 2f) / r.height);
    }

    /// <summary>보기를 직접 정하기 (자동 테스트 · 사진용)</summary>
    public void SetView(float scale, float angle, Vector2 offset)
    {
        SaveHome();
        full = false;
        userScale = Mathf.Clamp(scale, minScale, maxScale);
        userAngle = angle;
        userPos = homePos + (Vector3)offset;
        Apply(true);
    }

    void Update()
    {
        SaveHome();
        ReadTouches();
        ReadMouseWheel();
        ReadMouseRotate();
        Apply(false);
        if (fitButton != null) fitButton.SetActive(!gesturing);
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

        if (count >= 2)
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
        if (full) return;                   // 전체 화면이면 그대로 (그리기만 잠가요)
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
        if (mouse == null || full) return;
        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) < 0.01f) return;
        userScale = Mathf.Clamp(userScale * (wheel > 0f ? 1.1f : 1f / 1.1f), minScale, maxScale);
    }

    // 에디터 · 마우스 : 오른쪽 버튼을 누른 채 끌면 쪽지 가운데를 중심으로 돌아가요 (그리기는 왼쪽 버튼만)
    void ReadMouseRotate()
    {
        var mouse = Mouse.current;
        if (mouse == null || full || gesturing || !mouse.rightButton.isPressed) { mouseRotating = false; return; }
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, paper.position);
        Vector2 d = mouse.position.ReadValue() - center;
        if (d.sqrMagnitude < 100f) return;                  // 가운데 바로 위에서는 각도가 튀어요
        float now = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        if (mouseRotating) userAngle += Mathf.DeltaAngle(lastMouseAngle, now);
        mouseRotating = true;
        lastMouseAngle = now;
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
        Vector3 pos = userPos;
        float scale = userScale;
        float angle = userAngle;
        if (instant || gesturing || mouseRotating)
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// DrawingBoard : 쪽지(그림판). 마우스/터치로 선을 긋는 기능만 담당해요.
// ─────────────────────────────────────────────────────────────
// 원리
//   1) 흰색으로 꽉 찬 "텍스처(Texture2D)" = 픽셀 그림 한 장을 만든다.
//   2) 그 텍스처를 화면의 RawImage(이미지 칸)에 붙여서 보여준다.
//   3) 마우스를 누르거나 끌 때마다, 그 위치의 픽셀들을 지금 고른 색(또는 흰색=지우개)으로 바꾼다.
//
// 되돌리기 원리
//   - 선을 긋기 "직전"의 픽셀 배열을 통째로 복사해 목록(undoStack)에 쌓아둬요.
//   - 되돌리기를 누르면 마지막 복사본으로 되돌려요. (최대 20번까지 기억)
//
// 사용법
//   - 이 스크립트는 RawImage 가 있는 오브젝트(DrawPanel 안의 "Board")에 붙어 있어요.
//   - IPointerDownHandler / IDragHandler / IPointerUpHandler 는 uGUI 의 "클릭/드래그 이벤트를 받겠다"는 약속이에요.
//     그래서 Input System 코드를 따로 쓰지 않아도 마우스와 터치 모두 동작해요.
//     (씬에 EventSystem 오브젝트가 있어야 이벤트가 들어와요.)
//   - 색·굵기·지우개 버튼은 ToolBar 가 이 스크립트의 함수를 불러서 바꿔요.
[RequireComponent(typeof(RawImage))]
public class DrawingBoard : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("그림판 크기 (픽셀)")]
    [SerializeField] int textureSize = 512;

    [Header("붓 설정")]
    [Tooltip("지우개 반지름 (픽셀)")]
    [SerializeField] int eraserRadius = 18;

    [Tooltip("되돌리기를 몇 번까지 기억할지")]
    [SerializeField] int maxUndo = 20;

    // ── 바깥(GameManager, ToolBar)에서 읽기만 하는 값들 ──
    // { get; private set; } = 다른 스크립트는 읽을 수만 있고, 바꾸는 건 이 스크립트만 가능.

    /// <summary>쪽지에 그린 게 있는지. 빈 쪽지면 제출·되돌리기·모두 지우기 버튼을 막아요.</summary>
    public bool HasDrawn { get; private set; }

    /// <summary>되돌릴 수 있는지</summary>
    public bool CanUndo => undoStack.Count > 0;

    /// <summary>지금까지 그린 그림. 판정·비교 화면에서 그대로 다시 써요.</summary>
    public Texture2D Texture { get; private set; }

    // ── 이 스크립트 안에서만 쓰는 값들 ──
    RawImage rawImage;      // 텍스처를 보여줄 화면 칸
    RectTransform rect;     // 화면 칸의 위치/크기 정보
    Color32[] pixels;       // 텍스처의 모든 픽셀 색 (가로 x 세로 개)
    readonly List<Color32[]> undoStack = new List<Color32[]>();
    Color32 inkColor = new Color32(0, 0, 0, 255); // 지금 펜 색
    int penRadius = 6;      // 지금 펜 굵기(반지름)
    bool isEraser;          // true 면 지우개 모드
    Vector2 lastPixel;      // 직전에 그린 점 (선을 끊기지 않게 잇는 데 사용)

    static readonly Color32 Paper = new Color32(255, 255, 255, 255); // 쪽지 색(흰색)

    // Awake : 이 오브젝트가 "처음 켜질 때" 한 번 실행돼요.
    // ※ 그림판은 꺼져 있는 DrawPanel 안에 있어서, 게임 시작 때는 Awake 가 아직 안 불려요.
    //   그래서 준비 작업을 Init() 으로 빼고, 다른 함수들도 먼저 Init() 을 부르게 했어요.
    void Awake()
    {
        Init();
    }

    // 텍스처 준비. 이미 준비됐으면 아무것도 안 해요. (여러 번 불러도 안전)
    void Init()
    {
        if (Texture != null) return;

        rawImage = GetComponent<RawImage>();
        rect = GetComponent<RectTransform>();

        // 그림을 담을 빈 텍스처 만들기
        Texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Texture.filterMode = FilterMode.Bilinear;
        pixels = new Color32[textureSize * textureSize];

        rawImage.texture = Texture;
        ResetBoard();
    }

    // ───────────── GameManager / ToolBar 에서 부르는 함수들 ─────────────

    /// <summary>새 쪽지 : 흰색으로 비우고 되돌리기 기록도 지워요. (새 도전마다)</summary>
    public void ResetBoard()
    {
        Init();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Paper;
        undoStack.Clear();
        ApplyPixels();
        HasDrawn = false;
    }

    /// <summary>
    /// 미리 만든 그림(512x512 픽셀)을 쪽지에 통째로 넣기.
    /// 자동 검사(판정 전수 검사)에서 손그림 샘플을 넣을 때 써요. 나중에 "저장한 그림 불러오기"에도 쓸 수 있어요.
    /// </summary>
    public void LoadPixels(Color32[] source)
    {
        Init();
        if (source.Length != pixels.Length) return;
        SaveUndo();
        Array.Copy(source, pixels, pixels.Length);
        ApplyPixels();
        HasDrawn = ScanForInk();
    }

    /// <summary>[버튼] 모두 지우기 : 흰색으로 비우지만, 되돌리기로 다시 살릴 수 있어요.</summary>
    public void Clear()
    {
        Init();
        if (!HasDrawn) return;
        SaveUndo();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Paper;
        ApplyPixels();
        HasDrawn = false;
    }

    /// <summary>[버튼] 되돌리기 : 마지막 선 하나 전으로</summary>
    public void Undo()
    {
        if (!CanUndo) return;
        int last = undoStack.Count - 1;
        pixels = undoStack[last];
        undoStack.RemoveAt(last);
        ApplyPixels();
        HasDrawn = ScanForInk();
    }

    /// <summary>펜 색 바꾸기 (지우개 모드는 꺼져요)</summary>
    public void SetColor(Color color)
    {
        inkColor = color;
        isEraser = false;
    }

    /// <summary>펜 굵기(반지름 픽셀) 바꾸기</summary>
    public void SetPenSize(int radius)
    {
        penRadius = radius;
    }

    /// <summary>지우개 모드</summary>
    public void UseEraser()
    {
        isEraser = true;
    }

    // ───────────── 마우스/터치 이벤트 ─────────────

    // 누른 순간 : 되돌리기용 복사본 저장 → 점 하나 찍기
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!TryGetPixel(eventData, out Vector2 p)) return;
        SaveUndo();
        DrawDot(p);
        lastPixel = p;
        ApplyPixels();
    }

    // 누른 채로 끄는 동안 : 직전 점에서 지금 점까지 선 잇기
    public void OnDrag(PointerEventData eventData)
    {
        if (!TryGetPixel(eventData, out Vector2 p)) return;
        DrawLine(lastPixel, p);
        lastPixel = p;
        ApplyPixels();
    }

    // 손을 뗀 순간 : 지우개로 다 지웠을 수도 있으니 한 번 확인
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isEraser) HasDrawn = ScanForInk();
    }

    // ───────────── 내부 계산 ─────────────

    void SaveUndo()
    {
        undoStack.Add((Color32[])pixels.Clone());                 // 지금 그림을 통째로 복사
        if (undoStack.Count > maxUndo) undoStack.RemoveAt(0);     // 너무 많으면 가장 오래된 것 버림
    }

    // 쪽지에 흰색이 아닌 픽셀이 하나라도 있는지
    bool ScanForInk()
    {
        foreach (Color32 c in pixels)
        {
            if (c.r != 255 || c.g != 255 || c.b != 255) return true;
        }
        return false;
    }

    // 화면 좌표(마우스 위치) → 텍스처의 픽셀 좌표로 바꾸기
    bool TryGetPixel(PointerEventData eventData, out Vector2 pixel)
    {
        pixel = Vector2.zero;

        // 1) 화면 좌표를 이 칸(RawImage) 기준 좌표로 바꾼다
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return false;

        // 2) 칸의 왼쪽 아래 = (0,0), 오른쪽 위 = (1,1) 인 비율로 바꾼다
        Rect r = rect.rect;
        float u = (local.x - r.x) / r.width;
        float v = (local.y - r.y) / r.height;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false; // 칸 밖이면 무시

        // 3) 비율 × 텍스처 크기 = 픽셀 좌표
        pixel = new Vector2(u * textureSize, v * textureSize);
        return true;
    }

    // 두 점 사이를 촘촘히 점으로 채워 선처럼 보이게 하기
    void DrawLine(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance)); // 1픽셀마다 점 하나
        for (int i = 0; i <= steps; i++)
        {
            DrawDot(Vector2.Lerp(from, to, i / (float)steps));
        }
    }

    // 동그란 붓 자국 하나 찍기
    void DrawDot(Vector2 center)
    {
        int radius = isEraser ? eraserRadius : penRadius;
        Color32 color = isEraser ? Paper : inkColor;
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);

        // 중심 주변 네모 영역을 돌면서, 원 안에 들어가는 픽셀만 칠한다
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            if (y < 0 || y >= textureSize) continue;
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || x >= textureSize) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    pixels[y * textureSize + x] = color; // 2차원(x,y) → 1차원 배열 번호
                }
            }
        }

        if (!isEraser) HasDrawn = true;
    }

    // 바꾼 픽셀 배열을 실제 텍스처에 반영 (이걸 해야 화면에 보여요)
    void ApplyPixels()
    {
        Texture.SetPixels32(pixels);
        Texture.Apply();
    }
}

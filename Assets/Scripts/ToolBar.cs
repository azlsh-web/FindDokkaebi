using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// ToolBar : 쪽지 아래 그리기 도구 줄 (색 · 지우개 · 굵기 · 되돌리기 · 모두 지우기 · 색 섞기)
// ─────────────────────────────────────────────────────────────
// - 버튼을 누르면 DrawingBoard 의 함수를 불러서 펜 색·굵기를 바꿔요.
// - 지금 고른 색과 도구는 "테두리(Frame)"로 표시해요. (기획서 4장)
// - 단계마다 쓸 수 있는 색이 달라요 → Setup(ColorMode) 에서 버튼을 보이거나 숨겨요.
//     BlackOnly : 검정만 / Primaries : 검정·빨강·노랑·파랑 / MixTwo·MixFree : + 혼합색 칸 + 색 섞기
// - 빈 쪽지에서는 되돌리기·모두 지우기가 비활성 (Update 에서 매 프레임 확인)
public class ToolBar : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] DrawingBoard board;
    [SerializeField] ColorMixer mixer;

    [Header("색 버튼 (순서 고정 : 검정, 빨강, 노랑, 파랑)")]
    [SerializeField] GameObject[] colorButtons;
    [SerializeField] GameObject[] colorFrames;

    [Header("혼합색 칸 + 색 섞기 버튼")]
    [SerializeField] GameObject mixedSlotButton;
    [SerializeField] Image mixedSlotSwatch;
    [SerializeField] GameObject mixedSlotFrame;
    [SerializeField] GameObject mixOpenButton;

    [Header("지우개 · 굵기 (가는, 보통, 굵은)")]
    [SerializeField] GameObject eraserFrame;
    [SerializeField] GameObject[] sizeFrames;

    [Header("되돌리기 · 모두 지우기")]
    [SerializeField] Button undoButton;
    [SerializeField] Button clearButton;

    // 색 4가지와 굵기 3가지 (반지름 픽셀)
    public static readonly Color[] Palette =
    {
        new Color32(0, 0, 0, 255),       // 검정
        new Color32(225, 45, 45, 255),   // 빨강
        new Color32(250, 210, 40, 255),  // 노랑
        new Color32(40, 90, 220, 255),   // 파랑
    };
    static readonly int[] PenSizes = { 3, 6, 12 };

    const int MixedSlot = 4;    // selectedColor 가 4 면 혼합색
    const int Eraser = -1;      // selectedColor 가 -1 이면 지우개

    ColorMode mode;
    int selectedColor;
    int selectedSize;
    bool hasMixed;              // 혼합색을 한 번이라도 만들었는지
    Color mixedColor;

    /// <summary>단계 시작 때 GameManager 가 불러요. 쓸 수 있는 색에 맞춰 버튼을 정리.</summary>
    public void Setup(ColorMode colorMode)
    {
        mode = colorMode;
        bool primaries = mode != ColorMode.BlackOnly;
        bool mixing = mode == ColorMode.MixTwo || mode == ColorMode.MixFree;

        colorButtons[0].SetActive(true);
        for (int i = 1; i < colorButtons.Length; i++) colorButtons[i].SetActive(primaries);
        mixedSlotButton.SetActive(mixing);
        mixOpenButton.SetActive(mixing);

        // 혼합색은 단계마다 새로 만들어요
        hasMixed = false;
        mixedSlotSwatch.color = new Color(0.85f, 0.85f, 0.85f);
        mixer.OnMixed = OnMixed;
        mixer.Close();

        SelectSize(1);
        SelectColor(0);
    }

    void Update()
    {
        undoButton.interactable = board.CanUndo;
        clearButton.interactable = board.HasDrawn;
    }

    // ───────────── 버튼 함수 ─────────────

    /// <summary>[버튼] 색 고르기. 0 검정, 1 빨강, 2 노랑, 3 파랑</summary>
    public void SelectColor(int index)
    {
        selectedColor = index;
        board.SetColor(Palette[index]);
        RefreshFrames();
    }

    /// <summary>[버튼] 혼합색 칸 (만든 색이 있을 때만)</summary>
    public void SelectMixed()
    {
        if (!hasMixed) return;
        selectedColor = MixedSlot;
        board.SetColor(mixedColor);
        RefreshFrames();
    }

    /// <summary>[버튼] 지우개</summary>
    public void SelectEraser()
    {
        selectedColor = Eraser;
        board.UseEraser();
        RefreshFrames();
    }

    /// <summary>[버튼] 굵기. 0 가는 선, 1 보통 선, 2 굵은 선</summary>
    public void SelectSize(int index)
    {
        selectedSize = index;
        board.SetPenSize(PenSizes[index]);
        RefreshFrames();
    }

    /// <summary>[버튼] 색 섞기 → 팔레트 열기</summary>
    public void OpenMixer()
    {
        mixer.Open(mode == ColorMode.MixTwo ? 2 : 3);
    }

    /// <summary>[버튼] 되돌리기</summary>
    public void Undo() => board.Undo();

    /// <summary>[버튼] 모두 지우기</summary>
    public void ClearAll() => board.Clear();

    // ───────────── 내부 ─────────────

    // 팔레트에서 색을 다 섞으면 불려요 → 혼합색 칸에 저장하고 바로 그 색 선택
    void OnMixed(Color color, string colorName)
    {
        hasMixed = true;
        mixedColor = color;
        mixedSlotSwatch.color = color;
        SelectMixed();
    }

    // 고른 것만 테두리 보이게
    void RefreshFrames()
    {
        for (int i = 0; i < colorFrames.Length; i++) colorFrames[i].SetActive(selectedColor == i);
        mixedSlotFrame.SetActive(selectedColor == MixedSlot);
        eraserFrame.SetActive(selectedColor == Eraser);
        for (int i = 0; i < sizeFrames.Length; i++) sizeFrames[i].SetActive(selectedSize == i);
    }
}

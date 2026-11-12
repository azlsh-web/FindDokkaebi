using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// ToolBar : 그리기 화면 아래 띠
// ─────────────────────────────────────────────────────────────
// 아래 띠  : [붓] | [↶ 되돌리기] [↷ 다시 하기]                  [다 그렸어!] (GameManager)
//   - 붓 끝 색 = 지금 색. 붓을 누르면 띠 바로 위에 팔레트가 떠요 (꼬리가 붓을 가리켜요). 한 번 더 누르면 닫혀요.
//   - 쪽지에 그리기 시작하면 팔레트는 닫혀요.
// 팔레트   : [검정] | [빨강] [노랑] [파랑] ( [↶ 물감 빼기] ) | [가는] [보통] [굵은]   (화면 폭에 맞춰 고르게)
//   - 색을 고르면 물방울이 붓 끝으로 날아가 첨벙하고 색이 바뀌어요 (ColorMixer).
//   - 섞는 단계(4 · 5단계)는 빨강 · 노랑 · 파랑이 물감이에요 : 누를 때마다 넣어져 섞이고, 옆에 넣은 횟수가 떠요.
//   - 굵기 점 세 개 중 고른 점은 지금 색으로 칠하고 고리를 둘러요.
// 단계마다 쓸 수 있는 색 : BlackOnly 검정 / Primaries 4색 / MixTwo · MixFree 4색 + 물감 섞기
public class ToolBar : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] DrawingBoard board;
    [SerializeField] ColorMixer mixer;
    [SerializeField] PaperView paperView;

    [Header("아래 띠")]
    [SerializeField] Button undoButton;
    [SerializeField] Button redoButton;
    [SerializeField] RectTransform bar;             // 띠 전체 (색 섞기 시범 때 위로 올려요)
    [SerializeField] float raiseHeight = 360f;

    [Header("팔레트")]
    [SerializeField] GameObject palette;
    [SerializeField] GameObject blackFrame;         // 검정을 골랐으면 고리
    [SerializeField] GameObject primaryDivider;     // 검정 | 빨강 · 노랑 · 파랑 사이 구분선
    [SerializeField] GameObject[] primaryButtons;   // 빨강, 노랑, 파랑
    [SerializeField] GameObject[] primaryFrames;
    [SerializeField] GameObject paintUndo;          // 물감 빼기 (섞는 단계만)
    [SerializeField] Image[] sizeDots;              // 가는 · 보통 · 굵은 점
    [SerializeField] GameObject[] sizeFrames;

    // 색 4가지 (검정 · 빨강 · 노랑 · 파랑)
    public static readonly Color[] Palette =
    {
        new Color32(0, 0, 0, 255),
        new Color32(225, 45, 45, 255),
        new Color32(250, 210, 40, 255),
        new Color32(40, 90, 220, 255),
    };
    static readonly int[] PenSizes = { 3, 6, 11 };       // 가는 · 보통 · 굵은 (반지름 픽셀)
    static readonly Color DotColor = new Color32(140, 107, 78, 255);    // 고르지 않은 굵기 점

    ColorMode mode;
    int selectedSize;
    Vector2 barHome;
    bool barHomeSaved;
    bool raised;

    /// <summary>지금 붓 색</summary>
    public Color CurrentColor => mixer.ResultColor;
    public bool PanelOpen => palette.activeSelf;
    public int SelectedSize => selectedSize;

    /// <summary>단계 시작 때 GameManager 가 불러요. 쓸 수 있는 색에 맞춰 팔레트를 정리</summary>
    public void Setup(ColorMode colorMode)
    {
        mode = colorMode;
        bool primaries = mode != ColorMode.BlackOnly;
        bool mixing = mode == ColorMode.MixTwo || mode == ColorMode.MixFree;
        primaryDivider.SetActive(primaries);
        foreach (GameObject b in primaryButtons) b.SetActive(primaries);
        paintUndo.SetActive(mixing);

        mixer.OnChanged = OnColorChanged;
        board.OnStrokeBegin = OnStrokeBegin;
        mixer.Setup(mixing, mode == ColorMode.MixTwo ? 2 : 3);
        ClosePanel();
        Raise(false);
        SelectSize(1);
    }

    /// <summary>색 섞기 시범이 끝나면 붓을 다시 검정으로 (플레이어가 처음부터 섞어요)</summary>
    public void ResetPaints()
    {
        mixer.Setup(mode == ColorMode.MixTwo || mode == ColorMode.MixFree, mode == ColorMode.MixTwo ? 2 : 3);
    }

    void Update()
    {
        undoButton.interactable = board.CanUndo;
        redoButton.interactable = board.CanRedo;
    }

    // 쪽지에 선을 긋기 시작했어요
    void OnStrokeBegin()
    {
        mixer.MarkUsed();                                   // 섞은 색으로 그렸으면 다음 물감은 새로 섞기
        if (palette.activeSelf && !raised) ClosePanel();    // 그리기 시작하면 팔레트는 닫혀요
    }

    // ───────────── 버튼 함수 ─────────────

    /// <summary>[버튼] 붓 : 팔레트 열기 · 닫기</summary>
    public void BrushTap()
    {
        Sfx.Play(Sfx.Tap);
        if (palette.activeSelf) ClosePanel(); else OpenPanel();
    }

    public void OpenPanel()
    {
        palette.SetActive(true);
        paperView.Squeeze(raised ? 2 : 1);
        Refresh();
    }

    public void ClosePanel()
    {
        palette.SetActive(false);
        if (paperView != null) paperView.Squeeze(0);
    }

    /// <summary>예전 이름 (색 섞기 시범 · 자동 테스트) : 팔레트 열기</summary>
    public void OpenMixer() => OpenPanel();

    /// <summary>[버튼] 검정</summary>
    public void BlackTap() => mixer.SetPure(0);

    /// <summary>[버튼] 빨강 · 노랑 · 파랑. index 0 빨강, 1 노랑, 2 파랑. 섞는 단계면 물감을 넣어요</summary>
    public void PaintTap(int index) => mixer.AddPaint(index);

    /// <summary>[버튼] 마지막에 넣은 물감 빼기</summary>
    public void PaintUndo() => mixer.UndoPaint();

    /// <summary>색 하나로 고르기 (0 검정, 1 빨강, 2 노랑, 3 파랑). 자동 테스트 · 녹화용</summary>
    public void SelectColor(int index) => mixer.SetPure(index);

    /// <summary>[버튼] 굵기. 0 가는, 1 보통, 2 굵은</summary>
    public void SelectSize(int index)
    {
        selectedSize = index;
        board.SetPenSize(PenSizes[index]);
        Refresh();
    }

    /// <summary>[버튼 ↶] 되돌리기</summary>
    public void Undo() => board.Undo();

    /// <summary>[버튼 ↷] 다시 하기</summary>
    public void Redo() => board.Redo();

    /// <summary>모두 지우기 (자동 테스트용. 화면에는 버튼이 없어요)</summary>
    public void ClearAll() => board.Clear();

    /// <summary>색 섞기 시범 동안 아래 띠(와 팔레트)를 대사 상자 위로 올려요. 아무것도 가리지 않게</summary>
    public void Raise(bool up)
    {
        if (!barHomeSaved) { barHome = bar.anchoredPosition; barHomeSaved = true; }
        raised = up;
        bar.anchoredPosition = barHome + (up ? new Vector2(0f, raiseHeight) : Vector2.zero);
        if (paperView != null) paperView.Squeeze(palette.activeSelf ? (up ? 2 : 1) : 0);
    }

    // ───────────── 내부 ─────────────

    void OnColorChanged(Color color)
    {
        board.SetColor(color);
        Refresh();
    }

    // 고른 색 고리 · 굵기 점 (고른 점은 지금 색으로)
    void Refresh()
    {
        int pure = mixer.PureIndex;
        blackFrame.SetActive(pure == 0);
        bool mixing = mode == ColorMode.MixTwo || mode == ColorMode.MixFree;
        for (int i = 0; i < primaryFrames.Length; i++) primaryFrames[i].SetActive(!mixing && pure == i + 1);
        for (int i = 0; i < sizeDots.Length; i++)
        {
            sizeDots[i].color = i == selectedSize ? CurrentColor : DotColor;
            sizeFrames[i].SetActive(i == selectedSize);
        }
    }
}

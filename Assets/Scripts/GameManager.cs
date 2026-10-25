using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// GameManager : 게임 진행(흐름)을 책임지는 "사회자" 스크립트
// ─────────────────────────────────────────────────────────────
// 기획서의 플레이 흐름을 그대로 따라가요.
//
//   [도입] → [관찰 3초] → [그리기 60초] → [제출: 간식 -1] → [판정] → [결과]
//                                ↑                                   │
//                                └──────── 다시 그리기 ───────────────┤
//                                                                    ↓
//                                                       [원본 비교] → 처음으로
//
// 화면 구조
//   - 씬은 하나(Game)뿐이고, 화면마다 "패널(Panel)"이라는 UI 묶음이 있어요.
//   - 한 번에 패널 하나만 켜고(SetActive(true)) 나머지는 꺼요. → ShowPanel() 함수
//   - 위쪽 HUD(간식 수, 남은 시간)는 항상 켜져 있어요.
//
// 판정
//   - 기획서는 AI(LLM)가 그림을 보고 F1~F3 을 판정해요.
//   - MVP 에서는 AI 대신 "테스트 입력": 판정 화면의 체크박스(Toggle)로
//     어떤 특징이 그려졌는지 사람이 직접 고르게 했어요.
//   - 나중에 AI 를 붙일 때는 판정 결과(bool[])를 만드는 부분만 바꾸면 돼요. → OnJudgeConfirmed()
public class GameManager : MonoBehaviour
{
    // 지금 어떤 화면(단계)에 있는지 나타내는 목록
    enum State { Intro, Observe, Draw, Judge, Result, Compare }

    // ───────────── Inspector 에서 연결하는 것들 ─────────────
    // [SerializeField] = private 이지만 Inspector 창에 보이게 해서 드래그로 연결할 수 있게 함

    [Header("데이터")]
    [Tooltip("이번 단계 데이터 파일 (Assets/Data/Stage1_Bat)")]
    [SerializeField] StageData stage;

    [Header("규칙 (기획서 수치)")]
    [SerializeField] int startSnacks = 3;           // 시작 간식 수
    [SerializeField] float observeSeconds = 3f;     // 관찰 시간
    [SerializeField] float drawSeconds = 60f;       // 그리기 시간 (향이 타는 시간)

    [Header("항상 보이는 HUD")]
    [SerializeField] TMP_Text snackText;            // 왼쪽 위 "간식 3개"
    [SerializeField] TMP_Text stageText;            // 오른쪽 위 "1단계 방망이"
    [SerializeField] TMP_Text timerText;            // 오른쪽 위 "향 58초"

    [Header("패널(화면) 6개")]
    [SerializeField] GameObject introPanel;
    [SerializeField] GameObject observePanel;
    [SerializeField] GameObject drawPanel;
    [SerializeField] GameObject judgePanel;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject comparePanel;

    [Header("도입 화면")]
    [SerializeField] TMP_Text introStoryText;

    [Header("관찰 화면")]
    [SerializeField] Image observeImage;            // 원본 그림
    [SerializeField] TMP_Text observeCountText;     // "3", "2", "1"

    [Header("그리기 화면")]
    [SerializeField] DrawingBoard board;            // 쪽지(그림판)
    [SerializeField] Button submitButton;           // 제출 버튼 (빈 쪽지면 비활성)

    [Header("판정 화면 (테스트 입력)")]
    [SerializeField] Toggle[] featureToggles;       // F1, F2, F3 체크박스
    [SerializeField] TMP_Text[] featureToggleLabels;

    [Header("결과 화면")]
    [SerializeField] TMP_Text resultTitleText;      // "방망이를 찾았다!"
    [SerializeField] Image resultItemImage;         // 도깨비가 가져온 물건
    [SerializeField] TMP_Text resultFeatureText;    // 특징별 맞음/빠뜨림 목록
    [SerializeField] Button retryButton;            // 다시 그리기
    [SerializeField] TMP_Text compareButtonLabel;   // "비교하기" / "포기하고 비교"

    [Header("비교 화면")]
    [SerializeField] RawImage compareMyDrawing;     // 왼쪽: 내 그림
    [SerializeField] Image compareOriginal;         // 오른쪽: 원본

    // ───────────── 게임 도중 바뀌는 값들 ─────────────
    State state;
    int snacks;         // 남은 간식
    float timeLeft;     // 관찰/그리기 남은 시간

    // Start : 게임이 시작되면 한 번 실행
    void Start()
    {
        snacks = startSnacks;
        stageText.text = stage.stageTitle;

        // 판정 화면 체크박스 글자를 데이터 파일의 특징 이름으로 채우기
        for (int i = 0; i < featureToggleLabels.Length; i++)
        {
            featureToggleLabels[i].text = $"F{i + 1}  {stage.features[i]}";
        }

        ShowIntro();
    }

    // Update : 매 프레임(1초에 수십 번) 실행. 여기서는 타이머만 처리해요.
    void Update()
    {
        if (state == State.Observe)
        {
            timeLeft -= Time.deltaTime;                         // 지난 시간만큼 빼기
            observeCountText.text = Mathf.CeilToInt(timeLeft).ToString();
            if (timeLeft <= 0f) StartDraw();                    // 3초 끝 → 그리기
        }
        else if (state == State.Draw)
        {
            timeLeft -= Time.deltaTime;
            timerText.text = $"향 {Mathf.CeilToInt(timeLeft)}초";
            submitButton.interactable = board.HasDrawn;         // 그린 게 있어야 제출 가능
            if (timeLeft <= 0f) Submit();                       // 향이 다 타면 자동 제출
        }
    }

    // ───────────── 각 화면으로 넘어가는 함수들 ─────────────
    // public 함수는 버튼의 OnClick 에 연결돼 있어요.

    /// <summary>도입 화면 : 이야기와 "기억할 모습 보기" 버튼</summary>
    void ShowIntro()
    {
        state = State.Intro;
        introStoryText.text = stage.introStory;
        timerText.text = "";
        UpdateSnackText();
        ShowPanel(introPanel);
    }

    /// <summary>[버튼] 기억할 모습 보기 → 3초 관찰 시작</summary>
    public void StartObserve()
    {
        state = State.Observe;
        timeLeft = observeSeconds;
        observeImage.sprite = stage.originalSprite;
        ShowPanel(observePanel);
    }

    /// <summary>그리기 시작 : 빈 쪽지, 60초. (다시 그리기도 여기로 와요)</summary>
    void StartDraw()
    {
        state = State.Draw;
        timeLeft = drawSeconds;
        board.Clear();
        board.UsePen();
        ShowPanel(drawPanel);
    }

    /// <summary>[버튼] 제출 (또는 시간 초과로 자동 제출) : 간식 1개 사용</summary>
    public void Submit()
    {
        if (state != State.Draw) return;    // 두 번 눌림 방지

        snacks--;
        UpdateSnackText();
        timerText.text = "";

        // 판정 화면으로. 체크박스는 모두 꺼진 상태로 시작
        state = State.Judge;
        foreach (Toggle t in featureToggles) t.isOn = false;
        ShowPanel(judgePanel);
    }

    /// <summary>[버튼] 판정 결과 보내기 → 결과 계산</summary>
    public void OnJudgeConfirmed()
    {
        // ★ 판정 결과 = 특징마다 그렸는지(true) / 빠뜨렸는지(false)
        //   MVP : 체크박스 값을 그대로 사용 (테스트 입력)
        //   나중 : AI 가 보내준 JSON 의 status == "present" 인지로 바꾸면 됨
        bool[] present = new bool[featureToggles.Length];
        for (int i = 0; i < present.Length; i++) present[i] = featureToggles[i].isOn;

        ShowResult(present);
    }

    /// <summary>결과 화면 : 몇 개 맞혔는지에 따라 가져오는 물건이 달라져요.</summary>
    void ShowResult(bool[] present)
    {
        state = State.Result;

        // 맞힌 특징 개수 세기
        int count = 0;
        foreach (bool p in present) if (p) count++;

        bool success = count == present.Length;   // 전부 맞혀야 성공

        // 기획서 규칙
        //   전부 맞음     → 진짜 방망이 (성공)
        //   일부만 맞음   → 엉뚱한 방망이 (부분 성공)
        //   하나도 없음   → 아무것도 안 가져옴 (실패)
        if (success)
        {
            resultTitleText.text = $"도깨비가 {stage.targetName}를 찾아왔다!";
            ShowItem(stage.originalSprite);
        }
        else if (count > 0)
        {
            resultTitleText.text = $"엉뚱한 {stage.targetName}를 가져왔다...";
            ShowItem(stage.wrongSprite);
        }
        else
        {
            resultTitleText.text = "도깨비가 아무것도 찾지 못했다...";
            ShowItem(null);
        }

        // 특징별 결과 목록 만들기 (줄바꿈 \n 으로 한 줄씩)
        string lines = "";
        for (int i = 0; i < present.Length; i++)
        {
            string mark = present[i] ? "<color=#2E7D32>[O] 그림</color>"
                                     : "<color=#C62828>[X] 빠뜨림</color>";
            lines += $"{mark}   F{i + 1} {stage.features[i]}\n";
        }
        if (!success && snacks <= 0) lines += "\n간식이 다 떨어졌어요.";
        resultFeatureText.text = lines;

        // 버튼 정리 : 실패했고 간식이 남아 있을 때만 다시 그리기 가능
        retryButton.gameObject.SetActive(!success && snacks > 0);
        compareButtonLabel.text = success ? "내 그림과 비교하기" : "포기하고 비교하기";

        ShowPanel(resultPanel);
    }

    /// <summary>[버튼] 다시 그리기 : 원본은 다시 안 보여주고 바로 빈 쪽지로</summary>
    public void Retry()
    {
        StartDraw();
    }

    /// <summary>[버튼] 비교하기 : 왼쪽 내 그림, 오른쪽 원본</summary>
    public void ShowCompare()
    {
        state = State.Compare;
        compareMyDrawing.texture = board.Texture;
        compareOriginal.sprite = stage.originalSprite;
        ShowPanel(comparePanel);
    }

    /// <summary>[버튼] 처음부터 다시 : 간식을 채우고 도입 화면으로</summary>
    public void Restart()
    {
        snacks = startSnacks;
        ShowIntro();
    }

    // ───────────── 도우미 함수 ─────────────

    // 패널 하나만 켜고 나머지는 끄기
    void ShowPanel(GameObject panel)
    {
        introPanel.SetActive(panel == introPanel);
        observePanel.SetActive(panel == observePanel);
        drawPanel.SetActive(panel == drawPanel);
        judgePanel.SetActive(panel == judgePanel);
        resultPanel.SetActive(panel == resultPanel);
        comparePanel.SetActive(panel == comparePanel);
    }

    // 결과 그림 칸에 물건 보여주기 (null 이면 숨김)
    void ShowItem(Sprite sprite)
    {
        resultItemImage.sprite = sprite;
        resultItemImage.enabled = sprite != null;
    }

    void UpdateSnackText()
    {
        snackText.text = $"간식 {snacks}개";
    }
}

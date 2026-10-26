using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// GameManager : 게임 진행(흐름)을 책임지는 "사회자" 스크립트
// ─────────────────────────────────────────────────────────────
// 기획서 2장 플레이 흐름을 5단계 동안 반복해요.
//
//   [시작 화면]
//       ↓ 소풍 준비하러 가기
//   [단계 도입] → [관찰 3초] → [그리기 60초] → [제출: 간식 -1] → [판정] → [결과]
//                                   ↑                                       │
//                                   └──────────── 다시 그리기 ───────────────┤ (실패 + 간식 남음)
//                                                                            ↓ 비교하기 / 포기하고 비교
//                                                                      [원본 비교]
//                                                                            ↓ 다음 단계 (5단계 뒤엔)
//                                                                     [이야기 종료]
//
// 화면 구조
//   - 씬은 하나(Game)뿐이고, 화면마다 "패널(Panel)"이라는 UI 묶음이 있어요.
//   - 한 번에 패널 하나만 켜요. → ShowPanel()
//   - 위쪽 HUD(간식, 단계, 향 타이머)는 게임 중에만 보여요.
//
// 간식 규칙 : 단계마다 3개로 시작, 제출할 때마다 1개. 다 쓰면 다시 그리기 불가 → 포기하고 비교.
//
// 판정 (기획서 6장) : 항상 자동이에요. 기다리는 동안 도깨비가 쪽지를 들여다보며 고민해요.
//   - UserSettings/OpenAI.json 에 키가 있으면 : OpenAIJudge (멀티모달 LLM → JSON)
//   - 키가 없으면 : LocalJudge (기획서의 대체 기술 : HSV 색 분석 + 모양 계산, Unity 안에서)
//   - AI 오류 : 간식을 돌려주고, 그리던 그림 그대로 다시 제출하게 해요.
public class GameManager : MonoBehaviour
{
    // 지금 어떤 화면에 있는지
    enum State { Title, StageIntro, Observe, Draw, Judge, Result, Compare, Ending }

    // ───────────── Inspector 에서 연결하는 것들 ─────────────
    // [SerializeField] = private 이지만 Inspector 창에 보이게 해서 드래그로 연결할 수 있게 함

    [Header("데이터 (순서대로 1~5단계)")]
    [SerializeField] StageData[] stages;

    [Header("규칙 (기획서 수치)")]
    [SerializeField] int snacksPerStage = 3;
    [SerializeField] float observeSeconds = 3f;
    [SerializeField] float drawSeconds = 60f;

    [Header("다른 스크립트")]
    [SerializeField] DrawingBoard board;
    [SerializeField] ToolBar toolBar;
    [SerializeField] OpenAIJudge judge;

    [Header("HUD")]
    [SerializeField] GameObject hud;
    [SerializeField] TMP_Text snackText;            // 왼쪽 위 "간식 3개"
    [SerializeField] TMP_Text stageText;            // 오른쪽 위 "1단계 방망이"
    [SerializeField] GameObject timerGroup;         // 향 (그리는 동안만 보임)
    [SerializeField] TMP_Text timerText;            // "향 58초"
    [SerializeField] Image incenseBar;              // 타들어 가는 향 막대

    [Header("패널(화면)")]
    [SerializeField] GameObject titlePanel;
    [SerializeField] GameObject stageIntroPanel;
    [SerializeField] GameObject observePanel;
    [SerializeField] GameObject drawPanel;
    [SerializeField] GameObject judgePanel;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject comparePanel;
    [SerializeField] GameObject endingPanel;

    [Header("단계 도입 화면")]
    [SerializeField] TMP_Text introStageNumber;
    [SerializeField] TMP_Text introTargetName;
    [SerializeField] TMP_Text introStory;
    [SerializeField] TMP_Text introRules;

    [Header("관찰 화면")]
    [SerializeField] TMP_Text observeTitle;
    [SerializeField] Image observeImage;
    [SerializeField] TMP_Text observeCountText;

    [Header("그리기 화면")]
    [SerializeField] TMP_Text drawTitle;
    [SerializeField] Button submitButton;

    [Header("판정 화면")]
    [SerializeField] RawImage judgeDrawing;         // 도깨비에게 건넨 쪽지
    [SerializeField] TMP_Text judgeStatusText;      // "도깨비가 쪽지를 들여다보는 중..."
    [SerializeField] float thinkSeconds = 1.5f;     // 자동 판정 때 도깨비가 고민하는 시간
    [SerializeField] GameObject backToDrawButton;   // AI 오류 때만 보임

    [Header("결과 화면")]
    [SerializeField] TMP_Text resultTitleText;
    [SerializeField] Image resultItemImage;         // 도깨비가 가져온 물건
    [SerializeField] RawImage resultMyDrawing;      // 내 그림
    [SerializeField] TMP_Text resultFeatureText;    // 특징별 [O]/[X]
    [SerializeField] Image boyFace;                 // 소년 표정
    [SerializeField] TMP_Text boyLineText;          // 소년 대사
    [SerializeField] TMP_Text resultSnackText;      // "남은 간식 2개"
    [SerializeField] Button retryButton;
    [SerializeField] TMP_Text compareButtonLabel;
    [SerializeField] Sprite boyHappy;
    [SerializeField] Sprite boySurprised;
    [SerializeField] GameObject boySurprisedMark;   // 당황했을 때 소년 옆에 뜨는 "!?"
    [SerializeField] Sprite primaryButtonSprite;    // 주 버튼 그림 (코랄)
    [SerializeField] Sprite secondaryButtonSprite;  // 보조 버튼 그림 (크림)

    [Header("비교 화면")]
    [SerializeField] RawImage compareMyDrawing;
    [SerializeField] Image compareOriginal;
    [SerializeField] TMP_Text compareFeatureText;
    [SerializeField] TMP_Text nextButtonLabel;

    [Header("이야기 종료 화면")]
    [SerializeField] TMP_Text endingRecordText;

    // ───────────── 게임 도중 바뀌는 값들 ─────────────
    State state;
    int stageIndex;         // 지금 단계 번호 (0부터)
    int snacks;             // 이번 단계 남은 간식
    float timeLeft;         // 관찰/그리기 남은 시간
    float savedDrawTime;    // AI 오류로 그리기로 돌아갈 때 쓸 남은 시간
    int[] attempts;         // 단계별 제출 횟수 (이야기 종료 화면 기록용)
    bool[] cleared;         // 단계별 성공 여부
    int[] snacksLeft;       // 단계별 남은 간식 (이야기 종료 화면 기록용)
    int judgeTicket;        // 제출할 때마다 +1. 늦게 도착한 옛 판정 결과를 버리는 데 써요.

    StageData Stage => stages[stageIndex];

    // Start : 게임이 시작되면 한 번 실행
    void Start()
    {
        ShowTitle();
    }

    // Update : 매 프레임 실행. 타이머만 처리해요.
    void Update()
    {
        if (state == State.Observe)
        {
            timeLeft -= Time.deltaTime;                             // 지난 시간만큼 빼기
            observeCountText.text = Mathf.CeilToInt(timeLeft).ToString();
            if (timeLeft <= 0f) StartDraw(false);                   // 3초 끝 → 그리기
        }
        else if (state == State.Draw)
        {
            timeLeft -= Time.deltaTime;
            timerText.text = $"향 {Mathf.CeilToInt(Mathf.Max(0f, timeLeft))}초";
            incenseBar.fillAmount = timeLeft / drawSeconds;         // 향이 타들어 감
            submitButton.interactable = board.HasDrawn;             // 빈 쪽지면 제출 불가
            if (timeLeft <= 0f) Submit();                           // 향이 다 타면 자동 제출
        }
    }

    // ───────────── 화면 넘기기 (public = 버튼 OnClick 에 연결) ─────────────

    /// <summary>시작 화면</summary>
    void ShowTitle()
    {
        state = State.Title;
        hud.SetActive(false);
        ShowPanel(titlePanel);
    }

    /// <summary>[버튼] 소풍 준비하러 가기</summary>
    public void StartGame()
    {
        attempts = new int[stages.Length];
        cleared = new bool[stages.Length];
        snacksLeft = new int[stages.Length];
        StartStage(0);
    }

    /// <summary>단계 도입 화면 : 이야기 + 새 규칙 + 앞에서 배운 규칙</summary>
    void StartStage(int index)
    {
        state = State.StageIntro;
        stageIndex = index;
        snacks = snacksPerStage;
        toolBar.Setup(Stage.colorMode);

        hud.SetActive(true);
        timerGroup.SetActive(false);
        stageText.text = Stage.Title;
        UpdateSnackText();

        introStageNumber.text = $"{Stage.stageNumber}단계";
        introTargetName.text = Stage.targetName;
        introStory.text = Stage.introStory;
        string learned = string.IsNullOrEmpty(Stage.learnedRules) ? "없음 (첫 단계예요)" : Stage.learnedRules;
        introRules.text =
            $"<b>새로 배울 규칙</b>\n{Stage.newRule}\n\n" +
            $"<b>앞에서 배운 규칙</b>  <color=#7A6A5A>{learned}</color>\n\n" +
            $"<size=90%><color=#7A6A5A>관찰 {observeSeconds:0}초 · 그리기 {drawSeconds / 60f:0}분 · 간식 {snacksPerStage}개 (제출할 때 1개)</color></size>";
        ShowPanel(stageIntroPanel);
    }

    /// <summary>[버튼] 기억할 모습 보기 → 3초 관찰</summary>
    public void StartObserve()
    {
        state = State.Observe;
        timeLeft = observeSeconds;
        observeTitle.text = $"{Stage.targetName}{Josa(Stage.targetName, "을", "를")} 잘 기억하세요!";
        observeImage.sprite = Stage.originalSprite;
        ShowPanel(observePanel);
    }

    /// <summary>그리기 시작. keepDrawing = true 면 그리던 그림을 그대로 (AI 오류로 돌아올 때)</summary>
    void StartDraw(bool keepDrawing)
    {
        state = State.Draw;
        if (keepDrawing)
        {
            timeLeft = savedDrawTime;
        }
        else
        {
            timeLeft = drawSeconds;
            board.ResetBoard();
        }
        drawTitle.text = $"기억한 {Stage.targetName}{Josa(Stage.targetName, "을", "를")} 그려주세요 (원본은 볼 수 없어요)";
        timerGroup.SetActive(true);
        ShowPanel(drawPanel);
    }

    /// <summary>[버튼] 제출 (또는 시간 초과로 자동 제출) : 쪽지 + 간식 1개를 도깨비에게</summary>
    public void Submit()
    {
        if (state != State.Draw) return;    // 두 번 눌림 방지

        state = State.Judge;
        savedDrawTime = Mathf.Max(timeLeft, 10f);
        snacks--;
        attempts[stageIndex]++;
        UpdateSnackText();
        timerGroup.SetActive(false);

        judgeDrawing.texture = board.Texture;
        backToDrawButton.SetActive(false);
        ShowPanel(judgePanel);

        // 기다리는 동안 도깨비가 갸웃갸웃 (SimpleAnim)
        judgeStatusText.text = "쪽지와 간식 1개를 도깨비에게 건넸어요.\n도깨비가 쪽지를 들여다보는 중...";
        int ticket = ++judgeTicket;
        if (judge.IsReady)
        {
            // AI 판정 (OpenAI) : 응답이 오면 OnVerdict, 문제가 생기면 OnJudgeError
            StartCoroutine(judge.Judge(Stage, board.Texture,
                v => { if (ticket == judgeTicket) OnVerdict(v); },
                e => { if (ticket == judgeTicket) OnJudgeError(e); }));
        }
        else
        {
            // 자동 판정 (Unity 안에서 계산) : 도깨비가 잠깐 고민한 뒤 결과
            StartCoroutine(LocalJudgeRoutine(ticket));
        }
    }

    // 도깨비가 고민하는 시간만큼 기다렸다가 LocalJudge 로 판정
    IEnumerator LocalJudgeRoutine(int ticket)
    {
        yield return new WaitForSeconds(thinkSeconds);
        if (ticket != judgeTicket || state != State.Judge) yield break; // 그 사이 다른 화면으로 갔으면 무시
        OnVerdict(LocalJudge.Judge(Stage, board.Texture));
    }

    // 판정 결과가 도착했을 때 (AI 든 자동 판정이든 같은 모양)
    void OnVerdict(OpenAIJudge.Verdict verdict)
    {
        if (state != State.Judge) return;
        SaveSubmission(verdict);
        ShowResult(verdict.present, verdict.notes);
    }

    // AI 판정이 실패했을 때 : 간식 돌려주고 다시 제출하게 (기획서 6장 오류 처리)
    void OnJudgeError(string message)
    {
        if (state != State.Judge) return;
        snacks++;
        attempts[stageIndex]--;
        judgeTicket++;      // 이 제출은 끝난 것으로 (늦게 오는 결과는 무시)
        UpdateSnackText();
        judgeStatusText.text = $"판정 중에 문제가 생겼어요. 간식을 돌려줬어요.\n<size=70%>{message}</size>";
        backToDrawButton.SetActive(true);
        Debug.LogWarning("[OpenAIJudge] " + message);
    }

    /// <summary>[버튼] (AI 오류 후) 그림으로 돌아가서 다시 제출</summary>
    public void BackToDraw()
    {
        StartDraw(true);
    }

    /// <summary>결과 화면 : 맞힌 특징 수에 따라 도깨비가 가져오는 물건이 달라져요.</summary>
    void ShowResult(bool[] present, string[] notes)
    {
        state = State.Result;

        int count = 0;
        int firstMissing = -1;
        for (int i = 0; i < present.Length; i++)
        {
            if (present[i]) count++;
            else if (firstMissing < 0) firstMissing = i;
        }
        bool success = count == present.Length;
        cleared[stageIndex] = success;
        boySurprisedMark.SetActive(!success);

        // 기획서 규칙
        //   전부 맞음     → 진짜 물건 (성공)
        //   일부만 맞음   → 엉뚱한 물건 : "처음 빠뜨린 특징"이 빠진 물건이 와요
        //   하나도 없음   → 아무것도 안 가져옴
        string name = Stage.targetName;
        if (success)
        {
            resultTitleText.text = $"도깨비가 {name}{Josa(name, "을", "를")} 찾아왔다!";
            ShowItem(Stage.originalSprite);
            boyFace.sprite = boyHappy;
            boyLineText.text = $"찾았다! 바로 이 {name}{Josa(name, "이야", "야")}!\n고마워 도깨비야!";
        }
        else if (count > 0)
        {
            resultTitleText.text = $"엉뚱한 {name}{Josa(name, "을", "를")} 가져왔다...";
            ShowItem(Stage.features[firstMissing].wrongSprite);
            boyFace.sprite = boySurprised;
            boyLineText.text = $"어? 이 {name}{Josa(name, "은", "는")} 좀 이상한데...\n<size=80%>'{Stage.features[firstMissing].name}' 부분이 달라!</size>";
        }
        else
        {
            resultTitleText.text = "도깨비가 아무것도 찾지 못했다...";
            ShowItem(null);
            boyFace.sprite = boySurprised;
            boyLineText.text = "도깨비가 빈손으로 왔어...\n특징을 더 그려줘야겠다.";
        }

        // 특징별 결과 목록 (AI 판정이면 근거도 작게)
        string lines = "";
        for (int i = 0; i < present.Length; i++)
        {
            string mark = present[i] ? "<color=#2E7D32>[O] 그림</color>" : "<color=#C62828>[X] 빠뜨림</color>";
            lines += $"{mark}  {Stage.features[i].name}\n";
            // 빠뜨린 특징만 "왜 빠뜨렸다고 봤는지" 작게 덧붙여요 (화면이 너무 빽빽하지 않게)
            if (!present[i] && notes != null && !string.IsNullOrEmpty(notes[i])) lines += $"<size=70%><color=#7A6A5A>      {notes[i]}</color></size>\n";
        }
        resultFeatureText.text = lines;

        resultMyDrawing.texture = board.Texture;
        resultSnackText.text = (!success && snacks <= 0)
            ? "간식이 다 떨어졌어요. 원본과 비교하고 넘어가요."
            : $"남은 간식 {snacks}개";

        // 실패했고 간식이 남아 있을 때만 다시 그리기
        retryButton.gameObject.SetActive(!success && snacks > 0);
        compareButtonLabel.text = success ? "내 그림과 비교하기" : "포기하고 비교하기";
        // 성공이면 비교하기가 주된 버튼(코랄), 아니면 다시 그리기가 주된 버튼이라 비교하기는 보조(크림)
        compareButtonLabel.transform.parent.GetComponent<Image>().sprite = success ? primaryButtonSprite : secondaryButtonSprite;

        ShowPanel(resultPanel);
    }

    /// <summary>[버튼] 다시 그리기 : 원본은 다시 안 보여주고 빈 쪽지로, 시간은 다시 60초</summary>
    public void Retry()
    {
        StartDraw(false);
    }

    /// <summary>[버튼] 비교하기 : 왼쪽 내 그림, 오른쪽 원본, 아래 특징 설명</summary>
    public void ShowCompare()
    {
        state = State.Compare;
        compareMyDrawing.texture = board.Texture;
        compareOriginal.sprite = Stage.originalSprite;

        string hint = "<b>원본의 특징</b>\n";
        foreach (Feature f in Stage.features) hint += "- " + f.name + "\n";
        compareFeatureText.text = hint;

        snacksLeft[stageIndex] = snacks;   // 이야기 종료 화면 기록용

        bool last = stageIndex == stages.Length - 1;
        nextButtonLabel.text = last ? "오늘의 이야기 마치기" : "다음 단계로";
        ShowPanel(comparePanel);
    }

    /// <summary>[버튼] 다음 단계로 / 오늘의 이야기 마치기</summary>
    public void NextStage()
    {
        if (stageIndex + 1 < stages.Length) StartStage(stageIndex + 1);
        else ShowEnding();
    }

    /// <summary>이야기 종료 : 단계별 기록</summary>
    void ShowEnding()
    {
        state = State.Ending;
        hud.SetActive(false);

        string record = "";
        for (int i = 0; i < stages.Length; i++)
        {
            string result = cleared[i] ? "<color=#2E7D32>찾았어요</color>" : "<color=#C62828>포기했어요</color>";
            record += $"{stages[i].Title}  -  {result}  (제출 {attempts[i]}번, 남은 간식 {snacksLeft[i]}개)\n";
        }
        endingRecordText.text = record;
        ShowPanel(endingPanel);
    }

    /// <summary>[버튼] 처음으로</summary>
    public void BackToTitle()
    {
        ShowTitle();
    }

    // ───────────── 도우미 함수 ─────────────

    // (에디터에서만) 제출한 그림과 판정 결과를 Temp/Submissions 에 저장 → 판정 기준을 조절할 때 참고해요.
    // Temp 폴더는 Git 에 올라가지 않고, 실제 게임 빌드에서는 이 코드가 아예 빠져요.
    void SaveSubmission(OpenAIJudge.Verdict verdict)
    {
#if UNITY_EDITOR
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "Submissions");
        System.IO.Directory.CreateDirectory(dir);
        string name = $"{System.DateTime.Now:MMdd_HHmmss}_stage{Stage.stageNumber}";
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), board.Texture.EncodeToPNG());
        string log = "";
        for (int i = 0; i < verdict.present.Length; i++)
            log += $"{Stage.features[i].id} {(verdict.present[i] ? "O" : "X")} {Stage.features[i].name} : {verdict.notes?[i]}\n";
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".txt"), log);
#endif
    }

    // 패널 하나만 켜고 나머지는 끄기
    void ShowPanel(GameObject panel)
    {
        GameObject[] all = { titlePanel, stageIntroPanel, observePanel, drawPanel, judgePanel, resultPanel, comparePanel, endingPanel };
        foreach (GameObject p in all) p.SetActive(p == panel);
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

    /// <summary>
    /// 한국어 조사 고르기. 마지막 글자에 받침이 있으면 withBatchim, 없으면 withoutBatchim.
    /// 예) Josa("가방","을","를") → "을",  Josa("방망이","을","를") → "를"
    /// </summary>
    public static string Josa(string word, string withBatchim, string withoutBatchim)
    {
        if (string.IsNullOrEmpty(word)) return withoutBatchim;
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return withoutBatchim;      // 한글이 아니면
        bool hasBatchim = (last - 0xAC00) % 28 != 0;                     // 한글 글자 번호 규칙
        return hasBatchim ? withBatchim : withoutBatchim;
    }
}

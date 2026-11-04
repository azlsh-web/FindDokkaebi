using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// GameManager : 게임 진행(흐름)을 책임지는 "사회자" 스크립트
// ─────────────────────────────────────────────────────────────
// 글로 설명하는 대신 대사와 그림으로 진행해요. 대사나 화면을 누르면 순서대로 넘어가요.
//
//   [시작 화면]
//       ↓ 소풍 가기
//   [대화 → 도깨비의 쪽지] → [관찰 3초] → [그리기 60초] → [제출: 간식 -1] → [판정 대기] → [결과]
//                                             ↑                                            │
//                                             └──────── 다시 (이전 그림에 이어 그리기) ─────┤ (실패 + 간식 남음)
//                                                                                          ↓ 성공 / 포기 / 간식 없음
//                                                                                       [채점표]
//                                                                                          ↓ 다음 단계 (5단계 뒤엔)
//                                                                                     [이야기 종료]
//
// 1단계에서 방망이를 못 찾으면 채점표 뒤에 도깨비가 스스로 찾아오는 장면(Oops)이 나와요.
//
// 화면 구조
//   - 씬은 하나(Game)뿐이고, 화면마다 "패널(Panel)"이라는 UI 묶음이 있어요. 한 번에 패널 하나만 켜요.
//   - 위쪽 HUD(간식, 일시정지, 향)는 그리기부터 채점표까지만 보여요.
//
// 간식 규칙 : 단계마다 3개, 제출할 때마다 1개. 다 쓰면 다시 그릴 수 없어요.
//
// 판정 : 항상 자동이에요. 도깨비의 찾는 동작은 한 바퀴씩 반복하고, 응답이 오면 그 바퀴를 마치고 넘어가요.
//   AI 오류가 나면 간식을 돌려주고, 그리던 그림 그대로 다시 제출하게 해요.
//
// 대사는 GameScript, 소리는 Sfx 에 있어요.
public class GameManager : MonoBehaviour
{
    enum State { Title, Dialogue, Observe, Draw, Judge, Result, Compare, Oops, Ending }
    enum ResultPhase { Dokkaebi, Boy, Choose, Error }

    // ───────────── Inspector 에서 연결하는 것들 ─────────────

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
    [SerializeField] Image[] snackIcons;            // 오른쪽 위 복주머니 (쓴 간식은 흐리게)
    [SerializeField] SimpleAnim[] snackAnims;       // 단계 첫 그리기 때 들어오는 움직임
    [SerializeField] GameObject timerGroup;         // 향 (그리는 동안만 보임)
    [SerializeField] TMP_Text timerText;            // 남은 초 숫자 "58"
    [SerializeField] IncenseBurn incense;           // 타들어 가는 향
    [SerializeField] GameObject pausePanel;         // 일시정지 화면

    [Header("배경 : 시작, 게임 중, 이야기 종료 그림이 달라요")]
    [SerializeField] Image backgroundImage;
    [SerializeField] Sprite startBackground;
    [SerializeField] Sprite gameBackground;
    [SerializeField] Sprite endingBackground;

    [Header("에디터 전용 : 몇 단계부터 시작할지 (웹 빌드는 항상 1단계부터)")]
    [SerializeField] int editorStartStage = 1;

    [Header("패널(화면)")]
    [SerializeField] GameObject titlePanel;
    [SerializeField] GameObject dialoguePanel;
    [SerializeField] GameObject observePanel;
    [SerializeField] GameObject drawPanel;
    [SerializeField] GameObject judgePanel;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject comparePanel;
    [SerializeField] GameObject oopsPanel;
    [SerializeField] GameObject endingPanel;

    [Header("캐릭터 그림")]
    [SerializeField] Sprite faceBoySurprised;
    [SerializeField] Sprite faceBoyHappy;
    [SerializeField] Sprite faceDokkaebi;
    [SerializeField] Sprite dokkaebiNormal;
    [SerializeField] Sprite dokkaebiNoBat;          // 1단계 : 방망이를 찾기 전
    [SerializeField] Sprite dokkaebiSwing;

    [Header("대화 화면")]
    [SerializeField] DialogueBox dialogueBox;
    [SerializeField] RectTransform dialogueBoy;     // 처음 혼자 놀랄 때는 가운데에 크게
    [SerializeField] SimpleAnim dialogueBoyAnim;
    [SerializeField] GameObject dialogueDokkaebi;
    [SerializeField] Image dialogueDokkaebiImage;
    [SerializeField] SimpleAnim dialogueDokkaebiAnim;
    [SerializeField] GameObject noteCard;           // 도깨비의 쪽지
    [SerializeField] TMP_Text noteTitle;
    [SerializeField] GameObject[] noteRows;
    [SerializeField] Image[] noteRowBacks;
    [SerializeField] TMP_Text[] noteRowTexts;

    [Header("관찰 화면")]
    [SerializeField] Image observeImage;
    [SerializeField] TMP_Text observeCount;
    [SerializeField] SimpleAnim observeCountAnim;

    [Header("그리기 화면")]
    [SerializeField] TMP_Text drawTitle;
    [SerializeField] Button submitButton;

    [Header("판정 대기 화면")]
    [SerializeField] RawImage judgeDrawing;         // 화면 한 곳에 떠 있는 내 그림
    [SerializeField] RectTransform judgeSnackOut;   // 도깨비 쪽으로 빠져나가는 간식
    [SerializeField] float thinkSeconds = 1.5f;     // 자동 판정 때 고민하는 시간
    [SerializeField] float judgeEnterSeconds = 0.7f;// 도깨비가 들어오는 시간
    [SerializeField] float roamLoopSeconds = 6f;    // 찾는 동작 한 바퀴

    [Header("결과 화면")]
    [SerializeField] DialogueBox resultBox;
    [SerializeField] Image resultItemImage;         // 도깨비가 가져온 물건
    [SerializeField] GameObject resultItemShadow;
    [SerializeField] GameObject resultSparkles;     // 성공했을 때 반짝임
    [SerializeField] Image resultDokkaebi;
    [SerializeField] SimpleAnim resultDokkaebiAnim;
    [SerializeField] GameObject choiceRow;          // 다시 · 포기

    [Header("채점표")]
    [SerializeField] RawImage compareMyDrawing;
    [SerializeField] Image compareOriginal;
    [SerializeField] GameObject[] scoreRows;
    [SerializeField] TMP_Text[] scoreTexts;
    [SerializeField] GameObject[] scoreCircles;     // 맞으면 초록 동그라미
    [SerializeField] GameObject[] scoreSlashes;     // 틀리면 빨간 빗금

    [Header("1단계를 못 찾았을 때 : 도깨비가 스스로 찾아와요")]
    [SerializeField] DialogueBox oopsBox;
    [SerializeField] GameObject oopsHold;           // 대파를 든 도깨비
    [SerializeField] GameObject oopsRun;            // 뛰어나가는 도깨비
    [SerializeField] GameObject oopsBack;           // 방망이를 들고 돌아온 도깨비
    [SerializeField] float oopsRunSeconds = 1.8f;

    [Header("이야기 종료 화면 : 물건과 남은 간식 (방망이, 가방, 돗자리, 김밥, 친구 순)")]
    [SerializeField] DialogueBox endingBox;
    [SerializeField] Image[] endingItems;
    [SerializeField] GameObject[] endingSnacks;     // 단계마다 3개씩 차례로

    static readonly Color UsedSnackColor = new Color(0.55f, 0.55f, 0.55f, 0.35f);
    static readonly Color NoteNewRowColor = new Color32(251, 226, 182, 255);

    // 대화 화면의 주인공 자리 (앞 = 처음 혼자, 뒤 = 도깨비와 함께)
    static readonly Vector2 BoyAlonePos = new Vector2(0f, -618f);
    static readonly Vector2 BoyAloneSize = new Vector2(570f, 867f);
    static readonly Vector2 BoyWithPos = new Vector2(-267f, -768f);
    static readonly Vector2 BoyWithSize = new Vector2(450f, 684f);

    // ───────────── 게임 도중 바뀌는 값들 ─────────────
    State state;
    ResultPhase resultPhase;
    int stageIndex;
    int snacks;
    float timeLeft;
    float savedDrawTime;    // AI 오류로 그리기로 돌아갈 때 쓸 남은 시간
    int[] attempts;
    bool[] cleared;
    int[] snacksLeft;
    Sprite[] itemsFound;    // 단계별로 마지막에 받은 물건 (이야기 종료 장면에 놓여요)
    int judgeTicket;        // 제출할 때마다 +1. 늦게 도착한 옛 판정 결과를 버리는 데 써요.
    float judgeStartTime;
    bool snackIntroPending; // 단계 첫 그리기 때만 간식이 들어와요
    int dialogueLine;
    int lastCount;
    int outcome;            // 0 전부 맞음, 1 일부 틀림, 2 전부 틀림
    bool[] lastPresent;
    bool batLost;           // 1단계를 못 찾음
    int oopsPhase;
    bool endingAsking;
    Vector2 boyTargetPos, boyTargetSize;

    StageData Stage => stages[stageIndex];

    void Start()
    {
        ShowTitle();
    }

    void Update()
    {
        if (state == State.Dialogue)
        {
            dialogueBoy.anchoredPosition = Vector2.Lerp(dialogueBoy.anchoredPosition, boyTargetPos, Time.deltaTime * 8f);
            dialogueBoy.sizeDelta = Vector2.Lerp(dialogueBoy.sizeDelta, boyTargetSize, Time.deltaTime * 8f);
        }
        else if (state == State.Observe)
        {
            timeLeft -= Time.deltaTime;
            int count = Mathf.Clamp(Mathf.CeilToInt(timeLeft), 1, 3);   // 가운데 숫자 하나가 3 → 2 → 1
            if (count != lastCount)
            {
                lastCount = count;
                observeCount.text = count.ToString();
                observeCountAnim.Play(SimpleAnim.Kind.NumPop);
                Sfx.Play(Sfx.Count);
            }
            if (timeLeft <= 0f) StartDraw(false);
        }
        else if (state == State.Draw)
        {
            timeLeft -= Time.deltaTime;
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, timeLeft)).ToString();
            incense.Show(timeLeft / drawSeconds, timeLeft);         // 향이 타들어 감
            submitButton.interactable = board.HasDrawn;             // 빈 쪽지면 제출 불가
            if (timeLeft <= 0f) Submit();                           // 향이 다 타면 자동 제출
        }
    }

    // ───────────── 시작 ─────────────

    void ShowTitle()
    {
        state = State.Title;
        SetPaused(false);
        hud.SetActive(false);
        backgroundImage.sprite = startBackground;
        Sfx.Bgm(Sfx.BgmRoom);
        ShowPanel(titlePanel);
    }

    /// <summary>[버튼] 소풍 가기</summary>
    public void StartGame()
    {
        Sfx.Play(Sfx.Tap);
        attempts = new int[stages.Length];
        cleared = new bool[stages.Length];
        snacksLeft = new int[stages.Length];
        itemsFound = new Sprite[stages.Length];
        batLost = false;
        backgroundImage.sprite = gameBackground;
        int first = 0;
#if UNITY_EDITOR
        // (에디터 전용) 앞 단계를 건너뛰어 녹화 · 확인. 건너뛴 단계는 찾은 것으로 쳐요
        first = Mathf.Clamp(editorStartStage - 1, 0, stages.Length - 1);
        for (int i = 0; i < first; i++)
        {
            cleared[i] = true;
            attempts[i] = 1;
            snacksLeft[i] = snacksPerStage - 1;
            itemsFound[i] = stages[i].originalSprite;
        }
#endif
        StartStage(first);
    }

    // ───────────── 대화 + 도깨비의 쪽지 ─────────────

    void StartStage(int index)
    {
        state = State.Dialogue;
        stageIndex = index;
        snacks = snacksPerStage;
        snackIntroPending = true;
        lastPresent = null;
        toolBar.Setup(Stage.colorMode);
        hud.SetActive(false);
        timerGroup.SetActive(false);
        backgroundImage.sprite = gameBackground;
        UpdateSnacks();
        Sfx.Bgm(Sfx.BgmRoom);

        noteCard.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebiImage.sprite = index == 0 ? dokkaebiNoBat : dokkaebiNormal;
        FillNote();
        ShowPanel(dialoguePanel);

        bool alone = index == 0;                    // 맨 처음에는 주인공 혼자 놀라요
        boyTargetPos = alone ? BoyAlonePos : BoyWithPos;
        boyTargetSize = alone ? BoyAloneSize : BoyWithSize;
        dialogueBoy.anchoredPosition = boyTargetPos;
        dialogueBoy.sizeDelta = boyTargetSize;
        dialogueLine = 0;
        SayIntroLine();
    }

    void SayIntroLine()
    {
        GameScript.Line line = GameScript.IntroLines(stageIndex)[dialogueLine];
        dialogueBox.Say(line.text, FaceOf(line.face));

        bool alone = stageIndex == 0 && dialogueLine == 0;
        boyTargetPos = alone ? BoyAlonePos : BoyWithPos;
        boyTargetSize = alone ? BoyAloneSize : BoyWithSize;
        if (!alone) dialogueDokkaebi.SetActive(true);

        bool dokkaebiTalks = line.face == GameScript.Face.Dokkaebi;
        dialogueBoyAnim.Play(line.face == GameScript.Face.BoySurprised ? SimpleAnim.Kind.Shake
                             : dokkaebiTalks ? SimpleAnim.Kind.None : SimpleAnim.Kind.Gentle);
        if (dialogueDokkaebi.activeSelf)
            dialogueDokkaebiAnim.Play(dokkaebiTalks ? SimpleAnim.Kind.Gentle : SimpleAnim.Kind.None);
    }

    // 쪽지에 판정 기준 적기 : 1. 2. 3. 번호, 새로 생긴 기준은 색칠
    void FillNote()
    {
        noteTitle.text = $"{Stage.stageNumber}단계 {Stage.targetName}";
        for (int i = 0; i < noteRows.Length; i++)
        {
            bool used = i < Stage.features.Length;
            noteRows[i].SetActive(used);
            if (!used) continue;
            noteRowTexts[i].text = $"{i + 1}.<indent=1.3em>{GameScript.NoBreakParts(Stage.features[i].name)}</indent>";
            bool isNew = Stage.stageNumber > 1 && i == Stage.features.Length - 1;
            noteRowBacks[i].color = isNew ? NoteNewRowColor : Color.clear;
        }
    }

    /// <summary>[화면 터치] 대사 넘기기 → 쪽지 → 관찰</summary>
    public void DialogueTap()
    {
        if (state != State.Dialogue) return;
        if (dialogueBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);

        GameScript.Line[] lines = GameScript.IntroLines(stageIndex);
        if (dialogueLine < lines.Length - 1)
        {
            dialogueLine++;
            SayIntroLine();
        }
        else if (!noteCard.activeSelf)
        {
            // 쪽지가 나오면 캐릭터는 사라지고, 도깨비가 주의할 점을 말해요
            dialogueBoy.gameObject.SetActive(false);
            dialogueDokkaebi.SetActive(false);
            noteCard.SetActive(true);
            dialogueBox.Say(GameScript.CardLine(stageIndex), faceDokkaebi);
        }
        else
        {
            StartObserve();
        }
    }

    /// <summary>3초 관찰 : 원본과 가운데 숫자만</summary>
    public void StartObserve()
    {
        state = State.Observe;
        timeLeft = observeSeconds;
        lastCount = 0;
        observeImage.sprite = Stage.originalSprite;
        observeCount.text = "3";
        ShowPanel(observePanel);
    }

    // ───────────── 그리기 ─────────────

    /// <summary>그리기 시작. keepDrawing = true 면 그리던 그림을 그대로</summary>
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
        drawTitle.text = $"기억나는 {Stage.targetName}{Josa(Stage.targetName, "을", "를")} 그려 줘";
        hud.SetActive(true);
        timerGroup.SetActive(true);
        Sfx.Bgm(Sfx.BgmDraw);
        ShowPanel(drawPanel);
        UpdateSnacks();

        if (snackIntroPending)
        {
            snackIntroPending = false;
            foreach (SimpleAnim a in snackAnims) a.Play(SimpleAnim.Kind.SlideRight);
            Sfx.Play(Sfx.SnackIn);
        }
    }

    /// <summary>[버튼] 제출 (또는 시간 초과로 자동 제출)</summary>
    public void Submit()
    {
        if (state != State.Draw) return;

        state = State.Judge;
        savedDrawTime = Mathf.Max(timeLeft, 10f);
        int usedBefore = snacksPerStage - snacks;
        snacks--;
        attempts[stageIndex]++;
        UpdateSnacks();
        timerGroup.SetActive(false);
        Sfx.Play(Sfx.Submit);
        Sfx.Bgm(Sfx.BgmJudge);

        judgeDrawing.texture = board.Texture;
        // 방금 쓴 간식 자리에서 도깨비 쪽으로 빠져나가요
        Vector2 p = judgeSnackOut.anchoredPosition;
        p.x = snackIcons[Mathf.Clamp(usedBefore, 0, snackIcons.Length - 1)].rectTransform.anchoredPosition.x;
        judgeSnackOut.anchoredPosition = p;
        judgeStartTime = Time.time;
        ShowPanel(judgePanel);

        int ticket = ++judgeTicket;
        //   AI 판정이 안 되면(서버·키·인터넷 문제) 간식을 돌려주고 다시 제출하게 해요 (OnJudgeError).
        //   계산식 자동 판정(LocalJudge)은 자동 테스트(OpenAIJudge.DisableAI)에서만 써요.
        if (OpenAIJudge.DisableAI)
        {
            StartCoroutine(LocalJudgeRoutine(ticket));
        }
        else if (judge.IsReady)
        {
            StartCoroutine(judge.Judge(Stage, board.Texture,
                v => { if (ticket == judgeTicket) StartCoroutine(FinishLoopThen(ticket, () => OnVerdict(v))); },
                e => { if (ticket == judgeTicket) StartCoroutine(FinishLoopThen(ticket, () => OnJudgeError(e))); }));
        }
        else
        {
            StartCoroutine(NoJudgeRoutine(ticket));
        }
    }

    // 응답이 오면 도깨비가 찾는 동작 한 바퀴를 마칠 때까지 기다렸다가 넘어가요
    IEnumerator FinishLoopThen(int ticket, System.Action next)
    {
        float passed = Time.time - judgeStartTime - judgeEnterSeconds;
        float laps = Mathf.Max(1f, Mathf.Ceil(passed / Mathf.Max(0.01f, roamLoopSeconds)));
        float remain = laps * roamLoopSeconds - passed;
        if (remain > 0f) yield return new WaitForSeconds(remain);
        if (ticket == judgeTicket && state == State.Judge) next();
    }

    IEnumerator NoJudgeRoutine(int ticket)
    {
        yield return new WaitForSeconds(thinkSeconds);
        if (ticket == judgeTicket && state == State.Judge)
            yield return FinishLoopThen(ticket, () => OnJudgeError("AI 판정 설정이 없어요 (키 또는 판정 서버 주소)"));
    }

    // (자동 테스트용) 고민하는 시간만큼 기다렸다가 LocalJudge 로 판정
    IEnumerator LocalJudgeRoutine(int ticket)
    {
        yield return new WaitForSeconds(thinkSeconds);
        if (ticket != judgeTicket || state != State.Judge) yield break;
        yield return FinishLoopThen(ticket, () => OnVerdict(LocalJudge.Judge(Stage, board.Texture)));
    }

    void OnVerdict(OpenAIJudge.Verdict verdict)
    {
        if (state != State.Judge) return;
        SaveSubmission(verdict);
        ShowResult(verdict.present, verdict.notes);
    }

    // AI 판정이 실패했을 때 : 간식 돌려주고, 대사를 누르면 그림으로 돌아가요
    void OnJudgeError(string message)
    {
        if (state != State.Judge) return;
        snacks++;
        attempts[stageIndex]--;
        judgeTicket++;      // 이 제출은 끝난 것으로 (늦게 오는 결과는 무시)
        UpdateSnacks();
        Debug.LogWarning("[OpenAIJudge] " + message);

        state = State.Result;
        resultPhase = ResultPhase.Error;
        resultItemImage.enabled = false;
        resultItemShadow.SetActive(false);
        resultSparkles.SetActive(false);
        resultDokkaebi.sprite = stageIndex == 0 ? dokkaebiNoBat : dokkaebiNormal;
        choiceRow.SetActive(false);
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
        resultBox.Say(GameScript.JudgeError, faceDokkaebi);
    }

    /// <summary>(AI 오류 후) 그림으로 돌아가서 다시 제출</summary>
    public void BackToDraw()
    {
        StartDraw(true);
    }

    // ───────────── 결과 ─────────────

    /// <summary>결과 화면 : 맞힌 특징 수에 따라 도깨비가 가져오는 물건이 달라져요.</summary>
    void ShowResult(bool[] present, string[] notes)
    {
        state = State.Result;
        resultPhase = ResultPhase.Dokkaebi;
        lastPresent = present;

        int count = 0;
        foreach (bool ok in present) if (ok) count++;
        bool success = count == present.Length;
        cleared[stageIndex] = success;
        outcome = success ? 0 : count > 0 ? 1 : 2;

        // 판정 기준이 n개면 경우의 수는 2ⁿ 가지예요 (Docs/6_엉뚱한_물건_제작.md)
        //   전부 맞음 → 진짜 물건 A / 일부만 맞음 → 그 조합대로 틀린 A' / 전부 틀림 → 완전히 다른 물건 B
        Sprite item = success ? Stage.originalSprite : Stage.WrongSpriteFor(present);
        resultItemImage.sprite = item;
        resultItemImage.enabled = item != null;
        itemsFound[stageIndex] = item;
        resultItemShadow.SetActive(true);
        resultSparkles.SetActive(success);
        choiceRow.SetActive(false);

        // 1단계를 못 찾았으면 도깨비는 아직 빈손이에요
        bool batMissing = stageIndex == 0 && !success;
        resultDokkaebi.sprite = batMissing ? dokkaebiNoBat : dokkaebiSwing;
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.Swing);
        Sfx.Play(Sfx.Item);
        Sfx.Play(success ? Sfx.Success : Sfx.Fail);
        resultBox.Say(GameScript.DokkaebiResult(stageIndex, outcome), faceDokkaebi);
    }

    /// <summary>[화면 터치] 도깨비 대사 → 주인공 대사 → 다시 · 포기 또는 채점표</summary>
    public void ResultTap()
    {
        if (state != State.Result || resultPhase == ResultPhase.Choose) return;
        if (resultBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);

        if (resultPhase == ResultPhase.Error)
        {
            BackToDraw();
        }
        else if (resultPhase == ResultPhase.Dokkaebi)
        {
            bool wrong = outcome != 0;
            resultPhase = ResultPhase.Boy;
            resultDokkaebi.sprite = stageIndex == 0 && wrong ? dokkaebiNoBat : dokkaebiNormal;
            resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
            string line = wrong && snacks <= 0 ? GameScript.BoyOutOfSnacks : GameScript.BoyResult(outcome);
            resultBox.Say(line, wrong ? faceBoySurprised : faceBoyHappy);
        }
        else if (outcome != 0 && snacks > 0)
        {
            // 대사 상자 자리에 다시 · 포기 버튼이 나란히 나와요
            resultPhase = ResultPhase.Choose;
            resultBox.gameObject.SetActive(false);
            choiceRow.SetActive(true);
        }
        else
        {
            ShowCompare();
        }
    }

    /// <summary>[버튼] 다시 : 이전 그림에 이어 그리기, 시간은 다시 60초</summary>
    public void Retry()
    {
        Sfx.Play(Sfx.Tap);
        savedDrawTime = drawSeconds;
        StartDraw(true);
    }

    /// <summary>[버튼 · 터치] 채점표 : 내 그림, 원본, 번호마다 동그라미 또는 빗금</summary>
    public void ShowCompare()
    {
        state = State.Compare;
        compareMyDrawing.texture = board.Texture;
        compareOriginal.sprite = Stage.originalSprite;

        for (int i = 0; i < scoreRows.Length; i++)
        {
            bool used = i < Stage.features.Length;
            scoreRows[i].SetActive(used);
            if (!used) continue;
            bool ok = lastPresent != null && i < lastPresent.Length && lastPresent[i];
            scoreTexts[i].text = GameScript.NoBreakParts(Stage.features[i].name);
            scoreCircles[i].SetActive(ok);
            scoreSlashes[i].SetActive(!ok);
        }

        snacksLeft[stageIndex] = snacks;
        batLost = stageIndex == 0 && !cleared[0];
        if (batLost) itemsFound[0] = stages[0].originalSprite;   // 도깨비가 스스로 찾아오니 소풍에는 진짜 방망이
        Sfx.Play(Sfx.Mark);
        ShowPanel(comparePanel);
    }

    /// <summary>[화면 터치] 채점표 다음으로</summary>
    public void NextStage()
    {
        Sfx.Play(Sfx.Tap);
        if (stageIndex == 0 && batLost)
        {
            batLost = false;
            ShowOops();
        }
        else if (stageIndex + 1 < stages.Length) StartStage(stageIndex + 1);
        else ShowEnding();
    }

    // ───────────── 1단계를 못 찾았을 때 ─────────────

    void ShowOops()
    {
        state = State.Oops;
        oopsPhase = 0;
        hud.SetActive(false);
        oopsHold.SetActive(true);
        oopsRun.SetActive(false);
        oopsBack.SetActive(false);
        ShowPanel(oopsPanel);
        oopsBox.Say(GameScript.Oops1, faceDokkaebi);
    }

    /// <summary>[화면 터치] 대파 → 뛰어나가기 → 방망이를 들고 돌아오기 → 2단계</summary>
    public void OopsTap()
    {
        if (state != State.Oops) return;
        if (oopsBox.FinishTyping()) return;
        if (oopsPhase == 0)
        {
            Sfx.Play(Sfx.Tap);
            oopsPhase = 1;
            oopsHold.SetActive(false);
            oopsRun.SetActive(true);
            Sfx.Play(Sfx.Step);
            oopsBox.Say(GameScript.Oops2, faceDokkaebi);
            StartCoroutine(OopsReturn());
        }
        else if (oopsPhase == 2)
        {
            Sfx.Play(Sfx.Tap);
            StartStage(1);
        }
    }

    IEnumerator OopsReturn()
    {
        yield return new WaitForSeconds(oopsRunSeconds);
        if (state != State.Oops || oopsPhase != 1) yield break;
        oopsPhase = 2;
        oopsRun.SetActive(false);
        oopsBack.SetActive(true);
        Sfx.Play(Sfx.Item);
        oopsBox.Say(GameScript.Oops3, faceDokkaebi);
    }

    // ───────────── 이야기 종료 ─────────────

    /// <summary>소풍 장면 : 물건이 날아와 놓이고, 물건 옆에 남은 간식이 나와요</summary>
    void ShowEnding()
    {
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = endingBackground;
        endingAsking = false;
        endingBox.gameObject.SetActive(false);
        for (int i = 0; i < endingItems.Length; i++)
        {
            endingItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
            for (int k = 0; k < snacksPerStage; k++)
                endingSnacks[i * snacksPerStage + k].SetActive(k < snacksLeft[i]);
        }
        Sfx.Bgm(Sfx.BgmPicnic);
        Sfx.Play(Sfx.Cheer);
        ShowPanel(endingPanel);
    }

    /// <summary>[화면 터치] 도깨비가 처음으로 돌아갈지 묻고, 대사를 누르면 돌아가요</summary>
    public void EndingTap()
    {
        if (state != State.Ending) return;
        if (!endingAsking)
        {
            endingAsking = true;
            endingBox.Say(GameScript.EndingAsk, faceDokkaebi);
            return;
        }
        if (endingBox.FinishTyping()) return;
        BackToTitle();
    }

    public void BackToTitle()
    {
        Sfx.Play(Sfx.Tap);
        ShowTitle();
    }

    /// <summary>[버튼] 오른쪽 위 일시정지 ↔ 계속하기. 멈춘 동안은 시간(향·관찰)이 흐르지 않아요.</summary>
    public void TogglePause() => SetPaused(!pausePanel.activeSelf);

    void SetPaused(bool paused)
    {
        pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    // ───────────── 도우미 함수 ─────────────

    Sprite FaceOf(GameScript.Face face) =>
        face == GameScript.Face.BoySurprised ? faceBoySurprised : face == GameScript.Face.BoyHappy ? faceBoyHappy : faceDokkaebi;

    // (에디터에서만) 제출한 그림과 판정 결과를 Temp/Submissions 에 저장 → 판정 기준을 조절할 때 참고해요.
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
        GameObject[] all = { titlePanel, dialoguePanel, observePanel, drawPanel, judgePanel, resultPanel, comparePanel, oopsPanel, endingPanel };
        foreach (GameObject p in all) p.SetActive(p == panel);
    }

    // 남은 간식은 또렷하게, 쓴 간식은 흐리게 (왼쪽부터 흐려져요)
    void UpdateSnacks()
    {
        int used = snackIcons.Length - snacks;
        for (int i = 0; i < snackIcons.Length; i++)
            snackIcons[i].color = i < used ? UsedSnackColor : Color.white;
    }

    /// <summary>
    /// 한국어 조사 고르기. 마지막 글자에 받침이 있으면 withBatchim, 없으면 withoutBatchim.
    /// 예) Josa("가방","을","를") → "을",  Josa("방망이","을","를") → "를"
    /// </summary>
    public static string Josa(string word, string withBatchim, string withoutBatchim)
    {
        if (string.IsNullOrEmpty(word)) return withoutBatchim;
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return withoutBatchim;
        bool hasBatchim = (last - 0xAC00) % 28 != 0;
        return hasBatchim ? withBatchim : withoutBatchim;
    }
}

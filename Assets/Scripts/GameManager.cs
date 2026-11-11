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
//       ↓ 게임 시작하기
//   [오프닝 (1단계)] → [대화] → [떠올리기 3초] → [쪽지 쓰기 60초] → [건네기: 사탕 -1] → [판정 대기] → [결과]
//                                                   ↑                                               │
//                                                   └────────── 다시 (이전 그림에 이어 그리기) ──────┤ (실패 + 간식 남음)
//                                                                                                   ↓ 성공 / 포기 / 사탕 없음
//                                                                                                [채점표]
//                                                                                                   ↓ 다음 단계 (5단계 뒤엔)
//                                                                                              [이야기 종료]
//
// 쪽지는 주인공이 도깨비에게 써 주는 것이에요. 판정 기준은 시작 대화의 생김새 대사로 알려 줘요.
// 오프닝은 준비물이 사라지는 그림과 함께 보여 줘요. 모든 대사는 터치해야 넘어가요. 떠올리기는 2단계부터 주인공, 1단계는 도깨비가 해요.
// 1단계에서 방망이를 못 찾으면 도깨비는 이야기가 끝날 때까지 빈손이에요.
//
// 화면 구조
//   - 씬은 하나(Game)뿐이고, 화면마다 "패널(Panel)"이라는 UI 묶음이 있어요. 한 번에 패널 하나만 켜요.
//   - 위쪽 HUD(간식, 일시정지, 향)는 그리기부터 채점표까지만 보여요.
//
// 간식 규칙 : 단계마다 사탕 2개 · 초콜릿 2개를 새로 받아요.
//   사탕 : 쪽지를 건넬 때마다 1개. 다 쓰면 다시 그릴 수 없어요.
//   초콜릿 : 돋보기 1번에 1개. 그리는 중에 맨 위 돋보기를 누르면 렌즈 안으로 원본을 3초 둘러봐요. 향은 계속 타요.
//
// 판정 : 항상 자동이에요. 도깨비의 찾는 동작은 한 바퀴씩 반복하고, 응답이 오면 그 바퀴를 마치고 넘어가요.
//   AI 오류가 나면 간식을 돌려주고, 그리던 그림 그대로 다시 제출하게 해요.
//
// 대사는 GameScript, 소리는 Sfx 에 있어요.
public class GameManager : MonoBehaviour
{
    enum State { Title, Dialogue, Observe, Draw, Give, Judge, Result, Compare, Ending }
    enum ResultPhase { Dokkaebi, Boy, Choose, Error }

    // ───────────── Inspector 에서 연결하는 것들 ─────────────

    [Header("데이터 (순서대로 1~5단계)")]
    [SerializeField] StageData[] stages;

    [Header("규칙 (기획서 수치)")]
    [SerializeField] int candiesPerStage = 2;       // 쪽지를 건넬 때 쓰는 사탕 (단계마다)
    [SerializeField] int chocolatesPerStage = 2;    // 돋보기에 쓰는 초콜릿 (단계마다)
    [SerializeField] float observeSeconds = 3f;
    [SerializeField] float drawSeconds = 60f;
    [SerializeField] float peekSeconds = 3f;        // 돋보기로 둘러보는 시간

    [Header("다른 스크립트")]
    [SerializeField] DrawingBoard board;
    [SerializeField] ToolBar toolBar;
    [SerializeField] OpenAIJudge judge;

    [Header("HUD")]
    [SerializeField] GameObject hud;
    [SerializeField] Button magnifierButton;        // 맨 위 왼쪽 돋보기 (초콜릿 1개로 둘러보기)
    [SerializeField] Image[] chocolateIcons;        // 돋보기 바로 오른쪽 초콜릿 (쓴 것은 오른쪽부터 흐리게)
    [SerializeField] Image[] candyIcons;            // 그 오른쪽 사탕 (쓴 것은 왼쪽부터 흐리게)
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
    [SerializeField] GameObject dialogueDim;        // 대사 중 어두운 막 (화면 전체를 덮으려고 패널 밖에 있어요)
    [SerializeField] GameObject observePanel;
    [SerializeField] GameObject drawPanel;
    [SerializeField] GameObject judgePanel;
    [SerializeField] GameObject resultPanel;
    [SerializeField] GameObject comparePanel;
    [SerializeField] GameObject endingPanel;

    [Header("캐릭터 그림")]
    [SerializeField] Sprite faceBoySurprised;
    [SerializeField] Sprite faceBoyHappy;
    [SerializeField] Sprite faceBoySad;             // 틀렸을 때 · 간식이 떨어졌을 때 : 우는 얼굴
    [SerializeField] Sprite faceDokkaebi;
    [SerializeField] Sprite faceDokkaebiProud;     // 맞았을 때 : 뿌듯한 얼굴
    [SerializeField] Sprite faceDokkaebiTease;     // 틀렸을 때 : 놀리는 얼굴
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

    [Header("오프닝 : 준비물이 사라지는 그림 (대화 화면 안)")]
    [SerializeField] GameObject openingGroup;
    [SerializeField] SimpleAnim[] openingItems;     // 가방, 돗자리, 김밥
    [SerializeField] GameObject openingAsk;         // 물음표
    [SerializeField] GameObject openingIdea;        // 쪽지와 간식 떠올리기
    [SerializeField] GameObject openingCall;        // 도깨비 부르기

    [Header("건네기 : 쪽지와 간식 (대화 화면 안)")]
    [SerializeField] GameObject giveNote;           // 그림과 글이 적힌 쪽지
    [SerializeField] SimpleAnim giveNoteAnim;
    [SerializeField] TMP_Text giveNoteText;
    [SerializeField] RawImage giveNoteDrawing;
    [SerializeField] RectTransform giveSnackOut;    // 위쪽 간식 자리에서 도깨비에게 날아가는 간식

    [Header("관찰 화면")]
    [SerializeField] Image observeImage;
    [SerializeField] TMP_Text observeCount;
    [SerializeField] SimpleAnim observeCountAnim;
    [SerializeField] RectTransform observeCountEdge;
    [SerializeField] GameObject observeBoy;         // 말풍선을 떠올리는 주인공
    [SerializeField] GameObject observeDokkaebi;    // 1단계는 도깨비가 떠올려요

    [Header("그리기 화면")]
    [SerializeField] TMP_Text drawTitle;
    [SerializeField] Button submitButton;
    [SerializeField] MagnifierPeek magnifierPeek;   // 렌즈로 원본 둘러보기

    [Header("판정 대기 화면")]
    [SerializeField] RawImage judgeDrawing;         // 화면 한 곳에 떠 있는 내 쪽지
    [SerializeField] TMP_Text judgeNoteText;
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

    [Header("이야기 종료 화면 : 물건 (방망이, 가방, 돗자리, 김밥, 친구 순)과 끝까지 남은 간식")]
    [SerializeField] DialogueBox endingBox;
    [SerializeField] Image endingDokkaebi;
    [SerializeField] Image[] endingItems;
    [SerializeField] GameObject[] endingCandies;    // 돗자리에 모아 놓은 남은 사탕 (단계마다 남은 것을 모두 더해요)
    [SerializeField] GameObject[] endingChocolates; // 돗자리에 모아 놓은 남은 초콜릿

    static readonly Color UsedSnackColor = new Color(0.55f, 0.55f, 0.55f, 0.35f);

    // 떠올리기 화면의 숫자 자리 (1단계는 도깨비가 오른쪽에 있어서 왼쪽으로)
    const float ObserveCountRightX = 252f;
    const float ObserveCountLeftX = -252f;

    // 대화 화면의 주인공 자리 (앞 = 처음 혼자, 뒤 = 도깨비와 함께)
    static readonly Vector2 BoyAlonePos = new Vector2(0f, -618f);
    static readonly Vector2 BoyAloneSize = new Vector2(570f, 867f);
    static readonly Vector2 BoyWithPos = new Vector2(-267f, -768f);
    static readonly Vector2 BoyWithSize = new Vector2(450f, 684f);

    // ───────────── 게임 도중 바뀌는 값들 ─────────────
    State state;
    ResultPhase resultPhase;
    int stageIndex;
    int candies;            // 이번 단계에 남은 사탕
    int chocolates;         // 이번 단계에 남은 초콜릿
    int[] candiesLeft;      // 단계별로 끝날 때 남은 사탕 (이야기 종료에서 모아 놓아요)
    int[] chocolatesLeft;
    float timeLeft;
    float savedDrawTime;    // AI 오류로 그리기로 돌아갈 때 쓸 남은 시간
    int[] attempts;
    bool[] cleared;
    Sprite[] itemsFound;    // 단계별로 마지막에 받은 물건 (이야기 종료 장면에 놓여요)
    int judgeTicket;        // 제출할 때마다 +1. 늦게 도착한 옛 판정 결과를 버리는 데 써요.
    float judgeStartTime;
    bool snackIntroPending; // 단계 첫 그리기 때만 간식이 들어와요
    int dialogueLine;
    int lastCount;
    int outcome;            // 0 전부 맞음, 1 일부 틀림, 2 전부 틀림
    bool[] lastPresent;
    bool batLost;           // 1단계를 못 찾음 : 이야기가 끝날 때까지 도깨비가 빈손이에요
    bool endingAsking;
    bool opening;           // 오프닝 : 도깨비가 나오기 전, 주인공 혼자 말하는 중
    int giveStep;           // 건네기 : 0 주인공, 1 먹기, 2 읽기
    Coroutine noteRoutine;
    Vector2 boyTargetPos, boyTargetSize;

    StageData Stage => stages[stageIndex];

    void Start()
    {
        ShowTitle();
    }

    void Update()
    {
        if (state == State.Dialogue || state == State.Give)
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

    /// <summary>[버튼] 게임 시작하기</summary>
    public void StartGame()
    {
        Sfx.Play(Sfx.Tap);
        attempts = new int[stages.Length];
        cleared = new bool[stages.Length];
        itemsFound = new Sprite[stages.Length];
        batLost = false;
        candiesLeft = new int[stages.Length];
        chocolatesLeft = new int[stages.Length];
        backgroundImage.sprite = gameBackground;
        int first = 0;
#if UNITY_EDITOR
        // (에디터 전용) 앞 단계를 건너뛰어 녹화 · 확인. 건너뛴 단계는 찾은 것으로 쳐요
        first = Mathf.Clamp(editorStartStage - 1, 0, stages.Length - 1);
        for (int i = 0; i < first; i++)
        {
            cleared[i] = true;
            attempts[i] = 1;
            candiesLeft[i] = candiesPerStage - 1;
            chocolatesLeft[i] = chocolatesPerStage;
            itemsFound[i] = stages[i].originalSprite;
        }
#endif
        StartStage(first);
    }

    // ───────────── 오프닝 + 대화 ─────────────

    void StartStage(int index)
    {
        state = State.Dialogue;
        stageIndex = index;
        candies = candiesPerStage;
        chocolates = chocolatesPerStage;
        snackIntroPending = true;
        lastPresent = null;
        toolBar.Setup(Stage.colorMode);
        hud.SetActive(false);
        timerGroup.SetActive(false);
        backgroundImage.sprite = gameBackground;
        UpdateSnacks();
        Sfx.Bgm(Sfx.BgmRoom);

        giveNote.SetActive(false);
        giveSnackOut.gameObject.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebi.GetComponent<SimpleAnim>().enabled = true;     // 대화에서는 도깨비가 걸어 들어와요
        dialogueDokkaebiImage.sprite = index == 0 || batLost ? dokkaebiNoBat : dokkaebiNormal;
        ShowPanel(dialoguePanel);

        opening = index == 0;                       // 맨 처음에는 주인공 혼자 말해요
        openingGroup.SetActive(opening);
        dialogueDim.SetActive(!opening);
        boyTargetPos = opening ? BoyAlonePos : BoyWithPos;
        boyTargetSize = opening ? BoyAloneSize : BoyWithSize;
        dialogueBoy.anchoredPosition = boyTargetPos;
        dialogueBoy.sizeDelta = boyTargetSize;
        dialogueLine = 0;
        SayIntroLine();
    }

    void SayIntroLine()
    {
        GameScript.Line line = GameScript.IntroLines(stageIndex, batLost)[dialogueLine];
        dialogueBox.Say(line.text, FaceOf(line.face));

        opening = stageIndex == 0 && dialogueLine < GameScript.OpeningAloneLines;
        boyTargetPos = opening ? BoyAlonePos : BoyWithPos;
        boyTargetSize = opening ? BoyAloneSize : BoyWithSize;
        openingGroup.SetActive(opening);
        if (opening)
        {
            ShowOpeningBeat(dialogueLine);
        }
        else
        {
            dialogueDim.SetActive(true);
            dialogueDokkaebi.SetActive(true);
        }

        bool dokkaebiTalks = line.face == GameScript.Face.Dokkaebi;
        dialogueBoyAnim.Play(line.face == GameScript.Face.BoySurprised ? SimpleAnim.Kind.Shake
                             : dokkaebiTalks ? SimpleAnim.Kind.None : SimpleAnim.Kind.Gentle);
        if (dialogueDokkaebi.activeSelf)
            dialogueDokkaebiAnim.Play(dokkaebiTalks ? SimpleAnim.Kind.Gentle : SimpleAnim.Kind.None);
    }

    const float OpeningItemAlpha = 0.28f;

    // 오프닝 그림 : 가방 → 돗자리 · 김밥이 사라지고, 쪽지와 간식을 떠올린 뒤 도깨비를 불러요
    void ShowOpeningBeat(int line)
    {
        openingItems[0].gameObject.SetActive(line == 0);
        openingItems[1].gameObject.SetActive(line <= 1);
        openingItems[2].gameObject.SetActive(line <= 1);
        openingAsk.SetActive(line == 1);
        openingIdea.SetActive(line == 2);
        openingCall.SetActive(line == 3);
        if (line == 0)
        {
            // 방에 진짜 놓인 물건이 아니라 "있어야 할 자리"로 보이게 흐리게 그려요
            for (int i = 0; i < openingItems.Length; i++)
            {
                openingItems[i].GetComponent<Image>().sprite = stages[i + 1].originalSprite;
                openingItems[i].SetBaseAlpha(OpeningItemAlpha);    // Play 가 투명도를 되돌리므로 SimpleAnim 에 알려 줘요
            }
            PlayVanish(0, 1.3f);
            openingItems[1].Play(SimpleAnim.Kind.None);
            openingItems[2].Play(SimpleAnim.Kind.None);
        }
        else if (line == 1)
        {
            PlayVanish(1, 0.4f);
            PlayVanish(2, 1.1f);
        }
    }

    void PlayVanish(int item, float delay)
    {
        openingItems[item].delay = delay;
        openingItems[item].popTime = 0.5f;
        openingItems[item].Play(SimpleAnim.Kind.Vanish);
    }

    void NextIntroLine()
    {
        GameScript.Line[] lines = GameScript.IntroLines(stageIndex, batLost);
        if (dialogueLine < lines.Length - 1)
        {
            dialogueLine++;
            SayIntroLine();
        }
        else
        {
            StartObserve();
        }
    }

    /// <summary>[화면 터치] 대사 넘기기 → 떠올리기. 건네기 중이면 건네기를 이어 가요</summary>
    public void DialogueTap()
    {
        if (state == State.Give) { GiveTap(); return; }
        if (state != State.Dialogue) return;
        if (dialogueBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        NextIntroLine();
    }

    /// <summary>3초 떠올리기 : 말풍선 속 원본과 숫자. 1단계는 도깨비, 그 뒤는 주인공이 떠올려요</summary>
    public void StartObserve()
    {
        state = State.Observe;
        timeLeft = observeSeconds;
        lastCount = 0;
        observeImage.sprite = Stage.originalSprite;
        observeCount.text = "3";
        bool dokkaebiRecalls = stageIndex == 0;
        observeBoy.SetActive(!dokkaebiRecalls);
        observeDokkaebi.SetActive(dokkaebiRecalls);
        Vector2 p = observeCountEdge.anchoredPosition;
        p.x = dokkaebiRecalls ? ObserveCountLeftX : ObserveCountRightX;
        observeCountEdge.anchoredPosition = p;
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
        // 쪽지 글은 주인공이 적어 둔 것처럼 한 글자씩 나타나요. 다시 그릴 때는 이미 적혀 있어요.
        drawTitle.text = GameScript.NoteText(stageIndex);
        if (noteRoutine != null) StopCoroutine(noteRoutine);
        drawTitle.maxVisibleCharacters = keepDrawing ? int.MaxValue : 0;
        hud.SetActive(true);
        timerGroup.SetActive(true);
        Sfx.Bgm(Sfx.BgmDraw);
        ShowPanel(drawPanel);
        magnifierPeek.Stop();
        UpdateSnacks();
        if (!keepDrawing) noteRoutine = StartCoroutine(WriteNote());

        if (snackIntroPending)
        {
            snackIntroPending = false;
            foreach (SimpleAnim a in snackAnims) a.Play();     // 초콜릿은 돋보기 뒤에서 오른쪽으로, 사탕은 오른쪽 끝에서 왼쪽으로
            Sfx.Play(Sfx.SnackIn);
        }
    }

    IEnumerator WriteNote()
    {
        int total = drawTitle.text.Length;
        for (int i = 1; i <= total; i++)
        {
            drawTitle.maxVisibleCharacters = i;
            yield return new WaitForSeconds(0.07f);
        }
        drawTitle.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>[버튼] 맨 위 돋보기 : 그리는 중에 초콜릿 1개로 렌즈 안의 원본을 잠깐 둘러봐요 (향은 계속 타요)</summary>
    public void MagnifierTap()
    {
        if (state != State.Draw || magnifierPeek.Active || chocolates <= 0) return;
        Sfx.Play(Sfx.Tap);
        chocolates--;
        UpdateSnacks();
        magnifierPeek.Begin(Stage.originalSprite, peekSeconds);
    }

    /// <summary>[버튼] 다 그렸어 (또는 시간 초과로 자동 제출) : 쪽지와 간식을 건네러 가요</summary>
    public void Submit()
    {
        if (state != State.Draw) return;
        magnifierPeek.Stop();

        state = State.Give;
        savedDrawTime = Mathf.Max(timeLeft, 10f);
        timerGroup.SetActive(false);
        Sfx.Play(Sfx.Submit);

        giveStep = 0;
        giveSnackOut.gameObject.SetActive(false);
        giveNoteText.text = GameScript.NoteText(stageIndex);
        giveNoteDrawing.texture = board.Texture;
        openingGroup.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebi.GetComponent<SimpleAnim>().enabled = false;    // 건넬 때는 이미 곁에 있어요
        dialogueDokkaebi.SetActive(true);
        dialogueDokkaebiImage.sprite = stageIndex == 0 || batLost ? dokkaebiNoBat : dokkaebiNormal;
        boyTargetPos = BoyWithPos;
        boyTargetSize = BoyWithSize;
        dialogueBoy.anchoredPosition = boyTargetPos;
        dialogueBoy.sizeDelta = boyTargetSize;
        ShowPanel(dialoguePanel);
        giveNote.SetActive(true);
        giveNoteAnim.Play(SimpleAnim.Kind.Float);
        dialogueBoyAnim.Play(SimpleAnim.Kind.Gentle);
        dialogueDokkaebiAnim.Play(SimpleAnim.Kind.None);
        dialogueBox.Say(GameScript.GiveBoy, faceBoyHappy);
    }

    // 주인공이 건네면 쪽지와 간식이 날아가고, 도깨비가 먹고 읽은 뒤 찾으러 가요
    void GiveTap()
    {
        if (dialogueBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        if (giveStep == 0)
        {
            giveStep = 1;
            int usedBefore = candiesPerStage - candies;
            candies--;
            attempts[stageIndex]++;
            UpdateSnacks();
            // 방금 쓴 사탕 자리에서 도깨비에게 날아가요
            Vector2 p = giveSnackOut.anchoredPosition;
            p.x = candyIcons[Mathf.Clamp(usedBefore, 0, candyIcons.Length - 1)].rectTransform.anchoredPosition.x;
            giveSnackOut.anchoredPosition = p;
            giveSnackOut.gameObject.SetActive(true);
            giveNoteAnim.Play(SimpleAnim.Kind.NoteGive);
            dialogueBoyAnim.Play(SimpleAnim.Kind.None);
            dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
            dialogueBox.Say(GameScript.GiveEat, faceDokkaebi);
        }
        else if (giveStep == 1)
        {
            giveStep = 2;
            giveNote.SetActive(false);
            dialogueBox.Say(GameScript.GiveReadLine(stageIndex), faceDokkaebi);
        }
        else
        {
            StartJudge();
        }
    }

    void StartJudge()
    {
        state = State.Judge;
        giveNote.SetActive(false);
        giveSnackOut.gameObject.SetActive(false);
        Sfx.Bgm(Sfx.BgmJudge);

        judgeDrawing.texture = board.Texture;
        judgeNoteText.text = GameScript.NoteText(stageIndex);
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

    // AI 판정이 실패했을 때 : 사탕을 돌려주고, 대사를 누르면 그림으로 돌아가요
    void OnJudgeError(string message)
    {
        if (state != State.Judge) return;
        candies++;
        attempts[stageIndex]--;
        judgeTicket++;      // 이 제출은 끝난 것으로 (늦게 오는 결과는 무시)
        UpdateSnacks();
        Debug.LogWarning("[OpenAIJudge] " + message);

        state = State.Result;
        resultPhase = ResultPhase.Error;
        resultItemImage.enabled = false;
        resultItemShadow.SetActive(false);
        resultSparkles.SetActive(false);
        resultDokkaebi.sprite = stageIndex == 0 || batLost ? dokkaebiNoBat : dokkaebiNormal;
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

        // 판정 기준이 n개면 경우의 수는 2ⁿ 가지예요 (Docs/기록/6_엉뚱한_물건_제작.md)
        //   전부 맞음 → 진짜 물건 A / 일부만 맞음 → 그 조합대로 틀린 A' / 전부 틀림 → 완전히 다른 물건 B
        Sprite item = success ? Stage.originalSprite : Stage.WrongSpriteFor(present);
        resultItemImage.sprite = item;
        resultItemImage.enabled = item != null;
        itemsFound[stageIndex] = item;
        resultItemShadow.SetActive(true);
        resultSparkles.SetActive(success);
        choiceRow.SetActive(false);

        // 1단계를 틀렸거나 못 찾고 넘어왔으면 도깨비는 빈손이에요
        bool batMissing = (stageIndex == 0 && !success) || batLost;
        resultDokkaebi.sprite = batMissing ? dokkaebiNoBat : dokkaebiSwing;
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.Swing);
        Sfx.Play(Sfx.Item);
        Sfx.Play(success ? Sfx.Success : Sfx.Fail);
        resultBox.Say(GameScript.DokkaebiResult(stageIndex, outcome), success ? faceDokkaebiProud : faceDokkaebiTease);
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
            resultDokkaebi.sprite = (stageIndex == 0 && wrong) || batLost ? dokkaebiNoBat : dokkaebiNormal;
            resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
            string line = wrong && candies <= 0 ? GameScript.BoyOutOfSnacks : GameScript.BoyResult(stageIndex, outcome);
            resultBox.Say(line, wrong ? faceBoySad : faceBoyHappy);     // 엉뚱한 물건 · 간식 없음 : 우는 얼굴
        }
        else if (outcome != 0 && candies > 0)
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

        candiesLeft[stageIndex] = candies;
        chocolatesLeft[stageIndex] = chocolates;
        if (stageIndex == 0) batLost = !cleared[0];
        Sfx.Play(Sfx.Mark);
        ShowPanel(comparePanel);
    }

    /// <summary>[화면 터치] 채점표 다음으로</summary>
    public void NextStage()
    {
        Sfx.Play(Sfx.Tap);
        if (stageIndex + 1 < stages.Length) StartStage(stageIndex + 1);
        else ShowEnding();
    }

    // ───────────── 이야기 종료 ─────────────

    /// <summary>소풍 장면 : 물건이 날아와 놓이고, 단계마다 남은 간식이 돗자리에 모여요.</summary>
    void ShowEnding()
    {
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = endingBackground;
        endingAsking = false;
        endingBox.gameObject.SetActive(false);
        endingDokkaebi.sprite = batLost ? dokkaebiNoBat : dokkaebiNormal;
        int candyTotal = 0, chocolateTotal = 0;
        for (int i = 0; i < endingItems.Length; i++)
        {
            endingItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
            candyTotal += candiesLeft[i];
            chocolateTotal += chocolatesLeft[i];
        }
        for (int k = 0; k < endingCandies.Length; k++) endingCandies[k].SetActive(k < candyTotal);
        for (int k = 0; k < endingChocolates.Length; k++) endingChocolates[k].SetActive(k < chocolateTotal);
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
        face == GameScript.Face.BoySurprised ? faceBoySurprised : face == GameScript.Face.BoyHappy ? faceBoyHappy
        : face == GameScript.Face.BoySad ? faceBoySad : faceDokkaebi;

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
        GameObject[] all = { titlePanel, dialoguePanel, observePanel, drawPanel, judgePanel, resultPanel, comparePanel, endingPanel };
        foreach (GameObject p in all) p.SetActive(p == panel);
        dialogueDim.SetActive(panel == dialoguePanel || panel == observePanel);
    }

    // 남은 간식은 또렷하게, 쓴 간식은 흐리게. 사탕은 왼쪽부터, 초콜릿은 (돋보기에서 먼 쪽인) 오른쪽부터 흐려져요.
    // 초콜릿이 없으면 돋보기도 흐리게
    void UpdateSnacks()
    {
        Fade(candyIcons, candies, fromRight: false);
        Fade(chocolateIcons, chocolates, fromRight: true);
        magnifierButton.interactable = chocolates > 0;
    }

    static void Fade(Image[] icons, int left, bool fromRight)
    {
        int used = icons.Length - left;
        for (int i = 0; i < icons.Length; i++)
        {
            bool spent = fromRight ? i >= left : i < used;
            icons[i].color = spent ? UsedSnackColor : Color.white;
        }
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

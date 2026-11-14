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
//   [챕터 선택] 소풍만 열려 있고, 나머지 4개는 자물쇠 (준비 중)
//       ↓ 소풍
//   [오프닝 (1단계)] → [대화] → [떠올리기 3초] → [쪽지 쓰기 60초] → [건네기: 사탕 -1] → [판정 대기] → [결과]
//                                                   ↑                                               │
//                                                   └────────── 다시 (이전 그림에 이어 그리기) ──────┤ (실패 + 간식 남음)
//                                                                                                   ↓ 성공 / 포기 / 사탕 없음
//                                                                                                [채점표]
//                                                                                                   ↓ 다음 단계 (5단계 뒤엔)
//                                                                                              [이야기 종료]
//                                                                                                   ↓ 도깨비 인사 → [챕터 선택]
//
// 쪽지는 주인공이 도깨비에게 써 주는 것이에요. 판정 기준은 시작 대화의 생김새 대사로 알려 줘요.
// 오프닝은 준비물이 사라지는 그림과 함께 보여 줘요. 모든 대사는 터치해야 넘어가요. 떠올리기는 2단계부터 주인공, 1단계는 도깨비가 해요.
// 1단계에서 방망이를 못 찾으면 도깨비는 이야기가 끝날 때까지 빈손이에요.
//
// 화면 구조
//   - 씬은 하나(Game)뿐이고, 화면마다 "패널(Panel)"이라는 UI 묶음이 있어요. 한 번에 패널 하나만 켜요.
//   - 위쪽 HUD(간식, 일시정지, 향)는 그리기부터 채점표까지만 보여요.
//
// 간식 규칙 : 단계마다 사탕 3개를 새로 받아요. 쪽지를 건넬 때마다 1개. 다 쓰면 다시 그릴 수 없어요.
// 돋보기 : 그리는 중에 맨 위 돋보기를 누르면 렌즈 안으로 원본을 5초 둘러봐요. 향은 계속 타요.
//   - 게임 전체에서 1번 (magnifierPerGame).
//   - 1단계 첫 그리기 때 도깨비가 쓰는 법을 보여 줘요. 이 시범은 공짜이고, 시범 동안은 향이 타지 않아요.
//     눌러 보라는 대사 → (대사를 넘기면 대사가 사라지고 도깨비불 셋이 돋보기 둘레를 돎) → (플레이어가 돋보기를 누름)
//     → 보는 법 대사 → (대사를 넘김) → 5초 둘러보기 → 한 번만 더 빌려준다는 대사
//   - 한 단계에서 2번째로 틀리면 그 단계에서만 쓰는 무료 기회를 1번 줘요 (도깨비가 돋보기를 건네줘요). 단계가 끝나면 사라져요.
//   - 무료 기회가 있으면 그것부터 써요. 돋보기 오른쪽 아래 숫자가 지금 쓸 수 있는 횟수예요.
// 색 섞기 시범 : 5단계 첫 그리기 때 도깨비가 말하는 동안 실제 팔레트에 빨강 2 · 노랑 2 · 파랑 1 을 넣어 갈색을 만들어 보여 줘요.
//   같은 물감을 여러 번 넣을 수 있다는 걸 보여 주려는 거예요. 시범 동안은 향이 타지 않아요.
//
// 판정 : 항상 자동이에요. 도깨비의 찾는 동작은 한 바퀴씩 반복하고, 응답이 오면 그 바퀴를 마치고 넘어가요.
//   AI 오류가 나면 간식을 돌려주고, 그리던 그림 그대로 다시 제출하게 해요.
//
// 대사는 GameScript, 소리는 Sfx 에 있어요.
public class GameManager : MonoBehaviour
{
    enum State { Title, Chapter, Dialogue, Observe, Draw, Give, Judge, Result, Compare, Ending }
    enum ResultPhase { Dokkaebi, Boy, Gift, Choose, Error }

    // ───────────── Inspector 에서 연결하는 것들 ─────────────

    [Header("데이터 (순서대로 1~5단계)")]
    [SerializeField] StageData[] stages;

    [Header("규칙 (기획서 수치)")]
    [SerializeField] int candiesPerStage = 3;       // 쪽지를 건넬 때 쓰는 사탕 (단계마다)
    [SerializeField] int magnifierPerGame = 1;      // 돋보기 (게임 전체에서. 시범과 무료 기회는 따로)
    [SerializeField] float observeSeconds = 3f;
    [SerializeField] float drawSeconds = 60f;
    [SerializeField] float peekSeconds = 5f;        // 돋보기로 둘러보는 시간

    [Header("다른 스크립트")]
    [SerializeField] DrawingBoard board;
    [SerializeField] ToolBar toolBar;
    [SerializeField] OpenAIJudge judge;

    [Header("HUD")]
    [SerializeField] GameObject hud;
    [SerializeField] Button magnifierButton;        // 맨 위 왼쪽 돋보기
    [SerializeField] GameObject magnifierBadge;     // 돋보기 오른쪽 아래 동그라미 : 쓸 수 있는 횟수
    [SerializeField] TMP_Text magnifierBadgeText;
    [SerializeField] GameObject magnifierSparkles;  // 무료 기회를 받으면 돋보기가 반짝여요
    [SerializeField] Image[] candyIcons;            // 오른쪽 사탕 (쓴 것은 왼쪽부터 흐리게)
    [SerializeField] SimpleAnim[] snackAnims;       // 단계 첫 그리기 때 들어오는 움직임
    [SerializeField] GameObject timerGroup;         // 향 (그리는 동안만 보임)
    [SerializeField] TMP_Text timerText;            // 남은 초 숫자 "58"
    [SerializeField] IncenseBurn incense;           // 타들어 가는 향
    [SerializeField] GameObject settingsPanel;      // 소리 설정 창 (오른쪽 위 톱니). 여는 동안 게임이 멈춰요
    [SerializeField] Slider bgmSlider;              // 배경음 볼륨
    [SerializeField] Slider sfxSlider;              // 효과음 볼륨

    [Header("배경 : 시작, 게임 중, 이야기 종료 그림이 달라요")]
    [SerializeField] Image backgroundImage;
    [SerializeField] Sprite startBackground;
    [SerializeField] Sprite gameBackground;
    [SerializeField] Sprite endingBackground;

    [Header("에디터 전용 : 몇 단계부터 시작할지 (웹 빌드는 항상 1단계부터)")]
    [SerializeField] int editorStartStage = 1;

    [Header("패널(화면)")]
    [SerializeField] GameObject titlePanel;
    [SerializeField] GameObject chapterPanel;       // 챕터 선택 : 소풍 + 준비 중인 챕터 4개
    [SerializeField] SimpleAnim[] chapterLockedAnims;   // 준비 중인 챕터를 누르면 자물쇠 카드가 흔들려요
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
    [SerializeField] GameObject peekGuide;          // 1단계 돋보기 시범 : 그림판을 덮고 도깨비가 말해요
    [SerializeField] DialogueBox peekGuideBox;
    [SerializeField] GameObject mixGuide;           // 5단계 색 섞기 시범 : 팔레트 위쪽에 대사 상자
    [SerializeField] DialogueBox mixGuideBox;
    [SerializeField] ColorMixer mixer;
    [SerializeField] PaperView paperView;           // 책상 위 쪽지 : 두 손가락으로 키우기 · 돌리기 · 옮기기
    [SerializeField] GameObject magnifierHighlight;         // 시범 : 돋보기 둘레를 도는 도깨비불
    [SerializeField] RectTransform[] magnifierFires;
    [SerializeField] GameObject brushHighlight;     // 5단계 색 섞기 시범 : 붓 둘레를 도는 도깨비불
    [SerializeField] RectTransform[] brushFires;
    [SerializeField] GameObject paintHighlight;     // 5단계 색 섞기 시범 : 넣을 물감 둘레를 도는 도깨비불
    [SerializeField] RectTransform paintOrbit;
    [SerializeField] RectTransform[] paintFires;
    [SerializeField] RectTransform brushHolder;     // 붓 (눌러 볼 차례면 콩콩 뛰어요)

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
    [SerializeField] RectTransform giftMagnifier;   // 무료 기회 : 도깨비가 건네서 맨 위 돋보기로 날아가요

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
    [SerializeField] GameObject[] endingCandies;    // 사탕 통에 모은 남은 사탕 (단계마다 남은 것을 모두 더해요)

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
    int[] candiesLeft;      // 단계별로 끝날 때 남은 사탕 (이야기 종료에서 모아 놓아요)
    int magnifiers;         // 게임 전체에서 남은 돋보기
    bool freePeek;          // 이번 단계의 무료 돋보기 (단계가 끝나면 사라져요)
    bool freeGiven;         // 이번 단계에 무료 기회를 이미 줬는지
    int stageWrongs;        // 이번 단계에서 틀린 횟수
    bool peekShown;         // 1단계 돋보기 시범을 했는지
    int peekGuideStep;      // 시범 : 0 없음, 1 눌러 보라고 말함, 2 돋보기 강조 (대사 없음), 3 보는 법을 말함, 4 둘러보는 중, 5 규칙을 말함

    Coroutine giftRoutine;
    bool mixShown;          // 5단계 색 섞기 시범을 했는지
    int mixDemoStep;        // 색 섞기 안내 : 0 없음, 1 붓을 눌러 보라고 말함, 2 붓을 누르기 기다림(도깨비불),
                            //   3 물감(mixPaint)을 넣어 보라고 말함, 4 그 물감을 넣기 기다림(도깨비불), 5 결과를 말함, 6 검정 만드는 법을 말함
    int mixPaint;           // 색 섞기 안내 : 지금 넣을 물감 (0 빨강, 1 노랑, 2 파랑)
    Coroutine mixDone;      // 색 섞기 안내 : 넣은 물감이 붓 끝에 닿아 색이 바뀐 걸 본 뒤 다음 대사
    static readonly int[] MixGoal = { 2, 2, 1 };   // 색 섞기 안내 : 빨강 2 · 노랑 2 · 파랑 1 = 갈색
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
    Vector2 boyAnchorShift;     // 씬에서 주인공 자리 기준이 위쪽이 아닐 때(긴 폰용 가운데 기준) 더할 값

    StageData Stage => stages[stageIndex];

    void Start()
    {
        boyAnchorShift = new Vector2(0f, dialogueBoy.anchoredPosition.y - BoyWithPos.y);    // 씬은 "도깨비와 함께" 높이로 만들어져 있어요
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
            if (peekGuideStep != 0)
            {
                // 돋보기 시범 중에는 향이 타지 않아요. 눌러 볼 차례면 돋보기가 콩콩 뛰어요
                bool waiting = peekGuideStep == 1 || peekGuideStep == 2;
                float wave = Mathf.Abs(Mathf.Sin(Time.time * 4f));
                magnifierButton.transform.localScale = Vector3.one * (waiting ? 1f + 0.1f * wave : 1f);
                if (peekGuideStep == 2) SpinFires(magnifierFires, 80f, 45f);
                if (peekGuideStep == 4 && !magnifierPeek.Active) SayPeekRule();
                return;
            }
            if (mixDemoStep != 0)
            {
                // 색 섞기 안내 중에는 향이 타지 않아요. 붓을 눌러 볼 차례면 붓이 콩콩 뛰고 도깨비불이 돌아요
                if (mixDemoStep == 2)
                {
                    brushHolder.localScale = Vector3.one * (1f + 0.1f * Mathf.Abs(Mathf.Sin(Time.time * 4f)));
                    SpinFires(brushFires, 78f, 72f);
                }
                // 물감을 넣을 차례 : 도깨비불이 그 물감 둘레를 돌고, 정한 횟수만큼 넣으면 붓 끝이 바뀌는 걸 본 뒤 다음 대사
                if (mixDemoStep == 4)
                {
                    PlacePaintFires();
                    if (mixDone == null && mixer.CountOf(mixPaint) >= MixGoal[mixPaint]) mixDone = StartCoroutine(SayMixNext());
                }
                return;
            }
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

    /// <summary>[버튼] 게임 시작하기 → 챕터 선택</summary>
    public void OpenChapters()
    {
        Sfx.Play(Sfx.Tap);
        ShowChapters();
    }

    void ShowChapters()
    {
        state = State.Chapter;
        SetPaused(false);
        hud.SetActive(false);
        backgroundImage.sprite = startBackground;
        Sfx.Bgm(Sfx.BgmRoom);
        ShowPanel(chapterPanel);
    }

    /// <summary>[버튼] 챕터 카드. 0 = 소풍(시작), 그 밖은 준비 중이라 자물쇠 카드가 흔들리기만 해요</summary>
    public void ChapterTap(int index)
    {
        if (state != State.Chapter) return;
        if (index == 0) { StartGame(); return; }
        Sfx.Play(Sfx.Tap);
        int k = index - 1;
        if (k >= 0 && k < chapterLockedAnims.Length) chapterLockedAnims[k].Play(SimpleAnim.Kind.Shake);
    }

    /// <summary>소풍 챕터 시작</summary>
    public void StartGame()
    {
        Sfx.Play(Sfx.Tap);
        attempts = new int[stages.Length];
        cleared = new bool[stages.Length];
        itemsFound = new Sprite[stages.Length];
        batLost = false;
        candiesLeft = new int[stages.Length];
        magnifiers = magnifierPerGame;
        peekShown = false;
        mixShown = false;
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
            itemsFound[i] = stages[i].originalSprite;
        }
        if (first > 0) peekShown = true;     // 시범은 1단계에서만 해요
#endif
        StartStage(first);
    }

    // ───────────── 오프닝 + 대화 ─────────────

    void StartStage(int index)
    {
        state = State.Dialogue;
        stageIndex = index;
        candies = candiesPerStage;
        freePeek = false;
        freeGiven = false;
        stageWrongs = 0;
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
        boyTargetPos = (opening ? BoyAlonePos : BoyWithPos) + boyAnchorShift;
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
        boyTargetPos = (opening ? BoyAlonePos : BoyWithPos) + boyAnchorShift;
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
            paperView.ResetView();      // 새 쪽지는 책상 위에 똑바로
        }
        // 향과 남은 초를 바로 맞춰 둬요 (시범 동안은 향이 멈춰 있어서, 안 맞추면 불꽃이 왼쪽 끝에 남아 있어요)
        timerText.text = Mathf.CeilToInt(timeLeft).ToString();
        incense.Show(timeLeft / drawSeconds, timeLeft);
        // 쪽지 글은 주인공이 적어 둔 것처럼 한 글자씩 나타나요. 다시 그릴 때는 이미 적혀 있어요.
        drawTitle.text = GameScript.NoteText(stageIndex);
        if (noteRoutine != null) StopCoroutine(noteRoutine);
        drawTitle.maxVisibleCharacters = keepDrawing ? int.MaxValue : 0;
        hud.SetActive(true);
        timerGroup.SetActive(true);
        Sfx.Bgm(Sfx.BgmDraw);
        ShowPanel(drawPanel);
        magnifierPeek.Stop();
        StopGift();
        toolBar.ClosePanel();

        // 1단계 첫 그리기 : 도깨비가 돋보기 쓰는 법을 보여 줘요
        peekGuideStep = 0;
        peekGuide.SetActive(false);
        ShowMagnifierHighlight(false);
        magnifierButton.transform.localScale = Vector3.one;
        if (stageIndex == 0 && !peekShown && !keepDrawing)
        {
            peekShown = true;
            peekGuideStep = 1;
            peekGuide.SetActive(true);
            peekGuideBox.Say(GameScript.MagnifierShow, faceDokkaebi);
        }

        // 5단계 첫 그리기 : 색 섞기 시범
        StopMixDemo();
        if (stageIndex == 4 && !mixShown && !keepDrawing) StartMixDemo();

        UpdateSnacks();
        if (!keepDrawing) noteRoutine = StartCoroutine(WriteNote());

        if (snackIntroPending)
        {
            snackIntroPending = false;
            foreach (SimpleAnim a in snackAnims) a.Play();     // 사탕은 오른쪽 끝에서 왼쪽으로
            Sfx.Play(Sfx.SnackIn);
        }
    }

    /// <summary>[화면 터치] 돋보기 시범 중 대사 넘기기. 눌러 보라는 대사는 돋보기를 직접 눌러야 넘어가요</summary>
    public void PeekGuideTap()
    {
        if (state != State.Draw || peekGuideStep == 0) return;
        if (peekGuideBox.FinishTyping()) return;
        if (peekGuideStep == 1)
        {
            // 눌러 보라는 대사를 넘기면 대사는 사라지고 맨 위 돋보기가 빛나요. 이제 돋보기를 직접 눌러야 해요
            Sfx.Play(Sfx.Tap);
            peekGuideStep = 2;
            peekGuideBox.gameObject.SetActive(false);
            ShowMagnifierHighlight(true);
        }
        else if (peekGuideStep == 3)
        {
            // 보는 법을 들었으면 둘러보기 시작 (시범이라 공짜)
            peekGuideStep = 4;
            peekGuide.SetActive(false);
            Sfx.Play(Sfx.Magnifier);
            magnifierPeek.Begin(Stage.originalSprite, peekSeconds);
        }
        else if (peekGuideStep == 5)
        {
            Sfx.Play(Sfx.Tap);
            peekGuideStep = 0;
            peekGuide.SetActive(false);
        }
    }

    // 시범으로 둘러본 뒤 : 돋보기는 게임 전체에서 한 번뿐이라고 알려 줘요
    void SayPeekRule()
    {
        peekGuideStep = 5;
        peekGuide.SetActive(true);
        peekGuideBox.Say(GameScript.MagnifierRule, faceDokkaebi);
    }

    // 도깨비불 셋이 둘레를 시계 방향으로 돌아요 (돋보기 · 붓). 가로 rx, 세로 ry 인 원을 돌고, 불꽃은 늘 위를 보며 일렁여요.
    // 크기는 화면 끝에서도 불이 다 보이게 정했어요 (배치 검사가 화면 밖을 확인해요)
    void SpinFires(RectTransform[] fires, float rx, float ry)
    {
        for (int i = 0; i < fires.Length; i++)
        {
            float a = -Time.time * 2.2f + i * Mathf.PI * 2f / fires.Length;
            fires[i].anchoredPosition = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            float flicker = 1f + 0.08f * Mathf.Sin(Time.time * 9f + i * 2f);
            fires[i].localScale = new Vector3(1f / flicker, flicker, 1f);
        }
    }

    // 5단계 첫 그리기 : 대사가 알려 주고 사라지면 아이가 직접 눌러 봐요. 대사는 원래 자리에 뜨고, 화면은 움직이지 않아요
    void StartMixDemo()
    {
        mixShown = true;
        mixDemoStep = 1;
        toolBar.OnBrushTap = BrushTapped;
        board.Locked = true;         // 안내 동안은 쪽지에 그려지지 않아요
        mixGuide.SetActive(true);
        mixGuideBox.Say(GameScript.MixTapBrush, faceDokkaebi);
    }

    // 붓을 눌렀어요 : 눌러 볼 차례였으면 빨강부터 넣어 보라고 말해요. 팔레트는 대사를 넘기면 열려요 (대사 상자에 반쯤 가리지 않게)
    // 안내 중 다른 때 붓을 누르면 아무 일도 없어요 (팔레트가 닫혀 버리면 도깨비불이 가리킬 물감이 안 보여요)
    bool BrushTapped()
    {
        if (mixDemoStep == 0) return false;
        if (state != State.Draw || mixDemoStep != 2) return true;
        brushHolder.localScale = Vector3.one;
        brushHighlight.SetActive(false);
        SayMixPaint(0);
        return true;
    }

    // 물감 하나를 넣어 보라고 말해요 (빨강 2 → 노랑 2 → 파랑 1)
    void SayMixPaint(int paint)
    {
        mixDemoStep = 3;
        mixPaint = paint;
        mixer.SetGuide(paint, MixGoal[paint]);     // 이 물감만 정한 횟수까지 넣을 수 있어요
        mixGuide.SetActive(true);
        mixGuideBox.Say(GameScript.MixPaintSteps[paint], faceDokkaebi);
    }

    // 도깨비불 셋이 넣을 물감 둘레를 돌아요
    void PlacePaintFires()
    {
        RectTransform slot = mixer.PaintButton(mixPaint);
        paintOrbit.position = slot.TransformPoint(slot.rect.center);
        SpinFires(paintFires, 68f, 68f);
    }

    // 넣은 물감이 붓 끝에서 첨벙하고 색이 바뀌는 걸 볼 시간을 준 뒤, 팔레트를 닫고 다음 대사 (대사 상자에 반쯤 가리지 않게)
    IEnumerator SayMixNext()
    {
        bool last = mixPaint == MixGoal.Length - 1;
        yield return new WaitForSeconds(last ? 1.2f : 0.9f);
        mixDone = null;
        if (mixDemoStep != 4) yield break;
        paintHighlight.SetActive(false);
        toolBar.ClosePanel();
        if (!last) { SayMixPaint(mixPaint + 1); yield break; }
        mixDemoStep = 5;
        mixer.SetGuide(-1, 0);
        mixGuide.SetActive(true);
        mixGuideBox.Say(GameScript.MixHow, faceDokkaebi);
    }

    // 안내를 끝내요. 아이가 직접 만든 색은 그대로 둬요 (중간에 그만두면 검정으로)
    void StopMixDemo(bool keepColor = false)
    {
        if (mixDone != null) StopCoroutine(mixDone);
        mixDone = null;
        if (mixDemoStep != 0)
        {
            if (!keepColor) toolBar.ResetPaints();
            toolBar.ClosePanel();
            board.Locked = false;
            brushHolder.localScale = Vector3.one;
            brushHighlight.SetActive(false);
            paintHighlight.SetActive(false);
            mixer.SetGuide(-1, 0);
        }
        mixDemoStep = 0;
        mixGuide.SetActive(false);
    }

    /// <summary>[화면 터치] 색 섞기 안내 대사 넘기기. 대사가 사라지면 아이가 직접 붓 · 물감을 눌러야 다음으로 가요</summary>
    public void MixGuideTap()
    {
        if (state != State.Draw || mixDemoStep == 0 || mixDemoStep == 2 || mixDemoStep == 4) return;
        if (mixGuideBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        if (mixDemoStep == 1)
        {
            // 대사는 사라지고 도깨비불이 붓 둘레를 돌아요 (화면을 눌러도 안 넘어가요. 붓을 눌러야 해요)
            mixDemoStep = 2;
            mixGuide.SetActive(false);
            brushHighlight.SetActive(true);
            SpinFires(brushFires, 78f, 72f);
        }
        else if (mixDemoStep == 3)
        {
            // 대사가 사라지고 팔레트가 열려요. 도깨비불이 넣을 물감 둘레를 돌고, 아이가 직접 넣어요
            mixDemoStep = 4;
            mixGuide.SetActive(false);
            toolBar.OpenPanel();
            Canvas.ForceUpdateCanvases();            // 팔레트 칸 자리를 먼저 정해 두고 도깨비불을 옮겨요
            paintHighlight.SetActive(true);
            PlacePaintFires();
        }
        else if (mixDemoStep == 5)
        {
            // 검정 물감이 없으니 세 가지를 똑같이 넣으면 검정이라고 알려 줘요
            mixDemoStep = 6;
            mixGuideBox.Say(GameScript.MixBlack, faceDokkaebi);
        }
        else
        {
            // 다 했으면 그리기 시작. 직접 만든 갈색은 그대로 써요
            StopMixDemo(keepColor: true);
        }
    }

    void ShowMagnifierHighlight(bool on)
    {
        if (on) SpinFires(magnifierFires, 80f, 45f);
        magnifierHighlight.SetActive(on);
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

    /// <summary>[버튼] 맨 위 돋보기 : 그리는 중에 렌즈 안의 원본을 5초 둘러봐요 (향은 계속 타요)
    /// 시범이면 공짜, 아니면 이번 단계 무료 기회 → 게임 전체 1번 순서로 써요.</summary>
    public void MagnifierTap()
    {
        if (state != State.Draw || magnifierPeek.Active) return;
        if (peekGuideStep == 1 || peekGuideStep == 2)
        {
            // 시범 : 눌렀으면 바로 열지 않고, 몇 초 동안 어떻게 보는지 먼저 알려 줘요
            Sfx.Play(Sfx.Tap);
            peekGuideStep = 3;
            magnifierButton.transform.localScale = Vector3.one;
            ShowMagnifierHighlight(false);
            peekGuideBox.Say(GameScript.MagnifierHow, faceDokkaebi);
            return;
        }
        if (peekGuideStep != 0 || mixDemoStep != 0) return;
        if (freePeek) freePeek = false;
        else if (magnifiers > 0) magnifiers--;
        else return;
        Sfx.Play(Sfx.Magnifier);
        UpdateSnacks();
        magnifierPeek.Begin(Stage.originalSprite, peekSeconds);
    }

    /// <summary>[버튼] 다 그렸어 (또는 시간 초과로 자동 제출) : 쪽지와 간식을 건네러 가요</summary>
    public void Submit()
    {
        if (state != State.Draw || peekGuideStep != 0 || mixDemoStep != 0) return;
        magnifierPeek.Stop();
        toolBar.ClosePanel();

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
        boyTargetPos = BoyWithPos + boyAnchorShift;
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
        Sfx.Play(giveStep == 0 ? Sfx.Give : Sfx.Tap);     // 쪽지와 사탕이 날아갈 때는 건네는 소리
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
        if (!success) stageWrongs++;

        // 판정 기준이 n개면 경우의 수는 2ⁿ 가지예요 (Docs/기록/6_엉뚱한_물건_제작.md)
        //   전부 맞음 → 진짜 물건 A / 일부만 맞음 → 그 조합대로 틀린 A' / 전부 틀림 → 완전히 다른 물건 B
        Sprite item = success ? Stage.originalSprite : Stage.WrongSpriteFor(present);
        resultItemImage.sprite = item;
        resultItemImage.enabled = item != null;
        itemsFound[stageIndex] = item;
        resultItemShadow.SetActive(true);
        resultSparkles.SetActive(success);
        choiceRow.SetActive(false);

        // 방망이는 화면에 하나만 보여요.
        //   1단계 : 맞히면 방망이가 물건 자리에 놓여 있으니 도깨비는 빈손 (주인공이 말할 때 집어 들어요, ResultTap)
        //           틀리면 엉뚱한 물건이 왔으니 역시 빈손
        //   2단계부터 : 1단계를 못 찾고 넘어왔으면(batLost) 빈손, 아니면 방망이를 휘둘러요
        bool batMissing = stageIndex == 0 || batLost;
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
            // 1단계를 맞혔으면 도깨비가 바닥의 방망이를 집어 들어요 : 바닥 방망이 · 그림자 · 반짝이를 치워요
            if (stageIndex == 0 && !wrong)
            {
                resultItemImage.enabled = false;
                resultItemShadow.SetActive(false);
                resultSparkles.SetActive(false);
            }
            resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
            string line = wrong && candies <= 0 ? GameScript.BoyOutOfSnacks : GameScript.BoyResult(stageIndex, outcome);
            resultBox.Say(line, wrong ? faceBoySad : faceBoyHappy);     // 엉뚱한 물건 · 간식 없음 : 우는 얼굴
        }
        else if (resultPhase == ResultPhase.Boy && outcome != 0 && candies > 0 && stageWrongs == 2 && !freeGiven)
        {
            // 이 단계에서 두 번째로 틀렸어요 : 도깨비가 돋보기를 건네주고, 맨 위 돋보기로 날아가 반짝여요
            resultPhase = ResultPhase.Gift;
            freeGiven = true;
            freePeek = true;
            resultDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
            resultBox.Say(GameScript.MagnifierGift, faceDokkaebi);
            StopGift();
            giftRoutine = StartCoroutine(GiftFly());
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
        freePeek = false;           // 무료 기회는 그 단계에서만
        StopGift();
        UpdateSnacks();
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

    /// <summary>소풍 장면 : 물건이 날아와 놓이고, 단계마다 남은 사탕이 사탕 통에 모여요.</summary>
    void ShowEnding()
    {
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = endingBackground;
        endingAsking = false;
        endingBox.gameObject.SetActive(false);
        endingDokkaebi.sprite = batLost ? dokkaebiNoBat : dokkaebiNormal;
        int candyTotal = 0;
        for (int i = 0; i < endingItems.Length; i++)
        {
            endingItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
            candyTotal += candiesLeft[i];
        }
        // 방망이는 화면에 하나만 : 찾았으면 도깨비 손에만 있고 돗자리 방망이 자리는 비워요.
        //   못 찾았으면 도깨비는 빈손이고 그 자리에 받은 엉뚱한 물건이 놓여요.
        endingItems[0].gameObject.SetActive(batLost);
        for (int k = 0; k < endingCandies.Length; k++) endingCandies[k].SetActive(k < candyTotal);
        Sfx.Bgm(Sfx.BgmPicnic);
        Sfx.Play(Sfx.Cheer);
        ShowPanel(endingPanel);
    }

    /// <summary>[화면 터치] 도깨비가 다음에 또 온다고 인사하고, 대사를 누르면 챕터 선택으로 가요</summary>
    public void EndingTap()
    {
        if (state != State.Ending) return;
        if (!endingAsking)
        {
            endingAsking = true;
            endingBox.Say(GameScript.EndingBye, faceDokkaebi);
            return;
        }
        if (endingBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        ShowChapters();
    }

    /// <summary>[버튼] 오른쪽 위 일시정지 ↔ 계속하기. 멈춘 동안은 시간(향·관찰)이 흐르지 않아요.</summary>
    /// <summary>[버튼] 오른쪽 위 톱니 : 소리 설정 창 열기 ↔ 닫기. 시작 · 챕터 화면에서도 열려요.
    /// 여는 동안은 시간(향 · 관찰 · 돋보기)이 흐르지 않아요.</summary>
    public void ToggleSettings()
    {
        Sfx.Play(Sfx.Tap);
        SetPaused(!settingsPanel.activeSelf);
    }

    /// <summary>[버튼] 설정 창의 닫기, 창 바깥 누르기</summary>
    public void CloseSettings()
    {
        if (!settingsPanel.activeSelf) return;
        Sfx.Play(Sfx.Tap);
        SetPaused(false);
    }

    /// <summary>[슬라이더] 배경음 볼륨 (저장돼요)</summary>
    public void SetBgmVolume(float volume) => Sfx.BgmVolume = volume;

    float lastSfxSample;

    /// <summary>[슬라이더] 효과음 볼륨 (저장돼요). 움직이면 그 크기로 톡 소리를 들려줘요</summary>
    public void SetSfxVolume(float volume)
    {
        Sfx.SfxVolume = volume;
        if (Time.unscaledTime - lastSfxSample < 0.15f) return;     // 끄는 동안 너무 자주 울리지 않게
        lastSfxSample = Time.unscaledTime;
        Sfx.Play(Sfx.Tap);
    }

    void SetPaused(bool paused)
    {
        if (paused)
        {
            bgmSlider.SetValueWithoutNotify(Sfx.BgmVolume);
            sfxSlider.SetValueWithoutNotify(Sfx.SfxVolume);
        }
        settingsPanel.SetActive(paused);
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
        GameObject[] all = { titlePanel, chapterPanel, dialoguePanel, observePanel, drawPanel, judgePanel, resultPanel, comparePanel, endingPanel };
        foreach (GameObject p in all) p.SetActive(p == panel);
        dialogueDim.SetActive(panel == dialoguePanel || panel == observePanel);
    }

    /// <summary>지금 쓸 수 있는 돋보기 수 (이번 단계 무료 기회 + 게임 전체에서 남은 것)</summary>
    public int MagnifiersReady => (freePeek ? 1 : 0) + magnifiers;

    // 남은 사탕은 또렷하게, 쓴 사탕은 왼쪽부터 흐리게. 돋보기는 쓸 수 있는 횟수를 오른쪽 아래 숫자로,
    // 다 쓰면 숫자 없이 흐리게 (시범 중에는 공짜라 또렷하게)
    void UpdateSnacks()
    {
        int used = candyIcons.Length - candies;
        for (int i = 0; i < candyIcons.Length; i++)
            candyIcons[i].color = i < used ? UsedSnackColor : Color.white;
        int ready = giftRoutine != null ? magnifiers : MagnifiersReady;     // 무료 돋보기가 날아가는 중이면 도착 전 숫자
        magnifierButton.interactable = ready > 0 || peekGuideStep == 1 || peekGuideStep == 2;
        magnifierBadge.SetActive(ready > 0);
        magnifierBadgeText.text = ready.ToString();
    }

    // 무료 기회 : 도깨비가 대사를 시작할 때부터 오른손(화면에서 오른쪽)에 돋보기를 쥐고 있다가,
    //   잠깐 뒤 손을 떠나 포물선으로 맨 위 돋보기 자리에 날아가 반짝여요.
    // 손 자리 : 도깨비 그림(980x956)에서 잰 비율 (가로, 위에서). 방망이를 든 그림과 빈손 그림의 손 자리가 조금 달라요
    static readonly Vector2 HandOnNormal = new Vector2(0.75f, 0.66f);
    static readonly Vector2 HandOnNoBat = new Vector2(0.79f, 0.61f);
    // 돋보기 그림에서 렌즈 가운데 (기준점은 손잡이 끝이라, 날아갈 때 렌즈가 맨 위 돋보기에 맞게 따로 맞춰요)
    static readonly Vector2 GiftLensCenter = new Vector2(0.38f, 0.64f);
    const float GiftHeldAngle = -60f;       // 손에 쥐었을 때 기울기 : 손잡이는 아래, 렌즈는 몸 바깥쪽 위 (얼굴을 가리지 않게)

    Vector3 HandPoint()
    {
        RectTransform rt = resultDokkaebi.rectTransform;
        Vector2 hand = resultDokkaebi.sprite == dokkaebiNoBat ? HandOnNoBat : HandOnNormal;
        Rect r = rt.rect;
        return rt.TransformPoint(new Vector3(r.xMin + hand.x * r.width, r.yMax - hand.y * r.height, 0f));
    }

    // 지금 렌즈 가운데의 화면 위치와 기준점(손잡이 끝)의 차이
    Vector3 GiftLensOffset()
    {
        Rect r = giftMagnifier.rect;
        Vector2 local = new Vector2((GiftLensCenter.x - giftMagnifier.pivot.x) * r.width, (GiftLensCenter.y - giftMagnifier.pivot.y) * r.height);
        return giftMagnifier.TransformPoint(local) - giftMagnifier.position;
    }

    IEnumerator GiftFly()
    {
        var target = (RectTransform)magnifierButton.transform;
        Vector3 to = target.TransformPoint(target.rect.center);
        giftMagnifier.gameObject.SetActive(true);
        giftMagnifier.localScale = Vector3.one;
        giftMagnifier.localRotation = Quaternion.Euler(0f, 0f, GiftHeldAngle);
        giftMagnifier.position = HandPoint();
        const float holdTime = 1.2f, flyTime = 0.75f;
        for (float t = 0f; t < holdTime; t += Time.deltaTime)
        {
            giftMagnifier.position = HandPoint();         // 도깨비가 말하며 움직여도 손에 쥔 채로
            yield return null;
        }
        Sfx.Play(Sfx.Give);
        Vector3 fromLens = giftMagnifier.position + GiftLensOffset();
        for (float t = 0f; t < flyTime; t += Time.deltaTime)
        {
            float p = t / flyTime;
            float e = p * p * (3f - 2f * p);
            giftMagnifier.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(GiftHeldAngle, 0f, e));
            giftMagnifier.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
            Vector3 lens = Vector3.Lerp(fromLens, to, e);
            lens.y += Mathf.Sin(p * Mathf.PI) * Mathf.Abs(to.x - fromLens.x) * 0.3f;   // 포물선
            giftMagnifier.position = lens - GiftLensOffset();
            yield return null;
        }
        giftRoutine = null;
        LandGift();
    }

    void LandGift()
    {
        giftMagnifier.gameObject.SetActive(false);
        giftMagnifier.localScale = Vector3.one;
        giftMagnifier.localRotation = Quaternion.identity;
        UpdateSnacks();
        magnifierSparkles.SetActive(false);
        magnifierSparkles.SetActive(true);
        Sfx.Play(Sfx.SnackIn);
    }

    // 날아가는 중에 화면이 바뀌면 바로 도착한 것으로 해요
    void StopGift()
    {
        if (giftRoutine == null) return;
        StopCoroutine(giftRoutine);
        giftRoutine = null;
        LandGift();
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

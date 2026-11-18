using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// GameManager : 게임 진행(흐름)을 책임지는 "사회자" 스크립트
// ─────────────────────────────────────────────────────────────
// 글로 설명하는 대신 대사와 그림으로 진행해요. 대사나 화면을 누르면 순서대로 넘어가요.
//
//   [로비 씬] 시작 화면 → 챕터 선택 (LobbyManager) → ChapterSelect.Current 를 정하고 이 씬을 열어요
//
//   보통 챕터 (1~4) : 물건을 차례로 (1챕터 5개, 2~4챕터 3개). 2~4챕터는 방에서 시작 장면 → 물건 → 방 끝 장면
//   챕터 안내 : 첫 물건의 첫 그리기 때 1챕터 펜(그려 보기 → 되돌리기 → 다 그렸어!), 2챕터 돋보기, 3챕터 색(붓 → 팔레트)
//   4챕터 : 사탕 0개. 쪽지만 건네고, 물건마다 한 번만 그려요 (틀리면 바로 채점표)
//   [시작 대화] → [떠올리기 3초] → [쪽지 쓰기 60초] → [건네기: 사탕 -1] → [판정 대기] → [결과]
//        ↑ 다시 (이전 그림에 이어 그리기) ← (틀림 + 사탕 남음)          맞음 / 포기 / 사탕 없음 → [채점표] → 다음 물건
//   5번째 물건 뒤 → [끝 장면] → 로비의 챕터 선택
//
//   저주 챕터 (5) : 3단계, 하트 3개
//   [시작 장면] → 단계마다 [단계 대화] → [고르기: 요청 물건 · 맞설 물건] → 떠올리기 … 결과 → 채점표
//   맞설 물건 성공 → 하트 -1 (0이면 해피 엔딩). 요청 물건 실패 → 점프스케어 + 친구 영혼. 같은 물건 두 번 실패 → 게임오버.
//   3단계 끝에 하트가 남으면 배드 엔딩. 엔딩 · 게임오버도 깬 것으로 쳐요.
//
// 쪽지는 주인공이 도깨비에게 써 주는 것이에요. 판정 기준은 시작 대화의 생김새 대사로 알려 줘요.
// 오프닝은 준비물이 사라지는 그림과 함께 보여 줘요. 모든 대사는 터치해야 넘어가요. 떠올리기는 2단계부터 주인공, 1단계는 도깨비가 해요.
// 1단계에서 방망이를 못 찾으면 도깨비는 이야기가 끝날 때까지 빈손이에요.
//
// 화면 구조
//   - 씬은 하나(Game)뿐이고, 화면마다 "패널(Panel)"이라는 UI 묶음이 있어요. 한 번에 패널 하나만 켜요.
//   - 위쪽 HUD(간식, 일시정지, 향)는 그리기부터 채점표까지만 보여요.
//
// 간식 규칙 : 물건(5챕터는 단계)마다 사탕 3개를 새로 받아요(4챕터는 0개). 쪽지를 건넬 때마다 1개. 다 쓰면 다시 그릴 수 없어요.
// 돋보기 : 그리는 중에 맨 위 돋보기를 누르면 렌즈 안으로 원본을 5초 둘러봐요. 향은 계속 타요.
//   - 2챕터부터 챕터마다 1번 (magnifierPerChapter, 챕터를 시작할 때 새로 받아요). 1챕터에는 돋보기가 없어요.
//   - 2챕터 첫 물건의 첫 그리기 때 도깨비가 쓰는 법을 보여 줘요. 이 시범은 공짜이고, 시범 동안은 향이 타지 않아요.
//     눌러 보라는 대사 → (대사를 넘기면 대사가 사라지고 도깨비불 셋이 돋보기 둘레를 돎) → (플레이어가 돋보기를 누름)
//     → 보는 법 대사 → (대사를 넘김) → 5초 둘러보기 → 한 번만 더 빌려준다는 대사
//   - 한 물건에서 2번째로 틀리면 그 물건에서만 쓰는 무료 기회를 1번 줘요 (도깨비가 돋보기를 건네줘요). 단계가 끝나면 사라져요.
//   - 무료 기회가 있으면 그것부터 써요. 돋보기 오른쪽 아래 숫자가 지금 쓸 수 있는 횟수예요.
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

    [Header("데이터 : 챕터 (로비에서 고른 번호의 챕터를 해요)")]
    [SerializeField] ChapterData[] chapters;

    [Header("규칙 (기획서 수치)")]
    [SerializeField] int candiesPerStage = 3;       // 쪽지를 건넬 때 쓰는 사탕 (단계마다)
    [SerializeField] int magnifierPerChapter = 1;   // 돋보기 (챕터마다. 시범과 무료 기회는 따로)
    [SerializeField] int heartsPerCurse = 3;        // 5챕터 도깨비 하트
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
    [SerializeField] GameObject timerGroup;         // 촛대 게이지 (그리는 동안만 보임, 숫자 없음)
    [SerializeField] IncenseBurn incense;           // 막대가 줄고 불꽃이 따라가는 게이지
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
    [SerializeField] Sprite faceBoyNormal;          // 물건 생김새를 차분히 설명할 때
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
    [SerializeField] ColorMixer mixer;
    [SerializeField] PaperView paperView;           // 책상 위 쪽지 : 두 손가락으로 키우기 · 돌리기 · 옮기기
    [SerializeField] GameObject magnifierHighlight;         // 시범 : 돋보기 둘레를 도는 도깨비불
    [SerializeField] RectTransform[] magnifierFires;

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

    [Header("2~4챕터 : 방 시작 · 끝 장면, 복선")]
    [SerializeField] GameObject windowFire;         // 2챕터 시작 첫 대사 : 창밖 도깨비불 (대화 화면 안)
    [SerializeField] GameObject roomEndPanel;       // 방에 찾은 물건 3개가 놓이고 도깨비가 인사
    [SerializeField] Image roomEndDokkaebi;
    [SerializeField] SimpleAnim roomEndDokkaebiAnim;
    [SerializeField] Image[] roomEndItems;
    [SerializeField] DialogueBox roomEndBox;
    [SerializeField] GameObject roomEndEyes;        // 4챕터 독백 : 창 안의 빨간 눈
    [SerializeField] Image judgeDokkaebi;           // 판정 대기 도깨비 (기분에 따라 그림이 달라요)

    [Header("도깨비 기분 (머리 불 색) : [0 보통, 1 주황, 2 붉음]")]
    [SerializeField] Sprite[] moodNormal;
    [SerializeField] Sprite[] moodNoBat;
    [SerializeField] Sprite[] moodSwing;
    [SerializeField] Sprite[] moodThink;
    [SerializeField] Sprite[] moodFace;
    [SerializeField] Sprite[] moodFaceProud;
    [SerializeField] Sprite[] moodFaceTease;

    [Header("챕터 안내 (펜 · 색) : 도깨비불이 누를 곳 둘레를 돌아요")]
    [SerializeField] RectTransform guideHighlight;  // 도깨비불 셋 (GameManager 가 대상 자리로 옮겨요)
    [SerializeField] RectTransform[] guideFires;
    [SerializeField] RectTransform guidePaper;      // 쪽지 (그려 보기)
    [SerializeField] RectTransform guideUndo;       // 되돌리기
    [SerializeField] RectTransform guideSubmit;     // 다 그렸어!
    [SerializeField] RectTransform guideBrush;      // 붓

    [Header("설정 창 : 나가기 (게임 무대에서만)")]
    [SerializeField] GameObject settingsMain;       // 볼륨 두 줄
    [SerializeField] GameObject settingsConfirm;    // [취소] [나가기]
    [SerializeField] GameObject settingsExitButton; // 창 왼쪽 위 나가기 아이콘

    [Header("5챕터 · 도깨비의 저주")]
    [SerializeField] GameObject heartGroup;         // 위 줄 하트 (돋보기와 사탕 사이)
    [SerializeField] Image[] heartIcons;
    [SerializeField] GameObject choosePanel;        // 고르기 : 그림 2개만 (요청 물건 · 맞설 물건, 좌우는 섞어요)
    [SerializeField] Image[] chooseImages;
    [SerializeField] Image dialogueFriend;          // 대화 화면의 친구 (5챕터만)
    [SerializeField] Sprite friendSprite;
    [SerializeField] Sprite friendSpiritSprite;     // 요청 물건을 틀리면 친구 영혼을 빼앗겨 흐려져요
    [SerializeField] Sprite dokkaebiDark;           // 흑화한 도깨비 (빈손)
    [SerializeField] Sprite dokkaebiDarkBat;        // 흑화한 도깨비 (방망이)
    [SerializeField] Sprite forestBright;
    [SerializeField] Sprite forestDark;
    [SerializeField] GameObject scareOverlay;       // 점프스케어 (화면에 덮기만)
    [SerializeField] GameObject jamOverlay;         // 맞설 물건을 그릴 때 방해 연출 (쪽지 위에 덮기만. 그림 · 판정은 안 바뀌어요)
    [SerializeField] RectTransform[] jamFires;
    [SerializeField] GameObject curseEndPanel;      // 게임오버 · 해피 · 배드 엔딩
    [SerializeField] Image curseEndBoy;
    [SerializeField] Image curseEndFriend;
    [SerializeField] Image curseEndDokkaebi;
    [SerializeField] GameObject curseEndSilhouette;
    [SerializeField] GameObject curseEndBat;
    [SerializeField] DialogueBox curseEndBox;

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
    ChapterData chapter;    // 지금 하는 챕터
    int candies;            // 이번 물건에 남은 사탕
    int magnifiers;         // 이번 챕터에서 남은 돋보기
    bool freePeek;          // 이번 단계의 무료 돋보기 (단계가 끝나면 사라져요)
    bool freeGiven;         // 이번 단계에 무료 기회를 이미 줬는지
    int stageWrongs;        // 이번 단계에서 틀린 횟수
    bool peekShown;         // 1단계 돋보기 시범을 했는지
    int peekGuideStep;      // 시범 : 0 없음, 1 눌러 보라고 말함, 2 돋보기 강조 (대사 없음), 3 보는 법을 말함, 4 둘러보는 중, 5 규칙을 말함
    int toolGuideStep;      // 펜 · 색 안내 : 0 없음. 홀수 = 도깨비가 말함, 짝수 = 도깨비불이 누를 곳을 가리킴
    bool guideShown;        // 이번 챕터 안내를 했는지
    Vector2 peekBoxPos;     // 안내 대사 상자 원래 자리 (색 안내 마지막 대사는 팔레트를 가리지 않게 위로 올려요)
    int roomEndLine;        // 방 끝 장면 : 0 대사 전, 1부터 대사 번호

    Coroutine giftRoutine;
    // 5챕터
    int hearts;             // 도깨비 하트 (맞설 물건을 맞히면 -1)
    StageData chosen;       // 이번 단계에 고른 물건
    bool chosenCounter;     // 고른 것이 맞설 물건인지
    bool leftIsCounter;     // 고르기 화면 왼쪽이 맞설 물건인지 (좌우를 섞어요)
    int[] requestFails;     // 단계별 요청 물건 실패 횟수 (두 번이면 게임오버)
    bool friendLost;        // 친구 영혼을 빼앗겼는지
    bool dark;              // 숲이 어두워지고 도깨비가 흑화했는지
    bool gameOverPending;   // 결과 대사를 넘기면 게임오버
    int curseEnd;           // 0 없음, 1 게임오버, 2 해피, 3 배드
    bool curseEndTalking;
    Coroutine scareRoutine;
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

    StageData[] stages => chapter.items;
    bool IsCurse => chapter != null && chapter.rule == ChapterRule.Curse;
    bool BatItem => chapter != null && chapter.number == 1 && stageIndex == 0;    // 1챕터 첫 물건 = 도깨비 방망이
    bool RoomChapter => chapter != null && !IsCurse && chapter.number != 1;        // 2~4챕터 : 방 시작 · 끝 장면
    bool PenGuide => chapter.guide == ChapterGuide.Pen;
    StageData Stage => IsCurse ? chosen : stages[stageIndex];

    void Start()
    {
        boyAnchorShift = new Vector2(0f, dialogueBoy.anchoredPosition.y - BoyWithPos.y);    // 씬은 "도깨비와 함께" 높이로 만들어져 있어요
        StartGame();
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
            if (toolGuideStep != 0)
            {
                UpdateToolGuide();      // 안내 중에는 시간이 흐르지 않아요
                return;
            }
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
            if (jamOverlay.activeSelf) SpinFires(jamFires, 300f, 380f);    // 방해 연출 : 도깨비불이 쪽지 위를 맴돌아요
            timeLeft -= Time.deltaTime;
            incense.Show(timeLeft / drawSeconds, timeLeft);         // 향이 타들어 감
            submitButton.interactable = board.HasDrawn;             // 빈 쪽지면 제출 불가
            if (timeLeft <= 0f) Submit();                           // 향이 다 타면 자동 제출
        }
    }

    /// <summary>로비에서 고른 챕터를 처음부터 시작해요 (에디터에서 바로 Play 하면 1챕터)</summary>
    public void StartGame()
    {
        chapter = System.Array.Find(chapters, c => c != null && c.number == ChapterSelect.Current) ?? chapters[0];
        SetPaused(false);
        attempts = new int[stages.Length];
        cleared = new bool[stages.Length];
        itemsFound = new Sprite[stages.Length];
        requestFails = new int[stages.Length];
        // 방망이 : 1챕터에서는 찾기 전까지 빈손. 다른 챕터는 1챕터 결과대로 (기록이 없으면 들고 있어요)
        batLost = chapter.number != 1 && Progress.BatKnown && !Progress.BatFound;
        magnifiers = chapter.hasMagnifier ? magnifierPerChapter : 0;
        magnifierButton.gameObject.SetActive(chapter.hasMagnifier);     // 1챕터에는 돋보기가 없어요
        ApplyMood(chapter.dokkaebiMood);
        guideShown = false;
        hearts = heartsPerCurse;
        friendLost = false;
        dark = false;
        curseEnd = 0;
        peekShown = chapter.guide != ChapterGuide.Magnifier;     // 돋보기 시범은 2챕터 첫 물건에서만
        backgroundImage.sprite = PlayBackground;
        int first = 0;
#if UNITY_EDITOR
        // (에디터 전용) 앞 물건을 건너뛰어 녹화 · 확인. 건너뛴 물건은 찾은 것으로 쳐요
        if (!IsCurse)
        {
            first = Mathf.Clamp(editorStartStage - 1, 0, stages.Length - 1);
            for (int i = 0; i < first; i++)
            {
                cleared[i] = true;
                attempts[i] = 1;
                itemsFound[i] = stages[i].originalSprite;
            }
            if (first > 0) { peekShown = true; guideShown = true; }
        }
#endif
        StartStage(first);
    }

    Sprite PlayBackground => IsCurse ? (dark ? forestDark : forestBright) : chapter.playBackground != null ? chapter.playBackground : gameBackground;

    /// <summary>지금 도깨비 그림 : 방망이를 들었는지, 흑화했는지</summary>
    Sprite DokkaebiSprite(bool swing = false)
    {
        bool noBat = BatItem || batLost;
        if (dark) return noBat ? dokkaebiDark : dokkaebiDarkBat;
        return noBat ? dokkaebiNoBat : swing ? dokkaebiSwing : dokkaebiNormal;
    }

    // ───────────── 오프닝 + 대화 ─────────────

    void StartStage(int index)
    {
        state = State.Dialogue;
        stageIndex = index;
        candies = chapter.candies;
        freePeek = false;
        freeGiven = false;
        stageWrongs = 0;
        snackIntroPending = true;
        lastPresent = null;
        gameOverPending = false;
        if (IsCurse) chosen = stages[index];        // 고르기 전까지는 요청 물건으로
        toolBar.Setup(chapter.penMode);             // 1 · 2챕터 검정 펜, 3챕터부터 빨강 · 노랑 · 파랑 섞기
        hud.SetActive(false);
        timerGroup.SetActive(false);
        backgroundImage.sprite = PlayBackground;
        UpdateSnacks();
        Sfx.Bgm(Sfx.BgmRoom);

        giveNote.SetActive(false);
        giveSnackOut.gameObject.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebi.GetComponent<SimpleAnim>().enabled = true;     // 대화에서는 도깨비가 걸어 들어와요
        dialogueDokkaebiImage.sprite = DokkaebiSprite();
        ShowFriend();
        ShowPanel(dialoguePanel);

        opening = AloneAt(0);                       // 챕터 맨 처음에는 주인공 혼자 말해요
        openingGroup.SetActive(opening && BatItem);
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
        GameScript.Line line = IntroLines()[dialogueLine];
        dialogueBox.Say(line.text, FaceOf(line.face));

        // 5챕터 시작 장면 : 간식 이야기를 꺼내면 숲이 어두워지고 도깨비가 흑화해요
        if (IsCurse && stageIndex == 0 && !dark && dialogueLine >= GameScript.CurseDarkFrom)
        {
            dark = true;
            backgroundImage.sprite = PlayBackground;
            dialogueDokkaebiImage.sprite = DokkaebiSprite();
            UpdateSnacks();
            Sfx.Play(Sfx.Fail);
        }

        opening = AloneAt(dialogueLine);
        boyTargetPos = (opening ? BoyAlonePos : BoyWithPos) + boyAnchorShift;
        boyTargetSize = opening ? BoyAloneSize : BoyWithSize;
        openingGroup.SetActive(opening && BatItem);
        windowFire.SetActive(chapter.windowFire && stageIndex == 0 && dialogueLine == 0);    // 2챕터 복선 : 창밖 도깨비불
        if (opening)
        {
            dialogueDim.SetActive(false);
            dialogueDokkaebi.SetActive(false);
            if (BatItem) ShowOpeningBeat(dialogueLine);
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

    /// <summary>이 대사에서 주인공 혼자 있는지 (챕터 첫 물건의 앞쪽 대사, 도깨비가 나오기 전)</summary>
    bool AloneAt(int line) =>
        BatItem ? line < GameScript.OpeningAloneLines : RoomChapter && stageIndex == 0 && line < chapter.aloneLines;

    // 시작 대화 : 보통 챕터는 물건마다 (2~4챕터 첫 물건은 챕터 시작 장면 +), 5챕터는 (첫 단계만 시작 장면 +) 단계 대화
    GameScript.Line[] IntroLines()
    {
        if (RoomChapter) return GameScript.RoomIntroLines(chapter.number, stageIndex);
        if (!IsCurse) return GameScript.IntroLines(stageIndex, batLost);
        GameScript.Line[] step = GameScript.CurseStepLines(stageIndex);
        if (stageIndex != 0) return step;
        var all = new GameScript.Line[GameScript.CurseIntro.Length + step.Length];
        GameScript.CurseIntro.CopyTo(all, 0);
        step.CopyTo(all, GameScript.CurseIntro.Length);
        return all;
    }

    void NextIntroLine()
    {
        GameScript.Line[] lines = IntroLines();
        if (dialogueLine < lines.Length - 1)
        {
            dialogueLine++;
            SayIntroLine();
        }
        else if (IsCurse)
        {
            ShowChoose();
        }
        else
        {
            StartObserve();
        }
    }

    // 5챕터 친구 : 화면에 세워 둬요. 요청 물건을 틀리면 영혼을 빼앗겨 흐려져요
    void ShowFriend()
    {
        dialogueFriend.gameObject.SetActive(IsCurse);
        dialogueFriend.sprite = friendLost ? friendSpiritSprite : friendSprite;
    }

    // ───────────── 5챕터 고르기 ─────────────

    void ShowChoose()
    {
        state = State.Dialogue;
        leftIsCounter = Random.value < 0.5f;
        chooseImages[0].sprite = (leftIsCounter ? chapter.counterItems[stageIndex] : stages[stageIndex]).originalSprite;
        chooseImages[1].sprite = (leftIsCounter ? stages[stageIndex] : chapter.counterItems[stageIndex]).originalSprite;
        ShowPanel(choosePanel);
    }

    /// <summary>[버튼] 고르기 : 0 왼쪽, 1 오른쪽 그림. 고른 물건을 떠올리고 그려요</summary>
    public void ChooseItem(int side)
    {
        if (!IsCurse || !choosePanel.activeSelf) return;
        Sfx.Play(Sfx.Tap);
        chosenCounter = (side == 0) == leftIsCounter;
        chosen = chosenCounter ? chapter.counterItems[stageIndex] : stages[stageIndex];
        StartObserve();
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
        bool dokkaebiRecalls = BatItem;
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
        // 게이지를 바로 맞춰 둬요 (시범 동안은 게이지가 멈춰 있어서, 안 맞추면 불꽃이 왼쪽 끝에 남아 있어요)
        incense.Show(timeLeft / drawSeconds, timeLeft);
        // 쪽지 글은 주인공이 적어 둔 것처럼 한 글자씩 나타나요. 다시 그릴 때는 이미 적혀 있어요.
        drawTitle.text = NoteText();
        if (noteRoutine != null) StopCoroutine(noteRoutine);
        drawTitle.maxVisibleCharacters = keepDrawing ? int.MaxValue : 0;
        hud.SetActive(true);
        timerGroup.SetActive(true);
        Sfx.Bgm(Sfx.BgmDraw);
        ShowPanel(drawPanel);
        magnifierPeek.Stop();
        StopGift();
        toolBar.ClosePanel();

        // 챕터 첫 그리기 : 도깨비가 안내해요 (1챕터 펜, 2챕터 돋보기, 3챕터 색)
        peekGuideStep = 0;
        toolGuideStep = 0;
        peekGuide.SetActive(false);
        guideHighlight.gameObject.SetActive(false);
        ShowMagnifierHighlight(false);
        magnifierButton.transform.localScale = Vector3.one;
        bool firstDraw = stageIndex == 0 && !keepDrawing && !guideShown;
        if (firstDraw && (chapter.guide == ChapterGuide.Pen || chapter.guide == ChapterGuide.Mix))
        {
            guideShown = true;
            StartToolGuide();
        }
        else if (chapter.guide == ChapterGuide.Magnifier && stageIndex == 0 && !peekShown && !keepDrawing)
        {
            peekShown = true;
            peekGuideStep = 1;
            peekGuide.SetActive(true);
            peekGuideBox.Say(GameScript.MagnifierShow, faceDokkaebi);
        }

        // 5챕터 맞설 물건 : 방해 연출 (쪽지 위에 덮기만)
        jamOverlay.SetActive(IsCurse && chosenCounter);

        UpdateSnacks();
        if (!keepDrawing) noteRoutine = StartCoroutine(WriteNote());

        bool snackIn = snackIntroPending && chapter.candies > 0;     // 4챕터는 사탕이 없어서 들어오지 않아요
        snackIntroPending = false;
        if (snackIn)
        {
            foreach (SimpleAnim a in snackAnims) a.Play();     // 사탕은 오른쪽 끝에서 왼쪽으로
            Sfx.Play(Sfx.SnackIn);
        }
    }

    /// <summary>[화면 터치] 돋보기 시범 중 대사 넘기기. 눌러 보라는 대사는 돋보기를 직접 눌러야 넘어가요</summary>
    public void PeekGuideTap()
    {
        if (state == State.Draw && toolGuideStep != 0) { ToolGuideTap(); return; }
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
        if (peekGuideStep != 0) return;
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
        if (state != State.Draw || peekGuideStep != 0 || toolGuideStep != 0) return;
        magnifierPeek.Stop();
        jamOverlay.SetActive(false);
        toolBar.ClosePanel();

        state = State.Give;
        savedDrawTime = Mathf.Max(timeLeft, 10f);
        timerGroup.SetActive(false);
        Sfx.Play(Sfx.Submit);

        giveStep = 0;
        giveSnackOut.gameObject.SetActive(false);
        giveNoteText.text = NoteText();
        giveNoteDrawing.texture = board.Texture;
        openingGroup.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebi.GetComponent<SimpleAnim>().enabled = false;    // 건넬 때는 이미 곁에 있어요
        dialogueDokkaebi.SetActive(true);
        dialogueDokkaebiImage.sprite = DokkaebiSprite();
        ShowFriend();
        boyTargetPos = BoyWithPos + boyAnchorShift;
        boyTargetSize = BoyWithSize;
        dialogueBoy.anchoredPosition = boyTargetPos;
        dialogueBoy.sizeDelta = boyTargetSize;
        ShowPanel(dialoguePanel);
        giveNote.SetActive(true);
        giveNoteAnim.Play(SimpleAnim.Kind.Float);
        dialogueBoyAnim.Play(SimpleAnim.Kind.Gentle);
        dialogueDokkaebiAnim.Play(SimpleAnim.Kind.None);
        dialogueBox.Say(chapter.candies > 0 ? GameScript.GiveBoy : GameScript.GiveBoyNoCandy, faceBoyHappy);
    }

    // 주인공이 건네면 쪽지와 간식이 날아가고, 도깨비가 먹고 읽은 뒤 찾으러 가요
    void GiveTap()
    {
        if (dialogueBox.FinishTyping()) return;
        Sfx.Play(giveStep == 0 ? Sfx.Give : Sfx.Tap);     // 쪽지와 사탕이 날아갈 때는 건네는 소리
        if (giveStep == 0 && chapter.candies <= 0)
        {
            // 4챕터 : 사탕 없이 쪽지만 건네요. 도깨비는 먹지 않고 바로 읽어요
            giveStep = 2;
            attempts[stageIndex]++;
            giveNoteAnim.Play(SimpleAnim.Kind.NoteGive);
            dialogueBoyAnim.Play(SimpleAnim.Kind.None);
            dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
            dialogueBox.Say(GameScript.ItemRead(Stage.targetName), faceDokkaebi);
        }
        else if (giveStep == 0)
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
            dialogueBox.Say(chapter.number != 1 ? GameScript.ItemRead(Stage.targetName) : GameScript.GiveReadLine(stageIndex), faceDokkaebi);
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
        judgeNoteText.text = NoteText();
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
            StartCoroutine(judge.Judge(Stage, board.FlatTexture(),   // AI 에는 흰 바탕에 합친 그림을 보내요
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
        if (chapter.candies > 0) candies++;
        attempts[stageIndex]--;
        judgeTicket++;      // 이 제출은 끝난 것으로 (늦게 오는 결과는 무시)
        UpdateSnacks();
        Debug.LogWarning("[OpenAIJudge] " + message);

        state = State.Result;
        resultPhase = ResultPhase.Error;
        resultItemImage.enabled = false;
        resultItemShadow.SetActive(false);
        resultSparkles.SetActive(false);
        resultDokkaebi.sprite = DokkaebiSprite();
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

        // 맞으면 진짜 물건, 틀리면 n번째로 틀린 만큼 엉뚱한 물건 (물건마다 오답 3장)
        Sprite item = success ? Stage.originalSprite : Stage.WrongSpriteFor(stageWrongs);
        resultItemImage.sprite = item;
        resultItemImage.enabled = item != null;
        itemsFound[stageIndex] = item;
        resultItemShadow.SetActive(true);
        resultSparkles.SetActive(success);
        choiceRow.SetActive(false);

        // 방망이는 화면에 하나만 보여요.
        //   1챕터 첫 물건 : 맞히면 방망이가 물건 자리에 놓여 있으니 도깨비는 빈손 (주인공이 말할 때 집어 들어요, ResultTap)
        //   그 뒤 : 방망이를 못 찾았으면(batLost) 빈손, 아니면 방망이를 휘둘러요
        resultDokkaebi.sprite = DokkaebiSprite(swing: true);
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.Swing);
        Sfx.Play(Sfx.Item);
        Sfx.Play(success ? Sfx.Success : Sfx.Fail);

        string line = RoomChapter ? GameScript.RoomResult(chapter.number, stageIndex, success)
                    : GameScript.DokkaebiResult(Mathf.Min(stageIndex, 4), outcome);
        if (IsCurse)
        {
            if (success && chosenCounter)
            {
                hearts = Mathf.Max(0, hearts - 1);      // 맞설 물건 성공 : 도깨비 하트 -1
                UpdateSnacks();
                line = GameScript.CurseCounterHit;
            }
            else if (success)
            {
                line = GameScript.CurseBoastLine(stageIndex);       // 요청 물건 성공 : 도깨비가 힘 자랑
            }
            else if (chosenCounter)
            {
                line = GameScript.CurseCounterWrong;
            }
            else
            {
                // 요청 물건 실패 : 점프스케어 → 친구 영혼을 빼앗겨요. 같은 물건을 두 번 틀리면 게임오버
                requestFails[stageIndex]++;
                friendLost = true;
                gameOverPending = requestFails[stageIndex] >= 2;
                line = GameScript.CurseRequestWrong;       // 게임오버 대사는 게임오버 화면에서 한 번만
                if (scareRoutine != null) StopCoroutine(scareRoutine);
                scareRoutine = StartCoroutine(Scare());
            }
        }
        resultBox.Say(line, success ? faceDokkaebiProud : faceDokkaebiTease);
    }

    // 점프스케어 : 무서운 얼굴이 잠깐 화면을 덮어요 (그림 · 판정은 안 바뀌어요)
    IEnumerator Scare()
    {
        scareOverlay.SetActive(true);
        Sfx.Play(Sfx.Fail);
        yield return new WaitForSeconds(0.9f);
        scareOverlay.SetActive(false);
        scareRoutine = null;
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
        else if (gameOverPending)
        {
            ShowCurseEnd(1);
        }
        else if (resultPhase == ResultPhase.Dokkaebi)
        {
            bool wrong = outcome != 0;
            resultPhase = ResultPhase.Boy;
            if (BatItem && !wrong) batLost = false;
            resultDokkaebi.sprite = BatItem && !wrong ? (dark ? dokkaebiDarkBat : dokkaebiNormal) : DokkaebiSprite();
            // 1챕터 첫 물건을 맞혔으면 도깨비가 바닥의 방망이를 집어 들어요 : 바닥 방망이 · 그림자 · 반짝이를 치워요
            if (BatItem && !wrong)
            {
                resultItemImage.enabled = false;
                resultItemShadow.SetActive(false);
                resultSparkles.SetActive(false);
            }
            resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
            string line = wrong && candies <= 0 && chapter.CanRetry ? GameScript.BoyOutOfSnacks
                : RoomChapter ? GameScript.RoomBoyResult(chapter.number, stageIndex, !wrong)
                : IsCurse ? (wrong ? GameScript.CurseBoyWrong : chosenCounter ? GameScript.CurseBoyHit : GameScript.CurseBoyBoast)
                : GameScript.BoyResult(stageIndex, outcome);
            resultBox.Say(line, wrong ? faceBoySad : faceBoyHappy);     // 엉뚱한 물건 · 간식 없음 : 우는 얼굴
        }
        else if (resultPhase == ResultPhase.Boy && outcome != 0 && candies > 0 && stageWrongs == 2 && !freeGiven && chapter.hasMagnifier)
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

        freePeek = false;           // 무료 기회는 그 물건에서만
        StopGift();
        UpdateSnacks();
        if (BatItem) batLost = !cleared[0];
        jamOverlay.SetActive(false);
        Sfx.Play(Sfx.Mark);
        ShowPanel(comparePanel);
    }

    /// <summary>[화면 터치] 채점표 다음으로</summary>
    public void NextStage()
    {
        Sfx.Play(Sfx.Tap);
        if (IsCurse && hearts <= 0) ShowCurseEnd(2);                  // 맞설 물건으로 하트를 다 깎음 : 해피 엔딩
        else if (stageIndex + 1 < stages.Length) StartStage(stageIndex + 1);
        else if (IsCurse) ShowCurseEnd(3);                            // 3단계 끝에 하트가 남음 : 배드 엔딩
        else ShowEnding();
    }

    // ───────────── 이야기 종료 ─────────────

    /// <summary>소풍 장면 : 물건이 날아와 놓이고, 단계마다 남은 사탕이 사탕 통에 모여요.</summary>
    void ShowEnding()
    {
        if (RoomChapter) { ShowRoomEnd(); return; }
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = chapter.endBackground != null ? chapter.endBackground : endingBackground;
        endingAsking = false;
        endingBox.gameObject.SetActive(false);
        endingDokkaebi.sprite = batLost ? dokkaebiNoBat : dokkaebiNormal;
        for (int i = 0; i < endingItems.Length && i < stages.Length; i++)
            endingItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
        // 물건을 다 거치면 깬 것 (정답이 아니어도). 1챕터는 방망이를 찾았는지도 저장해요
        Progress.MarkCleared(chapter.number);
        if (chapter.number == 1) Progress.BatFound = !batLost;
        // 방망이는 화면에 하나만 : 찾았으면 도깨비 손에만 있고 돗자리 방망이 자리는 비워요.
        //   못 찾았으면 도깨비는 빈손이고 그 자리에 받은 엉뚱한 물건이 놓여요.
        endingItems[0].gameObject.SetActive(batLost);
        Sfx.Bgm(Sfx.BgmPicnic);
        Sfx.Play(Sfx.Cheer);
        ShowPanel(endingPanel);
    }

    /// <summary>[화면 터치] 도깨비가 다음에 또 온다고 인사하고, 대사를 누르면 챕터 선택으로 가요</summary>
    public void EndingTap()
    {
        if (state != State.Ending) return;
        if (RoomChapter) { RoomEndTap(); return; }
        if (!endingAsking)
        {
            endingAsking = true;
            endingBox.Say(GameScript.EndingBye, faceDokkaebi);
            return;
        }
        if (endingBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        ChapterSelect.BackToChapters();
    }

    // ───────────── 2~4챕터 끝 장면 ─────────────

    /// <summary>방 끝 장면 : 찾은 물건 3개가 방에 놓여요. 화면을 누르면 도깨비 인사 (4챕터는 도깨비가 떠나고 주인공 독백)</summary>
    void ShowRoomEnd()
    {
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = chapter.endBackground != null ? chapter.endBackground : PlayBackground;
        roomEndLine = 0;
        roomEndBox.gameObject.SetActive(false);
        roomEndEyes.SetActive(false);
        roomEndDokkaebi.gameObject.SetActive(true);
        roomEndDokkaebi.color = Color.white;
        roomEndDokkaebi.sprite = DokkaebiSprite();
        roomEndDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
        for (int i = 0; i < roomEndItems.Length; i++)
        {
            bool used = i < stages.Length;
            roomEndItems[i].gameObject.SetActive(used);
            if (used) roomEndItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
        }
        Progress.MarkCleared(chapter.number);       // 물건을 다 거치면 깬 것 → 다음 챕터가 열려요
        Sfx.Bgm(Sfx.BgmRoom);
        Sfx.Play(Sfx.Cheer);
        ShowPanel(roomEndPanel);
    }

    /// <summary>[화면 터치] 방 끝 장면 : 대사를 하나씩, 다 보면 챕터 선택으로</summary>
    public void RoomEndTap()
    {
        if (state != State.Ending || !RoomChapter) return;
        if (roomEndBox.gameObject.activeSelf && roomEndBox.FinishTyping()) return;
        GameScript.Line[] lines = GameScript.RoomEndLines(chapter.number);
        if (roomEndLine >= lines.Length)
        {
            Sfx.Play(Sfx.Tap);
            ChapterSelect.BackToChapters();
            return;
        }
        if (roomEndLine > 0) Sfx.Play(Sfx.Tap);
        GameScript.Line line = lines[roomEndLine];
        // 주인공 독백 (4챕터) : 도깨비는 떠나고, 창 안에서 빨간 눈이 지켜봐요
        if (line.face != GameScript.Face.Dokkaebi && roomEndLine > 0)
        {
            roomEndDokkaebiAnim.Play(SimpleAnim.Kind.Vanish);
            roomEndEyes.SetActive(chapter.endEyes);
        }
        roomEndLine++;
        roomEndBox.Say(line.text, FaceOf(line.face));
    }

    // ───────────── 챕터 안내 : 펜 (1챕터) · 색 (3챕터) ─────────────
    //   홀수 단계 = 도깨비가 말해요 (화면을 누르면 다음). 짝수 단계 = 대사가 사라지고 도깨비불이 누를 곳 둘레를 돌아요.
    //   펜 : 1 "그려 봐" → 2 쪽지 (그리면 다음) → 3 "되돌려" → 4 되돌리기 (누르면 다음) → 5 "다 그렸으면" → 6 다 그렸어! (화면을 누르면 끝)
    //   색 : 1 "붓을 눌러 봐" → 2 붓 (팔레트가 열리면 다음) → 3 "하나만 써도, 섞어도 돼" (누르면 끝)
    //   안내하는 동안은 시간이 흐르지 않아요.

    void StartToolGuide()
    {
        toolGuideStep = 1;
        peekBoxPos = ((RectTransform)peekGuideBox.transform).anchoredPosition;
        SayToolGuide();
    }

    string[] ToolGuideLines => PenGuide ? new[] { GameScript.PenDraw, GameScript.PenUndo, GameScript.PenDone } : new[] { GameScript.MixBrush, GameScript.MixHow };

    RectTransform ToolGuideTarget(int step) =>
        PenGuide ? (step == 2 ? guidePaper : step == 4 ? guideUndo : guideSubmit) : guideBrush;

    void SayToolGuide()
    {
        int index = (toolGuideStep - 1) / 2;
        peekGuide.SetActive(true);
        peekGuideBox.gameObject.SetActive(true);
        // 색 안내 마지막 대사는 열린 팔레트를 가리지 않게 화면 위쪽에서 말해요
        bool top = !PenGuide && index == 1;
        ((RectTransform)peekGuideBox.transform).anchoredPosition = top ? new Vector2(peekBoxPos.x, peekBoxPos.y + GuideBoxRise) : peekBoxPos;
        peekGuideBox.Say(ToolGuideLines[index], faceDokkaebi);
        guideHighlight.gameObject.SetActive(false);
    }

    const float GuideBoxRise = 1000f;      // 색 안내 마지막 대사 : 아래 자리에서 이만큼 위로 (팔레트 위, 위 줄 아래)

    void PointToolGuide()
    {
        // 마지막(다 그렸어!)은 가리키기만 하고 화면을 누르면 끝나요. 그 전에는 덮개를 치워서 쪽지 · 버튼을 직접 눌러요
        bool lastPen = PenGuide && toolGuideStep == 6;
        peekGuide.SetActive(lastPen);
        peekGuideBox.gameObject.SetActive(false);
        guideHighlight.gameObject.SetActive(true);
        UpdateToolGuide();
    }

    void ToolGuideTap()
    {
        if (peekGuideBox.gameObject.activeSelf && peekGuideBox.FinishTyping()) return;
        bool talking = toolGuideStep % 2 == 1;
        if (talking)
        {
            Sfx.Play(Sfx.Tap);
            bool lastLine = (toolGuideStep + 1) / 2 >= ToolGuideLines.Length;
            if (lastLine && !PenGuide) { EndToolGuide(); return; }     // 색 안내 마지막 대사 : 끝
            toolGuideStep++;
            PointToolGuide();
            return;
        }
        if (PenGuide && toolGuideStep == 6)
        {
            Sfx.Play(Sfx.Tap);
            EndToolGuide();                         // 다 그렸어! 를 가리킨 뒤 화면을 누르면 끝
        }
    }

    // 도깨비불을 대상 자리로 옮기고 돌려요. 해야 할 일을 하면 다음 대사로
    void UpdateToolGuide()
    {
        if (toolGuideStep % 2 == 1) return;
        RectTransform target = ToolGuideTarget(toolGuideStep);
        Rect r = target.rect;
        guideHighlight.position = target.TransformPoint(r.center);
        Vector3 scale = target.lossyScale;
        Vector3 own = guideHighlight.lossyScale;
        float rx = Mathf.Min(r.width * 0.5f * scale.x / own.x + 10f, 470f);
        float ry = Mathf.Min(r.height * 0.5f * scale.y / own.y + 10f, 560f);
        // 화면 가장자리의 붓 · 버튼이면 도깨비불이 화면 밖으로 나가지 않게 원을 줄여요
        var area = (RectTransform)guideHighlight.parent;
        Vector2 c = area.InverseTransformPoint(guideHighlight.position);
        Rect a = area.rect;
        rx = Mathf.Max(30f, Mathf.Min(rx, c.x - a.xMin - 50f, a.xMax - c.x - 50f));
        ry = Mathf.Max(30f, Mathf.Min(ry, c.y - a.yMin - 60f, a.yMax - c.y - 60f));
        SpinFires(guideFires, rx, ry);
        bool done = PenGuide ? (toolGuideStep == 2 && board.HasDrawn) || (toolGuideStep == 4 && board.CanRedo)
                             : toolGuideStep == 2 && toolBar.PanelOpen;
        if (!done) return;
        toolGuideStep++;
        SayToolGuide();
    }

    void EndToolGuide()
    {
        toolGuideStep = 0;
        peekGuide.SetActive(false);
        peekGuideBox.gameObject.SetActive(true);
        ((RectTransform)peekGuideBox.transform).anchoredPosition = peekBoxPos;
        guideHighlight.gameObject.SetActive(false);
    }

    /// <summary>도깨비 기분(머리 불 색)에 맞는 그림으로 바꿔요 : 0 보통, 1 주황 (3챕터), 2 붉음 (4챕터)</summary>
    void ApplyMood(int mood)
    {
        Sprite Pick(Sprite[] set, Sprite keep) => set != null && mood >= 0 && mood < set.Length && set[mood] != null ? set[mood] : keep;
        dokkaebiNormal = Pick(moodNormal, dokkaebiNormal);
        dokkaebiNoBat = Pick(moodNoBat, dokkaebiNoBat);
        dokkaebiSwing = Pick(moodSwing, dokkaebiSwing);
        faceDokkaebi = Pick(moodFace, faceDokkaebi);
        faceDokkaebiProud = Pick(moodFaceProud, faceDokkaebiProud);
        faceDokkaebiTease = Pick(moodFaceTease, faceDokkaebiTease);
        if (judgeDokkaebi != null) judgeDokkaebi.sprite = Pick(moodThink, judgeDokkaebi.sprite);
    }


    // ───────────── 5챕터 엔딩 ─────────────

    /// <summary>kind : 1 게임오버, 2 해피 엔딩, 3 배드 엔딩. 모두 깬 것으로 치고 챕터 선택으로 가요</summary>
    void ShowCurseEnd(int kind)
    {
        state = State.Ending;
        curseEnd = kind;
        curseEndTalking = false;
        gameOverPending = false;
        hud.SetActive(kind != 1);
        timerGroup.SetActive(false);
        scareOverlay.SetActive(false);
        bool happy = kind == 2;
        backgroundImage.sprite = happy ? forestBright : forestDark;
        curseEndSilhouette.SetActive(kind == 1);
        curseEndBoy.gameObject.SetActive(kind != 1);
        curseEndFriend.gameObject.SetActive(kind != 1);
        curseEndFriend.sprite = friendLost && !happy ? friendSpiritSprite : friendSprite;
        curseEndDokkaebi.sprite = happy ? (batLost ? dokkaebiNoBat : dokkaebiNormal) : DokkaebiSprite();
        curseEndBat.SetActive(happy && !batLost);       // 해피 엔딩 : 들고 있던 방망이만 두고 가요
        curseEndBox.gameObject.SetActive(false);
        Progress.MarkCleared(chapter.number);
        Sfx.Play(happy ? Sfx.Cheer : Sfx.Fail);
        ShowPanel(curseEndPanel);
        CurseEndTap();
    }

    /// <summary>[화면 터치] 엔딩 대사 → 챕터 선택</summary>
    public void CurseEndTap()
    {
        if (curseEnd == 0) return;
        if (!curseEndTalking)
        {
            curseEndTalking = true;
            string line = curseEnd == 1 ? GameScript.CurseGameOver : curseEnd == 2 ? (batLost ? GameScript.CurseHappyNoBat : GameScript.CurseHappy) : GameScript.CurseBad;
            curseEndBox.Say(line, curseEnd == 2 ? faceBoyHappy : faceDokkaebi);
            return;
        }
        if (curseEndBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        ChapterSelect.BackToChapters();
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

    /// <summary>[버튼] 설정 창 왼쪽 위 나가기 : 창 안이 [취소] [나가기] 로 바뀌어요</summary>
    public void AskExit()
    {
        Sfx.Play(Sfx.Tap);
        settingsMain.SetActive(false);
        settingsConfirm.SetActive(true);
    }

    /// <summary>[버튼] 취소 : 다시 볼륨 설정으로</summary>
    public void CancelExit()
    {
        Sfx.Play(Sfx.Tap);
        settingsMain.SetActive(true);
        settingsConfirm.SetActive(false);
    }

    /// <summary>[버튼] 나가기 : 챕터 선택으로 (그 챕터는 다음에 처음부터)</summary>
    public void ExitToChapters()
    {
        Sfx.Play(Sfx.Tap);
        ChapterSelect.BackToChapters();
    }

    void SetPaused(bool paused)
    {
        settingsMain.SetActive(true);
        settingsConfirm.SetActive(false);
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
        : face == GameScript.Face.BoySad ? faceBoySad : face == GameScript.Face.BoyNormal ? faceBoyNormal : faceDokkaebi;

    // (에디터에서만) 제출한 그림과 판정 결과를 Temp/Submissions 에 저장 → 판정 기준을 조절할 때 참고해요.
    void SaveSubmission(OpenAIJudge.Verdict verdict)
    {
#if UNITY_EDITOR
        string dir = System.IO.Path.Combine(Application.dataPath, "..", "Temp", "Submissions");
        System.IO.Directory.CreateDirectory(dir);
        string name = $"{System.DateTime.Now:MMdd_HHmmss}_stage{Stage.stageNumber}";
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), board.FlatTexture().EncodeToPNG());
        string log = "";
        for (int i = 0; i < verdict.present.Length; i++)
            log += $"{Stage.features[i].id} {(verdict.present[i] ? "O" : "X")} {Stage.features[i].name} : {verdict.notes?[i]}\n";
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".txt"), log);
#endif
    }

    // 패널 하나만 켜고 나머지는 끄기
    void ShowPanel(GameObject panel)
    {
        GameObject[] all = { dialoguePanel, observePanel, drawPanel, judgePanel, resultPanel, comparePanel, endingPanel, choosePanel, curseEndPanel, roomEndPanel };
        foreach (GameObject p in all) p.SetActive(p == panel);
        dialogueDim.SetActive(panel == dialoguePanel || panel == observePanel || panel == choosePanel);
    }

    /// <summary>지금 쓸 수 있는 돋보기 수 (이번 물건 무료 기회 + 이번 챕터에서 남은 것)</summary>
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
        // 5챕터 하트 : 남은 만큼 또렷하게
        heartGroup.SetActive(IsCurse && dark);
        for (int i = 0; i < heartIcons.Length; i++) heartIcons[i].color = i < hearts ? Color.white : UsedSnackColor;
    }

    string NoteText() => chapter.number != 1 ? GameScript.ItemNote(Stage.targetName) : GameScript.NoteText(stageIndex);

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

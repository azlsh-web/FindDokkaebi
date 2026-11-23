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
//   저주 챕터 (5) : 3단계, 주인공 하트 3개 (챕터 전체), 사탕 없음, 기회는 한 번씩
//   [시작 장면] → 단계마다 [단계 대화] → [고르기: 요청 물건 · 맞설 물건]
//     요청 물건 → 그리지 않고 도깨비가 가져가요 (하트 -1) → 같은 단계 고르기
//     맞설 물건 → 떠올리기 → 그리기 (방해 연출) → 쪽지를 들이밀기 → 판정 대기 → 결과 (채점표 · 다시 · 포기 없음)
//       성공 → "제법이군" → 다음 단계 (3단계를 다 이기면 해피 엔딩)
//       실패 → 점프스케어 (하트 -1) → 같은 단계 고르기
//   하트가 0이 되면 친구가 쓰러지고 영혼을 빼앗겨요 → 게임오버. 엔딩 · 게임오버도 깬 것으로 쳐요.
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
//
// 파일 나눔 (partial class : 한 클래스를 여러 파일에 나눠 적어요)
//   GameManager.cs           칸 · 시작 · 설정 창 · 도우미
//   GameManager.Dialogue.cs  오프닝 · 대화 · 5챕터 고르기 · 떠올리기
//   GameManager.Draw.cs      그리기 · 돋보기 · 건네기 · 판정 대기
//   GameManager.Result.cs    결과 · 채점표 · 이야기 종료 · 2~4챕터 끝 장면
//   GameManager.Guide.cs     챕터 안내 (펜 · 색) · 도깨비 기분
//   GameManager.Chapter5.cs  5챕터 빼앗기기 · 결과 · 엔딩
public partial class GameManager : MonoBehaviour
{
    enum State { Title, Chapter, Dialogue, Observe, Draw, Give, Judge, Result, Compare, Ending }
    enum ResultPhase { Dokkaebi, Boy, Gift, Choose, Error }

    // ───────────── Inspector 에서 연결하는 것들 ─────────────

    [Header("데이터 : 챕터 (로비에서 고른 번호의 챕터를 해요)")]
    [SerializeField] ChapterData[] chapters;

    [Header("규칙 (기획서 수치)")]
    [SerializeField] int candiesPerStage = 3;       // 쪽지를 건넬 때 쓰는 사탕 (단계마다)
    [SerializeField] int magnifierPerChapter = 1;   // 돋보기 (챕터마다. 시범과 무료 기회는 따로)
    [SerializeField] int heartsPerCurse = 3;        // 5챕터 주인공 하트 (챕터 전체)
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
    [SerializeField] Sprite faceFriend;            // 5챕터 친구 귓속말 (Friend_Original 에서 얼굴만 오린 임시 그림)
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
    [SerializeField] Sprite[] faceDark;             // 흑화한 도깨비 대사 얼굴 : 보통 · 뿌듯 · 놀림 (B1, 아트팀 얼굴 색만 바꾼 임시)
    [SerializeField] Sprite forestBright;
    [SerializeField] Sprite forestDark;
    [SerializeField] GameObject scareOverlay;       // 점프스케어 (화면에 덮기만)
    [SerializeField] GameObject jamOverlay;         // 맞설 물건을 그릴 때 방해 연출 (쪽지 위에 덮기만. 그림 · 판정은 안 바뀌어요)
    [SerializeField] RectTransform jamPeek;         // 방해 : 흑화 도깨비가 쪽지 곳곳에서 불쑥 나타나 흔들거려요
    [SerializeField] Image[] jamScribbles;          // 방해 : 낙서가 진해졌다 옅어졌다
    [SerializeField] Image jamTint;                 // 방해 : 붉은 막이 일렁여요
    [SerializeField] GameObject judgeRoam;          // 판정 대기 : 찾아다니는 도깨비 (5챕터는 숨겨요)
    [SerializeField] GameObject judgeCurse;         // 5챕터 판정 대기 : 빛나는 쪽지 + 움찔거리는 흑화 도깨비
    [SerializeField] Image judgeGlow;
    [SerializeField] Image soulFriend;              // 5챕터 하트 0 : 결과 화면에서 쓰러지는 친구
    [SerializeField] Image soulSpirit;              //   빠져나가 도깨비에게 날아가는 영혼
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
    int hearts;             // 주인공 하트 (요청 물건을 고르거나 맞설 물건을 틀리면 -1, 0이면 게임오버)
    StageData chosen;       // 이번 단계에 고른 물건
    bool chosenCounter;     // 고른 것이 맞설 물건인지
    bool curseTaken;        // 이번 결과는 요청 물건을 골라 빼앗긴 것인지
    bool leftIsCounter;     // 고르기 화면 왼쪽이 맞설 물건인지 (좌우를 섞어요)
    bool friendLost;        // 친구 영혼을 빼앗겼는지
    bool dark;              // 숲이 어두워지고 도깨비가 흑화했는지
    bool gameOverPending;   // 결과 대사를 넘기면 게임오버
    int curseEnd;           // 0 없음, 1 게임오버, 2 해피
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
        else if (state == State.Judge && IsCurse)
        {
            PulseGlow();
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
            if (jamOverlay.activeSelf) { SpinFires(jamFires, 300f, 380f); AnimateJam(); }    // 방해 연출 : 도깨비불이 맴돌고 도깨비가 불쑥
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
        face == GameScript.Face.Friend && faceFriend != null ? faceFriend
        : face == GameScript.Face.BoySurprised ? faceBoySurprised : face == GameScript.Face.BoyHappy ? faceBoyHappy
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
        // 5챕터 : 사탕 대신 주인공 하트. 남은 만큼 또렷하게
        candyIcons[0].transform.parent.gameObject.SetActive(!IsCurse);
        heartGroup.SetActive(IsCurse && dark);
        for (int i = 0; i < heartIcons.Length; i++) heartIcons[i].color = i < hearts ? Color.white : UsedSnackColor;
    }

    string NoteText() => IsCurse ? GameScript.CurseNote
                       : chapter.number != 1 ? GameScript.ItemNote(Stage.targetName) : GameScript.NoteText(stageIndex);

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

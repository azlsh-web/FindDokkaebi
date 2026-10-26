using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// MvpSceneBuilder : "씬 자동 조립 도구" (에디터 전용, 게임 빌드에는 안 들어가요)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → MVP 씬 만들기] 를 누르면
//   1) Assets/Sprites 의 그림을 전부 Sprite 로 설정하고
//   2) 5단계 데이터 파일(Assets/Data/Stage1_Bat ~ Stage5_Friend)이 없으면 만들고
//   3) UserSettings/OpenAI.json (API 키 넣는 빈 파일)이 없으면 만들고
//   4) Assets/Scenes/Game.unity 씬에 카메라·Canvas·패널·버튼을 전부 만들어
//   5) GameManager·ToolBar·ColorMixer 의 칸들과 버튼 OnClick 까지 연결한 뒤 저장해요.
//
// ※ 손으로 Hierarchy 에서 하나씩 만드는 것과 결과는 똑같아요.
//   만들어진 씬을 열어서 오브젝트를 클릭해 보면 Inspector 에 전부 보여요.
//   게임 로직은 Assets/Scripts 에 있고, 이 파일은 "조립 도우미"일 뿐이에요.
// ※ 다시 누르면 Game 씬을 새로 덮어써요. (데이터 파일은 이미 있으면 그대로 둬요)
public static class MvpSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string DataFolder = "Assets/Data";
    const string SpriteFolder = "Assets/Sprites";
    const string FontPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";

    // 화면 색 (따뜻한 방 느낌)
    // 시작 화면 참고 이미지에서 뽑은 색 (진갈색 굵은 테두리 + 크림 + 코랄)
    static readonly Color RoomColor = new Color32(248, 241, 227, 255);
    static readonly Color OutlineColor = new Color32(74, 43, 22, 255);   // 테두리·글자 진갈색
    static readonly Color TextColor = new Color32(74, 43, 22, 255);
    static readonly Color SubTextColor = new Color32(122, 96, 74, 255);
    static readonly Color FrameColor = new Color32(240, 150, 60, 255);   // 고른 도구 표시 (주황 테두리)
    static readonly Color CreamColor = new Color32(251, 245, 231, 255);
    static readonly Color EmptySlotColor = new Color(0.85f, 0.85f, 0.85f);
    static readonly Color StagePillColor = new Color32(226, 238, 212, 255);  // HUD 가운데 단계 알약 (연두)
    static readonly Color HintColor = new Color32(160, 92, 70, 255);         // 관찰 카드 안내 글자
    static readonly Color SizeDotColor = new Color32(150, 140, 130, 255);    // 굵기 점 (고르지 않은 것)

    static TMP_FontAsset font;
    static Sprite cardSprite;       // 크림 카드 (진갈색 테두리)
    static Sprite whiteCardSprite;  // 흰 쪽지 카드
    static Sprite pillCoral;        // 주 버튼 (코랄)
    static Sprite pillCream;        // 보조 버튼 (크림)
    static Sprite tagSprite;        // HUD 이름표
    static Sprite circleSprite;     // 동그라미 (색 버튼·도구 버튼)

    [MenuItem("Tools/찾아라도깨비/MVP 씬 만들기")]
    public static void Build()
    {
        // 저장 안 한 씬이 있으면 먼저 물어보기
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        MakeAllSprites();
        CreateStageDataFiles();
        CreateOpenAISettingsFile();

        // ── 새 빈 씬 ──
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 새 씬을 열면 Unity 가 안 쓰는 에셋을 메모리에서 내려버려서,
        // 데이터·그림은 씬을 연 "뒤에" 불러와야 연결이 끊기지 않아요.
        cardSprite = LoadSprite("UI/Card.png");
        whiteCardSprite = LoadSprite("UI/WhiteCard.png");
        pillCoral = LoadSprite("UI/PillCoral.png");
        pillCream = LoadSprite("UI/PillCream.png");
        tagSprite = LoadSprite("UI/Tag.png");
        circleSprite = LoadSprite("UI/Circle.png");
        StageData[] stages =
        {
            LoadStage("Stage1_Bat"), LoadStage("Stage2_Bag"), LoadStage("Stage3_Mat"),
            LoadStage("Stage4_Gimbap"), LoadStage("Stage5_Friend"),
        };
        Sprite boyNormal = LoadSprite("Characters/Boy_Normal.png");
        Sprite boyHappy = LoadSprite("Characters/Boy_Happy.png");
        Sprite boySurprised = LoadSprite("Characters/Boy_Surprised.png");
        Sprite dokkaebi = LoadSprite("Characters/Dokkaebi.png");
        Sprite friend = LoadSprite("Stage5_Friend/Original.png");
        Sprite snack = LoadSprite("Props/Snack.png");
        Sprite incense = LoadSprite("Props/Incense.png");
        Sprite room = LoadSprite("Background/RoomBG.png");          // 시작 화면 그림에서 글자를 지운 방
        Sprite titleScreen = LoadSprite("Background/TitleScreen.png"); // 팀이 만든 시작 화면 그림
        Sprite iconEraser = LoadSprite("UI/EraserPink.png"), iconUndo = LoadSprite("UI/Undo.png"), iconTrash = LoadSprite("UI/Trash.png");
        Sprite iconPalette = LoadSprite("UI/Palette.png"), iconClose = LoadSprite("UI/Close.png");
        Sprite iconPencil = LoadSprite("UI/Pencil.png"), iconPause = LoadSprite("UI/Pause.png"), mixIcon = LoadSprite("UI/MixIcon.png");
        Sprite clipboard = LoadSprite("UI/Clipboard.png"), clip = LoadSprite("UI/Clip.png"), paletteBoard = LoadSprite("UI/PaletteBoard.png");
        Sprite flame = LoadSprite("UI/Flame.png"), sparkle = LoadSprite("UI/Sparkle.png"), tape = LoadSprite("UI/Tape.png"), bubbleSprite = LoadSprite("UI/Bubble.png");
        Sprite dokkaebiFace = LoadSprite("UI/DokkaebiFace.png"), dokkaebiPeek = LoadSprite("UI/DokkaebiPeek.png");

        // 카메라 (UI 는 Canvas 가 그리지만, 카메라가 하나는 있어야 해요)
        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RoomColor;

        // EventSystem : 클릭·드래그를 UI 에 전달하는 오브젝트 (Input System 용 모듈 사용)
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        // Canvas : 모든 UI 의 부모. 세로 화면 1080 폭 기준으로 크기 자동 조절
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        // 0 = 가로 폭 기준. 세로가 더 긴 폰(예: 1080x2340)에서도 좌우가 잘리지 않고 아래쪽 여백만 늘어나요.
        scaler.matchWidthOrHeight = 0f;
        Transform canvas = canvasGo.transform;

        // 배경 : 시작 화면과 같은 방 (글자만 지운 그림). 화면 비율이 달라도 꽉 채움
        var bg = NewImage("Background", canvas, Color.white);
        bg.sprite = room;
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // 비율 유지하며 화면을 꽉 채움
        fitter.aspectRatio = 690f / 1204f;
        bg.raycastTarget = false;
        var veil = NewImage("Veil", canvas, new Color(1f, 1f, 1f, 0.35f)); // 살짝 흐리게 덮어서 카드·글자가 잘 보이게
        Stretch(veil.rectTransform);
        veil.raycastTarget = false;

        // GameManager 오브젝트 (+ AI 판정 스크립트)
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var judge = gmGo.AddComponent<OpenAIJudge>();

        // ── HUD (게임 중에만 보임) : 위 줄 = 로고 · 단계 · 일시정지 / 아랫줄 = 간식 · 향 ──
        var hud = NewEmpty("HUD", canvas);
        var logoTag = NewTag("LogoTag", hud, false, 34, new Vector2(270, 100));
        var logoFlame = NewImage("Flame", logoTag, Color.white);
        logoFlame.sprite = flame; logoFlame.preserveAspect = true; logoFlame.raycastTarget = false;
        Anchor(logoFlame.rectTransform, 0f, 0.5f, new Vector2(22, 0), new Vector2(54, 72));
        var logoText = NewText("Logo", logoTag, "찾아라!\n도깨비", 30, Vector2.zero, Vector2.zero, bold: true);
        logoText.lineSpacing = -18;
        Anchor(logoText.rectTransform, 0f, 0.5f, new Vector2(84, 0), new Vector2(170, 92));
        var stageTag = NewImage("StageTag", hud, StagePillColor);   // 가운데 연두 알약 "1/5 · 방망이"
        stageTag.sprite = tagSprite; stageTag.type = Image.Type.Sliced; stageTag.raycastTarget = false;
        Place(stageTag.rectTransform, new Vector2(0, 34), new Vector2(330, 100));
        var stageText = NewText("StageText", stageTag.transform, "", 42, Vector2.zero, Vector2.zero, bold: true);
        Stretch(stageText.rectTransform);
        var pauseButton = NewRoundTool("PauseButton", hud, iconPause, CreamColor, 100, out GameObject pauseFrame);
        Object.DestroyImmediate(pauseFrame);
        PlaceCorner((RectTransform)pauseButton.transform.parent, true, 34, new Vector2(100, 100), 30);

        var snackTag = NewTag("SnackTag", hud, false, 160, new Vector2(270, 100));
        var snackIcon = NewImage("SnackIcon", snackTag, Color.white);
        snackIcon.sprite = snack; snackIcon.preserveAspect = true; snackIcon.raycastTarget = false;
        Anchor(snackIcon.rectTransform, 0f, 0.5f, new Vector2(18, 0), new Vector2(76, 76));
        var snackText = NewText("SnackText", snackTag, "간식 3개", 42, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, bold: true);
        Anchor(snackText.rectTransform, 0f, 0.5f, new Vector2(104, 0), new Vector2(160, 80));
        var timerGroup = NewTag("TimerGroup", hud, true, 160, new Vector2(250, 100));
        var incenseIcon = NewImage("IncenseIcon", timerGroup, Color.white);
        incenseIcon.sprite = incense; incenseIcon.preserveAspect = true; incenseIcon.raycastTarget = false;
        Anchor(incenseIcon.rectTransform, 0f, 0.5f, new Vector2(30, 0), new Vector2(30, 76));
        var timerText = NewText("TimerText", timerGroup, "60초", 50, Vector2.zero, Vector2.zero, bold: true);
        Anchor(timerText.rectTransform, 0f, 0.5f, new Vector2(74, 0), new Vector2(160, 80));
        foreach (TMP_Text t in new[] { stageText, snackText, timerText })
            t.textWrappingMode = TextWrappingModes.NoWrap;        // 이름표 글자는 한 줄로

        // ── 1. 시작 화면 : 팀이 그린 시작 화면 그림 + 그 위 "소풍 준비하러 가기" 자리에 투명 버튼 ──
        var title = NewEmpty("TitlePanel", canvas);
        var titleArt = NewImage("TitleArt", title, Color.white);
        titleArt.sprite = titleScreen;
        Stretch(titleArt.rectTransform);
        var titleFit = titleArt.gameObject.AddComponent<AspectRatioFitter>();
        titleFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        titleFit.aspectRatio = 674f / 1182f;   // 시작 화면 그림 비율
        var startImg = NewImage("StartButton", titleArt.transform, new Color(1f, 1f, 1f, 0f)); // 그림 속 버튼과 같은 자리
        var srt = startImg.rectTransform;
        srt.anchorMin = new Vector2(63f / 674f, 1f - 1072f / 1182f);   // 그림 속 "소풍 준비하러 가기" 버튼 자리
        srt.anchorMax = new Vector2(612f / 674f, 1f - 958f / 1182f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;
        var startGameButton = startImg.gameObject.AddComponent<Button>();
        var startColors = startGameButton.colors;
        startColors.pressedColor = new Color(0f, 0f, 0f, 0.15f);       // 누르면 살짝 어두워짐
        startColors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        startGameButton.colors = startColors;

        // ── 2. 단계 도입 화면 : 큰 크림 카드 하나에 단계 · 이야기 · 규칙 ──
        var intro = NewEmpty("StageIntroPanel", canvas);
        var introCard = NewCard("IntroCard", intro, new Vector2(0, 160), new Vector2(980, 1180));
        var introNumber = NewText("StageNumber", introCard, "1단계", 44, new Vector2(0, 40), new Vector2(900, 70), bold: true);
        introNumber.color = SubTextColor;
        var introTarget = NewText("TargetName", introCard, "방망이", 100, new Vector2(0, 100), new Vector2(900, 140), bold: true);
        NewSprite("Boy", introCard, boyNormal, new Vector2(-180, 250), new Vector2(270, 270));
        NewSprite("Dokkaebi", introCard, dokkaebi, new Vector2(180, 250), new Vector2(270, 270));
        var introStory = NewText("Story", introCard, "", 36, new Vector2(0, 525), new Vector2(900, 270));
        var rulesCard = NewCard("RulesCard", introCard, new Vector2(0, 805), new Vector2(900, 380), white: true);
        var introRules = NewText("Rules", rulesCard, "", 31, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(introRules.rectTransform, 30);
        var observeButton = NewButton("ObserveButton", intro, "기억할 모습 보기", new Vector2(0, 1390), new Vector2(720, 150));

        ShiftDown(intro, 110);   // HUD 두 줄 아래로

        // ── 3. 관찰 화면 : 카드 위로 빼꼼한 도깨비 + 말풍선, 테이프 붙인 쪽지, 3 · 2 · 1 ──
        var observe = NewEmpty("ObservePanel", canvas);
        var peek = NewSprite("Dokkaebi", observe, dokkaebiPeek, new Vector2(-70, 290), new Vector2(380, 325));
        peek.gameObject.AddComponent<SimpleAnim>();                         // 살랑살랑
        var bubbleImg = NewSprite("Bubble", observe, bubbleSprite, new Vector2(255, 282), new Vector2(380, 230));
        NewText("Line", bubbleImg.transform, "잘 봐 둬!\n곧 숨긴다?", 42, new Vector2(12, 26), new Vector2(330, 130), bold: true);
        var observeCard = NewCard("Card", observe, new Vector2(0, 560), new Vector2(800, 880), white: true);
        var observeImage = NewImage("TargetImage", observeCard, Color.white);
        Place(observeImage.rectTransform, new Vector2(0, 50), new Vector2(640, 560));
        observeImage.preserveAspect = true;
        var observeTitle = NewText("TargetName", observeCard, "", 100, new Vector2(0, 630), new Vector2(760, 120), bold: true);
        var observeHint = NewText("Hint", observeCard, "", 38, new Vector2(0, 760), new Vector2(760, 60), bold: true);
        observeHint.color = HintColor;
        foreach (float side in new[] { -1f, 1f })                         // 위 양쪽 모서리에 테이프
        {
            var t = NewSprite(side < 0 ? "TapeL" : "TapeR", observeCard, tape, new Vector2(side * 360, -22), new Vector2(150, 54));
            t.rectTransform.localEulerAngles = new Vector3(0, 0, -side * 35f);
        }
        var countDots = new Image[3];
        for (int i = 0; i < 3; i++)                                         // 3 · 2 · 1 (지금 숫자만 코랄)
        {
            var edge = NewImage("Count" + (3 - i), observe, OutlineColor);
            edge.sprite = circleSprite; edge.raycastTarget = false;
            Place(edge.rectTransform, new Vector2((i - 1) * 200, 1475), new Vector2(170, 170));
            countDots[i] = NewImage("Fill", edge.transform, CreamColor);
            countDots[i].sprite = circleSprite; countDots[i].raycastTarget = false;
            Stretch(countDots[i].rectTransform, 7);
            Stretch(NewText("Number", edge.transform, (3 - i).ToString(), 104, Vector2.zero, Vector2.zero, bold: true).rectTransform);
        }
        NewText("CountGuide", observe, "잠시 뒤 쪽지가 사라져요", 38, new Vector2(0, 1668), new Vector2(900, 60), bold: true);
        NewSprite("Flame", observe, flame, new Vector2(0, 1745), new Vector2(66, 88));

        // ── 4. 그리기 화면 : 클립보드 쪽지 + 물감 판 + 도구 + "다 그렸어!" ──
        var draw = NewEmpty("DrawPanel", canvas);
        var boardFrame = NewImage("BoardFrame", draw, Color.white);           // 살구색 클립보드 + 크림 종이 (9-slice)
        boardFrame.sprite = clipboard; boardFrame.type = Image.Type.Sliced; boardFrame.raycastTarget = false;
        Place(boardFrame.rectTransform, new Vector2(0, 330), new Vector2(900, 930));
        NewSprite("Clip", draw, clip, new Vector2(0, 288), new Vector2(300, 139));
        var drawTitle = NewText("Guide", draw, "", 52, new Vector2(0, 432), new Vector2(580, 76), bold: true);
        drawTitle.enableAutoSizing = true; drawTitle.fontSizeMin = 36; drawTitle.fontSizeMax = 52;   // 긴 이름도 한 줄에
        NewSprite("SparkleL", draw, sparkle, new Vector2(-330, 436), new Vector2(64, 64));
        NewSprite("SparkleR", draw, sparkle, new Vector2(330, 436), new Vector2(64, 64)).rectTransform.localScale = new Vector3(-1, 1, 1);
        var boardGo = new GameObject("Board", typeof(RectTransform), typeof(RawImage), typeof(DrawingBoard));
        boardGo.transform.SetParent(draw, false);
        Place(boardGo.GetComponent<RectTransform>(), new Vector2(0, 520), new Vector2(700, 700));
        var board = boardGo.GetComponent<DrawingBoard>();

        var toolBarGo = new GameObject("ToolBar", typeof(RectTransform), typeof(ToolBar));
        toolBarGo.transform.SetParent(draw, false);
        Stretch(toolBarGo.GetComponent<RectTransform>());
        var toolBar = toolBarGo.GetComponent<ToolBar>();
        Transform tb = toolBarGo.transform;

        // 물감 판 + 색 동그라미 (글자 없이 색만). 보이는 버튼만 가운데로 모이도록 HorizontalLayoutGroup 사용
        NewSprite("PaletteBoard", tb, paletteBoard, new Vector2(-122, 1285), new Vector2(700, 196));
        var colorRow = NewRow("ColorRow", tb, 1313, 140);
        Place(colorRow.GetComponent<RectTransform>(), new Vector2(-60, 1313), new Vector2(540, 140));
        colorRow.GetComponent<HorizontalLayoutGroup>().spacing = 12;
        string[] colorNames = { "검정", "빨강", "노랑", "파랑" };
        var colorButtons = new GameObject[4];
        var colorFrames = new GameObject[4];
        var colorButtonComps = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            colorButtonComps[i] = NewRoundTool("Color_" + colorNames[i], colorRow, null, ToolBar.Palette[i], 92, out colorFrames[i]);
            colorButtons[i] = colorButtonComps[i].transform.parent.gameObject;
        }
        var mixedSlot = NewRoundTool("MixedSlot", colorRow, null, EmptySlotColor, 92, out GameObject mixedFrame); // 섞은 색 한 칸

        // 색 섞기 : 물감 판 오른쪽 카드 버튼 (ColorRow 안에 있지만 줄 정렬에서는 빠져요)
        var mixHolder = new GameObject("MixOpen", typeof(RectTransform), typeof(LayoutElement));
        mixHolder.transform.SetParent(colorRow, false);
        mixHolder.GetComponent<LayoutElement>().ignoreLayout = true;
        Anchor(mixHolder.GetComponent<RectTransform>(), 0.5f, 0.5f, new Vector2(400, 0), new Vector2(200, 186));
        var mixCard = NewImage("Button", mixHolder.transform, Color.white);
        mixCard.sprite = cardSprite; mixCard.type = Image.Type.Sliced;
        Stretch(mixCard.rectTransform);
        var mixOpen = mixCard.gameObject.AddComponent<Button>();
        NewSprite("Icon", mixCard.transform, mixIcon, new Vector2(0, 26), new Vector2(118, 81));
        NewText("Label", mixCard.transform, "색 섞기", 38, new Vector2(0, 114), new Vector2(190, 50), bold: true);

        // 도구 줄 : 연필 · 지우개 · 되돌리기 · 모두 지우기 · 굵기 알약 (자리 고정)
        var toolRow = new GameObject("ToolRow", typeof(RectTransform)).transform;
        toolRow.SetParent(tb, false);
        Place(toolRow.GetComponent<RectTransform>(), new Vector2(0, 1512), new Vector2(1080, 150));
        Button ToolAt(string name, Sprite icon, float x, out GameObject frame)
        {
            Button b = NewRoundTool(name, toolRow, icon, CreamColor, 140, out frame);
            Anchor((RectTransform)b.transform.parent, 0.5f, 0.5f, new Vector2(x, 0), new Vector2(140, 140));
            return b;
        }
        var pencilButton = ToolAt("Pencil", iconPencil, -391, out GameObject pencilFrame);
        var eraserButton = ToolAt("Eraser", iconEraser, -223, out GameObject eraserFrame);
        var undoButton = ToolAt("Undo", iconUndo, -55, out GameObject undoFrame);
        var clearButton = ToolAt("Clear", iconTrash, 113, out GameObject clearFrame);
        Object.DestroyImmediate(undoFrame);
        Object.DestroyImmediate(clearFrame);
        var sizePill = NewImage("SizePill", toolRow, Color.white);
        sizePill.sprite = pillCream; sizePill.type = Image.Type.Sliced; sizePill.raycastTarget = false;
        Anchor(sizePill.rectTransform, 0.5f, 0.5f, new Vector2(336, 0), new Vector2(250, 112));
        var sizeFrames = new GameObject[3];
        var sizeButtons = new Button[3];
        float[] dotSizes = { 22, 34, 48 };
        for (int i = 0; i < 3; i++)                                         // 회색 점 = 고를 수 있음, 까만 점 = 지금 굵기
        {
            var holder = new GameObject("Size_" + i, typeof(RectTransform));
            holder.transform.SetParent(toolRow, false);
            Anchor(holder.GetComponent<RectTransform>(), 0.5f, 0.5f, new Vector2(336 + (i - 1) * 76, 0), new Vector2(76, 96));
            var hit = NewImage("Button", holder.transform, new Color(1f, 1f, 1f, 0f));   // 투명한 누르는 자리
            Stretch(hit.rectTransform);
            sizeButtons[i] = hit.gameObject.AddComponent<Button>();
            var dot = NewImage("Dot", holder.transform, SizeDotColor);
            var on = NewImage("Frame", holder.transform, Color.black);
            foreach (Image d in new[] { dot, on })
            {
                d.sprite = circleSprite; d.raycastTarget = false;
                Anchor(d.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2(dotSizes[i], dotSizes[i]));
            }
            sizeFrames[i] = on.gameObject;
        }

        // 다 그렸어! (간식 1개 사용) : 도깨비 얼굴 + 흰 글자 (진갈색 그림자)
        var submitImg = NewImage("SubmitButton", draw, Color.white);
        submitImg.sprite = pillCoral; submitImg.type = Image.Type.Sliced;
        Place(submitImg.rectTransform, new Vector2(0, 1692), new Vector2(840, 170));
        var submitButton = submitImg.gameObject.AddComponent<Button>();
        var face = NewSprite("Dokkaebi", submitImg.transform, dokkaebiFace, Vector2.zero, Vector2.zero);
        Anchor(face.rectTransform, 0f, 0.5f, new Vector2(70, 0), new Vector2(170, 120));
        var labelShadow = NewText("LabelShadow", submitImg.transform, "다 그렸어!", 68, Vector2.zero, Vector2.zero, bold: true);
        labelShadow.color = OutlineColor;
        Anchor(labelShadow.rectTransform, 0.5f, 0.5f, new Vector2(84, 14), new Vector2(520, 90));
        var label = NewText("Label", submitImg.transform, "다 그렸어!", 68, Vector2.zero, Vector2.zero, bold: true);
        label.color = Color.white;
        Anchor(label.rectTransform, 0.5f, 0.5f, new Vector2(80, 18), new Vector2(520, 90));
        var subLabel = NewText("SubLabel", submitImg.transform, "간식 1개 사용", 32, Vector2.zero, Vector2.zero, bold: true);
        subLabel.color = new Color32(255, 246, 240, 255);
        Anchor(subLabel.rectTransform, 0.5f, 0.5f, new Vector2(80, -48), new Vector2(520, 46));

        // 혼색 팔레트 (색 섞기를 누르면 도구 줄 위에 열림)
        var mixerGo = NewCard("MixerPanel", draw, new Vector2(0, 1170), new Vector2(980, 450));
        var mixerCard = mixerGo.GetComponent<Image>();
        var mixer = mixerGo.gameObject.AddComponent<ColorMixer>();
        Transform mx = mixerGo;
        var mixGuide = NewText("Guide", mx, "", 38, new Vector2(-40, 30), new Vector2(760, 60), bold: true);
        var closeRow = NewRow("CloseRow", mx, 18, 84);
        Place(closeRow.GetComponent<RectTransform>(), new Vector2(410, 18), new Vector2(90, 84));
        var mixClose = NewRoundTool("CloseButton", closeRow, iconClose, Color.white, 72, out GameObject closeFrame);
        Object.DestroyImmediate(closeFrame);
        var materialRow = NewRow("MaterialRow", mx, 110, 130);
        var materialFrames = new GameObject[3];
        var materialButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            materialButtons[i] = NewRoundTool("Material_" + colorNames[i + 1], materialRow, null, ToolBar.Palette[i + 1], 112, out materialFrames[i]);
        }
        var swatchEdge = NewImage("PreviewEdge", mx, OutlineColor);
        swatchEdge.sprite = circleSprite;
        Place(swatchEdge.rectTransform, new Vector2(-300, 255), new Vector2(100, 100));
        var swatch = NewImage("Preview", swatchEdge.transform, EmptySlotColor);
        swatch.sprite = circleSprite;
        Stretch(swatch.rectTransform, 5);
        var recipe = NewText("Recipe", mx, "", 42, new Vector2(70, 270), new Vector2(620, 70), TextAlignmentOptions.Left, bold: true);
        var mixApply = NewButton("ApplyButton", mx, "섞어서 칠하기", new Vector2(0, 350), new Vector2(520, 84));

        // ── 5. 판정 화면 (쪽지와 간식을 도깨비에게) ──
        var judgeP = NewEmpty("JudgePanel", canvas);
        var statusCard = NewCard("StatusCard", judgeP, new Vector2(0, 230), new Vector2(980, 190));
        var judgeStatus = NewText("Status", statusCard, "", 38, Vector2.zero, Vector2.zero, bold: true);
        Stretch(judgeStatus.rectTransform, 20);
        var judgeDokkaebi = NewSprite("Dokkaebi", judgeP, dokkaebi, new Vector2(-220, 480), new Vector2(360, 360));
        judgeDokkaebi.gameObject.AddComponent<SimpleAnim>(); // 갸웃갸웃 고민
        var noteCard = NewCard("Note", judgeP, new Vector2(230, 500), new Vector2(320, 320), white: true);
        var judgeDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        judgeDrawing.transform.SetParent(noteCard, false);
        Stretch(judgeDrawing.rectTransform, 14);
        NewSprite("Snack", judgeP, snack, new Vector2(60, 760), new Vector2(110, 110));

        // 판정은 자동이라 버튼이 없어요. AI 오류가 났을 때만 "다시 제출" 버튼이 보여요.
        var backToDraw = NewButton("BackToDrawButton", judgeP, "그림으로 돌아가 다시 제출", new Vector2(0, 960), new Vector2(760, 140), primary: false);

        ShiftDown(judgeP, 110);

        // ── 6. 결과 화면 ──
        var result = NewEmpty("ResultPanel", canvas);
        var swingDokkaebi = NewSprite("Dokkaebi", result, dokkaebi, new Vector2(-350, 230), new Vector2(250, 250));
        var swing = swingDokkaebi.gameObject.AddComponent<SimpleAnim>();  // 방망이를 휙 휘둘러 물건을 꺼냄
        swing.kind = SimpleAnim.Kind.Swing;
        swing.angle = 25f;
        swingDokkaebi.rectTransform.pivot = new Vector2(0.5f, 0f);       // 발밑을 기준으로 휘청
        swingDokkaebi.rectTransform.anchoredPosition = new Vector2(-350, -480);
        var resultItem = NewImage("ReturnedItem", result, Color.white);
        Place(resultItem.rectTransform, new Vector2(0, 200), new Vector2(380, 380));
        resultItem.preserveAspect = true;
        resultItem.gameObject.AddComponent<SimpleAnim>().kind = SimpleAnim.Kind.Pop; // 뿅 하고 나타남
        var myCard = NewCard("MyCard", result, new Vector2(350, 260), new Vector2(230, 230), white: true);
        NewText("MyLabel", myCard, "내 그림", 28, new Vector2(0, -40), new Vector2(230, 40), bold: true);
        var resultMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        resultMine.transform.SetParent(myCard, false);
        Stretch(resultMine.rectTransform, 12);
        var resultCard = NewCard("ResultCard", result, new Vector2(0, 590), new Vector2(980, 700));
        var resultTitle = NewText("ResultTitle", resultCard, "", 48, new Vector2(0, 30), new Vector2(920, 80), bold: true);
        var resultFeatures = NewText("FeatureList", resultCard, "", 33, new Vector2(0, 118), new Vector2(860, 380), TextAlignmentOptions.TopLeft);
        var boyFace = NewSprite("BoyFace", resultCard, boyNormal, new Vector2(-350, 500), new Vector2(180, 180));
        var boyMark = NewText("SurprisedMark", resultCard, "!?", 60, new Vector2(-260, 480), new Vector2(110, 90), bold: true);
        boyMark.color = new Color32(214, 80, 66, 255);   // 당황했을 때만 보이는 "!?"
        var bubble = NewCard("Bubble", resultCard, new Vector2(110, 515), new Vector2(680, 150), white: true);
        var boyLine = NewText("BoyLine", bubble, "", 34, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        Stretch(boyLine.rectTransform, 22);
        var resultSnack = NewText("SnackLeft", result, "", 36, new Vector2(0, 1310), new Vector2(1000, 60), bold: true);
        var retryButton = NewButton("RetryButton", result, "다시 그리기 (간식 1개)", new Vector2(0, 1380), new Vector2(720, 140));
        var compareButton = NewButton("CompareButton", result, "비교하기", new Vector2(0, 1535), new Vector2(720, 140), primary: false);

        ShiftDown(result, 110);

        // ── 7. 비교 화면 ──
        var compare = NewEmpty("ComparePanel", canvas);
        var mineCard = NewCard("MyCard", compare, new Vector2(-250, 290), new Vector2(460, 460), white: true);
        NewText("MyLabel", mineCard, "내 그림", 40, new Vector2(0, -62), new Vector2(460, 60), bold: true);
        var compareMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        compareMine.transform.SetParent(mineCard, false);
        Stretch(compareMine.rectTransform, 16);
        var origCard = NewCard("OriginalCard", compare, new Vector2(250, 290), new Vector2(460, 460), white: true);
        NewText("OriginalLabel", origCard, "원본", 40, new Vector2(0, -62), new Vector2(460, 60), bold: true);
        var compareOriginal = NewImage("Original", origCard, Color.white);
        Stretch(compareOriginal.rectTransform, 30);
        compareOriginal.preserveAspect = true;
        var hintCard = NewCard("HintCard", compare, new Vector2(0, 800), new Vector2(980, 440));
        var compareFeatures = NewText("FeatureHint", hintCard, "", 38, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(compareFeatures.rectTransform, 36);
        var nextButton = NewButton("NextButton", compare, "다음 단계로", new Vector2(0, 1330), new Vector2(720, 150));

        ShiftDown(compare, 110);

        // ── 8. 이야기 종료 화면 ──
        var ending = NewEmpty("EndingPanel", canvas);
        var endTitle = NewCard("TitleCard", ending, new Vector2(0, 120), new Vector2(760, 140));
        Stretch(NewText("Title", endTitle, "오늘의 이야기 끝!", 70, Vector2.zero, Vector2.zero, bold: true).rectTransform);
        NewSprite("Boy", ending, boyHappy, new Vector2(-330, 300), new Vector2(290, 290));
        NewSprite("Dokkaebi", ending, dokkaebi, new Vector2(0, 300), new Vector2(290, 290));
        NewSprite("Friend", ending, friend, new Vector2(330, 300), new Vector2(290, 290));
        var recordCard = NewCard("RecordCard", ending, new Vector2(0, 640), new Vector2(980, 620));
        NewText("Message", recordCard, "도깨비의 도움으로 친구까지 찾아\n소풍 준비를 모두 마쳤어요!", 42, new Vector2(0, 40), new Vector2(920, 130), bold: true);
        NewText("RecordTitle", recordCard, "단계별 기록", 36, new Vector2(0, 200), new Vector2(900, 56), bold: true).color = SubTextColor;
        var endingRecords = NewText("Records", recordCard, "", 34, new Vector2(0, 270), new Vector2(880, 320), TextAlignmentOptions.TopLeft);
        var restartButton = NewButton("BackToTitleButton", ending, "처음으로", new Vector2(0, 1330), new Vector2(720, 150));

        // ── 일시정지 화면 (HUD 오른쪽 위 버튼) : 어둡게 덮고 "계속하기" ──
        var pause = NewEmpty("PausePanel", canvas);
        pause.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);   // 뒤 화면을 못 누르게 덮음
        var pauseCard = NewCard("PauseCard", pause, new Vector2(0, 700), new Vector2(760, 430));
        NewText("Title", pauseCard, "잠깐 쉬는 중", 64, new Vector2(0, 60), new Vector2(700, 90), bold: true);
        NewText("Sub", pauseCard, "멈춘 동안은 향이 타지 않아요", 36, new Vector2(0, 160), new Vector2(700, 60)).color = SubTextColor;
        var resumeButton = NewButton("ResumeButton", pauseCard, "계속하기", new Vector2(0, 260), new Vector2(520, 130));

        // HUD 가 패널보다 위에 그려지도록 맨 뒤로 보내기 (Hierarchy 아래쪽일수록 앞에 그려짐). 일시정지 화면은 그보다 위
        hud.SetAsLastSibling();
        pause.SetAsLastSibling();

        // ── 버튼 OnClick 연결 (Inspector 의 On Click() 목록에 보여요) ──
        UnityEventTools.AddPersistentListener(startGameButton.onClick, gm.StartGame);
        UnityEventTools.AddPersistentListener(observeButton.onClick, gm.StartObserve);
        UnityEventTools.AddPersistentListener(submitButton.onClick, gm.Submit);
        UnityEventTools.AddPersistentListener(backToDraw.onClick, gm.BackToDraw);
        UnityEventTools.AddPersistentListener(retryButton.onClick, gm.Retry);
        UnityEventTools.AddPersistentListener(compareButton.onClick, gm.ShowCompare);
        UnityEventTools.AddPersistentListener(nextButton.onClick, gm.NextStage);
        UnityEventTools.AddPersistentListener(restartButton.onClick, gm.BackToTitle);
        UnityEventTools.AddPersistentListener(pauseButton.onClick, gm.TogglePause);
        UnityEventTools.AddPersistentListener(resumeButton.onClick, gm.TogglePause);

        for (int i = 0; i < 4; i++) UnityEventTools.AddIntPersistentListener(colorButtonComps[i].onClick, toolBar.SelectColor, i);
        UnityEventTools.AddPersistentListener(mixedSlot.onClick, toolBar.SelectMixed);
        UnityEventTools.AddPersistentListener(mixOpen.onClick, toolBar.OpenMixer);
        UnityEventTools.AddPersistentListener(pencilButton.onClick, toolBar.SelectPen);
        UnityEventTools.AddPersistentListener(eraserButton.onClick, toolBar.SelectEraser);
        for (int i = 0; i < 3; i++) UnityEventTools.AddIntPersistentListener(sizeButtons[i].onClick, toolBar.SelectSize, i);
        UnityEventTools.AddPersistentListener(undoButton.onClick, toolBar.Undo);
        UnityEventTools.AddPersistentListener(clearButton.onClick, toolBar.ClearAll);

        for (int i = 0; i < 3; i++) UnityEventTools.AddIntPersistentListener(materialButtons[i].onClick, mixer.TogglePick, i);
        UnityEventTools.AddPersistentListener(mixApply.onClick, mixer.Apply);
        UnityEventTools.AddPersistentListener(mixClose.onClick, mixer.Close);

        // ── Inspector 칸 채우기 ──
        // (private [SerializeField] 칸은 SerializedObject 로 이름을 찾아서 넣어요. 이름이 틀리면 바로 에러)
        var so = new SerializedObject(gm);
        SetArray(so, "stages", stages);
        Set(so, "board", board);
        Set(so, "toolBar", toolBar);
        Set(so, "judge", judge);
        Set(so, "hud", hud.gameObject);
        Set(so, "snackText", snackText);
        Set(so, "stageText", stageText);
        Set(so, "timerGroup", timerGroup.gameObject);
        Set(so, "timerText", timerText);
        Set(so, "pausePanel", pause.gameObject);
        Set(so, "titlePanel", title.gameObject);
        Set(so, "stageIntroPanel", intro.gameObject);
        Set(so, "observePanel", observe.gameObject);
        Set(so, "drawPanel", draw.gameObject);
        Set(so, "judgePanel", judgeP.gameObject);
        Set(so, "resultPanel", result.gameObject);
        Set(so, "comparePanel", compare.gameObject);
        Set(so, "endingPanel", ending.gameObject);
        Set(so, "introStageNumber", introNumber);
        Set(so, "introTargetName", introTarget);
        Set(so, "introStory", introStory);
        Set(so, "introRules", introRules);
        Set(so, "observeTitle", observeTitle);
        Set(so, "observeImage", observeImage);
        Set(so, "observeHintText", observeHint);
        SetArray(so, "observeCountDots", countDots);
        Set(so, "drawTitle", drawTitle);
        Set(so, "submitButton", submitButton);
        Set(so, "judgeDrawing", judgeDrawing);
        Set(so, "judgeStatusText", judgeStatus);
        Set(so, "backToDrawButton", backToDraw.gameObject);
        Set(so, "resultTitleText", resultTitle);
        Set(so, "resultItemImage", resultItem);
        Set(so, "resultMyDrawing", resultMine);
        Set(so, "resultFeatureText", resultFeatures);
        Set(so, "boyFace", boyFace);
        Set(so, "boyLineText", boyLine);
        Set(so, "resultSnackText", resultSnack);
        Set(so, "retryButton", retryButton);
        Set(so, "compareButtonLabel", compareButton.GetComponentInChildren<TMP_Text>());
        Set(so, "boyHappy", boyHappy);
        Set(so, "boySurprised", boySurprised);
        Set(so, "boySurprisedMark", boyMark.gameObject);
        Set(so, "primaryButtonSprite", pillCoral);
        Set(so, "secondaryButtonSprite", pillCream);
        Set(so, "compareMyDrawing", compareMine);
        Set(so, "compareOriginal", compareOriginal);
        Set(so, "compareFeatureText", compareFeatures);
        Set(so, "nextButtonLabel", nextButton.GetComponentInChildren<TMP_Text>());
        Set(so, "endingRecordText", endingRecords);
        so.ApplyModifiedPropertiesWithoutUndo();

        var tso = new SerializedObject(toolBar);
        Set(tso, "board", board);
        Set(tso, "mixer", mixer);
        SetArray(tso, "colorButtons", colorButtons);
        SetArray(tso, "colorFrames", colorFrames);
        Set(tso, "mixedSlotButton", mixedSlot.transform.parent.gameObject);
        Set(tso, "mixedSlotSwatch", mixedSlot.GetComponent<Image>());
        Set(tso, "mixedSlotFrame", mixedFrame);
        Set(tso, "mixOpenButton", mixOpen.transform.parent.gameObject);
        Set(tso, "pencilFrame", pencilFrame);
        Set(tso, "eraserFrame", eraserFrame);
        SetArray(tso, "sizeFrames", sizeFrames);
        Set(tso, "undoButton", undoButton);
        Set(tso, "clearButton", clearButton);
        tso.ApplyModifiedPropertiesWithoutUndo();

        // 웹 게임용 AI 판정 중계 서버 주소 (Server/judge/server_url.txt, 없으면 빈 칸 = 서버 안 씀)
        var jso = new SerializedObject(judge);
        jso.FindProperty("serverUrl").stringValue = MvpJudgeServer.ServerUrl;
        jso.ApplyModifiedPropertiesWithoutUndo();

        var mso = new SerializedObject(mixer);
        SetArray(mso, "materialFrames", materialFrames);
        Set(mso, "previewSwatch", swatch);
        Set(mso, "recipeText", recipe);
        Set(mso, "guideText", mixGuide);
        Set(mso, "applyButton", mixApply);
        mso.ApplyModifiedPropertiesWithoutUndo();

        // 처음에는 시작 화면만 보이게 (게임 시작 시 GameManager 도 같은 처리를 해요)
        foreach (Transform p in new[] { intro, observe, draw, judgeP, result, compare, ending }) p.gameObject.SetActive(false);
        mixerCard.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
        hud.gameObject.SetActive(false);

        // ── 저장 + Build Settings 의 첫 번째 씬으로 등록 ──
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets(); // Build Settings 변경도 디스크에 저장
        Debug.Log("[찾아라도깨비] MVP 씬을 만들었어요: " + ScenePath);
    }

    // ───────────── 데이터 파일 만들기 (5단계) ─────────────
    // 기획서 3장 레벨 디자인 + 부록 B 판정 기준을 그대로 옮겼어요.
    // 이미 파일이 있으면 건드리지 않아요. (Inspector 에서 고친 내용 보존)

    static void CreateStageDataFiles()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");

        CreateStage("Stage1_Bat", 1, "방망이", ColorMode.BlackOnly,
            "소풍 준비물이 모두 사라졌어요!\n쪽지에 그림을 그려 간식과 함께 두면\n도깨비가 물건을 찾아준대요.\n그런데 도깨비가 먼저 부탁해요.\n\"내 방망이부터 찾아 줘!\"",
            "검은 펜으로 모양(형태)을 그려요.\n지우개 · 선 굵기 · 되돌리기 · 모두 지우기를 쓸 수 있어요.",
            "",
            "가늘고 긴 손잡이와, 손잡이보다 굵은 머리가 연결된 도깨비방망이입니다. 머리 부분의 바깥 윤곽에는 뾰족한 돌기가 두 개 이상 있습니다. 검은색으로 형태만 표현하는 단계이며, 색과 패턴은 판정하지 않습니다.",
            ("길쭉한 손잡이", "방망이의 한쪽에 가늘고 길게 뻗은 손잡이가 표현되어 있어야 합니다. 길이가 폭보다 긴 형태로 식별되면 인정합니다. 선이 휘거나 한 줄로 그렸어도 길쭉한 연결 부분으로 식별되면 인정합니다."),
            ("손잡이보다 굵은 머리", "손잡이 한쪽 끝에 손잡이보다 폭이 넓은 머리 부분이 있어야 합니다. 둥글거나 각져도 인정합니다. 머리와 손잡이가 하나로 이어져야 하며, 머리만 따로 있거나 손잡이와 떨어져 있으면 인정하지 않습니다."),
            ("머리 바깥의 돌기 2개 이상", "머리 부분의 바깥 윤곽에서 튀어나온 뾰족한 돌기가 두 개 이상 구분되어야 합니다. 삼각형, 뿔 모양, 짧고 뾰족한 선도 인정합니다. 머리 안쪽의 무늬나 점, 한 개뿐인 돌기, 손잡이의 돌기는 인정하지 않습니다."));

        CreateStage("Stage2_Bag", 2, "가방", ColorMode.BlackOnly,
            "방망이를 찾은 도깨비가 신이 났어요.\n\"이제 네 물건을 찾아 줄게!\"\n먼저 소풍 가방을 찾아요.\n주머니 안의 줄무늬를 잘 보세요.",
            "모양 안에 무늬(패턴)를 넣어요.\n무늬의 모양과 위치도 기억해야 해요.",
            "검은 펜으로 모양 그리기",
            "정면에서 본 네모난 가방입니다. 위쪽에 둥근 손잡이가 하나 있고, 몸체 앞쪽 아래에 네모난 주머니가 있으며, 주머니 안에는 가로 줄무늬가 세 줄 있습니다. 검은색으로 형태와 무늬만 표현하는 단계이며, 색은 판정하지 않습니다.",
            ("네모난 가방 몸체", "네 변을 가진 사각형에 가까운 가방 몸체 외곽이 식별되어야 합니다. 모서리가 둥글거나 선이 삐뚤어도 인정합니다. 원형이나 삼각형처럼 명확히 다른 외곽이면 인정하지 않습니다."),
            ("위쪽 손잡이", "몸체 위쪽에 고리나 아치 모양의 손잡이가 몸체와 이어져 있어야 합니다. 손잡이가 몸체 옆이나 아래에만 있으면 인정하지 않습니다."),
            ("앞쪽 주머니", "몸체 안쪽에 몸체보다 작은 사각형 주머니 외곽이 따로 그려져 있어야 합니다. 몸체 외곽만 있고 주머니 구분선이 없으면 인정하지 않습니다."),
            ("주머니 안 가로 줄무늬", "주머니 외곽 안에 가로 방향 줄이 두 줄 이상 반복되어야 합니다. 세로줄이나 점, 주머니 밖에만 있는 줄은 인정하지 않습니다."));

        CreateStage("Stage3_Mat", 3, "돗자리", ColorMode.Primaries,
            "다음은 소풍에 깔고 앉을 돗자리예요.\n이번에는 색도 기억해야 해요!\n어느 칸이 무슨 색이었는지 잘 보세요.",
            "빨강 · 노랑 · 파랑 버튼으로 색을 골라 칠해요.\n색이 들어간 위치도 기억해야 해요.",
            "모양 그리기 · 무늬 넣기",
            "정면에서 본 직사각형 돗자리입니다. 내부는 검은색 가로선 하나와 세로선 하나로 위아래 두 줄, 좌우 두 칸의 네 영역으로 나뉩니다. 화면에서 보이는 방향을 기준으로 왼쪽 위 영역은 빨강, 오른쪽 위 영역은 노랑, 왼쪽 아래와 오른쪽 아래 영역은 파랑입니다. 빨강·노랑·파랑의 삼원색을 사용하며 혼색 과정은 판정하지 않습니다.",
            ("사각형 외곽", "네 변을 가진 직사각형에 가까운 외곽이 식별되어야 합니다. 선이 삐뚤거나 모서리가 둥글고 비율이 달라도 인정합니다. 원형이나 삼각형처럼 명확히 다른 외곽이면 인정하지 않습니다."),
            ("네 칸으로 나누는 십자선", "외곽 내부에 가로 구분선 하나와 세로 구분선 하나가 교차하여 네 영역으로 나뉘어야 합니다. 선이 중앙이 아니거나 조금 휘어도 인정합니다. 색 경계만 있고 구분선이 없거나, 한 방향 선만 있으면 인정하지 않습니다."),
            ("왼쪽 위 빨강", "왼쪽 위 영역의 주된 채색이 빨간색 계열이어야 합니다. 거친 채색과 약간의 빈틈은 인정합니다. 작은 점이나 선만 있으면 인정하지 않습니다."),
            ("오른쪽 위 노랑", "오른쪽 위 영역의 주된 채색이 노란색 계열이어야 합니다. 거친 채색과 약간의 빈틈은 인정합니다. 작은 점이나 선만 있으면 인정하지 않습니다."),
            ("아래 두 칸 파랑", "왼쪽 아래와 오른쪽 아래 영역 모두의 주된 채색이 파란색 계열이어야 합니다. 한쪽만 파랑이거나 파랑이 위쪽에만 있으면 인정하지 않습니다."));

        CreateStage("Stage4_Gimbap", 4, "김밥", ColorMode.MixTwo,
            "맛있는 김밥도 사라졌어요!\n김밥 속 재료의 색을 잘 보세요.\n없는 색은 팔레트에서 섞어서 만들어요.",
            "[색 섞기]로 두 가지 색을 섞어 새 색을 만들어요.\n예) 노랑 + 파랑 = 초록",
            "모양 · 무늬 · 빨강 노랑 파랑 칠하기",
            "단면에서 본 동그란 김밥입니다. 가장 바깥은 두꺼운 검은 김 테두리이고, 그 안쪽 넓은 부분은 흰 밥입니다. 밥 안쪽 위에 빨간 재료와 노란 재료가 나란히 있고, 그 아래에 초록 재료가 있습니다. 초록은 노랑과 파랑을 섞어 만든 혼합색입니다.",
            ("검은 김 테두리", "가장 바깥에 검은색 둥근 테두리가 둘러져 있어야 합니다. 선이 굵지 않아도 둥근 검은 외곽이 식별되면 인정합니다. 다른 색 테두리나 사각형 외곽은 인정하지 않습니다."),
            ("흰 밥", "검은 테두리 안쪽의 넓은 부분이 흰색(칠하지 않은 흰 종이 포함)으로 남아 있어야 합니다. 밥 부분 전체를 다른 색으로 칠했으면 인정하지 않습니다."),
            ("빨간 재료", "밥 안쪽에 빨간색 계열로 칠한 재료 조각이 있어야 합니다. 테두리 밖의 빨강은 인정하지 않습니다."),
            ("노란 재료", "밥 안쪽에 노란색 계열로 칠한 재료 조각이 있어야 합니다. 테두리 밖의 노랑은 인정하지 않습니다."),
            ("초록 재료", "밥 안쪽에 초록색 계열로 칠한 재료 조각이 있어야 합니다. 파랑이나 노랑만 있으면 인정하지 않습니다."));

        CreateStage("Stage5_Friend", 5, "친구", ColorMode.MixFree,
            "소풍 준비물을 다 찾았어요!\n그런데 같이 가기로 한 친구가 안 보여요.\n도깨비가 친구도 찾아 준대요.\n친구 옷의 무늬와 색을 잘 보세요.",
            "두 가지 이상 색을 자유롭게 섞어요.\n예) 빨강 + 노랑 + 파랑 = 갈색",
            "모양 · 무늬 · 삼원색 · 두 색 혼합",
            "정면에서 본 친구입니다. 머리, 몸통, 팔, 다리가 있는 사람 모습입니다. 상의는 초록색이고 가로 줄무늬가 있습니다. 하의(바지)는 갈색입니다. 초록은 두 색 혼합, 갈색은 세 색 혼합으로 만드는 색입니다.",
            ("사람 모습", "머리와 몸통이 있고 팔이나 다리가 붙은 사람 형태로 식별되어야 합니다. 막대 인간처럼 단순해도 인정합니다. 사람이 아닌 물체나 머리만 있는 그림은 인정하지 않습니다."),
            ("상의 가로 줄무늬", "상의(몸통 윗부분) 안에 가로 방향 줄이 두 줄 이상 반복되어야 합니다. 세로줄이나 점, 하의에만 있는 줄은 인정하지 않습니다."),
            ("초록 상의", "상의의 주된 채색이 초록색 계열이어야 합니다. 파랑이나 노랑이면 인정하지 않습니다."),
            ("갈색 하의", "하의(바지)의 주된 채색이 갈색 계열이어야 합니다. 빨강, 주황, 보라 등 명확히 다른 색이면 인정하지 않습니다."));

        AssetDatabase.SaveAssets();
    }

    // 자동 판정(LocalJudge)이 특징마다 할 계산 : [단계 번호 - 1][특징 번호 - 1]
    static readonly (AutoCheck check, InkColor color, Area area)[][] AutoChecks =
    {
        new[] { (AutoCheck.LongHandle, InkColor.Black, Area.TopLeft),            // 1 방망이
                (AutoCheck.ThickHead, InkColor.Black, Area.TopLeft),
                (AutoCheck.Spikes, InkColor.Black, Area.TopLeft) },
        new[] { (AutoCheck.RectOutline, InkColor.Black, Area.TopLeft),           // 2 가방
                (AutoCheck.HandleOnTop, InkColor.Black, Area.TopLeft),
                (AutoCheck.InnerPocket, InkColor.Black, Area.TopLeft),
                (AutoCheck.PocketStripes, InkColor.Black, Area.TopLeft) },
        new[] { (AutoCheck.RectOutline, InkColor.Black, Area.TopLeft),           // 3 돗자리
                (AutoCheck.CrossLines, InkColor.Black, Area.TopLeft),
                (AutoCheck.ColorAt, InkColor.Red, Area.TopLeft),
                (AutoCheck.ColorAt, InkColor.Yellow, Area.TopRight),
                (AutoCheck.ColorAt, InkColor.Blue, Area.BottomBoth) },
        new[] { (AutoCheck.RoundBlackRing, InkColor.Black, Area.TopLeft),        // 4 김밥
                (AutoCheck.WhiteInside, InkColor.Black, Area.TopLeft),
                (AutoCheck.ColorInside, InkColor.Red, Area.TopLeft),
                (AutoCheck.ColorInside, InkColor.Yellow, Area.TopLeft),
                (AutoCheck.ColorInside, InkColor.Green, Area.TopLeft) },
        new[] { (AutoCheck.PersonShape, InkColor.Black, Area.TopLeft),           // 5 친구
                (AutoCheck.ShirtStripes, InkColor.Black, Area.TopLeft),
                (AutoCheck.ShirtColor, InkColor.Green, Area.TopLeft),
                (AutoCheck.PantsColor, InkColor.Brown, Area.TopLeft) },
    };

    static void CreateStage(string file, int number, string target, ColorMode mode, string story, string newRule,
                            string learned, string description, params (string name, string criteria)[] features)
    {
        string path = $"{DataFolder}/{file}.asset";
        if (AssetDatabase.LoadAssetAtPath<StageData>(path) != null) return; // 이미 있으면 그대로

        var stage = ScriptableObject.CreateInstance<StageData>();
        stage.stageNumber = number;
        stage.targetName = target;
        stage.colorMode = mode;
        stage.introStory = story;
        stage.newRule = newRule;
        stage.learnedRules = learned;
        stage.targetDescription = description;
        stage.originalSprite = LoadSprite($"{file}/Original.png");
        stage.features = new Feature[features.Length];
        for (int i = 0; i < features.Length; i++)
        {
            stage.features[i] = new Feature
            {
                id = "F" + (i + 1),
                name = features[i].name,
                criteria = features[i].criteria,
                check = AutoChecks[number - 1][i].check,
                color = AutoChecks[number - 1][i].color,
                area = AutoChecks[number - 1][i].area,
                wrongSprite = LoadSprite($"{file}/Wrong_F{i + 1}.png"),
            };
        }
        AssetDatabase.CreateAsset(stage, path);
    }

    static StageData LoadStage(string file) => AssetDatabase.LoadAssetAtPath<StageData>($"{DataFolder}/{file}.asset");

    static Sprite LoadSprite(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{relative}");

    // Assets/Sprites 아래 PNG 를 전부 UI 에서 쓸 수 있는 Sprite 로 설정
    // ※ Sprite Mode 는 꼭 Single(한 장 통째로)!
    //   2D 프로젝트 기본값이 Multiple 이라, 떨어진 부분이 있는 그림(친구의 머리와 몸 등)을
    //   Unity 가 여러 조각으로 잘라서 머리만 보이는 문제가 있었어요.
    static void MakeAllSprites()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            Vector4 border = Vector4.zero;                       // 늘려 쓰는 그림은 모서리를 고정 (9-slice)
            if (path.EndsWith("UI/RoundRect.png")) border = new Vector4(48, 48, 48, 48);
            if (path.EndsWith("UI/Card.png")) border = new Vector4(54, 54, 54, 54);
            if (path.EndsWith("UI/WhiteCard.png")) border = new Vector4(46, 46, 46, 46);
            if (path.EndsWith("UI/PillCoral.png") || path.EndsWith("UI/PillCream.png")) border = new Vector4(72, 68, 72, 68);
            if (path.EndsWith("UI/Tag.png")) border = new Vector4(40, 38, 40, 38);
            if (path.EndsWith("UI/Clipboard.png")) border = new Vector4(70, 70, 70, 70);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
        }
    }

    // API 키를 넣을 빈 설정 파일 (UserSettings 폴더는 Git 에 안 올라가요)
    static void CreateOpenAISettingsFile()
    {
        string path = OpenAIJudge.SettingsPath;
        if (File.Exists(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "{\n  \"apiKey\": \"\",\n  \"model\": \"gpt-6-luna\"\n}\n");
        Debug.Log("[찾아라도깨비] AI 판정을 쓰려면 여기에 OpenAI API 키를 넣으세요: " + path);
    }

    // ───────────── UI 만들기 도우미들 ─────────────
    // 좌표 규칙 : 모든 UI 는 화면 "위쪽 가운데"를 기준점으로,
    //             pos.x = 가운데에서 오른쪽(+)/왼쪽(-), pos.y = 위에서 아래로 떨어진 거리

    static Transform NewEmpty(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    // 그림(Sprite) 한 장 놓기 (비율 유지, 클릭은 통과)
    static Image NewSprite(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Place(img.rectTransform, pos, size);
        return img;
    }

    static TMP_Text NewText(string name, Transform parent, string text, float size, Vector2 pos, Vector2 box,
                            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = TextColor;
        t.alignment = align;
        if (bold) t.fontStyle = FontStyles.Bold;   // 제목·버튼은 굵게 (시작 화면처럼 또렷하게)
        t.raycastTarget = false; // 글자가 클릭을 가로채지 않게
        Place(t.rectTransform, pos, box);
        return t;
    }

    // 글자 버튼 (알약 모양). primary = 코랄(이 화면의 주된 행동) / false = 크림(보조 행동). 테두리는 그림에 들어 있어요
    static Button NewButton(string name, Transform parent, string label, Vector2 pos, Vector2 box, bool primary = true)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = primary ? pillCoral : pillCream;
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, pos, box);
        var button = img.gameObject.AddComponent<Button>();

        var t = NewText("Label", img.transform, label, box.y >= 120 ? 46 : 38, Vector2.zero, Vector2.zero, bold: true);
        Stretch(t.rectTransform);
        return button;
    }

    // 크림(또는 흰) 카드 : 진갈색 굵은 테두리가 있는 둥근 네모
    static Transform NewCard(string name, Transform parent, Vector2 pos, Vector2 size, bool white = false)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = white ? whiteCardSprite : cardSprite;
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, pos, size);
        return img.transform;
    }

    // HUD 이름표 : 화면 왼쪽 위(right=false) / 오른쪽 위(right=true) 모서리에 붙는 크림 알약
    static Transform NewTag(string name, Transform parent, bool right, float top, Vector2 size)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = tagSprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        PlaceCorner(img.rectTransform, right, top, size, 30);
        return img.transform;
    }

    // 가로로 버튼을 늘어놓는 줄. 꺼진 버튼은 빼고 남은 버튼을 가운데로 모아요.
    static Transform NewRow(string name, Transform parent, float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), new Vector2(0, y), new Vector2(1060, height));
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 22;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return go.transform;
    }

    // 부모 안의 기준점(ax, ay : 0~1)에 붙여서 놓기. pos 는 기준점에서 떨어진 거리
    static void Anchor(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(ax, ay);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // 동그란 도구 버튼 : [묶음] 안에 [Frame(선택 테두리)] + [Edge(진갈색 테두리)] + [Button(동그라미) + Icon]
    static Button NewRoundTool(string name, Transform row, Sprite icon, Color color, float size, out GameObject frame)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(row, false);
        holder.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        var frameImg = NewImage("Frame", holder.transform, FrameColor);
        frameImg.sprite = circleSprite;
        Stretch(frameImg.rectTransform, -12); // 버튼보다 크게 → 고른 도구 둘레에 주황 고리
        frameImg.raycastTarget = false;
        frame = frameImg.gameObject;

        var edge = NewImage("Edge", holder.transform, OutlineColor);
        edge.sprite = circleSprite;
        Stretch(edge.rectTransform);
        edge.raycastTarget = false;

        var img = NewImage("Button", holder.transform, color);
        img.sprite = circleSprite;
        Stretch(img.rectTransform, 5);       // 진갈색 테두리 5 만큼 안쪽
        var button = img.gameObject.AddComponent<Button>();

        if (icon != null)
        {
            var iconImg = NewImage("Icon", img.transform, Color.white);
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            Stretch(iconImg.rectTransform, size * 0.14f);
        }
        return button;
    }

    // 화면 전체 패널을 dy 만큼 아래로 (안의 것들이 같이 내려가요)
    static void ShiftDown(Transform panel, float dy)
    {
        ((RectTransform)panel).offsetMax = new Vector2(0f, -dy);
    }

    // 위쪽 가운데 기준으로 위치·크기 정하기
    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        rt.sizeDelta = size;
    }

    // 왼쪽 위(right=false) 또는 오른쪽 위(right=true) 모서리에 붙이기. top = 위에서, side = 옆에서 떨어진 거리
    static void PlaceCorner(RectTransform rt, bool right, float top, Vector2 size, float side = 40f)
    {
        float x = right ? 1f : 0f;
        rt.anchorMin = rt.anchorMax = new Vector2(x, 1f);
        rt.pivot = new Vector2(x, 1f);
        rt.anchoredPosition = new Vector2(right ? -side : side, -top);
        rt.sizeDelta = size;
    }

    // 부모 크기에 꽉 채우기 (inset 만큼 안쪽으로. 음수면 바깥으로)
    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // SerializedObject 칸 채우기. 칸 이름이 틀리면 바로 알려줘요.
    static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.Exception($"{so.targetObject.GetType().Name} 에 '{field}' 칸이 없어요");
        p.objectReferenceValue = value;
    }

    static void SetArray(SerializedObject so, string field, Object[] items)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.Exception($"{so.targetObject.GetType().Name} 에 '{field}' 칸이 없어요");
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}

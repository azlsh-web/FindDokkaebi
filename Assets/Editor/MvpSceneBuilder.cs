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
    // 글꼴 : 팀 시안 글꼴 Jua (MvpFontBake 로 구운 것). 아직 안 구웠으면 Noto Sans KR
    const string FontPath = "Assets/Fonts/Jua-Regular SDF.asset";
    const string FallbackFontPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";

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
    static Sprite pillCoral;        // 주 버튼 (코랄)
    static Sprite pillCream;        // 보조 버튼 (크림)
    static Sprite circleSprite;     // 동그라미 (색 버튼·도구 버튼)

    [MenuItem("Tools/찾아라도깨비/MVP 씬 만들기")]
    public static void Build()
    {
        // 저장 안 한 씬이 있으면 먼저 물어보기
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        MakeAllSprites();
        CreateStageDataFiles();
        CreateOpenAISettingsFile();

        // ── 새 빈 씬 ──
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 새 씬을 열면 Unity 가 안 쓰는 에셋을 메모리에서 내려버려서,
        // 데이터·그림은 씬을 연 "뒤에" 불러와야 연결이 끊기지 않아요.
        // 카드·버튼은 팀 시안 그림에서 오린 것 (Assets/Sprites/UI, 오린 방법은 Tools/docs/art_pipeline/team_ui)
        cardSprite = T("Common_Card");
        pillCoral = T("Common_ButtonCoral");
        pillCream = T("Common_ButtonCream");
        circleSprite = LoadSprite("UI/Common_Circle.png");
        StageData[] stages =
        {
            LoadStage("Stage1_Bat"), LoadStage("Stage2_Bag"), LoadStage("Stage3_Mat"),
            LoadStage("Stage4_Gimbap"), LoadStage("Stage5_Friend"),
        };
        Sprite boyNormal = LoadSprite("Characters/Char_Boy_Normal.png");
        Sprite boyHappy = LoadSprite("Characters/Char_Boy_Happy.png");
        Sprite faceHappy = LoadSprite("Characters/Char_BoyFace_Happy.png");
        Sprite faceSurprised = LoadSprite("Characters/Char_BoyFace_Surprised.png");
        Sprite dokkaebi = LoadSprite("Characters/Char_Dokkaebi_Normal.png");
        Sprite dokkaebiThink = LoadSprite("Characters/Char_Dokkaebi_Think.png");    // 판정 대기 : 쪽지를 들여다봐요
        Sprite dokkaebiSwing = LoadSprite("Characters/Char_Dokkaebi_Swing.png");    // 결과 : 방망이를 휘둘러요
        Sprite friend = LoadSprite("Stage5_Friend/Friend_Original.png");
        Sprite snack = T("HUD_SnackBag");                                 // 간식 = 시안의 복주머니
        Sprite room = LoadSprite("Background/BG_Room.png");          // 시작 화면 그림에서 글자를 지운 방
        Sprite startBackground = LoadSprite("Background/BG_Start.png");  // 시작 화면의 빈 방 (물건은 위에 하나씩 올려요)
        Sprite iconTrash = LoadSprite("UI/Draw_TrashIcon.png"), iconClose = LoadSprite("UI/Mix_CloseIcon.png");

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
        // 팀 시안 크기(2248x3952)를 가로 1080 으로 줄인 1080x1899 가 기준. Expand = 이 크기가 항상 다 보이게
        //   → 더 긴 폰이면 위아래에, 더 넓은 화면이면 양옆에 방 배경이 더 보여요. 화면 내용은 가운데 "Frame" 안에.
        scaler.referenceResolution = new Vector2(1080, FrameHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Transform root = canvasGo.transform;

        // 배경 : 맨 뒤에 한 장만 깔고, 시작 화면(BG_Start)과 게임 중(BG_Room)에 GameManager 가 바꿔요. 화면 비율이 달라도 꽉 채움
        var bg = NewImage("Background", root, Color.white);
        bg.sprite = startBackground;
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // 비율 유지하며 화면을 꽉 채움
        fitter.aspectRatio = 2248f / 3952f;
        bg.raycastTarget = false;
        var frameGo = new GameObject("Frame", typeof(RectTransform));        // 시안 크기 화면 (가운데)
        frameGo.transform.SetParent(root, false);
        Anchor(frameGo.GetComponent<RectTransform>(), 0.5f, 0.5f, Vector2.zero, new Vector2(1080, FrameHeight));
        Transform canvas = frameGo.transform;

        // GameManager 오브젝트 (+ AI 판정 스크립트)
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var judge = gmGo.AddComponent<OpenAIJudge>();

        // ── HUD (게임 중에만 보임) : 위 줄 오른쪽에 남은 간식 복주머니 · 일시정지, 오른쪽 긴 향 ──
        //   좌표는 시안 그림(2248x3952) 픽셀 그대로 적고, PlaceM 이 화면(1080x1899) 크기로 바꿔요.
        var hud = NewEmpty("HUD", canvas);
        var pauseImg = TeamImage("PauseButton", hud, T("HUD_PauseButton"), 1880, 112, 2108, 344);
        pauseImg.raycastTarget = true;
        var pauseButton = pauseImg.gameObject.AddComponent<Button>();

        // 복주머니 3개 : 간식을 쓰면 왼쪽부터 흐려져요 (GameManager.UpdateSnacks)
        var snackRow = NewEmpty("SnackRow", hud);
        var snackIcons = new Image[3];
        for (int i = 0; i < snackIcons.Length; i++)
        {
            float x0 = 1192 + i * 220;
            snackIcons[i] = TeamImage("Snack_" + i, snackRow, snack, x0, 127, x0 + 182, 329);
        }

        // 향 : 줄기가 위에서부터 타고, 불꽃 · 재 · 연기가 끝을 따라 움직여요 (IncenseBurn)
        var timerGroup = NewEmpty("TimerGroup", hud);
        TeamImage("IncenseHolder", timerGroup, T("Incense_Holder"), 1950, 2190, 2210, 2542);   // 놋쇠 촛대 (향은 잔 안에 꽂혀요)
        var incenseBar = TeamImage("Incense", timerGroup, T("Incense_Stick"), 2006, 1172, 2153, 2228);
        incenseBar.type = Image.Type.Filled;
        incenseBar.fillMethod = Image.FillMethod.Vertical;
        incenseBar.fillOrigin = (int)Image.OriginVertical.Bottom;
        Image IncenseFx(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var img = NewImage(name, parent, color);
            img.sprite = sprite;
            img.raycastTarget = false;
            Anchor(img.rectTransform, 0.5f, 0f, pos, size);
            return img;
        }
        var incenseTipGo = new GameObject("IncenseFx_Tip", typeof(RectTransform));
        incenseTipGo.transform.SetParent(incenseBar.transform, false);
        var incenseTip = (RectTransform)incenseTipGo.transform;
        Anchor(incenseTip, 0.5f, 0f, Vector2.zero, new Vector2(70, 0));
        var incenseSmoke = new Image[14];
        for (int i = 0; i < incenseSmoke.Length; i++)
            incenseSmoke[i] = IncenseFx("IncenseFx_Smoke", incenseBar.transform, T("Common_SoftPuff"), Vector2.zero, new Vector2(64, 64), Color.clear);
        var incenseAsh = IncenseFx("IncenseFx_Ash", incenseTip, T("Incense_Ash"), new Vector2(0, 40), new Vector2(70, 4), Color.white);
        var incenseGlow = IncenseFx("IncenseFx_Glow", incenseTip, T("Common_SoftPuff"), new Vector2(0, -35), new Vector2(150, 150), Color.clear);
        IncenseFx("IncenseFx_Ember", incenseTip, T("Incense_Ember"), new Vector2(0, -8), new Vector2(70, 38), Color.white);
        var incenseFlame = IncenseFx("IncenseFx_Flame", incenseTip, T("Incense_Flame"), new Vector2(0, 8), new Vector2(66, 88), Color.white);
        var incenseFallingAsh = IncenseFx("IncenseFx_FallingAsh", incenseBar.transform, T("Incense_Ash"), Vector2.zero, new Vector2(34, 42), Color.white);
        incenseFallingAsh.enabled = false;
        var timerText = NewText("TimerText", timerGroup, "60", 34, Vector2.zero, Vector2.zero, bold: true);
        PlaceM(timerText.rectTransform, 1940, 2548, 2220, 2672);      // 글자 높이(약 49)가 들어가게
        timerText.textWrappingMode = TextWrappingModes.NoWrap;        // 이름표 글자는 한 줄로

        // ── 1. 시작 화면 : 배경(BG_Start) 위에 로고 · 주인공 · 도깨비 · 문구 · 버튼 · 톱니바퀴를 하나씩 올려요 ──
        //   부품을 오린 방법은 Tools/docs/art_pipeline/cut_title_parts.py (좌표도 같아요)
        var title = NewEmpty("TitlePanel", canvas);
        TeamImage("TitleBoy", title, boyNormal, 298, 1512, 1204, 2892);
        TeamImage("TitleDokkaebi", title, dokkaebi, 1012, 1962, 2078, 3002);
        TeamImage("TitleLogo", title, T("Title_Logo"), 300, 160, 1948, 1356);
        TeamImage("TitleTagline", title, T("Title_Tagline"), 304, 2920, 1948, 3160);
        TeamImage("TitleGear", title, T("Title_Gear"), 1920, 3608, 2208, 3892);
        var startImg = TeamImage("StartButton", title, T("Title_StartButton"), 188, 3192, 2060, 3608);
        startImg.raycastTarget = true;
        var startGameButton = startImg.gameObject.AddComponent<Button>();
        var startColors = startGameButton.colors;
        startColors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);  // 누르면 살짝 어두워짐
        startGameButton.colors = startColors;

        // ── 2. 단계 도입 화면 : 큰 크림 카드 하나에 단계 · 이야기 · 규칙 ──
        var intro = NewEmpty("StageIntroPanel", canvas);
        var introCard = NewCard("IntroCard", intro, new Vector2(0, 160), new Vector2(980, 1250));
        var introNumber = NewText("StageNumber", introCard, "1단계", 40, new Vector2(0, 24), new Vector2(900, 66), bold: true);
        introNumber.color = SubTextColor;
        var introTarget = NewText("TargetName", introCard, "방망이", 96, new Vector2(0, 96), new Vector2(900, 150), bold: true);
        NewSprite("Boy", introCard, boyNormal, new Vector2(-180, 250), new Vector2(270, 270));
        NewSprite("Dokkaebi", introCard, dokkaebi, new Vector2(180, 250), new Vector2(270, 270));
        var introStory = NewText("Story", introCard, "", 34, new Vector2(0, 525), new Vector2(900, 255));
        var rulesCard = NewCard("RulesCard", introCard, new Vector2(0, 780), new Vector2(900, 440));   // 바깥 카드 안쪽에 딱 들어가게
        var introRules = NewText("Rules", rulesCard, "", 29, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(introRules.rectTransform, 30);
        var observeButton = NewButton("ObserveButton", intro, "기억할 모습 보기", new Vector2(0, 1435), new Vector2(720, 130));

        ShiftDown(intro, 200);   // HUD 두 줄 아래로

        // ── 3. 관찰 화면 : 팀 시안(그림 9) — 빼꼼한 도깨비 + 말풍선 + 테이프 쪽지 + 3·2·1 ──
        var observe = NewEmpty("ObservePanel", canvas);
        var observeCard = TeamImage("Card", observe, T("Observe_Card"), 300, 590, 2010, 3040);
        float[] cardBox = { 300, 590, 2010, 3040 };
        var observeImage = NewImage("TargetImage", observeCard.transform, Color.white);
        PlaceIn(observeImage.rectTransform, cardBox, 500, 1300, 1740, 2440);
        observeImage.preserveAspect = true;
        observeImage.raycastTarget = false;
        var observeTitle = NewText("TargetName", observeCard.transform, "", 84, Vector2.zero, Vector2.zero, bold: true);
        PlaceIn(observeTitle.rectTransform, cardBox, 400, 2460, 1840, 2730);
        var observeHint = NewText("Hint", observeCard.transform, "", 40, Vector2.zero, Vector2.zero, bold: true);
        PlaceIn(observeHint.rectTransform, cardBox, 400, 2745, 1840, 2875);
        observeHint.color = HintColor;

        // 3 · 2 · 1 : 지금 숫자는 코랄 동그라미 + 흰 숫자 + 노란 반짝이, 나머지는 크림 + 검은 숫자
        string[] countBoxes = { "Count3", "Count2", "Count1" };
        float[][] countXY = { new float[] { 528, 3088, 872, 3436 }, new float[] { 952, 3092, 1300, 3440 }, new float[] { 1380, 3092, 1728, 3440 } };
        var countDots = new Image[3];
        var countGlyphs = new TMP_Text[3];
        var countSparkles = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            float[] b = countXY[i];
            countDots[i] = TeamImage(countBoxes[i], observe, i == 0 ? T("Observe_CountOn") : T("Observe_CountOff"), b[0], b[1], b[2], b[3]);
            countGlyphs[i] = NewText("Number", countDots[i].transform, (3 - i).ToString(), 124, Vector2.zero, Vector2.zero);
            countGlyphs[i].color = i == 0 ? Color.white : Color.black;
            countGlyphs[i].fontStyle = FontStyles.Bold;                     // 시안 숫자처럼 굵게
            Stretch(countGlyphs[i].rectTransform);
            countGlyphs[i].margin = new Vector4(0, 10, 0, 0);               // 숫자를 동그라미 가운데로
            // 반짝이 : 시안에서 3 왼쪽 위에 있던 자리 (동그라미 기준 같은 거리)
            var sp = TeamImage("Sparkle", observe, T("Observe_Sparkle"), b[0] - 138, b[1] + 17, b[0] - 8, b[1] + 162);
            countSparkles[i] = sp.gameObject;
        }
        TeamImage("FlameDeco", observe, T("Observe_FlameDeco"), 790, 3530, 1471, 3738);

        // ── 4. 그리기 화면 : 팀 시안(그림 10) — 클립보드 쪽지, 물감 판, 도구, "다 그렸어!" ──
        var draw = NewEmpty("DrawPanel", canvas);
        // 그림판은 틀 뒤에 있고, 틀의 종이 면은 투명해요. 그래서 하얀 면 전체에 그려지고 잉크가 둥근 모서리를 넘지 않아요.
        var boardGo = new GameObject("Board", typeof(RectTransform), typeof(RawImage), typeof(DrawingBoard));
        boardGo.transform.SetParent(draw, false);
        PlaceM(boardGo.GetComponent<RectTransform>(), 230, 643, 1845, 2550);    // 틀의 종이 면보다 테두리 밑으로 살짝 넓게
        var board = boardGo.GetComponent<DrawingBoard>();
        TeamImage("BoardFrame", draw, T("Draw_ClipboardFrame"), 140, 424, 1918, 2618);
        var drawTitle = NewText("Guide", draw, "", 54, Vector2.zero, Vector2.zero, bold: true);
        PlaceM(drawTitle.rectTransform, 470, 745, 1600, 885);
        drawTitle.enableAutoSizing = true; drawTitle.fontSizeMin = 34; drawTitle.fontSizeMax = 54;   // 긴 이름도 한 줄에
        drawTitle.raycastTarget = false;                                        // 글자 밑에도 그릴 수 있게

        var toolBarGo = new GameObject("ToolBar", typeof(RectTransform), typeof(ToolBar));
        toolBarGo.transform.SetParent(draw, false);
        Stretch(toolBarGo.GetComponent<RectTransform>());
        var toolBar = toolBarGo.GetComponent<ToolBar>();
        Transform tb = toolBarGo.transform;

        // 물감 판 + 시안의 물감 동그라미 (단계마다 쓸 수 있는 색만 보여요)
        // 아래 세 줄의 양 끝을 x 140 ~ 2108 로 맞췄어요. (물감 줄 / 도구 줄)
        TeamImage("PaletteBoard", tb, T("Draw_PaletteBoard"), 140, 2670, 1658, 3069);
        var colorRow = NewEmpty("ColorRow", tb);
        string[] colorNames = { "검정", "빨강", "노랑", "파랑" };
        string[] blobNames = { "Draw_PaintBlack", "Draw_PaintRed", "Draw_PaintYellow", "Draw_PaintBlue" };
        float[][] blobXY =                                                      // 물감 판 자리에 맞춘 좌표
        {
            new float[] { 420, 2744, 598, 2920 }, new float[] { 646, 2779, 828, 2959 },
            new float[] { 866, 2802, 1052, 2986 }, new float[] { 1084, 2786, 1271, 2970 },
        };
        var colorButtons = new GameObject[4];
        var colorFrames = new GameObject[4];
        var colorButtonComps = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            float[] b = blobXY[i];
            colorButtonComps[i] = TeamTool("Color_" + colorNames[i], colorRow, T(blobNames[i]), b[0], b[1], b[2], b[3], 16, out colorFrames[i]);
            colorButtons[i] = colorButtonComps[i].transform.parent.gameObject;
        }
        // 섞은 색 칸 : 시안의 다섯 번째 동그라미 (명암만 남긴 흰 동그라미에 섞은 색을 곱해 칠해요)
        var mixedSlot = TeamTool("MixedSlot", colorRow, T("Draw_PaintMixed"), 1306, 2750, 1492, 2934, 16, out GameObject mixedFrame);
        mixedSlot.GetComponent<Image>().color = EmptySlotColor;
        // 색 섞기 버튼 (ColorRow 안에 있어서 단계마다 같이 보이고 숨어요)
        var mixOpen = TeamTool("MixOpen", colorRow, T("Draw_MixButton"), 1692, 2672, 2108, 3068, 0, out GameObject mixOpenFrame);
        Object.DestroyImmediate(mixOpenFrame);

        // 도구 줄 : 연필 · 지우개 · 되돌리기 · 모두 지우기 · 굵기
        var toolRow = NewEmpty("ToolRow", tb);
        var pencilButton = TeamTool("Pencil", toolRow, T("Draw_PencilButton"), 140, 3116, 460, 3420, 8, out GameObject pencilFrame);
        var eraserButton = TeamTool("Eraser", toolRow, T("Draw_EraserButton"), 512, 3116, 832, 3420, 8, out GameObject eraserFrame);
        var undoButton = TeamTool("Undo", toolRow, T("Draw_UndoButton"), 884, 3116, 1204, 3420, 0, out GameObject undoFrame);
        var clearButton = TeamTool("Clear", toolRow, T("Draw_ClearButton"), 1256, 3116, 1576, 3420, 0, out GameObject clearFrame);
        Object.DestroyImmediate(undoFrame);
        Object.DestroyImmediate(clearFrame);
        var trashIcon = NewImage("Icon", clearButton.transform, Color.white);
        trashIcon.sprite = iconTrash; trashIcon.preserveAspect = true; trashIcon.raycastTarget = false;
        Stretch(trashIcon.rectTransform, 22);
        var sizePill = TeamImage("SizePill", toolRow, pillCream, 1628, 3128, 2108, 3408);
        sizePill.type = Image.Type.Sliced;
        sizePill.pixelsPerUnitMultiplier = 1f / K;
        var sizeFrames = new GameObject[3];
        var sizeButtons = new Button[3];
        float[][] dotXY = { new float[] { 1694, 3244, 1742, 3292 }, new float[] { 1814, 3232, 1886, 3304 }, new float[] { 1938, 3208, 2058, 3328 } };
        for (int i = 0; i < 3; i++)                                         // 회색 점 = 고를 수 있음, 까만 점 = 지금 굵기
        {
            float[] b = dotXY[i];
            float cx = (b[0] + b[2]) / 2, cy = (b[1] + b[3]) / 2;
            var holder = new GameObject("Size_" + i, typeof(RectTransform));
            holder.transform.SetParent(toolRow, false);
            PlaceM(holder.GetComponent<RectTransform>(), cx - 72, cy - 120, cx + 72, cy + 120);
            var hit = NewImage("Button", holder.transform, new Color(1f, 1f, 1f, 0f));   // 투명한 누르는 자리
            Stretch(hit.rectTransform);
            sizeButtons[i] = hit.gameObject.AddComponent<Button>();
            var dot = NewImage("Dot", holder.transform, SizeDotColor);
            var on = NewImage("Frame", holder.transform, Color.black);
            foreach (Image d in new[] { dot, on })
            {
                d.sprite = circleSprite; d.raycastTarget = false;
                Anchor(d.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2((b[2] - b[0]) * K, (b[3] - b[1]) * K));
            }
            sizeFrames[i] = on.gameObject;
        }

        // 다 그렸어! (간식 1개 사용) : 시안 버튼 그림 그대로
        var submitImg = TeamImage("SubmitButton", draw, T("Draw_SubmitButton"), 251, 3474, 1997, 3836);
        submitImg.raycastTarget = true;
        var submitButton = submitImg.gameObject.AddComponent<Button>();
        var submitColors = submitButton.colors;
        submitColors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.55f);   // 빈 쪽지면 흐리게
        submitButton.colors = submitColors;

        // 혼색 팔레트 (색 섞기를 누르면 물감 줄 + 도구 줄 자리를 딱 덮으며 열림)
        var mixerGo = NewCard("MixerPanel", draw, Vector2.zero, Vector2.zero);
        PlaceM((RectTransform)mixerGo, 124, 2660, 2124, 3440);
        var mixerCard = mixerGo.GetComponent<Image>();
        var mixer = mixerGo.gameObject.AddComponent<ColorMixer>();
        Transform mx = mixerGo;
        var mixGuide = NewText("Guide", mx, "", 36, new Vector2(-40, 22), new Vector2(720, 56), bold: true);
        var closeRow = NewRow("CloseRow", mx, 16, 72);
        Place(closeRow.GetComponent<RectTransform>(), new Vector2(405, 16), new Vector2(80, 72));
        var mixClose = NewRoundTool("CloseButton", closeRow, iconClose, Color.white, 64, out GameObject closeFrame);
        Object.DestroyImmediate(closeFrame);
        var materialRow = NewRow("MaterialRow", mx, 84, 112);
        Place(materialRow.GetComponent<RectTransform>(), new Vector2(0, 84), new Vector2(860, 112));
        var materialFrames = new GameObject[3];
        var materialButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            materialButtons[i] = NewRoundTool("Material_" + colorNames[i + 1], materialRow, null, ToolBar.Palette[i + 1], 96, out materialFrames[i]);
        }
        var swatchEdge = NewImage("PreviewEdge", mx, OutlineColor);
        swatchEdge.sprite = circleSprite;
        Place(swatchEdge.rectTransform, new Vector2(-300, 212), new Vector2(76, 76));
        var swatch = NewImage("Preview", swatchEdge.transform, EmptySlotColor);
        swatch.sprite = circleSprite;
        Stretch(swatch.rectTransform, 5);
        var recipe = NewText("Recipe", mx, "", 38, new Vector2(60, 214), new Vector2(600, 72), TextAlignmentOptions.Left, bold: true);
        var mixApply = NewButton("ApplyButton", mx, "섞어서 칠하기", new Vector2(0, 298), new Vector2(480, 66));

        // ── 5. 판정 화면 (쪽지와 간식을 도깨비에게) ──
        var judgeP = NewEmpty("JudgePanel", canvas);
        var statusCard = NewCard("StatusCard", judgeP, new Vector2(0, 230), new Vector2(980, 190));
        var judgeStatus = NewText("Status", statusCard, "", 38, Vector2.zero, Vector2.zero, bold: true);
        Stretch(judgeStatus.rectTransform, 20);
        var judgeDokkaebi = NewSprite("Dokkaebi", judgeP, dokkaebiThink, new Vector2(-220, 480), new Vector2(360, 360));
        judgeDokkaebi.gameObject.AddComponent<SimpleAnim>(); // 갸웃갸웃 고민
        var noteCard = NewCard("Note", judgeP, new Vector2(230, 500), new Vector2(320, 320));
        var judgeDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        judgeDrawing.transform.SetParent(noteCard, false);
        FitDrawing(judgeDrawing.rectTransform, 320, 14);
        NewSprite("Snack", judgeP, snack, new Vector2(20, 745), new Vector2(90, 90));   // 도깨비와 쪽지 사이

        // 판정은 자동이라 버튼이 없어요. AI 오류가 났을 때만 "다시 제출" 버튼이 보여요.
        var backToDraw = NewButton("BackToDrawButton", judgeP, "그림으로 돌아가 다시 제출", new Vector2(0, 960), new Vector2(760, 140), primary: false);

        ShiftDown(judgeP, 200);

        // ── 6. 결과 화면 ──
        var result = NewEmpty("ResultPanel", canvas);
        var swingDokkaebi = NewSprite("Dokkaebi", result, dokkaebiSwing, new Vector2(-350, 230), new Vector2(250, 250));
        var swing = swingDokkaebi.gameObject.AddComponent<SimpleAnim>();  // 방망이를 휙 휘둘러 물건을 꺼냄
        swing.kind = SimpleAnim.Kind.Swing;
        swing.angle = 25f;
        swingDokkaebi.rectTransform.pivot = new Vector2(0.5f, 0f);       // 발밑을 기준으로 휘청
        swingDokkaebi.rectTransform.anchoredPosition = new Vector2(-350, -480);
        var resultItem = NewImage("ReturnedItem", result, Color.white);
        Place(resultItem.rectTransform, new Vector2(0, 205), new Vector2(350, 350));   // 뿅 하고 커져도 카드에 가리지 않게
        resultItem.preserveAspect = true;
        resultItem.gameObject.AddComponent<SimpleAnim>().kind = SimpleAnim.Kind.Pop; // 뿅 하고 나타남
        var myCard = NewCard("MyCard", result, new Vector2(350, 260), new Vector2(230, 230));
        NewText("MyLabel", myCard, "내 그림", 28, new Vector2(0, -40), new Vector2(230, 40), bold: true);
        var resultMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        resultMine.transform.SetParent(myCard, false);
        FitDrawing(resultMine.rectTransform, 230, 12);
        var resultCard = NewCard("ResultCard", result, new Vector2(0, 590), new Vector2(980, 700));
        var resultTitle = NewText("ResultTitle", resultCard, "", 48, new Vector2(0, 30), new Vector2(920, 80), bold: true);
        var resultFeatures = NewText("FeatureList", resultCard, "", 33, new Vector2(0, 118), new Vector2(860, 380), TextAlignmentOptions.TopLeft);
        var boyFace = NewSprite("BoyFace", resultCard, faceSurprised, new Vector2(-350, 500), new Vector2(180, 180));
        var boyMark = NewText("SurprisedMark", resultCard, "!?", 56, new Vector2(-350, 410), new Vector2(110, 88), bold: true);   // 소년 바로 위
        boyMark.color = new Color32(214, 80, 66, 255);   // 당황했을 때만 보이는 "!?"
        var bubble = NewCard("Bubble", resultCard, new Vector2(110, 515), new Vector2(680, 150));
        var boyLine = NewText("BoyLine", bubble, "", 34, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        Stretch(boyLine.rectTransform, 22);
        var resultSnack = NewText("SnackLeft", result, "", 36, new Vector2(0, 1310), new Vector2(1000, 60), bold: true);
        var retryButton = NewButton("RetryButton", result, "다시 그리기 (간식 1개)", new Vector2(0, 1380), new Vector2(720, 140));
        var compareButton = NewButton("CompareButton", result, "비교하기", new Vector2(0, 1535), new Vector2(720, 140), primary: false);

        ShiftDown(result, 200);

        // ── 7. 비교 화면 ──
        var compare = NewEmpty("ComparePanel", canvas);
        var mineCard = NewCard("MyCard", compare, new Vector2(-250, 290), new Vector2(460, 460));
        NewText("MyLabel", mineCard, "내 그림", 40, new Vector2(0, -62), new Vector2(460, 60), bold: true);
        var compareMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        compareMine.transform.SetParent(mineCard, false);
        FitDrawing(compareMine.rectTransform, 460, 16);
        var origCard = NewCard("OriginalCard", compare, new Vector2(250, 290), new Vector2(460, 460));
        NewText("OriginalLabel", origCard, "원본", 40, new Vector2(0, -62), new Vector2(460, 60), bold: true);
        var compareOriginal = NewImage("Original", origCard, Color.white);
        Stretch(compareOriginal.rectTransform, 30);
        compareOriginal.preserveAspect = true;
        var hintCard = NewCard("HintCard", compare, new Vector2(0, 800), new Vector2(980, 440));
        var compareFeatures = NewText("FeatureHint", hintCard, "", 38, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(compareFeatures.rectTransform, 36);
        var nextButton = NewButton("NextButton", compare, "다음 단계로", new Vector2(0, 1330), new Vector2(720, 150));

        ShiftDown(compare, 200);

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
        var pause = NewEmpty("PausePanel", root);
        pause.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);   // 뒤 화면을 못 누르게 덮음
        var pauseCard = NewCard("PauseCard", pause, new Vector2(0, 700), new Vector2(760, 430));
        NewText("Title", pauseCard, "잠깐 쉬는 중", 64, new Vector2(0, 56), new Vector2(700, 100), bold: true);
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
        SetArray(so, "snackIcons", snackIcons);
        Set(so, "timerGroup", timerGroup.gameObject);
        Set(so, "timerText", timerText);
        var burn = timerGroup.gameObject.AddComponent<IncenseBurn>();
        var burnSo = new SerializedObject(burn);
        Set(burnSo, "stick", incenseBar);
        Set(burnSo, "tip", incenseTip);
        Set(burnSo, "flame", incenseFlame.rectTransform);
        Set(burnSo, "glow", incenseGlow);
        Set(burnSo, "ash", incenseAsh.rectTransform);
        Set(burnSo, "fallingAsh", incenseFallingAsh);
        SetArray(burnSo, "smoke", incenseSmoke);
        Set(burnSo, "secondsText", timerText);
        burnSo.ApplyModifiedPropertiesWithoutUndo();
        Set(so, "incense", burn);
        Set(so, "pausePanel", pause.gameObject);
        Set(so, "backgroundImage", bg);
        Set(so, "startBackground", startBackground);
        Set(so, "gameBackground", room);
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
        SetArray(so, "observeCountGlyphs", countGlyphs);
        SetArray(so, "observeCountSparkles", countSparkles);
        Set(so, "countOnSprite", T("Observe_CountOn"));
        Set(so, "countOffSprite", T("Observe_CountOff"));
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
        Set(so, "boyHappy", faceHappy);
        Set(so, "boySurprised", faceSurprised);
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
            "가늘고 긴 손잡이와, 손잡이보다 굵은 머리가 이어진 도깨비방망이입니다. 머리에는 짧게 튀어나온 돌기가 여러 개 있고, 손잡이에는 리본이 묶여 있습니다. 검은색으로 형태만 표현하는 단계이며, 색은 판정하지 않습니다.",
            ("긴 손잡이 + 굵은 머리 + 돌기", "가늘고 긴 손잡이 한쪽 끝에 손잡이보다 굵은 머리가 이어져 있고, 머리 바깥 윤곽에 튀어나온 돌기가 두 개 이상 구분되어야 합니다. 뾰족한 돌기, 둥근 혹, 구름 모양을 인정합니다. 매끈한 원이나 타원 머리, 머리 안쪽의 무늬, 손잡이의 리본은 돌기로 인정하지 않습니다."));

        CreateStage("Stage2_Bag", 2, "가방", ColorMode.BlackOnly,
            "방망이를 찾은 도깨비가 신이 났어요.\n\"이제 네 물건을 찾아 줄게!\"\n먼저 소풍 가방을 찾아요.\n주머니 안의 줄무늬를 잘 보세요.",
            "모양 안에 무늬(패턴)를 넣어요.\n무늬의 모양과 위치도 기억해야 해요.",
            "검은 펜으로 모양 그리기",
            "정면에서 본 배낭입니다. 몸체는 모서리가 둥근 네모이고, 위쪽에 고리 모양 손잡이가 하나 있습니다. 몸체 앞쪽 아래에 네모난 주머니가 있으며, 주머니 안에는 가로 줄무늬가 세 줄 있습니다. 검은색으로 형태와 무늬만 표현하는 단계이며, 색은 판정하지 않습니다.",
            ("네모 몸체 + 위쪽 손잡이 + 앞주머니", "네 변을 가진 사각형에 가까운 몸체 외곽, 몸체 위쪽에 이어진 고리나 아치 모양 손잡이, 몸체 안쪽의 작은 사각형 주머니가 모두 식별되어야 합니다. 모서리가 둥글거나 선이 삐뚤어도 인정합니다. 원형이나 삼각형 몸체, 옆이나 아래에만 있는 손잡이는 인정하지 않습니다."),
            ("주머니 안 가로 줄무늬", "주머니로 보이는 영역 안에 가로 방향 줄이 두 줄 이상 반복되어야 합니다. 세로줄이나 점, 주머니 밖에만 있는 줄은 인정하지 않습니다."));

        CreateStage("Stage3_Mat", 3, "돗자리", ColorMode.Primaries,
            "다음은 소풍에 깔고 앉을 돗자리예요.\n이번에는 색도 기억해야 해요!\n어떤 색 띠가 어떤 순서였는지 잘 보세요.",
            "빨강 · 노랑 · 파랑 버튼으로 색을 골라 칠해요.\n색이 들어간 위치도 기억해야 해요.",
            "모양 그리기 · 무늬 넣기",
            "네모난 돗자리입니다. 안쪽은 나란한 색 띠 네 줄로 나뉘어 있고, 화면에서 보이는 위에서부터 파랑, 빨강, 노랑, 파랑 순서입니다. 돗자리는 조금 비스듬히 놓여 있습니다. 빨강·노랑·파랑의 삼원색을 사용하며 혼색 과정은 판정하지 않습니다.",
            ("사각형 외곽", "네 변을 가진 사각형에 가까운 외곽이 식별되어야 합니다. 비스듬하거나 선이 삐뚤고 비율이 달라도 인정합니다. 원형이나 삼각형처럼 명확히 다른 외곽이면 인정하지 않습니다."),
            ("띠 네 줄", "외곽 안이 나란한 띠 네 줄로 나뉘어 있어야 합니다. 띠는 가로나 비스듬한 방향 모두 인정합니다. 한 가지 색으로만 칠했거나 띠가 네 줄이 아니면 인정하지 않습니다."),
            ("띠 색 순서", "맨 위 띠와 맨 아래 띠의 주된 채색이 파란색 계열이고, 그 사이에 위에서부터 빨간색 계열과 노란색 계열이 차례로 나타나야 합니다. 띠의 개수는 이 항목에서 보지 않습니다. 거친 채색과 약간의 빈틈은 인정합니다."));

        CreateStage("Stage4_Gimbap", 4, "김밥", ColorMode.MixTwo,
            "맛있는 김밥도 사라졌어요!\n김밥 속 재료의 색을 잘 보세요.\n없는 색은 팔레트에서 섞어서 만들어요.",
            "[색 섞기]로 두 가지 색을 섞어 새 색을 만들어요.\n예) 노랑 + 파랑 = 초록",
            "모양 · 무늬 · 빨강 노랑 파랑 칠하기",
            "단면에서 본 동그란 김밥입니다. 가장 바깥은 두꺼운 검은 김 테두리이고, 그 안쪽 넓은 부분은 흰 밥입니다. 밥 안쪽 위에 붉은 재료와 노란 단무지가 나란히 있고, 그 아래에 초록 시금치가 있습니다. 초록은 노랑과 파랑을 섞어 만든 혼합색입니다.",
            ("검은 김 테두리", "가장 바깥에 검은색 둥근 테두리가 둘러져 있어야 합니다. 선이 굵지 않아도 둥근 검은 외곽이 식별되면 인정합니다. 다른 색 테두리나 사각형 외곽은 인정하지 않습니다."),
            ("재료 배치", "테두리 안쪽에 재료 조각이 위쪽에 두 개 나란히, 그 아래쪽에 한 개 있어야 합니다. 재료의 색은 이 항목에서 보지 않습니다."),
            ("노란 단무지", "밥 안쪽에 노란색 계열로 칠한 재료 조각이 있어야 합니다. 테두리 밖의 노랑은 인정하지 않습니다."),
            ("초록 시금치", "밥 안쪽에 초록색 계열로 칠한 재료 조각이 있어야 합니다. 파랑이나 노랑만 있으면 인정하지 않습니다."));

        CreateStage("Stage5_Friend", 5, "친구", ColorMode.MixFree,
            "소풍 준비물을 다 찾았어요!\n그런데 같이 가기로 한 친구가 안 보여요.\n도깨비가 친구도 찾아 준대요.\n친구의 머리와 옷 색을 잘 보세요.",
            "두 가지 이상 색을 자유롭게 섞어요.\n예) 빨강 + 노랑 + 파랑 = 갈색",
            "모양 · 무늬 · 삼원색 · 두 색 혼합",
            "정면에서 본 친구입니다. 양 갈래로 묶은 갈색 머리, 살구색(주황 계열) 반팔 상의, 앞주머니에 천 조각을 덧댄 파란 멜빵 반바지를 입고 노란 신발을 신었습니다. 갈색은 세 색 혼합, 주황은 두 색 혼합으로 만드는 색입니다.",
            ("양 갈래 머리", "머리 양옆으로 묶은 머리 두 갈래가 구분되어야 합니다. 단발이나 한 갈래 머리는 인정하지 않습니다."),
            ("주머니 천 조각", "하의 앞쪽 주머니 자리에 덧댄 작은 천 조각이 표현되어 있어야 합니다. 천 조각이 없는 주머니는 인정하지 않습니다."),
            ("파란 바지", "하의(멜빵바지)의 주된 채색이 파란색 계열이어야 합니다. 빨강이나 보라처럼 명확히 다른 색이면 인정하지 않습니다."),
            ("주황 상의", "상의(몸통 윗부분)에 주황·살구색 계열이 칠해져 있어야 합니다. 빨강이나 초록처럼 명확히 다른 색이면 인정하지 않습니다."),
            ("갈색 머리", "머리카락 부분의 주된 채색이 갈색 계열이어야 합니다. 검정, 주황, 노랑처럼 명확히 다른 색이면 인정하지 않습니다."));

        AssetDatabase.SaveAssets();
    }

    // 자동 판정(LocalJudge)이 특징마다 할 계산 : [단계 번호 - 1][특징 번호 - 1]
    static readonly (AutoCheck check, InkColor color, Area area)[][] AutoChecks =
    {
        new[] { (AutoCheck.BatShape, InkColor.Black, Area.TopLeft) },            // 1 방망이 : 형태
        new[] { (AutoCheck.BagShape, InkColor.Black, Area.TopLeft),              // 2 가방 : 형태 · 패턴
                (AutoCheck.PocketStripes, InkColor.Black, Area.TopLeft) },
        new[] { (AutoCheck.RectOutline, InkColor.Black, Area.TopLeft),           // 3 돗자리 : 형태 · 패턴 · 삼원색
                (AutoCheck.ColorBands, InkColor.Black, Area.TopLeft),
                (AutoCheck.BandOrder, InkColor.Black, Area.TopLeft) },
        new[] { (AutoCheck.RoundBlackRing, InkColor.Black, Area.TopLeft),        // 4 김밥 : 형태 · 패턴 · 삼원색 · 2색 혼합
                (AutoCheck.IngredientLayout, InkColor.Black, Area.TopLeft),
                (AutoCheck.ColorInside, InkColor.Yellow, Area.TopLeft),
                (AutoCheck.ColorInside, InkColor.Green, Area.TopLeft) },
        new[] { (AutoCheck.PigTails, InkColor.Black, Area.TopLeft),              // 5 친구 : 형태 · 패턴 · 삼원색 · 2색 혼합 · 3색 혼합
                (AutoCheck.PocketPatch, InkColor.Black, Area.TopLeft),
                (AutoCheck.ColorInZone, InkColor.Blue, Area.Legs),
                (AutoCheck.ColorInZone, InkColor.Orange, Area.Body),
                (AutoCheck.ColorInZone, InkColor.Brown, Area.Head) },
    };

    static void CreateStage(string file, int number, string target, ColorMode mode, string story, string newRule,
                            string learned, string description, params (string name, string criteria)[] features)
    {
        string path = $"{DataFolder}/{file}.asset";
        var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);       // 있으면 내용을 새로 덮어써요
        bool isNew = stage == null;
        if (isNew) stage = ScriptableObject.CreateInstance<StageData>();
        stage.stageNumber = number;
        stage.targetName = target;
        stage.colorMode = mode;
        stage.introStory = story;
        stage.newRule = newRule;
        stage.learnedRules = learned;
        stage.targetDescription = description;
        stage.originalSprite = LoadSprite($"{file}/{file.Substring(file.IndexOf('_') + 1)}_Original.png");
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
            };
        }
        stage.wrongSprites = LoadWrongSprites(file, features.Length);
        if (isNew) AssetDatabase.CreateAsset(stage, path);
        else EditorUtility.SetDirty(stage);
    }

    // 판정 조합마다 돌려줄 그림 : Bag_Wrong_XO.png (O 맞음, X 틀림)
    static Sprite[] LoadWrongSprites(string file, int featureCount)
    {
        string key = file.Substring(file.IndexOf('_') + 1);
        var sprites = new Sprite[1 << featureCount];
        for (int mask = 1; mask < sprites.Length; mask++)
        {
            var code = new char[featureCount];
            for (int i = 0; i < featureCount; i++) code[i] = (mask & (1 << (featureCount - 1 - i))) != 0 ? 'X' : 'O';
            sprites[mask] = LoadSprite($"{file}/{key}_Wrong_{new string(code)}.png");
        }
        return sprites;
    }

    /// <summary>새로 넣은 조합 그림을 단계 데이터에 연결 (씬은 그대로). Temp/mvp_wrongart.flag 로도 실행돼요.</summary>
    public static void ConnectWrongArt()
    {
        AssetDatabase.Refresh();
        MakeAllSprites();
        int connected = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:StageData", new[] { DataFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            stage.wrongSprites = LoadWrongSprites(System.IO.Path.GetFileNameWithoutExtension(path), stage.features.Length);
            foreach (Sprite s in stage.wrongSprites)
                if (s != null) connected++;
            EditorUtility.SetDirty(stage);
        }
        AssetDatabase.SaveAssets();
        UnityEngine.Debug.Log($"[엉뚱한 물건] 조합 그림 {connected}장을 연결했어요.");
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
            if (path.EndsWith("UI/Common_Card.png")) border = new Vector4(80, 80, 80, 80);
            if (path.EndsWith("UI/Common_ButtonCoral.png")) border = new Vector4(182, 0, 182, 0);
            if (path.EndsWith("UI/Common_ButtonCream.png")) border = new Vector4(140, 0, 140, 0);

            // 외곽선이 또렷하게 : 시안 UI · 시작 화면은 압축하지 않고(압축하면 가는 진갈색 선이 뭉개져요),
            //   모든 그림에 밉맵(미리 줄여 둔 그림)을 켜서 작은 화면에서 크게 줄어들어도 선이 끊기지 않게
            bool crisp = path.Contains("/UI/") || path.EndsWith("Background/BG_Start.png");
            var compression = crisp ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spriteBorder != border || importer.textureCompression != compression
                || !importer.mipmapEnabled || importer.maxTextureSize < 4096)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.textureCompression = compression;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 4096;                  // 기본 2048 이면 긴 그림이 줄어들어 흐려져요
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
        // Jua 는 원래 굵은 글꼴이라 따로 굵게 하지 않아요 (Noto 일 때만 제목·버튼을 굵게)
        if (bold && !font.name.StartsWith("Jua")) t.fontStyle = FontStyles.Bold;
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
        img.pixelsPerUnitMultiplier = img.sprite.rect.height / box.y;   // 알약 양 끝 동그라미가 버튼 높이에 딱 맞게
        Place(img.rectTransform, pos, box);
        var button = img.gameObject.AddComponent<Button>();

        var t = NewText("Label", img.transform, label, box.y >= 120 ? 46 : 38, Vector2.zero, Vector2.zero, bold: true);
        Stretch(t.rectTransform);
        return button;
    }

    // 크림(또는 흰) 카드 : 진갈색 굵은 테두리가 있는 둥근 네모
    static Transform NewCard(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / K;                           // 모서리 둥글기를 시안과 같은 크기로 (카드 그림은 시안 크기)
        Place(img.rectTransform, pos, size);
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

    // ── 팀 시안 좌표 도우미 ──
    // 시안 그림(2248x3952)의 픽셀 좌표 → 화면(1080 폭) 좌표. K = 1080 / 2248
    const float K = 1080f / 2248f;
    const float FrameHeight = 3952f * K;      // ≈ 1899

    static Sprite T(string name) => LoadSprite("UI/" + name + ".png");

    // 시안의 상자 (x0,y0)-(x1,y1) 자리에 놓기 (부모 = 화면 크기 패널)
    static void PlaceM(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        Place(rt, new Vector2((x0 + x1) * 0.5f * K - 540f, y0 * K), new Vector2((x1 - x0) * K, (y1 - y0) * K));
    }

    // 시안 상자 parentBox 안의 상자 자리에 놓기 (부모 = parentBox 자리에 놓인 그림)
    static void PlaceIn(RectTransform rt, float[] parentBox, float x0, float y0, float x1, float y1)
    {
        float pcx = (parentBox[0] + parentBox[2]) * 0.5f;
        Place(rt, new Vector2(((x0 + x1) * 0.5f - pcx) * K, (y0 - parentBox[1]) * K), new Vector2((x1 - x0) * K, (y1 - y0) * K));
    }

    // 시안에서 오린 그림 한 장을 원래 자리에 (클릭은 통과)
    static Image TeamImage(string name, Transform parent, Sprite sprite, float x0, float y0, float x1, float y1)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.raycastTarget = false;
        PlaceM(img.rectTransform, x0, y0, x1, y1);
        return img;
    }

    // 시안 그림 버튼 : [묶음] 안에 [Frame(고른 표시 코랄 고리)] + [Button(시안 그림)]
    static Button TeamTool(string name, Transform parent, Sprite sprite, float x0, float y0, float x1, float y1, float ringPad, out GameObject frame)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        PlaceM(holder.GetComponent<RectTransform>(), x0, y0, x1, y1);
        var ring = NewImage("Frame", holder.transform, Color.white);
        ring.sprite = T("Common_SelectRing");
        ring.raycastTarget = false;
        Stretch(ring.rectTransform, -ringPad * K);
        frame = ring.gameObject;
        var img = NewImage("Button", holder.transform, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        Stretch(img.rectTransform);
        return img.gameObject.AddComponent<Button>();
    }

    // 세로로 긴 내 그림을 정사각형 카드 가운데에 비율 그대로 놓기
    static void FitDrawing(RectTransform rt, float cardSize, float padding)
    {
        float height = cardSize - padding * 2f;
        float width = height * DrawingBoard.TextureWidth / DrawingBoard.TextureHeight;
        Anchor(rt, 0.5f, 0.5f, Vector2.zero, new Vector2(width, height));
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

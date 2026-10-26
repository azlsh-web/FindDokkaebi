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
    static readonly Color RoomColor = new Color(0.95f, 0.89f, 0.76f);
    static readonly Color ButtonColor = new Color(0.55f, 0.36f, 0.22f);
    static readonly Color TextColor = new Color(0.20f, 0.14f, 0.10f);
    static readonly Color CardColor = new Color(1f, 0.98f, 0.93f, 0.92f);
    static readonly Color FrameColor = new Color(1f, 0.62f, 0.05f);

    static TMP_FontAsset font;
    static Sprite uiSprite;

    [MenuItem("Tools/찾아라도깨비/MVP 씬 만들기")]
    public static void Build()
    {
        // 저장 안 한 씬이 있으면 먼저 물어보기
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        MakeAllSprites();
        CreateStageDataFiles();
        CreateOpenAISettingsFile();

        // ── 새 빈 씬 ──
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 새 씬을 열면 Unity 가 안 쓰는 에셋을 메모리에서 내려버려서,
        // 데이터·그림은 씬을 연 "뒤에" 불러와야 연결이 끊기지 않아요.
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
        Sprite room = LoadSprite("Background/Room.png");

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

        // 배경 : 창호가 보이는 방 + 나무 책상 (흐리게 깔아서 글자가 잘 보이게)
        var bg = NewImage("Background", canvas, new Color(1f, 1f, 1f, 0.55f));
        bg.sprite = room;
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // 비율 유지하며 화면을 꽉 채움
        fitter.aspectRatio = 1080f / 2340f;
        bg.raycastTarget = false;

        // GameManager 오브젝트 (+ AI 판정 스크립트)
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var judge = gmGo.AddComponent<OpenAIJudge>();

        // ── HUD (게임 중에만 보임) ──
        var hud = NewEmpty("HUD", canvas);
        var snackIcon = NewSprite("SnackIcon", hud, snack, Vector2.zero, Vector2.zero);
        PlaceCorner(snackIcon.rectTransform, false, 30, new Vector2(80, 80), 30);
        var snackText = NewText("SnackText", hud, "간식 3개", 46, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        PlaceCorner(snackText.rectTransform, false, 40, new Vector2(400, 70), 120);
        var stageText = NewText("StageText", hud, "", 46, Vector2.zero, Vector2.zero, TextAlignmentOptions.Right);
        PlaceCorner(stageText.rectTransform, true, 40, new Vector2(500, 70));
        var timerGroup = NewEmpty("TimerGroup", hud);
        var timerText = NewText("TimerText", timerGroup, "향 60초", 40, Vector2.zero, Vector2.zero, TextAlignmentOptions.Right);
        PlaceCorner(timerText.rectTransform, true, 110, new Vector2(400, 60));
        var incenseIcon = NewSprite("IncenseIcon", timerGroup, incense, Vector2.zero, Vector2.zero);
        PlaceCorner(incenseIcon.rectTransform, true, 112, new Vector2(30, 76), 360);
        var barBack = NewImage("IncenseBarBack", timerGroup, new Color(0.8f, 0.72f, 0.6f));
        PlaceCorner(barBack.rectTransform, true, 175, new Vector2(320, 16));
        var incenseBar = NewImage("IncenseBar", barBack.transform, new Color(0.47f, 0.27f, 0.16f));
        Stretch(incenseBar.rectTransform);
        incenseBar.sprite = uiSprite;
        incenseBar.type = Image.Type.Filled;
        incenseBar.fillMethod = Image.FillMethod.Horizontal;
        incenseBar.fillOrigin = (int)Image.OriginHorizontal.Right; // 오른쪽부터 줄어들게 → 왼쪽 끝이 남음

        // ── 1. 시작 화면 ──
        var title = NewEmpty("TitlePanel", canvas);
        NewText("Title", title, "찾아라! 도깨비", 110, new Vector2(0, 300), new Vector2(1000, 170));
        NewText("Subtitle", title, "관찰 · 기억 · 드로잉 퍼즐", 44, new Vector2(0, 470), new Vector2(1000, 70));
        NewSprite("Boy", title, boyNormal, new Vector2(-210, 600), new Vector2(380, 380));
        NewSprite("Dokkaebi", title, dokkaebi, new Vector2(210, 600), new Vector2(380, 380)).gameObject.AddComponent<SimpleAnim>();
        NewText("Hint", title, "잠깐 본 물건을 기억해서 그리면\n도깨비가 찾아 줘요", 42, new Vector2(0, 1060), new Vector2(1000, 150));
        var startGameButton = NewButton("StartButton", title, "소풍 준비하러 가기", new Vector2(0, 1330), new Vector2(720, 160));

        // ── 2. 단계 도입 화면 ──
        var intro = NewEmpty("StageIntroPanel", canvas);
        var introNumber = NewText("StageNumber", intro, "1단계", 56, new Vector2(0, 220), new Vector2(1000, 90));
        var introTarget = NewText("TargetName", intro, "방망이", 96, new Vector2(0, 300), new Vector2(1000, 140));
        NewSprite("Boy", intro, boyNormal, new Vector2(-200, 450), new Vector2(280, 280));
        NewSprite("Dokkaebi", intro, dokkaebi, new Vector2(200, 450), new Vector2(280, 280));
        var introStory = NewText("Story", intro, "", 40, new Vector2(0, 760), new Vector2(940, 330));
        var rulesCard = NewImage("RulesCard", intro, CardColor);
        rulesCard.sprite = uiSprite; rulesCard.type = Image.Type.Sliced;
        Place(rulesCard.rectTransform, new Vector2(0, 1100), new Vector2(960, 400));
        var introRules = NewText("Rules", rulesCard.transform, "", 36, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        Stretch(introRules.rectTransform, 36);
        var observeButton = NewButton("ObserveButton", intro, "기억할 모습 보기", new Vector2(0, 1560), new Vector2(720, 150));

        // ── 3. 관찰 화면 ──
        var observe = NewEmpty("ObservePanel", canvas);
        var observeTitle = NewText("Guide", observe, "", 52, new Vector2(0, 220), new Vector2(1000, 90));
        var observeCard = NewImage("Card", observe, Color.white);
        observeCard.sprite = uiSprite; observeCard.type = Image.Type.Sliced;
        Place(observeCard.rectTransform, new Vector2(0, 330), new Vector2(880, 880));
        var observeImage = NewImage("TargetImage", observeCard.transform, Color.white);
        Stretch(observeImage.rectTransform, 30);
        observeImage.preserveAspect = true;
        var observeCount = NewText("Countdown", observe, "3", 170, new Vector2(0, 1260), new Vector2(400, 240));

        // ── 4. 그리기 화면 ──
        var draw = NewEmpty("DrawPanel", canvas);
        var drawTitle = NewText("Guide", draw, "", 36, new Vector2(0, 205), new Vector2(1040, 60));
        var boardGo = new GameObject("Board", typeof(RectTransform), typeof(RawImage), typeof(DrawingBoard));
        boardGo.transform.SetParent(draw, false);
        Place(boardGo.GetComponent<RectTransform>(), new Vector2(0, 270), new Vector2(860, 860));
        var board = boardGo.GetComponent<DrawingBoard>();

        var toolBarGo = new GameObject("ToolBar", typeof(RectTransform), typeof(ToolBar));
        toolBarGo.transform.SetParent(draw, false);
        Stretch(toolBarGo.GetComponent<RectTransform>());
        var toolBar = toolBarGo.GetComponent<ToolBar>();
        Transform tb = toolBarGo.transform;

        // 1줄 : 색 (보이는 버튼만 가운데로 모이도록 HorizontalLayoutGroup 사용)
        var colorRow = NewRow("ColorRow", tb, 1150, 120);
        string[] colorNames = { "검정", "빨강", "노랑", "파랑" };
        var colorButtons = new GameObject[4];
        var colorFrames = new GameObject[4];
        var colorButtonComps = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            colorButtonComps[i] = NewToolButton("Color_" + colorNames[i], colorRow, colorNames[i], ToolBar.Palette[i], new Vector2(150, 110), out colorFrames[i]);
            colorButtons[i] = colorButtonComps[i].transform.parent.gameObject;
            if (i == 2) colorButtonComps[i].GetComponentInChildren<TMP_Text>().color = TextColor; // 노랑 위 글자는 진하게
        }
        var mixedSlot = NewToolButton("MixedSlot", colorRow, "섞은 색", new Color(0.85f, 0.85f, 0.85f), new Vector2(150, 110), out GameObject mixedFrame);
        mixedSlot.GetComponentInChildren<TMP_Text>().color = TextColor;
        var mixOpen = NewToolButton("MixOpen", colorRow, "색 섞기", ButtonColor, new Vector2(150, 110), out GameObject mixOpenFrame);
        Object.DestroyImmediate(mixOpenFrame); // 색 섞기 버튼은 선택 테두리가 필요 없어요

        // 2줄 : 지우개 · 굵기 · 되돌리기 · 모두 지우기
        var toolRow = NewRow("ToolRow", tb, 1290, 110);
        var eraserButton = NewToolButton("Eraser", toolRow, "지우개", ButtonColor, new Vector2(150, 100), out GameObject eraserFrame);
        string[] sizeNames = { "가는 선", "보통 선", "굵은 선" };
        var sizeFrames = new GameObject[3];
        var sizeButtons = new Button[3];
        for (int i = 0; i < 3; i++) sizeButtons[i] = NewToolButton("Size_" + i, toolRow, sizeNames[i], ButtonColor, new Vector2(150, 100), out sizeFrames[i]);
        var undoButton = NewToolButton("Undo", toolRow, "되돌리기", ButtonColor, new Vector2(150, 100), out GameObject undoFrame);
        var clearButton = NewToolButton("Clear", toolRow, "모두 지우기", ButtonColor, new Vector2(150, 100), out GameObject clearFrame);
        Object.DestroyImmediate(undoFrame);
        Object.DestroyImmediate(clearFrame);

        var submitButton = NewButton("SubmitButton", draw, "제출 (간식 1개)", new Vector2(0, 1430), new Vector2(700, 140));

        // 혼색 팔레트 (색 섞기를 누르면 도구 줄 위에 열림)
        var mixerCard = NewImage("MixerPanel", draw, new Color(1f, 0.97f, 0.9f, 1f));
        mixerCard.sprite = uiSprite; mixerCard.type = Image.Type.Sliced;
        Place(mixerCard.rectTransform, new Vector2(0, 1120), new Vector2(1000, 480));
        var mixer = mixerCard.gameObject.AddComponent<ColorMixer>();
        Transform mx = mixerCard.transform;
        var mixGuide = NewText("Guide", mx, "", 38, new Vector2(-60, 20), new Vector2(760, 60));
        var mixClose = NewButton("CloseButton", mx, "닫기", new Vector2(390, 15), new Vector2(170, 80));
        var materialRow = NewRow("MaterialRow", mx, 100, 130);
        var materialFrames = new GameObject[3];
        var materialButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            materialButtons[i] = NewToolButton("Material_" + colorNames[i + 1], materialRow, colorNames[i + 1], ToolBar.Palette[i + 1], new Vector2(200, 120), out materialFrames[i]);
            if (i == 1) materialButtons[i].GetComponentInChildren<TMP_Text>().color = TextColor;
        }
        var swatch = NewImage("Preview", mx, Color.gray);
        swatch.sprite = uiSprite; swatch.type = Image.Type.Sliced;
        Place(swatch.rectTransform, new Vector2(-330, 260), new Vector2(110, 110));
        var recipe = NewText("Recipe", mx, "", 42, new Vector2(80, 280), new Vector2(640, 70), TextAlignmentOptions.Left);
        var mixApply = NewButton("ApplyButton", mx, "섞어서 칠하기", new Vector2(0, 385), new Vector2(520, 80));

        // ── 5. 판정 화면 (쪽지와 간식을 도깨비에게) ──
        var judgeP = NewEmpty("JudgePanel", canvas);
        var judgeStatus = NewText("Status", judgeP, "", 40, new Vector2(0, 210), new Vector2(1000, 160));
        var judgeDokkaebi = NewSprite("Dokkaebi", judgeP, dokkaebi, new Vector2(-220, 390), new Vector2(330, 330));
        judgeDokkaebi.gameObject.AddComponent<SimpleAnim>(); // 갸웃갸웃 고민
        var noteCard = NewImage("Note", judgeP, Color.white);
        Place(noteCard.rectTransform, new Vector2(220, 400), new Vector2(300, 300));
        var judgeDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        judgeDrawing.transform.SetParent(noteCard.transform, false);
        Stretch(judgeDrawing.rectTransform, 6);
        NewSprite("Snack", judgeP, snack, new Vector2(60, 640), new Vector2(110, 110));

        var testGroup = NewEmpty("TestInput", judgeP);
        var toggles = new Toggle[5];
        var toggleLabels = new TMP_Text[5];
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i] = NewToggle($"FeatureToggle{i + 1}", testGroup, new Vector2(0, 800 + i * 105), out toggleLabels[i]);
        }
        var judgeButton = NewButton("ConfirmButton", testGroup, "판정 결과 보내기", new Vector2(0, 1380), new Vector2(700, 140));
        var backToDraw = NewButton("BackToDrawButton", judgeP, "그림으로 돌아가 다시 제출", new Vector2(0, 1380), new Vector2(760, 140));

        // ── 6. 결과 화면 ──
        var result = NewEmpty("ResultPanel", canvas);
        NewSprite("Dokkaebi", result, dokkaebi, new Vector2(-350, 250), new Vector2(240, 240));
        var resultItem = NewImage("ReturnedItem", result, Color.white);
        Place(resultItem.rectTransform, new Vector2(0, 210), new Vector2(380, 380));
        resultItem.preserveAspect = true;
        resultItem.gameObject.AddComponent<SimpleAnim>().kind = SimpleAnim.Kind.Pop; // 뿅 하고 나타남
        NewText("MyLabel", result, "내 그림", 30, new Vector2(350, 215), new Vector2(240, 44));
        var myCard = NewImage("MyCard", result, Color.white);
        Place(myCard.rectTransform, new Vector2(350, 260), new Vector2(230, 230));
        var resultMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        resultMine.transform.SetParent(myCard.transform, false);
        Stretch(resultMine.rectTransform, 4);
        var resultTitle = NewText("ResultTitle", result, "", 52, new Vector2(0, 610), new Vector2(1040, 90));
        var resultFeatures = NewText("FeatureList", result, "", 38, new Vector2(0, 710), new Vector2(900, 430), TextAlignmentOptions.TopLeft);
        var boyFace = NewSprite("BoyFace", result, boyNormal, new Vector2(-350, 1150), new Vector2(200, 200));
        var boyLine = NewText("BoyLine", result, "", 38, new Vector2(110, 1160), new Vector2(680, 180), TextAlignmentOptions.Left);
        var resultSnack = NewText("SnackLeft", result, "", 38, new Vector2(0, 1370), new Vector2(1000, 60));
        var retryButton = NewButton("RetryButton", result, "다시 그리기 (간식 1개)", new Vector2(0, 1450), new Vector2(700, 130));
        var compareButton = NewButton("CompareButton", result, "비교하기", new Vector2(0, 1600), new Vector2(700, 130));

        // ── 7. 비교 화면 ──
        var compare = NewEmpty("ComparePanel", canvas);
        NewText("MyLabel", compare, "내 그림", 46, new Vector2(-250, 220), new Vector2(460, 80));
        NewText("OriginalLabel", compare, "원본", 46, new Vector2(250, 220), new Vector2(460, 80));
        var mineCard = NewImage("MyCard", compare, Color.white);
        Place(mineCard.rectTransform, new Vector2(-250, 310), new Vector2(460, 460));
        var compareMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        compareMine.transform.SetParent(mineCard.transform, false);
        Stretch(compareMine.rectTransform, 6);
        var origCard = NewImage("OriginalCard", compare, Color.white);
        Place(origCard.rectTransform, new Vector2(250, 310), new Vector2(460, 460));
        var compareOriginal = NewImage("Original", origCard.transform, Color.white);
        Stretch(compareOriginal.rectTransform, 20);
        compareOriginal.preserveAspect = true;
        var compareFeatures = NewText("FeatureHint", compare, "", 40, new Vector2(0, 830), new Vector2(900, 450), TextAlignmentOptions.TopLeft);
        var nextButton = NewButton("NextButton", compare, "다음 단계로", new Vector2(0, 1450), new Vector2(720, 150));

        // ── 8. 이야기 종료 화면 ──
        var ending = NewEmpty("EndingPanel", canvas);
        NewText("Title", ending, "오늘의 이야기 끝!", 80, new Vector2(0, 160), new Vector2(1000, 130));
        NewSprite("Boy", ending, boyHappy, new Vector2(-330, 330), new Vector2(280, 280));
        NewSprite("Dokkaebi", ending, dokkaebi, new Vector2(0, 330), new Vector2(280, 280));
        NewSprite("Friend", ending, friend, new Vector2(330, 330), new Vector2(280, 280));
        NewText("Message", ending, "도깨비의 도움으로 친구까지 찾아\n소풍 준비를 모두 마쳤어요!", 44, new Vector2(0, 660), new Vector2(1000, 150));
        var recordCard = NewImage("RecordCard", ending, CardColor);
        recordCard.sprite = uiSprite; recordCard.type = Image.Type.Sliced;
        Place(recordCard.rectTransform, new Vector2(0, 850), new Vector2(960, 420));
        NewText("RecordTitle", recordCard.transform, "단계별 기록", 42, new Vector2(0, 25), new Vector2(900, 60));
        var endingRecords = NewText("Records", recordCard.transform, "", 38, new Vector2(0, 100), new Vector2(880, 300), TextAlignmentOptions.TopLeft);
        var restartButton = NewButton("BackToTitleButton", ending, "처음으로", new Vector2(0, 1400), new Vector2(720, 150));

        // HUD 가 패널보다 위에 그려지도록 맨 뒤로 보내기 (Hierarchy 아래쪽일수록 앞에 그려짐)
        hud.SetAsLastSibling();

        // ── 버튼 OnClick 연결 (Inspector 의 On Click() 목록에 보여요) ──
        UnityEventTools.AddPersistentListener(startGameButton.onClick, gm.StartGame);
        UnityEventTools.AddPersistentListener(observeButton.onClick, gm.StartObserve);
        UnityEventTools.AddPersistentListener(submitButton.onClick, gm.Submit);
        UnityEventTools.AddPersistentListener(judgeButton.onClick, gm.OnJudgeConfirmed);
        UnityEventTools.AddPersistentListener(backToDraw.onClick, gm.BackToDraw);
        UnityEventTools.AddPersistentListener(retryButton.onClick, gm.Retry);
        UnityEventTools.AddPersistentListener(compareButton.onClick, gm.ShowCompare);
        UnityEventTools.AddPersistentListener(nextButton.onClick, gm.NextStage);
        UnityEventTools.AddPersistentListener(restartButton.onClick, gm.BackToTitle);

        for (int i = 0; i < 4; i++) UnityEventTools.AddIntPersistentListener(colorButtonComps[i].onClick, toolBar.SelectColor, i);
        UnityEventTools.AddPersistentListener(mixedSlot.onClick, toolBar.SelectMixed);
        UnityEventTools.AddPersistentListener(mixOpen.onClick, toolBar.OpenMixer);
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
        Set(so, "incenseBar", incenseBar);
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
        Set(so, "observeCountText", observeCount);
        Set(so, "drawTitle", drawTitle);
        Set(so, "submitButton", submitButton);
        Set(so, "judgeDrawing", judgeDrawing);
        Set(so, "judgeStatusText", judgeStatus);
        Set(so, "testInputGroup", testGroup.gameObject);
        SetArray(so, "featureToggles", toggles);
        SetArray(so, "featureToggleLabels", toggleLabels);
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
        Set(tso, "eraserFrame", eraserFrame);
        SetArray(tso, "sizeFrames", sizeFrames);
        Set(tso, "undoButton", undoButton);
        Set(tso, "clearButton", clearButton);
        tso.ApplyModifiedPropertiesWithoutUndo();

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
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
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
                            TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = TextColor;
        t.alignment = align;
        t.raycastTarget = false; // 글자가 클릭을 가로채지 않게
        Place(t.rectTransform, pos, box);
        return t;
    }

    static Button NewButton(string name, Transform parent, string label, Vector2 pos, Vector2 box)
    {
        var img = NewImage(name, parent, ButtonColor);
        img.sprite = uiSprite;
        img.type = Image.Type.Sliced;
        Place(img.rectTransform, pos, box);
        var button = img.gameObject.AddComponent<Button>();

        var t = NewText("Label", img.transform, label, 42, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        t.color = Color.white;
        return button;
    }

    // 가로로 버튼을 늘어놓는 줄. 꺼진 버튼은 빼고 남은 버튼을 가운데로 모아요.
    static Transform NewRow(string name, Transform parent, float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), new Vector2(0, y), new Vector2(1060, height));
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 18;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return go.transform;
    }

    // 선택 테두리가 있는 도구 버튼 : [묶음] 안에 [Frame(테두리, 뒤)] + [버튼(앞)]
    static Button NewToolButton(string name, Transform row, string label, Color color, Vector2 size, out GameObject frame)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(row, false);
        holder.GetComponent<RectTransform>().sizeDelta = size;

        var frameImg = NewImage("Frame", holder.transform, FrameColor);
        frameImg.sprite = uiSprite; frameImg.type = Image.Type.Sliced;
        Stretch(frameImg.rectTransform, -9); // 버튼보다 9 만큼 크게 → 테두리처럼 보임
        frameImg.raycastTarget = false;
        frame = frameImg.gameObject;

        var img = NewImage("Button", holder.transform, color);
        img.sprite = uiSprite; img.type = Image.Type.Sliced;
        Stretch(img.rectTransform);
        var button = img.gameObject.AddComponent<Button>();

        var t = NewText("Label", img.transform, label, 30, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        t.color = Color.white;
        return button;
    }

    static Toggle NewToggle(string name, Transform parent, Vector2 pos, out TMP_Text label)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        Place(root.GetComponent<RectTransform>(), pos, new Vector2(900, 95));

        // 체크 상자
        var box = NewImage("Box", root.transform, Color.white);
        box.sprite = uiSprite;
        box.type = Image.Type.Sliced;
        var boxRt = box.rectTransform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0, 0.5f);
        boxRt.pivot = new Vector2(0, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(80, 80);

        // 체크 표시 (켜지면 보임)
        var check = NewImage("Check", box.transform, new Color(0.18f, 0.49f, 0.20f));
        check.sprite = uiSprite;
        check.type = Image.Type.Sliced;
        Stretch(check.rectTransform, 14);

        // 글자 (GameManager 가 특징 이름으로 채워요)
        label = NewText("Label", root.transform, "F?", 40, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(110, 0);
        lrt.offsetMax = Vector2.zero;
        label.raycastTarget = true; // 글자를 눌러도 체크되게

        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.toggleTransition = Toggle.ToggleTransition.None; // 바로 보이게
        toggle.isOn = false;
        return toggle;
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

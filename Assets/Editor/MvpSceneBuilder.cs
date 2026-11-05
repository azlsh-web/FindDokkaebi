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
    static readonly Color SizeDotColor = new Color32(150, 140, 130, 255);    // 굵기 점 (고르지 않은 것)

    static TMP_FontAsset font;
    static TMP_FontAsset talkFont;
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
        talkFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);   // 대사는 얇고 작은 글꼴로
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
        Sprite dokkaebiNoBat = LoadSprite("Characters/Char_Dokkaebi_NoBat.png");    // 방망이를 찾기 전 (아트팀 그림)
        Sprite faceDokkaebi = LoadSprite("Characters/Char_DokkaebiFace_Normal.png");
        Sprite picnic = LoadSprite("Background/BG_Picnic.png");                     // 이야기 종료 (임시 그림)
        Sprite friend = LoadSprite("Stage5_Friend/Friend_Original.png");
        Sprite snack = T("HUD_SnackBag");                                 // 간식 = 시안의 복주머니
        Sprite room = LoadSprite("Background/BG_Room.png");          // 시작 화면 그림에서 글자를 지운 방
        Sprite startBackground = LoadSprite("Background/BG_Start.png");  // 시작 화면의 빈 방 (물건은 위에 하나씩 올려요)
        Sprite iconClose = LoadSprite("UI/Mix_CloseIcon.png");

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

        // 대사 중 어두운 막 : 고정 크기인 Frame 밖(Canvas 바로 아래)에 둬서 어떤 화면 비율에서도 꽉 차요. 켜고 끄기는 GameManager.ShowPanel
        //   SimpleAnim 의 FadeIn 은 끝에 불투명도 1 로 만들어서, 그림이 아닌 CanvasGroup 으로 페이드해야 42% 가 유지돼요 (NewPanel)
        var dialogueDim = NewPanel("DialogueDim", root);
        var dialogueDimImage = dialogueDim.gameObject.AddComponent<Image>();
        dialogueDimImage.color = new Color(40f / 255f, 24f / 255f, 10f / 255f, 0.42f);
        dialogueDimImage.raycastTarget = false;

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
        var snackAnims = new SimpleAnim[3];
        for (int i = 0; i < snackIcons.Length; i++)
        {
            float x0 = 1192 + i * 220;
            snackIcons[i] = TeamImage("Snack_" + i, snackRow, snack, x0, 127, x0 + 182, 329);
            snackAnims[i] = Anim(snackIcons[i].gameObject, SimpleAnim.Kind.SlideRight, 0.4f, (snackIcons.Length - 1 - i) * 0.14f, 900f);
            snackAnims[i].enabled = false;
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

        // ── 1. 시작 화면 : 로고 · 캐릭터 · 시작 버튼만 ──
        var title = NewPanel("TitlePanel", canvas);
        var titleBoy = MImage("TitleBoy", title, boyNormal, 44, 250, 150, 228);
        Anim(titleBoy.gameObject, SimpleAnim.Kind.Gentle, 1.2f, 0f, 15f);
        var titleDok = MImage("TitleDokkaebi", title, dokkaebi, 158, 318, 176, 172);
        Anim(titleDok.gameObject, SimpleAnim.Kind.Gentle, 1.2f, 0.5f, 15f);
        MImage("TitleLogo", title, T("Title_Logo"), 48, 30, 264, 192);
        var startGameButton = NewButton("StartButton", title, "게임 시작하기", Vector2.zero, new Vector2(780, 210));
        M((RectTransform)startGameButton.transform, 50, 516, 260, 70);
        startGameButton.GetComponentInChildren<TMP_Text>().fontSize = 96;
        startGameButton.GetComponentInChildren<TMP_Text>().color = Color.white;

        // ── 2. 대화 화면 : 캐릭터 + 대사 상자. 오프닝 그림과 건네기 쪽지도 여기에 있어요 ──
        var dialogue = NewPanel("DialoguePanel", canvas);
        // 오프닝 : 물건 그림은 GameManager 가 단계 데이터에서 넣어요
        var openingGroup = NewEmpty("Opening", dialogue);
        var openingBag = MImage("Bag", openingGroup, null, 12, 300, 84, 84);
        var openingMat = MImage("Mat", openingGroup, null, 262, 340, 90, 56);
        var openingGimbap = MImage("Gimbap", openingGroup, null, 24, 418, 58, 58);
        var openingItems = new SimpleAnim[3];
        openingItems[0] = Anim(openingBag.gameObject, SimpleAnim.Kind.None, 0.5f, 0f, 0f);
        openingItems[1] = Anim(openingMat.gameObject, SimpleAnim.Kind.None, 0.5f, 0f, 0f);
        openingItems[2] = Anim(openingGimbap.gameObject, SimpleAnim.Kind.None, 0.5f, 0f, 0f);
        var openingAsk = NewEmpty("Ask", openingGroup);
        for (int i = 0; i < 3; i++)
        {
            var ask = NewText("Question_" + i, openingAsk, "?", 126, Vector2.zero, Vector2.zero);
            M(ask.rectTransform, new[] { 30, 288, 34 }[i], new[] { 310, 330, 414 }[i], 50, 50);
            ask.color = new Color32(200, 72, 60, 255);
            Anim(ask.gameObject, SimpleAnim.Kind.Look, 2f, 0f, 0f);
        }
        var openingIdea = NewEmpty("Idea", openingGroup);
        var ideaNote = NewCard("Note", openingIdea, Vector2.zero, Vector2.zero);
        M((RectTransform)ideaNote, 96, 120, 64, 76);
        Anim(ideaNote.gameObject, SimpleAnim.Kind.Pop, 0.45f, 0f, 0f);
        var ideaPencil = NewImage("Pencil", ideaNote, Color.white);
        ideaPencil.sprite = T("Draw_PencilButton");
        ideaPencil.preserveAspect = true;
        ideaPencil.raycastTarget = false;
        Stretch(ideaPencil.rectTransform, 24);
        var ideaSnack = MImage("Snack", openingIdea, snack, 196, 136, 62, 68);
        Anim(ideaSnack.gameObject, SimpleAnim.Kind.Pop, 0.45f, 0.2f, 0f);
        var openingCall = NewEmpty("Call", openingGroup);
        var callLeft = NewText("Left", openingCall, "((", 150, Vector2.zero, Vector2.zero);
        M(callLeft.rectTransform, 34, 236, 54, 70);
        var callRight = NewText("Right", openingCall, "))", 150, Vector2.zero, Vector2.zero);
        M(callRight.rectTransform, 272, 236, 54, 70);
        Anim(callLeft.gameObject, SimpleAnim.Kind.Look, 0.8f, 0f, 0f);
        Anim(callRight.gameObject, SimpleAnim.Kind.Look, 0.8f, 0f, 0f);

        var boyHolderGo = new GameObject("Boy", typeof(RectTransform));
        boyHolderGo.transform.SetParent(dialogue, false);
        var boyHolder = (RectTransform)boyHolderGo.transform;
        M(boyHolder, 14, 256, 150, 228);
        var boyImage = NewImage("Image", boyHolder, Color.white);
        boyImage.sprite = boyNormal;
        boyImage.preserveAspect = true;
        boyImage.raycastTarget = false;
        Stretch(boyImage.rectTransform);
        var boyImageAnim = Anim(boyImage.gameObject, SimpleAnim.Kind.None, 0.8f, 0f, 15f);

        var dokHolderGo = new GameObject("Dokkaebi", typeof(RectTransform));
        dokHolderGo.transform.SetParent(dialogue, false);
        var dokHolder = (RectTransform)dokHolderGo.transform;
        M(dokHolder, 150, 296, 200, 195);
        Anim(dokHolderGo, SimpleAnim.Kind.EnterRight, 0.8f, 0f, 660f);
        var dokImage = NewImage("Image", dokHolder, Color.white);
        dokImage.sprite = dokkaebiNoBat;
        dokImage.preserveAspect = true;
        dokImage.raycastTarget = false;
        Stretch(dokImage.rectTransform);
        var dokImageAnim = Anim(dokImage.gameObject, SimpleAnim.Kind.None, 0.8f, 0f, 15f);

        // 건네기 : 그림과 글이 적힌 쪽지. 간식은 위쪽 간식 자리에서 도깨비에게 날아가요.
        var giveNote = NewCard("GiveNote", dialogue, Vector2.zero, Vector2.zero);
        M((RectTransform)giveNote, 82, 58, 190, 192);
        giveNote.gameObject.AddComponent<CanvasGroup>();
        var giveNoteAnim = Anim(giveNote.gameObject, SimpleAnim.Kind.Float, 3f, 0f, 30f, 2f);
        var giveNoteText = NewText("Text", giveNote, "", 42, Vector2.zero, Vector2.zero);
        PIn(giveNoteText.rectTransform, 12, 8, 166, 40);
        giveNoteText.enableAutoSizing = true; giveNoteText.fontSizeMin = 30; giveNoteText.fontSizeMax = 42;
        var giveNoteDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        giveNoteDrawing.transform.SetParent(giveNote, false);
        PIn(giveNoteDrawing.rectTransform, 22, 46, 146, 138);
        giveNoteDrawing.raycastTarget = false;
        var giveSnack = MImage("SnackOut", dialogue, snack, 191, 20, 30, 33);
        Anim(giveSnack.gameObject, SimpleAnim.Kind.SnackOut, 0.7f, 0f, 0f);
        var dialogueBoxComp = NewDialogueBox(dialogue, faceSurprised);
        NewTap("TapButton", dialogue);
        var dialogueTapButton = dialogue.Find("TapButton").GetComponent<Button>();

        // ── 3. 떠올리기 화면 : 말풍선 속 원본과 숫자. 2단계부터 주인공, 1단계는 도깨비가 떠올려요 ──
        var observe = NewPanel("ObservePanel", canvas);
        var observeCard = NewCard("Card", observe, Vector2.zero, Vector2.zero);
        M((RectTransform)observeCard, 24, 30, 312, 352);
        observeCard.GetComponent<Image>().pixelsPerUnitMultiplier = 0.4f / K;  // 말풍선처럼 더 둥글게
        var observeBoyGroup = NewEmpty("BoyRecall", observe);
        BubbleDot(observeBoyGroup, 100, 392, 30);
        BubbleDot(observeBoyGroup, 84, 428, 18);
        MImage("Boy", observeBoyGroup, boyNormal, 4, 448, 150, 228);
        var observeDokGroup = NewEmpty("DokkaebiRecall", observe);
        BubbleDot(observeDokGroup, 236, 392, 30);
        BubbleDot(observeDokGroup, 254, 428, 18);
        MImage("Dokkaebi", observeDokGroup, dokkaebiNoBat, 176, 446, 176, 172);
        var observeImage = NewImage("TargetImage", observeCard, Color.white);
        Stretch(observeImage.rectTransform, 60);
        observeImage.preserveAspect = true;
        observeImage.raycastTarget = false;
        var countEdge = MImage("CountEdge", observe, circleSprite, 216, 462, 96, 96);
        countEdge.color = OutlineColor;
        var countFill = NewImage("CountFill", countEdge.transform, new Color32(242, 119, 107, 255));
        countFill.sprite = circleSprite;
        countFill.raycastTarget = false;
        Stretch(countFill.rectTransform, 15);
        var observeCount = NewText("CountText", countEdge.transform, "3", 186, Vector2.zero, Vector2.zero);
        Stretch(observeCount.rectTransform);
        observeCount.color = Color.white;
        var observeCountAnim = Anim(observeCount.gameObject, SimpleAnim.Kind.NumPop, 0.25f, 0f, 0f);

        // ── 4. 그리기 화면 : 팀 시안(그림 10) — 클립보드 쪽지, 물감 판, 도구, "다 그렸어!" ──
        var draw = NewPanel("DrawPanel", canvas);
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
        var closeRow = NewRow("CloseRow", mx, 16, 72);
        Place(closeRow.GetComponent<RectTransform>(), new Vector2(405, 16), new Vector2(80, 72));
        var mixClose = NewRoundTool("CloseButton", closeRow, iconClose, Color.white, 64, out GameObject closeFrame);
        Object.DestroyImmediate(closeFrame);
        // 왼쪽 물감 3개를 누르면 오른쪽 큰 동그라미의 색이 바로 바뀌어요
        var materialRow = NewRow("MaterialRow", mx, 70, 150);
        Place(materialRow.GetComponent<RectTransform>(), new Vector2(-140, 70), new Vector2(560, 150));
        var materialButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            materialButtons[i] = NewRoundTool("Material_" + colorNames[i + 1], materialRow, null, ToolBar.Palette[i + 1], 130, out GameObject materialFrame);
            Object.DestroyImmediate(materialFrame);
            materialButtons[i].transform.parent.gameObject.AddComponent<CanvasGroup>();  // 못 누를 때 통째로 흐리게
            var materialColors = materialButtons[i].colors;
            materialColors.disabledColor = Color.white;                                   // 어두워지지 않게
            materialButtons[i].colors = materialColors;
        }
        var swatchEdge = NewImage("PreviewEdge", mx, OutlineColor);
        swatchEdge.sprite = circleSprite;
        Place(swatchEdge.rectTransform, new Vector2(250, 40), new Vector2(190, 190));
        var swatch = NewImage("Preview", swatchEdge.transform, Color.white);
        swatch.sprite = circleSprite;
        Stretch(swatch.rectTransform, 6);
        var mixUndo = TeamTool("UndoButton", mx, T("Draw_UndoButton"), 0, 0, 1, 1, 0, out GameObject mixUndoFrame);
        Object.DestroyImmediate(mixUndoFrame);
        Place((RectTransform)mixUndo.transform.parent, new Vector2(-300, 262), new Vector2(100, 95));
        var mixApply = NewButton("ApplyButton", mx, "섞어서 칠하기", new Vector2(40, 272), new Vector2(480, 76));

        // ── 5. 판정 대기 : 내 그림은 떠 있고, 도깨비는 가운데서 좁게 오가요 ──
        var judgeP = NewPanel("JudgePanel", canvas);
        var judgeDrawingBack = MImage("MyDrawingBack", judgeP, cardSprite, 105, 90, 150, 177);
        judgeDrawingBack.type = Image.Type.Sliced;
        judgeDrawingBack.preserveAspect = false;
        judgeDrawingBack.pixelsPerUnitMultiplier = 1f / K;
        Anim(judgeDrawingBack.gameObject, SimpleAnim.Kind.Float, 3f, 0f, 30f, 2f);
        var judgeDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        judgeDrawing.transform.SetParent(judgeDrawingBack.transform, false);
        PIn(judgeDrawing.rectTransform, 8, 28, 134, 142);
        judgeDrawing.raycastTarget = false;
        var judgeNoteText = NewText("Text", judgeDrawingBack.transform, "", 30, Vector2.zero, Vector2.zero);
        PIn(judgeNoteText.rectTransform, 6, 5, 138, 24);
        judgeNoteText.enableAutoSizing = true; judgeNoteText.fontSizeMin = 22; judgeNoteText.fontSizeMax = 30;

        var dokEnterGo = new GameObject("DokkaebiEnter", typeof(RectTransform));
        dokEnterGo.transform.SetParent(judgeP, false);
        M((RectTransform)dokEnterGo.transform, 65, 345, 170, 200);
        Anim(dokEnterGo, SimpleAnim.Kind.EnterLeft, 0.7f, 0f, 720f);
        var roamGo = new GameObject("Roam", typeof(RectTransform));
        roamGo.transform.SetParent(dokEnterGo.transform, false);
        Stretch((RectTransform)roamGo.transform);
        Anim(roamGo, SimpleAnim.Kind.Roam, 6f, 0.7f, 180f);
        var stepGo = new GameObject("Step", typeof(RectTransform));
        stepGo.transform.SetParent(roamGo.transform, false);
        Stretch((RectTransform)stepGo.transform);
        Anim(stepGo, SimpleAnim.Kind.Step, 1.2f, 0f, 12f, 1.5f);
        var judgeDokkaebi = MImage("Dokkaebi", stepGo.transform, dokkaebiThink, 0, 0, 170, 166);
        Anchor(judgeDokkaebi.rectTransform, 0.5f, 1f, Vector2.zero, new Vector2(510, 498));
        var question = NewText("Question", stepGo.transform, "?", 114, new Vector2(0, -102), new Vector2(150, 120));
        question.color = new Color32(200, 72, 60, 255);
        Anim(question.gameObject, SimpleAnim.Kind.Look, 3f, 0f, 0f);
        var dust = NewImage("Dust", roamGo.transform, new Color32(232, 214, 188, 255));
        dust.sprite = T("Common_SoftPuff");
        dust.raycastTarget = false;
        Anchor(dust.rectTransform, 0.2f, 0f, new Vector2(0, 54), new Vector2(78, 78));
        Anim(dust.gameObject, SimpleAnim.Kind.Dust, 1.2f, 0f, 24f);

        // ── 6. 결과 화면 : 물건은 바닥에, 대사를 누르면 순서대로 ──
        var result = NewPanel("ResultPanel", canvas);
        var itemShadow = MImage("ItemShadow", result, T("Common_SoftPuff"), 165, 462, 160, 24);
        itemShadow.preserveAspect = false;
        itemShadow.color = new Color(60f / 255f, 40f / 255f, 20f / 255f, 0.35f);
        var resultItem = MImage("ReturnedItem", result, null, 130, 256, 220, 220);
        PivotBottom(resultItem.rectTransform);
        Anim(resultItem.gameObject, SimpleAnim.Kind.Pop, 0.6f, 0f, 0f);
        var sparkles = NewEmpty("Sparkles", result);
        float[][] sparkleXY = { new float[] { 150, 262, 22, 0.3f }, new float[] { 326, 286, 16, 0.7f }, new float[] { 318, 430, 20, 1f } };
        for (int i = 0; i < sparkleXY.Length; i++)
        {
            float[] sp = sparkleXY[i];
            var spark = MImage("Spark", sparkles, circleSprite, sp[0], sp[1], sp[2], sp[2]);
            spark.color = new Color32(245, 201, 107, 255);
            Anim(spark.gameObject, SimpleAnim.Kind.Spark, 1.4f, sp[3], 0f);
        }
        var resultDok = MImage("Dokkaebi", result, dokkaebiSwing, 8, 300, 150, 146);
        PivotBottom(resultDok.rectTransform);
        var resultDokAnim = Anim(resultDok.gameObject, SimpleAnim.Kind.Swing, 0.9f, 0f, 0f, 6f);
        var resultBoxComp = NewDialogueBox(result, faceSurprised);
        var resultTapButton = NewTap("TapButton", result);

        var choiceRow = NewEmpty("ChoiceRow", result);
        Anim(choiceRow.gameObject, SimpleAnim.Kind.FadeIn, 0.25f, 0f, 0f);
        choiceRow.gameObject.AddComponent<CanvasGroup>();
        Button ChoiceButton(string name, float x, Color tint, Sprite icon, string label, Color labelColor)
        {
            var b = NewCard(name, choiceRow, Vector2.zero, Vector2.zero);
            M((RectTransform)b, x, 513, 162, 104);
            b.GetComponent<Image>().color = tint;
            var iconImg = NewImage("Icon", b, Color.white);
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            Anchor(iconImg.rectTransform, 0.5f, 0.5f, new Vector2(-96, 0), new Vector2(114, 114));
            var t = NewText("Label", b, label, 72, Vector2.zero, Vector2.zero);
            Anchor(t.rectTransform, 0.5f, 0.5f, new Vector2(66, 0), new Vector2(210, 120));
            t.color = labelColor;
            return b.gameObject.AddComponent<Button>();
        }
        var retryButton = ChoiceButton("RetryButton", 12, new Color32(242, 119, 107, 255), T("Result_IconRetry"), "다시", Color.white);
        var retryCost = NewImage("Cost", retryButton.transform, Color.white);
        retryCost.sprite = snack;
        retryCost.preserveAspect = true;
        retryCost.raycastTarget = false;
        Anchor(retryCost.rectTransform, 1f, 1f, new Vector2(-21, -21), new Vector2(78, 84));
        var giveUpButton = ChoiceButton("GiveUpButton", 186, Color.white, T("Result_IconGiveUp"), "포기", TextColor);

        // ── 7. 채점표 : 시험지처럼 번호 위에 동그라미 · 빗금 ──
        var compare = NewPanel("ComparePanel", canvas);
        var paper = NewCard("Paper", compare, Vector2.zero, Vector2.zero);
        M((RectTransform)paper, 18, 62, 324, 520);
        var mineCard = NewCard("MyCard", paper, Vector2.zero, Vector2.zero);
        PIn((RectTransform)mineCard, 14, 14, 138, 150);
        var compareMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        compareMine.transform.SetParent(mineCard, false);
        Stretch(compareMine.rectTransform, 12);
        compareMine.raycastTarget = false;
        var origCard = NewCard("OriginalCard", paper, Vector2.zero, Vector2.zero);
        PIn((RectTransform)origCard, 164, 14, 138, 150);
        var compareOriginal = NewImage("Original", origCard, Color.white);
        Stretch(compareOriginal.rectTransform, 24);
        compareOriginal.preserveAspect = true;
        compareOriginal.raycastTarget = false;
        var scoreRows = new GameObject[5];
        var scoreTexts = new TMP_Text[5];
        var scoreCircles = new GameObject[5];
        var scoreSlashes = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            var row = new GameObject("Row_" + (i + 1), typeof(RectTransform));
            row.transform.SetParent(paper, false);
            PIn((RectTransform)row.transform, 14, 178 + i * 64, 290, 60);
            scoreRows[i] = row;
            var numberText = NewText("Number", row.transform, (i + 1).ToString(), 72, Vector2.zero, Vector2.zero);
            Anchor(numberText.rectTransform, 0f, 0.5f, Vector2.zero, new Vector2(120, 120));
            scoreTexts[i] = NewText("Text", row.transform, "", 54, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
            Anchor(scoreTexts[i].rectTransform, 0f, 0.5f, new Vector2(150, 0), new Vector2(720, 180));
            float delay = 0.3f + i * 0.5f;
            Image Mark(string name, Sprite sprite, Image.FillMethod method)
            {
                var m = NewImage(name, row.transform, Color.white);
                m.sprite = sprite;
                m.type = Image.Type.Filled;
                m.fillMethod = method;
                m.raycastTarget = false;
                Anchor(m.rectTransform, 0f, 0.5f, new Vector2(-12, 0), new Vector2(144, 144));
                Anim(m.gameObject, SimpleAnim.Kind.FillIn, 0.3f, delay, 0f);
                return m;
            }
            scoreCircles[i] = Mark("Circle", T("Score_Circle"), Image.FillMethod.Radial360).gameObject;
            scoreSlashes[i] = Mark("Slash", T("Score_Slash"), Image.FillMethod.Vertical).gameObject;
        }
        var scoreArrow = NewImage("Arrow", paper, new Color(1f, 1f, 1f, 0.8f));
        scoreArrow.sprite = T("Common_NextArrow");
        scoreArrow.raycastTarget = false;
        Anchor(scoreArrow.rectTransform, 1f, 0f, new Vector2(-42, 36), new Vector2(54, 54));
        var compareTapButton = NewTap("TapButton", compare);

        // ── 8. 이야기 종료 : 소풍 장면. 캐릭터는 땅에 서 있고, 물건 옆에 남은 간식 ──
        var ending = NewPanel("EndingPanel", canvas);
        Image Standing(string name, Sprite sprite, float x, float y, float w, float h, float delay)
        {
            var img = MImage(name, ending, sprite, x, y, w, h);
            PivotBottom(img.rectTransform);
            Anim(img.gameObject, SimpleAnim.Kind.FootSway, 2.6f, delay, 0f, 1.5f);
            return img;
        }
        Standing("Boy", boyHappy, 6, 224, 120, 183, 0f);
        var endFriend = Standing("FriendSlot", friend, 90, 233, 170, 170, 0.3f);
        var endDokkaebi = Standing("Dokkaebi", dokkaebi, 226, 290, 130, 127, 0.6f);
        Image Item(string name, float x, float y, float w, float h, float delay)
        {
            var img = MImage(name, ending, null, x, y, w, h);
            Anim(img.gameObject, SimpleAnim.Kind.Land, 0.7f, delay, 1080f);
            return img;
        }
        var endMat = Item("MatSlot", 6, 386, 348, 203, 0.2f);
        var endBag = Item("BagSlot", 50, 418, 74, 66, 0.7f);
        var endGimbap = Item("GimbapSlot", 150, 468, 62, 62, 1.2f);
        var endBat = Item("BatSlot", 246, 416, 66, 90, 1.7f);
        // 물건 옆 간식 : 순서는 방망이, 가방, 돗자리, 김밥, 친구
        float[][] snackSpots = { new float[] { 316, 424, 2.2f }, new float[] { 128, 424, 1.2f }, new float[] { 22, 490, 0.7f }, new float[] { 216, 472, 1.7f }, new float[] { 222, 250, 2.5f } };
        var endingSnacks = new GameObject[15];
        for (int i = 0; i < 5; i++)
            for (int k = 0; k < 3; k++)
            {
                float[] spot = snackSpots[i];
                var s = MImage($"Snack_{i + 1}_{k + 1}", ending, snack, spot[0], spot[1] + k * 17, 15, 16);
                Anim(s.gameObject, SimpleAnim.Kind.Pop, 0.35f, spot[2] + k * 0.1f, 0f);
                endingSnacks[i * 3 + k] = s.gameObject;
            }
        Color32[] confettiColors = { new Color32(242, 119, 107, 255), new Color32(245, 201, 107, 255), new Color32(90, 167, 217, 255) };
        float[][] confetti = { new float[] { 40, 3.2f, 0f }, new float[] { 110, 2.6f, 0.5f }, new float[] { 190, 3.5f, 1f }, new float[] { 260, 2.9f, 0.2f }, new float[] { 325, 3.3f, 1.4f } };
        for (int i = 0; i < confetti.Length; i++)
        {
            var c = NewImage("Confetti", ending, confettiColors[i % confettiColors.Length]);
            c.raycastTarget = false;
            M(c.rectTransform, confetti[i][0], 0, 10, 14);
            Anim(c.gameObject, SimpleAnim.Kind.Fall, confetti[i][1], confetti[i][2], 0f);
        }
        var endingBoxComp = NewDialogueBox(ending, faceSurprised);
        var endingTapButton = NewTap("TapButton", ending);

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
        UnityEventTools.AddPersistentListener(dialogueTapButton.onClick, gm.DialogueTap);
        UnityEventTools.AddPersistentListener(submitButton.onClick, gm.Submit);
        UnityEventTools.AddPersistentListener(retryButton.onClick, gm.Retry);
        UnityEventTools.AddPersistentListener(giveUpButton.onClick, gm.ShowCompare);
        UnityEventTools.AddPersistentListener(resultTapButton.onClick, gm.ResultTap);
        UnityEventTools.AddPersistentListener(compareTapButton.onClick, gm.NextStage);
        UnityEventTools.AddPersistentListener(endingTapButton.onClick, gm.EndingTap);
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

        for (int i = 0; i < 3; i++) UnityEventTools.AddIntPersistentListener(materialButtons[i].onClick, mixer.AddPaint, i);
        UnityEventTools.AddPersistentListener(mixUndo.onClick, mixer.UndoPaint);
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
        SetArray(so, "snackAnims", snackAnims);
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
        Set(so, "observePanel", observe.gameObject);
        Set(so, "drawPanel", draw.gameObject);
        Set(so, "judgePanel", judgeP.gameObject);
        Set(so, "resultPanel", result.gameObject);
        Set(so, "comparePanel", compare.gameObject);
        Set(so, "endingPanel", ending.gameObject);
        Set(so, "endingBackground", picnic);
        Set(so, "dialoguePanel", dialogue.gameObject);
        Set(so, "dialogueDim", dialogueDim.gameObject);
        Set(so, "faceBoySurprised", faceSurprised);
        Set(so, "faceBoyHappy", faceHappy);
        Set(so, "faceDokkaebi", faceDokkaebi);
        Set(so, "dokkaebiNormal", dokkaebi);
        Set(so, "dokkaebiNoBat", dokkaebiNoBat);
        Set(so, "dokkaebiSwing", dokkaebiSwing);
        Set(so, "dialogueBox", dialogueBoxComp);
        Set(so, "dialogueBoy", boyHolder);
        Set(so, "dialogueBoyAnim", boyImageAnim);
        Set(so, "dialogueDokkaebi", dokHolderGo);
        Set(so, "dialogueDokkaebiImage", dokImage);
        Set(so, "dialogueDokkaebiAnim", dokImageAnim);
        Set(so, "openingGroup", openingGroup.gameObject);
        SetArray(so, "openingItems", openingItems);
        Set(so, "openingAsk", openingAsk.gameObject);
        Set(so, "openingIdea", openingIdea.gameObject);
        Set(so, "openingCall", openingCall.gameObject);
        Set(so, "giveNote", giveNote.gameObject);
        Set(so, "giveNoteAnim", giveNoteAnim);
        Set(so, "giveNoteText", giveNoteText);
        Set(so, "giveNoteDrawing", giveNoteDrawing);
        Set(so, "giveSnackOut", giveSnack.rectTransform);
        Set(so, "observeCountEdge", countEdge.rectTransform);
        Set(so, "observeBoy", observeBoyGroup.gameObject);
        Set(so, "observeDokkaebi", observeDokGroup.gameObject);
        Set(so, "judgeNoteText", judgeNoteText);
        Set(so, "observeCount", observeCount);
        Set(so, "observeCountAnim", observeCountAnim);
        Set(so, "resultBox", resultBoxComp);
        Set(so, "resultItemShadow", itemShadow.gameObject);
        Set(so, "resultSparkles", sparkles.gameObject);
        Set(so, "resultDokkaebi", resultDok);
        Set(so, "resultDokkaebiAnim", resultDokAnim);
        Set(so, "choiceRow", choiceRow.gameObject);
        SetArray(so, "scoreRows", scoreRows);
        SetArray(so, "scoreTexts", scoreTexts);
        SetArray(so, "scoreCircles", scoreCircles);
        SetArray(so, "scoreSlashes", scoreSlashes);
        Set(so, "endingDokkaebi", endDokkaebi);
        Set(so, "endingBox", endingBoxComp);
        SetArray(so, "endingItems", new[] { endBat, endBag, endMat, endGimbap, endFriend });
        SetArray(so, "endingSnacks", endingSnacks);
        Set(so, "observeImage", observeImage);
        Set(so, "drawTitle", drawTitle);
        Set(so, "submitButton", submitButton);
        Set(so, "judgeDrawing", judgeDrawing);
        Set(so, "resultItemImage", resultItem);
        Set(so, "compareMyDrawing", compareMine);
        Set(so, "compareOriginal", compareOriginal);
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
        SetArray(mso, "materialButtons", materialButtons);
        Set(mso, "previewSwatch", swatch);
        Set(mso, "undoButton", mixUndo);
        Set(mso, "applyButton", mixApply);
        mso.ApplyModifiedPropertiesWithoutUndo();

        // 처음에는 시작 화면만 보이게 (게임 시작 시 GameManager 도 같은 처리를 해요)
        foreach (Transform p in new[] { dialogue, dialogueDim, observe, draw, judgeP, result, compare, ending }) p.gameObject.SetActive(false);
        giveNote.gameObject.SetActive(false);
        giveSnack.gameObject.SetActive(false);
        openingGroup.gameObject.SetActive(false);
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
            "굵은 머리에 손잡이가 이어진 도깨비방망이입니다. 머리에는 짧게 튀어나온 돌기가 여러 개 있고, 손잡이에는 리본이 묶여 있습니다. 검은색으로 형태만 표현하는 단계이며, 색은 판정하지 않습니다.",
            ("굵은 머리 + 돌기", "굵은 머리가 표현되어 있고, 머리 바깥 윤곽에 튀어나온 돌기가 두 개 이상 구분되어야 합니다. 뾰족한 돌기, 둥근 혹, 구름 모양을 인정합니다. 매끈한 원이나 타원 머리, 머리 안쪽의 무늬, 손잡이의 리본은 돌기로 인정하지 않습니다. 손잡이의 유무와 길이는 판정하지 않습니다."));

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
            "두 가지 이상 색을 자유롭게 섞어요.\n많이 넣은 색에 따라 섞인 색이 달라져요.",
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

    // ── 연출 개편 도우미 : 화면을 360x633 으로 본 좌표 (x, y, 폭, 높이) → 실제는 3배 ──
    static void M(RectTransform rt, float x, float y, float w, float h)
    {
        Place(rt, new Vector2((x + w * 0.5f) * 3f - 540f, y * 3f), new Vector2(w * 3f, h * 3f));
    }

    // 부모 안의 왼쪽 위 기준 좌표 (같은 3배 단위)
    static void PIn(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * 3f, -y * 3f);
        rt.sizeDelta = new Vector2(w * 3f, h * 3f);
    }

    static Image MImage(string name, Transform parent, Sprite sprite, float x, float y, float w, float h)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        M(img.rectTransform, x, y, w, h);
        return img;
    }

    // 발밑을 기준으로 움직이게 (자리는 그대로)
    static void PivotBottom(RectTransform rt)
    {
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition += new Vector2(0f, -rt.sizeDelta.y);
    }

    // 생각 말풍선으로 이어지는 작은 동그라미 (테두리 + 속)
    static void BubbleDot(Transform parent, float x, float y, float size)
    {
        var edge = MImage("BubbleDot", parent, circleSprite, x, y, size, size);
        edge.color = OutlineColor;
        var fill = NewImage("Fill", edge.transform, new Color32(255, 253, 246, 255));
        fill.sprite = circleSprite;
        fill.raycastTarget = false;
        Stretch(fill.rectTransform, 10);
    }

    static SimpleAnim Anim(GameObject go, SimpleAnim.Kind kind, float time, float delay, float distance, float angle = 0f)
    {
        var a = go.AddComponent<SimpleAnim>();
        a.kind = kind;
        a.popTime = time;
        a.delay = delay;
        a.distance = distance;
        a.angle = angle;
        return a;
    }

    // 화면 패널 : 켜질 때 서서히 나타나요
    static Transform NewPanel(string name, Transform parent)
    {
        var panel = NewEmpty(name, parent);
        panel.gameObject.AddComponent<CanvasGroup>();
        Anim(panel.gameObject, SimpleAnim.Kind.FadeIn, 0.35f, 0f, 0f);
        return panel;
    }

    // 화면 전체를 덮는 투명한 터치 영역
    static Button NewTap(string name, Transform parent)
    {
        var img = NewImage(name, parent, new Color(1f, 1f, 1f, 0f));
        Stretch(img.rectTransform);
        return img.gameObject.AddComponent<Button>();
    }

    // 대사 상자 : 모든 대사가 같은 크기 · 같은 자리예요
    static DialogueBox NewDialogueBox(Transform parent, Sprite face)
    {
        var card = NewCard("DialogueBox", parent, Vector2.zero, Vector2.zero);
        M((RectTransform)card, 12, 513, 336, 104);
        var faceImg = NewImage("Face", card, Color.white);
        faceImg.sprite = face;
        faceImg.preserveAspect = true;
        faceImg.raycastTarget = false;
        Anchor(faceImg.rectTransform, 0f, 0.5f, new Vector2(39, 0), new Vector2(186, 168));
        var line = NewText("Line", card, "", 48, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        line.font = talkFont;
        line.fontStyle = FontStyles.Normal;
        line.lineSpacing = 6;
        line.rectTransform.anchorMin = Vector2.zero;
        line.rectTransform.anchorMax = Vector2.one;
        line.rectTransform.offsetMin = new Vector2(255, 24);
        line.rectTransform.offsetMax = new Vector2(-90, -24);
        var arrow = NewImage("Arrow", card, Color.white);
        arrow.sprite = T("Common_NextArrow");
        arrow.raycastTarget = false;
        Anchor(arrow.rectTransform, 1f, 0f, new Vector2(-42, 36), new Vector2(54, 54));
        var box = card.gameObject.AddComponent<DialogueBox>();
        var so = new SerializedObject(box);
        Set(so, "face", faceImg);
        Set(so, "text", line);
        Set(so, "arrow", arrow);
        so.ApplyModifiedPropertiesWithoutUndo();
        return box;
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

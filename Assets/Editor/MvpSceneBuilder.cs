using System.IO;
using System.Collections.Generic;
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
//
// 파일 나눔 : 데이터 파일 만들기는 MvpSceneBuilder.Data.cs, UI 만들기 도우미는 MvpSceneBuilder.UI.cs 에 있어요.
public static partial class MvpSceneBuilder
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
        CreateChapterFiles();
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
        ChapterData[] chapters = { LoadChapter("Chapter1_Picnic"), LoadChapter("Chapter2_Rain"), LoadChapter("Chapter3_Birthday"), LoadChapter("Chapter4_Snack"), LoadChapter("Chapter5_Curse") };
        StageData[] stages = chapters[0].items;     // 1챕터 물건 (방망이 · 가방 · 돗자리 · 김밥 · 친구)
        Sprite boyNormal = LoadSprite("Characters/Char_Boy_Normal.png");
        Sprite boyHappy = LoadSprite("Characters/Char_Boy_Happy.png");
        Sprite faceHappy = LoadSprite("Characters/Char_BoyFace_Happy.png");
        Sprite faceSurprised = LoadSprite("Characters/Char_BoyFace_Surprised.png");
        Sprite faceFriend = LoadSprite("Characters/Char_FriendFace_Normal.png");        // 5챕터 친구 귓속말 (임시 : 친구 원본에서 얼굴만)
        Sprite faceNormal = LoadSprite("Characters/Char_BoyFace_Normal.png");           // 물건 생김새를 차분히 설명할 때
        Sprite faceSad = LoadSprite("Characters/Char_BoyFace_Sad.png");                 // 틀렸을 때 · 간식이 떨어졌을 때
        Sprite dokkaebi = LoadSprite("Characters/Char_Dokkaebi_Normal.png");
        Sprite dokkaebiThink = LoadSprite("Characters/Char_Dokkaebi_Think.png");    // 판정 대기 : 쪽지를 들여다봐요
        Sprite dokkaebiSwing = LoadSprite("Characters/Char_Dokkaebi_Swing.png");    // 결과 : 방망이를 휘둘러요
        Sprite dokkaebiNoBat = LoadSprite("Characters/Char_Dokkaebi_NoBat.png");    // 방망이를 찾기 전 (아트팀 그림)
        Sprite faceDokkaebi = LoadSprite("Characters/Char_DokkaebiFace_Normal.png");
        Sprite faceDokkaebiProud = LoadSprite("Characters/Char_DokkaebiFace_Proud.png");
        Sprite faceDokkaebiTease = LoadSprite("Characters/Char_DokkaebiFace_Tease.png");
        Sprite picnic = LoadSprite("Background/BG_Picnic.png");                     // 이야기 종료 (아트팀 그림)
        Sprite friend = LoadSprite("Stage5_Friend/Friend_Original.png");
        Sprite snack = T("HUD_Candy");                                    // 사탕 : 쪽지를 건넬 때 (아트팀 그림)
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
        //   → 더 넓은 화면이면 양옆에 방 배경이 더 보여요. 화면 내용은 가운데 폭 1080 "Frame" 안에.
        //   → 더 긴 폰이면 Frame 이 화면 높이만큼 늘어나요. 위 줄은 맨 위, 대사 상자 · 버튼 · 아래 띠는 맨 아래에 붙고
        //     인물 · 카드처럼 가운데 있는 것은 가운데에 남아요 (PinToScreenEdges).
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
        var frameRt = frameGo.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0f);                          // 폭은 1080 그대로, 높이는 화면에 꽉
        frameRt.anchorMax = new Vector2(0.5f, 1f);
        frameRt.pivot = new Vector2(0.5f, 0.5f);
        frameRt.anchoredPosition = Vector2.zero;
        frameRt.sizeDelta = new Vector2(1080f, 0f);
        Transform canvas = frameGo.transform;

        // GameManager 오브젝트 (+ AI 판정 스크립트)
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        var judge = gmGo.AddComponent<OpenAIJudge>();

        // ── HUD (게임 중에만 보임) : 위 줄에 돋보기 · 사탕 · 일시정지, 오른쪽 긴 향 ──
        //   좌표는 시안 그림(2248x3952) 픽셀 그대로 적고, PlaceM 이 화면(1080x1899) 크기로 바꿔요.
        //   위 줄 칸은 화면 y 0 ~ 200 이고, 돋보기(몸통) · 사탕 · 하트 · 톱니는 보이는 그림의 가운데를 y 100 에 맞춰요 (11/23).
        //   그림마다 투명 여백이 달라서 상자 숫자는 서로 달라요. 로비 톱니(LobbySceneBuilder)도 같은 높이예요.
        var hud = NewEmpty("HUD", canvas);
        // 오른쪽 위 톱니 : 소리 설정 창 (시작 · 챕터 화면에도 같은 자리에 있어요)
        Button SettingsButton(Transform parent)
        {
            var img = TeamImage("SettingsButton", parent, T("HUD_SettingsButton"), 1880, 93, 2108, 325);
            img.preserveAspect = true;
            img.raycastTarget = true;
            return img.gameObject.AddComponent<Button>();
        }
        var pauseButton = SettingsButton(hud);
        var pauseImg = pauseButton.GetComponent<Image>();

        // 위 줄 : 돋보기 (오른쪽 아래에 쓸 수 있는 횟수) | 사탕 3개 | 일시정지. 사탕은 단계마다 새로 받아요.
        //   쓴 사탕은 왼쪽부터 흐려지고, 돋보기를 다 쓰면 숫자가 사라지고 흐려져요 (GameManager.UpdateSnacks)
        // 돋보기는 화면 모서리에서 조금 띄워요 (콩콩 뛰거나 도깨비불이 돌아도 화면 안에 있게)
        var magImg = TeamImage("MagnifierButton", hud, T("Magnifier_Icon"), 207, 84, 417, 332);
        magImg.preserveAspect = true;
        magImg.raycastTarget = true;
        var magnifierButton = magImg.gameObject.AddComponent<Button>();
        var magColors = magnifierButton.colors;
        magColors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.45f);    // 다 쓰면 흐리게
        magnifierButton.colors = magColors;
        // 쓸 수 있는 횟수 : 돋보기 오른쪽 아래의 코랄 동그라미 숫자 (떠올리기 숫자와 같은 모양)
        var magBadge = TeamImage("MagnifierBadge", hud, circleSprite, 349, 242, 459, 352);
        magBadge.color = OutlineColor;
        var magBadgeFill = NewImage("Fill", magBadge.transform, new Color32(242, 119, 107, 255));
        magBadgeFill.sprite = circleSprite;
        magBadgeFill.raycastTarget = false;
        Stretch(magBadgeFill.rectTransform, 5);
        var magBadgeText = NewText("Count", magBadge.transform, "1", 40, Vector2.zero, Vector2.zero, bold: true);
        Stretch(magBadgeText.rectTransform);
        magBadgeText.color = Color.white;
        // 무료 돋보기를 받으면 반짝 (켤 때마다 처음부터)
        var magSparkles = NewEmpty("MagnifierSparkles", hud);
        float[][] magSparkXY = { new float[] { 183, 100, 60, 0f }, new float[] { 407, 130, 44, 0.25f }, new float[] { 140, 255, 46, 0.5f } };
        foreach (float[] sp in magSparkXY)
        {
            var spark = TeamImage("Spark", magSparkles, circleSprite, sp[0], sp[1], sp[0] + sp[2], sp[1] + sp[2]);
            spark.color = new Color32(245, 201, 107, 255);
            Anim(spark.gameObject, SimpleAnim.Kind.Spark, 1.0f, sp[3], 0f);
        }
        const int PerStage = 3;         // 단계마다 받는 사탕 수 (GameManager 와 같게)
        const int SnackCount = 15;      // 이야기 종료에 모아 놓을 수 있는 최대 수 (5단계 x 3)
        var candyIcons = new Image[PerStage];
        var snackAnims = new SimpleAnim[PerStage];
        // 단계 첫 그리기 때 들어오는 움직임 : 사탕은 오른쪽 화면 밖에서 왼쪽으로 (오른쪽 것부터). fromX = 출발하는 시안 x (가운데)
        void SnackRow(string row, string item, Sprite sprite, float x0, float step, float w, Image[] icons, int animFrom,
                      SimpleAnim.Kind kind, float fromX, bool rightFirst)
        {
            var parent = NewEmpty(row, hud);
            for (int i = 0; i < PerStage; i++)
            {
                float x = x0 + i * step;
                icons[i] = TeamImage($"{item}_{i}", parent, sprite, x, 157, x + w, 349);
                icons[i].preserveAspect = true;
                float distance = Mathf.Abs(x + w * 0.5f - fromX) * K;
                float delay = (rightFirst ? PerStage - 1 - i : i) * 0.14f;
                snackAnims[animFrom + i] = Anim(icons[i].gameObject, kind, 0.45f, delay, distance);
                snackAnims[animFrom + i].enabled = false;
            }
        }
        SnackRow("CandyRow", "Candy", snack, 1190, 230, 200, candyIcons, 0, SimpleAnim.Kind.SlideLeft, 2360, rightFirst: true);
        // 시범 강조 (1단계 첫 그리기) : 돋보기 뒤에서 빛이 커졌다 작아지고, 아래에서 화살표가 콕콕 가리켜요 (GameManager 가 움직여요)
        var magHighlight = NewEmpty("MagnifierHighlight", hud);
        // 푸른 도깨비불 세 개가 돋보기 둘레를 빙글빙글 돌아요 (GameManager 가 돌려요). 가운데 = 돋보기 가운데
        var fireOrbit = new GameObject("Orbit", typeof(RectTransform));
        fireOrbit.transform.SetParent(magHighlight, false);
        PlaceM((RectTransform)fireOrbit.transform, 312, 208, 312, 208);
        var magFires = DokkaebiFires(fireOrbit.transform, 84f);
        magHighlight.SetAsLastSibling();
        magImg.transform.SetAsLastSibling();
        magBadge.transform.SetAsLastSibling();
        magSparkles.SetAsLastSibling();
        pauseImg.transform.SetAsLastSibling();  // 사탕은 톱니 버튼 뒤를 지나 나와요

        // 남은 시간 : 위 줄(돋보기 · 사탕 · 톱니)과 전체 화면 쪽지 사이의 촛대 게이지. 숫자 없이 게이지만.
        //   슬라이더처럼 막대(HUD_CandleGauge)가 왼쪽부터 채워져 있다가 오른쪽 끝부터 줄어들고,
        //   불꽃(HUD_CandleFlame)이 핸들처럼 막대 끝을 따라 왼쪽으로 가요 (IncenseBurn horizontal).
        //   아트팀 그림 한 장을 cut_candle_gauge.py 로 막대 · 불꽃 두 장으로 나눴어요.
        //   막대 뒤에는 같은 그림을 옅게 칠한 트랙(GaugeSlot)이 깔려 있어서 줄어든 자리가 보여요.
        //   원본 px : 막대 1915 x 78 (x 29 ~ 1944, 가운데 y 393) · 불꽃 218 x 269 (왼쪽 위 1937, 208), 불꽃 몸통 가운데 x 2048
        //   원본 전체 폭(막대 왼쪽 끝 29 ~ 불꽃 오른쪽 끝 2149)을 화면 x 50 ~ 1030 에 맞춰요.
        float gs = GaugeWidth / (2149f - 29f);
        var stickSize = new Vector2(1915f, 78f) * gs;
        var stickPos = new Vector2(50f + stickSize.x / 2f - 540f, GaugeMid - stickSize.y / 2f);
        var timerGroup = NewEmpty("TimerGroup", hud);
        var gaugeSlot = NewImage("GaugeSlot", timerGroup, new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 0.18f));
        gaugeSlot.sprite = T("HUD_CandleGauge");
        gaugeSlot.raycastTarget = false;
        Place(gaugeSlot.rectTransform, stickPos, stickSize);
        var incenseBar = NewImage("Incense", timerGroup, Color.white);
        incenseBar.sprite = T("HUD_CandleGauge");
        incenseBar.raycastTarget = false;
        incenseBar.type = Image.Type.Filled;
        incenseBar.fillMethod = Image.FillMethod.Horizontal;
        incenseBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        var stickRt = incenseBar.rectTransform;
        Place(stickRt, stickPos, stickSize);
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
        Anchor(incenseTip, 0f, 0.5f, Vector2.zero, Vector2.zero);           // 바의 줄어드는 끝 (불꽃 · 빛은 숨겨 둬요)
        var incenseAsh = IncenseFx("IncenseFx_Ash", incenseTip, T("Incense_Ash"), new Vector2(0, 40), new Vector2(70, 4), Color.white);
        var incenseGlow = IncenseFx("IncenseFx_Glow", incenseTip, T("Common_SoftPuff"), new Vector2(0, -35), new Vector2(150, 150), Color.clear);
        incenseGlow.enabled = false;                                    // 그냥 바라서 불꽃 · 빛은 없어요
        IncenseFx("IncenseFx_Ember", incenseTip, T("Incense_Ember"), new Vector2(0, -8), new Vector2(70, 38), Color.white).enabled = false;   // 향 불씨는 초에 안 어울려요
        // 불꽃 : 둥근 몸통 가운데가 막대 끝(가운데 줄)에서 오른쪽으로 떨어진 자리. 흔들림 · 커짐은 그 점을 중심으로 해요
        var incenseFlame = NewImage("IncenseFx_Flame", incenseTip, Color.white);
        incenseFlame.sprite = T("HUD_CandleFlame");
        incenseFlame.raycastTarget = false;
        var flameRt = incenseFlame.rectTransform;
        flameRt.anchorMin = flameRt.anchorMax = new Vector2(0.5f, 0.5f);
        flameRt.pivot = new Vector2((2048f - 1937f) / 218f, (208f + 269f - 393f) / 269f);
        flameRt.sizeDelta = new Vector2(218f, 269f) * gs;
        flameRt.anchoredPosition = new Vector2((2048f - 1944f) * gs, 0f);
        var incenseFallingAsh = IncenseFx("IncenseFx_FallingAsh", incenseTip, T("Incense_Ash"), Vector2.zero, new Vector2(34, 42), Color.white);
        incenseFallingAsh.enabled = false;
        var incenseSmoke = new Image[0];                                // 연기 없이

        // 시작 화면 · 챕터 선택은 로비 씬에 있어요 (LobbySceneBuilder)

        // ── 2. 대화 화면 : 캐릭터 + 대사 상자. 오프닝 그림과 건네기 쪽지도 여기에 있어요 ──
        var dialogue = NewPanel("DialoguePanel", canvas);
        // 2챕터 복선 : 시작 첫 대사 때 비 오는 창밖에 도깨비불이 잠깐 (방 배경 창 x 620~1080, y 280~826 안)
        var windowFire = MImage("WindowFire", dialogue, T("Magnifier_DokkaebiFire"), 296, 150, 26, 35);
        windowFire.color = new Color(1f, 1f, 1f, 0.85f);
        Anim(windowFire.gameObject, SimpleAnim.Kind.Float, 2.4f, 0f, 18f);
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
        // 쪽지 위에 사탕을 겹쳐 놓아요 (쪽지 + 간식을 같이 두면 도깨비가 와요)
        var openingIdea = NewEmpty("Idea", openingGroup);
        var ideaNote = NewPaper("Note", openingIdea, "Draw_Paper").transform;    // 그림판 종이를 줄여서
        M((RectTransform)ideaNote, 128, 100, 96, 113);
        Anim(ideaNote.gameObject, SimpleAnim.Kind.Pop, 0.45f, 0f, 0f);
        for (int i = 0; i < 2; i++)                                         // 쪽지에 적힌 줄
        {
            var noteLine = NewImage("Line_" + i, ideaNote, new Color32(205, 190, 170, 255));
            noteLine.raycastTarget = false;
            PIn(noteLine.rectTransform, 16, 22 + i * 20, i == 1 ? 44 : 64, 5);
        }
        var ideaSnack = MImage("Snack", openingIdea, snack, 150, 148, 66, 72);   // 쪽지 아래쪽 위에 올려요
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
        //   쪽지는 그림판 종이(Draw_Paper)를 줄인 것. 판정 대기 화면의 쪽지와 같은 모양이에요
        var giveNote = NewPaper("GiveNote", dialogue, "Draw_Paper").transform;
        M((RectTransform)giveNote, 95, 58, 163, 192);
        giveNote.gameObject.AddComponent<CanvasGroup>();
        var giveNoteAnim = Anim(giveNote.gameObject, SimpleAnim.Kind.Float, 3f, 0f, 30f, 2f);
        var giveNoteText = NewText("Text", giveNote, "", 42, Vector2.zero, Vector2.zero);
        PIn(giveNoteText.rectTransform, 7, 5, 149, 27);
        giveNoteText.enableAutoSizing = true; giveNoteText.fontSizeMin = 24; giveNoteText.fontSizeMax = 33;
        var giveNoteDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        giveNoteDrawing.transform.SetParent(giveNote, false);
        PIn(giveNoteDrawing.rectTransform, 9, 30, 145, 154);
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

        // ── 4. 그리기 화면 : 방 위에 쪽지 한 장 (PaperView : 두 손가락으로 키우기 · 돌리기 · 옮기기) ──
        //   아래 띠 : 붓 | ↶ ↷ … 다 그렸어!   붓을 누르면 띠 위에 꼬리 달린 팔레트가 한 줄로 (ToolBar · ColorMixer)
        var draw = NewPanel("DrawPanel", canvas);
        var desk = NewEmpty("Desk", draw);                                  // 쪽지가 움직이는 자리 (화면보다 넓게)
        Stretch((RectTransform)desk, -700f);
        var paperView = desk.gameObject.AddComponent<PaperView>();
        // 쪽지 : 화면 폭의 4분의 3쯤, 똑바로. 쪽지 질감 그림(Draw_Paper) 위에 그린 선만 투명한 층(Board)으로 올려요
        var paperImg = NewPaper("Paper", desk, "Draw_Paper");
        paperImg.raycastTarget = false;
        var paperRt = paperImg.rectTransform;
        paperRt.anchorMin = paperRt.anchorMax = new Vector2(0.5f, 0.5f);
        paperRt.pivot = new Vector2(0.5f, 0.5f);
        paperRt.sizeDelta = new Vector2(820f, 963f);                      // 그림판 796 x 939 = 512 : 604
        paperRt.anchoredPosition = new Vector2(0f, -10f);
        var boardGo = new GameObject("Board", typeof(RectTransform), typeof(RawImage), typeof(DrawingBoard));
        boardGo.transform.SetParent(paperRt, false);
        Stretch(boardGo.GetComponent<RectTransform>(), 12f);
        var board = boardGo.GetComponent<DrawingBoard>();
        var drawTitle = NewText("Guide", paperRt, "", 46, Vector2.zero, Vector2.zero, bold: true);
        drawTitle.rectTransform.anchorMin = drawTitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        drawTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        drawTitle.rectTransform.anchoredPosition = new Vector2(0f, -26f);
        drawTitle.rectTransform.sizeDelta = new Vector2(740f, 64f);
        drawTitle.enableAutoSizing = true; drawTitle.fontSizeMin = 30; drawTitle.fontSizeMax = 46;   // 긴 글도 한 줄에
        drawTitle.raycastTarget = false;                                    // 글자 밑에도 그릴 수 있게
        // 전체 화면 : 쪽지는 처음부터 똑바로 서서 FullArea(위 HUD 와 아래 띠 사이)를 가득 채워요.
        //   키우기 · 돌리기 · 옮기기로 흐트러지면 오른쪽 위에 버튼이 뜨고, 누르면 다시 똑바로 가득 (PaperView)
        var fullArea = (RectTransform)NewEmpty("FullArea", draw);             // 긴 폰이면 위아래로 함께 늘어나요
        fullArea.offsetMax = new Vector2(0f, -300f);                        // 위 줄 아래부터
        fullArea.offsetMin = new Vector2(0f, FrameHeight - 1660f);          // 아래 띠 위까지
        var fitImg = NewImage("FitButton", draw, Color.white);
        fitImg.sprite = T("Draw_FitIcon");
        fitImg.preserveAspect = true;
        Place(fitImg.rectTransform, new Vector2(470f, 196f), new Vector2(84f, 84f));
        var fitButton = fitImg.gameObject.AddComponent<Button>();

        // 아래 띠 : 화면 폭보다 넓게 깔아 긴 폰에서도 아래 끝까지 덮어요. 띠 위를 눌러도 쪽지에 그려지지 않아요
        var band = NewImage("ToolBar", draw, new Color32(251, 240, 220, 255));
        Place(band.rectTransform, new Vector2(0f, 1660f), new Vector2(2480f, 640f));
        var bandLine = NewImage("Line", band.transform, OutlineColor);
        bandLine.raycastTarget = false;
        Place(bandLine.rectTransform, Vector2.zero, new Vector2(2480f, 7f));
        var toolBar = band.gameObject.AddComponent<ToolBar>();
        var mixer = band.gameObject.AddComponent<ColorMixer>();
        Button IconButton(string name, Transform parent, Sprite sprite, Vector2 pos, float size)
        {
            var img = NewImage(name, parent, Color.white);
            img.sprite = sprite;
            img.preserveAspect = true;
            Place(img.rectTransform, pos, new Vector2(size, size));
            var b = img.gameObject.AddComponent<Button>();
            var c = b.colors;
            c.disabledColor = new Color(1f, 1f, 1f, 0.3f);                 // 할 게 없으면 흐리게
            b.colors = c;
            return b;
        }
        // 5단계 색 섞기 시범 : 붓을 눌러 볼 차례면 도깨비불 셋이 붓 둘레를 돌아요 (돋보기 시범과 같아요)
        var brushHighlight = NewEmpty("BrushHighlight", band.transform);
        var brushOrbit = new GameObject("Orbit", typeof(RectTransform)).GetComponent<RectTransform>();
        brushOrbit.SetParent(brushHighlight, false);
        Place(brushOrbit, new Vector2(-420f, 112f), Vector2.zero);
        brushOrbit.pivot = new Vector2(0.5f, 0.5f);
        var brushFires = DokkaebiFires(brushOrbit, 70f);
        // 붓 : 붓 끝(Tip)을 지금 색으로 칠해요. 물방울은 TipPoint 에 닿아요
        var brushHolder = new GameObject("Brush", typeof(RectTransform)).GetComponent<RectTransform>();
        brushHolder.SetParent(band.transform, false);
        Place(brushHolder, new Vector2(-420f, 10f), new Vector2(205f, 205f));
        brushHolder.pivot = new Vector2(0.5f, 0.5f);                        // 첨벙할 때 가운데 기준으로 커졌다 돌아와요
        brushHolder.anchoredPosition -= new Vector2(0f, 102.5f);
        var brushHit = NewImage("Button", brushHolder, new Color(1f, 1f, 1f, 0f));
        Stretch(brushHit.rectTransform);
        var brushButton = brushHit.gameObject.AddComponent<Button>();
        var brushTip = NewImage("Tip", brushHolder, Color.black);
        brushTip.sprite = T("Draw_BrushTip");
        brushTip.raycastTarget = false;
        Stretch(brushTip.rectTransform);
        var brushBody = NewImage("Body", brushHolder, Color.white);
        brushBody.sprite = T("Draw_Brush");
        brushBody.raycastTarget = false;
        Stretch(brushBody.rectTransform);
        var tipPoint = new GameObject("TipPoint", typeof(RectTransform)).GetComponent<RectTransform>();
        tipPoint.SetParent(brushHolder, false);
        tipPoint.anchorMin = tipPoint.anchorMax = new Vector2(85f / 256f, 1f - 180f / 256f);   // 붓 그림의 붓 끝 가운데
        tipPoint.sizeDelta = Vector2.zero;
        tipPoint.anchoredPosition = Vector2.zero;
        var bandDivider = NewImage("Divider", band.transform, new Color32(215, 185, 142, 255));
        bandDivider.raycastTarget = false;
        Place(bandDivider.rectTransform, new Vector2(-292f, 45f), new Vector2(6f, 130f));
        var undoButton = IconButton("Undo", band.transform, T("Draw_UndoIcon"), new Vector2(-190f, 45f), 130f);
        var redoButton = IconButton("Redo", band.transform, T("Draw_RedoIcon"), new Vector2(-40f, 45f), 130f);
        // 다 그렸어! (사탕 1개를 건네요) : 한 줄
        var submitButton = NewButton("SubmitButton", band.transform, "다 그렸어!", new Vector2(300f, 32f), new Vector2(400f, 160f));
        var submitLabel = submitButton.GetComponentInChildren<TMP_Text>();
        submitLabel.fontSize = 62;
        submitLabel.color = Color.white;
        submitLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var submitColors = submitButton.colors;
        submitColors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.55f);   // 빈 쪽지면 흐리게
        submitButton.colors = submitColors;

        // 팔레트 : 띠 바로 위에 한 줄. 꼬리는 붓을 가리켜요. 버튼은 화면 폭에 맞춰 고르게 (빈 칸이 늘어나요)
        var paletteGroup = NewEmpty("Palette", band.transform);
        Color paletteColor = new Color32(255, 232, 196, 255);
        Image Tail(string name, Color color, float size)
        {
            var img = NewImage(name, paletteGroup, color);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-420f, 30f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return img;
        }
        Tail("TailEdge", OutlineColor, 50f);
        Tail("TailFill", paletteColor, 38f);
        var palCard = NewImage("Card", paletteGroup, Color.white);
        palCard.sprite = pillCream;
        palCard.type = Image.Type.Sliced;
        palCard.pixelsPerUnitMultiplier = pillCream.rect.height / 175f;
        palCard.color = paletteColor;
        Place(palCard.rectTransform, new Vector2(0f, -205f), new Vector2(1048f, 175f));
        var palRow = palCard.gameObject.AddComponent<HorizontalLayoutGroup>();
        palRow.padding = new RectOffset(44, 44, 0, 0);
        palRow.childAlignment = TextAnchor.MiddleCenter;
        palRow.childControlWidth = palRow.childControlHeight = true;
        palRow.childForceExpandWidth = palRow.childForceExpandHeight = false;
        Transform Slot(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(palCard.transform, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;
            return go.transform;
        }
        void Gap(string name)
        {
            var le = Slot(name, 0f, 10f).GetComponent<LayoutElement>();
            le.flexibleWidth = 1f; le.minWidth = 4f;
        }
        GameObject PaletteDivider(string name)
        {
            var slot = Slot(name, 6f, 110f);
            var line = NewImage("Line", slot, new Color32(205, 166, 112, 255));
            line.raycastTarget = false;
            Stretch(line.rectTransform);
            return slot.gameObject;
        }
        // 색 동그라미 : 고리(고른 표시) + 진갈색 테두리 + 색
        Button ColorDot(Transform slot, Color color, out GameObject frame)
        {
            var ring = NewImage("Frame", slot, Color.white);
            ring.sprite = T("Common_SelectRing");
            ring.raycastTarget = false;
            Stretch(ring.rectTransform, -10f);
            frame = ring.gameObject;
            var edge = NewImage("Edge", slot, OutlineColor);
            edge.sprite = circleSprite;
            Stretch(edge.rectTransform);
            var fill = NewImage("Button", edge.transform, color);
            fill.sprite = circleSprite;
            Stretch(fill.rectTransform, 7f);
            var b = fill.gameObject.AddComponent<Button>();
            var c = b.colors;
            c.disabledColor = Color.white;
            b.colors = c;
            return b;
        }
        var blackSlot = Slot("Color_검정", 104f, 104f);
        blackSlot.gameObject.AddComponent<CanvasGroup>();                   // 색 섞기 안내 중에는 흐리게 (ColorMixer)
        var blackButton = ColorDot(blackSlot, ToolBar.Palette[0], out GameObject blackFrame);
        Gap("Gap_1");
        var primaryDivider = PaletteDivider("Divider_1");
        Gap("Gap_2");
        string[] paintNames = { "빨강", "노랑", "파랑" };
        var primaryButtons = new GameObject[3];
        var primaryFrames = new GameObject[3];
        var materialButtons = new Button[3];
        var countTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            var slot = Slot("Color_" + paintNames[i], 104f, 104f);
            slot.gameObject.AddComponent<CanvasGroup>();                    // 못 넣는 물감은 통째로 흐리게 (ColorMixer)
            materialButtons[i] = ColorDot(slot, ToolBar.Palette[i + 1], out primaryFrames[i]);
            primaryButtons[i] = slot.gameObject;
            // 넣은 횟수 (섞는 단계)
            var badge = NewImage("Count", slot, OutlineColor);
            badge.sprite = circleSprite;
            badge.raycastTarget = false;
            Anchor(badge.rectTransform, 1f, 1f, new Vector2(12f, 12f), new Vector2(46f, 46f));
            countTexts[i] = NewText("Text", badge.transform, "0", 30, Vector2.zero, Vector2.zero, bold: true);
            Stretch(countTexts[i].rectTransform);
            countTexts[i].color = Color.white;
            if (i < 2) Gap("Gap_c" + i);
        }
        Gap("Gap_3");
        var paintUndoSlot = Slot("PaintUndo", 92f, 92f);
        var paintUndoEdge = NewImage("Edge", paintUndoSlot, OutlineColor);
        paintUndoEdge.sprite = circleSprite;
        Stretch(paintUndoEdge.rectTransform);
        var paintUndoFill = NewImage("Button", paintUndoEdge.transform, new Color32(255, 253, 246, 255));
        paintUndoFill.sprite = circleSprite;
        Stretch(paintUndoFill.rectTransform, 6f);
        var paintUndoButton = paintUndoFill.gameObject.AddComponent<Button>();
        var paintUndoColors = paintUndoButton.colors;
        paintUndoColors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        paintUndoButton.colors = paintUndoColors;
        var paintUndoIcon = NewImage("Icon", paintUndoFill.transform, Color.white);
        paintUndoIcon.sprite = T("Draw_UndoIcon");
        paintUndoIcon.preserveAspect = true;
        paintUndoIcon.raycastTarget = false;
        Stretch(paintUndoIcon.rectTransform, 16f);
        Gap("Gap_4");
        PaletteDivider("Divider_2");
        Gap("Gap_5");
        // 굵기 점 세 개 : 고른 점은 지금 색 + 고리
        var sizeDots = new Image[3];
        var sizeFrames = new GameObject[3];
        var sizeButtons = new Button[3];
        float[] dotSizes = { 24f, 42f, 64f };
        for (int i = 0; i < 3; i++)
        {
            var slot = Slot("Size_" + i, 96f, 96f);
            var ring = NewImage("Frame", slot, Color.white);
            ring.sprite = T("Common_SelectRing");
            ring.raycastTarget = false;
            Stretch(ring.rectTransform, -4f);
            sizeFrames[i] = ring.gameObject;
            var hit = NewImage("Button", slot, new Color(1f, 1f, 1f, 0f));
            Stretch(hit.rectTransform);
            sizeButtons[i] = hit.gameObject.AddComponent<Button>();
            var dot = NewImage("Dot", slot, Color.black);
            dot.sprite = circleSprite;
            dot.raycastTarget = false;
            Anchor(dot.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2(dotSizes[i], dotSizes[i]));
            sizeDots[i] = dot;
        }

        // 5단계 색 섞기 안내 : 넣을 물감 둘레를 도깨비불 셋이 돌아요 (GameManager 가 그 물감 자리로 옮겨요). 팔레트와 같이 보였다 숨어요
        var paintHighlight = NewEmpty("PaintHighlight", paletteGroup);
        var paintOrbit = new GameObject("Orbit", typeof(RectTransform)).GetComponent<RectTransform>();
        paintOrbit.SetParent(paintHighlight, false);
        paintOrbit.anchorMin = paintOrbit.anchorMax = paintOrbit.pivot = new Vector2(0.5f, 0.5f);
        paintOrbit.sizeDelta = Vector2.zero;
        var paintFires = DokkaebiFires(paintOrbit, 46f);

        // 돋보기 시범 (1단계 첫 그리기) : 그림판을 살짝 덮고 도깨비가 "돋보기를 눌러 봐" 라고 말해요.
        //   HUD 가 이 덮개보다 위에 있어서 돋보기는 또렷하게 보이고 누를 수 있어요.
        var peekGuide = NewEmpty("PeekGuide", draw);
        var peekGuideDim = NewImage("Dim", peekGuide, new Color(40f / 255f, 24f / 255f, 10f / 255f, 0.3f));
        peekGuideDim.raycastTarget = false;
        Stretch(peekGuideDim.rectTransform);
        var peekGuideBox = NewDialogueBox(peekGuide, faceDokkaebi);
        var peekGuideTap = NewTap("TapButton", peekGuide);
        // 챕터 안내 (1챕터 펜 · 3챕터 색) : 도깨비불 셋이 누를 곳(쪽지 · 되돌리기 · 다 그렸어! · 붓) 둘레를 돌아요.
        //   GameManager 가 대상 자리로 옮기고 크기에 맞춰 돌려요. 누르기는 통과시켜요
        var guideHighlight = (RectTransform)NewEmpty("GuideHighlight", draw);
        guideHighlight.anchorMin = guideHighlight.anchorMax = guideHighlight.pivot = new Vector2(0.5f, 0.5f);
        guideHighlight.sizeDelta = Vector2.zero;
        var guideOrbit = new GameObject("Orbit", typeof(RectTransform)).GetComponent<RectTransform>();
        guideOrbit.SetParent(guideHighlight, false);
        guideOrbit.anchorMin = guideOrbit.anchorMax = guideOrbit.pivot = new Vector2(0.5f, 0.5f);
        guideOrbit.sizeDelta = Vector2.zero;
        var guideFires = DokkaebiFires(guideOrbit, 84f);

        // 둘러보기 : 화면을 어둡게 덮고, 렌즈 안에만 그림판 자리에 놓인 원본이 보여요 (MagnifierPeek)
        var peekGo = NewImage("MagnifierPeek", draw, new Color(0f, 0f, 0f, 0.4f));    // 덮개가 누르기를 모두 받아서 그릴 수 없어요
        Stretch(peekGo.rectTransform);
        var peek = peekGo.gameObject.AddComponent<MagnifierPeek>();
        var peekArea = (RectTransform)boardGo.transform;                       // 그림판 (쪽지를 돌리고 키워도 따라가요)
        // 돋보기 그림(362x484)의 렌즈 구멍 : 가운데 (169, 174), 지름 약 230
        const float lensHole = 400f, frameW = 362f, frameH = 484f, holeX = 169f, holeY = 174f, holeD = 230f;
        float lensScale = lensHole / holeD;
        var lens = new GameObject("Lens", typeof(RectTransform)).GetComponent<RectTransform>();
        lens.SetParent(peekGo.transform, false);
        lens.sizeDelta = new Vector2(frameW, frameH) * lensScale;
        lens.pivot = new Vector2(holeX / frameW, 1f - holeY / frameH);           // 기준점 = 렌즈 구멍 가운데
        var lensMask = NewImage("Hole", lens, Color.white);
        lensMask.sprite = circleSprite;
        lensMask.raycastTarget = false;
        lensMask.rectTransform.anchorMin = lensMask.rectTransform.anchorMax = lens.pivot;
        lensMask.rectTransform.anchoredPosition = Vector2.zero;
        lensMask.rectTransform.sizeDelta = Vector2.one * (lensHole + 12f);      // 테두리 밑까지
        lensMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var peekView = NewImage("View", lensMask.transform, new Color32(250, 237, 219, 255));   // 쪽지 종이(Draw_Paper) 색
        peekView.raycastTarget = false;
        peekView.rectTransform.sizeDelta = new Vector2(796f, 939f);
        var peekOriginal = NewImage("Original", peekView.transform, Color.white);
        peekOriginal.preserveAspect = true;
        peekOriginal.raycastTarget = false;
        Stretch(peekOriginal.rectTransform, 70);
        var lensFrame = NewImage("Frame", lens, Color.white);
        lensFrame.sprite = T("Magnifier_Frame");
        lensFrame.raycastTarget = false;
        Stretch(lensFrame.rectTransform);
        var lensAnim = Anim(lens.gameObject, SimpleAnim.Kind.Pop, 0.3f, 0f, 0f);
        // 남은 시간 : 테두리의 크림색 띠를 오린 고리를 테두리에 꼭 맞게 겹쳐 호박색으로 칠하고, 시계 방향으로 줄여요
        //   (MagnifierPeek. 고리 그림은 make_candy_jar.py 가 Magnifier_Frame 에서 오려요 : 렌즈 가운데 기준 340 x 340)
        var timeRing = NewImage("TimeRing", lens, Color.white);
        timeRing.sprite = T("Magnifier_TimeRing");
        timeRing.type = Image.Type.Filled;
        timeRing.fillMethod = Image.FillMethod.Radial360;
        timeRing.fillOrigin = (int)Image.Origin360.Top;
        timeRing.fillClockwise = false;     // 12시에서 시작해 시계 방향으로 비어 가요
        timeRing.raycastTarget = false;
        timeRing.rectTransform.anchorMin = timeRing.rectTransform.anchorMax = lens.pivot;
        timeRing.rectTransform.anchoredPosition = Vector2.zero;
        timeRing.rectTransform.sizeDelta = Vector2.one * (340f * lensScale);     // 돋보기 그림과 같은 비율
        var pso = new SerializedObject(peek);
        Set(pso, "area", peekArea);
        Set(pso, "lens", lens);
        Set(pso, "view", peekView.rectTransform);
        Set(pso, "original", peekOriginal);
        Set(pso, "lensAnim", lensAnim);
        Set(pso, "timeRing", timeRing);
        pso.ApplyModifiedPropertiesWithoutUndo();

        // 색 섞기 시범 (5단계 첫 그리기) : 대사 상자는 다른 대사와 같은 자리(아래). 시범 동안만 팔레트를 줄여 그림판과 대사 상자 사이에 넣어서
        //   아무것도 가리지 않아요 (GameManager.StartMixDemo). 화면 전체를 덮는 터치로 시범 중에 팔레트를 못 누르게 해요

        // ── 5. 판정 대기 : 내 그림은 떠 있고, 도깨비는 가운데서 좁게 오가요 ──
        var judgeP = NewPanel("JudgePanel", canvas);
        var judgeDrawingBack = MImage("MyDrawingBack", judgeP, T("Draw_Paper"), 105, 90, 150, 177);   // 그림판 종이를 줄여서
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
        // 5챕터 : 내 쪽지가 빛나다 번쩍 하며 물건으로 바뀌어요 (GameManager.NoteToItem). 물건 자리 가운데에
        var curseNoteGo = NewEmpty("CurseNote", result);
        M((RectTransform)curseNoteGo, 170, 286, 140, 165);
        var curseNoteGlowImg = NewImage("Glow", curseNoteGo, new Color32(255, 214, 102, 210));
        curseNoteGlowImg.sprite = T("Common_SoftPuff");
        curseNoteGlowImg.raycastTarget = false;
        Stretch(curseNoteGlowImg.rectTransform, -30f);
        var curseNotePaper = NewPaper("Paper", curseNoteGo, "Draw_Paper");
        curseNotePaper.raycastTarget = false;
        Stretch(curseNotePaper.rectTransform);
        var curseNoteDrawingGo = new GameObject("Drawing", typeof(RectTransform), typeof(RawImage));
        curseNoteDrawingGo.transform.SetParent(curseNoteGo, false);
        var curseNoteDrawingImg = curseNoteDrawingGo.GetComponent<RawImage>();
        curseNoteDrawingImg.raycastTarget = false;
        Stretch(curseNoteDrawingImg.rectTransform, 8f);
        curseNoteGo.gameObject.SetActive(false);
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
        // 무료 돋보기 : 도깨비 손에 쥐어져 있다가 맨 위 돋보기 자리로 날아가요 (GameManager.GiftFly 가 움직여요)
        //   기준점은 손잡이 끝 (손에 쥐는 곳)
        var giftMagnifier = MImage("GiftMagnifier", result, T("Magnifier_Icon"), 0, 0, 60, 70);
        Anchor(giftMagnifier.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2(130, 152));
        giftMagnifier.rectTransform.pivot = new Vector2(0.8f, 0.12f);

        // ── 7. 채점표 : 시험지처럼 번호 위에 동그라미 · 빗금 ──
        var compare = NewPanel("ComparePanel", canvas);
        //   바깥 판은 아트팀 그림(Paper_Score). 위 두 그림은 둘 다 칸 없이 쪽지 위에 누끼로 (왼쪽 내 그림, 오른쪽 원본)
        var paper = NewPaper("Paper", compare, "Paper_Score").transform;
        M((RectTransform)paper, 18, 62, 324, 520);
        var mineCard = NewEmpty("MyCard", paper);
        PIn((RectTransform)mineCard, 14, 14, 138, 150);
        var compareMine = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        compareMine.transform.SetParent(mineCard, false);
        Stretch(compareMine.rectTransform, 12);
        compareMine.raycastTarget = false;
        var origCard = NewEmpty("OriginalCard", paper);     // 흰 카드 · 테두리 없이 (원본 그림은 이미 배경이 투명해요)
        PIn((RectTransform)origCard, 164, 14, 138, 150);
        var compareOriginal = NewImage("Original", origCard, Color.white);
        Stretch(compareOriginal.rectTransform, 12);         // 내 그림과 같은 여백
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

        // ── 8. 이야기 종료 : 소풍 장면. 캐릭터는 땅에 서 있고, 돗자리 옆 사탕 통에 남은 사탕 ──
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
        var endingJar = NewCandyJar(ending, snack, 288, 500, 62, 80, 1.9f);     // 돗자리 오른쪽 아래 (옛 C-25 자리)
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
        M((RectTransform)endingBoxComp.transform, 12, 24, 336, 104);     // 하늘 쪽 위에 : 돗자리 · 김밥 · 사탕 통을 가리지 않게
        var endingTapButton = NewTap("TapButton", ending);

        // ── 8-2. 2~4챕터 끝 장면 : 방에 찾은 물건 3개가 놓이고, 화면을 누르면 도깨비 인사 (4챕터는 도깨비가 떠나고 주인공 독백) ──
        //   자리 (게임 좌표) : 주인공 18, 700, 300, 458 · 도깨비 726, 826, 340, 332 · 물건 240 x 240 (120 · 420 · 720, 1250) · 대사 상자 위
        var roomEnd = NewPanel("RoomEndPanel", canvas);
        var roomEndBoyImg = MImage("Boy", roomEnd, boyHappy, 6, 233.3f, 100, 152.7f);
        PivotBottom(roomEndBoyImg.rectTransform);
        Anim(roomEndBoyImg.gameObject, SimpleAnim.Kind.FootSway, 2.6f, 0f, 0f, 1.5f);
        var roomEndDokImg = MImage("Dokkaebi", roomEnd, dokkaebi, 242, 275.3f, 113.3f, 110.7f);
        var roomEndDokAnim = Anim(roomEndDokImg.gameObject, SimpleAnim.Kind.Gentle, 0.8f, 0f, 15f);
        var roomEndItems = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            roomEndItems[i] = MImage("Item_" + i, roomEnd, null, 40 + 100 * i, 416.7f, 80, 80);
            Anim(roomEndItems[i].gameObject, SimpleAnim.Kind.Land, 0.7f, 0.2f + 0.5f * i, 1080f);
        }
        var roomEndJar = NewCandyJar(roomEnd, snack, 150, 330, 50, 64, 1.6f);    // 주인공과 도깨비 사이 바닥
        // 4챕터 독백 : 밤 창 안에서 빨간 눈이 지켜봐요 (방 배경 창 x 620~1080, y 280~826 안)
        var roomEndEyes = MImage("RedEyes", roomEnd, LoadSprite("Chapter4/Room_RedEyes.png"), 263, 200, 70, 24);     // 위 대사 상자(아래 끝 y 504) 밑
        Anim(roomEndEyes.gameObject, SimpleAnim.Kind.FadeIn, 1.2f, 0.3f, 0f);
        var roomEndBoxComp = NewDialogueBox(roomEnd, faceSurprised);
        M((RectTransform)roomEndBoxComp.transform, 12, 64, 336, 104);          // 위쪽 (36, 192) : 인물 · 물건을 가리지 않게
        var roomEndTapButton = NewTap("TapButton", roomEnd);
        UnityEventTools.AddPersistentListener(roomEndTapButton.onClick, gm.RoomEndTap);

        // ── 5챕터 (도깨비의 저주) 화면들 ──
        //   그림은 Sprites/Chapter5 에 있어요. 아직 없는 그림은 있는 그림이나 단색으로 대신해요.
        //   아래 좌표는 1080 x 1899 화면 기준 (왼쪽 위 x, y, 폭, 높이) → M() 은 3으로 나눈 단위라서 Img3 가 나눠 줘요.
        Sprite Chap5(string file, Sprite fallback)
        {
            Sprite s = LoadSprite("Chapter5/" + file);
            return s != null ? s : fallback;
        }
        Image Img3(string name, Transform parent, Sprite sprite, float x, float y, float w, float h)
            => MImage(name, parent, sprite, x / 3f, y / 3f, w / 3f, h / 3f);
        void Put3(RectTransform rt, float x, float y, float w, float h) => M(rt, x / 3f, y / 3f, w / 3f, h / 3f);

        Sprite friendSpirit = Chap5("Char_Friend_Spirit.png", friend);
        Sprite dokkaebiDark = Chap5("Char_Dokkaebi_Dark.png", dokkaebiNoBat);
        Sprite dokkaebiDarkBat = Chap5("Char_Dokkaebi_DarkBat.png", dokkaebi);
        Sprite forestBright = Chap5("BG_ForestBright.png", room);
        Sprite forestDark = Chap5("BG_ForestDark.png", room);

        // 위 줄 하트 3개 (목숨) : 돋보기와 사탕 사이. 5챕터에서만 켜요
        var heartGroup = NewEmpty("HeartGroup", hud);
        Sprite heartSprite = LoadSprite("Chapter5/HUD_Heart.png");
        var heartIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            heartIcons[i] = Img3("Heart_" + i, heartGroup, heartSprite != null ? heartSprite : circleSprite, 260 + i * 100, 56, 88, 88);
            if (heartSprite == null) heartIcons[i].color = new Color32(224, 64, 64, 255);   // 그림이 없으면 빨간 동그라미
        }

        // 물건 고르기 화면 : 카드 두 장 (왼쪽 0 · 오른쪽 1) + 아래에 소년 · 친구 · 어두운 도깨비 (글자 없이)
        var choose = NewPanel("ChoosePanel", canvas);
        var chooseImages = new Image[2];
        float[] chooseX = { 54, 558 };
        for (int i = 0; i < 2; i++)
        {
            // 카드는 다른 화면의 쪽지와 같은 종이 질감(Draw_Paper) (B9)
            var chooseCard = NewPaper("ChooseCard_" + i, choose, "Draw_Paper").transform;
            Put3((RectTransform)chooseCard, chooseX[i], 240, 468, 460);
            var chooseButton = chooseCard.gameObject.AddComponent<Button>();
            var chooseItem = NewImage("Item", chooseCard, Color.white);
            chooseItem.preserveAspect = true;
            chooseItem.raycastTarget = false;
            Stretch(chooseItem.rectTransform, 30f);
            chooseImages[i] = chooseItem;
            UnityEventTools.AddIntPersistentListener(chooseButton.onClick, gm.ChooseItem, i);
        }
        Img3("Boy", choose, boyNormal, 24, 838, 360, 548);
        Img3("Friend", choose, friend, 408, 1086, 300, 300);
        Img3("Dokkaebi", choose, dokkaebiDark, 732, 1070, 324, 316);

        // 대사 화면의 친구 (5챕터에서 켜요)
        var dialogueFriend = Img3("Friend", dialogue, friend, 408, 1086, 300, 300);
        dialogueFriend.gameObject.SetActive(false);

        // 놀래키기 : 화면을 어둡게 덮고 무서운 얼굴. 위 줄(HUD) 아래, 눌러도 아무 일 없어요
        var scareOverlay = NewEmpty("ScareOverlay", canvas);
        var scareDim = NewImage("Dim", scareOverlay, new Color(0f, 0f, 0f, 0.85f));
        scareDim.raycastTarget = false;
        Stretch(scareDim.rectTransform);
        scareDim.rectTransform.offsetMin = new Vector2(-2000f, 0f);          // 넓은 화면에서도 옆까지 덮게
        scareDim.rectTransform.offsetMax = new Vector2(2000f, 0f);
        Sprite scareSprite = LoadSprite("Chapter5/Scare_Face.png");
        var scareFace = Img3("Face", scareOverlay, scareSprite, 0, 400, 1080, 1080);
        if (scareSprite == null) scareFace.enabled = false;

        // 그리기 방해 : 쪽지 위에 붉은 막 + 도깨비불 + 낙서. 전부 클릭을 통과시켜서 그리는 데 방해가 안 돼요
        var jamOverlay = NewEmpty("JamOverlay", draw);
        Put3((RectTransform)jamOverlay, 20, 370, 1040, 1222);
        var jamTint = NewImage("Tint", jamOverlay, new Color(0.55f, 0.08f, 0.08f, 0.25f));
        jamTint.raycastTarget = false;
        Stretch(jamTint.rectTransform);
        var jamFires = new RectTransform[3];
        float[][] jamFirePos = { new float[] { 120, 160 }, new float[] { 640, 520 }, new float[] { 300, 880 } };   // 영역 안 좌표 (1080 기준)
        for (int i = 0; i < 3; i++)
        {
            var jamFire = NewImage("Fire_" + i, jamOverlay, new Color32(255, 90, 80, 230));
            jamFire.sprite = T("Magnifier_DokkaebiFire");
            jamFire.preserveAspect = true;
            jamFire.raycastTarget = false;
            PIn(jamFire.rectTransform, jamFirePos[i][0] / 3f, jamFirePos[i][1] / 3f, 100f / 3f, 133f / 3f);
            jamFires[i] = jamFire.rectTransform;
        }
        float[][] scribbles = { new float[] { 60, 300, 520, 14, -12 }, new float[] { 420, 700, 480, 12, 9 }, new float[] { 100, 1020, 560, 14, -6 }, new float[] { 520, 240, 380, 12, 18 } };    // 마지막 낙서는 쪽지 글 아래
        foreach (float[] sc in scribbles)
        {
            var scribble = NewImage("Scribble", jamOverlay, new Color32(200, 40, 40, 200));
            scribble.raycastTarget = false;
            PIn(scribble.rectTransform, sc[0] / 3f, sc[1] / 3f, sc[2] / 3f, sc[3] / 3f);
            scribble.rectTransform.localEulerAngles = new Vector3(0f, 0f, sc[4]);
        }
        var jamScribbles = jamOverlay.GetComponentsInChildren<Image>(true);
        jamScribbles = System.Array.FindAll(jamScribbles, im => im.name == "Scribble");
        // 흑화 도깨비가 쪽지 곳곳에서 불쑥 나타나 흔들거려요 (GameManager 가 자리 · 크기를 움직여요). 클릭은 통과해요
        var jamPeek = NewImage("Peek", jamOverlay, Color.white);
        jamPeek.sprite = dokkaebiDark;
        jamPeek.preserveAspect = true;
        jamPeek.raycastTarget = false;
        PIn(jamPeek.rectTransform, 0, 0, 100, 98);
        jamPeek.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        jamPeek.rectTransform.anchoredPosition = new Vector2(820f, -240f);     // GameManager.JamSpots 첫 자리. 처음엔 숨어 있어요
        jamPeek.rectTransform.localScale = Vector3.zero;

        // 5챕터 판정 대기 : 찾아다니는 도깨비 대신, 빛나는 쪽지와 움찔거리는 흑화 도깨비
        var judgeCurse = NewEmpty("CurseJudge", judgeP);
        judgeCurse.SetSiblingIndex(0);                       // 내 쪽지 뒤에서 빛나요
        var judgeGlow = MImage("Glow", judgeCurse, T("Common_SoftPuff"), 60, 50, 240, 260);
        judgeGlow.preserveAspect = false;
        judgeGlow.color = new Color32(255, 214, 102, 210);
        var judgeCurseDok = MImage("Dokkaebi", judgeCurse, dokkaebiDark, 105, 330, 150, 146);
        PivotBottom(judgeCurseDok.rectTransform);
        Anim(judgeCurseDok.gameObject, SimpleAnim.Kind.Sway, 0.5f, 0f, 0f, 6f);
        judgeCurse.gameObject.SetActive(false);

        // 5챕터 하트 0 : 결과 화면에서 친구가 쓰러지고, 영혼이 빠져나가 도깨비에게 가요 (GameManager.SoulLeaves)
        var soulFriend = MImage("SoulFriend", result, friend, 190, 330, 130, 146);
        PivotBottom(soulFriend.rectTransform);
        soulFriend.gameObject.SetActive(false);
        var soulSpirit = MImage("Soul", result, friendSpirit, 190, 330, 130, 146);
        PivotBottom(soulSpirit.rectTransform);
        soulSpirit.gameObject.SetActive(false);

        // 5챕터 끝 화면 : 소년 · 친구 · 도깨비, 어두운 그림자와 방망이, 위쪽 대사 상자. 아무 데나 누르면 넘어가요
        var curseEnd = NewPanel("CurseEndPanel", canvas);
        var curseSilhouetteImg = NewImage("Silhouette", curseEnd, new Color32(10, 8, 16, 230));
        curseSilhouetteImg.raycastTarget = false;
        Sprite silhouetteSprite = LoadSprite("Chapter5/GameOver_Silhouette.png");
        if (silhouetteSprite != null) { curseSilhouetteImg.sprite = silhouetteSprite; curseSilhouetteImg.color = Color.white; curseSilhouetteImg.preserveAspect = true; }
        Put3(curseSilhouetteImg.rectTransform, 0, 520, 1080, 700);           // 도깨비 등 뒤에 쌓여 있어요 (기획서 4쪽)
        var curseEndSoulImg = Img3("PileSoul", curseEnd, friendSpirit, 140, 560, 150, 170);   // 더미 위의 친구 영혼 (희미하게, 대사 상자 아래)
        curseEndSoulImg.color = new Color(1f, 1f, 1f, 0.55f);
        curseEndSoulImg.raycastTarget = false;
        Anim(curseEndSoulImg.gameObject, SimpleAnim.Kind.Float, 2.4f, 0f, 12f);
        var curseEndBoy = Img3("Boy", curseEnd, boyNormal, 24, 838, 360, 548);
        var curseEndFriend = Img3("Friend", curseEnd, friend, 408, 1086, 300, 300);
        var curseEndDokkaebi = Img3("Dokkaebi", curseEnd, dokkaebiDark, 732, 1070, 324, 316);
        var curseEndBat = Img3("Bat", curseEnd, LoadSprite("Stage1_Bat/Bat_Original.png"), 600, 1300, 198, 270);
        var curseEndJar = NewCandyJar(curseEnd, snack, 330f / 3f, 1420f / 3f, 150f / 3f, 194f / 3f, 0.8f);   // 해피 : 남은 하트가 사탕으로
        var curseEndBoxComp = NewDialogueBox(curseEnd, faceSurprised);
        M((RectTransform)curseEndBoxComp.transform, 12, 64, 336, 104);          // 위쪽 (36, 192) : 인물을 가리지 않게
        var curseEndTapButton = NewTap("TapButton", curseEnd);
        UnityEventTools.AddPersistentListener(curseEndTapButton.onClick, gm.CurseEndTap);

        // ── 소리 설정 창 (오른쪽 위 톱니) : 어둡게 덮고 배경음 · 효과음 볼륨. 창 바깥을 누르거나 X 로 닫아요 ──
        //   제목 글 없이 줄마다 이름과 슬라이더만 있어요. 여는 동안 게임은 멈춰요 (GameManager.SetPaused)
        var pause = NewEmpty("SettingsPanel", root);
        var settingsDim = pause.gameObject.AddComponent<Image>();
        settingsDim.color = new Color(0f, 0f, 0f, 0.45f);                       // 뒤 화면을 못 누르게 덮음
        var settingsDimButton = pause.gameObject.AddComponent<Button>();     // 창 바깥을 누르면 닫혀요
        settingsDimButton.transition = Selectable.Transition.None;
        var pauseCard = NewCard("SettingsCard", pause, new Vector2(0, 640), new Vector2(880, 520));
        var settingsClose = NewRoundTool("CloseButton", pauseCard, iconClose, Color.white, 110, out GameObject settingsCloseFrame);
        Object.DestroyImmediate(settingsCloseFrame);
        Place((RectTransform)settingsClose.transform.parent, new Vector2(370, 26), new Vector2(110, 110));
        // 왼쪽 위 나가기 버튼 (닫기 X 와 좌우 대칭) : 누르면 "나갈까요?" 확인으로 바뀌어요
        var settingsExit = NewRoundTool("ExitButton", pauseCard, T("Result_IconGiveUp"), Color.white, 110, out GameObject settingsExitFrame);
        Object.DestroyImmediate(settingsExitFrame);
        Place((RectTransform)settingsExit.transform.parent, new Vector2(-370, 26), new Vector2(110, 110));
        // 평소 모습 : 볼륨 줄 두 개 (카드와 같은 크기의 묶음 안에 넣어서, 숨기고 보이기 쉽게)
        var settingsMain = NewEmpty("SettingsMain", pauseCard);
        // 나가기 확인 : 취소 / 나가기 두 버튼 (처음엔 꺼져 있어요)
        var settingsConfirm = NewEmpty("SettingsConfirm", pauseCard);
        var confirmCancel = NewButton("CancelButton", settingsConfirm, "취소", new Vector2(-200, 290), new Vector2(330, 120), primary: false);
        var confirmExit = NewButton("ExitConfirmButton", settingsConfirm, "나가기", new Vector2(200, 290), new Vector2(330, 120), primary: true);
        Slider VolumeRow(string name, string label, float y)
        {
            var text = NewText(name + "Label", settingsMain, label, 60, new Vector2(-250, y), new Vector2(260, 110), bold: true);
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(settingsMain, false);
            Place((RectTransform)go.transform, new Vector2(110, y + 15), new Vector2(520, 80));
            var track = NewImage("Background", go.transform, Color.white);
            track.sprite = pillCream; track.type = Image.Type.Sliced; track.pixelsPerUnitMultiplier = 2f / K;
            Anchor(track.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2(520, 44));
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f); track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0f, 44f);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(go.transform, false);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-40f, 44f);
            var fill = NewImage("Fill", fillArea, Color.white);
            fill.sprite = pillCoral; fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 2f / K;
            fill.rectTransform.sizeDelta = new Vector2(40f, 0f);
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(go.transform, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(40f, 0f); handleArea.offsetMax = new Vector2(-40f, 0f);
            var handle = NewImage("Handle", handleArea, OutlineColor);
            handle.sprite = circleSprite;
            handle.rectTransform.sizeDelta = new Vector2(84f, 0f);
            var handleFill = NewImage("Fill", handle.transform, CreamColor);
            handleFill.sprite = circleSprite;
            handleFill.raycastTarget = false;
            Stretch(handleFill.rectTransform, 9);
            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            return slider;
        }
        var bgmSlider = VolumeRow("BgmSlider", "배경음", 170);
        var sfxSlider = VolumeRow("SfxSlider", "효과음", 330);

        // HUD 가 패널보다 위에 그려지도록 맨 뒤로 보내기 (Hierarchy 아래쪽일수록 앞에 그려짐). 일시정지 화면은 그보다 위
        peekGo.transform.SetAsLastSibling();    // 둘러보는 동안은 색 섞기 판까지 덮어요
        hud.SetAsLastSibling();
        pause.SetAsLastSibling();

        // ── 버튼 OnClick 연결 (Inspector 의 On Click() 목록에 보여요) ──
        UnityEventTools.AddPersistentListener(dialogueTapButton.onClick, gm.DialogueTap);
        UnityEventTools.AddPersistentListener(submitButton.onClick, gm.Submit);
        UnityEventTools.AddPersistentListener(magnifierButton.onClick, gm.MagnifierTap);
        UnityEventTools.AddPersistentListener(peekGuideTap.onClick, gm.PeekGuideTap);
        UnityEventTools.AddPersistentListener(retryButton.onClick, gm.Retry);
        UnityEventTools.AddPersistentListener(giveUpButton.onClick, gm.ShowCompare);
        UnityEventTools.AddPersistentListener(resultTapButton.onClick, gm.ResultTap);
        UnityEventTools.AddPersistentListener(compareTapButton.onClick, gm.NextStage);
        UnityEventTools.AddPersistentListener(endingTapButton.onClick, gm.EndingTap);
        foreach (Button b in new[] { pauseButton })
            UnityEventTools.AddPersistentListener(b.onClick, gm.ToggleSettings);
        UnityEventTools.AddPersistentListener(settingsClose.onClick, gm.CloseSettings);
        UnityEventTools.AddPersistentListener(settingsExit.onClick, gm.AskExit);
        UnityEventTools.AddPersistentListener(confirmCancel.onClick, gm.CancelExit);
        UnityEventTools.AddPersistentListener(confirmExit.onClick, gm.ExitToChapters);
        UnityEventTools.AddPersistentListener(settingsDimButton.onClick, gm.CloseSettings);
        UnityEventTools.AddPersistentListener(bgmSlider.onValueChanged, gm.SetBgmVolume);
        UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, gm.SetSfxVolume);

        UnityEventTools.AddPersistentListener(brushButton.onClick, toolBar.BrushTap);
        UnityEventTools.AddPersistentListener(undoButton.onClick, toolBar.Undo);
        UnityEventTools.AddPersistentListener(redoButton.onClick, toolBar.Redo);
        UnityEventTools.AddPersistentListener(blackButton.onClick, toolBar.BlackTap);
        for (int i = 0; i < 3; i++) UnityEventTools.AddIntPersistentListener(materialButtons[i].onClick, toolBar.PaintTap, i);
        UnityEventTools.AddPersistentListener(paintUndoButton.onClick, toolBar.PaintUndo);
        for (int i = 0; i < 3; i++) UnityEventTools.AddIntPersistentListener(sizeButtons[i].onClick, toolBar.SelectSize, i);
        UnityEventTools.AddPersistentListener(fitButton.onClick, paperView.FitTap);

        // ── Inspector 칸 채우기 ──
        // (private [SerializeField] 칸은 SerializedObject 로 이름을 찾아서 넣어요. 이름이 틀리면 바로 에러)
        var so = new SerializedObject(gm);
        SetArray(so, "chapters", chapters);
        Set(so, "board", board);
        Set(so, "toolBar", toolBar);
        Set(so, "paperView", paperView);
        Set(so, "judge", judge);
        Set(so, "hud", hud.gameObject);
        Set(so, "magnifierBadge", magBadge.gameObject);
        Set(so, "magnifierBadgeText", magBadgeText);
        Set(so, "magnifierSparkles", magSparkles.gameObject);
        Set(so, "peekGuide", peekGuide.gameObject);
        Set(so, "peekGuideBox", peekGuideBox);
        Set(so, "mixer", mixer);
        Set(so, "magnifierHighlight", magHighlight.gameObject);
        SetArray(so, "magnifierFires", magFires);
        Set(so, "giftMagnifier", giftMagnifier.rectTransform);
        SetArray(so, "candyIcons", candyIcons);
        SetArray(so, "snackAnims", snackAnims);
        Set(so, "timerGroup", timerGroup.gameObject);
        var burn = timerGroup.gameObject.AddComponent<IncenseBurn>();
        var burnSo = new SerializedObject(burn);
        Set(burnSo, "stick", incenseBar);
        Set(burnSo, "tip", incenseTip);
        Set(burnSo, "flame", incenseFlame.rectTransform);
        Set(burnSo, "glow", incenseGlow);
        Set(burnSo, "ash", incenseAsh.rectTransform);
        Set(burnSo, "fallingAsh", incenseFallingAsh);
        SetArray(burnSo, "smoke", incenseSmoke);
        burnSo.FindProperty("horizontal").boolValue = true;
        burnSo.FindProperty("baseRatio").floatValue = 0f;                 // 바는 끝까지 다 줄어요
        burnSo.FindProperty("maxAshHeight").floatValue = 0f;
        burnSo.FindProperty("ashDrops").intValue = 0;
        burnSo.ApplyModifiedPropertiesWithoutUndo();
        Set(so, "incense", burn);
        Set(so, "settingsPanel", pause.gameObject);
        Set(so, "bgmSlider", bgmSlider);
        Set(so, "sfxSlider", sfxSlider);
        Set(so, "backgroundImage", bg);
        Set(so, "startBackground", startBackground);
        Set(so, "gameBackground", room);
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
        Set(so, "faceBoyNormal", faceNormal);
        Set(so, "faceBoySad", faceSad);
        Set(so, "magnifierButton", magnifierButton);
        Set(so, "magnifierPeek", peek);
        Set(so, "faceDokkaebi", faceDokkaebi);
        Set(so, "faceFriend", faceFriend);
        Set(so, "faceDokkaebiProud", faceDokkaebiProud);
        Set(so, "faceDokkaebiTease", faceDokkaebiTease);
        Set(so, "dokkaebiNormal", dokkaebi);
        Set(so, "dokkaebiNoBat", dokkaebiNoBat);
        Set(so, "dokkaebiSwing", dokkaebiSwing);
        // 5챕터 (도깨비의 저주)
        Set(so, "heartGroup", heartGroup.gameObject);
        SetArray(so, "heartIcons", heartIcons);
        Set(so, "choosePanel", choose.gameObject);
        SetArray(so, "chooseImages", chooseImages);
        Set(so, "dialogueFriend", dialogueFriend);
        Set(so, "friendSprite", friend);
        Set(so, "friendSpiritSprite", friendSpirit);
        Set(so, "dokkaebiDark", dokkaebiDark);
        SetArray(so, "faceDark", new[] { Chap5("Char_DokkaebiFace_Normal_Dark.png", faceDokkaebi),
                                         Chap5("Char_DokkaebiFace_Proud_Dark.png", faceDokkaebiProud),
                                         Chap5("Char_DokkaebiFace_Tease_Dark.png", faceDokkaebiTease) });
        Set(so, "dokkaebiDarkBat", dokkaebiDarkBat);
        Set(so, "forestBright", forestBright);
        Set(so, "forestDark", forestDark);
        Set(so, "scareOverlay", scareOverlay.gameObject);
        Set(so, "jamOverlay", jamOverlay.gameObject);
        SetArray(so, "jamFires", jamFires);
        Set(so, "jamPeek", jamPeek.rectTransform);
        SetArray(so, "jamScribbles", jamScribbles);
        Set(so, "jamTint", jamTint);
        Set(so, "judgeRoam", dokEnterGo);
        Set(so, "judgeCurse", judgeCurse.gameObject);
        Set(so, "judgeGlow", judgeGlow);
        Set(so, "soulFriend", soulFriend);
        Set(so, "soulSpirit", soulSpirit);
        Set(so, "curseEndPanel", curseEnd.gameObject);
        Set(so, "curseEndBoy", curseEndBoy);
        Set(so, "curseEndFriend", curseEndFriend);
        Set(so, "curseEndDokkaebi", curseEndDokkaebi);
        Set(so, "curseEndSilhouette", curseSilhouetteImg.gameObject);
        Set(so, "curseEndBat", curseEndBat.gameObject);
        Set(so, "curseEndBox", curseEndBoxComp);
        Set(so, "curseEndSoul", curseEndSoulImg.gameObject);
        Set(so, "endingJar", endingJar);
        Set(so, "roomEndJar", roomEndJar);
        Set(so, "curseEndJar", curseEndJar);
        Set(so, "curseNote", curseNoteGo.gameObject);
        Set(so, "curseNoteDrawing", curseNoteDrawingImg);
        Set(so, "curseNoteGlow", curseNoteGlowImg);
        // 흑화 순간 번쩍 (C1) : 대화 화면 맨 위를 덮는 흰 막. 터치는 막지 않아요
        var darkFlashImg = NewImage("DarkFlash", dialogue, new Color(1f, 1f, 1f, 0.85f));
        darkFlashImg.raycastTarget = false;
        Stretch(darkFlashImg.rectTransform);
        darkFlashImg.gameObject.SetActive(false);
        Set(so, "darkFlash", darkFlashImg);
        Set(so, "settingsMain", settingsMain.gameObject);
        Set(so, "settingsConfirm", settingsConfirm.gameObject);
        Set(so, "settingsExitButton", settingsExit.transform.parent.gameObject);
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
        Set(so, "windowFire", windowFire.gameObject);
        Set(so, "roomEndPanel", roomEnd.gameObject);
        Set(so, "roomEndDokkaebi", roomEndDokImg);
        Set(so, "roomEndDokkaebiAnim", roomEndDokAnim);
        SetArray(so, "roomEndItems", roomEndItems);
        Set(so, "roomEndBox", roomEndBoxComp);
        Set(so, "roomEndEyes", roomEndEyes.gameObject);
        Set(so, "judgeDokkaebi", judgeDokkaebi);
        // 도깨비 기분 : 3챕터 주황, 4챕터 붉은 머리 불 (Tools/art_temp/make_dokkaebi_mood.py 로 만든 임시 그림)
        Sprite Mood(string name, int mood, Sprite normal) => mood == 0 ? normal : LoadSprite($"Characters/Mood/{name}_M{mood}.png") ?? normal;
        Sprite[] Moods(string name, Sprite normal) => new[] { normal, Mood(name, 1, normal), Mood(name, 2, normal) };
        SetArray(so, "moodNormal", Moods("Char_Dokkaebi_Normal", dokkaebi));
        SetArray(so, "moodNoBat", Moods("Char_Dokkaebi_NoBat", dokkaebiNoBat));
        SetArray(so, "moodSwing", Moods("Char_Dokkaebi_Swing", dokkaebiSwing));
        SetArray(so, "moodThink", Moods("Char_Dokkaebi_Think", dokkaebiThink));
        SetArray(so, "moodFace", Moods("Char_DokkaebiFace_Normal", faceDokkaebi));
        SetArray(so, "moodFaceProud", Moods("Char_DokkaebiFace_Proud", faceDokkaebiProud));
        SetArray(so, "moodFaceTease", Moods("Char_DokkaebiFace_Tease", faceDokkaebiTease));
        Set(so, "guideHighlight", guideHighlight);
        SetArray(so, "guideFires", guideFires);
        Set(so, "guidePaper", paperImg.rectTransform);
        Set(so, "guideUndo", (RectTransform)undoButton.transform);
        Set(so, "guideSubmit", (RectTransform)submitButton.transform);
        Set(so, "guideBrush", brushHolder);
        SetArray(so, "endingItems", new[] { endBat, endBag, endMat, endGimbap, endFriend });
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
        Set(tso, "paperView", paperView);
        Set(tso, "undoButton", undoButton);
        Set(tso, "redoButton", redoButton);
        Set(tso, "palette", paletteGroup.gameObject);
        Set(tso, "blackFrame", blackFrame);
        Set(tso, "primaryDivider", primaryDivider);
        SetArray(tso, "primaryButtons", primaryButtons);
        SetArray(tso, "primaryFrames", primaryFrames);
        Set(tso, "paintUndo", paintUndoSlot.gameObject);
        SetArray(tso, "sizeDots", sizeDots);
        SetArray(tso, "sizeFrames", sizeFrames);
        tso.ApplyModifiedPropertiesWithoutUndo();

        var pvso = new SerializedObject(paperView);
        Set(pvso, "paper", paperRt);
        Set(pvso, "board", board);
        Set(pvso, "fitButton", fitImg.gameObject);
        Set(pvso, "fullArea", fullArea);
        pvso.ApplyModifiedPropertiesWithoutUndo();

        // 웹 게임용 AI 판정 중계 서버 주소 (Server/judge/server_url.txt, 없으면 빈 칸 = 서버 안 씀)
        var jso = new SerializedObject(judge);
        jso.FindProperty("serverUrl").stringValue = MvpJudgeServer.ServerUrl;
        jso.ApplyModifiedPropertiesWithoutUndo();

        var mso = new SerializedObject(mixer);
        Set(mso, "blackButton", (RectTransform)blackSlot);
        SetArray(mso, "materialButtons", materialButtons);
        Set(mso, "undoButton", paintUndoButton);
        SetArray(mso, "countTexts", countTexts);
        Set(mso, "previewSwatch", brushTip);
        Set(mso, "splashPoint", tipPoint);
        Set(mso, "splashTarget", brushHolder);
        Set(mso, "dropSprite", circleSprite);
        mso.ApplyModifiedPropertiesWithoutUndo();

        // 처음에는 시작 화면만 보이게 (게임 시작 시 GameManager 도 같은 처리를 해요)
        foreach (Transform p in new[] { dialogue, dialogueDim, observe, draw, judgeP, result, compare, ending, choose, curseEnd }) p.gameObject.SetActive(false);
        heartGroup.gameObject.SetActive(false);
        dialogueFriend.gameObject.SetActive(false);
        scareOverlay.gameObject.SetActive(false);
        jamOverlay.gameObject.SetActive(false);
        curseSilhouetteImg.gameObject.SetActive(false);
        curseEndBat.gameObject.SetActive(false);
        settingsConfirm.gameObject.SetActive(false);
        giveNote.gameObject.SetActive(false);
        giveSnack.gameObject.SetActive(false);
        openingGroup.gameObject.SetActive(false);
        paletteGroup.gameObject.SetActive(false);
        brushHighlight.gameObject.SetActive(false);
        paintHighlight.gameObject.SetActive(false);
        fitImg.gameObject.SetActive(false);
        peekGo.gameObject.SetActive(false);
        peekGuide.gameObject.SetActive(false);
        guideHighlight.gameObject.SetActive(false);
        windowFire.gameObject.SetActive(false);
        roomEnd.gameObject.SetActive(false);
        roomEndEyes.gameObject.SetActive(false);
        magHighlight.gameObject.SetActive(false);
        giftMagnifier.gameObject.SetActive(false);
        magSparkles.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
        hud.gameObject.SetActive(false);

        PinToScreenEdges(frameRt);

        // ── 저장 + Build Settings 의 첫 번째 씬으로 등록 ──
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true), new EditorBuildSettingsScene(ScenePath, true) };   // 로비가 맨 앞 (처음 열리는 씬)
        AssetDatabase.SaveAssets(); // Build Settings 변경도 디스크에 저장
        Debug.Log("[찾아라도깨비] MVP 씬을 만들었어요: " + ScenePath);
    }
}

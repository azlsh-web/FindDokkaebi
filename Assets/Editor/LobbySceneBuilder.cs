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
// LobbySceneBuilder : 로비 씬(Assets/Scenes/Lobby.unity) 자동 조립 도구 (에디터 전용)
// ─────────────────────────────────────────────────────────────
// 메뉴 [Tools → 찾아라도깨비 → 로비 씬 만들기] 를 누르거나,
// Temp/mvp_lobby.flag 파일이 생기면 (아래 LobbyAutoBuild) 조립해요.
// 시작 화면 + 챕터 선택 + 사진 팝업 + 소리 설정 창이 한 씬에 들어 있고, 로직은 LobbyManager 에 있어요.
// 끝나면 Build Settings 에 [Lobby, Game] 순서로 등록해요.
// 좌표는 모두 1080 x 1899 화면 기준, 왼쪽 위에서 (x, y, 폭, 높이) 예요.
public static class LobbySceneBuilder
{
    const string ScenePath = "Assets/Scenes/Lobby.unity";
    const string GameScenePath = "Assets/Scenes/Game.unity";
    const string SpriteFolder = "Assets/Sprites";
    const string FontPath = "Assets/Fonts/Jua-Regular SDF.asset";
    const string FallbackFontPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";
    const float K = 1080f / 2248f;
    const float FrameHeight = 3952f * K;      // ≈ 1899 (게임 씬과 같은 기준)

    static readonly Color RoomColor = new Color32(248, 241, 227, 255);
    static readonly Color OutlineColor = new Color32(74, 43, 22, 255);
    static readonly Color CreamColor = new Color32(251, 245, 231, 255);
    static readonly Color StringColor = new Color32(138, 106, 79, 255);     // #8a6a4f
    static readonly Color DarkPhoto = new Color(0.35f, 0.35f, 0.42f, 1f);   // 5챕터 그림이 아직 없을 때

    static TMP_FontAsset font;
    static Sprite cardSprite, pillCoral, pillCream, circleSprite;

    [MenuItem("Tools/찾아라도깨비/로비 씬 만들기")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        EnsureSprites();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 새 씬을 연 "뒤에" 그림을 불러와야 연결이 끊기지 않아요
        cardSprite = Ui("Common_Card");
        pillCoral = Ui("Common_ButtonCoral");
        pillCream = Ui("Common_ButtonCream");
        circleSprite = Sp("UI/Common_Circle.png");
        Sprite bgStart = Sp("Background/BG_Start.png");
        Sprite bgRoom = Sp("Background/BG_Room.png");
        Sprite picnicPhoto = Sp("UI/Chapter_Picnic.png");
        Sprite forestPhoto = Sp("Chapter5/Chapter_Forest.png");
        Sprite rainPhoto = Sp("Chapter2/Chapter_Rain.png") ?? picnicPhoto;          // 2~4챕터 사진 (임시 그림, 없으면 소풍 사진)
        Sprite birthdayPhoto = Sp("Chapter3/Chapter_Birthday.png") ?? picnicPhoto;
        Sprite snackPhoto = Sp("Chapter4/Chapter_Snack.png") ?? picnicPhoto;
        Material grayMaterial = GrayMaterial();
        bool forestMissing = forestPhoto == null;
        if (forestMissing) forestPhoto = picnicPhoto;       // 아직 없으면 소풍 그림을 어둡게

        // 카메라 · EventSystem · Canvas
        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RoomColor;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, FrameHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Transform root = canvasGo.transform;

        // 배경 : 화면을 꽉 채워요 (시작 화면 = BG_Start, 챕터 선택 = BG_Room 으로 LobbyManager 가 바꿔요)
        var bg = NewImage("Background", root, Color.white);
        bg.sprite = bgStart;
        Stretch(bg.rectTransform);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 2248f / 3952f;
        bg.raycastTarget = false;

        // 가운데 폭 1080 "Frame"
        var frameGo = new GameObject("Frame", typeof(RectTransform));
        frameGo.transform.SetParent(root, false);
        var frameRt = (RectTransform)frameGo.transform;
        frameRt.anchorMin = new Vector2(0.5f, 0f);
        frameRt.anchorMax = new Vector2(0.5f, 1f);
        frameRt.pivot = new Vector2(0.5f, 0.5f);
        frameRt.anchoredPosition = Vector2.zero;
        frameRt.sizeDelta = new Vector2(1080f, 0f);
        Transform frame = frameGo.transform;

        var mgrGo = new GameObject("LobbyManager");
        var mgr = mgrGo.AddComponent<LobbyManager>();

        // ── 시작 화면 : 버튼 없이 아무 데나 누르면 챕터 선택 ──
        var startPanel = NewPanel("StartPanel", frame);
        var startContent = NewContent(startPanel);
        var startTap = NewTap("StartTap", startContent);
        NewSprite("TitleLogo", startContent, Ui("Title_Logo"), 180, 90, 720, 522);
        var boy = NewSprite("TitleBoy", startContent, Sp("Characters/Char_Boy_Normal.png"), 74, 826, 400, 608);
        Anim(boy.gameObject, SimpleAnim.Kind.Gentle, 1.2f, 0f, 15f);
        var dok = NewSprite("TitleDokkaebi", startContent, Sp("Characters/Char_Dokkaebi_Normal.png"), 506, 946, 500, 488);
        Anim(dok.gameObject, SimpleAnim.Kind.Gentle, 1.2f, 0.5f, 15f);
        var startText = NewText("StartText", startContent, "게임 시작하기", 64, Color.black);
        G(startText.rectTransform, 150, 1548, 780, 210);

        // ── 챕터 선택 : 줄에 매달린 사진 5장 ──
        var chapterPanel = NewPanel("ChapterPanel", frame);
        var chapterContent = NewContent(chapterPanel);
        var stringLine = NewImage("String", chapterContent, StringColor);
        stringLine.raycastTarget = false;
        G(stringLine.rectTransform, 535, 140, 3, 1720);

        var chapterButtons = new Button[5];
        var cardAnims = new SimpleAnim[5];
        float[] tilt = { -3f, 2.5f, -2f, 3f, -1.5f };
        Sprite[] photos = { picnicPhoto, rainPhoto, birthdayPhoto, snackPhoto, forestPhoto };
        var cardPictures = new Image[5];
        for (int n = 0; n < 5; n++)
        {
            float x = n % 2 == 0 ? 340f : 364f;
            float y = 200f + 332f * n;
            var holder = new GameObject($"Chapter_{n + 1}", typeof(RectTransform));
            holder.transform.SetParent(chapterContent, false);
            var hrt = (RectTransform)holder.transform;          // 기울기는 이 껍데기가 맡고, 흔들림은 안쪽 카드가 맡아요
            hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 0.5f);
            hrt.anchoredPosition = new Vector2(x + 194f - 540f, -(y + 146f));
            hrt.sizeDelta = new Vector2(388, 292);
            hrt.localRotation = Quaternion.Euler(0f, 0f, tilt[n]);

            var card = NewImage("Card", holder.transform, Color.white);
            card.sprite = cardSprite;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 1f / K;
            Stretch(card.rectTransform);
            chapterButtons[n] = card.gameObject.AddComponent<Button>();
            chapterButtons[n].transition = Selectable.Transition.None;
            cardAnims[n] = Anim(card.gameObject, SimpleAnim.Kind.None, 0.4f, 0f, 18f);

            // 사진은 늘 넣고, 아직 못 연 챕터는 LobbyManager 가 흑백 재질로 바꿔요 (깬 기록은 실행할 때 읽어요)
            var pic = NewImage("Picture", card.transform, Color.white);
            pic.sprite = photos[n];
            pic.raycastTarget = false;
            Stretch(pic.rectTransform, 24);
            if (n == 4 && forestMissing) pic.color = DarkPhoto;
            cardPictures[n] = pic;
        }

        // ── 위 줄 톱니 (두 화면 모두) ──
        var topGo = new GameObject("TopBar", typeof(RectTransform));
        topGo.transform.SetParent(frame, false);
        var topRt = (RectTransform)topGo.transform;
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.anchoredPosition = Vector2.zero;
        topRt.sizeDelta = new Vector2(0f, 200f);
        var gearImg = NewImage("SettingsButton", topRt, Color.white);
        gearImg.sprite = Ui("HUD_SettingsButton");
        gearImg.preserveAspect = true;
        G(gearImg.rectTransform, 902, 45, 110, 112);
        var gearButton = gearImg.gameObject.AddComponent<Button>();

        // ── 큰 사진 창 : 어둡게 덮고, 창을 누르면 게임 시작 · 바깥을 누르면 닫기 ──
        var popup = NewEmpty("Popup", root);
        var popupDim = popup.gameObject.AddComponent<Image>();
        popupDim.color = new Color(0f, 0f, 0f, 0.55f);
        var popupDimButton = popup.gameObject.AddComponent<Button>();
        popupDimButton.transition = Selectable.Transition.None;
        var popupContent = NewContent(popup);
        var popupCard = NewImage("PopupCard", popupContent, Color.white);
        popupCard.sprite = cardSprite;
        popupCard.type = Image.Type.Sliced;
        popupCard.pixelsPerUnitMultiplier = 1f / K;
        G(popupCard.rectTransform, 80, 460, 920, 800);
        var popupCardButton = popupCard.gameObject.AddComponent<Button>();
        popupCardButton.transition = Selectable.Transition.None;
        var popupPhoto = NewImage("Photo", popupCard.transform, Color.white);
        popupPhoto.raycastTarget = false;
        PIn(popupPhoto.rectTransform, 40, 40, 840, 600);
        var popupName = NewText("Name", popupCard.transform, "", 80, OutlineColor);
        PIn(popupName.rectTransform, 0, 660, 920, 140);

        // ── 소리 설정 창 : 60,770,960,360 = [왼쪽 110 빈칸] [두 줄 슬라이더] [닫기 110] ──
        var settings = NewEmpty("SettingsPanel", root);
        var settingsDim = settings.gameObject.AddComponent<Image>();
        settingsDim.color = new Color(0f, 0f, 0f, 0.45f);
        var settingsDimButton = settings.gameObject.AddComponent<Button>();
        settingsDimButton.transition = Selectable.Transition.None;
        var settingsContent = NewContent(settings);
        var settingsCard = NewImage("SettingsCard", settingsContent, Color.white);
        settingsCard.sprite = cardSprite;
        settingsCard.type = Image.Type.Sliced;
        settingsCard.pixelsPerUnitMultiplier = 1f / K;
        G(settingsCard.rectTransform, 60, 770, 960, 500);
        var closeButton = NewCloseButton("CloseHolder", settingsContent, Sp("UI/Mix_CloseIcon.png"));
        G((RectTransform)closeButton.transform.parent, 890, 790, 110, 110);
        var bgmSlider = VolumeRow("BgmSlider", "배경음", settingsContent, 840);
        var sfxSlider = VolumeRow("SfxSlider", "효과음", settingsContent, 980);
        // 개발자 모드 (C-66) : 누를 때마다 켜짐 ↔ 꺼짐. 켜면 판정을 AI 대신 O · X 로 골라요
        var devLabel = NewText("DevModeLabel", settingsContent, "개발자 모드", 52, OutlineColor);
        G(devLabel.rectTransform, 110, 1125, 340, 90);
        var devBack = NewImage("DevModeButton", settingsContent, Color.white);
        devBack.sprite = pillCream; devBack.type = Image.Type.Sliced; devBack.pixelsPerUnitMultiplier = pillCream.rect.height / 100f;
        G(devBack.rectTransform, 560, 1120, 300, 100);
        var devButton = devBack.gameObject.AddComponent<Button>();
        var devState = NewText("Label", devBack.transform, "꺼짐", 40, OutlineColor);
        Stretch(devState.rectTransform);
        var devToggle = devBack.gameObject.AddComponent<DevModeToggle>();
        UnityEventTools.AddPersistentListener(devButton.onClick, devToggle.Toggle);
        var devSo = new SerializedObject(devToggle);
        Set(devSo, "stateText", devState);
        Set(devSo, "back", devBack);
        Set(devSo, "onSprite", pillCoral);
        Set(devSo, "offSprite", pillCream);
        devSo.ApplyModifiedPropertiesWithoutUndo();

        // 겹치는 순서 : 톱니 < 사진 창 < 소리 설정 창
        topRt.SetAsLastSibling();
        popup.SetAsLastSibling();
        settings.SetAsLastSibling();

        // ── 버튼 연결 ──
        UnityEventTools.AddPersistentListener(startTap.onClick, mgr.StartTap);
        for (int i = 0; i < 5; i++) UnityEventTools.AddIntPersistentListener(chapterButtons[i].onClick, mgr.ChapterTap, i);
        UnityEventTools.AddPersistentListener(popupCardButton.onClick, mgr.PopupPlay);
        UnityEventTools.AddPersistentListener(popupDimButton.onClick, mgr.ClosePopup);
        UnityEventTools.AddPersistentListener(gearButton.onClick, mgr.ToggleSettings);
        UnityEventTools.AddPersistentListener(closeButton.onClick, mgr.CloseSettings);
        UnityEventTools.AddPersistentListener(settingsDimButton.onClick, mgr.CloseSettings);
        UnityEventTools.AddPersistentListener(bgmSlider.onValueChanged, mgr.SetBgmVolume);
        UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, mgr.SetSfxVolume);

        // ── LobbyManager 칸 채우기 ──
        var so = new SerializedObject(mgr);
        Set(so, "startPanel", startPanel.gameObject);
        Set(so, "chapterPanel", chapterPanel.gameObject);
        Set(so, "popup", popup.gameObject);
        Set(so, "settingsPanel", settings.gameObject);
        Set(so, "background", bg);
        Set(so, "startBackground", bgStart);
        Set(so, "roomBackground", bgRoom);
        Set(so, "startText", startText);
        SetArray(so, "cardAnims", cardAnims);
        SetArray(so, "chapterPhotos", photos);
        SetArray(so, "cardPictures", cardPictures);
        Set(so, "grayMaterial", grayMaterial);
        Set(so, "popupPhoto", popupPhoto);
        Set(so, "popupName", popupName);
        Set(so, "bgmSlider", bgmSlider);
        Set(so, "sfxSlider", sfxSlider);
        var tint = so.FindProperty("chapter5Tint");
        if (tint != null) tint.colorValue = forestMissing ? DarkPhoto : Color.white;
        so.ApplyModifiedPropertiesWithoutUndo();

        popup.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        chapterPanel.gameObject.SetActive(false);       // 시작할 때 LobbyManager 가 알맞게 켜요
        startPanel.gameObject.SetActive(true);

        // ── 저장 + Build Settings : Lobby 가 첫 번째, Game 이 두 번째 ──
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        AssetDatabase.SaveAssets();
        Debug.Log("[찾아라도깨비] 로비 씬을 만들었어요: " + ScenePath + (forestMissing ? " (5챕터 그림 Chapter_Forest.png 가 아직 없어 소풍 그림을 어둡게 썼어요)" : ""));
    }

    // ───────────── 그림 설정 ─────────────
    // 게임 씬 조립 때 이미 설정돼 있으면 그대로 두고, 아직 Sprite 가 아닌 것만 맞춰요 (게임 씬과 같은 값)
    static void EnsureSprites()
    {
        string[] files =
        {
            "UI/Common_Card.png", "UI/Common_ButtonCoral.png", "UI/Common_ButtonCream.png", "UI/Common_Circle.png",
            "UI/Chapter_Picnic.png", "UI/Chapter_Lock.png", "UI/HUD_SettingsButton.png", "UI/Mix_CloseIcon.png", "UI/Title_Logo.png",
            "Background/BG_Start.png", "Background/BG_Room.png",
            "Characters/Char_Boy_Normal.png", "Characters/Char_Dokkaebi_Normal.png",
            "Chapter5/Chapter_Forest.png",
            "Chapter2/Chapter_Rain.png", "Chapter3/Chapter_Birthday.png", "Chapter4/Chapter_Snack.png",
        };
        foreach (string f in files)
        {
            string path = SpriteFolder + "/" + f;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) continue;
            Vector4 border = Vector4.zero;
            if (path.EndsWith("UI/Common_Card.png")) border = new Vector4(80, 80, 80, 80);
            if (path.EndsWith("UI/Common_ButtonCoral.png")) border = new Vector4(182, 0, 182, 0);
            if (path.EndsWith("UI/Common_ButtonCream.png")) border = new Vector4(140, 0, 140, 0);
            bool crisp = path.Contains("/UI/") || path.EndsWith("Background/BG_Start.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.textureCompression = crisp ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = path.Contains("Chapter_") ? 1024 : 4096;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    // 아직 못 연 챕터 사진을 흑백으로 그리는 재질 (Assets/Materials/UI_Grayscale.mat). 없으면 만들어요
    static Material GrayMaterial()
    {
        const string path = "Assets/Materials/UI_Grayscale.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("FindDokkaebi/UI Grayscale");
        if (shader == null) return null;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Sprite Sp(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{relative}");
    static Sprite Ui(string name) => Sp("UI/" + name + ".png");

    // ───────────── UI 도우미 (MvpSceneBuilder 에서 필요한 것만 옮겨 왔어요) ─────────────

    static Transform NewEmpty(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go.transform;
    }

    // 화면이 더 길어도 1080 x 1899 그림이 가운데에 오게 하는 묶음
    static Transform NewContent(Transform parent)
    {
        var go = new GameObject("Content", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(1080f, FrameHeight);
        return rt;
    }

    // 화면 패널 : 켜질 때 서서히 나타나요
    static Transform NewPanel(string name, Transform parent)
    {
        var panel = NewEmpty(name, parent);
        panel.gameObject.AddComponent<CanvasGroup>();
        Anim(panel.gameObject, SimpleAnim.Kind.FadeIn, 0.35f, 0f, 0f);
        return panel;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    static Image NewSprite(string name, Transform parent, Sprite sprite, float x, float y, float w, float h)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        G(img.rectTransform, x, y, w, h);
        return img;
    }

    static TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    // 화면 전체를 덮는 투명한 터치 영역
    static Button NewTap(string name, Transform parent)
    {
        var img = NewImage(name, parent, new Color(1f, 1f, 1f, 0f));
        Stretch(img.rectTransform);
        return img.gameObject.AddComponent<Button>();
    }

    static SimpleAnim Anim(GameObject go, SimpleAnim.Kind kind, float time, float delay, float distance)
    {
        var a = go.AddComponent<SimpleAnim>();
        a.kind = kind;
        a.popTime = time;
        a.delay = delay;
        a.distance = distance;
        return a;
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // 부모(폭 1080)의 왼쪽 위에서 (x, y), 크기 (w, h)
    static void G(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x + w * 0.5f - 540f, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 안의 왼쪽 위 기준 좌표
    static void PIn(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Anchor(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(ax, ay);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // 동그란 닫기 버튼 : [묶음] 안에 [테두리 동그라미] + [크림 동그라미 + X 그림]
    static Button NewCloseButton(string name, Transform parent, Sprite icon)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        var edge = NewImage("Edge", holder.transform, OutlineColor);
        edge.sprite = circleSprite;
        Stretch(edge.rectTransform);
        edge.raycastTarget = false;
        var img = NewImage("Button", holder.transform, Color.white);
        img.sprite = circleSprite;
        Stretch(img.rectTransform, 5);
        var button = img.gameObject.AddComponent<Button>();
        var iconImg = NewImage("Icon", img.transform, Color.white);
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        Stretch(iconImg.rectTransform, 110 * 0.14f);
        return button;
    }

    // 이름 글자 + 슬라이더 한 줄 (y = 줄 위쪽)
    static Slider VolumeRow(string name, string label, Transform parent, float y)
    {
        var text = NewText(name + "Label", parent, label, 60, OutlineColor);
        G(text.rectTransform, 190, y, 200, 90);
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        G((RectTransform)go.transform, 400, y, 470, 90);
        var track = NewImage("Background", go.transform, Color.white);
        track.sprite = pillCream; track.type = Image.Type.Sliced; track.pixelsPerUnitMultiplier = 2f / K;
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

// Temp/mvp_lobby.flag 파일이 생기면 로비 씬을 조립해요 (MvpAutoBuild 와 같은 방식)
[InitializeOnLoad]
static class LobbyAutoBuild
{
    const string Flag = "Temp/mvp_lobby.flag";
    static double nextCheck;
    static bool refreshed;

    static LobbyAutoBuild()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        if (!File.Exists(Flag)) return;

        // 바뀐 코드를 먼저 읽고, 다음 확인 때 조립해요
        if (!refreshed)
        {
            refreshed = true;
            AssetDatabase.Refresh();
            return;
        }
        refreshed = false;
        File.Delete(Flag);
        LobbySceneBuilder.Build();
    }
}

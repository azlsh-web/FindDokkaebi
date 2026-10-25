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
//   1) Assets/Sprites 의 그림을 Sprite 로 설정하고
//   2) 1단계 데이터 파일(Assets/Data/Stage1_Bat.asset)을 만들고
//   3) Assets/Scenes/Game.unity 씬에 카메라·Canvas·패널·버튼을 전부 만들어
//   4) GameManager 의 칸들과 버튼 OnClick 까지 연결한 뒤 저장해요.
//
// ※ 손으로 Hierarchy 에서 하나씩 만드는 것과 결과는 똑같아요.
//   만들어진 씬을 열어서 오브젝트를 클릭해 보면 Inspector 에 전부 보여요.
//   게임 로직은 Assets/Scripts 에 있고, 이 파일은 "조립 도우미"일 뿐이에요.
//   씬이 완성된 뒤에는 지워도 게임은 그대로 동작해요.
//   (다시 누르면 Game 씬을 새로 덮어써요. 손으로 고친 내용은 사라지니 주의!)
public static class MvpSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string StagePath = "Assets/Data/Stage1_Bat.asset";
    const string OriginalPath = "Assets/Sprites/Bat_Original.png";
    const string WrongPath = "Assets/Sprites/Bat_Wrong.png";
    const string FontPath = "Assets/Fonts/NotoSansKR-Regular SDF.asset";

    // 화면 색 (따뜻한 방 느낌)
    static readonly Color RoomColor = new Color(0.95f, 0.89f, 0.76f);
    static readonly Color ButtonColor = new Color(0.55f, 0.36f, 0.22f);
    static readonly Color TextColor = new Color(0.20f, 0.14f, 0.10f);

    static TMP_FontAsset font;
    static Sprite uiSprite;

    [MenuItem("Tools/찾아라도깨비/MVP 씬 만들기")]
    public static void Build()
    {
        // 저장 안 한 씬이 있으면 먼저 물어보기
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        CreateStageData();

        // ── 새 빈 씬 ──
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 새 씬을 열면 Unity 가 안 쓰는 에셋을 메모리에서 내려버려서,
        // 위에서 만든 데이터 파일을 씬을 연 "뒤에" 다시 불러와야 연결이 끊기지 않아요.
        StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);

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

        // Canvas : 모든 UI 의 부모. 세로 화면 1080x1920 기준으로 크기 자동 조절
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        Transform canvas = canvasGo.transform;

        // 배경 (방)
        Stretch(NewImage("Background", canvas, RoomColor).rectTransform);

        // GameManager 오브젝트
        var gm = new GameObject("GameManager").AddComponent<GameManager>();

        // ── HUD (항상 보임) ──
        var hud = NewEmpty("HUD", canvas);
        var snackText = NewText("SnackText", hud, "간식 3개", 48, new Vector2(-280, 40), new Vector2(460, 80), TextAlignmentOptions.Left);
        var stageText = NewText("StageText", hud, stage.stageTitle, 48, new Vector2(280, 40), new Vector2(460, 80), TextAlignmentOptions.Right);
        var timerText = NewText("TimerText", hud, "", 44, new Vector2(280, 120), new Vector2(460, 70), TextAlignmentOptions.Right);

        // ── 1. 도입 화면 ──
        var intro = NewEmpty("IntroPanel", canvas);
        NewText("Title", intro, "찾아라! 도깨비", 96, new Vector2(0, 300), new Vector2(1000, 160));
        var introStory = NewText("Story", intro, "", 44, new Vector2(0, 520), new Vector2(900, 600));
        var startButton = NewButton("StartButton", intro, "기억할 모습 보기", new Vector2(0, 1500), new Vector2(700, 150));

        // ── 2. 관찰 화면 ──
        var observe = NewEmpty("ObservePanel", canvas);
        NewText("Guide", observe, "잘 기억하세요!", 56, new Vector2(0, 240), new Vector2(1000, 100));
        var observeImage = NewImage("TargetImage", observe, Color.white);
        Place(observeImage.rectTransform, new Vector2(0, 380), new Vector2(900, 900));
        observeImage.preserveAspect = true;
        var observeCount = NewText("Countdown", observe, "3", 160, new Vector2(0, 1330), new Vector2(400, 220));

        // ── 3. 그리기 화면 ──
        var draw = NewEmpty("DrawPanel", canvas);
        NewText("Guide", draw, $"기억한 {stage.targetName}를 그려주세요", 48, new Vector2(0, 220), new Vector2(1000, 90));
        var boardGo = new GameObject("Board", typeof(RectTransform), typeof(RawImage), typeof(DrawingBoard));
        boardGo.transform.SetParent(draw, false);
        Place(boardGo.GetComponent<RectTransform>(), new Vector2(0, 330), new Vector2(900, 900));
        var board = boardGo.GetComponent<DrawingBoard>();
        var penButton = NewButton("PenButton", draw, "펜", new Vector2(-300, 1270), new Vector2(260, 120));
        var eraserButton = NewButton("EraserButton", draw, "지우개", new Vector2(0, 1270), new Vector2(260, 120));
        var clearButton = NewButton("ClearButton", draw, "모두 지우기", new Vector2(300, 1270), new Vector2(260, 120));
        var submitButton = NewButton("SubmitButton", draw, "제출 (간식 1개)", new Vector2(0, 1460), new Vector2(700, 150));

        // ── 4. 판정 화면 (테스트 입력) ──
        var judge = NewEmpty("JudgePanel", canvas);
        NewText("Waiting", judge, "도깨비가 쪽지를 들여다보는 중...", 52, new Vector2(0, 260), new Vector2(1000, 100));
        NewText("TestGuide", judge, "[테스트 입력] 그림에 들어간 특징을 체크하세요\n(나중에 AI 판정으로 바뀔 부분)", 36,
                new Vector2(0, 400), new Vector2(1000, 130));
        var toggles = new Toggle[stage.features.Length];
        var toggleLabels = new TMP_Text[stage.features.Length];
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i] = NewToggle($"FeatureToggle{i + 1}", judge, new Vector2(0, 600 + i * 130), out toggleLabels[i]);
        }
        var judgeButton = NewButton("ConfirmButton", judge, "판정 결과 보내기", new Vector2(0, 1100), new Vector2(700, 150));

        // ── 5. 결과 화면 ──
        var result = NewEmpty("ResultPanel", canvas);
        var resultTitle = NewText("ResultTitle", result, "", 56, new Vector2(0, 220), new Vector2(1000, 120));
        var resultItem = NewImage("ReturnedItem", result, Color.white);
        Place(resultItem.rectTransform, new Vector2(0, 370), new Vector2(600, 600));
        resultItem.preserveAspect = true;
        var resultFeatures = NewText("FeatureList", result, "", 40, new Vector2(0, 1010), new Vector2(900, 320), TextAlignmentOptions.TopLeft);
        var retryButton = NewButton("RetryButton", result, "다시 그리기 (간식 1개)", new Vector2(0, 1400), new Vector2(700, 140));
        var compareButton = NewButton("CompareButton", result, "비교하기", new Vector2(0, 1570), new Vector2(700, 140));

        // ── 6. 비교 화면 ──
        var compare = NewEmpty("ComparePanel", canvas);
        NewText("MyLabel", compare, "내 그림", 48, new Vector2(-260, 260), new Vector2(480, 90));
        NewText("OriginalLabel", compare, "원본", 48, new Vector2(260, 260), new Vector2(480, 90));
        var myDrawing = new GameObject("MyDrawing", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        myDrawing.transform.SetParent(compare, false);
        Place(myDrawing.rectTransform, new Vector2(-260, 370), new Vector2(480, 480));
        var original = NewImage("Original", compare, Color.white);
        Place(original.rectTransform, new Vector2(260, 370), new Vector2(480, 480));
        original.preserveAspect = true;
        NewText("FeatureHint", compare, "원본의 특징\n- " + string.Join("\n- ", stage.features), 40,
                new Vector2(0, 920), new Vector2(900, 300), TextAlignmentOptions.TopLeft);
        var restartButton = NewButton("RestartButton", compare, "처음부터 다시", new Vector2(0, 1450), new Vector2(700, 150));

        // HUD 가 패널보다 위에 그려지도록 맨 뒤로 보내기 (Hierarchy 아래쪽일수록 앞에 그려짐)
        hud.SetAsLastSibling();

        // ── 버튼 OnClick 연결 (Inspector 의 On Click() 목록에 보여요) ──
        UnityEventTools.AddPersistentListener(startButton.onClick, gm.StartObserve);
        UnityEventTools.AddPersistentListener(penButton.onClick, board.UsePen);
        UnityEventTools.AddPersistentListener(eraserButton.onClick, board.UseEraser);
        UnityEventTools.AddPersistentListener(clearButton.onClick, board.Clear);
        UnityEventTools.AddPersistentListener(submitButton.onClick, gm.Submit);
        UnityEventTools.AddPersistentListener(judgeButton.onClick, gm.OnJudgeConfirmed);
        UnityEventTools.AddPersistentListener(retryButton.onClick, gm.Retry);
        UnityEventTools.AddPersistentListener(compareButton.onClick, gm.ShowCompare);
        UnityEventTools.AddPersistentListener(restartButton.onClick, gm.Restart);

        // ── GameManager 의 Inspector 칸 채우기 ──
        // (private [SerializeField] 칸은 SerializedObject 로 이름을 찾아서 넣어요)
        var so = new SerializedObject(gm);
        so.FindProperty("stage").objectReferenceValue = stage;
        so.FindProperty("snackText").objectReferenceValue = snackText;
        so.FindProperty("stageText").objectReferenceValue = stageText;
        so.FindProperty("timerText").objectReferenceValue = timerText;
        so.FindProperty("introPanel").objectReferenceValue = intro.gameObject;
        so.FindProperty("observePanel").objectReferenceValue = observe.gameObject;
        so.FindProperty("drawPanel").objectReferenceValue = draw.gameObject;
        so.FindProperty("judgePanel").objectReferenceValue = judge.gameObject;
        so.FindProperty("resultPanel").objectReferenceValue = result.gameObject;
        so.FindProperty("comparePanel").objectReferenceValue = compare.gameObject;
        so.FindProperty("introStoryText").objectReferenceValue = introStory;
        so.FindProperty("observeImage").objectReferenceValue = observeImage;
        so.FindProperty("observeCountText").objectReferenceValue = observeCount;
        so.FindProperty("board").objectReferenceValue = board;
        so.FindProperty("submitButton").objectReferenceValue = submitButton;
        SetArray(so.FindProperty("featureToggles"), toggles);
        SetArray(so.FindProperty("featureToggleLabels"), toggleLabels);
        so.FindProperty("resultTitleText").objectReferenceValue = resultTitle;
        so.FindProperty("resultItemImage").objectReferenceValue = resultItem;
        so.FindProperty("resultFeatureText").objectReferenceValue = resultFeatures;
        so.FindProperty("retryButton").objectReferenceValue = retryButton;
        so.FindProperty("compareButtonLabel").objectReferenceValue = compareButton.GetComponentInChildren<TMP_Text>();
        so.FindProperty("compareMyDrawing").objectReferenceValue = myDrawing;
        so.FindProperty("compareOriginal").objectReferenceValue = original;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 처음에는 도입 화면만 보이게 (게임 시작 시 GameManager 도 같은 처리를 해요)
        observe.gameObject.SetActive(false);
        draw.gameObject.SetActive(false);
        judge.gameObject.SetActive(false);
        result.gameObject.SetActive(false);
        compare.gameObject.SetActive(false);

        // ── 저장 + Build Settings 의 첫 번째 씬으로 등록 ──
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets(); // Build Settings 변경도 디스크에 저장
        Debug.Log("[찾아라도깨비] MVP 씬을 만들었어요: " + ScenePath);
    }

    // ───────────── 데이터 파일 만들기 ─────────────

    static void CreateStageData()
    {
        MakeSprite(OriginalPath);
        MakeSprite(WrongPath);

        var stage = AssetDatabase.LoadAssetAtPath<StageData>(StagePath);
        if (stage == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageTitle = "1단계 방망이";
            stage.targetName = "방망이";
            stage.introStory = "도깨비가 방망이를 잃어버렸대요.\n방망이를 먼저 찾아주면\n소풍 준비물도 찾아준대요!\n\n3초 동안 잘 보고 기억해서 그려보세요.";
            AssetDatabase.CreateAsset(stage, StagePath);
        }
        stage.originalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OriginalPath);
        stage.wrongSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WrongPath);
        EditorUtility.SetDirty(stage);
        AssetDatabase.SaveAssets();
    }

    // PNG 를 UI 에서 쓸 수 있는 Sprite 로 설정
    static void MakeSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
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

        var t = NewText("Label", img.transform, label, 44, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        t.color = Color.white;
        return button;
    }

    static Toggle NewToggle(string name, Transform parent, Vector2 pos, out TMP_Text label)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        Place(root.GetComponent<RectTransform>(), pos, new Vector2(900, 110));

        // 체크 상자
        var box = NewImage("Box", root.transform, Color.white);
        box.sprite = uiSprite;
        box.type = Image.Type.Sliced;
        var boxRt = box.rectTransform;
        boxRt.anchorMin = boxRt.anchorMax = new Vector2(0, 0.5f);
        boxRt.pivot = new Vector2(0, 0.5f);
        boxRt.anchoredPosition = Vector2.zero;
        boxRt.sizeDelta = new Vector2(90, 90);

        // 체크 표시 (켜지면 보임)
        var check = NewImage("Check", box.transform, new Color(0.18f, 0.49f, 0.20f));
        check.sprite = uiSprite;
        check.type = Image.Type.Sliced;
        var checkRt = check.rectTransform;
        checkRt.anchorMin = Vector2.zero;
        checkRt.anchorMax = Vector2.one;
        checkRt.offsetMin = new Vector2(14, 14);
        checkRt.offsetMax = new Vector2(-14, -14);

        // 글자 (GameManager 가 시작할 때 특징 이름으로 채워요)
        label = NewText("Label", root.transform, "F?", 42, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0, 0);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(120, 0);
        lrt.offsetMax = Vector2.zero;
        label.raycastTarget = true; // 글자를 눌러도 체크되게

        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
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

    // 부모 크기에 꽉 채우기
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetArray(SerializedProperty prop, Object[] items)
    {
        prop.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}

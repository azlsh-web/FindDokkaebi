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

// MvpSceneBuilder 의 데이터 파일 만들기 (물건 · 챕터 · 그림 설정) 부분이에요. 씬 조립 순서는 MvpSceneBuilder.cs 의 Build() 에 있어요.
public static partial class MvpSceneBuilder
{
    // ───────────── 데이터 파일 만들기 (5단계) ─────────────
    // 기획서 3장 레벨 디자인 + 부록 B 판정 기준을 그대로 옮겼어요.
    // 이미 파일이 있으면 건드리지 않아요. (Inspector 에서 고친 내용 보존)

    // 물건 데이터. 1챕터는 형태 기준 하나, 5챕터는 기획서 판정 포인트대로 (형태 · 무늬 · 색)
    //   오답 그림은 물건마다 3장 : n번째로 틀리면 n번째 그림. 없으면 앞의 오답으로 채워요
    static void CreateStageDataFiles()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Data");
        ItemWrongs.Clear();

        CreateItem("Stage1_Bat", 1, "방망이",
            "굵은 머리에 손잡이가 이어진 도깨비방망이입니다. 머리에는 짧게 튀어나온 돌기가 여러 개 있고, 손잡이에는 리본이 묶여 있습니다.",
            "Stage1_Bat/Bat_Original.png", new[] { "Stage1_Bat/Bat_Wrong_X.png", "Chapter5/Ch1_Bat_Wrong2.png", "Chapter5/Ch1_Bat_Wrong3.png" },
            ("굵은 머리 + 돌기", "굵은 머리가 표현되어 있고, 머리 바깥 윤곽에 튀어나온 돌기가 두 개 이상 구분되어야 합니다. 뾰족한 돌기, 둥근 혹, 구름 모양을 인정합니다. 매끈한 원이나 타원 머리, 머리 안쪽의 무늬, 손잡이의 리본은 돌기로 인정하지 않습니다. 손잡이의 유무와 길이는 판정하지 않습니다.", AutoCheck.BatShape, InkColor.Black, Area.TopLeft));

        CreateItem("Stage2_Bag", 2, "가방",
            "정면에서 본 배낭입니다. 몸체는 모서리가 둥근 네모이고, 위쪽에 고리 모양 손잡이, 앞쪽에 네모난 주머니가 있습니다.",
            "Stage2_Bag/Bag_Original.png", new[] { "Stage2_Bag/Bag_Wrong_XO.png", "Stage2_Bag/Bag_Wrong_XX.png", "Stage2_Bag/Bag_Wrong_OX.png" },
            ("네모 몸체 + 위쪽 손잡이 + 앞주머니", "네 변을 가진 사각형에 가까운 몸체 외곽, 몸체 위쪽에 이어진 고리나 아치 모양 손잡이, 몸체 안쪽의 작은 사각형 주머니가 모두 식별되어야 합니다. 모서리가 둥글거나 선이 삐뚤어도 인정합니다. 원형이나 삼각형 몸체, 옆이나 아래에만 있는 손잡이는 인정하지 않습니다.", AutoCheck.BagShape, InkColor.Black, Area.TopLeft));

        CreateItem("Stage3_Mat", 3, "돗자리",
            "조금 비스듬히 놓인 네모난 돗자리입니다.",
            "Stage3_Mat/Mat_Original.png", new[] { "Stage3_Mat/Mat_Wrong_XOO.png", "Stage3_Mat/Mat_Wrong_XXO.png", "Stage3_Mat/Mat_Wrong_XXX.png" },
            ("사각형 외곽", "네 변을 가진 사각형에 가까운 외곽이 식별되어야 합니다. 비스듬하거나 선이 삐뚤고 비율이 달라도 인정합니다. 원형이나 삼각형처럼 명확히 다른 외곽이면 인정하지 않습니다.", AutoCheck.RectOutline, InkColor.Black, Area.TopLeft));

        CreateItem("Stage4_Gimbap", 4, "김밥",
            "단면에서 본 김밥입니다. 바깥에 둥근 김 테두리가 둘러져 있습니다.",
            "Stage4_Gimbap/Gimbap_Original.png", new[] { "Stage4_Gimbap/Gimbap_Wrong_XOOO.png", "Stage4_Gimbap/Gimbap_Wrong_XXOO.png", "Stage4_Gimbap/Gimbap_Wrong_XXXX.png" },
            ("둥근 테두리", "둥근 바깥 테두리가 둘러져 있어야 합니다. 살짝 찌그러지거나 선이 조금 끊겨도 전체적으로 둥근 외곽이 식별되면 인정합니다. 사각형 외곽이나 크게 열린 곡선은 인정하지 않습니다. 선의 색과 굵기, 안쪽 재료의 유무와 배치는 판정하지 않습니다.", AutoCheck.RoundOutline, InkColor.Black, Area.TopLeft));

        CreateItem("Stage5_Friend", 5, "친구",
            "정면에서 본 친구입니다. 머리를 양쪽으로 한 갈래씩 묶었습니다.",
            "Stage5_Friend/Friend_Original.png", new[] { "Stage5_Friend/Friend_Wrong_XOOOO.png", "Stage5_Friend/Friend_Wrong_XXOOO.png", "Stage5_Friend/Friend_Wrong_XXXXX.png" },
            ("양 갈래 머리", "머리 양옆으로 묶은 머리 두 갈래가 구분되어야 합니다. 단발이나 한 갈래 머리는 인정하지 않습니다.", AutoCheck.PigTails, InkColor.Black, Area.TopLeft));

        // 5챕터 : 도깨비가 요청한 물건 3개 + 맞설 물건 3개 (기획서 표). 판정 포인트는 기획서 표대로 임시 (기획자 확정 전)
        Ch5Item("Ch5_Jipsin", 1, "짚신", "앞이 둥글고 길쭉한 짚신입니다. 바닥은 짚을 엮은 무늬이고 색은 갈색(세 색 섞기)입니다.",
            ("짚신 모양", "앞이 둥글고 길쭉한 신발 바닥 모양이 식별되어야 합니다. 원이나 네모는 인정하지 않습니다."),
            ("엮은 무늬", "신발 안쪽에 짚을 엮은 줄이나 격자가 여러 줄 반복되어야 합니다."),
            ("갈색", "신발의 주된 채색이 갈색 계열이어야 합니다. 노랑, 주황, 검정처럼 명확히 다른 색이면 인정하지 않습니다."));
        Ch5Item("Ch5_Bujeok", 2, "부적", "세로로 긴 노란 종이에 빨간 글씨가 적힌 부적입니다.",
            ("긴 네모 종이", "세로로 긴 사각형 종이 모양이 식별되어야 합니다."),
            ("노란 종이", "종이 바탕의 주된 채색이 노란색 계열이어야 합니다."),
            ("빨간 글씨", "종이 안에 빨간색 선이나 글씨 모양이 있어야 합니다."));
        Ch5Item("Ch5_Buchae", 3, "도깨비 부채", "부챗살이 펼쳐진 도깨비 부채입니다. 부챗살은 갈색(세 색 섞기)이고 부채 면에 무늬가 있습니다.",
            ("펼친 부채 모양", "한 점에서 퍼져 나가는 부챗살과 넓게 펼친 부채 면이 식별되어야 합니다."),
            ("부채 무늬", "부채 면 안에 줄이나 점 같은 무늬가 있어야 합니다."),
            ("갈색 부챗살", "부챗살의 주된 채색이 갈색 계열이어야 합니다."));
        Ch5Item("Ch5_DakpiBowl", 4, "닭피 그릇", "빨간 닭피가 담긴 둥근 그릇입니다. 그릇 옆면에 띠 무늬가 있습니다.",
            ("둥근 그릇 모양", "위가 넓고 아래가 둥근 그릇 모양이 식별되어야 합니다."),
            ("그릇 무늬", "그릇 옆면에 띠나 줄 무늬가 있어야 합니다."),
            ("빨간 피", "그릇 위쪽 안에 빨간색 계열로 칠한 부분이 있어야 합니다."));
        Ch5Item("Ch5_Gamtu", 5, "도깨비 감투", "뒤가 솟은 검은 감투입니다. 검정은 세 색을 섞어 만들어요.",
            ("감투 모양", "머리에 쓰는 작은 모자 모양이고 뒤쪽이 위로 솟아 있어야 합니다."),
            ("띠 무늬", "모자 아래쪽에 가로 띠가 있어야 합니다."),
            ("검정", "모자의 주된 채색이 검정에 가까운 어두운 색이어야 합니다."));
        Ch5Item("Ch5_PatBag", 6, "팥주머니", "위를 매듭으로 묶은 팥색(세 색 섞기) 주머니입니다.",
            ("주머니 모양", "아래가 둥글고 위가 오므라든 주머니 모양이 식별되어야 합니다."),
            ("매듭", "주머니 위쪽에 묶은 매듭이나 끈이 있어야 합니다."),
            ("팥색", "주머니의 주된 채색이 어두운 붉은 갈색(팥색) 계열이어야 합니다."));

        // 2~4챕터 (기획서 표 : 2 형태 + 무늬, 3 + 삼원색, 4 + 두 색 섞기). 그림은 codex 임시 그림, 판정 기준은 기획자 확인 전
        RoomItem("Ch2_Socks", 2, 1, "줄무늬 양말", 3, "발목이 길고 발끝이 옆으로 굽은 양말입니다. 양말에 가로 줄무늬가 있습니다.",
            ("양말 모양", "발목 부분과 옆으로 굽은 발끝이 이어진 L자 양말 모양이 식별되어야 합니다. 네모나 원만 그린 것은 인정하지 않습니다."),
            ("가로 줄무늬", "양말 안에 가로 방향 줄이 두 줄 이상 반복되어야 합니다. 세로줄이나 점무늬는 인정하지 않습니다."));
        RoomItem("Ch2_Umbrella", 2, 2, "물방울무늬 우산", 3, "펼친 우산입니다. 둥근 지붕 아래에 손잡이가 있고, 지붕에 물방울(점)무늬가 있습니다.",
            ("우산 모양", "위가 둥근 지붕 모양과 그 아래로 이어진 손잡이 대가 식별되어야 합니다."),
            ("물방울무늬", "우산 지붕 안에 작은 점이나 동그라미가 세 개 이상 있어야 합니다. 줄무늬는 인정하지 않습니다."));
        RoomItem("Ch2_Pillow", 2, 3, "체크무늬 베개", 3, "네모난 베개입니다. 가로줄과 세로줄이 엇갈린 체크무늬가 있습니다.",
            ("베개 모양", "모서리가 둥글거나 살짝 부푼 네모 모양이 식별되어야 합니다."),
            ("체크무늬", "베개 안에 가로줄과 세로줄이 엇갈려 작은 네모 칸이 여러 개 생겨야 합니다. 한 방향 줄만 있으면 인정하지 않습니다."));
        RoomItem("Ch3_PartyHat", 3, 1, "고깔모자", 3, "끝이 뾰족한 고깔모자입니다. 비스듬한 띠가 위에서부터 빨강, 노랑, 파랑 순서입니다.",
            ("고깔 모양", "위가 뾰족하고 아래가 넓은 삼각뿔 모자 모양이 식별되어야 합니다."),
            ("띠 무늬", "모자 안에 띠가 두 줄 이상 있어야 합니다."),
            ("빨강 · 노랑 · 파랑 띠", "띠의 색이 위에서부터 빨강, 노랑, 파랑 순서로 칠해져 있어야 합니다."));
        RoomItem("Ch3_Balloons", 3, 2, "풍선", 3, "둥근 풍선 세 개가 아래에서 한데 묶여 있습니다. 풍선은 빨강, 노랑, 파랑입니다.",
            ("풍선 세 개", "둥근 풍선 모양이 세 개 식별되어야 합니다."),
            ("묶은 끈", "풍선마다 아래로 줄이 있고, 줄이 아래에서 한 점으로 모여야 합니다."),
            ("빨강 · 노랑 · 파랑 풍선", "풍선이 빨강, 노랑, 파랑으로 하나씩 칠해져 있어야 합니다."));
        RoomItem("Ch3_GiftBox", 3, 3, "선물 상자", 3, "네모난 선물 상자입니다. 파란 상자에 빨간 리본이 묶여 있고, 노란 별무늬가 있습니다.",
            ("상자 모양", "네모난 상자와 위의 리본 매듭이 식별되어야 합니다."),
            ("별무늬", "상자 안에 별 모양이 하나 이상 있어야 합니다."),
            ("파랑 · 빨강 · 노랑", "상자는 파랑, 리본은 빨강, 별은 노랑으로 칠해져 있어야 합니다."));
        RoomItem("Ch4_Tangerine", 4, 1, "귤", 1, "동그란 주황색 귤입니다. 꼭대기에 초록색 잎이 하나 있습니다.",
            ("둥근 모양", "둥근 귤 몸통이 식별되어야 합니다."),
            ("잎", "몸통 위쪽에 잎 모양이 붙어 있어야 합니다."),
            ("주황 몸통 · 초록 잎", "몸통은 주황색(빨강 + 노랑), 잎은 초록색(노랑 + 파랑) 계열로 칠해져 있어야 합니다."));
        RoomItem("Ch4_Grapes", 4, 2, "포도", 1, "보라색 알이 위는 넓고 아래는 좁게 모인 포도송이입니다. 위에 초록색 줄기가 있습니다.",
            ("송이 모양", "알들이 모여 위가 넓고 아래가 좁은 송이 모양이 식별되어야 합니다."),
            ("알 배열", "둥근 알이 여러 개 다닥다닥 붙어 있어야 합니다."),
            ("보라 알 · 초록 줄기", "알은 보라색(빨강 + 파랑), 줄기는 초록색(노랑 + 파랑) 계열로 칠해져 있어야 합니다."));
        RoomItem("Ch4_Candy", 4, 3, "줄무늬 사탕", 1, "양 끝을 꼬아 싼 둥근 사탕입니다. 가운데에 주황색과 초록색 줄이 번갈아 있습니다.",
            ("사탕 모양", "둥근 사탕과 양 끝에 꼬인 포장이 식별되어야 합니다."),
            ("줄무늬", "사탕 가운데에 줄이 두 줄 이상 번갈아 있어야 합니다."),
            ("주황 · 초록 줄", "줄 색이 주황색(빨강 + 노랑)과 초록색(노랑 + 파랑) 계열이어야 합니다."));

        AssetDatabase.SaveAssets();
    }

    // 2~4챕터 물건 : 그림은 Sprites/Chapter{n}/{file}_Original · _Wrong1~3 (4챕터는 오답 1장, 나머지 칸은 같은 그림으로 채워요)
    static void RoomItem(string file, int chapter, int number, string target, int wrongCount, string description, params (string name, string criteria)[] features)
    {
        var full = new (string, string, AutoCheck, InkColor, Area)[features.Length];
        for (int i = 0; i < features.Length; i++) full[i] = (features[i].name, features[i].criteria, AutoCheck.LongHandle, InkColor.Black, Area.TopLeft);
        var wrongs = new string[wrongCount];
        for (int i = 0; i < wrongCount; i++) wrongs[i] = $"Chapter{chapter}/{file}_Wrong{i + 1}.png";
        CreateItem(file, number, target, description, $"Chapter{chapter}/{file}_Original.png", wrongs, full);
    }

    static void Ch5Item(string file, int number, string target, string description, params (string name, string criteria)[] features)
    {
        var full = new (string, string, AutoCheck, InkColor, Area)[features.Length];
        for (int i = 0; i < features.Length; i++) full[i] = (features[i].name, features[i].criteria, AutoCheck.LongHandle, InkColor.Black, Area.TopLeft);
        CreateItem(file, number, target, description, $"Chapter5/{file}_Original.png",
            new[] { $"Chapter5/{file}_Wrong1.png", $"Chapter5/{file}_Wrong2.png", $"Chapter5/{file}_Wrong3.png" }, full);
    }

    static void CreateItem(string file, int number, string target, string description, string original, string[] wrongs,
                           params (string name, string criteria, AutoCheck check, InkColor color, Area area)[] features)
    {
        string path = $"{DataFolder}/{file}.asset";
        var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);       // 있으면 내용을 새로 덮어써요
        bool isNew = stage == null;
        if (isNew) stage = ScriptableObject.CreateInstance<StageData>();
        stage.stageNumber = number;
        stage.targetName = target;
        stage.colorMode = ToolBar.GameplayColorMode;    // (참고용) 실제 펜 도구는 챕터 데이터의 penMode 를 써요
        stage.shapeOnly = file.StartsWith("Stage");     // 1챕터 소풍 : 형태 하나만 (서버 이름 picnic-shape-N)
        // 서버 프롬프트 이름 : 5챕터 curse-N, 2~4챕터 room-{챕터}-{N} (서버에 아직 없어요. 배포는 사용자 허락 뒤)
        stage.serverKey = file.StartsWith("Ch5_") ? "curse-" + number
                        : file.StartsWith("Ch") ? $"room-{file[2]}-{number}" : "";
        stage.targetDescription = description;
        stage.originalSprite = LoadSprite(original);
        stage.features = new Feature[features.Length];
        for (int i = 0; i < features.Length; i++)
            stage.features[i] = new Feature
            {
                id = "F" + (i + 1), name = features[i].name, criteria = features[i].criteria,
                check = features[i].check, color = features[i].color, area = features[i].area,
            };
        ItemWrongs[file] = wrongs;
        stage.wrongSprites = LoadWrongSprites(file);
        if (isNew) AssetDatabase.CreateAsset(stage, path);
        else EditorUtility.SetDirty(stage);
    }

    static readonly System.Collections.Generic.Dictionary<string, string[]> ItemWrongs = new System.Collections.Generic.Dictionary<string, string[]>();

    // 오답 3장. 없는 그림은 앞의 오답으로 채워요 (ConnectWrongArt 처럼 이름표가 없으면 지금 그림을 그대로 둬요)
    static Sprite[] LoadWrongSprites(string file)
    {
        var sprites = new Sprite[3];
        if (!ItemWrongs.TryGetValue(file, out string[] names))
        {
            var old = AssetDatabase.LoadAssetAtPath<StageData>($"{DataFolder}/{file}.asset");
            return old != null && old.wrongSprites != null ? old.wrongSprites : sprites;
        }
        for (int i = 0; i < 3; i++)
        {
            sprites[i] = i < names.Length ? LoadSprite(names[i]) : null;
            if (sprites[i] == null && i > 0) sprites[i] = sprites[i - 1];
        }
        return sprites;
    }

    // 챕터 데이터 : 1챕터 소풍 준비 (물건 5개), 2~4챕터 방 (물건 3개), 5챕터 도깨비의 저주 (요청 물건 · 맞설 물건 3쌍)
    //   챕터마다 규칙 : 펜(1 · 2 검정, 3~ 섞기), 돋보기(1챕터 없음), 사탕(4챕터 0개), 첫 그리기 안내(1 펜, 2 돋보기, 3 색), 복선
    static void CreateChapterFiles()
    {
        Chapter("Chapter1_Picnic", 1, "소풍 준비", ChapterRule.Normal, 1, "UI/Chapter_Picnic.png", "Background/BG_Room.png", "Background/BG_Picnic.png", null,
            new[] { "Stage1_Bat", "Stage2_Bag", "Stage3_Mat", "Stage4_Gimbap", "Stage5_Friend" }, null);
        Rules("Chapter1_Picnic", ColorMode.BlackOnly, magnifier: false, candies: 3, ChapterGuide.Pen);
        Chapter("Chapter2_Rain", 2, "비 오는 날 방 정리", ChapterRule.Normal, 2, "Chapter2/Chapter_Rain.png", "Background/BG_RoomRain.png", "Background/BG_RoomRain.png", null,
            new[] { "Ch2_Socks", "Ch2_Umbrella", "Ch2_Pillow" }, null);
        Rules("Chapter2_Rain", ColorMode.BlackOnly, magnifier: true, candies: 3, ChapterGuide.Magnifier, windowFire: true);
        Chapter("Chapter3_Birthday", 3, "동생 생일 파티 준비", ChapterRule.Normal, 3, "Chapter3/Chapter_Birthday.png", "Background/BG_RoomEvening.png", "Background/BG_RoomEvening.png", null,
            new[] { "Ch3_PartyHat", "Ch3_Balloons", "Ch3_GiftBox" }, null);
        Rules("Chapter3_Birthday", ColorMode.MixFree, magnifier: true, candies: 3, ChapterGuide.Mix, mood: 1);
        Chapter("Chapter4_Snack", 4, "도깨비 간식 바구니", ChapterRule.Normal, 4, "Chapter4/Chapter_Snack.png", "Background/BG_RoomNight.png", "Background/BG_RoomNight.png", null,
            new[] { "Ch4_Tangerine", "Ch4_Grapes", "Ch4_Candy" }, null);
        Rules("Chapter4_Snack", ColorMode.MixFree, magnifier: true, candies: 0, ChapterGuide.None, mood: 2, endEyes: true);
        Chapter("Chapter5_Curse", 5, "도깨비의 저주", ChapterRule.Curse, 5, "Chapter5/Chapter_Forest.png", "Chapter5/BG_ForestBright.png", "Chapter5/BG_ForestBright.png", "Chapter5/BG_ForestDark.png",
            new[] { "Ch5_Jipsin", "Ch5_Buchae", "Ch5_Gamtu" }, new[] { "Ch5_Bujeok", "Ch5_DakpiBowl", "Ch5_PatBag" });
        Rules("Chapter5_Curse", ColorMode.MixFree, magnifier: true, candies: 3, ChapterGuide.None);
        AssetDatabase.SaveAssets();
    }

    static void Rules(string file, ColorMode pen, bool magnifier, int candies, ChapterGuide guide, bool windowFire = false, int mood = 0, bool endEyes = false)
    {
        var c = LoadChapter(file);
        c.penMode = pen;
        c.hasMagnifier = magnifier;
        c.candies = candies;
        c.guide = guide;
        c.aloneLines = 2;
        c.windowFire = windowFire;
        c.dokkaebiMood = mood;
        c.endEyes = endEyes;
        EditorUtility.SetDirty(c);
    }

    static void Chapter(string file, int number, string title, ChapterRule rule, int level, string photo, string play, string end, string dark, string[] items, string[] counters)
    {
        string path = $"{DataFolder}/{file}.asset";
        var c = AssetDatabase.LoadAssetAtPath<ChapterData>(path);
        bool isNew = c == null;
        if (isNew) c = ScriptableObject.CreateInstance<ChapterData>();
        c.number = number;
        c.title = title;
        c.rule = rule;
        c.criteriaLevel = level;
        c.photo = LoadSprite(photo) ?? LoadSprite("UI/Chapter_Picnic.png");
        c.playBackground = LoadSprite(play) ?? LoadSprite("Background/BG_Room.png");
        c.endBackground = LoadSprite(end) ?? c.playBackground;
        c.darkBackground = dark == null ? null : LoadSprite(dark) ?? c.playBackground;
        c.items = System.Array.ConvertAll(items, LoadStage);
        c.counterItems = counters == null ? new StageData[0] : System.Array.ConvertAll(counters, LoadStage);
        if (isNew) AssetDatabase.CreateAsset(c, path);
        else EditorUtility.SetDirty(c);
    }

    static ChapterData LoadChapter(string file) => AssetDatabase.LoadAssetAtPath<ChapterData>($"{DataFolder}/{file}.asset");

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
            stage.wrongSprites = LoadWrongSprites(System.IO.Path.GetFileNameWithoutExtension(path));
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
            int maxSize = 4096;                                  // 기본 2048 이면 긴 그림이 줄어들어 흐려져요
            //   쪽지 질감 3장은 아주 커서(그림판 종이 3230 x 3814) 그대로 두면 메모리를 많이 먹어요.
            //   웹(WebGL)에서는 크기가 2의 거듭제곱이 아니고 밉맵이 켜진 그림은 압축이 풀려 무압축으로 들어가요.
            //   종이 결(잡티) 때문에 gzip 도 잘 안 돼서, 2048 이면 세 장이 웹 용량 약 12MB 를 차지했어요.
            //   1024 로 줄이면 약 3MB : 화면(폭 1080)에서 그림판 종이 820 x 963 · 대사 상자 1008 x 312 는 거의 1:1,
            //   채점표 972 x 1560 만 1.5 배 늘어나요 (테두리가 굵어서 티가 거의 안 나요)
            if (path.EndsWith("UI/Draw_Paper.png") || path.EndsWith("UI/Paper_Score.png") || path.EndsWith("UI/Dialogue_Box.png"))
            {
                compression = TextureImporterCompression.CompressedHQ;
                maxSize = 1024;
            }
            //   소풍 카드 그림은 화면에서 846 x 618 로만 보여서, 1024 로 줄여 넣어요 (선이 또렷하게 압축은 안 해요. 웹 용량 약 12MB → 4MB)
            if (path.EndsWith("UI/Chapter_Picnic.png") || path.Contains("/Chapter_")) maxSize = 1024;       // 챕터 사진은 모두 작게
            //   2~4챕터 방 배경 3장은 방 그림에 색을 덮은 임시 그림이라, 웹 용량을 아끼려고 2048 로 줄여요
            if (path.Contains("Background/BG_Room") && !path.EndsWith("BG_Room.png")) maxSize = 2048;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || importer.spriteBorder != border || importer.textureCompression != compression
                || !importer.mipmapEnabled || importer.maxTextureSize != maxSize || !importer.alphaIsTransparency)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.textureCompression = compression;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = maxSize;
                importer.alphaIsTransparency = true;             // 투명한 곳의 색이 가장자리에 번지지 않게
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
}

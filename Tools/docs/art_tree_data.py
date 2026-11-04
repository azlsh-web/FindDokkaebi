# 아트 트리 데이터 : 게임 그림이 어느 화면 · 오브젝트에 들어가고 어디서 왔는지 (3번 · 4번 문서가 같이 써요)
G9, G10, G11 = "기획서 그림 9", "기획서 그림 10", "기획서 그림 11과 BG_Start의 차이로 오림"
TEAM, CODEX, CODE = "아트팀", "Codex", "코드로 그림"

# 화면 > 오브젝트 > (파일, 출처). 오브젝트 이름은 Unity Hierarchy 이름이에요.
TREE = [
    ("맨 뒤 배경", "Canvas/Background", [
        ("시작 화면", "UI/../Background/BG_Start", TEAM),
        ("게임 중", "UI/../Background/BG_Room", TEAM + ", 투명한 가장자리는 주변 색으로 채움"),
    ]),
    ("HUD (게임 중 위쪽)", "Frame/HUD", [
        ("SnackRow/Snack_0~2", "UI/HUD_SnackBag", "기획서 그림 9, 쓴 간식은 왼쪽부터 흐리게"),
        ("PauseButton", "UI/HUD_PauseButton", "기획서 그림 9"),
    ]),
    ("향과 촛대 (그리는 동안)", "Frame/HUD/TimerGroup", [
        ("IncenseHolder", "UI/Incense_Holder", CODE),
        ("Incense", "UI/Incense_Stick", CODE),
        ("IncenseFx_Tip/Ember", "UI/Incense_Ember", CODE),
        ("IncenseFx_Tip/Flame", "UI/Incense_Flame", CODE),
        ("IncenseFx_Tip/Ash, FallingAsh", "UI/Incense_Ash", CODE),
        ("IncenseFx_Smoke, Glow", "UI/Common_SoftPuff", CODE),
    ]),
    ("1. 시작 화면", "Frame/TitlePanel", [
        ("TitleBoy", "Characters/Char_Boy_Normal", TEAM),
        ("TitleDokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("TitleLogo", "UI/Title_Logo", G11),
        ("TitleTagline", "UI/Title_Tagline", G11),
        ("TitleGear", "UI/Title_Gear", G11 + ", 눌러도 기능 없음"),
        ("StartButton", "UI/Title_StartButton", G11),
    ]),
    ("2. 단계 도입", "Frame/StageIntroPanel", [
        ("IntroCard, RulesCard", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("Boy", "Characters/Char_Boy_Normal", TEAM),
        ("Dokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("ObserveButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("3. 관찰", "Frame/ObservePanel", [
        ("Card", "UI/Observe_Card", G9 + ", 글자는 게임이 씀"),
        ("TargetImage", "물건별 원본 (아래 단계 폴더)", ""),
        ("숫자 칸 (지금)", "UI/Observe_CountOn", G9),
        ("숫자 칸 (다른 차례)", "UI/Observe_CountOff", G9),
        ("Number", "3 · 2 · 1 은 게임 글자 (Jua)", ""),
        ("Sparkle", "UI/Observe_Sparkle", G9),
        ("FlameDeco", "UI/Observe_FlameDeco", G9),
    ]),
    ("4. 그리기", "Frame/DrawPanel", [
        ("BoardFrame", "UI/Draw_ClipboardFrame", G10 + "을 세로로 늘리고 종이 면을 뚫음, 글자는 게임이 씀"),
        ("PaletteBoard", "UI/Draw_PaletteBoard", G10),
        ("검정", "UI/Draw_PaintBlack", G10),
        ("빨강", "UI/Draw_PaintRed", G10),
        ("노랑", "UI/Draw_PaintYellow", G10),
        ("파랑", "UI/Draw_PaintBlue", G10),
        ("MixedSlot", "UI/Draw_PaintMixed", G10 + ", 섞은 색을 곱해 칠함"),
        ("MixOpen", "UI/Draw_MixButton", G10),
        ("Pencil", "UI/Draw_PencilButton", G10),
        ("Eraser", "UI/Draw_EraserButton", G10),
        ("Undo", "UI/Draw_UndoButton", G10),
        ("Clear", "UI/Draw_ClearButton", G10),
        ("Clear 안 아이콘", "UI/Draw_TrashIcon", CODE),
        ("SizePill", "UI/Common_ButtonCream", G10),
        ("굵기 점 · 색 미리보기", "UI/Common_Circle", CODE),
        ("고른 도구 표시", "UI/Common_SelectRing", CODE + ", 시안과 같은 모양"),
        ("SubmitButton", "UI/Draw_SubmitButton", G10),
    ]),
    ("5. 색 섞기", "Frame/DrawPanel/MixerPanel", [
        ("MixerPanel", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("CloseButton", "UI/Mix_CloseIcon", CODE),
        ("Swatch", "UI/Common_Circle", CODE),
        ("ApplyButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("6. 판정 대기", "Frame/JudgePanel", [
        ("StatusCard, Note", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("Dokkaebi", "Characters/Char_Dokkaebi_Think", TEAM),
        ("Snack", "UI/HUD_SnackBag", G10),
        ("BackToDrawButton", "UI/Common_ButtonCream", G10),
    ]),
    ("7. 결과", "Frame/ResultPanel", [
        ("Dokkaebi", "Characters/Char_Dokkaebi_Swing", TEAM + ", 다른 도깨비와 크기가 맞게 자름"),
        ("ReturnedItem", "물건별 원본 또는 Wrong (아래 단계 폴더)", ""),
        ("MyCard, ResultCard, Bubble", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("BoyFace (성공)", "Characters/Char_BoyFace_Happy", TEAM + " 그림에서 얼굴만 자름"),
        ("BoyFace (실패)", "Characters/Char_BoyFace_Surprised", TEAM + " 그림에서 얼굴만 자름"),
        ("RetryButton", "UI/Common_ButtonCoral", G10),
        ("CompareButton", "UI/Common_ButtonCream", G10),
    ]),
    ("8. 비교", "Frame/ComparePanel", [
        ("MyCard, OriginalCard, HintCard", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("Original", "물건별 원본 (아래 단계 폴더)", ""),
        ("NextButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("9. 이야기 종료", "Frame/EndingPanel", [
        ("TitleCard, RecordCard", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("Boy", "Characters/Char_Boy_Happy", TEAM),
        ("Dokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("Friend", "Stage5_Friend/Friend_Original", "기획서 그림 19 캐릭터 시트"),
        ("BackToTitleButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("일시정지", "Canvas/PausePanel", [
        ("PauseCard", "UI/Common_Card", CODE + ", 시안과 같은 모양"),
        ("ResumeButton", "UI/Common_ButtonCoral", G10),
    ]),
]

STAGES = [
    ("1단계 방망이", "Stage1_Bat", "Bat", 1, "대파", TEAM),
    ("2단계 가방", "Stage2_Bag", "Bag", 2, "항아리", TEAM),
    ("3단계 돗자리", "Stage3_Mat", "Mat", 3, "파전", TEAM),
    ("4단계 김밥", "Stage4_Gimbap", "Gimbap", 4, "고무신", TEAM),
    ("5단계 친구", "Stage5_Friend", "Friend", 5, "허수아비", "기획서 그림 19 캐릭터 시트"),
]

# 아트팀에 요청하는 그림 (파일, 자리, 화면 크기, 주의할 점). 4번 문서와 apply_art_zip.py 가 같이 써요.
ZIP_FOLDER = "FindDokkaebi_Art"
REQUEST = [
    ("Incense_Holder", "향을 꽂는 촛대 (그리기 화면 오른쪽)", "125×169", "향 줄기가 위쪽 잔 안으로 들어가요. 지금처럼 금색 놋쇠 느낌으로 해 주세요."),
    ("Incense_Stick", "타들어 가는 향 줄기", "71×507", "게임이 위에서부터 잘라 내며 타들어 가요. 위아래 굵기를 일정하게, 칸 가운데에 그려 주세요."),
    ("Incense_Ember", "줄기 끝의 불씨", "70×38", "줄기와 같은 폭의 칸에 가운데 맞춰 그려 주세요."),
    ("Incense_Flame", "줄기 끝의 불꽃", "66×88", "아래쪽을 기준으로 흔들려요. 아래 끝이 칸 바닥 가운데에 닿게 그려 주세요."),
    ("Incense_Ash", "타고 남은 재", "끝 70×4, 떨어질 때 34×42", "끝에 납작하게 눌려 붙기도 하고 떨어지기도 해요. 뭉툭한 재 덩어리로 그려 주세요."),
]

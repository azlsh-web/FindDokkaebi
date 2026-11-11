# 아트 트리 데이터 : 게임 그림이 어느 화면 · 오브젝트에 들어가고 어디서 왔는지 (3번 · 4번 문서가 같이 써요)
G9, G10, G11 = "기획서 그림 9", "기획서 그림 10", "기획서 그림 11과 BG_Start의 차이로 오림"
TEAM, CODEX, CODE, TEMP = "아트팀", "Codex", "코드로 그림", "개발 임시 그림"

# 화면 > 오브젝트 > (파일, 출처). 오브젝트 이름은 Unity Hierarchy 이름이에요.
TREE = [
    ("맨 뒤 배경", "Canvas/Background", [
        ("시작 화면", "Background/BG_Start", TEAM),
        ("게임 중", "Background/BG_Room", TEAM + ", 투명한 가장자리는 주변 색으로 채움"),
        ("이야기 종료", "Background/BG_Picnic", TEAM + ", 왼쪽 가장자리의 투명한 줄은 옆 색으로 채움"),
    ]),
    ("HUD (그리기부터 채점표까지)", "Frame/HUD", [
        ("MagnifierButton (돋보기)", "UI/Magnifier_Icon", TEAM + " 렌즈가 막힌 돋보기, 여백을 잘라 냄"),
        ("ChocolateRow/Chocolate_0~1 (돋보기에 씀)", "UI/HUD_Chocolate", TEAM + ", 여백을 잘라 냄. 돋보기에 붙이고, 쓴 것은 오른쪽부터 흐리게"),
        ("CandyRow/Candy_0~1 (쪽지를 건넬 때 씀)", "UI/HUD_Candy", TEAM + ", 여백을 잘라 냄. 쓴 것은 왼쪽부터 흐리게"),
        ("PauseButton", "UI/HUD_PauseButton", G9),
    ]),
    ("향과 촛대 (그리는 동안)", "Frame/HUD/TimerGroup", [
        ("IncenseHolder", "UI/Incense_Holder", TEAM + ", 칸 안 여백을 맞춤 (fit_incense_art.py)"),
        ("Incense", "UI/Incense_Stick", TEAM + ", 칸 안 여백을 맞춤 (fit_incense_art.py)"),
        ("IncenseFx_Tip/Ember", "UI/Incense_Ember", TEAM),
        ("IncenseFx_Tip/Flame", "UI/Incense_Flame", TEAM + ", 칸 안 여백을 맞춤 (fit_incense_art.py)"),
        ("IncenseFx_Tip/Ash, FallingAsh", "UI/Incense_Ash", TEAM + ", 칸 안 여백을 맞춤 (fit_incense_art.py)"),
        ("IncenseFx_Smoke, Glow", "UI/Common_SoftPuff", CODE),
    ]),
    ("1. 시작", "Frame/TitlePanel", [
        ("TitleBoy", "Characters/Char_Boy_Normal", TEAM),
        ("TitleDokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("TitleLogo", "UI/Title_Logo", G11 + ", 문구를 지움"),
        ("StartButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("2. 대화 + 오프닝 + 건네기", "Frame/DialoguePanel", [
        ("Boy", "Characters/Char_Boy_Normal", TEAM),
        ("Dokkaebi (1단계)", "Characters/Char_Dokkaebi_NoBat", TEAM),
        ("Dokkaebi (2단계부터)", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("GiveNote, DialogueBox", "UI/Common_Card", CODE),
        ("Opening/Bag · Mat · Gimbap", "물건별 원본 (아래 단계 폴더)", ""),
        ("Opening/Idea/Pencil", "UI/Draw_PencilButton", TEAM),
        ("Opening/Idea/Snack, SnackOut", "UI/HUD_Candy", TEAM),
        ("DialogueBox/Face (주인공 기쁨)", "Characters/Char_BoyFace_Happy", TEAM + " 그림에서 얼굴만 자름"),
        ("DialogueBox/Face (주인공 놀람)", "Characters/Char_BoyFace_Surprised", TEAM + " 그림에서 얼굴만 자름"),
        ("DialogueBox/Face (주인공 울음 · 결과 화면에서 틀렸을 때)", "Characters/Char_BoyFace_Sad", TEAM + " 그림에서 얼굴만 자름"),
        ("(아직 안 씀) 우는 주인공 전신", "Characters/Char_Boy_Sad", TEAM + ", 얼굴만 잘라 위 얼굴로 씀"),
        ("DialogueBox/Face (도깨비)", "Characters/Char_DokkaebiFace_Normal", TEAM),
        ("DialogueBox/Face (도깨비 뿌듯 · 맞았을 때)", "Characters/Char_DokkaebiFace_Proud", TEAM),
        ("DialogueBox/Face (도깨비 놀림 · 틀렸을 때)", "Characters/Char_DokkaebiFace_Tease", TEAM),
        ("DialogueBox/Arrow", "UI/Common_NextArrow", CODE),
    ]),
    ("3. 떠올리기", "Frame/ObservePanel", [
        ("Card (말풍선)", "UI/Common_Card", CODE),
        ("BoyRecall/Boy", "Characters/Char_Boy_Normal", TEAM),
        ("DokkaebiRecall/Dokkaebi (1단계)", "Characters/Char_Dokkaebi_NoBat", TEAM),
        ("TargetImage", "물건별 원본 (아래 단계 폴더)", ""),
        ("CountText", "3 · 2 · 1 은 게임 글자 (Jua)", ""),
    ]),
    ("4. 그리기", "Frame/DrawPanel", [
        ("BoardFrame", "UI/Draw_ClipboardFrame", G10 + "을 세로로 늘리고 종이 면을 뚫음"),
        ("PaletteBoard", "UI/Draw_PaletteBoard", G10),
        ("검정", "UI/Draw_PaintBlack", G10),
        ("빨강", "UI/Draw_PaintRed", G10),
        ("노랑", "UI/Draw_PaintYellow", G10),
        ("파랑", "UI/Draw_PaintBlue", G10),
        ("MixedSlot", "UI/Draw_PaintMixed", G10 + ", 섞은 색을 곱해 칠함"),
        ("MixOpen", "UI/Draw_MixButton", G10),
        ("Pencil", "UI/Draw_PencilButton", G10 + ", 아이콘 크기를 맞춤"),
        ("Eraser", "UI/Draw_EraserButton", G10 + ", 아이콘 크기를 맞춤"),
        ("Undo", "UI/Draw_UndoButton", G10 + ", 아이콘 크기를 맞춤"),
        ("Clear", "UI/Draw_ClearButton", G10 + ", 휴지통 아이콘은 코드로 그림"),
        ("SizePill", "UI/Common_ButtonCream", G10),
        ("굵기 점 · 색 미리보기", "UI/Common_Circle", CODE),
        ("고른 도구 표시", "UI/Common_SelectRing", CODE),
        ("SubmitButton", "UI/Draw_SubmitButton", G10),
        ("MagnifierPeek/Lens/Frame", "UI/Magnifier_Frame", TEAM + " 렌즈가 뚫린 돋보기, 둘레의 옅은 막을 지움"),
        ("MagnifierPeek/Lens/Hole (렌즈 모양, 안에 원본)", "UI/Common_Circle", CODE),
    ]),
    ("5. 색 섞기", "Frame/DrawPanel/MixerPanel", [
        ("MixerPanel", "UI/Common_Card", CODE),
        ("CloseButton", "UI/Mix_CloseIcon", CODE),
        ("Preview", "UI/Common_Circle", CODE),
        ("UndoButton", "UI/Draw_UndoButton", G10 + ", 아이콘 크기를 맞춤"),
        ("ApplyButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("6. 판정 대기", "Frame/JudgePanel", [
        ("Dokkaebi", "Characters/Char_Dokkaebi_Think", TEAM),
    ]),
    ("7. 결과", "Frame/ResultPanel", [
        ("Dokkaebi (성공)", "Characters/Char_Dokkaebi_Swing", TEAM),
        ("Dokkaebi (1단계 실패)", "Characters/Char_Dokkaebi_NoBat", TEAM),
        ("ReturnedItem", "물건별 원본 또는 Wrong (아래 단계 폴더)", ""),
        ("RetryButton 아이콘", "UI/Result_IconRetry", CODE),
        ("RetryButton/Cost", "UI/HUD_Candy", TEAM),
        ("GiveUpButton 아이콘", "UI/Result_IconGiveUp", CODE),
    ]),
    ("8. 채점표", "Frame/ComparePanel", [
        ("Circle", "UI/Score_Circle", CODE),
        ("Slash", "UI/Score_Slash", CODE),
        ("OriginalCard", "물건별 원본 (아래 단계 폴더)", ""),
    ]),
    ("9. 이야기 종료", "Frame/EndingPanel", [
        ("Boy", "Characters/Char_Boy_Happy", TEAM),
        ("Candy_1~10 (단계마다 남은 사탕을 더해 돗자리에)", "UI/HUD_Candy", TEAM),
        ("Chocolate_1~10 (단계마다 남은 초콜릿을 더해 돗자리에)", "UI/HUD_Chocolate", TEAM),
        ("FriendSlot", "물건별 원본 (아래 단계 폴더)", ""),
        ("Dokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("Dokkaebi (방망이를 못 찾았을 때)", "Characters/Char_Dokkaebi_NoBat", TEAM),
        ("Mat · Bag · Gimbap · BatSlot", "받은 물건 (아래 단계 폴더)", ""),
    ]),
]

STAGES = [
    ("1단계 방망이", "Stage1_Bat", "Bat", 1, "대파", TEAM),
    ("2단계 가방", "Stage2_Bag", "Bag", 2, "항아리", TEAM),
    ("3단계 돗자리", "Stage3_Mat", "Mat", 3, "파전", TEAM),
    ("4단계 김밥", "Stage4_Gimbap", "Gimbap", 4, "고무신", TEAM),
    ("5단계 친구", "Stage5_Friend", "Friend", 5, "허수아비", "기획서 그림 19 캐릭터 시트"),
]

# 아트팀에 요청하는 그림. 4번 문서와 apply_art_zip.py 가 같이 써요.
#   (파일, 폴더, 묶음, 크기, 자리, 그려 줄 내용)
ZIP_FOLDER = "FindDokkaebi_Art"
GROUPS = [
    ("먼저", "먼저 필요한 그림", "지금 개발 쪽 임시 그림이라 전시에서 가장 눈에 띄어요. 이야기 종료 화면 전체를 차지해요."),
    ("향", "시간이 되면 부탁드리는 그림", "이전에 요청한 향과 촛대예요. 늦으면 지금 그림으로 출품해요."),
    ("선택", "있으면 좋은 그림", "도깨비 표정이 하나뿐이라 대사와 표정이 어울리지 않을 때가 있어요. 받으면 개발 쪽에서 코드를 연결해요."),
]
REQUEST = [
    ("BG_Picnic", "Background", "먼저", (2248, 3952), "이야기 종료 배경", "소풍 잔디밭. 화면 아래쪽 넓게 잔디와 돗자리가 놓일 자리가 필요해요. 캐릭터와 물건을 위에 따로 올리니 배경만 그려 주세요. 다른 배경처럼 투명 없이 꽉 채워 주세요."),
    ("Incense_Holder", "UI", "향", (130, 176), "그리기 화면 오른쪽의 촛대", "향 줄기가 위쪽 잔 안으로 들어가요. 금색 놋쇠 느낌으로 그려 주세요."),
    ("Incense_Stick", "UI", "향", (74, 552), "타들어 가는 향 줄기", "게임이 위에서부터 잘라 내며 타들어 가요. 위아래 굵기를 일정하게, 칸 가운데에 그려 주세요."),
    ("Incense_Ember", "UI", "향", (74, 40), "줄기 끝의 불씨", "줄기와 같은 폭의 칸에 가운데 맞춰 그려 주세요."),
    ("Incense_Flame", "UI", "향", (90, 120), "줄기 끝의 불꽃", "아래쪽을 기준으로 흔들려요. 아래 끝이 칸 바닥 가운데에 닿게 그려 주세요."),
    ("Incense_Ash", "UI", "향", (74, 150), "타고 남은 재", "끝에 납작하게 붙기도 하고 떨어지기도 해요. 뭉툭한 재 덩어리로 그려 주세요."),
    ("Char_DokkaebiFace_Normal", "Characters", "선택", (660, 560), "도깨비 대사의 기본 얼굴", "대사 상자에 작게 나오는 얼굴. 받은 그림을 넣었어요."),
    ("Char_DokkaebiFace_Tease", "Characters", "선택", (660, 560), "틀렸을 때 도깨비 대사", "엉뚱한 물건을 내밀며 놀리는 얼굴. 받은 그림을 넣었어요. 예) 「짠! 네 그림이랑 똑같지?」"),
    ("Char_DokkaebiFace_Proud", "Characters", "선택", (660, 560), "맞았을 때 도깨비 대사", "뿌듯한 얼굴. 받은 그림을 넣었어요. 예) 「가방 여기 있다! 어때, 나 잘 찾지?」"),
]

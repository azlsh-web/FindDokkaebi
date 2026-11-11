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

# 아트팀에 요청하는 그림. 4번 문서(make_art_request.py)와 apply_art_zip.py 가 같이 써요.
#   (파일, 폴더, 묶음, 크기, 자리, 그려 줄 내용)
#   이전 요청 9장(소풍 배경, 향과 촛대, 도깨비 얼굴 3장)은 모두 받아서 넣었어요 (2026-11-08).
REQUEST = [
    ("Paper_Note", "UI", "쪽지", (1024, 1024), "대사 상자, 건네는 쪽지, 떠올리기 칸, 채점표와 그 안의 두 칸, 색 섞기 판, 일시정지 판",
     "쪽지 한 장. 지금 흰 카드(`Common_Card`) 자리에 모두 이 그림이 깔려요. 판마다 크기가 달라서 게임이 늘려 써요. "
     "**바깥 120px 띠**에 테두리와 귀퉁이를 그려 주세요. 귀퉁이 네 곳은 그대로 쓰고, 위아래 띠는 가로로, 양옆 띠는 세로로 반복해서 이어 붙여요. "
     "**가운데 784×784**는 바둑판처럼 반복해서 깔아요. 이어 붙여도 이음매가 안 보이게 무늬가 끝에서 끝으로 이어져야 해요. "
     "종이 색은 지금 크림색 근처로, 결은 아주 옅게 그려 주세요. 위에 글자와 그림이 올라가요. "
     "다른 그림처럼 갈색 외곽선이 있으면 잘 어울려요. 귀퉁이 바깥은 투명하게 해 주세요."),
    ("Draw_Paper", "UI", "그림판", (1615, 1907), "그리기 화면 클립보드 안의 종이",
     "아이가 그림을 그리는 종이 면. 클립보드 틀(`Draw_ClipboardFrame`) 뒤에 깔려서 가장자리 40px쯤은 틀에 가려요. "
     "`Paper_Note`와 같은 종이로 보이게, 투명 없이 꽉 채워 주세요. "
     "결은 `Paper_Note`보다도 옅게 해 주세요. 검정 · 노랑 같은 옅은 선도 잘 보여야 해요. 줄이나 모눈은 넣지 말아 주세요."),
]

# 투명한 부분 없이 꽉 채우는 그림 (apply_art_zip.py 가 투명 검사를 건너뛰어요). Background 폴더는 모두 꽉 채워요.
OPAQUE = {"Draw_Paper"}

ZIP_FOLDER = "FindDokkaebi_Art"
GROUPS = [
    ("쪽지", "먼저 필요한 그림", "흰 판이 거의 모든 화면에 나와서 게임 분위기와 가장 동떨어져 보여요. 이 한 장만 와도 대부분의 흰 판이 쪽지로 바뀌어요."),
    ("그림판", "다음으로 필요한 그림", "그리기 화면에서 가장 큰 면이에요. 지금은 코드가 크림색 한 가지로 칠하고 있어요."),
]

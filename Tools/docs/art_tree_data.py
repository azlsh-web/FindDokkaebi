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
        ("MagnifierBadge (쓸 수 있는 돋보기 수)", "UI/Common_Circle", "코드로 그린 동그라미, 숫자는 글꼴"),
        ("MagnifierHighlight/Orbit/Fire_0~2 (1단계 시범 때 돋보기 둘레를 도는 도깨비불)", "UI/Magnifier_DokkaebiFire", TEMP + ", 향 불꽃을 푸른빛으로 바꿈 (make_candy_jar.py)"),
        ("CandyRow/Candy_0~2 (쪽지를 건넬 때 씀)", "UI/HUD_Candy", TEAM + ", 여백을 잘라 냄. 쓴 것은 왼쪽부터 흐리게"),
        ("SettingsButton (소리 설정, 시작 · 챕터 화면에도)", "UI/HUD_SettingsButton", "기획 단계에 받은 톱니 그림 (Title_Gear)"),
        ("SettingsPanel/SettingsCard/CloseButton (소리 설정 닫기)", "UI/Mix_CloseIcon", CODE),
    ]),
    ("남은 시간 게이지 (그리는 동안, 위 줄과 쪽지 사이에 화면 가로 가득, 숫자 없음)", "Frame/HUD/TimerGroup", [
        ("GaugeSlot (막대 그림을 옅게 칠한 트랙, 막대가 줄어든 자리가 보임)", "UI/HUD_CandleGauge", TEAM + ", 막대와 같은 그림"),
        ("Incense (촛대 막대, 오른쪽 끝부터 줄어듦)", "UI/HUD_CandleGauge", TEAM + ", 11/18 촛대 그림 한 장을 막대 · 불꽃으로 나눔 (cut_candle_gauge.py)"),
        ("Incense/IncenseFx_Tip/IncenseFx_Flame (막대 끝을 따라가는 불꽃 핸들)", "UI/HUD_CandleFlame", TEAM + ", 같은 촛대 그림에서 오린 불꽃"),
        ("(지금은 안 씀) 전에 쓰던 향 줄기", "UI/Incense_Stick", TEAM + ", 11/14 게이지로 바꾸며 뺐어요. 파일은 남겨 둬요"),
        ("(숨김) IncenseFx_Tip/Ember", "UI/Incense_Ember", TEAM + ", 가로 게이지에서는 쓰지 않아요"),
        ("(지금은 안 씀) 전에 쓰던 향 불꽃", "UI/Incense_Flame", TEAM + ", 11/18 촛대 불꽃으로 바꿨어요. 파일은 남겨 둬요"),
        ("(숨김) IncenseFx_Tip/Glow", "UI/Common_SoftPuff", CODE + ", 11/15 그냥 바로 바꾸며 숨겼어요"),
        ("(지금은 안 씀) IncenseFx_Tip/Ash, FallingAsh", "UI/Incense_Ash", TEAM + ", 가로 게이지에서는 쓰지 않아요"),
    ]),
    ("1. 시작 (로비 씬)", "Lobby/Frame/StartScreen", [
        ("TitleBoy", "Characters/Char_Boy_Normal", TEAM),
        ("TitleDokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("TitleLogo", "UI/Title_Logo", G11 + ", 문구를 지움"),
        ("StartButton", "UI/Common_ButtonCoral", G10),
    ]),
    ("1-2. 챕터 선택 (로비 씬)", "Lobby/Frame/ChapterScreen", [
        ("Chapter_1/Picture (소풍 카드 그림)", "UI/Chapter_Picnic", TEAM),
        ("Chapter_5/Picture (도깨비의 저주 사진)", "Chapter5/Chapter_Forest", CODEX + " 임시 그림 (11/18)"),
        ("Chapter_2/Picture (비 오는 날 방 정리 사진)", "Chapter2/Chapter_Rain", CODEX + " 임시 그림 (11/18)"),
        ("Chapter_3/Picture (동생 생일 파티 사진)", "Chapter3/Chapter_Birthday", CODEX + " 임시 그림 (11/18)"),
        ("Chapter_4/Picture (도깨비 간식 바구니 사진)", "Chapter4/Chapter_Snack", CODEX + " 임시 그림 (11/18)"),
        ("(지금은 안 씀) 자물쇠", "UI/Chapter_Lock", TEMP + ". 11/18 못 연 챕터를 흑백 사진으로 바꾸며 뺐어요 (재질 UI_Grayscale)"),
    ]),
    ("2. 대화 + 오프닝 + 건네기", "Frame/DialoguePanel", [
        ("Boy", "Characters/Char_Boy_Normal", TEAM),
        ("Dokkaebi (1단계)", "Characters/Char_Dokkaebi_NoBat", TEAM),
        ("Dokkaebi (2단계부터)", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("DialogueBox (모든 대사 상자)", "UI/Dialogue_Box", TEAM),
        ("GiveNote (건네는 쪽지, 그림판 종이를 줄여서)", "UI/Draw_Paper", TEAM),
        ("Opening/Idea/Note (오프닝 쪽지, 그림판 종이를 줄여서)", "UI/Draw_Paper", TEAM),
        ("Opening/Bag · Mat · Gimbap", "물건별 원본 (아래 단계 폴더)", ""),
        ("Opening/Idea/Pencil", "UI/Draw_PencilButton", TEAM),
        ("Opening/Idea/Snack, SnackOut", "UI/HUD_Candy", TEAM),
        ("DialogueBox/Face (주인공 기쁨)", "Characters/Char_BoyFace_Happy", TEAM + " 얼굴 그림 (BoyFace)"),
        ("DialogueBox/Face (주인공 놀람)", "Characters/Char_BoyFace_Surprised", TEAM + " 얼굴 그림 (BoyFace)"),
        ("DialogueBox/Face (주인공 보통 · 물건 생김새 설명)", "Characters/Char_BoyFace_Normal", TEAM + " 얼굴 그림 (BoyFace)"),
        ("DialogueBox/Face (주인공 울음 · 결과 화면에서 틀렸을 때)", "Characters/Char_BoyFace_Sad", TEAM + " 얼굴 그림 (BoyFace)"),
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
        ("Desk/Paper (쪽지, 두 손가락으로 키우기 · 돌리기 · 옮기기)", "UI/Draw_Paper", TEAM + ", 둥근 테두리 바깥을 투명하게 (cut_paper_corners.py). 그린 선은 위의 투명한 층(Board)에 따로 그려요"),
        ("FitButton (맞춤, 쪽지를 움직였을 때만)", "UI/Draw_FitIcon", TEMP + " (make_candy_jar.py)"),
        ("ToolBar (아래 띠)", "단색 + 진갈색 윗줄", CODE),
        ("ToolBar/Brush/Body (붓)", "UI/Draw_Brush", TEMP + ", 붓 끝이 왼쪽 아래를 봐요 (make_candy_jar.py)"),
        ("ToolBar/Brush/Tip (붓 끝, 지금 색으로 칠함)", "UI/Draw_BrushTip", TEMP + " (make_candy_jar.py)"),
        ("ToolBar/Undo · PaintUndo (되돌리기 · 물감 빼기)", "UI/Draw_UndoIcon", TEMP + ", 굽은 화살표 (make_candy_jar.py)"),
        ("ToolBar/Redo (다시 하기)", "UI/Draw_RedoIcon", TEMP + ", 굽은 화살표 (make_candy_jar.py)"),
        ("ToolBar/SubmitButton (다 그렸어!)", "UI/Common_ButtonCoral", G10),
        ("ToolBar/Palette/Card (팔레트 한 줄)", "UI/Common_ButtonCream", G10 + ", 색을 입힘"),
        ("Palette 색 · 굵기 점 · 넣은 횟수 · 날아가는 물방울", "UI/Common_Circle", CODE),
        ("Palette 고른 색 · 굵기 고리", "UI/Common_SelectRing", CODE),
        ("MagnifierPeek/Lens/Frame", "UI/Magnifier_Frame", TEAM + " 렌즈가 뚫린 돋보기, 둘레의 옅은 막을 지움"),
        ("MagnifierPeek/Lens/Hole (렌즈 모양, 안에 원본)", "UI/Common_Circle", CODE),
        ("MagnifierPeek/Lens/TimeRing (남은 시간, 시계 방향으로 줄어듦)", "UI/Magnifier_TimeRing", TEMP + ", 돋보기 테두리의 크림색 띠를 오림 (make_candy_jar.py)"),
    ]),
    ("6. 판정 대기", "Frame/JudgePanel", [
        ("MyDrawingBack (건네받은 쪽지)", "UI/Draw_Paper", TEAM),
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
        ("Paper (채점표 바깥 판)", "UI/Paper_Score", TEAM),
        ("MyCard (칸 없이 내가 그린 선만)", "그림판의 선 층", ""),
        ("Circle", "UI/Score_Circle", CODE),
        ("Slash", "UI/Score_Slash", CODE),
        ("OriginalCard", "물건별 원본 (아래 단계 폴더)", ""),
    ]),
    ("9. 이야기 종료", "Frame/EndingPanel", [
        ("Boy", "Characters/Char_Boy_Happy", TEAM),
        ("CandyJar/Back · Front (사탕 통, 사탕을 사이에 끼워요)", "UI/Ending_CandyJarBack", TEMP + " (make_candy_jar.py). 앞쪽 그림은 Ending_CandyJarFront"),
        ("CandyJar/Front", "UI/Ending_CandyJarFront", TEMP + " (make_candy_jar.py)"),
        ("CandyJar/Candy_1~15 (단계마다 남은 사탕을 더해 통에)", "UI/HUD_Candy", TEAM),
        ("FriendSlot", "물건별 원본 (아래 단계 폴더)", ""),
        ("Dokkaebi", "Characters/Char_Dokkaebi_Normal", TEAM),
        ("Dokkaebi (방망이를 못 찾았을 때)", "Characters/Char_Dokkaebi_NoBat", TEAM),
        ("Mat · Bag · Gimbap · BatSlot", "받은 물건 (아래 단계 폴더)", ""),
    ]),
    ("2~4챕터 · 방 (임시 그림, 11/18)", "Frame (2~4챕터)", [
        ("Background (비 오는 방 · 2챕터)", "Background/BG_RoomRain", TEMP + ", BG_Room 에 색을 덮음 (make_room_variants.py)"),
        ("Background (저녁 방 · 3챕터)", "Background/BG_RoomEvening", TEMP + ", BG_Room 에 색을 덮음 (make_room_variants.py)"),
        ("Background (밤 방 · 4챕터)", "Background/BG_RoomNight", TEMP + ", BG_Room 에 색을 덮음 (make_room_variants.py)"),
        ("DialoguePanel/WindowFire (2챕터 창밖 도깨비불)", "UI/Magnifier_DokkaebiFire", TEMP + ", 돋보기 시범 도깨비불을 그대로"),
        ("DrawPanel/GuideHighlight/Orbit/Fire_0~2 (1챕터 펜 · 3챕터 색 안내 도깨비불)", "UI/Magnifier_DokkaebiFire", TEMP),
        ("RoomEndPanel/RedEyes (4챕터 창 안 빨간 눈)", "Chapter4/Room_RedEyes", CODE),
        ("RoomEndPanel/Boy", "Characters/Char_Boy_Happy", TEAM),
        ("Char_Dokkaebi_Normal (3챕터 주황 머리)", "Characters/Mood/Char_Dokkaebi_Normal_M1", TEMP + ", Char_Dokkaebi_Normal 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_Normal (4챕터 붉음 머리)", "Characters/Mood/Char_Dokkaebi_Normal_M2", TEMP + ", Char_Dokkaebi_Normal 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_NoBat (3챕터 주황 머리)", "Characters/Mood/Char_Dokkaebi_NoBat_M1", TEMP + ", Char_Dokkaebi_NoBat 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_NoBat (4챕터 붉음 머리)", "Characters/Mood/Char_Dokkaebi_NoBat_M2", TEMP + ", Char_Dokkaebi_NoBat 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_Swing (3챕터 주황 머리)", "Characters/Mood/Char_Dokkaebi_Swing_M1", TEMP + ", Char_Dokkaebi_Swing 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_Swing (4챕터 붉음 머리)", "Characters/Mood/Char_Dokkaebi_Swing_M2", TEMP + ", Char_Dokkaebi_Swing 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_Think (3챕터 주황 머리)", "Characters/Mood/Char_Dokkaebi_Think_M1", TEMP + ", Char_Dokkaebi_Think 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_Dokkaebi_Think (4챕터 붉음 머리)", "Characters/Mood/Char_Dokkaebi_Think_M2", TEMP + ", Char_Dokkaebi_Think 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_DokkaebiFace_Normal (3챕터 주황 머리)", "Characters/Mood/Char_DokkaebiFace_Normal_M1", TEMP + ", Char_DokkaebiFace_Normal 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_DokkaebiFace_Normal (4챕터 붉음 머리)", "Characters/Mood/Char_DokkaebiFace_Normal_M2", TEMP + ", Char_DokkaebiFace_Normal 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_DokkaebiFace_Proud (3챕터 주황 머리)", "Characters/Mood/Char_DokkaebiFace_Proud_M1", TEMP + ", Char_DokkaebiFace_Proud 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_DokkaebiFace_Proud (4챕터 붉음 머리)", "Characters/Mood/Char_DokkaebiFace_Proud_M2", TEMP + ", Char_DokkaebiFace_Proud 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_DokkaebiFace_Tease (3챕터 주황 머리)", "Characters/Mood/Char_DokkaebiFace_Tease_M1", TEMP + ", Char_DokkaebiFace_Tease 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Char_DokkaebiFace_Tease (4챕터 붉음 머리)", "Characters/Mood/Char_DokkaebiFace_Tease_M2", TEMP + ", Char_DokkaebiFace_Tease 의 노란 머리 색만 바꿈 (make_dokkaebi_mood.py)"),
        ("Socks Original", "Chapter2/Ch2_Socks_Original", CODEX + " 임시 그림"),
        ("Socks Wrong1", "Chapter2/Ch2_Socks_Wrong1", CODEX + " 임시 그림"),
        ("Socks Wrong2", "Chapter2/Ch2_Socks_Wrong2", CODEX + " 임시 그림"),
        ("Socks Wrong3", "Chapter2/Ch2_Socks_Wrong3", CODEX + " 임시 그림"),
        ("Umbrella Original", "Chapter2/Ch2_Umbrella_Original", CODEX + " 임시 그림"),
        ("Umbrella Wrong1", "Chapter2/Ch2_Umbrella_Wrong1", CODEX + " 임시 그림"),
        ("Umbrella Wrong2", "Chapter2/Ch2_Umbrella_Wrong2", CODEX + " 임시 그림"),
        ("Umbrella Wrong3", "Chapter2/Ch2_Umbrella_Wrong3", CODEX + " 임시 그림"),
        ("Pillow Original", "Chapter2/Ch2_Pillow_Original", CODEX + " 임시 그림"),
        ("Pillow Wrong1", "Chapter2/Ch2_Pillow_Wrong1", CODEX + " 임시 그림"),
        ("Pillow Wrong2", "Chapter2/Ch2_Pillow_Wrong2", CODEX + " 임시 그림"),
        ("Pillow Wrong3", "Chapter2/Ch2_Pillow_Wrong3", CODEX + " 임시 그림"),
        ("PartyHat Original", "Chapter3/Ch3_PartyHat_Original", CODEX + " 임시 그림"),
        ("PartyHat Wrong1", "Chapter3/Ch3_PartyHat_Wrong1", CODEX + " 임시 그림"),
        ("PartyHat Wrong2", "Chapter3/Ch3_PartyHat_Wrong2", CODEX + " 임시 그림"),
        ("PartyHat Wrong3", "Chapter3/Ch3_PartyHat_Wrong3", CODEX + " 임시 그림"),
        ("Balloons Original", "Chapter3/Ch3_Balloons_Original", CODEX + " 임시 그림"),
        ("Balloons Wrong1", "Chapter3/Ch3_Balloons_Wrong1", CODEX + " 임시 그림"),
        ("Balloons Wrong2", "Chapter3/Ch3_Balloons_Wrong2", CODEX + " 임시 그림"),
        ("Balloons Wrong3", "Chapter3/Ch3_Balloons_Wrong3", CODEX + " 임시 그림"),
        ("GiftBox Original", "Chapter3/Ch3_GiftBox_Original", CODEX + " 임시 그림"),
        ("GiftBox Wrong1", "Chapter3/Ch3_GiftBox_Wrong1", CODEX + " 임시 그림"),
        ("GiftBox Wrong2", "Chapter3/Ch3_GiftBox_Wrong2", CODEX + " 임시 그림"),
        ("GiftBox Wrong3", "Chapter3/Ch3_GiftBox_Wrong3", CODEX + " 임시 그림"),
        ("Tangerine Original", "Chapter4/Ch4_Tangerine_Original", CODEX + " 임시 그림"),
        ("Tangerine Wrong1", "Chapter4/Ch4_Tangerine_Wrong1", CODEX + " 임시 그림"),
        ("Grapes Original", "Chapter4/Ch4_Grapes_Original", CODEX + " 임시 그림"),
        ("Grapes Wrong1", "Chapter4/Ch4_Grapes_Wrong1", CODEX + " 임시 그림"),
        ("Candy Original", "Chapter4/Ch4_Candy_Original", CODEX + " 임시 그림"),
        ("Candy Wrong1", "Chapter4/Ch4_Candy_Wrong1", CODEX + " 임시 그림"),
    ]),
    ("5챕터 · 도깨비의 저주 (codex 임시 그림, 11/18)", "Frame (5챕터)", [
        ("Background (밝은 숲)", "Chapter5/BG_ForestBright", CODEX + " 임시 그림"),
        ("Background (어두운 숲)", "Chapter5/BG_ForestDark", CODEX + " 임시 그림"),
        ("흑화 도깨비 (빈손)", "Chapter5/Char_Dokkaebi_Dark", CODEX + ", Char_Dokkaebi_NoBat 색을 바꿈"),
        ("흑화 도깨비 (방망이)", "Chapter5/Char_Dokkaebi_DarkBat", CODEX + ", Char_Dokkaebi_Normal 색을 바꿈"),
        ("DialoguePanel/Friend (영혼을 빼앗긴 친구)", "Chapter5/Char_Friend_Spirit", CODEX + ", Friend_Original 을 흐리게"),
        ("HUD/HeartGroup/Heart_0~2 (도깨비 하트)", "Chapter5/HUD_Heart", CODEX + " 임시 그림"),
        ("CurseEndPanel/Silhouette (게임오버)", "Chapter5/GameOver_Silhouette", CODEX + " 임시 그림"),
        ("ScareOverlay/Face (점프스케어)", "Chapter5/Scare_Face", CODEX + " 임시 그림"),
        ("1챕터 방망이 오답 2 (나무 숟가락)", "Chapter5/Ch1_Bat_Wrong2", CODEX + " 임시 그림"),
        ("1챕터 방망이 오답 3 (당근)", "Chapter5/Ch1_Bat_Wrong3", CODEX + " 임시 그림"),
        ("Jipsin Original", "Chapter5/Ch5_Jipsin_Original", CODEX + " 임시 그림"),
        ("Jipsin Wrong1", "Chapter5/Ch5_Jipsin_Wrong1", CODEX + " 임시 그림"),
        ("Jipsin Wrong2", "Chapter5/Ch5_Jipsin_Wrong2", CODEX + " 임시 그림"),
        ("Jipsin Wrong3", "Chapter5/Ch5_Jipsin_Wrong3", CODEX + " 임시 그림"),
        ("Bujeok Original", "Chapter5/Ch5_Bujeok_Original", CODEX + " 임시 그림"),
        ("Bujeok Wrong1", "Chapter5/Ch5_Bujeok_Wrong1", CODEX + " 임시 그림"),
        ("Bujeok Wrong2", "Chapter5/Ch5_Bujeok_Wrong2", CODEX + " 임시 그림"),
        ("Bujeok Wrong3", "Chapter5/Ch5_Bujeok_Wrong3", CODEX + " 임시 그림"),
        ("Buchae Original", "Chapter5/Ch5_Buchae_Original", CODEX + " 임시 그림"),
        ("Buchae Wrong1", "Chapter5/Ch5_Buchae_Wrong1", CODEX + " 임시 그림"),
        ("Buchae Wrong2", "Chapter5/Ch5_Buchae_Wrong2", CODEX + " 임시 그림"),
        ("Buchae Wrong3", "Chapter5/Ch5_Buchae_Wrong3", CODEX + " 임시 그림"),
        ("DakpiBowl Original", "Chapter5/Ch5_DakpiBowl_Original", CODEX + " 임시 그림"),
        ("DakpiBowl Wrong1", "Chapter5/Ch5_DakpiBowl_Wrong1", CODEX + " 임시 그림"),
        ("DakpiBowl Wrong2", "Chapter5/Ch5_DakpiBowl_Wrong2", CODEX + " 임시 그림"),
        ("DakpiBowl Wrong3", "Chapter5/Ch5_DakpiBowl_Wrong3", CODEX + " 임시 그림"),
        ("Gamtu Original", "Chapter5/Ch5_Gamtu_Original", CODEX + " 임시 그림"),
        ("Gamtu Wrong1", "Chapter5/Ch5_Gamtu_Wrong1", CODEX + " 임시 그림"),
        ("Gamtu Wrong2", "Chapter5/Ch5_Gamtu_Wrong2", CODEX + " 임시 그림"),
        ("Gamtu Wrong3", "Chapter5/Ch5_Gamtu_Wrong3", CODEX + " 임시 그림"),
        ("PatBag Original", "Chapter5/Ch5_PatBag_Original", CODEX + " 임시 그림"),
        ("PatBag Wrong1", "Chapter5/Ch5_PatBag_Wrong1", CODEX + " 임시 그림"),
        ("PatBag Wrong2", "Chapter5/Ch5_PatBag_Wrong2", CODEX + " 임시 그림"),
        ("PatBag Wrong3", "Chapter5/Ch5_PatBag_Wrong3", CODEX + " 임시 그림"),
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
    ("Dialogue_Box", "UI", "쪽지", (2098, 649), "대사 상자 (대화 · 결과 · 이야기 종료, 모든 대사)",
     "대사가 나오는 상자. 모든 화면에서 같은 크기 · 같은 자리라 늘리지 않고 그대로 써요. "
     "게임이 위에 올리는 것: **왼쪽 550px**에 말하는 사람 얼굴, **오른쪽 아래 200px**쯤에 다음 화살표(▼), 그 사이에 글자 두세 줄. "
     "이 자리들은 장식 없이 비워 두고, 장식은 바깥 60px 안에 넣어 주세요. 상자 바깥은 투명하게 해 주세요."),
    ("Draw_Paper", "UI", "쪽지", (3230, 3814), "그림판 종이. 줄여서 건네는 쪽지와 오프닝 쪽지로도 써요",
     "아이가 그림을 그리는 종이 면. 그리기 화면에서는 방 배경 위에 쪽지 한 장만 놓이고 **가장자리까지 다 보여요** (틀이 없어요). "
     "아이가 두 손가락으로 쪽지를 3배까지 키워 보기 때문에 예전 크기(1615 x 1907)의 **2배**로 부탁해요. 비율은 그대로예요. "
     "다 그리면 이 종이가 그대로 작아져서 도깨비에게 건네는 쪽지가 되고, 오프닝에서도 작게 떠요. 그래서 가장자리까지 쪽지로 보이게 그려 주세요. "
     "투명 없이 꽉 채워 주세요. 결은 아주 옅게 해 주세요. 검정 · 노랑 같은 옅은 선도 잘 보여야 해요. 줄이나 모눈은 넣지 말아 주세요."),
    ("Paper_Score", "UI", "쪽지", (2023, 3247), "채점표의 바깥 판",
     "채점표 한 장. 크기가 고정이라 늘리지 않고 그대로 써요. "
     "게임이 위에 올리는 것: **위쪽 약 1000px**에 왼쪽은 아이가 그린 선, 오른쪽은 원본이 든 흰 칸, 그 아래에 번호와 동그라미 · 빗금이 달린 목록(최대 5줄). "
     "가운데는 장식 없이 비워 두고, 결은 옅게 그려 주세요. 종이 바깥은 투명하게 해 주세요."),
    ("Chapter_Picnic", "UI", "챕터", (1760, 1286), "챕터 선택 화면의 소풍 카드 그림",
     "게임 시작하기를 누르면 나오는 챕터 선택 화면에서 가장 큰 카드의 그림이에요. 카드 아래에 게임이 `소풍` 글자를 올려요. "
     "주인공 · 도깨비 · 돗자리가 있는 소풍 장면이면 돼요 (지금은 소풍 배경에 캐릭터를 얹어 임시로 꾸몄어요). "
     "투명 없이 꽉 채워 주세요. 네 모서리는 카드 테두리에 살짝 가려요."),
    ("HUD_CandleGauge", "UI", "게이지", (2000, 200), "그리기 화면 : 위 줄(돋보기 · 사탕 · 톱니)과 쪽지 사이에 화면 가로 가득(좌우 여백 50) 놓이는 남은 시간 (60초)",
     "**가로 직사각형 게이지 바** 한 장 (화면에서는 980 x 98). 구체적인 디자인은 정해지지 않았어요. 기본적인 가로 바면 돼요. "
     "게임이 시간이 갈수록 그림을 **오른쪽부터 잘라 내서** 왼쪽으로 줄어들고, 다 되면 없어져요. 그래서 처음부터 끝까지 굵기와 무늬가 같게 해 주세요 (어디서 잘려도 어색하지 않게). "
     "바가 그림의 왼쪽 끝부터 오른쪽 끝까지 꽉 차게 해 주세요. 뒤에는 같은 크기의 옅은 틀을 게임이 깔아요. "
     "지금 게임에 들어 있는 임시 그림(같은 이름, 그냥 주황 직사각형)과 크기를 맞춰 주시면 바로 바꿔 넣을 수 있어요."),
]

# 투명한 부분 없이 꽉 채우는 그림 (apply_art_zip.py 가 투명 검사를 건너뛰어요). Background 폴더는 모두 꽉 채워요.
OPAQUE = {"Draw_Paper", "Chapter_Picnic"}
# 꽉 찬 직사각형이어도 되는 그림 (투명 검사를 건너뛰어요)
ANY_ALPHA = {"HUD_CandleGauge"}

ZIP_FOLDER = "FindDokkaebi_Art"
GROUPS = [
    ("쪽지", "쪽지 질감", "흰 카드 판이 손그림 배경 위에서 따로 떠 보여서, 자주 보이는 판 세 가지를 쪽지 질감으로 바꿔요."),
    ("게이지", "남은 시간 게이지", "남은 시간 게이지는 위 줄(돋보기 · 사탕 · 톱니)과 쪽지 사이에 화면 가로 가득 놓여요 (11/15, 좌우 여백 50). 쪽지는 처음부터 전체 화면이라 그 사이 빈 줄이 늘 보여요. 남은 초 숫자는 위 줄 가운데에 있어요. 촛대 모양은 쓰지 않고, 기본적인 가로 직사각형 게이지 바로 부탁드려요. 지금은 자리만 보이는 임시 바가 들어 있어요."),
    ("챕터", "챕터 선택 그림", "11월 12일에 챕터 선택 화면이 새로 생겼어요. 지금은 소풍 챕터만 있고 나머지 4개는 자물쇠와 `준비 중`으로 보여요."),
]

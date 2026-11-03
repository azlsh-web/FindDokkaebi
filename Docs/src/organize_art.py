# 아트 원본 정리 : 기획서 그림 · 첨부 사진 → ArtSource/ (게임 플로우 순서)
#   python organize_art.py <test 폴더> <오려 낸 부품 폴더(원래 크기)> <물건 원본 폴더>
#   ArtSource/원본/기획서   : 「게임기획서_아트 수정.docx」 그림 1~24 (이름에 내용)
#   ArtSource/원본/첨부     : 폴더에 첨부한 사진 8장 (이름에 내용, 원래 파일 이름은 README)
#   ArtSource/화면별/NN_…   : 그 화면에 실제로 들어간 그림·부품 (원래 크기) + README(어디서 · 어떻게 · 게임 파일)
#   ArtSource/README.md     : 전체 표 (이미지 → 들어가는 화면 → 통째로/일부만(좌표))
import sys, os, io, shutil, zipfile
from PIL import Image

TEST, PARTS, OBJ = sys.argv[1:4]
ROOT = os.path.join(TEST, "FindDokkaebi", "ArtSource")
DOCX = os.path.join(TEST, "찾아라 도깨비_중간대체 게임기획서_아트 수정.docx")
shutil.rmtree(ROOT, ignore_errors=True)

DOC = {
    1: "1단계_관찰_예시", 2: "1단계_그리기_예시", 3: "2단계_관찰_예시", 4: "2단계_그리기_예시",
    5: "3단계_관찰_예시", 6: "3단계_그리기_예시", 7: "4단계_관찰_예시", 8: "4단계_그리기_혼색팔레트_예시",
    9: "관찰화면_시안_확정", 10: "그리기화면_시안_확정", 11: "시작화면_확정", 12: "단계도입_예시",
    13: "성공결과_예시", 14: "부분성공결과_예시", 15: "원본비교_예시", 16: "이야기종료기록_예시",
    17: "주인공_캐릭터시트", 18: "도깨비_캐릭터시트", 19: "친구_캐릭터시트", 20: "아트레퍼런스_외부그림",
    21: "판정예시_방망이1", 22: "판정예시_방망이2", 23: "판정예시_돗자리1", 24: "판정예시_돗자리2",
}
ATT = [
    ("A", "도깨비", "KakaoTalk_20261026_100425597.png"),
    ("B", "주인공_한복_첫버전", "KakaoTalk_20261026_100425597_01.png"),
    ("C", "친구_한복_첫버전", "KakaoTalk_20261026_100425597_02.png"),
    ("D", "시작화면참고_빈방", "KakaoTalk_20261026_131203350.png"),
    ("E", "시작화면_v2", "시작화면_v2.png"),
    ("F", "한옥_시작화면", "KakaoTalk_20261026_100536283.png"),
    ("G", "관찰화면_UX", "KakaoTalk_20261026_175942190.png"),
    ("H", "그리기화면_UX_15초버전", "KakaoTalk_20261026_192259923.png"),
]

os.makedirs(os.path.join(ROOT, "원본", "기획서"))
os.makedirs(os.path.join(ROOT, "원본", "첨부"))
z = zipfile.ZipFile(DOCX)
docname = {}
for n, d in DOC.items():
    docname[n] = f"그림{n:02d}_{d}.png"
    Image.open(io.BytesIO(z.read(f"word/media/image{n}.png"))).save(os.path.join(ROOT, "원본", "기획서", docname[n]))
attname = {}
for k, d, fn in ATT:
    attname[k] = f"첨부{k}_{d}.png"
    shutil.copy(os.path.join(TEST, fn), os.path.join(ROOT, "원본", "첨부", attname[k]))

G = lambda n: f"원본/기획서/{docname[n]}"
A = lambda k: f"원본/첨부/{attname[k]}"
SP = os.path.join(TEST, "FindDokkaebi", "Assets", "Sprites")

# (화면 폴더, 화면 설명, [(부품 파일, 가져올 곳(부품 폴더/게임 폴더), 원본, 위치·처리, 게임 파일)])
P = lambda n: ("part", n + ".png")
S = lambda rel: ("sprite", rel)
O = lambda n: ("obj", n + ".png")
screens = [
    ("00_공통_HUD_배경", "모든 게임 화면 위쪽 HUD 와 뒤 배경, 여러 화면이 같이 쓰는 카드·버튼", [
        ("Logo", P("Logo"), G(10), "(186,106)~(860,344) 그대로", "TeamUI/Logo.png"),
        ("StagePill", P("StagePill"), G(10), "(948,108)~(1720,344), 「5/5·친구」 글자 지움 → 게임이 「1/5 · 방망이」", "TeamUI/StagePill.png"),
        ("Pause", P("Pause"), G(10), "(1908,112)~(2136,344) 그대로", "TeamUI/Pause.png"),
        ("SnackPill", P("SnackPill"), G(10), "(108,424)~(880,708), 「간식 10개」 글자 지움", "TeamUI/SnackPill.png"),
        ("Incense", P("Incense"), G(10), "(2006,772)~(2153,2543) 그대로, 위에서부터 타들어 가게", "TeamUI/Incense.png"),
        ("RoomBG", S("Background/RoomBG.png"), A("D"), "통째로, 가운데 제목·문구·버튼을 지움", "Background/RoomBG.png"),
        ("TeamCard", P("TeamCard"), G(9), "관찰 카드 아래쪽 조각을 위아래로 이어 붙인 9-slice 카드 (도입·판정·결과·비교·기록·일시정지·색 섞기)", "TeamUI/TeamCard.png"),
        ("TeamPillCoral", P("TeamPillCoral"), G(10), "「다 그렸어!」 버튼 바탕 (글자·도깨비 지움), 주 버튼", "TeamUI/TeamPillCoral.png"),
        ("TeamPillCream", P("TeamPillCream"), G(10), "굵기 알약 바탕 (점 지움), 보조 버튼", "TeamUI/TeamPillCream.png"),
    ]),
    ("01_시작화면", "게임을 켜면 처음 보는 화면. 「소풍 준비하러 가기」 → 1단계 도입", [
        ("TitleScreen", S("Background/TitleScreen.png"), G(11), "통째로 (첨부 E 의 고해상도판). 버튼 자리 (200,3204)~(2048,3596) 에 투명 버튼", "Background/TitleScreen.png"),
    ]),
    ("02_단계도입", "단계 번호 · 이름 · 이야기 · 규칙. 「기억할 모습 보기」 → 관찰", [
        ("Boy_Normal", S("Characters/Boy_Normal.png"), A("E"), "시작 화면 그림에서 주인공만 오려 냄", "Characters/Boy_Normal.png"),
        ("Dokkaebi", S("Characters/Dokkaebi.png"), A("A"), "통째로 (회색 바탕만 지움)", "Characters/Dokkaebi.png"),
    ]),
    ("03_관찰", "3초 동안 물건을 보여 주고 3·2·1 → 그리기", [
        ("ObserveCard", P("ObserveCard"), G(9), "(300,590)~(2010,3040) 쪽지·테이프·빼꼼 도깨비·말풍선 한 덩어리, 속 친구 그림·글자 지움", "TeamUI/ObserveCard.png"),
        ("CountOn", P("CountOn"), G(9), "코랄 「3」 동그라미 바탕 (숫자 떼어 냄)", "TeamUI/CountOn.png"),
        ("CountOff", P("CountOff"), G(9), "크림 「2」 동그라미 바탕", "TeamUI/CountOff.png"),
        ("Glyph3", P("Glyph3"), G(9), "숫자 3 모양 (2·1 도 같은 방법)", "TeamUI/Glyph1~3.png"),
        ("CountSparkle", P("CountSparkle"), G(9), "3 왼쪽 위 노란 반짝이", "TeamUI/CountSparkle.png"),
        ("FlameDeco", P("FlameDeco"), G(9), "(840,3631)~(1521,3839) 아래 도깨비불 장식", "TeamUI/FlameDeco.png"),
    ]),
    ("04_그리기", "60초 동안 쪽지에 그리기. 「다 그렸어!」 → 판정 대기", [
        ("Clipboard", P("Clipboard"), G(10), "(116,636)~(1940,2632), 제목 글자 지움", "TeamUI/Clipboard.png"),
        ("PaletteEmpty", P("PaletteEmpty"), G(10), "(126,2686)~(1644,3085), 물감 동그라미 떼어 냄", "TeamUI/PaletteEmpty.png"),
        ("BlobBlack", P("BlobBlack"), G(10), "물감 동그라미 (빨강·노랑·파랑도 같은 방법)", "TeamUI/BlobBlack·Red·Yellow·Blue.png"),
        ("BlobMixed", P("BlobMixed"), G(10), "초록 동그라미를 흰 명암으로 → 섞은 색을 곱해 칠함", "TeamUI/BlobMixed.png"),
        ("MixButton", P("MixButton"), G(10), "(1632,2684)~(2048,3080) 「색 섞기」 그대로", "TeamUI/MixButton.png"),
        ("PencilButton", P("PencilButton"), G(10), "연필 아이콘 + 지우개 버튼 틀", "TeamUI/PencilButton.png"),
        ("EraserButton", P("EraserButton"), G(10), "지우개 버튼 그대로", "TeamUI/EraserButton.png"),
        ("UndoButton", P("UndoButton"), G(10), "되돌리기 버튼 그대로", "TeamUI/UndoButton.png"),
        ("SizePill", P("SizePill"), G(10), "굵기 알약 (점은 게임이 그림)", "TeamUI/SizePill.png"),
        ("SubmitButton", P("SubmitButton"), G(10), "(241,3488)~(1987,3850) 「다 그렸어! 간식 1개 사용」 그대로", "TeamUI/SubmitButton.png"),
    ]),
    ("05_색섞기", "4·5단계 그리기 중 「색 섞기」를 누르면 물감 판 자리에 열림", [
        ("TeamCard", P("TeamCard"), G(9), "공통 카드 (00 폴더와 같음)", "TeamUI/TeamCard.png"),
    ]),
    ("06_판정대기", "쪽지와 간식을 도깨비에게 건네고 판정을 기다림", [
        ("Dokkaebi", S("Characters/Dokkaebi.png"), A("A"), "통째로", "Characters/Dokkaebi.png"),
        ("SnackBag", P("SnackBag"), G(10), "간식 알약 속 복주머니만", "TeamUI/SnackBag.png"),
    ]),
    ("07_결과", "도깨비가 가져온 물건 + 특징별 O/X + 소년 반응. 다시 그리기 / 비교", [
        ("Dokkaebi", S("Characters/Dokkaebi.png"), A("A"), "통째로", "Characters/Dokkaebi.png"),
        ("Boy_Normal", S("Characters/Boy_Normal.png"), A("E"), "주인공 (기쁨·당황 표정은 아직 없음 → 그림 준비 중)", "Characters/Boy_*.png"),
        ("ArtComing", S("UI/ArtComing.png"), "-", "아직 팀 그림이 없는 엉뚱한 물건 · 주인공 표정 자리", "UI/ArtComing.png"),
    ]),
    ("08_비교", "내 그림과 원본을 나란히 + 원본의 특징 → 다음 단계", []),
    ("09_이야기종료", "5단계를 마치면 단계별 기록 + 처음으로", [
        ("Friend", S("Stage5_Friend/Original.png"), G(19), "친구 캐릭터 시트 통째로", "Stage5_Friend/Original.png"),
    ]),
    ("10_단계별물건", "관찰 화면에 보여 주고 결과·비교 화면에 나오는 물건", [
        ("Stage1_방망이", O("Bat"), G(1), "관찰 카드 속 물건만 오려 2배", "Stage1_Bat/Original.png"),
        ("Stage2_가방", O("Bag"), G(3), "관찰 카드 속 물건만", "Stage2_Bag/Original.png"),
        ("Stage3_돗자리", O("Mat"), G(5), "관찰 카드 속 물건만 (왼쪽 모서리는 원래 그림에서 잘려 있음)", "Stage3_Mat/Original.png"),
        ("Stage4_김밥", O("Gimbap"), G(7), "관찰 카드 속 물건만", "Stage4_Gimbap/Original.png"),
        ("Stage5_친구", S("Stage5_Friend/Original.png"), G(19), "캐릭터 시트 통째로", "Stage5_Friend/Original.png"),
        ("엉뚱한물건_전부", S("UI/ArtComing.png"), "-", "아직 팀 그림 없음 → 그림 준비 중 (같은 파일 이름으로 덮어쓰면 바뀜)", "Stage*/Wrong_F*.png"),
    ]),
]

def src_file(kind, name):
    if kind == "part": return os.path.join(PARTS, name)
    if kind == "obj": return os.path.join(OBJ, name)
    return os.path.join(SP, name)

master = ["# 아트 원본 정리 (게임 플로우 순서)\n",
          "기획서 그림과 첨부 사진이 게임의 어느 화면에 들어가는지, 통째로 쓰는지 일부만 오려 쓰는지 정리했어요.\n",
          "- `원본/기획서` : 「게임기획서_아트 수정.docx」 그림 1~24",
          "- `원본/첨부` : 폴더에 첨부한 사진 8장",
          "- `화면별/NN_화면` : 그 화면에 들어간 그림·부품 (원래 크기). 게임 안에는 `Assets/Sprites/` 에 있어요 (시안 UI 는 절반 크기)",
          "- 좌표는 시안 그림(2248×3952) 픽셀이에요. 오린 방법은 `Docs/src/art_pipeline/*.py`\n",
          "## 게임 플로우 순서로 보기\n",
          "| 화면 | 들어간 그림 | 원본 | 통째로 / 일부(위치) | 게임 파일 |", "|---|---|---|---|---|"]
for folder, desc, parts in screens:
    d = os.path.join(ROOT, "화면별", folder); os.makedirs(d)
    lines = [f"# {folder}\n", desc + "\n", "| 파일 | 원본 | 통째로 / 일부(위치) · 처리 | 게임 파일 (Assets/Sprites/) |", "|---|---|---|---|"]
    for name, (kind, fn), src, how, game in parts:
        sp = src_file(kind, fn)
        if os.path.exists(sp): shutil.copy(sp, os.path.join(d, name + ".png"))
        lines.append(f"| {name}.png | {src} | {how} | {game} |")
        master.append(f"| {folder} | {name} | {src} | {how} | {game} |")
    if not parts:
        lines.append("| (새 그림 없음) | | 공통 카드·버튼과 단계별 물건을 씀 | |")
        master.append(f"| {folder} | (공통 카드 · 물건) | | | |")
    open(os.path.join(d, "README.md"), "w", encoding="utf-8").write("\n".join(lines) + "\n")

master += ["\n## 원본별로 보기\n", "| 원본 | 게임에서 |", "|---|---|"]
uses = {
    9: "관찰 화면 전체 부품, 카드(TeamCard)", 10: "HUD, 그리기 화면 전체 부품, 버튼 바탕, 복주머니", 11: "시작 화면 통째로",
    1: "1단계 물건(방망이)만 오림", 3: "2단계 물건(가방)만 오림", 5: "3단계 물건(돗자리)만 오림", 7: "4단계 물건(김밥)만 오림",
    19: "5단계 친구 · 이야기 종료 화면", 17: "(첨부 E 의 주인공과 같은 그림)", 18: "(첨부 A 와 같은 그림)",
}
for n in DOC:
    master.append(f"| 기획서 그림 {n} `{docname[n]}` | {uses.get(n, '쓰지 않음 (예시 화면·레퍼런스·판정 예시)')} |")
attuse = {"A": "도깨비 (도입·판정·결과·기록)", "D": "게임 화면 뒤 방 배경 (제목·버튼 지움)",
          "E": "주인공 오려 냄 (시작 화면 자체는 같은 그림의 고해상도판 = 기획서 그림 11)",
          "B": "쓰지 않음 (멜빵 주인공으로 바뀜)", "C": "쓰지 않음 (기획서 그림 19 친구로 바뀜)", "F": "쓰지 않음 (확정 시작 화면이 아님)",
          "G": "직접 쓰지 않음 — 같은 시안의 고해상도판(기획서 그림 9)을 오려 씀", "H": "직접 쓰지 않음 — 최신 고해상도판(기획서 그림 10)을 오려 씀"}
for k, d, fn in ATT:
    master.append(f"| 첨부 {k} `{attname[k]}` (원래 이름 {fn}) | {attuse[k]} |")
master += ["\n## 아직 팀 그림이 없는 것\n",
           "- 엉뚱한 물건 21장 (`Assets/Sprites/Stage*/Wrong_F*.png`) · 주인공 기쁨·당황 표정 (`Characters/Boy_Happy·Surprised.png`) → 지금은 `UI/ArtComing.png`(그림 준비 중)",
           "- 같은 파일 이름으로 덮어쓰면 다음 씬 만들기 때 바로 바뀌어요."]
open(os.path.join(ROOT, "README.md"), "w", encoding="utf-8").write("\n".join(master) + "\n")
print("ArtSource ok")

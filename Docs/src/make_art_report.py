# 아트 적용 정리 PDF 만들기 : 장면마다 어떤 그림을 어디서 가져와 어떻게 썼는지
#   python make_art_report.py <test 폴더> <SelfTest 캡처 폴더> <출력 html>
#   (html 을 Edge 로 PDF 인쇄 : msedge --headless --print-to-pdf)
import sys, os, io, base64, zipfile
from PIL import Image

TEST, SHOTS, OUT = sys.argv[1:4]
PROJ = os.path.join(TEST, "FindDokkaebi")
DOCX = os.path.join(TEST, "찾아라 도깨비_중간대체 게임기획서_아트 수정.docx")
docx = zipfile.ZipFile(DOCX)


def b64(im, w, q=80):
    im = im.convert("RGBA")
    if im.width > w:
        im = im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)
    bg = Image.new("RGB", im.size, (233, 239, 246))
    bg.paste(im, mask=im.getchannel("A"))
    buf = io.BytesIO(); bg.save(buf, "JPEG", quality=q)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


def doc_img(n): return Image.open(io.BytesIO(docx.read(f"word/media/image{n}.png")))
def folder(n): return Image.open(os.path.join(TEST, n))
def shot(n): return Image.open(os.path.join(SHOTS, n))
def sprite(rel): return Image.open(os.path.join(PROJ, "Assets", "Sprites", rel))


F = {   # 폴더에 있는 그림 (번호는 이 문서에서 부르는 이름)
    "폴더 A": ("KakaoTalk_20261026_100425597.png", "도깨비 캐릭터"),
    "폴더 B": ("KakaoTalk_20261026_100425597_01.png", "주인공 (한복, 첫 버전)"),
    "폴더 C": ("KakaoTalk_20261026_100425597_02.png", "친구 (한복, 첫 버전)"),
    "폴더 D": ("KakaoTalk_20261026_131203350.png", "시작 화면 참고 (빈 방)"),
    "폴더 E": ("시작화면_v2.png", "시작 화면 v2 (멜빵 주인공)"),
    "폴더 F": ("KakaoTalk_20261026_100536283.png", "한옥 시작 화면"),
    "폴더 G": ("KakaoTalk_20261026_175942190.png", "관찰 화면 UX"),
    "폴더 H": ("KakaoTalk_20261026_192259923.png", "그리기 화면 UX (15초 알약 버전)"),
}
D = {   # 아트 수정 기획서 그림
    9: "관찰 화면 시안 (확정)", 10: "그리기 화면 시안 (확정)", 11: "시작 화면 (확정)",
    1: "1단계 관찰 예시 (방망이)", 3: "2단계 관찰 예시 (가방)", 5: "3단계 관찰 예시 (돗자리)", 7: "4단계 관찰 예시 (김밥)",
    17: "주인공 캐릭터 시트", 18: "도깨비 캐릭터 시트", 19: "친구 캐릭터 시트",
}

KIND = {
    "그대로": ("그대로 오림", "#2f7d4f"),
    "글자": ("글자 지움 → 게임이 글자를 씀", "#2f6fa8"),
    "합침": ("팀 그림 조각을 합치거나 늘림", "#7a5ab0"),
    "수정": ("팀 그림의 색·모양 일부 수정", "#b0701c"),
    "직접": ("팀 그림 없음 → 단순한 도형·아이콘으로 만듦", "#c0392b"),
}


def tag(k):
    t, c = KIND[k]
    return f'<span class="tag" style="background:{c}">{t}</span>'


css = """
@page { size: A4; margin: 14mm 12mm; }
body { font-family: 'Malgun Gothic', sans-serif; color: #3b2414; font-size: 10.5pt; }
h1 { font-size: 20pt; margin: 0 0 4px; } h2 { font-size: 14pt; margin: 18px 0 6px; border-bottom: 2px solid #4a2b16; padding-bottom: 3px; }
h3 { font-size: 11.5pt; margin: 12px 0 4px; }
p { margin: 4px 0; line-height: 1.5; }
table { border-collapse: collapse; width: 100%; margin: 6px 0; }
td, th { border: 1px solid #d9c7b4; padding: 4px 6px; vertical-align: top; font-size: 9.5pt; }
th { background: #f6ecdf; text-align: left; }
.tag { color: #fff; border-radius: 8px; padding: 1px 6px; font-size: 8.5pt; white-space: nowrap; }
.scene { display: flex; gap: 10px; page-break-inside: avoid; margin-top: 6px; }
.scene img.shot { width: 130px; border: 1px solid #aaa; border-radius: 6px; }
.src { display: flex; gap: 6px; flex-wrap: wrap; margin: 4px 0; }
.src figure { margin: 0; text-align: center; font-size: 8.5pt; }
.src img { height: 108px; border: 1px solid #ccc; border-radius: 4px; }
.keep { break-inside: avoid; page-break-inside: avoid; }
.parts { display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px; }
.parts div { border: 1px solid #ddd; border-radius: 6px; padding: 4px; font-size: 8.5pt; page-break-inside: avoid; }
.parts img { max-width: 100%; max-height: 56px; display: block; margin: 0 auto 3px; }
.warn { background: #fdecea; border-left: 5px solid #c0392b; padding: 6px 10px; margin: 6px 0; }
.ok { background: #eaf6ee; border-left: 5px solid #2f7d4f; padding: 6px 10px; margin: 6px 0; }
.pb { page-break-before: always; }
"""

h = [f"<html><head><meta charset='utf-8'><style>{css}</style></head><body>"]
h.append("<h1>찾아라! 도깨비 — 아트 적용 정리</h1>")
h.append("<p>게임 화면의 그림 요소가 <b>폴더에 둔 이미지</b>와 <b>「게임기획서_아트 수정.docx」 그림</b> 중 어디서 왔는지, 어떻게 썼는지 장면별로 정리했어요. "
         "팀 그림이 없어서 제가 만든 것은 빨간 표시로 따로 모았어요.</p>")
h.append("<table><tr><th>처리 방식</th><th>뜻</th></tr>" + "".join(
    f"<tr><td>{tag(k)}</td><td>{d}</td></tr>" for k, d in [
        ("그대로", "시안·캐릭터 그림에서 해당 부분을 그대로 오려서, 시안과 같은 자리에 놓았어요."),
        ("글자", "시안 그림에 박힌 글자(단계 이름, 간식 수, 제목 등)는 상황마다 바뀌어야 해서 같은 바탕색으로 지우고, 게임이 글자를 써요. 글꼴은 시안 글꼴이 없어 Noto Sans KR 이에요."),
        ("합침", "시안 조각을 이어 붙이거나(카드 네 모서리), 늘리거나(9-slice 버튼), 두 버튼 그림을 합쳤어요. 새로 그린 선은 없어요."),
        ("수정", "팀 원본 그림에서 색을 바꾸거나 한 부분을 지웠어요 (엉뚱한 물건, 방 배경, 섞은 색 칸)."),
        ("직접", "팀 그림이 없는 기능이라 단순한 원·아이콘으로 만들었어요. 팀 그림이 오면 바꿀 자리예요."),
    ]) + "</table>")

# ── 쓴 원본 목록 ──
h.append("<h2>1. 쓴 원본 그림</h2>")
h.append("<h3>아트 수정 기획서 그림</h3><div class='src'>" + "".join(
    f"<figure><img src='{b64(doc_img(n), 300)}'><br>그림 {n}<br>{d}</figure>" for n, d in D.items()) + "</div>")
h.append("<h3>폴더의 이미지</h3><div class='src'>" + "".join(
    f"<figure><img src='{b64(folder(fn), 300)}'><br>{k}<br>{d}</figure>" for k, (fn, d) in F.items()) + "</div>")
h.append("<table><tr><th>원본</th><th>게임에서 쓴 곳</th></tr>"
         "<tr><td>그림 9 · 10 (관찰·그리기 시안)</td><td>HUD 전체, 관찰 화면, 그리기 화면 부품, 다른 화면의 카드·버튼 바탕</td></tr>"
         "<tr><td>그림 11 (시작 화면)</td><td>시작 화면 그림 그대로 (= 폴더 E 의 고해상도판)</td></tr>"
         "<tr><td>그림 1 · 3 · 5 · 7</td><td>1~4단계 물건 원본 (방망이 · 가방 · 돗자리 · 김밥)</td></tr>"
         "<tr><td>그림 19</td><td>5단계 친구 원본, 이야기 종료 화면의 친구</td></tr>"
         "<tr><td>폴더 A</td><td>도깨비 (도입·판정·결과·기록 화면). 그림 18 과 같은 그림</td></tr>"
         "<tr><td>폴더 E</td><td>주인공 (시작 화면 그림에서 오려 냄). 그림 17 과 같은 옷</td></tr>"
         "<tr><td>폴더 D</td><td>게임 화면 뒤 방 배경 (제목·버튼을 지운 것)</td></tr>"
         "<tr><td>폴더 B · C · F · G · H</td><td>안 씀 — B·C(한복 캐릭터)는 확정 캐릭터 시트로 바뀌었고, F(한옥 시작 화면)는 확정본이 아니고, G·H 는 기획서 그림 9·10 이 같은 시안의 고해상도·최신판이라 그쪽을 썼어요.</td></tr>"
         "</table>")

# ── 장면별 ──
def scene(title, shot_name, rows, note=""):
    s = f"<div class='keep'><h3>{title}</h3><div class='scene'>"
    if shot_name:
        s += f"<img class='shot' src='{b64(shot(shot_name), 300, 72)}'>"
    s += "<div style='flex:1'><table><tr><th style='width:26%'>요소</th><th style='width:38%'>원본 (그림 · 위치)</th><th>처리</th></tr>"
    for el, src, k, extra in rows:
        s += f"<tr><td>{el}</td><td>{src}</td><td>{tag(k)} {extra}</td></tr>"
    s += "</table>" + (f"<p>{note}</p>" if note else "") + "</div></div></div>"
    return s


h.append("<h2 class='pb'>2. 장면별 적용</h2>")
h.append("<p>좌표는 시안 그림(2248×3952) 픽셀이에요. 게임은 이 크기를 가로 1080 으로 줄인 화면(1080×1899)에 같은 좌표 그대로 놓아요. 폰이 더 길면 위아래에 방 배경이 보여요.</p>")
h.append(scene("시작 화면", "01_title.png", [
    ("시작 화면 그림 전체", "그림 11 (2248×3952) 전체", "그대로", ""),
    ("「소풍 준비하러 가기」 누르는 자리", "그림 11 속 버튼 자리 (200,3204)~(2048,3596)", "그대로", "투명한 버튼을 그 자리에 겹침"),
    ("오른쪽 아래 톱니바퀴", "그림 11 에 그려져 있음", "그대로", "설정 기능은 없어서 눌러도 반응 없음"),
]))
h.append(scene("모든 게임 화면 공통 : HUD · 배경", "04_s1_draw.png", [
    ("로고 「찾아라! 도깨비」", "그림 10 (186,106)~(860,344)", "그대로", ""),
    ("단계 알약 「1/5 · 방망이」", "그림 10 (948,108)~(1720,344)", "글자", "「5/5·친구」 글자를 지움"),
    ("일시정지 버튼", "그림 10 (1908,112)~(2136,344)", "그대로", "누르면 일시정지 화면"),
    ("간식 알약 + 복주머니", "그림 10 (108,424)~(880,708)", "글자", "「간식 10개」 글자를 지움"),
    ("오른쪽 긴 향 (그리기 때만)", "그림 10 (2006,772)~(2153,2543)", "그대로", "위에서부터 타들어 가게 잘라 보여 줌"),
    ("향 아래 「60초」", "시안에 없음", "글자", "기획서 HUD 설명(향과 남은 초)에 따라 글자만 추가"),
    ("방 배경", "폴더 D 전체", "수정", "제목·버튼을 지운 빈 방 (폴더 D 와 77% 같은 픽셀)"),
]))
h.append(scene("단계 도입 화면", "02_s1_intro.png", [
    ("큰 카드 · 규칙 카드", "그림 9 관찰 카드의 아래쪽 (모서리 2개 + 테두리)", "합침", "위아래로 이어 붙여 네 모서리 카드로, 늘림(9-slice)"),
    ("「기억할 모습 보기」 버튼", "그림 10 「다 그렸어!」 버튼 바탕", "합침", "글자·도깨비를 지운 코랄 알약을 늘림"),
    ("주인공", "폴더 E 에서 오려 냄", "그대로", ""),
    ("도깨비", "폴더 A", "그대로", "회색 바탕만 지움"),
    ("단계 · 이름 · 이야기 · 규칙 글자", "—", "글자", "확정 시안이 없어 배치는 기획서 4장 설명(위 단계·이름, 가운데 이야기·캐릭터, 아래 규칙) 기준"),
]))
h.append(scene("관찰 화면 (3초)", "03_s1_observe.png", [
    ("쪽지 카드 + 테이프 + 빼꼼한 도깨비 + 말풍선", "그림 9 (300,590)~(2010,3040) 한 덩어리", "글자", "카드 속 친구 그림과 「친구」「모양과 색을…」 글자를 종이 색으로 지움. 말풍선 글자는 시안 그대로"),
    ("가운데 물건 그림", "단계별 원본 (아래 3장)", "그대로", ""),
    ("물건 이름 · 안내 글자", "그림 9 의 글자 자리", "글자", "단계마다 바뀜"),
    ("3 · 2 · 1 동그라미", "그림 9 코랄 「3」 · 크림 「2」「1」", "합침", "동그라미 바탕과 숫자를 따로 떼어, 지금 숫자만 코랄 바탕 + 흰 숫자로 바꿔 끼움"),
    ("노란 반짝이", "그림 9 「3」 왼쪽 위", "그대로", "지금 숫자 옆으로 따라감"),
    ("「잠시 뒤 쪽지가 사라져요」", "그림 9 글자 자리", "글자", ""),
    ("아래 도깨비불 장식", "그림 9 (840,3631)~(1521,3839)", "그대로", ""),
]))
h.append(scene("그리기 화면 (60초)", "16_s4_draw.png", [
    ("클립보드 쪽지", "그림 10 (116,636)~(1940,2632)", "글자", "「기억나는 친구를 그려 줘」 글자를 지움, 반짝이는 그대로"),
    ("그림판 바탕색", "그림 10 종이 색 (253,252,245)", "그대로", "쪽지와 같은 색"),
    ("물감 판", "그림 10 (126,2686)~(1644,3085)", "합침", "물감 동그라미를 떼어 내고 판만 남김"),
    ("물감 동그라미 (검정·빨강·노랑·파랑)", "그림 10 각 동그라미", "그대로", "단계마다 쓸 수 있는 색만 보임"),
    ("섞은 색 칸 (5번째 동그라미)", "그림 10 초록 동그라미", "수정", "명암만 남긴 흰 동그라미로 바꿔, 섞은 색을 칠함"),
    ("「색 섞기」 버튼", "그림 10 (1632,2684)~(2048,3080)", "그대로", "4·5단계만 보임"),
    ("연필 버튼", "그림 10 연필 + 지우개 버튼 틀", "합침", "시안 연필 버튼은 테두리가 고른 표시(코랄)라 틀은 지우개 버튼 것을 씀"),
    ("지우개 · 되돌리기 버튼", "그림 10 각 버튼", "그대로", ""),
    ("굵기 알약", "그림 10 (1536,3140)~(2060,3420)", "글자", "점을 지우고 점은 게임이 그림 (회색 = 고를 수 있음, 검정 = 지금)"),
    ("고른 도구 표시 (코랄 고리)", "그림 10 연필 버튼 고리 색 (251,126,115)", "직접", "색은 시안, 고리 모양은 단순한 원"),
    ("모두 지우기 (쪽지 오른쪽 아래)", "시안에 없음 (기획서 4장 도구 목록에는 있음)", "직접", "지우개 버튼 틀 + 휴지통 아이콘"),
    ("「다 그렸어! 간식 1개 사용」", "그림 10 (241,3488)~(1987,3850)", "그대로", "빈 쪽지면 흐리게"),
]))
h.append(scene("색 섞기 팔레트 (4·5단계)", None, [
    ("팔레트 카드", "그림 9 카드 조각", "합침", "물감 판 + 도구 줄 자리를 딱 덮도록"),
    ("재료 동그라미 · 미리보기 · 닫기(X)", "확정 시안 없음 (기획서 그림 8 은 예시)", "직접", "단순한 원과 X 아이콘"),
    ("「섞어서 칠하기」 버튼", "그림 10 버튼 바탕", "합침", ""),
]))
h.append(scene("판정 대기 · 결과 · 비교 · 기록 · 일시정지", "06_s1_result_partial.png", [
    ("카드 · 말풍선 · 버튼", "그림 9 카드 / 그림 10 버튼 바탕 · 굵기 알약 바탕", "합침", "확정 시안이 없어 같은 스타일로 맞춤"),
    ("도깨비", "폴더 A", "그대로", ""),
    ("주인공 (결과 반응)", "폴더 E 에서 오려 냄", "그대로", "기쁨·당황 표정 그림이 없어 같은 그림 + 「!?」 글자"),
    ("간식(쪽지 옆)", "그림 10 복주머니", "그대로", ""),
    ("가져온 물건 (진짜)", "단계별 원본 (아래)", "그대로", ""),
    ("가져온 물건 (엉뚱한 것)", "단계별 원본을 고친 것 (아래)", "수정", ""),
], "배치는 기획서 4장(판정 결과와 재도전, 원본 비교와 이야기 종료) 설명 기준이에요."))

# ── 물건 ──
h.append("<h2 class='pb'>3. 단계별 물건 · 엉뚱한 물건</h2>")
h.append("<p>원본은 기획서 그림을 그대로 오려 2배로 키웠어요. 엉뚱한 물건은 <b>팀 그림이 없어서</b> 원본에서 한 부분만 바꿔 만들었어요. 팀이 그려 주면 바로 바꿀 수 있어요.</p>")
stages = [("Stage1_Bat", "1 방망이 (그림 1)", ["손잡이 지움 (머리만)", "머리 지움 (손잡이만)", "구름 머리 → 매끈한 동그라미"]),
          ("Stage2_Bag", "2 가방 (그림 3)", ["몸체를 둥글게", "위 손잡이 지움", "주머니 지움", "주머니 줄무늬 지움"]),
          ("Stage3_Mat", "3 돗자리 (그림 5)", ["둥근 모양으로", "띠 없이 파랑 한 가지", "빨강 띠 → 파랑", "노랑 띠 → 빨강", "양 끝 파랑 → 초록"]),
          ("Stage4_Gimbap", "4 김밥 (그림 7)", ["검은 김 → 회색", "흰 밥 → 보라(흑미)", "빨강 재료 → 노랑", "노랑 재료 → 빨강", "초록 재료 → 파랑"]),
          ("Stage5_Friend", "5 친구 (그림 19)", ["머리만", "갈색 머리 → 검정", "주황 상의 → 초록", "파란 하의 → 보라"])]
for folder_name, title, wrongs in stages:
    h.append(f"<div class='keep'><h3>{title}</h3><table><tr><th>원본 {tag('그대로')}</th>" + "".join(f"<th>F{i+1} 빠짐 {tag('수정')}</th>" for i in range(len(wrongs))) + "</tr><tr>")
    h.append(f"<td><img style='height:80px' src='{b64(sprite(folder_name + '/Original.png'), 160)}'></td>")
    for i, wtxt in enumerate(wrongs):
        h.append(f"<td><img style='height:80px' src='{b64(sprite(folder_name + f'/Wrong_F{i+1}.png'), 160)}'><br>{wtxt}</td>")
    h.append("</tr></table></div>")
h.append("<div class='warn'>그림 5(돗자리)는 기획서 그림 자체에서 왼쪽 모서리가 카드에 잘려 있어서, 게임에서도 그대로 잘려 보여요.</div>")

# ── 부품 목록 ──
h.append("<h2 class='pb'>4. 게임에 들어간 시안 부품 (Assets/Sprites/TeamUI)</h2><div class='parts'>")
parts = [("Logo", "그림 10", "그대로"), ("StagePill", "그림 10", "글자"), ("Pause", "그림 10", "그대로"), ("SnackPill", "그림 10", "글자"),
         ("SnackBag", "그림 10", "그대로"), ("Incense", "그림 10", "그대로"), ("Clipboard", "그림 10", "글자"), ("PaletteEmpty", "그림 10", "합침"),
         ("BlobBlack", "그림 10", "그대로"), ("BlobRed", "그림 10", "그대로"), ("BlobYellow", "그림 10", "그대로"), ("BlobBlue", "그림 10", "그대로"),
         ("BlobMixed", "그림 10", "수정"), ("MixButton", "그림 10", "그대로"), ("PencilButton", "그림 10", "합침"), ("EraserButton", "그림 10", "그대로"),
         ("UndoButton", "그림 10", "그대로"), ("RoundButton", "그림 10", "합침"), ("SizePill", "그림 10", "글자"), ("SubmitButton", "그림 10", "그대로"),
         ("SelectRing", "그림 10 색", "직접"), ("ObserveCard", "그림 9", "글자"), ("CountOn", "그림 9", "합침"), ("CountOff", "그림 9", "합침"),
         ("Glyph3", "그림 9", "합침"), ("CountSparkle", "그림 9", "그대로"), ("FlameDeco", "그림 9", "그대로"), ("TeamCard", "그림 9", "합침"),
         ("TeamPillCoral", "그림 10", "합침"), ("TeamPillCream", "그림 10", "합침")]
for name, src, k in parts:
    im = sprite(f"TeamUI/{name}.png")
    if name.startswith("Glyph"):
        g = Image.new("RGBA", im.size, (240, 120, 110, 255)); g.alpha_composite(im); im = g
    h.append(f"<div><img src='{b64(im, 220)}'><b>{name}</b><br>{src} {tag(k)}</div>")
h.append("</div><p>오린 방법(좌표·과정)은 Docs/architecture_src/team_ui/*.py 에 그대로 남아 있어 다시 만들 수 있어요.</p>")

# ── 직접 만든 것 ──
h.append("<h2>5. 팀 그림이 없어서 제가 만든 것 (팀 그림이 오면 바꿀 자리)</h2>")
h.append("<div class='warn'><table><tr><th>무엇</th><th>어디</th><th>지금</th></tr>"
         "<tr><td>엉뚱한 물건 18장</td><td>결과 화면</td><td>원본에서 한 부분만 바꿈 (위 3장)</td></tr>"
         "<tr><td>방 배경</td><td>게임 화면 뒤</td><td>폴더 D 에서 제목·버튼을 지움</td></tr>"
         "<tr><td>굵기 점 · 고른 표시 고리</td><td>그리기 화면</td><td>단순한 원 (색은 시안)</td></tr>"
         "<tr><td>모두 지우기 휴지통 아이콘</td><td>그리기 화면</td><td>단순한 아이콘 (시안에 버튼 없음)</td></tr>"
         "<tr><td>색 섞기 팔레트 안 재료 동그라미 · 미리보기 · 닫기(X)</td><td>4·5단계</td><td>단순한 원 · X</td></tr>"
         "<tr><td>주인공 표정 (기쁨 · 당황)</td><td>결과 화면</td><td>표정 그림이 없어 같은 그림 + 「!?」</td></tr>"
         "<tr><td>글꼴</td><td>바뀌는 글자 전부</td><td>Noto Sans KR (시안 글꼴 파일이 없음)</td></tr>"
         "</table></div>")
h.append("<div class='ok'>그 밖의 그림(HUD, 관찰·그리기 화면 부품, 시작 화면, 캐릭터, 물건 원본)은 모두 팀 그림에서 오린 그대로예요. "
         "배치는 자동 플레이 테스트의 「배치 검사」로 숫자로 확인해요 (겹침 · 글자 넘침 · 화면 밖, 지금 0개).</div>")
h.append("</body></html>")
open(OUT, "w", encoding="utf-8").write("".join(h))
print("html ok", OUT)

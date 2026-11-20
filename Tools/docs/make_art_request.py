# 아트.pdf : 아트팀에 보내는 요청
#   python Tools/docs/make_art_request.py
#   요청 목록은 art_tree_data.REQUEST 하나예요. 받은 ZIP 을 넣는 스크립트(apply_art_zip.py)도 같은 목록을 써요.
#   2026-11-11 : 흰 카드 판 중 대사 상자 · 그림판 · 채점표를 쪽지 질감으로 바꾸는 3장 요청
#   2026-11-12 : 챕터 선택 화면의 소풍 카드 그림 1장 추가
#   2026-11-18 : 2~5챕터 임시 그림 목록 (codex · 색 바꾸기로 만든 것, 아트팀이 바꿔 주면 좋은 것)
import base64
import io
import re
from datetime import date
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_tree_data import GROUPS, OPAQUE, REQUEST, ZIP_FOLDER
from doc_pdf import BASE_CSS, write_pdf
from flow_shots import ROOT, data_url, find
import paths

SP = os.path.join(ROOT, "Assets", "Sprites")

# 새 그림이 들어갈 자리에 지금 쓰는 그림 (표의 "지금 그림" 칸)
NOW = {"Dialogue_Box": ("UI", "Common_Card", "흰 카드"), "Paper_Score": ("UI", "Common_Card", "흰 카드"),
       "Chapter_Picnic": ("Background", "BG_Picnic", "배경 + 캐릭터로 임시"),
       "HUD_CandleGauge": ("UI", "HUD_CandleGauge", "받음 (11/18) : 촛대 그림을 막대 · 불꽃으로 나눠 넣음")}

# 흰 판마다 어떻게 하는지 (2026-11-11 정함)
PLAN = [
    ("대사 상자", "새 그림 `Dialogue_Box`"),
    ("그림판", "새 그림 `Draw_Paper`"),
    ("건네는 쪽지 · 오프닝 쪽지", "`Draw_Paper`를 그대로 줄여서 써요. 오프닝 쪽지의 연필 그림은 빼고, 쪽지에 사탕을 겹쳐 띄워요."),
    ("채점표 바깥 판", "새 그림 `Paper_Score`"),
    ("채점표 왼쪽 위", "그림판에서 아이가 그린 선만 따서 올려요. 칸은 없어요."),
    ("채점표 오른쪽 위", "떠올리기 칸과 같은 흰 칸에 원본 (지금처럼)"),
    ("떠올리기 칸, 색 섞기 판, 소리 설정 판, 다시 · 포기 버튼", "지금 그림 그대로"),
]


def fmt(text):
    """`이름` 은 코드 글자로, **글** 은 굵게"""
    parts = text.split("`")
    text = "".join(f"<code>{p}</code>" if i % 2 else p for i, p in enumerate(parts))
    return re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)


def thumb(name):
    if name not in NOW:
        return "<span class='small'>새 그림<br>(지금은 코드가 크림색으로 칠함)</span>"
    folder, now, label = NOW[name]
    im = Image.open(os.path.join(SP, folder, now + ".png")).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((200, 200), Image.LANCZOS)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=85)
    return (f"<img src='data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}'>"
            f"<br><span class='small'>{now}<br>({label})</span>")


def shot(section, caption, label):
    return f"<figure><img src='{data_url(find(section, caption), 360)}'><figcaption>{label}</figcaption></figure>"


sections = ""
for key, title, why in GROUPS:
    rows = [r for r in REQUEST if r[2] == key]
    body = "".join(
        f"<tr><td class='pic'>{thumb(name)}</td><td><code>{name}.png</code><br><span class='small'>{w}×{h}px"
        f"{'<br>투명 없이 꽉 채움' if name in OPAQUE else ''}</span></td>"
        f"<td>{where}</td><td>{fmt(note)}</td></tr>"
        for name, folder, _, (w, h), where, note in rows)
    sections += (f"<div class='keep'><h2>{title} {len(rows)}장</h2><p>{why}</p>"
                 f"<table><tr><th>지금 그림</th><th style='width:20%'>파일 이름 · 크기</th><th style='width:20%'>나오는 곳</th><th>그려 주실 내용</th></tr>{body}</table></div>")

# 2~5챕터 임시 그림 : (제목, 설명, [(파일 경로 Sprites 기준, 만든 방법)])
TEMP_ART = [
    ("2~4챕터 물건 (정답 + 오답)", "물건마다 정답 1장 + 오답 3장 (4챕터는 오답 1장). 1024×1024, 투명 PNG. 판정 기준(형태 · 무늬 · 색)이 잘 보여야 해요.",
     [(f"Chapter{c}/{n}_{k}.png", "codex") for c, names in ((2, ["Ch2_Socks", "Ch2_Umbrella", "Ch2_Pillow"]), (3, ["Ch3_PartyHat", "Ch3_Balloons", "Ch3_GiftBox"]), (4, ["Ch4_Tangerine", "Ch4_Grapes", "Ch4_Candy"]))
      for n in names for k in (["Original", "Wrong1", "Wrong2", "Wrong3"] if c < 4 else ["Original", "Wrong1"])]),
    ("2~4챕터 챕터 사진", "챕터 선택 사진. 1760×1286, 투명 없이 꽉 채움. 아직 못 연 챕터는 게임이 흑백으로 바꿔요.",
     [("Chapter2/Chapter_Rain.png", "codex"), ("Chapter3/Chapter_Birthday.png", "codex"), ("Chapter4/Chapter_Snack.png", "codex")]),
    ("2~4챕터 방 배경", "기획서 4장 : 2 비 오는 낮, 3 저녁, 4 밤. 지금 방 그림(BG_Room)에 색을 덮어 만든 임시 그림. 2248×3952, 같은 구도",
     [("Background/BG_RoomRain.png", "색 덮기 (Tools/art_temp/make_room_variants.py)"), ("Background/BG_RoomEvening.png", "색 덮기"), ("Background/BG_RoomNight.png", "색 덮기")]),
    ("도깨비 머리 불 색", "기획서 4장 : 3챕터 1단계(주황), 4챕터 2단계(붉음). 몸 그림 4장과 대사 상자 얼굴 3장을 같이 바꿔야 서로 맞아요. 4챕터는 웃음이 어색한 표정도 기획서에 있어요(아직 없음)",
     [(f"Characters/Mood/{n}_M{m}.png", "색 바꾸기 (Tools/art_temp/make_dokkaebi_mood.py)") for n in ("Char_Dokkaebi_Normal", "Char_DokkaebiFace_Normal") for m in (1, 2)]),
    ("4챕터 빨간 눈", "끝 장면 주인공 속마음 때 밤 창 안에서 보이는 도깨비 눈. 280×96 투명 PNG", [("Chapter4/Room_RedEyes.png", "코드로 그림")]),
    ("5챕터 (C-48)", "물건 6개 × (정답 + 오답 3), 숲 배경 2장, 흑화 도깨비, 하트, 실루엣, 점프스케어 얼굴, 챕터 사진. 흑화 도깨비 · 점프스케어 얼굴이 대사 상자 얼굴과 그림체가 달라요 (BACKLOG.md B1 · B3)",
     [("Chapter5/Char_Dokkaebi_Dark.png", "codex"), ("Chapter5/Scare_Face.png", "codex"), ("Chapter5/GameOver_Silhouette.png", "codex"), ("Chapter5/HUD_Heart.png", "codex"),
      ("Chapter5/BG_ForestBright.png", "codex"), ("Chapter5/BG_ForestDark.png", "codex"), ("Chapter5/Chapter_Forest.png", "codex"), ("Chapter5/Ch5_Jipsin_Original.png", "codex (물건 6개 · 오답 18장 중 하나)")]),
]


def temp_thumb(rel):
    path = os.path.join(SP, rel)
    if not os.path.exists(path):
        return "<span class='small'>없음</span>"
    im = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((150, 150), Image.LANCZOS)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=80)
    return f"<img src='data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}'>"


temp_sections = ""
for title, why, files in TEMP_ART:
    cells = "".join(f"<figure>{temp_thumb(rel)}<figcaption><code>{os.path.basename(rel)}</code><br>{how}</figcaption></figure>" for rel, how in files)
    temp_sections += f"<div class='keep'><h3>{title} ({len(files)}장)</h3><p class='small'>{why}</p><div class='temps'>{cells}</div></div>"

plan_rows = "".join(f"<tr><td>{a}</td><td>{fmt(b)}</td></tr>" for a, b in PLAN)

names = sorted(r[0] for r in REQUEST)
tree = "\n".join(["FindDokkaebi_Art_Paper.zip", f"└─ {ZIP_FOLDER}/"] +
                 [f"   {'└─' if i == len(names) - 1 else '├─'} {n}.png" for i, n in enumerate(names)])

CSS = BASE_CSS + """
.pic { width: 92px; text-align: center; background: #f4f7f8; } .pic img { max-width: 80px; max-height: 80px; }
.small { font-size: 10px; color: #6d645c; } tr, figure, .keep { break-inside: avoid; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-family: Consolas, monospace; font-size: 11px; line-height: 1.4; }
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 8px 12px; margin: 10px 0; }
.shots { display: grid; grid-template-columns: repeat(5, 1fr); gap: 8px; } figure { margin: 0; }
figure img { width: 100%; border: 1px solid #ddd6cc; border-radius: 4px; }
figcaption { font-size: 10px; color: #6d645c; margin-top: 3px; text-align: center; }
.temps { display: grid; grid-template-columns: repeat(6, 1fr); gap: 6px; } .temps img { width: 100%; border: 1px solid #ddd6cc; border-radius: 4px; }
h3 { font-size: 13px; margin: 14px 0 4px; }
"""

html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>아트팀 전달사항</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 아트팀 전달사항</h1><p class='sub'>{date.today()} 기준 · 출품 마감 2026-11-15(일)</p>
<div class='note'><b>요청:</b> 모두 {len(REQUEST)}장 · 쪽지 질감 3장 (대사 상자 · 그림판 · 채점표, 11/11) + 챕터 선택의 소풍 카드 그림 1장 (11/12 새로) + 화면 가로 가득 남은 시간 게이지 바 1장 (11/15 바꿈 : 2000×200, 기본적인 가로 직사각형)<br>
<b>기한:</b> 11월 14일(토). 늦으면 지금 그림으로 출품해요. 한 장씩 먼저 보내셔도 돼요.<br>
<b>그 밖의 수정본:</b> 같은 기한, 파일 이름은 기존과 같게</div>

<h2>왜 바꾸나요</h2>
<p>배경과 캐릭터는 손그림 느낌인데, 대사 상자 · 그림판 · 채점표 같은 판은 흰 카드 한 장으로 그려져 있어서 화면에서 따로 떠 보여요.
게임 이야기도 "쪽지에 그려서 도깨비에게 건네는" 흐름이라, 자주 보이는 판을 쪽지 질감으로 바꾸려고 해요.</p>

{sections}

<div class='keep'><h2>바뀌는 화면</h2>
<div class='shots'>
{shot('1단계', '시작 대화 9', '대사 상자 → Dialogue_Box')}
{shot('1단계', '시작 대화 3', '오프닝 쪽지 → Draw_Paper 줄임')}
{shot('1단계', '그리기 시작 : 빈 쪽지', '그림판 → Draw_Paper, 쪽지 위 게이지 바 → HUD_CandleGauge')}
{shot('1단계', '건네기 : 쪽지와 간식', '건네는 쪽지 → Draw_Paper 줄임')}
{shot('3단계', '포기 : 채점표', '채점표 → Paper_Score')}
{shot('시작', '게임 시작하기 → 챕터 선택 : 소풍만 열려 있고 나머지는 준비 중', '소풍 카드 → Chapter_Picnic')}
</div>
</div>

<div class='keep'><h2>흰 판마다 어떻게 하나요</h2>
<table><tr><th style='width:34%'>판</th><th>어떻게</th></tr>{plan_rows}</table>
<p>다시 · 포기 아이콘, 대사 상자의 화살표, 채점표의 동그라미와 빗금, 그리기 화면의 붓 · 화살표 · 맞춤 아이콘도 지금 그림을 그대로 써요.</p></div>

<h2>임시 그림 (2~5챕터, 2026-11-18)</h2>
<div class='note'>아래 그림은 게임을 먼저 굴리려고 Claude 쪽에서 만든 <b>임시 그림</b>이에요. 같은 파일 이름 · 같은 비율로 그려 주시면 그대로 바꿔 넣어요.
꼭 필요한 것부터 정해서 따로 요청드릴게요 (지금은 목록만 공유).</div>
{temp_sections}

<div class='keep'><h2>규칙</h2>
<ul><li><b>파일 이름</b>을 표와 똑같이 해 주세요.</li>
<li><b>가로세로 비율</b>을 표의 크기와 똑같이 맞춰 주세요. 2배로 그리는 건 괜찮아요.</li>
<li><code>Draw_Paper</code>와 <code>Chapter_Picnic</code>은 <b>투명 없이 꽉 채운 PNG</b>, <code>HUD_CandleGauge</code>는 꽉 찬 직사각형이어도 되고, 나머지는 판 바깥이 <b>투명한 PNG</b>로 해 주세요.</li>
<li>그림 안에 글자를 넣지 말아 주세요. 글자는 게임이 올려요.</li></ul></div>
<div class='keep'><h2>보내는 방법</h2>
<p>아래 구조 그대로 ZIP 하나로 묶어 주세요. 있는 파일만 넣으면 돼요. 수정본은 ZIP 이름 끝에 <code>_v2</code>, <code>_v3</code>을 붙이고 바뀐 파일만 넣어 주세요.</p>
<pre>{tree}</pre></div>
<div class='keep'><h2>받은 뒤 (개발 쪽)</h2>
<p>아래 명령이 이름 · 비율 · 투명 배경을 검사하고, 문제가 없으면 <code>Assets/Sprites/UI</code>에 넣어요. 모두 새 이름이라 코드 연결이 필요해요.
그림판은 <code>Draw_Paper</code>를 뒤에 깔고 아이가 그린 선만 투명한 층에 따로 그려요. 판정과 채점표의 내 그림은 그 선 층을 써요.</p>
<pre>python Tools/docs/apply_art_zip.py 받은파일.zip          (검사만)
python Tools/docs/apply_art_zip.py 받은파일.zip --apply  (검사 후 넣기)</pre></div>
</body></html>"""

print("요청", len(REQUEST), "장")
os.makedirs(os.path.join(ROOT, "Temp", "DocsHtml"), exist_ok=True)
open(os.path.join(ROOT, "Temp", "DocsHtml", "아트.html"), "w", encoding="utf-8").write(html)
print("아트.pdf", write_pdf(html, paths.doc(paths.ART_REQUEST)))

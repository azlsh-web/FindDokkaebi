# 아트.pdf : 아트팀에 보내는 요청
#   python Tools/docs/make_art_request.py
#   요청 목록은 art_tree_data.REQUEST 하나예요. 받은 ZIP 을 넣는 스크립트(apply_art_zip.py)도 같은 목록을 써요.
#   2026-11-11 : 흰 카드 판과 그림판을 쪽지 질감으로 바꾸는 2장 요청
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

SP = os.path.join(ROOT, "Assets", "Sprites")
OUT = os.path.join(ROOT, "Docs", "전달")
os.makedirs(OUT, exist_ok=True)

# 새 그림이 들어갈 자리에 지금 쓰는 그림 (표의 "지금 그림" 칸)
NOW = {"Paper_Note": ("UI", "Common_Card")}

# Paper_Note 가 깔리는 판의 크기. 화면 가로 2248px(배경 그림) 기준이에요. MvpSceneBuilder 의 자리에서 계산했어요.
PANELS = [
    ("대사 상자 (대화 · 결과 · 이야기 종료)", 2098, 649),
    ("채점표", 2023, 3247),
    ("채점표 안의 내 그림 · 원본 칸", 862, 937),
    ("떠올리기 칸", 1948, 2198),
    ("건네는 쪽지", 1186, 1199),
    ("색 섞기 판", 2000, 780),
    ("일시정지 판", 1582, 895),
]
EDGE = 120   # Paper_Note 의 테두리 띠 (늘리지 않는 부분)


def fmt(text):
    """`이름` 은 코드 글자로, **글** 은 굵게"""
    parts = text.split("`")
    text = "".join(f"<code>{p}</code>" if i % 2 else p for i, p in enumerate(parts))
    return re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)


def thumb(name):
    if name not in NOW:
        return "<span class='small'>새 그림<br>(지금은 코드가 크림색으로 칠함)</span>"
    folder, now = NOW[name]
    im = Image.open(os.path.join(SP, folder, now + ".png")).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((200, 200), Image.LANCZOS)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=85)
    return (f"<img src='data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}'>"
            f"<br><span class='small'>{now}</span>")


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

# Paper_Note 를 어떻게 늘려 쓰는지 그림 (1024px 를 260px 로 줄여서)
size = 1024
e, m = EDGE * 260 / size, (size - 2 * EDGE) * 260 / size
cells = [("그대로", "c"), ("가로로 반복", "h"), ("그대로", "c"),
         ("세로로<br>반복", "v"), (f"바둑판처럼 반복<br>{size - 2 * EDGE}×{size - 2 * EDGE}", "m"), ("세로로<br>반복", "v"),
         ("그대로", "c"), ("가로로 반복", "h"), ("그대로", "c")]
slice_fig = (f"<div class='slice' style='grid-template-columns:{e:.0f}px {m:.0f}px {e:.0f}px;grid-template-rows:{e:.0f}px {m:.0f}px {e:.0f}px'>"
             + "".join(f"<div class='{k}'>{t}</div>" for t, k in cells) + "</div>")
panel_rows = "".join(f"<tr><td>{n}</td><td>{w}×{h}</td></tr>" for n, w, h in PANELS)

names = sorted(r[0] for r in REQUEST)
tree = "\n".join(["FindDokkaebi_Art_Paper.zip", f"└─ {ZIP_FOLDER}/"] +
                 [f"   {'└─' if i == len(names) - 1 else '├─'} {n}.png" for i, n in enumerate(names)])

CSS = BASE_CSS + """
.pic { width: 92px; text-align: center; background: #f4f7f8; } .pic img { max-width: 80px; max-height: 80px; }
.small { font-size: 10px; color: #6d645c; } tr, figure, .keep { break-inside: avoid; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-family: Consolas, monospace; font-size: 11px; line-height: 1.4; }
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 8px 12px; margin: 10px 0; }
.shots { display: grid; grid-template-columns: repeat(6, 1fr); gap: 8px; } figure { margin: 0; }
figure img { width: 100%; border: 1px solid #ddd6cc; border-radius: 4px; }
figcaption { font-size: 10px; color: #6d645c; margin-top: 3px; text-align: center; }
.how { display: flex; gap: 24px; align-items: flex-start; } .how table { width: auto; }
.slice { display: grid; border: 1px solid #8a6a4a; font-size: 9px; text-align: center; flex: none; }
.slice div { display: flex; align-items: center; justify-content: center; line-height: 1.2; border: 1px dashed #c9b49a; }
.slice .c { background: #f3d9b5; } .slice .h, .slice .v { background: #f8ead6; } .slice .m { background: #fdfcf5; }
"""

html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>아트팀 전달사항</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 아트팀 전달사항</h1><p class='sub'>{date.today()} 기준 · 출품 마감 2026-11-15(일)</p>
<div class='note'><b>새 요청:</b> 쪽지 질감 {len(REQUEST)}장 (<code>Paper_Note</code> 먼저)<br>
<b>기한:</b> 11월 14일(토). 늦으면 지금 그림으로 출품해요. <code>Paper_Note</code>만 먼저 보내셔도 돼요.<br>
<b>그 밖의 수정본:</b> 같은 기한, 파일 이름은 기존과 같게</div>

<h2>왜 바꾸나요</h2>
<p>배경과 캐릭터는 손그림 느낌인데, 대사 상자 · 그림판 · 채점표 같은 판은 흰 카드 한 장으로 그려져 있어서 화면에서 따로 떠 보여요.
게임 이야기도 "쪽지에 그려서 도깨비에게 건네는" 흐름이라, 판들을 같은 쪽지 종이로 맞추려고 해요.</p>
<div class='shots'>
{shot('1단계', '시작 대화 9', '대사 상자')}
{shot('1단계', '떠올리기 3초', '떠올리기 칸')}
{shot('1단계', '그리기 시작 : 빈 쪽지', '그림판 (클립보드 안)')}
{shot('1단계', '건네기 : 쪽지와 간식', '건네는 쪽지')}
{shot('4단계', '색 섞기 팔레트', '색 섞기 판')}
{shot('3단계', '포기 : 채점표', '채점표')}
</div>

{sections}

<div class='keep'><h2><code>Paper_Note</code>를 게임이 쓰는 방법</h2>
<p>판마다 크기가 달라서 한 장을 아래처럼 아홉 칸으로 나눠 늘려 써요. 귀퉁이 네 칸은 그대로 쓰고, 바깥 띠({EDGE}px)는 늘리지 않고 반복만 해서 테두리 굵기가 어느 판에서나 같아요.
2배(2048px)로 그리면 띠도 2배({EDGE * 2}px)로 해 주세요.</p>
<div class='how'>{slice_fig}
<table><tr><th>깔리는 판</th><th>크기 (배경 그림 기준 px)</th></tr>{panel_rows}</table></div></div>

<h2>요청하지 않는 그림</h2>
<p>다시 · 포기 버튼, 대사 상자의 화살표, 채점표의 동그라미와 빗금, 도구 버튼, 클립보드 틀(<code>Draw_ClipboardFrame</code>)은 지금 그림을 그대로 써요.</p>
<div class='keep'><h2>규칙</h2>
<ul><li><b>파일 이름</b>을 표와 똑같이 해 주세요.</li>
<li><b>가로세로 비율</b>을 표의 크기와 똑같이 맞춰 주세요. 2배로 그리는 건 괜찮아요.</li>
<li><code>Paper_Note</code>는 귀퉁이 바깥이 <b>투명한 PNG</b>, <code>Draw_Paper</code>는 <b>투명 없이 꽉 채운 PNG</b>로 해 주세요.</li>
<li>그림 안에 글자를 넣지 말아 주세요. 글자는 게임이 올려요.</li></ul></div>
<div class='keep'><h2>보내는 방법</h2>
<p>아래 구조 그대로 ZIP 하나로 묶어 주세요. 있는 파일만 넣으면 돼요. 수정본은 ZIP 이름 끝에 <code>_v2</code>, <code>_v3</code>을 붙이고 바뀐 파일만 넣어 주세요.</p>
<pre>{tree}</pre></div>
<div class='keep'><h2>받은 뒤 (개발 쪽)</h2>
<p>아래 명령이 이름 · 비율 · 투명 배경을 검사하고, 문제가 없으면 <code>Assets/Sprites/UI</code>에 넣어요. 둘 다 새 이름이라 코드 연결이 필요해요.
<code>Paper_Note</code>는 흰 카드 자리에 이어 그리기(Tiled)로, <code>Draw_Paper</code>는 그림판 뒤에 깔고 아이가 그린 선만 투명한 층에 따로 그려요 (판정은 선 층만 봐요).</p>
<pre>python Tools/docs/apply_art_zip.py 받은파일.zip          (검사만)
python Tools/docs/apply_art_zip.py 받은파일.zip --apply  (검사 후 넣기)</pre></div>
</body></html>"""

print("요청", len(REQUEST), "장")
os.makedirs(os.path.join(ROOT, "Temp", "DocsHtml"), exist_ok=True)
open(os.path.join(ROOT, "Temp", "DocsHtml", "아트.html"), "w", encoding="utf-8").write(html)
print("아트.pdf", write_pdf(html, os.path.join(OUT, "아트.pdf")))

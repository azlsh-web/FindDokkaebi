# 4_아트팀_전달사항.pdf : 아트팀에 보내는 요청
#   python Tools/docs/make_art_request.py
#   요청 목록은 art_tree_data.REQUEST 하나예요. 받은 ZIP 을 넣는 스크립트(apply_art_zip.py)도 같은 목록을 써요.
import base64
import io
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_tree_data import GROUPS, REQUEST, ZIP_FOLDER
from doc_pdf import BASE_CSS, write_pdf
from flow_shots import ROOT, data_url, find

SP = os.path.join(ROOT, "Assets", "Sprites")
OUT = os.path.join(ROOT, "Docs", "전달")
os.makedirs(OUT, exist_ok=True)


def code(text):
    """`이름` 을 코드 글자로"""
    parts = text.split("`")
    return "".join(f"<code>{p}</code>" if i % 2 else p for i, p in enumerate(parts))


def thumb(folder, name):
    path = os.path.join(SP, folder, name + ".png")
    if not os.path.exists(path):
        return "<span class='small'>새 그림</span>"
    im = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((200, 200), Image.LANCZOS)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=85)
    return f"<img src='data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}'>"


def shot(section, caption, label):
    return f"<figure><img src='{data_url(find(section, caption), 480)}'><figcaption>{label}</figcaption></figure>"


sections = ""
for key, title, why in GROUPS:
    rows = [r for r in REQUEST if r[2] == key]
    body = "".join(
        f"<tr><td class='pic'>{thumb(folder, name)}</td><td><code>{name}.png</code><br><span class='small'>{w}×{h}px</span></td>"
        f"<td>{where}</td><td>{code(note)}</td></tr>"
        for name, folder, _, (w, h), where, note in rows)
    sections += (f"<h2>{title} {len(rows)}장</h2><p>{why}</p>"
                 f"<table><tr><th>지금 그림</th><th style='width:24%'>파일 이름 · 크기</th><th style='width:20%'>나오는 곳</th><th>그려 주실 내용</th></tr>{body}</table>")

names = sorted(r[0] for r in REQUEST)
tree = "\n".join(["FindDokkaebi_Art_v1.zip", f"└─ {ZIP_FOLDER}/"] +
                 [f"   {'└─' if i == len(names) - 1 else '├─'} {n}.png" for i, n in enumerate(names)])

first = [r for r in REQUEST if r[2] == "먼저"]
CSS = BASE_CSS + """
.pic { width: 92px; text-align: center; background: #f4f7f8; } .pic img { max-width: 80px; max-height: 80px; }
.small { font-size: 10px; color: #6d645c; } tr, figure, .keep { break-inside: avoid; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-family: Consolas, monospace; font-size: 11px; line-height: 1.4; }
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 8px 12px; margin: 10px 0; }
.shots { display: flex; gap: 12px; } figure { margin: 0; flex: 1; } figure img { width: 100%; border: 1px solid #ddd6cc; border-radius: 4px; }
figcaption { font-size: 10.5px; color: #6d645c; margin-top: 3px; text-align: center; }
"""

html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>아트팀 전달사항</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 아트팀 전달사항</h1><p class='sub'>2026-11-05 기준 · 출품 마감 2026-11-08(일)</p>
<div class='note'><b>먼저 그림 {len(first)}장을 부탁드려요.</b> 소풍 배경, 빈손 도깨비, 대파 든 도깨비, 도깨비 얼굴이에요. 지금은 개발 쪽 임시 그림이에요. 넣고 확인할 시간이 필요해서 <b>11월 7일(토)까지</b> 받으면 좋겠어요. 일부만 먼저 보내셔도 돼요.</div>
<h2>임시 그림이 나오는 화면</h2>
<div class='shots'>
{shot('이야기 종료', '전부 찾았을 때', '소풍 배경')}
{shot('1단계', '시작 대화 2', '빈손 도깨비와 도깨비 얼굴')}
{shot('1단계를 못 찾았을 때', '대파', '대파 든 도깨비')}
{shot('1단계', '그리기 시작', '향과 촛대')}
</div>
{sections}
<h2>함께 정할 것 : 친구 머리 색</h2>
<p>지금 게임은 빨강 + 노랑 + 파랑을 섞으면 갈색이 되고, 5단계 친구의 머리가 그 갈색이에요. 세 색을 섞어 갈색이 된다는 설정이 어색해서, 검은 머리로 바꾸는 것도 생각하고 있어요. <b>결정 전까지는 그리지 말아 주세요.</b></p>
<table><tr><th>안</th><th>그림 작업</th><th>게임 규칙</th></tr>
<tr><td>지금처럼 갈색</td><td>없음</td><td>그대로</td></tr>
<tr><td>검은 머리</td><td>친구 원본과, 머리 색 기준을 맞힌 오답 그림까지 총 16장을 다시 그려야 해요.</td><td>검정 물감은 처음부터 있어서, 5단계의 세 색 섞기 기준을 새로 정해야 해요.</td></tr></table>
<h2>요청하지 않는 그림</h2>
<p>다시 · 포기 아이콘, 채점표의 동그라미와 빗금, 대사 상자의 화살표, 도구 버튼 아이콘은 개발 쪽에서 만든 그림을 그대로 써요.</p>
<div class='keep'><h2>규칙</h2>
<ul><li><b>파일 이름</b>을 표와 똑같이 해 주세요. 같은 이름이면 게임에 바로 들어가요.</li>
<li><b>가로세로 비율</b>을 표의 크기와 똑같이 맞춰 주세요. 비율이 다르면 찌그러져요. 2배로 그리는 건 괜찮아요.</li>
<li><b>배경은 투명한 PNG</b>로 해 주세요. <code>BG_Picnic</code>만 투명 없이 꽉 채워 주세요.</li>
<li>그림 안에 글자를 넣지 말아 주세요.</li>
<li>움직임은 게임이 처리하니 한 장씩 멈춘 그림으로 그려 주세요.</li></ul></div>
<div class='keep'><h2>보내는 방법</h2>
<p>아래 구조 그대로 ZIP 하나로 묶어 주세요. 있는 파일만 넣으면 돼요. 수정본은 ZIP 이름만 <code>_v2</code>, <code>_v3</code>으로 올리고 바뀐 파일만 넣어 주세요.</p>
<pre>{tree}</pre></div>
<div class='keep'><h2>받은 뒤 (개발 쪽)</h2>
<p>아래 명령이 이름 · 비율 · 투명 배경을 검사하고, 문제가 없으면 같은 이름 파일을 덮어써요. <code>선택</code> 표정 3장은 코드 연결도 필요해요.</p>
<pre>python Tools/docs/apply_art_zip.py 받은파일.zip          (검사만)
python Tools/docs/apply_art_zip.py 받은파일.zip --apply  (검사 후 덮어쓰기)</pre></div>
</body></html>"""

print("요청", len(REQUEST), "장")
os.makedirs(os.path.join(ROOT, "Temp", "DocsHtml"), exist_ok=True)
open(os.path.join(ROOT, "Temp", "DocsHtml", "4_아트팀_전달사항.html"), "w", encoding="utf-8").write(html)
print("4_아트팀_전달사항.pdf", write_pdf(html, os.path.join(OUT, "4_아트팀_전달사항.pdf")))

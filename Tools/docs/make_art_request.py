# 4_아트팀_전달사항.pdf : 아트팀에 보내는 요청 (받아서 넣으면 아트 작업이 끝나요)
#   python make_art_request.py <FindDokkaebi 폴더>
#   요청 목록은 art_tree_data.REQUEST 하나예요. 받은 ZIP 을 넣는 스크립트(apply_art_zip.py)도 같은 목록을 써요.
import base64, io, os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_tree_data import REQUEST, ZIP_FOLDER
from doc_pdf import BASE_CSS, write_pdf

ROOT = os.path.abspath(sys.argv[1])
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")
SHOT = os.path.join(ROOT, "Temp", "SelfTest", "16_s4_draw.png")


def data_url(im, fmt="JPEG"):
    buf = io.BytesIO()
    im.save(buf, fmt, quality=85)
    return f"data:image/{fmt.lower()};base64," + base64.b64encode(buf.getvalue()).decode()


def thumb(name):
    im = Image.open(os.path.join(UI, name + ".png")).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((130, 130), Image.LANCZOS)
    return data_url(bg.convert("RGB")), im.size


rows = ""
for name, where, shown, note in REQUEST:
    url, size = thumb(name)
    rows += (f"<tr><td class='pic'><img src='{url}'></td><td><code>{name}.png</code></td><td class='num'>{size[0]}×{size[1]}</td>"
             f"<td>{where}<br><span class='small'>화면에서 {shown}</span></td><td>{note}</td></tr>")

tree = ["FindDokkaebi_Art_v1.zip", f"└─ {ZIP_FOLDER}/"]
names = sorted(r[0] for r in REQUEST)
tree += [f"   {'└─' if i == len(names) - 1 else '├─'} {n}.png" for i, n in enumerate(names)]
tree_text = "\n".join(tree)

shot_html = ""
if os.path.exists(SHOT):
    im = Image.open(SHOT).convert("RGB")
    im.thumbnail((300, 533), Image.LANCZOS)
    shot_html = f"<div class='shot'><img src='{data_url(im)}'><p class='small'>지금 게임 화면 : 오른쪽이 향과 촛대예요.</p></div>"

CSS = BASE_CSS + """
.pic { width: 92px; text-align: center; background: #f4f7f8; } .pic img { max-width: 80px; max-height: 80px; }
.small { font-size: 10px; color: #6d645c; } tr { break-inside: avoid; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-family: Consolas, monospace; font-size: 11px; line-height: 1.4; }
.two { display: flex; gap: 18px; align-items: flex-start; } .two > div:first-child { flex: 1; }
.shot { width: 220px; flex: none; } .shot img { width: 100%; border: 1px solid #ddd6cc; }
"""
html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>아트팀 전달사항</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 아트팀 전달사항</h1><p class='sub'>2026-11-04 기준</p>
<div class='note'><b>그려 주실 그림은 {len(REQUEST)}장이에요.</b> 그리는 동안 시간을 알려 주는 향과 촛대예요. 시안에 없어 개발 쪽에서 임시로 그렸어요. 다른 그림은 시안과 받은 그림으로 해결했으니, 이 {len(REQUEST)}장을 받아서 넣으면 아트 작업은 끝나요.</div>
<div class='two'><div>
<h2>규칙</h2>
<ul><li><b>파일 이름</b>을 표와 똑같이 해 주세요. 같은 이름이면 게임에 바로 들어가요.</li>
<li><b>가로세로 비율</b>을 표의 크기와 똑같이 맞춰 주세요. 게임이 그 칸에 맞춰 늘리기 때문에 비율이 다르면 찌그러져요. 2배로 그리는 건 좋아요.</li>
<li><b>배경은 투명</b>한 PNG로 해 주세요.</li>
<li>그림 안에 글자를 넣지 말아 주세요.</li></ul>
<h2>보내는 방법</h2>
<p>아래 구조 그대로 ZIP 하나로 묶어 주세요. PNG만 넣어 주세요. 일부만 먼저 보내도 돼요. 수정본은 ZIP 이름만 <code>_v2</code>, <code>_v3</code>으로 올리고, 바뀐 파일만 넣어 주세요.</p>
<pre>{tree_text}</pre>
</div>{shot_html}</div>
<h2>그려 주실 그림 {len(REQUEST)}장</h2>
<table><tr><th>지금 임시 그림</th><th>파일 이름</th><th class='num'>크기(px)</th><th>자리</th><th>주의할 점</th></tr>{rows}</table>
<p class='small'>향 줄기 · 불씨 · 재는 같은 폭의 칸에 가운데 맞춰 겹쳐요. 불꽃과 재가 움직이는 건 게임이 처리하니, 한 장씩 멈춘 그림으로 그려 주세요.</p>
<h2>받은 뒤 (개발 쪽)</h2>
<p>아래 명령이 파일 이름 · 비율 · 투명 배경을 검사하고, 문제가 없으면 <code>Assets/Sprites/UI</code>의 같은 이름 파일을 덮어써요. 코드와 씬은 고치지 않아도 돼요.</p>
<pre>python Tools/docs/apply_art_zip.py 받은파일.zip          (검사만)
python Tools/docs/apply_art_zip.py 받은파일.zip --apply  (검사 후 덮어쓰기)</pre>
<p>그다음 Unity에서 자동 플레이 테스트를 돌리고 웹 빌드 후 배포하면 끝이에요.</p>
</body></html>"""
print("요청", len(REQUEST))
print("4_아트팀_전달사항.pdf", write_pdf(html, os.path.join(ROOT, "Docs", "4_아트팀_전달사항.pdf")))

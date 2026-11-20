# 아트 구조 PDF (paths.ART_STRUCTURE) : 게임 그림이 화면마다 어떻게 들어가 있는지 트리로 정리해요.
#   python Tools/docs/make_art_structure.py
#   트리에 없는 그림이 씬에서 쓰이거나, 없는 파일을 적으면 멈춰요. 문서가 실제 게임과 어긋나지 않게 하려고요.
from datetime import date
import base64, io, itertools, os, re, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_tree_data import STAGES, TREE
from request_data import inventory
from doc_pdf import BASE_CSS, write_pdf
import paths

ROOT = paths.ROOT
SP = os.path.join(ROOT, "Assets", "Sprites")

def norm(p):
    return p.replace("UI/../", "")


def thumb(rel, box=46):
    path = os.path.join(SP, norm(rel) + ".png")
    im = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((box * 2, box * 2), Image.LANCZOS)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=80)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


def scene_used():
    """씬과 단계 데이터가 실제로 참조하는 그림 (파일 이름 없는 Unity 참조는 guid 로 찾아요)"""
    guid_to_path = {}
    for base, _, files in os.walk(SP):
        for f in files:
            if f.endswith(".png"):
                path = os.path.join(base, f)
                m = re.search(r"^guid: ([0-9a-f]{32})", open(path + ".meta", encoding="utf-8").read(), re.M)
                guid_to_path[m.group(1)] = os.path.relpath(path, SP).replace(os.sep, "/")[:-4]
    used = set()
    files = [os.path.join(ROOT, "Assets", "Scenes", "Game.unity")]
    data = os.path.join(ROOT, "Assets", "Data")
    files += [os.path.join(data, f) for f in os.listdir(data) if f.endswith(".asset")]
    for path in files:
        for g in re.findall(r"guid: ([0-9a-f]{32})", open(path, encoding="utf-8", errors="ignore").read()):
            if g in guid_to_path:
                used.add(guid_to_path[g])
    return used, set(guid_to_path.values())


listed = {norm(f) for _, _, rows in TREE for _, f, _ in rows if "/" in f and "아래" not in f}
for _, folder, key, n, _, _ in STAGES:
    listed.add(f"{folder}/{key}_Original")
    for marks in itertools.product("OX", repeat=n):
        if "X" in marks:
            listed.add(f"{folder}/{key}_Wrong_{''.join(marks)}")
used, existing = scene_used()
missing_file = sorted(listed - existing)
not_in_doc = sorted(existing - listed)
used_not_listed = sorted(used - listed)
assert not missing_file, ("문서에 있는데 파일이 없어요", missing_file)
assert not not_in_doc, ("파일이 있는데 문서에 없어요", not_in_doc)
assert not used_not_listed, ("씬에서 쓰는데 트리에 없어요", used_not_listed)
print("트리에 적었는데 씬 참조는 없음:", sorted(listed - used))

CSS = BASE_CSS + """
ul.tree, ul.tree ul { list-style: none; margin: 0; padding-left: 16px; }
ul.tree { padding-left: 0; }
ul.tree li { position: relative; padding: 2px 0 2px 12px; break-inside: avoid; }
ul.tree ul > li::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; border-left: 1px solid #c9c1b6; }
ul.tree ul > li::after { content: ''; position: absolute; left: 0; top: 18px; width: 9px; border-top: 1px solid #c9c1b6; }
ul.tree ul > li:last-child::before { bottom: auto; height: 18px; }
.node { display: flex; align-items: center; gap: 8px; min-height: 34px; }
.node img { width: 34px; height: 34px; object-fit: contain; background: #e8f0f4; border: 1px solid #ddd6cc; border-radius: 3px; flex: none; }
.obj { min-width: 170px; color: #6d645c; font-size: 11px; }
.file { font-family: Consolas, monospace; font-size: 11.5px; font-weight: 700; min-width: 215px; }
.src { font-size: 10.5px; color: #6d645c; }
.screen { font-weight: 700; font-size: 13px; }
.path { font-family: Consolas, monospace; color: #8a8177; font-size: 10.5px; margin-left: 6px; font-weight: 400; }
.grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px 8px; margin: 2px 0 8px; }
.cell { display: flex; align-items: center; gap: 5px; font-family: Consolas, monospace; font-size: 9.5px; break-inside: avoid; }
.cell img { width: 26px; height: 26px; object-fit: contain; background: #e8f0f4; border: 1px solid #ddd6cc; border-radius: 3px; flex: none; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-size: 11px; line-height: 1.5; font-family: Consolas, monospace; }
"""

p = [f"<h1>찾아라! 도깨비 아트 구조</h1><p class='sub'>게임에 들어간 그림 파일이 화면과 오브젝트에 어떻게 연결돼 있는지 정리한 문서예요. {date.today()} 기준이며, 씬과 단계 데이터의 실제 참조와 대조해 만들었어요.</p>"]
counts = {k: len([e for e in existing if e.split("/")[0] == k]) for k in ("Background", "Characters", "UI")}
stage_total = sum(len([e for e in existing if e.startswith(s[1] + "/")]) for s in STAGES)
current = [r for r in inventory() if r.team == "아트" and r.status != "없음"]
p.append(f"<h2>한눈에</h2><p><b>그림은 <code>Assets/Sprites</code> 안에 있어요.</b> 보관 파일까지 {len(existing)}장이고, 현재 요청 현황에는 {len(current)}개 파일을 관리해요. 아래 트리에는 미사용 표시가 있는 옛 부품도 남아 있어요. 새 요청과 수령 상태의 기준은 요청 현황이에요.</p>")
p.append("<table><tr><th>폴더</th><th>보관 포함 파일 수</th></tr>" + "".join(
    f"<tr><td>{folder}</td><td>{sum(e.startswith(folder + '/') for e in existing)}</td></tr>"
    for folder in sorted({e.split('/')[0] for e in existing})) + "</table>")
p.append("<table><tr><th>출처</th><th>뜻</th></tr><tr><td>아트팀</td><td>아트 담당자에게 받은 그림</td></tr><tr><td>Codex</td><td>원본을 바탕으로 Codex가 만든 그림</td></tr><tr><td>기획서 그림 N</td><td>기획서 시안에서 오린 부품</td></tr><tr><td>코드로 그림</td><td>개발 쪽에서 코드로 만든 그림</td></tr><tr><td>개발 임시 그림</td><td>아트팀 그림이 오기 전까지 쓰는 그림. <b>아트팀 요청</b>이라고 적힌 것은 받으면 같은 이름으로 바뀌어요 (4번 문서)</td></tr></table>")

requested = {r.file.removeprefix("Sprites/")[:-4] for r in current if r.action == "교체"}
p.append("<h2>화면별 트리</h2><ul class='tree'>")
for screen, path, rows in TREE:
    p.append(f"<li><div class='screen'>{screen}<span class='path'>{path}</span></div><ul>")
    for obj, f, src in rows:
        if "/" in f and "아래" not in f:
            p.append(f"<li><div class='node'><img src='{thumb(f)}'><span class='obj'>{obj}</span><span class='file'>{norm(f)}.png</span><span class='src'>{src}{' · 교체 요청' if norm(f) in requested else ''}</span></div></li>")
        else:
            p.append(f"<li><div class='node'><span class='obj'>{obj}</span><span class='src'>{f}</span></div></li>")
    p.append("</ul></li>")
p.append("</ul>")

p.append("<h2>현재 물건 그림</h2><p>원본은 성공할 때, 오답은 틀린 횟수에 따라 보여요. 4챕터는 물건마다 오답 한 장이에요. 옛 O/X 조합 파일 중 지금 쓰지 않는 것은 요청에서 뺐어요. 판정 기준은 게임 플로우의 챕터별 물건 표를 보세요.</p><div class='grid'>")
for r in current:
    if r.group == "1챕터 물건" or re.search(r"Ch[2345]_.*_(Original|Wrong\d)", r.file):
        rel = r.file.removeprefix("Sprites/")[:-4]
        p.append(f"<div class='cell'><img src='{thumb(rel, 26)}'><span>{rel}<br>{r.status}</span></div>")
p.append("</div>")

html = f"<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>아트 구조</title><style>{CSS}</style></head><body>{''.join(p)}</body></html>"
print(paths.ART_STRUCTURE, write_pdf(html, paths.doc(paths.ART_STRUCTURE)))

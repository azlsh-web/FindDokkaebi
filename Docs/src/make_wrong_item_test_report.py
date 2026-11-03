# 경우의 수별 테스트 보고서 (Temp/JudgeCases + Temp/SelfTest) → HTML → Edge 로 PDF
#   python make_wrong_item_test_report.py <FindDokkaebi 폴더>
import base64, csv, io, os, re, subprocess, sys, tempfile, time
from PIL import Image

ROOT = os.path.abspath(sys.argv[1])
DOCS = os.path.join(ROOT, "Docs")
CASES = os.path.join(ROOT, "Temp", "JudgeCases")
SELF = os.path.join(ROOT, "Temp", "SelfTest", "result.txt")
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
NAME = "8_경우의_수_테스트_보고서"

STAGES = {
    1: ("방망이", "Bat", ["형태"]),
    2: ("가방", "Bag", ["형태", "패턴"]),
    3: ("돗자리", "Mat", ["형태", "패턴", "삼원색"]),
    4: ("김밥", "Gimbap", ["형태", "패턴", "삼원색", "2색"]),
    5: ("친구", "Friend", ["형태", "패턴", "삼원색", "2색", "3색"]),
}


def img(path, width, quality=72):
    im = Image.open(path).convert("RGB")
    im.thumbnail((width, 10000), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=quality)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


rows = list(csv.DictReader(open(os.path.join(CASES, "results.csv"), encoding="utf-8-sig")))
for r in rows:
    stage = int(r["stage"])
    r["want_item"] = "Original" if "X" not in r["judged"] else f"Wrong-{STAGES[stage][1]}-{r['judged']}"
    r["judge_ok"] = r["expected"] == r["judged"]
    r["item_ok"] = r["returned"] == r["want_item"]
    r["ok"] = r["judge_ok"] and r["item_ok"]

self_text = open(SELF, encoding="utf-8").read()
self_result = re.search(r"\[SelfTest\][^\n]*", self_text).group(0).replace("[SelfTest] ", "")
all_ok = all(r["ok"] for r in rows)

CSS = """
@page { size: A4; margin: 14mm 12mm; }
* { box-sizing: border-box; }
body { font-family: 'Malgun Gothic', sans-serif; color: #2b2622; font-size: 12px; line-height: 1.5; margin: 0; }
h1 { font-size: 24px; margin: 0 0 4px; }
h2 { font-size: 17px; margin: 22px 0 8px; break-after: avoid; }
.sub { color: #6d645c; margin: 0 0 14px; }
table { border-collapse: collapse; width: 100%; margin: 8px 0 14px; }
th, td { border: 1px solid #d8d2ca; padding: 5px 8px; text-align: left; vertical-align: top; }
th { background: #f4f0ea; }
.num { text-align: right; }
.grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
.card { border: 1px solid #d8d2ca; border-radius: 6px; padding: 6px; break-inside: avoid; }
.card.bad { border: 2px solid #c62828; background: #fff4f4; }
.pics { display: grid; grid-template-columns: 1fr 1fr; gap: 5px; }
.pics img { width: 100%; display: block; border: 1px solid #e4dfd8; border-radius: 3px; }
.cap { font-size: 9px; color: #6d645c; }
.code { font-weight: 700; font-size: 11px; margin-top: 4px; word-break: break-all; }
.marks { font-size: 10px; margin: 2px 0; }
.o { color: #2e7d32; font-weight: 700; }
.x { color: #c62828; font-weight: 700; }
.state { font-weight: 700; font-size: 11px; }
"""

p = ["<h1>경우의 수 테스트 보고서</h1><p class='sub'>판정 조합별로 게임을 실행해 돌려주는 물건을 확인한 결과예요. 2026-11-03 기준이에요.</p>"]
passed = sum(r["ok"] for r in rows)
p.append(f"<h2>결론</h2><p><b>{len(rows)}번 중 {passed}번이 기대대로 나왔어요.</b> 62가지 조합이 게임에서 맞는 그림으로 연결돼 있어요. 다만 이 검사는 실제 AI가 아니라 테스트용 계산식과 손그림으로 돌렸어요.</p>" if all_ok else f"<h2>결론</h2><p><b>{len(rows)}번 중 {passed}번만 기대대로 나왔어요.</b> 빨간 테두리 경우를 확인해야 해요.</p>")
p.append("<table><tr><th>검사</th><th>결과</th><th>확인한 것</th></tr>"
         f"<tr><td>자동 플레이 테스트</td><td>{self_result}</td><td>5단계 흐름, 버튼, 간식, 화면 배치</td></tr>"
         f"<tr><td>판정 조합</td><td>{sum(r['judge_ok'] for r in rows)} / {len(rows)}</td><td>손그림의 O·X가 기대대로 판정되는지 (방망이는 굵은 펜도 검사해 62가지를 {len(rows)}번 실행)</td></tr>"
         f"<tr><td>돌려주는 그림</td><td>{sum(r['item_ok'] for r in rows)} / {len(rows)}</td><td>결과 화면에 나온 그림 파일이 그 조합의 파일인지</td></tr></table>")
p.append("<h2>이번에 확인하지 못한 것</h2><ul>"
         "<li><b>실제 AI 판정:</b> 새 판정 기준을 서버에 배포하지 않아, Gemini가 사람 그림을 어떻게 판정하는지는 확인하지 못했어요.</li>"
         "<li><b>실제 그림으로 판정:</b> 입력은 조합에 맞춰 코드로 그린 손그림이에요. 양 갈래와 천 조각 계산은 테스트용으로 단순하게 만들었어요.</li>"
         "<li><b>웹 빌드:</b> 공개된 웹 게임은 아직 예전 버전이에요.</li></ul>")
p.append("<table><tr><th>물건</th><th>평가 요소</th><th class='num'>경우의 수</th><th class='num'>통과</th></tr>")
for s, (name, _, els) in STAGES.items():
    sr = [r for r in rows if int(r["stage"]) == s]
    p.append(f"<tr><td>{name}</td><td>{' · '.join(els)}</td><td class='num'>{2 ** len(els)}</td><td class='num'>{sum(r['ok'] for r in sr)} / {len(sr)}</td></tr>")
p.append("</table>")

for s, (name, key, els) in STAGES.items():
    p.append(f"<h2>{name}</h2><div class='grid'>")
    for r in (x for x in rows if int(x["stage"]) == s):
        chips = " ".join(f"{els[i]} <span class='{'o' if m == 'O' else 'x'}'>{m}</span>" for i, m in enumerate(r["judged"]))
        pen = " · 굵은 펜" if r["pen"] == "12" else ""
        state = "✓ 통과" if r["ok"] else f"✗ 실패 : 기대 {r['expected']} / 판정 {r['judged']} / 그림 {r['returned']}"
        p.append(f"<div class='card{'' if r['ok'] else ' bad'}'><div class='pics'>"
                 f"<div><img src='{img(os.path.join(CASES, r['drawing']), 200)}'><div class='cap'>입력 그림</div></div>"
                 f"<div><img src='{img(os.path.join(CASES, r['shot']), 240)}'><div class='cap'>결과 화면</div></div></div>"
                 f"<div class='code'>{r['want_item']}{pen}</div><div class='marks'>{chips}</div><div class='state'>{state}</div></div>")
    p.append("</div>")

html = f"<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>경우의 수 테스트 보고서</title><style>{CSS}</style></head><body>{''.join(p)}</body></html>"
html_path = os.path.join(DOCS, "src", NAME + ".html")
open(html_path, "w", encoding="utf-8").write(html)

out = os.path.join(tempfile.mkdtemp(), "out.pdf")
subprocess.run([EDGE, "--headless", "--disable-gpu", "--no-pdf-header-footer", f"--user-data-dir={tempfile.mkdtemp(prefix='docs_edge_')}",
                f"--print-to-pdf={out}", "file:///" + html_path.replace("\\", "/")], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(90):      # Edge 는 파일을 다 쓰기 전에 돌아올 때가 있어요
    if os.path.exists(out) and os.path.getsize(out) > 0:
        size = os.path.getsize(out)
        time.sleep(3)
        if os.path.getsize(out) == size:
            break
    time.sleep(1)
os.replace(out, os.path.join(DOCS, NAME + ".pdf"))
sys.stdout.reconfigure(encoding="utf-8")
print("rows", len(rows), "ok", passed, "judge", sum(r["judge_ok"] for r in rows), "item", sum(r["item_ok"] for r in rows), os.path.getsize(os.path.join(DOCS, NAME + ".pdf")))
for r in rows:
    if not r["ok"]:
        print("FAIL", r["stage"], r["drawn"], r["judged"], r["returned"], "want", r["want_item"])

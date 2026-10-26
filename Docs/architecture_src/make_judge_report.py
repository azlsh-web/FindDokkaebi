# 판정 전수 검사 결과(Temp/JudgeCases) → HTML → PDF
import sys, os, csv, base64, io, datetime
from PIL import Image

case_dir, out_html = sys.argv[1], sys.argv[2]

STAGES = {
    1: ("방망이", ["길쭉한 손잡이", "손잡이보다 굵은 머리", "머리 바깥의 돌기 2개 이상"]),
    2: ("가방", ["네모난 가방 몸체", "위쪽 손잡이", "앞쪽 주머니", "주머니 안 가로 줄무늬"]),
    3: ("돗자리", ["사각형 외곽", "네 칸으로 나누는 십자선", "왼쪽 위 빨강", "오른쪽 위 노랑", "아래 두 칸 파랑"]),
    4: ("김밥", ["검은 김 테두리", "흰 밥", "빨간 재료", "노란 재료", "초록 재료"]),
    5: ("친구", ["사람 모습", "상의 가로 줄무늬", "초록 상의", "갈색 하의"]),
}
HOW = {
    1: "빠뜨림 그림 : 손잡이 없음 / 머리를 손잡이와 같은 굵기의 막대로(손잡이가 없으면 작은 동그라미) / 돌기 선 없음",
    2: "빠뜨림 그림 : 몸체를 동그랗게 / 손잡이 없음 / 주머니 없음 / 줄무늬 없음",
    3: "빠뜨림 그림 : 외곽을 동그랗게 / 십자선 없음 / 왼쪽 위 노랑 / 오른쪽 위 빨강 / 아래 칸 빨강",
    4: "빠뜨림 그림 : 테두리를 주황으로 / 밥을 갈색으로 칠함 / 빨강 없음 / 노랑 없음 / 초록 대신 파랑",
    5: "빠뜨림 그림 : 다리 대신 막대 하나(허수아비) / 줄무늬 없음 / 상의 파랑 / 하의 보라",
}


def img64(path, width, crop=None, quality=72):
    im = Image.open(path).convert("RGB")
    if crop:
        w, h = im.size
        im = im.crop((0, int(h * crop[0]), w, int(h * crop[1])))
    r = width / im.width
    im = im.resize((width, int(im.height * r)), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=quality)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


rows = list(csv.DictReader(open(os.path.join(case_dir, "results.csv"), encoding="utf-8-sig")))
total = len(rows)
passed = sum(r["pass"] == "PASS" for r in rows)

per_stage = {}
for r in rows:
    st = int(r["stage"])
    d = per_stage.setdefault(st, [0, 0])
    d[1] += 1
    d[0] += r["pass"] == "PASS"

# 특징별 정확도 (기대와 판정이 같은 비율)
feat_ok = {}
for r in rows:
    st = int(r["stage"])
    for i, (e, j) in enumerate(zip(r["expected"], r["judged"])):
        k = (st, i)
        a = feat_ok.setdefault(k, [0, 0])
        a[1] += 1
        a[0] += e == j

css = """
@page { size: A4; margin: 12mm 11mm; }
:root { --ink:#4a2b16; --muted:#7a604a; --line:#d9c7ad; --card:#fbf5e7; --ok:#2e7d32; --bad:#c62828; --coral:#ed8d80; }
* { box-sizing: border-box; }
body { font-family: "Malgun Gothic", "Noto Sans KR", sans-serif; color: var(--ink); margin: 0; font-size: 9.5pt; line-height: 1.45; }
h1 { font-size: 20pt; margin: 0 0 4px; }
h2 { font-size: 13pt; margin: 14px 0 6px; padding-bottom: 3px; border-bottom: 2.5px solid var(--ink); page-break-after: avoid; }
.sub { color: var(--muted); margin-bottom: 10px; }
.big { font-size: 34pt; font-weight: bold; }
.summary { display: flex; gap: 10px; align-items: stretch; margin: 8px 0 10px; }
.box { border: 2.5px solid var(--ink); border-radius: 14px; background: var(--card); padding: 8px 12px; }
table { border-collapse: collapse; width: 100%; font-size: 8.8pt; }
th, td { border: 1px solid var(--line); padding: 3px 6px; text-align: left; }
th { background: var(--card); }
.ok { color: var(--ok); font-weight: bold; } .bad { color: var(--bad); font-weight: bold; }
.grid { display: flex; flex-wrap: wrap; gap: 6px; }
.case { width: calc(25% - 5px); border: 2px solid var(--ink); border-radius: 10px; background: #fff; padding: 4px; page-break-inside: avoid; }
.case.fail { border-color: var(--bad); box-shadow: 0 0 0 2px #f6c4bf inset; }
.case img.shot { width: 100%; display: block; border-radius: 6px; }
.case .head { display: flex; justify-content: space-between; font-weight: bold; font-size: 8.6pt; margin: 2px 2px 3px; }
.case table { font-size: 7.4pt; }
.case td, .case th { padding: 1px 3px; } .case th { white-space: nowrap; }
.badge { border-radius: 8px; padding: 0 6px; color: #fff; }
.badge.ok { background: var(--ok); color: #fff; } .badge.bad { background: var(--bad); color: #fff; }
.note { border-left: 4px solid var(--coral); background: var(--card); padding: 5px 9px; margin: 6px 0; }
.mini { display:flex; gap:4px; } .mini img { width: 50%; border: 1px solid var(--line); border-radius: 4px; }
.page-break { page-break-before: always; }
"""

h = [f"<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>판정 전수 검사</title><style>{css}</style></head><body>"]
h.append("<h1>찾아라! 도깨비 — 자동 판정 전수 검사</h1>")
h.append(f"<div class='sub'>{datetime.date.today():%Y-%m-%d} · Unity 게임 화면에서 직접 제출해 결과 화면을 캡처 · 판정기 : LocalJudge (HSV 색 분석 + 모양 계산)</div>")
pct = passed / total * 100
h.append("<div class='summary'>")
h.append(f"<div class='box' style='flex:0 0 32%'><div>기대와 같은 판정</div><div class='big'>{passed} / {total}</div><div>{pct:.1f}%</div></div>")
h.append("<div class='box' style='flex:1'><table><tr><th>단계</th><th>경우의 수</th><th>맞음</th><th>틀림</th></tr>")
for st in sorted(per_stage):
    ok, n = per_stage[st]
    h.append(f"<tr><td>{st}단계 {STAGES[st][0]}</td><td>2<sup>{len(STAGES[st][1])}</sup> = {n}</td><td class='ok'>{ok}</td><td class='{'bad' if n - ok else ''}'>{n - ok}</td></tr>")
h.append("</table></div></div>")

h.append("<h2>어떻게 검사했나</h2>")
h.append("<ol style='margin:0 0 6px 18px;padding:0'>"
         "<li>단계마다 특징 하나하나를 <b>그림 / 빠뜨림</b>으로 정해서 모든 조합(2<sup>특징 수</sup>)을 만들었어요.</li>"
         "<li>각 조합을 사람이 마우스로 그린 것처럼 <b>삐뚤고, 조금 끊기고, 성기게 칠한</b> 손그림으로 그렸어요. (Assets/Editor/MvpJudgeCases.cs)</li>"
         "<li>그 그림을 실제 게임의 쪽지에 넣고 <b>제출 → 도깨비 판정 → 결과 화면</b>까지 진행해서 화면을 캡처했어요.</li>"
         "<li>기대한 판정과 실제 판정을 특징마다 비교했어요. 하나라도 다르면 틀림(FAIL)이에요.</li></ol>")
h.append("<div class='note'><b>기대 판정 규칙 (기획서 부록 B 기준)</b><br>"
         "· 방망이 '손잡이보다 굵은 머리'는 손잡이가 없으면 인정하지 않아요 → 손잡이를 빠뜨리면 이 특징도 빠뜨림이 기대값.<br>"
         "· 가방 '주머니 안 가로 줄무늬'는 주머니가 없으면 인정하지 않아요 (주머니 밖 줄은 불인정) → 주머니를 빠뜨리면 이 특징도 빠뜨림.<br>"
         "· 나머지는 그린 그대로가 기대값.</div>")

h.append("<h2>특징별 정확도</h2><table><tr><th>단계</th><th>특징</th><th>맞음 / 경우</th></tr>")
for (st, i), (ok, n) in sorted(feat_ok.items()):
    cls = "ok" if ok == n else "bad"
    h.append(f"<tr><td>{st}단계 {STAGES[st][0]}</td><td>F{i + 1} {STAGES[st][1][i]}</td><td class='{cls}'>{ok} / {n}</td></tr>")
h.append("</table>")

fails = [r for r in rows if r["pass"] != "PASS"]
h.append("<h2>틀린 경우와 이유</h2>")
if not fails:
    h.append("<p>없음</p>")
for r in fails:
    st = int(r["stage"])
    names = STAGES[st][1]
    notes = r["notes"].split(" | ")
    wrong = [f"F{i + 1} {names[i]} : 기대 {'그림' if e == 'O' else '빠뜨림'} → 판정 {'그림' if j == 'O' else '빠뜨림'} ({notes[i] if i < len(notes) else ''})"
             for i, (e, j) in enumerate(zip(r["expected"], r["judged"])) if e != j]
    h.append(f"<div class='note' style='border-left-color:var(--bad)'><b>{st}단계 {STAGES[st][0]} · 그림 {r['drawn']}</b><br>" + "<br>".join(wrong) + "</div>")
if any(int(r["stage"]) == 1 and r["drawn"] == "XXO" for r in fails):
    h.append("<div class='note'><b>알려진 한계 (1단계)</b> : 손잡이 없이 <b>작은 동그라미 머리에 돌기 선만</b> 그리면, 아래로 길게 뻗은 돌기 선 하나를 "
             "짧은 손잡이로 볼 수 있어요. 사람이 봐도 '손잡이가 짧은 방망이'로 볼 수 있는 애매한 그림이라, 기준을 더 엄하게 하면 "
             "진짜 방망이(손잡이가 짧게 그려진 그림)를 틀리게 될 수 있어서 그대로 두었어요. AI 판정(OpenAI)을 쓰면 이런 그림도 더 잘 구분해요.</div>")

for st in sorted(STAGES):
    name, feats = STAGES[st]
    h.append(f"<h2 class='page-break'>{st}단계 {name} — {per_stage[st][0]} / {per_stage[st][1]}</h2>")
    h.append(f"<div class='sub'>{HOW[st]}</div><div class='grid'>")
    for r in [r for r in rows if int(r["stage"]) == st]:
        ok = r["pass"] == "PASS"
        shot = os.path.join(case_dir, r["shot"])
        draw = os.path.join(case_dir, r["drawing"])
        h.append(f"<div class='case {'' if ok else 'fail'}'>")
        h.append(f"<div class='head'><span>그림 {r['drawn']}</span><span class='badge {'ok' if ok else 'bad'}'>{'맞음' if ok else '틀림'}</span></div>")
        if os.path.exists(shot):
            h.append(f"<div class='mini'><img src='{img64(draw, 150)}'><img src='{img64(shot, 150, crop=(0.0, 0.68))}'></div>")
        h.append("<table><tr><th>특징</th><th>기대</th><th>판정</th></tr>")
        for i, fname in enumerate(feats):
            e, j = r["expected"][i], r["judged"][i]
            h.append(f"<tr><td>F{i + 1} {fname}</td><td>{e}</td><td class='{'ok' if e == j else 'bad'}'>{j}</td></tr>")
        h.append("</table></div>")
    h.append("</div>")

h.append("</body></html>")
open(out_html, "w", encoding="utf-8").write("\n".join(h))
print("html", passed, total)

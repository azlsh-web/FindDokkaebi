# 엉뚱한 물건 검수 보고서 (Assets/Sprites 의 물건_Wrong_*.png + 검수 결과) → HTML → Edge 로 PDF
#   python make_wrong_art_report.py <FindDokkaebi 폴더>
import base64, io, itertools, os, subprocess, sys, tempfile, time
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from doc_pdf import write_pdf

ROOT = os.path.abspath(sys.argv[1])
DOCS = os.path.join(ROOT, "Docs", "기록")
NAME = "7_엉뚱한_물건_검수_보고서"

STAGES = [
    ("방망이", "Bat", "Stage1_Bat", [("형태", "")], "대파"),
    ("가방", "Bag", "Stage2_Bag", [("형태", "동그란 몸체"), ("패턴", "줄무늬 없음")], "항아리"),
    ("돗자리", "Mat", "Stage3_Mat", [("형태", "타원형"), ("패턴", "띠 다섯 줄"), ("삼원색", "빨강 띠가 노랑")], "파전"),
    ("김밥", "Gimbap", "Stage4_Gimbap", [("형태", "김 없음"), ("패턴", "초록 재료가 위"), ("삼원색", "단무지가 파랑"), ("2색", "시금치가 파랑")], "고무신"),
    ("친구", "Friend", "Stage5_Friend", [("형태", "단발"), ("패턴", "천 조각 없음"), ("삼원색", "바지가 빨강"), ("2색", "상의가 초록"), ("3색", "머리가 주황")], "허수아비"),
]

# 눈으로 확인한 결과 : 여기 없는 파일은 통과예요
ISSUES = {}
NOTES = [
    "허수아비(Friend_Wrong_XXXXX)와 파전(Mat_Wrong_XXX)은 1차 검수에서 고칠 점이 나와 다시 만든 그림이에요. 허수아비는 원본의 옷을 없앴고, 파전의 파는 초록으로 바꿨어요.",
    "같은 조합 요소의 색이 그림마다 조금 달라요. 친구의 초록 상의, 주황 머리, 김밥의 파란 재료에서 보여요. 틀린 부분을 알아보는 데는 지장이 없어요.",
    "김밥에서 김이 없는 그림은 물건 크기가 그림마다 조금 달라요. 게임 화면에서 비율은 유지돼요.",
]


def img(path, width=280, quality=80):
    im = Image.open(path).convert("RGBA")
    bg = Image.new("RGBA", im.size, (232, 240, 244, 255))
    bg.alpha_composite(im)
    bg.thumbnail((width, width), Image.LANCZOS)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=quality)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


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
.grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; }
.card { border: 1px solid #d8d2ca; border-radius: 6px; padding: 6px; break-inside: avoid; }
.card img { width: 100%; display: block; border-radius: 4px; }
.card.warn { border: 2px solid #c8741a; background: #fff8ef; }
.code { font-weight: 700; font-size: 11px; word-break: break-all; }
.marks { margin: 3px 0; font-size: 10px; }
.o { color: #2e7d32; font-weight: 700; }
.x { color: #c62828; font-weight: 700; }
.state { font-weight: 700; font-size: 11px; }
.desc { font-size: 10.5px; color: #4f4840; }
.issue { font-size: 10.5px; color: #8a4b0b; margin-top: 3px; }
.original { width: 150px; }
"""

pieces = ["<h1>엉뚱한 물건 검수 보고서</h1><p class='sub'>Codex가 만든 그림 57장을 판정 조합별로 원본과 비교했어요. 2026-11-03 기준이며, 게임 테스트 전 그림 자체만 확인한 결과예요.</p>"]
total = len([1 for s in STAGES for m in itertools.product("OX", repeat=len(s[3])) if "X" in m])
warn = len(ISSUES)
pieces.append(f"<h2>결론</h2><p><b>{total}장 모두 그대로 쓸 수 있어요.</b> 파일 이름, 크기(1024×1024), 투명 배경은 57장 모두 맞고 Unity에 연결돼 있어요.</p>")
pieces.append("<table><tr><th>물건</th><th>평가 요소</th><th class='num'>경우의 수</th><th class='num'>만든 그림</th><th class='num'>통과</th><th class='num'>재생성 추천</th></tr>")
for name, key, _, els, _ in STAGES:
    codes = [c for c in map("".join, itertools.product("OX", repeat=len(els))) if "X" in c]
    w = sum(f"{key}_Wrong_{c}" in ISSUES for c in codes)
    pieces.append(f"<tr><td>{name}</td><td>{' · '.join(e for e, _ in els)}</td><td class='num'>{2 ** len(els)}</td><td class='num'>{len(codes)}</td><td class='num'>{len(codes) - w}</td><td class='num'>{w}</td></tr>")
pieces.append("</table>")
if ISSUES:
    pieces.append("<h2>재생성 추천</h2><table><tr><th>파일</th><th>이유</th></tr>")
    for k, v in ISSUES.items():
        pieces.append(f"<tr><td>{k}</td><td>{v}</td></tr>")
    pieces.append("</table>")
pieces.append("<h2>참고</h2><ul>" + "".join(f"<li>{n}</li>" for n in NOTES) + "</ul>")

for name, key, folder, els, other in STAGES:
    pieces.append(f"<h2>{name}</h2><div class='grid'><div class='card'><img src='{img(os.path.join(ROOT, 'Assets', 'Sprites', folder, f'{key}_Original.png'))}'><div class='code'>원본 A</div><div class='desc'>모든 기준 맞음</div></div>")
    for marks in itertools.product("OX", repeat=len(els)):
        if "X" not in marks:
            continue
        fname = f"{key}_Wrong_{''.join(marks)}"
        path = os.path.join(ROOT, "Assets", "Sprites", folder, fname + ".png")
        wrong = [els[i][1] for i, m in enumerate(marks) if m == "X"]
        desc = f"완전히 다른 물건: {other}" if "O" not in marks else ", ".join(wrong)
        chips = " ".join(f"{els[i][0]} <span class='{'o' if m == 'O' else 'x'}'>{m}</span>" for i, m in enumerate(marks))
        issue = ISSUES.get(fname)
        state = "⚠ 재생성 추천" if issue else "✓ 통과"
        pieces.append(f"<div class='card{' warn' if issue else ''}'><img src='{img(path)}'><div class='code'>{fname}</div><div class='marks'>{chips}</div><div class='desc'>{desc}</div><div class='state'>{state}</div>" + (f"<div class='issue'>{issue}</div>" if issue else "") + "</div>")
    pieces.append("</div>")

html = f"<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>엉뚱한 물건 검수 보고서</title><style>{CSS}</style></head><body>{''.join(pieces)}</body></html>"
print(NAME, write_pdf(html, os.path.join(DOCS, NAME + ".pdf")))

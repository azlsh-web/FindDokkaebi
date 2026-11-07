# 1_게임_플로우.pdf : 게임 흐름도와 화면 사진 전체
#   python Tools/docs/make_flow_doc.py
#   먼저 Unity 메뉴 "게임 플로우 사진 찍기"를 실행하세요 (AI 를 부르지 않아요). 사진을 줄여서 Docs/기획/플로우_사진 으로 옮기고 PDF 를 만들어요.
import csv
import html as esc
from datetime import date
import os
import re
import shutil
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from doc_pdf import BASE_CSS, write_pdf
from flow_shots import PHOTOS, ROOT, data_url, load_manifest

SRC = os.path.join(ROOT, "Temp", "FlowShots")
DOCS = os.path.join(ROOT, "Docs", "기획")

# 1) 찍은 사진을 문서 폴더로 (Temp 는 Unity 를 닫으면 지워져요)
if os.path.exists(os.path.join(SRC, "manifest.tsv")):
    done = open(os.path.join(SRC, "done.txt"), encoding="utf-8").read()
    print(done)
    assert "오류 0개" in done, "촬영 중 오류가 있었어요"
    if os.path.isdir(PHOTOS):
        shutil.rmtree(PHOTOS)
    os.makedirs(PHOTOS)
    rows = []
    with open(os.path.join(SRC, "manifest.tsv"), encoding="utf-8", newline="") as f:
        for file, section, caption, line in csv.reader(f, delimiter="\t", quotechar=None):
            name = os.path.splitext(file)[0] + ".jpg"
            im = Image.open(os.path.join(SRC, file)).convert("RGB")
            im = im.resize((540, round(im.height * 540 / im.width)), Image.LANCZOS)
            im.save(os.path.join(PHOTOS, name), "JPEG", quality=84, optimize=True)
            rows.append([name, section, caption, line])
    with open(os.path.join(PHOTOS, "manifest.tsv"), "w", encoding="utf-8", newline="") as f:
        csv.writer(f, delimiter="\t", quotechar=None, quoting=csv.QUOTE_NONE, lineterminator="\n").writerows(rows)

# 2) PDF
FLOW = """시작
└─ 게임 시작하기
   └─ 단계 시작 (1단계 → 5단계, 간식 3개)
      ├─ 1단계만 : 오프닝 (준비물이 사라지는 그림, 대사 4줄, 터치로 넘김)
      ├─ 시작 대화 : 생김새로 판정 기준을 말함 (2단계 첫 대사는 방망이를 찾았는지에 따라 달라짐)
      ├─ 떠올리기 3초 : 말풍선 속 원본, 대사 없음 (1단계는 도깨비, 그 뒤는 주인공)
      ├─ 쪽지 쓰기 60초 : 주인공의 글이 적힌 쪽지에 그림 (다 그렸어! 또는 향이 다 타면 자동 제출)
      ├─ 건네기 : 쪽지와 간식을 건넴 (간식 -1) → 도깨비가 먹고 쪽지를 읽음
      ├─ 판정 대기
      │  └─ 판정 오류 → 간식을 돌려주고 그리던 그림으로
      └─ 결과 : 도깨비가 물건을 가져옴 → 주인공 반응
         ├─ 전부 맞음 → 진짜 물건 → 채점표
         ├─ 틀림 + 간식 남음 → 일부 틀리면 비슷한 물건, 전부 틀리면 완전히 다른 물건
         │  ├─ 다시 → 이전 그림에 이어 그리기 (향은 다시 60초)
         │  └─ 포기 → 채점표
         └─ 틀림 + 간식 없음 → 채점표
            └─ 채점표 : 번호마다 초록 동그라미 또는 빨간 빗금
               ├─ 1~4단계 → 다음 단계 (1단계를 못 찾았으면 이야기 종료까지 도깨비가 빈손)
               └─ 5단계 → 이야기 종료 : 소풍 장면
                  └─ 화면 터치 → 도깨비가 물어봄 → 대사 터치 → 시작"""

STAGE_TABLE = """<table class='st'><tr><th>단계</th><th>물건</th><th>쓸 수 있는 색</th><th>판정 기준</th><th>조합</th><th>전부 틀리면</th></tr>
<tr><td>1</td><td>방망이</td><td>검정</td><td>굵은 머리 + 돌기</td><td>2가지</td><td>대파</td></tr>
<tr><td>2</td><td>가방</td><td>검정</td><td>네모 몸체 + 위쪽 손잡이 + 앞주머니 · 주머니 안 가로 줄무늬</td><td>4가지</td><td>항아리</td></tr>
<tr><td>3</td><td>돗자리</td><td>검정 · 빨강 · 노랑 · 파랑</td><td>사각형 외곽 · 띠 네 줄 · 띠 색 순서</td><td>8가지</td><td>파전</td></tr>
<tr><td>4</td><td>김밥</td><td>+ 두 색 섞기</td><td>검은 김 테두리 · 재료 배치 · 노란 단무지 · 초록 시금치</td><td>16가지</td><td>고무신</td></tr>
<tr><td>5</td><td>친구</td><td>+ 세 색까지 섞기</td><td>양 갈래 머리 · 주머니 천 조각 · 파란 바지 · 주황 상의 · 갈색 머리</td><td>32가지</td><td>허수아비</td></tr></table>"""

rows = load_manifest()
order = []
by_section = {}
for r in rows:
    if r["section"] not in by_section:
        by_section[r["section"]] = []
        order.append(r["section"])
    by_section[r["section"]].append(r)

gallery = ""
for section in order:
    if section == "시작":      # 사진이 한 장이라 1단계와 같은 페이지에 둬요
        continue
    cards = ""
    title = section
    shots = by_section[section]
    if section == order[1]:
        shots = by_section["시작"] + shots
        title = "시작과 " + section
    for r in shots:
        line = re.sub(r"<[^>]+>", "", r["line"])
        spoken = f"<div class='line'>{esc.escape(line)}</div>" if line else ""
        cards += (f"<figure><img src='{data_url(os.path.join(PHOTOS, r['file']), 400)}'>"
                  f"<figcaption><b>{esc.escape(r['caption'])}</b>{spoken}</figcaption></figure>")
    gallery += f"<section><h2>{esc.escape(title)}</h2><div class='grid'>{cards}</div></section>"

CSS = BASE_CSS + """
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-family: Consolas, 'Malgun Gothic', monospace; font-size: 11px; line-height: 1.5; }
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 8px 12px; margin: 10px 0; }
section { break-before: page; }
.st td:nth-child(1), .st td:nth-child(2), .st td:nth-child(5), .st td:nth-child(6), .st th { white-space: nowrap; }
.grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 8px 7px; }
figure { margin: 0; break-inside: avoid; } figure img { width: 100%; border: 1px solid #ddd6cc; border-radius: 3px; display: block; }
figcaption { font-size: 8.6px; line-height: 1.3; margin-top: 3px; } figcaption b { display: block; }
.line { color: #6d645c; margin-top: 1px; }
"""

html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>게임 플로우</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 게임 플로우</h1><p class='sub'>{date.today()} 기준 · <code>feature/game-feel</code> 브랜치 · 사진 {len(rows)}장</p>
<div class='note'><b>사진을 찍을 때 AI 판정을 보내지 않았어요.</b> 판정 결과를 미리 정해 넣고 손그림을 제출했어요. 도깨비 대사는 상황마다 무작위로 나오므로 사진은 그중 하나예요. 모든 대사는 <code>13_대사_트리.md</code>에 있어요.</div>
<h2>흐름</h2><pre>{FLOW}</pre>
<h2>단계마다 다른 것</h2>{STAGE_TABLE}
<div class='note'><b>사진을 만든 방법:</b> 경우마다 사진을 남기려고 같은 단계를 이어서 진행했어요. 그래서 간식 수나 그림이 실제 한 번의 플레이와 다를 수 있어요. 움직임은 사진으로 다 보이지 않으니 게임에서 확인해 주세요.</div>
{gallery}
</body></html>"""
print("1_게임_플로우.pdf", write_pdf(html, os.path.join(DOCS, "1_게임_플로우.pdf")))

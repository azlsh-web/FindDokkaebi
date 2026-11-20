# 게임 플로우 (paths.FLOW) : 흐름도, 챕터별 물건 표, 화면 사진 전체를 글 문서 하나로
#   python Tools/docs/make_flow_doc.py
#   먼저 Unity 메뉴 "게임 플로우 사진 찍기"를 실행하세요 (AI 를 부르지 않아요). 사진을 줄여서 문서 폴더(paths.PHOTOS)로 옮기고 문서를 만들어요.
#   2026-11-20 : 예전 PDF(사진을 다시 담아 11MB)와 사진 모음 페이지를 이 문서 하나로 합쳤어요.
#   물건 표는 Assets/Data 의 챕터 · 물건 데이터에서 읽어서, 데이터가 바뀌면 같이 바뀌어요.
import csv
import glob
import html as esc
import json
import os
import re
import shutil
import sys
from datetime import date

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from flow_shots import PHOTOS, ROOT, load_manifest
import paths

sys.stdout.reconfigure(encoding="utf-8")
SRC = os.path.join(ROOT, "Temp", "FlowShots")
DATA = os.path.join(ROOT, "Assets", "Data")

# 1) 찍은 사진을 문서 폴더로 (Temp 는 Unity 를 닫으면 지워져요)
if (os.path.exists(os.path.join(SRC, "manifest.tsv")) and os.path.exists(os.path.join(SRC, "done.txt"))
        and (not os.path.exists(os.path.join(PHOTOS, "manifest.tsv"))
             or os.path.getmtime(os.path.join(SRC, "done.txt")) > os.path.getmtime(os.path.join(PHOTOS, "manifest.tsv")))):
    done = open(os.path.join(SRC, "done.txt"), encoding="utf-8").read()
    print(done.split("\n")[0])
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


# 2) 챕터 · 물건 데이터 읽기 (Unity 데이터 파일에서 필요한 칸만)
QUOTED = r'"(?:[^"\\]|\\.)*"'     # 큰따옴표 값 (여러 줄일 수 있어요)


def unquote_yaml(v):
    """YAML 큰따옴표 값 → 글자. 줄바꿈 + 들여쓰기는 빈칸 하나, \\uXXXX · \\" 같은 표시는 풀어요"""
    v = re.sub(r"\\\n\s*", "", v[1:-1])          # 줄 끝 \ 는 이어 붙임
    v = re.sub(r"\s*\n\s*", " ", v)
    v = v.replace("\\ ", " ")
    v = re.sub(r"\\x([0-9A-Fa-f]{2})", r"\\u00\1", v)   # YAML 의 \xB7 (가운뎃점) 은 JSON 에 없어서 \u00B7 로
    return json.loads('"' + v + '"')


def scalar(raw):
    return unquote_yaml(raw) if raw.startswith('"') else raw.strip()


def text_field(src, key):
    m = re.search(rf"^[ ]*{key}: ({QUOTED}|[^\n]*)", src, re.M)
    return scalar(m.group(1)) if m else ""


def guid_refs(src, key):
    m = re.search(rf"^  {key}:\n((?:  - .*\n)*)", src, re.M)
    return re.findall(r"guid: (\w+)", m.group(1)) if m else []


assets = {}
for path in glob.glob(os.path.join(DATA, "*.asset")):
    guid = re.search(r"guid: (\w+)", open(path + ".meta", encoding="utf-8").read()).group(1)
    assets[guid] = open(path, encoding="utf-8").read()


def item_row(src):
    names = [scalar(n) for n in re.findall(rf"^    name: ({QUOTED}|[^\n]*)", src, re.M)]
    if text_field(src, "shapeOnly") == "1":
        names = names[:1]
    wrong = len(set(guid_refs(src, "wrongSprites")))   # 4챕터는 같은 그림 1장을 세 칸에 넣어 둬요
    return text_field(src, "targetName"), " · ".join(names), wrong


chapters = sorted((s for s in assets.values() if "ChapterData" in s), key=lambda s: int(text_field(s, "number")))
PEN = {"0": "검정 펜", "3": "빨강 · 노랑 · 파랑 섞기"}
table = ["| 챕터 | 물건 | 판정 기준 | 오답 그림 |", "|---|---|---|---|"]
for ch in chapters:
    n, title = text_field(ch, "number"), text_field(ch, "title")
    extra = []
    if text_field(ch, "hasMagnifier") == "0":
        extra.append("돋보기 없음")
    extra.append(f"사탕 {text_field(ch, 'candies')}개")
    head = f"**{n} {title}**<br>{' · '.join(extra)}"
    pairs = list(zip(guid_refs(ch, "items"), guid_refs(ch, "counterItems") or [None] * 99))
    for i, (g, cg) in enumerate(pairs):
        name, crit, wrong = item_row(assets[g])
        if cg:
            cname, ccrit, cwrong = item_row(assets[cg])
            name, crit, wrong = f"{i + 1}단계 요청 {name} / 맞설 {cname}", f"{crit} / {ccrit}", f"{wrong} / {cwrong}"
        table.append(f"| {head if i == 0 else ''} | {name} | {crit} | {wrong}장 |")
TABLE = "\n".join(table)

FLOW = """로비 씬
└─ 화면 터치 → 챕터 선택 : 챕터 사진 5장을 끈으로 엮음. 앞 챕터를 깨야 열리고, 못 연 챕터는 흑백 · 누르면 흔들림
   └─ 사진을 누름 → 큰 사진 + 챕터 이름 → 누르면 그 챕터 시작 (게임 씬)

1~4챕터 : 방에서 물건을 차례로 찾아요
├─ 2~4챕터만 : 방 시작 장면 (첫 두 줄은 주인공 혼자. 2챕터는 창밖 도깨비불)
├─ 물건마다
│  ├─ 시작 대화 : 주인공이 생김새를 말하고, 도깨비가 알아듣는 말투로 받음
│  ├─ 떠올리기 3초 : 말풍선 속 원본
│  ├─ 쪽지에 그리기 60초 : 촛대 게이지가 줄어듦 (다 그렸어! 또는 시간이 다 되면 제출)
│  │  ├─ 챕터 첫 물건 첫 그리기 안내 : 1챕터 펜 · 2챕터 돋보기 시범 · 3챕터 색 (안내하는 동안 시간 멈춤)
│  │  └─ 돋보기 (2챕터부터) : 챕터마다 1번 + 물건마다 두 번째로 틀리면 그 물건에서만 무료 1번
│  ├─ 건네기 : 쪽지와 사탕 (사탕 -1). 4챕터는 사탕이 없어서 쪽지만
│  ├─ 판정 대기 (판정 오류면 사탕을 돌려주고 그리던 그림으로)
│  └─ 결과
│     ├─ 맞음 → 진짜 물건 → 채점표
│     ├─ 틀림 + 사탕 남음 → n번째로 틀리면 n번째 엉뚱한 물건 → 다시 (이어 그리기) · 포기
│     └─ 틀림 + 사탕 없음 (4챕터는 늘) → 채점표
├─ 1챕터만 : 이야기 종료 (소풍 장면)
├─ 2~4챕터만 : 방 끝 장면 (찾은 물건 3개 + 도깨비 인사. 4챕터는 도깨비가 떠나고 창 안에 빨간 눈)
└─ 챕터를 깸 → 다음 챕터가 열림 → 챕터 선택

5챕터 도깨비의 저주 : 숲, 3단계, 위 줄에 도깨비 하트 3개
└─ 단계마다 (사탕 3개)
   ├─ 고르기 : 그림 2개 (요청 물건 · 맞설 물건, 좌우가 섞임)
   ├─ 요청 물건 → 성공 : 도깨비 힘 자랑 / 실패 : 점프스케어 + 친구 영혼, 같은 물건을 두 번 틀리면 게임오버
   └─ 맞설 물건 (그리는 동안 방해 연출) → 성공 : 하트 -1, 0개가 되면 해피 엔딩
   3단계가 끝나도 하트가 남으면 → 배드 엔딩
   (5챕터 흐름은 BACKLOG 의 "5챕터 흐름 재정비"에서 다시 정하는 중이에요)"""

# 3) 사진 : 구역마다 제목 + 사진 칸
rows = load_manifest()
gallery, current = [], None
for r in rows:
    if r["section"] != current:
        if current is not None:
            gallery.append("</div></div>\n")
        current = r["section"]
        gallery.append(f"### {current}\n\n<div class=\"shots\"><div class=\"row\">")
    line = re.sub(r"<[^>]+>", "", r["line"])
    spoken = f"<span class=\"l\">{esc.escape(line)}</span>" if line else ""
    src = f"플로우_사진/{r['file']}"
    gallery.append(f"<figure><a href=\"{src}\"><img loading=\"lazy\" src=\"{src}\" alt=\"{esc.escape(r['caption'])}\"></a>"
                   f"<figcaption>{esc.escape(r['caption'])}{spoken}</figcaption></figure>")
gallery.append("</div></div>\n")

doc = f"""# 게임 플로우

> **상태** 최신 · **담당** 개발 · **마지막 갱신** {date.today()}

게임이 어떻게 흘러가는지와, 자동 플레이로 찍은 화면 {len(rows)}장이에요. 코드에서 다시 만들어요 (`Tools/docs/make_flow_doc.py`). 손으로 고치지 마세요.

- 사진을 찍을 때 **AI 판정을 보내지 않았어요.** 판정 결과를 미리 정해 넣고 손그림을 제출했어요.
- 도깨비 대사는 상황마다 무작위라 사진은 그중 하나예요. 모든 대사는 [대사 트리](대사_트리.md)에 있어요.
- 경우마다 사진을 남기려고 같은 물건을 이어서 진행해서, 사탕 수나 그림이 실제 한 번의 플레이와 다를 수 있어요.

## 흐름

```text
{FLOW}
```

## 챕터별 물건

{TABLE}

## 화면 사진

사진을 누르면 크게 보여요.

{chr(10).join(gallery)}"""

out = paths.doc(paths.FLOW)
open(out, "w", encoding="utf-8").write(doc)
print(paths.FLOW, f"사진 {len(rows)}장, 챕터 {len(chapters)}개")

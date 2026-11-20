# 대사 트리 (paths.DIALOGUE_TREE) : 현재 챕터의 대사를 흐름대로 정리해요.
#   python Tools/docs/make_dialogue_tree.py         글 문서만 (2026-11-20 부터 기본, PDF 는 같은 내용이라 뺐어요)
#   python Tools/docs/make_dialogue_tree.py --pdf   PDF 도 Temp 에 따로 만들어요
#   python Tools/docs/make_dialogue_tree.py          PDF 도 함께 만들어요
# 대사와 번호는 수정표와 같은 read_lines() 에서 읽어요.
import html as esc
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dialogue_lines import ROOT, STAGES, read_lines
import paths

sys.stdout.reconfigure(encoding="utf-8")
rows = read_lines()
by_id = {row["id"]: row for row in rows}


def node(label, *children):
    return label, [c if isinstance(c, tuple) else (c, []) for c in children]


def render(nodes, prefix=""):
    out = []
    for i, (label, kids) in enumerate(nodes):
        last = i == len(nodes) - 1
        out.append(prefix + ("└─ " if last else "├─ ") + label)
        out += render(kids, prefix + ("   " if last else "│  "))
    return out


N = node


def say(num):
    row = by_id[num]
    return f"{num} · {row['who']}: {row['text']}"


def matching(prefix):
    return [say(r["id"]) for r in rows if r["id"].startswith(prefix)]


def stage_tree(i, target):
    code = f"S{i + 1}"
    boy_ok = "주인공-맞음-방망이" if i == 0 else "주인공-맞음-친구" if i == 4 else "주인공-맞음-1"
    boy_wrong = "주인공-틀림-방망이" if i == 0 else "주인공-틀림-친구" if i == 4 else "주인공-틀림-1"
    start = matching(f"{code}-시작-")
    if i == 1:
        start = [N("1챕터에서 방망이를 찾았는지에 따라 맨 앞에 하나",
                   say("S2-시작-찾음"), say("S2-시작-못찾음"))] + [
                       say(r["id"]) for r in rows
                       if r["id"].startswith(f"{code}-시작-") and r["id"].rsplit("-", 1)[-1].isdigit()]
    return N(f"1챕터 · {target}",
             N("시작 대화 (순서대로)", *start),
             "떠올리기 3초 — 대사 없음" + (" (도깨비)" if i == 0 else " (주인공)"),
             N("쪽지 글", say(f"{code}-쪽지글")),
             N("쪽지와 간식을 건넴 (순서대로)", *matching(f"{code}-건네기-")),
             N("결과",
               N("형태 기준 통과", N("도깨비 (아래 2개 중 하나)", *matching(f"{code}-맞음-")), say(boy_ok)),
               N("형태 기준 실패 — 틀린 횟수에 맞는 오답 그림",
                 N("도깨비 (아래 2개 중 하나)", *matching(f"{code}-틀림-")), say(boy_wrong))))


def curse_step(i, requested, counter):
    return N(f"{i}단계 · {requested} / {counter}",
             N("시작 대화 (순서대로)", *matching(f"C5-단계{i}-")),
             f"글 없는 그림 두 장 중 하나를 고름 (좌우는 무작위): {requested} / {counter}",
             "떠올리기 → 그리기 → 쪽지와 간식 건네기 (건네기 앞 두 대사는 1챕터와 공통)",
             N("결과",
               N("맞설 물건 성공 — 도깨비가 아파하고 하트 하나를 잃음",
                 say("C5-맞섬-성공"), say("C5-주인공-맞섬")),
               N("요청 물건 성공 — 도깨비가 얻은 힘을 자랑함",
                 say(f"C5-자랑-{i}"), say("C5-주인공-자랑")),
               N("맞설 물건 실패", say("C5-맞섬-실패"), say("C5-주인공-실패")),
               N("같은 요청 물건 첫 실패 — 무서운 얼굴이 나타나고 친구 영혼을 빼앗김",
                 say("C5-요청-실패"), say("C5-주인공-실패")),
               N("같은 요청 물건 두 번째 실패 — 바로 게임오버",
                 say("C5-게임오버"), "도깨비 뒤에 의식을 잃은 사람들의 실루엣")),
             "사탕을 다 쓰고 틀렸다면 주인공 반응은 공통 간식 없음 대사로 바뀜 (게임오버는 제외)",
             "다시 또는 포기 → 채점표 → 다음 단계나 엔딩")


tree = [
    N("로비",
      "시작 화면을 누르면 챕터 선택",
      "끈에 매달린 사진 5장: 1챕터 소풍 준비, 5챕터 도깨비의 저주가 열림",
      "2·3·4챕터는 자물쇠",
      "사진을 누르면 큰 사진과 챕터 이름 → 큰 사진을 누르면 게임 시작"),
    N("1챕터 · 소풍 준비",
      N("첫 물건 첫 그리기 · 돋보기 시범 (순서대로)",
        say("도깨비-돋보기-1"), "도깨비불이 돋보기를 강조함 → 돋보기를 누르면",
        say("도깨비-돋보기-2"), "둘러본 뒤", say("도깨비-돋보기-3")),
      *[stage_tree(i, target) for i, target in enumerate(STAGES)],
      N("마무리", "글자 없는 소풍 장면", say("도깨비-마무리"), "다음 터치 → 챕터 선택")),
    N("5챕터 · 도깨비의 저주",
      "주인공의 친구가 화면에 함께 서 있음",
      N("시작 대화 (순서대로)", *[
          N(r["scene"], say(r["id"])) for r in rows if r["id"].startswith("C5-시작-")]),
      curse_step(1, "짚신", "부적"),
      curse_step(2, "도깨비 부채", "닭피 그릇"),
      curse_step(3, "도깨비 감투", "팥주머니"),
      N("엔딩",
        N("하트 3개가 모두 사라짐 — 도깨비가 정화되어 도망감",
          N("1챕터에서 방망이를 찾음 — 마법 방망이를 남김", say("C5-해피-방망이")),
          N("1챕터에서 방망이를 못 찾음 — 남기는 방망이 없음", say("C5-해피-방망이없음"))),
        N("3단계 뒤 하트가 남음 — 감투를 쓰고 사라지는 불안한 끝", say("C5-배드")),
        N("같은 요청 물건을 두 번 틀림", say("C5-게임오버"),
          "도깨비 뒤에 의식을 잃은 사람들의 실루엣"),
        "엔딩 대사 뒤 터치 → 챕터 선택")),
    N("챕터 공통 대사",
      N("사탕을 다 쓰고 틀렸을 때 (게임오버는 제외)", say("주인공-간식없음")),
      N("판정 서버 오류 — 간식을 돌려줌", say("도깨비-오류")),
      N("한 물건에서 두 번째 실패 — 사탕이 남고 게임오버가 아닐 때",
        say("도깨비-무료돋보기"), "무료 돋보기는 그 물건에서만 쓸 수 있음")),
]

screen = [
    N("대사 상자 바깥의 글자와 그림",
      N("떠올리기", "숫자: 3, 2, 1"),
      N("그리기", "남은 초 숫자 (향 옆)", "버튼: 다 그렸어!", "모든 물건에서 같은 팔레트 사용"),
      N("판정 대기", "도깨비 머리 위: ?"),
      N("결과 버튼", "다시", "포기"),
      N("5챕터", "사탕과 돋보기 사이에 도깨비 하트 3개", "고르기 화면에는 글 없이 물건 그림 두 장"),
      N("설정", "배경음", "효과음", "게임 씬: 나가기 → 취소 / 나가기 → 챕터 선택", "로비: 나가기 없음")),
]

text = f"""# 대사 트리

> **상태** 자동 · **담당** 코드에서 만들어요 (`GameScript.cs`) · 게임을 고칠 때마다 다시 만들어서 늘 최신이에요

게임에 나오는 대사를 챕터와 분기별로 정리했어요. [대사 수정표](../3_전달/대사_수정표.xlsx)와 같은 `GameScript.cs`의 대사를 읽어요. 번호와 대사는 수정표와 같아요.

이 문서는 코드에서 자동으로 만들어요. 이 문서만 다시 만들려면:

```bash
python Tools/docs/make_dialogue_tree.py
```

## 읽는 법

- **순서대로:** 위에서 아래로 차례로 나와요.
- **N개 중 하나:** 그 상황에서 무작위로 하나를 골라요.
- **괄호 안 대사:** 주인공의 속마음이에요.
- **번호:** S1~S5는 1챕터의 물건, C5는 5챕터예요. 같은 번호는 같은 대사를 다시 사용해요.
- **표정:** 주인공은 놀람·기쁨·울음·보통, 도깨비는 도깨비 표정으로 표시해요.

## 대사

```text
찾아라! 도깨비
{chr(10).join(render(tree))}
```

## 화면 글자와 연출

```text
찾아라! 도깨비
{chr(10).join(render(screen))}
```

## 참고

- 1챕터의 다섯 물건은 형태 기준 하나로 통과 또는 실패를 결정해요.
- 5챕터는 요청 물건과 맞설 물건을 고르는 세 단계예요. 판정 기준은 물건마다 세 개예요.
- 5챕터 시작 대사 C5-시작-3에서 간식 이야기를 꺼내며 숲이 어두워져요. 코드의 `CurseDarkFrom = 2`는 0부터 세는 위치예요.
- 5챕터의 쪽지 글과 건네기 마지막 대사는 `ItemNote(name)`, `ItemRead(name)`으로 고른 물건 이름에 맞춰 만들어요.
- 돋보기 기본 기회는 챕터마다 한 번이고, 시범은 1챕터 첫 물건에서만 나와요.
- 색 섞기 시범, 소풍 뒤 사탕 통, 쿠키 영상은 없어요.
"""
out = paths.doc(paths.DIALOGUE_TREE)
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write(text)
print(paths.DIALOGUE_TREE, len(text))

if "--pdf" not in sys.argv:
    print("PDF 는 만들지 않아요 (사이트에서 글 문서로 봐요). 필요하면 --pdf")
else:
    from doc_pdf import BASE_CSS, EDGE, write_pdf

    if not os.path.isfile(EDGE):
        print("PDF 생략: Edge 실행 파일이 없어요.")
    else:
        def branch(label, kids):
            return f"<section><h2>{esc.escape(label)}</h2><pre>{esc.escape(chr(10).join(render(kids)))}</pre></section>"

        css = BASE_CSS + """
section { break-inside: auto; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px 12px; white-space: pre-wrap;
      font-family: Consolas, 'Malgun Gothic', monospace; font-size: 11px; line-height: 1.55; }
"""
        html = f"""<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>대사 트리</title>
<style>{css}</style></head><body><h1>찾아라! 도깨비 대사 트리</h1>
<p class="sub">1챕터 소풍 준비 · 5챕터 도깨비의 저주. 괄호 안은 주인공 속마음이에요.</p>
{"".join(branch(label, kids) for label, kids in tree)}
{"".join(branch(label, kids) for label, kids in screen)}
</body></html>"""
        print(paths.DIALOGUE_TREE[:-3] + ".pdf", write_pdf(html, os.path.join(ROOT, "Temp", "대사_트리.pdf")))

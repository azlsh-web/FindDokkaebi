# Docs/기획/13_대사_트리.md 와 .pdf : 게임의 모든 대사와 화면 글자를 흐름 순서대로 트리로 정리해요.
#   python Tools/docs/make_dialogue_tree.py
#   대사는 Assets/Scripts/GameScript.cs 에서, 판정 기준은 Assets/Data 에서 바로 읽어요. 손으로 옮겨 적지 않아서 게임과 어긋나지 않아요.
import html as esc
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from doc_pdf import BASE_CSS, write_pdf

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
src = open(os.path.join(ROOT, "Assets", "Scripts", "GameScript.cs"), encoding="utf-8").read()
FACE = {"BoySurprised": "주인공(놀람)", "BoyHappy": "주인공(기쁨)", "Dokkaebi": "도깨비"}


def block(name):
    m = re.search(r"\b" + name + r"\s*=\s*\{(.*?)\n    \};", src, re.S)
    assert m, name
    return m.group(1)


def strings(text):
    return re.findall(r'"((?:[^"\\]|\\.)*)"', text)


def const(name):
    m = re.search(r"const string " + name + r' = "(.*?)";', src)
    assert m, name
    return m.group(1)


def groups(name):
    """new[] { ... } 묶음을 단계 순서대로"""
    return re.findall(r"new\[\]\s*\{(.*?)\}", block(name), re.S)


intro = [re.findall(r'new Line\(Face\.(\w+), "(.*?)"\)', g) for g in re.findall(r"new\[\]\s*\{(.*?)\n        \}", block("Intro"), re.S)]
card = strings(block("Card"))
success = [strings(g) for g in groups("DokkaebiSuccess")]
partial = [strings(g) for g in groups("DokkaebiPartial")]
allwrong = [strings(g) for g in groups("DokkaebiAllWrong")]
boy_ok = strings(re.search(r"BoySuccess = \{(.*?)\};", src).group(1))
boy_part = strings(re.search(r"BoyPartial = \{(.*?)\};", src).group(1))
boy_wrong = strings(re.search(r"BoyAllWrong = \{(.*?)\};", src).group(1))
assert len(intro) == len(card) == len(success) == len(partial) == len(allwrong) == 5

DATA = ["Stage1_Bat", "Stage2_Bag", "Stage3_Mat", "Stage4_Gimbap", "Stage5_Friend"]
WRONG_ITEM = {0: "대파", 1: "항아리", 2: "파전", 3: "고무신", 4: "허수아비"}


def stage_data(name):
    text = open(os.path.join(ROOT, "Assets", "Data", name + ".asset"), encoding="utf-8").read()
    decode = lambda raw: json.loads(raw) if raw.startswith('"') else raw
    target = decode(re.search(r"targetName: (.*)", text).group(1).strip())
    feats = [decode(v.strip()) for v in re.findall(r"^    name: (.*)$", text, re.M)]
    return target, feats


class Tree:
    def __init__(self):
        self.root = []

    @staticmethod
    def node(label, *children):
        return (label, [c if isinstance(c, tuple) else (c, []) for c in children])


def render(nodes, prefix=""):
    out = []
    for i, (label, kids) in enumerate(nodes):
        last = i == len(nodes) - 1
        out.append(prefix + ("└─ " if last else "├─ ") + label)
        out += render(kids, prefix + ("   " if last else "│  "))
    return out


N = Tree.node


def random_list(who, lines):
    """무작위로 하나 고르는 대사"""
    if len(lines) == 1:
        return f"{who}: {lines[0]}"
    return N(f"{who} (아래 {len(lines)}개 중 하나)", *lines)


def stage_tree(i):
    target, feats = stage_data(DATA[i])
    rows = [f"{k + 1}. {f}" for k, f in enumerate(feats)]
    wrong = []
    if i > 0:
        wrong.append(N("일부 틀림", random_list("도깨비", partial[i]), random_list("주인공(놀람)", boy_part)))
    wrong.append(N(f"전부 틀림 (물건: {WRONG_ITEM[i]})" if i else f"틀림 (기준이 하나라 곧 전부 틀림, 물건: {WRONG_ITEM[i]})",
                   random_list("도깨비", allwrong[i]), random_list("주인공(놀람)", boy_wrong)))
    return N(f"{i + 1}단계 {target}",
             N("시작 대화 (순서대로)", *[f"{FACE[f]}: {t}" for f, t in intro[i]]),
             N(f"도깨비의 쪽지 — 제목 `{i + 1}단계 {target}`", N("판정 기준", *rows), f"도깨비: {card[i]}"),
             N("그리기 — 쪽지 제목", f"기억나는 {target}{'을' if (ord(target[-1]) - 0xAC00) % 28 else '를'} 그려 줘"),
             N("결과",
               N("전부 맞음 (물건: 진짜)", random_list("도깨비", success[i]), random_list("주인공(기쁨)", boy_ok)),
               *wrong))


tree = [
    N("시작 화면", "버튼: 게임 시작하기"),
    *[stage_tree(i) for i in range(5)],
    N("어느 단계든 나오는 대사",
      N("간식을 다 쓰고 틀렸을 때", f"주인공(놀람): {const('BoyOutOfSnacks')}", "이때는 위 주인공 결과 대사 대신 이 대사가 나와요."),
      N("판정 서버 오류 (간식을 돌려줘요)", f"도깨비: {const('JudgeError')}")),
    N("이야기 종료", "글자 없이 소풍 장면", f"화면을 누르면 도깨비: {const('EndingAsk')}"),
]

screen = [
    N("대사 상자 바깥의 글자",
      N("도깨비의 쪽지", "머리글: 도깨비의 쪽지"),
      N("관찰", "가운데 숫자: 3, 2, 1"),
      N("그리기", "남은 초 숫자 (향 옆)", "버튼: 다 그렸어!"),
      N("색 섞기 (4·5단계)", "4단계 안내: 재료 색을 2가지 고르세요", "5단계 안내: 재료 색을 2~3가지 고르세요",
        "조합 표시 예: 노랑 + 파랑 = 초록", "버튼: 섞어서 칠하기"),
      N("판정 대기", "도깨비 머리 위: ?"),
      N("결과 버튼", "다시", "포기"),
      N("일시정지", "잠깐 쉬는 중", "멈춘 동안은 향이 타지 않아요", "버튼: 계속하기")),
]

text = f"""# 대사 트리

게임에 나오는 모든 대사를 흐름 순서대로 정리했어요. 기획 수정은 [대사 수정표](../전달/14_대사_수정표.xlsx)로 받아요. 코드에서 고칠 때는 `Assets/Scripts/GameScript.cs`만 고치면 돼요.

이 문서는 코드에서 자동으로 만들어요. 코드를 고친 뒤 아래 명령을 다시 실행하세요.

```bash
python Tools/docs/make_dialogue_tree.py
```

## 읽는 법

- **순서대로:** 위에서 아래로 차례로 나와요.
- **N개 중 하나:** 그 상황이 올 때마다 무작위로 하나를 골라요.
- **얼굴:** 말하는 사람의 얼굴이 대사 상자에 나와요. 주인공은 놀람, 기쁨 두 가지이고, 도깨비는 한 가지예요.
- **색 조합:** 색을 어떻게 섞는지는 대사에서 알려 주지 않아요.

## 대사

```text
찾아라! 도깨비
{chr(10).join(render(tree))}
```

## 화면 글자

```text
찾아라! 도깨비
{chr(10).join(render(screen))}
```

## 참고

- **1단계의 일부 틀림:** 기준이 하나라 나오지 않아요. `GameScript.cs`에 대사가 있지만 쓰이지 않아서 트리에서 뺐어요.
- **판정 기준 글:** 대사가 아니라 단계 데이터에 있어요. `Assets/Editor/MvpSceneBuilder.cs`의 `CreateStageDataFiles`에서 고쳐요.
- **기획서 줄거리 글:** 단계 데이터에 남아 있지만 지금 게임 화면에는 나오지 않아요.
"""
out = os.path.join(ROOT, "Docs", "기획", "13_대사_트리.md")
os.makedirs(os.path.dirname(out), exist_ok=True)
open(out, "w", encoding="utf-8", newline="\n").write(text)
print("13_대사_트리.md", len(text))


# PDF : 큰 가지마다 제목을 두고 한 묶음으로 보여 줘요
def branch(label, kids):
    return f"<section><h2>{esc.escape(label)}</h2><pre>{esc.escape(chr(10).join(render(kids)))}</pre></section>"


CSS = BASE_CSS + """
section { break-inside: avoid; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px 12px; margin: 0; white-space: pre-wrap;
      font-family: Consolas, 'Malgun Gothic', monospace; font-size: 11px; line-height: 1.55; }
ul { margin: 6px 0 12px; padding-left: 18px; }
"""
html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>대사 트리</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 대사 트리</h1><p class='sub'>게임에 나오는 모든 대사를 흐름 순서대로 정리했어요. 코드에서 자동으로 만든 문서예요.</p>
<ul><li><b>순서대로:</b> 위에서 아래로 차례로 나와요.</li>
<li><b>N개 중 하나:</b> 그 상황이 올 때마다 무작위로 하나를 골라요.</li>
<li><b>얼굴:</b> 말하는 사람의 얼굴이 대사 상자에 나와요. 주인공은 놀람, 기쁨 두 가지이고, 도깨비는 한 가지예요.</li>
<li><b>색 조합:</b> 색을 어떻게 섞는지는 대사에서 알려 주지 않아요.</li></ul>
{"".join(branch(label, kids) for label, kids in tree)}
{"".join(branch(label, kids) for label, kids in screen)}
</body></html>"""
print("13_대사_트리.pdf", write_pdf(html, os.path.splitext(out)[0] + ".pdf"))

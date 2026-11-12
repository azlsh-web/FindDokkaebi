# GameScript.cs 의 대사를 번호와 함께 읽어요. 대사 수정표와 반영 도구가 같이 써요.
#   번호는 코드 위치에서 만들어지므로, 대사 글자를 바꿔도 번호는 그대로예요.
import os
import re

from PIL import ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Scripts", "GameScript.cs")
STAGES = ["방망이", "가방", "돗자리", "김밥", "친구"]
FACE = {"BoySurprised": "주인공 (놀람)", "BoyHappy": "주인공 (기쁨)", "BoySad": "주인공 (울음)", "Dokkaebi": "도깨비"}

# 대사 상자 크기 : MvpSceneBuilder.NewDialogueBox (글자 칸 폭 663, 글자 크기 48, 세 줄까지 들어가요)
BOX_WIDTH, FONT_SIZE, MAX_LINES = 663, 48, 3
FONT = ImageFont.truetype(os.path.join(ROOT, "Assets", "Fonts", "NotoSansKR-Regular.ttf"), FONT_SIZE)

_STRING = re.compile(r'"((?:[^"\\]|\\.)*)"')


def _closing(src, start):
    """start 의 { 와 짝이 맞는 } 위치"""
    depth, i = 0, start
    while i < len(src):
        ch = src[i]
        if ch == '"':
            i = _STRING.match(src, i).end()
            continue
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    raise ValueError("괄호 짝이 맞지 않아요")


def _block(src, name):
    m = re.search(r"\b" + name + r"\s*=\s*\{", src)
    assert m, name
    return m.end() - 1, _closing(src, m.end() - 1)


def _groups(src, name):
    """new[] { ... } 묶음의 범위를 단계 순서대로"""
    start, end = _block(src, name)
    out = []
    for m in re.finditer(r"new\[\]\s*\{", src[start:end]):
        a = start + m.end() - 1
        out.append((a, _closing(src, a)))
    return out


def _strings(src, start, end):
    return [(m.group(1), m.start(1), m.end(1)) for m in _STRING.finditer(src, start, end)]


def unescape(text):
    return text.replace('\\"', '"').replace("\\\\", "\\")


def escape(text):
    return text.replace("\\", "\\\\").replace('"', '\\"')


def read_lines(path=SOURCE):
    """게임 흐름 순서대로 (번호, 단계, 상황, 말하는 사람, 대사, 시작 위치, 끝 위치)"""
    src = open(path, encoding="utf-8", newline="").read()
    rows = []

    def add(num, stage, scene, who, text, a, b):
        rows.append({"id": num, "stage": stage, "scene": scene, "who": who, "text": unescape(text), "start": a, "end": b})

    intro, success, partial, wrong = (_groups(src, n) for n in ("Intro", "DokkaebiSuccess", "DokkaebiPartial", "DokkaebiAllWrong"))
    note = _strings(src, *_block(src, "Note"))
    read = _strings(src, *_block(src, "GiveRead"))
    assert len(intro) == len(note) == len(read) == len(success) == len(partial) == len(wrong) == len(STAGES)

    def const(name):
        m = re.search(r"const string " + name + r' = "((?:[^"\\]|\\.)*)"', src)
        assert m, name
        return m.group(1), m.start(1), m.end(1)

    def lead(name):
        m = re.search(name + r' = new Line\(Face\.Dokkaebi, "((?:[^"\\]|\\.)*)"\)', src)
        assert m, name
        return m.group(1), m.start(1), m.end(1)

    for i, stage in enumerate(STAGES):
        label, code = f"{i + 1}단계 {stage}", f"S{i + 1}"
        a, b = intro[i]
        if i == 1:
            add(f"{code}-시작-찾음", label, "방망이를 찾았을 때 (시작 대화 맨 앞)", "도깨비", *lead("BatFound"))
            add(f"{code}-시작-못찾음", label, "방망이를 못 찾았을 때 (시작 대화 맨 앞)", "도깨비", *lead("BatLost"))
        for k, m in enumerate(re.finditer(r'new Line\(Face\.(\w+),\s*"((?:[^"\\]|\\.)*)"\)', src[a:b])):
            add(f"{code}-시작-{k + 1}", label, "시작 대화 (순서대로)", FACE[m.group(1)], m.group(2), a + m.start(2), a + m.end(2))
        add(f"{code}-쪽지글", label, "쪽지에 적힌 글", "(쪽지 글)", *note[i])
        add(f"{code}-건네기-1", label, "쪽지와 간식을 건넬 때 (순서대로)", FACE["BoyHappy"], *const("GiveBoy"))
        add(f"{code}-건네기-2", label, "쪽지와 간식을 건넬 때 (순서대로)", "도깨비", *const("GiveEat"))
        add(f"{code}-건네기-3", label, "쪽지와 간식을 건넬 때 (순서대로)", "도깨비", *read[i])
        for key, title, ranges in (("맞음", "전부 맞음 (무작위)", success), ("일부", "일부 틀림 (무작위)", partial), ("틀림", "전부 틀림 (무작위)", wrong)):
            if key == "일부" and i == 0:
                continue    # 1단계는 기준이 하나라 일부 틀림이 없어요
            for k, s in enumerate(_strings(src, *ranges[i])):
                add(f"{code}-{key}-{k + 1}", label, title, "도깨비", *s)

    for num, stage, title, who, name in (("주인공-맞음-방망이", "1단계 방망이", "전부 맞음", FACE["BoyHappy"], "BoyBatSuccess"),
                                         ("주인공-맞음-1", "2~4단계", "전부 맞음", FACE["BoyHappy"], "BoySuccess"),
                                         ("주인공-일부-1", "2~4단계", "일부 틀림", FACE["BoySad"], "BoyWrong"),
                                         ("주인공-틀림-1", "2~4단계", "전부 틀림", FACE["BoySad"], "BoyWrong"),
                                         ("주인공-맞음-친구", "5단계 친구", "전부 맞음", FACE["BoyHappy"], "BoyFriendSuccess"),
                                         ("주인공-틀림-친구", "5단계 친구", "일부 틀림 · 전부 틀림", FACE["BoySad"], "BoyFriendWrong"),
                                         ("주인공-틀림-방망이", "1단계 방망이", "전부 틀림", FACE["BoySad"], "BoyBatWrong")):
        add(num, stage, title, who, *const(name))

    for num, stage, scene, who, const in (("주인공-간식없음", "모든 단계", "사탕을 다 쓰고 틀렸을 때", FACE["BoySad"], "BoyOutOfSnacks"),
                                          ("도깨비-오류", "모든 단계", "판정 서버 오류", "도깨비", "JudgeError"),
                                          ("도깨비-돋보기-1", "1단계 방망이", "첫 그리기 : 돋보기 시범 (넘기면 돋보기가 도깨비불로 강조돼요)", "도깨비", "MagnifierShow"),
                                          ("도깨비-돋보기-2", "1단계 방망이", "돋보기를 누른 뒤 (넘기면 둘러봐요)", "도깨비", "MagnifierHow"),
                                          ("도깨비-돋보기-3", "1단계 방망이", "시범으로 둘러본 뒤", "도깨비", "MagnifierRule"),
                                          ("도깨비-무료돋보기", "모든 단계", "한 단계에서 두 번째로 틀렸을 때 (사탕이 남았으면)", "도깨비", "MagnifierGift"),
                                          ("도깨비-마무리", "이야기 종료", "화면을 누른 뒤 (넘기면 챕터 선택)", "도깨비", "EndingBye")):
        m = re.search(r"const string " + const + r' = "((?:[^"\\]|\\.)*)"', src)
        assert m, const
        add(num, stage, scene, who, m.group(1), m.start(1), m.end(1))
    return rows


def wrapped_lines(text):
    """대사 상자에서 몇 줄이 되는지 (띄어쓰기 단위로 줄을 바꿔요). Unity 와 조금 다를 수 있는 어림값이에요."""
    lines, current = 1, ""
    for word in text.split(" "):
        trial = f"{current} {word}".strip()
        if FONT.getlength(trial) <= BOX_WIDTH:
            current = trial
        else:
            lines += 1
            current = word
    return lines


def font_chars():
    path = os.path.join(ROOT, "Assets", "Fonts", "NotoSansKR-Regular SDF.asset")
    return {chr(int(x)) for x in re.findall(r"m_Unicode: (\d+)", open(path, encoding="utf-8").read())}

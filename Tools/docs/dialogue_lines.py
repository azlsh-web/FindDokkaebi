# GameScript.cs 의 대사를 번호와 함께 읽어요. 대사 수정표와 반영 도구가 같이 써요.
#   번호는 코드 위치에서 만들어지므로, 대사 글자를 바꿔도 번호는 그대로예요.
import os
import re

from PIL import ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Scripts", "GameScript.cs")
STAGES = ["방망이", "가방", "돗자리", "김밥", "친구"]
FACE = {"BoySurprised": "주인공 (놀람)", "BoyHappy": "주인공 (기쁨)", "BoySad": "주인공 (울음)", "BoyNormal": "주인공 (보통)", "Dokkaebi": "도깨비"}

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


def _top_groups(src, start, end):
    """범위 안에서 바로 아래 단계의 new[] { ... } 묶음만 (안쪽 묶음은 건너뛰어요)"""
    out, i = [], start + 1
    for m in re.finditer(r"new\[\]\s*\{", src[start + 1:end]):
        a = start + 1 + m.end() - 1
        if a < i:
            continue
        b = _closing(src, a)
        out.append((a, b))
        i = b
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

    intro, success, wrong = (_groups(src, n) for n in ("Intro", "DokkaebiSuccess", "DokkaebiAllWrong"))
    note = _strings(src, *_block(src, "Note"))
    read = _strings(src, *_block(src, "GiveRead"))
    assert len(intro) == len(note) == len(read) == len(success) == len(wrong) == len(STAGES)

    def const(name):
        m = re.search(r"const string " + name + r' = "((?:[^"\\]|\\.)*)"', src)
        assert m, name
        return m.group(1), m.start(1), m.end(1)

    def lead(name):
        m = re.search(name + r' = new Line\(Face\.Dokkaebi, "((?:[^"\\]|\\.)*)"\)', src)
        assert m, name
        return m.group(1), m.start(1), m.end(1)

    for i, stage in enumerate(STAGES):
        label, code = f"1챕터 · {stage}", f"S{i + 1}"
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
        for key, title, ranges in (("맞음", "형태 기준 통과 (무작위)", success), ("틀림", "형태 기준 실패 (무작위)", wrong)):
            for k, s in enumerate(_strings(src, *ranges[i])):
                add(f"{code}-{key}-{k + 1}", label, title, "도깨비", *s)

    for num, stage, title, who, name in (("주인공-맞음-방망이", "1챕터 · 방망이", "형태 기준 통과", FACE["BoyHappy"], "BoyBatSuccess"),
                                         ("주인공-맞음-1", "1챕터 · 가방·돗자리·김밥", "형태 기준 통과", FACE["BoyHappy"], "BoySuccess"),
                                         ("주인공-틀림-1", "1챕터 · 가방·돗자리·김밥", "형태 기준 실패", FACE["BoySad"], "BoyWrong"),
                                         ("주인공-맞음-친구", "1챕터 · 친구", "형태 기준 통과", FACE["BoyHappy"], "BoyFriendSuccess"),
                                         ("주인공-틀림-친구", "1챕터 · 친구", "형태 기준 실패", FACE["BoySad"], "BoyFriendWrong"),
                                         ("주인공-틀림-방망이", "1챕터 · 방망이", "형태 기준 실패", FACE["BoySad"], "BoyBatWrong")):
        add(num, stage, title, who, *const(name))

    for num, stage, scene, who, name in (("주인공-간식없음", "모든 챕터", "사탕을 다 쓰고 틀렸을 때 (게임오버는 제외)", FACE["BoySad"], "BoyOutOfSnacks"),
                                         ("도깨비-오류", "모든 챕터", "판정 서버 오류", "도깨비", "JudgeError"),
                                         ("안내-펜-1", "1챕터 · 방망이", "첫 그리기 : 펜 안내 (넘기면 도깨비불이 쪽지를 가리켜요)", "도깨비", "PenDraw"),
                                         ("안내-펜-2", "1챕터 · 방망이", "쪽지에 그린 뒤 (넘기면 되돌리기를 가리켜요)", "도깨비", "PenUndo"),
                                         ("안내-펜-3", "1챕터 · 방망이", "되돌린 뒤 (넘기면 다 그렸어! 를 가리켜요)", "도깨비", "PenDone"),
                                         ("도깨비-돋보기-1", "2챕터 · 양말", "첫 그리기 : 돋보기 시범 (넘기면 돋보기가 도깨비불로 강조돼요)", "도깨비", "MagnifierShow"),
                                         ("도깨비-돋보기-2", "2챕터 · 양말", "돋보기를 누른 뒤 (넘기면 둘러봐요)", "도깨비", "MagnifierHow"),
                                         ("도깨비-돋보기-3", "2챕터 · 양말", "시범으로 둘러본 뒤", "도깨비", "MagnifierRule"),
                                         ("안내-색-1", "3챕터 · 고깔모자", "첫 그리기 : 색 안내 (넘기면 도깨비불이 붓을 가리켜요)", "도깨비", "MixBrush"),
                                         ("안내-색-2", "3챕터 · 고깔모자", "붓을 눌러 팔레트가 열린 뒤", "도깨비", "MixHow"),
                                         ("도깨비-무료돋보기", "2~5챕터", "한 물건에서 두 번째로 틀렸을 때 (사탕이 남았으면, 게임오버는 제외)", "도깨비", "MagnifierGift"),
                                         ("도깨비-마무리", "1챕터 · 마무리", "화면을 누른 뒤 (넘기면 챕터 선택)", "도깨비", "EndingBye")):
        add(num, stage, scene, who, *const(name))

    def line_array(name, code, label, scenes):
        a, b = _block(src, name)
        matches = list(re.finditer(r'new Line\(Face\.(\w+),\s*"((?:[^"\\]|\\.)*)"\)', src[a:b]))
        assert len(matches) == len(scenes), name
        for k, (m, scene) in enumerate(zip(matches, scenes), 1):
            add(f"{code}-{k}", label, scene, FACE[m.group(1)], m.group(2), a + m.start(2), a + m.end(2))

    # 2~4챕터 : 방 (시작 장면 → 물건 3개 → 끝 장면)
    rooms = [(2, ["양말", "우산", "베개"]), (3, ["고깔모자", "풍선", "선물 상자"]), (4, ["귤", "포도", "줄무늬 사탕"])]
    line_re = r'new Line\(Face\.(\w+),\s*"((?:[^"\\]|\\.)*)"\)'
    opening = _top_groups(src, *_block(src, "RoomOpening"))
    item_intro = _top_groups(src, *_block(src, "RoomItemIntro"))
    right = _top_groups(src, *_block(src, "RoomDokkaebiRight"))
    wrong = _top_groups(src, *_block(src, "RoomDokkaebiWrong"))
    boy_right = _top_groups(src, *_block(src, "RoomBoyRight"))
    boy_wrong = _top_groups(src, *_block(src, "RoomBoyWrong"))
    ends = _top_groups(src, *_block(src, "RoomEnd"))
    assert len(opening) == len(item_intro) == len(right) == len(wrong) == len(boy_right) == len(boy_wrong) == len(ends) == 3
    for c, (chapter, items) in enumerate(rooms):
        code = f"C{chapter}"
        a, b = opening[c]
        for k, m in enumerate(re.finditer(line_re, src[a:b]), 1):
            scene = "시작 장면 : 주인공 혼자" if k <= 2 else "시작 장면 : 도깨비와 대화"
            add(f"{code}-시작-{k}", f"{chapter}챕터 · 시작", scene, FACE[m.group(1)], m.group(2), a + m.start(2), a + m.end(2))
        intros = _top_groups(src, *item_intro[c])
        rights, wrongs = _top_groups(src, *right[c]), _top_groups(src, *wrong[c])
        boy_r, boy_w = _strings(src, *boy_right[c]), _strings(src, *boy_wrong[c])
        for i, item in enumerate(items):
            label = f"{chapter}챕터 · {item}"
            a, b = intros[i]
            for k, m in enumerate(re.finditer(line_re, src[a:b]), 1):
                add(f"{code}-{item}-시작-{k}", label, "물건 대화 (순서대로)", FACE[m.group(1)], m.group(2), a + m.start(2), a + m.end(2))
            for key, title, ranges in (("맞음", "기준 통과 (무작위)", rights), ("틀림", "기준 실패 (무작위)", wrongs)):
                for k, st in enumerate(_strings(src, *ranges[i]), 1):
                    add(f"{code}-{item}-{key}-{k}", label, title, "도깨비", *st)
            add(f"{code}-{item}-주인공-맞음", label, "기준 통과 뒤 주인공", FACE["BoyHappy"], *boy_r[i])
            add(f"{code}-{item}-주인공-틀림", label, "기준 실패 뒤 주인공" + (" (사탕이 없어 다시 못 그려요)" if chapter == 4 else " (사탕이 남았을 때)"),
                FACE["BoySad"], *boy_w[i])
        a, b = ends[c]
        for k, m in enumerate(re.finditer(line_re, src[a:b]), 1):
            add(f"{code}-끝-{k}", f"{chapter}챕터 · 끝 장면", "끝 장면 : 화면을 누르면 (순서대로)", FACE[m.group(1)], m.group(2), a + m.start(2), a + m.end(2))
    add("C4-건네기", "4챕터 · 모든 물건", "사탕 없이 쪽지만 건넬 때", FACE["BoyHappy"], *const("GiveBoyNoCandy"))

    line_array("CurseIntro", "C5-시작", "5챕터 · 시작", [
        "밝은 숲 : 주인공과 친구가 도깨비를 찾아옴",
        "밝은 숲 : 도깨비가 웃으며 맞이함",
        "숨겨 둔 간식 이야기 : 숲이 어두워지고 도깨비가 흑화함",
        "어두운 숲 : 도깨비가 저주를 걸며 그림을 요구함",
        "주인공 속마음 : 도깨비가 무서워하는 것을 떠올림",
    ])
    steps = _groups(src, "CurseStep")
    assert len(steps) == 3
    for i, ((a, b), requested, counter) in enumerate(zip(steps, ["짚신", "도깨비 부채", "도깨비 감투"], ["부적", "닭피 그릇", "팥주머니"]), 1):
        matches = list(re.finditer(r'new Line\(Face\.(\w+),\s*"((?:[^"\\]|\\.)*)"\)', src[a:b]))
        assert len(matches) == 2
        for k, m in enumerate(matches, 1):
            scene = f"요청 물건 {requested} : 그림을 요구함" if k == 1 else f"주인공 속마음 : 맞설 물건 {counter} 떠올림"
            add(f"C5-단계{i}-{k}", f"5챕터 · {i}단계", scene, FACE[m.group(1)], m.group(2), a + m.start(2), a + m.end(2))

    takes = _strings(src, *_block(src, "CurseTake"))
    assert len(takes) == 3
    for i, (s, item) in enumerate(zip(takes, ["짚신", "도깨비 부채", "도깨비 감투"]), 1):
        add(f"C5-요청-{i}", f"5챕터 · {i}단계", f"요청 물건 {item}을 고름 : 그리지 않고 도깨비가 가져감, 주인공 하트 -1", "도깨비", *s)
    hits = _strings(src, *_block(src, "CurseCounterHit"))
    assert len(hits) == 3
    for i, s in enumerate(hits, 1):
        add(f"C5-맞섬-성공-{i}", f"5챕터 · {i}단계", "맞설 물건 성공 : 도깨비가 \"제법이군\" 하고 다음 단계로" + (" (마지막 단계)" if i == 3 else ""), "도깨비", *s)

    add("C5-쪽지글", "5챕터 · 맞설 물건", "쪽지에 적힌 글", "(쪽지 글)", *const("CurseNote"))
    add("C5-건네기", "5챕터 · 맞설 물건", "다 그린 쪽지를 도깨비에게 들이밀 때 (사탕 없음)", FACE["BoySurprised"], *const("CurseGiveBoy"))
    for num, scene, who, name in (
        ("C5-주인공-요청", "요청 물건을 빼앗긴 뒤 주인공 속마음", FACE["BoySad"], "CurseBoyTaken"),
        ("C5-주인공-맞섬", "맞설 물건 성공 뒤 주인공 속마음", FACE["BoyHappy"], "CurseBoyHit"),
        ("C5-맞섬-실패", "맞설 물건 실패 : 점프스케어 뒤 도깨비가 비웃음, 주인공 하트 -1", "도깨비", "CurseCounterWrong"),
        ("C5-주인공-실패", "맞설 물건 실패 뒤 주인공 (하트가 남았을 때)", FACE["BoySad"], "CurseBoyWrong"),
        ("C5-영혼", "하트 0 : 친구가 쓰러지고 도깨비가 친구 영혼을 데려감", "도깨비", "CurseSoulTaken"),
        ("C5-게임오버", "게임오버 : 도깨비 뒤로 의식을 잃은 사람들의 실루엣", "도깨비", "CurseGameOver"),
        ("C5-해피-방망이", "3단계를 모두 이김 : 착해진 도깨비가 도망감. 1챕터에서 찾은 방망이를 남김", FACE["BoyHappy"], "CurseHappy"),
        ("C5-해피-방망이없음", "3단계를 모두 이김 : 착해진 도깨비가 도망감. 1챕터에서 방망이를 못 찾음", FACE["BoyHappy"], "CurseHappyNoBat"),
    ):
        add(num, "5챕터 · 결과", scene, who, *const(name))
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

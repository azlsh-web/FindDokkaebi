# 돌려받은 대사 수정표를 GameScript.cs 에 반영해요.
#   python Tools/docs/apply_dialogue_edits.py 받은파일.xlsx            검사만
#   python Tools/docs/apply_dialogue_edits.py 받은파일.xlsx --apply    검사를 통과한 줄을 반영하고 대사 트리와 수정표를 다시 만들어요
#   --source <파일> : 다른 파일에 반영해 봐요 (도구 시험용, 문서는 다시 만들지 않아요)
import json
import os
import re
import subprocess
import sys

from openpyxl import load_workbook

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dialogue_lines import MAX_LINES, ROOT, SOURCE, escape, font_chars, read_lines, wrapped_lines

sys.stdout.reconfigure(encoding="utf-8")
args = sys.argv[1:]
apply = "--apply" in args
source = args[args.index("--source") + 1] if "--source" in args else SOURCE
sheet_path = args[0]


def feature_words():
    """판정 기준 낱말. 결과 대사에 나오면 정답 힌트가 돼요."""
    words = set()
    data = os.path.join(ROOT, "Assets", "Data")
    for f in os.listdir(data):
        if f.endswith(".asset"):
            for raw in re.findall(r"^    name: (.*)$", open(os.path.join(data, f), encoding="utf-8").read(), re.M):
                name = json.loads(raw.strip()) if raw.strip().startswith('"') else raw.strip()
                words.update(w.strip() for w in name.split("+"))
    return words


ws = load_workbook(sheet_path, data_only=False)["대사"]
header = None
for row in ws.iter_rows(min_row=1, max_row=5):
    names = [c.value for c in row]
    if "번호" in names:
        header = {name: i for i, name in enumerate(names) if name}
        first = row[0].row + 1
        break
assert header, "`번호` 칸이 있는 줄을 찾지 못했어요"

known = {r["id"]: r for r in read_lines(source)}
chars = font_chars()
feature = feature_words()
changes, errors, warnings, memos, new_chars = [], [], [], [], set()

for row in ws.iter_rows(min_row=first, values_only=True):
    num = row[header["번호"]]
    if not num:
        continue
    old_in_sheet = row[header["지금 대사"]]
    new = row[header["바꿀 대사"]]
    memo = row[header["메모"]]
    if memo:
        memos.append(f"{num} : {memo}")
    if new is None or not str(new).strip():
        continue
    new = " ".join(str(new).split())    # 칸 안의 줄바꿈과 겹친 띄어쓰기 정리

    line = known.get(num)
    if line is None:
        errors.append(f"{num} : 없는 번호예요")
        continue
    if str(old_in_sheet) != line["text"]:
        errors.append(f"{num} : 표를 만든 뒤 대사가 바뀌었어요. 지금 코드의 대사는 `{line['text']}`예요")
        continue
    if new == line["text"]:
        continue
    count = wrapped_lines(new)
    if count > MAX_LINES:
        errors.append(f"{num} : 대사 상자에 {count}줄이 돼서 넘쳐요 ({MAX_LINES}줄까지) : {new}")
        continue
    is_result = re.search(r"-(맞음|일부|틀림)-", num)
    if is_result:
        hits = [w for w in feature if w in new]
        if hits:
            warnings.append(f"{num} : 판정 기준 낱말이 있어요 ({', '.join(hits)}) : {new}")
    if len(re.findall(r"빨강|노랑|파랑", new)) >= 2 or re.search(r"(빨강|노랑|파랑).*섞", new):
        warnings.append(f"{num} : 색 조합을 알려 주는 대사일 수 있어요 : {new}")
    new_chars |= {c for c in new if c not in chars and not c.isspace()}
    changes.append((line, new, count))

print(f"반영할 대사 {len(changes)}줄, 막힌 대사 {len(errors)}줄, 확인할 것 {len(warnings)}개, 메모 {len(memos)}개\n")
for line, new, count in changes:
    print(f"[{line['id']}] {count}줄\n  전 : {line['text']}\n  후 : {new}")
for title, items in (("막힌 대사 (반영하지 않아요)", errors), ("확인할 것 (반영은 돼요)", warnings), ("메모 (사람이 처리해요)", memos)):
    if items:
        print(f"\n{title}")
        for item in items:
            print(" -", item)
if new_chars:
    print("\n글꼴에 없는 글자 :", " ".join(sorted(new_chars)), "\n  Noto 글꼴을 다시 구워야 게임에 보여요. 글꼴에 넣기 전에는 네모로 보여요.")

if not apply:
    print("\n검사만 했어요. 반영하려면 --apply 를 붙여 주세요.")
elif changes:
    src = open(source, encoding="utf-8", newline="").read()
    for line, new, _ in sorted(changes, key=lambda c: c[0]["start"], reverse=True):
        src = src[:line["start"]] + escape(new) + src[line["end"]:]
    open(source, "w", encoding="utf-8", newline="").write(src)
    # 반영한 뒤 다시 읽어 맞는지 확인해요
    after = {r["id"]: r["text"] for r in read_lines(source)}
    wanted = {l["id"]: l["text"] for l in known.values()}
    wanted.update({l["id"]: n for l, n, _ in changes})
    assert after == wanted, "반영 결과가 다르게 읽혀요"
    print(f"\n{len(changes)}줄을 {os.path.basename(source)} 에 반영했어요.")
    if os.path.abspath(source) == os.path.abspath(SOURCE):
        tools = os.path.dirname(os.path.abspath(__file__))
        for tool in ("make_dialogue_tree.py", "make_dialogue_sheet.py"):
            subprocess.run([sys.executable, os.path.join(tools, tool)], check=True)
        print("이어서 Unity 자동 플레이 테스트와 웹 빌드를 하면 돼요.")
else:
    print("\n반영할 대사가 없어요.")
sys.exit(1 if errors else 0)

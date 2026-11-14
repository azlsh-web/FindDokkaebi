# 아트팀에게 받은 ZIP 을 검사하고 게임에 넣어요 (같은 이름 파일 덮어쓰기라 코드 · 씬은 그대로예요)
#   python apply_art_zip.py <받은 ZIP>            검사만
#   python apply_art_zip.py <받은 ZIP> --apply    검사를 통과한 파일을 Assets/Sprites 의 제자리에 덮어쓰기
import io, os, sys, zipfile
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from art_tree_data import ANY_ALPHA, OPAQUE, REQUEST

sys.stdout.reconfigure(encoding="utf-8")
SPRITES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Sprites")
INFO = {r[0]: r for r in REQUEST}
RATIO_TOLERANCE = 0.02     # 비율이 이만큼 넘게 다르면 게임에서 찌그러져 보여요


def check(name, data):
    """문제 목록을 돌려줘요 (없으면 통과)"""
    im = Image.open(io.BytesIO(data))
    if im.format != "PNG":
        return [f"PNG 가 아니에요 ({im.format})"]
    now = INFO[name][3]
    problems = []
    want, got = now[0] / now[1], im.width / im.height
    if abs(got / want - 1) > RATIO_TOLERANCE:
        problems.append(f"비율이 달라요 : 받은 {im.width}x{im.height}, 요청 {now[0]}x{now[1]}")
    if INFO[name][1] != "Background" and name not in OPAQUE | ANY_ALPHA and im.convert("RGBA").getchannel("A").getextrema()[0] == 255:
        problems.append("투명한 부분이 없어요")
    return problems


path = sys.argv[1]
apply = "--apply" in sys.argv[2:]
wanted = {r[0] for r in REQUEST}
good, bad = {}, {}
with zipfile.ZipFile(path) as z:
    for entry in z.namelist():
        base = os.path.basename(entry)
        if not base or "__MACOSX" in entry:
            continue
        stem, ext = os.path.splitext(base)
        if ext.lower() != ".png" or stem not in wanted:
            bad[base] = ["요청 목록에 없는 파일이에요"]
            continue
        data = z.read(entry)
        problems = check(stem, data)
        if problems:
            bad[base] = problems
        else:
            good[stem] = data

for name in sorted(good):
    print("통과", name + ".png")
for name, problems in sorted(bad.items()):
    print("문제", name, "-", " / ".join(problems))
missing = sorted(wanted - set(good) - {os.path.splitext(n)[0] for n in bad})
if missing:
    print("아직 안 온 파일", ", ".join(missing))

if apply:
    for name, data in good.items():
        with open(os.path.join(SPRITES, INFO[name][1], name + ".png"), "wb") as f:
            f.write(data)
    print(f"{len(good)}장을 덮어썼어요. Unity 에서 자동 플레이 테스트 → 웹 빌드 → 배포하면 돼요.")
else:
    print(f"검사만 했어요. 통과한 {len(good)}장을 넣으려면 --apply 를 붙여 주세요.")
sys.exit(1 if bad else 0)

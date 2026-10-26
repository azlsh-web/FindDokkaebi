# 팀 UI 시안(기획서 아트 수정본 그림 9·10, 2248x3952)에서 요소를 오려 내기
#   - 대략의 상자 안에서 진한 테두리의 실제 범위를 찾아 자르고
#   - 자른 그림 가장자리에서 시작해 테두리에 막힐 때까지 배경을 지워(투명) 요소만 남겨요.
import sys, os
from collections import deque
from PIL import Image, ImageDraw

SRC = "word/media"
OUT = sys.argv[1] if len(sys.argv) > 1 else "cut"
os.makedirs(OUT, exist_ok=True)


def dark(p):
    r, g, b = p[:3]
    return r + g + b < 300 and r < 140


def tight(im, box):
    x0, y0, x1, y1 = box
    xs, ys = [], []
    px = im.load()
    for y in range(y0, y1):
        for x in range(x0, x1):
            if dark(px[x, y]):
                xs.append(x); ys.append(y)
    return (min(xs), min(ys), max(xs) + 1, max(ys) + 1)


def cutout(src, box, name, pad=4, keep=None):
    im = Image.open(os.path.join(SRC, src)).convert("RGBA")
    bx = tight(im, box)
    bx = (bx[0] - pad, bx[1] - pad, bx[2] + pad, bx[3] + pad)
    c = im.crop(bx)
    w, h = c.size
    px = c.load()
    seen = [[False] * w for _ in range(h)]
    q = deque()
    for x in range(w):
        q.append((x, 0)); q.append((x, h - 1))
    for y in range(h):
        q.append((0, y)); q.append((w - 1, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]:
            continue
        seen[y][x] = True
        if dark(px[x, y]):
            continue
        r, g, b, a = px[x, y]
        px[x, y] = (r, g, b, 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    c.save(os.path.join(OUT, name + ".png"))
    print(name, bx, c.size)
    return bx


boxes = {}
D = "image10.png"; O = "image9.png"
boxes["Logo"] = cutout(D, (190, 110, 870, 345), "Logo")
boxes["StagePill"] = cutout(D, (940, 110, 1730, 345), "StagePill")
boxes["Pause"] = cutout(D, (1890, 105, 2150, 345), "Pause")
boxes["SnackPill"] = cutout(D, (100, 410, 890, 690), "SnackPill")
boxes["Clipboard"] = cutout(D, (120, 640, 1950, 2640), "Clipboard")
boxes["Palette"] = cutout(D, (120, 2670, 1640, 3100), "Palette")
boxes["MixButton"] = cutout(D, (1630, 2670, 2060, 3090), "MixButton")
boxes["PencilButton"] = cutout(D, (290, 3090, 660, 3450), "PencilButton")
boxes["EraserButton"] = cutout(D, (710, 3090, 1070, 3450), "EraserButton")
boxes["UndoButton"] = cutout(D, (1120, 3090, 1480, 3450), "UndoButton")
boxes["SizePill"] = cutout(D, (1530, 3130, 2090, 3430), "SizePill")
boxes["SubmitButton"] = cutout(D, (230, 3470, 2000, 3860), "SubmitButton")

open(os.path.join(OUT, "boxes.txt"), "w").write(repr(boxes))

# 시작 화면 부품 오리기 : 완성 그림(그림 11)과 빈 방 배경(StartBG)의 차이로 물건만 떼어 내요.
#   python cut_title_parts.py <FindDokkaebi 폴더>
#   좌표는 시안 그림(2248x3952) 픽셀이에요. MvpSceneBuilder 의 시작 화면도 같은 좌표에 올려요.
import os, shutil, sys
import numpy as np
from PIL import Image, ImageFilter

ROOT = sys.argv[1]
TITLE = os.path.join(ROOT, "ArtSource", "원본", "기획서", "그림11_시작화면_확정.png")
BG = os.path.join(ROOT, "Assets", "Sprites", "Background", "BG_Start.png")
OUT = os.path.join(ROOT, "Assets", "Sprites", "UI")
PARTS = {
    "Title_Logo": (308, 168, 1940, 1348),
    "Title_Tagline": (312, 2928, 1940, 3152),
    "Title_StartButton": (196, 3200, 2052, 3600),
    "Title_Gear": (1928, 3616, 2200, 3884),
}
MARGIN = 8


def fill_holes(mask):
    """바깥과 이어지지 않은 안쪽 구멍을 채워요 (배경과 색이 같은 안쪽이 뚫리지 않게)."""
    h, w = mask.shape
    outside = np.zeros((h, w), bool)
    stack = [(y, x) for y in range(h) for x in (0, w - 1) if not mask[y, x]]
    stack += [(y, x) for x in range(w) for y in (0, h - 1) if not mask[y, x]]
    for y, x in stack:
        outside[y, x] = True
    while stack:
        y, x = stack.pop()
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and not mask[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                stack.append((ny, nx))
    return ~outside


def largest_blob(mask):
    """가장 큰 덩어리만 남겨요 (배경 그림과 살짝 달라 묻어 나온 작은 조각을 지워요)."""
    h, w = mask.shape
    seen = np.zeros((h, w), bool)
    best = []
    for sy, sx in zip(*np.nonzero(mask)):
        if seen[sy, sx]:
            continue
        seen[sy, sx] = True
        stack, cells = [(sy, sx)], []
        while stack:
            y, x = stack.pop()
            cells.append((y, x))
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        if len(cells) > len(best):
            best = cells
    out = np.zeros((h, w), bool)
    ys, xs = zip(*best)
    out[list(ys), list(xs)] = True
    return out


title = Image.open(TITLE).convert("RGB")
bg = Image.open(BG).convert("RGB")
for name, (x0, y0, x1, y1) in PARTS.items():
    box = (max(0, x0 - MARGIN), max(0, y0 - MARGIN), min(2248, x1 + MARGIN), min(3952, y1 + MARGIN))
    a = np.asarray(title.crop(box), dtype=np.int16)
    b = np.asarray(bg.crop(box), dtype=np.int16)
    mask = fill_holes(np.abs(a - b).max(axis=2) > 24)
    if name == "Title_Logo":     # 로고 뒤 구름 선이 붙어 나와요 : 가는 선은 깎아 내고 간판 · 불꽃만 남겨요
        solid = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(21)).filter(ImageFilter.MaxFilter(21))
        mask = np.asarray(solid) > 128
        mask[:205, :715] = False    # 간판 위쪽은 불꽃 말고 비워요 (구름 조각 지우기)
        mask[:205, 935:] = False
        mask[:, :14] = False        # 간판 왼쪽 밖으로 삐져나온 구름 선
    mask = largest_blob(mask)
    alpha = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
    part = title.crop(box).convert("RGBA")
    part.putalpha(alpha)
    part.save(os.path.join(OUT, name + ".png"))
    print(name, box, part.size)


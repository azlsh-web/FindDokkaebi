# 향 · 촛대 부품 그리기 : 줄기 · 불씨 · 불꽃 · 재 · 놋쇠 촛대, 연기와 빛에 쓰는 부드러운 동그라미
#   python cut_incense.py <FindDokkaebi 폴더>
#   게임 시안처럼 굵은 어두운 외곽선 + 단순한 면으로 그려요. 4배로 그린 뒤 줄여서 선을 매끄럽게 해요.
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw

ROOT = sys.argv[1]
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")
S = 4
LINE = (60, 36, 21, 255)


def canvas(w, h):
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def save(im, name):
    im.resize((im.width // S, im.height // S), Image.LANCZOS).save(os.path.join(UI, name + ".png"))


def rounded(d, x0, y0, x1, y1, r, fill, line=LINE, width=3):
    d.rounded_rectangle([x0 * S, y0 * S, x1 * S, y1 * S], radius=r * S, fill=fill, outline=line, width=width * S)


def poly(d, pts, fill, width=3):
    p = [(x * S, y * S) for x, y in pts]
    d.polygon(p, fill=fill)
    d.line(p + [p[0], p[1]], fill=LINE, width=width * S, joint="curve")


def teardrop(cx, bottom, width, height, n=60):
    """위가 뾰족한 불꽃 모양 점들"""
    pts = []
    for i in range(n):
        t = i / n * 2 * math.pi
        y = (math.cos(t) + 1) / 2                 # 1 = 아래, 0 = 위
        r = math.sin(t) * (y ** 0.85) * (0.52 + 0.48 * y) * width / 2
        pts.append((cx + r, bottom - height * (1 - y)))
    return pts


# 줄기 : 금빛 향 (캔버스 폭은 다른 부품과 같은 74)
im, d = canvas(74, 552)
rounded(d, 26, 3, 48, 549, 6, (246, 184, 52, 255))
d.rectangle([40 * S, 6 * S, 45 * S, 546 * S], fill=(219, 146, 33, 255))
d.rectangle([30 * S, 6 * S, 33 * S, 546 * S], fill=(255, 221, 128, 255))
rounded(d, 26, 3, 48, 549, 6, None)
save(im, "Incense_Stick")

# 불씨 : 붉게 달아오른 끝
im, d = canvas(74, 40)
rounded(d, 26, 3, 48, 37, 8, (232, 58, 42, 255))
rounded(d, 31, 7, 43, 22, 5, (255, 150, 62, 255), line=(255, 150, 62, 255), width=1)
save(im, "Incense_Ember")

# 재 : 회색으로 타고 남은 끝
im, d = canvas(74, 150)
poly(d, [(28, 146), (27, 80), (29, 22), (38, 4), (47, 18), (47, 80), (46, 146)], (205, 205, 208, 255))
d.line([(34 * S, 34 * S), (34 * S, 136 * S)], fill=(168, 168, 174, 255), width=3 * S)
save(im, "Incense_Ash")

# 불꽃 : 바깥 노랑 + 안쪽 주황
im, d = canvas(90, 120)
poly(d, teardrop(45, 114, 74, 108), (255, 209, 102, 255), width=4)
in_pts = [(x * S, y * S) for x, y in teardrop(45, 104, 38, 58)]
d.polygon(in_pts, fill=(239, 138, 34, 255))
save(im, "Incense_Flame")

# 촛대 : 놋쇠 접시 받침 + 기둥 + 위쪽 잔
BRASS, DARK, LIGHT = (230, 178, 84, 255), (190, 128, 48, 255), (250, 224, 150, 255)
im, d = canvas(130, 176)
cx = 65
# 받침 접시
d.ellipse([9 * S, 142 * S, 121 * S, 172 * S], fill=DARK, outline=LINE, width=3 * S)
d.ellipse([9 * S, 134 * S, 121 * S, 164 * S], fill=BRASS, outline=LINE, width=3 * S)
d.ellipse([30 * S, 141 * S, 100 * S, 157 * S], fill=DARK)
# 기둥
poly(d, [(cx - 14, 150), (cx - 7, 112), (cx - 7, 62), (cx + 7, 62), (cx + 7, 112), (cx + 14, 150)], BRASS)
d.rectangle([(cx - 3) * S, 66 * S, (cx + 1) * S, 134 * S], fill=LIGHT)
# 기둥 가운데 마디
d.ellipse([(cx - 17) * S, 88 * S, (cx + 17) * S, 112 * S], fill=BRASS, outline=LINE, width=3 * S)
d.ellipse([(cx - 9) * S, 93 * S, (cx + 1) * S, 101 * S], fill=LIGHT)
# 위쪽 잔 (향을 꽂는 곳)
poly(d, [(cx - 34, 18), (cx + 34, 18), (cx + 20, 56), (cx + 8, 68), (cx - 8, 68), (cx - 20, 56)], BRASS)
d.polygon([((cx + 14) * S, 22 * S), ((cx + 31) * S, 22 * S), ((cx + 18) * S, 54 * S), ((cx + 7) * S, 64 * S)], fill=DARK)
d.ellipse([(cx - 36) * S, 8 * S, (cx + 36) * S, 28 * S], fill=DARK, outline=LINE, width=3 * S)
d.ellipse([(cx - 26) * S, 12 * S, (cx + 26) * S, 24 * S], fill=(120, 76, 34, 255))
save(im, "Incense_Holder")

# 연기 · 빛에 쓰는 부드러운 동그라미
n = 128
y, x = np.mgrid[0:n, 0:n]
r = np.hypot(x - (n - 1) / 2, y - (n - 1) / 2) / (n / 2)
alpha = np.clip(1 - r, 0, 1) ** 1.6
puff = np.dstack([np.full((n, n, 3), 255), (alpha * 255).astype(np.int32)]).astype(np.uint8)
Image.fromarray(puff, "RGBA").save(os.path.join(UI, "Common_SoftPuff.png"))

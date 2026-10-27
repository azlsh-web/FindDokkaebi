# 3단계 : 물감 판 색 동그라미 하나씩 + 빈 물감 판 + 굵기 점 위치 + 남은 배경 선 정리
import os
from collections import deque
from PIL import Image, ImageDraw

OUT = "cut"
src = Image.open("word/media/image10.png").convert("RGBA")
boxes = eval(open(os.path.join(OUT, "boxes.txt")).read())


def dark(p):
    r, g, b = p[:3]
    return r + g + b < 300 and r < 140


def region(center, tol):
    # 가운데 색과 비슷한 칸을 이어서 찾기 → 칠한 부분의 범위
    px = src.load()
    c = px[center]
    seen = set([center]); q = deque([center]); xs = []; ys = []
    while q:
        x, y = q.popleft(); xs.append(x); ys.append(y)
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if n in seen: continue
            p = px[n]
            if abs(p[0] - c[0]) + abs(p[1] - c[1]) + abs(p[2] - c[2]) < tol:
                seen.add(n); q.append(n)
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def cut_outlined(box):
    im = src.crop(box).copy()
    w, h = im.size; px = im.load()
    seen = [[False] * w for _ in range(h)]
    q = deque()
    for x in range(w): q.append((x, 0)); q.append((x, h - 1))
    for y in range(h): q.append((0, y)); q.append((w - 1, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]: continue
        seen[y][x] = True
        if dark(px[x, y]): continue
        r, g, b, a = px[x, y]; px[x, y] = (r, g, b, 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return im


blobs = {"Black": (496, 2844), "Red": (720, 2884), "Yellow": (944, 2896), "Blue": (1168, 2884), "Mixed": (1388, 2844)}
pal = Image.open(os.path.join(OUT, "Palette.png"))
px0, py0 = boxes["Palette"][:2]
cream = pal.getpixel((1000 - px0 + 300, 2780 - py0)) if False else src.getpixel((1500, 2900))
print("palette cream", cream)
d = ImageDraw.Draw(pal)
def blob_cut(center, bg):
    # 판 색과 확실히 다른 칸(동그라미 + 진한 테두리)을 가운데에서부터 이어서 찾기
    px = src.load()
    def diff(p): return abs(p[0] - bg[0]) + abs(p[1] - bg[1]) + abs(p[2] - bg[2])
    seen = set([center]); q = deque([center]); pts = []
    while q:
        x, y = q.popleft(); pts.append((x, y))
        for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if n in seen or abs(n[0] - center[0]) > 140 or abs(n[1] - center[1]) > 140: continue
            if diff(px[n]) > 70:
                seen.add(n); q.append(n)
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    box = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
    im = src.crop(box).copy()
    m = Image.new("L", im.size, 0); mp = m.load()
    for x, y in pts: mp[x - box[0], y - box[1]] = 255
    im.putalpha(m)
    return im, box

for name, c in blobs.items():
    blob, real = blob_cut(c, cream)
    boxes["Blob" + name] = real
    if name == "Mixed":
        # 섞은 색 칸 : 명암만 남긴 흰 동그라미 (게임에서 색을 곱해 칠해요)
        p = blob.load(); fill = src.getpixel(c)
        for y in range(blob.height):
            for x in range(blob.width):
                r, g, b, a = p[x, y]
                if a == 0: continue
                k = (r + g + b) / max(1, sum(fill[:3]))
                v = max(0, min(255, int(235 * k)))
                p[x, y] = (v, v, v, a)
    blob.save(os.path.join(OUT, "Blob" + name + ".png"))
    e = (real[0] - px0 - 4, real[1] - py0 - 4, real[2] - px0 + 4, real[3] - py0 + 4)
    d.ellipse(e, fill=cream)
    print(name, real)
pal.save(os.path.join(OUT, "PaletteEmpty.png"))

# 굵기 점 : 가운데·지름
for i, c in enumerate([(1640, 3280), (1790, 3280), (1940, 3280)]):
    bx = region(c, 60)
    boxes["Dot%d" % i] = bx
    print("dot", i, bx)

# 클립보드·관찰 카드 : 바깥쪽에 딸려 온 배경 선 지우기 (세로로 길게 이어진 테두리 기둥 바깥)
def clear_outside(name, top_skip, right_top_skip=0):
    im = Image.open(os.path.join(OUT, name + ".png")); p = im.load(); w, h = im.size
    counts = [sum(1 for y in range(h // 4, h * 3 // 4) if p[x, y][3] > 0 and dark(p[x, y])) for x in range(w)]
    left = next(x for x in range(w) if counts[x] > h // 4)
    right = next(x for x in range(w - 1, 0, -1) if counts[x] > h // 4)
    for y in range(top_skip, h):
        xs = list(range(0, left)) + (list(range(right + 1, w)) if y >= right_top_skip else [])
        for x in xs:
            r, g, b, a = p[x, y]; p[x, y] = (r, g, b, 0)
    im.save(os.path.join(OUT, name + ".png"))
    print(name, "edges", left, right)

clear_outside("Clipboard", 0)
clear_outside("ObserveCard", 0, right_top_skip=1100 - 590)   # 오른쪽 위 말풍선은 남김
open(os.path.join(OUT, "boxes.txt"), "w").write(repr(boxes))

# 색 동그라미 : 동그라미 밖으로 딸려 온 선 지우기 (세로 지름을 반지름 기준으로)
for name in blobs:
    im = Image.open(os.path.join(OUT, "Blob" + name + ".png")); p = im.load(); w, h = im.size
    pts = [(x, y) for y in range(h) for x in range(w) if p[x, y][3] > 0]
    ys = [y for x, y in pts]; cy = (min(ys) + max(ys)) / 2; r = (max(ys) - min(ys)) / 2 + 2
    row = [x for x, y in pts if abs(y - cy) < 3]; cx = (min(row) + max(row)) / 2 if (max(row) - min(row)) < 2 * r + 4 else w - r - 2
    for x, y in pts:
        if (x - cx) ** 2 + (y - cy) ** 2 > r * r:
            a = p[x, y]; p[x, y] = (a[0], a[1], a[2], 0)
    bb = im.getchannel("A").getbbox()
    b0 = boxes["Blob" + name]
    boxes["Blob" + name] = (b0[0] + bb[0], b0[1] + bb[1], b0[0] + bb[2], b0[1] + bb[3])
    im.crop(bb).save(os.path.join(OUT, "Blob" + name + ".png"))

# 관찰 카드 왼쪽 위 코르크판 모서리 선 지우기
im = Image.open(os.path.join(OUT, "ObserveCard.png")); p = im.load()
for y in range(0, 440):
    for x in range(0, 300):
        a = p[x, y]; p[x, y] = (a[0], a[1], a[2], 0)
im.save(os.path.join(OUT, "ObserveCard.png"))
open(os.path.join(OUT, "boxes.txt"), "w").write(repr(boxes))
print({k: v for k, v in boxes.items() if k.startswith("Blob")})

# 빈 물감 판 : 맨 오른쪽에 딸려 온 색 섞기 버튼 테두리(시안 x 1628 부터) 지우기
pe = Image.open(os.path.join(OUT, "PaletteEmpty.png")); p = pe.load(); w, h = pe.size
for y in range(h):
    for x in range(1628 - px0, w):
        c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
pe.save(os.path.join(OUT, "PaletteEmpty.png"))

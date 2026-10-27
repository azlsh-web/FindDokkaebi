# 5단계 : 복주머니(간식) 아이콘 — 간식 알약 속 그림만
import os
from collections import deque
from PIL import Image
src = Image.open("word/media/image10.png").convert("RGBA")
box = (150, 440, 400, 680)
im = src.crop(box).copy(); p = im.load(); w, h = im.size
bg = src.getpixel((420, 470))
def near(c): return abs(c[0] - bg[0]) + abs(c[1] - bg[1]) + abs(c[2] - bg[2]) < 40
seen = set(); q = deque([(x, 0) for x in range(w)] + [(x, h - 1) for x in range(w)] + [(0, y) for y in range(h)] + [(w - 1, y) for y in range(h)])
while q:
    x, y = q.popleft()
    if (x, y) in seen or x < 0 or y < 0 or x >= w or y >= h: continue
    seen.add((x, y))
    if not near(p[x, y]): continue
    c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
    q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
im = im.crop(im.getchannel("A").getbbox())
im.save("cut/SnackBag.png"); print(im.size, bg)
# 가장 큰 덩어리(복주머니)만
a = im.getchannel("A"); ap = a.load(); w, h = im.size
seen = set(); best = []
for sy in range(h):
    for sx in range(w):
        if (sx, sy) in seen or ap[sx, sy] == 0: continue
        comp = []; q = deque([(sx, sy)]); seen.add((sx, sy))
        while q:
            x, y = q.popleft(); comp.append((x, y))
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= n[0] < w and 0 <= n[1] < h and n not in seen and ap[n] > 0:
                    seen.add(n); q.append(n)
        if len(comp) > len(best): best = comp
keep = set(best); p = im.load()
for y in range(h):
    for x in range(w):
        if (x, y) not in keep:
            c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
im = im.crop(im.getchannel("A").getbbox()); im.save("cut/SnackBag.png"); print("bag", im.size)

# 단계 물건 원본 : 아트 수정 기획서 그림 1·3·5·7(관찰 화면 예시)의 쪽지 속 물건을 그대로 오려 내기
import os
from collections import deque
from PIL import Image
import sys as _s, os as _o; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from clean_edges import soften
OUT = "objects"; os.makedirs(OUT, exist_ok=True)

def cut(src, box, name, scale=2):
    im = Image.open(os.path.join("word/media", src)).convert("RGBA").crop(box)
    w, h = im.size; p = im.load()
    bg = p[2, 2]
    def near(c): return abs(c[0] - bg[0]) + abs(c[1] - bg[1]) + abs(c[2] - bg[2]) < 30
    seen = set(); q = deque([(x, 0) for x in range(w)] + [(x, h - 1) for x in range(w)] + [(0, y) for y in range(h)] + [(w - 1, y) for y in range(h)])
    while q:
        x, y = q.popleft()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h): continue
        seen.add((x, y))
        if not near(p[x, y]): continue
        c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    # 가장 큰 덩어리만 (옆 그림 조각 빼기)
    a = im.getchannel("A").load(); seen = set(); best = []
    for sy in range(h):
        for sx in range(w):
            if (sx, sy) in seen or a[sx, sy] == 0: continue
            comp = []; q = deque([(sx, sy)]); seen.add((sx, sy))
            while q:
                x, y = q.popleft(); comp.append((x, y))
                for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= n[0] < w and 0 <= n[1] < h and n not in seen and a[n] > 0:
                        seen.add(n); q.append(n)
            if len(comp) > len(best): best = comp
    keep = set(best)
    for y in range(h):
        for x in range(w):
            if (x, y) not in keep:
                c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
    soften(im)                                              # 가장자리 부드럽게
    im = im.crop(im.getchannel("A").getbbox())
    im = im.convert("RGBa").resize((im.width * scale, im.height * scale), Image.LANCZOS).convert("RGBA")   # 배경색이 번지지 않게
    im.save(os.path.join(OUT, name + ".png")); print(name, im.size)

cut("image1.png", (60, 290, 400, 548), "Bat")
cut("image3.png", (60, 290, 400, 548), "Bag")
cut("image5.png", (60, 290, 400, 548), "Mat")
cut("image7.png", (60, 290, 400, 548), "Gimbap")

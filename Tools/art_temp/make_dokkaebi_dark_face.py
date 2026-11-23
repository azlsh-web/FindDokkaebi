"""5챕터 흑화 도깨비 대사 상자 얼굴 (임시, BACKLOG B1) : 아트팀 얼굴 3장을 흑화 몸 그림(Char_Dokkaebi_Dark) 색으로 바꿔요.
초록 피부 → 회청색, 노란 머리 → 갈색, 크림색 뿔 · 볼 → 회분홍, 까만 눈동자 → 빨강 (반짝이는 흰 점은 분홍으로).
실행 : py -3 Tools/art_temp/make_dokkaebi_dark_face.py  →  Assets/Sprites/Chapter5/Char_DokkaebiFace_<표정>_Dark.png
"""
from collections import deque
from PIL import Image

SRC = "Assets/Sprites/Characters"
OUT = "Assets/Sprites/Chapter5"
NAMES = ["Normal", "Proud", "Tease"]
# (아트팀 색, 흑화 몸 그림 색) : 흑화 색은 Char_Dokkaebi_Dark 에서 뽑았어요
PAIRS = [
    ((194, 216, 159), (91, 106, 104)),     # 피부
    ((250, 220, 159), (171, 143, 121)),    # 머리
    ((249, 231, 204), (171, 150, 151)),    # 뿔 · 볼
]
EYE = (241, 25, 49)
SHINE = (255, 160, 170)


def recolor(c):
    """피부 · 머리 · 뿔 색과 검은 선이 섞인 색까지 : 검정 → 아트팀 색 직선 위 어디쯤인지(t) 보고 흑화 색으로 옮겨요"""
    r, g, b, a = c
    best = None
    for src, dst in PAIRS:
        t = (r * src[0] + g * src[1] + b * src[2]) / sum(v * v for v in src)
        res = abs(r - src[0] * t) + abs(g - src[1] * t) + abs(b - src[2] * t)
        if 0.12 <= t <= 1.15 and (best is None or res < best[0]):
            best = (res, t, src, dst)
    if best is None or best[0] > 45:
        return c
    res, t, src, dst = best
    if t > 0.97:                             # 칠한 면 : 명암 차이를 반만 살려요
        return tuple(int(max(0, min(255, dst[i] + (c[i] - src[i]) * 0.5))) for i in range(3)) + (a,)
    return tuple(int(dst[i] * t) for i in range(3)) + (a,)


def dark(c):
    return c[3] > 128 and c[0] < 60 and c[1] < 60 and c[2] < 60


for name in NAMES:
    im = Image.open(f"{SRC}/Char_DokkaebiFace_{name}.png").convert("RGBA")
    w, h = im.size
    px = im.load()
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                px[x, y] = recolor(px[x, y])
    # 눈동자 : 크고 둥근 검은 덩어리 (테두리 · 입 선은 가늘어서 넓이가 작아요)
    seen = set()
    for sy in range(h):
        for sx in range(w):
            if (sx, sy) in seen or not dark(px[sx, sy]):
                continue
            comp, q = [], deque([(sx, sy)])
            seen.add((sx, sy))
            while q:
                x, y = q.popleft()
                comp.append((x, y))
                for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= n[0] < w and 0 <= n[1] < h and n not in seen and dark(px[n]):
                        seen.add(n)
                        q.append(n)
            xs = [p[0] for p in comp]
            ys = [p[1] for p in comp]
            bw, bh = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
            fill = len(comp) / (bw * bh)
            if len(comp) > 600 and fill > 0.45 and 0.6 < bw / bh < 1.7 and bw < w * 0.2:
                for p in comp:
                    px[p] = EYE + (px[p][3],)
                # 눈 안의 흰 반짝이 → 분홍
                for y in range(min(ys), max(ys) + 1):
                    for x in range(min(xs), max(xs) + 1):
                        c = px[x, y]
                        if c[3] > 128 and c[0] > 200 and c[1] > 200 and c[2] > 200:
                            px[x, y] = SHINE + (c[3],)
    im.save(f"{OUT}/Char_DokkaebiFace_{name}_Dark.png")
    print(name, "ok")

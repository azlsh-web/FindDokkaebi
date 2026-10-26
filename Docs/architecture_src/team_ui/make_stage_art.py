# 단계 물건 원본 + "엉뚱한 물건"(특징 하나가 다른 것) 만들기
#   원본 = 아트 수정 기획서 그림 1·3·5·7(물건), 그림 19(친구) 그대로
#   엉뚱한 물건 = 그 원본에서 한 부분만 바꾼 것 (색 바꾸기 · 한 부분 지우기 · 모양 바꾸기)
#   모두 512x512 투명 바탕 가운데에 놓아요 (판정이 정사각형 쪽지 기준이라서)
import os, sys, colorsys
from collections import deque
from PIL import Image, ImageDraw

OUT = sys.argv[1]            # Assets/Sprites
OBJ = "objects"


def fit512(im, margin=16):
    im = im.crop(im.getchannel("A").getbbox())          # 빈 곳 잘라 내고 쪽지 크기에 맞게
    s = (512 - 2 * margin) / max(im.width, im.height)
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    c = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    c.alpha_composite(im, ((512 - im.width) // 2, (512 - im.height) // 2))
    return c


def save(im, stage, name):
    d = os.path.join(OUT, stage); os.makedirs(d, exist_ok=True)
    im.save(os.path.join(d, name + ".png"))


def hsv(p):
    h, s, v = colorsys.rgb_to_hsv(p[0] / 255, p[1] / 255, p[2] / 255)
    return h * 360, s, v


def recolor(im, pred, target, region=None):
    # pred(h,s,v) 인 칸의 색을 target 색으로 (밝기 차이는 살려서)
    out = im.copy(); p = out.load()
    th, ts, tv = hsv(target)
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = p[x, y]
            if a == 0 or (region and not region(x, y)): continue
            h, s, v = hsv((r, g, b))
            if pred(h, s, v):
                k = min(1.4, v / max(0.01, hsv_ref[0]))
                nr, ng, nb = colorsys.hsv_to_rgb(th / 360, ts, max(0, min(1, tv * k)))
                p[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


hsv_ref = [1.0]


def set_ref(color):
    hsv_ref[0] = hsv(color)[2]


def clear(im, region):
    out = im.copy(); p = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if region(x, y):
                c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
    return out


def white_fill(im, region):
    out = im.copy(); p = out.load()
    for y in range(out.height):
        for x in range(out.width):
            if region(x, y) and p[x, y][3] > 0:
                p[x, y] = (246, 246, 246, 255)
    return out


def oval_shape(im, box, keep_inside=True, line=12, keep=None):
    # box 안을 타원으로 : 타원 밖은 지우고, 타원 테두리를 검게
    out = im.copy(); p = out.load()
    x0, y0, x1, y1 = box
    cx, cy, rx, ry = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2
    for y in range(out.height):
        for x in range(out.width):
            if keep and keep(x, y): continue
            if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 > 1:
                c = p[x, y]; p[x, y] = (c[0], c[1], c[2], 0)
    ImageDraw.Draw(out).ellipse(box, outline=(20, 20, 20, 255), width=line)
    return out


# ── 1 방망이 (그림 1) : 머리는 오른쪽 위, 손잡이는 왼쪽 아래 ──
bat = Image.open(os.path.join(OBJ, "Bat.png")).convert("RGBA")
W, H = bat.size
head = lambda x, y: y < 285 and x > 112             # 구름 머리 영역
save(fit512(bat), "Stage1_Bat", "Original")
save(fit512(clear(bat, lambda x, y: not head(x, y))), "Stage1_Bat", "Wrong_F1")       # 손잡이 없음 (머리만)
save(fit512(clear(bat, head)), "Stage1_Bat", "Wrong_F2")                               # 굵은 머리 없음 (손잡이만)
smooth = clear(bat, head)
d = ImageDraw.Draw(smooth)
d.ellipse((128, 18, 392, 290), fill=(246, 246, 246, 255), outline=(20, 20, 20, 255), width=13)   # 매끈한 둥근 머리
save(fit512(smooth), "Stage1_Bat", "Wrong_F3")                                         # 볼록볼록 없음

# ── 2 가방 (그림 3) ──
bag = Image.open(os.path.join(OBJ, "Bag.png")).convert("RGBA")
save(fit512(bag), "Stage2_Bag", "Original")
save(fit512(oval_shape(bag, (6, 74, bag.width - 6, bag.height - 2), keep=lambda x, y: y < 92 and 132 < x < 314)), "Stage2_Bag", "Wrong_F1")   # 둥근 몸체
save(fit512(clear(bag, lambda x, y: y < 84 and 135 < x < 312)), "Stage2_Bag", "Wrong_F2")            # 위 손잡이 없음
save(fit512(white_fill(bag, lambda x, y: 78 < x < 368 and 228 < y < 420)), "Stage2_Bag", "Wrong_F3")  # 주머니 없음
save(fit512(white_fill(bag, lambda x, y: 104 < x < 342 and 272 < y < 398)), "Stage2_Bag", "Wrong_F4") # 줄무늬 없음

# ── 3 돗자리 (그림 5) : 파랑 · 빨강 · 노랑 · 파랑 띠 ──
mat = Image.open(os.path.join(OBJ, "Mat.png")).convert("RGBA")
isBlue = lambda h, s, v: 190 < h < 240 and s > 0.4 and v > 0.4
isRed = lambda h, s, v: (h < 15 or h > 340) and s > 0.4 and v > 0.5
isYellow = lambda h, s, v: 35 < h < 65 and s > 0.4 and v > 0.5
BLUE, RED, YELLOW, GREEN = (40, 120, 225), (240, 80, 80), (250, 205, 70), (70, 160, 80)
save(fit512(mat), "Stage3_Mat", "Original")
save(fit512(oval_shape(mat, (6, 4, mat.width - 6, mat.height - 4))), "Stage3_Mat", "Wrong_F1")       # 네모가 아님 (둥근 돗자리)
set_ref(RED); m2 = recolor(mat, isRed, BLUE)
set_ref(YELLOW); m2 = recolor(m2, isYellow, BLUE)
save(fit512(m2), "Stage3_Mat", "Wrong_F2")                                                         # 띠 없이 한 색
set_ref(RED); save(fit512(recolor(mat, isRed, BLUE)), "Stage3_Mat", "Wrong_F3")                   # 빨강 띠 없음
set_ref(YELLOW); save(fit512(recolor(mat, isYellow, RED)), "Stage3_Mat", "Wrong_F4")              # 노랑 띠 없음
set_ref(BLUE); save(fit512(recolor(mat, isBlue, GREEN)), "Stage3_Mat", "Wrong_F5")                # 양 끝이 파랑 아님

# ── 4 김밥 (그림 7) ──
gim = Image.open(os.path.join(OBJ, "Gimbap.png")).convert("RGBA")
gw, gh = gim.size; gcx, gcy, gr = gw / 2, gh / 2, min(gw, gh) / 2
ring = lambda x, y: ((x - gcx) ** 2 + (y - gcy) ** 2) ** 0.5 > gr * 0.80
inner = lambda x, y: not ring(x, y)
isDark = lambda h, s, v: v < 0.35
isWhite = lambda h, s, v: s < 0.18 and v > 0.75
isGreen = lambda h, s, v: 80 < h < 160 and s > 0.3
save(fit512(gim), "Stage4_Gimbap", "Original")
set_ref((60, 50, 50)); save(fit512(recolor(gim, isDark, (200, 200, 200), region=ring)), "Stage4_Gimbap", "Wrong_F1")   # 검은 김 없음
set_ref((245, 245, 245)); save(fit512(recolor(gim, isWhite, (150, 90, 170), region=inner)), "Stage4_Gimbap", "Wrong_F2")  # 흰 밥 아님 (흑미)
set_ref(RED); save(fit512(recolor(gim, isRed, YELLOW, region=inner)), "Stage4_Gimbap", "Wrong_F3")                  # 빨강 재료 없음
set_ref(YELLOW); save(fit512(recolor(gim, isYellow, RED, region=inner)), "Stage4_Gimbap", "Wrong_F4")              # 노랑 재료 없음
set_ref(GREEN); save(fit512(recolor(gim, isGreen, BLUE, region=inner)), "Stage4_Gimbap", "Wrong_F5")               # 초록 재료 없음

# ── 5 친구 (그림 19) : 흰 바탕 지우고 ──
fr = Image.open("word/media/image19.png").convert("RGBA")
w, h = fr.size; p = fr.load()
seen = set(); q = deque([(x, 0) for x in range(w)] + [(x, h - 1) for x in range(w)] + [(0, y) for y in range(h)] + [(w - 1, y) for y in range(h)])
while q:
    x, y = q.popleft()
    if (x, y) in seen or not (0 <= x < w and 0 <= y < h): continue
    seen.add((x, y))
    r, g, b, a = p[x, y]
    if r + g + b < 720: continue
    p[x, y] = (r, g, b, 0)
    q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
fr = fr.crop(fr.getchannel("A").getbbox())
fh = fr.height
isHair = lambda h, s, v: v < 0.45 and s > 0.35 and (h < 40 or h > 340)
isShirt = lambda h, s, v: 10 < h < 32 and s > 0.4 and v > 0.8
isDenim = lambda h, s, v: 190 < h < 225 and s > 0.3
save(fit512(fr), "Stage5_Friend", "Original")
save(fit512(clear(fr, lambda x, y: y > fh * 0.42)), "Stage5_Friend", "Wrong_F1")                       # 사람 모습 아님 (머리만)
set_ref((80, 35, 30)); save(fit512(recolor(fr, isHair, (40, 40, 40))), "Stage5_Friend", "Wrong_F2")    # 갈색 머리 아님 (검정)
set_ref((240, 144, 96)); save(fit512(recolor(fr, isShirt, (110, 170, 90), region=lambda x, y: y < fh * 0.75)), "Stage5_Friend", "Wrong_F3")  # 주황 상의 아님 (초록)
set_ref((96, 168, 216)); save(fit512(recolor(fr, isDenim, (170, 110, 190))), "Stage5_Friend", "Wrong_F4")   # 파란 하의 아님 (보라)
print("done")

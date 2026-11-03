# 2단계 : 오려 낸 요소 다듬기 + 관찰 화면 요소 오려 내기
#   - 가장 큰 덩어리만 남기기(딸려 온 배경 선 제거)
#   - 그림에 박힌 글자 자리를 바탕색으로 지우기(게임이 글자를 직접 써요)
#   - 연필 버튼 = 지우개 버튼 틀 + 연필 아이콘, 고른 표시 = 시안의 분홍 고리
#   - 3·2·1 동그라미 : 시안의 코랄(지금)·크림(나머지) 두 가지 모양으로 숫자 바꿔 만들기
import os
from collections import deque
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import importlib.util

spec = importlib.util.spec_from_file_location("cut_ui", "cut_ui.py")
OUT = "cut"
SRC = "word/media"


def dark(p):
    r, g, b = p[:3]
    return r + g + b < 300 and r < 140


def largest_only(im):
    w, h = im.size
    a = im.getchannel("A").load()
    seen = [[False] * w for _ in range(h)]
    best = []
    for sy in range(h):
        for sx in range(w):
            if seen[sy][sx] or a[sx, sy] == 0:
                continue
            comp = []
            q = deque([(sx, sy)])
            seen[sy][sx] = True
            while q:
                x, y = q.popleft()
                comp.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and a[nx, ny] > 0:
                        seen[ny][nx] = True
                        q.append((nx, ny))
            if len(comp) > len(best):
                best = comp
    keep = Image.new("L", (w, h), 0)
    kp = keep.load()
    for x, y in best:
        kp[x, y] = 255
    out = im.copy()
    out.putalpha(ImageChops.multiply(im.getchannel("A"), keep))
    return out


def flood_cut(src, box, pad=0):
    im = Image.open(os.path.join(SRC, src)).convert("RGBA").crop(box)
    w, h = im.size
    px = im.load()
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
    return im


def fill_box(im, box, color):
    d = ImageDraw.Draw(im)
    d.rectangle(box, fill=color)


def sample(im, xy):
    return im.getpixel(xy)


boxes = eval(open(os.path.join(OUT, "boxes.txt")).read())

# 클립보드 : 딸려 온 선 제거 + 제목 글자 지우기 (반짝이는 남김)
cb = largest_only(Image.open(os.path.join(OUT, "Clipboard.png")))
ox, oy = boxes["Clipboard"][:2]
paper = sample(cb, (1000 - ox, 1500 - oy))
fill_box(cb, (470 - ox, 950 - oy, 1610 - ox, 1110 - oy), paper)
cb.save(os.path.join(OUT, "Clipboard.png"))

# 단계 알약 : 글자 지우기
def fill_pill(im, inner, inset=26, x_from=0):
    filled = im.copy()
    w, h = im.size
    ImageDraw.Draw(filled).rounded_rectangle((inset, inset, w - 1 - inset, h - 1 - inset), radius=h / 2 - inset, fill=inner)
    im.paste(filled.crop((x_from, 0, w, h)), (x_from, 0))
    return im

sp = Image.open(os.path.join(OUT, "StagePill.png"))
inner = sample(sp, (40, sp.height // 2))
fill_pill(sp, inner)
sp.save(os.path.join(OUT, "StagePill.png"))

# 간식 알약 : 글자 지우기 (복주머니는 남김)
sn = Image.open(os.path.join(OUT, "SnackPill.png"))
bx = boxes["SnackPill"]
inner = sample(sn, (sn.width - 40, sn.height // 2))
fill_pill(sn, inner, x_from=358 - bx[0])
sn.save(os.path.join(OUT, "SnackPill.png"))

# 굵기 알약 : 점 지우기 (점은 게임이 그려요)
sz = Image.open(os.path.join(OUT, "SizePill.png"))
inner = sample(sz, (30, sz.height // 2))
fill_pill(sz, inner, inset=24)
sz.save(os.path.join(OUT, "SizePill.png"))

# 연필 버튼 : 지우개 버튼 틀 + 연필 아이콘
er = Image.open(os.path.join(OUT, "EraserButton.png"))
eb = boxes["EraserButton"]
cream = sample(er, (er.width // 2, 40))
base = er.copy()
d = ImageDraw.Draw(base)
cx, cy = er.width / 2, er.height / 2
r = min(er.width, er.height) / 2 - 22
d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=cream)
base.save(os.path.join(OUT, "RoundButton.png"))           # 빈 동그라미 버튼 (모두 지우기 등에 씀)
pen = Image.open(os.path.join(OUT, "PencilButton.png"))    # 1단계에서 연필만 잘렸어요
pb = base.copy()
pb.alpha_composite(pen, (int(cx - pen.width / 2), int(cy - pen.height / 2)))
pb.save(os.path.join(OUT, "PencilButton.png"))
pen.save(os.path.join(OUT, "PencilIcon.png"))

# 고른 도구 표시 : 시안 연필 버튼의 코랄 고리 (지우개 버튼의 진한 테두리를 덮을 만큼)
D4 = 352 * 4
ringimg = Image.new("RGBA", (D4, D4), (0, 0, 0, 0))
rd = ImageDraw.Draw(ringimg)
rd.ellipse((0, 0, D4 - 1, D4 - 1), fill=(251, 126, 115, 255))
rd.ellipse((18 * 4, 18 * 4, D4 - 1 - 18 * 4, D4 - 1 - 18 * 4), fill=(0, 0, 0, 0))
ringimg.resize((352, 352), Image.LANCZOS).save(os.path.join(OUT, "SelectRing.png"))

# 향 (오른쪽 긴 향 + 받침)
inc = largest_only(flood_cut("image10.png", (1990, 740, 2170, 2580)))
bb = inc.getchannel("A").getbbox()
inc = inc.crop(bb)
inc.save(os.path.join(OUT, "Incense.png"))
boxes["Incense"] = (1990 + bb[0], 740 + bb[1], 1990 + bb[2], 740 + bb[3])

# ── 관찰 화면 (그림 9) ──
src9 = Image.open(os.path.join(SRC, "image9.png")).convert("RGBA")
# 쪽지 카드 + 테이프 + 위로 빼꼼한 도깨비 + 말풍선 = 한 덩어리로 (서로 겹쳐 있어서)
cbox = (300, 590, 2010, 3040)
card = flood_cut("image9.png", cbox)
card = largest_only(card)
cpaper = sample(src9, (500, 2900))
fill_box(card, (395 - cbox[0], 1255 - cbox[1], 1845 - cbox[0], 2955 - cbox[1]), cpaper)
cp = card.load()
row = 2000 - cbox[1]
left = next(x for x in range(card.width) if dark(cp[x, row]))
right = next(x for x in range(card.width - 1, 0, -1) if dark(cp[x, row]))
for y in range(1150 - cbox[1], card.height):
    for x in list(range(0, left - 2)) + list(range(right + 3, card.width)):
        r, g, b, a = cp[x, y]
        cp[x, y] = (r, g, b, 0)
card.save(os.path.join(OUT, "ObserveCard.png"))
boxes["ObserveCard"] = cbox
print("paper", cpaper)

# 3·2·1 동그라미
def circle_crop(box):
    c = flood_cut("image9.png", box)
    c = largest_only(c)
    bb = c.getchannel("A").getbbox()
    return c.crop(bb), (box[0] + bb[0], box[1] + bb[1])

def coral_circle(box):
    # 코랄 동그라미는 진한 테두리가 없어서, 코랄 색 범위로 동그라미를 찾아 둥글게 잘라요
    im = src9.crop(box)
    p = im.load(); w, h = im.size
    xs = [x for y in range(h) for x in range(w) if p[x, y][0] > 220 and p[x, y][1] < 160 and p[x, y][2] < 160]
    ys = [y for y in range(h) for x in range(w) if p[x, y][0] > 220 and p[x, y][1] < 160 and p[x, y][2] < 160]
    bb = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
    c = im.crop(bb).convert("RGBA")
    m = Image.new("L", (c.width * 4, c.height * 4), 0)
    ImageDraw.Draw(m).ellipse((0, 0, c.width * 4 - 1, c.height * 4 - 1), fill=255)
    c.putalpha(m.resize(c.size, Image.LANCZOS))
    return c, (box[0] + bb[0], box[1] + bb[1])

on3, on_pos = coral_circle((528, 3060, 905, 3445))
off2, off2_pos = circle_crop((940, 3060, 1320, 3440))
off1, off1_pos = circle_crop((1370, 3060, 1745, 3440))
boxes["Count3"] = on_pos + (on_pos[0] + on3.width, on_pos[1] + on3.height)
boxes["Count2"] = off2_pos + (off2_pos[0] + off2.width, off2_pos[1] + off2.height)
boxes["Count1"] = off1_pos + (off1_pos[0] + off1.width, off1_pos[1] + off1.height)

def glyph_mask(img, mode):
    # mode "black" : 크림 동그라미 속 검은 숫자 / "white" : 코랄 동그라미 속 흰 숫자(+진한 테두리)
    w, h = img.size
    m = Image.new("L", (w, h), 0)
    mp = m.load(); p = img.load()
    cx, cy, rr = w / 2, h / 2, min(w, h) / 2 * 0.72
    for y in range(h):
        for x in range(w):
            if (x - cx) ** 2 + (y - cy) ** 2 > rr * rr:
                continue
            r, g, b, a = p[x, y]
            if mode == "black" and r + g + b < 200:
                mp[x, y] = 255
            if mode == "white" and r > 235 and g > 235 and b > 235:
                mp[x, y] = 255
    return m

def blank(img, fillc):
    out = img.copy()
    d = ImageDraw.Draw(out)
    w, h = out.size
    rr = min(w, h) / 2 - 28
    d.ellipse((w / 2 - rr, h / 2 - rr, w / 2 + rr, h / 2 + rr), fill=fillc)
    return out

coral = sample(src9, (650, 3150))
creamc = sample(src9, (990, 3266))
def blank_r(img, fillc, k):
    out = img.copy(); d = ImageDraw.Draw(out); w, h = out.size; rr = min(w, h) / 2 * k
    d.ellipse((w / 2 - rr, h / 2 - rr, w / 2 + rr, h / 2 + rr), fill=fillc)
    return out
on_blank = blank_r(on3, coral, 0.74)
off_blank = blank_r(off2, creamc, 0.80)
on_blank.save(os.path.join(OUT, "CountOn.png"))
off_blank.save(os.path.join(OUT, "CountOff.png"))
# 숫자 모양(글자 마스크)도 시안에서 : 3 은 코랄(흰 글자), 2·1 은 크림(검은 글자)
def glyph_png(m, name):
    g = Image.new("RGBA", m.size, (255, 255, 255, 0))
    g.putalpha(m.filter(ImageFilter.GaussianBlur(0.6)))
    g.save(os.path.join(OUT, name))
glyph_png(glyph_mask(on3, "white"), "Glyph3.png")
glyph_png(glyph_mask(off2, "black"), "Glyph2.png")
glyph_png(glyph_mask(off1, "black"), "Glyph1.png")
print("coral", coral, "cream", creamc)

def color_cut(box, bg, tol=45):
    im = src9.crop(box).copy(); p = im.load(); w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = p[x, y]
            if abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2]) < tol:
                p[x, y] = (r, g, b, 0)
    return im
# 3 옆 노란 반짝이
spark = color_cut((400, 3040, 540, 3200), sample(src9, (405, 3045)))
bb = spark.getchannel("A").getbbox()
if bb:
    spark.crop(bb).save(os.path.join(OUT, "CountSparkle.png"))
    boxes["CountSparkle"] = (400 + bb[0], 3040 + bb[1], 400 + bb[2], 3040 + bb[3])

# 아래 도깨비불 장식
fl = color_cut((700, 3600, 1540, 3850), sample(src9, (720, 3610)))
bb = fl.getchannel("A").getbbox()
fl.crop(bb).save(os.path.join(OUT, "FlameDeco.png"))
boxes["FlameDeco"] = (760 + bb[0], 3600 + bb[1], 760 + bb[2], 3600 + bb[3])

open(os.path.join(OUT, "boxes.txt"), "w").write(repr(boxes))
print(boxes)

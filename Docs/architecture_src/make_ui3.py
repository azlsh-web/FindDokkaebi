# 그리기·관찰 화면 UX 참고 이미지 스타일 스프라이트 (진갈색 굵은 테두리 + 크림/코랄/살구)
import sys, os, math
from PIL import Image, ImageDraw

SPR = sys.argv[1]
OUT = os.path.join(SPR, "UI")
OUTLINE = (74, 43, 22, 255)
CREAM = (251, 245, 231, 255)
PAPER = (255, 252, 245, 255)
CORAL = (237, 141, 128, 255)
CLIP = (242, 172, 150, 255)
WOOD = (236, 190, 132, 255)
YELLOW = (250, 210, 90, 255)
PINK = (244, 186, 205, 255)
K = 4


def canvas(w, h):
    im = Image.new("RGBA", (w * K, h * K), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def save(im, w, h, name):
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name))


def S(*v):
    return [x * K for x in v]


# 클립보드 판 (9-slice) : 살구색 나무 판 + 안쪽 크림 종이
w, h = 240, 240
im, d = canvas(w, h)
d.rounded_rectangle(S(0, 0, w - 1, h - 1), radius=44 * K, fill=OUTLINE)
d.rounded_rectangle(S(6, 6, w - 7, h - 7), radius=38 * K, fill=WOOD)
d.rounded_rectangle(S(26, 26, w - 27, h - 27), radius=30 * K, fill=OUTLINE)
d.rounded_rectangle(S(31, 31, w - 32, h - 32), radius=25 * K, fill=PAPER)
save(im, w, h, "Clipboard.png")

# 클립 (판 위쪽 집게) : 코랄 둥근 네모 + 위 볼록 + 구멍
w, h = 280, 130
im, d = canvas(w, h)
d.rounded_rectangle(S(0, 46, w - 1, h - 1), radius=30 * K, fill=OUTLINE)
d.ellipse(S(w / 2 - 52, 0, w / 2 + 52, 104), fill=OUTLINE)
d.rounded_rectangle(S(6, 52, w - 7, h - 7), radius=24 * K, fill=CLIP)
d.ellipse(S(w / 2 - 46, 6, w / 2 + 46, 98), fill=CLIP)
d.ellipse(S(w / 2 - 17, 26, w / 2 + 17, 60), fill=OUTLINE)
d.ellipse(S(w / 2 - 12, 31, w / 2 + 12, 55), fill=PAPER)
save(im, w, h, "Clip.png")

# 팔레트 (물감 판) : 울퉁불퉁한 크림 판 + 왼쪽 엄지 구멍
w, h = 680, 190
im, d = canvas(w, h)
pts = []
for i in range(160):
    a = 2 * math.pi * i / 160
    rx, ry = (w / 2 - 28), (h / 2 - 14)
    wob = 1 + 0.035 * math.sin(3 * a + 0.6) + 0.02 * math.sin(5 * a + 1.3)
    c, sn = math.cos(a), math.sin(a)
    # 둥근 네모에 가까운 타원 (|c|^0.7) → 물감 판처럼 통통하게
    x = math.copysign(abs(c) ** 0.7, c) * rx * wob
    y = math.copysign(abs(sn) ** 0.85, sn) * ry * wob * 0.96
    pts.append(((w / 2 + x) * K, (h / 2 + y) * K))
d.polygon(pts, fill=OUTLINE)
inner = [((x - w * K / 2) * (1 - 12 / w) + w * K / 2, (y - h * K / 2) * (1 - 12 / h) + h * K / 2) for x, y in pts]
d.polygon(inner, fill=CREAM)
d.ellipse(S(62, 104, 128, 138), fill=OUTLINE)
d.ellipse(S(67, 109, 123, 133), fill=WOOD)
save(im, w, h, "PaletteBoard.png")

# 연필 아이콘
w = h = 128
im, d = canvas(w, h)
def rot(px, py, ang, cx=64, cy=64):
    a = math.radians(ang)
    x, y = px - cx, py - cy
    return ((cx + x * math.cos(a) - y * math.sin(a)) * K, (cy + x * math.sin(a) + y * math.cos(a)) * K)
def poly(p, fill):
    d.polygon([rot(x, y, -45) for x, y in p], fill=fill)
ol = 5
poly([(20 - ol, 50 - ol), (96, 50 - ol), (118 + ol, 64), (96, 78 + ol), (20 - ol, 78 + ol)], OUTLINE)
poly([(20, 50), (34, 50), (34, 78), (20, 78)], PINK)
poly([(34, 50), (40, 50), (40, 78), (34, 78)], (190, 190, 200, 255))
poly([(40, 50), (94, 50), (94, 78), (40, 78)], YELLOW)
poly([(94, 50), (116, 64), (94, 78)], (240, 205, 160, 255))
poly([(108, 59), (117, 64), (108, 69)], OUTLINE)
poly([(40, 62), (94, 62), (94, 66), (40, 66)], (235, 180, 60, 255))
save(im, w, h, "Pencil.png")

# 지우개 아이콘 (분홍 + 흰 띠)
im, d = canvas(w, h)
poly([(18 - ol, 44 - ol), (110 + ol, 44 - ol), (110 + ol, 84 + ol), (18 - ol, 84 + ol)], OUTLINE)
poly([(18, 44), (70, 44), (70, 84), (18, 84)], (252, 250, 250, 255))
poly([(70, 44), (110, 44), (110, 84), (70, 84)], PINK)
poly([(66, 44), (72, 44), (72, 84), (66, 84)], OUTLINE)
save(im, w, h, "EraserPink.png")

# 색 섞기 아이콘 : 빨강·파랑 동그라미 겹침
w, h = 160, 110
im, d = canvas(w, h)
d.ellipse(S(6, 6, 96, 96), fill=OUTLINE)
d.ellipse(S(11, 11, 91, 91), fill=(236, 120, 110, 255))
d.ellipse(S(62, 6, 152, 96), fill=OUTLINE)
d.ellipse(S(67, 11, 147, 91), fill=(110, 160, 235, 255))
for x0 in (30, 86):
    d.ellipse(S(x0, 22, x0 + 18, 36), fill=(255, 255, 255, 170))
save(im, w, h, "MixIcon.png")

# 일시정지 아이콘
w = h = 128
im, d = canvas(w, h)
d.rounded_rectangle(S(38, 30, 56, 98), radius=6 * K, fill=OUTLINE)
d.rounded_rectangle(S(72, 30, 90, 98), radius=6 * K, fill=OUTLINE)
save(im, w, h, "Pause.png")

# 말풍선 (꼬리가 왼쪽 아래)
w, h = 380, 230
im, d = canvas(w, h)
d.polygon(S(40, 150, 16, 222, 110, 160), fill=OUTLINE)
d.ellipse(S(20, 0, w - 1, 190), fill=OUTLINE)
d.ellipse(S(26, 6, w - 7, 184), fill=(255, 255, 255, 255))
d.polygon(S(48, 146, 27, 208, 104, 154), fill=(255, 255, 255, 255))
save(im, w, h, "Bubble.png")

# 테이프 (반투명 노랑, 줄무늬)
w, h = 150, 54
im, d = canvas(w, h)
d.rectangle(S(0, 0, w - 1, h - 1), fill=(246, 214, 120, 235))
for x in range(0, w, 14):
    d.line(S(x, 0, x + 8, h), fill=(236, 196, 96, 235), width=3 * K)
d.rectangle(S(0, 0, w - 1, h - 1), outline=(214, 170, 80, 255), width=2 * K)
save(im, w, h, "Tape.png")

# 노란 강조 선 (제목 양옆 "반짝")
w, h = 60, 60
im, d = canvas(w, h)
for (x0, y0, x1, y1) in [(10, 14, 30, 30), (6, 42, 32, 40)]:
    d.line(S(x0, y0, x1, y1), fill=(244, 190, 60, 255), width=8 * K)
    d.ellipse(S(x0 - 4, y0 - 4, x0 + 4, y0 + 4), fill=(244, 190, 60, 255))
    d.ellipse(S(x1 - 4, y1 - 4, x1 + 4, y1 + 4), fill=(244, 190, 60, 255))
save(im, w, h, "Sparkle.png")

# 도깨비불 (HUD 로고 · 관찰 화면 아래 장식)
w, h = 96, 128
im, d = canvas(w, h)
def drop(cx, top, bottom, r, fill):
    d.ellipse(S(cx - r, bottom - 2 * r, cx + r, bottom), fill=fill)
    d.polygon(S(cx - r * 0.93, bottom - r * 1.35, cx, top, cx + r * 0.93, bottom - r * 1.35), fill=fill)
drop(48, 4, 124, 44, OUTLINE)
drop(48, 11, 118, 38, (244, 190, 80, 255))
drop(48, 52, 112, 22, (226, 120, 50, 255))
save(im, w, h, "Flame.png")

# 도깨비 얼굴 (제출 버튼) · 도깨비 위쪽 (관찰 화면에서 카드 위로 빼꼼)
dok = Image.open(os.path.join(SPR, "Characters", "Dokkaebi.png")).convert("RGBA")
# 왼쪽에는 방망이가 겹쳐 있어서, 오른쪽 반을 뒤집어 붙여 대칭 얼굴로 만들어요
CX = 288                                   # 얼굴 가운데 x
right = dok.crop((CX, 0, 512, 512))
sym = Image.new("RGBA", (2 * right.width, 512), (0, 0, 0, 0))
sym.paste(right.transpose(Image.FLIP_LEFT_RIGHT), (0, 0))
sym.paste(right, (right.width, 0))
bb = sym.getbbox()
sym.crop((bb[0], 12, bb[2], 312)).save(os.path.join(OUT, "DokkaebiFace.png"))
sym.crop((bb[0], 12, bb[2], 360)).save(os.path.join(OUT, "DokkaebiPeek.png"))
print("ui3 done")

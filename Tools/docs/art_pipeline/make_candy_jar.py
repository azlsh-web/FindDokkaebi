# 코드로 그리는 임시 그림 (아트팀 그림이 오면 같은 이름으로 바꿔 넣으면 돼요)
#   - 이야기 종료의 사탕 통
#   - 그리기 화면의 책상 (Draw_Desk : 나뭇결 판) · 맞춤 버튼 아이콘 (Draw_FitIcon)
#   - 그리기 도구 아이콘 (동그라미 없이 그림만) : 붓(Draw_Brush + 색을 입히는 붓 끝 Draw_BrushTip), 지우개, ←, →, 휴지통
#   - 챕터 선택의 자물쇠 (Chapter_Lock : 아직 준비 중인 챕터)
#   - 도깨비불 (Magnifier_DokkaebiFire : 향 불꽃 그림을 푸른빛으로 바꾼 것. 1단계 시범 때 돋보기 둘레를 돌아요)
#   - 돋보기 렌즈 둘레의 남은 시간 띠 (Magnifier_TimeRing : 돋보기 테두리의 크림색 띠를 오린 흰 고리. 게임에서 색을 입히고 시계처럼 줄여요)
#   python Tools/docs/art_pipeline/make_candy_jar.py
# 결과 (Assets/Sprites/UI)
#   Ending_CandyJarBack.png  : 유리 통의 뒤쪽 (사탕보다 뒤에 그려요)
#   Ending_CandyJarFront.png : 테두리 · 뚜껑 · 반짝이는 줄 (사탕보다 앞에 그려요)
# 게임의 다른 그림처럼 진갈색 굵은 테두리와 크림색을 써요. 4배 크게 그린 뒤 줄여서 테두리를 매끈하게 해요.
import os
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Sprites", "UI")

W, H = 372, 480          # 게임에서 186 x 240 (M 62 x 80) 으로 보여요
S = 4                    # 4배로 그려서 줄이기
OUTLINE = (74, 43, 22, 255)
GLASS = (226, 242, 247, 150)
LID = (242, 176, 98, 255)
LID_DARK = (214, 140, 70, 255)
SHINE = (255, 255, 255, 200)
LINE = 9 * S

# 통 몸체 : 둥근 네모. 위쪽은 목이 살짝 좁아요
body = (22 * S, 118 * S, (W - 22) * S, (H - 12) * S)
neck = (64 * S, 82 * S, (W - 64) * S, 136 * S)
lid = (44 * S, 18 * S, (W - 44) * S, 92 * S)


def back():
    im = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(body, radius=70 * S, fill=GLASS)
    d.rounded_rectangle(neck, radius=18 * S, fill=GLASS)
    return im.resize((W, H), Image.LANCZOS)


def front():
    im = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(body, radius=70 * S, outline=OUTLINE, width=LINE)
    d.rounded_rectangle(neck, radius=18 * S, outline=OUTLINE, width=LINE)
    # 목과 몸이 만나는 곳의 테두리를 유리색으로 덮어 한 덩어리처럼
    d.rectangle((neck[0] + LINE, 108 * S, neck[2] - LINE, 140 * S), fill=(0, 0, 0, 0))
    # 뚜껑 : 둥근 덮개 + 아래 띠
    d.rounded_rectangle(lid, radius=30 * S, fill=LID, outline=OUTLINE, width=LINE)
    d.rounded_rectangle((lid[0] + 30 * S, lid[1] + 14 * S, lid[2] - 30 * S, lid[1] + 34 * S), radius=10 * S, fill=LID_DARK)
    # 유리 반짝임 : 왼쪽 위 긴 줄과 작은 점
    d.rounded_rectangle((54 * S, 160 * S, 76 * S, 300 * S), radius=11 * S, fill=SHINE)
    d.ellipse((54 * S, 318 * S, 76 * S, 340 * S), fill=SHINE)
    return im.resize((W, H), Image.LANCZOS)


def time_ring():
    # 돋보기 테두리(Magnifier_Frame)의 크림색 띠를 그대로 오려 낸 고리예요. 그래서 게임에서 테두리에 꼭 맞게 겹쳐요.
    # 렌즈 가운데 (169, 174) 를 그림 가운데로 둔 340 x 340 (원형 채우기가 그림 가운데를 기준으로 돌아서요).
    frame = Image.open(os.path.join(ROOT, "Assets", "Sprites", "UI", "Magnifier_Frame.png")).convert("RGBA")
    cx, cy, half = 169, 174, 170
    cream = (252, 221, 162)
    im = Image.new("RGBA", (half * 2, half * 2), (0, 0, 0, 0))
    px = im.load()
    for y in range(half * 2):
        for x in range(half * 2):
            fx, fy = cx - half + x, cy - half + y
            if not (0 <= fx < frame.width and 0 <= fy < frame.height):
                continue
            d2 = (fx - cx) ** 2 + (fy - cy) ** 2
            if d2 < 118 ** 2 or d2 > 157 ** 2:      # 렌즈 구멍과 손잡이 이음새는 빼요
                continue
            r, g, b, a = frame.getpixel((fx, fy))
            dist = ((r - cream[0]) ** 2 + (g - cream[1]) ** 2 + (b - cream[2]) ** 2) ** 0.5
            k = max(0.0, min(1.0, (70 - dist) / 45))   # 크림색에 가까울수록 진하게 (가장자리는 부드럽게)
            if k > 0:
                px[x, y] = (255, 255, 255, int(255 * k * a / 255))
    return im


def draw_desk():
    import math, random
    random.seed(7)
    w, h = 1240, 1650
    im = Image.new("RGB", (w, h), (201, 150, 94))     # RGB 위에 반투명 줄을 섞어 그려요
    d = ImageDraw.Draw(im, "RGBA")   # 반투명 줄을 판 위에 섞어 그려요
    plank = 330
    for top in range(0, h, plank):
        shade = random.randint(-10, 10)
        d.rectangle((0, top, w, top + plank), fill=(201 + shade, 150 + shade, 94 + shade // 2))
        for k in range(12):                      # 나뭇결 : 살짝 물결치는 옅은 줄
            y0 = top + random.uniform(10, plank - 10)
            amp, freq, ph = random.uniform(2, 9), random.uniform(0.004, 0.012), random.uniform(0, 6.3)
            col = (180, 130, 78, random.randint(40, 80))
            pts = [(x, y0 + amp * math.sin(x * freq + ph)) for x in range(0, w + 20, 20)]
            d.line(pts, fill=col, width=random.choice([3, 4]))
        d.line((0, top, w, top), fill=(150, 104, 60), width=5)        # 판자 사이 틈
    return im.convert("RGBA")


def fit_icon():
    size = 128
    im = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m, l, t = 22 * S, 38 * S, 12 * S
    e = size * S - m
    for (x, y, dx, dy) in [(m, m, 1, 1), (e, m, -1, 1), (m, e, 1, -1), (e, e, -1, -1)]:
        d.line((x, y, x + dx * l, y), fill=OUTLINE, width=t)
        d.line((x, y, x, y + dy * l), fill=OUTLINE, width=t)
        d.ellipse((x - t // 2, y - t // 2, x + t // 2, y + t // 2), fill=OUTLINE)
    return im.resize((size, size), Image.LANCZOS)


def _brush_layers():
    """세로로 세운 붓을 그린 뒤 붓 끝이 왼쪽 아래를 보게 기울여요. 반환 : (붓 몸통 + 붓 끝 테두리, 붓 끝 안쪽 흰색)"""
    n = 256 * S
    body = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    tip = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    db, dt = ImageDraw.Draw(body), ImageDraw.Draw(tip)
    cx = 128 * S
    import math
    right = []
    for k in range(0, 41):                       # 붓 끝 : 위가 뾰족하고 아래로 갈수록 둥글게 부풀어요
        y = 12 + k * (100 - 12) / 40
        t = (y - 12) / (100 - 12)
        half = 34 * math.sin(min(1.0, t / 0.72) * math.pi / 2) ** 0.8 - (6 * (t - 0.72) / 0.28 if t > 0.72 else 0)
        right.append((cx + half * S, y * S))
    tip_poly = right + [(2 * cx - x, y) for (x, y) in reversed(right)]
    dt.polygon(tip_poly, fill=(255, 255, 255, 255))
    db.line(tip_poly + [tip_poly[0]], fill=OUTLINE, width=9 * S, joint="curve")
    db.rounded_rectangle((cx - 30 * S, 98 * S, cx + 30 * S, 130 * S), radius=6 * S, fill=(196, 200, 206, 255), outline=OUTLINE, width=9 * S)
    db.rounded_rectangle((cx - 22 * S, 128 * S, cx + 22 * S, 244 * S), radius=20 * S, fill=(214, 140, 70, 255), outline=OUTLINE, width=9 * S)
    db.ellipse((cx - 8 * S, 214 * S, cx + 8 * S, 230 * S), fill=OUTLINE)
    return [im.rotate(140, resample=Image.BICUBIC).resize((256, 256), Image.LANCZOS) for im in (body, tip)]   # 붓 끝이 왼쪽 아래


def brush():
    return _brush_layers()[0]


def brush_tip():
    return _brush_layers()[1]


def eraser_icon():
    n = 256 * S
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((40 * S, 86 * S, 216 * S, 170 * S), radius=18 * S, fill=(244, 166, 192, 255), outline=OUTLINE, width=9 * S)
    d.rectangle((120 * S, 90 * S, 150 * S, 166 * S), fill=(120, 170, 230, 255))
    d.line((120 * S, 90 * S, 120 * S, 166 * S), fill=OUTLINE, width=6 * S)
    d.line((150 * S, 90 * S, 150 * S, 166 * S), fill=OUTLINE, width=6 * S)
    return im.rotate(30, resample=Image.BICUBIC).resize((256, 256), Image.LANCZOS)


def arrow_icon(left):
    """되돌리기(↶) · 다시 하기(↷) : 위로 휘었다가 내려오는 화살표"""
    n = 256 * S
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    w = 26 * S
    box = (60 * S, 70 * S, 216 * S, 226 * S)                 # 오른쪽 아래에서 시작해 위를 돌아 왼쪽으로
    d.arc(box, start=200, end=360 + 60, fill=OUTLINE, width=w)
    end = (82 * S, 102 * S)                                   # 왼쪽 끝 : 화살촉
    d.polygon([(28 * S, 120 * S), (112 * S, 60 * S), (118 * S, 150 * S)], fill=OUTLINE)
    cx, cy, r = 138 * S, 148 * S, 78 * S - w // 2
    import math
    a = math.radians(60)
    tail = (cx + r * math.cos(a), cy + r * math.sin(a))
    d.ellipse((tail[0] - w / 2, tail[1] - w / 2, tail[0] + w / 2, tail[1] + w / 2), fill=OUTLINE)
    im = im.resize((256, 256), Image.LANCZOS)
    return im if left else im.transpose(Image.FLIP_LEFT_RIGHT)


def water_cup():
    """붓 씻는 물통 : 섞던 물감 비우기"""
    n = 256 * S
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.polygon([(52 * S, 70 * S), (204 * S, 70 * S), (186 * S, 226 * S), (70 * S, 226 * S)], fill=(214, 236, 248, 255))
    d.polygon([(60 * S, 132 * S), (196 * S, 132 * S), (186 * S, 226 * S), (70 * S, 226 * S)], fill=(150, 196, 230, 255))
    d.line([(52 * S, 70 * S), (70 * S, 226 * S), (186 * S, 226 * S), (204 * S, 70 * S)], fill=OUTLINE, width=11 * S, joint="curve")
    d.ellipse((46 * S, 58 * S, 210 * S, 84 * S), outline=OUTLINE, width=10 * S)
    for x, y in ((104, 168), (140, 186), (124, 150)):
        d.ellipse(((x - 8) * S, (y - 8) * S, (x + 8) * S, (y + 8) * S), fill=(255, 255, 255, 220))
    return im.resize((256, 256), Image.LANCZOS)


def trash_icon():
    n = 256 * S
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.polygon([(66 * S, 92 * S), (190 * S, 92 * S), (178 * S, 228 * S), (78 * S, 228 * S)], fill=(251, 245, 231, 255), outline=OUTLINE)
    d.line([(66 * S, 92 * S), (190 * S, 92 * S), (178 * S, 228 * S), (78 * S, 228 * S), (66 * S, 92 * S)], fill=OUTLINE, width=10 * S, joint="curve")
    d.rounded_rectangle((48 * S, 62 * S, 208 * S, 86 * S), radius=10 * S, fill=OUTLINE)
    d.rounded_rectangle((104 * S, 36 * S, 152 * S, 66 * S), radius=10 * S, outline=OUTLINE, width=10 * S)
    for x in (104, 128, 152):
        d.line((x * S, 118 * S, x * S, 202 * S), fill=OUTLINE, width=9 * S)
    return im.resize((256, 256), Image.LANCZOS)


def chapter_lock():
    w, h = 200, 240
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # 고리 : 진갈색 굵은 선 위에 놋쇠색 선
    d.arc((44 * S, 14 * S, 156 * S, 140 * S), 180, 360, fill=OUTLINE, width=34 * S)
    d.arc((52 * S, 22 * S, 148 * S, 132 * S), 180, 360, fill=(214, 168, 92, 255), width=18 * S)
    d.rectangle((44 * S, 76 * S, 78 * S, 112 * S), fill=OUTLINE)
    d.rectangle((122 * S, 76 * S, 156 * S, 112 * S), fill=OUTLINE)
    d.rectangle((52 * S, 76 * S, 70 * S, 112 * S), fill=(214, 168, 92, 255))
    d.rectangle((130 * S, 76 * S, 148 * S, 112 * S), fill=(214, 168, 92, 255))
    # 몸통 : 둥근 네모 + 열쇠 구멍
    d.rounded_rectangle((14 * S, 100 * S, 186 * S, 230 * S), radius=28 * S, fill=LID, outline=OUTLINE, width=LINE)
    d.ellipse((84 * S, 132 * S, 116 * S, 164 * S), fill=OUTLINE)
    d.polygon([(92 * S, 152 * S), (108 * S, 152 * S), (114 * S, 198 * S), (86 * S, 198 * S)], fill=OUTLINE)
    return im.resize((w, h), Image.LANCZOS)


def dokkaebi_fire():
    import colorsys
    flame = Image.open(os.path.join(ROOT, "Assets", "Sprites", "UI", "Incense_Flame.png")).convert("RGBA")
    out = Image.new("RGBA", flame.size)
    src, dst = flame.load(), out.load()
    for y in range(flame.height):
        for x in range(flame.width):
            r, g, b, a = src[x, y]
            h, l, s_ = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            if s_ > 0.25 and 0.3 < l < 0.92:             # 진갈색 테두리 · 흰 속은 그대로 두고 불꽃 색만
                h = (h + 0.5) % 1.0                      # 주황 → 푸른빛
                s_ = min(1.0, s_ * 0.9)
            nr, ng, nb = colorsys.hls_to_rgb(h, l, s_)
            dst[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1:
        OUT = sys.argv[1]       # 미리 보기 : 다른 폴더에 저장
    back().save(os.path.join(OUT, "Ending_CandyJarBack.png"))
    front().save(os.path.join(OUT, "Ending_CandyJarFront.png"))
    time_ring().save(os.path.join(OUT, "Magnifier_TimeRing.png"))
    dokkaebi_fire().save(os.path.join(OUT, "Magnifier_DokkaebiFire.png"))
    chapter_lock().save(os.path.join(OUT, "Chapter_Lock.png"))
    draw_desk().save(os.path.join(OUT, "Draw_Desk.png"))
    fit_icon().save(os.path.join(OUT, "Draw_FitIcon.png"))
    brush().save(os.path.join(OUT, "Draw_Brush.png"))
    brush_tip().save(os.path.join(OUT, "Draw_BrushTip.png"))
    eraser_icon().save(os.path.join(OUT, "Draw_EraserIcon.png"))
    arrow_icon(True).save(os.path.join(OUT, "Draw_UndoIcon.png"))
    arrow_icon(False).save(os.path.join(OUT, "Draw_RedoIcon.png"))
    trash_icon().save(os.path.join(OUT, "Draw_TrashIcon.png"))
    water_cup().save(os.path.join(OUT, "Draw_WaterCup.png"))
    print("saved", OUT)

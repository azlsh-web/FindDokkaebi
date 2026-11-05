# 연출 개편에 쓰는 그림을 만들어요. (아트팀 그림이 오면 같은 파일 이름으로 바꿔 끼워요)
#   python Tools/docs/art_pipeline/make_game_feel_art.py
# 원본은 ArtSource/원본/미사용 에 보관해 두고, 여기서 읽어서 Assets/Sprites 에 써요.
import math
import os
import random
import shutil
from PIL import Image, ImageChops, ImageDraw

S = 'Assets/Sprites/'
KEEP = 'ArtSource/원본/미사용/'
os.makedirs(KEEP, exist_ok=True)


def keep(rel):
    """처음 실행할 때 원본을 보관하고, 보관한 원본 경로를 돌려줘요."""
    src, dst = S + rel, KEEP + os.path.basename(rel)
    if not os.path.exists(dst):
        shutil.copy(src, dst)
    return dst


def retire(rel):
    """이제 안 쓰는 그림을 보관함으로 옮겨요."""
    src, dst = S + rel, KEEP + os.path.basename(rel)
    if os.path.exists(src):
        if not os.path.exists(dst):
            shutil.move(src, dst)
        else:
            os.remove(src)
    if os.path.exists(src + '.meta'):
        os.remove(src + '.meta')


# ── 로고 : 그림에 박힌 "기억 한 조각, 그림 한 장" 을 지워요 ──
logo = Image.open(keep('UI/Title_Logo.png')).convert('RGBA')
px = logo.load()
cream = px[800, 1135]
for y in range(1014, 1120):
    for x in range(360, 1300):
        px[x, y] = cream
logo.save(S + 'UI/Title_Logo.png', optimize=True)

# ── 빈손 도깨비 : 아트팀에게 받은 그림. 대사 상자 얼굴은 여기서 얼굴만 잘라요 ──
empty = Image.open('ArtSource/원본/받은그림/Dokkaebi_EmptyHand.png').convert('RGBA')
empty.save(S + 'Characters/Char_Dokkaebi_NoBat.png', optimize=True)
face = Image.new('RGBA', (660, 560), (0, 0, 0, 0))
face.alpha_composite(empty.crop((180, 40, 840, 548)), (0, 26))
face.paste((0, 0, 0, 0), (0, 500, 110, 560))     # 왼쪽 아래에 들어온 팔 끝 지우기
face.save(S + 'Characters/Char_DokkaebiFace_Normal.png', optimize=True)

# ── 도구 버튼 : 아이콘의 긴 변을 원 지름의 56% 로 똑같이 맞춰요 ──
base = Image.open(keep('UI/Draw_ClearButton.png')).convert('RGBA')
W, H = base.size
cx, cy, R = W / 2, H / 2, min(W, H) / 2
inner = Image.new('L', base.size, 0)
ImageDraw.Draw(inner).ellipse((cx - R * .84, cy - R * .84, cx + R * .84, cy + R * .84), fill=255)


def glyph(path):
    b = Image.open(path).convert('RGBA')
    diff = ImageChops.multiply(ImageChops.difference(b.convert('RGB'), base.convert('RGB')).convert('L'), inner)
    alpha = diff.point(lambda v: 255 if v > 6 else 0)
    b.putalpha(alpha)
    return b.crop(alpha.getbbox())


def place(g, rel):
    k = 0.56 * 2 * R / max(g.size)
    g2 = g.resize((max(1, round(g.width * k)), max(1, round(g.height * k))), Image.LANCZOS)
    out = base.copy()
    out.alpha_composite(g2, (round(cx - g2.width / 2), round(cy - g2.height / 2)))
    out.save(S + rel, optimize=True)


place(glyph(keep('UI/Draw_PencilButton.png')), 'UI/Draw_PencilButton.png')
place(glyph(keep('UI/Draw_EraserButton.png')), 'UI/Draw_EraserButton.png')
place(glyph(keep('UI/Draw_UndoButton.png')), 'UI/Draw_UndoButton.png')
trash = Image.open(keep('UI/Draw_TrashIcon.png')).convert('RGBA')
place(trash.crop(trash.getbbox()), 'UI/Draw_ClearButton.png')

# ── 작은 그림들 : 크게 그린 뒤 줄여서 선을 매끈하게 ──
SS = 4
BROWN, WHITE = (74, 43, 22, 255), (255, 255, 255, 255)


def icon(size, draw):
    im = Image.new('RGBA', (size * SS, size * SS), (0, 0, 0, 0))
    draw(ImageDraw.Draw(im), size * SS)
    return im.resize((size, size), Image.LANCZOS)


def pencil(dr, n):
    u = n / 24
    w = int(2.4 * u)
    dr.line([(4 * u, 20 * u), (5 * u, 15 * u), (16 * u, 4 * u), (20 * u, 8 * u), (9 * u, 19 * u), (4 * u, 20 * u)], fill=WHITE, width=w, joint='curve')
    dr.line([(13.5 * u, 6.5 * u), (17.5 * u, 10.5 * u)], fill=WHITE, width=w)


def flag(dr, n):
    u = n / 24
    w = int(2.2 * u)
    dr.line([(6 * u, 21 * u), (6 * u, 4 * u)], fill=BROWN, width=w)
    top = [(6 + 12 * t, 4 + 2.2 * math.sin(t * 6.28)) for t in [i / 16 for i in range(17)]]
    bottom = [(p[0], p[1] + 9) for p in reversed(top)]
    dr.line([(x * u, y * u) for x, y in top + bottom + top[:1]], fill=BROWN, width=w, joint='curve')


icon(192, pencil).save(S + 'UI/Result_IconRetry.png', optimize=True)
icon(192, flag).save(S + 'UI/Result_IconGiveUp.png', optimize=True)


def arrow(dr, n):
    dr.polygon([(n * .08, n * .2), (n * .92, n * .2), (n * .5, n * .85)], fill=BROWN)


icon(96, arrow).save(S + 'UI/Common_NextArrow.png', optimize=True)


def circle_mark(dr, n):
    u = n / 40
    pts = []
    rng = random.Random(3)
    for i in range(0, 90):
        t = i / 89 * 2 * math.pi * 1.04 - math.pi / 2
        r = 15.5 + rng.uniform(-.25, .25) + i * .012
        pts.append(((20 + r * math.cos(t)) * u, (20 + r * math.sin(t)) * u))
    dr.line(pts, fill=(46, 158, 79, 255), width=int(3 * u), joint='curve')


def slash_mark(dr, n):
    u = n / 40
    dr.line([(33 * u, 6 * u), (7 * u, 34 * u)], fill=(216, 53, 42, 255), width=int(4 * u))
    for p in ((33, 6), (7, 34)):
        r = 2 * u
        dr.ellipse((p[0] * u - r, p[1] * u - r, p[0] * u + r, p[1] * u + r), fill=(216, 53, 42, 255))


icon(240, circle_mark).save(S + 'UI/Score_Circle.png', optimize=True)
icon(240, slash_mark).save(S + 'UI/Score_Slash.png', optimize=True)

# ── 소풍 배경 (임시) ──
K = 2
bg = Image.new('RGB', (1080 * K, 1899 * K), (191, 228, 243))
dr = ImageDraw.Draw(bg)
for x, y, w, h in [(90, 144, 288, 90), (690, 276, 246, 78)]:
    dr.rounded_rectangle((x * K, y * K, (x + w) * K, (y + h) * K), radius=45 * K, fill='white')
dr.ellipse((-270 * K, 630 * K, 720 * K, 1140 * K), fill=(185, 217, 142))
dr.ellipse((450 * K, 570 * K, 1410 * K, 1140 * K), fill=(168, 206, 123))
dr.rectangle((0, 900 * K, 1080 * K, 1899 * K), fill=(148, 194, 102))
bg.resize((1080, 1899), Image.LANCZOS).save(S + 'Background/BG_Picnic.png', optimize=True)

# ── 안 쓰게 된 그림 보관 ──
for rel in ['UI/Title_Tagline.png', 'UI/Title_Gear.png', 'UI/Title_StartButton.png', 'UI/Observe_Card.png',
            'UI/Observe_CountOn.png', 'UI/Observe_CountOff.png', 'UI/Observe_FlameDeco.png', 'UI/Observe_Sparkle.png',
            'UI/Draw_TrashIcon.png', 'Characters/Char_Dokkaebi_Daepa.png']:
    retire(rel)

print('연출 개편 그림을 만들었어요.')

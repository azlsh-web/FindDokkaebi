"""2~4챕터 방 배경 (임시) : BG_Room 에 색을 덮어 비 오는 낮 · 저녁 · 밤을 만들어요.
실행 : py Tools/art_temp/make_room_variants.py  →  Assets/Sprites/Background/BG_RoomRain · BG_RoomEvening · BG_RoomNight.png
"""
import random
from PIL import Image, ImageDraw, ImageFilter

SRC = "Assets/Sprites/Background/BG_Room.png"
OUT = "Assets/Sprites/Background/BG_Room{}.png"
WIN = (1296, 584, 2248, 1720)   # 창 유리가 있는 상자 (원본 px)

def glass_mask(im):
    """창 상자 안에서 하늘색 · 풀색 (유리 너머) 픽셀만"""
    m = Image.new("L", im.size, 0)
    px, mp = im.load(), m.load()
    x0, y0, x1, y1 = WIN
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b = px[x, y]
            if b > r + 12 or g > r + 6:
                mp[x, y] = 255
    return m.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))

def tint(im, mul, add=(0, 0, 0)):
    r, g, b = im.split()
    f = lambda c, k, a: c.point(lambda v: max(0, min(255, int(v * k + a))))
    return Image.merge("RGB", (f(r, mul[0], add[0]), f(g, mul[1], add[1]), f(b, mul[2], add[2])))

def sky(size, top, bottom):
    w, h = size
    g = Image.new("RGB", size)
    d = ImageDraw.Draw(g)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return g

def outside(im, mask, top, bottom, bush_mul):
    """유리 너머 : 하늘은 새 색, 풀(초록)은 어둡게"""
    base = tint(im, bush_mul)
    s = sky(im.size, top, bottom)
    px, sp, bp = im.load(), s.load(), base.load()
    out = im.copy(); op = out.load()
    x0, y0, x1, y1 = WIN
    mp = mask.load()
    for y in range(y0, y1):
        t = (y - y0) / (y1 - y0)
        for x in range(x0, x1):
            if not mp[x, y]: continue
            r, g, b = px[x, y]
            op[x, y] = bp[x, y] if g > r + 6 and g >= b else tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
    return out

def rain_streaks(im, mask):
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    rnd = random.Random(7)
    x0, y0, x1, y1 = WIN
    for _ in range(260):
        x, y = rnd.randint(x0, x1), rnd.randint(y0, y1)
        d.line([(x, y), (x - 14, y + 70)], fill=(235, 242, 250, 170), width=5)
    clip = Image.new("RGBA", im.size, (0, 0, 0, 0))
    clip.paste(layer, (0, 0), mask)
    out = im.convert("RGBA"); out.alpha_composite(clip)
    return out.convert("RGB")

im = Image.open(SRC).convert("RGB")
mask = glass_mask(im)

rain = outside(im, mask, (150, 165, 182), (190, 198, 208), (0.62, 0.70, 0.72))
rain = rain_streaks(rain, mask)
rain = tint(rain, (0.86, 0.88, 0.94))
rain.save(OUT.format("Rain"))

eve = outside(im, mask, (247, 160, 110), (255, 212, 150), (0.62, 0.55, 0.45))
eve = tint(eve, (1.0, 0.86, 0.74))
eve.save(OUT.format("Evening"))

night = outside(im, mask, (22, 30, 62), (48, 54, 96), (0.18, 0.24, 0.30))
night = tint(night, (0.52, 0.52, 0.70))
night.save(OUT.format("Night"))
print("ok")

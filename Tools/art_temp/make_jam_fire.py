"""5챕터 그리기 방해 도깨비불 (임시) : 푸른 도깨비불(Magnifier_DokkaebiFire)의 파란 부분만 붉은 불로 바꾸고, 위쪽 빈자리를 잘라요.
갈색 테두리는 그대로 둬요. 밝은 곳일수록 노란 쪽, 어두운 곳일수록 붉은 쪽.
실행 : py -3 Tools/art_temp/make_jam_fire.py  →  Assets/Sprites/Chapter5/Jam_RedFire.png
"""
import colorsys
import sys
from PIL import Image

SRC = "Assets/Sprites/UI/Magnifier_DokkaebiFire.png"
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Sprites/Chapter5/Jam_RedFire.png"

im = Image.open(SRC).convert("RGBA")
px = im.load()
w, h = im.size
for y in range(h):
    for x in range(w):
        r, g, b, a = px[x, y]
        if a == 0:
            continue
        hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
        if 0.42 < hh < 0.78 and s > 0.12:
            light = min(1.0, max(0.0, (v - 0.55) / 0.45)) * (1 - s * 0.5)
            nh = 0.0 + 0.11 * light            # 빨강(0) ~ 주황노랑(0.11)
            ns = min(1.0, 0.75 + 0.25 * s)
            nv = min(1.0, 0.75 + 0.3 * v)
            nr, ng, nb = colorsys.hsv_to_rgb(nh, ns, nv)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
box = im.getbbox()
pad = 6
im = im.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(w, box[2] + pad), min(h, box[3] + pad)))
im.save(OUT)
print(OUT, im.size)

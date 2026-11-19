"""3 · 4챕터 도깨비 머리 불 색 (임시) : 몸 그림 4장 + 대사 상자 얼굴 3장의 노란 머리만 주황(M1) · 붉음(M2)으로 바꿔요.
실행 : py Tools/art_temp/make_dokkaebi_mood.py  →  Assets/Sprites/Characters/Mood/<이름>_M1.png, _M2.png
"""
import colorsys, os
from PIL import Image

SRC = "Assets/Sprites/Characters"
OUT = "Assets/Sprites/Characters/Mood"
NAMES = ["Char_Dokkaebi_Normal", "Char_Dokkaebi_NoBat", "Char_Dokkaebi_Swing", "Char_Dokkaebi_Think",
         "Char_DokkaebiFace_Normal", "Char_DokkaebiFace_Proud", "Char_DokkaebiFace_Tease"]
TARGET = {1: (0.075, 0.70), 2: (0.015, 0.78)}      # (색상, 채도) : 1 주황, 2 붉음

os.makedirs(OUT, exist_ok=True)
for name in NAMES:
    src = Image.open(f"{SRC}/{name}.png").convert("RGBA")
    for mood, (hue, sat) in TARGET.items():
        im = src.copy()
        px = im.load()
        w, h = im.size
        top = h if "Face" in name else int(h * 0.40)     # 몸 그림은 위쪽(머리)만 : 아래 표범 무늬 치마는 그대로
        for y in range(top):
            for x in range(w):
                r, g, b, a = px[x, y]
                if a == 0:
                    continue
                hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
                # 노란 머리 : 색상 25~55도, 채도가 있고 밝아요 (초록 피부 · 크림색 뿔 · 진한 선은 그대로)
                if 0.07 <= hh <= 0.155 and s >= 0.25 and v >= 0.55:
                    nr, ng, nb = colorsys.hsv_to_rgb(hue, min(1.0, sat * (s / 0.39)), v * 0.97)
                    px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
        im.save(f"{OUT}/{name}_M{mood}.png")
print("ok")

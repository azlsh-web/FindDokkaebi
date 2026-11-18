# 남은 시간 게이지 : 아트팀 촛대 그림(한 장)을 막대와 불꽃 두 장으로 나눠요
#   python Tools/docs/art_pipeline/cut_candle_gauge.py
#   원본 : ArtSource/원본/받은그림/CandleGauge/HUD_CandleGauge_원본.png (2172 x 724, 배경 투명, 막대 오른쪽 끝에 불꽃)
#   결과 : Assets/Sprites/UI/HUD_CandleGauge.png (막대) · HUD_CandleFlame.png (불꽃)
#   게임에서는 슬라이더처럼 막대가 왼쪽부터 채워지고(Filled), 불꽃이 핸들처럼 막대 끝을 따라가요.
#   불꽃에 붙어 있던 막대 끝은 지워요. 불꽃 왼쪽 테두리가 막대 끝을 덮도록 자리를 맞춰요 (MvpSceneBuilder).
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "원본", "받은그림", "CandleGauge", "HUD_CandleGauge_원본.png")
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")

STICK = (29, 354, 1944, 432)    # 막대 : 왼쪽 둥근 끝 ~ 불꽃 테두리 바로 앞 (원본 px)
X0 = 1930                       # 불꽃을 자르기 시작하는 x (막대 끝이 조금 섞여 있어서 아래에서 지워요)


def main():
    im = Image.open(SRC).convert("RGBA")
    im.crop(STICK).save(os.path.join(UI, "HUD_CandleGauge.png"))

    a = np.array(im)

    def first_opaque(y):
        return next(x for x in range(X0, 2100) if a[y, x, 3] > 20)

    def first_dark(y):
        return next(x for x in range(X0, 2010) if a[y, x, 3] > 200 and a[y, x, :3].max() < 120)

    # 막대 안쪽 줄 : 불꽃 테두리가 시작하는 곳까지 지워요.
    # 막대 위 · 아래 테두리 줄 : 막대 밖 16px 위 · 아래의 불꽃 가장자리와 이어 그어 지워요.
    inner = {y: first_dark(y) for y in range(352, 434) if a[y, X0, 3] > 20 and a[y, X0, :3].max() >= 120}
    top_y, bot_y = min(inner), max(inner)
    above, below = first_opaque(top_y - 16), first_opaque(bot_y + 16)
    for y in range(top_y - 16, bot_y + 17):
        if y in inner:
            b = inner[y]
        elif y < top_y:
            b = round(above + (inner[top_y] - above) * (y - (top_y - 16)) / 16)
        else:
            b = round(inner[bot_y] + (below - inner[bot_y]) * (y - bot_y) / 16)
        a[y, X0:b, 3] = 0

    flame = Image.fromarray(a).crop((X0, 205, 2155, 478))
    box = flame.getbbox()
    flame.crop(box).save(os.path.join(UI, "HUD_CandleFlame.png"))
    print(f"막대 {STICK[2] - STICK[0]} x {STICK[3] - STICK[1]}, 막대 가운데 y {(STICK[1] + STICK[3]) / 2}")
    print(f"불꽃 {box[2] - box[0]} x {box[3] - box[1]}, 원본 자리 왼쪽 위 ({X0 + box[0]}, {205 + box[1]})")


if __name__ == "__main__":
    main()

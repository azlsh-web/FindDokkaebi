# 주인공 얼굴 그림(BoyFace 4장)의 둘레 투명 여백을 잘라 대사 상자의 얼굴 칸에 꽉 차게 넣어요.
#   받은 그림은 얼굴 둘레에 빈 공간이 많아서 그대로 쓰면 도깨비 얼굴보다 작아 보여요.
#   python Tools/docs/art_pipeline/trim_boy_faces.py
#   원본 : ArtSource/원본/받은그림/BoyFace/Char_Boy_*.png  →  결과 : Assets/Sprites/Characters/Char_BoyFace_*.png
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")
SRC = os.path.join(ROOT, "ArtSource", "원본", "받은그림", "BoyFace")
OUT = os.path.join(ROOT, "Assets", "Sprites", "Characters")
NAMES = {"Happy": "Happy", "Surprise": "Surprised", "Sad": "Sad", "Normal": "Normal"}
MARGIN = 8          # 잘라 낸 뒤 둘레에 남기는 여백 (픽셀)

for src, dst in NAMES.items():
    im = Image.open(os.path.join(SRC, f"Char_Boy_{src}.png")).convert("RGBA")
    x0, y0, x1, y1 = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox()
    box = (max(0, x0 - MARGIN), max(0, y0 - MARGIN), min(im.width, x1 + MARGIN), min(im.height, y1 + MARGIN))
    out = im.crop(box)
    out.save(os.path.join(OUT, f"Char_BoyFace_{dst}.png"))
    print(f"Char_BoyFace_{dst}.png {im.size} -> {out.size}")

# 그림판 종이(Draw_Paper)의 둥근 테두리 바깥을 투명하게 만들어요.
#   받은 그림은 투명 없이 꽉 채운 PNG 라서, 둥근 모서리 바깥에 네모난 종이색 테두리가 남아
#   방 배경 위에서 네모 판처럼 보여요. 네 가장자리에서 시작해 진갈색 테두리에 닿을 때까지의 밝은 곳만 지워요.
#   python Tools/docs/art_pipeline/cut_paper_corners.py
#   원본 : ArtSource/원본/받은그림/Paper/Draw_Paper.png  →  결과 : Assets/Sprites/UI/Draw_Paper.png
import os
from collections import deque

import numpy as np
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..")
SRC = os.path.join(ROOT, "ArtSource", "원본", "받은그림", "Paper", "Draw_Paper.png")
OUT = os.path.join(ROOT, "Assets", "Sprites", "UI", "Draw_Paper.png")
DARK = 150          # 이보다 어두우면 테두리 선 (밝기 0~255)

im = np.array(Image.open(SRC).convert("RGBA"))
h, w = im.shape[:2]
lum = im[..., :3].astype(np.int32).mean(axis=2)
outside = np.zeros((h, w), bool)
q = deque()
for x in range(w):
    q.append((0, x)); q.append((h - 1, x))
for y in range(h):
    q.append((y, 0)); q.append((y, w - 1))
while q:
    y, x = q.popleft()
    if outside[y, x] or lum[y, x] < DARK:
        continue
    outside[y, x] = True
    for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
        if 0 <= ny < h and 0 <= nx < w and not outside[ny, nx]:
            q.append((ny, nx))

# 테두리 선의 바깥쪽 가장자리는 어두운 정도만큼만 남겨서 계단이 지지 않게 해요
alpha = np.where(outside, 0, 255).astype(np.int32)
edge = (~outside) & (np.roll(outside, 1, 0) | np.roll(outside, -1, 0) | np.roll(outside, 1, 1) | np.roll(outside, -1, 1))
alpha[edge] = np.clip((255 - lum[edge]) * 255 / (255 - 40), 0, 255).astype(np.int32)
im[..., 3] = alpha
Image.fromarray(im).save(OUT)
print(f"바깥 {outside.sum()} 픽셀을 투명하게 했어요 ({outside.mean() * 100:.1f}%)")

# 그리기 화면 클립보드 틀 만들기 : ArtSource/원본/미사용/Draw_Clipboard.png → Draw_ClipboardFrame.png
#   python make_clipboard_frame.py <FindDokkaebi 폴더>
#   1) 곧게 뻗은 가운데 줄을 반복해서 세로로 늘려요. (모서리 · 집게 · 반짝이 모양은 그대로)
#   2) 종이 면을 투명하게 뚫어요. 그림판을 틀 뒤에 두면 잉크가 둥근 종이 모양대로 보여요.
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = sys.argv[1]
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")
EXTRA_ROWS = 255      # 1824x1996 → 1824x2251
STRETCH_ROW = 1040    # 왼쪽 · 오른쪽 테두리가 곧은 줄
PAPER = np.array([253, 252, 245], dtype=float)
FULLY_INK = 120.0     # 종이 색에서 이만큼 멀면 불투명

src = np.array(Image.open(os.path.join(ROOT, "ArtSource", "원본", "미사용", "Draw_Clipboard.png")).convert("RGBA"))
row = src[STRETCH_ROW:STRETCH_ROW + 1]
im = np.concatenate([src[:STRETCH_ROW], np.repeat(row, EXTRA_ROWS, axis=0), src[STRETCH_ROW:]], axis=0).astype(float)

rgb, alpha = im[..., :3], im[..., 3]
dist = np.abs(rgb - PAPER).sum(axis=2)
regions, _ = ndimage.label((dist < 30) & (alpha > 200))
paper = regions == regions[im.shape[0] // 2, im.shape[1] // 2]

# 경계의 부드러운 픽셀 : 종이 색을 빼서 반투명으로 (뒤의 잉크와 자연스럽게 섞여요)
fringe = ndimage.binary_dilation(paper, iterations=3) & ~paper
keep = np.clip(dist / FULLY_INK, 0.0, 1.0)
fx = keep[fringe][:, None]
rgb[fringe] = np.clip(PAPER + (rgb[fringe] - PAPER) / np.maximum(fx, 0.01), 0, 255)
alpha[fringe] = np.minimum(alpha[fringe], fx[:, 0] * 255)
rgb[paper] = PAPER
alpha[paper] = 0

Image.fromarray(im.round().astype(np.uint8), "RGBA").save(os.path.join(UI, "Draw_ClipboardFrame.png"))
ys, xs = np.where(paper)
print("틀 크기", im.shape[1], "x", im.shape[0], "/ 종이 면", xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)

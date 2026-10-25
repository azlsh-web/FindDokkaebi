# 배경 제거: 테두리에서 이어진 밝은 배경을 지우고, 가장 큰 덩어리(캐릭터)만 남긴다
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
def cutout(img, box, tol=26, keep="largest", min_frac=0.02, dark=110):
    im = img.convert("RGB").crop(box); a = np.asarray(im).astype(int)
    h, w, _ = a.shape
    # 배경 후보: 밝고(어두운 윤곽선 아님) 이웃과 색이 비슷한 픽셀
    lum = a.mean(2)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    ref = np.median(border, 0)
    cand = (np.abs(a - ref).max(2) < tol * 2.2) & (lum > dark)
    lab, n = ndi.label(cand)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bg = np.isin(lab, list(edge))
    fg = ~bg
    lab2, n2 = ndi.label(fg)
    if n2:
        sizes = ndi.sum(fg, lab2, range(1, n2 + 1))
        if keep == "largest": fg = lab2 == (np.argmax(sizes) + 1)
        else: fg = np.isin(lab2, [i + 1 for i, s in enumerate(sizes) if s > fg.size * min_frac])
    fg = ndi.binary_fill_holes(fg) if keep == "fill" else fg
    alpha = (fg * 255).astype(np.uint8)
    # 가장자리 1px 부드럽게
    alpha = np.asarray(Image.fromarray(alpha).filter(__import__("PIL.ImageFilter", fromlist=["x"]).GaussianBlur(0.6)))
    out = np.dstack([a.astype(np.uint8), alpha])
    o = Image.fromarray(out, "RGBA"); bb = o.getbbox(); return o.crop(bb)

def cutout_outline(img, box, dark=120, close=3, grow=1):
    # 윤곽선 기반: 어두운 윤곽선을 닫고 안을 채워 실루엣을 만든다 (배경과 피부색이 비슷할 때)
    im = img.convert("RGB").crop(box); a = np.asarray(im).astype(int)
    line = a.mean(2) < dark
    m = ndi.binary_closing(line, iterations=close)
    m = ndi.binary_fill_holes(m)
    lab, n = ndi.label(m)
    if n:
        sizes = ndi.sum(m, lab, range(1, n + 1)); m = lab == (np.argmax(sizes) + 1)
    if grow: m = ndi.binary_dilation(m, iterations=grow) & ndi.binary_dilation(line | m, iterations=0)
    alpha = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(__import__("PIL.ImageFilter", fromlist=["x"]).GaussianBlur(0.6)))
    o = Image.fromarray(np.dstack([a.astype(np.uint8), alpha]), "RGBA"); return o.crop(o.getbbox())

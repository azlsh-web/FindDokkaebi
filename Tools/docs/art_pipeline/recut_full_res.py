# 시안 부품을 원본 해상도로 다시 오려요 (2026-11-04, 한 번만 실행하는 작업 기록)
#   처음에는 시안(2248x3952)을 절반 크기로 오려서, 큰 화면에서 테두리가 흐렸어요.
#   지금 부품(글자를 지운 것 포함)과 시안을 비교해, 내용이 같은 곳은 시안 픽셀을 그대로 쓰고
#   지운 곳은 지금 부품을 키워서 써요. 도형뿐인 카드 · 고리는 다시 그려요.
#   HUD 세 칸은 캡슐 한 장(HUD_Capsule) + 로고 내용(HUD_LogoMark)으로 나눠요.
#   python recut_full_res.py <FindDokkaebi 폴더>
import os, sys, time
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT = sys.argv[1]
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")
SRC = os.path.join(ROOT, "ArtSource", "원본", "기획서")
FULL = {
    "9": np.asarray(Image.open(os.path.join(SRC, "그림09_관찰화면_시안_확정.png")).convert("RGB")),
    "10": np.asarray(Image.open(os.path.join(SRC, "그림10_그리기화면_시안_확정.png")).convert("RGB")),
}
HALF = {k: cv2.resize(v, (v.shape[1] // 2, v.shape[0] // 2), interpolation=cv2.INTER_AREA) for k, v in FULL.items()}

# 부품 : (시안, 절반 크기 시안에서 찾을 위치). 버튼 바탕은 같은 모양이 있는 자리에서 가져와요.
PARTS = {
    "HUD_PauseButton": ("9", (952, 60)),
    "HUD_SnackBag": ("9", (86, 234)),
    "Observe_Card": ("9", (150, 295)),
    "Observe_CountOn": ("9", (264, 1544)),
    "Observe_CountOff": ("9", (476, 1546)),
    "Observe_FlameDeco": ("9", (390, 1815)),
    "Draw_Clipboard": ("10", (58, 318)),
    "Draw_PaletteBoard": ("10", (63, 1343)),
    "Draw_MixButton": ("10", (816, 1342)),
    "Draw_PaintBlack": ("10", (203, 1380)),
    "Draw_PaintRed": ("10", (316, 1398)),
    "Draw_PaintYellow": ("10", (426, 1409)),
    "Draw_PaintBlue": ("10", (535, 1401)),
    "Draw_PaintMixed": ("10", (646, 1383)),
    "Draw_PencilButton": ("10", (155, 1558)),
    "Draw_EraserButton": ("10", (360, 1558)),
    "Draw_UndoButton": ("10", (564, 1558)),
    "Draw_ClearButton": ("10", (564, 1558)),
    "Draw_SubmitButton": ("10", (121, 1744)),
    "Common_ButtonCoral": ("10", (121, 1744)),
    "Common_ButtonCream": ("10", (768, 1570)),
}
SEARCH = 24


def save(im, path):
    """Unity 가 파일을 잠시 잡고 있으면 쓰기가 실패해요 : 잠깐 기다렸다 다시 해요"""
    for attempt in range(30):
        try:
            im.save(path)
            return
        except OSError:
            time.sleep(1)
    im.save(path)


def smooth(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def recut(name, key, expected):
    path = os.path.join(UI, name + ".png")
    cur = np.asarray(Image.open(path).convert("RGBA"))
    h, w = cur.shape[:2]
    half, full = HALF[key], FULL[key]
    x0, y0 = max(0, expected[0] - SEARCH), max(0, expected[1] - SEARCH)
    window = half[y0 : min(half.shape[0], expected[1] + h + SEARCH), x0 : min(half.shape[1], expected[0] + w + SEARCH)]
    mask = np.dstack([(cur[..., 3] > 200).astype(np.uint8)] * 3)
    m = np.nan_to_num(cv2.matchTemplate(window, cur[..., :3], cv2.TM_SQDIFF, mask=mask), nan=9e18, posinf=9e18)
    _, _, loc, _ = cv2.minMaxLoc(m)
    x, y = x0 + loc[0], y0 + loc[1]

    hi = full[2 * y : 2 * y + 2 * h, 2 * x : 2 * x + 2 * w].astype(np.float32)
    up = cv2.resize(cur[..., :3], (2 * w, 2 * h), interpolation=cv2.INTER_CUBIC).astype(np.float32)
    down = cv2.resize(hi, (w, h), interpolation=cv2.INTER_AREA)
    # 살짝 흐려서 비교해요 : 줄일 때 생긴 작은 차이는 무시하고, 지운 글자 · 그림은 잡아내요
    blur = lambda im: cv2.GaussianBlur(np.float32(im), (0, 0), 1.2)
    different = ((np.abs(blur(down) - blur(cur[..., :3])).max(axis=2) >= 10) & (cur[..., 3] > 200)).astype(np.uint8)
    # 지운 자리는 둘레까지 넉넉히 지금 부품을 써요 (시안의 지운 내용이 희미하게 되살아나지 않게)
    same = 1 - cv2.dilate(different, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15)))
    keep = cv2.GaussianBlur(cv2.resize(same, (2 * w, 2 * h), interpolation=cv2.INTER_NEAREST).astype(np.float32), (0, 0), 1.0)[..., None]
    rgb = hi * keep + up * (1 - keep)
    alpha = smooth(cv2.resize(cur[..., 3], (2 * w, 2 * h), interpolation=cv2.INTER_CUBIC).astype(np.float32) / 255, 0.25, 0.75)
    out = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    save(Image.fromarray(out, "RGBA"), path)
    print(f"{name:22s} 시안 {key:>2s} ({x},{y}) 시안 픽셀 {same.mean() * 100:5.1f}%  {w}x{h} -> {2 * w}x{2 * h}")


def rounded(size, radius, stroke, fill, width):
    s = 4
    w, h = size
    im = Image.new("RGBA", (w * s, h * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, w * s - 1, h * s - 1), radius=radius * s, fill=tuple(stroke) + (255,))
    m = width * s
    d.rounded_rectangle((m, m, w * s - 1 - m, h * s - 1 - m), radius=max(0, radius - width) * s, fill=tuple(fill) + (255,))
    return im.resize(size, Image.LANCZOS)


def redraw_card():
    """카드 : 지금 그림에서 색 · 테두리 · 둥글기를 재서 2배로 다시 그려요"""
    path = os.path.join(UI, "Common_Card.png")
    cur = np.asarray(Image.open(path).convert("RGBA"))
    h, w = cur.shape[:2]
    fill = cur[h // 2, w // 2, :3].tolist()
    row = cur[h // 2].astype(int)
    solid = np.nonzero(row[:, 3] > 128)[0]
    left = solid[0]
    dark = left + int(np.argmin(row[left : left + 30, :3].sum(axis=1)))
    stroke = row[dark, :3].tolist()
    inner = left + int(np.argmax(np.abs(row[left : left + 30, :3] - np.array(fill)).max(axis=1) < 12))
    width = max(1, inner - left)
    best = None
    bg = np.array([200, 200, 200])
    target = cur[..., :3] * (cur[..., 3:] / 255) + bg * (1 - cur[..., 3:] / 255)
    for radius in range(4, 60):
        im = np.asarray(rounded((w, h), radius, stroke, fill, width))
        comp = im[..., :3] * (im[..., 3:] / 255) + bg * (1 - im[..., 3:] / 255)
        d = np.abs(comp - target).mean()
        if best is None or d < best[0]:
            best = (d, radius)
    print("Common_Card 채움", fill, "테두리", stroke, "굵기", width, "둥글기", best[1], "차이", round(best[0], 2))
    # 9-slice 라 가운데는 늘어나요. 그림이 넓으면 작은 카드에서 크게 줄어들어 위아래 테두리가 흐려져서 작게 만들어요
    save(rounded((200, 200), 2 * best[1], stroke, fill, 2 * width), path)


def redraw_ring():
    path = os.path.join(UI, "Common_SelectRing.png")
    cur = np.asarray(Image.open(path).convert("RGBA"))
    h, w = cur.shape[:2]
    row = cur[h // 2]
    on = np.nonzero(row[:, 3] > 128)[0]
    gaps = np.nonzero(np.diff(on) > 1)[0]
    outer = (on[-1] - on[0] + 1) / 2
    inner = (on[gaps[0] + 1] - on[gaps[0]] - 1) / 2
    color = tuple(int(v) for v in row[on[0] + 3, :3])
    s = 8
    W, H = 2 * w, 2 * h
    im = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, cy = W * s / 2, H * s / 2
    R, r = outer * 2 * s, inner * 2 * s
    d.ellipse((cx - R, cy - R, cx + R, cy + R), fill=color + (255,))
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=color + (0,))
    save(im.resize((W, H), Image.LANCZOS), path)
    print("Common_SelectRing 색", color, "바깥", outer, "안", inner)


def hud_parts():
    """HUD : 캡슐 한 장(흰 채움, 게임이 색을 입혀요)과 로고 내용"""
    full = FULL["9"]
    logo = full[114 : 114 + 238, 182 : 182 + 674].astype(np.float32)
    h, w = logo.shape[:2]
    cream = np.median(logo[h // 2 - 70 : h // 2 + 70, w - 60 : w - 40].reshape(-1, 3), axis=0)
    stroke = np.percentile(logo[h // 2 - 8 : h // 2 + 8, w - 16 : w].reshape(-1, 3), 5, axis=0)
    print("HUD 크림", cream.round(), "테두리", stroke.round())
    rounded((276, 236), 118, [int(v) for v in stroke], (255, 255, 255), 12).save(os.path.join(UI, "HUD_Capsule.png"))
    yy, xx = np.mgrid[0:h, 0:w]
    r = h / 2
    dist = np.hypot(xx - np.clip(xx, r, w - r), yy - r)
    inside = smooth(r - 34 - dist, -1, 1)
    alpha = smooth(np.abs(logo - cream).max(axis=2), 10, 46) * inside
    Image.fromarray(np.dstack([logo, alpha * 255]).astype(np.uint8), "RGBA").save(os.path.join(UI, "HUD_LogoMark.png"))
    for name in ("HUD_Logo", "HUD_StagePill", "HUD_SnackPill"):
        for ext in (".png", ".png.meta"):
            p = os.path.join(UI, name + ext)
            if os.path.exists(p):
                os.remove(p)


def narrow(name, cap, middle=40):
    """버튼 바탕도 양 끝 + 가운데 조금만 남겨요 (카드와 같은 이유)"""
    path = os.path.join(UI, name + ".png")
    a = np.asarray(Image.open(path).convert("RGBA"))
    c = a.shape[1] // 2
    save(Image.fromarray(np.hstack([a[:, :cap], a[:, c - middle // 2 : c + middle // 2], a[:, -cap:]]), "RGBA"), path)


def sparkle():
    """3 옆의 반짝이 : 처음 부품은 잘려 있어서 시안에서 두 줄을 온전히 다시 떼어 내요"""
    a = FULL["9"][3105:3250, 390:520].astype(np.float32)
    background = np.median(a[:, :6, 2])
    alpha = smooth(background - a[..., 2], 15, 70)
    color = np.median(a[alpha > 0.95].reshape(-1, 3), axis=0)
    out = np.dstack([np.full(alpha.shape + (3,), color), alpha * 255]).astype(np.uint8)
    save(Image.fromarray(out, "RGBA"), os.path.join(UI, "Observe_Sparkle.png"))


hud_parts()
for name, (key, expected) in PARTS.items():
    recut(name, key, expected)
redraw_card()
redraw_ring()
narrow("Common_ButtonCoral", 182)
narrow("Common_ButtonCream", 140)
sparkle()

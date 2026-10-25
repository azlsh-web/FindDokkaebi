# 기획서 시안 그림에서 "빠진 특징"마다 바뀐 모습(엉뚱한 물건)을 만든다
import itertools, colorsys, base64, io, json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

def hsv(a):
    rgb = a[..., :3] / 255.0
    mx, mn = rgb.max(-1), rgb.min(-1); v = mx; s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]; d = np.maximum(mx - mn, 1e-6)
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return np.where(mx == mn, 0, h), s, v
def recolor(a, mask, rgb_target, keep_value=True, sat=None):
    # 마스크 영역을 목표 색상으로: 명도(그림자)는 유지
    th, ts, tv = colorsys.rgb_to_hsv(*[c / 255 for c in rgb_target])
    h, s, v = hsv(a)
    out = a.copy()
    for yx in zip(*np.nonzero(mask)):
        vv = v[yx] / max(1e-6, VREF) * tv if keep_value else tv
        r, g, b = colorsys.hsv_to_rgb(th, ts if sat is None else sat, min(1, vv))
        out[yx][:3] = (r * 255, g * 255, b * 255)
    return out
VREF = 1

def solid_bag(src):
    # 윤곽선 틈을 메워 가방 안쪽을 불투명한 흰색으로 (반투명 가장자리는 흰 바탕 위에 합성)
    a = np.asarray(src).astype(float).copy(); op = a[..., 3] > 10
    m = ndi.binary_fill_holes(ndi.binary_closing(op, iterations=7)) & ~ndi.binary_dilation(~ndi.binary_fill_holes(op | ndi.binary_closing(op, iterations=7)), iterations=1)
    al = a[..., 3:4] / 255
    rgb = a[..., :3] * al + 255 * (1 - al)
    a[m, :3] = rgb[m]; a[m, 3] = 255
    return Image.fromarray(a.astype(np.uint8), "RGBA")
def bag_variant(src, present):
    body, handle, pocket, stripes = present
    src = solid_bag(src)
    a = np.asarray(src).astype(float).copy(); H, W = a.shape[:2]
    lum = a[..., :3].mean(-1); dark = (lum < 238) & (a[..., 3] > 0)
    yy, xx = np.mgrid[0:H, 0:W]
    stripe_zone = (xx >= 58) & (xx <= 170) & (yy >= 138) & (yy <= 198)
    pocket_zone = (xx >= 36) & (xx <= 189) & (yy >= 109) & (yy <= 213)
    if not stripes: a[stripe_zone, :3] = 255
    if not pocket:
        keep = stripe_zone & dark if stripes else np.zeros_like(dark)
        a[pocket_zone & dark & ~keep, :3] = 255
    if not handle: a[(yy < 38) & (xx > 64) & (xx < 164), 3] = 0
    img = Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    if not body:
        # 동그란 가방: 원 밖은 지우고 둥근 테두리
        m = Image.new("L", img.size, 0); ImageDraw.Draw(m).ellipse((10, 8, W - 10, H - 4), fill=255)
        al = np.minimum(np.asarray(img)[..., 3], np.asarray(m)); arr = np.asarray(img).copy(); arr[..., 3] = al
        img = Image.fromarray(arr, "RGBA"); ImageDraw.Draw(img).ellipse((10, 8, W - 10, H - 4), outline=(28, 28, 30, 255), width=7)
    return img

def gimbap_variant(src, present):
    nori, rice, center, ry, green = present
    a = np.asarray(src).astype(float).copy(); H, W = a.shape[:2]
    h, s, v = hsv(a); yy, xx = np.mgrid[0:H, 0:W]
    cx, cy, R = 108, 100, 81
    dist = np.hypot(xx - cx, yy - cy)
    op = a[..., 3] > 0
    red = op & (s > .45) & ((h < 18) | (h > 340)) & (v > .35)
    yel = op & (s > .45) & (h >= 35) & (h <= 62) & (v > .45)
    grn = op & (s > .3) & (h >= 75) & (h <= 160) & (v > .2)
    fill_core = red | yel | grn
    cluster = ndi.binary_dilation(fill_core, iterations=4) & (dist < 79)
    ricepx = op & (dist < R - 1) & ~cluster & (v > .55) & (s < .22)
    rice_rgb = np.array([250, 247, 240])
    out = a.copy()
    if not ry:
        for m, c in ((red, (185, 180, 173)), (yel, (214, 209, 201))):
            g = (v[m] / v[m].mean())[:, None] * np.array(c); out[m, :3] = g.clip(0, 255)
    if not green:
        th = 218 / 360; vv = v[grn] / v[grn].mean() * .78
        out[grn, :3] = np.array([colorsys.hsv_to_rgb(th, .75, min(1, x)) for x in vv]) * 255
    if not center:
        dists = [ndi.distance_transform_edt(~m) for m in (red, yel, grn)]
        owner = np.argmin(np.stack(dists), 0)
        src_px = out.copy()
        out[cluster, :3] = rice_rgb; out[cluster, 3] = 255
        for k, (dx, dy) in enumerate(((-18, -20), (24, -12), (-4, 28))):
            pm = cluster & (owner == k)
            ys, xs = np.nonzero(pm)
            out[(ys + dy).clip(0, H - 1), (xs + dx).clip(0, W - 1)] = src_px[ys, xs]
    if not rice:
        vv = (v[ricepx] / v[ricepx].max())
        out[ricepx, :3] = (vv[:, None] * np.array([122, 90, 134])).clip(0, 255)
    if not nori:
        out[dist > R + 1, 3] = 0
    img = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")
    if not nori: ImageDraw.Draw(img).ellipse((cx - R - 1, cy - R - 1, cx + R + 1, cy + R + 1), outline=(60, 44, 36, 255), width=3)
    return img

def friend_variant(src, present):
    person, stripes, greentop, brown = present
    a = np.asarray(src).astype(float).copy(); H, W = a.shape[:2]
    h, s, v = hsv(a); yy, xx = np.mgrid[0:H, 0:W]; op = a[..., 3] > 0
    clothes_y = (yy > 136) & (yy < 228)
    g = op & (h >= 70) & (h <= 160) & (s > .2) & clothes_y
    shirt = g & (v > .6); stripe = g & (v <= .6)
    br = op & (h >= 10) & (h <= 40) & (s > .25) & (v > .25) & (v < .75) & (yy > 185) & (yy < 228)
    out = a.copy()
    if not stripes:
        out[stripe, :3] = np.median(a[shirt, :3], 0)
        shirt = shirt | stripe; stripe = np.zeros_like(stripe)
    if not greentop:
        for m in (shirt, stripe):
            if m.any():
                vv = v[m] / v[shirt].mean() * .86
                out[m, :3] = np.array([colorsys.hsv_to_rgb(218 / 360, .45, min(1, x)) for x in vv]) * 255
    if not brown:
        vv = v[br] / v[br].mean()
        out[br, :3] = (vv[:, None] * np.array([142, 152, 166])).clip(0, 255)
    img = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")
    if not person:
        cl = ndi.binary_dilation(g | br, iterations=4) & clothes_y & op
        cl = ndi.binary_fill_holes(cl)
        arr = np.asarray(img).copy(); arr[..., 3] = np.where(cl, arr[..., 3], 0)
        img = Image.fromarray(arr, "RGBA")
        # 옷걸이
        d = ImageDraw.Draw(img); top = 128
        d.line([(80, top + 14), (112, top - 4), (144, top + 14)], fill=(70, 50, 40, 255), width=4)
        d.arc((104, top - 26, 120, top - 6), 180, 90, fill=(70, 50, 40, 255), width=4)
        d.line([(112, top - 6), (112, top - 4)], fill=(70, 50, 40, 255), width=4)
    return img

def to_uri(img, scale=2):
    img = img.crop(img.getbbox()) if img.getbbox() else img
    img = img.resize((img.width * scale, img.height * scale), Image.LANCZOS)
    buf = io.BytesIO(); img.save(buf, "WEBP", quality=88, method=6)
    return "data:image/webp;base64," + base64.b64encode(buf.getvalue()).decode()

out = {}
for key, fn, n in (("bag", bag_variant, 4), ("gimbap", gimbap_variant, 5), ("friend", friend_variant, 4)):
    src = Image.open(f"m_{key}.png").convert("RGBA")
    out[key] = {}
    for combo in itertools.product([1, 0], repeat=n):
        if not any(combo): continue
        im = fn(src, combo); code = "".join(map(str, combo))
        out[key][code] = to_uri(im)
        if code in ("1" * n,) or combo.count(0) == 1 or code in ("0101", "10110"):
            im.save(f"v_{key}_{code}.png")
for name, f in (("gob_club", "gob_club.png"), ("gob_normal", "gob_normal.png"), ("gob_happy", "gob_happy.png"), ("gob_excited", "gob_excited.png"),
                ("gob_think", "gob_think.png"), ("gob_oops", "gob_oops.png"), ("boy", "m_boy.png"), ("snack", "m_snack.png")):
    im = Image.open(f).convert("RGBA")
    if name == "gob_club": im = im.resize((im.width // 2, im.height // 2), Image.LANCZOS); out[name] = to_uri(im, 1)
    elif name.startswith("gob"): out[name] = to_uri(im, 1)
    else: out[name] = to_uri(im, 2)
json.dump(out, open("assets.json", "w"))
print({k: (len(v) if isinstance(v, dict) else 1) for k, v in out.items()}, round(len(json.dumps(out)) / 1e6, 2), "MB")

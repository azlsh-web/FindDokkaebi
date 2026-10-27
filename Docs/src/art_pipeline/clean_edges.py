# 오린 그림 가장자리 정리 (배경 제거 다듬기)
#   1) 모양이 정해진 UI(알약 · 동그라미 버튼 · 카드)는 그 모양 그대로 매끈한 마스크로 잘라서,
#      딸려 온 배경 조각(구름 선 등)과 계단 같은 가장자리를 없애요.
#   2) 모양이 제각각인 그림(도깨비 · 물감 판 · 물감 동그라미 등)은 바깥 한 줄을 "배경과 얼마나 다른가"로
#      반투명하게 만들어 가장자리를 부드럽게 해요. (지운 배경 칸에 원래 색이 남아 있어서 계산할 수 있어요)
import sys, os
from PIL import Image, ImageDraw


def apply_shape(path, kind):
    # 보이는 칸들의 범위(테두리 바깥 끝)에 꼭 맞는 매끈한 모양 마스크로 다시 자르기
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    # 범위 : 가운데 줄·칸에서만 재요 (모서리에 딸려 온 배경 선 때문에 범위가 커지지 않게)
    al = im.getchannel("A").load()
    rows = range(int(h * 0.35), int(h * 0.65)); cols = range(int(w * 0.3), int(w * 0.7))
    xs = [x for y in rows for x in range(w) if al[x, y] > 0]
    ys = [y for x in cols for y in range(h) if al[x, y] > 0]
    x0, x1, y0, y1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
    K = 4
    m = Image.new("L", (w * K, h * K), 0)
    d = ImageDraw.Draw(m)
    box = (x0 * K, y0 * K, x1 * K - 1, y1 * K - 1)
    bh = y1 - y0
    if kind == "pill":
        d.rounded_rectangle(box, radius=bh * K / 2, fill=255)
    elif kind == "circle":
        d.ellipse(box, fill=255)
    else:
        d.rounded_rectangle(box, radius=int(kind[5:]) * K, fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    # 원래 그림에서 2칸 넘게 벗어나지는 않게 (모서리 모양이 조금 달라도 배경이 비치지 않게)
    from PIL import ImageFilter, ImageChops
    near = im.getchannel("A").point(lambda v: 255 if v > 0 else 0).filter(ImageFilter.MaxFilter(5))
    m = ImageChops.multiply(m, near)
    src = im.load(); mp = m.load()
    out = im.copy(); op = out.load()
    for y in range(h):
        for x in range(w):
            a = mp[x, y]
            r, g, b, oa = src[x, y]
            if a == 0:
                op[x, y] = (r, g, b, 0); continue
            if oa == 0:        # 원래 지워졌던 가장자리 칸 → 바로 옆 보이는 칸 색으로 (배경색이 비치지 않게)
                best = None
                for rr in (1, 2, 3):
                    for dy in range(-rr, rr + 1):
                        for dx in range(-rr, rr + 1):
                            nx, ny = x + dx, y + dy
                            if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] > 200:
                                best = src[nx, ny]; break
                        if best: break
                    if best: break
                if best: r, g, b = best[:3]
            op[x, y] = (r, g, b, a)
    out.save(path)


def defringe(im):
    # 보이는 칸 중 가장 바깥 줄 : 테두리(진한 색)와 배경 사이의 중간 색이면, 그만큼 반투명한 테두리 색으로
    w, h = im.size
    src = im.load()
    px = [[src[x, y] for x in range(w)] for y in range(h)]
    def dist(p, q): return abs(p[0] - q[0]) + abs(p[1] - q[1]) + abs(p[2] - q[2])
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[y][x]
            if a < 250: continue
            bgs = [px[ny][nx] for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
                   if 0 <= nx < w and 0 <= ny < h and px[ny][nx][3] == 0]
            if not bgs: continue
            bg = tuple(sum(q[i] for q in bgs) // len(bgs) for i in range(3))
            inner = None
            for dy in range(-2, 3):
                for dx in range(-2, 3):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and px[ny][nx][3] >= 250:
                        q = px[ny][nx]
                        if inner is None or dist(q, bg) > dist(inner, bg): inner = q
            full = dist(inner, bg)
            if full < 90: continue
            k = dist((r, g, b), bg) / full
            if k < 0.92:
                src[x, y] = (inner[0], inner[1], inner[2], int(max(0.0, k) * 255))
    return im


def soften(im, passes=1):
    defringe(im)
    # 바깥 한 줄 : 지워진(투명) 칸 중 바로 옆에 보이는 칸이 있는 것 → 배경과 다른 만큼 반투명 + 옆 칸 색
    w, h = im.size
    for _ in range(passes):
        src = im.load()
        px = [[src[x, y] for x in range(w)] for y in range(h)]
        out = im.load()
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[y][x]
                if a > 0:
                    continue
                inner = None; bgs = []
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < w and 0 <= ny < h:
                            q = px[ny][nx]
                            if q[3] >= 200 and inner is None: inner = q
                for dy in (-3, 0, 3):
                    for dx in (-3, 0, 3):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < w and 0 <= ny < h and px[ny][nx][3] == 0:
                            bgs.append(px[ny][nx])
                if inner is None or not bgs:
                    continue
                br = sorted(q[0] for q in bgs)[len(bgs) // 2]
                bgc = sorted(bgs, key=lambda q: abs(q[0] - br))[0]
                def dist(p, q): return abs(p[0] - q[0]) + abs(p[1] - q[1]) + abs(p[2] - q[2])
                full = dist(inner, bgc)
                if full < 30:
                    continue
                alpha = max(0.0, min(1.0, dist((r, g, b), bgc) / full))
                if alpha > 0.12:
                    out[x, y] = (inner[0], inner[1], inner[2], int(alpha * 255))
    return im


if __name__ == "__main__":
    ui = sys.argv[1]           # Assets/Sprites/TeamUI
    P = lambda n: os.path.join(ui, n + ".png")
    # 모양이 정해진 것
    for n in ["StagePill", "SnackPill", "SizePill", "SubmitButton", "TeamPillCoral", "TeamPillCream", "Logo"]:
        apply_shape(P(n), "pill")
    for n in ["Pause", "EraserButton", "UndoButton", "PencilButton", "RoundButton", "CountOff"]:
        apply_shape(P(n), "circle")
    apply_shape(P("MixButton"), "round46")
    # 모양이 제각각인 것 : 바깥 한 줄만 부드럽게
    for n in ["Clipboard", "PaletteEmpty", "ObserveCard", "BlobBlack", "BlobRed", "BlobYellow", "BlobBlue", "BlobMixed",
              "SnackBag", "Incense", "FlameDeco", "CountSparkle", "TeamCard"]:
        im = Image.open(P(n)).convert("RGBA"); soften(im); im.save(P(n))
    print("edges cleaned")

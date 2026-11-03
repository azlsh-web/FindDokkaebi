# "아직 팀 그림이 없어요" 표시 그림 한 장 (엉뚱한 물건 · 주인공 표정처럼 아직 없는 그림 자리에 같은 그림을 써요)
#   팀 그림이 오면 같은 파일 이름으로 덮어쓰면 바로 바뀌어요.
import sys
from PIL import Image, ImageDraw, ImageFont
out = sys.argv[1]
K = 2; S = 512 * K
im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(im)
brown, cream, muted = (74, 43, 22, 255), (253, 248, 238, 255), (150, 120, 95, 255)
pad = 40 * K
d.rounded_rectangle((pad, pad, S - pad, S - pad), radius=60 * K, fill=cream)
# 점선 테두리
import math
x0, y0, x1, y1, r = pad, pad, S - pad, S - pad, 60 * K
def dash_line(a, b):
    L = math.dist(a, b); n = int(L // (28 * K))
    for i in range(n):
        if i % 2: continue
        t0, t1 = i / n, (i + 1) / n
        d.line([(a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0), (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)], fill=brown, width=9 * K)
dash_line((x0 + r, y0), (x1 - r, y0)); dash_line((x0 + r, y1), (x1 - r, y1))
dash_line((x0, y0 + r), (x0, y1 - r)); dash_line((x1, y0 + r), (x1, y1 - r))
for cx, cy, a0 in ((x0 + r, y0 + r, 180), (x1 - r, y0 + r, 270), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90)):
    d.arc((cx - r, cy - r, cx + r, cy + r), a0, a0 + 90, fill=brown, width=9 * K)
# 가운데 물음표 동그라미
c = S // 2; cr = 92 * K; cy = 205 * K
d.ellipse((c - cr, cy - cr, c + cr, cy + cr), outline=brown, width=10 * K, fill=(255, 255, 255, 255))
fb = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 120 * K)
d.text((c, cy), "?", font=fb, fill=brown, anchor="mm")
f1 = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 54 * K)
f2 = ImageFont.truetype("C:/Windows/Fonts/malgun.ttf", 27 * K)
d.text((c, 360 * K), "그림 준비 중", font=f1, fill=brown, anchor="mm")
d.text((c, 418 * K), "팀 그림이 들어갈 자리예요", font=f2, fill=muted, anchor="mm")
im.resize((512, 512), Image.LANCZOS).save(out)
print("ok", out)

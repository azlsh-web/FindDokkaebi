# 4단계 : 다른 화면에서도 쓰는 늘림(9-slice)용 카드·버튼 — 시안 그림에서 만들기
import os
from PIL import Image, ImageDraw
OUT = "cut"

# 카드 : 관찰 카드의 아래쪽(모서리 2개 + 옆 테두리)을 위아래로 이어 붙여 네 모서리 카드로
card = Image.open(os.path.join(OUT, "ObserveCard.png"))
w, h = card.size
p = card.load()
row = h - 300
xs = [x for x in range(w) if p[x, row][3] > 0]
left, right = min(xs), max(xs)
bottom = card.crop((left, h - 220, right + 1, h))
bottom = bottom.crop(bottom.getchannel("A").getbbox())
bw, bh = bottom.size
top = bottom.transpose(Image.FLIP_TOP_BOTTOM)
c = Image.new("RGBA", (bw, bh * 2), (0, 0, 0, 0))
c.paste(top, (0, 0)); c.paste(bottom, (0, bh))
c.save(os.path.join(OUT, "TeamCard.png"))
print("TeamCard", c.size)

# 코랄 버튼 : "다 그렸어!" 버튼 바탕만 (글자·도깨비 지우기)
sb = Image.open(os.path.join(OUT, "SubmitButton.png"))
w, h = sb.size
coral = sb.getpixel((w - 120, h // 2))
blank = sb.copy()
ImageDraw.Draw(blank).rounded_rectangle((26, 26, w - 27, h - 27), radius=h / 2 - 26, fill=coral)
blank.save(os.path.join(OUT, "TeamPillCoral.png"))
print("coral", coral, blank.size)

# 크림 버튼 : 굵기 알약(점 지운 것)
Image.open(os.path.join(OUT, "SizePill.png")).save(os.path.join(OUT, "TeamPillCream.png"))

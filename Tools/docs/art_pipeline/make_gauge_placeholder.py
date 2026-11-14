# 남은 시간 게이지 자리 표시용 임시 그림 (HUD_CandleGauge, 2000 x 200)
#   python Tools/docs/art_pipeline/make_gauge_placeholder.py
#   2026-11-15 : 촛대 그림 대신 그냥 가로 직사각형 바. 아트팀 그림이 들어갈 자리만 알 수 있으면 돼요.
#   게임은 이 그림을 오른쪽 끝부터 잘라 내요. 뒤에는 같은 크기의 옅은 틀(GaugeSlot)이 깔려 있어 다 줄어도 자리가 보여요.
#   아트팀 그림이 오면 같은 이름으로 덮어써요 (이 스크립트를 다시 돌리면 그 그림을 덮어쓰니 주의).
import os

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
PATH = os.path.join(ROOT, "Assets", "Sprites", "UI", "HUD_CandleGauge.png")
W, H, LINE, R = 2000, 200, 12, 40
OUTLINE = (74, 43, 22, 255)      # 게임의 진갈색 테두리
FILL = (242, 170, 76, 255)       # 따뜻한 주황

im = Image.new("RGBA", (W, H))
d = ImageDraw.Draw(im)
d.rounded_rectangle((0, 0, W - 1, H - 1), R, fill=OUTLINE)
d.rounded_rectangle((LINE, LINE, W - 1 - LINE, H - 1 - LINE), R - LINE, fill=FILL)
im.save(PATH)
print("저장", PATH, im.size)

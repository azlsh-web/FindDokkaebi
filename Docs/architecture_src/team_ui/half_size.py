# 시안에서 오린 UI 그림과 시작 화면 그림을 절반 크기로 (게임 화면 1080 폭에 필요한 만큼)
#   압축 없이 넣으니까 빌드 크기를 줄이려고. 줄일 때 배경색이 번지지 않게 premultiplied 로
import sys, os
from PIL import Image
for path in sys.argv[1:]:
    im = Image.open(path).convert("RGBA")
    if im.width < 8: continue
    im = im.convert("RGBa").resize((max(1, round(im.width / 2)), max(1, round(im.height / 2))), Image.LANCZOS).convert("RGBA")
    im.save(path)
    print(os.path.basename(path), im.size)

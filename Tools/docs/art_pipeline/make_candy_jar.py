# 코드로 그리는 임시 그림 (아트팀 그림이 오면 같은 이름으로 바꿔 넣으면 돼요)
#   - 이야기 종료의 사탕 통
#   - 도깨비불 (Magnifier_DokkaebiFire : 향 불꽃 그림을 푸른빛으로 바꾼 것. 1단계 시범 때 돋보기 둘레를 돌아요)
#   - 돋보기 렌즈 둘레의 남은 시간 띠 (Magnifier_TimeRing : 돋보기 테두리의 크림색 띠를 오린 흰 고리. 게임에서 색을 입히고 시계처럼 줄여요)
#   python Tools/docs/art_pipeline/make_candy_jar.py
# 결과 (Assets/Sprites/UI)
#   Ending_CandyJarBack.png  : 유리 통의 뒤쪽 (사탕보다 뒤에 그려요)
#   Ending_CandyJarFront.png : 테두리 · 뚜껑 · 반짝이는 줄 (사탕보다 앞에 그려요)
# 게임의 다른 그림처럼 진갈색 굵은 테두리와 크림색을 써요. 4배 크게 그린 뒤 줄여서 테두리를 매끈하게 해요.
import os
from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Sprites", "UI")

W, H = 372, 480          # 게임에서 186 x 240 (M 62 x 80) 으로 보여요
S = 4                    # 4배로 그려서 줄이기
OUTLINE = (74, 43, 22, 255)
GLASS = (226, 242, 247, 150)
LID = (242, 176, 98, 255)
LID_DARK = (214, 140, 70, 255)
SHINE = (255, 255, 255, 200)
LINE = 9 * S

# 통 몸체 : 둥근 네모. 위쪽은 목이 살짝 좁아요
body = (22 * S, 118 * S, (W - 22) * S, (H - 12) * S)
neck = (64 * S, 82 * S, (W - 64) * S, 136 * S)
lid = (44 * S, 18 * S, (W - 44) * S, 92 * S)


def back():
    im = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(body, radius=70 * S, fill=GLASS)
    d.rounded_rectangle(neck, radius=18 * S, fill=GLASS)
    return im.resize((W, H), Image.LANCZOS)


def front():
    im = Image.new("RGBA", (W * S, H * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(body, radius=70 * S, outline=OUTLINE, width=LINE)
    d.rounded_rectangle(neck, radius=18 * S, outline=OUTLINE, width=LINE)
    # 목과 몸이 만나는 곳의 테두리를 유리색으로 덮어 한 덩어리처럼
    d.rectangle((neck[0] + LINE, 108 * S, neck[2] - LINE, 140 * S), fill=(0, 0, 0, 0))
    # 뚜껑 : 둥근 덮개 + 아래 띠
    d.rounded_rectangle(lid, radius=30 * S, fill=LID, outline=OUTLINE, width=LINE)
    d.rounded_rectangle((lid[0] + 30 * S, lid[1] + 14 * S, lid[2] - 30 * S, lid[1] + 34 * S), radius=10 * S, fill=LID_DARK)
    # 유리 반짝임 : 왼쪽 위 긴 줄과 작은 점
    d.rounded_rectangle((54 * S, 160 * S, 76 * S, 300 * S), radius=11 * S, fill=SHINE)
    d.ellipse((54 * S, 318 * S, 76 * S, 340 * S), fill=SHINE)
    return im.resize((W, H), Image.LANCZOS)


def time_ring():
    # 돋보기 테두리(Magnifier_Frame)의 크림색 띠를 그대로 오려 낸 고리예요. 그래서 게임에서 테두리에 꼭 맞게 겹쳐요.
    # 렌즈 가운데 (169, 174) 를 그림 가운데로 둔 340 x 340 (원형 채우기가 그림 가운데를 기준으로 돌아서요).
    frame = Image.open(os.path.join(ROOT, "Assets", "Sprites", "UI", "Magnifier_Frame.png")).convert("RGBA")
    cx, cy, half = 169, 174, 170
    cream = (252, 221, 162)
    im = Image.new("RGBA", (half * 2, half * 2), (0, 0, 0, 0))
    px = im.load()
    for y in range(half * 2):
        for x in range(half * 2):
            fx, fy = cx - half + x, cy - half + y
            if not (0 <= fx < frame.width and 0 <= fy < frame.height):
                continue
            d2 = (fx - cx) ** 2 + (fy - cy) ** 2
            if d2 < 118 ** 2 or d2 > 157 ** 2:      # 렌즈 구멍과 손잡이 이음새는 빼요
                continue
            r, g, b, a = frame.getpixel((fx, fy))
            dist = ((r - cream[0]) ** 2 + (g - cream[1]) ** 2 + (b - cream[2]) ** 2) ** 0.5
            k = max(0.0, min(1.0, (70 - dist) / 45))   # 크림색에 가까울수록 진하게 (가장자리는 부드럽게)
            if k > 0:
                px[x, y] = (255, 255, 255, int(255 * k * a / 255))
    return im


def dokkaebi_fire():
    import colorsys
    flame = Image.open(os.path.join(ROOT, "Assets", "Sprites", "UI", "Incense_Flame.png")).convert("RGBA")
    out = Image.new("RGBA", flame.size)
    src, dst = flame.load(), out.load()
    for y in range(flame.height):
        for x in range(flame.width):
            r, g, b, a = src[x, y]
            h, l, s_ = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            if s_ > 0.25 and 0.3 < l < 0.92:             # 진갈색 테두리 · 흰 속은 그대로 두고 불꽃 색만
                h = (h + 0.5) % 1.0                      # 주황 → 푸른빛
                s_ = min(1.0, s_ * 0.9)
            nr, ng, nb = colorsys.hls_to_rgb(h, l, s_)
            dst[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1:
        OUT = sys.argv[1]       # 미리 보기 : 다른 폴더에 저장
    back().save(os.path.join(OUT, "Ending_CandyJarBack.png"))
    front().save(os.path.join(OUT, "Ending_CandyJarFront.png"))
    time_ring().save(os.path.join(OUT, "Magnifier_TimeRing.png"))
    dokkaebi_fire().save(os.path.join(OUT, "Magnifier_DokkaebiFire.png"))
    print("saved", OUT)

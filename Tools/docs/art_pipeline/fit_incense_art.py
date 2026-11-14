# 아트팀이 그린 향 · 촛대 그림을 게임 칸에 맞춰 넣어요
#   python fit_incense_art.py <FindDokkaebi 폴더>
#   원본 : ArtSource/원본/받은그림/Incense (요청한 칸의 4배 크기)
#   그림 둘레의 빈 공간을 임시 그림(cut_incense.py)과 같게 맞춰서, 씬 배치를 바꾸지 않아도
#   줄기가 촛대 잔에 꽂히고 불씨 · 불꽃이 줄기 끝에 붙어요.
import os, sys
import numpy as np
from PIL import Image

ROOT = sys.argv[1]
SRC = os.path.join(ROOT, "ArtSource", "원본", "받은그림", "Incense")
UI = os.path.join(ROOT, "Assets", "Sprites", "UI")


def load(name):
    a = np.array(Image.open(os.path.join(SRC, name + ".png")).convert("RGBA"))
    a[a[..., 3] <= 8] = 0                                  # 거의 안 보이는 얼룩 지우기
    return Image.fromarray(a, "RGBA")


def content(im):
    return im.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()


def stretch_rows(name, top, bottom):
    """가로는 그대로, 그림 내용을 세로 top~bottom 에 꽉 차게 (줄기 · 재처럼 늘여 쓰는 그림)"""
    im = load(name)
    _, y0, _, y1 = content(im)
    part = im.crop((0, y0, im.width, y1)).resize((im.width, bottom - top), Image.LANCZOS)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.alpha_composite(part, (0, top))
    return out


def fit_box(name, box):
    """비율을 지키며 내용을 box(x0, y0, x1, y1) 안 가운데 · 위에 맞춰요"""
    im = load(name)
    part = im.crop(content(im))
    s = min((box[2] - box[0]) / part.width, (box[3] - box[1]) / part.height)
    part = part.resize((round(part.width * s), round(part.height * s)), Image.LANCZOS)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.alpha_composite(part, ((box[0] + box[2] - part.width) // 2, box[1]))
    return out


def move_bottom(name, bottom):
    """내용의 아래 끝을 bottom 줄에 놓아요 (불꽃은 아래를 기준으로 흔들려요)"""
    im = load(name)
    _, _, _, y1 = content(im)
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.alpha_composite(im, (0, bottom - y1))
    return out


# 칸 안 위치는 임시 그림과 같은 자리 (4배)
stretch_rows("Incense_Stick", 12, 2196).save(os.path.join(UI, "Incense_Stick.png"))
stretch_rows("Incense_Ash", 16, 584).save(os.path.join(UI, "Incense_Ash.png"))
fit_box("Incense_Holder", (36, 32, 484, 688)).save(os.path.join(UI, "Incense_Holder.png"))
move_bottom("Incense_Flame", 400).save(os.path.join(UI, "Incense_Flame.png"))   # 불씨 위 재에 살짝 얹히게
load("Incense_Ember").save(os.path.join(UI, "Incense_Ember.png"))
print("향 그림 5장을 Assets/Sprites/UI 에 넣었어요.")

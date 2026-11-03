# 받은 캐릭터 그림(Assets/TempArt/Characters)을 게임 폴더(Assets/Sprites/Characters)에 넣어요.
#   python connect_characters.py <FindDokkaebi 폴더>
import os, sys
from PIL import Image

ROOT = sys.argv[1]
SRC = os.path.join(ROOT, "Assets", "TempArt", "Characters")
DST = os.path.join(ROOT, "Assets", "Sprites", "Characters")

for src, dst in {"Boy": "Boy_Normal", "Boy_Happy": "Boy_Happy", "Boy_Surprised": "Boy_Surprised", "Dokkaebi": "Dokkaebi",
                 "Dokkaebi_Think": "Dokkaebi_Think", "Dokkaebi_EmptyHand": "Dokkaebi_EmptyHand"}.items():
    Image.open(os.path.join(SRC, src + ".png")).convert("RGBA").save(os.path.join(DST, dst + ".png"))

# 휘두르기 그림만 캔버스가 달라요 : 발밑 · 크기가 다른 도깨비 그림과 같아지게 980x956 으로 잘라요
swing = Image.open(os.path.join(SRC, "Dokkaebi_Swing.png")).convert("RGBA")
swing.crop((104, 102, 1084, 1058)).save(os.path.join(DST, "Dokkaebi_Swing.png"))

# 결과 화면의 작은 칸에는 표정이 보이게 얼굴만 잘라요
for name in ("Boy", "Boy_Happy", "Boy_Surprised"):
    face = "BoyFace_Normal" if name == "Boy" else "BoyFace_" + name.split("_")[1]
    Image.open(os.path.join(SRC, name + ".png")).convert("RGBA").crop((120, 100, 710, 590)).save(os.path.join(DST, face + ".png"))

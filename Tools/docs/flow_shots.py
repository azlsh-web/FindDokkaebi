# 게임 플로우 사진 찾기. 문서 스크립트들이 같이 써요.
#   사진은 Unity 메뉴 "게임 플로우 사진 찍기"(MvpFlowShots)가 Temp/FlowShots 에 찍고,
#   make_flow_doc.py 가 줄여서 Docs/기획/플로우_사진 으로 옮겨요.
import base64
import csv
import io
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
PHOTOS = os.path.join(ROOT, "Docs", "기획", "플로우_사진")


def load_manifest():
    path = os.path.join(PHOTOS, "manifest.tsv")
    with open(path, encoding="utf-8", newline="") as f:
        return [dict(zip(("file", "section", "caption", "line"), row)) for row in csv.reader(f, delimiter="\t", quotechar=None)]


def find(section, caption):
    """구역과 설명 일부로 사진 한 장을 찾아요. 없으면 멈춰요 (문서가 없는 화면을 가리키지 않게)."""
    for row in load_manifest():
        if row["section"].startswith(section) and caption in row["caption"]:
            return os.path.join(PHOTOS, row["file"])
    raise KeyError((section, caption))


def data_url(path, width=None, quality=82):
    im = Image.open(path).convert("RGB")
    if width and im.width > width:
        im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=quality)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()

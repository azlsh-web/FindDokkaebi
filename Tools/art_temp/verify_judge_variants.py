"""조합 그림의 누락·투명도·규격을 확인하고, 눈으로 검수할 모음 그림을 만들어요.

py Tools/art_temp/verify_judge_variants.py [--allow-incomplete]
게임 그림은 수정하지 않아요. 모음 그림은 검수용으로만 저장해요.
"""

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/JudgeVariants"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--allow-incomplete", action="store_true")
    args = parser.parse_args()
    manifest = json.loads((ART / "manifest.json").read_text(encoding="utf-8-sig"))
    report = {"checked": 0, "missing": [], "errors": [], "sheets": []}
    qa = ART / "qa"
    qa.mkdir(exist_ok=True)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 20)
    for item in manifest["items"]:
        if item["chapter"] == 1 or not item["judged_in_current_flow"]:
            continue
        complete = True
        hashes = set()
        for variant in item["variants"]:
            if not variant["absent"]:
                continue
            path = ROOT / variant["file"]
            if not path.is_file():
                report["missing"].append(variant["file"])
                complete = False
                continue
            with Image.open(path) as image:
                problem = None
                if image.mode != "RGBA" or image.size != (1024, 1024):
                    problem = f"invalid image format: {image.mode}, {image.size}"
                else:
                    alpha = image.getchannel("A")
                    corners = [(0, 0), (1023, 0), (0, 1023), (1023, 1023)]
                    if alpha.getextrema() != (0, 255) or any(alpha.getpixel(p) for p in corners):
                        problem = "invalid alpha or opaque corner"
                    elif alpha.histogram()[0] < 1024 * 1024 * 0.1:
                        problem = "insufficient empty transparent background"
                if problem:
                    report["errors"].append({"file": variant["file"], "error": problem})
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            if digest in hashes:
                report["errors"].append({"file": variant["file"], "error": "duplicate variant"})
            hashes.add(digest)
            report["checked"] += 1
        if not complete:
            continue
        # 원본과 각 결과를 함께 보는 검수용 모음이에요. 게임 에셋에는 저장하지 않아요.
        tile, gap, title = 260, 36, 36
        sheet = Image.new("RGB", (tile * 4, title + (tile + gap) * 2), "#ece7df")
        draw = ImageDraw.Draw(sheet)
        draw.text((12, 8), item["item"] + " : " + " / ".join(f["id"] for f in item["features"]), fill="black", font=font)
        for index, variant in enumerate(item["variants"]):
            x, y = (index % 4) * tile, title + (index // 4) * (tile + gap)
            with Image.open(ROOT / variant["file"]) as image:
                preview = image.convert("RGBA")
                preview.thumbnail((tile - 12, tile - 12), Image.Resampling.LANCZOS)
                sheet.paste(preview, (x + (tile - preview.width) // 2, y), preview)
            draw.text((x + 10, y + tile), variant["mask"], fill="black", font=font)
        target = qa / (item["item"] + ".jpg")
        sheet.save(target, quality=92)
        report["sheets"].append(target.relative_to(ROOT).as_posix())
    (ART / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"checked={report['checked']}, missing={len(report['missing'])}, errors={len(report['errors'])}, sheets={len(report['sheets'])}")
    if report["errors"] or (report["missing"] and not args.allow_incomplete):
        raise SystemExit(1)


if __name__ == "__main__":
    main()

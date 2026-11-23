"""현재 StageData의 모든 O/X 조합과 제작할 그림을 목록으로 저장해요.

실행: py Tools/art_temp/build_judge_variant_manifest.py
원본과 게임 데이터는 변경하지 않아요. 그림 생성이나 AI 판정 요청도 하지 않아요.
"""

import hashlib
import itertools
import json
import re
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "ArtSource" / "JudgeVariants" / "manifest.json"
# 5챕터 요청 물건은 고르면 바로 빼앗겨요. 현재 흐름에서 그림 판정을 받지 않아요.
REQUEST_ONLY = {"Ch5_Jipsin", "Ch5_Buchae", "Ch5_Gamtu"}
LEGACY_ALL_WRONG = {
    "Stage1_Bat": "Stage1_Bat/Bat_Wrong_X.png",
    "Stage2_Bag": "Stage2_Bag/Bag_Wrong_XX.png",
    "Stage3_Mat": "Stage3_Mat/Mat_Wrong_XXX.png",
    "Stage4_Gimbap": "Stage4_Gimbap/Gimbap_Wrong_XXXX.png",
    "Stage5_Friend": "Stage5_Friend/Friend_Wrong_XXXXX.png",
}


def relative(path):
    return path.relative_to(ROOT).as_posix()


def build():
    reviewed = set()
    generation_log = OUT.with_name("generation_log.json")
    if generation_log.exists():
        log = json.loads(generation_log.read_text(encoding="utf-8-sig"))
        reviewed = {a["target"] for a in log.get("attempts", []) if a.get("visual_review") == "pass"}
    sprites = {}
    for meta in (ROOT / "Assets/Sprites").rglob("*.png.meta"):
        match = re.search(r"^guid: (\w+)$", meta.read_text(encoding="utf-8"), re.M)
        if match:
            sprites[match[1]] = Path(str(meta)[:-5])

    items = []
    for asset in sorted((ROOT / "Assets/Data").glob("*.asset")):
        if asset.stem.startswith("Chapter"):
            continue
        text = asset.read_text(encoding="utf-8-sig")
        # Unity YAML 머리글만 빼고 실제 값은 그대로 읽어요.
        data = yaml.safe_load("\n".join(line for line in text.splitlines()
                                       if not line.startswith(("%", "---"))))["MonoBehaviour"]
        if "features" not in data:
            continue
        original = sprites[data["originalSprite"]["guid"]]
        chapter = 1 if asset.stem.startswith("Stage") else int(asset.stem[2])
        features = [{"id": f["id"], "name": f["name"], "criteria": f["criteria"]}
                    for f in data["features"]]
        variants = []
        for bits in itertools.product("OX", repeat=len(features)):
            mask = "".join(bits)
            full_success = all(bit == "O" for bit in bits)
            required = asset.stem not in REQUEST_ONLY or full_success
            if full_success:
                target = original
                status = "original"
            elif chapter == 1:
                target = ROOT / "Assets/Sprites" / LEGACY_ALL_WRONG[asset.stem]
                status = "reuse_original_all_wrong"
            else:
                target = original.with_name(f"{asset.stem}_Wrong_{mask}.png")
                status = "reviewed" if target.exists() and relative(target) in reviewed else "generated_unreviewed" if target.exists() else "needs_generation"
                if not required:
                    status = "not_judged_in_current_flow"
            variants.append({
                "mask": mask,
                "present": [f["id"] for f, bit in zip(features, bits) if bit == "O"],
                "absent": [f["id"] for f, bit in zip(features, bits) if bit == "X"],
                "file": relative(target),
                "exists": target.exists(),
                "status": status,
                "required_for_current_flow": required,
            })
        items.append({
            "item": asset.stem, "chapter": chapter, "target": data["targetName"],
            "judged_in_current_flow": asset.stem not in REQUEST_ONLY,
            "stage_data": relative(asset), "original": relative(original),
            "original_sha256": hashlib.sha256(original.read_bytes()).hexdigest(),
            "features": features, "variants": variants,
        })

    document = {
        "basis": "C-57: user-approved result-color judging, grouped color criterion, chapter 5 three-primary mixtures.",
        "mask_order": "StageData.features order (F1, F2, ...); O=present, X=absent",
        "generation": "Built-in image_gen, original referenced as edit target, one call per variant.",
        "items": items,
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{relative(OUT)}: {len(items)} items, "
          f"{sum(len(item['variants']) for item in items)} data combinations, "
          f"{sum(v['required_for_current_flow'] for item in items for v in item['variants'])} used in current flow")


if __name__ == "__main__":
    build()

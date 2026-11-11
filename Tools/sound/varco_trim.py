# VARCO Sound 에서 받은 효과음 후보를 게임용으로 다듬어요
#   python Tools/sound/varco_trim.py            → ArtSource/Sound/varco/trimmed/*.wav + 듣기 페이지 index.html
#   python Tools/sound/varco_trim.py --install  → 고른 후보(PICK)를 Assets/Resources/Sound/<이름>.wav 로 복사
# 원본: ArtSource/Sound/varco/raw/varco_<이름>_<1~3>.mp3 (VARCO 무료 플랜, 한 번 생성에 후보 3개)
# 하는 일: 첫 소리 바로 앞에서 자르기 → 첫 소리가 끝나는 곳(또는 최대 길이)에서 자르기 → 끝 페이드 → 최대 음량 맞추기
import glob
import os
import shutil
import subprocess
import sys
import wave

import numpy as np

SR = 48000
HOP = 240  # 5ms
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtSource", "Sound", "varco", "raw")
OUT = os.path.join(ROOT, "ArtSource", "Sound", "varco", "trimmed")
GAME = os.path.join(ROOT, "Assets", "Resources", "Sound")

# 이름: (최대 길이 초, 첫 소리 하나만?, 최대 음량 dB, 나오는 때)
SPEC = {
    "sfx_tap": (0.15, True, -8, "대사 넘기기 · 버튼"),
    "sfx_talk": (0.06, True, -18, "대사 글자 (1초 15번)"),
    "sfx_count": (0.25, True, -5, "관찰 3, 2, 1"),
    "sfx_snack_in": (0.9, False, -5, "간식 들어옴"),
    "sfx_magnifier": (1.0, False, -5, "돋보기 (초콜릿 1개)"),
    "sfx_submit": (0.7, False, -4, "다 그렸어! (종이 접기)"),
    "sfx_give": (0.9, False, -4, "쪽지 · 사탕 건네기"),
    "sfx_item": (0.5, True, -4, "물건 등장"),
    "sfx_success": (1.5, False, -3, "전부 맞음"),
    "sfx_fail": (1.2, False, -3, "틀림"),
    "sfx_mark": (1.5, False, -5, "채점표"),
    "sfx_cheer": (2.5, False, -5, "이야기 종료"),
    "sfx_land": (0.3, True, -5, "소풍 물건 놓임"),
}

# 게임에 넣을 후보 번호 (듣기 페이지에서 골라 바꿔요)
# 처음에는 길이가 사운드팀 문서의 권장 길이에 가장 가까운 후보로 골랐어요.
PICK = {
    "sfx_tap": 2, "sfx_talk": 3, "sfx_count": 3, "sfx_snack_in": 1, "sfx_magnifier": 1,
    "sfx_submit": 1, "sfx_give": 3, "sfx_item": 3, "sfx_success": 1, "sfx_fail": 2,
    "sfx_mark": 1, "sfx_cheer": 1, "sfx_land": 2,
}


def load(path):
    pcm = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(pcm, np.float32).astype(float)


def envelope_db(x):
    n = len(x) // HOP
    rms = np.sqrt(np.mean(x[: n * HOP].reshape(n, HOP) ** 2, axis=1)) + 1e-9
    return 20 * np.log10(rms / rms.max())


def trim(x, max_sec, single):
    db = envelope_db(x)
    start = int(np.argmax(db > -30))
    end = len(db)
    if single:
        # 첫 소리가 충분히 작아지면(-40dB) 끝. 다음 타격이 바로 이어지면 그 앞에서 끝.
        for i in range(start + 1, len(db) - 3):
            if db[i] < -40 or (i > start + 4 and db[i] < -26 and db[i + 1:i + 3].max() > db[i] + 10):
                end = i
                break
    else:
        # 여러 번 나는 소리: 최대 길이 안에서 뒤쪽 무음만 잘라요
        last = start + int(max_sec * SR / HOP)
        loud = np.flatnonzero(db[start:last] > -40)
        end = start + (loud[-1] + 1 if len(loud) else 1)
    s = max(0, start * HOP - int(0.003 * SR))
    e = min(len(x), end * HOP, s + int(max_sec * SR))
    y = x[s:e].copy()
    fade_in = min(len(y), int(0.002 * SR))
    y[:fade_in] *= np.linspace(0, 1, fade_in)
    fade_out = min(len(y), max(int(0.01 * SR), int(len(y) * 0.2)))
    y[-fade_out:] *= np.linspace(1, 0, fade_out) ** 1.5
    return y


def write_wav(path, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def page(rows):
    body = []
    for name, (_, _, _, where) in SPEC.items():
        cells = []
        for n, length in rows.get(name, []):
            mark = " pick" if PICK[name] == n else ""
            cells.append(f"<td class='c{mark}'><div>{n}번 · {length:.2f}초</div><audio controls preload='auto' src='{name}_{n}.wav'></audio></td>")
        body.append(f"<tr><td><code>{name}</code><br><small>{where}</small></td>{''.join(cells)}</tr>")
    return f"""<!doctype html><html lang='ko'><meta charset='utf-8'><title>VARCO 효과음 후보</title>
<style>body{{font-family:'Malgun Gothic',sans-serif;background:#faf7f2;color:#2b2622;margin:20px}}td{{padding:6px 8px;border-bottom:1px solid #e3dcd2;vertical-align:top}}
.c div{{font-size:12px;color:#6d645c}}.pick{{background:#fbecc8}}.pick div::after{{content:' ← 게임에 넣음';color:#b0761a;font-weight:bold}}audio{{width:210px;height:32px}}small{{color:#6d645c}}</style>
<h2>VARCO 효과음 후보 (다듬은 뒤)</h2><p>노란 칸이 지금 게임에 들어간 후보예요. 바꾸고 싶은 소리는 이름과 번호를 알려 주세요.</p>
<table>{''.join(body)}</table></html>"""


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    rows = {}
    for name, (max_sec, single, peak_db, _) in SPEC.items():
        for path in sorted(glob.glob(os.path.join(RAW, f"varco_{name}_*.mp3"))):
            n = int(path[-5])
            y = trim(load(path), max_sec, single)
            y = y / (np.max(np.abs(y)) + 1e-12) * 10 ** (peak_db / 20)
            write_wav(os.path.join(OUT, f"{name}_{n}.wav"), y)
            rows.setdefault(name, []).append((n, len(y) / SR))
        print(f"{name:14s} " + "  ".join(f"{n}:{l:.2f}s" for n, l in rows.get(name, [])))
    open(os.path.join(OUT, "index.html"), "w", encoding="utf-8").write(page(rows))
    if "--install" in sys.argv:
        for name, n in PICK.items():
            shutil.copy(os.path.join(OUT, f"{name}_{n}.wav"), os.path.join(GAME, name + ".wav"))
        print("→", GAME)

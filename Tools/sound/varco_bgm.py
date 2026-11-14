# VARCO Sound 에서 받은 배경음을 게임용으로 다듬어요 (C-28)
#   python Tools/sound/varco_bgm.py   → Assets/Resources/Sound/<이름>.ogg
# 원본: ArtSource/Sound/varco/raw/varco_<이름>_<번호>.mp3 (한 번 생성에 후보 2개, 고른 번호는 PICK)
# 하는 일: 앞 무음 자르기 → 곡이 자연스럽게 끝나는 여운까지만 남기기 → 끝 짧은 페이드 → 최대 음량 맞추기
# 게임은 AudioSource.loop 로 처음부터 다시 틀어요. 곡 중간을 겹치면 화음이 섞여 탁해져서, 곡 끝 여운 뒤에 처음으로 돌아가게 해요.
import os
import subprocess

import numpy as np

SR = 44100
HOP = 441  # 10ms
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtSource", "Sound", "varco", "raw")
GAME = os.path.join(ROOT, "Assets", "Resources", "Sound")

PEAK_DB = -3   # 게임에서 절반 음량(Sfx.BgmBase)으로 한 번 더 줄여요
PICK = {"bgm_room": 1, "bgm_draw": 1, "bgm_judge": 2, "bgm_picnic": 1}


def load(path):
    pcm = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(pcm, np.float32).astype(float).reshape(-1, 2)


def trim(x):
    mono = x.mean(1)
    n = len(mono) // HOP
    rms = np.sqrt((mono[: n * HOP].reshape(n, HOP) ** 2).mean(1)) + 1e-9
    db = 20 * np.log10(rms / rms.max())
    loud = np.flatnonzero(db > -50)
    s = max(0, loud[0] * HOP - int(0.01 * SR))
    e = min(len(x), (loud[-1] + 1) * HOP)
    y = x[s:e].copy()
    fade = int(0.3 * SR)
    y[-fade:] *= np.linspace(1, 0, fade)[:, None]
    y[: int(0.005 * SR)] *= np.linspace(0, 1, int(0.005 * SR))[:, None]
    return y / np.abs(y).max() * 10 ** (PEAK_DB / 20)


def write_ogg(path, y):
    pcm = np.clip(y, -1, 1).astype("<f4").tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "5", path], input=pcm, check=True)


if __name__ == "__main__":
    for name, n in PICK.items():
        y = trim(load(os.path.join(RAW, f"varco_{name}_{n}.mp3")))
        out = os.path.join(GAME, name + ".ogg")
        write_ogg(out, y)
        print(f"{name} ← {n}번  {len(y) / SR:.1f}초  → {out}")

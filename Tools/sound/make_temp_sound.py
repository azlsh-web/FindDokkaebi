# 사운드팀 대신 만든 임시 소리 8개 (2026-11-25, C-65)
#   py Tools/sound/make_temp_sound.py [출력 폴더]   → 기본 Assets/Resources/Sound/
# 기존 VARCO 소리를 ffmpeg 로 바꾸거나(bgm_curse 와 같은 방식), numpy 로 직접 합성해요.
# 같은 결과가 나오게 난수 씨앗을 고정했어요. 만든 방법은 Docs/3_전달/요청_현황.md 에도 적혀요.
import os
import subprocess
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GAME = os.path.join(ROOT, "Assets", "Resources", "Sound")
SFX_SR, BGM_SR = 48000, 44100
rng = np.random.default_rng(20261125)


# ── 읽기 · 쓰기 ─────────────────────────────────────────────
def load(name, sr, ch, af=None):
    """게임 소리 파일을 ffmpeg 필터(af)를 거쳐 float 배열로 (n, ch)"""
    cmd = ["ffmpeg", "-v", "error", "-i", os.path.join(GAME, name)]
    if af:
        cmd += ["-af", af]
    cmd += ["-f", "f32le", "-ac", str(ch), "-ar", str(sr), "-"]
    pcm = subprocess.run(cmd, capture_output=True, check=True).stdout
    return np.frombuffer(pcm, np.float32).astype(float).reshape(-1, ch)


def peak(y, db):
    return y / (np.abs(y).max() + 1e-9) * 10 ** (db / 20)


def fade(y, sr, a=0.005, b=0.05):
    y = y.copy()
    na, nb = int(a * sr), int(b * sr)
    if na:
        y[:na] *= np.linspace(0, 1, na)[:, None]
    if nb:
        y[-nb:] *= np.linspace(1, 0, nb)[:, None]
    return y


def write(path, y, sr):
    ch = y.shape[1]
    pcm = np.clip(y, -1, 1).astype("<f4").tobytes()
    codec = ["-c:a", "libvorbis", "-q:a", "5"] if path.endswith(".ogg") else ["-c:a", "pcm_s16le"]
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", str(ch), "-ar", str(sr), "-i", "-"] + codec + [path],
                   input=pcm, check=True)


# ── 합성 부품 ───────────────────────────────────────────────
def t_axis(sec, sr=SFX_SR):
    return np.arange(int(sec * sr)) / sr


def bell(freq, sec, decay=3.0, sr=SFX_SR):
    """실로폰 · 글로켄 느낌 : 사인 + 2.76배 배음, 빠르게 줄어듦"""
    t = t_axis(sec, sr)
    env = np.exp(-decay * t) * (1 - np.exp(-t * 400))
    return env * (np.sin(2 * np.pi * freq * t) + 0.35 * np.sin(2 * np.pi * freq * 2.76 * t) * np.exp(-6 * t))


def soft(freq, sec, decay=1.5, sr=SFX_SR, vib=0.0):
    """부드러운 관악기 느낌 : 사인 + 약한 홀수 배음, 느린 시작"""
    t = t_axis(sec, sr)
    f = freq * (1 + vib * np.sin(2 * np.pi * 5 * t))
    ph = 2 * np.pi * np.cumsum(f) / sr
    env = np.exp(-decay * t) * (1 - np.exp(-t * 40))
    return env * (np.sin(ph) + 0.25 * np.sin(3 * ph) + 0.1 * np.sin(5 * ph))


def place(out, clip, at, sr=SFX_SR):
    s = int(at * sr)
    e = min(len(out), s + len(clip))
    out[s:e] += clip[: e - s]


def lowpass(x, cutoff, sr):
    """한 번 거르는 간단한 저역 통과"""
    a = np.exp(-2 * np.pi * cutoff / sr)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def reverb(y, sr, wet=0.3, length=1.2):
    """노이즈 꼬리를 곱해서 만든 간단한 울림"""
    n = int(length * sr)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / sr * 5)
    ir[0] = 0
    size = 1 << (len(y) + n).bit_length()
    wetsig = np.fft.irfft(np.fft.rfft(y, size) * np.fft.rfft(ir, size), size)[: len(y)]
    return y + wet * wetsig / (np.abs(wetsig).max() + 1e-9) * np.abs(y).max()


def mono(y):
    return y[:, None]


# ── 효과음 5개 ──────────────────────────────────────────────
def sfx_interfere():
    """빨간 낙서 : 연필 소리(sfx_mark)를 낮고 거칠게, 짧게"""
    y = load("sfx_mark.wav", SFX_SR, 1, "asetrate=48000*0.78,aresample=48000,atrim=0:0.75,"
             "highpass=f=180,acrusher=bits=10:mix=0.35,afade=t=out:st=0.55:d=0.2")
    return fade(peak(y, -6), SFX_SR)


def sfx_scare():
    """놀래키기 : 낮은 쿵 + 반음 부딪히는 화음 한 번, 0.9초 (너무 크지 않게 -4dB)"""
    sec = 0.9
    t = t_axis(sec)
    thump = np.sin(2 * np.pi * (70 * t - 25 * t * t)) * np.exp(-7 * t)
    stab = sum(soft(f, sec, decay=3.5) for f in (293.7, 311.1, 415.3, 440.0)) / 4
    noise = lowpass(rng.standard_normal(len(t)), 2500, SFX_SR) * np.exp(-14 * t) * 0.6
    y = 1.0 * thump + 0.9 * stab + noise
    return fade(mono(peak(reverb(y, SFX_SR, 0.25, 0.8), -4)), SFX_SR, 0.002, 0.1)


def sfx_heart_loss():
    """하트 감소 : '띠-둥' 내려가는 두 음 + 작은 깨지는 소리, 0.7초"""
    out = np.zeros(int(0.7 * SFX_SR))
    place(out, bell(659.3, 0.3, 6), 0.0)
    place(out, bell(415.3, 0.55, 5), 0.13)
    crack = rng.standard_normal(int(0.06 * SFX_SR)) * np.exp(-np.arange(int(0.06 * SFX_SR)) / SFX_SR * 60)
    place(out, 0.35 * np.diff(crack, prepend=0), 0.13)
    return fade(mono(peak(out, -5)), SFX_SR, 0.002, 0.08)


def sfx_ending_happy():
    """해피 엔딩 : 위로 올라가는 실로폰(도미솔도) 뒤에 따뜻한 화음, 3초 (저주가 풀리는 안도감)"""
    sec = 3.0
    out = np.zeros(int(sec * SFX_SR))
    for i, f in enumerate((523.3, 659.3, 784.0, 1046.5)):
        place(out, bell(f, 1.6, 2.5), i * 0.12)
    for f in (523.3, 659.3, 784.0):
        place(out, 0.5 * soft(f, 2.4, decay=1.0, vib=0.004), 0.5)
    place(out, 0.5 * bell(1568.0, 1.2, 2.0), 0.5)
    return fade(mono(peak(reverb(out, SFX_SR, 0.35, 1.5), -4)), SFX_SR, 0.002, 0.4)


def sfx_gameover():
    """게임오버 : 느리게 내려가는 단조(솔 미♭ 도) 뒤 낮은 울림, 3.2초 (위협 · 좌절)"""
    sec = 3.2
    out = np.zeros(int(sec * SFX_SR))
    for i, f in enumerate((392.0, 311.1, 261.6)):
        place(out, soft(f, 1.2, decay=2.0, vib=0.006), i * 0.38)
    t = t_axis(2.2)
    drone = (np.sin(2 * np.pi * 65.4 * t) + 0.5 * np.sin(2 * np.pi * 69.3 * t)) * np.exp(-1.3 * t) * (1 - np.exp(-t * 20))
    place(out, 1.1 * drone, 1.05)
    place(out, 0.4 * soft(246.9, 1.8, decay=1.5), 1.1)
    return fade(mono(peak(reverb(out, SFX_SR, 0.4, 1.8), -4)), SFX_SR, 0.002, 0.5)


# ── 배경 3개 ────────────────────────────────────────────────
def loop_crossfade(y, sr, xf=2.0):
    """끝 xf초를 처음과 겹쳐서 이음새 없이 반복되게"""
    n = int(xf * sr)
    head, body, tail = y[:n], y[n:-n], y[-n:]
    w = np.linspace(0, 1, n)[:, None]
    return np.concatenate([tail * (1 - w) + head * w, body])


def amb_rain():
    """2챕터 비 : 걸러 낸 노이즈(쏴아) + 띄엄띄엄 작은 빗방울, 32초 반복. 방 배경음 위에 겹쳐 깔아요"""
    sec, sr = 34.0, BGM_SR
    n = int(sec * sr)
    out = np.zeros((n, 2))
    for c in range(2):
        w = rng.standard_normal(n)
        hiss = lowpass(w, 5000, sr) - lowpass(w, 500, sr)          # 500~5000Hz
        slow = 1 + 0.15 * np.sin(2 * np.pi * (0.07 + 0.02 * c) * np.arange(n) / sr)
        out[:, c] = hiss * slow
    drops = rng.uniform(0, sec - 0.1, int(sec * 9))
    for d in drops:
        f = rng.uniform(1800, 4200)
        clip = bell(f, 0.04, 90, sr) * rng.uniform(0.15, 0.45)
        s = int(d * sr)
        pan = rng.uniform(0.2, 0.8)
        out[s:s + len(clip), 0] += clip * (1 - pan) * 2
        out[s:s + len(clip), 1] += clip * pan * 2
    return peak(loop_crossfade(out, sr), -9)


def bgm_variant(src, af):
    y = load(src, BGM_SR, 2, af)
    return fade(peak(y, -3), BGM_SR, 0.005, 0.3)


def bgm_evening():
    """3챕터 저녁 : bgm_room 을 조금 느리고 낮게(반음 아래) + 높은 소리를 줄여 어둑하게"""
    return bgm_variant("bgm_room.ogg", "asetrate=44100*0.944,aresample=44100,lowpass=f=3200")


def bgm_night():
    """4챕터 밤 : bgm_room 을 더 느리고 낮게(세 반음 아래) + 어두운 소리 + 울림"""
    return bgm_variant("bgm_room.ogg", "asetrate=44100*0.84,aresample=44100,lowpass=f=1900,"
                       "aecho=0.8:0.6:140|290:0.35|0.22")


MAKERS = {
    "sfx_interfere.wav": (sfx_interfere, SFX_SR),
    "sfx_scare.wav": (sfx_scare, SFX_SR),
    "sfx_heart_loss.wav": (sfx_heart_loss, SFX_SR),
    "sfx_ending_happy.wav": (sfx_ending_happy, SFX_SR),
    "sfx_gameover.wav": (sfx_gameover, SFX_SR),
    "amb_rain.ogg": (amb_rain, BGM_SR),
    "bgm_evening.ogg": (bgm_evening, BGM_SR),
    "bgm_night.ogg": (bgm_night, BGM_SR),
}

if __name__ == "__main__":
    out_dir = sys.argv[1] if len(sys.argv) > 1 else GAME
    os.makedirs(out_dir, exist_ok=True)
    for name, (make, sr) in MAKERS.items():
        y = make()
        path = os.path.join(out_dir, name)
        write(path, y, sr)
        print(f"{name:22s} {len(y) / sr:6.2f}초  최대 {20 * np.log10(np.abs(y).max()):5.1f}dB  → {path}")

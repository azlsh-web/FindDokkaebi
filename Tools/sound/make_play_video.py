# 플레이 녹화(MvpPlayRecorder)를 소리 있는 mp4 로 묶어요
#   python Tools/sound/make_play_video.py            → Temp/Recording 아래 다 끝난 녹화 전부
#   python Tools/sound/make_play_video.py 1챕터 4챕터 → 그 이름만
# 입력: Temp/Recording/<이름>/frame_*.jpg (초당 24장), audio.f32 (32비트 실수 PCM), audio.txt ("샘플레이트 채널수"), done.txt
# 출력: Recordings/플레이영상_<이름>.mp4 (git 에 안 올려요)
import os
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REC = os.path.join(ROOT, "Temp", "Recording")
OUT = os.path.join(ROOT, "Recordings")
FPS = 24


def make(name):
    src = os.path.join(REC, name)
    if not os.path.exists(os.path.join(src, "done.txt")):
        print(f"{name} : 녹화가 끝나지 않았어요 (done.txt 없음), 건너뛰어요")
        return
    out = os.path.join(OUT, f"플레이영상_{name}.mp4")
    rate, ch = open(os.path.join(src, "audio.txt")).read().split()
    frames = len([f for f in os.listdir(src) if f.startswith("frame_")])
    cmd = ["ffmpeg", "-v", "error", "-y",
           "-framerate", str(FPS), "-i", os.path.join(src, "frame_%05d.jpg"),
           "-f", "f32le", "-ar", rate, "-ac", ch, "-i", os.path.join(src, "audio.f32"),
           "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
           "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", out]
    subprocess.run(cmd, check=True)
    print(f"{out}  ({frames}장, {frames / FPS / 60:.1f}분)")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    names = sys.argv[1:] or sorted(d for d in os.listdir(REC) if os.path.isdir(os.path.join(REC, d)))
    for n in names:
        make(n)

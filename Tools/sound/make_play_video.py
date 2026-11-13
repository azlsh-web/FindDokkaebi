# 플레이 녹화(MvpPlayRecorder)를 소리 있는 mp4 로 묶어요
#   python Tools/sound/make_play_video.py [출력.mp4]
# 입력: Temp/Recording/frame_*.jpg (초당 24장), audio.f32 (32비트 실수 PCM), audio.txt ("샘플레이트 채널수")
# 출력: 기본은 Recordings/플레이영상_<날짜>.mp4 (git 에 안 올려요)
import os
import subprocess
import sys
from datetime import date

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REC = os.path.join(ROOT, "Temp", "Recording")
FPS = 24

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Recordings", f"플레이영상_{date.today()}.mp4")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    rate, ch = open(os.path.join(REC, "audio.txt")).read().split()
    frames = len([f for f in os.listdir(REC) if f.startswith("frame_")])
    cmd = ["ffmpeg", "-v", "error", "-y",
           "-framerate", str(FPS), "-i", os.path.join(REC, "frame_%05d.jpg"),
           "-f", "f32le", "-ar", rate, "-ac", ch, "-i", os.path.join(REC, "audio.f32"),
           "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
           "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", out]
    subprocess.run(cmd, check=True)
    print(f"{out}  ({frames}장, {frames / FPS / 60:.1f}분)")

# 게임이 바뀌면 문서를 한 번에 다시 만들어요. AI 판정은 보내지 않아요.
#   python Tools/docs/update_docs.py              코드에서 만드는 문서 전부
#   python Tools/docs/update_docs.py --shots      Unity 로 플로우 사진까지 다시 찍고 전부
#   python Tools/docs/update_docs.py --pre-commit 커밋 때 자동 실행 (.githooks/pre-commit)
#       커밋에 게임 파일이 있으면 문서를 다시 만들어 그 커밋에 함께 넣어요.
#       화면이 달라지는 파일이면 사진도 다시 찍어요. 같은 파일 내용으로 끝낸 촬영은 재사용해요.
#   커밋 전에 손으로 --shots 를 돌릴 필요는 없어요. 훅이 알아서 해요 (Unity 만 켜 두기).
#   --dry 를 붙이면 무엇을 할지만 보여 줘요. --files 뒤에 파일을 적으면 그 파일이 바뀐 것으로 쳐요 (시험용).
import os
import hashlib
import json
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paths

ROOT = paths.ROOT
TOOLS = os.path.dirname(os.path.abspath(__file__))
TEMP = os.path.join(ROOT, "Temp")
sys.stdout.reconfigure(encoding="utf-8")

# 플레이 화면이 달라질 수 있는 파일 : 사진까지 다시 찍어요
SCREEN = ("Assets/Scripts/", "Assets/Scenes/", "Assets/Sprites/", "Assets/Data/", "Assets/Editor/MvpSceneBuilder", "Assets/Editor/LobbySceneBuilder", "Assets/Editor/MvpFlowShots", "Assets/Fonts/")
# 문서를 만드는 데이터 : 사진 없이 문서만 다시 만들어요
DOC_DATA = ("Assets/Resources/", "Tools/docs/request_data.py", "Tools/docs/art_tree_data.py", "Tools/docs/make_", "Tools/docs/dialogue_lines.py", "Tools/docs/flow_shots.py", "Tools/docs/doc_pdf.py", "Tools/docs/paths.py")

SCRIPTS = ["make_dialogue_tree.py", "make_dialogue_sheet.py", "make_art_structure.py", "make_flow_doc.py",
           "make_requests.py", "make_arch_doc.py"]
GENERATED = [paths.repo(p) for p in paths.GENERATED]
REMINDER = """
[문서] 사람이 판단해야 하는 내용은 자동으로 바뀌지 않아요. 달라졌다면 함께 고쳐 주세요.
""" + "\n".join(f"  - Docs/{p}" for p in paths.HAND_WRITTEN) + """
  - 아키텍처 설명 글 : Tools/docs/make_arch_doc.py
  - 팀에 공유한 링크 페이지 : 전달 문서를 다시 올려야 해요"""


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", check=True).stdout


def unity_open():
    return os.path.exists(os.path.join(TEMP, "UnityLockfile"))


def screen_signature():
    """내용과 경로를 기록해 삭제·이름 변경·과거 파일 복원도 알아봐요."""
    digest = hashlib.sha256()
    for folder in ("Assets/Scripts", "Assets/Scenes", "Assets/Sprites", "Assets/Data", "Assets/Editor", "Assets/Fonts"):
        for base, dirs, files in os.walk(os.path.join(ROOT, folder)):
            dirs.sort()
            for name in sorted(files):
                path = os.path.join(base, name)
                rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
                if rel.startswith(SCREEN):
                    digest.update(rel.encode("utf-8") + b"\0")
                    with open(path, "rb") as fp:
                        digest.update(hashlib.file_digest(fp, "sha256").digest())
    return digest.hexdigest()


def shots_complete():
    done = os.path.join(TEMP, "FlowShots", "done.txt")
    manifest = os.path.join(TEMP, "FlowShots", "manifest.tsv")
    if not os.path.isfile(done) or not os.path.isfile(manifest):
        return False
    with open(done, encoding="utf-8") as fp:
        if "오류 0개" not in fp.read():
            return False
    with open(manifest, encoding="utf-8") as fp:
        rows = [line.split("\t")[0] for line in fp if line.strip()]
    return bool(rows) and all(os.path.isfile(os.path.join(TEMP, "FlowShots", f)) for f in rows)


def shots_fresh():
    cache = os.path.join(TEMP, "FlowShots", "inputs.json")
    if not shots_complete() or not os.path.isfile(cache):
        return False
    try:
        with open(cache, encoding="utf-8") as fp:
            return json.load(fp) == {"signature": screen_signature(), "done": os.path.getmtime(os.path.join(TEMP, "FlowShots", "done.txt"))}
    except (ValueError, OSError):
        return False


def take_shots():
    """사진을 찍었으면 True. Unity 가 꺼져 있거나 바쁘면 False (커밋을 막지는 않아요)."""
    if not unity_open():
        print("[문서] 경고 : Unity 가 꺼져 있어서 플로우 사진을 다시 찍지 못했어요. 사진은 이전 그대로예요.")
        return False
    before = screen_signature()
    flag = os.path.join(TEMP, "mvp_flowshots.flag")
    done = os.path.join(TEMP, "FlowShots", "done.txt")
    if os.path.exists(done):
        os.remove(done)
    open(flag, "w").close()
    print("[문서] Unity 에서 플로우 사진을 찍어요. 5분쯤 걸려요 (AI 판정 없음).", flush=True)
    end = time.time() + 120
    while os.path.exists(flag):
        if time.time() > end:
            os.remove(flag)
            print("[문서] 경고 : Unity 가 Play 중이거나 바빠서 사진을 찍지 못했어요. 사진은 이전 그대로예요.")
            return False
        time.sleep(2)
    end = time.time() + 900
    while not os.path.exists(done):
        if time.time() > end:
            raise SystemExit("[문서] 사진 촬영이 15분 안에 끝나지 않았어요. Unity 를 확인해 주세요.")
        time.sleep(2)
    if not shots_complete():
        raise SystemExit("[문서] 사진 촬영이 오류 없이 끝나지 않았어요. 결과를 확인해 주세요.")
    if screen_signature() != before:
        raise SystemExit("[문서] 촬영 중 게임 파일이 바뀌었어요. 저장을 마친 뒤 다시 실행해 주세요.")
    with open(os.path.join(TEMP, "FlowShots", "inputs.json"), "w", encoding="utf-8") as fp:
        json.dump({"signature": before, "done": os.path.getmtime(done)}, fp)
    return True


def main():
    args = sys.argv[1:]
    pre_commit = "--pre-commit" in args
    dry = "--dry" in args
    shots = "--shots" in args
    if pre_commit:
        files = args[args.index("--files") + 1:] if "--files" in args else git("diff", "--cached", "--name-only", "-z").split("\0")
        files = [f for f in files if f]
        screen = [f for f in files if f.startswith(SCREEN)]
        data = [f for f in files if f.startswith(DOC_DATA)]
        if not screen and not data:
            print("[문서] 게임 변경이 없어서 문서는 그대로예요.")
            return 0
        shots = bool(screen)
        print(f"[문서] 게임 파일 {len(screen)}개, 문서 데이터 {len(data)}개가 바뀌어서 문서를 다시 만들어요.", flush=True)
        if shots and shots_fresh():
            shots = False
            print("[문서] 같은 게임 파일로 오류 없이 찍은 사진이 있어서 다시 찍지 않아요.", flush=True)

    if dry:
        print("사진 다시 찍기 :", "예" if shots else "아니요", "/ Unity :", "켜짐" if unity_open() else "꺼짐")
        print("다시 만들 문서 :", ", ".join(SCRIPTS))
        return 0

    if shots:
        take_shots()
    for script in SCRIPTS:
        result = subprocess.run([sys.executable, os.path.join(TOOLS, script)], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", env={**os.environ, "PYTHONIOENCODING": "utf-8"})
        if result.returncode != 0:
            print(result.stdout[-1500:], result.stderr[-1500:])
            print(f"[문서] {script} 가 실패했어요. 문서가 게임과 어긋났다는 뜻일 수 있어요. 고친 뒤 다시 커밋해 주세요.")
            return 1
        last = result.stdout.strip().split("\n")[-1] if result.stdout.strip() else ""
        print(f"[문서] {script} : {last}", flush=True)

    if pre_commit:
        git("add", "--", *GENERATED)
        print("[문서] 다시 만든 문서를 이 커밋에 넣었어요.")
    print(REMINDER)
    return 0


if __name__ == "__main__":
    sys.exit(main())

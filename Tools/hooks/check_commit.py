# 커밋 전에 프로젝트 규칙을 확인해요 (.githooks/pre-commit 이 불러요).
#   1) 게임 코드(.cs)가 바뀐 커밋이면 : 그 코드로 돌린 "전체 테스트"가 PASS 여야 해요. 아니면 커밋을 막아요.
#      확인 방법 : Temp/SelfTest/result.txt 마지막 줄이 "[SelfTest] 전체 PASS" 이고,
#                  결과 파일이 바뀐 .cs 파일들보다 나중에 만들어졌는지 봐요.
#   2) 씬 파일(.unity)이 바뀌었는데 씬 조립 도구는 그대로면 : 손으로 고쳤을 수 있다고 경고만 해요.
#
# 급할 때 테스트 확인만 건너뛰기 (경고는 남아요) :
#   SKIP_TEST_CHECK=1 git commit ...
# 시험해 보기 : python Tools/hooks/check_commit.py --files Assets/Scripts/GameManager.cs
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RESULT = os.path.join(ROOT, "Temp", "SelfTest", "result.txt")
CODE = ("Assets/Scripts/", "Assets/Editor/")
BUILDERS = ("Assets/Editor/MvpSceneBuilder", "Assets/Editor/LobbySceneBuilder")
sys.stdout.reconfigure(encoding="utf-8")


def staged_files():
    out = subprocess.run(["git", "diff", "--cached", "--name-only", "--diff-filter=ACMR", "-z"],
                         cwd=ROOT, capture_output=True, text=True, encoding="utf-8", check=True).stdout
    return [f for f in out.split("\0") if f]


def check_tests(files):
    code = [f for f in files if f.startswith(CODE) and f.endswith(".cs")]
    if not code:
        return True
    problem = None
    if not os.path.exists(RESULT):
        problem = "전체 테스트 결과가 없어요."
    else:
        with open(RESULT, encoding="utf-8") as fp:
            lines = [l for l in fp.read().splitlines() if l.strip()]
        last = lines[-1] if lines else ""
        if not last.startswith("[SelfTest] 전체 PASS"):
            problem = f"마지막 테스트가 전체 PASS 가 아니에요 : {last or '(빈 파일)'}"
        else:
            tested = os.path.getmtime(RESULT)
            newer = [f for f in code if os.path.exists(os.path.join(ROOT, f)) and os.path.getmtime(os.path.join(ROOT, f)) > tested]
            if newer:
                problem = "테스트를 돌린 뒤에 코드가 또 바뀌었어요 : " + ", ".join(newer[:5]) + (" ..." if len(newer) > 5 else "")
    if problem is None:
        print(f"[검사] 게임 코드 {len(code)}개 : 전체 테스트 PASS 확인")
        return True
    print(f"[검사] {problem}")
    print("       Unity 를 켠 채로 Temp/mvp_selftest.flag 를 빈 파일로 만들면 전체 테스트가 돌아요 (Docs/4_기술/문서_도구.md).")
    if os.environ.get("SKIP_TEST_CHECK") == "1":
        print("[검사] SKIP_TEST_CHECK=1 이라서 이번만 커밋을 막지 않아요.")
        return True
    print("[검사] 커밋을 멈췄어요. 급하면 SKIP_TEST_CHECK=1 을 붙여 다시 커밋해요.")
    return False


def check_scene(files):
    scenes = [f for f in files if f.endswith(".unity")]
    if scenes and not any(f.startswith(BUILDERS) for f in files):
        print("[검사] 경고 : 씬 파일이 바뀌었는데 씬 조립 도구(MvpSceneBuilder · LobbySceneBuilder)는 그대로예요.")
        print("       씬은 손으로 고치지 않고 조립 도구로 만들어요. 일부러 다시 조립한 거면 괜찮아요 : " + ", ".join(scenes))


def main():
    args = sys.argv[1:]
    files = args[args.index("--files") + 1:] if "--files" in args else staged_files()
    check_scene(files)
    return 0 if check_tests(files) else 1


sys.exit(main())

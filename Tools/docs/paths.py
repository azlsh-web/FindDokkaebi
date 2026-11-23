# 문서 경로를 한곳에 모아요. 문서를 옮기거나 이름을 바꾸면 이 파일만 고쳐요.
#   경로는 모두 Docs 폴더 기준이고, 폴더는 / 로 나눠요.
#   다른 스크립트는 `import paths` 뒤 paths.doc(paths.ART_REQUEST) 처럼 실제 파일 위치를 얻어요.
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DOCS = os.path.join(ROOT, "Docs")


def doc(rel):
    """Docs 기준 경로 → 실제 파일 경로"""
    return os.path.join(DOCS, *rel.split("/"))


def repo(rel):
    """Docs 기준 경로 → 저장소 기준 경로 (git 명령에 써요)"""
    return "Docs/" + rel


# 시작 문서 : 사이트 홈의 목록도 이 문서의 표에서 읽어요
GUIDE = "0_시작하기.md"

# 사이트 구역 : (폴더, 화면에 보이는 이름). 시작 문서의 "## 보이는 이름" 표와 짝이에요.
SECTIONS = [
    ("1_기획", "기획"),
    ("2_제작", "제작"),
    ("3_전달", "전달"),
    ("4_기술", "기술"),
]

# 1_기획 : 게임이 어떻게 동작하는가
FLOW = "1_기획/게임_플로우.md"               # 자동 : 흐름 + 물건 표 + 사진 전체 (11/20 PDF 와 사진 모음을 합침)
PHOTOS = "1_기획/플로우_사진"                 # 자동 (Unity 로 찍음). 게임 플로우 문서가 이 사진을 보여 줘요
DIALOGUE_TREE = "1_기획/대사_트리.md"         # 자동
DIRECTION = "1_기획/연출_기획.md"             # 사람

# 2_제작 : 언제 무엇을 하는가, 왜 그렇게 정했는가
MILESTONE = "2_제작/마일스톤.md"              # 사람
DECISIONS = "2_제작/결정_기록.md"             # 사람
BACKLOG = "2_제작/백로그.md"                  # 사이트에만 : 저장소 맨 위 BACKLOG.md 를 옮겨 보여 줘요
JUDGE_TABLE = "2_제작/판정_그림_대응표.md"     # 자동 : 모든 판정 기준과 결과 그림 조합
JUDGE_PREVIEW = "2_제작/판정_그림_비교.html"   # 자동 : O/X를 눌러 보는 그림 비교

# 3_전달 : 다른 팀에 보내는 문서
REQUESTS = "3_전달/요청_현황.md"             # 자동 : 그림 · 소리를 받음 / 임시 / 없음으로 (request_data.py)
ART_REQUEST = "3_전달/아트.pdf"               # 자동
SOUND_REQUEST = "3_전달/사운드.pdf"           # 자동
DIALOGUE_SHEET = "3_전달/대사_수정표.xlsx"    # 자동

# 4_기술 : 코드와 그림의 구조
ARCH = "4_기술/아키텍처.pdf"                  # 자동
ART_STRUCTURE = "4_기술/아트_구조.pdf"        # 자동
DOC_TOOLS = "4_기술/문서_도구.md"             # 사람

# 코드에서 자동으로 만드는 문서 (커밋 훅이 다시 만들어 커밋에 넣어요)
GENERATED = [DIALOGUE_TREE, FLOW, PHOTOS, REQUESTS, JUDGE_TABLE, JUDGE_PREVIEW,
             DIALOGUE_SHEET, ART_REQUEST, SOUND_REQUEST, ARCH, ART_STRUCTURE]
# 사람이 판단해서 고치는 문서 (자동으로 안 바뀌어요)
HAND_WRITTEN = [DIRECTION, MILESTONE, DECISIONS, DOC_TOOLS]

# Docs 밖에 있지만 사이트에 함께 보여 줄 문서 : {사이트 경로(.md): 저장소 기준 원본}
EXTERNAL_PAGES = {BACKLOG: "BACKLOG.md"}

# 옛 사이트 주소 → 새 주소. 팀에 보낸 링크가 깨지지 않게 사이트에 옛 주소를 남겨요.
#   .md 는 넘어가는 페이지를 만들고, PDF · 엑셀은 같은 파일을 옛 자리에도 둬요.
LEGACY = {
    "0_문서_안내.md": GUIDE,
    "전달/아트.pdf": ART_REQUEST,
    "전달/사운드.pdf": SOUND_REQUEST,
    "전달/14_대사_수정표.xlsx": DIALOGUE_SHEET,
    "기획/13_대사_트리.md": DIALOGUE_TREE,
    "기획/5_기획_변경_기록.md": DECISIONS,
    "기획/10_할_일_목록.md": MILESTONE,
    "기획/11_연출_개편_기획.md": DIRECTION,
    "기획/플로우_사진/index.html": FLOW,
    "1_기획/플로우_사진/index.html": FLOW,     # 11/20 사진 모음 페이지 → 게임 플로우
}
# 사이트에서만 작은 이동 안내 PDF를 만들어요. 사진을 다시 담지 않아요.
PDF_REDIRECTS = {"1_기획/게임_플로우.pdf": FLOW, "기획/1_게임_플로우.pdf": FLOW,
                 "1_기획/대사_트리.pdf": DIALOGUE_TREE, "기획/13_대사_트리.pdf": DIALOGUE_TREE}

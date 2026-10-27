# 첨부한 사진 → 게임 구현 대조 PDF
#   python make_attached_report.py <test 폴더> <SelfTest 캡처 폴더> <출력 html>   (Edge 로 PDF 인쇄)
import sys, os, io, base64
from PIL import Image

TEST, SHOTS, OUT = sys.argv[1:4]


def b64(path, h):
    im = Image.open(path).convert("RGB")
    im = im.resize((round(im.width * h / im.height), h), Image.LANCZOS)
    buf = io.BytesIO(); im.save(buf, "JPEG", quality=82)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()


def shot(n): return os.path.join(SHOTS, n)


# (첨부 사진, 제목, 게임 화면 캡처들, 쓴 곳, 그대로, 달라진 점)
items = [
    ("KakaoTalk_20261026_100425597.png", "도깨비 캐릭터",
     [("02_s1_intro.png", "단계 도입"), ("06_s1_result_partial.png", "결과")],
     "단계 도입 · 판정 대기 · 결과 · 이야기 종료 화면의 도깨비",
     "그림 그대로 (회색 바탕만 지움)",
     "없음"),
    ("시작화면_v2.png", "시작 화면 v2",
     [("01_title.png", "시작 화면"), ("02_s1_intro.png", "주인공")],
     "시작 화면 전체, 그리고 이 그림에서 오려 낸 주인공(도입 · 결과 · 기록 화면)",
     "시작 화면은 같은 그림의 고해상도판(아트 수정 기획서 그림 11)을 그대로 씀. 「소풍 준비하러 가기」 자리를 누르면 시작",
     "주인공은 표정이 하나뿐이라, 결과 화면에서 기뻐하거나 당황할 때도 같은 얼굴 (당황할 때는 「!?」 글자만 붙음)"),
    ("KakaoTalk_20261026_131203350.png", "시작 화면 참고 (빈 방)",
     [("04_s1_draw.png", "게임 화면 뒤 배경")],
     "시작 화면을 뺀 모든 게임 화면의 뒤 배경",
     "방 그림 그대로",
     "가운데 제목 · 문구 · 버튼을 지운 빈 방으로 씀"),
    ("KakaoTalk_20261026_175942190.png", "관찰 화면 UX",
     [("03_s1_observe.png", "관찰 화면 (1단계)")],
     "관찰 화면 (3초)",
     "카드 위로 빼꼼한 도깨비, 말풍선 「잘 봐 둬! 곧 숨긴다?」, 테이프 붙인 쪽지, 3 · 2 · 1 동그라미, 「잠시 뒤 쪽지가 사라져요」, 도깨비불 장식, 위쪽 로고 · 단계 · 일시정지 · 간식 — 같은 시안의 고해상도판(기획서 그림 9)에서 오려 같은 자리에",
     "쪽지 속 그림 · 이름 · 안내 글자는 단계마다 바뀜 (1단계는 방망이, 「모양을 눈에 꼭 담아 줘!」). 지금 숫자만 코랄로 바뀜. 바뀌는 글자는 시안 글꼴이 없어 Noto Sans KR"),
    ("KakaoTalk_20261026_192259923.png", "그리기 화면 UX",
     [("04_s1_draw.png", "그리기 화면 (1단계)"), ("16_s4_draw.png", "그리기 화면 (4단계)")],
     "그리기 화면 (60초)",
     "클립보드 쪽지, 물감 판과 물감 동그라미, 「색 섞기」, 연필 · 지우개 · 되돌리기 · 굵기, 「다 그렸어! 간식 1개 사용」 — 기획서 그림 10(이 사진의 최신판)에서 오려 같은 자리에",
     "시간 표시는 사진의 「15초」 알약 대신 기획서 최신판대로 오른쪽 긴 향 + 아래 남은 초. "
     "물감 동그라미는 단계마다 쓸 수 있는 색만 보임 (1 · 2단계는 검정만). "
     "기획서 도구 목록에 있는 「모두 지우기」는 사진에 없어서 쪽지 오른쪽 아래에 작은 버튼으로 추가"),
    ("KakaoTalk_20261026_100425597_01.png", "주인공 (한복)", [], "쓰지 않음", "—",
     "이후 받은 시작화면_v2 와 기획서 캐릭터 시트가 멜빵바지 주인공이라 그쪽을 씀"),
    ("KakaoTalk_20261026_100425597_02.png", "친구 (한복)", [], "쓰지 않음", "—",
     "기획서 캐릭터 시트(그림 19)의 살구색 상의 + 청 멜빵 친구로 바뀌어서 그쪽을 씀 (5단계 원본 · 이야기 종료 화면)"),
    ("KakaoTalk_20261026_100536283.png", "한옥 시작 화면", [], "쓰지 않음", "—",
     "확정된 시작 화면은 방 배경 버전(시작화면_v2 · 기획서 그림 11)이라서 쓰지 않음"),
]

css = """
@page { size: A4; margin: 12mm; }
body { font-family: 'Malgun Gothic', sans-serif; color: #3b2414; font-size: 10.5pt; }
h1 { font-size: 19pt; margin: 0 0 4px; }
.item { border: 1px solid #d9c7b4; border-radius: 10px; padding: 8px 10px; margin: 8px 0; break-inside: avoid; page-break-inside: avoid; }
.item h2 { font-size: 13pt; margin: 0 0 6px; }
.row { display: flex; gap: 8px; align-items: flex-start; }
figure { margin: 0; text-align: center; font-size: 8.5pt; color: #7a604a; }
img { border: 1px solid #bbb; border-radius: 6px; }
.arrow { font-size: 22pt; align-self: center; color: #b07a50; }
table { border-collapse: collapse; margin-top: 6px; width: 100%; }
td { border-top: 1px solid #eee; padding: 3px 4px; vertical-align: top; font-size: 9.5pt; }
td.k { width: 18%; color: #7a604a; white-space: nowrap; }
.unused { background: #f5f1ec; }
"""
h = [f"<html><head><meta charset='utf-8'><style>{css}</style></head><body>",
     "<h1>첨부한 사진 → 게임에서 구현된 모습</h1>",
     "<p>폴더에 첨부한 사진 8장이 게임에서 어떻게 쓰였는지, 사진과 실제 게임 화면을 나란히 정리했어요.</p>"]
for fn, title, shots, where, same, diff in items:
    unused = not shots
    h.append(f"<div class='item{' unused' if unused else ''}'><h2>{title} <span style='font-size:9pt;color:#999'>({fn})</span></h2><div class='row'>")
    h.append(f"<figure><img src='{b64(os.path.join(TEST, fn), 230)}'><br>첨부 사진</figure>")
    if shots:
        h.append("<div class='arrow'>→</div>")
        for sn, cap in shots:
            h.append(f"<figure><img src='{b64(shot(sn), 230)}'><br>게임 : {cap}</figure>")
    h.append("</div><table>"
             f"<tr><td class='k'>쓴 곳</td><td>{where}</td></tr>"
             + (f"<tr><td class='k'>그대로 쓴 것</td><td>{same}</td></tr>" if not unused else "")
             + f"<tr><td class='k'>{'안 쓴 이유' if unused else '달라진 점'}</td><td>{diff}</td></tr>"
             "</table></div>")
h.append("</body></html>")
open(OUT, "w", encoding="utf-8").write("".join(h))
print("ok")

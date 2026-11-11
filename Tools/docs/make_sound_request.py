# 사운드.pdf : 사운드팀에 보내는 요청
#   python Tools/docs/make_sound_request.py
#   파일 이름은 Assets/Scripts/Sfx.cs 와 대조해요. 어긋나면 멈춰요.
from datetime import date
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from doc_pdf import BASE_CSS, write_pdf
from flow_shots import ROOT, data_url, find

OUT = os.path.join(ROOT, "Docs", "전달")
os.makedirs(OUT, exist_ok=True)

# (파일, 우선, 나오는 곳, 느낌, 권장 길이)
BGM = [
    ("bgm_room", "먼저", "시작, 단계 시작 대화, 쪽지, 관찰", "따뜻하고 아기자기한 아침", "30~60초 반복"),
    ("bgm_draw", "먼저", "그리기", "가볍게 긴장되지만 집중을 방해하지 않는 곡", "30~60초 반복"),
    ("bgm_judge", "먼저", "판정 대기, 결과, 채점표", "도깨비가 두리번거리는 장난스러운 곡. 맞았을 때와 틀렸을 때 모두 어울려야 해요.", "20~40초 반복"),
    ("bgm_picnic", "먼저", "이야기 종료", "신나는 소풍 곡", "30~60초 반복"),
]
SFX = [
    ("sfx_tap", "먼저", "대사 넘기기, 게임 시작하기, 다시", "짧고 가벼운 톡", "0.1초"),
    ("sfx_count", "먼저", "관찰 숫자 3, 2, 1 (1초에 한 번)", "짧은 똑딱", "0.2초"),
    ("sfx_submit", "먼저", "다 그렸어! 버튼", "쪽지를 건네는 소리", "0.5초"),
    ("sfx_item", "먼저", "결과에서 물건이 나타날 때", "뿅", "0.3초"),
    ("sfx_success", "먼저", "전부 맞았을 때. `sfx_item`과 동시에 나요.", "밝은 짠", "1초"),
    ("sfx_fail", "먼저", "틀렸을 때. `sfx_item`과 동시에 나요.", "어이없는 띠용", "1초"),
    ("sfx_mark", "먼저", "채점표가 나올 때 한 번. 표시는 0.5초 간격으로 최대 5개 그어져요.", "연필로 슥슥 긋는 소리", "최대 3초"),
    ("sfx_cheer", "먼저", "이야기 종료 시작", "작은 환호", "2초"),
    ("sfx_talk", "나중", "대사 글자가 나올 때. 1초에 약 15번 울려요.", "아주 작고 짧은 틱. 반복해도 거슬리지 않게 해 주세요.", "0.05초 이하"),
    ("sfx_snack_in", "나중", "단계 첫 그리기에 초콜릿 2개(돋보기 뒤에서)와 사탕 2개(오른쪽에서)가 들어올 때 한 번. 약 0.7초 동안 들어와요.", "샤샤샥", "0.7초"),
    ("sfx_land", "나중", "소풍 장면에 물건이 놓일 때. 코드 연결은 파일을 받은 뒤에 해요.", "톡", "0.2초"),
]

# 코드와 대조
code = open(os.path.join(ROOT, "Assets", "Scripts", "Sfx.cs"), encoding="utf-8").read()
in_code = set(re.findall(r'const string \w+ = "(\w+)"', code))
in_doc = {r[0] for r in BGM + SFX}
assert in_code == in_doc, in_code ^ in_doc


def code_tag(text):
    parts = text.split("`")
    return "".join(f"<code>{p}</code>" if i % 2 else p for i, p in enumerate(parts))


def table(rows):
    body = "".join(f"<tr><td><code>{n}</code></td><td>{code_tag(w)}</td><td>{code_tag(f)}</td><td>{l}</td></tr>" for n, _, w, f, l in rows)
    return f"<table><tr><th style='width:21%'>파일 이름</th><th style='width:35%'>나오는 때</th><th>느낌</th><th style='width:13%'>권장 길이</th></tr>{body}</table>"


def shot(section, caption, label, sounds):
    return (f"<figure><img src='{data_url(find(section, caption), 420)}'><figcaption><b>{label}</b><br>{sounds}</figcaption></figure>")


first = [r for r in BGM + SFX if r[1] == "먼저"]
later = [r for r in SFX if r[1] == "나중"]

flow = """찾아라! 도깨비
├─ 시작 ······················ bgm_room
│  └─ 게임 시작하기 ·········· sfx_tap
├─ 단계 시작 대화 ············· bgm_room
│  ├─ 글자가 나올 때 ········· sfx_talk
│  └─ 대사 넘기기 ············ sfx_tap
├─ 떠올리기 3초 ··············· bgm_room
│  └─ 숫자 3, 2, 1 ··········· sfx_count
├─ 쪽지 쓰기 60초 ············· bgm_draw
│  ├─ 단계 첫 그리기 ········· sfx_snack_in
│  └─ 다 그렸어! ············· sfx_submit
├─ 건네기 ···················· bgm_draw 이어짐
├─ 판정 대기 ················· bgm_judge
├─ 결과 ······················ bgm_judge 이어짐
│  ├─ 물건 등장 ·············· sfx_item
│  ├─ 전부 맞음 ·············· sfx_success
│  ├─ 틀림 ··················· sfx_fail
│  └─ 다시 ··················· sfx_tap → 그리기로
├─ 채점표 ···················· bgm_judge 이어짐
│  └─ 표시 긋기 ·············· sfx_mark
└─ 이야기 종료 ··············· bgm_picnic
   ├─ 시작 ··················· sfx_cheer
   └─ 물건이 놓일 때 ·········· sfx_land"""

tree = "\n".join(["FindDokkaebi_Sound_v1.zip", "└─ FindDokkaebi_Sound/", "   ├─ 출처.txt"] +
                 [f"   {'└─' if i == len(in_doc) - 1 else '├─'} {n}.wav" for i, n in enumerate(sorted(in_doc))])

CSS = BASE_CSS + """
tr, figure, .keep { break-inside: avoid; }
pre { background: #f7f4ef; border: 1px solid #e3dcd2; padding: 10px; font-family: Consolas, 'Malgun Gothic', monospace; font-size: 11px; line-height: 1.45; }
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 8px 12px; margin: 10px 0; }
.shots { display: grid; grid-template-columns: repeat(5, 1fr); gap: 10px; } figure { margin: 0; } figure img { width: 100%; border: 1px solid #ddd6cc; border-radius: 4px; }
figcaption { font-size: 9.5px; color: #6d645c; margin-top: 3px; text-align: center; word-break: break-all; }
figcaption b { color: #2b2622; font-size: 10.5px; }
"""

html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>사운드팀 전달사항</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 사운드팀 전달사항</h1><p class='sub'>{date.today()} 기준 · 출품 마감 2026-11-15(일)</p>
<div class='note'><b>요청:</b> {len(in_doc)}개 (배경음 {len(BGM)}, 효과음 {len(SFX)})<br><b>기한:</b> 11월 14일(토)<br><b>우선:</b> {len(first)}개 (표의 먼저 항목)<br><b>빠진 파일:</b> 소리 없이 진행</div>
<h2>게임 분위기</h2>
<p>주인공과 도깨비가 사라진 소풍 준비물을 찾는 아기자기하고 장난스러운 게임이에요. 플레이어가 기억해서 그린 쪽지대로 도깨비가 물건을 찾아오고, 그림이 틀리면 엉뚱한 물건을 가져와 놀려요. 전시장에서 플레이하므로 오래 들어도 피곤하지 않아야 해요.</p>
<div class='shots'>
{shot('시작', '시작 화면', '시작 · 대화', 'bgm_room')}
{shot('3단계', '그리기 시작', '그리기', 'bgm_draw')}
{shot('2단계', '판정 대기', '판정 대기', 'bgm_judge')}
{shot('2단계', '전부 틀림 : 도깨비', '틀렸을 때', 'sfx_item + sfx_fail')}
{shot('이야기 종료', '전부 찾았을 때', '이야기 종료', 'bgm_picnic + sfx_cheer')}
</div>
<h2>화면별 소리</h2>
<pre>{flow}</pre>
<h2>배경음 {len(BGM)}곡</h2>
{table(BGM)}
<p>끝과 시작이 자연스럽게 이어지게 만들어 주세요. 곡이 바뀔 때 페이드 없이 바로 넘어가요. 같은 곡이 이어지면 처음으로 돌아가지 않아요.</p>
<h2>효과음 : 먼저 필요한 {len([r for r in SFX if r[1] == '먼저'])}개</h2>
{table([r for r in SFX if r[1] == '먼저'])}
<h2>효과음 : 나중에 받아도 되는 {len(later)}개</h2>
{table(later)}
<p class='sub'>권장 길이는 화면 움직임에 맞춘 제안이에요. 조금 달라도 괜찮아요.</p>
<div class='keep'><h2>규칙</h2>
<ul><li><b>파일 이름</b>을 표와 똑같이 소문자로 해 주세요. 확장자는 파일 형식에 맞게 붙이면 돼요.</li>
<li><b>파일 형식:</b> <code>.wav</code> 또는 <code>.ogg</code>. <code>.mp3</code>도 괜찮아요.</li>
<li><b>음량:</b> 배경음은 게임에서 절반 음량으로 틀고, 효과음은 그대로 틀어요. 배경음과 효과음을 비슷한 크기로 맞춰 보내 주세요.</li>
<li><b>앞뒤 무음:</b> 효과음 앞에 빈 시간을 두지 말아 주세요. 화면과 어긋나요.</li>
<li><b>저작권:</b> 출품과 전시에 쓸 수 있는 소리여야 해요. 직접 만들지 않았다면 <code>출처.txt</code>에 파일 이름, 출처 주소, 사용 조건을 적어 주세요.</li>
<li><b>전시 환경:</b> 아직 정해지지 않았어요. 소리 없이도 게임을 이해할 수 있게 만들었으니 소리는 분위기를 더하는 역할이에요.</li></ul></div>
<div class='keep'><h2>보내는 방법</h2>
<p>아래 구조로 ZIP 하나로 묶어 주세요. 있는 파일만 넣으면 돼요. 수정본은 ZIP 이름만 <code>_v2</code>, <code>_v3</code>으로 올리고 바뀐 파일만 넣어 주세요.</p>
<pre>{tree}</pre></div>
<div class='keep'><h2>받은 뒤 (개발 쪽)</h2>
<p>파일을 <code>Assets/Resources/Sound</code>에 넣으면 코드를 고치지 않아도 소리가 나요. <code>sfx_land</code>만 코드 연결이 필요해요.</p></div>
</body></html>"""

os.makedirs(os.path.join(ROOT, "Temp", "DocsHtml"), exist_ok=True)
open(os.path.join(ROOT, "Temp", "DocsHtml", "사운드.html"), "w", encoding="utf-8").write(html)
print("사운드.pdf", write_pdf(html, os.path.join(OUT, "사운드.pdf")))

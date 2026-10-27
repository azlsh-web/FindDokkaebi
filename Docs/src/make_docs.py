# Docs 의 두 문서 만들기 : 1_게임_플로우 · 2_MVP_아키텍처 (HTML → Edge 로 PDF)
#   python make_docs.py <FindDokkaebi 폴더> <SelfTest 캡처 폴더>
import sys, os, io, base64, subprocess
from PIL import Image

ROOT, SHOTS = sys.argv[1:3]
DOCS = os.path.join(ROOT, "Docs")
SRC = os.path.join(DOCS, "src")
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"


def img(name, w=230):
    im = Image.open(os.path.join(SHOTS, name)).convert("RGB")
    im = im.resize((w * 2, round(im.height * w * 2 / im.width)), Image.LANCZOS)
    buf = io.BytesIO(); im.save(buf, "JPEG", quality=78)
    return f"<img class='shot' style='width:{w}px' src='data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}'>"


CSS = """
@page { size: A4; margin: 13mm 12mm; }
body { font-family: 'Malgun Gothic', sans-serif; color: #3b2414; font-size: 10.3pt; line-height: 1.5; }
h1 { font-size: 20pt; margin: 0 0 2px; } h2 { font-size: 14pt; margin: 16px 0 6px; border-bottom: 2px solid #4a2b16; padding-bottom: 2px; }
h3 { font-size: 11.5pt; margin: 10px 0 4px; }
.sub { color: #7a604a; margin: 0 0 8px; }
table { border-collapse: collapse; width: 100%; margin: 4px 0 8px; }
td, th { border: 1px solid #dccab6; padding: 3px 6px; vertical-align: top; font-size: 9.4pt; }
th { background: #f6ecdf; text-align: left; }
code { background: #f3ece3; padding: 0 3px; border-radius: 3px; font-size: 9pt; }
.screen { display: flex; gap: 12px; break-inside: avoid; page-break-inside: avoid; border: 1px solid #e2d3c2; border-radius: 10px; padding: 8px; margin: 8px 0; }
.screen .shot { border: 1px solid #aaa; border-radius: 8px; }
.screen h3 { margin-top: 0; }
.k { color: #7a604a; white-space: nowrap; width: 17%; }
.flow { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; margin: 6px 0; }
.flow span.b { border: 2px solid #4a2b16; border-radius: 10px; padding: 4px 8px; background: #fffaf2; font-weight: bold; font-size: 9.5pt; }
.flow span.a { color: #b07a50; font-weight: bold; }
.note { background: #f6f1ea; border-left: 5px solid #b07a50; padding: 5px 10px; margin: 6px 0; font-size: 9.6pt; }
.keep { break-inside: avoid; page-break-inside: avoid; }
.pb { page-break-before: always; }
pre { background: #f6f1ea; padding: 6px 10px; border-radius: 6px; font-size: 8.8pt; white-space: pre-wrap; }
"""


def page(title, body):
    return f"<html><head><meta charset='utf-8'><title>{title}</title><style>{CSS}</style></head><body>{body}</body></html>"


def screen(no, title, shot, rows, w=210):
    t = "".join(f"<tr><td class='k'>{k}</td><td>{v}</td></tr>" for k, v in rows)
    return f"<div class='screen'>{img(shot, w)}<div style='flex:1'><h3>{no}. {title}</h3><table>{t}</table></div></div>"


# ─────────────────────────────── 1. 게임 플로우 ───────────────────────────────
flow = ["<h1>찾아라! 도깨비 — 게임 플로우</h1>",
        "<p class='sub'>실제 게임 화면이 어떤 순서로, 어떤 조건에서 넘어가는지. 캡처는 자동 플레이 테스트가 찍은 지금 버전 화면이에요.</p>",
        "<h2>한눈에 보기</h2>",
        "<div class='flow'>" + "".join(
            f"<span class='b'>{b}</span>" + ("<span class='a'>→</span>" if i < 7 else "") for i, b in enumerate(
                ["1 시작", "2 단계 도입", "3 관찰 3초", "4 그리기 60초", "5 판정 대기 (AI)", "6 결과", "7 비교", "다음 단계 / 8 이야기 종료"])) + "</div>",
        "<table><tr><th>결과에 따라</th><th>다음</th></tr>"
        "<tr><td>특징을 전부 그림 → <b>성공</b> (진짜 물건)</td><td>「내 그림과 비교하기」 → 7 비교 → 다음 단계</td></tr>"
        "<tr><td>일부만 그림 → <b>부분 성공</b> (엉뚱한 물건)</td><td>간식이 남았으면 「다시 그리기」(원본 안 보여 주고 4 그리기, 60초) 또는 「포기하고 비교하기」</td></tr>"
        "<tr><td>하나도 못 그림 → <b>아무것도 못 찾음</b></td><td>부분 성공과 같음</td></tr>"
        "<tr><td>간식 0개가 됨</td><td>「다시 그리기」 없음 → 「포기하고 비교하기」만</td></tr>"
        "<tr><td>AI 판정 실패 (인터넷 · 서버 · 키)</td><td>간식을 돌려주고 「그림으로 돌아가 다시 제출」 (그리던 그림 그대로, 남은 시간 최소 10초)</td></tr>"
        "<tr><td>향이 다 탐 (60초)</td><td>지금 그림 그대로 자동 제출</td></tr>"
        "<tr><td>일시정지 (오른쪽 위)</td><td>어느 화면이든 시간 멈춤 → 「계속하기」</td></tr></table>",
        "<div class='note'>간식 : 단계마다 3개로 시작, 제출할 때 1개 씀. 5단계(방망이 → 가방 → 돗자리 → 김밥 → 친구)를 마치면 이야기 종료. "
        "판정은 AI 가 해요 (그림 + 단계별 특징 기준 → 특징마다 그림/빠뜨림). 그림이 들어간 곳은 <code>ArtSource/README.md</code>.</div>",
        "<h2 class='pb'>화면 순서대로</h2>",
        screen(1, "시작 화면", "01_title.png", [
            ("나오는 것", "팀 시작 화면 그림 (제목 · 주인공 · 도깨비 · 「소풍 준비하러 가기」)"),
            ("하는 것", "「소풍 준비하러 가기」 누르기"),
            ("다음", "1단계 단계 도입. 기록(단계별 성공·제출 수·남은 간식)을 새로 시작"),
            ("그림", "<code>ArtSource/화면별/01_시작화면</code>")]),
        screen(2, "단계 도입", "02_s1_intro.png", [
            ("나오는 것", "위 HUD (로고 · 「1/5 · 방망이」 · 일시정지 · 간식 3개), 단계 번호 · 물건 이름 · 주인공과 도깨비 · 이야기, 새로 배울 규칙 · 앞에서 배운 규칙 · 시간 안내"),
            ("하는 것", "읽고 「기억할 모습 보기」"),
            ("다음", "관찰 (3초)"),
            ("바뀌는 값", "간식 3개로 채움, 단계에 맞는 색 버튼 준비 (1·2단계 검정 / 3단계 + 빨노파 / 4·5단계 + 섞은 색 · 색 섞기)"),
            ("그림", "<code>화면별/02_단계도입</code> + 공통 카드 · 버튼")]),
        screen(3, "관찰 (3초)", "03_s1_observe.png", [
            ("나오는 것", "카드 위로 빼꼼한 도깨비 「잘 봐 둬! 곧 숨긴다?」, 테이프 쪽지 속 물건 원본 · 이름 · 「모양(과 색)을 눈에 꼭 담아 줘!」, 3 · 2 · 1 (지금 숫자만 코랄)"),
            ("하는 것", "모양 · 무늬 · 색을 기억하기 (누를 것 없음)"),
            ("다음", "3초가 지나면 자동으로 그리기. 원본은 다시 안 보여 줘요"),
            ("그림", "<code>화면별/03_관찰</code>, 물건은 <code>화면별/10_단계별물건</code>")]),
        screen(4, "그리기 (60초)", "16_s4_draw.png", [
            ("나오는 것", "클립보드 쪽지 「기억나는 김밥을 그려 줘」, 오른쪽 긴 향(타들어 감) + 남은 초, 물감 판(단계마다 쓸 수 있는 색), 연필 · 지우개 · 되돌리기 · 굵기 3단계, 쪽지 오른쪽 아래 모두 지우기, 「다 그렸어! 간식 1개 사용」"),
            ("하는 것", "기억한 특징을 그리기. 4·5단계는 「색 섞기」로 색을 만들어 칠하기 (아래 4-1)"),
            ("다음", "「다 그렸어!」 또는 60초가 지나면 → 판정 대기. 빈 쪽지면 「다 그렸어!」 · 되돌리기 · 모두 지우기가 꺼져 있음"),
            ("바뀌는 값", "제출하면 간식 −1, 이 단계 제출 횟수 +1"),
            ("그림", "<code>화면별/04_그리기</code>")]),
        "<div class='screen'><div style='flex:1'><h3>4-1. 색 섞기 (4·5단계, 그리기 중)</h3><table>"
        "<tr><td class='k'>나오는 것</td><td>물감 판과 도구 줄 자리를 덮는 카드 : 재료 색(빨강 · 노랑 · 파랑) 고르기, 결과 색 미리보기 · 「노랑 + 파랑 = 초록」, 「섞어서 칠하기」, 닫기</td></tr>"
        "<tr><td class='k'>규칙</td><td>4단계는 두 색만 (노랑+파랑=초록, 빨강+노랑=주황, 빨강+파랑=보라), 5단계는 세 색까지 (빨강+노랑+파랑=갈색)</td></tr>"
        "<tr><td class='k'>다음</td><td>「섞어서 칠하기」 → 카드가 닫히고 그 색이 물감 판 5번째 칸에 들어가 골라져요</td></tr></table></div></div>",
        screen(5, "판정 대기", "05_s1_ai_error.png", [
            ("나오는 것", "「쪽지와 간식 1개를 도깨비에게 건넸어요. 도깨비가 쪽지를 들여다보는 중...」, 갸웃거리는 도깨비 · 건넨 쪽지 · 간식"),
            ("판정", "그림(PNG) → 판정 서버 → AI(OpenAI) 가 단계별 특징 기준(기획서 부록 B 프롬프트)으로 특징마다 그림/빠뜨림 + 근거"),
            ("다음", "판정이 오면 결과. 실패하면 (캡처) 「판정 중에 문제가 생겼어요. 간식을 돌려줬어요」 + 「그림으로 돌아가 다시 제출」")]),
        screen(6, "결과 — 부분 성공", "06_s1_result_partial.png", [
            ("나오는 것", "도깨비 · 가져온 물건 · 내 그림, 「엉뚱한 방망이를 가져왔다...」, 특징별 [O] 그림 / [X] 빠뜨림 (+ 빠뜨린 이유), 소년 「'볼록볼록한 머리' 부분이 달라!」, 남은 간식"),
            ("가져온 물건", "처음 빠뜨린 특징에 맞는 엉뚱한 물건 (지금은 팀 그림이 없어 「그림 준비 중」)"),
            ("하는 것", "「다시 그리기 (간식 1개)」 → 그리기 (60초, 원본 다시 안 봄) / 「포기하고 비교하기」 → 비교")]),
        screen(6, "결과 — 성공", "07_s1_result_success.png", [
            ("나오는 것", "「도깨비가 방망이를 찾아왔다!」, 진짜 물건, 특징 전부 [O], 소년 「찾았다! 바로 이 방망이야!」"),
            ("하는 것", "「내 그림과 비교하기」 → 비교")]),
        screen(6, "결과 — 간식이 다 떨어짐", "11_s2_out_of_snacks.png", [
            ("나오는 것", "「도깨비가 아무것도 찾지 못했다...」(하나도 못 그림), 「간식이 다 떨어졌어요. 원본과 비교하고 넘어가요.」"),
            ("하는 것", "「포기하고 비교하기」만 있음 → 비교 (이 단계는 기록에 「포기했어요」)")]),
        screen(7, "비교", "08_s1_compare.png", [
            ("나오는 것", "왼쪽 내 그림 · 오른쪽 원본, 원본의 특징 목록"),
            ("하는 것", "「다음 단계로」 (5단계에서는 「오늘의 이야기 마치기」)"),
            ("다음", "다음 단계 도입 (간식 다시 3개) / 5단계 뒤에는 이야기 종료")]),
        screen(8, "이야기 종료", "23_ending.png", [
            ("나오는 것", "「오늘의 이야기 끝!」, 주인공 · 도깨비 · 친구, 단계별 기록 (찾았어요/포기했어요 · 제출 수 · 남은 간식)"),
            ("하는 것", "「처음으로」 → 시작 화면")]),
        "<h2>단계마다 다른 것</h2><table><tr><th>단계</th><th>물건</th><th>쓸 수 있는 색</th><th>판정할 특징</th></tr>"
        "<tr><td>1</td><td>방망이</td><td>검정</td><td>길쭉한 손잡이 · 손잡이보다 굵은 머리 · 볼록볼록한 머리</td></tr>"
        "<tr><td>2</td><td>가방</td><td>검정</td><td>네모난 몸체 · 위쪽 손잡이 · 앞쪽 주머니 · 주머니 안 가로 줄무늬</td></tr>"
        "<tr><td>3</td><td>돗자리</td><td>검정 · 빨강 · 노랑 · 파랑</td><td>사각형 외곽 · 띠 네 줄 · 두 번째 띠 빨강 · 세 번째 띠 노랑 · 양 끝 띠 파랑</td></tr>"
        "<tr><td>4</td><td>김밥</td><td>+ 두 색 섞기</td><td>검은 김 테두리 · 흰 밥 · 빨간 · 노란 · 초록 재료</td></tr>"
        "<tr><td>5</td><td>친구</td><td>+ 세 색까지 섞기</td><td>사람 모습 · 갈색 머리 · 주황 상의 · 파란 하의</td></tr></table>",
        ]
open(os.path.join(SRC, "1_게임_플로우.html"), "w", encoding="utf-8").write(page("게임 플로우", "".join(flow)))

# ─────────────────────────────── 2. MVP 아키텍처 ───────────────────────────────
arch = [
    "<h1>찾아라! 도깨비 — MVP 아키텍처</h1>",
    "<p class='sub'>개발할 때 옆에 두고 보는 문서. 어떤 파일이 무엇을 하고, 무엇을 바꾸려면 어디를 고치는지.</p>",
    "<h2>1. 한눈에</h2><table>"
    "<tr><td class='k'>엔진</td><td>Unity 6.3 LTS (6000.3.11f1), uGUI + TextMeshPro, Input System. 씬 1개 <code>Assets/Scenes/Game.unity</code></td></tr>"
    "<tr><td class='k'>씬 만들기</td><td>씬은 손으로 만들지 않고 <code>MvpSceneBuilder</code>가 코드로 전부 만들어요 (메뉴 Tools → 찾아라도깨비 → MVP 씬 만들기). 배치를 바꾸려면 이 파일을 고치고 다시 만들기</td></tr>"
    "<tr><td class='k'>화면</td><td>패널 8개 (시작 · 도입 · 관찰 · 그리기 · 판정 · 결과 · 비교 · 종료) 중 하나만 켜고, 위에 HUD · 일시정지. 화면 기준 1080×1899 = 팀 시안(2248×3952)을 줄인 크기, 시안 픽셀 좌표 그대로 배치</td></tr>"
    "<tr><td class='k'>판정</td><td>AI (OpenAI Responses API). 웹 · 에디터는 판정 중계 서버(Cloudflare Worker)를 거쳐요 (키는 서버에만). 실패하면 간식 돌려주고 다시 제출</td></tr>"
    "<tr><td class='k'>배포</td><td>웹 게임 = GitHub Pages (<code>gh-pages</code> 브랜치), 판정 서버 = <code>Server/judge</code> (wrangler), 코드 = <code>feature/mvp-stage1</code></td></tr></table>",
    "<h2>2. 폴더</h2><pre>"
    "FindDokkaebi/\n"
    "  Assets/Scripts/     게임 코드 (아래 3장)\n"
    "  Assets/Editor/      에디터 도구 : 씬 만들기 · 자동 테스트 · 판정 검사 · 웹 빌드 · 글꼴 굽기\n"
    "  Assets/Data/        단계 데이터 Stage1~5 (.asset, 씬 만들 때 MvpSceneBuilder 가 채움)\n"
    "  Assets/Sprites/     TeamUI(시안에서 오린 UI, 절반 크기) · Background · Characters · Stage1~5(원본·엉뚱한 물건) · UI(그림 준비 중 등)\n"
    "  Assets/Fonts/       Jua(시안 글꼴, 구운 SDF) · Noto Sans KR(없는 기호 대신)\n"
    "  ArtSource/          아트 원본 정리 : 원본(기획서 그림 · 첨부 사진) + 화면별로 들어간 부품 + README 표\n"
    "  Server/judge/       판정 중계 서버 (worker.js · stages.json 프롬프트 · wrangler.toml)\n"
    "  Docs/               이 문서들 · src(문서·그림 만드는 스크립트) · 참고(예전 문서)\n"
    "  Builds/WebGL/       웹 빌드 결과 (git 에 안 올림)\n"
    "  UserSettings/OpenAI.json   (선택) 내 OpenAI 키 — git 에 안 올림</pre>",
    "<h2 class='pb'>3. 게임 코드 (Assets/Scripts)</h2><table><tr><th>파일</th><th>하는 일</th><th>주로 고칠 때</th></tr>"
    "<tr><td><code>GameManager</code></td><td>화면 흐름(상태 머신) · 간식 · 타이머 · 판정 요청 · 결과 규칙 · 기록 · 일시정지</td><td>흐름 · 규칙 · 문구</td></tr>"
    "<tr><td><code>StageData</code></td><td>단계 하나의 데이터 (이름 · 이야기 · 규칙 · 색 모드 · 원본 그림 · 특징 목록). <code>Feature</code> = 특징 하나 (id · 이름 · 판정 기준 글 · 엉뚱한 물건 그림 · 테스트용 계산 종류)</td><td>단계 · 특징 추가</td></tr>"
    "<tr><td><code>DrawingBoard</code></td><td>512×512 쪽지 : 펜 · 지우개 · 굵기 · 되돌리기(20번) · 모두 지우기, 제출할 그림 텍스처</td><td>그리기 느낌</td></tr>"
    "<tr><td><code>ToolBar</code></td><td>색 · 연필 · 지우개 · 굵기 버튼, 고른 표시, 단계마다 색 버튼 보이기/숨기기</td><td>도구 버튼</td></tr>"
    "<tr><td><code>ColorMixer</code></td><td>색 섞기 카드 : 재료 고르기 → 결과 색 (MixTable) → 섞은 색 칸</td><td>섞는 규칙</td></tr>"
    "<tr><td><code>OpenAIJudge</code></td><td>AI 판정 요청 : 키 있으면 OpenAI 직접, 없으면 판정 서버. 기획서 부록 B 프롬프트(BuildPrompt) · 응답 JSON 해석(특징별 present)</td><td>프롬프트 · 서버 주소</td></tr>"
    "<tr><td><code>LocalJudge</code></td><td>계산식 판정 (모양 · 색 계산). <b>자동 테스트 전용</b> — 실제 플레이에서는 안 씀</td><td>테스트</td></tr>"
    "<tr><td><code>SimpleAnim</code></td><td>도깨비 갸웃 · 물건 뿅 · 방망이 휘두르기</td><td>연출</td></tr></table>",
    "<h3>화면 흐름 (GameManager.State)</h3><pre>"
    "Title ─StartGame→ StageIntro ─StartObserve→ Observe(3초) ─자동→ Draw(60초)\n"
    "Draw ─Submit(간식-1)→ Judge ─AI 결과→ Result ─ShowCompare→ Compare ─NextStage→ StageIntro(다음) … → Ending ─BackToTitle→ Title\n"
    "Judge ─AI 실패→ (간식+1) ─BackToDraw→ Draw(그리던 그림)     Result ─Retry(간식 남음)→ Draw(빈 쪽지, 60초)\n"
    "어느 화면이든 ─TogglePause→ 일시정지 (Time.timeScale 0)</pre>",
    "<h3>결과 규칙 (ShowResult)</h3><p>특징 전부 그림 → 성공 · 진짜 물건 / 일부 → 처음 빠뜨린 특징의 엉뚱한 물건 / 하나도 없음 → 아무것도 못 찾음. 실패했고 간식이 남으면 「다시 그리기」.</p>",
    "<h2>4. 판정이 흐르는 길</h2><pre>"
    "제출 → board.Texture → PNG → base64\n"
    "  ├ (UserSettings/OpenAI.json 에 키 있음)  OpenAI Responses API 로 바로 : 프롬프트(BuildPrompt) + 그림\n"
    "  └ (키 없음)  판정 서버 POST /judge {stage, image}\n"
    "        서버 : 허락한 곳(웹 게임 주소 · Unity)만, IP 당 1분 20번 → stages.json 의 그 단계 프롬프트 + 그림 → OpenAI\n"
    "← AI JSON {inputStatus, features:[{featureId, status, evidence}]} → present = (status == present) → ShowResult\n"
    "실패(연결 · 키 · JSON) → OnJudgeError : 간식 돌려주고 「그림으로 돌아가 다시 제출」</pre>"
    "<div class='note'>프롬프트를 바꾸면 : 특징 글은 <code>MvpSceneBuilder.CreateStageDataFiles</code>, 프롬프트 틀은 <code>OpenAIJudge.BuildPrompt</code> → 씬 만들기 → 웹 빌드(이때 <code>Server/judge/stages.json</code> 도 자동으로 새로) → <code>Server/judge</code> 에서 <code>npx wrangler deploy</code>. "
    "서버 키 : <code>npx wrangler secret put OPENAI_API_KEY</code> (값은 물어볼 때 붙여 넣기).</div>",
    "<h2 class='pb'>5. 화면 만들기 (MvpSceneBuilder)</h2><table>"
    "<tr><td class='k'>좌표</td><td><code>PlaceM(rt, x0,y0,x1,y1)</code> = 시안 픽셀 상자 그대로 (K = 1080/2248). <code>TeamImage</code> = 오린 그림을 원래 자리에, <code>TeamTool</code> = 시안 그림 버튼 + 고른 표시 고리</td></tr>"
    "<tr><td class='k'>화면 크기</td><td>CanvasScaler 1080×1899 Expand, 가운데 <code>Frame</code> 안에 모든 화면. 폰이 더 길면 위아래에 방 배경</td></tr>"
    "<tr><td class='k'>그림 설정</td><td>시안 UI · 시작 화면은 압축 없이 + 밉맵 + 최대 4096 (외곽선이 또렷하게). 카드 · 버튼은 9-slice (TeamCard · TeamPillCoral · TeamPillCream)</td></tr>"
    "<tr><td class='k'>글꼴</td><td>Jua SDF (<code>MvpFontBake</code> 가 게임 글자만 구움). 글자를 새로 넣었으면 글꼴 굽기를 다시</td></tr>"
    "<tr><td class='k'>단계 데이터</td><td><code>CreateStageDataFiles</code> : 이야기 · 규칙 · 특징(이름 · 기준 글) · 그림 연결. 씬 만들 때마다 덮어써요</td></tr></table>",
    "<h2>6. 도구 (Tools → 찾아라도깨비, 또는 Temp/ 의 flag 파일)</h2><table><tr><th>도구</th><th>파일</th><th>하는 일</th></tr>"
    "<tr><td>MVP 씬 만들기</td><td>MvpSceneBuilder · MvpAutoBuild</td><td>씬을 처음부터 만들기 (mvp_rebuild.flag)</td></tr>"
    "<tr><td>자동 플레이 테스트</td><td>MvpSelfTest · MvpLayoutCheck</td><td>5단계 흐름 · 규칙 · 버튼 확인 + 화면 캡처(Temp/SelfTest) + 배치 숫자 검사(걸쳐 겹침 · 글자 넘침 · 화면 밖) (mvp_selftest.flag)</td></tr>"
    "<tr><td>판정 전수 검사</td><td>MvpJudgeCases · HandSketch</td><td>특징 조합 112가지 손그림을 계산식 판정에 넣어 보기 (mvp_judgecases.flag)</td></tr>"
    "<tr><td>글꼴 굽기</td><td>MvpFontBake</td><td>Jua SDF (mvp_font.flag)</td></tr>"
    "<tr><td>웹 빌드</td><td>MvpWebGLBuild</td><td>Builds/WebGL + 판정 서버 프롬프트 내보내기 (mvp_webgl.flag)</td></tr>"
    "<tr><td>판정 서버 프롬프트</td><td>MvpJudgeServer</td><td>stages.json 내보내기, server_url.txt → 씬의 서버 주소</td></tr>"
    "<tr><td>플레이 영상</td><td>MvpPlayRecorder</td><td>자동 플레이를 프레임으로 저장 (예전 화면 기준, 다시 찍어야 함)</td></tr></table>",
    "<h2>7. 자주 하는 수정</h2><table><tr><th>하고 싶은 것</th><th>방법</th></tr>"
    "<tr><td>팀 그림이 왔어요 (엉뚱한 물건 · 표정)</td><td><code>Assets/Sprites/Stage*/Wrong_F*.png</code> · <code>Characters/Boy_*.png</code> 를 같은 이름으로 덮어쓰기 → 씬 만들기 → 웹 빌드</td></tr>"
    "<tr><td>시안 UI 가 바뀌었어요</td><td>새 시안을 <code>ArtSource/원본</code> 에 → <code>Docs/src/art_pipeline</code> 스크립트로 다시 오리기 → TeamUI 덮어쓰기</td></tr>"
    "<tr><td>배치를 옮기고 싶어요</td><td>MvpSceneBuilder 의 PlaceM 좌표(시안 픽셀) → 씬 만들기 → 자동 테스트의 배치 검사가 0 인지</td></tr>"
    "<tr><td>특징 · 판정 기준</td><td>CreateStageDataFiles 의 특징 글 → 씬 만들기 → 웹 빌드 → wrangler deploy</td></tr>"
    "<tr><td>웹에 올리기</td><td>웹 빌드 → Builds/WebGL/Build 를 gh-pages 브랜치에 커밋 · 푸시 (GitHub Pages)</td></tr></table>",
]
open(os.path.join(SRC, "2_MVP_아키텍처.html"), "w", encoding="utf-8").write(page("MVP 아키텍처", "".join(arch)))

for n in ["1_게임_플로우", "2_MVP_아키텍처"]:
    html = os.path.join(SRC, n + ".html")
    pdf = os.path.join(DOCS, n + ".pdf")
    subprocess.run([EDGE, "--headless", "--disable-gpu", "--no-pdf-header-footer", f"--print-to-pdf={pdf}", "file:///" + html.replace("\\", "/")],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print(n, os.path.getsize(pdf))

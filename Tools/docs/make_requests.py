"""request_data 하나에서 요청 현황과 팀 PDF 두 개를 만들어요."""
import base64
from collections import Counter
from datetime import date
from html import escape
import io
from pathlib import Path
import sys
from urllib.parse import quote

from PIL import Image

import paths
from doc_pdf import BASE_CSS, write_pdf
from request_data import ROOT, inventory

SITE = "https://azlsh-web.github.io/FindDokkaebi/docs/"
CSS = BASE_CSS + """
body { font-size: 11px; } table { table-layout: fixed; }
th, td, code { overflow-wrap: anywhere; } code { font-size: 9px; }
tr { break-inside: avoid; } thead { display: table-header-group; }
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 9px 12px; }
.small { color: #6d645c; font-size: 10px; }
.preview { width: 52px; height: 58px; object-fit: contain; float: left; margin: 0 8px 4px 0; }
"""
DEADLINE = "출품 마감 2026-11-25(수). 팀 납품일은 아직 합의하지 않았어요."
PLANNING = "5챕터는 흐름을 논의 중이에요. 신규 파일의 이름·규격은 제안이며, 자세·소지품 조합·소리 타이밍은 확정 뒤 맞춰 주세요. 신규 연출의 코드·씬·애니메이션 연결은 개발이 맡아요."
RECEIVED = "대사 상자·그림판 종이·채점표·소풍 카드(11/17), 촛대 게이지(11/18)는 받았어요. 다시 요청하지 않아요."


def md_cell(value):
    return escape(value).replace("|", "&#124;").replace("\n", "<br>")


def make_status(rows):
    lines = ["# 아트·사운드 요청 현황", "", f"> **상태** 최신 · **담당** 개발 · **마지막 갱신** {date.today()}", "",
             "이 파일이 요청 상태의 기준이에요. `Tools/docs/request_data.py`와 실제 파일을 대조해 자동으로 만들어요. 팀 PDF도 같은 목록에서 만들며 **교체·신규 요청만** 담아요.", "",
             "- **받음**: 아트팀 또는 기획 자료에서 받은 그림. 게임용으로 자르거나 나눈 것도 포함해요.",
             "- **임시**: 게임에 있지만 Codex·VARCO·색 바꾸기·코드로 만든 파일. **교체**는 팀에 받을 것, **유지**는 개발이 그대로 쓸 단순 UI예요.",
             "- **없음**: 별도 파일이 아직 없는 신규 요청. **개발 연결이 필요해요**.",
             "- 수량은 게임 파일 기준이에요. 촛대 원본 한 장을 막대·불꽃 두 파일로 나누기도 해요. 사용하지 않는 옛 그림은 요청에서 제외했어요.", "",
             DEADLINE, "", PLANNING, "", "## 바로 보낼 문서", "",
             "- [아트팀 전달사항](아트.pdf)", "- [사운드팀 전달사항](사운드.pdf)",
             "- [게임 플로우와 화면 사진](../1_기획/게임_플로우.md) · [결정 기록](../2_제작/결정_기록.md) · [남은 개발 작업](../../BACKLOG.md)", "",
             "## 요청 요약", "", "| 팀 | 받음 | 임시 | 없음 | 지금 요청 |", "|---|---:|---:|---:|---:|"]
    for team in ("아트", "사운드"):
        selected = [r for r in rows if r.team == team]
        c = Counter(r.status for r in selected)
        lines.append(f"| {team} | {c['받음']} | {c['임시']} | {c['없음']} | {sum(r.action in ('교체', '신규') for r in selected)} |")
    lines += ["", RECEIVED, "", "기존 소리 **17개(배경음 4곡·효과음 13개)는 모두 VARCO 임시본**이에요.", "",
              "아래 모든 파일 경로는 `Assets/` 기준이에요.", ""]
    for team in ("아트", "사운드"):
        for status in ("없음", "임시", "받음"):
            selected = [r for r in rows if r.team == team and r.status == status]
            if not selected:
                continue
            lines += [f"## {team} · {status} ({len(selected)}개)", "",
                      "| 파일 | 할 일·만든 방법 | 나오는 곳 | 규격·요청 내용 | 근거 |", "|---|---|---|---|---|"]
            for r in selected:
                cells = [f"`{r.file}`", md_cell(r.action + " / " + r.method), md_cell(r.where), md_cell(r.spec + " / " + r.note), md_cell(r.evidence)]
                lines.append("| " + " | ".join(cells) + " |")
            lines.append("")
    Path(paths.doc(paths.REQUESTS)).write_text("\n".join(lines), encoding="utf-8")
    print(paths.REQUESTS, len(rows), "개")


def thumb(r):
    if r.status == "없음":
        return ""
    with Image.open(ROOT / "Assets" / r.file) as src:
        im = src.convert("RGBA")
    im.thumbnail((104, 116))
    bg = Image.new("RGBA", im.size, "#edf1f3")
    bg.alpha_composite(im)
    buf = io.BytesIO()
    bg.convert("RGB").save(buf, "JPEG", quality=80)
    return f"<img class='preview' src='data:image/jpeg;base64,{base64.b64encode(buf.getvalue()).decode()}'>"


def make_team_pdf(team, rows):
    pending = [r for r in rows if r.team == team and r.action in ("교체", "신규")]
    counts = Counter(r.action for r in pending)
    body = [f"<h1>찾아라! 도깨비 {team}팀 전달사항</h1><p class='sub'>{date.today()} 기준 · {DEADLINE}</p>",
            f"<div class='note'><b>교체 {counts['교체']}개 + 신규 {counts['신규']}개</b>. 받은 파일과 개발이 유지할 단순 UI는 요청에서 뺐어요.</div><p>{PLANNING}</p>",
            f"<p><a href='{SITE}{quote(paths.REQUESTS[:-3] + '.html')}'>받음·임시·없음 전체 현황</a> · "
            f"<a href='{SITE}{quote(paths.FLOW[:-3] + '.html')}'>게임 플로우와 화면 사진</a></p>"]
    body.append(f"<p>{RECEIVED}</p>" if team == "아트" else
                "<p>기존 17개는 모두 VARCO Sound 임시본이에요. 생성 프롬프트·출처는 ArtSource/Sound/varco/출처.txt에 있어요. 신규 10개는 아직 파일과 코드 연결이 없어요.</p>")
    for status, title in (("없음", "새로 필요한 것"), ("임시", "현재 임시본을 교체할 것")):
        selected = [r for r in pending if r.status == status]
        body.append(f"<h2>{title} ({len(selected)}개)</h2>")
        for group in dict.fromkeys(r.group for r in selected):
            body.append(f"<h3>{escape(group)}</h3><table><thead><tr><th style='width:32%'>파일·규격</th><th style='width:30%'>나오는 곳·현재 상태</th><th>요청 내용</th></tr></thead><tbody>")
            for r in selected:
                if r.group != group:
                    continue
                file = escape(r.file).replace("/", "/<wbr>").replace("_", "_<wbr>")
                body.append(f"<tr><td>{thumb(r) if team == '아트' else ''}<code>{file}</code><br>{escape(r.spec)}</td>"
                            f"<td>{escape(r.where)}<p class='small'>{escape(r.method)}</p></td>"
                            f"<td>{escape(r.note)}<p class='small'>근거: {escape(r.evidence)}</p></td></tr>")
            body.append("</tbody></table>")
    body.append("<h2>보내는 방법</h2>")
    if team == "아트":
        body.append("<p>PNG로 보내 주세요. 기존 그림은 같은 이름·비율·투명 영역을 유지하고, 새 그림은 글자 없이 캐릭터 바깥을 투명하게 해 주세요. "
                    "챕터 카드·배경은 꽉 채워 주세요. 물건은 현재 오답의 틀린 특징과 원본의 판정 기준을 유지해 주세요. "
                    "Sprites 아래 표의 경로대로 ZIP에 묶어 바뀐 것만 보내 주세요. 기존 apply_art_zip.py는 옛 종이 그림 5장만 검사하므로 이 신규 목록에는 쓰지 않아요. 연결·검수는 개발이 맡아요.</p>")
    else:
        body.append("<p>기존 소리는 같은 이름으로 WAV 또는 OGG를 보내 주세요. 효과음 앞 무음은 빼고 배경음은 반복이 자연스럽게 해 주세요. "
                    "Resources/Sound 아래 표의 경로대로 ZIP에 묶어 주세요. 배경음은 게임에서 기본 음량의 절반으로 재생해요. "
                    "신규 소리는 개발이 재생 코드를 추가해야 해요. 외부 소리는 파일별 출처·사용 조건을 함께 적어 주세요.</p>")
    # 짧은 납품 안내만 마지막 페이지에 남지 않게 표 앞에서 알려 줘요.
    body = body[:4] + body[-2:] + body[4:-2]
    html = f"<!doctype html><html lang='ko'><meta charset='utf-8'><title>{team}팀 전달사항</title><style>{CSS}</style><body>{''.join(body)}</body></html>"
    work = ROOT / "Temp/DocsHtml"
    work.mkdir(parents=True, exist_ok=True)
    (work / f"{team}.html").write_text(html, encoding="utf-8")
    output = paths.ART_REQUEST if team == "아트" else paths.SOUND_REQUEST
    print(output, write_pdf(html, paths.doc(output)))


def main():
    rows = inventory()
    args = sys.argv[1:]
    if not args or "--status" in args:
        make_status(rows)
    if not args or "--art" in args:
        make_team_pdf("아트", rows)
    if not args or "--sound" in args:
        make_team_pdf("사운드", rows)


if __name__ == "__main__":
    main()

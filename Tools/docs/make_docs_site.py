# 문서 사이트 : Docs 폴더를 웹에서 한곳에 모아 볼 수 있게 만들어요
#   python Tools/docs/make_docs_site.py [만들 폴더]      (기본 Temp/DocsSite)
#   GitHub Actions(.github/workflows/docs-site.yml)가 Docs 가 바뀐 푸시마다 실행해서
#   gh-pages 브랜치의 docs 폴더에 올려요 → https://azlsh-web.github.io/FindDokkaebi/docs/
#
#   - 목록은 Docs/0_문서_안내.md 의 기획 · 전달 · 개발 · 기록 표에서 읽어요. 표에 없는 파일은 "그 밖의 파일"에 나와요.
#   - .md 는 같은 이름의 .html 로 바꾸고, 문서끼리 이은 .md 링크도 .html 로 바꿔요.
#   - PDF 는 브라우저에서 바로 열리고, 엑셀 · 발표 파일은 내려받아요.
#   - 플로우 사진은 manifest.tsv 로 화면별 사진 모음 페이지를 만들어요.
import csv
import html
import os
import re
import shutil
import subprocess
import sys
from datetime import datetime, timedelta, timezone
from urllib.parse import quote, unquote

import markdown
from markdown.extensions.toc import slugify_unicode

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
DOCS = os.path.join(ROOT, "Docs")
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(ROOT, "Temp", "DocsSite")
REPO = "https://github.com/azlsh-web/FindDokkaebi"
GAME = "../"                                   # 웹 게임은 사이트 한 단계 위 (gh-pages 맨 위)
SECTIONS = ["기획", "전달", "개발", "기록"]
GUIDE = "0_문서_안내.md"
PHOTOS = "기획/플로우_사진"
KST = timezone(timedelta(hours=9))

CSS = """
:root { --bg:#fbf6ec; --card:#fffdf8; --ink:#4a2b16; --sub:#7a604a; --line:#e8dcc8; --accent:#e9776b; --accent-ink:#fff;
        --chip:#f3e7d3; --code:#f5ecdc; }
@media (prefers-color-scheme: dark) {
  :root { --bg:#1f1a16; --card:#2a231d; --ink:#f3e7d6; --sub:#c4ad95; --line:#3e342b; --accent:#f08a7e; --accent-ink:#2a1a12;
          --chip:#3a3027; --code:#332a22; }
}
* { box-sizing:border-box; }
body { margin:0; background:var(--bg); color:var(--ink); font:16px/1.65 "Pretendard","Apple SD Gothic Neo","Malgun Gothic","Noto Sans KR",sans-serif; }
a { color:inherit; }
.wrap { max-width:960px; margin:0 auto; padding:24px 16px 64px; }
header.top { display:flex; flex-wrap:wrap; align-items:center; gap:8px 16px; margin-bottom:8px; }
header.top h1 { font-size:26px; margin:0; flex:1 1 auto; }
.meta { color:var(--sub); font-size:14px; margin:0 0 24px; }
.btn { display:inline-block; padding:8px 16px; border-radius:999px; background:var(--accent); color:var(--accent-ink); text-decoration:none; font-weight:700; }
.back { display:inline-block; margin-bottom:16px; color:var(--sub); text-decoration:none; }
h2.sec { font-size:20px; margin:32px 0 4px; }
p.note { color:var(--sub); margin:0 0 12px; font-size:14px; }
.grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(260px,1fr)); gap:12px; margin-top:12px; }
.card { display:block; background:var(--card); border:1px solid var(--line); border-radius:14px; padding:14px 16px; text-decoration:none; }
.card:hover { border-color:var(--accent); }
.card .t { font-weight:700; font-size:17px; display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
.card .d { color:var(--sub); font-size:14px; margin-top:4px; }
.card .more { margin-top:8px; font-size:14px; }
.card .more a { color:var(--accent); }
.chip { font-size:12px; font-weight:600; padding:2px 8px; border-radius:999px; background:var(--chip); color:var(--sub); }
.doc { background:var(--card); border:1px solid var(--line); border-radius:14px; padding:8px 24px 24px; overflow-wrap:anywhere; }
.doc table { border-collapse:collapse; width:100%; display:block; overflow-x:auto; font-size:15px; }
.doc th, .doc td { border:1px solid var(--line); padding:6px 10px; text-align:left; vertical-align:top; }
.doc th { background:var(--chip); }
.doc code { background:var(--code); padding:1px 5px; border-radius:6px; font-size:0.92em; }
.doc pre { background:var(--code); padding:12px; border-radius:10px; overflow-x:auto; }
.doc pre code { background:none; padding:0; }
.doc img { max-width:100%; }
.doc a { color:var(--accent); }
.shots h2 { font-size:18px; margin:28px 0 8px; }
.shots .row { display:grid; grid-template-columns:repeat(auto-fill,minmax(150px,1fr)); gap:10px; }
.shots figure { margin:0; background:var(--card); border:1px solid var(--line); border-radius:12px; overflow:hidden; }
.shots img { width:100%; display:block; aspect-ratio:9/16; object-fit:cover; background:var(--chip); }
.shots figcaption { padding:6px 8px 8px; font-size:13px; }
.shots figcaption .l { color:var(--sub); display:block; margin-top:2px; }
@media (max-width:520px) { .doc { padding:4px 14px 16px; } header.top h1 { font-size:22px; } }
"""


def page(title, body, depth):
    up = "../" * depth
    return (f"<!doctype html><html lang='ko'><head><meta charset='utf-8'>"
            f"<meta name='viewport' content='width=device-width,initial-scale=1'>"
            f"<title>{html.escape(title)} · 찾아라! 도깨비</title><link rel='stylesheet' href='{up}style.css'></head>"
            f"<body><div class='wrap'>{body}</div></body></html>")


def updated():
    """마지막 커밋 시간과 번호 (git 이 없으면 지금 시간)"""
    try:
        out = subprocess.run(["git", "log", "-1", "--format=%cI %h %H"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.split()
        when = datetime.fromisoformat(out[0]).astimezone(KST)
        return when.strftime("%Y-%m-%d %H:%M"), out[1], out[2]
    except Exception:
        return datetime.now(KST).strftime("%Y-%m-%d %H:%M"), "", ""


def md_links_to_html(text):
    """문서끼리 이은 .md 링크를 .html 로 (#앵커는 그대로)"""
    return re.sub(r"(\]\()([^)\s]+?)\.md(#[^)]*)?\)", lambda m: f"{m.group(1)}{m.group(2)}.html{m.group(3) or ''})", text)


def render_md(src_rel):
    """Docs 안의 .md 하나를 같은 자리의 .html 로"""
    text = open(os.path.join(DOCS, src_rel), encoding="utf-8").read()
    m = re.search(r"^#\s+(.+)$", text, re.M)
    title = m.group(1).strip() if m else os.path.splitext(os.path.basename(src_rel))[0]
    body = markdown.markdown(md_links_to_html(text), extensions=["tables", "fenced_code", "sane_lists", "toc"],
                             extension_configs={"toc": {"slugify": slugify_unicode}})
    depth = src_rel.count("/")
    out = os.path.join(OUT, os.path.splitext(src_rel)[0] + ".html")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    back = f"<a class='back' href='{'../' * depth}index.html'>← 문서 목록</a>"
    open(out, "w", encoding="utf-8").write(page(title, back + f"<article class='doc'>{body}</article>", depth))
    return title


def site_href(target):
    """안내 문서의 링크 → 사이트 안 주소 (.md 는 .html)"""
    path = unquote(target)
    if path.endswith(".md"):
        path = path[:-3] + ".html"
    return quote(path, safe="/#")


def kind(target):
    ext = os.path.splitext(unquote(target))[1].lower()
    return {".pdf": "PDF", ".md": "글", ".xlsx": "엑셀", ".pptx": "발표"}.get(ext, ext.lstrip(".").upper())


def read_guide():
    """0_문서_안내.md 의 표 → {구역: (안내 글, [(링크들, 받는 곳, 설명)])}"""
    text = open(os.path.join(DOCS, GUIDE), encoding="utf-8").read()
    sections = {}
    for name in SECTIONS:
        m = re.search(rf"^## {name}\s*$(.*?)(?=^## |\Z)", text, re.M | re.S)
        if not m:
            continue
        block = m.group(1)
        notes = [l.strip() for l in block.split("\n") if l.strip() and not l.startswith("|") and not l.startswith("```")]
        rows = []
        for line in block.split("\n"):
            if not line.startswith("|") or "](" not in line:
                continue
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            links = re.findall(r"\[([^\]]+)\]\(([^)]+)\)", line)
            who = cells[3] if len(cells) >= 5 else ""
            rows.append((links, who, cells[-1]))
        sections[name] = (notes[0] if notes else "", rows)
    return sections


def photo_page():
    """플로우 사진 모음 : 화면 흐름 순서대로 구역별 사진과 설명"""
    rows = list(csv.reader(open(os.path.join(DOCS, PHOTOS, "manifest.tsv"), encoding="utf-8", newline=""), delimiter="\t", quotechar=None))
    body = ["<a class='back' href='../../index.html'>← 문서 목록</a>",
            "<header class='top'><h1>플로우 사진</h1></header>",
            f"<p class='meta'>자동 플레이로 찍은 화면 {len(rows)}장이에요. 사진을 누르면 크게 보여요.</p><div class='shots'>"]
    current = None
    for row in rows:
        file, section, caption = row[0], row[1], row[2]
        line = row[3] if len(row) > 3 else ""
        if section != current:
            if current is not None:
                body.append("</div>")
            body.append(f"<h2>{html.escape(section)}</h2><div class='row'>")
            current = section
        l = f"<span class='l'>{html.escape(line)}</span>" if line else ""
        body.append(f"<figure><a href='{quote(file)}'><img loading='lazy' src='{quote(file)}' alt='{html.escape(caption)}'></a>"
                    f"<figcaption>{html.escape(caption)}{l}</figcaption></figure>")
    body.append("</div></div>")
    open(os.path.join(OUT, PHOTOS, "index.html"), "w", encoding="utf-8").write(page("플로우 사진", "".join(body), 2))
    return len(rows)


def main():
    # 폴더 자체는 두고 안만 비워요 (미리보기 서버가 폴더를 잡고 있어도 되게)
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        p = os.path.join(OUT, f)
        shutil.rmtree(p) if os.path.isdir(p) else os.remove(p)
    shutil.copytree(DOCS, OUT, dirs_exist_ok=True)
    open(os.path.join(OUT, "style.css"), "w", encoding="utf-8").write(CSS)

    titles = {}
    for folder, _, files in os.walk(DOCS):
        for f in files:
            if f.endswith(".md"):
                rel = os.path.relpath(os.path.join(folder, f), DOCS).replace(os.sep, "/")
                titles[rel] = render_md(rel)
    photo_count = photo_page()

    when, short, full = updated()
    commit = f" · <a href='{REPO}/commit/{full}'>{short}</a>" if short else ""
    body = [f"<header class='top'><h1>찾아라! 도깨비 문서</h1><a class='btn' href='{GAME}'>게임 하기</a></header>",
            f"<p class='meta'>마지막 갱신 {when}{commit} · Docs 폴더가 바뀌어 푸시되면 저절로 새로 만들어져요.</p>"]

    guide = read_guide()
    linked = set()
    for name in SECTIONS:
        note, rows = guide.get(name, ("", []))
        cards = []
        for links, who, desc in rows:
            (label, target), extra = links[0], links[1:]
            linked.update(unquote(t).replace("\\", "/") for _, t in links)
            chip = f"<span class='chip'>{kind(target)}</span>"
            who_chip = f"<span class='chip'>{html.escape(who)}</span>" if who else ""
            more = " · ".join(f"<a href='{site_href(t)}'>{html.escape(l)}</a>" for l, t in extra)
            cards.append(f"<div class='card'><a href='{site_href(target)}' style='text-decoration:none'><div class='t'>{html.escape(label)} {chip}{who_chip}</div>"
                         f"<div class='d'>{html.escape(desc)}</div></a>{f'<div class=more>함께 보기 : {more}</div>' if more else ''}</div>")
        if name == "기획":
            cards.insert(1, f"<a class='card' href='{quote(PHOTOS)}/index.html'><div class='t'>플로우 사진 <span class='chip'>사진 {photo_count}장</span></div>"
                            f"<div class='d'>자동 플레이로 찍은 화면을 흐름 순서대로</div></a>")
        # 표에 없는 파일도 빠짐없이
        folder = os.path.join(DOCS, name)
        others = []
        if os.path.isdir(folder):
            for f in sorted(os.listdir(folder)):
                rel = f"{name}/{f}"
                if os.path.isdir(os.path.join(folder, f)) or rel in linked or f.endswith(".tsv"):
                    continue
                label = titles.get(rel) or re.sub(r"^\d+_", "", os.path.splitext(f)[0]).replace("_", " ")
                others.append(f"<a class='card' href='{site_href(rel)}'><div class='t'>{html.escape(label)} <span class='chip'>{kind(rel)}</span></div>"
                              f"<div class='d'>{html.escape(f)}</div></a>")
        if not cards and not others:
            continue
        body.append(f"<h2 class='sec'>{name}</h2>" + (f"<p class='note'>{html.escape(note)}</p>" if note else ""))
        body.append("<div class='grid'>" + "".join(cards + others) + "</div>")

    body.append(f"<h2 class='sec'>문서 안내</h2><div class='grid'><a class='card' href='{quote(GUIDE[:-3])}.html'><div class='t'>폴더 구조와 문서 갱신 방법 <span class='chip'>글</span></div>"
                f"<div class='d'>개발용 안내 : 문서를 다시 만드는 명령, 대사 수정 받기</div></a></div>")
    open(os.path.join(OUT, "index.html"), "w", encoding="utf-8").write(page("문서", "".join(body), 0))

    missing = [t for t in linked if not os.path.exists(os.path.join(DOCS, t.split("#")[0]))]
    if missing:
        print("경고 : 안내 문서가 가리키는데 없는 파일", missing)
    print(f"문서 사이트 {OUT} (글 {len(titles)}개, 사진 {photo_count}장)")


main()

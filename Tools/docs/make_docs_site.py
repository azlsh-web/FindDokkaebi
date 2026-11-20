# 문서 사이트 : Docs 폴더를 웹에서 한곳에 모아 볼 수 있게 만들어요
#   python Tools/docs/make_docs_site.py [만들 폴더]      (기본 Temp/DocsSite)
#   GitHub Actions(.github/workflows/docs-site.yml)가 Docs 가 바뀐 푸시마다 실행해서
#   gh-pages 브랜치의 docs 폴더에 올려요 → https://azlsh-web.github.io/FindDokkaebi/docs/
#
#   - 경로와 구역은 paths.py 에서 읽어요. 카드 목록과 설명은 시작 문서(paths.GUIDE)의 구역별 표에서 읽어요.
#   - 표에 없는 파일도 그 구역에 이름만으로 나와요.
#   - .md 는 같은 이름의 .html 로 바꾸고, 문서끼리 이은 .md 링크도 .html 로 바꿔요.
#   - 글 문서 맨 위의 "> **상태** …" 줄을 읽어 카드에 상태(최신 / 검토 필요 / 보관 / 자동)를 붙여요.
#   - Docs 밖 문서(paths.EXTERNAL_PAGES)도 사이트 페이지로 만들고, 옛 주소(paths.LEGACY)도 남겨요.
#   - 모양은 site_style.css 예요.
#   시작 문서가 없는 파일을 가리키면 경고하고 1 로 끝나요.
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

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paths

sys.stdout.reconfigure(encoding="utf-8")
ROOT = paths.ROOT
DOCS = paths.DOCS
OUT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(ROOT, "Temp", "DocsSite")
STYLE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "site_style.css")
REPO = "https://github.com/azlsh-web/FindDokkaebi"
GAME = "../"                                   # 웹 게임은 사이트 한 단계 위 (gh-pages 맨 위)
KST = timezone(timedelta(hours=9))
STATE_RE = re.compile(r"^>\s*\*\*상태\*\*\s*([^·\n]+)", re.M)
STATE_CLASS = {"최신": "state-latest", "검토 필요": "state-review", "보관": "state-archive"}
KIND = {".pdf": "PDF", ".md": "글", ".xlsx": "엑셀", ".pptx": "발표"}


# ---- 작은 도우미 ----
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


def site_href(target):
    """문서 링크 → 사이트 안 주소 (.md 는 .html)"""
    path, _, anchor = unquote(target).partition("#")
    if path.endswith(".md"):
        path = path[:-3] + ".html"
    return quote(path, safe="/") + (f"#{quote(anchor)}" if anchor else "")


def kind(target):
    ext = os.path.splitext(unquote(target).split("#")[0])[1].lower()
    return KIND.get(ext, ext.lstrip(".").upper())


def chip(text, cls=""):
    return f"<span class='chip {cls}'>{html.escape(text)}</span>" if text else ""


def rel_to_docs(path):
    return os.path.relpath(path, DOCS).replace(os.sep, "/")


class Guide:
    """시작 문서(0_시작하기.md)를 읽어요. 구역마다 "## 이름" 아래에 안내 글 한 줄과 표가 있어요."""

    def __init__(self, path):
        self.text = open(path, encoding="utf-8").read()

    def section(self, heading):
        """→ (안내 글, [(열 이름 → 칸 dict, [(링크 글, 주소), …]), …])"""
        m = re.search(rf"^## {re.escape(heading)}\s*$(.*?)(?=^## |\Z)", self.text, re.M | re.S)
        if not m:
            return "", []
        lines = m.group(1).split("\n")
        notes = [l.strip() for l in lines if l.strip() and not l.startswith(("|", "```", "-", ">"))]
        table = [l for l in lines if l.startswith("|")]
        rows = []
        if len(table) >= 3:
            head = [c.strip() for c in table[0].strip().strip("|").split("|")]
            for line in table[2:]:
                cells = [c.strip() for c in line.strip().strip("|").split("|")]
                rows.append((dict(zip(head, cells)), re.findall(r"\[([^\]]+)\]\(([^)]+)\)", line)))
        return (notes[0] if notes else ""), rows


class Site:
    """Docs 폴더 하나 → 사이트 하나. 순서 : 복사 → 글 페이지 → 사진 페이지 → 옛 주소 → 홈"""

    def __init__(self, out):
        self.out = out
        self.titles = {}       # 사이트 기준 .md 경로 → 제목
        self.states = {}       # 사이트 기준 .md 경로 → 상태
        self.photo_count = 0
        # Docs 밖 문서로 가는 링크 → 그 문서의 사이트 페이지 (예: BACKLOG.md → 2_제작/백로그.md)
        self.external = {src: site for site, src in paths.EXTERNAL_PAGES.items()}

    def write(self, rel, text):
        dst = os.path.join(self.out, *rel.split("/"))
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        open(dst, "w", encoding="utf-8").write(text)

    # ---- 글 페이지 ----
    def fix_links(self, text, site_rel):
        """문서끼리 이은 .md 링크를 .html 로, Docs 밖 문서 링크는 그 사이트 페이지로 (#앵커는 그대로)"""
        base = os.path.dirname(site_rel)

        def fix(m):
            target = m.group(2)
            if re.match(r"(https?:|mailto:|#)", target):
                return m.group(0)
            path, _, anchor = target.partition("#")
            in_repo = os.path.normpath(os.path.join("Docs", base, unquote(path))).replace(os.sep, "/")
            if in_repo in self.external:
                path = os.path.relpath(self.external[in_repo], base or ".").replace(os.sep, "/")
            if path.endswith(".md"):
                path = path[:-3] + ".html"
            return f"{m.group(1)}{path}{'#' + anchor if anchor else ''})"

        return re.sub(r"(\]\()([^)\s]+)\)", fix, text)

    def render_md(self, src_file, site_rel):
        """글 문서 하나 → 사이트의 site_rel 자리에 .html 로"""
        text = open(src_file, encoding="utf-8").read()
        m = re.search(r"^#\s+(.+)$", text, re.M)
        title = m.group(1).strip() if m else os.path.splitext(os.path.basename(site_rel))[0]
        s = STATE_RE.search(text)
        self.titles[site_rel] = title
        self.states[site_rel] = s.group(1).strip() if s else ""
        body = markdown.markdown(self.fix_links(text, site_rel), extensions=["tables", "fenced_code", "sane_lists", "toc"],
                                 extension_configs={"toc": {"slugify": slugify_unicode}})
        depth = site_rel.count("/")
        back = f"<a class='back' href='{'../' * depth}index.html'>← 문서 목록</a>"
        self.write(site_rel[:-3] + ".html", page(title, back + f"<article class='doc'>{body}</article>", depth))

    def render_all_md(self):
        for folder, _, files in os.walk(DOCS):
            for f in files:
                if f.endswith(".md"):
                    self.render_md(os.path.join(folder, f), rel_to_docs(os.path.join(folder, f)))
        for site_rel, src in paths.EXTERNAL_PAGES.items():
            self.render_md(os.path.join(ROOT, src), site_rel)

    # ---- 플로우 사진 ----
    def photo_page(self):
        """플로우 사진 모음 : 화면 흐름 순서대로 구역별 사진과 설명"""
        rows = list(csv.reader(open(paths.doc(paths.PHOTOS + "/manifest.tsv"), encoding="utf-8", newline=""), delimiter="\t", quotechar=None))
        depth = paths.PHOTOS.count("/") + 1
        body = [f"<a class='back' href='{'../' * depth}index.html'>← 문서 목록</a>",
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
        self.write(paths.PHOTOS + "/index.html", page("플로우 사진", "".join(body), depth))
        self.photo_count = len(rows)

    # ---- 옛 주소 ----
    def legacy(self):
        """폴더를 바꾸기 전 주소로 들어와도 열리게 : 글은 넘어가는 페이지, 파일은 같은 파일을 옛 자리에"""
        for old, new in paths.LEGACY.items():
            if new.endswith((".md", ".html")):
                old_html = old[:-3] + ".html" if old.endswith(".md") else old
                new_html = new[:-3] + ".html" if new.endswith(".md") else new
                target = quote(os.path.relpath(new_html, os.path.dirname(old_html) or ".").replace(os.sep, "/"))
                self.write(old_html, f"<!doctype html><meta charset='utf-8'><meta http-equiv='refresh' content='0; url={target}'>"
                                     f"<link rel='canonical' href='{target}'><p>문서가 옮겨졌어요 : <a href='{target}'>새 주소로 가기</a></p>")
            else:
                dst = os.path.join(self.out, *old.split("/"))
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copyfile(paths.doc(new), dst)

    # ---- 홈 ----
    def card(self, label, target, desc, chips=(), extra=()):
        state = self.states.get(unquote(target).split("#")[0], "")
        more = " · ".join(f"<a href='{site_href(t)}'>{html.escape(l)}</a>" for l, t in extra)
        return (f"<div class='card'><a href='{site_href(target)}' style='text-decoration:none'>"
                f"<div class='t'>{html.escape(label)} {chip(kind(target))}{chip(state, STATE_CLASS.get(state, ''))}{''.join(chips)}</div>"
                f"<div class='d'>{html.escape(desc)}</div></a>{f'<div class=more>함께 보기 : {more}</div>' if more else ''}</div>")

    def role_block(self, guide):
        _, rows = guide.section("역할별로 먼저 볼 문서")
        cards = []
        for row, links in rows:
            who = next(iter(row.values()))
            route = " → ".join(f"<a href='{site_href(t)}'>{html.escape(l)}</a>" for l, t in links)
            cards.append(f"<div class='card'><div class='t'>{html.escape(who)}</div><div class='d'>{route}</div></div>")
        return "<h2 class='sec'>역할별로 먼저 볼 문서</h2><div class='roles'>" + "".join(cards) + "</div>" if cards else ""

    def section_block(self, guide, folder, name, linked):
        note, rows = guide.section(name)
        cards = []
        for row, links in rows:
            if not links:
                continue
            (label, target), extra = links[0], links[1:]
            linked.update(unquote(t).split("#")[0] for _, t in links)
            how = row.get("만드는 법", "")
            chips = [chip(how) if how == "자동" else "", chip(row.get("받는 곳", ""))]
            cards.append(self.card(label, target, row.get("내용", "").replace("`", ""), chips, extra))
        if paths.PHOTOS.startswith(folder + "/"):
            cards.insert(1, f"<a class='card' href='{quote(paths.PHOTOS)}/index.html'><div class='t'>플로우 사진 {chip('사진 %d장' % self.photo_count)}{chip('자동')}</div>"
                            f"<div class='d'>자동 플레이로 찍은 화면을 흐름 순서대로</div></a>")
        # 표에 없는 파일도 빠짐없이 (하위 폴더 포함, 사진 폴더는 빼요)
        others = []
        for sub, dirs, files in os.walk(paths.doc(folder)):
            dirs[:] = sorted(d for d in dirs if rel_to_docs(os.path.join(sub, d)) != paths.PHOTOS)
            for f in sorted(files):
                rel = rel_to_docs(os.path.join(sub, f))
                if rel in linked or f.endswith(".tsv"):
                    continue
                label = self.titles.get(rel) or re.sub(r"^\d+_", "", os.path.splitext(f)[0]).replace("_", " ")
                others.append(self.card(label, rel, f))
        for site_rel in paths.EXTERNAL_PAGES:
            if site_rel.startswith(folder + "/") and site_rel not in linked:
                others.append(self.card(self.titles[site_rel], site_rel, ""))
        if not cards and not others:
            return ""
        return (f"<h2 class='sec'>{html.escape(name)}</h2>" + (f"<p class='note'>{html.escape(note)}</p>" if note else "")
                + "<div class='grid'>" + "".join(cards + others) + "</div>")

    def home(self):
        guide = Guide(paths.doc(paths.GUIDE))
        when, short, full = updated()
        commit = f" · <a href='{REPO}/commit/{full}'>{short}</a>" if short else ""
        body = [f"<header class='top'><h1>찾아라! 도깨비 문서</h1><a class='btn' href='{GAME}'>게임 하기</a></header>",
                f"<p class='meta'>마지막 갱신 {when}{commit} · Docs 폴더가 바뀌어 푸시되면 저절로 새로 만들어져요.</p>",
                self.role_block(guide)]
        linked = set()
        for folder, name in paths.SECTIONS:
            body.append(self.section_block(guide, folder, name, linked))
        body.append("<h2 class='sec'>안내</h2><div class='grid'>"
                    + self.card("시작하기", paths.GUIDE, "폴더 구조, 문서 상태 표시, 역할별로 볼 문서") + "</div>")
        self.write("index.html", page("문서", "".join(body), 0))
        missing = sorted(t for t in linked if t not in paths.EXTERNAL_PAGES and not os.path.exists(paths.doc(t)))
        if missing:
            print("경고 : 시작 문서가 가리키는데 없는 파일", missing)
        return missing

    def build(self):
        # 폴더 자체는 두고 안만 비워요 (미리보기 서버가 폴더를 잡고 있어도 되게)
        os.makedirs(self.out, exist_ok=True)
        for f in os.listdir(self.out):
            p = os.path.join(self.out, f)
            shutil.rmtree(p) if os.path.isdir(p) else os.remove(p)
        shutil.copytree(DOCS, self.out, dirs_exist_ok=True)
        shutil.copyfile(STYLE, os.path.join(self.out, "style.css"))
        self.render_all_md()
        self.photo_page()
        self.legacy()
        missing = self.home()
        print(f"문서 사이트 {self.out} (글 {len(self.titles)}개, 사진 {self.photo_count}장, 옛 주소 {len(paths.LEGACY)}개)")
        return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(Site(OUT).build())

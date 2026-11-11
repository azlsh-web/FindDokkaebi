# 아트.pdf : 아트팀에 보내는 요청
#   python Tools/docs/make_art_request.py
#   요청했던 그림 9장은 모두 받아서 지금은 새 요청이 없어요 (2026-11-08).
#   받은 그림 목록은 art_tree_data.REQUEST 에 남겨 두었고, 받은 ZIP 을 넣는 apply_art_zip.py 가 써요.
from datetime import date
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from doc_pdf import BASE_CSS, write_pdf
from flow_shots import ROOT

OUT = os.path.join(ROOT, "Docs", "전달")
os.makedirs(OUT, exist_ok=True)

CSS = BASE_CSS + """
.note { background: #fbf3e4; border-left: 4px solid #d9a441; padding: 8px 12px; margin: 10px 0; }
"""

html = f"""<!doctype html><html lang='ko'><head><meta charset='utf-8'><title>아트팀 전달사항</title><style>{CSS}</style></head><body>
<h1>찾아라! 도깨비 아트팀 전달사항</h1><p class='sub'>{date.today()} 기준 · 출품 마감 2026-11-15(일)</p>
<div class='note'><b>새 요청:</b> 없음<br><b>수정본 기한:</b> 11월 14일(토)<br><b>파일 이름:</b> 기존과 같게</div>
</body></html>"""

os.makedirs(os.path.join(ROOT, "Temp", "DocsHtml"), exist_ok=True)
open(os.path.join(ROOT, "Temp", "DocsHtml", "아트.html"), "w", encoding="utf-8").write(html)
print("아트.pdf", write_pdf(html, os.path.join(OUT, "아트.pdf")))

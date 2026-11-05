# HTML 글자를 Edge 로 PDF 로 바꿔요. 문서 스크립트들이 같이 씁니다.
import os, subprocess, tempfile, time

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"

BASE_CSS = """
@page { size: A4; margin: 14mm 12mm; }
* { box-sizing: border-box; }
body { font-family: 'Malgun Gothic', sans-serif; color: #2b2622; font-size: 12px; line-height: 1.55; margin: 0; }
h1 { font-size: 24px; margin: 0 0 4px; }
h2 { font-size: 17px; margin: 22px 0 8px; break-after: avoid; }
h3 { font-size: 13.5px; margin: 14px 0 6px; break-after: avoid; }
.sub { color: #6d645c; margin: 0 0 14px; }
table { border-collapse: collapse; width: 100%; margin: 8px 0 14px; }
th, td { border: 1px solid #d8d2ca; padding: 5px 8px; text-align: left; vertical-align: top; }
th { background: #f4f0ea; }
code { font-family: Consolas, monospace; font-size: 11px; }
.num { text-align: right; }
"""


def write_pdf(html, pdf_path):
    """Edge 는 파일을 다 쓰기 전에 돌아올 때가 있어서, 크기가 멈출 때까지 기다린 뒤 옮겨요."""
    work = tempfile.mkdtemp(prefix="finddokkaebi_doc_")
    src = os.path.join(work, "doc.html")
    with open(src, "w", encoding="utf-8") as f:
        f.write(html)
    out = os.path.join(work, "out.pdf")
    subprocess.run([EDGE, "--headless", "--disable-gpu", "--no-pdf-header-footer", f"--user-data-dir={os.path.join(work, 'edge')}",
                    f"--print-to-pdf={out}", "file:///" + src.replace("\\", "/")], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(90):
        if os.path.exists(out) and os.path.getsize(out) > 0:
            size = os.path.getsize(out)
            time.sleep(3)
            if os.path.getsize(out) == size:
                break
        time.sleep(1)
    os.replace(out, pdf_path)
    return os.path.getsize(pdf_path)

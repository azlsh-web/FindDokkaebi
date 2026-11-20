"""옛 PDF 링크에 전체 문서 대신 1KB 안팎의 이동 안내 PDF를 둬요. 표준 라이브러리만 써요."""
from pathlib import Path


def write_redirect(path, target):
    # URL은 ASCII 퍼센트 인코딩된 절대 주소로 전달해요.
    assert target.isascii() and target.startswith("https://")
    content = b"BT /F1 22 Tf 50 750 Td (This document has moved.) Tj 0 -40 Td /F1 13 Tf (Click here to open the updated document online.) Tj ET"
    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R /Annots [6 0 R] >>",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        b"<< /Length " + str(len(content)).encode() + b" >>\nstream\n" + content + b"\nendstream",
        b"<< /Type /Annot /Subtype /Link /Rect [45 700 550 735] /Border [0 0 0] /A << /S /URI /URI (" + target.encode() + b") >> >>",
    ]
    data = bytearray(b"%PDF-1.4\n")
    offsets = [0]
    for i, obj in enumerate(objects, 1):
        offsets.append(len(data))
        data.extend(f"{i} 0 obj\n".encode() + obj + b"\nendobj\n")
    xref = len(data)
    data.extend(f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n".encode())
    for offset in offsets[1:]:
        data.extend(f"{offset:010d} 00000 n \n".encode())
    data.extend(f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n".encode())
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(data)

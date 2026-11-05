# Docs/전달/14_대사_수정표.xlsx : 기획자가 바꿀 대사만 적어 돌려주는 표
#   python Tools/docs/make_dialogue_sheet.py [저장할 곳]
#   돌려받으면 : python Tools/docs/apply_dialogue_edits.py 받은파일.xlsx
import os
import sys

from openpyxl import Workbook
from openpyxl.formatting.rule import FormulaRule
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dialogue_lines import ROOT, read_lines

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Docs", "전달", "14_대사_수정표.xlsx")
FONT = "Malgun Gothic"
LIMIT = 40      # 대사 상자 세 줄 안에 대개 들어가는 글자 수 (반영 도구가 정확한 줄 수를 다시 재요)

HEAD = PatternFill("solid", fgColor="4A2B16")
INPUT = PatternFill("solid", fgColor="FFF4B8")
BAND = PatternFill("solid", fgColor="F6F1EA")
THIN = Side(style="thin", color="D8D2CA")
LINE = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
WRAP = Alignment(wrap_text=True, vertical="center")
MID = Alignment(horizontal="center", vertical="center", wrap_text=True)


def text(size=11, bold=False, color="2B2622"):
    return Font(name=FONT, size=size, bold=bold, color=color)


wb = Workbook()
ws = wb.active
ws.title = "대사"
COLS = [("번호", 17), ("단계", 13), ("상황", 22), ("말하는 사람", 14), ("지금 대사", 52), ("바꿀 대사", 52), ("글자 수", 8), ("메모", 30)]

ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=len(COLS))
ws["A1"] = "바꾸고 싶은 대사의 노란 칸만 채워 주세요. 나머지 칸은 그대로 두세요. 자세한 방법은 `사용법` 탭에 있어요."
ws["A1"].font = text(11, True)
ws["A1"].alignment = WRAP
ws.row_dimensions[1].height = 30

for c, (name, width) in enumerate(COLS, 1):
    cell = ws.cell(row=2, column=c, value=name)
    cell.font = text(11, True, "FFFFFF")
    cell.fill = HEAD
    cell.alignment = MID
    cell.border = LINE
    ws.column_dimensions[cell.column_letter].width = width
ws.row_dimensions[2].height = 24

rows = read_lines()
band, last = False, None
for r, row in enumerate(rows, 3):
    if row["stage"] != last:
        band, last = not band, row["stage"]
    values = [row["id"], row["stage"], row["scene"], row["who"], row["text"], None, f'=IF(F{r}="","",LEN(F{r}))', None]
    for c, value in enumerate(values, 1):
        cell = ws.cell(row=r, column=c, value=value)
        cell.font = text()
        cell.border = LINE
        cell.alignment = MID if c in (1, 2, 4, 7) else WRAP
        if c in (6, 8):
            cell.fill = INPUT
        elif band:
            cell.fill = BAND
    ws.row_dimensions[r].height = 36

end = len(rows) + 2
ws.freeze_panes = "A3"
ws.auto_filter.ref = f"A2:H{end}"
ws.conditional_formatting.add(f"G3:G{end}", FormulaRule(formula=[f'AND(ISNUMBER(G3),G3>{LIMIT})'], font=Font(name=FONT, bold=True, color="D8352A")))

# 사용법
guide = wb.create_sheet("사용법")
guide.column_dimensions["A"].width = 18
guide.column_dimensions["B"].width = 90
content = [
    ("대사 수정표", None),
    ("쓰는 법", "`대사` 탭에서 바꾸고 싶은 대사의 노란 `바꿀 대사` 칸에 새 대사를 적어 주세요. 안 바꿀 대사는 비워 두면 돼요."),
    ("보내는 법", "이 파일을 그대로 개발 쪽에 돌려주세요. 번호로 찾아 적은 줄만 바꿔요."),
    ("줄 추가 · 삭제", "`메모` 칸에 적어 주세요. 예) 이 대사 삭제, 이 뒤에 도깨비 대사 하나 추가. 말하는 사람을 바꿀 때도 메모에 적어 주세요."),
    ("(무작위)", "그 상황이 올 때마다 여러 대사 중 하나가 무작위로 나와요. 같은 상황의 대사는 어느 것이 나와도 자연스러워야 해요."),
    ("길이", f"대사 상자에 세 줄까지 들어가요. 글자 수가 {LIMIT}자를 넘으면 빨갛게 표시돼요. {LIMIT}자는 어림값이고, 반영할 때 실제 줄 수를 다시 재요."),
    ("내용 규칙", "정답을 알려 주면 안 돼요. 색을 어떻게 섞는지는 쓰지 말아 주세요. 결과 대사에는 판정 기준(예: 줄무늬, 시금치)을 쓰지 말아 주세요."),
    ("이 표에 없는 글자", "버튼 글자(게임 시작하기, 다시, 포기 등)와 판정 기준은 대사가 아니라서 빠져 있어요. 바꾸고 싶으면 따로 알려 주세요."),
    ("", None),
    ("쓴 예시", "`대사` 탭의 한 줄을 이렇게 채워요. 실제 게임에 반영되는 내용은 아니에요."),
    ("번호", "S2-틀림-2"),
    ("지금 대사", "짠! 가방... 맞지? 히히."),
    ("바꿀 대사", "짠! 이게 네가 그린 가방이지? 히히."),
    ("메모", "조금 더 놀리는 느낌으로"),
]
for r, (a, b) in enumerate(content, 1):
    ca, cb = guide.cell(row=r, column=1, value=a), guide.cell(row=r, column=2, value=b)
    ca.font = text(15 if r == 1 else 11, True)
    cb.font = text()
    ca.alignment = Alignment(vertical="top", wrap_text=True)
    cb.alignment = Alignment(vertical="top", wrap_text=True)
    if b and len(b) > 45:
        guide.row_dimensions[r].height = 36
guide.cell(row=13, column=2).fill = INPUT
guide.cell(row=14, column=2).fill = INPUT

os.makedirs(os.path.dirname(OUT), exist_ok=True)
wb.save(OUT)
print(os.path.basename(OUT), "대사", len(rows), "줄")

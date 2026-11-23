"""manifest.json에서 판정 기준·전체 조합 대응표와 그림 비교 화면을 만들어요.

게임 에셋은 수정하지 않아요. HTML에 넣는 작은 그림만 검수용으로 줄여요.
"""
import base64
import io
import json
import os
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/docs"))
import paths

ART = ROOT / "ArtSource/JudgeVariants"
DOC = Path(paths.doc(paths.JUDGE_TABLE))


def main():
    manifest = json.loads((ART / "manifest.json").read_text(encoding="utf-8-sig"))
    judged = sorted((i for i in manifest["items"] if i["judged_in_current_flow"]),
                    key=lambda i: (i["chapter"], i["item"]))
    count = sum(len(i["variants"]) for i in judged)
    lines = ["# 판정 기준과 결과 그림 대응표", "",
             "> **상태** 최신 · **담당** 개발 · **마지막 갱신** 2026-11-22 · 자동", "",
             "[O/X를 눌러 그림 비교하기](판정_그림_비교.html)", "",
             "C-57 확정 기준. O는 충족, X는 미충족이며, 조합 순서는 F1 → F2 → F3예요. 실패 횟수는 그림 선택에 쓰지 않아요.", "",
             "| 챕터 | 보는 기준 | 색 도구 | 물건 수 | 정답 포함 조합 수 |",
             "|---|---|---|---:|---:|",
             "| 1 | 형태 1항목 | 검정 | 5 | 10 |",
             "| 2 | 형태 + 무늬 | 검정 | 3 | 12 |",
             "| 3 | 형태 + 무늬 + 색 | 빨강·노랑·파랑을 각각 사용 | 3 | 24 |",
             "| 4 | 형태 + 무늬 + 색 | 원색 2가지 혼합 | 3 | 24 |",
             "| 5 | 형태 + 무늬 + 색 | 원색 3가지 혼합 | 3 | 24 |", "",
             f"현재 그림 판정을 받는 17개 물건, 총 {count}가지예요. 새 오답 그림은 72장이고, 1챕터의 완전 오답 그림 5장은 기존 물건을 그대로 써요.", "",
             "색은 완성된 그림을 보고 하나의 항목으로 판정해요. 한 물건에 지정된 색이 여러 개면 전부 맞아야 색 O예요. 형태나 무늬가 틀려도 색은 독립적으로 봐요. 물감을 섞은 조작 이력은 판정하지 않아요.", "",
             "## 어디서 고치나요?", "",
             "| 바꿀 내용 | 수정할 곳 | 반영 방법 |", "|---|---|---|",
             "| 물건 설명·판정 기준·그림 파일 목록 | `Assets/Editor/MvpSceneBuilder.Data.cs`의 `CreateStageDataFiles` | Unity가 켜진 상태에서 `Temp/mvp_wrongart.flag` 파일 생성 |",
             "| 실제 물건 데이터 | `Assets/Data/*.asset` | 위 빌더에서 다시 만들므로 기준은 빌더부터 수정 |",
             "| 판정 O/X에 맞는 그림 선택 | `Assets/Scripts/StageData.cs`의 `SpriteFor` | F1부터 X를 1로 읽은 값 − 1번 오답 칸 선택 |",
             "| 결과 화면에 표시 | `Assets/Scripts/GameManager.Result.cs`의 `ShowResult` | 판정 배열을 그대로 `SpriteFor`에 전달 |",
             "| 그림 파일 | `Assets/Sprites/Chapter2` ~ `Chapter5` | `_Wrong_OOX.png`처럼 기준 순서에 맞게 저장 |",
             "| AI 공통 원칙 | `Assets/Scripts/OpenAIJudge.cs` | 물건 기준과 함께 프롬프트 내보내기 |",
             "| 서버가 쓰는 프롬프트 | `Server/judge/stages.json` | 로컬 내보내기 후 서버 배포 시 적용 |", "",
             "오답 배열 순서: 기준 1개는 X, 2개는 OX / XO / XX, 3개는 OOX / OXO / OXX / XOO / XOX / XXO / XXX예요. 전부 O이면 원본이에요.", ""]
    for item in judged:
        lines += [f"## {item['chapter']}챕터 · {item['target']} ({item['item']})", "",
                  f"데이터: `{item['stage_data']}`", "",
                  "| 항목 | 이름 | 충족 기준 |", "|---|---|---|"]
        for f in item["features"]:
            lines.append(f"| {f['id']} | {f['name']} | {f['criteria']} |")
        lines += ["", "| 조합 | 충족 | 미충족 | 가져올 그림 | 파일 |", "|---|---|---|---|---|"]
        for v in item["variants"]:
            rel = "../../" + v["file"]
            lines.append(f"| {v['mask']} | {' · '.join(v['present']) or '없음'} | "
                         f"{' · '.join(v['absent']) or '없음'} | <img src=\"{rel}\" width=\"90\" loading=\"lazy\" alt=\"{item['target']} {v['mask']}\"> | "
                         f"[{Path(v['file']).name}]({rel}) |")
        lines.append("")
    lines += ["## 5챕터 요청 물건", "",
              "짚신·부채·감투는 현재 흐름에서 고르면 바로 빼앗기는 요청 물건이에요. 그림 판정이 없으므로 오답 조합 그림은 만들지 않아요. 기준 데이터의 색은 각각 갈색 짚신 / 갈색 부챗살 / 검정 감투로, 모두 세 원색 혼합색이에요.", "",
              "이 문서는 `py Tools/art_temp/build_judge_variant_manifest.py` 다음 `py Tools/art_temp/build_judge_variant_review.py`로 다시 만들어요.", ""]
    DOC.write_text("\n".join(lines), encoding="utf-8")

    images = {}
    for item in judged:
        for v in item["variants"]:
            if v["file"] in images:
                continue
            with Image.open(ROOT / v["file"]) as source:
                preview = source.convert("RGBA")
                preview.thumbnail((136, 136), Image.Resampling.LANCZOS)
                stream = io.BytesIO()
                preview.save(stream, format="WEBP", quality=67, method=6)
                images[v["file"]] = "data:image/webp;base64," + base64.b64encode(stream.getvalue()).decode("ascii")
    payload = json.dumps({"items": judged, "images": images}, ensure_ascii=False).replace("</", "<\\/")
    template = '''<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>판정 기준과 결과 그림 대응표</title>
<style>
*{box-sizing:border-box}body{margin:0;padding:20px;font:14px/1.55 var(--font-sans,system-ui),sans-serif;background:var(--background,#fff);color:var(--foreground,#202020)}main{max-width:1020px;margin:auto}h1{font-size:23px;margin:0 0 6px}h2{font-size:16px;margin:0 0 8px}p{margin:6px 0 14px}.muted{color:var(--muted-foreground,#626262)}select,button{font:inherit;color:var(--foreground,#202020);background:var(--background,#fff);border:1px solid var(--border,#ddd);border-radius:7px;padding:7px 10px}select{width:100%;margin-bottom:12px}button{cursor:pointer}button:hover,tr[data-mask]:hover{background:var(--accent,#eee);color:var(--accent-foreground,#171717)}button[aria-pressed=true]{background:var(--primary,#222);color:var(--primary-foreground,#fff)}.checks{display:flex;flex-wrap:wrap;gap:8px;margin:0 0 14px}.checks button{flex:1;min-width:140px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:12px}.card{border:1px solid var(--border,#ddd);border-radius:10px;padding:12px;background:var(--card,#fff);color:var(--card-foreground,#222)}.picture{background:#f2eee6;border-radius:7px;text-align:center}.picture img{height:210px;width:100%;object-fit:contain;image-rendering:auto}.caption{font-weight:650;margin-top:7px}#selected-file{overflow-wrap:anywhere;font:11px/1.5 var(--font-mono,monospace);margin-top:5px}.table-scroll{overflow-x:auto;margin-top:12px}table{width:100%;border-collapse:collapse;font-size:13px}th,td{border-bottom:1px solid var(--border,#ddd);padding:8px;text-align:left;vertical-align:middle}th{background:var(--muted,#f7f7f7);color:var(--muted-foreground,#555)}td img{width:76px;height:76px;object-fit:contain;background:#f2eee6;border-radius:5px}tr[data-mask]{cursor:pointer}tr.selected td{box-shadow:inset 0 -2px var(--primary,#222)}code{font:11px var(--font-mono,monospace);overflow-wrap:anywhere}details{margin-top:14px}summary{cursor:pointer;font-weight:650}.criteria{padding:8px 0}.criteria p{margin:8px 0}.stats{display:flex;gap:9px;flex-wrap:wrap;margin-bottom:12px}.stats span{background:var(--secondary,#f1f1f1);color:var(--secondary-foreground,#333);padding:4px 9px;border-radius:20px}footer{margin-top:14px;font-size:12px;color:var(--muted-foreground,#666)}@media(max-width:540px){body{padding:12px}.picture img{height:160px}.card{padding:8px}.checks button{min-width:95px;font-size:12px;padding:6px}td,th{padding:6px}.path{max-width:150px}h1{font-size:20px}}
</style><main><p><a href="../index.html">← 문서 목록</a> · <a href="판정_그림_대응표.html">전체 대응표와 수정 위치</a></p><h1>판정 기준과 결과 그림 대응표</h1>
<p class="muted">물건을 고르고 O/X를 누르면 가져올 그림이 바뀝니다. 아래 표의 행도 선택할 수 있어요.</p>
<div class="stats"><span>17개 판정 물건</span><span>94가지 조합</span><span>새 오답 그림 72장</span></div>
<label for="item">챕터 · 물건</label><select id="item"></select><div id="checks" class="checks"></div>
<div class="pair"><section class="card"><h2>원본</h2><div class="picture"><img id="original" alt="원본 그림"></div><div id="original-label" class="caption"></div></section>
<section class="card"><h2>가져올 그림 <span id="mask"></span></h2><div class="picture"><img id="returned" alt="판정 조합에 대응하는 그림"></div><div id="result-label" class="caption"></div><div id="selected-file"></div></section></div>
<details><summary>이 물건의 정확한 판정 기준</summary><div id="criteria" class="criteria"></div></details>
<div class="table-scroll"><table><thead id="head"></thead><tbody id="rows"></tbody></table></div>
<footer>O = 충족 · X = 미충족 / 색 조건은 모두 합쳐 한 항목이며 최종 그림으로 판정합니다. 실패 횟수는 그림 선택에 쓰지 않습니다. 1챕터 완전 오답은 기존 물건을 유지합니다.<br>5챕터의 짚신·부채·감투는 현재 그림 판정이 없는 요청 물건이라 이 표에서 제외했습니다. 화면의 작은 그림은 검수용이며 게임에는 1024px 원본이 연결되어 있습니다.</footer>
</main><script>
const DATA=__DATA__;
// SITE_FULL_RES_IMAGES
const $=id=>document.getElementById(id);let active=null,mask='';
function txt(tag,value){const e=document.createElement(tag);e.textContent=value;return e}
function render(){
const v=active.variants.find(v=>v.mask===mask);$('checks').replaceChildren();
active.features.forEach((f,i)=>{const b=txt('button',f.id+' '+f.name+' : '+mask[i]);b.type='button';b.setAttribute('aria-pressed',mask[i]==='O');b.onclick=()=>{mask=mask.slice(0,i)+(mask[i]==='O'?'X':'O')+mask.slice(i+1);render()};$('checks').append(b)});
$('original').src=DATA.images[active.original];$('original-label').textContent=active.chapter+'챕터 · '+active.target;
$('returned').src=DATA.images[v.file];$('mask').textContent=mask;
$('result-label').textContent=v.absent.length?'미충족: '+v.absent.map(id=>active.features.find(f=>f.id===id).name).join(' · '):'모든 기준 충족 → 원본';$('selected-file').textContent=v.file;
$('criteria').replaceChildren();active.features.forEach(f=>{$('criteria').append(txt('p',f.id+' '+f.name+' — '+f.criteria))});
$('head').replaceChildren();const hr=document.createElement('tr');['조합',...active.features.map(f=>f.id), '결과 그림','파일'].forEach(x=>hr.append(txt('th',x)));$('head').append(hr);
$('rows').replaceChildren();active.variants.forEach(v=>{const row=document.createElement('tr');row.dataset.mask=v.mask;row.classList.toggle('selected',mask===v.mask);row.tabIndex=0;row.setAttribute('aria-label',v.mask+' 조합 선택');row.onclick=()=>{mask=v.mask;render()};row.onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();mask=v.mask;render()}};row.append(txt('td',v.mask));[...v.mask].forEach(x=>row.append(txt('td',x)));const cell=document.createElement('td');const im=document.createElement('img');im.src=DATA.images[v.file];im.alt=active.target+' '+v.mask;cell.append(im);row.append(cell);const path=txt('td','');path.className='path';path.append(txt('code',v.file.split('/').pop()));row.append(path);$('rows').append(row)});
}
for(let c=1;c<=5;c++){const group=document.createElement('optgroup');group.label=c+'챕터';DATA.items.filter(i=>i.chapter===c).forEach(i=>{const o=txt('option',i.target);o.value=i.item;group.append(o)});$('item').append(group)}
$('item').onchange=()=>{active=DATA.items.find(i=>i.item===$('item').value);mask='O'.repeat(active.features.length);render()};
$('item').value='Ch5_Bujeok';$('item').onchange();
</script></html>'''
    html = template.replace("__DATA__", payload)
    size = len(html.encode("utf-8"))
    assert size < 512 * 1024, size
    Path(paths.doc(paths.JUDGE_PREVIEW)).write_text(html, encoding="utf-8")
    scratch = os.environ.get("ORCA_CHAT_SCRATCH_DIR")
    if scratch:
        (Path(scratch) / "judge-variants-review-v2.html").write_text(html, encoding="utf-8")
    print(f"{DOC.relative_to(ROOT).as_posix()}: {count} rows")
    print(f"Docs/{paths.JUDGE_PREVIEW}: {size} bytes, {len(images)} inline images")


if __name__ == "__main__":
    main()

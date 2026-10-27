# art_pipeline — 시안에서 UI 부품 오리기

원본 : `ArtSource/원본/기획서/그림09·10` (작업 폴더에 `word/media/image9·10.png` 이름으로 두고 실행)

1. `cut_ui.py` → `cut_ui2.py` → `cut_ui3.py` → `cut_ui4.py` → `cut_ui5.py` : 시안 좌표(`boxes.txt`)로 오리고 글자 지우기 · 조각 합치기
2. `clean_edges.py <폴더>` : 가장자리 정리 (모양 마스크 · 반투명 한 줄)
3. `half_size.py <파일들>` : 절반 크기로 → `Assets/Sprites/TeamUI/`
4. `cut_objects.py` · `make_stage_art.py` : 단계별 물건 원본 (기획서 그림 1·3·5·7·19)
5. `make_placeholder.py` : 「그림 준비 중」 (`Assets/Sprites/UI/ArtComing.png`)

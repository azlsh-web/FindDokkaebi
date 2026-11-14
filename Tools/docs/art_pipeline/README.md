# art_pipeline

게임 그림을 만들 때 쓴 스크립트예요.

- `cut_title_parts.py` : 기획서 그림 11과 `BG_Start`의 차이로 시작 화면 로고 · 문구 · 버튼 · 톱니바퀴를 오려요.
- `cut_incense.py` : 향 줄기 · 불씨 · 불꽃 · 재 · 촛대 · 부드러운 동그라미 임시 그림을 그렸던 기록이에요. 향 · 촛대는 아트팀 그림으로 바뀌어서, 다시 실행하면 덮어써요 (`Common_SoftPuff`만 아직 써요).
- `make_gauge_placeholder.py` : 남은 시간 게이지 자리만 보이는 임시 직사각형 바(`HUD_CandleGauge`, 2000×200)를 그려요 (11/15). 아트팀 그림이 들어온 뒤에 돌리면 덮어써요.
- `fit_incense_art.py` : 아트팀 향 · 촛대 원본(`ArtSource/원본/받은그림/Incense`)의 여백을 칸에 맞춰 `Assets/Sprites/UI`에 넣어요.
- `make_clipboard_frame.py` : 클립보드를 세로로 늘리고 종이 면을 뚫어 `Draw_ClipboardFrame`을 만들어요.
- `make_game_feel_art.py` : 연출 개편에 쓰는 임시 그림과 아이콘을 만들고, 안 쓰게 된 그림을 보관함으로 옮겨요.
- `recut_full_res.py` : 시안 부품을 원본 크기로 다시 오리고 HUD 캡슐 · 로고 내용을 만든 기록이에요 (절반 크기 부품이 입력이라 다시 실행하지 않아요).

파일별 출처는 `Docs/개발/3_아트_구조.pdf`를 보세요.

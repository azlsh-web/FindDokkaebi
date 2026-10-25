# 찾아라! 도깨비 — MVP 구조 설명

> 1단계 **방망이** 하나로 기획서의 핵심 루프를 처음부터 끝까지 돌려보는 최소 버전입니다.
> 그림 판정은 AI 대신 **테스트 입력(체크박스)** 으로 대체했습니다.

---

## 1. 이번 MVP에 들어간 것 / 빠진 것

| 들어간 것 | 빠진 것 (다음 단계에서) |
|---|---|
| 도입 화면 (이야기 + 시작 버튼) | 2~5단계 (가방·돗자리·김밥·친구) |
| 관찰 3초 카운트다운 | 색상(삼원색), 혼색 팔레트 |
| 그리기 60초 (향 타이머), 시간 초과 시 자동 제출 | 선 굵기 3단계, 되돌리기 |
| 검은 펜 · 지우개 · 모두 지우기 | AI(LLM) 판정, 응답 대기 연출 |
| 빈 쪽지면 제출 버튼 비활성 | 캐릭터·배경 아트, 애니메이션 |
| 제출 시 간식 1개 사용 (시작 3개) | 이야기 종료 화면, 단계별 기록 |
| 판정: F1~F3 체크박스로 테스트 입력 | |
| 결과: 성공 / 엉뚱한 물건 / 아무것도 없음 + 특징별 [O]/[X] | |
| 다시 그리기 (원본 다시 안 보여줌) | |
| 원본 비교 (내 그림 vs 원본) | |

---

## 2. 실행 방법

1. `Assets/Scenes/Game.unity` 가 없으면 Unity 가 스크립트를 불러올 때 **자동으로** 만들어 줍니다.
   (직접 하려면 메뉴 **Tools → 찾아라도깨비 → MVP 씬 만들기**)
   → 씬이 만들어지고 자동으로 열립니다. "저장할까요?" 창이 뜨면 **Don't Save** 를 눌러도 됩니다.
2. **Game** 창 위쪽의 해상도 목록에서 세로 화면을 고릅니다. 예) `+` 를 눌러 **1080 x 1920** 추가
3. ▶ **Play** 버튼을 누릅니다.

> 도구를 다시 누르면 Game 씬을 **새로 덮어씁니다.** 씬을 손으로 고친 뒤에는 누르지 마세요.

### 자동 플레이 테스트
메뉴 **Tools → 찾아라도깨비 → 자동 플레이 테스트** 를 누르면 Unity 가 혼자 게임을 한 바퀴 돌려 봅니다.
(도입 → 관찰 → 그리기 → 부분 성공 → 다시 그리기 → 성공 → 비교 → 처음부터 → 간식 다 쓰기)

- 31가지를 확인하고 Console 에 `[SelfTest] 전체 PASS` 또는 `FAIL n개` 를 띄웁니다.
- 결과와 화면별 스크린샷은 `Temp/SelfTest/` 폴더에 저장됩니다. (Temp 는 Git 에 안 올라감)
- 코드를 고친 뒤 이걸 한 번 돌리면 "어디가 망가졌는지" 바로 알 수 있습니다.
- 확인하지 않는 것: 60초가 다 지나 자동 제출되는 경우 (테스트가 1분 넘게 걸려서 뺐어요)

---

## 3. 폴더 구조

```
FindDokkaebi/
├─ Assets/
│  ├─ Scripts/              ← 게임 코드 (실제 게임에 들어감)
│  │  ├─ GameManager.cs     ← 게임 흐름 담당 (사회자)
│  │  ├─ DrawingBoard.cs    ← 그림판 담당 (쪽지)
│  │  └─ StageData.cs       ← 단계 데이터의 "양식"
│  ├─ Editor/               ← 에디터 전용 도구 (게임 빌드에는 안 들어감)
│  │  ├─ MvpSceneBuilder.cs ← 씬 자동 조립 도구 (Tools 메뉴)
│  │  ├─ MvpAutoBuild.cs    ← Game 씬이 없으면 Unity 가 켜질 때 자동으로 한 번 조립
│  │  └─ MvpSelfTest.cs     ← 자동 플레이 테스트 (Tools 메뉴)
│  ├─ Data/
│  │  └─ Stage1_Bat.asset   ← 1단계 방망이 데이터 (StageData 양식으로 만든 파일)
│  ├─ Sprites/
│  │  ├─ Bat_Original.png   ← 원본 방망이 (돌기 있음) — 임시 그림
│  │  └─ Bat_Wrong.png      ← 엉뚱한 방망이 (돌기 없음) — 임시 그림
│  ├─ Scenes/
│  │  └─ Game.unity         ← 게임 씬 하나
│  └─ Fonts/                ← 한글 폰트 (NotoSansKR SDF)
└─ Docs/                    ← 이 문서와 아키텍처 PDF
```

**규칙 한 줄 요약:** 코드는 `Scripts`, 데이터는 `Data`, 그림은 `Sprites`, 화면은 `Scenes`.

---

## 4. 스크립트 3개 — 누가 무엇을 하나

### StageData.cs — "단계 데이터 양식"
- `ScriptableObject` = 씬에 올리지 않는 **데이터 파일**.
- 담는 것: 단계 이름, 대상 이름, 도입 이야기, 원본 그림, 엉뚱한 물건 그림, 특징 목록(F1~F3).
- 코드를 안 고치고 데이터 파일만 새로 만들면 다른 단계가 됩니다.
  (Project 창 우클릭 → **Create → 찾아라도깨비 → Stage Data**)

### DrawingBoard.cs — "쪽지(그림판)"
- 흰 `Texture2D`(픽셀 그림)를 만들어 `RawImage` 칸에 보여줍니다.
- 마우스를 누르거나 끌 때 그 위치 픽셀을 검정(펜) / 흰색(지우개)으로 바꿉니다.
- 바깥에 알려주는 것: `HasDrawn`(그린 게 있나?), `Texture`(그린 그림)
- 버튼용 함수: `UsePen()`, `UseEraser()`, `Clear()`

### GameManager.cs — "사회자"
- 지금 화면이 무엇인지 `State` 로 기억합니다: `Intro → Observe → Draw → Judge → Result → Compare`
- `ShowPanel()` 으로 **패널 하나만 켜고 나머지는 끕니다.** (씬 하나 + 패널 6개 구조)
- `Update()` 에서 관찰 3초 / 그리기 60초 타이머를 줄입니다.
- 간식 수를 관리하고, 판정 결과로 성공·실패를 계산합니다.

---

## 5. 데이터가 흐르는 길

```
[Stage1_Bat.asset]
   │  (Inspector 에서 GameManager.stage 칸에 연결)
   ▼
GameManager ──원본 그림──▶ 관찰 화면 Image
   │         ──특징 이름──▶ 판정 화면 체크박스 글자
   │
   │  ◀──HasDrawn── DrawingBoard (제출 버튼 켜기/끄기)
   │
   │  [제출] snacks - 1
   │
   │  ◀──체크박스 isOn ×3── 판정 화면   → bool[] present = {true, true, false}
   │
   ▼  present 에서 true 개수 세기
 결과 계산
   ├ 3개 모두  → 성공      → 원본 그림 표시
   ├ 1~2개     → 부분 성공 → 엉뚱한 그림 표시 + 다시 그리기 가능(간식 남으면)
   └ 0개       → 실패      → 그림 없음
   │
   ▼
비교 화면 ◀──Texture── DrawingBoard (내 그림 그대로 재사용)
          ◀──원본 그림── StageData
```

**핵심:** 판정 결과는 결국 `bool[] present` 하나입니다.
지금은 체크박스가 만들고, 나중에는 AI가 보낸 JSON(`status == "present"`)이 만들면 됩니다.
→ `GameManager.OnJudgeConfirmed()` 한 곳만 바꾸면 됩니다.

---

## 6. 씬(Hierarchy) 구조

```
Game (씬)
├─ Main Camera
├─ EventSystem              ← 클릭/드래그를 UI 로 전달 (Input System UI 모듈)
├─ GameManager              ← GameManager.cs 가 붙어 있음
└─ Canvas (1080x1920 기준)
   ├─ Background
   ├─ IntroPanel    : Title, Story, StartButton
   ├─ ObservePanel  : Guide, TargetImage, Countdown
   ├─ DrawPanel     : Guide, Board(DrawingBoard), Pen/Eraser/Clear, SubmitButton
   ├─ JudgePanel    : Waiting, TestGuide, FeatureToggle1~3, ConfirmButton
   ├─ ResultPanel   : ResultTitle, ReturnedItem, FeatureList, RetryButton, CompareButton
   ├─ ComparePanel  : MyDrawing, Original, FeatureHint, RestartButton
   └─ HUD           : SnackText, StageText, TimerText   ← 맨 아래 = 맨 앞에 그려짐
```

버튼을 클릭하고 Inspector 의 **Button → On Click()** 을 보면 어떤 함수가 연결돼 있는지 보입니다.
`GameManager` 오브젝트를 클릭하면 모든 연결 칸이 채워져 있는 것을 볼 수 있습니다.

---

## 7. 다음에 확장할 때

| 하고 싶은 것 | 어디를 고치나 |
|---|---|
| 2단계 가방 추가 | `Create → 찾아라도깨비 → Stage Data` 로 새 데이터 파일 + 그림 2장. GameManager 에 `StageData[] stages` 배열과 "다음 단계" 버튼 추가 |
| 색 펜 (빨강·노랑·파랑) | `DrawingBoard` 의 `Ink` 를 바꿀 수 있게 `SetColor(Color)` 함수 추가 + 색 버튼 |
| 선 굵기 3단계 | `DrawingBoard.penRadius` 를 바꾸는 `SetPenSize(int)` 함수 + 버튼 3개 |
| AI 판정 | `OnJudgeConfirmed()` 에서 `board.Texture.EncodeToPNG()` 로 그림을 보내고, 받은 JSON 으로 `present` 를 채우기 |
| 진짜 아트 | `Assets/Sprites` 의 PNG 두 장만 교체 (파일 이름 그대로) |

---

## 8. 커밋 기록 (브랜치: `feature/mvp-stage1`)

| 커밋 | 내용 |
|---|---|
| TextMeshPro 기본 리소스와 한글 폰트 추가 | 이미 작업해둔 폰트 설정 |
| 단계 데이터(StageData)와 임시 방망이 그림 추가 | `StageData.cs`, `Sprites/` |
| 그림판(DrawingBoard) | `DrawingBoard.cs` |
| 게임 흐름(GameManager) | `GameManager.cs` |
| 씬 자동 조립 도구 | `Editor/MvpSceneBuilder.cs` |
| MVP 게임 씬 생성 | `Scenes/Game.unity`, `Data/Stage1_Bat.asset` |
| 문서 | `Docs/` |

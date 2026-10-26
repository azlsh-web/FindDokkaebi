# 찾아라! 도깨비 — MVP 구조 설명

> 기획서의 5단계(방망이 → 가방 → 돗자리 → 김밥 → 친구)를 처음부터 이야기 종료까지 플레이할 수 있는 MVP입니다.
> 그림 판정은 **항상 자동**입니다 (기획서 6장).
> API 키가 있으면 **OpenAI(ChatGPT)**, 없으면 기획서의 대체 기술인 **HSV 색 분석 + 모양 계산(LocalJudge)** 으로 Unity 안에서 판정합니다.
> 도깨비·주인공·친구는 팀 그림을 쓰고, 단계별 물건은 임시 그림(코드로 그린 도형)이라 같은 이름의 PNG로 바꾸면 됩니다.

---

## 1. 기획서 → MVP 대응표

| 기획서 | MVP에 들어간 것 |
|---|---|
| 시작 화면 (제목, 소년, 도깨비, 소풍 준비하러 가기) | O |
| 단계 도입 (단계 번호, 대상, 이야기, 새 규칙, 앞에서 배운 규칙, 시간 안내) | O |
| 관찰 3초 → 원본 숨김 → 그리기 1분, 향이 다 타면 자동 제출 | O (향 막대가 줄어듦) |
| 검정 펜, 지우개, 가는·보통·굵은 선, 되돌리기, 모두 지우기 | O |
| 선택한 색·도구 테두리 표시, 빈 쪽지면 제출·되돌리기·모두 지우기 비활성 | O |
| 3단계부터 빨강·노랑·파랑 버튼 | O |
| 4단계 두 색 혼합 / 5단계 자유 혼합 팔레트 (재료 선택 → 미리보기·조합 문구 → 섞어서 칠하기 → 혼합색 칸) | O |
| 제출하면 쪽지와 간식 1개를 도깨비에게, 판정 대기 중 도깨비가 고민 | O (도깨비가 갸웃갸웃) |
| AI 판정 (멀티모달 LLM, JSON: inputStatus / features / status / evidence) | O (OpenAI Responses API) |
| 대체 기술 (색은 HSV 분석, 형태 비교) | O (LocalJudge : 키가 없을 때 자동 판정) |
| 판정 오류 시 간식 돌려주고 다시 제출 | O |
| 결과 : 도깨비와 찾아온 대상, 내 그림, 특징별 그림/빠뜨림, 소년 표정·대사, 남은 간식 | O |
| 빠뜨린 특징에 따라 다른 물건이 돌아옴 | O (특징마다 엉뚱한 그림 1장씩) |
| 다시 그리기 (원본 안 보여줌, 60초 다시) / 포기하고 비교 | O |
| 원본 비교 (내 그림 · 원본 · 특징 설명 · 다음 단계) | O |
| 이야기 종료 (소년·도깨비·친구, 단계별 기록과 남은 간식) | O |
| 판정 중 도깨비 고민 / 방망이를 휘둘러 물건을 꺼냄 | O (갸웃갸웃 · 휙 휘두르기 · 뿅) |
| 간식 개수 | 단계마다 3개 (기획서에 개수가 없어 정함) |
| 손그림 아트, 애니메이션, 효과음 | 캐릭터는 팀 그림, 물건은 임시 도형 그림, 효과음 없음 |

---

## 2. 실행 방법

1. `Assets/Scenes/Game.unity` 가 없으면 Unity 가 스크립트를 불러올 때 **자동으로** 만들어 줍니다.
   (직접 하려면 메뉴 **Tools → 찾아라도깨비 → MVP 씬 만들기**)
2. **Game** 창 위쪽 해상도 목록에서 세로 화면을 고릅니다. 예) **1080 x 1920** 또는 Galaxy S22 세로
3. ▶ **Play**

> 씬 조립 도구를 다시 누르면 Game 씬을 **새로 덮어씁니다.** (데이터 파일 `Assets/Data/*.asset` 은 이미 있으면 그대로 둡니다)

### AI 판정 켜기 (OpenAI)
1. 처음 씬을 만들 때 `FindDokkaebi/UserSettings/OpenAI.json` 파일이 생깁니다.
2. 메모장으로 열어서 `apiKey` 에 본인 OpenAI API 키를 넣고 저장합니다.
   ```json
   { "apiKey": "sk-여기에_키", "model": "gpt-6-luna" }
   ```
3. Play 하면 제출할 때 "도깨비가 쪽지를 들여다보는 중..." 이 나오고 AI 가 판정합니다.

- `UserSettings` 폴더는 **Git 에 올라가지 않습니다** (.gitignore). 키가 GitHub 에 새지 않아요.
- `model` 은 이미지 입력이 되는 OpenAI 모델이면 바꿔도 됩니다. 호출할 때마다 요금이 듭니다.
- 키를 비워 두면 LocalJudge(Unity 안 자동 판정)로 판정합니다. 체크박스 같은 수동 판정은 없습니다.
- 키를 게임 빌드에 넣어 배포하면 키가 새어 나갈 수 있어요. 실제 배포 때는 팀 서버가 키를 들고 OpenAI 를 대신 부르는 구조가 보통입니다.

### 자동 판정 (LocalJudge) 은 어떻게 보나
| 단계 | 확인 방법 |
|---|---|
| 방망이 | 그림을 긴 방향으로 16토막 → 끝쪽이 가늘게 이어지면 손잡이, 반대쪽이 1.8배 이상 굵으면 머리, 머리 둘레가 18% 이상 튀어나온 방향이 2곳 이상이면 돌기 |
| 가방 | 몸체 모서리 4곳이 채워져 있으면 네모, 몸체 위에 선이 있으면 손잡이, 몸체 안 닫힌 작은 칸 = 주머니, 그 안 가로줄 2줄 이상 = 줄무늬 |
| 돗자리 | 네모 외곽, 가운데에 길게 이어진 검은 세로·가로선(±2칸 삐뚤어도 인정), 네 칸의 가장 많은 색 |
| 김밥 | 검은 선이 감싼 둥근 안쪽, 그 안이 35% 이상 흰색, 빨강·노랑·초록 조각 |
| 친구 | 세로로 길고 아래에 두 다리, 상의(가운데 높이의 가장 넓은 색 덩어리)의 색과 어두운 가로줄, 상의 아래 색 덩어리의 색 |
- 색은 HSV(색상·채도·밝기)로 나눠요. 같은 색상이라도 어두우면 갈색, 밝으면 주황.
- 손으로 그린 선은 조금씩 끊겨 있어서 1칸(약 4픽셀) 두껍게 해서 닫힌 모양을 찾아요.
- 에디터에서 Play 하면 제출한 그림과 판정 결과가 `Temp/Submissions/` 에 저장돼요. 판정이 이상하면 그 파일을 보고 기준을 고치면 돼요.

### 자동 플레이 테스트
메뉴 **Tools → 찾아라도깨비 → 자동 플레이 테스트** 를 누르면 Unity 가 혼자 1~5단계를 끝까지 플레이합니다.
- 확인 내용 (179개) : 화면 전환, 간식 계산, 도구 보이기/숨기기, 혼색 결과, 빠뜨린 특징별 물건, AI 오류 시 간식 환불,
  향이 다 타면 자동 제출, 간식을 다 쓰면 포기, 이야기 종료 기록,
  **자동 판정** (원본 그림 = 전부 그림 / 엉뚱한 물건 그림 = 그 특징 빠뜨림 / 삐뚤고 성긴 손그림 샘플 5단계)
- 결과 : Console 에 `[SelfTest] 전체 PASS (n개)` / `FAIL`, 스크린샷은 `Temp/SelfTest/`
- AI 는 부르지 않습니다 (요금 X).
- Unity 창이 뒤에 있어도 돌아가도록 Project Settings → Player → Run In Background 를 켜 둡니다.

---

## 3. 폴더 구조

```
FindDokkaebi/
├─ Assets/
│  ├─ Scripts/               ← 게임 코드 (실제 게임에 들어감)
│  │  ├─ GameManager.cs      ← 게임 흐름 담당 (사회자)
│  │  ├─ StageData.cs        ← 단계 데이터의 "양식" (+ Feature, ColorMode)
│  │  ├─ DrawingBoard.cs     ← 쪽지(그림판) : 색, 굵기, 지우개, 되돌리기
│  │  ├─ ToolBar.cs          ← 쪽지 아래 도구 버튼 줄
│  │  ├─ ColorMixer.cs       ← 혼색 팔레트
│  │  ├─ OpenAIJudge.cs      ← AI 판정 (그림 → OpenAI → JSON → 그림/빠뜨림)
│  │  ├─ LocalJudge.cs       ← 자동 판정 (HSV 색 분석 + 모양 계산, 키가 없을 때)
│  │  └─ SimpleAnim.cs       ← 도깨비 갸웃갸웃 / 물건 뿅
│  ├─ Editor/                ← 에디터 전용 도구 (게임 빌드에는 안 들어감)
│  │  ├─ MvpSceneBuilder.cs  ← 씬 자동 조립 + 5단계 데이터 초기값
│  │  ├─ MvpAutoBuild.cs     ← Game 씬이 없으면 자동 조립
│  │  └─ MvpSelfTest.cs      ← 자동 플레이 테스트
│  ├─ Data/                  ← 단계 데이터 5개 (Stage1_Bat ~ Stage5_Friend)
│  ├─ Sprites/
│  │  ├─ Stage1_Bat/ …  Stage5_Friend/   Original.png + Wrong_F1~Fn.png (빠뜨린 특징별 물건)
│  │  ├─ Characters/         Boy_*, Dokkaebi, Friend_Art (팀 그림, 배경 제거)
│  │  ├─ UI/                 동그라미 · 둥근 버튼 · 도구 아이콘(지우개·되돌리기·휴지통·팔레트·굵기 점)
│  │  ├─ Props/              Snack, Incense
│  │  └─ Background/         Room (창호가 보이는 방 + 나무 책상)
│  ├─ Scenes/Game.unity      ← 게임 씬 하나
│  └─ Fonts/                 ← 한글 폰트 (NotoSansKR SDF)
├─ UserSettings/OpenAI.json  ← API 키 (Git 에 안 올라감)
└─ Docs/                     ← 이 문서와 아키텍처 PDF
```

**규칙 한 줄 요약:** 코드는 `Scripts`, 데이터는 `Data`, 그림은 `Sprites`, 화면은 `Scenes`.

---

## 4. 스크립트 8개 — 누가 무엇을 하나

| 스크립트 | 한 줄 설명 | 다른 스크립트와의 관계 |
|---|---|---|
| **GameManager** | 화면 흐름, 간식, 타이머, 결과 계산 | 모든 스크립트를 부름 (사회자) |
| **StageData** | 단계 하나의 데이터 (이야기, 규칙, 색 모드, 원본, 특징 목록) | 누구도 부르지 않음 (데이터) |
| **DrawingBoard** | 픽셀 그림판. 색·굵기·지우개·되돌리기 | ToolBar 가 부름, GameManager 가 그림을 가져감 |
| **ToolBar** | 도구 버튼 → DrawingBoard 설정, 선택 테두리 | ColorMixer 에게 팔레트 열기 부탁 |
| **ColorMixer** | 재료 고르기 → 섞은 색 만들기 | 결과를 ToolBar 에게 알려줌 (OnMixed) |
| **OpenAIJudge** | 그림+특징 → OpenAI → present/absent | 결과를 GameManager 에게 알려줌 (onDone/onError) |
| **LocalJudge** | 그림 → HSV 색 분석 + 모양 계산 → present/absent | GameManager 가 키 없을 때 부름 (Feature.check 로 계산 방법 선택) |
| **SimpleAnim** | 흔들기·튀어나오기 | 혼자 동작 |

### StageData 안의 Feature (특징 하나)
| 칸 | 예 (1단계 F3) |
|---|---|
| id | F3 |
| name | 머리 바깥의 돌기 2개 이상 |
| criteria | "머리 부분의 바깥 윤곽에서 튀어나온 뾰족한 돌기가 두 개 이상…" (AI 프롬프트에 그대로 들어감) |
| check | `Spikes` (자동 판정이 할 계산. 색을 보는 특징은 color · area 도 씀) |
| wrongSprite | `Sprites/Stage1_Bat/Wrong_F3.png` (돌기 없는 방망이) |

---

## 5. 데이터가 흐르는 길 (한 번 제출할 때)

```
[Stage1_Bat.asset] ──(GameManager.stages[0])──▶ GameManager
                                                  │
   관찰 : originalSprite 를 3초 보여줌 ◀───────────┤
   그리기 : toolBar.Setup(colorMode) → 버튼 보이기/숨기기
                                                  │
마우스 ─▶ DrawingBoard ─(Texture, HasDrawn)─────────▶ 제출 (간식 -1)
                                                  │
                    ┌───────────── 키 있음 ────────┴──────── 키 없음 ────────────┐
                    ▼                                                          ▼
   OpenAIJudge.Judge(stage, 그림)                       (도깨비 1.5초 고민) LocalJudge.Judge(stage, 그림)
     프롬프트 = targetDescription + features(name, criteria)            │
     + 그림 PNG(base64) → OpenAI                                       │
     ← {"features":[{"featureId":"F1","status":"present",...}]}        │
                    │  성공 → Verdict(present[], notes[])               │
                    │  실패 → onError → 간식 +1, 그림 그대로 다시 제출     │ HSV 색 분류 + 모양 계산
                    └──────────────────────┬───────────────────────────┘
                                           ▼
                               bool[] present 예) {O, O, X}
                                           │  O 개수 세기
                     ┌─────────────────────┼─────────────────────┐
                 전부 O                 일부 O                 전부 X
             originalSprite      처음 X 인 특징의 wrongSprite      아무것도 없음
              소년 웃음              소년 당황                   소년 당황
                                           │
                     다시 그리기 (간식 남으면) / 비교 → 다음 단계 → … → 이야기 종료
```

**핵심 :** 판정 결과는 결국 `bool[] present` 하나입니다. AI 든 자동 판정이든 이것만 만들면 나머지 화면은 똑같이 동작합니다.

---

## 6. 씬(Hierarchy) 구조

```
Game
├─ Main Camera
├─ EventSystem               (InputSystemUIInputModule : 마우스·터치를 UI 로)
├─ GameManager               ← GameManager.cs + OpenAIJudge.cs
└─ Canvas (1080 폭 기준)
   ├─ Background             (방 그림, 흐리게)
   ├─ TitlePanel             제목 · 소년 · 도깨비 · StartButton
   ├─ StageIntroPanel        단계 번호 · 대상 · 이야기 · RulesCard · ObserveButton
   ├─ ObservePanel           원본 그림 · 카운트다운
   ├─ DrawPanel              Board(DrawingBoard)
   │  ├─ ToolBar             ColorRow(색 동그라미 : 검정·빨강·노랑·파랑·섞은 색 + 팔레트 아이콘) / ToolRow(아이콘 : 지우개·굵기 점 3·되돌리기·휴지통)
   │  ├─ SubmitButton
   │  └─ MixerPanel          (ColorMixer) 재료 3개 · 미리보기 · 섞어서 칠하기 · 닫기
   ├─ JudgePanel             도깨비(갸웃갸웃) · 내 쪽지 · 간식 · BackToDrawButton(AI 오류 때만)
   ├─ ResultPanel            도깨비(휙 휘두르기) · 가져온 물건(뿅) · 내 그림 · 특징 목록 · 소년 + "!?" · 대사 · 버튼
   ├─ ComparePanel           내 그림 · 원본 · 특징 설명 · NextButton
   ├─ EndingPanel            소년 · 도깨비 · 친구 · 단계별 기록 · 처음으로
   └─ HUD                    간식 · 단계 이름 · 향(남은 초 + 막대)   ← 맨 아래 = 맨 앞에 그려짐
```

버튼을 클릭하고 Inspector 의 **Button → On Click()** 을 보면 어떤 함수가 연결돼 있는지 보입니다.

---

## 7. 다음에 바꾸거나 늘릴 때

| 하고 싶은 것 | 어디를 고치나 |
|---|---|
| 진짜 아트로 교체 | `Assets/Sprites/` 의 PNG 를 **같은 이름**으로 덮어쓰기 |
| 이야기·규칙 문장 수정 | `Assets/Data/StageN_*.asset` 클릭 → Inspector 에서 고치기 |
| 판정 기준(프롬프트) 수정 | 같은 데이터 파일의 `targetDescription`, `features[].criteria` |
| 간식 개수·시간 | `GameManager` 오브젝트 Inspector 의 `snacksPerStage`, `observeSeconds`, `drawSeconds` |
| 섞이는 색 바꾸기 | `ColorMixer.MixTable()` |
| 단계 추가 | 새 Stage Data + 그림 → `GameManager.stages` 배열에 추가 (도구는 `colorMode` 로 결정) |

---

## 8. 커밋 기록 (브랜치: `feature/mvp-stage1`)

기능 단위로 나눠 커밋했습니다. GitHub Desktop 의 History 탭에서 하나씩 눌러 보면 무엇이 바뀌었는지 볼 수 있습니다.

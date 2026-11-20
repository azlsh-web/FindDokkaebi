# 찾아라 도깨비

Unity로 만드는 휴대폰 세로 화면 게임이에요. 기준 브랜치는 `feature/game-feel`이고, 마감은 2026-11-25(수)이에요.

## 꼭 지킬 것

- 실제 AI 판정(Gemini `/judge`)은 사용자가 허락한 횟수만큼만 보내요.
- 기획이 바뀌는 일은 먼저 물어보고, 반영하면 `Docs/2_제작/결정_기록.md`에 기록해요.
- 대사(`Assets/Scripts/GameScript.cs`)를 바꾸면 대사 수정표 엑셀도 다시 만들어요.
- 커밋하기 전에 전체 자동 테스트를 돌리고 `python Tools/docs/update_docs.py --shots`를 실행해요.
- 씬은 `Assets/Editor/MvpSceneBuilder.cs`로 조립해요. 씬 파일을 손으로 고치지 않아요.
- 문서와 대화는 한국어, 쉬운 말로 써요.
- 문서 경로는 `Tools/docs/paths.py`에 모여 있어요. 문서를 옮기면 그 파일만 고쳐요. 문서 목록과 규칙은 `Docs/0_시작하기.md`, 도구 사용법은 `Docs/4_기술/문서_도구.md`예요.

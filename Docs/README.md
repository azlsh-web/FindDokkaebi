# Docs

| 문서 | 무엇 |
|---|---|
| **[1_게임_플로우.pdf](1_게임_플로우.pdf)** | 실제 게임 화면이 어떤 순서 · 조건으로 넘어가는지 (화면마다 캡처, 나오는 것 · 하는 것 · 다음) |
| **[2_MVP_아키텍처.pdf](2_MVP_아키텍처.pdf)** | 개발할 때 보는 구조 : 폴더 · 스크립트 역할 · 화면 흐름 · 판정 흐름 · 도구 · 배포 · 자주 하는 수정 |
| 그림 정리 | [`../ArtSource/README.md`](../ArtSource/README.md) — 기획서 그림 · 첨부 사진이 어느 화면에 통째로/일부만 들어가는지 |

- `src/` : 위 문서와 그림을 만드는 스크립트 (`make_docs.py` = 두 문서, `organize_art.py` = ArtSource, `art_pipeline/` = 시안에서 UI 오리기)
- `참고/` : 예전 문서 (판정 전수 검사 캡처, 부품별 아트 정리, 첨부 사진 대조, 예전 구조 설명)

문서 다시 만들기 (자동 플레이 테스트 캡처가 `Temp/SelfTest` 에 있을 때):

```
python Docs/src/make_docs.py . Temp/SelfTest
```

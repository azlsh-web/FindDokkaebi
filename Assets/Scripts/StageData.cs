using UnityEngine;

// ─────────────────────────────────────────────────────────────
// StageData : "한 단계(스테이지)에 필요한 데이터"만 담는 파일
// ─────────────────────────────────────────────────────────────
// - ScriptableObject 는 "씬에 올리지 않는 데이터 파일"이에요.
//   Project 창에서 우클릭 → Create → 찾아라도깨비 → Stage Data 로 만들 수 있어요.
// - 코드(GameManager)는 그대로 두고, 이 데이터 파일만 바꾸면 단계 내용이 바뀌어요.
// - 5단계 파일 : Assets/Data/Stage1_Bat ~ Stage5_Friend
//
// 기획서 3장 "단계별로 추가되는 요소" 표가 colorMode 와 features 로 들어가 있어요.
//   1 방망이 : 검정 형태          → BlackOnly
//   2 가방   : + 패턴             → BlackOnly (특징에 패턴이 추가)
//   3 돗자리 : + 빨강·노랑·파랑    → Primaries
//   4 김밥   : + 두 색 혼합        → MixTwo
//   5 친구   : + 자유 혼합         → MixFree

/// <summary>그리기 화면에서 쓸 수 있는 색 도구의 범위</summary>
public enum ColorMode
{
    BlackOnly,   // 검정 펜만
    Primaries,   // 검정 + 빨강·노랑·파랑
    MixTwo,      // + 두 가지 색 섞기
    MixFree,     // + 두 가지 이상(2~3가지) 색 섞기
}

/// <summary>
/// 자동 판정(LocalJudge)이 이 특징을 "어떤 계산으로" 확인할지.
/// AI(OpenAI) 판정은 criteria 글을 읽고, 자동 판정은 이 값을 보고 계산해요.
/// </summary>
public enum AutoCheck
{
    LongHandle,         // 가늘고 긴 부분이 있다 (방망이 손잡이)
    ThickHead,          // 한쪽 끝이 손잡이보다 굵다 (방망이 머리)
    Spikes,             // 머리 둘레에 뾰족한 돌기가 2개 이상
    RectOutline,        // 네모난 외곽 (가방 몸체, 돗자리)
    HandleOnTop,        // 몸체 위쪽에 손잡이
    InnerPocket,        // 몸체 안에 닫힌 작은 네모 (주머니)
    PocketStripes,      // 주머니 안 가로 줄무늬 2줄 이상
    CrossLines,         // 안쪽을 네 칸으로 나누는 가로선 + 세로선
    ColorAt,            // 정해진 칸(area)의 주된 색이 color
    RoundBlackRing,     // 둥근 검은 테두리 (김 테두리)
    WhiteInside,        // 테두리 안쪽이 대부분 흰색 (밥)
    ColorInside,        // 테두리 안쪽에 color 조각이 있다 (김밥 재료)
    PersonShape,        // 동그란 머리 + 아래로 이어진 몸 (사람)
    ShirtStripes,       // 상의 안 가로 줄무늬 2줄 이상
    ShirtColor,         // 상의의 주된 색이 color
    PantsColor,         // 하의의 주된 색이 color
}

/// <summary>자동 판정에서 쓰는 색 이름 (HSV 로 구분)</summary>
public enum InkColor { Black, Red, Yellow, Blue, Green, Orange, Purple, Brown }

/// <summary>ColorAt 이 볼 칸 (화면에서 보이는 방향 기준)</summary>
public enum Area { TopLeft, TopRight, BottomBoth }

/// <summary>판정할 특징 하나 (기획서의 F1, F2 …)</summary>
[System.Serializable]
public class Feature
{
    [Tooltip("특징 번호. AI 판정 JSON 의 featureId 와 같아야 해요. 예) F1")]
    public string id = "F1";

    [Tooltip("화면에 보일 짧은 이름. 예) 길쭉한 손잡이")]
    public string name;

    [Tooltip("AI 에게 알려줄 충족 기준 (기획서 부록 B 의 '충족 기준')")]
    [TextArea(2, 5)]
    public string criteria;

    [Header("자동 판정 (기획서 6장 대체 기술 : HSV 색 분석 + 모양 계산)")]
    public AutoCheck check;
    [Tooltip("색을 보는 판정(ColorAt, ColorInside, ShirtColor, PantsColor)에서 쓸 색")]
    public InkColor color;
    [Tooltip("ColorAt 에서 볼 칸")]
    public Area area;

    [Tooltip("이 특징을 빠뜨렸을 때 도깨비가 잘못 가져오는 물건")]
    public Sprite wrongSprite;
}

[CreateAssetMenu(fileName = "NewStage", menuName = "찾아라도깨비/Stage Data")]
public class StageData : ScriptableObject
{
    [Header("단계 정보")]
    public int stageNumber = 1;

    [Tooltip("그려야 할 대상 이름. 예) 방망이")]
    public string targetName = "방망이";

    [Tooltip("단계 도입 화면에 나오는 이야기")]
    [TextArea(3, 6)]
    public string introStory;

    [Tooltip("이번 단계에서 새로 배우는 규칙")]
    [TextArea(2, 4)]
    public string newRule;

    [Tooltip("앞 단계에서 배운 규칙 (1단계는 비워 둬요)")]
    [TextArea(2, 4)]
    public string learnedRules;

    [Header("도구")]
    public ColorMode colorMode = ColorMode.BlackOnly;

    [Header("그림")]
    [Tooltip("3초 동안 보여줄 원본(정답) 그림")]
    public Sprite originalSprite;

    [Header("판정")]
    [Tooltip("AI 에게 알려줄 목표 대상 설명 (기획서 부록 B 의 '목표 대상')")]
    [TextArea(3, 6)]
    public string targetDescription;

    [Tooltip("그림에 들어 있어야 할 특징들 (순서대로 F1, F2 …)")]
    public Feature[] features;

    /// <summary>화면에 보일 단계 제목. 예) 1단계 방망이</summary>
    public string Title => $"{stageNumber}단계 {targetName}";
}

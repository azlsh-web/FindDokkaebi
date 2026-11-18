using UnityEngine;

// ─────────────────────────────────────────────────────────────
// ChapterData : "챕터 하나"에 필요한 데이터
// ─────────────────────────────────────────────────────────────
// - 챕터 5개 × 물건. 한 챕터 안에서는 판정 기준이 같아요.
//   챕터가 오를수록 기준이 하나씩 늘어나요 (1 형태 → 2 +무늬 → 3 +원색 → 4 +두 색 섞기 → 5 +세 색 섞기).
// - 보통 챕터(1~4) : 물건 5개를 차례로 찾아요.
// - 저주 챕터(5) : 단계마다 [도깨비가 요청한 물건]과 [맞설 물건] 중 하나를 골라 그려요. 하트 3개.
// - 파일 : Assets/Data/Chapter1_Picnic, Chapter5_Curse (씬 만들기 때 MvpSceneBuilder 가 만들어요)

public enum ChapterRule
{
    Normal,     // 물건을 차례로 찾아요
    Curse,      // 5챕터 : 요청 물건 · 맞설 물건 중 골라요
}

[CreateAssetMenu(fileName = "NewChapter", menuName = "찾아라도깨비/Chapter Data")]
public class ChapterData : ScriptableObject
{
    [Header("챕터 정보")]
    public int number = 1;
    [Tooltip("챕터 선택 팝업에 나오는 이름. 예) 소풍 준비")]
    public string title = "소풍 준비";
    public ChapterRule rule = ChapterRule.Normal;
    [Tooltip("판정 기준 개수 (1 형태 ~ 5 세 색 섞기). 물건 데이터의 특징 수와 같아요")]
    public int criteriaLevel = 1;

    [Header("그림")]
    public Sprite photo;                // 챕터 선택 사진
    public Sprite playBackground;       // 대화 · 그리기 중 배경
    public Sprite endBackground;        // 끝 장면 배경
    public Sprite darkBackground;       // 저주 챕터 : 흑화 뒤 어두운 숲

    [Header("물건 (보통 챕터는 차례로, 저주 챕터는 단계마다 요청 · 맞설 물건 한 쌍)")]
    public StageData[] items;
    public StageData[] counterItems;    // 저주 챕터만 : items[i] 와 짝이 되는 맞설 물건

    public int ItemCount => items == null ? 0 : items.Length;
}

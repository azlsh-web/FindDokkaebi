using UnityEngine;

// ─────────────────────────────────────────────────────────────
// ChapterData : "챕터 하나"에 필요한 데이터
// ─────────────────────────────────────────────────────────────
// - 챕터 5개 × 물건. 한 챕터 안에서는 판정 기준이 같아요.
//   챕터가 오를수록 기준이 하나씩 늘어나요 (1 형태 → 2 +무늬 → 3 +원색 → 4 +두 색 섞기 → 5 +세 색 섞기).
// - 보통 챕터(1~4) : 물건을 차례로 찾아요 (1챕터 5개, 2~4챕터 3개).
// - 저주 챕터(5) : 단계마다 [도깨비가 요청한 물건]과 [맞설 물건] 중 하나를 골라 그려요. 하트 3개.
// - 파일 : Assets/Data/Chapter1_Picnic ~ Chapter5_Curse (씬 만들기 때 MvpSceneBuilder 가 만들어요)

/// <summary>챕터 첫 물건의 첫 그리기 때 도깨비가 보여 주는 안내 (챕터마다 하나씩 새로 배워요)</summary>
public enum ChapterGuide
{
    None,
    Pen,            // 1챕터 : 쪽지에 그려 보기 → 되돌리기 → 다 그렸어!
    Magnifier,      // 2챕터 : 돋보기 시범
    Mix,            // 3챕터 : 붓을 누르면 빨강 · 노랑 · 파랑 팔레트
}

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

    [Header("규칙")]
    [Tooltip("펜 도구. 1 · 2챕터는 검정 펜(BlackOnly), 3챕터부터 빨강 · 노랑 · 파랑 섞기(MixFree)")]
    public ColorMode penMode = ColorMode.MixFree;
    [Tooltip("돋보기를 쓰는 챕터인지 (1챕터는 없어요)")]
    public bool hasMagnifier = true;
    [Tooltip("물건마다 받는 사탕. 4챕터는 0개 (바구니가 비었어요) : 한 번만 그리고, 틀리면 바로 채점표")]
    public int candies = 3;
    public ChapterGuide guide = ChapterGuide.None;
    [Tooltip("시작 장면에서 도깨비가 나오기 전에 주인공 혼자 하는 대사 수")]
    public int aloneLines = 2;

    [Header("복선 (2~4챕터)")]
    [Tooltip("시작 첫 대사 때 창밖에 도깨비불이 깜빡 (2챕터)")]
    public bool windowFire;
    [Tooltip("도깨비 머리 불 색 : 0 보통, 1 주황 (3챕터), 2 붉음 (4챕터)")]
    public int dokkaebiMood;
    [Tooltip("끝 장면 독백 때 창 안에 빨간 눈 (4챕터)")]
    public bool endEyes;

    [Header("그림")]
    public Sprite photo;                // 챕터 선택 사진
    public Sprite playBackground;       // 대화 · 그리기 중 배경
    public Sprite endBackground;        // 끝 장면 배경
    public Sprite darkBackground;       // 저주 챕터 : 흑화 뒤 어두운 숲

    [Header("물건 (보통 챕터는 차례로, 저주 챕터는 단계마다 요청 · 맞설 물건 한 쌍)")]
    public StageData[] items;
    public StageData[] counterItems;    // 저주 챕터만 : items[i] 와 짝이 되는 맞설 물건

    public int ItemCount => items == null ? 0 : items.Length;
    /// <summary>다시 그릴 수 있는 챕터인지 (사탕이 있어야 다시 그려요)</summary>
    public bool CanRetry => candies > 0;
}

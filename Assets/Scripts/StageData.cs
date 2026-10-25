using UnityEngine;

// ─────────────────────────────────────────────────────────────
// StageData : "한 단계(스테이지)에 필요한 데이터"만 담는 파일
// ─────────────────────────────────────────────────────────────
// - ScriptableObject 는 "씬에 올리지 않는 데이터 파일"이에요.
//   Project 창에서 우클릭 → Create → 찾아라도깨비 → Stage Data 로 만들 수 있어요.
// - 코드(GameManager)는 그대로 두고, 이 데이터 파일만 바꾸면
//   방망이 → 가방 → 돗자리 … 처럼 다른 단계를 만들 수 있어요.
// - MVP 에서는 1단계(방망이) 파일 하나만 사용해요: Assets/Data/Stage1_Bat.asset
[CreateAssetMenu(fileName = "NewStage", menuName = "찾아라도깨비/Stage Data")]
public class StageData : ScriptableObject
{
    [Header("단계 정보")]
    [Tooltip("화면 위쪽에 보일 단계 이름. 예) 1단계 방망이")]
    public string stageTitle = "1단계 방망이";

    [Tooltip("그려야 할 대상 이름. 예) 방망이")]
    public string targetName = "방망이";

    [Tooltip("단계 도입 화면에 나오는 이야기")]
    [TextArea(3, 6)]
    public string introStory;

    [Header("그림")]
    [Tooltip("3초 동안 보여줄 원본(정답) 그림")]
    public Sprite originalSprite;

    [Tooltip("특징을 빠뜨렸을 때 도깨비가 잘못 가져오는 물건 그림")]
    public Sprite wrongSprite;

    [Header("판정할 특징 목록 (기획서 F1, F2, F3 …)")]
    [Tooltip("그림에 들어 있어야 할 특징들. 순서대로 F1, F2, F3 이에요.")]
    public string[] features =
    {
        "길쭉한 손잡이",
        "손잡이보다 굵은 머리",
        "머리 바깥의 돌기 2개 이상",
    };
}

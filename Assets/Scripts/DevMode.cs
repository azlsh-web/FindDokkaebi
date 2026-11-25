using UnityEngine;

// ─────────────────────────────────────────────────────────────
// DevMode : 개발자 모드 (설정 창의 "개발자 모드" 버튼으로 켜고 꺼요, 2026-11-25 C-66)
//   켜면 "다 그렸어!" 뒤에 AI 판정을 부르지 않고, 판정 기준마다 O · X 를 직접 골라
//   도깨비가 어떤 물건을 가져오는지 흐름을 확인해요 (DevJudgePanel). 켠 상태는 저장돼요.
// ─────────────────────────────────────────────────────────────
public static class DevMode
{
    const string Key = "dev_mode";

    /// <summary>자동 테스트에서만 : 저장값과 상관없이 손 판정을 켜거나 꺼요 (게임에서는 항상 null)</summary>
    public static bool? TestOverride;

    public static bool On
    {
        get => PlayerPrefs.GetInt(Key, 0) == 1;
        set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>판정을 손으로 고를지. 자동 테스트 · 녹화(OpenAIJudge.DisableAI)는 원래대로 자동 판정해요</summary>
    public static bool JudgeByHand => TestOverride ?? (On && !OpenAIJudge.DisableAI);
}

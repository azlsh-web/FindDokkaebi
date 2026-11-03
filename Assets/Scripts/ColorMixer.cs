using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// ColorMixer : 혼색 팔레트 (4단계 김밥부터 등장)
// ─────────────────────────────────────────────────────────────
// 기획서 4장 "혼색 조작"
//   색 섞기 버튼 → 팔레트가 열림 → 재료 색(빨강·노랑·파랑)을 고름
//   → 결과 색과 조합 문구가 보임 → [섞어서 칠하기] → 팔레트가 닫히고 만든 색이 선택됨
//   - 4단계(두 색 혼합) : 재료를 정확히 2가지
//   - 5단계(자유 혼합)  : 재료를 2~3가지
//   - 재료는 같은 양으로 섞어요.
//
// 왜 RGB 평균을 안 쓰나요?
//   물감은 빛과 달라서, 노랑(255,210,40)+파랑(40,90,220)을 평균 내면 칙칙한 회색이 나와요.
//   그래서 "노랑+파랑=초록" 같은 물감 섞기 결과를 표(MixTable)로 정해 두었어요.
public class ColorMixer : MonoBehaviour
{
    [Header("재료 버튼 (순서 고정 : 빨강, 노랑, 파랑)")]
    [SerializeField] GameObject[] materialFrames;   // 고른 재료에 보이는 테두리

    [Header("미리보기")]
    [SerializeField] Image previewSwatch;           // 결과 색 칸
    [SerializeField] TMP_Text recipeText;           // "노랑 + 파랑 = 초록"
    [SerializeField] TMP_Text guideText;            // "재료 색을 2가지 고르세요"
    [SerializeField] Button applyButton;            // 섞어서 칠하기

    /// <summary>색을 다 섞었을 때 알려줄 곳 (ToolBar 가 등록해요). (만든 색, 색 이름)</summary>
    public Action<Color, string> OnMixed;

    // 재료 3가지의 이름과 색
    static readonly string[] MaterialNames = { "빨강", "노랑", "파랑" };

    readonly bool[] picked = new bool[3];   // 재료를 골랐는지
    int maxPick = 2;                        // 최대 몇 가지까지 고를 수 있는지
    Color resultColor;
    string resultName;

    /// <summary>팔레트 열기. maxPick = 2(두 색 혼합) 또는 3(자유 혼합)</summary>
    public void Open(int maxPick)
    {
        this.maxPick = maxPick;
        for (int i = 0; i < picked.Length; i++) picked[i] = false;
        guideText.text = maxPick == 2 ? "재료 색을 2가지 고르세요" : "재료 색을 2~3가지 고르세요";
        gameObject.SetActive(true);
        Refresh();
    }

    /// <summary>[버튼] 닫기</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>[버튼] 재료 고르기/빼기. index : 0 빨강, 1 노랑, 2 파랑</summary>
    public void TogglePick(int index)
    {
        if (!picked[index] && CountPicked() >= maxPick) return; // 이미 최대 개수면 더 못 고름
        picked[index] = !picked[index];
        Refresh();
    }

    /// <summary>[버튼] 섞어서 칠하기</summary>
    public void Apply()
    {
        if (CountPicked() < 2) return;
        OnMixed?.Invoke(resultColor, resultName);
        Close();
    }

    // 고른 재료에 맞춰 테두리·미리보기·버튼 상태 다시 그리기
    void Refresh()
    {
        for (int i = 0; i < materialFrames.Length; i++) materialFrames[i].SetActive(picked[i]);

        bool ready = CountPicked() >= 2 && MixTable(picked[0], picked[1], picked[2], out resultColor, out resultName);
        applyButton.interactable = ready;
        previewSwatch.color = ready ? resultColor : new Color(0.85f, 0.85f, 0.85f);

        // 조합 문구 만들기 : "노랑 + 파랑 = 초록"
        string recipe = "";
        for (int i = 0; i < picked.Length; i++)
        {
            if (!picked[i]) continue;
            recipe += (recipe.Length > 0 ? " + " : "") + MaterialNames[i];
        }
        recipeText.text = ready ? $"{recipe} = {resultName}" : (recipe.Length > 0 ? recipe + " + ?" : "");
    }

    int CountPicked()
    {
        int n = 0;
        foreach (bool p in picked) if (p) n++;
        return n;
    }

    /// <summary>물감 섞기 표. 재료를 같은 양으로 섞었을 때의 결과</summary>
    public static bool MixTable(bool red, bool yellow, bool blue, out Color color, out string name)
    {
        if (red && yellow && blue) { color = new Color32(130, 80, 40, 255); name = "갈색"; return true; }
        if (red && yellow)         { color = new Color32(245, 140, 30, 255); name = "주황"; return true; }
        if (yellow && blue)        { color = new Color32(40, 160, 60, 255); name = "초록"; return true; }
        if (red && blue)           { color = new Color32(130, 50, 160, 255); name = "보라"; return true; }
        color = Color.clear; name = "";
        return false;
    }
}

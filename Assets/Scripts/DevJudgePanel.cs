using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>개발자 모드 판정 창 : 판정 기준마다 O · X 를 고르고 "결과 보기"를 누르면 그대로 판정해요 (C-66)</summary>
public class DevJudgePanel : MonoBehaviour
{
    [SerializeField] Image[] rows;          // 기준 한 줄씩 (최대 5개). 누르면 O ↔ X
    [SerializeField] TMP_Text[] rowTexts;
    [SerializeField] Sprite onSprite;       // O : 그렸다
    [SerializeField] Sprite offSprite;      // X : 빠뜨렸다
    [SerializeField] RectTransform confirm; // 결과 보기 버튼 : 줄 수에 맞춰 위로 당겨요

    const float RowTop = 150f, RowStep = 125f;     // 씬 조립(MvpSceneBuilder)의 줄 위치와 같아요

    StageData stage;
    bool[] present;
    Action<OpenAIJudge.Verdict> done;

    public void Show(StageData s, Action<OpenAIJudge.Verdict> onDone)
    {
        stage = s;
        done = onDone;
        present = new bool[s.features.Length];
        for (int i = 0; i < present.Length; i++) present[i] = true;     // 처음엔 다 O (성공)
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>[버튼] i 번째 기준 O ↔ X</summary>
    public void RowTap(int i)
    {
        if (present == null || i >= present.Length) return;
        present[i] = !present[i];
        Sfx.Play(Sfx.Tap);
        Refresh();
    }

    /// <summary>[버튼] 결과 보기 : 고른 대로 판정</summary>
    public void Confirm()
    {
        if (present == null) return;
        Sfx.Play(Sfx.Tap);
        var notes = new string[present.Length];
        for (int i = 0; i < notes.Length; i++) notes[i] = "개발자 모드에서 고름";
        var verdict = new OpenAIJudge.Verdict { present = (bool[])present.Clone(), notes = notes };
        Action<OpenAIJudge.Verdict> cb = done;
        present = null;
        done = null;
        Hide();
        cb?.Invoke(verdict);
    }

    void Refresh()
    {
        int n = present != null ? present.Length : 0;
        float bottom = RowTop + n * RowStep;
        var card = (RectTransform)transform;
        card.sizeDelta = new Vector2(card.sizeDelta.x, bottom + 150f);     // 기준 수만큼만 창을 키워요
        if (confirm != null) confirm.anchoredPosition = new Vector2(confirm.anchoredPosition.x, -(bottom + 25f));
        for (int i = 0; i < rows.Length; i++)
        {
            bool used = present != null && i < present.Length;
            rows[i].gameObject.SetActive(used);
            if (!used) continue;
            rows[i].sprite = present[i] ? onSprite : offSprite;
            rowTexts[i].text = (present[i] ? "O   " : "X   ") + stage.features[i].name;
        }
    }
}

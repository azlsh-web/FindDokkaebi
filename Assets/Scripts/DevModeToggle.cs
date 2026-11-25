using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 창의 "개발자 모드" 줄 : 누를 때마다 켜짐 ↔ 꺼짐 (로비 · 게임 씬 둘 다)</summary>
public class DevModeToggle : MonoBehaviour
{
    [SerializeField] TMP_Text stateText;
    [SerializeField] Image back;
    [SerializeField] Sprite onSprite;
    [SerializeField] Sprite offSprite;

    void OnEnable() => Refresh();

    /// <summary>[버튼] 개발자 모드 켜기 ↔ 끄기</summary>
    public void Toggle()
    {
        DevMode.On = !DevMode.On;
        Sfx.Play(Sfx.Tap);
        Refresh();
    }

    void Refresh()
    {
        if (stateText != null) stateText.text = DevMode.On ? "켜짐" : "꺼짐";
        if (back != null) back.sprite = DevMode.On ? onSprite : offSprite;
    }
}

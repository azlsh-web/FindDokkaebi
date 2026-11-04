using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────
// DialogueBox : 대사 상자 (모든 대사가 같은 크기의 상자에 나와요)
// ─────────────────────────────────────────────────────────────
// 글자는 한 글자씩 나오고, 터치하면 바로 다 보여요. 다 나오면 아래 화살표가 깜빡여요.
// 글자를 잘라 넣지 않고 "보이는 글자 수"만 늘려서, 나오는 도중에 줄이 바뀌지 않아요.
public class DialogueBox : MonoBehaviour
{
    [SerializeField] Image face;
    [SerializeField] TMP_Text text;
    [SerializeField] Image arrow;
    [SerializeField] float secondsPerChar = 0.032f;

    float shown;
    int total;

    /// <summary>대사를 보여 주기 시작</summary>
    public void Say(string line, Sprite faceSprite)
    {
        gameObject.SetActive(true);
        if (faceSprite != null) face.sprite = faceSprite;
        text.text = line;
        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();
        total = text.textInfo.characterCount;
        shown = 0f;
    }

    public bool IsTyping => shown < total;

    /// <summary>글자를 한 번에 다 보여 주기. 아직 나오는 중이었으면 true</summary>
    public bool FinishTyping()
    {
        if (!IsTyping) return false;
        shown = total;
        text.maxVisibleCharacters = total;
        return true;
    }

    void Update()
    {
        if (IsTyping)
        {
            int before = Mathf.FloorToInt(shown);
            shown = Mathf.Min(total, shown + Time.deltaTime / Mathf.Max(0.001f, secondsPerChar));
            int now = Mathf.FloorToInt(shown);
            text.maxVisibleCharacters = now;
            if (now != before && now % 2 == 0) Sfx.Play(Sfx.Talk);
        }
        if (arrow != null)
        {
            Color c = arrow.color;
            c.a = IsTyping ? 0f : Mathf.Lerp(0.2f, 1f, Mathf.PingPong(Time.time * 2f, 1f));
            arrow.color = c;
        }
    }
}

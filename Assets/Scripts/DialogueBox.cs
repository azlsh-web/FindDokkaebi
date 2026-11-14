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

    // 마지막 줄에 이보다 적은 글자만 남으면 ("그려 / 줘." 처럼) 앞 어절과 묶어서 같이 내려보내요
    public const int MinLastLine = 4;

    /// <summary>대사를 보여 주기 시작</summary>
    public void Say(string line, Sprite faceSprite)
    {
        gameObject.SetActive(true);
        if (faceSprite != null) face.sprite = faceSprite;
        text.maxVisibleCharacters = 0;
        text.text = KeepTailTogether(text, line);
        total = text.textInfo.characterCount;
        shown = 0f;
    }

    /// <summary>
    /// 마지막 줄이 짧은 조각 하나만 남지 않게, 끝의 어절 몇 개를 &lt;nobr&gt; 로 묶어 줄을 바꿔요.
    /// 상자 크기에 맞춰 실제로 줄을 나눠 보고 정하니까, 상자나 글꼴 크기가 바뀌어도 맞아요.
    /// </summary>
    public static string KeepTailTogether(TMP_Text box, string line)
    {
        string[] words = line.Split(' ');
        string result = line;
        for (int glue = 2; ; glue++)
        {
            box.text = result;
            box.ForceMeshUpdate(true);
            TMP_TextInfo info = box.textInfo;
            if (info.lineCount < 2 || info.lineInfo[info.lineCount - 1].visibleCharacterCount >= MinLastLine) return result;
            if (glue > 3 || glue >= words.Length) return result;     // 세 어절까지만 묶어요 (더 묶으면 앞줄이 너무 짧아져요)
            int head = words.Length - glue;
            result = string.Join(" ", words, 0, head) + " <nobr>" + string.Join(" ", words, head, glue) + "</nobr>";
        }
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

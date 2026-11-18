using UnityEngine;

// ─────────────────────────────────────────────────────────────
// Progress : 어디까지 했는지 저장 (PlayerPrefs)
// ─────────────────────────────────────────────────────────────
// 챕터 번호는 1부터예요 (1 ~ 5).
// 로비(챕터 선택)와 게임 씬이 같이 써요.
public static class Progress
{
    const string ClearedKey = "chapter_cleared_";
    const string BatFoundKey = "bat_found";
    const string BatKnownKey = "bat_known";

    /// <summary>이 챕터를 끝까지 깼는지</summary>
    public static bool IsCleared(int chapter) => PlayerPrefs.GetInt(ClearedKey + chapter, 0) == 1;

    /// <summary>이 챕터를 깼다고 저장</summary>
    public static void MarkCleared(int chapter)
    {
        PlayerPrefs.SetInt(ClearedKey + chapter, 1);
        PlayerPrefs.Save();
    }

    /// <summary>1챕터에서 방망이를 찾았는지. 값을 정하면 BatKnown 도 같이 true 가 돼요.</summary>
    public static bool BatFound
    {
        get => PlayerPrefs.GetInt(BatFoundKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(BatFoundKey, value ? 1 : 0);
            PlayerPrefs.SetInt(BatKnownKey, 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>1챕터를 끝까지 해서 방망이를 찾았는지 못 찾았는지 이미 정해졌는지 (BatFound 를 한 번이라도 정했으면 true)</summary>
    public static bool BatKnown => PlayerPrefs.GetInt(BatKnownKey, 0) == 1;

    /// <summary>들어갈 수 있는 챕터인지. 1 · 5 만 열려 있고, 2 · 3 · 4 는 아직 만들지 않아 잠겨 있어요.</summary>
    public static bool IsOpen(int chapter) => chapter == 1 || chapter == 5;

    /// <summary>저장한 진행을 모두 지워요 (테스트용)</summary>
    public static void ResetAll()
    {
        for (int i = 1; i <= 5; i++) PlayerPrefs.DeleteKey(ClearedKey + i);
        PlayerPrefs.DeleteKey(BatFoundKey);
        PlayerPrefs.DeleteKey(BatKnownKey);
        PlayerPrefs.Save();
    }
}

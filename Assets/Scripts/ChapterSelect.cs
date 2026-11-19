using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────
// ChapterSelect : 로비 씬과 게임 씬 사이에서 "어느 챕터를 할지" 전달
// ─────────────────────────────────────────────────────────────
// 로비에서 챕터를 고르면 PlayChapter 로 Game 씬을 열고,
// Game 씬은 시작할 때 ChapterSelect.Current 를 읽어요.
public static class ChapterSelect
{
    public const string LobbyScene = "Lobby";
    public const string GameScene = "Game";

    /// <summary>Game 씬에서 할 챕터 (1 ~ 5)</summary>
    public static int Current = 1;

    /// <summary>true 면 로비가 시작 화면을 건너뛰고 바로 챕터 선택을 보여줘요 (로비가 읽고 false 로 돌려놔요)</summary>
    public static bool OpenChaptersOnLobby;

    /// <summary>챕터를 정하고 Game 씬을 열어요</summary>
    public static void PlayChapter(int chapter)
    {
        Current = chapter;
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameScene);
    }

    /// <summary>로비의 챕터 선택 화면으로 돌아가요</summary>
    public static void BackToChapters()
    {
        OpenChaptersOnLobby = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(LobbyScene);
    }
}

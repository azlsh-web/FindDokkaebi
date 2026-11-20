using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameManager 의 5챕터 엔딩 부분이에요. 칸과 전체 흐름 설명은 GameManager.cs 에 있어요.
public partial class GameManager
{
    // ───────────── 5챕터 엔딩 ─────────────

    /// <summary>kind : 1 게임오버, 2 해피 엔딩, 3 배드 엔딩. 모두 깬 것으로 치고 챕터 선택으로 가요</summary>
    void ShowCurseEnd(int kind)
    {
        state = State.Ending;
        curseEnd = kind;
        curseEndTalking = false;
        gameOverPending = false;
        hud.SetActive(kind != 1);
        timerGroup.SetActive(false);
        scareOverlay.SetActive(false);
        bool happy = kind == 2;
        backgroundImage.sprite = happy ? forestBright : forestDark;
        curseEndSilhouette.SetActive(kind == 1);
        curseEndBoy.gameObject.SetActive(kind != 1);
        curseEndFriend.gameObject.SetActive(kind != 1);
        curseEndFriend.sprite = friendLost && !happy ? friendSpiritSprite : friendSprite;
        curseEndDokkaebi.sprite = happy ? (batLost ? dokkaebiNoBat : dokkaebiNormal) : DokkaebiSprite();
        curseEndBat.SetActive(happy && !batLost);       // 해피 엔딩 : 들고 있던 방망이만 두고 가요
        curseEndBox.gameObject.SetActive(false);
        Progress.MarkCleared(chapter.number);
        Sfx.Play(happy ? Sfx.Cheer : Sfx.Fail);
        ShowPanel(curseEndPanel);
        CurseEndTap();
    }

    /// <summary>[화면 터치] 엔딩 대사 → 챕터 선택</summary>
    public void CurseEndTap()
    {
        if (curseEnd == 0) return;
        if (!curseEndTalking)
        {
            curseEndTalking = true;
            string line = curseEnd == 1 ? GameScript.CurseGameOver : curseEnd == 2 ? (batLost ? GameScript.CurseHappyNoBat : GameScript.CurseHappy) : GameScript.CurseBad;
            curseEndBox.Say(line, curseEnd == 2 ? faceBoyHappy : faceDokkaebi);
            return;
        }
        if (curseEndBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        ChapterSelect.BackToChapters();
    }
}

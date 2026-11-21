using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameManager 의 5챕터 빼앗기기 · 결과 · 엔딩 부분이에요. 칸과 전체 흐름 설명은 GameManager.cs 에 있어요.
public partial class GameManager
{
    // ───────────── 5챕터 결과 ─────────────

    /// <summary>요청 물건을 고름 : 그리지 않고 도깨비가 그 물건을 가져가요. 주인공 하트 -1</summary>
    void TakeRequest()
    {
        state = State.Result;
        resultPhase = ResultPhase.Dokkaebi;
        curseTaken = true;
        outcome = 2;
        lastPresent = null;
        hud.SetActive(true);            // 위 줄 하트가 깎이는 게 보여요
        timerGroup.SetActive(false);
        LoseHeart();
        resultItemImage.sprite = Stage.originalSprite;
        resultItemImage.enabled = true;
        resultItemShadow.SetActive(true);
        resultSparkles.SetActive(false);
        choiceRow.SetActive(false);
        resultDokkaebi.sprite = DokkaebiSprite(swing: true);
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.Swing);
        Sfx.Play(Sfx.Fail);
        resultBox.Say(GameScript.CurseTakeLine(stageIndex), faceDokkaebiTease);
    }

    void LoseHeart()
    {
        hearts = Mathf.Max(0, hearts - 1);
        UpdateSnacks();
    }

    /// <summary>[화면 터치] 5챕터 결과 : 도깨비 대사 → (하트 0이면 친구 영혼) 주인공 대사 → 다음 단계 · 같은 단계 고르기</summary>
    void CurseResultTap()
    {
        bool won = outcome == 0 && !curseTaken;
        if (resultPhase == ResultPhase.Dokkaebi)
        {
            resultPhase = ResultPhase.Boy;
            resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
            if (hearts <= 0)
            {
                // 하트를 다 잃음 : 친구가 쓰러지고 영혼을 빼앗겨요. 다음 터치에 게임오버
                friendLost = true;
                gameOverPending = true;
                resultBox.Say(GameScript.CurseSoulTaken, faceDokkaebiTease);
                return;
            }
            string line = won ? GameScript.CurseBoyHit : curseTaken ? GameScript.CurseBoyTaken : GameScript.CurseBoyWrong;
            resultBox.Say(line, won ? faceBoyHappy : faceBoySad);
            return;
        }
        if (!won) { ShowChoose(); return; }                     // 같은 단계를 다시 골라요
        cleared[stageIndex] = true;
        if (stageIndex + 1 < stages.Length) StartStage(stageIndex + 1);
        else ShowCurseEnd(2);                                   // 3단계를 다 이김 : 해피 엔딩
    }

    // ───────────── 5챕터 엔딩 ─────────────

    /// <summary>kind : 1 게임오버, 2 해피 엔딩. 모두 깬 것으로 치고 챕터 선택으로 가요</summary>
    void ShowCurseEnd(int kind)
    {
        state = State.Ending;
        curseEnd = kind;
        curseEndTalking = false;
        gameOverPending = false;
        hud.SetActive(false);                           // 엔딩에는 위 줄이 없어요
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
            string line = curseEnd == 1 ? GameScript.CurseGameOver : batLost ? GameScript.CurseHappyNoBat : GameScript.CurseHappy;
            curseEndBox.Say(line, curseEnd == 2 ? faceBoyHappy : faceDokkaebi);
            return;
        }
        if (curseEndBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        ChapterSelect.BackToChapters();
    }
}

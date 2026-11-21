using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameManager 의 결과 · 채점표 · 이야기 종료 · 2~4챕터 끝 장면 부분이에요. 칸과 전체 흐름 설명은 GameManager.cs 에 있어요.
public partial class GameManager
{
    // ───────────── 결과 ─────────────

    /// <summary>결과 화면 : 맞힌 특징 수에 따라 도깨비가 가져오는 물건이 달라져요.</summary>
    void ShowResult(bool[] present, string[] notes)
    {
        state = State.Result;
        resultPhase = ResultPhase.Dokkaebi;
        lastPresent = present;

        int count = 0;
        foreach (bool ok in present) if (ok) count++;
        bool success = count == present.Length;
        cleared[stageIndex] = success;
        outcome = success ? 0 : count > 0 ? 1 : 2;
        if (!success) stageWrongs++;

        // 맞으면 진짜 물건, 틀리면 n번째로 틀린 만큼 엉뚱한 물건 (물건마다 오답 3장)
        Sprite item = success ? Stage.originalSprite : Stage.WrongSpriteFor(stageWrongs);
        resultItemImage.sprite = item;
        resultItemImage.enabled = item != null;
        itemsFound[stageIndex] = item;
        resultItemShadow.SetActive(true);
        resultSparkles.SetActive(success);
        choiceRow.SetActive(false);

        // 방망이는 화면에 하나만 보여요.
        //   1챕터 첫 물건 : 맞히면 방망이가 물건 자리에 놓여 있으니 도깨비는 빈손 (주인공이 말할 때 집어 들어요, ResultTap)
        //   그 뒤 : 방망이를 못 찾았으면(batLost) 빈손, 아니면 방망이를 휘둘러요
        resultDokkaebi.sprite = DokkaebiSprite(swing: true);
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.Swing);
        Sfx.Play(Sfx.Item);
        Sfx.Play(success ? Sfx.Success : Sfx.Fail);

        string line = RoomChapter ? GameScript.RoomResult(chapter.number, stageIndex, success)
                    : GameScript.DokkaebiResult(Mathf.Min(stageIndex, 4), outcome);
        if (IsCurse)
        {
            curseTaken = false;
            if (success) line = GameScript.CurseCounterHitLine(stageIndex);
            else
            {
                // 맞설 물건 실패 : 점프스케어 + 주인공 하트 -1
                LoseHeart();
                line = GameScript.CurseCounterWrong;
                if (scareRoutine != null) StopCoroutine(scareRoutine);
                scareRoutine = StartCoroutine(Scare());
            }
        }
        resultBox.Say(line, success ? faceDokkaebiProud : faceDokkaebiTease);
    }

    // 점프스케어 : 무서운 얼굴이 잠깐 화면을 덮어요 (그림 · 판정은 안 바뀌어요)
    IEnumerator Scare()
    {
        scareOverlay.SetActive(true);
        Sfx.Play(Sfx.Fail);
        yield return new WaitForSeconds(0.9f);
        scareOverlay.SetActive(false);
        scareRoutine = null;
    }

    /// <summary>[화면 터치] 도깨비 대사 → 주인공 대사 → 다시 · 포기 또는 채점표</summary>
    public void ResultTap()
    {
        if (state != State.Result || resultPhase == ResultPhase.Choose) return;
        if (resultBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);

        if (resultPhase == ResultPhase.Error)
        {
            BackToDraw();
        }
        else if (gameOverPending)
        {
            ShowCurseEnd(1);
        }
        else if (IsCurse)
        {
            CurseResultTap();
        }
        else if (resultPhase == ResultPhase.Dokkaebi)
        {
            bool wrong = outcome != 0;
            resultPhase = ResultPhase.Boy;
            if (BatItem && !wrong) batLost = false;
            resultDokkaebi.sprite = BatItem && !wrong ? (dark ? dokkaebiDarkBat : dokkaebiNormal) : DokkaebiSprite();
            // 1챕터 첫 물건을 맞혔으면 도깨비가 바닥의 방망이를 집어 들어요 : 바닥 방망이 · 그림자 · 반짝이를 치워요
            if (BatItem && !wrong)
            {
                resultItemImage.enabled = false;
                resultItemShadow.SetActive(false);
                resultSparkles.SetActive(false);
            }
            resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
            string line = wrong && candies <= 0 && chapter.CanRetry ? GameScript.BoyOutOfSnacks
                : RoomChapter ? GameScript.RoomBoyResult(chapter.number, stageIndex, !wrong)
                : GameScript.BoyResult(stageIndex, outcome);
            resultBox.Say(line, wrong ? faceBoySad : faceBoyHappy);     // 엉뚱한 물건 · 간식 없음 : 우는 얼굴
        }
        else if (resultPhase == ResultPhase.Boy && outcome != 0 && candies > 0 && stageWrongs == 2 && !freeGiven && chapter.hasMagnifier)
        {
            // 이 단계에서 두 번째로 틀렸어요 : 도깨비가 돋보기를 건네주고, 맨 위 돋보기로 날아가 반짝여요
            resultPhase = ResultPhase.Gift;
            freeGiven = true;
            freePeek = true;
            resultDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
            resultBox.Say(GameScript.MagnifierGift, faceDokkaebi);
            StopGift();
            giftRoutine = StartCoroutine(GiftFly());
        }
        else if (outcome != 0 && candies > 0)
        {
            // 대사 상자 자리에 다시 · 포기 버튼이 나란히 나와요
            resultPhase = ResultPhase.Choose;
            resultBox.gameObject.SetActive(false);
            choiceRow.SetActive(true);
        }
        else
        {
            ShowCompare();
        }
    }

    /// <summary>[버튼] 다시 : 이전 그림에 이어 그리기, 시간은 다시 60초</summary>
    public void Retry()
    {
        Sfx.Play(Sfx.Tap);
        savedDrawTime = drawSeconds;
        StartDraw(true);
    }

    /// <summary>[버튼 · 터치] 채점표 : 내 그림, 원본, 번호마다 동그라미 또는 빗금</summary>
    public void ShowCompare()
    {
        state = State.Compare;
        compareMyDrawing.texture = board.Texture;
        compareOriginal.sprite = Stage.originalSprite;

        for (int i = 0; i < scoreRows.Length; i++)
        {
            bool used = i < Stage.features.Length;
            scoreRows[i].SetActive(used);
            if (!used) continue;
            bool ok = lastPresent != null && i < lastPresent.Length && lastPresent[i];
            scoreTexts[i].text = GameScript.NoBreakParts(Stage.features[i].name);
            scoreCircles[i].SetActive(ok);
            scoreSlashes[i].SetActive(!ok);
        }

        freePeek = false;           // 무료 기회는 그 물건에서만
        StopGift();
        UpdateSnacks();
        if (BatItem) batLost = !cleared[0];
        jamOverlay.SetActive(false);
        Sfx.Play(Sfx.Mark);
        ShowPanel(comparePanel);
    }

    /// <summary>[화면 터치] 채점표 다음으로</summary>
    public void NextStage()
    {
        Sfx.Play(Sfx.Tap);
        if (stageIndex + 1 < stages.Length) StartStage(stageIndex + 1);
        else ShowEnding();
    }

    // ───────────── 이야기 종료 ─────────────

    /// <summary>소풍 장면 : 물건이 날아와 놓이고, 단계마다 남은 사탕이 사탕 통에 모여요.</summary>
    void ShowEnding()
    {
        if (RoomChapter) { ShowRoomEnd(); return; }
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = chapter.endBackground != null ? chapter.endBackground : endingBackground;
        endingAsking = false;
        endingBox.gameObject.SetActive(false);
        endingDokkaebi.sprite = batLost ? dokkaebiNoBat : dokkaebiNormal;
        for (int i = 0; i < endingItems.Length && i < stages.Length; i++)
            endingItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
        // 물건을 다 거치면 깬 것 (정답이 아니어도). 1챕터는 방망이를 찾았는지도 저장해요
        Progress.MarkCleared(chapter.number);
        if (chapter.number == 1) Progress.BatFound = !batLost;
        // 방망이는 화면에 하나만 : 찾았으면 도깨비 손에만 있고 돗자리 방망이 자리는 비워요.
        //   못 찾았으면 도깨비는 빈손이고 그 자리에 받은 엉뚱한 물건이 놓여요.
        endingItems[0].gameObject.SetActive(batLost);
        Sfx.Bgm(Sfx.BgmPicnic);
        Sfx.Play(Sfx.Cheer);
        ShowPanel(endingPanel);
    }

    /// <summary>[화면 터치] 도깨비가 다음에 또 온다고 인사하고, 대사를 누르면 챕터 선택으로 가요</summary>
    public void EndingTap()
    {
        if (state != State.Ending) return;
        if (RoomChapter) { RoomEndTap(); return; }
        if (!endingAsking)
        {
            endingAsking = true;
            endingBox.Say(GameScript.EndingBye, faceDokkaebi);
            return;
        }
        if (endingBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        ChapterSelect.BackToChapters();
    }

    // ───────────── 2~4챕터 끝 장면 ─────────────

    /// <summary>방 끝 장면 : 찾은 물건 3개가 방에 놓여요. 화면을 누르면 도깨비 인사 (4챕터는 도깨비가 떠나고 주인공 독백)</summary>
    void ShowRoomEnd()
    {
        state = State.Ending;
        hud.SetActive(false);
        backgroundImage.sprite = chapter.endBackground != null ? chapter.endBackground : PlayBackground;
        roomEndLine = 0;
        roomEndBox.gameObject.SetActive(false);
        roomEndEyes.SetActive(false);
        roomEndDokkaebi.gameObject.SetActive(true);
        roomEndDokkaebi.color = Color.white;
        roomEndDokkaebi.sprite = DokkaebiSprite();
        roomEndDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
        for (int i = 0; i < roomEndItems.Length; i++)
        {
            bool used = i < stages.Length;
            roomEndItems[i].gameObject.SetActive(used);
            if (used) roomEndItems[i].sprite = itemsFound[i] != null ? itemsFound[i] : stages[i].originalSprite;
        }
        Progress.MarkCleared(chapter.number);       // 물건을 다 거치면 깬 것 → 다음 챕터가 열려요
        Sfx.Bgm(Sfx.BgmRoom);
        Sfx.Play(Sfx.Cheer);
        ShowPanel(roomEndPanel);
    }

    /// <summary>[화면 터치] 방 끝 장면 : 대사를 하나씩, 다 보면 챕터 선택으로</summary>
    public void RoomEndTap()
    {
        if (state != State.Ending || !RoomChapter) return;
        if (roomEndBox.gameObject.activeSelf && roomEndBox.FinishTyping()) return;
        GameScript.Line[] lines = GameScript.RoomEndLines(chapter.number);
        if (roomEndLine >= lines.Length)
        {
            Sfx.Play(Sfx.Tap);
            ChapterSelect.BackToChapters();
            return;
        }
        if (roomEndLine > 0) Sfx.Play(Sfx.Tap);
        GameScript.Line line = lines[roomEndLine];
        // 주인공 독백 (4챕터) : 도깨비는 떠나고, 창 안에서 빨간 눈이 지켜봐요
        if (line.face != GameScript.Face.Dokkaebi && roomEndLine > 0)
        {
            roomEndDokkaebiAnim.Play(SimpleAnim.Kind.Vanish);
            roomEndEyes.SetActive(chapter.endEyes);
        }
        roomEndLine++;
        roomEndBox.Say(line.text, FaceOf(line.face));
    }
}

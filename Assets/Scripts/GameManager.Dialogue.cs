using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameManager 의 오프닝 · 대화 · 5챕터 고르기 · 떠올리기 부분이에요. 칸과 전체 흐름 설명은 GameManager.cs 에 있어요.
public partial class GameManager
{
    // ───────────── 오프닝 + 대화 ─────────────

    void StartStage(int index)
    {
        state = State.Dialogue;
        stageIndex = index;
        candies = chapter.candies;
        freePeek = false;
        freeGiven = false;
        stageWrongs = 0;
        snackIntroPending = true;
        lastPresent = null;
        gameOverPending = false;
        if (IsCurse) chosen = stages[index];        // 고르기 전까지는 요청 물건으로
        toolBar.Setup(chapter.penMode);             // 1 · 2챕터 검정 펜, 3챕터부터 빨강 · 노랑 · 파랑 섞기
        hud.SetActive(false);
        timerGroup.SetActive(false);
        backgroundImage.sprite = PlayBackground;
        UpdateSnacks();
        Sfx.Bgm(Sfx.BgmRoom);

        giveNote.SetActive(false);
        giveSnackOut.gameObject.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebi.GetComponent<SimpleAnim>().enabled = true;     // 대화에서는 도깨비가 걸어 들어와요
        dialogueDokkaebiImage.sprite = DokkaebiSprite();
        ShowFriend();
        ShowPanel(dialoguePanel);

        opening = AloneAt(0);                       // 챕터 맨 처음에는 주인공 혼자 말해요
        openingGroup.SetActive(opening && BatItem);
        dialogueDim.SetActive(!opening);
        boyTargetPos = (opening ? BoyAlonePos : BoyWithPos) + boyAnchorShift;
        boyTargetSize = opening ? BoyAloneSize : BoyWithSize;
        dialogueBoy.anchoredPosition = boyTargetPos;
        dialogueBoy.sizeDelta = boyTargetSize;
        dialogueLine = 0;
        SayIntroLine();
    }

    void SayIntroLine()
    {
        GameScript.Line line = IntroLines()[dialogueLine];
        dialogueBox.Say(line.text, FaceOf(line.face));

        // 5챕터 시작 장면 : 간식 이야기를 꺼내면 숲이 어두워지고 도깨비가 흑화해요
        if (IsCurse && stageIndex == 0 && !dark && dialogueLine >= GameScript.CurseDarkFrom)
        {
            dark = true;
            backgroundImage.sprite = PlayBackground;
            dialogueDokkaebiImage.sprite = DokkaebiSprite();
            UpdateSnacks();
            Sfx.Play(Sfx.Fail);
        }

        opening = AloneAt(dialogueLine);
        boyTargetPos = (opening ? BoyAlonePos : BoyWithPos) + boyAnchorShift;
        boyTargetSize = opening ? BoyAloneSize : BoyWithSize;
        openingGroup.SetActive(opening && BatItem);
        windowFire.SetActive(chapter.windowFire && stageIndex == 0 && dialogueLine == 0);    // 2챕터 복선 : 창밖 도깨비불
        if (opening)
        {
            dialogueDim.SetActive(false);
            dialogueDokkaebi.SetActive(false);
            if (BatItem) ShowOpeningBeat(dialogueLine);
        }
        else
        {
            dialogueDim.SetActive(true);
            dialogueDokkaebi.SetActive(true);
        }

        bool dokkaebiTalks = line.face == GameScript.Face.Dokkaebi;
        bool friendTalks = line.face == GameScript.Face.Friend;
        dialogueBoyAnim.Play(line.face == GameScript.Face.BoySurprised ? SimpleAnim.Kind.Shake
                             : dokkaebiTalks || friendTalks ? SimpleAnim.Kind.None : SimpleAnim.Kind.Gentle);
        if (dialogueDokkaebi.activeSelf)
            dialogueDokkaebiAnim.Play(dokkaebiTalks ? SimpleAnim.Kind.Gentle : SimpleAnim.Kind.None);
    }

    const float OpeningItemAlpha = 0.28f;

    // 오프닝 그림 : 가방 → 돗자리 · 김밥이 사라지고, 쪽지와 간식을 떠올린 뒤 도깨비를 불러요
    void ShowOpeningBeat(int line)
    {
        openingItems[0].gameObject.SetActive(line == 0);
        openingItems[1].gameObject.SetActive(line <= 1);
        openingItems[2].gameObject.SetActive(line <= 1);
        openingAsk.SetActive(line == 1);
        openingIdea.SetActive(line == 2);
        openingCall.SetActive(line == 3);
        if (line == 0)
        {
            // 방에 진짜 놓인 물건이 아니라 "있어야 할 자리"로 보이게 흐리게 그려요
            for (int i = 0; i < openingItems.Length; i++)
            {
                openingItems[i].GetComponent<Image>().sprite = stages[i + 1].originalSprite;
                openingItems[i].SetBaseAlpha(OpeningItemAlpha);    // Play 가 투명도를 되돌리므로 SimpleAnim 에 알려 줘요
            }
            PlayVanish(0, 1.3f);
            openingItems[1].Play(SimpleAnim.Kind.None);
            openingItems[2].Play(SimpleAnim.Kind.None);
        }
        else if (line == 1)
        {
            PlayVanish(1, 0.4f);
            PlayVanish(2, 1.1f);
        }
    }

    void PlayVanish(int item, float delay)
    {
        openingItems[item].delay = delay;
        openingItems[item].popTime = 0.5f;
        openingItems[item].Play(SimpleAnim.Kind.Vanish);
    }

    /// <summary>이 대사에서 주인공 혼자 있는지 (챕터 첫 물건의 앞쪽 대사, 도깨비가 나오기 전)</summary>
    bool AloneAt(int line) =>
        BatItem ? line < GameScript.OpeningAloneLines : RoomChapter && stageIndex == 0 && line < chapter.aloneLines;

    // 시작 대화 : 보통 챕터는 물건마다 (2~4챕터 첫 물건은 챕터 시작 장면 +), 5챕터는 (첫 단계만 시작 장면 +) 단계 대화
    GameScript.Line[] IntroLines()
    {
        if (RoomChapter) return GameScript.RoomIntroLines(chapter.number, stageIndex);
        if (!IsCurse) return GameScript.IntroLines(stageIndex, batLost);
        GameScript.Line[] step = GameScript.CurseStepLines(stageIndex);
        if (stageIndex != 0) return step;
        var all = new GameScript.Line[GameScript.CurseIntro.Length + step.Length];
        GameScript.CurseIntro.CopyTo(all, 0);
        step.CopyTo(all, GameScript.CurseIntro.Length);
        return all;
    }

    void NextIntroLine()
    {
        GameScript.Line[] lines = IntroLines();
        if (dialogueLine < lines.Length - 1)
        {
            dialogueLine++;
            SayIntroLine();
        }
        else if (IsCurse)
        {
            ShowChoose();
        }
        else
        {
            StartObserve();
        }
    }

    // 5챕터 친구 : 화면에 세워 둬요. 하트를 다 잃으면 영혼을 빼앗겨 흐려져요
    void ShowFriend()
    {
        dialogueFriend.gameObject.SetActive(IsCurse);
        dialogueFriend.sprite = friendLost ? friendSpiritSprite : friendSprite;
    }

    // ───────────── 5챕터 고르기 ─────────────

    void ShowChoose()
    {
        state = State.Dialogue;
        hud.SetActive(false);
        timerGroup.SetActive(false);
        jamOverlay.SetActive(false);
        magnifierPeek.Stop();
        backgroundImage.sprite = PlayBackground;
        leftIsCounter = Random.value < 0.5f;
        chooseImages[0].sprite = (leftIsCounter ? chapter.counterItems[stageIndex] : stages[stageIndex]).originalSprite;
        chooseImages[1].sprite = (leftIsCounter ? stages[stageIndex] : chapter.counterItems[stageIndex]).originalSprite;
        ShowPanel(choosePanel);
    }

    /// <summary>[버튼] 고르기 : 0 왼쪽, 1 오른쪽 그림.
    /// 맞설 물건은 떠올리고 그려요. 요청 물건은 그리지 않고 바로 도깨비가 가져가며 하트 -1 (TakeRequest)</summary>
    public void ChooseItem(int side)
    {
        if (!IsCurse || !choosePanel.activeSelf) return;
        Sfx.Play(Sfx.Tap);
        chosenCounter = (side == 0) == leftIsCounter;
        chosen = chosenCounter ? chapter.counterItems[stageIndex] : stages[stageIndex];
        if (chosenCounter) StartObserve();
        else TakeRequest();
    }

    /// <summary>[화면 터치] 대사 넘기기 → 떠올리기. 건네기 중이면 건네기를 이어 가요</summary>
    public void DialogueTap()
    {
        if (state == State.Give) { GiveTap(); return; }
        if (state != State.Dialogue) return;
        if (dialogueBox.FinishTyping()) return;
        Sfx.Play(Sfx.Tap);
        NextIntroLine();
    }

    /// <summary>3초 떠올리기 : 말풍선 속 원본과 숫자. 1단계는 도깨비, 그 뒤는 주인공이 떠올려요</summary>
    public void StartObserve()
    {
        state = State.Observe;
        timeLeft = observeSeconds;
        lastCount = 0;
        observeImage.sprite = Stage.originalSprite;
        observeCount.text = "3";
        bool dokkaebiRecalls = BatItem;
        observeBoy.SetActive(!dokkaebiRecalls);
        observeDokkaebi.SetActive(dokkaebiRecalls);
        Vector2 p = observeCountEdge.anchoredPosition;
        p.x = dokkaebiRecalls ? ObserveCountLeftX : ObserveCountRightX;
        observeCountEdge.anchoredPosition = p;
        ShowPanel(observePanel);
    }
}

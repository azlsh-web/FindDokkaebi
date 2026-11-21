using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameManager 의 그리기 · 돋보기 · 건네기 · 판정 대기 부분이에요. 칸과 전체 흐름 설명은 GameManager.cs 에 있어요.
public partial class GameManager
{
    // ───────────── 그리기 ─────────────

    /// <summary>그리기 시작. keepDrawing = true 면 그리던 그림을 그대로</summary>
    void StartDraw(bool keepDrawing)
    {
        state = State.Draw;
        if (keepDrawing)
        {
            timeLeft = savedDrawTime;
        }
        else
        {
            timeLeft = drawSeconds;
            board.ResetBoard();
            paperView.ResetView();      // 새 쪽지는 책상 위에 똑바로
        }
        // 게이지를 바로 맞춰 둬요 (시범 동안은 게이지가 멈춰 있어서, 안 맞추면 불꽃이 왼쪽 끝에 남아 있어요)
        incense.Show(timeLeft / drawSeconds, timeLeft);
        // 쪽지 글은 주인공이 적어 둔 것처럼 한 글자씩 나타나요. 다시 그릴 때는 이미 적혀 있어요.
        drawTitle.text = NoteText();
        if (noteRoutine != null) StopCoroutine(noteRoutine);
        drawTitle.maxVisibleCharacters = keepDrawing ? int.MaxValue : 0;
        hud.SetActive(true);
        timerGroup.SetActive(true);
        Sfx.Bgm(Sfx.BgmDraw);
        ShowPanel(drawPanel);
        magnifierPeek.Stop();
        StopGift();
        toolBar.ClosePanel();

        // 챕터 첫 그리기 : 도깨비가 안내해요 (1챕터 펜, 2챕터 돋보기, 3챕터 색)
        peekGuideStep = 0;
        toolGuideStep = 0;
        peekGuide.SetActive(false);
        guideHighlight.gameObject.SetActive(false);
        ShowMagnifierHighlight(false);
        magnifierButton.transform.localScale = Vector3.one;
        bool firstDraw = stageIndex == 0 && !keepDrawing && !guideShown;
        if (firstDraw && (chapter.guide == ChapterGuide.Pen || chapter.guide == ChapterGuide.Mix))
        {
            guideShown = true;
            StartToolGuide();
        }
        else if (chapter.guide == ChapterGuide.Magnifier && stageIndex == 0 && !peekShown && !keepDrawing)
        {
            peekShown = true;
            peekGuideStep = 1;
            peekGuide.SetActive(true);
            peekGuideBox.Say(GameScript.MagnifierShow, faceDokkaebi);
        }

        // 5챕터 맞설 물건 : 방해 연출 (쪽지 위에 덮기만)
        jamOverlay.SetActive(IsCurse && chosenCounter);

        UpdateSnacks();
        if (!keepDrawing) noteRoutine = StartCoroutine(WriteNote());

        bool snackIn = snackIntroPending && chapter.candies > 0;     // 4챕터는 사탕이 없어서 들어오지 않아요
        snackIntroPending = false;
        if (snackIn)
        {
            foreach (SimpleAnim a in snackAnims) a.Play();     // 사탕은 오른쪽 끝에서 왼쪽으로
            Sfx.Play(Sfx.SnackIn);
        }
    }

    /// <summary>[화면 터치] 돋보기 시범 중 대사 넘기기. 눌러 보라는 대사는 돋보기를 직접 눌러야 넘어가요</summary>
    public void PeekGuideTap()
    {
        if (state == State.Draw && toolGuideStep != 0) { ToolGuideTap(); return; }
        if (state != State.Draw || peekGuideStep == 0) return;
        if (peekGuideBox.FinishTyping()) return;
        if (peekGuideStep == 1)
        {
            // 눌러 보라는 대사를 넘기면 대사는 사라지고 맨 위 돋보기가 빛나요. 이제 돋보기를 직접 눌러야 해요
            Sfx.Play(Sfx.Tap);
            peekGuideStep = 2;
            peekGuideBox.gameObject.SetActive(false);
            ShowMagnifierHighlight(true);
        }
        else if (peekGuideStep == 3)
        {
            // 보는 법을 들었으면 둘러보기 시작 (시범이라 공짜)
            peekGuideStep = 4;
            peekGuide.SetActive(false);
            Sfx.Play(Sfx.Magnifier);
            magnifierPeek.Begin(Stage.originalSprite, peekSeconds);
        }
        else if (peekGuideStep == 5)
        {
            Sfx.Play(Sfx.Tap);
            peekGuideStep = 0;
            peekGuide.SetActive(false);
        }
    }

    // 시범으로 둘러본 뒤 : 돋보기는 게임 전체에서 한 번뿐이라고 알려 줘요
    void SayPeekRule()
    {
        peekGuideStep = 5;
        peekGuide.SetActive(true);
        peekGuideBox.Say(GameScript.MagnifierRule, faceDokkaebi);
    }

    // 도깨비불 셋이 둘레를 시계 방향으로 돌아요 (돋보기 · 붓). 가로 rx, 세로 ry 인 원을 돌고, 불꽃은 늘 위를 보며 일렁여요.
    // 크기는 화면 끝에서도 불이 다 보이게 정했어요 (배치 검사가 화면 밖을 확인해요)
    void SpinFires(RectTransform[] fires, float rx, float ry)
    {
        for (int i = 0; i < fires.Length; i++)
        {
            float a = -Time.time * 2.2f + i * Mathf.PI * 2f / fires.Length;
            fires[i].anchoredPosition = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
            float flicker = 1f + 0.08f * Mathf.Sin(Time.time * 9f + i * 2f);
            fires[i].localScale = new Vector3(1f / flicker, flicker, 1f);
        }
    }

    void ShowMagnifierHighlight(bool on)
    {
        if (on) SpinFires(magnifierFires, 80f, 45f);
        magnifierHighlight.SetActive(on);
    }

    IEnumerator WriteNote()
    {
        int total = drawTitle.text.Length;
        for (int i = 1; i <= total; i++)
        {
            drawTitle.maxVisibleCharacters = i;
            yield return new WaitForSeconds(0.07f);
        }
        drawTitle.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>[버튼] 맨 위 돋보기 : 그리는 중에 렌즈 안의 원본을 5초 둘러봐요 (향은 계속 타요)
    /// 시범이면 공짜, 아니면 이번 단계 무료 기회 → 게임 전체 1번 순서로 써요.</summary>
    public void MagnifierTap()
    {
        if (state != State.Draw || magnifierPeek.Active) return;
        if (peekGuideStep == 1 || peekGuideStep == 2)
        {
            // 시범 : 눌렀으면 바로 열지 않고, 몇 초 동안 어떻게 보는지 먼저 알려 줘요
            Sfx.Play(Sfx.Tap);
            peekGuideStep = 3;
            magnifierButton.transform.localScale = Vector3.one;
            ShowMagnifierHighlight(false);
            peekGuideBox.Say(GameScript.MagnifierHow, faceDokkaebi);
            return;
        }
        if (peekGuideStep != 0) return;
        if (freePeek) freePeek = false;
        else if (magnifiers > 0) magnifiers--;
        else return;
        Sfx.Play(Sfx.Magnifier);
        UpdateSnacks();
        magnifierPeek.Begin(Stage.originalSprite, peekSeconds);
    }

    /// <summary>[버튼] 다 그렸어 (또는 시간 초과로 자동 제출) : 쪽지와 간식을 건네러 가요</summary>
    public void Submit()
    {
        if (state != State.Draw || peekGuideStep != 0 || toolGuideStep != 0) return;
        magnifierPeek.Stop();
        jamOverlay.SetActive(false);
        toolBar.ClosePanel();

        state = State.Give;
        savedDrawTime = Mathf.Max(timeLeft, 10f);
        timerGroup.SetActive(false);
        Sfx.Play(Sfx.Submit);

        giveStep = 0;
        giveSnackOut.gameObject.SetActive(false);
        giveNoteText.text = NoteText();
        giveNoteDrawing.texture = board.Texture;
        openingGroup.SetActive(false);
        dialogueBoy.gameObject.SetActive(true);
        dialogueDokkaebi.SetActive(false);
        dialogueDokkaebi.GetComponent<SimpleAnim>().enabled = false;    // 건넬 때는 이미 곁에 있어요
        dialogueDokkaebi.SetActive(true);
        dialogueDokkaebiImage.sprite = DokkaebiSprite();
        ShowFriend();
        boyTargetPos = BoyWithPos + boyAnchorShift;
        boyTargetSize = BoyWithSize;
        dialogueBoy.anchoredPosition = boyTargetPos;
        dialogueBoy.sizeDelta = boyTargetSize;
        ShowPanel(dialoguePanel);
        giveNote.SetActive(true);
        giveNoteAnim.Play(SimpleAnim.Kind.Float);
        dialogueBoyAnim.Play(SimpleAnim.Kind.Gentle);
        dialogueDokkaebiAnim.Play(SimpleAnim.Kind.None);
        dialogueBox.Say(IsCurse ? GameScript.CurseGiveBoy : chapter.candies > 0 ? GameScript.GiveBoy : GameScript.GiveBoyNoCandy,
                        IsCurse ? faceBoySurprised : faceBoyHappy);
    }

    // 주인공이 건네면 쪽지와 간식이 날아가고, 도깨비가 먹고 읽은 뒤 찾으러 가요
    void GiveTap()
    {
        if (dialogueBox.FinishTyping()) return;
        Sfx.Play(giveStep == 0 ? Sfx.Give : Sfx.Tap);     // 쪽지와 사탕이 날아갈 때는 건네는 소리
        if (IsCurse)
        {
            // 5챕터 : 사탕 없이 쪽지를 들이밀어요. 도깨비는 먹거나 읽지 않고 바로 판정해요
            attempts[stageIndex]++;
            StartJudge();
        }
        else if (giveStep == 0 && chapter.candies <= 0)
        {
            // 4챕터 : 사탕 없이 쪽지만 건네요. 도깨비는 먹지 않고 바로 읽어요
            giveStep = 2;
            attempts[stageIndex]++;
            giveNoteAnim.Play(SimpleAnim.Kind.NoteGive);
            dialogueBoyAnim.Play(SimpleAnim.Kind.None);
            dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
            dialogueBox.Say(GameScript.ItemRead(Stage.targetName), faceDokkaebi);
        }
        else if (giveStep == 0)
        {
            giveStep = 1;
            int usedBefore = candiesPerStage - candies;
            candies--;
            attempts[stageIndex]++;
            UpdateSnacks();
            // 방금 쓴 사탕 자리에서 도깨비에게 날아가요
            Vector2 p = giveSnackOut.anchoredPosition;
            p.x = candyIcons[Mathf.Clamp(usedBefore, 0, candyIcons.Length - 1)].rectTransform.anchoredPosition.x;
            giveSnackOut.anchoredPosition = p;
            giveSnackOut.gameObject.SetActive(true);
            giveNoteAnim.Play(SimpleAnim.Kind.NoteGive);
            dialogueBoyAnim.Play(SimpleAnim.Kind.None);
            dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Gentle);
            dialogueBox.Say(GameScript.GiveEat, faceDokkaebi);
        }
        else if (giveStep == 1)
        {
            giveStep = 2;
            giveNote.SetActive(false);
            dialogueBox.Say(chapter.number != 1 ? GameScript.ItemRead(Stage.targetName) : GameScript.GiveReadLine(stageIndex), faceDokkaebi);
        }
        else
        {
            StartJudge();
        }
    }

    void StartJudge()
    {
        state = State.Judge;
        giveNote.SetActive(false);
        giveSnackOut.gameObject.SetActive(false);
        Sfx.Bgm(Sfx.BgmJudge);

        judgeDrawing.texture = board.Texture;
        judgeNoteText.text = NoteText();
        judgeStartTime = Time.time;
        ShowPanel(judgePanel);

        int ticket = ++judgeTicket;
        //   AI 판정이 안 되면(서버·키·인터넷 문제) 간식을 돌려주고 다시 제출하게 해요 (OnJudgeError).
        //   계산식 자동 판정(LocalJudge)은 자동 테스트(OpenAIJudge.DisableAI)에서만 써요.
        if (OpenAIJudge.DisableAI)
        {
            StartCoroutine(LocalJudgeRoutine(ticket));
        }
        else if (judge.IsReady)
        {
            StartCoroutine(judge.Judge(Stage, board.FlatTexture(),   // AI 에는 흰 바탕에 합친 그림을 보내요
                v => { if (ticket == judgeTicket) StartCoroutine(FinishLoopThen(ticket, () => OnVerdict(v))); },
                e => { if (ticket == judgeTicket) StartCoroutine(FinishLoopThen(ticket, () => OnJudgeError(e))); }));
        }
        else
        {
            StartCoroutine(NoJudgeRoutine(ticket));
        }
    }

    // 응답이 오면 도깨비가 찾는 동작 한 바퀴를 마칠 때까지 기다렸다가 넘어가요
    IEnumerator FinishLoopThen(int ticket, System.Action next)
    {
        float passed = Time.time - judgeStartTime - judgeEnterSeconds;
        float laps = Mathf.Max(1f, Mathf.Ceil(passed / Mathf.Max(0.01f, roamLoopSeconds)));
        float remain = laps * roamLoopSeconds - passed;
        if (remain > 0f) yield return new WaitForSeconds(remain);
        if (ticket == judgeTicket && state == State.Judge) next();
    }

    IEnumerator NoJudgeRoutine(int ticket)
    {
        yield return new WaitForSeconds(thinkSeconds);
        if (ticket == judgeTicket && state == State.Judge)
            yield return FinishLoopThen(ticket, () => OnJudgeError("AI 판정 설정이 없어요 (키 또는 판정 서버 주소)"));
    }

    // (자동 테스트용) 고민하는 시간만큼 기다렸다가 LocalJudge 로 판정
    IEnumerator LocalJudgeRoutine(int ticket)
    {
        yield return new WaitForSeconds(thinkSeconds);
        if (ticket != judgeTicket || state != State.Judge) yield break;
        yield return FinishLoopThen(ticket, () => OnVerdict(LocalJudge.Judge(Stage, board.Texture)));
    }

    void OnVerdict(OpenAIJudge.Verdict verdict)
    {
        if (state != State.Judge) return;
        SaveSubmission(verdict);
        ShowResult(verdict.present, verdict.notes);
    }

    // AI 판정이 실패했을 때 : 사탕을 돌려주고, 대사를 누르면 그림으로 돌아가요
    void OnJudgeError(string message)
    {
        if (state != State.Judge) return;
        if (chapter.candies > 0) candies++;
        attempts[stageIndex]--;
        judgeTicket++;      // 이 제출은 끝난 것으로 (늦게 오는 결과는 무시)
        UpdateSnacks();
        Debug.LogWarning("[OpenAIJudge] " + message);

        state = State.Result;
        resultPhase = ResultPhase.Error;
        resultItemImage.enabled = false;
        resultItemShadow.SetActive(false);
        resultSparkles.SetActive(false);
        resultDokkaebi.sprite = DokkaebiSprite();
        choiceRow.SetActive(false);
        ShowPanel(resultPanel);
        resultDokkaebiAnim.Play(SimpleAnim.Kind.None);
        resultBox.Say(GameScript.JudgeError, faceDokkaebi);
    }

    /// <summary>(AI 오류 후) 그림으로 돌아가서 다시 제출</summary>
    public void BackToDraw()
    {
        StartDraw(true);
    }
}

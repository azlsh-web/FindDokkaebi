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
        HideSoul();
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
        if (hearts < heartIcons.Length) StartCoroutine(HeartPunch(heartIcons[hearts].rectTransform));
    }

    // 깎인 하트가 크게 튀었다가 제자리로 줄어들며 흐려져요
    IEnumerator HeartPunch(RectTransform heart)
    {
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = t / 0.5f;
            heart.localScale = Vector3.one * (1f + 0.8f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f));
            heart.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 15f * (1f - k));
            yield return null;
        }
        heart.localScale = Vector3.one;
        heart.localEulerAngles = Vector3.zero;
    }

    // ───────────── 5챕터 연출 ─────────────

    // 그리기 방해 : 흑화 도깨비가 쪽지 곳곳에서 불쑥 나타나 흔들거리다 사라져요 (3.2초마다 다른 자리).
    //   낙서와 붉은 막도 일렁여요. 모두 클릭을 통과해서 그리는 데는 지장이 없어요
    static readonly Vector2[] JamSpots = { new Vector2(820f, -240f), new Vector2(200f, -700f), new Vector2(780f, -1000f), new Vector2(240f, -230f) };
    void AnimateJam()
    {
        const float Cycle = 3.2f;
        int n = Mathf.FloorToInt(Time.time / Cycle);
        float t = Time.time - n * Cycle;
        float size = t < 0.35f ? Mathf.SmoothStep(0f, 1f, t / 0.35f)
                   : t < 2.2f ? 1f
                   : t < 2.55f ? 1f - Mathf.SmoothStep(0f, 1f, (t - 2.2f) / 0.35f) : 0f;
        jamPeek.anchoredPosition = JamSpots[n % JamSpots.Length];
        jamPeek.localScale = new Vector3(n % 2 == 0 ? size : -size, size, 1f);       // 번갈아 좌우를 뒤집어요
        jamPeek.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.time * 14f) * 9f * size);
        for (int i = 0; i < jamScribbles.Length; i++)
        {
            Color c = jamScribbles[i].color;
            c.a = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 1.7f + i * 1.3f));
            jamScribbles[i].color = c;
        }
        Color tint = jamTint.color;
        tint.a = 0.2f + 0.08f * Mathf.Sin(Time.time * 3f);
        jamTint.color = tint;
    }

    // 쪽지를 들이밀어요 : 쪽지가 도깨비에게 날아가고 도깨비가 움찔한 뒤 판정 대기로
    IEnumerator ThrustNote()
    {
        giveNoteAnim.Play(SimpleAnim.Kind.NoteGive);
        dialogueBoyAnim.Play(SimpleAnim.Kind.None);
        dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Shake);
        yield return new WaitForSeconds(0.7f);
        if (state == State.Give) StartJudge();
    }

    // 판정 대기 : 내 쪽지 뒤의 빛이 커졌다 작아졌다
    void PulseGlow()
    {
        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        judgeGlow.rectTransform.localScale = Vector3.one * (0.92f + 0.16f * k);
        Color c = judgeGlow.color;
        c.a = 0.55f + 0.35f * k;
        judgeGlow.color = c;
    }

    void HideSoul()
    {
        soulFriend.gameObject.SetActive(false);
        soulSpirit.gameObject.SetActive(false);
    }

    // 하트 0 : 물건 자리에 친구가 서 있다가 쓰러지고, 영혼이 빠져나가 도깨비에게 날아가 스며들어요
    IEnumerator SoulLeaves()
    {
        resultItemImage.enabled = false;
        resultItemShadow.SetActive(false);
        resultSparkles.SetActive(false);
        RectTransform body = soulFriend.rectTransform, soul = soulSpirit.rectTransform;
        soulFriend.gameObject.SetActive(true);
        soulFriend.color = Color.white;
        body.localEulerAngles = Vector3.zero;
        soulSpirit.gameObject.SetActive(false);
        Vector2 from = body.anchoredPosition;
        Vector2 to = resultDokkaebi.rectTransform.anchoredPosition + new Vector2(0f, resultDokkaebi.rectTransform.sizeDelta.y * 0.5f);
        yield return new WaitForSeconds(0.5f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)           // 옆으로 쓰러져요
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            body.localEulerAngles = new Vector3(0f, 0f, 85f * k);          // 화면 안쪽(왼쪽)으로 쓰러져요
            soulFriend.color = Color.Lerp(Color.white, new Color(0.6f, 0.6f, 0.7f, 1f), k);
            yield return null;
        }
        body.localEulerAngles = new Vector3(0f, 0f, 85f);
        Sfx.Play(Sfx.Fail);
        soulSpirit.gameObject.SetActive(true);
        Color sc = Color.white;
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)           // 위로 떠올랐다가 도깨비에게 날아가 스며들어요
        {
            float k = t / 1.6f;
            Vector2 up = from + new Vector2(0f, 180f);
            Vector2 p = k < 0.4f ? Vector2.Lerp(from, up, Mathf.SmoothStep(0f, 1f, k / 0.4f))
                                 : Vector2.Lerp(up, to, Mathf.SmoothStep(0f, 1f, (k - 0.4f) / 0.6f));
            soul.anchoredPosition = p + new Vector2(Mathf.Sin(t * 8f) * 14f, 0f);
            float shrink = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            soul.localScale = Vector3.one * Mathf.Max(0.05f, shrink);
            sc.a = 0.85f * Mathf.Max(0f, shrink);
            soulSpirit.color = sc;
            yield return null;
        }
        soulSpirit.gameObject.SetActive(false);
        soul.anchoredPosition = from;
        soul.localScale = Vector3.one;
        resultDokkaebiAnim.Play(SimpleAnim.Kind.Shake);
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
                StartCoroutine(SoulLeaves());
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
        if (curseEndSoul != null) curseEndSoul.SetActive(kind == 1);
        PlaceCurseEndDokkaebi(gameOver: kind == 1);
        curseEndFriend.sprite = friendLost && !happy ? friendSpiritSprite : friendSprite;
        curseEndDokkaebi.sprite = happy ? (batLost ? dokkaebiNoBat : dokkaebiNormal) : DokkaebiSprite();
        curseEndBat.SetActive(happy && !batLost);       // 해피 엔딩 : 들고 있던 방망이만 두고 가요
        curseEndBox.gameObject.SetActive(false);
        if (curseEndJar != null)
        {
            curseEndJar.gameObject.SetActive(happy);
            if (happy) curseEndJar.Fill(hearts);    // 남은 하트가 사탕이 되어 통에 담겨요
        }
        Progress.MarkCleared(chapter.number);
        Sfx.Play(happy ? Sfx.Cheer : Sfx.Fail);
        ShowPanel(curseEndPanel);
        // 해피 : 정화된 도깨비가 머뭇거리다 뛰어서 도망가고 방망이만 남아요 (C6 · B5, 기획서 4쪽)
        // 게임오버 : 사람 더미 앞의 도깨비가 화면 쪽으로 천천히 커지며 다가와요 (C6 · B7)
        if (curseEndRoutine != null) StopCoroutine(curseEndRoutine);
        curseEndRoutine = StartCoroutine(happy ? HappyRunOff() : GameOverApproach());
    }

    // ───────────── 5챕터 연출 (11/23, BACKLOG C1 · C5 · C6 · 쪽지 → 물건) ─────────────

    // 끝 화면 도깨비 자리 : 해피는 오른쪽 (조립한 자리), 게임오버는 사람 더미 앞 가운데 (가운데를 기준으로 커져요)
    Vector2 curseEndDokHome;
    Vector2 curseEndDokPivot;
    bool curseEndDokSaved;
    void PlaceCurseEndDokkaebi(bool gameOver)
    {
        RectTransform rt = curseEndDokkaebi.rectTransform;
        if (!curseEndDokSaved)
        {
            curseEndDokSaved = true;
            curseEndDokHome = rt.anchoredPosition;
            curseEndDokPivot = rt.pivot;
        }
        rt.localScale = Vector3.one;
        rt.pivot = curseEndDokPivot;
        rt.anchoredPosition = curseEndDokHome;
        curseEndDokkaebi.gameObject.SetActive(true);
        if (!gameOver) return;
        // 조립한 자리(왼쪽 위 x 732, y 1070)의 가운데에서 화면 가운데(x 540)로, 조금 위(y 1010)로 옮겨요
        Vector2 size = rt.sizeDelta;
        Vector2 center = curseEndDokHome + new Vector2((0.5f - curseEndDokPivot.x) * size.x, (0.5f - curseEndDokPivot.y) * size.y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center + new Vector2(540f - (732f + 162f), 60f);
        rt.localScale = Vector3.one * 1.3f;
    }

    static readonly Color DarkTint = new Color(0.3f, 0.3f, 0.36f, 1f);

    /// <summary>진행 중인 연출을 멈추고 색 · 자리를 되돌려요 (챕터를 새로 시작할 때)</summary>
    void StopMotion()
    {
        foreach (Coroutine r in new[] { darkRoutine, noteItemRoutine, curseEndRoutine })
            if (r != null) StopCoroutine(r);
        darkRoutine = noteItemRoutine = curseEndRoutine = null;
        backgroundImage.color = Color.white;
        if (dialogueDokkaebiImage != null) dialogueDokkaebiImage.color = Color.white;
        if (darkFlash != null) darkFlash.gameObject.SetActive(false);
        if (curseNote != null) curseNote.SetActive(false);
        if (curseEndDokkaebi != null && curseEndDokSaved) PlaceCurseEndDokkaebi(gameOver: false);
    }

    /// <summary>C1 흑화 : 도깨비가 부르르 떨며 숲과 함께 어두워지고, 번쩍 한 뒤 흑화한 모습으로 번져요 (약 1초)</summary>
    IEnumerator DarkTransition()
    {
        dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Shake);
        Image dok = dialogueDokkaebiImage;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            Color c = Color.Lerp(Color.white, DarkTint, t / 0.5f);
            dok.color = c;
            backgroundImage.color = c;
            yield return null;
        }
        backgroundImage.sprite = PlayBackground;
        dok.sprite = DokkaebiSprite();
        if (darkFlash != null) darkFlash.gameObject.SetActive(true);
        dialogueDokkaebiAnim.Play(SimpleAnim.Kind.Shake);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float k = t / 0.5f;
            Color c = Color.Lerp(DarkTint, Color.white, k);
            dok.color = c;
            backgroundImage.color = c;
            if (darkFlash != null) darkFlash.color = new Color(1f, 1f, 1f, 0.85f * (1f - k));
            yield return null;
        }
        dok.color = Color.white;
        backgroundImage.color = Color.white;
        if (darkFlash != null) darkFlash.gameObject.SetActive(false);
        darkRoutine = null;
    }

    /// <summary>쪽지 → 물건 : 결과 화면에서 내 쪽지가 빛나다가 번쩍 하며 물건으로 바뀌어요 (5챕터)</summary>
    IEnumerator NoteToItem()
    {
        if (curseNote == null) yield break;
        GameObject item = resultItemImage.gameObject;
        item.SetActive(false);
        curseNote.SetActive(true);
        var rt = (RectTransform)curseNote.transform;
        rt.localScale = Vector3.one;
        if (curseNoteDrawing != null) curseNoteDrawing.texture = board.Texture;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            if (curseNoteGlow != null) curseNoteGlow.color = new Color(1f, 0.84f, 0.4f, 0.5f + 0.4f * Mathf.Sin(t * 20f));
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 40f) * 3f);
            yield return null;
        }
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            float k = t / 0.2f;
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.4f, k);
            if (curseNoteGlow != null) curseNoteGlow.color = new Color(1f, 1f, 0.8f, 1f - k);
            yield return null;
        }
        rt.localEulerAngles = Vector3.zero;
        curseNote.SetActive(false);
        item.SetActive(true);                       // 물건이 톡 커지며 나와요 (ReturnedItem 의 Pop)
        noteItemRoutine = null;
    }

    /// <summary>해피 엔딩 : 정화된 도깨비가 두리번거리다 깡충깡충 오른쪽으로 도망가요. 다 나가면 주인공 대사</summary>
    IEnumerator HappyRunOff()
    {
        RectTransform rt = curseEndDokkaebi.rectTransform;
        Vector2 home = rt.anchoredPosition;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * 22f) * 10f, 0f);
            yield return null;
        }
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            float k = t / 0.9f;
            rt.anchoredPosition = home + new Vector2(k * k * 1100f, Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * 40f);
            yield return null;
        }
        curseEndDokkaebi.gameObject.SetActive(false);
        rt.anchoredPosition = home;
        curseEndRoutine = null;
        if (!curseEndTalking) CurseEndTap();
    }

    /// <summary>게임오버 : 사람 더미 앞의 도깨비가 화면 쪽으로 천천히 커지며 다가와요. 대사는 바로 나와요</summary>
    IEnumerator GameOverApproach()
    {
        CurseEndTap();
        RectTransform rt = curseEndDokkaebi.rectTransform;
        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            float k = t / 2.5f;
            rt.localScale = Vector3.one * Mathf.Lerp(1.3f, 1.7f, k * (2f - k)) * (1f + Mathf.Sin(t * 3f) * 0.015f);
            yield return null;
        }
        curseEndRoutine = null;
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

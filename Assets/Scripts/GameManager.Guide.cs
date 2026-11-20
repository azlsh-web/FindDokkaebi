using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// GameManager 의 챕터 안내 (펜 · 색) · 도깨비 기분 부분이에요. 칸과 전체 흐름 설명은 GameManager.cs 에 있어요.
public partial class GameManager
{
    // ───────────── 챕터 안내 : 펜 (1챕터) · 색 (3챕터) ─────────────
    //   홀수 단계 = 도깨비가 말해요 (화면을 누르면 다음). 짝수 단계 = 대사가 사라지고 도깨비불이 누를 곳 둘레를 돌아요.
    //   펜 : 1 "그려 봐" → 2 쪽지 (그리면 다음) → 3 "되돌려" → 4 되돌리기 (누르면 다음) → 5 "다 그렸으면" → 6 다 그렸어! (화면을 누르면 끝)
    //   색 : 1 "붓을 눌러 봐" → 2 붓 (팔레트가 열리면 다음) → 3 "하나만 써도, 섞어도 돼" (누르면 끝)
    //   안내하는 동안은 시간이 흐르지 않아요.

    void StartToolGuide()
    {
        toolGuideStep = 1;
        peekBoxPos = ((RectTransform)peekGuideBox.transform).anchoredPosition;
        SayToolGuide();
    }

    string[] ToolGuideLines => PenGuide ? new[] { GameScript.PenDraw, GameScript.PenUndo, GameScript.PenDone } : new[] { GameScript.MixBrush, GameScript.MixHow };

    RectTransform ToolGuideTarget(int step) =>
        PenGuide ? (step == 2 ? guidePaper : step == 4 ? guideUndo : guideSubmit) : guideBrush;

    void SayToolGuide()
    {
        int index = (toolGuideStep - 1) / 2;
        peekGuide.SetActive(true);
        peekGuideBox.gameObject.SetActive(true);
        // 색 안내 마지막 대사는 열린 팔레트를 가리지 않게 화면 위쪽에서 말해요
        bool top = !PenGuide && index == 1;
        ((RectTransform)peekGuideBox.transform).anchoredPosition = top ? new Vector2(peekBoxPos.x, peekBoxPos.y + GuideBoxRise) : peekBoxPos;
        peekGuideBox.Say(ToolGuideLines[index], faceDokkaebi);
        guideHighlight.gameObject.SetActive(false);
    }

    const float GuideBoxRise = 1000f;      // 색 안내 마지막 대사 : 아래 자리에서 이만큼 위로 (팔레트 위, 위 줄 아래)

    void PointToolGuide()
    {
        // 마지막(다 그렸어!)은 가리키기만 하고 화면을 누르면 끝나요. 그 전에는 덮개를 치워서 쪽지 · 버튼을 직접 눌러요
        bool lastPen = PenGuide && toolGuideStep == 6;
        peekGuide.SetActive(lastPen);
        peekGuideBox.gameObject.SetActive(false);
        guideHighlight.gameObject.SetActive(true);
        UpdateToolGuide();
    }

    void ToolGuideTap()
    {
        if (peekGuideBox.gameObject.activeSelf && peekGuideBox.FinishTyping()) return;
        bool talking = toolGuideStep % 2 == 1;
        if (talking)
        {
            Sfx.Play(Sfx.Tap);
            bool lastLine = (toolGuideStep + 1) / 2 >= ToolGuideLines.Length;
            if (lastLine && !PenGuide) { EndToolGuide(); return; }     // 색 안내 마지막 대사 : 끝
            toolGuideStep++;
            PointToolGuide();
            return;
        }
        if (PenGuide && toolGuideStep == 6)
        {
            Sfx.Play(Sfx.Tap);
            EndToolGuide();                         // 다 그렸어! 를 가리킨 뒤 화면을 누르면 끝
        }
    }

    // 도깨비불을 대상 자리로 옮기고 돌려요. 해야 할 일을 하면 다음 대사로
    void UpdateToolGuide()
    {
        if (toolGuideStep % 2 == 1) return;
        RectTransform target = ToolGuideTarget(toolGuideStep);
        Rect r = target.rect;
        guideHighlight.position = target.TransformPoint(r.center);
        Vector3 scale = target.lossyScale;
        Vector3 own = guideHighlight.lossyScale;
        float rx = Mathf.Min(r.width * 0.5f * scale.x / own.x + 10f, 470f);
        float ry = Mathf.Min(r.height * 0.5f * scale.y / own.y + 10f, 560f);
        // 화면 가장자리의 붓 · 버튼이면 도깨비불이 화면 밖으로 나가지 않게 원을 줄여요
        var area = (RectTransform)guideHighlight.parent;
        Vector2 c = area.InverseTransformPoint(guideHighlight.position);
        Rect a = area.rect;
        rx = Mathf.Max(30f, Mathf.Min(rx, c.x - a.xMin - 50f, a.xMax - c.x - 50f));
        ry = Mathf.Max(30f, Mathf.Min(ry, c.y - a.yMin - 60f, a.yMax - c.y - 60f));
        SpinFires(guideFires, rx, ry);
        bool done = PenGuide ? (toolGuideStep == 2 && board.HasDrawn) || (toolGuideStep == 4 && board.CanRedo)
                             : toolGuideStep == 2 && toolBar.PanelOpen;
        if (!done) return;
        toolGuideStep++;
        SayToolGuide();
    }

    void EndToolGuide()
    {
        toolGuideStep = 0;
        peekGuide.SetActive(false);
        peekGuideBox.gameObject.SetActive(true);
        ((RectTransform)peekGuideBox.transform).anchoredPosition = peekBoxPos;
        guideHighlight.gameObject.SetActive(false);
    }

    /// <summary>도깨비 기분(머리 불 색)에 맞는 그림으로 바꿔요 : 0 보통, 1 주황 (3챕터), 2 붉음 (4챕터)</summary>
    void ApplyMood(int mood)
    {
        Sprite Pick(Sprite[] set, Sprite keep) => set != null && mood >= 0 && mood < set.Length && set[mood] != null ? set[mood] : keep;
        dokkaebiNormal = Pick(moodNormal, dokkaebiNormal);
        dokkaebiNoBat = Pick(moodNoBat, dokkaebiNoBat);
        dokkaebiSwing = Pick(moodSwing, dokkaebiSwing);
        faceDokkaebi = Pick(moodFace, faceDokkaebi);
        faceDokkaebiProud = Pick(moodFaceProud, faceDokkaebiProud);
        faceDokkaebiTease = Pick(moodFaceTease, faceDokkaebiTease);
        if (judgeDokkaebi != null) judgeDokkaebi.sprite = Pick(moodThink, judgeDokkaebi.sprite);
    }
}

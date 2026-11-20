using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// MvpSelfTest 의 5챕터 검사 (chapter5) 이에요. 실행 방법과 그룹 설명은 MvpSelfTest.cs 에 있어요.
static partial class MvpSelfTest
{
    static void Chapter5Steps()
    {
        // ───────────── 5챕터 : 도깨비의 저주 ─────────────
        //   판정은 LocalJudge.TestOverride 로 미리 정해 넣어요 (5챕터 물건은 자동 판정 기준이 아직 자리표시라서)
        //   엔딩 화면 뒤의 마지막 탭은 로비 씬을 불러서 검사가 끝나 버리니 누르지 않아요
        group = "chapter5";
        Add(0.5f, "5챕터 시작", () =>
        {
            StartChapter(5);
            ChapterData c5 = ChapterOf(5);
            Check(GetChapterNumber() == 5, "로비에서 5챕터를 고르면 5챕터가 시작된다");
            Check(IsOn("DialoguePanel") && IsOn("DialoguePanel/Friend") && !IsOn("HUD"), "시작 대사 : 친구가 서 있고 위 줄은 숨는다");
            Check(GetInt("hearts") == 3, "하트 3개로 시작");
            Check(!GetBool("dark"), "처음에는 밝은 숲");
            Shot("c5_dialogue");
            Then(() =>
            {
                TapToChoose();
                Check(GetBool("dark"), "간식 이야기를 꺼내면 숲이 어두워지고 도깨비가 흑화한다");
                Check(IsOn("ChoosePanel") && !IsOn("DialoguePanel"), "대사가 끝나면 고르기 화면");
            });
        });
        Add(0.3f, "5챕터 고르기", () =>
        {
            ChapterData c5 = ChapterOf(5);
            bool leftCounter = GetBool("leftIsCounter");
            Sprite left = Find("ChoosePanel/ChooseCard_0/Item").GetComponent<Image>().sprite;
            Sprite right = Find("ChoosePanel/ChooseCard_1/Item").GetComponent<Image>().sprite;
            Sprite request = c5.items[0].originalSprite, counter = c5.counterItems[0].originalSprite;
            Check(left != null && right != null && left != right, "그림 2장이 나온다");
            Check((left == counter && right == request) || (left == request && right == counter) , "두 그림은 요청 물건과 맞설 물건");
            Check(left == (leftCounter ? counter : request), "왼쪽 그림이 맞설 물건인지는 leftIsCounter 와 같다");
            Check(IsOn("ChoosePanel/ChooseCard_0") && IsOn("ChoosePanel/ChooseCard_1"), "그림 카드 두 장이 보인다");
            Shot("c5_choose");
            Then(() =>
            {
                Gm().ChooseItem(CounterSide());
                Check(IsOn("ObservePanel") && !IsOn("ChoosePanel"), "고르면 떠올리기로 간다");
                Check(GetBool("chosenCounter"), "맞설 물건을 골랐다");
                Check(Find("ObservePanel/Card/TargetImage").GetComponent<Image>().sprite == c5.counterItems[0].originalSprite, "고른 물건을 보여 준다");
            });
        });
        // 맞설 물건을 맞힘 : 하트 -1
        Add(0.4f, "5챕터 맞설 물건 그리기", () =>
        {
            Check(IsOn("DrawPanel") && IsOn("HUD/HeartGroup") && LitHearts() == 3, "그리기 : 위 줄에 하트 3개");
            Check(IsOn("DrawPanel/JamOverlay"), "맞설 물건을 그릴 때는 방해 연출이 덮인다");
            Check(!IsOn("DrawPanel/PeekGuide"), "돋보기 시범은 2챕터에서만");
            Check(GetInt("magnifiers") == 1, "돋보기는 챕터마다 1번");
            DrawStage("c5", false);
            Expect(ChapterOf(5).counterItems[0], -2);       // 전부 그림
            Shot("c5_draw");
            Then(() => Gm().Submit());
        }, () => IsOn("DrawPanel"));
        Add(0.3f, "5챕터 맞설 물건 성공", () =>
        {
            Check(GetInt("hearts") == 2 && LitHearts() == 2, "맞설 물건을 맞히면 하트 -1 (2개)");
            Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseCounterHit, "도깨비가 싫어한다");
            Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == ChapterOf(5).counterItems[0].originalSprite, "진짜 맞설 물건이 왔다");
            Shot("c5_counter_hit");
            Then(() =>
            {
                TapResult();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseBoyHit, "주인공이 기뻐한다");
                Gm().ResultTap();
                Gm().ResultTap();
                Check(IsOn("ComparePanel"), "채점표");
                Gm().NextStage();
                Check(IsOn("DialoguePanel") && GetInt("stageIndex") == 1, "하트가 남으면 다음 단계 대화");
                Check(!GetBool("friendLost"), "친구는 아직 영혼을 빼앗기지 않았다");
                TapToChoose();
                Check(IsOn("ChoosePanel"), "2단계도 고르기");
                Gm().ChooseItem(1 - CounterSide());       // 이번에는 요청 물건
                Check(!GetBool("chosenCounter") && IsOn("ObservePanel"), "요청 물건을 골랐다");
            });
        }, () => ResultShown());
        // 요청 물건을 두 번 틀림 : 점프스케어 + 친구 영혼 → 두 번째는 게임오버
        Add(0.4f, "5챕터 요청 물건 그리기", () =>
        {
            Check(!IsOn("DrawPanel/JamOverlay"), "요청 물건에는 방해 연출이 없다");
            Check(LitHearts() == 2, "하트는 그대로 2개");
            DrawStage("c5r", false);
            Expect(ChapterOf(5).items[1], -1);       // 전부 틀림
            Then(() => Gm().Submit());
        }, () => IsOn("DrawPanel"));
        Add(0.3f, "5챕터 요청 물건 1번째 실패", () =>
        {
            Check(GetBool("friendLost"), "요청 물건을 틀리면 친구 영혼을 빼앗긴다");
            Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseRequestWrong, "도깨비가 영혼을 가져간다고 한다");
            Check(!GetBool("gameOverPending") && GetInt("hearts") == 2, "한 번은 게임오버가 아니고 하트도 그대로");
            Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == ChapterOf(5).items[1].WrongSpriteFor(1), "오답 그림 1번이 왔다");
            Shot("c5_request_fail");
            Then(() =>
            {
                TapResult();
                for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                Check(IsOn("ResultPanel/ChoiceRow"), "다시 · 포기 버튼");
                Gm().Retry();
                Expect(ChapterOf(5).items[1], -1);
                Gm().Submit();
            });
        }, () => ResultShown());
        Add(0.3f, "5챕터 게임오버", () =>
        {
            Check(GetBool("gameOverPending") && Plain("ResultPanel/DialogueBox/Line") == GameScript.CurseRequestWrong, "같은 물건을 두 번 틀리면 게임오버 예약 (대사는 게임오버 화면에서)");
            Then(() =>
            {
                for (int k = 0; k < 4 && !IsOn("CurseEndPanel"); k++) Gm().ResultTap();
                Check(IsOn("CurseEndPanel") && GetInt("curseEnd") == 1, "대사를 넘기면 게임오버 화면");
                Check(IsOn("CurseEndPanel/Silhouette") && !IsOn("CurseEndPanel/Boy"), "게임오버는 실루엣만");
                Check(Progress.IsCleared(5), "엔딩을 보면 5챕터를 깬 것으로 친다");
                Shot("c5_gameover");
            });
        }, () => ResultShown());

        // 하트를 다 깎음 → 해피 엔딩 (하트 1개에서 시작해서 맞설 물건을 한 번 맞힘)
        Add(0.5f, "5챕터 해피 엔딩 준비", () =>
        {
            StartChapter(5);
            SetPrivate(Gm(), "hearts", 1);
            TapToChoose();
            Gm().ChooseItem(CounterSide());
        });
        Add(0.4f, "5챕터 해피 엔딩 그리기", () =>
        {
            DrawStage("c5h", false);
            Expect(ChapterOf(5).counterItems[0], -2);
            Then(() => Gm().Submit());
        }, () => IsOn("DrawPanel"));
        Add(0.3f, "5챕터 해피 엔딩", () =>
        {
            Check(GetInt("hearts") == 0 && LitHearts() == 0, "마지막 하트를 깎았다");
            Then(() =>
            {
                TapResult();
                Gm().ResultTap();
                Gm().ResultTap();
                Check(IsOn("ComparePanel"), "채점표");
                Gm().NextStage();
                Check(IsOn("CurseEndPanel") && GetInt("curseEnd") == 2, "하트가 0이면 해피 엔딩");
                Check(Plain("CurseEndPanel/DialogueBox/Line") == (GetBool("batLost") ? GameScript.CurseHappyNoBat : GameScript.CurseHappy), "해피 엔딩 대사");
                Shot("c5_happy");
                // 배드 엔딩 : 3단계를 다 마쳤는데 하트가 남음
                Invoke(Gm(), "ShowCurseEnd", 3);
                Check(GetInt("curseEnd") == 3 && Plain("CurseEndPanel/DialogueBox/Line") == GameScript.CurseBad, "하트가 남은 채 끝나면 배드 엔딩");
                Shot("c5_bad");
            });
        }, () => ResultShown());
    }
}

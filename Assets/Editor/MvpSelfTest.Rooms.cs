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

// MvpSelfTest 의 2 · 3 · 4챕터 검사 (chapter2 · chapter3 · chapter4) 이에요. 실행 방법과 그룹 설명은 MvpSelfTest.cs 에 있어요.
static partial class MvpSelfTest
{
    static void RoomSteps()
    {
        // ───────────── 2 · 3 · 4챕터 : 방 ─────────────
        //   판정은 Expect 로 미리 정해 넣어요 (2~4챕터 물건은 자동 판정 기준이 아직 자리표시라서).
        //   끝 장면의 마지막 탭은 로비 씬을 불러서 검사가 끝나 버리니 누르지 않아요
        {
            StageData st = null;
            int s = 0;
            int candiesAtDraw = 0;
            string Bg() => Find("Background").GetComponent<Image>().sprite.name;
            void TapToObserve() { for (int k = 0; k < 80 && !IsOn("ObservePanel"); k++) Gm().DialogueTap(); }
            // 끝 장면으로 건너뛰기 : 지금 채점표에서 마지막 물건을 마친 것처럼
            void SkipToEnd() { SetPrivate(Gm(), "stageIndex", 2); Gm().NextStage(); }

            group = "chapter2";
            Add(0.5f, "2챕터 시작", () =>
            {
                StartChapter(2);
                st = ChapterOf(2).items[0];
                Check(GetChapterNumber() == 2 && Bg() == "BG_RoomRain", "2챕터는 비 오는 방에서 시작한다");
                Check(Sfx.CurrentBgm == Sfx.BgmRoom && Sfx.CurrentAmbient == Sfx.AmbRain, "2챕터 방은 아침 곡 위에 빗소리 (C-65)");
                Check(IsOn("DialoguePanel") && !IsOn("DialoguePanel/Dokkaebi") && !IsOn("DialogueDim") && !IsOn("HUD"), "첫 대사는 주인공 혼자 (어둡게 하지 않는다)");
                Check(IsOn("DialoguePanel/WindowFire"), "첫 대사 때 창밖에 도깨비불이 보인다 (복선)");
                Check(!IsOn("DialoguePanel/Opening"), "1챕터 오프닝 그림은 나오지 않는다");
                Shot("c2_start");
                Then(() =>
                {
                    Gm().DialogueTap(); Gm().DialogueTap();
                    Check(!IsOn("DialoguePanel/WindowFire") && !IsOn("DialoguePanel/Dokkaebi"), "둘째 줄 : 도깨비불이 사라지고, 주인공이 도깨비를 부른다");
                    Gm().DialogueTap(); Gm().DialogueTap();
                    Check(IsOn("DialoguePanel/Dokkaebi") && IsOn("DialogueDim"), "셋째 줄부터 도깨비가 나오고 어두워진다");
                    Check(Plain("DialoguePanel/DialogueBox/Line") == "엄마? …나도 엄마 보고 싶다.", "기획서 대사 (도깨비도 엄마가 보고 싶다)");
                    TapToObserve();
                });
            });
            Add(0.4f, "2챕터 떠올리기", () =>
            {
                Check(Find("ObservePanel/Card/TargetImage").GetComponent<Image>().sprite == st.originalSprite, "첫 물건(양말) 원본을 떠올린다");
                Check(IsOn("ObservePanel/BoyRecall") && !IsOn("ObservePanel/DokkaebiRecall"), "주인공이 떠올린다");
            }, () => IsOn("ObservePanel"));
                // 2챕터 첫 그리기 : 도깨비가 돋보기 쓰는 법을 보여 줘요. 시범은 공짜이고 그동안 향이 타지 않아요
                float timeAtGuide = 0f;
                Add(3.0f, "2챕터 돋보기 시범", () =>
                {
                    Check(IsOn("DrawPanel/PeekGuide") && Text("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MagnifierShow, "그리기를 시작하면 도깨비가 돋보기를 눌러 보라고 한다");
                    Check(IsOn("HUD/MagnifierBadge") && Text("HUD/MagnifierBadge/Count") == "1", "돋보기 오른쪽 아래에 쓸 수 있는 횟수 1");
                    Check(Find("HUD").Find("ChocolateRow") == null, "맨 위 줄에 초콜릿이 없다");
                    Check(Find("HUD/CandyRow").childCount == 3 && CandiesLit() == 3, $"맨 위 줄에 사탕 3개 ({CandiesLit()})");
                    timeAtGuide = GetFloat("timeLeft");
                    // 사탕이 아직 날아 들어오는 중이면 겹쳐 보여서, 제자리에 둔 뒤 찍어요
                    foreach (SimpleAnim a in (SimpleAnim[])typeof(GameManager).GetField("snackAnims", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()))
                        a.enabled = false;
                    Shot("c2_peek_guide");
                    Then(() =>
                    {
                        Check(Mathf.Abs(GetFloat("timeLeft") - timeAtGuide) < 0.01f, "시범 대사 중에는 향이 타지 않는다");
                        Gm().PeekGuideTap();     // 글자 다 보이기
                        Gm().PeekGuideTap();     // 넘기기
                        Check(IsOn("DrawPanel/PeekGuide") && !IsOn("DrawPanel/PeekGuide/DialogueBox") && !IsOn("DrawPanel/MagnifierPeek"),
                              "대사를 넘기면 대사는 사라지고 아직 둘러보지 않는다");
                        Check(IsOn("HUD/MagnifierHighlight"), "맨 위 돋보기 둘레를 도깨비불이 돈다");
                        Gm().PeekGuideTap();
                        Check(!IsOn("DrawPanel/MagnifierPeek") && !IsOn("DrawPanel/PeekGuide/DialogueBox"), "화면을 눌러도 넘어가지 않는다 (돋보기를 직접 눌러야 해요)");
                    });
                }, () => IsOn("DrawPanel"));
                Add(0.8f, "2챕터 돋보기 시범 누르기", () =>
                {
                    var fire = (RectTransform)Find("HUD/MagnifierHighlight/Orbit/Fire_0");
                    Check(IsOn("HUD/MagnifierHighlight/Orbit/Fire_2/Flame") && fire.anchoredPosition.magnitude > 45f, "도깨비불 셋이 돋보기 둘레에 있다");
                    Shot("c2_peek_highlight");
                    Then(() =>
                    {
                        Gm().MagnifierTap();
                        Check(!IsOn("HUD/MagnifierHighlight"), "돋보기를 누르면 강조가 사라진다");
                        Check(!IsOn("DrawPanel/MagnifierPeek") && Text("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MagnifierHow,
                              "돋보기를 누르면 바로 열지 않고 몇 초 동안 어떻게 보는지 말한다");
                        Shot("c2_peek_how");
                        Then(() =>
                        {
                            Gm().PeekGuideTap();     // 글자 다 보이기
                            Gm().PeekGuideTap();     // 넘기기
                            Check(IsOn("DrawPanel/MagnifierPeek") && !IsOn("DrawPanel/PeekGuide"), "대사를 넘기면 둘러본다");
                            Check(Gm().MagnifiersReady == 1, "시범은 공짜 (챕터 1번이 그대로)");
                            Check(Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>().RingFill > 0.9f, "처음에는 렌즈 둘레 띠가 가득");
                        });
                    });
                });
                Add(1.5f, "2챕터 돋보기 시범 남은 시간", () =>
                {
                    var peek = Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>();
                    Check(peek.Active && peek.RingFill > 0.01f && peek.RingFill < 0.85f, $"시간이 지나면 띠가 줄어든다 ({peek.RingFill:0.00})");
                    Check(Mathf.Abs(peek.RingFill - peek.Left / 5f) < 0.05f, "띠는 남은 시간만큼 남아 있다");
                    Shot("c2_peek_count");
                });
                Add(2.0f, "2챕터 돋보기 시범 끝", () =>
                {
                    Check(!IsOn("DrawPanel/MagnifierPeek"), "5초 뒤 저절로 닫힌다");
                    Check(IsOn("DrawPanel/PeekGuide") && Text("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MagnifierRule, "닫히면 도깨비가 한 번만 더 빌려준다고 말한다");
                    Check(Mathf.Abs(GetFloat("timeLeft") - timeAtGuide) < 0.01f, "시범으로 둘러보는 동안에도 향이 타지 않는다");
                    Shot("c2_peek_rule");
                    Then(() =>
                    {
                        Gm().PeekGuideTap();     // 글자 다 보이기
                        Gm().PeekGuideTap();     // 닫기
                        Check(!IsOn("DrawPanel/PeekGuide"), "대사를 누르면 그리기로 돌아간다");
                    });
                }, () => !IsOn("DrawPanel/MagnifierPeek") && IsOn("DrawPanel/PeekGuide"));   // 렌즈가 닫힌 다음 프레임에 대사가 떠요

            Add(0.3f, "2챕터 그리기", () =>
            {
                candiesAtDraw = GetInt("candies");
                Check(candiesAtDraw == 3 && IsOn("HUD/MagnifierButton"), "사탕 3개, 돋보기가 있다");
                Check(Text("DrawPanel/Desk/Paper/Guide") == GameScript.ItemNote(st.targetName), "쪽지 글 : 도깨비야, 이 OO 찾아 줘!");
                Find("DrawPanel/ToolBar").GetComponent<ToolBar>().BrushTap();
                Check(!IsOn("DrawPanel/ToolBar/Palette/Card/Color_빨강") && !IsOn("DrawPanel/ToolBar/Palette/Card/PaintUndo"), "2챕터는 검정 펜 (빨강 · 섞기 없음)");
                Find("DrawPanel/ToolBar").GetComponent<ToolBar>().BrushTap();
                DrawStage("c2", false, colors: false);
            }, () => IsOn("DrawPanel") && !IsOn("DrawPanel/PeekGuide"));
            if (s == 0)
            {
                // 돋보기 : 챕터 1번을 여기서 써요
                float drawTimeAtPeek = 0f;
                Add(0.8f, "2챕터 돋보기", () =>
                {
                    Check(IsOn("HUD/MagnifierButton") && !IsOn("DrawPanel/MagnifierPeek"), "맨 위 왼쪽에 돋보기");
                    drawTimeAtPeek = GetFloat("timeLeft");
                    Gm().MagnifierTap();
                    Check(IsOn("DrawPanel/MagnifierPeek") && GetInt("magnifiers") == 0, "누르면 바로 챕터 1번을 쓰고 둘러본다");
                    Check(!IsOn("HUD/MagnifierBadge") && !Find("HUD/MagnifierButton").GetComponent<Button>().interactable, "다 쓰면 숫자가 사라지고 돋보기가 흐려진다");
                    Check(CandiesLit() == candiesAtDraw, "돋보기는 사탕을 쓰지 않는다");
                    Check(Find("DrawPanel/MagnifierPeek/Lens/Hole/View/Original").GetComponent<Image>().sprite == st.originalSprite, "렌즈 안에는 원본");
                }, () => IsOn("DrawPanel"));
                Add(0.3f, "2챕터 돋보기 렌즈", () =>
                {
                    var peek = Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>();
                    Check(peek.Active && peek.Left > 0f, "5초가 지나기 전에는 둘러보는 중");
                    Vector3 view = Find("DrawPanel/MagnifierPeek/Lens/Hole/View").position;
                    Find("DrawPanel/MagnifierPeek/Lens").position += new Vector3(80f, -60f, 0f);
                    Invoke(peek, "KeepView");
                    Check((Find("DrawPanel/MagnifierPeek/Lens/Hole/View").position - view).sqrMagnitude < 0.01f, "렌즈를 움직여도 원본은 그림판 자리에 그대로");
                    Shot("c2_magnifier");
                });
                Add(5.0f, "2챕터 돋보기 끝", () =>
                {
                    float now = GetFloat("timeLeft");
                    Check(!IsOn("DrawPanel/MagnifierPeek"), "5초 뒤 저절로 닫힌다");
                    Check(drawTimeAtPeek - now > 4.5f, $"둘러보는 동안에도 향이 탄다 ({drawTimeAtPeek - now:0.0}초)");
                    Check(IsOn("DrawPanel") && Board().HasDrawn, "그리던 그림 그대로 그리기로 돌아온다");
                    Gm().MagnifierTap();
                    Check(!IsOn("DrawPanel/MagnifierPeek"), "다 쓰면 더 누를 수 없다");
                });
            }

            Add(0.8f, "2챕터 첫 제출", () =>
            {
                Expect(st, -1);
                Gm().Submit();
            });
            Add(0.3f, "2챕터 1번째 실패", () =>
            {
                Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "1번째 틀림 : 오답 그림 1번");
                Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Tease", "틀리면 도깨비는 놀리는 얼굴");
                TapResult();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.RoomBoyResult(2, 0, false), "주인공 대사 : 2챕터 양말 틀림");
                for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                Check(IsOn("ResultPanel/ChoiceRow") && !IsOn("ResultPanel/GiftMagnifier"), "처음 틀렸을 때는 다시 · 포기");
                Gm().Retry();
                Gm().Submit();
            }, () => ResultShown() && CandiesLit() == 2);
                int readyBeforeGift = 0;
                Add(0.3f, "2챕터 2번째 실패 : 무료 돋보기", () =>
                {
                    readyBeforeGift = Gm().MagnifiersReady;
                    TapResult();
                    Gm().ResultTap();
                    Gm().ResultTap();
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "2번째 틀림 : 오답 그림 2번");
                    Check(Text("ResultPanel/DialogueBox/Line") == GameScript.MagnifierGift, "두 번째로 틀리면 도깨비가 돋보기를 빌려준다");
                    Check(IsOn("ResultPanel/GiftMagnifier") && !IsOn("ResultPanel/ChoiceRow"), "돋보기가 도깨비에게서 나타난다");
                    var gift = Find("ResultPanel/GiftMagnifier");
                    var dok = (RectTransform)Find("ResultPanel/Dokkaebi");
                    Vector3 local = dok.InverseTransformPoint(gift.position);
                    Check(gift.localScale.x > 0.99f && local.x > dok.rect.center.x && local.y < dok.rect.center.y,
                          "처음부터 도깨비 오른손에 쥐어져 있다 (커지며 나타나지 않는다)");
                    Check(Gm().MagnifiersReady == readyBeforeGift + 1, "무료 기회 +1");
                    Shot("c2_gift");
                }, () => ResultShown() && CandiesLit() == 1);
                Add(1.0f, "2챕터 무료 돋보기 도착", () =>
                {
                    Check(IsOn("HUD/MagnifierBadge") && Text("HUD/MagnifierBadge/Count") == (readyBeforeGift + 1).ToString(), $"맨 위 돋보기에 도착하면 숫자가 {readyBeforeGift + 1}");
                    Check(IsOn("HUD/MagnifierSparkles"), "도착하면 반짝인다");
                    Shot("c2_gift_landed");
                    Then(() =>
                    {
                        for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                        Check(IsOn("ResultPanel/ChoiceRow"), "그 뒤에 다시 · 포기 버튼");
                        Gm().Retry();
                        int global = GetInt("magnifiers");
                        Gm().MagnifierTap();
                        Check(IsOn("DrawPanel/MagnifierPeek") && GetInt("magnifiers") == global && Gm().MagnifiersReady == global, "무료 기회부터 쓴다");
                        Find("DrawPanel/MagnifierPeek").GetComponent<MagnifierPeek>().Stop();
                        Gm().Submit();
                    });
                }, () => !IsOn("ResultPanel/GiftMagnifier"));

            Add(0.3f, "2챕터 사탕 0개", () =>
            {
                TapResult();
                Check(Text("ResultPanel/DialogueBox/Line").Contains("사탕이 다 떨어졌어"), "사탕이 다 떨어지면 주인공이 말한다");
                Gm().ResultTap();
                Gm().ResultTap();
                Check(IsOn("ComparePanel"), "채점표");
                Check(Find("ComparePanel/Paper/Row_2") != null && IsOn("ComparePanel/Paper/Row_2") && !IsOn("ComparePanel/Paper/Row_3"), "채점 기준 2줄 (형태 + 무늬)");
                Shot("c2_compare");
            }, () => ResultShown() && CandiesLit() == 0);
            Add(0.3f, "2챕터 끝 장면", () =>
            {
                SkipToEnd();
                Check(IsOn("RoomEndPanel") && !IsOn("HUD") && !IsOn("RoomEndPanel/DialogueBox"), "물건을 다 거치면 방 끝 장면 (처음엔 대사 없이)");
                Check(IsOn("RoomEndPanel/Item_0") && IsOn("RoomEndPanel/Item_2") && Find("RoomEndPanel/Item_0").GetComponent<Image>().sprite == st.AllWrongSprite,
                      "찾은 물건 3개가 놓이고, 포기한 물건은 받은 엉뚱한 물건");
                Check(Progress.IsCleared(2) && Progress.IsOpen(3), "2챕터를 깨면 3챕터가 열린다");
                Check(IsOn("RoomEndPanel/CandyJar") && Find("RoomEndPanel/CandyJar").GetComponent<CandyJar>().Shown == GetInt("jarCandies"),
                      "방 끝 장면에도 사탕 통 : 남은 사탕이 모인다");
                Shot("c2_end");
                Then(() =>
                {
                    Gm().EndingTap();
                    Check(IsOn("RoomEndPanel/DialogueBox") && Plain("RoomEndPanel/DialogueBox/Line") == GameScript.RoomEndLines(2)[0].text, "화면을 누르면 도깨비가 인사한다");
                    Shot("c2_end_bye");
                });
            });

            group = "chapter3";
            Add(0.5f, "3챕터 시작", () =>
            {
                StartChapter(3);
                st = ChapterOf(3).items[0];
                Check(Bg() == "BG_RoomEvening", "3챕터는 저녁 방");
                Check(Sfx.CurrentBgm == Sfx.BgmEvening && Sfx.CurrentAmbient == null, "3챕터 방은 저녁 곡, 빗소리는 꺼짐 (C-65)");
                Then(() =>
                {
                    for (int k = 0; k < 20 && !IsOn("DialoguePanel/Dokkaebi"); k++) Gm().DialogueTap();
                    Check(Find("DialoguePanel/Dokkaebi/Image").GetComponent<Image>().sprite.name.EndsWith("_M1"), "3챕터 도깨비는 머리 불이 주황 (복선)");
                    Check(Find("DialoguePanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Normal_M1", "대사 상자 얼굴도 같은 주황 머리");
                    Shot("c3_dokkaebi");
                    TapToObserve();
                });
            });
            Add(3.2f, "3챕터 색 안내 : 붓", () =>
            {
                Check(IsOn("DrawPanel/PeekGuide") && Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MixBrush, "3챕터 첫 그리기 : 도깨비가 붓을 눌러 보라고 한다");
                Gm().PeekGuideTap();
                Gm().PeekGuideTap();
                Check(!IsOn("DrawPanel/PeekGuide") && IsOn("DrawPanel/GuideHighlight"), "도깨비불이 붓 둘레를 돈다");
                foreach (SimpleAnim a in (SimpleAnim[])typeof(GameManager).GetField("snackAnims", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()))
                    a.enabled = false;      // 사탕이 날아 들어오는 중이면 톱니와 잠깐 겹쳐 보여서 제자리에 둬요
                Shot("c3_mix_brush");
                Then(() => Find("DrawPanel/ToolBar").GetComponent<ToolBar>().BrushTap());
            }, () => IsOn("DrawPanel"));
            Add(0.3f, "3챕터 색 안내 : 팔레트", () =>
            {
                Check(IsOn("DrawPanel/ToolBar/Palette") && IsOn("DrawPanel/ToolBar/Palette/Card/Color_빨강") && IsOn("DrawPanel/ToolBar/Palette/Card/Color_검정")
                      && !IsOn("DrawPanel/ToolBar/Palette/Card/PaintUndo"), "3챕터는 검정 윤곽선과 삼원색 선택 (혼색 없음)");
                var box = (RectTransform)Find("DrawPanel/PeekGuide/DialogueBox");
                var palette = (RectTransform)Find("DrawPanel/ToolBar/Palette");
                Check(box.position.y > palette.position.y + 100f, "마지막 안내 대사는 열린 팔레트를 가리지 않게 위쪽에");
                Shot("c3_mix_how");
                Then(() =>
                {
                    Gm().PeekGuideTap();
                    Gm().PeekGuideTap();
                    Check(!IsOn("DrawPanel/PeekGuide") && !IsOn("DrawPanel/GuideHighlight"), "대사를 누르면 안내가 끝난다");
                    Find("DrawPanel/ToolBar").GetComponent<ToolBar>().ClosePanel();
                });
            }, () => IsOn("DrawPanel/PeekGuide") && Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.MixHow);
            Add(0.5f, "3챕터 그리기", () =>
            {
                DrawStage("c3", false);
                Expect(st, -2);
                Gm().Submit();
            });
            Add(0.3f, "3챕터 성공 · 끝 장면", () =>
            {
                Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.originalSprite, "진짜 고깔모자가 왔다");
                Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Proud_M1", "뿌듯한 얼굴도 주황 머리");
                TapResult();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.RoomBoyResult(3, 0, true), "주인공 대사 : 3챕터 고깔모자 맞음");
                Gm().ResultTap();
                Gm().ResultTap();
                Check(IsOn("ComparePanel") && IsOn("ComparePanel/Paper/Row_3"), "채점 기준 3줄 (형태 + 무늬 + 색)");
                SkipToEnd();
                Check(IsOn("RoomEndPanel") && Find("RoomEndPanel/Dokkaebi").GetComponent<Image>().sprite.name.EndsWith("_M1"), "끝 장면 도깨비도 주황 머리");
                Check(Progress.IsCleared(3) && Progress.IsOpen(4), "3챕터를 깨면 4챕터가 열린다");
            }, () => ResultShown());

            group = "chapter4";
            Add(0.5f, "4챕터 시작", () =>
            {
                StartChapter(4);
                st = ChapterOf(4).items[0];
                Check(Bg() == "BG_RoomNight", "4챕터는 밤 방");
                Check(Sfx.CurrentBgm == Sfx.BgmNight, "4챕터 방은 밤 곡 (C-65)");
                Then(TapToObserve);
            });
            Add(3.2f, "4챕터 그리기", () =>
            {
                Check(!IsOn("DrawPanel/PeekGuide"), "4챕터는 안내가 없다");
                Check(GetInt("candies") == 0 && CandiesLit() == 0, "사탕 0개 (바구니가 비었다)");
                DrawStage("c4", false);
                Expect(st, -1);
                Shot("c4_draw");
                Then(() =>
                {
                    Gm().Submit();
                    Check(Plain("DialoguePanel/DialogueBox/Line") == GameScript.GiveBoyNoCandy, "사탕 없이 쪽지만 건넨다");
                });
            }, () => IsOn("DrawPanel"));
            Add(0.3f, "4챕터 건네기", () =>
            {
                Gm().DialogueTap(); Gm().DialogueTap();
                Check(Plain("DialoguePanel/DialogueBox/Line") == GameScript.ItemRead(st.targetName) && !IsOn("DialoguePanel/SnackOut"), "도깨비는 먹지 않고 바로 쪽지를 읽는다 (날아가는 사탕 없음)");
                Check(SumInts("attempts") == 1 && GetInt("candies") == 0, "한 번 그렸고 사탕은 그대로 0개");
                Gm().DialogueTap(); Gm().DialogueTap();
            }, () => IsOn("DialoguePanel/GiveNote"));
            Add(0.3f, "4챕터 틀림 → 바로 채점표", () =>
            {
                Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "틀리면 오답 그림");
                TapResult();
                Check(Plain("ResultPanel/DialogueBox/Line") == GameScript.RoomBoyResult(4, 0, false), "주인공 대사 : 사탕이 없어 다시 그릴 수 없다");
                Shot("c4_wrong");
                Then(() =>
                {
                    Gm().ResultTap();
                    Gm().ResultTap();
                    Check(IsOn("ComparePanel") && !IsOn("ResultPanel"), "다시 · 포기 없이 바로 채점표");
                });
            }, () => ResultShown());
            Add(0.3f, "4챕터 끝 장면", () =>
            {
                SkipToEnd();
                Check(IsOn("RoomEndPanel") && !IsOn("RoomEndPanel/RedEyes"), "방 끝 장면 (빨간 눈은 아직)");
                Gm().EndingTap();
                Check(Plain("RoomEndPanel/DialogueBox/Line") == GameScript.RoomEndLines(4)[0].text, "도깨비가 약속을 말한다");
                Check(Find("RoomEndPanel/Dokkaebi").GetComponent<Image>().sprite?.name == "Char_Dokkaebi_Awkward"
                      && Find("RoomEndPanel/DialogueBox/Face").GetComponent<Image>().sprite?.name == "Char_DokkaebiFace_Awkward",
                      "4챕터 끝 장면 도깨비는 어색하게 웃는다 (C-64)");
                Gm().EndingTap();
                Gm().EndingTap();
                Check(Plain("RoomEndPanel/DialogueBox/Line") == GameScript.RoomEndLines(4)[1].text && !IsOn("RoomEndPanel/RedEyes"),
                      "도깨비가 팥은 싫다고 흘린다 (5챕터 복선)");
                Gm().EndingTap();
                Gm().EndingTap();
                Check(Plain("RoomEndPanel/DialogueBox/Line") == GameScript.RoomEndLines(4)[2].text, "주인공 속마음 (간식을 숨기자)");
                Check(IsOn("RoomEndPanel/RedEyes"), "속마음 때 창 안에 빨간 눈 (복선)");
                Check(Progress.IsCleared(4) && Progress.IsOpen(5), "4챕터를 깨면 5챕터가 열린다");
            });
            Add(1.5f, "4챕터 끝 장면 사진", () =>
            {
                Shot("c4_end");
            });
        }
    }
}

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

// MvpSelfTest 의 1챕터 검사 (stage1 ~ stage5 · ending) 이에요. 실행 방법과 그룹 설명은 MvpSelfTest.cs 에 있어요.
static partial class MvpSelfTest
{
    // 1챕터 물건 1 ~ 5, 이야기 종료
    static void Chapter1Steps()
    {
        StageData[] stages = Stages();
        for (int si = 0; si < stages.Length; si++)
        {
            int s = si;                 // 람다 안에서 쓸 복사본
            StageData st = stages[s];
            bool last = s == stages.Length - 1;
            group = $"stage{s + 1}";

            // 대화 → 떠올리기
            Add(1.2f, $"{st.Title} 대화", () =>
            {
                Check(IsOn("DialoguePanel") && Find("Background").GetComponent<Image>().sprite != null, $"{st.Title} 대화 화면");
                Check(!IsOn("HUD"), "대화 중에는 간식과 일시정지를 숨긴다");
                Check(IsOn("DialoguePanel/Boy"), "주인공이 보인다");
                Check(!IsOn("DialoguePanel/GiveNote"), "대화 중에는 쪽지가 없다");
                if (s == 0)
                {
                    Check(!IsOn("DialoguePanel/Dokkaebi"), "맨 처음에는 주인공 혼자 말한다");
                    Check(IsOn("DialoguePanel/Opening/Bag") && !IsOn("DialogueDim"), "오프닝은 어둡게 하지 않고 준비물이 보인다");
                }
                else Check(!IsOn("DialoguePanel/Opening"), "오프닝 그림은 1단계에만 나온다");
                Check(!IsOn("DialoguePanel/Friend") && !IsOn("HUD/HeartGroup"), "1챕터에는 친구와 하트가 없다");
                Shot($"s{s + 1}_dialogue");
                Then(() =>
                {
                    if (s == 0)
                    {
                        for (int k = 0; k < 40 && !IsOn("DialoguePanel/Dokkaebi"); k++) Gm().DialogueTap();
                        int line = (int)Gm().GetType().GetField("dialogueLine", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());
                        Check(line == GameScript.OpeningAloneLines, "도깨비는 주인공 혼잣말이 끝난 뒤 등장한다");
                        Check(!IsOn("DialoguePanel/Opening") && IsOn("DialogueDim"), "도깨비가 나오면 오프닝 그림이 사라지고 어두워진다");
                    }
                    for (int k = 0; k < 40 && !IsOn("ObservePanel"); k++) Gm().DialogueTap();
                });
            });

            // 관찰 3초
            Add(0.4f, $"{st.Title} 관찰", () =>
            {
                Check(IsOn("ObservePanel") && Find("ObservePanel/Card/TargetImage").GetComponent<Image>().sprite == st.originalSprite, "원본 그림을 보여준다");
                Check(IsOn("ObservePanel/CountEdge/CountText"), "숫자 하나");
                Check(IsOn("ObservePanel/DokkaebiRecall") == (s == 0) && IsOn("ObservePanel/BoyRecall") == (s != 0), "1단계는 도깨비, 그 뒤는 주인공이 떠올린다");
                Check(Find("DialoguePanel") != null && !IsOn("DialoguePanel"), "떠올리는 장면은 대사 없이 나온다");
                if (s == 0) Shot($"s{s + 1}_observe");
            });

            if (s == 0)
            {
                // 1챕터 첫 그리기 : 도깨비가 펜 쓰는 법을 알려 줘요 (그려 보기 → 되돌리기 → 다 그렸어!). 그동안 시간이 흐르지 않아요
                float timeAtGuide = 0f;
                Add(3.0f, "1챕터 펜 안내 : 그려 보기", () =>
                {
                    Check(IsOn("DrawPanel/PeekGuide") && Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.PenDraw, "그리기를 시작하면 도깨비가 쪽지에 그려 보라고 한다");
                    Check(!IsOn("HUD/MagnifierButton") && !IsOn("HUD/MagnifierBadge"), "1챕터에는 돋보기가 없다");
                    Check(Find("HUD/CandyRow").childCount == 3 && CandiesLit() == 3, $"맨 위 줄에 사탕 3개 ({CandiesLit()})");
                    timeAtGuide = GetFloat("timeLeft");
                    foreach (SimpleAnim a in (SimpleAnim[])typeof(GameManager).GetField("snackAnims", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()))
                        a.enabled = false;
                    Shot("s1_pen_guide");
                    Then(() =>
                    {
                        Check(Mathf.Abs(GetFloat("timeLeft") - timeAtGuide) < 0.01f, "안내 대사 중에는 시간이 흐르지 않는다");
                        Gm().PeekGuideTap();     // 글자 다 보이기
                        Gm().PeekGuideTap();     // 넘기기
                        Check(!IsOn("DrawPanel/PeekGuide") && IsOn("DrawPanel/GuideHighlight"), "대사가 사라지고 도깨비불이 쪽지 둘레를 돈다 (덮개가 없어 그릴 수 있다)");
                        Gm().Submit();
                        Check(IsOn("DrawPanel"), "안내 중에는 제출되지 않는다");
                    });
                }, () => IsOn("DrawPanel"));
                Add(0.5f, "1챕터 펜 안내 : 쪽지 가리키기", () =>
                {
                    var fire = (RectTransform)Find("DrawPanel/GuideHighlight/Orbit/Fire_0");
                    Check(fire.anchoredPosition.magnitude > 200f, "도깨비불이 쪽지 크기만큼 크게 돈다");
                    Shot("s1_pen_paper");
                    Then(() => Stroke(new Vector2(0.3f, 0.5f), new Vector2(0.7f, 0.5f)));
                });
                Add(0.3f, "1챕터 펜 안내 : 되돌리기", () =>
                {
                    Check(IsOn("DrawPanel/PeekGuide") && Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.PenUndo, "쪽지에 그리면 되돌리기를 알려 준다");
                    Gm().PeekGuideTap();
                    Gm().PeekGuideTap();
                    Check(!IsOn("DrawPanel/PeekGuide") && IsOn("DrawPanel/GuideHighlight"), "도깨비불이 되돌리기 둘레를 돈다");
                    Shot("s1_pen_undo");
                    Then(() => Board().Undo());
                }, () => IsOn("DrawPanel/PeekGuide") && Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.PenUndo);
                Add(0.3f, "1챕터 펜 안내 : 다 그렸어", () =>
                {
                    Check(Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.PenDone, "되돌리면 다 그렸어! 버튼을 알려 준다");
                    Gm().PeekGuideTap();
                    Gm().PeekGuideTap();
                    Check(IsOn("DrawPanel/PeekGuide") && !IsOn("DrawPanel/PeekGuide/DialogueBox") && IsOn("DrawPanel/GuideHighlight"), "도깨비불이 다 그렸어! 둘레를 돌고, 화면을 누르면 끝난다");
                    Shot("s1_pen_done");
                    Then(() =>
                    {
                        Gm().PeekGuideTap();
                        Check(!IsOn("DrawPanel/PeekGuide") && !IsOn("DrawPanel/GuideHighlight"), "화면을 누르면 안내가 끝난다");
                        Check(Mathf.Abs(GetFloat("timeLeft") - timeAtGuide) < 0.01f, "안내하는 동안 시간이 흐르지 않았다");
                        Check(!Board().HasDrawn, "되돌려서 빈 쪽지로 시작한다");
                    });
                }, () => IsOn("DrawPanel/PeekGuide") && Plain("DrawPanel/PeekGuide/DialogueBox/Line") == GameScript.PenDone);
            }
            // 그리기 + 도구
            int candiesAtDraw = 0;      // 이번 단계 그리기를 시작할 때 남은 사탕
            Add(s == 0 ? 0.3f : 3.0f, $"{st.Title} 그리기", () =>
            {
                Check(IsOn("DrawPanel") && !IsOn("ObservePanel"), "3초 뒤 그리기 화면");
                Check(!IsOn("DrawPanel/PeekGuide"), "안내는 챕터 첫 물건 첫 그리기에서만");
                Check(!IsOn("HUD/MagnifierButton"), "1챕터에는 돋보기가 없다");
                candiesAtDraw = GetInt("candies");
                Check(candiesAtDraw == 3, $"{st.Title} 사탕 3개로 시작");
                Check(Find("HUD/TimerGroup").GetComponentInChildren<TMP_Text>(true) == null, "남은 초 숫자는 없다 (게이지만)");
                {
                    var flame = Find("HUD/TimerGroup/Incense/IncenseFx_Tip/IncenseFx_Flame").GetComponent<Image>();
                    var stick = (RectTransform)Find("HUD/TimerGroup/Incense");
                    Check(stick.rect.width >= 880f && flame.enabled && flame.sprite != null && flame.sprite.name == "HUD_CandleFlame",
                          "촛대 게이지 : 막대는 화면 가로 거의 가득, 끝에 불꽃 그림");
                    var tip = (RectTransform)Find("HUD/TimerGroup/Incense/IncenseFx_Tip");
                    Check(tip.anchoredPosition.x > stick.rect.width * 0.9f, "그리기를 시작하면 불꽃이 막대 오른쪽 끝에 있다");
                }
                bool primaries = false, mixing = false;    // 1챕터 펜 도구는 검정 펜 (빨강 · 노랑 · 파랑 섞기는 3챕터부터)
                Check(!IsOn("DrawPanel/ToolBar/Palette"), "팔레트는 닫힌 채로 시작");
                Find("DrawPanel/ToolBar").GetComponent<ToolBar>().BrushTap();
                Check(IsOn("DrawPanel/ToolBar/Palette"), "붓을 누르면 팔레트가 열린다");
                Check(IsOn("DrawPanel/ToolBar/Palette/Card/Color_빨강") == primaries, $"빨강 버튼은 {(primaries ? "보임" : "숨김")}");
                Check(IsOn("DrawPanel/ToolBar/Palette/Card/PaintUndo") == mixing, $"물감 빼기는 {(mixing ? "보임" : "숨김")}");
                Find("DrawPanel/ToolBar").GetComponent<ToolBar>().BrushTap();
                Check(!IsOn("DrawPanel/ToolBar/Palette"), "붓을 다시 누르면 닫힌다");
                Check(!Find("DrawPanel/ToolBar/SubmitButton").GetComponent<Button>().interactable, "빈 쪽지면 제출 비활성");
                Check(Text("DrawPanel/Desk/Paper/Guide") == GameScript.NoteText(s), "쪽지 글이 적혀 있다");
                if (s == 1)
                {
                    // 전체 화면 : 새 쪽지는 처음부터 똑바로 서서 FullArea 를 가득 (버튼은 숨어요)
                    //   보기가 흐트러지면 버튼이 뜨고, 누르면 다시 똑바로 가득 (각도 0도)
                    var view = Find("DrawPanel/Desk").GetComponent<PaperView>();
                    var paperT = Find("DrawPanel/Desk/Paper");
                    var area = ((RectTransform)Find("DrawPanel/FullArea")).rect;
                    var paperRect = ((RectTransform)paperT).rect;
                    bool Filled()
                    {
                        float w = paperRect.width * paperT.localScale.x, h = paperRect.height * paperT.localScale.y;
                        return w <= area.width + 0.5f && h <= area.height + 0.5f && (area.width - w < 50f || area.height - h < 50f);
                    }
                    Check(view.IsFull && paperT.localRotation == Quaternion.identity && Filled(), "새 쪽지는 처음부터 똑바로 서서 그리는 자리를 가득 채운다");
                    view.SendMessage("Update");
                    Check(!IsOn("DrawPanel/FitButton"), "전체 화면일 때는 버튼이 숨는다");
                    view.SetView(1.3f, 385f, new Vector2(40f, -60f));
                    view.SendMessage("Update");
                    Check(!view.IsFull && IsOn("DrawPanel/FitButton"), "보기가 흐트러지면 전체 화면 버튼이 뜬다");
                    view.FitTap();
                    for (int f = 0; f < 3000 && !(paperT.localRotation == Quaternion.identity && Filled()); f++) view.SendMessage("Update");   // 다 올 때까지
                    Check(view.IsFull && paperT.localRotation == Quaternion.identity && Filled(), $"버튼을 누르면 꼭 0도로 똑바로 서서 가득 채운다 (각도 {paperT.localEulerAngles.z:0.###}, 크기 {paperT.localScale.x:0.###}/{view.Scale:0.###}, dt {Time.unscaledDeltaTime:0.####})");
                    Check(!IsOn("DrawPanel/FitButton"), "누른 뒤에는 버튼이 다시 숨는다");
                    view.SetView(0.8f, -40f, Vector2.zero);
                    view.ResetView();
                    Check(view.IsFull && paperT.localRotation == Quaternion.identity && Filled(), "다음 쪽지도 처음부터 전체 화면");
                }
                DrawStage($"s{s + 1}", false, colors: false);
            }, () => IsOn("DrawPanel"));
            Add(0.8f, $"{st.Title} 그린 뒤", () =>
            {
                Check(Board().HasDrawn && Find("DrawPanel/ToolBar/SubmitButton").GetComponent<Button>().interactable, "그리면 제출 활성");
                Check(Find("DrawPanel/ToolBar/Undo").GetComponent<Button>().interactable, "그리면 되돌리기 활성");
                Check(CandiesLit() == GetInt("candies"), $"위 줄에 남은 사탕 {GetInt("candies")}개");
                Check(IsOn("HUD/MagnifierBadge") == (Gm().MagnifiersReady > 0), $"돋보기 숫자는 쓸 수 있을 때만 ({Gm().MagnifiersReady})");
                Check(((RectTransform)Find("HUD/CandyRow/Candy_1")).anchoredPosition.x - ((RectTransform)Find("HUD/CandyRow/Candy_0")).anchoredPosition.x > 50f, "간식이 제자리로 들어왔다");
                if (s >= 3)
                    Check(BrushTip() == Find("DrawPanel/ToolBar").GetComponent<ColorMixer>().ResultColor, "붓 끝이 지금 색");
                if (s == 0)
                {
                    Board().Clear();
                    Check(!Board().HasDrawn, "모두 지우기");
                    Board().Undo();
                    Check(Board().HasDrawn, "되돌리기로 지운 그림 복구");

                    // 그림판 밖으로 나갔다가 다른 곳으로 들어오기 : 왼쪽으로 나가서 아래로 돌아 들어옴
                    Board().Clear();
                    Stroke(V(0.3f, 0.5f), V(-0.3f, 0.5f), V(-0.3f, -0.3f), V(0.5f, -0.3f), V(0.5f, 0.3f));
                    Check(!Ink(0.25f, 0.25f), "밖으로 나간 곳과 들어온 곳이 직선으로 이어지지 않는다");
                    Check(Ink(0.002f, 0.5f) && Ink(0.5f, 0.002f), "선은 그림판 가장자리까지 그려진다");
                    Check(Ink(0.5f, 0.2f), "다시 들어온 뒤의 선은 그려진다");
                    Board().Undo();
                    Check(!Board().HasDrawn, "되돌리기로 빈 쪽지가 되면 빈 쪽지로 인식");
                    Board().Undo();
                    Check(Board().HasDrawn, "그 전 그림으로 되돌리기");
                }
                // 도구 확인이 끝나면, 이번 결과(강제로 넣는 판정)에 맞는 손그림으로 바꿔서 제출해요.
                PutSketch(st, allDrawn: false);
                Shot($"s{s + 1}_draw");
                Expect(st, s == 1 ? -1 : st.features.Length - 1); // 2단계는 전부 빠뜨림, 나머지는 마지막 특징만 빠뜨림
                if (s == 2)
                {
                    // 3단계 : 향이 다 타면 자동 제출되는지 (남은 시간을 0.3초로 줄여 봄)
                    SetPrivate(Gm(), "timeLeft", 30f);
                    new GameObject("SelfTestShot").AddComponent<ShotHelper>().path = Path.Combine(OutDir, "extra_incense_8s.png");
                    Then(() => SetPrivate(Gm(), "timeLeft", 0.3f));
                }
                else Then(() =>
                {
                    Gm().Submit();
                    if (s != 0) return;
                    // 건네기 : 그림이 적힌 쪽지 → 위쪽 사탕이 날아가며 하나 줄어요
                    Check(IsOn("DialoguePanel/GiveNote") && IsOn("HUD") && CandiesLit() == candiesAtDraw, "건네기 : 쪽지가 보이고 아직 사탕은 그대로");
                    Check(Find("DialoguePanel/GiveNote/MyDrawing").GetComponent<RawImage>().texture == Board().Texture, "쪽지에 내가 그린 그림");
                    Check(Text("DialoguePanel/GiveNote/Text") == GameScript.NoteText(s), "쪽지에 글이 적혀 있다");
                    Shot("s1_give");
                    Gm().DialogueTap();
                    Gm().DialogueTap();
                    Check(CandiesLit() == candiesAtDraw - 1 && IsOn("DialoguePanel/SnackOut"), "건네면 사탕 -1, 사탕이 도깨비에게 날아간다");
                });
            });

            // 판정 대기
            Add(s == 2 ? 1.0f : 0.3f, $"{st.Title} 판정", () =>
            {
                if (s == 2) Check(Find("HUD/TimerGroup/Incense/IncenseFx_Tip").position.x < Find("HUD/TimerGroup/Incense").position.x - 100f, "시간이 다 되면 게이지 바가 왼쪽 끝까지 줄어든다");
                Check(IsOn("JudgePanel"), s == 2 ? "향이 다 타서 자동 제출됐다" : "판정 화면");
                Check(CandiesLit() == candiesAtDraw - 1, $"제출하면 사탕 -1 ({CandiesLit()})");
                Check(Find("HUD/CandyRow/Candy_0").GetComponent<Image>().color.a < 0.9f, "사탕은 왼쪽부터 흐려진다");
                Check(IsOn("JudgePanel/MyDrawingBack/MyDrawing"), "내 그림이 떠 있다");
                if (s == 0) Shot("s1_judge");

                if (s == 0)
                {
                    // AI 오류가 나면 사탕을 돌려주고, 대사를 누르면 그림 그대로 돌아가요
                    Invoke(Gm(), "OnJudgeError", "테스트용 가짜 오류");
                    Check(CandiesLit() == candiesAtDraw, "오류가 나면 사탕을 돌려준다");
                    Check(IsOn("ResultPanel/DialogueBox") && !IsOn("ResultPanel/ChoiceRow"), "도깨비가 오류를 말한다");
                    Shot("s1_ai_error");
                    Then(() =>
                    {
                        Gm().ResultTap();
                        Gm().ResultTap();
                        Check(IsOn("DrawPanel") && Board().HasDrawn, "그리던 그림 그대로 돌아왔다");
                        Gm().Submit();
                    });
                }
            }, () => { PassGive(); return IsOn("JudgePanel") && ((RectTransform)Find("JudgePanel/DokkaebiEnter")).anchoredPosition.x > -95f; });

            if (s == 1)
            {
                // 2단계 : 하나도 못 맞혀서 사탕 3개를 다 씀. 두 번째로 틀리면 무료 돋보기를 받아요
                Add(0.3f, "2번째 물건 1번째 실패", () =>
                {
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "1번째 틀림 : 오답 그림 1번");
                    TapResult();
                    Gm().ResultTap();
                    Gm().ResultTap();
                    Check(IsOn("ResultPanel/ChoiceRow") && !IsOn("ResultPanel/GiftMagnifier"), "처음 틀렸을 때는 무료 돋보기가 없다");
                    Gm().Retry();
                    Gm().Submit();
                }, () => ResultShown() && CandiesLit() == 2);
                Add(0.3f, "2번째 물건 2번째 실패 (1챕터는 무료 돋보기 없음)", () =>
                {
                    TapResult();
                    for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "2번째 틀림 : 오답 그림 2번");
                    Check(IsOn("ResultPanel/ChoiceRow") && !IsOn("ResultPanel/GiftMagnifier"), "1챕터는 두 번째로 틀려도 돋보기를 주지 않고 다시 · 포기");
                    Gm().Retry();
                    Gm().Submit();
                }, () => ResultShown() && CandiesLit() == 1);
                Add(0.3f, "2단계 사탕 0개", () =>
                {
                    Check(CandiesLit() == 0, "사탕 0개");
                    Check(!IsOn("ResultPanel/GiftMagnifier"), "무료 기회는 단계마다 한 번");
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "3번째 틀림 : 오답 그림 3번");
                    TapResult();
                    Check(!IsOn("ResultPanel/ChoiceRow"), "사탕이 없으면 다시 · 포기 버튼이 없다");
                    Check(Text("ResultPanel/DialogueBox/Line").Contains("사탕이 다 떨어졌어"), "사탕이 다 떨어졌다고 주인공이 말한다");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_BoyFace_Sad", "사탕이 떨어지면 우는 얼굴");
                    Shot("s2_out_of_snacks");
                    Then(() =>
                    {
                        Gm().ResultTap();   // 글자 다 보이기
                        Gm().ResultTap();   // 채점표로
                        Check(IsOn("ComparePanel"), "대사를 누르면 채점표로 넘어간다");
                    });
                }, () => ResultShown() && CandiesLit() == 0);
            }
            else
            {
                // 형태 하나를 빠뜨림 → 첫 번째 오답 물건
                Add(0.3f, $"{st.Title} 부분 성공", () =>
                {
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.AllWrongSprite, "틀린 물건이 왔다 (오답 그림 1번)");
                    if (s == 0) Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_NoBat", "1단계를 틀리면 도깨비는 빈손");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Tease", "틀리면 도깨비는 놀리는 얼굴");
                    if (s == 0 || s == 4) Shot($"s{s + 1}_result_partial");
                    Action next = () =>
                    {
                        TapResult();
                        Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_BoyFace_Sad", "엉뚱한 물건이 오면 주인공은 우는 얼굴");
                        for (int k = 0; k < 3 && !IsOn("ResultPanel/ChoiceRow"); k++) Gm().ResultTap();
                        Check(IsOn("ResultPanel/ChoiceRow/RetryButton") && IsOn("ResultPanel/ChoiceRow/GiveUpButton"), "대사 뒤에 다시 · 포기 버튼");
                        Check(!IsOn("ResultPanel/DialogueBox"), "버튼은 대사 상자 자리에 나온다");
                        if (s == 0) Shot("s1_choice");
                        Gm().Retry();
                    };
                    if (s == 0 || s == 4) Then(next); else next();
                }, ResultShown);

                // 다시 → 성공
                Add(0.6f, $"{st.Title} 다시 그리기", () =>
                {
                    Check(IsOn("DrawPanel") && Board().HasDrawn, "다시 그리기는 이전 그림에서 이어진다");
                    PutSketch(st, allDrawn: true);
                    Expect(st, -2);     // 이번엔 전부 그림
                    if (s == 2) DevMode.TestOverride = true;    // 3번째 물건은 개발자 모드로 (C-66)
                    Gm().Submit();
                });
                if (s == 2)
                    Add(0.3f, "개발자 모드 : 기준마다 O · X 고르기", () =>
                    {
                        var dev = Find("JudgePanel/DevJudge").GetComponent<DevJudgePanel>();
                        int rows = 0;
                        for (int k = 0; k < 5; k++) if (IsOn("JudgePanel/DevJudge/Row" + k)) rows++;
                        Check(rows == st.features.Length, $"판정 기준 수만큼 줄이 나온다 ({rows}줄)");
                        string Row0() => Find("JudgePanel/DevJudge/Row0/Label").GetComponent<TMP_Text>().text;
                        Check(Row0().StartsWith("O") && Row0().Contains(st.features[0].name), "처음엔 모두 O, 기준 이름이 보인다");
                        dev.RowTap(0);
                        Check(Row0().StartsWith("X"), "누르면 X 로 바뀐다");
                        dev.RowTap(0);
                        Check(TryFind("SettingsPanel/SettingsCard/SettingsMain/DevModeButton") != null, "설정 창에 개발자 모드 버튼이 있다");
                        Shot("dev_judge");
                        Then(() =>
                        {
                            dev.Confirm();
                            DevMode.TestOverride = null;
                            Check(!IsOn("JudgePanel/DevJudge"), "결과 보기를 누르면 창이 닫힌다");
                        });
                    }, () => { PassGive(); return IsOn("JudgePanel/DevJudge"); });
                Add(0.3f, $"{st.Title} 성공", () =>
                {
                    Check(Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.originalSprite, "진짜 물건이 왔다");
                    if (s == 0) Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_NoBat", "1단계를 맞히면 도깨비는 빈손으로 방망이를 내민다");
                    else Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Swing", "성공하면 방망이를 휘두른다");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_DokkaebiFace_Proud", "맞히면 도깨비는 뿌듯한 얼굴");
                    CheckOneBat("결과 : 도깨비 대사");
                    TapResult();
                    if (s == 0) Check(Find("ResultPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Normal"
                                      && !Find("ResultPanel/ReturnedItem").GetComponent<Image>().enabled, "1단계를 맞히면 주인공이 말할 때 도깨비가 방망이를 집어 든다");
                    CheckOneBat("결과 : 주인공 대사");
                    Check(!IsOn("ResultPanel/ChoiceRow"), "성공하면 다시 · 포기 버튼이 없다");
                    Check(Find("ResultPanel/DialogueBox/Face").GetComponent<Image>().sprite.name == "Char_BoyFace_Happy", "성공하면 기쁜 표정");
                    Shot($"s{s + 1}_result_success");
                    Then(() =>
                    {
                        Gm().ResultTap();
                        Gm().ResultTap();
                        Check(IsOn("ComparePanel"), "대사를 누르면 채점표로 넘어간다");
                    });
                }, () => ResultShown() && Find("ResultPanel/ReturnedItem").GetComponent<Image>().sprite == st.originalSprite);
            }

            // 채점표 → 다음 단계
            Add(1.5f, $"{st.Title} 채점표", () =>
            {
                Check(IsOn("ComparePanel"), "채점표");
                int marks = 0;
                for (int k = 1; k <= 5; k++)
                    if (IsOn($"ComparePanel/Paper/Row_{k}"))
                        marks += IsOn($"ComparePanel/Paper/Row_{k}/Circle") != IsOn($"ComparePanel/Paper/Row_{k}/Slash") ? 1 : 0;
                Check(marks == st.features.Length, "번호마다 동그라미 또는 빗금");
                Check(Find("ComparePanel/Paper/OriginalCard").GetComponent<Image>() == null
                      && Find("ComparePanel/Paper/OriginalCard/Original").GetComponent<Image>().sprite == st.originalSprite,
                      "원본은 흰 카드 없이 쪽지 위에 누끼로");
                if (s == 1) Check(!IsOn("ComparePanel/Paper/Row_1/Circle") && IsOn("ComparePanel/Paper/Row_1/Slash"), "틀리면 빨간 빗금");
                else Check(IsOn("ComparePanel/Paper/Row_1/Circle"), "맞으면 초록 동그라미");
                Check(!(bool)typeof(GameManager).GetField("freePeek", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()), "단계가 끝나면 무료 기회는 사라진다");
                if (s <= 1 || last) Shot($"s{s + 1}_compare");
                if (s <= 1 || last) Then(() => Gm().NextStage());
                else Gm().NextStage();
            });
        }

        // 이야기 종료
        group = "ending";
        Add(3.2f, "이야기 종료", () =>
        {
            Check(IsOn("EndingPanel") && !IsOn("HUD"), "이야기 종료 화면");
            Check(Find("Background").GetComponent<Image>().sprite != null, "소풍 배경");
            Check(Find("EndingPanel/BagSlot").GetComponent<Image>().sprite == stages[1].AllWrongSprite, "포기한 2번째 물건은 받은 엉뚱한 물건이 놓인다");
            Check(Find("EndingPanel/MatSlot").GetComponent<Image>().sprite == stages[2].originalSprite, "찾은 물건은 진짜가 놓인다");
            Check(Find("EndingPanel/Dokkaebi").GetComponent<Image>().sprite.name == "Char_Dokkaebi_Normal" && !IsOn("EndingPanel/BatSlot"),
                  "방망이를 찾았으면 도깨비 손에만 있고 돗자리 방망이 자리는 비어 있다");
            var jar = Find("EndingPanel/CandyJar").GetComponent<CandyJar>();
            Check(IsOn("EndingPanel/CandyJar") && jar.Shown == GetInt("jarCandies") && jar.Shown > 0,
                  "사탕 통에 물건마다 남은 사탕이 모여 있다 (11/23 다시 넣음)");
            Check(Progress.IsCleared(1), "끝까지 하면 1챕터를 깬 것으로 저장된다");
            Shot("ending");
            Then(() =>
            {
                Gm().EndingTap();
                Check(IsOn("EndingPanel/DialogueBox") && Text("EndingPanel/DialogueBox/Line") == GameScript.EndingBye, "화면을 누르면 도깨비가 다음에 또 온다고 인사한다");
                RectTransform box = (RectTransform)Find("EndingPanel/DialogueBox"), mat = (RectTransform)Find("EndingPanel/MatSlot");
                Check(box.position.y > mat.position.y + 200f, "인사 대사 상자는 위쪽 하늘에 떠서 돗자리를 가리지 않는다");
                Shot("ending_ask");
                // 인사 대사를 한 번 더 누르면 로비 씬을 불러서 검사가 끝나 버려요. 거기까지는 누르지 않아요
            });
        });

        // 1단계를 못 찾으면 이야기가 끝날 때까지 도깨비가 빈손이고, 소풍 장면에 대파가 놓여요
        Add(0.3f, "방망이를 못 찾음", () =>
        {
            Sprite Shown(string path) => Find(path).GetComponent<Image>().sprite;
            const string NoBat = "Char_Dokkaebi_NoBat";
            Gm().StartGame();
            Invoke(Gm(), "ShowResult", new object[] { new[] { false }, null });
            Gm().ShowCompare();
            Gm().NextStage();
            Check(IsOn("DialoguePanel"), "채점표 다음은 바로 2단계 대화");
            Check(Shown("DialoguePanel/Dokkaebi/Image").name == NoBat, "방망이를 못 찾으면 2단계 대화에서 빈손");
            Invoke(Gm(), "ShowResult", new object[] { new[] { true }, null });
            Check(Shown("ResultPanel/Dokkaebi").name == NoBat, "다음 단계를 맞혀도 빈손");
            TapResult();
            Check(Shown("ResultPanel/Dokkaebi").name == NoBat, "주인공이 말할 때도 빈손");
            Gm().ShowCompare();
            SetPrivate(Gm(), "stageIndex", stages.Length - 1);
            Gm().NextStage();
            Check(IsOn("EndingPanel") && Shown("EndingPanel/Dokkaebi").name == NoBat, "이야기 종료에서도 빈손");
            Check(Shown("EndingPanel/BatSlot") == stages[0].AllWrongSprite, "소풍 장면에는 대파가 놓인다");
            Check(Progress.IsCleared(1) && Progress.BatKnown && !Progress.BatFound, "빈손으로 끝나도 1챕터는 깬 것으로, 방망이는 못 찾은 것으로 저장된다");
        });
    }
}

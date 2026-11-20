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

// MvpSelfTest 의 도우미 (그리기 · 화면 찾기 · 확인 · 가짜 손그림) 이에요. 실행 방법과 그룹 설명은 MvpSelfTest.cs 에 있어요.
static partial class MvpSelfTest
{
    // 그리기 : 검정 테두리 (+ colors 면 빨강 · 노랑 · 파랑 선). mix 가 true 면 팔레트로 색도 섞어 봐요 (3챕터부터 섞기 펜)
    static void DrawStage(string tag, bool mix, bool colors = true)
    {
        ToolBar tb = Find("DrawPanel/ToolBar").GetComponent<ToolBar>();
        tb.SelectSize(1);
        tb.SelectColor(0);
        Stroke(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0.8f, 0.8f), new Vector2(0.2f, 0.8f), new Vector2(0.2f, 0.2f));

        if (colors)
        {
            tb.SelectSize(2);
            tb.SelectColor(1); Stroke(new Vector2(0.3f, 0.7f), new Vector2(0.45f, 0.7f));
            tb.SelectColor(2); Stroke(new Vector2(0.55f, 0.7f), new Vector2(0.7f, 0.7f));
            tb.SelectColor(3); Stroke(new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.35f));
        }
        if (mix)
        {
            ColorMixer mixer = Find("DrawPanel/ToolBar").GetComponent<ColorMixer>();
            tb.OpenPanel();
            Check(IsOn("DrawPanel/ToolBar/Palette"), "붓을 누르면 팔레트가 열린다");
            {
                tb.PaintTap(0);
                Check(BrushTip() != mixer.ResultColor, "물감을 넣어도 붓 끝 색이 한 번에 바뀌지 않는다");     // 붓 끝은 검정, 넣은 색은 빨강
                tb.PaintTap(1); tb.PaintTap(2);
                Check(mixer.ResultColor == ColorMixer.Mix(1, 1, 1), "자유 혼합 : 세 색을 한 번씩 넣으면 검은색에 가깝다");
                Check(Find("DrawPanel/ToolBar/PaintDrop") != null, "물감을 누르면 물방울이 붓 끝으로 날아간다");
                tb.PaintTap(0); tb.PaintTap(1);
                Check(mixer.ResultColor == ColorMixer.Mix(2, 2, 1), "자유 혼합 : 2:2:1 로 넣으면 갈색");
                mixer.SkipAnimation();
                tb.PaintUndo();
                Check(mixer.ResultColor == ColorMixer.Mix(2, 1, 1), "물감 빼기 : 마지막에 넣은 물감이 빠진다");
                Check(mixer.DropCount > 0, "물감 빼기 : 물방울이 물감으로 돌아간다");
                Check(BrushTip() != mixer.ResultColor, "물감 빼기 : 색이 한 번에 바뀌지 않는다");
                tb.PaintTap(1);
            }
            mixer.SkipAnimation();
            Check(BrushTip() == mixer.ResultColor, "움직임이 끝나면 붓 끝이 만든 색");
            Check(Find($"DrawPanel/ToolBar/Palette/Card/Size_{tb.SelectedSize}/Dot").GetComponent<Image>().color == mixer.ResultColor, "고른 굵기 점도 만든 색");
            // 그리기가 막 시작돼서 간식이 아직 날아 들어오는 중일 수 있어요. 날아오는 사탕끼리 잠깐 겹친 걸
            // 배치 문제로 잡지 않게, 간식 움직임을 끝낸(제자리에 둔) 뒤 검사해요.
            foreach (SimpleAnim a in (SimpleAnim[])typeof(GameManager).GetField("snackAnims", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm()))
                a.enabled = false;
            LayoutOnly($"{tag}_mixer");                     // 팔레트가 열린 화면도 배치 검사
            Color mixed = mixer.ResultColor;
            Stroke(new Vector2(0.35f, 0.6f), new Vector2(0.65f, 0.6f));
            Check(!IsOn("DrawPanel/ToolBar/Palette"), "그리기 시작하면 팔레트가 닫힌다");
            tb.OpenPanel();
            tb.PaintTap(2);
            Check(mixer.ResultColor == ColorMixer.Mix(0, 0, 1) && mixer.CountOf(0) == 0, "섞은 색으로 그린 뒤 물감을 누르면 새로 섞기");
            tb.PaintUndo();
            Check(mixer.ResultColor == ToolBar.Palette[0] && mixer.PureIndex == 0, "물감을 다 빼면 검정");
            {
                Check(!IsOn("DrawPanel/ToolBar/Palette/Card/Color_검정") && !IsOn("DrawPanel/ToolBar/Palette/Card/Divider_1"), "세 가지 섞기 : 검정 물감이 없다");
                tb.PaintTap(0); tb.PaintTap(1); tb.PaintTap(2);
                Check(mixer.ResultColor == ToolBar.Palette[0], "세 가지 섞기 : 빨강 · 노랑 · 파랑을 똑같이 넣으면 검정");
                tb.PaintUndo(); tb.PaintUndo(); tb.PaintUndo();
            }
            mixer.SkipAnimation();
            tb.ClosePanel();
            Check(mixed != Color.black, "섞은 색이 검정과 다르다");
            var gmPause = Object.FindFirstObjectByType<GameManager>();
            gmPause.ToggleSettings();                                       // 소리 설정 창도 배치 검사
            Check(IsOn("SettingsPanel") && Time.timeScale == 0f, "그리는 중 톱니를 누르면 소리 설정 창이 열리고 향이 멈춘다");
            LayoutOnly($"{tag}_settings");
            gmPause.ToggleSettings();
            Check(!IsOn("SettingsPanel") && Time.timeScale == 1f, "톱니를 다시 누르면 닫히고 다시 진행");
            Stroke(new Vector2(0.35f, 0.5f), new Vector2(0.65f, 0.5f));
        }
    }

    // ───────────── 도우미 ─────────────

    // 붓 끝의 지금 색
    static Color BrushTip() => Find("DrawPanel/ToolBar/Brush/Tip").GetComponent<UnityEngine.UI.Image>().color;

    static GameManager Gm() => Object.FindFirstObjectByType<GameManager>();

    // 결과 화면에서 도깨비 대사를 넘겨 주인공 대사까지 (글자 다 보이기 + 다음)
    static void TapResult()
    {
        Gm().ResultTap();
        Gm().ResultTap();
    }

    // 에디터에서 GameManager 에 연결된 챕터 데이터 중 number 번 (Play 중 챕터 값에 기대지 않아요)
    internal static ChapterData ChapterOf(int number)
    {
        var so = new SerializedObject(Gm());
        SerializedProperty p = so.FindProperty("chapters");
        for (int i = 0; i < p.arraySize; i++)
        {
            var c = p.GetArrayElementAtIndex(i).objectReferenceValue as ChapterData;
            if (c != null && c.number == number) return c;
        }
        return null;
    }

    // 1챕터의 물건 5개 (방망이, 가방, 돗자리, 김밥, 친구)
    internal static StageData[] Stages() => ChapterOf(1).items;

    // Canvas 아래에서 이름 경로로 찾기 (꺼져 있는 오브젝트도 찾을 수 있어요)
    //   게임 화면들은 Canvas/Frame(시안 크기 화면) 안에, 시작·일시정지 화면은 Canvas 바로 아래에 있어요.
    internal static Transform Find(string path)
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        Transform t = canvas.Find("Frame/" + path) ?? canvas.Find(path);
        if (t == null) throw new Exception("오브젝트 없음: " + path);
        return t;
    }

    // 없으면 null (예외 없이)
    internal static Transform TryFind(string path)
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        return canvas.Find("Frame/" + path) ?? canvas.Find(path);
    }

    internal static bool IsOn(string path) => Find(path).gameObject.activeInHierarchy;
    static string Text(string path) => Find(path).GetComponent<TMP_Text>().text;
    // 대사 상자 글 (줄바꿈용 <nobr> 같은 태그를 뺀 글)
    static string Plain(string path) => System.Text.RegularExpressions.Regex.Replace(Text(path), "<[^>]*>", "");
    static DrawingBoard Board() => Find("DrawPanel/Desk/Paper/Board").GetComponent<DrawingBoard>();


    static bool GetBool(string field) =>
        (bool)typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());

    static int GetChapterNumber()
    {
        var c = (ChapterData)typeof(GameManager).GetField("chapter", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());
        return c == null ? 0 : c.number;
    }

    // 고르기 화면이 나올 때까지 대사 넘기기
    static void TapToChoose()
    {
        for (int k = 0; k < 80 && !IsOn("ChoosePanel"); k++) Gm().DialogueTap();
    }

    // 고르기 화면에서 맞설 물건이 있는 쪽 (0 왼쪽, 1 오른쪽)
    static int CounterSide() => GetBool("leftIsCounter") ? 0 : 1;

    // 또렷하게 보이는 하트 수
    static int LitHearts() => Lit("HUD/HeartGroup");

    // 또렷하게 보이는 사탕 수 = 남은 수
    static int CandiesLit() => Lit("HUD/CandyRow");
    static int Lit(string row)
    {
        int lit = 0;
        foreach (Image icon in Find(row).GetComponentsInChildren<Image>())
            if (icon.color.a > 0.9f) lit++;
        return lit;
    }

    static int SumInts(string field)
    {
        int sum = 0;
        foreach (int v in (int[])typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm())) sum += v;
        return sum;
    }

    static int GetInt(string field) =>
        (int)typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());

    static float GetFloat(string field) =>
        (float)typeof(GameManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Gm());

    // 이름이 prefix1, prefix2 … 인 것 중 켜져 있는 수 (이야기 종료의 남은 간식)
    static int ShownCount(string prefix)
    {
        int n = 0;
        for (int k = 1; k <= 15; k++) if (IsOn(prefix + k)) n++;
        return n;
    }
    static int Count(string text, string word) => text.Split(new[] { word }, StringSplitOptions.None).Length - 1;

    // 다음 판정 결과를 미리 정해 두기. miss = 빠뜨릴 특징 번호 (-1 = 전부 빠뜨림, -2 = 전부 그림)
    internal static void Expect(StageData stage, int miss)
    {
        LocalJudge.TestOverride = st =>
        {
            int n = st.features.Length;
            var v = new OpenAIJudge.Verdict { present = new bool[n], notes = new string[n] };
            for (int i = 0; i < n; i++) v.present[i] = miss == -2 || (miss >= 0 && i != miss);
            return v;
        };
    }

    // 손그림 판정 확인. miss = 빠져야 하는 특징 번호 (-1 = 전부 그림이어야 함)
    static void ExpectJudge(StageData stage, Texture2D tex, int miss, string what) =>
        ExpectJudgeMiss(stage, tex, miss < 0 ? new int[0] : new[] { miss }, what);

    // 빠뜨렸다고 나와야 하는 특징이 여러 개일 때 (예: 띠가 없으면 띠 색도 없음)
    static void ExpectJudgeMiss(StageData stage, Texture2D tex, int[] miss, string what)
    {
        LocalJudge.TestOverride = null;
        OpenAIJudge.Verdict v = LocalJudge.Judge(stage, tex);
        bool ok = true;
        string detail = "";
        for (int i = 0; i < v.present.Length; i++)
        {
            bool want = System.Array.IndexOf(miss, i) < 0;
            if (v.present[i] != want) { ok = false; detail += $" {stage.features[i].id}({v.notes[i]})"; }
        }
        Check(ok, $"손그림 판정 : {what}" + (ok ? "" : " / 틀린 것:" + detail));
    }

    // 손그림은 HandSketch.cs 로 만들어요 (판정 전수 검사와 같이 씀)
    internal static readonly Color32 InkBlack = HandSketch.Black;

    static Color32 C(int i) => ToolBar.Palette[i];          // 0 검정, 1 빨강, 2 노랑, 3 파랑
    internal static Vector2 V(float x, float y) => new Vector2(x, y);

    internal static Texture2D HandBat(bool spikes)
    {
        var s = new HandSketch()
            .Line(InkBlack, 4, 2f, V(248, 40), V(252, 140), V(256, 246))               // 한 줄로 그은 손잡이
            .Ellipse(InkBlack, 4, V(256, 335), 58, 88, gapDeg: 14f);                  // 살짝 안 닫힌 타원 머리
        if (spikes)
        {
            foreach (float a in new[] { 25f, 70f, 115f, 160f })
            {
                Vector2 dir = V(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Vector2 edge = V(256, 335) + new Vector2(dir.x * 58, dir.y * 88);
                s.Line(InkBlack, 4, 1f, edge, edge + dir * 55);                       // 선으로 그은 돌기
            }
        }
        return s.Tex();
    }

    // 시안 그림처럼 동그라미 여러 개를 이어 그린 구름 머리
    internal static Texture2D HandCloudBat()
    {
        var s = new HandSketch().Line(InkBlack, 4, 2f, V(248, 40), V(252, 140), V(256, 246));
        foreach (float a in new[] { 20f, 75f, 130f, 185f, 250f, 330f })
        {
            Vector2 c = V(256, 330) + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 58;
            s.Ellipse(InkBlack, 4, c, 38, 38);
        }
        return s.Tex();
    }

    internal static Texture2D HandBag(bool stripes = true)
    {
        var s = new HandSketch()
            .Line(InkBlack, 4, 2f, V(100, 60), V(412, 64), V(408, 330), V(104, 326), V(100, 60))   // 몸체
            .Line(InkBlack, 4, 2f, V(190, 328), V(200, 400), V(256, 440), V(312, 400), V(322, 328)) // 손잡이
            .Line(InkBlack, 4, 2f, V(170, 90), V(342, 92), V(340, 210), V(168, 206), V(170, 90));  // 주머니
        if (stripes)
            s.Line(InkBlack, 3, 1.5f, V(190, 120), V(322, 124))
             .Line(InkBlack, 3, 1.5f, V(188, 150), V(320, 152))
             .Line(InkBlack, 3, 1.5f, V(190, 180), V(322, 178));                                   // 줄무늬 3줄
        return s.Tex();
    }

    internal static Texture2D HandMat(bool bands, bool endsBlue = true)
    {
        var s = new HandSketch();
        if (bands)
        {
            s.Scribble(C(3), 6, 85, 325, 430, 372, 16);     // 맨 위 파랑 (성기게)
            s.Scribble(C(1), 6, 85, 268, 430, 315, 16);     // 빨강
            s.Scribble(C(2), 6, 85, 211, 430, 258, 16);     // 노랑
            s.Scribble(C(endsBlue ? 3 : 1), 6, 85, 158, 430, 201, 16);    // 맨 아래 파랑 (endsBlue 가 false 면 빨강)
        }
        else s.Scribble(C(3), 6, 85, 170, 430, 370, 16);    // 띠 없이 파랑 한 가지
        s.Line(InkBlack, 3, 3f, V(70, 150), V(440, 145), V(445, 385), V(65, 390), V(70, 150)); // 외곽
        return s.Tex();
    }

    internal static Texture2D HandGimbap(bool green = true)
    {
        Color32 mixedGreen = new Color32(40, 160, 60, 255);  // 노랑 + 파랑 으로 섞은 초록
        var s = new HandSketch()
            .Ellipse(InkBlack, 9, V(256, 256), 200, 196, gapDeg: 8f)
            .Scribble(C(1), 6, 160, 280, 240, 340, 14)
            .Scribble(C(2), 6, 280, 280, 360, 340, 14);
        s.Scribble(green ? mixedGreen : C(3), 6, 210, 170, 310, 230, 14);   // 안 섞으면 파랑
        return s.Tex();
    }

    internal static Texture2D HandFriend(bool brownHair = true)
    {
        Color32 orange = new Color32(245, 140, 30, 255), brown = new Color32(130, 80, 40, 255);   // 섞어서 만든 주황 · 갈색
        return new HandSketch()
            .Ellipse(InkBlack, 4, V(256, 400), 55, 55)                                     // 머리
            .Ellipse(InkBlack, 4, V(178, 395), 22, 28)
            .Ellipse(InkBlack, 4, V(334, 395), 22, 28)                                     // 양 갈래
            .Scribble(brownHair ? brown : orange, 8, 200, 405, 312, 458, 9)                // 머리카락
            .Scribble(orange, 8, 165, 225, 347, 335, 9)                                    // 주황 상의 (꼼꼼히)
            .Scribble(orange, 8, 110, 250, 165, 320, 9)
            .Scribble(orange, 8, 347, 250, 402, 320, 9)                                    // 팔
            .Scribble(C(3), 8, 175, 70, 235, 215, 9)
            .Scribble(C(3), 8, 280, 70, 340, 215, 9)                                       // 두 다리
            .Line(InkBlack, 5, 1.5f, V(240, 175), V(272, 175), V(272, 205), V(240, 205), V(240, 175))   // 천 조각
            .Tex();
    }

    // 1챕터 물건 (stageIndex 0 방망이 ~ 4 친구) 의 형태 판정용 손그림. shape 가 true 면 그 물건의 형태를 그리고,
    // false 면 형태가 아닌 낙서를 그려요 (MvpJudgeCases 가 써요)
    internal static Texture2D ShapeSketch(int stageIndex, bool shape)
    {
        if (!shape)
            return new HandSketch().Line(InkBlack, 4, 3f, V(120, 300), V(200, 250), V(260, 310), V(330, 240), V(400, 290)).Tex();
        switch (stageIndex)
        {
            case 0: return HandBat(spikes: true);
            case 1: return HandBag();
            case 2: return HandMat(bands: true);
            case 3: return HandGimbap();
            default: return HandFriend();
        }
    }

    // Sprite 원본 PNG 를 픽셀을 읽을 수 있는 텍스처로 불러오기 (자동 판정 검사용)
    static Texture2D LoadReadable(Sprite sprite)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        return tex;
    }

    // 흐름 검사에서 제출할 손그림 : allDrawn = 특징 전부, 아니면 그 단계 첫 제출의 기대 결과대로
    //   1 방망이 돌기 없음 · 2 가방 특징 없는 낙서 · 3 돗자리 맨 아래 띠 빨강 · 4 김밥 시금치가 파랑 · 5 친구 머리가 주황
    static void PutSketch(StageData st, bool allDrawn)
    {
        Texture2D tex;
        switch (st.stageNumber)
        {
            case 1: tex = allDrawn ? HandCloudBat() : HandBat(spikes: false); break;
            case 2: tex = allDrawn ? HandBag() : new HandSketch().Line(InkBlack, 4, 3f, V(120, 300), V(200, 250), V(260, 310), V(330, 240), V(400, 290)).Tex(); break;
            case 3: tex = HandMat(bands: true, endsBlue: allDrawn); break;
            case 4: tex = HandGimbap(green: allDrawn); break;
            default: tex = HandFriend(brownHair: allDrawn); break;
        }
        LoadSketch(tex);
    }

    // 손그림을 쪽지에 올려요 (흐름 사진 도구도 같이 써요)
    internal static void LoadSketch(Texture2D tex)
    {
        Color32[] px = tex.GetPixels32();
        var blank = new Color32(255, 255, 255, 0);                   // 손그림의 흰 종이 → 투명 (종이 그림은 뒤에 따로 깔려 있어요)
        for (int i = 0; i < px.Length; i++)
            if (px[i].r == 255 && px[i].g == 255 && px[i].b == 255) px[i] = blank;
        Board().LoadPixels(px);
    }

    // 그림판 비율 좌표(0~1)의 픽셀에 잉크가 있는지 (투명하지 않으면 잉크)
    static bool Ink(float u, float v)
    {
        Texture2D t = Board().Texture;
        Color32 c = t.GetPixel(Mathf.Clamp((int)(u * t.width), 0, t.width - 1), Mathf.Clamp((int)(v * t.height), 0, t.height - 1));
        return c.a >= 128;
    }

    // 그림판 위 비율 좌표(0~1)를 따라 가짜 드래그 (0~1 밖이면 그림판 밖)
    static void Stroke(params Vector2[] uv)
    {
        DrawingBoard board = Board();
        var rt = (RectTransform)board.transform;
        Rect r = rt.rect;   // 쪽지가 돌아가거나 커져 있어도 맞게 (Overlay Canvas 는 월드 좌표 = 화면 좌표)
        Vector2 Pt(Vector2 p) => rt.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, p.x), Mathf.Lerp(r.yMin, r.yMax, p.y), 0f));

        var e = new PointerEventData(EventSystem.current) { position = Pt(uv[0]) };
        board.OnPointerDown(e);
        for (int i = 1; i < uv.Length; i++)
        {
            // 중간 점을 촘촘히 보내서 진짜 드래그처럼
            for (int k = 1; k <= 10; k++)
            {
                e.position = Pt(Vector2.Lerp(uv[i - 1], uv[i], k / 10f));
                board.OnDrag(e);
            }
        }
        board.OnPointerUp(e);
    }

    // private 함수·변수 건드리기 (테스트에서만)
    internal static void Invoke(object target, string method, params object[] args)
    {
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    }

    internal static void SetPrivate(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    static void Check(bool ok, string what)
    {
        report.Add((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failCount++;
    }

    // 참고용 결과 (실패로 세지 않아요)
    static void Info(bool ok, string what)
    {
        report.Add((ok ? "  INFO  O " : "  INFO  X ") + what);
    }

    static void Fail(string what)
    {
        report.Add("  FAIL  " + what);
        failCount++;
    }

    // Game 창 스크린샷 : 게임 프레임이 끝나는 순간에 찍는 도우미를 잠깐 붙여요.
    // (ScreenCapture.CaptureScreenshot 은 에디터에서 "다음에 그려지는 화면"을 저장해서 한 장씩 밀렸어요)
    // 스크린샷 없이 배치 검사만 (잠깐 열렸다 닫히는 화면)
    static void LayoutOnly(string name)
    {
        List<string> layout = MvpLayoutCheck.Check(name);
        Check(layout.Count == 0, $"배치 검사 : {name}" + (layout.Count == 0 ? " (겹침 · 넘침 없음)" : $" 문제 {layout.Count}개"));
        foreach (string line in layout) report.Add("        " + line);
    }

    // 방망이는 한 화면에 하나만 보여야 해요 (도깨비가 든 방망이 + 바닥 · 돗자리의 방망이 그림을 함께 세요).
    //   채점표는 원본을 옆에 놓고 비교하는 종이라서 세지 않아요.
    static void CheckOneBat(string where)
    {
        Sprite batItem = Stages()[0].originalSprite;
        Transform compare = Find("ComparePanel");
        int count = 0;
        foreach (Image img in GameObject.Find("Canvas").GetComponentsInChildren<Image>())   // 켜진 것만 찾아요
        {
            if (!img.enabled || img.sprite == null || img.transform.IsChildOf(compare)) continue;
            string n = img.sprite.name;
            if (img.sprite == batItem || n == "Char_Dokkaebi_Normal" || n == "Char_Dokkaebi_Swing") count++;
        }
        Check(count <= 1, $"방망이는 한 화면에 하나만 : {where}" + (count <= 1 ? "" : $" ({count}개 보임)"));
    }

    static void Shot(string name)
    {
        CheckOneBat(name);
        // 화면 배치 숫자 검사 (걸쳐 겹침 · 글자 겹침 · 글자 넘침 · 화면 밖)
        List<string> layout = MvpLayoutCheck.Check(name);
        Check(layout.Count == 0, $"배치 검사 : {name}" + (layout.Count == 0 ? " (겹침 · 넘침 없음)" : $" 문제 {layout.Count}개"));
        foreach (string line in layout) report.Add("        " + line);
        shotCount++;
        var helper = new GameObject("SelfTestShot").AddComponent<ShotHelper>();
        helper.path = Path.Combine(OutDir, $"{shotCount:00}_{name}.png");
    }

    class ShotHelper : MonoBehaviour
    {
        public string path;

        System.Collections.IEnumerator Start()
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) { Destroy(gameObject); yield break; } // Game 창이 안 그려지는 순간이면 건너뜀
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Destroy(gameObject);
        }
    }
}

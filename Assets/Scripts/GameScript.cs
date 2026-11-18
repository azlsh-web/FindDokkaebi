using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// GameScript : 게임 대사 모음
// ─────────────────────────────────────────────────────────────
// 대사를 고칠 때는 이 파일만 고치면 돼요. 기준은 Docs/전달/14_대사_수정표_바뀐내용.xlsx 의 대사 탭이에요.
// 특징 이름은 결과 대사에 넣지 않아요 (정답 힌트가 되니까요). 판정 기준은 시작 대화의 생김새 대사로 알려 줘요.
public static class GameScript
{
    public enum Face { BoySurprised, BoyHappy, Dokkaebi, BoySad, BoyNormal }   // BoyNormal : 물건 생김새를 차분히 설명할 때

    public struct Line
    {
        public Face face;
        public string text;
        public Line(Face face, string text) { this.face = face; this.text = text; }
    }

    // 오프닝 : 도깨비가 나오기 전에 주인공 혼자 말하는 줄 수. 이 대사들은 그림과 함께 나오고, 터치하면 넘어가요.
    public const int OpeningAloneLines = 4;

    // 단계별 시작 대화. 1단계는 게임 전체의 오프닝이에요.
    static readonly Line[][] Intro =
    {
        new[]
        {
            new Line(Face.BoySurprised, "오늘 소풍 가는 날인데… 어? 내 가방이 어디 갔지?"),
            new Line(Face.BoySurprised, "돗자리도, 김밥도 없어! 또 깜빡하고 어디 뒀나 봐…"),
            new Line(Face.BoyHappy, "아! 쪽지에 그림을 그려서 간식이랑 같이 두면 도깨비가 찾아 준댔어!"),
            new Line(Face.BoyHappy, "도깨비야, 도깨비야! 어디 있니?"),
            new Line(Face.Dokkaebi, "방금 누가 내 이름 불렀어?"),
            new Line(Face.BoySurprised, "와, 진짜 도깨비다! 내 소풍 준비물 좀 찾아 줘!"),
            new Line(Face.Dokkaebi, "찾아 줄 수는 있지. 대신 달달한 간식을 줘야 해."),
            new Line(Face.Dokkaebi, "그런데 도깨비로서 좀 창피한 얘기지만… 나도 방망이를 잃어버렸거든."),
            new Line(Face.Dokkaebi, "방망이가 없으니까 영 힘이 안 나."),
            new Line(Face.Dokkaebi, "내 방망이부터 찾아 주면, 네 물건도 전부 찾아 줄게."),
            new Line(Face.BoyHappy, "좋아! 어떻게 생겼는데? 내가 쪽지에 그려 줄게!"),
            new Line(Face.Dokkaebi, "음… 머리가 굵고, 돌기가 나 있었어."),
        },
        new[]
        {
            new Line(Face.BoyHappy, "먼저 소풍 준비물을 담을 가방부터 찾아 줘!"),
            new Line(Face.Dokkaebi, "가방이라… 어떤 가방인데?"),
            new Line(Face.BoyNormal, "내가 제일 아끼는 가방이야. 네모난 몸체에 위쪽 손잡이, 앞주머니가 달렸어."),
            new Line(Face.BoyNormal, "주머니 안에는 가로 줄무늬도 있어!"),
            new Line(Face.Dokkaebi, "네모난 몸체에 손잡이랑 앞주머니. 그 모양을 기억해 둘게!"),
            new Line(Face.Dokkaebi, "좋아! 네가 떠올린 그대로 쪽지에 그려 줘."),
        },
        new[]
        {
            new Line(Face.BoyNormal, "돗자리도 분명히 챙겼는데… 또 어디 뒀더라?"),
            new Line(Face.Dokkaebi, "너는 어쩜 그렇게 다 잃어버리니? 어떤 돗자리인데?"),
            new Line(Face.BoyNormal, "네모난 모양에, 띠가 네 줄 있어."),
            new Line(Face.BoyNormal, "펼치면 모서리가 네 개야."),
            new Line(Face.Dokkaebi, "네모난 바깥 모양이 보이면 찾을 수 있겠어!"),
            new Line(Face.Dokkaebi, "좋아! 떠올린 그대로 쪽지에 그려 줘."),
        },
        new[]
        {
            new Line(Face.BoyHappy, "아차! 김밥도 어디 뒀는지 깜빡했어!"),
            new Line(Face.Dokkaebi, "김밥? 그건 나도 먹고 싶은데! 어떻게 생겼어?"),
            new Line(Face.BoyNormal, "까만 김이 둥글게 말려 있고, 안에 재료가 위에 둘, 아래에 하나 있어."),
            new Line(Face.BoyNormal, "노란 단무지랑 초록 시금치도 들어 있어!"),
            new Line(Face.Dokkaebi, "동그랗게 말린 김 테두리를 그려 주면 찾을 수 있겠어!"),
            new Line(Face.Dokkaebi, "좋아, 쪽지에 그려 줘."),
        },
        new[]
        {
            new Line(Face.BoySurprised, "어? 방금까지 옆에 있던 친구가 없어졌어!"),
            new Line(Face.Dokkaebi, "물건도 모자라서 이번엔 친구까지 잃어버린 거야? 정말 못 말린다."),
            new Line(Face.BoySurprised, "같이 소풍 가기로 했단 말이야! 꼭 찾아 줘."),
            new Line(Face.Dokkaebi, "어떻게 생긴 친구야?"),
            new Line(Face.BoyNormal, "양 갈래로 묶은 갈색 머리야."),
            new Line(Face.BoyNormal, "파란 멜빵바지에 주황 상의를 입었고, 주머니에 천 조각이 덧대져 있어."),
            new Line(Face.Dokkaebi, "머리 양쪽에 한 갈래씩! 그 모양을 기억해 둘게."),
            new Line(Face.Dokkaebi, "걱정 마, 쪽지에 그려 주면 데려올게."),
        },
    };

    // 2단계 시작 대화 맨 앞 : 1단계에서 방망이를 찾았는지에 따라 달라요
    static readonly Line BatFound = new Line(Face.Dokkaebi, "방망이도 찾았으니 이제 내 차례지! 네 물건도 찾아 줄게.");
    static readonly Line BatLost = new Line(Face.Dokkaebi, "방망이가 없어서 힘은 안 나지만… 간식도 받았으니 네 물건을 찾아 볼게.");

    // 쪽지 종이에 적히는 글 (대사 상자가 아니에요)
    static readonly string[] Note =
    {
        "도깨비야, 네 방망이 여기 그렸어!",
        "도깨비야, 이 가방 찾아 줘!",
        "도깨비야, 이 돗자리 찾아 줘!",
        "도깨비야, 이 김밥 찾아 줘!",
        "도깨비야, 내 친구 좀 찾아 줘!",
    };

    // 쪽지와 간식을 건넬 때 : 주인공 → 도깨비 → 도깨비
    public const string GiveBoy = "도깨비야, 여기 쪽지랑 간식!";
    public const string GiveEat = "냠냠, 달달하다! 쪽지 좀 볼까…";
    static readonly string[] GiveRead =
    {
        "음, 내 방망이구나. 좋아, 찾아올게!",
        "음, 이 가방이구나. 좋아, 찾아올게!",
        "음, 이 돗자리구나. 좋아, 찾아올게!",
        "음, 이 김밥이구나. 좋아, 찾아올게!",
        "음, 이 친구구나. 좋아, 데려올게!",
    };

    // 결과 대사 : [단계 - 1] 별 전부 맞음 / 일부 틀림 / 전부 틀림
    static readonly string[][] DokkaebiSuccess =
    {
        new[] { "내 방망이다! 얼마나 보고 싶었다고!", "이 묵직한 손맛! 역시 내 방망이야!" },
        new[] { "가방 여기 있다! 어때, 나 잘 찾지?", "쪽지 보고 한 번에 찾았어!" },
        new[] { "돗자리 찾았다! 앉으면 딱이겠어.", "쪽지가 또렷해서 금방 찾았어!" },
        new[] { "김밥 찾았다! 한 줄만 나 주면 안 돼?", "쪽지 보니까 배고프더라. 바로 찾았지!" },
        new[] { "네 친구 찾았다! 저쪽에서 울고 있더라고.", "데려왔어! 너를 한참 찾고 있었대." },
    };

    // 소풍은 기준이 하나라 일부 틀림이 없어요
    static readonly string[][] DokkaebiPartial =
    {
        new[] { "이게 내 방망이라고? 내 방망이는 이렇게 안 생겼어!", "내 방망이를 이렇게 그리면 어떡해! 나 좀 서운해." },
        new[] { "네가 이렇게 그렸잖아~ 난 그대로 찾아왔다고!", "쪽지대로 찾았는데? 네 그림이 그런걸~" },
        new[] { "그림이 이랬는걸? 난 잘못 없어~", "흠, 네 쪽지에 있던 그대로야!" },
        new[] { "네가 그린 김밥이 이렇던데? 맛은 장담 못 해~", "그림대로 찾아왔어. 먹을 수는 있겠지?" },
        new[] { "이 친구 맞지? 네가 그린 대로인데~", "어라, 네 친구가 이렇게 생긴 거 아니야?" },
    };

    static readonly string[][] DokkaebiAllWrong =
    {
        new[] { "이게 내 방망이라고? 내 방망이는 이렇게 안 생겼어!", "내 방망이를 이렇게 그리면 어떡해! 나 좀 서운해." },
        new[] { "이거 아니야? 네 그림엔 딱 이렇게 생겼던데?", "짠! 가방... 맞지? 히히." },
        new[] { "냄새는 좋지? 네 그림이 꼭 이렇게 생겼더라.", "돗자리보다 맛있는 걸 찾아왔어! 히히." },
        new[] { "짠! 네 그림이랑 똑같지?", "이걸 어떻게 먹냐고? 네가 그렇게 그렸는걸!" },
        new[] { "논에 서 있길래 데려왔어! 네 그림이랑 닮았던걸?", "말이 좀 없지만... 네 친구 맞지? 히히." },
    };

    // 주인공 반응 : 1단계는 방망이, 2~4단계는 물건 공통, 5단계는 친구 (사람이라 친구에게 하는 말로)
    const string BoyBatSuccess = "다행이다! 이제 힘이 좀 나?";
    const string BoyBatWrong = "미안해… 간식 하나 더 줄 테니까 다시 그려 볼까?";
    const string BoySuccess = "맞아! 바로 이거야! 이번엔 안 잃어버려야지.";
    const string BoyWrong = "어… 이게 아닌데. 간식 하나 더 줄 테니까 다시 그려 볼까?";
    const string BoyFriendSuccess = "다행이다! 내가 놓쳐서 미안해. 이제 같이 소풍 가자!";
    const string BoyFriendWrong = "어… 내 친구가 아니야. 간식 하나 더 줄 테니까 다시 그려 볼까?";

    // 돋보기 : 1단계 첫 그리기 때 도깨비가 쓰는 법을 보여 줘요 (시범은 공짜)
    public const string MagnifierShow = "잘 기억 안 나지? 위에 있는 돋보기를 눌러 봐!";
    public const string MagnifierHow = "돋보기로 5초 동안 다시 볼 수 있어. 손가락으로 렌즈를 끌면 다른 곳도 보여!";
    public const string MagnifierRule = "이번은 공짜! 돋보기는 소풍 끝날 때까지 딱 한 번만 더 빌려줄게. 아껴 써!";
    // 한 단계에서 두 번째로 틀렸을 때 (단계마다 한 번, 그 단계에서만)
    public const string MagnifierGift = "자꾸 틀리니까 딱하다. 돋보기 한 번 빌려줄게! 이번 물건 찾을 때만 쓸 수 있어.";

    public const string BoyOutOfSnacks = "어떡해… 사탕이 다 떨어졌어.";
    public const string JudgeError = "그림이 잘 안 보여… 간식은 돌려줄게, 다시 그려 줘.";
    // 이야기 종료 : 도깨비 인사. 대사를 누르면 챕터 선택으로 가요
    public const string EndingBye = "오늘 소풍 즐거웠지? 다음에 또 뭐 잃어버리면 불러. 또 올게!";


    // ───────────── 5챕터 · 도깨비의 저주 (임시 대사, 구현 뒤 다시 정해요) ─────────────
    // 시작 장면 : 밝은 숲에서 웃던 도깨비가 간식 이야기를 꺼내면 숲이 어두워지고 흑화해요 (CurseDarkFrom 번째 대사부터)
    public static readonly Line[] CurseIntro =
    {
        new Line(Face.BoySurprised, "도깨비야! 우리를 이 숲으로 왜 불렀어?"),
        new Line(Face.Dokkaebi, "히히, 너희 얼굴이 보고 싶어서 불렀지."),
        new Line(Face.Dokkaebi, "그런데 숨겨 둔 간식은 왜 나한테 안 줬어?"),
        new Line(Face.Dokkaebi, "괘씸하구나! 널 저주하겠어. 내가 시키는 건 다 그려 와!"),
        new Line(Face.BoyNormal, "(도깨비가 무서워하는 걸 그리면 저주가 풀릴까…?)"),
    };
    public const int CurseDarkFrom = 2;

    // 단계 대화 : [단계] 도깨비가 요청 물건 이름을 말하고, 주인공은 혼잣말로 맞설 물건을 떠올려요
    static readonly Line[][] CurseStep =
    {
        new[] { new Line(Face.Dokkaebi, "짚신을 그려 와! 한 걸음에 어디든 널 쫓아가게!"),
                new Line(Face.BoyNormal, "(부적을 무서워한댔어. 노란 종이에 빨간 글씨였지…)") },
        new[] { new Line(Face.Dokkaebi, "도깨비 부채를 그려 와! 한 번 부치면 널 이 숲에 가둘 거야!"),
                new Line(Face.BoyNormal, "(닭피 그릇을 보면 도망간댔어. 친구를 지켜야 해…!)") },
        new[] { new Line(Face.Dokkaebi, "도깨비 감투를 그려 와! 아무도 날 못 보게 말이야!"),
                new Line(Face.BoyNormal, "(팥주머니라면… 도깨비도 꼼짝 못 할 거야!)") },
    };

    // 요청 물건을 받았을 때 도깨비가 힘 자랑 : [단계]
    static readonly string[] CurseBoast =
    {
        "히히! 이 짚신이면 네가 어디 있든 한 걸음에 따라가지!",
        "부채질 한 번에 숲길이 싹 막혔지? 이제 못 나간다!",
        "감투를 쓰면 내 모습이 싹 사라지지! 날 찾을 수 있겠어?",
    };
    public const string CurseCounterHit = "으악! 따가워! 내 힘이 빠져나가잖아!";
    public const string CurseCounterWrong = "히히, 그런 건 하나도 안 무섭지!";
    public const string CurseRequestWrong = "틀렸어! 네 친구 영혼은 내가 데려간다!";
    public const string CurseBoyHit = "(됐다! 도깨비가 힘을 잃고 있어!)";
    public const string CurseBoyBoast = "(큰일이야… 도깨비가 점점 강해져.)";
    public const string CurseBoyWrong = "안 돼! 내 친구한테 손대지 마!";
    public const string CurseGameOver = "내 뒤에 잠든 사람들 보이지? 너희도 함께 있어라!";
    public const string CurseHappy = "착해져서 도망갔네… 이 방망이로 잃어버린 걸 찾아야지!";
    public const string CurseHappyNoBat = "착해져서 도망갔네… 이제 잃어버린 건 내가 직접 찾아야지!";
    public const string CurseBad = "감투를 쓰니 내 모습이 싹 사라졌지? 히히…";

    public static Line[] CurseStepLines(int step) => CurseStep[Mathf.Clamp(step, 0, CurseStep.Length - 1)];
    public static string CurseBoastLine(int step) => CurseBoast[Mathf.Clamp(step, 0, CurseBoast.Length - 1)];
    /// <summary>쪽지 글 : "도깨비야, 이 OO 찾아 줘!" (쪽지 가운데 정렬)</summary>
    public static string ItemNote(string name) => $"도깨비야, 이 {name} 찾아 줘!";
    public static string ItemRead(string name) => $"음, {name}{GameManager.Josa(name, "이구나", "구나")}. 좋아, 찾아올게!";

    static string Pick(string[] list) => list[Random.Range(0, list.Length)];

    /// <summary>대사 상자에 나오는 모든 대사 (자동 테스트가 줄바꿈 · 넘침을 검사해요)</summary>
    public static System.Collections.Generic.IEnumerable<string> AllLines()
    {
        foreach (Line[] stage in Intro) foreach (Line l in stage) yield return l.text;
        yield return BatFound.text;
        yield return BatLost.text;
        yield return GiveBoy;
        yield return GiveEat;
        foreach (string l in GiveRead) yield return l;
        foreach (string[][] table in new[] { DokkaebiSuccess, DokkaebiPartial, DokkaebiAllWrong })
            foreach (string[] stage in table) foreach (string l in stage) yield return l;
        foreach (string l in new[] { BoyBatSuccess, BoyBatWrong, BoySuccess, BoyWrong, BoyFriendSuccess, BoyFriendWrong,
                                     MagnifierShow, MagnifierHow, MagnifierRule, MagnifierGift, BoyOutOfSnacks, JudgeError, EndingBye })
            yield return l;
        foreach (Line l in CurseIntro) yield return l.text;
        foreach (Line[] step in CurseStep) foreach (Line l in step) yield return l.text;
        foreach (string l in CurseBoast) yield return l;
        foreach (string l in new[] { CurseCounterHit, CurseCounterWrong, CurseRequestWrong, CurseBoyHit, CurseBoyBoast, CurseBoyWrong,
                                     CurseGameOver, CurseHappy, CurseHappyNoBat, CurseBad })
            yield return l;
    }

    /// <summary>시작 대화. 2단계는 방망이를 찾았는지에 따라 맨 앞 대사가 달라져요.</summary>
    public static Line[] IntroLines(int stageIndex, bool batLost)
    {
        Line[] lines = Intro[stageIndex];
        if (stageIndex != 1) return lines;
        var withLead = new Line[lines.Length + 1];
        withLead[0] = batLost ? BatLost : BatFound;
        lines.CopyTo(withLead, 1);
        return withLead;
    }

    public static string NoteText(int stageIndex) => Note[stageIndex];
    public static string GiveReadLine(int stageIndex) => GiveRead[stageIndex];

    /// <summary>도깨비 결과 대사. outcome : 0 전부 맞음, 1 일부 틀림, 2 전부 틀림</summary>
    public static string DokkaebiResult(int stageIndex, int outcome) =>
        Pick((outcome == 0 ? DokkaebiSuccess : outcome == 1 ? DokkaebiPartial : DokkaebiAllWrong)[stageIndex]);

    public static string BoyResult(int stageIndex, int outcome)
    {
        if (stageIndex == 0) return outcome == 0 ? BoyBatSuccess : BoyBatWrong;
        if (stageIndex == 4) return outcome == 0 ? BoyFriendSuccess : BoyFriendWrong;
        return outcome == 0 ? BoySuccess : BoyWrong;
    }

    /// <summary>"가 + 나" 를 + 로 나눈 덩어리째 줄이 바뀌게 만들어요. 단어 중간에서 끊기지 않아요.</summary>
    public static string NoBreakParts(string name)
    {
        string[] parts = name.Split(new[] { " + " }, System.StringSplitOptions.None);
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append("<nobr>").Append(i > 0 ? "+ " : "").Append(parts[i]).Append("</nobr>");
        }
        return sb.ToString();
    }
}

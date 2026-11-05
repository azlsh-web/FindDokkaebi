using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// GameScript : 게임 대사 모음
// ─────────────────────────────────────────────────────────────
// 대사를 고칠 때는 이 파일만 고치면 돼요. 기준은 Docs/전달/14_대사_수정표_바뀐내용.xlsx 의 대사 탭이에요.
// 특징 이름은 결과 대사에 넣지 않아요 (정답 힌트가 되니까요). 판정 기준은 시작 대화의 생김새 대사로 알려 줘요.
public static class GameScript
{
    public enum Face { BoySurprised, BoyHappy, Dokkaebi }

    public struct Line
    {
        public Face face;
        public string text;
        public Line(Face face, string text) { this.face = face; this.text = text; }
    }

    // 오프닝 : 도깨비가 나오기 전에 주인공 혼자 말하는 줄 수. 이 대사들은 그림과 함께 저절로 넘어가요.
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
            new Line(Face.BoyHappy, "내가 제일 아끼는 가방이야. 네모난 몸체에 위쪽 손잡이, 앞주머니가 달렸어."),
            new Line(Face.BoyHappy, "주머니 안에는 가로 줄무늬도 있어!"),
            new Line(Face.Dokkaebi, "좋아! 네가 떠올린 그대로 쪽지에 그려 줘."),
            new Line(Face.Dokkaebi, "이번엔 모양만으로는 부족해. 무늬도 잘 그려야 해!"),
        },
        new[]
        {
            new Line(Face.BoyHappy, "돗자리도 분명히 챙겼는데… 또 어디 뒀더라?"),
            new Line(Face.Dokkaebi, "너는 어쩜 그렇게 다 잃어버리니? 어떤 돗자리인데?"),
            new Line(Face.BoyHappy, "네모난 모양에, 띠가 네 줄 있어."),
            new Line(Face.BoyHappy, "띠 색 순서가 중요해!"),
            new Line(Face.Dokkaebi, "좋아! 떠올린 그대로 쪽지에 그려 줘."),
            new Line(Face.Dokkaebi, "모양이랑 무늬에 색깔까지! 이번엔 색도 잘 칠해야 해!"),
        },
        new[]
        {
            new Line(Face.BoyHappy, "아차! 김밥도 어디 뒀는지 깜빡했어!"),
            new Line(Face.Dokkaebi, "김밥? 그건 나도 먹고 싶은데! 어떻게 생겼어?"),
            new Line(Face.BoyHappy, "까만 김이 둥글게 말려 있고, 안에 재료가 위에 둘, 아래에 하나 있어."),
            new Line(Face.BoyHappy, "노란 단무지랑 초록 시금치도 들어 있어!"),
            new Line(Face.Dokkaebi, "좋아, 쪽지에 그려 줘. 없는 색은 두 가지를 섞어서 만들어 봐!"),
        },
        new[]
        {
            new Line(Face.BoySurprised, "아차! 친구랑 만나기로 한 것도 깜빡했어!"),
            new Line(Face.Dokkaebi, "큰일이야, 네 친구가 길을 잃었대!"),
            new Line(Face.BoySurprised, "같이 가야 하는데! 꼭 찾아 줘."),
            new Line(Face.Dokkaebi, "어떻게 생긴 친구야?"),
            new Line(Face.BoySurprised, "양 갈래로 묶은 갈색 머리야."),
            new Line(Face.BoySurprised, "파란 멜빵바지에 주황 상의를 입었고, 주머니에는 천 조각을 덧댔어."),
            new Line(Face.Dokkaebi, "걱정 마, 쪽지에 그려 주면 찾아올게. 이번엔 세 가지 색도 섞어야 해!"),
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
        "도깨비야, 이 친구 찾아 줘!",
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
        "음, 이 친구구나. 좋아, 찾아올게!",
    };

    // 결과 대사 : [단계 - 1] 별 전부 맞음 / 일부 틀림 / 전부 틀림
    static readonly string[][] DokkaebiSuccess =
    {
        new[] { "내 방망이다! 얼마나 보고 싶었다고!", "이 묵직한 손맛! 역시 내 방망이야!" },
        new[] { "가방 여기 있다! 어때, 나 잘 찾지?", "쪽지 보고 한 번에 찾았어!" },
        new[] { "돗자리 찾았다! 앉으면 딱이겠어.", "쪽지가 또렷해서 금방 찾았어!" },
        new[] { "김밥 찾았다! 한 줄만 나 주면 안 돼?", "쪽지 보니까 배고프더라. 바로 찾았지!" },
        new[] { "네 친구 찾았다! 저쪽에서 울고 있더라고.", "여기 있었네! 이제 소풍 가자!" },
    };

    // 방망이는 기준이 하나라 일부 틀림이 없어요
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

    // 주인공 반응 : 1단계는 방망이 전용, 2~5단계는 공통
    const string BoyBatSuccess = "다행이다! 이제 힘이 좀 나?";
    const string BoyBatWrong = "미안해… 간식 하나 더 줄 테니까 다시 그려 볼까?";
    const string BoySuccess = "맞아! 바로 이거야! 이번엔 안 잃어버려야지.";
    const string BoyWrong = "어… 이게 아닌데. 간식 하나 더 줄 테니까 다시 그려 볼까?";

    public const string BoyOutOfSnacks = "어떡해… 간식이 다 떨어졌어.";
    public const string JudgeError = "그림이 잘 안 보여… 간식은 돌려줄게, 다시 그려 줘.";
    public const string EndingAsk = "소풍 즐거웠지? 처음으로 돌아갈까?";

    static string Pick(string[] list) => list[Random.Range(0, list.Length)];

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

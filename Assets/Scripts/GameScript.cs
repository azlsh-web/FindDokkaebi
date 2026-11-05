using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// GameScript : 게임 대사 모음
// ─────────────────────────────────────────────────────────────
// 대사를 고칠 때는 이 파일만 고치면 돼요. 특징 이름은 결과 대사에 넣지 않아요 (정답 힌트가 되니까요).
public static class GameScript
{
    public enum Face { BoySurprised, BoyHappy, Dokkaebi }

    public struct Line
    {
        public Face face;
        public string text;
        public Line(Face face, string text) { this.face = face; this.text = text; }
    }

    // 단계별 시작 대화 (2~3줄)
    static readonly Line[][] Intro =
    {
        new[]
        {
            new Line(Face.BoySurprised, "소풍 준비물이 모두 어디 갔지?!"),
            new Line(Face.Dokkaebi, "내 방망이도 없어졌어! 쪽지에 그려 주면 찾아 올게."),
            new Line(Face.Dokkaebi, "간식 한 개면 돼. 잘 기억해 둬!"),
        },
        new[]
        {
            new Line(Face.Dokkaebi, "이제 준비물을 담을 가방을 찾자."),
            new Line(Face.Dokkaebi, "이번엔 무늬도 잘 봐!"),
        },
        new[]
        {
            new Line(Face.BoyHappy, "잔디밭에 펼칠 돗자리도 필요해."),
            new Line(Face.Dokkaebi, "이번엔 색이 중요해. 순서도 잘 봐!"),
        },
        new[]
        {
            new Line(Face.BoyHappy, "소풍에 김밥이 빠질 수 없지!"),
            new Line(Face.Dokkaebi, "없는 색은 두 가지를 섞어서 만들어 봐!"),
        },
        new[]
        {
            new Line(Face.Dokkaebi, "큰일이야, 네 친구가 길을 잃었대!"),
            new Line(Face.BoySurprised, "같이 가야 하는데! 꼭 찾아 줘."),
            new Line(Face.Dokkaebi, "이번엔 세 가지 색도 섞어야 해!"),
        },
    };

    // 쪽지가 나올 때 도깨비가 하는 말. 색 조합은 알려 주지 않아요.
    static readonly string[] Card =
    {
        "여기 적힌 건 꼭 그려 줘. 안 그리면 엉뚱한 걸 가져올 거야!",
        "이번엔 주머니 안쪽 줄무늬도 꼭 넣어 줘!",
        "띠의 색 순서가 중요해. 위에서부터 차례로 봐 둬!",
        "재료 색을 잘 봐 둬. 어떤 색을 섞을지는 네가 찾아봐!",
        "머리와 옷 색을 잘 봐 둬. 섞다 보면 딱 맞는 색이 나올 거야!",
    };

    // 결과 대사 : [단계 - 1] 별 전부 맞음 / 일부 틀림 / 전부 틀림
    static readonly string[][] DokkaebiSuccess =
    {
        new[] { "내 방망이다! 쪽지 덕분에 찾았어!", "이거야 이거! 네 그림 꽤 쓸 만한데?" },
        new[] { "가방 여기 있다! 어때, 나 잘 찾지?", "쪽지 보고 한 번에 찾았어!" },
        new[] { "돗자리 찾았다! 앉으면 딱이겠어.", "쪽지가 또렷해서 금방 찾았어!" },
        new[] { "김밥 찾았다! 한 줄만 나 주면 안 돼?", "쪽지 보니까 배고프더라. 바로 찾았지!" },
        new[] { "네 친구 찾았다! 저쪽에서 울고 있더라고.", "여기 있었네! 이제 소풍 가자!" },
    };

    static readonly string[][] DokkaebiPartial =
    {
        new[] { "네가 이렇게 그렸잖아~ 난 그대로 찾아왔다고!" },
        new[] { "네가 이렇게 그렸잖아~ 난 그대로 찾아왔다고!", "쪽지대로 찾았는데? 네 그림이 그런걸~" },
        new[] { "그림이 이랬는걸? 난 잘못 없어~", "흠, 네 쪽지에 있던 그대로야!" },
        new[] { "네가 그린 김밥이 이렇던데? 맛은 장담 못 해~", "그림대로 찾아왔어. 먹을 수는 있겠지?" },
        new[] { "이 친구 맞지? 네가 그린 대로인데~", "어라, 네 친구가 이렇게 생긴 거 아니야?" },
    };

    static readonly string[][] DokkaebiAllWrong =
    {
        new[] { "짠! 네가 그린 대로 찾아왔지?", "이게 네 그림이랑 제일 닮았던걸? 히히." },
        new[] { "이거 아니야? 네 그림엔 딱 이렇게 생겼던데?", "짠! 가방... 맞지? 히히." },
        new[] { "냄새는 좋지? 네 그림이 꼭 이렇게 생겼더라.", "돗자리보다 맛있는 걸 찾아왔어! 히히." },
        new[] { "짠! 네 그림이랑 똑같지?", "이걸 어떻게 먹냐고? 네가 그렇게 그렸는걸!" },
        new[] { "논에 서 있길래 데려왔어! 네 그림이랑 닮았던걸?", "말이 좀 없지만... 네 친구 맞지? 히히." },
    };

    static readonly string[] BoySuccess = { "맞아, 바로 이거야!", "고마워 도깨비야!" };
    static readonly string[] BoyPartial = { "어, 어라?! 이건 아닌데...!", "뭔가 다른데...?! 더 잘 그려 볼걸!" };
    static readonly string[] BoyAllWrong = { "이, 이게 뭐야?! 내 그림이 그렇게 이상해?!", "헉...! 특징을 더 그려 줘야겠어!" };

    public const string BoyOutOfSnacks = "어떡해, 간식이 다 떨어졌어...!";
    public const string JudgeError = "쪽지가 잘 안 보여! 간식 돌려줄게.";
    public const string Oops1 = "아차차! 이건 대파잖아...";
    public const string Oops2 = "잠깐만, 내가 직접 찾아올게!";
    public const string Oops3 = "찾았다! 내 방망이!";
    public const string EndingAsk = "소풍 즐거웠지? 처음으로 돌아갈까?";

    static string Pick(string[] list) => list[Random.Range(0, list.Length)];

    public static Line[] IntroLines(int stageIndex) => Intro[stageIndex];
    public static string CardLine(int stageIndex) => Card[stageIndex];

    /// <summary>도깨비 결과 대사. outcome : 0 전부 맞음, 1 일부 틀림, 2 전부 틀림</summary>
    public static string DokkaebiResult(int stageIndex, int outcome) =>
        Pick((outcome == 0 ? DokkaebiSuccess : outcome == 1 ? DokkaebiPartial : DokkaebiAllWrong)[stageIndex]);

    public static string BoyResult(int outcome) => Pick(outcome == 0 ? BoySuccess : outcome == 1 ? BoyPartial : BoyAllWrong);

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

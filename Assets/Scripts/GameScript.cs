using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// GameScript : 게임 대사 모음
// ─────────────────────────────────────────────────────────────
// 대사를 고칠 때는 이 파일만 고치면 돼요. 기획자와 주고받는 표는 Docs/3_전달/대사_수정표.xlsx 예요 (Docs/4_기술/문서_도구.md 의 "대사 수정 받기").
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

    // 돋보기 : 2챕터 첫 물건 첫 그리기 때 도깨비가 쓰는 법을 보여 줘요 (시범은 공짜)
    public const string MagnifierShow = "잘 기억 안 나지? 위에 있는 돋보기를 눌러 봐!";
    public const string MagnifierHow = "돋보기로 5초 동안 다시 볼 수 있어. 손가락으로 렌즈를 끌면 다른 곳도 보여!";
    public const string MagnifierRule = "돋보기는 이번 방 정리 때만 딱 한 번 더 빌려줄게. 아껴 써!";
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

    // 요청 물건을 고름 : 그리지 않고 바로 도깨비가 가져가요 (주인공 하트 -1) : [단계]
    static readonly string[] CurseTake =
    {
        "짚신은 내가 가져가지! 히히, 넌 이제 못 도망가!",
        "히히, 부채는 잘 쓰마! 숲길을 꽁꽁 막아 주지!",
        "히히, 감투는 내 거다! 안 보여도 난 네 뒤에 있어!",
    };
    public const string CurseBoyTaken = "(으윽… 시키는 대로 하니까 더 강해지잖아!)";

    // 맞설 물건 : 쪽지 글 · 들이밀 때 주인공 (사탕은 주지 않아요)
    public const string CurseNote = "도깨비야, 이걸 보고 저주를 풀어!";
    public const string CurseGiveBoy = "도깨비야, 이거 봐! 네가 무서워하는 거야!";

    // 맞설 물건 성공 : 도깨비가 "제법이군" 하고 다음 단계로 : [단계]
    static readonly string[] CurseCounterHit =
    {
        "흠, 제법이군. 그렇다고 내가 겁먹은 건 아냐!",
        "으윽… 제법이군. 그래도 아직 끝난 건 아냐!",
        "제, 제법이군… 으으, 알았어! 저주 풀고 갈게!",
    };
    public const string CurseBoyHit = "(됐다! 도깨비의 무서운 기운이 사라지고 있어!)";
    // 맞설 물건 실패 : 점프스케어 뒤 (주인공 하트 -1)
    public const string CurseCounterWrong = "히히, 깜짝 놀랐지? 그걸로 날 막을 수 있겠어?";
    public const string CurseBoyWrong = "아야… 아직 버틸 수 있어! 다시 해 보자!";
    // 하트 0 : 친구가 쓰러지고 영혼을 빼앗겨요 → 게임오버
    public const string CurseSoulTaken = "더는 못 버티겠지? 네 친구 영혼은 내가 데려간다!";
    public const string CurseGameOver = "내 뒤에 잠든 사람들 보이지? 너희도 함께 있어라!";
    public const string CurseHappy = "착해져서 도망갔네… 이 방망이로 잃어버린 걸 찾아야지!";
    public const string CurseHappyNoBat = "착해져서 도망갔네… 이제 잃어버린 건 내가 직접 찾아야지!";

    // ───────────── 2~4챕터 · 방 (기획서 대사 + codex 초안 임시 대사, 기획자 확인 전) ─────────────
    //   [챕터 - 2] 시작 장면 : 첫 줄은 기획서, 둘째 줄(주인공이 도깨비를 부름)부터 도깨비가 나와요 (ChapterData.aloneLines = 2)
    static readonly Line[][] RoomOpening =
    {
        new[]
        {
            new Line(Face.BoySurprised, "엄마가 방 정리 다 할 때까지 못 나간대… 근데 정리하려던 물건이 또 없어졌어!"),
            new Line(Face.BoyHappy, "도깨비야, 비도 오는데 내 방 정리 좀 도와줘!"),
            new Line(Face.Dokkaebi, "엄마? …나도 엄마 보고 싶다."),
            new Line(Face.BoyNormal, "엄마 도깨비도 너처럼 초록색이야?"),
            new Line(Face.Dokkaebi, "나도 한때는 너처럼 인간이었지…"),
            new Line(Face.BoySurprised, "응? 근데 어쩌다 도깨비가 된 거야?"),
            new Line(Face.Dokkaebi, "자자, 그래서 무슨 물건을 찾아 달라고?"),
            new Line(Face.Dokkaebi, "이번엔 모양만으론 안 돼. 무늬도 잘 기억해!"),
        },
        new[]
        {
            new Line(Face.BoySurprised, "쉿! 동생 몰래 생일 파티 준비 중이야. 그런데 장식이 하나도 없어!"),
            new Line(Face.BoyHappy, "도깨비야, 동생 몰래 생일 파티 준비하자!"),
            new Line(Face.Dokkaebi, "너는 왜 맨날 직접 찾아볼 생각도 안 하고 나한테만 도와 달라고 해?"),
            new Line(Face.BoySurprised, "너 모습이 왜 그래?"),
            new Line(Face.Dokkaebi, "…뭐, 단것을 주니까 이번에도 참도록 할게."),
            new Line(Face.Dokkaebi, "이번엔 색깔도 중요해. 빨강, 노랑, 파랑이 어디에 있었는지 기억해!"),
        },
        new[]
        {
            new Line(Face.BoySad, "간식 바구니가 비어 버렸어ㅠ 너한테 줄 간식을 찾아서 바구니를 채워야 해."),
            new Line(Face.BoyNormal, "사탕은 없지만… 도깨비야, 이번에도 와 줄래?"),
            new Line(Face.BoyHappy, "간식을 찾아 주면, 찾은 간식은 전부 너한테 줄게! 약속!"),
            new Line(Face.Dokkaebi, "이번엔 색을 섞어야 해. 팔레트에서 두 가지 색을 섞어 봐!"),
        },
    };

    // [챕터 - 2][물건] 물건 대화 : 주인공이 생김새를 말하고, 도깨비가 알아듣고 쪽지에 그려 달라고 해요
    static readonly Line[][][] RoomItemIntro =
    {
        new[]
        {
            new[]
            {
                new Line(Face.BoySurprised, "양말 한 짝이 없어! 어디로 쏙 숨었지?"),
                new Line(Face.BoyNormal, "발목은 길고, 발끝은 옆으로 굽은 양말이야."),
                new Line(Face.BoyNormal, "가로로 쭉쭉 줄무늬가 있어."),
                new Line(Face.Dokkaebi, "그럼 양말 무늬도 잘 그려야겠네."),
                new Line(Face.Dokkaebi, "어떤 양말인지 쪽지에 그려 줘!"),
            },
            new[]
            {
                new Line(Face.BoySurprised, "우산도 없어! 비 오는데 어디 갔지?"),
                new Line(Face.BoyNormal, "펼치면 둥근 지붕 같고, 밑에 손잡이가 있어."),
                new Line(Face.BoyNormal, "동글동글 물방울무늬가 콕콕 찍혀 있어!"),
                new Line(Face.Dokkaebi, "아하, 우산에 물방울무늬가 콕콕 있구나."),
                new Line(Face.Dokkaebi, "그 우산을 쪽지에 그려 줘!"),
            },
            new[]
            {
                new Line(Face.BoyNormal, "내 베개도 안 보여. 이불 속에도 없네."),
                new Line(Face.BoyNormal, "네모나고 푹신한 베개야."),
                new Line(Face.BoyNormal, "가로줄과 세로줄이 만나 작은 네모가 생겨."),
                new Line(Face.Dokkaebi, "작은 네모가 가득하겠네. 무늬도 기억할게!"),
                new Line(Face.Dokkaebi, "네 베개를 쪽지에 그려 줘."),
            },
        },
        new[]
        {
            new[]
            {
                new Line(Face.BoySurprised, "동생 씌워 줄 고깔모자가 없어!"),
                new Line(Face.BoyNormal, "끝은 뾰족하고, 아래는 둥근 모자야."),
                new Line(Face.BoyNormal, "띠는 위에서부터 빨강, 노랑, 파랑이야."),
                new Line(Face.Dokkaebi, "휴, 생일 준비까지… 띠 색도 기억해야겠네."),
                new Line(Face.Dokkaebi, "알았어. 그 모자를 쪽지에 그려 줘."),
            },
            new[]
            {
                new Line(Face.BoySurprised, "풍선 세 개 묶음도 어디 갔어!"),
                new Line(Face.BoyNormal, "동그란 풍선이 빨강, 노랑, 파랑 하나씩이야."),
                new Line(Face.BoyNormal, "풍선마다 달린 줄을 아래에서 한데 묶었어."),
                new Line(Face.Dokkaebi, "에휴, 이번엔 풍선이네. 묶인 줄도 있구나."),
                new Line(Face.Dokkaebi, "그 풍선들을 쪽지에 그려 줘."),
            },
            new[]
            {
                new Line(Face.BoySurprised, "제일 중요한 선물 상자를 못 찾겠어!"),
                new Line(Face.BoyNormal, "네모난 파란 상자에 빨간 리본이 묶여 있어."),
                new Line(Face.BoyNormal, "상자에는 노란 별무늬도 있어!"),
                new Line(Face.Dokkaebi, "흠, 별도 노란색이구나. 챙길 게 많네."),
                new Line(Face.Dokkaebi, "그 선물 상자를 쪽지에 그려 줘."),
            },
        },
        new[]
        {
            new[]
            {
                new Line(Face.BoyNormal, "바구니에 넣을 귤이 없어. 그것도 줄게!"),
                new Line(Face.BoyNormal, "동그란 주황색 귤이야."),
                new Line(Face.BoyNormal, "꼭대기에 초록색 잎이 하나 달려 있어."),
                new Line(Face.Dokkaebi, "주황이랑 초록이라… 두 색씩 섞어야겠네!"),
                new Line(Face.Dokkaebi, "벌써 군침 돈다! 쪽지에 그려 줘!"),
            },
            new[]
            {
                new Line(Face.BoySurprised, "포도송이도 없어! 그것도 네 간식인데!"),
                new Line(Face.BoyNormal, "보라색 알이 위엔 넓게, 아래엔 좁게 모였어."),
                new Line(Face.BoyNormal, "알들이 다닥다닥 붙고, 위엔 초록 줄기가 있어."),
                new Line(Face.Dokkaebi, "보라랑 초록도 두 색씩 섞겠네. 맛있겠다!"),
                new Line(Face.Dokkaebi, "그 포도송이를 쪽지에 그려 줘!"),
            },
            new[]
            {
                new Line(Face.BoyNormal, "바구니에 넣을 사탕도 없어. 찾으면 네 거야!"),
                new Line(Face.BoyNormal, "양끝을 꼬아 싼 사탕이야."),
                new Line(Face.BoyNormal, "주황색 줄과 초록색 줄이 번갈아 있어."),
                new Line(Face.Dokkaebi, "두 색씩 섞으면 되겠네. 사탕이라니, 히히!"),
                new Line(Face.Dokkaebi, "그 사탕을 쪽지에 그려 줘!"),
            },
        },
    };

    // [챕터 - 2][물건] 결과 : 도깨비 맞음 2 · 틀림 2 (하나를 골라요), 주인공 맞음 · 틀림
    static readonly string[][][] RoomDokkaebiRight =
    {
        new[]
        {
            new[] { "양말 찾았다! 나한테 숨바꼭질은 안 통하지!", "여기 네 양말! 내가 또 찾아냈지!" },
            new[] { "우산 찾아왔어! 이 정도쯤이야, 히히!", "짠, 네 우산! 비보다 내가 더 빨랐지?" },
            new[] { "네 베개 찾았어! 나 없었으면 어쩔 뻔했니!", "여기 베개! 숨은 물건 찾기는 내가 최고지!" },
        },
        new[]
        {
            new[] { "모자 찾았어! 휴, 그래도 나만 한 도깨비 없지?", "여기 모자! 투덜대도 찾는 건 잘하지?" },
            new[] { "풍선 찾아왔어! 휴, 그래도 내가 잘 찾긴 하지.", "여기 풍선! 이런 건 내 눈을 못 피하지!" },
            new[] { "선물 찾았다! 흠, 나한테 고마워해도 돼.", "여기 선물! 힘들어도 찾는 솜씨는 여전하지?" },
        },
        new[]
        {
            new[] { "귤 찾았다! 내 솜씨 덕에 간식이 생겼네!", "여기 귤! 잘 찾았지? 얼른 먹고 싶다!" },
            new[] { "포도 찾았어! 나 참 잘 찾지? 입맛 돈다!", "짠, 네 포도! 간식 찾을 땐 내가 최고야!" },
            new[] { "사탕 찾았다! 내가 잘 찾으니 달달한 것도 생기네!", "여기 사탕! 어때, 내 솜씨? 얼른 먹고 싶어!" },
        },
    };

    static readonly string[][][] RoomDokkaebiWrong =
    {
        new[]
        {
            new[] { "히히, 네 그림엔 이렇게 생겼던걸?", "쪽지랑 나란히 봐. 네가 이렇게 그렸잖아!" },
            new[] { "어라? 네가 그린 건 이거던데, 히히.", "이상하다고? 그림엔 딱 이렇게 있었는걸!" },
            new[] { "네가 이렇게 그려 놓고 놀라면 어떡해, 히히!", "난 쪽지만 따라갔어. 그림이랑 똑같지?" },
        },
        new[]
        {
            new[] { "흠, 쪽지엔 이렇게 그려 놨던데? 난 그대로야.", "흠, 네가 그린 대로인데? 나 보고 놀라지 마!" },
            new[] { "휴, 네가 이렇게 그렸으니 이걸 가져왔지!", "쪽지 좀 봐. 네 그림이랑 똑같잖아, 흥." },
            new[] { "흥, 내가 바꾼 거 아냐. 네가 이렇게 그렸어!", "뭐가 이상해? 네 쪽지랑 꼭 닮았는데." },
        },
        new[]
        {
            new[] { "네가 이렇게 그렸잖아! 맛도 궁금한걸, 히히.", "쪽지랑 똑같지? 네 그림 보고 골라 왔어!" },
            new[] { "네 그림이 이렇던데? 배고파서 바꾼 게 아냐!", "히히, 네가 그린 그대로야. 맛은 어떨까?" },
            new[] { "네가 이렇게 그렸는걸! 내 입맛 탓은 아냐!", "히히, 쪽지 그대로 찾았지! 먹어도 되려나?" },
        },
    };

    static readonly string[][] RoomBoyRight =
    {
        new[] { "내 양말 맞아! 이제 짝이 생겼네!", "맞아, 내 우산! 이제 비 와도 괜찮아!", "내 베개야! 오늘도 꼭 안고 자야지!" },
        new[] { "이 모자야! 동생이 쓰면 신나겠다!", "맞아, 이거야! 파티가 더 신나겠어!", "찾았다! 동생이 좋아할 얼굴이 벌써 보여!" },
        new[] { "맞아, 이 귤이야! 바구니에 담아 둘게!", "이 포도 맞아! 바구니에 담아 놔야지!", "맞아, 이 사탕이야! 이것도 담아 둘게!" },
    };

    static readonly string[][] RoomBoyWrong =
    {
        new[] { "내 게 아니야… 사탕 하나 더 줄게. 다시 그려 볼래.", "이건 아닌데… 사탕 하나 더 줄게. 다시 그려 볼래.", "내 베개가 아냐… 사탕 하나 더 줄게. 다시 그릴래." },
        new[] { "이 모자가 아냐… 사탕 하나 더 줄게. 다시 그릴래.", "찾던 게 아냐… 사탕 하나 더 줄게. 다시 그려 볼래.", "내 선물이 아냐… 사탕 하나 더 줄게. 다시 그릴래." },
        new[] { "이건 아닌데… 사탕도 없고, 다시 그리지도 못해.", "찾던 게 아냐… 사탕도 없어. 다시 그릴 수도 없네.", "이건 아니야… 줄 사탕도 없고, 다시 그릴 수도 없어." },
    };

    // [챕터 - 2] 끝 장면 : 도깨비 인사 (4챕터는 약속 + 주인공 속마음, 속마음은 기획서)
    static readonly Line[][] RoomEnd =
    {
        new[] { new Line(Face.Dokkaebi, "나도 사람일 땐 이랬지… 앗, 비 그친다! 또 봐!") },
        new[] { new Line(Face.Dokkaebi, "휴, 난 좀 쉴래. 동생한테 생일 축하한다고 해!") },
        new[]
        {
            new Line(Face.Dokkaebi, "찾은 간식은 전부 내 거랬지? 히히, 기다릴게!"),
            new Line(Face.BoyNormal, "(간식을 숨겨 두면 도깨비도 못 찾겠지? 다 찾고 보니 간식이 아까워ㅠ)"),
        },
    };

    // 4챕터 건네기 : 사탕 없이 쪽지만
    public const string GiveBoyNoCandy = "여기 쪽지! 사탕은 없지만, 찾은 간식은 다 줄게!";

    // 챕터 안내 : 1챕터 펜, 3챕터 색
    public const string PenDraw = "손가락으로 쪽지에 쓱쓱 그려 봐!";
    public const string PenUndo = "앗, 잘못 그렸어? 이걸 누르면 되돌릴 수 있어!";
    public const string PenDone = "다 그렸으면 이 버튼을 눌러 줘!";
    public const string MixBrush = "이젠 색도 중요하네! 붓을 눌러 봐.";
    public const string MixHow = "빨강 · 노랑 · 파랑 중 하나만 써도, 둘이나 셋을 섞어도 돼!";

    static int Room(int chapterNumber) => Mathf.Clamp(chapterNumber - 2, 0, RoomOpening.Length - 1);

    /// <summary>2~4챕터 물건 대화. 첫 물건은 챕터 시작 장면 + 물건 대화</summary>
    public static Line[] RoomIntroLines(int chapterNumber, int item)
    {
        int c = Room(chapterNumber);
        Line[] lines = RoomItemIntro[c][Mathf.Clamp(item, 0, RoomItemIntro[c].Length - 1)];
        if (item != 0) return lines;
        var all = new Line[RoomOpening[c].Length + lines.Length];
        RoomOpening[c].CopyTo(all, 0);
        lines.CopyTo(all, RoomOpening[c].Length);
        return all;
    }

    public static string RoomResult(int chapterNumber, int item, bool right)
    {
        int c = Room(chapterNumber);
        string[][] table = right ? RoomDokkaebiRight[c] : RoomDokkaebiWrong[c];
        return Pick(table[Mathf.Clamp(item, 0, table.Length - 1)]);
    }

    public static string RoomBoyResult(int chapterNumber, int item, bool right)
    {
        string[] row = (right ? RoomBoyRight : RoomBoyWrong)[Room(chapterNumber)];
        return row[Mathf.Clamp(item, 0, row.Length - 1)];
    }

    public static Line[] RoomEndLines(int chapterNumber) => RoomEnd[Room(chapterNumber)];
    /// <summary>2~4챕터 시작 장면 대사 수 (첫 물건 대화 앞)</summary>
    public static int RoomOpeningLength(int chapterNumber) => RoomOpening[Room(chapterNumber)].Length;

    public static Line[] CurseStepLines(int step) => CurseStep[Mathf.Clamp(step, 0, CurseStep.Length - 1)];
    public static string CurseTakeLine(int step) => CurseTake[Mathf.Clamp(step, 0, CurseTake.Length - 1)];
    public static string CurseCounterHitLine(int step) => CurseCounterHit[Mathf.Clamp(step, 0, CurseCounterHit.Length - 1)];
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
        foreach (Line[] c in RoomOpening) foreach (Line l in c) yield return l.text;
        foreach (Line[][] c in RoomItemIntro) foreach (Line[] item in c) foreach (Line l in item) yield return l.text;
        foreach (string[][][] table in new[] { RoomDokkaebiRight, RoomDokkaebiWrong })
            foreach (string[][] c in table) foreach (string[] item in c) foreach (string l in item) yield return l;
        foreach (string[][] table in new[] { RoomBoyRight, RoomBoyWrong }) foreach (string[] c in table) foreach (string l in c) yield return l;
        foreach (Line[] c in RoomEnd) foreach (Line l in c) yield return l.text;
        foreach (string l in new[] { GiveBoyNoCandy, PenDraw, PenUndo, PenDone, MixBrush, MixHow }) yield return l;
        foreach (Line l in CurseIntro) yield return l.text;
        foreach (Line[] step in CurseStep) foreach (Line l in step) yield return l.text;
        foreach (string l in CurseTake) yield return l;
        foreach (string l in CurseCounterHit) yield return l;
        foreach (string l in new[] { CurseBoyTaken, CurseGiveBoy, CurseBoyHit, CurseCounterWrong, CurseBoyWrong, CurseSoulTaken,
                                     CurseGameOver, CurseHappy, CurseHappyNoBat })
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

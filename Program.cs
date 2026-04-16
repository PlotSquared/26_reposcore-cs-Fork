using Cocona;
using RepoScore.Data;
using RepoScore.Services;

var app = CoconaApp.Create();

app.AddCommand(async (
    [Argument] string repo,
    [Option('t', Description = "GitHub Personal Access Token")] string? token = null,
    [Option("show-claims", Description = "최근 이슈 선점 현황 조회")] bool showClaims = false,
    [Option("keywords", Description = "선점 키워드 목록 (쉼표 구분, 예: \"제가 하겠습니다,할게요\")")] string? keywords = null
) =>
{
    if (showClaims)
    {
        if (string.IsNullOrEmpty(token))
            token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("GitHub 토큰이 필요합니다.");
            return;
        }

        // 쉼표 기준으로 분리, 앞뒤 공백 제거, 빈 항목 제외
        string[]? claimKeywords = null;
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            claimKeywords = keywords
                .Split(',')
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToArray();
        }

        var parts = repo.Split('/');
        var service = new GitHubService(parts[0], parts[1], token, claimKeywords);
        await service.ShowRecentClaimsAsync();
        return;
    }

    Console.WriteLine($"저장소: {repo}");

    if (!string.IsNullOrEmpty(token))
    {
        Console.WriteLine($"토큰 인증 사용 중 (토큰: {token[..Math.Min(4, token.Length)]}***)");
    }
    else
    {
        Console.WriteLine("토큰 미입력 - 비인증 모드로 실행");
    }

    Console.WriteLine();
    Console.WriteLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");
    // 메서드 파라미터 순서: (기능/버그PR, 문서PR, 오타PR, 기능/버그이슈, 문서이슈)
    int user1Score = ScoreCalculator.CalculateFinalScore(1, 3, 1, 2, 1);
    Console.WriteLine($"user1, 1, 2, 1, 3, 1, {user1Score}");

    int user2Score = ScoreCalculator.CalculateFinalScore(2, 3, 5, 2, 1);
    Console.WriteLine($"user2, 1, 2, 5, 3, 2, {user2Score}");

    int user3Score = ScoreCalculator.CalculateFinalScore(5, 6, 5, 2, 3);
    Console.WriteLine($"user3, 3, 2, 5, 6, 5, {user3Score}");
});

app.Run();

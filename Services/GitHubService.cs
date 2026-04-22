using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Octokit; 
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace RepoScore.Services
{
    public enum GitHubIssuePrLabel
    {
        None, Bug, Documentation, Duplicate, Enhancement, GoodFirstIssue,
        HelpWanted, Invalid, Pinned, Question, Typo, Wontfix
    }

    public enum IssueClosedStateReason
    {
        None,
        Completed,
        Duplicate,
        NotPlanned
    }

    // 구조화된 반환을 위한 데이터 모델
    public class ClaimRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool HasPr { get; set; }
        public IssueClosedStateReason ClosedReason { get; set; } = IssueClosedStateReason.None;
        public TimeSpan Remaining { get; set; }
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
    }

    public class ClaimsData
    {
        public Dictionary<string, List<ClaimRecord>> ClaimedMap { get; set; } = new();
        public List<string> UnclaimedUrls { get; set; } = new();
    }

    public class PRRecord
    {
        public int Number { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsMerged { get; set; } = false;
        public List<GitHubIssuePrLabel> Labels { get; set; } = new();
    }

    public class PRData
    {
        public Dictionary<string, List<PRRecord>> PullRequestsByAuthor { get; set; } = new();
        public List<string> AllUrls { get; set; } = new();
    }

    public class GitHubService
    {
        private readonly Octokit.GraphQL.Connection _graphQLConnection;
        private readonly Octokit.GitHubClient _restClient;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _token;
        private static readonly HttpClient s_httpClient = new HttpClient();

        private static readonly string[] s_claimKeywords = ["제가 하겠습니다", "진행하겠습니다", "할게요", "I'll take this"];

        public GitHubService(string owner, string repo, string token)
        {
            _owner = owner;
            _repo = repo;
            _token = token;
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));

            // 1. GraphQL 커넥션 초기화
            _graphQLConnection = new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue("reposcore-cs"), token);

            // 2. REST API 클라이언트 초기화
            _restClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("reposcore-cs"))
            {
                Credentials = new Octokit.Credentials(token)
            };
        }

        public List<PRRecord> GetPullRequests(string authorLogin)
        {
            var query = new Octokit.GraphQL.Query()
                .Search(query: $"repo:{_owner}/{_repo} is:pr author:{authorLogin}", type: SearchType.Issue, first: 50)
                .Nodes
                .OfType<Octokit.GraphQL.Model.PullRequest>()
                .Select(pr => new
                {
                    pr.Number,
                    pr.Title,
                    pr.Url,
                    pr.Merged, // main 브랜치의 IsMerged 요구사항 통합
                    Labels = pr.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList()
                });

            var result = _graphQLConnection.Run(query).Result;
            var prRecords = new List<PRRecord>();

            foreach (var pr in result)
            {
                prRecords.Add(new PRRecord
                {
                    Number = pr.Number,
                    Title = pr.Title,
                    Url = pr.Url,
                    IsMerged = pr.Merged,
                    Labels = pr.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList()
                });
            }

            return prRecords;
        }

        public List<ClaimRecord> GetClaims(string authorLogin)
        {
            // HttpClient와 JsonDocument를 사용하여 직접 GraphQL API 호출
            const string graphQLQuery = @"
                query($search: String!) {
                  search(query: $search, type: ISSUE, first: 50) {
                    nodes {
                      ... on Issue {
                        number
                        title
                        url
                        stateReason
                        labels(first: 10) {
                          nodes {
                            name
                          }
                        }
                      }
                    }
                  }
                }
            ";

            var requestBody = new
            {
                query = graphQLQuery,
                variables = new { search = $"repo:{_owner}/{_repo} is:issue author:{authorLogin}" }
            };

            var claimRecords = new List<ClaimRecord>();

            try
            {
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql")
                {
                    Content = jsonContent
                };
                request.Headers.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
                request.Headers.Add("User-Agent", "reposcore-cs");

                var response = s_httpClient.SendAsync(request).Result;

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"GitHub API 요청 실패: HTTP {(int)response.StatusCode}");
                    return claimRecords;
                }

                var body = response.Content.ReadAsStringAsync().Result;
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                // 에러 확인
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    if (errors.GetArrayLength() > 0)
                    {
                        Console.WriteLine("GraphQL 오류가 발생했습니다:");
                        foreach (var error in errors.EnumerateArray())
                        {
                            if (error.TryGetProperty("message", out var errorMessage))
                            {
                                Console.WriteLine($" - {errorMessage.GetString()}");
                            }
                        }
                        return claimRecords;
                    }
                }

                // 데이터 추출
                if (!root.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("search", out var search) ||
                    !search.TryGetProperty("nodes", out var nodes))
                {
                    return claimRecords;
                }

                foreach (var issue in nodes.EnumerateArray())
                {
                    var claimClosedReason = IssueClosedStateReason.None;

                    // stateReason을 문자열로 직접 처리 (DUPLICATE 포함 모든 값 수용)
                    if (issue.TryGetProperty("stateReason", out var stateReasonProp) && 
                        stateReasonProp.ValueKind == JsonValueKind.String)
                    {
                        var reasonStr = stateReasonProp.GetString()?.ToUpperInvariant() ?? "";
                        claimClosedReason = reasonStr switch
                        {
                            "COMPLETED" => IssueClosedStateReason.Completed,
                            "DUPLICATE" => IssueClosedStateReason.Duplicate,
                            "NOTPLANNED" or "NOT_PLANNED" => IssueClosedStateReason.NotPlanned,
                            _ => IssueClosedStateReason.None
                        };
                    }

                    // 라벨 추출
                    var labels = new List<GitHubIssuePrLabel>();
                    if (issue.TryGetProperty("labels", out var labelsObj) &&
                        labelsObj.TryGetProperty("nodes", out var labelNodes))
                    {
                        foreach (var label in labelNodes.EnumerateArray())
                        {
                            if (label.TryGetProperty("name", out var labelName))
                            {
                                var parsedLabel = ParseGitHubLabel(labelName.GetString() ?? "");
                                if (parsedLabel != GitHubIssuePrLabel.None)
                                {
                                    labels.Add(parsedLabel);
                                }
                            }
                        }
                    }

                    var number = 0;
                    var title = string.Empty;
                    var url = string.Empty;

                    if (issue.TryGetProperty("number", out var numberProp) && numberProp.ValueKind == JsonValueKind.Number)
                    {
                        number = numberProp.GetInt32();
                    }

                    if (issue.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                    {
                        title = titleProp.GetString() ?? "";
                    }

                    if (issue.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                    {
                        url = urlProp.GetString() ?? "";
                    }

                    claimRecords.Add(new ClaimRecord
                    {
                        Number = number,
                        Title = title,
                        Url = url,
                        ClosedReason = claimClosedReason,
                        Labels = labels
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetClaims 메서드 실행 중 오류 발생: {ex.Message}");
            }

            return claimRecords;
        }

        public List<string> GetPullRequestComments(int prNumber)
        {
            var query = new Octokit.GraphQL.Query()
                .Repository(_owner, _repo)
                .PullRequest(prNumber)
                .Comments(first: 50)
                .Nodes.Select(c => c.Body);

            return _graphQLConnection.Run(query).Result.ToList();
        }

        private bool HasLinkedPullRequest(int issueNumber)
        {
            try
            {
                var query = new Octokit.GraphQL.Query()
                    .Repository(_owner, _repo)
                    .Issue(issueNumber)
                    .TimelineItems(first: 50)
                    .Nodes
                    .OfType<CrossReferencedEvent>()
                    .Select(e => e.Url);

                var timelineUrls = _graphQLConnection.Run(query).Result;

                return timelineUrls.Any(url => !string.IsNullOrEmpty(url) && url.Contains("/pull/"));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDocumentTask(List<GitHubIssuePrLabel> issueLabels)
        {
            return issueLabels.Contains(GitHubIssuePrLabel.Documentation) || issueLabels.Contains(GitHubIssuePrLabel.Typo);
        }

        private static GitHubIssuePrLabel ParseGitHubLabel(string labelName)
        {
            if (string.IsNullOrEmpty(labelName)) return GitHubIssuePrLabel.None;

            var normalized = labelName.ToLowerInvariant().Replace(" ", "").Replace("-", "");
            return normalized switch
            {
                "bug" => GitHubIssuePrLabel.Bug,
                "documentation" => GitHubIssuePrLabel.Documentation,
                "duplicate" => GitHubIssuePrLabel.Duplicate,
                "enhancement" => GitHubIssuePrLabel.Enhancement,
                "goodfirstissue" => GitHubIssuePrLabel.GoodFirstIssue,
                "helpwanted" => GitHubIssuePrLabel.HelpWanted,
                "invalid" => GitHubIssuePrLabel.Invalid,
                "pinned" => GitHubIssuePrLabel.Pinned,
                "question" => GitHubIssuePrLabel.Question,
                "typo" => GitHubIssuePrLabel.Typo,
                "wontfix" => GitHubIssuePrLabel.Wontfix,
                _ => GitHubIssuePrLabel.None,
            };
        }

        public ClaimsData GetRecentClaimsData()
        {
            var query = new Octokit.GraphQL.Query()
                .Repository(_owner, _repo)
                .Issues(first: 20, states: new[] { IssueState.Open }, orderBy: new IssueOrder { Field = IssueOrderField.CreatedAt, Direction = OrderDirection.Desc })
                .Nodes.Select(issue => new
                {
                    issue.Number,
                    issue.Url,
                    Labels = issue.Labels(10, null, null, null, null).Nodes.Select(l => l.Name).ToList(),
                    Comments = issue.Comments(10, null, null, null, null).Nodes.Select(c => new
                    {
                        c.Body,
                        c.CreatedAt,
                        AuthorLogin = c.Author.Login
                    }).ToList()
                });

            var result = _graphQLConnection.Run(query).Result;
            var now = DateTimeOffset.UtcNow;
            var claimsData = new ClaimsData();

            foreach (var issue in result)
            {
                var issueLabels = issue.Labels.Select(ParseGitHubLabel).Where(l => l != GitHubIssuePrLabel.None).ToList();
                var isClaimed = false;

                foreach (var comment in issue.Comments)
                {
                    if ((now - comment.CreatedAt).TotalHours > 48) continue;

                    var login = comment.AuthorLogin ?? "unknown";

                    if (s_claimKeywords.Any(k => comment.Body.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        var deadlineHours = IsDocumentTask(issueLabels) ? 24.0 : 48.0;
                        var remaining = comment.CreatedAt.AddHours(deadlineHours) - now;
                        var hasPr = issue.Number > 0 && HasLinkedPullRequest(issue.Number);

                        if (!claimsData.ClaimedMap.ContainsKey(login))
                            claimsData.ClaimedMap[login] = new List<ClaimRecord>();

                        claimsData.ClaimedMap[login].Add(new ClaimRecord
                        {
                            Number = issue.Number,
                            Url = issue.Url,
                            HasPr = hasPr,
                            Remaining = remaining,
                            Labels = issueLabels
                        });
                        isClaimed = true;
                        break;
                    }
                }

                if (!isClaimed) claimsData.UnclaimedUrls.Add(issue.Url);
            }

            return claimsData;
        }

        public List<string> GetAllContributors()
        {
            try
            {
                var contributors = _restClient.Repository.GetAllContributors(_owner, _repo).Result;
                return contributors.Select(c => c.Login).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"기여자 목록 조회 실패: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
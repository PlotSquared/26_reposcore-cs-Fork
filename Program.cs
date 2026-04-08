using Cocona;

var app = CoconaApp.Create();

app.AddCommand(([Argument] string repo) =>
{
    Console.WriteLine($"저장소: {repo}");
    Console.WriteLine();
    Console.WriteLine("아이디, 문서이슈, 버그/기능이슈, 오타PR, 문서PR, 버그/기능PR, 총점");
    Console.WriteLine("user1, 1, 2, 1, 3, 1, 100");
    Console.WriteLine("user2, 1, 2, 5, 3, 2, 120");
    Console.WriteLine("user3, 3, 2, 5, 6, 5, 150");
});

app.Run();
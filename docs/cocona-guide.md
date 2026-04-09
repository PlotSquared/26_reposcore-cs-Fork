# Cocona 라이브러리 가이드

> 이 문서는 C# CLI 프로그램 개발을 위해 채택된 **Cocona** 라이브러리의 설치 방법, 기본 구조, 사용 예시를 설명합니다.


## Cocona란?

Cocona는 .NET 콘솔 애플리케이션을 빠르고 쉽게 만들 수 있는 마이크로 프레임워크입니다.
ASP.NET Core의 Minimal API 스타일과 유사한 방식으로 CLI 명령어를 정의할 수 있습니다.

특징

- ASP.NET Core Minimal API 스타일의 직관적인 API
- 메서드 파라미터를 자동으로 CLI 옵션/인자로 변환
- HELP 메시지 자동 생성 (--help)
---

## 설치 방법

프로젝트 루트 디렉토리에서 아래 명령어를 터미널에 입력합니다.

```
dotnet add package Cocona
```

---

## 기본 구조

Cocona 앱은 크게 두 가지 방식으로 작성할 수 있습니다.

### 방식 1 — 람다(Minimal API 스타일)

`CoconaApp.Run` 또는 `CoconaApp.Create`를 사용해 람다로 커맨드를 등록합니다.
메서드 파라미터 이름이 곧 `--옵션명`이 됩니다.

```csharp
using Cocona;

CoconaApp.Run((string name) =>
{
    Console.WriteLine($"Hello, {name}!");
});
```

실행:
```
$ dotnet run -- --name World
Hello, World!
```

### 방식 2 — 클래스 기반

클래스의 `public` 메서드가 자동으로 커맨드로 등록됩니다.
커맨드가 여러 개일 때 구조를 나누기 편리합니다.

```csharp
using Cocona;

CoconaApp.Run<MyCommands>(args);

class MyCommands
{
    public void Hello(string name)
    {
        Console.WriteLine($"Hello, {name}!");
    }
}
```

실행:
```
$ dotnet run -- hello --name World
Hello, World!
```

---

## 간단한 실행 코드

`--token` 옵션을 받아 출력하는 예시입니다.

```csharp
using Cocona;

CoconaApp.Run(([Option('t', Description = "GitHub 액세스 토큰")] string token) =>
{
    Console.WriteLine($"토큰: {token}");
});
```

실행:
```
$ dotnet run -- --token abc123
토큰: abc123

$ dotnet run -- -t abc123
토큰: abc123
```

자동 생성되는 도움말:
```
$ dotnet run -- --help
Usage: MyApp [--token <String>] [--help] [--version]

Options:
  -t, --token <String>    GitHub 액세스 토큰 (Required)
  -h, --help              Show help message
  --version               Show version
```

---

## 참고 링크

- [Cocona GitHub (archived)](https://github.com/mayuki/Cocona)
- [NuGet — Cocona](https://www.nuget.org/packages/Cocona)
- [NuGet — Cocona.Lite](https://www.nuget.org/packages/Cocona.Lite)

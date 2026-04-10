cd 25-reposcore-cs

DOTNET_ROLL_FORWARD=Major dotnet run -- oss2026hnu/reposcore-cs --token $GITHUB_TOKEN

DOTNET_ROLL_FORWARD=Major dotnet run --project 25-reposcore-cs/reposcore-cs.csproj -- oss2026hnu/reposcore-cs --token $GITHUB_TOKEN

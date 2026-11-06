FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Packages.props global.json ./
COPY performance/tools/Perf.Core performance/tools/Perf.Core
COPY performance/tools/PerfAgent performance/tools/PerfAgent
RUN dotnet publish performance/tools/PerfAgent/PerfAgent.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PerfAgent.dll"]

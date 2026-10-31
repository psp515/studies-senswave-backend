FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Packages.props global.json ./
COPY src/Shared/Senswave.Contracts src/Shared/Senswave.Contracts
COPY performance/tools performance/tools
RUN dotnet publish performance/tools/PerfRunner/PerfRunner.csproj -c Release -o /app \
 && dotnet publish performance/tools/PerfStream/PerfStream.csproj -c Release -o /app/stream

FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=grafana/k6:1.3.0 /usr/bin/k6 /usr/bin/k6
COPY --from=build /app /app
COPY src/Shared/Senswave.Contracts/Protos /work/src/Shared/Senswave.Contracts/Protos
COPY performance/k6 /work/performance/k6
WORKDIR /work/performance
ENTRYPOINT ["dotnet", "/app/PerfRunner.dll"]

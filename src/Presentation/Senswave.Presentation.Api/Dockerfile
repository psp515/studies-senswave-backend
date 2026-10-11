ARG BUILD_IMAGE_CONFIGURATION=release

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

COPY . .
WORKDIR /src/Presentation/Senswave.Presentation.Api

RUN dotnet restore "./Senswave.Api.csproj"
RUN dotnet build "./Senswave.Api.csproj" -c $BUILD_CONFIGURATION
RUN dotnet publish "./Senswave.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS build-release
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update && apt-get install -y curl

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS build-debug
WORKDIR /app
COPY --from=build /app/publish .

RUN apt-get update && apt-get install -y curl unzip procps
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

FROM build-${BUILD_IMAGE_CONFIGURATION} AS final

WORKDIR /app

EXPOSE 5001
EXPOSE 8443

ENTRYPOINT ["dotnet", "Senswave.Api.dll"]
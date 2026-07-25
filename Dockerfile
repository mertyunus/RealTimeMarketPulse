FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["RealTimeMarketPulse.sln", "./"]
COPY ["src/RealTimeMarketPulse.Domain/RealTimeMarketPulse.Domain.csproj", "src/RealTimeMarketPulse.Domain/"]
COPY ["src/RealTimeMarketPulse.Infrastructure/RealTimeMarketPulse.Infrastructure.csproj", "src/RealTimeMarketPulse.Infrastructure/"]
COPY ["src/RealTimeMarketPulse.Workers/RealTimeMarketPulse.Workers.csproj", "src/RealTimeMarketPulse.Workers/"]
COPY ["src/RealTimeMarketPulse.API/RealTimeMarketPulse.API.csproj", "src/RealTimeMarketPulse.API/"]

RUN dotnet restore "RealTimeMarketPulse.sln"

COPY . .
WORKDIR "/src/src/RealTimeMarketPulse.API"
RUN dotnet publish "RealTimeMarketPulse.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RealTimeMarketPulse.API.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TestTradeService.sln", "."]
COPY ["src/TestTradeService/TestTradeService.csproj", "src/TestTradeService/"]
RUN dotnet restore "src/TestTradeService/TestTradeService.csproj"

COPY . .
RUN dotnet publish "src/TestTradeService/TestTradeService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TestTradeService.dll"]

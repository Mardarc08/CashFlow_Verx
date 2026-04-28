FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /
COPY ["CashFlow/Consolidado/Consolidado.csproj", "CashFlow/Consolidado/"]
RUN dotnet restore "CashFlow/Consolidado/Consolidado.csproj"

COPY CashFlow/Consolidado/ CashFlow/Consolidado/
WORKDIR "/CashFlow/Consolidado"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Consolidado.dll"]

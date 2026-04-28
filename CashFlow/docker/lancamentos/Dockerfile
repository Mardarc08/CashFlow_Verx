FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /
COPY ["CashFlow/Lancamentos/Lancamentos.csproj", "CashFlow/Lancamentos/"]
RUN dotnet restore "CashFlow/Lancamentos/Lancamentos.csproj"

COPY CashFlow/Lancamentos/ CashFlow/Lancamentos/
WORKDIR "/CashFlow/Lancamentos"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Lancamentos.dll"]

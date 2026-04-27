FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /
COPY ["Consolidado/Consolidado.csproj", "Consolidado/"]
RUN dotnet restore "Consolidado/Consolidado.csproj"

COPY Consolidado/ Consolidado/
WORKDIR "/Consolidado"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Consolidado.dll"]

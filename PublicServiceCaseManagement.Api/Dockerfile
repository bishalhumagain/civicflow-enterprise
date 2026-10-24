FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PublicServiceCaseManagement.Api/PublicServiceCaseManagement.Api.csproj", "PublicServiceCaseManagement.Api/"]
RUN dotnet restore "PublicServiceCaseManagement.Api/PublicServiceCaseManagement.Api.csproj"
COPY . .
WORKDIR "/src/PublicServiceCaseManagement.Api"
RUN dotnet build "PublicServiceCaseManagement.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PublicServiceCaseManagement.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PublicServiceCaseManagement.Api.dll"]

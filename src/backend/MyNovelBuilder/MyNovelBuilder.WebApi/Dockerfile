FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj", "MyNovelBuilder.WebApi/"]
RUN dotnet restore "MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj"
COPY . .
WORKDIR "/src/MyNovelBuilder.WebApi"
RUN dotnet build "MyNovelBuilder.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "MyNovelBuilder.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyNovelBuilder.WebApi.dll"]

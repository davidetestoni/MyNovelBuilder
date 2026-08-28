# syntax=docker/dockerfile:1

FROM node:22-bookworm-slim AS frontend-build
WORKDIR /src/frontend

COPY src/frontend/my-novel-builder/package.json \
     src/frontend/my-novel-builder/package-lock.json ./
RUN npm ci

COPY src/frontend/my-novel-builder/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS backend-build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY global.json ./
COPY src/backend/MyNovelBuilder/Directory.Build.props \
     src/backend/MyNovelBuilder/
COPY src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj \
     src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/packages.lock.json \
     src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/
RUN dotnet restore \
    src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj \
    --locked-mode

COPY src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/ \
     src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/
RUN dotnet publish \
    src/backend/MyNovelBuilder/MyNovelBuilder.WebApi/MyNovelBuilder.WebApi.csproj \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:BuildAngular=false

COPY --from=frontend-build \
     /src/frontend/dist/my-novel-builder/browser/ \
     /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    MYNOVELBUILDER_DATA_DIR=/data

WORKDIR /app
COPY --from=backend-build /app/publish/ ./

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir --parents /data \
    && chown "$APP_UID:$APP_UID" /data

USER $APP_UID
EXPOSE 8080
VOLUME ["/data"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health/ready > /dev/null || exit 1

ENTRYPOINT ["dotnet", "MyNovelBuilder.WebApi.dll"]

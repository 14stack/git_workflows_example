# syntax=docker/dockerfile:1
#
# Targets:
#   runtime   – the Jobs API (default, last stage)
#   migrator  – EF Core migration bundle, run once by the `db-migrate` compose service
ARG DOTNET_VERSION=10.0

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first, in its own layer, so source edits don't invalidate the package cache.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY .config/dotnet-tools.json .config/
COPY src/JobsApi/JobsApi.csproj src/JobsApi/packages.lock.json src/JobsApi/
RUN dotnet restore src/JobsApi/JobsApi.csproj --locked-mode \
 && dotnet tool restore

COPY src/ src/
ARG APP_VERSION=0.0.0-dev
RUN dotnet publish src/JobsApi/JobsApi.csproj -c Release -o /out/app --no-restore \
      -p:UseAppHost=false -p:InformationalVersion=${APP_VERSION} \
 && dotnet ef migrations bundle --project src/JobsApi --configuration Release --no-build -o /out/migrations/efbundle

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled AS migrator
WORKDIR /migrations
COPY --from=build /out/migrations/ ./
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------------------------------------------------------------------------
# Chiseled = distroless Ubuntu: no shell, no package manager, non-root by default.
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /out/app/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "JobsApi.dll"]

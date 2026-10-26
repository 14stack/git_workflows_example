# syntax=docker/dockerfile:1
#
# Targets:
#   runtime   – the Jobs API (default, last stage)
#   migrator  – EF Core migration bundle, run once by the `db-migrate` compose service
#
# Base images are pinned by tag + digest (reproducible builds); Dependabot bumps both.

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d AS migrator
WORKDIR /migrations
COPY --from=build /out/migrations/ ./
USER $APP_UID
ENTRYPOINT ["./efbundle"]

# ---------------------------------------------------------------------------
# Chiseled = distroless Ubuntu: no shell, no package manager, non-root by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0
COPY --from=build /out/app/ ./
USER $APP_UID
EXPOSE 8080
# `migrator` and `runtime` are separate build targets, each with its own entrypoint.
# nosemgrep: multiple-entrypoint-instructions
ENTRYPOINT ["dotnet", "JobsApi.dll"]

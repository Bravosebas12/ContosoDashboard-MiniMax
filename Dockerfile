# syntax=docker/dockerfile:1.6
# =============================================================================
# ContosoDashboard — Blazor Server (.NET 8)
# Multi-stage build: SDK → ASP.NET runtime, non-root user, healthcheck.
# Target image: ~200 MB (aspnet:8.0) instead of ~800 MB (sdk:8.0).
# =============================================================================

# -------- Stage 1: build & publish -------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Restore layer (cache-friendly: copy csproj/slnx first, restore, then copy the rest)
COPY ContosoDashboard.slnx ./
COPY ContosoDashboard/ContosoDashboard.csproj ContosoDashboard/
RUN dotnet restore "ContosoDashboard/ContosoDashboard.csproj"

# Copy source and publish
COPY . .
RUN dotnet publish "ContosoDashboard/ContosoDashboard.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# -------- Stage 2: runtime ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG BUILD_CONFIGURATION=Release

# OCI labels (metadata for registries, image scanners, etc.)
LABEL org.opencontainers.image.title="ContosoDashboard" \
      org.opencontainers.image.description="Blazor Server dashboard for document management (training project)" \
      org.opencontainers.image.source="https://github.com/contoso/ContosoDashboard" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.vendor="Contoso"

WORKDIR /app

# Non-root user (UID 1000) — follows OWASP A01 least-privilege guidance.
# The numeric UID is important for Kubernetes Pod Security Standards (restricted PSA).
RUN groupadd --gid 1000 app \
    && useradd  --uid 1000 --gid app --shell /bin/bash --create-home app

# Copy published artifacts with correct ownership
COPY --from=build --chown=app:app /app/publish .

# Persistent storage for uploaded documents (mounted as a named volume in compose).
# Pre-create the directory with the right ownership so the volume inherits it on first mount.
RUN mkdir -p /app/AppData/uploads \
    && chown -R app:app /app/AppData

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8

USER app
EXPOSE 8080

# Lightweight healthcheck using the built-in Kestrel endpoint.
# Adjust path if you add a /health endpoint to the Blazor app.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "ContosoDashboard.dll"]

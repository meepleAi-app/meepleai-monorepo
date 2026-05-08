# Dockerfile API — multi-stage build con OOM mitigation (Iter 2 Patch 1+2+3).
#
# Memory profile pre-Iter 2: peak ~2-3GB durante `dotnet publish` Release.
# Cause primarie:
#   - 3 analyzer suites attivi simultaneamente (SonarAnalyzer dominante ~600-1000MB)
#   - Server GC default Release (high-throughput, high-memory)
#   - NuGet restore graph re-download ogni build
#
# Mitigations applicati (~1-1.8GB peak savings):
#   - Patch 1: -p:SkipAnalyzers=true → disabilita analyzer durante publish
#              (analyzer girano in CI Release + dev locale Debug)
#   - Patch 2: --mount=type=cache NuGet → riduce I/O memory pressure
#   - Patch 3: DOTNET_GCServer=0 → workstation GC (lower peak vs higher throughput)

# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Patch 3: workstation GC mode durante build (memory peak ridotto ~30%)
ENV DOTNET_GCServer=0
ENV DOTNET_GCConcurrent=1

COPY apps/api/src/Api/Api.csproj ./apps/api/src/Api/

# Patch 2: NuGet cache mount (BuildKit) → restore reuse cross-build
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore ./apps/api/src/Api/Api.csproj

COPY apps/api/src/Api ./apps/api/src/Api
COPY data ./data

# Patch 1: SkipAnalyzers=true durante publish (analyzer suite consumer
#          principale di memory peak — SonarAnalyzer + NetAnalyzers + Meziantou).
# Patch 2: NuGet cache mount riusato dal restore step → no re-download packages.
#          NB: NO --no-restore qui — il publish ha bisogno di project's obj/
#          (project.assets.json) che NON persiste cross-layer; cache mount copre
#          solo /root/.nuget/packages. Il restore con cache è veloce comunque.
# GenerateDocumentationFile=false: -10MB image size + minor memory savings.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish ./apps/api/src/Api/Api.csproj \
        -c Release \
        -o /app/publish \
        -p:SkipAnalyzers=true \
        -p:GenerateDocumentationFile=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install curl for healthcheck, create non-root user, prepare directories — single layer
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && adduser --disabled-password --gecos '' --uid 1001 apiuser \
    && mkdir -p /app/pdf_uploads

# Copy published files and remove debug/doc artifacts to reduce image size (~10MB savings)
COPY --from=build --chown=apiuser:apiuser /app/publish .
RUN find . -name "*.pdb" -delete && find . -name "*.xml" -delete \
    && chown -R apiuser:apiuser /app/pdf_uploads

# Switch to non-root user
USER apiuser

EXPOSE 8080
ENTRYPOINT ["dotnet","Api.dll"]

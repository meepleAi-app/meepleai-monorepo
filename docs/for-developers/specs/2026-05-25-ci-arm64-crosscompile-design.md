# CI Onda B — ARM64 Native Cross-Compile (Docker build) Design

**Date**: 2026-05-25
**Status**: Approved (brainstorming) — pending implementation plan
**Author**: /sc:spec-panel + brainstorming session
**Related**: [[2026-05-25-ci-build-once-test-many]] (Onda 2 + Onda A, merged via #1527/#1530)

## Problem

`deploy-staging.yml` builds ARM64 Docker images (API + Web) via `docker buildx --platform linux/arm64` with QEMU emulation. The expensive build steps (`dotnet publish`, `pnpm install`/`next build`) run **emulated** under QEMU, taking ~29 min — 56% of the ~52 min staging deploy wall-clock. This is the single largest deploy bottleneck.

## Goal

Eliminate QEMU emulation for the heavy build steps by **cross-compiling natively on the x64 runner**, keeping only the lightweight runtime stage on ARM64. Target: −15-22 min on the `build` job.

**Constraints**:
- **Zero cost** (no GitHub-hosted ARM64 runners — paid for private repos)
- **Zero new infra** (no Oracle VM provisioning / runner maintenance)
- Fully implementable in-repo (Dockerfiles + workflow comment)

## Key Insight — Why this works

Docker buildx sets two platform args when building with `--platform linux/arm64` on an x64 host:
- `BUILDPLATFORM` = `linux/amd64` (the runner's native arch)
- `TARGETPLATFORM` = `linux/arm64` (the image we want)

By pinning the **build stage** to `--platform=$BUILDPLATFORM`, that stage runs **natively on x64** (fast). Only the final **runtime stage** stays on the target ARM64 platform (QEMU, but it does only `apt-get`/`apk add` + `COPY` — seconds, not minutes).

- **.NET**: `dotnet publish -a $TARGETARCH` cross-compiles to ARM64 from an x64 SDK natively (.NET 8+ official feature). No emulation.
- **Next.js**: `pnpm build` output (`.next/standalone`) is **pure JS, architecture-independent**. Node ARM64 runs portable JS. The only native module, `canvas`, is **test-only** (transitive via `jsdom` devDependency → vitest; not a direct dependency, not imported in `src/`, tree-shaken out of `.next/standalone`). It never reaches the ARM64 runtime.

## Design

### Unit 1 — API Dockerfile (`apps/api/src/Api/Dockerfile`)

**Responsibility**: produce an ARM64 runtime image of the .NET API, building natively on x64.

**Change**: build stage pinned to `$BUILDPLATFORM`; `-a $TARGETARCH` on restore + publish.

```dockerfile
# syntax=docker/dockerfile:1.6
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src
ENV DOTNET_GCServer=0
ENV DOTNET_GCConcurrent=1
COPY apps/api/src/Api/Api.csproj ./apps/api/src/Api/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore -a $TARGETARCH ./apps/api/src/Api/Api.csproj
COPY apps/api/src/Api ./apps/api/src/Api
COPY data ./data
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish -a $TARGETARCH ./apps/api/src/Api/Api.csproj \
        -c Release \
        -o /app/publish \
        -p:SkipAnalyzers=true \
        -p:GenerateDocumentationFile=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
# UNCHANGED: runtime stage stays on TARGETPLATFORM (arm64).
# apt-get curl, adduser, COPY --from=build, pdb/xml cleanup, USER, ENTRYPOINT.
```

**Interface**: same image contract (ENTRYPOINT `dotnet Api.dll`, EXPOSE 8080, non-root `apiuser`). No consumer change.

**Note on NuGet cache**: the `--mount=type=cache` target is shared; with `-a arm64` the restored packages are arch-specific (runtime packages for linux-arm64). Cache key is implicitly per-build-arch by buildx; no manual change needed.

**Note on `$TARGETARCH` mapping**: Docker's `TARGETARCH` for `linux/arm64` is the string `"arm64"`, which `dotnet --arch` accepts directly. This works **only because we build `arm64` exclusively**. If a future multi-arch build added `linux/amd64`, `TARGETARCH` would be `"amd64"` but `dotnet --arch` expects `"x64"` — that case would need an explicit mapping (e.g. a `case` on `$TARGETARCH`). Out of scope now (arm64-only); the implementation plan must not hardcode an assumption that breaks if amd64 is added — leave a comment.

### Unit 2 — Web Dockerfile (`apps/web/Dockerfile`)

**Responsibility**: produce an ARM64 runtime image of the Next.js standalone server, building natively on x64.

**Change**: `deps` and `builder` stages pinned to `$BUILDPLATFORM`. Runner stage unchanged (target ARM64).

```dockerfile
FROM --platform=$BUILDPLATFORM node:20.18-alpine3.21 AS deps
# UNCHANGED body: apk add canvas build deps, pnpm install --frozen-lockfile
# (runs natively on x64; canvas compiles x64 but is test-only, never copied to runner)

FROM --platform=$BUILDPLATFORM node:20.18-alpine3.21 AS builder
# UNCHANGED body: COPY node_modules, COPY source, NEXT_PUBLIC_* args, pnpm run build
# → .next/standalone (pure JS, arch-independent)

FROM node:20.18-alpine3.21 AS runner
# UNCHANGED: stays on TARGETPLATFORM (arm64). apk add curl, adduser,
# COPY .next/standalone + static + public, node server.js
```

**Interface**: same image contract (CMD `node server.js`, EXPOSE 3000, non-root `nextjs`). No consumer change.

### Unit 3 — Workflow (`deploy-staging.yml`)

**No functional change.** `docker/build-push-action` already passes `platforms: linux/arm64`; buildx auto-populates `$BUILDPLATFORM`/`$TARGETARCH`. `setup-qemu-action` stays (needed for the lightweight runtime-stage `apt-get`/`apk add`). Add an explanatory comment near the build steps documenting the cross-compile expectation.

## Data Flow

```
docker buildx --platform linux/arm64 (on x64 runner)
  ├─ build/deps/builder stages → run on x64 (BUILDPLATFORM), native, fast
  │    .NET: dotnet publish -a arm64 → ARM64 binaries
  │    Web:  pnpm build → .next/standalone (JS, arch-independent)
  └─ runtime/runner stage → ARM64 (TARGETPLATFORM), QEMU
       only apt-get/apk + COPY → seconds
  → push ARM64 image to ghcr.io
```

## Error Handling / Failure Modes

| Failure | Detection | Recovery |
|---|---|---|
| `.next/standalone` unexpectedly needs canvas at runtime | `server.js` crashes on boot with `MODULE_NOT_FOUND canvas`; caught by `validate` + `e2e-staging` jobs | `git revert` Web Dockerfile; investigate nft trace |
| `dotnet -a arm64` produces incompatible binary | API container fails health check (`/health/live`); caught by `validate` | `git revert` API Dockerfile |
| buildx ignores `$BUILDPLATFORM` (old buildx) | Build still works (just emulated, no speedup) | Non-fatal; verify buildx version ≥ 0.10 |
| QEMU still needed but removed | N/A — we keep `setup-qemu-action` | — |

## Testing Strategy

**No local test possible** (no ARM64 Docker build environment in this dev setup). Validation is the **first real staging deploy** after merge:
1. `build` job completes (measure wall-clock vs ~29 min baseline)
2. `validate` job: API `/health/live` + `/health/ready` pass
3. `e2e-staging` job: smoke E2E against deployed ARM64 images pass

**Rollback**: `git revert` of the two Dockerfile commits restores QEMU-in-place build. The `deploy-staging.yml` is unchanged, so no workflow rollback needed.

## Scope

- **In**: `apps/api/src/Api/Dockerfile`, `apps/web/Dockerfile`, comment in `deploy-staging.yml`.
- **Out**: pre-deploy-check redundancy removal (separate concern, P0.3/B6), migrate-db build, any infra/runner change, the 3 CPU-side deferred items (archived — self-hosted/free tier, no billing pressure).

## Expected Impact

- `build` job: ~29 min → ~7-12 min (estimate; heavy steps go native, only runtime stage emulated). **−15-22 min** on the staging deploy critical path.
- Deploy wall-clock: ~52 min → ~30-37 min.
- Zero cost, zero new infra, reversible.

## Open Questions

None blocking. The canvas-not-in-standalone assumption is the one empirical risk; validated by the first deploy's `server.js` boot.

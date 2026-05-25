# ARM64 Native Cross-Compile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate QEMU emulation from the staging Docker build by cross-compiling the .NET API and Next.js Web images natively on the x64 runner, keeping only the lightweight runtime stage on ARM64.

**Architecture:** Pin each Dockerfile's build stages to `--platform=$BUILDPLATFORM` (x64 host, native) and the runtime stage to the default target platform (ARM64). .NET uses `dotnet publish -a $TARGETARCH` for native cross-compile; Next.js produces an arch-independent `.next/standalone` (pure JS). Heavy steps run native; QEMU only handles the runtime stage's `apt-get`/`apk add` (seconds).

**Tech Stack:** Docker buildx, `mcr.microsoft.com/dotnet/sdk:9.0` (multi-arch), `node:20.18-alpine3.21` (multi-arch), hadolint.

**Spec:** `docs/for-developers/specs/2026-05-25-ci-arm64-crosscompile-design.md`

**Validation note:** The spec conservatively assumed no local ARM64 test. This dev machine has `docker buildx v0.33.0` + QEMU + `hadolint 2.14.0`, so each Dockerfile is validated locally (build ARM64 + boot test) BEFORE merge. The canvas-not-in-standalone assumption is verified definitively by booting the Web container locally.

---

## File Structure

- **Modify** `apps/api/src/Api/Dockerfile` — build stage → `$BUILDPLATFORM` + `-a $TARGETARCH` cross-compile.
- **Modify** `apps/web/Dockerfile` — `deps` + `builder` stages → `$BUILDPLATFORM`; runner unchanged.
- **Modify** `.github/workflows/deploy-staging.yml` — explanatory comment only (no functional change).

Each Dockerfile is self-contained; changes are independent and individually committable + locally verifiable.

---

## Task 1: Pre-flight + baseline

**Files:** none

- [ ] **Step 1: Confirm branch + tools**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
git branch --show-current     # → feature/ci-onda-b-arm64-crosscompile
hadolint --version            # → 2.14.0
docker buildx version         # → v0.33.0+
docker buildx ls              # confirm a builder exists; QEMU/multi-platform support
```
Expected: branch correct, all tools present.

- [ ] **Step 2: Ensure a buildx builder with multi-platform support exists**

```bash
docker buildx inspect --bootstrap 2>&1 | grep -i "platforms" || docker buildx create --use --name xbuilder
docker buildx inspect --bootstrap | grep -i "platforms"
```
Expected: output lists `linux/arm64` among platforms (QEMU registered). If not, `docker run --privileged --rm tonistiigi/binfmt --install arm64` then re-inspect.

- [ ] **Step 3: Baseline hadolint on both Dockerfiles (record pre-existing warnings)**

```bash
hadolint apps/api/src/Api/Dockerfile || true
hadolint apps/web/Dockerfile || true
```
Expected: note any pre-existing warnings so we don't attribute them to our change. (Do not fix unrelated warnings — YAGNI.)

---

## Task 2: API Dockerfile cross-compile

**Files:**
- Modify: `apps/api/src/Api/Dockerfile`

- [ ] **Step 1: Apply the cross-compile changes**

Replace the build stage header and the restore/publish commands. The full target state of the file:

```dockerfile
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
#
# ARM64 cross-compile (2026-05-25, Onda B): the build stage is pinned to
# $BUILDPLATFORM (the x64 runner) and cross-compiles to ARM64 via
# `dotnet publish -a $TARGETARCH`. This avoids running dotnet under QEMU
# emulation (was ~half of the 29-min staging build). The runtime stage stays
# on the target platform (arm64); it only does apt-get + COPY (light under QEMU).
# NB: $TARGETARCH is "arm64" for linux/arm64, which `dotnet --arch` accepts
# directly. We build arm64 ONLY — if amd64 is ever added, map "amd64"→"x64".

# syntax=docker/dockerfile:1.6
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

# Patch 3: workstation GC mode durante build (memory peak ridotto ~30%)
ENV DOTNET_GCServer=0
ENV DOTNET_GCConcurrent=1

COPY apps/api/src/Api/Api.csproj ./apps/api/src/Api/

# Patch 2: NuGet cache mount (BuildKit) → restore reuse cross-build.
# -a $TARGETARCH: restore arch-specific (linux-arm64) runtime packages.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore -a $TARGETARCH ./apps/api/src/Api/Api.csproj

COPY apps/api/src/Api ./apps/api/src/Api
COPY data ./data

# Patch 1: SkipAnalyzers=true durante publish (analyzer suite consumer
#          principale di memory peak — SonarAnalyzer + NetAnalyzers + Meziantou).
# Patch 2: NuGet cache mount riusato dal restore step → no re-download packages.
# -a $TARGETARCH: cross-compile to ARM64 natively on the x64 build host.
# GenerateDocumentationFile=false: -10MB image size + minor memory savings.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish -a $TARGETARCH ./apps/api/src/Api/Api.csproj \
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
```

The diff vs current is exactly: (a) `FROM` → `FROM --platform=$BUILDPLATFORM`, (b) add `ARG TARGETARCH` after FROM, (c) `dotnet restore` → `dotnet restore -a $TARGETARCH`, (d) `dotnet publish` → `dotnet publish -a $TARGETARCH`, (e) the new comment block.

- [ ] **Step 2: Lint**

```bash
hadolint apps/api/src/Api/Dockerfile
```
Expected: no NEW warnings vs Task 1 baseline.

- [ ] **Step 3: Local ARM64 cross-compile build (no push) — verify it produces an image natively**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
time docker buildx build --platform linux/arm64 \
  -f apps/api/src/Api/Dockerfile \
  -t meepleai-api:arm64-test \
  --load .
```
Expected: build succeeds; `dotnet publish` runs WITHOUT the slow QEMU emulation of the SDK (the build stage logs run at native speed). Image loads. Note the wall-clock `time`.

- [ ] **Step 4: Boot test — verify the ARM64 image starts (QEMU runs it on this x64 host)**

```bash
docker run --rm -d --name api-arm64-test -p 18080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ASPNETCORE_ENVIRONMENT=CI \
  meepleai-api:arm64-test
sleep 20
docker logs api-arm64-test 2>&1 | tail -20
curl -sf http://localhost:18080/health/live && echo " ✅ API ARM64 boots + healthy" || echo " ⚠️ check logs (DB unavailable is OK; we only verify the binary runs on arm64)"
docker stop api-arm64-test 2>/dev/null || true
```
Expected: the process starts and the ARM64 `Api.dll` executes (proves the cross-compiled binary is valid ARM64). `/health/live` may return unhealthy if no DB, but the binary running at all is the proof. A crash with "exec format error" would mean cross-compile failed — that must NOT happen.

- [ ] **Step 5: Commit**

```bash
docker rmi meepleai-api:arm64-test 2>/dev/null || true
git add apps/api/src/Api/Dockerfile
git commit -m "$(cat <<'EOF'
perf(ci): cross-compile API Docker image natively for ARM64

Pin the build stage to $BUILDPLATFORM (x64 runner) and cross-compile to
ARM64 via `dotnet publish -a $TARGETARCH`, avoiding QEMU emulation of the
.NET SDK (was ~half of the 29-min staging build). Runtime stage unchanged
(arm64 target, apt-get + COPY only).

Validated locally: docker buildx --platform linux/arm64 builds at native
speed; the ARM64 Api.dll boots (no "exec format error").

Spec: docs/for-developers/specs/2026-05-25-ci-arm64-crosscompile-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Web Dockerfile cross-compile

**Files:**
- Modify: `apps/web/Dockerfile`

- [ ] **Step 1: Apply the cross-compile changes**

Change only the `FROM` lines of the `deps` and `builder` stages to pin `$BUILDPLATFORM`, and add an explanatory comment. The runner stage is UNCHANGED. Exact edits:

Change line 4 from:
```dockerfile
FROM node:20.18-alpine3.21 AS deps
```
to:
```dockerfile
# ARM64 cross-compile (2026-05-25, Onda B): deps + builder pinned to
# $BUILDPLATFORM (x64 runner) → pnpm install + next build run native, not
# under QEMU. The runner stage stays on the arm64 target. The .next/standalone
# output is pure JS (arch-independent); the only native module, canvas, is
# test-only (transitive via jsdom devDependency) and is NOT in the standalone
# bundle, so node arm64 runs the portable JS without it.
FROM --platform=$BUILDPLATFORM node:20.18-alpine3.21 AS deps
```

Change line 31 from:
```dockerfile
FROM node:20.18-alpine3.21 AS builder
```
to:
```dockerfile
FROM --platform=$BUILDPLATFORM node:20.18-alpine3.21 AS builder
```

Leave line 76 (`FROM node:20.18-alpine3.21 AS runner`) UNCHANGED — the runner must build for the arm64 target.

- [ ] **Step 2: Lint**

```bash
hadolint apps/web/Dockerfile
```
Expected: no NEW warnings vs Task 1 baseline.

- [ ] **Step 3: Local ARM64 cross-compile build (no push)**

```bash
cd "D:/Repositories/meepleai-monorepo-main/apps/web"
time docker buildx build --platform linux/arm64 \
  -f Dockerfile \
  -t meepleai-web:arm64-test \
  --build-arg NEXT_PUBLIC_API_BASE=https://meepleai.app \
  --build-arg NEXT_PUBLIC_APP_URL=https://meepleai.app \
  --build-arg NEXT_PUBLIC_SITE_URL=https://meepleai.app \
  --load .
```
Expected: build succeeds; `pnpm install` + `next build` run at native x64 speed (deps/builder are $BUILDPLATFORM). Image loads.

- [ ] **Step 4: Boot test — THE canvas verification (definitive)**

```bash
docker run --rm -d --name web-arm64-test -p 13000:3000 meepleai-web:arm64-test
sleep 12
docker logs web-arm64-test 2>&1 | tail -30
curl -sf http://localhost:13000/ -o /dev/null && echo " ✅ Web ARM64 server.js boots + serves" || echo " ❌ check logs"
docker logs web-arm64-test 2>&1 | grep -i "MODULE_NOT_FOUND\|canvas\|exec format" && echo " ❌ canvas/arch problem detected" || echo " ✅ no canvas/arch errors"
docker stop web-arm64-test 2>/dev/null || true
```
Expected: `node server.js` starts and serves `/` (HTTP 200). **No `MODULE_NOT_FOUND canvas`** — this definitively confirms canvas is not in the standalone runtime (the spec's one empirical risk). No "exec format error" (cross-compile produced valid arm64 JS runtime layout).

- [ ] **Step 5: Commit**

```bash
docker rmi meepleai-web:arm64-test 2>/dev/null || true
git add apps/web/Dockerfile
git commit -m "$(cat <<'EOF'
perf(ci): cross-compile Web Docker image natively for ARM64

Pin deps + builder stages to $BUILDPLATFORM (x64 runner) so pnpm install
and next build run native instead of under QEMU. Runner stage stays on the
arm64 target; .next/standalone is pure JS (arch-independent). Confirmed
locally that node arm64 server.js boots and serves without canvas (canvas
is test-only via jsdom, tree-shaken out of standalone).

Validated locally: docker buildx --platform linux/arm64 builds native;
ARM64 server.js boots, serves /, no MODULE_NOT_FOUND canvas.

Spec: docs/for-developers/specs/2026-05-25-ci-arm64-crosscompile-design.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: deploy-staging.yml explanatory comment

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

- [ ] **Step 1: Add a comment above the "Build and Push API Image" step**

Find (around line 368):
```yaml
      - name: Build and Push API Image
        if: needs.detect-changes.outputs.deploy_api == 'true'
        uses: docker/build-push-action@bcafcacb16a39f128d818304e6c9c0c18556b85f  # v7.1.0
```
Insert a comment immediately before it:
```yaml
      # ARM64 native cross-compile (2026-05-25, Onda B): the Dockerfiles pin
      # their build stages to $BUILDPLATFORM (x64 runner) and cross-compile to
      # arm64. buildx auto-populates $BUILDPLATFORM/$TARGETARCH from the
      # `platforms:` value below. setup-qemu-action (above) is still required
      # for the lightweight runtime stage (apt-get/apk). Heavy build steps now
      # run native — see docs/for-developers/specs/2026-05-25-ci-arm64-crosscompile-design.md
      - name: Build and Push API Image
        if: needs.detect-changes.outputs.deploy_api == 'true'
        uses: docker/build-push-action@bcafcacb16a39f128d818304e6c9c0c18556b85f  # v7.1.0
```

- [ ] **Step 2: Validate YAML**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
PYTHONIOENCODING=utf-8 python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); print('YAML valid')"
```
Expected: `YAML valid`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "$(cat <<'EOF'
docs(ci): document ARM64 cross-compile in deploy-staging build step

No functional change — buildx already passes platforms: linux/arm64.
Explains that the Dockerfiles now cross-compile natively and why QEMU is
still set up (runtime stage only).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Push + PR + deploy validation

**Files:** none

- [ ] **Step 1: Push**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
git push -u origin feature/ci-onda-b-arm64-crosscompile
```

- [ ] **Step 2: Open PR to main-dev**

```bash
gh pr create --base main-dev \
  --title "perf(ci): ARM64 native cross-compile — eliminate QEMU in staging build" \
  --body "$(cat <<'EOF'
## Summary

Cross-compile the staging Docker images (API + Web) natively on the x64 runner instead of emulating ARM64 via QEMU. The build stages are pinned to `$BUILDPLATFORM`; only the runtime stage stays on arm64.

- **API**: `dotnet publish -a $TARGETARCH` cross-compiles to ARM64 from the x64 SDK natively.
- **Web**: `deps`+`builder` on `$BUILDPLATFORM`; `.next/standalone` is pure JS (arch-independent). canvas is test-only (jsdom devDependency), not in the standalone bundle.
- **Workflow**: comment-only; buildx already passes `platforms: linux/arm64`.

## Expected impact

`build` job ~29 min → ~7-12 min (heavy steps go native). Staging deploy ~52 → ~30-37 min. Zero cost, zero new infra.

## Local validation (pre-merge)

Both images built locally via `docker buildx --platform linux/arm64` and boot-tested:
- API ARM64 `Api.dll` runs (no exec-format error)
- Web ARM64 `server.js` boots, serves `/`, **no MODULE_NOT_FOUND canvas** (confirms the one empirical risk)

## Validation post-merge

First staging deploy measures the real build time + `validate`/`e2e-staging` confirm the deployed ARM64 images are healthy.

## Rollback

`git revert` the two Dockerfile commits restores QEMU-in-place build. Workflow unchanged.

Spec: `docs/for-developers/specs/2026-05-25-ci-arm64-crosscompile-design.md`
Plan: `docs/superpowers/plans/2026-05-25-ci-arm64-crosscompile.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Report local build timings to the user**

Summarize the `time` outputs from Task 2 Step 3 and Task 3 Step 3 (local native cross-compile wall-clock) as the pre-merge evidence of speedup. Note that the true staging number comes from the first deploy.

- [ ] **Step 4: Decide merge + deploy validation with the user**

The real ARM64 staging build only runs when the code reaches `main-staging` (deploy-staging triggers on push there). Options to present:
1. Merge to main-dev now; the speedup is validated when a future main-dev → main-staging promotion deploys.
2. After merge, do a controlled main-staging promotion to measure + confirm immediately.

Do not auto-promote to main-staging without user direction.

---

## Self-Review

**1. Spec coverage:**
- Unit 1 (API Dockerfile) → Task 2 ✅
- Unit 2 (Web Dockerfile) → Task 3 ✅
- Unit 3 (workflow comment) → Task 4 ✅
- Error handling (boot tests for canvas + exec-format) → Task 2 Step 4, Task 3 Step 4 ✅
- Testing strategy (local buildx validation, exceeding spec's conservative "no local test") → Tasks 2-3 ✅
- Rollback (git revert) → documented in PR body Task 5 ✅
- `$TARGETARCH` mapping note → captured in Task 2 Step 1 comment ✅

**2. Placeholder scan:** No TBD/TODO. All Dockerfile content shown in full (Task 2) or as exact line changes (Task 3, 4). Commands have expected output.

**3. Type/name consistency:**
- Image tags: `meepleai-api:arm64-test`, `meepleai-web:arm64-test` — consistent build/run/rmi.
- Ports: API 18080→8080, Web 13000→3000 — consistent within each task.
- `$BUILDPLATFORM` / `$TARGETARCH` / `-a` usage consistent across Dockerfiles and comments.
- Branch `feature/ci-onda-b-arm64-crosscompile` consistent throughout.

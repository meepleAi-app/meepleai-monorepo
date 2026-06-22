# AI Services GHCR Build & Push Pipeline Design

**Date**: 2026-05-28
**Status**: Approved (brainstorming) — pending implementation plan
**Author**: /sc:spec-panel + brainstorming session
**Issue**: [#1578](https://github.com/meepleAi-app/meepleai-monorepo/issues/1578) — infra(ai-services): 2-month drift, local build, no GHCR push pipeline
**Related**: [[2026-05-25-ci-arm64-crosscompile-design]] (api/web ARM64 build), [[2026-05-25-ci-build-once-test-many]]

## Problem

In `infra/docker-compose.yml`, the AI services use a local-only `build:` directive with **no** `image: ghcr.io/...`:

```yaml
embedding-service:
  build:
    context: ../apps/embedding-service
    dockerfile: ./Dockerfile
  # no image: → built locally as meepleai_embedding-service:latest, never refreshed
```

Unlike `api`/`web` — which `deploy-staging.yml` builds and pushes to GHCR on every merge to `main-staging` — the AI services are **never rebuilt by automation**. `docker compose up` reuses the local image as long as it exists. Result: ~50-day drift between repo code and the images running on the staging VPS (discovered during the 2026-05-26 manual recovery deploy, #1575).

**Concrete impact:**
- `orchestration-service`: fix #1073 (OPENROUTER_API_KEY guard, 2026-05-12) is in repo but not in the running staging image.
- `embedding-service` / `reranker-service`: every dependency bump (pytest #776, Hadolint #781) and security fix of the last 2 months is absent from staging.

## Goal

Build + push `embedding`, `reranker`, and `orchestration` images to GHCR with **per-service change detection**, **pulled (never built) on the ARM64 staging VPS**, under **strict disk safety**.

**Constraints** (drove the design decisions below):

- **Disk safety is a hard requirement.** The staging VPS (Hetzner CAX21, ARM64, ~75 GB disk) hit disk-full on 2026-05-26 (#1575). Current AI images ≈ 7.4 GB (embedding ~5.26 GB + reranker ~2.1 GB). The real risk is the **transient** during deploy: pulling a new image while the old one is still referenced (~10.5 GB for embedding alone) before pruning.
- **ARM64 only.** The VPS is ARM64; images must be `linux/arm64`.
- **Python torch Dockerfiles are NOT cross-compile-ready.** All 5 AI Dockerfiles use plain `FROM python:3.11-slim AS builder` (no `$BUILDPLATFORM` pinning, unlike the api/web Onda B rework). `embedding` (`torch==2.8.0`) and `reranker` (`torch`) also **pre-download their ML model at build time** by executing torch under the build stage. Building `linux/arm64` on an x64 runner therefore requires **QEMU emulation** for those torch steps.
- **Zero added runner cost.** No paid GitHub-hosted ARM64 runners.
- **Dev local-build behavior must not regress.** `make dev` must keep building the AI services locally.
- **Never build torch on the VPS.** Rebuilding embedding/reranker on the CAX21 (8 GB RAM) drives it into swap thrashing and disk-full (prior incident; see memory `vps-torch-rebuild-diskfull`). Staging must **pull**, never build.

## Key Decisions (brainstorming outcomes)

| Decision | Choice | Rationale |
|---|---|---|
| **Scope** | embedding + reranker + orchestration | The 3 services with cited real impact. smoldocling + unstructured are disabled in staging (`pdf-cloud-extractors` profile) → out of scope. |
| **ARM64 build** | QEMU on `ubuntu-latest` + per-service change detection | Zero cost, reuses existing build pattern. Slow torch builds are acceptable because they run only when that service's code/requirements change (rare, ~monthly) and on a cloud runner (never the VPS). |
| **orchestration** | Publish-only (build+push, **not** activated in staging) | Avoids adding a running container to the 8 GB VPS. Image is fresh and ready for a future `--profile tutor-agents` activation (separate decision). |
| **Disk safety** | Aggressive prune + dedicated pull gate (15 GB) + GHCR retention (keep 5) | Protects the VPS disk against the old+new transient; bounds registry storage growth over time. |
| **Architecture** | Matrix `build-ai` job inside `deploy-staging.yml` | Isolates slow torch builds (parallel, per-service timeout, `fail-fast: false`) without duplicating the SSH/deploy/disk-gate logic. |
| **build-ai timeout** | 120 min | Prudent headroom for a not-yet-measured torch+model build under QEMU; lower once real timings are known. |
| **Disk gate behavior** | Abort fail-safe | If free disk stays below the gate after pre-prune, the AI deploy stops with a clear error rather than risking disk-full. |

## Design

All changes live in `deploy-staging.yml` + the two compose files. New job graph (additions in **bold**):

```
gate → detect-changes → pre-deploy-check
pre-deploy-check + detect-changes → build (api/web)        [existing]
pre-deploy-check + detect-changes → build-ai (matrix)      ◄── NEW
build + build-ai → snapshot-baseline
build + build-ai + snapshot-baseline → deploy              ◄── extended
deploy → validate → e2e-staging
... → notify-end
deploy → ghcr-retention                                    ◄── NEW
```

### Unit 1 — `detect-changes` extension

**Responsibility**: decide which AI services changed and need rebuild.

**Change**: add 3 path filters + two outputs.

```yaml
filters: |
  api:    [ 'apps/api/**' ]
  web:    [ 'apps/web/**' ]
  infra:  [ 'infra/**', '.github/workflows/deploy-staging.yml' ]
  embedding:     [ 'apps/embedding-service/**' ]
  reranker:      [ 'apps/reranker-service/**' ]
  orchestration: [ 'apps/orchestration-service/**' ]
```

The decision step emits:
- `ai_changed` — a JSON map, e.g. `{"embedding":true,"reranker":false,"orchestration":true}`.
- `ai_any` — `"true"` if any of the three changed (gates the whole `build-ai` job).

Same force-full logic as today: on `workflow_dispatch`, `force_full_deploy=true`, an `infra` change, or initial push (`before=zeros`), all three are forced `true`.

**Interface**: downstream jobs read `needs.detect-changes.outputs.ai_changed` / `.ai_any`.

### Unit 2 — `build-ai` matrix job (NEW)

**Responsibility**: build + push the ARM64 image of each changed AI service to GHCR.

```yaml
build-ai:
  name: Build AI Images
  runs-on: ubuntu-latest
  timeout-minutes: 120
  needs: [pre-deploy-check, detect-changes]
  if: always()
     && (needs.pre-deploy-check.result == 'success'
         || (github.event_name == 'workflow_dispatch' && inputs.skip_tests == true))
     && needs.detect-changes.outputs.ai_any == 'true'
  strategy:
    fail-fast: false
    matrix:
      service: [embedding, reranker, orchestration]
  steps:
    - Checkout (sparse: apps/${{ matrix.service }}-service/)
    - Free disk space            # reuse existing step (buildx + image prune)
    - Setup Buildx
    - Setup QEMU (platforms: arm64)
    - Login GHCR
    - Extract metadata (docker/metadata-action)   # if service changed
    - Build & Push                                 # if service changed
```

**Per-service skip**: the metadata + build/push steps carry
`if: fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service]`.
An unchanged service still spins up the leg (checkout + setup) but does no build/push — a fast no-op. (Matrix legs cannot be skipped wholesale; gating the heavy steps is the idiomatic approach.)

**Tags** (via `docker/metadata-action`, same scheme as api/web):
- `ghcr.io/<repo>/<service>-service:staging-YYYYMMDD-SHA` (immutable)
- `ghcr.io/<repo>/<service>-service:staging-latest` (moving)

**Build** (`docker/build-push-action`):
- `context: ./apps/<service>-service`, `file: ./apps/<service>-service/Dockerfile`
- `platforms: linux/arm64`, `push: true`
- `cache-from/to: type=gha,scope=ai-<service>` (GHA cache isolated per service)

**No job-output aggregation.** Matrix jobs don't aggregate outputs cleanly, and we don't need them: the deploy step pulls the deterministic `:staging-latest` tag for each changed service (build-ai always pushes `staging-latest`). Same pattern as the api/web `staging-latest` default.

**Cloud-runner disk**: each matrix leg runs on its own VM and builds a **single** image (~5 GB) → comfortably within ubuntu-latest's free space after the free-disk step. The matrix parallelism also helps here.

### Unit 3 — Compose changes

**Principle**: dev builds locally as today; staging pulls from GHCR and never builds torch on the VPS.

**`infra/docker-compose.yml`** (base) — add `image:` while keeping `build:`, mirroring api/web:

```yaml
embedding-service:
  build: { context: ../apps/embedding-service, dockerfile: ./Dockerfile }
  image: ${EMBEDDING_IMAGE:-meepleai-embedding}       # NEW
reranker-service:
  build: { ... }
  image: ${RERANKER_IMAGE:-meepleai-reranker}         # NEW
orchestration-service:
  build: { ... }
  image: ${ORCHESTRATION_IMAGE:-meepleai-orchestrator} # NEW
```

In **dev** the env vars are unset → compose builds locally under the default name → **dev behavior unchanged**.

**`infra/compose.staging.yml`** (override) — this is where disk/staleness safety lives:

```yaml
embedding-service:
  image: ${EMBEDDING_IMAGE:?EMBEDDING_IMAGE required for staging — set to GHCR tag}
  pull_policy: always          # forces PULL — never build torch on the VPS
reranker-service:
  image: ${RERANKER_IMAGE:?RERANKER_IMAGE required for staging — set to GHCR tag}
  pull_policy: always
orchestration-service:
  image: ${ORCHESTRATION_IMAGE:-ghcr.io/<repo>/orchestration-service:staging-latest}
  pull_policy: always
```

- **embedding + reranker**: hard-required `:?` (no stale-local fallback, like api/web) + `pull_policy: always` → the VPS pulls from GHCR, never builds. This is the safeguard against the torch-rebuild disk-full failure mode.
- **orchestration**: soft default (`:-…:staging-latest`), **not** in the `ai-essential` profile → not started by the deploy command. If `--profile tutor-agents` is ever passed, it pulls from GHCR instead of building locally.

### Unit 4 — `deploy` job extension + disk safety

**Responsibility**: pull + recreate changed staging-active AI services, pull-only orchestration, all under disk guards.

`needs` extended to `[build, build-ai, snapshot-baseline]`. SSH-script sequence (new steps in **bold**):

1. *(existing)* sync repo, kill stale `:8080`, load secrets.
2. **Export staging-latest defaults for ALL AI image vars** (analog of the #1399-C api/web fix): because `compose.staging.yml` uses `${EMBEDDING_IMAGE:?}` / `${RERANKER_IMAGE:?}`, any `docker compose up` evaluates the whole file — an unchanged AI service's var must still be set or the command aborts. Default each to `…:staging-latest`; changed services override below.
3. **Pre-pull disk guard**: if any AI service changed, require `free-disk ≥ AI_PULL_GATE_GB` (repo var, default `15`). First run `docker image prune -af --filter "until=24h"`; if still below threshold → **abort** with a clear cleanup message (fail-safe). api/web-only deploys are unaffected.
4. *(existing)* for changed api/web: pull + export var + add to `$SERVICES`.
5. **For changed embedding/reranker**: `docker pull <svc>:staging-latest`, export `EMBEDDING_IMAGE`/`RERANKER_IMAGE`, add to `$SERVICES`.
6. **For changed orchestration**: `docker pull` only (cached for future activation; **not** added to `$SERVICES`).
7. *(existing)* `docker compose -f docker-compose.yml -f compose.staging.yml --profile ai-essential --profile monitoring-essential up -d --no-deps --force-recreate $SERVICES`.
8. *(existing)* API health check.
9. **AI health check**: for each recreated AI service, wait for `docker inspect --format '{{.State.Health.Status}}'` = `healthy` (embedding start-period ~60 s, reranker ~120 s).
10. **Post-prune**: `docker rmi` the previous AI tags by name (keep `staging-latest` + the current versioned tag); `docker image prune -f` removes the now-unreferenced old images.
11. *(existing, extended)* write `DEPLOYMENT.json` with new `embedding_image` / `reranker_image` / `orchestration_image` fields; final prune.

**Pull→recreate→rmi ordering** guarantees no steady-state accumulation of two versions: after `--force-recreate`, the old image becomes unreferenced and is pruned.

### Unit 5 — `ghcr-retention` job (NEW)

**Responsibility**: bound registry storage growth.

```yaml
ghcr-retention:
  needs: [deploy]
  if: ${{ !cancelled() && needs.deploy.result == 'success' }}
  runs-on: ubuntu-latest
  strategy:
    matrix:
      service: [embedding, reranker, orchestration]   # same short tokens as build-ai
  steps:
    - uses: actions/delete-package-versions@<pinned-sha>
      with:
        package-name: meepleai-monorepo/${{ matrix.service }}-service
        package-type: container
        min-versions-to-keep: 5
        ignore-versions: '^staging-latest$'   # never delete the moving tag
```

Keeps the last 5 versions per package, protects `staging-latest`. Affects registry storage only — VPS disk is already covered by Unit 4.

**Naming convention** (consistent across all units): the short token `<svc>` ∈ {`embedding`, `reranker`, `orchestration`} is the single source of truth. From it: app dir = `apps/<svc>-service/`, GHCR image/package = `ghcr.io/<repo>/<svc>-service`, deploy env var = `<SVC>_IMAGE`, `ai_changed` key = `<svc>`.

## GHA skip-propagation handling (correctness note)

GitHub Actions propagates an implicit `success()` over the entire `needs` chain, and a skipped upstream job propagates `skipped` downstream (documented in this very workflow's #1602 comments; see memory `gha-if-cancelled-vs-always`). Two cases must be handled so an **AI-only** deploy (no api/web change → `build` is `skipped`) still proceeds:

- **`snapshot-baseline`**: `needs: [build, build-ai]`, `if: !cancelled() && (needs.build.result == 'success' || needs.build-ai.result == 'success') && vars.DEPLOY_METHOD == 'ssh'`.
- **`deploy`**: `if: !cancelled() && (needs.build.result == 'success' || needs.build.result == 'skipped') && (needs.build-ai.result == 'success' || needs.build-ai.result == 'skipped') && (needs.snapshot-baseline.result == 'success' || needs.snapshot-baseline.result == 'skipped')`. The SSH script already `exit 0`s when `$SERVICES` is empty, so a both-skipped run is a harmless no-op.

`!cancelled()` (not `always()`) is used throughout, consistent with the existing jobs, so cancellation still aborts cleanly.

## Data Flow

```
push main-staging / workflow_dispatch
  └─ detect-changes → ai_changed={embedding,reranker,orchestration}, ai_any
       └─ build-ai (matrix, ubuntu-latest, QEMU arm64)
            embedding ─┐  (build only if changed)
            reranker  ─┤→ push ghcr.io/<repo>/<svc>-service:{staging-DATE-SHA, staging-latest}
            orchestr. ─┘
       └─ deploy (self-hosted VPS, ARM64)
            pre-pull disk guard (≥15GB or abort)
            pull staging-latest for changed svc
            embedding/reranker → compose up --force-recreate (profile ai-essential)
            orchestration      → pull only (not started)
            post-prune old AI tags
  └─ ghcr-retention (keep last 5 per package)
```

## Error Handling / Failure Modes

| Failure | Detection | Recovery |
|---|---|---|
| Torch build exceeds 120 min under QEMU | `build-ai` leg times out | Re-run; raise `timeout-minutes` once real timings known. `fail-fast: false` isolates the affected service. |
| Insufficient VPS disk for pull | Pre-pull guard after pre-prune | **Abort** deploy with cleanup instructions (fail-safe). api/web deploy still proceeds if it needs no AI space. |
| Unchanged AI service var unset → compose abort | `docker compose up` errors on `${X_IMAGE:?}` | Prevented by step 2 (export `:staging-latest` defaults for all AI vars). |
| New AI image unhealthy after recreate | AI health check (`docker inspect` health) fails | Deploy job fails; previous image still pullable via its versioned tag for manual rollback. |
| QEMU torch step pulls wrong-arch wheel | Build fails on `pip install` | torch ships manylinux aarch64 wheels; buildx `--platform arm64` selects them. Non-issue, documented. |
| GHCR retention deletes an in-use tag | n/a — `staging-latest` is `ignore-versions`-protected; VPS keeps local copies regardless | Re-push via a forced `build-ai` run. |
| AI-only deploy skipped because `build` skipped | `deploy` never runs | Prevented by the skip-propagation `if:` (see correctness note). |

## Testing Strategy

No local ARM64 Docker build environment exists in this dev setup. Validation layers:

| Layer | How |
|---|---|
| Workflow syntax | `Validate Workflows` job (actionlint) already in CI covers the new YAML |
| Compose resolution | `docker compose -f docker-compose.yml -f compose.staging.yml config` → `${X_IMAGE}` resolve, no errors |
| Dev not regressed | `make dev` still builds AI services locally (env vars absent) |
| Real build+push | `workflow_dispatch` with `force_full_deploy` → matrix builds all 3; verify tags on GHCR (`gh api .../packages`) |
| Pull + health on VPS | Post-deploy `docker inspect` health of embedding/reranker = healthy; existing smoke tests |
| Disk | Pre-pull guard logs + `df -h` before/after; confirm old AI tags removed |

**Rollback**: the workflow changes are additive; reverting the deploy-staging.yml + compose commits restores the prior (local-build) behavior. Versioned GHCR tags remain pullable for manual image rollback on the VPS.

## Scope

- **In**: `.github/workflows/deploy-staging.yml` (detect-changes, build-ai, deploy, ghcr-retention, snapshot-baseline gating); `infra/docker-compose.yml` + `infra/compose.staging.yml` (image vars for embedding/reranker/orchestration).
- **Out**:
  - `smoldocling-service`, `unstructured-service` (disabled in staging via `pdf-cloud-extractors`) → remain local-build, separate follow-up.
  - Runtime activation of orchestration in staging (`--profile tutor-agents`) → separate decision; this spec only publishes its image.
  - Native cross-compile rework of the Python Dockerfiles (`$BUILDPLATFORM`) → future optimization if QEMU proves too slow.
  - Building AI images on `main-dev` CI → only on the staging deploy, consistent with api/web.

## Expected Impact

- Closes the ~50-day staging drift for embedding + reranker; publishes a fresh orchestration image (incl. fix #1073) ready for activation.
- Build cost incurred only when a service's code/requirements change (per-service change detection) — typically ≤ monthly per service.
- VPS disk protected by a dedicated pre-pull gate + pull→recreate→prune ordering; registry storage bounded by retention.
- Zero added runner cost; reversible.

## Open Questions

None blocking. Two empirical unknowns, both surfaced on the first real deploy:
1. Actual QEMU torch build wall-clock (sets whether 120 min can be lowered, or whether native ARM runners become worth their cost).
2. Whether the 15 GB `AI_PULL_GATE_GB` default is comfortable when embedding **and** reranker change in the same deploy (worst-case transient). Tunable via repo variable.

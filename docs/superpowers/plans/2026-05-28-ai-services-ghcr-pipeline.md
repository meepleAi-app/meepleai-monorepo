# AI Services GHCR Build & Push Pipeline — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build + push `embedding`, `reranker`, and `orchestration` Docker images to GHCR on the staging deploy, with per-service change detection, so the ARM64 staging VPS pulls fresh images (never builds torch) under strict disk safety — closing the ~50-day drift (#1578).

**Architecture:** Add a matrix `build-ai` job to `deploy-staging.yml` (QEMU arm64, one leg per service, gated by per-service change detection). The `deploy` job pulls + recreates the changed staging-active services (embedding, reranker) and pull-only-caches orchestration, behind a dedicated pre-pull disk gate. The two compose files get `image:` vars so dev still builds locally while staging pulls from GHCR. A `ghcr-retention` job bounds registry storage.

**Tech Stack:** GitHub Actions, `docker/build-push-action` + buildx + QEMU, GHCR, docker compose v2.

**Spec:** `docs/for-developers/specs/2026-05-28-ai-services-ghcr-pipeline-design.md`

**Naming convention (single source of truth):** short token `<svc>` ∈ {`embedding`, `reranker`, `orchestration`}. From it: app dir `apps/<svc>-service/`, GHCR image `ghcr.io/<repo>/<svc>-service`, compose service name `<svc>-service`, container name `meepleai-embedding`/`meepleai-reranker`/`meepleai-orchestrator`, deploy env var `<SVC>_IMAGE`, `ai_changed` JSON key `<svc>`.

**Validation note:** No local ARM64/torch build is attempted (slow under QEMU; that's the CI's job). Local validation = `docker compose config` (compose resolution) + `python -c yaml.safe_load` (workflow YAML parse, since `actionlint` is not installed locally — the `Validate Workflows` CI job runs actionlint on push). The authoritative end-to-end test is a `workflow_dispatch` run (Task 9).

---

## File Structure

- **Modify** `infra/docker-compose.yml` — add `image: ${<SVC>_IMAGE:-meepleai-<name>}` to the 3 AI services (keep `build:`). Dev behavior unchanged.
- **Modify** `infra/compose.staging.yml` — add `image:` + `pull_policy: always` to the 3 AI services (embedding/reranker hard-required `:?`; orchestration soft default).
- **Modify** `.github/workflows/deploy-staging.yml` — extend `detect-changes`; add `build-ai` matrix job; extend `deploy` (needs/if + SSH script); add `ghcr-retention` job.

Each task leaves both files in a valid, committable state.

---

## Task 1: Pre-flight + baselines

**Files:** none

- [ ] **Step 1: Confirm branch + tools**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
git branch --show-current        # → feature/issue-1578-ai-services-ghcr-pipeline
docker compose version           # → v2.x present
python -c "import yaml; print('pyyaml ok')"
```
Expected: branch correct, docker compose + pyyaml present. (The spec commit `e07818a84` is already on this branch.)

- [ ] **Step 2: Baseline — current compose resolves (dev)**

```bash
docker compose -f infra/docker-compose.yml --profile ai config >/dev/null && echo "BASE compose OK"
```
Expected: `BASE compose OK` (no error). Record that today the AI services have no `image:` key (compose assigns a default project image name).

- [ ] **Step 3: Baseline — workflow YAML parses**

```bash
PYTHONIOENCODING=utf-8 python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); print('YAML valid')"
```
Expected: `YAML valid`.

---

## Task 2: Compose base — `image:` vars for AI services

**Files:**
- Modify: `infra/docker-compose.yml`

- [ ] **Step 1: Add `image:` to `embedding-service`**

Find (the `embedding-service` block, ~line 180):
```yaml
  embedding-service:
    build:
      context: ../apps/embedding-service
      dockerfile: ./Dockerfile
    container_name: meepleai-embedding
```
Replace with (insert the `image:` line after the `build:` block):
```yaml
  embedding-service:
    build:
      context: ../apps/embedding-service
      dockerfile: ./Dockerfile
    # Issue #1578: staging pulls this from GHCR (compose.staging.yml sets
    # EMBEDDING_IMAGE + pull_policy: always). Dev leaves the var unset → builds
    # locally as meepleai-embedding (mirrors api/web).
    image: ${EMBEDDING_IMAGE:-meepleai-embedding}
    container_name: meepleai-embedding
```

- [ ] **Step 2: Add `image:` to `reranker-service`**

Find (~line 261):
```yaml
  reranker-service:
    build:
      context: ../apps/reranker-service
      dockerfile: ./Dockerfile
    container_name: meepleai-reranker
```
Replace with:
```yaml
  reranker-service:
    build:
      context: ../apps/reranker-service
      dockerfile: ./Dockerfile
    # Issue #1578: see embedding-service note above.
    image: ${RERANKER_IMAGE:-meepleai-reranker}
    container_name: meepleai-reranker
```

- [ ] **Step 3: Add `image:` to `orchestration-service`**

Find (~line 287):
```yaml
  orchestration-service:
    build:
      context: ../apps/orchestration-service
      dockerfile: ./Dockerfile
    container_name: meepleai-orchestrator
```
Replace with:
```yaml
  orchestration-service:
    build:
      context: ../apps/orchestration-service
      dockerfile: ./Dockerfile
    # Issue #1578: publish-only. Image built+pushed to GHCR but not activated
    # in staging (stays out of the ai-essential deploy profile).
    image: ${ORCHESTRATION_IMAGE:-meepleai-orchestrator}
    container_name: meepleai-orchestrator
```

- [ ] **Step 4: Validate dev resolution (vars unset → local names)**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
docker compose -f infra/docker-compose.yml --profile ai config | grep -E "image: (meepleai-embedding|meepleai-reranker|meepleai-orchestrator)$"
```
Expected: 3 lines, each showing the local default image name (proves dev still builds locally — the `:-default` branch is taken).

- [ ] **Step 5: Commit**

```bash
git add infra/docker-compose.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 add image: vars to AI services in base compose

Mirror the api/web pattern: keep build: but add image: ${<SVC>_IMAGE:-local}
so staging can inject GHCR tags while dev keeps building locally (vars unset).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Compose staging — `image:` + `pull_policy: always`

**Files:**
- Modify: `infra/compose.staging.yml`

- [ ] **Step 1: Add image + pull_policy to `embedding-service`**

Find (~line 153):
```yaml
  embedding-service:
    restart: always
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```
Replace with:
```yaml
  embedding-service:
    # Issue #1578: hard-required GHCR image (no stale-local fallback) + force
    # pull. pull_policy: always guarantees the VPS PULLS the arm64 image built
    # in CI and NEVER builds torch locally (avoids the disk-full/swap incident).
    image: ${EMBEDDING_IMAGE:?EMBEDDING_IMAGE env var required for staging — set to GHCR tag}
    pull_policy: always
    restart: always
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```

- [ ] **Step 2: Add image + pull_policy to `reranker-service`**

Find (~line 165):
```yaml
  reranker-service:
    restart: always
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```
Replace with:
```yaml
  reranker-service:
    # Issue #1578: see embedding-service note above.
    image: ${RERANKER_IMAGE:?RERANKER_IMAGE env var required for staging — set to GHCR tag}
    pull_policy: always
    restart: always
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M
```

- [ ] **Step 3: Add image + pull_policy to `orchestration-service`**

Find (~line 205):
```yaml
  orchestration-service:
    restart: always
    env_file:
      - ./secrets/database.secret
      - ./secrets/redis.secret
      - ./secrets/openrouter.secret
```
Replace with:
```yaml
  orchestration-service:
    # Issue #1578: publish-only. Soft default (not :?) — orchestration is not in
    # the ai-essential deploy profile, so it is not started in staging. If a
    # future --profile tutor-agents activates it, it pulls from GHCR (not build).
    image: ${ORCHESTRATION_IMAGE:-ghcr.io/meepleai-app/meepleai-monorepo/orchestration-service:staging-latest}
    pull_policy: always
    restart: always
    env_file:
      - ./secrets/database.secret
      - ./secrets/redis.secret
      - ./secrets/openrouter.secret
```

- [ ] **Step 4: Validate staging resolution (vars set → GHCR; unset → error)**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
# (a) With vars set → resolves to GHCR refs, pull_policy present
EMBEDDING_IMAGE=ghcr.io/meepleai-app/meepleai-monorepo/embedding-service:staging-latest \
RERANKER_IMAGE=ghcr.io/meepleai-app/meepleai-monorepo/reranker-service:staging-latest \
API_IMAGE=x WEB_IMAGE=y \
docker compose -f infra/docker-compose.yml -f infra/compose.staging.yml --profile ai-essential config \
  | grep -E "ghcr.io/.*(embedding|reranker)-service:staging-latest" && echo "STAGING resolves OK"

# (b) Without EMBEDDING_IMAGE → the :? guard must fail
API_IMAGE=x WEB_IMAGE=y RERANKER_IMAGE=z \
docker compose -f infra/docker-compose.yml -f infra/compose.staging.yml --profile ai-essential config >/dev/null 2>&1 \
  && echo "❌ guard did NOT fire" || echo "✅ EMBEDDING_IMAGE :? guard fires as expected"
```
Expected: (a) prints the GHCR refs + `STAGING resolves OK`; (b) prints `✅ … guard fires`.

- [ ] **Step 5: Commit**

```bash
git add infra/compose.staging.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 staging AI services pull from GHCR (pull_policy: always)

embedding/reranker hard-required (:?) + pull_policy: always so the ARM64 VPS
pulls CI-built images and never builds torch locally. orchestration soft
default (publish-only, not in ai-essential profile).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `detect-changes` — per-service filters + outputs

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

- [ ] **Step 1: Add outputs**

Find (~line 133):
```yaml
    outputs:
      api: ${{ steps.changes.outputs.api }}
      web: ${{ steps.changes.outputs.web }}
      infra: ${{ steps.changes.outputs.infra }}
      deploy_api: ${{ steps.decision.outputs.deploy_api }}
      deploy_web: ${{ steps.decision.outputs.deploy_web }}
```
Replace with:
```yaml
    outputs:
      api: ${{ steps.changes.outputs.api }}
      web: ${{ steps.changes.outputs.web }}
      infra: ${{ steps.changes.outputs.infra }}
      deploy_api: ${{ steps.decision.outputs.deploy_api }}
      deploy_web: ${{ steps.decision.outputs.deploy_web }}
      ai_changed: ${{ steps.decision.outputs.ai_changed }}
      ai_any: ${{ steps.decision.outputs.ai_any }}
```

- [ ] **Step 2: Add path filters**

Find (~line 173):
```yaml
          filters: |
            api:
              - 'apps/api/**'
            web:
              - 'apps/web/**'
            infra:
              - 'infra/**'
              - '.github/workflows/deploy-staging.yml'
```
Replace with:
```yaml
          filters: |
            api:
              - 'apps/api/**'
            web:
              - 'apps/web/**'
            infra:
              - 'infra/**'
              - '.github/workflows/deploy-staging.yml'
            embedding:
              - 'apps/embedding-service/**'
            reranker:
              - 'apps/reranker-service/**'
            orchestration:
              - 'apps/orchestration-service/**'
```

- [ ] **Step 3: Extend the decision step to emit `ai_changed` + `ai_any`**

Find the end of the decision step (~line 206-210):
```yaml
          else
            echo "deploy_api=$API" >> $GITHUB_OUTPUT
            echo "deploy_web=$WEB" >> $GITHUB_OUTPUT
            echo "📦 Selective deploy: api=$API, web=$WEB"
          fi
```
Replace with:
```yaml
          else
            echo "deploy_api=$API" >> $GITHUB_OUTPUT
            echo "deploy_web=$WEB" >> $GITHUB_OUTPUT
            echo "📦 Selective deploy: api=$API, web=$WEB"
          fi

          # Issue #1578 — AI service change detection. Same force-full triggers
          # as api/web (workflow_dispatch / force / infra / initial push).
          EMB="${{ steps.changes.outputs.embedding }}"
          RRK="${{ steps.changes.outputs.reranker }}"
          ORC="${{ steps.changes.outputs.orchestration }}"
          if [ "$EVENT" = "workflow_dispatch" ] \
            || [ "$FORCE" = "true" ] \
            || [ "$INFRA" = "true" ] \
            || [ "$BEFORE" = "$ZEROS" ] \
            || [ -z "$BEFORE" ]; then
            EMB=true; RRK=true; ORC=true
          fi
          # paths-filter outputs are empty when the changes step is skipped
          # (workflow_dispatch). Normalize empties to false.
          EMB="${EMB:-false}"; RRK="${RRK:-false}"; ORC="${ORC:-false}"
          echo "ai_changed={\"embedding\":$EMB,\"reranker\":$RRK,\"orchestration\":$ORC}" >> $GITHUB_OUTPUT
          if [ "$EMB" = "true" ] || [ "$RRK" = "true" ] || [ "$ORC" = "true" ]; then
            echo "ai_any=true" >> $GITHUB_OUTPUT
          else
            echo "ai_any=false" >> $GITHUB_OUTPUT
          fi
          echo "🤖 AI changed: embedding=$EMB reranker=$RRK orchestration=$ORC"
```

- [ ] **Step 4: Validate YAML**

```bash
PYTHONIOENCODING=utf-8 python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); print('YAML valid')"
```
Expected: `YAML valid`.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 detect-changes per-AI-service filters + ai_changed output

Adds embedding/reranker/orchestration path filters and emits ai_changed
(JSON map) + ai_any. Same force-full logic as api/web.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: `build-ai` matrix job

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

- [ ] **Step 1: Insert the `build-ai` job after the `build` job**

Find the end of the `build` job — the `Build Summary` step (~line 483-487):
```yaml
      - name: Build Summary
        run: |
          echo "📦 Build summary:"
          echo "  API built: ${{ needs.detect-changes.outputs.deploy_api }}"
          echo "  Web built: ${{ needs.detect-changes.outputs.deploy_web }}"
```
Insert immediately AFTER it (before the `snapshot-baseline:` job) this new job:
```yaml

  # Build and push AI service images (only changed ones). Issue #1578.
  # Separate from `build` so the slow torch QEMU builds run in parallel
  # (matrix), with per-service timeout, and a single failure does not block
  # the other services. ALWAYS ubuntu-latest (cloud) — never the VPS.
  build-ai:
    name: Build AI Images
    runs-on: ubuntu-latest
    timeout-minutes: 120
    needs: [pre-deploy-check, detect-changes]
    if: always() && (needs.pre-deploy-check.result == 'success' || (github.event_name == 'workflow_dispatch' && inputs.skip_tests == true)) && needs.detect-changes.outputs.ai_any == 'true'
    strategy:
      fail-fast: false
      matrix:
        service: [embedding, reranker, orchestration]
    steps:
      - name: Checkout
        uses: actions/checkout@v6
        with:
          ref: ${{ env.DEPLOY_SHA }}
          sparse-checkout: |
            apps/${{ matrix.service }}-service/
          sparse-checkout-cone-mode: true

      - name: Generate Version
        id: version
        run: |
          VERSION="staging-$(date +'%Y%m%d')-${GITHUB_SHA::7}"
          echo "version=$VERSION" >> $GITHUB_OUTPUT
          echo "📦 ${{ matrix.service }} version: $VERSION"

      - name: Free disk space for build
        if: ${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}
        run: |
          docker builder prune -f 2>/dev/null || true
          docker image prune -a -f --filter 'until=24h' 2>/dev/null || true
          echo "📊 Disk free: $(df -h / | tail -1 | awk '{print $4}')"

      - name: Setup Docker Buildx
        if: ${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}
        uses: docker/setup-buildx-action@4d04d5d9486b7bd6fa91e7baf45bbb4f8b9deedd  # v4

      - name: Setup QEMU for ARM64
        if: ${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}
        uses: docker/setup-qemu-action@ce360397dd3f832beb865e1373c09c0e9f86d70a  # v4
        with:
          platforms: arm64

      - name: Login to GitHub Container Registry
        if: ${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}
        uses: docker/login-action@650006c6eb7dba73a995cc03b0b2d7f5ca915bee  # v4
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract Metadata
        if: ${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}
        id: meta
        uses: docker/metadata-action@030e881283bb7a6894de51c315a6bfe6a94e05cf  # v6
        with:
          images: ${{ env.REGISTRY }}/${{ github.repository }}/${{ matrix.service }}-service
          tags: |
            type=raw,value=${{ steps.version.outputs.version }}
            type=raw,value=staging-latest

      # Python torch Dockerfiles are NOT cross-compile-ready (no $BUILDPLATFORM),
      # so the arm64 build runs under QEMU emulation. Acceptable: gated by
      # change-detection (rare) and runs on the cloud runner (never the VPS).
      - name: Build and Push ${{ matrix.service }}
        if: ${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}
        uses: docker/build-push-action@bcafcacb16a39f128d818304e6c9c0c18556b85f  # v7.1.0
        with:
          context: ./apps/${{ matrix.service }}-service
          file: ./apps/${{ matrix.service }}-service/Dockerfile
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          platforms: linux/arm64
          cache-from: type=gha,scope=ai-${{ matrix.service }}
          cache-to: type=gha,mode=max,scope=ai-${{ matrix.service }}

      - name: Build Summary
        run: echo "🤖 ${{ matrix.service }}-service — changed=${{ fromJSON(needs.detect-changes.outputs.ai_changed)[matrix.service] }}"
```

- [ ] **Step 2: Validate YAML**

```bash
PYTHONIOENCODING=utf-8 python -c "import yaml; d=yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); assert 'build-ai' in d['jobs']; print('build-ai present, YAML valid')"
```
Expected: `build-ai present, YAML valid`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 build-ai matrix job (QEMU arm64, change-gated)

Parallel per-service build+push of embedding/reranker/orchestration to GHCR.
fail-fast:false, timeout 120m (torch under QEMU), each leg builds only if its
service changed. Tags: staging-DATE-SHA + staging-latest.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: `deploy` job gating (needs + if)

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

- [ ] **Step 1: Extend deploy `needs` and `if`**

Find (~line 586-587):
```yaml
    needs: [build, snapshot-baseline]
    if: always() && needs.build.result == 'success' && (needs.snapshot-baseline.result == 'success' || needs.snapshot-baseline.result == 'skipped')
```
Replace with:
```yaml
    needs: [detect-changes, build, build-ai, snapshot-baseline]
    # Issue #1578 — skip-propagation handling. An AI-only deploy has `build`
    # (api/web) skipped, so `build` may be 'success' OR 'skipped'. build-ai may
    # be 'success' (built) or 'skipped' (no AI change); a build-ai FAILURE
    # (success||skipped false) blocks the deploy on purpose — don't deploy when
    # an image we asked to build failed. snapshot-baseline may be skipped on
    # AI-only deploys (it gates on `build`).
    if: always() && (needs.build.result == 'success' || needs.build.result == 'skipped') && (needs.build-ai.result == 'success' || needs.build-ai.result == 'skipped') && (needs.snapshot-baseline.result == 'success' || needs.snapshot-baseline.result == 'skipped')
```

- [ ] **Step 2: Validate YAML**

```bash
PYTHONIOENCODING=utf-8 python -c "import yaml; d=yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); n=d['jobs']['deploy']['needs']; assert 'detect-changes' in n and 'build-ai' in n, n; print('deploy needs OK:', n)"
```
Expected: `deploy needs OK: ['detect-changes', 'build', 'build-ai', 'snapshot-baseline']`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 gate deploy on build-ai (skip-propagation safe)

deploy now needs detect-changes + build-ai. if: accepts build/build-ai/
snapshot-baseline = success||skipped so AI-only deploys proceed; a build-ai
failure still blocks (don't deploy a failed image build).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: `deploy` SSH script — AI vars, disk guard, pull/recreate, health

**Files:**
- Modify: `.github/workflows/deploy-staging.yml` (the `Deploy via SSH` step's `script:`)

- [ ] **Step 1: Insert AI image vars + pre-pull disk guard**

Find (~line 757-760, inside the SSH `script:`):
```bash
            export API_IMAGE="${{ env.API_IMAGE }}:staging-latest"
            export WEB_IMAGE="${{ env.WEB_IMAGE }}:staging-latest"

            SERVICES=""
```
Replace with:
```bash
            export API_IMAGE="${{ env.API_IMAGE }}:staging-latest"
            export WEB_IMAGE="${{ env.WEB_IMAGE }}:staging-latest"

            # Issue #1578 — AI image vars. compose.staging.yml guards
            # embedding/reranker with ${X_IMAGE:?}, so every `compose up`
            # (even one targeting only some services) needs them set, or it
            # aborts (analog of the api/web #1399-C fix above). Default all to
            # staging-latest; changed services keep this ref (the build-ai job
            # always pushes staging-latest).
            AI_BASE="${{ env.REGISTRY }}/${{ github.repository }}"
            export EMBEDDING_IMAGE="${AI_BASE}/embedding-service:staging-latest"
            export RERANKER_IMAGE="${AI_BASE}/reranker-service:staging-latest"
            export ORCHESTRATION_IMAGE="${AI_BASE}/orchestration-service:staging-latest"

            EMB_CHANGED="${{ fromJSON(needs.detect-changes.outputs.ai_changed).embedding }}"
            RRK_CHANGED="${{ fromJSON(needs.detect-changes.outputs.ai_changed).reranker }}"
            ORC_CHANGED="${{ fromJSON(needs.detect-changes.outputs.ai_changed).orchestration }}"

            # Pre-pull disk guard (issue #1578 / #1575). Pulling a ~5GB torch
            # image while the old one is still referenced can fill the disk.
            # Pre-prune, then require AI_PULL_GATE_GB free; otherwise ABORT
            # (fail-safe) — never risk disk-full.
            AI_PULL_GATE_GB="${AI_PULL_GATE_GB:-15}"
            if [ "$EMB_CHANGED" = "true" ] || [ "$RRK_CHANGED" = "true" ] || [ "$ORC_CHANGED" = "true" ]; then
              echo "🧹 Pre-prune before AI image pull..."
              docker image prune -af --filter "until=24h" || true
              FREE_GB=$(df -BG / | awk 'NR==2 {gsub(/G/,"",$4); print $4}')
              echo "💽 VPS disk: ${FREE_GB} GB free — AI pull gate ${AI_PULL_GATE_GB} GB"
              if [ "$FREE_GB" -lt "$AI_PULL_GATE_GB" ]; then
                echo "::error::Insufficient disk for AI image pull: ${FREE_GB} GB free < ${AI_PULL_GATE_GB} GB"
                echo "🚫 Aborting AI deploy (fail-safe). Manual cleanup on the VPS:"
                echo "  docker image prune -af"
                echo "  bash /opt/meepleai/repo/infra/scripts/daily-disk-prune.sh"
                exit 1
              fi
            fi

            SERVICES=""
```

- [ ] **Step 2: Insert AI pull/recreate blocks (before the empty-SERVICES check)**

Find (~line 770-780):
```bash
            if [ "${{ needs.build.outputs.web_built }}" = "true" ]; then
              docker pull ${{ needs.build.outputs.web_image }}
              export WEB_IMAGE="${{ needs.build.outputs.web_image }}"
              SERVICES="$SERVICES web"
              echo "📦 Web image updated"
            fi

            if [ -z "$SERVICES" ]; then
              echo "⏭️ No services to deploy"
              exit 0
            fi
```
Replace with:
```bash
            if [ "${{ needs.build.outputs.web_built }}" = "true" ]; then
              docker pull ${{ needs.build.outputs.web_image }}
              export WEB_IMAGE="${{ needs.build.outputs.web_image }}"
              SERVICES="$SERVICES web"
              echo "📦 Web image updated"
            fi

            # Issue #1578 — AI services. embedding/reranker are in the
            # ai-essential profile → pull + recreate. orchestration is
            # publish-only → pull ONLY (cached for a future activation).
            if [ "$EMB_CHANGED" = "true" ]; then
              docker pull "$EMBEDDING_IMAGE"
              SERVICES="$SERVICES embedding-service"
              echo "📦 embedding-service image updated"
            fi
            if [ "$RRK_CHANGED" = "true" ]; then
              docker pull "$RERANKER_IMAGE"
              SERVICES="$SERVICES reranker-service"
              echo "📦 reranker-service image updated"
            fi
            if [ "$ORC_CHANGED" = "true" ]; then
              docker pull "$ORCHESTRATION_IMAGE" || echo "⚠️ orchestration pull failed (non-blocking; not active in staging)"
              echo "📦 orchestration-service image cached (not started in staging)"
            fi

            if [ -z "$SERVICES" ]; then
              echo "⏭️ No services to deploy"
              exit 0
            fi
```

- [ ] **Step 3: Insert AI health check after the API health check**

Find (~line 810-817), the end of the API health block:
```bash
            docker exec meepleai-api curl -sf http://localhost:8080/health | python3 -c "
            import sys,json
            d=json.load(sys.stdin)
            healthy = [c['name'] for c in d['checks'] if c['status']=='Healthy']
            print(f'Healthy services: {len(healthy)}')
            assert any(c['name']=='postgres' and c['status']=='Healthy' for c in d['checks']), 'postgres unhealthy'
            assert any(c['name']=='redis' and c['status']=='Healthy' for c in d['checks']), 'redis unhealthy'
            " || exit 1
```
Insert immediately AFTER it:
```bash

            # Issue #1578 — AI container health (only for recreated services).
            # Container names per compose: embedding-service→meepleai-embedding,
            # reranker-service→meepleai-reranker.
            ai_health() {
              cname="$1"
              echo "⏳ Waiting for ${cname} health..."
              for attempt in $(seq 1 40); do
                st=$(docker inspect --format '{{.State.Health.Status}}' "$cname" 2>/dev/null || echo "missing")
                if [ "$st" = "healthy" ]; then echo "✅ ${cname} healthy after ~$((attempt*5))s"; return 0; fi
                sleep 5
              done
              echo "::error::${cname} did not become healthy"
              docker logs "$cname" --tail 30 2>/dev/null || true
              return 1
            }
            case " $SERVICES " in *" embedding-service "*) ai_health meepleai-embedding || exit 1 ;; esac
            case " $SERVICES " in *" reranker-service "*) ai_health meepleai-reranker || exit 1 ;; esac
```

- [ ] **Step 4: Add AI image fields to DEPLOYMENT.json**

Find (~line 820-831):
```bash
            cat > /opt/meepleai/repo/infra/DEPLOYMENT.json << VEREOF
            {
              "version": "${{ needs.build.outputs.version }}",
              "api_image": "${{ needs.build.outputs.api_image }}",
              "web_image": "${{ needs.build.outputs.web_image }}",
              "environment": "staging",
              "deployed_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
              "deployed_by": "${{ github.actor }}",
              "commit": "${{ env.DEPLOY_SHA }}",
              "workflow_run": "${{ github.run_id }}"
            }
            VEREOF
```
Replace with:
```bash
            cat > /opt/meepleai/repo/infra/DEPLOYMENT.json << VEREOF
            {
              "version": "${{ needs.build.outputs.version }}",
              "api_image": "${{ needs.build.outputs.api_image }}",
              "web_image": "${{ needs.build.outputs.web_image }}",
              "embedding_image": "${EMBEDDING_IMAGE}",
              "reranker_image": "${RERANKER_IMAGE}",
              "orchestration_image": "${ORCHESTRATION_IMAGE}",
              "ai_changed": "embedding=${EMB_CHANGED},reranker=${RRK_CHANGED},orchestration=${ORC_CHANGED}",
              "environment": "staging",
              "deployed_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
              "deployed_by": "${{ github.actor }}",
              "commit": "${{ env.DEPLOY_SHA }}",
              "workflow_run": "${{ github.run_id }}"
            }
            VEREOF
```
(The final `docker image prune -f` at the end of the step already removes the now-dangling old AI image layers after `--force-recreate` re-points `staging-latest` — no extra rmi needed.)

- [ ] **Step 5: Validate YAML**

```bash
PYTHONIOENCODING=utf-8 python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); print('YAML valid')"
```
Expected: `YAML valid`.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 deploy AI images with disk-safe pull/recreate

SSH script: export AI image vars (#1399-C analog), pre-pull disk gate
(AI_PULL_GATE_GB=15, abort fail-safe), pull+recreate embedding/reranker,
pull-only orchestration, AI container health checks, DEPLOYMENT.json fields.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: `ghcr-retention` job

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

- [ ] **Step 1: Resolve the action SHA to pin (repo policy: SHA-pinned actions)**

```bash
gh api repos/actions/delete-package-versions/tags --jq '.[] | "\(.name) \(.commit.sha)"' | head -5
```
Expected: a list of `vX.Y.Z <sha>` lines. Pick the latest `v5.*` and use its `<sha>` in Step 2 (replace `PIN_SHA_HERE`).

- [ ] **Step 2: Insert the `ghcr-retention` job before `notify-end`**

Find (~line 1135) the start of `notify-end`:
```yaml
  notify-end:
    if: always()
    needs: [detect-changes, pre-deploy-check, build, snapshot-baseline, deploy, validate, e2e-staging]
```
Insert BEFORE it this new job (and add `build-ai` + `ghcr-retention` to notify-end's needs in the same edit):
```yaml
  # Bound GHCR storage for the AI packages. Issue #1578.
  ghcr-retention:
    name: GHCR Retention (AI)
    needs: [deploy]
    if: ${{ !cancelled() && needs.deploy.result == 'success' }}
    runs-on: ubuntu-latest
    permissions:
      packages: write
    strategy:
      fail-fast: false
      matrix:
        service: [embedding, reranker, orchestration]
    steps:
      - name: Delete old container versions (keep last 5)
        uses: actions/delete-package-versions@PIN_SHA_HERE  # v5 — resolved in Step 1
        with:
          package-name: ${{ github.event.repository.name }}/${{ matrix.service }}-service
          package-type: container
          min-versions-to-keep: 5
          ignore-versions: '^staging-latest$'

  notify-end:
    if: always()
    needs: [detect-changes, pre-deploy-check, build, build-ai, snapshot-baseline, deploy, validate, e2e-staging, ghcr-retention]
```

- [ ] **Step 3: Validate YAML**

```bash
PYTHONIOENCODING=utf-8 python -c "import yaml; d=yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); assert 'ghcr-retention' in d['jobs']; assert 'build-ai' in d['jobs']['notify-end']['needs']; print('retention + notify-end OK')"
```
Expected: `retention + notify-end OK`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "$(cat <<'EOF'
feat(ci): #1578 GHCR retention for AI packages (keep last 5)

Matrix job deletes old container versions per AI package after a successful
deploy, protecting staging-latest. Wires build-ai + ghcr-retention into
notify-end needs.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Push, PR, and `workflow_dispatch` validation (integration test)

**Files:** none

- [ ] **Step 1: Final local gates**

```bash
cd "D:/Repositories/meepleai-monorepo-main"
PYTHONIOENCODING=utf-8 python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml', encoding='utf-8')); print('workflow YAML valid')"
docker compose -f infra/docker-compose.yml --profile ai config >/dev/null && echo "dev compose OK"
EMBEDDING_IMAGE=ghcr.io/meepleai-app/meepleai-monorepo/embedding-service:staging-latest \
RERANKER_IMAGE=ghcr.io/meepleai-app/meepleai-monorepo/reranker-service:staging-latest \
API_IMAGE=x WEB_IMAGE=y \
docker compose -f infra/docker-compose.yml -f infra/compose.staging.yml --profile ai-essential config >/dev/null && echo "staging compose OK"
```
Expected: `workflow YAML valid`, `dev compose OK`, `staging compose OK`.

- [ ] **Step 2: Push**

```bash
git push -u origin feature/issue-1578-ai-services-ghcr-pipeline
```

- [ ] **Step 3: Open PR to main-dev**

```bash
gh pr create --base main-dev \
  --title "feat(ci): #1578 AI services GHCR build & push pipeline" \
  --body "$(cat <<'EOF'
## Summary

Closes the ~50-day AI-service image drift on staging (#1578). embedding,
reranker, and orchestration are now built + pushed to GHCR by the staging
deploy with per-service change detection; the ARM64 VPS pulls (never builds)
them, under a dedicated disk-safety gate. orchestration is publish-only.

- `detect-changes`: per-service path filters + `ai_changed`/`ai_any`.
- `build-ai`: matrix job (QEMU arm64, change-gated, fail-fast:false, 120m).
- compose: `image:` vars (dev builds local, staging pulls via `pull_policy: always`).
- `deploy`: pre-pull disk gate (abort fail-safe), pull+recreate embedding/
  reranker, pull-only orchestration, AI health checks.
- `ghcr-retention`: keep last 5 per package.

## Validation

- Local: workflow YAML parse + `docker compose config` (dev + staging) green.
- `Validate Workflows` CI (actionlint) on this PR.
- Post-merge: a `workflow_dispatch` run with `force_full_deploy` exercises the
  full matrix build + pull + health (see PR checklist).

## Out of scope

smoldocling/unstructured (disabled in staging), orchestration runtime
activation, native Dockerfile cross-compile.

Spec: `docs/for-developers/specs/2026-05-28-ai-services-ghcr-pipeline-design.md`
Plan: `docs/superpowers/plans/2026-05-28-ai-services-ghcr-pipeline.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Confirm `Validate Workflows` (actionlint) passes on the PR**

```bash
gh pr checks --watch | grep -i "Validate Workflows"
```
Expected: `Validate Workflows` = pass. If actionlint flags a GHA-expression issue (e.g. `fromJSON(...)[matrix.service]`), fix it and re-push before merge.

- [ ] **Step 5: Decide the integration test with the user**

The real build+push+pull only runs on a staging deploy. Present options (do NOT auto-trigger):
1. After merge to main-dev, run `gh workflow run deploy-staging.yml --ref feature/issue-1578-ai-services-ghcr-pipeline -f force_full_deploy=true -f skip_tests=true` to exercise the matrix build + GHCR push + VPS pull + AI health from the branch.
2. Verify GHCR tags: `gh api /orgs/meepleai-app/packages/container/meepleai-monorepo%2Fembedding-service/versions --jq '.[0].metadata.container.tags'`.
3. On the VPS, confirm `docker inspect meepleai-embedding meepleai-reranker --format '{{.Config.Image}}'` shows the GHCR ref + `df -h /` headroom held.

Report build wall-clock (QEMU torch timing) so the 120m timeout and 15GB gate can be tuned.

---

## Self-Review

**1. Spec coverage:**
- Unit 1 (detect-changes) → Task 4 ✅
- Unit 2 (build-ai matrix) → Task 5 ✅
- Unit 3 (compose base + staging) → Tasks 2, 3 ✅
- Unit 4 (deploy + disk safety) → Tasks 6, 7 ✅
- Unit 5 (ghcr-retention) → Task 8 ✅
- GHA skip-propagation note → Task 6 if-condition ✅
- Testing strategy (compose config, YAML parse, workflow_dispatch) → Tasks 1-3, 9 ✅
- Disk fail-safe (abort) → Task 7 Step 1 ✅
- orchestration publish-only (pull, not recreated) → Task 7 Step 2 ✅

**2. Placeholder scan:** `PIN_SHA_HERE` in Task 8 is resolved by Task 8 Step 1's `gh` command (concrete instruction, not a vague TODO). All YAML/code blocks are shown in full. No "TBD"/"add error handling"/"similar to".

**3. Type/name consistency:**
- Short tokens `embedding`/`reranker`/`orchestration` used in: filters (T4), `ai_changed` keys (T4), build-ai matrix (T5), SSH `*_CHANGED` flags (T7), retention matrix (T8). ✅
- Compose service names `embedding-service`/`reranker-service` used in `$SERVICES` + health `case` (T7) and match docker-compose.yml. ✅
- Container names `meepleai-embedding`/`meepleai-reranker` in health checks match compose `container_name`. ✅
- Env vars `EMBEDDING_IMAGE`/`RERANKER_IMAGE`/`ORCHESTRATION_IMAGE` consistent across compose (T2, T3) and SSH export (T7). ✅
- GHCR image path `ghcr.io/<repo>/<svc>-service` consistent in build-ai metadata (T5), SSH `AI_BASE` (T7), retention package-name (T8). ✅

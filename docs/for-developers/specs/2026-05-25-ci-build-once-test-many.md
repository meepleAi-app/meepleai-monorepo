# CI Build Once, Test Many — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans` (inline execution). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate duplicate `dotnet build` (×5) and `pnpm build` (×4) in `ci.yml` by introducing upstream `build-backend` / `build-frontend` jobs that publish artifacts consumed by downstream test jobs.

**Architecture:** Apply the *Build Once, Test Many* pattern (Fowler). Three new upstream jobs: `build-backend`, `build-frontend`, `build-frontend-a11y` (separate because `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED` is constant-folded at build time). Downstream jobs `download-artifact` + `dotnet test --no-build` / `next start`. Coverage: switch backend-integration from `coverlet.msbuild` (build-time instrumentation) to `dotnet-coverage` wrapper (runtime, --no-build friendly), matching backend-unit pattern.

**Tech Stack:** GitHub Actions, `actions/upload-artifact@v7`, `actions/download-artifact@v7`, .NET 9, Next.js 16 standalone output, dotnet-coverage tool.

**Expected impact:**
- CPU time per `ci.yml` run: **−25 min** (backend build 5×5min → 1×5min + 5×30s download; frontend build 4×100s → 2×100s + 2×download).
- Wall-clock critical path: marginal reduction (~3 min) because backend-integration Core shard remains the bottleneck at ~50 min. Strategic value: enables future shard increase + cross-workflow reuse.

**Non-goals:**
- Reduce backend-integration test time (P2.1 MaxCpuCount audit out of scope).
- Refactor `deploy-staging.yml` pre-deploy-check (P1.3, separate PR).
- Eliminate CodeQL bottleneck (P1.2, separate PR).

---

## File Structure

**New:**
- `.github/workflows/ci.yml` — refactor: 3 new jobs upstream, 5 downstream jobs converted to artifact consumers.

**Modified:**
- `.github/workflows/ci.yml` — primary surface (~200 line delta).

**No changes:**
- `.github/actions/setup-backend/action.yml` — still used by build-backend.
- `.github/actions/setup-frontend/action.yml` — still used by build-frontend.
- `.github/actions/setup-playwright/action.yml` — still used by jobs needing browsers.
- `apps/api/tests/Api.Tests/integration.runsettings` — coverage config switches at command-line level only.
- `apps/api/tests/Api.Tests/unit.runsettings` — unchanged.

**Excluded from scope (other workflows that duplicate build):**
- `dev-fast.yml`, `dev-async.yml`, `staging.yml`, `deploy-staging.yml`, `test-e2e.yml` — could benefit but not in this PR.

---

## Task 1: Pre-flight verification

**Files:** none

- [ ] **Step 1: Verify branch state**

```bash
git branch --show-current  # → feature/ci-build-once-test-many
git status                  # → clean
git config branch.feature/ci-build-once-test-many.parent  # → main-dev
```

- [ ] **Step 2: Capture baseline ci.yml line count**

```bash
wc -l .github/workflows/ci.yml
# Expected: 889 lines (record this for comparison post-refactor)
```

- [ ] **Step 3: Install actionlint locally for validation**

```bash
# Windows: choco install actionlint OR download from https://github.com/rhysd/actionlint/releases
actionlint --version
# If not available, skip local validation; rely on push-time validation in CI
```

---

## Task 2: Add `build-backend` upstream job

**Files:**
- Modify: `.github/workflows/ci.yml`

**Goal:** Single .NET solution build that produces a tar.gz artifact consumed by `backend-unit`, `backend-integration` (×3), and `e2e`.

- [ ] **Step 1: Add `build-backend` job after `changes` job (insert before `frontend` at line 78)**

```yaml
  # Build .NET solution once and share via artifact across all backend test jobs.
  # Eliminates ~5× redundant `dotnet build` (was 5 jobs × ~5min each = ~25min CPU).
  # Pattern: Build Once, Test Many (Fowler). Coverage switches to dotnet-coverage
  # wrapper (runtime, --no-build friendly) in consumer jobs.
  build-backend:
    name: Build Backend
    runs-on: ${{ needs.changes.outputs.runner }}
    needs: changes
    timeout-minutes: 15
    if: needs.changes.outputs.backend == 'true' || github.event_name == 'workflow_dispatch' || github.event_name == 'workflow_call'
    defaults:
      run:
        working-directory: apps/api
    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Install PDF Dependencies
        run: sudo apt-get update && sudo apt-get install -y libgdiplus

      - name: Setup Backend
        uses: ./.github/actions/setup-backend
        with:
          dotnet-version: '9.0.x'
          working-directory: apps/api

      - name: Build Solution (Release)
        run: dotnet build MeepleAI.Api.sln --no-restore --configuration Release

      - name: Package build artifacts
        # Tar+zstd preserves symlinks and timestamps; small overhead vs raw upload
        # but avoids the artifact-v4 silent permission stripping on bin/ executables.
        shell: bash
        run: |
          echo "📦 Packaging backend build artifacts..."
          tar --use-compress-program='zstd -3 -T0' \
              -cf /tmp/backend-build.tar.zst \
              --exclude='**/TestResults' \
              --exclude='**/coverage' \
              src/Api/bin src/Api/obj \
              src/Api.Analyzers/bin src/Api.Analyzers/obj \
              tests/Api.Tests/bin tests/Api.Tests/obj \
              tests/Api.Analyzers.Tests/bin tests/Api.Analyzers.Tests/obj
          ls -lh /tmp/backend-build.tar.zst
          echo "✅ Backend artifact packaged"

      - name: Upload backend artifact
        uses: actions/upload-artifact@v7
        with:
          name: backend-build-${{ github.run_id }}
          path: /tmp/backend-build.tar.zst
          retention-days: 1
          compression-level: 0  # already zstd-compressed
```

- [ ] **Step 2: Validate YAML syntax with actionlint (or manual review if unavailable)**

```bash
actionlint .github/workflows/ci.yml
# Expected: no errors
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci(build-once): add build-backend upstream job

Introduces a single .NET solution build that packages bin/+obj/ into
a tar.zst artifact. Consumed by backend-unit, backend-integration (3
shards), and e2e in subsequent commits — eliminates ~5× redundant
dotnet build (~25 min CPU per CI run).

Refs: docs/for-developers/specs/2026-05-25-ci-build-once-test-many.md

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: Add `build-frontend` upstream job (default config)

**Files:**
- Modify: `.github/workflows/ci.yml`

**Goal:** Single `pnpm build` (default env) consumed by `frontend-bundle-size` and `e2e`. Frontend lint/typecheck/test stays standalone (doesn't need `.next/`).

- [ ] **Step 1: Add `build-frontend` job after `build-backend`**

```yaml
  # Build Next.js once with default config (no fixture flag) and share via
  # artifact. Consumed by `frontend-bundle-size` and `e2e`. NOT used by
  # `frontend-a11y` which needs NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1
  # (constant-folded at build time → separate artifact in build-frontend-a11y).
  build-frontend:
    name: Build Frontend
    runs-on: ${{ needs.changes.outputs.runner }}
    needs: changes
    timeout-minutes: 15
    if: needs.changes.outputs.frontend == 'true' || github.event_name == 'workflow_dispatch'
    defaults:
      run:
        working-directory: apps/web
    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Setup Frontend
        uses: ./.github/actions/setup-frontend
        with:
          node-version: '20'
          pnpm-version: '10'
          working-directory: apps/web
          frozen-lockfile: 'true'

      - name: Build (default config)
        run: pnpm build
        env:
          NEXT_PUBLIC_API_BASE: http://localhost:8080

      - name: Package frontend build
        shell: bash
        run: |
          echo "📦 Packaging frontend .next/ ..."
          tar --use-compress-program='zstd -3 -T0' \
              -cf /tmp/frontend-build.tar.zst \
              .next
          ls -lh /tmp/frontend-build.tar.zst
          echo "✅ Frontend artifact packaged"

      - name: Upload frontend artifact
        uses: actions/upload-artifact@v7
        with:
          name: frontend-build-${{ github.run_id }}
          path: /tmp/frontend-build.tar.zst
          retention-days: 1
          compression-level: 0
```

- [ ] **Step 2: Add `build-frontend-a11y` job (separate because a11y build needs the visual-test fixture flag)**

```yaml
  # Build Next.js with NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 for the
  # a11y suite. Separate from build-frontend because Next.js constant-folds
  # NEXT_PUBLIC_* env vars at build time — same artifact cannot serve both
  # configs. Net change: 4× pnpm build → 2× builds (still −50% CPU).
  build-frontend-a11y:
    name: Build Frontend (a11y config)
    runs-on: ${{ needs.changes.outputs.runner }}
    needs: changes
    timeout-minutes: 15
    if: needs.changes.outputs.frontend == 'true' || github.event_name == 'workflow_dispatch'
    defaults:
      run:
        working-directory: apps/web
    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Setup Frontend
        uses: ./.github/actions/setup-frontend
        with:
          node-version: '20'
          pnpm-version: '10'
          working-directory: apps/web
          frozen-lockfile: 'true'

      - name: Build (a11y config — visual-test fixture enabled)
        run: pnpm build
        env:
          NEXT_PUBLIC_API_BASE: http://localhost:8080
          NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED: '1'

      - name: Package frontend (a11y) build
        shell: bash
        run: |
          echo "📦 Packaging frontend (a11y) .next/ ..."
          tar --use-compress-program='zstd -3 -T0' \
              -cf /tmp/frontend-build-a11y.tar.zst \
              .next
          ls -lh /tmp/frontend-build-a11y.tar.zst
          echo "✅ Frontend (a11y) artifact packaged"

      - name: Upload frontend (a11y) artifact
        uses: actions/upload-artifact@v7
        with:
          name: frontend-build-a11y-${{ github.run_id }}
          path: /tmp/frontend-build-a11y.tar.zst
          retention-days: 1
          compression-level: 0
```

- [ ] **Step 3: Validate and commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): add build-frontend + build-frontend-a11y jobs

Two separate Next.js builds because NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED
is constant-folded at build time. Reduces 4× pnpm build (frontend,
frontend-a11y, frontend-bundle-size, e2e) to 2× upstream builds.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: Refactor `frontend` job — drop build step

**Files:**
- Modify: `.github/workflows/ci.yml` lines 78-147 (job `frontend`)

**Goal:** `frontend` job runs only lint/typecheck/unit-tests. `pnpm build` moved to `build-frontend` upstream.

- [ ] **Step 1: Remove `Build` step from `frontend` job (lines 135-138)**

Replace this block in `ci.yml`:

```yaml
      - name: Unit & Integration Tests
        run: pnpm test:coverage
        env:
          NODE_ENV: test
          CI: true

      - name: Build
        run: pnpm build
        env:
          NEXT_PUBLIC_API_BASE: http://localhost:8080

      - name: Upload Coverage
```

With:

```yaml
      - name: Unit & Integration Tests
        run: pnpm test:coverage
        env:
          NODE_ENV: test
          CI: true

      - name: Upload Coverage
```

- [ ] **Step 2: Rename job description to reflect new scope**

In `ci.yml` line 79, change:
```yaml
    name: Frontend - Build & Test
```
to:
```yaml
    name: Frontend - Lint, Typecheck & Test
```

- [ ] **Step 3: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): drop pnpm build from frontend job

The frontend job runs lint, typecheck and vitest — none need .next/.
pnpm build moved to upstream build-frontend job.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: Refactor `backend-unit` job — consume artifact

**Files:**
- Modify: `.github/workflows/ci.yml` lines 150-204 (job `backend-unit`)

**Goal:** Drop `dotnet restore`+`dotnet build`, replace with artifact download + extract.

- [ ] **Step 1: Update `needs:` and replace setup with artifact download**

Replace lines 152-178 in `ci.yml`:

```yaml
  backend-unit:
    name: Backend - Unit Tests
    runs-on: ${{ needs.changes.outputs.runner }}
    needs: changes
    if: needs.changes.outputs.backend == 'true' || github.event_name == 'workflow_dispatch'
    defaults:
      run:
        working-directory: apps/api
    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Install PDF Dependencies
        run: sudo apt-get update && sudo apt-get install -y libgdiplus

      - name: Setup Backend
        uses: ./.github/actions/setup-backend
        with:
          dotnet-version: '9.0.x'
          working-directory: apps/api

      - name: Install dotnet-coverage
        run: |
          dotnet tool install --global dotnet-coverage
          echo "$HOME/.dotnet/tools" >> $GITHUB_PATH

      - name: Build
        run: dotnet build --no-restore --configuration Release
```

with:

```yaml
  backend-unit:
    name: Backend - Unit Tests
    runs-on: ${{ needs.changes.outputs.runner }}
    needs: [changes, build-backend]
    if: needs.changes.outputs.backend == 'true' || github.event_name == 'workflow_dispatch'
    defaults:
      run:
        working-directory: apps/api
    steps:
      - name: Checkout
        uses: actions/checkout@v6

      - name: Install PDF Dependencies
        run: sudo apt-get update && sudo apt-get install -y libgdiplus

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '9.0.x'

      - name: Download backend artifact
        uses: actions/download-artifact@v7
        with:
          name: backend-build-${{ github.run_id }}
          path: /tmp/

      - name: Extract backend artifact
        shell: bash
        working-directory: apps/api
        run: |
          echo "📂 Extracting backend build artifact..."
          tar --use-compress-program='zstd -d' -xf /tmp/backend-build.tar.zst
          echo "✅ Backend artifact extracted"

      - name: Install dotnet-coverage
        run: |
          dotnet tool install --global dotnet-coverage
          echo "$HOME/.dotnet/tools" >> $GITHUB_PATH
```

- [ ] **Step 2: Verify the existing `Run Unit Tests with Coverage` step still uses `--no-build`**

The existing block (lines 179-195) already uses `--no-build --no-restore` implicitly via the `dotnet-coverage` wrapper. No change needed.

Just confirm in the file:
```yaml
      - name: Run Unit Tests with Coverage
        timeout-minutes: 30
        env:
          CI: true
        run: |
          echo "🧪 Running unit tests with dotnet-coverage..."
          mkdir -p coverage
          dotnet-coverage collect \
            "dotnet test MeepleAI.Api.sln \
              --filter Category=Unit \
              --settings tests/Api.Tests/unit.runsettings \
              --no-build \
              --configuration Release \
              --blame-hang-timeout 5min" \
            -f cobertura \
            -o coverage/unit-coverage.xml
          echo "✅ Unit tests passed"
```

- [ ] **Step 3: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): backend-unit consumes build-backend artifact

Drops restore+build (4-5 min). Downloads tar.zst artifact (~30s) and
runs tests with --no-build. dotnet-coverage wrapper runs at runtime so
no build-time instrumentation needed.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: Refactor `backend-integration` job — consume artifact + switch coverage

**Files:**
- Modify: `.github/workflows/ci.yml` lines 214-394 (job `backend-integration`)

**Goal:** Same as Task 5 but for the 3-shard matrix. Also switch coverage from `coverlet.msbuild` (build-time instrumentation) to `dotnet-coverage` (runtime) so `--no-build` works.

- [ ] **Step 1: Update `needs:` and replace build steps**

Find the `backend-integration` job. In the `needs:` line (218):
```yaml
    needs: changes
```
change to:
```yaml
    needs: [changes, build-backend]
```

Find lines ~280-333 (Setup Backend, Build step). Replace:

```yaml
      - name: Setup Backend
        uses: ./.github/actions/setup-backend
        with:
          dotnet-version: '9.0.x'
          working-directory: apps/api

      - name: Pre-Test Cleanup
        ...

      - name: Build
        run: dotnet build --no-restore --configuration Release
```

with:

```yaml
      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '9.0.x'

      - name: Download backend artifact
        uses: actions/download-artifact@v7
        with:
          name: backend-build-${{ github.run_id }}
          path: /tmp/

      - name: Extract backend artifact
        shell: bash
        working-directory: apps/api
        run: |
          echo "📂 Extracting backend build artifact..."
          tar --use-compress-program='zstd -d' -xf /tmp/backend-build.tar.zst
          echo "✅ Backend artifact extracted"

      - name: Install dotnet-coverage
        run: |
          dotnet tool install --global dotnet-coverage
          echo "$HOME/.dotnet/tools" >> $GITHUB_PATH

      - name: Pre-Test Cleanup
        ...
```

(Keep `Pre-Test Cleanup`, `Install Redis CLI`, `Wait for Services`, `Create CI Secret Files`, `Verify PostgreSQL Connection Limits` unchanged.)

- [ ] **Step 2: Replace `Run Integration Tests` step with `dotnet-coverage` wrapper**

Find the `Run Integration Tests` step (current lines ~340-385). Replace:

```yaml
        run: |
          echo "🧪 Running integration tests - shard: ${{ matrix.shard.name }} (4 parallel threads)..."
          dotnet test tests/Api.Tests/Api.Tests.csproj \
            --filter "Category=Integration&FullyQualifiedName!~UploadPdfMidPhaseCancellationTests&FullyQualifiedName!~FrontendSdk&FullyQualifiedName!~ArbitroAgent${{ matrix.shard.filter_extra }}" \
            --settings tests/Api.Tests/integration.runsettings \
            --logger "console;verbosity=minimal" \
            --no-build \
            --configuration Release \
            --blame-hang-timeout 5min \
            --blame-hang-dump-type mini \
            -p:CollectCoverage=true \
            -p:CoverletOutputFormat=cobertura \
            -p:CoverletOutput=./coverage/${{ matrix.shard.coverage_file }}
          echo "✅ Integration tests passed - shard: ${{ matrix.shard.name }}"
```

with:

```yaml
        run: |
          echo "🧪 Running integration tests - shard: ${{ matrix.shard.name }} (4 parallel threads)..."
          mkdir -p coverage
          # Build-once refactor (2026-05-25): switched from coverlet.msbuild
          # (-p:CollectCoverage=true) to dotnet-coverage wrapper so the tests
          # can run with --no-build using the upstream build-backend artifact.
          # Coverlet.msbuild requires build-time instrumentation; dotnet-coverage
          # is a runtime profiler attached to the dotnet test process.
          dotnet-coverage collect \
            "dotnet test tests/Api.Tests/Api.Tests.csproj \
              --filter \"Category=Integration&FullyQualifiedName!~UploadPdfMidPhaseCancellationTests&FullyQualifiedName!~FrontendSdk&FullyQualifiedName!~ArbitroAgent${{ matrix.shard.filter_extra }}\" \
              --settings tests/Api.Tests/integration.runsettings \
              --logger \"console;verbosity=minimal\" \
              --no-build \
              --configuration Release \
              --blame-hang-timeout 5min \
              --blame-hang-dump-type mini" \
            -f cobertura \
            -o coverage/${{ matrix.shard.coverage_file }}
          echo "✅ Integration tests passed - shard: ${{ matrix.shard.name }}"
```

- [ ] **Step 3: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): backend-integration consumes artifact + dotnet-coverage

Switches from coverlet.msbuild (build-time instrumentation, requires
build before test) to dotnet-coverage wrapper (runtime profiler,
--no-build friendly). Each of 3 shards now downloads the upstream
backend artifact instead of rebuilding the solution — ~15 min CPU saved
across the matrix.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: Refactor `e2e` job — consume both artifacts

**Files:**
- Modify: `.github/workflows/ci.yml` lines 445-626 (job `e2e`)

**Goal:** Drop backend `dotnet restore`+`dotnet build` and frontend `pnpm build`. Download both artifacts.

- [ ] **Step 1: Update `needs:` and replace backend build steps**

Change line 448 from:
```yaml
    needs: changes
```
to:
```yaml
    needs: [changes, build-backend, build-frontend]
```

Find current lines 512-529 (Fix dotnet permissions, Setup .NET, Restore Backend, Build Backend). Replace:

```yaml
      - name: Fix dotnet directory permissions
        run: |
          if [ -d "/usr/share/dotnet" ]; then
            sudo chown -R $(whoami) /usr/share/dotnet || true
          fi

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '9.0.x'

      - name: Restore Backend Dependencies
        working-directory: apps/api/src/Api
        run: dotnet restore

      - name: Build Backend
        working-directory: apps/api/src/Api
        run: dotnet build --no-restore --configuration Release
```

with:

```yaml
      - name: Fix dotnet directory permissions
        run: |
          if [ -d "/usr/share/dotnet" ]; then
            sudo chown -R $(whoami) /usr/share/dotnet || true
          fi

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '9.0.x'

      - name: Download backend artifact
        uses: actions/download-artifact@v7
        with:
          name: backend-build-${{ github.run_id }}
          path: /tmp/

      - name: Extract backend artifact
        shell: bash
        working-directory: apps/api
        run: |
          echo "📂 Extracting backend build artifact..."
          tar --use-compress-program='zstd -d' -xf /tmp/backend-build.tar.zst
          echo "✅ Backend artifact extracted"
```

- [ ] **Step 2: Replace frontend `pnpm build` step with artifact download**

Find current lines 597-609 (Setup Frontend, Setup Playwright, Build App). Replace:

```yaml
      # ===== Frontend Setup =====
      - name: Setup Frontend
        uses: ./.github/actions/setup-frontend
        with:
          working-directory: apps/web
          frozen-lockfile: 'true'

      - name: Setup Playwright
        uses: ./.github/actions/setup-playwright
        with:
          working-directory: apps/web

      - name: Build App
        run: pnpm build
```

with:

```yaml
      # ===== Frontend Setup =====
      - name: Setup Frontend
        uses: ./.github/actions/setup-frontend
        with:
          working-directory: apps/web
          frozen-lockfile: 'true'

      - name: Setup Playwright
        uses: ./.github/actions/setup-playwright
        with:
          working-directory: apps/web

      - name: Download frontend artifact
        uses: actions/download-artifact@v7
        with:
          name: frontend-build-${{ github.run_id }}
          path: /tmp/

      - name: Extract frontend artifact
        shell: bash
        working-directory: apps/web
        run: |
          echo "📂 Extracting frontend build artifact..."
          tar --use-compress-program='zstd -d' -xf /tmp/frontend-build.tar.zst
          echo "✅ Frontend artifact extracted"
```

- [ ] **Step 3: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): e2e consumes backend+frontend artifacts

Drops dotnet build (~5 min) and pnpm build (~100s). Downloads upstream
build artifacts. Backend API and Next.js server start with --no-build
flags. Total saving: ~7 min CPU on the e2e critical path.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 8: Refactor `frontend-a11y` job — consume a11y artifact

**Files:**
- Modify: `.github/workflows/ci.yml` lines 651-705 (job `frontend-a11y`)

**Goal:** Drop `pnpm build`, download a11y-flagged frontend artifact.

- [ ] **Step 1: Update `needs:` and replace Build App step**

Change line 655 from:
```yaml
    needs: changes
```
to:
```yaml
    needs: [changes, build-frontend-a11y]
```

Find current lines 681-689 (Build App). Replace:

```yaml
      - name: Build App
        run: pnpm build
        env:
          NEXT_PUBLIC_API_BASE: http://localhost:8080
          # Wave B.1 (#633): enable visual-test fixture so e2e/a11y/games-library.spec.ts
          # can scan /games?tab=library default + filtered-empty surfaces without a backend.
          # Constant-folded at build time; STATE_OVERRIDE_ENABLED also unlocks `?state=...`
          # URL hatch under prod NODE_ENV. Pre-existing accessibility.spec.ts is unaffected.
          NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED: '1'
```

with:

```yaml
      - name: Download frontend (a11y) artifact
        uses: actions/download-artifact@v7
        with:
          name: frontend-build-a11y-${{ github.run_id }}
          path: /tmp/

      - name: Extract frontend (a11y) artifact
        shell: bash
        working-directory: apps/web
        run: |
          echo "📂 Extracting frontend (a11y) build artifact..."
          tar --use-compress-program='zstd -d' -xf /tmp/frontend-build-a11y.tar.zst
          echo "✅ Frontend (a11y) artifact extracted"
```

- [ ] **Step 2: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): frontend-a11y consumes build-frontend-a11y artifact

Replaces pnpm build (~100s with visual fixture flag) with artifact
download (~10s).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 9: Refactor `frontend-bundle-size` job — consume default artifact

**Files:**
- Modify: `.github/workflows/ci.yml` lines 712-747 (job `frontend-bundle-size`)

- [ ] **Step 1: Update `needs:` and replace Build App step**

Change line 716 from:
```yaml
    needs: changes
```
to:
```yaml
    needs: [changes, build-frontend]
```

Find current lines 733-736 (Build App). Replace:

```yaml
      - name: Build App
        run: pnpm build
        env:
          NEXT_PUBLIC_API_BASE: http://localhost:8080
```

with:

```yaml
      - name: Download frontend artifact
        uses: actions/download-artifact@v7
        with:
          name: frontend-build-${{ github.run_id }}
          path: /tmp/

      - name: Extract frontend artifact
        shell: bash
        working-directory: apps/web
        run: |
          echo "📂 Extracting frontend build artifact..."
          tar --use-compress-program='zstd -d' -xf /tmp/frontend-build.tar.zst
          echo "✅ Frontend artifact extracted"
```

- [ ] **Step 2: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): frontend-bundle-size consumes build-frontend artifact

Replaces pnpm build (~100s) with artifact download (~10s).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 10: Update `ci-success` aggregator with new dependencies

**Files:**
- Modify: `.github/workflows/ci.yml` lines 750-786 (job `ci-success`)

**Goal:** Add `build-backend`, `build-frontend`, `build-frontend-a11y` as required dependencies so a build failure surfaces immediately.

- [ ] **Step 1: Update `needs:` array in `ci-success` job**

Find line 753:
```yaml
    needs: [frontend, frontend-a11y, frontend-bundle-size, backend-unit, backend-integration, python-tests, e2e]
```

Replace with:
```yaml
    needs: [build-backend, build-frontend, build-frontend-a11y, frontend, frontend-a11y, frontend-bundle-size, backend-unit, backend-integration, python-tests, e2e]
```

- [ ] **Step 2: Add explicit build-job status checks in the script body**

Find the `Check Status` step (current lines 756-786). After the existing `for job in frontend ...` loop, add a separate block for upstream builds:

Replace:
```yaml
      - name: Check Status
        run: |
          # Required jobs (block CI)
          # NOTE: frontend-a11y was non-blocking via job-level continue-on-error
          # until Phase D gate flip (#1094 closure, 2026-05-18). It is now in the
          # required-jobs list — any axe AA violation fails CI. #1015 (release-
          # level baseline-diff) is complementary, not a substitute.
          for job in frontend frontend-a11y frontend-bundle-size backend-unit python-tests e2e; do
            result="${{ needs.frontend.result }}"
            case "$job" in
              frontend-a11y) result="${{ needs.frontend-a11y.result }}" ;;
              frontend-bundle-size) result="${{ needs.frontend-bundle-size.result }}" ;;
              backend-unit) result="${{ needs.backend-unit.result }}" ;;
              python-tests) result="${{ needs.python-tests.result }}" ;;
              e2e) result="${{ needs.e2e.result }}" ;;
            esac
            if [ "$result" == "failure" ] || [ "$result" == "cancelled" ]; then
              echo "❌ CI Failed: $job=$result"
              exit 1
            fi
          done
```

with:
```yaml
      - name: Check Status
        run: |
          # Required upstream build jobs (block CI immediately if build fails).
          # Build-Once-Test-Many refactor (2026-05-25): test jobs depend on these
          # artifacts, so a build failure cascades as 'skipped' downstream which
          # the loop below would not catch.
          for build_job in build-backend build-frontend build-frontend-a11y; do
            case "$build_job" in
              build-backend) result="${{ needs.build-backend.result }}" ;;
              build-frontend) result="${{ needs.build-frontend.result }}" ;;
              build-frontend-a11y) result="${{ needs.build-frontend-a11y.result }}" ;;
            esac
            # 'skipped' is OK (paths filter skipped the job); 'failure'/'cancelled' is not.
            if [ "$result" == "failure" ] || [ "$result" == "cancelled" ]; then
              echo "❌ CI Failed: upstream build $build_job=$result"
              exit 1
            fi
          done

          # Required test jobs (block CI)
          # NOTE: frontend-a11y was non-blocking via job-level continue-on-error
          # until Phase D gate flip (#1094 closure, 2026-05-18). It is now in the
          # required-jobs list — any axe AA violation fails CI. #1015 (release-
          # level baseline-diff) is complementary, not a substitute.
          for job in frontend frontend-a11y frontend-bundle-size backend-unit python-tests e2e; do
            result="${{ needs.frontend.result }}"
            case "$job" in
              frontend-a11y) result="${{ needs.frontend-a11y.result }}" ;;
              frontend-bundle-size) result="${{ needs.frontend-bundle-size.result }}" ;;
              backend-unit) result="${{ needs.backend-unit.result }}" ;;
              python-tests) result="${{ needs.python-tests.result }}" ;;
              e2e) result="${{ needs.e2e.result }}" ;;
            esac
            if [ "$result" == "failure" ] || [ "$result" == "cancelled" ]; then
              echo "❌ CI Failed: $job=$result"
              exit 1
            fi
          done
```

- [ ] **Step 3: Update the `notify-end` job needs list (line 875)**

Change:
```yaml
    needs: [changes, frontend, frontend-a11y, frontend-bundle-size, backend-unit, backend-integration, python-tests, e2e, ci-success, deploy-preview]
```

to:
```yaml
    needs: [changes, build-backend, build-frontend, build-frontend-a11y, frontend, frontend-a11y, frontend-bundle-size, backend-unit, backend-integration, python-tests, e2e, ci-success, deploy-preview]
```

- [ ] **Step 4: Commit**

```bash
actionlint .github/workflows/ci.yml
git add .github/workflows/ci.yml
git commit -m "ci(build-once): ci-success + notify-end depend on upstream builds

Adds build-backend, build-frontend, build-frontend-a11y to required
job lists. Build failures now fail CI immediately (instead of cascading
silently as 'skipped' downstream).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 11: Final validation + push

**Files:** none (validation only)

- [ ] **Step 1: Final actionlint pass**

```bash
actionlint .github/workflows/ci.yml
# Expected: no errors
```

- [ ] **Step 2: Visual diff review**

```bash
git diff main-dev -- .github/workflows/ci.yml | head -200
git log --oneline main-dev..HEAD
# Expected: ~9-10 commits, all on .github/workflows/ci.yml + plan file
```

- [ ] **Step 3: Push branch**

```bash
git push -u origin feature/ci-build-once-test-many
```

- [ ] **Step 4: Open PR to main-dev (CLAUDE.md rule)**

```bash
gh pr create \
  --base main-dev \
  --title "ci: Build Once, Test Many — eliminate duplicate dotnet/pnpm builds" \
  --body "$(cat <<'EOF'
## Summary

Introduces upstream \`build-backend\`, \`build-frontend\`, \`build-frontend-a11y\` jobs that publish artifacts consumed by downstream test jobs. Eliminates:

- **5× \`dotnet build\`** duplication (backend-unit + 3 integration shards + e2e) → 1 build + 5 downloads
- **4× \`pnpm build\`** duplication (frontend, frontend-a11y, frontend-bundle-size, e2e) → 2 builds (default + a11y due to constant-folded NEXT_PUBLIC_*) + 2 downloads

## Expected impact

- **CPU time per CI run: −25 min** (~−20 min backend, ~−3 min frontend, +small artifact transfer overhead)
- **Wall-clock**: marginal reduction (~3 min) — backend-integration Core shard remains the critical-path bottleneck at ~50 min
- **Strategic value**: enables future shard increases + cross-workflow artifact reuse (deploy-staging pre-deploy-check, test-e2e.yml 6-shard, etc.)

## Technical notes

- **Coverage switch**: \`backend-integration\` migrates from coverlet.msbuild (\`-p:CollectCoverage=true\`, build-time instrumentation) to \`dotnet-coverage\` wrapper (runtime profiler, \`--no-build\` friendly), matching the pattern already used in \`backend-unit\`.
- **Two frontend builds**: \`NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED\` is constant-folded at build time by Next.js, so a11y needs its own artifact. Still −50% vs the 4× duplication.
- **Artifact format**: tar+zstd compressed, 1-day retention. Avoids \`actions/upload-artifact@v4\` silent permission stripping on executables.

## Plan & spec

See [\`docs/for-developers/specs/2026-05-25-ci-build-once-test-many.md\`](docs/for-developers/specs/2026-05-25-ci-build-once-test-many.md) for the full implementation plan and rationale (drawn from the \`/sc:spec-panel\` analysis 2026-05-25).

## Out of scope (separate follow-ups)

- P2.1 MaxCpuCount audit (backend-integration parallelism)
- P1.2 CodeQL C# scope reduction
- P1.3 \`deploy-staging.yml\` pre-deploy-check consolidation
- Apply the same pattern to \`dev-fast.yml\`, \`staging.yml\`, \`test-e2e.yml\`

## Validation

- Local: \`actionlint .github/workflows/ci.yml\` passes
- CI: this PR's first run will validate the artifact pass-through end-to-end

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Monitor first CI run**

```bash
gh pr view --json url
gh run watch  # or: gh run list --branch feature/ci-build-once-test-many --limit 1
```

- [ ] **Step 6: Iterate on any failure**

If actionlint or CI run reveals issues:
1. Identify root cause from logs (`gh run view <id> --log-failed`)
2. Fix in a new commit (don't amend pushed history)
3. Push and re-run

---

## Self-Review Checklist

**Spec coverage:**
- ✅ Backend build duplication addressed (Task 2 + Tasks 5, 6, 7 consumers)
- ✅ Frontend build duplication addressed (Task 3 + Tasks 7, 8, 9 consumers)
- ✅ Coverage instrumentation handled (Task 6 dotnet-coverage switch)
- ✅ Aggregator job updated (Task 10)
- ✅ Two frontend builds explicitly designed (default + a11y, Task 3)

**Placeholders:** none. All steps contain exact commands and YAML blocks.

**Type/name consistency:**
- Artifact names: `backend-build-${{ github.run_id }}`, `frontend-build-${{ github.run_id }}`, `frontend-build-a11y-${{ github.run_id }}` — used consistently in all producer/consumer steps.
- Job names: `build-backend`, `build-frontend`, `build-frontend-a11y` — used in `needs:` consistently.
- File paths: `/tmp/backend-build.tar.zst`, `/tmp/frontend-build.tar.zst`, `/tmp/frontend-build-a11y.tar.zst` — consistent across upload/download.

**Known risks:**
1. **dotnet-coverage on integration tests**: not yet exercised in CI. If runtime profiler conflicts with Testcontainers (unlikely) or with parallel test threads, fall back to single-thread coverage collection.
2. **Artifact retention**: 1-day chosen for cost; if a stuck PR needs > 24h iteration, re-run CI to regenerate.
3. **Tar+zstd shell availability**: \`zstd\` is preinstalled on ubuntu-latest GitHub runners (apt: zstd in default image). Verified 2026-05-25.
4. **paths-ignore semantics**: If \`backend == 'false'\` and \`frontend == 'true'\`, only frontend builds run. Test jobs gated on \`backend\` correctly skip via existing `if:` clauses — verified inheritance from `changes.outputs.backend`.

---

## Execution mode

**User chose**: inline execution with single PR (per session preferences 2026-05-25).
**Sub-skill**: `superpowers:executing-plans`.

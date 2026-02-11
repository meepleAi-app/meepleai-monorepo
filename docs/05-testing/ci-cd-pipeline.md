# CI/CD Pipeline Guide

**Comprehensive testing pipeline across all test types**

## Table of Contents

1. [Overview](#overview)
2. [Workflow Architecture](#workflow-architecture)
3. [Stage 1-5: Core CI Pipeline](#stage-1-5-core-ci-pipeline)
4. [Stage 6: E2E Tests](#stage-6-e2e-tests)
5. [Stage 7: Visual Regression](#stage-7-visual-regression)
6. [Stage 8: Performance Audit](#stage-8-performance-audit)
7. [Parallel Execution Strategies](#parallel-execution-strategies)
8. [Coverage & Reporting](#coverage--reporting)
9. [Artifact Management](#artifact-management)
10. [Path Filtering](#path-filtering)
11. [Troubleshooting](#troubleshooting)

---

## Overview

MeepleAI's CI/CD pipeline provides comprehensive automated testing across 8 stages, from fast linting checks to deep performance audits. The pipeline is designed for:

- **Speed**: Parallel execution, path filtering, fail-fast strategies
- **Quality**: 90%+ coverage targets, multiple test types
- **Observability**: PR comments, artifacts, coverage reports
- **Reliability**: Health checks, retry logic, comprehensive error handling

### Key Metrics

| Metric | Target | Status |
|--------|--------|--------|
| **Backend Coverage** | ≥90% | ✅ Enforced |
| **Frontend Coverage** | ≥85% | ✅ Enforced |
| **E2E Pass Rate** | ≥90% | ✅ Quality Gate |
| **Performance Score** | ≥85% | ✅ Lighthouse CI |
| **Pipeline Duration** | <15 min (typical) | ✅ Optimized |

---

## Workflow Architecture

### Workflow Distribution

Four primary workflows handle the 8 pipeline stages:

| Workflow | Stages | Trigger | Duration |
|----------|--------|---------|----------|
| **ci.yml** | 1-5 (Lint, TypeCheck, Unit, Integration) | Push/PR to main branches | ~8-12 min |
| **e2e-tests.yml** | 6 (E2E with 4-shard parallelization) | Path filtering (web/api changes) | ~6-8 min |
| **visual-regression.yml** | 7 (Playwright snapshots + Chromatic) | PR/push to main branches | ~5-7 min |
| **lighthouse-ci.yml** | 8 (Performance audit + Core Web Vitals) | PR/push to main branches | ~4-6 min |

**Supporting workflows:**
- `k6-performance.yml`: Load/stress testing (nightly schedule + manual)
- `security.yml`: SAST, dependency scanning, secrets detection (weekly + PR)
- `branch-policy.yml`: Enforces feature → frontend-dev → main-dev → main flow
- `dependabot-automerge.yml`: Auto-merges security patches if CI passes

### Trigger Patterns

**Push/PR Triggers:**
```yaml
# Most workflows
on:
  push:
    branches: [main, main-dev, frontend-dev]
  pull_request:
    branches: [main, main-dev, frontend-dev]
```

**Scheduled Triggers:**
```yaml
# K6 Performance (nightly)
on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM UTC daily

# Security Scan (weekly)
on:
  schedule:
    - cron: '0 0 * * 0'  # Sunday midnight
```

**Manual Triggers:**
```yaml
# All major workflows support workflow_dispatch
on:
  workflow_dispatch:
    inputs:
      debug_enabled:
        description: 'Enable debug logging'
        type: boolean
        default: false
```

### Concurrency Control

All workflows implement concurrency groups to prevent resource waste:

```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

**Effect:** Only one run per PR/branch; new pushes cancel in-progress runs.

---

## Stage 1-5: Core CI Pipeline

**File:** `.github/workflows/ci.yml`

### Stage 1: Lint + TypeCheck (Fail Fast)

**Purpose:** Catch syntax/type errors before expensive tests

**Frontend:**
```bash
cd apps/web
pnpm install --frozen-lockfile
pnpm lint        # ESLint + Prettier
pnpm typecheck   # TypeScript compiler check
```

**Backend:**
```bash
cd apps/api/src/Api
dotnet restore
dotnet build --no-restore  # Implicit type check
```

**Duration:** ~2-3 min
**Failure Strategy:** Fail-fast (blocks all subsequent jobs)

---

### Stage 2: Backend Unit Tests

**Filter:** `Category=Unit`

**Command:**
```bash
dotnet test \
  --filter "Category=Unit" \
  --logger "console;verbosity=minimal" \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura \
  -p:CoverletOutput=coverage/unit-coverage.xml
```

**Coverage Target:** ≥90%
**Output:** `apps/api/coverage/unit-coverage.xml` → Codecov
**Duration:** ~3-4 min

**Test Categories:**
- Domain entity logic
- Value object validation
- Command/Query handlers (mocked dependencies)
- FluentValidation rules

---

### Stage 3: Frontend Unit Tests

**Tool:** Vitest + MSW (Mock Service Worker)

**Command:**
```bash
cd apps/web
pnpm test:coverage
```

**Configuration:**
- Setup: `vitest.setup.tsx` (MSW global config)
- Coverage: Istanbul instrumentation
- Output: `apps/web/coverage/lcov.info` → Codecov

**Coverage Target:** ≥85%
**Duration:** ~2-3 min

**Test Types:**
- Component unit tests
- Hook testing
- Utility function tests
- MSW-mocked API integration tests

---

### Stage 4: Backend Integration Tests

**Filter:** `Category=Integration`
**Infrastructure:** Testcontainers (postgres, redis, qdrant)

**Service Containers:**

```yaml
services:
  postgres:
    image: postgres:16-alpine
    options: >-
      --health-cmd pg_isready
      --health-interval 5s
      --health-timeout 5s
      --health-retries 10
    env:
      POSTGRES_DB: meepleai_test
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: testpass
      POSTGRES_INITDB_ARGS: "-c max_connections=500 --shared-buffers=512MB"  # Issue #2693

  redis:
    image: redis:7-alpine
    options: >-
      --health-cmd "redis-cli ping"
      --health-interval 3s
      --health-timeout 3s
      --health-retries 10

  qdrant:
    image: qdrant/qdrant:v1.12.4
    options: >-
      --health-cmd "bash -c '</dev/tcp/127.0.0.1/6333'"  # Custom TCP check
      --health-interval 5s
      --health-timeout 5s
      --health-retries 30
```

**Health Check Strategy:**
- PostgreSQL: pg_isready (native Postgres utility)
- Redis: redis-cli ping (native Redis utility)
- Qdrant: TCP connection test (wget/curl unavailable in image)

**Timeout Calculation:**
- PostgreSQL: 5s interval × 10 retries = ~60s max
- Redis: 3s interval × 10 retries = ~40s max
- Qdrant: 5s interval × 30 retries = ~160s max

**Command:**
```bash
dotnet test \
  --filter "Category=Integration" \
  --logger "console;verbosity=minimal" \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura \
  -p:CoverletOutput=coverage/integration-coverage.xml
```

**Environment:**
```bash
ASPNETCORE_ENVIRONMENT=Testing
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=testpass"
ConnectionStrings__Redis="localhost:6379"
ConnectionStrings__Qdrant="http://localhost:6333"
```

**Coverage Target:** ≥90% (combined with unit)
**Duration:** ~4-6 min

**Test Scope:**
- Database persistence (EF Core)
- Redis caching
- Qdrant vector operations
- MediatR pipeline integration
- Full command/query flows

---

### Stage 5: Frontend Integration Tests

**Tool:** Vitest + MSW (API mocking)

**Command:** Same as Stage 3 (`pnpm test:coverage`)
**Scope:** Tests with MSW-mocked backend

**MSW Setup:**
```typescript
// vitest.setup.tsx
import { setupServer } from 'msw/node';
import { handlers } from '@/test/mocks/handlers';

const server = setupServer(...handlers);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

**Test Types:**
- API integration (React Query + MSW)
- Form submissions with validation
- State management (Zustand)
- Error handling flows

**Duration:** Included in Stage 3 (~2-3 min total)

---

## Stage 6: E2E Tests

**File:** `.github/workflows/e2e-tests.yml`
**Tool:** Playwright
**Strategy:** 4-shard parallel execution

### Architecture

**Trigger:** Path filtering on web/api changes

```yaml
- id: changes
  uses: dorny/paths-filter@v3
  with:
    filters: |
      web:
        - 'apps/web/**'
      api:
        - 'apps/api/**'
```

**Matrix Strategy:**
```yaml
strategy:
  matrix:
    shard: [1, 2, 3, 4]
  fail-fast: false
```

**Test Distribution:** Playwright auto-distributes tests across shards based on file count.

### Execution

**Setup:**
1. Start service containers (postgres, redis, qdrant, n8n, hyperdx)
2. Build backend API (`dotnet build`)
3. Start API server (`dotnet run`)
4. Build frontend (`pnpm build`)
5. Start frontend (`pnpm start` - production mode for FORCE_PRODUCTION_SERVER=true)

**Command:**
```bash
pnpm test:e2e:shard${{ matrix.shard }}
```

**Scripts (package.json):**
```json
{
  "test:e2e:shard1": "playwright test --shard=1/4",
  "test:e2e:shard2": "playwright test --shard=2/4",
  "test:e2e:shard3": "playwright test --shard=3/4",
  "test:e2e:shard4": "playwright test --shard=4/4"
}
```

**Environment:**
```bash
CI=true
NODE_ENV=test
NEXT_PUBLIC_API_BASE=http://localhost:8080
FORCE_PRODUCTION_SERVER=true  # Uses 'next start' instead of 'next dev'
```

**Configuration:** `apps/web/playwright.config.ts`

### Quality Gate

**Job:** `e2e-quality-gate` (runs after all shards)

**Logic:**
1. Download all shard reports
2. Merge HTML reports
3. Parse test results
4. Calculate pass rate
5. Enforce ≥90% threshold

**Pass Rate Calculation:**
```javascript
const passRate = (passed / total) * 100;
const qualityGatePassed = passRate >= 90;
```

**PR Comment:**
```markdown
## 🎭 E2E Test Results

**Pass Rate:** 95% (104/109 tests)
**Quality Gate:** ✅ PASSED (≥90% required)

**Artifacts:**
- 📊 [HTML Report](link)
- 📸 Screenshots (failures only)
- 📦 Traces (failures only)
- 📈 Coverage Report
```

**Failure Handling:**
- Screenshots uploaded (7-day retention)
- Playwright traces uploaded (debugging)
- HTML report with full details
- Quality gate blocks merge if <90%

### Cross-Browser Testing

**Full Suite (Manual Trigger):**
```yaml
matrix:
  project:
    - desktop-chrome
    - desktop-firefox
    - desktop-safari
    - mobile-chrome
    - mobile-safari
    - tablet-chrome
```

**Playwright Projects:**
```typescript
// playwright.config.ts
export default defineConfig({
  projects: [
    { name: 'desktop-chrome', use: { ...devices['Desktop Chrome'] } },
    { name: 'desktop-firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'desktop-safari', use: { ...devices['Desktop Safari'] } },
    { name: 'mobile-chrome', use: { ...devices['Pixel 5'] } },
    { name: 'mobile-safari', use: { ...devices['iPhone 12'] } },
    { name: 'tablet-chrome', use: { ...devices['iPad Pro'] } },
  ],
});
```

**Duration:**
- 4-shard execution: ~6-8 min
- Full 6-project suite: ~25-30 min (manual only)

---

## Stage 7: Visual Regression

**File:** `.github/workflows/visual-regression.yml`
**Tools:** Playwright (snapshots) + Chromatic (Storybook)

### Playwright Visual Testing

**Command:**
```bash
pnpm test:e2e:visual
```

**Process:**
1. Run E2E tests with snapshot assertions
2. Compare screenshots to baseline
3. Report differences

**Configuration:**
```typescript
// playwright.config.ts
use: {
  screenshot: 'only-on-failure',
  video: 'retain-on-failure',
}
```

**Update Baseline:**
```bash
pnpm test:e2e:visual:update
```

### Chromatic Integration

**Command:**
```bash
pnpm chromatic:ci
```

**Environment:**
```bash
CHROMATIC_PROJECT_TOKEN=${{ secrets.CHROMATIC_PROJECT_TOKEN }}
```

**Features:**
- Storybook component visual review
- Pixel-perfect diff detection
- Collaboration UI for approvals
- Baseline management

**PR Comment:**
```markdown
## 🎨 Visual Regression Results

**Changes Detected:** 3 components
**Status:** ⚠️ Requires Review

**Links:**
- [Chromatic Build](link)
- [Storybook Preview](link)
- [Full Report](link)
```

**Duration:** ~5-7 min
**Failure Strategy:** Non-blocking (continue-on-error: true)

---

## Stage 8: Performance Audit

**File:** `.github/workflows/lighthouse-ci.yml`
**Tool:** Lighthouse CI

### Architecture

**Shared Build Strategy:**
```yaml
jobs:
  build-nextjs:
    steps:
      - name: Build Next.js
        run: pnpm build
      - name: Upload build
        uses: actions/upload-artifact@v4
        with:
          name: nextjs-build
          path: |
            apps/web/.next
            apps/web/node_modules/.cache
          retention-days: 1

  lighthouse-performance:
    needs: build-nextjs
    steps:
      - name: Download build
        uses: actions/download-artifact@v4
```

**Benefit:** Avoid 3x builds (saves ~2-3 min per dependent job)

### Configuration

**File:** `apps/web/.lighthouseci/lighthouserc.json`

```json
{
  "ci": {
    "collect": {
      "url": ["http://localhost:3000"],
      "numberOfRuns": 3
    },
    "assert": {
      "preset": "lighthouse:recommended",
      "assertions": {
        "categories:performance": ["error", { "minScore": 0.85 }],
        "categories:accessibility": ["error", { "minScore": 0.95 }],
        "categories:best-practices": ["error", { "minScore": 0.90 }],
        "categories:seo": ["error", { "minScore": 0.90 }]
      }
    },
    "upload": {
      "target": "temporary-public-storage"
    }
  }
}
```

### Core Web Vitals Targets

| Metric | Target | Description |
|--------|--------|-------------|
| **LCP** | <2.5s | Largest Contentful Paint (loading performance) |
| **FID** | <100ms | First Input Delay (interactivity) |
| **CLS** | <0.1 | Cumulative Layout Shift (visual stability) |
| **FCP** | <1.8s | First Contentful Paint (perceived load speed) |
| **TBT** | <200ms | Total Blocking Time (main thread blocking) |
| **SI** | <3.4s | Speed Index (visual progression) |

### Regression Detection

**Job:** `performance-regression-check`

**Logic:**
```yaml
- name: Compare with base
  run: |
    BASE_SCORE=$(cat base-manifest.json | jq '.performance')
    CURRENT_SCORE=$(cat current-manifest.json | jq '.performance')
    DIFF=$(echo "$CURRENT_SCORE - $BASE_SCORE" | bc)
    if (( $(echo "$DIFF < -0.10" | bc -l) )); then
      echo "::error::Performance regression detected: ${DIFF}%"
      exit 1
    fi
```

**Threshold:** 10% degradation = build failure

**PR Comment:**
```markdown
## 🚀 Lighthouse Performance Audit

**Performance:** 89% (+2% vs base)
**Accessibility:** 97%
**Best Practices:** 92%
**SEO:** 95%

**Core Web Vitals:**
- ✅ LCP: 2.1s (<2.5s)
- ✅ FID: 45ms (<100ms)
- ✅ CLS: 0.05 (<0.1)

**Regressions:** None detected
**Full Report:** [View Details](link)
```

**Duration:** ~4-6 min
**Failure Strategy:** Non-blocking (continue-on-error: true during alpha)

---

## Parallel Execution Strategies

### 1. E2E 4-Shard Parallelization

**Time Reduction:** ~75%

**Sequential (before):** 4 shards × 6 min each = 24 min
**Parallel (current):** 1 × 6 min = 6 min

**Distribution:**
```yaml
matrix:
  shard: [1, 2, 3, 4]
```

Playwright auto-distributes tests based on file count and historical duration.

**Aggregation:**
```yaml
- name: Download shard reports
  uses: actions/download-artifact@v4
  with:
    pattern: playwright-report-*
    path: all-reports/
```

### 2. Frontend/Backend Parallel Jobs

**ci.yml Structure:**
```yaml
jobs:
  lint-typecheck-frontend:    # Runs in parallel
  lint-typecheck-backend:     # Runs in parallel
  frontend-unit-tests:        # Needs: lint-typecheck-frontend
  backend-unit-tests:         # Needs: lint-typecheck-backend
  backend-integration-tests:  # Needs: backend-unit-tests
```

**Parallelization Points:**
- Lint/TypeCheck: Frontend & Backend run simultaneously
- Unit tests: Frontend & Backend run after respective linting

**Time Savings:** ~3-4 min (vs sequential)

### 3. Shared Build Caching

**Lighthouse CI Optimization:**
```yaml
build-nextjs: ~3 min (once)
  ↓
lighthouse-performance: downloads build (~30s)
lighthouse-cli: downloads build (~30s)
performance-regression-check: downloads build (~30s)
```

**Without sharing:** 3 × 3 min = 9 min
**With sharing:** 3 min + 3 × 30s = 4.5 min
**Savings:** ~4.5 min

### 4. Cross-Browser Matrix (Manual)

**Full E2E Suite:**
```yaml
matrix:
  project: [desktop-chrome, desktop-firefox, desktop-safari, mobile-chrome, mobile-safari, tablet-chrome]
```

**Each project runs independently** with full backend services.

**Duration:** ~25-30 min (all projects in parallel vs ~2.5-3 hours sequential)

---

## Coverage & Reporting

### Codecov Integration

**Configuration:** `.codecov.yml` (if exists)

**Upload Pattern:**
```yaml
- name: Upload coverage
  uses: codecov/codecov-action@v4
  with:
    token: ${{ secrets.CODECOV_TOKEN }}
    files: ./coverage/coverage.xml
    flags: backend
    fail_ci_if_error: false  # Non-blocking during alpha
```

**Coverage Sources:**

| Source | File | Flag | Job |
|--------|------|------|-----|
| Frontend Unit/Integration | `apps/web/coverage/lcov.info` | `frontend` | ci.yml |
| Backend Unit | `apps/api/coverage/unit-coverage.xml` | `backend` | ci.yml |
| Backend Integration | `apps/api/coverage/integration-coverage.xml` | `backend` | ci.yml |
| E2E Coverage | `apps/web/coverage-e2e/` | `e2e` | e2e-tests.yml |

**Targets:**

| Type | Target | Enforcement |
|------|--------|-------------|
| Backend (Unit + Integration) | ≥90% | ✅ Enforced |
| Frontend (Unit + Integration) | ≥85% | ✅ Enforced |
| E2E Coverage | ≥70% | 📊 Monitored |

### Test Result Reporting

**PR Comment Automation:**

**E2E Results (e2e-quality-gate):**
```javascript
const comment = `
## 🎭 E2E Test Results
**Pass Rate:** ${passRate}% (${passed}/${total} tests)
**Quality Gate:** ${passed ? '✅' : '❌'} ${passed ? 'PASSED' : 'FAILED'} (≥90% required)

**Artifacts:**
- 📊 [HTML Report](${reportUrl})
- 📸 Screenshots (failures only)
- 📦 Traces (failures only)
`;
```

**Lighthouse Performance (Comment PR job):**
```javascript
const comment = `
## 🚀 Lighthouse Performance Audit
**Performance:** ${scores.performance}%
**Accessibility:** ${scores.accessibility}%
**Best Practices:** ${scores.bestPractices}%
**SEO:** ${scores.seo}%

**Core Web Vitals:**
- ${lcp < 2.5 ? '✅' : '❌'} LCP: ${lcp}s (<2.5s)
- ${fid < 100 ? '✅' : '❌'} FID: ${fid}ms (<100ms)
- ${cls < 0.1 ? '✅' : '❌'} CLS: ${cls} (<0.1)
`;
```

**Chromatic Visual (visual-regression.yml):**
```javascript
const comment = `
## 🎨 Visual Regression Results
**Changes Detected:** ${changesCount} components
**Status:** ${changesCount > 0 ? '⚠️ Requires Review' : '✅ No Changes'}

**Links:**
- [Chromatic Build](${chromaticUrl})
- [Storybook Preview](${storybookUrl})
`;
```

---

## Artifact Management

### Upload Patterns

**Conditional Uploads (failures only):**
```yaml
- name: Upload screenshots
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: screenshots-${{ matrix.shard }}
    path: apps/web/test-results/**/*.png
    retention-days: 7
```

**Always Uploads:**
```yaml
- name: Upload Playwright report
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: playwright-report-${{ matrix.shard }}
    path: apps/web/playwright-report/
    retention-days: 7
```

### Retention Policies

| Artifact Type | Retention | Rationale |
|---------------|-----------|-----------|
| Shared builds (.next) | 1 day | Temporary for dependent jobs |
| E2E reports/screenshots | 7 days | Debugging recent failures |
| Lighthouse reports | 7 days | Performance history |
| K6 performance reports | 30 days | Trend analysis |
| K6 baseline (summary.json) | 90 days | Long-term baselines |
| API logs (k6 failures) | 7 days | Debugging |

### Size Optimization

**Selective Uploads:**
- Screenshots: Only on failure
- Traces: Only on failure
- Full reports: Always (small HTML)

**Compression:**
- Playwright traces: Auto-compressed .zip
- Logs: Plain text (small)

**Typical Sizes:**
- Playwright report (HTML): ~5-50MB
- Screenshots (failures): ~1-30MB
- Traces (failures): ~2-20MB
- Lighthouse reports: ~1-5MB

---

## Path Filtering

### Implementation

**Tool:** `dorny/paths-filter@v3`

**Pattern:**
```yaml
- id: changes
  uses: dorny/paths-filter@v3
  with:
    filters: |
      frontend:
        - 'apps/web/**'
        - 'package.json'
        - 'pnpm-lock.yaml'
      backend:
        - 'apps/api/**'
        - 'global.json'
      infra:
        - 'infra/**'
        - 'docker-compose*.yml'
```

**Usage:**
```yaml
jobs:
  frontend-tests:
    if: steps.changes.outputs.frontend == 'true'
    steps: [...]
```

### Workflow-Specific Filters

**ci.yml:**
```yaml
frontend: ['apps/web/**', 'package.json', 'pnpm-lock.yaml']
backend: ['apps/api/**', 'global.json']
e2e: ['apps/web/**', 'apps/api/**']  # Both needed for E2E
```

**e2e-tests.yml:**
```yaml
web: ['apps/web/**']
api: ['apps/api/**']
# Skip if neither changed
```

**k6-performance.yml:**
```yaml
admin_endpoints:
  - 'apps/api/src/Api/Routing/*AdminEndpoints.cs'
  - 'apps/api/src/Api/Routing/LlmAnalyticsEndpoints.cs'
  - 'apps/api/src/Api/BoundedContexts/Administration/**'
k6_tests:
  - 'tests/k6/**'
```

**lighthouse-ci.yml & visual-regression.yml:**
```yaml
web: ['apps/web/**', '.github/workflows/lighthouse-ci.yml']
# Skip if no web changes
```

### Benefits

**Time Savings:**
- Skip backend tests on frontend-only changes (~5-8 min)
- Skip frontend tests on backend-only changes (~4-6 min)
- Skip E2E on infra-only changes (~6-8 min)

**Resource Optimization:**
- Fewer concurrent jobs
- Lower GitHub Actions minutes consumption
- Faster feedback for focused changes

**Example:**
- PR changes: `apps/web/src/app/page.tsx`
- Skipped: backend-unit-tests, backend-integration-tests, k6-performance
- Run: frontend tests, E2E (needs web), lighthouse, visual regression
- Total time: ~12-15 min (vs ~20-25 min without filtering)

---

## Troubleshooting

### Common Issues

#### 1. Testhost Process Blocking (Issue #2593)

**Symptom:** Tests hang or timeout on Windows

**Cause:** Previous `testhost.exe` process still running

**Detection:**
```bash
tasklist | grep testhost
# or PowerShell
Get-Process -Name testhost -ErrorAction SilentlyContinue
```

**Fix:**
```bash
taskkill //PID <PID> //F
# or PowerShell
Get-Process -Name testhost | Stop-Process -Force
```

**CI Mitigation:**
```yaml
- name: Kill testhost before tests
  if: runner.os == 'Windows'
  run: |
    Get-Process -Name testhost -ErrorAction SilentlyContinue | Stop-Process -Force
  shell: pwsh
```

#### 2. Port Already in Use

**Symptom:** "Address already in use: 8080" or similar

**Detection:**
```bash
# Windows
netstat -ano | findstr :8080

# Linux/macOS
lsof -i :8080
```

**Fix:**
```bash
# Windows
taskkill /PID <PID> /F

# Linux/macOS
kill -9 <PID>
```

**CI Mitigation:**
```yaml
- name: Check port availability
  run: |
    if netstat -ano | findstr :8080; then
      echo "Port 8080 in use, killing process"
      FOR /F "tokens=5" %%P IN ('netstat -ano ^| findstr :8080') DO taskkill /PID %%P /F
    fi
```

#### 3. Database Connection Failures

**Symptom:** "Connection refused" or "Could not connect to PostgreSQL"

**Health Check Verification:**
```yaml
- name: Verify Postgres health
  run: |
    docker ps --filter "name=postgres" --format "table {{.Names}}\t{{.Status}}"
    docker exec postgres-container pg_isready
```

**Common Causes:**
- Health check not ready (increase retries)
- Wrong connection string (check env vars)
- Service container not started (check `services:` config)

**Fix:**
```yaml
services:
  postgres:
    options: >-
      --health-interval 5s
      --health-timeout 5s
      --health-retries 30  # Increase from 10 if needed
```

#### 4. Qdrant Custom Health Check Failures

**Issue:** Qdrant image lacks `wget`/`curl` for standard health checks

**Solution:** TCP-based health check
```yaml
options: >-
  --health-cmd "bash -c '</dev/tcp/127.0.0.1/6333'"
  --health-interval 5s
  --health-timeout 5s
  --health-retries 30
```

**Verification:**
```bash
# Manual check
curl http://localhost:6333/health
# Should return: {"status":"ok"}
```

#### 5. Coverage Upload Failures

**Symptom:** "Error uploading to Codecov" or 401 Unauthorized

**Checks:**
1. Verify `CODECOV_TOKEN` secret exists in repo settings
2. Check coverage file exists: `ls -la apps/web/coverage/lcov.info`
3. Validate coverage format (cobertura vs lcov)

**Debugging:**
```yaml
- name: Debug coverage
  run: |
    echo "Coverage files:"
    find . -name "*.xml" -o -name "lcov.info"
    cat apps/api/coverage/unit-coverage.xml | head -20
```

**Workaround:**
```yaml
- name: Upload coverage
  uses: codecov/codecov-action@v4
  with:
    fail_ci_if_error: false  # Don't block on upload failures
    verbose: true            # Enable debug logging
```

#### 6. E2E Quality Gate False Failures

**Symptom:** Pass rate calculation incorrect or quality gate fails unexpectedly

**Root Cause:** Test result parsing errors

**Debugging:**
```yaml
- name: Debug test results
  run: |
    find all-reports -name "*.json" -exec cat {} \;
    jq '.stats' all-reports/shard-*/results.json
```

**Validation:**
```javascript
// Ensure test result JSON exists
const results = JSON.parse(fs.readFileSync('results.json'));
console.log(`Tests: ${results.stats.tests}, Passed: ${results.stats.passed}`);
```

#### 7. Lighthouse CI Build Sharing Issues

**Symptom:** "Artifact not found" when downloading shared build

**Cause:** Build job failed or artifact not uploaded

**Fix:**
```yaml
build-nextjs:
  steps:
    - name: Build Next.js
      run: pnpm build
    - name: Upload build
      if: success()  # Only upload if build succeeded
      uses: actions/upload-artifact@v4
```

**Dependent Job Validation:**
```yaml
lighthouse-performance:
  needs: build-nextjs
  steps:
    - name: Download build
      uses: actions/download-artifact@v4
      with:
        name: nextjs-build
        path: apps/web/
    - name: Verify build exists
      run: |
        if [ ! -d "apps/web/.next" ]; then
          echo "::error::Build artifact missing"
          exit 1
        fi
```

#### 8. Chromatic Token Expiry

**Symptom:** "Authentication failed" in visual-regression.yml

**Fix:**
1. Regenerate token at https://www.chromatic.com/
2. Update `CHROMATIC_PROJECT_TOKEN` secret in GitHub repo settings
3. Re-run workflow

**Validation:**
```bash
# Local test
export CHROMATIC_PROJECT_TOKEN="your-token"
pnpm chromatic:ci
```

#### 9. K6 Performance Test Flakiness

**Symptom:** Intermittent failures in k6-performance.yml

**Common Causes:**
- Resource contention (GitHub runners)
- Network latency
- Cold start delays

**Mitigation (Issue #2286):**
```yaml
# Increased retries and exponential backoff
scenarios:
  admin_polling_load:
    executor: 'ramping-vus'
    gracefulStop: '30s'  # Increased from 10s
    exec:
      retry:
        max_attempts: 60     # Increased from 30
        backoff: exponential
```

**Smoke Test Non-Blocking:**
```yaml
- name: Run smoke test
  continue-on-error: true  # Don't block on smoke failures
```

#### 10. Service Container Startup Race Conditions

**Symptom:** Tests fail with "Connection refused" despite health checks passing

**Cause:** Application starts before services fully ready

**Solution:** Additional wait after health checks
```yaml
- name: Wait for services
  run: |
    echo "Health checks passed, waiting 5s for service initialization"
    sleep 5
```

**Verification:**
```yaml
- name: Verify all services
  run: |
    curl http://localhost:6333/health  # Qdrant
    redis-cli -h localhost ping        # Redis
    pg_isready -h localhost            # Postgres
```

---

## Performance Optimization

### Best Practices

**1. Fail Fast:**
- Run lint/typecheck before expensive tests
- Use `fail-fast: false` in matrices to see all failures

**2. Caching:**
```yaml
- uses: actions/setup-node@v4
  with:
    node-version: 20
    cache: 'pnpm'  # Auto-cache node_modules

- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: 9.0
    cache: true  # Auto-cache NuGet packages
```

**3. Parallelization:**
- Use matrix strategies for independent jobs
- Share artifacts (builds) across dependent jobs
- Path filtering to skip irrelevant tests

**4. Resource Management:**
- Concurrency groups cancel superseded runs
- Retention policies prevent artifact bloat
- Conditional uploads (failures only for screenshots)

**5. Service Optimization:**
```yaml
postgres:
  options: >-
    -c max_connections=500      # Support parallel tests (Issue #2693)
    -c shared_buffers=512MB     # Faster query execution
    -c fsync=off                # CI only - unsafe for production
```

### Monitoring

**GitHub Actions Insights:**
- Workflow run duration trends
- Job-level timing breakdown
- Artifact storage usage

**Key Metrics to Track:**
- Average pipeline duration (target: <15 min)
- Pass rate trends (target: ≥95%)
- Flaky test identification (retries needed)
- Resource consumption (Actions minutes)

---

## Additional Resources

- **Test Documentation:** `docs/05-testing/README.md`
- **Performance Testing:** `docs/05-testing/performance-benchmarks.md`
- **Visual Regression:** `docs/05-testing/visual-regression.md`
- **Playwright Best Practices:** `docs/05-testing/playwright-best-practices.md`
- **Testcontainers Best Practices:** `docs/05-testing/testcontainers-best-practices.md`

---

**Last Updated:** 2026-01-23
**Maintainer:** DevOps Team
**Related Issues:** #2921, #2693, #2593, #2542, #2918, #2286, #2284

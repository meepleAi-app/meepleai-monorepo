# CI/CD Test Pipeline - Comprehensive Guide

## Overview

The MeepleAI monorepo implements a comprehensive 8-stage CI/CD test pipeline that provides quality gates across all test types: unit, integration, E2E, visual regression, and performance auditing.

### Pipeline Philosophy

- **Fail Fast**: Lint and TypeCheck run first to catch syntax/type errors early
- **Parallel Execution**: Independent stages run concurrently to minimize total build time
- **Path Filtering**: Only affected areas trigger relevant tests (frontend changes skip backend heavy tests)
- **Quality Gates**: Each stage has clear pass/fail criteria with coverage targets
- **Non-Blocking Performance**: Visual and performance tests are informational (alpha phase)

---

## Complete Pipeline Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                        MAIN CI WORKFLOW                          │
│                      (.github/workflows/ci.yml)                  │
└─────────────────────────────────────────────────────────────────┘
                              ├─────┐
                              ▼     │
                    ┌──────────────┴────────────────┐
                    │   Path Filtering Detection    │
                    │   (frontend/backend/infra)    │
                    └──────────────┬────────────────┘
                                   │
              ┌────────────────────┴────────────────────┐
              │                                         │
              ▼                                         ▼
    ┌──────────────────┐                    ┌──────────────────┐
    │  FRONTEND JOBS   │                    │  BACKEND JOBS    │
    │  (parallel)      │                    │  (sequential)    │
    └──────────────────┘                    └──────────────────┘
              │                                         │
    ┌─────────┴─────────┐               ┌──────────────┴─────────────┐
    │                   │               │                             │
    ▼                   ▼               ▼                             ▼
┌────────┐       ┌─────────┐    ┌─────────┐                  ┌────────────┐
│ STAGE 1│       │ STAGE 3 │    │ STAGE 2 │                  │  STAGE 4   │
│ Lint + │ ───>  │Frontend │    │ Backend │  ─────────────>  │  Backend   │
│ Check  │       │ Unit +  │    │  Unit   │                  │Integration │
└────────┘       │ Integ   │    │ Tests   │                  │   Tests    │
                 │(MSW)    │    └─────────┘                  └────────────┘
                 └─────────┘
                      │
                      ▼
              ┌──────────────┐
              │   STAGE 6    │
              │  E2E Tests   │
              │  (Critical)  │
              └──────────────┘
                      │
                      ▼
          ┌──────────────────────────┐
          │   E2E WORKFLOW (Full)    │
          │ (4-shard parallel exec)  │
          └──────────────────────────┘
                      │
          ┌───────────┴───────────┐
          │                       │
          ▼                       ▼
┌──────────────────┐    ┌──────────────────┐
│    STAGE 7       │    │    STAGE 8       │
│Visual Regression │    │ Performance      │
│ - Playwright     │    │ - Lighthouse CI  │
│ - Chromatic      │    │ - Core Web Vitals│
└──────────────────┘    └──────────────────┘
```

---

## Stage-by-Workflow Mapping

### Stage 1: Lint + TypeCheck (Fail Fast)
**Workflow**: `ci.yml` → `frontend` job
**Purpose**: Catch syntax and type errors before running expensive tests
**Tools**: ESLint, TypeScript Compiler
**Pass Criteria**: 0 linting errors, 0 type errors
**Runtime**: ~30-60 seconds

**Local Execution**:
```bash
cd apps/web
pnpm lint
pnpm typecheck
```

---

### Stage 2: Backend Unit Tests
**Workflow**: `ci.yml` → `backend` job
**Purpose**: Test backend business logic in isolation
**Tools**: xUnit, FluentAssertions, Moq
**Coverage Target**: >90%
**Runtime**: ~2-3 minutes

**Local Execution**:
```bash
cd apps/api
dotnet test --filter "Category=Unit"
```

---

### Stage 3: Frontend Unit Tests (includes Integration with MSW)
**Workflow**: `ci.yml` → `frontend` job
**Purpose**: Test frontend components and business logic
**Tools**: Vitest, Testing Library, MSW (Mock Service Worker)
**Coverage Target**: 39% (interim, goal: 90%)
**Runtime**: ~1-2 minutes

**MSW Integration**: MSW is configured globally in `vitest.setup.tsx` to mock API responses. Tests can simulate both unit tests (isolated components) and integration tests (components + API interactions).

**Local Execution**:
```bash
cd apps/web
pnpm test
pnpm test:coverage  # With coverage report
```

---

### Stage 4: Backend Integration Tests (Testcontainers)
**Workflow**: `ci.yml` → `backend` job
**Purpose**: Test backend with real database and external services
**Tools**: xUnit, Testcontainers (PostgreSQL, Redis, Qdrant)
**Coverage Target**: >85%
**Runtime**: ~5-7 minutes

**Service Containers**:
- PostgreSQL 16 (max_connections: 500 for parallel tests)
- Redis 7
- Qdrant v1.12.4

**Local Execution**:
```bash
cd apps/api
dotnet test --filter "Category=Integration"
```

---

### Stage 5: Frontend Integration Tests (MSW)
**Workflow**: `ci.yml` → `frontend` job
**Purpose**: Test frontend components with simulated API interactions
**Tools**: Vitest + MSW (Mock Service Worker)
**Runtime**: Included in Stage 3 execution

**Note**: Frontend doesn't have separate unit/integration commands like backend. MSW allows tests to simulate API responses without a real backend, enabling both unit and integration testing within the same Vitest execution.

**Key Pattern**:
```typescript
// MSW configured in vitest.setup.tsx
import { server } from './src/__tests__/mocks/server';
server.listen({ onUnhandledRequest: 'bypass' });

// Test can mock API responses
import { rest } from 'msw';
server.use(
  rest.get('/api/v1/games', (req, res, ctx) => {
    return res(ctx.json({ games: [...] }));
  })
);
```

---

### Stage 6: E2E Tests
**Workflow (Critical Paths)**: `ci.yml` → `e2e` job
**Workflow (Full Suite)**: `e2e-tests.yml` (4-shard parallel)
**Purpose**: Test complete user journeys across frontend + backend
**Tools**: Playwright (6 browser projects)
**Pass Criteria**: ≥90% pass rate
**Runtime**: ~8-12 minutes (full suite with 4 shards)

**Browser Coverage**:
- Desktop: Chrome, Firefox, Safari
- Mobile: Chrome, Safari
- Tablet: Chrome

**Local Execution**:
```bash
cd apps/web
pnpm test:e2e                    # All tests
pnpm test:e2e:ui                 # Interactive UI
pnpm test:e2e:shard1             # Run specific shard (1/4)
pnpm test:e2e --project=desktop-chrome  # Specific browser
```

---

### Stage 7: Visual Regression Tests
**Workflow**: `visual-regression.yml`
**Purpose**: Detect unintended UI changes
**Tools**: Playwright Visual Snapshots + Chromatic UI Review
**Pass Criteria**: Manual approval for visual changes (non-blocking in alpha)
**Runtime**: ~5-8 minutes

**Two-Phase Approach**:
1. **Playwright Visual Snapshots**: Pixel-perfect screenshot comparison
2. **Chromatic UI Review**: Component-level visual diff with approve/reject workflow

**Local Execution**:
```bash
cd apps/web
pnpm test:e2e:visual              # Playwright visual tests
pnpm test:e2e:visual:update       # Update snapshots
pnpm chromatic                    # Chromatic upload
```

**Chromatic Integration**:
- Auto-accepts changes on `main` branch
- Requires manual approval for PRs
- Detects visual changes in Storybook components

---

### Stage 8: Performance Audit (Lighthouse CI)
**Workflow**: `lighthouse-ci.yml`
**Purpose**: Measure and enforce Core Web Vitals
**Tools**: Lighthouse CI, Lighthouse Playwright Integration
**Pass Criteria**: Performance ≥85%, Accessibility ≥95%, SEO ≥90%
**Runtime**: ~10-15 minutes

**Core Web Vitals Targets**:
- **LCP** (Largest Contentful Paint): < 2.5s
- **FCP** (First Contentful Paint): < 2.0s
- **TBT** (Total Blocking Time): < 300ms
- **CLS** (Cumulative Layout Shift): < 0.1
- **Speed Index**: < 3.0s

**Regression Detection**: Fails build if any metric degrades by >10% compared to base branch

**Local Execution**:
```bash
cd apps/web
pnpm build
pnpm exec lhci autorun  # Run Lighthouse CI
```

**Configuration**: See `apps/web/lighthouserc.json` for thresholds

---

## Trigger Conditions

### Automatic Triggers

| Workflow | Trigger | Branches | Paths |
|----------|---------|----------|-------|
| `ci.yml` | push, pull_request | main, main-dev, frontend-dev | `apps/**`, `*.json`, `*.yml` |
| `e2e-tests.yml` | push, pull_request, schedule | main, main-dev, frontend-dev | `apps/**` |
| `visual-regression.yml` | push, pull_request | main, main-dev, frontend-dev | `apps/web/**` |
| `lighthouse-ci.yml` | push, pull_request | main, main-dev | `apps/web/**` |
| `security.yml` | push (main), schedule (weekly) | main | All files |
| `k6-performance.yml` | schedule (nightly 2 AM UTC) | main | Backend files |

### Manual Triggers

All workflows support `workflow_dispatch` for manual execution:

```bash
# Via GitHub UI: Actions tab → Select workflow → Run workflow
# Or via gh CLI:
gh workflow run ci.yml
gh workflow run e2e-tests.yml
gh workflow run visual-regression.yml
gh workflow run lighthouse-ci.yml
```

---

## Quality Gates & Fail Criteria

### Blocking Gates (Fail PR if not met)
- ✅ **Stage 1**: 0 lint errors, 0 type errors
- ✅ **Stage 2**: Backend unit tests pass
- ✅ **Stage 3**: Frontend unit tests pass
- ✅ **Stage 4**: Backend integration tests pass
- ✅ **Stage 6**: E2E tests ≥90% pass rate

### Non-Blocking Gates (Informational)
- ⚠️ **Stage 7**: Visual regression (manual approval required)
- ⚠️ **Stage 8**: Performance audit (monitoring only in alpha)

**Rationale**: Visual and performance tests are non-blocking during alpha phase to avoid blocking development velocity. They provide valuable feedback but don't fail PRs.

---

## Coverage Reporting

### Backend Coverage
- **Unit Tests**: Separate coverage report (`unit-coverage.xml`)
- **Integration Tests**: Separate coverage report (`integration-coverage.xml`)
- **Tool**: Coverlet (Cobertura format)
- **Upload**: Codecov with `backend` flag

### Frontend Coverage
- **Unit + Integration**: Combined coverage report (`lcov.info`)
- **E2E**: Separate coverage report via `@bgotink/playwright-coverage`
- **Tool**: Vitest (c8/Istanbul) + Playwright Coverage
- **Upload**: Codecov with `frontend` flag

### View Coverage
- **Codecov Dashboard**: https://app.codecov.io/gh/{org}/{repo}
- **Local Reports**:
  ```bash
  # Backend
  cd apps/api && open coverage/index.html

  # Frontend
  cd apps/web && open coverage/index.html

  # E2E
  cd apps/web && open coverage-e2e/html/index.html
  ```

---

## Artifact Management

### Uploaded Artifacts (Retention: 7 days)

| Artifact | Workflow | Contents | Use Case |
|----------|----------|----------|----------|
| `playwright-report-{run_number}` | ci.yml, e2e-tests.yml | HTML report, traces | Debug E2E failures |
| `visual-test-results-{run_number}` | visual-regression.yml | Visual snapshots, diffs | Review visual changes |
| `lighthouse-reports-{run_number}` | lighthouse-ci.yml | Lighthouse JSON/HTML | Performance analysis |
| `lighthouse-ci-results-{run_number}` | lighthouse-ci.yml | LHCI comparison data | Regression tracking |

### Download Artifacts
```bash
# Via GitHub UI: Actions → Workflow run → Artifacts section

# Or via gh CLI:
gh run download {run_id} -n playwright-report-{run_number}
```

---

## Performance Optimization

### Parallel Execution Strategy

**Frontend Jobs** (independent, run in parallel):
- Lint + Typecheck
- Unit + Integration Tests
- Build

**Backend Jobs** (sequential within job):
- Unit Tests → Integration Tests (share service containers)

**E2E Jobs** (4-shard parallel):
- Shard 1/4, 2/4, 3/4, 4/4 run concurrently

### Caching Strategy

**Frontend Cache**:
- pnpm store directory (`~/.local/share/pnpm/store`)
- Next.js build cache (`.next/cache`)
- Playwright browsers

**Backend Cache**:
- NuGet packages (`~/.nuget/packages`)
- Build outputs (`bin/`, `obj/`)
- Docker images (Testcontainers base images)

### Build Time Benchmarks

| Stage | Duration (Typical) | Duration (Max) |
|-------|-------------------|----------------|
| Lint + TypeCheck | 30-60s | 90s |
| Backend Unit | 2-3 min | 5 min |
| Frontend Unit | 1-2 min | 4 min |
| Backend Integration | 5-7 min | 10 min |
| E2E (Critical) | 3-5 min | 8 min |
| E2E (Full Suite) | 8-12 min | 20 min |
| Visual Regression | 5-8 min | 15 min |
| Lighthouse CI | 10-15 min | 25 min |
| **Total (CI)** | **15-20 min** | **30 min** |
| **Total (All)** | **30-45 min** | **60 min** |

---

## Troubleshooting

### Common Issues

#### 1. Testhost blocking tests (Backend)
**Symptom**: `dotnet test` hangs or tests fail with "process in use"
**Solution**:
```bash
# Kill testhost processes
tasklist | grep testhost
taskkill /PID <PID> /F

# Or use cleanup script
pwsh tools/cleanup/cleanup-test-processes.ps1 -TestHostOnly
```

#### 2. Port conflicts (E2E)
**Symptom**: "Port 3000 already in use"
**Solution**:
```bash
netstat -ano | findstr :3000
taskkill /PID <PID> /F
```

#### 3. Playwright browser installation
**Symptom**: "Executable doesn't exist"
**Solution**:
```bash
cd apps/web
pnpm exec playwright install
pnpm exec playwright install-deps  # Linux dependencies
```

#### 4. MSW fetch patching (Frontend)
**Symptom**: "Network request was not handled by any worker"
**Solution**: Ensure MSW server is started in `vitest.setup.tsx` at module level (not in `beforeAll`)

#### 5. Lighthouse CI timeout
**Symptom**: "Lighthouse run timed out"
**Solution**: Increase timeout in `lighthouserc.json` or reduce number of runs

#### 6. Visual regression false positives
**Symptom**: Snapshots differ on font rendering or minor pixel shifts
**Solution**: Update snapshots with `pnpm test:e2e:visual:update` after verifying changes are intentional

---

## Best Practices

### Writing Tests

1. **Unit Tests**: Fast, isolated, no external dependencies
   - Backend: Mock repositories, use in-memory data
   - Frontend: Mock API with MSW, use `renderWithProviders`

2. **Integration Tests**: Real dependencies, isolated per test
   - Backend: Use Testcontainers, clear DB between tests
   - Frontend: Use MSW with realistic API responses

3. **E2E Tests**: Real app, minimal mocking, test critical paths only
   - Use Page Object Model for maintainability
   - Implement retry logic for flaky network operations
   - Tag with `@smoke`, `@critical` for selective execution

4. **Visual Tests**: Consistent environment, avoid animations
   - Disable animations in test environment
   - Use `waitForLoadState('networkidle')` before snapshots
   - Approve changes intentionally, don't "accept all"

5. **Performance Tests**: Production-like build, consistent baseline
   - Always use production build for Lighthouse
   - Run on same hardware/network for comparisons
   - Monitor trends, not absolute values

### CI/CD Hygiene

1. **Keep Workflows DRY**: Use composite actions (`.github/actions/`) for repeated setup steps
2. **Fail Fast**: Run cheap quality checks (lint, typecheck) before expensive tests
3. **Path Filtering**: Skip unnecessary jobs using `paths-filter` action
4. **Artifact Retention**: 7 days for debug artifacts, longer for release artifacts
5. **Security**: Use least-privilege permissions (`contents: read`, `pull-requests: write`)
6. **Secrets**: Store in GitHub Secrets, never commit in workflows

---

## Related Documentation

- [Backend Testing Guide](./BACKEND_TESTING.md)
- [Frontend Testing Guide](./FRONTEND_TESTING.md)
- [E2E Testing Guide](./E2E_TESTING.md)
- [Performance Testing](./PERFORMANCE_TESTING.md)
- [Accessibility Testing](./ACCESSIBILITY_TESTING.md)

---

## Maintenance

### Regular Tasks

- **Weekly**: Review test execution time trends, optimize slow tests
- **Monthly**: Update test dependencies (Playwright, Lighthouse, Vitest)
- **Quarterly**: Review coverage targets, adjust thresholds
- **Release**: Validate all stages pass on release branch before production deploy

### Monitoring

- **GitHub Actions Dashboard**: Monitor success/failure rates per workflow
- **Codecov Dashboard**: Track coverage trends over time
- **Lighthouse CI Server**: Historical performance data (if configured)
- **Prometheus/Grafana**: E2E test metrics (if Prometheus reporter enabled)

---

**Last Updated**: 2026-01-22
**Maintained By**: QA Team
**Related Issue**: [#2921](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2921)

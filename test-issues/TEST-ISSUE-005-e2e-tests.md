# TEST-ISSUE-005: Execute E2E Test Suite

**Priority**: 🟢 MEDIUM
**Labels**: `medium-priority`, `testing`, `e2e`, `playwright`
**Estimated Effort**: 8-10 hours (including setup)
**Test Files**: 28 E2E specs not executed
**Current Status**: 0/28 executed (infrastructure setup required)

---

## Problem Statement

28 E2E test files exist in `apps/web/e2e/` but were not executed in the current analysis due to infrastructure requirements. E2E tests validate complete user workflows and full system integration, providing the highest confidence level for deployments.

---

## E2E Test Inventory (28 Files)

### Authentication (3 files)
- `authenticated.spec.ts` - Auth-protected pages
- `demo-user-login.spec.ts` - Demo user login flow
- OAuth flows (partial coverage in other tests)

### Chat Features (5 files)
- `chat.spec.ts` - Basic chat functionality
- `chat-animations.spec.ts` - Chat animations and transitions
- `chat-context-switching.spec.ts` - Switching between game contexts
- `chat-edit-delete.spec.ts` - Message editing and deletion
- `chat-streaming.spec.ts` - Streaming responses

### Editor (3 files)
- `editor.spec.ts` - Basic editor functionality
- `editor-advanced.spec.ts` - Advanced editor features
- `editor-rich-text.spec.ts` - Rich text editing (TipTap)

### Admin (4 files)
- `admin.spec.ts` - Admin dashboard
- `admin-analytics.spec.ts` - Analytics page
- `admin-configuration.spec.ts` - Configuration management
- `admin-users.spec.ts` - User management

### PDF Processing (3 files)
- `pdf-preview.spec.ts` - PDF preview functionality
- `pdf-processing-progress.spec.ts` - Processing progress tracking
- `pdf-upload-journey.spec.ts` - Complete PDF upload flow

### Other Features (10 files)
- `accessibility.spec.ts` - Accessibility compliance
- `ai04-qa-snippets.spec.ts` - AI Q&A with snippets
- `chess-registration.spec.ts` - Chess agent registration
- `comments-enhanced.spec.ts` - Comment system
- `error-handling.spec.ts` - Error scenarios
- `home.spec.ts` - Homepage
- `n8n.spec.ts` - n8n integration
- `session-expiration.spec.ts` - Session management
- `setup.spec.ts` - Setup wizard
- `timeline.spec.ts` - Version timeline
- `versions.spec.ts` - Version management

---

## Infrastructure Requirements

### Backend Services (Required)

1. **API Server** (Port 8080)
   ```bash
   cd apps/api/src/Api
   dotnet run
   ```

2. **PostgreSQL** (Port 5432)
   ```bash
   docker run -d --name postgres \
     -e POSTGRES_PASSWORD=password \
     -p 5432:5432 postgres:16
   ```

3. **Qdrant** (Ports 6333/6334)
   ```bash
   docker run -d --name qdrant \
     -p 6333:6333 -p 6334:6334 \
     qdrant/qdrant:latest
   ```

4. **Redis** (Port 6379)
   ```bash
   docker run -d --name redis \
     -p 6379:6379 redis:latest
   ```

### Frontend (Required)

**Next.js Dev Server** (Port 3000)
```bash
cd apps/web
pnpm dev
```

### Test Environment

1. **Playwright Browsers**
   ```bash
   cd apps/web
   pnpm playwright install
   ```

2. **Environment Variables**
   ```bash
   # apps/web/.env.test
   NEXT_PUBLIC_API_BASE=http://localhost:8080
   ```

3. **Test Database Seeding**
   ```bash
   cd apps/api/src/Api
   dotnet ef database update
   ```

---

## Implementation Tasks

### Task 1: Environment Setup (2-3 hours)

#### Subtask 1.1: Docker Compose Configuration (1 hour)

**File**: `infra/docker-compose.e2e.yml` (if not exists)

```yaml
version: '3.8'
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: password
      POSTGRES_DB: meepleai_test
    ports:
      - "5432:5432"

  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"
      - "6334:6334"

  redis:
    image: redis:latest
    ports:
      - "6379:6379"
```

#### Subtask 1.2: Start Services (30 min)

```bash
# Start infrastructure
cd infra
docker compose -f docker-compose.e2e.yml up -d

# Verify health
docker compose ps

# Start API
cd ../apps/api/src/Api
dotnet run &

# Start frontend
cd ../../web
pnpm dev &
```

#### Subtask 1.3: Seed Test Data (30 min)

```bash
# Run migrations
cd apps/api/src/Api
dotnet ef database update

# Seed demo data (if needed)
# Data should be seeded by migration: 20251009140700_SeedDemoData
```

#### Subtask 1.4: Verify Environment (30 min)

```bash
# Check API health
curl http://localhost:8080/health

# Check frontend
curl http://localhost:3000

# Check database
psql -h localhost -U postgres -d meepleai_test -c "SELECT COUNT(*) FROM users;"

# Check Qdrant
curl http://localhost:6333/healthz

# Check Redis
redis-cli ping
```

---

### Task 2: E2E Test Execution (4-5 hours)

#### Subtask 2.1: Run Test Suites by Category

**Authentication Tests** (30 min)
```bash
cd apps/web
pnpm test:e2e authenticated.spec.ts
pnpm test:e2e demo-user-login.spec.ts
```

**Chat Tests** (1 hour)
```bash
pnpm test:e2e chat.spec.ts
pnpm test:e2e chat-animations.spec.ts
pnpm test:e2e chat-context-switching.spec.ts
pnpm test:e2e chat-edit-delete.spec.ts
pnpm test:e2e chat-streaming.spec.ts
```

**Editor Tests** (30 min)
```bash
pnpm test:e2e editor.spec.ts
pnpm test:e2e editor-advanced.spec.ts
pnpm test:e2e editor-rich-text.spec.ts
```

**Admin Tests** (1 hour)
```bash
pnpm test:e2e admin.spec.ts
pnpm test:e2e admin-analytics.spec.ts
pnpm test:e2e admin-configuration.spec.ts
pnpm test:e2e admin-users.spec.ts
```

**PDF Tests** (45 min)
```bash
pnpm test:e2e pdf-preview.spec.ts
pnpm test:e2e pdf-processing-progress.spec.ts
pnpm test:e2e pdf-upload-journey.spec.ts
```

**Other Tests** (1 hour)
```bash
pnpm test:e2e accessibility.spec.ts
pnpm test:e2e ai04-qa-snippets.spec.ts
pnpm test:e2e chess-registration.spec.ts
pnpm test:e2e comments-enhanced.spec.ts
pnpm test:e2e error-handling.spec.ts
pnpm test:e2e home.spec.ts
pnpm test:e2e n8n.spec.ts
pnpm test:e2e session-expiration.spec.ts
pnpm test:e2e setup.spec.ts
pnpm test:e2e timeline.spec.ts
pnpm test:e2e versions.spec.ts
```

#### Subtask 2.2: Document Results (1 hour)

**Create**: `TEST-E2E-RESULTS-2025-11-05.md`

```markdown
# E2E Test Results - 2025-11-05

## Summary
- Total Tests: X
- Passed: Y
- Failed: Z
- Pass Rate: Y/X %

## Failed Tests
| Test | Reason | Priority |
|------|--------|----------|
| ... | ... | ... |

## Recommendations
...
```

---

### Task 3: CI Integration (2 hours)

#### Subtask 3.1: Add E2E to CI Pipeline (1 hour)

**File**: `.github/workflows/e2e.yml` (create or update existing)

```yaml
name: E2E Tests

on:
  pull_request:
  push:
    branches: [main]

jobs:
  e2e:
    runs-on: ubuntu-latest
    timeout-minutes: 30

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: password
          POSTGRES_DB: meepleai_test
        ports:
          - 5432:5432

      qdrant:
        image: qdrant/qdrant:latest
        ports:
          - 6333:6333

      redis:
        image: redis:latest
        ports:
          - 6379:6379

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install pnpm
        uses: pnpm/action-setup@v2
        with:
          version: 9

      - name: Install API dependencies
        run: dotnet restore
        working-directory: apps/api

      - name: Build API
        run: dotnet build --no-restore
        working-directory: apps/api

      - name: Run migrations
        run: dotnet ef database update
        working-directory: apps/api/src/Api
        env:
          ConnectionStrings__Postgres: "Host=localhost;Database=meepleai_test;Username=postgres;Password=password"

      - name: Start API
        run: dotnet run &
        working-directory: apps/api/src/Api
        env:
          ConnectionStrings__Postgres: "Host=localhost;Database=meepleai_test;Username=postgres;Password=password"

      - name: Install frontend dependencies
        run: pnpm install --frozen-lockfile
        working-directory: apps/web

      - name: Install Playwright
        run: pnpm playwright install --with-deps
        working-directory: apps/web

      - name: Build frontend
        run: pnpm build
        working-directory: apps/web

      - name: Start frontend
        run: pnpm start &
        working-directory: apps/web

      - name: Wait for services
        run: |
          npx wait-on http://localhost:8080/health
          npx wait-on http://localhost:3000

      - name: Run E2E tests
        run: pnpm test:e2e
        working-directory: apps/web

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: apps/web/playwright-report/
          retention-days: 30
```

#### Subtask 3.2: Configure Parallelization (30 min)

**File**: `apps/web/playwright.config.ts` (update)

```typescript
export default defineConfig({
  workers: process.env.CI ? 2 : 4,
  retries: process.env.CI ? 2 : 0,
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
});
```

#### Subtask 3.3: Configure Failure Notifications (30 min)

Add Slack/email notifications for E2E failures in CI.

---

## Acceptance Criteria

### Execution
- [ ] All 28 E2E test files executed
- [ ] Test results documented
- [ ] Pass rate ≥ 95%
- [ ] Critical user flows validated

### Infrastructure
- [ ] Docker Compose setup complete
- [ ] All services healthy
- [ ] Test data seeded correctly
- [ ] Environment reproducible

### CI Integration
- [ ] E2E tests in CI pipeline
- [ ] Parallel execution configured
- [ ] Test artifacts uploaded
- [ ] Failure notifications configured

### Documentation
- [ ] E2E results report created
- [ ] Failed tests documented with priority
- [ ] Fix recommendations provided
- [ ] README updated with E2E instructions

---

## Success Metrics

**Target**:
- E2E pass rate: ≥ 95% (27/28 tests)
- Execution time: < 15 minutes (parallel)
- Critical flows: 100% passing

**Critical User Flows** (Must Pass):
- User authentication
- PDF upload and processing
- Chat Q&A functionality
- Admin user management

---

## Common E2E Issues

### Issue 1: Flaky Tests
**Symptom**: Tests pass/fail randomly
**Solution**: Add proper waits, increase timeouts, fix race conditions

### Issue 2: Slow Execution
**Symptom**: Tests take too long
**Solution**: Parallelize, optimize, reduce unnecessary waits

### Issue 3: Environment Issues
**Symptom**: Services not ready
**Solution**: Add health checks, proper wait conditions

---

## Related Issues

- TEST-ISSUE-002: May help identify E2E failures
- Infrastructure: Requires services setup

---

**Created**: 2025-11-05
**Estimated Effort**: 8-10 hours

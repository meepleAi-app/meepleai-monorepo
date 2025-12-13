# Test Automation Pipeline Guide

**Document Version**: 1.0.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: Production Ready
**Related Issue**: #871

---

## 📋 Executive Summary

Comprehensive test automation pipeline for MeepleAI monorepo with parallel execution, test sharding, coverage gates, and notifications. Targets <10 minute total execution time with 90%+ coverage enforcement.

### Key Features

- ✅ **Parallel Execution**: Backend-unit, backend-integration, frontend-unit run concurrently
- ✅ **Test Sharding**: E2E tests split across 4 shards per browser (12 parallel jobs)
- ✅ **Coverage Gates**: 90% threshold enforcement for all test suites
- ✅ **Selective Execution**: Changed files detection for efficient CI runs
- ✅ **Multi-Browser E2E**: Chromium, Firefox, WebKit matrix testing
- ✅ **Comprehensive Reports**: Automated test summaries in GitHub Actions
- ✅ **Notifications**: Slack/Discord integration for failures
- ✅ **Performance Optimized**: Caching, parallelization, and timeout controls

---

## 🏗️ Pipeline Architecture

### Job Dependency Graph

```
detect-changes (Change Detection)
       |
       ├─────────────┬──────────────────┬────────────────>
       |             |                  |
       v             v                  v
backend-unit    backend-integration  frontend-unit
   (5 min)          (10 min)           (5 min)
       |             |                  |
       └─────────────┴──────────────────┘
                     |
                     v
              e2e-tests (Matrix: 3 browsers × 4 shards)
                  (15 min per shard, parallel)
                     |
                     v
              coverage-gate (Threshold Enforcement)
                  (5 min)
                     |
                     v
              test-report (Summary Generation)
                  (5 min)
                     |
                     v
            notify-on-failure (Slack/Discord)
                  (2 min)
```

### Execution Timeline (Parallel)

```
Time  | Job
------|------------------------------------------------------------
0:00  | detect-changes
0:30  | backend-unit, backend-integration, frontend-unit (parallel)
5:30  | e2e-tests starts (after fastest job completes)
15:30 | coverage-gate (after all E2E shards complete)
16:00 | test-report
16:30 | Pipeline Complete ✅

Total: ~16-17 minutes (optimized from ~30 minutes sequential)
```

---

## 📊 Test Suites

### 1. Backend Unit Tests

**Job**: `backend-unit-tests`
**Duration**: ~5 minutes
**Parallel**: Yes (with backend-integration, frontend-unit)

**Configuration**:
```yaml
- Filter: Category=Unit
- Parallelization: 4 threads
- Coverage: 90%+ enforced via Coverlet
- Excludes: Api.Migrations.*
```

**Run locally**:
```bash
cd apps/api
dotnet test --filter "Category=Unit" -p:CollectCoverage=true
```

### 2. Backend Integration Tests

**Job**: `backend-integration-tests`
**Duration**: ~10 minutes
**Parallel**: Yes (with backend-unit, frontend-unit)
**Services**: PostgreSQL, Qdrant, Redis (GitHub Actions services)

**Configuration**:
```yaml
- Filter: Category=Integration
- Parallelization: 2 threads
- Coverage: 90%+ enforced via Coverlet
- Testcontainers: Enabled for additional service isolation
```

**Run locally**:
```bash
cd apps/api
docker compose up -d postgres qdrant redis
dotnet test --filter "Category=Integration"
docker compose down
```

### 3. Frontend Unit Tests

**Job**: `frontend-unit-tests`
**Duration**: ~5 minutes
**Parallel**: Yes (with backend-unit, backend-integration)

**Configuration**:
```yaml
- Framework: Jest + React Testing Library
- Coverage: 90%+ enforced via jest.config.js
- Includes: Accessibility tests (jest-axe)
```

**Run locally**:
```bash
cd apps/web
pnpm test:coverage
pnpm test:a11y
```

### 4. E2E Tests

**Job**: `e2e-tests`
**Duration**: ~15 minutes per shard (parallel)
**Sequential**: Runs after unit/integration tests pass
**Matrix**: 3 browsers × 4 shards = 12 parallel jobs

**Configuration**:
```yaml
- Browsers: chromium, firefox, webkit
- Sharding: 4 shards per browser
- Framework: Playwright
- Test Distribution: Automatic via --shard flag
```

**Run locally**:
```bash
cd apps/web
pnpm build
pnpm test:e2e --project=chromium --shard=1/4
pnpm test:e2e --project=firefox --shard=1/4
pnpm test:e2e --project=webkit --shard=1/4
```

### 5. Coverage Gate

**Job**: `coverage-gate`
**Duration**: ~5 minutes
**Sequential**: Runs after all tests complete
**Purpose**: Enforce 90% coverage threshold

**Thresholds**:
- Backend Unit: ≥90%
- Backend Integration: ≥90%
- Frontend Unit: ≥90%
- E2E: Critical paths coverage (visual validation)

**Failure Handling**:
- ❌ Fails pipeline if any coverage < 90%
- 📊 Generates coverage summary in GitHub Actions
- 📈 Uploads reports to Codecov

### 6. Test Report

**Job**: `test-report`
**Duration**: ~5 minutes
**Sequential**: Runs after coverage-gate
**Purpose**: Generate comprehensive test summary

**Report Contents**:
- Test execution results per suite
- Coverage percentages
- Duration metrics
- Failed test details
- Artifact links

### 7. Notifications

**Job**: `notify-on-failure`
**Duration**: ~2 minutes
**Sequential**: Runs if any job fails
**Purpose**: Alert team via Slack/Discord

**Notification Channels**:
- Slack (via `SLACK_WEBHOOK_URL` secret)
- Discord (via `DISCORD_WEBHOOK_URL` secret)

**Notification Format**:
- ❌ Pipeline status
- 📝 Commit details
- 👤 Actor information
- 🔗 Link to failed run

---

## 🚀 Running the Pipeline

### Automatic Triggers

**On Push**:
```yaml
branches: [main, develop]
paths:
  - 'apps/web/**'
  - 'apps/api/**'
  - 'schemas/**'
  - 'infra/**'
  - '.github/workflows/**'
```

**On Pull Request**:
```yaml
branches: [main, develop]
paths: (same as push)
```

### Manual Trigger

```bash
# Via GitHub CLI
gh workflow run test-automation.yml

# Via GitHub UI
# Navigate to Actions → Test Automation Pipeline → Run workflow
```

### Selective Execution

Pipeline automatically detects changed files and skips unnecessary jobs:

```yaml
# Only API changes → Skip frontend-unit and e2e-tests
# Only Web changes → Skip backend-unit and backend-integration
# Only Schemas changes → Run minimal validation
# All changes → Run complete pipeline
```

---

## ⚙️ Configuration

### Secrets Required

| Secret | Purpose | Setup Instructions |
|--------|---------|-------------------|
| `CODECOV_TOKEN` | Coverage reporting | Get from [codecov.io](https://codecov.io) |
| `SLACK_WEBHOOK_URL` | Slack notifications | Create in Slack App settings |
| `DISCORD_WEBHOOK_URL` | Discord notifications | Create in Discord server settings |

### Environment Variables

```yaml
DOTNET_VERSION: '9.0.x'
NODE_VERSION: '20'
PNPM_VERSION: '9'
```

### Timeout Limits

| Job | Timeout | Rationale |
|-----|---------|-----------|
| backend-unit-tests | 5 min | Fast unit tests |
| backend-integration-tests | 10 min | Database operations |
| frontend-unit-tests | 5 min | Fast unit tests |
| e2e-tests | 15 min | Browser automation |
| coverage-gate | 5 min | Artifact processing |
| test-report | 5 min | Report generation |
| notify-on-failure | 2 min | Webhook calls |

---

## 📈 Performance Optimization

### Caching Strategy

**NuGet Packages** (Backend):
{% raw %}
```yaml
key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}
restore-keys:
  - ${{ runner.os }}-nuget-
```
{% endraw %}

**pnpm Dependencies** (Frontend):
```yaml
cache: pnpm
cache-dependency-path: apps/web/pnpm-lock.yaml
```

**Playwright Browsers**:
```yaml
# Cached automatically by Playwright install
```

### Parallelization

**Backend Tests**:
```yaml
# Unit tests: 4 parallel threads
-- xUnit.Parallelization.MaxParallelThreads=4

# Integration tests: 2 parallel threads (database contention)
-- xUnit.Parallelization.MaxParallelThreads=2
```

**E2E Tests**:
```yaml
# 4 shards per browser × 3 browsers = 12 parallel jobs
--shard=${{ matrix.shard }}/4
```

### Test Sharding Benefits

**Without Sharding** (Sequential):
- Chromium: 15 min
- Firefox: 15 min
- WebKit: 15 min
- **Total**: 45 minutes

**With Sharding** (Parallel 4 shards × 3 browsers):
- Each shard: ~4 min
- All shards: ~4 min (parallel execution)
- **Total**: ~4 minutes per browser, 12 minutes total for all browsers

**Time Savings**: 45 min → 12 min (73% reduction)

---

## 🧪 Coverage Enforcement

### Backend Coverage

**Enforcement Point**: `dotnet test` with Coverlet

```yaml
-p:CollectCoverage=true
-p:CoverletOutputFormat=cobertura
-p:Threshold=90
-p:ThresholdType=line
-p:Exclude="[*]Api.Migrations.*"
```

**Behavior**:
- ✅ Coverage ≥90%: Test passes, continues pipeline
- ❌ Coverage <90%: Test fails, pipeline stops

### Frontend Coverage

**Enforcement Point**: `jest.config.js`

```javascript
coverageThreshold: {
  global: {
    branches: 90,
    functions: 90,
    lines: 90,
    statements: 90
  }
}
```

**Behavior**:
- ✅ Coverage ≥90%: Test passes, continues pipeline
- ❌ Coverage <90%: Test fails, pipeline stops

### Codecov Integration

**Configuration**: `.codecov.yml`

```yaml
coverage:
  status:
    project:
      default:
        target: 90%
        threshold: 1%
```

**Reports**:
- 📊 PR comments with coverage diff
- 📈 Historical coverage trends
- 🎯 File-level coverage details
- 🚨 Coverage decrease alerts

---

## 📝 Test Reports

### GitHub Actions Summary

**Location**: Actions run → Summary tab

**Contents**:
- Test execution results table
- Coverage percentages
- Duration metrics
- Pipeline performance summary

**Example**:
```markdown
## 🧪 Test Automation Pipeline - Summary Report

**Date:** 2025-11-14 15:30:00 UTC
**Commit:** a1b2c3d
**Branch:** feature/test-automation
**Actor:** @developer

### Test Execution Results

| Test Suite | Status | Duration | Coverage |
|------------|--------|----------|----------|
| 🔧 Backend Unit | ✅ Passed | ~3 min | 92% |
| 🔗 Backend Integration | ✅ Passed | ~8 min | 91% |
| ⚛️ Frontend Unit | ✅ Passed | ~4 min | 90.5% |
| 🌐 E2E Tests (3 browsers × 4 shards) | ✅ Passed | ~12 min | Critical Paths |
| 🎯 Coverage Gate | ✅ Passed | <1 min | 90% Enforced |

### ✅ All Tests Passed!
```

### Artifacts

**Uploaded Artifacts** (30 day retention):
- `backend-unit-test-results`: xUnit TRX reports
- `backend-integration-test-results`: xUnit TRX reports + coverage
- `frontend-unit-test-results`: Jest coverage reports
- `e2e-test-results-{browser}-shard-{n}`: Playwright results
- `playwright-report-{browser}-shard-{n}`: Playwright HTML reports (on failure)
- `test-automation-report-{run-number}`: Consolidated report

**Access**:
```bash
# Via GitHub CLI
gh run download <run-id> -n backend-unit-test-results

# Via GitHub UI
# Actions → Run → Artifacts section → Download
```

---

## 🔔 Notifications

### Slack Integration

**Setup**:
1. Create Slack App: https://api.slack.com/apps
2. Enable Incoming Webhooks
3. Add webhook URL to repository secrets as `SLACK_WEBHOOK_URL`

**Notification Format**:
```json
{
  "text": "❌ Test Automation Pipeline Failed",
  "blocks": [
    {
      "type": "header",
      "text": {
        "type": "plain_text",
        "text": "❌ Test Automation Pipeline Failed"
      }
    },
    {
      "type": "section",
      "fields": [
        {"type": "mrkdwn", "text": "*Repository:*\nmeepleai-monorepo"},
        {"type": "mrkdwn", "text": "*Branch:*\nfeature-branch"},
        {"type": "mrkdwn", "text": "*Commit:*\na1b2c3d"},
        {"type": "mrkdwn", "text": "*Actor:*\n@developer"}
      ]
    },
    {
      "type": "section",
      "text": {
        "type": "mrkdwn",
        "text": "<https://github.com/.../actions/runs/...|View Pipeline Details>"
      }
    }
  ]
}
```

### Discord Integration

**Setup**:
1. Navigate to Discord Server Settings → Integrations → Webhooks
2. Create New Webhook
3. Copy webhook URL
4. Add to repository secrets as `DISCORD_WEBHOOK_URL`

**Notification Format**:
```json
{
  "content": "❌ **Test Automation Pipeline Failed**",
  "embeds": [{
    "title": "Pipeline Failure",
    "description": "Test automation pipeline failed for `feature-branch`",
    "color": 15158332,
    "fields": [
      {"name": "Repository", "value": "meepleai-monorepo", "inline": true},
      {"name": "Branch", "value": "feature-branch", "inline": true},
      {"name": "Actor", "value": "@developer", "inline": true},
      {"name": "Commit", "value": "[`a1b2c3d`](https://...)", "inline": true}
    ],
    "url": "https://github.com/.../actions/runs/..."
  }]
}
```

---

## 🐛 Troubleshooting

### Common Issues

#### 1. "Coverage below 90% threshold"

**Symptoms**:
- `dotnet test` or `pnpm test:coverage` fails
- Error message: "Coverage X% is below 90% threshold"

**Resolution**:
```bash
# Check current coverage
cd apps/api && dotnet test -p:CollectCoverage=true
cd apps/web && pnpm test:coverage

# Add tests for uncovered code
# View coverage report:
cd apps/api && reportgenerator -reports:coverage/*.xml -targetdir:coverage-report
cd apps/web && open coverage/lcov-report/index.html

# Re-run tests
dotnet test
pnpm test
```

#### 2. "E2E test timeout"

**Symptoms**:
- Playwright tests hang
- Error: "Test timeout of 15000ms exceeded"

**Resolution**:
```bash
# Run with increased timeout locally
pnpm test:e2e --timeout=30000

# Debug specific test
pnpm test:e2e --debug tests/e2e/specific.spec.ts

# Check for network issues
curl http://localhost:8080/health
```

#### 3. "Qdrant health check failed"

**Symptoms**:
- Backend integration tests fail immediately
- Error: "Qdrant not ready after 20 attempts"

**Resolution**:
```bash
# Verify Qdrant is running locally
curl http://localhost:6333/healthz

# Restart Qdrant
docker compose restart qdrant

# Check Qdrant logs
docker compose logs qdrant
```

#### 4. "Artifact upload failed"

**Symptoms**:
- Test results missing in pipeline
- Warning: "No files were found with the provided path"

**Resolution**:
```yaml
# Verify artifact path exists
- name: Debug artifact path
  run: ls -R apps/api/test-results || echo "No results found"

# Ensure tests generate results
dotnet test --logger "trx" --results-directory ./test-results
```

#### 5. "Codecov upload timeout"

**Symptoms**:
- Coverage reports not appearing in Codecov
- Warning: "Codecov upload timed out"

**Resolution**:
```yaml
# Increase timeout
- name: Upload coverage to Codecov
  uses: codecov/codecov-action@v4
  timeout-minutes: 5  # Add explicit timeout
  with:
    files: apps/api/coverage/coverage.cobertura.xml
    fail_ci_if_error: false  # Don't fail pipeline
```

---

## 📚 Best Practices

### 1. Keep Tests Fast

**Target Times**:
- Unit tests: <10ms per test
- Integration tests: <1s per test
- E2E tests: <30s per test

**Strategies**:
- Mock external dependencies in unit tests
- Use Testcontainers for integration tests
- Minimize E2E test scope (critical paths only)

### 2. Write Deterministic Tests

**Avoid**:
- Hard-coded dates: `Assert.Equal(DateTime.Now, result.CreatedAt)`
- Random data: `var random = new Random(); var value = random.Next()`
- External API calls: `var data = await externalApi.GetData()`

**Use**:
- Relative dates: `result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1))`
- Seeded random: `var random = new Random(42); var value = random.Next()`
- Mocked APIs: `_mockApi.Setup(x => x.GetData()).ReturnsAsync(mockData)`

### 3. Maintain Test Independence

**Bad**:
```csharp
public class UserTests
{
    private static Guid _userId;

    [Fact, Order(1)]
    public void CreateUser() { _userId = ...; }

    [Fact, Order(2)]
    public void UpdateUser() { /* uses _userId */ }
}
```

**Good**:
```csharp
public class UserTests
{
    [Fact]
    public void UpdateUser()
    {
        // Arrange: Create user in this test
        var userId = CreateTestUser();

        // Act & Assert
        UpdateUser(userId);
    }
}
```

### 4. Use Descriptive Test Names

**Bad**:
```typescript
it('works', () => { });
it('test1', () => { });
```

**Good**:
```typescript
it('renders button text when not loading', () => { });
it('disables button when isLoading is true', () => { });
```

### 5. Add Coverage for Edge Cases

**Example**:
```typescript
describe('Pagination', () => {
  it('handles first page correctly', () => { });
  it('handles middle page correctly', () => { });
  it('handles last page correctly', () => { });
  it('handles empty results', () => { });
  it('handles single result', () => { });
  it('handles invalid page number', () => { });
});
```

---

## 📖 Additional Resources

### Internal Documentation

- [Test Writing Guide](./test-writing-guide.md): How to write your first test
- [Testing Strategy](./board-game-ai-testing-strategy.md): Overall testing philosophy
- [Test Automation Strategy 2025](./automation-strategy-2025.md): Industry best practices

### External Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [xUnit Documentation](https://xunit.net/)
- [Jest Documentation](https://jestjs.io/)
- [Playwright Documentation](https://playwright.dev/)
- [Codecov Documentation](https://docs.codecov.com/)

### Related Issues

- Issue #871: GitHub Actions - Test Automation Pipeline
- Issue #829: xUnit v3 + ExecuteDeleteAsync Performance
- Issue #1089: Test Coverage Improvements

---

## 🔄 Maintenance

### Weekly Tasks

- Review failed pipeline runs
- Update flaky test list
- Monitor pipeline execution times
- Check coverage trends in Codecov

### Monthly Tasks

- Update dependencies (Playwright, xUnit, Jest)
- Review and optimize slow tests
- Update test sharding configuration if needed
- Audit notification channels for relevance

### Quarterly Tasks

- Review pipeline architecture for improvements
- Evaluate new testing tools and frameworks
- Update this documentation with lessons learned
- Conduct test strategy retrospective with team

---

**Document Status**: ✅ Complete and Production Ready
**Next Review Date**: 2025-02-14
**Maintainer**: QA & DevOps Team


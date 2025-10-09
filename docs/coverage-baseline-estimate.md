# Code Coverage Baseline Estimate

**Date**: 2025-10-09
**Status**: Estimated (awaiting actual measurement)

## Current Test Suite Statistics

### Backend (API)

**Total Tests**: 313 tests across 39 test files

**Source Code Breakdown**:
- Services: 28 files
- Models: 8 files
- Infrastructure: 17 files (excluding migrations)
- **Total**: 54 source files

**Test-to-Source Ratio**: 5.8:1 (313 tests for 54 files)

### Test Categories

Based on test file analysis:

| Category | Estimated Tests | Coverage Type |
|----------|----------------|---------------|
| Service Tests | ~200 | Unit tests with mocks |
| Integration Tests | ~50 | Testcontainers (PostgreSQL, Qdrant) |
| Endpoint Tests | ~40 | API integration tests |
| Authorization Tests | ~15 | Security tests |
| Other | ~8 | Misc tests |

### Services with Test Coverage

Services identified in test suite:
- ✅ `AgentFeedbackService`
- ✅ `AiRequestLogService`
- ✅ `AiResponseCacheService`
- ✅ `AuthService`
- ✅ `BackgroundTaskService`
- ✅ `GameService`
- ✅ `N8nConfigService`
- ✅ `PdfStorageService`
- ✅ `QdrantService`
- ✅ `RagService`
- ✅ `RuleSpecDiffService`
- ✅ `RuleSpecService`
- ✅ And more...

## Estimated Coverage

Based on test-to-source ratio and comprehensive test suite:

| Metric | Estimated Range | Confidence |
|--------|----------------|------------|
| **Line Coverage** | 75-85% | Medium |
| **Branch Coverage** | 70-80% | Medium |
| **Method Coverage** | 80-90% | High |

**Reasoning**:
- High test-to-source ratio (5.8:1) suggests good coverage
- Comprehensive service layer testing
- Integration tests for critical paths
- Endpoint coverage for API layer

**Areas Likely Well-Covered**:
- Service business logic (Mocked dependencies)
- Authentication and authorization
- API endpoints
- Data validation

**Areas Potentially Under-Covered**:
- Infrastructure layer (entities, DbContext)
- Program.cs startup configuration
- Middleware components
- Error handling edge cases

## Frontend (Web)

**Test Framework**: Jest with 90% threshold enforced

**Coverage Status**: ✅ **90% minimum enforced**

Jest configuration requires:
- 90% branch coverage
- 90% function coverage
- 90% line coverage
- 90% statement coverage

**Estimated Status**: Meeting or exceeding 90% threshold (enforced by CI)

## Next Steps to Get Actual Baseline

### Option 1: Add Coverage to CI (Recommended)

Modify `.github/workflows/ci.yml`:

```yaml
# In ci-api job, replace test step:
- name: Test with Coverage
  run: |
    dotnet test \
      -p:CollectCoverage=true \
      -p:CoverletOutputFormat=lcov \
      -p:CoverletOutput=./tests/Api.Tests/coverage/
  working-directory: apps/api

- name: Upload Coverage to Codecov
  uses: codecov/codecov-action@v4
  with:
    files: apps/api/tests/Api.Tests/coverage/coverage.info
    flags: api
    token: ${{ secrets.CODECOV_TOKEN }}
```

**Benefits**:
- Automatic coverage on every PR
- Historical tracking
- No local performance issues
- Coverage badge for README

### Option 2: Run Locally (Slow)

```bash
# This will take 10-15+ minutes due to Testcontainers
pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml
```

**Note**: Local execution is impractical for regular use due to Testcontainers overhead.

### Option 3: Use Test Filters (Partial)

Get coverage for unit tests only (faster, but incomplete):

```bash
cd apps/api
dotnet test \
  --filter "FullyQualifiedName!~IntegrationTests" \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura
```

This gives service layer coverage but misses integration test coverage.

## Recommendation

**Immediate Action**: Implement Option 1 (CI Coverage)

1. Add Codecov to GitHub Actions
2. Get free Codecov account at https://codecov.io
3. Add `CODECOV_TOKEN` to repository secrets
4. Modify workflow as shown above
5. Merge PR to trigger first coverage report
6. Add coverage badge to README:
   ```markdown
   [![codecov](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo/branch/main/graph/badge.svg)](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo)
   ```

**Timeline**:
- Setup: 15-30 minutes
- First report: After next PR merge
- Ongoing: Automatic on every PR

## Coverage Goals

Based on estimated baseline and industry standards:

### Short Term (1-2 months)
- ✅ Document coverage process (completed)
- 🔲 Establish actual baseline via CI
- 🔲 Set initial threshold at 75% (line coverage)
- 🔲 Add coverage badge to README

### Medium Term (3-6 months)
- 🔲 Increase threshold to 80%
- 🔲 Identify and test uncovered critical paths
- 🔲 Add coverage requirements to PR checklist
- 🔲 Review coverage trends monthly

### Long Term (6-12 months)
- 🔲 Achieve 85%+ line coverage
- 🔲 Maintain 80%+ branch coverage
- 🔲 Automate coverage regression prevention
- 🔲 Document all intentional exclusions

## Appendix: Test Execution Times

Local test execution times on Windows with Testcontainers:

| Test Suite | Estimated Time | Notes |
|------------|---------------|-------|
| All tests | 10-15 minutes | Testcontainers initialization |
| Unit tests only | 2-3 minutes | Excludes integration tests |
| Single test class | 30-60 seconds | Using `--filter` |
| CI (Linux) | 3-5 minutes | Faster Docker on Linux |

**Recommendation**: Use CI for comprehensive coverage, local filters for development.

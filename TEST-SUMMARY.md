# Test Implementation Summary

_Last updated: 2025-10-12 (metrics pending fresh coverage run)_

## Latest Execution Results

| Suite | Command | Outcome | Key Notes |
|-------|---------|---------|-----------|
| Backend (`apps/api`) | `dotnet test --collect:"XPlat Code Coverage"` | **Failed:** 192 passed / 8 failed (200 total) | Qdrant Testcontainers cannot start without a Docker daemon; coverage artifacts still produced. Rerun required to ingest the new unit tests for `AiRequestLogService`, `GameService`, and `LlmService`. |
| Frontend (`apps/web`) | `npm run test -- --coverage` | **Failed:** 1 suite, 2 tests passed, coverage thresholds unmet | Jest exits with code 1 because global 90% thresholds are not satisfied; coverage reports are generated. Re-execute to capture the new suites covering `chat`, `editor`, `logs`, and the expanded `upload` flows. |

## Coverage Totals

| Suite | Statements | Branches | Functions | Lines | Coverage Source |
|-------|------------|----------|-----------|-------|-----------------|
| Backend (`apps/api`) | — | 39.20% | — | 38.00% | `apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml` (needs refresh after new suites) |
| Frontend (`apps/web`) | 14.22% | 15.06% | 11.49% | 14.13% | `apps/web/coverage/lcov.info` (stale; rerun to include new page suites) |

> **Note:** Backend Cobertura reports do not expose statement/function aggregates; lines and branches are the comparable metrics available.

## Untested / Low-Tested Areas

### Backend
- **Docker-only integration paths**: `QdrantServiceIntegrationTests` require Docker; without it, collection creation, vector indexing, and teardown paths are unverified.
- **Refresh Cobertura snapshot**: Confirm the effect of the newly added unit suites for `AiRequestLogService`, `GameService`, and `LlmService` and track any state-machine helpers that remain uncovered.
- **Low coverage utilities**: `PdfTableExtractionService` (~2.9% lines in the previous run) and parts of `QdrantService` (~11.9% lines) still lack meaningful assertions.

### Frontend
- **Next.js pages**: `admin.tsx`, `index.tsx`, `n8n.tsx`, and `versions.tsx` are still missing dedicated coverage.
- **API routes**: `pages/api/health.ts` remains uncovered.
- **Upload workflow**: `upload.tsx` now has expanded tests but still misses multi-step error states; keep iterating to meet thresholds.

## Tooling & Reproduction Checklist

1. **Install prerequisites**
   - .NET 8 SDK (use `https://dot.net/v1/dotnet-install.sh --version 8.0.100` if the CLI is unavailable).
   - Docker Desktop or another Docker daemon to satisfy Testcontainers.
   - Node.js 20.x + npm (CI uses Node 20).

2. **Run backend tests with coverage**
   ```bash
   cd apps/api
   dotnet test --collect:"XPlat Code Coverage"
   # Coverage: apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml
   ```

3. **Run frontend tests with coverage**
   ```bash
   cd apps/web
   npm install
   npm run test -- --coverage   # or npm run test:coverage
   # Coverage: apps/web/coverage/lcov.info + coverage/lcov-report/
   ```

4. **Optional reports**
   - Install `dotnet reportgenerator` to create HTML dashboards from Cobertura output.
   - Open `apps/web/coverage/lcov-report/index.html` in a browser for the Jest HTML report.

## Next Focus Areas

- Restore a working Docker environment before backend test execution to unblock integration tests.
- Expand frontend component tests beyond the upload page to raise coverage above the configured 90% thresholds.
- Target backend services currently at 0% coverage to improve regression detection for production features.

# Test Implementation Summary

_Last updated: 2025-10-05_

## Latest Execution Results

| Suite | Command | Outcome | Key Notes |
|-------|---------|---------|-----------|
| Backend (`apps/api`) | `dotnet test --collect:"XPlat Code Coverage"` | **Failed:** 192 passed / 8 failed (200 total) | Qdrant Testcontainers cannot start without a Docker daemon; coverage artifacts still produced. |
| Frontend (`apps/web`) | `npm run test -- --coverage` | **Failed:** 1 suite, 2 tests passed, coverage thresholds unmet | Jest exits with code 1 because global 90% thresholds are not satisfied; coverage reports are generated. |

## Coverage Totals

| Suite | Statements | Branches | Functions | Lines | Coverage Source |
|-------|------------|----------|-----------|-------|-----------------|
| Backend (`apps/api`) | — | 39.20% | — | 38.00% | `apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml` |
| Frontend (`apps/web`) | 14.22% | 15.06% | 11.49% | 14.13% | `apps/web/coverage/lcov.info` |

> **Note:** Backend Cobertura reports do not expose statement/function aggregates; lines and branches are the comparable metrics available.

## Untested / Low-Tested Areas

### Backend
- **Docker-only integration paths**: `QdrantServiceIntegrationTests` require Docker; without it, collection creation, vector indexing, and teardown paths are unverified.
- **Zero-coverage services**: `AiRequestLogService`, `GameService`, `LlmService`, and their async state machine helpers (55 classes) remain at 0% line coverage.
- **Low coverage utilities**: `PdfTableExtractionService` (~2.9% lines) and parts of `QdrantService` (~11.9% lines) lack meaningful assertions.

### Frontend
- **Next.js pages**: `admin.tsx`, `chat.tsx`, `editor.tsx`, `index.tsx`, `logs.tsx`, `n8n.tsx`, and `versions.tsx` report 0% coverage.
- **API routes**: `pages/api/health.ts` is uncovered.
- **Upload workflow**: `upload.tsx` retains large untested sections (wizard states, error cases) despite partial coverage from `src/pages/__tests__/upload.test.tsx`.

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

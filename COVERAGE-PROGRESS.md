# Coverage Progress Report

## 📊 Snapshot — 2025-10-12 (stale)

- **Backend (`apps/api`)**: Last recorded numbers remain at 38.00% line coverage and 39.20% branch coverage (`dotnet test --collect:"XPlat Code Coverage"`, 200 tests executed — 192 passed, 8 failed because Qdrant Testcontainers could not reach a Docker daemon). New unit suites for `AiRequestLogService`, `GameService`, and `LlmService` have landed since this run, so an updated measurement is required to reflect their impact.
- **Frontend (`apps/web`)**: Previously captured at 14.22% statements / 15.06% branches / 11.49% functions / 14.13% lines from `npm run test -- --coverage` (1 suite, 2 tests executed with Jest exiting non-zero due to the 90% global threshold). Dedicated coverage suites now exist for the `chat`, `editor`, `logs`, and `upload` pages; rerun coverage to surface the new totals.

## 🧪 Latest Commands Executed

- `dotnet test --collect:"XPlat Code Coverage"` (inside `apps/api`) — attempted 2025-10-05, the run hung after executing the integration suites; see `docs/coverage-logs/2025-10-05-dotnet-test.log`.
- `dotnet test /p:CollectCoverage=true ...` (inside `apps/api`) — coverlet fallback attempted, but long-running suites prevented completion in the sandbox.
- `npm run test -- --runTestsByPath src/pages/api/__tests__/health.test.ts` (inside `apps/web`) — new smoke test added, full coverage run pending once the backend suite is stabilised.

## 🔍 Coverage Gaps & Untested Areas

### Backend focus areas

- **Docker-dependent integration tests**: `QdrantServiceIntegrationTests` still require a reachable Docker daemon; without it the suite fails early and the Qdrant collection helpers remain unverified.
- **Refresh Cobertura snapshot**: Re-run coverage to capture the new unit tests for `AiRequestLogService`, `GameService`, and `LlmService`, and confirm whether any remaining generated state-machine types stay uncovered.
- **Low coverage utilities**: `PdfTableExtractionService` (~2.9% lines in the previous report) and asynchronous helpers in `QdrantService` (~11.9% lines) remain effectively untested until new coverage is recorded.

### Frontend focus areas

- **Pages without tests**: `src/pages/admin.tsx`, `index.tsx`, `n8n.tsx`, and `versions.tsx` remain without suites; add coverage to bring them above the 90% Jest threshold.
- **API routes**: `src/pages/api/health.ts` now has a dedicated smoke test; expand coverage to the remaining `/api/*` routes when the Jest global threshold can be satisfied.
- **Upload workflow**: Even with the refreshed tests, `src/pages/upload.tsx` still has uncovered multi-step flows and error branches—target these when updating the suite.

## 📂 Coverage Artifacts

- Backend Cobertura report: `apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml`
- Frontend LCOV report: `apps/web/coverage/lcov.info` (HTML report available at `apps/web/coverage/lcov-report/index.html`)

## ✅ Suggested Next Steps

1. Enable Docker (or point `QDRANT_URL` at a running instance) before executing backend integration tests so the Qdrant suite passes and records meaningful coverage.
2. Add component tests for the untested Next.js pages and API routes to raise frontend coverage above the global threshold.
3. Prioritise automated tests for `AiRequestLogService`, `GameService`, `LlmService`, and low-coverage utilities such as `PdfTableExtractionService` to close backend gaps.

# Coverage Progress Report

## 📊 Snapshot — 2025-10-05

- **Backend (`apps/api`)**: 38.00% line coverage and 39.20% branch coverage from the latest `dotnet test --collect:"XPlat Code Coverage"` run (200 tests executed — 192 passed, 8 failed because Qdrant Testcontainers could not reach a Docker daemon).
- **Frontend (`apps/web`)**: 14.22% statements / 15.06% branches / 11.49% functions / 14.13% lines from `npm run test -- --coverage` (1 suite, 2 tests executed; Jest exits non-zero because the project-wide 90% thresholds are not met).

## 🧪 Latest Commands Executed

- `dotnet test --collect:"XPlat Code Coverage"` (inside `apps/api`)
- `npm run test -- --coverage` (inside `apps/web`)

## 🔍 Coverage Gaps & Untested Areas

### Backend focus areas

- **Docker-dependent integration tests**: `QdrantServiceIntegrationTests` rely on local Docker; without it the suite fails early and the Qdrant collection helpers remain unverified.
- **Zero-coverage services**: `AiRequestLogService`, `GameService`, `LlmService` and their generated state-machine types all report 0% line coverage in the Cobertura report (55 classes total).
- **Low coverage utilities**: `PdfTableExtractionService` (~2.9% lines) and asynchronous helpers in `QdrantService` (~11.9% lines) are still effectively untested.

### Frontend focus areas

- **Pages without tests**: `src/pages/admin.tsx`, `chat.tsx`, `editor.tsx`, `index.tsx`, `logs.tsx`, `n8n.tsx`, and `versions.tsx` each remain at 0% across all metrics.
- **API routes**: `src/pages/api/health.ts` is generated in coverage but has no dedicated test, leaving line coverage at 0%.
- **Upload workflow**: `src/pages/upload.tsx` receives partial coverage (~48% lines) from the existing tests; multi-step flows and error branches remain uncovered.

## 📂 Coverage Artifacts

- Backend Cobertura report: `apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml`
- Frontend LCOV report: `apps/web/coverage/lcov.info` (HTML report available at `apps/web/coverage/lcov-report/index.html`)

## ✅ Suggested Next Steps

1. Enable Docker (or point `QDRANT_URL` at a running instance) before executing backend integration tests so the Qdrant suite passes and records meaningful coverage.
2. Add component tests for the untested Next.js pages and API routes to raise frontend coverage above the global threshold.
3. Prioritise automated tests for `AiRequestLogService`, `GameService`, `LlmService`, and low-coverage utilities such as `PdfTableExtractionService` to close backend gaps.

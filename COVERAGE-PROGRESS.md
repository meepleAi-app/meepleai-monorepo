# Coverage Progress Report

## 📊 Snapshot — 2025-10-06

- **Backend (`apps/api`)**: `dotnet test --collect:"XPlat Code Coverage"` failed during compilation, so no new Cobertura totals were generated. The build stopped at `tests/Api.Tests/PdfStorageServiceTests.cs` (mock signature mismatches) and `tests/Api.Tests/QdrantIntegrationTestBase.cs` (`SkipException` constructor mismatch), leaving both line and branch coverage unchanged (previous snapshot: 38.00% lines / 39.20% branches).
- **Frontend (`apps/web`)**: `npm run test -- --coverage` completed with coverage at **79.33% statements / 73.22% branches / 80.47% functions / 79.54% lines**. Jest exited non-zero because the configured 90% global thresholds were not met, even though all 10 suites (48 tests) passed.

## 🧪 Latest Commands Executed

- `dotnet test --collect:"XPlat Code Coverage"` (inside `apps/api`, aborted with compile errors)
- `npm run test -- --coverage` (inside `apps/web`, finished with threshold failure)

## 🔍 Coverage Gaps & Follow-up Items

### Backend

- **Fix compilation issues first**: Resolve the constructor/type mismatches in `PdfStorageServiceTests` and `QdrantIntegrationTestBase` so the suite can build and emit a Cobertura report.
- **Service wiring coverage**: Once the build succeeds, confirm the mocked dependencies for `PdfStorageService` and Redis/Qdrant integrations receive coverage to validate their registration logic.
- **External dependencies**: Re-verify Qdrant/Docker availability before rerunning to avoid future integration skips.

### Frontend

- **Raise coverage on `upload.tsx`**: Statements (65.01%) and functions (47.91%) are dragging global coverage under the threshold; expand tests around the multi-step upload flow and error branches.
- **Target branch-heavy pages**: `admin.tsx` (68.33% branches) and `n8n.tsx` (64.63% branches) require additional scenarios to meet the 90% bar.
- **Silence expected console noise**: Consider mocking `console.error` in tests where failures are intentional to keep output clean.

## 📂 Coverage Artifacts

- Backend Cobertura report: not generated (build failed before coverage instrumentation). Expected path once fixed: `apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml`.
- Frontend LCOV report: `apps/web/coverage/lcov.info` (HTML at `apps/web/coverage/lcov-report/index.html`).

## ✅ Suggested Next Steps

1. Restore backend test compilation by aligning mock constructor signatures and updating `SkipException` usage, then rerun the coverage collection.
2. Improve frontend coverage for `upload.tsx`, `admin.tsx`, and `n8n.tsx` to satisfy Jest's 90% global thresholds.
3. Re-run both coverage commands after the above fixes and append the fresh Cobertura/LCOV artifact locations to this log.

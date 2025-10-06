# Coverage Progress Report

## 📊 Snapshot — 2025-01-06

- **Backend (`apps/api`)**: `dotnet build` completes successfully with **0 errors, 0 warnings**. However, `dotnet test` hangs during integration test execution (likely Docker/Testcontainers-related), preventing coverage collection. Build is healthy; test suite requires Docker environment to complete.
- **Frontend (`apps/web`)**: `npm run test -- --coverage` completed with coverage at **79.45% statements / 73.77% branches / 80.58% functions / 79.61% lines** (10 suites, 51 tests: **all passed**). Jest fails on 90% global thresholds but test suite is healthy.

## 🧪 Latest Commands Executed

- `dotnet build` (inside `apps/api`, ✅ successful)
- `dotnet test --no-build` (inside `apps/api`, ⏱️ timeout after 2 minutes)
- `npm run test -- --coverage` (inside `apps/web`, ⚠️ threshold failures but tests pass)

## 🔍 Coverage Gaps & Follow-up Items

### Backend

- **Integration test hang**: Tests hang during execution, likely at Docker-dependent integration tests (`QdrantServiceIntegrationTests`). Requires Docker environment to complete.
- **Coverage collection blocked**: Cannot generate Cobertura reports until test suite completes. Consider running tests with Docker available or skipping integration tests for local coverage runs.
- **Next steps**: Set up Docker Desktop, or use `dotnet test --filter` to exclude integration tests and collect coverage for unit tests only.

### Frontend

- ✅ **Fixed failing test**: AdminDashboard test now passes (issue #253 resolved - `NEXT_PUBLIC_API_BASE` was not set before loading component in tests).
- **Raise coverage on `upload.tsx`**: Statements (65.01%) and functions (47.91%) are dragging global coverage under the threshold; expand tests around the multi-step upload flow and error branches.
- **Target branch-heavy pages**: Continue improving `admin.tsx` and `n8n.tsx` branch coverage to reach the 90% bar.

## 📂 Coverage Artifacts

- Backend Cobertura report: **not generated** (tests hang before completion). Expected path once Docker environment is available: `apps/api/tests/Api.Tests/TestResults/<timestamp>/coverage.cobertura.xml`.
- Frontend LCOV report: `apps/web/coverage/lcov.info` (HTML at `apps/web/coverage/lcov-report/index.html`).

## ✅ Suggested Next Steps

1. **Backend**: Set up Docker Desktop to enable integration tests, or exclude integration tests using `dotnet test --filter "Category!=Integration"` to collect unit test coverage only.
2. **Frontend**: ✅ ~~Fix the one failing test~~ (resolved via #253). Next: improve coverage for `upload.tsx`, `admin.tsx`, and `n8n.tsx` to reach Jest's 90% global thresholds.
3. Re-run coverage commands after Docker setup to generate fresh Cobertura reports for backend.

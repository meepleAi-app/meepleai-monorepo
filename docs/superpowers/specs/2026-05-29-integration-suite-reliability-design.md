# Make `Category=Integration` Suite Reliable Locally — Design Spec

**Date:** 2026-05-29
**Status:** Approved, pending writing-plans
**Origin:** Triage of PR #1670 (#1628) revealed the full local integration suite is unreliable. See `docs/superpowers/notes/2026-05-29-1628-fixture-alignment-fallout.md` for the original signal.

## Problem statement

Running `dotnet test --filter "Category=Integration"` locally produces ~50 failures out of ~1300 tests in ~30 minutes, but the failures are non-deterministic. Triage in #1628 showed they fall into three independent root causes:

1. **Resource exhaustion** from test classes that create their own Postgres container instead of using the existing `SharedTestcontainersFixture` (4 outlier files).
2. **Postgres connection-pool saturation** even with the shared fixture when 30+ classes execute concurrently against a single shared cluster.
3. **5 pre-existing FrontendSdk failures** on `main-dev` that fail identically with and without the #1628 change, blocking the "0 fail" target.

This spec covers a three-phase fix to make `Category=Integration` deterministically green locally in < 30 min.

## Out of scope

- Migrating the full integration suite to CI (CI today runs only `Backend Fast` (unit) on PRs; this stays).
- Performance optimization beyond a 30-min full run.
- Re-organizing the existing `Integration-GroupA/B/C/D` parallelism scheme.
- Removing `[Collection("Sequential")]` from non-integration test classes that use it for other reasons.
- Refactoring `FrontendSdkTestFactory` beyond what is needed to fix the 5 failing tests.

## Architecture: three independent phases

Each phase resolves one failure cluster and ends with a full-suite rerun to measure delta. Each phase can ship as a separate PR (linked via documentation) or bundled — decided in writing-plans.

```
Phase A  →  Consolidate outliers to SharedTestcontainersFixture
            Eliminates 4 dedicated Postgres containers.
            Expected delta: -30 Npgsql/EndOfStream/Socket failures.

Phase B  →  Stabilize concurrent execution
            xUnit MaxParallelThreads + Postgres max_connections + Npgsql pool size.
            Expected delta: residual Npgsql failures → 0.

Phase C  →  Debug + fix 5 FrontendSdk pre-existing failures
            Hypothesis-driven, per-cluster (3 auth-401, 2 validation-500).
            Expected delta: collection FrontendSdk → 0 fail; suite → 0 fail.
```

### Global success criterion

`dotnet test --filter "Category=Integration"` on `main-dev` post-PR runs **0 failures in ≤ 30 min**, deterministic across 2 consecutive runs (no flakes).

### Global validation method

Per phase, capture and compare a baseline log:

| Marker | Trigger | Saved to |
|---|---|---|
| `pre-A` | Before Phase A starts (captured on `main-dev` HEAD) | `audits/2026-05-29-integration-suite-baseline-pre-A.log` |
| `post-A` | After Phase A merged | `audits/2026-05-29-integration-suite-baseline-post-A.log` |
| `post-B` | After Phase B merged | `audits/2026-05-29-integration-suite-baseline-post-B.log` |
| `post-C` | After Phase C merged | `audits/2026-05-29-integration-suite-baseline-post-C.log` |

The `pre-A` baseline must be captured **before** any work begins on this branch and on the same `main-dev` HEAD that all subsequent phases will start from; otherwise the deltas become incomparable. This requirement is the first task of Phase A's implementation plan.

Each baseline is post-processed with:
- PASS/FAIL/SKIP counts and duration
- Error-type distribution: `grep -iE "(Npgsql|HttpStatusCode\.(Unauthorized|InternalServerError)|EndOfStream)" | sort | uniq -c | sort -rn`

## Phase A — Consolidate 4 outliers to `SharedTestcontainersFixture`

### Target pattern (from `SharedTestcontainersFixture.cs` lines 30-39 docstring)

```csharp
[Collection("SharedTestcontainers")]  // or "Integration-GroupA/B/C/D"
public class YourIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    public YourIntegrationTests(SharedTestcontainersFixture fixture) { _fixture = fixture; }

    public async ValueTask InitializeAsync()
    {
        var connStr = await _fixture.CreateIsolatedDatabaseAsync($"test_{Guid.NewGuid():N}");
        // ... rest of setup ...
    }
}
```

### Files and conversions

| File | Change | Collection target |
|---|---|---|
| `apps/api/tests/Api.Tests/Integration/Services/TotpServiceTrackingContractTests.cs` | Replace `new PostgreSqlBuilder()...` with `_fixture.CreateIsolatedDatabaseAsync(...)`; pass the connection string to `IntegrationWebApplicationFactory.Create(...)`. Constructor injection of `SharedTestcontainersFixture`. | `Integration-GroupD` (matches `BatchJobIntegrationTests` Administration cluster) |
| `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/DatabaseMetricsQueryTests.cs` | Same conversion. `pg_stat_*` queries are per-database, safe on the shared cluster with an isolated DB. Drop `[Collection("Sequential")]`. | `SharedTestcontainers` |
| `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/VacuumDatabaseCommandTests.cs` | Same conversion. VACUUM is per-database (not per-cluster), safe on the shared container. Drop `[Collection("Sequential")]`. | `SharedTestcontainers` |
| `apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs` | Abstract base class — switch image from `postgres:16-alpine` to `pgvector/pgvector:pg16` (superset, backward compatible) by replacing `new PostgreSqlBuilder()` with constructor-injected `SharedTestcontainersFixture`. All subclasses inherit the fixture. | Depends on subclasses (decided in writing-plans) |

### Open questions for writing-plans

- **Q1 — Subclass count for `AuthBoundedContextTestBase`**: run `grep -rl ": AuthBoundedContextTestBase" apps/api/tests/Api.Tests/` to enumerate; each subclass may need a constructor change to receive the fixture, or `[Collection(...)]` declaration.
- **Q2 — Schema initialization in `CreateIsolatedDatabaseAsync`**: does the helper apply EF migrations on the new isolated DB, or only create an empty schema? Verify in `SharedTestcontainersFixture.cs:~730` — determines whether call sites need `await db.Database.MigrateAsync()` explicitly.

### Risk

Low. The fixture is already used by 3+ classes (and is the implicit default for >30 classes via `Integration-GroupA/B/C/D`). The image swap for `AuthBoundedContextTestBase` adds only the `vector` extension to the Postgres environment, which the auth tests ignore.

### Phase A acceptance criterion

- Npgsql/EndOfStream/Socket failure cluster reduced by ≥ 70 % (target: from ~32 to ≤ 10).
- `Integration.Authentication` filter still 192/192 PASS (no regression).
- `S3AcceptanceScenariosTests` still 8-9/9 PASS (no regression of SP5 S3 critical flow).

## Phase B — Stabilize concurrent execution

Three independent, incrementally validated interventions.

### B.1 — `xunit.runner.json` MaxParallelThreads

- **File**: `apps/api/tests/Api.Tests/xunit.runner.json` (verify existence first in writing-plans — create if missing, modify if present).
- **Config**: `{"maxParallelThreads": 4, "parallelizeTestCollections": true}`.
- **Rationale**: 4 = number of `Integration-GroupA/B/C/D` collections → each group serializes internally, at most 4 collections run concurrently. Container/network/RAM saturation eliminated.
- **Rollback**: pure test-runner config, no application change.

### B.2 — Postgres `max_connections` bump

- **File**: `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs`, where the `PostgreSqlBuilder` is composed.
- **Modification**: append `.WithCommand("postgres", "-c", "max_connections=200")` to the builder.
- **Rationale**: default 100 is exhausted by 30+ classes × Npgsql pool of 100; raise to 200 + apply B.3 to keep within budget.
- **Rollback**: container restart in CI overhead ~5s; no production impact.

### B.3 — Npgsql `Maximum Pool Size` in connection string

- **File**: wherever `SharedTestcontainersFixture.CreateIsolatedDatabaseAsync` or `IntegrationServiceCollectionBuilder.CreateBase` composes the connection string.
- **Modification**: append `;Maximum Pool Size=20;Minimum Pool Size=2;Connection Idle Lifetime=30` to the connection string.
- **Rationale**: 20 conn × 4 classes-concurrent (B.1 gate) = **80 connections actively held** versus 200 server limit (B.2). Safe margin; idle classes return connections to pool within 30 s.

### Open question for writing-plans

- **Q3 — Existence of `xunit.runner.json`**: check `apps/api/tests/Api.Tests/` for prior config; if present, modify; if absent, create + ensure it is copied to output (`CopyToOutputDirectory` in csproj).

### Validation

After each of B.1, B.2, B.3 individually, capture a partial baseline and compare. Identify which intervention contributes most to the residual-failure-elimination. This per-step validation is important to localize the gain (avoids over-engineering).

### Phase B acceptance criterion

- Npgsql/EndOfStream/Socket failure cluster: **0**.
- Total duration of full suite ≤ duration(pre-B) + 25 %. If exceeded, revisit parameters before merge.

### Risk

Medium. Conservative tuning may slow the suite (more serial = slower). Mitigations:
- Keep the 4 `Integration-Group*` collections (parallelism unit = 4).
- If duration exceeds the +25 % threshold, escalate before merge.

## Phase C — Debug + fix 5 FrontendSdk pre-existing failures

### Failure inventory

| # | Test | Observed | Expected | Cluster |
|---|---|---|---|---|
| 1 | `Auth responses should include security headers` | 401 | 200 | A. Sessions broken |
| 2 | `Concurrent requests with same session should work correctly` | 401 | 200 | A. Sessions broken |
| 3 | `POST /auth/logout should clear session cookie` | 401 | 200 | A. Sessions broken |
| 4 | `GET with malformed request should return 400 Bad Request` | 500 | 400 | B. Validation 500 |
| 5 | `POST /auth/login with missing fields should return 400 Bad Request` | 500 | 400 | B. Validation 500 |

### Methodology (uses `superpowers:systematic-debugging` in implementation)

**For Cluster A (3 tests):**
1. Read `apps/api/tests/Api.Tests/Integration/FrontendSdk/FrontendSdkTestFactory.cs` and compare to `IntegrationWebApplicationFactory.cs`.
2. Read each of the 3 tests to understand exactly how the session is created.
3. Reproduce with logging or debugger: inspect `request.Cookies` and `HttpContext.User` server-side.
4. Identify divergence → narrow fix (test setup OR middleware).

**For Cluster B (2 tests):**
1. Read the 2 tests for the exact request payload/headers.
2. Reproduce manually: capture the unhandled exception stack-trace server-side.
3. Identify the middleware that should map it to 400 (does it exist? is it in the right pipeline order?).
4. Fix: add mapping in `ExceptionMappingMiddleware`, correct FluentValidator behavior, or add `[FromQuery]` with a custom binder.

### Open question for writing-plans

- **Q4 — Why 5 fail and 52 (or 17) PASS**: the `FrontendSdkTestFactory` had 57 tests in isolated run post-NoTracking and 22 in pre-NoTracking — number variance is itself a triage hint. The first question for cluster investigation: what makes these 5 tests structurally different from the others? Same fixture, same factory, same protocol. Answering this likely surfaces the root cause without test-by-test debugging.

### Output deliverables

- `docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md` with per-test root cause + linked fix commit.
- Fix commits narrowly scoped (no refactor): possibly 2-3 commits (1 cluster A, 1-2 cluster B).
- 0 fail in `Integration.FrontendSdk` isolated run AND full suite green.

### Risk handling

- **R1 — Cluster A fix breaks session-based tests elsewhere**: after each fix, re-run `Integration.Authentication` (192 baseline). It is the regression guard.
- **R2 — Bug in production code**: user has authorized fixing production code. Proceed.
- **R3 — Bug in `FrontendSdkTestFactory` only**: simpler fix, no production impact.

### Stop condition

If after ~4 h no root cause is identified for either cluster, escalate to the user to decide: continue debug, or fall back to skip + baseline using the CLAUDE.md "Known Flaky Tests" pattern.

### Phase C acceptance criterion

- `Integration.FrontendSdk` isolated run: 0 fail.
- Full suite `Category=Integration`: 0 fail.
- Two consecutive full-suite runs both green (determinism confirmed).

## Post-completion follow-ups

- Update `full-integration-suite-local-unreliable.md` memory: remove the "unreliable" warning; link to the canonical procedure.
- Add a `CONTRIBUTING.md` section: "How to run integration tests locally" with the validated commands and the new constraint (`maxParallelThreads: 4`).
- Memory note: capture the FrontendSdk root cause(s) as a new feedback entry so future analogous bugs are diagnosed faster.

## Cross-cutting risks

- B.2 + B.3 are the only changes that could affect the shared fixture's behavior. They are gated by per-intervention validation and the `Integration.Authentication` + `S3AcceptanceScenarios` regression guards in Phase A's acceptance criterion.
- The `pre-A` baseline log must be captured BEFORE any work begins on this branch; if it is captured after merging another PR to `main-dev`, the deltas across phases become incomparable.

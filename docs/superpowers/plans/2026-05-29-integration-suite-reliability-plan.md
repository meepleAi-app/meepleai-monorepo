# Integration Suite Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `dotnet test --filter "Category=Integration"` pass with 0 failures in ≤ 30 minutes deterministically on a local Docker Desktop.

**Architecture:** Two PRs in sequence. PR1 = "Test Infrastructure Cleanup" (Phase A delete-3-convert + Phase B verify) — low risk, infrastructure-only. PR2 = "FrontendSdk pre-existing failures fix" (Phase C) — touches application code, hypothesis-driven via `superpowers:systematic-debugging`. Validation: per-phase baseline comparison vs `pre-A` capture (see spec § Global validation method).

**Tech Stack:** .NET 9, xUnit v3, Testcontainers.PostgreSql, EF Core migrations, Microsoft.AspNetCore.Mvc.Testing, FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-05-29-integration-suite-reliability-design.md` (commit `28b69199a`).

**Branch in flight:** `feature/integration-suite-reliability` (already created, parent `main-dev`, contains the spec). PR1 work continues on this branch; PR2 will be a separate branch off `main-dev` post-PR1-merge.

---

## File structure

### Files to delete (PR1, Phase A)
- `apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs` — dead code (pre-flight Q1: zero subclasses).

### Files to modify (PR1, Phase A — conversion to `SharedTestcontainersFixture`)
- `apps/api/tests/Api.Tests/Integration/Services/TotpServiceTrackingContractTests.cs` — drop `PostgreSqlBuilder`, inject `SharedTestcontainersFixture`, use `CreateIsolatedDatabaseAsync` + explicit `MigrateAsync()`.
- `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/DatabaseMetricsQueryTests.cs` — same pattern.
- `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/VacuumDatabaseCommandTests.cs` — same pattern.

### Files to verify/modify (PR1, Phase B)
- `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs` — verify/possibly bump `PostgresMaxConnections` constant.

### Files to investigate (PR2, Phase C)
- `apps/api/tests/Api.Tests/Integration/FrontendSdk/FrontendSdkTestFactory.cs` — fixture for the failing collection.
- `apps/api/tests/Api.Tests/Integration/FrontendSdk/AuthenticationFlowTests.cs` + `HttpBehaviorTests.cs` + `EdgeCaseTests.cs` + `ErrorHandlingTests.cs` — locate the 5 failing tests by name search.
- `apps/api/src/Api/...` middleware pipeline (`ExceptionMappingMiddleware`, auth middleware) — application-side fix.

### Baseline files to create
- `audits/2026-05-29-integration-suite-baseline-pre-A.log`
- `audits/2026-05-29-integration-suite-baseline-post-A.log`
- `audits/2026-05-29-integration-suite-baseline-post-B.log`
- `audits/2026-05-29-integration-suite-baseline-post-C.log`

### Notes file to create
- `docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md` (during PR2, Phase C).

---

## Pre-flight (already done, do NOT redo)

The implementer should NOT redo these. They are recorded for context only.

- Q1: `AuthBoundedContextTestBase` has zero subclasses → delete.
- Q2: `CreateIsolatedDatabaseAsync` only runs `CREATE DATABASE`, does NOT apply migrations → each conversion must call `await db.Database.MigrateAsync()` explicitly.
- Q3: `xunit.runner.json` exists with `maxParallelThreads: 4` → B.1 already done.
- B.3: `MaxPoolSize=5` in `CreateIsolatedDatabaseAsync:750` → already done.

---

# PR1 — Test Infrastructure Cleanup (Phase A + Phase B)

## Task 1: Capture `pre-A` baseline

**Why:** Without a pre-change snapshot of failure count on a clean `main-dev`, post-phase deltas are not measurable. The baseline must be captured on `main-dev` HEAD before any work on this branch.

**Files:**
- Create: `audits/2026-05-29-integration-suite-baseline-pre-A.log`

- [ ] **Step 1: Confirm working tree is clean and `main-dev` is up to date**

```bash
cd D:/Repositories/meepleai-monorepo-main
git checkout main-dev
git pull --ff-only
git status
```

Expected: `On branch main-dev`, `Your branch is up to date with 'origin/main-dev'`, `nothing to commit, working tree clean`.

- [ ] **Step 2: Verify Docker Desktop is running**

```bash
docker info --format '{{.ServerVersion}}'
```

Expected: a version number (e.g., `29.4.3`). If missing daemon, start Docker Desktop and retry.

- [ ] **Step 3: Kill any zombie testhost / Api.Tests processes**

```bash
taskkill //F //IM testhost.exe 2>/dev/null
taskkill //F //IM Api.Tests.exe 2>/dev/null
```

Either output `OPERAZIONE RIUSCITA` or `non è in esecuzione` — both fine.

- [ ] **Step 4: Run the full Category=Integration suite and save the log**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "Category=Integration" --logger "console;verbosity=normal" > ../../../../audits/2026-05-29-integration-suite-baseline-pre-A.log 2>&1
```

This takes ~30 min. Expected at completion: a `Non superato!` or `Superato!` summary line with PASS/FAIL counts. Record them in the next step.

- [ ] **Step 5: Switch to the feature branch and stage the baseline file**

```bash
cd D:/Repositories/meepleai-monorepo-main
git checkout feature/integration-suite-reliability
git add audits/2026-05-29-integration-suite-baseline-pre-A.log
git commit -m "audit(test-infra): pre-A integration suite baseline

Captured on main-dev HEAD before any Phase A work. Reference point
for measuring per-phase delta in the integration suite reliability
project. See docs/superpowers/specs/2026-05-29-integration-suite-reliability-design.md."
```

---

## Task 2: Delete dead `AuthBoundedContextTestBase`

**Why:** Pre-flight Q1 confirmed zero subclasses. Deletion eliminates 1 of 4 own-container outliers with zero risk.

**Files:**
- Delete: `apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs`

- [ ] **Step 1: Re-verify zero subclasses before deletion**

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -rln ": AuthBoundedContextTestBase\|: Api\.Tests\.Infrastructure\.AuthBoundedContextTestBase" --include="*.cs" apps/api/tests/Api.Tests/
```

Expected: empty output. If output is non-empty, **STOP** and report — there is a subclass the pre-flight missed.

- [ ] **Step 2: Delete the file**

```bash
git rm apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs
```

- [ ] **Step 3: Build to confirm no broken references**

```bash
cd apps/api/src/Api
dotnet build --configuration Debug 2>&1 | tail -6
```

Expected: `Errori: 0`, `Avvisi: 0`. If errors, restore the file (`git restore --staged apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs && git restore apps/api/tests/Api.Tests/Infrastructure/AuthBoundedContextTestBase.cs`) and STOP — pre-flight Q1 was wrong.

- [ ] **Step 4: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git commit -m "chore(test-infra): delete dead AuthBoundedContextTestBase

Pre-flight verification of integration suite reliability spec confirmed
zero subclasses. The abstract base class is dead code; removing it
eliminates 1 of 4 own-container outliers contributing to local
integration-suite resource exhaustion."
```

---

## Task 3: Convert `DatabaseMetricsQueryTests` to `SharedTestcontainersFixture`

**Why:** Eliminates 1 dedicated Postgres container. Uses `pg_stat_*` queries which are per-database — safe on the shared cluster.

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/DatabaseMetricsQueryTests.cs`

- [ ] **Step 1: Read the current file in full to understand the existing structure**

```bash
cd D:/Repositories/meepleai-monorepo-main
cat apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/DatabaseMetricsQueryTests.cs
```

Note: current `InitializeAsync` creates `PostgreSqlContainer`, builds a small DI graph with `MediatR` mock and `IDomainEventCollector` mock, then calls `EnsureCreatedAsync`. Test method calls `GetDatabaseMetricsQueryHandler`. Keep all of this — only swap the Postgres source.

- [ ] **Step 2: Apply the conversion**

Replace the entire file with:

```csharp
using Api.BoundedContexts.Administration.Application.Queries.Resources;
using Api.Infrastructure;
using Api.Models;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for database metrics query.
/// Issue #3695: Resources Monitoring - Database metrics
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class DatabaseMetricsQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private ServiceProvider? _serviceProvider;

    public DatabaseMetricsQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #1628 follow-up: use the shared Postgres container with an isolated database
        // instead of spinning up a dedicated container per test class.
        var dbName = $"test_dbmetrics_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(connStr, o => o.UseVector()));

        // Mock dependencies required by MeepleAiDbContext
        services.AddScoped<IMediator>(_ => Mock.Of<IMediator>());
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector>(_ => Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        _serviceProvider = services.BuildServiceProvider();

        // CreateIsolatedDatabaseAsync only runs CREATE DATABASE — schema is empty.
        // Apply EF migrations explicitly (pre-flight Q2).
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Handle_ReturnsValidDatabaseMetrics()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new GetDatabaseMetricsQueryHandler(db);
        var query = new GetDatabaseMetricsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }
}
```

Notes:
- `[Collection("Sequential")]` is replaced by `[Collection("SharedTestcontainers")]`. The shared collection participates in `xunit.runner.json`'s `maxParallelThreads=4` budget.
- Constructor receives the fixture via xUnit collection-fixture DI.
- `Testcontainers.PostgreSql` `using` is removed; `Api.Tests.Infrastructure` `using` is added.
- `EnsureCreatedAsync` is replaced by `MigrateAsync` because the shared cluster needs migrations applied to the fresh database (pre-flight Q2).
- The `_postgres` field and its DisposeAsync are gone (fixture owns the container lifecycle).
- The 2nd test method body should be preserved if there are more than one in the original file; verify in Step 1 reading and copy as-is.

If the original file has more `[Fact]` methods beyond `Handle_ReturnsValidDatabaseMetrics`, **copy them all unchanged** into the new file. Do NOT change test bodies.

- [ ] **Step 3: Build**

```bash
cd apps/api/src/Api
dotnet build --configuration Debug 2>&1 | tail -6
```

Expected: `Errori: 0`.

- [ ] **Step 4: Run the converted test in isolation to verify it works under the shared fixture**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~DatabaseMetricsQueryTests" --logger "console;verbosity=normal" 2>&1 | tail -8
```

Expected: `Superato!` with all tests passing. If failures, inspect the output; common causes:
- `Migrate` failure → check that `MeepleAiDbContext` migrations apply cleanly on a fresh empty DB.
- `Cannot resolve SharedTestcontainersFixture` → check `using Api.Tests.Infrastructure;` is present.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/DatabaseMetricsQueryTests.cs
git commit -m "test(infra): convert DatabaseMetricsQueryTests to SharedTestcontainersFixture

Removes 1 of 4 own-container outliers contributing to local integration
suite resource exhaustion (#1628 follow-up). pg_stat_* queries are
per-database and safe on the shared cluster with an isolated DB.
Migrations applied explicitly post-CreateIsolatedDatabaseAsync (Q2)."
```

---

## Task 4: Convert `VacuumDatabaseCommandTests` to `SharedTestcontainersFixture`

**Why:** Eliminates 1 dedicated Postgres container. VACUUM is per-database (not per-cluster), safe on the shared cluster with an isolated DB.

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/VacuumDatabaseCommandTests.cs`

- [ ] **Step 1: Read the current file in full**

```bash
cd D:/Repositories/meepleai-monorepo-main
cat apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/VacuumDatabaseCommandTests.cs
```

Note all `[Fact]` methods present.

- [ ] **Step 2: Apply the conversion**

Replace the entire file with:

```csharp
using Api.BoundedContexts.Administration.Application.Commands.Resources;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for VACUUM database command.
/// Issue #3695: Resources Monitoring - VACUUM database action
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class VacuumDatabaseCommandTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private ServiceProvider? _serviceProvider;

    public VacuumDatabaseCommandTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #1628 follow-up: use the shared Postgres container with an isolated database
        // instead of spinning up a dedicated container per test class. VACUUM is per-database.
        var dbName = $"test_vacuum_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(connStr, o => o.UseVector()));

        services.AddScoped<IMediator>(_ => Mock.Of<IMediator>());
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector>(_ => Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        _serviceProvider = services.BuildServiceProvider();

        // Apply migrations on the freshly-created empty database (pre-flight Q2).
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    // PRESERVE EXISTING [Fact] / [Theory] METHODS HERE — copy unchanged from the original file.
}
```

In Step 1, you captured the existing `[Fact]` and `[Theory]` methods. **Paste them verbatim** in the `// PRESERVE EXISTING ...` placeholder above. Do NOT modify their bodies.

- [ ] **Step 3: Build**

```bash
cd apps/api/src/Api
dotnet build --configuration Debug 2>&1 | tail -6
```

Expected: `Errori: 0`.

- [ ] **Step 4: Run the converted test in isolation**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~VacuumDatabaseCommandTests" --logger "console;verbosity=normal" 2>&1 | tail -8
```

Expected: `Superato!` for all tests.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/VacuumDatabaseCommandTests.cs
git commit -m "test(infra): convert VacuumDatabaseCommandTests to SharedTestcontainersFixture

Removes 1 of 4 own-container outliers (#1628 follow-up). VACUUM is
per-database; safe on shared cluster with isolated DB."
```

---

## Task 5: Convert `TotpServiceTrackingContractTests` to `SharedTestcontainersFixture`

**Why:** Eliminates the last own-container outlier. This file was introduced by PR #1670 (#1628) — closing the loop on the debt I created.

**Files:**
- Modify: `apps/api/tests/Api.Tests/Integration/Services/TotpServiceTrackingContractTests.cs`

- [ ] **Step 1: Read the current file in full**

```bash
cd D:/Repositories/meepleai-monorepo-main
cat apps/api/tests/Api.Tests/Integration/Services/TotpServiceTrackingContractTests.cs
```

The file uses `IntegrationWebApplicationFactory.Create(_postgres.GetConnectionString())`. The fix is to replace `_postgres.GetConnectionString()` with the connection string from `_fixture.CreateIsolatedDatabaseAsync(...)`.

- [ ] **Step 2: Apply the conversion**

Modify the file with the following changes (do NOT rewrite from scratch — small targeted edits):

**Change 2.a**: replace the `using Testcontainers.PostgreSql;` line with nothing (remove the using).

**Change 2.b**: replace the `[Collection(...)]` attributes block at the class declaration. Find:

```csharp
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Authentication")]
public sealed class TotpServiceTrackingContractTests : IAsyncLifetime
```

Replace with:

```csharp
[Collection("Integration-GroupD")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Authentication")]
public sealed class TotpServiceTrackingContractTests : IAsyncLifetime
```

(Choice of `Integration-GroupD`: keeps parity with other Administration/Auth tests in that group.)

**Change 2.c**: replace the private field block:

```csharp
    private PostgreSqlContainer _postgres = null!;
    private WebApplicationFactory<Program> _factory = null!;
```

with:

```csharp
    private readonly SharedTestcontainersFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;

    public TotpServiceTrackingContractTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }
```

**Change 2.d**: replace the `InitializeAsync` body. Find:

```csharp
    public async ValueTask InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();
        await _postgres.StartAsync();

        _factory = IntegrationWebApplicationFactory.Create(_postgres.GetConnectionString());

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
    }
```

Replace with:

```csharp
    public async ValueTask InitializeAsync()
    {
        // Issue #1628 follow-up: use SharedTestcontainersFixture instead of a dedicated container.
        var dbName = $"test_totp_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName).ConfigureAwait(false);

        _factory = IntegrationWebApplicationFactory.Create(connStr);

        // Apply migrations on the freshly-created empty database (pre-flight Q2).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }
```

**Change 2.e**: replace the `DisposeAsync` body. Find:

```csharp
    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
```

Replace with:

```csharp
    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }
```

(The `_postgres` lifecycle is gone — the fixture owns the container.)

- [ ] **Step 3: Build**

```bash
cd apps/api/src/Api
dotnet build --configuration Debug 2>&1 | tail -6
```

Expected: `Errori: 0`. If errors mention `SharedTestcontainersFixture` not found, ensure the `Api.Tests.Infrastructure` using is present at the top of the file (likely already there — verify with `grep "using Api.Tests.Infrastructure" apps/api/tests/Api.Tests/Integration/Services/TotpServiceTrackingContractTests.cs`).

- [ ] **Step 4: Run the 3 contract tests in isolation**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~TotpServiceTrackingContractTests" --logger "console;verbosity=normal" 2>&1 | tail -8
```

Expected: `Superato!` with 3/3 PASS. Duration may be ~2-3 min including the 91s disable-test sleep.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/tests/Api.Tests/Integration/Services/TotpServiceTrackingContractTests.cs
git commit -m "test(infra): convert TotpServiceTrackingContractTests to SharedTestcontainersFixture

Closes the loop on the own-container outlier introduced by PR #1670
(#1628). Eliminates the last of 4 dedicated Postgres containers in the
integration suite. Test logic and assertions unchanged."
```

---

## Task 6: Validate Phase A — regression guards + post-A baseline

**Why:** Confirm Phase A introduced no regression in the critical paths (Auth, SP5 S3) and capture the post-A baseline for comparison vs `pre-A`.

**Files:**
- Create: `audits/2026-05-29-integration-suite-baseline-post-A.log`

- [ ] **Step 1: Run `Integration.Authentication` regression guard**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~Integration.Authentication" --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: `Superato!` with 192/192 PASS (or whatever the current count is, but 0 fail). If failures, **STOP** — a Phase A change broke auth flows.

- [ ] **Step 2: Run `S3AcceptanceScenariosTests` regression guard**

```bash
dotnet test --filter "FullyQualifiedName~S3AcceptanceScenarios" --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: `Superato!` with all SP5 S3 acceptance scenarios PASS. If failures, **STOP** — Phase A regressed SP5 strict 2FA.

- [ ] **Step 3: Kill zombies (full-suite run is heavy)**

```bash
taskkill //F //IM testhost.exe 2>/dev/null
taskkill //F //IM Api.Tests.exe 2>/dev/null
```

- [ ] **Step 4: Run the full `Category=Integration` suite and save `post-A` baseline**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "Category=Integration" --logger "console;verbosity=normal" > ../../../../audits/2026-05-29-integration-suite-baseline-post-A.log 2>&1
```

This takes ~30 min. After completion, extract the summary line:

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -iE "superato!|non superato!" audits/2026-05-29-integration-suite-baseline-post-A.log | tail -2
echo "=== Npgsql/EndOfStream/Socket cluster count ==="
grep -ciE "(Npgsql\.NpgsqlException|EndOfStreamException|Connessione interrotta)" audits/2026-05-29-integration-suite-baseline-post-A.log
```

- [ ] **Step 5: Compare to spec acceptance criterion**

Phase A acceptance: Npgsql/EndOfStream/Socket cluster ≤ 5 (down from ~32 in pre-A).

- If the cluster count is ≤ 5: Phase A succeeded. Proceed to Task 7 (Phase B verify).
- If the cluster count is > 5 but < 20: Phase B may help. Proceed to Task 7.
- If the cluster count is ≥ 20: **STOP and report** — the dominant cause is NOT the 4 outliers; the hypothesis in the spec is wrong. Re-analyze before continuing.

- [ ] **Step 6: Commit the baseline log**

```bash
git add audits/2026-05-29-integration-suite-baseline-post-A.log
git commit -m "audit(test-infra): post-A integration suite baseline

After Phase A (delete AuthBoundedContextTestBase + convert
DatabaseMetricsQueryTests, VacuumDatabaseCommandTests,
TotpServiceTrackingContractTests). Eliminates 4 dedicated Postgres
containers contributing to local resource exhaustion."
```

---

## Task 7: Phase B — verify `TestcontainersConfiguration.PostgresMaxConnections`

**Why:** The spec retains B as a verification gate. If the post-A baseline already shows 0 Npgsql failures, this is a no-op. If residuals remain, this single tuning may eliminate them.

**Files:**
- Modify (conditional): `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs`

- [ ] **Step 1: Read the current value**

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -n "PostgresMaxConnections" apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs
```

Record the current value.

- [ ] **Step 2: Compute the required budget**

The known constraints (from pre-flight):
- `xunit.runner.json`: `maxParallelThreads: 4` → at most 4 collections run concurrently.
- `CreateIsolatedDatabaseAsync:750`: `MaxPoolSize = 5` for local Testcontainers.

Worst-case demand: 4 concurrent classes × 5 connections each = **20 connections**. Add a 4× safety multiplier for transient peaks ⇒ **target ≥ 80**.

- [ ] **Step 3: Decide**

- If the current value is ≥ 100: **skip to Step 5** (no change needed). Commit nothing.
- If the current value is < 100: edit the constant to `100`.

- [ ] **Step 4: Apply the bump (only if Step 3 says "edit")**

Edit `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs`: locate the `PostgresMaxConnections` constant and change its value to `100`. Build to verify:

```bash
cd apps/api/src/Api
dotnet build --configuration Debug 2>&1 | tail -6
```

Expected: `Errori: 0`.

- [ ] **Step 5: Determine if post-B baseline is needed**

- If Step 3 said "skip" (no change): no post-B baseline needed; the post-A baseline is the final state for PR1. Proceed to Task 8.
- If Step 4 was executed: capture a post-B baseline.

```bash
taskkill //F //IM testhost.exe 2>/dev/null
taskkill //F //IM Api.Tests.exe 2>/dev/null
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "Category=Integration" --logger "console;verbosity=normal" > ../../../../audits/2026-05-29-integration-suite-baseline-post-B.log 2>&1
```

Verify the Npgsql cluster is now 0:

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -ciE "(Npgsql\.NpgsqlException|EndOfStreamException|Connessione interrotta)" audits/2026-05-29-integration-suite-baseline-post-B.log
```

Expected: `0`. If non-zero, the bump did not fully resolve — proceed anyway and report in the PR.

- [ ] **Step 6: Commit (only if Step 4 was executed)**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs audits/2026-05-29-integration-suite-baseline-post-B.log
git commit -m "test(infra): bump PostgresMaxConnections to 100

Worst-case budget under MaxParallelThreads=4 × MaxPoolSize=5 = 20
connections; raising the cap to 100 leaves 5× safety headroom for
transient peaks. Resolves residual Npgsql/EndOfStream cluster
observed in post-A baseline."
```

---

## Task 8: Open PR1 to `main-dev`

**Files:** (none — VCS operations)

- [ ] **Step 1: Push the branch**

```bash
cd D:/Repositories/meepleai-monorepo-main
git push -u origin feature/integration-suite-reliability 2>&1 | tail -5
```

Expected: branch pushed.

- [ ] **Step 2: Open PR**

```bash
gh pr create --draft --base main-dev --title "test(infra): consolidate own-container outliers to SharedTestcontainersFixture (Phase A + B)" --body "$(cat <<'EOF'
Companion PR to #1670 (#1628). Implements Phase A + Phase B of
`docs/superpowers/specs/2026-05-29-integration-suite-reliability-design.md`.

## What changed

- **Phase A**:
  - Deleted dead `AuthBoundedContextTestBase` (zero subclasses).
  - Converted `DatabaseMetricsQueryTests`, `VacuumDatabaseCommandTests`,
    `TotpServiceTrackingContractTests` from dedicated `PostgreSqlBuilder`
    to `SharedTestcontainersFixture` + `CreateIsolatedDatabaseAsync` +
    explicit `MigrateAsync`.
- **Phase B**: verified `TestcontainersConfiguration.PostgresMaxConnections`
  (see commit for whether a bump was applied).

## Why

Reduces local integration suite resource saturation: 4 fewer dedicated
Postgres containers + 4 fewer dedicated WebHosts. Confirmed in `pre-A`
vs `post-A` (vs `post-B` if applicable) baseline comparison:
`audits/2026-05-29-integration-suite-baseline-*.log`.

## Regression guards

- `Integration.Authentication` filter still 192/192 PASS.
- `S3AcceptanceScenariosTests` (SP5 S3 critical) still 8-9/9 PASS.
- `TotpServiceTrackingContractTests` (3 own contract tests) still 3/3
  PASS under the shared fixture.

## Out of scope

Phase C (5 FrontendSdk pre-existing failures) ships as a separate PR.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" 2>&1 | tail -3
```

Note the PR number.

- [ ] **Step 3: Wait for CI green, then mark ready and merge**

```bash
PR_NUM=$(gh pr view --json number --jq '.number')
gh pr checks $PR_NUM --watch
gh pr ready $PR_NUM
gh pr merge $PR_NUM --merge --delete-branch
```

Expected: all CI checks green, then merged to `main-dev`.

- [ ] **Step 4: Sync local main-dev**

```bash
cd D:/Repositories/meepleai-monorepo-main
git checkout main-dev
git pull --ff-only
```

---

# PR2 — FrontendSdk pre-existing failures fix (Phase C)

This PR starts on a fresh branch off `main-dev` (post-PR1 merge). It uses `superpowers:systematic-debugging` for the diagnosis.

## Task 9: Branch + locate the 5 failing tests

**Files:**
- Create branch only.

- [ ] **Step 1: Create branch from updated main-dev**

```bash
cd D:/Repositories/meepleai-monorepo-main
git checkout main-dev
git pull --ff-only
git checkout -b feature/frontendsdk-failures-fix
git config branch.feature/frontendsdk-failures-fix.parent main-dev
```

- [ ] **Step 2: Locate each of the 5 failing test methods**

The 5 tests by display name:
1. `Auth responses should include security headers`
2. `Concurrent requests with same session should work correctly`
3. `POST /auth/logout should clear session cookie`
4. `GET with malformed request should return 400 Bad Request`
5. `POST /auth/login with missing fields should return 400 Bad Request`

Locate each method name (xUnit display names may be set via `[Fact(DisplayName = "...")]`):

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -rn -A1 "DisplayName" apps/api/tests/Api.Tests/Integration/FrontendSdk/ | grep -E "(security headers|same session|logout|malformed|missing fields)" | head -20
```

Record each test's file path + method name. If display names are set via `[Theory]` + `[InlineData]`, the method may be parameterised; record the parameter values that match the failing case.

---

## Task 10: Cluster A investigation — sessions broken (3 tests)

**Why:** Tests 1, 2, 3 all return 401 instead of 200/expected. Hypothesis: the `FrontendSdkTestFactory` mishandles session cookies. Use `superpowers:systematic-debugging` to verify.

**Files:**
- Read-only investigation. Notes captured in `docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md` (created at end of cluster).

- [ ] **Step 1: Read `FrontendSdkTestFactory.cs` and compare to `IntegrationWebApplicationFactory.cs`**

```bash
cd D:/Repositories/meepleai-monorepo-main
diff apps/api/tests/Api.Tests/Integration/FrontendSdk/FrontendSdkTestFactory.cs apps/api/tests/Api.Tests/Infrastructure/IntegrationWebApplicationFactory.cs | head -100
```

Note divergences in:
- DbContext registration (NoTracking enabled?)
- Auth middleware order
- Cookie / CSRF middleware configuration
- Session services

- [ ] **Step 2: Read each of the 3 failing tests**

For each, capture: (a) how the session is created, (b) which endpoint is called, (c) the exact assertion.

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -n -A30 "security headers\|same session\|clear session cookie" apps/api/tests/Api.Tests/Integration/FrontendSdk/ | head -100
```

- [ ] **Step 3: Reproduce a single failing test with verbose logging**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "DisplayName~security headers" --logger "console;verbosity=detailed" 2>&1 | grep -iE "401|cookie|session|HttpContext" | head -30
```

- [ ] **Step 4: Form the hypothesis**

Based on Steps 1-3, write a one-paragraph hypothesis in `docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md` (create the file). Typical hypotheses:
- "Test creates session but doesn't extract the `meepleai_session` cookie from the response, so subsequent requests are anonymous."
- "FrontendSdkTestFactory registers a different cookie name than the auth middleware expects."
- "CSRF middleware blocks POST after login → 401."

Whichever hypothesis fits, state it explicitly and proceed to test it.

- [ ] **Step 5: Commit the diagnosis note (work-in-progress checkpoint)**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md
git commit -m "wip(notes): cluster A initial hypothesis for FrontendSdk auth-401 failures"
```

---

## Task 11: Cluster A fix

**Why:** Apply the narrowest fix consistent with the verified hypothesis.

**Files:**
- Modify: TBD per hypothesis (test fixture, OR application middleware, OR test methods themselves).

- [ ] **Step 1: Implement the fix per the verified hypothesis**

If the hypothesis is "test fixture mishandles cookie": fix `FrontendSdkTestFactory.cs` or the test helper.
If the hypothesis is "application middleware bug": fix the relevant file in `apps/api/src/Api/`.
If the hypothesis is "test assertion wrong": fix the test methods.

Make the change minimal — no refactor.

- [ ] **Step 2: Verify the 3 cluster A tests now pass in isolation**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "DisplayName~security headers|DisplayName~same session|DisplayName~clear session cookie" --logger "console;verbosity=normal" 2>&1 | tail -8
```

Expected: 3/3 PASS.

- [ ] **Step 3: Run `Integration.Authentication` regression guard**

```bash
dotnet test --filter "FullyQualifiedName~Integration.Authentication" --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: same PASS count as Task 6 Step 1. If failures, **STOP** — the fix regressed auth flows.

- [ ] **Step 4: Update the diagnosis note with the root cause and the fix**

Edit `docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md` to record:
- Root cause for cluster A (1 paragraph).
- Files changed by the fix (linked).
- The 3 tests now green in isolation.

- [ ] **Step 5: Commit cluster A fix**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add <files changed by the fix> docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md
git commit -m "fix(<area>): #1628 FrontendSdk cluster A — 3 auth-401 tests

Root cause: <one-sentence root cause from diagnosis note>.
Fix: <one-sentence fix description>.

Regression guard Integration.Authentication still 192/192 PASS."
```

---

## Task 12: Cluster B investigation — validation 500 (2 tests)

**Why:** Tests 4, 5 return 500 instead of 400 on malformed input. Hypothesis: an unhandled validation exception escapes the `ExceptionMappingMiddleware`. Use `superpowers:systematic-debugging`.

**Files:**
- Read-only investigation; notes appended to `2026-05-29-frontendsdk-failures-diagnosis.md`.

- [ ] **Step 1: Read each of the 2 failing tests**

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -n -A30 "malformed request\|missing fields" apps/api/tests/Api.Tests/Integration/FrontendSdk/ | head -80
```

Capture: (a) exact request payload, (b) headers, (c) endpoint, (d) expected status.

- [ ] **Step 2: Locate the global exception handler**

```bash
grep -rln "ExceptionMappingMiddleware\|UseExceptionHandler\|ProblemDetails" apps/api/src/Api/ | head -10
```

Read the middleware to understand which exception types map to which status codes.

- [ ] **Step 3: Reproduce one failing test with full stack capture**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "DisplayName~malformed request" --logger "console;verbosity=detailed" 2>&1 | grep -iE "Exception|stack|500|at " | head -40
```

Identify the exception type and where it is thrown.

- [ ] **Step 4: Form the hypothesis and document**

Append to `docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md`. Typical hypotheses:
- "FluentValidation throws `ValidationException`, not intercepted by `ExceptionMappingMiddleware` → 500."
- "Query parameter binding throws `FormatException` on malformed input → 500."
- "Model binding before validation runs → null reference inside handler → 500."

- [ ] **Step 5: Commit the WIP diagnosis**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md
git commit -m "wip(notes): cluster B hypothesis for FrontendSdk validation-500 failures"
```

---

## Task 13: Cluster B fix

**Files:**
- Modify: TBD per hypothesis (typically `ExceptionMappingMiddleware.cs` or the offending endpoint).

- [ ] **Step 1: Implement the narrowest fix**

Map the unhandled exception to 400 in `ExceptionMappingMiddleware`, OR add input validation pre-handler, OR fix the offending endpoint.

- [ ] **Step 2: Verify the 2 cluster B tests now pass**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "DisplayName~malformed request|DisplayName~missing fields" --logger "console;verbosity=normal" 2>&1 | tail -8
```

Expected: 2/2 PASS.

- [ ] **Step 3: Run `Integration.Authentication` regression guard**

```bash
dotnet test --filter "FullyQualifiedName~Integration.Authentication" --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: no regression.

- [ ] **Step 4: Update diagnosis note with cluster B root cause**

- [ ] **Step 5: Commit cluster B fix**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add <files changed> docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md
git commit -m "fix(<area>): #1628 FrontendSdk cluster B — 2 validation-500 tests

Root cause: <one-sentence root cause from diagnosis note>.
Fix: <one-sentence fix description>."
```

---

## Task 14: Validate Phase C — final baseline

**Files:**
- Create: `audits/2026-05-29-integration-suite-baseline-post-C.log`

- [ ] **Step 1: Run isolated FrontendSdk collection**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~Integration.FrontendSdk" --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: 0 FAIL.

- [ ] **Step 2: Kill zombies**

```bash
taskkill //F //IM testhost.exe 2>/dev/null
taskkill //F //IM Api.Tests.exe 2>/dev/null
```

- [ ] **Step 3: Run full Category=Integration suite and save post-C baseline**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/tests/Api.Tests
dotnet test --filter "Category=Integration" --logger "console;verbosity=normal" > ../../../../audits/2026-05-29-integration-suite-baseline-post-C.log 2>&1
```

- [ ] **Step 4: Verify global success criterion**

```bash
cd D:/Repositories/meepleai-monorepo-main
grep -iE "superato!|non superato!" audits/2026-05-29-integration-suite-baseline-post-C.log | tail -2
```

Expected: `Non superati: 0`. If non-zero, identify the residual failures — if they are the same 5 FrontendSdk tests, the fix was incomplete; if they are new, a regression slipped in.

- [ ] **Step 5: Re-run for determinism**

```bash
taskkill //F //IM testhost.exe 2>/dev/null
taskkill //F //IM Api.Tests.exe 2>/dev/null
dotnet test --filter "Category=Integration" --logger "console;verbosity=minimal" 2>&1 | grep -iE "superato!|non superato!" | tail -1
```

Expected: `Non superati: 0` again. If non-zero on either run, flakes remain — do not merge.

- [ ] **Step 6: Commit baseline**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add audits/2026-05-29-integration-suite-baseline-post-C.log
git commit -m "audit(test-infra): post-C integration suite baseline — 0 fail

After Phase C FrontendSdk fix. Two consecutive deterministic green runs
confirm the integration suite reliability project is complete."
```

---

## Task 15: Open PR2 to `main-dev`

**Files:** (none)

- [ ] **Step 1: Push the branch**

```bash
cd D:/Repositories/meepleai-monorepo-main
git push -u origin feature/frontendsdk-failures-fix 2>&1 | tail -5
```

- [ ] **Step 2: Open PR**

```bash
gh pr create --draft --base main-dev --title "fix(2fa-flows): FrontendSdk pre-existing 5 failures (Phase C)" --body "$(cat <<'EOF'
Companion PR to #1670 (#1628). Implements Phase C of
`docs/superpowers/specs/2026-05-29-integration-suite-reliability-design.md`.

## What changed

- **Cluster A** (3 tests, auth-401):
  - Root cause: <fill in from diagnosis note>.
  - Fix: <fill in>.
- **Cluster B** (2 tests, validation-500):
  - Root cause: <fill in from diagnosis note>.
  - Fix: <fill in>.

## Why

Eliminates the 5 pre-existing FrontendSdk failures that were the last
non-determinism in the local `Category=Integration` suite. Combined
with PR1 (Phase A+B), the suite now passes deterministically at 0 fail
in ~30 min locally. See diagnosis:
`docs/superpowers/notes/2026-05-29-frontendsdk-failures-diagnosis.md`.

## Validation

- Two consecutive full-suite runs locally: 0 fail. See
  `audits/2026-05-29-integration-suite-baseline-post-C.log`.
- `Integration.Authentication` regression guard 192/192 PASS after
  each cluster fix.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" 2>&1 | tail -3
```

- [ ] **Step 3: Wait for CI green, mark ready, merge**

```bash
PR_NUM=$(gh pr view --json number --jq '.number')
gh pr checks $PR_NUM --watch
gh pr ready $PR_NUM
gh pr merge $PR_NUM --merge --delete-branch
```

- [ ] **Step 4: Final cleanup + memory update**

```bash
cd D:/Repositories/meepleai-monorepo-main
git checkout main-dev
git pull --ff-only
```

Update `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-main\memory\full-integration-suite-local-unreliable.md`: remove the "unreliable" warning, replace with "as of <date>, the full Category=Integration suite passes deterministically locally in ~30 min after PR1+PR2 of the integration-suite-reliability project".

Append to `CONTRIBUTING.md` a "How to run integration tests locally" section with the canonical command and the constraint (`maxParallelThreads: 4`, kill zombies between runs).

---

## Self-Review

### Spec coverage

| Spec requirement | Plan task |
|---|---|
| Phase A delete `AuthBoundedContextTestBase` | Task 2 |
| Phase A convert `DatabaseMetricsQueryTests` | Task 3 |
| Phase A convert `VacuumDatabaseCommandTests` | Task 4 |
| Phase A convert `TotpServiceTrackingContractTests` | Task 5 |
| Phase A acceptance criteria (regression guards + Npgsql cluster ≤ 5) | Task 6 |
| Phase B verify `PostgresMaxConnections` | Task 7 |
| Phase C cluster A diagnosis + fix | Tasks 10, 11 |
| Phase C cluster B diagnosis + fix | Tasks 12, 13 |
| Phase C acceptance (0 fail, 2 consecutive runs) | Task 14 |
| Baseline captures (pre-A, post-A, post-C) | Tasks 1, 6, 14 |
| Post-B baseline (conditional) | Task 7 Step 5 |
| Memory update + CONTRIBUTING.md | Task 15 Step 4 |
| Diagnosis notes file | Tasks 10, 12 |
| Two PRs strategy | PR1 (Tasks 1-8), PR2 (Tasks 9-15) |

### Placeholder scan

The plan contains placeholders in Tasks 11 and 13 Step 1 (`Modify: TBD per hypothesis`) and in Task 15 Step 2 (PR body `<fill in from diagnosis note>`). These are **intentional** — Phase C is hypothesis-driven and the exact files / root causes cannot be known until the diagnosis phase completes. The hypothesis-formation steps (Task 10 Step 4 and Task 12 Step 4) instruct the implementer to commit explicit hypotheses before implementing fixes, which prevents the placeholders from being substituted with vague wording.

### Type / API consistency

- `SharedTestcontainersFixture` constructor injection pattern is identical across Tasks 3, 4, 5.
- `CreateIsolatedDatabaseAsync(string)` and `MigrateAsync()` are used consistently.
- `IntegrationWebApplicationFactory.Create(connStr)` signature in Task 5 matches the existing factory (one positional connection-string argument).
- Commit message tags (`test(infra)`, `chore(test-infra)`, `audit(test-infra)`, `fix(<area>)`, `docs(specs)`, `wip(notes)`) are consistent with the project convention seen in `git log` of `main-dev`.

### Coverage gap

No spec requirement is unaddressed. The plan's PR1+PR2 split is more granular than the spec's "shipping strategy" section but consistent with it.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-29-integration-suite-reliability-plan.md`. Two execution options:

1. **Subagent-Driven (recommended)** — dispatch a fresh subagent per task, two-stage review between tasks, fast iteration. Use `superpowers:subagent-driven-development`.

2. **Inline Execution** — execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints.

Which approach?

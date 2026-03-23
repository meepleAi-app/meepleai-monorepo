# CI Integration Test Fixes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 161 failing integration tests in CI pipeline (main-staging PRs)

**Architecture:** Fix bugs in test code across 6 root-cause categories: wrong migration references, Redis config, Moq callback mismatches, EF Core tracking conflicts, FluentValidation assertion, and DI/auth issues in WebApplicationFactory tests.

**Tech Stack:** .NET 9, xUnit, FluentAssertions, Moq, Testcontainers, EF Core, StackExchange.Redis

---

## Root Cause Analysis

| # | Root Cause | Tests Affected | Fix Type |
|---|-----------|---------------|----------|
| 1 | Migration names wrong (InitialCreate doesn't exist) | 13 | Code fix |
| 2 | Redis `allowAdmin=false` in test connection | 5 | Config fix |
| 3 | FluentValidation wildcard mismatch | 1 | Assertion fix |
| 4 | Moq `.Callback<>` param count mismatch | 1 | Mock fix |
| 5 | EF Core double-tracking in BatchJobRepository | 5+ | Repository fix |
| 6 | WebApplicationFactory DI/auth cascading failures | 100+ | Investigate separately |

**Scope:** Tasks 1-5 fix ~25 deterministic test failures with clear root causes. Task 6 documents the WebApplicationFactory issues for a follow-up investigation.

---

### Task 1: Fix ContextEngineeringMigrationRollbackTests migration names (13 tests)

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/ContextEngineeringMigrationRollbackTests.cs:59-64`

**Root cause:** The test references migrations `20260208111903_InitialCreate` and `20260208162522_AddPlayRecords` which never existed. The actual first migration is `20260316055120_Beta0` and the second is `20260316105334_AddServiceHealthStates`.

- [ ] **Step 1: Update migration constants**

In `ContextEngineeringMigrationRollbackTests.cs`, replace lines 59-64:

```csharp
// OLD:
private const string InitialCreateMigration = "20260208111903_InitialCreate";
private const string PostInitialMigration = "20260208162522_AddPlayRecords";

// NEW:
private const string InitialCreateMigration = "20260316055120_Beta0";
private const string PostInitialMigration = "20260316105334_AddServiceHealthStates";
```

- [ ] **Step 2: Verify migration names exist**

Run: `ls apps/api/src/Api/Infrastructure/Migrations/20260316055120_Beta0.cs apps/api/src/Api/Infrastructure/Migrations/20260316105334_AddServiceHealthStates.cs`
Expected: Both files exist

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/ContextEngineeringMigrationRollbackTests.cs
git commit -m "fix(tests): update migration names in ContextEngineeringMigrationRollbackTests

InitialCreate (20260208) never existed - actual first migration is Beta0 (20260316)"
```

---

### Task 2: Fix Redis allowAdmin in CacheMetrics and ClearCache tests (5 tests)

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/CacheMetricsQueryTests.cs:30-31`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/ClearCacheCommandTests.cs:30-31`

**Root cause:** Tests create a Redis Testcontainer and connect with `ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString())` which defaults to `allowAdmin=false`. The `GetCacheMetricsQueryHandler` calls Redis `INFO` (admin command) and `ClearCacheCommandHandler` calls `FLUSHALL` (admin command), both of which require `allowAdmin=true`.

- [ ] **Step 1: Fix CacheMetricsQueryTests Redis connection**

In `CacheMetricsQueryTests.cs`, change line 30-31:

```csharp
// OLD:
_connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString())
    .ConfigureAwait(false);

// NEW:
_connection = await ConnectionMultiplexer.ConnectAsync($"{_redis.GetConnectionString()},allowAdmin=true")
    .ConfigureAwait(false);
```

- [ ] **Step 2: Fix ClearCacheCommandTests Redis connection**

In `ClearCacheCommandTests.cs`, change line 31-32:

```csharp
// OLD:
_connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString())
    .ConfigureAwait(false);

// NEW:
_connection = await ConnectionMultiplexer.ConnectAsync($"{_redis.GetConnectionString()},allowAdmin=true")
    .ConfigureAwait(false);
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/CacheMetricsQueryTests.cs
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/ClearCacheCommandTests.cs
git commit -m "fix(tests): add allowAdmin=true to Redis test connections

INFO and FLUSHALL are admin commands that require allowAdmin=true"
```

---

### Task 3: Fix ClearCacheCommand validator wildcard assertion (1 test)

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/ClearCacheCommandTests.cs:124-125`

**Root cause:** The test asserts `WithErrorMessage("*Confirmation is required*")` using FluentValidation's wildcard. The actual error message is `"Confirmation is required to clear the cache. This action cannot be undone."`. FluentValidation's `WithErrorMessage` with wildcards uses `*` as glob - but the test passes the wildcard inside `.WithErrorMessage()` which uses exact match, not wildcard match. The correct method for wildcard matching is `.WithErrorMessage()` with the exact message or using `WithMessageContaining()` pattern.

- [ ] **Step 1: Fix validator assertion to match exact message**

In `ClearCacheCommandTests.cs`, change lines 124-125:

```csharp
// OLD:
result.ShouldHaveValidationErrorFor(x => x.Confirmed)
    .WithErrorMessage("*Confirmation is required*");

// NEW:
result.ShouldHaveValidationErrorFor(x => x.Confirmed)
    .WithErrorMessage("Confirmation is required to clear the cache. This action cannot be undone.");
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/ResourcesTests/ClearCacheCommandTests.cs
git commit -m "fix(tests): use exact error message in ClearCacheCommand validator test

Wildcard matching not supported in WithErrorMessage - use exact string"
```

---

### Task 4: Fix ExtractGameMetadata Moq Callback param mismatch (1 test)

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/ExtractGameMetadataFromPdfQueryIntegrationTests.cs:278`

**Root cause:** The mock setup calls `.Setup(b => b.RetrieveAsync(string, string, CancellationToken))` (3 params) but `.Callback<string, string, RequestSource, CancellationToken>` specifies 4 type params including `RequestSource` which doesn't exist in the `IBlobStorageService.RetrieveAsync` signature.

`IBlobStorageService.RetrieveAsync` signature: `Task<Stream?> RetrieveAsync(string fileId, string gameId, CancellationToken ct)`

- [ ] **Step 1: Fix Callback generic type parameters**

In `ExtractGameMetadataFromPdfQueryIntegrationTests.cs`, change line 278:

```csharp
// OLD:
.Callback<string, string, RequestSource, CancellationToken>((_, _, _, ct) =>
{
    tokenReceived = ct == cts.Token;
})

// NEW:
.Callback<string, string, CancellationToken>((_, _, ct) =>
{
    tokenReceived = ct == cts.Token;
})
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/ExtractGameMetadataFromPdfQueryIntegrationTests.cs
git commit -m "fix(tests): correct Moq Callback params for RetrieveAsync

IBlobStorageService.RetrieveAsync has 3 params, not 4"
```

---

### Task 5: Fix BatchJobRepository double-tracking in UpdateAsync (5+ tests)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/BatchJobRepository.cs:87-97`

**Root cause:** `AddAsync` tracks the entity via `DbContext.BatchJobs.AddAsync()`. Then `GetByIdAsync` returns a NEW detached instance (uses `AsNoTracking()`). When `UpdateAsync` is called with this new instance, `DbContext.BatchJobs.Update()` tries to track it, but the original instance with the same `Id` is still tracked → conflict.

The existing code checks `DbContext.Entry(batchJob).State == Detached` but this only checks the NEW entity's state. It doesn't detach the OLD tracked entity.

- [ ] **Step 1: Fix UpdateAsync to detach existing tracked entities**

In `BatchJobRepository.cs`, replace `UpdateAsync` method (lines 87-97):

```csharp
public async Task UpdateAsync(BatchJob batchJob, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(batchJob);

    // Detach any previously tracked entity with the same key to avoid conflicts
    // This happens when AddAsync tracked the original, then GetByIdAsync (AsNoTracking)
    // returns a new instance, and we try to Update the new instance
    var existingTracked = DbContext.ChangeTracker.Entries<BatchJob>()
        .FirstOrDefault(e => e.Entity.Id == batchJob.Id);
    if (existingTracked != null)
    {
        existingTracked.State = EntityState.Detached;
    }

    DbContext.BatchJobs.Update(batchJob);
    await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
```

- [ ] **Step 2: Verify EntityState using directive exists**

The file should already have `using Microsoft.EntityFrameworkCore;` — verify this import exists at the top of `BatchJobRepository.cs`.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/BatchJobRepository.cs
git commit -m "fix: detach existing tracked BatchJob before Update to prevent tracking conflict

GetByIdAsync uses AsNoTracking, so UpdateAsync receives a detached copy
while the original from AddAsync is still tracked"
```

---

### Task 6: Document remaining WebApplicationFactory failures for follow-up

**Files:**
- Create: `docs/superpowers/plans/2026-03-22-ci-waf-investigation.md` (investigation notes)

The remaining ~130 failures share common root causes in WebApplicationFactory integration tests:
- **DI resolution failures** (IDashboardStreamService, IGameSessionRepository, INotificationDispatcher, IDomainEventCollector) → ~17 tests. These handlers register in Program.cs but may be removed/overridden by test ConfigureServices
- **403 Forbidden** → ~17 tests. Auth middleware blocks requests - tests don't properly set up admin sessions
- **Role "system" validation** → ~11 tests. Tests create User entities with `Role = "system"` but Role value object only allows: user, creator, editor, admin, superadmin
- **AgentId null constraint** → ~8 tests. Tests create AgentSession without seeding the parent Agent entity
- **chk_shared_games_players** → ~6 tests. Test data seeds SharedGame with invalid min_players/max_players (constraint: both 0 OR min>0 AND max>=min)
- **JSON parse errors** → ~4 tests. Empty response body parsed as JSON
- **Various** → performance test timeout, sequence errors, format exceptions

These require a deeper investigation into each WebApplicationFactory setup to determine if they share a common misconfiguration pattern.

- [ ] **Step 1: Document the investigation findings**

Save this task's analysis to `docs/superpowers/plans/2026-03-22-ci-waf-investigation.md` for the follow-up session.

- [ ] **Step 2: Commit all fixes**

```bash
git add -A
git commit -m "docs: document remaining CI failures for follow-up investigation"
```

---

## Verification

After implementing Tasks 1-5, create a PR to `main-dev` and verify:

1. The 13 ContextEngineering migration tests pass
2. The 5 Redis admin tests pass
3. The 1 validator assertion test passes
4. The 1 Moq callback test passes
5. The 5+ BatchJob tracking tests pass

Expected improvement: ~25 fewer failures (from 161 → ~136)

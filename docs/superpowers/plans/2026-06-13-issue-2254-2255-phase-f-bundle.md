# F5+F6 Phase F bundle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship #2254 F5 bulk acknowledge UI + #2255 F6 attempt-source attribution in one PR, closing Phase F follow-up of epic #1823.

**Architecture:** Mirror Phase E F2/F3/F4 patterns. Bundle 1 migration (3 columns + 1 partial index) + extend aggregate (mutator + factory params) + 1 new CQRS command + 2 query DTO extensions + 1 new endpoint + FE toolbar/modal/badge on existing dead-letter visibility page.

**Tech Stack:** .NET 9, EF Core, FluentValidation, MediatR, PostgreSQL, xUnit + Testcontainers, Next.js 16 + React 19, Tailwind 4, Vitest, Playwright.

**Spec:** [`docs/superpowers/specs/2026-06-13-issue-2254-2255-phase-f-bundle-design.md`](../specs/2026-06-13-issue-2254-2255-phase-f-bundle-design.md)
**Branch:** `feature/issue-2254-2255-phase-f-bundle`
**Estimate:** ~19h (~2.5gg)

---

## Phase 1 — Backend Domain

### Task 1.1: Aggregate F5 mutator `Acknowledge()`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttempt.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttemptAcknowledgeTests.cs` (new file)

- [ ] **Step 1: Write the failing test** (4 cases in one file)

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public class WikidataCoverEnrichmentAttemptAcknowledgeTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime AttemptedAt = new(2026, 06, 13, 10, 00, 00, DateTimeKind.Utc);
    private static readonly DateTime AckedAt = new(2026, 06, 13, 11, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void Acknowledge_OnDeadLetter_PersistsAtAndBy()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", details: null, retryCount: 3, attemptedAt: AttemptedAt);

        dl.Acknowledge(UserId, AckedAt);

        dl.AcknowledgedAt.Should().Be(AckedAt);
        dl.AcknowledgedBy.Should().Be(UserId);
    }

    [Fact]
    public void Acknowledge_TwiceIsIdempotent_PreservesFirstAck()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", null, 3, AttemptedAt);

        dl.Acknowledge(UserId, AckedAt);

        var laterUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var laterAt = AckedAt.AddHours(2);
        dl.Acknowledge(laterUserId, laterAt);

        dl.AcknowledgedAt.Should().Be(AckedAt);  // preserved
        dl.AcknowledgedBy.Should().Be(UserId);    // preserved
    }

    [Fact]
    public void Acknowledge_GuidEmpty_ThrowsArgumentException()
    {
        var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(
            GameId, "r2-upload-error", null, 3, AttemptedAt);

        var act = () => dl.Acknowledge(Guid.Empty, AckedAt);

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(WikidataCoverEnrichmentOutcome.Success)]
    [InlineData(WikidataCoverEnrichmentOutcome.Skipped)]
    [InlineData(WikidataCoverEnrichmentOutcome.Failed)]
    public void Acknowledge_NonDeadLetterState_ThrowsInvalidOperationException(
        WikidataCoverEnrichmentOutcome outcome)
    {
        WikidataCoverEnrichmentAttempt attempt = outcome switch
        {
            WikidataCoverEnrichmentOutcome.Success =>
                WikidataCoverEnrichmentAttempt.RecordSuccess(GameId, 0, AttemptedAt),
            WikidataCoverEnrichmentOutcome.Skipped =>
                WikidataCoverEnrichmentAttempt.RecordSkipped(GameId, "qid-missing", 0, AttemptedAt),
            WikidataCoverEnrichmentOutcome.Failed =>
                WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
                    GameId, "r2-upload-error", null, 1, AttemptedAt, AttemptedAt.AddMinutes(5)),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        var act = () => attempt.Acknowledge(UserId, AckedAt);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{outcome}*");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttemptAcknowledgeTests" --no-restore`
Expected: FAIL with `'WikidataCoverEnrichmentAttempt' does not contain a definition for 'Acknowledge'` and `'AcknowledgedAt'/'AcknowledgedBy'`.

- [ ] **Step 3: Write minimal implementation**

Edit `WikidataCoverEnrichmentAttempt.cs` — add 2 properties and the mutator below `DeadLetteredAt`:

```csharp
/// <summary>UTC timestamp when an operator acknowledged the dead-letter; <see langword="null"/> when not yet acknowledged or not a dead-letter.</summary>
public DateTime? AcknowledgedAt { get; private set; }

/// <summary>User id of the operator who acknowledged the dead-letter; <see langword="null"/> when not yet acknowledged.</summary>
public Guid? AcknowledgedBy { get; private set; }

// (place the mutator at the end of the type, after Reconstitute)

/// <summary>
/// Issue #1823 Phase F F5 — operator marks a dead-letter as "not actionable".
/// EXCEPTION to the record-of-fact pattern documented on the type: ack is
/// operational metadata orthogonal to the pipeline (parallel to
/// <see cref="DeadLetteredAt"/>), not a new pipeline event. Idempotent on
/// re-call: the first ack is preserved and subsequent calls are no-ops so
/// repeated bulk-acknowledge clicks cannot rewrite the audit trail.
/// </summary>
/// <param name="userId">Acknowledging admin user id; must not be <see cref="Guid.Empty"/>.</param>
/// <param name="ackedAt">UTC acknowledgement timestamp.</param>
public void Acknowledge(Guid userId, DateTime ackedAt)
{
    if (Outcome != WikidataCoverEnrichmentOutcome.DeadLetter)
        throw new InvalidOperationException(
            $"Only DeadLetter attempts can be acknowledged; current Outcome={Outcome}.");

    if (userId == Guid.Empty)
        throw new ArgumentException("UserId cannot be Guid.Empty.", nameof(userId));

    if (AcknowledgedAt is not null) return; // idempotent

    AcknowledgedAt = ackedAt;
    AcknowledgedBy = userId;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttemptAcknowledgeTests" --no-restore`
Expected: PASS (6 tests — 3 happy + 3 theory).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttempt.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttemptAcknowledgeTests.cs
git commit -m "feat(catalog-be): #2254 Acknowledge() mutator on WikidataCoverEnrichmentAttempt"
```

---

### Task 1.2: Aggregate F6 factory + Reconstitute params

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttempt.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttemptTriggerSourceTests.cs` (new)

- [ ] **Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public class WikidataCoverEnrichmentAttemptTriggerSourceTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime At = new(2026, 06, 13, 10, 00, 00, DateTimeKind.Utc);

    [Fact]
    public void RecordSuccess_DefaultSchedulerPath_TriggeredByAdminUserIdIsNull()
    {
        var a = WikidataCoverEnrichmentAttempt.RecordSuccess(GameId, 0, At);

        a.TriggeredByAdminUserId.Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_AdminTriggered_PersistsAdminId()
    {
        var a = WikidataCoverEnrichmentAttempt.RecordSuccess(
            GameId, 0, At, triggeredByAdminUserId: AdminId);

        a.TriggeredByAdminUserId.Should().Be(AdminId);
    }

    [Fact]
    public void Reconstitute_RoundTripsTriggeredByAdminUserId()
    {
        var hydrated = WikidataCoverEnrichmentAttempt.Reconstitute(
            id: Guid.NewGuid(),
            sharedGameId: GameId,
            attemptedAt: At,
            outcome: WikidataCoverEnrichmentOutcome.Success,
            reason: "success",
            details: null,
            retryCount: 0,
            nextRetryAt: null,
            deadLetteredAt: null,
            acknowledgedAt: null,
            acknowledgedBy: null,
            triggeredByAdminUserId: AdminId);

        hydrated.TriggeredByAdminUserId.Should().Be(AdminId);
        hydrated.AcknowledgedAt.Should().BeNull();
        hydrated.AcknowledgedBy.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttemptTriggerSourceTests" --no-restore`
Expected: FAIL on `triggeredByAdminUserId` keyword unknown to factories.

- [ ] **Step 3: Write minimal implementation**

Edit `WikidataCoverEnrichmentAttempt.cs`:

1. Add property below `AcknowledgedBy`:
```csharp
/// <summary>
/// Issue #1823 Phase F F6 — user id of the admin who manually triggered this
/// attempt via M12 (single trigger) or F2 (bulk retry). <see langword="null"/>
/// when invoked by the M9 scheduler (the default path).
/// </summary>
public Guid? TriggeredByAdminUserId { get; private set; }
```

2. Update constructor signature + body to accept all 3 new fields:
```csharp
private WikidataCoverEnrichmentAttempt(
    Guid id, Guid sharedGameId, DateTime attemptedAt,
    WikidataCoverEnrichmentOutcome outcome, string reason, string? details,
    int retryCount, DateTime? nextRetryAt, DateTime? deadLetteredAt,
    DateTime? acknowledgedAt, Guid? acknowledgedBy,
    Guid? triggeredByAdminUserId) : base(id)
{
    SharedGameId = sharedGameId;
    AttemptedAt = attemptedAt;
    Outcome = outcome;
    Reason = reason;
    Details = details;
    RetryCount = retryCount;
    NextRetryAt = nextRetryAt;
    DeadLetteredAt = deadLetteredAt;
    AcknowledgedAt = acknowledgedAt;
    AcknowledgedBy = acknowledgedBy;
    TriggeredByAdminUserId = triggeredByAdminUserId;
}
```

3. Update `Create(...)` private helper to accept + propagate `Guid? triggeredByAdminUserId`:
```csharp
private static WikidataCoverEnrichmentAttempt Create(
    Guid sharedGameId, DateTime attemptedAt, WikidataCoverEnrichmentOutcome outcome,
    string reason, string? details, int retryCount,
    DateTime? nextRetryAt, DateTime? deadLetteredAt,
    Guid? triggeredByAdminUserId)
{
    if (sharedGameId == Guid.Empty)
        throw new ArgumentException("SharedGameId cannot be Guid.Empty.", nameof(sharedGameId));
    if (string.IsNullOrWhiteSpace(reason))
        throw new ArgumentException("Reason is required.", nameof(reason));
    if (reason.Length > 64)
        throw new ArgumentException("Reason must be 64 characters or fewer.", nameof(reason));
    if (details is { Length: > 1024 })
        throw new ArgumentException("Details must be 1024 characters or fewer.", nameof(details));
    if (retryCount < 0)
        throw new ArgumentOutOfRangeException(nameof(retryCount), retryCount, "RetryCount must be non-negative.");

    return new WikidataCoverEnrichmentAttempt(
        id: Guid.NewGuid(),
        sharedGameId, attemptedAt, outcome, reason, details, retryCount,
        nextRetryAt, deadLetteredAt,
        acknowledgedAt: null, acknowledgedBy: null,
        triggeredByAdminUserId);
}
```

4. Add `Guid? triggeredByAdminUserId = null` (default null) to each public factory and propagate:
```csharp
public static WikidataCoverEnrichmentAttempt RecordSuccess(
    Guid sharedGameId, int retryCount, DateTime attemptedAt,
    Guid? triggeredByAdminUserId = null) =>
    Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.Success,
        "success", null, retryCount, null, null, triggeredByAdminUserId);

public static WikidataCoverEnrichmentAttempt RecordSkipped(
    Guid sharedGameId, string reason, int retryCount, DateTime attemptedAt,
    Guid? triggeredByAdminUserId = null) =>
    Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.Skipped,
        reason, null, retryCount, null, null, triggeredByAdminUserId);

public static WikidataCoverEnrichmentAttempt RecordFailedWithRetry(
    Guid sharedGameId, string reason, string? details, int retryCount,
    DateTime attemptedAt, DateTime nextRetryAt,
    Guid? triggeredByAdminUserId = null) =>
    Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.Failed,
        reason, details, retryCount, nextRetryAt, null, triggeredByAdminUserId);

public static WikidataCoverEnrichmentAttempt RecordDeadLetter(
    Guid sharedGameId, string reason, string? details, int retryCount,
    DateTime attemptedAt,
    Guid? triggeredByAdminUserId = null) =>
    Create(sharedGameId, attemptedAt, WikidataCoverEnrichmentOutcome.DeadLetter,
        reason, details, retryCount, null, attemptedAt, triggeredByAdminUserId);
```

5. Update `Reconstitute(...)` signature:
```csharp
public static WikidataCoverEnrichmentAttempt Reconstitute(
    Guid id, Guid sharedGameId, DateTime attemptedAt,
    WikidataCoverEnrichmentOutcome outcome, string reason, string? details,
    int retryCount, DateTime? nextRetryAt, DateTime? deadLetteredAt,
    DateTime? acknowledgedAt, Guid? acknowledgedBy,
    Guid? triggeredByAdminUserId) =>
    new(id, sharedGameId, attemptedAt, outcome, reason, details,
        retryCount, nextRetryAt, deadLetteredAt,
        acknowledgedAt, acknowledgedBy, triggeredByAdminUserId);
```

- [ ] **Step 4: Run all aggregate tests + verify Task 1.1 still passes**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttempt" --no-restore`
Expected: PASS (Task 1.1 6 tests + Task 1.2 3 tests = 9 total).

If `Reconstitute(...)` callers in `WikidataCoverEnrichmentAttemptRepository.Map` break compile, Task 2.3 (Repository update) will fix them properly — but compile MUST succeed here for the test to run. If it doesn't, temporarily add `acknowledgedAt: null, acknowledgedBy: null, triggeredByAdminUserId: null` in the existing `Map(...)` call to unblock; Task 2.3 will overwrite that.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttempt.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Aggregates/WikidataCoverEnrichmentAttemptTriggerSourceTests.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/WikidataCoverEnrichmentAttemptRepository.cs
git commit -m "feat(catalog-be): #2255 TriggeredByAdminUserId factory param + Reconstitute"
```

---

## Phase 2 — Backend Infrastructure (Entity + EF + Migration + Repo)

### Task 2.1: Entity properties + EF config columns/index

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/WikidataCoverEnrichmentAttemptEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/WikidataCoverEnrichmentAttemptEntityConfiguration.cs`

- [ ] **Step 1: Write the failing test (entity config test via DbContext)**

`apps/api/tests/Api.Tests/Infrastructure/EntityConfigurations/SharedGameCatalog/WikidataCoverEnrichmentAttemptEntityConfigurationTests.cs` (new):

```csharp
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Api.Tests.Infrastructure.EntityConfigurations.SharedGameCatalog;

public class WikidataCoverEnrichmentAttemptEntityConfigurationTests
{
    [Fact]
    public void EntityModel_Has_PhaseF_ColumnsAndIndex()
    {
        using var ctx = TestDbContextFactory.CreateInMemory();

        var entityType = ctx.Model.FindEntityType(typeof(WikidataCoverEnrichmentAttemptEntity))!;
        var props = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();

        props.Should().Contain(new[] { "acknowledged_at", "acknowledged_by", "triggered_by_admin_user_id" });

        var idx = entityType.GetIndexes().FirstOrDefault(i =>
            i.GetDatabaseName() == "ix_wikidata_cover_attempts_acknowledged_at");
        idx.Should().NotBeNull("partial index required for fast 'exclude acked' default list");
        idx!.GetFilter().Should().Be("acknowledged_at IS NOT NULL");
    }
}
```

> **Note:** if `TestDbContextFactory` does not exist (verify with `find apps/api/tests -name 'TestDbContextFactory*'`), inline the DbContext creation:
> ```csharp
> var opts = new DbContextOptionsBuilder<MeepleAiDbContext>()
>     .UseInMemoryDatabase($"phasef-{Guid.NewGuid()}")
>     .Options;
> using var ctx = new MeepleAiDbContext(opts);
> ```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttemptEntityConfigurationTests" --no-restore`
Expected: FAIL (columns missing, no `acknowledged_*` property).

- [ ] **Step 3: Write minimal implementation**

Edit `WikidataCoverEnrichmentAttemptEntity.cs` — add 3 properties after `DeadLetteredAt`:

```csharp
/// <summary>F5 — UTC timestamp when an operator acknowledged the dead-letter; null otherwise.</summary>
public DateTime? AcknowledgedAt { get; set; }

/// <summary>F5 — User id of the operator who acknowledged; null otherwise.</summary>
public Guid? AcknowledgedBy { get; set; }

/// <summary>F6 — Admin user id when triggered via M12 or F2; null for M9 scheduler.</summary>
public Guid? TriggeredByAdminUserId { get; set; }
```

Edit `WikidataCoverEnrichmentAttemptEntityConfiguration.cs` — add 3 column mappings + 1 partial index after the existing config (before the closing brace):

```csharp
builder.Property(e => e.AcknowledgedAt)
    .HasColumnName("acknowledged_at")
    .IsRequired(false);

builder.Property(e => e.AcknowledgedBy)
    .HasColumnName("acknowledged_by")
    .IsRequired(false);

builder.Property(e => e.TriggeredByAdminUserId)
    .HasColumnName("triggered_by_admin_user_id")
    .IsRequired(false);

// F5 partial index: speeds up default list view (exclude acked = WHERE acknowledged_at IS NULL)
builder.HasIndex(e => e.AcknowledgedAt)
    .HasDatabaseName("ix_wikidata_cover_attempts_acknowledged_at")
    .HasFilter("acknowledged_at IS NOT NULL");
```

- [ ] **Step 4: Run test to verify it passes**

Run: same as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/WikidataCoverEnrichmentAttemptEntity.cs \
        apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/WikidataCoverEnrichmentAttemptEntityConfiguration.cs \
        apps/api/tests/Api.Tests/Infrastructure/EntityConfigurations/SharedGameCatalog/WikidataCoverEnrichmentAttemptEntityConfigurationTests.cs
git commit -m "feat(catalog-be): #2254+#2255 entity columns + acknowledged_at partial index"
```

---

### Task 2.2: EF Core migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/2026XXXXXXXX_AddAcknowledgeAndTriggerSourceToWikidataAttempts.cs` (auto-generated)
- Create: `apps/api/src/Api/Infrastructure/Migrations/2026XXXXXXXX_AddAcknowledgeAndTriggerSourceToWikidataAttempts.Designer.cs` (auto-generated)
- Modify: `apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs` (auto-updated)

- [ ] **Step 1: Generate migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddAcknowledgeAndTriggerSourceToWikidataAttempts --no-build`
Expected: 3 files created/modified above.

- [ ] **Step 2: Inspect generated SQL**

Open the new `*_AddAcknowledgeAndTriggerSourceToWikidataAttempts.cs` and verify `Up()` contains exactly:
- `migrationBuilder.AddColumn<DateTime>(name: "acknowledged_at", ...)` (nullable)
- `migrationBuilder.AddColumn<Guid>(name: "acknowledged_by", ...)` (nullable)
- `migrationBuilder.AddColumn<Guid>(name: "triggered_by_admin_user_id", ...)` (nullable)
- `migrationBuilder.CreateIndex(name: "ix_wikidata_cover_attempts_acknowledged_at", ..., filter: "acknowledged_at IS NOT NULL")`

If the index filter is missing (EF sometimes drops it), manually add `, filter: "acknowledged_at IS NOT NULL"` to the `CreateIndex` call.

- [ ] **Step 3: Verify migration applies cleanly on a fresh DB**

Run: `cd apps/api/src/Api && dotnet ef database update --no-build --connection "Host=localhost;Database=meepleai_dev;Username=postgres;Password=postgres"`
Expected: `Done.` (no errors). If you're not running Postgres locally, skip this step — Phase 6 IT will exercise it via Testcontainers.

- [ ] **Step 4: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -20`
Expected: build OK.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(catalog-be): #2254+#2255 migration — 3 columns + acknowledged_at partial index"
```

---

### Task 2.3: Repository update — `AddAsync`, `Map`, extend query signatures

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/IWikidataCoverEnrichmentAttemptRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/WikidataCoverEnrichmentAttemptRepository.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/WikidataCoverEnrichmentAttemptRepositoryIntegrationTests.cs` (modify — add 4 IT tests)

- [ ] **Step 1: Write the failing IT tests**

Append to `WikidataCoverEnrichmentAttemptRepositoryIntegrationTests.cs` (use existing fixture pattern):

```csharp
[Fact]
public async Task GetDeadLettersAsync_DefaultExcludesAcknowledgedRows()
{
    // Arrange: seed 2 dead-letters, ack the first
    await using var fixture = await IntegrationFixture.CreateAsync();
    var g1 = await fixture.SeedGameAsync("G1");
    var g2 = await fixture.SeedGameAsync("G2");
    var dl1 = WikidataCoverEnrichmentAttempt.RecordDeadLetter(g1, "r2-upload-error", null, 3, DateTime.UtcNow);
    var dl2 = WikidataCoverEnrichmentAttempt.RecordDeadLetter(g2, "r2-upload-error", null, 3, DateTime.UtcNow);
    dl1.Acknowledge(Guid.NewGuid(), DateTime.UtcNow);
    await fixture.Repo.AddAsync(dl1);
    await fixture.Repo.AddAsync(dl2);
    await fixture.SaveAsync();

    // Act
    var page = await fixture.Repo.GetDeadLettersAsync(
        skip: 0, take: 10, reasonFilter: null,
        includeAcknowledged: false, ct: default);

    // Assert
    page.TotalCount.Should().Be(1);
    page.Items.Should().HaveCount(1);
    page.Items[0].SharedGameId.Should().Be(g2);
}

[Fact]
public async Task GetDeadLettersAsync_IncludeAcknowledgedTrue_IncludesThem()
{
    await using var fixture = await IntegrationFixture.CreateAsync();
    var g1 = await fixture.SeedGameAsync("G1");
    var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(g1, "r2-upload-error", null, 3, DateTime.UtcNow);
    dl.Acknowledge(Guid.NewGuid(), DateTime.UtcNow);
    await fixture.Repo.AddAsync(dl);
    await fixture.SaveAsync();

    var page = await fixture.Repo.GetDeadLettersAsync(
        skip: 0, take: 10, reasonFilter: null,
        includeAcknowledged: true, ct: default);

    page.TotalCount.Should().Be(1);
    page.Items.Should().HaveCount(1);
    page.Items[0].AcknowledgedAt.Should().NotBeNull();
    page.Items[0].AcknowledgedBy.Should().NotBeNull();
}

[Fact]
public async Task GetDeadLettersAsync_PopulatesAcknowledgedByFullName_FromUsersJoin()
{
    await using var fixture = await IntegrationFixture.CreateAsync();
    var (adminId, _) = await fixture.SeedUserAsync("Alice Admin");
    var g1 = await fixture.SeedGameAsync("G1");
    var dl = WikidataCoverEnrichmentAttempt.RecordDeadLetter(g1, "r2-upload-error", null, 3, DateTime.UtcNow);
    dl.Acknowledge(adminId, DateTime.UtcNow);
    await fixture.Repo.AddAsync(dl);
    await fixture.SaveAsync();

    var page = await fixture.Repo.GetDeadLettersAsync(0, 10, null, includeAcknowledged: true, default);

    page.Items[0].AcknowledgedByFullName.Should().Be("Alice Admin");
}

[Fact]
public async Task GetAttemptsByGameIdAsync_PopulatesTriggeredByAdminFullName()
{
    await using var fixture = await IntegrationFixture.CreateAsync();
    var (adminId, _) = await fixture.SeedUserAsync("Bob Admin");
    var g1 = await fixture.SeedGameAsync("G1");
    var a = WikidataCoverEnrichmentAttempt.RecordSuccess(g1, 0, DateTime.UtcNow, triggeredByAdminUserId: adminId);
    await fixture.Repo.AddAsync(a);
    await fixture.SaveAsync();

    var rows = await fixture.Repo.GetAttemptsByGameIdAsync(g1, limit: 50, default);

    rows.Should().HaveCount(1);
    rows[0].TriggeredByAdminUserId.Should().Be(adminId);
    rows[0].TriggeredByAdminFullName.Should().Be("Bob Admin");
}
```

> **Note:** the existing `IntegrationFixture` class in the same test file already wraps `WikidataCoverEnrichmentAttemptRepository`. If `SeedGameAsync` / `SeedUserAsync` / `Repo` / `SaveAsync` helpers don't exist, add them by following the pattern from `WikidataCoverEnrichmentAttemptRepositoryIntegrationTests.cs` existing setup helpers. The new `GetAttemptsByGameIdAsync` overload returns `IReadOnlyList<WikidataAttemptTimelineRow>` (record with TriggeredByAdminUserId + TriggeredByAdminFullName) — not the domain aggregate; this is the F6 query DTO. Update fixture's `Repo` type to match.

- [ ] **Step 2: Run failing IT tests**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttemptRepositoryIntegrationTests" --no-restore`
Expected: FAIL — `GetDeadLettersAsync` signature missing `includeAcknowledged`, `DeadLetterRow` missing `AcknowledgedAt/By/FullName`, etc.

- [ ] **Step 3: Update repository interface + impl**

In `IWikidataCoverEnrichmentAttemptRepository.cs`:

```csharp
// Replace existing GetDeadLettersAsync signature
Task<DeadLetterPage> GetDeadLettersAsync(
    int skip, int take, string? reasonFilter,
    bool includeAcknowledged,                              // F5 NEW
    CancellationToken cancellationToken = default);

// Replace existing GetAttemptsByGameIdAsync return type
Task<IReadOnlyList<WikidataAttemptTimelineRow>> GetAttemptsByGameIdAsync(
    Guid sharedGameId, int limit,
    CancellationToken cancellationToken = default);

// Add new bulk lookup for F5 handler
Task<IReadOnlyDictionary<Guid, WikidataCoverEnrichmentAttempt>> GetByIdsAsync(
    IReadOnlyCollection<Guid> attemptIds,
    CancellationToken cancellationToken = default);

// Add new persist method (handler calls aggregate.Acknowledge → repo.UpdateAsync)
Task UpdateAsync(
    WikidataCoverEnrichmentAttempt attempt,
    CancellationToken cancellationToken = default);
```

Extend `DeadLetterRow` record:
```csharp
public sealed record DeadLetterRow(
    Guid Id, Guid SharedGameId, string GameTitle,
    DateTime AttemptedAt, DateTime DeadLetteredAt,
    string Reason, string? Details, int RetryCount,
    // F5
    DateTime? AcknowledgedAt,
    Guid?     AcknowledgedBy,
    string?   AcknowledgedByFullName,
    // F6
    Guid?     TriggeredByAdminUserId,
    string?   TriggeredByAdminFullName);
```

Add new record for F3 timeline rows with F6 fields:
```csharp
/// <summary>F3 timeline row with F6 admin attribution. Mirrors the aggregate
/// shape needed by the admin drawer (does not need full Reconstitute).</summary>
public sealed record WikidataAttemptTimelineRow(
    Guid Id, DateTime AttemptedAt, WikidataCoverEnrichmentOutcome Outcome,
    string Reason, string? Details, int RetryCount,
    DateTime? NextRetryAt, DateTime? DeadLetteredAt,
    Guid?   TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);
```

In `WikidataCoverEnrichmentAttemptRepository.cs`:

1. Update `AddAsync` to persist 3 new fields:
```csharp
await DbContext.WikidataCoverEnrichmentAttempts.AddAsync(new()
{
    Id = attempt.Id,
    SharedGameId = attempt.SharedGameId,
    AttemptedAt = attempt.AttemptedAt,
    Outcome = (int)attempt.Outcome,
    Reason = attempt.Reason,
    Details = attempt.Details,
    RetryCount = attempt.RetryCount,
    NextRetryAt = attempt.NextRetryAt,
    DeadLetteredAt = attempt.DeadLetteredAt,
    AcknowledgedAt = attempt.AcknowledgedAt,
    AcknowledgedBy = attempt.AcknowledgedBy,
    TriggeredByAdminUserId = attempt.TriggeredByAdminUserId,
}, cancellationToken).ConfigureAwait(false);
```

2. Update `Map(entity)` to pass all 3 new fields to `Reconstitute`:
```csharp
private static WikidataCoverEnrichmentAttempt Map(WikidataCoverEnrichmentAttemptEntity entity) =>
    WikidataCoverEnrichmentAttempt.Reconstitute(
        id: entity.Id,
        sharedGameId: entity.SharedGameId,
        attemptedAt: entity.AttemptedAt,
        outcome: (WikidataCoverEnrichmentOutcome)entity.Outcome,
        reason: entity.Reason,
        details: entity.Details,
        retryCount: entity.RetryCount,
        nextRetryAt: entity.NextRetryAt,
        deadLetteredAt: entity.DeadLetteredAt,
        acknowledgedAt: entity.AcknowledgedAt,
        acknowledgedBy: entity.AcknowledgedBy,
        triggeredByAdminUserId: entity.TriggeredByAdminUserId);
```

3. Replace `GetDeadLettersAsync` body — add `includeAcknowledged` filter + LEFT JOIN users x2:
```csharp
public async Task<DeadLetterPage> GetDeadLettersAsync(
    int skip, int take, string? reasonFilter,
    bool includeAcknowledged,
    CancellationToken cancellationToken = default)
{
    if (skip < 0) skip = 0;
    if (take < 1) take = 1;
    if (take > 200) take = 200;

    const int DeadLetterOutcome = (int)WikidataCoverEnrichmentOutcome.DeadLetter;

    var query =
        from a in DbContext.WikidataCoverEnrichmentAttempts.AsNoTracking()
        where a.DeadLetteredAt != null && a.Outcome == DeadLetterOutcome
        select a;

    if (!includeAcknowledged)
        query = query.Where(a => a.AcknowledgedAt == null);

    if (!string.IsNullOrWhiteSpace(reasonFilter))
        query = query.Where(a => a.Reason == reasonFilter);

    var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
    if (totalCount == 0)
        return new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0);

    var pageQuery =
        from a in query.OrderByDescending(x => x.DeadLetteredAt).Skip(skip).Take(take)
        join sg in DbContext.SharedGames.AsNoTracking().IgnoreQueryFilters()
            on a.SharedGameId equals sg.Id into sgs
        from sg in sgs.DefaultIfEmpty()
        join ackUser in DbContext.Users.AsNoTracking()
            on a.AcknowledgedBy equals ackUser.Id into ackUsers
        from ackUser in ackUsers.DefaultIfEmpty()
        join trgUser in DbContext.Users.AsNoTracking()
            on a.TriggeredByAdminUserId equals trgUser.Id into trgUsers
        from trgUser in trgUsers.DefaultIfEmpty()
        select new DeadLetterRow(
            a.Id, a.SharedGameId,
            sg != null ? sg.Title : "(deleted game)",
            a.AttemptedAt, a.DeadLetteredAt!.Value,
            a.Reason, a.Details, a.RetryCount,
            a.AcknowledgedAt, a.AcknowledgedBy,
            ackUser != null ? ackUser.FullName : null,
            a.TriggeredByAdminUserId,
            trgUser != null ? trgUser.FullName : null);

    var items = await pageQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
    return new DeadLetterPage(items, totalCount);
}
```

> **NOTE:** if `DbContext.Users` doesn't expose `FullName`, search the existing user entity (`grep -rn 'FullName\|DisplayName' apps/api/src/Api/Infrastructure/Entities/Identity/`) and use the actual property name. Fall back to `ackUser.Email` if no display name field exists — the badge tooltip then shows email instead.

4. Replace `GetAttemptsByGameIdAsync` body — return `WikidataAttemptTimelineRow` with F6 join:
```csharp
public async Task<IReadOnlyList<WikidataAttemptTimelineRow>> GetAttemptsByGameIdAsync(
    Guid sharedGameId, int limit,
    CancellationToken cancellationToken = default)
{
    limit = Math.Clamp(limit, 1, 200);

    var query =
        from a in DbContext.WikidataCoverEnrichmentAttempts.AsNoTracking()
        where a.SharedGameId == sharedGameId
        orderby a.AttemptedAt descending
        join u in DbContext.Users.AsNoTracking()
            on a.TriggeredByAdminUserId equals u.Id into us
        from u in us.DefaultIfEmpty()
        select new WikidataAttemptTimelineRow(
            a.Id, a.AttemptedAt,
            (WikidataCoverEnrichmentOutcome)a.Outcome,
            a.Reason, a.Details, a.RetryCount,
            a.NextRetryAt, a.DeadLetteredAt,
            a.TriggeredByAdminUserId,
            u != null ? u.FullName : null);

    return await query.Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
}
```

5. Add new methods `GetByIdsAsync` + `UpdateAsync`:
```csharp
public async Task<IReadOnlyDictionary<Guid, WikidataCoverEnrichmentAttempt>> GetByIdsAsync(
    IReadOnlyCollection<Guid> attemptIds,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(attemptIds);
    if (attemptIds.Count == 0) return new Dictionary<Guid, WikidataCoverEnrichmentAttempt>();
    var ids = attemptIds.Distinct().Take(50).ToArray();
    var entities = await DbContext.WikidataCoverEnrichmentAttempts
        .Where(a => ids.Contains(a.Id))
        .ToListAsync(cancellationToken).ConfigureAwait(false);
    return entities.ToDictionary(e => e.Id, Map);
}

public async Task UpdateAsync(
    WikidataCoverEnrichmentAttempt attempt,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(attempt);
    var entity = await DbContext.WikidataCoverEnrichmentAttempts
        .FirstOrDefaultAsync(e => e.Id == attempt.Id, cancellationToken)
        .ConfigureAwait(false)
        ?? throw new InvalidOperationException(
            $"WikidataCoverEnrichmentAttempt {attempt.Id} not found for update.");

    entity.AcknowledgedAt = attempt.AcknowledgedAt;
    entity.AcknowledgedBy = attempt.AcknowledgedBy;
    // Note: only ack metadata is mutable per F5 spec; other fields stay frozen
    // per the record-of-fact pattern.
    CollectDomainEvents(attempt);
}
```

6. **Update existing callers**: `WikidataCoverEnrichmentRunner.cs` (`GetLatestBySharedGameIdAsync` Map → no signature change needed) — only `Map(...)` was the call site; already covered by Step 3 update (2).

7. **F4 SSE flow**: the existing M13 query handler still calls `GetDeadLettersAsync` with old 3-arg signature. Fix by adding `includeAcknowledged: false` to existing call site in `GetWikidataDeadLetterAttemptsQuery.cs` Handler (Task 3.3 will do this — but bridge by updating the existing handler call here so build doesn't break):
```csharp
var page = await _attempts.GetDeadLettersAsync(
    skip, take, request.ReasonFilter,
    includeAcknowledged: false, cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 4: Run IT tests + verify build**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentAttemptRepositoryIntegrationTests" --no-restore`
Expected: PASS (4 new tests + existing IT tests still green).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/ \
        apps/api/tests/Api.Tests/Integration/SharedGameCatalog/WikidataCoverEnrichmentAttemptRepositoryIntegrationTests.cs
git commit -m "feat(catalog-be): #2254+#2255 repo — includeAcknowledged + JOIN users + GetByIds/UpdateAsync"
```

---

## Phase 3 — Backend Application (CQRS)

### Task 3.1: Command + Validator F5

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkAcknowledgeWikidataCoverCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Validators/AdminBulkAcknowledgeWikidataCoverCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Validators/AdminBulkAcknowledgeWikidataCoverCommandValidatorTests.cs` (new)

- [ ] **Step 1: Write the failing validator test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

public class AdminBulkAcknowledgeWikidataCoverCommandValidatorTests
{
    private readonly AdminBulkAcknowledgeWikidataCoverCommandValidator _v = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Empty_AttemptIds_Fails()
    {
        var r = _v.TestValidate(new AdminBulkAcknowledgeWikidataCoverCommand(
            AttemptIds: Array.Empty<Guid>(), Note: null, TriggeredByUserId: _userId));
        r.ShouldHaveValidationErrorFor(x => x.AttemptIds);
    }

    [Fact]
    public void Over_50_AttemptIds_Fails()
    {
        var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList();
        var r = _v.TestValidate(new AdminBulkAcknowledgeWikidataCoverCommand(
            ids, null, _userId));
        r.ShouldHaveValidationErrorFor(x => x.AttemptIds);
    }

    [Fact]
    public void Note_Over_500Chars_Fails()
    {
        var r = _v.TestValidate(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { Guid.NewGuid() }, Note: new string('a', 501), TriggeredByUserId: _userId));
        r.ShouldHaveValidationErrorFor(x => x.Note);
    }

    [Fact]
    public void TriggeredByUserId_Empty_Fails()
    {
        var r = _v.TestValidate(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { Guid.NewGuid() }, null, Guid.Empty));
        r.ShouldHaveValidationErrorFor(x => x.TriggeredByUserId);
    }

    [Fact]
    public void Valid_Command_NoteNull_Passes()
    {
        var r = _v.TestValidate(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { Guid.NewGuid() }, null, _userId));
        r.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_Command_NoteUnder500_Passes()
    {
        var r = _v.TestValidate(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { Guid.NewGuid() }, new string('a', 500), _userId));
        r.ShouldNotHaveAnyValidationErrors();
    }
}
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminBulkAcknowledgeWikidataCoverCommandValidatorTests" --no-restore`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Create command + validator**

`AdminBulkAcknowledgeWikidataCoverCommand.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Phase F F5 — admin bulk-acknowledge of one or more dead-letter
/// attempts. Operator marks a row as "not actionable" so it disappears from
/// the default list view without waiting for the DEC-3j 7-day retention sweep.
/// Idempotent: re-acknowledging an already-acked row is a no-op.
/// </summary>
/// <param name="AttemptIds">Dead-letter attempt ids selected on the admin page (max 50).</param>
/// <param name="Note">Optional free-text note shown in the confirmation modal; persisted log-only (DEC-F-4).</param>
/// <param name="TriggeredByUserId">Acknowledging admin user id (log-only).</param>
internal sealed record AdminBulkAcknowledgeWikidataCoverCommand(
    IReadOnlyList<Guid> AttemptIds,
    string? Note,
    Guid TriggeredByUserId) : ICommand<AdminBulkAcknowledgeResult>;

public sealed record AdminBulkAcknowledgeRow(
    Guid AttemptId, Guid? GameId, string Outcome, string? Reason)
{
    public const string OutcomeAcked = "acked";
    public const string OutcomeAlreadyAcked = "already-acked";
    public const string OutcomeNotFound = "not-found";
    public const string OutcomeWrongState = "wrong-state";
}

public sealed record AdminBulkAcknowledgeResult(
    int AckedCount,
    int IdempotentNoOpCount,
    int NotFoundCount,
    IReadOnlyList<AdminBulkAcknowledgeRow> Rows);
```

`AdminBulkAcknowledgeWikidataCoverCommandValidator.cs`:
```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

internal sealed class AdminBulkAcknowledgeWikidataCoverCommandValidator
    : AbstractValidator<AdminBulkAcknowledgeWikidataCoverCommand>
{
    public const int MaxBatchSize = 50;
    public const int MaxNoteLength = 500;

    public AdminBulkAcknowledgeWikidataCoverCommandValidator()
    {
        RuleFor(x => x.AttemptIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().NotEmpty()
            .WithMessage("AttemptIds must contain at least one id.")
            .Must(ids => ids.Count <= MaxBatchSize)
            .WithMessage($"AttemptIds cannot exceed {MaxBatchSize} per request.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("AttemptIds must not contain Guid.Empty.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("AttemptIds must not contain duplicates.");

        RuleFor(x => x.Note)
            .MaximumLength(MaxNoteLength)
            .When(x => x.Note is not null)
            .WithMessage($"Note must be {MaxNoteLength} characters or fewer.");

        RuleFor(x => x.TriggeredByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("TriggeredByUserId must not be empty.");
    }
}
```

- [ ] **Step 4: Run failing test, expect PASS**

Run: same as Step 2. Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkAcknowledgeWikidataCoverCommand.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Validators/AdminBulkAcknowledgeWikidataCoverCommandValidator.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Validators/AdminBulkAcknowledgeWikidataCoverCommandValidatorTests.cs
git commit -m "feat(catalog-be): #2254 F5 bulk-acknowledge command + validator"
```

---

### Task 3.2: Command handler F5

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkAcknowledgeWikidataCoverCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkAcknowledgeWikidataCoverCommandHandlerTests.cs` (new)

- [ ] **Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

public class AdminBulkAcknowledgeWikidataCoverCommandHandlerTests
{
    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly TimeProvider _time = TimeProvider.System;
    private readonly Guid _user = Guid.NewGuid();

    private AdminBulkAcknowledgeWikidataCoverCommandHandler Build() =>
        new(_repo.Object, _uow.Object, _time, NullLogger<AdminBulkAcknowledgeWikidataCoverCommandHandler>.Instance);

    private static WikidataCoverEnrichmentAttempt MakeDeadLetter(Guid? id = null) =>
        WikidataCoverEnrichmentAttempt.Reconstitute(
            id ?? Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            WikidataCoverEnrichmentOutcome.DeadLetter, "r2-upload-error", null, 3,
            null, DateTime.UtcNow, null, null, null);

    [Fact]
    public async Task SingleDeadLetter_Acked_ReturnsAckedCountOne()
    {
        var dl = MakeDeadLetter();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt> { [dl.Id] = dl });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { dl.Id }, null, _user), CancellationToken.None);

        result.AckedCount.Should().Be(1);
        result.IdempotentNoOpCount.Should().Be(0);
        result.NotFoundCount.Should().Be(0);
        result.Rows.Should().ContainSingle(r =>
            r.AttemptId == dl.Id && r.Outcome == AdminBulkAcknowledgeRow.OutcomeAcked);
        dl.AcknowledgedAt.Should().NotBeNull();
        dl.AcknowledgedBy.Should().Be(_user);
        _repo.Verify(r => r.UpdateAsync(dl, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyAcked_Idempotent_NotPersistedAgain()
    {
        var dl = MakeDeadLetter();
        dl.Acknowledge(Guid.NewGuid(), DateTime.UtcNow);
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt> { [dl.Id] = dl });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { dl.Id }, null, _user), CancellationToken.None);

        result.AckedCount.Should().Be(0);
        result.IdempotentNoOpCount.Should().Be(1);
        result.Rows.Single().Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeAlreadyAcked);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<WikidataCoverEnrichmentAttempt>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotFoundId_ReportedAsNotFound()
    {
        var missing = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt>());

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { missing }, null, _user), CancellationToken.None);

        result.NotFoundCount.Should().Be(1);
        result.Rows.Single().Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeNotFound);
    }

    [Fact]
    public async Task NonDeadLetterRow_ReportedAsWrongState()
    {
        var success = WikidataCoverEnrichmentAttempt.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow,
            WikidataCoverEnrichmentOutcome.Success, "success", null, 0,
            null, null, null, null, null);
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt> { [success.Id] = success });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { success.Id }, null, _user), CancellationToken.None);

        result.AckedCount.Should().Be(0);
        result.Rows.Single().Outcome.Should().Be(AdminBulkAcknowledgeRow.OutcomeWrongState);
    }

    [Fact]
    public async Task BatchMixedOutcomes_CountsAreAggregated()
    {
        var dl1 = MakeDeadLetter();
        var dl2Already = MakeDeadLetter();
        dl2Already.Acknowledge(Guid.NewGuid(), DateTime.UtcNow);
        var missing = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WikidataCoverEnrichmentAttempt>
            {
                [dl1.Id] = dl1,
                [dl2Already.Id] = dl2Already,
            });

        var result = await Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { dl1.Id, dl2Already.Id, missing }, "operator note", _user), CancellationToken.None);

        result.AckedCount.Should().Be(1);
        result.IdempotentNoOpCount.Should().Be(1);
        result.NotFoundCount.Should().Be(1);
        result.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => Build().Handle(new AdminBulkAcknowledgeWikidataCoverCommand(
            new[] { Guid.NewGuid() }, null, _user), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminBulkAcknowledgeWikidataCoverCommandHandlerTests" --no-restore`
Expected: FAIL — handler doesn't exist.

- [ ] **Step 3: Create handler**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Phase F F5 — bulk-acknowledge dead-letter attempts. Loads each
/// id, calls <see cref="WikidataCoverEnrichmentAttempt.Acknowledge"/> with
/// idempotency in-domain, persists via repo + UoW. Per-row outcomes are bucketed
/// into the result envelope (acked / already-acked / not-found / wrong-state)
/// so the admin UI can render partial success/failure.
/// </summary>
internal sealed class AdminBulkAcknowledgeWikidataCoverCommandHandler
    : ICommandHandler<AdminBulkAcknowledgeWikidataCoverCommand, AdminBulkAcknowledgeResult>
{
    private readonly IWikidataCoverEnrichmentAttemptRepository _attempts;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminBulkAcknowledgeWikidataCoverCommandHandler> _logger;

    public AdminBulkAcknowledgeWikidataCoverCommandHandler(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<AdminBulkAcknowledgeWikidataCoverCommandHandler> logger)
    {
        _attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminBulkAcknowledgeResult> Handle(
        AdminBulkAcknowledgeWikidataCoverCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "AdminBulkAcknowledgeWikidataCover: user {UserId} acking {Count} attempt(s); note={Note}",
            request.TriggeredByUserId, request.AttemptIds.Count, request.Note ?? "<none>");

        var loaded = await _attempts
            .GetByIdsAsync(request.AttemptIds, cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<AdminBulkAcknowledgeRow>(request.AttemptIds.Count);
        var acked = 0;
        var alreadyAcked = 0;
        var notFound = 0;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var anyUpdated = false;

        foreach (var id in request.AttemptIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!loaded.TryGetValue(id, out var attempt))
            {
                rows.Add(new(id, null, AdminBulkAcknowledgeRow.OutcomeNotFound, "attempt-id-not-found"));
                notFound++;
                continue;
            }

            if (attempt.Outcome != WikidataCoverEnrichmentOutcome.DeadLetter)
            {
                rows.Add(new(id, attempt.SharedGameId, AdminBulkAcknowledgeRow.OutcomeWrongState,
                    $"current-outcome:{attempt.Outcome}"));
                continue;
            }

            if (attempt.AcknowledgedAt is not null)
            {
                rows.Add(new(id, attempt.SharedGameId, AdminBulkAcknowledgeRow.OutcomeAlreadyAcked, null));
                alreadyAcked++;
                continue;
            }

            attempt.Acknowledge(request.TriggeredByUserId, nowUtc);
            await _attempts.UpdateAsync(attempt, cancellationToken).ConfigureAwait(false);
            rows.Add(new(id, attempt.SharedGameId, AdminBulkAcknowledgeRow.OutcomeAcked, null));
            acked++;
            anyUpdated = true;
        }

        if (anyUpdated)
            await _uow.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminBulkAcknowledgeResult(
            AckedCount: acked,
            IdempotentNoOpCount: alreadyAcked,
            NotFoundCount: notFound,
            Rows: rows);
    }
}
```

- [ ] **Step 4: Run tests, expect PASS**

Run: same as Step 2. Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkAcknowledgeWikidataCoverCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkAcknowledgeWikidataCoverCommandHandlerTests.cs
git commit -m "feat(catalog-be): #2254 F5 bulk-acknowledge handler with per-row outcomes"
```

---

### Task 3.3: Query F5 update — `GetWikidataDeadLetterAttemptsQuery`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataDeadLetterAttemptsQuery.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataDeadLetterAttemptsQueryHandlerTests.cs` (new or modify existing)

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

public class GetWikidataDeadLetterAttemptsQueryHandlerTests
{
    private readonly Mock<IWikidataCoverEnrichmentAttemptRepository> _repo = new();
    private GetWikidataDeadLetterAttemptsQueryHandler Build() => new(_repo.Object);

    [Fact]
    public async Task DefaultIncludeAcknowledgedFalse_PassedToRepo()
    {
        _repo.Setup(r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        var query = new GetWikidataDeadLetterAttemptsQuery(0, 50, null, IncludeAcknowledged: false);
        await Build().Handle(query, CancellationToken.None);

        _repo.Verify(r => r.GetDeadLettersAsync(0, 50, null, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IncludeAcknowledgedTrue_PassedThrough()
    {
        _repo.Setup(r => r.GetDeadLettersAsync(0, 50, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0));

        var query = new GetWikidataDeadLetterAttemptsQuery(0, 50, null, IncludeAcknowledged: true);
        await Build().Handle(query, CancellationToken.None);

        _repo.Verify(r => r.GetDeadLettersAsync(0, 50, null, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DtoIncludesAckAndAdminFields()
    {
        var row = new DeadLetterRow(
            Guid.NewGuid(), Guid.NewGuid(), "Game", DateTime.UtcNow, DateTime.UtcNow,
            "r2-upload-error", null, 3,
            AcknowledgedAt: DateTime.UtcNow, AcknowledgedBy: Guid.NewGuid(),
            AcknowledgedByFullName: "Alice", TriggeredByAdminUserId: Guid.NewGuid(),
            TriggeredByAdminFullName: "Bob");
        _repo.Setup(r => r.GetDeadLettersAsync(0, 50, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeadLetterPage(new[] { row }, 1));

        var query = new GetWikidataDeadLetterAttemptsQuery(0, 50, null, IncludeAcknowledged: true);
        var result = await Build().Handle(query, CancellationToken.None);

        result.Items.Single().AcknowledgedByFullName.Should().Be("Alice");
        result.Items.Single().TriggeredByAdminFullName.Should().Be("Bob");
    }
}
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetWikidataDeadLetterAttemptsQueryHandlerTests" --no-restore`
Expected: FAIL — `IncludeAcknowledged` keyword unknown + DTO missing fields.

- [ ] **Step 3: Update query + DTO + handler**

Edit `GetWikidataDeadLetterAttemptsQuery.cs`:

```csharp
internal sealed record GetWikidataDeadLetterAttemptsQuery(
    int Skip, int Take, string? ReasonFilter,
    bool IncludeAcknowledged = false) : IRequest<WikidataDeadLetterAttemptsResult>;

public sealed record WikidataDeadLetterAttemptDto(
    Guid Id, Guid SharedGameId, string GameTitle,
    DateTime AttemptedAt, DateTime DeadLetteredAt,
    string Reason, string? Details, int RetryCount,
    DateTime? AcknowledgedAt,
    Guid?     AcknowledgedBy,
    string?   AcknowledgedByFullName,
    Guid?     TriggeredByAdminUserId,
    string?   TriggeredByAdminFullName);
```

Update handler body:
```csharp
public async Task<WikidataDeadLetterAttemptsResult> Handle(
    GetWikidataDeadLetterAttemptsQuery request,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(request);
    var skip = Math.Max(0, request.Skip);
    var take = Math.Clamp(request.Take, 1, 200);

    var page = await _attempts
        .GetDeadLettersAsync(skip, take, request.ReasonFilter,
            request.IncludeAcknowledged, cancellationToken)
        .ConfigureAwait(false);

    var items = page.Items
        .Select(row => new WikidataDeadLetterAttemptDto(
            row.Id, row.SharedGameId, row.GameTitle,
            row.AttemptedAt, row.DeadLetteredAt,
            row.Reason, row.Details, row.RetryCount,
            row.AcknowledgedAt, row.AcknowledgedBy, row.AcknowledgedByFullName,
            row.TriggeredByAdminUserId, row.TriggeredByAdminFullName))
        .ToList();

    return new WikidataDeadLetterAttemptsResult(items, page.TotalCount, skip, take);
}
```

- [ ] **Step 4: Run tests + ensure prior tests still green**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetWikidataDeadLetter" --no-restore`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataDeadLetterAttemptsQuery.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataDeadLetterAttemptsQueryHandlerTests.cs
git commit -m "feat(catalog-be): #2254+#2255 query DTO — includeAcknowledged + ack/admin name fields"
```

---

### Task 3.4: Query F6 timeline DTO update

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataAttemptTimelineQuery.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataAttemptTimelineQueryHandlerTests.cs` (new)

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

public class GetWikidataAttemptTimelineQueryHandlerTests
{
    [Fact]
    public async Task Handler_MapsTriggeredByAdminFullName_FromRepoRow()
    {
        var repo = new Mock<IWikidataCoverEnrichmentAttemptRepository>();
        var adminId = Guid.NewGuid();
        var row = new WikidataAttemptTimelineRow(
            Id: Guid.NewGuid(), AttemptedAt: DateTime.UtcNow,
            Outcome: WikidataCoverEnrichmentOutcome.Success, Reason: "success", Details: null,
            RetryCount: 0, NextRetryAt: null, DeadLetteredAt: null,
            TriggeredByAdminUserId: adminId, TriggeredByAdminFullName: "Carol");
        repo.Setup(r => r.GetAttemptsByGameIdAsync(It.IsAny<Guid>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { row });

        var handler = new GetWikidataAttemptTimelineQueryHandler(repo.Object);
        var result = await handler.Handle(new GetWikidataAttemptTimelineQuery(Guid.NewGuid(), 50), default);

        result.Items.Single().TriggeredByAdminUserId.Should().Be(adminId);
        result.Items.Single().TriggeredByAdminFullName.Should().Be("Carol");
    }
}
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetWikidataAttemptTimelineQueryHandlerTests" --no-restore`
Expected: FAIL — `WikidataAttemptTimelineNode` missing new fields.

- [ ] **Step 3: Update DTO + handler**

Edit `GetWikidataAttemptTimelineQuery.cs`:

```csharp
public sealed record WikidataAttemptTimelineNode(
    Guid Id, DateTime AttemptedAt, string Outcome,
    string? Reason, string? Details, int RetryCount,
    DateTime? NextRetryAt, DateTime? DeadLetteredAt,
    Guid?   TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);
```

Update handler `Handle` body (the repo now returns `WikidataAttemptTimelineRow` instead of aggregates):
```csharp
public async Task<WikidataAttemptTimelineResult> Handle(
    GetWikidataAttemptTimelineQuery request,
    CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(request);

    var rows = await _attempts
        .GetAttemptsByGameIdAsync(request.GameId, request.Limit, cancellationToken)
        .ConfigureAwait(false);

    var items = rows
        .Select(r => new WikidataAttemptTimelineNode(
            r.Id, r.AttemptedAt, r.Outcome.ToString(),
            r.Reason, r.Details, r.RetryCount,
            r.NextRetryAt, r.DeadLetteredAt,
            r.TriggeredByAdminUserId, r.TriggeredByAdminFullName))
        .ToList();

    return new WikidataAttemptTimelineResult(request.GameId, items);
}
```

- [ ] **Step 4: Run tests, expect PASS**

Run: same as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataAttemptTimelineQuery.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataAttemptTimelineQueryHandlerTests.cs
git commit -m "feat(catalog-be): #2255 F6 timeline DTO with TriggeredByAdmin fields"
```

---

### Task 3.5: Runner F6 — interface + impl + event payload

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IWikidataCoverEnrichmentRunner.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunner.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IWikidataEnrichmentEventBroadcaster.cs` (extend `WikidataEnrichmentEvent` record)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminEnrichWikidataCoverCommandHandler.cs` (M12 caller)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkRetryWikidataCoverCommandHandler.cs` (F2 caller)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/WikidataCoverEnrichmentJob.cs` (M9 caller — passes `null`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunnerTests.cs` (new or extend)

- [ ] **Step 1: Write failing tests**

`WikidataCoverEnrichmentRunnerTests.cs` (extend existing or create):

```csharp
[Fact]
public async Task EnrichAndRecord_NullAdminId_PersistsNullOnAttempt()
{
    // Arrange mocks: mediator returns Success result for any gameId
    // Repo captures AddAsync attempt — assert TriggeredByAdminUserId is null
    // ... wire mocks following existing test pattern ...

    var addedAttempt = await CallRunnerAndCaptureAddedAttempt(triggeredByAdminUserId: null);

    addedAttempt.TriggeredByAdminUserId.Should().BeNull();
}

[Fact]
public async Task EnrichAndRecord_AdminTriggered_PersistsAdminIdOnAttempt()
{
    var adminId = Guid.NewGuid();
    var addedAttempt = await CallRunnerAndCaptureAddedAttempt(triggeredByAdminUserId: adminId);

    addedAttempt.TriggeredByAdminUserId.Should().Be(adminId);
}

[Fact]
public async Task EnrichAndRecord_PublishesEventWithTriggeredByAdmin()
{
    var adminId = Guid.NewGuid();
    var captured = await CallRunnerAndCaptureBroadcastEvent(triggeredByAdminUserId: adminId);

    captured.TriggeredByAdminUserId.Should().Be(adminId);
    captured.TriggeredByAdminFullName.Should().BeNull();
    // NOTE: TriggeredByAdminFullName is populated in the BROADCAST payload only
    // if the runner enriches via a user lookup. Per DEC-F-5 we resolve in the
    // QUERY path (GetDeadLettersAsync + GetAttemptsByGameIdAsync JOINs), and
    // the broadcaster pushes the slim attempt projection — so the FE refresh
    // on SSE will re-fetch the page (already wired in F4) and pick up the name
    // via the next listDeadLetters call. Hence we assert FullName is NULL here.
}
```

> **Note:** the helper methods `CallRunnerAndCaptureAddedAttempt` / `CallRunnerAndCaptureBroadcastEvent` MUST be added in the test file. They wire `IMediator` (returns `EnrichCatalogCoverResult.Success`), `IWikidataCoverEnrichmentAttemptRepository`, `IUnitOfWork`, `IWikidataCoverEnrichmentRetryPolicy` (returns `Terminal` for Success), `IWikidataEnrichmentEventBroadcaster` (captures `Publish`), and `TimeProvider`. Look at the existing test file to find the existing helper pattern and extend.

- [ ] **Step 2: Run failing test**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentRunnerTests" --no-restore`
Expected: FAIL — signature missing `triggeredByAdminUserId`.

- [ ] **Step 3: Apply implementation changes**

`IWikidataCoverEnrichmentRunner.cs`:
```csharp
Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
    Guid gameId,
    bool forceRefresh,
    Guid? triggeredByAdminUserId = null,
    CancellationToken cancellationToken = default);
```

`IWikidataEnrichmentEventBroadcaster.cs` — extend `WikidataEnrichmentEvent`:
```csharp
public sealed record WikidataEnrichmentEvent(
    Guid AttemptId, Guid SharedGameId,
    string Outcome, string? Reason,
    DateTime AttemptedAt, int RetryCount,
    DateTime? NextRetryAt, DateTime? DeadLetteredAt,
    Guid?   TriggeredByAdminUserId,
    string? TriggeredByAdminFullName);
```

`WikidataCoverEnrichmentRunner.cs`:
1. Update signature:
```csharp
public async Task<EnrichCatalogCoverResult> EnrichAndRecordAsync(
    Guid gameId, bool forceRefresh,
    Guid? triggeredByAdminUserId = null,
    CancellationToken cancellationToken = default)
```
2. Pass `triggeredByAdminUserId` into each factory call:
```csharp
(WikidataCoverEnrichmentRetryDecision.Terminal, EnrichCatalogCoverResult.Success) =>
    WikidataCoverEnrichmentAttempt.RecordSuccess(gameId, nextRetryCount, nowUtc, triggeredByAdminUserId),

(WikidataCoverEnrichmentRetryDecision.Terminal, EnrichCatalogCoverResult.Skipped skipped) =>
    WikidataCoverEnrichmentAttempt.RecordSkipped(gameId, skipped.Reason, nextRetryCount, nowUtc, triggeredByAdminUserId),

(WikidataCoverEnrichmentRetryDecision.ScheduleRetry retry, EnrichCatalogCoverResult.Failed failed) =>
    WikidataCoverEnrichmentAttempt.RecordFailedWithRetry(
        gameId, failed.Reason, failed.Details, nextRetryCount, nowUtc, retry.NextRetryAt,
        triggeredByAdminUserId),

(WikidataCoverEnrichmentRetryDecision.DeadLetter, EnrichCatalogCoverResult.Failed failed) =>
    WikidataCoverEnrichmentAttempt.RecordDeadLetter(
        gameId, failed.Reason, failed.Details, nextRetryCount, nowUtc, triggeredByAdminUserId),

_ => WikidataCoverEnrichmentAttempt.RecordDeadLetter(
    gameId, "unexpected-decision",
    $"{decision.GetType().Name} for {result.GetType().Name}",
    nextRetryCount, nowUtc, triggeredByAdminUserId),
```
3. Update broadcast payload (`TriggeredByAdminFullName` is null in the broadcast — resolved in the query path):
```csharp
_broadcaster.Publish(new WikidataEnrichmentEvent(
    AttemptId: newAttempt.Id,
    SharedGameId: newAttempt.SharedGameId,
    Outcome: newAttempt.Outcome.ToString(),
    Reason: newAttempt.Reason,
    AttemptedAt: newAttempt.AttemptedAt,
    RetryCount: newAttempt.RetryCount,
    NextRetryAt: newAttempt.NextRetryAt,
    DeadLetteredAt: newAttempt.DeadLetteredAt,
    TriggeredByAdminUserId: newAttempt.TriggeredByAdminUserId,
    TriggeredByAdminFullName: null));
```

Update 3 callers to pass `triggeredByAdminUserId`:

- `AdminEnrichWikidataCoverCommandHandler.cs`: change `await _runner.EnrichAndRecordAsync(command.GameId, command.ForceRefresh, ct)` → `(...).EnrichAndRecordAsync(command.GameId, command.ForceRefresh, command.TriggeredByUserId, ct)`.
- `AdminBulkRetryWikidataCoverCommandHandler.cs`: change `await _runner.EnrichAndRecordAsync(gameId, forceRefresh: true, cancellationToken)` → `(...).EnrichAndRecordAsync(gameId, forceRefresh: true, triggeredByAdminUserId: request.TriggeredByUserId, cancellationToken)`.
- `WikidataCoverEnrichmentJob.cs`: M9 cron call site stays `EnrichAndRecordAsync(gameId, false, cancellationToken)` — default `null` is correct (scheduler path).

- [ ] **Step 4: Run all runner + caller tests**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCoverEnrichmentRunnerTests|FullyQualifiedName~AdminEnrichWikidataCoverCommandHandlerTests|FullyQualifiedName~AdminBulkRetryWikidataCoverCommandHandlerTests" --no-restore`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IWikidataCoverEnrichmentRunner.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunner.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IWikidataEnrichmentEventBroadcaster.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminEnrichWikidataCoverCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnrichCatalogCover/AdminBulkRetryWikidataCoverCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/WikidataCoverEnrichmentJob.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/WikidataCoverEnrichmentRunnerTests.cs
git commit -m "feat(catalog-be): #2255 F6 runner persists triggeredByAdminUserId + SSE payload"
```

---

## Phase 4 — Backend Endpoint + Routing

### Task 4.1: New endpoint `POST /bulk-acknowledge` + update `GET /dead-letters`

**Files:**
- Modify: `apps/api/src/Api/Routing/Admin/AdminWikidataCoverEnrichmentEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/Integration/Routing/AdminWikidataCoverEnrichmentEndpointsTests.cs` (modify — add tests)

- [ ] **Step 1: Write failing IT tests**

Append to `AdminWikidataCoverEnrichmentEndpointsTests.cs`:

```csharp
[Fact]
public async Task BulkAcknowledge_Anonymous_Returns401()
{
    using var factory = new IntegrationWebApplicationFactory();
    var client = factory.CreateAnonClient();

    var response = await client.PostAsJsonAsync(
        "/api/v1/admin/wikidata/enrichment/bulk-acknowledge",
        new { attemptIds = new[] { Guid.NewGuid() }, note = (string?)null });

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task BulkAcknowledge_AdminWithDeadLetter_ReturnsAckedCountOne()
{
    using var factory = new IntegrationWebApplicationFactory();
    var (adminId, client) = await factory.CreateAdminClientAsync();
    // Seed: 1 game + 1 dead-letter
    var dl = await factory.SeedDeadLetterAsync();

    var response = await client.PostAsJsonAsync(
        "/api/v1/admin/wikidata/enrichment/bulk-acknowledge",
        new { attemptIds = new[] { dl.Id }, note = "test note" });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<AdminBulkAcknowledgeResult>();
    body!.AckedCount.Should().Be(1);
    body.Rows.Single().AttemptId.Should().Be(dl.Id);

    // Verify default list view now excludes it
    var listResponse = await client.GetAsync("/api/v1/admin/wikidata/enrichment/dead-letters");
    var list = await listResponse.Content.ReadFromJsonAsync<WikidataDeadLetterAttemptsResult>();
    list!.Items.Should().NotContain(i => i.Id == dl.Id);

    // Verify includeAcknowledged=true surfaces it
    var includedResponse = await client.GetAsync("/api/v1/admin/wikidata/enrichment/dead-letters?includeAcknowledged=true");
    var included = await includedResponse.Content.ReadFromJsonAsync<WikidataDeadLetterAttemptsResult>();
    included!.Items.Should().Contain(i => i.Id == dl.Id);
}

[Fact]
public async Task BulkAcknowledge_OverFifty_Returns400()
{
    using var factory = new IntegrationWebApplicationFactory();
    var (_, client) = await factory.CreateAdminClientAsync();
    var ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray();

    var response = await client.PostAsJsonAsync(
        "/api/v1/admin/wikidata/enrichment/bulk-acknowledge",
        new { attemptIds = ids, note = (string?)null });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

> **Note:** the `IntegrationWebApplicationFactory.SeedDeadLetterAsync` / `CreateAdminClientAsync` / `CreateAnonClient` helpers should already exist in the existing test class for Phase E F2 (PR #2222). Search the existing file before adding; reuse or extend.

- [ ] **Step 2: Run failing test**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminWikidataCoverEnrichmentEndpointsTests" --no-restore`
Expected: FAIL on `bulk-acknowledge` 404 (route not registered).

- [ ] **Step 3: Add endpoint mapping**

Edit `AdminWikidataCoverEnrichmentEndpoints.cs`:

1. Inside `MapAdminWikidataCoverEnrichmentEndpoints` add:
```csharp
// Phase F F5 — bulk-acknowledge endpoint.
group.MapPost("/bulk-acknowledge", HandleBulkAcknowledge)
    .WithName("AdminWikidataCoverEnrichment_BulkAcknowledge")
    .WithTags("Admin", "WikidataCoverEnrichment");
```

2. Add request DTO + handler:
```csharp
/// <summary>Body for the F5 bulk-acknowledge endpoint.</summary>
internal sealed record AdminBulkAcknowledgeWikidataRequest(
    IReadOnlyList<Guid>? AttemptIds,
    string? Note);

private static async Task<IResult> HandleBulkAcknowledge(
    AdminBulkAcknowledgeWikidataRequest? request,
    HttpContext context,
    IMediator mediator,
    CancellationToken ct)
{
    var command = new AdminBulkAcknowledgeWikidataCoverCommand(
        AttemptIds: request?.AttemptIds ?? Array.Empty<Guid>(),
        Note: request?.Note,
        TriggeredByUserId: context.User.GetUserId());

    var result = await mediator.Send(command, ct).ConfigureAwait(false);
    return Results.Ok(result);
}
```

3. Extend `HandleListDeadLetters` to accept `includeAcknowledged`:
```csharp
private static async Task<IResult> HandleListDeadLetters(
    [FromQuery] int? skip,
    [FromQuery] int? take,
    [FromQuery] string? reason,
    [FromQuery] bool? includeAcknowledged,   // F5 NEW
    IMediator mediator,
    CancellationToken ct)
{
    var query = new GetWikidataDeadLetterAttemptsQuery(
        Skip: skip ?? 0,
        Take: take ?? 50,
        ReasonFilter: string.IsNullOrWhiteSpace(reason) ? null : reason,
        IncludeAcknowledged: includeAcknowledged ?? false);

    var result = await mediator.Send(query, ct).ConfigureAwait(false);
    return Results.Ok(result);
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AdminWikidataCoverEnrichmentEndpointsTests" --no-restore`
Expected: PASS (existing + 3 new tests).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/Admin/AdminWikidataCoverEnrichmentEndpoints.cs \
        apps/api/tests/Api.Tests/Integration/Routing/AdminWikidataCoverEnrichmentEndpointsTests.cs
git commit -m "feat(catalog-be): #2254 POST /bulk-acknowledge + #2255 includeAcknowledged query"
```

---

## Phase 5 — Frontend

### Task 5.1: API client — `bulkAcknowledgeDeadLetters` + extended DTOs

**Files:**
- Modify: `apps/web/src/lib/api/admin-wikidata-dead-letters.ts`
- Test: `apps/web/src/lib/api/__tests__/admin-wikidata-dead-letters-phase-f.test.ts` (new)

- [ ] **Step 1: Write failing test**

```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest';
import {
  bulkAcknowledgeDeadLetters,
  listDeadLetters,
  type AdminBulkAcknowledgeResult,
} from '@/lib/api/admin-wikidata-dead-letters';

const mockFetch = vi.fn();
beforeEach(() => {
  vi.stubGlobal('fetch', mockFetch);
  mockFetch.mockReset();
});

describe('bulkAcknowledgeDeadLetters', () => {
  it('POSTs attemptIds + note to /bulk-acknowledge with credentials', async () => {
    const payload: AdminBulkAcknowledgeResult = {
      ackedCount: 1, idempotentNoOpCount: 0, notFoundCount: 0,
      rows: [{ attemptId: 'a-1', gameId: 'g-1', outcome: 'acked', reason: null }],
    };
    mockFetch.mockResolvedValueOnce(new Response(JSON.stringify(payload), { status: 200 }));

    const result = await bulkAcknowledgeDeadLetters(['a-1'], 'op note');

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/v1/admin/wikidata/enrichment/bulk-acknowledge',
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        body: JSON.stringify({ attemptIds: ['a-1'], note: 'op note' }),
      }),
    );
    expect(result.ackedCount).toBe(1);
  });

  it('throws Error on 400', async () => {
    mockFetch.mockResolvedValueOnce(new Response('Too many', { status: 400, statusText: 'Bad Request' }));
    await expect(bulkAcknowledgeDeadLetters(['a-1'], null)).rejects.toThrow(/Failed to bulk-acknowledge/);
  });
});

describe('listDeadLetters', () => {
  it('threads includeAcknowledged=true into query string', async () => {
    mockFetch.mockResolvedValueOnce(new Response(JSON.stringify({
      items: [], totalCount: 0, skip: 0, take: 50,
    }), { status: 200 }));

    await listDeadLetters({ includeAcknowledged: true });

    expect(mockFetch).toHaveBeenCalledWith(
      expect.stringContaining('includeAcknowledged=true'),
      expect.any(Object),
    );
  });
});
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/web && pnpm test -- --run admin-wikidata-dead-letters-phase-f`
Expected: FAIL — `bulkAcknowledgeDeadLetters` not exported.

- [ ] **Step 3: Update API client**

Edit `apps/web/src/lib/api/admin-wikidata-dead-letters.ts`:

1. Extend `WikidataDeadLetterAttemptDto`:
```typescript
export interface WikidataDeadLetterAttemptDto {
  id: string;
  sharedGameId: string;
  gameTitle: string;
  attemptedAt: string;
  deadLetteredAt: string;
  reason: string;
  details: string | null;
  retryCount: number;
  // F5
  acknowledgedAt: string | null;
  acknowledgedBy: string | null;
  acknowledgedByFullName: string | null;
  // F6
  triggeredByAdminUserId: string | null;
  triggeredByAdminFullName: string | null;
}
```

2. Extend `listDeadLetters` to accept `includeAcknowledged`:
```typescript
export async function listDeadLetters(
  options: {
    skip?: number;
    take?: number;
    reason?: string;
    includeAcknowledged?: boolean;
  } = {}
): Promise<WikidataDeadLetterAttemptsResult> {
  const params = new URLSearchParams();
  if (options.skip !== undefined) params.set('skip', String(options.skip));
  if (options.take !== undefined) params.set('take', String(options.take));
  if (options.reason) params.set('reason', options.reason);
  if (options.includeAcknowledged) params.set('includeAcknowledged', 'true');

  const url = `${ENDPOINT_BASE}/dead-letters${params.size > 0 ? `?${params.toString()}` : ''}`;
  const response = await fetch(url, { credentials: 'include' });
  if (!response.ok) await rejectAs(response, 'Failed to list dead-letters');
  return (await response.json()) as WikidataDeadLetterAttemptsResult;
}
```

3. Extend `WikidataAttemptTimelineNode`:
```typescript
export interface WikidataAttemptTimelineNode {
  id: string;
  attemptedAt: string;
  outcome: AttemptTimelineOutcome;
  reason: string | null;
  details: string | null;
  retryCount: number;
  nextRetryAt: string | null;
  deadLetteredAt: string | null;
  // F6
  triggeredByAdminUserId: string | null;
  triggeredByAdminFullName: string | null;
}
```

4. Add F5 section at the end:
```typescript
// ─────────────────────────────────────────────────────────────────────────────
// Phase F F5 — bulk acknowledge
// ─────────────────────────────────────────────────────────────────────────────

export const BULK_ACKNOWLEDGE_MAX_BATCH = 50;
export const BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH = 500;

export type BulkAcknowledgeRowOutcome = 'acked' | 'already-acked' | 'not-found' | 'wrong-state';

export interface BulkAcknowledgeRow {
  attemptId: string;
  gameId: string | null;
  outcome: BulkAcknowledgeRowOutcome;
  reason: string | null;
}

export interface AdminBulkAcknowledgeResult {
  ackedCount: number;
  idempotentNoOpCount: number;
  notFoundCount: number;
  rows: BulkAcknowledgeRow[];
}

export async function bulkAcknowledgeDeadLetters(
  attemptIds: string[],
  note: string | null,
): Promise<AdminBulkAcknowledgeResult> {
  const response = await fetch(`${ENDPOINT_BASE}/bulk-acknowledge`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ attemptIds, note }),
  });
  if (!response.ok) await rejectAs(response, 'Failed to bulk-acknowledge enrichments');
  return (await response.json()) as AdminBulkAcknowledgeResult;
}
```

- [ ] **Step 4: Run tests**

Run: same as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/admin-wikidata-dead-letters.ts \
        apps/web/src/lib/api/__tests__/admin-wikidata-dead-letters-phase-f.test.ts
git commit -m "feat(catalog-fe): #2254+#2255 API client — bulkAcknowledge + extended DTOs"
```

---

### Task 5.2: `AcknowledgeSelectedModal` component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/AcknowledgeSelectedModal.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/__tests__/AcknowledgeSelectedModal.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AcknowledgeSelectedModal } from '../AcknowledgeSelectedModal';

describe('AcknowledgeSelectedModal', () => {
  it('renders count and submits with note', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    const onCancel = vi.fn();

    render(<AcknowledgeSelectedModal
      open
      selectedCount={3}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />);

    expect(screen.getByText(/Acknowledge 3 dead-letter row/i)).toBeInTheDocument();
    const textarea = screen.getByLabelText(/note/i) as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: 'commons deleted file' } });
    fireEvent.click(screen.getByRole('button', { name: /^Confirm$/i }));

    await vi.waitFor(() => expect(onConfirm).toHaveBeenCalledWith('commons deleted file'));
  });

  it('cancel returns control without note submission', () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    render(<AcknowledgeSelectedModal
      open
      selectedCount={1}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />);

    fireEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalled();
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('enforces 500-char note limit', () => {
    render(<AcknowledgeSelectedModal
      open
      selectedCount={1}
      onConfirm={vi.fn()}
      onCancel={vi.fn()}
    />);

    const textarea = screen.getByLabelText(/note/i) as HTMLTextAreaElement;
    expect(textarea.maxLength).toBe(500);
  });
});
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/web && pnpm test -- --run AcknowledgeSelectedModal`
Expected: FAIL — component missing.

- [ ] **Step 3: Create component**

```tsx
'use client';

import { useState } from 'react';

import { BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH } from '@/lib/api/admin-wikidata-dead-letters';

interface AcknowledgeSelectedModalProps {
  open: boolean;
  selectedCount: number;
  onConfirm: (note: string | null) => Promise<void> | void;
  onCancel: () => void;
}

/**
 * Issue #1823 Phase F F5 — confirmation modal for bulk-acknowledge.
 * Optional free-text note (max 500 chars, log-only per DEC-F-4).
 */
export function AcknowledgeSelectedModal({
  open, selectedCount, onConfirm, onCancel,
}: AcknowledgeSelectedModalProps) {
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);

  if (!open) return null;

  const handleConfirm = async () => {
    setSubmitting(true);
    try {
      await onConfirm(note.trim() === '' ? null : note);
      setNote('');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="ack-modal-title"
      className="fixed inset-0 z-50 flex items-center justify-center bg-background/80 backdrop-blur-sm"
    >
      <div className="w-full max-w-md rounded-lg border border-border bg-card p-6 shadow-lg">
        <h2 id="ack-modal-title" className="text-lg font-semibold text-foreground">
          Acknowledge {selectedCount} dead-letter row{selectedCount === 1 ? '' : 's'}?
        </h2>
        <p className="mt-2 text-sm text-muted-foreground">
          Acknowledged rows are hidden from the default list view and won&apos;t consume
          scheduler retry budget. They are deleted after the 7-day retention sweep
          regardless.
        </p>

        <label htmlFor="ack-note" className="mt-4 block text-sm font-medium text-foreground">
          Note (optional, log only)
        </label>
        <textarea
          id="ack-note"
          aria-label="note"
          maxLength={BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH}
          value={note}
          onChange={(e) => setNote(e.target.value)}
          rows={3}
          className="mt-1 w-full rounded border border-border bg-background p-2 text-sm text-foreground"
          placeholder="e.g. Commons deleted file"
          disabled={submitting}
        />
        <p className="mt-1 text-xs text-muted-foreground">
          {note.length}/{BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH}
        </p>

        <div className="mt-6 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded border border-border bg-background px-3 py-1.5 text-sm hover:bg-muted"
            disabled={submitting}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            className="rounded bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
            disabled={submitting}
          >
            {submitting ? 'Acknowledging…' : 'Confirm'}
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

Run: same as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/wikidata-dead-letters/AcknowledgeSelectedModal.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/wikidata-dead-letters/__tests__/AcknowledgeSelectedModal.test.tsx
git commit -m "feat(catalog-fe): #2254 AcknowledgeSelectedModal with optional note"
```

---

### Task 5.3: Wire toolbar + toggle + visual marker on dead-letters page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/page.tsx`
- Test (smoke): `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/__tests__/page-phase-f.test.tsx` (new)

- [ ] **Step 1: Write failing smoke test**

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import WikidataDeadLettersPage from '../page';
import * as api from '@/lib/api/admin-wikidata-dead-letters';

vi.mock('@/lib/api/admin-wikidata-dead-letters');
vi.mock('../useWikidataEnrichmentEvents', () => ({
  useWikidataEnrichmentEvents: () => ({ state: 'open', lastEvent: null }),
}));

describe('WikidataDeadLettersPage — Phase F integration', () => {
  beforeEach(() => {
    vi.mocked(api.listDeadLetters).mockResolvedValue({
      items: [{
        id: 'a-1', sharedGameId: 'g-1', gameTitle: 'Game',
        attemptedAt: '2026-06-10T00:00:00Z', deadLetteredAt: '2026-06-10T00:00:00Z',
        reason: 'r2-upload-error', details: null, retryCount: 3,
        acknowledgedAt: null, acknowledgedBy: null, acknowledgedByFullName: null,
        triggeredByAdminUserId: null, triggeredByAdminFullName: null,
      }],
      totalCount: 1, skip: 0, take: 50,
    });
    vi.mocked(api.bulkAcknowledgeDeadLetters).mockResolvedValue({
      ackedCount: 1, idempotentNoOpCount: 0, notFoundCount: 0,
      rows: [{ attemptId: 'a-1', gameId: 'g-1', outcome: 'acked', reason: null }],
    });
  });

  it('renders Acknowledge selected button alongside Retry selected', async () => {
    render(<WikidataDeadLettersPage />);
    await waitFor(() => expect(screen.getByText('Game')).toBeInTheDocument());

    // Select the only row
    const checkbox = screen.getAllByRole('checkbox')[0];
    fireEvent.click(checkbox);

    expect(screen.getByRole('button', { name: /Acknowledge selected/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Retry selected/i })).toBeInTheDocument();
  });

  it('renders Show acknowledged toggle (off by default)', async () => {
    render(<WikidataDeadLettersPage />);
    await waitFor(() => expect(screen.getByText('Game')).toBeInTheDocument());

    const toggle = screen.getByRole('switch', { name: /Show acknowledged/i });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
  });

  it('toggling Show acknowledged refetches with includeAcknowledged=true', async () => {
    render(<WikidataDeadLettersPage />);
    await waitFor(() => expect(screen.getByText('Game')).toBeInTheDocument());

    const toggle = screen.getByRole('switch', { name: /Show acknowledged/i });
    fireEvent.click(toggle);

    await waitFor(() => expect(api.listDeadLetters).toHaveBeenCalledWith(
      expect.objectContaining({ includeAcknowledged: true })
    ));
  });
});
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/web && pnpm test -- --run page-phase-f`
Expected: FAIL — Acknowledge button/switch not rendered.

- [ ] **Step 3: Wire into page**

Edit `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/page.tsx`:

1. Import additions (top of file):
```typescript
import {
  BULK_ACKNOWLEDGE_MAX_BATCH,
  bulkAcknowledgeDeadLetters,
  type AdminBulkAcknowledgeResult,
  // ... existing imports
} from '@/lib/api/admin-wikidata-dead-letters';

import { AcknowledgeSelectedModal } from './AcknowledgeSelectedModal';
```

2. Add new state alongside `bulkState`:
```typescript
const [includeAcknowledged, setIncludeAcknowledged] = useState(false);
const [ackModalOpen, setAckModalOpen] = useState(false);
const [ackState, setAckState] = useState<
  | { state: 'idle' }
  | { state: 'running' }
  | { state: 'done'; result: AdminBulkAcknowledgeResult }
  | { state: 'error'; error: string }
>({ state: 'idle' });
```

3. Update `load()` callback to pass `includeAcknowledged`:
```typescript
const response = await listDeadLetters({
  skip: page * PAGE_SIZE,
  take: PAGE_SIZE,
  reason: reasonFilter || undefined,
  includeAcknowledged,                 // F5
});
```

4. Add toggle effect:
```typescript
useEffect(() => { void load(); }, [load, includeAcknowledged]);
```

5. Add Acknowledge handler:
```typescript
const handleAcknowledgeConfirm = useCallback(async (note: string | null) => {
  setAckState({ state: 'running' });
  try {
    const result = await bulkAcknowledgeDeadLetters(Array.from(selectedIds), note);
    setAckState({ state: 'done', result });
    setSelectedIds(new Set());
    setAckModalOpen(false);
    await load();
  } catch (err) {
    setAckState({ state: 'error', error: err instanceof Error ? err.message : 'Unknown error' });
  }
}, [selectedIds, load]);
```

6. In JSX, add toggle (somewhere in the filter bar):
```tsx
<label className="flex items-center gap-2 text-sm text-foreground">
  <button
    type="button"
    role="switch"
    aria-checked={includeAcknowledged}
    aria-label="Show acknowledged"
    onClick={() => setIncludeAcknowledged((v) => !v)}
    className={`h-5 w-9 rounded-full transition-colors ${
      includeAcknowledged ? 'bg-primary' : 'bg-muted'
    }`}
  >
    <span
      className={`block h-4 w-4 rounded-full bg-background shadow transition-transform ${
        includeAcknowledged ? 'translate-x-4' : 'translate-x-0.5'
      }`}
    />
  </button>
  Show acknowledged
</label>
```

7. Add "Acknowledge selected" button alongside "Retry selected" (find existing Retry button JSX and add):
```tsx
<button
  type="button"
  onClick={() => setAckModalOpen(true)}
  disabled={selectedIds.size === 0 || selectedIds.size > BULK_ACKNOWLEDGE_MAX_BATCH}
  className="rounded bg-secondary px-3 py-1.5 text-sm font-medium text-secondary-foreground hover:bg-secondary/90 disabled:opacity-50"
>
  Acknowledge selected ({selectedIds.size})
</button>
```

8. Render modal at the end of the JSX (before the closing fragment):
```tsx
<AcknowledgeSelectedModal
  open={ackModalOpen}
  selectedCount={selectedIds.size}
  onConfirm={handleAcknowledgeConfirm}
  onCancel={() => setAckModalOpen(false)}
/>
```

9. Visual marker for acked rows (in the row rendering, when `item.acknowledgedAt !== null`):
```tsx
<tr
  key={item.id}
  className={item.acknowledgedAt ? 'opacity-60' : ''}
>
  {/* ... existing cells ... */}
  {item.acknowledgedAt && item.acknowledgedByFullName && (
    <span className="ml-2 inline-flex rounded bg-muted px-2 py-0.5 text-xs text-muted-foreground">
      Acked by {item.acknowledgedByFullName} on{' '}
      {new Date(item.acknowledgedAt).toLocaleDateString()}
    </span>
  )}
</tr>
```

- [ ] **Step 4: Run tests**

Run: same as Step 2. Expected: PASS (3 new tests + all existing page tests still green).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/wikidata-dead-letters/page.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/wikidata-dead-letters/__tests__/page-phase-f.test.tsx
git commit -m "feat(catalog-fe): #2254 wire toolbar+toggle+marker on dead-letters page"
```

---

### Task 5.4: F6 badge in `AttemptTimelineDrawer`

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/AttemptTimelineDrawer.tsx`
- Test (modify or new): `apps/web/src/app/admin/(dashboard)/monitor/wikidata-dead-letters/__tests__/AttemptTimelineDrawer-phase-f.test.tsx` (new)

- [ ] **Step 1: Write failing test**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AttemptTimelineDrawer } from '../AttemptTimelineDrawer';
import * as api from '@/lib/api/admin-wikidata-dead-letters';

vi.mock('@/lib/api/admin-wikidata-dead-letters');

describe('AttemptTimelineDrawer — F6 admin badge', () => {
  it('shows admin badge when triggeredByAdminUserId is non-null', async () => {
    vi.mocked(api.getAttemptTimeline).mockResolvedValue({
      gameId: 'g-1',
      items: [{
        id: 'a-1', attemptedAt: '2026-06-10T00:00:00Z',
        outcome: 'Success', reason: 'success', details: null, retryCount: 0,
        nextRetryAt: null, deadLetteredAt: null,
        triggeredByAdminUserId: 'admin-1',
        triggeredByAdminFullName: 'Alice Admin',
      }],
    });
    render(<AttemptTimelineDrawer
      gameId="g-1" gameTitle="Test Game" open onClose={() => {}}
    />);
    await waitFor(() => expect(screen.getByText(/Success/i)).toBeInTheDocument());

    const badge = screen.getByText(/admin/i);
    expect(badge).toBeInTheDocument();
    expect(badge.closest('[title]')).toHaveAttribute('title', 'Triggered by admin Alice Admin');
  });

  it('hides admin badge when triggeredByAdminUserId is null', async () => {
    vi.mocked(api.getAttemptTimeline).mockResolvedValue({
      gameId: 'g-1',
      items: [{
        id: 'a-1', attemptedAt: '2026-06-10T00:00:00Z',
        outcome: 'Success', reason: 'success', details: null, retryCount: 0,
        nextRetryAt: null, deadLetteredAt: null,
        triggeredByAdminUserId: null,
        triggeredByAdminFullName: null,
      }],
    });
    render(<AttemptTimelineDrawer
      gameId="g-1" gameTitle="Test Game" open onClose={() => {}}
    />);
    await waitFor(() => expect(screen.getByText(/Success/i)).toBeInTheDocument());

    expect(screen.queryByText(/^admin$/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run failing test**

Run: `cd apps/web && pnpm test -- --run AttemptTimelineDrawer-phase-f`
Expected: FAIL — badge not rendered.

- [ ] **Step 3: Add badge in drawer**

Edit `AttemptTimelineDrawer.tsx` — locate the per-node rendering (search for `node.outcome` or `Outcome` JSX), add:

```tsx
{node.triggeredByAdminUserId && (
  <span
    title={
      node.triggeredByAdminFullName
        ? `Triggered by admin ${node.triggeredByAdminFullName}`
        : 'Triggered by admin (deleted user)'
    }
    className="ml-2 inline-flex items-center rounded border border-primary/40 bg-primary/10 px-1.5 py-0.5 text-xs font-medium text-primary"
  >
    admin
  </span>
)}
```

- [ ] **Step 4: Run tests**

Run: same as Step 2. Expected: PASS (2 new tests + existing drawer tests still green).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/monitor/wikidata-dead-letters/AttemptTimelineDrawer.tsx \
        apps/web/src/app/admin/\(dashboard\)/monitor/wikidata-dead-letters/__tests__/AttemptTimelineDrawer-phase-f.test.tsx
git commit -m "feat(catalog-fe): #2255 F6 admin badge in attempt timeline drawer"
```

---

## Phase 6 — Integration + E2E

### Task 6.1: E2E Playwright skeleton

**Files:**
- Create: `apps/web/e2e/admin-wikidata-bulk-acknowledge-flow.spec.ts`

- [ ] **Step 1: Write skeleton E2E spec**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Admin Wikidata bulk acknowledge flow (#2254)', () => {
  test.skip(
    !process.env.E2E_ADMIN_EMAIL || !process.env.E2E_ADMIN_PASSWORD,
    'Requires admin credentials',
  );

  test('select dead-letters, acknowledge with note, toggle Show acked', async ({ page }) => {
    // 1. Login as admin
    await page.goto('/login');
    await page.getByLabel(/email/i).fill(process.env.E2E_ADMIN_EMAIL!);
    await page.getByLabel(/password/i).fill(process.env.E2E_ADMIN_PASSWORD!);
    await page.getByRole('button', { name: /sign in/i }).click();
    await page.waitForURL(/\/(admin|dashboard)/);

    // 2. Navigate to dead-letter page
    await page.goto('/admin/monitor/wikidata-dead-letters');
    await expect(page.getByRole('heading', { name: /dead.*letter/i })).toBeVisible();

    // 3. If there are any dead-letters, select the first 2
    const rows = page.getByRole('row').filter({ has: page.getByRole('checkbox') });
    const count = await rows.count();
    test.skip(count < 2, 'Requires ≥ 2 dead-letters to run a meaningful bulk-ack');
    await rows.nth(0).getByRole('checkbox').check();
    await rows.nth(1).getByRole('checkbox').check();

    // 4. Open modal, type note, confirm
    await page.getByRole('button', { name: /Acknowledge selected/i }).click();
    await page.getByLabel(/note/i).fill('e2e: known commons-deleted asset');
    await page.getByRole('button', { name: /^Confirm$/i }).click();

    // 5. Modal closes, page reloads, selected rows hidden
    await expect(page.getByRole('dialog')).not.toBeVisible();
    // (cannot assert specific row ids without coupling; rely on count decrease)

    // 6. Toggle Show acknowledged → reappear
    await page.getByRole('switch', { name: /Show acknowledged/i }).click();
    await expect(page.getByText(/Acked by/i).first()).toBeVisible();
  });
});
```

> **Note:** if E2E is configured via `apps/web/e2e/playwright.config.ts` with a global `setup` for admin login, prefer that pattern over inline login.

- [ ] **Step 2: Verify test file syntax**

Run: `cd apps/web && pnpm typecheck`
Expected: no new errors.

- [ ] **Step 3: (No implementation step — spec is the deliverable)**

- [ ] **Step 4: (Skip — E2E runs on `continue-on-error: true`, validation in CI)**

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/admin-wikidata-bulk-acknowledge-flow.spec.ts
git commit -m "test(catalog-fe-e2e): #2254 bulk-acknowledge flow skeleton"
```

---

## Final steps

### Task F.1: Run full BE suite + FE suite + push

- [ ] **Step 1: Backend full suite**

Run: `cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --verbosity minimal 2>&1 | tail -30`
Expected: 0 failed. Failing tests not yet documented in CLAUDE.md "Known Flaky Tests" must be investigated before push.

- [ ] **Step 2: Frontend full unit suite**

Run: `cd apps/web && pnpm test:coverage 2>&1 | tail -30`
Expected: 0 failed.

- [ ] **Step 3: Frontend lint + typecheck**

Run: `cd apps/web && pnpm lint && pnpm typecheck`
Expected: 0 errors.

- [ ] **Step 4: Push branch + open draft PR**

```bash
git push -u origin feature/issue-2254-2255-phase-f-bundle
gh pr create --base main-dev --draft --title "feat(catalog): #2254+#2255 Phase F bundle — F5 bulk acknowledge + F6 attempt-source" --body "$(cat <<'EOF'
## Summary

Closes Phase F follow-up of epic #1823 (Wikidata cover enrichment). Bundles F5 (#2254) bulk acknowledge UI + F6 (#2255) attempt-source attribution in 1 PR per design DEC-F-1.

- **F5**: aggregate mutator `Acknowledge()` (eccezione record-of-fact documentata) + CQRS command + endpoint POST /bulk-acknowledge + FE toolbar/modal/toggle/visual marker
- **F6**: factory `triggeredByAdminUserId` param + runner signature change + DTO/SSE field + FE badge in timeline drawer
- 1 migration: 3 nullable columns + 1 partial index `ix_wikidata_cover_attempts_acknowledged_at`
- 8 design decisions locked: see [spec](docs/superpowers/specs/2026-06-13-issue-2254-2255-phase-f-bundle-design.md)

## Test plan

- [ ] Backend: `dotnet test` (Phase F adds ~25 unit + 6 IT tests)
- [ ] Frontend: `pnpm test:coverage` (Phase F adds ~9 Vitest tests)
- [ ] Lint+typecheck green
- [ ] E2E skeleton ships non-blocking (continue-on-error)
- [ ] Manual smoke on staging: select 2 dead-letters → ack with note → toggle show acked → reappear

Closes #2254
Closes #2255

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Wait for CI green, request code-review subagent**

Use code-review subagent (`/code-review:code-review <PR-URL>`) before marking PR ready-for-review.

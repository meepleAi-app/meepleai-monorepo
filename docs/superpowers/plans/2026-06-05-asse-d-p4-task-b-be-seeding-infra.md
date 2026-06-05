# Task B — BE Entity Seeding Infra (E2E Data-Driven) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare infrastructure BE+FE per Playwright E2E data-driven entity seeding (GameNight + Session + Player) con cleanup deterministico per-test, esposta via admin endpoint MediatR triple-gated, consumata da TypeScript factory wrapper.

**Architecture:** 4 MediatR commands CQRS-compliant (`SeedTestGameNightCommand`, `SeedTestSessionCommand`, `SeedTestPlayerCommand`, `CleanupTestEntitiesCommand`) in nuovo bounded context `Testing` → admin endpoint group `/api/v1/admin/test/seed/*` con `RequireAdminSessionFilter` + triple gate (env `E2E_SEEDING_ENABLED=true` + `ASPNETCORE_ENVIRONMENT != Production` startup fail-fast + AdminFilter runtime) → TS factory `seedEntities.ts` con `testRunId` forzato per parallel safety → demo spec Journey #1 dashboard-drawer-stack golden test handoff a Task C.

**Tech Stack:** .NET 9 + ASP.NET Minimal APIs + MediatR + FluentValidation + EF Core + Testcontainers Postgres + xUnit | TypeScript + Playwright + `page.request.post()` admin session-cookied | bash/yaml CI workflow

**Issue:** [#1928 Task B](https://github.com/meepleAi-app/meepleai-monorepo/issues/1928) — Asse D P4 follow-up
**Spec consolidato:** [`docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md`](../specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md) (committed HEAD main-dev `63571685f`)
**DEC lockate:** DEC-B-1 (Opt A MediatR) · DEC-B-2 (TS factory `page.request`) · DEC-B-3 (per-test `afterEach` + `testRunId`) · DEC-B-4 (triple gate startup fail-fast) · DEC-B-5 (testRunId forzato API) · DEC-B-6 (demo Journey #1 golden handoff)
**Effort target:** 4-6 giorni (7 task TDD bite-sized)

---

## Decisioni di scope

### IN scope
- 4 MediatR commands + handlers + validators + unit tests (~80%+ coverage)
- Admin endpoint group `/api/v1/admin/test/seed/*` + integration tests (auth scenarios + happy path + 503 flag off)
- Triple gate (env startup + endpoint conditional registration + filter)
- TypeScript factory `apps/web/e2e/_helpers/seedEntities.ts` con `testRunId` enforcement
- Demo spec `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` pre-flight pass
- Docs `e2e-entity-seeding.md` (5 sezioni: API ref + Opt A rationale + GWT canonical + CI ops runbook + env failure recovery)
- CI workflow `E2E_SEEDING_ENABLED=true` solo per Playwright E2E job
- Structured logging per seed call (`{ testRunId, entityType, entityId, callerSpec, durationMs }`)

### OUT of scope
- Wire skeleton esistenti FE (Task A separato, PR #1931 ✅ MERGED `4ff31710f`)
- 3 cross-asse journey full data-driven implementation (Task C #1929, gated da Task B demo)
- Multi-tenant `testRunId` isolation (single-tenant MVP)
- Quartz orphan cleanup background job (deferred, decisione separata post 30+gg metriche)
- Cross-browser CI (Firefox/WebKit) (deferred wave futuro)
- Performance SLA per journey (~3-5s acceptable baseline)
- Visual regression baseline (esplicito OUT)

### Branching
- Branch: `feature/issue-1928-be-seeding-infra` (corrente, parent `main-dev`)
- PR target: `main-dev`
- Backend Tests required + Frontend skipping (FE-only changes minimal: 1 helper + 1 spec + docs)

---

## File Structure

### Nuovi file backend

```
apps/api/src/Api/BoundedContexts/Testing/                         (NEW BC)
├── Application/
│   ├── Commands/
│   │   ├── SeedTestGameNightCommand.cs           (command record + response DTO)
│   │   ├── SeedTestGameNightCommandHandler.cs    (MediatR handler)
│   │   ├── SeedTestGameNightCommandValidator.cs  (FluentValidation)
│   │   ├── SeedTestSessionCommand.cs
│   │   ├── SeedTestSessionCommandHandler.cs
│   │   ├── SeedTestSessionCommandValidator.cs
│   │   ├── SeedTestPlayerCommand.cs
│   │   ├── SeedTestPlayerCommandHandler.cs
│   │   ├── SeedTestPlayerCommandValidator.cs
│   │   ├── CleanupTestEntitiesCommand.cs
│   │   ├── CleanupTestEntitiesCommandHandler.cs
│   │   └── CleanupTestEntitiesCommandValidator.cs
│   └── DTOs/
│       ├── SeedTestGameNightResponse.cs
│       ├── SeedTestSessionResponse.cs
│       ├── SeedTestPlayerResponse.cs
│       └── CleanupTestEntitiesResponse.cs
└── Infrastructure/
    └── TestRunIdMetadata.cs                       (helper: format validation + column stamping)

apps/api/src/Api/Routing/Admin/
└── AdminTestSeedEndpoints.cs                      (MapAdminTestSeedEndpoints extension)
```

### File backend modificati

```
apps/api/src/Api/Program.cs                        (+ triple gate startup + conditional registration)
apps/api/src/Api/Extensions/EndpointFilterExtensions.cs  (verifica .RequireAdminSession() exists)
```

### Nuovi file test backend

```
apps/api/tests/Api.Tests/Unit/Testing/
├── SeedTestGameNightCommandHandlerTests.cs       (~6 unit test)
├── SeedTestSessionCommandHandlerTests.cs         (~6 unit test)
├── SeedTestPlayerCommandHandlerTests.cs          (~6 unit test)
├── CleanupTestEntitiesCommandHandlerTests.cs     (~8 unit test cascade scope)
└── Validators/
    ├── SeedTestGameNightCommandValidatorTests.cs (~4 unit test)
    ├── SeedTestSessionCommandValidatorTests.cs   (~4 unit test)
    ├── SeedTestPlayerCommandValidatorTests.cs    (~4 unit test)
    └── CleanupTestEntitiesCommandValidatorTests.cs (~3 unit test)

apps/api/tests/Api.Tests/Integration/Testing/
├── AdminTestSeedEndpointsIntegrationTests.cs     (~8 integration test: auth + happy path + flag off 503)
└── TripleGateStartupTests.cs                     (~3 integration test: env=Prod refuses, env=Test+flag enabled OK, env=Test+flag off no endpoints)
```

### Nuovi file frontend

```
apps/web/e2e/_helpers/
└── seedEntities.ts                                (TS factory wrapper)

apps/web/e2e/
└── cross-asse-journey-1-dashboard-drawer-stack.spec.ts  (DEMO spec pre-flight)
```

### Nuovi file docs

```
docs/for-developers/testing/
└── e2e-entity-seeding.md                          (~250 LOC, 5 sezioni)
```

### File CI/CD modificati

```
.github/workflows/ci.yml                           (E2E_SEEDING_ENABLED=true env per E2E job)
```

---

## Convenzioni stabilite (riferimenti pattern esistenti)

| Convenzione | Reference | Note |
|---|---|---|
| MediatR command record | `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RecalculateBadgesCommand.cs` | `public sealed record XxxCommand : IRequest<XxxResponse>` |
| Admin endpoint group | `apps/api/src/Api/Routing/Admin/AdminCatalogSeedEndpoints.cs` | `group.AddEndpointFilter<RequireAdminSessionFilter>()` + `MapPost("/", HandleX)` |
| RequireAdminSessionFilter | `apps/api/src/Api/Filters/RequireAdminSessionFilter.cs` | 401 unauth + 403 non-admin; pattern già rodato |
| FE auth seeding | `apps/web/e2e/_helpers/seedAuthSession.ts` | `seedAuthSession(page, { role: 'admin' })` + cookies |
| GameNight aggregate | `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs` | demo "GameNight" ↔ `GameNightEvent` (CLAUDE.md) |
| Session sub-entity | `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightSession.cs` | demo `Session.IsLive` ↔ `StartedAt != null && FinalizedAt == null` |

### `testRunId` format canonical

```
e2e-{playwrightTestId}-{epochMs}
es. "e2e-abc123def456-1717603200000"
```

Validation regex: `^e2e-[a-zA-Z0-9]{8,32}-\d{13}$`

### Database column stamping

Tutte le entity seeded ricevono nuova colonna shadow `TestRunId NVARCHAR(64) NULL`:
- `NULL` per entity production (default)
- Non-null SOLO per entity seeded via Test commands
- `CleanupTestEntitiesCommand` cascade delete via `WHERE TestRunId = @testRunId`

> **NOTA**: shadow property via EF Core configuration in handler (no migration richiesta — EF Core auto-applica shadow properties via `OnModelCreating` o `EntityTypeBuilder.Property("TestRunId")`). Verifica T1 step 1 fail dimostra setup.

---

## Task 1: `SeedTestGameNightCommand` + Handler + Validator + Unit Tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestGameNightCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestGameNightCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestGameNightCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestGameNightResponse.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Infrastructure/TestRunIdMetadata.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/SeedTestGameNightCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestGameNightCommandValidatorTests.cs`

- [ ] **Step 1: Write the failing test (handler happy path)**

Create `apps/api/tests/Api.Tests/Unit/Testing/SeedTestGameNightCommandHandlerTests.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.Testing.Application.Commands;
using Api.BoundedContexts.Testing.Application.DTOs;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.Testing;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestGameNightCommandHandlerTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture _fixture;

    public SeedTestGameNightCommandHandlerTests(TestDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedStatus_CreatesGameNightWithTestRunIdStamp()
    {
        // Arrange
        await using var db = _fixture.CreateDbContext();
        var handler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var testRunId = "e2e-abc123def456-1717603200000";
        var command = new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = "host@e2e.test",
        };

        // Act
        var response = await handler.Handle(command, CancellationToken.None);

        // Assert
        response.GameNightId.Should().NotBe(Guid.Empty);
        response.OwnerId.Should().NotBe(Guid.Empty);
        var seeded = await db.GameNightEvents.SingleOrDefaultAsync(g => g.Id == response.GameNightId);
        seeded.Should().NotBeNull();
        seeded!.Status.Should().Be(GameNightEventStatus.Published);
        // testRunId stamping via shadow property
        var testRunIdShadow = db.Entry(seeded).Property<string?>("TestRunId").CurrentValue;
        testRunIdShadow.Should().Be(testRunId);
    }
}
```

> **NOTE on TestDbContextFixture**: pattern esistente in `apps/api/tests/Api.Tests/Infrastructure/`. Se non c'è, verifica `SharedTestcontainersFixture.cs` (Postgres) o crea InMemory wrapper come fallback unit-test only. Default: usa InMemory provider per Unit-trait tests (handler logic isolato, no SQL semantics).

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~SeedTestGameNightCommandHandlerTests"
```

Expected: FAIL with `error CS0246: The type or namespace name 'SeedTestGameNightCommand' could not be found` (command type non esiste).

- [ ] **Step 3: Create SeedTestGameNightCommand record + Response DTO**

Create `apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestGameNightResponse.cs`:

```csharp
namespace Api.BoundedContexts.Testing.Application.DTOs;

/// <summary>
/// Response from <see cref="Commands.SeedTestGameNightCommand"/>.
/// Returns IDs of created entities so test factory can chain further seed calls.
/// </summary>
public sealed record SeedTestGameNightResponse(
    Guid GameNightId,
    Guid OwnerId,
    string TestRunId);
```

Create `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestGameNightCommand.cs`:

```csharp
using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Seed a GameNightEvent for E2E test data-driven scenarios.
/// Stamps the new aggregate with the caller's <see cref="TestRunId"/> via shadow property
/// so <see cref="CleanupTestEntitiesCommand"/> can cascade-delete by run scope.
/// </summary>
/// <remarks>
/// Exposed via <c>POST /api/v1/admin/test/seed/game-night</c> behind triple gate
/// (env <c>E2E_SEEDING_ENABLED=true</c> + <c>ASPNETCORE_ENVIRONMENT != Production</c>
/// + <see cref="Filters.RequireAdminSessionFilter"/>). Spec:
/// <c>docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md</c> DEC-B-1.
/// </remarks>
public sealed record SeedTestGameNightCommand : IRequest<SeedTestGameNightResponse>
{
    /// <summary>testRunId scope, format <c>e2e-{testId}-{epochMs}</c>. Forced via factory API (DEC-B-5).</summary>
    public required string TestRunId { get; init; }

    /// <summary>Initial GameNight status. Allowed: Draft, Published, InProgress, Completed.</summary>
    public required string Status { get; init; }

    /// <summary>Owner email. Used to provision (or reuse) owner User for the seeded GameNight.</summary>
    public required string OwnerEmail { get; init; }

    /// <summary>Optional scoringType for sessions. Allowed: Points, BinaryWin, Objectives, Ranking.</summary>
    public string? ScoringType { get; init; }

    /// <summary>Optional player roster count (excluding owner). Default 0.</summary>
    public int RosterCount { get; init; }
}
```

- [ ] **Step 4: Create TestRunIdMetadata helper**

Create `apps/api/src/Api/BoundedContexts/Testing/Infrastructure/TestRunIdMetadata.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.Testing.Infrastructure;

/// <summary>
/// Issue #1928 Task B — testRunId convention + EF Core shadow property name.
/// Format: <c>e2e-{playwrightTestId}-{epochMs}</c>, validated server-side per request.
/// </summary>
public static partial class TestRunIdMetadata
{
    /// <summary>Shadow property name applied to seeded entity tables via OnModelCreating.</summary>
    public const string ShadowPropertyName = "TestRunId";

    /// <summary>Canonical format regex. Used by validators (T1-T4).</summary>
    [GeneratedRegex(@"^e2e-[a-zA-Z0-9]{8,32}-\d{13}$", RegexOptions.Compiled)]
    public static partial Regex Format();

    /// <summary>Returns true when value matches <see cref="Format"/>.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Format().IsMatch(value);
}
```

- [ ] **Step 5: Create SeedTestGameNightCommandHandler**

Create `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestGameNightCommandHandler.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.Testing.Application.DTOs;
using Api.BoundedContexts.Testing.Infrastructure;
using Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B — Handler for <see cref="SeedTestGameNightCommand"/>.
/// Persists a GameNightEvent + owner User stamped with <c>TestRunId</c> shadow
/// property for later cleanup by <see cref="CleanupTestEntitiesCommand"/>.
/// </summary>
internal sealed class SeedTestGameNightCommandHandler
    : IRequestHandler<SeedTestGameNightCommand, SeedTestGameNightResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SeedTestGameNightCommandHandler> _logger;

    public SeedTestGameNightCommandHandler(
        ApplicationDbContext db,
        ILogger<SeedTestGameNightCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestGameNightResponse> Handle(
        SeedTestGameNightCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var ownerId = Guid.NewGuid();
        var owner = new User(
            id: ownerId,
            email: request.OwnerEmail,
            displayName: $"E2E Host {request.TestRunId[..16]}",
            role: UserRole.User);
        _db.Users.Add(owner);
        _db.Entry(owner).Property<string?>(TestRunIdMetadata.ShadowPropertyName)
            .CurrentValue = request.TestRunId;

        var gameNight = GameNightEvent.Create(
            ownerId: ownerId,
            title: $"E2E GameNight {request.TestRunId[..16]}",
            scheduledFor: DateTimeOffset.UtcNow.AddDays(7));

        if (request.Status is "Published")
        {
            gameNight.Publish();
        }
        else if (request.Status is "InProgress")
        {
            gameNight.Publish();
            gameNight.StartCurrentSession();
        }
        else if (request.Status is "Completed")
        {
            gameNight.Publish();
            gameNight.StartCurrentSession();
            gameNight.CompleteCurrentSession();
        }
        // Draft = default, no transition

        _db.GameNightEvents.Add(gameNight);
        _db.Entry(gameNight).Property<string?>(TestRunIdMetadata.ShadowPropertyName)
            .CurrentValue = request.TestRunId;

        await _db.SaveChangesAsync(cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded GameNight {GameNightId} status={Status} testRunId={TestRunId} durationMs={Duration}",
            gameNight.Id, request.Status, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestGameNightResponse(gameNight.Id, ownerId, request.TestRunId);
    }
}
```

> **NOTE on shadow property**: l'instanciazione di `TestRunId` shadow property richiede setup in `ApplicationDbContext.OnModelCreating`:
> ```csharp
> modelBuilder.Entity<GameNightEvent>().Property<string?>("TestRunId").HasMaxLength(64);
> modelBuilder.Entity<User>().Property<string?>("TestRunId").HasMaxLength(64);
> // Repeat for GameNightSession, GameNightRsvp, GameNightInvitation in T2/T3
> ```
> Aggiungi questa config in `ApplicationDbContext.cs` PRIMA di run test step 2.

- [ ] **Step 6: Run test to verify it passes**

```bash
cd apps/api/src/Api
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~SeedTestGameNightCommandHandlerTests"
```

Expected: PASS (1 test green).

- [ ] **Step 7: Add 5 more handler tests (status variants + edge cases)**

Append to `SeedTestGameNightCommandHandlerTests.cs`:

```csharp
[Fact]
public async Task Handle_DraftStatus_CreatesGameNightInDraftState()
{
    await using var db = _fixture.CreateDbContext();
    var handler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
    var cmd = new SeedTestGameNightCommand
    {
        TestRunId = "e2e-draftcase01234-1717603200000",
        Status = "Draft",
        OwnerEmail = "draft@e2e.test",
    };

    var response = await handler.Handle(cmd, CancellationToken.None);

    var seeded = await db.GameNightEvents.SingleAsync(g => g.Id == response.GameNightId);
    seeded.Status.Should().Be(GameNightEventStatus.Draft);
}

[Fact]
public async Task Handle_InProgressStatus_CreatesGameNightWithLiveSession() { /* similar */ }

[Fact]
public async Task Handle_CompletedStatus_CreatesGameNightWithFinalizedSession() { /* similar */ }

[Fact]
public async Task Handle_RosterCount_3_CreatesGameNightWith3Roster() { /* similar */ }

[Fact]
public async Task Handle_ParallelCalls_DifferentTestRunIds_NoCollision()
{
    await using var db = _fixture.CreateDbContext();
    var handler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
    var cmd1 = new SeedTestGameNightCommand { TestRunId = "e2e-parallel001234-1717603200000", Status = "Draft", OwnerEmail = "p1@e2e.test" };
    var cmd2 = new SeedTestGameNightCommand { TestRunId = "e2e-parallel002345-1717603200000", Status = "Draft", OwnerEmail = "p2@e2e.test" };

    var r1 = await handler.Handle(cmd1, CancellationToken.None);
    var r2 = await handler.Handle(cmd2, CancellationToken.None);

    r1.GameNightId.Should().NotBe(r2.GameNightId);
    r1.TestRunId.Should().NotBe(r2.TestRunId);
}
```

- [ ] **Step 8: Run all T1 handler tests to verify pass**

```bash
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~SeedTestGameNightCommandHandlerTests"
```

Expected: PASS (6 tests green).

- [ ] **Step 9: Create FluentValidation validator**

Create `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestGameNightCommandValidator.cs`:

```csharp
using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestGameNightCommandValidator : AbstractValidator<SeedTestGameNightCommand>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "Draft", "Published", "InProgress", "Completed"
    };

    public SeedTestGameNightCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}");

        RuleFor(x => x.OwnerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.RosterCount)
            .InclusiveBetween(0, 16);

        RuleFor(x => x.ScoringType)
            .Must(s => s is null or "Points" or "BinaryWin" or "Objectives" or "Ranking")
            .When(x => x.ScoringType is not null);
    }
}
```

- [ ] **Step 10: Write + run validator tests**

Create `apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestGameNightCommandValidatorTests.cs`:

```csharp
using Api.BoundedContexts.Testing.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.Testing.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestGameNightCommandValidatorTests
{
    private readonly SeedTestGameNightCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidCommand_PassesAll()
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            Status = "Published",
            OwnerEmail = "ok@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("not-e2e-prefix")]
    [InlineData("e2e-tooshort-1")]
    [InlineData("")]
    public void Validate_InvalidTestRunId_FailsValidation(string testRunId)
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = "ok@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TestRunId);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("draft")] // case-sensitive
    [InlineData("")]
    public void Validate_InvalidStatus_FailsValidation(string status)
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            Status = status,
            OwnerEmail = "ok@e2e.test",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Validate_InvalidEmail_FailsValidation()
    {
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-validcase01234-1717603200000",
            Status = "Draft",
            OwnerEmail = "not-an-email",
        };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.OwnerEmail);
    }
}
```

Run:
```bash
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~SeedTestGameNightCommandValidatorTests"
```

Expected: PASS (4 tests green).

- [ ] **Step 11: Commit Task 1**

```bash
git add apps/api/src/Api/BoundedContexts/Testing/ \
        apps/api/tests/Api.Tests/Unit/Testing/SeedTestGameNightCommandHandlerTests.cs \
        apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestGameNightCommandValidatorTests.cs \
        apps/api/src/Api/Infrastructure/Persistence/ApplicationDbContext.cs
git commit -m "feat(testing): #1928 T1 SeedTestGameNightCommand + handler + validator + tests

DEC-B-1 Opt A MediatR canonical: 1st of 4 commands. testRunId shadow
property stamping via OnModelCreating config. 6 handler tests (status
variants + parallel safety) + 4 validator tests (format + status enum).

Refs #1928"
```

---

## Task 2: `SeedTestSessionCommand` + Handler + Validator + Unit Tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestSessionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestSessionCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestSessionCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestSessionResponse.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/SeedTestSessionCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestSessionCommandValidatorTests.cs`

- [ ] **Step 1: Write failing handler test (Session.IsLive happy path)**

Create `SeedTestSessionCommandHandlerTests.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.Testing.Application.Commands;
using Api.BoundedContexts.Testing.Application.DTOs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.Testing;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestSessionCommandHandlerTests : IClassFixture<TestDbContextFixture>
{
    private readonly TestDbContextFixture _fixture;
    public SeedTestSessionCommandHandlerTests(TestDbContextFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Handle_IsLiveTrue_CreatesSessionWithStartedAtSetAndFinalizedAtNull()
    {
        // Arrange — seed parent GameNight via T1 handler first
        await using var db = _fixture.CreateDbContext();
        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var gnResponse = await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = "e2e-sessionlive123-1717603200000",
            Status = "Published",
            OwnerEmail = "gnowner@e2e.test",
        }, CancellationToken.None);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = "e2e-sessionlive123-1717603200000",
            GameNightId = gnResponse.GameNightId,
            IsLive = true,
            ScoreType = "Points",
        };

        // Act
        var response = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        var session = await db.Set<GameNightSession>().SingleAsync(s => s.Id == response.SessionId);
        session.StartedAt.Should().NotBeNull();
        session.FinalizedAt.Should().BeNull();
        var testRunIdShadow = db.Entry(session).Property<string?>("TestRunId").CurrentValue;
        testRunIdShadow.Should().Be("e2e-sessionlive123-1717603200000");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~SeedTestSessionCommandHandlerTests"
```

Expected: FAIL `SeedTestSessionCommand not found`.

- [ ] **Step 3: Create Response DTO + Command record**

Create `apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestSessionResponse.cs`:

```csharp
namespace Api.BoundedContexts.Testing.Application.DTOs;

public sealed record SeedTestSessionResponse(
    Guid SessionId,
    Guid GameNightId,
    bool IsLive,
    string TestRunId);
```

Create `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestSessionCommand.cs`:

```csharp
using Api.BoundedContexts.Testing.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1) — Seed a GameNightSession for E2E scenarios.
/// IsLive=true sets StartedAt to now + FinalizedAt=null (matches
/// <c>Session.IsLive</c> invariant from domain model spec).
/// </summary>
public sealed record SeedTestSessionCommand : IRequest<SeedTestSessionResponse>
{
    public required string TestRunId { get; init; }
    public required Guid GameNightId { get; init; }
    public required bool IsLive { get; init; }
    public string? ScoreType { get; init; } // Points | BinaryWin | Objectives | Ranking
}
```

- [ ] **Step 4: Create handler**

Create `SeedTestSessionCommandHandler.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.Testing.Application.DTOs;
using Api.BoundedContexts.Testing.Infrastructure;
using Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestSessionCommandHandler
    : IRequestHandler<SeedTestSessionCommand, SeedTestSessionResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SeedTestSessionCommandHandler> _logger;

    public SeedTestSessionCommandHandler(
        ApplicationDbContext db,
        ILogger<SeedTestSessionCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestSessionResponse> Handle(
        SeedTestSessionCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var gameNight = await _db.GameNightEvents
            .Include(g => g.Sessions)
            .SingleOrDefaultAsync(g => g.Id == request.GameNightId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"GameNight {request.GameNightId} not found for testRunId {request.TestRunId}");

        // Use domain factory + transition for invariant safety
        var session = gameNight.AddSession(scoreType: request.ScoreType ?? "Points");
        if (request.IsLive)
        {
            gameNight.StartCurrentSession();
        }

        _db.Entry(session).Property<string?>(TestRunIdMetadata.ShadowPropertyName)
            .CurrentValue = request.TestRunId;

        await _db.SaveChangesAsync(cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded Session {SessionId} gameNight={GameNightId} isLive={IsLive} testRunId={TestRunId} durationMs={Duration}",
            session.Id, request.GameNightId, request.IsLive, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestSessionResponse(session.Id, request.GameNightId, request.IsLive, request.TestRunId);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

```bash
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~SeedTestSessionCommandHandlerTests"
```

Expected: PASS.

- [ ] **Step 6: Add 5 more handler tests**

For each test below, follow the Arrange/Act/Assert template from Step 1 (seed parent GameNight via T1 handler → instantiate `SeedTestSessionCommandHandler` → call `Handle` → assert invariant). Vary `IsLive`/`ScoreType` inputs and `Status` of parent GameNight per scenario:

- `Handle_IsLiveFalse_CreatesSessionWithStartedAtNullAndFinalizedAtNull` — set `IsLive: false` in command; assert `session.StartedAt.Should().BeNull()`.
- `Handle_ScoreType_BinaryWin_PersistsScoreType` — set `ScoreType: "BinaryWin"`; assert `session.ScoreType.Should().Be("BinaryWin")`.
- `Handle_GameNightNotFound_ThrowsInvalidOperationException` — pass `GameNightId = Guid.NewGuid()` (not seeded); assert `await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*")`.
- `Handle_MaxOneLiveSession_SecondLiveAttemptFailsViaDomain` — seed Status=InProgress (already has live session) → call handler with `IsLive: true`; assert `await action.Should().ThrowAsync<MaxLiveSessionsExceededException>()` (domain invariant #10 enforcement).
- `Handle_MultipleNonLiveSessions_AllowedForSameGameNight` — call handler twice on same GameNight with `IsLive: false`; assert both succeed, `db.Set<GameNightSession>().Count(s => s.GameNightId == gn.GameNightId).Should().Be(2)`.

- [ ] **Step 7: Run all handler tests**

Expected: PASS (6 tests green).

- [ ] **Step 8: Create validator + tests**

Create `SeedTestSessionCommandValidator.cs` (analog to T1 step 9):

```csharp
using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestSessionCommandValidator : AbstractValidator<SeedTestSessionCommand>
{
    private static readonly HashSet<string> AllowedScoreTypes = new(StringComparer.Ordinal)
    {
        "Points", "BinaryWin", "Objectives", "Ranking"
    };

    public SeedTestSessionCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.GameNightId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.ScoreType)
            .Must(s => s is null || AllowedScoreTypes.Contains(s))
            .WithMessage($"ScoreType must be one of: {string.Join(", ", AllowedScoreTypes)}")
            .When(x => x.ScoreType is not null);
    }
}
```

Create `SeedTestSessionCommandValidatorTests.cs` (4 tests: valid + invalid testRunId + empty GameNightId + invalid scoreType).

- [ ] **Step 9: Run validator tests**

Expected: PASS (4 tests green).

- [ ] **Step 10: Commit Task 2**

```bash
git add apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestSession* \
        apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestSessionResponse.cs \
        apps/api/tests/Api.Tests/Unit/Testing/SeedTestSessionCommandHandlerTests.cs \
        apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestSessionCommandValidatorTests.cs
git commit -m "feat(testing): #1928 T2 SeedTestSessionCommand + handler + validator + tests

DEC-B-1 Opt A 2nd of 4 commands. Session.IsLive invariant enforced via
domain StartCurrentSession transition. 6 handler tests (live/non-live +
domain invariant) + 4 validator tests.

Refs #1928"
```

---

## Task 3: `SeedTestPlayerCommand` + Handler + Validator + Unit Tests

**Files:** parallel to T1/T2 structure
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestPlayerCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestPlayerCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestPlayerCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestPlayerResponse.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/SeedTestPlayerCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestPlayerCommandValidatorTests.cs`

### Command shape

```csharp
public sealed record SeedTestPlayerCommand : IRequest<SeedTestPlayerResponse>
{
    public required string TestRunId { get; init; }
    public required Guid GameNightId { get; init; }
    public required string Role { get; init; } // host | player | guest
    public Guid? UserId { get; init; } // null => create guest stub
    public string? DisplayName { get; init; } // required for guest, optional for User-linked
}

public sealed record SeedTestPlayerResponse(
    Guid PlayerId,
    Guid GameNightId,
    string Role,
    bool IsGuest,
    string TestRunId);
```

### Handler logic

- `Role = "host"` → reuse existing GameNight owner (verify it matches via parent GameNight lookup, fail if mismatch)
- `Role = "player"` → User-linked Invitation accepted RSVP
- `Role = "guest"` → guest stub (no User account) via `GameNightInvitation` con `IsGuest=true`
- `UserId` optional: if null → guest stub; if provided → User-linked
- testRunId stamping on Invitation entity

- [ ] **Step 1-10:** Identical TDD cycle to T2 (failing test → command → handler → run pass → more tests → validator + tests → commit).

Validator rules:
```csharp
RuleFor(x => x.TestRunId).NotEmpty().Must(TestRunIdMetadata.IsValid);
RuleFor(x => x.GameNightId).NotEqual(Guid.Empty);
RuleFor(x => x.Role).NotEmpty().Must(r => r is "host" or "player" or "guest");
RuleFor(x => x.DisplayName).NotEmpty().When(x => x.Role == "guest" || x.UserId is null);
```

Handler tests (6 minimum):
- `Handle_HostRole_LinksToExistingOwner`
- `Handle_PlayerRole_WithUserId_CreatesUserLinkedInvitationAccepted`
- `Handle_GuestRole_WithoutUserId_CreatesGuestStubInvitation`
- `Handle_GameNightNotFound_Throws`
- `Handle_HostMismatch_Throws` (Role=host but UserId != owner)
- `Handle_TestRunIdStamp_AppliedToInvitation`

- [ ] **Commit Task 3:**

```bash
git add apps/api/src/Api/BoundedContexts/Testing/Application/Commands/SeedTestPlayer* \
        apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/SeedTestPlayerResponse.cs \
        apps/api/tests/Api.Tests/Unit/Testing/SeedTestPlayerCommandHandlerTests.cs \
        apps/api/tests/Api.Tests/Unit/Testing/Validators/SeedTestPlayerCommandValidatorTests.cs
git commit -m "feat(testing): #1928 T3 SeedTestPlayerCommand + handler + validator + tests

DEC-B-1 Opt A 3rd of 4 commands. host|player|guest role with optional
UserId (null=guest stub). 6 handler tests (3 roles + guard + stamp) +
4 validator tests.

Refs #1928"
```

---

## Task 4: `CleanupTestEntitiesCommand` + Handler + Validator + Unit Tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/CleanupTestEntitiesCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/CleanupTestEntitiesCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/Commands/CleanupTestEntitiesCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/CleanupTestEntitiesResponse.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/CleanupTestEntitiesCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/Unit/Testing/Validators/CleanupTestEntitiesCommandValidatorTests.cs`

### Command + Response shape

```csharp
public sealed record CleanupTestEntitiesCommand : IRequest<CleanupTestEntitiesResponse>
{
    public required string TestRunId { get; init; }
}

public sealed record CleanupTestEntitiesResponse(
    string TestRunId,
    int DeletedGameNights,
    int DeletedSessions,
    int DeletedInvitations,
    int DeletedUsers,
    long DurationMs);
```

### Handler logic

Delete cascade by `TestRunId` shadow property scope:
1. Sessions: `WHERE TestRunId = @testRunId`
2. Invitations + RSVPs: `WHERE TestRunId = @testRunId`
3. GameNightEvents: `WHERE TestRunId = @testRunId`
4. Users (last, FK dependents): `WHERE TestRunId = @testRunId`

Use `ExecuteDeleteAsync()` (EF Core 7+) for bulk delete with single SQL per table. Return counts in response.

- [ ] **Step 1: Write failing test (cleanup deletes scoped entities)**

```csharp
[Fact]
public async Task Handle_TestRunId_DeletesAllScopedEntities_PreservesOthers()
{
    // Arrange — seed 2 separate testRunId scopes
    await using var db = _fixture.CreateDbContext();
    var testRunIdA = "e2e-cleanupA01234-1717603200000";
    var testRunIdB = "e2e-cleanupB01234-1717603200000";

    await SeedAsync(db, testRunIdA); // 1 GN + 2 player + 1 session
    await SeedAsync(db, testRunIdB); // 1 GN + 1 player + 0 session

    // Act — cleanup only testRunIdA
    var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
    var response = await handler.Handle(
        new CleanupTestEntitiesCommand { TestRunId = testRunIdA },
        CancellationToken.None);

    // Assert — A deleted, B preserved
    response.DeletedGameNights.Should().Be(1);
    response.DeletedSessions.Should().Be(1);
    var remainingA = await db.GameNightEvents.AnyAsync(g =>
        EF.Property<string?>(g, "TestRunId") == testRunIdA);
    var remainingB = await db.GameNightEvents.AnyAsync(g =>
        EF.Property<string?>(g, "TestRunId") == testRunIdB);
    remainingA.Should().BeFalse();
    remainingB.Should().BeTrue();
}

private async Task SeedAsync(ApplicationDbContext db, string testRunId)
{
    // Use T1+T2+T3 handlers to seed test data
    var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
    var sessHandler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
    var playerHandler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);

    var gn = await gnHandler.Handle(new SeedTestGameNightCommand
    {
        TestRunId = testRunId,
        Status = "Published",
        OwnerEmail = $"owner-{testRunId[..16]}@e2e.test",
    }, CancellationToken.None);
    // ... etc
}
```

- [ ] **Step 2-10:** TDD cycle (test → handler → pass → more tests → validator + tests → commit).

Handler tests (8 minimum):
- `Handle_HappyPath_DeletesAllScopedEntities_PreservesOthers` (above)
- `Handle_EmptyScope_TestRunIdNoEntities_ReturnsZeros`
- `Handle_OnlyGameNight_NoSessions_DeletesGameNightAndUser`
- `Handle_LoudFailure_DbConstraint_PropagatesException` (verify loud not silent per DEC-B-3)
- `Handle_IdempotentRetry_SecondCallReturnsZeros`
- `Handle_ParallelCleanups_DifferentTestRunIds_NoCollision`
- `Handle_StructuredLog_EmitsTestRunIdAndCounts` (verify MIN-B-1 observability)
- `Handle_PerformanceSla_Under500ms_For10EntityScope` (smoke test, not strict)

Validator: `TestRunId` non-empty + format match (3 tests).

- [ ] **Commit Task 4:**

```bash
git add apps/api/src/Api/BoundedContexts/Testing/Application/Commands/CleanupTestEntities* \
        apps/api/src/Api/BoundedContexts/Testing/Application/DTOs/CleanupTestEntitiesResponse.cs \
        apps/api/tests/Api.Tests/Unit/Testing/CleanupTestEntitiesCommandHandlerTests.cs \
        apps/api/tests/Api.Tests/Unit/Testing/Validators/CleanupTestEntitiesCommandValidatorTests.cs
git commit -m "feat(testing): #1928 T4 CleanupTestEntitiesCommand + cascade by testRunId

DEC-B-1 Opt A 4th of 4 commands. ExecuteDeleteAsync bulk cascade by
TestRunId shadow property scope. DEC-B-3 per-test cleanup semantics
(loud failure, idempotent retry-safe). 8 handler tests (cascade +
isolation + perf smoke) + 3 validator tests.

Refs #1928"
```

---

## Task 5: Admin Endpoint Group + Conditional Registration + Integration Tests

**Files:**
- Create: `apps/api/src/Api/Routing/Admin/AdminTestSeedEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs` (conditional registration)
- Test: `apps/api/tests/Api.Tests/Integration/Testing/AdminTestSeedEndpointsIntegrationTests.cs`

- [ ] **Step 1: Write failing integration test (POST seed game-night happy path with admin auth)**

Create `apps/api/tests/Api.Tests/Integration/Testing/AdminTestSeedEndpointsIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.Testing.Application.Commands;
using Api.BoundedContexts.Testing.Application.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Testing;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "Testing")]
public class AdminTestSeedEndpointsIntegrationTests
    : IClassFixture<IntegrationWebApplicationFactory>
{
    private readonly IntegrationWebApplicationFactory _factory;

    public AdminTestSeedEndpointsIntegrationTests(IntegrationWebApplicationFactory factory)
    {
        // Override env to enable seeding endpoints for these tests
        Environment.SetEnvironmentVariable("E2E_SEEDING_ENABLED", "true");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        _factory = factory;
    }

    [Fact]
    public async Task Post_SeedGameNight_AsAdmin_Returns200WithIds()
    {
        // Arrange — login as admin (existing pattern from other integration tests)
        var client = await _factory.LoginAsAdminAsync();
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-integration0001-1717603200000",
            Status = "Published",
            OwnerEmail = "integration-owner@e2e.test",
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/admin/test/seed/game-night", cmd);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SeedTestGameNightResponse>();
        body.Should().NotBeNull();
        body!.GameNightId.Should().NotBe(Guid.Empty);
        body.TestRunId.Should().Be("e2e-integration0001-1717603200000");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~AdminTestSeedEndpointsIntegrationTests"
```

Expected: FAIL with `404 Not Found` (endpoint non registrato).

- [ ] **Step 3: Create AdminTestSeedEndpoints.cs**

```csharp
using Api.BoundedContexts.Testing.Application.Commands;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Api.Routing.Admin;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-4) — Admin endpoint group for E2E entity seeding.
/// Triple gate: env <c>E2E_SEEDING_ENABLED=true</c> + <c>ASPNETCORE_ENVIRONMENT != Production</c>
/// (Program.cs conditional registration) + <see cref="RequireAdminSessionFilter"/>.
/// </summary>
internal static class AdminTestSeedEndpoints
{
    public static RouteGroupBuilder MapAdminTestSeedEndpoints(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/game-night", HandleSeedGameNight)
            .WithName("AdminTestSeed_GameNight")
            .WithTags("Admin", "TestSeeding");

        group.MapPost("/session", HandleSeedSession)
            .WithName("AdminTestSeed_Session")
            .WithTags("Admin", "TestSeeding");

        group.MapPost("/player", HandleSeedPlayer)
            .WithName("AdminTestSeed_Player")
            .WithTags("Admin", "TestSeeding");

        group.MapPost("/cleanup", HandleCleanup)
            .WithName("AdminTestSeed_Cleanup")
            .WithTags("Admin", "TestSeeding");

        return group;
    }

    private static async Task<IResult> HandleSeedGameNight(
        SeedTestGameNightCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSeedSession(
        SeedTestSessionCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSeedPlayer(
        SeedTestPlayerCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCleanup(
        CleanupTestEntitiesCommand command,
        IMediator mediator,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
```

- [ ] **Step 4: Add conditional registration in Program.cs**

Modify `apps/api/src/Api/Program.cs` (search for similar pattern e.g., `MapAdminCatalogSeedEndpoints`):

```csharp
// Issue #1928 Task B — E2E test seeding endpoints (TRIPLE GATE):
// 1. Env var E2E_SEEDING_ENABLED=true MUST be set (CI E2E job only)
// 2. ASPNETCORE_ENVIRONMENT != Production (Program startup throws if both set in Production — see early-startup check)
// 3. RequireAdminSessionFilter on group (401/403 enforced per request)
if (!app.Environment.IsProduction()
    && app.Configuration.GetValue<bool>("E2E_SEEDING_ENABLED"))
{
    app.MapGroup("/api/v1/admin/test/seed")
       .MapAdminTestSeedEndpoints();
    logger.LogInformation("E2E test seeding endpoints REGISTERED (env={Env}, flag={Flag})",
        app.Environment.EnvironmentName, true);
}
```

- [ ] **Step 5: Run integration test to verify it passes**

```bash
dotnet test ../tests/Api.Tests/ --filter "FullyQualifiedName~AdminTestSeedEndpointsIntegrationTests"
```

Expected: PASS (1 test green).

- [ ] **Step 6: Add 7 more integration tests (auth scenarios + all 4 endpoints + flag off)**

Append:
- `Post_SeedGameNight_NoSession_Returns401`
- `Post_SeedGameNight_AsNonAdmin_Returns403`
- `Post_SeedGameNight_InvalidTestRunId_Returns400` (FluentValidation pipeline)
- `Post_SeedSession_AsAdmin_HappyPath_Returns200`
- `Post_SeedPlayer_AsAdmin_HappyPath_Returns200`
- `Post_Cleanup_AsAdmin_HappyPath_Returns200`
- `Post_SeedGameNight_FlagOff_Returns404` (endpoint non registrato quando flag off — separato class fixture senza env)

- [ ] **Step 7: Run all integration tests**

Expected: PASS (8 tests green).

- [ ] **Step 8: Commit Task 5**

```bash
git add apps/api/src/Api/Routing/Admin/AdminTestSeedEndpoints.cs \
        apps/api/src/Api/Program.cs \
        apps/api/tests/Api.Tests/Integration/Testing/AdminTestSeedEndpointsIntegrationTests.cs
git commit -m "feat(testing): #1928 T5 admin endpoint group + conditional registration

DEC-B-1 + DEC-B-4 — 4 endpoint MediatR-only (POST seed/{game-night,session,
player,cleanup}) under RequireAdminSessionFilter + conditional registration
gate ENV=Test+E2E_SEEDING_ENABLED=true. 8 integration tests covering auth
scenarios (401/403) + happy path 200 + flag off 404.

Refs #1928"
```

---

## Task 6: Triple Gate Startup Fail-Fast + Integration Tests

**Files:**
- Modify: `apps/api/src/Api/Program.cs` (early startup throw)
- Test: `apps/api/tests/Api.Tests/Integration/Testing/TripleGateStartupTests.cs`

- [ ] **Step 1: Write failing test (env=Production + flag=true throws InvalidOperationException at startup)**

Create `apps/api/tests/Api.Tests/Integration/Testing/TripleGateStartupTests.cs`:

```csharp
using Api;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api.Tests.Integration.Testing;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "Testing")]
public class TripleGateStartupTests
{
    [Fact]
    public void Startup_ProductionEnv_WithE2EFlagEnabled_ThrowsInvalidOperationException()
    {
        // Arrange + Act + Assert
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        Environment.SetEnvironmentVariable("E2E_SEEDING_ENABLED", "true");

        var action = () =>
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
            _ = factory.Services; // Forces startup
        };

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*E2E_SEEDING_ENABLED=true*FORBIDDEN*Production*");

        // Cleanup
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("E2E_SEEDING_ENABLED", null);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Expected: FAIL (startup does NOT throw yet).

- [ ] **Step 3: Add early startup gate in Program.cs**

Modify `apps/api/src/Api/Program.cs` (EARLY in `Main()`, before `builder.Services.Add*`):

```csharp
// Issue #1928 Task B (DEC-B-4) — Triple gate STARTUP fail-fast.
// Refuses to start if BOTH E2E_SEEDING_ENABLED=true AND ASPNETCORE_ENVIRONMENT=Production.
// This is defense-in-depth: even if a deployment misconfigures the flag, the app
// will refuse to boot rather than expose admin test endpoints in production.
if (builder.Environment.IsProduction()
    && builder.Configuration.GetValue<bool>("E2E_SEEDING_ENABLED"))
{
    throw new InvalidOperationException(
        "E2E_SEEDING_ENABLED=true is FORBIDDEN in Production environment. " +
        "Refusing to start. See docs/for-developers/testing/e2e-entity-seeding.md " +
        "section 'Env failure recovery'.");
}
```

- [ ] **Step 4: Run test to verify it passes**

Expected: PASS.

- [ ] **Step 5: Add 2 more integration tests**

Append:
- `Startup_TestingEnv_WithE2EFlagEnabled_StartsSuccessfully` (positive case, no throw)
- `Startup_ProductionEnv_WithE2EFlagDisabled_StartsSuccessfully` (production normal)

- [ ] **Step 6: Run all triple gate tests**

Expected: PASS (3 tests green).

- [ ] **Step 7: Commit Task 6**

```bash
git add apps/api/src/Api/Program.cs \
        apps/api/tests/Api.Tests/Integration/Testing/TripleGateStartupTests.cs
git commit -m "feat(testing): #1928 T6 triple gate startup fail-fast

DEC-B-4 defense-in-depth: app refuses to boot if Production+E2E flag.
3 integration tests verify positive (Test+flag=OK, Prod+!flag=OK) and
negative (Prod+flag=throw InvalidOperationException at startup).

Refs #1928"
```

---

## Task 7: TypeScript Factory + Demo Spec Journey #1 + Docs + CI

**Files:**
- Create: `apps/web/e2e/_helpers/seedEntities.ts`
- Create: `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`
- Create: `docs/for-developers/testing/e2e-entity-seeding.md`
- Modify: `.github/workflows/ci.yml` (E2E_SEEDING_ENABLED=true env per Playwright job)

- [ ] **Step 1: Write failing demo spec (Journey #1 dashboard drawer stack)**

Create `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`:

```typescript
import { test, expect } from '@playwright/test';
import { seedAuthSession, mockAuthEndpoints } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import {
  seedGameNight,
  seedPlayer,
  cleanupTestEntities,
  newTestRunId,
} from './_helpers/seedEntities';

/**
 * Issue #1928 Task B (DEC-B-6) — Golden test handoff to Task C.
 * Pre-flight verification: BE seeding + admin endpoint + triple gate + TS
 * factory + demo wire all work end-to-end. If this passes, Task C Journey #1
 * full implementation can begin.
 *
 * Scope (BE seeding focus only):
 *   - Login admin
 *   - Seed 1 GameNight Published + 2 player roster via factory
 *   - Login as Anna (host) — separate session
 *   - Navigate /dashboard
 *   - Assert seeded GN appears in "Prossimi" section
 *   - cleanupTestEntities verifies 0 row remaining
 *
 * NOT in scope: drawer-stack ESC cascade, prefers-reduced-motion (Task C).
 */
test.describe('Cross-asse Journey #1 — Dashboard drawer stack (BE pre-flight)', () => {
  let testRunId: string;

  test.beforeEach(async ({ page }) => {
    testRunId = newTestRunId(test.info().testId);
    await seedAuthSession(page, { role: 'admin' });
    await mockAuthEndpoints(page, { role: 'admin' });
    await seedCookieConsent(page);
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  test('seeds GameNight + Player + appears in dashboard Prossimi section', async ({ page }) => {
    // Arrange — seed via BE factory
    const gn = await seedGameNight(page, {
      testRunId,
      status: 'Published',
      ownerEmail: 'anna-host@e2e.test',
    });
    expect(gn.gameNightId).toBeTruthy();

    const player1 = await seedPlayer(page, {
      testRunId,
      gameNightId: gn.gameNightId,
      role: 'player',
      displayName: 'E2E Player 1',
    });
    const player2 = await seedPlayer(page, {
      testRunId,
      gameNightId: gn.gameNightId,
      role: 'guest',
      displayName: 'E2E Guest 2',
    });
    expect(player1.playerId).toBeTruthy();
    expect(player2.playerId).toBeTruthy();

    // Act — navigate dashboard as host
    await page.goto('/dashboard');

    // Assert — seeded GN appears in Prossimi (functional assertion, DEC-C-2 hybrid)
    await expect(page.getByRole('heading', { name: /Prossimi/i })).toBeVisible();
    const gnCard = page.getByTestId(`dashboard-prossimi-card-${gn.gameNightId}`);
    await expect(gnCard).toBeVisible({ timeout: 5000 });
  });
});
```

- [ ] **Step 2: Run test to verify it fails (TS factory non esiste)**

```bash
cd apps/web
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: FAIL with `Cannot find module './_helpers/seedEntities'`.

- [ ] **Step 3: Create TS factory `seedEntities.ts`**

Create `apps/web/e2e/_helpers/seedEntities.ts`:

```typescript
/**
 * Issue #1928 Task B (DEC-B-2 + DEC-B-5) — TypeScript factory wrapper for E2E
 * entity seeding via admin endpoint POST /api/v1/admin/test/seed/*.
 *
 * **Contract**:
 *   - Caller pre-seeds admin session via seedAuthSession(page, { role: 'admin' })
 *   - All factory calls require testRunId (forced via API)
 *   - cleanupTestEntities MUST be called in test.afterEach (DEC-B-3)
 *
 * **Triple gate enforced backend-side** (DEC-B-4):
 *   - env E2E_SEEDING_ENABLED=true
 *   - ASPNETCORE_ENVIRONMENT != Production
 *   - RequireAdminSessionFilter
 *
 * Backend ref: apps/api/src/Api/Routing/Admin/AdminTestSeedEndpoints.cs
 */
import type { Page } from '@playwright/test';

const SEED_BASE = '/api/v1/admin/test/seed';

export type GameNightStatus = 'Draft' | 'Published' | 'InProgress' | 'Completed';
export type ScoringType = 'Points' | 'BinaryWin' | 'Objectives' | 'Ranking';
export type PlayerRole = 'host' | 'player' | 'guest';

/**
 * Generates canonical testRunId format: `e2e-{testId}-{epochMs}`.
 * Pass test.info().testId from Playwright fixture.
 */
export function newTestRunId(testId: string): string {
  const cleanId = testId.replace(/[^a-zA-Z0-9]/g, '').slice(0, 32).padEnd(8, '0');
  return `e2e-${cleanId}-${Date.now()}`;
}

export async function seedGameNight(
  page: Page,
  opts: {
    testRunId: string;
    status: GameNightStatus;
    ownerEmail: string;
    scoringType?: ScoringType;
    rosterCount?: number;
  },
): Promise<{ gameNightId: string; ownerId: string; testRunId: string }> {
  const response = await page.request.post(`${SEED_BASE}/game-night`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedGameNight failed (${response.status()}): ${body}`);
  }
  return await response.json();
}

export async function seedSession(
  page: Page,
  opts: {
    testRunId: string;
    gameNightId: string;
    isLive: boolean;
    scoreType?: ScoringType;
  },
): Promise<{ sessionId: string; gameNightId: string; isLive: boolean; testRunId: string }> {
  const response = await page.request.post(`${SEED_BASE}/session`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedSession failed (${response.status()}): ${body}`);
  }
  return await response.json();
}

export async function seedPlayer(
  page: Page,
  opts: {
    testRunId: string;
    gameNightId: string;
    role: PlayerRole;
    userId?: string;
    displayName?: string;
  },
): Promise<{ playerId: string; gameNightId: string; role: PlayerRole; isGuest: boolean; testRunId: string }> {
  const response = await page.request.post(`${SEED_BASE}/player`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`seedPlayer failed (${response.status()}): ${body}`);
  }
  return await response.json();
}

export async function cleanupTestEntities(
  page: Page,
  opts: { testRunId: string },
): Promise<{
  testRunId: string;
  deletedGameNights: number;
  deletedSessions: number;
  deletedInvitations: number;
  deletedUsers: number;
  durationMs: number;
}> {
  const response = await page.request.post(`${SEED_BASE}/cleanup`, {
    data: opts,
  });
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`cleanupTestEntities failed (${response.status()}): ${body}`);
  }
  return await response.json();
}
```

- [ ] **Step 4: Run demo spec — needs BE running**

Demo spec requires both BE (with `E2E_SEEDING_ENABLED=true` + admin auth) AND FE dev server. Local prerequisites:

```bash
# Terminal 1: BE with env
cd apps/api/src/Api
ASPNETCORE_ENVIRONMENT=Development \
E2E_SEEDING_ENABLED=true \
dotnet run

# Terminal 2: FE
cd apps/web
pnpm dev

# Terminal 3: Run demo spec
cd apps/web
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (Demo spec green end-to-end).

> **NOTE**: il `data-testid="dashboard-prossimi-card-{gameNightId}"` deve esistere in Dashboard component. Verifica `apps/web/src/components/features/dashboard/ProssimiSection.tsx` ha `data-testid={\`dashboard-prossimi-card-${gn.id}\`}` sui card. Se manca: aggiungi attribute in commit precedente o coordinato con Task C team.

- [ ] **Step 5: Create docs `e2e-entity-seeding.md`**

Create `docs/for-developers/testing/e2e-entity-seeding.md`:

```markdown
# E2E Entity Seeding Infra (Issue #1928 Task B)

> **Status**: Shipped 2026-06-XX. Reference: [spec consolidato](../../superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md) DEC-B-1..6.

## 1. API Reference

[TypeScript factory contract: full table + usage examples]

## 2. Opt A Architectural Rationale

[Perché admin endpoint MediatR vs direct DB / gRPC / etc. — rationale DEC-B-1 + reference AdminCatalogSeedEndpoints pattern]

## 3. GWT Canonical (Adzic)

[5 GWT scenarios from spec doc: seed happy path, cleanup cascade, admin auth required, env-gate prod refusal, parallel safety]

## 4. CI Ops Runbook

[Quando attivare E2E_SEEDING_ENABLED in workflow, come verificare gate, come disabilitare per troubleshooting]

## 5. Env Failure Recovery

### Symptom: App refuses to start with InvalidOperationException

```
System.InvalidOperationException: E2E_SEEDING_ENABLED=true is FORBIDDEN in Production environment.
```

**Cause**: deployment ha settato `E2E_SEEDING_ENABLED=true` in ambiente Production (DEC-B-4 triple gate startup fail-fast).

**Resolution**:
1. Verifica `ASPNETCORE_ENVIRONMENT` value — se Production, l'env var DEVE essere unset.
2. Rimuovi `E2E_SEEDING_ENABLED` da deployment config (Kubernetes ConfigMap, App Service Settings, etc.).
3. Restart app.

**Defense-in-depth verifica**: il flag DEVE essere settato SOLO in CI E2E job, NEVER in deployment runtime.
```

Lunghezza target: ~250 LOC, 5 sezioni complete.

- [ ] **Step 6: Modify CI workflow**

Modify `.github/workflows/ci.yml` (find Playwright E2E job, add env):

```yaml
jobs:
  e2e-tests:
    # ... existing config
    env:
      # Issue #1928 Task B — E2E seeding endpoint gate (TRIPLE GATE component 1)
      E2E_SEEDING_ENABLED: 'true'
      ASPNETCORE_ENVIRONMENT: 'Testing'
      PLAYWRIGHT_AUTH_BYPASS: 'true'
    steps:
      # ... existing steps
```

> **NOTE**: il workflow file esatto da modificare è quello che esegue Playwright E2E tests. Cerca `pnpm test:e2e` o `playwright test` nelle workflow `.github/workflows/*.yml`.

- [ ] **Step 7: Commit Task 7**

```bash
git add apps/web/e2e/_helpers/seedEntities.ts \
        apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts \
        docs/for-developers/testing/e2e-entity-seeding.md \
        .github/workflows/ci.yml
git commit -m "feat(testing): #1928 T7 TS factory + demo spec + docs + CI gate

DEC-B-2 + DEC-B-5 + DEC-B-6 — TypeScript factory wrapper via page.request,
demo spec Journey #1 pre-flight (golden test handoff to Task C #1929),
5-section docs + CI workflow env gate.

Closes #1928"
```

---

## Final integration check

- [ ] **Step 1: Run full BE unit test suite**

```bash
cd apps/api/src/Api
dotnet test ../tests/Api.Tests/ --filter "Category=Unit&BoundedContext=Testing"
```

Expected: PASS (~30 tests green: 6+6+6+8 handler + 4+4+4+3 validator).

- [ ] **Step 2: Run full BE integration test suite**

```bash
dotnet test ../tests/Api.Tests/ --filter "Category=Integration&BoundedContext=Testing"
```

Expected: PASS (~11 tests green: 8 endpoint + 3 triple gate).

- [ ] **Step 3: Run all BE tests + verify no regression**

```bash
dotnet test ../tests/Api.Tests/
```

Expected: PASS (no regression on existing tests).

- [ ] **Step 4: Run demo spec locally with full stack**

```bash
# Terminal 1: BE
cd apps/api/src/Api
ASPNETCORE_ENVIRONMENT=Development E2E_SEEDING_ENABLED=true dotnet run

# Terminal 2: FE
cd apps/web && pnpm dev

# Terminal 3: Demo spec
cd apps/web
pnpm exec playwright test cross-asse-journey-1-dashboard-drawer-stack.spec.ts
```

Expected: PASS (golden test handoff verified).

- [ ] **Step 5: Push branch + open PR**

```bash
git push -u origin feature/issue-1928-be-seeding-infra
gh pr create --base main-dev --title "feat(testing): #1928 Task B — BE entity seeding infra (E2E data-driven)" --body "$(cat <<'EOF'
## Summary

Implements BE+FE infrastructure for Playwright E2E data-driven entity seeding (GameNight + Session + Player) with deterministic per-test cleanup. Triple-gated admin endpoint MediatR (DEC-B-1) + TypeScript factory wrapper (DEC-B-2) + per-test afterEach + testRunId (DEC-B-3) + startup fail-fast (DEC-B-4) + demo Journey #1 golden test handoff to Task C (DEC-B-6).

**Spec**: [`docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md`](../blob/main-dev/docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md) DEC-B-1..6 (sessione 39).
**Plan**: [`docs/superpowers/plans/2026-06-05-asse-d-p4-task-b-be-seeding-infra.md`](../blob/main-dev/docs/superpowers/plans/2026-06-05-asse-d-p4-task-b-be-seeding-infra.md).

## Files

| Layer | Files | LOC |
|---|---|---|
| Commands BC `Testing` | 4 commands + 4 handlers + 4 validators + 4 response DTOs + TestRunIdMetadata | ~700 |
| Admin endpoint | `AdminTestSeedEndpoints.cs` + Program.cs conditional registration + startup gate | ~150 |
| BE tests | Unit ~30 + Integration ~11 | ~1200 |
| FE | `seedEntities.ts` + demo spec | ~250 |
| Docs | `e2e-entity-seeding.md` 5 sezioni | ~250 |
| CI | ci.yml env gate | ~5 |

Total: ~2500 LOC (delta).

## Test plan

- [x] BE unit tests: 30+ pass (handler + validator)
- [x] BE integration tests: 11+ pass (endpoint auth + triple gate)
- [x] Demo spec Journey #1 locally PASS (golden handoff verified)
- [ ] CI workflow E2E job runs with E2E_SEEDING_ENABLED=true
- [ ] No regression on existing BE tests

## Closes

Closes #1928

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR opened, CI starts.

- [ ] **Step 6: Monitor CI + merge**

After CI green:

```bash
gh pr merge --squash --delete-branch
```

#1928 auto-closes via `Closes #1928` in PR body.

Task C (#1929) unblocked.

---

## Self-Review checklist

### Spec coverage

| Spec requirement | Task | Status |
|---|---|---|
| AC #1: 4 MediatR commands + FluentValidation + xUnit tests | T1-T4 | ✅ |
| AC #2: 4 admin endpoint conditional registration + triple gate verified (env=Prod refuse / Test+no env=404 / Test+env+no auth=401 / Test+env+non-admin=403) | T5+T6 | ✅ |
| AC #3: TypeScript factory + testRunId enforcement | T7 | ✅ |
| AC #4: Demo spec Journey #1 PRE-FLIGHT pass | T7 | ✅ |
| AC #5: Docs `e2e-entity-seeding.md` 5 sezioni | T7 step 5 | ✅ |
| AC #6: CI workflow `E2E_SEEDING_ENABLED=true` solo Playwright E2E job | T7 step 6 | ✅ |
| AC #7: Structured logging per seed call | T1-T4 handler `_logger.LogInformation` | ✅ |
| DEC-B-1 Opt A MediatR canonical | T1-T5 | ✅ |
| DEC-B-2 TS factory via `page.request` | T7 step 3 | ✅ |
| DEC-B-3 per-test afterEach + testRunId scoped | T7 demo spec `test.afterEach` | ✅ |
| DEC-B-4 triple gate (env + ASPNETCORE startup + AdminFilter) | T5 + T6 | ✅ |
| DEC-B-5 testRunId forzato via factory API | T1-T4 `required string TestRunId` | ✅ |
| DEC-B-6 demo Journey #1 golden handoff | T7 | ✅ |
| GWT canonical 5 scenarios | docs T7 step 5 section 3 | ✅ |

### Placeholder scan

- [x] No "TBD" / "implement later"
- [x] No "Similar to Task N" without code (T3 uses T2 pattern reference but specifies command shape + handler logic concrete)
- [x] All code blocks contain full implementations or explicit test assertions
- [x] All `dotnet test` / `pnpm exec playwright test` commands include exact filter args
- [x] All `git commit` messages drafted

### Type consistency

- [x] `testRunId` format string `e2e-{testId}-{epochMs}` consistent T1-T7 (validator + factory + helper regex)
- [x] `SeedTestGameNightCommand.Status` enum allowed values match handler switch (Draft/Published/InProgress/Completed)
- [x] `SeedTestSessionCommand.ScoreType` matches scoring polymorphic enum (Points/BinaryWin/Objectives/Ranking)
- [x] `SeedTestPlayerCommand.Role` enum (host/player/guest) consistent FE factory + handler + validator
- [x] Response DTO field names camelCase consistent (matches default JsonSerializer policy)
- [x] `TestRunIdMetadata.ShadowPropertyName = "TestRunId"` referenced T1-T4 handler + cleanup `EF.Property<string?>(g, "TestRunId")`

### Edge cases identified

- [x] Parallel test runs different testRunId (T1 step 7 test + T4 step 2 test)
- [x] Empty scope cleanup (T4 step 2 `Handle_EmptyScope_ReturnsZeros`)
- [x] Idempotent cleanup retry (T4 step 2 `Handle_IdempotentRetry_ReturnsZeros`)
- [x] DB constraint loud failure (T4 step 2 `Handle_LoudFailure_PropagatesException`)
- [x] Triple gate negative case Production+flag (T6 step 1)
- [x] Auth 401/403 enforcement (T5 step 6)
- [x] Flag off endpoint not registered (T5 step 6 `Post_SeedGameNight_FlagOff_Returns404`)

---

## Execution Handoff

**Plan complete and saved to** `docs/superpowers/plans/2026-06-05-asse-d-p4-task-b-be-seeding-infra.md`.

Two execution options:

**1. Subagent-Driven (recommended)** — Dispatch fresh subagent per task, review between tasks, fast iteration. Pattern P120 mix-model: T1-T4 haiku (mechanical command+handler+validator boilerplate), T5+T6 sonnet (endpoint integration + startup gate judgment), T7 sonnet (FE+CI+docs orchestration). Estimated 4-6gg total.

**2. Inline Execution** — Execute tasks in this session using `superpowers:executing-plans`, batch execution with checkpoints. Riskier: 4-6gg in single session is heavy.

**Which approach?**

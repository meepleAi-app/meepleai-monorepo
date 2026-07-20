# ToS Acceptance Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist an append-only per-user ToS acceptance record with a server-authoritative version, record acceptance at registration (and via a re-consent endpoint), and expose a `needsReAcceptance` status — WITHOUT any enforcement gate.

**Architecture:** New `TermsAcceptance` entity in the Authentication bounded context, configured directly as an EF entity (mirrors `UserAiConsent`), persisted in append-only table `user_terms_acceptances`. A repository (add + latest-lookup), one command (record/accept), one query (status), two `me`-scoped endpoints, and a register-pipeline extension (payload flag + endpoint 400 enforcement + handler recording). Frontend forwards the acceptance boolean.

**Tech Stack:** .NET 9, EF Core (Postgres), MediatR CQRS, FluentValidation, xUnit + Testcontainers; Next.js 16 + Vitest.

## Global Constraints

- CQRS: endpoints use `IMediator` only — no direct service injection (project rule).
- DI: register both interface and implementation (#2565).
- Exceptions: `ConflictException` (409) / `NotFoundException` (404), never `InvalidOperationException` (#2568). None expected here.
- Enum persisted as **string** (`.HasConversion<string>()`), never int+CHECK (avoids #2974 constraint drift).
- Append-only: `user_terms_acceptances` has **no** unique index on `UserId`.
- Migration is pure EF (no `migrationBuilder.Sql()`).
- ToS version literal: `TermsVersion.Current = "2026-07-15"` (single BE source of truth; matches `terms/page.tsx` `lastUpdated`).
- Test project root: `apps/api/tests/Api.Tests` (NOT `tests/Api.Tests`).
- Backend commands run from `apps/api/src/Api`; tests from `apps/api`.
- Namespaces (verified): `RepositoryBase` → `Api.SharedKernel.Infrastructure`; `IDomainEventCollector` → `Api.SharedKernel.Application.Services`; `IUnitOfWork` → `Api.SharedKernel.Infrastructure.Persistence`; `SessionStatusDto` → `Api.BoundedContexts.Authentication.Application.DTOs`; CQRS interfaces → `Api.SharedKernel.Application.Interfaces`.
- Kill testhost before running backend tests (`tasklist | grep testhost` → `taskkill //PID <PID> //F`) if a run hangs.

---

## File Structure

**Create (BE):**
- `apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/TermsAcceptanceContext.cs` — the acceptance-context enum.
- `apps/api/src/Api/BoundedContexts/Authentication/Domain/Constants/TermsVersion.cs` — current ToS version constant.
- `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/TermsAcceptance.cs` — append-only entity + factory.
- `apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/ITermsAcceptanceRepository.cs`
- `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/TermsAcceptanceRepository.cs`
- `apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/TermsAcceptanceEntityConfiguration.cs`
- `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/TermsConsentStatusDto.cs`
- `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/TermsAcceptance/RecordTermsAcceptanceCommand.cs` (+ handler in same file)
- `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/TermsAcceptance/GetTermsConsentStatusQuery.cs` (+ handler in same file)
- `apps/api/src/Api/Routing/TermsConsentEndpoints.cs`
- EF migration `AddUserTermsAcceptances` (generated).

**Modify (BE):**
- `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommand.cs` — `+ bool TermsAccepted = false`.
- `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler.cs` — inject repo + record on registration.
- `apps/api/src/Api/Models/AuthContracts.cs` — `RegisterPayload.TermsAccepted`.
- `apps/api/src/Api/Routing/AuthenticationEndpoints.cs` — 400 enforcement + pass flag.
- `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs` — DI.
- `apps/api/src/Api/Program.cs` — endpoint group wiring.
- `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/RegisterRaceConditionEndpointTests.cs` — send `termsAccepted:true`.

**Create (BE tests):**
- `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/TermsAcceptanceTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/RecordTermsAcceptanceCommandHandlerTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/GetTermsConsentStatusQueryHandlerTests.cs`
- `apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs`

**Modify (FE):**
- `apps/web/src/lib/api/clients/authClient.ts` — `RegisterRequest.termsAccepted`.
- `apps/web/src/app/(auth)/register/_content.tsx` — forward `termsAccepted: true`.
- `apps/web/src/app/(public)/terms/page.tsx` — cross-ref comment on `lastUpdated`.
- FE tests as noted in Task 6.

---

## Task 1: Domain primitives — enum, version constant, entity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/TermsAcceptanceContext.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Constants/TermsVersion.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/TermsAcceptance.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/TermsAcceptanceTests.cs`

**Interfaces:**
- Produces: `enum TermsAcceptanceContext { Registration, ReConsent }`; `static class TermsVersion { const string Current }`; `TermsAcceptance.Create(Guid userId, string termsVersion, TermsAcceptanceContext context, string? ipAddress = null, string? userAgent = null) : TermsAcceptance` with readable properties `Id, UserId, TermsVersion, AcceptedAt, Context, IpAddress, UserAgent, CreatedAt`.

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/TermsAcceptanceTests.cs`:

```csharp
using System;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

[Trait("Category", TestCategories.Unit)]
public sealed class TermsAcceptanceTests
{
    [Fact]
    public void Create_WithValidInput_SetsAllFields()
    {
        var userId = Guid.NewGuid();

        var acceptance = TermsAcceptance.Create(
            userId, "2026-07-15", TermsAcceptanceContext.Registration, "1.2.3.4", "UA/1.0");

        acceptance.Id.Should().NotBe(Guid.Empty);
        acceptance.UserId.Should().Be(userId);
        acceptance.TermsVersion.Should().Be("2026-07-15");
        acceptance.Context.Should().Be(TermsAcceptanceContext.Registration);
        acceptance.IpAddress.Should().Be("1.2.3.4");
        acceptance.UserAgent.Should().Be("UA/1.0");
        acceptance.AcceptedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        acceptance.CreatedAt.Should().Be(acceptance.AcceptedAt);
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        var act = () => TermsAcceptance.Create(
            Guid.Empty, "2026-07-15", TermsAcceptanceContext.Registration);
        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankVersion_Throws(string? version)
    {
        var act = () => TermsAcceptance.Create(
            Guid.NewGuid(), version!, TermsAcceptanceContext.ReConsent);
        act.Should().Throw<ArgumentException>().WithParameterName("termsVersion");
    }

    [Fact]
    public void Create_AllowsNullAuditFields()
    {
        var acceptance = TermsAcceptance.Create(
            Guid.NewGuid(), "2026-07-15", TermsAcceptanceContext.ReConsent);
        acceptance.IpAddress.Should().BeNull();
        acceptance.UserAgent.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: FAIL — `TermsAcceptance` / `TermsAcceptanceContext` do not exist.

- [ ] **Step 3: Create the enum**

Create `apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/TermsAcceptanceContext.cs`:

```csharp
namespace Api.BoundedContexts.Authentication.Domain.Enums;

/// <summary>
/// Why a ToS acceptance row was recorded (#2954 F1). Persisted as its string
/// name (never as an int) so growing the enum never requires a DB constraint change.
/// </summary>
public enum TermsAcceptanceContext
{
    /// <summary>Recorded during initial account registration.</summary>
    Registration,

    /// <summary>Recorded when the user re-accepts an updated ToS version.</summary>
    ReConsent,
}
```

- [ ] **Step 4: Create the version constant**

Create `apps/api/src/Api/BoundedContexts/Authentication/Domain/Constants/TermsVersion.cs`:

```csharp
namespace Api.BoundedContexts.Authentication.Domain.Constants;

/// <summary>
/// Single server-side source of truth for the current Terms of Service version
/// (#2954 F1). The ToS text lives in the frontend locales (it.json/en.json) and its
/// display date in apps/web/src/app/(public)/terms/page.tsx (lastUpdated). This
/// constant MUST be bumped in the same change whenever that text materially changes.
/// </summary>
public static class TermsVersion
{
    /// <summary>Current ToS version identifier (date-based; matches terms/page.tsx lastUpdated).</summary>
    public const string Current = "2026-07-15";
}
```

- [ ] **Step 5: Create the entity**

Create `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/TermsAcceptance.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.Enums;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Append-only record of a user's acceptance of a specific Terms of Service version
/// (#2954 F1). One row per acceptance event — never updated in place — so the history
/// of which version was accepted when is preserved for legal defensibility.
/// </summary>
public sealed class TermsAcceptance
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TermsVersion { get; private set; } = string.Empty;
    public DateTime AcceptedAt { get; private set; }
    public TermsAcceptanceContext Context { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core
    private TermsAcceptance() { }

    private TermsAcceptance(
        Guid userId, string termsVersion, TermsAcceptanceContext context, string? ipAddress, string? userAgent)
    {
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        UserId = userId;
        TermsVersion = termsVersion;
        AcceptedAt = now;
        Context = context;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CreatedAt = now;
    }

    public static TermsAcceptance Create(
        Guid userId,
        string termsVersion,
        TermsAcceptanceContext context,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (string.IsNullOrWhiteSpace(termsVersion))
            throw new ArgumentException("Terms version is required", nameof(termsVersion));

        return new TermsAcceptance(userId, termsVersion, context, ipAddress, userAgent);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TermsAcceptanceTests"`
Expected: PASS (4 tests).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/TermsAcceptanceTests.cs
git commit -m "feat(auth): TermsAcceptance domain entity + version constant (#2954 F1)"
```

---

## Task 2: Persistence — EF config, repository, DI, migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/TermsAcceptanceEntityConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/ITermsAcceptanceRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/TermsAcceptanceRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs:29`
- Create (generated): EF migration `AddUserTermsAcceptances`
- Test: `apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs` (repo round-trip subset here; full endpoint tests in Task 5)

**Interfaces:**
- Consumes: `TermsAcceptance` (Task 1).
- Produces: `ITermsAcceptanceRepository { Task AddAsync(TermsAcceptance, CancellationToken); Task<TermsAcceptance?> GetLatestByUserIdAsync(Guid, CancellationToken); }`; table `user_terms_acceptances`.

- [ ] **Step 1: Create the repository interface**

Create `apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/ITermsAcceptanceRepository.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

/// <summary>
/// Repository for the append-only TermsAcceptance record (#2954 F1).
/// </summary>
public interface ITermsAcceptanceRepository
{
    /// <summary>
    /// Adds a new acceptance row to the change tracker. Does NOT SaveChanges — the
    /// caller commits via its Unit of Work, so registration can batch it into one
    /// transaction with the new user (mirrors SessionRepository).
    /// </summary>
    Task AddAsync(TermsAcceptance acceptance, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's most recent acceptance (by AcceptedAt), or null if none.</summary>
    Task<TermsAcceptance?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create the EF configuration**

Create `apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/TermsAcceptanceEntityConfiguration.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Authentication;

/// <summary>
/// EF Core configuration for the append-only TermsAcceptance entity (#2954 F1).
/// Auto-applied via ApplyConfigurationsFromAssembly (mirrors UserAiConsent).
/// </summary>
internal sealed class TermsAcceptanceEntityConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> builder)
    {
        builder.ToTable("user_terms_acceptances");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.TermsVersion).IsRequired().HasMaxLength(50);
        builder.Property(e => e.AcceptedAt).IsRequired();

        // Persisted as the enum's string name (not int) → no CHECK-range drift
        // when the enum grows (avoids the #2974 constraint pitfall).
        builder.Property(e => e.Context)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(e => e.IpAddress).HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.CreatedAt).IsRequired();

        // FK to users with cascade delete (no navigation property needed).
        builder.HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Latest-acceptance lookup. Append-only: intentionally NO unique index on UserId.
        builder.HasIndex(e => new { e.UserId, e.AcceptedAt })
            .HasDatabaseName("ix_user_terms_acceptances_user_accepted");
    }
}
```

- [ ] **Step 3: Create the repository**

Create `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/TermsAcceptanceRepository.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for the append-only TermsAcceptance record (#2954 F1).
/// </summary>
public sealed class TermsAcceptanceRepository : RepositoryBase, ITermsAcceptanceRepository
{
    public TermsAcceptanceRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(TermsAcceptance acceptance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        // No SaveChanges here — the caller's Unit of Work commits (mirrors SessionRepository).
        await DbContext.Set<TermsAcceptance>().AddAsync(acceptance, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TermsAcceptance?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TermsAcceptance>()
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.AcceptedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Register in DI**

Modify `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs`. After line 29 (`services.AddScoped<IWaitlistEntryRepository, WaitlistEntryRepository>();`), add:

```csharp
        services.AddScoped<ITermsAcceptanceRepository, TermsAcceptanceRepository>(); // #2954 F1: ToS acceptance record
```

(The `using Api.BoundedContexts.Authentication.Domain.Repositories;` and `...Infrastructure.Persistence;` imports are already present in that file.)

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: PASS (0 errors).

- [ ] **Step 6: Generate the migration**

Run (from repo root):
```bash
cd apps/api/src/Api && dotnet ef migrations add AddUserTermsAcceptances && cd -
```
Expected: creates `Migrations/<timestamp>_AddUserTermsAcceptances.cs` + snapshot update.

- [ ] **Step 7: Verify the migration is correct**

Open the generated migration. Confirm: `CreateTable("user_terms_acceptances")` with columns `Id, UserId, TermsVersion, AcceptedAt, Context (text), IpAddress, UserAgent, CreatedAt`; a FK to `users` with `onDelete: ReferentialAction.Cascade`; index `ix_user_terms_acceptances_user_accepted` on `(UserId, AcceptedAt)`; **NO** `migrationBuilder.Sql(...)`; **NO** unique index on `UserId`. If any of these is wrong, fix the config in Step 2 and regenerate (`dotnet ef migrations remove` then re-add).

- [ ] **Step 8: Write the repository round-trip integration test**

Create `apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs` with the repository round-trip test (endpoint tests are added in Task 5; keep this file and extend it there). Use the existing integration fixture pattern for the BC — locate a sibling integration test in `apps/api/tests/Api.Tests/Integration/` for the exact fixture base class/attribute, and mirror it. The single test for this step:

```csharp
// NOTE: mirror the Testcontainers fixture used by sibling tests in
// apps/api/tests/Api.Tests/Integration/ (base class + [Collection]/[Trait] attributes).
[Fact]
public async Task AddAsync_ThenGetLatest_ReturnsMostRecentAcceptance()
{
    // Arrange: seed a user via the existing user-seed helper of the integration fixture,
    // then resolve ITermsAcceptanceRepository + IUnitOfWork from the scope.
    var userId = /* seeded user id */;
    var repo = /* resolve ITermsAcceptanceRepository */;
    var uow = /* resolve IUnitOfWork */;

    var older = TermsAcceptance.Create(userId, "2026-03-09", TermsAcceptanceContext.Registration);
    await repo.AddAsync(older);
    await uow.SaveChangesAsync();

    var newer = TermsAcceptance.Create(userId, "2026-07-15", TermsAcceptanceContext.ReConsent);
    await repo.AddAsync(newer);
    await uow.SaveChangesAsync();

    // Act
    var latest = await repo.GetLatestByUserIdAsync(userId);

    // Assert
    latest.Should().NotBeNull();
    latest!.TermsVersion.Should().Be("2026-07-15");
    latest.Context.Should().Be(TermsAcceptanceContext.ReConsent);
}
```

> The implementer MUST fill the `/* ... */` slots by copying the exact fixture wiring (Testcontainers Postgres, user seeding, DI scope resolution) from a sibling integration test in the same folder. This is the only place in the plan where the surrounding fixture cannot be shown verbatim — inspect one neighbor first.

- [ ] **Step 9: Run the integration test**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TermsAcceptanceIntegrationTests"`
Expected: PASS. (Requires Docker for Testcontainers.)

- [ ] **Step 10: Commit**

```bash
git add apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/TermsAcceptanceRepository.cs apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs apps/api/src/Api/Migrations apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs
git commit -m "feat(auth): persist append-only user_terms_acceptances (#2954 F1)"
```

---

## Task 3: Application — command, query, DTO, handlers

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/TermsConsentStatusDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/TermsAcceptance/RecordTermsAcceptanceCommand.cs` (command + handler)
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/TermsAcceptance/GetTermsConsentStatusQuery.cs` (query + handler)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/RecordTermsAcceptanceCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/GetTermsConsentStatusQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `ITermsAcceptanceRepository`, `TermsVersion.Current`, `TermsAcceptance`, `IUnitOfWork`.
- Produces: `TermsConsentStatusDto(string CurrentVersion, string? AcceptedVersion, DateTime? AcceptedAt, bool NeedsReAcceptance)`; `RecordTermsAcceptanceCommand(Guid UserId, string? IpAddress = null, string? UserAgent = null) : ICommand<TermsConsentStatusDto>`; `GetTermsConsentStatusQuery(Guid UserId) : IQuery<TermsConsentStatusDto>`.

- [ ] **Step 1: Write the failing handler tests**

Create `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/RecordTermsAcceptanceCommandHandlerTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

[Trait("Category", TestCategories.Unit)]
public sealed class RecordTermsAcceptanceCommandHandlerTests
{
    private readonly Mock<ITermsAcceptanceRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RecordTermsAcceptanceCommandHandler CreateSut() => new(_repo.Object, _uow.Object);

    [Fact]
    public async Task Handle_NoPriorAcceptance_AppendsCurrentVersion()
    {
        _repo.Setup(r => r.GetLatestByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TermsAcceptance?)null);

        var result = await CreateSut().Handle(
            new RecordTermsAcceptanceCommand(Guid.NewGuid()), CancellationToken.None);

        _repo.Verify(r => r.AddAsync(
            It.Is<TermsAcceptance>(a => a.TermsVersion == TermsVersion.Current
                                        && a.Context == TermsAcceptanceContext.ReConsent),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.AcceptedVersion.Should().Be(TermsVersion.Current);
        result.NeedsReAcceptance.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AlreadyAcceptedCurrent_IsNoOp()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, TermsVersion.Current, TermsAcceptanceContext.Registration));

        var result = await CreateSut().Handle(
            new RecordTermsAcceptanceCommand(userId), CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<TermsAcceptance>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        result.NeedsReAcceptance.Should().BeFalse();
        result.AcceptedVersion.Should().Be(TermsVersion.Current);
    }

    [Fact]
    public async Task Handle_StalePriorAcceptance_AppendsNewRow()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, "2026-03-09", TermsAcceptanceContext.Registration));

        await CreateSut().Handle(new RecordTermsAcceptanceCommand(userId), CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<TermsAcceptance>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

Create `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/GetTermsConsentStatusQueryHandlerTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application;

[Trait("Category", TestCategories.Unit)]
public sealed class GetTermsConsentStatusQueryHandlerTests
{
    private readonly Mock<ITermsAcceptanceRepository> _repo = new();

    private GetTermsConsentStatusQueryHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task Handle_NoAcceptance_NeedsReAcceptanceTrue()
    {
        _repo.Setup(r => r.GetLatestByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TermsAcceptance?)null);

        var result = await CreateSut().Handle(new GetTermsConsentStatusQuery(Guid.NewGuid()), CancellationToken.None);

        result.CurrentVersion.Should().Be(TermsVersion.Current);
        result.AcceptedVersion.Should().BeNull();
        result.AcceptedAt.Should().BeNull();
        result.NeedsReAcceptance.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StaleVersion_NeedsReAcceptanceTrue()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, "2026-03-09", TermsAcceptanceContext.Registration));

        var result = await CreateSut().Handle(new GetTermsConsentStatusQuery(userId), CancellationToken.None);

        result.AcceptedVersion.Should().Be("2026-03-09");
        result.NeedsReAcceptance.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CurrentVersion_NeedsReAcceptanceFalse()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetLatestByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TermsAcceptance.Create(userId, TermsVersion.Current, TermsAcceptanceContext.ReConsent));

        var result = await CreateSut().Handle(new GetTermsConsentStatusQuery(userId), CancellationToken.None);

        result.AcceptedVersion.Should().Be(TermsVersion.Current);
        result.NeedsReAcceptance.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: FAIL — DTO/command/query/handlers do not exist.

- [ ] **Step 3: Create the DTO**

Create `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/TermsConsentStatusDto.cs`:

```csharp
namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Read model describing a user's ToS acceptance status (#2954 F1).
/// NeedsReAcceptance is computed but intentionally NOT enforced by any gate in this scope.
/// </summary>
public sealed record TermsConsentStatusDto(
    string CurrentVersion,
    string? AcceptedVersion,
    DateTime? AcceptedAt,
    bool NeedsReAcceptance);
```

- [ ] **Step 4: Create the command + handler**

Create `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/TermsAcceptance/RecordTermsAcceptanceCommand.cs`:

```csharp
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

#pragma warning disable MA0048 // File name must match type name - Command + Handler in one file
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Records that a user accepted the current ToS version (#2954 F1). Used by the
/// /users/me/terms/accept endpoint (Context = ReConsent). Idempotent: no new row
/// when the user's latest accepted version already equals TermsVersion.Current.
/// </summary>
internal record RecordTermsAcceptanceCommand(
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<TermsConsentStatusDto>;

internal sealed class RecordTermsAcceptanceCommandHandler
    : ICommandHandler<RecordTermsAcceptanceCommand, TermsConsentStatusDto>
{
    private readonly ITermsAcceptanceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordTermsAcceptanceCommandHandler(ITermsAcceptanceRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TermsConsentStatusDto> Handle(RecordTermsAcceptanceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var latest = await _repository.GetLatestByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (latest is null || !string.Equals(latest.TermsVersion, TermsVersion.Current, StringComparison.Ordinal))
        {
            var acceptance = TermsAcceptance.Create(
                command.UserId,
                TermsVersion.Current,
                TermsAcceptanceContext.ReConsent,
                command.IpAddress,
                command.UserAgent);

            await _repository.AddAsync(acceptance, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new TermsConsentStatusDto(TermsVersion.Current, TermsVersion.Current, acceptance.AcceptedAt, NeedsReAcceptance: false);
        }

        return new TermsConsentStatusDto(TermsVersion.Current, latest.TermsVersion, latest.AcceptedAt, NeedsReAcceptance: false);
    }
}
```

- [ ] **Step 5: Create the query + handler**

Create `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/TermsAcceptance/GetTermsConsentStatusQuery.cs`:

```csharp
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Query + Handler in one file
namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>Returns the ToS acceptance status for a user (#2954 F1).</summary>
internal record GetTermsConsentStatusQuery(Guid UserId) : IQuery<TermsConsentStatusDto>;

internal sealed class GetTermsConsentStatusQueryHandler
    : IQueryHandler<GetTermsConsentStatusQuery, TermsConsentStatusDto>
{
    private readonly ITermsAcceptanceRepository _repository;

    public GetTermsConsentStatusQueryHandler(ITermsAcceptanceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TermsConsentStatusDto> Handle(GetTermsConsentStatusQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var latest = await _repository.GetLatestByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);
        var acceptedVersion = latest?.TermsVersion;
        var needsReAcceptance = !string.Equals(acceptedVersion, TermsVersion.Current, StringComparison.Ordinal);

        return new TermsConsentStatusDto(TermsVersion.Current, acceptedVersion, latest?.AcceptedAt, needsReAcceptance);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RecordTermsAcceptanceCommandHandlerTests|FullyQualifiedName~GetTermsConsentStatusQueryHandlerTests"`
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application
git commit -m "feat(auth): record/status CQRS for ToS acceptance (#2954 F1)"
```

---

## Task 4: Register pipeline — flag, enforcement, recording

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommand.cs:11-24`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler.cs`
- Modify: `apps/api/src/Api/Models/AuthContracts.cs:10-35`
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs:95-119`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Endpoints/RegisterRaceConditionEndpointTests.cs`
- Test (extend): `apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs`

**Interfaces:**
- Consumes: `RegisterCommand`, `RegisterPayload`, `ITermsAcceptanceRepository`, `TermsAcceptance`, `TermsVersion.Current`.
- Produces: register records a `Registration`-context acceptance; `/auth/register` returns 400 when `termsAccepted` is missing/false.

- [ ] **Step 1: Add `TermsAccepted` to the command**

Modify `RegisterCommand.cs`: add `bool TermsAccepted = false` as the LAST positional parameter (after `BootstrapToken`):

```csharp
internal record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string? Role = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? BootstrapToken = null,
    // #2954 F1: user's ToS acceptance from the register form. When true, the
    // handler records an append-only TermsAcceptance (Context=Registration).
    bool TermsAccepted = false
) : ICommand<RegisterResponse>;
```

- [ ] **Step 2: Add `TermsAccepted` to the payload**

Modify `AuthContracts.cs` `RegisterPayload` — add after `BootstrapToken` (line 34):

```csharp
    /// <summary>
    /// #2954 F1: whether the user checked the required "I accept the Terms of Service"
    /// box. Enforced server-side at the endpoint (400 when false/absent). Accepts both
    /// "termsAccepted" (camelCase) and "TermsAccepted" (PascalCase).
    /// </summary>
    public bool TermsAccepted { get; set; }
```

- [ ] **Step 3: Enforce + forward at the endpoint**

Modify `AuthenticationEndpoints.cs` `MapRegisterEndpoint`. After the email/password check block (currently ending line 98), add:

```csharp
            // #2954 F1: server-side enforcement of ToS acceptance. The register form's
            // checkbox is client-cosmetic; reject a direct API call that omits acceptance.
            if (!payload.TermsAccepted)
            {
                return Results.BadRequest(new { error = "You must accept the Terms of Service to register" });
            }
```

Then extend the `new DddRegisterCommand(...)` construction (currently ends `BootstrapToken: payload.BootstrapToken);` at line 119) to pass the flag:

```csharp
            var command = new DddRegisterCommand(
                Email: payload.Email,
                Password: payload.Password,
                DisplayName: displayName,
                Role: null,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString(),
                BootstrapToken: payload.BootstrapToken,
                TermsAccepted: payload.TermsAccepted);
```

- [ ] **Step 4: Record acceptance in the handler**

Modify `RegisterCommandHandler.cs`:

(a) Add usings at the top:
```csharp
using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
```

(b) Add a field + constructor parameter. Add field near the other `private readonly` fields:
```csharp
    private readonly ITermsAcceptanceRepository _termsAcceptanceRepository;
```
Add the parameter to the constructor signature (append after `auditLogger`) and assign it:
```csharp
        Api.BoundedContexts.SecurityAudit.Application.Services.IAuditLogger auditLogger,
        ITermsAcceptanceRepository termsAcceptanceRepository)
    {
        ...
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _termsAcceptanceRepository = termsAcceptanceRepository ?? throw new ArgumentNullException(nameof(termsAcceptanceRepository));
    }
```

(c) In `Handle`, immediately AFTER `await _sessionRepository.AddAsync(session, cancellationToken)...` (currently line 135) and BEFORE the `try { await _unitOfWork.SaveChangesAsync ... }` block, add:

```csharp
        // #2954 F1: record ToS acceptance in the SAME transaction as the new user.
        // The endpoint already rejects registration without acceptance; the guard here
        // keeps direct-command callers (tests) that omit the flag from writing a row.
        if (command.TermsAccepted)
        {
            var termsAcceptance = TermsAcceptance.Create(
                userId,
                TermsVersion.Current,
                TermsAcceptanceContext.Registration,
                command.IpAddress,
                command.UserAgent);
            await _termsAcceptanceRepository.AddAsync(termsAcceptance, cancellationToken).ConfigureAwait(false);
        }
```

- [ ] **Step 5: Update existing register endpoint tests to send acceptance**

In `RegisterRaceConditionEndpointTests.cs`, every register request payload must include `termsAccepted: true`. Find each JSON body posted to `/auth/register` (anonymous objects or serialized payloads) and add `termsAccepted = true`. Run the file's tests after editing:

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RegisterRaceConditionEndpointTests"`
Expected: PASS. If any test now returns 400, its payload is still missing `termsAccepted`.

> Also grep the whole test project for other `"/auth/register"` posters and add `termsAccepted = true` to each: `grep -rl '"/auth/register"' apps/api/tests`.

- [ ] **Step 6: Extend the integration test — registration records a row**

Append to `apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs`:

```csharp
[Fact]
public async Task Register_WithTermsAccepted_WritesExactlyOneRegistrationRow()
{
    // Arrange: build the HTTP client from the integration fixture (mirror a sibling
    // endpoint test's WebApplicationFactory/client setup).
    var body = new
    {
        email = $"terms-{Guid.NewGuid():N}@example.com",
        password = "ValidUnusualPwd123!",
        displayName = "Terms User",
        termsAccepted = true
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/v1/auth/register", body);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // Resolve the created user's id (from the response JSON `user.id`) and assert one row:
    var acceptances = await dbContext.Set<TermsAcceptance>()
        .Where(t => t.UserId == createdUserId).ToListAsync();
    acceptances.Should().HaveCount(1);
    acceptances[0].TermsVersion.Should().Be(TermsVersion.Current);
    acceptances[0].Context.Should().Be(TermsAcceptanceContext.Registration);
}

[Fact]
public async Task Register_WithoutTermsAccepted_Returns400()
{
    var body = new
    {
        email = $"noterms-{Guid.NewGuid():N}@example.com",
        password = "ValidUnusualPwd123!",
        displayName = "No Terms"
        // termsAccepted omitted → false
    };

    var response = await client.PostAsJsonAsync("/api/v1/auth/register", body);

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

> Fill `client`/`dbContext`/`createdUserId` from the sibling endpoint-test fixture pattern.

- [ ] **Step 7: Run the integration + build**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TermsAcceptanceIntegrationTests"`
Expected: PASS (repo round-trip + register-records-row + register-400).

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration apps/api/src/Api/Models/AuthContracts.cs apps/api/src/Api/Routing/AuthenticationEndpoints.cs apps/api/tests/Api.Tests
git commit -m "feat(auth): record ToS acceptance at registration + enforce checkbox (#2954 F1)"
```

---

## Task 5: Endpoints — accept + status, wired into routing

**Files:**
- Create: `apps/api/src/Api/Routing/TermsConsentEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs:852` (after `MapUserAiConsentEndpoints`)
- Test (extend): `apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs`

**Interfaces:**
- Consumes: `GetTermsConsentStatusQuery`, `RecordTermsAcceptanceCommand`, `SessionStatusDto`.
- Produces: `GET /api/v1/users/me/terms/status`, `POST /api/v1/users/me/terms/accept` (both session-authed, me-scoped).

- [ ] **Step 1: Create the endpoints**

Create `apps/api/src/Api/Routing/TermsConsentEndpoints.cs`:

```csharp
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Terms-of-Service acceptance endpoints (#2954 F1). Foundation only: records/reads
/// acceptance; no blocking gate is wired to needsReAcceptance in this scope.
/// </summary>
internal static class TermsConsentEndpoints
{
    public static RouteGroupBuilder MapTermsConsentEndpoints(this RouteGroupBuilder group)
    {
        MapGetTermsStatus(group);
        MapAcceptTerms(group);
        return group;
    }

    private static void MapGetTermsStatus(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/terms/status", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var status = await mediator.Send(
                new GetTermsConsentStatusQuery(session.Principal!.Subject.Id), ct).ConfigureAwait(false);
            return Results.Json(status);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetTermsConsentStatus")
        .WithTags("User Profile", "Terms")
        .WithSummary("Get current user's ToS acceptance status")
        .Produces(200)
        .Produces(401);
    }

    private static void MapAcceptTerms(RouteGroupBuilder group)
    {
        group.MapPost("/users/me/terms/accept", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var status = await mediator.Send(new RecordTermsAcceptanceCommand(
                UserId: session.Principal!.Subject.Id,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString()), ct).ConfigureAwait(false);
            return Results.Json(status);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("AcceptTerms")
        .WithTags("User Profile", "Terms")
        .WithSummary("Record acceptance of the current ToS version")
        .Produces(200)
        .Produces(401);
    }
}
```

- [ ] **Step 2: Wire into routing**

Modify `apps/api/src/Api/Program.cs`. After line 852 (`v1Api.MapUserAiConsentEndpoints();`), add:

```csharp
v1Api.MapTermsConsentEndpoints(); // #2954 F1: ToS acceptance foundation
```

- [ ] **Step 3: Build**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: PASS.

- [ ] **Step 4: Extend the integration test — accept + status + append-only + me-scoped**

Append to `TermsAcceptanceIntegrationTests.cs` (reuse the authenticated-client helper of the fixture; the user must have a session):

```csharp
[Fact]
public async Task StatusThenAccept_TransitionsNeedsReAcceptance()
{
    // Arrange: authenticated client for a freshly-registered user who accepted at register.
    // (After registration with termsAccepted:true, status should already be up to date.)

    // A user with NO acceptance (or a stale one) → needsReAcceptance true.
    // Seed a stale acceptance row for the user, then:
    var before = await authedClient.GetFromJsonAsync<TermsConsentStatusDto>("/api/v1/users/me/terms/status");
    before!.NeedsReAcceptance.Should().BeTrue();

    // Act: accept.
    var accept = await authedClient.PostAsync("/api/v1/users/me/terms/accept", content: null);
    accept.StatusCode.Should().Be(HttpStatusCode.OK);

    // Assert: status now current.
    var after = await authedClient.GetFromJsonAsync<TermsConsentStatusDto>("/api/v1/users/me/terms/status");
    after!.NeedsReAcceptance.Should().BeFalse();
    after.AcceptedVersion.Should().Be(TermsVersion.Current);

    // Idempotent: second accept adds no row.
    await authedClient.PostAsync("/api/v1/users/me/terms/accept", content: null);
    var rows = await dbContext.Set<TermsAcceptance>()
        .Where(t => t.UserId == userId && t.TermsVersion == TermsVersion.Current).CountAsync();
    rows.Should().Be(1);
}

[Fact]
public async Task Status_Unauthenticated_Returns401()
{
    var response = await anonymousClient.GetAsync("/api/v1/users/me/terms/status");
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

> Fill `authedClient`/`anonymousClient`/`dbContext`/`userId` from the sibling fixture. The me-scoping is inherent: userId comes from the session, so there is no cross-tenant param to attack — the 401 test confirms the auth gate.

- [ ] **Step 5: Run the full integration file**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~TermsAcceptanceIntegrationTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/TermsConsentEndpoints.cs apps/api/src/Api/Program.cs apps/api/tests/Api.Tests/Integration/Authentication/TermsAcceptanceIntegrationTests.cs
git commit -m "feat(auth): me-scoped ToS accept/status endpoints (#2954 F1)"
```

---

## Task 6: Frontend — forward acceptance, cross-ref comment

**Files:**
- Modify: `apps/web/src/lib/api/clients/authClient.ts:74-79`
- Modify: `apps/web/src/app/(auth)/register/_content.tsx:87-90`
- Modify: `apps/web/src/app/(public)/terms/page.tsx:52`
- Test: the client + register-content consumer tests.

**Interfaces:**
- Consumes: BE `/auth/register` now requires `termsAccepted:true`.
- Produces: `RegisterRequest.termsAccepted: boolean`; register call sends `termsAccepted: true`.

- [ ] **Step 1: Write/adjust the failing FE test**

Locate the authClient test (e.g. `apps/web/src/lib/api/clients/__tests__/authClient.test.ts` — confirm the path). Add a test asserting the register POST body includes `termsAccepted`:

```ts
it('register posts termsAccepted', async () => {
  const httpClient = { post: vi.fn().mockResolvedValue({ user: { id: '1' } }) };
  const client = createAuthClient({ httpClient: httpClient as never });
  await client.register({ email: 'a@b.com', password: 'ValidUnusualPwd123!', termsAccepted: true });
  expect(httpClient.post).toHaveBeenCalledWith(
    '/api/v1/auth/register',
    expect.objectContaining({ termsAccepted: true }),
    expect.anything()
  );
});
```

- [ ] **Step 2: Run it to verify it fails**

Run: `cd apps/web && pnpm test -- authClient`
Expected: FAIL — `termsAccepted` not on `RegisterRequest` (type error) / not sent.

- [ ] **Step 3: Add the field to the client**

Modify `authClient.ts` `RegisterRequest`:

```ts
export interface RegisterRequest {
  email: string;
  password: string;
  displayName?: string;
  role?: string;
  // #2954 F1: user's ToS acceptance; the backend rejects registration without it.
  termsAccepted: boolean;
}
```

(`register()` already posts `request` as-is, so no change to the method body.)

- [ ] **Step 4: Forward from the register page**

Modify `apps/web/src/app/(auth)/register/_content.tsx` — the `register({ email, password })` call (lines 87-90). The form only submits when the required terms checkbox is checked, so acceptance is always true here:

```tsx
        await register({
          email: data.email,
          password: data.password,
          termsAccepted: true,
        });
```

- [ ] **Step 5: Cross-ref comment on the ToS version literal**

Modify `apps/web/src/app/(public)/terms/page.tsx` line 52:

```tsx
      // #2954 F1: keep this date in sync with the backend TermsVersion.Current
      // constant (apps/api/src/Api/BoundedContexts/Authentication/Domain/Constants/TermsVersion.cs).
      lastUpdated={new Date('2026-07-15')}
```

- [ ] **Step 6: Run FE unit tests (client + register consumers)**

Run: `cd apps/web && pnpm test -- authClient register`
Expected: PASS. Fix any register consumer test that mocks `register` and asserts call args (add `termsAccepted: true` to expectations).

- [ ] **Step 7: Typecheck + lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/lib/api/clients/authClient.ts apps/web/src/app/(auth)/register/_content.tsx apps/web/src/app/(public)/terms/page.tsx apps/web/src/lib/api/clients/__tests__
git commit -m "feat(web): forward ToS acceptance on register (#2954 F1)"
```

---

## Final Verification

- [ ] **Backend unit + integration:** `cd apps/api && dotnet test --filter "FullyQualifiedName~TermsAcceptance|FullyQualifiedName~TermsConsent"` → all green.
- [ ] **Backend full build:** `dotnet build apps/api/src/Api/Api.csproj` → 0 errors.
- [ ] **No net-new baseline failures:** run the Authentication BC tests (`dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=Authentication"` if the trait exists, else the Authentication namespace filter) → no regressions vs. `main-dev` baseline.
- [ ] **Frontend:** `cd apps/web && pnpm test && pnpm typecheck && pnpm lint` → green.
- [ ] **Migration applies:** `cd apps/api/src/Api && dotnet ef database update` against a scratch DB → `user_terms_acceptances` created.
- [ ] **Scope guard:** confirm no blocking gate / middleware / FE modal was added — `needsReAcceptance` is exposed but unconsumed.

## Out of scope (do NOT implement)
- Blocking login gate / re-accept middleware / FE modal.
- Backfill or notification for existing users.
- OAuth / admin-created / seeded users recording acceptance (they legitimately show `needsReAcceptance=true`).
- The final legal determination (reserved to the professional reviewer; see spec §12).

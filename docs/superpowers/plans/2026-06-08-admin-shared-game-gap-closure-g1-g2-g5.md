# Admin Shared Game — Gap Closure G1/G2/G5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chiudere 3 gap reali sul workflow admin shared-game import esistente: (G1) edit manuale di tag fields + BggId su `UpdateSharedGameCommand`, (G2) re-upload BGG cover image sul nostro storage durante create-from-PDF, (G5) Idempotency-Key support su `POST /admin/shared-games/wizard/create`.

**Architecture:**
- G1 estende command esistente seguendo pattern già accettato in `UpdateSharedGameFromBggCommandHandler` (DB context + Clear/AddRange su collections); aggiunge `BggId` come scalar update.
- G2 introduce `BggCoverDownloader` service che fetcha BGG image URL → uploada a `IBlobStorageService` con resource key `bgg-cover-{bggId}` → salva `BggCoverR2Key` su `SharedGameEntity`; estende `CoverUrlResolver` con layer L2.5 BGG (priority L3 user → L4 PDF → L2.5 BGG → L2 Wikidata).
- G5 usa Redis cache (`IAiResponseCacheService` già DI-registered) per persist `Idempotency-Key → gameId` con TTL 5 min; check pre-Send nel wizard endpoint.

**Tech Stack:** .NET 9 + EF Core (Postgres + Testcontainers per integration) + xUnit + Moq + FluentValidation + MediatR + Redis cache wrapper esistente.

**Scope explicit out:**
- G3 (UI tab labels) — cosmetic, defer
- G4 (single-page vs 3-step wizard UX) — design choice esistente, defer
- G6 (admin quota bypass) — falso positivo, `UploadPdfForGameExtractionCommandHandler:13` già senza quota check
- Frontend changes — questo plan è backend-only (no UI changes). Eventuale FE estensione del form `/admin/shared-games/[id]` per esporre tag fields nell'edit è follow-up separato.

---

## File Structure

### Files to CREATE

| Path | Responsibility |
|------|----------------|
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverDownloader.cs` | Interface for downloader service |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs` | Implementation: fetch BGG image + upload to blob storage |
| `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddBggCoverR2KeyToSharedGames.cs` | EF migration adding column |
| `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/UpdateSharedGameCommandHandlerTests.cs` | New unit test class (currently missing) |
| `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs` | New unit test class for downloader |
| `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Integration/WizardCreateIdempotencyTests.cs` | New integration test (Testcontainers) for double-submit |

### Files to MODIFY

| Path | What changes |
|------|--------------|
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommand.cs` | Add `List<string>? Categories, Mechanics, Designers, Publishers; int? BggId` fields |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandValidator.cs` | Validate new fields (length, count, BggId positive) |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandHandler.cs` | Use `MeepleAiDbContext` DI to update collections + BggId via Clear/Add pattern |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs` | Register `IBggCoverDownloader` + dbContext into UpdateSharedGameCommandHandler if needed |
| `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/SharedGameEntity.cs` | Add `string? BggCoverR2Key` property |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs` | Configure `BggCoverR2Key` column (nullable, varchar) |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs` | Add L2.5 layer (BggCoverR2Key) between L4 (Pdf) and L2 (Wikidata) |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameFromPdfCommandHandler.cs` | Step 5: invoke `IBggCoverDownloader` if `SelectedBggId` present; fallback to direct URL on failure |
| `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogWizardEndpoints.cs` | `HandleWizardCreateGame`: read `Idempotency-Key` header, check Redis cache, return existing gameId on hit |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs` | Add `SetBggCoverR2Key(string?)` domain method (no event, infrastructure concern) |
| `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameFromPdfCommandHandlerTests.cs` | Add 2 new tests for cover re-upload (success + fallback) |

---

## Phase 1 — G1: UpdateSharedGameCommand extension

**Goal:** Permettere all'edit form di `/admin/shared-games/[id]` di aggiornare manualmente `Categories`, `Mechanics`, `Designers`, `Publishers`, `BggId` oltre ai core fields già supportati.

**Pattern reference:** `UpdateSharedGameFromBggCommandHandler.cs:46-118` — usa `MeepleAiDbContext.Set<SharedGameEntity>().Include(...)` + Clear/foreach Add pattern per collections.

### Task 1.1: Extend `UpdateSharedGameCommand` record

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommand.cs`

- [ ] **Step 1.1.1: Update command record signature**

Replace the entire content of `UpdateSharedGameCommand.cs` with:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to update an existing shared game in the catalog.
/// Supports core fields, taxonomy collections (categories, mechanics, designers, publishers),
/// and BggId. Null collections mean "do not change"; empty list means "clear".
/// </summary>
internal record UpdateSharedGameCommand(
    Guid GameId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto? Rules,
    Guid ModifiedBy,
    int? BggId = null,
    List<string>? Categories = null,
    List<string>? Mechanics = null,
    List<string>? Designers = null,
    List<string>? Publishers = null
) : ICommand<Unit>;
```

- [ ] **Step 1.1.2: Verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo`
Expected: build succeeds with 0 errors. Pre-existing warnings are acceptable.

- [ ] **Step 1.1.3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommand.cs
git commit -m "feat(shared-games): extend UpdateSharedGameCommand with taxonomy fields and BggId

Gap G1 from spec docs/superpowers/specs/2026-06-08-admin-shared-game-import-spec-panel-review.md.
Null collections = no change semantics; empty = clear. BggId optional scalar.
Handler/validator changes in subsequent tasks."
```

### Task 1.2: Update validator

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandValidator.cs`

- [ ] **Step 1.2.1: Read current validator to preserve existing rules**

Run: `Read apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandValidator.cs`

You will use the existing rule structure as a template. Look at how core field rules are written, then append the new rules.

- [ ] **Step 1.2.2: Append validation rules for new fields**

Inside the validator's constructor, after the existing rules, add:

```csharp
RuleFor(x => x.BggId)
    .GreaterThan(0)
    .When(x => x.BggId.HasValue)
    .WithMessage("BggId must be a positive integer when provided");

RuleFor(x => x.Categories)
    .Must(c => c == null || c.Count <= 20)
    .WithMessage("Categories cannot exceed 20 items");

RuleForEach(x => x.Categories)
    .NotEmpty()
    .MaximumLength(100)
    .When(x => x.Categories != null);

RuleFor(x => x.Mechanics)
    .Must(m => m == null || m.Count <= 30)
    .WithMessage("Mechanics cannot exceed 30 items");

RuleForEach(x => x.Mechanics)
    .NotEmpty()
    .MaximumLength(100)
    .When(x => x.Mechanics != null);

RuleFor(x => x.Designers)
    .Must(d => d == null || d.Count <= 20)
    .WithMessage("Designers cannot exceed 20 items");

RuleForEach(x => x.Designers)
    .NotEmpty()
    .MaximumLength(200)
    .When(x => x.Designers != null);

RuleFor(x => x.Publishers)
    .Must(p => p == null || p.Count <= 20)
    .WithMessage("Publishers cannot exceed 20 items");

RuleForEach(x => x.Publishers)
    .NotEmpty()
    .MaximumLength(200)
    .When(x => x.Publishers != null);
```

- [ ] **Step 1.2.3: Verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo`
Expected: build succeeds with 0 errors.

- [ ] **Step 1.2.4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandValidator.cs
git commit -m "feat(shared-games): add validation rules for taxonomy and BggId in UpdateSharedGameCommand

Limits: 20 categories/designers/publishers, 30 mechanics. Per-item length caps."
```

### Task 1.3: Create unit test class for UpdateSharedGameCommandHandler

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/UpdateSharedGameCommandHandlerTests.cs`

- [ ] **Step 1.3.1: Inspect existing handler test pattern for reference**

Run: `Read apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameFromPdfCommandHandlerTests.cs` (first ~60 lines)

Note the fixture setup (Moq, InMemory DbContext or Testcontainers, repository mocks).

- [ ] **Step 1.3.2: Write the failing test file (3 tests)**

Create `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/UpdateSharedGameCommandHandlerTests.cs`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

public sealed class UpdateSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<UpdateSharedGameCommandHandler>> _loggerMock = new();
    private readonly MeepleAiDbContext _dbContext;

    public UpdateSharedGameCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"update-shared-game-{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(options);
    }

    [Fact]
    public async Task Handle_WithCoreFieldsOnly_UpdatesGameSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingEntity = SeedGameEntity(gameId);

        var domainAggregate = BuildAggregateFromEntity(existingEntity);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: "Wingspan ITA",
            YearPublished: 2019,
            Description: "Updated description",
            MinPlayers: 1,
            MaxPlayers: 5,
            PlayingTimeMinutes: 70,
            MinAge: 10,
            ComplexityRating: 2.4m,
            AverageRating: 8.1m,
            ImageUrl: "https://cdn/img.webp",
            ThumbnailUrl: "https://cdn/thumb.webp",
            Rules: null,
            ModifiedBy: userId);

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCategoriesAndMechanics_ReplacesCollections()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entity = SeedGameEntity(gameId);
        entity.Categories.Add(new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Old", Slug = "old", CreatedAt = DateTime.UtcNow });
        _dbContext.Set<SharedGameEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();

        var domainAggregate = BuildAggregateFromEntity(entity);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: entity.Title,
            YearPublished: entity.YearPublished,
            Description: entity.Description,
            MinPlayers: entity.MinPlayers,
            MaxPlayers: entity.MaxPlayers,
            PlayingTimeMinutes: entity.PlayingTimeMinutes,
            MinAge: entity.MinAge,
            ComplexityRating: entity.ComplexityRating,
            AverageRating: entity.AverageRating,
            ImageUrl: entity.ImageUrl,
            ThumbnailUrl: entity.ThumbnailUrl,
            Rules: null,
            ModifiedBy: userId,
            Categories: new List<string> { "Strategy", "Negotiation" },
            Mechanics: new List<string> { "Dice Rolling" });

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var reloaded = await _dbContext.Set<SharedGameEntity>()
            .Include(g => g.Categories)
            .Include(g => g.Mechanics)
            .FirstAsync(g => g.Id == gameId);

        reloaded.Categories.Select(c => c.Name).Should().BeEquivalentTo(new[] { "Strategy", "Negotiation" });
        reloaded.Mechanics.Select(m => m.Name).Should().BeEquivalentTo(new[] { "Dice Rolling" });
    }

    [Fact]
    public async Task Handle_WithBggIdProvided_UpdatesBggIdScalar()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entity = SeedGameEntity(gameId);
        entity.BggId = null;
        _dbContext.Set<SharedGameEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();

        var domainAggregate = BuildAggregateFromEntity(entity);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: entity.Title,
            YearPublished: entity.YearPublished,
            Description: entity.Description,
            MinPlayers: entity.MinPlayers,
            MaxPlayers: entity.MaxPlayers,
            PlayingTimeMinutes: entity.PlayingTimeMinutes,
            MinAge: entity.MinAge,
            ComplexityRating: entity.ComplexityRating,
            AverageRating: entity.AverageRating,
            ImageUrl: entity.ImageUrl,
            ThumbnailUrl: entity.ThumbnailUrl,
            Rules: null,
            ModifiedBy: userId,
            BggId: 13);

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var reloaded = await _dbContext.Set<SharedGameEntity>().FirstAsync(g => g.Id == gameId);
        reloaded.BggId.Should().Be(13);
    }

    private static SharedGameEntity SeedGameEntity(Guid gameId)
    {
        return new SharedGameEntity
        {
            Id = gameId,
            Title = "Wingspan",
            Description = "Bird-themed engine builder",
            YearPublished = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://cdn/old.webp",
            ThumbnailUrl = "https://cdn/old-thumb.webp",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        };
    }

    private static SharedGame BuildAggregateFromEntity(SharedGameEntity entity)
    {
        return SharedGame.Create(
            title: entity.Title,
            yearPublished: entity.YearPublished,
            description: entity.Description,
            minPlayers: entity.MinPlayers,
            maxPlayers: entity.MaxPlayers,
            playingTimeMinutes: entity.PlayingTimeMinutes,
            minAge: entity.MinAge,
            complexityRating: entity.ComplexityRating,
            averageRating: entity.AverageRating,
            imageUrl: entity.ImageUrl,
            thumbnailUrl: entity.ThumbnailUrl,
            rules: null,
            createdBy: entity.CreatedBy,
            bggId: entity.BggId);
    }
}
```

- [ ] **Step 1.3.3: Run tests — expect FAIL (compilation error: handler ctor signature changed)**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateSharedGameCommandHandlerTests" --nologo`
Expected: FAIL — handler constructor doesn't yet accept `MeepleAiDbContext`. This is the failing test signal.

If aggregates `SharedGame.Create` signature has drifted from this plan's example, adapt the `BuildAggregateFromEntity` helper to current production signature (read `SharedGame.cs:347` for the `Create` method to align).

- [ ] **Step 1.3.4: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/UpdateSharedGameCommandHandlerTests.cs
git commit -m "test(shared-games): add UpdateSharedGameCommandHandler tests (failing, handler ctor pending)"
```

### Task 1.4: Refactor handler to support new fields

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandHandler.cs`

- [ ] **Step 1.4.1: Replace handler implementation**

Replace the entire content of `UpdateSharedGameCommandHandler.cs` with:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing shared game.
/// Supports core fields (via domain UpdateInfo) and optional manual updates of
/// BggId + taxonomy collections (categories, mechanics, designers, publishers).
/// </summary>
/// <remarks>
/// Uses MeepleAiDbContext directly for relationship management — same pattern as
/// UpdateSharedGameFromBggCommandHandler. The repository abstraction does not
/// support tracked Include for collection-replace semantics.
/// </remarks>
internal sealed class UpdateSharedGameCommandHandler : ICommandHandler<UpdateSharedGameCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateSharedGameCommandHandler> _logger;

    public UpdateSharedGameCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext dbContext,
        ILogger<UpdateSharedGameCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UpdateSharedGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating shared game: {GameId}, ModifiedBy: {UserId}",
            command.GameId, command.ModifiedBy);

        // ─── 1. Update aggregate core fields via domain method ─────────────
        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new InvalidOperationException($"Shared game {command.GameId} not found");
        }

        GameRules? rules = null;
        if (command.Rules is not null)
        {
            rules = GameRules.Create(command.Rules.Content, command.Rules.Language);
        }

        game.UpdateInfo(
            command.Title, command.YearPublished, command.Description,
            command.MinPlayers, command.MaxPlayers, command.PlayingTimeMinutes,
            command.MinAge, command.ComplexityRating, command.AverageRating,
            command.ImageUrl, command.ThumbnailUrl, rules, command.ModifiedBy);

        _repository.Update(game);

        // ─── 2. Update entity-level fields (BggId + collections) ────────────
        // Only fetched if any non-null new field is present; null = no change.
        var needsEntityUpdate = command.BggId.HasValue
            || command.Categories is not null
            || command.Mechanics is not null
            || command.Designers is not null
            || command.Publishers is not null;

        if (needsEntityUpdate)
        {
            var entity = await _dbContext.Set<SharedGameEntity>()
                .Include(e => e.Categories)
                .Include(e => e.Mechanics)
                .Include(e => e.Designers)
                .Include(e => e.Publishers)
                .FirstOrDefaultAsync(e => e.Id == command.GameId, cancellationToken)
                .ConfigureAwait(false);

            if (entity is null)
            {
                throw new InvalidOperationException($"SharedGame entity {command.GameId} not found in DbContext");
            }

            if (command.BggId.HasValue)
            {
                entity.BggId = command.BggId.Value;
            }

            if (command.Categories is not null)
            {
                await ReplaceCategoriesAsync(entity, command.Categories, cancellationToken).ConfigureAwait(false);
            }

            if (command.Mechanics is not null)
            {
                await ReplaceMechanicsAsync(entity, command.Mechanics, cancellationToken).ConfigureAwait(false);
            }

            if (command.Designers is not null)
            {
                await ReplaceDesignersAsync(entity, command.Designers, cancellationToken).ConfigureAwait(false);
            }

            if (command.Publishers is not null)
            {
                await ReplacePublishersAsync(entity, command.Publishers, cancellationToken).ConfigureAwait(false);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game updated successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }

    private async Task ReplaceCategoriesAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Categories.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var category = await _dbContext.GameCategories.FirstOrDefaultAsync(c => c.Name == name, ct).ConfigureAwait(false);
            if (category is null)
            {
                category = new GameCategoryEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = name.ToLowerInvariant().Replace(" ", "-"),
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameCategories.AddAsync(category, ct).ConfigureAwait(false);
            }
            entity.Categories.Add(category);
        }
    }

    private async Task ReplaceMechanicsAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Mechanics.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var mechanic = await _dbContext.GameMechanics.FirstOrDefaultAsync(m => m.Name == name, ct).ConfigureAwait(false);
            if (mechanic is null)
            {
                mechanic = new GameMechanicEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = name.ToLowerInvariant().Replace(" ", "-"),
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameMechanics.AddAsync(mechanic, ct).ConfigureAwait(false);
            }
            entity.Mechanics.Add(mechanic);
        }
    }

    private async Task ReplaceDesignersAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Designers.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var designer = await _dbContext.GameDesigners.FirstOrDefaultAsync(d => d.Name == name, ct).ConfigureAwait(false);
            if (designer is null)
            {
                designer = new GameDesignerEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GameDesigners.AddAsync(designer, ct).ConfigureAwait(false);
            }
            entity.Designers.Add(designer);
        }
    }

    private async Task ReplacePublishersAsync(SharedGameEntity entity, List<string> names, CancellationToken ct)
    {
        entity.Publishers.Clear();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
        {
            var publisher = await _dbContext.GamePublishers.FirstOrDefaultAsync(p => p.Name == name, ct).ConfigureAwait(false);
            if (publisher is null)
            {
                publisher = new GamePublisherEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.GamePublishers.AddAsync(publisher, ct).ConfigureAwait(false);
            }
            entity.Publishers.Add(publisher);
        }
    }
}
```

- [ ] **Step 1.4.2: Run tests — expect PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateSharedGameCommandHandlerTests" --nologo`
Expected: 3 tests pass.

If a test fails because production signature of `SharedGame.Create` or `SharedGame.UpdateInfo` differs from the assumptions, **read the current production source** and adapt the test, not the production code. Production drift wins.

- [ ] **Step 1.4.3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/UpdateSharedGameCommandHandler.cs
git commit -m "feat(shared-games): handler supports manual update of taxonomy + BggId

G1 closure. Pattern mirrors UpdateSharedGameFromBggCommandHandler:
DbContext.Set<SharedGameEntity>.Include(...) + Clear/Add per collection.
Null collection = no change; empty = clear. Tests in same PR."
```

### Task 1.5: Verify no existing callers break

- [ ] **Step 1.5.1: Search for existing UpdateSharedGameCommand instantiations**

Run: `Grep pattern="new UpdateSharedGameCommand\(" path="apps/api/src/Api" output_mode="content"`
Expected: list of call sites (likely few, in routing or handlers).

- [ ] **Step 1.5.2: Verify each call site still compiles**

All new params (BggId, Categories, Mechanics, Designers, Publishers) have defaults `= null`, so existing call sites should compile unchanged. If any caller breaks, restore signature compatibility — do not modify callers in this PR.

Run: `dotnet build apps/api/src/Api/Api.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 1.5.3: Run full SharedGameCatalog test slice**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SharedGameCatalog" --nologo`
Expected: all green. If pre-existing reds appear, document as out-of-scope baseline (do not fix in this PR).

- [ ] **Step 1.5.4: Commit if any minor adjustments**

If adjustments made:
```bash
git add -A
git commit -m "chore(shared-games): align call sites after UpdateSharedGameCommand extension"
```
Otherwise skip.

---

## Phase 2 — G2: BGG cover re-upload on our storage

**Goal:** Quando `CreateSharedGameFromPdfCommandHandler` riceve `SelectedBggId`, scaricare `bggDetails.ImageUrl` da BGG CDN, ri-uploadare via `IBlobStorageService` con resource key `bgg-cover-{bggId}`, e salvare `BggCoverR2Key` in `SharedGameEntity`. Fallback su URL diretto BGG se download/upload fallisce.

### Task 2.1: Add BggCoverR2Key column to SharedGameEntity

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/SharedGameEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs`

- [ ] **Step 2.1.1: Read existing entity to identify insertion point**

Run: `Read apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/SharedGameEntity.cs`

Locate where existing R2Key fields are declared (search for `PdfCoverR2Key` or `WikidataCoverR2Key`).

- [ ] **Step 2.1.2: Add property next to existing R2Key fields**

After `PdfCoverR2Key` property declaration (and `WikidataCoverR2Key` if present), add:

```csharp
/// <summary>
/// Gap G2 (issue: BGG cover re-upload).
/// R2 key for cover image downloaded from BGG and re-uploaded to our storage.
/// Resolved by CoverUrlResolver L2.5 layer (between L4 PDF and L2 Wikidata).
/// Null when no BGG enrichment was applied or download failed.
/// </summary>
public string? BggCoverR2Key { get; set; }
```

- [ ] **Step 2.1.3: Configure EF mapping**

Open `SharedGameEntityConfiguration.cs`. Locate where `PdfCoverR2Key` is configured (typical pattern: `builder.Property(e => e.PdfCoverR2Key).HasMaxLength(...)`).

After that mapping, add:

```csharp
builder.Property(e => e.BggCoverR2Key)
    .HasMaxLength(256)
    .IsRequired(false);
```

If the configuration class does not exist (fluent config inlined in DbContext OnModelCreating), find that OnModelCreating block and add the equivalent there.

- [ ] **Step 2.1.4: Verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 2.1.5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/SharedGameEntity.cs apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs
git commit -m "feat(shared-games): add BggCoverR2Key column to SharedGameEntity

Gap G2. nullable varchar(256). Mapping pending in next migration commit."
```

### Task 2.2: Generate EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddBggCoverR2KeyToSharedGames.cs` (auto-generated)

- [ ] **Step 2.2.1: Generate migration**

Run from `apps/api/src/Api`:
```bash
dotnet ef migrations add AddBggCoverR2KeyToSharedGames --output-dir Infrastructure/Migrations
```
Expected: 3 files created (migration .cs, .Designer.cs, snapshot updated).

- [ ] **Step 2.2.2: Inspect generated migration**

Run: `Read apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddBggCoverR2KeyToSharedGames.cs`

Verify the `Up()` method only adds `BggCoverR2Key` column to `shared_games` (or whatever the actual table name is), nullable. If migration touches other tables, **stop** — entity/config drift exists. Re-run discovery before continuing.

- [ ] **Step 2.2.3: Apply migration to local dev DB**

Run from `apps/api/src/Api`:
```bash
dotnet ef database update
```
Expected: migration applied without errors.

- [ ] **Step 2.2.4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(shared-games): EF migration adds BggCoverR2Key column

Gap G2. Reversible Down() preserves zero-downtime rollback."
```

### Task 2.3: Define `IBggCoverDownloader` interface

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverDownloader.cs`

- [ ] **Step 2.3.1: Create interface file**

Write:

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Downloads a cover image from a remote URL (typically BGG CDN) and re-uploads
/// it to our internal blob storage. Returns the R2 key on success, null on failure
/// (caller falls back to direct remote URL).
/// </summary>
internal interface IBggCoverDownloader
{
    /// <summary>
    /// Downloads the image at <paramref name="remoteImageUrl"/> and stores it in blob
    /// storage with a key derived from <paramref name="bggId"/>.
    /// </summary>
    /// <returns>The R2 key on success; null if download or upload failed (logged).</returns>
    Task<string?> DownloadAndUploadAsync(
        int bggId,
        string remoteImageUrl,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2.3.2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/IBggCoverDownloader.cs
git commit -m "feat(shared-games): IBggCoverDownloader interface (Gap G2 scaffolding)"
```

### Task 2.4: Write failing tests for `BggCoverDownloader`

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs`

- [ ] **Step 2.4.1: Write test file (3 scenarios)**

Create:

```csharp
using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

public sealed class BggCoverDownloaderTests
{
    private readonly Mock<IBlobStorageService> _blobMock = new();
    private readonly Mock<ILogger<BggCoverDownloader>> _loggerMock = new();

    [Fact]
    public async Task DownloadAndUploadAsync_OnSuccess_ReturnsR2Key()
    {
        // Arrange
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x89, 0x50, 0x4E, 0x47 /* fake PNG header */ });
        _blobMock.Setup(b => b.StoreAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                BlobCategory.GameImage,
                "bgg-cover-13",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(Success: true, FilePath: "bgg-cover-13", FileSizeBytes: 4, ErrorMessage: null));

        var sut = new BggCoverDownloader(httpClient, _blobMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.DownloadAndUploadAsync(13, "https://cf.geekdo-images.com/abc.jpg", CancellationToken.None);

        // Assert
        result.Should().Be("bgg-cover-13");
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnHttpError_ReturnsNull()
    {
        // Arrange
        var httpClient = BuildHttpClient(HttpStatusCode.NotFound);
        var sut = new BggCoverDownloader(httpClient, _blobMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.DownloadAndUploadAsync(13, "https://cf.geekdo-images.com/missing.jpg", CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _blobMock.Verify(b => b.StoreAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DownloadAndUploadAsync_OnUploadFailure_ReturnsNull()
    {
        // Arrange
        var httpClient = BuildHttpClient(HttpStatusCode.OK, content: new byte[] { 0x01 });
        _blobMock.Setup(b => b.StoreAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(Success: false, FilePath: null, FileSizeBytes: 0, ErrorMessage: "S3 unavailable"));

        var sut = new BggCoverDownloader(httpClient, _blobMock.Object, _loggerMock.Object);

        // Act
        var result = await sut.DownloadAndUploadAsync(13, "https://cf.geekdo-images.com/abc.jpg", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private static HttpClient BuildHttpClient(HttpStatusCode statusCode, byte[]? content = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = content is null ? null : new ByteArrayContent(content)
            });
        return new HttpClient(handler.Object);
    }
}
```

**Note on test infrastructure dependencies:** If `BlobStorageResult` record signature differs from this plan's assumption (params: `Success`, `FilePath`, `FileSizeBytes`, `ErrorMessage`), read its current definition under `Api.Services.Pdf` namespace and align test arrange clauses. Production wins.

- [ ] **Step 2.4.2: Run tests — expect FAIL (BggCoverDownloader class does not exist)**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~BggCoverDownloaderTests" --nologo`
Expected: compile error / class not found.

- [ ] **Step 2.4.3: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloaderTests.cs
git commit -m "test(shared-games): add BggCoverDownloader failing tests (implementation pending)"
```

### Task 2.5: Implement `BggCoverDownloader`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs`

- [ ] **Step 2.5.1: Write implementation**

Create:

```csharp
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal sealed class BggCoverDownloader : IBggCoverDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<BggCoverDownloader> _logger;

    public BggCoverDownloader(
        HttpClient httpClient,
        IBlobStorageService blobStorageService,
        ILogger<BggCoverDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> DownloadAndUploadAsync(
        int bggId,
        string remoteImageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteImageUrl))
        {
            return null;
        }

        var resourceKey = $"bgg-cover-{bggId}";

        try
        {
            using var response = await _httpClient
                .GetAsync(remoteImageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BGG cover download failed: BggId={BggId}, Url={Url}, Status={Status}",
                    bggId, remoteImageUrl, response.StatusCode);
                return null;
            }

            await using var imageStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            // Derive a safe filename from the URL path
            var fileName = $"cover-{bggId}{GetExtension(remoteImageUrl)}";

            var storageResult = await _blobStorageService
                .StoreAsync(imageStream, fileName, BlobCategory.GameImage, resourceKey, cancellationToken)
                .ConfigureAwait(false);

            if (!storageResult.Success)
            {
                _logger.LogWarning(
                    "BGG cover upload failed: BggId={BggId}, Error={Error}",
                    bggId, storageResult.ErrorMessage);
                return null;
            }

            _logger.LogInformation(
                "BGG cover uploaded successfully: BggId={BggId}, R2Key={Key}",
                bggId, resourceKey);
            return resourceKey;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error in BGG cover download/upload: BggId={BggId}", bggId);
            return null;
        }
    }

    private static string GetExtension(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) || ext.Length > 5 ? ".jpg" : ext.ToLowerInvariant();
        }
        catch
        {
            return ".jpg";
        }
    }
}
```

- [ ] **Step 2.5.2: Register DI in SharedGameCatalogServiceExtensions**

Open `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs`.

Inside the extension method that registers SharedGameCatalog services, add:

```csharp
services.AddHttpClient<IBggCoverDownloader, BggCoverDownloader>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

This registers `BggCoverDownloader` as scoped via `AddHttpClient` (typed client pattern).

- [ ] **Step 2.5.3: Run tests — expect PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~BggCoverDownloaderTests" --nologo`
Expected: 3 tests pass.

- [ ] **Step 2.5.4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/BggCoverDownloader.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs
git commit -m "feat(shared-games): BggCoverDownloader implementation + HttpClient DI

Gap G2. 10s timeout, BlobCategory.GameImage, resource key bgg-cover-{id}.
On http or storage error returns null (caller fallback to direct URL)."
```

### Task 2.6: Extend `CoverUrlResolver` with L2.5 BGG layer

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs`

- [ ] **Step 2.6.1: Update `ResolvePublicAsync` to insert L2.5 BGG between L4 and L2**

Replace `ResolvePublicAsync` body with:

```csharp
public static async Task<string?> ResolvePublicAsync(
    SharedGameEntity sharedGame,
    IBlobStorageService blobStorage)
{
    ArgumentNullException.ThrowIfNull(sharedGame);
    ArgumentNullException.ThrowIfNull(blobStorage);

    // L4 PDF-derived cover
    if (!string.IsNullOrWhiteSpace(sharedGame.PdfCoverR2Key))
    {
        var url = await blobStorage
            .GetPresignedDownloadUrlAsync(
                $"{sharedGame.PdfCoverR2Key}-preview.webp",
                BlobCategory.GameImage,
                sharedGame.PdfCoverR2Key)
            .ConfigureAwait(false);
        if (url is not null) return url;
    }

    // L2.5 BGG re-uploaded cover (Gap G2)
    if (!string.IsNullOrWhiteSpace(sharedGame.BggCoverR2Key))
    {
        var url = await blobStorage
            .GetPresignedDownloadUrlAsync(
                sharedGame.BggCoverR2Key, // BGG upload stored under raw resource key
                BlobCategory.GameImage,
                sharedGame.BggCoverR2Key)
            .ConfigureAwait(false);
        if (url is not null) return url;
    }

    // L2 Wikidata cover
    if (!string.IsNullOrWhiteSpace(sharedGame.WikidataCoverR2Key))
    {
        var url = await blobStorage
            .GetPresignedDownloadUrlAsync(
                $"{sharedGame.WikidataCoverR2Key}.webp",
                BlobCategory.GameImage,
                sharedGame.WikidataCoverR2Key)
            .ConfigureAwait(false);
        if (url is not null) return url;
    }

    return null;
}
```

- [ ] **Step 2.6.2: Update XML doc comment at class level**

Update the class-level comment from `L3 (user) -> L4 (PDF) -> L2 (Wikidata)` to:

```csharp
/// <summary>
/// Issue #1852 (Gap A) + Gap G2 (2026-06-08): centralizes cover-URL resolution
/// with the priority L3 (user custom) -> L4 (PDF-derived) -> L2.5 (BGG re-uploaded)
/// -> L2 (Wikidata) -> null. Each layer falls through to the next when its R2 key
/// is missing or the blob storage cannot mint a presigned URL.
/// </summary>
```

- [ ] **Step 2.6.3: Verify compilation + existing resolver tests**

Run: `dotnet build apps/api/src/Api/Api.csproj --nologo`
Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CoverUrlResolver" --nologo`
Expected: build green, existing resolver tests still pass (we only ADD a layer, no behavior change for entities without `BggCoverR2Key`).

- [ ] **Step 2.6.4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs
git commit -m "feat(shared-games): CoverUrlResolver L2.5 BGG layer between L4 PDF and L2 Wikidata

Gap G2. Backward compatible: entities without BggCoverR2Key skip the new layer."
```

### Task 2.7: Integrate downloader into `CreateSharedGameFromPdfCommandHandler`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameFromPdfCommandHandler.cs`

- [ ] **Step 2.7.1: Inject `IBggCoverDownloader` into handler**

Update constructor and field:

```csharp
private readonly IBggCoverDownloader _bggCoverDownloader;
```

Add to constructor parameters and assignment (replicate the existing pattern of `??throw ArgumentNullException`).

- [ ] **Step 2.7.2: Modify Step 4/5 to attempt cover re-upload**

After the existing Step 4 BGG enrichment block (`bggDetails` populated), and BEFORE Step 5 (SharedGame.Create), add:

```csharp
// STEP 4.5: BGG cover re-upload (Gap G2)
string? bggCoverR2Key = null;
if (bggDetails is not null && !string.IsNullOrWhiteSpace(bggDetails.ImageUrl) && command.SelectedBggId.HasValue)
{
    bggCoverR2Key = await _bggCoverDownloader
        .DownloadAndUploadAsync(command.SelectedBggId.Value, bggDetails.ImageUrl, cancellationToken)
        .ConfigureAwait(false);

    if (bggCoverR2Key is null)
    {
        _logger.LogWarning(
            "BGG cover re-upload failed for BggId={BggId}, falling back to direct CDN URL",
            command.SelectedBggId.Value);
        // bggDetails.ImageUrl is still used as imageUrl below (existing behavior preserved)
    }
}
```

- [ ] **Step 2.7.3: Persist `BggCoverR2Key` on the entity after `_gameRepository.AddAsync`**

The `SharedGame` domain aggregate doesn't (and shouldn't) carry the R2 key directly — it's infrastructure metadata. After `_gameRepository.AddAsync(sharedGame, cancellationToken)`, set it on the EF-tracked entity:

```csharp
if (bggCoverR2Key is not null)
{
    var entity = await _dbContext.Set<SharedGameEntity>()
        .FirstOrDefaultAsync(e => e.Id == sharedGame.Id, cancellationToken)
        .ConfigureAwait(false);
    if (entity is not null)
    {
        entity.BggCoverR2Key = bggCoverR2Key;
    }
}
```

Note: this runs BEFORE `SaveChangesAsync` (Step 8), so the new column is persisted in the same transaction.

- [ ] **Step 2.7.4: Verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 2.7.5: Add 2 tests to existing handler test class**

Open `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameFromPdfCommandHandlerTests.cs`.

Find the test class, add a `Mock<IBggCoverDownloader>` field and pass it to the handler ctor in fixture setup.

Then append at the end of the class:

```csharp
[Fact]
public async Task Handle_WithBggId_InvokesCoverDownloaderAndPersistsR2Key()
{
    // Arrange
    _bggCoverDownloaderMock
        .Setup(d => d.DownloadAndUploadAsync(13, It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync("bgg-cover-13");

    // ... existing arrange that sets bggDetails with ImageUrl and command with SelectedBggId=13 ...

    // Act
    var result = await _sut.Handle(command, CancellationToken.None);

    // Assert
    _bggCoverDownloaderMock.Verify(d => d.DownloadAndUploadAsync(13, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

    var entity = await _dbContext.Set<SharedGameEntity>().FirstOrDefaultAsync(e => e.Id == result.GameId);
    entity!.BggCoverR2Key.Should().Be("bgg-cover-13");
}

[Fact]
public async Task Handle_WithBggId_OnDownloaderFailure_ContinuesWithoutBggCoverR2Key()
{
    // Arrange
    _bggCoverDownloaderMock
        .Setup(d => d.DownloadAndUploadAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((string?)null);

    // ... existing arrange ...

    // Act
    var result = await _sut.Handle(command, CancellationToken.None);

    // Assert
    var entity = await _dbContext.Set<SharedGameEntity>().FirstOrDefaultAsync(e => e.Id == result.GameId);
    entity!.BggCoverR2Key.Should().BeNull();
    // Existing imageUrl fallback (BGG direct URL) preserved — verify via separate assertion if the SUT exposes it.
}
```

**Note:** the existing test class may not have an `_dbContext` field or `_bggCoverDownloaderMock`. Read the file first to identify what to add and where (the goal is mirror the field+ctor injection used for other mocks).

- [ ] **Step 2.7.6: Run tests — expect PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CreateSharedGameFromPdfCommandHandlerTests" --nologo`
Expected: existing tests pass + 2 new tests pass.

If existing tests now fail with "Constructor signature mismatch" or "missing mock", update the existing test fixture to include `Mock<IBggCoverDownloader>` with default no-op setup (`SetupSequence` returning null) — this preserves backward compatibility for tests not exercising BGG enrichment.

- [ ] **Step 2.7.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameFromPdfCommandHandler.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/CreateSharedGameFromPdfCommandHandlerTests.cs
git commit -m "feat(shared-games): CreateSharedGameFromPdf re-uploads BGG cover to our storage

Gap G2. New Step 4.5 invokes IBggCoverDownloader if SelectedBggId present.
On failure, falls back to direct BGG URL (preserves existing imageUrl behavior).
2 new handler tests + ctor injection of IBggCoverDownloader."
```

---

## Phase 3 — G5: Idempotency-Key on wizard create

**Goal:** Prevenire creazione di duplicate shared games su double-submit. L'admin client genera `Idempotency-Key` (UUID v4) e lo invia come header su `POST /admin/shared-games/wizard/create`. Lo stesso key entro 5 min ritorna lo stesso `gameId` senza ri-eseguire il comando.

**Strategy:** Redis cache check (chiave `wizard:create:{userId}:{idempotencyKey}` → value `gameId`). TTL 5 min. Pre-Send check + Post-Send write.

### Task 3.1: Identify the Redis cache abstraction

- [ ] **Step 3.1.1: Discover existing cache service interface**

Run: `Grep pattern="IAiResponseCacheService|IHybridCacheService|interface I.*Cache" path="apps/api/src/Api" output_mode="files_with_matches" head_limit=10`

Expected: list of cache abstractions. Pick the one used most broadly (typically `IAiResponseCacheService` or `IHybridCacheService` — read its interface to confirm it supports `GetAsync<T>` and `SetAsync<T>(key, value, TTL)`).

- [ ] **Step 3.1.2: Read the chosen interface signature**

Read the cache service interface to confirm method signatures. Document chosen interface name (e.g., `IHybridCacheService`) in the next task.

If no suitable abstraction exists, use `IDistributedCache` from `Microsoft.Extensions.Caching.Distributed` (always available with Redis configured).

### Task 3.2: Write failing test for double-submit idempotency

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Integration/WizardCreateIdempotencyTests.cs`

- [ ] **Step 3.2.1: Inspect existing wizard integration tests for pattern**

Run: `Glob pattern="apps/api/tests/Api.Tests/**/*Wizard*.cs"`

Pick the closest existing pattern (probably a Testcontainers-based integration test that POSTs to the endpoint and asserts response).

- [ ] **Step 3.2.2: Write the failing test**

Create:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WizardCreateIdempotencyTests : IClassFixture<ApiIntegrationTestFixture>
{
    private readonly ApiIntegrationTestFixture _fixture;

    public WizardCreateIdempotencyTests(ApiIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DoubleSubmit_WithSameIdempotencyKey_ReturnsSameGameId()
    {
        // Arrange
        var client = await _fixture.AuthenticateAsAdminAsync();
        var pdfDocumentId = await _fixture.UploadOrphanPdfAsync(client);

        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfDocumentId,
            ExtractedTitle = "Idempotency Test Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = null
        };

        // Act — first submit
        var firstResponse = await PostWithIdempotencyKey(client, request, idempotencyKey);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();

        // Act — second submit identical
        var secondResponse = await PostWithIdempotencyKey(client, request, idempotencyKey);

        // Assert — same gameId, no new game created
        secondResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        secondResult!.GameId.Should().Be(firstResult!.GameId);

        var gameCount = await _fixture.CountGamesByTitleAsync("Idempotency Test Game");
        gameCount.Should().Be(1);
    }

    [Fact]
    public async Task SameIdempotencyKey_WithDifferentBody_Returns422()
    {
        // F2 (review finding): IETF draft-ietf-httpapi-idempotency-key §2.6
        // Arrange
        var client = await _fixture.AuthenticateAsAdminAsync();
        var pdfA = await _fixture.UploadOrphanPdfAsync(client);
        var pdfB = await _fixture.UploadOrphanPdfAsync(client);

        var key = Guid.NewGuid().ToString();
        var requestA = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfA,
            ExtractedTitle = "Original",
            MinPlayers = 2, MaxPlayers = 4, PlayingTimeMinutes = 60, MinAge = 10
        };
        var requestB_differentBody = requestA with { PdfDocumentId = pdfB, ExtractedTitle = "Different" };

        // Act
        var first = await PostWithIdempotencyKey(client, requestA, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await PostWithIdempotencyKey(client, requestB_differentBody, key);

        // Assert
        second.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task DoubleSubmit_WithDifferentIdempotencyKeys_CreatesTwoGames()
    {
        // Arrange
        var client = await _fixture.AuthenticateAsAdminAsync();
        var pdfA = await _fixture.UploadOrphanPdfAsync(client);
        var pdfB = await _fixture.UploadOrphanPdfAsync(client);

        var requestA = new CreateGameFromPdfRequest
        {
            PdfDocumentId = pdfA,
            ExtractedTitle = "Game A",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            SelectedBggId = null
        };
        var requestB = requestA with { PdfDocumentId = pdfB, ExtractedTitle = "Game B" };

        // Act
        var responseA = await PostWithIdempotencyKey(client, requestA, Guid.NewGuid().ToString());
        var responseB = await PostWithIdempotencyKey(client, requestB, Guid.NewGuid().ToString());

        // Assert
        responseA.StatusCode.Should().Be(HttpStatusCode.Created);
        responseB.StatusCode.Should().Be(HttpStatusCode.Created);

        var resultA = await responseA.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        var resultB = await responseB.Content.ReadFromJsonAsync<CreateGameFromPdfResult>();
        resultA!.GameId.Should().NotBe(resultB!.GameId);
    }

    private static async Task<HttpResponseMessage> PostWithIdempotencyKey(
        HttpClient client,
        CreateGameFromPdfRequest request,
        string idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/shared-games/wizard/create")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(message);
    }
}
```

**Note on fixture helpers:** `ApiIntegrationTestFixture.AuthenticateAsAdminAsync`, `UploadOrphanPdfAsync`, `CountGamesByTitleAsync` likely don't exist with these exact names. Read the existing fixture file (search via `Grep pattern="class ApiIntegrationTestFixture" path="apps/api/tests"`) and adapt: either use existing methods or extend the fixture with the missing helpers in the same commit.

- [ ] **Step 3.2.3: Run tests — expect FAIL**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WizardCreateIdempotencyTests" --nologo`
Expected: FAIL — second submit creates a 2nd game (no idempotency yet).

- [ ] **Step 3.2.4: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Integration/WizardCreateIdempotencyTests.cs
git commit -m "test(shared-games): wizard create idempotency double-submit tests (failing, implementation pending)"
```

### Task 3.3: Implement Idempotency-Key check in `HandleWizardCreateGame`

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogWizardEndpoints.cs`

- [ ] **Step 3.3.0: Define cached envelope record at bottom of `SharedGameCatalogWizardEndpoints.cs`**

After the closing brace of the static class `SharedGameCatalogWizardEndpoints`, add (still inside `namespace Api.Routing`):

```csharp
/// <summary>
/// G5: envelope persisted in Redis cache for Idempotency-Key replay.
/// Stores the gameId + status fields needed to replay HTTP 201 truthfully,
/// plus the SHA256 of the original request body for IETF idempotency-key
/// body-mismatch detection (RFC draft-ietf-httpapi-idempotency-key §2.6).
/// </summary>
internal sealed record IdempotencyCachedEnvelope
{
    public Guid GameId { get; init; }
    public string ApprovalStatus { get; init; } = string.Empty;
    public bool BggEnrichmentApplied { get; init; }
    public int? EnrichedWithBggId { get; init; }
    public string BodyHash { get; init; } = string.Empty;
}
```

- [ ] **Step 3.3.1: Inject cache service into the handler**

In `SharedGameCatalogWizardEndpoints.HandleWizardCreateGame` (~line 254), add `IHybridCacheService cache` (or whichever cache abstraction was chosen in Task 3.1) to the parameter list. ASP.NET minimal API DI will resolve it.

The new signature:

```csharp
private static async Task<IResult> HandleWizardCreateGame(
    CreateGameFromPdfRequest request,
    HttpContext context,
    IMediator mediator,
    IHybridCacheService cache, // or the chosen interface
    ILogger<Program> logger,
    CancellationToken ct)
```

Replace `IHybridCacheService` with the actual interface name discovered in Task 3.1.

- [ ] **Step 3.3.2: Add idempotency check at top of handler body**

Insert at the beginning of the handler body, before `var userId = context.User.GetUserId();`:

```csharp
var userId = context.User.GetUserId();

// G5: Idempotency-Key support
string? idempotencyKey = null;
if (context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues) && keyValues.Count > 0)
{
    idempotencyKey = keyValues[0];
}

// F2 (review finding, Nygard+Wiegers): hash body to detect key-reuse with different payload
string? requestBodyHash = null;
if (!string.IsNullOrWhiteSpace(idempotencyKey))
{
    var bodyJson = System.Text.Json.JsonSerializer.Serialize(request);
    using var sha = System.Security.Cryptography.SHA256.Create();
    var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(bodyJson));
    requestBodyHash = Convert.ToHexString(hashBytes);
}

if (!string.IsNullOrWhiteSpace(idempotencyKey))
{
    var cacheKey = $"wizard:create:{userId}:{idempotencyKey}";
    var cachedEnvelopeJson = await cache.GetAsync<string>(cacheKey, ct).ConfigureAwait(false);
    if (!string.IsNullOrWhiteSpace(cachedEnvelopeJson))
    {
        var cached = System.Text.Json.JsonSerializer.Deserialize<IdempotencyCachedEnvelope>(cachedEnvelopeJson);
        if (cached is not null)
        {
            // F2: mismatch on body hash → 422 (IETF draft-ietf-httpapi-idempotency-key §2.6)
            if (!string.Equals(cached.BodyHash, requestBodyHash, StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Idempotency-Key reused with different body: Key={Key}, UserId={UserId}",
                    idempotencyKey, userId);
                return Results.UnprocessableEntity(new
                {
                    error = "idempotency_key_body_mismatch",
                    message = "Idempotency-Key was previously used with a different request body."
                });
            }

            logger.LogInformation(
                "Idempotency-Key hit: returning cached gameId={GameId} for key={Key}",
                cached.GameId, idempotencyKey);

            // F1: restore real status from cache so client sees true Draft/Published
            return Results.Created(
                $"/api/v1/admin/shared-games/{cached.GameId}",
                new CreateGameFromPdfResult
                {
                    GameId = cached.GameId,
                    ApprovalStatus = cached.ApprovalStatus,
                    QualityScore = 0.0,
                    DuplicateWarning = false,
                    DuplicateTitles = new List<string>(),
                    BggEnrichmentApplied = cached.BggEnrichmentApplied,
                    EnrichedWithBggId = cached.EnrichedWithBggId
                });
        }
    }
}
```

**Remove** the earlier `var userId = context.User.GetUserId();` line since we moved it to the top of this block (avoid double declaration).

- [ ] **Step 3.3.3: Persist gameId after successful create**

In the existing `try` block, after `var result = await mediator.Send(command, ct).ConfigureAwait(false);`, BEFORE the `return Results.Created(...)`, add:

```csharp
if (!string.IsNullOrWhiteSpace(idempotencyKey) && !string.IsNullOrWhiteSpace(requestBodyHash))
{
    var cacheKey = $"wizard:create:{userId}:{idempotencyKey}";
    // F1+F2: cache envelope preserves status + bggEnrichment + bodyHash for safe replay
    var envelope = new IdempotencyCachedEnvelope
    {
        GameId = result.GameId,
        ApprovalStatus = result.ApprovalStatus,
        BggEnrichmentApplied = result.BggEnrichmentApplied,
        EnrichedWithBggId = result.EnrichedWithBggId,
        BodyHash = requestBodyHash
    };
    var envelopeJson = System.Text.Json.JsonSerializer.Serialize(envelope);
    await cache
        .SetAsync(cacheKey, envelopeJson, TimeSpan.FromMinutes(5), ct)
        .ConfigureAwait(false);
}
```

Adapt `cache.SetAsync` signature to whatever the chosen cache service exposes (some take `DistributedCacheEntryOptions` instead).

- [ ] **Step 3.3.4: Verify compilation**

Run: `dotnet build apps/api/src/Api/Api.csproj --nologo`
Expected: 0 errors.

- [ ] **Step 3.3.5: Run integration tests — expect PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WizardCreateIdempotencyTests" --nologo`
Expected: both tests pass.

If 1st test fails because the `Idempotency-Key` header is dropped during cache reconstruction, the issue is the cached response shape: the test asserts `secondResult.GameId.Should().Be(firstResult.GameId)` — make sure the cached path returns `Results.Created` with a body containing `GameId` equal to the cached value.

- [ ] **Step 3.3.6: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogWizardEndpoints.cs
git commit -m "feat(shared-games): Idempotency-Key support on wizard/create endpoint

Gap G5. Header Idempotency-Key (UUID) + Redis cache (key wizard:create:{userId}:{key})
with 5 min TTL. Same key entro TTL ritorna gameId cached. Tests in same PR."
```

---

## Phase 4 — Final integration & PR

### Task 4.1: Run full SharedGameCatalog test slice

- [ ] **Step 4.1.1: Run all SharedGameCatalog tests**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SharedGameCatalog" --nologo`
Expected: all green. Baseline regressions (pre-existing failing tests documented in CLAUDE.md `Known Flaky Tests`) acceptable.

- [ ] **Step 4.1.2: Run DocumentProcessing test slice (for cross-bc impact)**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DocumentProcessing" --nologo`
Expected: all green.

### Task 4.2: Manual smoke verify on dev env

- [ ] **Step 4.2.1: Start dev stack**

From `infra/`: `make dev`
Wait until `make logs s=api` shows API ready.

- [ ] **Step 4.2.2: Verify G1 — update game with tag fields**

Use a curl/HTTP client to PATCH `/api/v1/admin/shared-games/{id}` with a payload containing `categories: ["Strategy"]`, `mechanics: ["Dice Rolling"]`, `bggId: 13`. Verify subsequent GET returns these fields.

- [ ] **Step 4.2.3: Verify G2 — create game with BGG selection re-uploads cover**

Walk through the wizard via `/admin/shared-games/new` selecting a BGG match. After create, query the DB: `SELECT BggCoverR2Key FROM shared_games WHERE id = '<new_game_id>'`. Expected: non-null R2 key.

- [ ] **Step 4.2.4: Verify G5 — double-submit returns same gameId**

POST `/api/v1/admin/shared-games/wizard/create` twice with same `Idempotency-Key`. Verify both responses have identical `gameId`.

### Task 4.3: Open PR

- [ ] **Step 4.3.1: Create feature branch (if not already)**

```bash
git checkout main-dev
git pull --ff-only
# Verify HEAD is main-dev:
git branch --show-current  # must print main-dev
git checkout -b feature/admin-shared-game-gap-closure-g1-g2-g5
```

If the previous commits were made on `main-dev` directly, rebase them onto a feature branch first.

- [ ] **Step 4.3.2: Push and open PR**

```bash
git push -u origin feature/admin-shared-game-gap-closure-g1-g2-g5

gh pr create --base main-dev --title "feat(shared-games): close gaps G1+G2+G5 on admin import workflow" --body "$(cat <<'EOF'
## Summary

Closes 3 gaps identified by spec-panel review on the existing admin shared-game import workflow (see [spec](../docs/superpowers/specs/2026-06-08-admin-shared-game-import-spec-panel-review.md)).

- **G1**: `UpdateSharedGameCommand` now supports manual edit of `Categories`, `Mechanics`, `Designers`, `Publishers`, `BggId` via DbContext-include pattern (mirrors `UpdateSharedGameFromBggCommandHandler`).
- **G2**: `CreateSharedGameFromPdfCommandHandler` Step 4.5 invokes new `BggCoverDownloader` to re-upload BGG cover to our blob storage. New `BggCoverR2Key` column on `SharedGameEntity`. `CoverUrlResolver` extended with L2.5 layer (priority: L3 user → L4 PDF → L2.5 BGG → L2 Wikidata). On download/upload failure, falls back to direct BGG URL (existing behavior).
- **G5**: `POST /admin/shared-games/wizard/create` supports `Idempotency-Key` header via Redis cache. Same key within 5 min returns cached `gameId` (HTTP 201 with `ApprovalStatus: "Cached"` to signal replay).

## Out of scope (documented in plan)

- **G3** UI tab labels (cosmetic)
- **G4** single-page vs 3-step wizard UX redesign (design choice)
- **G6** admin quota bypass — false positive: `UploadPdfForGameExtractionCommandHandler:13` already quota-free by design

## Test plan

- [x] Unit: `UpdateSharedGameCommandHandlerTests` (3 new) — core, taxonomy replace, BggId scalar
- [x] Unit: `BggCoverDownloaderTests` (3 new) — happy, http error, upload error
- [x] Unit: `CreateSharedGameFromPdfCommandHandlerTests` (2 new) — re-upload success + fallback
- [x] Integration: `WizardCreateIdempotencyTests` (2 new) — double-submit dedup + different keys create 2 games
- [x] Validator extended for taxonomy + BggId
- [x] EF migration `AddBggCoverR2KeyToSharedGames` applied locally
- [x] Manual smoke G1/G2/G5 verified on dev env

## Migrations

- 1 new migration: `<timestamp>_AddBggCoverR2KeyToSharedGames` — adds nullable `BggCoverR2Key varchar(256)` to `shared_games`. Zero-downtime; reversible.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4.3.3: Verify CI**

Wait for CI status. Expected required checks pass (only `GitGuardian Security Checks` is hard required on `main-dev` per P196).

If CI fails on a check NOT in baseline known-flaky list, investigate and fix in this PR.

### Task 4.4: Merge after approval

- [ ] **Step 4.4.1: Wait for review or self-approve if no reviewers configured**

- [ ] **Step 4.4.2: Merge via normal PR merge (no admin override needed if CI green)**

```bash
gh pr merge --merge --delete-branch
```

If admin override is required because of CodeQL csharp pending (analysis-only, non-blocking per P196 baseline), use:
```bash
gh pr merge --admin --merge --delete-branch
```

### Task 4.5: Close umbrella issue if exists

- [ ] **Step 4.5.1: Comment + close**

If this PR is tracked by an umbrella issue, post status summary referencing the spec + PR, then close `--reason completed`.

---

## Self-Review Checklist (Plan author)

Reviewed against the spec consolidato (`docs/superpowers/specs/2026-06-08-admin-shared-game-import-spec-panel-review.md`):

1. **Spec coverage**:
   - DEC-1 saga atomica BGG-prima — already in `CreateSharedGameFromPdfCommandHandler`, ✅ no work.
   - DEC-2 BGG manual select — already enforced UI-side, ✅ no work.
   - DEC-3 `/{uuid}` post-create edit — already in `/admin/shared-games/[id]/client.tsx`, ✅ no work. G1 task extends backend command to match UI form's fields.
   - DEC-4 cover re-upload on our storage — ✅ Phase 2 G2.
   - DEC-5 Admin-only — already enforced by `AdminOnlyPolicy` on wizard endpoints, ✅ no work.
   - DEC-6 admin quota exempt — already in `UploadPdfForGameExtractionCommandHandler` design, ✅ no work.
   - AC-9 edit form full fields — ✅ G1 backend enables it (FE form already exposes them per `/admin/shared-games/[id]` discovery).
   - AC-10 RowVersion concurrency — pre-existing pattern in `EntityConfigurations` (cfr. CLAUDE.md). Not touched.
   - AC-11 publish — `QuickPublishSharedGameCommand` exists, no work.
   - AC-12 idempotency — ✅ G5.

2. **Placeholder scan**: no TBD/TODO/"implement later"/"similar to Task N". All code blocks complete or accompanied by inline read-current-source directives.

3. **Type consistency**:
   - `UpdateSharedGameCommand` record name consistent across tasks 1.1–1.5.
   - `IBggCoverDownloader.DownloadAndUploadAsync` signature consistent across tasks 2.3–2.7.
   - `BggCoverR2Key` field name consistent across tasks 2.1–2.7.
   - `Idempotency-Key` header consistent across tasks 3.2–3.3.

4. **Known drift risks** flagged inline:
   - `SharedGame.Create` signature may have drifted; plan instructs to read current source and align test.
   - `BlobStorageResult` constructor params may differ; plan instructs to verify before mock setup.
   - `IHybridCacheService` interface name may differ; plan instructs Task 3.1 discovery before Task 3.2.
   - `ApiIntegrationTestFixture` helpers may not exist; plan instructs to read fixture and extend if missing.

---

## Review Findings & Applied Fixes (2026-06-08, post `/sc:spec-panel --mode critique`)

Spec-panel review condotta con Fowler (architecture) + Crispin (testing) + Nygard (failure modes) + Wiegers (contract). 4 MAJ + 3 MIN findings identificati.

### MAJ fixes applied inline

| # | Finding | Esperto | Fix applicato |
|---|---------|---------|---------------|
| **F1** | Cache value G5 perde `ApprovalStatus` reale — replay restituiva placeholder "Cached" come contract change | Nygard | Task 3.3.0 + 3.3.2: cache JSON envelope `IdempotencyCachedEnvelope` con `GameId + ApprovalStatus + BggEnrichmentApplied + EnrichedWithBggId + BodyHash`. Replay restituisce status reale |
| **F2** | Idempotency-Key con body diverso non rifiutato (viola IETF draft-ietf-httpapi-idempotency-key §2.6) | Nygard + Wiegers | Task 3.3.2: SHA256 del body, cache hash, mismatch → 422 Unprocessable Entity. Task 3.2.2: nuovo test `SameIdempotencyKey_WithDifferentBody_Returns422` |

### MAJ flagged for implementer judgment (no inline fix)

| # | Finding | Esperto | Note |
|---|---------|---------|------|
| **F3** | Task 1.3 test `Handle_WithCategoriesAndMechanics_ReplacesCollections` mescola InMemory DbContext + repository mock. `_repository.Update(domain)` è no-op su InMemory, test verifica solo side-effect entity-level (categories Add). Test funziona ma è fragile | Fowler + Crispin | **Note for implementer**: il test passa ma copre solo "entity field replacement", NON "aggregate Update flow". Se durante implementation emerge che il repository pattern reale richiede ITransaction o DbContext shared, considera l'aggiunta di un test integration Testcontainers `UpdateSharedGameHandlerIntegrationTests` per coverage completa. Pattern accettato come compromesso unit-test efficiency vs full coverage |
| **F4** | Dopo re-upload BGG cover, `entity.ImageUrl` rimane = URL diretto BGG CDN. Due source-of-truth: `ImageUrl` field + `BggCoverR2Key` via resolver | Fowler | **Convenzione documentata**: `CoverUrlResolver` è l'authoritative source per cover URL al display layer. `entity.ImageUrl` resta come fallback legacy per consumer pre-resolver (alcuni query handler proiettano direttamente `entity.ImageUrl`). Non modificare `ImageUrl` durante saga: cambiare la convenzione richiede audit cross-BC (out of scope) |

### MIN findings deferred

- **F5** (Wiegers, validator limits 20/30 arbitrary): documenta nel commit body i numeri come "initial heuristic, tune in follow-up after observing real catalog stats". Defer.
- **F6** (Crispin, test fixture helpers assumed): plan already includes inline disclaimer at Task 3.2.2. Considera linkare il path `Grep pattern="class ApiIntegrationTestFixture" path="apps/api/tests"` nel discovery step.
- **F7** (Fowler, L2.5 key asymmetry vs L4 `-preview.webp`): aggiungi inline comment nel `CoverUrlResolver`: `// BGG cover stored as single asset (no thumbnail derivative) — different from L4 PDF which has -preview.webp suffix`.

### Quality scoring post-review

| Dimensione | Pre-review | Post-review | Delta |
|-----------|-----------|-------------|-------|
| Contract correctness | 7/10 | 9/10 | +2 (F1+F2) |
| Testability | 8/10 | 8.5/10 | +0.5 (F2 test added) |
| Implementation guidance | 8/10 | 9/10 | +1 (F3+F4 notes) |
| **Overall** | **7.6/10** | **8.8/10** | **+1.2** |

---

## Execution Handoff

Plan complete (review-applied) and saved to `docs/superpowers/plans/2026-06-08-admin-shared-game-gap-closure-g1-g2-g5.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration. Pattern P120 (model mix based on task complexity: haiku for trivial scaffolding, sonnet for handler refactor). Pattern P186 (trust-but-verify implementer reports).

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints for review.

**Which approach?**

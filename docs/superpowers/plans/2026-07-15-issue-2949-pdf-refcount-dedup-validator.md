# Follow-up cover-da-PDF (#2949): ref-counting delete + dedup centralization + validator — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the data-loss bug where deleting a multi-linked PDF orphans a blob/record still referenced by another game, migrate `AddRulebookCommandHandler` to the centralized `IPdfDeduplicationService`, and add a defense-in-depth validator for `MaterializePdfCoverCommand`.

**Architecture:** Three backend-only changes in the same PR against `main-dev`. Task 1 (the substantial one) gates `DeletePdfCommandHandler`'s record/blob/vector/event removal behind an `IEntityLinkRepository.GetCountForEntityAsync(KbCard, pdfGuid) <= 1` ref-count check (last-link semantics); when other links remain it removes only the caller's `Game→KbCard` link. Task 2 replaces the inline SHA-256 + `FindByContentHashAsync` + reuse-via-EntityLink block in `AddRulebookCommandHandler` with `IPdfDeduplicationService.EvaluateAsync`, replicating the exact pattern already used by `CompleteChunkedUploadCommandHandler`. Task 3 adds a `FluentValidation` validator auto-registered via the existing DocumentProcessing assembly scan.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + MediatR (CQRS), EF Core (PostgreSQL 16 + pgvector), FluentValidation, xUnit + Testcontainers (`SharedTestcontainersFixture`), Moq, FluentAssertions.

## Global Constraints

- **Branch:** `feature/issue-2949-pdf-refcount-dedup-validator`, created from `main-dev` (HEAD = `963ebbd65`, merge of #2943). Run the branch-hygiene pre-check first: `git branch --show-current` MUST print `main-dev`, `git status` MUST be clean, `git pull --ff-only` MUST succeed, THEN `git checkout -b feature/issue-2949-pdf-refcount-dedup-validator`.
- **PR target:** `main-dev` (the parent branch), NOT `main`. Set `git config branch.feature/issue-2949-pdf-refcount-dedup-validator.parent main-dev`.
- **Commit convention:** `feat|fix|refactor|test|chore(scope): description`. End every commit message with the co-author trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **CQRS (🔴 critical):** endpoints use ONLY `IMediator.Send()`. Never inject services directly into endpoints. (This plan does not touch endpoints — handlers only.)
- **DDD:** entities have private setters + factory methods (`EntityLink.Create(...)`); value objects immutable. Do NOT mutate `EntityLink` via reflection or setters — seed via `EntityLink.Create(...)`.
- **Exceptions:** use `Api.Middleware.Exceptions.NotFoundException` (404) / `ConflictException` (409). NEVER throw `InvalidOperationException` (would surface as 500). `DeletePdfCommandHandler` returns a `PdfDeleteResult` result-pattern object for not-found (does not throw) — preserve that.
- **DI:** any new interface must have BOTH `IService` and implementation registered. `IEntityLinkRepository → EntityLinkRepository` is ALREADY registered (`EntityRelationshipsServiceExtensions.cs:20`), and `IPdfDeduplicationService → PdfDeduplicationService` is ALREADY registered (used by `CompleteChunkedUploadCommandHandler`). No new registration needed for Tasks 1 and 2. Task 3's validator is auto-registered via `AddValidatorsFromAssemblyContaining<BulkDeletePdfsCommandValidator>(includeInternalTypes: true)` (`ApplicationServiceExtensions.cs:289`) — no explicit registration.
- **Meziantou MA0025:** never leave `throw new NotImplementedException()` stubs — do real TDD (red test → minimal real impl). The build fails on `NotImplementedException` stubs.
- **SonarAnalyzer S1135:** a `// TODO(...)` comment in C# is a BUILD ERROR. If you must annotate a follow-up, write `// Follow-up:` instead.
- **Test namespace collision:** MediatR's `Unit` type collides with the `Api.Tests.Unit` namespace. This plan's tests live under `Api.Tests.Integration.DocumentProcessing` and `Api.Tests.BoundedContexts.DocumentProcessing.*` — no `using MediatR;` + `Unit` ambiguity arises. When mocking `IMediator.Send` for a command returning a value (`CreateEntityLinkCommand : ICommand<EntityLinkDto>`, `GetPdfPageImageQuery : IRequest<byte[]>`), use `.ReturnsAsync(<value>)`, never `.Returns(Unit.Value)`.
- **Windows testhost:** kill lingering testhost before running BE tests: `pwsh -c "tasklist | Select-String testhost"` → `taskkill //PID <PID> //F` if present.
- **Culture-independent percentages** (if ever formatting one): `$"{val*100:0}%"`. Not needed by these tasks.

---

## File Structure

| File | Change | Responsibility |
|------|--------|----------------|
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeletePdfCommandHandler.cs` | Modify | Inject `IEntityLinkRepository`; add last-link ref-count gate; when other links remain, remove only the caller's `Game→KbCard` link and skip record/blob/vector/event deletion. |
| `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeletePdfIntegrationTests.cs` | Modify | Register `IEntityLinkRepository` in the test container; update the 5 `new DeletePdfCommandHandler(...)` call sites to pass it; add 2 new tests (multi-link → record/blob preserved + only caller link removed; single-link → full delete unchanged). |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/AddRulebookCommandHandler.cs` | Modify | Replace inline `ComputeContentHashAsync` + `FindByContentHashAsync` dedup with `IPdfDeduplicationService.EvaluateAsync`; keep reuse-via-EntityLink + Failed-state-as-new-upload behavior. |
| `apps/api/tests/Api.Tests/Integration/DocumentProcessing/AddRulebookDedupIntegrationTests.cs` | Create | Integration test proving the migrated handler still reuses an existing Ready PDF via EntityLink (no new record) and performs a full upload when no hash match exists. |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidator.cs` | Create | `AbstractValidator<MaterializePdfCoverCommand>` enforcing `PageNumber > 0`. |
| `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidatorTests.cs` | Create | Unit tests: pass on `PageNumber = 1`, fail on `PageNumber = 0` and `PageNumber = -1`. |

---

## Task 1: Ref-counting delete (DEC-6)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeletePdfCommandHandler.cs:14-85` (constructor + `Handle` body)
- Test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeletePdfIntegrationTests.cs` (register repo + fix 5 call sites + 2 new tests)

**Interfaces:**
- Consumes:
  - `IEntityLinkRepository.GetCountForEntityAsync(MeepleEntityType entityType, Guid entityId, CancellationToken ct = default) : Task<int>` — counts links where entity is source OR bidirectional target. `Game→KbCard/RelatedTo` is bidirectional (`RelatedTo.IsBidirectional() == true`), so this counts every `Game→KbCard` link targeting `pdfGuid`.
  - `IEntityLinkRepository.GetForEntityAsync(MeepleEntityType entityType, Guid entityId, EntityLinkScope? scope = null, EntityLinkType? linkType = null, MeepleEntityType? targetEntityType = null, CancellationToken ct = default) : Task<IReadOnlyList<EntityLink>>` — used to fetch the caller's link(s) for removal.
  - `IEntityLinkRepository.Remove(EntityLink entityLink) : void`
  - `EntityLink` aggregate: `SourceEntityType`, `SourceEntityId`, `TargetEntityType`, `TargetEntityId`, `LinkType` (all `public get; private set;`); factory `EntityLink.Create(sourceType, sourceId, targetType, targetId, linkType, scope, ownerUserId, metadata=null, isBggImported=false)`.
  - Enums: `MeepleEntityType.Game = 1`, `MeepleEntityType.KbCard = 9`; `EntityLinkType.RelatedTo = 5`; `EntityLinkScope.User = 1`.
  - `PdfDeleteResult(bool Success, string Message, string? GameId)` — existing DTO.
- Produces:
  - `DeletePdfCommandHandler(MeepleAiDbContext db, IBlobStorageService blobStorageService, IAiResponseCacheService cacheService, ILogger<DeletePdfCommandHandler> logger, IEntityLinkRepository entityLinkRepository, IMediator? mediator = null)` — the NEW constructor signature (adds `entityLinkRepository` as the 5th positional required param, before the optional `mediator`).

### Behavior spec (locked)

`Handle` resolves `pdfGuid` and loads the `PdfDocumentEntity`. After confirming it exists, BEFORE removing anything:

1. `linkCount = await _entityLinkRepository.GetCountForEntityAsync(MeepleEntityType.KbCard, pdfGuid, ct)`.
2. **If `linkCount > 1`** (PDF still referenced by ≥2 games after this delete): DO NOT remove the PDF record, blob, vectors, or raise `PdfDeletedDomainEvent`. Instead remove ONLY the caller's `Game→KbCard/RelatedTo` link (the one whose `SourceEntityId == pdfDoc.SharedGameId`), `SaveChanges`, and return `new PdfDeleteResult(true, "PDF unlinked from game (still referenced by other games)", storageGameId)`.
   - The caller's game id is `pdfDoc.SharedGameId` (rulebook/catalog uploads set `SharedGameId`). If `SharedGameId` is null, there is no owning-game link to remove for this call — return the same success message without removing a link (defensive; the record stays intact because other links exist).
3. **If `linkCount <= 1`** (last link, or zero links — e.g. legacy private PDFs never linked): proceed with the existing full-delete path unchanged (vectors → record → event → blob → cache).

> **Why `GetCountForEntityAsync` is correct here:** `Game→KbCard` links use `EntityLinkType.RelatedTo`, which `IsBidirectional() == true`. `GetCountForEntityAsync(KbCard, pdfGuid)` counts `(x.IsBidirectional && x.TargetEntityType == KbCard && x.TargetEntityId == pdfGuid)` — i.e. every game linking this PDF. `<= 1` means "this is the last (or only) game link", so deleting the record is safe.

- [ ] **Step 1.1: Write the two failing integration tests**

Add these two `[Fact]` methods to `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeletePdfIntegrationTests.cs` (paste after the existing `DeleteWithBlobStorageFailure_StillSucceedsWithWarningLogged` method, before `EnsureCreatedWithRetry`). They reference a helper `SeedGameKbCardLinkAsync` defined in the same step, and a second-game seeding inline.

```csharp
    [Fact]
    public async Task DeletePdf_StillLinkedBySecondGame_PreservesRecordAndRemovesOnlyCallerLink()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("MultiLinked.pdf", withVectorDoc: true);
        var callerGameId = (await _dbContext!.SharedGames.FirstAsync(TestCancellationToken)).Id;
        var callerUserId = (await _dbContext.Users.FirstAsync(TestCancellationToken)).Id;

        // Second game that also links this PDF.
        var secondGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = secondGameId,
            Title = "Second Game Linking Same PDF",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Two EntityLinks Game -> KbCard/pdfId (caller game + second game).
        await SeedGameKbCardLinkAsync(callerGameId, pdfId, callerUserId);
        await SeedGameKbCardLinkAsync(secondGameId, pdfId, callerUserId);

        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: success, but the PDF record + vectors are PRESERVED (still referenced).
        result.Success.Should().BeTrue();

        var pdfExists = await _dbContext.PdfDocuments.AnyAsync(p => p.Id == pdfId, TestCancellationToken);
        pdfExists.Should().BeTrue("PDF is still referenced by the second game");

        var vectorExists = await _dbContext.VectorDocuments.AnyAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorExists.Should().BeTrue("vectors must survive while the PDF record survives");

        // Only the caller's link was removed; the second game's link remains.
        var callerLinkExists = await _dbContext.EntityLinks.AnyAsync(
            el => el.SourceEntityId == callerGameId && el.TargetEntityId == pdfId
                  && el.TargetEntityType == MeepleEntityType.KbCard,
            TestCancellationToken);
        callerLinkExists.Should().BeFalse("caller's game->PDF link should be removed");

        var secondLinkExists = await _dbContext.EntityLinks.AnyAsync(
            el => el.SourceEntityId == secondGameId && el.TargetEntityId == pdfId
                  && el.TargetEntityType == MeepleEntityType.KbCard,
            TestCancellationToken);
        secondLinkExists.Should().BeTrue("second game's link must be preserved");
    }

    [Fact]
    public async Task DeletePdf_LastRemainingLink_DeletesRecordAndVectors()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("LastLink.pdf", withVectorDoc: true);
        var callerGameId = (await _dbContext!.SharedGames.FirstAsync(TestCancellationToken)).Id;
        var callerUserId = (await _dbContext.Users.FirstAsync(TestCancellationToken)).Id;

        // Exactly one EntityLink Game -> KbCard/pdfId.
        await SeedGameKbCardLinkAsync(callerGameId, pdfId, callerUserId);

        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: last link -> full delete (record + vectors gone).
        result.Success.Should().BeTrue();

        var pdfExists = await _dbContext.PdfDocuments.AnyAsync(p => p.Id == pdfId, TestCancellationToken);
        pdfExists.Should().BeFalse("last link removed -> PDF record deleted");

        var vectorExists = await _dbContext.VectorDocuments.AnyAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);
        vectorExists.Should().BeFalse("last link removed -> vectors deleted");
    }

    private async Task SeedGameKbCardLinkAsync(Guid gameId, Guid pdfId, Guid ownerUserId)
    {
        var link = Api.BoundedContexts.EntityRelationships.Domain.Aggregates.EntityLink.Create(
            sourceEntityType: MeepleEntityType.Game,
            sourceEntityId: gameId,
            targetEntityType: MeepleEntityType.KbCard,
            targetEntityId: pdfId,
            linkType: EntityLinkType.RelatedTo,
            scope: EntityLinkScope.User,
            ownerUserId: ownerUserId);
        _dbContext!.EntityLinks.Add(link);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }
```

Add these `using` directives to the top of the test file (after the existing block, before the `namespace`):

```csharp
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
```

- [ ] **Step 1.2: Register `IEntityLinkRepository` in the test container and update the 5 `new DeletePdfCommandHandler(...)` call sites**

In `DeletePdfIntegrationTests.cs`, inside `InitializeAsync`, add the repo registration right after the existing `services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();` line:

```csharp
        services.AddScoped<IEntityLinkRepository, EntityLinkRepository>();
```

> `EntityLinkRepository`'s constructor is `(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)`. `IntegrationServiceCollectionBuilder.CreateBase` registers the DbContext and the `IDomainEventCollector` (both are part of the shared kernel base registration used by other integration tests). If DI resolution of `IDomainEventCollector` fails at `BuildServiceProvider`, add `services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Infrastructure.DomainEventCollector>();` — but attempt without it first and only add if the container throws for that service.

Now update every `new DeletePdfCommandHandler(...)` in the file to pass an `IEntityLinkRepository` as the 5th argument. There are exactly 5 call sites:
1. `CreateIndependentHandler()` (~line 361): resolve the repo from the scope and pass it.
2. `DeletePdfWithVectorEmbeddings_RemovesVectorDocument` (~line 303)
3. `DeleteWithDbUpdateException_ThrowsPdfStorageException` — `faultyHandler` (~line 378)
4. `DeleteWithVectorDocument_SuccessfullyRemovesBothRecords` (~line 399)
5. `DeleteWithBlobStorageFailure_StillSucceedsWithWarningLogged` (~line 435)

For `CreateIndependentHandler()`, replace the method body with:

```csharp
    private DeletePdfCommandHandler CreateIndependentHandler()
    {
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        var cache = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>();
        var entityLinks = scope.ServiceProvider.GetRequiredService<IEntityLinkRepository>();

        return new DeletePdfCommandHandler(dbContext, blobStorage, cache, logger, entityLinks);
    }
```

For the 4 inline `new DeletePdfCommandHandler(...)` sites, add `_serviceProvider!.GetRequiredService<IEntityLinkRepository>()` as the 5th argument. Example — site #2 becomes:

```csharp
        var handler = new DeletePdfCommandHandler(
            _dbContext!,
            _serviceProvider!.GetRequiredService<IBlobStorageService>(),
            _serviceProvider!.GetRequiredService<IAiResponseCacheService>(),
            _serviceProvider!.GetRequiredService<ILogger<DeletePdfCommandHandler>>(),
            _serviceProvider!.GetRequiredService<IEntityLinkRepository>()
        );
```

Apply the identical 5th-argument addition to sites #3 (`faultyHandler` — note it uses `disposedContext` as arg 1, keep that), #4, and #5. Do NOT change the existing `_serviceProvider!.GetRequiredService<DeletePdfCommandHandler>()` resolutions — those get the new dependency injected automatically once the handler ctor and registration are updated.

- [ ] **Step 1.3: Run the new tests to verify they FAIL**

Kill testhost first if present, then:

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePdfIntegrationTests.DeletePdf_StillLinkedBySecondGame_PreservesRecordAndRemovesOnlyCallerLink|FullyQualifiedName~DeletePdfIntegrationTests.DeletePdf_LastRemainingLink_DeletesRecordAndVectors"`

Expected: BUILD FAILS first — `DeletePdfCommandHandler` does not yet accept `IEntityLinkRepository` (the `new DeletePdfCommandHandler(...)` 5-arg calls don't match the current 4-arg+optional-mediator ctor). This is the red state: the compiler error `CS1729`/`CS7036` (no matching constructor) IS the failing signal. If the ctor already compiled, the `DeletePdf_StillLinkedBySecondGame_...` test would FAIL its assertion `pdfExists.Should().BeTrue()` because the current handler deletes unconditionally.

- [ ] **Step 1.4: Implement the ref-count gate in `DeletePdfCommandHandler`**

First, update the constructor and add the field. Replace lines 14-34 (the field block + constructor) with:

```csharp
internal class DeletePdfCommandHandler : ICommandHandler<DeletePdfCommand, PdfDeleteResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly IEntityLinkRepository _entityLinkRepository;
    private readonly IMediator? _mediator;
    private readonly ILogger<DeletePdfCommandHandler> _logger;

    public DeletePdfCommandHandler(
        MeepleAiDbContext db,
        IBlobStorageService blobStorageService,
        IAiResponseCacheService cacheService,
        ILogger<DeletePdfCommandHandler> logger,
        IEntityLinkRepository entityLinkRepository,
        IMediator? mediator = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _entityLinkRepository = entityLinkRepository ?? throw new ArgumentNullException(nameof(entityLinkRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator;
    }
```

Add these `using` directives to the top of the handler file (after the existing `using` block):

```csharp
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
```

Now insert the ref-count gate into `Handle`. In the `try` block, after loading `pdfDoc` and the null-check (after the current line 52 `var gameId = pdfDoc.SharedGameId;` and line 53 `var coverR2Key = pdfDoc.CoverR2Key;`), insert the gate BEFORE the vector-delete call (current line 55-56). Replace the current block from line 52 through line 60 (the `var gameId ...` down to and including the first `await _db.SaveChangesAsync(...)` for the record removal) — actually keep the full-delete block intact and wrap it. Concretely, insert this immediately after `var coverR2Key = pdfDoc.CoverR2Key;`:

```csharp
            var storageGameIdForUnlink = (pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId)?.ToString() ?? string.Empty;

            // Issue #2949 (DEC-6): ref-counting delete. A PDF may be linked from
            // multiple games (Task 2 of #2943 introduced dedup reuse via EntityLink).
            // Deleting the record/blob/vectors while another game still links it would
            // orphan those games' KB cards. Gate the destructive path on the last link.
            var linkCount = await _entityLinkRepository
                .GetCountForEntityAsync(MeepleEntityType.KbCard, pdfGuid, cancellationToken)
                .ConfigureAwait(false);

            if (linkCount > 1)
            {
                await RemoveCallerLinkAsync(pdfGuid, pdfDoc.SharedGameId, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "PDF {PdfId} still referenced by {RemainingLinks} game link(s) after unlink — record/blob/vectors preserved",
                    pdfId, linkCount - 1);

                return new PdfDeleteResult(true, "PDF unlinked from game (still referenced by other games)", storageGameIdForUnlink);
            }
```

Then add the private helper method (place it after `DeleteVectorDocumentAsync`, before `DeletePhysicalFileAsync`):

```csharp
    /// <summary>
    /// Removes only the calling game's Game→KbCard link (the last-link case is
    /// handled by the destructive path in <see cref="Handle"/>). Used when other
    /// games still reference this PDF, so the record must survive.
    /// </summary>
    private async Task RemoveCallerLinkAsync(Guid pdfGuid, Guid? callerGameId, CancellationToken cancellationToken)
    {
        if (callerGameId is null || callerGameId.Value == Guid.Empty)
        {
            // No owning-game link to remove for this call; the record survives
            // because other links exist (linkCount > 1 already established).
            return;
        }

        var links = await _entityLinkRepository
            .GetForEntityAsync(
                MeepleEntityType.KbCard,
                pdfGuid,
                linkType: EntityLinkType.RelatedTo,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var callerLink = links.FirstOrDefault(l =>
            l.SourceEntityType == MeepleEntityType.Game &&
            l.SourceEntityId == callerGameId.Value &&
            l.TargetEntityType == MeepleEntityType.KbCard &&
            l.TargetEntityId == pdfGuid);

        if (callerLink is not null)
        {
            _entityLinkRepository.Remove(callerLink);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
```

> Leave the existing full-delete path (lines 55-85: vector delete → record Remove → SaveChanges → `PdfDeletedDomainEvent` publish → blob delete → cache invalidate → `return new PdfDeleteResult(true, "PDF deleted successfully", storageGameId)`) UNCHANGED. It now runs only when `linkCount <= 1`.

- [ ] **Step 1.5: Run the new tests to verify they PASS**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePdfIntegrationTests.DeletePdf_StillLinkedBySecondGame_PreservesRecordAndRemovesOnlyCallerLink|FullyQualifiedName~DeletePdfIntegrationTests.DeletePdf_LastRemainingLink_DeletesRecordAndVectors"`

Expected: PASS (2 passed).

- [ ] **Step 1.6: Run the full `DeletePdfIntegrationTests` class to confirm no regression on the 9 existing tests**

The 9 pre-existing `[Fact]` tests (at lines 216/238/257/272/296/320/363/392/423) seed PDFs WITHOUT any EntityLink, so `GetCountForEntityAsync` returns 0 → `linkCount <= 1` → full-delete path runs exactly as before. Verify:

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePdfIntegrationTests"`

Expected: PASS (11 passed — 9 original + 2 new).

- [ ] **Step 1.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeletePdfCommandHandler.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeletePdfIntegrationTests.cs
git commit -m "fix(pdf): ref-counting delete gates record/blob removal on last EntityLink (#2949)

DeletePdfCommandHandler now consults IEntityLinkRepository.GetCountForEntityAsync
before destroying the record/blob/vectors. When the PDF is still linked by another
game it removes only the caller's Game->KbCard link, preventing orphaning of KB
cards introduced by #2943's dedup-reuse.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Centralize dedup in `AddRulebookCommandHandler`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/AddRulebookCommandHandler.cs:33-110` (add `IPdfDeduplicationService` dependency; replace inline hash+lookup with `EvaluateAsync`)
- Test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/AddRulebookDedupIntegrationTests.cs` (new)

**Interfaces:**
- Consumes:
  - `IPdfDeduplicationService.ComputeContentHashAsync(Stream content, CancellationToken ct) : Task<string>` — SHA-256 lowercase hex.
  - `IPdfDeduplicationService.EvaluateAsync(string contentHash, Guid? sharedGameId, Guid? privateGameId, Guid userId, CancellationToken ct) : Task<PdfDedupResult>`.
  - `PdfDedupResult(PdfDedupDecision Decision, Guid? ExistingPdfDocumentId, string ContentHash)`; `PdfDedupDecision.NewUpload | ReuseExisting`.
  - Existing repo call for reuse-message state: `IPdfDocumentRepository.FindByContentHashAsync` is REMOVED from `Handle`'s hot path, but the handler still needs the existing doc's `ProcessingState` for the reuse message. Resolve via `_pdfDocumentRepository.GetByIdAsync(existingId, ct) : Task<PdfDocument?>`. This method is NOT declared on `IPdfDocumentRepository` directly — it is INHERITED from the base `IRepository<PdfDocument, Guid>` (`SharedKernel/Infrastructure/Persistence/IRepository.cs:18`, signature `Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)`). `MaterializePdfCoverCommandHandler` already calls it the same way, so it is safe to use — do not add it to the interface.
- Produces:
  - `AddRulebookCommandHandler(IPdfDocumentRepository, MeepleAiDbContext, IMediator, IBlobStorageService, ITierEnforcementService, IBackgroundTaskService, IPdfUploadQuotaService, ILogger<AddRulebookCommandHandler>, IPdfDeduplicationService, TimeProvider? = null)` — NEW ctor adds `IPdfDeduplicationService pdfDeduplicationService` as the 9th positional param, before the optional `TimeProvider`.

### Behavior spec (locked)

Rulebook uploads are ALWAYS catalog uploads (`gameId` = `SharedGameId`, never a private game). So the `EvaluateAsync` call passes `sharedGameId: gameId, privateGameId: null, userId: userId` — matching the catalog-global dedup rule the service encodes.

The migration preserves the two existing observable behaviors:
1. **Match found & non-Failed** → reuse via `CreateKbCardEntityLinkSafelyAsync` + return `IsNew: false` with the state-derived status/message.
2. **No match, OR match is Failed** → full upload. (For the Failed case, `EvaluateAsync` returns `NewUpload` — the service treats `Failed` as not reusable. The existing `CleanupStaleEntityLinksAsync` call for the Failed-match case is preserved by looking up whether a Failed match existed; see step 2.4.)

Confirm `IPdfDocumentRepository.GetByIdAsync` exists before writing the impl:
Run: `cd apps/api && grep -n "GetByIdAsync" src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IPdfDocumentRepository.cs` (informational — do not gate a step on it; if absent, keep the single `FindByContentHashAsync` call solely to fetch the state, see the impl note in step 2.4).

- [ ] **Step 2.1: Write the failing integration test**

Create `apps/api/tests/Api.Tests/Integration/DocumentProcessing/AddRulebookDedupIntegrationTests.cs`:

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Issue #2949 Task 2: proves AddRulebookCommandHandler, after migration to
/// IPdfDeduplicationService, still (a) reuses an existing Ready PDF via EntityLink
/// without creating a new record, and (b) performs a full upload when no hash matches.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Issue", "2949")]
[Trait("Category", TestCategories.Integration)]
public sealed class AddRulebookDedupIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AddRulebookDedupIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_addrulebookdedup_{Guid.NewGuid():N}";
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(conn);
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IPdfDeduplicationService, PdfDeduplicationService>();
        // The reuse branch calls CreateKbCardEntityLinkSafelyAsync → _mediator.Send(CreateEntityLinkCommand)
        // → CreateEntityLinkCommandHandler, which REQUIRES IEntityLinkRepository
        // (CreateEntityLinkCommandHandler.cs:18,22-27). Without this registration the handler's
        // broad catch(Exception) (AddRulebookCommandHandler.cs:339) swallows the DI failure, the
        // Game→KbCard link is never created, and the reuse test's linkExists assertion fails.
        // EntityLinkRepository's ctor (MeepleAiDbContext, IDomainEventCollector) resolves from
        // CreateBase's shared-kernel registrations; IUnitOfWork (also needed by the handler) is
        // likewise part of CreateBase. If BuildServiceProvider throws for IDomainEventCollector or
        // IUnitOfWork, register them explicitly — but attempt without first.
        services.AddScoped<IEntityLinkRepository, EntityLinkRepository>();
        services.AddScoped<AddRulebookCommandHandler>();

        // Blob storage returns a deterministic GUID FileId so the record Id is parseable.
        // BlobStorageResult is a POSITIONAL record (Success, FileId, FilePath, FileSizeBytes,
        // ErrorMessage = null) — Api/Services/Pdf/IBlobStorageService.cs:164-169 — so it MUST
        // be constructed positionally; an object-initializer fails to compile (CS7036).
        var blobMock = new Mock<IBlobStorageService>();
        blobMock.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobStorageResult(true, Guid.NewGuid().ToString(), "/test/rulebook.pdf", 1024));
        services.AddSingleton<IBlobStorageService>(blobMock.Object);

        // The minimal DI container above does NOT register IBackgroundTaskService or
        // IPdfUploadQuotaService, both NON-optional ctor params of AddRulebookCommandHandler
        // (AddRulebookCommandHandler.cs:43-52). Without them GetRequiredService<AddRulebookCommandHandler>()
        // throws at construction for BOTH tests. Register happy-path mocks.
        var backgroundTaskMock = new Mock<IBackgroundTaskService>();
        services.AddSingleton<IBackgroundTaskService>(backgroundTaskMock.Object);

        var quotaMock = new Mock<IPdfUploadQuotaService>();
        quotaMock.Setup(q => q.ReserveQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaReservationResult.Success(DateTime.UtcNow.AddHours(1)));
        services.AddSingleton<IPdfUploadQuotaService>(quotaMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await EnsureCreatedWithRetry(_dbContext);
        await SeedBaseDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable ad) await ad.DisposeAsync();
        else (_serviceProvider as IDisposable)?.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); } catch { /* ignore */ }
        }
    }

    private Guid _gameId;
    private Guid _userId;

    private async Task SeedBaseDataAsync()
    {
        _userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = _userId, Email = "rb@meepleai.dev", DisplayName = "RB", Role = "Editor",
            Tier = "Free", CreatedAt = DateTime.UtcNow
        });

        _gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = _gameId, Title = "Dedup Game", YearPublished = 2024, MinPlayers = 2,
            MaxPlayers = 4, PlayingTimeMinutes = 60, CreatedAt = DateTime.UtcNow
        });

        // User owns the game (AddRulebookCommandHandler enforces ownership).
        // NOTE: UserLibraryEntryEntity has NO CreatedAt property — its timestamp column is
        // AddedAt (defaults to DateTime.UtcNow), so we omit any timestamp initializer here.
        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(), UserId = _userId, SharedGameId = _gameId
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static IFormFile MakePdfFile(byte[] content, string name = "rulebook.pdf")
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(name);
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.ContentType).Returns("application/pdf");
        mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return mock.Object;
    }

    // Minimal valid-looking PDF (>=50 bytes, %PDF- header).
    private static byte[] PdfBytes(string tag)
    {
        var body = "%PDF-1.4\n" + tag + new string('X', 64) + "\n%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(body);
    }

    [Fact]
    public async Task Handle_DuplicateContentHash_ReusesExistingViaEntityLink_NoNewRecord()
    {
        // Arrange: a Ready PDF already exists for the game with a known content hash.
        var content = PdfBytes("dup");
        var dedup = _serviceProvider!.GetRequiredService<IPdfDeduplicationService>();
        string hash;
        using (var s = new MemoryStream(content))
            hash = await dedup.ComputeContentHashAsync(s, TestCancellationToken);

        var existingId = Guid.NewGuid();
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = existingId, SharedGameId = _gameId, UploadedByUserId = _userId,
            FileName = "existing.pdf", FilePath = "/test/existing.pdf", FileSizeBytes = content.Length,
            UploadedAt = DateTime.UtcNow, ContentHash = hash,
            ProcessingState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready)
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var handler = _serviceProvider.GetRequiredService<AddRulebookCommandHandler>();
        var command = new AddRulebookCommand(_gameId, _userId, MakePdfFile(content));

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: reused, not new; no second PDF record created.
        result.IsNew.Should().BeFalse();
        result.PdfDocumentId.Should().Be(existingId);

        var pdfCount = await _dbContext.PdfDocuments.CountAsync(p => p.SharedGameId == _gameId, TestCancellationToken);
        pdfCount.Should().Be(1, "duplicate content must reuse the existing record");

        var linkExists = await _dbContext.EntityLinks.AnyAsync(
            el => el.SourceEntityId == _gameId && el.TargetEntityId == existingId
                  && el.TargetEntityType == MeepleEntityType.KbCard,
            TestCancellationToken);
        linkExists.Should().BeTrue("reuse must create the Game->KbCard EntityLink");
    }

    [Fact]
    public async Task Handle_NoHashMatch_PerformsFullUpload_CreatesNewRecord()
    {
        // Arrange: no existing PDF with this hash.
        var handler = _serviceProvider!.GetRequiredService<AddRulebookCommandHandler>();
        var command = new AddRulebookCommand(_gameId, _userId, MakePdfFile(PdfBytes("fresh")));

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.IsNew.Should().BeTrue();
        var pdfCount = await _dbContext!.PdfDocuments.CountAsync(p => p.SharedGameId == _gameId, TestCancellationToken);
        pdfCount.Should().Be(1, "a fresh upload must create exactly one new record");
    }

    private static async Task EnsureCreatedWithRetry(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try { await context.Database.MigrateAsync(TestCancellationToken); return; }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }
}
```

> **Type facts already accounted for above (do not re-derive):**
> - `BlobStorageResult` is a positional record `(bool Success, string? FileId, string? FilePath, long FileSizeBytes, string? ErrorMessage = null)` — the mock uses positional construction `new BlobStorageResult(true, Guid.NewGuid().ToString(), "/test/rulebook.pdf", 1024)`.
> - `UserLibraryEntryEntity` (namespace `Api.Infrastructure.Entities.UserLibrary`) has NO `CreatedAt`; its timestamp column is `AddedAt` (defaulted), so the seed omits it.
> - `IBackgroundTaskService` (namespace `Api.Services`) and `IPdfUploadQuotaService` (namespace `Api.BoundedContexts.DocumentProcessing.Domain.Services`) are NON-optional ctor params of `AddRulebookCommandHandler` and are registered as happy-path mocks in `InitializeAsync`. `ReserveQuotaAsync` returns `QuotaReservationResult.Success(DateTime)` (`Reserved = true`).
> - `IEntityLinkRepository → EntityLinkRepository` is registered so the reuse branch's `CreateEntityLinkCommandHandler` can resolve.
>
> If `ITierEnforcementService` is not registered by `CreateBase` and the handler resolution throws, register a lightweight mock (`CanPerformAsync`→true, `GetLimitsAsync`→`MaxPdfSizeBytes = long.MaxValue`) alongside the other mocks in `InitializeAsync` — attempt without it first.

- [ ] **Step 2.2: Run the new tests to verify they FAIL**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AddRulebookDedupIntegrationTests"`

Expected: BUILD FAILS — `services.AddScoped<AddRulebookCommandHandler>()` resolution requires `IPdfDeduplicationService` in the ctor, which the handler does not yet accept, so `AddRulebookCommandHandler` cannot be constructed with the registered service graph. The red signal is the DI/compile mismatch. (If it compiles because the ctor is unchanged, the dedup-reuse test still passes accidentally via the current inline path — in that case treat step 2.4 as the behavior-preserving refactor and rely on step 2.5's green run + the no-regression run for the safety net.)

- [ ] **Step 2.3: Add the `IPdfDeduplicationService` dependency to `AddRulebookCommandHandler`**

Add the field + ctor param. Change the field block (lines 33-41) to include:

```csharp
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly IPdfDeduplicationService _pdfDeduplicationService;
    private readonly ILogger<AddRulebookCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
```

Change the constructor (lines 43-63) to accept and assign it (insert `IPdfDeduplicationService pdfDeduplicationService` after `IPdfUploadQuotaService quotaService` and before `ILogger<...> logger`):

```csharp
    public AddRulebookCommandHandler(
        IPdfDocumentRepository pdfDocumentRepository,
        MeepleAiDbContext db,
        IMediator mediator,
        IBlobStorageService blobStorageService,
        ITierEnforcementService tierEnforcementService,
        IBackgroundTaskService backgroundTaskService,
        IPdfUploadQuotaService quotaService,
        IPdfDeduplicationService pdfDeduplicationService,
        ILogger<AddRulebookCommandHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _pdfDeduplicationService = pdfDeduplicationService ?? throw new ArgumentNullException(nameof(pdfDeduplicationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
```

Add the using directive at the top (it is likely already imported transitively, but make it explicit):

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
```

- [ ] **Step 2.4: Replace the inline dedup with `EvaluateAsync` in `Handle`**

Replace the `Handle` body from the `// Step 1: Compute SHA-256 hash` block through the end of the method (current lines 93-110) with the centralized version:

```csharp
        // Step 1: Evaluate dedup via the centralized service (Issue #2949 Task 2).
        // Rulebook uploads are catalog uploads: sharedGameId = gameId, privateGameId = null.
        string contentHash;
        using (var hashStream = file.OpenReadStream())
        {
            contentHash = await _pdfDeduplicationService
                .ComputeContentHashAsync(hashStream, cancellationToken)
                .ConfigureAwait(false);
        }

        var dedup = await _pdfDeduplicationService
            .EvaluateAsync(contentHash, sharedGameId: gameId, privateGameId: null, userId: userId, cancellationToken)
            .ConfigureAwait(false);

        if (dedup.Decision == PdfDedupDecision.ReuseExisting)
        {
            var existingDoc = await _pdfDocumentRepository
                .GetByIdAsync(dedup.ExistingPdfDocumentId!.Value, cancellationToken)
                .ConfigureAwait(false);

            if (existingDoc is not null)
            {
                return await HandleExistingDocumentAsync(existingDoc, gameId, userId, file, contentHash, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Step 2: NewUpload (no match, or match was Failed) — full upload flow.
        // A Failed match is reported as NewUpload by the dedup service; clean up any
        // stale Game→failed-PDF links first (mirrors the pre-migration behavior).
        var staleMatch = await _pdfDocumentRepository
            .FindByContentHashAsync(contentHash, cancellationToken)
            .ConfigureAwait(false);

        if (staleMatch is not null && staleMatch.ProcessingState == PdfProcessingState.Failed)
        {
            _logger.LogInformation(
                "Found existing PDF {PdfId} with matching hash but Failed state — treating as new upload",
                staleMatch.Id);
            await CleanupStaleEntityLinksAsync(staleMatch.Id, gameId, cancellationToken).ConfigureAwait(false);
        }

        return await HandleNewUploadAsync(gameId, userId, file, contentHash, cancellationToken)
            .ConfigureAwait(false);
```

Then DELETE the now-dead private methods that only served the old inline path:
- `HandleExistingDocumentAsync` — KEEP (still used by the reuse branch above), but REMOVE its internal `if (state == PdfProcessingState.Failed) { ... }` block (lines 127-138) because the Failed case is now handled before `HandleNewUploadAsync` is called and never reaches `HandleExistingDocumentAsync` (the service returns `NewUpload` for Failed). Replace the method body's leading lines so it only handles the reuse (Ready/in-progress) path:

```csharp
    private async Task<RulebookUploadResult> HandleExistingDocumentAsync(
        Domain.Entities.PdfDocument existingDoc,
        Guid gameId,
        Guid userId,
        IFormFile file,
        string contentHash,
        CancellationToken cancellationToken)
    {
        var state = existingDoc.ProcessingState;

        // Ready or in-progress: reuse by creating EntityLink.
        await CreateKbCardEntityLinkSafelyAsync(existingDoc.Id, gameId, userId, cancellationToken)
            .ConfigureAwait(false);

        var status = RulebookUploadResult.MapStatus(state);
        var message = state == PdfProcessingState.Ready
            ? "Regolamento già disponibile — collegato al tuo gioco!"
            : "Regolamento in elaborazione — sarà disponibile a breve.";

        _logger.LogInformation(
            "Reused existing PDF {PdfId} (state={State}) for game {GameId} by user {UserId}",
            existingDoc.Id, state, gameId, userId);

        return new RulebookUploadResult(
            PdfDocumentId: existingDoc.Id,
            IsNew: false,
            Status: status,
            Message: message);
    }
```

- REMOVE the now-unused `file` parameter warning risk: `HandleExistingDocumentAsync` still declares `file` and `contentHash` but no longer uses them. To avoid an analyzer "unused parameter" complaint, drop `file` and `contentHash` from BOTH the signature and the call site (the reuse branch call becomes `HandleExistingDocumentAsync(existingDoc, gameId, userId, cancellationToken)`). Final signature:

```csharp
    private async Task<RulebookUploadResult> HandleExistingDocumentAsync(
        Domain.Entities.PdfDocument existingDoc,
        Guid gameId,
        Guid userId,
        CancellationToken cancellationToken)
```

And the call in `Handle` becomes:

```csharp
                return await HandleExistingDocumentAsync(existingDoc, gameId, userId, cancellationToken)
                    .ConfigureAwait(false);
```

- DELETE the private `ComputeContentHashAsync(IFormFile, CancellationToken)` method (current lines 383-388) — its responsibility now lives in `IPdfDeduplicationService.ComputeContentHashAsync`. (`ValidatePdfStructureAsync`, `SanitizeFileName`, `CleanupStaleEntityLinksAsync`, `CreateKbCardEntityLinkSafelyAsync`, `ProcessPdfInBackgroundAsync`, `EnqueueForProcessingSafelyAsync` all remain.)

> **`GetByIdAsync` is inherited** from the base `IRepository<PdfDocument, Guid>` (it is NOT declared on `IPdfDocumentRepository` directly) — `MaterializePdfCoverCommandHandler` already calls it, so the reuse-branch `_pdfDocumentRepository.GetByIdAsync(dedup.ExistingPdfDocumentId!.Value, cancellationToken)` compiles as-is. Do not add it to the interface. Prefer it over a second `FindByContentHashAsync` (it matches the id the dedup service already resolved and avoids a redundant hash scan).

- [ ] **Step 2.5: Run the new tests to verify they PASS**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AddRulebookDedupIntegrationTests"`

Expected: PASS (2 passed).

- [ ] **Step 2.6: Confirm no regression across DocumentProcessing PDF-upload tests + the full Application command suite for the handler**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AddRulebook|FullyQualifiedName~PrivatePdfUpload|FullyQualifiedName~CompleteChunkedUpload"`

Expected: PASS (all green — the `AddRulebookCommandValidator` unit tests are untouched; `CompleteChunkedUpload` behavior is unchanged; `PrivatePdfUpload` is unaffected).

- [ ] **Step 2.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/AddRulebookCommandHandler.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/AddRulebookDedupIntegrationTests.cs
git commit -m "refactor(pdf): migrate AddRulebookCommandHandler to IPdfDeduplicationService (#2949)

Replaces the inline SHA-256 + FindByContentHashAsync + reuse-via-EntityLink block
with the centralized IPdfDeduplicationService.EvaluateAsync, matching the pattern
already used by CompleteChunkedUploadCommandHandler. Ready/in-progress matches reuse
via EntityLink; Failed matches fall through to a full upload with stale-link cleanup.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: `MaterializePdfCoverCommandValidator`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidatorTests.cs`

**Interfaces:**
- Consumes:
  - `MaterializePdfCoverCommand(Guid PdfDocumentId, int PageNumber, string DbKey)` — existing `internal sealed record` in `Api.BoundedContexts.DocumentProcessing.Application.Commands`.
  - `FluentValidation.AbstractValidator<T>`, `FluentValidation.TestHelper` (for `TestValidate`).
- Produces:
  - `internal sealed class MaterializePdfCoverCommandValidator : AbstractValidator<MaterializePdfCoverCommand>` — auto-registered by `AddValidatorsFromAssemblyContaining<BulkDeletePdfsCommandValidator>(includeInternalTypes: true)` (`ApplicationServiceExtensions.cs:289`) because it lives in the same assembly and is `internal`.

### Behavior spec (locked)

`RuleFor(x => x.PageNumber).GreaterThan(0)`. This is defense-in-depth: `MaterializePdfCoverCommand.PageNumber` is a 1-based page number that the handler converts to a 0-based index (`command.PageNumber - 1`), so a value ≤ 0 is semantically invalid and this validator rejects it with a clean 400. The only current caller (`ProposeCoverChangeCommandHandler`) already validates `PageNumber > 0` via `ProposeCoverChangeCommandValidator`, so in practice an invalid value cannot reach the handler today; the validator guards a future direct `IMediator.Send(new MaterializePdfCoverCommand(..., PageNumber: 0, ...))`.

> **Note (do NOT overstate the downstream failure):** for `PageNumber = 0`, the handler's page-image render step guards first — `GetPdfPageImageQueryHandler` throws `ArgumentOutOfRangeException` on `query.PageNumber < 1` (`GetPdfPageImageQueryHandler.cs:37`) BEFORE `pdf.MarkCoverGenerated(dbKey, command.PageNumber - 1)` is reached, so a 0 does not specifically hit the `MarkCoverGenerated` negative-index path. Either way the failure surfaces as a 500; the validator turns it into a clean 400 at the boundary. Treat the validator as a correctness/contract guard (1-based invariant), not as a fix for one specific downstream throw site.

`PdfDocumentId` and `DbKey` are intentionally NOT validated here — the handler resolves `PdfDocumentId` and throws a proper `NotFoundException` (404) if missing, and `DbKey` is always constructed internally by the caller. Keeping the validator minimal mirrors the "build only what's asked" scope.

- [ ] **Step 3.1: Write the failing unit tests**

Create `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidatorTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Unit tests for MaterializePdfCoverCommandValidator (Issue #2949 Task 3).
/// Defense-in-depth: PageNumber must be 1-based (> 0) so the handler's
/// PageNumber - 1 conversion never produces a negative index.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class MaterializePdfCoverCommandValidatorTests
{
    private readonly MaterializePdfCoverCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_PageNumber_Is_Positive()
    {
        var command = new MaterializePdfCoverCommand(Guid.NewGuid(), 1, "covers/x/pdf-cover-abc");

        var result = _validator.TestValidate(command);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Should_Fail_When_PageNumber_Is_Zero()
    {
        var command = new MaterializePdfCoverCommand(Guid.NewGuid(), 0, "covers/x/pdf-cover-abc");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }

    [Fact]
    public void Should_Fail_When_PageNumber_Is_Negative()
    {
        var command = new MaterializePdfCoverCommand(Guid.NewGuid(), -1, "covers/x/pdf-cover-abc");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PageNumber);
    }
}
```

- [ ] **Step 3.2: Run the tests to verify they FAIL**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MaterializePdfCoverCommandValidatorTests"`

Expected: BUILD FAILS — `MaterializePdfCoverCommandValidator` does not exist yet (`CS0246: type or namespace 'MaterializePdfCoverCommandValidator' could not be found`). That compile error is the red state.

- [ ] **Step 3.3: Create the validator**

Create `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidator.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for <see cref="MaterializePdfCoverCommand"/> (Issue #2949 Task 3).
/// Defense-in-depth: <c>PageNumber</c> is 1-based (render/query contract). The
/// handler converts it to a 0-based index via <c>PageNumber - 1</c>, so a value of
/// 0 or below is semantically invalid and would surface downstream as a 500. This
/// validator enforces the 1-based invariant at the boundary, yielding a clean 400
/// for any future direct sender.
/// </summary>
internal sealed class MaterializePdfCoverCommandValidator : AbstractValidator<MaterializePdfCoverCommand>
{
    public MaterializePdfCoverCommandValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
    }
}
```

- [ ] **Step 3.4: Run the tests to verify they PASS**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MaterializePdfCoverCommandValidatorTests"`

Expected: PASS (3 passed).

- [ ] **Step 3.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidator.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Validators/MaterializePdfCoverCommandValidatorTests.cs
git commit -m "feat(pdf): add MaterializePdfCoverCommandValidator (PageNumber > 0) (#2949)

Defense-in-depth validator auto-registered via the DocumentProcessing assembly
scan; a future direct IMediator.Send with PageNumber <= 0 now yields a clean 400
instead of a 500 from the handler's PageNumber - 1 index conversion.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Final Verification (before opening the PR)

- [ ] **Build the whole API project** to confirm no analyzer (MA0025, S1135) or compile errors:

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Run the combined DocumentProcessing test surface touched by this PR:**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DeletePdfIntegrationTests|FullyQualifiedName~AddRulebookDedupIntegrationTests|FullyQualifiedName~AddRulebook|FullyQualifiedName~MaterializePdfCover"`
Expected: all PASS (11 DeletePdf + 2 AddRulebookDedup + AddRulebook validator/handler + 3 MaterializePdfCover validator + existing MaterializePdfCover handler).

- [ ] **Push and open the PR to `main-dev`:**

```bash
git push -u origin feature/issue-2949-pdf-refcount-dedup-validator
gh pr create --base main-dev --title "fix(pdf): ref-counting delete + dedup centralization + cover validator (#2949)" --body "$(cat <<'EOF'
Closes #2949.

Three backend follow-ups from #2943 (cover-da-PDF), grouped in one PR:

1. **Ref-counting delete (DEC-6)** — `DeletePdfCommandHandler` now gates record/blob/vector/event removal on `IEntityLinkRepository.GetCountForEntityAsync(KbCard, pdfId) <= 1`. When another game still links the PDF, only the caller's `Game→KbCard` link is removed. Fixes the data-loss window opened by #2943's dedup-reuse.
2. **Dedup centralization** — `AddRulebookCommandHandler` migrated from its inline SHA-256 + `FindByContentHashAsync` + reuse-via-EntityLink block to `IPdfDeduplicationService.EvaluateAsync`, matching `CompleteChunkedUploadCommandHandler`.
3. **Validator** — new `MaterializePdfCoverCommandValidator` (`PageNumber > 0`), auto-registered via the DocumentProcessing assembly scan; defense-in-depth against a future direct send producing a negative index.

Tests: 2 new DeletePdf integration tests (multi-link preserved / last-link deleted), 2 new AddRulebook dedup integration tests (reuse / fresh upload), 3 new validator unit tests. No regression on the 9 existing DeletePdf tests.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review (performed against the issue body)

**1. Scope coverage** — all three issue tasks are covered:
- Issue §1 (ref-counting delete DEC-6) → Task 1. ✅
- Issue §2 (centralize dedup in `AddRulebookCommandHandler`) → Task 2 (chose the "migrate" option, not the "narrow the doc" fallback). ✅
- Issue §3 (missing `MaterializePdfCoverCommandValidator`) → Task 3. ✅

**2. Placeholder scan** — no `TBD`/`TODO`/`similar to Task N`/"add validation"/"handle edge cases". Every code step shows complete, real code. The two "verify before running" notes in Tasks 1.2 and 2.1 are conditional DI fallbacks with concrete instructions, not deferred work. No `// TODO(` comments introduced (S1135-safe); the one forward-reference in the validator XML doc uses prose, not a `// TODO`. No `NotImplementedException` stubs (MA0025-safe) — every red state is a compile error or a real assertion failure. ✅

**3. Type consistency** —
- `DeletePdfCommandHandler` new ctor `(db, blob, cache, logger, IEntityLinkRepository, IMediator? = null)` used identically in the impl (Step 1.4) and all 5 test call sites (Step 1.2). ✅
- `GetCountForEntityAsync(MeepleEntityType.KbCard, pdfGuid, ct)` and `GetForEntityAsync(KbCard, pdfGuid, linkType: RelatedTo, ...)` match `IEntityLinkRepository` exactly. ✅
- `EntityLink.Create(sourceEntityType, sourceEntityId, targetEntityType, targetEntityId, linkType, scope, ownerUserId)` matches the factory signature. ✅
- `IPdfDeduplicationService.EvaluateAsync(contentHash, sharedGameId, privateGameId, userId, ct)` and `ComputeContentHashAsync(Stream, ct)` match the interface. ✅
- `AddRulebookCommandHandler` new ctor inserts `IPdfDeduplicationService` as param 9 (before optional `TimeProvider`), assigned consistently. `HandleExistingDocumentAsync` signature reduced to `(existingDoc, gameId, userId, ct)` and its single call site updated to match. ✅
- `MaterializePdfCoverCommand(Guid, int, string)` positional args used correctly in every test (`new MaterializePdfCoverCommand(Guid.NewGuid(), 1, "...")`). ✅
- `PdfDeleteResult(bool, string, string?)` and `RulebookUploadResult(Guid, bool, string, string)` used with correct positional/named args. ✅
- `BlobStorageResult(bool Success, string? FileId, string? FilePath, long FileSizeBytes, string? ErrorMessage = null)` is a POSITIONAL record — the Task 2 mock constructs it positionally (`new BlobStorageResult(true, Guid.NewGuid().ToString(), "/test/rulebook.pdf", 1024)`), not via object-initializer (which fails CS7036). ✅
- `UserLibraryEntryEntity` (namespace `Api.Infrastructure.Entities.UserLibrary`) has no `CreatedAt`; the seed omits any timestamp (column `AddedAt` is defaulted). The `using Api.Infrastructure.Entities.UserLibrary;` is present. ✅
- The Task 2 test's minimal container registers `IBackgroundTaskService` (mock), `IPdfUploadQuotaService` (mock, `ReserveQuotaAsync → QuotaReservationResult.Success(...)`), and `IEntityLinkRepository → EntityLinkRepository` — the three services the handler graph needs beyond `CreateBase` (the first two are non-optional handler ctor params; the third backs the reuse branch's `CreateEntityLinkCommandHandler`). ✅

No inconsistencies found.

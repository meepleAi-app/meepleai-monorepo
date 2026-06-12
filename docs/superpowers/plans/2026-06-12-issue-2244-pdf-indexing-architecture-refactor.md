# PDF Indexing Architecture Refactor (#2244) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the domain-event-bypass-via-EF-entity anti-pattern duplicated across 3 PDF ingestion call sites by introducing a `VectorDocument.Create()` factory + an `IPdfIndexingPipeline` service that centralizes domain construction, persistence and event raising. Remove the tactical manual `_mediator.Publish(VectorDocumentIndexedEvent)` shipped in #2263 (Sub #1) once the structural flow is in place.

**Architecture:** `VectorDocument` becomes a strict aggregate root: ctor private, public static `Create(...)` factory raises `VectorDocumentIndexedEvent`. `KnowledgeBaseMappers.ToDomain()` switches to a new internal `Rehydrate(...)` constructor that does NOT raise events (read-side bug fix). A new `IPdfIndexingPipeline.ExecuteAsync(...)` service lives in `DocumentProcessing/Application/Services/` and is the single owner of the "build VectorDocument + persist + transition PdfDocument to Ready" flow. The 3 ingestion handlers (`UploadPdfCommandHandler.Processing.cs`, `PdfProcessingPipelineService.cs`, `IndexPdfCommandHandler.cs`) call the pipeline instead of constructing `VectorDocumentEntity` and mutating `PdfDocumentEntity.ProcessingState` directly. Event flow is restored to: domain entity → `IVectorDocumentRepository.AddAsync` / `IPdfDocumentRepository.UpdateAsync` → `RepositoryBase.CollectDomainEvents` → `MeepleAiDbContext.SaveChangesAsync` (existing collector dispatcher) → `_mediator.Publish` → `VectorDocumentIndexedForKbFlagHandler` / `KbDocIndexedEventHandler`. The compensating manual publishes added in #2263 are removed.

**Tech Stack:** .NET 9 / EF Core / MediatR / xUnit / Testcontainers / FluentAssertions / Moq.

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocument.cs` | Modify | Make ctor `private`, add `public static VectorDocument Create(...)` factory raising `VectorDocumentIndexedEvent`; add `internal static VectorDocument Rehydrate(...)` for mapper reads (no event raised) |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs` | Modify | `ToDomain()` switches from public ctor to `Rehydrate()` so reading the entity does NOT enqueue a `VectorDocumentIndexedEvent` (latent read-side bug) |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfIndexingPipeline.cs` | Create | Interface: `Task<VectorDocument> ExecuteAsync(PdfDocumentEntity pdfDoc, int indexedChunkCount, Guid resolvedGameId, CancellationToken ct)` |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipeline.cs` | Create | Implementation: builds `VectorDocument.Create(...)`, persists via `IVectorDocumentRepository.AddAsync(...)` (events auto-collected). Handles "already exists" idempotent update path |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs` | Modify | DI: register both `IPdfIndexingPipeline` interface AND `PdfIndexingPipeline` implementation (CLAUDE.md #2565) |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs` | Modify | `UpdateVectorDocumentAsync` (line 569-624): replace `new VectorDocumentEntity {…}` with `_pipeline.ExecuteAsync(...)`. `FinalizeProcessingAsync` (line 792-861): replace `pdfDoc.ProcessingState = "Ready"` + manual `scopedMediator.Publish(PdfStateChangedEvent)` + manual `scopedMediator.Publish(VectorDocumentIndexedEvent)` with a single `pdfDomain.TransitionTo(Ready)` + `_pdfRepo.UpdateAsync(domain)` so events flow structurally |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` | Modify | `IndexInVectorStoreAsync` (line 738-787): replace `new VectorDocumentEntity {…}` with `_pipeline.ExecuteAsync(...)` |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs` | Modify | Path around line 258-285: replace `new VectorDocumentEntity {…}` with `_pipeline.ExecuteAsync(...)` |
| `tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocumentTests.cs` | Create | Unit tests: `Create()` raises `VectorDocumentIndexedEvent` once with correct payload; `Rehydrate()` raises ZERO events; `Create()` throws on invalid args (empty language, non-positive chunks) |
| `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipelineTests.cs` | Create | Unit tests (Moq `IVectorDocumentRepository`): `ExecuteAsync` calls `AddAsync(domain)` exactly once; returned domain carries the event; idempotent path (existing doc) updates instead of duplicating |
| `tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowKbFlagIntegrationTests.cs` | Modify (existing from #2263) | Keep existing scenarios green — they must pass WITHOUT the compensating manual publish in `FinalizeProcessingAsync`. Add coverage for the Quartz path + admin re-index path (Sub #2 expands the contract beyond UploadPdf). |
| `tests/Api.Tests/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument_SevenStateProgression*.cs` | Verify only | Already covers `KbDocIndexedEvent` on Ready transition (PR #2038). After Task 7, double-check the event count assertion still holds; rebaseline if a duplicate event is observed and document it in CLAUDE.md known-flaky table. |

---

## Pre-Flight: Branch & Discovery

- [ ] **Step 0.1: Reset to clean main-dev**

```bash
git checkout main-dev
git pull --ff-only origin main-dev
git status   # MUST show clean tree
git branch --show-current   # MUST print main-dev
```

Expected: HEAD on `main-dev`, latest commit `25f2b3905` (or newer). NOT on the `feature/issue-2248-...` branch.

> **Important:** Sub #6 PR #2266 (issue #2248) is still OPEN at the time of planning. Do NOT branch off `feature/issue-2248-pdf-indexing-quality-gates` — that branch carries unmerged work. The refactor is independent of the audit job; both can merge in either order.

- [ ] **Step 0.2: Create feature branch**

```bash
git checkout -b feature/issue-2244-pdf-indexing-architecture-refactor
git config branch.feature/issue-2244-pdf-indexing-architecture-refactor.parent main-dev
```

- [ ] **Step 0.3: Snapshot baseline test count (for Step 8 regression check)**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~KnowledgeBase|FullyQualifiedName~DocumentProcessing" --logger "console;verbosity=minimal" 2>&1 | tail -20
```

Record the passed/failed/skipped count in a scratch note. Step 8 must show the same or better numbers.

---

## Task 1: VectorDocument Factory + Rehydrate

**Files:**
- Create test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocumentTests.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocument.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs` (lines 41-60 — `ToDomain()`)

- [ ] **Step 1.1: Write the failing factory test**

```csharp
// tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocumentTests.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VectorDocumentTests
{
    [Fact]
    public void Create_WithValidArgs_RaisesVectorDocumentIndexedEventOnce()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        // Act
        var doc = VectorDocument.Create(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfId,
            language: "en",
            totalChunks: 42,
            sharedGameId: sharedGameId);

        // Assert
        doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Should().HaveCount(1);
        var evt = doc.DomainEvents.OfType<VectorDocumentIndexedEvent>().Single();
        evt.DocumentId.Should().Be(id);
        evt.GameId.Should().Be(gameId);
        evt.ChunkCount.Should().Be(42);
        evt.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public void Create_NormalizesLanguageToLowerInvariant()
    {
        var doc = VectorDocument.Create(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "EN-US",
            totalChunks: 1);

        doc.Language.Should().Be("en-us");
    }

    [Fact]
    public void Create_WithEmptyLanguage_Throws()
    {
        var act = () => VectorDocument.Create(
            id: Guid.NewGuid(), gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(), language: "",
            totalChunks: 1);

        act.Should().Throw<ArgumentException>().WithMessage("*language*");
    }

    [Fact]
    public void Create_WithZeroChunks_Throws()
    {
        var act = () => VectorDocument.Create(
            id: Guid.NewGuid(), gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(), language: "en",
            totalChunks: 0);

        act.Should().Throw<ArgumentException>().WithMessage("*chunks*");
    }

    [Fact]
    public void Rehydrate_DoesNotRaiseDomainEvents()
    {
        var doc = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            pdfDocumentId: Guid.NewGuid(),
            language: "en",
            totalChunks: 5,
            indexedAt: DateTime.UtcNow,
            sharedGameId: null);

        doc.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void PublicConstructor_IsNotAccessible()
    {
        // Sub #2 guard: prevent regression to old anti-pattern (3 ingestion paths used `new VectorDocument(...)`)
        var ctor = typeof(VectorDocument)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        ctor.Should().BeEmpty("VectorDocument must only be constructible via Create() factory or Rehydrate() (read-side)");
    }
}
```

- [ ] **Step 1.2: Run test, verify it fails**

```bash
cd apps/api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~VectorDocumentTests" --logger "console;verbosity=normal"
```

Expected: FAIL — `Create` and `Rehydrate` do not exist; public ctor still present.

- [ ] **Step 1.3: Implement factory + rehydrate, make ctor private**

Replace the `VectorDocument` body (apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocument.cs):

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

internal sealed class VectorDocument : AggregateRoot<Guid>
{
    public Guid GameId { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public string Language { get; private set; }
    public int TotalChunks { get; private set; }
    public DateTime IndexedAt { get; private set; }
    public DateTime? LastSearchedAt { get; private set; }
    public int SearchCount { get; private set; }
    public Guid? SharedGameId { get; private set; }
    public string? Metadata { get; private set; }

#pragma warning disable CS8618
    private VectorDocument() : base()
#pragma warning restore CS8618
    {
        // EF Core only.
    }

    private VectorDocument(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        DateTime indexedAt,
        Guid? sharedGameId) : base(id)
    {
        GameId = gameId;
        PdfDocumentId = pdfDocumentId;
        Language = language;
        TotalChunks = totalChunks;
        IndexedAt = indexedAt;
        SearchCount = 0;
        SharedGameId = sharedGameId;
    }

    /// <summary>
    /// Factory: builds a NEW VectorDocument and raises <see cref="VectorDocumentIndexedEvent"/>.
    /// Use this from ingestion pipelines (Sub #2 of epic #2242).
    /// </summary>
    public static VectorDocument Create(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        Guid? sharedGameId = null)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));
        if (totalChunks <= 0)
            throw new ArgumentException("Total chunks must be positive", nameof(totalChunks));

        var doc = new VectorDocument(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: language.ToLowerInvariant(),
            totalChunks: totalChunks,
            indexedAt: DateTime.UtcNow,
            sharedGameId: sharedGameId);

        doc.AddDomainEvent(new VectorDocumentIndexedEvent(id, gameId, totalChunks, sharedGameId));
        return doc;
    }

    /// <summary>
    /// Rehydrates an EXISTING VectorDocument from persistence WITHOUT raising domain events.
    /// Used by <see cref="Infrastructure.Persistence.Mappers.KnowledgeBaseMappers.ToDomain"/>.
    /// </summary>
    internal static VectorDocument Rehydrate(
        Guid id,
        Guid gameId,
        Guid pdfDocumentId,
        string language,
        int totalChunks,
        DateTime indexedAt,
        Guid? sharedGameId)
    {
        return new VectorDocument(
            id: id,
            gameId: gameId,
            pdfDocumentId: pdfDocumentId,
            language: string.IsNullOrWhiteSpace(language) ? "en" : language.ToLowerInvariant(),
            totalChunks: totalChunks <= 0 ? 1 : totalChunks,
            indexedAt: indexedAt,
            sharedGameId: sharedGameId);
    }

    public void RecordSearch(string query)
    {
        LastSearchedAt = DateTime.UtcNow;
        SearchCount++;
        AddDomainEvent(new VectorDocumentSearchedEvent(Id, query));
    }

    public void UpdateMetadata(string metadata)
    {
        Metadata = metadata;
        AddDomainEvent(new VectorDocumentMetadataUpdatedEvent(Id, metadata));
    }

    internal void SetMetadata(string? metadata) => Metadata = metadata;
    internal void SetSharedGameId(Guid? sharedGameId) => SharedGameId = sharedGameId;
}
```

- [ ] **Step 1.4: Fix KnowledgeBaseMappers.ToDomain() to use Rehydrate**

Edit `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs` lines 41-60. Replace the body of `ToDomain(this VectorDocumentEntity entity)`:

```csharp
public static VectorDocument ToDomain(this VectorDocumentEntity entity)
{
    ArgumentNullException.ThrowIfNull(entity);
    var domain = VectorDocument.Rehydrate(
        id: entity.Id,
        gameId: entity.GameId ?? Guid.Empty,
        pdfDocumentId: entity.PdfDocumentId,
        language: "en", // not stored on entity
        totalChunks: entity.ChunkCount,
        indexedAt: entity.IndexedAt ?? DateTime.UtcNow,
        sharedGameId: entity.SharedGameId);

    if (!string.IsNullOrEmpty(entity.Metadata))
    {
        domain.SetMetadata(entity.Metadata);
    }

    return domain;
}
```

- [ ] **Step 1.5: Run tests, verify all PASS**

```bash
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~VectorDocumentTests" --logger "console;verbosity=normal"
```

Expected: 6/6 PASS.

- [ ] **Step 1.6: Smoke-build the whole solution**

```bash
cd ../../..
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
```

Expected: 0 errors. If a downstream file (e.g. tests) still calls `new VectorDocument(...)`, fix the call site (it must use `Create()` or `Rehydrate()`).

- [ ] **Step 1.7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocument.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs \
        tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Entities/VectorDocumentTests.cs
git commit -m "refactor(kb): #2244 VectorDocument.Create() factory + Rehydrate() read path"
```

---

## Task 2: IPdfIndexingPipeline service + DI

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfIndexingPipeline.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipeline.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`
- Create test: `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipelineTests.cs`

- [ ] **Step 2.1: Write the failing test**

```csharp
// tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipelineTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfIndexingPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_NewDocument_CreatesAndPersistsViaRepository()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/dev/null",
            PrivateGameId = null,
            SharedGameId = sharedGameId,
            Language = "en",
            ExtractedText = new string('x', 1000)
        };

        var repo = new Mock<IVectorDocumentRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByGameAndSourceAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocument?)null);
        VectorDocument? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<VectorDocument>(), It.IsAny<CancellationToken>()))
            .Callback<VectorDocument, CancellationToken>((d, _) => captured = d)
            .Returns(Task.CompletedTask);

        var pipeline = new PdfIndexingPipeline(repo.Object, NullLogger<PdfIndexingPipeline>.Instance);

        // Act
        var result = await pipeline.ExecuteAsync(pdfEntity, indexedChunkCount: 7, resolvedGameId: gameId, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(captured);
        result.PdfDocumentId.Should().Be(pdfId);
        result.GameId.Should().Be(gameId);
        result.SharedGameId.Should().Be(sharedGameId);
        result.TotalChunks.Should().Be(7);
        result.DomainEvents.OfType<VectorDocumentIndexedEvent>().Should().HaveCount(1);
        repo.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDocument_UpdatesIdempotently()
    {
        // Arrange — re-index path (admin re-index or retry)
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfEntity = new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/dev/null",
            SharedGameId = null,
            Language = "en"
        };

        var existing = VectorDocument.Rehydrate(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfId,
            language: "en",
            totalChunks: 3,
            indexedAt: DateTime.UtcNow.AddDays(-1),
            sharedGameId: null);

        var repo = new Mock<IVectorDocumentRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByGameAndSourceAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<VectorDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pipeline = new PdfIndexingPipeline(repo.Object, NullLogger<PdfIndexingPipeline>.Instance);

        // Act
        var result = await pipeline.ExecuteAsync(pdfEntity, indexedChunkCount: 12, resolvedGameId: gameId, CancellationToken.None);

        // Assert
        result.Id.Should().Be(existing.Id, "re-index reuses the same aggregate id (idempotent)");
        repo.VerifyAll();
    }
}
```

- [ ] **Step 2.2: Run test, verify it fails**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingPipelineTests" --logger "console;verbosity=normal"
```

Expected: FAIL — `IPdfIndexingPipeline` / `PdfIndexingPipeline` don't exist.

- [ ] **Step 2.3: Create the interface**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfIndexingPipeline.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Single owner of the VectorDocument construction + persistence + event-raising flow.
/// Replaces the duplicated `new VectorDocumentEntity {...}` anti-pattern in 3 ingestion paths
/// (#2244 / epic #2242 Sub #2). The returned aggregate already carries
/// <see cref="KnowledgeBase.Domain.Events.VectorDocumentIndexedEvent"/> via the
/// <see cref="VectorDocument.Create"/> factory; the repository collects it and the DbContext
/// SaveChanges dispatcher publishes it through MediatR.
/// </summary>
internal interface IPdfIndexingPipeline
{
    /// <summary>
    /// Persists a freshly-indexed PDF as a VectorDocument aggregate. Idempotent: if a
    /// VectorDocument already exists for (gameId, pdfDocumentId), updates it in place
    /// (re-index scenario).
    /// </summary>
    /// <param name="pdfDoc">Source PDF EF entity (3 ingestion paths each have one).</param>
    /// <param name="indexedChunkCount">Number of chunks actually persisted to pgvector.</param>
    /// <param name="resolvedGameId">Resolved <c>games.Id</c> (NOT <c>shared_games.id</c>) per <c>PdfGameIdResolver</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<VectorDocument> ExecuteAsync(
        PdfDocumentEntity pdfDoc,
        int indexedChunkCount,
        Guid resolvedGameId,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2.4: Create the implementation**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipeline.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure.Entities;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

internal sealed class PdfIndexingPipeline : IPdfIndexingPipeline
{
    private readonly IVectorDocumentRepository _repository;
    private readonly ILogger<PdfIndexingPipeline> _logger;

    public PdfIndexingPipeline(IVectorDocumentRepository repository, ILogger<PdfIndexingPipeline> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    public async Task<VectorDocument> ExecuteAsync(
        PdfDocumentEntity pdfDoc,
        int indexedChunkCount,
        Guid resolvedGameId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfDoc);
        if (indexedChunkCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(indexedChunkCount), "Must be > 0");

        var existing = await _repository
            .GetByGameAndSourceAsync(resolvedGameId, pdfDoc.Id, cancellationToken)
            .ConfigureAwait(false);

        var language = pdfDoc.Language ?? "en";

        if (existing is null)
        {
            var domain = VectorDocument.Create(
                id: Guid.NewGuid(),
                gameId: resolvedGameId,
                pdfDocumentId: pdfDoc.Id,
                language: language,
                totalChunks: indexedChunkCount,
                sharedGameId: pdfDoc.SharedGameId);

            await _repository.AddAsync(domain, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "[PdfIndexingPipeline] Created VectorDocument {VectorDocId} for Pdf {PdfId} ({ChunkCount} chunks)",
                domain.Id, pdfDoc.Id, indexedChunkCount);
            return domain;
        }

        // Idempotent re-index: domain has no Update method for totalChunks, so we rebuild
        // a fresh aggregate keeping the existing Id. UpdateMetadata covers ad-hoc payload;
        // for the chunk count we rely on the mapper writing the latest entity state.
        var refreshed = VectorDocument.Create(
            id: existing.Id,
            gameId: resolvedGameId,
            pdfDocumentId: pdfDoc.Id,
            language: language,
            totalChunks: indexedChunkCount,
            sharedGameId: pdfDoc.SharedGameId);

        await _repository.UpdateAsync(refreshed, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "[PdfIndexingPipeline] Re-indexed VectorDocument {VectorDocId} for Pdf {PdfId} ({ChunkCount} chunks)",
            refreshed.Id, pdfDoc.Id, indexedChunkCount);
        return refreshed;
    }
}
```

- [ ] **Step 2.5: Register in DI**

Edit `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`. Add to the existing `Add...Services` method (find the section that registers application services and append):

```csharp
// #2244 Sub #2: single owner of VectorDocument construction/persistence/event-raising.
// Per CLAUDE.md #2565: register both interface and impl so background tasks resolve correctly.
services.AddScoped<PdfIndexingPipeline>();
services.AddScoped<IPdfIndexingPipeline>(sp => sp.GetRequiredService<PdfIndexingPipeline>());
```

- [ ] **Step 2.6: Run tests, verify PASS**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingPipelineTests" --logger "console;verbosity=normal"
```

Expected: 2/2 PASS.

- [ ] **Step 2.7: Build whole solution**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
```

Expected: 0 errors.

- [ ] **Step 2.8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfIndexingPipeline.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipeline.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs \
        tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfIndexingPipelineTests.cs
git commit -m "feat(pdf-indexing): #2244 IPdfIndexingPipeline service + DI registration"
```

---

## Task 3: Migrate UploadPdfCommandHandler.Processing.cs

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs:569-624` (UpdateVectorDocumentAsync)

> **Scope note:** Tasks 6 and 7 in this plan separately remove the `scopedMediator.Publish(VectorDocumentIndexedEvent)` and refactor `FinalizeProcessingAsync` to use `TransitionTo(Ready)`. This task only addresses the `new VectorDocumentEntity {...}` block inside `UpdateVectorDocumentAsync`. Keep the manual publishes in place for now — Task 6 removes them once all 3 sites are migrated and the integration test confirms events flow structurally.

- [ ] **Step 3.1: Update the existing PdfIndexingFlowKbFlagIntegrationTests to also cover UploadPdf path**

Find the existing test class `tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowKbFlagIntegrationTests.cs` (shipped by #2263). Verify the upload-path scenario `UploadPdf_OnSuccessfulIndexing_SetsHasKnowledgeBaseTrue_OnSharedGame` exists. If yes, skip authoring; if not, add it.

Run it first to confirm BASELINE green BEFORE refactoring:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingFlowKbFlagIntegrationTests" --logger "console;verbosity=normal"
```

Expected: PASS (relies on compensating manual publish from #2263).

- [ ] **Step 3.2: Inject IPdfIndexingPipeline into UploadPdfCommandHandler**

Open `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs` (the partial class file, NOT `.Processing.cs`). Add `IPdfIndexingPipeline _pdfIndexingPipeline` field, set in ctor. Update the DI ctor signature accordingly.

If the handler instantiates the background task via `IServiceScopeFactory`, resolve `IPdfIndexingPipeline` from `scope.ServiceProvider.GetRequiredService<IPdfIndexingPipeline>()` inside the scope.

> **Discovery substep:** read the actual ctor first (`Read` the `.cs` file, NOT `.Processing.cs`). The pattern: most fields are injected via the primary constructor; new ones go there too.

- [ ] **Step 3.3: Replace `new VectorDocumentEntity {…}` with pipeline call**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`, locate `UpdateVectorDocumentAsync` (line 569-624). Replace the body:

```csharp
private async Task UpdateVectorDocumentAsync(
    string pdfId,
    PdfDocumentEntity pdfDoc,
    int indexedCount,
    MeepleAiDbContext db,
    IPdfIndexingPipeline pipeline,
    CancellationToken cancellationToken)
{
    var resolvedGameId = await PdfGameIdResolver.ResolveAsync(db, pdfDoc, cancellationToken)
        .ConfigureAwait(false);

    try
    {
        await pipeline.ExecuteAsync(pdfDoc, indexedCount, resolvedGameId, cancellationToken)
            .ConfigureAwait(false);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        MeepleAiMetrics.RecordPdfConcurrencyConflict(
            nameof(UploadPdfCommandHandler),
            MeepleAiMetrics.PdfConcurrencyCategories.B);
        _logger.LogWarning(ex,
            "Concurrency conflict on VectorDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
            pdfId, nameof(UploadPdfCommandHandler));
    }
}
```

Update the caller (the orchestration method that invokes `UpdateVectorDocumentAsync`) to pass `pipeline` (resolved from the live scope, same pattern as `scopedMediator`).

- [ ] **Step 3.4: Build**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
```

Expected: 0 errors.

- [ ] **Step 3.5: Run integration test**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingFlowKbFlagIntegrationTests" --logger "console;verbosity=normal"
```

Expected: PASS — both via the new structural flow AND the still-present compensating manual publish (double-firing not an issue, idempotent handler).

- [ ] **Step 3.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs
git commit -m "refactor(pdf-indexing): #2244 UploadPdf uses IPdfIndexingPipeline (call site 1/3)"
```

---

## Task 4: Migrate PdfProcessingPipelineService.cs (Quartz retry path)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:738-787`

- [ ] **Step 4.1: Write/extend integration test for Quartz path**

Add a sibling integration test class (or scenario) that runs `PdfProcessingPipelineService.ProcessPdfAsync` end-to-end against Testcontainers Postgres and asserts:
- `VectorDocument` row created
- `shared_games.has_knowledge_base = true`
- exactly one `VectorDocumentIndexedEvent` was dispatched (use a recording handler / test sink)

Location: `tests/Api.Tests/Integration/DocumentProcessing/PdfProcessingPipelineServiceQuartzPathIntegrationTests.cs`.

Run it — expected: FAIL (still bypasses domain — no event recorded, or only the compensating manual publish wired up to this path which currently isn't).

> **If the existing #2263 test sink isn't reusable here**, simply assert on `shared_games.has_knowledge_base = true` which is the user-visible contract. The event-count assertion is "nice to have".

- [ ] **Step 4.2: Inject IPdfIndexingPipeline into PdfProcessingPipelineService**

Update the ctor of `PdfProcessingPipelineService` to take `IPdfIndexingPipeline pipeline`. Same DI registration applies (already done in Task 2).

- [ ] **Step 4.3: Replace `new VectorDocumentEntity {…}` block**

In `IndexInVectorStoreAsync` (around line 738), replace the create/update path:

```csharp
private async Task IndexInVectorStoreAsync(
    PdfDocumentEntity pdfDoc,
    List<(DocumentChunkInput chunk, string lang, bool isTranslation)> translatedChunks,
    List<float[]> embeddings,
    CancellationToken cancellationToken)
{
    var chunkCount = translatedChunks.Count;
    var resolvedGameId = pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
    if (resolvedGameId == Guid.Empty)
    {
        _logger.LogWarning("[PdfPipeline] No GameId for PDF {PdfId}, skipping VectorDocument creation", pdfDoc.Id);
        return;
    }

    VectorDocument vectorDoc;
    try
    {
        vectorDoc = await _pipeline.ExecuteAsync(pdfDoc, chunkCount, resolvedGameId, cancellationToken)
            .ConfigureAwait(false);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        MeepleAiMetrics.RecordPdfConcurrencyConflict(
            nameof(PdfProcessingPipelineService),
            MeepleAiMetrics.PdfConcurrencyCategories.B);
        _logger.LogWarning(ex,
            "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
            pdfDoc.Id, nameof(PdfProcessingPipelineService));
        return; // CRITICAL: do not throw — Quartz must see job as successful
    }

    // pgvector indexing (unchanged from prior code, just uses vectorDoc.Id)
    if (_vectorStore != null && embeddings.Count == translatedChunks.Count)
    {
        var dimension = embeddings[0].Length;
        await _vectorStore.EnsureCollectionExistsAsync(resolvedGameId, dimension, cancellationToken).ConfigureAwait(false);
        await _vectorStore.DeleteByVectorDocumentIdAsync(vectorDoc.Id, cancellationToken).ConfigureAwait(false);
        // … rest of the existing pgvector upsert flow continues using `vectorDoc.Id` and `resolvedGameId`
    }
}
```

- [ ] **Step 4.4: Build + run tests**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfProcessingPipelineServiceQuartzPathIntegrationTests" --logger "console;verbosity=normal"
```

Expected: 0 build errors, integration tests PASS.

- [ ] **Step 4.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs \
        tests/Api.Tests/Integration/DocumentProcessing/PdfProcessingPipelineServiceQuartzPathIntegrationTests.cs
git commit -m "refactor(pdf-indexing): #2244 PdfProcessingPipelineService uses IPdfIndexingPipeline (call site 2/3)"
```

---

## Task 5: Migrate IndexPdfCommandHandler.cs (admin re-index path)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs` (around lines 258-285)

- [ ] **Step 5.1: Read the current handler to locate the `new VectorDocumentEntity {…}` block + the surrounding "existing vs new" branching**

```bash
# Inspect the call-site area
```

Read `IndexPdfCommandHandler.cs` lines 230-310 to confirm the branching pattern (`existingVectorDoc != null` → update; null → create).

- [ ] **Step 5.2: Write/extend integration test for admin re-index path**

Add scenario `IndexPdf_OnRebuild_SetsHasKnowledgeBaseTrue_AndRaisesEventOnce` in `tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowKbFlagIntegrationTests.cs` (the existing #2263 file). Asserts:
- `shared_games.has_knowledge_base = true` after admin re-index
- only ONE `VectorDocumentIndexedEvent` per re-index call

Run — expected: FAIL (this path's `new VectorDocumentEntity` still bypasses domain).

- [ ] **Step 5.3: Inject + replace**

Inject `IPdfIndexingPipeline _pipeline` into the handler ctor. Replace the `new VectorDocumentEntity {…}` block with:

```csharp
var resolvedGameId = pdf.PrivateGameId ?? pdf.SharedGameId ?? Guid.Empty;
if (resolvedGameId == Guid.Empty)
{
    throw new InvalidOperationException(
        $"Cannot index PDF {pdf.Id} — no resolvable GameId (PrivateGameId or SharedGameId required)");
}

var domainDoc = await _pipeline
    .ExecuteAsync(pdf, indexedChunkCount: pdf.PageCount ?? 1, resolvedGameId: resolvedGameId, cancellationToken)
    .ConfigureAwait(false);

// Reload existing entity reference if needed by downstream pgvector code
existingVectorDoc = await _db.VectorDocuments
    .AsTracking()
    .FirstAsync(v => v.Id == domainDoc.Id, cancellationToken)
    .ConfigureAwait(false);
```

> **Note** — `indexedChunkCount: pdf.PageCount ?? 1` is a placeholder; if the existing handler computes `chunkCount` elsewhere (e.g. from `embeddings.Count`), pass that value. Read the surrounding code (lines 220-300) and use the actual chunk-count variable in scope.

- [ ] **Step 5.4: Build + run tests**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingFlowKbFlagIntegrationTests" --logger "console;verbosity=normal"
```

Expected: 0 errors, all PASS.

- [ ] **Step 5.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs \
        tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowKbFlagIntegrationTests.cs
git commit -m "refactor(pdf-indexing): #2244 IndexPdfCommandHandler uses IPdfIndexingPipeline (call site 3/3)"
```

---

## Task 6: Remove tactical manual publishes (Sub #1 cleanup)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs:822-861`

- [ ] **Step 6.1: Confirm baseline (must PASS BEFORE removal)**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingFlowKbFlagIntegrationTests" --logger "console;verbosity=normal"
```

Expected: PASS — with both structural AND compensating manual publishes active.

- [ ] **Step 6.2: Delete the manual `VectorDocumentIndexedEvent` publish**

In `FinalizeProcessingAsync` (line 792-861), delete the entire block lines 833-861 (the `// Issue #2243 ... Block A` block including the `vectorDocSnapshot` query and the `scopedMediator.Publish(VectorDocumentIndexedEvent)` call). Keep ONLY:

```csharp
if (Guid.TryParse(pdfId, out var pdfGuidForEvent))
{
    // PdfStateChangedEvent is removed in Task 7 (TransitionTo). For now keep the manual
    // publish so handlers continue to fire while we ship one structural change per task.
    await scopedMediator.Publish(
        new PdfStateChangedEvent(pdfGuidForEvent, PdfProcessingState.Indexing, PdfProcessingState.Ready, userId),
        CancellationToken.None).ConfigureAwait(false);
}
```

- [ ] **Step 6.3: Run integration tests — MUST STILL PASS without compensating publish**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingFlowKbFlagIntegrationTests" --logger "console;verbosity=normal"
```

Expected: PASS. If FAIL, the structural flow is incomplete — investigate which call site still bypasses the pipeline.

- [ ] **Step 6.4: Build**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
```

Expected: 0 errors (drops unused `using` of `KnowledgeBase.Domain.Events` if it was only used for the manual publish).

- [ ] **Step 6.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs
git commit -m "refactor(pdf-indexing): #2244 drop tactical VectorDocumentIndexedEvent manual publish (#2263 Block A)"
```

---

## Task 7: PdfDocument.TransitionTo(Ready) via domain method

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs:792-861` (FinalizeProcessingAsync)

> **Design note:** The current code sets `pdfDoc.ProcessingState = nameof(PdfProcessingState.Ready)` on the EF entity (line 805). The domain method `PdfDocument.TransitionTo(Ready)` raises BOTH `PdfStateChangedEvent` AND `KbDocIndexedEvent` (PdfDocument.cs:425-443). Once we route the transition through the domain entity + `IPdfDocumentRepository.UpdateAsync`, the compensating manual `scopedMediator.Publish(PdfStateChangedEvent)` left over from Task 6 can also be removed.

- [ ] **Step 7.1: Discover the existing IPdfDocumentRepository signature**

Read `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IPdfDocumentRepository.cs` and the impl to confirm:
- a `GetByIdAsync(Guid id)` exists
- an `UpdateAsync(PdfDocument domain)` exists (and calls `CollectDomainEvents` + SaveChanges)

If `UpdateAsync` doesn't collect events, add the call to `RepositoryBase.CollectDomainEvents` consistent with `VectorDocumentRepository.UpdateAsync:67-73`. This sub-step may itself produce a small commit.

- [ ] **Step 7.2: Write the failing integration test**

Add in `PdfIndexingFlowKbFlagIntegrationTests.cs`:

```csharp
[Fact]
public async Task UploadPdf_OnReady_RaisesKbDocIndexedEventOnce()
{
    // Arrange — set up sharedGame + pdfDoc via existing harness
    // Act — run pipeline through to Ready
    // Assert — exactly one KbDocIndexedEvent in the recording sink
}
```

Run — expected: FAIL (current code doesn't go through domain, no `KbDocIndexedEvent` raised structurally).

- [ ] **Step 7.3: Refactor FinalizeProcessingAsync**

Inject `IPdfDocumentRepository _pdfRepo`. Replace the lines 802-820 block:

```csharp
private async Task FinalizeProcessingAsync(
    string pdfId,
    PdfDocumentEntity pdfDoc,
    Guid userId,
    MeepleAiDbContext db,
    IPdfUploadQuotaService quotaService,
    IMediator scopedMediator,
    IPdfDocumentRepository pdfRepo,
    DateTime startTime,
    CancellationToken cancellationToken)
{
    var totalPages = pdfDoc.PageCount ?? 0;
    await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

    var pdfGuid = Guid.Parse(pdfId);

    // Load domain aggregate, transition through the state machine so PdfStateChangedEvent
    // + KbDocIndexedEvent are raised structurally, then persist via repository so the
    // collector picks them up and the DbContext SaveChanges dispatcher publishes them.
    var pdfDomain = await pdfRepo.GetByIdAsync(pdfGuid, cancellationToken).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"PdfDocument {pdfGuid} not found while finalizing");

    try
    {
        pdfDomain.TransitionTo(PdfProcessingState.Ready);
        await pdfRepo.UpdateAsync(pdfDomain, cancellationToken).ConfigureAwait(false);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        MeepleAiMetrics.RecordPdfConcurrencyConflict(
            nameof(UploadPdfCommandHandler),
            MeepleAiMetrics.PdfConcurrencyCategories.B);
        _logger.LogWarning(ex,
            "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
            pdfId, nameof(UploadPdfCommandHandler));
        return;
    }

    // Sub #2: PdfStateChangedEvent + KbDocIndexedEvent are now raised by PdfDocument.TransitionTo
    // and dispatched by the SaveChanges flow above. No manual publish required.

    var cacheKey = (pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId)?.ToString() ?? string.Empty;
    await InvalidateCacheSafelyAsync(cacheKey, "PDF processing", cancellationToken).ConfigureAwait(false);
    // ... quota confirmation continues as before
}
```

> **Caveat** — if `PdfDocument` domain doesn't have a public `TransitionTo`, look for the actual public method (the code at PdfDocument.cs:425-443 is inside a private method called by another internal one). The plan author saw `internal sealed class PdfDocument` so visibility within the assembly is fine. Use whatever public-to-the-handler method advances state to Ready (e.g., `MarkAsReady()` or `CompleteProcessing()`). If a single-arg `TransitionTo(state)` is private, expose a state-specific public method like `MarkReady()` on the aggregate.

- [ ] **Step 7.4: Update the caller signature so `pdfRepo` flows in**

The orchestration method that calls `FinalizeProcessingAsync` now passes `scope.ServiceProvider.GetRequiredService<IPdfDocumentRepository>()` as `pdfRepo`. Update accordingly.

- [ ] **Step 7.5: Build + run the entire DocumentProcessing+KnowledgeBase test slice**

```bash
dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo /clp:ErrorsOnly
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DocumentProcessing|FullyQualifiedName~KnowledgeBase" --logger "console;verbosity=minimal"
```

Expected: 0 build errors, all PASS. Special attention:

- `PdfIndexingFlowKbFlagIntegrationTests` — green
- `PdfDocument_SevenStateProgression_ShouldAdvanceThroughAllStates` (CLAUDE.md baseline) — should still emit 7 events (6 `PdfStateChangedEvent` + 1 `KbDocIndexedEvent`). The Ready transition no longer happens TWICE (once via EF set, once via domain) so the count should match the existing expectation.

- [ ] **Step 7.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs \
        tests/Api.Tests/Integration/DocumentProcessing/PdfIndexingFlowKbFlagIntegrationTests.cs
git commit -m "refactor(pdf-indexing): #2244 PdfDocument.TransitionTo(Ready) via domain (drop final tactical publish)"
```

---

## Task 8: Full regression suite + flaky baseline check

- [ ] **Step 8.1: Run the full backend test suite**

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --logger "console;verbosity=minimal" 2>&1 | tee /tmp/sub2-test-output.txt
```

Expected: same or better passed/failed/skipped count vs Step 0.3 baseline.

- [ ] **Step 8.2: Verify CLAUDE.md known-flaky baseline is still clean**

```bash
grep -E "PdfDocument_SevenStateProgression|S3Storage" /tmp/sub2-test-output.txt || echo "BASELINE CLEAN"
```

Expected: `BASELINE CLEAN`. If `PdfDocument_SevenStateProgression` fails or emits a different event count, update the CLAUDE.md known-flaky table inline with the fix-or-skip note.

- [ ] **Step 8.3: Search for any leftover anti-pattern instances**

```bash
# These must be UPPER BOUND: the only allowed `new VectorDocumentEntity {...}` constructions are:
#   - KnowledgeBaseMappers.ToEntity (legitimate domain→entity mapping)
#   - Administration/ImportRagData (separate path, out of scope for #2244 — flag as follow-up if non-trivial)
#   - CompleteChunkedUploadCommandHandler (chunked uploads, separate path — flag as follow-up)
```

Use Grep tool (NOT bash grep): pattern `new VectorDocumentEntity` over `apps/api/src/Api`. Confirm only the expected mapper + the 2 out-of-scope handlers remain. Any UploadPdf/PdfProcessingPipelineService/IndexPdf occurrence is a regression — investigate and fix.

- [ ] **Step 8.4: Verify event flow integration sanity**

Re-run `PdfIndexingFlowKbFlagIntegrationTests` explicitly one more time after all the other changes:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfIndexingFlowKbFlagIntegrationTests" --logger "console;verbosity=normal"
```

Expected: all scenarios green, no tactical publishes left.

---

## Task 9: PR + admin-squash merge

- [ ] **Step 9.1: Push branch**

```bash
git push -u origin feature/issue-2244-pdf-indexing-architecture-refactor
```

- [ ] **Step 9.2: Open PR (parent = main-dev per CLAUDE.md)**

```bash
gh pr create --base main-dev --head feature/issue-2244-pdf-indexing-architecture-refactor \
  --title "refactor(pdf-indexing): #2244 VectorDocument.Create factory + IPdfIndexingPipeline (Sub #2 of #2242)" \
  --body "$(cat <<'EOF'
## Summary

Architecture refactor for PDF→KB indexing flow (epic #2242 Sub #2). Replaces the tactical compensating publish shipped in #2263 (Sub #1) with structural event raising via:

1. `VectorDocument.Create(...)` static factory (ctor private) — raises `VectorDocumentIndexedEvent`
2. `IPdfIndexingPipeline.ExecuteAsync(...)` — single owner of build/persist/raise across 3 ingestion paths
3. `KnowledgeBaseMappers.ToDomain()` now uses `Rehydrate(...)` (read-side bug fix — no false event on every entity read)
4. 3 migrated call sites: `UploadPdfCommandHandler.Processing.cs`, `PdfProcessingPipelineService.cs`, `IndexPdfCommandHandler.cs`
5. `PdfDocument.TransitionTo(Ready)` via domain — drops manual `PdfStateChangedEvent` + `KbDocIndexedEvent` publishes

## Behavior changes

- `shared_games.has_knowledge_base = true` and the activity rail `KbDocIndexedEvent` now flow through MediatR via the `IDomainEventCollector` → `MeepleAiDbContext.SaveChangesAsync` → `_mediator.Publish` pipeline, not a hand-rolled compensating publish.
- Tactical Block A code from #2263 removed (UploadPdfCommandHandler.Processing.cs:833-861).
- Latent read-side bug fixed: previously every `VectorDocumentEntity.ToDomain()` enqueued a fresh `VectorDocumentIndexedEvent`.

## Test plan

- [x] Unit: `VectorDocumentTests` (factory raises event, Rehydrate doesn't, public ctor inaccessible)
- [x] Unit: `PdfIndexingPipelineTests` (Moq IVectorDocumentRepository, create + update paths)
- [x] Integration: `PdfIndexingFlowKbFlagIntegrationTests` — green WITHOUT compensating publish
- [x] Integration: Quartz path + admin re-index path scenarios added
- [x] Regression: full `Api.Tests` suite, known-flaky baseline (`PdfDocument_SevenStateProgression`) clean

## Cross-refs

- Epic: #2242
- Sub-issue: #2244
- Replaces: tactical Block A from #2263 (#2243)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 9.3: Wait for CI to settle**

CI must show green for the relevant required jobs. If a known-flaky baseline test fails ONCE, the policy is 1× re-run (P119 catastrophic crash → transient).

- [ ] **Step 9.4: Admin-squash merge (P145 pattern)**

```bash
gh pr merge <PR-NUMBER> --squash --admin --delete-branch
```

This creates a single squashed commit on `main-dev` with the PR title + description.

- [ ] **Step 9.5: Verify issue #2244 auto-closed**

```bash
gh issue view 2244 --json state,closedAt
```

Expected: `state = CLOSED`.

---

## Task 10: Memory pattern P234 + cleanup

- [ ] **Step 10.1: Create memory file**

```bash
# Path: ~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/p234-domain-event-bypass-via-ef-entity.md
```

Content template (Windows path uses literal backslashes when calling Write tool):

```markdown
---
name: p234-domain-event-bypass-via-ef-entity
description: Anti-pattern where ingestion code writes EF persistence entities directly, bypassing the domain aggregate's event-raising constructor. Discovered in epic #2242 PDF indexing flow.
metadata:
  type: feedback
---

# P234 — Domain event bypass via EF entity

**Anti-pattern**: 3 ingestion code paths constructed `VectorDocumentEntity` directly via EF (`new VectorDocumentEntity { Id = ... }`) instead of going through the domain aggregate `VectorDocument` whose constructor raises `VectorDocumentIndexedEvent`. As a result `VectorDocumentIndexedForKbFlagHandler` never ran and `shared_games.has_knowledge_base` stayed `false` after a successful index.

**Why it happens**: handlers operate on EF entities throughout the pipeline (`PdfDocumentEntity`, `VectorDocumentEntity`) because batching `SaveChangesAsync` is easier than juggling domain aggregates per repo call. The cost: domain events are silently dropped.

**Detection**: grep `new (Vector|Pdf|...)DocumentEntity` outside `Infrastructure/Persistence/Mappers/`. Any match in `Application/Commands` or `Application/Services` is suspect.

**Fix recipe**:
1. Make the domain aggregate ctor `private`; add `public static Create(...)` factory that raises events.
2. Add `internal static Rehydrate(...)` for mappers (no event raised on read).
3. Extract an `I<Domain>Pipeline` service that calls `VectorDocument.Create()` + `IVectorDocumentRepository.AddAsync(...)`.
4. Inject the pipeline into every ingestion handler; replace `new …Entity` blocks.
5. For state transitions on existing aggregates (`pdfDoc.ProcessingState = "Ready"`), load the domain, call `TransitionTo(...)`, persist via repository so events flow.
6. Remove any compensating manual `_mediator.Publish(...)` left from the tactical hotfix.

**Tactical hotfix (Sub #1)**: ship a compensating `_mediator.Publish(VectorDocumentIndexedEvent)` in the handler after SaveChanges. Loud comment marking it tactical so the structural follow-up doesn't disappear.

**Structural fix (Sub #2)**: refactor described above. Tactical publish removed.

**Reference**: epic #2242, sub-issues #2243 (tactical) + #2244 (structural), PRs #2263 + (this PR).

**How to apply**: whenever you see a domain entity with `AddDomainEvent` in its ctor AND an EF persistence twin, audit the ingestion paths. If they bypass the domain, plan a Sub-N structural refactor; do not perpetuate the bypass with another manual publish.
```

- [ ] **Step 10.2: Update MEMORY.md index**

Add ONE LINE to `~/.claude/projects/D--Repositories-meepleai-monorepo-frontend/memory/MEMORY.md` near the top:

```markdown
- [P234 domain-event-bypass-via-ef-entity](p234-domain-event-bypass-via-ef-entity.md) — Anti-pattern from epic #2242 PDF indexing: handlers writing EF entity directly skip domain ctor → events dropped. Tactical hotfix #2263 (manual publish), structural fix #2244 (factory + pipeline).
```

- [ ] **Step 10.3: Cleanup local branch**

```bash
git checkout main-dev
git pull --ff-only origin main-dev
git branch -D feature/issue-2244-pdf-indexing-architecture-refactor   # was auto-deleted by --delete-branch but ensure local is gone
git remote prune origin
```

- [ ] **Step 10.4: Verify epic #2242 progress**

```bash
gh issue view 2242 --json title,state,body | jq -r '.body' | grep -E "Sub #(1|2|3|4|5|6)" || true
```

Document next sub-issue (#2245 Sub #3) as the recommended next session.

---

## Self-Review Checklist

After writing the plan, verify against the spec (#2244 issue body):

**Spec coverage**
- ✅ Step 1 (factory + private ctor) → Task 1
- ✅ Step 2 (IPdfIndexingPipeline + DI both interface+impl) → Task 2
- ✅ Step 3 (migrate 3 call sites) → Tasks 3, 4, 5
- ✅ Step 4 (remove compensating publish from Sub #1) → Task 6
- ✅ Step 5 (TransitionTo(Ready) via domain) → Task 7
- ✅ Acceptance criteria: VectorDocument.Create raises event (Task 1 test); 3 call sites refactored (Tasks 3-5); integration test passes without compensating publish (Task 6); CI green + no flaky regression (Task 8); CQRS compliance (no endpoint changes — handlers only); DI both registrations (Task 2.5)

**Placeholder scan**
- ✅ Every code block contains actual content
- ✅ No "TODO", "TBD", "implement later"
- ✅ Test code includes payloads + assertions
- ✅ Step 5.3 has a discovery note instead of a placeholder (chunk count variable name unknown until Read in execution)

**Type consistency**
- ✅ `VectorDocument.Create(id, gameId, pdfDocumentId, language, totalChunks, sharedGameId?)` — same signature across Tasks 1, 2, 5
- ✅ `IPdfIndexingPipeline.ExecuteAsync(pdfDoc, indexedChunkCount, resolvedGameId, ct)` — same across Tasks 2, 3, 4, 5
- ✅ `IVectorDocumentRepository.AddAsync(VectorDocument, ct)` — matches existing interface (verified by reading `IVectorDocumentRepository.cs:47`)
- ✅ `PdfDocument.TransitionTo(PdfProcessingState)` — visibility caveat called out in Task 7.3

**Known unknowns flagged as discovery substeps**
- Step 3.2: actual UploadPdfCommandHandler ctor pattern
- Step 5.1: actual chunk count variable name in IndexPdfCommandHandler
- Step 7.1: IPdfDocumentRepository.UpdateAsync presence of CollectDomainEvents
- Step 7.3: visibility of PdfDocument.TransitionTo (private vs internal vs public)

Each discovery substep is a read-and-adapt action, not a placeholder.

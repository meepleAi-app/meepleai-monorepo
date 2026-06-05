# Per-Doc Embeddings Viewer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il bottone "📋 View embeddings" nel hero-actions di `KbDocDetailPanel` come drawer scoped al PDF document selezionato, mostrando meta strip 4 KPI (Model · Dim · Total chunks · Indexed at) + ricerca semantica intra-doc + export, senza esporre raw vector values (zero corpus reconstruction risk).

**Architecture:** Backend: 1 NEW CQRS Query (`GetDocumentEmbeddingsMetaQuery`) + handler che risolve `PdfDocumentId → VectorDocument → Model dal primo embedding`, exposed via `GET /api/v1/admin/kb/docs/{docId}/embeddings/meta` con `[AuditableAction]` Level 1. Frontend: 1 NEW `DocumentEmbeddingsDrawer` (Sheet primitive side="right") + 5 sub-components + TanStack Query hook. Riusa endpoint `chunks/search` + `chunks/export` esistenti da #1653 FU-4 per ricerca semantica scoped + export footer.

**Tech Stack:** .NET 9 + EF Core 9 + MediatR + xUnit + Testcontainers (BE); Next.js 16 + React 19 + TanStack Query + Tailwind 4 + Vitest + Playwright (FE).

**Spec di riferimento**: [`docs/superpowers/specs/2026-06-05-per-doc-embeddings-viewer-design.md`](../specs/2026-06-05-per-doc-embeddings-viewer-design.md)

---

## 📋 Post-Review Fixes (applicare durante execution)

> Review subagent 2026-06-05 ha identificato 5 issue dopo Grep validation reale del codebase. **Tutti i fix sono applicati inline durante implementation**; le sezioni Task sottostanti restano come "v1" per audit history ma vanno seguite con questi adjustments.

### FIX-1 (CRITICAL): `VectorDocumentEntity` field name mismatch

L'entity reale (`apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/VectorDocumentEntity.cs`) ha:
- `ChunkCount` (non `TotalChunks`)
- `IndexedAt` è **`DateTime?`** nullable (non `DateTimeOffset`)
- **`EmbeddingModel`** + **`EmbeddingDimensions`** denormalizzati DIRETTAMENTE sulla entity → **NO seconda SELECT** su `PgVectorEmbeddings` necessaria
- **NO field `Language`** — recuperare da `PdfDocumentEntity.Language` (non-nullable, default "en") via JOIN

**Impact su task**:
- A.1 DTO: `IndexedAt: DateTime?` invece di `DateTimeOffset`
- B.1 Handler: refactor a single query con JOIN PdfDocuments per Language; usa `v.ChunkCount/EmbeddingModel/EmbeddingDimensions` direct
- B.1 Test fixture: usa `ChunkCount`, no seed di PgVectorEmbedding, seed `PdfDocumentEntity` con `Language="en"`, `IndexedAt = DateTime.UtcNow`
- B.2 Test: NotFoundException assertion via `Message` non `ResourceId`
- **B.3 DELETED** (no more "zero embeddings" case — Model è su VectorDoc stessa)
- **B.5 DELETED** (PdfDocument.Language non-nullable con default → no null case)
- C.2/C.3 Integration: stessi fixture adjustments

### FIX-2 (CRITICAL): `useSearchDocumentChunks` FE hook NON ESISTE

Grep `apps/web/src/hooks` → 0 match. FU-4 #1653 ha shipped solo BE search endpoint. **Aggiungere Task G.0** PRIMA di G.1 per creare hook FE che wrappa `POST /admin/kb/docs/{docId}/chunks/search` con `useMutation` TanStack.

### FIX-3 (IMPORTANT): `NotFoundException` constructor semantic

Costruttore `(string resourceType, string? resourceId)` usa `resourceId` come identifier, non message. **Usare** `new NotFoundException("Document not indexed")` single-arg → produce ProblemDetails message corretto. Aggiornare test B.2 assertion → `ex.Message.Should().Contain("Document not indexed")`.

### FIX-4 (IMPORTANT): VecThumb test missing `seed: number` case

Aggiungere in F.2 Step 1: `it('renders gradient for numeric seed', () => render(<VecThumb seed={42} />))` per coprire `String(seed)` coercion path usato da `EmbeddingsResultRow`.

### FIX-5 (SUGGESTION): Date locale + null handling in MetaStrip

- F.3 MetaStrip render: gestire `data.indexedAt === null` → mostra "—"
- Date format: prefer ISO format invariant o `Intl.DateTimeFormat` con `timeZone: 'UTC'` esplicito (evita flake CI vs dev)

---

## File Structure

### Backend (NEW)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQuery.cs` — Query record + AuditableAction attribute
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/DocumentEmbeddingsMetaDto.cs` — Response DTO record
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQueryValidator.cs` — FluentValidator
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQueryHandler.cs` — Handler

### Backend (MODIFY)
- `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` — aggiungi endpoint MapGet (zona linee 100-130)

### Backend Tests (NEW)
- `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs` — 6 unit
- `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryValidatorTests.cs` — 1 unit
- `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs` — 6 integration

### Frontend (NEW)
- `apps/web/src/lib/api/schemas/admin-kb-embeddings.schemas.ts` — Zod schema + TS type
- `apps/web/src/lib/api/admin-kb-embeddings.ts` — `getDocumentEmbeddingsMeta(docId)` fetcher
- `apps/web/src/hooks/admin/use-document-embeddings-meta.ts` — TanStack Query hook + queryKey
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/index.tsx` — Drawer orchestrator export
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/document-embeddings-drawer.tsx` — Drawer shell
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-meta-strip.tsx` — 4 KPI cards
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-search-panel.tsx` — search form + results
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-result-row.tsx` — single row collapse/expand
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/vec-thumb.tsx` — gradient deterministic
- `apps/web/src/lib/util/simple-hash.ts` — hash function for VecThumb seed (cyrb53)

### Frontend (MODIFY)
- `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` — wire-up trigger button + drawer state

### Frontend Tests (NEW)
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/document-embeddings-drawer.test.tsx` — ~13 component
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/vec-thumb.test.tsx` — 1 deterministic
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-meta-strip.test.tsx` — 3
- `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-result-row.test.tsx` — 1
- `apps/web/src/hooks/admin/__tests__/use-document-embeddings-meta.test.ts` — 3 hook
- `apps/web/src/lib/util/__tests__/simple-hash.test.ts` — 2 hash

### E2E (NEW)
- `apps/web/e2e/admin-kb-embeddings-viewer.spec.ts` — 4 spec

---

## Phase A — Backend Foundation (DTO + Query + Validator)

### Task A.1: DTO record

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/DocumentEmbeddingsMetaDto.cs`

- [ ] **Step 1: Create DTO record**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

internal sealed record DocumentEmbeddingsMetaDto(
    Guid DocId,
    string Model,
    int Dimensions,
    int TotalChunks,
    DateTimeOffset IndexedAt,
    string? Language);
```

- [ ] **Step 2: Verify compile**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/DocumentEmbeddingsMetaDto.cs
git commit -m "feat(admin-kb): #1674 add DocumentEmbeddingsMetaDto record"
```

---

### Task A.2: Query record with AuditableAction

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQuery.cs`

- [ ] **Step 1: Create Query record**

```csharp
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

[AuditableAction("EmbeddingsMetaView", "Document", Level = 1, UserIdSource = AuditUserIdSource.Caller)]
internal sealed record GetDocumentEmbeddingsMetaQuery(Guid DocId)
    : IQuery<DocumentEmbeddingsMetaDto>;
```

- [ ] **Step 2: Verify compile**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQuery.cs
git commit -m "feat(admin-kb): #1674 add GetDocumentEmbeddingsMetaQuery with audit Level 1"
```

---

### Task A.3: Validator + unit test

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQueryValidator.cs`
- Create: `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryValidatorTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetDocumentEmbeddingsMetaQueryValidatorTests
{
    [Fact]
    public void Validator_Rejects_Empty_DocId()
    {
        var validator = new GetDocumentEmbeddingsMetaQueryValidator();
        var result = validator.TestValidate(new GetDocumentEmbeddingsMetaQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(q => q.DocId);
    }

    [Fact]
    public void Validator_Accepts_Valid_DocId()
    {
        var validator = new GetDocumentEmbeddingsMetaQueryValidator();
        var result = validator.TestValidate(new GetDocumentEmbeddingsMetaQuery(Guid.NewGuid()));
        result.ShouldNotHaveValidationErrorFor(q => q.DocId);
    }
}
```

- [ ] **Step 2: Run test (should fail compile — Validator class missing)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "GetDocumentEmbeddingsMetaQueryValidatorTests"`
Expected: FAIL — Validator class not defined.

- [ ] **Step 3: Implement validator**

```csharp
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

internal sealed class GetDocumentEmbeddingsMetaQueryValidator
    : AbstractValidator<GetDocumentEmbeddingsMetaQuery>
{
    public GetDocumentEmbeddingsMetaQueryValidator()
    {
        RuleFor(q => q.DocId)
            .NotEmpty()
            .WithMessage("DocId is required.");
    }
}
```

- [ ] **Step 4: Run tests (should pass)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "GetDocumentEmbeddingsMetaQueryValidatorTests"`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQueryValidator.cs apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryValidatorTests.cs
git commit -m "test(admin-kb): #1674 add validator for GetDocumentEmbeddingsMetaQuery"
```

---

## Phase B — Backend Handler (TDD per 3 scenari)

### Task B.1: Handler scaffold + happy path test

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQueryHandler.cs`
- Create: `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs`

- [ ] **Step 1: Write happy path test (failing)**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Entities;
using Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetDocumentEmbeddingsMetaQueryHandlerTests : IAsyncDisposable
{
    private readonly MeepleAiDbContext _db;

    public GetDocumentEmbeddingsMetaQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"emb_meta_{Guid.NewGuid():N}")
            .Options;
        _db = new MeepleAiDbContext(options);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Returns_Meta_When_Document_Indexed()
    {
        var pdfId = Guid.NewGuid();
        var vectorDocId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var indexedAt = DateTimeOffset.UtcNow.AddHours(-2);

        _db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocId,
            PdfDocumentId = pdfId,
            GameId = gameId,
            Language = "en",
            TotalChunks = 412,
            IndexedAt = indexedAt,
        });
        _db.PgVectorEmbeddings.Add(new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = vectorDocId,
            GameId = gameId,
            Model = "bge-base-en-v1.5",
            ChunkIndex = 0,
            TextContent = "stub",
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetDocumentEmbeddingsMetaQueryHandler(_db, NullLogger<GetDocumentEmbeddingsMetaQueryHandler>.Instance);
        var result = await handler.Handle(new GetDocumentEmbeddingsMetaQuery(pdfId), TestContext.Current.CancellationToken);

        result.DocId.Should().Be(pdfId);
        result.Model.Should().Be("bge-base-en-v1.5");
        result.Dimensions.Should().Be(768);
        result.TotalChunks.Should().Be(412);
        result.IndexedAt.Should().BeCloseTo(indexedAt, TimeSpan.FromSeconds(1));
        result.Language.Should().Be("en");
    }
}
```

> **Note**: Se nel codebase `PgVectorEmbeddings` non è il DbSet name, sostituire con il nome esatto del DbSet in `MeepleAiDbContext` (verifica via Grep `DbSet<PgVectorEmbeddingEntity>` o similar prima di runnare). Stessa cosa per `VectorDocumentEntity` properties (verifica `Language`, `IndexedAt` types).

- [ ] **Step 2: Run test (should fail compile — handler missing)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "Returns_Meta_When_Document_Indexed"`
Expected: FAIL — `GetDocumentEmbeddingsMetaQueryHandler` not defined.

- [ ] **Step 3: Implement handler (happy path only)**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using Api.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;

internal sealed class GetDocumentEmbeddingsMetaQueryHandler
    : IQueryHandler<GetDocumentEmbeddingsMetaQuery, DocumentEmbeddingsMetaDto>
{
    // Dimensions schema-locked al modello bge-base-en-v1.5 (768).
    // Vedi D-EV-5 nello spec per discrepanza con /admin/embedding/info (1024 stale).
    private const int EmbeddingDimensions = 768;

    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetDocumentEmbeddingsMetaQueryHandler> _logger;

    public GetDocumentEmbeddingsMetaQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetDocumentEmbeddingsMetaQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DocumentEmbeddingsMetaDto> Handle(
        GetDocumentEmbeddingsMetaQuery request,
        CancellationToken cancellationToken)
    {
        var vectorDoc = await _db.VectorDocuments
            .AsNoTracking()
            .Where(v => v.PdfDocumentId == request.DocId)
            .Select(v => new { v.Id, v.Language, v.IndexedAt, v.TotalChunks })
            .FirstOrDefaultAsync(cancellationToken);

        if (vectorDoc is null)
        {
            _logger.LogWarning("Document {DocId} not indexed (no VectorDocument row)", request.DocId);
            throw new NotFoundException("Document", "not indexed");
        }

        var model = await _db.PgVectorEmbeddings
            .AsNoTracking()
            .Where(e => e.VectorDocumentId == vectorDoc.Id)
            .Select(e => e.Model)
            .FirstOrDefaultAsync(cancellationToken);

        if (model is null)
        {
            _logger.LogWarning("VectorDocument {VectorDocId} exists but has 0 embeddings (corrupted state)", vectorDoc.Id);
            throw new NotFoundException("Embeddings", "no embeddings found for this document");
        }

        _logger.LogInformation(
            "Returning embeddings meta for doc {DocId}: model={Model}, chunks={Chunks}",
            request.DocId, model, vectorDoc.TotalChunks);

        return new DocumentEmbeddingsMetaDto(
            DocId: request.DocId,
            Model: model,
            Dimensions: EmbeddingDimensions,
            TotalChunks: vectorDoc.TotalChunks,
            IndexedAt: vectorDoc.IndexedAt,
            Language: vectorDoc.Language);
    }
}
```

- [ ] **Step 4: Run test (should pass)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "Returns_Meta_When_Document_Indexed"`
Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetDocumentEmbeddingsMeta/GetDocumentEmbeddingsMetaQueryHandler.cs apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs
git commit -m "feat(admin-kb): #1674 add handler happy path + unit test"
```

---

### Task B.2: Handler 404 — VectorDocument missing

**Files:**
- Modify: `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public async Task Throws_NotFound_When_VectorDocument_Missing()
{
    var pdfId = Guid.NewGuid();
    // No VectorDocument seeded

    var handler = new GetDocumentEmbeddingsMetaQueryHandler(_db, NullLogger<GetDocumentEmbeddingsMetaQueryHandler>.Instance);
    var act = async () => await handler.Handle(new GetDocumentEmbeddingsMetaQuery(pdfId), TestContext.Current.CancellationToken);

    var ex = await act.Should().ThrowAsync<NotFoundException>();
    ex.Which.ResourceType.Should().Be("Document");
    ex.Which.ResourceId.Should().Be("not indexed");
}
```

- [ ] **Step 2: Run test (should pass — already implemented in B.1)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "Throws_NotFound_When_VectorDocument_Missing"`
Expected: 1 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs
git commit -m "test(admin-kb): #1674 cover NotFound when VectorDocument missing"
```

---

### Task B.3: Handler 404 — VectorDoc esiste, 0 embeddings

**Files:**
- Modify: `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs`

- [ ] **Step 1: Add failing test**

```csharp
[Fact]
public async Task Throws_NotFound_When_VectorDocument_Has_Zero_Embeddings()
{
    var pdfId = Guid.NewGuid();
    var vectorDocId = Guid.NewGuid();

    _db.VectorDocuments.Add(new VectorDocumentEntity
    {
        Id = vectorDocId,
        PdfDocumentId = pdfId,
        GameId = Guid.NewGuid(),
        Language = "en",
        TotalChunks = 0,
        IndexedAt = DateTimeOffset.UtcNow,
    });
    await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    // No PgVectorEmbeddings seeded

    var handler = new GetDocumentEmbeddingsMetaQueryHandler(_db, NullLogger<GetDocumentEmbeddingsMetaQueryHandler>.Instance);
    var act = async () => await handler.Handle(new GetDocumentEmbeddingsMetaQuery(pdfId), TestContext.Current.CancellationToken);

    var ex = await act.Should().ThrowAsync<NotFoundException>();
    ex.Which.ResourceType.Should().Be("Embeddings");
    ex.Which.ResourceId.Should().Be("no embeddings found for this document");
}
```

- [ ] **Step 2: Run test (should pass — already implemented in B.1)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "Throws_NotFound_When_VectorDocument_Has_Zero_Embeddings"`
Expected: 1 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs
git commit -m "test(admin-kb): #1674 cover NotFound when 0 embeddings (corrupted state)"
```

---

### Task B.4: Handler — AuditableAction attribute presence test

**Files:**
- Modify: `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs`

- [ ] **Step 1: Add test for attribute reflection**

```csharp
[Fact]
public void AuditableAction_Attribute_Applied_On_Query()
{
    var attr = typeof(GetDocumentEmbeddingsMetaQuery)
        .GetCustomAttributes(typeof(AuditableActionAttribute), inherit: false)
        .Cast<AuditableActionAttribute>()
        .SingleOrDefault();

    attr.Should().NotBeNull();
    attr!.Action.Should().Be("EmbeddingsMetaView");
    attr.Resource.Should().Be("Document");
    attr.Level.Should().Be(1);
    attr.UserIdSource.Should().Be(AuditUserIdSource.Caller);
}
```

Aggiungi anche al top del file:
```csharp
using Api.BoundedContexts.Administration.Application.Attributes;
```

- [ ] **Step 2: Run test (should pass — attribute already in A.2)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "AuditableAction_Attribute_Applied_On_Query"`
Expected: 1 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs
git commit -m "test(admin-kb): #1674 assert AuditableAction attribute on query class"
```

---

### Task B.5: Handler — Language null edge case

**Files:**
- Modify: `apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs`

- [ ] **Step 1: Add test**

```csharp
[Fact]
public async Task Returns_Language_Null_When_VectorDocument_Language_Null()
{
    var pdfId = Guid.NewGuid();
    var vectorDocId = Guid.NewGuid();

    _db.VectorDocuments.Add(new VectorDocumentEntity
    {
        Id = vectorDocId,
        PdfDocumentId = pdfId,
        GameId = Guid.NewGuid(),
        Language = null,
        TotalChunks = 100,
        IndexedAt = DateTimeOffset.UtcNow,
    });
    _db.PgVectorEmbeddings.Add(new PgVectorEmbeddingEntity
    {
        Id = Guid.NewGuid(),
        VectorDocumentId = vectorDocId,
        GameId = Guid.NewGuid(),
        Model = "bge-base-en-v1.5",
        ChunkIndex = 0,
        TextContent = "stub",
    });
    await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var handler = new GetDocumentEmbeddingsMetaQueryHandler(_db, NullLogger<GetDocumentEmbeddingsMetaQueryHandler>.Instance);
    var result = await handler.Handle(new GetDocumentEmbeddingsMetaQuery(pdfId), TestContext.Current.CancellationToken);

    result.Language.Should().BeNull();
}
```

- [ ] **Step 2: Run test (should pass — handler handles nullable)**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "Returns_Language_Null"`
Expected: 1 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Unit/KnowledgeBase/Queries/GetDocumentEmbeddingsMetaQueryHandlerTests.cs
git commit -m "test(admin-kb): #1674 cover null language edge case"
```

---

## Phase C — Backend Endpoint + Integration Tests

### Task C.1: Register endpoint route

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` (zona dopo `MapPost("/docs/{docId:guid}/chunks/search"...)` finale, intorno linea 125)

- [ ] **Step 1: Add using statement at top**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
```

- [ ] **Step 2: Add endpoint mapping**

Subito dopo il blocco `kbGroup.MapPost("/docs/{docId:guid}/chunks/search", ...)`:

```csharp
// GET /api/v1/admin/kb/docs/{docId}/embeddings/meta — Issue #1674
kbGroup.MapGet("/docs/{docId:guid}/embeddings/meta", async (
    Guid docId,
    IMediator m,
    CancellationToken ct) =>
{
    var result = await m.Send(new GetDocumentEmbeddingsMetaQuery(docId), ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.WithName("GetDocumentEmbeddingsMeta")
.WithSummary("Get document embeddings metadata (model, dimensions, total chunks, indexed at).");
```

- [ ] **Step 3: Verify build + run all KnowledgeBase unit tests**

```bash
cd apps/api/src/Api && dotnet build
cd ../../tests/Api.Tests && dotnet test --filter "BoundedContext=KnowledgeBase&Category=Unit"
```
Expected: Build succeeded; all KB unit tests pass (existing + 6 new).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs
git commit -m "feat(admin-kb): #1674 register GET /admin/kb/docs/{docId}/embeddings/meta endpoint"
```

---

### Task C.2: Integration test — happy path + audit row

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs`

- [ ] **Step 1: Scaffold integration test class with shared fixture pattern**

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetDocumentEmbeddingsMeta;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Entities;
using Api.Infrastructure.Persistence;
using Api.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetDocumentEmbeddingsMetaIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private CustomWebApplicationFactory? _factory;
    private HttpClient? _client;
    private string? _databaseName;

    public GetDocumentEmbeddingsMetaIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_emb_meta_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _factory = new CustomWebApplicationFactory(connectionString);
        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_databaseName is not null) await _fixture.DropDatabaseAsync(_databaseName);
    }

    private async Task<Guid> SeedIndexedDocAsync(Guid? adminUserId = null)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfId = Guid.NewGuid();
        var vectorDocId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocId,
            PdfDocumentId = pdfId,
            GameId = gameId,
            Language = "en",
            TotalChunks = 412,
            IndexedAt = DateTimeOffset.UtcNow.AddHours(-2),
        });
        db.PgVectorEmbeddings.Add(new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = vectorDocId,
            GameId = gameId,
            Model = "bge-base-en-v1.5",
            ChunkIndex = 0,
            TextContent = "stub",
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return pdfId;
    }
}
```

> **IMPORTANT**: La nomenclatura esatta di `CustomWebApplicationFactory`, `SharedTestcontainersFixture`, `Integration-GroupA` collection deve essere validata con Grep prima di runnare — il pattern qui mostrato è derivato da `ExportDocumentChunksQueryHandlerIntegrationTests.cs`. Se le classi non esistono con questo nome esatto, sostituire con quelle del project (vedi `apps/api/tests/Api.Tests/Integration/Infrastructure/`).

- [ ] **Step 2: Add happy path test (admin session via test auth helper)**

```csharp
[Fact(Timeout = 30000)]
public async Task GET_Returns_200_For_Indexed_Doc()
{
    var pdfId = await SeedIndexedDocAsync();
    await TestAuthHelper.AuthenticateAsAdminAsync(_client!);

    var response = await _client!.GetAsync($"/api/v1/admin/kb/docs/{pdfId}/embeddings/meta", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var dto = await response.Content.ReadFromJsonAsync<DocumentEmbeddingsMetaDto>(cancellationToken: TestContext.Current.CancellationToken);
    dto.Should().NotBeNull();
    dto!.DocId.Should().Be(pdfId);
    dto.Model.Should().Be("bge-base-en-v1.5");
    dto.Dimensions.Should().Be(768);
    dto.TotalChunks.Should().Be(412);
    dto.Language.Should().Be("en");
}
```

> **TestAuthHelper.AuthenticateAsAdminAsync**: se non esiste, sostituire con il pattern di auth admin usato in `SearchDocumentChunksQueryHandlerIntegrationTests.cs` — probabilmente cookie injection diretta. Verificare con `Grep "AuthenticateAsAdmin" apps/api/tests/Api.Tests`.

- [ ] **Step 3: Add audit row assertion test**

```csharp
[Fact(Timeout = 30000)]
public async Task GET_Writes_Audit_Row_On_Success()
{
    var pdfId = await SeedIndexedDocAsync();
    var adminUserId = await TestAuthHelper.AuthenticateAsAdminAsync(_client!);

    var response = await _client!.GetAsync($"/api/v1/admin/kb/docs/{pdfId}/embeddings/meta", TestContext.Current.CancellationToken);
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    await using var scope = _factory!.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var auditRow = await db.AuditLogs
        .Where(a => a.Action == "EmbeddingsMetaView" && a.ResourceId == pdfId.ToString())
        .SingleOrDefaultAsync(TestContext.Current.CancellationToken);

    auditRow.Should().NotBeNull();
    auditRow!.Resource.Should().Be("Document");
    auditRow.ActorId.Should().Be(adminUserId);
    auditRow.Level.Should().Be(1);
}
```

> **Note**: `AuditLogs` DbSet name, `ActorId`/`ResourceId`/`Action`/`Resource`/`Level` field names variano; allineare con `audit_logs` entity esistente nel BC Administration (Grep `class AuditLogEntity` o `DbSet<AuditLogEntity>`).

- [ ] **Step 4: Run tests**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "GetDocumentEmbeddingsMetaIntegrationTests&Category=Integration"`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs
git commit -m "test(admin-kb): #1674 integration test happy path + audit row"
```

---

### Task C.3: Integration test — 404 / auth failures

**Files:**
- Modify: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs`

- [ ] **Step 1: Add tests**

```csharp
[Fact(Timeout = 30000)]
public async Task GET_Returns_404_When_Doc_Not_Indexed()
{
    var unknownDocId = Guid.NewGuid();
    await TestAuthHelper.AuthenticateAsAdminAsync(_client!);

    var response = await _client!.GetAsync($"/api/v1/admin/kb/docs/{unknownDocId}/embeddings/meta", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact(Timeout = 30000)]
public async Task GET_Returns_401_When_No_Session()
{
    var pdfId = await SeedIndexedDocAsync();
    // No auth

    var response = await _client!.GetAsync($"/api/v1/admin/kb/docs/{pdfId}/embeddings/meta", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact(Timeout = 30000)]
public async Task GET_Returns_403_When_Not_Admin()
{
    var pdfId = await SeedIndexedDocAsync();
    await TestAuthHelper.AuthenticateAsUserAsync(_client!); // non-admin

    var response = await _client!.GetAsync($"/api/v1/admin/kb/docs/{pdfId}/embeddings/meta", TestContext.Current.CancellationToken);

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 2: Run tests**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "GetDocumentEmbeddingsMetaIntegrationTests&Category=Integration"`
Expected: 5 passed (2 existing + 3 new).

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs
git commit -m "test(admin-kb): #1674 integration tests for 404 + 401 + 403"
```

---

### Task C.4: Integration test — DTO whitelist (no vector field leak)

**Files:**
- Modify: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs`

- [ ] **Step 1: Add security test**

```csharp
[Fact(Timeout = 30000)]
public async Task GET_Response_Json_Has_No_Vector_Field()
{
    var pdfId = await SeedIndexedDocAsync();
    await TestAuthHelper.AuthenticateAsAdminAsync(_client!);

    var response = await _client!.GetAsync($"/api/v1/admin/kb/docs/{pdfId}/embeddings/meta", TestContext.Current.CancellationToken);
    var rawJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    // Security guarantee from spec §7: zero raw vector exposure
    rawJson.Should().NotContain("\"vector\"", because: "DTO must not expose raw vector values");
    rawJson.Should().NotContain("\"embedding\"", because: "DTO must not expose embedding values");
    rawJson.Should().NotContain("\"coordinates\"", because: "DTO must not expose vector coordinates");
    rawJson.Should().NotContain("\"values\"", because: "DTO must not include arbitrary values arrays");

    // Whitelist check: only the 6 DTO fields are present
    rawJson.Should().Contain("\"docId\"");
    rawJson.Should().Contain("\"model\"");
    rawJson.Should().Contain("\"dimensions\"");
    rawJson.Should().Contain("\"totalChunks\"");
    rawJson.Should().Contain("\"indexedAt\"");
}
```

- [ ] **Step 2: Run test**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "GET_Response_Json_Has_No_Vector_Field"`
Expected: 1 passed.

- [ ] **Step 3: Run all integration tests for this file**

Run: `cd apps/api/tests/Api.Tests && dotnet test --filter "GetDocumentEmbeddingsMetaIntegrationTests"`
Expected: 6 passed.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/GetDocumentEmbeddingsMetaIntegrationTests.cs
git commit -m "test(admin-kb): #1674 security test — assert zero raw vector field in response JSON"
```

---

## Phase D — Frontend API Client + Schema

### Task D.1: Zod schema + TS type

**Files:**
- Create: `apps/web/src/lib/api/schemas/admin-kb-embeddings.schemas.ts`

- [ ] **Step 1: Create schema file**

```typescript
import { z } from 'zod';

export const DocumentEmbeddingsMetaDtoSchema = z.object({
  docId: z.string().uuid(),
  model: z.string().min(1),
  dimensions: z.number().int().positive(),
  totalChunks: z.number().int().nonnegative(),
  indexedAt: z.string().datetime({ offset: true }),
  language: z.string().nullable(),
});

export type DocumentEmbeddingsMetaDto = z.infer<typeof DocumentEmbeddingsMetaDtoSchema>;
```

- [ ] **Step 2: Verify TS compile**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/schemas/admin-kb-embeddings.schemas.ts
git commit -m "feat(admin-kb-fe): #1674 add Zod schema for DocumentEmbeddingsMetaDto"
```

---

### Task D.2: API client fetcher

**Files:**
- Create: `apps/web/src/lib/api/admin-kb-embeddings.ts`

- [ ] **Step 1: Create fetcher**

```typescript
import { apiClient } from '@/lib/api/client';
import {
  DocumentEmbeddingsMetaDtoSchema,
  type DocumentEmbeddingsMetaDto,
} from '@/lib/api/schemas/admin-kb-embeddings.schemas';

export async function getDocumentEmbeddingsMeta(
  docId: string,
  options?: { signal?: AbortSignal }
): Promise<DocumentEmbeddingsMetaDto | null> {
  return apiClient.get<DocumentEmbeddingsMetaDto>(
    `/api/v1/admin/kb/docs/${docId}/embeddings/meta`,
    DocumentEmbeddingsMetaDtoSchema,
    { signal: options?.signal }
  );
}
```

- [ ] **Step 2: Verify TS compile**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/admin-kb-embeddings.ts
git commit -m "feat(admin-kb-fe): #1674 add getDocumentEmbeddingsMeta fetcher"
```

---

## Phase E — Frontend Hook

### Task E.1: TanStack Query hook with TDD

**Files:**
- Create: `apps/web/src/hooks/admin/use-document-embeddings-meta.ts`
- Create: `apps/web/src/hooks/admin/__tests__/use-document-embeddings-meta.test.ts`

- [ ] **Step 1: Write failing tests**

```typescript
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { PropsWithChildren } from 'react';
import { documentEmbeddingsKeys, useDocumentEmbeddingsMeta } from '../use-document-embeddings-meta';

vi.mock('@/lib/api/admin-kb-embeddings', () => ({
  getDocumentEmbeddingsMeta: vi.fn(),
}));

import { getDocumentEmbeddingsMeta } from '@/lib/api/admin-kb-embeddings';

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: PropsWithChildren) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('useDocumentEmbeddingsMeta', () => {
  it('returns null query key parts when docId is null', () => {
    const keys = documentEmbeddingsKeys.meta('abc-123');
    expect(keys).toEqual(['admin', 'kb', 'docs', 'abc-123', 'embeddings', 'meta']);
  });

  it('does NOT fire fetch when enabled=false', async () => {
    const wrapper = makeWrapper();
    renderHook(() => useDocumentEmbeddingsMeta('abc-123', false), { wrapper });
    await new Promise(r => setTimeout(r, 50));
    expect(getDocumentEmbeddingsMeta).not.toHaveBeenCalled();
  });

  it('fires fetch with docId when enabled=true', async () => {
    vi.mocked(getDocumentEmbeddingsMeta).mockResolvedValue({
      docId: 'abc',
      model: 'bge-base-en-v1.5',
      dimensions: 768,
      totalChunks: 412,
      indexedAt: '2026-05-28T14:22:14Z',
      language: 'en',
    });
    const wrapper = makeWrapper();
    const { result } = renderHook(() => useDocumentEmbeddingsMeta('abc', true), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getDocumentEmbeddingsMeta).toHaveBeenCalledWith('abc', expect.any(Object));
    expect(result.current.data?.model).toBe('bge-base-en-v1.5');
  });
});
```

- [ ] **Step 2: Run test (should fail — hook missing)**

Run: `cd apps/web && pnpm test src/hooks/admin/__tests__/use-document-embeddings-meta.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement hook**

```typescript
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { getDocumentEmbeddingsMeta } from '@/lib/api/admin-kb-embeddings';
import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

export const documentEmbeddingsKeys = {
  all: ['admin', 'kb', 'docs'] as const,
  meta: (docId: string) =>
    ['admin', 'kb', 'docs', docId, 'embeddings', 'meta'] as const,
};

const STALE_TIME_MS = 5 * 60 * 1000;
const GC_TIME_MS = 10 * 60 * 1000;

export function useDocumentEmbeddingsMeta(
  docId: string | null,
  enabled: boolean
): UseQueryResult<DocumentEmbeddingsMetaDto | null, Error> {
  const isValid = typeof docId === 'string' && docId.length > 0;
  return useQuery<DocumentEmbeddingsMetaDto | null, Error>({
    queryKey: isValid ? documentEmbeddingsKeys.meta(docId) : [...documentEmbeddingsKeys.all, 'noop'],
    queryFn: ({ signal }) => getDocumentEmbeddingsMeta(docId!, { signal }),
    enabled: enabled && isValid,
    staleTime: STALE_TIME_MS,
    gcTime: GC_TIME_MS,
    retry: 1,
  });
}
```

- [ ] **Step 4: Run test (should pass)**

Run: `cd apps/web && pnpm test src/hooks/admin/__tests__/use-document-embeddings-meta.test.ts`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/admin/use-document-embeddings-meta.ts apps/web/src/hooks/admin/__tests__/use-document-embeddings-meta.test.ts
git commit -m "feat(admin-kb-fe): #1674 add useDocumentEmbeddingsMeta TanStack hook + tests"
```

---

## Phase F — Frontend Pure Components

### Task F.1: simple-hash util + tests

**Files:**
- Create: `apps/web/src/lib/util/simple-hash.ts`
- Create: `apps/web/src/lib/util/__tests__/simple-hash.test.ts`

- [ ] **Step 1: Write failing test**

```typescript
import { describe, expect, it } from 'vitest';
import { simpleHash } from '../simple-hash';

describe('simpleHash', () => {
  it('returns same hash for same input (deterministic)', () => {
    expect(simpleHash('chunk-42')).toBe(simpleHash('chunk-42'));
  });

  it('returns different hashes for different inputs', () => {
    expect(simpleHash('chunk-1')).not.toBe(simpleHash('chunk-2'));
  });
});
```

- [ ] **Step 2: Run (should fail — module missing)**

Run: `cd apps/web && pnpm test src/lib/util/__tests__/simple-hash.test.ts`
Expected: FAIL.

- [ ] **Step 3: Implement (cyrb53 inline)**

```typescript
/**
 * cyrb53 hash function — deterministic, fast, good distribution.
 * Used by VecThumb to derive stable gradient hues from chunkIndex.
 * https://stackoverflow.com/a/52171480
 */
export function simpleHash(input: string, seed = 0): number {
  let h1 = 0xdeadbeef ^ seed;
  let h2 = 0x41c6ce57 ^ seed;
  for (let i = 0, ch; i < input.length; i++) {
    ch = input.charCodeAt(i);
    h1 = Math.imul(h1 ^ ch, 2654435761);
    h2 = Math.imul(h2 ^ ch, 1597334677);
  }
  h1 = Math.imul(h1 ^ (h1 >>> 16), 2246822507);
  h1 ^= Math.imul(h2 ^ (h2 >>> 13), 3266489909);
  h2 = Math.imul(h2 ^ (h2 >>> 16), 2246822507);
  h2 ^= Math.imul(h1 ^ (h1 >>> 13), 3266489909);
  return 4294967296 * (2097151 & h2) + (h1 >>> 0);
}
```

- [ ] **Step 4: Run (should pass)**

Run: `cd apps/web && pnpm test src/lib/util/__tests__/simple-hash.test.ts`
Expected: 2 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/util/simple-hash.ts apps/web/src/lib/util/__tests__/simple-hash.test.ts
git commit -m "feat(util): #1674 add simpleHash (cyrb53) for VecThumb seed"
```

---

### Task F.2: VecThumb component + test

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/vec-thumb.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/vec-thumb.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { VecThumb } from '../vec-thumb';

describe('VecThumb', () => {
  it('renders deterministic gradient for same seed', () => {
    const { container: a } = render(<VecThumb seed="chunk-42" />);
    const { container: b } = render(<VecThumb seed="chunk-42" />);
    const aBg = a.firstElementChild?.getAttribute('style') ?? '';
    const bBg = b.firstElementChild?.getAttribute('style') ?? '';
    expect(aBg).toBe(bBg);
    expect(aBg).toContain('linear-gradient');
  });

  it('renders "768d · float32" label', () => {
    const { getByText } = render(<VecThumb seed="any" />);
    expect(getByText(/768d · float32/)).toBeInTheDocument();
  });

  it('marks itself aria-hidden (decorative)', () => {
    const { container } = render(<VecThumb seed="any" />);
    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true');
  });
});
```

- [ ] **Step 2: Run (should fail — module missing)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/vec-thumb.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement**

```tsx
import { simpleHash } from '@/lib/util/simple-hash';

export interface VecThumbProps {
  seed: number | string;
}

export function VecThumb({ seed }: VecThumbProps): JSX.Element {
  const hash = simpleHash(String(seed));
  const hue1 = hash % 360;
  const hue2 = (hash * 7) % 360;
  const hue3 = (hash * 13) % 360;

  return (
    <div
      className="relative mt-1.5 h-7 overflow-hidden rounded-md"
      style={{
        background: `linear-gradient(90deg, hsl(${hue1} 60% 50% / .35), hsl(${hue2} 60% 50% / .05), hsl(${hue3} 60% 50% / .25))`,
      }}
      aria-hidden="true"
    >
      <span className="absolute right-1.5 top-1/2 -translate-y-1/2 font-mono text-[9px] font-bold opacity-75">
        768d · float32
      </span>
    </div>
  );
}
```

- [ ] **Step 4: Run (should pass)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/vec-thumb.test.tsx`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/vec-thumb.tsx apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/vec-thumb.test.tsx
git commit -m "feat(admin-kb-fe): #1674 add VecThumb deterministic gradient component"
```

---

### Task F.3: EmbeddingsMetaStrip component + tests

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-meta-strip.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-meta-strip.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmbeddingsMetaStrip } from '../embeddings-meta-strip';
import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

const fixture: DocumentEmbeddingsMetaDto = {
  docId: 'abc',
  model: 'bge-base-en-v1.5',
  dimensions: 768,
  totalChunks: 412,
  indexedAt: '2026-05-28T14:22:14Z',
  language: 'en',
};

describe('EmbeddingsMetaStrip', () => {
  it('renders 4 KPI cards on success', () => {
    render(<EmbeddingsMetaStrip state={{ status: 'success', data: fixture }} />);
    expect(screen.getByText('bge-base-en-v1.5')).toBeInTheDocument();
    expect(screen.getByText('768')).toBeInTheDocument();
    expect(screen.getByText('412')).toBeInTheDocument();
    expect(screen.getByText(/Indexed at/i)).toBeInTheDocument();
  });

  it('renders 4 skeletons while loading', () => {
    const { container } = render(<EmbeddingsMetaStrip state={{ status: 'loading' }} />);
    expect(container.querySelectorAll('[data-testid="meta-skeleton"]')).toHaveLength(4);
  });

  it('renders EmptyState when 404 not-indexed', () => {
    render(<EmbeddingsMetaStrip state={{ status: 'not-indexed' }} />);
    expect(screen.getByText(/Documento non indicizzato/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run (should fail)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-meta-strip.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement**

```tsx
import type { DocumentEmbeddingsMetaDto } from '@/lib/api/schemas/admin-kb-embeddings.schemas';

export type EmbeddingsMetaState =
  | { status: 'loading' }
  | { status: 'success'; data: DocumentEmbeddingsMetaDto }
  | { status: 'not-indexed' }
  | { status: 'error'; message: string };

export interface EmbeddingsMetaStripProps {
  state: EmbeddingsMetaState;
}

export function EmbeddingsMetaStrip({ state }: EmbeddingsMetaStripProps): JSX.Element {
  if (state.status === 'loading') {
    return (
      <div className="grid grid-cols-4 gap-3" role="status" aria-label="Caricamento metadati embeddings">
        {[0, 1, 2, 3].map(i => (
          <div
            key={i}
            data-testid="meta-skeleton"
            className="h-[88px] animate-pulse rounded-lg border border-border bg-muted"
          />
        ))}
      </div>
    );
  }

  if (state.status === 'not-indexed') {
    return (
      <div className="rounded-lg border border-border bg-muted p-6 text-center">
        <p className="text-sm font-semibold text-foreground">Documento non indicizzato</p>
        <p className="mt-1 text-xs text-muted-foreground">
          Esegui re-index dal pannello principale per generare gli embeddings.
        </p>
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-foreground">
        Impossibile caricare metadati embeddings. {state.message}
      </div>
    );
  }

  const { data } = state;
  const indexed = new Date(data.indexedAt);
  return (
    <div className="grid grid-cols-4 gap-3">
      <KpiCard label="Model" value={data.model} />
      <KpiCard label="Dimensions" value={String(data.dimensions)} unit="d" />
      <KpiCard label="Total chunks" value={data.totalChunks.toLocaleString('it-IT')} />
      <KpiCard label="Indexed at" value={indexed.toLocaleString('it-IT', { dateStyle: 'short', timeStyle: 'short' })} />
    </div>
  );
}

function KpiCard({ label, value, unit }: { label: string; value: string; unit?: string }): JSX.Element {
  return (
    <div className="flex min-h-[88px] flex-col gap-1 rounded-lg border-l-[3px] border-l-entity-kb border border-border bg-card p-3 ring-entity-kb/30">
      <span className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
        {label}
      </span>
      <span className="text-xl font-extrabold leading-tight text-foreground">
        {value}
        {unit ? <span className="ml-0.5 text-xs font-bold text-muted-foreground">{unit}</span> : null}
      </span>
    </div>
  );
}
```

- [ ] **Step 4: Run (should pass)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-meta-strip.test.tsx`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-meta-strip.tsx apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-meta-strip.test.tsx
git commit -m "feat(admin-kb-fe): #1674 add EmbeddingsMetaStrip (4 KPI + skeleton + empty + error)"
```

---

### Task F.4: EmbeddingsResultRow component + test

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-result-row.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-result-row.test.tsx`

- [ ] **Step 1: Write failing test**

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { EmbeddingsResultRow, type ScoredChunkDto } from '../embeddings-result-row';

const fixture: ScoredChunkDto = {
  chunkIndex: 218,
  page: 22,
  snippet: 'predator activation order',
  score: 0.912,
  vectorDocumentId: 'vd-1',
  language: 'en',
};

describe('EmbeddingsResultRow', () => {
  it('expands and shows VecThumb on click', () => {
    render(<EmbeddingsResultRow chunk={fixture} />);
    expect(screen.queryByText(/768d · float32/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /expand|espandi/i }));
    expect(screen.getByText(/768d · float32/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run (should fail)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-result-row.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement**

```tsx
import { useState } from 'react';
import { VecThumb } from './vec-thumb';

export interface ScoredChunkDto {
  chunkIndex: number;
  page: number;
  snippet: string;
  score: number;
  vectorDocumentId: string;
  language: string | null;
}

export interface EmbeddingsResultRowProps {
  chunk: ScoredChunkDto;
}

function scoreClass(score: number): string {
  if (score >= 0.75) return 'text-entity-kb';
  if (score >= 0.5) return 'text-amber-500';
  return 'text-muted-foreground';
}

export function EmbeddingsResultRow({ chunk }: EmbeddingsResultRowProps): JSX.Element {
  const [expanded, setExpanded] = useState(false);
  return (
    <div className={expanded ? 'bg-muted' : ''}>
      <button
        type="button"
        onClick={() => setExpanded(p => !p)}
        aria-expanded={expanded}
        aria-label={expanded ? 'Collapse row' : 'Espandi riga'}
        className="grid w-full grid-cols-[60px_60px_1fr_70px_30px] items-center gap-3 border-b border-border px-3 py-2 text-left text-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        <span className="font-mono text-[10px] text-muted-foreground">p.{chunk.page}</span>
        <span className="font-mono text-[10px] text-muted-foreground">#{chunk.chunkIndex}</span>
        <span className="truncate text-muted-foreground">{chunk.snippet}</span>
        <span className={`text-right font-mono font-bold ${scoreClass(chunk.score)}`}>{chunk.score.toFixed(3)}</span>
        <span className="font-mono text-xs text-muted-foreground">{expanded ? '▾' : '›'}</span>
      </button>
      {expanded ? (
        <div className="border-b border-border bg-card px-3 pb-3 pt-2">
          <p className="mb-2 text-xs text-foreground">{chunk.snippet}</p>
          <VecThumb seed={chunk.chunkIndex} />
        </div>
      ) : null}
    </div>
  );
}
```

- [ ] **Step 4: Run (should pass)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-result-row.test.tsx`
Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-result-row.tsx apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/embeddings-result-row.test.tsx
git commit -m "feat(admin-kb-fe): #1674 add EmbeddingsResultRow collapsed/expanded with VecThumb"
```

---

## Phase G — SearchPanel + Drawer Assembly

### Task G.1: EmbeddingsSearchPanel component

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-search-panel.tsx`

> **Pre-step**: Verify se `useSearchDocumentChunks(docId)` hook esiste già (da FU-4 #1653). Run: `Grep "useSearchDocumentChunks" apps/web/src/hooks`. Se SI: import path da quel file. Se NO: spec dice "esistente da FU-4" — se in realtà l'hook FE non è stato creato in FU-4, la PR è scope-creep e va aggiunta come task G.0 (skippato qui assumendo presenza).

- [ ] **Step 1: Implement (no separate test — coperto da drawer integration test)**

```tsx
'use client';

import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/primitives/button';
import { useSearchDocumentChunks } from '@/hooks/admin/use-search-document-chunks';
import { EmbeddingsResultRow, type ScoredChunkDto } from './embeddings-result-row';

export interface EmbeddingsSearchPanelProps {
  docId: string;
}

const MAX_QUERY_LENGTH = 1000;

export function EmbeddingsSearchPanel({ docId }: EmbeddingsSearchPanelProps): JSX.Element {
  const [query, setQuery] = useState('');
  const [limit, setLimit] = useState(10);
  const search = useSearchDocumentChunks(docId);

  const queryTooLong = query.length > MAX_QUERY_LENGTH;
  const canSubmit = query.trim().length > 0 && !queryTooLong;

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    search.mutate({ query: query.trim(), limit });
  };

  return (
    <section className="mt-6">
      <h3 className="mb-3 font-display text-sm font-extrabold">🔬 Ricerca semantica</h3>

      <form onSubmit={onSubmit} className="grid grid-cols-[1fr_120px_auto] gap-2">
        <input
          type="text"
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Cerca nei chunk del documento…"
          aria-label="Query semantica"
          aria-invalid={queryTooLong || undefined}
          aria-describedby={queryTooLong ? 'query-too-long' : undefined}
          className="rounded-md border border-border bg-card px-3 py-1.5 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
        <select
          value={limit}
          onChange={e => setLimit(parseInt(e.target.value, 10))}
          aria-label="Limit"
          className="rounded-md border border-border bg-card px-3 py-1.5 text-sm"
        >
          <option value={5}>limit 5</option>
          <option value={10}>limit 10</option>
          <option value={20}>limit 20</option>
        </select>
        <Button type="submit" disabled={!canSubmit || search.isPending}>
          {search.isPending ? 'Cerca…' : 'Cerca'}
        </Button>
      </form>

      {queryTooLong ? (
        <p id="query-too-long" className="mt-1 text-xs text-destructive">
          Query troppo lunga (max {MAX_QUERY_LENGTH} caratteri)
        </p>
      ) : null}

      <div className="mt-4 rounded-md border border-border">
        {search.isPending ? (
          <div className="space-y-1 p-2" data-testid="search-skeleton">
            {[0, 1, 2, 3, 4].map(i => (
              <div key={i} className="h-8 animate-pulse rounded bg-muted" />
            ))}
          </div>
        ) : search.isError ? (
          <p className="p-4 text-sm text-destructive">Errore ricerca: {search.error.message}</p>
        ) : search.data && Array.isArray(search.data) ? (
          search.data.length === 0 ? (
            <p className="p-4 text-sm text-muted-foreground" role="status">
              Nessun chunk corrisponde a «{query}»
            </p>
          ) : (
            <>
              <p className="border-b border-border px-3 py-2 text-xs text-muted-foreground" aria-live="polite">
                {search.data.length} risultati trovati
              </p>
              {(search.data as ScoredChunkDto[]).map(chunk => (
                <EmbeddingsResultRow key={`${chunk.vectorDocumentId}-${chunk.chunkIndex}`} chunk={chunk} />
              ))}
            </>
          )
        ) : (
          <p className="p-4 text-sm text-muted-foreground">Digita una query e clicca Cerca per iniziare.</p>
        )}
      </div>
    </section>
  );
}
```

- [ ] **Step 2: Verify TS compile**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors. Se `useSearchDocumentChunks` non esiste, vedi pre-step.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/embeddings-search-panel.tsx
git commit -m "feat(admin-kb-fe): #1674 add EmbeddingsSearchPanel (form + results + states)"
```

---

### Task G.2: DocumentEmbeddingsDrawer orchestrator + index export

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/document-embeddings-drawer.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/index.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/document-embeddings-drawer.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DocumentEmbeddingsDrawer } from '../document-embeddings-drawer';

vi.mock('@/lib/api/admin-kb-embeddings', () => ({
  getDocumentEmbeddingsMeta: vi.fn().mockResolvedValue({
    docId: 'abc',
    model: 'bge-base-en-v1.5',
    dimensions: 768,
    totalChunks: 412,
    indexedAt: '2026-05-28T14:22:14Z',
    language: 'en',
  }),
}));
vi.mock('@/hooks/admin/use-search-document-chunks', () => ({
  useSearchDocumentChunks: () => ({ mutate: vi.fn(), isPending: false, isError: false, data: null }),
}));

function wrap(children: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('DocumentEmbeddingsDrawer', () => {
  it('renders nothing when docId is null', () => {
    const { container } = render(wrap(
      <DocumentEmbeddingsDrawer open onOpenChange={() => {}} docId={null} docFileName={null} />
    ));
    expect(container.firstChild).toBeNull();
  });

  it('renders dialog with title when open', async () => {
    render(wrap(
      <DocumentEmbeddingsDrawer open onOpenChange={() => {}} docId="abc" docFileName="Wingspan.pdf" />
    ));
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText(/Embeddings · Wingspan\.pdf/)).toBeInTheDocument();
  });

  it('renders MetaStrip when meta fetch succeeds', async () => {
    render(wrap(
      <DocumentEmbeddingsDrawer open onOpenChange={() => {}} docId="abc" docFileName="Wingspan.pdf" />
    ));
    await waitFor(() => expect(screen.getByText('bge-base-en-v1.5')).toBeInTheDocument());
    expect(screen.getByText('768')).toBeInTheDocument();
    expect(screen.getByText('412')).toBeInTheDocument();
  });

  it('export link points to correct endpoint', async () => {
    render(wrap(
      <DocumentEmbeddingsDrawer open onOpenChange={() => {}} docId="abc" docFileName="Wingspan.pdf" />
    ));
    const link = await screen.findByRole('link', { name: /Export chunks JSON/i });
    expect(link).toHaveAttribute('href', '/api/v1/admin/kb/docs/abc/chunks/export');
  });

  it('calls onOpenChange(false) on Escape key', async () => {
    const onOpenChange = vi.fn();
    render(wrap(
      <DocumentEmbeddingsDrawer open onOpenChange={onOpenChange} docId="abc" docFileName="Wingspan.pdf" />
    ));
    fireEvent.keyDown(document, { key: 'Escape' });
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
  });
});
```

- [ ] **Step 2: Run (should fail — module missing)**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/document-embeddings-drawer.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement drawer**

```tsx
'use client';

import { isAxiosError } from 'axios';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useDocumentEmbeddingsMeta } from '@/hooks/admin/use-document-embeddings-meta';
import { EmbeddingsMetaStrip, type EmbeddingsMetaState } from './embeddings-meta-strip';
import { EmbeddingsSearchPanel } from './embeddings-search-panel';

export interface DocumentEmbeddingsDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  docId: string | null;
  docFileName: string | null;
}

export function DocumentEmbeddingsDrawer({
  open,
  onOpenChange,
  docId,
  docFileName,
}: DocumentEmbeddingsDrawerProps): JSX.Element | null {
  const metaQuery = useDocumentEmbeddingsMeta(docId, open);

  if (!docId || !docFileName) {
    return null;
  }

  const exportHref = `/api/v1/admin/kb/docs/${docId}/chunks/export`;

  const metaState: EmbeddingsMetaState = metaQuery.isPending
    ? { status: 'loading' }
    : metaQuery.isError
      ? is404(metaQuery.error)
        ? { status: 'not-indexed' }
        : { status: 'error', message: metaQuery.error.message }
      : metaQuery.data
        ? { status: 'success', data: metaQuery.data }
        : { status: 'loading' };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="right"
        className="flex w-[720px] flex-col gap-0 p-0 sm:max-w-[720px]"
      >
        <SheetHeader className="border-b border-border px-6 py-4">
          <SheetTitle className="text-base font-semibold">
            Embeddings · {docFileName}
          </SheetTitle>
        </SheetHeader>

        <div className="flex-1 space-y-4 overflow-y-auto px-6 py-4">
          <EmbeddingsMetaStrip state={metaState} />
          {metaState.status === 'success' ? <EmbeddingsSearchPanel docId={docId} /> : null}
        </div>

        <div className="flex justify-between gap-3 border-t border-border px-6 py-4">
          <Button asChild variant="outline" disabled={metaState.status !== 'success'}>
            <a href={exportHref} download={`${docId}-chunks.json`}>
              ⤓ Export chunks JSON
            </a>
          </Button>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Chiudi
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}

function is404(error: unknown): boolean {
  if (isAxiosError(error)) return error.response?.status === 404;
  if (error instanceof Error && /404|not.?found/i.test(error.message)) return true;
  return false;
}
```

- [ ] **Step 4: Create index re-export**

```tsx
export { DocumentEmbeddingsDrawer, type DocumentEmbeddingsDrawerProps } from './document-embeddings-drawer';
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm test src/components/admin/knowledge-base/document-embeddings-drawer/__tests__/document-embeddings-drawer.test.tsx`
Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/document-embeddings-drawer/
git commit -m "feat(admin-kb-fe): #1674 add DocumentEmbeddingsDrawer + 5 component tests"
```

---

## Phase H — Wire-up Trigger + Invalidation

### Task H.1: Wire-up "📋 View embeddings" button in KbDocDetailPanel

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`

> **Pre-step**: identifica la riga `hero-actions` esistente. Run: `Grep "Re-index\|reindex\|⟳" apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`. Aggiungi il bottone tra Re-index e Export.

- [ ] **Step 1: Add state lifted + import**

In cima al file aggiungi:
```tsx
import { useState } from 'react';
import { DocumentEmbeddingsDrawer } from '../document-embeddings-drawer';
```

Dentro il component, all'inizio:
```tsx
const [openEmbeddingsForDocId, setOpenEmbeddingsForDocId] = useState<string | null>(null);
```

- [ ] **Step 2: Add trigger button + drawer mount**

Nella riga hero-actions, inserisci tra Re-index e Export (o in posizione equivalente al mockup `sp5-admin-kb.html:236`):
```tsx
<button
  type="button"
  onClick={() => setOpenEmbeddingsForDocId(doc.id)}
  disabled={!doc.id || doc.processingStatus !== 'ready'}
  className="btn-admin"
  aria-label="Mostra embeddings del documento"
>
  📋 View embeddings
</button>
```

In fondo al JSX del component:
```tsx
<DocumentEmbeddingsDrawer
  open={openEmbeddingsForDocId !== null}
  onOpenChange={(o) => { if (!o) setOpenEmbeddingsForDocId(null); }}
  docId={openEmbeddingsForDocId}
  docFileName={openEmbeddingsForDocId && doc?.fileName ? doc.fileName : null}
/>
```

> **Verify field names**: `doc.id`, `doc.processingStatus`, `doc.fileName` devono matchare il DTO `KbDocEnvelope` esistente. Se nomi differiscono, aggiustare (Grep `interface KbDocDetail\|KbDocEnvelope` per scoprire).

- [ ] **Step 3: Run typecheck + existing KB tests**

```bash
cd apps/web && pnpm typecheck
pnpm test src/components/admin/knowledge-base/explorer
```
Expected: typecheck OK; existing KbDocDetailPanel tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx
git commit -m "feat(admin-kb-fe): #1674 wire-up View embeddings button in KbDocDetailPanel"
```

---

### Task H.2: Wire-up cache invalidation on reindex

**Files:**
- Modify: file che ospita la mutation `reindexDocument` (probabile `apps/web/src/hooks/admin/use-reindex-document.ts` o pattern equivalente — Grep prima)

> **Pre-step**: `Grep "reindexDocument\|ReindexDocument\|reindexMutation" apps/web/src/hooks/admin/`. Trova il file che ha `onSuccess` callback della mutation.

- [ ] **Step 1: Add invalidation in onSuccess**

Nel file della mutation, dentro `onSuccess`, aggiungi:
```typescript
import { documentEmbeddingsKeys } from '@/hooks/admin/use-document-embeddings-meta';

// dentro onSuccess di useReindexDocument mutation:
queryClient.invalidateQueries({ queryKey: documentEmbeddingsKeys.meta(docId) });
```

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/admin/use-reindex-document.ts
git commit -m "feat(admin-kb-fe): #1674 invalidate embeddings meta cache on reindex success"
```

---

## Phase I — E2E + Final Validation

### Task I.1: Playwright E2E smoke specs

**Files:**
- Create: `apps/web/e2e/admin-kb-embeddings-viewer.spec.ts`

- [ ] **Step 1: Write all 4 spec at once (E2E generally not TDD; smoke validation)**

```typescript
import { test, expect } from '@playwright/test';
import { loginAsAdmin } from './fixtures/auth';
import AxeBuilder from '@axe-core/playwright';

test.describe('Admin KB · Per-doc embeddings viewer (#1674)', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/knowledge-base');
  });

  test('EM-01 happy path: open drawer + meta strip + search', async ({ page }) => {
    // Select first indexed doc in tree
    await page.getByRole('treeitem', { name: /Wingspan/i }).first().click();
    await page.getByRole('button', { name: /View embeddings/i }).click();

    const drawer = page.getByRole('dialog');
    await expect(drawer).toBeVisible();
    await expect(drawer.getByText(/bge-base-en/i)).toBeVisible();
    await expect(drawer.getByText('768')).toBeVisible();

    await drawer.getByLabel('Query semantica').fill('predator activation');
    await drawer.getByRole('button', { name: 'Cerca' }).click();

    await expect(drawer.getByText(/risultati trovati/i)).toBeVisible({ timeout: 10000 });
  });

  test('EM-02 not-indexed doc: shows empty state', async ({ page }) => {
    await page.getByRole('treeitem', { name: /Failed Doc/i }).first().click();
    await page.getByRole('button', { name: /View embeddings/i }).click();

    const drawer = page.getByRole('dialog');
    await expect(drawer.getByText(/Documento non indicizzato/i)).toBeVisible();
  });

  test('EM-03 export: download trigger', async ({ page }) => {
    await page.getByRole('treeitem', { name: /Wingspan/i }).first().click();
    await page.getByRole('button', { name: /View embeddings/i }).click();

    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('link', { name: /Export chunks JSON/i }).click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/-chunks\.json$/);
  });

  test('EM-04 a11y axe: no violations on drawer', async ({ page }) => {
    await page.getByRole('treeitem', { name: /Wingspan/i }).first().click();
    await page.getByRole('button', { name: /View embeddings/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .include('[role="dialog"]')
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
```

> **Note**: `loginAsAdmin` helper path varia; allineare con altri spec admin in `apps/web/e2e/` (Grep `loginAsAdmin` per percorso esatto). Se non esiste, usare un fixture file/helper esistente per setup admin session.

- [ ] **Step 2: Run E2E suite**

```bash
cd apps/web && pnpm test:e2e admin-kb-embeddings-viewer
```
Expected: 4 passed (assumendo BE + FE entrambi avviati via fixture/docker).

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin-kb-embeddings-viewer.spec.ts
git commit -m "test(e2e): #1674 add 4 admin-kb-embeddings-viewer specs (happy + 404 + export + a11y)"
```

---

### Task I.2: Final full-suite validation + lint

**Files:** (no edits — validation only)

- [ ] **Step 1: Run BE full test suite for KnowledgeBase**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "BoundedContext=KnowledgeBase"
```
Expected: all KB tests pass (previous baseline + ~12 new for #1674).

- [ ] **Step 2: Run FE unit + lint + typecheck**

```bash
cd apps/web
pnpm test
pnpm lint
pnpm typecheck
```
Expected: all tests pass, 0 lint errors, 0 type errors.

- [ ] **Step 3: Run lint:tokens specifically (check hardcoded colors)**

```bash
cd apps/web && pnpm lint:tokens
```
Expected: 0 violations (token-only, entity-kb utilities used).

- [ ] **Step 4: Verify clean git status**

```bash
git status
```
Expected: clean working tree, all commits made.

- [ ] **Step 5: No commit needed (validation pass)**

If any failure → fix root cause inline before proceeding.

---

### Task I.3: Push branch + open PR

**Files:** (no code edits)

- [ ] **Step 1: Verify parent branch**

```bash
git config branch.feature/issue-1674-embeddings-viewer.parent
```
Expected: `main-dev`

- [ ] **Step 2: Push to remote with upstream tracking**

```bash
git push -u origin feature/issue-1674-embeddings-viewer
```

- [ ] **Step 3: Open PR to main-dev**

```bash
gh pr create --base main-dev --title "feat(admin-kb): #1674 — Per-doc embeddings viewer" --body "$(cat <<'EOF'
Closes #1674

## Summary

- BE: `GET /api/v1/admin/kb/docs/{docId}/embeddings/meta` (NEW endpoint) with `[AuditableAction("EmbeddingsMetaView","Document",Level=1)]`
- FE: `DocumentEmbeddingsDrawer` (side-right Sheet 720px) with `EmbeddingsMetaStrip` (4 KPI) + `EmbeddingsSearchPanel` (riusa `useSearchDocumentChunks` from FU-4) + `VecThumb` (gradient client-side deterministic, ZERO raw vector exposure) + Export footer (riusa `/chunks/export` from FU-4)
- Wire-up button "📋 View embeddings" in `KbDocDetailPanel` hero-actions
- Cache invalidation: `reindexMutation.onSuccess` invalida `documentEmbeddingsKeys.meta(docId)`

## Newman risk mitigation (spec §7)

- DTO whitelist: 6 fields (docId, model, dimensions, totalChunks, indexedAt, language) — no Vector field
- VecThumb gradient: client-side from `seed=hash(chunkIdx)`, pure visual, zero info leak
- Security integration test asserts JSON response contiene 0 occorrenze di "vector|embedding|coordinates|values"

## Test plan

- [x] BE unit tests (handler + validator + audit attribute): 6 tests
- [x] BE integration tests (200/404/401/403 + audit row + DTO whitelist): 6 tests
- [x] FE unit tests (drawer + meta-strip + result-row + vec-thumb + hook): ~13 tests
- [x] FE hook tests (enabled flag, query key, fetch): 3 tests
- [x] E2E Playwright (happy + not-indexed + export + a11y axe): 4 specs

## Spec & references

- Design spec: `docs/superpowers/specs/2026-06-05-per-doc-embeddings-viewer-design.md`
- Plan: `docs/superpowers/plans/2026-06-05-per-doc-embeddings-viewer.md`
- Parent: #1653 FU-4 (PR #1649 merged 2026-05-28) — spin-out per Newman corpus-reconstruction risk

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
Expected: PR URL returned.

- [ ] **Step 4: Verify CI green (wait for required checks)**

Check PR status on GitHub. Required checks: BE tests, FE unit, FE typecheck, FE lint, E2E if blocking.

If any failure → debug locally, push fix commit, re-verify.

---

## Self-Review Checklist (run after writing plan, fix inline)

### 1. Spec coverage

| Spec section | Task(s) | Status |
|---|---|---|
| §5 NEW Query+Handler+Validator+DTO+Endpoint | A.1, A.2, A.3, B.1-B.5, C.1 | ✅ |
| §5 Resolver pattern (VectorDoc → Model) | B.1 (handler impl) | ✅ |
| §5 FE drawer + 5 sub-components | F.2-F.4, G.1, G.2 | ✅ |
| §5 useDocumentEmbeddingsMeta hook | E.1 | ✅ |
| §5 api.admin.kb.getDocumentEmbeddingsMeta fetcher | D.2 | ✅ |
| §5 Wire-up KbDocDetailPanel | H.1 | ✅ |
| §6 GET endpoint contract (200/400/401/403/404/500) | C.1 (endpoint), C.2-C.3 (integration tests) | ✅ |
| §6 AuditableAction Level 1 | A.2 (attribute), B.4 (presence test), C.2 (audit row test) | ✅ |
| §7 Zero raw vector leak | C.4 (DTO whitelist test) | ✅ |
| §7 VecThumb client-side | F.2 | ✅ |
| §8 UI components | F.2-F.4, G.1, G.2 | ✅ |
| §8 Accessibility (focus trap, ARIA, Escape) | G.2 (drawer test), I.1 (axe E2E) | ✅ |
| §9 Error handling (loading, 404, 401, 403, empty, error) | F.3 (meta states), G.1 (search states), C.2-C.3 (BE) | ✅ |
| §9 Cache invalidation on reindex | H.2 | ✅ |
| §10 Test count ~35 | BE: 6 unit + 6 integration = 12; FE: 1 hash + 3 hook + 3 meta + 1 row + 3 vec-thumb + 5 drawer = 16; E2E 4 → total 32 ✅ matches target |

### 2. Placeholder scan

- ❌ No "TBD/TODO" — all steps have concrete content
- ⚠️ Several "Verify..." sub-pre-steps require runtime Grep validation (DbSet names, hook paths, AuthHelper names). These are not placeholders but **runtime preconditions** explicit. Acceptable per skill guidance ("assume engineer has zero context").

### 3. Type consistency

- `documentEmbeddingsKeys.meta(docId)` — used identically in E.1 (hook), H.2 (invalidation), G.2 (referenced indirectly through hook)
- `DocumentEmbeddingsMetaDto` — fields `docId, model, dimensions, totalChunks, indexedAt, language` used identically in A.1, D.1, D.2, E.1, F.3, G.2, C.2, C.4 ✅
- `ScoredChunkDto` — fields `chunkIndex, page, snippet, score, vectorDocumentId, language` used in F.4, G.1 ✅
- `EmbeddingsMetaState` discriminated union — `loading | success | not-indexed | error` used in F.3, G.2 ✅
- Endpoint URL `/api/v1/admin/kb/docs/{docId}/embeddings/meta` — used in D.2, C.2, C.3, C.4, E.1 (via fetcher), G.2 (via hook) ✅
- Export endpoint URL `/api/v1/admin/kb/docs/{docId}/chunks/export` — used in G.2 (export link), I.1 (E2E) ✅

### 4. Critical assumptions to verify before execution

These are documented inline as "**Pre-step**" / "**Verify**" but listed here for the executor:

- [ ] `MeepleAiDbContext.PgVectorEmbeddings` DbSet name (Grep `DbSet<PgVectorEmbeddingEntity>`)
- [ ] `MeepleAiDbContext.AuditLogs` DbSet name (or equivalent)
- [ ] `SharedTestcontainersFixture` + `CustomWebApplicationFactory` + `Integration-GroupA` exact names
- [ ] `TestAuthHelper.AuthenticateAsAdminAsync` exists (or substitute pattern)
- [ ] `useSearchDocumentChunks(docId)` FE hook exists (from #1653 FU-4). If NOT → add Task G.0 to create it.
- [ ] `KbDocDetailPanel.tsx` field names: `doc.id`, `doc.processingStatus`, `doc.fileName`
- [ ] `loginAsAdmin` Playwright helper path

If any of these don't match, the executor must adapt code and re-run tests before commit.

---

## Plan complete. Execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**

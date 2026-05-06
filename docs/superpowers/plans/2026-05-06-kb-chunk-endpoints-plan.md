# KB Chunk-Level Retrieval Endpoints — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement 5 backend endpoints (4 queries + 1 search) for chunk-level retrieval to unblock V2 migration Wave 3 child #684 (`/kb/[id]` split-view).

**Architecture:** Extend `TextChunkEntity` with hierarchy fields (`heading`, `parent_chunk_id`, `level`, `element_type`), expose chunk metadata via 4 MediatR query handlers + 1 search handler with PostgreSQL FTS, gate admin-only fields per-DTO via `[JsonIgnore(WhenWritingNull)]`. Frontend gets Zod schemas + React Query hooks.

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs · MediatR · EF Core 9 (PostgreSQL 16 + pgvector) · xUnit + FluentAssertions + NSubstitute · Next.js 16 · React Query · Zod

**Spec:** `docs/superpowers/specs/2026-05-06-kb-chunk-endpoints-design.md`

---

## File Structure

### Backend — Created

| File | Responsibility |
|------|----------------|
| `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddChunkHierarchyToTextChunkEntity.cs` | EF Core migration adding 4 columns + 2 indexes |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/GetKbDocumentByIdQuery.cs` | Query record |
| `…/GetKbDocumentById/GetKbDocumentByIdHandler.cs` | Handler (G4) |
| `…/GetKbDocumentById/KbDocumentDto.cs` | DTO |
| `…/GetKbChunks/GetKbChunksQuery.cs` | Query record + paging params |
| `…/GetKbChunks/GetKbChunksHandler.cs` | Handler (G1) with recursive CTE for headingPath |
| `…/GetKbChunks/KbChunkSummaryDto.cs` | DTO with admin-only fields nullable |
| `…/GetKbChunks/KbChunkListDto.cs` | Wrapper DTO with pagination meta |
| `…/GetKbChunkById/GetKbChunkByIdQuery.cs` | Query record |
| `…/GetKbChunkById/GetKbChunkByIdHandler.cs` | Handler (G2) with prev/next |
| `…/GetKbChunkById/KbChunkDetailDto.cs` | DTO with full content + admin fields |
| `…/SearchKbChunks/SearchKbChunksQuery.cs` | Query record + validation |
| `…/SearchKbChunks/SearchKbChunksHandler.cs` | Handler (G3) with FTS |
| `…/SearchKbChunks/KbChunkMatchDto.cs` | DTO with rank + highlighted snippet |
| `…/SearchKbChunks/KbChunkSearchResultDto.cs` | Wrapper DTO |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentByIdHandlerTests.cs` | Unit tests |
| `…/GetKbChunksHandlerTests.cs` | Unit tests |
| `…/GetKbChunkByIdHandlerTests.cs` | Unit tests |
| `…/SearchKbChunksHandlerTests.cs` | Unit tests |
| `apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs` | Testcontainers PostgreSQL integration tests |

### Backend — Modified

| File | Change |
|------|--------|
| `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs` | Add 4 properties (`Heading`, `ParentChunkId`, `Level`, `ElementType`) |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs` | Configure new properties + indexes |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/AdvancedChunkingService.cs` | Persist new fields when writing `TextChunkEntity` rows |
| `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs` | Register 4 new endpoint methods + handlers |

### Frontend — Created

| File | Responsibility |
|------|----------------|
| `apps/web/src/lib/api/schemas/kb-document.schemas.ts` | Zod schemas for the 4 DTOs |
| `apps/web/src/hooks/queries/useKbDocument.ts` | React Query hook for G4 |
| `apps/web/src/hooks/queries/useKbChunks.ts` | React Query hook for G1 (paginated) |
| `apps/web/src/hooks/queries/useKbChunk.ts` | React Query hook for G2 |
| `apps/web/src/hooks/queries/useKbChunkSearch.ts` | React Query hook for G3 |

### Frontend — Modified

| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` | Add 4 client methods (`getKbDocument`, `getKbChunks`, `getKbChunk`, `searchKbChunks`) |
| `apps/web/src/hooks/queries/index.ts` | Export new hooks |
| `apps/web/src/lib/api/schemas/index.ts` | Export new schemas |

---

## Task 1: Extend TextChunkEntity with hierarchy fields

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs`

- [ ] **Step 1: Add 4 properties to TextChunkEntity**

Edit `TextChunkEntity.cs` after `CreatedAt` (line 28), before `SearchVector`:

```csharp
// Issue #730: Chunk hierarchy fields (heading_path derivation)
public string? Heading { get; set; }
public Guid? ParentChunkId { get; set; }
public short Level { get; set; } = 1;
public string ElementType { get; set; } = "NarrativeText";
```

- [ ] **Step 2: Configure new properties in TextChunkEntityConfiguration**

Edit `TextChunkEntityConfiguration.cs`, append before the existing `HasIndex` calls (after line 22 `builder.Property(e => e.CreatedAt).IsRequired();`):

```csharp
// Issue #730: Chunk hierarchy fields
builder.Property(e => e.Heading).HasMaxLength(500).IsRequired(false);
builder.Property(e => e.ParentChunkId).IsRequired(false);
builder.Property(e => e.Level).IsRequired().HasDefaultValue<short>(1);
builder.Property(e => e.ElementType).IsRequired().HasMaxLength(20).HasDefaultValue("NarrativeText");

builder.HasIndex(e => e.ParentChunkId);
builder.HasIndex(e => new { e.PdfDocumentId, e.ChunkIndex }).HasDatabaseName("ix_text_chunks_pdf_chunk_index");
```

- [ ] **Step 3: Build to verify no compile errors**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: build succeeds with zero errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/TextChunkEntityConfiguration.cs
git commit -m "feat(kb): #730 add chunk hierarchy fields to TextChunkEntity

Adds Heading, ParentChunkId, Level, ElementType properties + EF Core
configuration. Indexes on ParentChunkId and (PdfDocumentId, ChunkIndex)
for recursive CTE traversal and ordered chunk navigation.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 2: Generate and apply EF Core migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddChunkHierarchyToTextChunkEntity.cs` (auto-generated)

- [ ] **Step 1: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddChunkHierarchyToTextChunkEntity
```

Expected: new migration file appears in `Infrastructure/Migrations/` with `Up()` adding 4 columns + 2 indexes.

- [ ] **Step 2: Review the generated SQL**

Inspect the migration file (`Infrastructure/Migrations/<timestamp>_AddChunkHierarchyToTextChunkEntity.cs`). Verify the `Up()` method contains:
- 4 `AddColumn<>` calls for `Heading`, `ParentChunkId`, `Level`, `ElementType`
- 2 `CreateIndex` calls
- A `Down()` method with reverse operations

If anything looks off (e.g., wrong column types or missing defaults), do NOT edit the migration. Instead, adjust the entity configuration in Task 1 and regenerate.

- [ ] **Step 3: Apply migration to dev database**

```bash
cd apps/api/src/Api
dotnet ef database update
```

Expected: "Done." with no errors. Existing rows have `Heading=NULL`, `ParentChunkId=NULL`, `Level=1`, `ElementType='NarrativeText'`.

- [ ] **Step 4: Verify schema in PostgreSQL**

```bash
docker exec meepleai-postgres psql -U meepleai -d meepleai -c "\d text_chunks" | grep -E "heading|parent_chunk|level|element_type"
```

Expected output includes 4 lines with the new columns.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(kb): #730 EF migration AddChunkHierarchyToTextChunkEntity

Adds heading, parent_chunk_id, level, element_type columns to
text_chunks plus indexes ix_text_chunks_parent_chunk_id and
ix_text_chunks_pdf_chunk_index.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 3: Persist chunk hierarchy in AdvancedChunkingService (forward path)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/AdvancedChunkingService.cs`

**Note:** This task ensures NEW ingestions populate the 4 new columns. Backfill of legacy rows is out-of-scope (separate follow-up issue).

- [ ] **Step 1: Locate the TextChunkEntity persistence point**

Read `AdvancedChunkingService.cs`. Find where `TextChunkEntity` instances are created and added to `DbContext` (look for `new TextChunkEntity` or `_dbContext.TextChunks.Add`).

If the persistence is delegated to a collaborator (`ITextChunkRepository`, `IPersistChunksService`, etc.), follow the chain to the actual write site.

- [ ] **Step 2: Map ChunkPayload → TextChunkEntity new fields**

At the persistence site, for each chunk being written, populate the new properties from the corresponding `ChunkPayload` or `ChunkMetadata` source:

```csharp
var entity = new TextChunkEntity
{
    Id = chunk.Id,
    GameId = chunk.GameId,
    PdfDocumentId = chunk.PdfDocumentId,
    Content = chunk.Content,
    ChunkIndex = chunk.ChunkIndex,
    PageNumber = chunk.PageNumber,
    CharacterCount = chunk.Content.Length,
    CreatedAt = DateTime.UtcNow,
    // Issue #730: hierarchy fields
    Heading = payload.Heading,
    ParentChunkId = string.IsNullOrEmpty(payload.ParentChunkId) ? null : Guid.Parse(payload.ParentChunkId),
    Level = (short)payload.Level,
    ElementType = payload.ElementType ?? "NarrativeText"
};
```

(Adapt to the actual variable names found in step 1; the field-mapping is what matters.)

- [ ] **Step 3: Add a unit test (or extend an existing one) asserting new fields are set**

If a unit test already covers the persistence (`AdvancedChunkingServiceTests.cs` or similar), add an assertion:

```csharp
[Fact]
public async Task ChunkAsync_PersistsHierarchyFields()
{
    // Arrange: existing test setup with synthetic chunks
    // Act: invoke ChunkAsync
    // Assert:
    var saved = await _dbContext.TextChunks.FirstAsync();
    saved.Heading.Should().NotBeNullOrEmpty();
    saved.Level.Should().BeInRange((short)0, (short)2);
    saved.ElementType.Should().NotBeNullOrEmpty();
}
```

If no such test exists, create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/AdvancedChunkingServicePersistenceTests.cs` with an integration test using Testcontainers (skip if pattern is too unfamiliar; integration coverage in Task 12 catches this).

- [ ] **Step 4: Run tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~AdvancedChunkingService" --logger "console;verbosity=normal"
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/AdvancedChunkingService.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/
git commit -m "feat(kb): #730 persist chunk hierarchy on ingestion

AdvancedChunkingService now writes Heading, ParentChunkId, Level,
ElementType to TextChunkEntity (mirror of Qdrant ChunkPayload).
Backfill of legacy rows tracked separately.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 4: G4 — `GetKbDocumentByIdQuery` (smallest endpoint, sets auth pattern)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/GetKbDocumentByIdQuery.cs`
- Create: `…/KbDocumentDto.cs`
- Create: `…/GetKbDocumentByIdHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentByIdHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

Create the test file with three scenarios from G4 Gherkin:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbDocumentByIdHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbDocumentByIdHandler> _logger;
    private readonly GetKbDocumentByIdHandler _handler;

    public GetKbDocumentByIdHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new MeepleAiDbContext(options);
        _logger = Substitute.For<ILogger<GetKbDocumentByIdHandler>>();
        _handler = new GetKbDocumentByIdHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_WhenDocReady_ReturnsFullMetadata()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = docId,
            FileName = "Catan rulebook.pdf",
            ProcessingState = "Ready",
            PageCount = 32,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            Language = "it",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/test.pdf"
        };
        var vd = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            GameId = Guid.NewGuid(),
            TotalChunks = 120,
            IndexedAt = DateTime.UtcNow,
            IndexingStatus = "Completed",
            Language = "it"
        };
        _dbContext.PdfDocuments.Add(pdf);
        _dbContext.VectorDocuments.Add(vd);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, UserIsAdmin: false);

        // Act
        var dto = await _handler.Handle(query, CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.Title.Should().Be("Catan rulebook.pdf");
        dto.ProcessingState.Should().Be("ready");
        dto.TotalChunks.Should().Be(120);
        dto.PageCount.Should().Be(32);
        dto.ProcessingError.Should().BeNull();  // not admin → admin field nulled
    }

    [Fact]
    public async Task Handle_WhenDocFailed_AdminSeesError()
    {
        // Arrange (similar setup with ProcessingState="Failed", ProcessingError="...")
        // Act with UserIsAdmin: true
        // Assert: dto.ProcessingError.Should().Be("Embedding API timeout");
    }

    [Fact]
    public async Task Handle_WhenDocNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var query = new GetKbDocumentByIdQuery(Guid.NewGuid(), UserIsAdmin: false);

        // Act
        var act = () => _handler.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

- [ ] **Step 2: Run test to verify compile failure (handler not implemented)**

```bash
cd apps/api/src/Api
dotnet build
```

Expected: compile error referencing missing types `GetKbDocumentByIdQuery`, `GetKbDocumentByIdHandler`, `KbDocumentDto`.

- [ ] **Step 3: Create the Query record**

`apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/GetKbDocumentByIdQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

internal sealed record GetKbDocumentByIdQuery(
    Guid DocumentId,
    bool UserIsAdmin
) : IQuery<KbDocumentDto?>;
```

- [ ] **Step 4: Create the DTO**

`…/KbDocumentDto.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

internal sealed record KbDocumentDto(
    Guid Id,
    string Title,
    Guid? GameId,
    Guid? SharedGameId,
    string DocumentCategory,
    string ProcessingState,
    int TotalChunks,
    int PageCount,
    DateTime? IndexedAt,
    DateTime UploadedAt,
    string Language,
    string? VersionLabel,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ProcessingError,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? RetryCount,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailedAtState
);
```

- [ ] **Step 5: Create the Handler**

`…/GetKbDocumentByIdHandler.cs`:

```csharp
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

internal sealed class GetKbDocumentByIdHandler : IQueryHandler<GetKbDocumentByIdQuery, KbDocumentDto?>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbDocumentByIdHandler> _logger;

    public GetKbDocumentByIdHandler(MeepleAiDbContext dbContext, ILogger<GetKbDocumentByIdHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbDocumentDto?> Handle(GetKbDocumentByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogDebug("Fetching KB document {DocId} (admin={IsAdmin})", query.DocumentId, query.UserIsAdmin);

        var data = await (
            from pdf in _dbContext.PdfDocuments.AsNoTracking()
            join vd in _dbContext.VectorDocuments.AsNoTracking()
                on pdf.Id equals vd.PdfDocumentId into vdj
            from vd in vdj.DefaultIfEmpty()
            where pdf.Id == query.DocumentId
            select new
            {
                pdf,
                TotalChunks = vd != null ? vd.TotalChunks : 0,
                IndexedAt = vd != null ? vd.IndexedAt : (DateTime?)null,
                GameId = vd != null ? vd.GameId : (Guid?)null
            }
        ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (data is null)
        {
            throw new NotFoundException($"KB document {query.DocumentId} not found");
        }

        return new KbDocumentDto(
            Id: data.pdf.Id,
            Title: data.pdf.FileName,
            GameId: data.GameId,
            SharedGameId: data.pdf.SharedGameId,
            DocumentCategory: data.pdf.DocumentCategory,
            ProcessingState: data.pdf.ProcessingState.ToLowerInvariant(),
            TotalChunks: data.TotalChunks,
            PageCount: data.pdf.PageCount ?? 0,
            IndexedAt: data.IndexedAt,
            UploadedAt: data.pdf.UploadedAt,
            Language: data.pdf.Language,
            VersionLabel: data.pdf.VersionLabel,
            ProcessingError: query.UserIsAdmin ? data.pdf.ProcessingError : null,
            RetryCount: query.UserIsAdmin ? data.pdf.RetryCount : null,
            FailedAtState: query.UserIsAdmin ? data.pdf.FailedAtState : null
        );
    }
}
```

- [ ] **Step 6: Run tests, expect pass**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~GetKbDocumentByIdHandler" --logger "console;verbosity=normal"
```

Expected: all 3 tests pass.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/ apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentByIdHandlerTests.cs
git commit -m "feat(kb): #730 G4 GetKbDocumentByIdQuery handler

Returns full doc metadata (title, gameId, processingState, totalChunks,
pageCount, indexedAt, language, versionLabel) for /api/v1/kb-docs/{id}.
Admin-only fields (processingError, retryCount, failedAtState) gated
via UserIsAdmin flag and serialized with [JsonIgnore(WhenWritingNull)].

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 5: G4 — Wire endpoint in KnowledgeBaseEndpoints + integration test

**Files:**
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`
- Create: `apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs` (initial scaffolding for the suite)

- [ ] **Step 1: Add `MapKbDocumentEndpoints` private method to KnowledgeBaseEndpoints.cs**

Add to the file, near other Map methods:

```csharp
private static void MapKbDocumentEndpoints(RouteGroupBuilder group)
{
    // Issue #730: G4 single doc metadata
    group.MapGet("/kb-docs/{id:guid}", HandleGetKbDocumentById)
        .WithName("GetKbDocumentById")
        .RequireSession()
        .WithTags("KnowledgeBase")
        .WithSummary("Get KB document metadata")
        .WithDescription("Returns metadata for a single KB document (title, processing state, total chunks, page count). Admin users see additional diagnostic fields.")
        .Produces<KbDocumentDto>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden);
}

private static async Task<IResult> HandleGetKbDocumentById(
    Guid id,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken cancellationToken)
{
    var session = httpContext.GetSession();
    var query = new GetKbDocumentByIdQuery(id, UserIsAdmin: session.User.Role == "Admin");
    var dto = await mediator.Send(query, cancellationToken);
    return dto is not null ? Results.Ok(dto) : Results.NotFound();
}
```

(Adjust the `httpContext.GetSession()` and `session.User.Role` calls to match the project's actual session API — check the existing `HandleGetKnowledgeBaseStatus` for the canonical pattern.)

- [ ] **Step 2: Add the `using` for the new namespace**

At the top of `KnowledgeBaseEndpoints.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;
```

- [ ] **Step 3: Register the new mapping in `MapKnowledgeBaseEndpoints`**

Add to the chain (around line 40 next to `MapGameDocumentsEndpoint`):

```csharp
MapKbDocumentEndpoints(group);
```

- [ ] **Step 4: Build to verify compile**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: builds cleanly.

- [ ] **Step 5: Write the integration test scaffold**

Create `apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbChunkEndpointsIntegrationTests : IClassFixture<MeepleAiWebApplicationFactory>
{
    private readonly MeepleAiWebApplicationFactory _factory;

    public KbChunkEndpointsIntegrationTests(MeepleAiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetKbDocument_WhenAuthenticated_Returns200()
    {
        // Arrange
        var (client, docId) = await _factory.CreateAuthenticatedClientWithSeededDocumentAsync(role: "User");

        // Act
        var response = await client.GetAsync($"/api/v1/kb-docs/{docId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<KbDocumentDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(docId);
        dto.ProcessingError.Should().BeNull();  // not admin
    }

    [Fact]
    public async Task GetKbDocument_AsAdmin_ReturnsErrorField()
    {
        // Similar but with role:"Admin" + a Failed doc seed
    }

    [Fact]
    public async Task GetKbDocument_WhenNotFound_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/kb-docs/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

(If `MeepleAiWebApplicationFactory` doesn't expose `CreateAuthenticatedClientWithSeededDocumentAsync`, follow the existing integration test pattern in `Api.Tests/Integration/` — look at any existing endpoint integration test and copy the seed/auth helpers.)

- [ ] **Step 6: Run integration tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~KbChunkEndpointsIntegrationTests" --logger "console;verbosity=normal"
```

Expected: 3 tests pass (initially the second admin test may use a stub; that's fine for now).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs
git commit -m "feat(kb): #730 G4 endpoint /api/v1/kb-docs/{id}

Routing wiring + integration test scaffold. Admin role drives
ProcessingError/RetryCount/FailedAtState population in DTO.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 6: G1 — `GetKbChunksQuery` DTOs + handler skeleton (no CTE yet)

**Files:**
- Create: `…/GetKbChunks/GetKbChunksQuery.cs`
- Create: `…/GetKbChunks/KbChunkSummaryDto.cs`
- Create: `…/GetKbChunks/KbChunkListDto.cs`
- Create: `…/GetKbChunks/GetKbChunksHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunksHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbChunksHandlerTests
{
    // ctor wires _dbContext (InMemory) + _handler (similar to GetKbDocumentByIdHandlerTests)

    [Fact]
    public async Task Handle_WhenDocHas120Chunks_ReturnsFirstPage()
    {
        // Arrange: seed 120 TextChunkEntity rows for one PdfDocumentId, with chunkIndex 0..119
        // and varying heading values
        var docId = await SeedDocumentWithChunksAsync(120);
        var query = new GetKbChunksQuery(docId, Skip: 0, Take: 50, UserIsAdmin: false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Chunks.Should().HaveCount(50);
        result.Chunks.Select(c => c.Position).Should().BeInAscendingOrder();
        result.TotalCount.Should().Be(120);
        result.HasMore.Should().BeTrue();
        result.Chunks.First().Snippet.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task Handle_LastPagePartial_HasMoreFalse()
    {
        var docId = await SeedDocumentWithChunksAsync(120);
        var query = new GetKbChunksQuery(docId, Skip: 100, Take: 50, UserIsAdmin: false);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Chunks.Should().HaveCount(20);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DocStillProcessing_ReturnsEmptyChunks()
    {
        // Arrange: seed PdfDocument in state=Embedding with 0 chunks
        // Act + Assert: result.Chunks empty, ProcessingState="embedding"
    }

    [Fact]
    public async Task Handle_AdminSeesDiagnosticFields()
    {
        // Arrange: 5 chunks, each with elementType+characterCount populated
        // Act with UserIsAdmin: true
        // Assert: vectorId equals chunkId, characterCount > 0, elementType non-null
    }

    [Fact]
    public async Task Handle_NonAdminGetsAdminFieldsNulled()
    {
        // Same seed
        // Act with UserIsAdmin: false
        // Assert: vectorId, characterCount, elementType, embeddingStatus all null
    }
}
```

- [ ] **Step 2: Build to verify compile failure**

Expected: missing types.

- [ ] **Step 3: Create Query, DTOs, and Handler skeleton**

`GetKbChunksQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

internal sealed record GetKbChunksQuery(
    Guid DocumentId,
    int Skip,
    int Take,
    bool UserIsAdmin
) : IQuery<KbChunkListDto>;
```

`KbChunkSummaryDto.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

internal sealed record KbChunkSummaryDto(
    Guid ChunkId,
    int? PageNumber,
    int Position,
    short Level,
    IReadOnlyList<string> HeadingPath,
    string Snippet,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? VectorId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CharacterCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ElementType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EmbeddingStatus
);
```

`KbChunkListDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

internal sealed record KbChunkListDto(
    IReadOnlyList<KbChunkSummaryDto> Chunks,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore,
    string ProcessingState
);
```

`GetKbChunksHandler.cs` (skeleton, headingPath returns empty array for now):

```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

internal sealed class GetKbChunksHandler : IQueryHandler<GetKbChunksQuery, KbChunkListDto>
{
    private const int MaxTake = 100;
    private const int SnippetMaxLength = 200;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbChunksHandler> _logger;

    public GetKbChunksHandler(MeepleAiDbContext dbContext, ILogger<GetKbChunksHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkListDto> Handle(GetKbChunksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, MaxTake);

        var processingState = await _dbContext.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == query.DocumentId)
            .Select(p => p.ProcessingState.ToLower())
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? "unknown";

        var totalCount = await _dbContext.TextChunks.AsNoTracking()
            .CountAsync(c => c.PdfDocumentId == query.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        var rows = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId)
            .OrderBy(c => c.ChunkIndex)
            .Skip(skip)
            .Take(take)
            .Select(c => new
            {
                c.Id,
                c.PageNumber,
                c.ChunkIndex,
                c.Level,
                c.Heading,
                c.ParentChunkId,
                c.Content,
                c.CharacterCount,
                c.ElementType
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var chunks = rows.Select(r => new KbChunkSummaryDto(
            ChunkId: r.Id,
            PageNumber: r.PageNumber,
            Position: r.ChunkIndex,
            Level: r.Level,
            HeadingPath: Array.Empty<string>(), // TODO: filled in Task 7 (CTE)
            Snippet: TruncateSnippet(r.Content),
            VectorId: query.UserIsAdmin ? r.Id : (Guid?)null,
            CharacterCount: query.UserIsAdmin ? r.CharacterCount : (int?)null,
            ElementType: query.UserIsAdmin ? r.ElementType : null,
            EmbeddingStatus: query.UserIsAdmin ? "indexed" : null  // simple stub; refine if richer status is tracked
        )).ToList();

        return new KbChunkListDto(
            Chunks: chunks,
            TotalCount: totalCount,
            Skip: skip,
            Take: take,
            HasMore: skip + take < totalCount,
            ProcessingState: processingState
        );
    }

    private static string TruncateSnippet(string content) =>
        content.Length <= SnippetMaxLength ? content : content[..SnippetMaxLength];
}
```

- [ ] **Step 4: Run unit tests, expect 4/5 pass (one will fail because headingPath is empty array)**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~GetKbChunksHandlerTests" --logger "console;verbosity=normal"
```

Expected: 4 tests pass; the test asserting `headingPath` content (if you wrote one) fails. We'll fix this in Task 7.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunks/ apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunksHandlerTests.cs
git commit -m "feat(kb): #730 G1 GetKbChunksQuery skeleton (headingPath stub)

Paginated chunks list with offset pagination (max 100), DTO field
gating for admin diagnostic fields. headingPath returns empty array
pending CTE implementation in next commit.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 7: G1 — Implement headingPath via recursive CTE

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunks/GetKbChunksHandler.cs`

- [ ] **Step 1: Write the failing test**

Add to `GetKbChunksHandlerTests.cs`:

```csharp
[Fact]
public async Task Handle_NestedHeadings_ReturnsHeadingPathTraversal()
{
    // Arrange: seed 3 chunks where:
    //   chunk1 (level=0, heading="Setup",       parent=null)
    //   chunk2 (level=1, heading="Players",     parent=chunk1.Id)
    //   chunk3 (level=2, heading="Distribution",parent=chunk2.Id)
    // (and a few extra chunks at level=2 referencing chunk3 indirectly is overkill;
    //  3 levels deep covers the recursive CTE)

    var docId = Guid.NewGuid();
    // ... seed PdfDocument + 3 TextChunks chained via ParentChunkId
    await _dbContext.SaveChangesAsync();

    var query = new GetKbChunksQuery(docId, Skip: 0, Take: 50, UserIsAdmin: false);

    // Act
    var result = await _handler.Handle(query, CancellationToken.None);

    // Assert
    var leaf = result.Chunks.Single(c => c.Position == 2);  // chunk3
    leaf.HeadingPath.Should().BeEquivalentTo(new[] { "Setup", "Players", "Distribution" });
}
```

**Note**: this test requires PostgreSQL to exercise the recursive CTE. EF InMemory does NOT support `WITH RECURSIVE`. Mark this test with `[Trait("Category", TestCategories.Integration)]` and use the Testcontainers fixture instead — or move it to `KbChunkEndpointsIntegrationTests.cs` where the suite already has DB access.

- [ ] **Step 2: Run test, expect fail (CTE not implemented)**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~Handle_NestedHeadings_ReturnsHeadingPathTraversal" --logger "console;verbosity=normal"
```

Expected: assertion fails (`HeadingPath` is empty array).

- [ ] **Step 3: Implement the CTE in the handler**

Replace the `chunks = rows.Select(...)` block in `GetKbChunksHandler.cs` with a two-phase approach:

```csharp
// Phase 1: load chunk rows (already done above)
// Phase 2: load heading paths in a single SQL call using FromSqlRaw

var chunkIds = rows.Select(r => r.Id).ToList();
var headingPaths = chunkIds.Count == 0
    ? new Dictionary<Guid, IReadOnlyList<string>>()
    : await LoadHeadingPathsAsync(chunkIds, cancellationToken);

var chunks = rows.Select(r => new KbChunkSummaryDto(
    // ...same as before, but:
    HeadingPath: headingPaths.TryGetValue(r.Id, out var path) ? path : Array.Empty<string>(),
    // ...
)).ToList();
```

Add the helper method to the handler class:

```csharp
private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadHeadingPathsAsync(
    IReadOnlyList<Guid> chunkIds,
    CancellationToken cancellationToken)
{
    // Use parameterized SQL with WITH RECURSIVE
    const string sql = @"
        WITH RECURSIVE chunk_path(start_id, id, heading, parent_chunk_id, depth) AS (
          SELECT t.id AS start_id, t.id, t.heading, t.parent_chunk_id, 1 AS depth
          FROM text_chunks t
          WHERE t.id = ANY({0})
          UNION ALL
          SELECT cp.start_id, t.id, t.heading, t.parent_chunk_id, cp.depth + 1
          FROM text_chunks t
          JOIN chunk_path cp ON t.id = cp.parent_chunk_id
          WHERE cp.depth < 10
        )
        SELECT start_id, heading, depth
        FROM chunk_path
        WHERE heading IS NOT NULL
        ORDER BY start_id, depth DESC;";

    var raw = await _dbContext.Database
        .SqlQueryRaw<HeadingPathRow>(sql, new[] { chunkIds.ToArray() })
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    return raw
        .GroupBy(r => r.StartId)
        .ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<string>)g.Select(x => x.Heading!).ToList());
}

private sealed record HeadingPathRow(Guid StartId, string? Heading, int Depth);
```

(If `SqlQueryRaw` overload signatures differ in the project's EF version, fall back to `_dbContext.Database.GetDbConnection()` + an Npgsql command — but try `SqlQueryRaw` first since EF 9 supports it.)

- [ ] **Step 4: Run integration test, expect pass**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~Handle_NestedHeadings_ReturnsHeadingPathTraversal" --logger "console;verbosity=normal"
```

Expected: pass.

- [ ] **Step 5: Run all G1 tests to confirm no regression**

```bash
dotnet test --filter "FullyQualifiedName~GetKbChunksHandler" --logger "console;verbosity=normal"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunks/GetKbChunksHandler.cs apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs
git commit -m "feat(kb): #730 G1 headingPath via recursive CTE

Loads heading_path arrays for all chunks in one SQL roundtrip using
WITH RECURSIVE on text_chunks, capped at depth 10. Empty array when
no heading exists in the parent chain.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 8: G1 — Wire `/kb-docs/{id}/chunks` endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`

- [ ] **Step 1: Add endpoint mapping inside `MapKbDocumentEndpoints`**

```csharp
// Issue #730: G1 paginated chunks list
group.MapGet("/kb-docs/{id:guid}/chunks", HandleGetKbChunks)
    .WithName("GetKbChunks")
    .RequireSession()
    .WithTags("KnowledgeBase")
    .WithSummary("Get paginated chunks list with hierarchical headings")
    .WithDescription("Returns chunks ordered by position with breadcrumb headingPath. Admin users see vectorId, characterCount, elementType, embeddingStatus.")
    .Produces<KbChunkListDto>()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound);
```

- [ ] **Step 2: Add the handler method**

```csharp
private static async Task<IResult> HandleGetKbChunks(
    Guid id,
    int? skip,
    int? take,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken cancellationToken)
{
    var skipValue = skip ?? 0;
    var takeValue = take ?? 50;

    if (takeValue < 1 || takeValue > 100)
    {
        return Results.BadRequest(new { error = "take must be between 1 and 100" });
    }

    var session = httpContext.GetSession();
    var query = new GetKbChunksQuery(id, skipValue, takeValue, UserIsAdmin: session.User.Role == "Admin");
    var result = await mediator.Send(query, cancellationToken);
    return Results.Ok(result);
}
```

- [ ] **Step 3: Add `using` for the new namespace**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;
```

- [ ] **Step 4: Add an integration test for the take=500 boundary**

In `KbChunkEndpointsIntegrationTests.cs`:

```csharp
[Fact]
public async Task GetKbChunks_TakeExceedsLimit_Returns400()
{
    var (client, docId) = await _factory.CreateAuthenticatedClientWithSeededDocumentAsync(role: "User");

    var response = await client.GetAsync($"/api/v1/kb-docs/{docId}/chunks?take=500");

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 5: Run integration tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~KbChunkEndpointsIntegrationTests" --logger "console;verbosity=normal"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs
git commit -m "feat(kb): #730 G1 endpoint /api/v1/kb-docs/{id}/chunks

Wires GetKbChunksQuery with take∈[1,100] validation. Returns 400 if
take is out of range, 404 if doc not found.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 9: G2 — `GetKbChunkByIdQuery` handler with prev/next navigation

**Files:**
- Create: `…/GetKbChunkById/GetKbChunkByIdQuery.cs`
- Create: `…/GetKbChunkById/KbChunkDetailDto.cs`
- Create: `…/GetKbChunkById/GetKbChunkByIdHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunkByIdHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Trait("Category", TestCategories.Unit)]
public sealed class GetKbChunkByIdHandlerTests
{
    // ctor as before

    [Fact]
    public async Task Handle_MiddleChunk_ReturnsContentWithPrevNext()
    {
        // Arrange: seed 3 chunks (chunkIndex 0, 1, 2)
        // Act: query for chunk at index 1
        // Assert: prevChunkId = chunk0.Id, nextChunkId = chunk2.Id
    }

    [Fact]
    public async Task Handle_FirstChunk_PrevIsNull()
    {
        // Arrange: 3 chunks, query for index 0
        // Assert: prevChunkId is null, nextChunkId is chunk1.Id
    }

    [Fact]
    public async Task Handle_LastChunk_NextIsNull()
    {
        // Same seed, query for index 2
        // Assert: nextChunkId is null
    }

    [Fact]
    public async Task Handle_ChunkBelongsToDifferentDoc_ThrowsNotFound()
    {
        // Seed two docs A, B. Query (A.Id, B.chunkId) -> 404
    }

    [Fact]
    public async Task Handle_NonExistentChunk_ThrowsNotFound()
    {
        // Query (validDocId, randomGuid) -> 404
    }
}
```

- [ ] **Step 2: Build, expect compile errors**

- [ ] **Step 3: Create Query record**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

internal sealed record GetKbChunkByIdQuery(
    Guid DocumentId,
    Guid ChunkId,
    bool UserIsAdmin
) : IQuery<KbChunkDetailDto>;
```

- [ ] **Step 4: Create the DTO**

```csharp
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

internal sealed record KbChunkDetailDto(
    Guid ChunkId,
    string Content,
    int? PageNumber,
    int Position,
    short Level,
    IReadOnlyList<string> HeadingPath,
    Guid? PrevChunkId,
    Guid? NextChunkId,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? VectorId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CharacterCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ElementType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EmbeddingStatus,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? ParentChunkId
);
```

- [ ] **Step 5: Create the Handler**

```csharp
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

internal sealed class GetKbChunkByIdHandler : IQueryHandler<GetKbChunkByIdQuery, KbChunkDetailDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbChunkByIdHandler> _logger;

    public GetKbChunkByIdHandler(MeepleAiDbContext dbContext, ILogger<GetKbChunkByIdHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkDetailDto> Handle(GetKbChunkByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var chunk = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.Id == query.ChunkId && c.PdfDocumentId == query.DocumentId)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.PageNumber,
                c.ChunkIndex,
                c.Level,
                c.Heading,
                c.ParentChunkId,
                c.CharacterCount,
                c.ElementType,
                c.PdfDocumentId
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunk is null)
        {
            throw new NotFoundException($"Chunk {query.ChunkId} not found in document {query.DocumentId}");
        }

        // Prev / Next chunks (by chunkIndex)
        var prevChunkId = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId && c.ChunkIndex == chunk.ChunkIndex - 1)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var nextChunkId = await _dbContext.TextChunks.AsNoTracking()
            .Where(c => c.PdfDocumentId == query.DocumentId && c.ChunkIndex == chunk.ChunkIndex + 1)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Heading path via the same CTE pattern as G1 (TODO: extract to a shared service if duplication grows)
        var headingPath = await LoadHeadingPathAsync(chunk.Id, cancellationToken);

        return new KbChunkDetailDto(
            ChunkId: chunk.Id,
            Content: chunk.Content,
            PageNumber: chunk.PageNumber,
            Position: chunk.ChunkIndex,
            Level: chunk.Level,
            HeadingPath: headingPath,
            PrevChunkId: prevChunkId,
            NextChunkId: nextChunkId,
            VectorId: query.UserIsAdmin ? chunk.Id : (Guid?)null,
            CharacterCount: query.UserIsAdmin ? chunk.CharacterCount : (int?)null,
            ElementType: query.UserIsAdmin ? chunk.ElementType : null,
            EmbeddingStatus: query.UserIsAdmin ? "indexed" : null,
            ParentChunkId: query.UserIsAdmin ? chunk.ParentChunkId : null
        );
    }

    private async Task<IReadOnlyList<string>> LoadHeadingPathAsync(Guid chunkId, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH RECURSIVE chunk_path(id, heading, parent_chunk_id, depth) AS (
              SELECT t.id, t.heading, t.parent_chunk_id, 1 AS depth
              FROM text_chunks t WHERE t.id = {0}
              UNION ALL
              SELECT t.id, t.heading, t.parent_chunk_id, cp.depth + 1
              FROM text_chunks t
              JOIN chunk_path cp ON t.id = cp.parent_chunk_id
              WHERE cp.depth < 10
            )
            SELECT heading FROM chunk_path
            WHERE heading IS NOT NULL
            ORDER BY depth DESC;";

        var headings = await _dbContext.Database
            .SqlQueryRaw<string>(sql, new object[] { chunkId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return headings;
    }
}
```

- [ ] **Step 6: Run unit tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~GetKbChunkByIdHandler" --logger "console;verbosity=normal"
```

Expected: all 5 tests pass (heading path test may need to move to integration suite — see Task 7 step 1 note).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunkById/ apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbChunkByIdHandlerTests.cs
git commit -m "feat(kb): #730 G2 GetKbChunkByIdQuery handler

Single-chunk fetch with full content + prev/next chunkId lookup by
ChunkIndex offset and recursive headingPath. 404 when chunk doesn't
exist or belongs to a different document.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 10: G2 — Wire `/kb-docs/{id}/chunks/{chunkId}` endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`

- [ ] **Step 1: Add endpoint mapping**

```csharp
// Issue #730: G2 single chunk full content
group.MapGet("/kb-docs/{id:guid}/chunks/{chunkId:guid}", HandleGetKbChunkById)
    .WithName("GetKbChunkById")
    .RequireSession()
    .WithTags("KnowledgeBase")
    .WithSummary("Get a single chunk with full content + prev/next navigation")
    .WithDescription("Returns chunk content as markdown, with hierarchical breadcrumb and prev/next chunk IDs for navigation.")
    .Produces<KbChunkDetailDto>()
    .Produces(StatusCodes.Status404NotFound);
```

- [ ] **Step 2: Add the handler**

```csharp
private static async Task<IResult> HandleGetKbChunkById(
    Guid id,
    Guid chunkId,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken cancellationToken)
{
    var session = httpContext.GetSession();
    var query = new GetKbChunkByIdQuery(id, chunkId, UserIsAdmin: session.User.Role == "Admin");
    var dto = await mediator.Send(query, cancellationToken);
    return Results.Ok(dto);
}
```

(NotFoundException is mapped to 404 by the global exception middleware; no manual handling needed.)

- [ ] **Step 3: Add `using`**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;
```

- [ ] **Step 4: Add integration test**

```csharp
[Fact]
public async Task GetKbChunk_ChunkBelongsToOtherDoc_Returns404()
{
    var (client, docAId) = await _factory.CreateAuthenticatedClientWithSeededDocumentAsync(role: "User");
    var (_, docBId, chunkBId) = await _factory.SeedAnotherDocumentWithOneChunkAsync();

    var response = await client.GetAsync($"/api/v1/kb-docs/{docAId}/chunks/{chunkBId}");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}
```

- [ ] **Step 5: Run all KB integration tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~KbChunkEndpointsIntegrationTests" --logger "console;verbosity=normal"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs
git commit -m "feat(kb): #730 G2 endpoint /api/v1/kb-docs/{id}/chunks/{chunkId}

Wires GetKbChunkByIdQuery. Cross-doc chunkId returns 404 (no leak of
chunk existence in other docs).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 11: G3 — `SearchKbChunksQuery` with PostgreSQL FTS

**Files:**
- Create: `…/SearchKbChunks/SearchKbChunksQuery.cs`
- Create: `…/SearchKbChunks/KbChunkMatchDto.cs`
- Create: `…/SearchKbChunks/KbChunkSearchResultDto.cs`
- Create: `…/SearchKbChunks/SearchKbChunksHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/SearchKbChunksHandlerTests.cs`

- [ ] **Step 1: Write failing tests (integration only — FTS requires real PostgreSQL)**

Add to the integration suite:

```csharp
[Fact]
public async Task SearchChunks_KeywordPresent_ReturnsRankedMatches()
{
    var (client, docId) = await _factory.SeedDocumentWithKeywordChunksAsync(
        keyword: "initiative", occurrencesPerChunk: new[] { 3, 1, 0, 5, 2 });

    var response = await client.PostAsJsonAsync(
        $"/api/v1/kb-docs/{docId}/chunks/search",
        new { query = "initiative", skip = 0, take = 20 });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<KbChunkSearchResultDto>();
    result!.TotalCount.Should().Be(4);  // 4 chunks with the keyword
    result.Matches.Should().BeInDescendingOrder(m => m.Rank);
    result.Matches.First().Snippet.Should().Contain("<mark>initiative</mark>");
}

[Fact]
public async Task SearchChunks_NoMatches_ReturnsEmpty()
{
    var (client, docId) = await _factory.CreateAuthenticatedClientWithSeededDocumentAsync(role: "User");

    var response = await client.PostAsJsonAsync(
        $"/api/v1/kb-docs/{docId}/chunks/search",
        new { query = "xyzzyx", skip = 0, take = 20 });

    var result = await response.Content.ReadFromJsonAsync<KbChunkSearchResultDto>();
    result!.TotalCount.Should().Be(0);
    result.Matches.Should().BeEmpty();
}

[Fact]
public async Task SearchChunks_QueryTooLong_Returns400()
{
    var (client, docId) = await _factory.CreateAuthenticatedClientWithSeededDocumentAsync(role: "User");

    var longQuery = new string('a', 250);
    var response = await client.PostAsJsonAsync(
        $"/api/v1/kb-docs/{docId}/chunks/search",
        new { query = longQuery, skip = 0, take = 20 });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task SearchChunks_OperatorInjection_TreatedAsLiteral()
{
    var (client, docId) = await _factory.SeedDocumentWithKeywordChunksAsync(
        keyword: "initiative", occurrencesPerChunk: new[] { 1 });

    var response = await client.PostAsJsonAsync(
        $"/api/v1/kb-docs/{docId}/chunks/search",
        new { query = "initiative | drop", skip = 0, take = 20 });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    // Should NOT throw "syntax error in tsquery"; the | is treated as a separator.
}
```

- [ ] **Step 2: Build, expect missing types**

- [ ] **Step 3: Create Query, DTOs, Handler**

`SearchKbChunksQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

internal sealed record SearchKbChunksQuery(
    Guid DocumentId,
    string Query,
    int Skip,
    int Take
) : IQuery<KbChunkSearchResultDto>;
```

`KbChunkMatchDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

internal sealed record KbChunkMatchDto(
    Guid ChunkId,
    IReadOnlyList<string> HeadingPath,
    string Snippet,         // ts_headline output with <mark> tags
    float Rank,
    int? PageNumber,
    int Position
);
```

`KbChunkSearchResultDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

internal sealed record KbChunkSearchResultDto(
    IReadOnlyList<KbChunkMatchDto> Matches,
    int TotalCount,
    int Skip,
    int Take
);
```

`SearchKbChunksHandler.cs`:

```csharp
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

internal sealed class SearchKbChunksHandler : IQueryHandler<SearchKbChunksQuery, KbChunkSearchResultDto>
{
    private const int MaxQueryLength = 200;
    private const int MaxTake = 100;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SearchKbChunksHandler> _logger;

    public SearchKbChunksHandler(MeepleAiDbContext dbContext, ILogger<SearchKbChunksHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<KbChunkSearchResultDto> Handle(SearchKbChunksQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Query) || query.Query.Length > MaxQueryLength)
        {
            throw new ValidationException("query must be between 1 and 200 characters");
        }

        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, MaxTake);

        // Verify doc exists
        var docExists = await _dbContext.PdfDocuments.AsNoTracking()
            .AnyAsync(p => p.Id == query.DocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (!docExists)
        {
            throw new NotFoundException($"Document {query.DocumentId} not found");
        }

        // FTS query: plainto_tsquery sanitizes user input (no operator injection)
        const string sql = @"
            WITH ranked AS (
              SELECT
                tc.id,
                tc.heading,
                tc.parent_chunk_id,
                tc.page_number,
                tc.chunk_index,
                ts_rank_cd(tc.search_vector, q) AS rank,
                ts_headline('simple', tc.content, q,
                  'StartSel=<mark>, StopSel=</mark>, MaxFragments=2, MaxWords=20, MinWords=5') AS snippet
              FROM text_chunks tc, plainto_tsquery('simple', {1}) q
              WHERE tc.pdf_document_id = {0}
                AND tc.search_vector @@ q
            )
            SELECT id, heading, parent_chunk_id, page_number, chunk_index, rank, snippet
            FROM ranked
            ORDER BY rank DESC
            OFFSET {2} LIMIT {3};";

        var rows = await _dbContext.Database
            .SqlQueryRaw<SearchRow>(sql, new object[] { query.DocumentId, query.Query, skip, take })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Total count (separate query for accuracy with skip/take)
        const string countSql = @"
            SELECT COUNT(*)::int AS Value
            FROM text_chunks tc, plainto_tsquery('simple', {1}) q
            WHERE tc.pdf_document_id = {0} AND tc.search_vector @@ q;";

        var totalCount = await _dbContext.Database
            .SqlQueryRaw<int>(countSql, new object[] { query.DocumentId, query.Query })
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        // Build heading paths for the matched chunks
        var matchIds = rows.Select(r => r.Id).ToList();
        var headingPaths = matchIds.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<string>>()
            : await LoadHeadingPathsAsync(matchIds, cancellationToken);

        var matches = rows.Select(r => new KbChunkMatchDto(
            ChunkId: r.Id,
            HeadingPath: headingPaths.TryGetValue(r.Id, out var hp) ? hp : Array.Empty<string>(),
            Snippet: r.Snippet,
            Rank: r.Rank,
            PageNumber: r.PageNumber,
            Position: r.ChunkIndex
        )).ToList();

        return new KbChunkSearchResultDto(matches, totalCount, skip, take);
    }

    // (LoadHeadingPathsAsync — copy from GetKbChunksHandler or extract to a shared service)

    private sealed record SearchRow(
        Guid Id, string? Heading, Guid? ParentChunkId,
        int? PageNumber, int ChunkIndex, float Rank, string Snippet);
}
```

- [ ] **Step 4: Add `ValidationException` mapping if not already present**

If `ValidationException` doesn't already map to 400 in the global middleware, the simplest fix is to wrap the validation in the routing handler instead. But check `Middleware/Exceptions/` first.

- [ ] **Step 5: Run integration tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~SearchChunks" --logger "console;verbosity=normal"
```

Expected: 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchKbChunks/ apps/api/tests/Api.Tests/Integration/KbChunkEndpointsIntegrationTests.cs
git commit -m "feat(kb): #730 G3 SearchKbChunksQuery handler

In-document keyword search using plainto_tsquery + ts_rank_cd +
ts_headline (StartSel=<mark>, MaxFragments=2). Operator injection
mitigated by plainto_tsquery (treats input as plain text, not tsquery
syntax). Validation: query length 1-200 chars.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 12: G3 — Wire `POST /kb-docs/{id}/chunks/search` endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`

- [ ] **Step 1: Add endpoint mapping**

```csharp
// Issue #730: G3 in-document FTS
group.MapPost("/kb-docs/{id:guid}/chunks/search", HandleSearchKbChunks)
    .WithName("SearchKbChunks")
    .RequireSession()
    .WithTags("KnowledgeBase")
    .WithSummary("Search chunks within a single document by keyword")
    .WithDescription("PostgreSQL ts_rank_cd ranking with ts_headline highlighting (<mark> tags). Query length 1-200 chars.")
    .Produces<KbChunkSearchResultDto>()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status404NotFound);
```

- [ ] **Step 2: Define request DTO + handler**

```csharp
internal sealed record SearchKbChunksRequest(string Query, int? Skip, int? Take);

private static async Task<IResult> HandleSearchKbChunks(
    Guid id,
    [FromBody] SearchKbChunksRequest body,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken cancellationToken)
{
    if (body is null || string.IsNullOrWhiteSpace(body.Query) || body.Query.Length > 200)
    {
        return Results.BadRequest(new { error = "query must be between 1 and 200 characters" });
    }

    var query = new SearchKbChunksQuery(id, body.Query, body.Skip ?? 0, body.Take ?? 20);
    var result = await mediator.Send(query, cancellationToken);
    return Results.Ok(result);
}
```

- [ ] **Step 3: Add `using`**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;
```

- [ ] **Step 4: Run all KB tests**

```bash
cd apps/api/src/Api
dotnet test --filter "FullyQualifiedName~KnowledgeBase|FullyQualifiedName~KbChunk" --logger "console;verbosity=normal"
```

Expected: all green.

- [ ] **Step 5: Run full test suite to confirm no regression**

```bash
cd apps/api/src/Api
dotnet test --logger "console;verbosity=minimal"
```

Expected: full suite passes (existing 13,000+ tests + new ones).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs
git commit -m "feat(kb): #730 G3 endpoint POST /api/v1/kb-docs/{id}/chunks/search

Wires SearchKbChunksQuery with body validation (query length 1-200).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 13: Frontend — Zod schemas + client methods

**Files:**
- Create: `apps/web/src/lib/api/schemas/kb-document.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/knowledgeBaseClient.ts`
- Modify: `apps/web/src/lib/api/schemas/index.ts`

- [ ] **Step 1: Create the Zod schemas**

`apps/web/src/lib/api/schemas/kb-document.schemas.ts`:

```typescript
import { z } from 'zod';

export const kbDocumentSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  gameId: z.string().uuid().nullable().optional(),
  sharedGameId: z.string().uuid().nullable().optional(),
  documentCategory: z.string(),
  processingState: z.enum([
    'pending', 'uploading', 'extracting', 'chunking',
    'embedding', 'indexing', 'ready', 'failed',
  ]),
  totalChunks: z.number().int().nonnegative(),
  pageCount: z.number().int().nonnegative(),
  indexedAt: z.string().datetime().nullable().optional(),
  uploadedAt: z.string().datetime(),
  language: z.string(),
  versionLabel: z.string().nullable().optional(),
  // Admin-only:
  processingError: z.string().nullable().optional(),
  retryCount: z.number().int().nullable().optional(),
  failedAtState: z.string().nullable().optional(),
});
export type KbDocument = z.infer<typeof kbDocumentSchema>;

export const kbChunkSummarySchema = z.object({
  chunkId: z.string().uuid(),
  pageNumber: z.number().int().nullable().optional(),
  position: z.number().int().nonnegative(),
  level: z.number().int().min(0).max(2),
  headingPath: z.array(z.string()),
  snippet: z.string(),
  // Admin-only:
  vectorId: z.string().uuid().nullable().optional(),
  characterCount: z.number().int().nullable().optional(),
  elementType: z.string().nullable().optional(),
  embeddingStatus: z.string().nullable().optional(),
});
export type KbChunkSummary = z.infer<typeof kbChunkSummarySchema>;

export const kbChunkListSchema = z.object({
  chunks: z.array(kbChunkSummarySchema),
  totalCount: z.number().int().nonnegative(),
  skip: z.number().int().nonnegative(),
  take: z.number().int().positive(),
  hasMore: z.boolean(),
  processingState: z.string(),
});
export type KbChunkList = z.infer<typeof kbChunkListSchema>;

export const kbChunkDetailSchema = z.object({
  chunkId: z.string().uuid(),
  content: z.string(),
  pageNumber: z.number().int().nullable().optional(),
  position: z.number().int().nonnegative(),
  level: z.number().int().min(0).max(2),
  headingPath: z.array(z.string()),
  prevChunkId: z.string().uuid().nullable().optional(),
  nextChunkId: z.string().uuid().nullable().optional(),
  // Admin-only:
  vectorId: z.string().uuid().nullable().optional(),
  characterCount: z.number().int().nullable().optional(),
  elementType: z.string().nullable().optional(),
  embeddingStatus: z.string().nullable().optional(),
  parentChunkId: z.string().uuid().nullable().optional(),
});
export type KbChunkDetail = z.infer<typeof kbChunkDetailSchema>;

export const kbChunkMatchSchema = z.object({
  chunkId: z.string().uuid(),
  headingPath: z.array(z.string()),
  snippet: z.string(),
  rank: z.number(),
  pageNumber: z.number().int().nullable().optional(),
  position: z.number().int().nonnegative(),
});
export type KbChunkMatch = z.infer<typeof kbChunkMatchSchema>;

export const kbChunkSearchResultSchema = z.object({
  matches: z.array(kbChunkMatchSchema),
  totalCount: z.number().int().nonnegative(),
  skip: z.number().int().nonnegative(),
  take: z.number().int().positive(),
});
export type KbChunkSearchResult = z.infer<typeof kbChunkSearchResultSchema>;
```

- [ ] **Step 2: Re-export from schemas/index.ts**

Append to `apps/web/src/lib/api/schemas/index.ts`:

```typescript
export * from './kb-document.schemas';
```

- [ ] **Step 3: Extend knowledgeBaseClient.ts with 4 methods**

Open `apps/web/src/lib/api/clients/knowledgeBaseClient.ts`. Find the existing pattern (e.g. `getGameDocuments`) and add four sibling methods:

```typescript
import {
  kbDocumentSchema, type KbDocument,
  kbChunkListSchema, type KbChunkList,
  kbChunkDetailSchema, type KbChunkDetail,
  kbChunkSearchResultSchema, type KbChunkSearchResult,
} from '@/lib/api/schemas/kb-document.schemas';

export const knowledgeBaseClient = {
  // ... existing methods,

  async getKbDocument(docId: string): Promise<KbDocument> {
    const res = await fetchJson(`/api/v1/kb-docs/${docId}`);
    return kbDocumentSchema.parse(res);
  },

  async getKbChunks(docId: string, params: { skip?: number; take?: number } = {}): Promise<KbChunkList> {
    const search = new URLSearchParams();
    if (params.skip !== undefined) search.set('skip', String(params.skip));
    if (params.take !== undefined) search.set('take', String(params.take));
    const qs = search.toString();
    const url = `/api/v1/kb-docs/${docId}/chunks${qs ? `?${qs}` : ''}`;
    const res = await fetchJson(url);
    return kbChunkListSchema.parse(res);
  },

  async getKbChunk(docId: string, chunkId: string): Promise<KbChunkDetail> {
    const res = await fetchJson(`/api/v1/kb-docs/${docId}/chunks/${chunkId}`);
    return kbChunkDetailSchema.parse(res);
  },

  async searchKbChunks(
    docId: string,
    body: { query: string; skip?: number; take?: number },
  ): Promise<KbChunkSearchResult> {
    const res = await fetchJson(`/api/v1/kb-docs/${docId}/chunks/search`, {
      method: 'POST',
      body: JSON.stringify(body),
      headers: { 'Content-Type': 'application/json' },
    });
    return kbChunkSearchResultSchema.parse(res);
  },
};
```

(Adapt the `fetchJson` import and the structure of the existing client to the actual file — the four methods above are the new additions.)

- [ ] **Step 4: Run frontend typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: zero errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/kb-document.schemas.ts apps/web/src/lib/api/schemas/index.ts apps/web/src/lib/api/clients/knowledgeBaseClient.ts
git commit -m "feat(kb): #730 frontend Zod schemas + client methods

Adds kb-document.schemas.ts mirroring backend DTOs, plus four client
methods (getKbDocument, getKbChunks, getKbChunk, searchKbChunks).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 14: Frontend — React Query hooks

**Files:**
- Create: `apps/web/src/hooks/queries/useKbDocument.ts`
- Create: `apps/web/src/hooks/queries/useKbChunks.ts`
- Create: `apps/web/src/hooks/queries/useKbChunk.ts`
- Create: `apps/web/src/hooks/queries/useKbChunkSearch.ts`
- Modify: `apps/web/src/hooks/queries/index.ts`

- [ ] **Step 1: Create useKbDocument**

`apps/web/src/hooks/queries/useKbDocument.ts`:

```typescript
'use client';

import { useQuery } from '@tanstack/react-query';
import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';
import type { KbDocument } from '@/lib/api/schemas/kb-document.schemas';

export const kbDocumentKeys = {
  all: ['kb-document'] as const,
  byId: (docId: string) => ['kb-document', docId] as const,
} as const;

export function useKbDocument(docId: string | undefined, enabled = true) {
  return useQuery<KbDocument, Error>({
    queryKey: kbDocumentKeys.byId(docId ?? ''),
    queryFn: () => knowledgeBaseClient.getKbDocument(docId!),
    enabled: enabled && !!docId,
    staleTime: 60_000,    // 60s — metadata may change during ingestion
    gcTime: 5 * 60_000,
  });
}
```

- [ ] **Step 2: Create useKbChunks**

```typescript
'use client';

import { useQuery, keepPreviousData } from '@tanstack/react-query';
import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';
import type { KbChunkList } from '@/lib/api/schemas/kb-document.schemas';

export const kbChunksKeys = {
  all: ['kb-chunks'] as const,
  list: (docId: string, skip: number, take: number) =>
    ['kb-chunks', docId, { skip, take }] as const,
} as const;

export function useKbChunks(
  docId: string | undefined,
  skip = 0,
  take = 50,
  enabled = true,
) {
  return useQuery<KbChunkList, Error>({
    queryKey: kbChunksKeys.list(docId ?? '', skip, take),
    queryFn: () => knowledgeBaseClient.getKbChunks(docId!, { skip, take }),
    enabled: enabled && !!docId,
    staleTime: 5 * 60_000,
    gcTime: 10 * 60_000,
    placeholderData: keepPreviousData,
  });
}
```

- [ ] **Step 3: Create useKbChunk**

```typescript
'use client';

import { useQuery } from '@tanstack/react-query';
import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';
import type { KbChunkDetail } from '@/lib/api/schemas/kb-document.schemas';

export const kbChunkKeys = {
  all: ['kb-chunk'] as const,
  byId: (docId: string, chunkId: string) =>
    ['kb-chunk', docId, chunkId] as const,
} as const;

export function useKbChunk(
  docId: string | undefined,
  chunkId: string | undefined,
  enabled = true,
) {
  return useQuery<KbChunkDetail, Error>({
    queryKey: kbChunkKeys.byId(docId ?? '', chunkId ?? ''),
    queryFn: () => knowledgeBaseClient.getKbChunk(docId!, chunkId!),
    enabled: enabled && !!docId && !!chunkId,
    staleTime: 10 * 60_000, // chunk content immutable post-ingest
    gcTime: 30 * 60_000,
  });
}
```

- [ ] **Step 4: Create useKbChunkSearch**

```typescript
'use client';

import { useMutation } from '@tanstack/react-query';
import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';
import type { KbChunkSearchResult } from '@/lib/api/schemas/kb-document.schemas';

export function useKbChunkSearch() {
  return useMutation<
    KbChunkSearchResult,
    Error,
    { docId: string; query: string; skip?: number; take?: number }
  >({
    mutationFn: ({ docId, query, skip, take }) =>
      knowledgeBaseClient.searchKbChunks(docId, { query, skip, take }),
  });
}
```

(Search is a mutation rather than a query: each call has a different `query` string, results don't naturally cache, and the user typically triggers it explicitly.)

- [ ] **Step 5: Re-export from hooks/queries/index.ts**

Append:

```typescript
export * from './useKbDocument';
export * from './useKbChunks';
export * from './useKbChunk';
export * from './useKbChunkSearch';
```

- [ ] **Step 6: Run frontend typecheck and lint**

```bash
cd apps/web
pnpm typecheck && pnpm lint
```

Expected: zero errors.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/hooks/queries/useKbDocument.ts apps/web/src/hooks/queries/useKbChunks.ts apps/web/src/hooks/queries/useKbChunk.ts apps/web/src/hooks/queries/useKbChunkSearch.ts apps/web/src/hooks/queries/index.ts
git commit -m "feat(kb): #730 frontend React Query hooks for chunk endpoints

useKbDocument (60s TTL), useKbChunks (5min TTL with keepPreviousData),
useKbChunk (10min TTL — immutable), useKbChunkSearch (mutation).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 15: PR creation + close-out

- [ ] **Step 1: Push the branch**

```bash
git push -u origin feature/issue-730-kb-chunk-endpoints
```

- [ ] **Step 2: Open the PR against `main-dev` (NOT main)**

```bash
gh pr create --base main-dev --title "feat(kb): #730 chunk-level retrieval endpoints (Wave 3 prereq)" --body "$(cat <<'EOF'
## Summary

- 4 new endpoints + 1 search endpoint under `/api/v1/kb-docs/{id}` to unblock Wave 3 child #684 `/kb/[id]` split-view UI
- Migration adds `heading`, `parent_chunk_id`, `level`, `element_type` to `text_chunks` (normalized hierarchy mirroring Qdrant payload)
- DTO field gating: admin-only fields (`vectorId`, `characterCount`, `elementType`, `embeddingStatus`, `processingError`, `retryCount`, `failedAtState`) gated server-side and stripped from JSON wire via `[JsonIgnore(WhenWritingNull)]`
- Frontend Zod schemas + 4 React Query hooks ready for migration wave consumption
- Spec: `docs/superpowers/specs/2026-05-06-kb-chunk-endpoints-design.md`
- Plan: `docs/superpowers/plans/2026-05-06-kb-chunk-endpoints-plan.md`

## Out of scope (follow-up issues)
- Backfill of legacy `text_chunks` rows from Qdrant (new issue to be filed)
- Cursor-based pagination (only if offset becomes a perf problem)
- Vector semantic search inside a single doc (YAGNI)

## Test plan
- [ ] `dotnet test --filter "BoundedContext=KnowledgeBase"` passes
- [ ] `dotnet test --filter "Category=Integration"` passes (Testcontainers)
- [ ] `pnpm typecheck` passes
- [ ] Manual: hit `/api/v1/kb-docs/{id}` via Scalar and verify DTO shape
- [ ] Manual: search for a known keyword in an ingested doc and verify `<mark>` highlighting

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Comment on issue #684 to unblock the FE migration wave**

```bash
gh issue comment 684 --body "Backend prerequisites for /kb/[id] (#730) are now in PR \$(gh pr view --json url -q .url). New hooks available:

- \`useKbDocument(docId)\` — header metadata
- \`useKbChunks(docId, skip, take)\` — paginated list with hierarchical breadcrumb
- \`useKbChunk(docId, chunkId)\` — full markdown content + prev/next nav
- \`useKbChunkSearch()\` — in-doc keyword search (mutation)

Zod schemas in \`apps/web/src/lib/api/schemas/kb-document.schemas.ts\`.
Phase 0.5 contract for \`/kb/[id]\` can now be written referencing these endpoints."
```

- [ ] **Step 4: Close-out comment on #730**

```bash
gh issue comment 730 --body "PR opened: \$(gh pr view --json url -q .url)

5 endpoints delivered:
- \`GET /api/v1/kb-docs/{id}\`
- \`GET /api/v1/kb-docs/{id}/chunks?skip&take\`
- \`GET /api/v1/kb-docs/{id}/chunks/{chunkId}\`
- \`POST /api/v1/kb-docs/{id}/chunks/search\`
- (G5 admin diagnostic fields embedded in chunk endpoints via DTO gating)

Migration: \`AddChunkHierarchyToTextChunkEntity\` adds heading + parent_chunk_id + level + element_type to text_chunks. Backfill of legacy rows tracked separately.

Closes #730 once PR merges."
```

---

## Spec coverage note: Caching (D8) deferred

D8 of the spec proposes HybridCache wrapping (G1 list 5min · G2 single-chunk 10min · G4 metadata 60s). This plan **defers caching wiring** to a follow-up commit/issue — rationale:

- Caching is a non-functional optimization (latency), not a correctness requirement
- Cold P95 targets in the spec (`<200ms` for G1, `<250ms` for G2, `<200ms` for G4) are likely achievable without cache given that the queries are simple SELECTs against indexed columns. Measure first, add cache only if SLO is missed
- Adding cache later is a straightforward wrap of `mediator.Send(...)` in the routing handler; no DTO/migration changes needed
- Avoids early commitment to cache invalidation logic on re-ingestion (which would complicate Task 3)

If post-merge measurements show cold P95 exceeding spec targets, file a follow-up issue and add `IHybridCacheService.GetOrCreateAsync` wraps in `KnowledgeBaseEndpoints.cs` for G1, G2, G4 (search remains uncached).

---

## Summary of commits (15 total)

1. `feat(kb): #730 add chunk hierarchy fields to TextChunkEntity`
2. `feat(kb): #730 EF migration AddChunkHierarchyToTextChunkEntity`
3. `feat(kb): #730 persist chunk hierarchy on ingestion`
4. `feat(kb): #730 G4 GetKbDocumentByIdQuery handler`
5. `feat(kb): #730 G4 endpoint /api/v1/kb-docs/{id}`
6. `feat(kb): #730 G1 GetKbChunksQuery skeleton (headingPath stub)`
7. `feat(kb): #730 G1 headingPath via recursive CTE`
8. `feat(kb): #730 G1 endpoint /api/v1/kb-docs/{id}/chunks`
9. `feat(kb): #730 G2 GetKbChunkByIdQuery handler`
10. `feat(kb): #730 G2 endpoint /api/v1/kb-docs/{id}/chunks/{chunkId}`
11. `feat(kb): #730 G3 SearchKbChunksQuery handler`
12. `feat(kb): #730 G3 endpoint POST /api/v1/kb-docs/{id}/chunks/search`
13. `feat(kb): #730 frontend Zod schemas + client methods`
14. `feat(kb): #730 frontend React Query hooks for chunk endpoints`
15. (PR open, no extra commit)

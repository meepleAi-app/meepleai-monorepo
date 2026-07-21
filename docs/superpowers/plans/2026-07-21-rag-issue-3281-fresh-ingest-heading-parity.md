# Issue #3281 — Fresh-Upload Ingest Heading-Aware Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire heading-aware hierarchical chunking into the 3 fresh-upload ingest paths (so freshly-uploaded PDFs get the same heading-bearing chunks the re-index path already produces), fix the pre-existing Upload-path persistence bug, and add a real-Postgres round-trip test.

**Architecture:** PR #3282 ("Slice D") already shipped the shared infrastructure — `HeadingAwareChunker.BuildAsync`, `HierarchicalChunkMapper`, `PdfDocumentEntity.StructuredElementsJson` + migration, the `MaxEmbeddingChars` cap, and the `IndexPdfCommandHandler` (re-index) wiring. This plan **reuses that infra unchanged** and routes the 3 fresh-ingest handlers' chunk production through the same `HeadingAwareChunker.BuildAsync`. The persist sites (`new TextChunkEntity { … Heading, Level, ParentChunkId, ElementType … }` + `Id == Guid.Empty ? Guid.NewGuid()` fallback) already carry hierarchy fields in all 3 handlers — they just receive flat chunks today. So each task swaps the *producer* (`PrepareForEmbedding`/`ChunkText` → `BuildAsync`), maps `DocumentChunk → DocumentChunkInput`, and caps embedding-input text. The Upload path additionally needs a persistence fix because its working entity is `AsNoTracking()` and its state transitions route through a repository that clobbers content columns.

**Tech Stack:** .NET 9, EF Core (pgvector), MediatR/CQRS, xUnit + Moq + FluentAssertions + Testcontainers.

## Global Constraints

- **Reuse, do NOT duplicate.** Use the merged `HeadingAwareChunker.BuildAsync` — do NOT create a second chunker, mapper, migration, or `StructuredElementsJson` column. Its exact signature (static class, namespace `Api.BoundedContexts.DocumentProcessing.Application.Services`):
  ```csharp
  public static Task<List<DocumentChunk>> BuildAsync(
      IReadOnlyList<ExtractedElement>? structuredElements,
      string flatText,
      Guid documentId,
      Guid? gameId,
      IAdvancedChunkingService advancedChunking,
      CancellationToken cancellationToken)
  ```
- `IAdvancedChunkingService` — namespace `Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking`, registered `AddScoped` in `KnowledgeBaseServiceExtensions.cs:542`.
- `ExtractedElement` — `Api.BoundedContexts.DocumentProcessing.Domain.Services.ExtractedElement`, a `record ExtractedElement(string Text, int PageNumber, string ElementType)`. `PagedTextExtractionResult.StructuredElements` is `IReadOnlyList<ExtractedElement>?`.
- `DocumentChunk` (`Api.Services`, `apps/api/src/Api/Services/VectorSearchModels.cs:9`) and `DocumentChunkInput` (`Api.Services`, `apps/api/src/Api/Services/TextChunkingService.cs:391`) are both init-only `record`s carrying: `Guid Id` (default `Guid.Empty`), `string Text`, `int Page`, `int CharStart`, `int CharEnd`, `string? Heading`, `short Level` (default `1`), `Guid? ParentChunkId`, `string ElementType` (default `"NarrativeText"`). `DocumentChunk` also has `float[] Embedding`.
- **Embedding cap:** `ChunkingConstants.MaxEmbeddingChars = 1800` (namespace `Api.Constants`). Cap ONLY the text sent to the embedding provider; the persisted `Content`/`text_chunks`/pgvector row keeps the FULL `chunk.Text`. Mirror `IndexPdfCommandHandler`'s `CapTextForEmbedding`.
- **StructuredElementsJson serialize** (mirror `PdfProcessingPipelineService.cs:496-498`):
  ```csharp
  extractResult.StructuredElements is null ? null : JsonSerializer.Serialize(extractResult.StructuredElements)
  ```
- **Do NOT route Upload's extraction-column persistence through `IPdfDocumentRepository`** — `PdfDocumentRepository.MapToPersistence` omits `ExtractedText`/`StructuredElementsJson`/`CharacterCount`/`ExtractedTables` and `DbSet.Update()` marks the whole row Modified, nulling them. Persist on a **tracked** entity.
- **Backward-compat DI:** when `IAdvancedChunkingService` is unavailable (null), fall back to the existing flat chunk production so pre-existing test constructors/scopes keep compiling and passing. Pipeline → trailing optional nullable ctor param; Upload/Complete → `scope.ServiceProvider.GetService<IAdvancedChunkingService>()` (nullable). `null → flat fallback`.
- **Persist sites are already correct** — do NOT re-edit the `new TextChunkEntity { … }` blocks (they carry `Id == Guid.Empty ? Guid.NewGuid()` + `Heading`/`Level`/`ParentChunkId`/`ElementType` + role-tagging). Just feed them hierarchy-bearing chunks.
- **Out of scope** (per #3281): `EnhancedPdfProcessorOrchestrator.SplitOversizedPageChunks` (pre-extraction page-level splitting on `PageTextChunk`, no hierarchy/identity).
- **Git:** branch `feature/issue-3281-fresh-ingest-heading-parity` (already created, parent `main-dev`). Commit per task. PR → `main-dev`, body `Closes #3281`. Commit/PR footer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. Background all `git commit` (slow pre-commit hook). Never `--no-verify`.

---

### Task 1: Shared `HeadingAwareChunkAdapter` (DocumentChunk→DocumentChunkInput map + embedding cap)

Both `BuildAsync`'s output (`List<DocumentChunk>`) and the 3 handlers' downstream embed/persist code (`List<DocumentChunkInput>`) need bridging, and all 3 handlers need the same embedding-input cap. One tested static helper removes the duplication.

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HeadingAwareChunkAdapter.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HeadingAwareChunkAdapterTests.cs`

**Interfaces:**
- Consumes: `DocumentChunk` (`Api.Services`), `DocumentChunkInput` (`Api.Services`), `ChunkingConstants.MaxEmbeddingChars` (`Api.Constants`).
- Produces:
  - `static List<DocumentChunkInput> ToChunkInputs(IReadOnlyList<DocumentChunk> chunks)` — preserves `Id`, `Text`, `Page`, `CharStart`, `CharEnd`, `Heading`, `Level`, `ParentChunkId`, `ElementType`.
  - `static string CapForEmbedding(string text)` — returns `text` unchanged if `text.Length <= MaxEmbeddingChars`, else `text[..MaxEmbeddingChars]`.

- [ ] **Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.Constants;
using Api.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class HeadingAwareChunkAdapterTests
{
    [Fact]
    public void ToChunkInputs_PreservesIdentityAndHierarchyFields()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var chunks = new List<DocumentChunk>
        {
            new() { Id = parentId, Text = "Setup", Page = 2, CharStart = 10, CharEnd = 15,
                    Heading = "Setup", Level = 0, ParentChunkId = null, ElementType = "Title" },
            new() { Id = childId, Text = "Place 3 tiles", Page = 2, CharStart = 16, CharEnd = 29,
                    Heading = "Setup", Level = 2, ParentChunkId = parentId, ElementType = "NarrativeText" },
        };

        var result = HeadingAwareChunkAdapter.ToChunkInputs(chunks);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(parentId);
        result[0].Heading.Should().Be("Setup");
        result[0].Level.Should().Be((short)0);
        result[0].ElementType.Should().Be("Title");
        result[1].Id.Should().Be(childId);
        result[1].ParentChunkId.Should().Be(parentId);
        result[1].Level.Should().Be((short)2);
        result[1].Text.Should().Be("Place 3 tiles");
        result[1].CharStart.Should().Be(16);
    }

    [Fact]
    public void CapForEmbedding_TruncatesOnlyWhenOverLimit()
    {
        var under = new string('a', ChunkingConstants.MaxEmbeddingChars);
        var over = new string('b', ChunkingConstants.MaxEmbeddingChars + 500);

        HeadingAwareChunkAdapter.CapForEmbedding(under).Should().HaveLength(ChunkingConstants.MaxEmbeddingChars);
        HeadingAwareChunkAdapter.CapForEmbedding(over).Should().HaveLength(ChunkingConstants.MaxEmbeddingChars);
        HeadingAwareChunkAdapter.CapForEmbedding("short").Should().Be("short");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HeadingAwareChunkAdapterTests" -v minimal` (from `apps/api/src/Api`)
Expected: FAIL — `HeadingAwareChunkAdapter` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using Api.Constants;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// Bridges <see cref="HeadingAwareChunker"/>'s <see cref="DocumentChunk"/> output to the
/// <see cref="DocumentChunkInput"/> the fresh-upload ingest handlers embed + persist, and
/// caps embedding-input text at <see cref="ChunkingConstants.MaxEmbeddingChars"/> (the full
/// text is still persisted for retrieval; only the vector-provider input is capped). Shared
/// by all 3 fresh-ingest paths (Issue #3281) so the map + cap logic lives in one tested place.
/// </summary>
internal static class HeadingAwareChunkAdapter
{
    public static List<DocumentChunkInput> ToChunkInputs(IReadOnlyList<DocumentChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return chunks
            .Select(c => new DocumentChunkInput
            {
                Id = c.Id,
                Text = c.Text,
                Page = c.Page,
                CharStart = c.CharStart,
                CharEnd = c.CharEnd,
                Heading = c.Heading,
                Level = c.Level,
                ParentChunkId = c.ParentChunkId,
                ElementType = c.ElementType,
            })
            .ToList();
    }

    public static string CapForEmbedding(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length <= ChunkingConstants.MaxEmbeddingChars
            ? text
            : text[..ChunkingConstants.MaxEmbeddingChars];
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HeadingAwareChunkAdapterTests" -v minimal`
Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HeadingAwareChunkAdapter.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HeadingAwareChunkAdapterTests.cs
git commit -m "feat(chunking): shared DocumentChunk→Input adapter + embedding cap (#3281)"
```

---

### Task 2: Wire `PdfProcessingPipelineService` (re-process/Quartz path) + translation hierarchy copy

The stale-recovery/shared-game path. `pdfDoc` is a **tracked** entity (`FindAsync`), `extractResult.StructuredElements` is in scope, and this path **already persists `StructuredElementsJson`** — so only the producer + translation copy change here.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`
  - ctor (lines 66-111): add trailing optional `IAdvancedChunkingService? advancedChunking = null` + field.
  - `ChunkText` (line 658) → make it `async Task<List<DocumentChunkInput>> ChunkTextAsync(...)` threading `Guid documentId, Guid? gameId`, call `BuildAsync` when `_advancedChunking != null`.
  - call site (line 191) → `await ChunkTextAsync(fullText, extractResult, pdfDoc.Id, pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId, cancellationToken)`.
  - translation branch (lines 224-238) → copy hierarchy fields from `origChunk`.
  - `GenerateEmbeddingsAsync` (line 688) → cap batch texts via `HeadingAwareChunkAdapter.CapForEmbedding`.
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineHeadingAwareTests.cs` (new)

**Interfaces:**
- Consumes: `HeadingAwareChunker.BuildAsync`, `HeadingAwareChunkAdapter.ToChunkInputs`/`CapForEmbedding`, `IAdvancedChunkingService`.
- Produces: heading-bearing `DocumentChunkInput` list from `ChunkTextAsync`; translated chunks carrying `Heading`/`Level`/`ParentChunkId`/`ElementType`.

- [ ] **Step 1: Write the failing test**

Model it on the existing pipeline unit tests in the same folder (look for `PdfProcessingPipeline*Tests.cs` — reuse their fixture/mocks). The test builds a `PdfProcessingPipelineService` with a mocked `IAdvancedChunkingService` whose `ChunkDocumentAsync` returns a parent (`Level 0`, `Heading "Setup"`) + child (`Level 2`, `ParentChunkId = parent`), drives `ProcessAsync` (or calls the now-`internal` `ChunkTextAsync` via `InternalsVisibleTo`-exposed access if the class exposes it — otherwise assert through the persisted `text_chunks` in an in-memory `MeepleAiDbContext`), and asserts the produced chunk inputs carry `Heading == "Setup"` and a non-null `ParentChunkId` on the child.

```csharp
// Skeleton — adapt mocks to the existing pipeline test fixture in this folder.
[Fact]
public async Task ChunkTextAsync_WithAdvancedChunking_ProducesHeadingBearingChunks()
{
    // Arrange: mock IAdvancedChunkingService.ChunkDocumentAsync → [parent Level0 "Setup", child Level2]
    // Build PdfProcessingPipelineService with that mock in the new trailing ctor slot.
    // Act: invoke the chunk production for a doc whose extractResult.StructuredElements is non-empty.
    // Assert: result has a chunk with Heading "Setup"; a child chunk with ParentChunkId != null and Level 2.
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PdfProcessingPipelineHeadingAware" -v minimal`
Expected: FAIL (flat production, no `Heading`).

- [ ] **Step 3: Write the implementation**

ctor — add field + trailing optional param (after `IPdfCoverUploadPipeline? pdfCoverUploadPipeline = null`):
```csharp
    // Issue #3281: optional so pre-existing test constructors compile. When null,
    // chunk production falls back to the flat ITextChunkingService path (pre-Slice-D behaviour).
    private readonly IAdvancedChunkingService? _advancedChunking;
    // ...ctor params: add `IAdvancedChunkingService? advancedChunking = null,` in the trailing-optional block
    // ...ctor body:
    _advancedChunking = advancedChunking;
```
Add `using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;` and `using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;` (for `IAdvancedChunkingService`).

Replace `ChunkText` (line 658) with:
```csharp
    private async Task<List<DocumentChunkInput>> ChunkTextAsync(
        string fullText,
        PagedTextExtractionResult extractResult,
        Guid documentId,
        Guid? gameId,
        CancellationToken cancellationToken)
    {
        // Issue #3281: heading-aware production when AdvancedChunkingService is available.
        if (_advancedChunking != null)
        {
            var hierarchical = await HeadingAwareChunker.BuildAsync(
                extractResult.StructuredElements,
                fullText,
                documentId,
                gameId,
                _advancedChunking,
                cancellationToken).ConfigureAwait(false);
            return HeadingAwareChunkAdapter.ToChunkInputs(hierarchical);
        }

        // Fallback: flat production (pre-Slice-D behaviour) when the chunker is unavailable.
        var chunks = _chunkingService.PrepareForEmbedding(fullText, ChunkSize, ChunkOverlap)
            ?.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text))
            .ToList()
            ?? [];
        if (chunks.Count == 0)
        {
            foreach (var pageChunk in extractResult.PageChunks.Where(pc => !pc.IsEmpty))
            {
                var pageTextChunks = _chunkingService.ChunkText(pageChunk.Text, ChunkSize, ChunkOverlap);
                foreach (var textChunk in pageTextChunks.Where(t => !string.IsNullOrWhiteSpace(t.Text)))
                {
                    chunks.Add(new DocumentChunkInput
                    {
                        Text = textChunk.Text,
                        Page = pageChunk.PageNumber,
                        CharStart = textChunk.CharStart,
                        CharEnd = textChunk.CharEnd
                    });
                }
            }
        }
        return chunks.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();
    }
```

Call site (line 191): `var chunks = await ChunkTextAsync(fullText, extractResult, pdfDoc.Id, pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId, cancellationToken).ConfigureAwait(false);`

Translation branch (lines 224-238) — copy hierarchy fields + remove the stale `// Issue #730 / spec §5.3 forward-wiring` TODO comment (now done):
```csharp
        var origChunk = chunks[t.OriginalIndex];
        translatedChunks.Add((
            new DocumentChunkInput
            {
                Id = origChunk.Id,
                Text = t.TranslatedText,
                Page = origChunk.Page,
                CharStart = origChunk.CharStart,
                CharEnd = origChunk.CharEnd,
                Heading = origChunk.Heading,
                Level = origChunk.Level,
                ParentChunkId = origChunk.ParentChunkId,
                ElementType = origChunk.ElementType
            },
            "en",
            true));
```

`GenerateEmbeddingsAsync` (line 688) — cap the batch text list. Find the `.Select(... => ....Text)` that builds `batchTexts` and wrap each with `HeadingAwareChunkAdapter.CapForEmbedding(...)`.

- [ ] **Step 4: Run tests**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PdfProcessingPipeline" -v minimal`
Expected: new heading-aware test PASS; pre-existing pipeline tests still PASS (flat fallback path unchanged when no chunker injected).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineHeadingAwareTests.cs
git commit -m "feat(chunking): heading-aware chunking in PdfProcessingPipeline + translation hierarchy copy (#3281)"
```

---

### Task 3: Wire `UploadPdfCommandHandler.Processing` (regular + private-game upload)

Widen `ChunkExtractedTextAsync` to thread `pdfDoc` (for `gameId`/`documentId`) and consume `extractResult.StructuredElements`, resolve `IAdvancedChunkingService` from the async scope (nullable → flat fallback), and cap embedding input. **StructuredElementsJson persistence is Task 4** — this task only makes the produced chunks heading-aware.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`
  - `ChunkExtractedTextAsync` (line 337): add `PdfDocumentEntity pdfDoc` param; resolve `IAdvancedChunkingService?` from `scope`; `BuildAsync` when non-null else existing flat body.
  - call site (line 68): pass `pdfDoc`.
  - `GenerateAndValidateEmbeddingsAsync` batch texts (line 429): cap via `HeadingAwareChunkAdapter.CapForEmbedding`.
- Test: extend/add a handler-driven test asserting produced chunks carry `Heading` when a mocked `IAdvancedChunkingService` is registered in the scope.

**Interfaces:**
- Consumes: `HeadingAwareChunker.BuildAsync`, `HeadingAwareChunkAdapter`, `IAdvancedChunkingService` (scope-resolved), `extractResult.StructuredElements`.
- Produces: heading-bearing `DocumentChunkInput` from `ChunkExtractedTextAsync`.

- [ ] **Step 1: Write the failing test**

Locate the existing Upload processing tests (grep `ChunkExtractedTextAsync` / `UploadPdfCommandHandler` tests) and follow their scope-building pattern. Register a mocked `IAdvancedChunkingService` in the test scope returning a parent+child hierarchy; drive the chunk step; assert the persisted `text_chunks` (in-memory DbContext) carry `Heading` non-null + a child `ParentChunkId`. If no such harness exists, add a focused test that constructs the handler, builds a scope with the mock + an in-memory `MeepleAiDbContext`, and invokes the processing entrypoint for a small PDF.

```csharp
[Fact]
public async Task ChunkExtractedText_WithAdvancedChunking_PersistsHeadingBearingChunks()
{
    // Arrange: scope with mocked IAdvancedChunkingService → [parent Level0 "Setup", child Level2],
    //          in-memory MeepleAiDbContext, a Ready-bound small PDF with non-empty StructuredElements.
    // Act: run the upload processing pipeline.
    // Assert: text_chunks for the pdf have a row with Heading "Setup" and a child with ParentChunkId != null.
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdf&FullyQualifiedName~HeadingAware" -v minimal` (adjust filter to the test name)
Expected: FAIL (flat chunks, `Heading` null).

- [ ] **Step 3: Write the implementation**

Add usings: `using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;` and `using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;`.

`ChunkExtractedTextAsync` (line 337) — add `PdfDocumentEntity pdfDoc` param (place it before `db`), and replace the primary production:
```csharp
    private async Task<List<DocumentChunkInput>> ChunkExtractedTextAsync(
        string pdfId,
        string fullText,
        PagedTextExtractionResult extractResult,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
        const int chunkSize = 512;
        const int chunkOverlap = 50;

        // Issue #3281: heading-aware production when AdvancedChunkingService is available in scope.
        var advancedChunking = scope.ServiceProvider.GetService<IAdvancedChunkingService>();
        if (advancedChunking != null)
        {
            var hierarchical = await HeadingAwareChunker.BuildAsync(
                extractResult.StructuredElements,
                fullText,
                pdfDoc.Id,
                pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId,
                advancedChunking,
                cancellationToken).ConfigureAwait(false);
            return HeadingAwareChunkAdapter.ToChunkInputs(hierarchical);
        }

        // Fallback: existing flat production (primary PrepareForEmbedding + per-page ChunkText fallback).
        // ... KEEP the existing body from `var allDocumentChunks = chunkingService.PrepareForEmbedding(...)`
        //     through the fallback loop and return, unchanged ...
    }
```
(Preserve the existing flat body verbatim under the fallback — do not delete it.)

Call site (line 68): `var allDocumentChunks = await ChunkExtractedTextAsync(pdfId, fullText!, extractResult!, pdfDoc, db, scope, startTime, cancellationToken).ConfigureAwait(false);`

`GenerateAndValidateEmbeddingsAsync` (line 429): `var batchTexts = batchChunks.Select(c => HeadingAwareChunkAdapter.CapForEmbedding(c.Text)).ToList();`

- [ ] **Step 4: Run tests**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdf" -v minimal`
Expected: new heading-aware test PASS; pre-existing Upload tests PASS (scopes without the chunker registered → flat fallback).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs apps/api/tests/Api.Tests/...
git commit -m "feat(chunking): heading-aware chunking in UploadPdf ingest path (#3281)"
```

---

### Task 4: Persist `ExtractedText` + `StructuredElementsJson` for the Upload path (AsNoTracking fix)

Pre-existing bug (not in #3281's original body): the Upload pipeline's working `pdfDoc` is `AsNoTracking()` (`Processing.cs:205`) AND state transitions route through `IPdfDocumentRepository.UpdateAsync → MapToPersistence` (which omits the extraction columns and clobbers them to NULL via whole-row `Update()`), so `ExtractedText`/`StructuredElementsJson`/`PageCount`/table columns **never persist** for Upload PDFs. This blocks re-index parity (`IndexPdfCommandHandler` reads `pdf.StructuredElementsJson` to rebuild headings, and fails `TextExtractionRequired` on null `ExtractedText`). Fix: set `StructuredElementsJson` in-memory at extraction, then re-persist all extraction columns on a freshly **tracked** entity AFTER the final Ready transition (mirrors `ExtractPdfTextCommandHandler`'s tracked-write pattern).

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`
  - `ExtractPdfContentAsync` (~line 260): set `pdfDoc.StructuredElementsJson = …Serialize(…)` in-memory (so the tracked re-write can copy it).
  - `FinalizeProcessingAsync` (~lines 872-916): after the Ready `SaveChangesAsync`, add a dedicated tracked re-write.
- Test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/UploadPdfExtractionPersistenceIntegrationTests.cs` (Testcontainers) OR extend an existing Upload integration test — assert persisted `ExtractedText` + `StructuredElementsJson` non-null after the pipeline reaches Ready.

**Interfaces:**
- Consumes: `StructuredElementsJson` column, `DbUpdateConcurrencyException`, `db.PdfDocuments.AsTracking()`.
- Produces: durable `pdf_documents.ExtractedText` + `StructuredElementsJson` for Upload-originated PDFs.

- [ ] **Step 1: Write the failing test (Testcontainers)**

Use the `SharedTestcontainersFixture` pattern from `apps/api/tests/Api.Tests/Integration/DocumentProcessing/PdfPipelineIntegrationTests.cs`. Drive a small titled PDF through the Upload background pipeline to Ready, then re-query:
```csharp
var persisted = await dbContext.PdfDocuments.AsNoTracking()
    .FirstAsync(p => p.Id == pdfGuid, TestContext.Current.CancellationToken);
persisted.ExtractedText.Should().NotBeNullOrEmpty();
persisted.StructuredElementsJson.Should().NotBeNull();
```
Run → RED (both null today).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdfExtractionPersistence" -v minimal`
Expected: FAIL — `ExtractedText`/`StructuredElementsJson` null.

- [ ] **Step 3: Write the implementation**

At `ExtractPdfContentAsync` (~line 260), alongside `pdfDoc.ExtractedText = fullText;`:
```csharp
        pdfDoc.ExtractedText = fullText;
        pdfDoc.StructuredElementsJson = extractResult.StructuredElements is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(extractResult.StructuredElements);
```
(This assignment is a no-op on the untracked entity for the DB, but keeps the value in-memory for the tracked re-write. The `db.SaveChangesAsync` here stays as-is.)

In `FinalizeProcessingAsync`, AFTER the Ready transition's `try { … TransitionTo(Ready); … SaveChangesAsync } catch (DbUpdateConcurrencyException …) { return; }` block:
```csharp
        // Issue #3281 / pre-existing AsNoTracking bug: the pipeline's working pdfDoc is AsNoTracking
        // and state transitions route through IPdfDocumentRepository (MapToPersistence omits + clobbers
        // ExtractedText/StructuredElementsJson/content columns), so extraction output written during
        // ExtractPdfContentAsync/ExtractStructuredContentAsync never persists. Re-persist it here on a
        // freshly TRACKED entity AFTER the final Ready transition (mirrors ExtractPdfTextCommandHandler),
        // so no later repository transition can clobber it. Needed for re-index parity: IndexPdf reads
        // StructuredElementsJson and requires non-null ExtractedText.
        var tracked = await db.PdfDocuments.AsTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken).ConfigureAwait(false);
        if (tracked != null)
        {
            tracked.ExtractedText = pdfDoc.ExtractedText;
            tracked.StructuredElementsJson = pdfDoc.StructuredElementsJson;
            tracked.PageCount = pdfDoc.PageCount;
            tracked.CharacterCount = pdfDoc.CharacterCount;
            tracked.ExtractedTables = pdfDoc.ExtractedTables;
            tracked.ExtractedDiagrams = pdfDoc.ExtractedDiagrams;
            tracked.AtomicRules = pdfDoc.AtomicRules;
            tracked.TableCount = pdfDoc.TableCount;
            tracked.DiagramCount = pdfDoc.DiagramCount;
            tracked.AtomicRuleCount = pdfDoc.AtomicRuleCount;
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex,
                    "Concurrency conflict persisting extraction output for PDF {PdfId} — admin mutation wins",
                    pdfId);
            }
        }
```
Verify the in-scope names (`pdfGuid`, `db`, `pdfDoc`, `cancellationToken`, `_logger`, `pdfId`) against the actual `FinalizeProcessingAsync` signature; thread/resolve `db` from `scope` if not already a param.

- [ ] **Step 4: Run tests**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdfExtractionPersistence" -v minimal`
Expected: PASS (both columns non-null). Re-run `FullyQualifiedName~UploadPdf` to confirm no regression.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/UploadPdfExtractionPersistenceIntegrationTests.cs
git commit -m "fix(pdf): persist ExtractedText/StructuredElementsJson after Ready in Upload pipeline (#3281)"
```

---

### Task 5: Wire `CompleteChunkedUploadCommandHandler` + carry StructuredElements

`ChunkTextContentAsync` (line 682) takes neither `pdfDoc` nor `extractResult`, and `ExtractPdfTextAsync` (line 565) **discards** `PagedTextExtractionResult`/`StructuredElements` (returns `(bool, string?, int)`). Widen the tuple to carry structured elements, persist `StructuredElementsJson` on the tracked `pdfDoc` (this handler mutates state directly — no AsNoTracking bug), thread `pdfDoc` + structured elements into the chunker, and cap embedding input.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs`
  - `ExtractPdfTextAsync` (line 565): widen return to `(bool success, string? fullText, int totalPages, IReadOnlyList<ExtractedElement>? structuredElements)`; at the extraction write (lines 609-611) also set `pdfDoc.StructuredElementsJson`; update ALL return sites.
  - `ChunkTextContentAsync` (line 682): add `PdfDocumentEntity pdfDoc, IReadOnlyList<ExtractedElement>? structuredElements`; resolve `IAdvancedChunkingService?`; `BuildAsync` when non-null else flat.
  - call sites (line 487 for the extract tuple, line 487-488 for chunk) updated.
  - `GenerateEmbeddingsAsync` (line 720): cap texts.
- Test: handler-driven test asserting heading-bearing chunks + `StructuredElementsJson` persisted.

**Interfaces:**
- Consumes: `HeadingAwareChunker.BuildAsync`, `HeadingAwareChunkAdapter`, `IAdvancedChunkingService`, `ExtractedElement`.
- Produces: heading-bearing `DocumentChunkInput` + persisted `StructuredElementsJson` for chunked uploads.

- [ ] **Step 1: Write the failing test**

Follow the existing `CompleteChunkedUploadCommandHandler` tests. Register a mocked `IAdvancedChunkingService` (parent+child) in the scope; assert persisted `text_chunks` carry `Heading` + child `ParentChunkId`, and `pdf_documents.StructuredElementsJson` is non-null after completion.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~CompleteChunkedUpload&FullyQualifiedName~HeadingAware" -v minimal`
Expected: FAIL.

- [ ] **Step 3: Write the implementation**

Add usings (`…Application.Services.Chunking`, `…KnowledgeBase.Application.Services.Chunking`, `…DocumentProcessing.Domain.Services` for `ExtractedElement`).

`ExtractPdfTextAsync` (line 565): change signature to
```csharp
    private async Task<(bool success, string? fullText, int totalPages, IReadOnlyList<ExtractedElement>? structuredElements)> ExtractPdfTextAsync(
```
At the extraction write (lines 609-611):
```csharp
        pdfDoc.ExtractedText = fullText;
        pdfDoc.StructuredElementsJson = extractResult.StructuredElements is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(extractResult.StructuredElements);
        pdfDoc.PageCount = extractResult.TotalPages;
        pdfDoc.CharacterCount = extractResult.TotalCharacters;
```
Update every `return` in this method to include the 4th element (success returns `extractResult.StructuredElements`; failure/early returns `null`).

Call site (line 487): destructure the 4-tuple; pass `structuredElements` + `pdfDoc` to `ChunkTextContentAsync`.

`ChunkTextContentAsync` (line 682):
```csharp
    private async Task<List<DocumentChunkInput>> ChunkTextContentAsync(
        string pdfId,
        string fullText,
        PdfDocumentEntity pdfDoc,
        IReadOnlyList<ExtractedElement>? structuredElements,
        IServiceScope scope)
    {
        var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
        const int chunkSize = 512;
        const int chunkOverlap = 50;

        var advancedChunking = scope.ServiceProvider.GetService<IAdvancedChunkingService>();
        if (advancedChunking != null)
        {
            var hierarchical = await HeadingAwareChunker.BuildAsync(
                structuredElements,
                fullText,
                pdfDoc.Id,
                pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId,
                advancedChunking,
                CancellationToken.None).ConfigureAwait(false);
            return HeadingAwareChunkAdapter.ToChunkInputs(hierarchical);
        }

        // Fallback: existing flat PrepareForEmbedding body, unchanged.
        // ... keep existing body ...
    }
```

`GenerateEmbeddingsAsync` (line 720): wrap the batch texts with `HeadingAwareChunkAdapter.CapForEmbedding`.

- [ ] **Step 4: Run tests**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~CompleteChunkedUpload" -v minimal`
Expected: new test PASS; pre-existing PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs apps/api/tests/Api.Tests/...
git commit -m "feat(chunking): heading-aware chunking + StructuredElements persist in CompleteChunkedUpload (#3281)"
```

---

### Task 6: Revive `IndexPdfIntegrationTests` + HeadingPath CTE round-trip (Testcontainers)

Repo rule #1555 wants a real-Postgres round-trip proving the recursive `HeadingPath` CTE resolves. The stale `IndexPdfIntegrationTests.cs` is `<Compile Remove>`-excluded and references a pre-`ISemanticResponseCache`/`IPdfIndexingPipeline` ctor + a removed `PdfDocumentEntity.GameId`. Fix those, un-exclude it, and add a heading-aware assertion via `GetKbChunksHandler`.

**Files:**
- Modify: `apps/api/tests/Api.Tests/Api.Tests.csproj:107` — remove the `<Compile Remove="Integration\DocumentProcessing\IndexPdfIntegrationTests.cs" />` line.
- Modify: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/IndexPdfIntegrationTests.cs` — fix the stale ctor + `PdfDocumentEntity.GameId`; add a heading-aware round-trip assertion.

**Interfaces:**
- Consumes: current `IndexPdfCommandHandler` ctor `(MeepleAiDbContext, IAdvancedChunkingService, IEmbeddingService, ILogger<IndexPdfCommandHandler>, IOptions<IndexingSettings>, ISemanticResponseCache, IPdfIndexingPipeline, TimeProvider?, IRoleClassifierService?)`; `GetKbChunksHandler` → `KbChunksListResponse` (`KbChunkSummaryDto.HeadingPath`).

- [ ] **Step 1: Un-exclude + build to surface all drift**

Remove the `<Compile Remove>` line for `IndexPdfIntegrationTests.cs`. Run `dotnet build ../../tests/Api.Tests` and collect every compile error (the 2 known + any further drift). Fix each:
- Constructor: supply `_serviceProvider.GetRequiredService<IAdvancedChunkingService>()` as arg 2 (NOT `ITextChunkingService`), add `ISemanticResponseCache` + `IPdfIndexingPipeline` (real from the container SP, or `Mock.Of<>()`).
- Remove the `GameId = gameId` initializer on the `PdfDocumentEntity` (line ~285); set `SharedGameId`/`PrivateGameId` as the other tests do. Leave `VectorDocumentEntity.GameId` (still valid).
- Remove/replace the `ITextChunkingService.ChunkText` mock setup (handler now calls `IAdvancedChunkingService`) — register the real `AddChunkingAndRerankingServices` set instead.

- [ ] **Step 2: Run to verify it compiles + runs (may be RED on the new assertion)**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~IndexPdfIntegrationTests" -v minimal`
Expected: compiles; existing scenarios pass; the new heading assertion (Step 3) not yet added.

- [ ] **Step 3: Add the heading-aware round-trip assertion**

Add a test that seeds a `pdf_documents` row with non-empty `StructuredElementsJson` (a titled document), runs `IndexPdfCommandHandler`, then:
```csharp
// direct column check (EF InMemory can't do the CTE, but this is real Postgres):
var chunks = await dbContext.TextChunks.AsNoTracking()
    .Where(t => t.PdfDocumentId == pdfGuid).ToListAsync(ct);
chunks.Should().Contain(c => c.Heading != null);
chunks.Should().Contain(c => c.ParentChunkId != null);

// CTE via retrieval handler:
var resp = await mediator.Send(new GetKbChunksQuery(/* kb doc id */), ct);
resp.Chunks.Should().Contain(c => c.HeadingPath.Count > 0);
```

- [ ] **Step 4: Run tests**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~IndexPdfIntegrationTests" -v minimal`
Expected: PASS. If drift beyond the 2 known issues is unexpectedly large, report it (do not silently rewrite unrelated scenarios).

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Api.Tests.csproj apps/api/tests/Api.Tests/Integration/DocumentProcessing/IndexPdfIntegrationTests.cs
git commit -m "test(chunking): revive IndexPdf integration test + HeadingPath CTE round-trip (#3281)"
```

---

## Final Verification (before PR)

- [ ] `dotnet build` (from `apps/api/src/Api`) — 0 errors, 0 warnings (`TreatWarningsAsErrors`).
- [ ] `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HeadingAware|FullyQualifiedName~PdfProcessingPipeline|FullyQualifiedName~UploadPdf|FullyQualifiedName~CompleteChunkedUpload"` — all green.
- [ ] Whole-branch review (opus) via superpowers:requesting-code-review.
- [ ] PR → `main-dev`, body `Closes #3281`, footer.

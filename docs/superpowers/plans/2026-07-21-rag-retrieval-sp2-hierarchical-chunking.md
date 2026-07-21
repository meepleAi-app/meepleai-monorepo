# SP2 — Hierarchical Chunking + Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire SP1's heading-aware extraction into all 4 ingestion pipelines so persisted `TextChunkEntity` rows carry a real per-section `Heading`, activating the deterministic role-classification fast-path.

**Architecture:** A shared `IHeadingAwareChunker` (DocumentProcessing) encapsulates `ExtractedDocumentFactory.FromExtraction` → `AdvancedChunkingService.ChunkDocumentAsync` → child-only mapper → oversize post-split, returning `List<DocumentChunkInput>`. The 4 pipelines call it (via scope-resolve for the 2 background handlers, ctor-optional for the 2 in-scope handlers). `StructuredElements` is persisted as versioned JSON on `PdfDocumentEntity` so `IndexPdf` (which only has flat `ExtractedText`) can rebuild the `ExtractedDocument`.

**Tech Stack:** .NET 9 (xUnit, Moq, FluentAssertions, EF Core + Npgsql, Testcontainers for integration).

**Spec:** `docs/superpowers/specs/2026-07-21-rag-retrieval-sp2-hierarchical-chunking-design.md`

> **Review 2026-07-21**: piano rivisto dopo review adversariale multi-lente (15 finding applicati) — corretto `pdf.GameId` inesistente (→ `PrivateGameId ?? SharedGameId`, threadato come parametro dove `pdfDoc` era fuori scope) in Task 4/5/6/7; Task 1 test con parent a content non-vuoto + test multi-pagina; per-child page recompute nel mapper; Task 7 unificato su `List<DocumentChunkInput>` (il `Select` con campi hierarchy non compilava su `TextChunk`); Task 4 test con harness sincrono; dual-language estratto in `TranslatedChunkMapper` + unit test; body flat espansi (no placeholder).

## Global Constraints

- Branch: `feature/rag-retrieval-sp2-hierarchical-chunking` (created, parent `main-dev`).
- Commit convention: `feat|fix|test|refactor|chore(scope): subject`, subject ≤ 72 chars.
- **Only child chunks** are persisted/embedded; `Heading` inherited from parent; `ParentChunkId = null`; `Level = 2`; `ElementType = "text"`. Parent (Level 0) chunks are NOT persisted.
- **No embedded chunk may exceed `MaxEmbeddingChars` (1800)** — post-split any mapped child whose `Text.Length > 1800` via `ITextChunkingService.ChunkText(text, MaxEmbeddingChars, DefaultChunkOverlap)`.
- `IHeadingAwareChunker` returns `List<DocumentChunkInput>`; `IndexPdf` builds `DocumentChunk` from those (adding `Embedding`).
- DI: IndexPdf + PdfProcessingPipeline get a trailing optional `IHeadingAwareChunker? = null` (null → existing flat path); Upload + Complete resolve it from the async scope (`scope.ServiceProvider.GetService<IHeadingAwareChunker>()`). Both cases null → flat fallback, so the ~13 existing test ctor sites keep compiling.
- `StructuredElementsJson` persisted as `{ SchemaVersion, Elements }` with **default** `JsonSerializer` options (PascalCase, matching `ExtractedTables`/`ExtractedDiagrams`); reader tolerant + `try/catch(JsonException)` → null (never hard-fail). Invariant: every writer of `ExtractedText` co-writes or nulls `StructuredElementsJson` in the same `SaveChanges`.
- Dual-language (PdfProcessingPipeline): translated chunks propagate `origChunk.Heading/Level/ElementType`.
- null-path (no/failed StructuredElements) → `FromExtraction(null)` → child with `Heading=null`; boundaries change vs today (not byte-identical); assert content-preservation + `Heading=null`, NOT equality.
- Backend build/test from `apps/api/src/Api`; kill stray `testhost` on lock errors; `git commit` triggers ~5-min FE pre-commit hook (expected, don't `--no-verify`).

## File Structure

**Create:**
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HierarchicalChunkMapper.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/IHeadingAwareChunker.cs` (interface + impl `HeadingAwareChunker`)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/StructuredElementsPayload.cs` (versioned DTO + serializer helper)
- Migration `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddStructuredElementsJson.cs` (via `dotnet ef`)
- Tests mirroring each.

**Modify:**
- `PdfDocumentEntity.cs` (add `StructuredElementsJson`)
- `UploadPdfCommandHandler.Processing.cs`, `PdfProcessingPipelineService.cs`, `IndexPdfCommandHandler.cs`, `CompleteChunkedUploadCommandHandler.cs` (wire chunker + persist JSON)
- `ExtractPdfTextCommandHandler.cs` (invariant co-write)
- `DocumentProcessingServiceExtensions.cs` (DI register `IHeadingAwareChunker`)

---

### Task 1: `HierarchicalChunkMapper` (child-only, pure)

**Files:**
- Create: `.../DocumentProcessing/Application/Services/Chunking/HierarchicalChunkMapper.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HierarchicalChunkMapperTests.cs`

**Interfaces:**
- Produces: `static List<DocumentChunkInput> ToChildDocumentChunks(IReadOnlyList<HierarchicalChunk> chunks)` — child (Level 2, non-root) only, Heading inherited, per-child page, `ParentChunkId=null`, `Level=2`, `ElementType` from child metadata.

- [ ] **Step 1: Write the failing test**

Create `HierarchicalChunkMapperTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class HierarchicalChunkMapperTests
{
    private static HierarchicalChunk Parent(string heading, string content, int page = 1) =>
        HierarchicalChunk.CreateParent(content, new ChunkMetadata { Page = page, Heading = heading, ElementType = "heading", CharStart = 0, CharEnd = content.Length, DocumentId = System.Guid.NewGuid() });

    private static HierarchicalChunk Child(string parentId, string content, string? heading, int page, int charStart) =>
        HierarchicalChunk.CreateChild(content, level: 2, new ChunkMetadata { Page = page, Heading = heading, ElementType = "text", CharStart = charStart, CharEnd = charStart + content.Length, DocumentId = System.Guid.NewGuid() }, parentId);

    [Fact]
    public void ToChildDocumentChunks_KeepsOnlyChildren_InheritsHeading()
    {
        var parent = Parent("Preparazione", "Preparazione body");
        var c1 = Child(parent.Id, "Disponi le tessere.", "Preparazione", 1, 0);
        var c2 = Child(parent.Id, "Mescola il mazzo.", "Preparazione", 1, 20);

        var result = HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent, c1, c2 });

        result.Should().HaveCount(2); // parent excluded
        result[0].Text.Should().Be("Disponi le tessere.");
        result[0].Heading.Should().Be("Preparazione");
        result[0].Level.Should().Be(2);
        result[0].ElementType.Should().Be("text");
        result[0].ParentChunkId.Should().BeNull();
    }

    [Fact]
    public void ToChildDocumentChunks_NullHeadingPreamble_Preserved()
    {
        var parent = Parent(null!, "intro");
        var c = Child(parent.Id, "intro text", null, 1, 0);
        var result = HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent, c });
        result.Should().ContainSingle();
        result[0].Heading.Should().BeNull();
    }

    [Fact]
    public void ToChildDocumentChunks_OnlyParent_NoChildren_ReturnsEmpty()
    {
        // HierarchicalChunk forbids empty content, so the parent must carry text; the mapper still
        // returns empty because the sole chunk IsRoot and roots are skipped.
        var parent = Parent("Empty", "section body");
        HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent }).Should().BeEmpty();
    }

    [Fact]
    public void ToChildDocumentChunks_MultiPageSection_RecomputesPagePerChild()
    {
        var parent = Parent("Long", "long section body");
        // two children of the same section at very different char offsets → different pages
        var c1 = Child(parent.Id, "early text", "Long", page: 1, charStart: 10);
        var c2 = Child(parent.Id, "late text", "Long", page: 1, charStart: 5000);

        var result = HierarchicalChunkMapper.ToChildDocumentChunks(new[] { parent, c1, c2 });

        result[0].Page.Should().Be(1);           // 10 / 2000 + 1
        result[1].Page.Should().Be(3);           // 5000 / 2000 + 1  (not collapsed to the section's page 1)
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HierarchicalChunkMapperTests" -v minimal`
Expected: FAIL to compile — `HierarchicalChunkMapper` does not exist.

- [ ] **Step 3: Implement the mapper**

Create `HierarchicalChunkMapper.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// SP2: maps AdvancedChunkingService output to embeddable inputs, keeping ONLY child chunks
/// (Level 2). Heading is already inherited from the parent section in the child's ChunkMetadata.
/// Parent (Level 0) chunks are not persisted; ParentChunkId is left null (only-child model).
/// </summary>
internal static class HierarchicalChunkMapper
{
    public static List<DocumentChunkInput> ToChildDocumentChunks(IReadOnlyList<HierarchicalChunk> chunks)
    {
        var result = new List<DocumentChunkInput>();
        if (chunks is null)
        {
            return result;
        }

        foreach (var c in chunks)
        {
            if (c.IsRoot)
            {
                continue; // parent/section container is not persisted
            }
            if (string.IsNullOrWhiteSpace(c.Content))
            {
                continue;
            }

            result.Add(new DocumentChunkInput
            {
                Text = c.Content,
                // Recompute per-child page from CharStart (~2000 chars/page, matching
                // TextChunkingService.EstimatePageNumber) so a multi-page section does not collapse
                // every child to the section's first page. Falls back to the section page for offset 0.
                Page = c.Metadata.CharStart > 0 ? (c.Metadata.CharStart / 2000) + 1 : c.Metadata.Page,
                CharStart = c.Metadata.CharStart,
                CharEnd = c.Metadata.CharEnd,
                Heading = c.Metadata.Heading,
                Level = 2,
                ParentChunkId = null,
                ElementType = string.IsNullOrWhiteSpace(c.Metadata.ElementType) ? "text" : c.Metadata.ElementType,
            });
        }

        return result;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HierarchicalChunkMapperTests" -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HierarchicalChunkMapper.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HierarchicalChunkMapperTests.cs
git commit -m "feat(chunking): HierarchicalChunkMapper maps child chunks with heading"
```

---

### Task 2: `IHeadingAwareChunker` shared service + DI

**Files:**
- Create: `.../DocumentProcessing/Application/Services/Chunking/IHeadingAwareChunker.cs`
- Modify: `.../DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs`
- Test: `.../DocumentProcessing/Application/Services/Chunking/HeadingAwareChunkerTests.cs`

**Interfaces:**
- Consumes: `ExtractedDocumentFactory.FromExtraction`, `IAdvancedChunkingService.ChunkDocumentAsync`, `HierarchicalChunkMapper`, `ITextChunkingService` (post-split).
- Produces: `Task<List<DocumentChunkInput>> ChunkAsync(Guid documentId, Guid? gameId, IReadOnlyList<ExtractedElement>? structuredElements, string fullText, CancellationToken ct)`.

- [ ] **Step 1: Write the failing test**

Create `HeadingAwareChunkerTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Constants;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class HeadingAwareChunkerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Real collaborators (all pure/deterministic): TextChunkingService + strategy selector + AdvancedChunkingService.
    private static HeadingAwareChunker CreateChunker()
    {
        var textChunking = new TextChunkingService(NullLogger<TextChunkingService>.Instance);
        var selector = new ChunkingStrategySelector();
        var advanced = new AdvancedChunkingService(textChunking, selector, NullLogger<AdvancedChunkingService>.Instance);
        return new HeadingAwareChunker(advanced, textChunking, NullLogger<HeadingAwareChunker>.Instance);
    }

    [Fact]
    public async Task ChunkAsync_WithTitleElement_ProducesChildrenWithHeading()
    {
        var elements = new List<ExtractedElement>
        {
            new("Preparazione", 1, "Title"),
            new("Disponi le tessere sul tavolo e mescola il mazzo di carte.", 1, "NarrativeText"),
        };
        var chunker = CreateChunker();

        var result = await chunker.ChunkAsync(System.Guid.NewGuid(), null, elements, "flat", Ct);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.Heading == "Preparazione");
    }

    [Fact]
    public async Task ChunkAsync_NullElements_ProducesNullHeadingChildren_ContentPreserved()
    {
        var chunker = CreateChunker();
        var flat = "Some flat body text without any structure.";
        var result = await chunker.ChunkAsync(System.Guid.NewGuid(), null, null, flat, Ct);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(c => c.Heading == null);
        string.Join(" ", result.Select(c => c.Text)).Should().Contain("flat body text");
    }

    [Fact]
    public async Task ChunkAsync_NoChildExceedsMaxEmbeddingChars()
    {
        var longBody = string.Join(" ", Enumerable.Repeat("parola", 1200)); // > 1800 chars, narrative → Sparse (2000)
        var elements = new List<ExtractedElement> { new("Regole", 1, "Title"), new(longBody, 1, "NarrativeText") };
        var chunker = CreateChunker();

        var result = await chunker.ChunkAsync(System.Guid.NewGuid(), null, elements, "flat", Ct);

        result.Should().OnlyContain(c => c.Text.Length <= ChunkingConstants.MaxEmbeddingChars);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HeadingAwareChunkerTests" -v minimal`
Expected: FAIL to compile — `IHeadingAwareChunker`/`HeadingAwareChunker` do not exist.

- [ ] **Step 3: Implement the service + register DI**

Create `IHeadingAwareChunker.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Constants;
using Api.Services;

#pragma warning disable MA0048 // File name must match type name — interface + impl together
namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

internal interface IHeadingAwareChunker
{
    Task<List<DocumentChunkInput>> ChunkAsync(
        Guid documentId,
        Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string fullText,
        CancellationToken ct);
}

internal sealed class HeadingAwareChunker : IHeadingAwareChunker
{
    private readonly IAdvancedChunkingService _advanced;
    private readonly ITextChunkingService _textChunking;
    private readonly ILogger<HeadingAwareChunker> _logger;

    public HeadingAwareChunker(
        IAdvancedChunkingService advanced,
        ITextChunkingService textChunking,
        ILogger<HeadingAwareChunker> logger)
    {
        _advanced = advanced;
        _textChunking = textChunking;
        _logger = logger;
    }

    public async Task<List<DocumentChunkInput>> ChunkAsync(
        Guid documentId, Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string fullText, CancellationToken ct)
    {
        var document = ExtractedDocumentFactory.FromExtraction(documentId, gameId, structuredElements, fullText ?? string.Empty);
        var hchunks = await _advanced.ChunkDocumentAsync(document, config: null, ct).ConfigureAwait(false);
        var mapped = HierarchicalChunkMapper.ToChildDocumentChunks(hchunks);
        return PostSplitOversized(mapped);
    }

    // Mirror of EnhancedPdfProcessingOrchestrator.SplitOversizedPageChunks: no embedded chunk may exceed
    // MaxEmbeddingChars (E5-base token limit); Sparse strategy can emit ~2000-char children.
    private List<DocumentChunkInput> PostSplitOversized(List<DocumentChunkInput> chunks)
    {
        var result = new List<DocumentChunkInput>(chunks.Count);
        foreach (var chunk in chunks)
        {
            if (chunk.Text.Length <= ChunkingConstants.MaxEmbeddingChars)
            {
                result.Add(chunk);
                continue;
            }

            var subs = _textChunking.ChunkText(chunk.Text, ChunkingConstants.MaxEmbeddingChars, ChunkingConstants.DefaultChunkOverlap);
            foreach (var sub in subs.Where(s => !string.IsNullOrWhiteSpace(s.Text)))
            {
                result.Add(chunk with
                {
                    Text = sub.Text,
                    CharStart = chunk.CharStart + sub.CharStart,
                    CharEnd = chunk.CharStart + sub.CharEnd,
                });
            }
        }
        return result;
    }
}
```

In `DocumentProcessingServiceExtensions.cs`, register (near the other DocumentProcessing service registrations):

```csharp
        services.AddScoped<IHeadingAwareChunker, HeadingAwareChunker>();
```

(`AdvancedChunkingService`/`ITextChunkingService`/`ChunkingStrategySelector` are already registered in KnowledgeBase/Application DI.)

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~HeadingAwareChunkerTests" -v minimal`
Expected: PASS (heading inherited, null-path, no child > 1800).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/IHeadingAwareChunker.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/Chunking/HeadingAwareChunkerTests.cs
git commit -m "feat(chunking): IHeadingAwareChunker shared service with oversize post-split"
```

---

### Task 3: Persist `StructuredElements` (migration + versioned JSON)

**Files:**
- Modify: `.../Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`
- Create: `.../DocumentProcessing/Application/Services/Chunking/StructuredElementsPayload.cs`
- Migration via `dotnet ef migrations add AddStructuredElementsJson`
- Test: `.../Chunking/StructuredElementsPayloadTests.cs`

**Interfaces:**
- Produces: `PdfDocumentEntity.StructuredElementsJson` (`string?`); `StructuredElementsPayload.Serialize(elements) -> string?`, `StructuredElementsPayload.TryDeserialize(json) -> IReadOnlyList<ExtractedElement>?` (tolerant, never throws).

- [ ] **Step 1: Write the failing test**

Create `StructuredElementsPayloadTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class StructuredElementsPayloadTests
{
    [Fact]
    public void RoundTrip_PreservesElements()
    {
        var elements = new List<ExtractedElement> { new("Setup", 1, "Title"), new("body", 2, "NarrativeText") };
        var json = StructuredElementsPayload.Serialize(elements);
        json.Should().NotBeNullOrEmpty();

        var back = StructuredElementsPayload.TryDeserialize(json);
        back.Should().NotBeNull();
        back!.Select(e => (e.Text, e.PageNumber, e.ElementType))
            .Should().Equal(("Setup", 1, "Title"), ("body", 2, "NarrativeText"));
    }

    [Fact]
    public void Serialize_NullOrEmpty_ReturnsNull()
    {
        StructuredElementsPayload.Serialize(null).Should().BeNull();
        StructuredElementsPayload.Serialize(new List<ExtractedElement>()).Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_MalformedOrLegacy_ReturnsNull_NeverThrows()
    {
        StructuredElementsPayload.TryDeserialize("{ not valid json").Should().BeNull();
        StructuredElementsPayload.TryDeserialize(null).Should().BeNull();
        // legacy/unknown shape tolerated (unknown members ignored, missing -> null)
        StructuredElementsPayload.TryDeserialize("{\"SchemaVersion\":99,\"Elements\":null}").Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_FrozenBlob_Reads()
    {
        const string frozen = "{\"SchemaVersion\":1,\"Elements\":[{\"Text\":\"Setup\",\"PageNumber\":1,\"ElementType\":\"Title\"}]}";
        var back = StructuredElementsPayload.TryDeserialize(frozen);
        back.Should().ContainSingle();
        back![0].Text.Should().Be("Setup");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~StructuredElementsPayloadTests" -v minimal`
Expected: FAIL to compile — `StructuredElementsPayload` does not exist.

- [ ] **Step 3: Implement the payload + entity field + migration**

Create `StructuredElementsPayload.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// Versioned, tolerant persistence of the raw extraction elements so IndexPdf (which only has flat
/// ExtractedText) can rebuild the ExtractedDocument. Default JSON options (PascalCase) match the
/// existing ExtractedTables/ExtractedDiagrams columns. Reads never throw — malformed/legacy blobs
/// return null so the caller degrades to the flat null-path.
/// </summary>
internal static class StructuredElementsPayload
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record Envelope(int SchemaVersion, IReadOnlyList<ExtractedElement>? Elements);

    public static string? Serialize(IReadOnlyList<ExtractedElement>? elements)
    {
        if (elements is null || elements.Count == 0)
        {
            return null;
        }
        return JsonSerializer.Serialize(new Envelope(CurrentSchemaVersion, elements), Options);
    }

    public static IReadOnlyList<ExtractedElement>? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            var env = JsonSerializer.Deserialize<Envelope>(json, Options);
            return env?.Elements is { Count: > 0 } ? env.Elements : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

In `PdfDocumentEntity.cs`, after `AtomicRuleCount` (near the other JSON columns), add:

```csharp
    // SP2 (#3268): raw extraction elements (versioned JSON) so IndexPdf can rebuild the
    // ExtractedDocument for heading-aware chunking. Invariant: co-written or nulled with ExtractedText.
    public string? StructuredElementsJson { get; set; }
```

Generate the migration:

```bash
cd apps/api/src/Api
dotnet ef migrations add AddStructuredElementsJson
```

Verify the generated `Up`/`Down` is a single nullable `text` `AddColumn`/`DropColumn` on `pdf_documents` (mirroring `20260714163243_AddShareRequestCoverChangeFields`). It needs NO tsvector handling (`search_vector` is `GENERATED ALWAYS` on `ExtractedText` only).

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/api/src/Api && dotnet build && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~StructuredElementsPayloadTests" -v minimal`
Expected: build clean (migration compiles), tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/Chunking/StructuredElementsPayload.cs apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs apps/api/src/Api/Infrastructure/Migrations/ apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/Chunking/StructuredElementsPayloadTests.cs
git commit -m "feat(chunking): persist StructuredElements as versioned JSON on pdf_documents"
```

---

### Task 4: Wire `UploadPdfCommandHandler` (scope-resolve + persist)

**Files:**
- Modify: `.../Application/Commands/UploadPdfCommandHandler.Processing.cs`
- Test: `apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs` (add heading assertion)

**Interfaces:**
- Consumes: `IHeadingAwareChunker` (scope-resolved), `StructuredElementsPayload`.

- [ ] **Step 1: Write the failing test**

Upload's chunking runs inside `ProcessPdfAsync`, enqueued via `_backgroundTaskService.ExecuteWithCancellation(...)`. The default `UploadPdfIntegrationTests` harness registers a bare `Mock<IBackgroundTaskService>` (delegate never runs → chunks never produced), a non-success `Mock.Of<IEmbeddingService>`, and no `IRoleClassifierService`. So the test needs a **synchronous harness** to observe persisted chunks. In the test setup:
1. Register an inline `IBackgroundTaskService` whose `ExecuteWithCancellation` **invokes the delegate synchronously** (so chunking+persistence run in-test).
2. Register a stub `IEmbeddingService` returning `Success=true` with one 768-dim vector per input text.
3. Register a **real** `RoleClassifierService` with a mock `ILlmService` (a `"Setup"` heading resolves via `HeadingRules` without calling the LLM).
4. Set up `IPdfTextExtractor.ExtractPagedTextAsync` to return a `PagedTextExtractionResult` whose `StructuredElements` = `[ new ExtractedElement("Setup", 1, "Title"), new ExtractedElement("Disponi le tessere e mescola il mazzo.", 1, "NarrativeText") ]`.

Then assert:

```csharp
        var chunks = await dbContext.TextChunks.Where(c => c.PdfDocumentId == pdfGuid).ToListAsync(TestContext.Current.CancellationToken);
        chunks.Should().NotBeEmpty();
        chunks.Should().OnlyContain(c => c.Heading == "Setup");
        chunks.Should().Contain(c => c.RoleTags != GameBookRole.None); // heading fast-path assigned a role
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdfIntegrationTests" -v minimal`
Expected: FAIL — with the synchronous harness the chunks are produced by the **flat** path, so `Heading == null` (the `OnlyContain(c => c.Heading == "Setup")` assertion fails).

- [ ] **Step 3: Wire the chunker + persist JSON**

In `UploadPdfCommandHandler.Processing.cs`, in `ExtractPdfContentAsync` where `pdfDoc.ExtractedText = fullText;` is set (~line 260), add:

```csharp
            pdfDoc.StructuredElementsJson = StructuredElementsPayload.Serialize(extractResult.StructuredElements);
```

Thread a game id into `ChunkExtractedTextAsync` (it has no `pdfDoc` in scope, and `PdfDocumentEntity` exposes `SharedGameId`/`PrivateGameId`, **not** `GameId`). Add a `Guid? gameId` parameter to the method signature (line 337) and update the single call site (lines 68-69) where `pdfDoc` IS in scope:

```csharp
            var allDocumentChunks = await ChunkExtractedTextAsync(
                pdfId, fullText!, extractResult!, pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId,
                db, scope, startTime, cancellationToken).ConfigureAwait(false);
```

Then replace the body of `ChunkExtractedTextAsync` so it prefers the heading-aware chunker, falling back to the existing flat logic when unavailable:

```csharp
        var chunkingStopwatch = Stopwatch.StartNew();
        var headingAwareChunker = scope.ServiceProvider.GetService<IHeadingAwareChunker>();

        List<DocumentChunkInput> allDocumentChunks;
        if (headingAwareChunker != null)
        {
            allDocumentChunks = await headingAwareChunker.ChunkAsync(
                Guid.Parse(pdfId), gameId, extractResult.StructuredElements, fullText, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
            const int chunkSize = 512;
            const int chunkOverlap = 50;
            allDocumentChunks = chunkingService.PrepareForEmbedding(fullText, chunkSize, chunkOverlap)
                ?.Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
                .Select(chunk => new DocumentChunkInput { Text = chunk.Text, Page = chunk.Page, CharStart = chunk.CharStart, CharEnd = chunk.CharEnd })
                .ToList()
                ?? new List<DocumentChunkInput>();
        }

        allDocumentChunks = allDocumentChunks
            .Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
            .ToList();

        chunkingStopwatch.Stop();
        RecordPipelineMetricSafely("chunking", chunkingStopwatch.Elapsed.TotalMilliseconds, allDocumentChunks.Count);
        return allDocumentChunks;
```

(`gameId` is functionally immaterial to the persisted chunk — the mapper does not carry `Metadata.GameId` — but the literal `pdfDoc.GameId` must be removed to compile.) The downstream `SaveTextChunksForHybridSearchAsync` already copies `chunk.Heading/Level/ParentChunkId/ElementType` onto `TextChunkEntity` and calls the role classifier — no change needed there.

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdfIntegrationTests" -v minimal`
Expected: PASS (Heading persisted, role assigned).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs
git commit -m "feat(chunking): wire heading-aware chunker into UploadPdf + persist elements"
```

---

### Task 5: Wire `PdfProcessingPipelineService` (ctor optional + dual-language)

**Files:**
- Modify: `.../Application/Services/PdfProcessingPipelineService.cs`
- Test: `.../Application/Services/PdfProcessingPipelineServiceCoverTests.cs` (or a new focused test) — dual-language heading propagation.

**Interfaces:**
- Consumes: `IHeadingAwareChunker?` (trailing optional ctor param).

- [ ] **Step 1: Write the failing test**

The full pipeline is infeasible via `PdfProcessingPipelineServiceCoverTestFactory` (heavy deps + background execution). Instead, extract a pure static helper for the translated-chunk mapping and unit-test it in isolation. Create `.../Chunking/TranslatedChunkMapper.cs` test:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.Services;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class TranslatedChunkMapperTests
{
    [Fact]
    public void ForTranslation_PreservesHeadingAndHierarchy()
    {
        var orig = new DocumentChunkInput { Text = "Disponi", Page = 3, CharStart = 10, CharEnd = 17, Heading = "Setup", Level = 2, ElementType = "text" };
        var translated = TranslatedChunkMapper.ForTranslation(orig, "Lay out");
        translated.Text.Should().Be("Lay out");
        translated.Heading.Should().Be("Setup");   // heading inherited on the EN chunk (was dropped before SP2)
        translated.Page.Should().Be(3);
        translated.Level.Should().Be(2);
        translated.ElementType.Should().Be("text");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PdfProcessingPipelineService" -v minimal`
Expected: FAIL — translated chunks have `Heading == null`.

- [ ] **Step 3: Add optional ctor param, wire chunker, propagate Heading**

Add trailing optional param to the ctor (after `pdfCoverUploadPipeline`):

```csharp
        IPdfCoverUploadPipeline? pdfCoverUploadPipeline = null,
        IHeadingAwareChunker? headingAwareChunker = null)
```

Field: `private readonly IHeadingAwareChunker? _headingAwareChunker;` and assign `_headingAwareChunker = headingAwareChunker;`.

At the `pdfDoc.ExtractedText = fullText;` write site, add `pdfDoc.StructuredElementsJson = StructuredElementsPayload.Serialize(extractResult.StructuredElements);`.

Change `ChunkText` (line 655) into an async method that prefers the chunker, with the existing flat body pasted verbatim as fallback:

```csharp
    private async Task<List<DocumentChunkInput>> ChunkTextAsync(string fullText, PagedTextExtractionResult extractResult, Guid documentId, Guid? gameId, CancellationToken ct)
    {
        if (_headingAwareChunker != null)
        {
            var hc = await _headingAwareChunker.ChunkAsync(documentId, gameId, extractResult.StructuredElements, fullText, ct).ConfigureAwait(false);
            var filtered = hc.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();
            if (filtered.Count > 0) return filtered;
        }

        // Flat fallback (unchanged from the original ChunkText body: PrepareForEmbedding 1024/150 + page fallback).
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
                    chunks.Add(new DocumentChunkInput { Text = textChunk.Text, Page = pageChunk.PageNumber, CharStart = textChunk.CharStart, CharEnd = textChunk.CharEnd });
                }
            }
        }

        return chunks.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();
    }
```

Update the call site (line 191) to `await ChunkTextAsync(fullText, extractResult, pdfDoc.Id, pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId, cancellationToken)`.

Create the pure helper `.../Chunking/TranslatedChunkMapper.cs`:

```csharp
using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// SP2: builds the English-translation chunk from an original chunk, preserving all hierarchy
/// fields (Heading/Level/ParentChunkId/ElementType) so translated chunks of non-EN rulebooks also
/// activate the role fast-path (resolves the #730 forward-wiring TODO in PdfProcessingPipelineService).
/// </summary>
internal static class TranslatedChunkMapper
{
    public static DocumentChunkInput ForTranslation(DocumentChunkInput orig, string translatedText) =>
        orig with { Text = translatedText };
}
```

Fix the dual-language TODO (lines 225-235) to use it:

```csharp
                            translatedChunks.Add((
                                TranslatedChunkMapper.ForTranslation(origChunk, t.TranslatedText),
                                "en",
                                true));
```

(The record `with`-expression preserves `Page/CharStart/CharEnd/Heading/Level/ParentChunkId/ElementType`; the helper pins that contract so a future non-record refactor cannot silently drop the heading again.)

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PdfProcessingPipelineService" -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineServiceCoverTests.cs
git commit -m "feat(chunking): wire chunker into PdfProcessingPipeline + dual-language heading"
```

---

### Task 6: Wire `CompleteChunkedUploadCommandHandler` (signature + scope-resolve)

**Files:**
- Modify: `.../Application/Commands/CompleteChunkedUploadCommandHandler.cs`
- Test: `.../Application/Commands/CompleteChunkedUploadDedupTests.cs` (or focused) — heading persisted.

- [ ] **Step 1: Write the failing test** — given StructuredElements with a `"Setup"` Title, the persisted `text_chunks` have `Heading == "Setup"`.

- [ ] **Step 2: Run to verify it fails** (Heading null).

- [ ] **Step 3: Change signature + wire chunker + persist JSON**

Change `ExtractPdfTextAsync` (line 565) return type to surface StructuredElements:

```csharp
    private async Task<(bool success, string? fullText, int totalPages, IReadOnlyList<ExtractedElement>? structuredElements)> ExtractPdfTextAsync(...)
    {
        ...
        pdfDoc.ExtractedText = fullText;
        pdfDoc.StructuredElementsJson = StructuredElementsPayload.Serialize(extractResult.StructuredElements);
        ...
        return (true, fullText, extractResult.TotalPages, extractResult.StructuredElements);
    }
```

Update the caller (line 482): `var (extractSuccess, fullText, totalPages, structuredElements) = await ExtractPdfTextAsync(...)`, then pass the extra args into `ChunkTextContentAsync(pdfId, fullText!, structuredElements, pdfGuid, pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId, scope)`. New method (heading-aware branch + the existing flat body pasted verbatim as fallback):

```csharp
    private async Task<List<DocumentChunkInput>> ChunkTextContentAsync(
        string pdfId, string fullText, IReadOnlyList<ExtractedElement>? structuredElements,
        Guid documentId, Guid? gameId, IServiceScope scope)
    {
        var headingAwareChunker = scope.ServiceProvider.GetService<IHeadingAwareChunker>();
        if (headingAwareChunker != null)
        {
            var hc = await headingAwareChunker.ChunkAsync(documentId, gameId, structuredElements, fullText, CancellationToken.None).ConfigureAwait(false);
            var filtered = hc.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();
            if (filtered.Count > 0) return filtered;
        }

        // Flat fallback (unchanged from the original ChunkTextContentAsync body).
        var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
        const int chunkSize = 512;
        const int chunkOverlap = 50;
        var allDocumentChunks = chunkingService.PrepareForEmbedding(fullText, chunkSize, chunkOverlap)
            ?.Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
            .Select(chunk => new DocumentChunkInput { Text = chunk.Text, Page = chunk.Page, CharStart = chunk.CharStart, CharEnd = chunk.CharEnd })
            .ToList()
            ?? new List<DocumentChunkInput>();

        return allDocumentChunks.Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text)).ToList();
    }
```

- [ ] **Step 4: Run to verify it passes.**
- [ ] **Step 5: Commit** `feat(chunking): wire chunker into CompleteChunkedUpload + surface elements`.

---

### Task 7: Wire `IndexPdfCommandHandler` (read JSON → chunker → DocumentChunk)

**Files:**
- Modify: `.../Application/Commands/IndexPdfCommandHandler.cs`
- Test: `.../Application/Handlers/IndexPdfCommandHandlerTests.cs` — from `StructuredElementsJson`, chunks carry Heading; malformed JSON → flat fallback (no throw).

> **Note for implementer:** `IndexPdfIntegrationTests.cs:440` reportedly constructs the handler with 5 args against a 7-mandatory ctor — verify whether that file currently compiles before starting; if it is stale/relies on a helper, do not let the new optional param mask a pre-existing break. The new trailing `IHeadingAwareChunker? = null` does not change any existing site.

- [ ] **Step 1: Write the failing test** — handler given a `PdfDocumentEntity` with `StructuredElementsJson` containing a `"Setup"` Title → persisted chunks `Heading == "Setup"`; a second test with `StructuredElementsJson = "{malformed"` → succeeds via flat path, no exception.

- [ ] **Step 2: Run to verify it fails.**

- [ ] **Step 3: Add optional ctor param + read JSON + build DocumentChunk**

Add trailing optional ctor param `IHeadingAwareChunker? headingAwareChunker = null` + field `_headingAwareChunker`. Thread the `pdf` entity into `ChunkAndEmbedTextAsync` (extend its signature and the call at lines 100-101, which currently passes only `pdf.ExtractedText!`) so `StructuredElementsJson` + game id are available. `IndexPdf` runs in-scope, so ctor injection is correct.

**Unify both paths on `List<DocumentChunkInput>`** — the current flat path uses `_chunkingService.ChunkText(...)` returning `List<TextChunk>`, which has NO hierarchy fields (a `DocumentChunk` Select reading `chunk.Heading` on a `TextChunk` is CS1061). Replace the `var textChunks = _chunkingService.ChunkText(extractedText);` block with a single unified list:

```csharp
        var structured = StructuredElementsPayload.TryDeserialize(pdf.StructuredElementsJson); // null on malformed → flat
        List<DocumentChunkInput> chunkInputs =
            (_headingAwareChunker != null
                ? await _headingAwareChunker.ChunkAsync(Guid.Parse(pdfId), pdf.PrivateGameId ?? pdf.SharedGameId, structured, extractedText, cancellationToken).ConfigureAwait(false)
                : null) is { Count: > 0 } hc
            ? hc
            : (_chunkingService.PrepareForEmbedding(extractedText) ?? new List<DocumentChunkInput>());

        chunkInputs = chunkInputs.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();

        if (chunkInputs.Count == 0)
        {
            _logger.LogWarning("No chunks created for PDF {PdfId}", pdfId);
            return (false, null, "No chunks created from text", PdfIndexingErrorCode.ChunkingFailed);
        }
```

Then drive the existing batch/embedding loop off `chunkInputs` (its `.Text` is what the loop already reads) and construct `DocumentChunk` carrying the hierarchy fields — valid for BOTH paths because `chunkInputs` is always `DocumentChunkInput` (the flat path leaves `Heading` null via defaults):

```csharp
            var batchChunks = chunkInputs.Skip(i).Take(batchSize)
                .Select((chunk, index) => new DocumentChunk
                {
                    Text = chunk.Text,
                    Embedding = embeddingResult.Embeddings[index],
                    Page = chunk.Page,
                    CharStart = chunk.CharStart,
                    CharEnd = chunk.CharEnd,
                    Heading = chunk.Heading,
                    Level = chunk.Level,
                    ParentChunkId = chunk.ParentChunkId,
                    ElementType = chunk.ElementType,
                })
                .ToList();
```

(Rename `textChunks` → `chunkInputs` throughout the loop; `PrepareForEmbedding` internally calls `ChunkText` and wraps in `DocumentChunkInput`, so the flat path is behavior-equivalent to today.)

- [ ] **Step 4: Run to verify it passes** (heading from JSON; malformed → flat, no throw).
- [ ] **Step 5: Commit** `feat(chunking): wire chunker into IndexPdf via persisted StructuredElements`.

---

### Task 8: `ExtractedText↔StructuredElementsJson` invariant + role-fast-path regression

**Files:**
- Modify: `.../Application/Commands/ExtractPdfTextCommandHandler.cs`
- Test: `.../DocumentProcessing/.../ExtractPdfTextCommandHandlerTests.cs` (co-write); role-fast-path regression test.

- [ ] **Step 1: Write the failing tests**

(a) `ExtractPdfTextCommandHandler` co-write: after re-extraction, `pdf.StructuredElementsJson` is set from `extractResult.StructuredElements` in the same `SaveChanges` (assert non-null when elements present; null when absent). 
(b) Role-fast-path regression: a chunk with `Heading = "Setup"` → `RoleClassifierService.ClassifyAsync` returns a non-`None` role and the injected `ILlmService` mock is **never** invoked; companion: `Heading = "Zzz Random"` (matches no `HeadingRule`) → routes to the LLM fallback.

- [ ] **Step 2: Run to verify they fail.**

- [ ] **Step 3: Implement**

In `ExtractPdfTextCommandHandler.cs`, where `pdf.ExtractedText = fullText;` is set (line ~135), add the co-write:

```csharp
            pdf.StructuredElementsJson = StructuredElementsPayload.Serialize(extractResult.StructuredElements);
```

(The role-fast-path is already correct in `RoleClassifierService`; the test asserts the existing behavior now that Heading is populated — no production change needed beyond confirming the wiring. If the test reveals a gap, fix in `TextChunkRoleClassifier`.)

- [ ] **Step 4: Run to verify they pass.**
- [ ] **Step 5: Commit** `feat(chunking): ExtractedText/StructuredElements invariant + role fast-path test`.

---

## Final verification (before PR)

- [ ] `cd apps/api/src/Api && dotnet build` → 0 errors (migration + all wiring compile).
- [ ] `dotnet test ../../tests/Api.Tests --filter "Category=Unit&FullyQualifiedName~Chunking" -v minimal` → green.
- [ ] Targeted integration: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UploadPdfIntegrationTests|FullyQualifiedName~IndexPdfCommandHandlerTests|FullyQualifiedName~CompleteChunkedUpload" -v minimal` → green.
- [ ] The ~13 handler ctor test sites compile unchanged (no arg added at call sites).
- [ ] PR to `main-dev` (Part of #3266, Closes #3268) with per-pipeline heading evidence.

## Self-review coverage (spec → task)

- §4.1 IHeadingAwareChunker (FromExtraction→Chunk→mapper→post-split) → Task 2.
- §4.2 mapper child-only → Task 1.
- §4.3 DI optional/scope → Tasks 4-7 (Upload/Complete scope-resolve; Pipeline/IndexPdf ctor-optional).
- §4.4 clamp/post-split MaxEmbeddingChars → Task 2 `PostSplitOversized`.
- §4.5 per-child page → Task 1 (child metadata page).
- §4.6 versioned JSON + tolerant read + invariant → Task 3 + Task 8.
- §4.7 dual-language Heading → Task 5.
- §5 null-path content-preservation → Task 2 test.
- §6 per-pipeline + role-fast-path (match + non-match) + JSON-written → Tasks 4-8.
- §7 DoD per-pipeline → Tasks 4-7.

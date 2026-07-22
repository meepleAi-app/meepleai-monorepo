# SP1 — Structured Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Esporre gli elementi tipizzati (con heading) dal servizio Python `unstructured` e costruire da essi un `ExtractedDocument` con `Sections` popolate, verificabile in isolamento.

**Architecture:** Il servizio Python già rileva i `Title` nei raw `partition_pdf` elements ma li appiattisce con `chunk_by_title` prima di serializzare. Aggiungiamo un campo `elements[]` alla response Python (i raw elements, non i chunk), lo propaghiamo in C# attraverso `PagedTextExtractionResult` **e l'orchestrator** (altrimenti il default `OrchestratedPdfTextExtractor` lo droppa), e un builder puro `ExtractedDocumentFactory` raggruppa gli elementi per `Title` in sezioni-con-heading.

**Tech Stack:** Python 3.11 (FastAPI, Pydantic, pytest), .NET 9 (xUnit, Moq/Moq.Protected, FluentAssertions).

**Spec:** `docs/superpowers/specs/2026-07-21-rag-retrieval-sp1-structured-extraction-design.md`

> **Review 2026-07-21**: piano rivisto dopo review adversariale multi-lente (13 finding applicati) — logica del separatore inter-sezione corretta (`TotalTextLength` rimosso), test orchestrator riscritto sul **path di produzione reale** (`CreateEnhancedPagedResult`, non `FromStage`), aggiunto test su `doc.Content` multi-sezione, `CaseSensitiveTitle` ora asserisce il preambolo, `PageChunks` regression pin reso concreto, de-risk gate instradato attraverso l'estrattore reale.

## Global Constraints

- Branch: `feature/rag-retrieval-sp1-structured-extraction` (già creato, parent `main-dev`).
- Commit convention: `feat|fix|test|refactor|chore(scope): subject` (subject ≤ 72 char).
- `ExtractedElement` vive in `Api.BoundedContexts.DocumentProcessing.Domain.Services` (published contract), **non** in Infrastructure — così `KnowledgeBase` non dipende da `DocumentProcessing.Infrastructure`.
- `ExtractedElement.ElementType` = category **grezza** da Unstructured (`"Title"`/`"NarrativeText"`/`"Table"`/…), coalesced `null`/whitespace → `"NarrativeText"` (MAI null). La normalizzazione al vocabolario `{heading,table,list,text}` avviene **solo** nel factory, sul valore scritto in `DocumentSection.ElementType`.
- Grouping esatto: solo `ElementType == "Title"` (case-sensitive) apre una sezione.
- `DocumentSection.Content` include il testo del Title di apertura; invariante: `section.Content == doc.Content.Substring(section.CharStart, section.CharEnd - section.CharStart)` per ogni sezione.
- Null-path: `StructuredElements` null/vuoto → **una** sezione preambolo (`Heading=null`, `Content=flatText`) — il testo non va mai perso.
- `ExtractTextAsync` e `PageChunks` restano invariati (regression pin nei test).
- C# test trait: `[Trait("Category", TestCategories.Unit)]`.
- Backend test/build vanno eseguiti da `apps/api/src/Api`; kill di eventuali `testhost` prima (Known Pitfall #2593).

## File Structure

**Python** (`apps/unstructured-service/`):
- `src/api/schemas.py` — MODIFY: `ElementSchema` + campo `elements` su `PdfExtractionResponse`.
- `src/main.py` — MODIFY: serializza `result.elements` → `elements`.
- `tests/test_api.py` — MODIFY: test serializzazione `elements`.

**C#** (`apps/api/src/Api/`):
- `BoundedContexts/DocumentProcessing/Domain/Services/ExtractedElement.cs` — CREATE.
- `BoundedContexts/DocumentProcessing/Infrastructure/External/IPdfTextExtractor.cs` — MODIFY: `StructuredElements` su `PagedTextExtractionResult`.
- `BoundedContexts/DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractor.cs` — MODIFY: `UnstructuredElement` record + popolamento `StructuredElements`.
- `BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs` — MODIFY: `StructuredElements` su `EnhancedPagedExtractionResult` + `FromStage` + `CreateEnhancedPagedResult`.
- `BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractor.cs` — MODIFY: propaga `StructuredElements`.
- `BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactory.cs` — CREATE.

**C# test** (`apps/api/tests/Api.Tests/`):
- `BoundedContexts/DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractorTests.cs` — MODIFY.
- `BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractorTests.cs` — CREATE.
- `BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryTests.cs` — CREATE.
- `BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryDeriskTests.cs` — CREATE (Task 5).
- `TestData/unstructured-terraforming-response.json` — CREATE (Task 5, fixture reale).

---

### Task 1: Python — esporre i raw `elements` nella response

**Files:**
- Modify: `apps/unstructured-service/src/api/schemas.py`
- Modify: `apps/unstructured-service/src/main.py:15-21` (import), `:205-226` (build response)
- Test: `apps/unstructured-service/tests/test_api.py`

**Interfaces:**
- Produces: `PdfExtractionResponse.elements: List[ElementSchema]` dove `ElementSchema { text: str, page_number: int, category: Optional[str] }`, popolato dai raw `ExtractionResult.elements`. `chunks` resta invariato.

- [ ] **Step 1: Write the failing test**

In `apps/unstructured-service/tests/test_api.py`, dentro `class TestExtractEndpoint`, aggiungi (usa la fixture `mock_unstructured_elements` da `conftest.py`, che espone MockElement con `text`/`category`/`metadata.page_number`):

```python
    @patch("src.main.pdf_service.extract")
    def test_extract_serializes_raw_elements(
        self, mock_extract, client, mock_pdf_content, mock_unstructured_elements
    ):
        """Response must expose raw partition elements (with Title category)."""
        mock_extract.return_value = ExtractionResult(
            full_text="Title of Document\n\nThis is a paragraph.",
            chunks=[TextChunk(text="composite", page_number=1, element_type="CompositeElement")],
            page_count=2,
            elements=mock_unstructured_elements,
            tables=[],
            detected_structures=["Title", "Paragraph", "Table"],
            extraction_duration_ms=1200,
            quality_score=QualityScore(0.85, 0.40, 0.18, 0.15, 0.12),
        )

        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.pdf", mock_pdf_content, "application/pdf")},
            data={"strategy": "fast", "language": "ita"},
        )

        assert response.status_code == 200
        elements = response.json()["elements"]
        assert [e["category"] for e in elements] == ["Title", "Paragraph", "Table"]
        assert elements[0]["text"] == "Title of Document"
        assert elements[0]["page_number"] == 1
        assert elements[2]["page_number"] == 2
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/unstructured-service && python -m pytest tests/test_api.py::TestExtractEndpoint::test_extract_serializes_raw_elements -v`
Expected: FAIL — `KeyError: 'elements'` (campo non ancora nella response).

- [ ] **Step 3: Add `ElementSchema` and the `elements` field**

In `apps/unstructured-service/src/api/schemas.py`, dopo `class TextChunkSchema` (riga 27), aggiungi:

```python
class ElementSchema(BaseModel):
    """Raw partition element (pre-chunking), preserving its structural category."""

    text: str = Field(description="Element text content")
    page_number: int = Field(description="Page number (1-indexed)", ge=1)
    category: Optional[str] = Field(
        default=None, description="Raw Unstructured category (Title, NarrativeText, Table, …)"
    )
```

In `class PdfExtractionResponse`, dopo il campo `chunks` (riga 42), aggiungi:

```python
    elements: List[ElementSchema] = Field(
        default_factory=list, description="Raw partition elements with structural category"
    )
```

- [ ] **Step 4: Serialize `result.elements` in main.py**

In `apps/unstructured-service/src/main.py`, aggiorna l'import (riga 15-21) aggiungendo `ElementSchema`:

```python
from .api.schemas import (
    PdfExtractionResponse,
    TextChunkSchema,
    ElementSchema,
    ErrorDetail,
    ErrorResponse,
    HealthCheckResponse,
)
```

Nel `PdfExtractionResponse(...)` (riga 205), dopo il blocco `chunks=[...]` (riga 215), aggiungi il campo `elements`:

```python
            elements=[
                ElementSchema(
                    text=el.text,
                    page_number=getattr(getattr(el, "metadata", None), "page_number", 1) or 1,
                    category=getattr(el, "category", None),
                )
                for el in result.elements
                if getattr(el, "text", None)
            ],
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/unstructured-service && python -m pytest tests/test_api.py -v`
Expected: PASS (nuovo test verde; i test esistenti — inclusi `test_extract_success` con `elements=[]` → `elements: []` — restano verdi).

- [ ] **Step 6: Commit**

```bash
git add apps/unstructured-service/src/api/schemas.py apps/unstructured-service/src/main.py apps/unstructured-service/tests/test_api.py
git commit -m "feat(extraction): expose raw partition elements in Python response"
```

---

### Task 2: C# — `ExtractedElement` + estrattore popola `StructuredElements`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Services/ExtractedElement.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/IPdfTextExtractor.cs:82-88`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractor.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractorTests.cs`

**Interfaces:**
- Produces: `record ExtractedElement(string Text, int PageNumber, string ElementType)` in `DocumentProcessing.Domain.Services`.
- Produces: `PagedTextExtractionResult.StructuredElements` (`IReadOnlyList<ExtractedElement>?`, default `null`).
- Consumes (Task 1): la response Python con `elements[]`.

- [ ] **Step 1: Create `ExtractedElement`**

Create `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Services/ExtractedElement.cs`:

```csharp
namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// A raw partition element from PDF extraction, preserving its structural category.
/// ElementType carries the raw Unstructured category ("Title"/"NarrativeText"/"Table"/…),
/// coalesced null/whitespace to "NarrativeText" (never null). Published contract: consumed
/// by KnowledgeBase's ExtractedDocumentFactory (SP1) to build heading-aware sections.
/// </summary>
public record ExtractedElement(
    string Text,
    int PageNumber,
    string ElementType);
```

- [ ] **Step 2: Add `StructuredElements` to `PagedTextExtractionResult`**

In `IPdfTextExtractor.cs`, aggiorna il record `PagedTextExtractionResult` (riga 82) aggiungendo il parametro finale (nullable con default → retro-compatibile con tutti i chiamanti):

```csharp
internal record PagedTextExtractionResult(
    bool Success,
    IList<PageTextChunk> PageChunks,
    int TotalPages,
    int TotalCharacters,
    bool OcrTriggered,
    string? ErrorMessage = null,
    IReadOnlyList<ExtractedElement>? StructuredElements = null)
```

E aggiorna `CreateSuccess` (riga 90) per accettarli e passarli:

```csharp
    public static PagedTextExtractionResult CreateSuccess(
        IList<PageTextChunk> pageChunks,
        int totalPages,
        int totalCharacters,
        bool ocrTriggered,
        IReadOnlyList<ExtractedElement>? structuredElements = null)
    {
        return new PagedTextExtractionResult(
            Success: true,
            PageChunks: pageChunks,
            TotalPages: totalPages,
            TotalCharacters: totalCharacters,
            OcrTriggered: ocrTriggered,
            ErrorMessage: null,
            StructuredElements: structuredElements);
    }
```

(`ExtractedElement` è già in scope: `IPdfTextExtractor.cs:1` importa `Api.BoundedContexts.DocumentProcessing.Domain.Services`.)

- [ ] **Step 3: Write the failing test**

In `UnstructuredPdfTextExtractorTests.cs`, aggiungi un helper che costruisce una response con `elements[]` e un test. Dopo `CreateSuccessResponse` (riga 97) aggiungi:

```csharp
    private string CreateResponseWithElements()
    {
        var response = new
        {
            text = "Preparazione\n\nDisponi le tessere.",
            chunks = new[] { new { text = "composite", page_number = 1, element_type = "CompositeElement", metadata = new Dictionary<string, object>() } },
            elements = new object[]
            {
                new { text = "Preparazione", page_number = 1, category = "Title" },
                new { text = "Disponi le tessere.", page_number = 1, category = "NarrativeText" },
                new { text = "", page_number = 2, category = "PageBreak" }
            },
            quality_score = 0.85,
            page_count = 1,
            metadata = new { extraction_duration_ms = 10, strategy_used = "fast", language = "ita", detected_tables = 0, detected_structures = new[] { "Title" }, quality_breakdown = new { text_coverage_score = 0.4, structure_detection_score = 0.2, table_detection_score = 0.0, page_coverage_score = 0.2 } }
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }

    [Fact]
    public async Task ExtractPagedTextAsync_PopulatesStructuredElements_SkippingEmpty()
    {
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(CreateResponseWithElements()) });

        var result = await extractor.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        result.Success.Should().BeTrue();
        result.StructuredElements.Should().NotBeNull();
        result.StructuredElements!.Select(e => e.ElementType).Should().Equal("Title", "NarrativeText");
        result.StructuredElements![0].Text.Should().Be("Preparazione");
        result.StructuredElements![0].PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExtractPagedTextAsync_NoElements_LeavesStructuredElementsNull_AndPageChunksIntact()
    {
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(CreateSuccessResponse()) });

        var result = await extractor.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        result.Success.Should().BeTrue();
        result.StructuredElements.Should().BeNull();
        // regression pin: the ExtractPagedTextAsync rewrite must not change chunking.
        // CreateSuccessResponse() has pageCount=1 → exactly one page chunk starting at offset 0.
        result.PageChunks.Should().HaveCount(1);
        result.PageChunks[0].CharStartIndex.Should().Be(0);
        result.PageChunks[0].Text.Should().NotBeEmpty();
    }
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UnstructuredPdfTextExtractorTests.ExtractPagedTextAsync_PopulatesStructuredElements" -v minimal`
Expected: FAIL — `StructuredElements` è sempre `null` (non ancora popolato) → l'assert `NotBeNull` fallisce.

- [ ] **Step 5: Deserialize `elements` and populate `StructuredElements`**

In `UnstructuredPdfTextExtractor.cs`, aggiungi il record deserializzato dopo `UnstructuredChunk` (riga 315):

```csharp
internal record UnstructuredElement(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("category")] string? Category);
```

Aggiungi il campo a `UnstructuredExtractionResponse` (riga 304), dopo `Chunks`:

```csharp
    [property: JsonPropertyName("elements")] List<UnstructuredElement>? Elements,
```

(nota: `Elements` va inserito come parametro del record prima di `QualityScore` **oppure** in fondo con default; per un positional record senza default, inseriscilo subito dopo `Chunks` e prima di `QualityScore`.)

Aggiungi il mapper privato (dopo `CreatePageChunksFromText`, riga 298):

```csharp
    private static IReadOnlyList<ExtractedElement>? MapStructuredElements(List<UnstructuredElement>? elements)
    {
        if (elements is null || elements.Count == 0)
        {
            return null;
        }

        var mapped = elements
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => new ExtractedElement(
                Text: e.Text!,
                PageNumber: e.PageNumber > 0 ? e.PageNumber : 1,
                ElementType: string.IsNullOrWhiteSpace(e.Category) ? "NarrativeText" : e.Category!))
            .ToList();

        return mapped.Count > 0 ? mapped : null;
    }
```

Rifattorizza `ExtractPagedTextAsync` (riga 223-248) per accedere alla response deserializzata (oggi delega a `ExtractTextAsync`, che scarta gli `elements`). Sostituisci il corpo con una chiamata HTTP diretta che riusa gli helper privati esistenti:

```csharp
    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        string requestId = Guid.NewGuid().ToString("N");
        var client = _httpClientFactory.CreateClient("UnstructuredService");
        var configuredTimeout = client.Timeout;

        try
        {
            using var content = PrepareMultipartContent(pdfStream);
            using var response = await CallUnstructuredServiceAsync(client, content, cancellationToken).ConfigureAwait(false);
            var extractionResponse = await ParseExtractionResponseAsync(response, cancellationToken).ConfigureAwait(false);
            if (extractionResponse == null)
            {
                return PagedTextExtractionResult.CreateFailure("Invalid response from Unstructured service");
            }

            var normalizedText = PdfTextProcessingDomainService.NormalizeText(extractionResponse.Text);
            var pageChunks = CreatePageChunksFromText(normalizedText, extractionResponse.PageCount);
            var structuredElements = MapStructuredElements(extractionResponse.Elements);

            return PagedTextExtractionResult.CreateSuccess(
                pageChunks,
                extractionResponse.PageCount,
                normalizedText.Length,
                ocrTriggered: false,
                structuredElements: structuredElements);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Unstructured service (paged) failed. RequestId: {RequestId}", requestId);
            return PagedTextExtractionResult.CreateFailure($"Failed to connect to Unstructured service: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return PagedTextExtractionResult.CreateFailure($"Unstructured service timeout after {configuredTimeout.TotalSeconds}s");
        }
        catch (JsonException)
        {
            return PagedTextExtractionResult.CreateFailure("Invalid JSON response from Unstructured service");
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during paged Unstructured extraction. RequestId: {RequestId}", requestId);
            return PagedTextExtractionResult.CreateFailure($"Unexpected error during PDF extraction: {ex.Message}");
        }
#pragma warning restore CA1031
    }
```

(`ExtractTextAsync` resta invariato — regression pin sui suoi test.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UnstructuredPdfTextExtractorTests" -v minimal`
Expected: PASS (nuovi test verdi + i test esistenti di `ExtractPagedTextAsync`/`ExtractTextAsync` invariati).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Services/ExtractedElement.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/IPdfTextExtractor.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractor.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractorTests.cs
git commit -m "feat(extraction): map raw elements to StructuredElements in extractor"
```

---

### Task 3: Orchestrator — propaga `StructuredElements` (default path)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs:669-690` (`CreateEnhancedPagedResult`), `:877-908` (record + `FromStage`)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractor.cs:57-63`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractorTests.cs` (CREATE)

**Interfaces:**
- Consumes: `PagedTextExtractionResult.StructuredElements` (Task 2).
- Produces: `EnhancedPagedExtractionResult.StructuredElements`; `OrchestratedPdfTextExtractor.ExtractPagedTextAsync` che lo ripropaga. Il provider di default è `"Orchestrator"` (`DocumentProcessingServiceExtensions.cs:97,120`), quindi questo è il path di produzione.

- [ ] **Step 1: Write the failing test (real production path)**

Create `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractorTests.cs`. Il test esercita il **path di produzione reale**: costruisce un `EnhancedPdfProcessingOrchestrator` vero con uno stub Stage-1 che porta `StructuredElements`, lo wrappa nell'adapter e verifica che il campo sopravviva attraverso `CreateEnhancedPagedResult` + il mapping dell'adapter (i due hop che la produzione usa davvero — `FromStage` è dead code sul paged path):

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

[Trait("Category", TestCategories.Unit)]
public class OrchestratedPdfTextExtractorTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Stub Stage-1 extractor: returns a paged result carrying StructuredElements, with
    // ≥800 chars/page so the Stage-1 quality gate (CalculatePagedQualityScore ≥ 0.80) accepts it.
    private sealed class StubStage1Extractor : IPdfTextExtractor
    {
        private readonly IReadOnlyList<ExtractedElement> _elements;
        public StubStage1Extractor(IReadOnlyList<ExtractedElement> elements) => _elements = elements;

        public Task<TextExtractionResult> ExtractTextAsync(Stream s, bool ocr = true, CancellationToken ct = default) =>
            Task.FromResult(TextExtractionResult.CreateSuccess(new string('x', 800), 1, 800, false, ExtractionQuality.High));

        public Task<PagedTextExtractionResult> ExtractPagedTextAsync(Stream s, bool ocr = true, CancellationToken ct = default) =>
            Task.FromResult(PagedTextExtractionResult.CreateSuccess(
                new List<PageTextChunk> { new(1, new string('x', 800), 0, 799) },
                totalPages: 1, totalCharacters: 800, ocrTriggered: false, structuredElements: _elements));
    }

    private static Stream DummyPdf() =>
        new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest\n%%EOF"));

    [Fact]
    public async Task ExtractPagedTextAsync_PropagatesStructuredElements_ThroughRealOrchestratorPath()
    {
        var elements = new List<ExtractedElement> { new("Preparazione", 1, "Title") };
        var orchestrator = new EnhancedPdfProcessingOrchestrator(
            new StubStage1Extractor(elements),
            Mock.Of<IPdfTextExtractor>(),
            Mock.Of<IPdfTextExtractor>(),
            Mock.Of<ILogger<EnhancedPdfProcessingOrchestrator>>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new PdfProcessingOptions { LargePdfThresholdBytes = 52428800, UseTempFileForLargePdfs = true }),
            Mock.Of<ITextChunkingService>());
        var adapter = new OrchestratedPdfTextExtractor(orchestrator);

        await using var pdf = DummyPdf();
        var result = await adapter.ExtractPagedTextAsync(pdf, cancellationToken: TestCancellationToken);

        result.Success.Should().BeTrue();
        result.StructuredElements.Should().NotBeNull();
        result.StructuredElements!.Single().Text.Should().Be("Preparazione");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~OrchestratedPdfTextExtractorTests" -v minimal`
Expected: FAIL a runtime — il test compila (`StructuredElements` esiste su `PagedTextExtractionResult` dal Task 2), ma `result.StructuredElements` è `null` perché l'orchestrator lo droppa prima del fix di questo task.

- [ ] **Step 3: Add `StructuredElements` to `EnhancedPagedExtractionResult`**

In `EnhancedPdfProcessingOrchestrator.cs`, aggiorna il record (riga 877) aggiungendo il parametro finale:

```csharp
internal record EnhancedPagedExtractionResult(
    bool Success,
    IList<PageTextChunk> PageChunks,
    int TotalPages,
    int TotalCharacters,
    bool OcrTriggered,
    int StageUsed,
    string StageName,
    int TotalDurationMs,
    string? ErrorMessage = null,
    IReadOnlyList<ExtractedElement>? StructuredElements = null)
```

Aggiungi `using Api.BoundedContexts.DocumentProcessing.Domain.Services;` in testa al file se non presente.

In `FromStage` (riga 897) aggiungi il passaggio del campo:

```csharp
        return new EnhancedPagedExtractionResult(
            Success: result.Success,
            PageChunks: result.PageChunks,
            TotalPages: result.TotalPages,
            TotalCharacters: result.TotalCharacters,
            OcrTriggered: result.OcrTriggered,
            StageUsed: stageUsed,
            StageName: stageName,
            TotalDurationMs: totalDurationMs,
            ErrorMessage: result.ErrorMessage,
            StructuredElements: result.StructuredElements);
```

In `CreateEnhancedPagedResult` (riga 680) aggiungi lo stesso passaggio finale:

```csharp
        return new EnhancedPagedExtractionResult(
            Success: pagedResult.Success,
            PageChunks: pagedResult.PageChunks,
            TotalPages: pagedResult.TotalPages,
            TotalCharacters: pagedResult.TotalCharacters,
            OcrTriggered: pagedResult.OcrTriggered,
            StageUsed: stageUsed,
            StageName: stageName,
            TotalDurationMs: (int)totalDuration.TotalMilliseconds,
            ErrorMessage: pagedResult.ErrorMessage,
            StructuredElements: pagedResult.StructuredElements);
```

- [ ] **Step 4: Propagate in `OrchestratedPdfTextExtractor`**

In `OrchestratedPdfTextExtractor.cs`, nel mapping di `ExtractPagedTextAsync` (riga 57-63) aggiungi il campo finale:

```csharp
        return new PagedTextExtractionResult(
            Success: enhancedResult.Success,
            PageChunks: enhancedResult.PageChunks,
            TotalPages: enhancedResult.TotalPages,
            TotalCharacters: enhancedResult.TotalCharacters,
            OcrTriggered: enhancedResult.OcrTriggered,
            ErrorMessage: enhancedResult.ErrorMessage,
            StructuredElements: enhancedResult.StructuredElements);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~OrchestratedPdfTextExtractorTests" -v minimal`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractor.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Infrastructure/External/OrchestratedPdfTextExtractorTests.cs
git commit -m "feat(extraction): propagate StructuredElements through orchestrator"
```

---

### Task 4: `ExtractedDocumentFactory` — grouping Title→Section

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactory.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryTests.cs` (CREATE)

**Interfaces:**
- Consumes: `IReadOnlyList<ExtractedElement>?` (Task 2), `ExtractedDocument`/`DocumentSection` (`IAdvancedChunkingService.cs:43-110`).
- Produces: `static ExtractedDocument FromExtraction(Guid documentId, Guid? gameId, IReadOnlyList<ExtractedElement>? structuredElements, string flatText)`.

- [ ] **Step 1: Write the failing tests**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryTests.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class ExtractedDocumentFactoryTests
{
    private static ExtractedElement El(string text, string type, int page = 1) => new(text, page, type);
    private static readonly Guid Doc = Guid.NewGuid();

    [Fact]
    public void SingleTitle_CreatesOneSectionWithHeading()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Preparazione", "Title"), El("Disponi le tessere.", "NarrativeText") }, "ignored");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Heading.Should().Be("Preparazione");
        doc.Sections[0].ElementType.Should().Be("heading");
        doc.Sections[0].Content.Should().Be("Preparazione\n\nDisponi le tessere.");
    }

    [Fact]
    public void MultipleTitles_CreateSeparateSections()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Setup", "Title"), El("a", "NarrativeText"), El("Punteggio", "Title"), El("b", "NarrativeText") }, "x");

        doc.Sections.Select(s => s.Heading).Should().Equal("Setup", "Punteggio");
    }

    [Fact]
    public void ElementsBeforeFirstTitle_BecomePreambleWithNullHeading()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("intro text", "NarrativeText"), El("Setup", "Title"), El("body", "NarrativeText") }, "x");

        doc.Sections.Should().HaveCount(2);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("intro text");
        doc.Sections[1].Heading.Should().Be("Setup");
    }

    [Fact]
    public void ConsecutiveTitles_EmitHeadingOnlySection()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("A", "Title"), El("B", "Title"), El("body", "NarrativeText") }, "x");

        doc.Sections.Select(s => s.Heading).Should().Equal("A", "B");
        doc.Sections[0].Content.Should().Be("A");
    }

    [Fact]
    public void TrailingTitle_EmitsHeadingOnlySection()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("body", "NarrativeText"), El("Appendice", "Title") }, "x");

        doc.Sections.Last().Heading.Should().Be("Appendice");
        doc.Sections.Last().Content.Should().Be("Appendice");
    }

    [Fact]
    public void NoTitle_SinglePreambleWithAllContent()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("a", "NarrativeText"), El("b", "NarrativeText") }, "x");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("a\n\nb");
    }

    [Fact]
    public void CaseSensitiveTitle_LowercaseDoesNotOpenSection()
    {
        // "title" (lowercase) is NOT a section opener; only "Title" is. The lowercase element
        // therefore falls into the null-heading preamble (spec §6.1/§7 — text is never lost).
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("nope", "title"), El("Real", "Title") }, "x");

        doc.Sections.Should().HaveCount(2);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("nope");
        doc.Sections[1].Heading.Should().Be("Real");
    }

    [Fact]
    public void DocContent_IsAllElementsJoinedBySeparator()
    {
        // §6.2: doc.Content is every element concatenated in order, separated by "\n\n"
        // (independently computed — NOT read back from the produced offsets).
        var input = new[] { El("pre", "NarrativeText"), El("S1", "Title"), El("x", "NarrativeText"), El("S2", "Title") };
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, input, "ignored");

        doc.Content.Should().Be(string.Join("\n\n", input.Select(e => e.Text))); // "pre\n\nS1\n\nx\n\nS2"
    }

    [Fact]
    public void TableElement_MapsToTableElementTypeInBody()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("Costi", "Title"), El("r1 | r2", "Table") }, "x");

        doc.Sections[0].Content.Should().Contain("r1 | r2");
    }

    [Fact]
    public void SubstringInvariant_HoldsForEverySection()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null,
            new[] { El("pre", "NarrativeText"), El("S1", "Title"), El("x", "NarrativeText"), El("S2", "Title") }, "x");

        foreach (var s in doc.Sections)
        {
            s.Content.Should().Be(doc.Content.Substring(s.CharStart, s.CharEnd - s.CharStart));
        }
    }

    [Fact]
    public void NullStructuredElements_ProducesOnePreambleFromFlatText()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, null, "flat body text");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Heading.Should().BeNull();
        doc.Sections[0].Content.Should().Be("flat body text");
        doc.Sections[0].CharStart.Should().Be(0);
        doc.Sections[0].CharEnd.Should().Be("flat body text".Length);
        doc.Content.Should().Be("flat body text");
    }

    [Fact]
    public void EmptyStructuredElements_AlsoFallsBackToFlatText()
    {
        var doc = ExtractedDocumentFactory.FromExtraction(Doc, null, System.Array.Empty<ExtractedElement>(), "flat");

        doc.Sections.Should().HaveCount(1);
        doc.Sections[0].Content.Should().Be("flat");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ExtractedDocumentFactoryTests" -v minimal`
Expected: FAIL to compile — `ExtractedDocumentFactory` non esiste.

- [ ] **Step 3: Implement the factory**

Create `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactory.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// SP1: builds an <see cref="ExtractedDocument"/> with heading-aware <see cref="DocumentSection"/>s
/// by grouping raw extraction elements. A "Title" element opens a section (its text becomes the
/// heading); elements from the Title up to the next Title form the section content. Elements before
/// the first Title become a null-heading preamble. When no structured elements are available
/// (degradation: SmolDocling/Docnet/malformed response) a single preamble section carries the flat
/// text so the document content is never lost downstream.
/// </summary>
internal static class ExtractedDocumentFactory
{
    private const string ElementSeparator = "\n\n";
    private const string TitleCategory = "Title";

    public static ExtractedDocument FromExtraction(
        Guid documentId,
        Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string flatText)
    {
        if (structuredElements is null || structuredElements.Count == 0)
        {
            return NullPathDocument(documentId, gameId, flatText ?? string.Empty);
        }

        var sections = new List<DocumentSection>();
        var content = new System.Text.StringBuilder();
        var groups = GroupByTitle(structuredElements);

        foreach (var group in groups)
        {
            var sectionStart = content.Length;
            for (var i = 0; i < group.Elements.Count; i++)
            {
                if (i > 0) content.Append(ElementSeparator);
                content.Append(group.Elements[i].Text);
            }
            var sectionEnd = content.Length;

            sections.Add(new DocumentSection
            {
                Heading = group.Heading,
                Content = content.ToString(sectionStart, sectionEnd - sectionStart),
                Page = group.Elements[0].PageNumber,
                ElementType = NormalizeElementType(group.Elements[0].ElementType),
                CharStart = sectionStart,
                CharEnd = sectionEnd,
            });

            // Inter-section separator: lands in the GAP between this section's CharEnd and the
            // next section's CharStart, so doc.Content is fully "\n\n"-separated (§6.2) while the
            // substring invariant still holds (each section.Content excludes the trailing seam).
            if (!ReferenceEquals(group, groups[^1]))
            {
                content.Append(ElementSeparator);
            }
        }

        return new ExtractedDocument
        {
            Id = documentId,
            GameId = gameId,
            Content = content.ToString(),
            Sections = sections,
            PageCount = structuredElements.Max(e => e.PageNumber),
        };
    }

    private static ExtractedDocument NullPathDocument(Guid documentId, Guid? gameId, string flatText)
    {
        return new ExtractedDocument
        {
            Id = documentId,
            GameId = gameId,
            Content = flatText,
            PageCount = 1,
            Sections = new List<DocumentSection>
            {
                new()
                {
                    Heading = null,
                    Content = flatText,
                    Page = 1,
                    ElementType = "text",
                    CharStart = 0,
                    CharEnd = flatText.Length,
                },
            },
        };
    }

    private sealed record SectionGroup(string? Heading, List<ExtractedElement> Elements);

    private static List<SectionGroup> GroupByTitle(IReadOnlyList<ExtractedElement> elements)
    {
        var groups = new List<SectionGroup>();
        SectionGroup? current = null;

        foreach (var el in elements)
        {
            if (string.Equals(el.ElementType, TitleCategory, StringComparison.Ordinal))
            {
                current = new SectionGroup(el.Text, new List<ExtractedElement> { el });
                groups.Add(current);
            }
            else
            {
                if (current is null)
                {
                    current = new SectionGroup(null, new List<ExtractedElement>());
                    groups.Add(current);
                }
                current.Elements.Add(el);
            }
        }

        return groups;
    }

    private static string NormalizeElementType(string rawCategory) => rawCategory switch
    {
        "Title" => "heading",
        "Table" => "table",
        "ListItem" => "list",
        _ => "text",
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ExtractedDocumentFactoryTests" -v minimal`
Expected: PASS (tutti i casi, incluso l'invariante substring e il null-path).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactory.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryTests.cs
git commit -m "feat(chunking): ExtractedDocumentFactory groups elements into heading sections"
```

---

### Task 5: De-risk eval — fixture reale + criterio misurabile

**Files:**
- Create: `apps/api/tests/Api.Tests/TestData/unstructured-terraforming-response.json` (response reale catturata)
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryDeriskTests.cs`

**Interfaces:**
- Consumes: `UnstructuredPdfTextExtractor` (Task 2) + `ExtractedDocumentFactory` (Task 4).

- [ ] **Step 1: Capture a real Unstructured response**

Cattura **una** response reale del servizio `unstructured` per un rulebook IT disponibile (Terraforming Mars IT se caricabile, altrimenti un rulebook IT già in staging). Con lo stack dev up:

```bash
curl -s -X POST http://localhost:8001/api/v1/extract \
  -F "file=@<path-al-rulebook>.pdf" -F "strategy=fast" -F "language=ita" \
  -o apps/api/tests/Api.Tests/TestData/unstructured-terraforming-response.json
```

Ispeziona il campo `elements[]` e annota i heading (`category == "Title"`) effettivamente presenti. **Questo passaggio fissa il giudizio umano "heading sensati".** Se `elements` è vuoto o privo di `Title`, questo è il segnale di stop del gate: NON procedere a SP2, rivalutare l'approccio (es. strategy `hi_res`).

Assicurati che il file sia incluso come test asset (copia in output): in `apps/api/tests/Api.Tests/Api.Tests.csproj`, se non già presente un glob per `TestData/**`, aggiungi:

```xml
  <ItemGroup>
    <None Update="TestData\**\*.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 2: Write the de-risk test with a measurable threshold**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryDeriskTests.cs`. La fixture è guidata attraverso `UnstructuredPdfTextExtractor` (così valida anche la deserializzazione reale + `MapStructuredElements`, §6.4) e poi il factory — Stage-1 diretto, come da §8.3. Adatta `ExpectedHeadings` a quelli annotati allo Step 1:

```csharp
using System.Net;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

[Trait("Category", TestCategories.Unit)]
public class ExtractedDocumentFactoryDeriskTests
{
    // Sostituisci con i heading annotati allo Step 1 sul rulebook reale.
    private static readonly string[] ExpectedHeadings = { "Preparazione", "Punteggio" };
    private const int MinExpectedHits = 2; // gate: ≥ N di M heading recuperati
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RealRulebook_ThroughExtractor_RecoversKeyHeadings_AboveThreshold()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "unstructured-terraforming-response.json");
        var fixtureJson = File.ReadAllText(fixturePath);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(fixtureJson) });
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory.Setup(f => f.CreateClient("UnstructuredService"))
            .Returns(new HttpClient(handler.Object) { BaseAddress = new Uri("http://test:8001") });

        var extractor = new UnstructuredPdfTextExtractor(httpFactory.Object, Mock.Of<ILogger<UnstructuredPdfTextExtractor>>());
        using var pdf = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\ntest\n%%EOF"));
        var paged = await extractor.ExtractPagedTextAsync(pdf, cancellationToken: Ct);

        var flatText = paged.PageChunks.Count > 0 ? string.Concat(paged.PageChunks.Select(c => c.Text)) : "";
        var doc = ExtractedDocumentFactory.FromExtraction(Guid.NewGuid(), null, paged.StructuredElements, flatText);

        var headings = doc.Sections.Where(s => s.Heading != null).Select(s => s.Heading!).ToList();
        var hits = ExpectedHeadings.Count(expected =>
            headings.Any(h => h.Contains(expected, StringComparison.OrdinalIgnoreCase)));

        hits.Should().BeGreaterThanOrEqualTo(MinExpectedHits,
            $"il gate SP1 richiede ≥{MinExpectedHits}/{ExpectedHeadings.Length} heading chiave; trovati: {string.Join(", ", headings)}");
    }
}
```

> **Nota**: `ExtractedDocumentFactory` è `internal static`; il progetto di test accede ai tipi `internal` via `InternalsVisibleTo` (già configurato per gli altri test). Se il de-risk gate FALLISCE, è il segnale di stop: documenta gli heading trovati e non procedere a SP2.

- [ ] **Step 3: Run the de-risk test**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ExtractedDocumentFactoryDeriskTests" -v minimal`
Expected: PASS se il rulebook produce heading. **Se FAIL**: il gate ha fatto il suo lavoro — documenta nella PR gli heading trovati e ferma SP2 fino a rivalutazione (non aggirare il gate).

- [ ] **Step 4: Document the outcome + commit**

Annota nella descrizione della PR: rulebook usato, heading attesi, heading recuperati, esito del gate.

```bash
git add apps/api/tests/Api.Tests/TestData/unstructured-terraforming-response.json apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/Chunking/ExtractedDocumentFactoryDeriskTests.cs apps/api/tests/Api.Tests/Api.Tests.csproj
git commit -m "test(chunking): de-risk gate — real rulebook heading recovery"
```

---

## Final verification (prima della PR)

- [ ] `cd apps/unstructured-service && python -m pytest -q` → verde.
- [ ] `cd apps/api/src/Api && dotnet build` → 0 errori.
- [ ] `dotnet test ../../tests/Api.Tests --filter "Category=Unit&FullyQualifiedName~DocumentProcessing|FullyQualifiedName~Chunking" -v minimal` → verde (nuovi + regressione `ExtractPagedTextAsync`/`ExtractTextAsync`/`PageChunks`).
- [ ] PR verso `main-dev` con esito del de-risk gate documentato.

## Self-review coverage (spec → task)

- §5.1 Python `elements[]` → Task 1.
- §5.2 `ExtractedElement` in DP.Domain → Task 2 Step 1.
- §5.3 estrattore popola + orchestrator propaga → Task 2 (estrattore) + Task 3 (orchestrator).
- §5.4 factory → Task 4.
- §6.1 grouping (Title, consecutivi, trailing, preambolo, case-sensitive) → Task 4 test.
- §6.3 invariante substring → Task 4 `SubstringInvariant_HoldsForEverySection`.
- §6.4 normalizzazione ElementType + coalesce null → Task 2 `MapStructuredElements` + Task 4 `NormalizeElementType`.
- §7 null-path flat-text-safe → Task 4 `NullStructuredElements_*` + `EmptyStructuredElements_*`.
- §8.2 degradazione + PageChunks pin → Task 2 `ExtractPagedTextAsync_NoElements_*`.
- §8.3 de-risk gate misurabile → Task 5.

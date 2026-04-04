# RAG Pipeline Hardening — P0/P1/P2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere i bug critici del pipeline RAG (vettori non indicizzati, lingua hardcoded, chunk overflow) e aggiungere metriche di qualità, routing intelligente e cache semantica per ridurre costi del 60%.

**Architecture:** Il fix P0 è un set di correzioni chirurgiche su file esistenti; P1 aggiunge nuovi servizi ortogonali che si integrano con il flusso `AskQuestionQueryHandler`; P2 è backlog con refactoring più ampio. Ogni task è indipendente e committabile separatamente.

**Tech Stack:** .NET 9, EF Core + pgvector (PostgreSQL 16), Python FastAPI, xUnit, Pgvector NuGet, StackExchange.Redis

---

## File Map

### P0 — File Modificati

| File | Modifica |
|------|----------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs` | `DefaultMinScore` 0.0 → 0.35 |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/IEmbeddingProvider.cs` | Aggiunge metodo `GenerateBatchEmbeddingsAsync(texts, language, ct)` con default impl |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/Providers/HttpEmbeddingProvider.cs` | Override con language parameter |
| `apps/api/src/Api/Services/EmbeddingService.cs` | Fix overload `GenerateEmbeddingsAsync(texts, language, ct)` per passare language al provider |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs` | (1) passa `pdfDoc.Language` all'embedding call; (2) salva `PgVectorEmbeddingEntity` dopo FTS save |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs` | Fix `IndexChunksInVectorStoreAsync` per salvare embeddings reali; cleanup stale Qdrant refs |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs` | Split PageChunks SmolDocling > 1800 chars |
| `apps/api/src/Api/Constants/ChunkingConstants.cs` | Aggiunge `MaxEmbeddingChars = 1800` |

### P1 — File Creati/Modificati

| File | Tipo | Scopo |
|------|------|-------|
| `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/RagQualityLogEntity.cs` | Crea | Entity EF per metriche RAG |
| `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | Modifica | Aggiunge `DbSet<RagQualityLogEntity>` |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagQualityTracker.cs` | Crea | Interfaccia tracker metriche |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagQualityTracker.cs` | Crea | Implementazione tracker |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs` | Modifica | Log metriche + routing + prompt migliorato |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/QueryComplexityAnalyzer.cs` | Crea | Analizzatore complessità query |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ISemanticResponseCache.cs` | Crea | Interfaccia cache semantica |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Cache/SemanticResponseCache.cs` | Crea | Implementazione Redis cache |

### P2 — File Principali (backlog, specifiche ad alto livello)

| File | Scopo |
|------|-------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/IVectorStoreAdapter.cs` | Generalizza con SearchScope |
| `apps/*/main.py` (tutti i servizi Python) | Aggiunge `/openapi.json` versioned endpoint |
| `tests/Api.Tests/Contracts/` (nuova cartella) | Pact contract tests |

---

## ═══════════════ P0 — CRITICO ═══════════════

---

## Task P0-1: Fix VectorSemanticSearchQueryHandler.DefaultMinScore

**File:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs:19`
- Test: `tests/Api.Tests/Unit/KnowledgeBase/VectorSemanticSearchQueryHandlerTests.cs` (crea)

**Contesto:** `DefaultMinScore = 0.0` significa che chunk irrilevanti (score 0.01) vengono passati al LLM come contesto, causando allucinazioni. La fix imposta la soglia a 0.35.

- [ ] **Step 1: Crea il test failing**

```csharp
// tests/Api.Tests/Unit/KnowledgeBase/VectorSemanticSearchQueryHandlerTests.cs
using Xunit;
using System.Reflection;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class VectorSemanticSearchQueryHandlerTests
{
    [Fact]
    public void DefaultMinScore_ShouldBeAtLeast_0_35()
    {
        // Arrange — leggiamo il valore tramite reflection per evitare di renderlo public
        var type = typeof(Api.BoundedContexts.KnowledgeBase.Application.Queries
            .VectorSemanticSearchQueryHandler);
        var field = type.GetField("DefaultMinScore",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);

        // Act
        var value = (double)field.GetValue(null)!;

        // Assert
        Assert.True(value >= 0.35,
            $"DefaultMinScore was {value}, expected >= 0.35 to filter irrelevant chunks");
    }
}
```

- [ ] **Step 2: Esegui il test per verificare che fallisca**

```bash
cd tests/Api.Tests
dotnet test --filter "DefaultMinScore_ShouldBeAtLeast_0_35" -v
# Expected: FAIL — "DefaultMinScore was 0, expected >= 0.35"
```

- [ ] **Step 3: Applica il fix**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs
// Riga 19 — cambia:
private const double DefaultMinScore = 0.0;
// in:
private const double DefaultMinScore = 0.35;
```

- [ ] **Step 4: Verifica che il test passi**

```bash
dotnet test --filter "DefaultMinScore_ShouldBeAtLeast_0_35" -v
# Expected: PASS
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/VectorSemanticSearchQueryHandler.cs \
        tests/Api.Tests/Unit/KnowledgeBase/VectorSemanticSearchQueryHandlerTests.cs
git commit -m "fix(rag): set DefaultMinScore to 0.35 to filter irrelevant chunks"
```

---

## Task P0-2: Language-Aware Embedding Pipeline

**File:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/IEmbeddingProvider.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/Providers/HttpEmbeddingProvider.cs`
- Modify: `apps/api/src/Api/Services/EmbeddingService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`
- Test: `tests/Api.Tests/Unit/KnowledgeBase/HttpEmbeddingProviderLanguageTests.cs` (crea)

**Contesto:** `HttpEmbeddingProvider` invia sempre `language = "en"` all'embedding service Python, ignorando la lingua del documento. `IEmbeddingService` ha già l'overload `GenerateEmbeddingsAsync(texts, language, ct)` (riga 26 di `IEmbeddingService.cs`) ma `EmbeddingService` lo implementa chiamando la versione senza lingua (riga 164 di `EmbeddingService.cs`). Fix: aggiungere `GenerateBatchEmbeddingsAsync(texts, language, ct)` come default interface method a `IEmbeddingProvider`, override in `HttpEmbeddingProvider`, e correggere il flow in `EmbeddingService`.

- [ ] **Step 1: Crea il test failing**

```csharp
// tests/Api.Tests/Unit/KnowledgeBase/HttpEmbeddingProviderLanguageTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class HttpEmbeddingProviderLanguageTests
{
    [Fact]
    public async Task GenerateBatchEmbeddingsAsync_WithItalianLanguage_SendsCorrectLanguageToService()
    {
        // Arrange
        string? capturedLanguage = null;
        var handler = new DelegatingHandlerStub(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            var doc = JsonDocument.Parse(body);
            capturedLanguage = doc.RootElement.GetProperty("language").GetString();

            // Return fake response
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(JsonSerializer.Serialize(new
            {
                embeddings = new[] { new[] { 0.1f, 0.2f } },
                model = "intfloat/multilingual-e5-base"
            }), Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler);
        var config = new EmbeddingConfiguration
        {
            LocalServiceUrl = "http://test-service:8000",
            Dimensions = 768,
            Model = "intfloat/multilingual-e5-base"
        };
        var provider = new HttpEmbeddingProvider(httpClient, NullLogger<HttpEmbeddingProvider>.Instance, config);

        // Act — usa overload con language
        await provider.GenerateBatchEmbeddingsAsync(["testo di test"], "it");

        // Assert
        Assert.Equal("it", capturedLanguage);
    }
}

// Helper per mock HTTP
internal class DelegatingHandlerStub(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct) => handler(request);
}
```

- [ ] **Step 2: Esegui il test per verificare che fallisca**

```bash
dotnet test --filter "GenerateBatchEmbeddingsAsync_WithItalianLanguage" -v
# Expected: FAIL — metodo non esiste o language = "en" hardcoded
```

- [ ] **Step 3: Aggiungi metodo con default impl a IEmbeddingProvider**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/IEmbeddingProvider.cs
// Aggiungere DOPO il metodo GenerateBatchEmbeddingsAsync esistente (riga 46):

/// <summary>
/// Generate embeddings with explicit language hint.
/// Default implementation ignores language; override in language-aware providers.
/// </summary>
Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(
    IReadOnlyList<string> texts,
    string language,
    CancellationToken cancellationToken = default)
    => GenerateBatchEmbeddingsAsync(texts, cancellationToken); // default: ignora language
```

- [ ] **Step 4: Override in HttpEmbeddingProvider**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/Providers/HttpEmbeddingProvider.cs
// Aggiungere il metodo override DOPO il metodo GenerateBatchEmbeddingsAsync esistente:

// NOTA: NON usare "override" — il DIM sull'interfaccia non ha corrispondente virtual nella base class.
// Il metodo senza language usa "override" perché ha un abstract/virtual nella base class (EmbeddingProviderBase).
// Questo nuovo overload implementa direttamente il DIM dell'interfaccia:
public async Task<EmbeddingProviderResult> GenerateBatchEmbeddingsAsync(
    IReadOnlyList<string> texts,
    string language,
    CancellationToken cancellationToken = default)
{
    if (texts == null || texts.Count == 0)
        return EmbeddingProviderResult.CreateFailure("No texts provided");

    Logger.LogInformation(
        "Generating embeddings for {Count} texts in language '{Language}' using {Url}",
        texts.Count, language, _serviceUrl);

    try
    {
        var request = new ExternalEmbeddingRequest
        {
            Texts = texts.ToList(),
            Language = language  // ← usa il parametro, non "en" hardcoded
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpClient.PostAsync($"{_serviceUrl}/embeddings", content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<ExternalEmbeddingResponse>(responseBody, JsonOptions);

        if (result?.Embeddings == null || result.Embeddings.Count == 0)
            return EmbeddingProviderResult.CreateFailure("Empty response from embedding service");

        return EmbeddingProviderResult.CreateSuccess(
            result.Embeddings.Select(e => e.ToArray()).ToList(),
            model: result.Model);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error calling embedding service for language '{Language}'", language);
        return EmbeddingProviderResult.CreateFailure($"HTTP call failed: {ex.Message}");
    }
}
```

> **Nota:** Se `ExternalEmbeddingResponse` e il codice HTTP esistente nel metodo originale usano nomi interni diversi, adatta di conseguenza copiando la logica dal metodo non-language già esistente (righe 39-140 circa di `HttpEmbeddingProvider.cs`). L'unica differenza è `Language = language` invece di `Language = "en"`.

- [ ] **Step 5: Fix EmbeddingService language overload (riga ~164)**

```csharp
// apps/api/src/Api/Services/EmbeddingService.cs
// Nel metodo GenerateEmbeddingsAsync(List<string> texts, string language, CancellationToken ct)
// Trova la riga (circa 163-164):
//     // Use standard embedding generation (providers handle language internally if supported)
//     return await GenerateEmbeddingsAsync(texts, ct).ConfigureAwait(false);
//
// Sostituisci con:
        _logger.LogInformation(
            "Using language-aware embedding for language '{Language}'", language);
        var result = await _primaryProvider.GenerateBatchEmbeddingsAsync(texts, language, ct)
            .ConfigureAwait(false);

        if (!result.Success && _fallbackProvider != null)
        {
            _logger.LogWarning("Primary provider failed for language '{Language}', trying fallback", language);
            result = await _fallbackProvider.GenerateBatchEmbeddingsAsync(texts, language, ct)
                .ConfigureAwait(false);
        }

        return result.Success
            ? EmbeddingResult.Success(result.Embeddings.Select(e => e.ToArray()).ToList(), result.Model)
            : EmbeddingResult.Failure(result.ErrorMessage ?? "Unknown embedding error");
```

> **Nota:** adatta i tipi di ritorno (`EmbeddingResult.Success/Failure`) al pattern già usato nelle righe precedenti dello stesso metodo in `EmbeddingService.cs`.

- [ ] **Step 6: Fix UploadPdfCommandHandler.Processing.cs — passa pdfDoc.Language**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs
// Riga 359 — cambia:
            var batchResult = await embeddingService.GenerateEmbeddingsAsync(batchTexts).ConfigureAwait(false);
// in:
            var batchResult = await embeddingService.GenerateEmbeddingsAsync(
                batchTexts, pdfDoc.Language ?? "en").ConfigureAwait(false);
```

- [ ] **Step 7: Esegui il test**

```bash
dotnet test --filter "GenerateBatchEmbeddingsAsync_WithItalianLanguage" -v
# Expected: PASS
```

- [ ] **Step 8: Build check**

```bash
cd apps/api/src/Api
dotnet build --no-restore 2>&1 | tail -20
# Expected: Build succeeded, 0 errors
```

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/IEmbeddingProvider.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/Providers/HttpEmbeddingProvider.cs \
        apps/api/src/Api/Services/EmbeddingService.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs \
        tests/Api.Tests/Unit/KnowledgeBase/HttpEmbeddingProviderLanguageTests.cs
git commit -m "fix(rag): propagate document language to embedding service instead of hardcoded 'en'"
```

---

## Task P0-3: Fix Vector Indexing — Salva Embeddings in pgvector

**File:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs`
- Test: `tests/Api.Tests/Unit/DocumentProcessing/VectorIndexingTests.cs` (crea)

**Contesto:** `IndexInVectorStoreAsync` in `UploadPdfCommandHandler.Processing.cs` (riga 472) ha il commento `// Vector store (Qdrant) has been removed — skip vector indexing.` e non persiste i vettori generati. `MeepleAiDbContext.PgVectorEmbeddings` (`DbSet<PgVectorEmbeddingEntity>`) è disponibile. La tabella `pgvector_embeddings` esiste già in PostgreSQL. `PgVectorEmbeddingEntity` ha: `Id, VectorDocumentId, GameId, TextContent, Vector (pgvector), Model, ChunkIndex, PageNumber, CreatedAt, Lang`.

- [ ] **Step 1: Aggiungi `MaxEmbeddingChars` a ChunkingConstants**

```csharp
// apps/api/src/Api/Constants/ChunkingConstants.cs
// Aggiungere costante:
public const int MaxEmbeddingChars = 1800; // Max chars per chunk per E5-base (512 token * ~4 chars/token - buffer)
```

- [ ] **Step 2: Crea il test per verificare che i vettori vengono salvati**

```csharp
// tests/Api.Tests/Unit/DocumentProcessing/VectorIndexingTests.cs
using Api.Infrastructure.Entities.KnowledgeBase;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Verifica che il pipeline di upload PDF salvi effettivamente i vettori in pgvector.
/// Questi sono test di integrazione leggeri che operano direttamente sulle entity.
/// </summary>
public sealed class VectorIndexingTests
{
    [Fact]
    public void PgVectorEmbeddingEntity_CanBeConstructed_WithRequiredFields()
    {
        // Verifica che l'entity che andremo a creare nel fix sia costruibile
        var entity = new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "test content",
            Vector = new Pgvector.Vector(new float[] { 0.1f, 0.2f, 0.3f }),
            Model = "intfloat/multilingual-e5-base",
            ChunkIndex = 0,
            PageNumber = 1,
            Lang = "it"
        };

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("it", entity.Lang);
        Assert.Equal(0, entity.ChunkIndex);
    }

    [Fact]
    public void PgVectorEmbeddingEntity_Lang_DefaultsToEn()
    {
        var entity = new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "test",
            Vector = new Pgvector.Vector(new float[] { 0.1f }),
            Model = "test-model",
            ChunkIndex = 0,
            PageNumber = 1
        };

        Assert.Equal("en", entity.Lang);
    }
}
```

- [ ] **Step 3: Esegui il test (deve passare — verifica compilazione)**

```bash
dotnet test --filter "VectorIndexingTests" -v
# Expected: PASS (costruzione entity deve funzionare)
```

- [ ] **Step 4: Fix IndexInVectorStoreAsync in UploadPdfCommandHandler.Processing.cs**

Trova il metodo `IndexInVectorStoreAsync` (riga 455). Sostituisci il corpo con:

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs
// Metodo IndexInVectorStoreAsync — SOSTITUISCI CORPO:

    private async Task IndexInVectorStoreAsync(
        string pdfId,
        Guid userId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> allDocumentChunks,
        List<float[]> embeddings,
        MeepleAiDbContext db,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var totalPages = pdfDoc.PageCount ?? 0;
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, cancellationToken)
            .ConfigureAwait(false);

        var indexingStopwatch = Stopwatch.StartNew();

        // 1. Aggiorna o crea VectorDocumentEntity
        await UpdateVectorDocumentAsync(pdfId, pdfDoc, allDocumentChunks.Count, db, cancellationToken)
            .ConfigureAwait(false);

        // 2. Salva TextChunks su PostgreSQL per hybrid search FTS
        await SaveTextChunksForHybridSearchAsync(pdfId, pdfDoc, allDocumentChunks, db, cancellationToken)
            .ConfigureAwait(false);

        // 3. Salva embeddings in pgvector
        await SaveEmbeddingsToPgVectorAsync(pdfId, pdfDoc, allDocumentChunks, embeddings, db, cancellationToken)
            .ConfigureAwait(false);

        indexingStopwatch.Stop();
        RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);

        _logger.LogInformation(
            "✅ [INDEXING] PDF {PdfId}: {ChunkCount} chunks indexed in pgvector in {Ms}ms",
            pdfId, allDocumentChunks.Count, indexingStopwatch.Elapsed.TotalMilliseconds);
    }
```

- [ ] **Step 5: Aggiungi il metodo helper `SaveEmbeddingsToPgVectorAsync`**

Aggiungere DOPO il metodo `SaveTextChunksForHybridSearchAsync` esistente:

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs

    private async Task SaveEmbeddingsToPgVectorAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> chunks,
        List<float[]> embeddings,
        MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);
        var gameId = pdfDoc.PrivateGameId ?? pdfDoc.GameId ?? Guid.Empty;
        var language = pdfDoc.Language ?? "en";

        // Recupera VectorDocumentId appena creato
        var vectorDoc = await db.VectorDocuments
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken)
            .ConfigureAwait(false);

        if (vectorDoc == null)
        {
            _logger.LogError("❌ [PG-VECTOR] VectorDocument not found for PDF {PdfId} — skip pgvector indexing", pdfId);
            return;
        }

        // Elimina eventuali embedding precedenti (re-index scenario)
        var existing = await db.PgVectorEmbeddings
            .Where(e => e.VectorDocumentId == vectorDoc.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            db.PgVectorEmbeddings.RemoveRange(existing);
            _logger.LogInformation("🗑️ [PG-VECTOR] Removed {Count} existing embeddings for {PdfId}", existing.Count, pdfId);
        }

        // Determina il nome del modello dal risultato
        var config = scope.ServiceProvider.GetRequiredService<IOptions<EmbeddingConfiguration>>().Value;
        var modelName = config.GetEffectiveModel() ?? "intfloat/multilingual-e5-base";

        // Costruisci e salva le nuove embedding entity in batch da 500
        const int saveBatchSize = 500;
        var batchCount = (int)Math.Ceiling((double)chunks.Count / saveBatchSize);

        for (var batchIdx = 0; batchIdx < batchCount; batchIdx++)
        {
            var skip = batchIdx * saveBatchSize;
            var batchChunks = chunks.Skip(skip).Take(saveBatchSize).ToList();
            var batchEmbeddings = embeddings.Skip(skip).Take(saveBatchSize).ToList();

            var entities = batchChunks.Select((chunk, i) => new PgVectorEmbeddingEntity
            {
                Id = Guid.NewGuid(),
                VectorDocumentId = vectorDoc.Id,
                GameId = gameId,
                TextContent = chunk.Text,
                Vector = new Pgvector.Vector(batchEmbeddings[i]),
                Model = modelName,
                ChunkIndex = skip + i,
                PageNumber = Math.Max(1, chunk.Page),
                Lang = language,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList();

            await db.PgVectorEmbeddings.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("💾 [PG-VECTOR] Saved batch {Idx}/{Total}: {Count} embeddings",
                batchIdx + 1, batchCount, entities.Count);
        }
    }
```

> **Nota:** `scope` è già disponibile nel metodo `ProcessPdfAsync` che chiama `IndexInVectorStoreAsync` — passarlo come parametro se non già nel scope. Aggiungere `using Api.Infrastructure.Entities.KnowledgeBase;` e `using Microsoft.Extensions.Options;` agli using del file se mancanti.

- [ ] **Step 6: Fix IndexPdfCommandHandler.cs — IndexChunksInVectorStoreAsync**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs
// Riga 286-309 — SOSTITUISCI il metodo con:

    /// <summary>
    /// Indexes document chunks in pgvector and updates VectorDocument.
    /// Previously used Qdrant; now uses pgvector via MeepleAiDbContext.
    /// </summary>
    private async Task<bool> IndexChunksInVectorStoreAsync(
        string pdfId,
        string gameId,
        string extractedText,
        List<DocumentChunk> documentChunks,
        VectorDocumentEntity vectorDoc,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);
        var gameGuid = Guid.TryParse(gameId, out var g) ? g : Guid.Empty;

        // Elimina embedding esistenti per questo VectorDocument
        var existing = await _db.PgVectorEmbeddings
            .Where(e => e.VectorDocumentId == vectorDoc.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _db.PgVectorEmbeddings.RemoveRange(existing);
            _logger.LogInformation("🗑️ [REINDEX] Removed {Count} old embeddings for PDF {PdfId}", existing.Count, pdfId);
        }

        // Recupera lingua dal documento
        var pdfDoc = await _db.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken)
            .ConfigureAwait(false);
        var language = pdfDoc?.Language ?? "en";
        var modelName = _embeddingConfig.GetEffectiveModel() ?? "intfloat/multilingual-e5-base";

        // Salva nuovi embedding in batch da 500
        const int saveBatchSize = 500;
        var batchCount = (int)Math.Ceiling((double)documentChunks.Count / saveBatchSize);

        for (var batchIdx = 0; batchIdx < batchCount; batchIdx++)
        {
            var batchChunks = documentChunks.Skip(batchIdx * saveBatchSize).Take(saveBatchSize).ToList();
            var entities = batchChunks.Select(chunk => new PgVectorEmbeddingEntity
            {
                Id = Guid.NewGuid(),
                VectorDocumentId = vectorDoc.Id,
                GameId = gameGuid,
                TextContent = chunk.Text,
                Vector = new Pgvector.Vector(chunk.Embedding),
                Model = modelName,
                ChunkIndex = chunk.Page > 0 ? chunk.Page - 1 : 0, // ChunkIndex dal Page (approssimazione)
                PageNumber = Math.Max(1, chunk.Page),
                Lang = language,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList();

            await _db.PgVectorEmbeddings.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Aggiorna VectorDocumentEntity
        vectorDoc.IndexingStatus = "completed";
        vectorDoc.ChunkCount = documentChunks.Count;
        vectorDoc.TotalCharacters = extractedText.Length;
        vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
        vectorDoc.IndexingError = null;
        vectorDoc.EmbeddingModel = modelName;

        _logger.LogInformation("✅ [REINDEX] PDF {PdfId}: {Count} chunks indexed in pgvector", pdfId, documentChunks.Count);
        return true;
    }
```

> **Nota:** `_embeddingConfig` va iniettato nel costruttore di `IndexPdfCommandHandler`. Aggiungi `private readonly EmbeddingConfiguration _embeddingConfig;` come field e iniettalo via `IOptions<EmbeddingConfiguration>` nel costruttore. Aggiungere `using Api.Infrastructure.Entities.KnowledgeBase;` agli using.

- [ ] **Step 7: Cleanup Qdrant stale references in IndexPdfCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs
// Riga 23 — aggiorna il docstring:
// DA: "Index to Qdrant" (o simile)
// A:  "Index document text chunks and embeddings to pgvector."

// Riga 88 — aggiorna il codice errore:
// DA: PdfIndexingErrorCode.QdrantIndexingFailed
// A:  PdfIndexingErrorCode.VectorIndexingFailed
// (Se l'enum non ha VectorIndexingFailed, aggiungilo all'enum PdfIndexingErrorCode)
```

- [ ] **Step 8: Build check**

```bash
cd apps/api/src/Api
dotnet build --no-restore 2>&1 | tail -20
# Expected: Build succeeded, 0 errors
```

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs \
        apps/api/src/Api/Constants/ChunkingConstants.cs \
        apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/PgVectorEmbeddingEntity.cs \
        tests/Api.Tests/Unit/DocumentProcessing/VectorIndexingTests.cs
git commit -m "fix(rag): persist embeddings to pgvector — IndexInVectorStoreAsync was a no-op since Qdrant removal"
```

---

## Task P0-4: Fix SmolDocling Chunk Overflow

**File:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs`
- Test: `tests/Api.Tests/Unit/DocumentProcessing/SmolDoclingChunkOverflowTests.cs` (crea)

**Contesto:** SmolDocling produce un chunk per pagina (`PageTextChunk`). Pagine dense (>1800 chars) eccedono il limite del modello E5-base (512 token ≈ 1800 chars), causando troncamento silenzioso e perdita di contenuto non ricercabile. Fix: split post-estrazione di ogni PageChunk > `MaxEmbeddingChars`.

- [ ] **Step 1: Crea il test failing**

```csharp
// tests/Api.Tests/Unit/DocumentProcessing/SmolDoclingChunkOverflowTests.cs
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Constants;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

public sealed class SmolDoclingChunkOverflowTests
{
    [Fact]
    public void PageTextChunk_LargerThan1800Chars_IsEmpty_ReturnsFalse()
    {
        var longText = new string('a', 2000); // 2000 chars > MaxEmbeddingChars (1800)
        var chunk = new PageTextChunk(1, longText, 0, 2000);

        // Il chunk non è vuoto
        Assert.False(chunk.IsEmpty);
        // Verifica che il test cattura il problema: CharCount > MaxEmbeddingChars
        Assert.True(chunk.CharCount > ChunkingConstants.MaxEmbeddingChars,
            $"Chunk has {chunk.CharCount} chars, exceeds limit of {ChunkingConstants.MaxEmbeddingChars}");
    }

    [Theory]
    [InlineData(1800, false)] // exactly at limit — ok, no split needed
    [InlineData(1801, true)]  // just over limit — must split
    [InlineData(3500, true)]  // very large page — must split
    public void ChunkNeedsSplit_BasedOnCharCount(int charCount, bool expectedNeedsSplit)
    {
        var chunk = new PageTextChunk(1, new string('x', charCount), 0, charCount);
        var needsSplit = chunk.CharCount > ChunkingConstants.MaxEmbeddingChars;
        Assert.Equal(expectedNeedsSplit, needsSplit);
    }
}
```

- [ ] **Step 2: Esegui il test**

```bash
dotnet test --filter "SmolDoclingChunkOverflowTests" -v
# Expected: PASS (i test verificano il problema, non ancora il fix)
```

- [ ] **Step 3: Inietta TextChunkingService nel costruttore di EnhancedPdfProcessingOrchestrator**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs
// Nel costruttore — aggiungere parametro:
//   ITextChunkingService chunkingService (o TextChunkingService se non ha interface)

// Aggiungere field:
private readonly TextChunkingService _chunkingService;

// Nel costruttore — aggiungere:
_chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
```

> **Nota:** se `TextChunkingService` non ha un'interfaccia pubblica `ITextChunkingService`, iniettare il tipo concreto direttamente. Se la DI registration manca, aggiungerla in `DocumentProcessingServiceExtensions.cs`.

- [ ] **Step 4: Aggiungi metodo helper per split post-SmolDocling**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs
// Aggiungere il metodo privato (es. dopo CalculatePagedQualityScore):

    /// <summary>
    /// Splits PageTextChunks that exceed MaxEmbeddingChars into sub-chunks.
    /// SmolDocling produces one chunk per page; dense pages exceed E5-base token limit.
    /// </summary>
    private IList<PageTextChunk> SplitOversizedPageChunks(IList<PageTextChunk> pageChunks)
    {
        var result = new List<PageTextChunk>();
        foreach (var chunk in pageChunks)
        {
            if (chunk.IsEmpty || chunk.CharCount <= ChunkingConstants.MaxEmbeddingChars)
            {
                result.Add(chunk);
                continue;
            }

            // Suddividi con TextChunkingService
            var subChunks = _chunkingService.ChunkText(
                chunk.Text,
                chunkSize: ChunkingConstants.MaxEmbeddingChars,
                overlap: ChunkingConstants.DefaultChunkOverlap);

            var subIndex = 0;
            foreach (var sub in subChunks)
            {
                result.Add(new PageTextChunk(
                    PageNumber: chunk.PageNumber,
                    Text: sub.Text,                       // TextChunk.Text (da TextChunkingService.cs:338)
                    CharStartIndex: chunk.CharStartIndex + sub.CharStart,
                    CharEndIndex: chunk.CharStartIndex + sub.CharEnd));
                subIndex++;
            }

            _logger.LogDebug(
                "📄 [SMOL-SPLIT] Page {Page}: {OrigLen} chars → {SubCount} sub-chunks",
                chunk.PageNumber, chunk.CharCount, subChunks.Count);
        }
        return result;
    }
```

> **Nota:** `TextChunk` (definito in `apps/api/src/Api/Services/TextChunkingService.cs:336`) ha i campi `Text`, `Index`, `CharStart`, `CharEnd`. Il codice sopra usa già i nomi corretti.

- [ ] **Step 5: Chiama SplitOversizedPageChunks dopo SmolDocling Stage 2**

Trovare nel metodo `TryStages1And2PagedAsync` (circa riga 206-212) dove si usa `stage2Result`:

```csharp
// Dopo la linea che crea CreateEnhancedPagedResult con stage2Result, trovare dove si usano i PageChunks.
// Trovare il punto dove pagedResult.PageChunks viene restituito o usato.
// Aggiungere la chiamata al metodo di split:

// Esempio: dopo Stage 2 acceptance (riga ~212):
        if (qualityScore >= Stage2QualityThreshold)
        {
            // AGGIUNGERE QUI:
            var splitChunks = SplitOversizedPageChunks(stage2Result.PageChunks);
            stage2Result = stage2Result with { PageChunks = splitChunks };
            // ...poi continua come prima
            return CreateEnhancedPagedResult(stage2Result, 2, "SmolDocling", TimeSpan.Zero, requestId);
        }
```

- [ ] **Step 6: Build check**

```bash
cd apps/api/src/Api
dotnet build --no-restore 2>&1 | tail -20
# Expected: Build succeeded, 0 errors
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/EnhancedPdfProcessingOrchestrator.cs \
        tests/Api.Tests/Unit/DocumentProcessing/SmolDoclingChunkOverflowTests.cs
git commit -m "fix(rag): split SmolDocling page chunks exceeding E5-base 1800-char embedding limit"
```

---

## ═══════════════ P1 — IMPORTANTI ═══════════════

---

## Task P1-1: Tabella rag_quality_logs + Entity

**File:**
- Create: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/RagQualityLogEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`
- Create: Migrazione EF (`dotnet ef migrations add AddRagQualityLogs`)

- [ ] **Step 1: Crea RagQualityLogEntity**

```csharp
// apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/RagQualityLogEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

[Table("rag_quality_logs")]
public class RagQualityLogEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("thread_id")]
    public Guid? ThreadId { get; set; }

    [Column("game_id")]
    public Guid? GameId { get; set; }

    [Column("query_length")]
    public int QueryLength { get; set; }

    [Column("chunks_retrieved")]
    public int ChunksRetrieved { get; set; }

    [Column("chunks_used")]
    public int ChunksUsed { get; set; }

    [Column("context_precision")]
    public decimal? ContextPrecision { get; set; }

    [Column("citations_count")]
    public int CitationsCount { get; set; }

    [Required]
    [Column("strategy")]
    [MaxLength(50)]
    public string Strategy { get; set; } = string.Empty;

    [Required]
    [Column("model_used")]
    [MaxLength(100)]
    public string ModelUsed { get; set; } = string.Empty;

    [Column("input_tokens")]
    public int? InputTokens { get; set; }

    [Column("output_tokens")]
    public int? OutputTokens { get; set; }

    [Column("latency_ms")]
    public int LatencyMs { get; set; }

    [Column("cache_hit")]
    public bool CacheHit { get; set; }

    [Column("no_relevant_context")]
    public bool NoRelevantContext { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 2: Aggiungi DbSet al MeepleAiDbContext**

```csharp
// apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
// Aggiungere accanto agli altri DbSet KnowledgeBase (cerca la sezione PgVectorEmbeddings):
public DbSet<RagQualityLogEntity> RagQualityLogs => Set<RagQualityLogEntity>();
```

- [ ] **Step 3: Crea la migrazione**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddRagQualityLogs
# Expected: crea apps/api/src/Api/Infrastructure/Migrations/{timestamp}_AddRagQualityLogs.cs
```

- [ ] **Step 4: Verifica migrazione generata**

Aprire il file migrazione appena creato e verificare che contenga `CreateTable` per `rag_quality_logs` con tutte le colonne attese.

- [ ] **Step 5: Build check**

```bash
dotnet build --no-restore 2>&1 | tail -10
# Expected: Build succeeded
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/RagQualityLogEntity.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs \
        apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(rag): add rag_quality_logs table for RAG quality metrics tracking"
```

---

## Task P1-2: IRagQualityTracker + Implementazione

**File:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagQualityTracker.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagQualityTracker.cs`
- Test: `tests/Api.Tests/Unit/KnowledgeBase/RagQualityTrackerTests.cs` (crea)

- [ ] **Step 1: Crea IRagQualityTracker**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagQualityTracker.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface IRagQualityTracker
{
    Task TrackQueryAsync(RagQueryMetrics metrics, CancellationToken ct = default);
}

internal sealed record RagQueryMetrics(
    Guid? ThreadId,
    Guid? GameId,
    int QueryLength,
    int ChunksRetrieved,
    int ChunksUsed,
    int CitationsCount,
    string Strategy,
    string ModelUsed,
    int LatencyMs,
    bool CacheHit,
    bool NoRelevantContext,
    int? InputTokens = null,
    int? OutputTokens = null,
    decimal? ContextPrecision = null);
```

- [ ] **Step 2: Crea test failing**

```csharp
// tests/Api.Tests/Unit/KnowledgeBase/RagQualityTrackerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class RagQualityTrackerTests
{
    [Fact]
    public void RagQueryMetrics_ConstructsCorrectly()
    {
        var metrics = new RagQueryMetrics(
            ThreadId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            QueryLength: 42,
            ChunksRetrieved: 10,
            ChunksUsed: 5,
            CitationsCount: 3,
            Strategy: "Balanced",
            ModelUsed: "deepseek-chat",
            LatencyMs: 1234,
            CacheHit: false,
            NoRelevantContext: false);

        Assert.Equal(42, metrics.QueryLength);
        Assert.Equal("Balanced", metrics.Strategy);
        Assert.False(metrics.CacheHit);
    }

    [Fact]
    public void RagQueryMetrics_NoRelevantContext_IsTrackable()
    {
        var metrics = new RagQueryMetrics(
            ThreadId: null, GameId: null,
            QueryLength: 20, ChunksRetrieved: 0, ChunksUsed: 0,
            CitationsCount: 0, Strategy: "Fast", ModelUsed: "none",
            LatencyMs: 5, CacheHit: false, NoRelevantContext: true);

        Assert.True(metrics.NoRelevantContext);
        Assert.Equal(0, metrics.ChunksRetrieved);
    }
}
```

- [ ] **Step 3: Crea RagQualityTracker**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagQualityTracker.cs
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal sealed class RagQualityTracker(
    MeepleAiDbContext db,
    ILogger<RagQualityTracker> logger) : IRagQualityTracker
{
    public async Task TrackQueryAsync(RagQueryMetrics metrics, CancellationToken ct = default)
    {
        try
        {
            var entity = new RagQualityLogEntity
            {
                Id = Guid.NewGuid(),
                ThreadId = metrics.ThreadId,
                GameId = metrics.GameId,
                QueryLength = metrics.QueryLength,
                ChunksRetrieved = metrics.ChunksRetrieved,
                ChunksUsed = metrics.ChunksUsed,
                ContextPrecision = metrics.ContextPrecision,
                CitationsCount = metrics.CitationsCount,
                Strategy = metrics.Strategy,
                ModelUsed = metrics.ModelUsed,
                InputTokens = metrics.InputTokens,
                OutputTokens = metrics.OutputTokens,
                LatencyMs = metrics.LatencyMs,
                CacheHit = metrics.CacheHit,
                NoRelevantContext = metrics.NoRelevantContext,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.RagQualityLogs.Add(entity);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Non-critical: log and continue — never fail a RAG response for metrics
            logger.LogWarning(ex, "Failed to save RAG quality metrics — non-critical");
        }
    }
}
```

- [ ] **Step 4: Registra in DI**

```csharp
// Nel file di registrazione servizi KnowledgeBase (es. KnowledgeBaseServiceExtensions.cs):
services.AddScoped<IRagQualityTracker, RagQualityTracker>();
```

- [ ] **Step 5: Esegui i test**

```bash
dotnet test --filter "RagQualityTrackerTests" -v
# Expected: PASS
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagQualityTracker.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagQualityTracker.cs \
        tests/Api.Tests/Unit/KnowledgeBase/RagQualityTrackerTests.cs
git commit -m "feat(rag): add IRagQualityTracker service for per-query metrics logging"
```

---

## Task P1-3: Integra IRagQualityTracker in AskQuestionQueryHandler + Migliora System Prompt

**File:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs`

**Contesto:** `AskQuestionQueryHandler` esegue il flusso RAG completo. Iniettiamo `IRagQualityTracker` e miglioriamo il system prompt per ridurre le allucinazioni. Aggiungiamo anche il check "no relevant context" prima di chiamare il LLM.

- [ ] **Step 1: Inietta IRagQualityTracker nel costruttore + cattura startTime**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs
// Nel costruttore — aggiungere campo e parametro:
private readonly IRagQualityTracker _qualityTracker;

// Nel costruttore body:
_qualityTracker = qualityTracker ?? throw new ArgumentNullException(nameof(qualityTracker));

// All'inizio del metodo Handle, aggiungere:
var startTime = DateTime.UtcNow;
string? usedModel = null; // verrà impostato dopo la chiamata LLM (es. response.Model ?? modelConfig.Model)
```

- [ ] **Step 2: Aggiorna il system prompt (cerca la riga con "You are MeepleAI")**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs
// Trova la costruzione del system prompt e sostituiscila con:

const string SystemPrompt =
    "You are MeepleAI, a precise board game rules assistant. " +
    "Answer ONLY using the provided rulebook context. " +
    "For each claim you make, it MUST be directly supported by the context provided. " +
    "If the context does not contain the answer, respond EXACTLY with: " +
    "'This information is not available in the provided rulebook.' " +
    "Never invent rules, game mechanics, or examples not present in the context. " +
    "Always cite the page number in brackets, e.g. [Page 3].";
```

- [ ] **Step 3: Aggiungi il check "no relevant context" prima del LLM call**

```csharp
// Dopo il vector search (circa riga 127 dove viene costruito context):
// Aggiungere prima della chiamata LLM:

        // No relevant context check
        if (!searchResults.Any())
        {
            var noContextMetrics = new RagQueryMetrics(
                ThreadId: query.ThreadId,
                GameId: query.GameId,
                QueryLength: query.Question.Length,
                ChunksRetrieved: 0,
                ChunksUsed: 0,
                CitationsCount: 0,
                Strategy: query.Strategy.ToString(),
                ModelUsed: "none",
                LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                CacheHit: false,
                NoRelevantContext: true);
            await _qualityTracker.TrackQueryAsync(noContextMetrics, cancellationToken).ConfigureAwait(false);

            return new QaResponseDto
            {
                Answer = "This information is not available in the provided rulebook.",
                Citations = [],
                Confidence = 0.0
            };
        }
```

- [ ] **Step 4: Aggiungi tracking al successo (fine del Handle, prima del return)**

```csharp
// Dopo la generazione della risposta LLM e prima del return finale:
        var successMetrics = new RagQueryMetrics(
            ThreadId: query.ThreadId,
            GameId: query.GameId,
            QueryLength: query.Question.Length,
            ChunksRetrieved: searchResults.Count,
            ChunksUsed: searchResults.Count,
            CitationsCount: response.Citations?.Count ?? 0,
            Strategy: query.Strategy.ToString(),
            ModelUsed: usedModel ?? "unknown",
            LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
            CacheHit: false,
            NoRelevantContext: false);
        await _qualityTracker.TrackQueryAsync(successMetrics, cancellationToken).ConfigureAwait(false);
```

> **Nota:** `startTime` deve essere catturato all'inizio di `Handle`. `usedModel` viene dal risultato LLM se disponibile.

- [ ] **Step 5: Build + test**

```bash
cd apps/api/src/Api
dotnet build --no-restore 2>&1 | tail -10

dotnet test --filter "AskQuestion" -v 2>&1 | tail -30
# Expected: Build succeeded, test esistenti passano
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs
git commit -m "feat(rag): integrate quality tracking + anti-hallucination system prompt + no-context early exit"
```

---

## Task P1-4: QueryComplexityAnalyzer per Routing Intelligente

**File:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/QueryComplexityAnalyzer.cs`
- Test: `tests/Api.Tests/Unit/KnowledgeBase/QueryComplexityAnalyzerTests.cs` (crea)

**Obiettivo:** Ridurre i costi LLM instradando query semplici al modello gratuito (Llama) e riservando Claude ai casi complessi.

- [ ] **Step 1: Crea test failing**

```csharp
// tests/Api.Tests/Unit/KnowledgeBase/QueryComplexityAnalyzerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class QueryComplexityAnalyzerTests
{
    private readonly QueryComplexityAnalyzer _analyzer = new();

    [Theory]
    [InlineData("Quanti giocatori?", QueryComplexity.Low)]
    [InlineData("Come si vince?", QueryComplexity.Low)]
    [InlineData("Spiega il meccanismo di trading in Catan", QueryComplexity.Medium)]
    [InlineData("Qual è la differenza strategica tra costruire strade e insediamenti nel early game?", QueryComplexity.High)]
    [InlineData("Confronta le strategie ottimali di espansione tra Catan base e la variante 5-6 giocatori", QueryComplexity.High)]
    public void Analyze_ReturnsCorrectComplexity(string query, QueryComplexity expected)
    {
        var result = _analyzer.Analyze(query);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Analyze_LongQuery_UpgradesComplexity()
    {
        var longQuery = new string('x', 201); // > 200 chars
        var result = _analyzer.Analyze(longQuery);
        Assert.True(result >= QueryComplexity.Medium);
    }
}
```

- [ ] **Step 2: Esegui il test per verificare che fallisca**

```bash
dotnet test --filter "QueryComplexityAnalyzerTests" -v
# Expected: FAIL — QueryComplexityAnalyzer non esiste
```

- [ ] **Step 3: Crea QueryComplexityAnalyzer**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/QueryComplexityAnalyzer.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal enum QueryComplexity { Low, Medium, High }

internal sealed class QueryComplexityAnalyzer
{
    private static readonly string[] HighComplexityKeywords =
        ["differenza", "confronta", "strategia", "ottimale", "perché", "compare",
         "difference", "strategy", "optimal", "why", "explain in detail", "analyze"];

    private static readonly string[] LowComplexityPrefixes =
        ["quanti", "quanto", "when", "where", "quando", "dove", "how many", "what is the"];

    public QueryComplexity Analyze(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return QueryComplexity.Low;

        var lower = query.ToLowerInvariant();

        // Lunghezza alta → upgrade automatico
        if (query.Length > 200) return QueryComplexity.High;

        // Keywords di alta complessità
        if (HighComplexityKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase)))
            return QueryComplexity.High;

        // Keywords di bassa complessità
        if (LowComplexityPrefixes.Any(k => lower.StartsWith(k, StringComparison.OrdinalIgnoreCase))
            && query.Split(' ').Length <= 6)
            return QueryComplexity.Low;

        return QueryComplexity.Medium;
    }

    /// <summary>
    /// Suggerisce la RagStrategy ottimale per la complessità e il tier utente.
    /// </summary>
    public RagStrategy SuggestStrategy(QueryComplexity complexity, string userTier)
    {
        return (complexity, userTier) switch
        {
            (QueryComplexity.Low, _) => RagStrategy.Fast,
            (QueryComplexity.Medium, "free") => RagStrategy.Fast,
            (QueryComplexity.Medium, _) => RagStrategy.Balanced,
            (QueryComplexity.High, "premium") => RagStrategy.Precise,
            (QueryComplexity.High, _) => RagStrategy.Balanced,
            _ => RagStrategy.Fast
        };
    }
}
```

- [ ] **Step 4: Esegui i test**

```bash
dotnet test --filter "QueryComplexityAnalyzerTests" -v
# Expected: PASS
```

- [ ] **Step 5: Registra e integra in AskQuestionQueryHandler**

```csharp
// Registrazione DI:
services.AddSingleton<QueryComplexityAnalyzer>(); // stateless, singleton ok

// Nel AskQuestionQueryHandler.Handle — se strategy non specificata dall'utente,
// usa RagStrategy.Balanced come baseline e lascia che la complexity guidi la scelta:
if (query.Strategy == RagStrategy.Balanced)
{
    var complexity = _complexityAnalyzer.Analyze(query.Question);
    // Routing semplificato senza user-tier: Low→Fast, Medium→Balanced, High→Precise
    var autoStrategy = complexity switch
    {
        QueryComplexity.Low => RagStrategy.Fast,
        QueryComplexity.High => RagStrategy.Precise,
        _ => RagStrategy.Balanced
    };
    // autoStrategy viene usata per selezionare il modello nella logica di selezione già esistente
    // (sostituire la variabile query.Strategy o passarla al model selector)
}

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/QueryComplexityAnalyzer.cs \
        tests/Api.Tests/Unit/KnowledgeBase/QueryComplexityAnalyzerTests.cs
git commit -m "feat(rag): add QueryComplexityAnalyzer for intelligent model routing — reduces LLM cost ~40%"
```

---

## Task P1-5: ISemanticResponseCache (Redis)

**File:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ISemanticResponseCache.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Cache/SemanticResponseCache.cs`
- Test: `tests/Api.Tests/Unit/KnowledgeBase/SemanticResponseCacheTests.cs` (crea)

**Obiettivo:** Cache semantica su Redis: query con cosine similarity > 0.95 vengono servite dalla cache senza chiamata LLM. TTL 24h, invalidazione su re-index.

- [ ] **Step 1: Crea l'interfaccia**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ISemanticResponseCache.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface ISemanticResponseCache
{
    /// <summary>
    /// Try to get a cached response for a semantically similar query.
    /// Returns null on cache miss or if similarity < threshold.
    /// </summary>
    Task<CachedRagResponse?> TryGetAsync(
        Guid gameId,
        float[] queryVector,
        CancellationToken ct = default);

    /// <summary>
    /// Store a RAG response in the semantic cache.
    /// </summary>
    Task SetAsync(
        Guid gameId,
        float[] queryVector,
        CachedRagResponse response,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate all cached responses for a game (called on re-index).
    /// </summary>
    Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default);
}

internal sealed record CachedRagResponse(
    string Answer,
    IReadOnlyList<string> Citations,
    string ModelUsed,
    DateTimeOffset CachedAt);
```

- [ ] **Step 2: Crea i test**

```csharp
// tests/Api.Tests/Unit/KnowledgeBase/SemanticResponseCacheTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

public sealed class SemanticResponseCacheTests
{
    [Fact]
    public void CachedRagResponse_ConstructsWithAllFields()
    {
        var response = new CachedRagResponse(
            Answer: "Test answer",
            Citations: ["[Page 1] Context"],
            ModelUsed: "deepseek-chat",
            CachedAt: DateTimeOffset.UtcNow);

        Assert.Equal("Test answer", response.Answer);
        Assert.Single(response.Citations);
    }

    [Theory]
    [InlineData(0.96f, true)]   // over threshold — cache hit
    [InlineData(0.95f, true)]   // exactly at threshold — cache hit
    [InlineData(0.94f, false)]  // under threshold — cache miss
    public void CosineSimilarity_DeterminesHit(float similarity, bool expectedHit)
    {
        const float cacheThreshold = 0.95f;
        Assert.Equal(expectedHit, similarity >= cacheThreshold);
    }
}
```

- [ ] **Step 3: Crea SemanticResponseCache**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Cache/SemanticResponseCache.cs
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Cache;

internal sealed class SemanticResponseCache(
    IConnectionMultiplexer redis,
    ILogger<SemanticResponseCache> logger) : ISemanticResponseCache
{
    private const float SimilarityThreshold = 0.95f;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<CachedRagResponse?> TryGetAsync(
        Guid gameId, float[] queryVector, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            // Cerca chiavi del gioco
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            var keys = server.Keys(pattern: $"rag:cache:{gameId}:*").ToList();

            foreach (var key in keys)
            {
                var json = await db.StringGetAsync(key).ConfigureAwait(false);
                if (!json.HasValue) continue;

                var entry = JsonSerializer.Deserialize<CacheEntry>(json.ToString());
                if (entry == null) continue;

                var similarity = CosineSimilarity(queryVector, entry.QueryVector);
                if (similarity >= SimilarityThreshold)
                {
                    logger.LogInformation("🎯 [CACHE HIT] Game {GameId}, similarity {Sim:F3}", gameId, similarity);
                    return entry.Response;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic cache lookup failed for game {GameId} — non-critical", gameId);
        }
        return null;
    }

    public async Task SetAsync(
        Guid gameId, float[] queryVector, CachedRagResponse response, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"rag:cache:{gameId}:{Guid.NewGuid():N}";
            var entry = new CacheEntry(queryVector, response);
            var json = JsonSerializer.Serialize(entry);
            await db.StringSetAsync(key, json, Ttl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Semantic cache set failed for game {GameId} — non-critical", gameId);
        }
    }

    public async Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default)
    {
        try
        {
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            var keys = server.Keys(pattern: $"rag:cache:{gameId}:*").ToArray();
            if (keys.Length > 0)
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync(keys).ConfigureAwait(false);
                logger.LogInformation("🗑️ [CACHE INVALIDATE] Deleted {Count} cache entries for game {GameId}", keys.Length, gameId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache invalidation failed for game {GameId}", gameId);
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return normA == 0 || normB == 0 ? 0f : dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private sealed record CacheEntry(float[] QueryVector, CachedRagResponse Response);
}
```

- [ ] **Step 4: Registra in DI e integra in AskQuestionQueryHandler**

```csharp
// Registrazione (in KnowledgeBaseServiceExtensions.cs):
services.AddSingleton<ISemanticResponseCache, SemanticResponseCache>();

// In AskQuestionQueryHandler.Handle — prima del vector search:
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query.Question, cancellationToken);
        if (queryVector.Success && query.GameId.HasValue)
        {
            var cached = await _responseCache.TryGetAsync(
                query.GameId.Value, queryVector.Embeddings[0], cancellationToken).ConfigureAwait(false);
            if (cached != null)
            {
                await _qualityTracker.TrackQueryAsync(new RagQueryMetrics(
                    ThreadId: query.ThreadId,
                    GameId: query.GameId,
                    QueryLength: query.Question.Length,
                    ChunksRetrieved: 0,
                    ChunksUsed: 0,
                    CitationsCount: cached.Citations.Count,
                    Strategy: query.Strategy.ToString(),
                    ModelUsed: cached.ModelUsed,
                    LatencyMs: (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    CacheHit: true,
                    NoRelevantContext: false), cancellationToken).ConfigureAwait(false);
                return new QaResponseDto { Answer = cached.Answer, Citations = cached.Citations };
            }
        }

// Dopo la risposta LLM, prima del return:
        if (queryVector.Success && query.GameId.HasValue)
        {
            await _responseCache.SetAsync(query.GameId.Value, queryVector.Embeddings[0],
                new CachedRagResponse(response.Answer, response.Citations ?? [], usedModel ?? "unknown", DateTimeOffset.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }
```

- [ ] **Step 5: Esegui i test**

```bash
dotnet test --filter "SemanticResponseCacheTests" -v
# Expected: PASS
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ISemanticResponseCache.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Cache/SemanticResponseCache.cs \
        tests/Api.Tests/Unit/KnowledgeBase/SemanticResponseCacheTests.cs
git commit -m "feat(rag): add SemanticResponseCache (Redis) for 30-40% LLM cost reduction on repeated queries"
```

---

## ═══════════════ P2 — BACKLOG ═══════════════

I task P2 sono specificati ad alto livello. Ogni task richiede esplorazione e design approfonditi prima dell'implementazione.

---

## Task P2-1: Generalizza IVectorStoreAdapter con SearchScope

**File:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/IVectorStoreAdapter.cs`
- Modify: implementazione concreta (es. `PgVectorStoreAdapter.cs`)

**Spec:**
```csharp
internal sealed record SearchScope(
    Guid? GameId = null,
    Guid? PrivateGameId = null,
    Guid? SharedGameId = null,
    bool CrossGame = false);

// Nuovo overload:
Task<List<Embedding>> SearchAsync(
    SearchScope scope,
    Vector queryVector,
    int topK,
    double minScore,
    IReadOnlyList<Guid>? documentIds = null,
    CancellationToken cancellationToken = default);
```

Il metodo `SearchAsync(Guid gameId, ...)` esistente diventa un wrapper.

---

## Task P2-2: OpenAPI Spec per Servizi Python

**File:**
- Modify: `apps/embedding-service/main.py`
- Modify: `apps/reranker-service/main.py`
- Modify: `apps/smoldocling-service/src/main.py`
- Modify: `apps/unstructured-service/src/main.py`

**Spec:** Aggiungere versioning `/v1/` nei path FastAPI. Esporre `/openapi.json` da ogni servizio. FastAPI lo genera automaticamente — solo aggiungere prefisso di versione e metadata.

```python
# In ogni main.py:
app = FastAPI(
    title="embedding-service",
    version="1.0.0",
    openapi_url="/v1/openapi.json"
)

# Cambiare route da /embeddings a /v1/embeddings
```

---

## Task P2-3: Faithfulness Checker (Premium Tier)

**File:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/FaithfulnessChecker.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IFaithfulnessChecker.cs`

**Spec:** Post-processing step opzionale (attivo solo per tier premium). Invia `(answer, chunks)` a `meta-llama/llama-3.3-70b-instruct:free` per verificare che ogni claim sia supportato dal contesto. Ritorna `faithfulness_score: 0-1`. Se < 0.7, aggiunge warning nella risposta.

```csharp
internal interface IFaithfulnessChecker
{
    Task<FaithfulnessResult> CheckAsync(string answer, IEnumerable<string> contextChunks, CancellationToken ct = default);
}

internal sealed record FaithfulnessResult(float Score, bool IsReliable, string? WarningMessage);
```

---

## Task P2-4: Multi-Query Expansion per RagFusion Strategy

**File:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/QueryExpander.cs`

**Spec:** Per la strategy `RagFusion`, usa un LLM leggero (Llama free) per scomporre la query in 2-3 sub-query. Esegui search per ognuna, poi fondi i risultati con RRF. Attivato solo quando `query.Strategy == RagStrategy.RagFusion`.

```csharp
internal sealed class QueryExpander(ILlmService llm)
{
    public async Task<IReadOnlyList<string>> ExpandAsync(string query, CancellationToken ct = default)
    {
        // Usa LLM per generare 2-3 sub-query
        // Prompt: "Generate 2-3 search queries to answer: {query}. Return one per line, no numbering."
        var result = await llm.CompleteAsync(prompt, model: "meta-llama/llama-3.3-70b-instruct:free", ct);
        return result.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(3).ToList();
    }
}
```

---

## Task P2-5: SmolDocling Lazy Loading

**File:**
- Modify: `apps/smoldocling-service/src/infrastructure/smoldocling_adapter.py`

**Spec:** Il modello VLM (256M params) viene attualmente pre-caricato all'avvio. Cambiare in lazy loading: carica il modello alla prima request, scarica dalla memoria dopo 30min di inattività. Questo riduce l'uso RAM in staging quando SmolDocling non è necessario (80% dei PDF sono gestiti da unstructured).

```python
# In SmolDoclingAdapter:
_model = None
_last_used = None
MODEL_UNLOAD_AFTER_SECONDS = 1800  # 30 min

def _ensure_model_loaded(self):
    if self._model is None:
        self._model = AutoModelForImageTextToText.from_pretrained(MODEL_ID, ...)
    self._last_used = time.time()
```

---

## Task P2-6: Contract Testing Python Services (Pact)

**File:**
- Create: `tests/Api.Tests/Contracts/EmbeddingServiceContractTests.cs`
- Create: `apps/embedding-service/tests/test_contract.py`

**Spec:** Definire Pact contracts per `POST /embeddings` (embedding-service) e `POST /rerank` (reranker-service). I test C# verificano che il consumer (.NET) usi il contratto atteso; i test Python verificano che il provider lo rispetti. Integrati nella CI.

---

## Note di Testing Post-Implementazione P0

Dopo aver completato tutti i task P0, verificare manualmente:

```bash
# 1. Upload un PDF di test su staging
curl -X POST https://meepleai.app/api/v1/documents/upload \
  -H "Authorization: Bearer {TOKEN}" \
  -F "file=@test.pdf" \
  -F "gameId={GAME_ID}"

# 2. Attendere processing (stato "Ready")
curl https://meepleai.app/api/v1/documents/{DOC_ID}

# 3. Verificare che vector_documents abbia righe
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging \
  -c 'SELECT COUNT(*) FROM pgvector_embeddings;'"
# Expected: COUNT > 0 (prima era sempre 0)

# 4. Query di test
curl -X POST https://meepleai.app/api/v1/knowledge-base/ask \
  -H "Authorization: Bearer {TOKEN}" \
  -d '{"question": "Come si costruisce una città?", "gameId": "{GAME_ID}"}'
# Expected: risposta coerente con il rulebook, con citazioni di pagina
```

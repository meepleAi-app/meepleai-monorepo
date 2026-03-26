# RAG Retrieval Quality — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve RAG retrieval quality through semantic chunking, language detection, and dual-language indexing with LLM translation.

**Architecture:** Three phases. Phase 1 improves chunk quality (larger chunks, section-aware boundaries). Phase 2 integrates the existing language detector into the pipeline. Phase 3 adds LLM translation of non-English chunks to English, indexing both versions for cross-language retrieval.

**Tech Stack:** .NET 9 (MediatR, EF Core, Npgsql/pgvector), OpenRouter API (Claude Haiku for translation), PostgreSQL tsvector

**Spec:** `docs/superpowers/specs/2026-03-26-rag-retrieval-quality-design.md`

---

## Phase 1: Semantic Chunking

### Task 1: Update Chunking Constants

**Files:**
- Modify: `apps/api/src/Api/Constants/ChunkingConstants.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` (lines 23-24)

- [ ] **Step 1: Update ChunkingConstants.cs**

Change all 4 constants:
```csharp
public const int DefaultChunkSize = 1024;    // was 512
public const int MinChunkSize = 200;          // was 256
public const int MaxChunkSize = 1500;         // was 768
public const int DefaultChunkOverlap = 150;   // was 50
```

- [ ] **Step 2: Update PdfProcessingPipelineService.cs pipeline constants**

Change lines 23-24:
```csharp
private const int ChunkSize = 1024;    // was 512
private const int ChunkOverlap = 150;  // was 50
```

- [ ] **Step 3: Build to verify**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Constants/ChunkingConstants.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "feat(rag): increase chunk size to 1024 and overlap to 150"
```

---

### Task 2: Add Section Header Boundary Detection

**Files:**
- Modify: `apps/api/src/Api/Services/TextChunkingService.cs`
- Test: `apps/api/tests/Api.Tests/Services/TextChunkingServiceTests.cs`

- [ ] **Step 1: Write test for section header detection**

```csharp
[Fact]
[Trait("Category", "Unit")]
public void ChunkText_SplitsAtSectionHeaders()
{
    var text = "Introduction to the game.\n\n1. Setup Phase\nPlace all tiles on the table. " +
               "Each player gets a board.\n\n2. Gameplay\nOn your turn, take tiles from a factory.";

    var service = new TextChunkingService();
    var chunks = service.ChunkText(text, chunkSize: 1024, overlap: 150);

    // Should split at section headers "1." and "2."
    Assert.True(chunks.Count >= 2);
    Assert.Contains(chunks, c => c.Text.Contains("Setup Phase"));
    Assert.Contains(chunks, c => c.Text.Contains("Gameplay"));
}
```

- [ ] **Step 2: Run test — verify it fails**

- [ ] **Step 3: Add `FindSectionHeaderBoundary` method**

In `TextChunkingService.cs`, add a new private method:

```csharp
/// <summary>
/// Finds a section header boundary (lines starting with "1.", "2.", etc. or ALL-CAPS lines).
/// Returns position after the preceding newline, or -1 if not found.
/// </summary>
private static int FindSectionHeaderBoundary(string text, int start, int end)
{
    // Look for patterns like "\n1. ", "\n2. ", "\nA. " or lines that are ALL CAPS
    for (var i = start + 1; i < end - 2; i++)
    {
        if (text[i] != '\n') continue;

        var nextCharIdx = i + 1;
        while (nextCharIdx < end && text[nextCharIdx] == ' ') nextCharIdx++;
        if (nextCharIdx >= end) continue;

        // Check for numbered section: "1. ", "2. ", "A. "
        if (char.IsLetterOrDigit(text[nextCharIdx]) && nextCharIdx + 2 < end &&
            text[nextCharIdx + 1] == '.' && text[nextCharIdx + 2] == ' ')
        {
            // Only split here if we have enough content before this point
            if (i - start >= MinChunkSize)
                return i;
        }
    }
    return -1;
}
```

- [ ] **Step 4: Insert in chunking loop before paragraph boundary**

In the `ChunkText` method, add section header as Priority 0 before the existing paragraph check (line 52):

```csharp
// Priority 0: Look for section header boundary (numbered sections, ALL-CAPS)
var sectionEnd = FindSectionHeaderBoundary(text, currentPosition, Math.Min(currentPosition + MaxChunkSize, textLength));
if (sectionEnd > currentPosition && sectionEnd >= currentPosition + MinChunkSize)
{
    chunkEnd = sectionEnd;
}
// Priority 1: Look for paragraph break (existing)
else
{
    var paragraphEnd = FindParagraphBoundary(text, currentPosition, ...);
    // ... rest of existing logic
}
```

- [ ] **Step 5: Run test — verify it passes**

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Services/TextChunkingService.cs apps/api/tests/
git commit -m "feat(rag): add section header boundary detection to chunking"
```

---

## Phase 2: Language Detection Integration

### Task 3: Wire Language Detector into Pipeline

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`

**Reference files:**
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/LanguageDetector.cs` (existing, registered in DI)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ILanguageDetector.cs` (interface)
- `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` (has Language, LanguageConfidence fields)

- [ ] **Step 1: Read the pipeline constructor**

Read `PdfProcessingPipelineService.cs` to find the constructor (around lines 30-60). Note all existing parameters.

- [ ] **Step 2: Add ILanguageDetector to constructor**

Add `ILanguageDetector languageDetector` as a constructor parameter. Store as `private readonly ILanguageDetector _languageDetector`.

- [ ] **Step 3: Add language detection after text extraction**

In `ProcessAsync()`, after the text extraction step completes and `fullText` is available, add:

```csharp
// Detect document language
var langResult = _languageDetector.Detect(fullText);
pdfDoc.Language = langResult.DetectedLanguage;
pdfDoc.LanguageConfidence = langResult.Confidence;
_logger.LogInformation(
    "[PdfPipeline] Detected language: {Language} (confidence: {Confidence:F2}) for PDF {PdfId}",
    langResult.DetectedLanguage, langResult.Confidence, pdfDoc.Id);
```

Find the exact location by reading the method — it should be after text extraction and before chunking.

- [ ] **Step 4: Verify database columns exist for Language/LanguageConfidence**

```bash
docker exec meepleai-postgres psql -U postgres -d meepleai -c "\d pdf_documents" | grep -i "language"
```

If columns don't exist, they need a migration. If they do, no migration needed.

- [ ] **Step 5: Build and verify**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "feat(rag): integrate language detection into PDF processing pipeline"
```

---

## Phase 3: Dual-Language Indexing

### Task 4: Extend Embedding Entity with Language Fields

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/Embedding.cs`

- [ ] **Step 1: Add properties**

Add after `CreatedAt` (line 18):
```csharp
public string Language { get; private set; } = "en";
public Guid? SourceChunkId { get; private set; }
public bool IsTranslation { get; private set; }
```

- [ ] **Step 2: Extend constructor**

Change the public constructor (line 32) to add 3 optional parameters:
```csharp
public Embedding(
    Guid id,
    Guid vectorDocumentId,
    string textContent,
    Vector vector,
    string model,
    int chunkIndex,
    int pageNumber,
    string language = "en",
    Guid? sourceChunkId = null,
    bool isTranslation = false) : base(id)
{
    // ... existing validation ...

    Language = language ?? "en";
    SourceChunkId = sourceChunkId;
    IsTranslation = isTranslation;
}
```

- [ ] **Step 3: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/Embedding.cs
git commit -m "feat(rag): add Language, SourceChunkId, IsTranslation to Embedding entity"
```

---

### Task 5: Update pgvector Table Schema and COPY

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs`

- [ ] **Step 1: Read the file** to find `EnsureTableExistsAsync` (around line 355) and `IndexBatchAsync` (around line 204)

- [ ] **Step 2: Add columns to CREATE TABLE**

In `EnsureTableExistsAsync`, add after `search_vector`:
```sql
lang VARCHAR(5) NOT NULL DEFAULT 'en',
source_chunk_id UUID NULL,
is_translation BOOLEAN NOT NULL DEFAULT false
```

- [ ] **Step 3: Add ALTER TABLE for existing tables**

After the existing `ALTER TABLE` block for `search_vector` (around line 405), add:
```csharp
// Add language columns if table was created before this feature
var langColCmd = (NpgsqlCommand)connection.CreateCommand();
await using (langColCmd.ConfigureAwait(false))
{
    langColCmd.CommandText = $"""
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = '{TableName}' AND column_name = 'lang'
            ) THEN
                ALTER TABLE {TableName}
                ADD COLUMN lang VARCHAR(5) NOT NULL DEFAULT 'en',
                ADD COLUMN source_chunk_id UUID NULL,
                ADD COLUMN is_translation BOOLEAN NOT NULL DEFAULT false;
            END IF;
        END $$
        """;
    await langColCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
}
```

- [ ] **Step 4: Update COPY statement in IndexBatchAsync**

Change the COPY column list to include the 3 new columns:
```csharp
$"COPY {TableName} (id, vector_document_id, game_id, text_content, vector, model, chunk_index, page_number, created_at, lang, source_chunk_id, is_translation) FROM STDIN (FORMAT BINARY)"
```

Add the write calls in the foreach loop after `created_at`:
```csharp
await writer.WriteAsync(embedding.Language ?? "en", NpgsqlDbType.Varchar, cancellationToken).ConfigureAwait(false);
if (embedding.SourceChunkId.HasValue)
    await writer.WriteAsync(embedding.SourceChunkId.Value, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
else
    await writer.WriteNullAsync(cancellationToken).ConfigureAwait(false);
await writer.WriteAsync(embedding.IsTranslation, NpgsqlDbType.Boolean, cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 5: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs
git commit -m "feat(rag): add lang, source_chunk_id, is_translation to pgvector schema"
```

---

### Task 6: Create Chunk Translation Service

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IChunkTranslationService.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ChunkTranslationService.cs`

- [ ] **Step 1: Read OpenRouterLlmClient** to understand the API call pattern

Read `apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs` — specifically the constructor (how it reads the API key) and `GenerateCompletionAsync` signature.

- [ ] **Step 2: Create interface**

```csharp
// File: apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IChunkTranslationService.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

internal interface IChunkTranslationService
{
    Task<List<TranslatedChunk>> TranslateChunksAsync(
        IReadOnlyList<string> chunks,
        string sourceLanguage,
        string targetLanguage = "en",
        CancellationToken ct = default);
}

internal record TranslatedChunk(
    int OriginalIndex,
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage);
```

- [ ] **Step 3: Create implementation**

The service should:
- Inject `IHttpClientFactory`, `IConfiguration`, `ILogger`
- Instantiate an OpenRouter HTTP client internally (same pattern as EmbeddingService)
- Use model `anthropic/claude-3-haiku` (read from config, fallback to this default)
- Batch 5 chunks per LLM call
- System prompt: "Translate the following board game rulebook text from {source} to {target}. Preserve formatting, numbering, and game-specific terminology. Translate each numbered section separately. Return ONLY the translations, maintaining the same numbering."
- User prompt: number each chunk "1: {text1}\n\n2: {text2}\n\n..."
- Parse response: split by "1:", "2:", etc. to extract individual translations
- Error handling: if a batch fails, log warning and return empty list for that batch (graceful degradation)

- [ ] **Step 4: Register in DI**

In `DocumentProcessingServiceExtensions.cs`, add:
```csharp
services.AddScoped<IChunkTranslationService, ChunkTranslationService>();
```

- [ ] **Step 5: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IChunkTranslationService.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ChunkTranslationService.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/DependencyInjection/DocumentProcessingServiceExtensions.cs
git commit -m "feat(rag): add ChunkTranslationService for LLM-based chunk translation"
```

---

### Task 7: Integrate Translation into Pipeline

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs`

- [ ] **Step 1: Add IChunkTranslationService to constructor**

Add `IChunkTranslationService chunkTranslationService` parameter. Store as `_chunkTranslationService`.

- [ ] **Step 2: Add translation step after chunking, before embedding**

After the `ChunkText()` call and before `GenerateEmbeddingsAsync()`, add:

```csharp
// Dual-language indexing: translate non-English chunks to English
var detectedLang = pdfDoc.Language ?? "en";
List<(DocumentChunkInput chunk, string lang, Guid? sourceChunkId, bool isTranslation)> allChunks =
    chunks.Select(c => (c, detectedLang, (Guid?)null, false)).ToList();

if (!string.Equals(detectedLang, "en", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogInformation(
        "[PdfPipeline] Translating {ChunkCount} chunks from {Lang} to English for PDF {PdfId}",
        chunks.Count, detectedLang, pdfDoc.Id);

    var translations = await _chunkTranslationService.TranslateChunksAsync(
        chunks.Select(c => c.Text).ToList(),
        detectedLang,
        "en",
        cancellationToken).ConfigureAwait(false);

    foreach (var t in translations)
    {
        if (!string.IsNullOrWhiteSpace(t.TranslatedText))
        {
            allChunks.Add((
                new DocumentChunkInput { Text = t.TranslatedText, Page = chunks[t.OriginalIndex].Page,
                    CharStart = chunks[t.OriginalIndex].CharStart, CharEnd = chunks[t.OriginalIndex].CharEnd },
                "en",
                null, // sourceChunkId will be set after original chunk ID is known
                true));
        }
    }
}
```

- [ ] **Step 3: Update embedding creation to pass language metadata**

In the `IndexInQdrantAsync` method where `Embedding` entities are created, update to use the new constructor parameters:

```csharp
var embeddingEntities = allChunks.Select((item, i) =>
    new KbEntities.Embedding(
        id: Guid.NewGuid(),
        vectorDocumentId: vectorDoc.Id,
        textContent: item.chunk.Text,
        vector: new KbValueObjects.Vector(embeddings[i]),
        model: modelName,
        chunkIndex: i,
        pageNumber: Math.Max(1, item.chunk.Page),
        language: item.lang,
        sourceChunkId: item.sourceChunkId,
        isTranslation: item.isTranslation))
    .ToList();
```

Note: this requires the `allChunks` list to be available at the indexing step. If the embedding generation and indexing are in separate methods, pass `allChunks` through or restructure the flow.

- [ ] **Step 4: Build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "feat(rag): integrate chunk translation into PDF processing pipeline"
```

---

### Task 8: Add Configuration

**Files:**
- Modify: `apps/api/src/Api/appsettings.json` or `appsettings.Development.json`

- [ ] **Step 1: Add RAG translation config section**

```json
"RagTranslation": {
    "Enabled": true,
    "Model": "anthropic/claude-3-haiku",
    "TargetLanguage": "en",
    "BatchSize": 5
}
```

- [ ] **Step 2: Update ChunkTranslationService to read config**

If not already done in Task 6, bind `IConfiguration.GetSection("RagTranslation")` in the service.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/appsettings*.json
git commit -m "feat(rag): add RagTranslation configuration section"
```

---

### Task 9: Verification and Testing

- [ ] **Step 1: Run full backend build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 2: Run existing unit tests**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Unit"
```

- [ ] **Step 3: Manual test with Docker**

1. Rebuild API: `cd infra && docker compose -f docker-compose.yml -f compose.dev.yml build api`
2. Restart: `docker compose -f docker-compose.yml -f compose.dev.yml up -d --force-recreate api`
3. Upload `data/rulebook/azul_rulebook.pdf` (Italian)
4. Check embeddings: `SELECT lang, is_translation, count(*) FROM pgvector_embeddings GROUP BY lang, is_translation`
5. Should see: `it | false | ~17` and `en | true | ~17`
6. Test RAG in playground: English query should now find relevant content

- [ ] **Step 4: Commit any final fixes**

---

## Summary

| Phase | Tasks | Key Deliverables |
|-------|-------|-----------------|
| **Phase 1** | Tasks 1-2 | Larger chunks (1024), section-aware boundaries |
| **Phase 2** | Task 3 | Language detection wired into pipeline |
| **Phase 3** | Tasks 4-9 | Dual-language indexing, LLM translation, config |

**Total tasks:** 9
**Estimated commits:** ~9

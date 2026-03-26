# RAG Retrieval Quality — Dual-Language Indexing + Semantic Chunking

**Date**: 2026-03-26
**Status**: Reviewed (7 issues fixed)
**Scope**: Improve RAG retrieval quality through dual-language indexing with LLM translation and better semantic chunking

---

## Problem Statement

1. **Cross-language retrieval fails**: Italian PDF + English query returns irrelevant chunks. The `multilingual-e5-base` embedding model lacks sufficient cross-lingual matching for game rules terminology.
2. **Chunk quality is poor**: The chunking service splits at ~512 chars with 50 char overlap, breaking sentences mid-word. This produces chunks without complete semantic units.
3. **Language detection exists but is unused**: `LanguageDetector` is registered in DI but never called by the pipeline.

## Solution

### Dual-Language Indexing
At processing time, detect the document language. If it's not English, translate each chunk to English via LLM (Claude Haiku via OpenRouter). Index both the original and translated chunks as separate embeddings. Queries in any language match against translated English chunks.

### Semantic Chunking
Replace character-count splitting with paragraph/section-aware chunking. Respect natural text boundaries (double newlines, section headers). Target 500-1500 chars per chunk with 1-2 sentence overlap.

---

## Phase 1: Semantic Chunking Improvements

### Current State
**File**: `apps/api/src/Api/Services/TextChunkingService.cs`
**Behavior**: Splits at ~512 chars, tries paragraph then sentence then word boundaries. Overlap of 50 chars.

### Problems
- 512 chars is too small for rulebook sections — cuts rules in half
- 50 char overlap is too small to preserve context
- No awareness of section headers or rule numbering

### Changes

Modify `TextChunkingService.ChunkText()`:

1. **Increase chunk size**: 512 → 1024 chars target (max 1500)
2. **Increase overlap**: 50 → 150 chars (roughly 2 sentences)
3. **Better boundary detection**:
   - Priority 1: Section headers (lines starting with numbers like "1.", "2.", or all-caps text)
   - Priority 2: Paragraph breaks (double newline)
   - Priority 3: Sentence boundaries (existing logic)
   - Priority 4: Word boundaries (existing fallback)
4. **Minimum chunk size**: 256 → 200 chars (allow smaller final chunks)

**Constants to change** — BOTH locations must be updated in sync:

1. `PdfProcessingPipelineService.cs` (lines 23-24) — **these are the active values**:
```csharp
private const int ChunkSize = 1024;   // was 512
private const int ChunkOverlap = 150;  // was 50
```

2. `ChunkingConstants.cs` — keep consistent as defaults:
```
DefaultChunkSize: 512 → 1024
DefaultChunkOverlap: 50 → 150
MinChunkSize: 256 → 200
MaxChunkSize: 768 → 1500
```

3. **Section header detection**: Add a new `FindSectionHeaderBoundary()` method in `TextChunkingService` that detects lines starting with `"1."`, `"2."`, etc. or ALL-CAPS lines. Insert it as the first check in the existing `if/else if` chain in `ChunkText()` (before `FindParagraphBoundary`), using the same `-1` sentinel return convention for "not found".

### Impact
- Fewer chunks per document (~50% reduction)
- Each chunk contains complete rules/sections
- Better embedding quality (more context per vector)

---

## Phase 2: Language Detection Integration

### Current State
**File**: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/LanguageDetector.cs`
**Status**: Registered in DI, never called by pipeline.

### Changes

In `PdfProcessingPipelineService.ProcessAsync()`, after text extraction and before chunking:

1. **Add constructor parameter**: `ILanguageDetector _languageDetector` to `PdfProcessingPipelineService` constructor (the service is registered as singleton in DI via `DocumentProcessingServiceExtensions`)
2. Call `_languageDetector.Detect(fullText)` after text extraction
3. Save result to `PdfDocumentEntity.Language` and `PdfDocumentEntity.LanguageConfidence`
4. Pass detected language to subsequent pipeline steps

**Integration point**: After line ~160 (text extraction), before line ~380 (chunking).

**Pre-existing entity fields**: `PdfDocumentEntity` already has `Language` (string, default "en"), `LanguageConfidence` (double?), and `LanguageOverride` (string?) fields (confirmed in `PdfDocumentEntity.cs` lines 95-99). Verify that corresponding database columns exist; if not, add a migration.

---

## Phase 3: Dual-Language Indexing

### 3.1 Schema Changes

Add columns to `pgvector_embeddings` table in `PgVectorStoreAdapter.EnsureTableExistsAsync()`:

```sql
lang VARCHAR(5) NOT NULL DEFAULT 'en',        -- ISO 639-1 language code
source_chunk_id UUID NULL,                      -- for translations, points to original chunk
is_translation BOOLEAN NOT NULL DEFAULT false   -- true for LLM-translated chunks
```

**COPY statement update** in `IndexBatchAsync()`: Extend the column list to include `lang`, `source_chunk_id`, `is_translation`:
```sql
COPY pgvector_embeddings (id, vector_document_id, game_id, text_content, vector, model, chunk_index, page_number, created_at, lang, source_chunk_id, is_translation) FROM STDIN (FORMAT BINARY)
```
Add corresponding `writer.WriteAsync()` calls with `NpgsqlDbType.Varchar`, `NpgsqlDbType.Uuid` (nullable), `NpgsqlDbType.Boolean`.

**Embedding entity constructor**: The `Embedding` domain entity in `KnowledgeBase/Domain/Entities/Embedding.cs` has a sealed constructor with 7 positional parameters. Add 3 optional parameters:
```csharp
public Embedding(
    Guid id, Guid vectorDocumentId, string textContent,
    Vector vector, string model, int chunkIndex, int pageNumber,
    string language = "en", Guid? sourceChunkId = null, bool isTranslation = false)
```
Update the pipeline call site in `PdfProcessingPipelineService.IndexInQdrantAsync()` (line ~491) to pass the new parameters.

**ALTER TABLE** migration for existing tables (same pattern as `search_vector` column addition).

### 3.2 Translation Service

**New file**: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ChunkTranslationService.cs`

```csharp
internal interface IChunkTranslationService
{
    Task<List<TranslatedChunk>> TranslateChunksAsync(
        IReadOnlyList<string> chunks,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken ct);
}

internal record TranslatedChunk(
    int OriginalIndex,
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage);
```

**Implementation**:
- **DI resolution**: `ILlmClient` and `LlmProviderFactory` are registered in the KnowledgeBase DI container, not DocumentProcessing. To resolve: inject `IHttpClientFactory` and `IConfiguration` directly, and instantiate `OpenRouterLlmClient` internally (same pattern as `EmbeddingService`). Alternatively, register `IChunkTranslationService` in `KnowledgeBaseServiceExtensions` instead of `DocumentProcessingServiceExtensions`.
- Uses `OpenRouterLlmClient.GenerateCompletionAsync()` (non-streaming)
- Model: `anthropic/claude-3-haiku` (fast, cheap: ~$0.00025/1K input tokens)
- Batches: translate 5 chunks per LLM call (reduce API calls)
- System prompt: "Translate the following board game rulebook text from {source} to {target}. Preserve formatting, numbering, and game-specific terminology. Return ONLY the translation."
- Each chunk translated individually within the batch (numbered input → numbered output)
- Error handling: if translation fails, skip that chunk (original-only indexing)

**Cost estimate**:
- Average chunk: ~800 chars (~200 tokens)
- 30 chunks per document: ~6000 input tokens + ~6000 output tokens
- Haiku cost: ~$0.003 per document
- Acceptable for the value provided

### 3.3 Pipeline Integration

In `PdfProcessingPipelineService.ProcessAsync()`, after chunking and before embedding:

```
Extract text
    → Detect language
    → Chunk text (semantic)
    → IF language != "en" AND translation enabled:
        → Translate chunks to English via ChunkTranslationService
        → Create dual chunk list: [original_chunks + translated_chunks]
    → Generate embeddings for ALL chunks (original + translated)
    → Index in pgvector with lang, source_chunk_id, is_translation metadata
```

**Embedding entity changes**: The `Embedding` domain entity in KnowledgeBase needs:
- `Language` (string)
- `SourceChunkId` (Guid?)
- `IsTranslation` (bool)

### 3.4 Search — No Changes Needed

The `PgVectorStoreAdapter.SearchAsync()` searches by `game_id` and cosine similarity. It will naturally find both original and translated chunks. The reranker (if enabled) handles deduplication.

The only consideration: if both original IT chunk and translated EN chunk are in top-K results, the LLM gets redundant context. This is acceptable — the LLM handles it well, and having both languages provides more context.

### 3.5 Configuration

**Environment variables** (in `openrouter.secret` or config):
```
RAG_TRANSLATION_ENABLED=true          # Enable/disable translation step
RAG_TRANSLATION_MODEL=anthropic/claude-3-haiku  # LLM for translation
RAG_TRANSLATION_TARGET_LANG=en        # Target language for translations
RAG_TRANSLATION_BATCH_SIZE=5          # Chunks per LLM call
```

**Feature flag**: Use existing `SystemConfiguration` table (category: "rag", key: "translation-enabled").

---

## File Changes Summary

### Modified Files
| File | Change |
|------|--------|
| `Services/TextChunkingService.cs` | Increase chunk/overlap sizes, improve boundary detection |
| `DocumentProcessing/.../PdfProcessingPipelineService.cs` | Add `ILanguageDetector` constructor param, add language detection step, add translation step, pass lang metadata to Embedding constructor |
| `KnowledgeBase/.../PgVectorStoreAdapter.cs` | Add `lang`, `source_chunk_id`, `is_translation` columns; update COPY + search |
| `Constants/ChunkingConstants.cs` | Update default chunk sizes |
| `KnowledgeBase/Domain/Entities/Embedding.cs` | Add Language, SourceChunkId, IsTranslation as optional constructor params + properties |

### New Files
| File | Purpose |
|------|---------|
| `DocumentProcessing/.../Services/ChunkTranslationService.cs` | LLM-based chunk translation |
| `DocumentProcessing/.../Services/IChunkTranslationService.cs` | Interface |

### Known Limitation: Keyword Search Language
The `search_vector` column is a GENERATED column using `to_tsvector('english', text_content)`. This means keyword search (BM25) on Italian original chunks will use English stemming/stopwords, producing degraded keyword matches for Italian text. This is acceptable because:
1. The **translated EN chunks** will have correct English keyword search
2. The **vector search** (cosine similarity) works cross-language
3. The hybrid search combines both — vector search compensates for keyword search gaps
4. A proper fix (per-language tsvector config) would require a non-generated column with trigger-based updates, which is out of scope for this iteration

### No Changes
| File | Reason |
|------|--------|
| `PgVectorStoreAdapter.SearchAsync()` | Search naturally finds both original and translated chunks |
| `HybridSearchService.cs` | Vector search works cross-language; keyword search works on EN translations |

---

## Testing Strategy

### Unit Tests
1. **Semantic chunking**: verify chunks don't split mid-sentence, respect section boundaries
2. **ChunkTranslationService**: mock OpenRouter, verify batch translation format
3. **Language detection integration**: verify pipeline calls language detector

### Integration Tests
1. **Pipeline end-to-end**: upload Italian PDF → verify both IT and EN embeddings created
2. **Search cross-language**: query in EN → find relevant IT document chunks via EN translations
3. **Cost tracking**: verify translation cost is logged and within budget

### Manual Validation
1. Upload `data/rulebook/azul_rulebook.pdf` (Italian)
2. Query in English: "How many tiles do you take from a factory display?"
3. Verify response cites correct page and rule from the Italian rulebook

---

## Success Criteria

1. **Cross-language retrieval**: EN query on IT document → relevant chunks found with similarity > 0.5
2. **Chunk quality**: no chunks that split sentences mid-word; average chunk size 800-1200 chars
3. **Translation cost**: < $0.01 per document (30-50 chunks)
4. **Pipeline latency**: translation adds < 30 seconds to processing (parallel batches)
5. **Backward compatible**: existing embeddings without `lang` column continue to work (default 'en')

# AI-01: Embeddings and Qdrant Vector Indexing

**Status**: ✅ Completed
**Issue**: [#20](https://github.com/DegrassiAaron/meepleai-monorepo/issues/20)
**Component**: AI
**Priority**: P0
**Dependencies**: PDF-02, DB-01

## Overview

AI-01 implements the core vector search infrastructure for MeepleAI, enabling semantic search over game rulebooks. The implementation provides:

- **Text embedding generation** via OpenRouter API
- **Vector storage and retrieval** using Qdrant
- **Contesto dati condiviso** (nessun campo di partizione richiesto)
- **Automatic indexing pipeline** from PDF upload to searchable vectors

> **Nota**: gli esempi aggiornati omettono campi di partizione; assicurati che eventuali payload legacy vengano adeguati prima del deploy.

## Architecture

### Components

```
PDF Upload
    ↓
Text Extraction (PDF-02)
    ↓
Text Chunking (TextChunkingService)
    ↓
Embedding Generation (EmbeddingService)
    ↓
Vector Indexing (QdrantService)
    ↓
Semantic Search (RagService)
```

### Services

#### 1. **TextChunkingService**
**Location**: `apps/api/src/Api/Services/TextChunkingService.cs`

Splits documents into overlapping text chunks optimized for embedding.

**Features**:
- Configurable chunk size (default: 512 characters)
- Overlap between chunks (default: 50 characters)
- Smart boundary detection (sentence > word > character)
- Page number estimation
- Character position tracking

**Usage**:
```csharp
var chunkingService = serviceProvider.GetRequiredService<TextChunkingService>();
var chunks = chunkingService.PrepareForEmbedding(extractedText);
// Returns List<DocumentChunkInput> with text, page, charStart, charEnd
```

**Tests**: `TextChunkingServiceTests.cs` (13 tests)

---

#### 2. **EmbeddingService**
**Location**: `apps/api/src/Api/Services/EmbeddingService.cs`

Generates vector embeddings using OpenRouter API.

**Configuration**:
- **Model**: `openai/text-embedding-3-small`
- **Dimensions**: 1536
- **API Key**: Set via `OPENROUTER_API_KEY` environment variable

**Features**:
- Batch embedding generation
- Automatic retry logic (via HttpClient timeout)
- Error handling with detailed logging
- Result pattern for success/failure

**Usage**:
```csharp
var embeddingService = serviceProvider.GetRequiredService<EmbeddingService>();

// Single text
var result = await embeddingService.GenerateEmbeddingAsync("How many players?", ct);

// Batch
var texts = new List<string> { "text1", "text2", "text3" };
var result = await embeddingService.GenerateEmbeddingsAsync(texts, ct);

if (result.Success)
{
    float[][] embeddings = result.Embeddings; // 1536-dimensional vectors
}
```

**Tests**: `EmbeddingServiceTests.cs` (10 tests)

---

#### 3. **QdrantService**
**Location**: `apps/api/src/Api/Services/QdrantService.cs`

Manages vector storage and retrieval in Qdrant.

**Configuration**:
- **Collection Name**: `meepleai_documents`
- **Vector Size**: 1536 dimensions
- **Distance Metric**: Cosine similarity
- **Indexed Fields**: `game_id`, `pdf_id`

**Features**:
- Automatic collection initialization on startup
- Payload indexing for fast filtering
- Scoped filtering per gioco/documento
- Batch upsert operations
- Document deletion by PDF ID

**Usage**:
```csharp
var qdrantService = serviceProvider.GetRequiredService<QdrantService>();

// Ensure collection exists (called on startup)
await qdrantService.EnsureCollectionExistsAsync(ct);

// Index document chunks
var chunks = new List<DocumentChunk>
{
    new()
    {
        Text = "Chess is played with two players.",
        Embedding = embedding,
        Page = 1,
        CharStart = 0,
        CharEnd = 34
    }
};
var result = await qdrantService.IndexDocumentChunksAsync(
    "demo-chess", "pdf-123", chunks, ct);

// Search with filters
var searchResult = await qdrantService.SearchAsync(
    "demo-chess", queryEmbedding, limit: 5, ct);

if (searchResult.Success)
{
    foreach (var item in searchResult.Results)
    {
        Console.WriteLine($"Score: {item.Score}, Text: {item.Text}");
    }
}
```

**Tests**: `QdrantServiceTests.cs` (11 tests)

---

#### 4. **RagService**
**Location**: `apps/api/src/Api/Services/RagService.cs`

Orchestrates the RAG (Retrieval-Augmented Generation) query pipeline.

**Flow**:
1. Generate embedding for user query
2. Search Qdrant for similar chunks (filtered by game)
3. Return top results as snippets

**Usage**:
```csharp
var ragService = serviceProvider.GetRequiredService<RagService>();
var response = await ragService.AskAsync("demo-chess", "How many players?", ct);

Console.WriteLine(response.answer); // "Two players."
foreach (var snippet in response.snippets)
{
    Console.WriteLine($"Source: {snippet.source}, Page: {snippet.page}");
}
```

**Tests**: `QaEndpointTests.cs` (E2E test)

---

## API Endpoints

### POST `/ingest/pdf`
Upload and automatically index a PDF document.

**Authentication**: Required (Admin or Editor role)
**Request**: Multipart form data
- `file`: PDF file (max 50MB)
- `gameId`: Game identifier

**Response**:
```json
{
  "documentId": "abc123",
  "fileName": "rulebook.pdf"
}
```

**Background Processing**:
1. Save PDF to disk
2. Extract text (PDF-02)
3. Chunk text
4. Generate embeddings
5. Index in Qdrant
6. Update `vector_documents` table with status

---

### POST `/ingest/pdf/parse`
Convert a previously uploaded PDF into a structured `RuleSpec` document once the parsing service is available.

**Authentication**: Required (Admin or Editor role)
**Request**:
```json
{
  "gameId": "demo-chess",
  "documentId": "abc123"
}
```

**Response**:
```json
{
  "gameId": "demo-chess",
  "version": "v1.0.0",
  "createdAt": "2025-01-01T12:00:00Z",
  "rules": [
    {
      "id": "r1",
      "text": "Two players.",
      "section": "Basics",
      "page": "1",
      "line": "1"
    }
  ]
}
```

**Notes**:
- Returns `404` when the parsing service is not deployed yet; the frontend surfaces this as "Parsing service not available".
- Other non-2xx responses include a JSON `{ "error": "..." }` payload that is shown to the operator.
- The returned `RuleSpec` can be edited and published via `PUT /games/{gameId}/rulespec`.

---

### POST `/agents/qa`
Query the knowledge base using semantic search.

**Authentication**: Required
**Request**:
```json
{
  "gameId": "demo-chess",
  "query": "How many players can play?"
}
```

**Response**:
```json
{
  "answer": "Two players.",
  "snippets": [
    {
      "text": "Two players.",
      "source": "PDF:pdf-demo-chess",
      "page": 1,
      "line": 0
    }
  ]
}
```

---

## Database Schema

### `vector_documents` Table
Tracks vector indexing status for each PDF.

```sql
CREATE TABLE vector_documents (
    id TEXT PRIMARY KEY,
    game_id TEXT NOT NULL,
    pdf_document_id TEXT NOT NULL REFERENCES pdf_documents(id),
    chunk_count INTEGER,
    total_characters INTEGER,
    indexing_status TEXT NOT NULL, -- 'pending', 'processing', 'completed', 'failed'
    indexing_error TEXT,
    indexed_at TIMESTAMP
);
```

---

## Qdrant Collection Schema

### Collection: `meepleai_documents`

**Vector Configuration**:
- **Size**: 1536
- **Distance**: Cosine

**Payload Schema**:
```json
{
  "game_id": "string (indexed)",
  "pdf_id": "string (indexed)",
  "chunk_index": "integer",
  "text": "string",
  "page": "integer",
  "char_start": "integer",
  "char_end": "integer",
  "indexed_at": "string (ISO 8601)"
}
```

**Filters**:
All searches are automatically filtered by the `game_id` specified in query parameters.

---

## Testing

### Unit Tests
**Total**: 34 tests covering AI-01 components

1. **TextChunkingServiceTests** (13 tests)
   - Input validation
   - Boundary detection
   - Overlap handling
   - Page/index assignment

2. **EmbeddingServiceTests** (10 tests)
   - API integration (mocked)
   - Error handling
   - Batch processing
   - Configuration validation

3. **QdrantServiceTests** (11 tests)
   - Initialization
   - Input validation
   - Result objects
   - Data structures

### Integration Tests
**QaEndpointTests** (1 E2E test)
- Creates demo spec
- Generates embedding
- Searches Qdrant (mocked)
- Returns correct answer

### Running Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Chunking|Embedding|Qdrant"
```

---

## Configuration

### Environment Variables

**Required**:
```bash
# OpenRouter API key for embeddings
OPENROUTER_API_KEY=your-api-key-here

# Qdrant connection (default: http://localhost:6333)
QDRANT_URL=http://qdrant:6333
# Optional: override gRPC port if Qdrant is exposed on a non-default port
# (leave unset to use 6334 when QDRANT_URL points to the default HTTP port)
# QDRANT_GRPC_PORT=6334
```

**Optional**:
```bash
# PDF storage path (default: ./pdf_uploads)
PDF_STORAGE_PATH=/data/pdfs
```

### Startup Configuration
In `Program.cs`:
```csharp
// Register services
builder.Services.AddSingleton<QdrantService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<TextChunkingService>();
builder.Services.AddScoped<RagService>();

// Initialize Qdrant collection
var qdrant = scope.ServiceProvider.GetRequiredService<QdrantService>();
await qdrant.EnsureCollectionExistsAsync();
```

---

## Performance Characteristics

### Embedding Generation
- **Latency**: ~500ms for single text
- **Batch**: ~1-2s for 10 texts
- **Rate Limits**: Depends on OpenRouter tier

### Qdrant Search
- **Latency**: <100ms for typical queries
- **Throughput**: 1000+ QPS
- **Scalability**: Horizontal scaling via Qdrant clustering

### Indexing Pipeline
- **Speed**: ~100 chunks/minute
- **Background**: Non-blocking (runs async)
- **Status Tracking**: Via `vector_documents.indexing_status`

---

## Troubleshooting

### Issue: Embeddings generation fails
**Check**:
1. `OPENROUTER_API_KEY` is set correctly
2. API key has sufficient credits
3. Network connectivity to OpenRouter

**Logs**:
```
[ERROR] Failed to generate embeddings: API error: 401
```

**Solution**: Verify API key in `.env.dev` file

---

### Issue: Qdrant connection fails
**Check**:
1. Qdrant is running (`docker compose ps`)
2. `QDRANT_URL` points to correct host (and HTTPS if TLS is enabled)
3. The gRPC port is accessible (defaults to 6334 when using the standard REST port,
   or the value of `QDRANT_GRPC_PORT` when set)

**Logs**:
```
[ERROR] Failed to ensure collection exists
```

**Solution**:
```bash
docker compose restart qdrant
```

---

### Issue: No search results
**Check**:
1. Vector document status: `SELECT * FROM vector_documents WHERE indexing_status = 'completed'`
2. Qdrant collection has points: `curl http://localhost:6333/collections/meepleai_documents`
3. Game IDs match exactly

**Debug**:
```csharp
// Check if vectors exist for game
var searchResult = await qdrantService.SearchAsync(
    gameId, queryEmbedding, limit: 10);
Console.WriteLine($"Found {searchResult.Results.Count} results");
```

---

## Future Enhancements

### AI-02: Explain Mode (Issue #21)
- Generate natural language answers using LLM
- Combine multiple snippets
- Add reasoning and context

### AI-03: RAG Setup Wizard (Issue #22)
- Guided PDF upload flow
- Preview extracted text
- Test search before going live

### AI-04: Q&A with Snippet Highlighting (Issue #23)
- Return character positions
- Highlight matching text in PDF viewer
- Fallback to "not found" responses

### AI-05: Response Caching (Issue #24)
- Cache common queries per game
- Redis-backed storage
- TTL-based invalidation

---

## References

- **Qdrant Docs**: https://qdrant.tech/documentation/
- **OpenRouter API**: https://openrouter.ai/docs
- **Text Chunking Best Practices**: https://www.pinecone.io/learn/chunking-strategies/
- **Vector Search Patterns**: https://github.com/openai/openai-cookbook

---

## Acceptance Criteria

✅ **Servizio embeddings HTTP**: EmbeddingService implemented with OpenRouter integration
✅ **Indicizzazione chunk per gioco**: QdrantService with scoped filtering
✅ **Query vettoriali filtrate**: SearchAsync con filtro su `game_id`
✅ **Test coverage**: 34 unit tests + 1 E2E test passing
✅ **Documentation**: This file

**Issue AI-01 is complete and ready for production.**

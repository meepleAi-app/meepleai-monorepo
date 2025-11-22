# DocumentProcessing Bounded Context

**Purpose**: PDF ingestion, extraction, quality validation, and vector indexing for RAG workflows

**Architecture**: DDD + CQRS + MediatR
**Status**: Production-ready (Month 1 complete)
**Quality Score**: 9.2/10 (Code review #956)

---

## 📋 Overview

The DocumentProcessing bounded context manages the complete lifecycle of PDF rulebook documents:

1. **Upload** → Validation (file type, size, PDF integrity)
2. **Extraction** → 3-stage pipeline (Unstructured → SmolDocling → Docnet)
3. **Quality Assessment** → 4-metric scoring system
4. **Indexing** → Text chunking + embedding generation + Qdrant storage
5. **Retrieval** → Query by game, fetch extracted text, manage documents

**Key Metric**: 80% Stage 1 success rate, 1.3s avg processing time, ≥0.80 quality threshold

---

## 🏗️ Architecture

### 3-Stage PDF Extraction Pipeline

```
┌─────────────────────────────────────────────────────────────────────┐
│  HTTP POST /api/v1/ingest/pdf (multipart/form-data)                 │
│  Authorization: Admin or Editor role                                 │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                    ┌───────────▼──────────────┐
                    │ PdfValidationDomainService│
                    │ • Magic bytes check       │
                    │ • File size limit (100MB) │
                    │ • MIME type validation    │
                    │ • PDF version check       │
                    └───────────┬──────────────┘
                                │ Valid?
                                ├─NO──→ 400 Bad Request
                                │
                                YES
                                │
        ┌───────────────────────▼────────────────────────┐
        │ EnhancedPdfProcessingOrchestrator              │
        │                                                 │
        │  Stage 1: Unstructured (fast, RAG-optimized)   │
        │  Quality ≥0.80?  ─YES→ Success (80% cases)     │
        │         │                                       │
        │         NO                                      │
        │         │                                       │
        │  Stage 2: SmolDocling (VLM, complex layouts)   │
        │  Quality ≥0.70?  ─YES→ Success (15% cases)     │
        │         │                                       │
        │         NO                                      │
        │         │                                       │
        │  Stage 3: Docnet (best effort, 5% cases)       │
        │  Always returns result                         │
        └────────────────────┬───────────────────────────┘
                             │
             ┌───────────────▼───────────────┐
             │ PdfQualityValidationDomainService│
             │ 4-metric scoring:              │
             │ • Text coverage (40%)          │
             │ • Structure detection (20%)    │
             │ • Table detection (20%)        │
             │ • Page coverage (20%)          │
             └───────────┬────────────────────┘
                         │
                         ├─ Quality Report Generated
                         │
        ┌────────────────▼───────────────────┐
        │ IndexPdfCommandHandler (CQRS)      │
        │ 1. Chunk text (semantic chunking)  │
        │ 2. Generate embeddings             │
        │ 3. Index to Qdrant                 │
        │ 4. Update VectorDocumentEntity     │
        └────────────────────────────────────┘
```

**Success Flow**: Upload → Validate → Extract (3-stage) → Quality Check → Index → Searchable

**Failure Flow**: Each stage has fallback, final Stage 3 always succeeds (degraded quality acceptable)

---

## 📦 Components

### Domain Layer

**Entities**:
- `PdfDocument` - Aggregate root for PDF document lifecycle

**Value Objects**:
- `FileName` - Encapsulates filename validation and sanitization
- `FileSize` - Enforces size limits (max 100MB)
- `PageCount` - PDF page count value object
- `PdfVersion` - PDF specification version (1.4, 1.5, 1.6, 1.7, 2.0)

**Domain Services**:
- `PdfValidationDomainService` - PDF integrity and compliance validation
- `PdfQualityValidationDomainService` - 4-metric quality scoring
- `PdfTextProcessingDomainService` - Text normalization and preprocessing
- `TableToAtomicRuleConverter` - Converts table data to atomic rules

**Repositories (Interfaces)**:
- `IPdfDocumentRepository` - PDF document persistence

---

### Application Layer (CQRS)

**Commands**:
- `IndexPdfCommand` → `IndexPdfCommandHandler`
  - Orchestrates chunking, embedding, Qdrant indexing
  - Returns `IndexingResultDto` with status and metadata

**Queries**:
- `GetPdfDocumentByIdQuery` → `GetPdfDocumentByIdQueryHandler`
  - Retrieves single PDF document by ID
  - Returns `PdfDocumentDto` with extraction status

- `GetPdfDocumentsByGameQuery` → `GetPdfDocumentsByGameQueryHandler`
  - Retrieves all PDFs for a specific game
  - Returns list of `PdfDocumentDto`

**DTOs**:
- `PdfDocumentDto` - PDF document transfer object
- `IndexingResultDto` - Indexing operation result
- `PdfIndexingErrorCode` - Error categorization enum

**Services**:
- `EnhancedPdfProcessingOrchestrator` - 3-stage extraction pipeline coordinator

---

### Infrastructure Layer

**External Adapters (Extractors)**:
- `UnstructuredPdfTextExtractor` - Stage 1 (Apache 2.0, RAG-optimized, 1.3s avg)
- `SmolDoclingPdfTextExtractor` - Stage 2 (VLM 256M, complex layouts, 3-5s avg)
- `DocnetPdfTextExtractor` - Stage 3 (Local fallback, fast)
- `OrchestratedPdfTextExtractor` - Adapter wrapping orchestrator as `IPdfTextExtractor`

**Interfaces**:
- `IPdfTextExtractor` - Common interface for all extractors
  - `Task<TextExtractionResult> ExtractTextAsync(Stream, bool enableOcr, CancellationToken)`
  - `Task<PagedTextExtractionResult> ExtractPagedTextAsync(Stream, bool enableOcr, CancellationToken)`

- `IPdfValidator` - PDF integrity validation
- `IPdfTableExtractor` - Table structure extraction

**External Models**:
- `UnstructuredModels.cs` - Unstructured API request/response schemas
- `SmolDoclingModels.cs` - SmolDocling API schemas

**Persistence**:
- `PdfDocumentRepository` - EF Core repository implementation
  - Uses `PdfDocumentEntity` for database mapping

**Dependency Injection**:
- `DocumentProcessingServiceExtensions.cs` - Registers all services, extractors, validators

---

## 🔄 CQRS Pattern

### Command Flow Example (IndexPdfCommand)

```
HTTP Request
    │
    ▼
PdfEndpoints.cs
    │ POST /ingest/pdf/{pdfId}/index
    │ Uses: IMediator.Send(new IndexPdfCommand(pdfId))
    ▼
MediatR Pipeline
    │
    ▼
IndexPdfCommandHandler
    │ Implements: ICommandHandler<IndexPdfCommand, IndexingResultDto>
    │
    ├─→ 1. Load PdfDocument from repository
    ├─→ 2. Validate extracted text exists
    ├─→ 3. Chunk text (ITextChunkingService)
    ├─→ 4. Generate embeddings (IEmbeddingService)
    ├─→ 5. Index in Qdrant (IQdrantService)
    ├─→ 6. Update VectorDocumentEntity status
    │
    ▼
Return IndexingResultDto
    │
    ▼
HTTP Response JSON
```

### Query Flow Example (GetPdfDocumentByIdQuery)

```
HTTP Request
    │
    ▼
PdfEndpoints.cs (implied, or via GameEndpoints)
    │ GET /pdfs/{id}
    │ Uses: IMediator.Send(new GetPdfDocumentByIdQuery(id))
    ▼
MediatR Pipeline
    │
    ▼
GetPdfDocumentByIdQueryHandler
    │ Implements: IQueryHandler<GetPdfDocumentByIdQuery, PdfDocumentDto>
    │
    ├─→ 1. Query database (DbContext.PdfDocuments)
    ├─→ 2. Map entity to DTO
    │
    ▼
Return PdfDocumentDto
    │
    ▼
HTTP Response JSON
```

**Key Principle**: HTTP layer uses `IMediator.Send()`, ZERO direct service injection

---

## 🔌 Integration Points

### Upstream (Depends On)

- **KnowledgeBase Context**: Provides `IEmbeddingService`, `IQdrantService` for vector indexing
- **GameManagement Context**: Validates `gameId` exists before PDF association
- **SystemConfiguration Context**: Feature flags (`Features.PdfUpload`), quality thresholds

### Downstream (Used By)

- **KnowledgeBase Context**: Uses indexed PDFs for RAG hybrid search
- **Administration Context**: PDF statistics, processing status monitoring

### External Dependencies

- **Unstructured Python Service** (Port 8001): Stage 1 extractor
- **SmolDocling Python Service** (Port 8002): Stage 2 extractor
- **PostgreSQL**: Persistent storage for `PdfDocumentEntity`, `VectorDocumentEntity`
- **Qdrant** (Port 6333): Vector storage for semantic search
- **File System**: Physical PDF storage (`data/pdfs/`)

---

## 📊 Domain Model

### Aggregates

**PdfDocument** (Aggregate Root):
```
PdfDocument
├── Id (Guid)
├── GameId (Guid) → Foreign key to Game
├── FileName (FileName value object)
├── FileSize (FileSize value object)
├── StoragePath (string)
├── UploadedByUserId (Guid)
├── UploadedAt (DateTime)
├── ProcessingStatus (string: "pending" | "processing" | "completed" | "failed")
├── ProcessedAt (DateTime?)
├── ExtractedText (string) → Output from 3-stage pipeline
├── PageCount (PageCount value object)
├── CharacterCount (int)
├── PdfVersion (PdfVersion value object)
├── ProcessingError (string?)
├── ProcessingProgressJson (string?) → JSON progress tracking
└── QualityScore (double?) → 0.0-1.0 from quality validation
```

### Value Objects (Invariants)

**FileName**:
- Max length: 255 characters
- Allowed characters: alphanumeric, `-`, `_`, `.`, space
- Extension: Must be `.pdf`
- Sanitization: Removes path traversal attempts (`../`, `..\\`)

**FileSize**:
- Min: 1 KB (non-empty file)
- Max: 100 MB (configurable via `PdfProcessing:MaxFileSizeBytes`)
- Validation: Rejects zero-byte and oversized files

**PageCount**:
- Min: 1 (at least one page)
- Max: 1000 (reasonable limit for rulebooks)

**PdfVersion**:
- Supported: 1.4, 1.5, 1.6, 1.7, 2.0
- Validates PDF compliance (rejects corrupted or unsupported versions)

---

## 🚀 Quick Start (Developer Guide)

### Prerequisites

1. **Docker running**: Unstructured + SmolDocling services
2. **PostgreSQL**: Database for entities
3. **Qdrant**: Vector storage (port 6333)

### Start Services

```bash
# From repository root
cd infra
docker compose up -d meepleai-unstructured meepleai-smoldocling meepleai-qdrant meepleai-postgres

# Verify health
curl http://localhost:8001/health  # Unstructured
curl http://localhost:8002/health  # SmolDocling
curl http://localhost:6333/healthz # Qdrant
```

### Upload and Process PDF

```bash
# 1. Upload PDF
curl -X POST http://localhost:8080/api/v1/ingest/pdf \
  -H "Cookie: meepleai-session=<your-session-cookie>" \
  -F "file=@docs/test-pdfs/catan-it.pdf" \
  -F "gameId=<game-guid>"

# Response: { "documentId": "abc-123-def", "fileName": "catan-it.pdf" }

# 2. Check processing progress
curl http://localhost:8080/api/v1/pdfs/abc-123-def/progress

# 3. View extracted text
curl http://localhost:8080/api/v1/pdfs/abc-123-def/text

# 4. Index for semantic search
curl -X POST http://localhost:8080/api/v1/ingest/pdf/abc-123-def/index \
  -H "Cookie: meepleai-session=<your-session-cookie>"

# Response: { "success": true, "chunkCount": 42, "indexedAt": "..." }
```

### Verify Indexing

```bash
# Check Qdrant collection
curl http://localhost:6333/collections/meepleai-rules

# Query vectors
curl http://localhost:8080/api/v1/search?q=costruire+strade&gameId=<game-guid>
```

---

## 🧪 Testing

### Run All Tests

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~DocumentProcessing"
```

**Expected**: 71 tests passing

### Test Categories

**Unit Tests** (Domain + Application):
```bash
dotnet test --filter "FullyQualifiedName~DocumentProcessing.Domain"
dotnet test --filter "FullyQualifiedName~DocumentProcessing.Application"
```

**Integration Tests** (Testcontainers):
```bash
dotnet test --filter "Category=Integration&FullyQualifiedName~DocumentProcessing"
```

**E2E Tests** (Full Pipeline):
```bash
dotnet test --filter "FullyQualifiedName~ThreeStagePdfPipelineE2E"
```

---

## ⚙️ Configuration

### Feature Flags

```json
"Features": {
  "PdfUpload": true,  // Enable/disable PDF upload endpoint
  "PdfIndexing": true // Enable/disable vector indexing
}
```

### PDF Processing

```json
"PdfProcessing": {
  "Extractor": {
    "Provider": "Orchestrator"  // "Unstructured" | "SmolDocling" | "Docnet" | "Orchestrator"
  },
  "Quality": {
    "MinimumThreshold": 0.80,   // Stage 1 acceptance threshold
    "MinCharsPerPage": 500       // Text coverage threshold (500-1000 chars/page)
  },
  "MaxFileSizeBytes": 100000000, // 100MB
  "Unstructured": {
    "ApiUrl": "http://unstructured-service:8001",
    "TimeoutSeconds": 30
  },
  "SmolDocling": {
    "ApiUrl": "http://meepleai-smoldocling:8002",
    "TimeoutSeconds": 60
  }
}
```

### Docker Services

```yaml
# infra/docker-compose.yml
services:
  unstructured-service:
    image: unstructured-service:latest
    ports: ["8001:8001"]
    environment:
      - UNSTRUCTURED_STRATEGY=fast  # or "hi_res" for accuracy

  meepleai-smoldocling:
    image: meepleai-smoldocling:latest
    ports: ["8002:8002"]
    environment:
      - MODEL_NAME=smoldocling-256m
      - DEVICE=cpu  # or "cuda" for GPU acceleration
```

---

## 🧩 Adding a New Extractor (Stage 4)

### Step 1: Implement IPdfTextExtractor

```csharp
public class CustomPdfTextExtractor : IPdfTextExtractor
{
    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        // Your extraction logic
        var text = await YourLibrary.ExtractAsync(pdfStream);

        return new TextExtractionResult(
            Success: true,
            ExtractedText: text,
            PageCount: pages,
            CharacterCount: text.Length,
            OcrTriggered: false,
            Quality: ExtractionQuality.High,
            ErrorMessage: null
        );
    }

    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(...)
    {
        // Paged extraction implementation
    }
}
```

### Step 2: Register in DI

```csharp
// DocumentProcessingServiceExtensions.cs
services.AddScoped<IPdfTextExtractor>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config.GetValue<string>("PdfProcessing:Extractor:Provider");

    return provider switch
    {
        "Custom" => sp.GetRequiredService<CustomPdfTextExtractor>(),
        // ... other providers
    };
});
```

### Step 3: Add to Orchestrator (Optional)

If you want it as Stage 4 in pipeline:

```csharp
// EnhancedPdfProcessingOrchestrator.cs
private readonly IPdfTextExtractor _customExtractor;

// Constructor: inject custom extractor

// In ExtractTextWithFallbackAsync:
// After Stage 3, before final return:
var stage4Result = await TryExtractWithStage(4, "Custom", _customExtractor, pdfBytes, 0.60, enableOcrFallback, requestId, ct);
if (stage4Result != null) return CreateEnhancedResult(stage4Result, 4, "Custom", overallStopwatch.Elapsed, requestId);
```

### Step 4: Add Tests

```csharp
public class CustomPdfTextExtractorTests
{
    [Fact]
    public async Task ExtractText_ValidPdf_ReturnsSuccess()
    {
        // Arrange
        var extractor = new CustomPdfTextExtractor();
        var pdfStream = LoadTestPdf("test.pdf");

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.ExtractedText);
    }
}
```

---

## 📖 API Endpoints

See [API Documentation](../../../docs/03-api/pdf-processing-api.md) for complete endpoint specifications.

**Quick Reference**:

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/v1/ingest/pdf` | Admin, Editor | Upload PDF for game |
| POST | `/api/v1/ingest/pdf/{id}/index` | Admin, Editor | Index PDF for search |
| GET | `/api/v1/pdfs/{id}/text` | Authenticated | Get extracted text |
| GET | `/api/v1/pdfs/{id}/progress` | Owner, Admin | Get processing progress |
| GET | `/api/v1/games/{gameId}/pdfs` | Authenticated | List PDFs for game |
| DELETE | `/api/v1/pdf/{id}` | Owner, Admin | Delete PDF (RLS) |
| DELETE | `/api/v1/pdfs/{id}/processing` | Owner, Admin | Cancel processing |

---

## 🐛 Troubleshooting

See [Troubleshooting Guide](../../../docs/02-development/guides/pdf-processing-troubleshooting.md) for detailed debug procedures.

**Common Issues**:

### "Stage 1 failed, falling back to Stage 2"

**Cause**: Unstructured service down or quality <0.80

**Solution**:
```bash
# Check Unstructured health
curl http://localhost:8001/health

# Restart service
docker compose restart meepleai-unstructured

# Check logs
docker compose logs meepleai-unstructured --tail=50
```

### "All stages failed"

**Cause**: All 3 extractors failed (rare)

**Solution**:
1. Check all services running: `docker compose ps`
2. Review orchestrator logs (Seq): Search for `[RequestId]`
3. Verify PDF is valid: `pdfinfo test.pdf`
4. Try manual extraction: `curl -X POST http://localhost:8001/api/v1/extract -F "file=@test.pdf"`

### "Quality score below threshold"

**Cause**: PDF quality poor (scanned, low resolution)

**Solution**:
1. Lower threshold temporarily: `PdfProcessing:Quality:MinimumThreshold = 0.70`
2. Use Stage 2 (SmolDocling VLM) explicitly
3. Manual review: Check extracted text at `/pdfs/{id}/text`
4. Rescan PDF at higher DPI if possible (300 DPI+ recommended)

---

## 📚 References

**Architecture Decisions**:
- [ADR-003b: Unstructured PDF Extraction](../../../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)
- [ADR-003: PDF Processing Pipeline](../../../docs/01-architecture/adr/adr-003-pdf-processing.md) (superseded)

**Related Documentation**:
- [PDF Processing Troubleshooting Guide](../../../docs/02-development/guides/pdf-processing-troubleshooting.md)
- [PDF Processing API Specification](../../../docs/03-api/pdf-processing-api.md)
- [Unstructured Setup Guide](../../../docs/02-development/guides/unstructured-setup.md)
- [Code Review Checklist](../../../docs/02-development/code-review/pdf-processing-checklist.md)

**Issues**:
- #949 (BGAI-010): EnhancedPdfProcessingOrchestrator
- #951 (BGAI-012): Quality Validation
- #952 (BGAI-001-v2): Unstructured Integration
- #956 (BGAI-014): Code Review Checklist
- #957 (BGAI-015): This documentation

---

**Last Updated**: 2025-11-13
**Owner**: Backend Team
**Status**: ✅ Production-ready (Month 1 complete)


# DocumentProcessing Bounded Context

**PDF Upload, Extraction, Chunking, e Validation**

---

## 📋 Responsabilità

- PDF upload e storage
- 3-stage extraction pipeline (Unstructured → SmolDocling → Docnet)
- Quality validation (text coverage, structure, tables)
- Chunking con overlap (800 tokens, 100 overlap)
- Status tracking e error handling

---

## 🏗️ Domain Model

### Aggregates

**Document**:
```csharp
public class Document
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string Filename { get; private set; }
    public long FileSize { get; private set; }
    public ProcessingStatus Status { get; private set; }
    public int Progress { get; private set; }        // 0-100
    public double QualityScore { get; private set; } // 0.0-1.0
    public List<ExtractionAttempt> Attempts { get; private set; }
    public List<string> Errors { get; private set; }

    public void StartProcessing() { }
    public void RecordAttempt(ExtractionStage stage, double quality) { }
    public void MarkCompleted(double finalQuality) { }
    public void MarkFailed(string error) { }
}
```

**ExtractionAttempt**:
```csharp
public class ExtractionAttempt
{
    public ExtractionStage Stage { get; private set; } // Unstructured | SmolDocling | Docnet
    public double QualityScore { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public bool Succeeded { get; private set; }
}
```

---

## 📡 Application Layer

### Commands

| Command | Description | Endpoint |
|---------|-------------|----------|
| `UploadDocumentCommand` | Upload PDF | `POST /api/v1/documents/upload` |
| `ProcessDocumentCommand` | Start extraction | Internal (background job) |
| `DeleteDocumentCommand` | Delete PDF + chunks | `DELETE /api/v1/documents/{id}` |

### Queries

| Query | Description | Endpoint |
|-------|-------------|----------|
| `GetDocumentByIdQuery` | Document metadata | `GET /api/v1/documents/{id}` |
| `GetProcessingStatusQuery` | Processing status | `GET /api/v1/documents/{id}/status` |
| `ListDocumentsQuery` | User's documents | `GET /api/v1/documents` |

---

## 🔄 3-Stage Extraction Pipeline (ADR-003b)

### Stage 1: Unstructured (80% success)

**Target**: ≥0.80 quality score
**Latency**: <2s average
**Success Rate**: 80% of PDFs

```python
# Unstructured service
result = partition_pdf(
    filename="rules.pdf",
    strategy="hi_res",           # High-resolution layout analysis
    extract_images=False,
    languages=["ita", "eng"]
)
```

**Quality Metrics**:
- Text coverage: 40%
- Structure preservation: 20%
- Table extraction: 20%
- Page coverage: 20%

### Stage 2: SmolDocling VLM (15% fallback)

**Target**: ≥0.70 quality score
**Latency**: 5-8s average
**Success Rate**: 15% fallback cases

```python
# SmolDocling service (VLM)
result = process_document(
    file_path="rules.pdf",
    model="smoldocling-vlm",     # Vision-Language Model
    extract_tables=True
)
```

**Strengths**: Complex layouts, diagrams, multi-column text

### Stage 3: Docnet OCR (5% last resort)

**Target**: Best effort (no threshold)
**Latency**: 3-5s average
**Success Rate**: 5% extreme fallback

```python
# Docnet OCR
result = extract_text(
    file_path="rules.pdf",
    engine="docnet",
    ocr_enabled=True
)
```

**Use Case**: Scanned PDFs, image-heavy documents

---

## 📊 Chunking Strategy

**Configuration**:
- Chunk size: 800 tokens
- Overlap: 100 tokens
- Separator: Double newline (`\n\n`)
- Metadata: Page number, section title

**Example**:
```
Chunk 1 (tokens 0-800):    "Introduzione... setup... ──overlap──"
Chunk 2 (tokens 700-1500): "──overlap── first turn... actions..."
Chunk 3 (tokens 1400-2200): "──overlap── end game... scoring..."
```

**Rationale**: Overlap ensures context continuity across chunk boundaries

---

## 🔄 Integration Points

### Outbound Events

**DocumentProcessedEvent**:
```csharp
public class DocumentProcessedEvent : INotification
{
    public Guid DocumentId { get; init; }
    public List<ChunkDto> Chunks { get; init; }
    public double QualityScore { get; init; }
}
```

**Handled By**:
- **KnowledgeBase**: Generate embeddings and store in Qdrant

---

## 🧪 Testing

**Location**: `tests/Api.Tests/BoundedContexts/DocumentProcessing/`

**Coverage**: 90%

**Key Tests**:
```csharp
UploadDocument_ValidPdf_ShouldStartProcessing()
ProcessDocument_Stage1Success_ShouldNotFallback()
ProcessDocument_Stage1Fail_ShouldTryStage2()
QualityValidation_BelowThreshold_ShouldFallback()
```

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/DocumentProcessing/`

---

## 📖 Related Documentation

- [ADR-003b: Unstructured PDF](../01-architecture/adr/adr-003b-unstructured-pdf.md)
- [ADR-016: Advanced PDF Embedding Pipeline](../01-architecture/adr/adr-016-advanced-pdf-embedding-pipeline.md)
- [PDF Pipeline Diagram](../01-architecture/diagrams/pdf-pipeline-detailed.md)

---

**Last Updated**: 2026-01-18
**Status**: ✅ Production
**Pipeline**: 3-stage fallback (95%+ success rate)

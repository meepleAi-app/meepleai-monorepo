# DocumentProcessing Bounded Context

**PDF Upload, 3-Stage Extraction, Chunking, Vector Indexing**

---

## Responsibilities

- PDF upload & storage (S3/local)
- 3-stage extraction (Unstructured → SmolDocling → Docnet) per ADR-003b
- Chunked uploads (resumable, 5MB chunks)
- Private PDF support
- Document collections (multi-PDF organization)
- Processing progress (SSE streaming)
- Quality validation (text coverage, structure)
- Vector indexing (Qdrant integration)
- Background jobs
- Quota enforcement (tier-based)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **PdfDocument** | Id, GameId, UploadedByUserId, FileName, Status, ExtractedText, QualityScore, IsDeleted | Aggregate root |
| **ExtractionAttempt** | PdfDocumentId, Stage, QualityScore, Succeeded, DurationMs | Pipeline tracking |
| **DocumentCollection** | GameId, UserId, Name, Documents[] | Multi-PDF grouping |
| **ChunkedUploadSession** | SessionId, TotalChunks, ReceivedChunks, IsComplete | Resumable uploads |

**Value Objects**: ExtractionStage (Unstructured/SmolDocling/Docnet), ProcessingStatus (Pending/Extracting/Completed/Failed), ProcessingProgress

**Domain Methods**: `StartProcessing()`, `RecordAttempt()`, `MarkCompleted()`, `MarkFailed()`, `SoftDelete()`, `AddDocument()`

---

## API Operations (26 total)

**14 Commands**: UploadPdf, UploadPrivatePdf, InitChunkedUpload, UploadChunk, CompleteChunkedUpload, ExtractPdfText, IndexPdf, DeletePdf, SetPdfVisibility, CancelPdfProcessing, CreateDocumentCollection, AddDocumentToCollection, RemoveDocumentFromCollection, GenerateRuleSpecFromPdf

**10 Queries**: GetPdfText, DownloadPdf, GetPdfDocumentsByGame, GetPdfDocumentById, GetPdfOwnership, GetPdfProgress, GetCollectionByGame, GetCollectionById, GetCollectionsByUser, ExtractBggGamesFromPdf

**2 Background Jobs**: PDF extraction pipeline, Qdrant indexing

---

## Key Endpoints

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| POST | `/api/v1/ingest/pdf` | Session | Upload PDF |
| POST | `/api/v1/users/{userId}/library/entries/{entryId}/pdf` | Session | Upload private PDF |
| POST | `/api/v1/ingest/pdf/chunked/init` | Session | Init chunked upload |
| POST | `/api/v1/ingest/pdf/chunked/chunk` | Session | Upload chunk |
| POST | `/api/v1/ingest/pdf/chunked/complete` | Session | Complete chunked |
| POST | `/api/v1/ingest/pdf/{pdfId}/extract` | Admin | Extract text |
| POST | `/api/v1/ingest/pdf/{pdfId}/index` | Admin | Vector index |
| GET | `/api/v1/pdfs/{pdfId}/text` | Session | Get extracted text |
| GET | `/api/v1/pdfs/{pdfId}/download` | Session | Download PDF |
| GET | `/api/v1/pdfs/{pdfId}/progress` | Session | Progress (SSE) |
| DELETE | `/api/v1/pdf/{pdfId}` | Session+Owner | Soft-delete |

---

## 3-Stage Extraction Pipeline (ADR-003b)

| Stage | Service | Target Quality | Success Rate | Latency |
|-------|---------|----------------|--------------|---------|
| **Stage 1** | Unstructured.py | ≥ 0.80 | 80% | <2s P95 |
| **Stage 2** | SmolDocling VLM | ≥ 0.70 | 15% | <10s P95 |
| **Stage 3** | Docnet OCR | Accept any | 5% | <5s P95 |

**Quality Calculation**: `TextQuality = 0.5 + (chars_per_page - 500) / 500 * 0.5`

**Decision Logic**: Stage N quality ≥ threshold → RETURN ✅ | Else → fallback to Stage N+1

**Guaranteed Success**: Stage 3 always returns result (best-effort)

---

## Domain Events

| Event | When | Subscribers |
|-------|------|-------------|
| `PdfUploadedEvent` | Upload complete | Administration (audit) |
| `PdfExtractedEvent` | Extraction done | KnowledgeBase (index trigger) |
| `ExtractionStageCompletedEvent` | Stage done | Administration (metrics) |
| `PdfIndexedEvent` | Indexing done | KnowledgeBase, Administration |
| `PdfDeletedEvent` | PDF deleted | KnowledgeBase (cleanup vectors) |
| `ProcessingFailedEvent` | Error | UserNotifications |
| `ChunkedUploadCompletedEvent` | Chunks assembled | Administration |

---

## Integration Points

**Inbound**:
- UserLibrary → UploadPrivatePdf
- SharedGameCatalog → ExtractBggGamesFromPdf

**Outbound**:
- Blob Storage (S3/R2/Local) via `IBlobStorageService`
- Python Services: Unstructured (8000), SmolDocling (8001), Embedding (8002)
- Qdrant (vector DB) via collection: `pdfs`, namespace: `game_{gameId}`
- KnowledgeBase → PdfIndexedEvent enables RAG queries

---

## Security & Quotas

**Access Control**:
- Upload: Authenticated users (quota enforced)
- Download: Owner OR Admin (row-level security)
- Processing: Admin/Editor only
- Private PDFs: Owner only (no admin override)

**Quota Limits**:

| Tier | Storage | File Size | Daily Uploads |
|------|---------|-----------|---------------|
| Free | 100 MB | 10 MB | 5 |
| Basic | 1 GB | 50 MB | 20 |
| Premium | 10 GB | 100 MB | 100 |
| Enterprise | Unlimited | 500 MB | Unlimited |

---

## Performance

**Latency Targets**:
- Total pipeline: <15s P95 (all stages)
- Stage 1: <2s, Stage 2: <10s, Stage 3: <5s

**Caching**:
- GetPdfText: Redis 1h (invalidate: PdfExtractedEvent)
- GetPdfProgress: HybridCache 10s (event-driven)
- GetPdfDocumentsByGame: Redis 30m (invalidate: PdfUploadedEvent, PdfDeletedEvent)

**Database Indexes**:
```sql
idx_pdfs_game ON PdfDocuments(GameId) WHERE NOT IsDeleted
idx_pdfs_user ON PdfDocuments(UploadedByUserId) WHERE NOT IsDeleted
idx_pdfs_status ON PdfDocuments(ProcessingStatus, UploadedAt DESC)
idx_collections_game ON DocumentCollections(GameId, UserId)
```

---

## Testing

**Unit Tests** (`tests/Api.Tests/DocumentProcessing/`):
- EnhancedPdfProcessingOrchestrator_Tests.cs (3-stage pipeline logic)
- QualityCalculator_Tests.cs (quality score formulas)
- ChunkedUploadSession_Tests.cs (out-of-order chunks)
- PdfDocument_Tests.cs (state transitions)

**Integration Tests** (Testcontainers: PostgreSQL, MinIO, Qdrant):
1. End-to-End Upload → Extract → Index
2. 3-Stage Fallback (force Stage 1 failure → verify Stage 2 called)
3. Chunked Upload (10 chunks, missing chunk → complete → reassemble)
4. Private PDF Namespace (isolation verification)

---

## Code Location

`apps/api/src/Api/BoundedContexts/DocumentProcessing/`

---

## Related Documentation

**ADRs**:
- [ADR-003b: Unstructured PDF Processing](../../01-architecture/adr/adr-003b-unstructured-pdf.md)
- [ADR-026: Document Collections](../../01-architecture/adr/adr-026-document-collections.md)

**Contexts**:
- [KnowledgeBase](./knowledge-base.md) - RAG indexing
- [UserLibrary](./user-library.md) - Private PDFs
- [SharedGameCatalog](./shared-game-catalog.md) - BGG extraction

---

**Status**: ✅ Production
**Commands**: 14 | **Queries**: 10 | **Extraction Success**: 100% (3-stage fallback) | **Avg Processing**: <15s P95

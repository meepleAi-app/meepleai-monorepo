# Document Processing - Flussi API

## Panoramica

Il bounded context Document Processing gestisce upload, estrazione, indicizzazione e gestione dei documenti PDF per il sistema RAG.

---

## 1. Upload Standard

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/ingest/pdf` | `UploadPdfCommand` | Multipart: `file`, `gameId`, `gameName?`, `versionType?`, `language?`, `versionNumber?` | `[S]` |
| POST | `/users/{userId}/library/entries/{entryId}/pdf` | `UploadPrivatePdfCommand` | Multipart: `file` | `[S][O]` |

### Flusso Upload Standard

```
┌──────────┐     ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Upload   │────▶│  Validate    │────▶│  Store File  │────▶│  Return      │
│  PDF      │     │  Size/Type   │     │  (S3/Local)  │     │  documentId  │
└──────────┘     └──────────────┘     └──────────────┘     └──────────────┘
                                                                   │
                                                                   ▼
                                                            Auto-trigger
                                                            Extract+Index
```

**Feature Flag**: `Features.PdfUpload`

---

## 2. Chunked Upload

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/ingest/pdf/chunked/init` | `InitChunkedUploadCommand` | `{ gameId, fileName, totalFileSize }` | `[S]` |
| POST | `/ingest/pdf/chunked/chunk` | `UploadChunkCommand` | Multipart: `sessionId`, `chunkIndex`, `chunk` | `[S]` |
| POST | `/ingest/pdf/chunked/complete` | `CompleteChunkedUploadCommand` | `{ sessionId }` | `[S]` |
| GET | `/ingest/pdf/chunked/{sessionId}/status` | `GetChunkedUploadStatusQuery` | — | `[S]` |

### Flusso Chunked Upload

```
POST /chunked/init { gameId, fileName, totalFileSize }
       │
       ▼ { sessionId, totalChunks, chunkSizeBytes, expiresAt }
       │
       ├──▶ POST /chunked/chunk { sessionId, chunkIndex=0, chunk }
       ├──▶ POST /chunked/chunk { sessionId, chunkIndex=1, chunk }
       ├──▶ ...
       ├──▶ POST /chunked/chunk { sessionId, chunkIndex=N, chunk }
       │
       ▼ (all chunks received)
       │
POST /chunked/complete { sessionId }
       │
       ▼ { documentId, fileName }
```

---

## 3. Processing Pipeline

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/ingest/pdf/{pdfId}/extract` | `ExtractPdfTextCommand` | — | `[A/E]` |
| POST | `/ingest/pdf/{pdfId}/index` | `IndexPdfCommand` | — | `[A/E]` |
| POST | `/ingest/pdf/{pdfId}/rulespec` | `GenerateRuleSpecFromPdfCommand` | — | `[A/E]` |
| POST | `/documents/{pdfId}/retry` | `RetryPdfProcessingCommand` | — | `[S][O]` |

### Pipeline 3 Stadi

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  1. EXTRACT  │────▶│  2. INDEX    │────▶│  3. RULESPEC │
│  Text/OCR    │     │  Embeddings  │     │  (opzionale) │
│  POST extract│     │  POST index  │     │  POST rulespec│
└──────────────┘     └──────────────┘     └──────────────┘
       │                    │                     │
       ▼                    ▼                     ▼
  characterCount       vectorDocId            RuleSpec DTO
  pageCount            chunkCount
  processingStatus     indexedAt
```

**Retry**: Max 3 tentativi (429 Too Many Requests)

---

## 4. Progress Tracking e SSE

| Metodo | Path | Command/Query | Response | Auth |
|--------|------|---------------|----------|------|
| GET | `/pdfs/{pdfId}/progress` | `GetPdfProgressQuery` | JSON | `[S][O]` |
| GET | `/pdfs/{pdfId}/status/stream` | `StreamPdfStatusQuery` | SSE | `[S][O]` |
| GET | `/pdfs/{pdfId}/progress/stream` | `StreamPdfProgressQuery` | SSE | `[S][O]` |
| GET | `/documents/{id}/metrics` | `GetPdfMetricsQuery` | JSON | `[S]` |
| DELETE | `/pdfs/{pdfId}/processing` | `CancelPdfProcessingCommand` | JSON | `[S][O]` |

### SSE Events

**`/progress/stream`**:
- Event: `progress` → `ProcessingProgressJson` object
- Event: `heartbeat` → ogni 30 secondi

**`/status/stream`**:
- Event: stato corrente del processamento

---

## 5. Recupero Documenti

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/games/{gameId}/pdfs` | `GetPdfDocumentsByGameQuery` | — | `[S]` |
| GET | `/pdfs/{pdfId}/text` | `GetPdfTextQuery` | — | `[S]` |
| GET | `/pdfs/{pdfId}/download` | `DownloadPdfQuery` | — | `[S][O]` |

---

## 6. Gestione Documenti

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| DELETE | `/pdf/{pdfId}` | `DeletePdfCommand` | — | `[S][O]` |
| PATCH | `/pdfs/{pdfId}/visibility` | `SetPdfVisibilityCommand` | `{ isPublic }` | `[S][O]` |

---

## 7. Document Collections

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/games/{gameId}/document-collections` | `CreateDocumentCollectionCommand` | `{ name, description, initialDocuments[] }` | `[S]` |
| GET | `/games/{gameId}/document-collections` | `GetCollectionByGameQuery` | — | `[S]` |
| GET | `/games/{gameId}/document-collections/{collectionId}` | `GetCollectionByIdQuery` | — | `[S]` |
| GET | `/document-collections/by-user/{userId}` | `GetCollectionsByUserQuery` | — | `[S][O]` |
| POST | `/games/{gameId}/document-collections/{collectionId}/documents` | `AddDocumentToCollectionCommand` | `{ pdfDocumentId, documentType, sortOrder }` | `[S][O]` |
| DELETE | `/games/{gameId}/document-collections/{collectionId}/documents/{documentId}` | `RemoveDocumentFromCollectionCommand` | — | `[S][O]` |

---

## 8. BGG Extraction

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/extract-bgg-games` | `ExtractBggGamesFromPdfQuery` | `{ pdfFilePath }` | `[S]` |

---

## 9. Admin Configuration

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/admin/system/pdf-upload-limits` | `GetPdfUploadLimitsQuery` | — | `[A]` |
| PUT | `/admin/system/pdf-upload-limits` | `UpdatePdfUploadLimitsCommand` | `{ maxFileSizeBytes, maxPagesPerDocument, maxDocumentsPerGame, allowedMimeTypes[] }` | `[A]` |
| GET | `/admin/config/pdf-tier-upload-limits` | `GetPdfTierUploadLimitsQuery` | — | `[A]` |
| PUT | `/admin/config/pdf-tier-upload-limits` | `UpdatePdfTierUploadLimitsCommand` | `{ freeDailyLimit, freeWeeklyLimit, normalDailyLimit, ... }` | `[A]` |
| GET | `/admin/pdfs/metrics/processing` | `GetProcessingMetricsQuery` | — | `[A]` |

---

## Flusso Completo End-to-End

```
1. Upload:     POST /ingest/pdf { file, gameId }
                    │
                    ▼ { documentId }
                    │
2. Monitor:    GET /pdfs/{id}/progress/stream (SSE)
                    │
                    ▼ events: progress, heartbeat
                    │
3. Extract:    POST /ingest/pdf/{id}/extract
                    │
                    ▼ { characterCount, pageCount }
                    │
4. Index:      POST /ingest/pdf/{id}/index
                    │
                    ▼ { vectorDocumentId, chunkCount }
                    │
5. Verify:     GET /games/{gameId}/pdfs
                    │
                    ▼ Lista documenti con stato
                    │
6. Use:        POST /knowledge-base/ask { gameId, query }
                    │
                    ▼ Risposta RAG basata sui documenti indicizzati
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 824 |
| **Passati** | 823 |
| **Falliti** | 0 |
| **Ignorati** | 1 |
| **Pass Rate** | 100% |
| **Durata** | 6s |

### Fix Applicati (2026-02-15)

| Test | Fix |
|------|-----|
| `Handle_WithNonExistentDocument_ThrowsNotFoundException` | Fix parametri `NotFoundException("PDF document", id)` nel handler |
| `Handle_WithCompletedDocument_ReturnsZeroETA` | Mock `CalculateETAAsync` → `TimeSpan.Zero` + `MarkAsCompleted()` per `ProcessedAt` |
| `CalculateETAAsync_InsufficientHistoricalData_UsesFallback` | `BeGreaterThan(60)` → `BeGreaterThanOrEqualTo(60)` |
| `CleanupOldMetricsAsync_RetainsCorrectCount` | Skip: `ExecuteDeleteAsync` non supportato da InMemory provider |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Upload Standard | `UploadPdfCommandHandlerTests.cs` | Passato |
| Chunked Upload | `InitChunkedUploadTests.cs`, `UploadChunkTests.cs`, `CompleteChunkedUploadTests.cs` | Passato |
| Extraction | `ExtractPdfTextCommandHandlerTests.cs` | Passato |
| Indexing | `IndexPdfCommandHandlerTests.cs` | Passato |
| PDF Metrics/ETA | `GetPdfMetricsQueryHandlerTests.cs` | Passato |
| Download | `DownloadPdfQueryHandlerTests.cs` | Passato |
| Delete/Visibility | `DeletePdfCommandHandlerTests.cs`, `SetPdfVisibilityTests.cs` | Passato |
| Collections | `CreateDocumentCollectionTests.cs` | Passato |
| Text Chunking | `TextChunkerTests.cs`, `SemanticChunkerTests.cs` | Passato |
| Validators | 4 file di validazione | Passato |
| Domain Entities | `Document.cs`, `Page.cs`, `TextChunk.cs` | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*

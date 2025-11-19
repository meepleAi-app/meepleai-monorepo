# PDF Processing API Specification

**Version**: v1
**Base URL**: `/api/v1`
**Authentication**: Cookie-based session or API key
**Content-Type**: `multipart/form-data` (upload), `application/json` (other endpoints)

---

## 📋 Endpoints Overview

| Method | Endpoint | Auth | Role | Description |
|--------|----------|------|------|-------------|
| POST | `/ingest/pdf` | ✅ | Admin, Editor | Upload PDF for game rulebook |
| POST | `/ingest/pdf/{id}/index` | ✅ | Admin, Editor | Index PDF for semantic search |
| POST | `/ingest/pdf/{id}/rulespec` | ✅ | Admin, Editor | Generate rule spec from PDF |
| GET | `/pdfs/{id}/text` | ✅ | All | Get extracted text from PDF |
| GET | `/pdfs/{id}/progress` | ✅ | Owner, Admin | Get PDF processing progress |
| GET | `/games/{gameId}/pdfs` | ✅ | All | List all PDFs for game |
| DELETE | `/pdf/{id}` | ✅ | Owner, Admin | Delete PDF (Row-Level Security) |
| DELETE | `/pdfs/{id}/processing` | ✅ | Owner, Admin | Cancel active processing |

---

## 🔐 Authentication

### Cookie-based Session

```bash
# Login first
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.dev","password":"Demo123!"}' \
  -c cookies.txt

# Use cookie for subsequent requests
curl http://localhost:5080/api/v1/ingest/pdf \
  -b cookies.txt \
  -F "file=@rulebook.pdf" \
  -F "gameId=<guid>"
```

### API Key

```bash
curl -X POST http://localhost:5080/api/v1/ingest/pdf \
  -H "X-API-Key: mpl_prod_<base64-key>" \
  -F "file=@rulebook.pdf" \
  -F "gameId=<guid>"
```

---

## 📤 POST /ingest/pdf - Upload PDF

**Purpose**: Upload PDF rulebook and trigger 3-stage extraction pipeline

### Request

```http
POST /api/v1/ingest/pdf HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
Content-Type: multipart/form-data; boundary=----FormBoundary

------FormBoundary
Content-Disposition: form-data; name="file"; filename="catan-it.pdf"
Content-Type: application/pdf

<PDF binary data>
------FormBoundary
Content-Disposition: form-data; name="gameId"

550e8400-e29b-41d4-a716-446655440000
------FormBoundary--
```

**Form Fields**:
- `file` (file, required): PDF file to upload
- `gameId` (string, required): GUID of game this rulebook belongs to

**Validation Rules**:
- File type: Must be `application/pdf` (magic bytes: `%PDF`)
- File size: 1 KB - 100 MB (configurable)
- PDF version: 1.4, 1.5, 1.6, 1.7, 2.0 supported
- Page count: 1-1000 pages
- Filename: Max 255 chars, no path traversal attempts

### Response (Success)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "documentId": "abc-123-def-456",
  "fileName": "catan-it.pdf"
}
```

**Fields**:
- `documentId` (string): GUID of created PDF document (use for indexing, retrieval)
- `fileName` (string): Sanitized filename stored

### Response (Validation Error)

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "validation_failed",
  "details": {
    "file": "File size exceeds maximum (100 MB)",
    "mime": "Invalid MIME type, expected application/pdf"
  }
}
```

### Response (Feature Disabled)

```http
HTTP/1.1 403 Forbidden
Content-Type: application/json

{
  "error": "feature_disabled",
  "message": "PDF uploads are currently disabled",
  "featureName": "Features.PdfUpload"
}
```

### Response (Authorization Error)

```http
HTTP/1.1 403 Forbidden

// User role is not Admin or Editor
```

---

## 🔍 GET /pdfs/{id}/text - Get Extracted Text

**Purpose**: Retrieve extracted text from processed PDF

### Request

```http
GET /api/v1/pdfs/abc-123-def/text HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

**Path Parameters**:
- `id` (guid, required): PDF document ID from upload response

### Response (Success)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "abc-123-def",
  "fileName": "catan-it.pdf",
  "extractedText": "# CATAN - Regolamento\n\n## Panoramica\nCatan è un gioco...",
  "processingStatus": "completed",
  "processedAt": "2025-11-13T14:30:00Z",
  "pageCount": 24,
  "characterCount": 45678,
  "processingError": null
}
```

**Fields**:
- `id` (string): PDF document GUID
- `fileName` (string): Original filename
- `extractedText` (string): Full extracted text (can be large, 50KB-500KB)
- `processingStatus` (string): `"pending"` | `"processing"` | `"completed"` | `"failed"`
- `processedAt` (datetime?): When extraction completed (null if pending)
- `pageCount` (int): Number of pages in PDF
- `characterCount` (int): Total characters extracted
- `processingError` (string?): Error message if status is `"failed"`

### Response (Not Found)

```http
HTTP/1.1 404 Not Found
Content-Type: application/json

{
  "error": "PDF not found"
}
```

### Response (Processing Incomplete)

```http
HTTP/1.1 200 OK

{
  "id": "abc-123-def",
  "fileName": "catan-it.pdf",
  "extractedText": "",
  "processingStatus": "processing",
  "processedAt": null,
  "processingError": null
}
```

**Note**: `extractedText` will be empty until `processingStatus = "completed"`

---

## 📈 GET /pdfs/{id}/progress - Get Processing Progress

**Purpose**: Real-time progress tracking for long-running PDF processing

### Request

```http
GET /api/v1/pdfs/abc-123-def/progress HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

**Authorization**: Owner or Admin can view progress

### Response (In Progress)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "stage": "extraction",
  "currentStep": 2,
  "totalSteps": 3,
  "percentComplete": 66,
  "message": "Extracting text with SmolDocling (Stage 2)...",
  "estimatedSecondsRemaining": 45
}
```

**Fields**:
- `stage` (string): `"validation"` | `"extraction"` | `"indexing"` | `"completed"`
- `currentStep` (int): Current step number (1-based)
- `totalSteps` (int): Total steps in current stage
- `percentComplete` (int): Overall progress 0-100
- `message` (string): Human-readable status message
- `estimatedSecondsRemaining` (int?): Time estimate (null if unknown)

### Response (Completed)

```http
HTTP/1.1 200 OK
Content-Type: application/json

null
```

**Note**: Returns `null` when processing is complete (check `/text` endpoint for results)

### Response (Forbidden)

```http
HTTP/1.1 403 Forbidden

// User is not owner and not admin
```

---

## 🗂️ POST /ingest/pdf/{id}/index - Index PDF for Search

**Purpose**: Generate embeddings and index PDF in Qdrant for semantic search

### Request

```http
POST /api/v1/ingest/pdf/abc-123-def/index HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

**Prerequisites**:
- PDF must have `processingStatus = "completed"`
- `extractedText` must be non-empty
- Qdrant service must be available

### Response (Success)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "success": true,
  "vectorDocumentId": "def-456-ghi",
  "chunkCount": 42,
  "indexedAt": "2025-11-13T14:35:00Z"
}
```

**Fields**:
- `success` (boolean): Always `true` in success response
- `vectorDocumentId` (string): GUID of created VectorDocumentEntity
- `chunkCount` (int): Number of text chunks indexed (typically 20-100)
- `indexedAt` (datetime): When indexing completed

**Chunking Strategy**:
- Semantic chunking (by_title)
- Max chunk size: 2000 characters
- Overlap: 200 characters
- Preserves document structure (titles, headers)

### Response (Text Extraction Required)

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "PDF text extraction required. Please extract text before indexing."
}
```

### Response (PDF Not Found)

```http
HTTP/1.1 404 Not Found
Content-Type: application/json

{
  "error": "PDF not found"
}
```

### Response (Indexing Failed)

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "Qdrant indexing failed: Connection timeout"
}
```

**Error Codes** (in `IndexingResultDto`):
- `PdfNotFound` → 404
- `TextExtractionRequired` → 400
- `ChunkingFailed` → 400
- `EmbeddingFailed` → 400
- `QdrantIndexingFailed` → 400
- `UnexpectedError` → 400

---

## 📊 GET /games/{gameId}/pdfs - List PDFs for Game

**Purpose**: Retrieve all PDFs uploaded for a specific game

### Request

```http
GET /api/v1/games/550e8400-e29b-41d4-a716-446655440000/pdfs HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

### Response

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "pdfs": [
    {
      "id": "abc-123-def",
      "fileName": "catan-it.pdf",
      "uploadedAt": "2025-11-13T14:00:00Z",
      "uploadedBy": "user@example.com",
      "processingStatus": "completed",
      "pageCount": 24,
      "characterCount": 45678,
      "qualityScore": 0.85,
      "indexed": true
    },
    {
      "id": "def-456-ghi",
      "fileName": "catan-espansione.pdf",
      "uploadedAt": "2025-11-13T15:00:00Z",
      "uploadedBy": "user@example.com",
      "processingStatus": "processing",
      "pageCount": null,
      "characterCount": null,
      "qualityScore": null,
      "indexed": false
    }
  ]
}
```

**Fields per PDF**:
- `id` (string): PDF document GUID
- `fileName` (string): Original filename
- `uploadedAt` (datetime): Upload timestamp (UTC)
- `uploadedBy` (string): Uploader email
- `processingStatus` (string): Current status
- `pageCount` (int?): Pages (null if processing incomplete)
- `characterCount` (int?): Characters extracted (null if incomplete)
- `qualityScore` (double?): Quality score 0.0-1.0 (null if incomplete)
- `indexed` (boolean): Whether indexed in Qdrant

---

## 🗑️ DELETE /pdf/{id} - Delete PDF

**Purpose**: Delete PDF document with Row-Level Security

### Request

```http
DELETE /api/v1/pdf/abc-123-def HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

**Authorization Rules**:
- ✅ **Owner**: User who uploaded the PDF
- ✅ **Admin**: Any admin can delete any PDF
- ❌ **Other users**: Forbidden (403)

### Response (Success)

```http
HTTP/1.1 204 No Content
```

**Side Effects**:
- PDF file deleted from file system (`data/pdfs/`)
- `PdfDocumentEntity` deleted from database
- Vectors deleted from Qdrant
- `VectorDocumentEntity` deleted
- Audit log entry created

### Response (Forbidden - Not Owner)

```http
HTTP/1.1 403 Forbidden
```

**Audit Log**:
```json
{
  "userId": "user-123",
  "action": "ACCESS_DENIED",
  "resource": "PdfDocument",
  "resourceId": "abc-123-def",
  "outcome": "Denied",
  "details": "User attempted to delete PDF owned by another user. User role: User, Owner: owner-456. RLS scope: own resources only."
}
```

---

## ⏸️ DELETE /pdfs/{id}/processing - Cancel Processing

**Purpose**: Cancel in-flight PDF processing (extraction or indexing)

### Request

```http
DELETE /api/v1/pdfs/abc-123-def/processing HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

**Authorization**: Owner or Admin

### Response (Success)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "message": "Processing cancellation requested"
}
```

**Note**: Cancellation is asynchronous. Check `/progress` endpoint to confirm.

### Response (Already Completed)

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "Processing already completed or failed"
}
```

---

## 📝 POST /ingest/pdf/{id}/rulespec - Generate Rule Spec

**Purpose**: Extract structured rule specification from PDF using LLM

### Request

```http
POST /api/v1/ingest/pdf/abc-123-def/rulespec HTTP/1.1
Host: localhost:5080
Cookie: meepleai-session=<session-token>
```

**Prerequisites**:
- PDF processing must be complete (`processingStatus = "completed"`)

### Response (Success)

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "gameId": "550e8400-e29b-41d4-a716-446655440000",
  "ruleSpec": {
    "playerCount": { "min": 3, "max": 4 },
    "playTime": "60-90 minutes",
    "ageRating": "10+",
    "rules": [
      {
        "category": "Setup",
        "rules": [
          "Each player receives 2 settlements and 2 roads",
          "Place settlements on intersection points"
        ]
      }
    ],
    "winCondition": "First player to 10 victory points wins"
  }
}
```

### Response (PDF Not Processed)

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json

{
  "error": "PDF text extraction not completed. Please wait for processing to finish."
}
```

---

## 🔍 Error Handling

### Error Response Format

All error responses follow this schema:

```json
{
  "error": "<error_code>",
  "message": "<human-readable-message>",
  "details": { /* optional additional context */ }
}
```

### Common Error Codes

| Code | HTTP Status | Meaning |
|------|-------------|---------|
| `validation_failed` | 400 | Request validation failed (see `details`) |
| `pdf_not_found` | 404 | PDF document ID not found |
| `feature_disabled` | 403 | PDF upload/indexing feature flag disabled |
| `unauthorized` | 401 | Authentication required |
| `forbidden` | 403 | User lacks required role or ownership |
| `text_extraction_required` | 400 | PDF must be processed before indexing |
| `chunking_failed` | 400 | Text chunking failed (empty text?) |
| `embedding_failed` | 400 | Embedding generation failed (service down?) |
| `qdrant_indexing_failed` | 400 | Qdrant indexing failed (connection issue?) |
| `unexpected_error` | 500 | Unhandled exception (check logs) |

---

## 📊 Status Codes

| Status | Code | Meaning | Example |
|--------|------|---------|---------|
| ✅ Success | 200 | Request successful | PDF indexed |
| ✅ Created | 201 | Resource created | (not used, returns 200) |
| ✅ No Content | 204 | Success, no response body | PDF deleted |
| ❌ Bad Request | 400 | Validation error | Invalid file size |
| ❌ Unauthorized | 401 | Authentication required | No cookie/API key |
| ❌ Forbidden | 403 | Authorization failed | Non-admin trying to delete |
| ❌ Not Found | 404 | Resource not found | Invalid PDF ID |
| ❌ Conflict | 409 | (not used currently) | |
| ❌ Internal Error | 500 | Unexpected error | Unhandled exception |
| ❌ Service Unavailable | 503 | External service down | Unstructured API offline |

---

## 🎯 Rate Limiting

**Current**: No rate limiting (MVP)

**Planned (Phase 2)**:
- 10 PDF uploads per user per hour
- 100 indexing requests per hour
- Configurable via `RateLimit:PdfUpload:RequestsPerHour`

**Headers** (when implemented):
```http
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 7
X-RateLimit-Reset: 1699564800
```

---

## 🔄 Idempotency

### Upload Endpoint

**Not idempotent**: Each upload creates a new `PdfDocument` entity

**Behavior**: Uploading the same file twice creates 2 separate documents

**Recommendation**: Check if PDF exists before upload:
```bash
# Check existing PDFs for game
curl http://localhost:5080/api/v1/games/{gameId}/pdfs

# If duplicate found, use existing document ID
```

### Index Endpoint

**Idempotent**: Indexing the same PDF twice overwrites previous vectors

**Behavior**:
1. Checks if `VectorDocumentEntity` exists for PDF
2. If exists: Deletes old vectors from Qdrant, re-indexes with new chunks
3. If not exists: Creates new `VectorDocumentEntity`, indexes

**Use Case**: Re-index after improving chunking strategy or embedding model

---

## 📐 Request/Response Schemas

### PdfDocumentDto

```typescript
interface PdfDocumentDto {
  id: string;                    // GUID
  gameId: string;                // GUID
  fileName: string;              // Sanitized filename
  fileSize: number;              // Bytes
  uploadedAt: string;            // ISO 8601 datetime (UTC)
  uploadedByUserId: string;      // GUID
  processingStatus: ProcessingStatus;
  processedAt: string | null;    // ISO 8601 or null
  extractedText: string;         // Can be very large (50KB-500KB)
  pageCount: number | null;
  characterCount: number | null;
  pdfVersion: string | null;     // "1.7", "2.0", etc.
  qualityScore: number | null;   // 0.0-1.0
  processingError: string | null;
}

type ProcessingStatus = "pending" | "processing" | "completed" | "failed";
```

### IndexingResultDto

```typescript
interface IndexingResultDto {
  success: boolean;
  vectorDocumentId?: string;     // GUID (if success)
  chunkCount?: number;           // Number of chunks indexed
  indexedAt?: string;            // ISO 8601 datetime
  errorMessage?: string;         // Error message (if failed)
  errorCode?: PdfIndexingErrorCode;
}

enum PdfIndexingErrorCode {
  PdfNotFound = 0,
  TextExtractionRequired = 1,
  ChunkingFailed = 2,
  EmbeddingFailed = 3,
  QdrantIndexingFailed = 4,
  UnexpectedError = 5
}
```

### ProcessingProgress

```typescript
interface ProcessingProgress {
  stage: "validation" | "extraction" | "indexing" | "completed";
  currentStep: number;           // 1-based
  totalSteps: number;
  percentComplete: number;       // 0-100
  message: string;               // Human-readable status
  estimatedSecondsRemaining: number | null;
}
```

---

## 🧪 Example Workflows

### Complete Upload + Index Workflow

```bash
#!/bin/bash

# 1. Upload PDF
RESPONSE=$(curl -s -X POST http://localhost:5080/api/v1/ingest/pdf \
  -b cookies.txt \
  -F "file=@catan-it.pdf" \
  -F "gameId=550e8400-e29b-41d4-a716-446655440000")

# Extract document ID
DOC_ID=$(echo $RESPONSE | jq -r '.documentId')
echo "Uploaded: $DOC_ID"

# 2. Poll for processing completion (max 2 minutes)
for i in {1..24}; do
  PROGRESS=$(curl -s http://localhost:5080/api/v1/pdfs/$DOC_ID/progress -b cookies.txt)

  if [ "$PROGRESS" == "null" ]; then
    echo "Processing complete!"
    break
  fi

  PCT=$(echo $PROGRESS | jq -r '.percentComplete')
  MSG=$(echo $PROGRESS | jq -r '.message')
  echo "Progress: $PCT% - $MSG"

  sleep 5
done

# 3. Verify extraction
TEXT=$(curl -s http://localhost:5080/api/v1/pdfs/$DOC_ID/text -b cookies.txt)
STATUS=$(echo $TEXT | jq -r '.processingStatus')
CHARS=$(echo $TEXT | jq -r '.characterCount')

if [ "$STATUS" == "completed" ]; then
  echo "Extraction successful: $CHARS characters"
else
  echo "Extraction failed: $STATUS"
  exit 1
fi

# 4. Index for search
INDEX_RESULT=$(curl -s -X POST http://localhost:5080/api/v1/ingest/pdf/$DOC_ID/index -b cookies.txt)
CHUNKS=$(echo $INDEX_RESULT | jq -r '.chunkCount')
echo "Indexed: $CHUNKS chunks"

# 5. Test search
curl -s "http://localhost:5080/api/v1/search?q=costruire+strade&gameId=550e8400-e29b-41d4-a716-446655440000" \
  -b cookies.txt | jq '.results[] | {text: .text, page: .page, score: .score}'
```

### Batch Upload Workflow

```bash
#!/bin/bash

GAME_ID="550e8400-e29b-41d4-a716-446655440000"
PDF_DIR="./rulebooks"

# Upload all PDFs in directory
for pdf in $PDF_DIR/*.pdf; do
  echo "Uploading: $pdf"

  RESPONSE=$(curl -s -X POST http://localhost:5080/api/v1/ingest/pdf \
    -b cookies.txt \
    -F "file=@$pdf" \
    -F "gameId=$GAME_ID")

  DOC_ID=$(echo $RESPONSE | jq -r '.documentId')
  echo "  Document ID: $DOC_ID"

  # Wait 10 seconds between uploads (avoid overload)
  sleep 10
done

echo "Batch upload complete!"
```

---

## 🔒 Security Considerations

### Input Validation

**File Upload**:
- ✅ Magic bytes verification (`%PDF` signature)
- ✅ MIME type check (`application/pdf`)
- ✅ File size limit (100 MB max)
- ✅ Filename sanitization (path traversal prevention)
- ✅ PDF version validation (1.4-2.0 supported)

**Authorization**:
- ✅ Role-based access (Admin, Editor for uploads)
- ✅ Row-Level Security (users can only delete own PDFs)
- ✅ Audit logging (all delete operations logged)

### Data Protection

**Sensitive Data**:
- ❌ PDF content NOT logged (only metadata: page count, character count)
- ❌ Extracted text NOT exposed in error messages
- ✅ Request IDs used for correlation (non-sensitive)

**Resource Limits**:
- ✅ File size: 100 MB max
- ✅ Page count: 1-1000 pages
- ✅ CancellationToken: All operations support timeout
- ⚠️ No concurrent upload limit (planned for Phase 2)

---

## 📈 Performance

### Expected Latencies

| Operation | P50 | P95 | P99 | Notes |
|-----------|-----|-----|-----|-------|
| **Upload** (validation only) | 100ms | 200ms | 500ms | File I/O, validation |
| **Extraction (Stage 1)** | 1.3s | 2.5s | 5s | Unstructured processing |
| **Extraction (Stage 2)** | 3.5s | 5s | 8s | SmolDocling VLM |
| **Extraction (Stage 3)** | 800ms | 1.5s | 3s | Docnet fallback |
| **Indexing** | 2s | 5s | 10s | Chunking + embeddings + Qdrant |
| **Total (upload → indexed)** | 5s | 10s | 20s | End-to-end workflow |

**Optimization Tips**:
- Use `fast` strategy for Unstructured (0.3s vs 1.3s, slight quality trade-off)
- Pre-warm SmolDocling model (first request slow due to loading)
- Enable Qdrant caching for repeated queries

### Concurrent Requests

**Current Capacity** (MVP):
- 3 concurrent PDF extractions (limited by Python service workers)
- 5 concurrent indexing operations (Qdrant connection pool)

**Phase 2 Scaling**:
- Horizontal scaling: Multiple Unstructured pods behind load balancer
- GPU acceleration: SmolDocling with CUDA (10x faster)

---

## 🐛 Common Issues

### 1. "Unstructured service unavailable"

**Symptoms**: All uploads fail with Stage 1 errors, fall back to Stage 2

**Debug**:
```bash
# Check service health
curl http://localhost:8001/health

# Check Docker container
docker compose ps meepleai-unstructured
docker compose logs meepleai-unstructured --tail=50

# Restart if needed
docker compose restart meepleai-unstructured
```

**Root Causes**:
- Docker container crashed (OOM if processing large PDF)
- Network issue (check `docker network ls`)
- Service startup time (wait 30s after `docker compose up`)

### 2. "Quality score below threshold"

**Symptoms**: PDF processed but quality <0.80, falls back to Stage 2/3

**Debug**:
```bash
# Check extracted text quality
curl http://localhost:5080/api/v1/pdfs/{id}/text | jq '.characterCount, .pageCount'

# Calculate chars/page (should be ≥500 for quality 0.80)
# If chars/page < 500: PDF is low quality (scanned at low DPI?)
```

**Solutions**:
- Lower threshold temporarily: `PdfProcessing:Quality:MinimumThreshold = 0.70`
- Re-scan PDF at higher DPI (300 DPI+ recommended)
- Use Stage 2 (SmolDocling VLM) explicitly via config
- Manual review: Admin can approve low-quality PDFs if text is readable

### 3. "Indexing failed: No chunks created"

**Symptoms**: Extraction succeeds but indexing fails

**Debug**:
```bash
# Check extracted text length
curl http://localhost:5080/api/v1/pdfs/{id}/text | jq '.extractedText | length'

# If length < 100: Text too short for chunking
```

**Root Cause**: PDF is images-only (no text layer), and OCR failed

**Solution**:
- Enable OCR fallback in Stage 3: `enableOcrFallback=true` (default)
- Check Tesseract installation in Docnet container
- Verify PDF is not corrupted: `pdfinfo file.pdf`

---

## 📚 Related Documentation

**Architecture**:
- [ADR-003b: Unstructured PDF Extraction](../../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)
- [Bounded Context README](../../apps/api/src/Api/BoundedContexts/DocumentProcessing/README.md)

**Development Guides**:
- [PDF Processing Troubleshooting](../../docs/02-development/guides/pdf-processing-troubleshooting.md)
- [Unstructured Setup Guide](../../docs/02-development/guides/unstructured-setup.md)

**Code Review**:
- [PDF Processing Checklist](../../docs/02-development/code-review/pdf-processing-checklist.md)

---

**Version**: 1.0
**Last Updated**: 2025-11-13
**Maintainer**: Backend Team

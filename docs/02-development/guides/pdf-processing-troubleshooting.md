# PDF Processing Troubleshooting Guide

**Version**: 1.0
**Last Updated**: 2025-11-13
**Context**: DocumentProcessing Bounded Context

---

## 🎯 Quick Diagnostic

**Symptom**: PDF upload fails
**First Check**: [Upload Failures](#-upload-failures)

**Symptom**: Processing stuck at "pending"
**First Check**: [Processing Stuck](#-processing-stuck)

**Symptom**: Quality score too low
**First Check**: [Quality Issues](#-quality-issues)

**Symptom**: Indexing fails
**First Check**: [Indexing Failures](#-indexing-failures)

---

## 📤 Upload Failures

### Issue 1: "validation_failed: File size exceeds maximum"

**Symptoms**:
```json
{
  "error": "validation_failed",
  "details": {
    "file": "File size exceeds maximum (100 MB)"
  }
}
```

**Root Cause**: PDF file size > configured maximum (default 100 MB)

**Solutions**:

1. **Increase limit** (if PDF is legitimate):
```json
// appsettings.json
"PdfProcessing": {
  "MaxFileSizeBytes": 209715200  // 200 MB
}
```

2. **Compress PDF** (if file is unnecessarily large):
```bash
# Using Ghostscript
gs -sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/ebook \
   -dNOPAUSE -dQUIET -dBATCH \
   -sOutputFile=output-compressed.pdf input-large.pdf

# Compression: /screen (low), /ebook (medium), /printer (high quality)
```

3. **Check actual file size**:
```bash
ls -lh rulebook.pdf
# If size is reasonable (<100 MB), check for frontend bug sending wrong file
```

---

### Issue 2: "validation_failed: Invalid MIME type"

**Symptoms**:
```json
{
  "error": "validation_failed",
  "details": {
    "mime": "Invalid MIME type, expected application/pdf"
  }
}
```

**Root Cause**: File is not a PDF (magic bytes check failed)

**Debug**:
```bash
# Check file type
file rulebook.pdf
# Expected: "PDF document, version 1.7"

# Check magic bytes
head -c 4 rulebook.pdf | od -c
# Expected: %PDF
```

**Solutions**:
- Ensure file extension is `.pdf`
- Verify file is actual PDF (not renamed .docx or .txt)
- Re-export from source (Photoshop, InDesign, etc.)

---

### Issue 3: "feature_disabled: PDF uploads are currently disabled"

**Symptoms**:
```json
{
  "error": "feature_disabled",
  "message": "PDF uploads are currently disabled",
  "featureName": "Features.PdfUpload"
}
```

**Root Cause**: Feature flag disabled in configuration

**Solutions**:

1. **Check feature flag** (database):
```sql
SELECT value FROM system_configurations
WHERE category = 'Features' AND key = 'PdfUpload';
```

2. **Enable via Admin UI**:
```
Navigate: http://localhost:3000/admin/configuration
Find: Features.PdfUpload
Toggle: ON
```

3. **Enable via API** (if admin UI unavailable):
```bash
curl -X PUT http://localhost:8080/api/v1/admin/configuration \
  -H "Cookie: meepleai-session=<admin-session>" \
  -d '{"category":"Features","key":"PdfUpload","value":"true"}'
```

---

## ⏳ Processing Stuck

### Issue 4: PDF stuck at "pending" status

**Symptoms**:
- Upload succeeds, but `processingStatus` never changes to `"processing"`
- `/progress` endpoint returns `null` immediately

**Root Cause**: Background task not triggered

**Debug**:
```bash
# Check background task service logs (Seq)
# Search for: "Starting PDF processing for document {PdfId}"

# If no logs found: Background task not registered
```

**Solutions**:

1. **Restart API**:
```bash
docker compose restart api
# Background task queue may be stuck
```

2. **Check PdfStorageService**:
```csharp
// Verify BackgroundTaskService.QueueTask is called in UploadPdfAsync
// Expected: _backgroundTaskService.QueueTask(pdf.Id.ToString(), async ct => { ... });
```

3. **Manual trigger** (temporary workaround):
```bash
# Re-upload same file, should trigger processing
```

---

### Issue 5: PDF stuck at "processing" for >5 minutes

**Symptoms**:
- `processingStatus = "processing"` for >5 minutes
- `/progress` shows same step repeatedly

**Root Cause**: Extraction hanging (Stage 1 or 2 timeout)

**Debug**:
```bash
# Check Unstructured service logs
docker compose logs unstructured-service --tail=100 | grep "ERROR"

# Check SmolDocling logs
docker compose logs smoldocling-service --tail=100 | grep "ERROR"

# Check API logs (Seq) for timeout errors
```

**Solutions**:

1. **Cancel processing**:
```bash
curl -X DELETE http://localhost:8080/api/v1/pdfs/{id}/processing -b cookies.txt
```

2. **Increase timeout**:
```json
"PdfProcessing": {
  "Extractor": {
    "TimeoutSeconds": 300  // 5 minutes (from 120s)
  }
}
```

3. **Retry extraction**:
```bash
# Delete and re-upload
curl -X DELETE http://localhost:8080/api/v1/pdf/{id} -b cookies.txt
# Upload again
```

---

## ⚠️ Quality Issues

### Issue 6: "Stage 1 failed, falling back to Stage 2"

**Symptoms**:
- Logs show: `[RequestId] Stage 1 (Unstructured) quality below threshold - Score: 0.75 < 0.80`
- Processing completes with Stage 2 (slower)

**Root Cause**: PDF quality doesn't meet Stage 1 threshold (0.80)

**Debug**:
```bash
# Check quality report
curl http://localhost:8080/api/v1/pdfs/{id}/text | jq '.qualityScore, .characterCount, .pageCount'

# Calculate chars/page
# If <800 chars/page: Quality score will be <0.80
```

**Solutions**:

**Option A: Accept Stage 2** (recommended):
- Stage 2 (SmolDocling) provides good quality (0.70-0.79)
- No action needed, fallback working as designed

**Option B: Lower Stage 1 threshold**:
```json
"PdfProcessing": {
  "Quality": {
    "MinimumThreshold": 0.75  // Accept more PDFs in Stage 1
  }
}
```

**Option C: Use hi_res strategy**:
```json
"PdfProcessing": {
  "Unstructured": {
    "Strategy": "hi_res"  // Slower (4s) but higher quality
  }
}
```

---

### Issue 7: "Quality score 0.25 (VeryLow)"

**Symptoms**:
- PDF processes but quality is very low (<0.50)
- Extracted text is garbled or incomplete

**Root Cause**: Scanned PDF at low resolution, or corrupted file

**Debug**:
```bash
# Check PDF metadata
pdfinfo file.pdf
# Look for: "Page size", "PDF version", "Encrypted: no"

# Check if scanned (images only)
pdffonts file.pdf
# If "no fonts" → PDF is images-only (requires OCR)

# Check DPI
pdfimages -list file.pdf
# DPI <150: Low quality scan
```

**Solutions**:

1. **Re-scan at higher DPI** (best solution):
```
Scan settings:
- DPI: 300 (minimum), 600 (recommended for small text)
- Color: Grayscale (smaller files, faster OCR)
- Format: PDF/A (archival quality)
```

2. **Enable OCR explicitly**:
```csharp
// Already enabled by default, verify:
"PdfProcessing": {
  "Docnet": {
    "EnableOcr": true,
    "OcrLanguage": "ita"  // Italian
  }
}
```

3. **Manual text extraction** (last resort):
- Admin can upload text file separately
- Link to PDF in database

---

## 🗂️ Indexing Failures

### Issue 8: "text_extraction_required"

**Symptoms**:
```json
{
  "error": "PDF text extraction required. Please extract text before indexing."
}
```

**Root Cause**: Trying to index before extraction completes

**Debug**:
```bash
# Check processing status
curl http://localhost:8080/api/v1/pdfs/{id}/text | jq '.processingStatus'
# Must be "completed" before indexing
```

**Solution**: Wait for extraction to complete, then retry indexing

---

### Issue 9: "Qdrant indexing failed: Connection refused"

**Symptoms**:
```json
{
  "error": "Qdrant indexing failed: Connection refused"
}
```

**Root Cause**: Qdrant service not running or unreachable

**Debug**:
```bash
# Check Qdrant health
curl http://localhost:6333/healthz

# Check Docker container
docker compose ps qdrant

# Check network connectivity
docker compose exec api ping qdrant -c 3
```

**Solutions**:

1. **Start Qdrant**:
```bash
docker compose up -d qdrant
# Wait 10s for startup
curl http://localhost:6333/healthz
```

2. **Check URL configuration**:
```bash
# Verify QDRANT_URL environment variable
echo $QDRANT_URL
# Expected: http://qdrant:6333 (Docker) or http://localhost:6333 (local)
```

3. **Reset Qdrant collection** (if corrupted):
```bash
curl -X DELETE http://localhost:6333/collections/meepleai-rules
# Re-index all PDFs
```

---

### Issue 10: "Embedding generation failed"

**Symptoms**:
```json
{
  "error": "Embedding generation failed: Connection timeout"
}
```

**Root Cause**: Embedding service down or slow

**Debug**:
```bash
# Check embedding service
curl http://localhost:8003/health

# Check logs
docker compose logs embedding-service --tail=50
```

**Solutions**:

1. **Restart embedding service**:
```bash
docker compose restart embedding-service
# Wait 20s for model loading
```

2. **Increase timeout**:
```json
"EmbeddingService": {
  "TimeoutSeconds": 60  // Increase from default 30s
}
```

3. **Check model availability**:
```bash
docker compose exec embedding-service ls -la /models
# Verify model files present (~1 GB)
```

---

## 🚀 Performance Issues

### Issue 11: Extraction taking >10 seconds

**Symptoms**:
- Processing completes but takes 10-30 seconds
- Users complain about slow uploads

**Debug**:
```bash
# Check which stage is used
curl http://localhost:8080/api/v1/pdfs/{id}/text | jq '.processingStage'

# Check stage timings (Seq logs)
# Search for: "[RequestId] Stage X (StageName) succeeded in {DurationMs}ms"
```

**Solutions by Stage**:

**Stage 1 (Unstructured) slow**:
```json
"Unstructured": {
  "Strategy": "fast"  // Switch from "hi_res"
}
```
Expected: 1.3s → 0.3s

**Stage 2 (SmolDocling) slow**:
```json
"SmolDocling": {
  "Device": "cuda"  // Switch from "cpu"
}
```
Expected: 3-5s/page → 0.5s/page (requires GPU)

**Stage 3 (Docnet) slow**:
- Docnet is already fast (~1s)
- If slow: Check Tesseract OCR is not running on every page
- Disable OCR if PDF has text layer: `EnableOcr=false`

---

### Issue 12: High memory usage (>2 GB)

**Symptoms**:
- API container using >2 GB RAM
- OOM (Out of Memory) crashes

**Root Cause**: Large PDFs loaded into memory (`byte[]` for retries)

**Debug**:
```bash
# Check PDF sizes being processed
curl http://localhost:8080/api/v1/games/{gameId}/pdfs | jq '.pdfs[] | {fileName, fileSize}'

# Check for PDFs >50 MB
```

**Solutions**:

1. **Limit upload size** (short-term):
```json
"PdfProcessing": {
  "MaxFileSizeBytes": 52428800  // 50 MB max
}
```

2. **Implement streaming optimization** (BGAI-017, long-term):
```csharp
// Use temp file for large PDFs instead of byte[]
if (pdfStream.Length > 50_000_000)
{
    var tempFile = Path.GetTempFileName();
    await using var fileStream = File.Create(tempFile);
    await pdfStream.CopyToAsync(fileStream, ct);
    // Process from temp file, delete after
}
```

3. **Increase container memory**:
```yaml
services:
  api:
    deploy:
      resources:
        limits:
          memory: 4G  // Increase from 2G
```

---

## 🔍 Debugging Procedures

### Enable Detailed Logging

**appsettings.Development.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Api.BoundedContexts.DocumentProcessing": "Debug",
      "Api.Services.Pdf": "Debug",
      "Default": "Information"
    }
  },
  "Seq": {
    "ServerUrl": "http://localhost:8081",
    "ApiKey": ""
  }
}
```

**Restart API**:
```bash
dotnet run --environment Development
```

**View Logs** (Seq):
```
Navigate: http://localhost:8081
Filter: RequestId = "<guid-from-upload-response>"
```

---

### Trace Request Flow

**Step 1: Find Request ID**

Upload response includes document ID, use it to find logs:

```
Seq Query: @RequestId = "<document-id>" OR PdfId = "<document-id>"
```

**Step 2: Analyze Log Timeline**

Expected log sequence:
```
1. [RequestId] Starting 3-stage PDF extraction pipeline
2. [RequestId] Attempting Stage 1 (Unstructured) - Quality threshold: 0.80
3. [RequestId] Stage 1 (Unstructured) succeeded in 1300ms - Quality: High (0.85 ≥ 0.80)
4. [RequestId] Pipeline completed - Stage: 1 (Unstructured), Duration: 1320ms, Pages: 24, Chars: 45678
```

**If Stage 1 fails**:
```
2. [RequestId] Attempting Stage 1 (Unstructured) - Quality threshold: 0.80
3. [RequestId] Stage 1 (Unstructured) threw exception: Connection timeout
4. [RequestId] Attempting Stage 2 (SmolDocling) - Quality threshold: 0.70
```

**Step 3: Identify Failure Point**

Look for:
- `LogError`: Exceptions (red in Seq)
- `LogWarning`: Fallbacks, quality issues (yellow)
- `LogInformation`: Normal flow (white)

---

### Test Individual Stages

**Test Stage 1 (Unstructured) Directly**:
```bash
curl -X POST http://localhost:8001/api/v1/extract \
  -F "file=@test.pdf" \
  -F "strategy=fast"
```

**Expected Response**:
```json
{
  "success": true,
  "text": "# Game Title\n\nRules...",
  "page_count": 24,
  "character_count": 45678,
  "quality_score": 0.85,
  "processing_time_ms": 1300
}
```

**Test Stage 2 (SmolDocling) Directly**:
```bash
curl -X POST http://localhost:8002/api/v1/extract \
  -F "file=@test.pdf"
```

**Test Stage 3 (Docnet) via C#**:
```csharp
// In test or dev console
var extractor = new DocnetPdfTextExtractor(logger);
await using var stream = File.OpenRead("test.pdf");
var result = await extractor.ExtractTextAsync(stream, enableOcrFallback: true);
Console.WriteLine($"Success: {result.Success}, Chars: {result.CharacterCount}");
```

---

## 🐛 Common Error Patterns

### Error: "System.Text.Json.JsonException: The JSON value could not be converted"

**Root Cause**: Unstructured API response schema mismatch

**Debug**:
```bash
# Get raw API response
curl -X POST http://localhost:8001/api/v1/extract \
  -F "file=@test.pdf" \
  -o response.json

# Check schema matches UnstructuredModels.cs
cat response.json | jq '.'
```

**Fix**: Update `UnstructuredModels.cs` to match API response

---

### Error: "HttpRequestException: Connection refused (unstructured-service:8001)"

**Root Cause**: Unstructured service not running or wrong URL

**Debug**:
```bash
# Check service is running
docker compose ps | grep unstructured

# Check DNS resolution
docker compose exec api nslookup unstructured-service
# Expected: Resolves to container IP

# Check port is exposed
docker compose exec api nc -zv unstructured-service 8001
```

**Fix**:
```bash
# Restart service
docker compose up -d unstructured-service

# Check logs for startup errors
docker compose logs unstructured-service
```

---

### Error: "TaskCanceledException: A task was canceled"

**Root Cause**: CancellationToken triggered (timeout or user cancellation)

**Debug**:
```bash
# Check configured timeout
cat appsettings.json | jq '.PdfProcessing.Extractor.TimeoutSeconds'

# Check actual processing time (Seq logs)
# Search for: "Stage X succeeded in {DurationMs}ms"
```

**Fix**:
- Increase timeout if PDF is large (>50 pages)
- Check if user cancelled manually (DELETE /processing endpoint)

---

## 🔧 Italian Language Issues

### Issue 13: Italian special characters garbled (à → �)

**Symptoms**:
- Extracted text shows `�` instead of `à`, `è`, `ì`, `ò`, `ù`

**Root Cause**: Encoding issue (not UTF-8)

**Debug**:
```bash
# Check PDF encoding
pdffonts test.pdf | grep "Encoding"

# Check extracted text encoding
curl http://localhost:8080/api/v1/pdfs/{id}/text | jq -r '.extractedText' | file -
# Expected: "UTF-8 Unicode text"
```

**Solutions**:

1. **Verify Tesseract Italian** (Stage 3 OCR):
```bash
docker compose exec api tesseract --list-langs
# Must show: ita
```

2. **Install if missing**:
```dockerfile
# Dockerfile (api)
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    tesseract-ocr-ita \
    && rm -rf /var/lib/apt/lists/*
```

3. **Check Unstructured language config**:
```python
# apps/unstructured-service/src/application/pdf_extraction_service.py
elements = partition_pdf(
    filename=pdf_path,
    languages=["ita"],  # Italian language
    strategy=strategy
)
```

---

## 📊 Performance Tuning

### Optimization 1: Reduce Fallback Rate

**Goal**: Increase Stage 1 success rate from 80% → 90%+

**Actions**:

1. **Lower quality threshold**:
```json
"Quality": {
  "MinimumThreshold": 0.75,  // From 0.80
  "MinCharsPerPage": 450      // From 500
}
```

2. **Use hi_res strategy**:
```json
"Unstructured": {
  "Strategy": "hi_res"  // +5% quality, 3x slower
}
```

3. **Monitor impact**:
```sql
SELECT
  processing_stage,
  COUNT(*) * 100.0 / SUM(COUNT(*)) OVER () as percentage
FROM pdf_documents
WHERE processed_at > NOW() - INTERVAL '7 days'
GROUP BY processing_stage;
```

**Target**: Stage 1 ≥85%

---

### Optimization 2: Speed Up Stage 2

**Goal**: Reduce Stage 2 latency from 3-5s → <1s

**Actions**:

1. **Enable GPU** (requires NVIDIA GPU):
```yaml
smoldocling-service:
  deploy:
    resources:
      reservations:
        devices:
          - driver: nvidia
            capabilities: [gpu]
  environment:
    - DEVICE=cuda
```

2. **Pre-warm model** (load on startup, not first request):
```python
# apps/smoldocling-service/main.py
@app.on_event("startup")
async def warmup():
    # Load model on startup
    model.load()
```

3. **Verify GPU usage**:
```bash
# Inside container
nvidia-smi
# Expected: GPU memory usage >1 GB when processing
```

---

### Optimization 3: Concurrent Processing

**Goal**: Process multiple PDFs simultaneously

**Current Limit**: 3 concurrent (Unstructured service workers)

**Actions**:

1. **Increase worker count**:
```yaml
# docker-compose.yml
unstructured-service:
  environment:
    - MAX_WORKERS=10  # From 3
```

2. **Horizontal scaling** (Phase 2):
```yaml
unstructured-service:
  deploy:
    replicas: 3  # 3 containers behind load balancer
```

3. **Monitor queue depth**:
```sql
SELECT COUNT(*) FROM pdf_documents WHERE processing_status = 'pending';
-- If queue >10: Increase workers or add replicas
```

---

## 🧹 Maintenance Procedures

### Clear Stuck Processing Jobs

```sql
-- Find PDFs stuck in "processing" >1 hour
UPDATE pdf_documents
SET processing_status = 'failed',
    processing_error = 'Processing timeout - stuck >1 hour'
WHERE processing_status = 'processing'
  AND uploaded_at < NOW() - INTERVAL '1 hour';
```

### Clean Up Failed PDFs

```bash
# Delete PDFs with failed status >7 days old
curl -X DELETE http://localhost:8080/api/v1/admin/pdfs/cleanup \
  -b cookies.txt \
  -d '{"olderThan":"7d","status":"failed"}'
```

### Rebuild Qdrant Index

```bash
# 1. Backup current index (optional)
curl http://localhost:6333/collections/meepleai-rules/snapshots

# 2. Delete collection
curl -X DELETE http://localhost:6333/collections/meepleai-rules

# 3. Re-index all completed PDFs
for pdf_id in $(curl http://localhost:8080/api/v1/admin/pdfs?status=completed | jq -r '.pdfs[].id'); do
  curl -X POST http://localhost:8080/api/v1/ingest/pdf/$pdf_id/index -b cookies.txt
  sleep 2
done
```

---

## 📞 Escalation

### When to Escalate

Escalate to senior engineer if:
- All 3 stages consistently fail (>10% of uploads)
- Memory usage growing unbounded (possible leak)
- Quality scores dropping over time (model degradation?)
- Security vulnerability suspected (file upload exploit)

### Debug Information to Provide

```bash
# 1. Environment info
dotnet --version
docker --version
cat /etc/os-release

# 2. Service status
docker compose ps
curl http://localhost:8001/health
curl http://localhost:8002/health
curl http://localhost:6333/healthz

# 3. Recent errors (last 24h)
# Seq query: @Level = 'Error' AND @Timestamp > Now()-1d

# 4. Performance metrics
# Grafana dashboard: PDF Processing Overview
```

---

## 📚 References

- [DocumentProcessing README](../../../apps/api/src/Api/BoundedContexts/DocumentProcessing/README.md)
- [PDF Processing API](../../03-api/pdf-processing-api.md)
- [Configuration Guide](./pdf-processing-configuration.md)
- [Code Review Checklist](../code-review/pdf-processing-checklist.md)
- [ADR-003b: Unstructured PDF](../../01-architecture/adr/adr-003b-unstructured-pdf.md)

---

**Version**: 1.0
**Last Updated**: 2025-11-13
**Maintainer**: Backend Team

---
name: Implement Batch Embedding Processing for Large PDFs
about: Fix OutOfMemoryException during PDF indexing by implementing batch processing
title: '[PDF-PROCESSING] Implement batch embedding generation to support large PDFs'
labels: enhancement, pdf-processing, performance, memory
assignees: ''
---

## Problem Statement

PDF indexing fails with `OutOfMemoryException` for PDFs larger than 10MB, even with 8GB container memory limit.

**Current Behavior**:
- All document chunks processed simultaneously
- All embeddings loaded into memory at once
- OOM occurs during embedding generation step
- Tested with 2GB, 4GB, 8GB - all fail for 12MB PDF

**Error**:
```
Exception of type 'System.OutOfMemoryException' was thrown.
```

## Root Cause Analysis

**File**: `UploadPdfCommandHandler.cs` lines 863-864

```csharp
// CURRENT IMPLEMENTATION (loads all in memory):
var texts = allDocumentChunks.Select(c => c.Text).ToList();
var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts);
```

**Memory Profile** (12MB PDF, ~50 chunks):
- Full PDF file: 12MB
- Extracted text: ~26KB
- All chunks in memory: ~120KB
- **Embedding vectors**: ~2-3GB (all at once)
- .NET heap overhead: ~1GB
- **Total Peak**: >8GB

## Proposed Solution

### Batch Processing Implementation

```csharp
// PROPOSED: Process in batches of 20 chunks
const int BATCH_SIZE = 20;
var allEmbeddings = new List<float[]>();

foreach (var batch in allDocumentChunks.Chunk(BATCH_SIZE))
{
    var batchTexts = batch.Select(c => c.Text).ToList();
    var batchResult = await embeddingService.GenerateEmbeddingsAsync(batchTexts);

    if (!batchResult.Success)
    {
        // Handle batch failure
        continue;
    }

    allEmbeddings.AddRange(batchResult.Embeddings);

    // Optional: Progressive indexing
    await IndexBatchInQdrant(batch, batchResult.Embeddings);

    // Force GC between batches to release memory
    GC.Collect();
    GC.WaitForPendingFinalizers();
}
```

**Expected Memory Reduction**:
- Peak memory: 8GB → 2-3GB
- Enables processing of PDFs up to 50MB with 4GB limit

## Acceptance Criteria

- [ ] PDF indexing succeeds for 12MB PDF with 4GB memory
- [ ] PDF indexing succeeds for 20MB PDF with 4GB memory
- [ ] PDF indexing succeeds for 50MB PDF with 8GB memory
- [ ] Memory usage stays below 80% of container limit during processing
- [ ] Processing time increases linearly with PDF size (not exponentially)
- [ ] Existing PDFs < 10MB continue to work
- [ ] Progress tracking updated per batch (not just at end)

## Implementation Plan

### Phase 1: Core Batch Processing (2-3 hours)

1. **Modify `GenerateAndValidateEmbeddingsAsync`**:
   - Add `BATCH_SIZE` constant (start with 20)
   - Implement chunk batching loop
   - Collect results across batches

2. **Update Progress Tracking**:
   - Report progress per batch (currently per-step)
   - Update `ProcessingProgress.PercentComplete` incrementally

3. **Error Handling**:
   - Handle per-batch failures gracefully
   - Allow partial success (some batches succeed)
   - Log which batches failed for debugging

### Phase 2: Progressive Indexing (1-2 hours)

1. **Index per batch instead of at end**:
   - Call `IndexInVectorStoreAsync` per batch
   - Reduces memory footprint further
   - Enables resume on failure

2. **Chunking Optimization**:
   - Consider splitting `ChunkExtractedTextAsync` into batches
   - Stream chunks instead of materializing all at once

### Phase 3: Testing & Validation (1 hour)

1. **Test Matrix**:
   ```
   | PDF Size | Chunks | Memory Limit | Expected Result |
   |----------|--------|--------------|-----------------|
   | 5MB      | ~25    | 2GB          | ✅ Success      |
   | 12MB     | ~50    | 4GB          | ✅ Success      |
   | 20MB     | ~85    | 4GB          | ✅ Success      |
   | 50MB     | ~210   | 8GB          | ✅ Success      |
   ```

2. **Performance Benchmarks**:
   - Measure time per batch
   - Verify memory stays < 80% limit
   - Check Qdrant indexing performance

## Technical Notes

### Memory Calculation

```
Batch Size: 20 chunks
Chunk Size: 512 chars average
Embedding Dimensions: 1024 (float32)

Per-Batch Memory:
- Text: 20 * 512 = 10KB
- Embeddings: 20 * 1024 * 4 bytes = 80KB
- Overhead: ~500MB (.NET + libs)

Total Peak: ~1GB per batch (vs 8GB+ currently)
```

### Alternative Approaches Considered

1. **External Embedding Service** (attempted):
   - Configured `EMBEDDING_PROVIDER=external`
   - Still OOM because API collects all results
   - Requires batch processing anyway

2. **Streaming Processing**:
   - More complex implementation
   - Better for PDFs > 100MB
   - Consider for Phase 4

## Testing

### Manual Test Procedure

```bash
# 1. Upload test PDF (20MB)
curl -X POST http://localhost:8080/api/v1/ingest/pdf \
  -H "Cookie: session_token" \
  -F "file=@test_20mb.pdf" \
  -F "gameId=<game-id>" \
  -F "language=en"

# 2. Monitor processing
docker compose logs -f api | grep "PDF-DEBUG\|Batch"

# 3. Verify completion
psql -c "SELECT ProcessingStatus, PageCount FROM pdf_documents WHERE Id='<pdf-id>'"

# 4. Check Qdrant vectors
curl http://localhost:6333/collections/meepleai_documents/points/count
```

### Automated Tests

Add integration test:
```csharp
[Fact]
public async Task IndexPdf_With20MBFile_ShouldSucceedWithBatchProcessing()
{
    // Arrange: Create 20MB test PDF
    // Act: Upload and index
    // Assert: ProcessingStatus = "completed", ChunkCount > 0
}
```

## References

- Original Investigation: `docs/claudedocs/pdf-processing-debug-session.md`
- Related Commit: 70a2d4de9
- EF Core Tracking: https://learn.microsoft.com/en-us/ef/core/querying/tracking
- Memory Profiling: Prometheus metrics at `/metrics`

## Estimated Effort

- **Development**: 4-6 hours
- **Testing**: 2 hours
- **Documentation**: 1 hour

**Total**: 7-9 hours

**Priority**: High (blocks PDF processing for production use)

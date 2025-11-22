# Issue #4: Incremental Re-indexing

**Priority**: 🟡 High
**Category**: Cost Optimization
**Effort**: 12-16 hours
**Impact**: High - 90% cost reduction on re-index

---

## 📋 Problem

Current behavior: **Re-indexing a PDF re-generates ALL embeddings**, even if text unchanged:

```csharp
// File: IndexPdfCommandHandler.cs (line 86-95)
if (existingVectorDoc != null)
{
    _logger.LogInformation("PDF already indexed, deleting old vectors");

    // ❌ DELETE ALL vectors
    await _qdrantService.DeleteDocumentAsync(pdfId, ct);

    // ❌ RE-GENERATE ALL embeddings (even if unchanged!)
    var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts, ct);
}
```

**Waste**: Re-indexing 100-page PDF = 108 embeddings × $0.000001 = $0.11 (OpenAI)

---

## 🎯 Solution

**Incremental re-indexing**: Only re-generate embeddings for **modified chunks**.

```csharp
// Proposed: Differential update

1. Compute hash for each chunk (SHA256 of text)
2. Compare with stored hashes from previous index
3. Identify modified/added/deleted chunks
4. Re-generate embeddings ONLY for changed chunks
5. Update Qdrant with differential changes
```

**Savings**: If PDF unchanged → 0 embeddings generated (100% saving!)

---

## 🛠️ Implementation

### 1. Extend VectorDocumentEntity
```csharp
// File: Infrastructure/Entities/VectorDocumentEntity.cs

public class VectorDocumentEntity
{
    // ... existing properties ...

    /// <summary>
    /// JSON array of chunk hashes (SHA256) for differential indexing
    /// </summary>
    [Column("chunk_hashes")]
    public string? ChunkHashes { get; set; } // JSON: ["hash1", "hash2", ...]

    /// <summary>
    /// Get chunk hashes as list
    /// </summary>
    public List<string> GetChunkHashes()
    {
        if (string.IsNullOrWhiteSpace(ChunkHashes))
            return new List<string>();

        return JsonSerializer.Deserialize<List<string>>(ChunkHashes) ?? new List<string>();
    }

    /// <summary>
    /// Set chunk hashes from list
    /// </summary>
    public void SetChunkHashes(List<string> hashes)
    {
        ChunkHashes = JsonSerializer.Serialize(hashes);
    }
}
```

### 2. Chunk Hashing Utility
```csharp
// File: Services/ChunkHashingService.cs

public class ChunkHashingService
{
    public string ComputeChunkHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text.Trim().ToLowerInvariant());
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public List<int> FindModifiedChunks(List<string> oldHashes, List<string> newHashes)
    {
        var modifiedIndexes = new List<int>();

        for (int i = 0; i < newHashes.Count; i++)
        {
            // New chunk added (beyond old length)
            if (i >= oldHashes.Count)
            {
                modifiedIndexes.Add(i);
                continue;
            }

            // Chunk modified (hash mismatch)
            if (oldHashes[i] != newHashes[i])
            {
                modifiedIndexes.Add(i);
            }
        }

        return modifiedIndexes;
    }
}
```

### 3. Update IndexPdfCommandHandler
```csharp
// File: BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfCommandHandler.cs

public async Task<IndexingResultDto> Handle(IndexPdfCommand command, CancellationToken ct)
{
    // ... existing code: load PDF, chunk text ...

    // 3. Compute hashes for new chunks
    var newHashes = textChunks.Select(c => _hashingService.ComputeChunkHash(c.Text)).ToList();

    // 4. Check if already indexed
    if (existingVectorDoc != null)
    {
        var oldHashes = existingVectorDoc.GetChunkHashes();

        // 5. Find modified chunks
        var modifiedIndexes = _hashingService.FindModifiedChunks(oldHashes, newHashes);

        if (modifiedIndexes.Count == 0)
        {
            _logger.LogInformation("No chunks modified, skipping re-index");
            return IndexingResultDto.CreateSuccess(existingVectorDoc.Id.ToString(), 0);
        }

        _logger.LogInformation("Re-indexing {Count} modified chunks (out of {Total})",
            modifiedIndexes.Count, textChunks.Count);

        // 6. Extract only modified chunks
        var modifiedChunks = modifiedIndexes.Select(i => textChunks[i]).ToList();
        var modifiedTexts = modifiedChunks.Select(c => c.Text).ToList();

        // 7. Generate embeddings ONLY for modified chunks
        var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(modifiedTexts, ct);

        // 8. Update Qdrant (upsert modified vectors)
        var points = modifiedIndexes.Select((index, i) => new
        {
            Id = $"{pdfId}_{index}",
            Vector = embeddingResult.Embeddings[i],
            Chunk = modifiedChunks[i]
        }).ToList();

        await _qdrantService.UpsertVectorsAsync(pdfId, points, ct);

        // 9. Save updated hashes
        existingVectorDoc.SetChunkHashes(newHashes);
        await _db.SaveChangesAsync(ct);

        return IndexingResultDto.CreateSuccess(existingVectorDoc.Id.ToString(), modifiedIndexes.Count);
    }
    else
    {
        // First-time indexing: generate all embeddings
        var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(
            textChunks.Select(c => c.Text).ToList(), ct);

        // ... existing indexing logic ...

        // Save chunk hashes
        existingVectorDoc.SetChunkHashes(newHashes);
        await _db.SaveChangesAsync(ct);
    }
}
```

### 4. Qdrant Upsert Support
```csharp
// File: Services/IQdrantService.cs
public interface IQdrantService
{
    // ... existing methods ...

    /// <summary>
    /// Upsert (update or insert) specific vectors by ID
    /// </summary>
    Task<bool> UpsertVectorsAsync(
        string pdfId,
        List<QdrantPoint> points,
        CancellationToken ct = default);
}

public record QdrantPoint
{
    public string Id { get; init; } = string.Empty;
    public float[] Vector { get; init; } = Array.Empty<float>();
    public TextChunk Chunk { get; init; } = null!;
}
```

---

## ✅ Acceptance Criteria

1. **Unchanged PDF**
   - [ ] Re-index generates 0 embeddings
   - [ ] Returns success immediately (<1s)
   - [ ] Chunk hashes stored correctly

2. **Partially Modified PDF**
   - [ ] Only modified chunks re-indexed
   - [ ] Chunk hashes updated for modified chunks only
   - [ ] Qdrant vectors upserted (not full delete)

3. **Fully Modified PDF**
   - [ ] All chunks re-indexed (fallback to full re-index)
   - [ ] Performance similar to first-time index

4. **Edge Cases**
   - [ ] New chunks added (hash list grows)
   - [ ] Chunks deleted (hash list shrinks)
   - [ ] Chunk order changed (detected as modification)

---

## 🧪 Testing

```csharp
[Fact]
public async Task UnchangedPdf_SkipsReindex()
{
    // Arrange: Index PDF first time
    var pdfId = await IndexPdf("rulebook.pdf");

    // Act: Re-index same PDF (no changes)
    var result = await ReIndexPdf(pdfId);

    // Assert
    Assert.True(result.Success);
    Assert.Equal(0, result.ChunksIndexed); // ✅ No embeddings generated
}

[Fact]
public async Task PartiallyModifiedPdf_ReindexesOnlyChanges()
{
    // Arrange: Index PDF
    var pdfId = await IndexPdf("rulebook-v1.pdf");

    // Act: Modify 10% of PDF, re-index
    var modifiedPdf = ModifyChunks("rulebook-v1.pdf", modifyPercentage: 0.1);
    var result = await ReIndexPdf(pdfId, modifiedPdf);

    // Assert
    Assert.InRange(result.ChunksIndexed, 8, 12); // ~10% of 100 chunks
}
```

---

## 📈 Success Metrics

| Scenario | Embeddings Generated | Savings |
|----------|----------------------|---------|
| **Unchanged PDF** | 0 (was 100) | **100%** |
| **10% modified** | 10 (was 100) | **90%** |
| **50% modified** | 50 (was 100) | **50%** |

**Realistic Estimate**: Average 90% reduction (most re-indexes are minor corrections)

---

**Created**: 2025-11-22
**Estimated Effort**: 12-16 hours

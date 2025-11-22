# Issue #3: Chunking Optimization

**Priority**: 🟡 High
**Category**: Cost & Storage Optimization
**Effort**: 8-10 hours
**Impact**: Medium - 30% fewer embeddings

---

## 📋 Problem

Current chunking strategy creates **many small chunks**, leading to:
- More embeddings generated per PDF
- Higher storage costs in Qdrant
- Slower similarity search (more vectors to compare)

```csharp
// Current: ChunkingConstants.cs
DefaultChunkSize = 512 char    // Small chunks
DefaultOverlap = 50 char

// Example PDF (100 pages = 50K characters):
// 50,000 / (512 - 50) = 108 chunks = 108 embeddings ❌
```

---

## 🎯 Solution

**Increase chunk size** to reduce total embeddings while maintaining semantic quality:

```csharp
// Proposed: Optimized chunking
DefaultChunkSize = 768 char    // +50% larger
DefaultOverlap = 100 char      // Maintain overlap %

// Same 100-page PDF:
// 50,000 / (768 - 100) = 75 chunks = 75 embeddings ✅
// Reduction: 108 → 75 = -31% embeddings!
```

**Key Insight**: Embedding models (nomic-embed-text, text-embedding-3-small) support up to **8192 tokens** (~32K characters), so 768 char chunks are still well within limits.

---

## 💰 Benefits

| Metric | Before (512 char) | After (768 char) | Improvement |
|--------|-------------------|------------------|-------------|
| **Chunks per PDF** | 108 | 75 | **-31%** |
| **Embeddings per PDF** | 108 | 75 | **-31%** |
| **Qdrant Storage** | 108 vectors | 75 vectors | **-31%** |
| **Indexing Time** | 10.8s (100ms/emb) | 7.5s | **-30%** |
| **Search Latency** | 108 comparisons | 75 comparisons | **-30%** |

**Cost Impact** (OpenAI):
- 500 PDF/month × 33 fewer embeddings = 16,500 fewer embeddings
- 16,500 × 500 tokens × $0.000002/token = **-$16.50/mese** savings

---

## 🛠️ Implementation

### 1. Update ChunkingConstants
```csharp
// File: Constants/ChunkingConstants.cs
public static class ChunkingConstants
{
    // BEFORE
    // public const int DefaultChunkSize = 512;
    // public const int DefaultOverlap = 50;

    // AFTER
    public const int DefaultChunkSize = 768;  // +50%
    public const int DefaultOverlap = 100;    // +100% (maintain ~13% overlap)

    public const int MinChunkSize = 384;      // Increased from 256
    public const int MaxChunkSize = 1024;     // Increased from 768
}
```

### 2. Add Configuration Override
```json
// File: appsettings.json
{
  "Chunking": {
    "DefaultChunkSize": 768,
    "DefaultOverlap": 100,
    "AdaptiveSizing": true  // Use sentence boundaries (already implemented)
  }
}
```

### 3. Update TextChunkingService (if needed)
```csharp
// File: Services/TextChunkingService.cs
// Ensure adaptive chunking still works with larger chunks

// No code changes needed - existing adaptive logic handles larger chunks! ✅
```

---

## 🧪 Testing Strategy

### 1. Accuracy Validation (CRITICAL)
```bash
# Test: Does larger chunk size maintain >95% RAG accuracy?

# Script: test-chunk-size-accuracy.sh
#!/bin/bash

# Test with current (512 char) chunks
dotnet test --filter "RagAccuracyTests" > results_512.txt

# Update to new (768 char) chunks
sed -i 's/DefaultChunkSize = 512/DefaultChunkSize = 768/' Constants/ChunkingConstants.cs

# Re-test with new chunks
dotnet test --filter "RagAccuracyTests" > results_768.txt

# Compare accuracy
# Acceptance: Accuracy drop <2% (e.g., 96% → 94.5% OK)
```

### 2. Performance Benchmark
```csharp
[Fact]
public async Task LargerChunks_ReduceTotalEmbeddings()
{
    // Arrange
    var text = LoadTestPdf("100-page-rulebook.txt"); // 50K chars
    var service512 = new TextChunkingService(chunkSize: 512, overlap: 50);
    var service768 = new TextChunkingService(chunkSize: 768, overlap: 100);

    // Act
    var chunks512 = service512.ChunkText(text);
    var chunks768 = service768.ChunkText(text);

    // Assert
    Assert.Equal(108, chunks512.Count);
    Assert.Equal(75, chunks768.Count);
    Assert.InRange(chunks768.Count, chunks512.Count * 0.6, chunks512.Count * 0.8); // 20-40% reduction
}
```

### 3. Retrieval Quality Test
```csharp
[Fact]
public async Task LargerChunks_MaintainRetrievalQuality()
{
    // Test P@10 (Precision at 10) with both chunk sizes
    // Acceptance: P@10 drop <5%

    var queries = LoadTestQueries(); // 100 test queries
    var groundTruth = LoadGroundTruth();

    // Index with 512 char chunks
    await IndexPdfWithChunkSize(512);
    var p10_512 = await EvaluatePrecisionAt10(queries, groundTruth);

    // Re-index with 768 char chunks
    await IndexPdfWithChunkSize(768);
    var p10_768 = await EvaluatePrecisionAt10(queries, groundTruth);

    // Assert: quality maintained
    Assert.InRange(p10_768, p10_512 - 0.05, p10_512); // Max 5% drop
}
```

---

## ✅ Acceptance Criteria

1. **Chunk Reduction**
   - [ ] 100-page PDF generates 30-35% fewer chunks
   - [ ] Verified with real rulebook PDFs

2. **Quality Maintenance**
   - [ ] RAG accuracy >95% (drop <2% from baseline)
   - [ ] P@10 metric >0.80 (drop <5%)
   - [ ] MRR (Mean Reciprocal Rank) >0.75

3. **Performance**
   - [ ] PDF indexing 25-35% faster
   - [ ] Search latency reduced by 20-30%

4. **Storage**
   - [ ] Qdrant storage reduced by 25-35%
   - [ ] Measured on production dataset (500 PDFs)

5. **Configuration**
   - [ ] Chunk size configurable via appsettings.json
   - [ ] Can override per-environment (dev/staging/prod)

---

## 📊 Validation Plan

### Phase 1: Staging Testing (Week 1)
```bash
# Deploy with new chunk size to staging
# Monitor metrics for 1 week:
# - P@10 accuracy
# - User feedback
# - Search quality issues

# Acceptance:
# - P@10 ≥0.80
# - <5 user complaints about relevance
```

### Phase 2: A/B Test (Week 2)
```bash
# Split production traffic:
# - 50% → 512 char chunks (baseline)
# - 50% → 768 char chunks (new)

# Compare metrics:
# - Accuracy (P@10, MRR)
# - Latency (P95)
# - User satisfaction (CTR on results)

# Decision criteria:
# - If accuracy drop >5%: rollback
# - If accuracy drop 2-5%: investigate further
# - If accuracy drop <2%: proceed to full rollout
```

### Phase 3: Full Rollout (Week 3)
```bash
# Gradual rollout:
# - Day 1: 25% traffic
# - Day 3: 50% traffic
# - Day 5: 75% traffic
# - Day 7: 100% traffic

# Monitor closely for accuracy regression
```

---

## 📈 Success Metrics

- **Embeddings Reduction**: -30% ✅
- **Accuracy Maintained**: >95% ✅
- **Storage Savings**: -30% Qdrant vectors
- **Cost Savings**: $15-20/mese (if using OpenAI)

---

## ⚠️ Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Accuracy drops >5%** | High | A/B test, easy rollback to 512 |
| **Larger chunks lose context boundaries** | Medium | Adaptive chunking uses sentence breaks |
| **Search relevance degrades** | High | Monitor P@10, MRR, user feedback |

---

**Created**: 2025-11-22
**Estimated Effort**: 8-10 hours
**Priority**: 🟡 High

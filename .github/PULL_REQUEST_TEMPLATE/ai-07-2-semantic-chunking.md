# AI-07.2: Adaptive Semantic Chunking for PDF Processing

## Summary

Replaces fixed-size chunking (512 chars) with adaptive semantic chunking that respects sentence/paragraph boundaries and preserves context, improving retrieval quality by +10-15%.

**Technical Design**: [docs/technic/ai-07-rag-optimization-phase1.md](../docs/technic/ai-07-rag-optimization-phase1.md#3-optimization-2-adaptive-semantic-chunking)

## Related Issue

Closes #469

## Type of Change

- [x] New feature (non-breaking change which adds functionality)
- [x] Performance improvement
- [ ] Breaking change (requires PDF re-indexing migration)
- [ ] Configuration/infrastructure change

## Changes Made

### Core Implementation

- [ ] Created `IChunkingStrategy` interface with `ChunkText(text, config)` method
- [ ] Implemented `SemanticChunkingStrategy` with recursive splitting:
  - [ ] `SplitByParagraphs()` - Try paragraphs first (\n\n)
  - [ ] `SplitBySentences()` - Fallback to sentences (.\s)
  - [ ] `SplitByClauses()` - Fallback to clauses ([,;:]\s)
  - [ ] `SplitByWords()` - Final fallback (word boundaries)
- [ ] Implemented `PreserveLists()` - Keep numbered/bulleted lists together
- [ ] Implemented `AddContextualOverlap()` - Prepend last sentence from previous chunk
- [ ] Refactored `FixedSizeChunkingStrategy` to implement `IChunkingStrategy`
- [ ] Updated `TextChunkingService` to use strategy pattern
- [ ] Added DI registration in `Program.cs` (strategy selection via config)

### Configuration

- [ ] Added `TextChunking` section to `appsettings.json`:
  - [ ] `Strategy`: "Semantic" or "Fixed" (backward compatibility)
  - [ ] `TargetChunkSize`: 800 (increased from 512)
  - [ ] `MinChunkSize`: 400 (prevent tiny orphan chunks)
  - [ ] `MaxChunkSize`: 1200 (hard limit for edge cases)
  - [ ] `OverlapTokens`: 100 (~1-2 sentences)
  - [ ] `PreserveBoundaries`: Paragraphs, Sentences, Lists, Tables
  - [ ] `SplitPriority`: [paragraph, sentence, clause, word]

### Migration

- [ ] Created migration script `tools/reindex-pdfs.ps1`:
  - [ ] Backup current Qdrant collections
  - [ ] Soft delete old chunks (set `is_active=false`)
  - [ ] Re-run PDF processing pipeline with new chunking
  - [ ] Validate chunk count and quality
  - [ ] Run RAG evaluation to compare metrics
- [ ] Created migration documentation `docs/migrations/ai-07-2-semantic-chunking.md`
- [ ] Implemented rollback capability (toggle `is_active` back to old chunks)

## Testing

### Test Coverage

- [x] Unit tests added/updated (`SemanticChunkingStrategyTests.cs`)
  - [ ] Edge cases: single-sentence paragraphs, very long sentences, no paragraph breaks
  - [ ] List preservation: numbered lists, bulleted lists, nested lists
  - [ ] Overlap validation: last sentence extraction, prepending logic
  - [ ] Size constraints: min/max chunk size enforcement
  - [ ] Boundary detection: paragraph/sentence/clause/word splitting
- [x] Integration tests added/updated (`TextChunkingServiceTests.cs`)
  - [ ] Real rulebook excerpts (Chess, Tic-Tac-Toe)
  - [ ] Compare chunk boundaries: fixed vs. semantic
  - [ ] Measure chunk coherence (readability scores)
  - [ ] Performance: chunking speed <500ms per page
- [x] All tests passing locally
- [x] Test names follow BDD convention

### RAG Evaluation (Post-Migration)

- [ ] Baseline evaluation (with old fixed-size chunks)
- [ ] Re-index test PDFs (Chess + Tic-Tac-Toe) with semantic chunking
- [ ] Post-migration evaluation
- [ ] Metrics comparison:
  - [ ] Precision@5 improvement: ____% (target: +10-15%)
  - [ ] Recall@10 improvement: ____% (target: +10-15%)
  - [ ] Chunk count change: ____ chunks (before) → ____ chunks (after)

**Evaluation Results**:
```
<!-- Paste RAG evaluation results (before/after migration) -->
Baseline (Fixed-Size):
  P@5: 0.XX, Recall@10: 0.XX, Chunks: XXX

After Migration (Semantic):
  P@5: 0.XX, Recall@10: 0.XX, Chunks: XXX
```

### Manual Testing

- [ ] Inspect chunk boundaries visually (no mid-sentence breaks)
- [ ] Verify numbered lists kept together (e.g., setup steps 1-10)
- [ ] Verify contextual overlap (last sentence prepended)
- [ ] Test fallback to fixed-size strategy via config
- [ ] Test performance: chunking speed <500ms per page (100-page PDF)

**Chunk Quality Examples**:
```
<!-- Paste examples of chunk boundaries (before/after) -->

BEFORE (Fixed-Size, 512 chars):
Chunk 1: "...The rook can move horizontally or vert-"
Chunk 2: "-ically any number of squares. The queen..."

AFTER (Semantic):
Chunk 1: "...The rook can move horizontally or vertically any number of squares."
Chunk 2: "The rook can move horizontally or vertically any number of squares. The queen is the most powerful piece..."
                ^^^ contextual overlap from previous chunk ^^^
```

## Acceptance Criteria (from #469)

- [ ] `IChunkingStrategy` interface created (ChunkText method)
- [ ] `SemanticChunkingStrategy` implemented with recursive splitting
- [ ] `FixedSizeChunkingStrategy` preserves current behavior
- [ ] Strategy selected via config (`ChunkingStrategy` setting)
- [ ] Unit tests cover edge cases (short docs, no paragraphs, long sentences)
- [ ] Integration test: re-index Chess rulebook, verify chunk quality
- [ ] Evaluation shows +10-15% improvement in P@5/Recall
- [ ] Migration guide for re-indexing existing PDFs

## Checklist

- [ ] Code follows project style guidelines
- [ ] Self-review of code completed
- [ ] Comments added for recursive splitting logic and overlap strategy
- [ ] Documentation updated (`docs/migrations/ai-07-2-semantic-chunking.md`)
- [ ] No new warnings introduced
- [ ] Tests added/updated and passing
- [ ] Test names follow BDD-style naming convention
- [ ] Changes are backwards compatible (feature flag for strategy selection)
- [ ] No secrets or API keys committed

## Configuration Example

**appsettings.json** (excerpt):
```json
{
  "TextChunking": {
    "Strategy": "Semantic",
    "TargetChunkSize": 800,
    "MinChunkSize": 400,
    "MaxChunkSize": 1200,
    "OverlapTokens": 100,
    "OverlapStrategy": "LastSentence",
    "PreserveBoundaries": {
      "Paragraphs": true,
      "Sentences": true,
      "Lists": true,
      "Tables": true
    },
    "SplitPriority": ["paragraph", "sentence", "clause", "word"]
  }
}
```

## Migration Guide

**Command**:
```powershell
# Dry run (validate only, no changes)
pwsh tools/reindex-pdfs.ps1 -DryRun

# Re-index all PDFs
pwsh tools/reindex-pdfs.ps1

# Re-index specific game
pwsh tools/reindex-pdfs.ps1 -GameId "chess-uuid"
```

**Expected Impact**:
- Chess (100-page PDF): ~200 fixed chunks → ~150-170 semantic chunks
- Tic-Tac-Toe (10-page PDF): ~20 fixed chunks → ~15-18 semantic chunks
- Larger, more coherent chunks improve retrieval relevance

## Performance Impact

- [ ] Chunking speed measured:
  - Fixed-size: ____ ms per page
  - Semantic: ____ ms per page (expect <20% increase)
- [ ] Memory usage: No significant increase expected
- [ ] Qdrant indexing time: ____ seconds (Chess + Tic-Tac-Toe)

**Benchmark Results**:
```
<!-- Paste performance benchmark results -->
```

## Rollback Plan

If metrics regress after migration:
1. **Immediate** (< 5 min):
   - Set `TextChunking.Strategy` to "Fixed" in `appsettings.json`
   - Restart API pods
2. **Short-term** (< 1 hour):
   - Run SQL: `UPDATE vector_documents SET is_active = false WHERE chunking_strategy = 'Semantic'`
   - Run SQL: `UPDATE vector_documents SET is_active = true WHERE chunking_strategy = 'Fixed'`
   - Clear Qdrant cache
3. **Post-mortem**:
   - Analyze chunk boundaries that caused regression
   - Fix edge cases in `SemanticChunkingStrategy`
   - Re-test before next migration attempt

## Additional Notes

<!-- Any additional context or implementation decisions -->

**Design Decisions**:
- Chose `TargetChunkSize=800` (vs. 512) to reduce total chunk count while staying within LLM context limits
- `OverlapTokens=100` (~1-2 sentences) provides sufficient context without excessive duplication
- Strategy pattern allows easy A/B testing and rollback

**Known Limitations**:
- Tables in PDFs may still be split if they exceed `MaxChunkSize=1200`
- Very long paragraphs (>1200 chars) will fallback to sentence splitting

**Next Steps** (after merge):
- Monitor RAG metrics for 1 week post-migration
- A/B test with 50% of traffic before full rollout
- Proceed to AI-07.3 (Query Expansion)

**Cost Impact**: $0/month (local processing, no additional API calls)

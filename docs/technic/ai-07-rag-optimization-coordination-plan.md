# AI-07: RAG Optimization Phase 1 - Epic Coordination Plan

**Epic Issue:** #467
**Status:** Open
**Timeline:** 2-3 weeks
**Budget:** $2-3/month (query expansion LLM calls)

---

## Executive Summary

This document provides a comprehensive coordination plan for implementing three RAG optimization techniques in parallel. The plan ensures minimal conflicts, safe rollouts, and measurable quality improvements.

**Child Issues:**
- **#468**: AI-07.1 - Advanced Prompt Engineering (3-4 days, $0/month)
- **#469**: AI-07.2 - Adaptive Semantic Chunking (4-5 days, $0/month)
- **#470**: AI-07.3 - LLM-Based Query Expansion (5-6 days, $2-3/month)

**Expected ROI:** +30-40% improvement in P@5/Recall metrics with minimal cost increase

---

## 1. Current State Analysis

### 1.1 Baseline Metrics (from AI-06 Evaluation)

| Metric | Current | Target (AI-07) | Improvement |
|--------|---------|----------------|-------------|
| **Precision@5** | 0.70 | ≥0.90 | +28% |
| **Mean Reciprocal Rank** | 0.60 | ≥0.75 | +25% |
| **Latency p95** | ~1,800ms | ≤2,000ms | +11% acceptable |
| **Success Rate** | 96% | ≥95% | Maintain |

### 1.2 Current RAG Architecture

```
User Query
    │
    ▼
EmbeddingService (nomic-embed-text, 768-dim)
    │
    ▼
QdrantService (Vector Search, game-filtered)
    │ (limit=3, cosine similarity)
    ▼
StreamingQaService/RagService
    │ (Hardcoded prompts, no few-shot examples)
    ▼
LlmService (OpenRouter API)
    │
    ▼
Response Stream (SSE) / JSON Response
```

**Identified Bottlenecks:**
1. **Generic prompts**: No domain-specific board game guidance
2. **Fixed chunking**: 512 chars breaks mid-sentence, poor context preservation
3. **Single-query retrieval**: Misses synonyms and related concepts

---

## 2. Optimization Strategy Overview

### 2.1 Three-Pronged Approach

| Optimization | Target Component | Expected Impact | Cost | Risk |
|--------------|------------------|-----------------|------|------|
| **Prompt Engineering** | StreamingQaService, RagService | +12% P@5, +18% MRR | $0 | Low |
| **Semantic Chunking** | TextChunkingService | +14% P@5, +8% MRR | $0 | Medium* |
| **Query Expansion** | RagService | +9% P@5, +6% MRR | $2-3/mo | Medium |

*\*Medium risk due to re-indexing requirement, but has rollback plan*

### 2.2 Key Design Principles (from LangChain Best Practices)

**Semantic Chunking (AI-07.2):**
- Use RecursiveCharacterTextSplitter strategy (LangChain pattern)
- Split hierarchy: Paragraphs → Sentences → Clauses → Characters
- Preserve semantic boundaries (lists, tables, sections)
- Contextual overlap: include previous chunk's last sentence

**Prompt Engineering (AI-07.1):**
- Few-shot examples (3-5 per question category)
- Structured templates: System + Examples + Instructions + Context
- Question classification: setup, gameplay, edge cases, winning conditions
- Game-specific prompts (Chess complexity ≠ Tic-Tac-Toe)

**Query Expansion (AI-07.3):**
- LLM-based expansion (Claude Haiku or GPT-4o-mini for cost efficiency)
- Multi-query retrieval with RRF (Reciprocal Rank Fusion) score merging
- Redis caching (TTL 1 hour) to minimize LLM costs
- Cost control: monthly budget enforcement ($5 hard limit)

---

## 3. Implementation Order (Recommended)

### ✅ Option A: Parallel Implementation (2 weeks total)

**Week 1: Parallel Track**
```
Monday-Wednesday:
├─ AI-07.1 (Prompt Engineering)
│  ├─ Create PromptTemplateService
│  ├─ Add few-shot examples to appsettings.json
│  └─ Integrate into StreamingQaService
│
└─ AI-07.2 (Semantic Chunking)
   ├─ Create IChunkingStrategy interface
   ├─ Implement SemanticChunkingStrategy
   └─ Refactor TextChunkingService

Thursday:
├─ Merge both PRs
├─ Run integration tests
└─ Deploy to staging with feature flags

Friday:
├─ RAG evaluation tests (24-query dataset)
├─ Measure combined impact (P@5, MRR)
└─ Gradual rollout (50% → 100% traffic)
```

**Week 2: Sequential Track**
```
Monday-Tuesday:
├─ AI-07.3 (Query Expansion)
│  ├─ Create QueryExpansionService
│  ├─ Implement RRF score fusion
│  └─ Redis caching layer

Wednesday-Thursday:
├─ Integration with RagService
├─ Cost monitoring metrics (Prometheus)
└─ Full RAG evaluation with all three optimizations

Friday:
├─ Final validation (quality gates)
├─ Production deployment
└─ Monitoring dashboard review
```

**Why This Order?**
- ✅ Prompt engineering and semantic chunking are **independent** (no resource contention)
- ✅ Both can be tested and validated in parallel
- ✅ Query expansion builds on **stable foundation** of improved chunks
- ✅ Cost control requires operational monitoring before enabling

---

## 4. Extension Points & Integration Details

### 4.1 AI-07.1: Prompt Engineering Extension Point

**Target Files:**
- `StreamingQaService.cs` (lines 162-178) - Replace hardcoded prompts
- `RagService.cs` (lines 119-135) - Replace Explain prompts

**New Components:**
```csharp
// NEW SERVICE
public interface IPromptTemplateService
{
    Task<PromptTemplate> LoadTemplateAsync(Guid gameId, QuestionType questionType);
    string RenderSystemPrompt(PromptTemplate template);
    string RenderUserPrompt(PromptTemplate template, string context, string query);
}

// INTEGRATION
var promptTemplate = await _promptTemplateService
    .LoadTemplateAsync(gameId, ClassifyQuestionType(query));
var systemPrompt = promptTemplate.RenderSystemPrompt();
var userPrompt = promptTemplate.RenderUserPrompt(context, query);
```

**Configuration Addition (appsettings.json):**
```json
"RagPrompts": {
  "DefaultTemplate": "generic",
  "Templates": {
    "generic": {
      "SystemPrompt": "You are an expert board game rules assistant...",
      "FewShotExamples": [
        {
          "question": "How do I set up Chess?",
          "answer": "To set up Chess, place the board...",
          "category": "setup"
        }
      ],
      "MaxExamples": 3
    }
  }
}
```

**No Breaking Changes:** ✅ Backward compatible fallback if template missing

---

### 4.2 AI-07.2: Semantic Chunking Extension Point

**Target Files:**
- `TextChunkingService.cs` (entire class) - Refactor to strategy pattern

**New Components:**
```csharp
// NEW INTERFACE
public interface IChunkingStrategy
{
    List<TextChunk> ChunkText(string text, int targetSize, int overlap);
}

// IMPLEMENTATIONS
public class FixedSizeChunkingStrategy : IChunkingStrategy { } // Current behavior
public class SemanticChunkingStrategy : IChunkingStrategy      // NEW
{
    // Recursive splitting: Paragraphs → Sentences → Clauses → Characters
    private List<string> SplitByParagraphs(string text) { }
    private List<string> SplitBySentences(string text) { }
    private List<string> SplitByClauses(string text) { }
}

// DI REGISTRATION (Program.cs)
services.AddSingleton<IChunkingStrategy>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return config["TextChunking:Strategy"] switch
    {
        "Semantic" => new SemanticChunkingStrategy(config),
        _ => new FixedSizeChunkingStrategy(config)
    };
});
```

**Configuration Addition (appsettings.json):**
```json
"TextChunking": {
  "Strategy": "Semantic",
  "TargetChunkSize": 800,
  "MinChunkSize": 400,
  "MaxChunkSize": 1200,
  "OverlapTokens": 100,
  "PreserveBoundaries": {
    "Paragraphs": true,
    "Sentences": true,
    "Lists": true,
    "Tables": true
  }
}
```

**Migration Strategy:**
1. Deploy with `Strategy: "Fixed"` (backward compatible)
2. Run migration script: `tools/reindex-pdfs.ps1` on staging
3. Test with `Strategy: "Semantic"` on new uploads
4. Gradual rollout with A/B testing

**No Breaking Changes:** ✅ Can rollback via config change

---

### 4.3 AI-07.3: Query Expansion Extension Point

**Target Files:**
- `RagService.cs` (lines 85-104) - Integrate multi-query retrieval

**New Components:**
```csharp
// NEW SERVICE
public interface IQueryExpansionService
{
    Task<string[]> ExpandQueryAsync(string query, string language);
    Task<SearchResult> FuseResultsWithRRF(List<SearchResultItem>[] results, int topK);
}

// INTEGRATION (RagService.SearchAsync)
if (QueryExpansionEnabled)
{
    var queryVariants = await _queryExpansionService
        .ExpandQueryAsync(query, language);

    var allResults = new List<SearchResultItem>();
    foreach (var variant in queryVariants)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(variant, language);
        var results = await _qdrantService.SearchAsync(gameId, embedding, language, limit: 20);
        allResults.AddRange(results);
    }

    searchResult = await _queryExpansionService.FuseResultsWithRRF(allResults, topK: 10);
}
```

**Configuration Addition (appsettings.json):**
```json
"QueryExpansion": {
  "Enabled": true,
  "Model": "anthropic/claude-3-haiku",
  "MaxVariations": 3,
  "FusionMethod": "RRF",
  "TopKPerQuery": 20,
  "FinalTopK": 10,
  "Caching": {
    "Enabled": true,
    "TtlSeconds": 3600
  },
  "CostControl": {
    "MonthlyBudgetUsd": 5.0
  }
}
```

**Cost Analysis:**
- Model: Claude Haiku ($0.25/1M input, $1.25/1M output)
- Per query: ~150 tokens = $0.0002
- 1000 queries/month = $0.20
- Conservative estimate: $2-3/month with buffer

**No Breaking Changes:** ✅ Feature-flaggable, graceful degradation on LLM failure

---

## 5. Conflict Analysis & Synergies

### 5.1 Potential Conflicts

| Optimization Pair | Conflict | Severity | Mitigation |
|-------------------|----------|----------|------------|
| Prompting + Chunking | None | ✅ None | Independent concerns |
| Prompting + Expansion | None | ✅ None | No resource contention |
| Chunking + Expansion | Indirect | ⚠️ Low | Monitor combined effectiveness |
| **All Three** | Multi-query latency | ⚠️ Medium | Query expansion + semantic search may push p95 to 2000ms limit |

### 5.2 Beneficial Synergies

1. **Semantic Chunking + Query Expansion**
   - Better chunks are more retrievable
   - Query expansion multiplies benefit
   - **Expected combined gain:** >25% (vs. 14% + 9% separately)

2. **Prompt Engineering + All Retrievals**
   - Better prompts work with any retrieval quality
   - Can be deployed independently
   - No dependency on chunk or query changes

---

## 6. Testing Strategy

### 6.1 Automated Validation (CI)

**RAG Evaluation Tests** (existing infrastructure from AI-06):
```bash
# Run full evaluation suite
dotnet test --filter "FullyQualifiedName~RagEvaluation"

# Metrics tracked automatically:
- Precision@K (K=1,3,5,10)
- Recall@K
- Mean Reciprocal Rank (MRR)
- Latency percentiles (p50, p95, p99)
- Success rate
```

**Quality Gates (CI will FAIL if not met):**
- P@5 ≥ 0.90 (up from 0.70)
- MRR ≥ 0.75 (up from 0.60)
- Latency p95 ≤ 2000ms (max +11% increase)
- Success rate ≥ 95% (maintain)

### 6.2 Manual Testing (24-Query Dataset)

**Test Categories:**
- Setup questions (e.g., "How do I set up Chess?")
- Gameplay rules (e.g., "Can I move my pawn backward?")
- Winning conditions (e.g., "How do I win Tic-Tac-Toe?")
- Edge cases (e.g., "What happens in en passant?")

**Test Execution:**
1. Baseline run with current system (save results)
2. Run with AI-07.1 only (prompt engineering)
3. Run with AI-07.1 + AI-07.2 (+ semantic chunking)
4. Run with all three (+ query expansion)
5. Compare metrics at each stage

---

## 7. Rollout Plan

### 7.1 Staged Deployment

**Phase 1: Staging Validation (Day 1-2)**
```
1. Deploy all three optimizations with feature flags OFF
2. Enable AI-07.1 (Prompt Engineering) only
   - Run automated tests
   - Manual spot checks
3. Enable AI-07.2 (Semantic Chunking)
   - Re-index test PDFs
   - Validate chunk quality
4. Enable AI-07.3 (Query Expansion)
   - Monitor cost metrics
   - Test Redis caching
```

**Phase 2: Production Canary (Day 3-4)**
```
1. Deploy to production with flags OFF
2. Enable AI-07.1 for 50% traffic (A/B test)
   - Monitor metrics for 24 hours
3. If stable, enable AI-07.2 for 50% traffic
   - Monitor for 24 hours
4. If stable, enable AI-07.3 for 50% traffic
   - Monitor cost and latency
```

**Phase 3: Full Rollout (Day 5)**
```
1. Enable all optimizations for 100% traffic
2. Monitor for 48 hours
3. Document final metrics
4. Close epic issue
```

### 7.2 Rollback Procedures

**If quality degrades:**
```json
// appsettings.json - Instant rollback via config change
"RagPrompts": { "Enabled": false },
"TextChunking": { "Strategy": "Fixed" },
"QueryExpansion": { "Enabled": false }
```

**If cost exceeds budget:**
```json
"QueryExpansion": {
  "Enabled": false,  // Instant disable
  "CostControl": { "MonthlyBudgetUsd": 3.0 }  // Lower limit
}
```

---

## 8. Monitoring & Observability

### 8.1 New Metrics (Prometheus)

**Prompt Engineering:**
- `meepleai.prompts.template_loads.total` (counter)
- `meepleai.prompts.examples_used` (histogram)

**Semantic Chunking:**
- `meepleai.chunking.strategy` (label: "Fixed" | "Semantic")
- `meepleai.chunking.avg_chunk_size` (histogram)
- `meepleai.chunking.boundary_preservations` (counter)

**Query Expansion:**
- `meepleai.query_expansion.calls.total` (counter)
- `meepleai.query_expansion.tokens.used` (counter)
- `meepleai.query_expansion.cost.usd` (gauge)
- `meepleai.query_expansion.cache.hits` (counter)

### 8.2 Grafana Dashboard

**New Panel: RAG Optimization**
- P@5 trend over time (before/after)
- MRR trend
- Query expansion cost tracking (with $5 alert threshold)
- Chunking strategy distribution
- Prompt template usage breakdown

---

## 9. Success Metrics Summary

### 9.1 Per-Optimization Targets

| Optimization | P@5 Target | MRR Target | Latency Impact | Cost |
|--------------|-----------|-----------|----------------|------|
| AI-07.1 Prompting | +12% | +18% | 0ms | $0 |
| AI-07.2 Chunking | +14% | +8% | 0ms | $0 |
| AI-07.3 Expansion | +9% | +6% | +200-300ms | $2-3/mo |
| **COMBINED** | **+35%** | **+32%** | **+200ms** | **$2-3/mo** |

### 9.2 Final Validation Criteria

**Before closing AI-07 epic:**
- ✅ All three child issues (#468, #469, #470) closed
- ✅ P@5 ≥ 0.90 (measured via RagEvaluationService)
- ✅ MRR ≥ 0.75
- ✅ Latency p95 ≤ 2000ms
- ✅ Cost ≤ $5/month
- ✅ CI pipeline green (all quality gates passed)
- ✅ Production monitoring stable for 48 hours
- ✅ Documentation updated (this file + child issue docs)

---

## 10. Risk Mitigation Summary

### 10.1 Risk Matrix

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Latency exceeds 2s p95 | Medium | High | Feature flags, gradual rollout |
| Query expansion cost overrun | Low | Medium | Redis caching, $5 monthly budget hard limit |
| Semantic chunking breaks retrieval | Low | High | Strategy pattern rollback, A/B testing |
| Prompt changes reduce quality | Low | Medium | Automated evaluation tests in CI |

### 10.2 Mitigation Strategies

1. **Feature Flags:** All optimizations behind config switches
2. **Automated Testing:** RAG evaluation in CI with quality gates
3. **Gradual Rollout:** 50% → 100% traffic with monitoring
4. **Cost Controls:** Monthly budget enforcement, alerts at 80% threshold
5. **Rollback Scripts:** One-line config changes for instant revert

---

## 11. Dependencies & Prerequisites

### 11.1 Infrastructure (Already Available)

- ✅ PostgreSQL (EF Core 9.0) - Database
- ✅ Qdrant - Vector search
- ✅ Redis - Response caching + query expansion caching
- ✅ OpenRouter API - LLM access (Haiku available)
- ✅ Prometheus + Grafana - Metrics and dashboards
- ✅ RagEvaluationService - Automated testing (AI-06)

### 11.2 New Requirements

- ⚠️ **Migration Script:** `tools/reindex-pdfs.ps1` for semantic chunking
- ⚠️ **Configuration Updates:** Three new appsettings.json sections
- ⚠️ **Grafana Dashboard:** RAG Optimization panel (new)

---

## 12. Documentation Deliverables

### 12.1 Technical Documentation

- ✅ This file: `docs/technic/ai-07-rag-optimization-coordination-plan.md`
- 📝 Child issue docs:
  - `docs/issue/ai-07-1-prompt-engineering.md` (created by #468)
  - `docs/issue/ai-07-2-semantic-chunking.md` (created by #469)
  - `docs/issue/ai-07-3-query-expansion.md` (created by #470)

### 12.2 User-Facing Documentation

- 📝 Update: `docs/ai-06-rag-evaluation.md` (add AI-07 improvements section)
- 📝 Update: `docs/observability.md` (new metrics from AI-07)
- 📝 New: `docs/migrations/semantic-chunking-migration.md` (re-indexing guide)

---

## 13. Timeline & Milestones

### 13.1 Detailed Schedule (2-3 weeks)

**Week 1:**
- Day 1-3: Parallel implementation (AI-07.1 + AI-07.2)
- Day 4: Integration testing, merge PRs
- Day 5: Staging deployment, initial evaluation

**Week 2:**
- Day 1-2: AI-07.3 implementation (query expansion)
- Day 3-4: Full integration, cost monitoring setup
- Day 5: Final validation, production deployment preparation

**Week 3 (Buffer):**
- Day 1-2: Production canary rollout (50%)
- Day 3-4: Full production rollout (100%)
- Day 5: Monitoring, documentation finalization, epic closure

### 13.2 Key Milestones

- ✅ **M1:** Child issues #468, #469 merged (Week 1 end)
- ✅ **M2:** Child issue #470 merged (Week 2 mid)
- ✅ **M3:** All quality gates passed (Week 2 end)
- ✅ **M4:** Production rollout complete (Week 3 mid)
- ✅ **M5:** Epic #467 closed (Week 3 end)

---

## 14. Recommended Next Steps

### 14.1 For Implementation Teams

**AI-07.1 Team (Prompt Engineering):**
1. Use `/work 468` to start BDD workflow
2. Reference: LangChain few-shot examples pattern (from Context7 research)
3. Focus: `PromptTemplateService` + appsettings.json templates
4. Timeline: 3-4 days

**AI-07.2 Team (Semantic Chunking):**
1. Use `/work 469` to start BDD workflow
2. Reference: LangChain RecursiveCharacterTextSplitter (from Context7 research)
3. Focus: `IChunkingStrategy` interface + `SemanticChunkingStrategy`
4. Timeline: 4-5 days

**AI-07.3 Team (Query Expansion):**
1. Use `/work 470` to start BDD workflow (after Week 1 completion)
2. Reference: LangChain query expansion + RRF patterns
3. Focus: `QueryExpansionService` + RRF score fusion
4. Timeline: 5-6 days

### 14.2 For Epic Coordinator

1. Monitor child issue progress (daily standups)
2. Resolve merge conflicts if any (likely minimal)
3. Coordinate integration testing (Week 1 Day 4)
4. Manage staging deployment (Week 1 Day 5)
5. Oversee production rollout (Week 3)
6. Update this document as needed
7. Close epic when all validation criteria met

---

## 15. Conclusion

The AI-07 RAG Optimization Epic is **well-scoped, low-risk, and high-ROI**. All three child optimizations can proceed in **parallel with minimal conflicts**, leveraging existing infrastructure and battle-tested patterns from LangChain.

**Key Success Factors:**
- ✅ Clear separation of concerns (retrieval vs. generation vs. chunking)
- ✅ Feature-flag protection for safe rollouts
- ✅ Comprehensive test infrastructure (AI-06 evaluation framework)
- ✅ Established configuration patterns (appsettings.json)
- ✅ Cost controls and monitoring (Prometheus + Grafana)

**Expected Outcome:**
- +30-40% improvement in P@5 and MRR
- Maintained latency SLA (<2s p95)
- Minimal cost increase ($2-3/month)
- Zero breaking changes to existing APIs

---

**Document Version:** 1.0
**Last Updated:** 2025-10-20
**Author:** MeepleAI Engineering (via Claude Code)
**Status:** Ready for Child Issue Implementation

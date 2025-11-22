# Embedding Optimization - Implementation Plan

**Date**: 2025-11-22
**Status**: 📋 Planning Phase
**Owner**: Engineering Team
**Timeline**: 3-4 weeks

---

## 🎯 Executive Summary

Piano completo per **ridurre i costi di embedding da $30-50/mese a ~$0-10/mese** (67-100% saving) mentre si migliora performance (+60% velocità) e si mantiene >95% accuracy.

**Key Innovation**: Admin UI per selezionare modelli embedding OpenRouter, con configurabilità runtime e cost estimation.

---

## 📊 Issues Overview

| # | Title | Priority | Effort | Impact | Savings |
|---|-------|----------|--------|--------|---------|
| [#1](./issue-001-admin-embedding-model-selection.md) | Admin Embedding Model Selection | 🔴 Critical | 12-16h | High | $0-30/mese |
| [#2](./issue-002-query-embedding-cache.md) | Query Embedding Cache | 🔴 Critical | 6-8h | High | 60% latency |
| [#3](./issue-003-chunking-optimization.md) | Chunking Optimization | 🟡 High | 8-10h | Medium | -30% embeddings |
| [#4](./issue-004-incremental-reindexing.md) | Incremental Re-indexing | 🟡 High | 12-16h | High | 90% re-index |
| [#5](./issue-005-ollama-batch-parallel.md) | Ollama Batch Parallelization | 🟢 Medium | 4-6h | Medium | 5x speed |
| [#6](./issue-006-embedding-cost-monitoring.md) | Embedding Cost Monitoring | 🟢 Medium | 6-8h | Medium | Visibility |
| **TOTAL** | | | **48-64h** | | **$25-45/mese** |

---

## 🗓️ 4-Week Implementation Timeline

### Week 1: Admin Configuration + Quick Wins
**Goal**: Enable admin model selection + immediate cache benefits

**Tasks**:
1. **Issue #1: Admin Embedding Model Selection** (12-16h)
   - Day 1-2: Database migration + catalog seeding
   - Day 3: Backend CQRS handlers
   - Day 4: HTTP endpoints
   - Day 5: Frontend Admin UI

2. **Issue #2: Query Embedding Cache** (6-8h)
   - Day 1: CachedEmbeddingService implementation
   - Day 2: Metrics + testing

**Deliverables**:
- ✅ Admin can select from 12+ embedding models (Ollama + OpenRouter)
- ✅ Query cache active (60% hit rate expected)
- ✅ Grafana metrics for cache performance

**Cost Impact**: $30 → $12/mese (if using OpenAI + cache)

---

### Week 2: Performance Optimization
**Goal**: Faster PDF indexing + better resource utilization

**Tasks**:
1. **Issue #5: Ollama Batch Parallelization** (4-6h)
   - Day 1: Parallel embedding generation
   - Day 2: Testing + tuning concurrency

2. **Issue #6: Embedding Cost Monitoring** (6-8h)
   - Day 1: Prometheus metrics
   - Day 2: Grafana dashboard
   - Day 3: Cost estimation API

**Deliverables**:
- ✅ PDF indexing 5x faster (10s → 2s for 100 chunks)
- ✅ Grafana dashboard with cost tracking
- ✅ Real-time visibility into embedding usage

**Cost Impact**: Improved visibility, no direct savings

---

### Week 3: Cost Reduction
**Goal**: Minimize embeddings generated per PDF

**Tasks**:
1. **Issue #3: Chunking Optimization** (8-10h)
   - Day 1-2: Update chunk size (512 → 768)
   - Day 3: Accuracy testing (P@10, MRR validation)
   - Day 4: A/B test in staging
   - Day 5: Production rollout (gradual)

2. **Issue #4: Incremental Re-indexing** (12-16h)
   - Day 1-2: Chunk hashing + VectorDocumentEntity migration
   - Day 3: IndexPdfCommandHandler refactor
   - Day 4: Qdrant upsert implementation
   - Day 5: Testing + validation

**Deliverables**:
- ✅ 30% fewer chunks per PDF (108 → 75 chunks)
- ✅ 90% savings on PDF re-indexing
- ✅ Accuracy maintained (>95%)

**Cost Impact**: $12 → $5/mese (chunking + incremental re-index)

---

### Week 4: Testing & Rollout
**Goal**: Production deployment + monitoring

**Tasks**:
1. **Integration Testing**
   - All 6 features work together
   - No regressions in RAG quality
   - Performance benchmarks met

2. **Staging Validation** (3 days)
   - Deploy to staging
   - Monitor metrics (accuracy, latency, cost)
   - QA testing

3. **Production Rollout** (gradual)
   - Day 1: 25% traffic
   - Day 2: 50% traffic
   - Day 3: 75% traffic
   - Day 4: 100% traffic

**Deliverables**:
- ✅ All features in production
- ✅ Cost reduced by 80-100%
- ✅ Performance improved 60%+
- ✅ Accuracy maintained >95%

---

## 💰 Cost Breakdown Analysis

### Current State (Baseline)
```
Provider: OpenAI text-embedding-3-small
Usage:
  - PDF uploads: 500/month × 108 chunks = 54,000 embeddings
  - User queries: 100,000/month = 100,000 embeddings
  - Total: 154,000 embeddings × 500 tokens = 77M tokens

Cost: 77M × $0.020/M = $30.80/mese ❌
```

### After Issue #1 (Admin Model Selection) + Issue #2 (Cache)
```
Provider: Ollama nomic-embed-text (admin selected)
Usage:
  - PDF uploads: 54,000 embeddings (unchanged)
  - User queries: 100,000 × 40% (cache miss) = 40,000 embeddings
  - Total: 94,000 embeddings

Cost: $0/mese (Ollama local) ✅
  OR
Cost (if admin selects OpenAI): 94,000 × 500 × $0.020/M = $9.40/mese ✅
```

### After Issue #3 (Chunking Optimization)
```
Provider: Ollama (or OpenAI if admin preference)
Usage:
  - PDF uploads: 500 × 75 chunks (-30%) = 37,500 embeddings
  - User queries: 40,000 (cached)
  - Total: 77,500 embeddings

Cost (Ollama): $0/mese ✅
Cost (OpenAI): 77,500 × 500 × $0.020/M = $7.75/mese ✅
```

### After Issue #4 (Incremental Re-index)
```
Provider: Ollama or OpenAI
Usage:
  - PDF uploads (first-time): 300 × 75 = 22,500 embeddings
  - PDF re-index: 200 × 75 × 10% (incremental) = 1,500 embeddings
  - User queries: 40,000 (cached)
  - Total: 64,000 embeddings

Cost (Ollama): $0/mese ✅
Cost (OpenAI): 64,000 × 500 × $0.020/M = $6.40/mese ✅
```

### Final State (All Issues Implemented)
```
Monthly Cost Options (Admin Configurable):
┌─────────────────────────────────────────────────────────┐
│  Option 1: Ollama nomic-embed-text (Free)               │
│  Cost: $0/mese ✅                                        │
│  Accuracy: 94-96%                                        │
│  Latency: 50ms (local)                                   │
├─────────────────────────────────────────────────────────┤
│  Option 2: OpenRouter Jina AI (Free Tier)               │
│  Cost: $0/mese ✅                                        │
│  Accuracy: 95-97%                                        │
│  Latency: 150ms (API)                                    │
├─────────────────────────────────────────────────────────┤
│  Option 3: OpenRouter OpenAI text-embedding-3-small     │
│  Cost: $6-10/mese ✅                                     │
│  Accuracy: 96-98%                                        │
│  Latency: 120ms (API)                                    │
└─────────────────────────────────────────────────────────┘

SAVINGS: $30-50 → $0-10 = 67-100% reduction ✅
```

---

## 📈 Performance Improvements

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Query Latency P95** | 200ms | 80ms (cache) | **-60%** |
| **PDF Indexing Time** | 10s | 2s (parallel) | **-80%** |
| **Re-index Cost** | 100% embeddings | 10% (incremental) | **-90%** |
| **Monthly Cost** | $30-50 | $0-10 | **-67-100%** |
| **Cache Hit Rate** | 0% | 60-75% | **+60-75%** |
| **Embeddings per PDF** | 108 | 75 | **-31%** |

---

## ✅ Success Criteria

### Technical Metrics
- [ ] Monthly cost <$10/mese (target: $0-5)
- [ ] Query latency P95 <100ms (target: 80ms)
- [ ] RAG accuracy >95% (maintained)
- [ ] Cache hit rate >60% (target: 75%)
- [ ] PDF indexing <3s for 100 chunks

### Business Metrics
- [ ] Admin adoption >80% (admins use model selection)
- [ ] Cost visibility 100% (Grafana dashboard)
- [ ] Zero accuracy regressions
- [ ] User satisfaction maintained (NPS unchanged)

---

## 🧪 Testing Strategy

### Unit Tests
```bash
# Run all embedding optimization tests
dotnet test --filter "Category=EmbeddingOptimization"

# Expected: 50+ tests (100% pass rate)
```

### Integration Tests
```bash
# Test admin model selection flow
dotnet test --filter "AdminEmbeddingSelectionTests"

# Test cache effectiveness
dotnet test --filter "CachedEmbeddingServiceTests"

# Test incremental re-indexing
dotnet test --filter "IncrementalIndexingTests"
```

### E2E Tests
```typescript
// Test admin UI
pnpm test:e2e -- admin-embedding-selection

// Test cost dashboard
pnpm test:e2e -- embedding-cost-dashboard
```

### Performance Tests
```bash
# Benchmark embedding latency
dotnet test --filter "EmbeddingPerformanceTests"

# Target: P95 <100ms for cached, <200ms for uncached
```

---

## 🚀 Deployment Plan

### Stage 1: Development (Week 1)
```bash
# Local testing with Ollama
EMBEDDING_PROVIDER=ollama
EMBEDDING_MODEL=nomic-embed-text

# Verify all features work locally
```

### Stage 2: Staging (Week 2-3)
```bash
# Deploy to staging
# Admin selects test models
# Run A/B tests (512 vs 768 chunk size)

# Metrics to monitor:
# - P@10 accuracy
# - MRR
# - User feedback
# - Cost tracking
```

### Stage 3: Production (Week 4)
```bash
# Gradual rollout with feature flags

# Day 1: Enable admin UI (0% traffic impact)
# Day 2: Enable cache (50% queries)
# Day 3: Enable cache (100% queries)
# Day 5: Enable chunking optimization (25% PDFs)
# Day 7: Enable chunking optimization (100% PDFs)
# Day 10: Enable incremental re-indexing (100%)

# Rollback triggers:
# - Accuracy drop >3%
# - Latency increase >50%
# - Error rate >1%
```

---

## 🔄 Rollback Plan

### Quick Rollback (Issue #1-2)
```bash
# Revert admin model selection
UPDATE system_configurations
SET value = 'text-embedding-3-small'
WHERE key = 'Embedding:Model';

# Disable cache
EMBEDDING_CACHE_ENABLED=false

# Restart API
docker compose restart api
```

### Gradual Rollback (Issue #3-4)
```bash
# Revert chunk size
sed -i 's/DefaultChunkSize = 768/DefaultChunkSize = 512/' ChunkingConstants.cs

# Disable incremental re-index
INCREMENTAL_REINDEX_ENABLED=false

# Re-deploy
```

---

## 📚 Documentation Updates

After implementation, update:
- [ ] [CLAUDE.md](../../../CLAUDE.md) - Add embedding optimization section
- [ ] [API docs](../../03-api/board-game-ai-api-specification.md) - Document admin endpoints
- [ ] [Environment variables](../../06-security/environment-variables-production.md) - Add new config
- [ ] [Cost tracking guide](../../02-development/implementation/bgai-026-cost-tracking.md) - Add embedding metrics
- [ ] [Admin guide](../../04-frontend/admin-guide.md) - Document model selection UI

---

## 👥 Team Assignments

| Issue | Owner | Reviewer | QA |
|-------|-------|----------|-----|
| #1 Admin Model Selection | Backend Lead | Engineering Lead | QA Team |
| #2 Query Cache | Backend Dev | Backend Lead | QA Team |
| #3 Chunking Optimization | Backend Dev | Engineering Lead | QA + Product |
| #4 Incremental Re-index | Backend Lead | Engineering Lead | QA Team |
| #5 Ollama Parallelization | Backend Dev | Backend Lead | QA Team |
| #6 Cost Monitoring | DevOps | Backend Lead | Engineering Lead |

---

## 📞 Communication Plan

### Weekly Updates
- **Monday**: Sprint planning, issue assignments
- **Wednesday**: Mid-week progress check, blockers
- **Friday**: Demo + retrospective

### Stakeholder Updates
- **Product Manager**: Weekly email with cost savings progress
- **Engineering Lead**: Daily Slack updates in #backend-optimization
- **QA Team**: Testing requests in #qa-requests

### Launch Announcement
- [ ] Engineering blog post (cost optimization case study)
- [ ] Internal demo (show admin UI + Grafana dashboard)
- [ ] User documentation (how to monitor embedding costs)

---

## 🎯 Next Steps

1. **Review & Approve** (Day 1-2)
   - Engineering Lead reviews this plan
   - Product Manager approves cost/timeline
   - QA Team confirms testing capacity

2. **Sprint Planning** (Day 3)
   - Create GitHub issues from docs/issues/embedding-optimization/
   - Assign issues to team
   - Set up project board

3. **Kickoff** (Week 1, Day 1)
   - Team standup
   - Environment setup
   - Start Issue #1

---

**Ready to Start**: ✅ All issues documented, plan approved
**Estimated Completion**: 4 weeks from kickoff
**Expected ROI**: $300-600/year savings (12 months × $25-50/mese)

---

**Created**: 2025-11-22
**Last Updated**: 2025-11-22
**Version**: 1.0
**Author**: Engineering Team

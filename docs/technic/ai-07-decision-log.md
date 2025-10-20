# AI-07 RAG Optimization - Decision Log

## Decision 1: Epic Coordination Strategy

**Date:** 2025-10-20
**Context:** AI-07 is a parent epic with three child issues (#468 Prompt Engineering, #469 Semantic Chunking, #470 Query Expansion) aiming to optimize RAG system performance by +30-40% in P@5/MRR metrics. The team needed to decide on implementation order to minimize conflicts while maximizing efficiency.

**Decision:** Adopt hybrid approach: Parallel implementation for Week 1 (AI-07.1 + AI-07.2), followed by sequential implementation for Week 2 (AI-07.3).

**Alternatives Considered:**
1. **Full sequential implementation** (3-4 weeks)
   - Lowest risk but slowest delivery
   - Complete each optimization before starting next
   - Missed opportunity for parallel work

2. **Full parallel implementation** (1.5-2 weeks)
   - Fastest delivery but higher integration risk
   - All three teams working simultaneously
   - Potential merge conflicts and resource contention

3. **Query expansion first** (prioritize highest-cost optimization)
   - Establish cost monitoring early
   - But misses synergy with improved chunking
   - Baseline chunks may reduce query expansion effectiveness

**Consequences:**
✅ **Pros:**
- Week 1 parallel work saves 3-4 days vs sequential
- Zero resource contention between prompt engineering and chunking
- Query expansion builds on stable foundation of improved chunks
- Cost monitoring can be established before enabling query expansion
- Total timeline: 2-3 weeks (meets epic goal)
- Clear integration point (Week 1 Thursday merge)

⚠️ **Cons/Risks:**
- Requires coordination for Week 1 Thursday merge (mitigated by clear extension points)
- Query expansion team waits for Week 1 completion (acceptable tradeoff for stability)

**Validation:**
- Codebase exploration confirmed zero conflicts between AI-07.1 and AI-07.2
- Extension points are isolated (StreamingQaService vs TextChunkingService)
- Both optimizations use different configuration sections (RagPrompts vs TextChunking)
- LangChain research validated patterns are proven and compatible

---

## Decision 2: Use LangChain Patterns for Implementation

**Date:** 2025-10-20
**Context:** Three optimizations needed design patterns. Team researched industry best practices via Context7 (LangChain documentation).

**Decision:** Adopt LangChain patterns for all three optimizations:
1. **RecursiveCharacterTextSplitter** for semantic chunking (AI-07.2)
2. **Few-shot prompt engineering** with templating (AI-07.1)
3. **Multi-query retrieval with RRF fusion** for query expansion (AI-07.3)

**Alternatives Considered:**
1. **Custom algorithms from scratch**
   - Full control but higher development time
   - Risk of missing edge cases
   - No proven track record

2. **Use .NET-specific libraries (e.g., Semantic Kernel)**
   - Better .NET integration
   - But less mature RAG patterns than LangChain
   - Limited documentation for semantic chunking

**Consequences:**
✅ **Pros:**
- Battle-tested patterns used in production by thousands of teams
- Extensive documentation and examples (Context7 provided 12K+ code snippets)
- Clear separation of concerns (hierarchy: Paragraphs → Sentences → Clauses)
- Proven cost optimization strategies (Redis caching, RRF fusion)
- Can adapt Python patterns to C# with minimal risk

⚠️ **Cons/Risks:**
- Requires translation from Python to C# (mitigated by clear algorithm descriptions)
- Some features may not directly map (e.g., AI21SemanticTextSplitter is external API)

**Validation:**
- LangChain RecursiveCharacterTextSplitter is standard for semantic chunking
- Few-shot prompting is LLM-agnostic (works with OpenRouter/Haiku)
- RRF fusion is well-documented with clear math formula

---

## Decision 3: Feature-Flag All Optimizations

**Date:** 2025-10-20
**Context:** All three optimizations needed safe rollout mechanisms to production without breaking existing functionality.

**Decision:** Implement feature flags in appsettings.json for each optimization:
- `RagPrompts.Enabled` (AI-07.1)
- `TextChunking.Strategy` = "Fixed" | "Semantic" (AI-07.2)
- `QueryExpansion.Enabled` (AI-07.3)

**Alternatives Considered:**
1. **Deploy directly without flags**
   - Faster initial deployment
   - But no rollback mechanism
   - High risk if quality degrades

2. **Use external feature flag service (LaunchDarkly, etc.)**
   - More sophisticated targeting rules
   - But adds infrastructure dependency
   - Overkill for simple enable/disable

**Consequences:**
✅ **Pros:**
- Instant rollback via config change (no code deployment)
- Gradual rollout possible (50% → 100% traffic)
- A/B testing capability
- Zero breaking changes to existing APIs
- Fallback to current behavior if optimization fails

⚠️ **Cons/Risks:**
- Slightly increased code complexity (if/else branches)
- Need to monitor config changes (mitigated by audit logging)

**Validation:**
- Existing codebase already uses config-driven feature flags (CacheOptimization.WarmingEnabled)
- appsettings.json is versioned and deployed with application
- Clear precedent for this approach

---

## Decision 4: Cost Control with $5 Monthly Budget Hard Limit

**Date:** 2025-10-20
**Context:** Query expansion (AI-07.3) requires LLM API calls (Claude Haiku) which incur costs. Need cost control to prevent budget overruns.

**Decision:** Implement multi-layered cost control:
1. Redis caching (TTL 1 hour) to minimize duplicate queries
2. Prometheus metric tracking (`meepleai.query_expansion.cost.usd`)
3. Hard budget limit in config (`CostControl.MonthlyBudgetUsd: 5.0`)
4. Grafana alert at 80% threshold ($4)
5. Automatic disable if budget exceeded

**Alternatives Considered:**
1. **No cost controls** (trust traffic estimates)
   - Simplest implementation
   - But risk of unexpected traffic spike → cost overrun
   - No visibility into actual spend

2. **Pre-paid API credits with hard limit**
   - Guaranteed no overrun
   - But may cause service degradation if credits exhausted
   - Less flexible than soft monitoring

**Consequences:**
✅ **Pros:**
- Conservative $5 budget covers 2000+ queries/month (10x estimated 200/month)
- Redis caching expected to reduce LLM calls by 60-80%
- Real-time cost visibility in Grafana
- Graceful degradation (falls back to single-query retrieval if budget exceeded)
- Alert provides early warning before hard limit

⚠️ **Cons/Risks:**
- Cost tracking requires accurate token counting (mitigated by OpenRouter API response)
- Cache evictions could increase cost temporarily (acceptable with 10x buffer)

**Validation:**
- Haiku model costs: $0.25/1M input, $1.25/1M output
- Per-query estimate: 50 input + 100 output = 150 tokens = $0.0002
- 1000 queries = $0.20, so $5 = 25,000 queries (huge buffer)
- Redis is already in infrastructure (zero additional cost)

---

## Summary

All four decisions follow **risk-averse, incremental, and reversible** principles:

1. **Hybrid implementation order** → Fast delivery with low risk
2. **LangChain patterns** → Proven algorithms, reduce R&D time
3. **Feature flags** → Safe rollout, instant rollback
4. **Cost controls** → Prevent budget surprises, maintain SLA

**Total Risk Assessment:** Low to Medium
- Low: Prompt engineering, feature flags, cost controls
- Medium: Semantic chunking (requires re-indexing), query expansion (external LLM dependency)

**Mitigation:** All medium risks have clear rollback plans and monitoring.

---

**Last Updated:** 2025-10-20
**Status:** Approved for Implementation

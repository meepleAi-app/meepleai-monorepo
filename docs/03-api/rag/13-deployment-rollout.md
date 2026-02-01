# Deployment Rollout Plan

**12-Week Phased Implementation**

---

## Phase 0: Quick Wins (Weeks 1-2)

**Goal**: Immediate token savings through caching

- [x] Semantic cache implementation (Redis + LLM similarity)
- [x] Metadata filtering (add game_id, category to chunks)
- [ ] Deploy to dev environment
- [ ] A/B test with 10% Admin traffic
- [ ] Target: 60% cache hit rate

**Expected Impact**: -50% token cost on cache hits

---

## Phase 1: FAST Strategy (Weeks 3-4)

**Goal**: Production-ready FAST tier

- [ ] Integrate MiniLM-L6-v2 embedding
- [ ] Vector-only retrieval (top-K=3)
- [ ] Template-specific prompts (rule_lookup, resource_planning)
- [ ] Citation validator (rule-based)
- [ ] Expand to 50% Admin, 25% Editor traffic

**Expected Impact**: Baseline for comparison

---

## Phase 2: BALANCED + CRAG (Weeks 5-8)

**Goal**: High-accuracy tier with CRAG quality gating

- [ ] Fine-tune T5-large CRAG evaluator (create dataset first!)
- [ ] Hybrid search (Vector + BM25)
- [ ] Cross-encoder reranking
- [ ] CRAG pipeline integration (evaluate → filter → decompose-recompose)
- [ ] Web search augmentation (Bing API)
- [ ] Expand to 100% Admin/Editor, 50% User traffic

**Expected Impact**: +12% accuracy, CRAG filtering saves 40-70% context tokens

---

## Phase 3: PRECISE + Self-RAG (Weeks 9-10)

**Goal**: Premium tier for strategic queries

- [ ] Self-RAG reflection prompts
- [ ] Multi-hop retrieval (3-5 hops, adaptive)
- [ ] LLM-based grading (gte-Qwen2 or Claude Haiku)
- [ ] Expand to 100% User (with 5/day quota), full Editor/Admin

**Expected Impact**: 95% accuracy for critical queries

---

## Phase 4: Multi-Agent (Weeks 11-12)

**Goal**: 3-agent system for complex strategic planning

- [ ] LangGraph setup and orchestration
- [ ] Agent implementations (Analyzer, Strategist, Validator)
- [ ] Agent coordination logic
- [ ] Deploy for Admin tier only (Editor optional)
- [ ] Full rollout to all tiers

**Expected Impact**: 95-98% accuracy for strategic planning

---

## Rollback Plan

If CRAG accuracy <85% → disable, use standard Advanced RAG
If Multi-Agent costs exceed budget → disable, use Self-RAG only
If cache hit <50% → tune similarity threshold

---

**Back**: [Overview](00-overview.md) | [Implementation](10-implementation-guide.md)

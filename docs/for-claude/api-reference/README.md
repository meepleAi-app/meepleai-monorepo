# MeepleAI RAG System Documentation

**TOMAC-RAG**: Token-Optimized Modular Adaptive Corrective RAG

> **Single Source of Truth**: `apps/web/src/components/rag-dashboard/rag-data.ts`
>
> All values in this documentation are synced from the frontend data file.

---

## Quick Navigation

| Audience | Start Here |
|----------|------------|
| **Developers** | [Implementation Guide](10-implementation-guide.md) |
| **Architects** | [Technical Reference](15-technical-reference.md) |
| **Business** | [HOW-IT-WORKS.md](HOW-IT-WORKS.md) |
| **Everyone** | [Interactive Dashboard](/rag) |

---

## Implementation Status

### POC (Current) vs TOMAC-RAG (Planned)

```
POC Implemented ████████████░░░░░░░░ ~55%
```

| Component | POC Status | TOMAC-RAG Plan |
|-----------|------------|----------------|
| **Hybrid Search** | Implemented | Same |
| **Vector Search (Qdrant)** | Implemented | Same |
| **Keyword Search (PostgreSQL)** | Implemented | Same |
| **RRF Fusion** | Implemented | Same |
| **LLM Generation** | Implemented | Enhanced |
| **Circuit Breaker** | Implemented | Same |
| **Response Cache** | Implemented | + Semantic |
| **L1 Strategy Router** | - | Planned |
| **L4 CRAG Evaluation** | - | Planned |
| **L6 Self-RAG Validation** | - | Planned |
| **Reranker Integration** | Container ready | Planned |

### POC Architecture (Working Now)

```
User Query
    │
    ▼
┌─────────────────────────────────────────────────────────┐
│ EMBEDDING SERVICE (:8000)                               │
│ text-embedding-3-large → 3072 dimensions                │
└─────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────┐
│ HYBRID SEARCH                                           │
│ ┌─────────────────┐    ┌─────────────────┐             │
│ │ SEMANTIC        │    │ KEYWORD         │             │
│ │ Qdrant :6333    │    │ PostgreSQL :5432│             │
│ │ cosine sim      │    │ tsvector FTS    │             │
│ │ HNSW index      │    │ ts_rank_cd      │             │
│ └────────┬────────┘    └────────┬────────┘             │
│          │                      │                       │
│          └──────────┬───────────┘                       │
│                     ▼                                   │
│            ┌─────────────────┐                          │
│            │ RRF FUSION      │                          │
│            │ k=60            │                          │
│            │ vector: 0.7     │                          │
│            │ keyword: 0.3    │                          │
│            └─────────────────┘                          │
└─────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────┐
│ CONTEXT ASSEMBLY                                        │
│ Top-K chunks → Deduplication → Format for LLM           │
└─────────────────────────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────────────────────────┐
│ LLM GENERATION (HybridLlmService)                       │
│ ┌─────────────────┐    ┌─────────────────┐             │
│ │ OLLAMA :11434   │ ←→ │ OPENROUTER      │             │
│ │ llama3.3:70b    │    │ gpt-4o-mini     │             │
│ │ LOCAL (FREE)    │    │ EXTERNAL (PAID) │             │
│ └─────────────────┘    └─────────────────┘             │
│         ↑                                               │
│         │ Circuit Breaker: 5 fails → 30s open           │
└─────────────────────────────────────────────────────────┘
    │
    ▼
Response (Streaming)
```

---

## 6 TOMAC-RAG Strategies

| Strategy | Tokens | Cost | Latency | Accuracy | Expected Usage |
|----------|--------|------|---------|----------|----------------|
| ⚡ **FAST** | 2,060 | $0.0001 | <200ms | 78-85% | 60-70% |
| ⚖️ **BALANCED** | 2,820 | $0.01 | 1-2s | 85-92% | 25-30% |
| 🎯 **PRECISE** | 22,396 | $0.132 | 5-10s | 95-98% | 5-10% |
| 🔍 **EXPERT** | 15,000 | $0.099 | 8-15s | 92-96% | 2-5% |
| 🗳️ **CONSENSUS** | 18,000 | $0.09 | 10-20s | 97-99% | 1-3% |
| ⚙️ **CUSTOM** | Variable | Variable | Variable | Variable | <1% |

### Strategy → Layer Mapping

| Strategy | L1 Routing | L2 Cache | L3 Retrieval | L4 CRAG | L5 Generation | L6 Validation |
|----------|------------|----------|--------------|---------|---------------|---------------|
| ⚡ FAST | ✓ | ✓ | Basic | - | Single-pass | - |
| ⚖️ BALANCED | ✓ | ✓ | Hybrid | ✓ | Single-pass | - |
| 🎯 PRECISE | ✓ | - | Multi-hop | ✓ | Multi-agent | ✓ Self-RAG |
| 🔍 EXPERT | ✓ | - | Web+Local | - | Synthesis | - |
| 🗳️ CONSENSUS | ✓ | - | Standard | - | 3-LLM Voting | - |

---

## 6-Layer Architecture (TOMAC-RAG Full)

```
┌─────────────────────────────────────────────────────────┐
│ L1: ROUTING                                             │
│ Query classification → Strategy selection               │
│ (User tier × Template × Complexity)                     │
├─────────────────────────────────────────────────────────┤
│ L2: CACHE                                               │
│ Semantic similarity matching (80% hit rate target)      │
│ Redis + Embedding similarity                            │
├─────────────────────────────────────────────────────────┤
│ L3: RETRIEVAL                                           │
│ Vector + Keyword + RRF Fusion (adaptive depth)          │
│ Qdrant + PostgreSQL + Optional Reranker                 │
├─────────────────────────────────────────────────────────┤
│ L4: CRAG EVALUATION                                     │
│ T5-Large evaluator + Web search fallback                │
│ Correct/Ambiguous/Incorrect classification              │
├─────────────────────────────────────────────────────────┤
│ L5: GENERATION                                          │
│ Template-specific LLM responses                         │
│ Single-pass / Multi-agent / Voting                      │
├─────────────────────────────────────────────────────────┤
│ L6: VALIDATION                                          │
│ Self-RAG reflection + Citation checking                 │
│ Hallucination detection + Auto-escalation               │
└─────────────────────────────────────────────────────────┘
```

---

## User Tier Access Control

> **Note**: Tier determines ACCESS to strategies, not COST.
> Cost is determined by strategy + model selection.

| Tier | RAG Access | Max Strategy | Cache TTL |
|------|------------|--------------|-----------|
| Anonymous | NO | - | - |
| User | YES | BALANCED | 48h |
| Editor | YES | PRECISE | 72h |
| Admin | YES | CONSENSUS + CUSTOM | 168h |
| Premium | YES | CONSENSUS | 336h |

---

## Key Parameters (Configurable)

### POC Parameters (Current)

| Parameter | Value | Effect |
|-----------|-------|--------|
| `rrf_k` | 60 | RRF smoothing factor |
| `vector_weight` | 0.7 | Semantic search weight |
| `keyword_weight` | 0.3 | Keyword search weight |
| `top_k` | 10 | Retrieved chunks |
| `embedding_dims` | 3072 | Vector dimensions |
| `temperature` | 0.7 | LLM creativity |
| `circuit_breaker_threshold` | 5 | Failures before failover |
| `circuit_breaker_duration` | 30s | Recovery time |

### TOMAC-RAG Parameters (Planned)

| Parameter | Value | Effect |
|-----------|-------|--------|
| `semantic_cache_threshold` | 0.95 | Cache hit similarity |
| `crag_relevance_threshold` | 0.7 | CRAG evaluation gate |
| `complexity_threshold_fast` | 0.3 | FAST strategy cutoff |
| `complexity_threshold_balanced` | 0.7 | BALANCED cutoff |
| `self_rag_enabled` | true | Enable L6 validation |

---

## Docker Services

| Service | Port | Purpose | Status |
|---------|------|---------|--------|
| PostgreSQL | 5432 | FTS + Storage | Running |
| Qdrant | 6333 | Vector DB | Running |
| Redis | 6379 | Cache | Running |
| Ollama | 11434 | Local LLM | Running |
| Embedding | 8000 | text-embedding-3-large | Running |
| SmolDocling | 8002 | VLM PDF extraction | Running |
| Reranker | 8001 | bge-reranker-v2-m3 | Ready |
| Orchestrator | 8003 | LangGraph | Partial |

---

## Documentation Structure

### Core Docs
| Doc | Description |
|-----|-------------|
| [HOW-IT-WORKS.md](HOW-IT-WORKS.md) | System walkthrough |
| [15-technical-reference.md](15-technical-reference.md) | Code-level reference |

### Layer Docs
| Doc | Description |
|-----|-------------|
| [02-layer1-routing.md](02-layer1-routing.md) | L1: Strategy routing |
| [03-layer2-caching.md](03-layer2-caching.md) | L2: Semantic cache |
| [04-layer3-retrieval.md](04-layer3-retrieval.md) | L3: Hybrid search |
| [05-layer4-crag-evaluation.md](05-layer4-crag-evaluation.md) | L4: CRAG quality gate |
| [06-layer5-generation.md](06-layer5-generation.md) | L5: LLM generation |
| [07-layer6-validation.md](07-layer6-validation.md) | L6: Self-RAG validation |

### Implementation
| Doc | Description |
|-----|-------------|
| [10-implementation-guide.md](10-implementation-guide.md) | Code examples |
| [11-testing-strategy.md](11-testing-strategy.md) | Test specs |
| [12-monitoring-metrics.md](12-monitoring-metrics.md) | Prometheus + Grafana |
| [13-deployment-rollout.md](13-deployment-rollout.md) | 12-week roadmap |

### Appendices
| Doc | Description |
|-----|-------------|
| [appendix/E-model-pricing-2026.md](appendix/E-model-pricing-2026.md) | LLM pricing |
| [appendix/F-calculation-formulas.md](appendix/F-calculation-formulas.md) | Token/cost formulas |
| [appendix/G-admin-configuration-system.md](appendix/G-admin-configuration-system.md) | Admin config |
| [variants/README.md](variants/README.md) | 15 RAG variants catalog |

### Future (Roadmap)
| Doc | Description |
|-----|-------------|
| [future/plugins/](future/plugins/) | Plugin system design (not yet implemented) |

---

## Frontend Components

Location: `apps/web/src/components/rag-dashboard/`

| Component | Purpose |
|-----------|---------|
| `rag-data.ts` | **Single Source of Truth** |
| `RagDashboard.tsx` | Main dashboard (tabbed layout) |
| `PocStatus.tsx` | POC vs TOMAC-RAG status |
| `ParameterGuide.tsx` | Parameters & strategies guide |
| `TechnicalReference.tsx` | Code examples, infrastructure |
| `StrategySelector.tsx` | Strategy selection UI |
| `CostCalculator.tsx` | Cost projection tool |

### Access the Dashboard

```
http://localhost:3000/rag
```

---

## Key Metrics

| Metric | Baseline | Target | Current (POC) |
|--------|----------|--------|---------------|
| Rule lookup accuracy | 80% | 95% | ~85% |
| Strategy accuracy | 75% | 90% | ~80% |
| Avg tokens/query | 2,000 | 1,310 | ~2,500 |
| Cache hit rate | 40% | 80% | ~50% |
| Monthly cost (100K) | $800 | $419 | ~$500 |

---

## Infographic

Download the visual architecture diagram:
- **PDF**: [meepleai-rag-architecture.pdf](meepleai-rag-architecture.pdf)
- **Web**: [/docs/meepleai-rag-architecture.pdf](/docs/meepleai-rag-architecture.pdf)

---

## FAQ

**Q: What's the difference between POC and TOMAC-RAG?**
A: POC implements Hybrid Search + LLM Generation. TOMAC-RAG adds L1 Routing, L2 Semantic Cache, L4 CRAG evaluation, and L6 Self-RAG validation.

**Q: Which strategy should I use?**
A:
- Simple FAQ → FAST (free, <200ms)
- Complex rules → BALANCED (validated)
- Critical decisions → PRECISE (multi-agent)
- External info needed → EXPERT (web search)
- High-stakes arbitration → CONSENSUS (3-LLM voting)

**Q: Where is the single source of truth?**
A: `apps/web/src/components/rag-dashboard/rag-data.ts`

**Q: Can anonymous users access RAG?**
A: No. Authentication required.

---

**Last Updated**: 2026-02-04
**Version**: 3.0
**Status**: POC Running | TOMAC-RAG Planning

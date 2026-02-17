# RAG Strategy Overview

**MeepleAI Rules Agent** - Token-Optimized Modular Adaptive Corrective RAG (TOMAC-RAG)

---

## 🎯 Executive Summary

TOMAC-RAG combines best practices from 36 analyzed RAG variants into a unified 6-layer architecture with **6 routing strategies** optimized for board game Rules Agent.

### Key Results

| Metric | Target | Baseline | Improvement |
|--------|--------|----------|-------------|
| **Accuracy (rule lookup)** | 95% | 80% | +15% |
| **Accuracy (strategy)** | 90% | 75% | +15% |
| **Avg Tokens/Query** | 1,310 | 2,000 | -35% |
| **Monthly Cost** (100K) | $419 | $800 | -48% |
| **Cache Hit Rate** | 80% | 40% | +100% |

### Cost Projection

**$419/month** (100K queries) breakdown:
- LLM: $294
- Embeddings: $50
- Infrastructure: $75

**Savings**: Token optimizations (-$576) offset quality investments (+$95)

---

## 🎯 6 Routing Strategies

| Strategy | Tokens | Cost | Latency | Accuracy | Use Case |
|----------|--------|------|---------|----------|----------|
| 🚀 **FAST** | 2,060 | $0.008 | <200ms | 78-85% | FAQ semplici, risposte rapide |
| ⚖️ **BALANCED** | 2,820 | $0.011 | 1-2s | 85-92% | Query standard + CRAG validation |
| 🎯 **PRECISE** | 22,396 | $0.095 | 5-10s | 95-98% | Multi-agent pipeline, decisioni critiche |
| 🔬 **EXPERT** | 15,000 | $0.065 | 8-15s | 92-96% | Web search + multi-hop reasoning |
| 🗳️ **CONSENSUS** | 18,000 | $0.078 | 10-20s | 97-99% | Multi-LLM voting (3 voters + aggregator) |
| ⚙️ **CUSTOM** | Variable | Variable | Variable | Variable | Configurazione admin personalizzata |

### Strategy Phases

```
FAST        → [Synthesis]
BALANCED    → [Synthesis] + [CRAG Evaluation]
PRECISE     → [Retrieval] + [Analysis] + [Synthesis] + [Validation]
EXPERT      → [Web Search] + [Multi-Hop] + [Synthesis]
CONSENSUS   → [Voter1] + [Voter2] + [Voter3] + [Aggregator]
CUSTOM      → [Synthesis] + any combination (Admin configured)
```

---

## 👥 User Tier Access

> **Note**: Anonymous users cannot access the RAG system. Authentication is required.

| Tier | Strategies Available | Token Budget | Models |
|------|---------------------|--------------|--------|
| ~~Anonymous~~ | ❌ **NO ACCESS** | - | - |
| **User** | FAST, BALANCED | 3,000 | Free + GPT-4o-mini |
| **Editor** | FAST, BALANCED, PRECISE | 5,000 | + Haiku, Sonnet |
| **Admin** | ALL (incl. EXPERT, CONSENSUS, CUSTOM) | 15,000 | Full access (Opus) |
| **Premium** | ALL | 20,000+ | Priority access |

---

## 🎓 How TOMAC-RAG Works

**New to the system?** Start here: **[How It Works - Complete Explanation](HOW-IT-WORKS.md)**

### Quick Summary

TOMAC-RAG intelligently routes queries through 6 layers:

1. **Routing**: User tier + Template (rule/strategy) + Complexity (0-5) → Strategy selection
2. **Cache**: Semantic similarity matching → 80% hit rate (50-986 tokens)
3. **Retrieval**: Adaptive depth (Vector → Hybrid → Multi-hop based on strategy)
4. **CRAG**: T5 evaluator gates quality (Correct/Ambiguous/Incorrect), web search augmentation
5. **Generation**: Template-specific (Direct → Structured → Multi-agent)
6. **Validation**: Self-checking (Citation → Alignment → Reflection)

**Result**: Right strategy for each query type. Simple questions stay simple (50-2,060t), complex strategic queries get premium analysis (22,396t).

**Key Insight**: Adding quality features (CRAG, Self-RAG) **reduces** total cost through aggressive optimization (cache 80%, contextual embeddings -30%, CRAG filtering -40-70%).

---

## 🏗️ 6-Layer Architecture

```
L1: ROUTING → User tier + Template + Complexity
L2: CACHE → Semantic similarity (80% hit)
L3: RETRIEVAL → Vector/Hybrid/Multi-hop
L4: CRAG → Evaluator + Web search + Filter
L5: GENERATION → Direct/Structured/Agentic
L6: VALIDATION → Citation/Alignment/Reflection
```

---

## 📚 Documentation

### Quick Links
- **[README](README.md)** - Navigation guide
- **[Dashboard](index.html)** - Interactive explorer
- **[Architecture](01-tomac-rag-architecture.md)** - System design
- **[Implementation](10-implementation-guide.md)** - Code guide
- **[36 Variants](appendix/B-variant-comparison.md)** - Comparison matrix

### Research Docs (claudedocs/)
- [Tri-level Research](../../../claudedocs/research_rag_strategies_20260131.md)
- [5-Tier Integration](../../../claudedocs/rag_5tier_integrated_strategy.md)
- [36 Variants Catalog](../../../claudedocs/rag_complete_variants_token_costs.md)

---

## 🚀 Next Steps

1. Review research findings
2. Approve TOMAC-RAG design
3. Implement Phase 1 (cache + FAST, 4 weeks)
4. Measure baseline metrics
5. Gradual rollout (Admin → Editor → User → Premium)

---

**Status**: Research Complete | Design Approved | Implementation Pending

# RAG Strategy Overview

**MeepleAI Rules Agent** - Token-Optimized Modular Adaptive Corrective RAG (TOMAC-RAG)

---

## 🎯 Executive Summary

TOMAC-RAG combines best practices from 36 analyzed RAG variants into a unified 6-layer architecture optimized for board game Rules Agent.

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
5. Gradual rollout (Admin → Editor → User → Anonymous)

---

**Status**: Research Complete | Design Approved | Implementation Pending

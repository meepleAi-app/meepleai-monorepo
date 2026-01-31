# RAG Strategy Documentation - MeepleAI

**TOMAC-RAG**: Token-Optimized Modular Adaptive Corrective RAG

---

## 🚀 Quick Start

### For Developers
1. **Start Here**: [00-overview.md](00-overview.md) - Executive summary
2. **Understand**: [01-tomac-rag-architecture.md](01-tomac-rag-architecture.md) - System architecture
3. **Implement**: [10-implementation-guide.md](10-implementation-guide.md) - Step-by-step guide

### For Architects
1. **Explore**: [appendix/B-variant-comparison.md](appendix/B-variant-comparison.md) - 36 variants comparison
2. **Analyze**: [appendix/C-token-cost-breakdown.md](appendix/C-token-cost-breakdown.md) - Token costs
3. **Decide**: [index.html](index.html) - Interactive dashboard with calculator

### For Product/Business
1. **ROI**: [00-overview.md](00-overview.md#performance-targets) - Performance targets & costs
2. **Timeline**: [13-deployment-rollout.md](13-deployment-rollout.md) - 12-week roadmap

---

## 📊 Interactive Dashboard

**→ [Open HTML Dashboard](index.html) ←**

Features:
- 36 variants comparison table (sortable, filterable)
- Token & cost calculator (adjust parameters, see real-time estimates)
- Architecture diagrams (visual 6-layer system)
- Research sources (53 citations with links)
- Navigation to all documentation

---

## 📚 Documentation Structure

### Core Architecture (01-09)
- **[00-overview.md](00-overview.md)** - Executive summary with performance targets
- **[01-tomac-rag-architecture.md](01-tomac-rag-architecture.md)** - Complete 6-layer design
- **[02-layer1-routing.md](02-layer1-routing.md)** - User-tier + Template + Complexity routing
- **[03-layer2-caching.md](03-layer2-caching.md)** - Semantic cache (80% hit rate target)
- **[04-layer3-retrieval.md](04-layer3-retrieval.md)** - Modular retrieval (FAST/BALANCED/PRECISE)
- **[05-layer4-crag-evaluation.md](05-layer4-crag-evaluation.md)** - CRAG evaluator + decompose-recompose
- **[06-layer5-generation.md](06-layer5-generation.md)** - Template-specific generation
- **[07-layer6-validation.md](07-layer6-validation.md)** - Self-validation + auto-escalation
- **[08-token-optimization.md](08-token-optimization.md)** - 10 optimization techniques
- **[09-multi-agent-orchestration.md](09-multi-agent-orchestration.md)** - LangGraph 3-agent system

### Implementation (10-13)
- **[10-implementation-guide.md](10-implementation-guide.md)** - Code examples, setup, deployment
- **[11-testing-strategy.md](11-testing-strategy.md)** - Unit/integration/performance tests
- **[12-monitoring-metrics.md](12-monitoring-metrics.md)** - Prometheus + Grafana setup
- **[13-deployment-rollout.md](13-deployment-rollout.md)** - 12-week phased deployment

### Appendices
- **[appendix/A-research-sources.md](appendix/A-research-sources.md)** - 53 research sources
- **[appendix/B-variant-comparison.md](appendix/B-variant-comparison.md)** - 36 variants matrix
- **[appendix/C-token-cost-breakdown.md](appendix/C-token-cost-breakdown.md)** - Detailed token analysis
- **[appendix/D-fine-tuning-crag-evaluator.md](appendix/D-fine-tuning-crag-evaluator.md)** - T5 training guide

---

## 🔬 Research Foundation

**Research Docs** (in `claudedocs/`):
1. [research_rag_strategies_20260131.md](../../../claudedocs/research_rag_strategies_20260131.md) - Initial tri-level research
2. [rag_5tier_integrated_strategy.md](../../../claudedocs/rag_5tier_integrated_strategy.md) - 5-tier + user-tier integration
3. [rag_architectures_comparison_taxonomy.md](../../../claudedocs/rag_architectures_comparison_taxonomy.md) - 14 architectures
4. [rag_complete_variants_token_costs.md](../../../claudedocs/rag_complete_variants_token_costs.md) - 36 variants catalog
5. [tomac_rag_final_design.md](../../../claudedocs/tomac_rag_final_design.md) - Final design spec

**PDF Sources**:
- [Approcci LLM per agenti di giochi da tavolo.pdf](../../../data/pdfDocs/Approcci%20LLM%20per%20agenti%20di%20giochi%20da%20tavolo.pdf) - Multi-agent research

---

## 📈 Key Metrics

| Metric | Current | Target | Improvement |
|--------|---------|--------|-------------|
| **Accuracy (rule lookup)** | 80% | 95% | +15% |
| **Accuracy (strategy)** | 75% | 90% | +15% |
| **Avg Tokens/Query** | 2,000 | 1,310 | -35% |
| **Monthly Cost** (100K queries) | $800 | $419 | -48% |
| **Cache Hit Rate** | 40% | 80% | +100% |

---

## 🏗️ Architecture Summary

**6-Layer System**:
1. **Routing**: User tier + Template + Complexity → Strategy
2. **Cache**: Semantic similarity (80% hit rate)
3. **Retrieval**: Vector/Hybrid/Multi-hop (adaptive)
4. **CRAG**: Evaluator + Web search + Decompose-recompose
5. **Generation**: Direct/Structured/Agentic (template-specific)
6. **Validation**: Citation/Alignment/Reflection (self-correcting)

**3 Strategies**:
- **FAST** (60-70%): ~2,060 tokens, $0.008, <200ms, 78-85% accuracy
- **BALANCED** (25-30%): ~2,820 tokens, $0.011, 1-2s, 85-92% accuracy
- **PRECISE** (5-10%): ~22,396 tokens, $0.095, 5-10s, 95-98% accuracy

---

## 🛠️ Implementation Status

- [x] **Phase 0: Research** (Complete) - 36 variants, 53 sources, 5 design docs
- [x] **Phase 0: Documentation** (Complete) - 13 docs + appendices + HTML dashboard
- [ ] **Phase 1: Foundation** (Weeks 1-4) - Cache + FAST strategy
- [ ] **Phase 2: Quality** (Weeks 5-8) - CRAG + BALANCED
- [ ] **Phase 3: Advanced** (Weeks 9-12) - Self-RAG + PRECISE + Multi-Agent

---

## ❓ FAQ

**Q: Why 36 variants if implementing only 5-6?**
A: Comprehensive research prevents lock-in, enables informed decisions, provides options for future optimization.

**Q: How does this integrate with ADR-007 (Hybrid LLM)?**
A: TOMAC-RAG extends ADR-007 by adding query-complexity routing. User-tier routing remains, model selection now considers both user tier AND query strategy.

**Q: What's the cost increase?**
A: Paradoxically, cost DECREASES (-48%) due to aggressive caching and token optimizations, despite adding CRAG and Self-RAG quality enhancements.

**Q: Can I skip CRAG/Self-RAG and just use cache + metadata?**
A: Yes! Phase 1 (cache + metadata + contextual embeddings) provides 80% of benefits for 20% of effort. CRAG/Self-RAG add final 15% accuracy gain for mission-critical queries.

---

## 📞 Support

- **Questions**: Create issue with label `rag-strategy`
- **Bug Reports**: Issue with label `bug:rag`
- **Feature Requests**: Issue with label `enhancement:rag`

---

**Last Updated**: 2026-01-31
**Version**: 1.0
**Status**: Design Complete | Implementation Pending

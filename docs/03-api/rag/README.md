# RAG Strategy Documentation - MeepleAI

**TOMAC-RAG**: Token-Optimized Modular Adaptive Corrective RAG

---

## 🚀 Quick Start

### For Developers
1. **Start Here**: [00-overview.md](00-overview.md) - Executive summary
2. **Understand**: [HOW-IT-WORKS.md](HOW-IT-WORKS.md) - Complete system walkthrough
3. **Implement**: [10-implementation-guide.md](10-implementation-guide.md) - Step-by-step guide

### For Architects
1. **Explore**: [appendix/B-variant-comparison.md](appendix/B-variant-comparison.md) - 36 variants comparison
2. **Analyze**: [appendix/F-calculation-formulas.md](appendix/F-calculation-formulas.md) - Token/cost formulas
3. **Configure**: [appendix/G-admin-configuration-system.md](appendix/G-admin-configuration-system.md) - Admin system

### For Product/Business
1. **ROI**: [00-overview.md](00-overview.md#performance-targets) - Performance targets & costs
2. **Pricing**: [appendix/E-model-pricing-2026.md](appendix/E-model-pricing-2026.md) - Current model costs
3. **Timeline**: [13-deployment-rollout.md](13-deployment-rollout.md) - 12-week roadmap

---

## 📊 Interactive Dashboard

**→ [Open HTML Dashboard](index.html) ←**

Features:
- 6 strategies comparison (sortable, filterable)
- Token & cost calculator (real-time estimates)
- Architecture diagrams (visual 6-layer system)
- Research sources (53 citations with links)

---

## 🎯 6 Routing Strategies

| Strategy | Tokens | Cost | Latency | Accuracy | Usage |
|----------|--------|------|---------|----------|-------|
| ⚡ **FAST** | 2,060 | $0.0001 | <200ms | 78-85% | 60-70% |
| ⚖️ **BALANCED** | 2,820 | $0.01 | 1-2s | 85-92% | 25-30% |
| 🎯 **PRECISE** | 22,396 | $0.132 | 5-10s | 95-98% | 5-10% |
| 🔍 **EXPERT** | 15,000 | $0.099 | 8-15s | 92-96% | 2-5% |
| 🗳️ **CONSENSUS** | 18,000 | $0.09 | 10-20s | 97-99% | 1-3% |
| ⚙️ **CUSTOM** | Variable | Variable | Variable | Variable | <1% |

**Source of Truth**: `apps/web/src/components/rag-dashboard/rag-data.ts`

---

## 🔐 User Tier Access

> **IMPORTANT**: User tier affects **ACCESS CONTROL ONLY**, not cost.
> Cost is determined by **STRATEGY + MODEL selection**.
> See [Appendix E](appendix/E-model-pricing-2026.md) for pricing.

| Tier | Access | Max Strategy | Cache TTL |
|------|--------|--------------|-----------|
| 🚫 Anonymous | **NO ACCESS** | - | - |
| 👤 User | ✅ | BALANCED | 48h |
| ✏️ Editor | ✅ | PRECISE | 72h |
| 👑 Admin | ✅ | CONSENSUS | 168h |
| ⭐ Premium | ✅ | CONSENSUS | 336h |

---

## 🏗️ 6-Layer Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Query → L1 Routing → Strategy Selection                │
├─────────────────────────────────────────────────────────┤
│  L2 Cache │ Semantic similarity (80% hit rate target)   │
├─────────────────────────────────────────────────────────┤
│  L3 Retrieval │ Vector + Hybrid + Multi-hop (adaptive)  │
├─────────────────────────────────────────────────────────┤
│  L4 CRAG │ T5-Large evaluator + web search fallback     │
├─────────────────────────────────────────────────────────┤
│  L5 Generation │ Template-specific LLM responses        │
├─────────────────────────────────────────────────────────┤
│  L6 Validation │ Self-RAG + citation checking           │
└─────────────────────────────────────────────────────────┘
```

---

## 📚 Documentation Structure

### Core Architecture
| Doc | Description |
|-----|-------------|
| [00-overview.md](00-overview.md) | Executive summary with performance targets |
| [HOW-IT-WORKS.md](HOW-IT-WORKS.md) | Complete step-by-step explanation |
| [02-layer1-routing.md](02-layer1-routing.md) | 3D routing (user tier × template × complexity) |
| [03-layer2-caching.md](03-layer2-caching.md) | Semantic cache (80% hit rate) |
| [04-layer3-retrieval.md](04-layer3-retrieval.md) | Modular retrieval strategies |
| [05-layer4-crag-evaluation.md](05-layer4-crag-evaluation.md) | CRAG evaluator + decompose-recompose |
| [06-layer5-generation.md](06-layer5-generation.md) | Template-specific generation |
| [07-layer6-validation.md](07-layer6-validation.md) | Self-validation + auto-escalation |

### Advanced Topics
| Doc | Description |
|-----|-------------|
| [08-token-optimization.md](08-token-optimization.md) | 10 optimization techniques |
| [09-multi-agent-orchestration.md](09-multi-agent-orchestration.md) | LangGraph 3-agent system |
| [14-admin-phase-model-config.md](14-admin-phase-model-config.md) | Per-phase model configuration |

### Implementation
| Doc | Description |
|-----|-------------|
| [10-implementation-guide.md](10-implementation-guide.md) | Code examples, setup, deployment |
| [11-testing-strategy.md](11-testing-strategy.md) | Unit/integration/performance tests |
| [12-monitoring-metrics.md](12-monitoring-metrics.md) | Prometheus + Grafana setup |
| [13-deployment-rollout.md](13-deployment-rollout.md) | 12-week phased deployment |

### Appendices
| Doc | Description |
|-----|-------------|
| [A-research-sources.md](appendix/A-research-sources.md) | 53 research sources |
| [B-variant-comparison.md](appendix/B-variant-comparison.md) | 36 variants comparison matrix |
| [C-token-cost-breakdown.md](appendix/C-token-cost-breakdown.md) | Detailed token analysis |
| [D-data-consistency-audit.md](appendix/D-data-consistency-audit.md) | Data consistency validation |
| [E-model-pricing-2026.md](appendix/E-model-pricing-2026.md) | Current model pricing reference |
| [F-calculation-formulas.md](appendix/F-calculation-formulas.md) | Token/cost calculation formulas |
| [G-admin-configuration-system.md](appendix/G-admin-configuration-system.md) | Admin configuration system |

### RAG Variants (46+ docs)
| Doc | Description |
|-----|-------------|
| [variants/README.md](variants/README.md) | Index of all 46+ variant implementations |
| [variants/adaptive-rag.md](variants/adaptive-rag.md) | Adaptive RAG with 6 strategies |
| [variants/self-rag.md](variants/self-rag.md) | Self-RAG with reflection |
| [variants/crag-corrective.md](variants/crag-corrective.md) | Corrective RAG implementation |

---

## 📈 Key Metrics

| Metric | Baseline | TOMAC-RAG | Improvement |
|--------|----------|-----------|-------------|
| Accuracy (rule lookup) | 80% | 95% | **+15%** |
| Accuracy (strategy) | 75% | 90% | **+15%** |
| Avg Tokens/Query | 2,000 | 1,310 | **-35%** |
| Monthly Cost (100K) | $800 | $2,053* | See note |
| Cache Hit Rate | 40% | 80% | **+100%** |

*Note: Monthly cost increased due to updated 2026 model pricing and multi-agent strategies. See [appendix/E-model-pricing-2026.md](appendix/E-model-pricing-2026.md) for details.

---

## 🛠️ Frontend Components

Location: `apps/web/src/components/rag-dashboard/`

| Component | Description |
|-----------|-------------|
| `RagDashboard.tsx` | Main container with dual view mode |
| `types.ts` | TypeScript interfaces (legacy) |
| `rag-data.ts` | **Single Source of Truth** for all data |
| `types-configurable.ts` | Configurable parameter types |
| `RagConfigurationForm.tsx` | Admin configuration form |

---

## 🛠️ Implementation Status

- [x] **Phase 0: Research** (Complete) - 36 variants, 53 sources
- [x] **Phase 0: Documentation** (Complete) - 14 docs + 7 appendices
- [ ] **Phase 1: Foundation** (Weeks 1-4) - Cache + FAST strategy
- [ ] **Phase 2: Quality** (Weeks 5-8) - CRAG + BALANCED
- [ ] **Phase 3: Advanced** (Weeks 9-12) - Self-RAG + PRECISE + Multi-Agent

---

## ❓ FAQ

**Q: Why 6 strategies instead of 3?**
A: Extended from original 3 (FAST/BALANCED/PRECISE) to include EXPERT (web search), CONSENSUS (multi-LLM voting), and CUSTOM (admin-configured) for complete coverage.

**Q: Why does monthly cost show higher than original estimate?**
A: Original $419 estimate used 2025 pricing. Updated 2026 pricing and inclusion of multi-agent strategies (PRECISE, CONSENSUS) increases cost to ~$2,053 at 100K queries. See Appendix E.

**Q: Can Anonymous users access the system?**
A: No. Authentication is required. Anonymous users receive `AuthenticationRequiredException`.

**Q: Where is the Single Source of Truth for data?**
A: `apps/web/src/components/rag-dashboard/rag-data.ts` contains all canonical values for strategies, layers, pricing, and metrics.

---

## 📞 Support

- **Questions**: Create issue with label `rag-strategy`
- **Bug Reports**: Issue with label `bug:rag`
- **Feature Requests**: Issue with label `enhancement:rag`

---

**Last Updated**: 2026-02-02
**Version**: 2.0
**Status**: Design Complete | Implementation Pending

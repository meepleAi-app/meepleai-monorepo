# 🎨 Visual RAG Strategy Builder - Project Brief

**Session**: 2026-02-02 Brainstorming
**Participants**: User + Claude (PM Agent + Research)
**Status**: ✅ Discovery Complete → Ready for Epic Creation

---

## 🎯 Vision Statement

**Enable Admin users to visually design custom RAG strategies** by dragging and connecting building blocks on a canvas, similar to Figma or n8n workflows.

**Goal**: Make RAG pipeline customization accessible without writing code, while maintaining production-grade quality and validation.

---

## ✅ User Requirements (Validated)

### 1. Interactive Flow Visualization
- ✅ Click on block → Modal popup with explanation
- ✅ View curated examples (static)
- ✅ Test live with real backend (Admin only)
- ✅ Beautiful design (current HTML theme approved)

### 2. Visual Strategy Builder
- ✅ Drag & Drop canvas (like Figma)
- ✅ Block palette with 23+ building blocks
- ✅ Connect blocks with edges
- ✅ Configure block parameters
- ✅ Validate before save

### 3. Building Blocks Required
- ✅ RAG Layers (L1-L6 standard)
- ✅ Agent selectors (4 types)
- ✅ Model selectors (6 models)
- ✅ Conditional branches (If/Else)
- ✅ Parallel execution (Split/Merge)
- ✅ Transform/Filter blocks (23 total identified)

---

## 📦 Deliverables

### 1. Enhanced Flow Visualizations (DONE ✅)

**Files Created**:
- `docs/03-api/rag/diagrams/strategy-flows-comparison.html`
  - Side-by-side all 6 strategies
  - SVG flow diagrams
  - Metrics comparison

- `docs/03-api/rag/diagrams/strategy-matrix-view.html`
  - Matrix view: Layers × Strategies
  - Shows which layers each strategy uses
  - Interactive cells

- `docs/03-api/rag/diagrams/strategy-flow-FAST.html`
  - Detailed FAST strategy (green theme)
  - Block-by-block flow
  - Metrics panel

- `docs/03-api/rag/diagrams/strategy-flow-PRECISE.html`
  - Multi-agent pipeline visualization (purple theme)
  - 5-layer validation detail

- `docs/03-api/rag/diagrams/strategy-flow-CONSENSUS.html`
  - Multi-LLM voting system (red theme)
  - 3 voters + aggregator

**Status**: ✅ Ready to view in browser

### 2. Research Report (DONE ✅)

**File**: `docs/claudedocs/research_rag-building-blocks_2026-02-02.md`

**Key Findings**:
- Analyzed 30+ sources (2024-2026 research)
- Identified **23 production-ready building blocks**
- Categorized into 6 types: Retrieval, Optimization, Ranking, Validation, Agents, Control Flow
- Performance metrics for each block
- Best practices and patterns

**Top Insights**:
- Hybrid Search: +35-48% improvement vs pure vector
- Reranking: +20-40% precision boost
- CRAG: +5-15% accuracy with self-correction
- Multi-Agent: +25-40% task completion

### 3. Technical Specification (DONE ✅)

**File**: `docs/claudedocs/strategy-visual-builder-spec.md`

**Covers**:
- Block click → Explanation modal (detailed UX)
- Static examples + live testing (SSE streaming)
- Drag & Drop canvas (ReactFlow architecture)
- Block palette organization (23 blocks)
- Validation engine rules
- Connection rules matrix
- Strategy serialization format (JSON)
- Database schema
- 10-week implementation roadmap

---

## 🏗️ Architecture Summary

### Building Blocks Catalog (23 Blocks)

#### Tier 1: Essential (7 blocks) - MVP
1. Vector Search - Semantic retrieval
2. Keyword Search (BM25) - Exact matching
3. Hybrid Search - Vector + Keyword fusion
4. Cross-Encoder Reranking - Precision boost
5. CRAG Evaluator - Quality gate
6. Confidence Scoring - Self-assessment
7. Citation Verification - Source validation

#### Tier 2: Advanced (6 blocks)
8. Multi-Hop Retrieval - Iterative deepening
9. Query Rewriting - Improve recall
10. Query Decomposition - Complex queries
11. Metadata Filtering - Pre-filter
12. Document Repacking - Optimize attention
13. Hallucination Detection - Safety

#### Tier 3: Multi-Agent (3 blocks)
14. Sequential Chain - Pipeline pattern
15. Parallel Execution - Concurrent workers
16. Supervisor-Worker - Coordinator pattern

#### Tier 4: Experimental (7 blocks)
17. HyDE - Hypothetical docs (expensive!)
18. RAG-Fusion - Multi-query + RRF
19. Iterative RAG - Adaptive loop
20. GraphRAG - Knowledge graph
21. Web Search - External augmentation
22. Deduplication - Remove redundancy
23. Self-RAG - Reflection tokens

### Visual Builder Tech Stack

```typescript
Frontend:
  - ReactFlow (canvas)
  - React DnD (drag-drop)
  - Zustand (state)
  - shadcn/ui (components)
  - Framer Motion (animations)

Backend:
  - Dynamic pipeline execution from JSON
  - Strategy validation service
  - SSE test endpoint
  - PostgreSQL JSONB storage
```

---

## 🎨 Key UX Patterns

### Pattern 1: Block Explanation
```
Click block → Modal popup → 4 tabs:
├─ Description (what, why, when)
├─ Technical (how it works, code refs)
├─ Examples (before/after)
└─ Research (academic sources)
```

### Pattern 2: Example I/O
```
Strategy card → "View Examples" button → Modal with:
├─ 3-5 static curated examples (instant)
└─ [Button: "Test Live"] (Admin only) → SSE stream
```

### Pattern 3: Visual Builder
```
Palette (left) → Drag block → Canvas (center) → Connect edges → Configure (right)
                                    ↓
                           Validate → Save → Test → Publish
```

---

## 📊 Business Value

### For Users (Editor/Admin)
- **Transparency**: See exactly how their query is processed
- **Customization**: Create strategies for specific use cases
- **Learning**: Understand RAG architecture through visualization
- **Optimization**: Fine-tune for cost/latency/accuracy trade-offs

### For Product
- **Differentiation**: Visual RAG builder is unique feature
- **Flexibility**: Support diverse use cases without code changes
- **Scalability**: Admin can iterate without developer dependency
- **Education**: Built-in learning through explanations

### ROI Estimate
- **Development**: 10 weeks (1 senior developer)
- **Value**: Enable 5-10 custom strategies per month
- **Cost Savings**: Optimize strategies can reduce costs 20-40%
- **User Retention**: Power users love customization

---

## 🎯 Success Metrics

### Adoption Metrics
- **Target**: 50% of Admin users create ≥1 custom strategy (Month 1)
- **Engagement**: 5+ custom strategies created per week
- **Retention**: Custom strategy users have 2× higher retention

### Performance Metrics
- **Canvas**: Smooth 60fps with 50+ blocks
- **Validation**: 100% catch invalid configs
- **Testing**: Live test completes in <30s
- **Accuracy**: Custom strategies achieve 85%+ accuracy

### Quality Metrics
- **Code Coverage**: 85%+ (Vitest + Playwright)
- **Accessibility**: WCAG 2.1 AA compliant
- **Error Rate**: <1% invalid saves

---

## ⚠️ Risks & Mitigations

### Risk 1: Complexity Overwhelms Users
**Mitigation**:
- Provide strategy templates (Quick Start)
- Wizard mode: guided strategy creation
- Recommend default configurations
- Progressive disclosure (hide advanced params)

### Risk 2: Invalid Strategies in Production
**Mitigation**:
- Validation engine with strict rules
- Sandbox testing required before publish
- Admin approval workflow
- Rollback capability

### Risk 3: Performance Issues
**Mitigation**:
- Lazy load block explanations
- Virtualize large canvases
- Debounce validation
- Web Workers for heavy computation

### Risk 4: Backend Execution Complexity
**Mitigation**:
- Epic #3413 (Plugin Architecture) provides foundation
- Start with static strategy definitions
- Phase in dynamic execution

---

## 🔄 Relationship to Epic #3413 (Plugin Architecture)

### Synergy
```
Epic #3434: Tier → Strategy → Model (foundation) ✅ In Progress
                      ↓
Epic #3413: Plugin-Based RAG Pipeline (backend)
                      ↓
Visual Builder Epic: UI for creating plugin-based strategies
                      ↓
Users can create custom RAG flows without code
```

### Why This Matters

**Current** (Epic #3434):
- 6 predefined strategies (FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM)
- Fixed pipelines
- Admin can enable/disable strategies per tier

**After Plugin Architecture** (Epic #3413):
- Strategies are plugin compositions
- Backend can execute arbitrary DAG flows
- Admin can create unlimited strategy variations

**After Visual Builder** (This Epic):
- Admin creates strategies visually (no code)
- Drag blocks → Connect → Configure → Test → Publish
- Users select from Admin-created strategies

---

## 📋 Recommended Next Steps

### Immediate (This Week)
1. ✅ Complete Epic #3434 (Tier-Strategy-Model refactor)
2. ✅ Review visualization files in browser
3. ⬜ Validate specification with team
4. ⬜ Create Epic issue for Visual Builder

### Short-term (Next 2-4 Weeks)
5. ⬜ Start Epic #3413 (Plugin Architecture) - prerequisite
6. ⬜ Design detailed UI mockups for builder
7. ⬜ Prototype ReactFlow canvas
8. ⬜ Write block metadata (23 blocks)

### Medium-term (Next 2-3 Months)
9. ⬜ Implement Tier 1 blocks (MVP)
10. ⬜ Build explanation modal system
11. ⬜ Create static examples dataset
12. ⬜ Implement live testing capability

---

## 💬 Open Questions (For Next Session)

### Technical Decisions
1. **Block Granularity**: How fine-grained should blocks be?
   - Option A: High-level (one block = entire "Hybrid Search")
   - Option B: Low-level (separate blocks for Vector + Keyword + Fusion)
   - **Recommendation**: Start high-level, allow "expand block" later

2. **Conditional Logic**: How complex should If/Else be?
   - Option A: Simple (if confidence > 0.8)
   - Option B: Advanced (complex boolean expressions)
   - **Recommendation**: Start simple, add expression builder later

3. **Parallel Execution**: How to visualize?
   - Option A: Branching edges (like flowchart)
   - Option B: Special "parallel container" nodes
   - **Recommendation**: Parallel container (clearer semantics)

### UX Decisions
4. **Strategy Templates**: What templates to provide?
   - "Simple FAQ Bot" (FAST clone)
   - "Balanced with Reranking" (BALANCED+)
   - "Maximum Quality" (PRECISE clone)
   - "Research Assistant" (EXPERT clone)
   - **Need**: 5-10 templates for quick start

5. **Help System**: How to guide new users?
   - Interactive tutorial?
   - Contextual hints?
   - Video walkthrough?
   - **Recommendation**: All three (progressive)

---

## 📚 Artifacts Created

1. ✅ `strategy-flows-comparison.html` - All strategies side-by-side
2. ✅ `strategy-matrix-view.html` - Layers × Strategies matrix
3. ✅ `strategy-flow-FAST.html` - FAST detail (green)
4. ✅ `strategy-flow-PRECISE.html` - PRECISE detail (purple)
5. ✅ `strategy-flow-CONSENSUS.html` - CONSENSUS detail (red)
6. ✅ `research_rag-building-blocks_2026-02-02.md` - 23 blocks catalog
7. ✅ `strategy-visual-builder-spec.md` - Technical specification
8. ✅ `brainstorm-visual-strategy-builder-brief.md` - This document

---

## ✅ Brainstorming Session Summary

### What We Discovered
- **23 production-ready RAG building blocks** from 2024-2026 research
- **Hybrid Search is the new standard** (+35-48% improvement)
- **Multi-agent patterns** outperform monolithic (+25-40%)
- **Visual builder is feasible** with ReactFlow + existing tech stack

### What We Decided
- **Modal popups** for block explanations (4 tabs)
- **Hybrid examples**: Static + live test option
- **Drag & Drop canvas** for strategy creation
- **All block types** supported (layers, agents, models, control flow)

### What We Created
- ✅ 5 interactive HTML visualizations (browser-ready)
- ✅ Research report (30+ sources, high confidence)
- ✅ Technical specification (90+ implementation details)
- ✅ 10-week implementation roadmap

### What's Next
1. Review visualizations → gather feedback
2. Create Epic issue with specification
3. Complete Epic #3434 backend (prerequisite)
4. Start Epic #3413 plugin architecture (foundation)
5. Build visual strategy builder (Epic TBD)

---

**Brainstorming Status**: ✅ Complete
**Outcome**: Actionable specification ready for development
**Confidence**: High (research-backed, user-validated)
**Next Action**: Create Epic issue and link to #3434, #3413

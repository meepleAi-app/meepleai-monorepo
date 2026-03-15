# RAG Strategies and Architectures: Comprehensive Research Report

**Date**: 2026-03-15
**Purpose**: Evaluate RAG approaches for MeepleAI board game knowledge base
**Stack Context**: .NET 9 backend, Qdrant vector store, Python embedding/reranker services

---

## Table of Contents

1. [TOMAC (Tree of Multi-Agent Collaboration)](#1-tomac-tree-of-multi-agent-collaboration)
2. [Strategy Deep Dives](#2-strategy-deep-dives)
3. [Comparison Matrix](#3-comparison-matrix)
4. [Combinability Analysis](#4-combinability-analysis)
5. [State of the Art 2025-2026](#5-state-of-the-art-2025-2026)
6. [Recommendations for MeepleAI](#6-recommendations-for-meepleai)

---

## 1. TOMAC (Tree of Multi-Agent Collaboration)

### Finding

"TOMAC" as a named framework does not appear in published literature as of March 2026. The concept likely refers to the broader family of **tree-structured multi-agent RAG** systems. The closest published works are:

- **MA-RAG** (Multi-Agent RAG, arXiv 2505.20096, May 2025): Multiple specialized agents collaborate via chain-of-thought reasoning to handle complex information-seeking tasks. Agents divide responsibilities (retrieval, reasoning, verification) and coordinate through structured communication.

- **A-RAG** (arXiv 2602.03442, Feb 2026): Hierarchical retrieval interfaces where agents are organized in a tree, with each level handling different abstraction levels of retrieval.

- **MCTS-RAG** (2025): Integrates Monte Carlo Tree Search into RAG for complex reasoning, using tree-based exploration of retrieval-generation paths.

### How Tree-Based Multi-Agent RAG Works

```
                    [Orchestrator Agent]
                    /        |         \
         [Retriever]    [Reasoner]    [Verifier]
          /      \          |            |
    [Vector]  [Graph]  [Synthesizer]  [Fact-Checker]
```

1. **Orchestrator** decomposes the query into sub-tasks
2. **Retriever agents** handle different retrieval strategies (vector, graph, keyword)
3. **Reasoner agents** synthesize retrieved information
4. **Verifier agents** validate factual accuracy before final output

### Advantages Over Standard RAG

| Aspect | Standard RAG | Multi-Agent Tree RAG |
|--------|-------------|---------------------|
| Query handling | Single retrieval pass | Decomposed sub-queries |
| Error correction | None | Agent-level verification |
| Retrieval strategy | Fixed | Dynamically selected per sub-task |
| Complex reasoning | Limited | Multi-hop via agent collaboration |
| Scalability | Linear | Parallel agent execution |

### Best Use Cases

- Complex multi-hop questions ("Which games by the designer of Catan use worker placement?")
- Questions requiring cross-document reasoning
- High-stakes applications needing verification

### Suitability for MeepleAI

**Moderate fit.** Adds significant complexity. Best reserved for the most complex queries (e.g., cross-game comparisons, multi-step rule interactions). Overkill for simple FAQ retrieval.

---

## 2. Strategy Deep Dives

### 2.1 Corrective RAG (CRAG)

**Architecture:**
```
Query -> Retrieve -> [Evaluator] -> {Correct | Ambiguous | Incorrect}
                         |               |            |
                    Use docs directly   Refine    Web search fallback
                                        query     or expand retrieval
```

The evaluator classifies retrieved documents into three categories, then routes accordingly. A lightweight classifier scores document relevance before the LLM sees them.

| Aspect | Details |
|--------|---------|
| **Pros** | Catches bad retrievals before generation; reduces hallucinations; web search fallback for knowledge gaps |
| **Cons** | Added latency from evaluation step; evaluator accuracy is a bottleneck; web search may not apply in closed-domain |
| **Complexity** | Medium - requires training or prompting a relevance evaluator |
| **Latency** | +50-150ms for evaluation step |
| **Accuracy** | +10-15% over naive RAG on complex queries |
| **Board game fit** | **Good.** Prevents hallucinating rules. Web fallback can check BGG for missing info |

### 2.2 Self-RAG

**Architecture:**
```
Query -> [Retrieve?] -> [Generate segment] -> [Critique tokens]
              ^                                      |
              |                                      v
              +---- Re-retrieve if needed <--- {ISREL, ISSUP, ISUSE}
```

Self-RAG trains the LLM itself to output **reflection tokens** alongside generated text:
- **Retrieve**: Should I retrieve information? (yes/no)
- **ISREL**: Is the retrieved passage relevant?
- **ISSUP**: Does the passage support my generation?
- **ISUSE**: Is the overall response useful?

The model generates segment-by-segment, evaluating at each step whether to retrieve, skip, or re-retrieve.

| Aspect | Details |
|--------|---------|
| **Pros** | Self-aware generation; fine-grained quality control; no external evaluator needed |
| **Cons** | Requires fine-tuned model with reflection tokens; cannot use off-the-shelf LLMs easily; training data expensive |
| **Complexity** | **High** - requires custom model training with expanded vocabulary |
| **Latency** | Comparable to standard RAG (critique tokens are part of generation) |
| **Accuracy** | State-of-the-art on factuality benchmarks; +3-13pp over baselines |
| **Board game fit** | **Moderate.** Powerful but requires custom-trained model. Better suited for organizations with ML training infrastructure |

### 2.3 Adaptive RAG

**Architecture:**
```
Query -> [Complexity Classifier] -> {Simple | Moderate | Complex}
                                       |          |          |
                                   No retrieval  Single    Multi-step
                                   (LLM only)    retrieval  iterative
```

A trained classifier predicts query complexity and routes to the appropriate pipeline. Recent advances (CAR, 2025) use clustering patterns of similarity distances to dynamically determine optimal document count.

| Aspect | Details |
|--------|---------|
| **Pros** | Efficient resource usage; fast for simple queries; thorough for complex ones; 60% token reduction (CAR) |
| **Cons** | Classifier accuracy is critical; misrouting degrades quality; needs training data for classifier |
| **Complexity** | Medium - classifier training + multiple pipeline paths |
| **Latency** | Best case: near-zero (no retrieval). Worst case: multi-step retrieval |
| **Accuracy** | Matches or exceeds fixed-strategy RAG across all complexity levels |
| **Board game fit** | **Excellent.** "What is Catan?" needs no retrieval. "How does trading work in Catan with 5-6 player expansion?" needs multi-step. Perfect for mixed-complexity queries |

### 2.4 Graph RAG

**Architecture:**
```
Documents -> [Entity Extraction] -> [Knowledge Graph Construction]
                                            |
Query -> [Graph Traversal + Vector Search] -> [Subgraph Retrieval]
                                                    |
                                              [LLM Generation]
```

Builds a knowledge graph from documents, capturing entities and relationships. Retrieval combines graph traversal (following relationships) with vector search (semantic similarity). Dual-channel: DPR for unstructured text + GNN for structured graph paths.

| Aspect | Details |
|--------|---------|
| **Pros** | Captures entity relationships; 80% accuracy vs 50% for naive RAG on complex queries; excellent for "global" questions about themes/patterns |
| **Cons** | Expensive graph construction; entity extraction quality matters; graph maintenance overhead; higher storage requirements |
| **Complexity** | **High** - requires NER, relation extraction, graph DB, GNN |
| **Latency** | +200-500ms for graph traversal |
| **Accuracy** | 3.4x improvement on enterprise benchmarks; 72-83% comprehensiveness on global questions |
| **Board game fit** | **Excellent.** Board games have natural entity relationships: Games -> Mechanics -> Designers -> Publishers. "Show me all worker placement games by Uwe Rosenberg" is a natural graph query |

### 2.5 Agentic RAG

**Architecture:**
```
Query -> [Planning Agent] -> [Task Decomposition]
                                    |
              +---------------------+---------------------+
              |                     |                     |
        [Retriever Agent]    [Calculator Agent]    [Web Agent]
              |                     |                     |
              +---------------------+---------------------+
                                    |
                            [Synthesis Agent]
                                    |
                            [Verification Agent]
```

Autonomous agents with planning, reflection, tool use, and collaboration capabilities manage the entire RAG pipeline. Agents decide when to retrieve, which tools to use, and whether to re-query.

| Aspect | Details |
|--------|---------|
| **Pros** | Most flexible; handles diverse query types; tool integration; self-correcting; can handle multi-step reasoning |
| **Cons** | Highest latency; most expensive (multiple LLM calls); hardest to debug; non-deterministic behavior |
| **Complexity** | **Very High** - agent orchestration, tool definitions, state management |
| **Latency** | 2-10x standard RAG (multiple agent turns) |
| **Accuracy** | Highest potential accuracy for complex queries |
| **Board game fit** | **Good for advanced features.** "Plan a game night for 4 players who like strategy games under 90 minutes" benefits from agentic reasoning. Overkill for rule lookups |

### 2.6 Modular RAG

**Architecture:**
```
[Indexing Module] -> [Retrieval Module] -> [Reranking Module] -> [Generation Module]
      |                     |                     |                      |
 (pluggable)           (pluggable)           (pluggable)           (pluggable)
 - Chunker             - Vector search       - Cross-encoder       - Prompt template
 - Embedder            - BM25                - ColBERT             - LLM selection
 - Metadata            - Graph traversal     - LLM-based           - Output format
```

Treats RAG as composable building blocks. Each module is independently replaceable and testable. This is more of an **architectural philosophy** than a specific algorithm.

| Aspect | Details |
|--------|---------|
| **Pros** | Maximum flexibility; independent testing; easy A/B testing; future-proof architecture |
| **Cons** | Requires careful interface design; integration testing complexity; more code to maintain |
| **Complexity** | Medium (architecture) but enables managing complexity of other strategies |
| **Latency** | Depends on module composition |
| **Accuracy** | Depends on module selection |
| **Board game fit** | **Essential.** This is the architecture pattern to adopt. Compose other strategies as modules |

### 2.7 RAPTOR (Recursive Abstractive Processing for Tree-Organized Retrieval)

**Architecture:**
```
Documents -> [Chunk (100 tokens)] -> [Embed (SBERT)] -> [Cluster (GMM)]
                                                              |
                                                        [Summarize clusters]
                                                              |
                                                        [Embed summaries]
                                                              |
                                                        [Cluster again]
                                                              |
                                                          ... repeat ...
                                                              |
                                                        [Root summary]

Result: Tree structure from detailed chunks (leaves) to high-level summaries (root)

Query -> [Search across ALL tree levels] -> [Retrieve multi-granularity context]
```

RAPTOR builds a hierarchical summary tree over your documents. At query time, retrieval can happen at any level: detailed chunks for specific questions, mid-level summaries for moderate questions, root summaries for broad questions.

| Aspect | Details |
|--------|---------|
| **Pros** | Multi-granularity retrieval; handles both specific and broad questions; 20% absolute accuracy improvement on QuALITY benchmark; preserves document-level context |
| **Cons** | Expensive indexing (LLM summarization at each level); tree needs rebuilding when documents change; storage overhead for all summary levels |
| **Complexity** | Medium-High - clustering, recursive summarization, multi-level indexing |
| **Latency** | Retrieval is fast (just search across tree levels). Indexing is slow |
| **Accuracy** | State-of-the-art on multi-step reasoning tasks |
| **Board game fit** | **Excellent.** Board game rulebooks have natural hierarchy: Overview -> Sections -> Specific rules -> Edge cases. RAPTOR mirrors this structure perfectly. "What is Terraforming Mars about?" retrieves root summary. "What happens when you play a card with insufficient resources?" retrieves leaf nodes |

### 2.8 RAG-Fusion / Fusion RAG

**Architecture:**
```
User Query -> [LLM: Generate N query variations]
                        |
              Query 1   Query 2   Query 3   Query 4
                |          |          |          |
            Retrieve   Retrieve   Retrieve   Retrieve
                |          |          |          |
                +----------+----------+----------+
                                |
                    [Reciprocal Rank Fusion (RRF)]
                                |
                    [Merged ranked document list]
                                |
                          [LLM Generate]
```

Generates multiple reformulations of the user query, retrieves separately for each, then merges results using RRF. RRF assigns each document a score: `sum(1 / (k + rank_i))` across all query lists.

| Aspect | Details |
|--------|---------|
| **Pros** | Handles ambiguous queries well; captures multiple query intents; simple to implement; consistently outperforms single-query RAG |
| **Cons** | N-times retrieval cost; LLM call for query generation; diminishing returns beyond 3-4 queries |
| **Complexity** | **Low** - just query generation + RRF merge |
| **Latency** | +200-400ms (query generation + parallel retrievals) |
| **Accuracy** | Wins 2x more questions than classic RAG |
| **Board game fit** | **Good.** "How do I win at Catan?" generates: "Catan winning strategies", "Catan victory points", "Catan endgame tactics" - covering more relevant chunks |

### 2.9 HyDE (Hypothetical Document Embeddings)

**Architecture:**
```
User Query -> [LLM: Generate hypothetical answer document]
                            |
                   [Embed hypothetical doc]
                            |
              [Search with hypothetical embedding]
                            |
                   [Retrieve real documents]
                            |
                      [LLM Generate]
```

Instead of searching with the query embedding directly, HyDE generates a hypothetical answer first, then uses that answer's embedding to find similar real documents. This bridges the semantic gap between short questions and long document passages.

| Aspect | Details |
|--------|---------|
| **Pros** | Bridges query-document semantic gap; no training required; works with any embedding model; significant recall improvement |
| **Cons** | Hypothetical answer may be wrong, biasing retrieval; LLM call adds latency; doesn't work well for factoid questions |
| **Complexity** | **Low** - single LLM call + standard retrieval |
| **Latency** | +300-800ms for hypothetical generation |
| **Accuracy** | +10-20% recall improvement on complex queries |
| **Board game fit** | **Moderate.** Helpful for vague questions like "How does combat work in that space game?" Less useful for precise rule lookups where the query already matches document language |

### 2.10 ColBERT / Late Interaction

**Architecture:**
```
Document Indexing:
  Document -> [BERT encoder] -> [Token-level embeddings stored per document]

Query Time:
  Query -> [BERT encoder] -> [Token-level query embeddings]
                                        |
                              [MaxSim: max similarity between
                               each query token and all doc tokens]
                                        |
                              [Sum MaxSim scores = relevance]
```

Unlike dense retrievers that compress documents into a single vector, ColBERT preserves **token-level embeddings**. At query time, it computes fine-grained token-to-token similarity using the MaxSim operator.

| Aspect | Details |
|--------|---------|
| **Pros** | Token-level precision; precomputed document embeddings; better handling of specific terminology; 6-10x storage reduction in v2 |
| **Cons** | Higher storage than single-vector (though v2 reduces this significantly); requires ColBERT-specific model; less ecosystem support than bi-encoders |
| **Complexity** | Medium - requires ColBERT model + specialized index (RAGatouille simplifies this) |
| **Latency** | Fast retrieval (precomputed docs); slightly slower than single-vector but much faster than cross-encoders |
| **Accuracy** | Superior to bi-encoders, approaching cross-encoder quality at retrieval speed |
| **Board game fit** | **Good.** Board game rules use specific terminology ("meeple", "worker placement", "action points"). Token-level matching excels at these domain-specific terms |

---

## 3. Comparison Matrix

### Overall Comparison

| Strategy | Implementation Complexity | Latency Impact | Accuracy Gain | Best Query Type | Training Required |
|----------|--------------------------|----------------|---------------|-----------------|-------------------|
| CRAG | Medium | +50-150ms | +10-15% | All (with fallback) | Evaluator training or prompting |
| Self-RAG | High | Minimal | +3-13pp | Factual, citation-heavy | Custom model fine-tuning |
| Adaptive RAG | Medium | Variable (best case: 0) | Matches best strategy per query | Mixed complexity | Classifier training |
| Graph RAG | High | +200-500ms | +60% on relational queries | Entity relationships | NER + graph construction |
| Agentic RAG | Very High | 2-10x | Highest for complex | Multi-step reasoning | Agent framework setup |
| Modular RAG | Medium (arch) | Depends | Depends | All (composable) | None (architecture pattern) |
| RAPTOR | Medium-High | Fast retrieval, slow indexing | +20% abs on reasoning | Broad + specific mix | LLM summarization |
| RAG-Fusion | Low | +200-400ms | 2x win rate | Ambiguous queries | None |
| HyDE | Low | +300-800ms | +10-20% recall | Vague/exploratory | None |
| ColBERT | Medium | Fast | Near cross-encoder | Terminology-specific | ColBERT model |

### Suitability for Board Game Knowledge Base

| Strategy | Rule Lookup | FAQ | Game Comparisons | Recommendations | Overall Fit |
|----------|-------------|-----|-------------------|-----------------|-------------|
| CRAG | HIGH | HIGH | MEDIUM | MEDIUM | HIGH |
| Self-RAG | HIGH | MEDIUM | MEDIUM | MEDIUM | MEDIUM |
| Adaptive RAG | HIGH | HIGH | HIGH | HIGH | **EXCELLENT** |
| Graph RAG | MEDIUM | MEDIUM | **EXCELLENT** | **EXCELLENT** | HIGH |
| Agentic RAG | MEDIUM | LOW | HIGH | **EXCELLENT** | MEDIUM |
| Modular RAG | HIGH | HIGH | HIGH | HIGH | **ESSENTIAL** |
| RAPTOR | **EXCELLENT** | HIGH | HIGH | MEDIUM | **EXCELLENT** |
| RAG-Fusion | HIGH | HIGH | MEDIUM | MEDIUM | HIGH |
| HyDE | MEDIUM | MEDIUM | MEDIUM | MEDIUM | MODERATE |
| ColBERT | HIGH | HIGH | MEDIUM | LOW | HIGH |

---

## 4. Combinability Analysis

### Which Strategies Complement Each Other

```
COMPLEMENTARY COMBINATIONS (synergistic):
==========================================

[Modular RAG]  <-- Foundation architecture for all others
     |
     +-- [Adaptive RAG] + [CRAG]
     |       Route by complexity, then correct bad retrievals
     |
     +-- [RAPTOR] + [RAG-Fusion]
     |       Multi-level indexing + multi-query retrieval
     |
     +-- [Graph RAG] + [ColBERT]
     |       Entity relationships + precise term matching
     |
     +-- [HyDE] + [Reranker (cross-encoder)]
             Better recall + precise reranking

REDUNDANT COMBINATIONS (pick one):
===================================
- Self-RAG vs CRAG: Both address retrieval quality; CRAG is simpler
- RAG-Fusion vs HyDE: Both improve recall; Fusion is more reliable
- Full Agentic vs Adaptive: Agentic subsumes Adaptive but costs more
```

### Recommended Combination Layers

```
Layer 1 - Architecture: Modular RAG (composable pipeline)
Layer 2 - Indexing:     RAPTOR (hierarchical summaries) + Graph (entity relations)
Layer 3 - Retrieval:    Hybrid search (dense + sparse) + ColBERT reranking
Layer 4 - Query:        Adaptive routing (simple/moderate/complex)
Layer 5 - Correction:   CRAG-style evaluation before generation
Layer 6 - Generation:   Standard LLM with grounded prompting
Layer 7 - (Optional):   Agentic orchestration for complex multi-step queries
```

### Implementation Priority (Incremental Value)

| Priority | Strategy | Effort | Value | Cumulative Accuracy |
|----------|----------|--------|-------|---------------------|
| P0 | Hybrid Search (dense + BM25) + Cross-encoder reranking | Low | **Massive** | ~75% |
| P1 | Adaptive RAG (complexity routing) | Medium | High | ~82% |
| P2 | RAPTOR (hierarchical indexing) | Medium | High | ~87% |
| P3 | CRAG (retrieval correction) | Medium | Medium | ~90% |
| P4 | RAG-Fusion (multi-query) | Low | Medium | ~92% |
| P5 | Graph RAG (entity relationships) | High | Medium-High (for relational queries) | ~94% |
| P6 | ColBERT (late interaction) | Medium | Moderate | ~95% |
| P7 | Agentic orchestration | Very High | High (for complex queries) | ~97% |

---

## 5. State of the Art 2025-2026

### Key Trends

1. **Agentic RAG is the dominant paradigm** (2026). Systems embed autonomous agents with planning, reflection, tool use, and multi-agent collaboration.

2. **Hybrid retrieval is non-negotiable.** Production systems combine dense vectors + sparse (BM25) + reranking. Hybrid improves recall by 1-9% over vector-only, and reranking adds +33% average accuracy.

3. **Context window expansion challenges RAG assumptions.** Models now support 1-2M tokens. Engineers must decide when direct context processing outperforms retrieval. RAG remains essential for: frequently updated content, large corpora, cost control, and attribution.

4. **Graph RAG is maturing rapidly.** GraphRAG Benchmark accepted at ICLR 2026. Knowledge graphs provide 80% accuracy vs 50% for naive RAG on complex queries.

5. **Modular/composable architectures win.** Qdrant's $50M raise signals the end of rigid RAG architectures. Composability is the architectural future.

6. **Evaluation and observability are mandatory.** 40-60% of RAG implementations fail to reach production. Continuous measurement of retrieval quality (precision, recall) and generation quality (groundedness, hallucination rate) is required.

7. **Chunking strategy matters more than embedding model.** Most performance gains still come from "boring work" on chunking, indexing, and embedding selection.

8. **Semantic caching for cost reduction.** Page-level chunking for accuracy + semantic caching early to reduce LLM API costs.

### Production Architecture Recommendations (2026)

```
PRODUCTION RAG STACK (2026 best practices)
==========================================

Ingestion Pipeline:
  [PDF/HTML] -> [Smart Chunking] -> [Metadata Extraction]
       |              |                     |
  [RAPTOR tree] [Hybrid embeddings]  [Entity extraction]
       |              |                     |
       +--------> [Qdrant Collection] <-----+
                  (dense + sparse + payload)

Query Pipeline:
  [User Query] -> [Complexity Classifier] -> [Router]
                                                |
                    +---------------------------+---------------------------+
                    |                           |                           |
              [Simple: LLM only]      [Moderate: Single retrieval]  [Complex: Multi-step]
                                              |                           |
                                    [Hybrid Search + RRF]          [Decompose + iterate]
                                              |                           |
                                    [Cross-encoder rerank]         [Agent orchestration]
                                              |                           |
                                    [CRAG evaluation]              [CRAG evaluation]
                                              |                           |
                                        [LLM Generate]             [Synthesize + verify]

Observability:
  [Retrieval metrics] + [Generation metrics] + [User feedback] + [A/B testing]
```

### Key Performance Benchmarks (2026)

| Metric | Naive RAG | Hybrid + Rerank | Full Advanced Stack |
|--------|-----------|-----------------|---------------------|
| Answer accuracy | 50-60% | 75-85% | 90-97% |
| Hallucination rate | 15-25% | 5-10% | 1-3% |
| P95 latency | 800ms | 1.2s | 2-5s |
| Cost per query | $0.01 | $0.02 | $0.05-0.15 |

---

## 6. Recommendations for MeepleAI

### Current Stack Assessment

MeepleAI already has:
- Qdrant vector store (supports hybrid search natively)
- Python embedding service (sentence-transformers)
- Python reranker service (cross-encoder)
- .NET 9 backend with CQRS pattern

This is a strong foundation. The reranker service already provides cross-encoder reranking.

### Recommended Implementation Roadmap

#### Phase 1: Strengthen the Foundation (Weeks 1-2)
**Hybrid Search + Improved Chunking**

- Enable Qdrant's native hybrid search (dense + sparse vectors)
- Implement smart chunking for board game rulebooks (respect section boundaries)
- Tune the existing reranker service
- **Expected gain**: +15-25% accuracy over current setup

#### Phase 2: Adaptive Retrieval (Weeks 3-4)
**Query Complexity Routing**

- Build a lightweight classifier (can be prompt-based initially)
- Route simple queries (definitions, single facts) to direct LLM or single retrieval
- Route complex queries to multi-step retrieval with reranking
- **Expected gain**: +7-10% accuracy, 60% token savings on simple queries

#### Phase 3: RAPTOR Indexing (Weeks 5-7)
**Hierarchical Document Structure**

- Build summary trees for rulebooks (game overview -> section summaries -> detailed rules)
- Store multi-level embeddings in Qdrant (use payload metadata for tree level)
- Enable retrieval across abstraction levels
- **Expected gain**: +5-8% accuracy, especially on "what is this game about?" type queries

#### Phase 4: Corrective Retrieval (Weeks 8-9)
**CRAG-Style Evaluation**

- Add relevance evaluation before generation
- Implement confidence scoring on retrieved documents
- Add fallback to expanded retrieval when confidence is low
- **Expected gain**: +3-5% accuracy, significant hallucination reduction

#### Phase 5: Graph Enhancement (Weeks 10-14)
**Knowledge Graph for Game Entities**

- Extract entities: Games, Mechanics, Designers, Publishers, Categories
- Build relationships in a graph structure (can use Qdrant payload + application logic, or add Neo4j)
- Enable relational queries: "games like Catan", "other games by this designer"
- **Expected gain**: +4-6% accuracy on relational queries, enables new query types

#### Phase 6: RAG-Fusion (Week 15)
**Multi-Query Generation**

- Generate 3-4 query variations using the LLM
- Run parallel retrievals, merge with RRF
- Low effort, good gain for ambiguous board game questions
- **Expected gain**: +2-3% accuracy on ambiguous queries

### Architecture Decision

```
RECOMMENDED: Modular RAG with Adaptive Routing
================================================

                    [Query API (.NET)]
                          |
                    [Complexity Router]
                    /        |         \
            [Simple]    [Standard]    [Complex]
               |            |             |
          [LLM only]  [Hybrid Search] [Multi-step]
                           |          [+ RAG-Fusion]
                      [Reranker]          |
                           |         [Reranker]
                      [CRAG eval]         |
                           |         [CRAG eval]
                      [Generate]     [Synthesize]
                           |              |
                    [Response + Sources + Confidence]
```

### What to Skip (For Now)

| Strategy | Why Skip |
|----------|----------|
| Self-RAG | Requires custom model training; too complex for current stage |
| Full Agentic RAG | Overkill for domain-specific KB; add later for advanced features |
| ColBERT | Current reranker service (cross-encoder) provides similar quality; ColBERT adds storage complexity |
| HyDE | RAG-Fusion provides similar benefits with more reliability |

### Cost-Benefit Summary

| Investment | Monthly Cost Impact | Accuracy Impact | User Experience Impact |
|------------|--------------------|-----------------|-----------------------|
| Hybrid search | ~$0 (Qdrant native) | +15-25% | Much better term matching |
| Adaptive routing | ~$0 (routing logic) | +7-10% | Faster simple queries |
| RAPTOR indexing | +$20-50/mo (LLM for summaries) | +5-8% | Better broad questions |
| CRAG evaluation | +$10-30/mo (extra LLM calls) | +3-5% | Fewer wrong answers |
| Graph enhancement | +$0-50/mo (depends on approach) | +4-6% relational | New query capabilities |
| RAG-Fusion | +$15-40/mo (extra retrievals) | +2-3% | Better ambiguous queries |

---

## Sources

### Primary Research Papers
- [Self-RAG: Learning to Retrieve, Generate, and Critique through Self-Reflection](https://arxiv.org/abs/2310.11511) (Asai et al., 2023)
- [RAPTOR: Recursive Abstractive Processing for Tree-Organized Retrieval](https://arxiv.org/abs/2401.18059) (Sarthi et al., 2024, ICLR 2024)
- [Corrective RAG (CRAG)](https://openreview.net/pdf?id=JnWJbrnaUE) (Yan et al., 2024)
- [RAG-Fusion: a New Take on Retrieval-Augmented Generation](https://arxiv.org/abs/2402.03367) (Raudaschl, 2024)
- [Agentic Retrieval-Augmented Generation: A Survey on Agentic RAG](https://arxiv.org/abs/2501.09136) (Singh et al., 2025)
- [MA-RAG: Multi-Agent RAG via Collaborative Chain-of-Thought Reasoning](https://arxiv.org/abs/2505.20096) (2025)
- [A-RAG: Scaling Agentic RAG via Hierarchical Retrieval Interfaces](https://arxiv.org/html/2602.03442v1) (2026)
- [Graph Retrieval-Augmented Generation: A Survey](https://arxiv.org/abs/2408.08921) (2024, accepted ACM TOIS)
- [GraphRAG with Knowledge Graphs](https://arxiv.org/abs/2501.00309) (2025)
- [Cluster-based Adaptive Retrieval (CAR)](https://arxiv.org/abs/2511.14769) (2025)
- [Engineering the RAG Stack: Architecture and Trust Frameworks](https://arxiv.org/html/2601.05264v1) (2026)

### Industry & Technical References
- [ColBERT in Practice: Bridging Research and Industry](https://sease.io/2025/11/colbert-in-practice-bridging-research-and-industry.html) (Sease, 2025)
- [RAG at Scale: Production AI Systems in 2026](https://redis.io/blog/rag-at-scale/) (Redis, 2026)
- [RAG in 2026: Bridging Knowledge and Generative AI](https://squirro.com/squirro-blog/state-of-rag-genai) (Squirro, 2026)
- [State of the Art RAG](https://medium.com/@hardiktaneja_99752/state-of-the-art-rag-e3cb26d9a7c0) (Taneja, 2026)
- [Qdrant Reranking in Hybrid Search](https://qdrant.tech/documentation/advanced-tutorials/reranking-hybrid-search/) (Qdrant docs)
- [10 Types of RAG Architectures in 2026](https://www.rakeshgohel.com/blog/10-types-of-rag-architectures-and-their-use-cases-in-2026) (Gohel, 2026)
- [Beyond Vanilla RAG: 7 Modern Architectures](https://dev.to/naresh_007/beyond-vanilla-rag-the-7-modern-rag-architectures-every-ai-engineer-must-know-4l0c) (Dev.to)
- [Adaptive RAG Explained](https://www.meilisearch.com/blog/adaptive-rag) (Meilisearch, 2026)
- [Late Interaction Models: ColBERT, ColPali, ColQwen](https://weaviate.io/blog/late-interaction-overview) (Weaviate)
- [LFM2-ColBERT-350M](https://www.marktechpost.com/2025/10/28/liquid-ai-releases-lfm2-colbert-350m/) (MarkTechPost, 2025)
- [RAGatouille: ColBERT for RAG](https://github.com/AnswerDotAI/RAGatouille) (GitHub)
- [DO-RAG: Domain-Specific QA with Knowledge Graph-Enhanced RAG](https://arxiv.org/html/2505.17058v1) (2025)
- [CRAG Implementation with LangGraph](https://www.datacamp.com/tutorial/corrective-rag-crag) (DataCamp)
- [Enhancing RAPTOR with Semantic Chunking](https://www.frontiersin.org/journals/computer-science/articles/10.3389/fcomp.2025.1710121/full) (Frontiers, 2025)
- [Advanced RAG: Hybrid Search and RRF](https://glaforge.dev/posts/2026/02/10/advanced-rag-understanding-reciprocal-rank-fusion-in-hybrid-search/) (Glaforge, 2026)
- [Qdrant Composability Shift](https://ragaboutit.com/the-composability-shift-why-qdrants-50m-bet-signals-the-end-of-rigid-rag-architectures/) (RAG About It)

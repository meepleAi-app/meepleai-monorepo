# How TOMAC-RAG Works

**Token-Optimized Modular Adaptive Corrective RAG**

---

## Core Problem

**Naive RAG**: Every query → Retrieve 10 chunks → Send to LLM
**Issue**: Wastes tokens on simple questions, underperforms on complex ones

**TOMAC-RAG Solution**: Route each query to optimal strategy based on:
1. **Who**: User tier (budget)
2. **What**: Template type (rules vs strategy)
3. **How complex**: Complexity score (0-5)

**Note**: Anonymous users cannot access RAG. Authentication required.

---

## 6-Layer Architecture

### Layer 1: Intelligent Routing (Brain)

**Decision Matrix**:

```python
def route_query(query, user) -> Strategy:
    budget = get_token_budget(user.role)     # User: 3K, Editor: 5K, Admin: 15K
    template = classify_template(query)       # rule_lookup, resource_planning
    complexity = score_complexity(query)      # 0-5 based on length, concepts, conditions

    if complexity <= 1 and template == "rule_lookup":
        return "FAST"       # Simple FAQ
    elif complexity <= 3:
        return "BALANCED"   # Moderate complexity
    else:
        return "PRECISE"    # Complex reasoning
```

**Example**:
```
Query: "How many food tokens in Wingspan?"
→ User tier: User (3K budget)
→ Template: rule_lookup
→ Complexity: 1 (simple, clear)
→ Strategy: FAST (Llama 3.3 Free)
→ Tokens: 50 (cache hit) or 2,060 (miss)
```

---

### Layer 2: Semantic Cache (Memory)

**Process**:

```python
async def semantic_cache_lookup(query) -> CacheResult:
    normalized = query.lower().strip()

    # LLM similarity check
    prompt = f"Is '{normalized}' similar to cached: {recent_queries}?"
    result = llm_haiku.generate(prompt, max_tokens=10)

    if result.similar_index:
        return CacheResult(hit=True, answer=cache[index], tokens=50)
    else:
        return CacheResult(hit=False, tokens=310)
```

**Example**:
```
User A: "How many food tokens in Wingspan?"
→ Cache miss, full RAG (2,060t), cache result

User B: "What's the food token count for Wingspan?"
→ Semantic check (310t) → LLM detects similarity
→ Cache HIT! Return cached
→ Total: 310t vs 2,060t (85% savings)
```

**Expected Hit Rate**: 80% for FAQ-heavy domains

---

### Layer 3: Modular Retrieval (Knowledge Fetcher)

#### FAST: Vector-Only (Speed Priority)

```python
async def retrieve_fast(query) -> list[Document]:
    embedding = await embed_minilm(query)  # 14.7ms
    results = await qdrant.search(embedding, limit=3)
    return results  # ~1,500 tokens (3 × 500t chunks)
```

**When**: Simple FAQ, clear terminology
**Trade-off**: 5-8% lower accuracy, 5x faster

#### BALANCED: Hybrid + Metadata (Quality + Efficiency)

```python
async def retrieve_balanced(query, game_id) -> list[Document]:
    embedding = await embed_e5_base(query)  # 79ms, 83-85% accuracy

    # Parallel retrieval
    vector = await qdrant.search(embedding, limit=10)
    keyword = await qdrant.search_bm25(query, limit=10)

    # Metadata filter
    if game_id:
        vector = filter_by_metadata(vector, game_id=game_id)
        keyword = filter_by_metadata(keyword, game_id=game_id)

    # Reciprocal Rank Fusion
    fused = rrf(vector, keyword)
    return fused[:10]  # ~3,500 tokens
```

**When**: Ambiguous queries, complex rules, multi-concept
**Trade-off**: 2x tokens vs FAST, +10-12% accuracy

#### PRECISE: Multi-Hop Adaptive (Maximum Quality)

```python
async def retrieve_precise(query) -> list[Document]:
    # Hop 1: Broad initial (20 chunks, 5,000t)
    hop1 = await qdrant.search(query, limit=20)

    # Hop 2: Entity expansion (extract "combat", "resources")
    entities = extract_entities(hop1)
    hop2 = [await qdrant.search(e, limit=5) for e in entities[:5]]  # +3,000t

    # Hop 3: Cross-reference validation
    hop3 = await retrieve_related(extract_rules(hop1 + hop2))  # +2,000t

    # Deduplicate
    unique = deduplicate_by_id(hop1 + hop2 + hop3)
    return unique[:20]  # ~8,000t after dedup
```

**When**: Multi-step reasoning, strategic planning, rule conflicts
**Trade-off**: 5x tokens vs FAST, 95-98% accuracy

---

### Layer 4: CRAG Evaluation (Quality Gate)

**Purpose**: Prevent hallucinations BEFORE expensive LLM generation

```python
async def crag_evaluate(query, docs) -> CragResult:
    # Step 1: Score each doc (T5-Large evaluator)
    for doc in docs:
        score = await t5_evaluator.score_relevance(query, doc)
        category = "correct" if score >= 0.8 else ("ambiguous" if score >= 0.5 else "incorrect")

    # Step 2: Decide action
    correct = [e for e in evals if e.category == "correct"]

    if len(correct) >= 3:
        final_docs = correct[:5]
        source = "internal"
    elif len(correct) > 0 or len(ambiguous) > 0:
        web_docs = await web_search(query + " official rules FAQ")
        final_docs = await rerank(correct + ambiguous + web_docs, top_k=5)
        source = "internal+web"
    else:
        web_docs = await web_search(query + " board game rules")
        final_docs = await rerank(web_docs, top_k=10)
        source = "web"

    # Step 3: Decompose-recompose (extract key sentences)
    filtered = [filter_sentences(doc, query, keep=0.6) for doc in final_docs]
    return CragResult(docs=filtered, source=source)
```

**Example**:
```
Query: "Can I attack diagonally in chess variant X?"
Retrieved:
  - Doc 1: General chess (0.4) → INCORRECT
  - Doc 2: Variant X overview (0.6) → AMBIGUOUS
  - Doc 3: Variant X movement (0.9) → CORRECT

CRAG Decision:
  → Only 1 correct, 1 ambiguous
  → Trigger web search "chess variant X rules official"
  → Find 5 web results (official site, BGG FAQ)
  → Rerank: 2 internal + 5 web → top-5
  → Decompose: 3,500t → 1,800t (49% reduction)

Result: Higher quality + fewer tokens
```

**Why Critical**: Catches bad retrievals BEFORE generation, not after. Uses T5-Large (0 LLM tokens) to filter.

---

### Layer 5: Adaptive Generation (Answer Creator)

#### rule_lookup Template

**FAST Prompt** (Simple Extraction):
```python
f"""
Extract exact rule text that answers this question.
Include page citation (mandatory).

Rules: {docs}
Question: {query}

Format: [exact rule] (page X)
If not found: "Rule not found."
"""
# Model: Haiku or Llama 3.3 Free
# Tokens: ~1,950 input, ~200 output
```

**BALANCED Prompt** (Synthesis):
```python
f"""
Board game rules expert. Synthesize comprehensive answer.

Retrieved (reranked top-5): {docs}
Question: {query}

Provide:
1. Synthesized answer
2. Citations (page numbers mandatory)
3. Note ambiguities/edge cases

Format: [answer] (pages X, Y, Z)
"""
# Model: Claude Sonnet or GPT-4o-mini
# Tokens: ~3,050 input, ~300 output
```

**PRECISE Prompt** (Chain-of-Thought + Self-RAG):
```python
f"""
Rules expert. Use chain-of-thought reasoning.

Context: {docs}
Question: {query}

Analysis:
1. Identify relevant sections
2. Check conflicts/contradictions
3. Resolve using priority rules
4. Synthesize with reasoning

Provide: Reasoning, Answer, Citations, Confidence (0-1)

Self-critique:
- Are you confident?
- Should you retrieve more context?
"""
# Model: Claude Opus
# Tokens: ~3,150 input, ~500 output + 4,400 reflection
```

#### resource_planning Template

**BALANCED Prompt** (Trade-off Analysis):
```python
f"""
Strategic advisor. Analyze trade-offs.

Rules: {docs}
Decision: {query}

Analysis:
1. Option A: Pros/Cons with citations
2. Option B: Pros/Cons with citations
3. Resource costs (use calculator if needed)
4. Recommendation with reasoning

Tools: calculator(a, b, op)
"""
# Model: Sonnet or GPT-4o-mini
# Tokens: ~3,050 input, ~400 output
```

**PRECISE Prompt** (Multi-Agent):
```python
# Agent 1: Analyzer (Haiku, 400t output)
"""Extract: resources, options, constraints"""

# Agent 2: Strategist (Opus, 500t output)
"""
Given: {analyzer_output} + {docs}
Generate: Strategic recommendation
- Expected value, risk, opportunity cost
- Long-term vs short-term
- Optimal action with reasoning
"""

# Agent 3: Validator (Haiku, 300t output)
"""
Verify: {strategist_output} against {docs}
- Legal per rules?
- Resource costs accurate?
- Confidence (0-1)
"""

# Coordinator: Fuse agent outputs
```

**Why Multi-Agent**: Separation of concerns (Analyzer extracts, Strategist reasons, Validator verifies)

---

### Layer 6: Self-Validation (Quality Checker)

#### FAST: Rule-Based (0 tokens, instant)

```python
def validate_fast(answer, template) -> bool:
    if template == "rule_lookup":
        has_citation = bool(re.search(r'\(page \d+\)', answer))
        if not has_citation: return False

    if len(answer.split()) < 10: return False  # Too short
    return True
```

**Why**: Instant regex checks catch obvious problems (missing citations, truncated)

#### BALANCED: Cross-Encoder Alignment (0 LLM tokens)

```python
async def validate_balanced(answer, docs) -> float:
    pairs = [(answer, doc.content) for doc in docs]
    scores = await cross_encoder.score_pairs(pairs)
    avg_alignment = sum(scores) / len(scores)

    if avg_alignment < 0.6: return 0.6  # Low confidence
    if detect_contradictions(docs): return 0.7  # Medium
    return avg_alignment
```

**Why**: Cross-encoder checks factual grounding without LLM tokens

#### PRECISE: Self-RAG Reflection (4,400 tokens)

```python
async def validate_precise(answer, docs, query) -> dict:
    reflection_prompt = f"""
    Self-critique:
    Answer: {answer}
    Based on: {docs}
    Query: {query}

    Evaluate:
    1. Relevance: Docs relevant? (Yes/Partial/No)
    2. Support: Answer supported? (Yes/Partial/No)
    3. Usefulness: Satisfies user? (Yes/Maybe/No)

    Confidence: 0-1
    Re-retrieve: If any Partial/No/Maybe OR confidence <0.9
    Refined query: What would improve?

    JSON: {relevance, support, usefulness, confidence, re_retrieve, refined_query}
    """

    critique = await llm_opus.generate(reflection_prompt)

    if critique.re_retrieve and critique.confidence < 0.9:
        # Self-correct: retrieve more, regenerate
        new_docs = await retrieve_precise(critique.refined_query)
        return await generate_precise(new_docs, query)

    return critique
```

**Example**:
```
Query: "Should I build settlement or city in Catan with 3 ore, 2 wheat?"
Initial: "Build settlement (costs 1 wood, 1 brick, 1 wheat, 1 sheep)"

Self-reflection:
→ Support: Partial (user doesn't have sheep!)
→ Confidence: 0.4 (low, answer wrong)
→ Re-retrieve: Yes
→ Refined: "Catan building costs settlement vs city"

New retrieval: "City costs 3 ore + 2 wheat"
New answer: "Build city (you have exactly these resources)"
Confidence: 0.95
```

**Why**: System catches its own errors, self-corrects automatically

---

## Strategy Comparison

| Strategy | Retrieval | Model | Validation | Tokens | Accuracy | Latency | Cost |
|----------|-----------|-------|------------|--------|----------|---------|------|
| **FAST** | Vector-only (3 chunks) | Haiku/Llama Free | Regex | 2,060 | 78-85% | 200ms | $0.001 |
| **BALANCED** | Hybrid (10 chunks) + CRAG | Sonnet/GPT-4o-mini | Cross-encoder | 2,820 | 85-92% | 1-2s | $0.011 |
| **PRECISE** | Multi-hop (20 chunks) | Opus Multi-Agent | Self-RAG | 7,420-12,900 | 95-98% | 5-10s | $0.028-0.095 |

---

## Agent-Specific Configurations

### Rules Agent (MeepleAI Primary)

**Query Profile**: 70% rule_lookup, 30% resource_planning

**Distribution**:
- 80% → Cache (50-986t, instant, $0)
- 18% → FAST + Contextual embeddings (1,950t, 200ms, $0.001)
- 8% → BALANCED + CRAG (2,625t, 1-2s, $0.011)
- 2% → PRECISE + Self-RAG (7,420t, 5s, $0.028)

**Result**: Avg 900t/query, 92% accuracy, $300/mo (100K queries)

**Why BALANCED + CRAG**:
- ✅ 85-92% accuracy (legal liability)
- ✅ Citations mandatory
- ✅ 1-2s acceptable latency
- ✅ $0.011 affordable at scale
- vs FAST: Higher accuracy critical
- vs PRECISE: Overkill for 90% FAQ

---

### Strategy Agent (Planning Focus)

**Query Profile**: 100% resource_planning (all strategic)

**Distribution**:
- 25% → Semantic Cache (986t)
- 40% → BALANCED + Structured (2,820t, trade-off analysis)
- 30% → PRECISE + Self-RAG (7,420t, multi-step planning)
- 5% → PRECISE + Multi-Agent (12,900t, tournament-level)

**Result**: Avg 4,200t/query, 90% accuracy, $1,400/mo

**Why PRECISE + Self-RAG**:
- ✅ Expert advice (users value quality)
- ✅ Confidence scores (build trust)
- ✅ Self-correction (catch errors)
- ✅ 2-5s acceptable (strategic analysis)
- Cost justified: Strategic advice has high value

---

### Setup Agent (Procedural)

**Query Profile**: 100% rule_lookup (setup procedures)

**Distribution**:
- 95% → Memory Cache (50t, instant, ultra-cacheable)
- 3% → FAST + Metadata (2,200t, variants/expansions)
- 2% → Multimodal RAG (8,550t, visual diagrams)

**Result**: Avg 400t/query, 85% accuracy

**Why Memory Cache Dominance**: Setup doesn't change. First query generates, next 1,000 queries = 50K tokens (vs 2M without cache = 40x difference!)

---

### Assistant Agent (Real-Time Gameplay)

**Query Profile**: 100% rule_lookup (quick clarifications)

**Distribution**:
- 95% → Memory Cache (50t, <50ms)
- 5% → FAST only (2,060t, 150-200ms)
- BALANCED/PRECISE: DISABLED (too slow)

**Result**: Avg 300t/query, 75% accuracy, <200ms latency

**Why Sacrifice Accuracy**:
- Latency budget: Gameplay can't pause 2-5s
- Acceptable errors: 75% accurate, users verify quickly
- Volume: High query rate, cost adds up
- Alternative: Ask detailed question post-game (BALANCED)

**Design**: Real-time → speed, post-game → accuracy

---

## Token Efficiency Principles

### Principle 1: Cache Everything Cacheable

**80/20 Rule**: 80% queries from 20% patterns (board game FAQ)

**Example**:
```
Top 20 Wingspan FAQ: 100K queries/mo

Without cache: 100K × 2,000t = 200M tokens ($600/mo)
With cache: 20 × 2,000t (gen) + 100K × 50t (hits) = 5.04M tokens ($15/mo)
Savings: 40x reduction!
```

**Insight**: Cache is foundation, not nice-to-have

---

### Principle 2: Filter Context Aggressively

**97% tokens = input context**, 3% = output

**Naive**: Retrieve 10 chunks (5,000t) → Send all to LLM

**TOMAC-RAG**:
```
Retrieve 10 (5,000t)
→ CRAG: Only 3 "correct"
→ Decompose-recompose: Extract key sentences
→ Final: 1,000t (80% reduction!)
→ Better accuracy (no noise)
```

**Insight**: More context ≠ better. **Relevant** context = better.

---

### Principle 3: Cheapest Model That Works

**Model Pricing** (per 1M tokens):
- Llama 3.3 70B: $0 (free)
- Haiku: $0.25 input / $1.25 output
- GPT-4o-mini: $0.15 / $0.60
- Sonnet: $3 / $15
- Opus: $15 / $75

**Naive** (Opus for all): 100K × 2,000t × Opus = $3,000/mo

**TOMAC-RAG** (adaptive):
```
60K cache × 50t × Free = $0
25K FAST × 2,060t × Haiku = $13
13K BALANCED × 2,820t × GPT-4o-mini = $6
2K PRECISE × 7,420t × Opus = $222
────────────────────────────
Total: $241/mo (12x cheaper!)
```

**Insight**: FAQ don't need Opus. Strategic optimization does. Right model for right task.

---

## Token Efficiency Paradox

**Counterintuitive**: Adding quality features (CRAG, Self-RAG) **reduces** total cost!

**Without Optimizations** (naive):
```
100K queries × 2,000t = 200M tokens
Cost: $600/mo
Accuracy: 80%
```

**With TOMAC-RAG**:
```
60K cache hits × 50t = 3M
25K FAST × 2,060t = 51.5M
13K BALANCED × 2,820t = 36.7M (CRAG filters 3,500t → 1,800t)
2K PRECISE × 7,420t = 14.8M
────────────────────────
Total: 106M tokens (47% reduction!)
Cost: $294/mo (51% reduction!)
Accuracy: 92% (+12%)
```

**The Magic**:
1. Cache eliminates 60% retrieval (saves 117M tokens)
2. CRAG filters 40-70% context (saves 15M tokens)
3. Contextual embeddings improve precision (saves 20M tokens)
4. Metadata filtering reduces irrelevant results (saves 10M tokens)

**Net**: -147M tokens saved while improving accuracy!

---

## Decision Flow Example

### Example 1: User, Simple FAQ

```
User: User tier
Query: "How many players in Wingspan?"

Layer 1: User (3K budget) + rule_lookup + complexity 1 → FAST (Llama Free)
Layer 2: Cache check → HIT! "2-5 players (page 2)"
Tokens: 50 | Latency: 35ms | Cost: $0.0002

Decision: Don't waste retrieval when cached. Perfect UX.
```

---

### Example 2: Editor, Complex Rule

```
User: Editor tier
Query: "Can I trade resources before building in Catan, or must I build first?"

Layer 1: Editor (5K budget) + rule_lookup + complexity 3 → BALANCED (GPT-4o-mini)
Layer 2: Semantic check → MISS (310t)
Layer 3: Hybrid search (vector + keyword "trade before build") + metadata filter → 10 chunks (3,500t)
Layer 4: CRAG
  - T5 scores: 4 correct, 3 ambiguous, 3 incorrect
  - Insufficient → web search "Catan trading building order official"
  - Find: BGG FAQ + official FAQ
  - Rerank: 4 internal + 5 web → top-5
  - Decompose: 4,500t → 2,250t
Layer 5: GPT-4o-mini generates (2,800t input, 300t output)
  - "Yes, trade before building. Rules state any order (page 7, FAQ: Trading)"
Layer 6: Cross-encoder alignment → 0.865 (good) → PASS

Tokens: 310 + 2,800 + 300 = 3,410t (effective 2,820t after CRAG)
Cost: $0.011 | Latency: 1.8s | Accuracy: High

Decision: BALANCED right choice. FAST would miss web sources. PRECISE unnecessary.
```

**Key**: CRAG detected insufficient docs, triggered web search, found official FAQ

---

### Example 3: Admin, Strategic Planning

```
User: Admin tier
Query: "Optimal opening for 4-player Catan with wheat-heavy tiles, 6-8-10 numbers, aggressive vs defensive?"

Layer 1: Admin (15K budget) + resource_planning + complexity 5 → PRECISE Multi-Agent (Opus)
Layer 2: SKIP (strategic queries rarely cached)
Layer 3: Multi-hop retrieval
  - Hop 1: "Catan opening wheat" → 20 chunks (5,000t)
  - Hop 2: Entities "wheat", "6-8-10", "4-player" → 15 chunks (3,000t)
  - Hop 3: Cross-ref "aggressive vs defensive" → 8 chunks (2,000t)
  - Dedup: 28 → top-20 (8,000t)
Layer 4: LLM Grading (Haiku grades 20 chunks → top-5, 2,500t)
Layer 5: Multi-Agent Generation
  - Analyzer (Haiku): 2,550t input, 400t output (extract facts)
  - Strategist (Opus): 3,050t input, 500t output (strategic rec)
  - Validator (Haiku): 2,950t input, 300t output (verify legal/costs)
  - Coordinator: 4,400t input, 200t output (fuse)
Layer 6: Self-Reflection (Opus self-critiques)
  - Confidence: 0.92 (high)
  - Re-retrieve: No
  - Tokens: 4,400t

Total: 22,908t (with 15% re-retrieval avg)
Cost: $0.098 | Latency: 8.5s | Accuracy: 96% | Confidence: 0.92

Decision: Multi-Agent justified for tournament-level strategic advice.
```

**Key**: Use Opus only for strategic reasoning (Agent 2), Haiku for mechanical tasks (Agents 1, 3). Cost-optimized specialization.

---

## Right-Sizing Principle

**Anti-Pattern**: One-size-fits-all
```python
# BAD
def answer_query(query):
    docs = retrieve(query, top_k=10)  # Always 10
    answer = llm_opus.generate(docs, query)  # Always Opus
```

**TOMAC-RAG Pattern**: Adapt everything
```python
# GOOD
def answer_query(query, user):
    # Right-size retrieval
    docs = retrieve(query, top_k=3 if complexity <= 1 else (10 if complexity <= 3 else 20))

    # Right-size model
    model = "llama-free" if user.tier == "User" else ("gpt-4o-mini" if complexity <= 3 else "opus")

    # Right-size validation
    validate = regex if complexity <= 1 else (cross_encoder if complexity <= 3 else self_rag)
```

**Result**: Simple stays simple/cheap. Complex gets resources needed. No waste, no underperformance.

---

## Key Takeaways

1. **Adaptive Routing**: Simple FAQ → FAST, Complex strategic → PRECISE. Right-sized for need.
2. **Cache is King**: 80% hit rate eliminates 80% expensive operations. Biggest ROI.
3. **Filter, Don't Retrieve More**: CRAG reduces context 40-70% while improving accuracy.
4. **Right Model for Task**: Opus for reasoning, Haiku for facts. Don't use Ferrari for groceries.
5. **Self-Correction**: Self-RAG catches 15% errors automatically. Critical for strategic advice.
6. **Multi-Agent for Multi-Dimensional**: Complex strategic queries benefit from specialized perspectives.
7. **User Tiers Align with Value**: User gets fast FAQ (low cost), Admin gets premium analysis (high value).

---

**Back to**: [RAG Overview](00-overview.md) | [Dashboard](index.html)

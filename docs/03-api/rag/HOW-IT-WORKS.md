# How TOMAC-RAG Works - Complete System Explanation

**For Developers & Architects**

---

## 🎯 What is TOMAC-RAG?

**TOMAC-RAG** (Token-Optimized Modular Adaptive Corrective RAG) is MeepleAI's intelligent question-answering system for board game rules. It combines multiple state-of-art techniques to optimize the three-way tradeoff between:

- **Cost** (token consumption)
- **Accuracy** (answer quality)
- **Latency** (response time)

**The Core Problem**: A naive RAG system treats all queries the same way:
```
Every query → Retrieve 10 chunks → Send to expensive LLM → Generate answer
```

This wastes tokens on simple questions and underperforms on complex ones.

**TOMAC-RAG's Solution**: Intelligently route each query to the optimal strategy based on:
1. **Who's asking** (user tier determines budget)
2. **What they're asking** (template type: rules vs strategy)
3. **How complex it is** (complexity score 0-5)

---

## 🏗️ The 6-Layer Architecture Explained

### Layer 1: Intelligent Routing (The Brain)

**Purpose**: Decide the optimal path for each query

**How it works**:
```python
def route_query(query: str, user: User) -> RoutingDecision:
    # Dimension 1: What does the user have access to?
    max_budget = get_token_budget(user.role)  # User: 3K, Editor: 5K, Admin: 15K tokens

    # Dimension 2: What kind of question is this?
    template = classify_template(query)
    # → "rule_lookup" (wants exact rules)
    # → "resource_planning" (wants strategic advice)

    # Dimension 3: How complex is the question?
    complexity = score_complexity(query, template)
    # Score 0-5 based on:
    # - Length (>50 words = +1)
    # - Multi-concept ("and", "or" = +1)
    # - Conditionals ("what if", "except when" = +1)
    # - Negations ("not", "can't" = +1)

    # Select strategy
    if complexity <= 1 and template == "rule_lookup":
        return "FAST"  # Simple lookup
    elif complexity <= 3:
        return "BALANCED"  # Moderate complexity
    else:
        return "PRECISE"  # Complex reasoning needed
```

**Real Example**:
```
Query: "How many food tokens in Wingspan?"
→ Template: rule_lookup (wants rule text)
→ Complexity: 1 (simple, clear terminology)
→ Strategy: FAST (simple lookup)
→ Model: Llama 3.3 Free (User tier)
→ Expected: 50 tokens (cache hit) or 2,060 tokens (cache miss)
```

**Why This Matters**: User tier asking FAQ get instant, low-cost responses. Complex strategic queries from Admins get premium multi-agent analysis. Everyone gets optimal service for their needs.

> **Note**: Anonymous users cannot access the RAG system. Authentication is required.

---

### Layer 2: Semantic Cache (The Memory)

**Purpose**: Return cached answers for repeated or similar queries

**How it works**:
```python
async def semantic_cache_lookup(query: str) -> CacheResult:
    # Normalize query
    normalized = query.lower().strip()  # "How many food?" → "how many food"

    # Check against recent queries using LLM similarity
    prompt = f"""
    Is this new query similar to any cached query?
    New: {normalized}
    Cached: ["how many food tokens", "food token count", ...]

    Return index of most similar (0-9) or null if none similar.
    Similarity threshold: 0.85
    """

    result = await llm_haiku.generate(prompt, max_tokens=10)

    if result.index is not None:
        # Cache HIT! Return cached answer
        return CacheResult(hit=True, answer=cache[result.index], tokens=50)
    else:
        # Cache MISS, continue to full RAG
        return CacheResult(hit=False, tokens=310)
```

**Real Example**:
```
User A asks: "How many food tokens in Wingspan?"
→ Cache miss, run full RAG (2,060t), cache result

User B asks: "What's the food token count for Wingspan?"
→ Semantic similarity check (310t)
→ LLM recognizes similarity (both asking about food tokens)
→ Cache HIT! Return cached answer
→ Total: 310 tokens vs 2,060 (85% savings)
```

**Why This Matters**: Users ask the same questions in different ways. Semantic cache handles variations without running expensive retrieval every time. **Expected hit rate: 80%** for FAQ-heavy domains like board games.

---

### Layer 3: Modular Retrieval (The Knowledge Fetcher)

**Purpose**: Retrieve relevant rulebook sections with strategy-appropriate depth

**Three Retrieval Strategies**:

#### FAST: Vector-Only (Speed Priority)
```python
async def retrieve_fast(query: str) -> list[Document]:
    # Use fast embedding model
    embedding = await embed_minilm(query)  # 14.7ms latency

    # Simple vector search
    results = await qdrant.search(
        query_vector=embedding,
        limit=3  # Only top-3 chunks
    )

    return results  # ~1,500 tokens (3 × 500t chunks)
```

**When**: Simple FAQ, clear terminology
**Trade-off**: 5-8% lower accuracy than larger models, but 5x faster

---

#### BALANCED: Hybrid Search + Metadata (Quality + Efficiency)
```python
async def retrieve_balanced(query: str, game_id: str) -> list[Document]:
    # Better embedding model
    embedding = await embed_e5_base(query)  # 79ms, 83-85% accuracy

    # Parallel retrieval
    vector_results = await qdrant.search(query_vector=embedding, limit=10)
    keyword_results = await qdrant.search_bm25(query_text=query, limit=10)

    # Metadata filter (reduce search space)
    if game_id:
        vector_results = filter_by_metadata(vector_results, game_id=game_id)
        keyword_results = filter_by_metadata(keyword_results, game_id=game_id)

    # Reciprocal Rank Fusion (combine results)
    fused = reciprocal_rank_fusion(vector_results, keyword_results)

    return fused[:10]  # ~3,500 tokens (10 × 350t average)
```

**When**: Ambiguous queries, complex rules, multi-concept questions
**Trade-off**: 2x tokens vs FAST, but +10-12% accuracy

---

#### PRECISE: Multi-Hop Adaptive (Maximum Quality)
```python
async def retrieve_precise(query: str) -> list[Document]:
    all_docs = []

    # Hop 1: Broad initial retrieval
    hop1 = await qdrant.search(query, limit=20)  # 5,000 tokens
    all_docs.extend(hop1)

    # Hop 2: Entity expansion (explore related concepts)
    entities = extract_entities(hop1)  # "combat", "resources", "trading"
    hop2 = []
    for entity in entities[:5]:
        entity_docs = await qdrant.search(entity, limit=5)
        hop2.extend(entity_docs)  # +3,000 tokens

    all_docs.extend(hop2)

    # Hop 3: Cross-reference validation
    related_rules = extract_related_rules(all_docs)
    hop3 = await retrieve_related(related_rules)  # +2,000 tokens
    all_docs.extend(hop3)

    # Deduplicate
    unique = deduplicate_by_id(all_docs)  # Remove overlaps

    return unique[:20]  # ~8,000 tokens after dedup
```

**When**: Multi-step reasoning, strategic planning, rule conflicts
**Trade-off**: 5x tokens vs FAST, but 95-98% accuracy (vs 78-85%)

---

**Why Three Strategies?**

Think of it like shipping options:
- **FAST** = Standard mail (cheap, 3-5 days, occasional errors)
- **BALANCED** = Express mail (moderate cost, 1-2 days, reliable)
- **PRECISE** = Overnight courier (expensive, same day, guaranteed)

You wouldn't use overnight shipping for junk mail, and you wouldn't use standard mail for critical legal documents. Same principle applies to RAG strategies.

---

### Layer 4: CRAG Evaluation (The Quality Gate)

**Purpose**: Prevent hallucinations by evaluating retrieval quality **before** expensive LLM generation

**How it works**:
```python
async def crag_evaluate(query: str, docs: list[Document]) -> CragResult:
    # Step 1: Evaluate each document with fine-tuned T5-Large
    evaluations = []
    for doc in docs:
        score = await t5_evaluator.score_relevance(query, doc.content)
        # Score: 0-1 (how relevant is this doc to the query?)

        if score >= 0.8:
            category = "correct"  # Highly relevant
        elif score >= 0.5:
            category = "ambiguous"  # Partially relevant
        else:
            category = "incorrect"  # Irrelevant

        evaluations.append({"doc": doc, "category": category, "score": score})

    # Step 2: Decide action based on distribution
    correct = [e for e in evaluations if e["category"] == "correct"]
    ambiguous = [e for e in evaluations if e["category"] == "ambiguous"]

    if len(correct) >= 3:
        # Sufficient good docs, use internal KB only
        final_docs = correct[:5]
        source = "internal"

    elif len(ambiguous) > 0 or len(correct) > 0:
        # Partial info, augment with web search
        web_docs = await web_search(query + " official rules FAQ")
        combined = correct + ambiguous + web_docs
        final_docs = await rerank(combined, query, top_k=5)
        source = "internal+web"

    else:
        # All docs irrelevant, web search only
        web_docs = await web_search(query + " board game rules")
        final_docs = await rerank(web_docs, query, top_k=10)
        source = "web"

    # Step 3: Decompose-then-recompose (extract only key info)
    filtered_docs = []
    for doc in final_docs:
        sentences = split_into_sentences(doc.content)
        # Keep only top 60% most relevant sentences
        top_sentences = rank_and_filter(sentences, query, keep=0.6)
        filtered_docs.append(Document(content=" ".join(top_sentences)))

    return CragResult(docs=filtered_docs, source=source)
```

**Real Example**:
```
Query: "Can I attack diagonally in chess variant X?"
Retrieved docs:
  - Doc 1: General chess rules (score: 0.4) → INCORRECT
  - Doc 2: Variant X overview (score: 0.6) → AMBIGUOUS
  - Doc 3: Variant X movement rules (score: 0.9) → CORRECT

CRAG Decision:
  → Only 1 correct doc, 1 ambiguous
  → Trigger web search for "chess variant X rules official"
  → Find 5 web results (official site, BoardGameGeek FAQ)
  → Rerank: internal (2 docs) + web (5 docs) → top-5
  → Decompose-recompose: 3,500 tokens → 1,800 tokens (49% reduction)

Result: Higher quality context (web augmentation) with fewer tokens (filtering)
```

**Why CRAG is Critical**: Prevents the LLM from generating answers based on irrelevant documents. Catches bad retrievals **before** expensive generation, not after.

**Token Magic**: Uses T5-Large (separate model, 0 LLM tokens) to filter, then only sends high-quality context to expensive LLM. Net effect: Better accuracy with fewer tokens!

---

### Layer 5: Adaptive Generation (The Answer Creator)

**Purpose**: Generate answer with template-appropriate prompts and models

**Template-Specific Behavior**:

#### rule_lookup Template (Exact Rule Extraction)

**FAST Prompt**:
```python
prompt = f"""
Extract the exact rule text that answers this question.
Include page citation (mandatory).

Retrieved Rules:
{format_docs(docs)}

Question: {query}

Answer format: [exact rule text] (page X)
If not found: "Rule not found in retrieved sections."
"""
# Simple extraction, minimal reasoning
# Model: Haiku or Llama 3.3 Free
# Tokens: ~1,950 input, ~200 output
```

**BALANCED Prompt**:
```python
prompt = f"""
You are a board game rules expert. Synthesize a comprehensive answer
from these rule sections.

Retrieved (reranked top-5):
{format_docs_with_metadata(docs)}

Question: {query}

Provide:
1. Synthesized answer combining multiple sections
2. Citations for each referenced section (page numbers mandatory)
3. Note any ambiguities or edge cases

Format: [comprehensive answer] (pages X, Y, Z)
"""
# Synthesis from multiple sources
# Model: Claude Sonnet or GPT-4o-mini
# Tokens: ~3,050 input, ~300 output
```

**PRECISE Prompt** (with Self-RAG):
```python
prompt = f"""
You are a rules expert. Use chain-of-thought reasoning to analyze
rule interactions and resolve conflicts.

Context: {format_docs_detailed(docs)}
Question: {query}

Step-by-step analysis:
1. Identify all relevant rule sections
2. Check for conflicts or contradictions
3. If conflicting, resolve using priority/timing rules
4. Synthesize final answer with reasoning

Provide:
- Reasoning steps
- Resolved answer
- All citations
- Confidence level (0-1)

Then self-critique:
- Are you confident this is correct?
- Should you retrieve additional context?
"""
# Complex reasoning with self-reflection
# Model: Claude Opus
# Tokens: ~3,150 input, ~500 output + 4,400 reflection
```

**Why Different Prompts?** Simple questions need simple answers. Complex questions need reasoning. Using chain-of-thought on "How many players?" wastes tokens. Not using it on "Optimal opening strategy?" produces poor advice.

---

#### resource_planning Template (Strategic Advice)

**BALANCED Prompt**:
```python
prompt = f"""
Strategic advisor for board games. Analyze trade-offs.

Rules Context: {format_docs(docs)}
Decision: {query}

Analysis:
1. Option A: [Pros/Cons with rule citations]
2. Option B: [Pros/Cons with rule citations]
3. Resource costs for each option (use calculator if needed)
4. Recommendation with reasoning

Tools available:
- calculator(a, b, op): For resource math

Format: Structured comparison → Clear recommendation
"""
# Trade-off analysis with tools
# Model: Claude Sonnet or GPT-4o-mini
# Tokens: ~3,050 input, ~400 output
```

**PRECISE Prompt** (Multi-Agent):
```python
# Instead of single LLM, use 3 specialized agents:

# Agent 1: Analyzer (Haiku)
analyzer_prompt = """
Analyze game state and constraints from rules.
Extract: current resources, available options, rule constraints
"""
# Output: Structured analysis (400 tokens)

# Agent 2: Strategist (Opus) - Most important, gets premium model
strategist_prompt = """
Given this analysis: {analyzer_output}
And these rules: {docs}

Generate strategic recommendation:
1. Evaluate each option (expected value, risk, opportunity cost)
2. Consider long-term vs short-term tradeoffs
3. Recommend optimal action with reasoning
"""
# Output: Detailed recommendation (500 tokens)

# Agent 3: Validator (Haiku)
validator_prompt = """
Cross-check this recommendation against rules:
Recommendation: {strategist_output}
Rules: {docs}

Verify:
- Is it legal per rules?
- Are resource costs accurate?
- Any violations?
- Confidence (0-1)
"""
# Output: Validation report (300 tokens)

# Coordinator: Fuse agent outputs into final answer
```

**Why Multi-Agent for PRECISE resource_planning?**

Strategic decisions benefit from **separation of concerns**:
- Analyzer extracts facts (uses cheap Haiku, no creativity needed)
- Strategist reasons strategically (uses expensive Opus, creativity critical)
- Validator cross-checks (uses cheap Haiku, fact-checking task)

**Cost Optimization**: Use Opus only where it matters (strategic reasoning), use Haiku for mechanical tasks (analysis, validation). Total: ~12,900 tokens but highest accuracy (95-98%).

---

### Layer 6: Self-Validation (The Quality Checker)

**Purpose**: Catch errors before returning answer to user

**Three Validation Levels**:

#### FAST: Rule-Based (0 tokens, instant)
```python
def validate_fast(answer: str, template: str) -> bool:
    if template == "rule_lookup":
        # Check for page citation (mandatory for rules)
        has_citation = bool(re.search(r'\(page \d+\)', answer))
        if not has_citation:
            return False  # FAIL → escalate to BALANCED

    # Check minimum answer length
    if len(answer.split()) < 10:
        return False  # Too short, likely incomplete

    return True  # PASS
```

**Why**: FAST tier needs quick validation. Regex checks are instant and catch obvious problems (missing citations, truncated answers).

---

#### BALANCED: Cross-Encoder Alignment (0 LLM tokens)
```python
async def validate_balanced(answer: str, docs: list) -> float:
    # Use cross-encoder to score how well answer matches docs
    pairs = [(answer, doc.content) for doc in docs]
    scores = await cross_encoder.score_pairs(pairs)

    avg_alignment = sum(scores) / len(scores)

    if avg_alignment < 0.6:
        # Low alignment = potential hallucination
        return 0.6  # Low confidence
    elif detect_contradictions(docs):
        # Docs contradict each other
        return 0.7  # Medium confidence, suggest escalation
    else:
        return avg_alignment  # Good confidence
```

**Why**: Cross-encoder (separate model, not LLM) checks factual grounding without extra LLM tokens. Detects hallucinations cheaply.

---

#### PRECISE: Self-RAG Reflection (4,400 tokens)
```python
async def validate_precise(answer: str, docs: list, query: str) -> dict:
    reflection_prompt = f"""
    Self-critique your answer:

    Your Answer: {answer}
    Based On: {docs}
    Original Query: {query}

    Evaluate yourself:
    1. Relevance: Are docs relevant to query? (Yes/Partial/No)
    2. Support: Is answer fully supported by docs? (Yes/Partial/No)
    3. Usefulness: Will this satisfy the user? (Yes/Maybe/No)

    Confidence: Based on above, how confident are you? (0-1)

    Should re-retrieve?
    - Yes if: any criterion is Partial/No/Maybe OR confidence <0.9
    - Refined query: What would improve the answer?

    JSON: {"relevance": str, "support": str, "usefulness": str,
           "confidence": float, "re_retrieve": bool, "refined_query": str}
    """

    reflection = await llm_opus.generate(reflection_prompt, max_tokens=300)
    critique = parse_json(reflection)

    if critique["re_retrieve"] and critique["confidence"] < 0.9:
        # LLM thinks it needs more context
        new_docs = await retrieve_precise(critique["refined_query"])
        new_answer = await generate_precise(new_docs, query)
        # Recursive validation (max 2 loops to prevent infinite)
        return await validate_precise(new_answer, new_docs, query, max_loops=1)

    return critique
```

**Why**: For critical strategic decisions, LLM should question itself. "Am I confident in this recommendation?" If not, automatically retrieve more context. **Self-correcting** system.

**Real Example**:
```
Query: "Should I build settlement or city in Catan with 3 ore, 2 wheat?"

Initial answer: "Build settlement (costs 1 wood, 1 brick, 1 wheat, 1 sheep)"

Self-reflection:
→ Support: Partial (retrieved docs show settlement costs, but user doesn't have sheep!)
→ Confidence: 0.4 (low, answer is actually wrong)
→ Re-retrieve: Yes
→ Refined query: "Catan building costs settlement vs city required resources"

New retrieval finds: "City costs 3 ore + 2 wheat"
New answer: "Build city (you have exactly these resources)"
Confidence: 0.95 (high)
```

**Result**: System caught its own error and self-corrected. Without Self-RAG, would have given wrong advice.

---

## 🎯 Why Specific Agent Roles Need Specific Strategies

### Rules Agent (MeepleAI Primary Use Case)

**Query Profile**:
- 70% rule_lookup (exact rule text)
- 30% resource_planning (occasional strategic questions)
- Accuracy critical (legal liability if rules wrong)
- Citation mandatory (users need page references)

**Optimal Stack**:
```
80% queries → Cache (Memory + Semantic)
  → 50t or 986t
  → Instant answers for FAQ
  → Examples: "How many players?", "Setup procedure"

18% queries → FAST + Contextual Embeddings
  → 1,950t
  → Simple rule lookups with good precision
  → Examples: "Food tokens in setup", "Card draw rules"

8% queries → BALANCED + CRAG
  → 2,625t
  → Complex rules, ambiguous questions
  → CRAG evaluator ensures high quality
  → Examples: "Trading rules with restrictions", "Combat in multiple phases"

2% queries → PRECISE + Self-RAG
  → 7,420t
  → Rule conflicts, edge cases
  → Self-reflection ensures correctness
  → Examples: "Can I do X in situation Y?", "Priority when rules conflict"
```

**Result**: Average 900 tokens/query, 92% accuracy, $300/month (100K queries)

**Why Not Always Use PRECISE?**
- 98% of queries don't need it (FAQ are simple)
- Would cost $2,800/month instead of $300 (9x increase!)
- Latency would be 5-10s for all queries (poor UX for simple questions)

---

### Strategy Agent (Strategic Planning Focus)

**Query Profile**:
- 100% resource_planning (all queries are strategic)
- Reasoning quality > speed (users willing to wait for good advice)
- Confidence scoring critical (users need to trust recommendations)

**Optimal Stack**:
```
25% queries → Semantic Cache
  → 986t
  → Common strategic patterns cached
  → Example: "Should I trade or build?" (recurring question)

40% queries → BALANCED + Structured Prompts
  → 2,820t
  → Pros/cons analysis with rule citations
  → Tool-calling enabled (calculator for resource math)
  → Example: "Better to build settlement or save for city?"

30% queries → PRECISE + Self-RAG
  → 7,420t
  → Complex multi-step planning
  → Self-reflection provides confidence scores
  → Example: "Optimal opening for 4-player Catan with wheat-heavy position"

5% queries → PRECISE + Multi-Agent
  → 12,900t
  → Tournament-level optimization
  → 3-agent collaboration (Analyzer + Strategist + Validator)
  → Example: "Optimal strategy for turns 1-5 given starting position X?"
```

**Result**: Average 4,200 tokens/query, 90% accuracy, $1,400/month

**Why High Token Budget?**
- Strategic advice has **high value** (helps users win games)
- Users seeking strategy expect **quality over speed**
- **Confidence scores** justify token cost (users know when to trust advice)
- Multi-agent for Admin tier only (5% of queries, high-value users)

---

### Setup Agent (Procedural Instructions)

**Query Profile**:
- 100% rule_lookup (setup procedures)
- **Extremely cacheable** (setup rarely changes)
- Visual support helpful (diagrams, component layouts)

**Optimal Stack**:
```
95% queries → Memory Cache
  → 50t
  → Setup procedure identical for all users
  → Example: "Wingspan setup", "Catan starting resources"

3% queries → FAST + Metadata
  → 2,200t
  → Setup with variants/expansions
  → Metadata filter: game_id + expansion_id
  → Example: "Catan setup with Seafarers expansion"

2% queries → Multimodal RAG (if visual needed)
  → 8,550t
  → Vision model interprets setup diagrams
  → Example: "Show me player board setup" (with image)
```

**Result**: Average 400 tokens/query (ultra-efficient!), 85% accuracy

**Why Memory Cache Dominance?**
- Setup doesn't change between games
- First query generates answer, caches for weeks
- 1,000 subsequent queries = 1,000 × 50t = 50K tokens
- Without cache = 1,000 × 2,000t = 2M tokens (40x difference!)

**ROI**: Setup agent has **best ROI** of all agent types (highest cache hit rate)

---

### Assistant Agent (Real-Time Gameplay)

**Query Profile**:
- 100% rule_lookup (quick rule clarifications during gameplay)
- **Latency critical** (users won't wait >200ms during game)
- Accuracy acceptable (not mission-critical, just helpful)

**Optimal Stack**:
```
95% queries → Memory Cache
  → 50t, <50ms
  → Common gameplay questions cached

5% queries → FAST only (no BALANCED/PRECISE)
  → 2,060t, 150-200ms
  → Fallback for cache misses

BALANCED/PRECISE: DISABLED (too slow for real-time)
```

**Result**: Average 300 tokens/query, 75% accuracy, <200ms latency

**Why Sacrifice Accuracy?**
- **Latency budget**: Gameplay can't pause for 2-5s
- **Acceptable errors**: If answer is 75% accurate, users can verify quickly
- **Volume**: Gameplay generates high query volume, cost adds up
- **Alternative**: Users can ask detailed question later (post-game) with BALANCED

**Design Philosophy**: Real-time agents optimize for **speed**, post-game agents optimize for **accuracy**.

---

## 💡 Token Efficiency Principles

### Principle 1: Cache Everything Cacheable

**80/20 Rule**: 80% of queries come from 20% of question patterns (especially in board games)

**Example**:
```
Top 20 FAQ questions in Wingspan:
1. "How many players?" → Asked 5,000 times/month
2. "Setup procedure?" → Asked 3,000 times/month
3. "Food tokens?" → Asked 2,500 times/month
...

Without cache:
  20 questions × avg 5,000 asks = 100K queries
  100K × 2,000 tokens = 200M tokens
  Cost: ~$600/month

With cache:
  First ask: 20 × 2,000 = 40K tokens (generate & cache)
  Subsequent: 100K × 50 = 5M tokens (cache hits)
  Total: 5.04M tokens
  Cost: ~$15/month (40x reduction!)
```

**Key Insight**: Cache is not a nice-to-have, it's **the foundation** of token efficiency.

---

### Principle 2: Filter Context Aggressively

**97% of tokens are input context** (retrieved docs), only 3% are output (answer).

**Naive approach**:
```
Retrieve 10 chunks × 500 tokens = 5,000 tokens
Send all to LLM
```

**TOMAC-RAG approach**:
```
Retrieve 10 chunks = 5,000 tokens
↓
CRAG Evaluator: Only 3 are "correct" (others irrelevant)
↓
Decompose-recompose: Extract key sentences from 3 correct docs
↓
Final context: 1,000 tokens (80% reduction!)
↓
LLM generates from high-quality, focused context
```

**Result**: Better accuracy (no noise) with 80% fewer tokens (filtering).

**Key Insight**: More context ≠ better answers. **Relevant** context = better answers.

---

### Principle 3: Use the Cheapest Model That Works

**Model Pricing** (per 1M tokens):
- Llama 3.3 70B (free): $0
- Claude Haiku: $0.25 (input) / $1.25 (output)
- GPT-4o-mini: $0.15 / $0.60
- Claude Sonnet: $3 / $15
- Claude Opus: $15 / $75

**Naive approach**: Use Opus for everything (best quality)
```
100K queries × 2,000 tokens × Opus pricing
= 200M tokens × $15/1M = $3,000/month
```

**TOMAC-RAG approach**: Match model to task complexity
```
60K FAST (cache) × 50t × Free = $0
25K FAST (miss) × 2,060t × Haiku = $13
13K BALANCED × 2,820t × GPT-4o-mini = $6
2K PRECISE × 7,420t × Opus = $222
────────────────────────────────────
Total: $241/month (12x cheaper!)
```

**Key Insight**: FAQ don't need Opus. Strategic optimization does. Right model for right task.

---

## 🔀 Real-World Decision Making Examples

### Example 1: User Tier, Simple Question

```
User: User (basic tier)
Query: "How many players in Wingspan?"

Layer 1 (Routing):
  → User tier: User (budget: 3,000 tokens max)
  → Template: rule_lookup (wants rule text)
  → Complexity: 1 (simple, clear)
  → Strategy: FAST
  → Model: Llama 3.3 Free

Layer 2 (Cache):
  → Check: "how many players in wingspan"
  → CACHE HIT! (this FAQ asked 5,000 times before)
  → Return: "2-5 players (page 2)"
  → Tokens: 50
  → Latency: 35ms
  → Cost: $0.0002

Decision: Don't waste tokens on retrieval when answer is cached.
Result: Instant, free response. Perfect UX.
```

---

### Example 2: Editor User, Complex Rule

```
User: Editor (premium tier)
Query: "Can I trade resources before building during my turn in Catan, or must I build first?"

Layer 1 (Routing):
  → User tier: Editor (budget: 5,000 tokens)
  → Template: rule_lookup
  → Complexity: 3 (multi-concept: "trade", "build", "order")
  → Strategy: BALANCED
  → Model: GPT-4o-mini (50%) or Claude Haiku (50%)

Layer 2 (Cache):
  → Check semantic similarity
  → CACHE MISS (novel phrasing)
  → Tokens: 310

Layer 3 (Retrieval):
  → Hybrid search (vector + keyword "trade before build")
  → Metadata filter: game_id = "catan"
  → Results: 10 chunks (3,500 tokens)

Layer 4 (CRAG):
  → T5 Evaluator scores each chunk:
    - 4 chunks: "correct" (≥0.8) - about trading and building rules
    - 3 chunks: "ambiguous" (0.5-0.8) - general resource rules
    - 3 chunks: "incorrect" (<0.5) - unrelated (setup rules)
  → Decision: Correct docs insufficient, trigger web search
  → Web search: "Catan trading building order official rules"
  → Finds: BoardGameGeek FAQ + official Catan FAQ
  → Combined: 4 internal + 5 web = 9 docs
  → Rerank with cross-encoder → top-5
  → Decompose-recompose: Extract key sentences
  → Final context: 2,250 tokens (down from 4,500)

Layer 5 (Generation):
  → Model: GPT-4o-mini (drawn from 50% pool)
  → Prompt: Synthesize from internal + web sources
  → Input: 500 (system) + 50 (query) + 2,250 (context) = 2,800t
  → Output: 300 tokens
  → Answer: "Yes, you can trade before building. The rules state that during your turn, you may perform actions in any order (page 7, Catan FAQ: Trading section). You can trade with other players or the bank, then use those resources to build."

Layer 6 (Validation):
  → Cross-encoder alignment check
  → Answer vs internal docs: 0.82 alignment
  → Answer vs web docs: 0.91 alignment
  → Average: 0.865 (good confidence)
  → PASS

Result:
  → Total tokens: 310 + 2,800 + 300 = 3,410t
  → Effective (after CRAG filter): 2,820t
  → Cost: $0.011
  → Latency: 1.8s
  → Accuracy: High (web augmentation found official FAQ)

Decision: BALANCED was right choice. FAST would have missed web sources (lower accuracy). PRECISE unnecessary (no multi-step reasoning needed).
```

**Key Lesson**: CRAG's value shines here. Evaluator detected insufficient internal docs, triggered web search, found official FAQ. Without CRAG, might have hallucinated based on incomplete context.

---

### Example 3: Admin User, Strategic Planning

```
User: Admin (full access)
Query: "What's the optimal opening strategy for 4-player Catan if I start with wheat-heavy tiles and a 6-8-10 number spread, considering aggressive vs defensive play styles?"

Layer 1 (Routing):
  → User tier: Admin (budget: 15,000 tokens, full access)
  → Template: resource_planning (strategic advice)
  → Complexity: 5 (multi-concept + optimization + conditionals)
  → Strategy: PRECISE
  → Model: Multi-Agent (Analyzer Haiku + Strategist Opus + Validator Haiku)

Layer 2 (Cache):
  → SKIP (strategic queries rarely cached - too context-specific)

Layer 3 (Multi-Hop Retrieval):
  → Hop 1: "Catan opening strategy wheat tiles" → 20 chunks (5,000t)
  → Extract entities: "wheat production", "6-8-10 numbers", "4-player dynamics"
  → Hop 2: Retrieve for each entity → 15 chunks (3,000t)
  → Hop 3: Cross-reference "aggressive vs defensive" → 8 chunks (2,000t)
  → Deduplicate: 28 unique chunks → top-20 (8,000t)

Layer 4 (LLM Grading):
  → Use Claude Haiku to grade all 20 chunks
  → Input: 8,350t, Output: 100t (rankings)
  → Top-5 selected: 2,500t

Layer 5 (Multi-Agent Generation):
  → Agent 1 (Analyzer - Haiku):
    - Input: 500 (system) + 50 (query) + 2,000 (relevant docs) = 2,550t
    - Task: Analyze wheat advantage, number spread implications
    - Output: 400t (structured analysis)

  → Agent 2 (Strategist - Opus):
    - Input: 600 (system) + 50 (query) + 400 (analyzer) + 2,000 (docs) = 3,050t
    - Task: Generate strategic recommendation
    - Reasoning: "Wheat-heavy with 6-8-10 numbers:
        - Pros: Consistent wheat production, diverse numbers reduce variance
        - Cons: Vulnerable if 7 is blocked, need ore/brick for cities
        - Aggressive play: Early city on wheat hex (leverage high production)
        - Defensive play: Diversify with port trade (wheat → other resources)
        - Recommendation: Hybrid approach - secure wheat city early, then expand to ore/brick for balanced development"
    - Output: 500t (detailed recommendation)

  → Agent 3 (Validator - Haiku):
    - Input: 500 (system) + 50 (query) + 900 (agents 1+2) + 1,500 (docs) = 2,950t
    - Task: Verify recommendation legal per rules, resource costs accurate
    - Output: 300t (validation: "✅ Strategy legal, resource costs verified")

  → Coordinator:
    - Fuse agent outputs into final answer
    - Input: 4,400t (all agent outputs + docs summary)
    - Output: 200t (final formatted answer with confidence score)

Layer 6 (Self-Reflection):
  → Opus self-critiques the final recommendation
  → Confidence: 0.92 (high)
  → Re-retrieve: No (confident in recommendation)
  → Tokens: 4,400t

Result:
  → Total tokens: 8,630 (retrieval) + 8,450 (grading) + 8,500 (agents) + 4,400 (reflection) = 29,980t
  → With 15% re-retrieval average: 0.85 × 22,396 + 0.15 × 25,896 = 22,908t actual
  → Cost: $0.098
  → Latency: 8.5s
  → Accuracy: 96% (multi-agent collaboration)
  → Confidence: 0.92 (transparent to user)

Decision: PRECISE Multi-Agent justified. Query requires:
  - Multi-dimensional analysis (wheat advantage, number spread, play style)
  - Strategic reasoning (aggressive vs defensive trade-offs)
  - Expert-level advice (tournament-quality recommendation)

For Admin tier user seeking competitive advice, $0.10 and 8.5s are acceptable for 96% accurate, high-confidence recommendation.
```

**Key Lesson**: Multi-agent overkill for "How many players?" but perfect for complex strategic optimization. **Right tool for right job.**

---

## 🎓 Educational Insight: Why This Architecture?

### The Token Efficiency Paradox

**Counterintuitive Finding**: Adding quality features (CRAG, Self-RAG) **reduces** total cost!

**How?**

**Without Optimizations** (naive approach):
```
100K queries × 2,000 tokens (naive RAG) = 200M tokens
Cost: $600/month
Accuracy: 80%
```

**With TOMAC-RAG** (optimized):
```
60K cache hits × 50t = 3M tokens
25K FAST × 2,060t = 51.5M tokens
13K BALANCED (CRAG filters context) × 2,820t = 36.7M tokens
2K PRECISE × 7,420t = 14.8M tokens
────────────────────────────────────
Total: 106M tokens (47% reduction!)
Cost: $294/month (51% reduction!)
Accuracy: 92% (+12%)
```

**The Magic**:
1. **Cache** eliminates 60% of retrieval (60K × 1,950t saved = 117M tokens saved)
2. **CRAG** filters context 40-70% (saves ~15M tokens on BALANCED queries)
3. **Contextual embeddings** improve precision (retrieve 3 chunks vs 5 = saves ~20M tokens)
4. **Metadata filtering** reduces irrelevant results (saves ~10M tokens)

**Total optimizations**: ~162M tokens saved
**Quality investments**: +15M tokens (CRAG evaluator, Self-RAG reflection)
**Net**: -147M tokens saved **while improving accuracy**!

---

### The Right-Sizing Principle

**Anti-Pattern**: One-size-fits-all
```python
# BAD: Treat all queries the same
def answer_query(query):
    docs = retrieve(query, top_k=10)  # Always 10
    answer = llm_opus.generate(docs, query)  # Always Opus
    return answer
```

**TOMAC-RAG Pattern**: Right-size everything
```python
# GOOD: Adapt to query characteristics
def answer_query(query, user):
    # Right-size retrieval depth
    if complexity <= 1:
        docs = retrieve(query, top_k=3)  # Only 3 for simple
    elif complexity <= 3:
        docs = retrieve(query, top_k=10)  # 10 for moderate
    else:
        docs = multi_hop_retrieve(query, max_hops=3)  # Deep for complex

    # Right-size model
    if user.tier == "User":
        model = "llama-3.3-free"  # or gpt-4o-mini
    elif complexity <= 3:
        model = "gpt-4o-mini"
    else:
        model = "claude-opus"

    # Right-size validation
    if complexity <= 1:
        validate = check_citations  # Simple regex
    elif complexity <= 3:
        validate = cross_encoder_alignment  # Medium validation
    else:
        validate = self_rag_reflection  # Full self-critique

    return generate_with_validation(docs, query, model, validate)
```

**Result**: Simple queries stay simple and cheap. Complex queries get the resources they need. No waste, no underperformance.

---

## 🔄 How Routing Decisions Propagate

### Decision Tree Visualization

```
User Query: "Should I build settlement or city with 3 ore, 2 wheat?"
User: Editor tier

Step 1: Template Classification
├─ Keywords: "should I", "or" → Strategic decision
└─ Template: resource_planning ✓

Step 2: Complexity Scoring
├─ Length: 10 words (no penalty)
├─ Multi-concept: "settlement" AND "city" (+1)
├─ Conditionals: "with X resources" (+1)
├─ Template baseline: resource_planning (+1)
└─ Total: 3 points

Step 3: Strategy Selection
├─ Complexity 3 + resource_planning template
└─ Strategy: BALANCED ✓

Step 4: Model Selection
├─ User tier: Editor
├─ Strategy: BALANCED
├─ Random split: 50% GPT-4o-mini, 50% Claude Haiku
└─ Selected: GPT-4o-mini (drawn from pool) ✓

Step 5: Flow Pattern Selection
├─ Strategy: BALANCED
├─ Template: resource_planning
└─ Flow: Conditional (with CRAG evaluation) ✓

Step 6: Validation Method
├─ Strategy: BALANCED
└─ Validation: Cross-encoder alignment ✓

Final Routing Decision:
{
  "strategy": "BALANCED",
  "template": "resource_planning",
  "complexity": 3,
  "model": "gpt-4o-mini",
  "flow_pattern": "conditional",
  "crag_enabled": true,
  "validation": "cross_encoder",
  "estimated_tokens": 2820,
  "estimated_cost": 0.011,
  "estimated_latency": "1-2s"
}
```

**What Happens Next**:
1. Check cache (likely miss for strategic query)
2. Hybrid retrieval (vector + BM25 for "settlement city resources")
3. CRAG evaluates docs, possibly triggers web search
4. GPT-4o-mini generates structured pros/cons analysis
5. Cross-encoder validates answer grounded in context
6. Return answer with confidence score

**User Experience**: Gets strategic advice in ~1.5s with pros/cons for both options, based on actual game rules (cited), with confidence score to inform decision.

---

## 📊 Performance Characteristics by Agent Role

### Why Rules Agent Uses BALANCED + CRAG

**Requirements**:
- ✅ High accuracy (>92%) - legal liability if rules wrong
- ✅ Citations mandatory (users need page references)
- ✅ Acceptable latency (1-2s) - users willing to wait for accuracy
- ✅ Moderate cost - balance between free tier and premium

**BALANCED + CRAG Delivers**:
- ✅ 85-92% accuracy (meets requirement)
- ✅ CRAG ensures citations (evaluator validates doc quality)
- ✅ 1-2s latency (acceptable)
- ✅ $0.011/query (affordable at scale)

**Why Not FAST?**
- ❌ 78-85% accuracy (too low for legal liability)
- ❌ No quality gating (might return wrong rules)
- ✅ Would save $0.003/query but risk incorrect rules

**Why Not PRECISE?**
- ✅ 95-98% accuracy (exceeds requirement)
- ❌ $0.095/query (9x more expensive)
- ❌ 5-10s latency (poor UX for simple rule lookups)
- ❌ Overkill for 90% of rule queries

**Conclusion**: BALANCED + CRAG is **Goldilocks zone** for Rules Agent (not too cheap, not too expensive, just right).

---

### Why Strategy Agent Uses PRECISE + Self-RAG

**Requirements**:
- ✅ High-quality reasoning (users seek expert advice)
- ✅ Confidence scoring (users need to know when to trust)
- ✅ Self-correcting (catch logical errors)
- ⚠️ Latency acceptable (users expect strategic analysis takes time)
- ⚠️ Higher cost acceptable (strategic advice has high value)

**PRECISE + Self-RAG Delivers**:
- ✅ 88-95% accuracy (expert-level advice)
- ✅ Confidence scores (0-1 scale, transparent to user)
- ✅ Self-reflection catches errors (15% of queries self-correct)
- ✅ 2-5s latency (acceptable for strategic analysis)
- ✅ $0.028/query (justified by advice quality)

**Why Not BALANCED?**
- ⚠️ 85-92% accuracy (acceptable but not optimal for strategy)
- ❌ No confidence scoring (users don't know when to trust)
- ❌ No self-correction (errors go undetected)

**Why Multi-Agent for Admin?**
- ✅ 95-98% accuracy (tournament-level advice)
- ✅ Multi-perspective analysis (Analyzer + Strategist + Validator)
- ✅ Collaborative reasoning (catches edge cases)
- ⚠️ $0.095/query (3x BALANCED but 5% query volume for high-value users)

**Conclusion**: Strategy Agent users **value quality over cost**. They're asking for expert advice, not FAQ answers. Self-RAG confidence scores build trust ("I'm 92% confident in this recommendation").

---

## 🚀 System Evolution Path

### Current State (Before TOMAC-RAG)
```
Simple RAG:
  → Retrieve 5 chunks (2,500t)
  → GPT-4o-mini generates (input: 3,000t, output: 200t)
  → No validation, no caching, no optimization
  → Cost: $0.012/query
  → Accuracy: 80%
  → No confidence scores
```

### Phase 1 (Weeks 1-4): Foundation
```
Add:
  → Semantic cache (80% hit rate target)
  → Metadata filtering (30% token reduction)
  → Contextual embeddings (one-time re-index)

Result:
  → 80% queries from cache (50-986t)
  → 20% optimized retrieval (1,950t vs 2,500t)
  → Cost: $0.004/query (-67%)
  → Accuracy: 85% (+5% from contextual embeddings)
```

### Phase 2 (Weeks 5-8): Quality
```
Add:
  → CRAG evaluator (fine-tuned T5-Large)
  → Hybrid search (vector + BM25)
  → Cross-encoder reranking

Result:
  → BALANCED strategy operational
  → CRAG filtering: 3,500t → 1,800t effective
  → Cost: $0.011/query (blended)
  → Accuracy: 92% (+12% total)
```

### Phase 3 (Weeks 9-12): Advanced
```
Add:
  → Self-RAG reflection
  → Multi-agent orchestration (Admin tier)
  → Multi-hop retrieval (PRECISE tier)

Result:
  → Full TOMAC-RAG operational
  → 3-strategy adaptive system
  → Cost: $0.012/query average (blended across all strategies)
  → Accuracy: 95% rule_lookup, 90% resource_planning
  → Confidence scoring enabled
```

**Evolution Principle**: Start with foundation (cache), add quality (CRAG), finish with premium features (Multi-Agent). Each phase delivers value before next begins.

---

## 💡 Key Takeaways

1. **Not All Queries Are Equal**: Simple FAQ need speed, complex strategic queries need quality. Adaptive routing gives each query what it needs.

2. **Cache is King**: 80% hit rate eliminates 80% of expensive operations. Biggest ROI optimization.

3. **Filter, Don't Retrieve More**: CRAG's decompose-recompose reduces context 40-70% while improving accuracy. Quality > quantity.

4. **Right Model for Right Task**: Use Opus for strategic reasoning, Haiku for fact-checking. Don't use Ferrari for grocery shopping.

5. **Self-Correction Matters**: Self-RAG catches 15% of errors automatically. For strategic advice, this is critical (wrong advice = user loses game).

6. **Multi-Agent for Multi-Dimensional**: Complex strategic queries benefit from specialized perspectives (Analyzer extracts facts, Strategist reasons, Validator verifies).

7. **User Tiers Align with Value**: User tier gets fast FAQ answers (low cost). Admins get premium strategic analysis (high value justifies high cost). Anonymous users must authenticate to access the system.

---

**This explanation should be added to**: `docs/03-api/rag/00-overview.md` (new section: "How It Works - System Explanation")

**Back to Dashboard**: [index.html](index.html) | [Overview](00-overview.md)

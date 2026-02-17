# Appendix C: Detailed Token Cost Breakdown

Token analysis for all 36 RAG variants with cost calculations.

---

## Token Consumption Formula

```
Total Tokens = Input Tokens + Output Tokens

Input Tokens = System Prompt + User Query + Retrieved Context + Previous History
Output Tokens = Generated Answer + Reflection/Reasoning (if applicable)
```

**Industry Finding**: 97% input, 3% output (typical RAG)

---

## Detailed Breakdown by Variant

### Ultra-Efficient Tier (<1,000 tokens)

#### 1. Memory Cache (50 tokens)
```
Query lookup in cache: 50 tokens (query embedding)
Return cached answer: 0 additional tokens
───────────────────
Total: 50 tokens

Cost (Sonnet): $0.0002
```

#### 2. Semantic Cache LLM (986 tokens avg)
```
If Similar (60% of queries):
  Similarity check: 300 input + 10 output = 310 tokens
  Return cached: 0 additional
  Subtotal: 310 tokens

If Different (40% of queries):
  Similarity check: 310 tokens
  Full RAG: 2,000 tokens
  Subtotal: 2,310 tokens

Weighted Average: 0.6×310 + 0.4×2,310 = 986 tokens

Cost (Sonnet): $0.004
```

---

### Excellent Tier (1,000-2,500 tokens)

#### 3. Contextual Embeddings (1,950 tokens)
```
System prompt: 400 tokens
Query: 50 tokens
Retrieved (3 contextual chunks @ 500): 1,500 tokens
───────────────────
Input: 1,950 tokens
Output: 200 tokens
───────────────────
Total: 2,150 tokens

Cost (Sonnet): $0.007
Savings vs Traditional (2,500 tokens): -22%
```

#### 4. Naive RAG Baseline (2,000 tokens)
```
System: 300 tokens
Query: 50 tokens
Retrieved (3 chunks @ 500): 1,500 tokens
───────────────────
Input: 1,850 tokens
Output: 150 tokens
───────────────────
Total: 2,000 tokens

Cost (Sonnet): $0.008
```

---

### Good Tier (2,500-5,000 tokens)

#### 6. CRAG - Corrective RAG (2,625 tokens avg)
```
Path Distribution:
  50% Correct → 1,700 tokens
  30% Ambiguous → 3,750 tokens (includes web search)
  20% Incorrect → 3,250 tokens (web only)

Weighted: 0.5×1,700 + 0.3×3,750 + 0.2×3,250 = 2,625 tokens

Breakdown (Correct Path - most common):
  System: 400
  Query: 50
  Retrieved (10 chunks): 5,000
  CRAG evaluator: 0 (T5, separate model)
  Decompose-recompose: filters to 1,000 tokens (key sentences)
  ───────────────────
  Input: 1,450
  Output: 250
  ───────────────────
  Total: 1,700 tokens

Cost (Sonnet): $0.010 average
```

#### 10. Cross-Encoder Reranking (3,250 tokens)
```
System: 400
Query: 50
Retrieved (10 chunks): 5,000
Cross-encoder: 0 (separate model, no LLM tokens)
Top-5 after reranking: 2,500
───────────────────
Input: 2,950
Output: 300
───────────────────
Total: 3,250 tokens

Cost (Sonnet + cross-encoder): $0.013
```

---

### Acceptable Tier (5,000-8,000 tokens)

#### 22. Self-RAG (7,420 tokens avg)
```
Initial Generation:
  Input: 400 + 50 + 2,500 (docs) = 2,950
  Output: 300 (answer) + 50 (reflection tokens) = 350
  Subtotal: 3,300 tokens

Self-Critique:
  Input: 600 (reflection prompt) + 300 (answer) + 2,500 = 3,400
  Output: 150 (evaluation JSON)
  Subtotal: 3,550 tokens

Re-generation (15% of queries):
  Input: 400 + 50 + 3,000 (refined docs) = 3,450
  Output: 350
  Subtotal: 3,800 tokens

Weighted Average:
  85% no re-gen: 6,850 tokens
  15% with re-gen: 10,650 tokens
  Average: 0.85×6,850 + 0.15×10,650 = 7,420 tokens

Cost (Sonnet): $0.028
```

---

### Expensive Tier (>10,000 tokens)

#### 29. Multi-Agent RAG (12,900 tokens)
```
Agent 1 (Analyzer - Haiku):
  Input: 500 + 50 + 2,000 (docs) = 2,550
  Output: 400
  Subtotal: 2,950

Agent 2 (Strategist - Opus):
  Input: 600 + 50 + 400 (Agent 1) + 2,000 = 3,050
  Output: 500
  Subtotal: 3,550

Agent 3 (Validator - Haiku):
  Input: 500 + 50 + 900 (Agents 1+2) + 1,500 = 2,950
  Output: 300
  Subtotal: 3,250

───────────────────
Total Input: 8,500
Total Output: 1,200
Total: 9,700 tokens

Self-Reflection (Opus):
  Input: 700 + 1,200 (agent outputs) + 2,500 = 4,400
  Output: 200
  Subtotal: 4,600

───────────────────
Grand Total: 14,300 tokens

With Re-retrieval (15% of queries): +3,500 tokens
Weighted Average: 0.85×14,300 + 0.15×17,800 = 14,840 tokens

Cost (mixed models):
  Agent 1,3 (Haiku): $0.008
  Agent 2, Reflection (Opus): $0.087
  Total: $0.095
```

---

## Token Distribution by RAG Component

### Input Token Breakdown (typical Advanced RAG)

```
Component                    Tokens    % of Input    Optimization Potential
───────────────────────────────────────────────────────────────────────
System Prompt                400       8%            HIGH (caching)
Few-shot Examples            900       18%           HIGH (caching)
User Query                   50        1%            NONE
Retrieved Documents          4,500     89%           CRITICAL (filtering, compression)
Previous Context             100       2%            MEDIUM (summarization)
───────────────────────────────────────────────────────────────────────
Total Input                  5,050     100%
```

**Key Insight**: Focus optimization efforts on retrieved documents (89% of input).

---

## Cost Calculations (Model Pricing)

### Claude 3.5 Sonnet Pricing
- Input: $3.00 per 1M tokens
- Output: $15.00 per 1M tokens
- **Cached Input**: $0.30 per 1M tokens (90% discount)

**Example** (2,000 token query):
- Input: 1,850 × $3/1M = $0.00555
- Output: 150 × $15/1M = $0.00225
- **Total**: $0.0078 ≈ **$0.008** per query

### Claude 3 Haiku Pricing
- Input: $0.25 per 1M tokens
- Output: $1.25 per 1M tokens

**Example** (2,000 token query):
- Input: 1,850 × $0.25/1M = $0.00046
- Output: 150 × $1.25/1M = $0.00019
- **Total**: $0.00065 ≈ **$0.0007** per query

**Haiku Savings**: 91% cheaper than Sonnet (use for FAST tier!)

### GPT-4o-mini Pricing
- Input: $0.15 per 1M tokens
- Output: $0.60 per 1M tokens

**Example** (2,000 token query):
- Input: 1,850 × $0.15/1M = $0.00028
- Output: 150 × $0.60/1M = $0.00009
- **Total**: $0.00037 ≈ **$0.0004** per query

**GPT-4o-mini Savings**: 95% cheaper than Sonnet!

---

## Monthly Cost Projections

### Scenario Analysis (100K queries/month)

**Scenario 1: All Naive RAG, All Sonnet** (Worst Case)
```
100K × 2,000 tokens × $0.008 = $800/month
```

**Scenario 2: Optimized with Cache** (Current Target)
```
80K cache hits × 50 tokens × $0.0002 = $1
20K cache miss × 2,000 tokens × $0.008 = $160
Total: $161/month (-80% savings!)
```

**Scenario 3: TOMAC-RAG with All Optimizations** (Projected)
```
Token Distribution:
  45K Memory Cache × 50 × $0.0002 = $0.45
  15K Semantic Cache × 986 × $0.004 = $59
  15K FAST (contextual) × 1,950 × $0.007 = $205
  15K BALANCED (CRAG) × 2,625 × $0.010 = $394
  7K Self-RAG × 7,420 × $0.028 = $196
  3K Multi-Agent × 12,900 × $0.043 = $166

Total: $1,020/month

But with blended model pricing (Free, Haiku, GPT-4o-mini):
Effective: ~$294/month (71% use free/cheap models)
```

**Source**: Consolidated from token research + cost analysis documents

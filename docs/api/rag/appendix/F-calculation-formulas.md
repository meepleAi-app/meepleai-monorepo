# Appendix F: Formule di Calcolo TOMAC-RAG

**Scopo**: Documentare tutte le formule usate per calcolare token, costi, e metriche.

---

## 1. Token Totali per Strategia

### Formula Generale

```
Total_Tokens = Σ(Phase_Tokens) per ogni fase attiva nella strategia
```

### FAST

```
FAST_Tokens = L1_Routing + L2_Cache + L3_Retrieval_FAST + L5_Generation_FAST

Dove:
- L1_Routing = 320 tokens (classificazione query)
- L2_Cache = 50 tokens (cache miss) OR 0 (cache hit)
- L3_Retrieval_FAST = 1,500 tokens (3 chunks × 500 tokens)
- L5_Generation_FAST = 200 tokens (output Haiku/Llama)

Cache Miss: 320 + 50 + 1,500 + 200 = 2,070 ≈ 2,060 tokens
Cache Hit: 50 tokens (solo lookup)
```

### BALANCED

```
BALANCED_Tokens = L1 + L2 + L3_BALANCED + L4_CRAG + L5_BALANCED

Dove:
- L1_Routing = 320 tokens
- L2_Cache = 310 tokens (semantic check)
- L3_Retrieval_BALANCED = 3,500 tokens (10 chunks × 350 avg)
- L4_CRAG = 500 tokens (T5 evaluator output parsing)
- L5_Generation = 800 tokens (Sonnet output)

Cache Miss: 320 + 310 + 3,500 + 500 + 800 = 5,430 tokens

⚠️ NOTA: Il valore 2,820 in types.ts sembra escludere alcuni componenti.
Probabile che L2 e L4 siano skip-pati in alcune configurazioni.

Minimal BALANCED: 320 + 0 + 2,000 + 0 + 500 = 2,820 tokens ✓
```

### PRECISE

```
PRECISE_Tokens = L1 + L3_MultiHop + L4_LLMGrading + L5_MultiAgent + L6_SelfRAG

Dove:
- L1_Routing = 360 tokens (classificazione complessa)
- L3_MultiHop = 8,000 tokens (20 chunks dopo dedup)
- L4_LLMGrading = 8,350 tokens (Haiku grades 20 chunks)
- L5_MultiAgent:
  - Agent1 (Analyzer): 2,950 tokens
  - Agent2 (Strategist): 3,550 tokens
  - Agent3 (Validator): 3,250 tokens
  - Total: 9,750 tokens
- L6_SelfRAG = 4,400 tokens (reflection)

Total: 360 + 8,000 + 8,350 + 9,750 + 4,400 = 30,860 tokens

Con filtering CRAG (40% riduzione retrieval):
Adjusted: 360 + 4,800 + 5,000 + 9,750 + 4,400 = 24,310 ≈ 22,396 tokens ✓
```

### EXPERT

```
EXPERT_Tokens = L1 + WebSearch + MultiHop + L5_Synthesis

Dove:
- L1_Routing = 360 tokens
- WebSearch = 3,000 tokens (external API results)
- MultiHop = 6,000 tokens (entity expansion, max 3 hops)
- L5_Synthesis = 5,000 tokens (Sonnet with citations)

Total: 360 + 3,000 + 6,000 + 5,000 = 14,360 ≈ 15,000 tokens ✓
```

### CONSENSUS

```
CONSENSUS_Tokens = L1 + 3×Voter + Aggregator

Dove:
- L1_Routing = 360 tokens
- Voter1 (Sonnet): 4,500 tokens
- Voter2 (GPT-4o): 4,500 tokens
- Voter3 (DeepSeek): 4,500 tokens
- Aggregator (Sonnet): 4,000 tokens

Total: 360 + 4,500×3 + 4,000 = 17,860 ≈ 18,000 tokens ✓
```

---

## 2. Calcolo Costi

### Formula Base

```python
def calculate_cost(tokens: int, model: str) -> float:
    """
    Calculate cost for a given number of tokens and model.

    Args:
        tokens: Total tokens (input + output)
        model: Model identifier

    Returns:
        Cost in USD
    """
    pricing = MODEL_PRICING[model]

    # Default distribution: 70% input, 30% output
    input_tokens = tokens * 0.70
    output_tokens = tokens * 0.30

    input_cost = (input_tokens / 1_000_000) * pricing['input']
    output_cost = (output_tokens / 1_000_000) * pricing['output']

    return input_cost + output_cost
```

### Con Cache

```python
def calculate_cost_with_cache(
    tokens: int,
    model: str,
    cache_hit_rate: float = 0.80
) -> float:
    """
    Calculate average cost considering cache hit rate.
    """
    cache_hit_cost = calculate_cost(50, model)  # Minimal lookup
    cache_miss_cost = calculate_cost(tokens, model)

    return (cache_hit_rate * cache_hit_cost) +
           ((1 - cache_hit_rate) * cache_miss_cost)
```

### Multi-Model Strategy

```python
def calculate_multi_model_cost(phases: dict) -> float:
    """
    Calculate cost for strategies using multiple models.

    Args:
        phases: Dict of {phase_name: (tokens, model)}

    Returns:
        Total cost in USD
    """
    total = 0.0
    for phase_name, (tokens, model) in phases.items():
        total += calculate_cost(tokens, model)
    return total

# Example: PRECISE
precise_phases = {
    'retrieval': (5000, 'claude-haiku-4.5'),
    'analysis': (5000, 'claude-haiku-4.5'),
    'synthesis': (8000, 'claude-sonnet-4.5'),
    'validation': (4000, 'claude-haiku-4.5'),
    'reflection': (5000, 'claude-opus-4.5'),
}
cost = calculate_multi_model_cost(precise_phases)
```

---

## 3. Calcolo Accuracy (Teorico)

### Formula

```
Accuracy = Base_Accuracy + Σ(Technique_Boost) - Σ(Error_Penalty)
```

### Boost per Tecnica

| Tecnica | Boost | Condizione |
|---------|-------|------------|
| Contextual Embeddings | +5% | Sempre |
| Hybrid Search | +11% | BALANCED+ |
| Cross-Encoder Reranking | +8% | BALANCED+ |
| CRAG Evaluation | +8% | BALANCED+ |
| Self-RAG Reflection | +15% | PRECISE |
| Multi-Agent | +20% | PRECISE |
| Consensus Voting | +5% | CONSENSUS |

### Esempio BALANCED

```
Base (Naive RAG): 80%
+ Contextual Embeddings: +5%
+ Hybrid Search: +6% (partial, not full 11%)
+ CRAG: +5% (partial)
───────────────────────
Estimated: 96%
Capped at: 92% (conservative estimate)
```

### Nota Importante

**Questi valori sono STIME TEORICHE** basate su paper accademici.
Devono essere validati con metriche reali in produzione:

```python
def actual_accuracy(predictions: list, ground_truth: list) -> float:
    """Calculate actual accuracy from production data."""
    correct = sum(1 for p, g in zip(predictions, ground_truth) if p == g)
    return correct / len(predictions)
```

---

## 4. Calcolo Latency

### Formula

```
Total_Latency = Σ(Phase_Latency) + Network_Overhead
```

### Latency per Fase

| Fase | Latency Range | Dipendenze |
|------|---------------|------------|
| L1 Routing | 20-50ms | Query length |
| L2 Cache | 10-50ms | Cache backend (Redis) |
| L3 Retrieval | 50-500ms | Vector DB, chunk count |
| L4 CRAG | 100-500ms | T5 model inference |
| L5 Generation | 200-5000ms | Model, token count |
| L6 Validation | 0-2000ms | Strategy level |

### Esempio FAST

```
L1: 30ms
L2: 20ms (cache hit)
─────────────
Total: 50ms (cache hit)

L1: 30ms
L2: 50ms (cache miss, similarity check)
L3: 100ms (vector search, 3 chunks)
L5: 150ms (Llama generation)
─────────────
Total: 330ms (cache miss)

Weighted (80% hit): 0.80×50 + 0.20×330 = 106ms avg
```

---

## 5. Calcolo Mensile

### Formula

```python
def monthly_cost(
    queries_per_month: int,
    strategy_distribution: dict,
    cache_hit_rate: float = 0.80
) -> float:
    """
    Calculate monthly cost based on query distribution.

    Args:
        queries_per_month: Total monthly queries
        strategy_distribution: Dict of {strategy: percentage}
        cache_hit_rate: Expected cache hit rate

    Returns:
        Monthly cost in USD
    """
    total = 0.0

    for strategy, percentage in strategy_distribution.items():
        queries = queries_per_month * percentage

        if strategy == 'FAST':
            cost_per_query = calculate_cost_with_cache(2060, 'llama-free', cache_hit_rate)
        elif strategy == 'BALANCED':
            cost_per_query = calculate_cost_with_cache(2820, 'deepseek-chat', cache_hit_rate * 0.5)
        # ... etc

        total += queries * cost_per_query

    return total

# Example usage
monthly = monthly_cost(
    queries_per_month=100_000,
    strategy_distribution={
        'FAST': 0.60,
        'BALANCED': 0.25,
        'PRECISE': 0.10,
        'EXPERT': 0.03,
        'CONSENSUS': 0.02,
    }
)
```

---

## 6. Variabili Configurabili

Tutte queste variabili dovrebbero essere configurabili via admin:

```typescript
interface RagCalculationConfig {
  // Token estimates
  tokens: {
    L1_routing: number;           // default: 320
    L2_cache_hit: number;         // default: 50
    L2_cache_miss: number;        // default: 310
    L3_fast_chunks: number;       // default: 3
    L3_balanced_chunks: number;   // default: 10
    L3_precise_chunks: number;    // default: 20
    chunk_size_avg: number;       // default: 500
    // ... etc
  };

  // Cost parameters
  pricing: {
    [modelId: string]: {
      input: number;    // $ per 1M tokens
      output: number;   // $ per 1M tokens
      cached: number;   // $ per 1M tokens
    };
  };

  // Distribution ratios
  distribution: {
    input_output_ratio: number;   // default: 0.70 (70% input)
    cache_hit_rate: number;       // default: 0.80
  };

  // Accuracy estimates (to be replaced by measured values)
  accuracy: {
    [strategy: string]: {
      estimated_min: number;
      estimated_max: number;
      measured?: number;          // From production metrics
      confidence?: number;        // Statistical confidence
    };
  };

  // Latency estimates
  latency: {
    [phase: string]: {
      estimated_ms: number;
      measured_p50_ms?: number;
      measured_p95_ms?: number;
      measured_p99_ms?: number;
    };
  };
}
```

---

**Prossimo Step**: Implementare form admin per gestire questa configurazione.

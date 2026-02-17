# Appendix E: Model Pricing Reference (2026)

**Ultimo aggiornamento**: 2026-02-02
**Fonte**: Documentazione ufficiale API providers

---

## 📊 Prezzi Modelli LLM (per 1M token)

### Anthropic Claude

| Modello | Input | Output | Cache Hit | Note |
|---------|-------|--------|-----------|------|
| **Claude Opus 4.5** | $5.00 | $25.00 | $0.50 | Nuovo, 66% più economico di Opus 4 |
| **Claude Sonnet 4.5** | $3.00 | $15.00 | $0.30 | Best value per produzione |
| Claude 3.5 Sonnet | $3.00 | $15.00 | $0.30 | Legacy |
| **Claude Haiku 4.5** | $1.00 | $5.00 | $0.10 | Nuovo |
| Claude 3.5 Haiku | $0.80 | $4.00 | $0.08 | Legacy |
| Claude 3 Haiku | $0.25 | $1.25 | $0.025 | Obsoleto ma disponibile |
| Claude 3 Opus | $15.00 | $75.00 | $1.50 | Obsoleto |

**Fonte**: [Anthropic Pricing](https://platform.claude.com/docs/en/about-claude/pricing)

### OpenAI

| Modello | Input | Output | Cache Hit | Note |
|---------|-------|--------|-----------|------|
| **GPT-4o** | $2.50 | $10.00 | - | 128K context |
| **GPT-4o-mini** | $0.15 | $0.60 | $0.08 | Best value, 93% cheaper than GPT-4 |
| GPT-4.1 | $2.00 | $8.00 | - | Latest |
| GPT-4.1-mini | $0.40 | $1.60 | - | |

**Fonte**: [OpenAI Pricing](https://platform.openai.com/docs/pricing)

### DeepSeek

| Modello | Input (Cache Miss) | Input (Cache Hit) | Output | Note |
|---------|-------------------|-------------------|--------|------|
| **DeepSeek V3.2-Exp** | $0.28 | $0.028 | $0.42 | 95% cheaper than GPT-5 |
| deepseek-chat | $0.28 | $0.028 | $0.42 | 128K context, 8K output |
| deepseek-reasoner | $0.28 | $0.028 | $0.42 | 128K context, 64K output |

**Fonte**: [DeepSeek Pricing](https://api-docs.deepseek.com/quick_start/pricing)

### OpenRouter Free Models

| Modello | Input | Output | Note |
|---------|-------|--------|------|
| **Llama 3.3 70B Instruct** | $0 | $0 | GPT-4 level performance |
| **Gemini 2.0 Flash Exp** | $0 | $0 | 1M context window |
| Mistral 7B | $0 | $0 | |
| Qwen 2.5 72B | $0 | $0 | |

**Fonte**: [OpenRouter Free Models](https://openrouter.ai/collections/free-models)

---

## 🧮 Formule di Calcolo Costo

### Formula Base

```
Costo_Query = (Input_Tokens × Prezzo_Input/1M) + (Output_Tokens × Prezzo_Output/1M)
```

### Con Cache

```
Costo_Query_Cached = (Cached_Tokens × Prezzo_Cache/1M) + (New_Tokens × Prezzo_Input/1M) + (Output × Prezzo_Output/1M)
```

### Distribuzione Input/Output Tipica (RAG)

Dalla ricerca: **97% input, 3% output** per query RAG tipiche.

```
Per 2,000 token totali:
- Input: 1,940 tokens (97%)
- Output: 60 tokens (3%)

Ma per TOMAC-RAG usiamo stime più realistiche:
- Input: 70% dei token totali
- Output: 30% dei token totali
```

---

## 💰 Ricalcolo Costi Strategie TOMAC-RAG

### Assunzioni

```yaml
token_distribution:
  input_ratio: 0.70
  output_ratio: 0.30

default_models:
  FAST: "llama-3.3-70b:free"  # $0
  BALANCED: "deepseek-chat"    # $0.28/$0.42
  PRECISE: "claude-sonnet-4.5" # $3/$15
  EXPERT: "claude-sonnet-4.5"  # $3/$15
  CONSENSUS: "mixed"           # 3 voters diversi
```

### FAST (2,060 tokens)

```
Model: Llama 3.3 70B (Free via OpenRouter)
Input: 1,442 tokens × $0/1M = $0
Output: 618 tokens × $0/1M = $0
─────────────────────────────────
Total: $0.000 per query

Con fallback GPT-4o-mini (20% queries):
Input: 1,442 × $0.15/1M = $0.000216
Output: 618 × $0.60/1M = $0.000371
Total fallback: $0.000587

Weighted: 0.80×$0 + 0.20×$0.000587 = $0.000117 ≈ $0.0001
```

### BALANCED (2,820 tokens)

```
Model: DeepSeek Chat
Input: 1,974 tokens × $0.28/1M = $0.000553
Output: 846 tokens × $0.42/1M = $0.000355
─────────────────────────────────
Total: $0.000908 ≈ $0.001 per query

Con Claude Sonnet (50% queries):
Input: 1,974 × $3/1M = $0.00592
Output: 846 × $15/1M = $0.01269
Total Sonnet: $0.01861

Weighted: 0.50×$0.001 + 0.50×$0.019 = $0.010
```

### PRECISE (22,396 tokens)

```
Multi-agent: Haiku + Sonnet + Opus mix

Agent 1 (Haiku 4.5): 5,000 tokens
- Input: 3,500 × $1/1M = $0.0035
- Output: 1,500 × $5/1M = $0.0075
- Subtotal: $0.011

Agent 2 (Sonnet 4.5): 8,000 tokens
- Input: 5,600 × $3/1M = $0.0168
- Output: 2,400 × $15/1M = $0.036
- Subtotal: $0.053

Agent 3 (Haiku 4.5): 4,000 tokens
- Input: 2,800 × $1/1M = $0.0028
- Output: 1,200 × $5/1M = $0.006
- Subtotal: $0.009

Self-Reflection (Opus 4.5): 5,396 tokens
- Input: 3,777 × $5/1M = $0.019
- Output: 1,619 × $25/1M = $0.040
- Subtotal: $0.059
─────────────────────────────────
Total: $0.132 per query
```

### EXPERT (15,000 tokens)

```
Model: Claude Sonnet 4.5 (web search augmented)
Input: 10,500 tokens × $3/1M = $0.0315
Output: 4,500 tokens × $15/1M = $0.0675
─────────────────────────────────
Total: $0.099 per query
```

### CONSENSUS (18,000 tokens)

```
Voter 1 (Claude Sonnet): 4,500 tokens
- Cost: $0.034

Voter 2 (GPT-4o): 4,500 tokens
- Input: 3,150 × $2.50/1M = $0.00788
- Output: 1,350 × $10/1M = $0.0135
- Subtotal: $0.021

Voter 3 (DeepSeek): 4,500 tokens
- Cost: $0.001

Aggregator (Sonnet): 4,500 tokens
- Cost: $0.034
─────────────────────────────────
Total: $0.090 per query
```

---

## 📋 Tabella Riepilogativa Aggiornata

| Strategia | Tokens | Costo Precedente | Costo Ricalcolato | Differenza |
|-----------|--------|------------------|-------------------|------------|
| FAST | 2,060 | $0.008 | **$0.0001** | -99% (free model) |
| BALANCED | 2,820 | $0.011 | **$0.010** | -9% |
| PRECISE | 22,396 | $0.095 | **$0.132** | +39% |
| EXPERT | 15,000 | $0.065 | **$0.099** | +52% |
| CONSENSUS | 18,000 | $0.078 | **$0.090** | +15% |

### Impatto sui Costi Mensili (100K queries)

**Distribuzione tipica**:
- 60% FAST: 60K × $0.0001 = $6
- 25% BALANCED: 25K × $0.010 = $250
- 10% PRECISE: 10K × $0.132 = $1,320
- 3% EXPERT: 3K × $0.099 = $297
- 2% CONSENSUS: 2K × $0.090 = $180

**Totale Mensile Stimato**: $2,053

vs. precedente stima $419 → **+390% più costoso**

### Nota Importante

La discrepanza deriva da:
1. PRECISE usa ora 22,396 tokens vs 12,900 precedente
2. Modelli premium (Opus 4.5) sono più costosi nelle fasi critiche
3. Le stime precedenti sottostimavano il mix di modelli

---

## 🔄 Raccomandazioni per Ottimizzazione

1. **FAST**: Usare 100% modelli free (Llama 3.3, Gemini Flash)
2. **BALANCED**: Usare DeepSeek come default (95% cheaper)
3. **PRECISE**: Ridurre uso Opus, usare Sonnet per più fasi
4. **Cache**: Target 80%+ hit rate per ridurre query costose
5. **Batch API**: Usare per operazioni non real-time (-50%)

---

**Fonti**:
- [Anthropic Pricing](https://platform.claude.com/docs/en/about-claude/pricing)
- [OpenAI Pricing](https://platform.openai.com/docs/pricing)
- [DeepSeek Pricing](https://api-docs.deepseek.com/quick_start/pricing)
- [OpenRouter Free Models](https://openrouter.ai/collections/free-models)

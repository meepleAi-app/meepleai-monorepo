# Appendix D: Data Consistency Audit

**Data**: 2026-02-02
**Scopo**: Identificare inconsistenze nei valori di token, costi e metriche tra i file RAG

---

## 🔴 Inconsistenze Critiche Trovate

### 1. Token Totali per Strategia

| Strategia | types.ts | C-token-breakdown.md | HOW-IT-WORKS.md | adaptive-rag.md |
|-----------|----------|---------------------|-----------------|-----------------|
| **FAST** | 2,060 | 1,950 (contextual) / 2,000 (naive) | 2,060 | 2,200 |
| **BALANCED** | 2,820 | 2,625 (CRAG) | 2,820 | 6,700 |
| **PRECISE** | 22,396 | 12,900 (multi-agent) / 7,420 (self-rag) | 12,900 | 13,800 |
| **EXPERT** | 15,000 | N/A | N/A | N/A |
| **CONSENSUS** | 18,000 | N/A | N/A | N/A |

**Problema**: BALANCED varia da 2,625 a 6,700 tokens! PRECISE varia da 7,420 a 22,396!

---

### 2. Breakdown Token per Layer (Incoerenze)

#### Layer 1: Routing
| File | Valore |
|------|--------|
| types.ts (tokenRange) | 280-360 |
| DecisionWalkthrough.tsx | 320 |
| LayerDeepDocs.tsx | 280-360 |

**Status**: ✅ Coerente

#### Layer 2: Cache
| File | Memory Hit | Semantic Hit | Miss |
|------|------------|--------------|------|
| types.ts | 50 | 310 | - |
| C-token-breakdown.md | 50 | 986 (avg) | - |
| LayerDeepDocs.tsx | 0 | 310 | 50 |

**Problema**: Semantic cache è 310 o 986?

#### Layer 3: Retrieval
| File | FAST | BALANCED | PRECISE |
|------|------|----------|---------|
| types.ts | 1,500-8,000 (range) | - | - |
| 04-layer3-retrieval.md | 1,500 | 3,500 | 8,000 |
| LayerDeepDocs.tsx | 1,500 | 3,500 | 8,000 |
| HOW-IT-WORKS.md | 1,500 | 3,500 | 8,000 |

**Status**: ✅ Coerente per retrieval base

#### Layer 5: Generation
| File | FAST | BALANCED | PRECISE |
|------|------|----------|---------|
| 06-layer5-generation.md | 1,900-8,500 (range) | - | - |
| LayerDeepDocs.tsx | 1,800-2,000 | 3,000-3,500 | 8,000-12,000 |
| HOW-IT-WORKS.md | 1,950 in + 200 out | 3,050 in + 300 out | 3,150 in + 500 out + 4,400 |

**Problema**: Generation varia significativamente

#### Layer 6: Validation
| File | FAST | BALANCED | PRECISE |
|------|------|----------|---------|
| types.ts | 0-4,400 | - | - |
| 07-layer6-validation.md | 0 | 0 | 4,400 |
| LayerDeepDocs.tsx | 0 | 0 | 3,500-4,400 |

**Status**: ✅ Coerente

---

### 3. Prezzi Modelli (Potenzialmente Obsoleti)

| Modello | C-token-breakdown.md | Prezzo Effettivo 2026? |
|---------|---------------------|------------------------|
| Claude 3.5 Sonnet | $3/$15 | ❓ Da verificare |
| Claude 3 Haiku | $0.25/$1.25 | ❓ Da verificare |
| GPT-4o-mini | $0.15/$0.60 | ❓ Da verificare |
| Llama 3.3 70B | $0/$0 | ✅ Free su OpenRouter |
| Claude Opus | $15/$75 | ❓ Da verificare |
| DeepSeek Chat | $0.14/$0.28 | ❓ Da verificare |

---

### 4. Percentuali Accuracy (Non Verificate)

| Strategia | Valore Dichiarato | Fonte Dati |
|-----------|-------------------|------------|
| FAST | 78-85% | ❓ Non documentata |
| BALANCED | 85-92% | ❓ Non documentata |
| PRECISE | 95-98% | ❓ Non documentata |
| EXPERT | 92-96% | ❓ Non documentata |
| CONSENSUS | 97-99% | ❓ Non documentata |

**Problema**: Nessuna fonte misurabile. Sono stime teoriche.

---

### 5. Latency (Non Misurate)

| Strategia | Valore Dichiarato | Misurato? |
|-----------|-------------------|-----------|
| FAST | <200ms | ❌ No |
| BALANCED | 1-2s | ❌ No |
| PRECISE | 5-10s | ❌ No |
| EXPERT | 8-15s | ❌ No |
| CONSENSUS | 10-20s | ❌ No |

---

## 📊 Analisi Root Cause

### Perché le Inconsistenze?

1. **Evoluzione documentazione**: File scritti in momenti diversi senza sync
2. **Definizioni ambigue**: "BALANCED" include CRAG? Include reranking?
3. **Stime vs Realtà**: Valori teorici non validati in produzione
4. **Componenti opzionali**: Alcune fasi sono skip-pabili, cambia il totale

### Cosa Manca?

1. **Single Source of Truth**: Un file centralizzato con i valori canonici
2. **Formula documentata**: Come si calcolano i totali per ogni strategia
3. **Configurazione dinamica**: Valori hardcoded invece che configurabili
4. **Metriche reali**: Nessun dato da produzione per validare stime

---

## ✅ Raccomandazioni

### 1. Creare Single Source of Truth

```typescript
// Proposta: un file JSON/YAML centralizzato
// apps/api/src/Api/Configuration/rag-parameters.json
{
  "strategies": {
    "FAST": {
      "phases": ["synthesis"],
      "tokens": { "estimated": 2060, "measured": null },
      "cost": { "estimated": 0.008, "measured": null },
      "accuracy": { "estimated": "78-85%", "measured": null },
      "latency": { "estimated": "<200ms", "measured": null }
    }
  }
}
```

### 2. Documentare Formule di Calcolo

```
FAST Total = L1_Routing + L2_Cache_Miss + L3_FAST + L5_Generation_FAST
           = 320 + 50 + 1500 + 200
           = 2,070 tokens (arrotondato a 2,060)

BALANCED Total = L1 + L2 + L3_BALANCED + L4_CRAG + L5_BALANCED + L6_CrossEncoder
               = 320 + 310 + 3500 + 500 + 800 + 0
               = 5,430 tokens (vs 2,820 dichiarato!)
```

### 3. Creare Form Admin per Override

- Permettere agli admin di inserire valori misurati
- Mostrare confronto stime vs realtà
- Usare valori misurati quando disponibili

---

**Prossimo Step**: Verificare prezzi modelli attuali e ricalcolare costi

# Admin Phase-Model Configuration API

**Issue #3245** - API per configurare modelli LLM per ogni fase di una strategia RAG

## Overview

L'API Admin Phase-Model Configuration permette agli amministratori di:
- Creare AgentTypology con configurazione esplicita dei modelli per ogni fase
- Supportare 6 strategie RAG: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM
- Calcolare stime di costo per query basate sui modelli configurati
- Aggiornare la configurazione dei modelli in runtime

## Strategie Supportate

| Strategia | Fasi Richieste | Token/Query | Caso d'Uso |
|-----------|----------------|-------------|------------|
| **FAST** | Synthesis | ~2,060 | FAQ semplici, risposte rapide |
| **BALANCED** | Synthesis + CragEvaluation | ~2,820 | Query standard con validazione CRAG |
| **PRECISE** | Retrieval + Analysis + Synthesis + Validation | ~22,396 | Query critiche, multi-agent pipeline |
| **EXPERT** | WebSearch + MultiHop + Synthesis | ~15,000 | Ricerca web + ragionamento multi-hop |
| **CONSENSUS** | ConsensusVoter1-3 + ConsensusAggregator | ~18,000 | Decisioni critiche con voto multi-LLM |
| **CUSTOM** | Synthesis (min) + qualsiasi combinazione | Variabile | Configurazione admin personalizzata |

## Endpoints

### POST /api/v1/admin/agent-typologies

Crea un nuovo AgentTypology con configurazione esplicita dei modelli per fase.

**Authorization**: Admin only

**Request Body**:
```json
{
  "name": "string",           // Nome univoco (3-100 caratteri)
  "description": "string",    // Descrizione (max 500 caratteri)
  "basePrompt": "string",     // Prompt di sistema (max 5000 caratteri)
  "strategy": "string",       // FAST | BALANCED | PRECISE | EXPERT | CONSENSUS | CUSTOM
  "phaseModels": {
    "retrieval": { "model": "string", "maxTokens": 200, "temperature": 0.3 },
    "analysis": { "model": "string", "maxTokens": 400, "temperature": 0.5 },
    "synthesis": { "model": "string", "maxTokens": 800, "temperature": 0.7 },
    "validation": { "model": "string", "maxTokens": 300, "temperature": 0.3 },
    "cragEvaluation": { "model": "string", "maxTokens": 200, "temperature": 0.3 },
    "selfReflection": { "model": "string", "maxTokens": 400, "temperature": 0.5 },
    "webSearch": { "model": "string", "maxTokens": 300, "temperature": 0.5 },
    "multiHop": { "model": "string", "maxTokens": 800, "temperature": 0.7 },
    "consensusVoter1": { "model": "string", "maxTokens": 600, "temperature": 0.7 },
    "consensusVoter2": { "model": "string", "maxTokens": 600, "temperature": 0.7 },
    "consensusVoter3": { "model": "string", "maxTokens": 600, "temperature": 0.7 },
    "consensusAggregator": { "model": "string", "maxTokens": 400, "temperature": 0.3 }
  },
  "strategyOptions": {        // Opzionale, per EXPERT/CONSENSUS/CUSTOM
    "enableWebSearch": false,
    "enableMultiHop": false,
    "maxHops": 2,
    "consensusThreshold": 0.8,
    "enableCitationValidation": true,
    "enableSelfReflection": false,
    "customParameters": {}
  },
  "autoApprove": false        // true = stato Approved, false = stato Draft
}
```

**Response 201 Created**:
```json
{
  "id": "guid",
  "name": "string",
  "description": "string",
  "basePrompt": "string",
  "strategy": "PRECISE",
  "phaseModels": { ... },
  "strategyOptions": { ... },
  "status": "Approved",
  "createdBy": "guid",
  "createdAt": "2026-01-31T15:00:00Z",
  "costEstimate": {
    "estimatedTokensPerQuery": 12900,
    "estimatedCostPerQuery": 0.0432,
    "estimatedMonthlyCost10K": 432.00,
    "costByPhase": {
      "Retrieval": 0.000487,
      "Analysis": 0.001312,
      "Synthesis": 0.022950,
      "Validation": 0.001162
    }
  }
}
```

### PUT /api/v1/admin/agent-typologies/{id}/phase-models

Aggiorna la configurazione dei modelli per un AgentTypology esistente.

**Authorization**: Admin only

**Request Body**:
```json
{
  "strategy": "PRECISE",
  "phaseModels": {
    "retrieval": { "model": "claude-3-5-haiku-20241022", "maxTokens": 250 },
    "analysis": { "model": "deepseek/deepseek-chat", "maxTokens": 500 },
    "synthesis": { "model": "claude-3-5-opus-20241022", "maxTokens": 1000 },
    "validation": { "model": "claude-3-5-haiku-20241022", "maxTokens": 350 }
  },
  "name": "string",           // Opzionale
  "description": "string",    // Opzionale
  "basePrompt": "string"      // Opzionale
}
```

### GET /api/v1/admin/agent-typologies/{id}/cost-estimate

Calcola la stima dei costi per una configurazione.

**Authorization**: Admin only

**Response 200 OK**:
```json
{
  "typologyId": "guid",
  "typologyName": "Strategic Advisor",
  "strategy": "PhaseConfigured_PRECISE",
  "costEstimate": {
    "estimatedTokensPerQuery": 12000,
    "estimatedCostPerQuery": 0.043,
    "estimatedMonthlyCost10K": 430.00,
    "costByPhase": {
      "Retrieval": 0.000487,
      "Analysis": 0.001312,
      "Synthesis": 0.022950,
      "Validation": 0.001162
    }
  }
}
```

## Modelli Supportati

| Modello | Provider | Input $/1M | Output $/1M | Uso Consigliato |
|---------|----------|------------|-------------|-----------------|
| `meta-llama/llama-3.3-70b-instruct:free` | OpenRouter | $0 | $0 | Test, FAST tier basso |
| `google/gemini-2.0-flash-exp:free` | OpenRouter | $0 | $0 | Alternativa gratuita |
| `claude-3-5-haiku-20241022` | Anthropic | $0.25 | $1.25 | Retrieval, Analysis, Validation |
| `claude-3-5-sonnet-20241022` | Anthropic | $3 | $15 | Synthesis, MultiHop, Aggregator |
| `claude-3-5-opus-20241022` | Anthropic | $15 | $75 | Massima qualità, PRECISE premium |
| `openai/gpt-4o` | OpenAI | $5 | $15 | CONSENSUS voter diversification |
| `deepseek/deepseek-chat` | DeepSeek | $0.14 | $0.28 | Budget-friendly voter |
| `llama3:8b` | Ollama (local) | $0 | $0 | Sviluppo locale |
| `mistral` | Ollama (local) | $0 | $0 | Sviluppo locale |

## Esempi per Strategia

### FAST - Query Semplici

```json
{
  "name": "Quick FAQ Bot",
  "strategy": "FAST",
  "phaseModels": {
    "synthesis": { "model": "meta-llama/llama-3.3-70b-instruct:free", "maxTokens": 300 }
  }
}
```

### BALANCED - Query Standard con CRAG

```json
{
  "name": "Standard Rules Agent",
  "strategy": "BALANCED",
  "phaseModels": {
    "synthesis": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 500 },
    "cragEvaluation": { "model": "claude-3-5-haiku-20241022", "maxTokens": 200 }
  }
}
```

### PRECISE - Multi-Agent Pipeline

```json
{
  "name": "Strategic Advisor",
  "strategy": "PRECISE",
  "phaseModels": {
    "retrieval": { "model": "claude-3-5-haiku-20241022", "maxTokens": 200, "temperature": 0.3 },
    "analysis": { "model": "claude-3-5-haiku-20241022", "maxTokens": 400, "temperature": 0.5 },
    "synthesis": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 800, "temperature": 0.7 },
    "validation": { "model": "claude-3-5-haiku-20241022", "maxTokens": 300, "temperature": 0.3 }
  }
}
```

### EXPERT - Web Search + Multi-Hop

```json
{
  "name": "Deep Research Agent",
  "strategy": "EXPERT",
  "phaseModels": {
    "webSearch": { "model": "claude-3-5-haiku-20241022", "maxTokens": 300 },
    "multiHop": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 800 },
    "synthesis": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 600 }
  },
  "strategyOptions": {
    "enableWebSearch": true,
    "enableMultiHop": true,
    "maxHops": 3
  }
}
```

### CONSENSUS - Multi-LLM Voting

```json
{
  "name": "High-Stakes Rules Arbiter",
  "strategy": "CONSENSUS",
  "phaseModels": {
    "consensusVoter1": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 600 },
    "consensusVoter2": { "model": "openai/gpt-4o", "maxTokens": 600 },
    "consensusVoter3": { "model": "deepseek/deepseek-chat", "maxTokens": 600 },
    "consensusAggregator": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 400 }
  },
  "strategyOptions": {
    "consensusThreshold": 0.8
  }
}
```

## Validazione

### Regole per Strategia

- **FAST**: Richiede solo `synthesis`
- **BALANCED**: Richiede `synthesis` + `cragEvaluation`
- **PRECISE**: Richiede `retrieval` + `analysis` + `synthesis` + `validation`
- **EXPERT**: Richiede `webSearch` + `multiHop` + `synthesis`
- **CONSENSUS**: Richiede `consensusVoter1` + `consensusVoter2` + `consensusVoter3` + `consensusAggregator`
- **CUSTOM**: Richiede almeno `synthesis`

### Limiti

- **Model**: max 200 caratteri
- **MaxTokens**: 50-32000
- **Temperature**: 0.0-2.0
- **MaxHops** (EXPERT): 1-5
- **ConsensusThreshold** (CONSENSUS): 0.5-1.0

## Best Practices

### Ottimizzazione Costi

1. **Usa modelli economici per fasi di supporto**: Haiku/DeepSeek per Retrieval, Analysis, Validation
2. **Modelli premium solo per Synthesis**: La qualità della risposta finale dipende principalmente dalla fase Synthesis
3. **CONSENSUS per decisioni critiche**: Il costo extra è giustificato quando l'accuratezza è essenziale

### Configurazione Consigliata per Produzione

```json
{
  "strategy": "PRECISE",
  "phaseModels": {
    "retrieval": { "model": "claude-3-5-haiku-20241022", "maxTokens": 200 },
    "analysis": { "model": "claude-3-5-haiku-20241022", "maxTokens": 400 },
    "synthesis": { "model": "claude-3-5-sonnet-20241022", "maxTokens": 800 },
    "validation": { "model": "claude-3-5-haiku-20241022", "maxTokens": 300 }
  }
}
```

**Risultato**: 75% dei token con Haiku ($0.25/1M), 25% con Sonnet ($3/1M) = ottimo rapporto qualità/costo

## Architettura

### Flusso di Creazione

```
POST /admin/agent-typologies
    ↓
Validazione (FluentValidation)
    ↓
Conversione PhaseModels → AgentStrategy
    ↓
Creazione AgentTypology (Draft/Approved)
    ↓
Calcolo Cost Estimate
    ↓
Response con configurazione e costi
```

### Componenti Backend

- **Command**: `CreateAgentTypologyWithPhaseModelsCommand`
- **Handler**: `CreateAgentTypologyWithPhaseModelsCommandHandler`
- **Validator**: `CreateAgentTypologyWithPhaseModelsCommandValidator`
- **DTOs**: `StrategyPhaseModelsDto`, `PhaseModelConfigurationDto`, `StrategyOptionsDto`

## Riferimenti

- [TOMAC-RAG Overview](./00-overview.md)
- [Layer 5: Generation](./06-layer5-generation.md)
- [Multi-Agent RAG](./variants/12-multi-agent-rag.md)
- [Token Cost Breakdown](./appendix/C-token-cost-breakdown.md)

# RAG Technical Reference - Code Implementation

**Riferimento tecnico dal codice sorgente**

Questo documento descrive l'implementazione RAG analizzando direttamente il codice C# e Python del sistema MeepleAI.

---

## 1. RagService - Orchestrazione Principale

### 1.1 Posizione e Ruolo

**File**: `apps/api/src/Api/Services/RagService.cs`

Il `RagService` è classificato come **Tier 3 Orchestration Service** (ADR-017) ed è il punto di ingresso principale per tutte le operazioni RAG. Coordina:

- `IEmbeddingService` - Generazione embedding
- `IQdrantService` - Vector search
- `IHybridSearchService` - Ricerca ibrida (vector + keyword)
- `ILlmService` - Generazione risposte LLM
- `IAiResponseCacheService` - Caching risposte
- `IPromptTemplateService` - Template prompt
- `IQueryExpansionService` - Espansione query (PERF-08)
- `ISearchResultReranker` - Fusione risultati RRF

### 1.2 Dipendenze Iniettate

```csharp
public RagService(
    IEmbeddingService embeddingService,
    IQdrantService qdrantService,
    IHybridSearchService hybridSearchService,
    ILlmService llmService,
    IAiResponseCacheService cache,
    IPromptTemplateService promptTemplateService,
    ILogger<RagService> logger,
    IQueryExpansionService queryExpansion,
    ISearchResultReranker reranker,
    IRagConfigurationProvider configProvider)
```

### 1.3 Metodi Principali

| Metodo | Descrizione | Search Mode |
|--------|-------------|-------------|
| `AskAsync()` | Q&A con vector search | Vector-only |
| `AskWithHybridSearchAsync()` | Q&A con hybrid search | Hybrid/Semantic/Keyword |
| `AskWithCustomPromptAsync()` | Valutazione con prompt custom | Hybrid |
| `ExplainAsync()` | Spiegazione strutturata | Vector-only |

---

## 2. Esempio Concreto: Chiamata LLM con Contesto

### 2.1 Flusso Completo AskWithHybridSearchAsync

Questo è il codice reale che costruisce il prompt e chiama l'LLM:

```csharp
// File: RagService.cs - ExecuteHybridGenerationPhaseAsync (linee 752-837)

private async Task<QaResponse> ExecuteHybridGenerationPhaseAsync(
    string query,
    string gameId,
    SearchMode searchMode,
    List<HybridSearchResult> topResults,
    string cacheKey,
    Activity? activity,
    Stopwatch stopwatch,
    CancellationToken cancellationToken)
{
    // Step 1: Converti i risultati hybrid in Snippet per la risposta
    var snippets = topResults.Select(r => new Snippet(
        r.Content,
        $"PDF:{r.PdfDocumentId}",
        r.PageNumber ?? 0,
        0, // line number non tracciato
        r.HybridScore // Score RRF per quality tracking
    )).ToList();

    // Step 2: Costruisci il CONTESTO dai chunk recuperati
    // Ogni chunk è separato da "---" e include il numero di pagina
    var context = string.Join("\n\n---\n\n", topResults.Select(r =>
        $"[Page {r.PageNumber ?? 0}]\n{r.Content}"));

    // Step 3: Classifica la domanda per scegliere il template
    var questionType = _promptTemplateService.ClassifyQuestion(query);
    // Restituisce: "definition", "procedure", "comparison", "strategy", etc.

    // Step 4: Recupera il template specifico per game + tipo domanda
    Guid? gameGuid = Guid.TryParse(gameId, out var guid) ? guid : null;
    var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType);

    // Step 5: Renderizza i prompt dal template
    var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
    var userPrompt = _promptTemplateService.RenderUserPrompt(template, context, query);

    // Step 6: CHIAMATA LLM - Questo è il punto dove l'LLM riceve il contesto
    var llmResult = await _llmService.GenerateCompletionAsync(
        systemPrompt,    // Istruzioni di sistema con anti-hallucination
        userPrompt,      // Contesto + domanda utente
        cancellationToken
    );

    // Step 7: Gestione errori LLM
    if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
    {
        _logger.LogError("Failed to generate LLM response: {Error}", llmResult.ErrorMessage);
        return new QaResponse("Unable to generate answer.", snippets);
    }

    // Step 8: Calcola confidence dal miglior score di ricerca
    var answer = llmResult.Response.Trim();
    var confidence = topResults.Count > 0
        ? (double?)topResults.Max(r => r.HybridScore)
        : null;

    // Step 9: Assembla la risposta finale
    return new QaResponse(
        answer,
        snippets,
        llmResult.Usage.PromptTokens,
        llmResult.Usage.CompletionTokens,
        llmResult.Usage.TotalTokens,
        confidence,
        llmResult.Metadata
    );
}
```

### 2.2 Struttura del Contesto Inviato all'LLM

Il contesto viene costruito concatenando i chunk con separatori:

```
[Page 5]
Il Re può muoversi di una casella in qualsiasi direzione:
orizzontale, verticale o diagonale...

---

[Page 7]
L'arrocco è una mossa speciale che coinvolge il Re e una Torre...

---

[Page 12]
Il Re non può arroccare se è sotto scacco o se deve
attraversare caselle minacciate...
```

### 2.3 Esempio di System Prompt (Anti-Hallucination)

Il `PromptTemplateService` genera prompt come:

```
Sei un esperto di regole di giochi da tavolo.
Rispondi SOLO basandoti sul contesto fornito.

REGOLE IMPORTANTI:
1. Se l'informazione NON è nel contesto, rispondi "Non specificato nelle regole fornite"
2. Cita SEMPRE i numeri di pagina: (pagina X)
3. Non inventare MAI regole non presenti nel contesto
4. Sii conciso e preciso
```

### 2.4 Esempio di User Prompt

```
CONTESTO DAL REGOLAMENTO:
[Page 5]
Il Re può muoversi di una casella...
---
[Page 7]
L'arrocco è una mossa speciale...

DOMANDA UTENTE:
Come si muove il Re negli scacchi?

Rispondi citando le pagine di riferimento.
```

---

## 3. Cosine Similarity - Ricerca Semantica

### 3.1 Cos'è la Cosine Similarity

La **cosine similarity** (similarità del coseno) misura la somiglianza tra due vettori calcolando il coseno dell'angolo tra di essi.

**Formula matematica**:
```
cos(θ) = (A · B) / (||A|| × ||B||)

dove:
- A · B = prodotto scalare dei vettori
- ||A|| = norma (lunghezza) del vettore A
- ||B|| = norma del vettore B
```

**Range valori**: -1 a 1 (per vettori normalizzati: 0 a 1)
- **1.0** = vettori identici (stessa direzione)
- **0.0** = vettori ortogonali (nessuna relazione)
- **-1.0** = vettori opposti

### 3.2 Dove si usa nel sistema

**Qdrant Vector Database** usa cosine similarity come metrica di default:

```csharp
// File: KnowledgeBase/Infrastructure/Indexing/OptimizedVectorIndexService.cs
// ADR-016 Phase 3 Specifications

var collectionConfig = new CollectionConfig
{
    VectorDimensions = 3072,                    // text-embedding-3-large
    CollectionName = "boardgame_rules",
    DistanceMetric = DistanceMetric.Cosine,     // COSINE SIMILARITY
    HnswConfig = new HnswConfiguration
    {
        M = 16,               // Max connections per node
        EfConstruct = 200     // Construction parameter
    }
};
```

### 3.3 Come funziona la ricerca

1. **Query** → Embedding (3072 dimensioni)
2. **Qdrant** cerca i vettori più simili usando HNSW (Hierarchical Navigable Small World)
3. **Score** ritornato = cosine similarity (0-1)

```csharp
// File: Services/Qdrant/QdrantVectorSearcher.cs
var searchResult = await _qdrantService.SearchAsync(
    gameId,
    queryEmbedding,      // [float; 3072]
    language,
    limit: topK,         // es. 5
    documentIds: null,
    cancellationToken
);

// Risultato: List<SearchResultItem> ordinati per Score (descending)
// Score = cosine similarity tra queryEmbedding e chunk embedding
```

---

## 4. tsvector - Full-Text Search PostgreSQL

### 4.1 Cos'è tsvector

**tsvector** è un tipo di dato PostgreSQL ottimizzato per la ricerca full-text. Rappresenta un documento come una lista di **lexemi** (parole normalizzate) con le loro posizioni.

**Esempio**:
```sql
SELECT to_tsvector('italian', 'Il Re si muove di una casella');
-- Risultato: 'casel':6 'muov':4 're':2
-- (articoli rimossi, parole ridotte alla radice)
```

### 4.2 Dove si trova nel sistema

**File**: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs`

```csharp
public class TextChunkEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid PdfDocumentId { get; set; }
    public string Content { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public int? PageNumber { get; set; }
    public int CharacterCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // PostgreSQL full-text search vector
    // Aggiornato automaticamente dal trigger tsvector_update_text_chunks
    [Column("search_vector")]
    public string? SearchVector { get; set; }
}
```

### 4.3 Query SQL per Keyword Search

**File**: `apps/api/src/Api/Services/KeywordSearchService.cs`

```sql
SELECT
    "Id",
    "Content",
    "PdfDocumentId",
    "GameId",
    "ChunkIndex",
    "PageNumber",
    ts_rank_cd(search_vector, to_tsquery(@textSearchConfig, @tsQuery), @normalization) AS "RelevanceScore"
FROM text_chunks
WHERE
    "GameId" = @gameId::uuid
    AND search_vector @@ to_tsquery(@textSearchConfig, @tsQuery)
ORDER BY "RelevanceScore" DESC
LIMIT @limit
```

**Operatori chiave**:
- `@@` = match operator (il vettore contiene i termini della query?)
- `to_tsquery()` = converte la query in formato tsquery
- `ts_rank_cd()` = calcola il punteggio di rilevanza (cover density ranking)

### 4.4 Configurazioni FTS supportate

```csharp
// File: KeywordSearchService.cs
private static readonly Dictionary<string, string> LanguageToFtsConfig = new()
{
    { "it", "italian" },
    { "italian", "italian" },
    { "en", "english" },
    { "english", "english" }
};
```

### 4.5 Costruzione tsquery

```csharp
// Esempi di conversione query → tsquery:

// Simple AND (default):
"castling rules" → "castling & rules"

// Phrase search (con virgolette):
"\"en passant\"" → "en <-> passant"  // Proximity operator

// Con boost terms:
"check" + boostTerms=["check","checkmate"]
→ "check:A | checkmate:A"  // :A = highest weight
```

---

## 5. LlmService - Generazione Risposte

### 5.1 Architettura Hybrid

**File**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmService.cs`

Il `HybridLlmService` coordina più provider LLM con routing adattivo:

```csharp
internal class HybridLlmService : ILlmService
{
    private readonly List<ILlmClient> _clients;           // Ollama, OpenRouter
    private readonly ILlmRoutingStrategy _routingStrategy;
    private readonly ILlmCostLogRepository _costLogRepository;
    private readonly IProviderHealthCheckService _healthCheckService;

    // Circuit breaker per ogni provider
    private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers;
    private readonly Dictionary<string, LatencyStats> _latencyStats;
}
```

### 5.2 Provider Supportati

| Provider | File | Modelli |
|----------|------|---------|
| **Ollama** | `Services/LlmClients/OllamaLlmClient.cs` | Llama 3.3 70B (free), nomic-embed-text |
| **OpenRouter** | `Services/LlmClients/OpenRouterLlmClient.cs` | GPT-4o-mini, Claude Sonnet, Opus |

### 5.3 Routing Strategy

**File**: `KnowledgeBase/Domain/Services/LlmManagement/HybridAdaptiveRoutingStrategy.cs`

```csharp
// ISSUE-958: Routing basato su:
// 1. User tier (Anonymous → User → Editor → Admin)
// 2. RAG Strategy (Fast → Balanced → Precise)
// 3. Provider health (circuit breaker status)

public RoutingDecision SelectProvider(User? user, RagStrategy strategy)
{
    // User tier determina l'ACCESSO (quali strategie può usare)
    // Strategy determina il MODELLO (quale LLM usare)

    // Esempio routing:
    // User + Fast → Llama 3.3 Free (Ollama)
    // Editor + Balanced → GPT-4o-mini (OpenRouter)
    // Admin + Precise → Claude Opus (OpenRouter)
}
```

### 5.4 Interfaccia ILlmService

**File**: `apps/api/src/Api/Services/ILlmService.cs`

```csharp
internal interface ILlmService
{
    // Completion standard
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);

    // Streaming (CHAT-01)
    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default);

    // JSON structured output (CHAT-02)
    Task<T?> GenerateJsonAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default) where T : class;
}
```

### 5.5 Risultato LLM

```csharp
internal record LlmCompletionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string Response { get; init; } = string.Empty;
    public LlmUsage Usage { get; init; } = LlmUsage.Empty;  // Token counts
    public LlmCost Cost { get; init; } = LlmCost.Empty;     // $ cost
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}

internal record LlmUsage(
    int PromptTokens,      // Token nel prompt (context + query)
    int CompletionTokens,  // Token nella risposta
    int TotalTokens        // Totale
);

internal record LlmCost
{
    public decimal InputCost { get; init; }   // $ per input tokens
    public decimal OutputCost { get; init; }  // $ per output tokens
    public decimal TotalCost => InputCost + OutputCost;
    public string ModelId { get; init; }      // es. "gpt-4o-mini"
    public string Provider { get; init; }     // es. "OpenRouter"
}
```

---

## 6. Servizi Interni (Docker Infrastructure)

### 6.1 Mappa Completa Servizi

| Servizio | Container | Porta | Scopo |
|----------|-----------|-------|-------|
| **PostgreSQL** | meepleai-postgres | 5432 | Database principale + FTS (tsvector) |
| **Qdrant** | meepleai-qdrant | 6333/6334 | Vector database (embeddings) |
| **Redis** | meepleai-redis | 6379 | Cache (AI-05), session state |
| **Ollama** | meepleai-ollama | 11434 | LLM locale (Llama 3.3 70B) |
| **Embedding Service** | meepleai-embedding | 8000 | Generazione embeddings |
| **Unstructured** | meepleai-unstructured | 8001 | PDF extraction Stage 1 |
| **SmolDocling** | meepleai-smoldocling | 8002 | PDF extraction Stage 2 (VLM) |
| **Reranker** | meepleai-reranker | 8003 | Cross-encoder reranking |
| **Orchestrator** | meepleai-orchestrator | 8004 | LangGraph multi-agent |
| **Prometheus** | meepleai-prometheus | 9090 | Metrics collection |
| **Alertmanager** | meepleai-alertmanager | 9093 | Alert routing |

### 6.2 SmolDocling Service

**File**: `apps/smoldocling-service/src/main.py`

**Scopo**: Estrazione PDF con Vision Language Model per layout complessi (Stage 2 fallback).

```yaml
# docker-compose.yml
smoldocling-service:
  container_name: meepleai-smoldocling
  ports:
    - "8002:8002"
  environment:
    - DEVICE=cpu  # o "cuda" con GPU
    - MODEL_NAME=docling-project/SmolDocling-256M-preview
    - MAX_NEW_TOKENS=2048
    - IMAGE_DPI=300
    - QUALITY_THRESHOLD=0.70  # Soglia qualità minima
```

**Quando si usa**: Quando Unstructured (Stage 1) fallisce o restituisce qualità < 0.80.

### 6.3 Unstructured Service

**File**: `apps/unstructured-service/`

**Scopo**: Estrazione PDF primaria (Stage 1) con analisi semantica.

```yaml
unstructured-service:
  container_name: meepleai-unstructured
  ports:
    - "8001:8001"
  environment:
    - UNSTRUCTURED_STRATEGY=fast
    - LANGUAGE=ita
    - CHUNK_MAX_CHARACTERS=2000
    - CHUNK_OVERLAP=200
    - QUALITY_THRESHOLD=0.80  # Soglia per passare a Stage 2
```

**Pipeline 3-Stage (ADR-003)**:
```
Stage 1: Unstructured (Quality ≥ 0.80) → Success ~80%
    ↓ fallback
Stage 2: SmolDocling (Quality ≥ 0.70) → Success ~15%
    ↓ fallback
Stage 3: Docnet (Best Effort) → Success ~5%
```

### 6.4 Embedding Service

**File**: `apps/embedding-service/`

**Scopo**: Generazione embeddings multilingua (AI-09).

```yaml
embedding-service:
  container_name: meepleai-embedding
  ports:
    - "8000:8000"
  # Supporta GPU per performance migliori
```

**Modello default**: `text-embedding-3-large` (3072 dimensioni)

### 6.5 Reranker Service

**File**: `apps/reranker-service/`

**Scopo**: Cross-encoder reranking per migliorare precisione retrieval (ADR-016 Phase 4).

```yaml
reranker-service:
  container_name: meepleai-reranker
  ports:
    - "8003:8003"
  environment:
    - MODEL_NAME=BAAI/bge-reranker-v2-m3
    - BATCH_SIZE=32
```

**Come funziona**: Dopo il retrieval iniziale, il reranker ri-ordina i risultati usando un cross-encoder che valuta la coppia (query, document) insieme.

### 6.6 Orchestration Service (LangGraph)

**File**: `apps/orchestration-service/src/main.py`

**Scopo**: Coordinamento multi-agent con LangGraph (ISSUE-3495).

```yaml
orchestration-service:
  container_name: meepleai-orchestrator
  ports:
    - "8004:8004"
  environment:
    - EMBEDDING_SERVICE_URL=http://embedding-service:8000
    - RERANKER_SERVICE_URL=http://reranker-service:8003
    - LANGGRAPH_TIMEOUT=30
    - MAX_WORKFLOW_DEPTH=10
```

**Agenti supportati**: Tutor, Arbitro, Decisore

### 6.7 Monitoring (cAdvisor, Prometheus)

**cAdvisor** monitora le risorse dei container Docker:

```yaml
# In prometheus.yml
scrape_configs:
  - job_name: 'cadvisor'
    static_configs:
      - targets: ['cadvisor:8080']
```

**Metriche raccolte**:
- CPU usage per container
- Memory usage per container
- Network I/O
- Disk I/O

**Prometheus** raccoglie metriche custom dall'API:
- `workflow_executions_total` - Esecuzioni RAG
- `workflow_failures_total` - Fallimenti
- `workflow_duration_ms_avg` - Latenza media

---

## 7. Hybrid Search - Fusione RRF

### 7.1 Reciprocal Rank Fusion

**File**: `apps/api/src/Api/Services/HybridSearchService.cs`

```csharp
// RRF constant k (standard value from research: Cormack et al. 2009)
private const int DefaultRrfK = 60;

// Formula: RRF_score = sum(1 / (k + rank_i))
// Dove rank_i è la posizione del documento nel ranking i-esimo
```

**Esempio calcolo**:
```
Documento X:
- Vector search rank: 2
- Keyword search rank: 5

RRF_score = (1/(60+2)) × 0.7 + (1/(60+5)) × 0.3
          = 0.0113 × 0.7 + 0.0154 × 0.3
          = 0.0079 + 0.0046
          = 0.0125
```

### 7.2 Pesi Default

```csharp
// HybridSearchService.SearchAsync()
float vectorWeight = 0.7f,   // 70% peso semantic
float keywordWeight = 0.3f   // 30% peso keyword
```

---

## 8. Flow Diagram Completo

```
┌─────────────────────────────────────────────────────────────────┐
│                         USER QUERY                              │
│                    "Come si muove il Re?"                       │
└──────────────────────────┬──────────────────────────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     QUERY VALIDATION              │
         │     QueryValidator.ValidateQuery() │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     CACHE CHECK (AI-05)           │
         │     IAiResponseCacheService       │
         │     Key: {gameId}:{query}:lang:it │
         └─────────────────┬─────────────────┘
                           │ cache miss
         ┌─────────────────▼─────────────────────┐
         │     QUERY EXPANSION (PERF-08)         │
         │     IQueryExpansionService            │
         │     ["movimento re", "re muove",      │
         │      "spostamento re scacchi"]        │
         └─────────────────┬─────────────────────┘
                           │
         ┌─────────────────▼─────────────────────┐
         │     EMBEDDING GENERATION              │
         │     IEmbeddingService                 │
         │     → text-embedding-3-large          │
         │     → vector[3072] per ogni variante  │
         └─────────────────┬─────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  │                  ▼
┌───────────────┐          │         ┌───────────────┐
│ VECTOR SEARCH │          │         │KEYWORD SEARCH │
│   (Qdrant)    │          │         │ (PostgreSQL)  │
│ cosine simil. │          │         │   tsvector    │
│ HNSW index    │          │         │  ts_rank_cd   │
└───────┬───────┘          │         └───────┬───────┘
        │                  │                  │
        └──────────────────┼──────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     RRF FUSION (k=60)             │
         │     ISearchResultReranker         │
         │     vector_weight=0.7             │
         │     keyword_weight=0.3            │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     CONTEXT ASSEMBLY              │
         │     Join chunks con separatori    │
         │     "[Page X]\n{content}"         │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     PROMPT ENGINEERING            │
         │     IPromptTemplateService        │
         │     1. ClassifyQuestion(query)    │
         │     2. GetTemplateAsync(gameId)   │
         │     3. RenderSystemPrompt()       │
         │     4. RenderUserPrompt(context)  │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     LLM GENERATION                │
         │     HybridLlmService              │
         │     → Routing: User tier + Strategy│
         │     → Provider: Ollama/OpenRouter │
         │     → Circuit breaker check       │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     RESPONSE ASSEMBLY             │
         │     QaResponse {                  │
         │       answer: string,             │
         │       snippets: Snippet[],        │
         │       promptTokens: int,          │
         │       completionTokens: int,      │
         │       confidence: double?         │
         │     }                             │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     CACHE STORE + METRICS         │
         │     _cache.SetAsync(key, 86400)   │
         │     MeepleAiMetrics.RecordRag()   │
         └─────────────────┬─────────────────┘
                           │
         ┌─────────────────▼─────────────────┐
         │     RESPONSE TO USER              │
         └───────────────────────────────────┘
```

---

## 9. File Reference

| Componente | File Path |
|------------|-----------|
| RagService | `apps/api/src/Api/Services/RagService.cs` |
| IRagService | `apps/api/src/Api/Services/IRagService.cs` |
| HybridSearchService | `apps/api/src/Api/Services/HybridSearchService.cs` |
| KeywordSearchService | `apps/api/src/Api/Services/KeywordSearchService.cs` |
| HybridLlmService | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmService.cs` |
| ILlmService | `apps/api/src/Api/Services/ILlmService.cs` |
| OpenRouterLlmClient | `apps/api/src/Api/Services/LlmClients/OpenRouterLlmClient.cs` |
| OllamaLlmClient | `apps/api/src/Api/Services/LlmClients/OllamaLlmClient.cs` |
| EmbeddingService | `apps/api/src/Api/Services/EmbeddingService.cs` |
| QdrantVectorSearcher | `apps/api/src/Api/Services/Qdrant/QdrantVectorSearcher.cs` |
| TextChunkEntity | `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs` |
| PromptTemplateService | `apps/api/src/Api/Services/IPromptTemplateService.cs` |
| QueryExpansionService | `apps/api/src/Api/Services/Rag/IQueryExpansionService.cs` |
| Docker Compose | `infra/docker-compose.yml` |

---

**Last Updated**: 2026-02-04
**Version**: 1.0
**Based on**: Code analysis from `backend-dev` branch

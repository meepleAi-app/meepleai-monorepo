# Sistema RAG - Diagrammi Dettagliati

## Architettura RAG Completa (Hybrid Search)

```mermaid
graph TB
    subgraph "Client Layer"
        Client[Client Request<br/>POST /api/v1/chat]
    end

    subgraph "Application Layer - RAG Service"
        RagService[RagService]
        Cache[Response Cache<br/>HybridCache<br/>TTL: 24h]
    end

    subgraph "Query Enhancement"
        QueryExp[QueryExpansionService<br/>Max 4 variations]
        Template[PromptTemplateService<br/>Question classification]
    end

    subgraph "Retrieval Layer - Hybrid Search"
        Hybrid[HybridSearchService<br/>Mode: Vector/Keyword/Hybrid]

        subgraph "Vector Path"
            Embedding[EmbeddingService<br/>Ollama: nomic-embed-text]
            Qdrant[QdrantService<br/>Cosine similarity]
        end

        subgraph "Keyword Path"
            Keyword[KeywordSearchService<br/>PostgreSQL FTS]
        end

        RRF[RrfFusionDomainService<br/>k=60, weights: 0.7/0.3]
    end

    subgraph "Generation Layer - Multi-Provider LLM"
        LLM[HybridLlmService]

        subgraph "Provider Selection"
            Routing[LlmRoutingStrategy<br/>User-tier based]
            CircuitBreaker[Circuit Breaker<br/>5 failures → OPEN]
            Health[ProviderHealthCheck<br/>Latency + Status]
        end

        subgraph "LLM Providers"
            Ollama[Ollama Local<br/>Llama 3.3 70B/8B]
            OpenRouter[OpenRouter<br/>GPT-4 + Claude]
        end
    end

    subgraph "Validation Layer - 5-Layer Quality Assurance"
        V1[1. Confidence Validation<br/>threshold >= 0.70]
        V2[2. Citation Validation<br/>PDF references]
        V3[3. Hallucination Detection<br/>Keyword-based, 5 langs]
        V4[4. Quality Tracking<br/>P@10, MRR]
        V5[5. Response Quality<br/>Overall assessment]
    end

    subgraph "Observability"
        Logging[Serilog → Seq<br/>Structured logs]
        Tracing[OpenTelemetry → Jaeger<br/>Distributed tracing]
        Metrics[Prometheus<br/>Latency, tokens, cost]
    end

    Client -->|Query + GameId| RagService

    RagService -->|Check cache| Cache
    Cache -.->|Hit| Client

    RagService -->|Miss| QueryExp
    QueryExp -->|Variations| Embedding

    Embedding -->|Vectors| Qdrant
    QueryExp -->|Text queries| Keyword

    Qdrant -->|Vector results| RRF
    Keyword -->|Keyword results| RRF
    RRF -->|Fused top-K| Template

    Template -->|System + User prompt| LLM

    LLM --> Routing
    Routing --> CircuitBreaker
    CircuitBreaker --> Health

    Health -->|Route| Ollama
    Health -->|Route| OpenRouter

    Ollama -->|Completion| V1
    OpenRouter -->|Completion| V1

    V1 --> V2
    V2 --> V3
    V3 --> V4
    V4 --> V5

    V5 -->|Valid response| RagService
    RagService -->|Store| Cache
    RagService -->|Response| Client

    RagService -.-> Logging
    RagService -.-> Tracing
    RagService -.-> Metrics

    style Client fill:#e1f5ff
    style RagService fill:#4fc3f7
    style Hybrid fill:#29b6f6
    style LLM fill:#ab47bc
    style V1 fill:#66bb6a
    style V2 fill:#81c784
    style V3 fill:#a5d6a7
    style V4 fill:#c8e6c9
    style V5 fill:#dcedc8
```

## RRF Fusion Algorithm (Reciprocal Rank Fusion)

```mermaid
flowchart TD
    Start([Start Hybrid Search]) --> VectorSearch[Vector Search<br/>Qdrant cosine similarity<br/>Limit: 2x TopK]
    Start --> KeywordSearch[Keyword Search<br/>PostgreSQL ts_rank_cd<br/>Limit: 2x TopK]

    VectorSearch --> VectorResults[Vector Results<br/>Ranked 1..N]
    KeywordSearch --> KeywordResults[Keyword Results<br/>Ranked 1..N]

    VectorResults --> RRF[RRF Algorithm]
    KeywordResults --> RRF

    RRF --> InitDict[Initialize score dictionary<br/>key: chunkId, value: 0.0]

    InitDict --> LoopVector{For each<br/>vector result}
    LoopVector -->|Next| CalcVector[score += vectorWeight / (k + rank)<br/>weight=0.7, k=60]
    CalcVector --> LoopVector
    LoopVector -->|Done| LoopKeyword

    LoopKeyword{For each<br/>keyword result} -->|Next| CalcKeyword[score += keywordWeight / (k + rank)<br/>weight=0.3, k=60]
    CalcKeyword --> LoopKeyword
    LoopKeyword -->|Done| Normalize

    Normalize[Normalize scores<br/>score = min(score * 30, 1.0)] --> Sort[Sort by hybrid score DESC]

    Sort --> TopN[Take top N results<br/>N = TopK from config]

    TopN --> Return([Return<br/>HybridSearchResult[]])

    style Start fill:#4fc3f7
    style RRF fill:#ffa726
    style Normalize fill:#66bb6a
    style Return fill:#ab47bc
```

### RRF Formula Breakdown

```
Per ogni documento:

  RRF_score = Σ (weight / (k + rank))

  Dove:
  - k = 60 (costante empirica ottimale)
  - rank = posizione 1-based nella lista
  - weight = vectorWeight (0.7) o keywordWeight (0.3)

  Esempio:
  Documento appare a:
  - rank 1 in vector search
  - rank 3 in keyword search

  RRF_score = 0.7/(60+1) + 0.3/(60+3)
            = 0.7/61 + 0.3/63
            = 0.01148 + 0.00476
            = 0.01624

  Normalizzazione:
  final_score = min(0.01624 * 30, 1.0) = 0.487
```

## Query Expansion Strategy (PERF-08)

```mermaid
flowchart LR
    Original[Original Query<br/>"How do I setup the game?"]

    Original --> Analyze[Analyze Query]

    Analyze --> Synonyms[Synonym Expansion<br/>Rule-based dictionary]
    Analyze --> Reformulate[Question Reformulation<br/>Remove prefixes]

    Synonyms --> V1["Variation 1:<br/>'game setup'"]
    Synonyms --> V2["Variation 2:<br/>'initial setup'"]

    Reformulate --> V3["Variation 3:<br/>'setup rules'"]
    Reformulate --> V4["Variation 4:<br/>'starting position'"]

    V1 --> Limit{Max variations<br/>reached?<br/>Config: 4}
    V2 --> Limit
    V3 --> Limit
    V4 --> Limit

    Limit -->|Yes| Output[["[original, v1, v2, v3, v4]"]]
    Limit -->|No| More[Generate more]
    More --> Limit

    Output --> Embedding[Parallel Embedding<br/>Generation]

    style Original fill:#4fc3f7
    style Limit fill:#ffa726
    style Output fill:#66bb6a
```

**Impact**: 15-25% recall improvement (PERF-08)

## LLM Provider Routing Strategy

```mermaid
flowchart TD
    Request[LLM Request] --> CheckUser{Check User Role}

    CheckUser -->|Anonymous/User| FreeRoute[Free Tier Routing]
    CheckUser -->|Editor| MixedRoute[Mixed Routing]
    CheckUser -->|Admin| PremiumRoute[Premium Routing]

    FreeRoute --> Free80[80% Llama 3.3 70B<br/>OpenRouter free tier]
    FreeRoute --> Free20[20% GPT-4o-mini<br/>Paid fallback]

    MixedRoute --> Mixed50A[50% Llama 3.3 70B]
    MixedRoute --> Mixed50B[50% GPT-4o-mini]

    PremiumRoute --> Prem20[20% Llama 3.3 8B<br/>Local Ollama]
    PremiumRoute --> Prem80[80% Claude-3.5-Haiku<br/>Premium quality]

    Free80 --> CircuitCheck{Circuit Breaker<br/>State?}
    Free20 --> CircuitCheck
    Mixed50A --> CircuitCheck
    Mixed50B --> CircuitCheck
    Prem20 --> CircuitCheck
    Prem80 --> CircuitCheck

    CircuitCheck -->|CLOSED| HealthCheck{Health Check<br/>OK?}
    CircuitCheck -->|OPEN| Fallback[Try Fallback Provider]
    CircuitCheck -->|HALF_OPEN| TestRequest[Test Request]

    TestRequest -->|Success| CloseCB[Circuit → CLOSED]
    TestRequest -->|Failure| KeepOpen[Circuit → OPEN]

    HealthCheck -->|OK| SendRequest[Send Request]
    HealthCheck -->|Degraded| Fallback

    SendRequest --> TrackMetrics[Track Metrics:<br/>- Latency P50/P95/P99<br/>- Token usage<br/>- Cost<br/>- Error rate]

    TrackMetrics --> Success{Success?}

    Success -->|Yes| RecordSuccess[Record Success<br/>Update health]
    Success -->|No| RecordFailure[Record Failure<br/>Increment CB counter]

    RecordFailure --> CheckThreshold{Failures >= 5?}
    CheckThreshold -->|Yes| OpenCB[Circuit → OPEN<br/>30s timeout]
    CheckThreshold -->|No| Retry[Retry with<br/>exponential backoff]

    RecordSuccess --> Return[Return LlmCompletionResult]
    Retry --> Return

    style Request fill:#4fc3f7
    style CircuitCheck fill:#ffa726
    style TrackMetrics fill:#66bb6a
    style Return fill:#ab47bc
```

## Circuit Breaker State Machine

```mermaid
stateDiagram-v2
    [*] --> CLOSED: Initial state

    CLOSED --> OPEN: 5 consecutive failures
    CLOSED --> CLOSED: Success (counter = 0)

    OPEN --> HALF_OPEN: After 30s timeout
    OPEN --> OPEN: Request → Fast fail

    HALF_OPEN --> CLOSED: Test request success
    HALF_OPEN --> OPEN: Test request failure

    note right of CLOSED
        Normal operation
        All requests pass through
        Track failure count
    end note

    note right of OPEN
        Provider unavailable
        Fast fail all requests
        Start 30s recovery timer
    end note

    note right of HALF_OPEN
        Testing recovery
        Allow 1 test request
        Decide next state
    end note
```

## 5-Layer Validation Pipeline

```mermaid
flowchart TD
    Response[LLM Response] --> Layer1

    subgraph "Layer 1: Confidence Validation"
        Layer1[Calculate Confidence<br/>70% search + 30% LLM]
        Layer1 --> Check1{Confidence<br/>>= 0.70?}
        Check1 -->|Yes| Pass1[✓ PASS]
        Check1 -->|0.60-0.70| Warn1[⚠ WARNING]
        Check1 -->|< 0.60| Fail1[✗ CRITICAL]
    end

    Pass1 --> Layer2
    Warn1 --> Layer2
    Fail1 --> Layer2

    subgraph "Layer 2: Citation Validation"
        Layer2[Parse Citations<br/>PDF:guid format]
        Layer2 --> Check2{All citations<br/>valid?}
        Check2 -->|Yes| Pass2[✓ Valid sources]
        Check2 -->|No| Fail2[✗ Invalid citations]
    end

    Pass2 --> Layer3
    Fail2 --> Layer3

    subgraph "Layer 3: Hallucination Detection"
        Layer3[Check Forbidden Keywords<br/>5 languages support]
        Layer3 --> Check3{Keywords<br/>found?}
        Check3 -->|None| Pass3[✓ No hallucination]
        Check3 -->|1-2| Warn3[⚠ Low severity]
        Check3 -->|3-4| Warn3b[⚠ Medium severity]
        Check3 -->|5+| Fail3[✗ High severity]
    end

    Pass3 --> Layer4
    Warn3 --> Layer4
    Warn3b --> Layer4
    Fail3 --> Layer4

    subgraph "Layer 4: Quality Tracking"
        Layer4[Calculate Quality Metrics]
        Layer4 --> Metrics[P@10: Precision at 10<br/>MRR: Mean Reciprocal Rank<br/>Search confidence weighted]
        Metrics --> Pass4[✓ Metrics recorded]
    end

    Pass4 --> Layer5

    subgraph "Layer 5: Response Quality"
        Layer5[Overall Quality Assessment]
        Layer5 --> Check5{Overall<br/>quality?}
        Check5 -->|High| PassFinal[✓ High Quality<br/>confidence >= 0.8]
        Check5 -->|Medium| WarnFinal[⚠ Medium Quality<br/>0.5 <= confidence < 0.8]
        Check5 -->|Low| FailFinal[✗ Low Quality<br/>confidence < 0.5]
    end

    PassFinal --> Accept[Accept Response]
    WarnFinal --> Flag[Flag for Review]
    FailFinal --> Reject[Reject or Retry]

    Accept --> Return([Return to User])
    Flag --> Return
    Reject --> Return

    style Layer1 fill:#e3f2fd
    style Layer2 fill:#f3e5f5
    style Layer3 fill:#fff3e0
    style Layer4 fill:#e8f5e9
    style Layer5 fill:#fce4ec
    style Accept fill:#66bb6a
    style Reject fill:#ef5350
```

## Hallucination Detection - Multilingual Support

```mermaid
graph TB
    Text[Response Text] --> DetectLang{Detect or Use<br/>Specified Language}

    DetectLang -->|Italian| IT[Italian Keywords]
    DetectLang -->|English| EN[English Keywords]
    DetectLang -->|German| DE[German Keywords]
    DetectLang -->|French| FR[French Keywords]
    DetectLang -->|Spanish| ES[Spanish Keywords]

    subgraph "Italian Keywords"
        IT --> ITU[Uncertainty:<br/>'non lo so', 'non sono sicuro']
        IT --> ITA[Admission:<br/>'non specificato', 'non posso dire']
        IT --> ITH[Hedging:<br/>'potrebbe essere', 'probabilmente']
    end

    subgraph "English Keywords"
        EN --> ENU[Uncertainty:<br/>'don't know', 'not sure']
        EN --> ENA[Admission:<br/>'not specified', 'cannot say']
        EN --> ENH[Hedging:<br/>'might be', 'probably']
    end

    subgraph "Critical Phrases (All Languages)"
        Critical[Critical Phrases]
        Critical --> C1['cannot find']
        Critical --> C2['don't know']
        Critical --> C3['non riesco']
        Critical --> C4['no puedo']
    end

    ITU --> Count[Count Occurrences]
    ITA --> Count
    ITH --> Count
    ENU --> Count
    ENA --> Count
    ENH --> Count

    Count --> CheckCritical{Contains<br/>critical phrase?}
    CheckCritical -->|Yes| High[Severity: HIGH]
    CheckCritical -->|No| CheckCount{Keyword<br/>count?}

    CheckCount -->|0| None[Severity: NONE]
    CheckCount -->|1-2| Low[Severity: LOW]
    CheckCount -->|3-4| Medium[Severity: MEDIUM]
    CheckCount -->|5+| High

    style IT fill:#81c784
    style EN fill:#64b5f6
    style DE fill:#ffb74d
    style FR fill:#e57373
    style ES fill:#9575cd
    style High fill:#ef5350
    style None fill:#66bb6a
```

## Cost Tracking Architecture

```mermaid
flowchart TD
    Request[LLM Request] --> PreLog[Pre-Request Log]

    PreLog --> Record1[Record:<br/>- UserId<br/>- Endpoint<br/>- Provider<br/>- Model<br/>- Timestamp]

    Record1 --> Send[Send to LLM]

    Send --> Response[LLM Response]

    Response --> Extract[Extract Token Usage]

    Extract --> Calculate[Calculate Cost]

    subgraph "Cost Calculation"
        Calculate --> Pricing[Load Model Pricing<br/>from LlmModelPricing table]
        Pricing --> Formula[Cost = <br/>promptTokens * inputCostPerToken +<br/>completionTokens * outputCostPerToken]
    end

    Formula --> PostLog[Post-Request Log]

    PostLog --> Record2[Record:<br/>- Prompt tokens<br/>- Completion tokens<br/>- Total cost<br/>- Latency ms<br/>- Success/Error]

    Record2 --> Store[(LlmCostLog table<br/>PostgreSQL)]

    Store --> Aggregate[Aggregate Metrics]

    Aggregate --> PerUser[Per-User Cost]
    Aggregate --> PerProvider[Per-Provider Usage]
    Aggregate --> PerEndpoint[Per-Endpoint Stats]

    PerUser --> Alert{Cost threshold<br/>exceeded?}
    Alert -->|Yes| Notify[Send Alert<br/>Email/Slack/PagerDuty]
    Alert -->|No| Monitor[Continue Monitoring]

    style Calculate fill:#ffa726
    style Store fill:#66bb6a
    style Alert fill:#ef5350
```

## Quality Metrics Dashboard

```mermaid
graph TB
    subgraph "Search Quality Metrics"
        PAT10[Precision @ 10<br/>Top 10 relevance]
        MRR[Mean Reciprocal Rank<br/>First relevant position]
        Recall[Recall<br/>Retrieved / Total relevant]
    end

    subgraph "LLM Quality Metrics"
        Confidence[Confidence Score<br/>0.0 - 1.0]
        Tokens[Token Efficiency<br/>Tokens per response]
        Latency[Response Latency<br/>P50, P95, P99]
    end

    subgraph "Validation Metrics"
        HallucinationRate[Hallucination Rate<br/>Target: < 3%]
        CitationAccuracy[Citation Accuracy<br/>Valid sources %]
        QualityPass[Quality Pass Rate<br/>>= 0.70 threshold]
    end

    subgraph "Cost Metrics"
        CostPerQuery[Cost per Query<br/>USD]
        DailyCost[Daily Total Cost<br/>Per user/provider]
        FreeVsPaid[Free vs Paid<br/>Traffic split]
    end

    PAT10 --> Aggregate[Quality Score<br/>Aggregation]
    MRR --> Aggregate
    Recall --> Aggregate
    Confidence --> Aggregate

    Aggregate --> SLA{SLA<br/>Met?}
    SLA -->|Yes| Green[✓ System Healthy<br/>>95% accuracy]
    SLA -->|No| Red[✗ Action Required<br/>Review system]

    HallucinationRate --> Target{< 3%?}
    Target -->|Yes| GoodQuality[Good Quality]
    Target -->|No| Investigation[Investigate<br/>prompt templates]

    style PAT10 fill:#81c784
    style Confidence fill:#64b5f6
    style HallucinationRate fill:#ffb74d
    style CostPerQuery fill:#e57373
    style Green fill:#66bb6a
    style Red fill:#ef5350
```

---

## Performance Targets

| Metrica | Target | Attuale | Status |
|---------|--------|---------|--------|
| **Retrieval Latency** | < 1s | ~800ms | ✓ |
| **Generation Latency** | < 3s | 2-5s | ⚠ |
| **Total E2E Latency** | < 5s | 3-6s | ✓ |
| **Accuracy** | > 95% | ~93% | ⚠ |
| **Hallucination Rate** | < 3% | ~2.5% | ✓ |
| **P@10** | > 0.8 | ~0.75 | ⚠ |
| **MRR** | > 0.7 | ~0.68 | ⚠ |
| **Cache Hit Rate** | > 40% | ~35% | ⚠ |
| **Cost per Query** | < $0.01 | $0.008 | ✓ |

## Configuration (Dynamic via SystemConfiguration)

```json
{
  "RAG": {
    "TopK": 5,
    "MinScore": 0.7,
    "RrfK": 60,
    "MaxQueryVariations": 4
  },
  "HybridSearch": {
    "VectorWeight": 0.7,
    "KeywordWeight": 0.3,
    "RrfConstant": 60
  },
  "LlmRouting": {
    "AnonymousModel": "llama-3.3-70b",
    "UserModel": "llama-3.3-70b",
    "EditorModel": "llama3:8b",
    "AdminModel": "llama3:8b",
    "OpenRouterAnonymousPercent": 20,
    "OpenRouterUserPercent": 20,
    "OpenRouterEditorPercent": 50,
    "OpenRouterAdminPercent": 80
  },
  "Validation": {
    "MinimumConfidence": 0.70,
    "WarningConfidence": 0.60,
    "CriticalConfidence": 0.50
  }
}
```

---

**Versione**: 1.0
**Data**: 2025-11-13
**Sistema**: RAG Hybrid Search with Multi-Provider LLM

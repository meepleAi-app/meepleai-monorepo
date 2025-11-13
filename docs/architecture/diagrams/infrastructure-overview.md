# Diagramma Infrastruttura MeepleAI

## Infrastruttura Docker Compose

```mermaid
graph TB
    subgraph "Client Layer"
        Browser[Browser<br/>:3000]
    end

    subgraph "Application Layer"
        Web[Next.js Web App<br/>:3000<br/>React 19 + Tailwind]
        API[ASP.NET Core API<br/>:8080<br/>.NET 9]
    end

    subgraph "Data Layer"
        PG[(PostgreSQL<br/>:5432<br/>Game + User Data)]
        Qdrant[(Qdrant<br/>:6333<br/>Vector Store)]
        Redis[(Redis<br/>:6379<br/>Cache + Sessions)]
    end

    subgraph "AI Services"
        Unstructured[Unstructured<br/>:8001<br/>PDF Extraction]
        SmolDocling[SmolDocling<br/>:8002<br/>VLM PDF Processing]
        Ollama[Ollama<br/>:11434<br/>Local LLMs]
        OpenRouter[OpenRouter API<br/>Cloud LLMs]
    end

    subgraph "Workflow Layer"
        N8N[n8n<br/>:5678<br/>Workflow Automation]
    end

    subgraph "Observability Layer"
        Seq[Seq<br/>:8081<br/>Logging]
        Jaeger[Jaeger<br/>:16686<br/>Tracing]
        Prometheus[Prometheus<br/>:9090<br/>Metrics]
        Grafana[Grafana<br/>:3001<br/>Dashboards]
    end

    Browser -->|HTTP/HTTPS| Web
    Web -->|REST API<br/>Cookie + API Key Auth| API

    API -->|EF Core<br/>Connection Pool| PG
    API -->|Vector Search| Qdrant
    API -->|HybridCache L1+L2| Redis

    API -->|HTTP Client<br/>Stage 1 Extraction| Unstructured
    API -->|HTTP Client<br/>Stage 2 Extraction| SmolDocling
    API -->|HTTP Client<br/>Embedding + LLM| Ollama
    API -->|HTTP Client<br/>Multi-Model LLM| OpenRouter

    API -->|Webhook Triggers| N8N
    N8N -->|Workflow Events| API

    API -->|Serilog| Seq
    API -->|OpenTelemetry| Jaeger
    API -->|Metrics Endpoint| Prometheus
    Prometheus -->|Data Source| Grafana

    style Browser fill:#e1f5ff
    style Web fill:#4fc3f7
    style API fill:#29b6f6
    style PG fill:#66bb6a
    style Qdrant fill:#ab47bc
    style Redis fill:#ef5350
    style Unstructured fill:#ffa726
    style SmolDocling fill:#ff7043
    style Ollama fill:#8d6e63
    style OpenRouter fill:#78909c
    style N8N fill:#9575cd
    style Seq fill:#ffb74d
    style Jaeger fill:#64b5f6
    style Prometheus fill:#81c784
    style Grafana fill:#ff8a65
```

## Porte e Servizi

| Servizio | Porta | Tecnologia | Scopo |
|----------|-------|------------|-------|
| **Web App** | 3000 | Next.js 16 + React 19 | Frontend UI |
| **API** | 8080 | ASP.NET Core 9 | Backend REST API |
| **PostgreSQL** | 5432 | PostgreSQL 16 | Database principale |
| **Qdrant** | 6333 | Qdrant | Vector database per RAG |
| **Redis** | 6379 | Redis 7 | Cache L2 + Sessions |
| **Unstructured** | 8001 | Python FastAPI | PDF extraction Stage 1 |
| **SmolDocling** | 8002 | Python + VLM | PDF extraction Stage 2 |
| **Ollama** | 11434 | Ollama | LLM locale + Embeddings |
| **n8n** | 5678 | n8n | Workflow automation |
| **Seq** | 8081 | Seq | Centralized logging |
| **Jaeger** | 16686 | Jaeger | Distributed tracing |
| **Prometheus** | 9090 | Prometheus | Metrics collection |
| **Grafana** | 3001 | Grafana | Monitoring dashboards |

## Flusso Dati Principali

### 1. Autenticazione (Dual Auth)
```
Browser → Web → API
         ↓
    Cookie Session (httpOnly, secure)
         OR
    API Key (mpl_{env}_{base64})
         ↓
    Validation → Redis (Session Store)
                 PostgreSQL (User + ApiKey)
```

### 2. Upload e Processing PDF
```
Browser → Web → API → PostgreSQL (PdfDocument entity)
                 ↓
            Stage 1: Unstructured Service (:8001)
                 ↓ (if quality < 0.80)
            Stage 2: SmolDocling Service (:8002)
                 ↓ (if quality < 0.70)
            Stage 3: Docnet (local library)
                 ↓
            Text Extraction Complete
                 ↓
            Chunking → Ollama (Embedding)
                 ↓
            Qdrant (Vector Indexing)
```

### 3. RAG Query (Hybrid Search)
```
Browser → Web → API
         ↓
    Query Expansion (4 variations)
         ↓
    Parallel Search:
    ├─ Qdrant (Vector Search)
    └─ PostgreSQL (Keyword FTS)
         ↓
    RRF Fusion (70/30 weights)
         ↓
    LLM Generation:
    ├─ Ollama (free tier 80%)
    └─ OpenRouter (paid 20%)
         ↓
    5-Layer Validation
         ↓
    Response + Citations
```

## Connessioni Osservabilità

```mermaid
graph LR
    API[API Service]

    API -->|Structured Logs<br/>Serilog| Seq[Seq]
    API -->|W3C Trace Context<br/>OpenTelemetry| Jaeger[Jaeger]
    API -->|Metrics Endpoint<br/>/metrics| Prometheus[Prometheus]
    Prometheus -->|Data Source| Grafana[Grafana]

    API -->|Health Checks<br/>/health| Monitor[Health Monitoring]

    style API fill:#29b6f6
    style Seq fill:#ffb74d
    style Jaeger fill:#64b5f6
    style Prometheus fill:#81c784
    style Grafana fill:#ff8a65
    style Monitor fill:#4db6ac
```

## Stack Tecnologico Completo

### Backend
- **Runtime**: .NET 9 (ASP.NET Core)
- **Architecture**: DDD + CQRS (MediatR)
- **ORM**: Entity Framework Core 9
- **Database**: PostgreSQL 16
- **Cache**: Redis 7 + HybridCache
- **Vector DB**: Qdrant

### Frontend
- **Framework**: Next.js 16 (App Router)
- **UI Library**: React 19
- **Components**: Shadcn/UI (Radix + Tailwind CSS 4)
- **State**: React Context + Hooks
- **API Client**: Fetch with cookie auth

### AI/ML
- **LLM Providers**: OpenRouter (GPT-4, Claude), Ollama (Llama 3.3)
- **Embeddings**: Ollama (nomic-embed-text 384D)
- **PDF Processing**: Unstructured, SmolDocling VLM, Docnet
- **OCR**: Tesseract

### DevOps
- **Container**: Docker + Docker Compose
- **CI/CD**: GitHub Actions
- **Logging**: Serilog → Seq
- **Tracing**: OpenTelemetry → Jaeger
- **Metrics**: Prometheus + Grafana
- **Security**: CodeQL SAST, Dependabot

---

**Versione**: 1.0
**Data**: 2025-11-13
**Autore**: Claude Code Analysis

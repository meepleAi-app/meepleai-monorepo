# MeepleAI Architecture Diagrams

Diagrammi di architettura del sistema MeepleAI con Mermaid.

## Architettura di Sistema

```mermaid
graph TB
    subgraph "Client Layer"
        Browser[Browser]
        CLI[CLI/API Client]
    end

    subgraph "Frontend - Next.js"
        Web[Next.js App<br/>Port 3000]
        Pages[Pages<br/>chat, upload, admin]
        APIClient[API Client<br/>lib/api.ts]
    end

    subgraph "Backend - ASP.NET Core"
        API[ASP.NET Core API<br/>Port 8080]

        subgraph "Authentication"
            AuthMiddleware[Auth Middleware]
            ApiKeyAuth[API Key Auth]
            CookieAuth[Cookie Auth]
        end

        subgraph "Core Services"
            RagService[RAG Service]
            LlmService[LLM Service]
            PdfService[PDF Processing]
            GameService[Game Service]
            StreamingQA[Streaming QA]
        end

        subgraph "Infrastructure Services"
            EmbedService[Embedding Service]
            QdrantService[Qdrant Service]
            CacheService[Cache Service]
            AuditService[Audit Service]
        end
    end

    subgraph "Data Layer"
        Postgres[(PostgreSQL<br/>Port 5432)]
        Qdrant[(Qdrant Vector DB<br/>Port 6333)]
        Redis[(Redis Cache<br/>Port 6379)]
    end

    subgraph "External Services"
        OpenRouter[OpenRouter API<br/>LLM Provider]
        N8N[n8n Workflows<br/>Port 5678]
    end

    subgraph "Observability"
        Seq[Seq Logs<br/>Port 8081]
        Jaeger[Jaeger Tracing<br/>Port 16686]
        Prometheus[Prometheus<br/>Port 9090]
        Grafana[Grafana<br/>Port 3001]
    end

    Browser --> Web
    CLI --> API
    Web --> APIClient
    APIClient --> API

    API --> AuthMiddleware
    AuthMiddleware --> ApiKeyAuth
    AuthMiddleware --> CookieAuth

    API --> RagService
    API --> LlmService
    API --> PdfService
    API --> GameService
    API --> StreamingQA

    RagService --> EmbedService
    RagService --> QdrantService
    StreamingQA --> RagService
    StreamingQA --> CacheService

    EmbedService --> OpenRouter
    LlmService --> OpenRouter

    PdfService --> Postgres
    GameService --> Postgres
    QdrantService --> Qdrant
    CacheService --> Redis
    AuditService --> Postgres

    API --> N8N

    API --> Seq
    API --> Jaeger
    API --> Prometheus
    Prometheus --> Grafana

    classDef frontend fill:#61dafb,stroke:#333,stroke-width:2px,color:#000
    classDef backend fill:#512bd4,stroke:#333,stroke-width:2px,color:#fff
    classDef database fill:#336791,stroke:#333,stroke-width:2px,color:#fff
    classDef external fill:#ff6b6b,stroke:#333,stroke-width:2px,color:#fff
    classDef observability fill:#f59e0b,stroke:#333,stroke-width:2px,color:#000

    class Web,Pages,APIClient frontend
    class API,AuthMiddleware,ApiKeyAuth,CookieAuth,RagService,LlmService,PdfService,GameService,StreamingQA,EmbedService,QdrantService,CacheService,AuditService backend
    class Postgres,Qdrant,Redis database
    class OpenRouter,N8N external
    class Seq,Jaeger,Prometheus,Grafana observability
```

## Dual Authentication Flow

```mermaid
sequenceDiagram
    participant Client
    participant Middleware as Auth Middleware
    participant ApiKeyAuth as API Key Service
    participant CookieAuth as Cookie Auth Service
    participant DB as Database
    participant Endpoint as Protected Endpoint

    Client->>Middleware: HTTP Request

    alt Has X-API-Key Header
        Middleware->>ApiKeyAuth: ValidateApiKeyAsync(apiKey)
        ApiKeyAuth->>DB: Query api_keys table
        DB-->>ApiKeyAuth: API Key data
        ApiKeyAuth->>ApiKeyAuth: Verify PBKDF2 hash
        ApiKeyAuth->>ApiKeyAuth: Check expiration, status

        alt Valid API Key
            ApiKeyAuth-->>Middleware: ClaimsPrincipal (user + scopes)
            Middleware->>Endpoint: Set HttpContext.User
            Endpoint-->>Client: 200 OK + Response
        else Invalid API Key
            ApiKeyAuth-->>Middleware: null
            Middleware-->>Client: 401 Unauthorized
        end

    else Has Session Cookie
        Middleware->>CookieAuth: ValidateSessionAsync(cookie)
        CookieAuth->>DB: Query user_sessions table
        DB-->>CookieAuth: Session data
        CookieAuth->>CookieAuth: Validate expiration

        alt Valid Session
            CookieAuth-->>Middleware: ClaimsPrincipal (user + role)
            Middleware->>Endpoint: Set HttpContext.User
            Endpoint-->>Client: 200 OK + Response
        else Invalid Session
            CookieAuth-->>Middleware: null
            Middleware-->>Client: 401 Unauthorized
        end

    else No Auth
        Middleware-->>Client: 401 Unauthorized
    end
```

## RAG/Vector Pipeline

```mermaid
graph LR
    subgraph "PDF Upload"
        Upload[PDF Upload]
        Validation[PDF Validation]
        Storage[PDF Storage]
    end

    subgraph "Text Processing"
        Extraction[Text Extraction<br/>Docnet.Core]
        Chunking[Text Chunking<br/>512 chars, 50 overlap]
    end

    subgraph "Embedding"
        EmbedAPI[Embedding Service]
        OpenRouter[OpenRouter API<br/>text-embedding-3-small]
    end

    subgraph "Vector Storage"
        Qdrant[Qdrant Vector DB]
        Collection[Game Collection]
    end

    subgraph "RAG Query"
        Query[User Query]
        RagSearch[RAG Search]
        Retrieval[Top-K Retrieval]
        LLM[LLM Generation]
        Response[AI Response]
    end

    Upload --> Validation
    Validation --> Storage
    Storage --> Extraction
    Extraction --> Chunking
    Chunking --> EmbedAPI
    EmbedAPI --> OpenRouter
    OpenRouter --> Qdrant
    Qdrant --> Collection

    Query --> RagSearch
    RagSearch --> Retrieval
    Retrieval --> Collection
    Collection --> LLM
    LLM --> Response

    classDef upload fill:#3b82f6,stroke:#333,stroke-width:2px,color:#fff
    classDef process fill:#8b5cf6,stroke:#333,stroke-width:2px,color:#fff
    classDef vector fill:#10b981,stroke:#333,stroke-width:2px,color:#fff
    classDef query fill:#f59e0b,stroke:#333,stroke-width:2px,color:#000

    class Upload,Validation,Storage upload
    class Extraction,Chunking,EmbedAPI,OpenRouter process
    class Qdrant,Collection vector
    class Query,RagSearch,Retrieval,LLM,Response query
```

## Streaming SSE Flow (CHAT-01)

```mermaid
sequenceDiagram
    participant Client as Frontend
    participant API as /api/v1/agents/qa/stream
    participant StreamingQA as StreamingQA Service
    participant Cache as Redis Cache
    participant RAG as RAG Service
    participant LLM as LLM Service
    participant DB as PostgreSQL

    Client->>API: POST (gameId, query, chatId?)
    API->>API: Set SSE headers
    API->>StreamingQA: AskStreamAsync()

    alt Cache Hit
        StreamingQA->>Cache: Check cache
        Cache-->>StreamingQA: Cached response
        StreamingQA-->>API: event: state (Retrieving from cache)
        API-->>Client: SSE: state
        StreamingQA-->>API: event: citations
        API-->>Client: SSE: citations

        loop Simulate Streaming
            StreamingQA-->>API: event: token
            API-->>Client: SSE: token (word-by-word)
        end

        StreamingQA-->>API: event: complete
        API-->>Client: SSE: complete

    else Cache Miss
        StreamingQA->>Cache: Miss
        StreamingQA-->>API: event: state (Searching rulebook)
        API-->>Client: SSE: state

        StreamingQA->>RAG: SearchAsync(gameId, query)
        RAG-->>StreamingQA: Snippets + Citations

        StreamingQA-->>API: event: citations
        API-->>Client: SSE: citations

        StreamingQA-->>API: event: state (Generating answer)
        API-->>Client: SSE: state

        StreamingQA->>LLM: GenerateCompletionStreamAsync()

        loop Stream Tokens
            LLM-->>StreamingQA: token
            StreamingQA-->>API: event: token
            API-->>Client: SSE: token (real-time)
        end

        LLM-->>StreamingQA: Complete
        StreamingQA->>Cache: Store response
        StreamingQA-->>API: event: complete
        API-->>Client: SSE: complete
    end

    API->>DB: Save chat message
    API->>DB: Log AI request
    DB-->>API: Success
    API-->>Client: Close SSE stream

    Note over Client,DB: Event Types:<br/>- StateUpdate (status indicator)<br/>- Citations (rulebook refs)<br/>- Token (word/chunk)<br/>- Complete (metadata)<br/>- Error (if failure)
```

## PDF Processing Pipeline

```mermaid
graph TB
    subgraph "Client Upload"
        ClientValidation[Client-Side Validation<br/>MIME, Size, Magic Bytes]
        FileSelect[File Selection]
    end

    subgraph "Server Validation"
        ServerValidation[PDF Validation Service]
        MagicBytes[Magic Bytes Check]
        Structure[PDF Structure Check]
        PageCount[Page Count Validation]
        Version[PDF Version Check]
    end

    subgraph "Storage"
        BlobStorage[PDF Storage Service]
        FileSystem[File System<br/>/pdfs/]
        DBRecord[Database Record]
    end

    subgraph "Processing"
        TextExtraction[Text Extraction<br/>Docnet.Core]
        TableExtraction[Table Extraction<br/>iText7]
        Chunking[Text Chunking<br/>512 chars]
    end

    subgraph "Indexing"
        Embedding[Embedding Generation]
        VectorIndex[Qdrant Indexing]
    end

    FileSelect --> ClientValidation
    ClientValidation --> ServerValidation

    ServerValidation --> MagicBytes
    MagicBytes --> Structure
    Structure --> PageCount
    PageCount --> Version

    Version --> BlobStorage
    BlobStorage --> FileSystem
    BlobStorage --> DBRecord

    DBRecord --> TextExtraction
    TextExtraction --> TableExtraction
    TableExtraction --> Chunking

    Chunking --> Embedding
    Embedding --> VectorIndex

    classDef client fill:#3b82f6,stroke:#333,stroke-width:2px,color:#fff
    classDef validation fill:#ef4444,stroke:#333,stroke-width:2px,color:#fff
    classDef storage fill:#8b5cf6,stroke:#333,stroke-width:2px,color:#fff
    classDef process fill:#10b981,stroke:#333,stroke-width:2px,color:#fff
    classDef index fill:#f59e0b,stroke:#333,stroke-width:2px,color:#000

    class FileSelect,ClientValidation client
    class ServerValidation,MagicBytes,Structure,PageCount,Version validation
    class BlobStorage,FileSystem,DBRecord storage
    class TextExtraction,TableExtraction,Chunking process
    class Embedding,VectorIndex index
```

## Session Management (AUTH-03)

```mermaid
graph TB
    subgraph "Session Lifecycle"
        Login[User Login]
        CreateSession[Create Session]
        SessionStore[(user_sessions table)]
        ActiveSession[Active Session]

        Login --> CreateSession
        CreateSession --> SessionStore
        SessionStore --> ActiveSession
    end

    subgraph "Session Revocation"
        ManualRevoke[Manual Revocation<br/>Admin/User]
        PasswordChange[Password Change]
        AutoRevoke[Auto-Revocation Service]

        ManualRevoke --> RevokeSession
        PasswordChange --> RevokeAllSessions
        AutoRevoke --> RevokeInactive
    end

    subgraph "Auto-Revocation Background Service"
        Timer[Timer<br/>Every 1 hour]
        CheckInactive[Check Inactive Sessions<br/>> 30 days]
        BulkRevoke[Bulk Revoke]

        Timer --> CheckInactive
        CheckInactive --> BulkRevoke
        BulkRevoke --> SessionStore
    end

    subgraph "Session Validation"
        Request[HTTP Request]
        ValidateMiddleware[Auth Middleware]
        CheckSession[Validate Session]

        Request --> ValidateMiddleware
        ValidateMiddleware --> CheckSession
        CheckSession --> SessionStore
    end

    ActiveSession --> Request

    RevokeSession[Revoke Session] --> SessionStore
    RevokeAllSessions[Revoke All User Sessions] --> SessionStore
    RevokeInactive[Revoke Inactive] --> SessionStore

    classDef lifecycle fill:#3b82f6,stroke:#333,stroke-width:2px,color:#fff
    classDef revoke fill:#ef4444,stroke:#333,stroke-width:2px,color:#fff
    classDef auto fill:#f59e0b,stroke:#333,stroke-width:2px,color:#000
    classDef validate fill:#10b981,stroke:#333,stroke-width:2px,color:#fff

    class Login,CreateSession,SessionStore,ActiveSession lifecycle
    class ManualRevoke,PasswordChange,RevokeSession,RevokeAllSessions revoke
    class Timer,CheckInactive,BulkRevoke,AutoRevoke auto
    class Request,ValidateMiddleware,CheckSession validate
```

## Observability Stack (OPS-01, OPS-02)

```mermaid
graph TB
    subgraph "Application"
        API[ASP.NET Core API]
        Metrics[Metrics Collection]
        Traces[Trace Collection]
        Logs[Log Collection]
    end

    subgraph "Instrumentation"
        OTel[OpenTelemetry SDK]
        Serilog[Serilog]
        CustomMetrics[Custom Metrics<br/>MeepleAiMetrics.cs]
    end

    subgraph "Exporters"
        PrometheusExp[Prometheus Exporter<br/>/metrics]
        JaegerExp[OTLP Exporter<br/>Jaeger]
        SeqExp[Seq Sink]
    end

    subgraph "Backends"
        Prometheus[Prometheus<br/>Port 9090]
        Jaeger[Jaeger<br/>Port 16686]
        Seq[Seq<br/>Port 8081]
        Grafana[Grafana<br/>Port 3001]
    end

    subgraph "Dashboards"
        APIPerf[API Performance]
        AIRag[AI/RAG Operations]
        Infrastructure[Infrastructure]
    end

    API --> Metrics
    API --> Traces
    API --> Logs

    Metrics --> OTel
    Traces --> OTel
    Logs --> Serilog

    OTel --> CustomMetrics
    OTel --> PrometheusExp
    OTel --> JaegerExp
    Serilog --> SeqExp

    PrometheusExp --> Prometheus
    JaegerExp --> Jaeger
    SeqExp --> Seq

    Prometheus --> Grafana
    Grafana --> APIPerf
    Grafana --> AIRag
    Grafana --> Infrastructure

    classDef app fill:#512bd4,stroke:#333,stroke-width:2px,color:#fff
    classDef instrument fill:#8b5cf6,stroke:#333,stroke-width:2px,color:#fff
    classDef export fill:#3b82f6,stroke:#333,stroke-width:2px,color:#fff
    classDef backend fill:#10b981,stroke:#333,stroke-width:2px,color:#fff
    classDef dashboard fill:#f59e0b,stroke:#333,stroke-width:2px,color:#000

    class API,Metrics,Traces,Logs app
    class OTel,Serilog,CustomMetrics instrument
    class PrometheusExp,JaegerExp,SeqExp export
    class Prometheus,Jaeger,Seq,Grafana backend
    class APIPerf,AIRag,Infrastructure dashboard
```

## Legend

- **Blue**: Client/Frontend components
- **Purple**: Backend/API services
- **Green**: Data stores/Vector DB
- **Red**: External services
- **Orange/Yellow**: Observability/Monitoring

## References

- System Architecture: `docs/README.md`
- Authentication: `docs/SECURITY.md`
- RAG Pipeline: `docs/ai-06-rag-evaluation.md`
- Streaming: `docs/issue/chat-01-streaming-sse-implementation.md`
- PDF Processing: `docs/technic/pdf-processing-design.md`
- Session Management: AUTH-03 implementation
- Observability: `docs/observability.md`, `docs/technic/ops-02-opentelemetry-design.md`

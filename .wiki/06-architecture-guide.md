# Architecture Guide - MeepleAI

**Audience**: Technical leads, architects, and senior engineers.

## 📋 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Domain-Driven Design](#domain-driven-design)
3. [CQRS Pattern](#cqrs-pattern)
4. [Bounded Contexts](#bounded-contexts)
5. [Data Architecture](#data-architecture)
6. [RAG Pipeline](#rag-pipeline)
7. [Authentication Architecture](#authentication-architecture)
8. [PDF Processing Pipeline](#pdf-processing-pipeline)
9. [Caching Strategy](#caching-strategy)
10. [Observability Architecture](#observability-architecture)
11. [Design Decisions (ADRs)](#design-decisions-adrs)
12. [Scalability & Performance](#scalability--performance)

## 🏗️ Architecture Overview

### System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Client Layer                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Browser    │  │  Mobile App  │  │  API Client  │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
└─────────┼──────────────────┼──────────────────┼──────────────┘
          │                  │                  │
          │         HTTPS (TLS 1.3)            │
          │                  │                  │
┌─────────▼──────────────────▼──────────────────▼──────────────┐
│                   Load Balancer / CDN                         │
│              (Nginx / Cloudflare / AWS ALB)                   │
└─────────┬──────────────────┬──────────────────┬──────────────┘
          │                  │                  │
    ┌─────▼─────┐      ┌─────▼─────┐      ┌────▼────┐
    │    Web    │      │    Web    │      │   Web   │
    │  (Next.js)│      │  (Next.js)│      │ (Next.js)│
    │   :3000   │      │   :3000   │      │  :3000  │
    └─────┬─────┘      └─────┬─────┘      └────┬────┘
          │                  │                  │
          └──────────────────┼──────────────────┘
                             │
                   REST API (JSON)
                             │
┌────────────────────────────▼──────────────────────────────────┐
│                      API Layer                                │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐     │
│  │   API    │  │   API    │  │   API    │  │   API    │     │
│  │(ASP.NET) │  │(ASP.NET) │  │(ASP.NET) │  │(ASP.NET) │     │
│  │  :8080   │  │  :8080   │  │  :8080   │  │  :8080   │     │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘     │
└───────┼─────────────┼─────────────┼─────────────┼────────────┘
        │             │             │             │
        └─────────────┴─────────────┴─────────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
┌───────▼────────┐  ┌────────▼────────┐  ┌───────▼────────┐
│   PostgreSQL   │  │     Qdrant      │  │     Redis      │
│  (Primary DB)  │  │  (Vector DB)    │  │    (Cache)     │
│     :5432      │  │     :6333       │  │     :6379      │
└────────────────┘  └─────────────────┘  └────────────────┘
        │
        │
┌───────▼─────────────────────────────────────────────────────┐
│                    AI/ML Services                            │
│  ┌────────────┐  ┌─────────────┐  ┌──────────────┐         │
│  │ OpenRouter │  │   Ollama    │  │  Embedding   │         │
│  │   (LLM)    │  │ (Local LLM) │  │   Service    │         │
│  └────────────┘  └─────────────┘  └──────────────┘         │
│  ┌────────────┐  ┌─────────────┐                           │
│  │Unstructured│  │ SmolDocling │                           │
│  │(PDF Stage1)│  │(PDF Stage2) │                           │
│  └────────────┘  └─────────────┘                           │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│              Observability Stack                             │
│  ┌─────┐  ┌────────┐  ┌────────────┐  ┌────────┐          │
│  │ Seq │  │ Jaeger │  │ Prometheus │  │ Grafana│          │
│  │:8081│  │ :16686 │  │   :9090    │  │ :3001  │          │
│  └─────┘  └────────┘  └────────────┘  └────────┘          │
└─────────────────────────────────────────────────────────────┘
```

### Technology Stack

**Backend**:
- ASP.NET 9 (C# 13)
- Entity Framework Core 9
- MediatR (CQRS)
- Serilog (Logging)
- OpenTelemetry (Tracing)

**Frontend**:
- Next.js 16 (App Router)
- React 19
- TypeScript 5
- Shadcn/UI (Radix + Tailwind CSS 4)

**Data**:
- PostgreSQL 17 (Primary DB)
- Qdrant (Vector DB)
- Redis (Cache)

**AI/ML**:
- OpenRouter (LLM Proxy)
- Ollama (Local LLM)
- Unstructured (PDF Extraction)
- SmolDocling (VLM Fallback)

**Infrastructure**:
- Docker & Docker Compose
- Kubernetes (Production)
- Nginx (Reverse Proxy)
- n8n (Workflow Automation)

## 🎯 Domain-Driven Design

### DDD Principles

MeepleAI follows Domain-Driven Design with:
- **Ubiquitous Language**: Shared terminology across team
- **Bounded Contexts**: 7 isolated business domains
- **Aggregates**: Encapsulated business logic
- **Value Objects**: Immutable domain concepts
- **Domain Events**: Communication between contexts

### Strategic Design

**7 Bounded Contexts**:

```
┌─────────────────────────────────────────────────────────────┐
│                  MeepleAI Domain Model                       │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────┐      ┌──────────────────┐            │
│  │ Authentication   │◄────►│ GameManagement   │            │
│  │                  │      │                  │            │
│  │ • Users          │      │ • Games          │            │
│  │ • Sessions       │      │ • Play Sessions  │            │
│  │ • API Keys       │      └──────────────────┘            │
│  │ • OAuth          │                ▲                     │
│  │ • 2FA            │                │                     │
│  └──────────────────┘                │                     │
│          ▲                           │                     │
│          │                           │                     │
│  ┌───────┴──────────┐      ┌────────┴─────────┐           │
│  │ KnowledgeBase    │◄────►│ DocumentProcessing│           │
│  │                  │      │                  │            │
│  │ • RAG Pipeline   │      │ • PDF Upload     │            │
│  │ • Hybrid Search  │      │ • Extraction     │            │
│  │ • Chat Threads   │      │ • Quality Check  │            │
│  │ • Vectors        │      └──────────────────┘            │
│  └──────────────────┘                                      │
│          ▲                                                  │
│          │                                                  │
│  ┌───────┴──────────┐      ┌──────────────────┐           │
│  │SystemConfiguration│     │ Administration   │            │
│  │                  │      │                  │            │
│  │ • Config Store   │      │ • Users Mgmt     │            │
│  │ • Feature Flags  │      │ • Alerts         │            │
│  │ • Runtime Config │      │ • Audit          │            │
│  └──────────────────┘      │ • Analytics      │            │
│                            └──────────────────┘            │
│  ┌──────────────────┐                                      │
│  │WorkflowIntegration│                                     │
│  │                  │                                      │
│  │ • n8n Workflows  │                                      │
│  │ • Error Logging  │                                      │
│  └──────────────────┘                                      │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### Tactical Patterns

**Aggregates** (Example: Game):
```csharp
public class Game  // Aggregate Root
{
    public GameId Id { get; private set; }
    public GameTitle Title { get; private set; }  // Value Object
    public Publisher Publisher { get; private set; }  // Value Object
    private readonly List<RuleSpec> _ruleSpecs = new();
    public IReadOnlyCollection<RuleSpec> RuleSpecs => _ruleSpecs.AsReadOnly();

    // Domain methods enforce invariants
    public void UpdateTitle(GameTitle newTitle)
    {
        if (newTitle == null)
            throw new DomainException("Title cannot be null");

        Title = newTitle;
        AddDomainEvent(new GameTitleUpdatedEvent(Id, newTitle));
    }

    public void AddRuleSpec(RuleSpec ruleSpec)
    {
        if (_ruleSpecs.Any(r => r.Id == ruleSpec.Id))
            throw new DomainException("Rule spec already exists");

        _ruleSpecs.Add(ruleSpec);
        AddDomainEvent(new RuleSpecAddedEvent(Id, ruleSpec.Id));
    }
}
```

**Value Objects**:
```csharp
public class GameTitle : ValueObject
{
    public string Value { get; }

    public GameTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Title cannot be empty");

        if (value.Length > 200)
            throw new DomainException("Title too long");

        Value = value;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**Domain Services**:
```csharp
public class PdfQualityValidationDomainService
{
    public ValidationResult Validate(PdfExtractionResult result, QualityThresholds thresholds)
    {
        // Complex domain logic that doesn't fit in aggregates
        var quality = CalculateQuality(result);

        if (quality < thresholds.MinimumQuality)
            return ValidationResult.Failed($"Quality {quality} below threshold {thresholds.MinimumQuality}");

        return ValidationResult.Success(quality);
    }

    private double CalculateQuality(PdfExtractionResult result)
    {
        // 4-metric scoring
        var textCoverage = CalculateTextCoverage(result) * 0.4;
        var structureDetection = CalculateStructureDetection(result) * 0.2;
        var tableDetection = CalculateTableDetection(result) * 0.2;
        var pageCoverage = CalculatePageCoverage(result) * 0.2;

        return textCoverage + structureDetection + tableDetection + pageCoverage;
    }
}
```

## ⚡ CQRS Pattern

### CQRS Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      HTTP Request                            │
└──────────────────────────┬──────────────────────────────────┘
                           │
                  ┌────────▼────────┐
                  │   Endpoint      │
                  │  (Minimal API)  │
                  └────────┬────────┘
                           │
                    IMediator.Send()
                           │
        ┌──────────────────┴──────────────────┐
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│  Command       │                  │     Query      │
│  (Write)       │                  │     (Read)     │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│ CommandHandler │                  │  QueryHandler  │
│ (MediatR)      │                  │   (MediatR)    │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│  Repository    │                  │  Read Model    │
│  (Write DB)    │                  │  (Optimized)   │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
        └──────────────────┬──────────────────┘
                           │
                  ┌────────▼────────┐
                  │   PostgreSQL    │
                  └─────────────────┘
```

### Implementation

**Command**:
```csharp
public record CreateGameCommand(string Title, string Publisher) : IRequest<Result<int>>;

public class CreateGameCommandHandler : IRequestHandler<CreateGameCommand, Result<int>>
{
    private readonly IGameRepository _repository;
    private readonly ILogger<CreateGameCommandHandler> _logger;

    public CreateGameCommandHandler(
        IGameRepository repository,
        ILogger<CreateGameCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<int>> Handle(CreateGameCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Creating game: {Title}", request.Title);

        // Create domain aggregate
        var game = new Game
        {
            Title = new GameTitle(request.Title),
            Publisher = new Publisher(request.Publisher)
        };

        // Persist
        await _repository.AddAsync(game, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation("Game created: {GameId}", game.Id);

        return Result.Success(game.Id);
    }
}
```

**Query**:
```csharp
public record GetGameQuery(int GameId) : IRequest<GameDto>;

public class GetGameQueryHandler : IRequestHandler<GetGameQuery, GameDto>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GetGameQueryHandler> _logger;

    public async Task<GameDto> Handle(GetGameQuery request, CancellationToken ct)
    {
        _logger.LogInformation("Getting game: {GameId}", request.GameId);

        // Read-optimized query
        var game = await _context.Games
            .AsNoTracking()  // Performance optimization
            .Where(g => g.Id == request.GameId)
            .Select(g => new GameDto
            {
                Id = g.Id,
                Title = g.Title,
                Publisher = g.Publisher
            })
            .FirstOrDefaultAsync(ct);

        return game ?? throw new NotFoundException($"Game {request.GameId} not found");
    }
}
```

**Endpoint**:
```csharp
// ALWAYS use IMediator, NEVER inject services directly
app.MapPost("/api/v1/games", async (CreateGameRequest request, IMediator mediator) =>
{
    var command = new CreateGameCommand(request.Title, request.Publisher);
    var result = await mediator.Send(command);

    return result.IsSuccess
        ? Results.Created($"/api/v1/games/{result.Value}", result.Value)
        : Results.BadRequest(result.Error);
});

app.MapGet("/api/v1/games/{id}", async (int id, IMediator mediator) =>
{
    var query = new GetGameQuery(id);
    var game = await mediator.Send(query);

    return game != null ? Results.Ok(game) : Results.NotFound();
});
```

## 🏢 Bounded Contexts

### Context Structure

Each bounded context follows this pattern:

```
BoundedContexts/{Context}/
├── Domain/                     # Pure domain logic
│   ├── Aggregates/             # Aggregate roots (Game, User, etc.)
│   ├── ValueObjects/           # Value objects (GameTitle, Email, etc.)
│   ├── DomainServices/         # Domain services
│   ├── Events/                 # Domain events
│   ├── Exceptions/             # Domain exceptions
│   └── Interfaces/             # Domain interfaces
├── Application/                # CQRS layer
│   ├── Commands/               # Write operations
│   │   ├── CreateGameCommand.cs
│   │   └── UpdateGameCommand.cs
│   ├── Queries/                # Read operations
│   │   ├── GetGameQuery.cs
│   │   └── ListGamesQuery.cs
│   └── Handlers/               # MediatR handlers
│       ├── CreateGameCommandHandler.cs
│       ├── UpdateGameCommandHandler.cs
│       ├── GetGameQueryHandler.cs
│       └── ListGamesQueryHandler.cs
└── Infrastructure/             # Adapters
    ├── Repositories/           # Data access
    ├── Services/               # External services
    └── Adapters/               # Third-party integrations
```

### Context Details

**1. Authentication** (`apps/api/src/Api/BoundedContexts/Authentication/`):
- User registration and login
- Session management
- API key generation and validation
- OAuth integration (Google, GitHub, Discord)
- 2FA (TOTP) support
- Password reset

**2. GameManagement** (`apps/api/src/Api/BoundedContexts/GameManagement/`):
- Games catalog (CRUD)
- Game metadata (title, publisher, complexity)
- Play session tracking
- Game search and filtering

**3. KnowledgeBase** (`apps/api/src/Api/BoundedContexts/KnowledgeBase/`):
- RAG pipeline orchestration
- Hybrid search (vector + keyword)
- Chat thread management
- Question answering
- Citation tracking
- Confidence scoring

**4. DocumentProcessing** (`apps/api/src/Api/BoundedContexts/DocumentProcessing/`):
- PDF upload and storage
- 3-stage extraction pipeline
- Quality validation
- Text chunking
- Vector embedding
- Indexing to Qdrant

**5. WorkflowIntegration** (`apps/api/src/Api/BoundedContexts/WorkflowIntegration/`):
- n8n workflow triggering
- Error logging
- Webhook handling
- Background job coordination

**6. SystemConfiguration** (`apps/api/src/Api/BoundedContexts/SystemConfiguration/`):
- Runtime configuration
- Feature flags
- Version control
- Rollback support
- Bulk operations

**7. Administration** (`apps/api/src/Api/BoundedContexts/Administration/`):
- User management
- Alert configuration
- Audit logging
- Analytics and reporting

## 🗄️ Data Architecture

### Database Schema

**PostgreSQL** (Primary Data Store):

```sql
-- Users & Authentication
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    last_login_at TIMESTAMP,
    is_active BOOLEAN DEFAULT true
);

CREATE TABLE sessions (
    id UUID PRIMARY KEY,
    user_id INT REFERENCES users(id),
    token VARCHAR(255) UNIQUE NOT NULL,
    ip_address VARCHAR(50),
    user_agent TEXT,
    created_at TIMESTAMP NOT NULL,
    expires_at TIMESTAMP NOT NULL
);

CREATE TABLE api_keys (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    key_hash VARCHAR(255) NOT NULL,
    name VARCHAR(100),
    created_at TIMESTAMP NOT NULL,
    last_used_at TIMESTAMP,
    is_active BOOLEAN DEFAULT true
);

CREATE TABLE oauth_accounts (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    provider VARCHAR(50) NOT NULL,
    provider_user_id VARCHAR(255) NOT NULL,
    access_token TEXT NOT NULL,
    refresh_token TEXT,
    expires_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL
);

-- Games
CREATE TABLE games (
    id SERIAL PRIMARY KEY,
    title VARCHAR(200) NOT NULL,
    publisher VARCHAR(200),
    year INT,
    min_players INT,
    max_players INT,
    min_playtime INT,
    max_playtime INT,
    complexity DECIMAL(3,2),
    bgg_id INT,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);

CREATE TABLE rule_specs (
    id SERIAL PRIMARY KEY,
    game_id INT REFERENCES games(id),
    content TEXT NOT NULL,
    page_number INT,
    section VARCHAR(200),
    created_at TIMESTAMP NOT NULL
);

-- Knowledge Base
CREATE TABLE chat_threads (
    id SERIAL PRIMARY KEY,
    user_id INT REFERENCES users(id),
    game_id INT REFERENCES games(id),
    title VARCHAR(200),
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);

CREATE TABLE chat_messages (
    id SERIAL PRIMARY KEY,
    thread_id INT REFERENCES chat_threads(id),
    role VARCHAR(20) NOT NULL,  -- 'user' or 'assistant'
    content TEXT NOT NULL,
    confidence DECIMAL(5,4),
    created_at TIMESTAMP NOT NULL
);

-- Documents
CREATE TABLE pdf_documents (
    id SERIAL PRIMARY KEY,
    game_id INT REFERENCES games(id),
    file_name VARCHAR(255) NOT NULL,
    file_size BIGINT,
    storage_path TEXT NOT NULL,
    quality_score DECIMAL(5,4),
    extraction_method VARCHAR(50),
    created_at TIMESTAMP NOT NULL
);

CREATE TABLE vector_documents (
    id UUID PRIMARY KEY,
    pdf_document_id INT REFERENCES pdf_documents(id),
    content TEXT NOT NULL,
    chunk_index INT,
    page_number INT,
    qdrant_point_id UUID,
    created_at TIMESTAMP NOT NULL
);

-- Configuration
CREATE TABLE configuration (
    id SERIAL PRIMARY KEY,
    key VARCHAR(200) UNIQUE NOT NULL,
    value TEXT NOT NULL,
    category VARCHAR(100),
    description TEXT,
    version INT DEFAULT 1,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);

-- Indexes for performance
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_sessions_token ON sessions(token);
CREATE INDEX idx_sessions_user_id ON sessions(user_id);
CREATE INDEX idx_api_keys_user_id ON api_keys(user_id);
CREATE INDEX idx_games_title ON games(title);
CREATE INDEX idx_rule_specs_game_id ON rule_specs(game_id);
CREATE INDEX idx_chat_threads_user_id ON chat_threads(user_id);
CREATE INDEX idx_chat_messages_thread_id ON chat_messages(thread_id);
CREATE INDEX idx_vector_documents_pdf_id ON vector_documents(pdf_document_id);

-- Full-text search
CREATE INDEX idx_rule_specs_content_fts ON rule_specs USING GIN(to_tsvector('english', content));
CREATE INDEX idx_games_title_trgm ON games USING GIN(title gin_trgm_ops);
```

### Qdrant (Vector Database)

**Collections**:
```python
{
  "collection_name": "game_rules",
  "vector_size": 1536,  # OpenAI embedding dimension
  "distance": "Cosine",
  "payload_schema": {
    "game_id": "int",
    "game_title": "string",
    "chunk_text": "string",
    "page_number": "int",
    "section": "string",
    "document_id": "uuid"
  }
}
```

### Redis (Cache)

**Key Patterns**:
```
# User sessions
session:{session_id} → {user_id, expires_at}

# API rate limiting
ratelimit:user:{user_id}:{minute} → count

# Cache for queries
cache:game:{game_id} → {game_json}
cache:search:{query_hash} → {results_json}

# RAG pipeline cache
rag:embedding:{text_hash} → {vector}
rag:search:{query_hash} → {results}
```

## 🔍 RAG Pipeline

### Hybrid RAG Architecture (ADR-001)

```
┌─────────────────────────────────────────────────────────────┐
│                    User Question                             │
└──────────────────────────┬──────────────────────────────────┘
                           │
                  ┌────────▼────────┐
                  │ Query Expansion │
                  │ (Synonyms, etc) │
                  └────────┬────────┘
                           │
        ┌──────────────────┴──────────────────┐
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│ Vector Search  │                  │ Keyword Search │
│ (Qdrant)       │                  │ (PostgreSQL)   │
│                │                  │  (Full-Text)   │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
        │  Top 10 results                    │  Top 10 results
        │  (semantic)                        │  (exact match)
        │                                     │
        └──────────────────┬──────────────────┘
                           │
                  ┌────────▼────────┐
                  │   RRF Fusion    │
                  │ (70% vec + 30% kw)│
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │ Re-ranking      │
                  │ (Confidence)    │
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │  Top 5 Chunks   │
                  └────────┬────────┘
                           │
        ┌──────────────────┴──────────────────┐
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│  GPT-4 Turbo   │                  │ Claude 3.5     │
│  (OpenRouter)  │                  │ Sonnet         │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
        │  Answer + Confidence               │  Answer + Confidence
        │                                     │
        └──────────────────┬──────────────────┘
                           │
                  ┌────────▼────────┐
                  │ Consensus       │
                  │ (Majority vote) │
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │ Validation      │
                  │ (5 layers)      │
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │ Final Answer    │
                  │ + Citations     │
                  │ + Confidence    │
                  └─────────────────┘
```

### RRF (Reciprocal Rank Fusion)

**Algorithm**:
```csharp
public List<SearchResult> FuseResults(
    List<SearchResult> vectorResults,
    List<SearchResult> keywordResults,
    double vectorWeight = 0.7,
    double keywordWeight = 0.3)
{
    const int k = 60;  // RRF constant

    var scores = new Dictionary<string, double>();

    // Score vector results
    for (int i = 0; i < vectorResults.Count; i++)
    {
        var doc = vectorResults[i];
        scores[doc.Id] = scores.GetValueOrDefault(doc.Id) +
            vectorWeight * (1.0 / (k + i + 1));
    }

    // Score keyword results
    for (int i = 0; i < keywordResults.Count; i++)
    {
        var doc = keywordResults[i];
        scores[doc.Id] = scores.GetValueOrDefault(doc.Id) +
            keywordWeight * (1.0 / (k + i + 1));
    }

    // Sort by fused score
    return scores
        .OrderByDescending(kv => kv.Value)
        .Select(kv => new SearchResult { Id = kv.Key, Score = kv.Value })
        .ToList();
}
```

### Validation Layers

**5-Layer Validation**:
1. **Confidence Threshold**: ≥0.70
2. **Citation Verification**: Answer references source
3. **Forbidden Keywords**: No "I think", "maybe", etc.
4. **Length Check**: Answer not empty, not too long
5. **Hallucination Detection**: Cross-check with sources

## 🔐 Authentication Architecture

### Auth Flow

```
┌─────────────────────────────────────────────────────────────┐
│                   Login Request                              │
│               POST /api/v1/auth/login                        │
│             {email, password, [totpCode]}                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
                  ┌────────▼────────┐
                  │ Validate        │
                  │ Credentials     │
                  └────────┬────────┘
                           │
                   ┌───────▼────────┐
                   │  2FA Enabled?  │
                   └───────┬────────┘
                      Yes  │  No
        ┌─────────────────┴───────────────────┐
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│ Verify TOTP    │                  │ Create Session │
│ Code           │                  │ Generate Token │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
  Valid │ Invalid                            │
        │                                     │
┌───────▼────────┐                           │
│ Create Session │                           │
│ Generate Token │                           │
└───────┬────────┘                           │
        │                                     │
        └──────────────────┬──────────────────┘
                           │
                  ┌────────▼────────┐
                  │  Set Cookie     │
                  │ (HttpOnly,      │
                  │  Secure,        │
                  │  SameSite)      │
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │ Return Success  │
                  └─────────────────┘
```

### Dual Authentication

**Cookie-based** (Web):
```csharp
// Set cookie on login
Response.Cookies.Append("meepleai-session", sessionToken, new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTimeOffset.UtcNow.AddDays(7)
});

// Validate on request
var sessionToken = Request.Cookies["meepleai-session"];
var session = await _sessionRepository.GetByTokenAsync(sessionToken);
```

**API Key** (Programmatic):
```csharp
// Generate key
var apiKey = $"mpl_{environment}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}";
var keyHash = HashApiKey(apiKey);  // PBKDF2, 210k iterations

// Validate on request
var authorization = Request.Headers["Authorization"].FirstOrDefault();
var apiKey = authorization?.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase) == true
    ? authorization.Substring("ApiKey ".Length)
    : null;
var keyHash = HashApiKey(apiKey);
var key = await _apiKeyRepository.GetByHashAsync(keyHash);
```

**Priority**: API Key > Cookie

## 📄 PDF Processing Pipeline

### 3-Stage Pipeline (ADR-003b)

```
┌─────────────────────────────────────────────────────────────┐
│                    PDF Upload                                │
└──────────────────────────┬──────────────────────────────────┘
                           │
                  ┌────────▼────────┐
                  │  Stage 1:       │
                  │  Unstructured   │
                  │  (Apache 2.0)   │
                  └────────┬────────┘
                           │
                   ┌───────▼────────┐
                   │ Quality ≥0.80? │
                   └───────┬────────┘
                      Yes  │  No
        ┌─────────────────┴───────────────────┐
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│ Success        │                  │  Stage 2:      │
│ (80% of PDFs)  │                  │  SmolDocling   │
│ ~1.3s avg      │                  │  (VLM 256M)    │
└───────┬────────┘                  └─────────┬──────┘
        │                                     │
        │                             ┌───────▼────────┐
        │                             │ Quality ≥0.70? │
        │                             └───────┬────────┘
        │                                Yes  │  No
        │              ┌───────────────────┐  │
        │              │                   │  │
        │      ┌───────▼────────┐  ┌───────▼──▼──────┐
        │      │ Success        │  │  Stage 3:       │
        │      │ (15% of PDFs)  │  │  Docnet         │
        │      │ ~3-5s avg      │  │  (Best effort)  │
        │      └───────┬────────┘  └─────────┬───────┘
        │              │                     │
        │              │             ┌───────▼────────┐
        │              │             │ Success        │
        │              │             │ (5% of PDFs)   │
        │              │             │ Fast           │
        │              │             └───────┬────────┘
        └──────────────┴─────────────────────┘
                           │
                  ┌────────▼────────┐
                  │ Text Chunking   │
                  │ (Sentences)     │
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │ Embedding       │
                  │ (OpenAI)        │
                  └────────┬────────┘
                           │
                  ┌────────▼────────┐
                  │ Index to Qdrant │
                  └─────────────────┘
```

### Quality Scoring

**4-Metric Quality Score**:
```csharp
public double CalculateQuality(PdfExtractionResult result)
{
    // 1. Text Coverage (40%) - chars per page ratio
    var textCoverage = Math.Min(result.TotalChars / (result.PageCount * 1500.0), 1.0) * 0.4;

    // 2. Structure Detection (20%) - titles, headers, lists
    var structureScore = (
        (result.TitlesDetected ? 0.4 : 0) +
        (result.HeadersDetected ? 0.3 : 0) +
        (result.ListsDetected ? 0.3 : 0)
    ) * 0.2;

    // 3. Table Detection (20%) - game rules often have tables
    var tableScore = Math.Min(result.TablesDetected / 5.0, 1.0) * 0.2;

    // 4. Page Coverage (20%) - all pages processed
    var pageCoverage = (result.PagesProcessed / (double)result.PageCount) * 0.2;

    return textCoverage + structureScore + tableScore + pageCoverage;
}
```

## 🗄️ Caching Strategy

### HybridCache (L1 + L2)

```
┌─────────────────────────────────────────────────────────────┐
│                    Cache Request                             │
└──────────────────────────┬──────────────────────────────────┘
                           │
                  ┌────────▼────────┐
                  │   L1 Cache      │
                  │ (In-Memory)     │
                  │ Fast (< 1ms)    │
                  └────────┬────────┘
                           │
                   ┌───────▼────────┐
                   │   Hit?         │
                   └───────┬────────┘
                      Yes  │  No
        ┌─────────────────┴───────────────────┐
        │                                     │
┌───────▼────────┐                  ┌─────────▼──────┐
│ Return Value   │                  │   L2 Cache     │
│                │                  │ (Redis)        │
└────────────────┘                  │ (5-10ms)       │
                                    └─────────┬──────┘
                                              │
                                      ┌───────▼────────┐
                                      │   Hit?         │
                                      └───────┬────────┘
                                         Yes  │  No
                       ┌──────────────────────┴────────┐
                       │                               │
               ┌───────▼────────┐          ┌───────────▼──────┐
               │ Warm L1        │          │ Query Database   │
               │ Return Value   │          │ (50-200ms)       │
               └────────────────┘          └───────────┬──────┘
                                                       │
                                           ┌───────────▼──────┐
                                           │ Cache in L2      │
                                           │ Cache in L1      │
                                           │ Return Value     │
                                           └──────────────────┘
```

**Configuration**:
```csharp
services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
    options.MaximumPayloadBytes = 1_048_576;  // 1MB
    options.MaximumKeyLength = 1024;
});
```

## 📊 Observability Architecture

### Three Pillars

**1. Logs (Seq)**:
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:8081")
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MeepleAI")
    .CreateLogger();

_logger.LogInformation(
    "User {UserId} asked question about {GameId}",
    userId,
    gameId);
```

**2. Traces (Jaeger)**:
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddJaegerExporter());
```

**3. Metrics (Prometheus)**:
```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());
```

### Correlation IDs

All logs, traces, and metrics are correlated:
```csharp
Activity.Current?.SetTag("correlation_id", correlationId);
_logger.LogInformation("Processing request {CorrelationId}", correlationId);
```

## 📐 Design Decisions (ADRs)

See `docs/01-architecture/adr/` for complete ADRs:

- **ADR-001**: Hybrid RAG (Vector + Keyword)
- **ADR-002**: CQRS with MediatR
- **ADR-003a**: PDF Processing - Unstructured
- **ADR-003b**: PDF Processing - 3-Stage Pipeline
- **CONFIG-01 to CONFIG-06**: Dynamic configuration
- **OPS-07**: Alerting system
- **PERF-05 to PERF-11**: Performance optimizations

## 📈 Scalability & Performance

### Horizontal Scaling

**Stateless API**: Can scale to N instances
**Load Balancing**: Round-robin or least connections
**Session Affinity**: Not required (sessions in Redis)

### Performance Optimizations

**Database**:
- AsNoTracking for read queries (30% faster)
- Connection pooling (10-100 connections)
- Indexes on common queries
- Full-text search indexes

**Caching**:
- HybridCache L1 + L2 (5min TTL)
- Redis for distributed cache
- 80%+ cache hit rate target

**Compression**:
- Brotli/Gzip (60-80% size reduction)
- Enabled for API responses

**RAG**:
- Sentence chunking (20% better recall)
- Query expansion (15-25% recall boost)
- RRF fusion (better than either alone)

### SLA Targets

| Metric | Target |
|--------|--------|
| **Uptime** | >99.5% |
| **P50 Response** | <500ms |
| **P95 Response** | <2s |
| **P99 Response** | <5s |
| **Throughput** | >100 req/s |

## 📚 Additional Resources

- **[Main Documentation](../docs/INDEX.md)** - Complete documentation (115+ docs)
- **[ADRs](../docs/01-architecture/adr/)** - Architecture Decision Records
- **[Developer Guide](./02-developer-guide.md)** - Development workflow
- **[Deployment Guide](./04-deployment-guide.md)** - Deployment procedures

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: Technical Leads & Architects

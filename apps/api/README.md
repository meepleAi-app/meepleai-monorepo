# MeepleAI API

**ASP.NET Core 9 Backend** - Domain-Driven Design (DDD) with CQRS/MediatR architecture for Italian board game rules assistant.

---

## 📁 Directory Structure

```
apps/api/
├── src/Api/                        # Main application source
│   ├── BoundedContexts/           # DDD Bounded Contexts (7 contexts)
│   │   ├── Authentication/        # Auth, sessions, API keys, OAuth, 2FA
│   │   ├── GameManagement/        # Games catalog, play sessions
│   │   ├── KnowledgeBase/         # RAG, vectors, chat (hybrid search)
│   │   ├── DocumentProcessing/    # PDF upload, extraction, validation
│   │   ├── WorkflowIntegration/   # n8n workflows, error logging
│   │   ├── SystemConfiguration/   # Runtime config, feature flags
│   │   └── Administration/        # Users, alerts, audit, analytics
│   ├── Infrastructure/            # EF Core DbContext, entities, configs
│   │   ├── Entities/             # Database entities
│   │   ├── EntityConfigurations/ # EF Core fluent API configs
│   │   └── Security/             # Security infrastructure
│   ├── Routing/                   # HTTP endpoints (MediatR-based)
│   ├── Services/                  # Legacy & infrastructure services
│   │   ├── Chat/                 # Chat orchestration
│   │   ├── LlmClients/           # OpenRouter, OpenAI, Claude clients
│   │   ├── Pdf/                  # PDF extraction services
│   │   ├── Qdrant/               # Vector DB client
│   │   └── Rag/                  # RAG orchestration
│   ├── SharedKernel/             # Cross-cutting DDD patterns
│   │   ├── Application/          # CQRS base classes
│   │   ├── Domain/               # Domain primitives, value objects
│   │   └── Infrastructure/       # Common infrastructure
│   ├── Middleware/               # ASP.NET middleware
│   ├── Migrations/               # EF Core migrations
│   ├── Observability/            # Logging, tracing, metrics
│   ├── Configuration/            # Startup configuration
│   ├── Data/                     # Seed data
│   └── Program.cs                # Application entry point
├── tests/Api.Tests/              # Test suite
│   ├── BoundedContexts/         # Context-specific tests
│   ├── Integration/             # Integration tests (Testcontainers)
│   └── Infrastructure/          # Infrastructure tests
├── GeneratePasswordHash.csx      # Password hash utility
├── MeepleAI.Api.sln             # Solution file
└── README.md                     # This file
```

---

## 🏗️ Architecture

### Domain-Driven Design (DDD) - 99% Complete

**7 Bounded Contexts** with CQRS/MediatR:

```
Authentication          → Auth, sessions, API keys, OAuth, 2FA
GameManagement         → Games catalog, play sessions
KnowledgeBase          → RAG, vectors, chat (Hybrid: vector+keyword RRF)
DocumentProcessing     → PDF upload, extraction, validation
WorkflowIntegration    → n8n workflows, error logging
SystemConfiguration    → Runtime config, feature flags
Administration         → Users, alerts, audit, analytics
```

**Pattern**: Domain (pure logic) → Application (CQRS) → Infrastructure (adapters) → HTTP (MediatR)

### Bounded Context Structure

Each bounded context follows this pattern:

```
BoundedContexts/{Context}/
├── Domain/                    # Pure domain logic (no dependencies)
│   ├── Aggregates/           # Domain aggregates (e.g., User, Game)
│   ├── ValueObjects/         # Immutable value objects
│   ├── DomainServices/       # Domain business logic
│   └── Events/               # Domain events
├── Application/               # Application layer (CQRS)
│   ├── Commands/             # Write operations
│   ├── Queries/              # Read operations
│   └── Handlers/             # MediatR request handlers
└── Infrastructure/            # Infrastructure adapters
    └── Repositories/         # Data access (EF Core)
```

**Example Flow**:
```
HTTP POST /api/v1/auth/login
    ↓
Routing/AuthenticationRoutes.cs
    ↓
IMediator.Send(LoginCommand)
    ↓
Authentication/Application/Handlers/LoginCommandHandler.cs
    ↓
Authentication/Domain/Services/AuthenticationDomainService.cs
    ↓
Response (session token)
```

---

## 🚀 Quick Start

### Prerequisites

- .NET 9 SDK
- Docker (for PostgreSQL, Qdrant, Redis)
- Visual Studio 2022 / VS Code / Rider

### Run Locally

```bash
# 1. Start infrastructure (from project root)
cd infra && docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis

# 2. Run API
cd apps/api/src/Api
dotnet run

# API available at: http://localhost:5080
# Swagger UI: http://localhost:5080/swagger
# Health check: http://localhost:5080/health
```

### Build

```bash
# Build solution
dotnet build

# Build specific project
cd src/Api && dotnet build

# Release build
dotnet build -c Release
```

### Test

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageThreshold=90

# Run specific test
dotnet test --filter "FullyQualifiedName~LoginCommandHandlerTests"
```

### Migrations

```bash
# Add migration
dotnet ef migrations add <MigrationName> --project src/Api

# Update database
dotnet ef database update --project src/Api

# Rollback migration
dotnet ef database update <PreviousMigrationName> --project src/Api

# Remove last migration
dotnet ef migrations remove --project src/Api
```

---

## 📦 Key Features

### Authentication (Dual + 2FA)

- **Cookie Auth**: Session-based (httpOnly, secure)
- **API Key Auth**: `mpl_{env}_{base64}` format, PBKDF2 (210k iterations)
- **OAuth**: Google/Discord/GitHub, token encryption (DataProtection)
- **2FA**: TOTP + backup codes, temporary sessions (5 min)

**Endpoints**:
```
POST   /api/v1/auth/register       → RegisterCommand
POST   /api/v1/auth/login          → LoginCommand (2FA support)
POST   /api/v1/auth/logout         → LogoutCommand
POST   /api/v1/auth/2fa/enable     → Enable2FACommand
GET    /api/v1/auth/apikeys        → GetApiKeysQuery
```

### RAG Pipeline (Hybrid Search - ADR-001)

- **Retrieval**: Vector (Qdrant) + Keyword (PG FTS) → RRF fusion (70/30)
- **Generation**: Multi-model (GPT-4 + Claude consensus)
- **Validation**: 5-layer (confidence ≥0.70, citation verify, forbidden keywords)
- **Quality**: P@10, MRR, confidence tracking, <3% hallucination target

**Endpoints**:
```
POST   /api/v1/chat                → AskQuestionCommand (streaming SSE)
GET    /api/v1/search              → SearchQuery (hybrid)
GET    /api/v1/chat/threads        → GetChatThreadsQuery
```

### PDF Processing Pipeline (ADR-003b)

**3-Stage Fallback Architecture**:
```
PDF Upload → EnhancedPdfProcessingOrchestrator
               ├─ Stage 1: Unstructured (≥0.80 quality) - 80% success
               ├─ Stage 2: SmolDocling VLM (≥0.70 quality) - 15% fallback
               └─ Stage 3: Docnet (best effort) - 5% fallback
                     ↓
            Quality Validation (4-metric scoring)
```

**Quality Metrics**:
- Text coverage: 40% (chars/page ratio)
- Structure detection: 20% (titles, headers, lists)
- Table detection: 20% (game rules tables)
- Page coverage: 20% (all pages processed)

**Endpoints**:
```
POST   /api/v1/documents/upload    → UploadPdfCommand
GET    /api/v1/documents           → GetDocumentsQuery
DELETE /api/v1/documents/{id}      → DeleteDocumentCommand
```

### Game Management

```
GET    /api/v1/games               → GetAllGamesQuery
POST   /api/v1/games               → CreateGameCommand
PUT    /api/v1/games/{id}          → UpdateGameCommand
DELETE /api/v1/games/{id}          → DeleteGameCommand
```

### System Configuration (CONFIG-01-06)

- **3-tier fallback**: DB → appsettings.json → defaults
- **Admin UI**: `/admin/configuration`
- **Categories**: Features, RateLimit, AI/LLM, RAG, PDF
- **Version control**: Rollback, bulk ops

```
GET    /api/v1/config              → GetConfigurationQuery
PUT    /api/v1/config              → UpdateConfigurationCommand
```

---

## 🔧 Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=meepleai;Username=meeple;Password=***"
  },
  "Qdrant": {
    "Url": "http://localhost:6333",
    "CollectionName": "board_game_rules",
    "VectorSize": 1024
  },
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "MeepleAI:"
  },
  "OpenRouter": {
    "ApiKey": "sk-or-***",
    "BaseUrl": "https://openrouter.ai/api/v1"
  },
  "PdfProcessing": {
    "Extractor": {
      "Provider": "Orchestrator"  // Use 3-stage pipeline
    },
    "Quality": {
      "MinimumThreshold": 0.80,
      "MinCharsPerPage": 500
    }
  }
}
```

### Environment Variables

```bash
# Required
OPENROUTER_API_KEY=sk-or-***
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=***

# Optional (override appsettings.json)
ConnectionStrings__Postgres=***
QDRANT_URL=http://localhost:6333
REDIS_URL=localhost:6379
SEQ_URL=http://localhost:8081
```

See `infra/env/.env.example` for complete list.

---

## 🧪 Testing

### Test Structure

```
tests/Api.Tests/
├── BoundedContexts/              # Context-specific tests
│   ├── Authentication/           # Auth tests (login, 2FA, OAuth)
│   ├── GameManagement/           # Game CRUD tests
│   ├── KnowledgeBase/            # RAG, chat tests
│   └── DocumentProcessing/       # PDF processing tests
├── Integration/                  # Integration tests (Testcontainers)
│   ├── PdfProcessing/           # End-to-end PDF pipeline
│   ├── RagPipeline/             # End-to-end RAG pipeline
│   └── Authentication/          # Auth integration tests
└── Infrastructure/               # Infrastructure tests
```

### Test Stack

- **xUnit**: Test framework
- **Moq**: Mocking framework
- **Testcontainers**: Docker containers for integration tests (Postgres, Qdrant)
- **FluentAssertions**: Assertion library

### Coverage Requirements

- **Overall**: 90%+ enforced
- **Domain**: 95%+ (pure logic, no excuses)
- **Application**: 90%+ (handlers)
- **Infrastructure**: 80%+ (repositories)

### Running Tests

```bash
# All tests
dotnet test

# Specific namespace
dotnet test --filter "FullyQualifiedName~Authentication"

# Integration tests only
dotnet test --filter "Category=Integration"

# Unit tests only
dotnet test --filter "Category!=Integration"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverageThreshold=90
```

---

## 🛠️ Development

### Adding a New Feature

**Example**: Add "Delete Game Session" feature

1. **Domain Logic** (`BoundedContexts/GameManagement/Domain/`)
   ```csharp
   // Domain service or aggregate method
   public class GameSessionAggregate
   {
       public void Delete(Guid userId)
       {
           // Business rules validation
           if (IsActive) throw new InvalidOperationException("Cannot delete active session");
           Status = SessionStatus.Deleted;
       }
   }
   ```

2. **Application Command** (`BoundedContexts/GameManagement/Application/Commands/`)
   ```csharp
   public record DeleteGameSessionCommand(Guid SessionId, Guid UserId)
       : IRequest<Result<Unit>>;
   ```

3. **Command Handler** (`BoundedContexts/GameManagement/Application/Handlers/`)
   ```csharp
   public class DeleteGameSessionCommandHandler
       : IRequestHandler<DeleteGameSessionCommand, Result<Unit>>
   {
       public async Task<Result<Unit>> Handle(DeleteGameSessionCommand request, CancellationToken ct)
       {
           var session = await _repository.GetByIdAsync(request.SessionId, ct);
           session.Delete(request.UserId);
           await _repository.SaveChangesAsync(ct);
           return Result.Success(Unit.Value);
       }
   }
   ```

4. **HTTP Endpoint** (`Routing/GameRoutes.cs`)
   ```csharp
   app.MapDelete("/api/v1/games/sessions/{id}", async (Guid id, IMediator mediator) =>
   {
       var result = await mediator.Send(new DeleteGameSessionCommand(id, userId));
       return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
   });
   ```

5. **Tests** (`tests/Api.Tests/BoundedContexts/GameManagement/`)
   ```csharp
   [Fact]
   public async Task DeleteGameSession_ValidRequest_ReturnsSuccess()
   {
       // Arrange, Act, Assert
   }
   ```

### Code Style

- **Nullable References**: Enabled (`<Nullable>enable</Nullable>`)
- **Async/Await**: Use `async Task` for all I/O operations
- **Dependency Injection**: Constructor injection, scoped/transient/singleton
- **Logging**: Use `ILogger<T>`, structured logging with Serilog
- **Error Handling**: Use `Result<T>` pattern, avoid exceptions for business logic

### Useful Scripts

```bash
# Generate password hash (for seed data)
dotnet script GeneratePasswordHash.csx "MyPassword123"

# Format code
dotnet format

# Analyze code
dotnet build /p:EnforceCodeStyleInBuild=true

# List vulnerable packages
dotnet list package --vulnerable
```

---

## 🔍 Observability

### Logging

- **Provider**: Serilog → Seq (http://localhost:8081)
- **Structured Logging**: Correlation IDs, user context, request details
- **Levels**: Debug (dev), Information (prod), Warning, Error, Fatal

```csharp
_logger.LogInformation("User {UserId} logged in successfully", userId);
```

### Tracing

- **Provider**: OpenTelemetry → Jaeger (http://localhost:16686)
- **Traces**: W3C Trace Context, distributed tracing
- **Spans**: HTTP requests, database queries, external API calls

### Metrics

- **Provider**: Prometheus (http://localhost:9090)
- **Metrics**: Request rate, latency (P50, P95, P99), error rate, custom metrics
- **Endpoint**: `/metrics`

### Health Checks

```
GET /health       → Overall health (ready, live)
GET /health/ready → Readiness probe (DB, Redis, Qdrant)
GET /health/live  → Liveness probe (app running)
```

**Health Check Components**:
- PostgreSQL connection
- Redis connection
- Qdrant connection
- Disk space
- Memory usage

---

## 📚 Documentation

### Internal Documentation

- **[CLAUDE.md](../../CLAUDE.md)** - Complete development guide
- **[Architecture Overview](../../docs/01-architecture/overview/system-architecture.md)** - System architecture
- **[API Specification](../../docs/03-api/board-game-ai-api-specification.md)** - API docs
- **[DDD Quick Reference](../../docs/01-architecture/ddd/quick-reference.md)** - DDD patterns
- **[Testing Guide](../../docs/02-development/testing/testing-guide.md)** - Testing standards

### Architecture Decision Records (ADRs)

- **[ADR-001: Hybrid RAG](../../docs/01-architecture/adr/adr-001-hybrid-rag.md)** - Vector + Keyword search
- **[ADR-003b: Unstructured PDF](../../docs/01-architecture/adr/adr-003b-unstructured-pdf.md)** - 3-stage PDF pipeline
- **[ADR-004b: Hybrid LLM](../../docs/01-architecture/adr/adr-004b-hybrid-llm.md)** - Multi-model generation

---

## 🐛 Troubleshooting

### Migration Errors

```bash
# Reset database (CAUTION: data loss!)
dotnet ef database drop --force --project src/Api
dotnet ef database update --project src/Api

# Rollback to specific migration
dotnet ef database update <PreviousMigration> --project src/Api
```

### CORS Errors

```csharp
// Check CORS configuration in Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowCredentials()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

### Auth Issues

```bash
# Check session in database
docker exec -it postgres psql -U meeple -d meepleai
SELECT * FROM "Sessions" WHERE "UserId" = 'xxx';

# Check cookies in browser DevTools
# Should see: .AspNetCore.Session, secure, httpOnly

# Check API key
SELECT * FROM "ApiKeys" WHERE "UserId" = 'xxx';
```

### Qdrant Down

```bash
# Check Qdrant health
curl http://localhost:6333/healthz

# Restart Qdrant
docker compose restart meepleai-qdrant

# Check collections
curl http://localhost:6333/collections
```

---

## 🚀 Deployment

### Docker Build

```bash
# Build image
docker build -t meepleai-api:latest -f src/Api/Dockerfile .

# Run container
docker run -p 8080:8080 \
  -e ConnectionStrings__Postgres="Host=meepleai-postgres;Database=meepleai;..." \
  -e OPENROUTER_API_KEY="sk-or-***" \
  meepleai-api:latest
```

### Production Checklist

- [ ] Environment variables set (secrets in Vault/Infisical)
- [ ] Database migrations applied
- [ ] Connection strings configured
- [ ] Redis cache configured
- [ ] Qdrant vector DB configured
- [ ] Logging configured (Seq/ELK)
- [ ] Monitoring configured (Prometheus/Grafana)
- [ ] Health checks tested
- [ ] SSL/TLS certificates configured
- [ ] CORS origins configured
- [ ] Rate limiting enabled
- [ ] Admin user seeded

See [Deployment Guide](../../docs/05-operations/deployment/board-game-ai-deployment-guide.md) for details.

---

## 🤝 Contributing

1. **Follow DDD patterns** (Domain → Application → Infrastructure → HTTP)
2. **Write tests first** (TDD recommended, 90%+ coverage required)
3. **Use MediatR** for all HTTP endpoints (no direct service injection)
4. **Document ADRs** for architectural decisions
5. **Run tests** before committing (`dotnet test`)
6. **Format code** (`dotnet format`)
7. **Create PR** with `[API]` prefix

---

**Last Updated**: 2025-11-15
**Maintainer**: Backend Team
**Current Phase**: Alpha (DDD 99% complete)
**Test Coverage**: 90%+


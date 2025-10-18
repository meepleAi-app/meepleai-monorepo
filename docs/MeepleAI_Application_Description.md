# MeepleAI
## AI-Powered Board Game Rules Assistant

**Application Documentation**

---

**Version:** 1.0
**Date:** October 18, 2025
**Stack:** ASP.NET Core 9.0 + Next.js 14
**Database:** PostgreSQL + Qdrant + Redis

---

## Table of Contents

1. [Overview](#1-overview)
2. [Technology Stack](#2-technology-stack)
3. [Architecture](#3-architecture)
4. [Key Features](#4-key-features)
5. [Services & Components](#5-services--components)
6. [Authentication & Security](#6-authentication--security)
7. [MCP Servers](#7-mcp-servers)
8. [Observability](#8-observability)
9. [Testing & Quality](#9-testing--quality)
10. [Deployment](#10-deployment)

---

## 1. Overview

MeepleAI is an **AI-powered assistant** designed to help board game players understand complex rulebooks through intelligent question answering. The system processes PDF rulebooks, extracts and chunks text, creates vector embeddings, and uses **Retrieval Augmented Generation (RAG)** to provide accurate answers to player questions.

### Key Capabilities

- ✅ PDF rulebook upload and processing with validation
- ✅ Semantic search using vector embeddings (Qdrant)
- ✅ Intelligent Q&A powered by LLM (OpenRouter API)
- ✅ Streaming responses with Server-Sent Events (SSE)
- ✅ Response caching for improved performance (Redis)
- ✅ Setup guide generation using RAG
- ✅ n8n workflow integration for automation
- ✅ Comprehensive observability (Seq, Jaeger, Prometheus, Grafana)

---

## 2. Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| **Backend** | ASP.NET Core | 9.0 |
| **Frontend** | Next.js | 14 |
| **Database** | PostgreSQL | Latest |
| **Vector DB** | Qdrant | Latest |
| **Cache** | Redis | Latest |
| **AI/LLM** | OpenRouter API | - |
| **Workflow** | n8n | Latest |
| **Container** | Docker | 24.0+ |
| **Package Manager** | pnpm | 9 |

---

## 3. Architecture

### Monorepo Structure

```
meepleai-monorepo/
├── apps/
│   ├── api/                - Backend (ASP.NET Core 9.0)
│   │   ├── src/Api/
│   │   │   ├── Services/      - Business logic
│   │   │   ├── Infrastructure/ - DB context & entities
│   │   │   ├── Models/        - DTOs
│   │   │   ├── Middleware/    - HTTP middleware
│   │   │   ├── Migrations/    - EF Core migrations
│   │   │   └── Program.cs     - Entry point & DI
│   │   └── tests/Api.Tests/   - xUnit + Testcontainers
│   │
│   └── web/                - Frontend (Next.js 14)
│       ├── src/pages/         - Routes
│       ├── src/lib/           - Utilities & API client
│       └── src/components/    - React components
│
├── infra/                  - Docker Compose
├── schemas/                - JSON schemas (RuleSpec v0)
├── docs/                   - Documentation
├── mcp/                    - MCP servers (8 servers)
└── tools/                  - PowerShell scripts
```

### Key Design Patterns

- **Dependency Injection** (ASP.NET Core built-in)
- **Repository Pattern** (EF Core DbContext)
- **Service Layer** (Business logic separation)
- **API Versioning** (URL path strategy: `/api/v1/*`)
- **Event-Driven** (Server-Sent Events for streaming)

---

## 4. Key Features

### 4.1 PDF Processing

Upload and validate PDF rulebooks with comprehensive validation:
- **File size** validation (max 100MB)
- **MIME type** check (application/pdf)
- **Magic bytes** validation (%PDF-)
- **Page count** validation (1-500 pages)
- **PDF version** check (minimum 1.4)

Text extraction using **Docnet.Core**, table extraction using **iText7**.

### 4.2 RAG Pipeline

**Retrieval Augmented Generation** for accurate Q&A:

1. **Text Chunking**: 512 characters with 50 character overlap
2. **Embedding Generation**: OpenRouter API (text-embedding-3-small)
3. **Vector Indexing**: Qdrant vector database
4. **Semantic Search**: Configurable top-K retrieval
5. **LLM Generation**: Context-aware responses

**Offline Evaluation** with IR metrics:
- Precision@K (K=1,3,5,10)
- Mean Reciprocal Rank (MRR)
- Recall@K
- Quality gates enforced in CI

### 4.3 Streaming Responses (CHAT-01)

**Server-Sent Events (SSE)** for real-time streaming:
- Token-by-token progressive display
- State updates (Searching, Generating, etc.)
- Stop button for cancellation
- Citations visible during streaming
- Fallback to cached responses (simulated streaming)

### 4.4 Response Caching (AI-05)

**Redis-based caching** for improved performance:
- SHA256 hash-based cache keys
- Configurable TTL (Time-To-Live)
- Simulated streaming for cached responses
- Automatic cache invalidation on game updates
- Hit/miss metrics tracking

### 4.5 Setup Guide Generation (AI-03)

**AI-powered game setup wizard** using RAG:
- LLM synthesizes setup steps from RAG context
- Step-by-step instructions with descriptions
- Citation references to rulebook pages
- Optional steps detection
- Progress tracking with checkboxes
- Time estimates per step

### 4.6 Authentication (API-01, AUTH-03)

**Dual authentication system**:

**Session Cookies:**
- Session-based authentication for web users
- Auto-revocation on inactivity (configurable, default: 30 days)
- Session management endpoints (admin + user)

**API Keys:**
- `mpl_{environment}_{random_base64}` format
- PBKDF2 hashing (210,000 iterations, SHA256)
- Scopes and expiration support
- Environment tagging (live/test)
- Last used tracking

### 4.7 n8n Integration (N8N-01, N8N-03)

**Webhook-based workflow automation**:
- **Q&A Webhook**: External orchestration for Q&A
- **Explain Webhook**: Rule explanation workflow
- Async processing
- Error handling and retry logic

---

## 5. Services & Components

### 5.1 AI/RAG Services

| Service | Purpose |
|---------|---------|
| `EmbeddingService` | Vector embedding generation via OpenRouter |
| `QdrantService` | Vector database operations (index, search) |
| `TextChunkingService` | Document chunking (512 chars, 50 overlap) |
| `RagService` | Semantic search and retrieval |
| `LlmService` | LLM completions (OpenRouter/Ollama) |
| `StreamingQaService` | Token-by-token streaming Q&A |

### 5.2 PDF Services

| Service | Purpose |
|---------|---------|
| `PdfStorageService` | File storage management |
| `PdfTextExtractionService` | Text extraction (Docnet.Core) |
| `PdfTableExtractionService` | Table extraction (iText7) |
| `PdfValidationService` | Pre-upload validation |

### 5.3 Domain Services

| Service | Purpose |
|---------|---------|
| `GameService` | Game CRUD operations |
| `RuleSpecService` | RuleSpec v0 operations |
| `SetupGuideService` | Setup wizard generation |
| `RuleSpecDiffService` | Version comparison |

### 5.4 Infrastructure Services

| Service | Purpose |
|---------|---------|
| `AuthService` | Session cookie authentication |
| `ApiKeyAuthenticationService` | API key validation |
| `SessionManagementService` | Session CRUD operations |
| `SessionAutoRevocationService` | Background auto-revocation |
| `AuditService` | Audit logging |
| `AiRequestLogService` | AI request tracking |
| `AiResponseCacheService` | Redis-based caching |
| `RateLimitService` | Rate limiting |
| `N8nConfigService` | n8n webhook configuration |
| `BackgroundTaskService` | Background job management |

---

## 6. Authentication & Security

### 6.1 Authentication Methods

**Session Cookies:**
- `AuthService.ValidateSessionAsync()` → ClaimsPrincipal
- Claims: UserId, Email, DisplayName, Role
- Auto-revocation on inactivity

**API Keys:**
- `ApiKeyAuthenticationService.ValidateApiKeyAsync()` → ClaimsPrincipal
- Claims: UserId, Email, DisplayName, Role, Scopes
- PBKDF2 hashing (210,000 iterations)
- Format: `mpl_{env}_{base64}`

### 6.2 Security Features

- ✅ **PBKDF2** password hashing (210,000 iterations, SHA256)
- ✅ **API key** secure hash storage (never plaintext)
- ✅ **Constant-time** hash comparison (timing attack prevention)
- ✅ **Session auto-revocation** (configurable timeout)
- ✅ **API key scopes** and expiration
- ✅ **CORS** policy enforcement
- ✅ **Input validation** and sanitization
- ✅ **SQL injection prevention** (EF Core parameterized queries)
- ✅ **Rate limiting** (configurable per endpoint)
- ✅ **CodeQL SAST** security scanning (CI)
- ✅ **Dependency scanning** (Dependabot, `dotnet list package --vulnerable`)

---

## 7. MCP Servers (Model Context Protocol)

MeepleAI includes **8 containerized MCP servers** for enhanced AI capabilities:

| Server | Purpose | Status |
|--------|---------|--------|
| **github-project-manager** | GitHub issue/PR management | ✅ Healthy |
| **n8n-manager** | Workflow automation | ✅ Healthy |
| **memory-bank** | Persistent memory storage | ✅ Healthy |
| **sequential** | Sequential reasoning | ✅ Running |
| **playwright** | Browser automation | ✅ Running |
| **magic** | UI component generation (21st.dev) | ✅ Running |
| **context7** | Library documentation (Upstash) | ✅ Running |
| **knowledge-graph** | Knowledge graph operations | ✅ Running |

### Security Constraints (All Servers)

- **Read-only filesystem**
- **Dropped capabilities** (ALL)
- **No new privileges**
- **Resource limits** (512MB RAM, 0.5 CPU)
- **Non-root user** (UID 1000)
- **Tmpfs mount** (64MB)

---

## 8. Observability

### 8.1 Logging (Serilog + Seq)

**Centralized logging** with Seq dashboard:
- **URL**: http://localhost:8081
- **Enrichment**: Correlation IDs, UserId, UserEmail, RequestPath, UserAgent, RemoteIp
- **Structured logging** for queryability
- **Log levels**: Info (default), Warning (AspNetCore, EF)

### 8.2 Distributed Tracing (Jaeger)

**OpenTelemetry integration**:
- **URL**: http://localhost:16686
- **OTLP export** to Jaeger
- **W3C Trace Context** propagation
- **Custom Activity Sources** for RAG/AI operations
- **Filtered traces** (health checks excluded)

### 8.3 Metrics (Prometheus + Grafana)

**Custom metrics** for key operations:

**RAG Metrics:**
- `meepleai.rag.requests.total`
- `meepleai.rag.request.duration`
- `meepleai.ai.tokens.used`
- `meepleai.rag.confidence.score`
- `meepleai.rag.errors.total`

**Vector Search Metrics:**
- `meepleai.vector.search.total`
- `meepleai.vector.search.duration`
- `meepleai.vector.results.count`
- `meepleai.vector.indexing.duration`

**PDF Metrics:**
- `meepleai.pdf.upload.total`
- `meepleai.pdf.processing.duration`
- `meepleai.pdf.pages.processed`
- `meepleai.pdf.extraction.errors`

**Cache Metrics:**
- `meepleai.cache.hits.total`
- `meepleai.cache.misses.total`
- `meepleai.cache.evictions.total`

**Dashboards:**
- API Performance
- AI/RAG Operations
- Infrastructure

### 8.4 Health Checks

**Endpoints:**
- `/health` - Detailed health status (all checks)
- `/health/ready` - Kubernetes readiness probe
- `/health/live` - Kubernetes liveness probe

**Monitored Services:**
- PostgreSQL
- Redis
- Qdrant (HTTP + collection)

---

## 9. Testing & Quality

### 9.1 Backend Testing (xUnit)

**Test Types:**
- **Unit tests**: Moq for mocking, SQLite in-memory for DB
- **Integration tests**: Testcontainers (PostgreSQL, Qdrant)
- **Endpoint tests**: WebApplicationFactory
- **Coverage**: Coverlet (target: comprehensive)

**Example Test Classes:**
- `RagServiceTests.cs` - RAG service unit tests
- `QaEndpointsIntegrationTests.cs` - Q&A endpoint integration tests
- `PdfValidationServiceTests.cs` - PDF validation tests
- `StreamingQaServiceTests.cs` - Streaming service tests
- `RagEvaluationIntegrationTests.cs` - Offline evaluation tests

### 9.2 Frontend Testing

**Test Types:**
- **Jest unit tests**: 90% coverage threshold
- **React Testing Library**: Component testing
- **Playwright E2E**: End-to-end flows
- **TypeScript**: Strict mode

**Example Test Files:**
- `pages/__tests__/chat.test.tsx` - Chat page tests
- `pages/__tests__/setup.test.tsx` - Setup wizard tests
- `e2e/chat.spec.ts` - E2E chat flow
- `e2e/setup.spec.ts` - E2E setup flow

### 9.3 CI/CD (GitHub Actions)

**Workflows:**

1. **ci.yml** (Main CI pipeline)
   - Backend: Build → Test
   - Frontend: Lint → Typecheck → Test
   - E2E: Playwright tests
   - RAG Evaluation: Offline metrics

2. **security-scan.yml** (Security)
   - CodeQL SAST (C#, JS/TS)
   - Dependency scanning (`dotnet list package --vulnerable`, `pnpm audit`)
   - .NET Security Analyzers (SecurityCodeScan, NetAnalyzers)

3. **Dependabot** (Weekly updates)
   - NuGet packages
   - npm packages
   - GitHub Actions
   - Docker images

---

## 10. Deployment

### 10.1 Docker Compose Setup

All services containerized with proper networking, volumes, and configuration.

**Service Ports:**

| Service | Port | Purpose |
|---------|------|---------|
| **API** | 8080 | Backend REST API |
| **Web** | 3000 | Frontend Next.js |
| **PostgreSQL** | 5432 | Relational database |
| **Qdrant** | 6333/6334 | Vector database (HTTP/gRPC) |
| **Redis** | 6379 | Cache |
| **Ollama** | 11434 | Local LLM (optional) |
| **n8n** | 5678 | Workflow automation |
| **Seq** | 8081 | Logging dashboard |
| **Jaeger** | 16686 | Tracing UI |
| **Prometheus** | 9090 | Metrics |
| **Grafana** | 3001 | Dashboards |

### 10.2 Environment Variables

**API** (`infra/env/api.env.dev.example`):
- `OPENROUTER_API_KEY` - OpenRouter API key
- `QDRANT_URL` - Qdrant URL (default: http://qdrant:6333)
- `REDIS_URL` - Redis URL (default: redis:6379)
- `SEQ_URL` - Seq URL (default: http://seq:5341)
- `ConnectionStrings__Postgres` - PostgreSQL connection string
- `OTEL_EXPORTER_OTLP_ENDPOINT` - Jaeger OTLP endpoint

**Web** (`infra/env/web.env.dev.example`):
- `NEXT_PUBLIC_API_BASE` - API base URL (default: http://localhost:8080)

**n8n** (`infra/env/n8n.env.dev.example`):
- `N8N_WEBHOOK_URL` - n8n webhook base URL
- `N8N_ENCRYPTION_KEY` - n8n encryption key

### 10.3 Local Development

**Full Stack Startup:**

```bash
# Terminal 1: Infrastructure
cd infra && docker compose up postgres qdrant redis ollama n8n seq jaeger prometheus grafana

# Terminal 2: Backend
cd apps/api/src/Api && dotnet run

# Terminal 3: Frontend
cd apps/web && pnpm dev
```

**Access Points:**
- Frontend: http://localhost:3000
- API: http://localhost:8080
- Seq (logs): http://localhost:8081
- Jaeger (traces): http://localhost:16686
- Prometheus (metrics): http://localhost:9090
- Grafana (dashboards): http://localhost:3001 (admin/admin)
- API metrics: http://localhost:8080/metrics
- Health: http://localhost:8080/health

---

## Conclusion

MeepleAI is a **production-ready**, **AI-powered** board game rules assistant with:

- ✅ Comprehensive **RAG pipeline** for accurate Q&A
- ✅ **Streaming responses** with SSE
- ✅ **Response caching** for performance
- ✅ **Dual authentication** (sessions + API keys)
- ✅ **Full observability** (logs, traces, metrics)
- ✅ **Comprehensive testing** (unit, integration, E2E)
- ✅ **CI/CD with security scanning**
- ✅ **8 MCP servers** for enhanced AI capabilities
- ✅ **Docker Compose** deployment

**Generated on October 18, 2025**

---

**MeepleAI** - *Making board game rules accessible to everyone.*

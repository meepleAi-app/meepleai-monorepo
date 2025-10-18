# CLAUDE.md

Guidance for Claude Code working with this MeepleAI monorepo.

## Overview

AI-powered board game rules assistant. Processes PDF rulebooks, semantic search with vector embeddings, intelligent Q&A.

**Stack**: ASP.NET Core 9.0, Next.js 14, PostgreSQL, Qdrant, Redis, OpenRouter API, n8n, Docker

## Structure

```
apps/api/          - Backend (ASP.NET Core)
  src/Api/
    Services/      - Business logic
    Infrastructure/ - DB context & entities
    Models/        - DTOs
    Migrations/    - EF Core
    Program.cs     - Entry point & DI
  tests/Api.Tests/ - xUnit + Testcontainers
apps/web/          - Frontend (Next.js)
  src/pages/       - Routes
  src/lib/         - Utilities & API client
infra/             - Docker Compose
schemas/           - JSON schemas
docs/              - Documentation
tools/             - PowerShell scripts
```

## Commands

**Backend** (`apps/api`):
- `dotnet build` / `dotnet test` - Build & test
- `dotnet test /p:CollectCoverage=true` - Coverage
- `pwsh tools/measure-coverage.ps1 -Project api` - Quick coverage
- `dotnet ef migrations add <Name> --project src/Api` - New migration
- `dotnet ef database update --project src/Api` - Apply migrations
- Test: xUnit + Moq + Testcontainers

**Frontend** (`apps/web`, pnpm 9):
- `pnpm dev` / `pnpm build` / `pnpm start` - Dev/build/prod
- `pnpm lint` / `pnpm typecheck` - Linting & type checking
- `pnpm test` / `pnpm test:coverage` - Jest (90% threshold)
- `pnpm test:e2e` / `pnpm test:e2e:ui` - Playwright E2E

**Docker** (`infra`):
- `docker compose up -d [--build]` - Start [rebuild]
- `docker compose logs -f [service]` - View logs
- `docker compose down [-v]` - Stop [delete volumes]
- **Ports**: postgres:5432, qdrant:6333/6334, redis:6379, ollama:11434, n8n:5678, seq:8081, jaeger:16686, prometheus:9090, grafana:3001, api:8080, web:3000

## Architecture

**Services** (DI in `Program.cs`, search for `builder.Services.Add`):
- **AI/RAG**: EmbeddingService, QdrantService, TextChunkingService (512 chars, 50 overlap), RagService, LlmService (OpenRouter)
- **PDF**: PdfStorageService, PdfTextExtractionService (Docnet.Core), PdfTableExtractionService (iText7), PdfValidationService (PDF-09)
- **Domain**: GameService, RuleSpecService, RuleSpecDiffService, SetupGuideService
- **Infra**: AuthService (session cookies), SessionManagementService, SessionAutoRevocationService, AuditService, AiRequestLogService, AiResponseCacheService (Redis), RateLimitService, N8nConfigService, BackgroundTaskService

**Database** (EF Core 9.0 + PostgreSQL):
- **Context**: `MeepleAiDbContext` (`Infrastructure/MeepleAiDbContext.cs`)
- **Entities**: User/Auth, Game/RuleSpec, PDF/Vector docs, Chat logs, AI logs, Agents, N8n config
- **Migrations**: Auto-applied in `Program.cs` (search for `Database.Migrate`)
- **Seed Data** (DB-02): Demo users (admin/editor/user@meepleai.dev, pwd: `Demo123!`), games (Tic-Tac-Toe, Chess), rule specs, agents. Migration: `20251009140700_SeedDemoData`. Tests: `SeedDataTests.cs`

**Frontend** (Next.js 14):
- **Pages**: index, chat, upload (complex), editor, versions, admin, n8n, logs, setup (AI-03)
- **API Client**: `lib/api.ts` - `get/post/put/delete`, cookie auth (`credentials: "include"`), 401 handling, base URL from `NEXT_PUBLIC_API_BASE`
- **Tests**: Jest (90% coverage) + Playwright E2E

**Auth** (configured in `Program.cs`): Dual authentication system supports both cookie-based sessions and API keys
- **Cookie Auth**: Session cookies → `AuthService.ValidateSessionAsync()` → ClaimsPrincipal (UserId, Email, DisplayName, Role)
- **API Key Auth**: X-API-Key header → `ApiKeyAuthenticationService.ValidateApiKeyAsync()` → ClaimsPrincipal with scopes
- API key takes precedence if both are provided

**API Key Authentication** (API-01):
- **ApiKeyAuthenticationService** (`Services/ApiKeyAuthenticationService.cs`): Core API key operations
  - `ValidateApiKeyAsync(apiKey)` - Validates API key via PBKDF2 hash verification
  - `GenerateApiKeyAsync(userId, keyName, scopes, expiresAt, environment)` - Generates new API key
  - `RevokeApiKeyAsync(keyId, revokedByUserId)` - Revokes API key
  - Uses PBKDF2 with 210,000 iterations (SHA256) for key hashing, consistent with password hashing
- **ApiKeyAuthenticationMiddleware** (`Middleware/ApiKeyAuthenticationMiddleware.cs`): HTTP middleware
  - Processes `/api/*` paths only (skips health checks, swagger, root)
  - Validates `X-API-Key` header
  - Sets ClaimsPrincipal with user info and scopes on successful validation
  - Returns 401 Unauthorized with JSON error response on failure
  - Falls through to cookie auth if no API key provided
- **API Key Format**: `mpl_{environment}_{random_base64}` (e.g., `mpl_live_abc123...`, `mpl_test_xyz789...`)
- **Database**: `api_keys` table with indexes on `key_hash` (unique), `user_id`, `is_active+expires_at`
- **Migration**: `20251015084754_AddApiKeysTable` - Creates api_keys table and relationships
- **Security Features**:
  - Secure hash storage (never stores plaintext keys)
  - Constant-time hash comparison (prevents timing attacks)
  - Key expiration support (optional)
  - Key revocation support
  - Scoped permissions per key
  - Last used timestamp tracking
  - Environment tagging (live/test)
- **Tests**: 21 unit tests (all passing) + 17 integration tests
  - `ApiKeyAuthenticationServiceTests.cs` - Unit tests with SQLite in-memory
  - `ApiKeyAuthenticationIntegrationTests.cs` - Integration tests with Testcontainers

**Session Management** (AUTH-03):
- **SessionManagementService** (`Services/SessionManagementService.cs`): Core session management
  - `GetUserSessionsAsync()` - List user's active sessions
  - `GetAllSessionsAsync()` - Admin: list all sessions (with filters, pagination)
  - `RevokeSessionAsync()` - Revoke specific session by ID
  - `RevokeAllUserSessionsAsync()` - Revoke all sessions for user (e.g., password change)
  - `RevokeInactiveSessionsAsync()` - Auto-revoke sessions exceeding inactivity threshold
- **SessionAutoRevocationService** (`Services/SessionAutoRevocationService.cs`): Background service
  - Runs periodically (configurable interval, default: 1 hour)
  - Auto-revokes sessions inactive > N days (configurable, default: 30 days)
  - Waits 1 minute after startup before first run
  - Graceful shutdown on cancellation
- **Configuration** (`appsettings.json`): `Authentication:SessionManagement`
  - `InactivityTimeoutDays`: Days before inactive session is revoked (default: 30)
  - `AutoRevocationIntervalHours`: Hours between auto-revocation runs (default: 1)
- **Endpoints** (Admin only, see `Program.cs` session management endpoints):
  - `GET /admin/sessions` - List sessions (optional filters: userId, limit)
  - `DELETE /admin/sessions/{id}` - Revoke specific session
  - `DELETE /admin/users/{userId}/sessions` - Revoke all sessions for user
  - `GET /users/me/sessions` - User views own sessions
- **Tests**: 39 tests (25 unit + 13 integration + 13 background service)
  - `SessionManagementServiceTests.cs` - Unit tests with SQLite in-memory
  - `SessionManagementEndpointsTests.cs` - Integration tests with auth
  - `SessionAutoRevocationServiceTests.cs` - Background service tests

**Streaming Responses** (CHAT-01):
- **ILlmService** (`Services/ILlmService.cs`, `Services/LlmService.cs`): Token-by-token streaming support
  - `GenerateCompletionStreamAsync()` - Returns `IAsyncEnumerable<string>` for streaming tokens
  - Supports both OpenRouter and Ollama LLM backends
  - Proper SSE format parsing with cancellation support
- **IStreamingQaService** (`Services/IStreamingQaService.cs`, `Services/StreamingQaService.cs`): Progressive QA responses via SSE
  - `AskStreamAsync(gameId, query, chatId?)` - Streams QA response with events
  - Event flow: StateUpdate → Citations → Token(s) → Complete
  - Integrates with AI-05 response caching (simulates streaming for cached responses)
  - Tracks tokens, confidence, and snippets for logging
- **SSE Endpoint** (see `/api/v1/agents/qa/stream` in `Program.cs`):
  - `POST /api/v1/agents/qa/stream` - Server-Sent Events endpoint for streaming QA
  - SSE headers: `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `Connection: keep-alive`
  - Chat persistence of complete response after streaming
  - AI request logging with latency, tokens, and confidence metrics
  - Error events sent to client on failure
- **Event Types** (`Models/Contracts.cs`):
  - `StreamingEventType.Token` - Individual LLM token
  - `StreamingToken(string token)` - Token data model
  - Reuses existing: StateUpdate, Citations, Complete, Error, Heartbeat
- **Docs**: `docs/issue/chat-01-streaming-sse-implementation.md` - Complete implementation guide

**Setup Guide Generation** (AI-03):
- **SetupGuideService** (`Services/SetupGuideService.cs`): RAG-powered game setup wizard
  - `GenerateSetupGuideAsync(gameId, chatId?)` - LLM synthesizes setup steps from RAG context
  - Retrieves 10 most relevant chunks via RAG for comprehensive context
  - Uses ILlmService to generate coherent, game-specific instructions
  - Supports optional steps detection (setup variations)
  - Distributes citation references across generated steps
  - Falls back to default steps if LLM/RAG unavailable
  - Integrates with AI-05 response caching for performance
  - Returns structured response: steps with descriptions, citations, time estimates, confidence
- **Frontend** (`pages/setup.tsx`, 735 lines): Interactive setup wizard
  - Game selection with auto-load from /api/v1/games
  - Generate button triggers RAG-powered step synthesis
  - Real-time progress tracking with checkboxes
  - Progress percentage calculation (completed/total)
  - Citation modal for viewing rulebook references with page numbers
  - Reset progress confirmation dialog
  - Authentication gate (login required)
  - Empty state before guide generation
  - Loading indicators during API calls
- **Endpoint** (`Program.cs`, v1Api group):
  - `POST /api/v1/setup/generate` - Generate setup guide (requires authentication)
  - Request: `{ "gameId": "uuid", "chatId": "uuid?" }`
  - Response: `{ "steps": [...], "totalSteps": int, "estimatedTimeMinutes": int, "confidence": float? }`
- **Tests**: Comprehensive coverage across all layers
  - **Backend Unit**: `SetupGuideServiceComprehensiveTests.cs` (20+ tests) - RAG failures, LLM parsing, optional steps, cache scenarios, error handling
  - **Backend Integration**: `SetupGuideEndpointIntegrationTests.cs` (11 tests) - BDD-style, auth, validation, token tracking, concurrent requests
  - **Frontend Unit**: `pages/__tests__/setup.test.tsx` - Component behavior, authentication, data loading, step interactions, citations modal, edge cases
  - **E2E**: `e2e/setup.spec.ts` - Full user flow, authentication gate, guide generation, step completion, progress tracking, citation modal interactions

**RAG Offline Evaluation** (AI-06):
- **RagEvaluationService** (`Services/RagEvaluationService.cs`): Comprehensive IR metrics for RAG system
  - `LoadDatasetAsync(filePath)` - Load test queries from JSON
  - `EvaluateAsync(dataset, topK, thresholds?)` - Run evaluation with quality gates
  - `GenerateMarkdownReport(report)` - Human-readable report with tables
  - `GenerateJsonReport(report)` - Machine-readable JSON output
  - Calculates: Precision@K (K=1,3,5,10), Recall@K, Mean Reciprocal Rank (MRR), latency percentiles (p50, p95, p99)
  - Quality thresholds: P@5 ≥ 0.70, MRR ≥ 0.60, Latency p95 ≤ 2000ms, Success rate ≥ 95%
- **Test Dataset** (`tests/Api.Tests/TestData/rag-evaluation-dataset.json`): 24 queries
  - 8 Tic-Tac-Toe queries (setup, gameplay, winning conditions)
  - 16 Chess queries (setup, piece movement, special moves, draw conditions)
  - Ground truth answers and relevant document IDs for recall calculation
  - Difficulty levels (easy, medium, hard) and categories for analysis
- **CI Integration** (`.github/workflows/ci.yml`): `rag-evaluation` job
  - Runs integration tests with Testcontainers (Postgres + Qdrant)
  - Generates evaluation report (markdown summary)
  - Uploads artifacts (30-day retention)
  - Enforces quality gates (fails CI if thresholds not met)
- **Tests**: 28+ tests (20 unit + 8 integration)
  - `RagEvaluationServiceTests.cs` - Metric calculations, edge cases, report generation
  - `RagEvaluationIntegrationTests.cs` - End-to-end with real Qdrant, quality gate enforcement
- **Docs**: `docs/ai-06-rag-evaluation.md` - Complete guide with metrics explanations, troubleshooting

**Vector Pipeline**: PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService (OpenRouter) → QdrantService → RagService (search)

**PDF Validation** (PDF-09):
- **PdfValidationService** (`Services/PdfValidationService.cs`): Pre-upload PDF validation
  - `ValidateAsync(stream, fileName)` - Comprehensive validation: magic bytes, structure, page count, PDF version
  - `ValidateFileSize(bytes)` - File size validation (max 100MB configurable)
  - `ValidateMimeType(contentType)` - MIME type validation (application/pdf)
  - Thread-safe with Docnet.Core semaphore
  - Returns structured errors: `{ "error": "validation_failed", "details": { "fileSize": "...", "pageCount": "..." } }`
- **Configuration** (`appsettings.json:PdfProcessing`):
  - `MaxFileSizeBytes`: 104857600 (100 MB)
  - `MaxPageCount`: 500
  - `MinPageCount`: 1
  - `MinPdfVersion`: "1.4"
  - `AllowedContentTypes`: ["application/pdf"]
- **Client-Side Validation** (`pages/upload.tsx`):
  - MIME type check (application/pdf)
  - File size check (100MB limit)
  - PDF magic bytes validation (%PDF-)
  - Real-time validation on file selection
  - Visual feedback (red border, error list, success indicator)
- **Server-Side Endpoint** (PDF upload validation in `Program.cs`):
  - Validates before `PdfStorageService.UploadPdfAsync()`
  - Returns 400 Bad Request with structured error details
  - Falls back to upload on validation success
- **Tests**: 42 tests (33 unit + 9 integration)
  - `PdfValidationServiceTests.cs` - File size, MIME, magic bytes, page count, PDF version, structure
  - `PdfUploadValidationIntegrationTests.cs` - End-to-end validation flow, error responses
- **Docs**: `docs/issue/pdf-09-validation-implementation.md` - Complete implementation guide

**API Versioning** (API-01):
- **URL Path Strategy**: All API endpoints under `/api/v1/*`
- **Infrastructure Endpoints**: Unversioned (`/`, `/health`, `/health/ready`, `/health/live`)
- **Swagger/OpenAPI**: Available at `/api/docs` in development mode
  - Supports both ApiKey (`X-API-Key` header) and Cookie authentication
  - Security definitions for both auth methods
  - Bearer token style for API keys
- **Example URLs**:
  - `/api/v1/auth/login` - Authentication
  - `/api/v1/games` - Games list
  - `/api/v1/agents/qa` - Q&A agent
  - `/health/ready` - Health check (unversioned)

**CORS** (configured in `Program.cs`): Policy "web", origins from config, fallback `http://localhost:3000`, credentials enabled

**Logging** (Serilog, see `Api/Logging/LoggingConfiguration.cs`): Console + Seq, enriched (MachineName, EnvironmentName, CorrelationId), X-Correlation-Id header. Levels: Info (default), Warning (AspNetCore, EF)

**Observability** (OPS-01):
- **Health Checks** (configured in `Program.cs`): `/health` (detailed), `/health/ready` (K8s readiness), `/health/live` (K8s liveness). Monitors Postgres, Redis, Qdrant (HTTP + collection)
- **Seq Dashboard**: `http://localhost:8081` - Centralized log aggregation, search by correlation ID, user ID, endpoint. Configured via `SEQ_URL` env var
- **Correlation IDs**: Every request gets `X-Correlation-Id` response header (= `TraceIdentifier`). All logs enriched with `RequestId`, `RequestPath`, `RequestMethod`, `UserAgent`, `RemoteIp`, `UserId`, `UserEmail`
- **Docs**: `docs/observability.md` - Complete observability guide

**OpenTelemetry** (OPS-02):
- **Distributed Tracing**: OTLP export to Jaeger, W3C Trace Context propagation, filtered health checks/metrics from traces
- **Metrics**: Prometheus exporter at `/metrics`, custom meters for RAG/AI, vector search, PDF processing, cache ops
- **Infrastructure**: Jaeger UI (:16686), Prometheus (:9090), Grafana (:3001) with auto-provisioned dashboards
- **Custom Metrics** (`Api/Observability/MeepleAiMetrics.cs`):
  - RAG: `meepleai.rag.requests.total`, `meepleai.rag.request.duration`, `meepleai.ai.tokens.used`, `meepleai.rag.confidence.score`, `meepleai.rag.errors.total`
  - Vector Search: `meepleai.vector.search.total`, `meepleai.vector.search.duration`, `meepleai.vector.results.count`, `meepleai.vector.indexing.duration`
  - PDF: `meepleai.pdf.upload.total`, `meepleai.pdf.processing.duration`, `meepleai.pdf.pages.processed`, `meepleai.pdf.extraction.errors`
  - Cache: `meepleai.cache.hits.total`, `meepleai.cache.misses.total`, `meepleai.cache.evictions.total`
- **Dashboards**: `infra/dashboards/` - API Performance, AI/RAG Operations, Infrastructure (auto-provisioned)
- **Configuration**: `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4318` in `infra/env/api.env.dev`
- **Packages**: OpenTelemetry 1.13.1 (core), Instrumentation 1.12.0 (AspNetCore, Http, Runtime), Prometheus Exporter 1.13.1-beta.1
- **Tests**: `OpenTelemetryIntegrationTests.cs` - Metrics endpoint, HTTP/runtime metrics, Prometheus format
- **Docs**: `docs/ops-02-opentelemetry-design.md` - Architecture & implementation guide

## Testing

**API**: xUnit + Moq + Testcontainers (Postgres, Qdrant), WebApplicationFactory, Coverlet. SQLite for unit tests, Testcontainers for integration. CI: `CI=true`, `DocnetRuntime=linux`

**Web**: Jest (jsdom, 90% threshold) + Playwright E2E. Patterns: `**/__tests__/**/*.[jt]s?(x)`, module alias `@/*`

**Coverage**: `pwsh tools/measure-coverage.ps1 [-Project api|web] [-GenerateHtml]`. See `docs/code-coverage.md`. Note: Full API coverage ~10min, use test filters in dev

## CI/CD

**Main Pipeline** (`.github/workflows/ci.yml`):
- **ci-web**: Lint → Typecheck → Test (Node 20, pnpm 9)
- **ci-api**: Build → Test (.NET 9, postgres, qdrant, libgdiplus, `CI=true`)

**Security** (`.github/workflows/security-scan.yml`, see `docs/security-scanning.md`):
1. **CodeQL SAST**: C#, JS/TS, security-extended queries
2. **Deps**: `dotnet list package --vulnerable`, `pnpm audit` (fails on HIGH/CRITICAL)
3. **.NET Analyzers**: SecurityCodeScan v5.6.7, NetAnalyzers v9.0.0 (SQL injection, XSS, crypto)
4. **Dependabot**: Weekly (Mondays), NuGet, npm, Actions, Docker
- Quick scan: `cd apps/api && dotnet list package --vulnerable --include-transitive` / `cd apps/web && pnpm audit --audit-level=high`
- Reports: 30-day CI artifacts

## Environment

Templates: `infra/env/*.env.*.example`. Never commit `.env.dev/local/prod`

**API**: `OPENROUTER_API_KEY`, `QDRANT_URL` (default: `http://qdrant:6333`), `REDIS_URL` (default: `redis:6379`), `SEQ_URL` (default: `http://seq:5341`), `ConnectionStrings__Postgres`
**Web**: `NEXT_PUBLIC_API_BASE` (default: `http://localhost:8080`)
**n8n**: Workflow config

## Workflows

**New API Endpoint**: Service method → `Program.cs` v1 route group endpoint (line 468+) → models in `Models/` → tests → auth/authz
- All new endpoints must use versioned path: `v1Api.MapGet("/resource", ...)` not `app.MapGet("/api/v1/resource", ...)`
- Infrastructure endpoints (health checks, root) remain unversioned

**New DB Entity**: `Infrastructure/Entities/` → `DbSet<T>` in `MeepleAiDbContext` → `dotnet ef migrations add <Name> --project src/Api` → review → `dotnet ef database update --project src/Api`

**Vector Search Flow**: Upload PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService → `QdrantService.IndexTextChunksAsync()` → query via `RagService.SearchAsync()`. Note: shared context, no tenant partitioning

**New Frontend Page**: Create `pages/<name>.tsx` → use `@/lib/api` → tests in `__tests__/<name>.test.tsx` → 90% coverage

**Local Full Stack**:
```bash
cd infra && docker compose up postgres qdrant redis ollama n8n seq jaeger prometheus grafana  # Terminal 1
cd apps/api/src/Api && dotnet run                                                              # Terminal 2 (port 8080)
cd apps/web && pnpm dev                                                                        # Terminal 3 (port 3000)
# Observability UIs:
# - Seq (logs): http://localhost:8081
# - Jaeger (traces): http://localhost:16686
# - Prometheus (metrics): http://localhost:9090
# - Grafana (dashboards): http://localhost:3001 (admin/admin)
# - API metrics: http://localhost:8080/metrics
```

**RuleSpec v0** (`schemas/rulespec.v0.schema.json`): Machine-readable board game rules for AI/LLM
- **Structure**: Metadata, Setup, Phases, Actions, Scoring, End Conditions, Edge Cases, Glossary
- **Examples**: `tic-tac-toe.rulespec.json`, `chess.rulespec.json` (1146 lines)
- **Validate**: `node schemas/validate-all-examples.js`
- **Code**: `Models/RuleSpecV0.cs`, `Entities/RuleSpecEntity.cs`, `RuleSpecService`, `RuleSpecDiffService`, `RuleSpecV0ModelTests.cs` (786 lines)
- **Docs**: `schemas/README.md`

## Quality

**C#**: Standard conventions, nullable refs (`<Nullable>enable</Nullable>`), async I/O, DI in `Program.cs`, `ILogger<T>`, Serilog, appropriate HTTP status codes

**TS/React**: Strict mode, ESLint (Next.js), avoid `any`, use `@/lib/api`, Arrange-Act-Assert tests

## Key Documentation

- **Security**: `docs/SECURITY.md` - Security policies, secret management, key rotation procedures
- **Database**: `docs/database-schema.md` - Complete DB schema reference
- **Observability**: `docs/observability.md` - Health checks, logging, Seq dashboard, correlation IDs (OPS-01)
- **OpenTelemetry**: `docs/technic/ops-02-opentelemetry-design.md` - Distributed tracing & metrics architecture (OPS-02)
- **RAG Evaluation**: `docs/ai-06-rag-evaluation.md` - Offline evaluation system, IR metrics, quality gates (AI-06)
- **n8n Workflows**: `docs/guide/n8n-integration-guide.md` - n8n webhook integrations (N8N-01: Explain, N8N-03: Q&A)
  - Technical designs: `docs/technic/n8n-webhook-explain-design.md`, `docs/technic/n8n-webhook-qa-design.md`
  - Workflow JSONs: `infra/init/n8n/agent-explain-orchestrator.json`, `infra/init/n8n/agent-qa-webhook.json`
- **Coverage**: `docs/code-coverage.md` - Coverage measurement & tracking
- **Security Scanning**: `docs/security-scanning.md` - CI security scanning guide
- **Schemas**: `schemas/README.md` - RuleSpec v0 schema reference

## Troubleshooting

- **Migration errors**: Check `__EFMigrationsHistory`, rollback: `dotnet ef database update <PreviousMigration>`
- **Qdrant failures**: `curl http://localhost:6333/healthz`, check API startup logs
- **CORS errors**: Verify `NEXT_PUBLIC_API_BASE`, check `Program.cs` CORS config, ensure `credentials: "include"`
- **CI test failures**: Check Docker/Linux issues (Docnet runtime), verify env vars, ensure services healthy
- **Auth issues**: Check `SessionCookieConfiguration`, verify `user_sessions` table, check browser cookie settings
- **Health check failures**: `curl http://localhost:8080/health` - Check Postgres/Redis/Qdrant status. See `docs/observability.md`
- **Seq not receiving logs**: Verify `SEQ_URL` in env, check `docker compose logs seq`, test `curl http://localhost:5341/api`
- i documenti guida mettili in ./docs/guide, i documenti tecnici in ./docs/technic, i documenti sulle risoluzioni issue/pr in ./docs/issue, i documenti sull'app in ./docs
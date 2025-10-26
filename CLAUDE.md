# CLAUDE.md

AI-powered board game rules assistant. PDF processing → vector embeddings → semantic Q&A.

**Stack**: ASP.NET Core 9.0, Next.js 14, PostgreSQL, Qdrant, Redis, OpenRouter API, n8n, Docker

## Structure
```
apps/api/          - Backend (ASP.NET Core)
  src/Api/Services/      - Business logic
  src/Api/Infrastructure/ - DB context & entities
  src/Api/Models/        - DTOs
  src/Api/Migrations/    - EF Core
  tests/Api.Tests/       - xUnit + Testcontainers
apps/web/          - Frontend (Next.js)
  src/pages/       - Routes
  src/lib/         - API client
infra/             - Docker Compose
schemas/           - JSON schemas
docs/              - Documentation
tools/             - PowerShell scripts
```

## Commands

| Task | Command | Notes |
|------|---------|-------|
| **Backend** (`apps/api`) |||
| Build/Test | `dotnet build` / `dotnet test` | xUnit+Moq+Testcontainers |
| Coverage | `dotnet test /p:CollectCoverage=true` | |
| Quick Coverage | `pwsh tools/measure-coverage.ps1 -Project api` | ~10min full |
| New Migration | `dotnet ef migrations add <Name> --project src/Api` | |
| Apply Migrations | `dotnet ef database update --project src/Api` | Auto-applied on startup |
| **Frontend** (`apps/web`, pnpm 9) |||
| Dev/Build/Prod | `pnpm dev` / `pnpm build` / `pnpm start` | Ports: 3000 |
| Lint/Typecheck | `pnpm lint` / `pnpm typecheck` | |
| Test | `pnpm test` / `pnpm test:coverage` | Jest 90% threshold |
| E2E | `pnpm test:e2e` / `pnpm test:e2e:ui` | Playwright |
| **Docker** (`infra`) |||
| Start/Rebuild | `docker compose up -d [--build]` | |
| Logs | `docker compose logs -f [service]` | |
| Stop/Clean | `docker compose down [-v]` | |

**Ports**: postgres:5432, qdrant:6333/6334, redis:6379, ollama:11434, n8n:5678, seq:8081, jaeger:16686, prometheus:9090, grafana:3001, api:8080, web:3000

## Architecture

### Services (DI in `Program.cs`)

| Category | Services | Key Features |
|----------|----------|--------------|
| **AI/RAG** | EmbeddingService, QdrantService, RagService, LlmService | Sentence chunking (256-768 chars), RRF fusion, OpenRouter |
| **PDF** | PdfStorageService, PdfTextExtractionService, PdfTableExtractionService, PdfValidationService | Docnet.Core, iText7, PDF-09 validation |
| **Domain** | GameService, RuleSpecService, RuleSpecDiffService, SetupGuideService | Core business logic |
| **Auth** | AuthService, ApiKeyAuthenticationService, SessionManagementService, SessionAutoRevocationService | Cookie+API key dual auth |
| **Admin** | UserManagementService, AdminStatsService, WorkflowErrorLoggingService | ADMIN-01/02, N8N-05 |
| **Infra** | AuditService, AiRequestLogService, RateLimitService, N8nConfigService, BackgroundTaskService, AlertingService | OPS-07 multi-channel alerts |
| **Cache** (PERF-05) | HybridCacheService, AiResponseCacheService | L1 memory + L2 Redis, stampede protection |

**Performance Optimizations**:
- PERF-05: HybridCache L1+L2, 5min TTL
- PERF-06: AsNoTracking (30% faster reads)
- PERF-07: Sentence-aware chunking (20% better RAG)
- PERF-08: Query expansion + RRF (15-25% recall)
- PERF-09: Connection pools (PG: 10-100, Redis: 3 retries)
- PERF-11: Brotli/Gzip compression (60-80% reduction)

### Database (EF Core 9.0 + PostgreSQL)

- **Context**: `MeepleAiDbContext` (`Infrastructure/MeepleAiDbContext.cs`)
- **Entities**: User/Auth, Game/RuleSpec, PDF/Vector docs, Chat logs, AI logs, Agents, N8n config, API keys, Sessions, Alerts
- **Migrations**: Auto-applied in `Program.cs` (search `Database.Migrate`)
- **Seed Data** (DB-02): Demo users (admin/editor/user@meepleai.dev, pwd: `Demo123!`), games, specs. Migration: `20251009140700_SeedDemoData`

### Frontend (Next.js 14)

**Pages**: index, chat, upload, editor (EDIT-03 rich text), versions, admin, admin/users (ADMIN-01), admin/analytics (ADMIN-02), admin/cache, n8n, logs, setup (AI-03)

**API Client**: `lib/api.ts` - get/post/put/delete, cookie auth (`credentials: "include"`), 401 handling, `NEXT_PUBLIC_API_BASE`

**Tests**: Jest (90% coverage) + Playwright E2E

### Auth (Dual System)

| Method | Flow | Format |
|--------|------|--------|
| **Cookie** | Session cookie → `AuthService.ValidateSessionAsync()` → ClaimsPrincipal | Standard session |
| **API Key** | X-API-Key header → `ApiKeyAuthenticationService.ValidateApiKeyAsync()` → ClaimsPrincipal | `mpl_{env}_{base64}` |

**Priority**: API key > cookie

### Key Features

| Feature | ID | Implementation | Tests |
|---------|----|--------------|----|
| **API Key Auth** | API-01 | ApiKeyAuthenticationService, Middleware, PBKDF2 (210k iter) | 21 unit + 17 integration |
| **Session Mgmt** | AUTH-03 | SessionManagementService, auto-revoke (30d default), background svc | 39 tests |
| **User Mgmt** | ADMIN-01 | UserManagementService, CRUD endpoints, safety checks | 75 tests |
| **Analytics** | ADMIN-02 | AdminStatsService, 8 metrics, 5 charts, CSV/JSON export | 20 tests |
| **Workflow Errors** | N8N-05 | WorkflowErrorLoggingService, n8n webhook, sensitive data redaction | 33 tests |
| **Alerting** | OPS-07 | Email/Slack/PagerDuty, throttling (1hr), Prometheus integration | 11 tests |
| **Streaming QA** | CHAT-01 | ILlmService, SSE endpoint, token-by-token | - |
| **BGG Integration** | AI-13 | BggApiService, search/details, 7d cache, Polly retry | - |
| **Setup Guide** | AI-03 | SetupGuideService, RAG-powered, 10 chunks, citations | 20+ tests |
| **RAG Evaluation** | AI-06 | RagEvaluationService, P@K, MRR, quality gates | 28 tests |
| **PDF Validation** | PDF-09 | PdfValidationService, magic bytes, size, page count | 42 tests |
| **Hybrid Search** | AI-14 | KeywordSearchService, HybridSearchService, RRF fusion (70% vec + 30% keyword) | - |
| **Rich Text Editor** | EDIT-03 | TipTap WYSIWYG, 15+ formats, auto-save (2s debounce) | 11/11 unit + E2E |
| **Prompt Management** | ADMIN-01 | PromptTemplateService, Redis cache-first (<10ms), transaction-safe activation, version control | 40 tests |

**Vector Pipeline**: PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService → QdrantService → RagService

**Prompt Management** (ADMIN-01):
- **PromptTemplateService** (`Services/PromptTemplateService.cs`, `Services/IPromptTemplateService.cs`): Database-driven prompt management with Redis caching
  - `GetActivePromptAsync(templateName)` - Cache-first retrieval (Redis → DB → Config fallback, < 10ms target)
  - `ActivateVersionAsync(templateId, versionId, userId)` - Transaction-safe activation with cache invalidation
  - `InvalidateCacheAsync(templateName)` - Manual cache refresh
  - Architecture: Redis cache (1hr TTL) → PostgreSQL → appsettings.json fallback
  - Backward compatible with AI-07.1 configuration-based templates
- **Admin API** (`Program.cs` v1Api group, lines 3976-4400+): 9 endpoints
  - `GET /api/v1/admin/prompts` - List templates (pagination, category filter)
  - `POST /api/v1/admin/prompts` - Create template
  - `GET /api/v1/admin/prompts/{id}` - Get template details
  - `PUT /api/v1/admin/prompts/{id}` - Update template
  - `DELETE /api/v1/admin/prompts/{id}` - Delete template
  - `POST /api/v1/admin/prompts/{id}/versions` - Create new version
  - `GET /api/v1/admin/prompts/{id}/versions` - Version history
  - `POST /api/v1/admin/prompts/{id}/versions/{versionId}/activate` - Activate version
  - `GET /api/v1/admin/prompts/{id}/audit` - Audit logs
- **Admin UI** (Phase 2, PR #551): 6 pages + Monaco editor
  - `/admin/prompts` - Template list with search, filter, pagination
  - `/admin/prompts/[id]` - Template detail with version history
  - `/admin/prompts/[id]/versions/new` - Create new version (Monaco editor)
  - `/admin/prompts/[id]/versions/[versionId]` - Version detail (readonly Monaco)
  - `/admin/prompts/[id]/compare` - Side-by-side version diff (Monaco DiffEditor)
  - `/admin/prompts/[id]/audit` - Audit log viewer
- **Service Migration** (Phase 3, PR #552): 2 services using database prompts
  - ChessAgentService: `chess-system-prompt` with dynamic FEN sections
  - SetupGuideService: `setup-guide-system-prompt`
  - RagService, StreamingQaService: Already use IPromptTemplateService (Phase 1/AI-07.1)
  - Feature flag: `Features:PromptDatabase` (default: false for safety)
  - Fallback strategy: DB lookup fails OR flag disabled → hardcoded prompts
- **Database**: `prompt_templates`, `prompt_versions`, `prompt_audit_logs` (migrated in AI-14)
- **DTOs**: `PromptManagementDto.cs` - PromptTemplateDto, PromptVersionDto, PromptAuditLogDto
- **Tests**: 44 tests passing (24 Chess + 20 SetupGuide, 100% coverage on migrated services)
- **Seed Data** (Migration 20251026161831): chess-system-prompt, setup-guide-system-prompt (v1, active)
- **Phase Status**: Phase 1-3 ✅ Complete | Phase 4-5 ⏳ Pending (testing framework, deployment)

### API Versioning (API-01)

- **Versioned**: `/api/v1/*` (all business endpoints)
- **Unversioned**: `/`, `/health`, `/health/ready`, `/health/live`
- **Swagger**: `/api/docs` (dev only)
- **Auth**: ApiKey (X-API-Key) + Cookie

### Observability

| Component | Purpose | Location |
|-----------|---------|----------|
| **Health Checks** (OPS-01) | `/health`, `/health/ready`, `/health/live` | Postgres, Redis, Qdrant |
| **Logging** | Serilog → Console + Seq | `X-Correlation-Id` header |
| **Seq Dashboard** | Centralized logs | `http://localhost:8081` |
| **Tracing** (OPS-02) | OTLP → Jaeger, W3C context | `http://localhost:16686` |
| **Metrics** | Prometheus `/metrics` | `http://localhost:9090` |
| **Grafana** | Dashboards (API, AI/RAG, Infra, Quality) | `http://localhost:3001` (admin/admin) |

**Custom Metrics** (`Api/Observability/MeepleAiMetrics.cs`):
- RAG: requests, duration, tokens, confidence, errors
- Vector: search, indexing, results
- PDF: upload, processing, pages, errors
- Cache: hits, misses, evictions
- Quality (AI-11.2): score (rag/llm/citation/overall), low_quality_count

**Prometheus Alerts** (`infra/prometheus-rules.yml`):
- Critical: HighErrorRate (>1/s 2min), ErrorSpike (3x), DatabaseDown, RedisDown, QdrantDown
- Warning: HighErrorRatio (>5% 5min), RagErrors, SlowResponse, HighMemory
- AI Quality: LowOverallConfidence (<60%), HighLowQualityRate (>30%), LowRagConfidence, LowLlmConfidence

## Testing

| Layer | Stack | Coverage |
|-------|-------|----------|
| **API** | xUnit + Moq + Testcontainers (Postgres, Qdrant) | SQLite (unit), Testcontainers (integration) |
| **Web** | Jest (jsdom) + Playwright | 90% threshold |
| **CI** | `CI=true`, `DocnetRuntime=linux` | |

**Coverage**: `pwsh tools/measure-coverage.ps1 [-Project api|web] [-GenerateHtml]`

## CI/CD

**Main Pipeline** (`.github/workflows/ci.yml`):
- ci-web: Lint → Typecheck → Test (Node 20, pnpm 9)
- ci-api: Build → Test (.NET 9, postgres, qdrant, libgdiplus)
- Performance: ~10-12min (post-OPS-06)

**Security** (`.github/workflows/security-scan.yml`):
1. CodeQL SAST (C#, JS/TS)
2. Deps: `dotnet list package --vulnerable`, `pnpm audit`
3. .NET Analyzers: SecurityCodeScan v5.6.7, NetAnalyzers v9.0.0
4. Dependabot: Weekly (Mondays)

**Quick Scan**:
```bash
cd apps/api && dotnet list package --vulnerable --include-transitive
cd apps/web && pnpm audit --audit-level=high
```

## Environment

Templates: `infra/env/*.env.*.example` (never commit `.env.dev/local/prod`)

**API**: `OPENROUTER_API_KEY`, `QDRANT_URL` (http://qdrant:6333), `REDIS_URL` (redis:6379), `SEQ_URL` (http://seq:5341), `ConnectionStrings__Postgres`
**Web**: `NEXT_PUBLIC_API_BASE` (http://localhost:8080)

## Workflows

**New API Endpoint**:
1. Service method → `Program.cs` v1Api group (line 468+) → `Models/` DTOs → tests → auth
2. Use `v1Api.MapGet("/resource", ...)` NOT `app.MapGet("/api/v1/resource", ...)`
3. Infrastructure endpoints remain unversioned

**New DB Entity**:
`Infrastructure/Entities/` → `DbSet<T>` in `MeepleAiDbContext` → `dotnet ef migrations add <Name> --project src/Api` → review → `dotnet ef database update --project src/Api`

**Vector Search Flow**:
Upload PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService → `QdrantService.IndexTextChunksAsync()` → query via `RagService.SearchAsync()`

**New Frontend Page**:
`pages/<name>.tsx` → use `@/lib/api` → `__tests__/<name>.test.tsx` → 90% coverage

**Local Full Stack**:
```bash
cd infra && docker compose up postgres qdrant redis ollama n8n seq jaeger prometheus grafana  # T1
cd apps/api/src/Api && dotnet run                                                              # T2 (8080)
cd apps/web && pnpm dev                                                                        # T3 (3000)
# UIs: Seq :8081, Jaeger :16686, Prometheus :9090, Grafana :3001, Metrics /metrics
```

**RuleSpec v0** (`schemas/rulespec.v0.schema.json`): Machine-readable rules for AI/LLM
- Structure: Metadata, Setup, Phases, Actions, Scoring, End, Edge Cases, Glossary
- Examples: tic-tac-toe, chess (1146 lines)
- Validate: `node schemas/validate-all-examples.js`
- Code: `Models/RuleSpecV0.cs`, `Entities/RuleSpecEntity.cs`, Services, Tests (786 lines)

## Quality Standards

**C#**: Standard conventions, nullable refs, async I/O, DI in `Program.cs`, `ILogger<T>`, Serilog, proper HTTP codes
**TS/React**: Strict mode, ESLint, avoid `any`, use `@/lib/api`, AAA tests

## Key Docs

| Category | Path | Content |
|----------|------|---------|
| **Security** | `docs/SECURITY.md` | Policies, secrets, key rotation |
| **Database** | `docs/database-schema.md` | Complete schema ref |
| **Observability** | `docs/observability.md` | Health, logging, correlation (OPS-01) |
| **OpenTelemetry** | `docs/technic/ops-02-opentelemetry-design.md` | Tracing, metrics (OPS-02) |
| **Perf Summary** | `docs/technic/performance-optimization-summary.md` | PERF-05 to PERF-11 |
| **HybridCache** | `docs/technic/perf-05-hybridcache-implementation.md` | L1+L2 cache (PERF-05) |
| **AsNoTracking** | `docs/technic/perf-06-asnotracking-implementation.md` | EF optimization (PERF-06) |
| **Chunking** | `docs/technic/perf-07-sentence-aware-chunking.md` | Sentence-aware (PERF-07) |
| **Query Expansion** | `docs/technic/perf-08-query-expansion.md` | RRF fusion (PERF-08) |
| **Pooling** | `docs/technic/perf-09-connection-pooling.md` | PG/Redis/HTTP (PERF-09) |
| **Async** | `docs/technic/perf-10-async-all-the-way.md` | 100% async audit (PERF-10) |
| **Compression** | `docs/technic/perf-11-response-compression.md` | Brotli/Gzip (PERF-11) |
| **P2 Analysis** | `docs/technic/perf-p2-analysis.md` | Phase 2 feasibility |
| **RAG Eval** | `docs/ai-06-rag-evaluation.md` | IR metrics, gates (AI-06) |
| **RAG Opt** | `docs/technic/ai-07-rag-optimization-phase1.md` | Phase 1 design (AI-07) |
| **Hybrid Search** | `docs/guide/hybrid-search-guide.md` | PG FTS + Qdrant RRF (AI-14) |
| **n8n Integration** | `docs/guide/n8n-integration-guide.md` | N8N-01, N8N-03 webhooks |
| **n8n Errors** | `docs/guide/n8n-error-handling.md` | Error handling (N8N-05) |
| **Coverage** | `docs/code-coverage.md` | Measurement & tracking |
| **Security Scan** | `docs/security-scanning.md` | CI scanning guide |
| **Repo Migration** | `docs/guide/repository-visibility-migration.md` | Public ↔ private |
| **Schemas** | `schemas/README.md` | RuleSpec v0 reference |
| **Skills** | `docs/guides/SKILLS_GUIDE.md` | 21 available skills |

## Troubleshooting

| Issue | Check | Fix |
|-------|-------|-----|
| **Migration errors** | `__EFMigrationsHistory` | `dotnet ef database update <PreviousMigration>` |
| **Qdrant failures** | `curl http://localhost:6333/healthz` | Check API logs |
| **CORS errors** | `NEXT_PUBLIC_API_BASE`, `Program.cs` CORS | Verify `credentials: "include"` |
| **CI test failures** | Docker/Linux, env vars | Verify Docnet runtime=linux |
| **Auth issues** | `SessionCookieConfiguration`, `user_sessions` | Check browser cookies |
| **Health check failures** | `curl http://localhost:8080/health` | Check PG/Redis/Qdrant |
| **Seq logs missing** | `SEQ_URL`, `docker compose logs seq` | `curl http://localhost:5341/api` |

**Doc Locations**: guides → `docs/guide`, technical → `docs/technic`, issues/PRs → `docs/issue`, app docs → `docs`

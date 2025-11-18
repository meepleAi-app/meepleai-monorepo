# CLAUDE.md

**MeepleAI** - AI board game rules assistant. Italian-first, >95% accuracy target.

**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n

**UI**: Shadcn/UI (Radix + Tailwind), Tailwind CSS 4

---

## Architecture (DDD - 100% Complete)

**7 Bounded Contexts** - CQRS/MediatR architecture:

```
apps/api/src/Api/BoundedContexts/
├── Authentication/         Auth, sessions, API keys, OAuth, 2FA
├── GameManagement/         Games catalog, play sessions
├── KnowledgeBase/          RAG, vectors, chat (Hybrid: vector+keyword RRF) [STREAMING CQRS]
├── DocumentProcessing/     PDF upload, extraction, validation
├── WorkflowIntegration/    n8n workflows, error logging
├── SystemConfiguration/    Runtime config, feature flags
└── Administration/         Users, alerts, audit, analytics
```

**Pattern**: Domain (pure logic) → Application (CQRS) → Infrastructure (adapters) → HTTP (MediatR)

**Eliminated**: 5,387 lines legacy code (Services: 3,710 lines | Error handling: 1,677 lines from Issue #1194)
**Services Removed**: GameService 181, AuthService 346, PDF services 1,300, UserManagementService 243, Streaming services 940, RuleSpec Comment/Diff services 700
**Error Handling Centralized**: 53 try-catch blocks removed from endpoints (AiEndpoints 315L, ChatEndpoints 77L, RuleSpecEndpoints 129L)
**Retained**: ConfigurationService, AdminStatsService, AlertingService, RagService (orchestration/infrastructure)

---

## Structure

```
apps/api/
  BoundedContexts/{Context}/
    Domain/           Aggregates, VOs, domain services
    Application/      Commands, Queries, Handlers (MediatR)
    Infrastructure/   Repositories, adapters
  Infrastructure/     EF Core DbContext, entities
    BackgroundTasks/  IBackgroundTaskOrchestrator (Redis-backed)
  Routing/           HTTP endpoints (use IMediator, NOT services)
apps/web/
  src/app/           Next.js App Router (31 routes, RSC + client)
  src/app/layout.tsx Root layout + AppProviders (Intl, Theme, Query, Auth)
  src/lib/api/       Modular API SDK (HttpClient + feature clients)
infra/               Docker Compose
docs/                Architecture, ADRs, guides
```

---

## Commands

| Area | Command | Notes |
|------|---------|-------|
| **Quick Start** | `./quick-start.sh` | Automated setup (no tests, ~2min) |
| | `./tools/setup-test-environment.sh` | Full setup with tests (~5min) |
| | `./tools/setup-test-environment.sh --full` | Complete suite + E2E (~12min) |
| **Backend** | `dotnet build && dotnet test` | xUnit+Testcontainers |
| | `dotnet ef migrations add <Name> --project src/Api` | Auto-applied |
| **Frontend** | `pnpm dev` / `pnpm build` / `pnpm test` | Jest 90%+ |
| **Docker** | `docker compose up -d` | Full stack (15 services) |

**Services**:
- **Core**: meepleai-postgres:5432, meepleai-qdrant:6333, meepleai-redis:6379
- **AI/ML**: meepleai-ollama:11434, meepleai-embedding:8000, meepleai-unstructured:8001, meepleai-smoldocling:8002
- **Observability**: meepleai-seq:8081, meepleai-jaeger:16686, meepleai-prometheus:9090, meepleai-alertmanager:9093, meepleai-grafana:3001
- **Workflow**: meepleai-n8n:5678
- **App**: meepleai-api:8080, meepleai-web:3000

---

## Key Features

### RAG Pipeline (Hybrid Search - ADR-001)
- **Retrieval**: Vector (Qdrant) + Keyword (PG FTS) → RRF fusion (70/30)
- **Generation**: Multi-model (GPT-4 + Claude consensus)
- **Validation**: 5-layer (confidence ≥0.70, citation verify, forbidden keywords)
- **Quality**: P@10, MRR, confidence tracking, <3% hallucination target

### Auth (Dual + 2FA)
- **Cookie**: Session-based (httpOnly, secure)
- **API Key**: `mpl_{env}_{base64}` format, PBKDF2 (210k iter)
  - **Browser**: httpOnly cookie (XSS-protected, POST /api/v1/auth/apikey/login)
  - **Programmatic**: X-API-Key header (CLI, scripts)
  - **Priority**: Cookie > Header
- **OAuth**: Google/Discord/GitHub, token encryption (DataProtection)
- **2FA**: TOTP + backup codes, temp sessions (5min)

### Frontend Pages (SPRINT-1)
- **Settings** (`/settings`): 4-tab comprehensive settings page
  - Profile: Display name, email, password change (UI ready, backend pending)
  - Preferences: Language, theme, notifications, data retention (mock data)
  - Privacy: 2FA management + OAuth linking (fully functional)
  - Advanced: API key authentication (fully functional), sessions, account deletion (placeholders)
- **Profile** (`/profile`): Deprecated, redirects to `/settings`
- **UI**: Shadcn/UI components (Tabs, Label, Alert, Separator, Card, Input, Select, Switch)

### Performance (PERF-05 to PERF-11)
- HybridCache L1+L2 (5min TTL)
- AsNoTracking (30% faster reads)
- Sentence chunking (20% better RAG)
- Query expansion + RRF (15-25% recall boost)
- Connection pools (PG: 10-100, Redis: 3 retries)
- Brotli/Gzip (60-80% compression)

### Background Task Orchestration
- **IBackgroundTaskOrchestrator**: Redis-backed distributed coordination
  - Immediate, delayed, and recurring task scheduling
  - Task status tracking (Scheduled, Running, Completed, Failed, Cancelled)
  - Task cancellation with CancellationToken propagation
  - Distributed locking for cross-server coordination
- **RedisBackgroundTaskOrchestrator**: Production implementation
  - Redis key pattern: `meepleai:tasks:status:{taskId}`, `meepleai:tasks:lock:{lockKey}`
  - Task status TTL: 24 hours
  - Lua scripts for atomic lock operations
  - Graceful error handling and logging
- **OAuth State Storage**: Migrated to Redis (IOAuthStateStore)
  - Redis key pattern: `meepleai:oauth:state:{state}`
  - Automatic expiration via TTL (10 minutes)
  - Single-use CSRF tokens (atomic validation + removal)
  - Distributed-safe (works across multiple servers)

### Observability
- **Health**: `/health` (ready/live), checks PG/Redis/Qdrant
- **Logs**: Serilog → Seq, correlation IDs
- **Traces**: OpenTelemetry → Jaeger (W3C)
- **Metrics**: Prometheus `/metrics`, Grafana dashboards
- **Alerts**: Email/Slack/PagerDuty (OPS-07)

### Dynamic Config (CONFIG-01-06)
- 3-tier fallback: DB → appsettings.json → defaults
- Admin UI: `/admin/configuration`
- Categories: Features, RateLimit, AI/LLM, RAG, PDF
- Version control, rollback, bulk ops

---

## Database

**EF Core 9 + PostgreSQL**:
- Entities: User, Game, RuleSpec, Session, ApiKey, OAuthAccount, Alert, PdfDocument, VectorDoc, ChatThread, N8nConfig
- Migrations: Auto-applied on startup
- Seed: Demo users (admin/editor/user@meepleai.dev, pwd: `Demo123!`)

**Repositories** (per context): Pure infrastructure, no business logic

---

## HTTP Endpoints

**Pattern**: All use `IMediator.Send()`, ZERO service injection

**Examples**:
```csharp
// Authentication
POST   /api/v1/auth/register               → RegisterCommand
POST   /api/v1/auth/login                  → LoginCommand (2FA support)
POST   /api/v1/auth/logout                 → LogoutCommand
POST   /api/v1/auth/apikey/login           → LoginWithApiKeyCommand (sets httpOnly cookie)
POST   /api/v1/auth/apikey/logout          → LogoutApiKeyCommand (removes httpOnly cookie)
GET    /api/v1/auth/oauth/{provider}/callback → HandleOAuthCallbackCommand (CQRS)

// Games
GET    /api/v1/games              → GetAllGamesQuery
POST   /api/v1/games              → CreateGameCommand

// RAG
POST   /api/v1/chat               → AskQuestionCommand (streaming SSE)
GET    /api/v1/search             → SearchQuery (hybrid)
```

**Auth Priority**: API key > cookie

---

## PDF Processing Pipeline (BGAI - Production Ready)

**3-Stage Fallback Architecture** (ADR-003b - Unstructured):
```
PDF Upload → EnhancedPdfProcessingOrchestrator
               ├─ Stage 1: Unstructured (≥0.80 quality) - 80% success, 1.3s avg
               ├─ Stage 2: SmolDocling VLM (≥0.70 quality) - 15% fallback, 3-5s avg
               └─ Stage 3: Docnet (best effort) - 5% fallback, fast
                     ↓
            PdfQualityValidationDomainService
                     ↓
            Quality Reports + Recommendations
```

**Components**:
- `OrchestratedPdfTextExtractor`: IPdfTextExtractor adapter for orchestrator
- `EnhancedPdfProcessingOrchestrator`: 3-stage coordinator with quality routing
- `PdfQualityValidationDomainService`: Threshold enforcement + reporting
- `UnstructuredPdfTextExtractor`: Stage 1 (Apache 2.0, RAG-optimized)
- `SmolDoclingPdfTextExtractor`: Stage 2 (VLM 256M, complex layouts)
- `DocnetPdfTextExtractor`: Stage 3 (local fallback)

**Quality Metrics** (4-metric scoring):
- Text coverage: 40% (chars/page ratio)
- Structure detection: 20% (titles, headers, lists)
- Table detection: 20% (game rules tables)
- Page coverage: 20% (all pages processed)

**Configuration**:
```json
"PdfProcessing": {
  "Extractor": {
    "Provider": "Orchestrator"  // Use 3-stage pipeline (recommended)
  },
  "Quality": {
    "MinimumThreshold": 0.80,
    "MinCharsPerPage": 500
  }
}
```

**Tests**: 50 PDF tests (8 integration + 10 orchestrator + 10 quality + 6 E2E + 16 validation)

---

## Testing

**Strategy**: Test Pyramid (70% unit, 20% integration, 5% quality, 5% E2E) + Performance Tests (k6)
**Coverage**: 90%+ enforced (frontend 90.03%, backend 90%+)

**Test Pyramid**:
```
           /\
          /  \     E2E Tests (5%) - ~5min
         /____\    User journey scenarios
        /      \
       /________\  Quality Tests (5%) - ~15min
      /          \ 5-metric framework
     /____________\
    /              \ Integration Tests (20%) - <2min
   /                \ Multi-service interaction
  /____________________\
 /                      \ Unit Tests (70%) - <5s
/__________________________\ 90%+ coverage
```

**Stack**:
- Backend: xUnit + Moq + Testcontainers (Postgres, Qdrant, Unstructured, SmolDocling, Redis)
- Frontend: Jest + React Testing Library + Playwright + Lighthouse CI
- Performance: k6 load testing (API endpoints, database, Redis, WebSocket)
- CI: GitHub Actions (~14min, optimized)

**Tests**: 4,033 frontend + 189 backend + 30 E2E = 4,252 total
- New: 27 infrastructure tests (RedisOAuthStateStore: 13, RedisBackgroundTaskOrchestrator: 14)

**Performance Testing** (Issue #873 - k6):
- **RAG Search**: <2s P95, 1000 req/s target
- **Chat Messaging**: <1s P95, 500 req/s target
- **Game Search**: <500ms P95, 2000 req/s target
- **Session Updates**: <100ms P95, 1000 req/s target
- **Database Stress**: Concurrent query handling
- **Redis Cache**: Hit rate >80%, <50ms P95
- **WebSocket**: 1000+ concurrent connections
- **Nightly CI**: Automated benchmarking + regression detection

**Quality Tests** (5-Metric Framework):
- **Accuracy**: ≥80% on golden dataset (1000 Q&A pairs)
- **Hallucination**: ≤10% forbidden keywords detection
- **Confidence**: ≥0.70 average RAG retrieval quality
- **Citation**: ≥80% page number + snippet validation
- **Latency**: ≤3000ms P95 response time

**Performance Testing** (Issue #842):
- **Lighthouse CI**: Automated performance monitoring with Core Web Vitals
- **Thresholds**: LCP <2.5s, FID <100ms, CLS <0.1, Performance ≥85%, Accessibility ≥95%
- **Pages**: Homepage, Chat, Upload (Priority 1) + Games, Login (Priority 2)
- **CI/CD**: Automatic runs on PRs, >10% regression fails build
- **Commands**: `pnpm test:performance` (Playwright) | `pnpm lighthouse:ci` (audits)
- **Reports**: HTML/JSON artifacts with 30-day retention

**Test Execution**:
```bash
# Unit Tests (70%)
pnpm test                    # Frontend unit tests
dotnet test                  # Backend unit tests

# Integration Tests (20%)
dotnet test --filter "Category=Integration"

# Quality Tests (5%)
dotnet test --filter "FullyQualifiedName~RagEvaluationIntegrationTests"

# E2E Tests (5%)
pnpm test:e2e               # User journey scenarios

# Performance Tests (k6)
cd tests/k6
npm run test:smoke          # Quick smoke test
npm run test:load           # Full load test
npm run test:stress         # Stress test
npm run test:rag-search     # RAG endpoint test
npm run test:chat           # Chat endpoint test
npm run test:games          # Games endpoint test
npm run test:sessions       # Sessions endpoint test
```

---

## DDD Migration Status

**100% Complete** (2025-11-16):
- ✅ 7/7 contexts migrated to CQRS (ALL at 100%)
- ✅ 96+ CQRS handlers operational (including 3 streaming handlers + 8 agent handlers + 13 RuleSpec comment/diff handlers)
- ✅ 5,180 lines legacy code removed (+146 from Issue #1191: OAuth callback service method)
- ✅ 83+ endpoints migrated to MediatR (OAuth callback fully CQRS-compliant)
- ✅ Zero build errors
- ✅ Streaming RAG/QA/Setup migrated to IAsyncEnumerable pattern
- ✅ Agent services (Chess, Feedback, FollowUp) migrated to CQRS (#1188)
- ✅ RuleSpec Comment/Diff services migrated to CQRS (#1189)
- ✅ **Domain Events**: 40 events + 39 handlers + integration events (#1190)
- ✅ OAuth callback legacy code removed (#1191)

**Contexts**: All 7 contexts at 100% - Authentication (OAuth fully CQRS-compliant), GameManagement, KnowledgeBase, DocumentProcessing, WorkflowIntegration, SystemConfiguration, Administration

**Domain Events** (Issue #1190 - Complete):
- **Infrastructure**: Event dispatcher in DbContext, base handler with auto-audit
- **Authentication**: 11 events (PasswordChanged, EmailChanged, RoleChanged, 2FA, OAuth, ApiKey, Session)
- **GameManagement**: 10 events (GameCreated/Updated, SessionLifecycle, PlayerAdded)
- **KnowledgeBase**: 14 events (Agent lifecycle, Chat messages, Vector documents)
- **WorkflowIntegration**: 5 events (N8n config, Workflow errors)
- **Integration Events**: Cross-context communication infrastructure (e.g., GameCreated → WorkflowIntegration)
- **Auto-Audit**: All domain events automatically create audit log entries

**Pattern Reused**:
1. Implement handlers (Commands/Queries)
2. Migrate endpoints to MediatR
3. Run tests
4. Remove legacy service
5. Commit

---

## Environment

**API** (.env in `infra/env/`):
- `OPENROUTER_API_KEY` (LLM)
- `ConnectionStrings__Postgres`
- `QDRANT_URL`, `REDIS_URL`, `SEQ_URL`
- `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD` (bootstrap)

**Web**:
- `NEXT_PUBLIC_API_BASE=http://localhost:8080`

**Never commit** `.env.dev/local/prod`

---

## Workflows

**New Feature**:
1. Domain logic in `BoundedContexts/{Context}/Domain/`
2. Command/Query in `Application/`
3. Handler in `Application/Handlers/`
4. Endpoint uses `IMediator.Send()`
5. Tests (domain + integration)

**Vector Search**:
PDF → PdfTextExtractor → TextChunking → Embedding → Qdrant → RagService (hybrid search)

**Local Stack**:
```bash
cd infra && docker compose up meepleai-postgres meepleai-qdrant meepleai-redis meepleai-n8n meepleai-seq    # T1
cd apps/api/src/Api && dotnet run                              # T2 (8080)
cd apps/web && pnpm dev                                        # T3 (3000)
```

---

## Quality Standards

- **C#**: Nullable refs, async/await, DI, ILogger, Serilog
- **IDisposable**: Always `using`, `IHttpClientFactory`, no resource leaks
- **TS/React**: Strict mode, ESLint, avoid `any`, `@/lib/api`
- **Tests**: AAA pattern, 90%+ coverage

**Code Review**: All PRs require approval, CI green, coverage maintained

---

## Key Docs

**See [docs/INDEX.md](docs/INDEX.md) for complete navigation** (115 docs, 800+ pages)

| Doc | Path |
|-----|------|
| **Architecture** | `docs/01-architecture/overview/system-architecture.md` |
| **API Spec** | `docs/03-api/board-game-ai-api-specification.md` |
| **ADR Hybrid RAG** | `docs/01-architecture/adr/adr-001-hybrid-rag.md` |
| **ADR Multi-Layer Validation** | `docs/01-architecture/adr/adr-006-multi-layer-validation.md` |
| **ADR Cosine Similarity** | `docs/01-architecture/adr/adr-005-cosine-similarity-consensus.md` |
| **ADR PDF Processing** | `docs/01-architecture/adr/adr-003b-unstructured-pdf.md` |
| **Security** | `SECURITY.md`, `docs/06-security/code-scanning-remediation-summary.md` |
| **OAuth Security** | `docs/06-security/oauth-security.md` |
| **Testing** | `docs/02-development/testing/testing-guide.md` |
| **Shadcn/UI** | `docs/04-frontend/shadcn-ui-installation.md` |
| **AI Provider Config** | `docs/03-api/ai-provider-configuration.md`, `docs/02-development/ai-provider-integration.md` |

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Migration errors | `dotnet ef database update <PreviousMigration>` |
| CORS errors | Check `NEXT_PUBLIC_API_BASE`, `credentials: "include"` |
| Qdrant down | `curl http://localhost:6333/healthz` |
| Auth issues | Check cookies, sessions table |
| CI failures | Verify Docker/Linux, env vars |

**Health Check**: `curl http://localhost:8080/health`

---

## CI/CD

**GitHub Actions** (5 workflows optimized):
- **`ci.yml`**: Main CI pipeline (~14min, 38% faster since 2025-11-09)
  - Web: Lint → Typecheck → Unit Tests (90%+) → E2E Tests → Accessibility
  - API: Build → Unit & Integration Tests (90%+) → Quality Tests (RAG evaluation)
  - Validation: Schemas, Observability configs (Prometheus, Alertmanager, Grafana)
- **`security-scan.yml`**: CodeQL (C#, JS/TS) + Dependency scanning + Semgrep + Secrets detection
- **`migration-guard.yml`**: EF Core migration validation (prevents deletion)
- **`lighthouse-ci.yml`**: Performance monitoring (Core Web Vitals, Lighthouse CI)
- **`storybook-deploy.yml`**: Storybook build + deploy (Vercel, Chromatic)

**Security**:
- CodeQL SAST (C#, JS/TS)
- `dotnet list package --vulnerable`
- `pnpm audit --audit-level=high`
- Dependabot: Weekly

**2025-11-17 Optimization**: Removed 3 redundant workflows (test-automation, integration-tests, cleanup-caches), saving ~50% CI time and costs

---

## Maintenance

**Local Cache Cleanup** (run locally as needed):
```bash
bash tools/cleanup-caches.sh --dry-run      # Preview
bash tools/cleanup-caches.sh                # Run
```

**Cleans**: `.serena/`, `codeql-db/`, `.playwright-mcp/`, build artifacts

**Note**: Cache cleanup is for local development only. GitHub Actions runners are ephemeral and auto-cleaned.

---

## Phase Status

**Current**: Alpha (pre-production, DDD refactoring COMPLETE)
**DDD**: **99% complete** (7/7 contexts, 72+ handlers, 60+ endpoints, 2,070 lines removed)
**Next**: Final polish (1%) → Beta testing (2-4 weeks) → Production
**Target**: 10,000 MAU by Phase 4, >99.5% uptime SLA

---

**Version**: 1.0-rc (DDD 99%, k6 Performance Suite)
**Last Updated**: 2025-11-18
**Last Verified**: 2025-11-18 (against codebase)
**Owner**: Engineering Lead

---

**Note**: For complete documentation index see [docs/INDEX.md](docs/INDEX.md). Docker services and ports updated to reflect full observability stack (15 services total).


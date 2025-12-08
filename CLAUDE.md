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
├── KnowledgeBase/          RAG, vectors, chat (Hybrid: vector+keyword RRF)
├── DocumentProcessing/     PDF upload, extraction, validation
├── WorkflowIntegration/    n8n workflows, error logging
├── SystemConfiguration/    Runtime config, feature flags
└── Administration/         Users, alerts, audit, analytics
```

**Pattern**: Domain (pure logic) → Application (CQRS) → Infrastructure (adapters) → HTTP (MediatR)

**Eliminated**: 2,070 lines legacy services (GameService 181, AuthService 346, PDF services 1,300, UserManagementService 243)
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
  Routing/           HTTP endpoints (use IMediator, NOT services)
apps/web/
  pages/             Routes (SSR/SSG)
  lib/api.ts         Client (cookie+API key auth)
infra/               Docker Compose
docs/                Architecture, ADRs, guides
```

---

## Commands

| Area | Command | Notes |
|------|---------|-------|
| **Backend** | `dotnet build && dotnet test` | xUnit+Testcontainers |
| | `dotnet ef migrations add <Name> --project src/Api` | Auto-applied |
| **Frontend** | `pnpm dev` / `pnpm build` / `pnpm test` | Vitest 90%+ |
| | `pnpm storybook` | Storybook dev server |
| | `pnpm test:visual` | Visual regression (Chromatic) |
| **Docker** | `docker compose up -d` | Full stack (default via COMPOSE_PROFILES=full) |
| | `./start-minimal.sh` | Core only (postgres, redis, qdrant, api, web) |
| | `./start-dev.sh` | Dev + basic monitoring (minimal + prometheus, grafana) |
| | `./start-observability.sh` | Full observability (+ alertmanager, hyperdx) |
| | `./start-ai.sh` | AI/ML services only |
| | `./start-automation.sh` | Automation (n8n) only |
| | `docker compose --profile <profile> up` | Manual profile selection (minimal/dev/observability/ai/automation/full) |

**Docker Profiles** (Issue #702):
- **minimal**: Core services only (postgres, redis, qdrant, api, web)
- **dev**: Development + basic observability (minimal + prometheus, grafana)
- **observability**: Full monitoring stack (dev + alertmanager, hyperdx)
- **ai**: AI/ML services (ollama, embedding, unstructured, smoldocling, reranker)
- **automation**: Workflow automation (n8n)
- **full**: Everything (default for backward compatibility)

**Services**:
- **Core**: postgres:5432, qdrant:6333, redis:6379
- **AI/ML**: ollama:11434, embedding:8000, unstructured:8001, smoldocling:8002
- **Observability**: hyperdx:8180, prometheus:9090, alertmanager:9093, grafana:3001
- **Workflow**: n8n:5678
- **App**: api:8080, web:3000

---

## Key Features

### RAG Pipeline (Hybrid Search - ADR-001)
- **Retrieval**: Vector (Qdrant) + Keyword (PG FTS) → RRF fusion (70/30)
- **Generation**: Multi-model (GPT-4 + Claude consensus)
- **Validation**: 5-layer (confidence ≥0.70, citation verify, forbidden keywords)
- **Quality**: P@10, MRR, confidence tracking, <3% hallucination target

### RAG Evaluation Pipeline (ADR-016 Phase 5)
- **Grid Search**: 12 configurations (3 chunking × 2 quantization × 2 reranking)
- **Metrics**: Recall@5, Recall@10, nDCG@10, MRR, P95 latency
- **Phase 5 Targets**: Recall@10 ≥ 70%, P95 < 1500ms
- **Dashboard**: Grafana (`infra/dashboards/rag-evaluation.json`)
- **Runbook**: [RAG Evaluation Runbook](docs/05-operations/runbooks/rag-evaluation-pipeline.md)
- **Components**: `RunGridSearchHandler`, `GridSearchConfiguration`, `BenchmarkReport`, `ReportGeneratorService`

### Auth (Dual + 2FA)
- **Cookie**: Session-based (httpOnly, secure)
- **API Key**: `mpl_{env}_{base64}` format, PBKDF2 (210k iter)
- **OAuth**: Google/Discord/GitHub, token encryption (DataProtection)
- **2FA**: TOTP + backup codes, temp sessions (5min)

### Frontend Pages (SPRINT-1)
- **Settings** (`/settings`): 4-tab comprehensive settings page
  - Profile: Display name, email, password change (UI ready, backend pending)
  - Preferences: Language, theme, notifications, data retention (mock data)
  - Privacy: 2FA management + OAuth linking (fully functional)
  - Advanced: API keys, sessions, account deletion (placeholders)
- **Profile** (`/profile`): Deprecated, redirects to `/settings`
- **UI**: Shadcn/UI components (Tabs, Label, Alert, Separator, Card, Input, Select, Switch)

### Performance (PERF-05 to PERF-11)
- HybridCache L1+L2 (5min TTL)
- AsNoTracking (30% faster reads)
- Sentence chunking (20% better RAG)
- Query expansion + RRF (15-25% recall boost)
- Connection pools (PG: 10-100, Redis: 3 retries)
- Brotli/Gzip (60-80% compression)

### Observability
- **Health**: `/health` (ready/live), checks PG/Redis/Qdrant
- **Unified Platform**: HyperDX (logs, traces, session replay)
- **Logs**: Serilog → HyperDX OTLP, correlation IDs
- **Traces**: OpenTelemetry → HyperDX (W3C)
- **Metrics**: Prometheus `/metrics`, Grafana dashboards
- **Alerts**: Email/Slack/PagerDuty (OPS-07)

### Dynamic Config (CONFIG-01-06)
- 3-tier fallback: DB → appsettings.json → defaults
- Admin UI: `/admin/configuration`
- Categories: Features, RateLimit, AI/LLM, RAG, PDF
- Version control, rollback, bulk ops

### Visual Testing (Chromatic)
- **Status**: Phase 1 Complete (Infrastructure Setup)
- **Coverage**: 0% (Phase 2 - In Planning)
- **Mode**: Non-blocking (will enable blocking at 50%+ coverage)
- **CI Integration**: Automatic visual regression on PRs
- **Scripts**: `pnpm test:visual`, `pnpm test:visual:ci`, `pnpm test:visual:debug`
- **Documentation**: [Visual Testing Guide](docs/02-development/testing/visual-testing-guide.md)

---

## Database

**EF Core 9 + PostgreSQL**:
- Entities: User, Game, RuleSpec, Session, ApiKey, OAuthAccount, Alert, PdfDocument, VectorDoc, ChatThread, N8nConfig
- Migrations: Auto-applied on startup
- Initial admin user created from environment variables on first run

**Repositories** (per context): Pure infrastructure, no business logic

---

## HTTP Endpoints

**Pattern**: All use `IMediator.Send()`, ZERO service injection

**Examples**:
```csharp
// Authentication
POST   /api/v1/auth/register      → RegisterCommand
POST   /api/v1/auth/login         → LoginCommand (2FA support)
POST   /api/v1/auth/logout        → LogoutCommand

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

**Coverage**: 90%+ enforced (frontend 90.03%, backend 90%+)

**Stack**:
- Backend: xUnit + Moq + Testcontainers (Postgres, Qdrant, Unstructured, SmolDocling)
- Frontend: Jest + React Testing Library + Playwright
- CI: GitHub Actions (~14min, optimized)

**Tests**: 4,033 frontend + 162 backend + 30 E2E = 4,225 total

---

## DDD Migration Status

**100% Complete** (2025-12-06 - Issue #1676):
- ✅ 7/7 contexts migrated (all at 100%)
- ✅ 72+ CQRS handlers operational
- ✅ 2,470+ lines legacy code removed (+400 from backward compat layers)
- ✅ 60+ endpoints migrated to MediatR
- ✅ Backward compatibility layers removed (frontend + backend)
- ✅ 99.7% test pass rate maintained
- ✅ Zero build errors

**Contexts**: All 7 bounded contexts at 100% DDD compliance
**Latest:** Issue #1676 - Removed all DTO → legacy model conversions (3-phase migration: Frontend, Backend, Storybook)

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
- `QDRANT_URL`, `REDIS_URL`, `HYPERDX_OTLP_ENDPOINT`
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
cd infra && docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml up -d postgres qdrant redis hyperdx    # T1
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
| **ADR PDF Processing** | `docs/01-architecture/adr/adr-003b-unstructured-pdf.md` |
| **ADR Embedding Pipeline** | `docs/01-architecture/adr/adr-016-advanced-pdf-embedding-pipeline.md` |
| **RAG Eval Runbook** | `docs/05-operations/runbooks/rag-evaluation-pipeline.md` |
| **Security** | `SECURITY.md`, `docs/06-security/code-scanning-remediation-summary.md` |
| **Environment Variables** | `docs/06-security/environment-variables-production.md` |
| **OAuth Security** | `docs/06-security/oauth-security.md` |
| **Testing** | `docs/02-development/testing/test-writing-guide.md` |
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

**GitHub Actions** (`.github/workflows/ci.yml`):
- ci-web: Lint → Typecheck → Test (90%+)
- ci-api: Build → Test → RAG eval
- security-scan: CodeQL, dependency audit, analyzers
- Performance: ~14min (38% faster, optimized 2025-11-09)

**Security**:
- CodeQL SAST (C#, JS/TS)
- `dotnet list package --vulnerable`
- `pnpm audit --audit-level=high`
- Dependabot: Weekly

---

## Maintenance

**Cache Cleanup** (monthly):
```bash
bash tools/cleanup-caches.sh --dry-run      # Preview
bash tools/cleanup-caches.sh                # Run
```

**Cleans**: `.serena/`, `codeql-db/`, `.playwright-mcp/`, build artifacts

---

## Phase Status

**Current**: Alpha (pre-production, DDD refactoring COMPLETE)
**DDD**: **100% complete** (7/7 contexts, 72+ handlers, 60+ endpoints, 2,070 lines removed)
**Next**: Beta testing (2-4 weeks) → Production
**Target**: 10,000 MAU by Phase 4, >99.5% uptime SLA

---

**Version**: 1.0-rc (DDD 100%)
**Last Updated**: 2025-12-07
**Last Verified**: 2025-12-07 (against codebase)
**Owner**: Engineering Lead

---

**Note**: For complete documentation index see [docs/INDEX.md](docs/INDEX.md). Docker services and ports updated to reflect full observability stack (15 services total).

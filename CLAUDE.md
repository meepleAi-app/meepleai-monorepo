# CLAUDE.md

**MeepleAI** - AI board game rules assistant. Italian-first, >95% accuracy target.

**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n

---

## Architecture (DDD - 100% Complete)

**7 Bounded Contexts** - Full CQRS/MediatR:

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

**Eliminated**: 2,870+ lines legacy services (GameService, AuthService, PDF services, ConfigurationService, UserManagementService)

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
| **Frontend** | `pnpm dev` / `pnpm build` / `pnpm test` | Jest 90%+ |
| **Docker** | `docker compose up -d` | PG, Qdrant, Redis, n8n, Seq, Jaeger |

**Ports**: api:8080, web:3000, pg:5432, qdrant:6333, redis:6379, n8n:5678, seq:8081, jaeger:16686

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
- **OAuth**: Google/Discord/GitHub, token encryption (DataProtection)
- **2FA**: TOTP + backup codes, temp sessions (5min)

### Performance (PERF-05 to PERF-11)
- HybridCache L1+L2 (5min TTL)
- AsNoTracking (30% faster reads)
- Sentence chunking (20% better RAG)
- Query expansion + RRF (15-25% recall boost)
- Connection pools (PG: 10-100, Redis: 3 retries)
- Brotli/Gzip (60-80% compression)

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

## Testing

**Coverage**: 90%+ enforced (frontend 90.03%, backend 90%+)

**Stack**:
- Backend: xUnit + Moq + Testcontainers (Postgres, Qdrant)
- Frontend: Jest + React Testing Library + Playwright
- CI: GitHub Actions (~14min, optimized)

**Tests**: 4,033 frontend + 112 backend + 30 E2E = 4,175 total

---

## DDD Migration Status

**100% Complete** (2025-01-11):
- ✅ 7/7 contexts with full CQRS
- ✅ 57+ handlers operational
- ✅ 2,627+ lines legacy code removed
- ✅ 160 endpoints migrated to MediatR
- ✅ 99.1% test pass rate
- ✅ Zero build errors

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
cd infra && docker compose up postgres qdrant redis n8n seq    # T1
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

| Doc | Path |
|-----|------|
| **Architecture** | `docs/architecture/board-game-ai-architecture-overview.md` |
| **API Spec** | `docs/api/board-game-ai-api-specification.md` |
| **ADR Hybrid RAG** | `docs/architecture/adr-001-hybrid-rag-architecture.md` |
| **DDD Status** | `docs/refactoring/ddd-status-and-roadmap.md` |
| **DB Schema** | `docs/database-schema.md` |
| **OAuth Setup** | `docs/guide/oauth-setup-guide.md` |
| **Security** | `docs/SECURITY.md`, `docs/security-scanning.md` |
| **Testing** | `docs/testing/test-writing-guide.md` |

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

**Current**: Alpha (pre-production, aggressive refactoring OK)
**DDD**: 100% complete (all 7 contexts migrated)
**Next**: Beta testing (2-4 weeks) → Production
**Target**: 10,000 MAU by Phase 4, >99.5% uptime SLA

---

**Version**: 1.0 (DDD Complete)
**Last Updated**: 2025-01-15
**Owner**: Engineering Lead

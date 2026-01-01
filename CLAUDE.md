# CLAUDE.md

**MeepleAI** - AI board game rules assistant. Italian-first, >95% accuracy.
**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n
**UI**: Shadcn/UI (Radix + Tailwind CSS 4)

---

## Architecture (DDD 100%)

**7 Bounded Contexts** - CQRS/MediatR:
```
apps/api/src/Api/BoundedContexts/
├── Authentication/         Auth, sessions, API keys, OAuth, 2FA
├── GameManagement/         Games catalog, play sessions
├── KnowledgeBase/          RAG, vectors, chat (Hybrid RRF)
├── DocumentProcessing/     PDF upload, extraction, validation
├── WorkflowIntegration/    n8n workflows, error logging
├── SystemConfiguration/    Runtime config, feature flags
└── Administration/         Users, alerts, audit, analytics
```

**Pattern**: Domain → Application (CQRS) → Infrastructure → HTTP (MediatR)
**Eliminated**: 2,070 lines legacy services | **Retained**: ConfigurationService, AdminStatsService, AlertingService, RagService

---

## Structure

```
apps/api/BoundedContexts/{Context}/Domain|Application|Infrastructure/
apps/api/Infrastructure/     EF Core DbContext
apps/api/Routing/           HTTP endpoints (use IMediator, NOT services)
apps/web/pages|lib/api.ts   Routes (SSR/SSG), Client
infra/                      Docker Compose
docs/                       Architecture, ADRs, guides
```

---

## Commands

| Area | Command |
|------|---------|
| **Backend** | `dotnet build && dotnet test` (xUnit+Testcontainers) |
| | `dotnet ef migrations add <Name> --project src/Api` |
| **Frontend** | `pnpm dev` / `pnpm build` / `pnpm test` (Vitest 90%+) |
| **Docker** | `docker compose up -d` (full) / `./start-minimal.sh` (core) / `./start-dev.sh` |
| **Traefik** | `docker compose -f docker-compose.yml -f docker-compose.traefik.yml up -d` |

**Profiles**: minimal (core) | dev (+monitoring) | observability (+alertmanager) | ai | automation | full
**Services**: postgres:5432, qdrant:6333, redis:6379, api:8080, web:3000, grafana:3001, n8n:5678

---

## Key Features

### RAG (ADR-001)
- **Retrieval**: Vector (Qdrant) + Keyword (PG FTS) → RRF 70/30
- **Validation**: 5-layer, confidence ≥0.70, <3% hallucination
- **Eval**: Grid search 12 configs, Recall@10 ≥70%, P95 <1500ms

### Auth (Dual + 2FA)
- **Cookie**: httpOnly, secure | **API Key**: `mpl_{env}_{base64}` PBKDF2
- **OAuth**: Google/Discord/GitHub | **2FA**: TOTP + backup codes

### PDF Pipeline (ADR-003b)
```
Stage 1: Unstructured (≥0.80) → Stage 2: SmolDocling (≥0.70) → Stage 3: Docnet
```
Config: `"PdfProcessing.Extractor.Provider": "Orchestrator"`

### Performance
HybridCache L1+L2 | AsNoTracking | Sentence chunking | RRF | Brotli/Gzip

### Observability
Health: `/health` | Logs: Serilog→HyperDX | Traces: OpenTelemetry | Metrics: Prometheus

### Dynamic Config
3-tier: DB → appsettings.json → defaults | Admin UI: `/admin/configuration`

---

## HTTP Endpoints

**Pattern**: `IMediator.Send()` only, ZERO service injection | **Auth**: API key > cookie

```csharp
POST /api/v1/auth/register|login|logout  → Register|Login|LogoutCommand
GET  /api/v1/games                       → GetAllGamesQuery
POST /api/v1/chat                        → AskQuestionCommand (SSE)
```

---

## Database

**EF Core 9 + PostgreSQL**: User, Game, RuleSpec, Session, ApiKey, OAuthAccount, Alert, PdfDocument, VectorDoc, ChatThread
Migrations auto-applied | Initial admin from env vars

---

## Testing

**Coverage**: 90%+ | **Stack**: xUnit+Moq+Testcontainers (BE), Vitest+Playwright (FE)
**Total**: 4,033 FE + 162 BE + 30 E2E = 4,225 | **CI**: ~14min

---

## Environment

```
OPENROUTER_API_KEY, ConnectionStrings__Postgres, QDRANT_URL, REDIS_URL
INITIAL_ADMIN_EMAIL, INITIAL_ADMIN_PASSWORD
NEXT_PUBLIC_API_BASE=http://localhost:8080
```
Never commit `.env.*`

---

## Workflows

**New Feature**: Domain → Application (Command/Query) → Handler → Endpoint (`IMediator.Send()`) → Tests

**Local**:
```bash
cd infra && docker compose up -d postgres qdrant redis  # T1
cd apps/api/src/Api && dotnet run                       # T2 :8080
cd apps/web && pnpm dev                                 # T3 :3000
```

---

## Quality

- **C#**: Nullable refs, async/await, DI, Serilog, `using` for IDisposable
- **TS**: Strict mode, ESLint, no `any`, `@/lib/api`
- **PRs**: Approval required, CI green, 90%+ coverage

---

## Key Docs (Living Documentation v1.0)

**Living Docs System**: Auto-generate da codice + ADR manuali (47 files, ~84% riduzione)

| Resource | Path/URL |
|----------|----------|
| **INDEX** | [docs/INDEX.md](docs/INDEX.md) (complete documentation index) |
| **Living Docs Guide** | [docs/living-documentation.md](docs/living-documentation.md) |
| **API Reference** | http://localhost:8080/scalar/v1 (Scalar UI - interactive) |
| **OpenAPI Spec** | http://localhost:8080/openapi/v1.json (auto-generated) |
| **ADRs** | [docs/01-architecture/adr/](docs/01-architecture/adr/) (22 architectural decisions) |
| **Architecture** | [docs/01-architecture/overview/](docs/01-architecture/overview/) (system design) |
| **Security** | [SECURITY.md](SECURITY.md) (consolidated security docs) |
| **Traefik** | [infra/traefik/README.md](infra/traefik/README.md) (reverse proxy config) |

**Bounded Context README**: `apps/api/src/Api/BoundedContexts/{Context}/README.md` (template-based)
**XML Docs**: Auto-generated on build → `apps/api/src/Api/bin/Debug/net9.0/Api.xml`

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Migration errors | `dotnet ef database update <Previous>` |
| CORS | Check `NEXT_PUBLIC_API_BASE`, `credentials: "include"` |
| Health | `curl http://localhost:8080/health` |

---

## CI/CD

**Actions**: ci-web (lint→typecheck→test) | ci-api (build→test) | security-scan (CodeQL)
**Security**: CodeQL, `dotnet list package --vulnerable`, `pnpm audit`, Dependabot weekly

---

## Status

**Phase**: Alpha (DDD 100% complete) → Beta (2-4 weeks) → Production
**Target**: 10K MAU, >99.5% uptime

---

**v1.0-rc** | Updated: 2025-12-13 | Owner: Engineering Lead

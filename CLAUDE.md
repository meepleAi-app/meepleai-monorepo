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
- **Ports**: postgres:5432, qdrant:6333, redis:6379, n8n:5678, seq:8081, api:8080, web:3000

## Architecture

**Services** (DI in `Program.cs:100-139`):
- **AI/RAG**: EmbeddingService, QdrantService, TextChunkingService (512 chars, 50 overlap), RagService, LlmService (OpenRouter)
- **PDF**: PdfStorageService, PdfTextExtractionService (Docnet.Core), PdfTableExtractionService (iText7)
- **Domain**: GameService, RuleSpecService, RuleSpecDiffService, SetupGuideService
- **Infra**: AuthService (session cookies), SessionManagementService, SessionAutoRevocationService, AuditService, AiRequestLogService, AiResponseCacheService (Redis), RateLimitService, N8nConfigService, BackgroundTaskService

**Database** (EF Core 9.0 + PostgreSQL):
- **Context**: `MeepleAiDbContext` (`Infrastructure/MeepleAiDbContext.cs`)
- **Entities**: User/Auth, Game/RuleSpec, PDF/Vector docs, Chat logs, AI logs, Agents, N8n config
- **Migrations**: Auto-applied (`Program.cs:184`)
- **Seed Data** (DB-02): Demo users (admin/editor/user@meepleai.dev, pwd: `Demo123!`), games (Tic-Tac-Toe, Chess), rule specs, agents. Migration: `20251009140700_SeedDemoData`. Tests: `SeedDataTests.cs`

**Frontend** (Next.js 14):
- **Pages**: index, chat, upload (complex), editor, versions, admin, n8n, logs
- **API Client**: `lib/api.ts` - `get/post/put/delete`, cookie auth (`credentials: "include"`), 401 handling, base URL from `NEXT_PUBLIC_API_BASE`
- **Tests**: Jest (90% coverage) + Playwright E2E

**Auth** (`Program.cs:226-248`): Cookie-based sessions → `AuthService.ValidateSessionAsync()` → ClaimsPrincipal (UserId, Email, DisplayName, Role)

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
- **Endpoints** (Admin only, `Program.cs:1724-1793`):
  - `GET /admin/sessions` - List sessions (optional filters: userId, limit)
  - `DELETE /admin/sessions/{id}` - Revoke specific session
  - `DELETE /admin/users/{userId}/sessions` - Revoke all sessions for user
  - `GET /users/me/sessions` - User views own sessions
- **Tests**: 39 tests (25 unit + 13 integration + 13 background service)
  - `SessionManagementServiceTests.cs` - Unit tests with SQLite in-memory
  - `SessionManagementEndpointsTests.cs` - Integration tests with auth
  - `SessionAutoRevocationServiceTests.cs` - Background service tests

**Vector Pipeline**: PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService (OpenRouter) → QdrantService → RagService (search)

**CORS** (`Program.cs:141-170`): Policy "web", origins from config, fallback `http://localhost:3000`, credentials enabled

**Logging** (Serilog, `Program.cs:22-45`): Console + Seq, enriched (MachineName, EnvironmentName, CorrelationId), X-Correlation-Id header. Levels: Info (default), Warning (AspNetCore, EF)

**Observability** (OPS-01):
- **Health Checks** (`Program.cs:162-177`): `/health` (detailed), `/health/ready` (K8s readiness), `/health/live` (K8s liveness). Monitors Postgres, Redis, Qdrant (HTTP + collection)
- **Seq Dashboard**: `http://localhost:8081` - Centralized log aggregation, search by correlation ID, user ID, endpoint. Configured via `SEQ_URL` env var
- **Correlation IDs**: Every request gets `X-Correlation-Id` response header (= `TraceIdentifier`). All logs enriched with `RequestId`, `RequestPath`, `RequestMethod`, `UserAgent`, `RemoteIp`, `UserId`, `UserEmail`
- **Docs**: `docs/observability.md` - Complete observability guide

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

**New API Endpoint**: Service method → `Program.cs` endpoint (line 250+) → models in `Models/` → tests → auth/authz

**New DB Entity**: `Infrastructure/Entities/` → `DbSet<T>` in `MeepleAiDbContext` → `dotnet ef migrations add <Name> --project src/Api` → review → `dotnet ef database update --project src/Api`

**Vector Search Flow**: Upload PDF → PdfTextExtractionService → TextChunkingService → EmbeddingService → `QdrantService.IndexTextChunksAsync()` → query via `RagService.SearchAsync()`. Note: shared context, no tenant partitioning

**New Frontend Page**: Create `pages/<name>.tsx` → use `@/lib/api` → tests in `__tests__/<name>.test.tsx` → 90% coverage

**Local Full Stack**:
```bash
cd infra && docker compose up postgres qdrant redis n8n seq  # Terminal 1 (add seq for logs)
cd apps/api/src/Api && dotnet run                            # Terminal 2 (port 8080)
cd apps/web && pnpm dev                                      # Terminal 3 (port 3000)
# Seq Dashboard: http://localhost:8081
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
- **Workflows**: `docs/N8N-01-README.md` - n8n workflow automation guide
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

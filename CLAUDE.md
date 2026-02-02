# MeepleAI Monorepo - Developer Guide

**AI board game assistant: RAG, multi-agent, living docs**

## Quick Reference

| Task | Command | Location |
|------|---------|----------|
| Start API | `dotnet run` | `apps/api/src/Api/` |
| Start Web | `pnpm dev` | `apps/web/` |
| Run Tests | `dotnet test` / `pnpm test` | Root of each app |
| Setup Secrets | `pwsh setup-secrets.ps1 -SaveGenerated` | `infra/secrets/` |
| Migration | `dotnet ef migrations add Name` | `apps/api/src/Api/` |
| API Docs | http://localhost:8080/scalar/v1 | Browser |
| Infra | `docker compose up -d postgres qdrant redis` | `infra/` |

## Stack & Features

**Backend** (.NET 9): ASP.NET Minimal APIs + MediatR | PostgreSQL 16 + EF Core | Qdrant + Redis | FluentValidation | xUnit + Testcontainers

**Frontend** (Next.js 14): App Router + React 18 | Tailwind + shadcn/ui | Zustand + React Query | Vitest + Playwright

**AI** (Python): sentence-transformers | cross-encoder | Unstructured | SmolDocling

**Core Features**: RAG (hybrid retrieval) | Multi-agent AI | PDF processing (OCR) | Community game catalog | SSE streaming | CQRS pattern

## Architecture

### 🔴 CQRS Pattern (CRITICAL)

**Rule**: Endpoints use ONLY `IMediator.Send()` - ZERO direct service injection

```csharp
// ✅ CORRECT
app.MapPost("/api/v1/auth/register", async (RegisterCommand cmd, IMediator m) =>
    Results.Ok(await m.Send(cmd)));

// ❌ FORBIDDEN
app.MapPost("/api/v1/auth/register", async (RegisterCommand cmd, IAuthService svc) => ...);
```

### DDD Bounded Contexts (9)

| Context | Responsibility |
|---------|---------------|
| Administration | Users, roles, audit, analytics |
| Authentication | Auth flows, sessions, OAuth, 2FA |
| DocumentProcessing | PDF upload, extraction, chunking |
| GameManagement | Catalog, sessions, FAQs, specs |
| KnowledgeBase | RAG, AI agents, chat, vector search |
| SharedGameCatalog | Community DB w/ soft-delete |
| SystemConfiguration | Runtime config, flags |
| UserLibrary | Collections, wishlist, history |
| UserNotifications | Alerts, email, push |
| WorkflowIntegration | n8n, webhooks, logging |

**Layers**: Domain → Application (commands/queries) → Infrastructure

## Development

### 🔴 PowerShell for Docker (Windows)

```bash
# ✅ CORRECT
pwsh -c "docker logs meepleai-api --tail=50"

# ❌ WRONG (bash pipe issues)
docker logs meepleai-api | grep pattern
```

### Quick Start

```bash
# 1. Dependencies
cd apps/api/src/Api && dotnet restore
cd ../../../web && pnpm install

# 2. Secrets (auto-gen saves 15-30min)
cd ../../infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated

# 3. Frontend env
cd ../../apps/web && cp .env.development.example .env.local

# 4. Start services
cd ../../infra && docker compose up -d postgres qdrant redis
cd ../apps/api/src/Api && dotnet run  # Terminal 1: :8080
cd ../../../web && pnpm dev           # Terminal 2: :3000
```

### Secret Management

**System**: `.secret` files (Issue #2570) - 10 total files

| Priority | Files | Behavior |
|----------|-------|----------|
| 🔴 CRITICAL | database, redis, qdrant, jwt, admin, embedding-service | Blocks startup |
| 🟡 IMPORTANT | openrouter, unstructured-service, bgg | Warns |
| 🟢 OPTIONAL | oauth, email, monitoring, n8n, storage, traefik, smoldocling/reranker | Silent |

**Workflow**:
```bash
# Setup: cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated
# Update: nano infra/secrets/redis.secret && docker compose restart redis
```

**Rules**: ✅ Run setup script, gitignore `.secret`, rotate 90d | ❌ Commit secrets, use dev in prod

### Git Workflow

**Branches**: `main-dev` (dev) | `frontend-dev` (frontend) | `main` (prod) | `feature/issue-{n}-{desc}`

**🔴 PR Target Rule**: Feature branches MUST merge to parent branch (track: `git config branch.<feature>.parent <parent>`)

```bash
# Standard flow
git checkout frontend-dev && git pull
git checkout -b feature/issue-123-desc
git config branch.feature/issue-123-desc.parent frontend-dev  # Track parent
# ... work ...
git commit -m "feat(scope): description"
# ... test (dotnet test / pnpm test) ...
git push -u origin feature/issue-123-desc
# PR to frontend-dev (NOT main!) → merge → cleanup
git checkout frontend-dev && git pull && git branch -D feature/issue-123-desc
```

**Commits**: `feat|fix|docs|refactor|test|chore(scope): description`

### Feature Development Pattern

**Flow**: Domain → Application (Command + Validator + Handler) → Endpoint → Tests

```
1. Domain: Game.MarkAsPlayed() { PlayCount++; }
2. Application: MarkGameAsPlayedCommand + Handler
3. Endpoint: app.MapPut("/games/{id}/mark-played", async (Guid id, IMediator m) => ...)
4. Tests: Unit (domain) + Integration (DB) + E2E (HTTP)
```

### Migrations

```bash
cd apps/api/src/Api
dotnet ef migrations add DescriptiveName
dotnet ef database update
```

**Best practices**: Review SQL, test dev first, never delete old migrations

## Code Standards

### C# Backend

**Naming**: PascalCase (public) | camelCase+`_` (private) | `I` prefix (interfaces)

**Key Patterns**:
- **Entity**: Private setters + factory method (`Game.Create()`)
- **Value Object**: Immutable record + validation (`Email.Create()`)
- **Exception**: Domain-specific (`GameNotFoundException`)

*Full examples: `docs/02-development/coding-standards.md`*

### TypeScript Frontend

**Naming**: PascalCase (components/types) | camelCase (functions/vars) | UPPER_SNAKE_CASE (constants)

**Key Patterns**:
- **Component**: Typed props + explicit JSX.Element return
- **Store**: Zustand with TypeScript interface

*Full examples: `docs/02-development/coding-standards.md`*

## Testing

### Backend (Target: 90%+)

**Stats**: 737 test files | 8,630+ tests | Unit 70% | Integration 25% | E2E 5%

```bash
cd apps/api/src/Api
dotnet test                                         # All
dotnet test --filter "Category=Unit"                # Unit only
dotnet test --filter "BoundedContext=GameManagement"  # By context
dotnet test /p:CollectCoverage=true                 # With coverage
```

**Test Patterns**: `docs/05-testing/backend/backend-testing-patterns.md`

### Frontend (Target: 85%+)

```bash
cd apps/web
pnpm test && pnpm test:coverage     # Unit (Vitest)
pnpm test:e2e                       # E2E (Playwright)
pnpm typecheck && pnpm lint         # Quality
```

## Common Commands

| Task | Backend | Frontend |
|------|---------|----------|
| **Build** | `dotnet build` | `pnpm build` |
| **Run** | `dotnet run` | `pnpm dev` |
| **Test** | `dotnet test` | `pnpm test` |
| **Lint** | Built into build | `pnpm lint` |
| **Clean** | `dotnet clean` | `rm -rf .next` |

**Infra**:
```bash
docker compose up -d postgres qdrant redis  # Start core
docker compose logs -f api                  # View logs
docker compose down -v                      # Reset (⚠️ data loss!)
```

## Project Structure

```
apps/
├── api/src/Api/          # .NET 9: BoundedContexts/, Routing/, Infrastructure/
├── web/                  # Next.js: src/app/, components/, lib/, __tests__/
├── embedding-service/    # Python: embeddings
├── reranker-service/     # Python: reranking
└── {smoldocling,unstructured}-service/  # Python: PDF/docs

docs/                     # Architecture, dev guides, API ref
infra/                    # docker-compose.yml, secrets/, monitoring/
tests/Api.Tests/          # Backend test suite
.github/workflows/        # CI/CD pipelines
```

## Documentation

- **API**: http://localhost:8080/scalar/v1
- **Dev Guides**: `docs/02-development/`
- **API Reference**: `docs/03-api/`
- **Testing**: `docs/05-testing/`
- **ADRs**: `docs/01-architecture/adr/`

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Missing secrets | `cd infra/secrets && pwsh setup-secrets.ps1` |
| DB connection | `docker compose logs postgres && dotnet ef database update` |
| Build fails (FE) | `rm -rf .next && pnpm build` |
| Build fails (BE) | `dotnet clean && dotnet build` |
| Testhost blocking | `tasklist \| grep testhost` → `taskkill //PID <PID> //F` |
| Port conflict | `netstat -ano \| findstr :8080` → `taskkill /PID <PID> /F` |

## AI Assistant Context

### Development Checklist

| Phase | Action |
|-------|--------|
| **Planning** | Read Bounded Context README → Review ADRs |
| **Implementation** | Domain first → CQRS (Command/Handler) → Endpoint → Tests |
| **Validation** | 🔴 MediatR only (no direct service injection) |
| **Quality** | 90%+ backend coverage, 85%+ frontend |

### Key Patterns

| Pattern | Implementation |
|---------|---------------|
| **Soft Delete** | `IsDeleted` + `DeletedAt` + `HasQueryFilter(e => !e.IsDeleted)` |
| **Audit** | `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` |
| **Concurrency** | `[Timestamp] byte[] RowVersion` + catch `DbUpdateConcurrencyException` |

### DDD Rules

- ✅ Entities: Private setters, factory methods
- ✅ Value Objects: Immutable, validation in factory
- ✅ Repos: Interfaces in Domain, implementation in Infrastructure
- ❌ Domain services directly in endpoints
- ❌ Shared models between commands/queries

### Recent Learnings (Issues)

| Issue | Learning |
|-------|----------|
| #2567 | Endpoint flow: DTOs → Queries → Commands → Validators → Handlers → Routing |
| #2568 | Exceptions: `ConflictException` (409), `NotFoundException` (404), NOT `InvalidOperationException` (500) |
| #2565 | DI: Register both `IService` + implementation |
| #2593 | Tests: Kill testhost first, culture-independent formatting `$"{val*100:0}%"` |
| #2600 | OAuth: Defensive validation + InMemory transaction + manual rollback |
| #2620 | FK constraints: Seed dependent entities first | HybridCache requires `IHybridCacheService` for event handlers |

---

**Last Updated**: 2026-02-02
**License**: Proprietary

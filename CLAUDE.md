# MeepleAI Monorepo - Developer Guide

**AI board game assistant: RAG, multi-agent, living docs**

## Quick Reference

| Task | Command | Location |
|------|---------|----------|
| Start Dev (full) | `make dev` | `infra/` |
| Start Dev (core) | `make dev-core` | `infra/` |
| Start Integration | `make tunnel && make integration` | `infra/` |
| Deploy Staging | `make staging` | `infra/` (on server) |
| Setup Secrets | `make secrets-dev` | `infra/` |
| Start API (no Docker) | `dotnet run` | `apps/api/src/Api/` |
| Start Web (no Docker) | `pnpm dev` | `apps/web/` |
| Run Tests | `dotnet test` / `pnpm test` | Root of each app |
| Migration | `dotnet ef migrations add Name` | `apps/api/src/Api/` |
| API Docs | http://localhost:8080/scalar/v1 | Browser |
| All Make commands | `make help` | `infra/` |

## Stack & Features

**Backend** (.NET 9): ASP.NET Minimal APIs + MediatR | PostgreSQL 16 + EF Core | Qdrant + Redis | FluentValidation | xUnit + Testcontainers

**Frontend** (Next.js 16): App Router + React 19 | Tailwind 4 + shadcn/ui | Zustand + React Query | Vitest + Playwright

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

### DDD Bounded Contexts (13)

| Context | Responsibility |
|---------|---------------|
| Administration | Users, roles, audit, analytics |
| Authentication | Auth flows, sessions, OAuth, 2FA |
| BusinessSimulations | Ledger entries, cost scenarios, resource forecasts |
| DocumentProcessing | PDF upload, extraction, chunking |
| Gamification | Achievements, badges, leaderboards |
| GameManagement | Catalog, sessions, FAQs, specs |
| KnowledgeBase | RAG, AI agents, chat, vector search |
| SessionTracking | Session notes, scoring, activity tracking |
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
cd ../../infra && make secrets-dev

# 3. Frontend env
cd ../apps/web && cp .env.development.example .env.local

# 4. Start services (Docker)
cd ../../infra && make dev            # All services
# OR: make dev-core                   # Core only (no AI/monitoring)
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

### S3 Storage Configuration

**Storage Providers**: Local filesystem (default) | S3-compatible (Cloudflare R2, AWS S3, Backblaze B2, MinIO)

**Implementation**: Factory pattern selects provider via `STORAGE_PROVIDER` env var
- `local` (default): Filesystem storage in `pdf_uploads/`
- `s3`: S3-compatible object storage (requires `storage.secret`)

**S3 Configuration** (`infra/secrets/storage.secret`):
```bash
STORAGE_PROVIDER=s3                  # Enable S3 storage
S3_ENDPOINT=https://xxx.r2.cloudflarestorage.com
S3_ACCESS_KEY=your_access_key
S3_SECRET_KEY=your_secret_key
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=auto                       # "auto" for R2, or AWS region
S3_PRESIGNED_URL_EXPIRY=3600         # Download URL expiry (seconds)
S3_FORCE_PATH_STYLE=false            # true for MinIO
```

**Features**: Server-side encryption (AES256) | Pre-signed URLs | Path traversal protection | Multi-provider support

**Service**: `IBlobStorageService` → `BlobStorageServiceFactory` → `S3BlobStorageService` or `BlobStorageService`

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

*Full examples: [docs/development/README.md](./docs/development/README.md)*

### TypeScript Frontend

**Naming**: PascalCase (components/types) | camelCase (functions/vars) | UPPER_SNAKE_CASE (constants)

**Key Patterns**:
- **Component**: Typed props + explicit JSX.Element return
- **Store**: Zustand with TypeScript interface

*Full examples: [docs/development/README.md](./docs/development/README.md)*

## Testing

### Backend (Target: 90%+)

**Stats**: 930+ test classes | 13,134+ tests | Unit 76% | Integration 22% | E2E/Security/Perf 2%

```bash
cd apps/api/src/Api
dotnet test                                         # All
dotnet test --filter "Category=Unit"                # Unit only
dotnet test --filter "BoundedContext=GameManagement"  # By context
dotnet test /p:CollectCoverage=true                 # With coverage
```

**Test Patterns**: [docs/testing/backend/backend-testing-patterns.md](./docs/testing/backend/backend-testing-patterns.md)

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

**Infra** (from `infra/`):
```bash
make dev                  # Start all local services
make dev-core             # Start core only (postgres, redis, qdrant, api, web)
make dev-down             # Stop dev
make tunnel               # SSH tunnel for integration env
make integration          # Local code + remote services
make staging              # Deploy staging (on server)
make logs s=api           # View service logs
make help                 # Show all commands
```

**Docker Quick Reference**: See [docs/deployment/](./docs/deployment/)
- **Quick Start**: [docker-quickstart.md](./docs/deployment/docker-quickstart.md)
- **Services**: [docker-services.md](./docs/deployment/docker-services.md)
- **Cheatsheet**: [deployment-cheatsheet.md](./docs/deployment/deployment-cheatsheet.md)

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
- **Dev Guides**: `docs/development/`, `docs/api/`, `docs/testing/`
- **ADRs**: `docs/architecture/adr/`
- **Deployment**: `docs/deployment/`

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

### Card Components

- Use `MeepleCard` for all entity displays (games, players, collections, events)
- Do NOT use deprecated `GameCard` or `PlayerCard` components
- Entity types: game (orange), player (purple), collection (teal), event (rose)
- Variants: grid (default), list, compact, featured, hero

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

<MeepleCard
  entity="game"
  variant="grid"
  title={game.title}
  subtitle={game.publisher}
  imageUrl={game.imageUrl}
  rating={game.averageRating}
  ratingMax={10}
/>
```

**Docs**: [docs/frontend/meeple-card-v2-design-tokens.md](./docs/frontend/meeple-card-v2-design-tokens.md)

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

**Last Updated**: 2026-02-18
**License**: Proprietary

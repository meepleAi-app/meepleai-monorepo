# MeepleAI Monorepo - Developer Guide

**AI board game assistant: RAG, multi-agent, living docs**

## Overview

**MeepleAI** features:
- RAG: Hybrid retrieval (vector+keyword) w/ multi-model validation
- AI Agents: Rules, strategy, setup assistance
- PDF: Layout analysis + multilingual OCR
- Game Catalog: Community DB w/ soft-delete + audit
- Real-time: SSE streaming + CQRS

## Stack

**Backend** (.NET 9): ASP.NET Minimal APIs + MediatR | PostgreSQL 16 + EF Core | Qdrant + Redis | FluentValidation | xUnit + Testcontainers

**Frontend** (Next.js 14): App Router + React 18 | Tailwind + shadcn/ui | Zustand + React Query | Vitest + Playwright

**AI** (Python): sentence-transformers | cross-encoder | Unstructured | SmolDocling

**Infra**: Docker Compose | Traefik | Grafana + Prometheus | GitHub Actions

## Architecture

### DDD Bounded Contexts (9)

```
BoundedContexts/
├── Administration/       # Users, roles, audit, analytics
├── Authentication/       # Auth flows, sessions, OAuth, 2FA
├── DocumentProcessing/   # PDF upload, extraction, chunking
├── GameManagement/       # Catalog, sessions, FAQs, specs
├── KnowledgeBase/        # RAG, AI agents, chat, vector search
├── SharedGameCatalog/    # Community DB w/ soft-delete
├── SystemConfiguration/  # Runtime config, flags
├── UserLibrary/          # Collections, wishlist, history
├── UserNotifications/    # Alerts, email, push
└── WorkflowIntegration/  # n8n, webhooks, logging
```

**Layers**: Domain (entities, value objects, repos) → Application (commands, queries, handlers, validators) → Infrastructure (persistence, services)

### CQRS Pattern

**CRITICAL**: Endpoints use ONLY `IMediator.Send()` - ZERO direct service injection

```csharp
// ✅ CORRECT
app.MapPost("/api/v1/auth/register", async (RegisterCommand cmd, IMediator m) =>
    Results.Ok(await m.Send(cmd)));

// ❌ FORBIDDEN - Direct service injection
app.MapPost("/api/v1/auth/register", async (RegisterCommand cmd, IAuthService svc) => ...);
```

## Secret Management

**System**: `.secret` files (Issue #2570 - Consolidated 2026-01-17)

```yaml
# docker-compose.yml pattern
service:
  env_file:
    - ./secrets/service-name.secret  # KEY=VALUE format
```

### Files (10 total)

**CRITICAL** (blocks startup):
- database.secret, redis.secret, qdrant.secret, jwt.secret, admin.secret, embedding-service.secret

**IMPORTANT** (warns):
- openrouter.secret, unstructured-service.secret, bgg.secret

**OPTIONAL**:
- oauth.secret, email.secret, monitoring.secret, n8n.secret, storage.secret, traefik.secret, smoldocling/reranker-service.secret

### Workflow

```bash
# Initial setup (auto-generates JWT, passwords, API keys)
cd infra/secrets && pwsh setup-secrets.ps1 -SaveGenerated

# Update secret
nano infra/secrets/redis.secret
cd infra && docker compose restart redis

# Development sync
# .env.development (repo root) manually synced with .secret files
```

**Security Rules**:
✅ DO: Use `.secret` as source of truth, run `setup-secrets.ps1`, gitignore all `.secret`, rotate 90d
❌ DON'T: Commit `.secret`/`.env`, include real values in docs, use dev secrets in prod

**Validation**: `SecretLoader.cs` validates at startup (3 levels: Critical/Important/Optional)

## Development

### PowerShell Preference

**CRITICAL**: Always use `pwsh -c "command"` for Docker operations on Windows, NOT bash directly.

```bash
# ✅ CORRECT
pwsh -c "docker logs meepleai-api --tail=50"
pwsh -c "cd infra; docker compose ps"

# ❌ WRONG
docker logs meepleai-api | grep pattern  # bash pipe issues on Windows
```

### Quick Start

```bash
# Clone & deps
git clone <repo> && cd meepleai-monorepo-dev
cd apps/api/src/Api && dotnet restore
cd ../../../web && pnpm install

# Secrets (auto-gen saves 15-30min)
cd ../../infra/secrets && .\setup-secrets.ps1 -SaveGenerated

# Frontend env
cd ../../apps/web && cp .env.development.example .env.local

# Start infra
cd ../../infra && docker compose up -d postgres qdrant redis

# Backend (Terminal 1)
cd ../apps/api/src/Api && dotnet run  # http://localhost:8080

# Frontend (Terminal 2)
cd ../../../web && pnpm dev  # http://localhost:3000
```

### Git Workflow

**Branches**: `main-dev` (dev) | `main` (prod) | `feature/issue-{n}-{desc}` | `hotfix/{desc}`

**Commits** (Conventional): `feat(scope): desc` | `fix` | `docs` | `refactor` | `test` | `chore`

```bash
# Feature flow
git checkout main-dev && git pull
git checkout -b feature/issue-123-add-search
git add . && git commit -m "feat(game): add complexity filter"
cd apps/api/src/Api && dotnet test
cd ../../../web && pnpm typecheck && pnpm lint && pnpm test
git push -u origin feature/issue-123-add-search
# Create PR → merge → cleanup
git checkout main-dev && git pull && git branch -D feature/issue-123-add-search
```

### Add Feature Pattern

**Flow**: Domain → Application (Command/Query + Validator + Handler) → Endpoint → Tests

**Example** (MarkGameAsPlayed):

1. **Domain**: `Game.MarkAsPlayed() { PlayCount++; LastPlayedAt = DateTime.UtcNow; }`
2. **Application**: `MarkGameAsPlayedCommand(Guid GameId)` + Validator + Handler
3. **Endpoint**: `app.MapPut("/games/{id}/mark-played", async (Guid id, IMediator m) => ...)`
4. **Tests**: Unit (domain logic) + Integration (DB persistence) + E2E (HTTP flow)

### Migrations

```bash
cd apps/api/src/Api
dotnet ef migrations add AddPlayCountToGames
dotnet ef database update
```

**Best practices**: Descriptive names, review SQL, test dev first, rollback strategy, never delete old migrations

## Code Standards

### C# Backend

**Naming**: PascalCase (classes, methods, properties) | camelCase with `_` (private fields, params) | `I` prefix (interfaces)

**Patterns**:
```csharp
// Entity: private setters + factory
public class Game {
    public Guid Id { get; private set; }
    private Game() { }
    public static Game Create(string name) => new() { Id = Guid.NewGuid(), Name = name };
}

// Value Object: immutable + validation
public record Email {
    public string Value { get; init; }
    public static Email Create(string v) {
        if (!v.Contains('@')) throw new ArgumentException("Invalid");
        return new Email { Value = v.ToLower() };
    }
}

// Exceptions: domain-specific
public class GameNotFoundException : Exception {
    public Guid GameId { get; }
    public GameNotFoundException(Guid id) : base($"Game {id} not found") => GameId = id;
}
```

### TypeScript Frontend

**Naming**: PascalCase (components, types) | camelCase (functions, vars) | UPPER_SNAKE_CASE (constants)

**Patterns**:
```typescript
// Component: typed + explicit return
interface GameCardProps {
  game: GameData;
  onPlay: (id: string) => void;
}
export function GameCard({ game, onPlay }: GameCardProps): JSX.Element {
  return <div className="border p-4"><h3>{game.name}</h3></div>;
}

// Zustand store: typed
interface GameStore {
  games: GameData[];
  fetchGames: () => Promise<void>;
}
export const useGameStore = create<GameStore>((set) => ({
  games: [],
  fetchGames: async () => set({ games: await fetch('/api/v1/games').then(r => r.json()) })
}));
```

## Testing

**Backend** (xUnit + Testcontainers) - Target: 90%+ | **737 test files** | **8,630+ tests**
- Unit: 70% (domain logic, fast, isolated)
- Integration: 25% (DB, handlers, full flows)
- E2E: 5% (critical journeys)

```bash
cd apps/api/src/Api
dotnet test                                    # All tests
dotnet test --filter "Category=Unit"           # Unit only
dotnet test --filter "Category=Integration"    # Integration only
dotnet test --filter "BoundedContext=GameManagement"  # By context
dotnet test /p:CollectCoverage=true            # With coverage
```

### Backend Test Patterns

**Handler Test** (mock dependencies):
```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "YourContext")]
public class YourHandlerTests
{
    private readonly Mock<IRepository> _mockRepo;
    private readonly YourHandler _handler;

    public YourHandlerTests()
    {
        _mockRepo = new Mock<IRepository>();
        _handler = new YourHandler(_mockRepo.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsResult()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity());

        // Act
        var result = await _handler.Handle(new Command(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _mockRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**Integration Test** (Testcontainers):
```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public class RepoIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        var connString = await _fixture.CreateIsolatedDatabaseAsync($"test_{Guid.NewGuid():N}");
        // Setup DbContext and run migrations
    }

    [Fact]
    public async Task AddAsync_PersistsEntity()
    {
        await _repository.AddAsync(entity, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear(); // Fresh read
        // Assert retrieval
    }
}
```

**Docs**: `docs/05-testing/backend/backend-testing-patterns.md`, `docs/05-testing/backend/test-data-builders.md`

**Frontend** (Vitest + Playwright) - Target: 85%+

```bash
cd apps/web
pnpm test && pnpm test:coverage     # Unit (Vitest)
pnpm test:e2e && pnpm test:e2e:ui   # E2E (Playwright)
pnpm typecheck && pnpm lint         # Quality checks
```

## Common Commands

**Backend**:
```bash
dotnet build && dotnet run
dotnet ef migrations add Name && dotnet ef database update
```

**Frontend**:
```bash
pnpm dev && pnpm build && pnpm start
pnpm generate:api  # From OpenAPI spec
```

**Infra**:
```bash
docker compose up -d postgres qdrant redis  # Core
docker compose logs -f api
docker compose down -v  # Reset (data loss!)
```

**Docker Quick Reference**: See [docs/02-development/docker/](./docs/02-development/docker/)
- **Quick Start** (5min): [quick-start.md](./docs/02-development/docker/quick-start.md)
- **All Endpoints**: [service-endpoints.md](./docs/02-development/docker/service-endpoints.md)
- **Clean Builds**: [clean-builds.md](./docs/02-development/docker/clean-builds.md)
- **Commands**: [common-commands.md](./docs/02-development/docker/common-commands.md)
- **Troubleshooting**: [troubleshooting.md](./docs/02-development/docker/troubleshooting.md)

## Project Structure

```
apps/
├── api/src/Api/          # .NET 9: BoundedContexts/, Routing/, Infrastructure/
├── web/                  # Next.js 14: src/app/, components/, lib/, __tests__/
├── embedding-service/    # Python: embeddings
├── reranker-service/     # Python: reranking
└── {smoldocling,unstructured}-service/  # Python: PDF/docs

docs/                     # Architecture, dev guides, API ref, deployment
infra/                    # docker-compose.yml, traefik/, monitoring/, secrets/
tests/Api.Tests/          # Backend test suite
.github/workflows/        # CI/CD: backend-ci, frontend-ci, e2e-tests
```

## Docs Reference

- **API**: http://localhost:8080/scalar/v1
- **Docker Guides**: `docs/02-development/docker/` (Quick start, endpoints, troubleshooting)
- **Dev Guides**: `docs/02-development/`, `docs/03-api/`, `docs/05-testing/`
- **ADRs**: `docs/01-architecture/adr/`

## Troubleshooting

**Missing secrets**: `cd infra/secrets && pwsh setup-secrets.ps1`

**DB connection**: `docker compose logs postgres && dotnet ef database update`

**Build fails**: `rm -rf .next && pnpm build` (frontend) | `dotnet clean && dotnet build` (backend)

**Testhost blocking** (#2593): `tasklist | grep testhost` → `taskkill //PID <PID> //F`

**Port conflict**: `netstat -ano | findstr :8080` → `taskkill /PID <PID> /F`

## AI Context for Claude

### Feature Development

1. **Domain first**: Read Bounded Context README
2. **CQRS**: Command → Handler → Endpoint → Tests (TDD)
3. **MediatR only**: Endpoints NEVER inject services directly
4. **Test coverage**: 90%+ backend, 85%+ frontend
5. **Check ADRs**: Review decisions before changes

### Code Analysis

1. **DDD**: Entities private setters, value objects immutable, repo interfaces in Domain
2. **CQRS**: Commands vs Queries separated, no shared models
3. **Validation**: FluentValidation on all commands/queries
4. **Security**: Semgrep + detect-secrets compliance

### Common Patterns

**Soft Delete**: `IsDeleted` + `DeletedAt` + `HasQueryFilter(e => !e.IsDeleted)`

**Audit**: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`

**Concurrency**: `[Timestamp] byte[] RowVersion` + catch `DbUpdateConcurrencyException`

### Recent Learnings (Condensed)

**Endpoints** (#2567): DTOs → Queries → Commands → Validators → Handlers → Routing → Register

**Exceptions** (#2568): Use `ConflictException` (409), `NotFoundException` (404), NOT `InvalidOperationException` (500)

**DI** (#2565): Register both `IService` + implementation: `services.AddScoped<IGame, Game>()`

**Secrets** (#2565): Rotate exposed credentials, redact docs, use `[REDACTED]` examples

**Code Review** (#2568): `/code-review:code-review <pr>` → 5 parallel agents → confidence scoring ≥80

**Docker**: Double `$$` for env vars in shell commands (compose expansion)

**Tests** (#2593): Kill testhost before tests, culture-independent formatting (`$"{val*100:0}%"`)

**OAuth** (#2600): Defensive validation + InMemory transaction handling + manual rollback

**Tests/FK** (#2620): Seed dependent entities before referencing (FK constraints), HybridCache + IHybridCacheService required for event handlers, `ChangeTracker.Clear()` > `Entry().State = Detached`

---

**Last Updated**: 2026-01-19
**License**: Proprietary

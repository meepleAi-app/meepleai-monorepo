# MeepleAI Monorepo - Developer Guide for Claude

**AI-powered board game assistant with RAG, multi-agent systems, and living documentation**

---

## Project Overview

**MeepleAI** is an intelligent board game assistant that helps users understand and play board games through:
- **RAG System**: Hybrid retrieval (vector + keyword) with multi-model validation
- **AI Agents**: Specialized agents for rules clarification, strategy tips, and game setup
- **PDF Processing**: Advanced PDF extraction with layout analysis and multi-language OCR
- **Shared Game Catalog**: Community-driven game database with soft-delete and audit trails
- **Real-time Chat**: SSE streaming with CQRS patterns for scalable interactions

---

## Tech Stack

### Backend (.NET 9 C#)
- **Framework**: ASP.NET Core Minimal APIs + MediatR CQRS
- **Database**: PostgreSQL 16+ (EF Core migrations)
- **Vector DB**: Qdrant for embeddings
- **Cache**: Redis (distributed caching + rate limiting)
- **Validation**: FluentValidation + 5-layer validation system
- **Testing**: xUnit + Testcontainers + Moq + FluentAssertions

### Frontend (Next.js 14 TypeScript)
- **Framework**: Next.js 14 App Router + React 18
- **Styling**: Tailwind CSS + shadcn/ui components
- **State**: Zustand + React Query
- **Forms**: React Hook Form + Zod validation
- **Testing**: Vitest + Testing Library + Playwright (E2E)
- **Storybook**: Component library with Chromatic visual testing

### AI Services (Python)
- **Embedding**: sentence-transformers multilingual models
- **Reranking**: cross-encoder reranking for precision
- **Unstructured**: PDF processing with layout analysis
- **SmolDocling**: Document intelligence extraction

### Infrastructure
- **Containerization**: Docker + Docker Compose
- **Reverse Proxy**: Traefik with automatic SSL
- **Monitoring**: Grafana + Prometheus + OpenTelemetry
- **CI/CD**: GitHub Actions with parallel test execution
- **Secrets**: detect-secrets + Semgrep for security scanning

---

## Architecture

### DDD Bounded Contexts

**Pattern**: Each context is self-contained with Domain → Application → Infrastructure layers

```
apps/api/src/Api/BoundedContexts/
├── Administration/          # Users, roles, audit logs, system analytics, alerts
├── Authentication/          # Auth flows, sessions, API keys, OAuth, 2FA
├── DocumentProcessing/      # PDF upload, extraction, chunking, validation
├── GameManagement/          # Games catalog, play sessions, FAQs, rule specs
├── KnowledgeBase/           # RAG system, AI agents, chat threads, vector search
├── SharedGameCatalog/       # Community game database with soft-delete
├── SystemConfiguration/     # Runtime config, feature flags, environment settings
├── UserNotifications/       # In-app alerts, email notifications, push notifications
└── WorkflowIntegration/     # n8n workflows, webhooks, error logging
```

**Layer Responsibilities**:
```
Domain/                # Pure business logic, entities, value objects, domain events
├── Entities/          # Aggregate roots with identity and lifecycle
├── ValueObjects/      # Immutable objects without identity (Email, Money, etc.)
├── Repositories/      # Interface contracts (implementation in Infrastructure)
├── Services/          # Domain services for multi-entity operations
└── Events/            # Domain events for bounded context communication

Application/           # Use cases, commands, queries (CQRS pattern)
├── Commands/          # Write operations (Create, Update, Delete)
├── Queries/           # Read operations (Get, List, Search)
├── Handlers/          # MediatR handlers for commands and queries
├── DTOs/              # Data transfer objects for API contracts
└── Validators/        # FluentValidation rules for commands/queries

Infrastructure/        # External concerns, persistence, integrations
├── Persistence/       # EF Core repositories, configurations, mappers
├── Services/          # External API clients, email senders, etc.
└── DependencyInjection/ # Service registration and configuration
```

### CQRS Pattern (MediatR)

**ALL HTTP endpoints use ONLY IMediator.Send() - ZERO direct service dependencies**

```csharp
// ✅ CORRECT - Routing/AuthenticationEndpoints.cs
app.MapPost("/api/v1/auth/register", async (
    RegisterCommand command,
    IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return Results.Ok(result);
});

// ❌ WRONG - Never inject services directly in endpoints
app.MapPost("/api/v1/auth/register", async (
    RegisterCommand command,
    IAuthService authService) => { }); // FORBIDDEN
```

**Benefits**:
- Decoupling between HTTP layer and business logic
- Testable handlers without HTTP concerns
- Consistent request/response flow
- Centralized validation and error handling

---

## Secret Management

**System**: Single source of truth using `.secret` files (Issue #2570 - Consolidated 2026-01-17)

### Architecture

**All services use .secret files via env_file** (no Docker secrets, no .txt files):

```yaml
# docker-compose.yml pattern
service:
  env_file:
    - ./secrets/service-name.secret  # Multi-variable KEY=VALUE format
```

**File Structure**:
```
infra/secrets/
├── *.secret           # Real values (gitignored, NEVER commit)
├── *.secret.example   # Templates (committed, safe)
└── setup-secrets.ps1  # ONLY script (generates .secret from examples)
```

### Secret Files (10 total)

**CRITICAL** (startup blocked if missing):
- `database.secret` - POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
- `redis.secret` - REDIS_PASSWORD
- `qdrant.secret` - QDRANT_API_KEY
- `jwt.secret` - JWT_SECRET_KEY, JWT_ISSUER, JWT_AUDIENCE
- `admin.secret` - ADMIN_EMAIL, ADMIN_PASSWORD, ADMIN_DISPLAY_NAME
- `embedding-service.secret` - EMBEDDING_SERVICE_API_KEY

**IMPORTANT** (warnings if missing):
- `openrouter.secret` - OPENROUTER_API_KEY, OPENROUTER_DEFAULT_MODEL
- `unstructured-service.secret` - UNSTRUCTURED_API_KEY
- `bgg.secret` - BGG_USERNAME, BGG_PASSWORD (optional for dev)

**OPTIONAL**:
- `oauth.secret` - GOOGLE_*, GITHUB_*, DISCORD_* (6 OAuth credentials)
- `email.secret` - SMTP_*, GMAIL_APP_PASSWORD
- `monitoring.secret` - GRAFANA_ADMIN_PASSWORD, PROMETHEUS_PASSWORD, SLACK_WEBHOOK_URL
- `n8n.secret` - N8N_ENCRYPTION_KEY, N8N_BASIC_AUTH_PASSWORD
- `storage.secret` - S3_* (if using cloud storage)
- `traefik.secret` - TRAEFIK_DASHBOARD_*
- `smoldocling-service.secret` - SMOLDOCLING_API_KEY
- `reranker-service.secret` - RERANKER_API_KEY

### Workflow

**Initial Setup**:
```bash
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated
# Creates all .secret files with auto-generated secure values
```

**Update Secret**:
```bash
# 1. Edit .secret file
nano infra/secrets/redis.secret

# 2. Restart service
cd infra
docker compose restart redis

# Done! No manual sync needed
```

**Development** (.env.development):
- Located in repo root
- Loaded by Program.cs via DotNetEnv
- ⚠️ Manually synchronized with .secret files
- ✅ Gitignored (safe)

### Security Rules

**✅ DO**:
- Use `.secret` files as single source of truth
- Run `setup-secrets.ps1` for initial generation
- Keep all `.secret` files gitignored
- Rotate passwords every 90 days
- Sync `.env.development` manually after `.secret` changes

**❌ DON'T**:
- NEVER commit `.secret` files
- NEVER commit `.env` files
- NEVER include real secret values in documentation
- NEVER use development secrets in production
- NEVER create `.txt` files manually (deprecated system)

### Backend Validation

**SecretLoader.cs** automatically validates all secrets at startup:
- Location: `apps/api/src/Api/Infrastructure/Configuration/SecretLoader.cs`
- Definitions: `SecretDefinitions.cs` (3-level validation)
- Levels: Critical (blocks startup), Important (warns), Optional (info)

**Example Output**:
```
[INF] Secret validation complete: 17 loaded, 0 critical missing, 2 optional missing
```

### Troubleshooting

**Missing secrets**:
```bash
# Check which secrets are missing
cd apps/api/src/Api
dotnet run
# Logs will show: "CRITICAL secrets missing: database.secret:POSTGRES_PASSWORD"

# Fix: Run setup script
cd ../../../infra/secrets
pwsh setup-secrets.ps1
```

**Service fails after secret update**:
```bash
# Recreate service to reload env_file
cd infra
docker compose up -d --force-recreate service-name
```

**Password contains special characters causing issues**:
- Avoid `;`, `'`, `"` in passwords (breaks connection strings)
- Use alphanumeric + basic symbols: `!@#$%^&*()-_=+`
- Generate with: `openssl rand -base64 16 | tr -d '/+='`

### References

- **System Documentation**: `docs/claudedocs/secret-system-final.md`
- **Audit Report**: `docs/claudedocs/secret-audit-2026-01-17.md`
- **Consolidation Analysis**: `docs/claudedocs/issue-2565-secret-consolidation-analysis.md`
- **Implementation**: Issue #2570, PR #2572 (merged 2026-01-17)

---

## Development Workflow

### Local Setup

**Prerequisites**:
- .NET 9 SDK
- Node.js 20+ (with pnpm: `npm install -g pnpm`)
- Docker Desktop
- Git

**Quick Start**:
```bash
# 1. Clone repository
git clone <repo-url>
cd meepleai-monorepo-dev

# 2. Backend dependencies (auto-restore on build)
cd apps/api/src/Api
dotnet restore

# 3. Frontend dependencies
cd ../../../web
pnpm install

# 4. Environment configuration

# 4a. Auto-generate secrets (RECOMMENDED - saves 15-30 minutes)
cd ../../infra/secrets
.\setup-secrets.ps1 -SaveGenerated
# Generates: JWT keys, database passwords, API keys (11 values)
# Creates backup: .generated-values-TIMESTAMP.txt
# Manual config still needed: bgg.secret, openrouter.secret (optional)

# 4b. Frontend environment
cd ../../apps/web
cp .env.development.example .env.local
# Edit .env.local if custom API endpoints needed

# 5. Start infrastructure
cd ../../infra
docker compose up -d postgres qdrant redis

# 6. Run backend (Terminal 1)
cd ../apps/api/src/Api
dotnet run
# API: http://localhost:8080
# Swagger: http://localhost:8080/scalar/v1

# 7. Run frontend (Terminal 2)
cd ../../../web
pnpm dev
# Web: http://localhost:3000
```

### Git Workflow

**Branch Strategy**:
- `main-dev`: Development branch (default)
- `main`: Production branch (stable releases)
- `feature/issue-{number}-{description}`: Feature branches
- `hotfix/{description}`: Emergency fixes for production

**Commit Messages** (Conventional Commits):
```bash
# Format: <type>(<scope>): <description>
feat(knowledge-base): add multi-model validation for RAG responses
fix(auth): resolve session expiration edge case
chore(deps): update FluentValidation to 12.1.1
docs(api): update authentication endpoint examples
test(game-management): add integration tests for FAQ search
refactor(shared-catalog): simplify soft-delete query filters
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`

**Feature Development Process**:
```bash
# 1. Create feature branch from main-dev
git checkout main-dev
git pull origin main-dev
git checkout -b feature/issue-123-add-game-search

# 2. Develop feature with incremental commits
git add .
git commit -m "feat(game-management): add search by complexity filter"

# 3. Run tests and linting
cd apps/api/src/Api
dotnet test
cd ../../../web
pnpm typecheck && pnpm lint && pnpm test

# 4. Push and create PR
git push -u origin feature/issue-123-add-game-search
# Create PR via GitHub UI with description and screenshots

# 5. After PR merge, delete local branch
git checkout main-dev
git pull origin main-dev
git branch -D feature/issue-123-add-game-search
```

### Adding a New Feature

**Pattern**: Domain → Application (Command/Query) → Handler → Endpoint → Tests

**Example: Add "MarkGameAsPlayed" feature**

**1. Domain Layer**:
```csharp
// BoundedContexts/GameManagement/Domain/Entities/Game.cs
public class Game
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public int PlayCount { get; private set; }
    public DateTime? LastPlayedAt { get; private set; }

    public void MarkAsPlayed()
    {
        PlayCount++;
        LastPlayedAt = DateTime.UtcNow;
    }
}
```

**2. Application Layer - Command**:
```csharp
// BoundedContexts/GameManagement/Application/Commands/MarkGameAsPlayedCommand.cs
public record MarkGameAsPlayedCommand(Guid GameId) : IRequest<GameDto>;

// Validator
public class MarkGameAsPlayedCommandValidator : AbstractValidator<MarkGameAsPlayedCommand>
{
    public MarkGameAsPlayedCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
    }
}

// Handler
public class MarkGameAsPlayedCommandHandler : IRequestHandler<MarkGameAsPlayedCommand, GameDto>
{
    private readonly MeepleAiDbContext _db;

    public MarkGameAsPlayedCommandHandler(MeepleAiDbContext db) => _db = db;

    public async Task<GameDto> Handle(MarkGameAsPlayedCommand request, CancellationToken ct)
    {
        var game = await _db.Games.FindAsync(new object[] { request.GameId }, ct)
            ?? throw new NotFoundException($"Game {request.GameId} not found");

        game.MarkAsPlayed();
        await _db.SaveChangesAsync(ct);

        return new GameDto { Id = game.Id, Name = game.Name, PlayCount = game.PlayCount };
    }
}
```

**3. HTTP Endpoint**:
```csharp
// Routing/GameEndpoints.cs
public static class GameEndpoints
{
    public static void MapGameEndpoints(this WebApplication app)
    {
        app.MapPut("/api/v1/games/{gameId:guid}/mark-as-played",
            async (Guid gameId, IMediator mediator) =>
        {
            var result = await mediator.Send(new MarkGameAsPlayedCommand(gameId));
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("MarkGameAsPlayed")
        .WithTags("Games")
        .Produces<GameDto>(200)
        .Produces(401)
        .Produces(404);
    }
}
```

**4. Tests**:
```csharp
// tests/Api.Tests/GameManagement/MarkGameAsPlayedTests.cs
public class MarkGameAsPlayedTests : IntegrationTestBase
{
    [Fact]
    public async Task MarkAsPlayed_ShouldIncrementPlayCount()
    {
        // Arrange
        var game = await CreateTestGame("Catan");
        var command = new MarkGameAsPlayedCommand(game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.PlayCount.Should().Be(1);
        var updated = await DbContext.Games.FindAsync(game.Id);
        updated.LastPlayedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkAsPlayed_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var command = new MarkGameAsPlayedCommand(Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await Mediator.Send(command));
    }
}
```

### Database Migrations

**Creating Migrations**:
```bash
cd apps/api/src/Api

# Add migration
dotnet ef migrations add AddPlayCountToGames

# Review migration file in Migrations/ directory

# Apply migration
dotnet ef database update
```

**Migration Best Practices**:
- Name migrations descriptively: `AddPlayCountToGames`, `CreateGameFAQsTable`
- Review generated SQL before applying
- Test migrations on development database first
- Include rollback strategy for production migrations
- Never delete old migrations (breaks history)

---

## Code Standards

### C# Backend Standards

**Naming Conventions**:
- **PascalCase**: Classes, methods, properties, public fields
- **camelCase**: Private fields (with `_` prefix), parameters, local variables
- **Interfaces**: Prefix with `I` (e.g., `IGameRepository`)

```csharp
// ✅ CORRECT
public class GameService
{
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<GameService> _logger;

    public async Task<GameDto> GetGameAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        return MapToDto(game);
    }
}

// ❌ WRONG
public class game_service  // Wrong casing
{
    public GameRepository gameRepository;  // Should be private with _ prefix
    public async Task<GameDto> get_game(Guid GameId) { }  // Wrong casing
}
```

**Entity Patterns**:
```csharp
// ✅ Aggregate Root with private setters and factory methods
public class Game
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int PlayCount { get; private set; }

    private Game() { }  // EF Core constructor

    public static Game Create(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        PlayCount = 0
    };

    public void MarkAsPlayed() => PlayCount++;
}

// ❌ WRONG - Public setters break encapsulation
public class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; }  // Anyone can modify
    public int PlayCount { get; set; }  // No business logic control
}
```

**Value Objects**:
```csharp
// ✅ Immutable value object with validation
public record Email
{
    public string Value { get; init; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format");
        return new Email(value.ToLowerInvariant());
    }
}

// ❌ WRONG - Mutable, no validation
public class Email
{
    public string Value { get; set; }
}
```

**Error Handling**:
```csharp
// ✅ Domain exceptions for business rule violations
public class GameNotFoundException : Exception
{
    public Guid GameId { get; }
    public GameNotFoundException(Guid gameId)
        : base($"Game with ID {gameId} not found")
    {
        GameId = gameId;
    }
}

// Use in handlers
var game = await _db.Games.FindAsync(gameId)
    ?? throw new GameNotFoundException(gameId);

// ❌ WRONG - Generic exceptions lose context
var game = await _db.Games.FindAsync(gameId)
    ?? throw new Exception("Game not found");
```

### TypeScript Frontend Standards

**Naming Conventions**:
- **PascalCase**: Components, types, interfaces, enums
- **camelCase**: Functions, variables, parameters
- **UPPER_SNAKE_CASE**: Constants

```typescript
// ✅ CORRECT
interface GameData {
  id: string;
  name: string;
  playCount: number;
}

const MAX_GAMES_PER_PAGE = 20;

export function GameList({ games }: { games: GameData[] }) {
  const [selectedGame, setSelectedGame] = useState<GameData | null>(null);

  const handleGameClick = (game: GameData) => {
    setSelectedGame(game);
  };

  return <div>{/* ... */}</div>;
}

// ❌ WRONG
interface game_data { }  // Wrong casing
const maxGamesPerPage = 20;  // Should be UPPER_SNAKE_CASE for constants
export function gameList() { }  // Component should be PascalCase
```

**Component Patterns**:
```typescript
// ✅ Typed component with explicit return
interface GameCardProps {
  game: GameData;
  onPlay: (gameId: string) => void;
}

export function GameCard({ game, onPlay }: GameCardProps): JSX.Element {
  return (
    <div className="rounded-lg border p-4">
      <h3 className="text-lg font-semibold">{game.name}</h3>
      <p className="text-sm text-muted-foreground">
        Played {game.playCount} times
      </p>
      <Button onClick={() => onPlay(game.id)}>Mark as Played</Button>
    </div>
  );
}

// ❌ WRONG - Untyped, implicit return
export function GameCard({ game, onPlay }) {  // Missing types
  return <div>{game.name}</div>;  // Missing accessibility, styling
}
```

**State Management (Zustand)**:
```typescript
// ✅ Typed store with actions
interface GameStore {
  games: GameData[];
  selectedGame: GameData | null;
  fetchGames: () => Promise<void>;
  selectGame: (game: GameData) => void;
}

export const useGameStore = create<GameStore>((set) => ({
  games: [],
  selectedGame: null,
  fetchGames: async () => {
    const response = await fetch('/api/v1/games');
    const games = await response.json();
    set({ games });
  },
  selectGame: (game) => set({ selectedGame: game }),
}));

// Usage
function GameList() {
  const { games, fetchGames } = useGameStore();
  useEffect(() => { fetchGames(); }, [fetchGames]);
  return <div>{/* ... */}</div>;
}
```

---

## Testing Strategy

### Backend Testing (xUnit + Testcontainers)

**Test Pyramid** (90%+ coverage target):
- **Unit Tests**: 70% - Fast, isolated, pure logic
- **Integration Tests**: 25% - Database, handlers, full flows
- **E2E Tests**: 5% - Critical user journeys

**Unit Test Example**:
```csharp
// tests/Api.Tests/GameManagement/Domain/GameTests.cs
public class GameTests
{
    [Fact]
    public void MarkAsPlayed_ShouldIncrementPlayCount()
    {
        // Arrange
        var game = Game.Create("Catan");
        var initialCount = game.PlayCount;

        // Act
        game.MarkAsPlayed();

        // Assert
        game.PlayCount.Should().Be(initialCount + 1);
        game.LastPlayedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
```

**Integration Test Example** (Testcontainers):
```csharp
// tests/Api.Tests/IntegrationTestBase.cs
public class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;
    protected MeepleAiDbContext DbContext { get; private set; }
    protected IMediator Mediator { get; private set; }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        DbContext = CreateDbContext();
        await DbContext.Database.MigrateAsync();
        Mediator = CreateMediator();
    }
}

// tests/Api.Tests/GameManagement/MarkGameAsPlayedIntegrationTests.cs
public class MarkGameAsPlayedIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task MarkAsPlayed_ShouldPersistToDatabase()
    {
        // Arrange
        var game = await CreateTestGame("Catan");
        var command = new MarkGameAsPlayedCommand(game.Id);

        // Act
        await Mediator.Send(command);

        // Assert
        var updated = await DbContext.Games.FindAsync(game.Id);
        updated.PlayCount.Should().Be(1);
    }
}
```

**Running Tests**:
```bash
cd apps/api/src/Api

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~GameTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Frontend Testing (Vitest + Testing Library)

**Unit Test Example**:
```typescript
// apps/web/__tests__/components/GameCard.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { GameCard } from '@/components/GameCard';

describe('GameCard', () => {
  it('should render game name and play count', () => {
    const game = { id: '1', name: 'Catan', playCount: 5 };
    render(<GameCard game={game} onPlay={vi.fn()} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Played 5 times')).toBeInTheDocument();
  });

  it('should call onPlay when button clicked', () => {
    const game = { id: '1', name: 'Catan', playCount: 5 };
    const onPlay = vi.fn();
    render(<GameCard game={game} onPlay={onPlay} />);

    fireEvent.click(screen.getByText('Mark as Played'));
    expect(onPlay).toHaveBeenCalledWith('1');
  });
});
```

**E2E Test Example (Playwright)**:
```typescript
// apps/web/e2e/game-search.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Game Search', () => {
  test('should search and display games', async ({ page }) => {
    await page.goto('/games');

    await page.fill('[placeholder="Search games..."]', 'Catan');
    await page.click('button:has-text("Search")');

    await expect(page.locator('text=Catan')).toBeVisible();
    await expect(page.locator('[data-testid="game-card"]')).toHaveCount(1);
  });

  test('should mark game as played', async ({ page }) => {
    await page.goto('/games');
    await page.click('[data-testid="game-card"]:first-child >> text=Mark as Played');

    await expect(page.locator('text=Play count increased')).toBeVisible();
  });
});
```

**Running Frontend Tests**:
```bash
cd apps/web

# Unit tests (Vitest)
pnpm test                # Run once
pnpm test:watch          # Watch mode
pnpm test:coverage       # With coverage report

# E2E tests (Playwright)
pnpm test:e2e            # Headless mode
pnpm test:e2e:ui         # Interactive UI mode
pnpm test:e2e:report     # View test report

# Type checking
pnpm typecheck

# Linting
pnpm lint
pnpm lint:fix
```

---

## Common Commands

### Backend Commands
```bash
# Build and run
cd apps/api/src/Api
dotnet build
dotnet run

# Tests
dotnet test
dotnet test --filter "FullyQualifiedName~GameTests"
dotnet test /p:CollectCoverage=true

# Database migrations
dotnet ef migrations add MigrationName
dotnet ef database update
dotnet ef database drop --force  # Reset database

# Code generation
dotnet ef dbcontext scaffold "ConnectionString" Npgsql.EntityFrameworkCore.PostgreSQL
```

### Frontend Commands
```bash
cd apps/web

# Development
pnpm dev                 # Start dev server
pnpm build               # Production build
pnpm start               # Start production server
pnpm build:analyze       # Bundle analysis

# Testing
pnpm test                # Unit tests
pnpm test:e2e            # E2E tests
pnpm test:coverage       # Coverage report

# Code quality
pnpm lint                # ESLint
pnpm lint:fix            # Auto-fix issues
pnpm typecheck           # TypeScript check

# API client generation
pnpm generate:api        # Generate from OpenAPI spec
```

### Infrastructure Commands
```bash
cd infra

# Start services
docker compose up -d postgres qdrant redis  # Core services
docker compose up -d                        # All services
docker compose down                         # Stop all

# Logs
docker compose logs -f postgres
docker compose logs -f api

# Reset
docker compose down -v  # Remove volumes (data loss!)
```

---

## Project Structure Reference

```
meepleai-monorepo-dev/
├── apps/
│   ├── api/                        # .NET 9 Backend API
│   │   └── src/Api/
│   │       ├── BoundedContexts/    # DDD contexts (9 domains)
│   │       ├── Extensions/         # ASP.NET configuration extensions
│   │       ├── Routing/            # HTTP endpoints (Minimal APIs)
│   │       ├── Infrastructure/     # EF Core, migrations, configs
│   │       ├── Middleware/         # Custom middleware (auth, logging)
│   │       ├── Models/             # API contracts (DTOs)
│   │       ├── Services/           # Cross-cutting services
│   │       ├── Observability/      # Metrics, tracing, logging
│   │       └── Program.cs          # Application entry point
│   ├── web/                        # Next.js 14 Frontend
│   │   ├── src/
│   │   │   ├── app/                # Next.js App Router pages
│   │   │   ├── components/         # React components (shadcn/ui)
│   │   │   ├── lib/                # Utilities, API client, stores
│   │   │   ├── hooks/              # Custom React hooks
│   │   │   └── styles/             # Global CSS, Tailwind config
│   │   ├── e2e/                    # Playwright E2E tests
│   │   ├── __tests__/              # Vitest unit/integration tests
│   │   └── public/                 # Static assets
│   ├── embedding-service/          # Python embeddings service
│   ├── reranker-service/           # Python reranking service
│   ├── smoldocling-service/        # Document intelligence service
│   └── unstructured-service/       # PDF processing service
├── docs/                           # Comprehensive documentation
│   ├── 01-architecture/            # ADRs, diagrams, DDD docs
│   ├── 02-development/             # Developer guides, workflows
│   ├── 03-api/                     # API reference, examples
│   ├── 04-deployment/              # Deployment, infra, monitoring
│   ├── 05-testing/                 # Testing strategy, coverage
│   └── claudedocs/                 # Claude-specific analysis docs
├── infra/                          # Infrastructure as Code
│   ├── docker-compose.yml          # Local development stack
│   ├── traefik/                    # Reverse proxy config
│   ├── monitoring/                 # Grafana, Prometheus configs
│   └── secrets/                    # Secret management (git-ignored)
├── tests/                          # Shared test utilities
│   └── Api.Tests/                  # Backend test suite
├── scripts/                        # Automation scripts
│   ├── deploy/                     # Deployment scripts
│   ├── db/                         # Database maintenance
│   └── tools/                      # Development utilities
├── .github/                        # GitHub Actions CI/CD
│   └── workflows/
│       ├── backend-ci.yml          # Backend tests + build
│       ├── frontend-ci.yml         # Frontend tests + build
│       └── e2e-tests.yml           # Playwright E2E suite
└── tools/                          # Development tooling
    ├── game-scraper/               # BGG data scraper
    └── cleanup/                    # Test process cleanup scripts
```

---

## Documentation Reference

**Quick Links**:
- **API Documentation**: http://localhost:8080/scalar/v1 (Scalar UI)
- **Development Guide**: [docs/02-development/README.md](docs/02-development/README.md)
- **API Reference**: [docs/03-api/README.md](docs/03-api/README.md)
- **Deployment Guide**: [docs/04-deployment/README.md](docs/04-deployment/README.md)
- **Testing Guide**: [docs/05-testing/README.md](docs/05-testing/README.md)
- **Architecture ADRs**: [docs/01-architecture/adr/](docs/01-architecture/adr/)

**Living Documentation**:
- Auto-generated from code + manual ADRs
- Each Bounded Context has README.md with auto-updated API surface
- OpenAPI spec auto-generated from C# attributes

---

## Troubleshooting

### Common Issues

**Backend fails to start - Missing secrets**:
```bash
# Error: "CRITICAL secret missing: infra/secrets/database.secret"

# Solution: Auto-generate all secrets
cd infra/secrets
.\setup-secrets.ps1

# Verify all CRITICAL secrets exist
ls *.secret | findstr "admin database jwt qdrant redis embedding"
# Should show 6 files

# Then restart
cd ../../infra
docker compose restart api
```

**Backend fails to start - Database connection**:
```bash
# Check PostgreSQL connection
docker ps | grep postgres
docker compose logs postgres

# Reset database
cd apps/api/src/Api
dotnet ef database drop --force
dotnet ef database update
```

**Frontend build fails**:
```bash
# Clear Next.js cache
cd apps/web
rm -rf .next
pnpm build

# Regenerate API client
pnpm generate:api
```

**Tests fail with "Database already exists"**:
```bash
# Testcontainers cleanup
docker ps -a | grep testcontainers | awk '{print $1}' | xargs docker rm -f
```

**Port already in use**:
```bash
# Find process using port 8080 (API)
netstat -ano | findstr :8080
# Kill process by PID
taskkill /PID <PID> /F

# Find process using port 3000 (Web)
netstat -ano | findstr :3000
```

**Tests fail to run - Testhost processes blocking files** (Issue #2593):
```bash
# Symptom: "The process cannot access the file 'Api.dll' because it is being used by another process"
# Root Cause: Previous test runs left testhost.exe processes active

# Solution: Kill testhost processes before running tests
tasklist | grep -i "testhost"
taskkill //PID <PID> //F

# Verify cleanup
tasklist | grep -i "testhost" || echo "✅ Clean"

# Then rebuild and test
cd apps/api && dotnet build && dotnet test
```

**Culture-dependent test failures** (Issue #2593):
```bash
# Symptom: ToString() tests fail with "87 %" instead of "87%"
# Root Cause: InvariantCulture with P0 format adds space in percentages

# ❌ Wrong: Still adds space
RelevanceScore.ToString("P0", CultureInfo.InvariantCulture) // "87 %"

# ✅ Correct: Custom format without spaces
$"{(RelevanceScore * 100):0}%" // "87%"
```

---

## AI-Specific Context for Claude

### When Working on Features

1. **Always start with domain understanding**: Read the relevant Bounded Context README
2. **Follow CQRS pattern**: Command → Handler → Endpoint → Tests
3. **Use MediatR**: HTTP endpoints ONLY call `IMediator.Send()`, never services directly
4. **Write tests first**: TDD approach with unit → integration → E2E progression
5. **Check ADRs**: Review architecture decisions before proposing changes

### Code Analysis Priorities

1. **DDD Compliance**: Entities with private setters, value objects immutable, repositories interfaces in Domain
2. **CQRS Separation**: Commands vs Queries, no shared models
3. **FluentValidation**: All commands/queries have validators
4. **Test Coverage**: Aim for 90%+ (check `dotnet test /p:CollectCoverage=true`)
5. **Security**: Use Semgrep patterns (`.semgrep.yml`), detect-secrets for keys

### Common Patterns to Apply

**Soft Delete**:
```csharp
public bool IsDeleted { get; private set; }
public DateTime? DeletedAt { get; private set; }

public void SoftDelete()
{
    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
}

// In DbContext
modelBuilder.Entity<Game>().HasQueryFilter(g => !g.IsDeleted);
```

**Audit Trail**:
```csharp
public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
public DateTime? UpdatedAt { get; private set; }
public string CreatedBy { get; private set; }
public string? UpdatedBy { get; private set; }

public void UpdateAuditInfo(string userId)
{
    UpdatedAt = DateTime.UtcNow;
    UpdatedBy = userId;
}
```

**Optimistic Concurrency**:
```csharp
[Timestamp]
public byte[] RowVersion { get; private set; }

// In SaveChanges, catch DbUpdateConcurrencyException
```

### Recent Learnings & Best Practices

**Implementing HTTP Endpoints (Issue #2567 pattern)**:

When adding new CRUD endpoints for a domain entity:

1. **Create in this order** (Issue #2567 implementation):
   - DTOs (`Application/DTOs/EntityDto.cs`) - API contracts
   - Queries (`Application/Queries/GetAll*.cs`, `GetById*.cs`)
   - Commands (`Application/Commands/Create*.cs`, `Update*.cs`, `Delete*.cs`)
   - Validators (`Application/Validators/*Validator.cs`) - FluentValidation rules
   - Handlers (`Application/Handlers/*Handler.cs`) - Business logic
   - Routing (`Routing/EntityEndpoints.cs`) - HTTP mappings
   - Register in `Program.cs`: `v1Api.MapEntityEndpoints();`

2. **Exception Handling** (Code Review PR #2568):
   - Use `ConflictException` for business rule violations (maps to 409 Conflict)
   - Use `NotFoundException` for missing resources (maps to 404 Not Found)
   - NEVER use `InvalidOperationException` (maps to 500 Internal Server Error)
   - Align with ADR-009 centralized error handling

3. **Immutable Fields**:
   - If entity fields are immutable (e.g., ModelId, DisplayName), do NOT include them in Update command
   - Avoids misleading API contracts where fields appear updatable but are silently ignored

**Security Incident Response** (Issue #2565 lesson):

If secret values are accidentally committed in documentation:

1. **Immediate Actions**:
   ```bash
   # Redact documentation
   # Replace real values with [REDACTED]

   # Rotate ALL exposed credentials
   cd infra/secrets
   # Generate new passwords for exposed secrets

   # Update .secret files with new values
   # Restart Docker services
   docker compose down
   docker volume rm infra_pgdata  # If database password rotated
   docker compose up -d

   # Re-apply migrations if needed
   cd apps/api/src/Api
   pwsh apply-migrations.ps1
   ```

2. **Prevent Future Incidents**:
   - Add files with passwords to `.gitignore`
   - Use `[REDACTED]` in all documentation examples
   - Never copy actual password values into commit messages or docs

3. **Git History**:
   - For dev environment: Rotation is sufficient (acceptable to have old passwords in history)
   - For production: Consider `git-filter-repo` to remove from history

**Code Review Automation** (PR #2568, #2572 workflow):

After creating PR, use automated code review:
```bash
# From Claude Code skill
/code-review:code-review <pr-number>
```

Process:
1. Checks PR eligibility (open, not draft, not reviewed)
2. Identifies relevant CLAUDE.md files
3. Launches 5 parallel review agents (CLAUDE.md, bugs, history, PRs, comments)
4. Scores issues with confidence (0-100 scale)
5. Filters high-confidence issues (≥80)
6. Posts review comment with file/line links

**DI Registration Pattern** (Issue #2565 fix):

Always register both interface and implementation:
```csharp
// ✅ CORRECT
services.AddScoped<IGameStateParser, GameStateParser>();

// ❌ WRONG - Only concrete class, missing interface mapping
services.AddScoped<GameStateParser>();
```

**Docker Compose Variable Escaping**:

When using environment variables in shell commands:
```yaml
# ✅ CORRECT - Double $$ for docker-compose variable expansion
command: ["sh","-c","redis-server --requirepass $$REDIS_PASSWORD"]

# ❌ WRONG - Single $ gets expanded by docker-compose, not in container
command: ["sh","-c","redis-server --requirepass $REDIS_PASSWORD"]
```

**Test Environment Cleanup** (Issue #2593):

Before running tests, verify no testhost processes are blocking files:
```bash
# Check for blocking processes
tasklist | grep -i "testhost"

# Kill if found
taskkill //PID <PID> //F

# Verify clean state before build/test
tasklist | grep -i "testhost" || echo "✅ Clean"
```

**Culture-Independent Formatting** (Issue #2593):

Percentage formatting in value objects must avoid culture-dependent spaces:
```csharp
// ❌ WRONG - InvariantCulture P0 adds space ("87 %")
ToString("P0", CultureInfo.InvariantCulture)

// ✅ CORRECT - Custom format guarantees no space ("87%")
$"{(value * 100):0}%"
```

**Local Test Validation Priority** (Issue #2593):

- Always test locally before relying on CI for validation
- Kill testhost processes: `tasklist | grep testhost` → `taskkill`
- Verify build clean: `dotnet build` (0 warnings, 0 errors)
- Run specific tests: `dotnet test --filter "TestName"`
- Confirm all pass locally before PR push

---

## Success Metrics

**Development Velocity**:
- Time to implement new feature: < 4 hours (domain → tests → endpoint)
- Time to fix bug: < 1 hour (identify → fix → test → deploy)
- PR merge time: < 24 hours (review → approve → merge)

**Quality Metrics**:
- Test coverage: > 90% (backend), > 85% (frontend)
- Build success rate: > 95% (CI/CD pipeline)
- Bug escape rate: < 5% (bugs found in production vs staging)
- Security vulnerabilities: 0 critical, < 3 high (Semgrep + detect-secrets)

**Performance Metrics** (Production):
- API response time (p95): < 200ms
- Frontend TTI (Time to Interactive): < 3s
- Lighthouse score: > 90 (Performance, Accessibility, Best Practices)

---

**Last Updated**: 2026-01-12
**Maintainer**: MeepleAI Development Team
**License**: Proprietary

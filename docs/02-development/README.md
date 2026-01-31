# Development Guide

**MeepleAI Development Documentation** - Guide complete per sviluppatori

---

## Quick Start

### Prerequisites
- .NET 9 SDK
- Node.js 20+ (con pnpm)
- Docker Desktop
- PostgreSQL 16+ (via Docker)
- Git

### Local Setup

**1. Clone e Dependencies**
```bash
git clone <repo-url>
cd meepleai-monorepo

# Backend dependencies (auto-restore on build)
cd apps/api/src/Api
dotnet restore

# Frontend dependencies
cd ../../../web
pnpm install
```

**2. Environment Configuration**
```bash
# Copy environment template
cp .env.example .env.local

# Required variables
OPENROUTER_API_KEY=<your-key>
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=postgres
QDRANT_URL=http://localhost:6333
REDIS_URL=localhost:6379
INITIAL_ADMIN_EMAIL=admin@meepleai.com
INITIAL_ADMIN_PASSWORD=<secure-password>
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

**3. Start Infrastructure**
```bash
cd infra
docker compose up -d postgres qdrant redis
```

**4. Run Backend (Terminal 1)**
```bash
cd apps/api/src/Api
dotnet run
# API available at http://localhost:8080
# Swagger UI at http://localhost:8080/scalar/v1
```

**5. Run Frontend (Terminal 2)**
```bash
cd apps/web
pnpm dev
# Web app at http://localhost:3000
```

---

## Architecture Overview

### DDD Bounded Contexts

Il progetto segue DDD con 7 bounded contexts, ciascuno con pattern CQRS/MediatR:

```
apps/api/src/Api/BoundedContexts/
├── Authentication/         # Auth, sessions, API keys, OAuth, 2FA
├── GameManagement/         # Games catalog, play sessions
├── KnowledgeBase/          # RAG, vectors, chat (Hybrid RRF)
├── DocumentProcessing/     # PDF upload, extraction, validation
├── WorkflowIntegration/    # n8n workflows, error logging
├── SystemConfiguration/    # Runtime config, feature flags
└── Administration/         # Users, alerts, audit, analytics
```

**Pattern per Context**:
```
{Context}/
├── Domain/             # Entities, Value Objects, Domain Events
├── Application/        # Commands, Queries, Handlers (CQRS)
└── Infrastructure/     # Repositories, External Services
```

### HTTP Layer (Routing)

Tutti gli endpoint HTTP usano **SOLO** `IMediator.Send()`, ZERO dipendenze da services:

```csharp
// apps/api/Routing/AuthenticationEndpoints.cs
app.MapPost("/api/v1/auth/register", async (
    RegisterCommand command,
    IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return Results.Ok(result);
});
```

---

## Development Workflow

### Adding a New Feature

**Pattern**: Domain → Application (Command/Query) → Handler → Endpoint → Tests

**1. Domain Layer**
```csharp
// BoundedContexts/{Context}/Domain/Entities/MyEntity.cs
public class MyEntity
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    // Constructor + Factory methods
    public static MyEntity Create(string name) => new() { Name = name };
}
```

**2. Application Layer - Command**
```csharp
// BoundedContexts/{Context}/Application/Commands/CreateMyEntityCommand.cs
public record CreateMyEntityCommand(string Name) : IRequest<MyEntityDto>;

// Handler
public class CreateMyEntityCommandHandler : IRequestHandler<CreateMyEntityCommand, MyEntityDto>
{
    private readonly AppDbContext _db;

    public async Task<MyEntityDto> Handle(CreateMyEntityCommand request, CancellationToken ct)
    {
        var entity = MyEntity.Create(request.Name);
        _db.MyEntities.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new MyEntityDto { Id = entity.Id, Name = entity.Name };
    }
}
```

**3. HTTP Endpoint**
```csharp
// Routing/MyContextEndpoints.cs
public static void MapMyContextEndpoints(this IEndpointRouteBuilder app)
{
    app.MapPost("/api/v1/mycontext", async (
        CreateMyEntityCommand command,
        IMediator mediator) =>
    {
        var result = await mediator.Send(command);
        return Results.Ok(result);
    })
    .WithTags("MyContext")
    .WithOpenApi();
}
```

**4. Tests**
```csharp
// tests/Api.Tests/BoundedContexts/MyContext/CreateMyEntityTests.cs
public class CreateMyEntityTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateMyEntity_ShouldReturnDto()
    {
        // Arrange
        var command = new CreateMyEntityCommand("Test");

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Name.Should().Be("Test");
    }
}
```

### Database Migrations

**Create Migration**
```bash
cd apps/api/src/Api
dotnet ef migrations add AddMyEntity --project .
```

**Apply Migration (auto-apply on startup)**
```bash
dotnet run
# Migrations applied automatically via DbInitializer
```

**Rollback Migration**
```bash
dotnet ef database update <PreviousMigrationName>
```

---

## Code Standards

### Backend (C#)

**Required**:
- Nullable reference types enabled
- `async/await` for I/O operations
- Dependency Injection via constructor
- Structured logging with Serilog
- `using` statements for IDisposable
- XML comments for public APIs

**Example**:
```csharp
/// <summary>
/// Retrieves game by ID
/// </summary>
/// <param name="gameId">Game identifier</param>
/// <returns>Game DTO or null if not found</returns>
public async Task<GameDto?> GetGameAsync(Guid gameId)
{
    return await _db.Games
        .AsNoTracking()
        .Where(g => g.Id == gameId)
        .Select(g => new GameDto { Id = g.Id, Name = g.Name })
        .FirstOrDefaultAsync();
}
```

### Frontend (TypeScript)

**Required**:
- TypeScript strict mode
- ESLint + Prettier compliance
- No `any` types (use `unknown` with type guards)
- API calls via `@/lib/api` client
- Component documentation with JSDoc

**Example**:
```typescript
/**
 * Fetches game by ID
 * @param gameId - Game identifier
 * @returns Game DTO or null
 */
export async function getGame(gameId: string): Promise<GameDto | null> {
  const response = await apiClient.get<GameDto>(`/api/v1/games/${gameId}`);
  return response.data;
}
```

---

## Testing Strategy

### Backend Tests

**Stack**: xUnit + Moq + Testcontainers + FluentAssertions

**Coverage Target**: >90%

**Test Categories**:
```csharp
// Unit Tests
[Fact]
public void EntityCreate_ShouldValidateInput() { }

// Integration Tests with Testcontainers
[Fact]
public async Task Handler_ShouldPersistToDatabase() { }
```

**Run Tests**:
```bash
cd apps/api
dotnet test
```

### Frontend Tests

**Stack**: Vitest + Testing Library + Playwright

**Coverage Target**: >90%

**Test Categories**:
```typescript
// Unit Tests (Vitest)
describe('useGameQuery', () => {
  it('should fetch game data', async () => { });
});

// E2E Tests (Playwright)
test('user can login and view games', async ({ page }) => { });
```

**Run Tests**:
```bash
cd apps/web
pnpm test              # Unit tests
pnpm test:e2e          # E2E tests
pnpm test:coverage     # Coverage report
```

---

## Debugging

### Backend Debugging

**VS Code Launch Configuration**:
```json
{
  "name": ".NET Core Launch (API)",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "build",
  "program": "${workspaceFolder}/apps/api/src/Api/bin/Debug/net9.0/Api.dll",
  "cwd": "${workspaceFolder}/apps/api/src/Api",
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

**Logs Location**:
- Console: stdout
- HyperDX: http://localhost:8080 → logs panel
- Seq (optional): http://localhost:5341

### Frontend Debugging

**Browser DevTools**:
- Network tab for API calls
- Console for errors
- React DevTools for component state

**VS Code Debugging**:
```json
{
  "name": "Next.js: debug",
  "type": "node-terminal",
  "request": "launch",
  "command": "pnpm dev"
}
```

---

## Performance Optimization

### Backend Performance

**Query Optimization**:
```csharp
// ✅ GOOD - AsNoTracking for read-only queries
var games = await _db.Games
    .AsNoTracking()
    .ToListAsync();

// ❌ BAD - Tracking overhead for read-only data
var games = await _db.Games.ToListAsync();
```

**Caching Strategy**:
```csharp
// HybridCache (L1 + L2)
var result = await _cache.GetOrCreateAsync(
    $"game:{id}",
    async entry =>
    {
        entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
        return await _db.Games.FindAsync(id);
    }
);
```

### Frontend Performance

**Code Splitting**:
```typescript
// Dynamic imports for large components
const GameDetailsModal = dynamic(() => import('./GameDetailsModal'), {
  loading: () => <Skeleton />,
});
```

**React Query Optimization**:
```typescript
// Stale-while-revalidate pattern
const { data } = useQuery({
  queryKey: ['games'],
  queryFn: getGames,
  staleTime: 5 * 60 * 1000, // 5 min
  cacheTime: 10 * 60 * 1000, // 10 min
});
```

---

## Common Tasks

### Add New Bounded Context

1. Create directory structure:
```bash
mkdir -p apps/api/src/Api/BoundedContexts/MyContext/{Domain,Application,Infrastructure}
```

2. Add entities, commands, handlers
3. Register in `Program.cs`:
```csharp
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(MyContextCommand).Assembly));
```

4. Create README from template:
```bash
cp apps/api/src/Api/BoundedContexts/README-TEMPLATE.md \
   apps/api/src/Api/BoundedContexts/MyContext/README.md
```

### Update Dependencies

**Backend**:
```bash
dotnet list package --outdated
dotnet add package <PackageName>
```

**Frontend**:
```bash
pnpm outdated
pnpm update <package-name>
```

### Generate API Client

**Auto-generated on build** via Scalar OpenAPI:
- Frontend client: `apps/web/src/lib/api.ts`
- OpenAPI spec: http://localhost:8080/openapi/v1.json

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Migration errors | `dotnet ef database update <PreviousMigration>` |
| CORS errors | Check `NEXT_PUBLIC_API_BASE` and `credentials: "include"` |
| Port conflicts | Kill processes: `lsof -ti:8080 \| xargs kill` (Mac/Linux) |
| Database connection | Verify PostgreSQL running: `docker ps \| grep postgres` |
| Redis connection | Check Redis: `docker logs meepleai-redis` |

### Debug Checklist

- [ ] Environment variables set correctly
- [ ] All infrastructure services running (postgres, qdrant, redis)
- [ ] Migrations applied successfully
- [ ] No port conflicts
- [ ] API key configured (for OpenRouter)
- [ ] Frontend API base URL matches backend port

---

## Resources

### Development Guides
- **[Visual Studio Code Setup](guida-visualcode.md)** ⭐ Task automation, troubleshooting, Docker workflow
- [Git Workflow](git-workflow.md)
- [Operational Guide](operational-guide.md)
- [Documentation Tools](documentation-tools-guide.md)

### Configuration
- [Local Secrets Setup](local-secrets-setup.md)
- [Configuration Values Guide](configuration-values-guide.md)
- [Docker Services Test URLs](docker-services-test-urls.md)

### Architecture
- [Architecture Overview](../01-architecture/overview/system-architecture.md)
- [ADRs](../01-architecture/adr/)
- [Bounded Context READMEs](../../apps/api/src/Api/BoundedContexts/)

### API & Testing
- [API Documentation](http://localhost:8080/scalar/v1)
- [Living Documentation Guide](../living-documentation.md)

---

**Version**: 1.1
**Last Updated**: 2026-01-18
**Maintainers**: Engineering Team

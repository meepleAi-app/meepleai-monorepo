# Developer Guide - MeepleAI

**Audience**: Software developers working on the MeepleAI codebase.

## 📋 Table of Contents

1. [Prerequisites](#prerequisites)
2. [Development Environment Setup](#development-environment-setup)
3. [Project Structure](#project-structure)
4. [Development Workflow](#development-workflow)
5. [Coding Standards](#coding-standards)
6. [Architecture Patterns](#architecture-patterns)
7. [API Development](#api-development)
8. [Frontend Development](#frontend-development)
9. [Database Changes](#database-changes)
10. [Testing](#testing)
11. [Debugging](#debugging)
12. [Common Tasks](#common-tasks)
13. [Troubleshooting](#troubleshooting)

## ✅ Prerequisites

### Required Tools

- **.NET 9 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Node.js 20+**: [Download](https://nodejs.org/)
- **pnpm 9+**: `npm install -g pnpm`
- **Docker Desktop**: [Download](https://www.docker.com/products/docker-desktop)
- **Git**: [Download](https://git-scm.com/)

### Recommended Tools

- **IDE**: Visual Studio 2022, Rider, or VS Code
- **Database Client**: pgAdmin, DBeaver, or DataGrip
- **API Client**: Postman, Insomnia, or HTTPie
- **Git Client**: GitKraken, SourceTree, or CLI

### Knowledge Prerequisites

- **C# & .NET**: ASP.NET Core, Entity Framework Core, LINQ
- **TypeScript/JavaScript**: React, Next.js, async/await
- **Databases**: PostgreSQL, SQL basics
- **DDD**: Bounded contexts, aggregates, value objects
- **CQRS**: Commands, queries, handlers (MediatR)
- **REST APIs**: HTTP methods, status codes, JSON

## 🛠️ Development Environment Setup

### 1. Clone Repository

```bash
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo
```

### 2. Start Infrastructure Services

```bash
cd infra
docker compose up -d
```

**Services Started**:
- PostgreSQL: `localhost:5432`
- Qdrant: `localhost:6333`
- Redis: `localhost:6379`
- n8n: `localhost:5678`
- Seq: `localhost:8081`
- Jaeger: `localhost:16686`
- Prometheus: `localhost:9090`
- Grafana: `localhost:3001`
- Ollama: `localhost:11434`
- Embedding: `localhost:8000`
- Unstructured: `localhost:8001`
- SmolDocling: `localhost:8002`

**Verify Services**:
```bash
docker compose ps
```

### 3. Configure Environment

```bash
# API environment
cp infra/env/.env.example infra/env/.env.dev

# Edit with your values
nano infra/env/.env.dev
```

**Required Variables**:
```env
# Database
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=postgres

# AI Services
OPENROUTER_API_KEY=your_openrouter_key_here

# External Services
QDRANT_URL=http://localhost:6333
REDIS_URL=localhost:6379
SEQ_URL=http://localhost:8081

# Initial Admin
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=Admin123!
```

**Frontend Environment**:
```bash
# In apps/web/
cp .env.example .env.local

# Edit
nano .env.local
```

```env
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

### 4. Start Backend (API)

```bash
# Terminal 1
cd apps/api/src/Api
dotnet restore
dotnet build
dotnet run
```

**API Available**: `http://localhost:8080`
**Health Check**: `curl http://localhost:8080/health`

### 5. Start Frontend (Web)

```bash
# Terminal 2
cd apps/web
pnpm install
pnpm dev
```

**Web Available**: `http://localhost:3000`

### 6. Verify Setup

**Check API**:
```bash
curl http://localhost:8080/health
# Expected: {"status": "Healthy", ...}
```

**Check Frontend**:
```bash
curl http://localhost:3000
# Expected: HTML response
```

**Check Database**:
```bash
docker exec -it postgres psql -U postgres -d meepleai -c "\dt"
# Expected: List of tables
```

## 📁 Project Structure

```
meepleai-monorepo/
├── apps/
│   ├── api/                        # Backend API (ASP.NET 9)
│   │   ├── src/Api/
│   │   │   ├── BoundedContexts/    # 7 DDD contexts
│   │   │   │   ├── Authentication/
│   │   │   │   ├── GameManagement/
│   │   │   │   ├── KnowledgeBase/
│   │   │   │   ├── DocumentProcessing/
│   │   │   │   ├── WorkflowIntegration/
│   │   │   │   ├── SystemConfiguration/
│   │   │   │   └── Administration/
│   │   │   ├── Infrastructure/     # EF Core, DbContext
│   │   │   ├── Routing/            # HTTP endpoints
│   │   │   └── Program.cs
│   │   └── tests/                  # xUnit tests
│   └── web/                        # Frontend (Next.js 16)
│       ├── pages/                  # Routes
│       ├── components/             # React components
│       ├── lib/                    # Utilities
│       └── tests/                  # Jest tests
├── infra/
│   ├── docker-compose.yml          # Local services
│   └── env/                        # Environment configs
├── docs/                           # Documentation (115+ docs)
├── .wiki/                          # Wiki guides (this!)
└── tools/                          # Scripts and utilities
```

### Bounded Context Structure

Each context follows this pattern:

```
BoundedContexts/{Context}/
├── Domain/                         # Pure domain logic
│   ├── Aggregates/                 # Aggregate roots
│   ├── ValueObjects/               # Value objects
│   ├── DomainServices/             # Domain services
│   ├── Events/                     # Domain events
│   └── Exceptions/                 # Domain exceptions
├── Application/                    # CQRS layer
│   ├── Commands/                   # Write operations
│   ├── Queries/                    # Read operations
│   └── Handlers/                   # MediatR handlers
└── Infrastructure/                 # Adapters
    ├── Repositories/               # Data access
    ├── Services/                   # External services
    └── Adapters/                   # Third-party integrations
```

## 🔄 Development Workflow

### Feature Development

**1. Create Feature Branch**:
```bash
git checkout -b feature/your-feature-name
```

**2. Implement Feature**:
Follow the DDD/CQRS pattern:

```csharp
// Step 1: Define domain model
// Domain/Aggregates/Game.cs
public class Game
{
    public GameId Id { get; private set; }
    public string Title { get; private set; }

    public void UpdateTitle(string title)
    {
        // Domain validation
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title cannot be empty");

        Title = title;
    }
}

// Step 2: Create command
// Application/Commands/UpdateGameTitleCommand.cs
public record UpdateGameTitleCommand(int GameId, string Title) : IRequest<Result>;

// Step 3: Create handler
// Application/Handlers/UpdateGameTitleCommandHandler.cs
public class UpdateGameTitleCommandHandler : IRequestHandler<UpdateGameTitleCommand, Result>
{
    private readonly IGameRepository _repository;

    public async Task<Result> Handle(UpdateGameTitleCommand request, CancellationToken ct)
    {
        var game = await _repository.GetByIdAsync(request.GameId, ct);
        game.UpdateTitle(request.Title);
        await _repository.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// Step 4: Add endpoint
// Routing/GameEndpoints.cs
app.MapPut("/api/v1/games/{id}/title", async (int id, UpdateTitleRequest req, IMediator mediator) =>
{
    var result = await mediator.Send(new UpdateGameTitleCommand(id, req.Title));
    return result.IsSuccess ? Results.Ok() : Results.BadRequest();
});
```

**3. Write Tests**:
```csharp
public class UpdateGameTitleCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidTitle_UpdatesGame()
    {
        // Arrange
        var repository = new Mock<IGameRepository>();
        var handler = new UpdateGameTitleCommandHandler(repository.Object);
        var command = new UpdateGameTitleCommand(1, "New Title");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**4. Run Tests**:
```bash
# Backend
cd apps/api
dotnet test

# Frontend
cd apps/web
pnpm test
```

**5. Commit Changes**:
```bash
git add .
git commit -m "feat(games): Add game title update functionality"
```

**6. Push and Create PR**:
```bash
git push origin feature/your-feature-name
gh pr create --title "Add game title update" --body "Description..."
```

### Git Commit Convention

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Formatting
- `refactor`: Code restructuring
- `test`: Adding tests
- `chore`: Maintenance

**Examples**:
```
feat(auth): Add 2FA support
fix(rag): Fix hallucination detection
docs(api): Update API specification
refactor(pdf): Migrate to orchestrator pattern
test(games): Add integration tests
```

## 📐 Coding Standards

### C# Standards

**Style**:
- PascalCase for classes, methods, properties
- camelCase for parameters, local variables
- Prefix interfaces with `I`
- Use `var` when type is obvious
- Async methods end with `Async`

**Example**:
```csharp
public class GameService : IGameService
{
    private readonly ILogger<GameService> _logger;

    public GameService(ILogger<GameService> logger)
    {
        _logger = logger;
    }

    public async Task<Game> GetGameAsync(int gameId, CancellationToken ct)
    {
        var game = await _dbContext.Games.FindAsync(gameId, ct);
        return game;
    }
}
```

**Best Practices**:
- Use nullable reference types
- Always pass `CancellationToken`
- Use `ILogger` for logging
- Dispose resources with `using`
- Use `IHttpClientFactory` for HTTP clients
- Avoid `async void` (except event handlers)

### TypeScript/React Standards

**Style**:
- PascalCase for components, types, interfaces
- camelCase for variables, functions
- Use arrow functions for components
- Prefer `const` over `let`
- Use TypeScript strict mode

**Example**:
```typescript
import { useState } from 'react'
import { Button } from '@/components/ui/button'

interface GameCardProps {
  gameId: number
  title: string
  onSelect?: (id: number) => void
}

export const GameCard: React.FC<GameCardProps> = ({ gameId, title, onSelect }) => {
  const [isHovered, setIsHovered] = useState(false)

  const handleClick = () => {
    onSelect?.(gameId)
  }

  return (
    <div
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <h3>{title}</h3>
      <Button onClick={handleClick}>Select</Button>
    </div>
  )
}
```

**Best Practices**:
- Use TypeScript, avoid `any`
- Extract reusable components
- Use Shadcn/UI components
- Handle loading and error states
- Use `@/lib/api` for API calls
- Implement proper error boundaries

### Database Standards

**Migrations**:
```bash
# Create migration
dotnet ef migrations add YourMigrationName --project src/Api

# Apply migration (auto-applied on startup)
dotnet run
```

**Entity Configuration**:
```csharp
public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(g => g.Title);
    }
}
```

## 🏗️ Architecture Patterns

### Domain-Driven Design (DDD)

**Aggregates**:
- Encapsulate domain logic
- Enforce invariants
- Use private setters
- Expose methods for operations

**Value Objects**:
- Immutable
- Equality by value
- No identity

**Domain Services**:
- Domain logic that doesn't fit in aggregates
- Stateless
- Pure logic

### CQRS with MediatR

**Commands** (Write):
```csharp
public record CreateGameCommand(string Title, string Publisher) : IRequest<Result<int>>;

public class CreateGameCommandHandler : IRequestHandler<CreateGameCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateGameCommand request, CancellationToken ct)
    {
        // Create game
        return Result.Success(gameId);
    }
}
```

**Queries** (Read):
```csharp
public record GetGameQuery(int GameId) : IRequest<GameDto>;

public class GetGameQueryHandler : IRequestHandler<GetGameQuery, GameDto>
{
    public async Task<GameDto> Handle(GetGameQuery request, CancellationToken ct)
    {
        // Get game
        return gameDto;
    }
}
```

**Endpoints**:
```csharp
// ALWAYS use IMediator, NEVER inject services
app.MapPost("/api/v1/games", async (CreateGameRequest req, IMediator mediator) =>
{
    var result = await mediator.Send(new CreateGameCommand(req.Title, req.Publisher));
    return result.IsSuccess ? Results.Created($"/api/v1/games/{result.Value}", result.Value) : Results.BadRequest();
});
```

### Repository Pattern

```csharp
public interface IGameRepository
{
    Task<Game> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Game>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Game game, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public class GameRepository : IGameRepository
{
    private readonly ApplicationDbContext _context;

    public async Task<Game> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Games.FindAsync(new object[] { id }, ct);
    }

    // ... other methods
}
```

## 🔌 API Development

### Creating New Endpoints

See [Architecture Patterns](#architecture-patterns) above.

### Authentication

**Cookie Auth**:
```csharp
// Automatic for web clients
// Cookie set on login
```

**API Key Auth**:
```bash
curl http://localhost:8080/api/v1/games \
  -H "X-API-Key: mpl_dev_YOUR_KEY"
```

**Priority**: API Key > Cookie

### Error Handling

```csharp
public class ErrorHandlingMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = "Internal server error" });
        }
    }
}
```

### Logging

```csharp
public class MyHandler
{
    private readonly ILogger<MyHandler> _logger;

    public async Task Handle()
    {
        _logger.LogInformation("Processing request for game {GameId}", gameId);

        try
        {
            // ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process game {GameId}", gameId);
            throw;
        }
    }
}
```

## 🎨 Frontend Development

### Using Shadcn/UI

```typescript
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

export const MyComponent = () => {
  return (
    <Card>
      <CardHeader>
        <CardTitle>My Card</CardTitle>
      </CardHeader>
      <CardContent>
        <Input placeholder="Enter text..." />
        <Button>Submit</Button>
      </CardContent>
    </Card>
  )
}
```

### API Calls

```typescript
import { api } from '@/lib/api'

// GET
const games = await api.get('/api/v1/games')

// POST
const result = await api.post('/api/v1/games', {
  title: 'Catan',
  publisher: 'Kosmos'
})

// PUT
await api.put(`/api/v1/games/${id}`, { title: 'New Title' })

// DELETE
await api.delete(`/api/v1/games/${id}`)
```

### State Management

```typescript
'use client'

import { useState, useEffect } from 'react'
import { api } from '@/lib/api'

export const GamesList = () => {
  const [games, setGames] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    const fetchGames = async () => {
      try {
        const data = await api.get('/api/v1/games')
        setGames(data)
      } catch (err) {
        setError(err.message)
      } finally {
        setLoading(false)
      }
    }

    fetchGames()
  }, [])

  if (loading) return <div>Loading...</div>
  if (error) return <div>Error: {error}</div>

  return (
    <div>
      {games.map(game => (
        <div key={game.id}>{game.title}</div>
      ))}
    </div>
  )
}
```

## 🗄️ Database Changes

### Creating Migrations

```bash
cd apps/api/src/Api
dotnet ef migrations add YourMigrationName
```

**Migration Files Generated**:
- `Migrations/YYYYMMDDHHMMSS_YourMigrationName.cs`
- `Migrations/ApplicationDbContextModelSnapshot.cs`

### Applying Migrations

Migrations are **auto-applied** on application startup.

**Manual Application**:
```bash
dotnet ef database update
```

### Reverting Migrations

```bash
# Revert to previous migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove
```

### Seeding Data

```csharp
// ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.Entity<User>().HasData(
        new User { Id = 1, Email = "admin@meepleai.dev", ... }
    );
}
```

## 🧪 Testing

See [Testing Guide](./03-testing-guide.md) for complete testing documentation.

**Quick Reference**:

```bash
# Backend tests
cd apps/api
dotnet test

# Frontend tests
cd apps/web
pnpm test

# E2E tests
cd apps/web
pnpm test:e2e

# Coverage
dotnet test /p:CollectCoverage=true
pnpm test --coverage
```

## 🐛 Debugging

### Backend Debugging

**VS Code** (`launch.json`):
```json
{
  "type": "coreclr",
  "request": "launch",
  "name": "Debug API",
  "program": "${workspaceFolder}/apps/api/src/Api/bin/Debug/net9.0/Api.dll",
  "cwd": "${workspaceFolder}/apps/api/src/Api",
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

**Visual Studio / Rider**: Press F5

### Frontend Debugging

**Browser DevTools**: F12

**VS Code Debugger**: Use "Debug Next.js" launch configuration

### Logs

**Backend Logs** (Seq): `http://localhost:8081`

**Frontend Logs**: Browser console

**Docker Logs**:
```bash
docker compose logs -f api
docker compose logs -f postgres
```

### Common Debug Points

**API Health**:
```bash
curl http://localhost:8080/health
```

**Database Connection**:
```bash
docker exec -it postgres psql -U postgres -c "SELECT 1"
```

**Qdrant Health**:
```bash
curl http://localhost:6333/healthz
```

**Redis Health**:
```bash
docker exec -it redis redis-cli ping
```

## 🔧 Common Tasks

### Add New Bounded Context

See [Architecture Guide](./06-architecture-guide.md) for detailed instructions.

### Add New API Endpoint

1. Create Command/Query in `Application/`
2. Create Handler in `Application/Handlers/`
3. Add endpoint in `Routing/`
4. Write tests
5. Update API documentation

### Add New UI Component

1. Create component in `components/`
2. Use Shadcn/UI primitives
3. Add TypeScript types
4. Write tests
5. Export from `index.ts`

### Update Dependencies

**Backend**:
```bash
dotnet list package --outdated
dotnet add package PackageName --version X.Y.Z
```

**Frontend**:
```bash
pnpm outdated
pnpm update PackageName
```

### Clear Caches

```bash
bash tools/cleanup-caches.sh --dry-run  # Preview
bash tools/cleanup-caches.sh            # Execute
```

## 🐞 Troubleshooting

### Build Errors

**"Cannot find package"**:
```bash
# Backend
dotnet restore

# Frontend
pnpm install
```

**"Migration already applied"**:
```bash
dotnet ef database update PreviousMigration
dotnet ef migrations remove
dotnet ef migrations add YourMigration
```

### Runtime Errors

**"Cannot connect to database"**:
- Check Docker: `docker compose ps`
- Check connection string in `.env.dev`
- Restart Postgres: `docker compose restart postgres`

**"Qdrant not available"**:
- Check Docker: `docker compose ps`
- Restart Qdrant: `docker compose restart qdrant`
- Check health: `curl http://localhost:6333/healthz`

**"CORS errors"**:
- Check `NEXT_PUBLIC_API_BASE` in `.env.local`
- Ensure `credentials: "include"` in API calls
- Check CORS policy in `Program.cs`

### Test Failures

**"Testcontainers timeout"**:
- Ensure Docker is running
- Check Docker resources (memory, CPU)
- Increase timeout in test configuration

**"Port already in use"**:
- Change port in `docker-compose.yml`
- Kill process using port: `lsof -ti:8080 | xargs kill`

## 📚 Additional Resources

- **[Testing Guide](./03-testing-guide.md)** - Comprehensive testing documentation
- **[Architecture Guide](./06-architecture-guide.md)** - Deep dive into architecture
- **[Contributing Guide](./07-contributing-guide.md)** - How to contribute
- **[API Specification](../docs/03-api/board-game-ai-api-specification.md)** - Complete API reference
- **[Main Documentation](../docs/INDEX.md)** - All documentation (115+ docs)

## 🤝 Getting Help

1. Check this guide and [Main Documentation](../docs/INDEX.md)
2. Search [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
3. Ask in [GitHub Discussions](https://github.com/DegrassiAaron/meepleai-monorepo/discussions)
4. Ask team members in Slack/Discord (if available)

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: Developers

# DDD Quick Reference Guide

**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: 6/7 bounded contexts implemented

---

## Bounded Context Map

```
┌─────────────────────────────────────────────────────────────────┐
│                    MeepleAI DDD Architecture                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │Authentication│  │GameManagement│  │KnowledgeBase │         │
│  │   (Core)     │◄─┤    (Core)    │◄─┤   (Core)     │         │
│  │              │  │              │  │              │         │
│  │ User         │  │ Game         │  │ VectorDoc    │         │
│  │ Session      │  │ GameSession  │  │ ChatThread   │         │
│  │ ApiKey       │  │              │  │ Embedding    │         │
│  │ OAuth        │  │              │  │ SearchResult │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
│         ▲                 ▲                 ▲                    │
│         │                 │                 │                    │
│  ┌──────┴──────┐  ┌──────┴──────┐  ┌──────┴──────┐            │
│  │Workflow     │  │SystemConfig │  │Document     │            │
│  │Integration  │  │  (Generic)  │  │Processing   │            │
│  │             │  │             │  │(Supporting) │            │
│  │ N8nConfig   │  │ Config      │  │ PdfDocument │            │
│  │ ErrorLog    │  │ FeatureFlag │  │             │            │
│  └─────────────┘  └─────────────┘  └─────────────┘            │
│                                                                  │
│  ┌──────────────────────────────────────────────────┐          │
│  │           Administration (Cross-Cutting)          │          │
│  │           Alert, AuditLog                         │          │
│  └──────────────────────────────────────────────────┘          │
│                                                                  │
│  ┌──────────────────────────────────────────────────┐          │
│  │              SharedKernel (Foundation)            │          │
│  │  AggregateRoot, Entity, ValueObject, IRepository │          │
│  └──────────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Quick Start: Add New Operation

### 1. Create Command/Query
```csharp
// Application/Commands/YourCommand.cs
public record YourCommand(Params...) : ICommand<YourDto>;
```

### 2. Create Handler
```csharp
// Application/Handlers/YourCommandHandler.cs
public class YourCommandHandler : ICommandHandler<YourCommand, YourDto>
{
    private readonly IYourRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public async Task<YourDto> Handle(YourCommand cmd, CancellationToken ct)
    {
        var aggregate = new YourAggregate(...);
        await _repo.AddAsync(aggregate, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapToDto(aggregate);
    }
}
```

### 3. Add Endpoint
```csharp
// Routing/YourEndpoints.cs
group.MapPost("/resource", async (YourRequest req, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new YourCommand(...), ct);
    return Results.Created($"/resource/{result.Id}", result);
});
```

### 4. Test
```csharp
// tests/BoundedContexts/YourContext/Domain/YourAggregateTests.cs
[Fact]
public void YourAggregate_DomainMethod_WorksCorrectly()
{
    // Arrange
    var aggregate = new YourAggregate(...);

    // Act
    aggregate.DomainMethod();

    // Assert
    Assert.Equal(expected, aggregate.Property);
}
```

---

## Bounded Context Patterns

### Authentication Pattern
- **Aggregates**: User, Session, ApiKey, OAuthAccount
- **Use for**: User management, authentication, authorization
- **Example**: Create user, validate session, manage API keys

### GameManagement Pattern
- **Aggregates**: Game, GameSession
- **Use for**: Catalog management, play session tracking
- **Example**: Create game, start session, track players

### KnowledgeBase Pattern
- **Aggregates**: ChatThread, VectorDocument, Embedding
- **Use for**: AI/RAG, vector search, conversations
- **Example**: Create chat, search documents, ask questions

### Workflow/Config/Admin Patterns
- **Simpler contexts**: Configuration, alerts, workflows
- **Use for**: System configuration, monitoring, automation

---

## Common Commands

### Create Migration
```bash
cd apps/api/src/Api
export CONNECTIONSTRINGS__POSTGRES="Host=localhost,Database=meepleai,Username=postgres,Password=postgres"
dotnet ef migrations add DDD_YourMigrationName --output-dir Migrations
```

### Run Tests (Domain Only)
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~BoundedContexts"
```

### Add New Bounded Context
```bash
# 1. Create structure
mkdir -p src/Api/BoundedContexts/YourContext/{Domain,Application,Infrastructure}/{Entities,Commands,Persistence}

# 2. Follow pattern from GameManagement
cp -r BoundedContexts/GameManagement/Domain/Entities/Game.cs BoundedContexts/YourContext/Domain/Entities/YourAggregate.cs
# ... modify as needed

# 3. Register DI
# Add to Extensions/ApplicationServiceExtensions.cs
services.AddYourContext();
```

---

## File Locations Quick Reference

| What | Where | Example |
|------|-------|---------|
| **Aggregates** | `BoundedContexts/{Context}/Domain/Entities/` | `Game.cs` |
| **Value Objects** | `BoundedContexts/{Context}/Domain/ValueObjects/` | `GameTitle.cs` |
| **Commands** | `BoundedContexts/{Context}/Application/Commands/` | `CreateGameCommand.cs` |
| **Queries** | `BoundedContexts/{Context}/Application/Queries/` | `GetGameByIdQuery.cs` |
| **Handlers** | `BoundedContexts/{Context}/Application/Handlers/` | `CreateGameCommandHandler.cs` |
| **DTOs** | `BoundedContexts/{Context}/Application/DTOs/` | `GameDto.cs` |
| **Repositories** | `BoundedContexts/{Context}/Infrastructure/Persistence/` | `GameRepository.cs` |
| **Domain Tests** | `tests/BoundedContexts/{Context}/Domain/` | `GameDomainTests.cs` |
| **Entities (DB)** | `Infrastructure/Entities/` | `GameEntity.cs` |
| **DI Registration** | `BoundedContexts/{Context}/Infrastructure/DependencyInjection/` | `GameManagementServiceExtensions.cs` |

---

## Common Patterns

### Value Object Validation
```csharp
public GameTitle(string title)
{
    if (string.IsNullOrWhiteSpace(title))
        throw new ValidationException("Title cannot be empty");

    var trimmed = title.Trim();
    if (trimmed.Length > 200)
        throw new ValidationException("Title cannot exceed 200 characters");

    Value = trimmed;
}
```

### Aggregate Domain Method
```csharp
public void UpdateDetails(GameTitle? title = null, Publisher? publisher = null)
{
    if (title != null) Title = title;
    if (publisher != null) Publisher = publisher;

    // TODO: Add domain event GameUpdated
}
```

### Repository Mapping
```csharp
private static Game MapToDomain(GameEntity entity)
{
    var title = new GameTitle(entity.Name);
    var game = new Game(entity.Id, title, ...);

    // Override timestamps from DB
    var createdAtProp = typeof(Game).GetProperty("CreatedAt");
    createdAtProp?.SetValue(game, entity.CreatedAt);

    return game;
}
```

### JSON Collection Storage
```csharp
// Domain → Persistence
var playersJson = JsonSerializer.Serialize(
    domainEntity.Players.Select(p => new { p.Name, p.Order, p.Color })
);

// Persistence → Domain
var playerDtos = JsonSerializer.Deserialize<List<PlayerDto>>(entity.PlayersJson);
var players = playerDtos.Select(dto => new SessionPlayer(dto.Name, dto.Order, dto.Color));
```

---

## Testing Patterns

### Value Object Tests
```csharp
[Fact]
public void VO_WithValidData_CreatesSuccessfully() { }

[Fact]
public void VO_WithInvalidData_ThrowsValidationException() { }

[Fact]
public void VO_EqualityComparison_WorksCorrectly() { }
```

### Aggregate Tests
```csharp
[Fact]
public void Aggregate_DomainMethod_UpdatesState() { }

[Fact]
public void Aggregate_InvalidOperation_ThrowsException() { }

[Fact]
public void Aggregate_BusinessRule_EnforcesInvariant() { }
```

---

## Troubleshooting

### Build Errors

**"IRepository<T> requires 2 type arguments"**:
- Fix: Use `IRepository<YourAggregate, Guid>` (not `IRepository<YourAggregate>`)

**"Configuration is a namespace but is used as type"**:
- Fix: Rename class or use type alias: `using YourConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;`

**"Property names conflict"**:
- Fix: Rename constants (e.g., `MinMinutes` const vs `MinMinutes` property → `MinPlayTimeMinutes` const)

### Migration Errors

**"Database connection string not configured"**:
- Fix: `export CONNECTIONSTRINGS__POSTGRES="Host=localhost,Database=meepleai,Username=postgres,Password=postgres"`

**"Migration name already exists"**:
- Fix: Use unique name or remove duplicate with `dotnet ef migrations remove`

---

## Performance Tips

- **AsNoTracking**: All query handlers use `.AsNoTracking()` for read performance
- **JSON Collections**: Use for <1000 items (Players, Messages), faster than joins
- **Scoped Lifetime**: Repositories are Scoped (tied to request lifetime)
- **Singleton Domain Services**: Stateless domain services can be Singleton

---

## DDD Resources

- **Planning**: `docs/refactoring/ddd-architecture-plan.md` (16-week roadmap)
- **Progress**: `claudedocs/DDD-FOUNDATION-COMPLETE-2025-11-11.md` (session summary)
- **GameManagement**: `claudedocs/ddd-phase2-complete-final.md` (full implementation guide)
- **Pattern Examples**: Use GameManagement as gold standard for new contexts


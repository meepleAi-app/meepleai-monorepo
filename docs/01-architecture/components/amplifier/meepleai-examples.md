# Amplifier Examples per MeepleAI Development

Esempi pratici di comandi personalizzati e workflow automatizzati per accelerare lo sviluppo di MeepleAI.

## Esempio 1: /meepleai:new-service - Generare Service Completo

### Metacognitive Recipe

**Thinking Process**:
1. "What's the service responsibility?" → Gather requirements
2. "What are the dependencies?" → Identify required services
3. "What's the public interface?" → Design method signatures
4. "How do we test this?" → Plan test strategy
5. "How does it integrate?" → DI registration, docs update

### Creazione del Comando

**Tell Claude Code**:
```
/ultrathink-task Create /meepleai:new-service command that generates a complete ASP.NET Core service following MeepleAI patterns:

**Pattern Analysis** (from existing services):
Read these files for pattern reference:
- apps/api/src/Api/Services/RagService.cs
- apps/api/src/Api/Services/GameService.cs (LEGACY - removed in DDD migration - use CQRS handlers instead)
- apps/api/tests/Api.Tests/Unit/RagServiceTests.cs

**Generation Steps**:

1. Prompt user for:
   - Service name (e.g., "NotificationService")
   - Primary methods (e.g., "SendNotification, GetNotificationHistory")
   - Dependencies (e.g., "MeepleAiDbContext, IEmailService, ILogger")

2. Generate Interface (IMyService.cs):
   - Location: apps/api/src/Api/Services/
   - Pattern: All methods return Task<T> or Task
   - XML comments for each method
   - Nullable reference types

3. Generate Implementation (MyService.cs):
   - Constructor injection for dependencies
   - ILogger<T> for logging
   - Async all the way
   - Proper error handling with try-catch
   - Use `using` for IDisposable resources

4. Generate Unit Tests (MyServiceTests.cs):
   - Location: apps/api/tests/Api.Tests/Unit/
   - xUnit framework
   - Moq for mocking dependencies
   - AAA pattern (Arrange, Act, Assert)
   - Test success paths and edge cases
   - Coverage ≥90%

5. Generate Integration Tests (MyServiceEndpointsTests.cs):
   - Location: apps/api/tests/Api.Tests/Integration/
   - Testcontainers for dependencies
   - Test actual database operations
   - Test API endpoints if applicable

6. Update Program.cs:
   - Add DI registration: builder.Services.AddScoped<IMyService, MyService>();
   - Place in correct section (search for similar services)

7. Update CLAUDE.md:
   - Add service to Services table
   - Document key methods
   - Add to Key Features if significant

**Validation**:
- Run: dotnet build
- Run: dotnet test --filter MyService
- Verify: Coverage ≥90%
- Confirm: No warnings or errors
```

### Output Generato

Quando esegui `/meepleai:new-service NotificationService`:

**File 1**: `apps/api/src/Api/Services/INotificationService.cs`
```csharp
using MeepleAi.Api.Models;

namespace MeepleAi.Api.Services;

/// <summary>
/// Service for managing user notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification to a user.
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="message">Notification message</param>
    /// <param name="type">Notification type</param>
    /// <returns>Created notification</returns>
    Task<NotificationDto> SendNotificationAsync(
        Guid userId,
        string message,
        NotificationType type
    );

    /// <summary>
    /// Gets notification history for a user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="limit">Maximum number of notifications</param>
    /// <returns>List of notifications</returns>
    Task<List<NotificationDto>> GetNotificationHistoryAsync(
        Guid userId,
        int limit = 50
    );

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="notificationId">Notification ID</param>
    /// <param name="userId">User ID (for authorization)</param>
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
}
```

**File 2**: `apps/api/src/Api/Services/NotificationService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using MeepleAi.Api.Infrastructure;
using MeepleAi.Api.Infrastructure.Entities;
using MeepleAi.Api.Models;

namespace MeepleAi.Api.Services;

public class NotificationService : INotificationService
{
    private readonly MeepleAiDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        MeepleAiDbContext context,
        IEmailService emailService,
        ILogger<NotificationService> logger
    )
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<NotificationDto> SendNotificationAsync(
        Guid userId,
        string message,
        NotificationType type
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending {Type} notification to user {UserId}",
                type,
                userId
            );

            // Create notification entity
            var notification = new NotificationEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Send email if type requires it
            if (type == NotificationType.Email || type == NotificationType.Critical)
            {
                await _emailService.SendNotificationEmailAsync(userId, message);
            }

            _logger.LogInformation(
                "Notification {NotificationId} sent successfully",
                notification.Id
            );

            return new NotificationDto
            {
                Id = notification.Id,
                UserId = notification.UserId,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send notification to user {UserId}",
                userId
            );
            throw;
        }
    }

    public async Task<List<NotificationDto>> GetNotificationHistoryAsync(
        Guid userId,
        int limit = 50
    )
    {
        try
        {
            var notifications = await _context.Notifications
                .AsNoTracking()  // PERF-06: Read-only query
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    UserId = n.UserId,
                    Message = n.Message,
                    Type = n.Type,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return notifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to get notification history for user {UserId}",
                userId
            );
            throw;
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.Id == notificationId &&
                    n.UserId == userId  // Authorization check
                );

            if (notification == null)
            {
                throw new InvalidOperationException(
                    $"Notification {notificationId} not found for user {userId}"
                );
            }

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Notification {NotificationId} marked as read",
                notificationId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mark notification {NotificationId} as read",
                notificationId
            );
            throw;
        }
    }
}
```

**File 3**: `apps/api/tests/Api.Tests/Unit/NotificationServiceTests.cs`
```csharp
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using MeepleAi.Api.Services;
using MeepleAi.Api.Infrastructure;
using MeepleAi.Api.Models;

namespace MeepleAi.Api.Tests.Unit;

public class NotificationServiceTests
{
    private readonly Mock<MeepleAiDbContext> _contextMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _contextMock = new Mock<MeepleAiDbContext>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<NotificationService>>();

        _service = new NotificationService(
            _contextMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task SendNotificationAsync_CreatesNotification_ReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = "Test notification";
        var type = NotificationType.Info;

        // Act
        var result = await _service.SendNotificationAsync(userId, message, type);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(message, result.Message);
        Assert.Equal(type, result.Type);
        Assert.False(result.IsRead);

        _contextMock.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task SendNotificationAsync_EmailType_SendsEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var message = "Important notification";

        // Act
        await _service.SendNotificationAsync(userId, message, NotificationType.Email);

        // Assert
        _emailServiceMock.Verify(
            e => e.SendNotificationEmailAsync(userId, message),
            Times.Once
        );
    }

    [Fact]
    public async Task GetNotificationHistoryAsync_ReturnsUserNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notifications = CreateTestNotifications(userId, count: 5);

        _contextMock.Setup(c => c.Notifications)
            .ReturnsDbSet(notifications);

        // Act
        var result = await _service.GetNotificationHistoryAsync(userId, limit: 10);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.All(result, n => Assert.Equal(userId, n.UserId));
    }

    // ... altri 7-10 test per coverage ≥90%
}
```

**Tempo totale con `/meepleai:new-service`**: ~2 minuti
**Tempo manuale**: ~30 minuti
**Risparmio**: 93%

---

## Esempio 2: /meepleai:new-endpoint - API Endpoint Completo

### Creazione Comando

**Tell Claude Code**:
```
/ultrathink-task Create /meepleai:new-endpoint that generates API endpoint following MeepleAI patterns:

**Pattern Reference**:
apps/api/src/Api/Program.cs (search for "var v1Api = app.MapGroup")

**Generation Steps**:

1. Prompt for:
   - Endpoint name (e.g., "GetGameRules")
   - HTTP method (GET, POST, PUT, DELETE)
   - Route (e.g., "/games/{id}/rules")
   - Service method to call
   - Request/Response DTOs
   - Authorization (None, User, Editor, Admin)

2. Generate endpoint code for Program.cs:
   ```csharp
   v1Api.MapGet("/games/{id}/rules", async (
       Guid id,
       IGameService gameService,
       ClaimsPrincipal user
   ) =>
   {
       var rules = await gameService.GetGameRulesAsync(id);
       return Results.Ok(rules);
   })
   .RequireAuthorization()  // If auth needed
   .WithName("GetGameRules")
   .WithOpenApi();
   ```

3. Generate DTO if not exists:
   - Location: apps/api/src/Api/Models/
   - Nullable reference types
   - Validation attributes

4. Generate integration test:
   - Test success case
   - Test authorization (if applicable)
   - Test error cases (404, 400, etc.)

5. Update Swagger/OpenAPI comments

**Insert Logic**:
- Find correct position in Program.cs (after similar endpoints)
- Maintain alphabetical order within group
- Add proper spacing and comments
```

### Esempio d'Uso

```bash
/meepleai:new-endpoint

# Amplifier chiede:
Endpoint name: GetGamesByCategory
HTTP method: GET
Route: /games/category/{category}
Service: IGameService.GetByCategoryAsync
Request DTO: None (category as string)
Response DTO: List<GameDto>
Authorization: None (public endpoint)

# Output generato in Program.cs (linea corretta):
```

```csharp
// Generated by /meepleai:new-endpoint command
v1Api.MapGet("/games/category/{category}", async (
    string category,
    IGameService gameService,
    ILogger<Program> logger
) =>
{
    try
    {
        logger.LogInformation("Fetching games for category: {Category}", category);

        var games = await gameService.GetByCategoryAsync(category);

        if (games.Count == 0)
        {
            logger.LogInformation("No games found for category: {Category}", category);
            return Results.Ok(new List<GameDto>());  // Return empty list, not 404
        }

        return Results.Ok(games);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get games for category: {Category}", category);
        return Results.Problem(
            title: "Internal Server Error",
            statusCode: 500
        );
    }
})
.WithName("GetGamesByCategory")
.WithOpenApi(operation => new(operation)
{
    Summary = "Get games by category",
    Description = "Retrieves all games matching the specified category"
})
.Produces<List<GameDto>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status500InternalServerError);
```

**Test generato**: `apps/api/tests/Api.Tests/Integration/GameEndpointsTests.cs`
```csharp
[Fact]
public async Task GetGamesByCategory_ValidCategory_ReturnsGames()
{
    // Arrange
    var category = "strategy";
    await SeedTestGamesAsync(category);

    // Act
    var response = await _client.GetAsync($"/api/v1/games/category/{category}");

    // Assert
    response.EnsureSuccessStatusCode();

    var games = await response.Content.ReadFromJsonAsync<List<GameDto>>();
    Assert.NotNull(games);
    Assert.NotEmpty(games);
    Assert.All(games, g => Assert.Equal(category, g.Category));
}

[Fact]
public async Task GetGamesByCategory_InvalidCategory_ReturnsEmptyList()
{
    // Arrange
    var category = "nonexistent";

    // Act
    var response = await _client.GetAsync($"/api/v1/games/category/{category}");

    // Assert
    response.EnsureSuccessStatusCode();

    var games = await response.Content.ReadFromJsonAsync<List<GameDto>>();
    Assert.NotNull(games);
    Assert.Empty(games);
}
```

---

## Esempio 3: /meepleai:ui-component - React Component Accessibile

### Creazione Comando

**Tell Claude Code**:
```
/ultrathink-task Create /meepleai:ui-component for Next.js components:

**Pattern Reference**:
apps/web/src/components/ (existing components)

**Generation Steps**:

1. Prompt for:
   - Component name (e.g., "GameCard")
   - Component type (Button, Card, Modal, Form, etc.)
   - Props (e.g., "game: Game, onSelect: () => void")
   - State needed (e.g., "loading, error")
   - Accessibility requirements

2. Generate Component (components/GameCard.tsx):
   - TypeScript with proper types
   - React hooks (useState, useEffect as needed)
   - Accessibility (ARIA labels, keyboard nav)
   - Error boundaries
   - Loading states

3. Generate Test (components/__tests__/GameCard.test.tsx):
   - Jest + React Testing Library
   - Test rendering
   - Test user interactions
   - Test accessibility (axe-core)
   - Coverage ≥90%

4. Generate CSS Module (components/GameCard.module.css):
   - Follow existing Tailwind patterns
   - Responsive breakpoints
   - Dark mode support (if applicable)

5. API Integration (if needed):
   - Use @/lib/api client
   - Error handling
   - Loading states

**Validation**:
- Run: pnpm typecheck
- Run: pnpm test GameCard
- Run: pnpm lint
- Verify: Accessibility with axe
```

### Output Generato

**File 1**: `apps/web/src/components/GameCard.tsx`
```typescript
import { useState } from 'react';
import type { Game } from '@/types';
import styles from './GameCard.module.css';

export interface GameCardProps {
  game: Game;
  onSelect?: (gameId: string) => void;
  showDetails?: boolean;
}

export function GameCard({
  game,
  onSelect,
  showDetails = false
}: GameCardProps) {
  const [isHovered, setIsHovered] = useState(false);

  const handleClick = () => {
    onSelect?.(game.id);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      handleClick();
    }
  };

  return (
    <article
      className={styles.card}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      tabIndex={0}
      role="button"
      aria-label={`Select ${game.name}`}
    >
      <div className={styles.imageWrapper}>
        <img
          src={game.imageUrl ?? '/placeholder-game.png'}
          alt={`${game.name} cover`}
          className={styles.image}
          loading="lazy"
        />
      </div>

      <div className={styles.content}>
        <h3 className={styles.title}>{game.name}</h3>

        {showDetails && (
          <div className={styles.details}>
            <p className={styles.category}>{game.category}</p>
            <p className={styles.players}>
              {game.minPlayers}-{game.maxPlayers} players
            </p>
            <p className={styles.duration}>{game.playTime} min</p>
          </div>
        )}
      </div>

      {isHovered && (
        <div className={styles.overlay} aria-hidden="true">
          <span className={styles.viewRules}>View Rules →</span>
        </div>
      )}
    </article>
  );
}
```

**File 2**: `apps/web/src/components/__tests__/GameCard.test.tsx`
```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { GameCard } from '../GameCard';
import type { Game } from '@/types';

expect.extend(toHaveNoViolations);

const mockGame: Game = {
  id: '123',
  name: 'Catan',
  category: 'Strategy',
  minPlayers: 3,
  maxPlayers: 4,
  playTime: 90,
  imageUrl: 'https://example.com/catan.jpg'
};

describe('GameCard', () => {
  it('renders game information correctly', () => {
    render(<GameCard game={mockGame} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('calls onSelect when clicked', () => {
    const handleSelect = jest.fn();
    render(<GameCard game={mockGame} onSelect={handleSelect} />);

    const card = screen.getByRole('button');
    fireEvent.click(card);

    expect(handleSelect).toHaveBeenCalledWith('123');
  });

  it('calls onSelect when Enter key pressed', () => {
    const handleSelect = jest.fn();
    render(<GameCard game={mockGame} onSelect={handleSelect} />);

    const card = screen.getByRole('button');
    fireEvent.keyDown(card, { key: 'Enter' });

    expect(handleSelect).toHaveBeenCalledWith('123');
  });

  it('shows details when showDetails is true', () => {
    render(<GameCard game={mockGame} showDetails />);

    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('3-4 players')).toBeInTheDocument();
    expect(screen.getByText('90 min')).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<GameCard game={mockGame} />);
    const results = await axe(container);

    expect(results).toHaveNoViolations();
  });

  it('uses placeholder image when imageUrl is null', () => {
    const gameWithoutImage = { ...mockGame, imageUrl: null };
    render(<GameCard game={gameWithoutImage} />);

    const img = screen.getByAltText('Catan cover');
    expect(img).toHaveAttribute('src', '/placeholder-game.png');
  });
});
```

---

## Esempio 4: DDD Workflow Completo

### Feature: Game Collections

**Phase 1: Design** (`/ddd:1-plan`)

```bash
/ddd:1-plan

# Amplifier chiede:
Feature description: Users can organize games into custom collections, add/remove games, and share collections with others

# Output: docs/issue/FEATURE-collections.md
```

**Generated Design Doc**:
```markdown
# Feature: Game Collections

## Requirements
- Users create named collections
- Add/remove games from collections
- Share collections (public/private)
- Search within collections

## API Design

### Endpoints
POST   /api/v1/collections                    # Create collection
GET    /api/v1/collections                    # List user collections
GET    /api/v1/collections/{id}               # Get collection details
PUT    /api/v1/collections/{id}               # Update collection
DELETE /api/v1/collections/{id}               # Delete collection
POST   /api/v1/collections/{id}/games         # Add game to collection
DELETE /api/v1/collections/{id}/games/{gameId} # Remove game

### Models
```csharp
public class CollectionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public List<Guid> GameIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

## Database Schema
```sql
CREATE TABLE collections (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    is_public BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP,
    CONSTRAINT uk_user_collection_name UNIQUE (user_id, name)
);

CREATE INDEX idx_collections_user_id ON collections(user_id);
CREATE INDEX idx_collections_public ON collections(is_public) WHERE is_public = true;

CREATE TABLE collection_games (
    collection_id UUID NOT NULL REFERENCES collections(id) ON DELETE CASCADE,
    game_id UUID NOT NULL REFERENCES games(id) ON DELETE CASCADE,
    added_at TIMESTAMP DEFAULT NOW(),
    display_order INT,
    PRIMARY KEY (collection_id, game_id)
);
```

## Services

### ICollectionService
```csharp
Task<CollectionDto> CreateCollectionAsync(Guid userId, string name, string? description);
Task<List<CollectionDto>> GetUserCollectionsAsync(Guid userId);
Task<CollectionDto?> GetCollectionByIdAsync(Guid id, Guid? requestingUserId);
Task<CollectionDto> UpdateCollectionAsync(Guid id, Guid userId, UpdateCollectionRequest request);
Task DeleteCollectionAsync(Guid id, Guid userId);
Task AddGameToCollectionAsync(Guid collectionId, Guid gameId, Guid userId);
Task RemoveGameFromCollectionAsync(Guid collectionId, Guid gameId, Guid userId);
```

## Tests
- Unit: CollectionServiceTests (12 tests)
- Integration: CollectionEndpointsTests (15 tests)
- E2E: Collection management flow (Playwright)

## Frontend
- pages/collections.tsx (List view)
- components/CollectionCard.tsx
- components/AddToCollectionModal.tsx

## Acceptance Criteria
- [ ] User can create collection with name
- [ ] User can add/remove games
- [ ] Collections visible in user dashboard
- [ ] Public collections searchable by others
- [ ] Tests pass with ≥90% coverage
```

**Phase 2: Implementation** (`/ddd:2-impl`)

```bash
/ddd:2-impl

# Claude legge docs/issue/FEATURE-collections.md
# Genera automaticamente:
# ✅ CollectionEntity.cs
# ✅ ICollectionService.cs + CollectionService.cs
# ✅ CollectionDto.cs
# ✅ API endpoints in Program.cs
# ✅ Migration (20251101000000_AddCollections.cs)
# ✅ Unit tests (12 tests)
# ✅ Integration tests (15 tests)
# ✅ Frontend pages + components
# ✅ API client methods

# Esegue automaticamente:
dotnet build
dotnet test
pnpm typecheck
pnpm test

# Se fallisce: corregge automaticamente
# Se passa: Phase 2 completa
```

**Phase 3: Cleanup** (`/ddd:3-clean`)

```bash
/ddd:3-clean

# Cleanup automatico:
# ✅ Rimuove TODO comments
# ✅ Rimuove debug logging
# ✅ Aggiorna CLAUDE.md:
#    - Aggiungi CollectionService alla tabella Services
#    - Aggiungi endpoints alla tabella API
# ✅ Aggiorna database-schema.md
# ✅ Genera PR description da docs/issue/

# Output: Feature pronta per code review
```

**PR Description Generated**:
```markdown
# Feature: Game Collections

## Summary
Implements user collections feature allowing users to organize and share game lists.

## Changes
- **Backend**:
  - New CollectionService with 7 methods
  - 7 new API endpoints under `/api/v1/collections`
  - Database migration: collections + collection_games tables
  - 27 tests (12 unit + 15 integration) - 100% coverage

- **Frontend**:
  - Collections list page
  - Collection detail view
  - Add to collection modal
  - 18 Jest tests - 95% coverage

## Testing
- ✅ All backend tests passing (27/27)
- ✅ All frontend tests passing (18/18)
- ✅ E2E flow tested (create → add games → share)
- ✅ Accessibility validated (WCAG 2.1 AA)

## Documentation
- ✅ CLAUDE.md updated
- ✅ database-schema.md updated
- ✅ API endpoints documented
- ✅ Feature spec: docs/issue/FEATURE-collections.md

## Checklist
- [x] Tests pass
- [x] Coverage ≥90%
- [x] Docs updated
- [x] Migration reviewed
- [x] No TODOs in code
- [ ] Code review requested

---
Generated by Document-Driven Development workflow
Phase 1: Design (15 min) → Phase 2: Implementation (30 min) → Phase 3: Cleanup (5 min)
Total: 50 minutes
```

**Tempo con DDD workflow**: 50 minuti
**Tempo manuale**: 7-8 ore
**Risparmio**: 90%

---

## Esempio 5: Parallel Development con Worktree

### Scenario: Testare Due Approcci di Caching

**Approccio A**: Redis distributed cache
**Approccio B**: In-memory cache con invalidation

```bash
# Create worktrees
make worktree name=redis-cache branch=feat/redis-cache
make worktree name=memory-cache branch=feat/memory-cache

# Directory structure:
# D:/Repositories/meepleai-monorepo/              (main)
# D:/Repositories/meepleai-monorepo-redis-cache/   (variant A)
# D:/Repositories/meepleai-monorepo-memory-cache/  (variant B)

# Develop Approach A
cd ../meepleai-monorepo-redis-cache

# Tell Claude:
Implement distributed caching with Redis using HybridCacheService patterns from PERF-05

# Develop Approach B (in parallel)
cd ../meepleai-monorepo-memory-cache

# Tell Claude:
Implement in-memory caching with smart invalidation

# Compare results
cd ../meepleai-monorepo
git diff feat/redis-cache..feat/memory-cache -- apps/api/src/Api/Services/

# Benchmark both
cd ../meepleai-monorepo-redis-cache
dotnet test --filter BenchmarkTests

cd ../meepleai-monorepo-memory-cache
dotnet test --filter BenchmarkTests

# Results:
# Redis: 50ms p95 latency, distributed
# Memory: 5ms p95 latency, single-instance only

# Decision: Use Redis for production
cd ../meepleai-monorepo
git merge feat/redis-cache
make worktree-remove name=memory-cache

# Cleanup
git branch -D feat/memory-cache
```

**Vantaggi Worktree**:
- ✅ Sviluppo parallelo senza context switch
- ✅ Confronto diretto tra approcci
- ✅ Nessuna perdita di lavoro
- ✅ Decision data-driven (benchmark entrambi)

---

## Esempio 6: Design System con Design Agents

### Scenario: Creare Design System Consistente

```bash
# Step 1: Establish visual direction
Use the art-director agent to:
1. Analyze existing MeepleAI UI
2. Establish color palette (primary, secondary, accents)
3. Define typography scale
4. Create design principles document

# Output: docs/design/visual-direction.md

# Step 2: Create design tokens
Deploy design-system-architect to:
1. Convert visual direction to design tokens
2. Generate CSS variables
3. Create Tailwind config
4. Document token usage

# Output: Design tokens in apps/web/styles/tokens.css

# Step 3: Build component library
Use component-designer for each component:

/designer create Button component with:
- Primary, secondary, ghost variants
- Small, medium, large sizes
- Loading states
- Disabled states
- Icon support
- Accessibility (keyboard, screen reader)

# Repeat for: Card, Modal, Input, Select, etc.

# Step 4: Responsive strategy
Deploy responsive-strategist to:
1. Define breakpoints
2. Audit components for mobile
3. Create responsive utilities
4. Test on multiple devices

# Step 5: Animation system
Use animation-choreographer for:
1. Page transitions
2. Modal animations
3. Loading states
4. Micro-interactions

# Step 6: Voice & tone
Deploy voice-strategist to:
1. Audit all UI copy
2. Establish tone guidelines
3. Improve button labels
4. Enhance error messages
```

**Output**: Complete design system in 2-3 giorni vs 2-3 settimane manualmente

---

## Template AGENTS.md per MeepleAI

```markdown
# MeepleAI Development Context

## Project Mission
AI-powered board game rules assistant with RAG, vector search, and semantic Q&A.

## Stack & Architecture

### Backend (ASP.NET Core 9.0)
- **Pattern**: Service-based architecture with DI
- **Database**: PostgreSQL + EF Core migrations
- **AI/LLM**: OpenRouter API (Claude, GPT models)
- **Vector DB**: Qdrant for embeddings
- **Cache**: Redis + HybridCache (L1 + L2)
- **Observability**: Serilog, Seq, Prometheus, Grafana, OpenTelemetry

### Frontend (Next.js 14)
- **UI**: React with TypeScript
- **Styling**: Tailwind CSS + CSS Modules
- **API Client**: Custom client in lib/api.ts
- **Testing**: Jest + React Testing Library + Playwright

### Testing Standards
- **Backend**: xUnit + Moq + Testcontainers
- **Frontend**: Jest (90% coverage) + Playwright E2E
- **Patterns**: AAA (Arrange, Act, Assert)

## Code Conventions

### C# Patterns
```csharp
// All services async
public async Task<ResultDto> DoSomethingAsync(string param)
{
    try
    {
        _logger.LogInformation("Doing something: {Param}", param);

        // Use 'using' for IDisposable (CODE-01)
        using var scope = _scopeFactory.CreateScope();

        // AsNoTracking for read-only (PERF-06)
        var data = await _context.Items.AsNoTracking().ToListAsync();

        return new ResultDto { /* ... */ };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to do something");
        throw;
    }
}
```

### TypeScript Patterns
```typescript
// API calls with error handling
export async function fetchGame(id: string): Promise<Game> {
  try {
    const response = await api.get<Game>(`/games/${id}`);
    return response;
  } catch (error) {
    console.error('Failed to fetch game:', error);
    throw error;
  }
}

// Components with accessibility
export function MyComponent({ prop }: MyProps) {
  return (
    <div role="region" aria-label="Description">
      {/* ... */}
    </div>
  );
}
```

## Common Tasks

### New Service
`/meepleai:new-service` - Generate service with interface, implementation, DI, tests

### New Endpoint
`/meepleai:new-endpoint` - Add API endpoint with tests and OpenAPI docs

### UI Component
`/meepleai:ui-component` - Create React component with accessibility and tests

### Database Entity
`/meepleai:new-entity` - Generate EF Core entity with migration

### Feature Development
`/ddd:1-plan` → `/ddd:2-impl` → `/ddd:3-clean` - Complete feature workflow

## Quality Gates

### Before Commit
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes (coverage ≥90%)
- [ ] `pnpm typecheck` passes
- [ ] `pnpm test` passes (coverage ≥90%)
- [ ] `pnpm lint` passes
- [ ] No TODO comments in production code
- [ ] CLAUDE.md updated if needed

### Before PR
- [ ] All quality gates passed
- [ ] Docs synchronized
- [ ] Migration reviewed (if applicable)
- [ ] E2E tests pass
- [ ] Accessibility validated

## Project-Specific Principles

1. **Evidence > Assumptions**: Always verify, never guess
2. **Tests First**: No feature without tests
3. **Docs in Sync**: Update CLAUDE.md with every significant change
4. **Security**: OWASP Top 10 awareness, input validation
5. **Performance**: AsNoTracking, connection pooling, compression
6. **Accessibility**: WCAG 2.1 AA minimum
```

---

## Summary: Amplifier Value Proposition

### For Individual Developer
- ⏱️ **Time**: 100+ ore risparmiate/anno
- 📈 **Velocity**: +50-75% productivity
- ✅ **Quality**: 90-100% test coverage automatico
- 🎯 **Consistency**: Same patterns every time

### For Team
- 📚 **Knowledge Sharing**: Comandi codificano best practices
- 🔄 **Onboarding**: Nuovi dev produttivi subito
- 📊 **Quality**: Consistent code quality
- 🚀 **Velocity**: Team-wide productivity boost

### For MeepleAI Project
- 📝 **Docs**: Always synchronized
- 🏗️ **Architecture**: Patterns enforced automatically
- 🧪 **Testing**: Comprehensive coverage standard
- 🎨 **Design**: Consistent UI/UX

**Investment**: 4-5 ore setup + 10-15 ore comandi
**Break-even**: 2-3 settimane
**ROI Year 1**: 100-150 ore risparmiate

---

## Next Steps

1. **Read**: `amplifier-developer-workflow-guide.md` per setup pratico
2. **Try**: Crea primo comando `/meepleai:new-service`
3. **Expand**: Aggiungi comandi per task frequenti
4. **Share**: Commit comandi per team collaboration

**Ricorda**: Amplifier accelera lo **sviluppo**, Agent Lightning ottimizza gli **agenti AI**. Usali entrambi per massimo impatto!

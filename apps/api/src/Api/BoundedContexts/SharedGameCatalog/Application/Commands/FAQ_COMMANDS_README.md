# FAQ Commands for SharedGameCatalog

This document describes the three FAQ commands created for the SharedGameCatalog bounded context.

## Commands Created

### 1. AddGameFaqCommand
**File**: `AddGameFaqCommand.cs`
- **Type**: `ICommand<Guid>`
- **Returns**: The ID of the newly created FAQ
- **Parameters**:
  - `SharedGameId` (Guid): The game to add the FAQ to
  - `Question` (string): The FAQ question (max 500 chars)
  - `Answer` (string): The FAQ answer
  - `Order` (int): Display order (0 or higher)

**Handler**: `AddGameFaqCommandHandler.cs`
- Fetches the SharedGame by ID
- Creates a new GameFaq entity via GameFaq.Create()
- Calls game.AddFaq() to invoke domain logic and raise GameFaqAddedEvent
- Saves via repository.Update() and unitOfWork.SaveChangesAsync()
- Logs at Information level for successful creation

**Validator**: `AddGameFaqCommandValidator.cs`
- Validates SharedGameId is not empty
- Question: required, max 500 characters
- Answer: required (no length limit)
- Order: >= 0 (non-negative)

---

### 2. UpdateGameFaqCommand
**File**: `UpdateGameFaqCommand.cs`
- **Type**: `ICommand<Unit>`
- **Returns**: Unit (no result)
- **Parameters**:
  - `FaqId` (Guid): The FAQ to update
  - `Question` (string): New question (max 500 chars)
  - `Answer` (string): New answer
  - `Order` (int): New display order (0 or higher)

**Handler**: `UpdateGameFaqCommandHandler.cs`
- **Status**: NotImplemented - Requires repository extension
- **Issue**: Finding a game by FAQ ID is not currently supported
- **Recommended Solutions**:
  1. Add `GetGameByFaqIdAsync(Guid faqId)` method to ISharedGameRepository
  2. Add `GetFaqByIdWithGameAsync(Guid faqId)` direct FAQ query method
  3. Store SharedGameId on GameFaq entity for direct lookup
  4. Extend UpdateGameFaqCommand to require SharedGameId parameter

**Validator**: `UpdateGameFaqCommandValidator.cs`
- Validates FaqId is not empty
- Question: required, max 500 characters
- Answer: required (no length limit)
- Order: >= 0 (non-negative)

---

### 3. DeleteGameFaqCommand
**File**: `DeleteGameFaqCommand.cs`
- **Type**: `ICommand<Unit>`
- **Returns**: Unit (no result)
- **Parameters**:
  - `FaqId` (Guid): The FAQ to delete

**Handler**: `DeleteGameFaqCommandHandler.cs`
- **Status**: NotImplemented - Requires repository extension
- **Issue**: Same as Update - finding a game by FAQ ID is not currently supported
- **Recommended Solutions**:
  1. Add `GetGameByFaqIdAsync(Guid faqId)` method to ISharedGameRepository
  2. Add logic to remove FAQ from game aggregate's _faqs collection
  3. Raise GameFaqRemovedEvent from aggregate (needs domain method)
  4. Save via repository.Update() and unitOfWork.SaveChangesAsync()

**Validator**: `DeleteGameFaqCommandValidator.cs`
- Validates FaqId is not empty

---

## Architecture Decisions

### Injection Pattern
All handlers follow the existing pattern:
```csharp
public sealed class CommandHandler : ICommandHandler<Command, TResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommandHandler> _logger;

    // Constructor with null checks
    // Handle method with ArgumentNullException.ThrowIfNull
}
```

### Domain Event Pattern
- AddGameFaqCommand: Properly implements via SharedGame.AddFaq() → GameFaqAddedEvent
- UpdateGameFaqCommand & DeleteGameFaqCommand: Need domain methods to raise events

### Required Repository Extensions

To complete Update and Delete operations, extend `ISharedGameRepository`:

```csharp
/// <summary>
/// Gets a shared game by a FAQ contained within it.
/// </summary>
/// <param name="faqId">The FAQ ID to search for</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The game containing the FAQ, or null if not found</returns>
Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default);
```

Then implement in `SharedGameRepository`:
```csharp
public async Task<SharedGame?> GetGameByFaqIdAsync(Guid faqId, CancellationToken cancellationToken = default)
{
    var entity = await _context.Set<SharedGameEntity>()
        .AsNoTracking()
        .Where(g => g.GameFaqs.Any(f => f.Id == faqId) && !g.IsDeleted)
        .FirstOrDefaultAsync(cancellationToken)
        .ConfigureAwait(false);

    return entity is null ? null : MapToDomain(entity);
}
```

### Required Domain Methods

Add to `SharedGame` aggregate (similar to AddFaq):

```csharp
/// <summary>
/// Updates a FAQ in this game.
/// </summary>
public void UpdateFaq(GameFaq faq)
{
    ArgumentNullException.ThrowIfNull(faq);
    var existing = _faqs.FirstOrDefault(f => f.Id == faq.Id);
    if (existing is null)
        throw new InvalidOperationException($"FAQ {faq.Id} not found in this game");

    var index = _faqs.IndexOf(existing);
    _faqs[index] = faq;
    AddDomainEvent(new GameFaqUpdatedEvent(_id, faq.Id));
}

/// <summary>
/// Removes a FAQ from this game.
/// </summary>
public void RemoveFaq(Guid faqId)
{
    var faq = _faqs.FirstOrDefault(f => f.Id == faqId);
    if (faq is null)
        throw new InvalidOperationException($"FAQ {faqId} not found in this game");

    _faqs.Remove(faq);
    AddDomainEvent(new GameFaqRemovedEvent(_id, faqId));
}
```

### Required Domain Events

Add event classes:
```csharp
public sealed record GameFaqUpdatedEvent(Guid SharedGameId, Guid FaqId) : IDomainEvent;
public sealed record GameFaqRemovedEvent(Guid SharedGameId, Guid FaqId) : IDomainEvent;
```

---

## Testing Strategy

### Unit Tests for Validators
- Test boundary conditions (empty strings, negative order)
- Test max length constraints
- Test valid happy paths

### Integration Tests for Handlers
- AddGameFaqCommand: Verify game is loaded, FAQ is created, event is raised
- UpdateGameFaqCommand: Will require repository extension + domain method
- DeleteGameFaqCommand: Will require repository extension + domain method

### E2E Tests
- Create game → Add FAQ → Verify FAQ appears in game
- Update FAQ → Verify updates are persisted
- Delete FAQ → Verify FAQ is removed from game

---

## Migration Path

### Phase 1 (Done)
- Create command records with proper signatures
- Create validators with FluentValidation rules
- Create handler stubs

### Phase 2 (Required)
1. Extend `ISharedGameRepository` with `GetGameByFaqIdAsync`
2. Implement in `SharedGameRepository`
3. Add domain methods to `SharedGame`: UpdateFaq, RemoveFaq
4. Create domain event records: GameFaqUpdatedEvent, GameFaqRemovedEvent
5. Complete handler implementations

### Phase 3 (Recommended)
- Add HTTP endpoints for FAQ operations
- Add API documentation
- Add comprehensive test coverage
- Consider dedicated FAQ queries (GetFaqByIdQuery, GetGameFaqsQuery)

---

## Files Created

1. `AddGameFaqCommand.cs` (27 lines)
2. `AddGameFaqCommandValidator.cs` (19 lines)
3. `AddGameFaqCommandHandler.cs` (53 lines)
4. `UpdateGameFaqCommand.cs` (15 lines)
5. `UpdateGameFaqCommandValidator.cs` (19 lines)
6. `UpdateGameFaqCommandHandler.cs` (53 lines)
7. `DeleteGameFaqCommand.cs` (12 lines)
8. `DeleteGameFaqCommandValidator.cs` (15 lines)
9. `DeleteGameFaqCommandHandler.cs` (50 lines)

**Total**: 9 files, 263 lines of code

All files follow the exact patterns from:
- `CreateSharedGameCommand.cs`
- `CreateSharedGameCommandHandler.cs`
- `CreateSharedGameCommandValidator.cs`

# Ownership-Gated RAG Access — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gate RAG access behind game ownership declaration, with admin override for copyright-free games, and provide a quick-create tutor agent flow.

**Architecture:** Domain changes in UserLibrary + SharedGameCatalog BCs. New `IRagAccessService` in KnowledgeBase BC (cross-BC read-side query). 3 new endpoints + enforcement on 12 existing endpoints. 4 new frontend components with dialog-based ownership flow.

**Tech Stack:** .NET 9 (MediatR/CQRS, EF Core, FluentValidation) | Next.js 16 (React 19, shadcn/ui, Zustand) | PostgreSQL 16

**Spec:** `docs/superpowers/specs/2026-03-14-ownership-rag-access-design.md`

---

## Chunk 1: Domain Model + Migration

### Task 1: Add OwnershipDeclaredEvent domain event

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/OwnershipDeclaredEvent.cs`
- Reference: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/GameStateChangedEvent.cs` (follow this pattern)

- [ ] **Step 1: Create the domain event file**

Follow the existing `GameStateChangedEvent` pattern. The event carries audit data: EntryId, UserId, GameId, DeclaredAt.

```csharp
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

public sealed record OwnershipDeclaredEvent(
    Guid EntryId,
    Guid UserId,
    Guid GameId,
    DateTime DeclaredAt) : INotification;
```

- [ ] **Step 2: Verify it compiles**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/OwnershipDeclaredEvent.cs
git commit -m "feat(user-library): add OwnershipDeclaredEvent domain event"
```

---

### Task 2: Add DeclareOwnership() to UserLibraryEntry

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs`
- Reference: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/ValueObjects/GameState.cs` (GameStateType enum: Nuovo=0, InPrestito=1, Wishlist=2, Owned=3)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/UserLibraryEntryDeclareOwnershipTests.cs`

- [ ] **Step 1: Write failing tests for DeclareOwnership()**

Create test file with 5 test cases covering the state diagram from the spec:

```csharp
namespace Api.Tests.BoundedContexts.UserLibrary.Domain;

public class UserLibraryEntryDeclareOwnershipTests
{
    // Test 1: Nuovo → Owned, sets OwnershipDeclaredAt
    // Test 2: Wishlist → throws DomainException
    // Test 3: InPrestito → stays InPrestito, sets OwnershipDeclaredAt
    // Test 4: Already Owned (no OwnershipDeclaredAt) → sets it, no state change
    // Test 5: Idempotent — already declared → no-op, no new event
}
```

Check existing test files in `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/` for patterns on how to construct a `UserLibraryEntry` in tests (factory methods, test builders).

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UserLibraryEntryDeclareOwnershipTests" --no-restore`
Expected: FAIL — method does not exist

- [ ] **Step 3: Add OwnershipDeclaredAt property + HasDeclaredOwnership computed property + DeclareOwnership() method**

Open `UserLibraryEntry.cs`. Add:

```csharp
// New property (persisted)
public DateTime? OwnershipDeclaredAt { get; private set; }

// Computed (NOT a DB column)
public bool HasDeclaredOwnership => OwnershipDeclaredAt != null;

// New method
public void DeclareOwnership()
{
    if (CurrentState.Type == GameStateType.Wishlist)
        throw new DomainException("Cannot declare ownership of a wishlist game");

    if (HasDeclaredOwnership)
        return; // Idempotent

    OwnershipDeclaredAt = DateTime.UtcNow;

    // For InPrestito, don't change state (already physically owned)
    if (CurrentState.Type != GameStateType.InPrestito
        && CurrentState.Type != GameStateType.Owned)
    {
        MarkAsOwned(); // Reuses existing state machine
    }

    AddDomainEvent(new OwnershipDeclaredEvent(Id, UserId, GameId, OwnershipDeclaredAt.Value));
}
```

**IMPORTANT**: Check exact property/method names in the existing entity. The state field might be `CurrentState`, `State`, or `GameState`. The `MarkAsOwned()` might be `ChangeState(GameState.Owned())`. Read the file first.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~UserLibraryEntryDeclareOwnershipTests"`
Expected: All 5 PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/UserLibraryEntryDeclareOwnershipTests.cs
git commit -m "feat(user-library): add DeclareOwnership() with audit trail and state transitions"
```

---

### Task 3: Add IsRagPublic to SharedGame

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/SharedGame.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/SharedGameEntityConfiguration.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/SharedGameRagPublicTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// Test: SetRagPublicAccess toggles IsRagPublic
// Test: IsRagPublic defaults to false
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Add IsRagPublic property + SetRagPublicAccess() method to SharedGame entity**

```csharp
public bool IsRagPublic { get; private set; } = false;

public void SetRagPublicAccess(bool isPublic)
{
    IsRagPublic = isPublic;
    UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 4: Add EF configuration for IsRagPublic**

In `SharedGameEntityConfiguration.cs`, add:
```csharp
builder.Property(e => e.IsRagPublic)
    .HasDefaultValue(false)
    .IsRequired();
```

- [ ] **Step 5: Run tests to verify they pass**

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(shared-game): add IsRagPublic flag for admin RAG access override"
```

---

### Task 4: Add OwnershipDeclaredAt to UserLibraryEntry EF config

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/UserLibrary/UserLibraryEntryEntityConfiguration.cs`

- [ ] **Step 1: Add EF property mapping**

```csharp
builder.Property(e => e.OwnershipDeclaredAt)
    .IsRequired(false);

// Ensure HasDeclaredOwnership is NOT mapped (it's computed in C#)
builder.Ignore(e => e.HasDeclaredOwnership);
```

- [ ] **Step 2: Verify build succeeds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(ef): add OwnershipDeclaredAt EF configuration"
```

---

### Task 5: Create EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/[timestamp]_AddOwnershipRagAccess.cs` (auto-generated)

- [ ] **Step 1: Generate migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddOwnershipRagAccess`

- [ ] **Step 2: Review generated migration**

Open the generated file. Verify it contains:
- `ALTER TABLE shared_games ADD COLUMN is_rag_public BOOLEAN NOT NULL DEFAULT FALSE`
- `ALTER TABLE user_library_entries ADD COLUMN ownership_declared_at TIMESTAMPTZ NULL`

- [ ] **Step 3: Add backfill SQL to the migration's Up() method**

After the schema changes, add raw SQL for backfill:

```csharp
// Backfill: existing Owned (3) and InPrestito (1) entries get OwnershipDeclaredAt
migrationBuilder.Sql(
    "UPDATE user_library_entries SET ownership_declared_at = NOW() WHERE current_state IN (1, 3)");
```

**NOTE**: Verify the actual column name for `current_state` by checking the EF config. It might be `game_state`, `current_state`, or `state`. Read `UserLibraryEntryEntityConfiguration.cs` to confirm.

- [ ] **Step 4: Apply migration locally**

Run: `cd apps/api/src/Api && dotnet ef database update`
Expected: Migration applied successfully

- [ ] **Step 5: Write integration test for backfill**

Create: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Integration/MigrationBackfillTests.cs`

```csharp
// Test: Seed DB with entries in states Owned(3) and InPrestito(1) with null OwnershipDeclaredAt.
// Apply migration. Verify OwnershipDeclaredAt is now set for Owned and InPrestito entries.
// Verify Nuovo and Wishlist entries still have null OwnershipDeclaredAt.
```

Use Testcontainers (follow existing integration test patterns in the project).

- [ ] **Step 6: Run the backfill test**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MigrationBackfillTests"`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(migration): add IsRagPublic and OwnershipDeclaredAt with backfill and verification test"
```

---

### Task 6: Extend SharedGameDto with IsRagPublic

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs`
- Modify: SharedGameDto mapping/projection

- [ ] **Step 1: Add field to SharedGameDto**

```csharp
public bool IsRagPublic { get; init; }
```

- [ ] **Step 2: Update SharedGameDto mapping/projection**

Find where `SharedGameDto` is projected from the entity. Add `IsRagPublic = entity.IsRagPublic` to the projection.

- [ ] **Step 3: Add OwnershipDeclaredAt to UserLibraryEntryDto (without HasRagAccess yet)**

In `UserLibraryEntryDto.cs`, add:
```csharp
public DateTime? OwnershipDeclaredAt { get; init; }
```

Update the mapping to include `OwnershipDeclaredAt = entry.OwnershipDeclaredAt`.

**NOTE**: `HasRagAccess` is NOT added here — it depends on `IRagAccessService` which is created in Task 7. It will be added in Task 7b.

- [ ] **Step 4: Verify build + existing tests pass**

Run: `cd apps/api/src/Api && dotnet build && dotnet test --filter "Category=Unit" --no-restore`

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dto): extend SharedGameDto with IsRagPublic, UserLibraryEntryDto with OwnershipDeclaredAt"
```

---

## Chunk 2: RagAccessService + New Backend Endpoints

### Task 7: Create IRagAccessService interface + implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagAccessService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Services/RagAccessService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Services/RagAccessServiceTests.cs`

- [ ] **Step 1: Write failing tests (6 cases)**

```csharp
// CanAccessRagAsync tests:
// Test 1: Admin role → returns true (no DB calls needed)
// Test 2: IsRagPublic game → returns true (even without ownership)
// Test 3: HasDeclaredOwnership → returns true
// Test 4: No admin, not public, no ownership → returns false

// GetAccessibleKbCardsAsync tests:
// Test 5: With access → returns all indexed VectorDocument IDs for the game
// Test 6: Without access → returns empty list
```

Mock the repositories. Use the existing repository interfaces for SharedGame and UserLibraryEntry. Check what interfaces exist:
- Look in `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/` for `ISharedGameRepository`
- Look in `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/` for `IUserLibraryRepository`

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Create the interface**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

public interface IRagAccessService
{
    Task<bool> CanAccessRagAsync(Guid userId, Guid gameId, UserRole role);
    Task<List<Guid>> GetAccessibleKbCardsAsync(Guid userId, Guid gameId, UserRole role);
}
```

**NOTE**: Check what `UserRole` type exists in the codebase. It might be an enum in Authentication BC or a string. Search: `grep -r "UserRole" apps/api/src/Api/BoundedContexts/ --include="*.cs" -l`

- [ ] **Step 4: Create the implementation**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

public class RagAccessService : IRagAccessService
{
    // Inject: ISharedGameRepository (or equivalent), IUserLibraryRepository (or equivalent)
    // The cross-BC read uses direct DbContext access (read-side query service pattern)

    public async Task<bool> CanAccessRagAsync(Guid userId, Guid gameId, UserRole role)
    {
        // Rule 1: Admin bypass
        if (role == UserRole.Admin) return true;

        // Rule 2: Public RAG (admin override)
        // Query SharedGame.IsRagPublic by gameId
        var isPublic = await _dbContext.SharedGames
            .Where(sg => sg.Id == gameId)
            .Select(sg => sg.IsRagPublic)
            .FirstOrDefaultAsync();
        if (isPublic) return true;

        // Rule 3: User declared ownership
        var hasDeclared = await _dbContext.UserLibraryEntries
            .Where(e => e.UserId == userId && e.GameId == gameId)
            .Select(e => e.OwnershipDeclaredAt != null)
            .FirstOrDefaultAsync();
        if (hasDeclared) return true;

        // Rule 4: Denied
        return false;
    }

    public async Task<List<Guid>> GetAccessibleKbCardsAsync(
        Guid userId, Guid gameId, UserRole role)
    {
        var canAccess = await CanAccessRagAsync(userId, gameId, role);
        if (!canAccess) return new List<Guid>();

        // Return all indexed VectorDocument IDs for this game
        return await _dbContext.VectorDocuments
            .Where(vd => (vd.SharedGameId == gameId || vd.GameId == gameId)
                && vd.ProcessingState == ProcessingState.Ready)
            .Select(vd => vd.Id)
            .ToListAsync();
    }
}
```

**IMPORTANT**: Read the actual DbContext to find exact DbSet names, VectorDocument entity fields, and ProcessingState enum. The pattern above is illustrative — adapt to actual repository/DbContext patterns used.

- [ ] **Step 5: Register in DI (scoped lifetime for request-scoped caching)**

In `KnowledgeBaseServiceExtensions.cs`, add:
```csharp
services.AddScoped<IRagAccessService, RagAccessService>();
```

- [ ] **Step 6: Run tests to verify they pass (all 6)**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(kb): add IRagAccessService with cross-BC read-side access checks"
```

---

### Task 7b: Wire HasRagAccess into UserLibraryEntryDto

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserLibraryEntryDto.cs`
- Modify: The query handler that projects UserLibraryEntryDto (find via grep for `UserLibraryEntryDto`)

This task depends on Task 7 (IRagAccessService must exist).

- [ ] **Step 1: Add HasRagAccess to UserLibraryEntryDto**

```csharp
public bool HasRagAccess { get; init; }
```

- [ ] **Step 2: Update the query handler to compute HasRagAccess**

In the query handler that lists/gets library entries, inject `IRagAccessService`. For each entry, call `CanAccessRagAsync(userId, entry.GameId, role)` to populate `HasRagAccess`.

**NOTE**: For list queries returning many entries, calling `CanAccessRagAsync` per-item is N+1. Consider a batch approach: query all SharedGame.IsRagPublic flags in one call, then compute HasRagAccess in memory using: `isAdmin || isRagPublic || entry.OwnershipDeclaredAt != null`.

- [ ] **Step 3: Verify build + existing tests pass**

Run: `cd apps/api/src/Api && dotnet build && dotnet test --filter "Category=Unit" --no-restore`

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(dto): wire HasRagAccess into UserLibraryEntryDto via IRagAccessService"
```

---

### Task 8: DeclareOwnershipCommand + Handler + Validator + Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/DeclareOwnershipCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Handlers/DeclareOwnershipCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/DeclareOwnershipCommandValidator.cs`
- Modify: Routing file for library endpoints (check `apps/api/src/Api/Routing/` — likely `UserLibraryEndpoints.cs` or `PrivateGameEndpoints.cs`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Handlers/DeclareOwnershipCommandHandlerTests.cs`

- [ ] **Step 1: Write failing tests for the handler**

```csharp
// Test 1: Happy path — Nuovo entry → declares ownership, returns OwnershipResult with hasRagAccess
// Test 2: Entry not found → throws NotFoundException (404)
// Test 3: Wishlist entry → throws ConflictException (409)
// Test 4: Already declared → idempotent, returns current state
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Create Command record**

```csharp
public sealed record DeclareOwnershipCommand(
    Guid UserId,
    Guid GameId,
    UserRole UserRole) : IRequest<DeclareOwnershipResult>;

public sealed record DeclareOwnershipResult(
    string GameState,
    DateTime? OwnershipDeclaredAt,
    bool HasRagAccess,
    int KbCardCount,
    bool IsRagPublic);
```

- [ ] **Step 4: Create Validator**

```csharp
public class DeclareOwnershipCommandValidator : AbstractValidator<DeclareOwnershipCommand>
{
    public DeclareOwnershipCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

- [ ] **Step 5: Create Handler**

Handler logic:
1. Get UserLibraryEntry by UserId + GameId → NotFoundException if not found
2. If Wishlist → throw ConflictException
3. Call entry.DeclareOwnership()
4. Save via UnitOfWork
5. Check RAG access + KB card count for response
6. Return DeclareOwnershipResult

- [ ] **Step 6: Add endpoint route**

```csharp
group.MapPost("/{gameId:guid}/declare-ownership", async (
    Guid gameId, IMediator mediator, HttpContext context) =>
{
    var session = context.RequireSession();
    var result = await mediator.Send(new DeclareOwnershipCommand(
        session.UserId, gameId, session.UserRole));
    return Results.Ok(result);
});
```

**IMPORTANT**: Check how `RequireSession()` works in existing endpoints. It might return a tuple `(authorized, session, error)` for admin, or a session object directly. Read existing library endpoints for the pattern.

- [ ] **Step 7: Run tests to verify they pass**

- [ ] **Step 8: Commit**

```bash
git commit -m "feat(user-library): add POST /library/{gameId}/declare-ownership endpoint"
```

---

### Task 9: QuickCreateAgentCommand + Handler + Validator + Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/QuickCreateAgentCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/QuickCreateAgentCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/QuickCreateAgentCommandValidator.cs`
- Modify: Agent routing file (check `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Handlers/QuickCreateAgentCommandHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Test 1: Happy path — creates agent (type=Tutor) + chat thread, returns IDs
// Test 2: No RAG access → throws ForbiddenException (403)
// Test 3: Auto-selects all indexed KB cards for the game
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Create Command + Result records**

```csharp
public sealed record QuickCreateAgentCommand(
    Guid UserId,
    Guid GameId,
    Guid? SharedGameId,
    UserRole UserRole,
    string UserTier) : IRequest<QuickCreateAgentResult>;

public sealed record QuickCreateAgentResult(
    Guid AgentId,
    Guid ChatThreadId,
    string AgentName,
    int KbCardCount);
```

- [ ] **Step 4: Create Validator**

```csharp
public class QuickCreateAgentCommandValidator : AbstractValidator<QuickCreateAgentCommand>
{
    public QuickCreateAgentCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SharedGameId)
            .Must(id => id == null || id != Guid.Empty)
            .When(x => x.SharedGameId.HasValue)
            .WithMessage("SharedGameId must be a valid UUID if provided");
    }
}
```

- [ ] **Step 5: Create Handler**

Handler logic:
1. `CanAccessRagAsync(userId, gameId, role)` → 403 if false
2. Get KB cards: query VectorDocuments where SharedGameId == sharedGameId (or GameId == gameId) and status == Ready
3. Get game name from SharedGame or PrivateGame
4. Create AgentDefinition: type=Tutor, name="Tutor {gameName}", kbCardIds=all indexed cards
5. Create ChatSession linked to agent
6. Return QuickCreateAgentResult

**IMPORTANT**: Study `CreateAgentWithSetupCommandHandler.cs` closely — it does similar multi-step creation. Follow that exact pattern for agent + chat thread creation.

- [ ] **Step 6: Add endpoint route**

In the agent routing file, add:
```csharp
group.MapPost("/quick-create", async (
    QuickCreateAgentRequest request, IMediator mediator, HttpContext context) =>
{
    var session = context.RequireSession();
    var result = await mediator.Send(new QuickCreateAgentCommand(
        session.UserId, request.GameId, request.SharedGameId,
        session.UserRole, session.UserTier));
    return Results.Ok(result);
});
```

- [ ] **Step 7: Run tests to verify they pass**

- [ ] **Step 8: Commit**

```bash
git commit -m "feat(kb): add POST /agents/quick-create endpoint for one-click tutor creation"
```

---

### Task 10: SetRagPublicAccessCommand + Admin endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/SetRagPublicAccessCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Handlers/SetRagPublicAccessCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Validators/SetRagPublicAccessCommandValidator.cs`
- Modify: Admin routing file (check `apps/api/src/Api/Routing/AdminSharedGameEndpoints.cs` or similar)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Handlers/SetRagPublicAccessCommandHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// Test 1: Toggle on → SharedGame.IsRagPublic = true
// Test 2: Toggle off → SharedGame.IsRagPublic = false
// Test 3: Game not found → NotFoundException
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Create Command + Validator + Handler + Endpoint**

Follow exact same pattern as Task 8. The handler:
1. Get SharedGame by ID → 404 if not found
2. Call `sharedGame.SetRagPublicAccess(command.IsRagPublic)`
3. Save
4. Return 204 No Content

Endpoint uses `RequireAdminSession()`.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(admin): add PUT /admin/shared-games/{id}/rag-access endpoint"
```

---

## Chunk 3: Backend Enforcement on Existing Endpoints

### Task 11: Add RAG access enforcement to agent creation handlers

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/CreateUserAgentCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/CreateAgentWithSetupCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Handlers/CreateUserAgentRagAccessTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Handlers/CreateAgentWithSetupRagAccessTests.cs`

- [ ] **Step 1: Write failing tests**

For each handler, test:
```csharp
// Test: No RAG access → throws ForbiddenException
// Test: With RAG access (ownership declared) → proceeds normally
// Test: With IsRagPublic game → proceeds even without ownership
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Inject IRagAccessService and add check at top of Handle()**

In each handler, add before existing logic:
```csharp
var canAccess = await _ragAccessService.CanAccessRagAsync(
    command.UserId, command.GameId, command.UserRole);
if (!canAccess)
    throw new ForbiddenException("Devi possedere il gioco per creare un agente");
```

- [ ] **Step 4: Run ALL existing tests to verify no regressions**

Run: `cd apps/api/src/Api && dotnet test --no-restore`
Expected: All existing tests still pass (may need to mock IRagAccessService in existing test setups)

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(kb): enforce RAG access on agent creation endpoints"
```

---

### Task 12: Add RAG access enforcement to chat/query endpoints

**Files — 10 endpoints to enforce:**

| # | Endpoint | Likely handler location |
|---|----------|------------------------|
| 1 | POST /chat/sessions | `Routing/ChatSessionEndpoints.cs` (inline or handler in KnowledgeBase) |
| 2 | POST /chat/sessions/{sessionId}/messages | `Routing/ChatSessionEndpoints.cs` |
| 3 | POST /agents/{id}/chat | `Routing/KnowledgeBaseEndpoints.cs` |
| 4 | POST /agents/query | `Routing/KnowledgeBaseEndpoints.cs` |
| 5 | POST /agents/{id}/invoke | `Routing/KnowledgeBaseEndpoints.cs` |
| 6 | POST /agents/chat/ask | `Routing/KnowledgeBaseEndpoints.cs` |
| 7 | POST /agents/tutor/query | `Routing/KnowledgeBaseEndpoints.cs` |
| 8 | POST /agents/{id}/arbiter | `Routing/KnowledgeBaseEndpoints.cs` |
| 9 | POST /chat-threads/{threadId}/messages | `Routing/KnowledgeBaseEndpoints.cs` |
| 10 | POST /game-sessions/{sessionId}/chat/ask-agent | `Routing/SessionTrackingEndpoints.cs` |

For endpoints using MediatR handlers, the handler is in `BoundedContexts/KnowledgeBase/Application/Handlers/`. For inline Minimal API endpoints, add the check directly in the routing lambda.

- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Handlers/ChatRagAccessEnforcementTests.cs`

- [ ] **Step 1: Identify all 10 handler files**

For each endpoint above, open the routing file and determine:
- Is it inline (lambda in MapPost)? → Add check in the lambda
- Does it dispatch via MediatR? → Add check in the command handler

List all files that need modification. Grep for the route patterns:
```
grep -rn "chat/sessions" apps/api/src/Api/Routing/ --include="*.cs"
grep -rn "agents.*chat\|agents.*query\|agents.*invoke\|agents.*arbiter" apps/api/src/Api/Routing/ --include="*.cs"
grep -rn "chat-threads" apps/api/src/Api/Routing/ --include="*.cs"
grep -rn "ask-agent" apps/api/src/Api/Routing/ --include="*.cs"
```

- [ ] **Step 2: Write failing test for each endpoint category**

At minimum:
```csharp
// Test: POST /chat/sessions — no RAG access → 403
// Test: POST /agents/{id}/chat — agent for non-owned game → 403
// Test: POST /agents/query — non-owned game → 403
```

- [ ] **Step 3: Run tests to verify they fail**

- [ ] **Step 4: Add enforcement to each handler**

For endpoints that have a direct `gameId` in the request, add the check directly. For endpoints that resolve `agent.GameId` or `session.GameId`, first resolve the entity, extract the gameId, then check.

Pattern for indirect resolution:
```csharp
var agent = await _agentRepository.GetByIdAsync(agentId);
if (agent == null) throw new NotFoundException(...);

var canAccess = await _ragAccessService.CanAccessRagAsync(
    userId, agent.GameId, userRole);
if (!canAccess)
    throw new ForbiddenException("Accesso RAG non autorizzato");
```

- [ ] **Step 5: Run ALL tests**

Run: `cd apps/api/src/Api && dotnet test --no-restore`

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(kb): enforce RAG access on all chat and query endpoints"
```

---

## Chunk 4: Frontend — API Client + Components

### Task 13: Add API client methods

**Files:**
- Modify: `apps/web/src/lib/api/clients/libraryClient.ts`
- Modify: `apps/web/src/lib/api/clients/agentsClient.ts`
- Modify: `apps/web/src/lib/api/clients/adminClient.ts`
- Modify: `apps/web/src/lib/api/index.ts` (if new client methods need wiring)
- Test: `apps/web/src/lib/api/__tests__/libraryClient.test.ts` (or add to existing)
- Test: `apps/web/src/lib/api/__tests__/agentsClient.test.ts`

- [ ] **Step 1: Write failing tests for new API methods**

```typescript
// libraryClient.declareOwnership(gameId) → POST /api/v1/library/{gameId}/declare-ownership
// agentsClient.quickCreateTutor(gameId, sharedGameId?) → POST /api/v1/agents/quick-create
// adminClient.setRagPublicAccess(sharedGameId, isPublic) → PUT /api/v1/admin/shared-games/{id}/rag-access
```

Follow existing test patterns in `apps/web/src/lib/api/__tests__/`. Use `vi.hoisted()` + `vi.mock()` for mocking fetch.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run src/lib/api/__tests__/`

- [ ] **Step 3: Add methods to clients**

In `libraryClient.ts`:
```typescript
async declareOwnership(gameId: string): Promise<OwnershipResult> {
  const response = await this.httpClient.post(`/api/v1/library/${gameId}/declare-ownership`);
  return response.json();
}
```

In `agentsClient.ts`:
```typescript
async quickCreateTutor(gameId: string, sharedGameId?: string): Promise<QuickCreateResult> {
  const response = await this.httpClient.post('/api/v1/agents/quick-create', {
    body: JSON.stringify({ gameId, sharedGameId }),
  });
  return response.json();
}
```

In `adminClient.ts`:
```typescript
async setRagPublicAccess(sharedGameId: string, isPublic: boolean): Promise<void> {
  await this.httpClient.put(`/api/v1/admin/shared-games/${sharedGameId}/rag-access`, {
    body: JSON.stringify({ isRagPublic: isPublic }),
  });
}
```

**IMPORTANT**: Read the existing client files first. They may use a different HTTP abstraction (custom httpClient, fetch wrapper, etc.). Follow the exact pattern.

- [ ] **Step 4: Add TypeScript types for new endpoints**

Add to the appropriate types file (check where other API response types live):
```typescript
export interface OwnershipResult {
  gameState: string;
  ownershipDeclaredAt: string | null;
  hasRagAccess: boolean;
  kbCardCount: number;
  isRagPublic: boolean;
}

export interface QuickCreateResult {
  agentId: string;
  chatThreadId: string;
  agentName: string;
  kbCardCount: number;
}
```

- [ ] **Step 5: Update EXISTING TypeScript types for extended DTOs**

Find the existing TypeScript interfaces for `UserLibraryEntry` and `SharedGame` responses (grep for them in `apps/web/src/lib/api/` or `apps/web/src/types/`). Add the new fields:

In the UserLibraryEntry type:
```typescript
ownershipDeclaredAt: string | null;
hasRagAccess: boolean;
```

In the SharedGame type:
```typescript
isRagPublic: boolean;
```

If Zod schemas exist for these types, update them too.

- [ ] **Step 6: Run tests to verify they pass**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(web): add API methods and update DTO types for ownership/RAG access"
```

---

### Task 14: Create OwnershipDeclarationDialog component

**Files:**
- Create: `apps/web/src/components/library/OwnershipDeclarationDialog.tsx`
- Test: `apps/web/src/components/library/__tests__/OwnershipDeclarationDialog.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// Test 1: Renders title "Possiedi {gameName}?", 3 benefits, checkbox, disabled button
// Test 2: Checking checkbox enables "Conferma Possesso" button
// Test 3: Clicking confirm calls declareOwnership API
// Test 4: "Non ancora" closes the dialog (calls onOpenChange(false))
// Test 5: Shows spinner during API call, disables controls
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run src/components/library/__tests__/OwnershipDeclarationDialog.test.tsx`

- [ ] **Step 3: Implement component**

Use shadcn `AlertDialog` (check import path: likely `@/components/ui/overlays/alert-dialog-primitives`).

Structure:
- AlertDialogHeader: title "Possiedi {gameName}?"
- 3 benefit items with icons
- Checkbox with label "Confermo di possedere questo gioco"
- `<details>` for "Perche' lo chiediamo?" legal text
- AlertDialogFooter: "Non ancora" + "Conferma Possesso" (disabled until checkbox)
- Loading state: `useMutation` or `useState` for isPending

**Design tokens**: Follow project patterns — `bg-white/70 backdrop-blur-md`, `font-quicksand` for headings, amber accents.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(web): add OwnershipDeclarationDialog component"
```

---

### Task 15: Create OwnershipConfirmationDialog component

**Files:**
- Create: `apps/web/src/components/library/OwnershipConfirmationDialog.tsx`
- Test: `apps/web/src/components/library/__tests__/OwnershipConfirmationDialog.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// Test 1: With kbCardCount > 0 — shows KB card chips + "Crea veloce" + "Personalizza"
// Test 2: With kbCardCount == 0 — shows "Tutor non ancora disponibile", only Close
// Test 3: "Crea veloce" calls quickCreateTutor and navigates to /chat/[threadId]
// Test 4: "Personalizza" navigates to /chat/agents/create?gameId=X&step=2
// Test 5: Shows spinner during quick-create, disables both CTAs
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement component**

Use shadcn `Dialog`. Two rendering branches based on `ownershipResult.kbCardCount`.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(web): add OwnershipConfirmationDialog component"
```

---

### Task 16: Create DeclareOwnershipButton + RagAccessBadge

**Files:**
- Create: `apps/web/src/components/library/DeclareOwnershipButton.tsx`
- Create: `apps/web/src/components/library/RagAccessBadge.tsx`
- Test: `apps/web/src/components/library/__tests__/DeclareOwnershipButton.test.tsx`
- Test: `apps/web/src/components/library/__tests__/RagAccessBadge.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// DeclareOwnershipButton:
// Test 1: Visible when gameState === 'Nuovo'
// Test 2: Hidden when gameState === 'Owned'
// Test 3: Hidden when gameState === 'Wishlist'
// Test 4: Hidden when gameState === 'InPrestito'
// Test 5: Click opens OwnershipDeclarationDialog

// RagAccessBadge:
// Test 1: hasRagAccess=true → green unlocked badge
// Test 2: hasRagAccess=false → gray locked badge
// Test 3: isRagPublic=true → special "public" badge
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement both components**

`DeclareOwnershipButton`: Manages dialog open state. Renders button only when `gameState === 'Nuovo'`. Opens `OwnershipDeclarationDialog`, chains to `OwnershipConfirmationDialog` on success.

`RagAccessBadge`: Simple badge component with 3 visual states.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(web): add DeclareOwnershipButton and RagAccessBadge components"
```

---

## Chunk 5: Frontend Integration + Wizard Support

### Task 17: Integrate DeclareOwnershipButton into library page

**Files:**
- Modify: Library collection page component (find the component that renders game cards in `/library` Collection tab — likely in `apps/web/src/app/(authenticated)/library/` or a subcomponent)
- Modify: Game detail page `apps/web/src/app/(authenticated)/games/[id]/page.tsx` (or its client component)

- [ ] **Step 1: Find the exact component that renders MeepleCard in library Collection tab**

Read the library page files to find where `MeepleCard` (or `MeepleGameCatalogCard`) is rendered for collection items.

- [ ] **Step 2: Add DeclareOwnershipButton to the card or card actions**

Render `DeclareOwnershipButton` below or within each game card when the entry's gameState is available. Pass the required props: `gameId`, `gameName`, `sharedGameId`, `gameState`, `onOwnershipDeclared` (refresh the list).

- [ ] **Step 3: Add RagAccessBadge to the card**

Show RAG access status using the new DTO fields (`hasRagAccess`, `isRagPublic`).

- [ ] **Step 4: Add DeclareOwnershipButton to game detail page**

On `/games/[id]`, show the button if the game is in the user's library with state Nuovo.

- [ ] **Step 5: Verify visually in dev**

Run: `cd apps/web && pnpm dev`
Navigate to `/library`, verify the button appears for Nuovo games.

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(web): integrate DeclareOwnershipButton into library and game detail pages"
```

---

### Task 18: Add step query param support to agent creation wizard

**Files:**
- Modify: `apps/web/src/app/(chat)/chat/agents/create/page.tsx`

- [ ] **Step 1: Read the current wizard implementation**

Understand how the 4-step wizard works. Find the step state management.

- [ ] **Step 2: Add gameId + step query param handling**

```typescript
const searchParams = useSearchParams();
const initialGameId = searchParams.get('gameId');
const initialStep = parseInt(searchParams.get('step') || '1', 10);

// If gameId provided and step >= 2, pre-select game and skip to that step
useEffect(() => {
  if (initialGameId && initialStep >= 2) {
    setSelectedGameId(initialGameId);
    setCurrentStep(initialStep);
  }
}, [initialGameId, initialStep]);
```

- [ ] **Step 3: Verify navigation from OwnershipConfirmationDialog → wizard works**

Test manually: declare ownership → click "Personalizza" → wizard opens at step 2 with game pre-selected.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(web): support gameId and step query params in agent creation wizard"
```

---

## Chunk 6: Manual Testing + Final Verification

### Task 19: Run all automated tests

- [ ] **Step 1: Run backend tests**

Run: `cd apps/api/src/Api && dotnet test --no-restore`
Expected: All pass (existing + ~24 new)

- [ ] **Step 2: Run frontend tests**

Run: `cd apps/web && pnpm test -- --run`
Expected: All pass (existing + ~12 new)

- [ ] **Step 3: Run TypeScript check**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 4: Fix any failures**

- [ ] **Step 5: Commit any fixes**

---

### Task 20: Manual browser test — Prerequisites setup

- [ ] **Step 1: Start infrastructure**

Run: `cd infra && docker compose up -d postgres qdrant redis`

- [ ] **Step 2: Apply migration**

Run: `cd apps/api/src/Api && dotnet ef database update`

- [ ] **Step 3: Start API**

Run: `cd apps/api/src/Api && dotnet run` (Terminal 1, port 8080)

- [ ] **Step 4: Start Web**

Run: `cd apps/web && pnpm dev` (Terminal 2, port 3000)

- [ ] **Step 5: Login as admin, upload catan_en_rulebook.pdf**

Navigate to admin → shared games → find Catan → upload `data/rulebook/catan_en_rulebook.pdf`. Wait for indexing to complete (status: Ready).

- [ ] **Step 6: Create/verify test user account (non-admin)**

---

### Task 21: Execute TC-01 through TC-05

Follow the test plan from the spec exactly. For each test case:

- [ ] **Step 1: TC-01 — Happy Path (10 steps)**

Execute all 10 steps from the spec. Verify each expected result. Take note of any failures.

- [ ] **Step 2: TC-02 — No Ownership → Cannot Create Agent**

- [ ] **Step 3: TC-03 — No Ownership → Cannot Chat (API direct)**

Use browser DevTools Network tab to send the direct API call.

- [ ] **Step 4: TC-04 — No KB Cards → No Tutor CTA**

Pick a game in the catalog that has NO uploaded PDFs.

- [ ] **Step 5: TC-05 — Public RAG → Accessible Without Ownership**

As admin: set IsRagPublic = true for a game (via the new admin endpoint or direct DB). Then test as user.

- [ ] **Step 6: Document results**

Note any issues found. Fix and re-test.

---

### Task 22: Final commit + PR

- [ ] **Step 1: Verify all changes are committed**

Run: `git status` — should show clean working tree.

- [ ] **Step 2: Push branch**

Run: `git push -u origin [branch-name]`

- [ ] **Step 3: Create PR**

Target: parent branch (detect with `git config branch.[current].parent`).

Include in PR description:
- Link to spec: `docs/superpowers/specs/2026-03-14-ownership-rag-access-design.md`
- Test results summary (TC-01 through TC-05)
- New endpoint list
- Breaking changes (enforcement on existing endpoints may affect existing users with agents)

- [ ] **Step 4: Code review**

Run: `/code-review:code-review [PR-URL]`

- [ ] **Step 5: Fix code review issues (if any, max 3 iterations)**

- [ ] **Step 6: Merge PR**

- [ ] **Step 7: Cleanup branch**

```bash
git checkout [parent-branch] && git pull && git branch -D [feature-branch]
```

# Agent System Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse three overlapping "agent" concepts into `AgentDefinition`, reduce strategies from 13 to 4, delete all chess and dead command paths, fix the broken arbiter retrieval, and migrate the `agent_sessions` FK from typology to definition.

**Architecture:** `AgentDefinition` absorbs `Agent` + `AgentTypology`; `AgentSession.TypologyId` → `AgentDefinitionId`; execution paths reduced from 6 to 3 (ChatWithSession, PlaygroundChat, AskArbiter). All deleted types are fully removed — no deprecation wrappers.

**Tech Stack:** .NET 9 / ASP.NET Core, EF Core 9, PostgreSQL, MediatR, xUnit + Testcontainers

**Spec:** `docs/superpowers/specs/2026-03-30-agent-system-simplification-design.md`

---

## File Map

### Create
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentSessionDefinitionChangedEvent.cs`

### Modify
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/AgentStrategy.cs` — remove 9 factory methods
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs` — add 5 fields
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs` — map 5 new columns
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentSession.cs` — TypologyId→AgentDefinitionId, remove AgentId
- `apps/api/src/Api/Infrastructure/Entities/AgentSessionEntity.cs` — same rename/removal
- `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/AgentSessionEntityConfiguration.cs` — update FK
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs` — update session mapping
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/LaunchSessionAgentCommandHandler.cs` — remove AgentId param
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/LaunchSessionAgentCommand.cs` — AgentId+TypologyId → AgentDefinitionId
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/UpdateAgentSessionTypologyCommand.cs` — rename to UpdateAgentSessionDefinitionCommand
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/UpdateAgentSessionTypologyCommandHandler.cs` — use IAgentDefinitionRepository
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskArbiterCommand.cs` — AgentId → AgentDefinitionId
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/AskArbiterCommandHandler.cs` — use IAgentDefinitionRepository + IHybridSearchService
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs` — use IAgentDefinitionRepository
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateGameAgentCommand.cs` — TypologyId → AgentDefinitionId
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateGameAgentCommandHandler.cs` — use IAgentDefinitionRepository
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs` — use IAgentDefinitionRepository
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/SaveAgentConfigCommandHandler.cs` — use IAgentDefinitionRepository
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/SaveAgentConfigCommand.cs` — TypologyId → AgentDefinitionId
- `apps/api/src/Api/Infrastructure/Seeders/Catalog/AgentSeeder.cs` — rewrite to seed AgentDefinition
- `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/ListTablesHandler.cs` — remove typology_prompt_templates entry

### Delete (backend)
**Dead execution paths:**
- `Application/Commands/InvokeAgentCommand.cs` + Handler + Validator
- `Application/Commands/AskAgentQuestionCommand.cs` + Handler + Validator
- `Application/Commands/InvokeChessAgentCommandHandler.cs` + Command + DTOs (ChessAgentResponse, ChessAnalysis, ParsedChessResponse)

**AgentTypology application layer (all commands/handlers/validators/queries/DTOs):**
- ProposeAgentTypologyCommand + Handler + Validator
- ApproveAgentTypologyCommand + Handler + Validator
- RejectAgentTypologyCommand + Handler + Validator
- CreateAgentTypologyCommand + Handler + Validator
- CreateAgentTypologyWithPhaseModelsCommand + Handler + Validator
- UpdateAgentTypologyCommand + Handler + Validator
- DeleteAgentTypologyCommand + Handler + Validator
- TestAgentTypologyCommand + Handler + Validator
- GetTypologyByIdQuery + Handler
- GetAll*AgentTypologies*Query + Handlers
- AgentTypologyDto, AgentTypologyWithCostDto, StrategyPhaseModelsDto, PhaseModelConfigurationDto, CostEstimateDto
- Routing/AdminAgentTypologyEndpoints.cs
- Routing/AgentTypologyEndpoints.cs
- Domain/Events/TypologyApprovedEvent.cs
- Domain/Events/TypologyRejectedEvent.cs
- Domain/Events/AgentSessionTypologyChangedEvent.cs (replaced by AgentSessionDefinitionChangedEvent)

**Agent application layer:**
- SearchChessKnowledgeQuery + Handler (chess)

**Domain layer:**
- Domain/Entities/Agent.cs
- Domain/Entities/AgentTypology.cs
- Domain/Entities/TypologyPromptTemplate.cs
- Domain/ValueObjects/TypologyStatus.cs
- Domain/ValueObjects/TypologyProfile.cs
- Domain/Repositories/IAgentRepository.cs
- Domain/Repositories/IAgentTypologyRepository.cs

**Infrastructure layer:**
- Infrastructure/Entities/KnowledgeBase/AgentEntity.cs
- Infrastructure/Entities/KnowledgeBase/AgentConfigurationEntity.cs
- Infrastructure/Entities/KnowledgeBase/AgentGameStateSnapshotEntity.cs
- Infrastructure/Entities/KnowledgeBase/AgentTypologyEntity.cs
- Infrastructure/Entities/KnowledgeBase/TypologyPromptTemplateEntity.cs
- Infrastructure/EntityConfigurations/KnowledgeBase/AgentEntityConfiguration.cs
- Infrastructure/EntityConfigurations/KnowledgeBase/AgentConfigurationEntityConfiguration.cs
- Infrastructure/EntityConfigurations/KnowledgeBase/AgentGameStateSnapshotEntityConfiguration.cs (if exists)
- Infrastructure/EntityConfigurations/KnowledgeBase/AgentTypologyEntityConfiguration.cs
- Infrastructure/EntityConfigurations/KnowledgeBase/TypologyPromptTemplateEntityConfiguration.cs
- Infrastructure/Persistence/AgentRepository.cs (KB-scoped)
- Infrastructure/Persistence/AgentTypologyRepository.cs (KB-scoped)
- Infrastructure/Persistence/AgentGameStateSnapshotRepository.cs
- MeepleAiDbContext lines: AgentTypologies, TypologyPromptTemplates, AgentEntity set registrations

---

## Task 1: Simplify AgentStrategy

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/AgentStrategy.cs`

- [ ] **Step 1.1: Remove the 9 obsolete factory methods**

Open `AgentStrategy.cs`. Delete these methods entirely (along with the private static fields `DefaultModels` and `ValidationLayers` that only they used):
- `SingleModel()`
- `VectorOnly()`
- `MultiModelConsensus()`
- `CitationValidation()`
- `ConfidenceScoring()`
- `IterativeRAG()`
- `ChainOfThoughtRAG()`
- `QueryDecomposition()`
- `MultiAgentRAG()`
- `RAGFusion()`
- `StepBackPrompting()`
- `QueryExpansion()`
- `Custom()`

**Keep:** `RetrievalOnly()`, `HybridSearch()`, `SentenceWindowRAG()`, `ColBERTReranking()`, plus `GetParameter<T>()`, `HasParameter()`, `ToString()`, and the constructors/properties.

Also delete the two private static fields at lines 20-27:
```csharp
// DELETE these:
private static readonly string[] DefaultModels = { "gpt-4", "claude-3-opus" };
private static readonly string[] ValidationLayers = { ... };
```

- [ ] **Step 1.2: Verify build**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep -E "error|warning CS" | grep "error" | head -20
```

Expected: build errors only from callers of deleted methods (typology commands etc.). We'll fix those in later tasks — for now just ensure no errors in `AgentStrategy.cs` itself.

- [ ] **Step 1.3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/AgentStrategy.cs
git commit -m "refactor(kb): reduce AgentStrategy from 13 to 4 factory methods"
```

---

## Task 2: Extend AgentDefinition Domain Entity

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs`

- [ ] **Step 2.1: Add 5 new backing fields + properties to AgentDefinition**

After the `_chatLanguage` field declaration (line ~34), add:

```csharp
private bool _isSystemDefined;
private string? _typologySlug;
private Guid? _gameId;
private int _invocationCount;
private DateTime? _lastInvokedAt;
```

After the `ChatLanguage` property (line ~127), add:

```csharp
/// <summary>Seeded by the system; not editable by users.</summary>
public bool IsSystemDefined => _isSystemDefined;

/// <summary>"arbitro" | "game-master" | "chat" — fast lookup for system agents.</summary>
public string? TypologySlug => _typologySlug;

/// <summary>Optional game association.</summary>
public Guid? GameId => _gameId;

/// <summary>Total number of times this agent was invoked.</summary>
public int InvocationCount => _invocationCount;

/// <summary>Timestamp of last invocation.</summary>
public DateTime? LastInvokedAt => _lastInvokedAt;
```

- [ ] **Step 2.2: Add RecordInvocation() method**

After the `Unpublish()` method (end of class), add:

```csharp
/// <summary>Records a new invocation, incrementing count and updating timestamp.</summary>
public void RecordInvocation()
{
    _invocationCount++;
    _lastInvokedAt = DateTime.UtcNow;
    _updatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 2.3: Update the internal reconstitution constructor**

The constructor at line 178 needs the 5 new parameters. Add them after `updatedAt`:

```csharp
internal AgentDefinition(
    Guid id,
    string name,
    string description,
    string typeValue,
    string typeDescription,
    AgentDefinitionConfig config,
    string strategyJson,
    string promptsJson,
    string toolsJson,
    bool isActive,
    AgentDefinitionStatus status,
    DateTime createdAt,
    DateTime? updatedAt,
    bool isSystemDefined = false,
    string? typologySlug = null,
    Guid? gameId = null,
    int invocationCount = 0,
    DateTime? lastInvokedAt = null) : base(id)
{
    // ... existing assignments ...
    _isSystemDefined = isSystemDefined;
    _typologySlug = typologySlug;
    _gameId = gameId;
    _invocationCount = invocationCount;
    _lastInvokedAt = lastInvokedAt;
}
```

- [ ] **Step 2.4: Update AgentDefinitionConfiguration.cs to map the 5 new columns**

At the end of the `Configure` method (before the closing `}`), add:

```csharp
// New columns from agent system simplification
builder.Property<bool>("_isSystemDefined")
    .HasColumnName("is_system_defined")
    .HasDefaultValue(false)
    .IsRequired();

builder.Property<string?>("_typologySlug")
    .HasColumnName("typology_slug")
    .HasMaxLength(50);

builder.Property<Guid?>("_gameId")
    .HasColumnName("game_id");

builder.Property<int>("_invocationCount")
    .HasColumnName("invocation_count")
    .HasDefaultValue(0)
    .IsRequired();

builder.Property<DateTime?>("_lastInvokedAt")
    .HasColumnName("last_invoked_at");
```

- [ ] **Step 2.5: Build check**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
```

Expected: no new errors in these files.

- [ ] **Step 2.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentDefinition.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/AgentDefinitionConfiguration.cs
git commit -m "feat(kb): add IsSystemDefined, TypologySlug, GameId, InvocationCount, LastInvokedAt to AgentDefinition"
```

---

## Task 3: Rewrite AgentSession (TypologyId→AgentDefinitionId, remove AgentId)

**Files:**
- Create: `.../Domain/Events/AgentSessionDefinitionChangedEvent.cs`
- Modify: `.../Domain/Entities/AgentSession.cs`
- Modify: `Infrastructure/Entities/AgentSessionEntity.cs`
- Modify: `Infrastructure/EntityConfigurations/KnowledgeBase/AgentSessionEntityConfiguration.cs`
- Modify: `.../Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs`
- Modify: `.../Application/Commands/LaunchSessionAgentCommand.cs`
- Modify: `.../Application/Commands/LaunchSessionAgentCommandHandler.cs`

- [ ] **Step 3.1: Create AgentSessionDefinitionChangedEvent**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentSessionDefinitionChangedEvent.cs
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed record AgentSessionDefinitionChangedEvent(
    Guid AgentSessionId,
    Guid NewAgentDefinitionId) : DomainEvent;
```

- [ ] **Step 3.2: Update AgentSession domain entity**

Replace the file content. Key changes from the current version:
- Remove `AgentId` property and its validation
- Rename `TypologyId` → `AgentDefinitionId`
- Update constructor signature (remove `agentId:`, rename `typologyId:` → `agentDefinitionId:`)
- Rename `UpdateTypology` → `UpdateAgentDefinition`, use new event

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentSession.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

internal sealed class AgentSession : AggregateRoot<Guid>
{
    public Guid AgentDefinitionId { get; private set; }
    public Guid GameSessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public AgentConfig Config { get; private set; }
    public GameState CurrentGameState { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public bool IsActive { get; private set; }

#pragma warning disable CS8618
    private AgentSession() : base() { }
#pragma warning restore CS8618

    public AgentSession(
        Guid id,
        Guid agentDefinitionId,
        Guid gameSessionId,
        Guid userId,
        Guid gameId,
        GameState initialState,
        AgentConfig? config = null) : base(id)
    {
        if (agentDefinitionId == Guid.Empty)
            throw new ArgumentException("AgentDefinitionId cannot be empty", nameof(agentDefinitionId));
        if (gameSessionId == Guid.Empty)
            throw new ArgumentException("GameSessionId cannot be empty", nameof(gameSessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        ArgumentNullException.ThrowIfNull(initialState);

        AgentDefinitionId = agentDefinitionId;
        GameSessionId = gameSessionId;
        UserId = userId;
        GameId = gameId;
        Config = config ?? AgentConfig.Default();
        CurrentGameState = initialState;
        StartedAt = DateTime.UtcNow;
        IsActive = true;

        AddDomainEvent(new AgentSessionCreatedEvent(id, agentDefinitionId, gameSessionId, userId));
    }

    public void UpdateGameState(GameState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);
        if (!IsActive)
            throw new ConflictException("Cannot update state of inactive agent session");
        CurrentGameState = newState;
        AddDomainEvent(new AgentSessionStateUpdatedEvent(Id, newState.ToJson()));
    }

    public void UpdateAgentDefinition(Guid newAgentDefinitionId)
    {
        if (!IsActive)
            throw new ConflictException("Cannot update definition of inactive agent session");
        if (newAgentDefinitionId == Guid.Empty)
            throw new ArgumentException("AgentDefinitionId cannot be empty", nameof(newAgentDefinitionId));
        AgentDefinitionId = newAgentDefinitionId;
        AddDomainEvent(new AgentSessionDefinitionChangedEvent(Id, newAgentDefinitionId));
    }

    public void UpdateConfig(AgentConfig newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);
        if (!IsActive)
            throw new ConflictException("Cannot update config of inactive agent session");
        Config = newConfig;
        AddDomainEvent(new AgentSessionConfigUpdatedEvent(Id, newConfig.ToJson()));
    }

    public void End()
    {
        if (!IsActive)
            throw new ConflictException("Agent session is already ended");
        IsActive = false;
        EndedAt = DateTime.UtcNow;
        AddDomainEvent(new AgentSessionEndedEvent(Id, Duration));
    }

    public TimeSpan Duration =>
        EndedAt.HasValue ? EndedAt.Value - StartedAt : DateTime.UtcNow - StartedAt;
}
```

Note: `AgentSessionCreatedEvent` currently takes `agentId` as 2nd parameter. You need to check its signature and update the 2nd arg from `agentId` to `agentDefinitionId`. Find the event file and update the parameter name (the value passed is the same Guid, just semantically renamed).

- [ ] **Step 3.3: Update AgentSessionEntity (infrastructure)**

In `apps/api/src/Api/Infrastructure/Entities/AgentSessionEntity.cs`:
- Remove `AgentId` property
- Remove `AgentEntity Agent` navigation property
- Rename `TypologyId` → `AgentDefinitionId`
- Remove `AgentTypologyEntity Typology` navigation property (will be added back after AgentDefinition entity is available, or use a nullable placeholder)

```csharp
public class AgentSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgentDefinitionId { get; set; }      // renamed from TypologyId
    public Guid GameSessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public string CurrentGameStateJson { get; set; } = "{}";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public GameSessionEntity GameSession { get; set; } = default!;
    public UserEntity User { get; set; } = default!;
    public GameEntity Game { get; set; } = default!;
    // AgentDefinition nav property: add after AgentDefinition is accessible
    // from global infrastructure (or reference domain entity directly)
}
```

- [ ] **Step 3.4: Update AgentSessionEntityConfiguration**

In `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/AgentSessionEntityConfiguration.cs`:
- Remove the `AgentId` FK section entirely
- Replace the `TypologyId` FK section:

```csharp
// REMOVE: FK to Agent (lines ~21-25)
// builder.Property(e => e.AgentId).IsRequired();
// builder.HasOne(e => e.Agent)...

// REPLACE: FK to AgentTypology → FK to AgentDefinition
builder.Property(e => e.AgentDefinitionId)
    .HasColumnName("agent_definition_id")
    .IsRequired();
// No nav property until AgentDefinition is in same infra context
// The FK constraint is enforced at DB level via migration

// REMOVE: AgentId index
// builder.HasIndex(e => e.AgentId)...
```

- [ ] **Step 3.5: Update KnowledgeBaseMappers.cs**

Find the `ToDomain` and `ToEntity` mapper methods for AgentSession (around lines 360-400).

In `ToDomain`:
```csharp
// Before:
var session = new AgentSession(
    id: entity.Id,
    agentId: entity.AgentId,
    ...
    typologyId: entity.TypologyId,
    ...);

// After:
var session = new AgentSession(
    id: entity.Id,
    agentDefinitionId: entity.AgentDefinitionId,
    ...);
```

In `ToEntity`:
```csharp
// Before:
AgentId = session.AgentId,
TypologyId = session.TypologyId,

// After:
AgentDefinitionId = session.AgentDefinitionId,
```

- [ ] **Step 3.6: Update LaunchSessionAgentCommand**

Find the command record/class. Replace `AgentId` and `TypologyId` with `AgentDefinitionId`:

```csharp
public record LaunchSessionAgentCommand(
    Guid AgentDefinitionId,    // was: AgentId + TypologyId separately
    Guid GameSessionId,
    Guid UserId,
    Guid GameId,
    string InitialGameStateJson) : IRequest<Guid>;
```

- [ ] **Step 3.7: Update LaunchSessionAgentCommandHandler**

```csharp
var agentSession = new AgentSession(
    id: Guid.NewGuid(),
    agentDefinitionId: request.AgentDefinitionId,
    gameSessionId: request.GameSessionId,
    userId: request.UserId,
    gameId: request.GameId,
    initialState: initialGameState
);
```

- [ ] **Step 3.8: Build check**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -20
```

Fix any remaining compile errors in the modified files before continuing.

- [ ] **Step 3.9: Commit**

```bash
git add -A
git commit -m "refactor(kb): rename AgentSession.TypologyId → AgentDefinitionId, remove AgentId"
```

---

## Task 4: Update UpdateAgentSessionTypology → UpdateAgentSessionDefinition

**Files:**
- Modify: `.../Application/Commands/UpdateAgentSessionTypologyCommand.cs`
- Modify: `.../Application/Commands/UpdateAgentSessionTypologyCommandHandler.cs`

- [ ] **Step 4.1: Rename command — update `UpdateAgentSessionTypologyCommand.cs`**

The file name stays (rename is optional), but update the record name and parameter:

```csharp
public record UpdateAgentSessionDefinitionCommand(
    Guid AgentSessionId,
    Guid NewAgentDefinitionId) : IRequest;
```

- [ ] **Step 4.2: Update handler — replace `IAgentTypologyRepository` with `IAgentDefinitionRepository`**

```csharp
internal sealed class UpdateAgentSessionDefinitionCommandHandler
    : IRequestHandler<UpdateAgentSessionDefinitionCommand>
{
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentDefinitionRepository _definitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateAgentSessionDefinitionCommandHandler> _logger;

    // constructor updated accordingly

    public async Task Handle(UpdateAgentSessionDefinitionCommand request, CancellationToken cancellationToken)
    {
        var agentSession = await _sessionRepository
            .GetByIdAsync(request.AgentSessionId, cancellationToken).ConfigureAwait(false);

        if (agentSession == null)
            throw new NotFoundException("AgentSession", request.AgentSessionId.ToString());

        var definition = await _definitionRepository
            .GetByIdAsync(request.NewAgentDefinitionId, cancellationToken).ConfigureAwait(false);

        if (definition == null)
            throw new NotFoundException("AgentDefinition", request.NewAgentDefinitionId.ToString());

        if (!definition.IsActive)
            throw new ValidationException($"AgentDefinition {request.NewAgentDefinitionId} is not active");

        agentSession.UpdateAgentDefinition(request.NewAgentDefinitionId);

        await _sessionRepository.UpdateAsync(agentSession, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4.3: Find and update the endpoint that calls this command**

```bash
grep -rn "UpdateAgentSessionTypologyCommand\|UpdateAgentSessionDefinitionCommand" \
  apps/api/src/Api/Routing --include="*.cs"
```

Update the endpoint to send `UpdateAgentSessionDefinitionCommand` instead.

- [ ] **Step 4.4: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
git add -A
git commit -m "refactor(kb): UpdateAgentSessionTypology → UpdateAgentSessionDefinition"
```

---

## Task 5: Fix AskArbiterCommandHandler

**Files:**
- Modify: `.../Application/Commands/AskArbiterCommand.cs`
- Modify: `.../Application/Commands/AskArbiterCommandHandler.cs`

The bug: `var searchResult = SearchResult.CreateSuccess(new List<SearchResultItem>())` — hardcoded empty results.
The fix: replace `IAgentRepository` with `IAgentDefinitionRepository`, wire `IHybridSearchService`.

- [ ] **Step 5.1: Update AskArbiterCommand**

```csharp
public record AskArbiterCommand : IRequest<ArbiterVerdictDto>
{
    public required Guid AgentDefinitionId { get; init; }  // was AgentId
    public required Guid SessionId { get; init; }
    public required string Situation { get; init; }
    public required string PositionA { get; init; }
    public required string PositionB { get; init; }
    public Guid UserId { get; init; }
}
```

- [ ] **Step 5.2: Update AskArbiterCommandHandler**

Replace `IAgentRepository` with `IAgentDefinitionRepository` and wire `IHybridSearchService`.

Key changes to the `Handle` method:

```csharp
// Replace IAgentRepository field and constructor param with:
private readonly IAgentDefinitionRepository _definitionRepository;
private readonly IHybridSearchService _hybridSearchService;

// In Handle():
// Step 1: Load AgentDefinition instead of Agent
var definition = await _definitionRepository
    .GetByIdAsync(command.AgentDefinitionId, cancellationToken).ConfigureAwait(false);

if (definition == null)
    throw new NotFoundException("AgentDefinition", command.AgentDefinitionId.ToString());

// RAG access check uses definition.GameId
if (definition.GameId is not null && definition.GameId != Guid.Empty)
{
    var canAccess = await _ragAccessService.CanAccessRagAsync(
        command.UserId, definition.GameId.Value, UserRole.User, cancellationToken).ConfigureAwait(false);
    if (!canAccess)
        throw new ForbiddenException("Accesso RAG non autorizzato");
}

// Step 2: Build search query
var searchQuery = BuildSearchQuery(command.Situation, command.PositionA, command.PositionB);

// Step 3: Search via HybridSearchService (replaces hardcoded empty result)
var searchResult = await _hybridSearchService.SearchAsync(
    query: searchQuery,
    agentDefinition: definition,
    cancellationToken: cancellationToken).ConfigureAwait(false);

// Step 4: Replace TypologyProfile.Arbitro().MinScore with inline constant
const double ArbiterMinScore = 0.70;

var relevantChunks = searchResult.Results
    .Where(r => r.Score >= ArbiterMinScore)
    .OrderByDescending(r => r.Score)
    .GroupBy(r => (PdfId: r.PdfId.Replace("-", "", StringComparison.Ordinal), r.ChunkIndex))
    .Select(g => g.First())
    .ToList();

// Step 5: Game name resolution uses definition.GameId
var gameName = await ResolveGameNameAsync(definition.GameId, cancellationToken).ConfigureAwait(false);
```

Also update `ResolveGameNameAsync` to accept `Guid?` (already does, no change needed).

Remove the `IEmbeddingService` dependency (HybridSearchService handles embeddings internally).
Remove the `TypologyProfile.Arbitro()` reference entirely.

- [ ] **Step 5.3: Check IHybridSearchService.SearchAsync signature**

```bash
grep -n "SearchAsync" apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/IHybridSearchService.cs
```

Verify the method signature accepts `(string query, AgentDefinition agentDefinition, CancellationToken ct)` or similar. If different, adjust the call accordingly.

- [ ] **Step 5.4: Update the arbiter endpoint in Routing**

```bash
grep -rn "AskArbiterCommand\|AgentId.*arbiter\|arbiter" apps/api/src/Api/Routing --include="*.cs"
```

Update the endpoint to pass `AgentDefinitionId` instead of `AgentId`.

- [ ] **Step 5.5: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
git add -A
git commit -m "fix(kb): wire HybridSearchService in AskArbiter, use AgentDefinitionId"
```

---

## Task 6: Fix ChatWithSessionAgentCommandHandler

**Files:**
- Modify: `.../Application/Commands/ChatWithSessionAgentCommandHandler.cs`

- [ ] **Step 6.1: Replace IAgentTypologyRepository with IAgentDefinitionRepository**

Current code (line 32):
```csharp
private readonly IAgentTypologyRepository _typologyRepository;
```

Change to:
```csharp
private readonly IAgentDefinitionRepository _definitionRepository;
```

Update the constructor parameter accordingly.

Current usage (line ~121):
```csharp
var typology = await _typologyRepository
    .GetByIdAsync(agentSession.TypologyId, cancellationToken).ConfigureAwait(false);

if (typology == null)
    yield return new StreamingError($"AgentTypology {agentSession.TypologyId} not found", "TYPOLOGY_NOT_FOUND");
```

Replace with:
```csharp
var definition = await _definitionRepository
    .GetByIdAsync(agentSession.AgentDefinitionId, cancellationToken).ConfigureAwait(false);

if (definition == null)
{
    yield return new StreamingError(
        $"AgentDefinition {agentSession.AgentDefinitionId} not found", "DEFINITION_NOT_FOUND");
    yield break;
}
```

Then update all subsequent references from `typology.X` to `definition.X`. The key properties used are likely:
- `typology.Name` → `definition.Name`
- `typology.BasePrompt` → use `definition.Prompts` (first system prompt)
- `typology.DefaultStrategy` → `definition.Strategy`

Read the handler carefully to identify all usages and replace them.

- [ ] **Step 6.2: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
git add -A
git commit -m "fix(kb): ChatWithSessionAgent uses AgentDefinitionRepository instead of AgentTypologyRepository"
```

---

## Task 7: Update Cross-BC Dependencies

**Files:**
- Modify: `.../Application/Commands/CreateGameAgentCommand.cs`
- Modify: `.../Application/Commands/CreateGameAgentCommandHandler.cs`
- Modify: `.../Application/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs`
- Modify: `UserLibrary/Application/Commands/SaveAgentConfigCommand.cs`
- Modify: `UserLibrary/Application/Commands/SaveAgentConfigCommandHandler.cs`

- [ ] **Step 7.1: Update CreateGameAgentCommand**

```csharp
public record CreateGameAgentCommand(
    Guid GameId,
    Guid AgentDefinitionId,    // was TypologyId
    string StrategyName,
    string? StrategyParameters,
    Guid UserId,
    string UserTier,
    string UserRole
) : IRequest<CreateGameAgentResult>;
```

Update `CreateGameAgentResult.Typology` property → `Definition`:

```csharp
public record CreateGameAgentResult
{
    public required Guid LibraryEntryId { get; init; }
    public required Guid GameId { get; init; }
    public required string Status { get; init; }
    public required AgentDefinitionInfo Definition { get; init; }  // was Typology
    public required AgentStrategyInfo Strategy { get; init; }
}

public record AgentDefinitionInfo
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}
```

- [ ] **Step 7.2: Update CreateGameAgentCommandHandler**

Replace `IAgentTypologyRepository typologyRepository` with `IAgentDefinitionRepository definitionRepository` in the constructor. Update the handler body:

```csharp
var definition = await _definitionRepository
    .GetByIdAsync(request.AgentDefinitionId, cancellationToken).ConfigureAwait(false);

if (definition == null)
    throw new NotFoundException("AgentDefinition", request.AgentDefinitionId.ToString());

if (!definition.IsActive)
    throw new ConflictException($"AgentDefinition {definition.Name} is not active");

// Replace typology.Name usage with definition.Name
// Replace typology.Status == Approved check with definition.IsActive
```

- [ ] **Step 7.3: Update AutoCreateAgentOnPdfReadyHandler**

Replace `IAgentTypologyRepository _typologyRepo` with `IAgentDefinitionRepository _definitionRepo`.

```csharp
// Replace:
var typologies = await _typologyRepo.GetAllAsync(cancellationToken).ConfigureAwait(false);
var defaultTypology = typologies.FirstOrDefault(t => t.Status == TypologyStatus.Approved);

// With:
var definitions = await _definitionRepo.GetAllAsync(cancellationToken).ConfigureAwait(false);
var defaultDefinition = definitions.FirstOrDefault(d => d.IsSystemDefined && d.IsActive);

if (defaultDefinition == null)
{
    _logger.LogWarning("AutoCreateAgent: No system-defined AgentDefinition found. Cannot auto-create agent for game {GameId}", pdfEntity.GameId);
    return;
}

// Replace:
var command = new CreateGameAgentCommand(
    GameId: pdfEntity.GameId ?? Guid.Empty,
    TypologyId: defaultTypology.Id,   // → AgentDefinitionId: defaultDefinition.Id
    ...);
```

- [ ] **Step 7.4: Update SaveAgentConfigCommand + Handler (UserLibrary BC)**

In `SaveAgentConfigCommand.cs`, rename `TypologyId` → `AgentDefinitionId`.

In `SaveAgentConfigCommandHandler.cs`:
- Change constructor: replace `IAgentTypologyRepository typologyRepository` with `IAgentDefinitionRepository definitionRepository`
- Update validation:

```csharp
var definition = await _definitionRepository
    .GetByIdAsync(request.AgentDefinitionId, cancellationToken).ConfigureAwait(false);

if (definition == null)
    throw new NotFoundException("AgentDefinition", request.AgentDefinitionId.ToString());

if (!definition.IsActive || definition.Status != KnowledgeBase.Domain.Enums.AgentDefinitionStatus.Published)
    throw new ConflictException($"AgentDefinition {definition.Name} is not published");

// Replace typology.Name references with definition.Name
```

- [ ] **Step 7.5: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
git add -A
git commit -m "refactor(kb,userlib): cross-BC handlers use AgentDefinitionRepository instead of AgentTypologyRepository"
```

---

## Task 8: Delete Dead Execution Paths

**Files to delete:**
- `AskAgentQuestionCommand.cs` + `AskAgentQuestionCommandHandler.cs` + validator
- `InvokeAgentCommand.cs` + `InvokeAgentCommandHandler.cs` + validator
- `InvokeChessAgentCommand.cs` + Handler + chess DTOs
- `SearchChessKnowledgeQuery.cs` + Handler (if exists)

- [ ] **Step 8.1: Find all files to delete**

```bash
find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "AskAgentQuestion*" \
  -o -name "InvokeAgent*" -o -name "InvokeChessAgent*" \
  -o -name "SearchChessKnowledge*" \
  -o -name "ChessAgent*" -o -name "Chess*Dto*" 2>/dev/null
```

- [ ] **Step 8.2: Delete the files**

```bash
find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "AskAgentQuestion*" -delete
find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "InvokeAgent*" -delete
find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "InvokeChessAgent*" -delete
find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "SearchChessKnowledge*" -delete
find apps/api/src/Api/BoundedContexts/KnowledgeBase -name "Chess*" -delete
```

- [ ] **Step 8.3: Find and delete related routing endpoints**

```bash
grep -rn "AskAgentQuestion\|InvokeAgent\|InvokeChessAgent\|chess" \
  apps/api/src/Api/Routing --include="*.cs" -l
```

Open each file found and delete the chess / dead-path endpoint registrations.

- [ ] **Step 8.4: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
git add -A
git commit -m "refactor(kb): delete InvokeAgent, AskAgentQuestion, chess execution paths"
```

---

## Task 9: Delete All AgentTypology Application Layer

- [ ] **Step 9.1: Delete all typology commands/handlers/validators**

```bash
find apps/api/src/Api/BoundedContexts/KnowledgeBase/Application -name "*Typology*" -delete
find apps/api/src/Api/BoundedContexts/KnowledgeBase/Application -name "*AgentTypology*" -delete
```

- [ ] **Step 9.2: Delete typology domain events**

```bash
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/TypologyApprovedEvent.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/TypologyRejectedEvent.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentSessionTypologyChangedEvent.cs
```

- [ ] **Step 9.3: Delete routing files**

```bash
rm apps/api/src/Api/Routing/AdminAgentTypologyEndpoints.cs
rm apps/api/src/Api/Routing/AgentTypologyEndpoints.cs
```

- [ ] **Step 9.4: Remove routing registrations**

Find where `MapAdminAgentTypologyEndpoints` and `MapAgentTypologyEndpoints` are called in program setup:

```bash
grep -rn "AgentTypologyEndpoints\|MapAdminAgentTypology\|MapAgentTypology" \
  apps/api/src/Api --include="*.cs" | grep -v "Routing/Admin\|Routing/Agent"
```

Delete those call-sites.

- [ ] **Step 9.5: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -20
git add -A
git commit -m "refactor(kb): delete AgentTypology application layer, routing, events"
```

---

## Task 10: Delete Agent + AgentTypology Domain and Infrastructure

- [ ] **Step 10.1: Delete domain entities**

```bash
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/Agent.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/AgentTypology.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/TypologyPromptTemplate.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/TypologyStatus.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/TypologyProfile.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentRepository.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IAgentTypologyRepository.cs
```

- [ ] **Step 10.2: Delete infrastructure entities + EF configs**

```bash
rm apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/AgentEntity.cs
rm apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/AgentConfigurationEntity.cs
rm apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/AgentGameStateSnapshotEntity.cs
rm apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/AgentTypologyEntity.cs
rm apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TypologyPromptTemplateEntity.cs

find apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase \
  -name "AgentEntity*" -o -name "AgentConfiguration*" \
  -o -name "AgentGameState*" -o -name "AgentTypology*" \
  -o -name "TypologyPromptTemplate*" | xargs rm -f
```

- [ ] **Step 10.3: Delete repositories**

```bash
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentRepository.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentTypologyRepository.cs
rm apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/AgentGameStateSnapshotRepository.cs
```

- [ ] **Step 10.4: Clean up MeepleAiDbContext**

Open `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`. Remove these DbSet properties:

```bash
grep -n "AgentTypologies\|TypologyPromptTemplates\|AgentEntity\|AgentConfiguration\|AgentGameState\|AgentSessionEntity" \
  apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
```

Delete the lines that declare `DbSet<AgentTypologyEntity>`, `DbSet<TypologyPromptTemplateEntity>`, etc.

Also remove any `using` directives that no longer apply.

- [ ] **Step 10.5: Clean up DI registrations**

Find where `AgentRepository`, `AgentTypologyRepository`, etc. are registered:

```bash
grep -rn "AgentRepository\|AgentTypologyRepository\|IAgentRepository\|IAgentTypologyRepository" \
  apps/api/src/Api --include="*.cs" | grep -v "Test\|test\|\.cs:.*using"
```

Remove those registrations from the DI setup file(s).

- [ ] **Step 10.6: Build check**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -30
```

Fix any remaining compilation errors from the deletions.

- [ ] **Step 10.7: Commit**

```bash
git add -A
git commit -m "refactor(kb): delete Agent, AgentTypology domain + infrastructure entities and repositories"
```

---

## Task 11: Infrastructure Cleanup

**Files:**
- Modify: `Infrastructure/Seeders/Catalog/AgentSeeder.cs`
- Modify: `BoundedContexts/DatabaseSync/Application/Queries/ListTablesHandler.cs`

- [ ] **Step 11.1: Rewrite AgentSeeder**

The seeder currently creates `AgentTypologyEntity` + `AgentEntity` + `AgentConfigurationEntity`. Replace entirely with an idempotent check/create of a system-defined `AgentDefinition` via EF Core directly:

```csharp
internal static class AgentSeeder
{
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Ensure the system "Rules Expert" AgentDefinition exists
        const string SystemDefinitionName = "Rules Expert";

        var existing = await db.AgentDefinitions
            .FirstOrDefaultAsync(d => d.Name == SystemDefinitionName, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            logger.LogDebug("AgentSeeder: system definition '{Name}' already exists", SystemDefinitionName);
            return;
        }

        // NOTE: AgentDefinition is a domain entity — use its factory + EF tracking
        var definition = AgentDefinition.Create(
            name: SystemDefinitionName,
            description: "Specialized agent for board game rules interpretation and clarification.",
            type: AgentType.Custom("RAG", "Rules retrieval-augmented generation"),
            config: new AgentDefinitionConfig(
                model: AgentDefaults.DefaultModel,
                maxTokens: AgentDefaults.DefaultMaxTokens,
                temperature: (float)AgentDefaults.DefaultTemperature),
            strategy: AgentStrategy.HybridSearch());

        // Set system fields via reflection (backing fields) since no public setter exists
        // Alternatively, add a static factory method AgentDefinition.CreateSystem(...)
        // For now, use the EF shadow property approach in migration seed data instead
        logger.LogInformation("AgentSeeder: seeding system AgentDefinition '{Name}'", SystemDefinitionName);
        db.AgentDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

**Note:** If `AgentDefinition.Create()` doesn't support setting `IsSystemDefined` + `TypologySlug`, add a static factory method:

```csharp
// In AgentDefinition.cs, add after Create():
public static AgentDefinition CreateSystem(
    string name, string description, AgentType type,
    AgentDefinitionConfig config, string typologySlug,
    AgentStrategy? strategy = null)
{
    var definition = Create(name, description, type, config, strategy);
    definition._isSystemDefined = true;
    definition._typologySlug = typologySlug;
    return definition;
}
```

- [ ] **Step 11.2: Remove typology_prompt_templates from DatabaseSync**

Open `apps/api/src/Api/BoundedContexts/DatabaseSync/Application/Queries/ListTablesHandler.cs`.

Find and delete the line:
```csharp
["typology_prompt_templates"] = "KnowledgeBase",
```

- [ ] **Step 11.3: Build check + commit**

```bash
cd apps/api && dotnet build --no-restore 2>&1 | grep "error CS" | head -10
git add -A
git commit -m "refactor(kb): rewrite AgentSeeder for AgentDefinition, remove typology_prompt_templates from sync"
```

---

## Task 12: Delete / Update Tests

- [ ] **Step 12.1: Find all test files referencing deleted types**

```bash
find tests -name "*.cs" | xargs grep -l "AgentTypology\|IAgentTypologyRepository\|InvokeAgentCommand\|InvokeChessAgent\|AskAgentQuestion\|TypologyProfile\|TypologyStatus\|AgentEntity\|AgentConfigurationEntity" 2>/dev/null | sort
```

Also:

```bash
find tests -name "*.cs" | xargs grep -l "AgentStrategy.*SingleModel\|AgentStrategy.*VectorOnly\|AgentStrategy.*MultiModel\|AgentStrategy.*Citation\|AgentStrategy.*IterativeRAG\|AgentStrategy.*RAGFusion\|AgentStrategy.*MultiAgent\|AgentStrategy.*ChainOfThought\|AgentStrategy.*QueryDecomp\|AgentStrategy.*StepBack\|AgentStrategy.*QueryExpan\|AgentStrategy.*ConfidenceScoring\|AgentStrategy.*Custom" 2>/dev/null
```

- [ ] **Step 12.2: Delete test files for deleted types**

Delete test files that exclusively test deleted types. For test files that test kept types (like `AgentDefinition`, `AgentSession`, `AskArbiterCommandHandler`), update them instead.

```bash
# Delete typology tests:
find tests -name "*AgentTypology*" -delete
find tests -name "*InvokeAgent*" -delete
find tests -name "*AskAgentQuestion*" -delete
find tests -name "*InvokeChess*" -delete
find tests -name "*Chess*" -delete
```

- [ ] **Step 12.3: Update AgentSession tests**

Find tests that construct `AgentSession` with old signature:

```bash
grep -rn "new AgentSession(" tests --include="*.cs"
```

Update to new signature (remove `agentId:`, rename `typologyId:` → `agentDefinitionId:`).

- [ ] **Step 12.4: Update AskArbiterCommandHandler tests**

Find the test file:

```bash
find tests -name "*AskArbiter*"
```

Update:
- Replace `IAgentRepository` mock with `IAgentDefinitionRepository` mock
- Replace `AgentId` in `AskArbiterCommand` with `AgentDefinitionId`
- Mock `IHybridSearchService.SearchAsync` to return test chunks
- Remove `IEmbeddingService` mock

- [ ] **Step 12.5: Run tests**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "BoundedContext=KnowledgeBase" --no-build 2>&1 | tail -20
```

Fix any remaining test failures.

- [ ] **Step 12.6: Run full test suite**

```bash
cd apps/api && dotnet test tests/Api.Tests --no-build 2>&1 | tail -30
```

- [ ] **Step 12.7: Commit**

```bash
git add -A
git commit -m "test(kb): delete/update tests for agent system simplification"
```

---

## Task 13: EF Migration

- [ ] **Step 13.1: Verify EF Core can build the model**

```bash
cd apps/api/src/Api && dotnet ef dbcontext info 2>&1 | tail -5
```

Expected: model info printed, no errors.

- [ ] **Step 13.2: Create the migration**

```bash
cd apps/api/src/Api && dotnet ef migrations add AgentSystemSimplification
```

- [ ] **Step 13.3: Review the generated migration file**

Open `Infrastructure/Migrations/*_AgentSystemSimplification.cs`. Verify it contains:

**`Up` method must include:**
1. Add 5 columns to `agent_definitions`:
   - `is_system_defined boolean NOT NULL DEFAULT FALSE`
   - `typology_slug varchar(50)`
   - `game_id uuid REFERENCES games(id)`
   - `invocation_count int NOT NULL DEFAULT 0`
   - `last_invoked_at timestamptz`

2. Migrate `agent_typologies` → `agent_definitions` (INSERT SELECT preserving IDs):
```csharp
migrationBuilder.Sql(@"
    INSERT INTO knowledge_base.agent_definitions
        (id, name, description, type_value, type_description,
         model, temperature, max_tokens,
         is_system_defined, typology_slug,
         status, is_active, created_at,
         kb_card_ids, prompts, strategy, tools, chat_language)
    SELECT
        id, name, description, 'system', name,
        COALESCE(default_model, 'meta-llama/llama-3.3-70b-instruct:free'),
        COALESCE(default_temperature, 0.3),
        COALESCE(default_max_tokens, 2048),
        TRUE, slug,
        2, TRUE, created_at,
        '[]', '[]', '{""Name"":""HybridSearch"",""Parameters"":{}}', '[]', 'auto'
    FROM knowledge_base.agent_typologies
    ON CONFLICT (id) DO NOTHING;
");
```

3. Add `agent_definition_id` column to `agent_sessions`:
```csharp
migrationBuilder.AddColumn<Guid>(
    name: "agent_definition_id",
    table: "agent_sessions",
    nullable: true);  // nullable first, fill, then make required
```

4. Populate `agent_definition_id` from `typology_id`:
```csharp
migrationBuilder.Sql("UPDATE agent_sessions SET agent_definition_id = \"TypologyId\";");
```

5. Verify no nulls:
```csharp
migrationBuilder.Sql(@"
    DO $$ BEGIN
        IF EXISTS (SELECT 1 FROM agent_sessions WHERE agent_definition_id IS NULL) THEN
            RAISE EXCEPTION 'Migration incomplete: agent_sessions rows with null agent_definition_id';
        END IF;
    END $$;
");
```

6. Make `agent_definition_id` NOT NULL + add FK:
```csharp
migrationBuilder.AlterColumn<Guid>(name: "agent_definition_id", table: "agent_sessions", nullable: false);
// Add FK constraint
migrationBuilder.AddForeignKey(
    name: "FK_agent_sessions_agent_definitions_agent_definition_id",
    table: "agent_sessions",
    column: "agent_definition_id",
    principalSchema: "knowledge_base",
    principalTable: "agent_definitions",
    principalColumn: "id",
    onDelete: ReferentialAction.Restrict);
```

7. Drop old columns and tables:
```csharp
migrationBuilder.DropForeignKey("FK_agent_sessions_agent_typologies_TypologyId", "agent_sessions");
migrationBuilder.DropForeignKey("FK_agent_sessions_agents_AgentId", "agent_sessions");
migrationBuilder.DropColumn(name: "TypologyId", table: "agent_sessions");
migrationBuilder.DropColumn(name: "AgentId", table: "agent_sessions");

migrationBuilder.DropTable("typology_prompt_templates", schema: "knowledge_base");
migrationBuilder.DropTable("agents", schema: "knowledge_base");
migrationBuilder.DropTable("agent_configurations", schema: "knowledge_base");
migrationBuilder.DropTable("agent_game_state_snapshots");
migrationBuilder.DropTable("agent_typologies", schema: "knowledge_base");
```

If the auto-generated migration is missing any of these, add them manually.

- [ ] **Step 13.4: Apply migration to dev database**

```bash
cd apps/api/src/Api && dotnet ef database update
```

Expected: migration applies without errors.

- [ ] **Step 13.5: Final build + test**

```bash
cd apps/api && dotnet build && dotnet test tests/Api.Tests --no-build 2>&1 | tail -20
```

- [ ] **Step 13.6: Commit**

```bash
git add -A
git commit -m "feat(db): AgentSystemSimplification migration — 5 new AgentDefinition columns, agent_sessions FK rename, drop legacy tables"
```

---

## Task 14: Frontend Cleanup

- [ ] **Step 14.1: Find and delete typology-related frontend files**

```bash
find apps/web/src -type f \( -name "*typology*" -o -name "*Typology*" \) | sort
find apps/web/src -type f \( -name "*chess*" -o -name "*Chess*" \) | sort
```

Delete all found files.

- [ ] **Step 14.2: Find API client types to update**

```bash
grep -rn "typologyId\|TypologyId\|agentTypology\|AgentTypology\|chess" \
  apps/web/src --include="*.ts" --include="*.tsx" -l | sort
```

- [ ] **Step 14.3: Replace typologyId → agentDefinitionId in types and API calls**

For each file found, replace:
- `typologyId` → `agentDefinitionId`
- `TypologyId` → `AgentDefinitionId`
- Remove chess page/component/hook/API files

- [ ] **Step 14.4: Update admin agent pages**

```bash
grep -rn "agent-typologies\|agentTypologies" apps/web/src --include="*.ts" --include="*.tsx" -l
```

Update API paths from `/agent-typologies` to `/agents/definitions`.

- [ ] **Step 14.5: TypeScript check**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep "error TS" | head -20
```

Fix all TypeScript errors.

- [ ] **Step 14.6: Commit**

```bash
git add -A
git commit -m "feat(web): remove chess, typology UI; update session types to agentDefinitionId"
```

---

## Task 15: Final Verification + PR

- [ ] **Step 15.1: Full backend build + tests**

```bash
cd apps/api && dotnet build && dotnet test tests/Api.Tests 2>&1 | tail -30
```

Expected: 0 build errors, all tests pass.

- [ ] **Step 15.2: Frontend lint + typecheck**

```bash
cd apps/web && pnpm lint && pnpm typecheck
```

- [ ] **Step 15.3: Verify dead code is gone**

```bash
# No references to deleted types should remain:
grep -rn "AgentTypology\|IAgentTypologyRepository\|TypologyStatus\|TypologyProfile\|AgentEntity\b\|AgentConfigurationEntity" \
  apps/api/src apps/web/src --include="*.cs" --include="*.ts" --include="*.tsx" | grep -v "Migration\|Designer\|Snapshot"
```

Expected: zero results.

- [ ] **Step 15.4: Create PR to main-dev**

```bash
git push origin HEAD
gh pr create \
  --base main-dev \
  --title "refactor(kb): agent system simplification — unify domain, reduce strategies, fix arbiter" \
  --body "$(cat <<'EOF'
## Summary
- Collapse Agent + AgentTypology → AgentDefinition (single concept)
- AgentStrategy: 13 → 4 methods (HybridSearch, RetrievalOnly, SentenceWindowRAG, ColBERTReranking)
- Execution paths: 6 → 3 (ChatWithSession, PlaygroundChat, AskArbiter)
- Fix AskArbiterCommandHandler: wire IHybridSearchService (was hardcoded empty result since PR #165)
- agent_sessions FK: typology_id → agent_definition_id
- Delete chess agent, InvokeAgent, AskAgentQuestion, all AgentTypology CRUD
- Cross-BC updates: SaveAgentConfig, AutoCreateAgent, CreateGameAgent

## Test plan
- [ ] `dotnet test` passes
- [ ] `pnpm typecheck` passes
- [ ] Migration applied on staging
- [ ] Arbiter endpoint returns non-empty citations

Closes #XXX (agent system simplification)
EOF
)"
```

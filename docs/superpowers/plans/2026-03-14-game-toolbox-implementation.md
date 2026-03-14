# Game Toolbox Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a new `GameToolbox` bounded context with Card Deck tool, MeepleCard integration, and template system.

**Architecture:** New BC `GameToolbox` with adapter bridge to existing `GameToolkit`. CardDeck facades `SessionDeck`. Frontend uses MeepleCard system with new `tool` entity type, Zustand store, and SSE sync.

**Tech Stack:** .NET 9, MediatR, EF Core, FluentValidation, Next.js 16, React 19, Zustand, Tailwind 4, shadcn/ui

**Epic:** #412 | **Branch:** `epic/issue-412-game-toolbox` (parent: `frontend-dev`)

**Design Spec:** `docs/superpowers/specs/2026-03-14-game-toolbox-design.md`

---

## Chunk 1: Backend Domain + CQRS (Issues #413, #414)

### Task 1: Domain Entities (#413)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/Entities/Toolbox.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/Entities/ToolboxTool.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/Entities/Phase.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/Entities/ToolboxTemplate.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/ValueObjects/SharedContext.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/ValueObjects/ToolboxMode.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/Repositories/IToolboxRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Domain/Repositories/IToolboxTemplateRepository.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Domain/ToolboxTests.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Domain/ToolboxToolTests.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Domain/PhaseTests.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Domain/SharedContextTests.cs`

- [ ] **Step 1: Create ToolboxMode enum**

```csharp
// Domain/ValueObjects/ToolboxMode.cs
namespace Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

public enum ToolboxMode
{
    Freeform = 0,
    Phased = 1
}
```

- [ ] **Step 2: Create SharedContext value object**

```csharp
// Domain/ValueObjects/SharedContext.cs
namespace Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

public record PlayerInfo(string Name, string Color, string? AvatarUrl = null);

public record SharedContext
{
    public List<PlayerInfo> Players { get; init; } = [];
    public int CurrentPlayerIndex { get; init; }
    public int CurrentRound { get; init; } = 1;
    public Dictionary<string, string> CustomProperties { get; init; } = [];

    public PlayerInfo? CurrentPlayer =>
        CurrentPlayerIndex >= 0 && CurrentPlayerIndex < Players.Count
            ? Players[CurrentPlayerIndex]
            : null;

    public SharedContext AdvancePlayer()
    {
        var nextIndex = Players.Count > 0
            ? (CurrentPlayerIndex + 1) % Players.Count
            : 0;
        return this with { CurrentPlayerIndex = nextIndex };
    }

    public SharedContext AdvanceRound() =>
        this with { CurrentRound = CurrentRound + 1, CurrentPlayerIndex = 0 };
}
```

- [ ] **Step 3: Create ToolboxTool entity**

```csharp
// Domain/Entities/ToolboxTool.cs
namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

public class ToolboxTool
{
    public Guid Id { get; private set; }
    public Guid ToolboxId { get; private set; }
    public string Type { get; private set; } = string.Empty; // "DiceRoller", "CardDeck", etc.
    public string Config { get; private set; } = "{}"; // JSON
    public string State { get; private set; } = "{}"; // JSON runtime state
    public bool IsEnabled { get; private set; } = true;
    public int Order { get; private set; }

    private ToolboxTool() { } // EF Core

    public static ToolboxTool Create(string type, string config, int order)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Tool type is required.", nameof(type));

        return new ToolboxTool
        {
            Id = Guid.NewGuid(),
            Type = type,
            Config = config,
            Order = order,
            IsEnabled = true,
            State = "{}"
        };
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
    public void UpdateConfig(string config) => Config = config;
    public void UpdateState(string state) => State = state;
    public void SetOrder(int order) => Order = order;

    // EF Core hydration
    internal void SetToolboxId(Guid toolboxId) => ToolboxId = toolboxId;
}
```

- [ ] **Step 4: Create Phase entity**

```csharp
// Domain/Entities/Phase.cs
namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

public class Phase
{
    public Guid Id { get; private set; }
    public Guid ToolboxId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public List<Guid> ActiveToolIds { get; private set; } = [];

    private Phase() { } // EF Core

    public static Phase Create(string name, int order, List<Guid>? activeToolIds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Phase name is required.", nameof(name));

        return new Phase
        {
            Id = Guid.NewGuid(),
            Name = name,
            Order = order,
            ActiveToolIds = activeToolIds ?? []
        };
    }

    public void UpdateName(string name) => Name = name;
    public void SetOrder(int order) => Order = order;
    public void SetActiveTools(List<Guid> toolIds) => ActiveToolIds = toolIds;
    public bool IsToolActive(Guid toolId) => ActiveToolIds.Contains(toolId);

    internal void SetToolboxId(Guid toolboxId) => ToolboxId = toolboxId;
}
```

- [ ] **Step 5: Create Toolbox aggregate root**

```csharp
// Domain/Entities/Toolbox.cs
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;

namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

public class Toolbox
{
    private List<ToolboxTool> _tools = [];
    private List<Phase> _phases = [];

    public Guid Id { get; private set; }
    public Guid? GameId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ToolboxMode Mode { get; private set; } = ToolboxMode.Freeform;
    public SharedContext SharedContext { get; private set; } = new();
    public Guid? CurrentPhaseId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyList<ToolboxTool> Tools => _tools.AsReadOnly();
    public IReadOnlyList<Phase> Phases => _phases.AsReadOnly();

    private Toolbox() { } // EF Core

    public static Toolbox Create(string name, Guid? gameId = null, ToolboxMode mode = ToolboxMode.Freeform)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Toolbox name is required.", nameof(name));

        return new Toolbox
        {
            Id = Guid.NewGuid(),
            Name = name,
            GameId = gameId,
            Mode = mode,
            SharedContext = new SharedContext(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateMode(ToolboxMode mode)
    {
        Mode = mode;
        if (mode == ToolboxMode.Freeform)
            CurrentPhaseId = null;
        else if (_phases.Count > 0)
            CurrentPhaseId = _phases.OrderBy(p => p.Order).First().Id;
        UpdatedAt = DateTime.UtcNow;
    }

    public ToolboxTool AddTool(string type, string config)
    {
        var order = _tools.Count > 0 ? _tools.Max(t => t.Order) + 1 : 0;
        var tool = ToolboxTool.Create(type, config, order);
        tool.SetToolboxId(Id);
        _tools.Add(tool);
        UpdatedAt = DateTime.UtcNow;
        return tool;
    }

    public void RemoveTool(Guid toolId)
    {
        var tool = _tools.FirstOrDefault(t => t.Id == toolId)
            ?? throw new InvalidOperationException($"Tool {toolId} not found.");
        _tools.Remove(tool);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReorderTools(List<Guid> orderedToolIds)
    {
        for (var i = 0; i < orderedToolIds.Count; i++)
        {
            var tool = _tools.FirstOrDefault(t => t.Id == orderedToolIds[i]);
            tool?.SetOrder(i);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSharedContext(SharedContext context)
    {
        SharedContext = context;
        UpdatedAt = DateTime.UtcNow;
    }

    public Phase AddPhase(string name, List<Guid>? activeToolIds = null)
    {
        var order = _phases.Count > 0 ? _phases.Max(p => p.Order) + 1 : 0;
        var phase = Phase.Create(name, order, activeToolIds);
        phase.SetToolboxId(Id);
        _phases.Add(phase);
        if (CurrentPhaseId == null && Mode == ToolboxMode.Phased)
            CurrentPhaseId = phase.Id;
        UpdatedAt = DateTime.UtcNow;
        return phase;
    }

    public void RemovePhase(Guid phaseId)
    {
        var phase = _phases.FirstOrDefault(p => p.Id == phaseId)
            ?? throw new InvalidOperationException($"Phase {phaseId} not found.");
        _phases.Remove(phase);
        if (CurrentPhaseId == phaseId)
            CurrentPhaseId = _phases.OrderBy(p => p.Order).FirstOrDefault()?.Id;
        UpdatedAt = DateTime.UtcNow;
    }

    public Phase AdvancePhase()
    {
        if (Mode != ToolboxMode.Phased)
            throw new InvalidOperationException("Cannot advance phase in Freeform mode.");
        if (_phases.Count == 0)
            throw new InvalidOperationException("No phases defined.");

        var ordered = _phases.OrderBy(p => p.Order).ToList();
        var currentIndex = ordered.FindIndex(p => p.Id == CurrentPhaseId);
        var nextIndex = (currentIndex + 1) % ordered.Count;

        // If wrapping around, advance round
        if (nextIndex == 0)
            SharedContext = SharedContext.AdvanceRound();

        CurrentPhaseId = ordered[nextIndex].Id;
        UpdatedAt = DateTime.UtcNow;
        return ordered[nextIndex];
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    // EF Core hydration
    internal void SetTools(List<ToolboxTool> tools) => _tools = tools;
    internal void SetPhases(List<Phase> phases) => _phases = phases;
}
```

- [ ] **Step 6: Create ToolboxTemplate entity**

```csharp
// Domain/Entities/ToolboxTemplate.cs
namespace Api.BoundedContexts.GameToolbox.Domain.Entities;

public enum TemplateSource { Manual = 0, Community = 1, AI = 2 }

public class ToolboxTemplate
{
    public Guid Id { get; private set; }
    public Guid? GameId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ToolboxMode Mode { get; private set; }
    public TemplateSource Source { get; private set; }
    public string ToolsJson { get; private set; } = "[]"; // Serialized tool configs
    public string PhasesJson { get; private set; } = "[]"; // Serialized phase configs
    public string SharedContextDefaultsJson { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private ToolboxTemplate() { }

    public static ToolboxTemplate Create(
        string name, ToolboxMode mode, TemplateSource source,
        string toolsJson, string phasesJson, string sharedContextDefaultsJson,
        Guid? gameId = null)
    {
        return new ToolboxTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            GameId = gameId,
            Mode = mode,
            Source = source,
            ToolsJson = toolsJson,
            PhasesJson = phasesJson,
            SharedContextDefaultsJson = sharedContextDefaultsJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SoftDelete() { IsDeleted = true; DeletedAt = DateTime.UtcNow; }
}
```

- [ ] **Step 7: Create repository interfaces**

```csharp
// Domain/Repositories/IToolboxRepository.cs
namespace Api.BoundedContexts.GameToolbox.Domain.Repositories;

public interface IToolboxRepository
{
    Task<Toolbox?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Toolbox?> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task AddAsync(Toolbox toolbox, CancellationToken ct = default);
    Task UpdateAsync(Toolbox toolbox, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// Domain/Repositories/IToolboxTemplateRepository.cs
namespace Api.BoundedContexts.GameToolbox.Domain.Repositories;

public interface IToolboxTemplateRepository
{
    Task<ToolboxTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ToolboxTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task<List<ToolboxTemplate>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(ToolboxTemplate template, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 8: Write unit tests for Toolbox aggregate**

```csharp
// tests/Api.Tests/BoundedContexts/GameToolbox/Domain/ToolboxTests.cs
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolbox")]
public class ToolboxTests
{
    [Fact]
    public void Create_WithValidName_ReturnsToolbox()
    {
        var toolbox = Toolbox.Create("Catan Toolbox", Guid.NewGuid());
        Assert.Equal("Catan Toolbox", toolbox.Name);
        Assert.Equal(ToolboxMode.Freeform, toolbox.Mode);
        Assert.Empty(toolbox.Tools);
        Assert.Empty(toolbox.Phases);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Toolbox.Create(""));
    }

    [Fact]
    public void AddTool_AddsToCollection()
    {
        var toolbox = Toolbox.Create("Test");
        var tool = toolbox.AddTool("DiceRoller", """{"formula":"2d6"}""");
        Assert.Single(toolbox.Tools);
        Assert.Equal("DiceRoller", tool.Type);
        Assert.Equal(0, tool.Order);
    }

    [Fact]
    public void AddTool_SecondTool_IncrementsOrder()
    {
        var toolbox = Toolbox.Create("Test");
        toolbox.AddTool("DiceRoller", "{}");
        var second = toolbox.AddTool("ScoreTracker", "{}");
        Assert.Equal(1, second.Order);
    }

    [Fact]
    public void RemoveTool_RemovesFromCollection()
    {
        var toolbox = Toolbox.Create("Test");
        var tool = toolbox.AddTool("DiceRoller", "{}");
        toolbox.RemoveTool(tool.Id);
        Assert.Empty(toolbox.Tools);
    }

    [Fact]
    public void RemoveTool_NotFound_Throws()
    {
        var toolbox = Toolbox.Create("Test");
        Assert.Throws<InvalidOperationException>(() => toolbox.RemoveTool(Guid.NewGuid()));
    }

    [Fact]
    public void UpdateMode_ToPhasedWithPhases_SetsCurrentPhase()
    {
        var toolbox = Toolbox.Create("Test");
        var phase = toolbox.AddPhase("Setup");
        toolbox.UpdateMode(ToolboxMode.Phased);
        Assert.Equal(ToolboxMode.Phased, toolbox.Mode);
        Assert.Equal(phase.Id, toolbox.CurrentPhaseId);
    }

    [Fact]
    public void UpdateMode_ToFreeform_ClearsCurrentPhase()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        toolbox.AddPhase("Setup");
        toolbox.UpdateMode(ToolboxMode.Freeform);
        Assert.Null(toolbox.CurrentPhaseId);
    }

    [Fact]
    public void AdvancePhase_CyclesToNextPhase()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        var p1 = toolbox.AddPhase("Phase 1");
        var p2 = toolbox.AddPhase("Phase 2");
        Assert.Equal(p1.Id, toolbox.CurrentPhaseId);

        var next = toolbox.AdvancePhase();
        Assert.Equal(p2.Id, toolbox.CurrentPhaseId);
        Assert.Equal("Phase 2", next.Name);
    }

    [Fact]
    public void AdvancePhase_WrapsAround_AdvancesRound()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        toolbox.AddPhase("Phase 1");
        toolbox.AddPhase("Phase 2");
        Assert.Equal(1, toolbox.SharedContext.CurrentRound);

        toolbox.AdvancePhase(); // → Phase 2
        toolbox.AdvancePhase(); // → Phase 1, round 2
        Assert.Equal(2, toolbox.SharedContext.CurrentRound);
    }

    [Fact]
    public void AdvancePhase_InFreeform_Throws()
    {
        var toolbox = Toolbox.Create("Test");
        Assert.Throws<InvalidOperationException>(() => toolbox.AdvancePhase());
    }

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        var toolbox = Toolbox.Create("Test");
        toolbox.SoftDelete();
        Assert.True(toolbox.IsDeleted);
        Assert.NotNull(toolbox.DeletedAt);
    }
}
```

- [ ] **Step 9: Write unit tests for SharedContext**

```csharp
// tests/Api.Tests/BoundedContexts/GameToolbox/Domain/SharedContextTests.cs
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolbox")]
public class SharedContextTests
{
    [Fact]
    public void AdvancePlayer_CyclesToNext()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue"), new("Leo", "green")],
            CurrentPlayerIndex = 0
        };
        var next = ctx.AdvancePlayer();
        Assert.Equal(1, next.CurrentPlayerIndex);
    }

    [Fact]
    public void AdvancePlayer_WrapsAround()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue")],
            CurrentPlayerIndex = 1
        };
        var next = ctx.AdvancePlayer();
        Assert.Equal(0, next.CurrentPlayerIndex);
    }

    [Fact]
    public void CurrentPlayer_ReturnsCorrectPlayer()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue")],
            CurrentPlayerIndex = 1
        };
        Assert.Equal("Lucia", ctx.CurrentPlayer?.Name);
    }

    [Fact]
    public void AdvanceRound_IncrementsAndResetsPlayer()
    {
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red")],
            CurrentPlayerIndex = 0,
            CurrentRound = 3
        };
        var next = ctx.AdvanceRound();
        Assert.Equal(4, next.CurrentRound);
        Assert.Equal(0, next.CurrentPlayerIndex);
    }
}
```

- [ ] **Step 10: Run tests to verify they compile and pass**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=GameToolbox&Category=Unit" -v minimal`
Expected: All tests PASS

- [ ] **Step 11: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolbox/Domain/ tests/Api.Tests/BoundedContexts/GameToolbox/
git commit -m "feat(toolbox): add domain entities, value objects, and unit tests (#413)"
```

---

### Task 2: DTOs (#414)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/DTOs/ToolboxDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/DTOs/ToolboxToolDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/DTOs/PhaseDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/DTOs/ToolboxTemplateDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/DTOs/CardDrawResultDto.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// Application/DTOs/ToolboxToolDto.cs
namespace Api.BoundedContexts.GameToolbox.Application.DTOs;

public record ToolboxToolDto(
    Guid Id, string Type, string Config, string State,
    bool IsEnabled, int Order);

// Application/DTOs/PhaseDto.cs
public record PhaseDto(
    Guid Id, string Name, int Order, List<Guid> ActiveToolIds);

// Application/DTOs/ToolboxDto.cs
public record ToolboxDto(
    Guid Id, string Name, Guid? GameId, string Mode,
    SharedContextDto SharedContext, Guid? CurrentPhaseId,
    List<ToolboxToolDto> Tools, List<PhaseDto> Phases,
    DateTime CreatedAt, DateTime UpdatedAt);

public record SharedContextDto(
    List<PlayerInfoDto> Players, int CurrentPlayerIndex,
    int CurrentRound, Dictionary<string, string> CustomProperties);

public record PlayerInfoDto(string Name, string Color, string? AvatarUrl);

// Application/DTOs/ToolboxTemplateDto.cs
public record ToolboxTemplateDto(
    Guid Id, string Name, Guid? GameId, string Mode,
    string Source, string ToolsJson, string PhasesJson,
    DateTime CreatedAt);

// Application/DTOs/CardDrawResultDto.cs
public record CardDrawResultDto(List<DrawnCardDto> Cards, int RemainingInDeck);
public record DrawnCardDto(Guid Id, string Name, string? Value, string? Suit,
    Dictionary<string, string> CustomProperties);
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolbox/Application/DTOs/
git commit -m "feat(toolbox): add application DTOs (#414)"
```

---

### Task 3: Commands + Handlers — Toolbox CRUD (#414)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/CreateToolboxCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/CreateToolboxCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/UpdateToolboxModeCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/UpdateToolboxModeCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/AddToolToToolboxCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/AddToolToToolboxCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/RemoveToolFromToolboxCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/RemoveToolFromToolboxCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/ReorderToolsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/ReorderToolsCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/UpdateSharedContextCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/UpdateSharedContextCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Queries/GetToolboxQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Queries/GetToolboxByGameQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/QueryHandlers/ToolboxQueryHandlers.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Validators/CreateToolboxCommandValidator.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Application/CreateToolboxCommandHandlerTests.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Application/UpdateToolboxModeCommandHandlerTests.cs`

- [ ] **Step 1: Create commands**

Each command as `internal record` implementing `ICommand<T>` or `IRequest<T>`:
- `CreateToolboxCommand(string Name, Guid? GameId, string Mode)` → `ToolboxDto`
- `UpdateToolboxModeCommand(Guid ToolboxId, string Mode)` → `ToolboxDto`
- `AddToolToToolboxCommand(Guid ToolboxId, string Type, string Config)` → `ToolboxToolDto`
- `RemoveToolFromToolboxCommand(Guid ToolboxId, Guid ToolId)` → `Unit`
- `ReorderToolsCommand(Guid ToolboxId, List<Guid> OrderedToolIds)` → `Unit`
- `UpdateSharedContextCommand(Guid ToolboxId, SharedContextDto Context)` → `SharedContextDto`

- [ ] **Step 2: Create handlers**

Each handler: inject `IToolboxRepository`, fetch entity, call domain method, save, map to DTO. Follow `CreateGameCommandHandler` pattern exactly.

- [ ] **Step 3: Create queries + query handlers**

- `GetToolboxQuery(Guid Id)` → `ToolboxDto`
- `GetToolboxByGameQuery(Guid GameId)` → `ToolboxDto?`
- `ToolboxQueryHandlers`: fetch from repo, map to DTO

- [ ] **Step 4: Create validators**

```csharp
// Validators/CreateToolboxCommandValidator.cs
public class CreateToolboxCommandValidator : AbstractValidator<CreateToolboxCommand>
{
    public CreateToolboxCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Mode).Must(m => m is "Freeform" or "Phased")
            .WithMessage("Mode must be 'Freeform' or 'Phased'");
    }
}
```

- [ ] **Step 5: Write unit tests for CreateToolboxCommandHandler**

Mock `IToolboxRepository`, verify `AddAsync` called, verify DTO mapping. Follow `CreateGameCommandHandlerTests` pattern.

- [ ] **Step 6: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=GameToolbox" -v minimal`
Expected: All tests PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolbox/Application/
git add tests/Api.Tests/BoundedContexts/GameToolbox/Application/
git commit -m "feat(toolbox): add CQRS commands, handlers, validators, queries (#414)"
```

---

### Task 4: CardDeck + Phase Commands (#414, #415)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/CreateCardDeckCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/ShuffleCardDeckCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/DrawCardsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/ResetCardDeckCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/CardDeckCommandHandlers.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/AddPhaseCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/AdvancePhaseCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/PhaseCommandHandlers.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Adapters/CardDeckAdapter.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Application/CardDeckCommandHandlerTests.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Application/PhaseCommandHandlerTests.cs`

- [ ] **Step 1: Create CardDeckAdapter**

```csharp
// Adapters/CardDeckAdapter.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;

namespace Api.BoundedContexts.GameToolbox.Adapters;

public class CardDeckAdapter
{
    private readonly ISessionDeckRepository _deckRepository;

    public CardDeckAdapter(ISessionDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<SessionDeck> CreateStandardDeckAsync(
        Guid sessionId, string name, bool includeJokers, CancellationToken ct)
    {
        var deck = SessionDeck.CreateStandardDeck(sessionId, name, includeJokers);
        await _deckRepository.AddAsync(deck, ct);
        await _deckRepository.SaveChangesAsync(ct);
        return deck;
    }

    public async Task<SessionDeck> CreateCustomDeckAsync(
        Guid sessionId, string name, List<Card> cards, CancellationToken ct)
    {
        var deck = SessionDeck.CreateCustomDeck(sessionId, name, cards);
        await _deckRepository.AddAsync(deck, ct);
        await _deckRepository.SaveChangesAsync(ct);
        return deck;
    }

    public async Task ShuffleAsync(Guid deckId, CancellationToken ct)
    {
        var deck = await _deckRepository.GetByIdAsync(deckId, ct)
            ?? throw new InvalidOperationException($"Deck {deckId} not found.");
        deck.Shuffle();
        await _deckRepository.UpdateAsync(deck, ct);
        await _deckRepository.SaveChangesAsync(ct);
    }

    public async Task<List<Card>> DrawAsync(Guid deckId, int count, CancellationToken ct)
    {
        var deck = await _deckRepository.GetByIdAsync(deckId, ct)
            ?? throw new InvalidOperationException($"Deck {deckId} not found.");
        // Draw without participant (toolbox mode — cards visible to all)
        var cardIds = deck.DrawCards(Guid.Empty, count);
        await _deckRepository.UpdateAsync(deck, ct);
        await _deckRepository.SaveChangesAsync(ct);
        return deck.GetCards(cardIds);
    }

    public async Task ResetAsync(Guid deckId, CancellationToken ct)
    {
        var deck = await _deckRepository.GetByIdAsync(deckId, ct)
            ?? throw new InvalidOperationException($"Deck {deckId} not found.");
        deck.Reset();
        await _deckRepository.UpdateAsync(deck, ct);
        await _deckRepository.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Create CardDeck commands + handlers**

Commands delegate to `CardDeckAdapter`. `DrawCardsCommand` returns `CardDrawResultDto`.

- [ ] **Step 3: Create Phase commands + handlers**

- `AddPhaseCommand(Guid ToolboxId, string Name, List<Guid>? ActiveToolIds)` → `PhaseDto`
- `RemovePhaseCommand(Guid ToolboxId, Guid PhaseId)` → `Unit`
- `ReorderPhasesCommand(Guid ToolboxId, List<Guid> OrderedPhaseIds)` → `Unit`
- `AdvancePhaseCommand(Guid ToolboxId)` → `PhaseDto`

Handlers use `Toolbox` aggregate methods.

- [ ] **Step 4: Write tests**

Test CardDeckAdapter with mocked `ISessionDeckRepository`. Test phase handlers with mocked `IToolboxRepository`.

- [ ] **Step 5: Run tests and commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolbox/
git add tests/Api.Tests/BoundedContexts/GameToolbox/
git commit -m "feat(toolbox): add CardDeck adapter and Phase commands (#414, #415)"
```

---

### Task 5: Infrastructure — EF Core, DI, Routes (#416, #417)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Infrastructure/Persistence/ToolboxRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Infrastructure/Persistence/ToolboxTemplateRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Infrastructure/Persistence/ToolboxConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Infrastructure/DependencyInjection/GameToolboxServiceExtensions.cs`
- Create: `apps/api/src/Api/Routing/GameToolboxRoutes.cs`
- Modify: `apps/api/src/Api/Program.cs` — register BC services + routes
- Modify: `apps/api/src/Api/Infrastructure/Persistence/ApplicationDbContext.cs` — add DbSet

- [ ] **Step 1: Create EF Core configurations**

`ToolboxConfiguration`: map Toolbox, ToolboxTool (owned collection), Phase (owned collection). SharedContext as JSON column. Soft delete query filter.

- [ ] **Step 2: Create repository implementations**

Follow `GameRepository` pattern — inject `ApplicationDbContext`, implement interface methods.

- [ ] **Step 3: Create DI registration**

```csharp
// Infrastructure/DependencyInjection/GameToolboxServiceExtensions.cs
public static IServiceCollection AddGameToolboxContext(this IServiceCollection services)
{
    services.AddScoped<IToolboxRepository, ToolboxRepository>();
    services.AddScoped<IToolboxTemplateRepository, ToolboxTemplateRepository>();
    services.AddScoped<CardDeckAdapter>();
    services.AddScoped<ToolkitWidgetAdapter>();
    return services;
}
```

- [ ] **Step 4: Create routes**

```csharp
// Routing/GameToolboxRoutes.cs
public static class GameToolboxRoutes
{
    public static RouteGroupBuilder MapGameToolboxEndpoints(this RouteGroupBuilder group)
    {
        // All endpoints: IMediator.Send() only, RequireAuthorization
        // Follow exact pattern from GameManagementRoutes.cs
        // See spec Section 5.2 for full route list
    }
}
```

- [ ] **Step 5: Register in Program.cs**

Add `builder.Services.AddGameToolboxContext()` and `group.MapGameToolboxEndpoints()`.

- [ ] **Step 6: Create EF Core migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddGameToolbox`

- [ ] **Step 7: Verify migration + test endpoints**

Run: `dotnet ef database update && dotnet build`

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolbox/Infrastructure/
git add apps/api/src/Api/Routing/GameToolboxRoutes.cs
git add apps/api/src/Api/Program.cs
git add apps/api/src/Api/Infrastructure/Persistence/ApplicationDbContext.cs
git add apps/api/src/Api/Migrations/
git commit -m "feat(toolbox): add EF Core persistence, DI, API routes, migration (#416, #417)"
```

---

## Chunk 2: Frontend — MeepleCard + Toolbox UX (Issues #418, #419, #420, #421)

### Task 6: Design Tokens + MeepleCard Entity Type (#418)

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css` — add `--color-entity-tool`
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts` — add `tool` to union + colors
- Modify: any component switching on `MeepleEntityType` (CardNavigationFooter, entity-table-view, etc.)
- Test: `apps/web/__tests__/components/ui/data-display/meeple-card-tool-entity.test.tsx`

- [ ] **Step 1: Add CSS variable**

In `design-tokens.css`, add after `--color-entity-event`:
```css
--color-entity-tool: 195 80% 50%;      /* Sky Blue */
```

- [ ] **Step 2: Update MeepleEntityType union**

In `meeple-card-styles.ts`:
- Add `| 'tool'` to `MeepleEntityType`
- Add `tool: { hsl: '195 80% 50%', name: 'Tool' }` to `entityColors`

- [ ] **Step 3: Update components that switch on entity type**

Find all files that match `MeepleEntityType` and handle the `tool` case.

- [ ] **Step 4: Write test**

Verify MeepleCard renders with `entity="tool"` and correct color attribute.

- [ ] **Step 5: Run tests and commit**

```bash
pnpm test -- --filter meeple-card
git commit -m "feat(toolbox): add 'tool' entity type to MeepleCard system (#418)"
```

---

### Task 7: Zustand Store + API Client (#421)

**Files:**
- Create: `apps/web/src/lib/stores/toolboxStore.ts`
- Create: `apps/web/src/lib/api/clients/toolboxClient.ts`
- Create: `apps/web/src/lib/api/schemas/toolbox.schemas.ts`
- Test: `apps/web/__tests__/lib/stores/toolboxStore.test.ts`

- [ ] **Step 1: Create Zod schemas**

Define schemas matching backend DTOs: `ToolboxSchema`, `ToolboxToolSchema`, `PhaseSchema`, `SharedContextSchema`, `CardDrawResultSchema`.

- [ ] **Step 2: Create API client**

Follow existing client pattern (e.g., `sessionsClient.ts`). Type-safe fetch for all Toolbox endpoints.

- [ ] **Step 3: Create Zustand store**

Follow `sessionStore.ts` pattern exactly. Include devtools middleware, action names, loading/error state.

- [ ] **Step 4: Write store tests**

Test key actions: loadToolbox, addTool, draw, shuffle, advancePhase.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(toolbox): add Zustand store, API client, Zod schemas (#421)"
```

---

### Task 8: Toolbox Kit Card (#419)

**Files:**
- Create: `apps/web/src/components/toolbox/ToolboxKitCard.tsx`
- Create: `apps/web/src/components/toolbox/ToolboxKitCardBack.tsx`
- Create: `apps/web/src/components/toolbox/ToolboxDrawer.tsx`
- Create: `apps/web/src/components/toolbox/ToolPreviewChips.tsx`
- Test: `apps/web/__tests__/components/toolbox/ToolboxKitCard.test.tsx`

- [ ] **Step 1: Create ToolPreviewChips** — shows tool icons + names in a row with +N overflow
- [ ] **Step 2: Create ToolboxKitCard front** — MeepleCard entity="toolkit" with game image, mode badge, tool count, preview chips
- [ ] **Step 3: Create ToolboxKitCardBack** — full tool list with configs, phase list, template name
- [ ] **Step 4: Create ToolboxDrawer** — tabs: Overview, Tools (list of Tool Cards), Phases, Links
- [ ] **Step 5: Write tests and commit**

```bash
git commit -m "feat(toolbox): add Toolbox Kit Card with front/back/drawer (#419)"
```

---

### Task 9: Tool Cards + Freeform Layout (#420)

**Files:**
- Create: `apps/web/src/components/toolbox/ToolCard.tsx`
- Create: `apps/web/src/components/toolbox/ToolCardDeck.tsx`
- Create: `apps/web/src/components/toolbox/FreeformToolbox.tsx`
- Create: `apps/web/src/components/toolbox/SharedContextBar.tsx`
- Test: `apps/web/__tests__/components/toolbox/FreeformToolbox.test.tsx`
- Test: `apps/web/__tests__/components/toolbox/ToolCardDeck.test.tsx`

- [ ] **Step 1: Create SharedContextBar** — sticky header: players with colors, current turn indicator, round number
- [ ] **Step 2: Create ToolCard** — MeepleCard entity="tool" with sub-type icon, compact/expanded variants, delegates to existing widget components for expanded view
- [ ] **Step 3: Create ToolCardDeck** — CardDeck-specific Tool Card: deck name, card counts, Draw/Shuffle/Reset buttons, drawn cards display
- [ ] **Step 4: Create FreeformToolbox** — layout container: SharedContextBar + collapsible ToolCard list
- [ ] **Step 5: Write tests and commit**

```bash
git commit -m "feat(toolbox): add Tool Cards and Freeform layout (#420)"
```

---

## Chunk 3: Phased Mode + Templates (Issues #422, #423)

### Task 10: Phased Mode (#422)

**Files:**
- Create: `apps/web/src/components/toolbox/PhaseTimeline.tsx`
- Create: `apps/web/src/components/toolbox/PhasedToolbox.tsx`
- Test: `apps/web/__tests__/components/toolbox/PhasedToolbox.test.tsx`

- [ ] **Step 1: Create PhaseTimeline** — horizontal stepper: current phase highlighted, others greyed, mobile scrollable
- [ ] **Step 2: Create PhasedToolbox** — wraps FreeformToolbox with PhaseTimeline, locks/unlocks tools based on `Phase.ActiveToolIds`
- [ ] **Step 3: Wire AdvancePhase** — button dispatches to store → API → SSE broadcast
- [ ] **Step 4: Write tests and commit**

```bash
git commit -m "feat(toolbox): add Phased mode with timeline and tool activation (#422)"
```

---

### Task 11: Template System (#423)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/CreateToolboxTemplateCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Commands/ApplyToolboxTemplateCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Handlers/TemplateCommandHandlers.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Queries/GetToolboxTemplatesQuery.cs`
- Create: `apps/web/src/components/toolbox/TemplatePicker.tsx`
- Create: `apps/web/src/components/toolbox/ToolboxConfigurator.tsx`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Application/TemplateCommandHandlerTests.cs`
- Test: `apps/web/__tests__/components/toolbox/TemplatePicker.test.tsx`

- [ ] **Step 1: Backend template commands + handlers**

- `CreateToolboxTemplateCommand` → serializes current Toolbox config to JSON fields
- `ApplyToolboxTemplateCommand` → deserializes template, creates Toolbox with tools + phases + CardDecks
- `GetToolboxTemplatesQuery` → list by game or all

- [ ] **Step 2: Frontend TemplatePicker** — shows template list, AI-suggested on top, "Create from scratch" option
- [ ] **Step 3: Frontend ToolboxConfigurator** — tool catalog, add/remove/configure tools, save as template
- [ ] **Step 4: Write tests and commit**

```bash
git commit -m "feat(toolbox): add template system — create, apply, picker UI (#423)"
```

---

### Task 12: SSE Events + Offline Mode (#421)

**Files:**
- Modify: `apps/web/src/lib/hooks/useWidgetSync.ts` or create `apps/web/src/lib/hooks/useToolboxSync.ts`
- Modify: `apps/web/src/components/toolbox/FreeformToolbox.tsx` — wire SSE
- Create: `apps/web/src/components/toolbox/OfflineBanner.tsx`

- [ ] **Step 1: Create useToolboxSync hook** — SSE subscription for Toolbox events, maps to store actions
- [ ] **Step 2: Wire SSE into Toolbox layouts** — FreeformToolbox and PhasedToolbox subscribe on mount
- [ ] **Step 3: Create OfflineBanner** — "Offline mode — solo per te" banner when `isOffline=true`
- [ ] **Step 4: Write tests and commit**

```bash
git commit -m "feat(toolbox): add SSE sync and offline companion mode (#421)"
```

---

### Task 13: Widget Adapter Bridge (#416)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Adapters/ToolkitWidgetAdapter.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolbox/Application/Queries/GetAvailableToolsQuery.cs`
- Test: `tests/Api.Tests/BoundedContexts/GameToolbox/Adapters/ToolkitWidgetAdapterTests.cs`

- [ ] **Step 1: Create ToolkitWidgetAdapter** — maps 6 existing widgets to ToolboxTool interface
- [ ] **Step 2: Create GetAvailableToolsQuery** — returns both adapted widgets + native tools
- [ ] **Step 3: Write tests and commit**

```bash
git commit -m "feat(toolbox): add widget adapter bridge for 6 existing tools (#416)"
```

---

## Chunk 4: Integration + Cleanup

### Task 14: Toolbox Pages + Navigation

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/toolbox/page.tsx`
- Modify: Game Card navigation to include Toolbox link

- [ ] **Step 1: Create Toolbox page** — loads Toolbox by game, renders FreeformToolbox or PhasedToolbox
- [ ] **Step 2: Wire navigation** — Game Card → click 🔧 → Toolbox page
- [ ] **Step 3: Commit**

```bash
git commit -m "feat(toolbox): add Toolbox page and Game Card navigation (#412)"
```

---

### Task 15: E2E Test + Final Verification

**Files:**
- Create: `apps/web/__tests__/e2e/toolbox-basic.spec.ts`

- [ ] **Step 1: Write E2E test** — create Toolbox from template, add tools, draw card, advance phase
- [ ] **Step 2: Run full test suite**

```bash
cd apps/api/src/Api && dotnet test --filter "BoundedContext=GameToolbox"
cd apps/web && pnpm test && pnpm typecheck && pnpm lint
```

- [ ] **Step 3: Final commit and push**

```bash
git push -u origin epic/issue-412-game-toolbox
```

# Game Night Improvvisata v2 — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing Game Night Improvvisata journey with proactive setup guidance, structured rule arbitration with democratic override, and persistent agent memory across sessions.

**Architecture:** Three phases extending GameManagement bounded context (Phases 1-2) and creating a new AgentMemory bounded context (Phase 3). All features behind SystemConfiguration feature flags. CQRS pattern: endpoints use only `IMediator.Send()`. LiveGameSession is in-memory (singleton `ILiveSessionRepository`), not EF-backed.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR, EF Core + PostgreSQL (JSONB), SignalR (`GameStateHub`), FluentValidation, xUnit + Moq, Next.js 16 + React 19 + Zustand + Tailwind 4

**Spec:** `docs/superpowers/specs/2026-03-18-game-night-improvvisata-v2-design.md`

**Prerequisites:** Resolve review issues I2, I3, I4 BEFORE starting this plan. These are tracked in the project memory and must be completed first to avoid migration conflicts (especially I3: PauseSnapshot FK).

---

## File Structure

### Phase 1: Setup Wizard

**Backend — Domain (GameManagement)**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/SetupChecklistData.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/SetupComponent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/SetupStep.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` — add SetupChecklist property + methods

**Backend — Application (GameManagement)**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/GenerateSetupChecklistCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GenerateSetupChecklistCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/LiveSessions/GenerateSetupChecklistCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/UpdateSetupChecklistCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/UpdateSetupChecklistCommandHandler.cs`

**Backend — KnowledgeBase (extend existing)**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamSetupGuideQuery.cs` — add PlayerCount parameter
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/StreamSetupGuideQueryHandler.cs` — use PlayerCount in prompt

**Backend — Infrastructure**
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs` — add SetupChecklistJson column
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs` — configure JSONB column
- Create: migration via `dotnet ef migrations add AddSetupChecklistToLiveSession`

**Backend — Routing**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` — add setup checklist endpoints

**Frontend**
- Create: `apps/web/src/components/session/live/SetupWizard.tsx`
- Create: `apps/web/src/components/session/live/ComponentChecklist.tsx`
- Create: `apps/web/src/components/session/live/SetupStepGuide.tsx`
- Create: `apps/web/src/components/session/live/__tests__/SetupWizard.test.tsx`

**Tests**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Models/SetupChecklistDataTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GenerateSetupChecklistCommandHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/UpdateSetupChecklistCommandHandlerTests.cs`

### Phase 2: Arbitro Strutturato

**Backend — Domain (GameManagement)**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/RuleDispute.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/DisputeVerdict.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/RulingFor.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/VerdictConfidence.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/DisputeOutcome.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/DisputeVote.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/StructuredDisputeResolvedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/DisputeVotesCastEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IRuleDisputeRepository.cs`

**Backend — Application (GameManagement)**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNight/OpenStructuredDisputeCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNight/RespondToDisputeCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNight/RespondentTimeoutCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNight/CastVoteOnDisputeCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/GameNight/TallyDisputeVotesCommand.cs`
- Create: handlers + validators for each command (in respective folders)
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GameNight/GetGameDisputeHistoryQuery.cs` — extend with v2 fields

**Backend — Infrastructure**
- Create: `apps/api/src/Api/Infrastructure/Entities/GameManagement/RuleDisputeEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/RuleDisputeEntityConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/RuleDisputeRepository.cs`
- Create: migration via `dotnet ef migrations add AddRuleDisputeTable`

**Backend — Routing**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` — add dispute v2 endpoints

**Backend — SignalR**
- Modify: `apps/api/src/Api/Hubs/GameStateHub.cs` — add dispute voting broadcast methods

**Frontend**
- Modify: `apps/web/src/components/session/live/ArbitroModal.tsx` — v2 multi-party UI
- Create: `apps/web/src/components/session/live/DisputeVoting.tsx`
- Create: `apps/web/src/components/session/live/DisputeVerdictStructured.tsx`
- Modify: `apps/web/src/components/session/live/DisputeHistory.tsx` — show v2 fields
- Create: `apps/web/src/components/session/live/__tests__/DisputeVoting.test.tsx`

**Tests**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/RuleDisputeTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GameNight/OpenStructuredDisputeCommandHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GameNight/CastVoteOnDisputeCommandHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GameNight/TallyDisputeVotesCommandHandlerTests.cs`

### Phase 3: Agent Memory

**Backend — Domain (new AgentMemory context)**
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Entities/GameMemory.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Entities/GroupMemory.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Entities/PlayerMemory.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/HouseRule.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/MemoryNote.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/GroupMember.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/GroupPreferences.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/GroupStats.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Models/PlayerGameStats.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Enums/HouseRuleSource.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Enums/PreferredComplexity.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Repositories/IGameMemoryRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Repositories/IGroupMemoryRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Repositories/IPlayerMemoryRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Events/HouseRuleAddedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Domain/Events/GuestClaimedEvent.cs`

**Backend — Application (AgentMemory)**
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/CreateGroupMemoryCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/AddHouseRuleCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/ClaimGuestPlayerCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/ConfirmGuestClaimCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/UpdateGroupPreferencesCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Commands/AddMemoryNoteCommand.cs`
- Create: handlers + validators for each command
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Queries/GetGameMemoryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Queries/GetGroupMemoryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Queries/GetPlayerStatsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/Queries/GetClaimableGuestsQuery.cs`
- Create: query handlers

**Backend — Event Handlers (cross-context)**
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/EventHandlers/OnDisputeOverriddenAddHouseRuleHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/EventHandlers/OnSessionCompletedUpdateStatsHandler.cs`

**Backend — Infrastructure**
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Entities/GameMemoryEntity.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Entities/GroupMemoryEntity.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Entities/PlayerMemoryEntity.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Configurations/GameMemoryEntityConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Configurations/GroupMemoryEntityConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Configurations/PlayerMemoryEntityConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Persistence/GameMemoryRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Persistence/GroupMemoryRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/Persistence/PlayerMemoryRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Infrastructure/DependencyInjection/AgentMemoryServiceExtensions.cs`
- Create: migration via `dotnet ef migrations add AddAgentMemoryTables`

**Backend — Routing**
- Create: `apps/api/src/Api/Routing/AgentMemoryEndpoints.cs`

**Frontend**
- Create: `apps/web/src/components/session/live/GroupMemoryPanel.tsx`
- Create: `apps/web/src/components/session/live/HouseRulesDisplay.tsx`
- Create: `apps/web/src/components/session/live/PlayerStatsCard.tsx`
- Create: `apps/web/src/components/profile/ClaimGuestGames.tsx`
- Create: frontend tests for each component

**Tests**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/AgentMemory/Domain/Entities/GameMemoryTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/AgentMemory/Domain/Entities/GroupMemoryTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/AgentMemory/Domain/Entities/PlayerMemoryTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/AgentMemory/Application/Handlers/` — handler tests

---

## Chunk 0: Prerequisites + Feature Flags

### Task 1: Seed feature flags migration

**Files:**
- Create: migration via `dotnet ef migrations add SeedGameNightV2FeatureFlags`

**Reference:** Existing `FeatureFlag` entity at `apps/api/src/Api/BoundedContexts/SystemConfiguration/Domain/Entities/FeatureFlag.cs`. Flags are seeded in migrations, toggled via `ToggleFeatureFlagCommand`.

- [ ] **Step 1: Generate migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add SeedGameNightV2FeatureFlags
```

- [ ] **Step 2: Edit the migration to seed flags**

In the generated migration file, add to `Up()`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    var now = DateTime.UtcNow;
    var flags = new[]
    {
        new { Id = Guid.NewGuid(), Name = "SetupWizard.Enabled", IsEnabled = true, Description = "Enable Setup Wizard for live sessions", CreatedAt = now, UpdatedAt = now },
        new { Id = Guid.NewGuid(), Name = "SetupWizard.BggFallback", IsEnabled = false, Description = "Enable BGG data fallback in setup checklist cascade", CreatedAt = now, UpdatedAt = now },
        new { Id = Guid.NewGuid(), Name = "Arbitro.StructuredDisputes", IsEnabled = true, Description = "Enable v2 structured dispute system", CreatedAt = now, UpdatedAt = now },
        new { Id = Guid.NewGuid(), Name = "Arbitro.DemocraticOverride", IsEnabled = true, Description = "Enable democratic override voting on dispute verdicts", CreatedAt = now, UpdatedAt = now },
        new { Id = Guid.NewGuid(), Name = "AgentMemory.Enabled", IsEnabled = true, Description = "Enable persistent agent memory system", CreatedAt = now, UpdatedAt = now },
        new { Id = Guid.NewGuid(), Name = "AgentMemory.GuestClaim", IsEnabled = true, Description = "Enable guest account claiming for player history", CreatedAt = now, UpdatedAt = now },
    };

    foreach (var f in flags)
    {
        migrationBuilder.InsertData(
            table: "feature_flags",
            columns: new[] { "id", "name", "is_enabled", "description", "created_at", "updated_at" },
            values: new object[] { f.Id, f.Name, f.IsEnabled, f.Description, f.CreatedAt, f.UpdatedAt });
    }
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    var flagNames = new[] { "SetupWizard.Enabled", "SetupWizard.BggFallback", "Arbitro.StructuredDisputes", "Arbitro.DemocraticOverride", "AgentMemory.Enabled", "AgentMemory.GuestClaim" };
    foreach (var name in flagNames)
    {
        migrationBuilder.Sql($"DELETE FROM feature_flags WHERE name = '{name}';");
    }
}
```

- [ ] **Step 3: Apply migration**

```bash
cd apps/api/src/Api
dotnet ef database update
```

- [ ] **Step 4: Verify flags exist**

```bash
# Via API (requires admin login)
curl http://localhost:8080/api/v1/admin/feature-flags | jq '.[] | select(.name | startswith("SetupWizard") or startswith("Arbitro") or startswith("AgentMemory"))'
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "chore(system-config): seed Game Night v2 feature flags"
```

---

## Chunk 1: Setup Wizard (Phase 1)

### Task 2: SetupChecklistData model + domain tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/SetupChecklistData.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/SetupComponent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/SetupStep.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Models/SetupChecklistDataTests.cs`

**Context:** These are mutable JSONB payload classes (NOT DDD value objects). They live in `Domain/Models/` to distinguish from immutable value objects in `Domain/ValueObjects/`.

- [ ] **Step 1: Write the failing tests**

```csharp
// SetupChecklistDataTests.cs
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SetupChecklistDataTests
{
    [Fact]
    public void Create_WithValidData_ReturnsChecklist()
    {
        var components = new List<SetupComponent>
        {
            new("Game board", 1),
            new("Dice", 2)
        };
        var steps = new List<SetupStep>
        {
            new(1, "Place the board in the center"),
            new(2, "Deal 5 cards to each player")
        };

        var checklist = new SetupChecklistData(4, components, steps);

        Assert.Equal(4, checklist.PlayerCount);
        Assert.Equal(2, checklist.Components.Count);
        Assert.Equal(2, checklist.SetupSteps.Count);
        Assert.False(checklist.Components[0].Checked);
        Assert.False(checklist.SetupSteps[0].Completed);
    }

    [Fact]
    public void ToggleComponent_SetsCheckedTrue()
    {
        var checklist = CreateSampleChecklist();
        checklist.ToggleComponent(0);
        Assert.True(checklist.Components[0].Checked);
    }

    [Fact]
    public void CompleteStep_SetsCompletedTrue()
    {
        var checklist = CreateSampleChecklist();
        checklist.CompleteStep(0);
        Assert.True(checklist.SetupSteps[0].Completed);
    }

    [Fact]
    public void Create_WithZeroPlayerCount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SetupChecklistData(0, new List<SetupComponent>(), new List<SetupStep>()));
    }

    private static SetupChecklistData CreateSampleChecklist() =>
        new(4,
            new List<SetupComponent> { new("Board", 1) },
            new List<SetupStep> { new(1, "Place board") });
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SetupChecklistDataTests" -v minimal
```
Expected: FAIL — classes don't exist yet.

- [ ] **Step 3: Implement the models**

```csharp
// SetupComponent.cs
namespace Api.BoundedContexts.GameManagement.Domain.Models;

public sealed class SetupComponent
{
    public string Name { get; set; }
    public int Quantity { get; set; }
    public bool Checked { get; set; }

    public SetupComponent(string name, int quantity)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name required", nameof(name));
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be positive", nameof(quantity));
        Checked = false;
    }
}

// SetupStep.cs
namespace Api.BoundedContexts.GameManagement.Domain.Models;

public sealed class SetupStep
{
    public int Order { get; set; }
    public string Instruction { get; set; }
    public bool Completed { get; set; }

    public SetupStep(int order, string instruction)
    {
        Order = order;
        Instruction = !string.IsNullOrWhiteSpace(instruction) ? instruction : throw new ArgumentException("Instruction required", nameof(instruction));
        Completed = false;
    }
}

// SetupChecklistData.cs
namespace Api.BoundedContexts.GameManagement.Domain.Models;

public sealed class SetupChecklistData
{
    public int PlayerCount { get; set; }
    public List<SetupComponent> Components { get; set; }
    public List<SetupStep> SetupSteps { get; set; }

    public SetupChecklistData(int playerCount, List<SetupComponent> components, List<SetupStep> setupSteps)
    {
        PlayerCount = playerCount > 0 ? playerCount : throw new ArgumentException("PlayerCount must be positive", nameof(playerCount));
        Components = components ?? new List<SetupComponent>();
        SetupSteps = setupSteps ?? new List<SetupStep>();
    }

    public void ToggleComponent(int index)
    {
        if (index < 0 || index >= Components.Count) throw new ArgumentOutOfRangeException(nameof(index));
        Components[index].Checked = !Components[index].Checked;
    }

    public void CompleteStep(int index)
    {
        if (index < 0 || index >= SetupSteps.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SetupSteps[index].Completed = true;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SetupChecklistDataTests" -v minimal
```
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/Setup*.cs apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Models/SetupChecklistDataTests.cs
git commit -m "feat(game-mgmt): add SetupChecklistData model with tests"
```

### Task 3: Extend LiveGameSession with SetupChecklist

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs`
- Create: migration

- [ ] **Step 1: Add SetupChecklist to domain entity**

In `LiveGameSession.cs`, add:
- Private field: `private SetupChecklistData? _setupChecklist;`
- Public property: `public SetupChecklistData? SetupChecklist => _setupChecklist;`
- Method: `public void SetSetupChecklist(SetupChecklistData checklist)` — sets `_setupChecklist`, validates not null
- Method: `public void UpdateSetupChecklist(SetupChecklistData checklist)` — replaces entire checklist (replace-whole semantics)

- [ ] **Step 2: Add SetupChecklistJson to infrastructure entity**

In `LiveGameSessionEntity.cs`, add:
```csharp
public string? SetupChecklistJson { get; set; }
```

- [ ] **Step 3: Configure JSONB column**

In `LiveGameSessionEntityConfiguration.cs`, add after `DisputesJson` configuration:
```csharp
builder.Property(e => e.SetupChecklistJson)
    .HasColumnName("setup_checklist_json")
    .HasColumnType("jsonb");
```

- [ ] **Step 4: Update repository mapping**

Update the `LiveSessionRepository` domain ↔ entity mapping to serialize/deserialize `SetupChecklistData` to/from `SetupChecklistJson`, following the same pattern as `DisputesJson`.

- [ ] **Step 5: Generate and apply migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddSetupChecklistToLiveSession
dotnet ef database update
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(game-mgmt): add SetupChecklistJson to LiveGameSession"
```

### Task 4: Extend StreamSetupGuideQuery with PlayerCount

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamSetupGuideQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/StreamSetupGuideQueryHandler.cs`

- [ ] **Step 1: Add PlayerCount to query**

```csharp
// StreamSetupGuideQuery.cs
internal record StreamSetupGuideQuery(
    string GameId,
    int PlayerCount = 0  // 0 = generic setup, >0 = adapted to player count
) : IStreamingQuery<RagStreamingEvent>;
```

- [ ] **Step 2: Update handler prompt**

In `StreamSetupGuideQueryHandler.cs`, modify the prompt to include player count:
```csharp
var prompt = query.PlayerCount > 0
    ? $"Extract from the rulebook: 1) complete list of all components needed 2) step-by-step setup procedure for {query.PlayerCount} players. Respond in structured JSON with {{\"components\": [{{\"name\": string, \"quantity\": int}}], \"steps\": [{{\"order\": int, \"instruction\": string}}]}}"
    : existingPrompt;
```

- [ ] **Step 3: Verify build and cross-context wiring**

```bash
cd apps/api/src/Api && dotnet build
```

Verify that `StreamSetupGuideQuery` is accessible from GameManagement handlers. The MediatR assembly scanning must include KnowledgeBase. Check `Program.cs` or the MediatR registration to confirm both assemblies are scanned. If not, add the KnowledgeBase assembly to the scan.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamSetupGuideQuery.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/StreamSetupGuideQueryHandler.cs
git commit -m "feat(knowledge-base): extend StreamSetupGuideQuery with PlayerCount"
```

### Task 5: GenerateSetupChecklistCommand + Handler + Tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/GenerateSetupChecklistCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/LiveSessions/GenerateSetupChecklistCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GenerateSetupChecklistCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GenerateSetupChecklistCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GenerateSetupChecklistCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidSession_GeneratesChecklist()
    {
        // Arrange: mock ILiveSessionRepository, IMediator (for StreamSetupGuideQuery), IFeatureFlagRepository
        // Create a session with GameId set
        // Act: handle GenerateSetupChecklistCommand
        // Assert: session.SetupChecklist is not null, has components and steps
    }

    [Fact]
    public async Task Handle_WhenFeatureDisabled_ThrowsInvalidOperation()
    {
        // Arrange: feature flag SetupWizard.Enabled = false
        // Act + Assert: throws
    }

    [Fact]
    public async Task Handle_WhenNoGameId_ThrowsNotFoundException()
    {
        // Arrange: session without GameId
        // Act + Assert: throws
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~GenerateSetupChecklistCommandHandlerTests" -v minimal
```

- [ ] **Step 3: Implement command, validator, handler**

```csharp
// GenerateSetupChecklistCommand.cs
internal record GenerateSetupChecklistCommand(
    Guid SessionId,
    int PlayerCount
) : ICommand<SetupChecklistData>;

// GenerateSetupChecklistCommandValidator.cs
internal class GenerateSetupChecklistCommandValidator : AbstractValidator<GenerateSetupChecklistCommand>
{
    public GenerateSetupChecklistCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.PlayerCount).GreaterThan(0).LessThanOrEqualTo(20);
    }
}

// GenerateSetupChecklistCommandHandler.cs
// 1. Check feature flag SetupWizard.Enabled
// 2. Get session from ILiveSessionRepository
// 3. Verify session has GameId
// 4. Send StreamSetupGuideQuery(GameId, PlayerCount) via IMediator
// 5. Parse JSON response into SetupChecklistData
// 6. Call session.SetSetupChecklist(checklist)
// 7. Return checklist
```

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/GenerateSetupChecklistCommand.cs apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/LiveSessions/GenerateSetupChecklistCommandValidator.cs apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GenerateSetupChecklistCommandHandler.cs apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/GenerateSetupChecklistCommandHandlerTests.cs
git commit -m "feat(game-mgmt): add GenerateSetupChecklistCommand with TDD"
```

### Task 6: UpdateSetupChecklistCommand (toggle component/step)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/UpdateSetupChecklistCommand.cs`
- Create: handler + validator + tests

Follow same TDD pattern as Task 5. This command receives the updated `SetupChecklistData` and calls `session.UpdateSetupChecklist()` (replace-whole semantics).

- [ ] **Step 1: Write failing tests**
- [ ] **Step 2: Run tests — verify FAIL**
- [ ] **Step 3: Implement command + handler + validator**
- [ ] **Step 4: Run tests — verify PASS**
- [ ] **Step 5: Commit**

### Task 7: Setup Wizard endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`

- [ ] **Step 1: Add endpoints**

```csharp
// POST /api/v1/live-sessions/{sessionId}/setup-checklist
group.MapPost("/live-sessions/{sessionId:guid}/setup-checklist", HandleGenerateSetupChecklist)
    .RequireAuthenticatedUser()
    .Produces<SetupChecklistData>(200)
    .Produces(400).Produces(404)
    .WithTags("LiveSessions")
    .WithSummary("Generate setup checklist from game rulebook via RAG");

// PUT /api/v1/live-sessions/{sessionId}/setup-checklist
group.MapPut("/live-sessions/{sessionId:guid}/setup-checklist", HandleUpdateSetupChecklist)
    .RequireAuthenticatedUser()
    .Produces(204)
    .Produces(400).Produces(404)
    .WithTags("LiveSessions")
    .WithSummary("Update setup checklist state (toggle components/steps)");
```

- [ ] **Step 2: Implement handlers** (using `IMediator.Send()` only)
- [ ] **Step 3: Verify build**
- [ ] **Step 4: Commit**

### Task 8: Setup Wizard frontend components

**Files:**
- Create: `apps/web/src/components/session/live/SetupWizard.tsx`
- Create: `apps/web/src/components/session/live/ComponentChecklist.tsx`
- Create: `apps/web/src/components/session/live/SetupStepGuide.tsx`
- Create: `apps/web/src/components/session/live/__tests__/SetupWizard.test.tsx`

- [ ] **Step 1: Create ComponentChecklist component**

Checkable list of setup components. Each item has name, quantity, and a checkbox.

- [ ] **Step 2: Create SetupStepGuide component**

Step-by-step guide with "Done, next step" button. Shows current step instruction with progress indicator.

- [ ] **Step 3: Create SetupWizard container**

Tabbed UI with two tabs: "Componenti" (ComponentChecklist) and "Setup" (SetupStepGuide). Calls `POST /live-sessions/{id}/setup-checklist` on mount if no checklist exists. Calls `PUT` on every toggle/complete action.

- [ ] **Step 4: Write tests**

Test rendering, tab switching, checkbox toggles, API calls via `vi.mock`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/live/SetupWizard.tsx apps/web/src/components/session/live/ComponentChecklist.tsx apps/web/src/components/session/live/SetupStepGuide.tsx apps/web/src/components/session/live/__tests__/SetupWizard.test.tsx
git commit -m "feat(web): add Setup Wizard UI with component checklist and step guide"
```

---

## Chunk 2: Arbitro Strutturato (Phase 2)

### Task 9: RuleDispute entity + DisputeVerdict value object + enums + tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/RulingFor.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/VerdictConfidence.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/DisputeOutcome.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Models/DisputeVote.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/DisputeVerdict.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/RuleDispute.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/StructuredDisputeResolvedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/DisputeVotesCastEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IRuleDisputeRepository.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/RuleDisputeTests.cs`

- [ ] **Step 1: Write failing tests for RuleDispute entity**

Test: create dispute, add respondent claim, set verdict, cast votes, tally votes (accept/override), backward-compatible `RuleDisputeEntry` generation.

- [ ] **Step 2: Run tests — verify FAIL**
- [ ] **Step 3: Implement enums**

```csharp
// RulingFor.cs
internal enum RulingFor { Initiator, Respondent, Ambiguous }

// VerdictConfidence.cs
internal enum VerdictConfidence { High, Medium, Low }

// DisputeOutcome.cs
internal enum DisputeOutcome { Pending, VerdictAccepted, VerdictOverridden }
```

- [ ] **Step 4: Implement DisputeVote model**

```csharp
// DisputeVote.cs — mutable JSONB payload (not value object)
internal sealed class DisputeVote
{
    public Guid PlayerId { get; set; }
    public bool AcceptsVerdict { get; set; }
}
```

- [ ] **Step 5: Implement DisputeVerdict value object**

```csharp
// DisputeVerdict.cs — immutable record (true DDD value object)
internal sealed record DisputeVerdict
{
    public RulingFor RulingFor { get; init; }
    public string Reasoning { get; init; }
    public string? Citation { get; init; }
    public VerdictConfidence Confidence { get; init; }

    public DisputeVerdict(RulingFor rulingFor, string reasoning, string? citation, VerdictConfidence confidence)
    {
        RulingFor = rulingFor;
        Reasoning = !string.IsNullOrWhiteSpace(reasoning) ? reasoning : throw new ArgumentException("Reasoning required");
        Citation = citation;
        Confidence = confidence;
    }
}
```

- [ ] **Step 6: Implement RuleDispute entity**

```csharp
// RuleDispute.cs — aggregate root with factory method
internal sealed class RuleDispute : AggregateRoot<Guid>
{
    // Properties matching spec data model
    // Factory: RuleDispute.Open(sessionId, gameId, initiatorPlayerId, initiatorClaim)
    // Methods: AddRespondentClaim(), SetVerdict(), CastVote(), TallyVotes()
    // Method: ToLegacyEntry() → RuleDisputeEntry (backward-compatible subset for DisputesJson)
    // Raises: StructuredDisputeResolvedEvent when verdict set
    // Raises: DisputeVotesCastEvent when tally completes
}
```

- [ ] **Step 7: Run tests — verify PASS**
- [ ] **Step 8: Commit**

### Task 10: RuleDispute infrastructure (entity, config, repo, migration)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/GameManagement/RuleDisputeEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/RuleDisputeEntityConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/RuleDisputeRepository.cs`
- Create: migration

- [ ] **Step 1: Create infrastructure entity**

Map `RuleDispute` domain entity to EF Core model. `VotesJson` and `RelatedDisputeIdsJson` as JSONB columns.

- [ ] **Step 2: Create configuration**

Table: `rule_disputes`. Indexes on `(session_id)`, `(game_id)`, `(game_id, created_at)` for history queries. FK to `live_game_sessions` and `games`.

- [ ] **Step 3: Create repository**

Implement `IRuleDisputeRepository` with methods: `AddAsync`, `GetByIdAsync`, `GetBySessionIdAsync`, `GetByGameIdAsync` (for cross-session history).

- [ ] **Step 4: Register in DI**

Add to `GameManagementServiceExtensions.cs`:
```csharp
services.AddScoped<IRuleDisputeRepository, RuleDisputeRepository>();
```

- [ ] **Step 5: Generate and apply migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddRuleDisputeTable
dotnet ef database update
```

- [ ] **Step 6: Commit**

### Task 11: Dispute commands + handlers + tests (OpenStructuredDispute, RespondToDispute, RespondentTimeout)

**Files:** Create commands, validators, handlers, tests for the three dispute-opening commands.

Follow TDD pattern:
- [ ] **Step 1: Write failing tests** for `OpenStructuredDisputeCommandHandler`
- [ ] **Step 2: Implement command + validator + handler**
- [ ] **Step 3: Run tests — verify PASS**
- [ ] **Step 4: Repeat for `RespondToDisputeCommand`**
- [ ] **Step 5: Repeat for `RespondentTimeoutCommand`**
- [ ] **Step 6: Commit**

Key handler logic for `OpenStructuredDisputeCommand`:
1. Check feature flag `Arbitro.StructuredDisputes`
2. Get session from `ILiveSessionRepository`
3. Create `RuleDispute.Open(sessionId, gameId, initiatorPlayerId, claim)`
4. Query cross-session disputes by GameId for semantic similarity (via IMediator → KnowledgeBase)
5. Save via `IRuleDisputeRepository`
6. Broadcast opening via SignalR (`GameStateHub`)
7. Return dispute ID

### Task 12: Voting commands + handlers + tests (CastVote, TallyVotes)

**Files:** Create commands, validators, handlers, tests.

Follow TDD pattern:
- [ ] **Step 1: Write failing tests** for `CastVoteOnDisputeCommandHandler`
- [ ] **Step 2: Implement** — loads dispute, calls `dispute.CastVote(playerId, accepts)`
- [ ] **Step 3: Write failing tests** for `TallyDisputeVotesCommandHandler`
- [ ] **Step 4: Implement** — calls `dispute.TallyVotes()`, appends `RuleDisputeEntry`-compatible subset to session's `DisputesJson` for backward compatibility
- [ ] **Step 5: Run all tests — verify PASS**
- [ ] **Step 6: Commit**

### Task 13: SignalR dispute broadcasting

**Files:**
- Modify: `apps/api/src/Api/Hubs/GameStateHub.cs`
- Create: event handler for `StructuredDisputeResolvedEvent` → SignalR broadcast

- [ ] **Step 1: Add hub methods** for dispute opened, verdict ready, vote cast
- [ ] **Step 2: Create SignalR event handler** following existing `DisputeResolvedSignalRHandler` pattern
- [ ] **Step 3: Commit**

### Task 14: Dispute v2 endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`

- [ ] **Step 1: Add endpoints**

```
POST /api/v1/live-sessions/{sessionId}/disputes          → OpenStructuredDisputeCommand
PUT  /api/v1/live-sessions/{sessionId}/disputes/{id}/respond → RespondToDisputeCommand
POST /api/v1/live-sessions/{sessionId}/disputes/{id}/timeout → RespondentTimeoutCommand
POST /api/v1/live-sessions/{sessionId}/disputes/{id}/vote    → CastVoteOnDisputeCommand
POST /api/v1/live-sessions/{sessionId}/disputes/{id}/tally   → TallyDisputeVotesCommand
GET  /api/v1/games/{gameId}/dispute-history               → GetGameDisputeHistoryQuery (extended with v2 fields)
```

- [ ] **Step 2: Implement handlers** (IMediator.Send() only)
- [ ] **Step 3: Verify build**
- [ ] **Step 4: Commit**

### Task 15: Arbitro v2 frontend components

**Files:**
- Modify: `apps/web/src/components/session/live/ArbitroModal.tsx`
- Create: `apps/web/src/components/session/live/DisputeVoting.tsx`
- Create: `apps/web/src/components/session/live/DisputeVerdictStructured.tsx`
- Modify: `apps/web/src/components/session/live/DisputeHistory.tsx`
- Create: `apps/web/src/components/session/live/__tests__/DisputeVoting.test.tsx`

- [ ] **Step 1: Create DisputeVerdictStructured** — shows ruling, confidence badge, citation, reasoning per position
- [ ] **Step 2: Create DisputeVoting** — accept/reject buttons for each player, majority indicator, 1-min timer
- [ ] **Step 3: Update ArbitroModal** — v2 flow: initiator claim → wait for respondent (2-min timer) → verdict → voting
- [ ] **Step 4: Update DisputeHistory** — show v2 fields (confidence, votes, outcome) alongside v1 entries
- [ ] **Step 5: Write tests**
- [ ] **Step 6: Commit**

---

## Chunk 3: Agent Memory (Phase 3)

### Task 16: AgentMemory bounded context scaffolding

**Files:** Create the directory structure for the new bounded context.

- [ ] **Step 1: Create directory structure**

```bash
# Phase 2 test directories (if not existing)
mkdir -p apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/Handlers/GameNight
mkdir -p apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities

# Phase 3 AgentMemory bounded context
mkdir -p apps/api/src/Api/BoundedContexts/AgentMemory/{Domain/{Entities,Models,Enums,Events,Repositories},Application/{Commands,Queries,Handlers,Validators,DTOs,EventHandlers},Infrastructure/{DependencyInjection,Entities,Configurations,Persistence}}
mkdir -p apps/api/tests/Api.Tests/BoundedContexts/AgentMemory/{Domain/Entities,Application/Handlers}
```

- [ ] **Step 2: Create DI extension**

```csharp
// AgentMemoryServiceExtensions.cs
public static IServiceCollection AddAgentMemoryContext(this IServiceCollection services)
{
    services.AddScoped<IGameMemoryRepository, GameMemoryRepository>();
    services.AddScoped<IGroupMemoryRepository, GroupMemoryRepository>();
    services.AddScoped<IPlayerMemoryRepository, PlayerMemoryRepository>();
    return services;
}
```

- [ ] **Step 3: Register in Program.cs** (find where other contexts are registered)
- [ ] **Step 4: Commit**

### Task 17: GameMemory entity + tests

**Files:**
- Create: domain models (HouseRule, MemoryNote, HouseRuleSource enum)
- Create: `GameMemory.cs` entity with factory method
- Create: `IGameMemoryRepository.cs`
- Create: `GameMemoryTests.cs`

Follow TDD pattern:
- [ ] **Step 1: Write failing tests** — create, add house rule (user-added), add house rule (dispute override), add note, add custom setup
- [ ] **Step 2: Implement models + entity**
- [ ] **Step 3: Run tests — verify PASS**
- [ ] **Step 4: Commit**

### Task 18: GroupMemory entity + tests

**Files:**
- Create: domain models (GroupMember, GroupPreferences, GroupStats, PreferredComplexity enum)
- Create: `GroupMemory.cs` entity
- Create: `IGroupMemoryRepository.cs`
- Create: `GroupMemoryTests.cs`

Follow TDD pattern:
- [ ] **Step 1: Write failing tests** — create group, add member (user), add member (guest), update preferences, update stats
- [ ] **Step 2: Implement**
- [ ] **Step 3: Run tests — verify PASS**
- [ ] **Step 4: Commit**

### Task 19: PlayerMemory entity + Guest Claim + tests

**Files:**
- Create: `PlayerMemory.cs` entity, `PlayerGameStats.cs` model
- Create: `IPlayerMemoryRepository.cs`
- Create: `PlayerMemoryTests.cs`

Follow TDD pattern:
- [ ] **Step 1: Write failing tests** — create (with user), create (guest), update game stats, claim guest (sets UserId + ClaimedAt), claim already-claimed throws
- [ ] **Step 2: Implement** — `PlayerMemory.CreateForUser()`, `PlayerMemory.CreateForGuest()`, `ClaimByUser(userId)`
- [ ] **Step 3: Run tests — verify PASS**
- [ ] **Step 4: Commit**

### Task 20: AgentMemory infrastructure (entities, configs, repos, migration)

**Files:**
- Create: `GameMemoryEntity.cs`, `GroupMemoryEntity.cs`, `PlayerMemoryEntity.cs`
- Create: configurations with JSONB columns
- Create: repositories
- Create: migration

- [ ] **Step 1: Create infrastructure entities** mapping domain to EF Core
- [ ] **Step 2: Create configurations**

Tables: `game_memories`, `group_memories`, `player_memories`
- `game_memories`: indexes on `(game_id, owner_id)`, JSONB columns for house_rules, custom_setup, notes
- `group_memories`: index on `(creator_id)`, JSONB columns for members, preferences, stats
- `player_memories`: indexes on `(user_id)`, `(group_id)`, `(guest_name)` where user_id IS NULL

- [ ] **Step 3: Create repositories**
- [ ] **Step 4: Generate and apply migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddAgentMemoryTables
dotnet ef database update
```

- [ ] **Step 5: Commit**

### Task 21: AgentMemory commands + handlers + tests

**Files:** Create commands, validators, handlers for:
- `CreateGroupMemoryCommand` — creates a named group with initial members
- `UpdateGroupPreferencesCommand` — updates group preferences
- `AddHouseRuleCommand` — manually adds a house rule to GameMemory
- `AddMemoryNoteCommand` — adds a note to GameMemory or GroupMemory
- `ClaimGuestPlayerCommand` — guest user requests claim → sends notification to host
- `ConfirmGuestClaimCommand` — host confirms claim → sets PlayerMemory.UserId

Follow TDD pattern for each command:
- [ ] **Step 1-6: TDD each command** (test → fail → implement → pass → commit)

### Task 22: AgentMemory queries + handlers

**Files:** Create queries for:
- `GetGameMemoryQuery(GameId, OwnerId)` — returns GameMemory with house rules, setup, notes
- `GetGroupMemoryQuery(GroupId)` — returns group with members, preferences, stats
- `GetPlayerStatsQuery(UserId)` — returns all PlayerMemory records for a user
- `GetClaimableGuestsQuery(UserId, GuestName)` — finds groups with matching guest name for claiming

- [ ] **Step 1-4: Implement each query + handler + tests**
- [ ] **Step 5: Commit**

### Task 23: Cross-context event handlers

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/EventHandlers/OnDisputeOverriddenAddHouseRuleHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/AgentMemory/Application/EventHandlers/OnSessionCompletedUpdateStatsHandler.cs`

- [ ] **Step 1: Write failing test** for `OnDisputeOverriddenAddHouseRuleHandler`

When `StructuredDisputeResolvedEvent` fires with `FinalOutcome == VerdictOverridden`, handler:
1. Finds or creates `GameMemory` for the game
2. Adds `HouseRule` with `Source = DisputeOverride` and the override rule text

- [ ] **Step 2: Implement handler**
- [ ] **Step 3: Write failing test** for `OnSessionCompletedUpdateStatsHandler`

When `LiveSessionCompletedEvent` fires, handler:
1. Updates `PlayerMemory.GameStats` for each player in the session
2. Updates `GroupMemory.Stats` if session has a GroupId

- [ ] **Step 4: Implement handler**
- [ ] **Step 5: Run tests — verify PASS**
- [ ] **Step 6: Commit**

### Task 24: AgentMemory endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/AgentMemoryEndpoints.cs`

- [ ] **Step 1: Create endpoint group**

```csharp
// Register in Program.cs as: app.MapGroup("/api/v1").MapAgentMemoryEndpoints()

// Group Memory
POST   /api/v1/groups                        → CreateGroupMemoryCommand
GET    /api/v1/groups/{groupId}              → GetGroupMemoryQuery
PUT    /api/v1/groups/{groupId}/preferences  → UpdateGroupPreferencesCommand

// Game Memory
GET    /api/v1/games/{gameId}/memory         → GetGameMemoryQuery
POST   /api/v1/games/{gameId}/memory/house-rules → AddHouseRuleCommand
POST   /api/v1/games/{gameId}/memory/notes   → AddMemoryNoteCommand

// Player Stats
GET    /api/v1/players/me/stats              → GetPlayerStatsQuery
GET    /api/v1/players/me/claimable-guests   → GetClaimableGuestsQuery
POST   /api/v1/players/me/claim-guest        → ClaimGuestPlayerCommand
POST   /api/v1/players/claims/{claimId}/confirm → ConfirmGuestClaimCommand
```

- [ ] **Step 2: Implement** (IMediator.Send() only, RequireAuthenticatedUser)
- [ ] **Step 3: Verify build**
- [ ] **Step 4: Commit**

### Task 25: AgentMemory frontend components

**Files:**
- Create: `apps/web/src/components/session/live/GroupMemoryPanel.tsx`
- Create: `apps/web/src/components/session/live/HouseRulesDisplay.tsx`
- Create: `apps/web/src/components/session/live/PlayerStatsCard.tsx`
- Create: `apps/web/src/components/profile/ClaimGuestGames.tsx`

- [ ] **Step 1: Create HouseRulesDisplay** — shows house rules for current game, badges for source (user/dispute)
- [ ] **Step 2: Create GroupMemoryPanel** — group name, members, preferences summary, session stats
- [ ] **Step 3: Create PlayerStatsCard** — win/loss record, games played, best scores per game
- [ ] **Step 4: Create ClaimGuestGames** — profile page section showing claimable guests, confirmation flow
- [ ] **Step 5: Write tests for each component**
- [ ] **Step 6: Commit**

---

## Chunk 4: Integration + Cleanup

### Task 26: Agent context injection for enriched responses

**Files:**
- Modify: RAG chat handler in KnowledgeBase to inject AgentMemory context (house rules, group preferences, player stats) into the LLM prompt when a session is active.

- [ ] **Step 1: Identify the chat handler** that processes RAG queries during a session
- [ ] **Step 2: Add AgentMemory query** before LLM call to fetch GameMemory + GroupMemory for current session
- [ ] **Step 3: Inject context** into system prompt: "House rules: ..., Group preferences: ..., Player note: ..."
- [ ] **Step 4: Test with mocked memory**
- [ ] **Step 5: Commit**

### Task 27: End-to-end smoke test

- [ ] **Step 1: Verify all migrations apply cleanly**

```bash
cd apps/api/src/Api
dotnet ef database drop --force
dotnet ef database update
```

- [ ] **Step 2: Verify feature flags are seeded**
- [ ] **Step 3: Run full backend test suite**

```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests -v minimal
```

- [ ] **Step 4: Run frontend tests**

```bash
cd apps/web
pnpm test
```

- [ ] **Step 5: Verify build**

```bash
cd apps/api/src/Api && dotnet build
cd ../../../web && pnpm build
```

- [ ] **Step 6: Final commit with any fixes**

---

## Summary

| Chunk | Tasks | Scope |
|---|---|---|
| 0: Prerequisites | 1 | Feature flags seed migration |
| 1: Setup Wizard | 2-8 | Domain models, CQRS flow, KnowledgeBase extension, frontend |
| 2: Arbitro Strutturato | 9-15 | RuleDispute entity, voting, SignalR, frontend |
| 3: Agent Memory | 16-25 | New bounded context, 3 entities, cross-context events, frontend |
| 4: Integration | 26-27 | Context injection, smoke tests |

**Parallelization:** Chunks 1 and 2 are independent — can be implemented in parallel. Chunk 3 depends on Chunk 2 (dispute override → house rule feeding). Chunk 4 depends on all prior chunks.

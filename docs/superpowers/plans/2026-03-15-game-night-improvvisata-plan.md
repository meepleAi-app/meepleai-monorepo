# Game Night Improvvisata — Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete end-to-end game night experience: BGG search → PDF upload → auto-agent → live session with CardStack → score tracking → rule arbitration → save/resume.

**Architecture:** Vertical slice through GameManagement, KnowledgeBase, UserLibrary, UserNotifications, and DocumentProcessing bounded contexts. Backend is CQRS with MediatR, DDD entities, EF Core + PostgreSQL. Frontend is Next.js App Router with CardStack navigation pattern. Real-time via SignalR `GameStateHub`.

**Tech Stack:** .NET 9 (MediatR, EF Core, SignalR, FluentValidation) | Next.js 16 (React 19, Zustand, React Query, Tailwind 4, shadcn/ui) | PostgreSQL 16 (JSONB) | Redis (quotas)

**Spec:** `docs/superpowers/specs/2026-03-15-game-night-improvvisata-vertical-slice-design.md`

---

## File Map

### Backend — New Files

| File | Responsibility |
|------|---------------|
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PauseSnapshot/PauseSnapshot.cs` | Full-state snapshot for save/resume |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PauseSnapshot/PlayerScoreSnapshot.cs` | Score snapshot VO |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/RuleDisputeEntry.cs` | Arbitro verdict VO |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IPauseSnapshotRepository.cs` | Repository interface |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/PauseSnapshotRepository.cs` | EF implementation |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/StartImprovvisataSessionCommand.cs` | Session creation shortcut |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreatePauseSnapshotCommand.cs` | Save session state |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/ResumeSessionCommand.cs` | Resume paused session |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SubmitRuleDisputeCommand.cs` | Arbitro verdict |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/SessionSaveRequestedEvent.cs` | Cross-context event for summary |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/DisputeResolvedEvent.cs` | SignalR broadcast trigger |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/SessionPausedEvent.cs` | Graceful disconnect trigger |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/SessionResumedEvent.cs` | Recap trigger |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/AppBackgroundedEvent.cs` | Auto-save trigger |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/UpdateSnapshotSummaryHandler.cs` | Updates PauseSnapshot with summary |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/SessionContextPromptBuilder.cs` | Builds session-aware agent prompt |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfProcessingCommand.cs` | Retry failed PDF processing |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs` | Auto-create agent when PDF done |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/GenerateAgentSummaryHandler.cs` | Async summary generation |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentAutoCreatedEvent.cs` | Notification trigger |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentSummaryGeneratedEvent.cs` | Summary complete event |
| `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/NotifyAgentReadyHandler.cs` | In-app notification |
| `apps/api/src/Api/Infrastructure/BackgroundServices/SessionAutoSaveBackgroundService.cs` | 10-min auto-save |
| `apps/api/src/Api/Infrastructure/Configurations/GameManagement/PauseSnapshotEntityConfiguration.cs` | EF config |
| `apps/api/src/Api/Infrastructure/Entities/GameManagement/PauseSnapshotEntity.cs` | EF entity model |

### Backend — Modified Files

| File | Change |
|------|--------|
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` | Add `Disputes` (JSONB), `AddDispute()`, `Pause()`, `Resume()` methods |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/SessionSnapshot/SnapshotTrigger.cs` | Add `AutoSave = 8` value |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/ILiveSessionRepository.cs` | Add `GetPausedByGameIdAsync()`, `GetDisputesByGameIdAsync()` |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/LiveSessionRepository.cs` | Implement new methods |
| `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs` | JSONB column for Disputes |
| `apps/api/src/Api/Hubs/GameStateHub.cs` | Add `DisputeResolved`, `SessionPaused`, `ScoreUpdated`, `AppBackgrounded` methods |
| `apps/api/src/Api/Routing/GameNightImprovvisataEndpoints.cs` | Add session, dispute, snapshot endpoints |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs` | Register new repos, handlers |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Register auto-create handler |

### Frontend — New Files

| File | Responsibility |
|------|---------------|
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx` | Session card parent page |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/agent/page.tsx` | Agent chat child card |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` | Scoreboard child card |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/photos/page.tsx` | Photo gallery child card |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/players/page.tsx` | Players + invite child card |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx` | Session layout with NavigationProvider |
| `apps/web/src/app/join/[inviteToken]/page.tsx` | Guest landing page (no auth) |
| `apps/web/src/components/sessions/live/SessionCardParent.tsx` | Session overview component |
| `apps/web/src/components/sessions/live/ScoreBoard.tsx` | Real-time scoreboard |
| `apps/web/src/components/sessions/live/PlayerList.tsx` | Player list with status |
| `apps/web/src/components/sessions/live/InviteModal.tsx` | QR code + share link modal |
| `apps/web/src/components/sessions/live/ArbitroModal.tsx` | Dispute submission modal |
| `apps/web/src/components/sessions/live/ArbitroVerdictCard.tsx` | Styled verdict display |
| `apps/web/src/components/sessions/live/DisputeHistory.tsx` | Past verdicts collapsible |
| `apps/web/src/components/sessions/live/SaveSessionModal.tsx` | Save & exit confirmation |
| `apps/web/src/components/sessions/live/PausedSessionCard.tsx` | Resume card in library |
| `apps/web/src/components/sessions/live/GuestScoreProposal.tsx` | Guest score proposal form |
| `apps/web/src/components/sessions/live/SessionNavConfig.tsx` | MiniNav config for session |
| `apps/web/src/lib/hooks/use-signalr-session.ts` | SignalR connection hook |
| `apps/web/src/lib/hooks/use-session-scores.ts` | Real-time score state |
| `apps/web/src/lib/stores/live-session-store.ts` | Zustand store for session |

### Frontend — Modified Files

| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/liveSessionsClient.ts` | Add `startImprovvisata()`, `createPauseSnapshot()`, `resumeSession()`, `submitDispute()`, `getDisputeHistory()` |
| `apps/web/src/components/library/add-game-sheet/steps/SuccessState.tsx` | Add processing progress indicator |
| `apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx` | Add "Sessioni in pausa" section |

### Test Files

| File | Tests |
|------|-------|
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PauseSnapshotTests.cs` | Entity creation, validation |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/RuleDisputeEntryTests.cs` | VO immutability |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/LiveGameSession_DisputesTests.cs` | AddDispute, JSONB serialization |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/StartImprovvisataSessionCommandHandlerTests.cs` | Session creation flow |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/CreatePauseSnapshotCommandHandlerTests.cs` | Save flow |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/ResumeSessionCommandHandlerTests.cs` | Resume flow |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/SubmitRuleDisputeCommandHandlerTests.cs` | Arbitro flow |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/AutoCreateAgentOnPdfReadyHandlerTests.cs` | Auto-create flow |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/GenerateAgentSummaryHandlerTests.cs` | Summary generation |
| `apps/api/tests/Api.Tests/Hubs/GameStateHub_ImprovvisataTests.cs` | New SignalR methods |
| `apps/api/tests/Api.Tests/Infrastructure/SessionAutoSaveBackgroundServiceTests.cs` | Auto-save logic |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/SessionContextPromptBuilderTests.cs` | Prompt building |
| `apps/web/src/components/sessions/live/__tests__/ScoreBoard.test.tsx` | Score display + interaction |
| `apps/web/src/components/sessions/live/__tests__/ArbitroModal.test.tsx` | Dispute submission |
| `apps/web/src/components/sessions/live/__tests__/SaveSessionModal.test.tsx` | Save confirmation |
| `apps/web/src/components/sessions/live/__tests__/InviteModal.test.tsx` | QR + link display |
| `apps/web/src/components/sessions/live/__tests__/PausedSessionCard.test.tsx` | Resume UI |
| `apps/web/src/lib/hooks/__tests__/use-signalr-session.test.ts` | Hook connection lifecycle |
| `apps/web/src/app/join/__tests__/guest-join.test.tsx` | Guest landing page |

---

## Chunk 1: Backend Domain Model + Infrastructure

### Task 1: RuleDisputeEntry Value Object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/RuleDisputeEntry.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/RuleDisputeEntryTests.cs`

- [ ] **Step 1: Write failing test for RuleDisputeEntry creation**

```csharp
// RuleDisputeEntryTests.cs
public class RuleDisputeEntryTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateEntry()
    {
        var entry = new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "Can I play 2 cards per turn?",
            verdict: "No, only one card per turn is allowed.",
            ruleReferences: new List<string> { "Page 12, Section 3.2" },
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow);

        Assert.Equal("Marco", entry.RaisedByPlayerName);
        Assert.Single(entry.RuleReferences);
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public void Create_WithEmptyDescription_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new RuleDisputeEntry(
            id: Guid.NewGuid(),
            description: "",
            verdict: "verdict",
            ruleReferences: new List<string>(),
            raisedByPlayerName: "Marco",
            timestamp: DateTime.UtcNow));
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (class doesn't exist)

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RuleDisputeEntryTests" --no-build 2>&1 | head -20`

- [ ] **Step 3: Implement RuleDisputeEntry**

```csharp
// RuleDisputeEntry.cs
namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

public sealed record RuleDisputeEntry
{
    public Guid Id { get; init; }
    public string Description { get; init; }
    public string Verdict { get; init; }
    public List<string> RuleReferences { get; init; }
    public string RaisedByPlayerName { get; init; }
    public DateTime Timestamp { get; init; }

    public RuleDisputeEntry(
        Guid id, string description, string verdict,
        List<string> ruleReferences, string raisedByPlayerName,
        DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(verdict))
            throw new ArgumentException("Verdict is required.", nameof(verdict));
        if (string.IsNullOrWhiteSpace(raisedByPlayerName))
            throw new ArgumentException("Player name is required.", nameof(raisedByPlayerName));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Description = description;
        Verdict = verdict;
        RuleReferences = ruleReferences ?? new List<string>();
        RaisedByPlayerName = raisedByPlayerName;
        Timestamp = timestamp;
    }
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RuleDisputeEntryTests"`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/ValueObjects/RuleDisputeEntry.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/RuleDisputeEntryTests.cs
git commit -m "feat(game-night): add RuleDisputeEntry value object for arbitro verdicts"
```

---

### Task 2: Add Disputes JSONB to LiveGameSession

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/LiveGameSession_DisputesTests.cs`

- [ ] **Step 1: Write failing test for AddDispute on LiveGameSession**

```csharp
// LiveGameSession_DisputesTests.cs
public class LiveGameSession_DisputesTests
{
    [Fact]
    public void AddDispute_ShouldAddToDisputesList()
    {
        var session = CreateTestSession();
        var dispute = new RuleDisputeEntry(
            Guid.NewGuid(), "Can I do X?", "No, rule says Y.",
            new List<string> { "Page 5" }, "Marco", DateTime.UtcNow);

        session.AddDispute(dispute);

        Assert.Single(session.Disputes);
        Assert.Equal("Marco", session.Disputes[0].RaisedByPlayerName);
    }

    [Fact]
    public void Disputes_ShouldBeReadOnly()
    {
        var session = CreateTestSession();
        Assert.IsAssignableFrom<IReadOnlyList<RuleDisputeEntry>>(session.Disputes);
    }

    private LiveGameSession CreateTestSession()
    {
        // Use existing factory method — check LiveGameSession.Create() signature
        return LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Test Game");
    }
}
```

- [ ] **Step 2: Run test — expect FAIL** (AddDispute method doesn't exist)

- [ ] **Step 3: Add Disputes collection and AddDispute method to LiveGameSession**

Add to `LiveGameSession.cs`:
```csharp
private readonly List<RuleDisputeEntry> _disputes = new();
public IReadOnlyList<RuleDisputeEntry> Disputes => _disputes.AsReadOnly();

public void AddDispute(RuleDisputeEntry dispute)
{
    ArgumentNullException.ThrowIfNull(dispute);
    _disputes.Add(dispute);
}
```

- [ ] **Step 4: Add Pause() and Resume() domain methods to LiveGameSession**

```csharp
// Add to LiveGameSession.cs
public void Pause()
{
    if (Status != LiveSessionStatus.InProgress)
        throw new InvalidOperationException("Can only pause an in-progress session.");
    Status = LiveSessionStatus.Paused;
    PausedAt = DateTime.UtcNow;
}

public void Resume()
{
    if (Status != LiveSessionStatus.Paused)
        throw new InvalidOperationException("Can only resume a paused session.");
    Status = LiveSessionStatus.InProgress;
    PausedAt = null;
}
```

Add tests:
```csharp
[Fact]
public void Pause_WhenInProgress_ShouldSetStatusToPaused()
{
    var session = CreateTestSession(); // Status = InProgress
    session.Pause();
    Assert.Equal(LiveSessionStatus.Paused, session.Status);
    Assert.NotNull(session.PausedAt);
}

[Fact]
public void Resume_WhenPaused_ShouldSetStatusToInProgress()
{
    var session = CreateTestSession();
    session.Pause();
    session.Resume();
    Assert.Equal(LiveSessionStatus.InProgress, session.Status);
    Assert.Null(session.PausedAt);
}
```

- [ ] **Step 5: Configure JSONB column in EF configuration**

Add to `LiveGameSessionEntityConfiguration.cs`:
```csharp
builder.Property(e => e.Disputes)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
        v => JsonSerializer.Deserialize<List<RuleDisputeEntry>>(v, JsonSerializerOptions.Default)
            ?? new List<RuleDisputeEntry>());
```

Note: Check existing JSONB patterns in codebase (e.g., `AgentDefinition.KbCardIds` uses a backing field pattern). Follow the same approach.

- [ ] **Step 5: Run test — expect PASS**

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs \
       apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/LiveGameSession_DisputesTests.cs
git commit -m "feat(game-night): add Disputes JSONB collection to LiveGameSession"
```

---

### Task 3: PauseSnapshot Entity + Repository

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PauseSnapshot/PauseSnapshot.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PauseSnapshot/PlayerScoreSnapshot.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IPauseSnapshotRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/PauseSnapshotRepository.cs`
- Create: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/PauseSnapshotEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/GameManagement/PauseSnapshotEntity.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PauseSnapshotTests.cs`

- [ ] **Step 1: Write failing tests for PauseSnapshot creation**

```csharp
// PauseSnapshotTests.cs
public class PauseSnapshotTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateSnapshot()
    {
        var snapshot = PauseSnapshot.Create(
            liveGameSessionId: Guid.NewGuid(),
            currentTurn: 5,
            currentPhase: "Trading",
            playerScores: new List<PlayerScoreSnapshot>
            {
                new("Marco", 42),
                new("Giulia", 45)
            },
            savedByUserId: Guid.NewGuid(),
            isAutoSave: false);

        Assert.Equal(5, snapshot.CurrentTurn);
        Assert.Equal("Trading", snapshot.CurrentPhase);
        Assert.Equal(2, snapshot.PlayerScores.Count);
        Assert.False(snapshot.IsAutoSave);
        Assert.Null(snapshot.AgentConversationSummary);
    }

    [Fact]
    public void UpdateSummary_ShouldSetAgentConversationSummary()
    {
        var snapshot = PauseSnapshot.Create(
            Guid.NewGuid(), 1, null, new(), Guid.NewGuid(), false);

        snapshot.UpdateSummary("{\"game_state\": \"round 5\"}");

        Assert.NotNull(snapshot.AgentConversationSummary);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

- [ ] **Step 3: Implement PlayerScoreSnapshot record**

```csharp
// PlayerScoreSnapshot.cs
namespace Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;

public sealed record PlayerScoreSnapshot(string PlayerName, decimal Score, int? PlayerId = null);
```

- [ ] **Step 4: Implement PauseSnapshot entity**

```csharp
// PauseSnapshot.cs
namespace Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;

public sealed class PauseSnapshot : Entity
{
    public Guid LiveGameSessionId { get; private set; }
    public int CurrentTurn { get; private set; }
    public string? CurrentPhase { get; private set; }
    public List<PlayerScoreSnapshot> PlayerScores { get; private set; } = new();
    public List<Guid> AttachmentIds { get; private set; } = new();
    public List<RuleDisputeEntry> Disputes { get; private set; } = new();
    public string? AgentConversationSummary { get; private set; }
    public string? GameStateJson { get; private set; }
    public DateTime SavedAt { get; private set; }
    public Guid SavedByUserId { get; private set; }
    public bool IsAutoSave { get; private set; }

    private PauseSnapshot() { } // EF

    public static PauseSnapshot Create(
        Guid liveGameSessionId, int currentTurn, string? currentPhase,
        List<PlayerScoreSnapshot> playerScores, Guid savedByUserId,
        bool isAutoSave, List<Guid>? attachmentIds = null,
        List<RuleDisputeEntry>? disputes = null, string? gameStateJson = null)
    {
        return new PauseSnapshot
        {
            Id = Guid.NewGuid(),
            LiveGameSessionId = liveGameSessionId,
            CurrentTurn = currentTurn,
            CurrentPhase = currentPhase,
            PlayerScores = playerScores ?? new(),
            AttachmentIds = attachmentIds ?? new(),
            Disputes = disputes ?? new(),
            GameStateJson = gameStateJson,
            SavedAt = DateTime.UtcNow,
            SavedByUserId = savedByUserId,
            IsAutoSave = isAutoSave
        };
    }

    public void UpdateSummary(string summary)
    {
        AgentConversationSummary = summary;
    }
}
```

- [ ] **Step 5: Implement repository interface**

```csharp
// IPauseSnapshotRepository.cs
namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

public interface IPauseSnapshotRepository
{
    Task<PauseSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PauseSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<PauseSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(PauseSnapshot snapshot, CancellationToken ct = default);
    Task UpdateAsync(PauseSnapshot snapshot, CancellationToken ct = default);
    Task DeleteAutoSavesBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
}
```

- [ ] **Step 6: Implement EF entity model, configuration, and repository**

Follow existing patterns from `SessionSnapshotEntity.cs` and `SessionSnapshotEntityConfiguration.cs`. JSONB columns for `PlayerScores`, `AttachmentIds`, `Disputes`. FK to `LiveGameSessions`.

- [ ] **Step 7: Run tests — expect PASS**

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PauseSnapshot/ \
       apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IPauseSnapshotRepository.cs \
       apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/PauseSnapshotRepository.cs \
       apps/api/src/Api/Infrastructure/Configurations/GameManagement/PauseSnapshotEntityConfiguration.cs \
       apps/api/src/Api/Infrastructure/Entities/GameManagement/PauseSnapshotEntity.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PauseSnapshotTests.cs
git commit -m "feat(game-night): add PauseSnapshot entity for session save/resume"
```

---

### Task 4: EF Migration + SnapshotTrigger Update

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/SessionSnapshot/SnapshotTrigger.cs`
- Create: New migration in `apps/api/src/Api/Infrastructure/Migrations/`

- [ ] **Step 1: Add AutoSave value to SnapshotTrigger enum**

```csharp
// Add to SnapshotTrigger.cs
AutoSave = 8
```

- [ ] **Step 2: Register PauseSnapshotRepository in DI**

Add to `GameManagementServiceExtensions.cs`:
```csharp
services.AddScoped<IPauseSnapshotRepository, PauseSnapshotRepository>();
```

- [ ] **Step 3: Generate EF migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddPauseSnapshotAndDisputesJsonb
```

- [ ] **Step 4: Review generated migration SQL**

Verify it creates:
- `PauseSnapshots` table with FK to `LiveGameSessions`
- JSONB columns for `PlayerScores`, `AttachmentIds`, `Disputes` on `PauseSnapshots`
- JSONB column for `Disputes` on `LiveGameSessions`

- [ ] **Step 5: Apply migration locally**

```bash
cd apps/api/src/Api
dotnet ef database update
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/SessionSnapshot/SnapshotTrigger.cs \
       apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs \
       apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(game-night): add EF migration for PauseSnapshot table and Disputes JSONB"
```

---

## Chunk 2: Backend Commands + Handlers

### Task 5: Domain Events

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/SessionSaveRequestedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/DisputeResolvedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentAutoCreatedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/AgentSummaryGeneratedEvent.cs`

- [ ] **Step 1: Create all 7 domain events**

```csharp
// SessionSaveRequestedEvent.cs
public sealed record SessionSaveRequestedEvent(
    Guid PauseSnapshotId,
    Guid LiveGameSessionId,
    Guid AgentDefinitionId,
    List<string> LastMessages) : INotification;

// DisputeResolvedEvent.cs
public sealed record DisputeResolvedEvent(
    Guid SessionId,
    RuleDisputeEntry Dispute) : INotification;

// AgentAutoCreatedEvent.cs
public sealed record AgentAutoCreatedEvent(
    Guid AgentDefinitionId,
    Guid PrivateGameId,
    Guid UserId,
    string GameName) : INotification;

// AgentSummaryGeneratedEvent.cs
public sealed record AgentSummaryGeneratedEvent(
    Guid PauseSnapshotId,
    string Summary) : INotification;

// SessionPausedEvent.cs
public sealed record SessionPausedEvent(
    Guid SessionId) : INotification;

// SessionResumedEvent.cs
public sealed record SessionResumedEvent(
    Guid SessionId,
    string? AgentRecap) : INotification;

// AppBackgroundedEvent.cs
public sealed record AppBackgroundedEvent(
    Guid SessionId) : INotification;
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/ \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Events/
git commit -m "feat(game-night): add domain events for session save, disputes, agent creation"
```

---

### Task 6: AutoCreateAgentOnPdfReadyHandler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/NotifyAgentReadyHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/AutoCreateAgentOnPdfReadyHandlerTests.cs`

- [ ] **Step 1: Find the PDF processing completion event**

Search for the domain event that fires when PDF processing (embedding/indexing) completes. Check `DocumentProcessing` bounded context for events. If none exists, check what the existing cross-context test stubs reference.

```bash
cd apps/api && grep -r "PdfProcessing\|DocumentIndexed\|EmbeddingCompleted\|VectorDocument.*Created" \
  src/Api/BoundedContexts/DocumentProcessing/Domain/Events/ \
  src/Api/BoundedContexts/KnowledgeBase/Domain/Events/ 2>/dev/null | head -20
```

- [ ] **Step 2: Write failing test for AutoCreateAgentOnPdfReadyHandler**

```csharp
// AutoCreateAgentOnPdfReadyHandlerTests.cs
public class AutoCreateAgentOnPdfReadyHandlerTests
{
    [Fact]
    public async Task Handle_WhenPdfLinkedToPrivateGame_ShouldCreateAgent()
    {
        // Arrange: mock IMediator, IPrivateGameRepository
        // The handler receives the PDF completion event
        // It checks if the PDF is linked to a PrivateGame
        // If yes: creates AgentDefinition with default prompt, publishes AgentAutoCreatedEvent

        // Assert: mediator.Send(CreateAgentDefinitionCommand) was called
        // Assert: mediator.Publish(AgentAutoCreatedEvent) was called
    }

    [Fact]
    public async Task Handle_WhenPdfNotLinkedToPrivateGame_ShouldDoNothing()
    {
        // No agent created for unlinked PDFs
    }

    [Fact]
    public async Task Handle_WhenAgentCreationFails_ShouldPublishFailureNotification()
    {
        // Agent creation throws → notification sent instead
    }
}
```

- [ ] **Step 3: Implement AutoCreateAgentOnPdfReadyHandler**

Key logic:
1. Receive PDF completion event
2. Query PrivateGame by PdfDocumentId (check if this FK exists; if not, use the document's GameId or SharedGameId)
3. If linked to PrivateGame → create AgentDefinition via MediatR `Send(CreateAgentDefinitionCommand)`
4. Use default Italian prompt template with `{GameName}` interpolation
5. Publish `AgentAutoCreatedEvent` on success
6. On failure: catch, log, publish notification with manual creation link

- [ ] **Step 4: Implement NotifyAgentReadyHandler**

```csharp
// NotifyAgentReadyHandler.cs — handles AgentAutoCreatedEvent
public class NotifyAgentReadyHandler : INotificationHandler<AgentAutoCreatedEvent>
{
    public async Task Handle(AgentAutoCreatedEvent notification, CancellationToken ct)
    {
        var notif = new Notification(
            Guid.NewGuid(),
            notification.UserId,
            NotificationType.FromString("AgentReady"),
            NotificationSeverity.Info,
            $"Agente per {notification.GameName} pronto!",
            $"L'agente AI per {notification.GameName} è stato creato automaticamente.",
            link: $"/library/private/{notification.PrivateGameId}/toolkit");

        await _notificationRepository.AddAsync(notif, ct);
    }
}
```

- [ ] **Step 5: Implement RetryPdfProcessingCommand**

Create `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfProcessingCommand.cs`:
- Re-queues a failed PDF document for processing
- Max 3 retries (track retry count on PdfDocument entity or metadata)
- On max retries exceeded: notification with "Contatta il supporto"
- Endpoint: `POST /api/v1/game-night/pdf/{pdfId}/retry`

- [ ] **Step 6: Register handlers in DI**

Add to `KnowledgeBaseServiceExtensions.cs` — MediatR auto-discovers `INotificationHandler` implementations, but verify the assembly scanning includes the handler's namespace.

- [ ] **Step 7: Run tests — expect PASS**

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/AutoCreateAgentOnPdfReadyHandler.cs \
       apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/NotifyAgentReadyHandler.cs \
       apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/RetryPdfProcessingCommand.cs \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/AutoCreateAgentOnPdfReadyHandlerTests.cs
git commit -m "feat(game-night): auto-create agent when PDF processing completes + retry support"
```

---

### Task 7: StartImprovvisataSessionCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/StartImprovvisataSessionCommand.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/StartImprovvisataSessionCommandHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

Test cases:
1. Creates `LiveGameSession` with correct game name, status InProgress
2. Creates `SessionInvite` with 24h expiry, max 10 uses
3. Sets current user as host player
4. Links toolkit if PrivateGame has one
5. Returns sessionId, inviteCode, shareLink
6. Fails if PrivateGame not found → NotFoundException
7. Tier quota check (if applicable)

- [ ] **Step 2: Implement command + handler**

```csharp
// StartImprovvisataSessionCommand.cs
public sealed record StartImprovvisataSessionCommand(
    Guid PrivateGameId) : IRequest<StartImprovvisataSessionResponse>;

public sealed record StartImprovvisataSessionResponse(
    Guid SessionId,
    string InviteCode,
    string ShareLink);
```

Handler logic:
1. Get PrivateGame by ID (throw NotFoundException if missing)
2. Create LiveGameSession via factory method
3. Add host as first player
4. Create SessionInvite (6-char code + UUID token, 24h expiry, max 10 uses)
5. Save all via repository
6. Return response with session code and share link

- [ ] **Step 3: Add FluentValidation validator**

```csharp
public class StartImprovvisataSessionValidator : AbstractValidator<StartImprovvisataSessionCommand>
{
    public StartImprovvisataSessionValidator()
    {
        RuleFor(x => x.PrivateGameId).NotEmpty();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Add endpoint to GameNightImprovvisataEndpoints.cs**

```csharp
group.MapPost("/start-session", async (StartImprovvisataSessionCommand cmd, IMediator mediator) =>
    Results.Ok(await mediator.Send(cmd)))
    .RequireAuthorization();
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/StartImprovvisataSessionCommand.cs \
       apps/api/src/Api/Routing/GameNightImprovvisataEndpoints.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/StartImprovvisataSessionCommandHandlerTests.cs
git commit -m "feat(game-night): add StartImprovvisataSession command for quick session creation"
```

---

### Task 8: SubmitRuleDisputeCommand (Arbitro)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SubmitRuleDisputeCommand.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/SubmitRuleDisputeCommandHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

Test cases:
1. Creates RuleDisputeEntry with verdict from agent
2. Adds dispute to LiveGameSession.Disputes
3. Publishes DisputeResolvedEvent for SignalR broadcast
4. Returns the verdict DTO
5. Fails if session not found
6. Fails if session not InProgress

- [ ] **Step 2: Implement command + handler**

```csharp
public sealed record SubmitRuleDisputeCommand(
    Guid SessionId,
    string Description,
    string RaisedByPlayerName) : IRequest<RuleDisputeResponse>;

public sealed record RuleDisputeResponse(
    Guid Id, string Verdict, List<string> RuleReferences, string? Note);
```

Handler logic:
1. Load LiveGameSession
2. Load AgentDefinition via session's ToolkitId
3. Build arbitration prompt (spec Section 3B) with session context + previous disputes
4. Query the RAG agent (use existing chat/query service)
5. Parse structured response (VERDETTO/REGOLA/NOTA sections)
6. Create RuleDisputeEntry, call `session.AddDispute(entry)`
7. Publish `DisputeResolvedEvent`
8. Save session

**Important:** Check how existing agent querying works — look at `ChatSessionsClient` or the existing RAG query pipeline in KnowledgeBase. The handler needs to call the same LLM query path but with the arbitro system prompt.

- [ ] **Step 3: Add endpoint**

```csharp
group.MapPost("/sessions/{sessionId:guid}/disputes", async (
    Guid sessionId, SubmitRuleDisputeRequest request, IMediator mediator) =>
    Results.Ok(await mediator.Send(new SubmitRuleDisputeCommand(
        sessionId, request.Description, request.RaisedByPlayerName))))
    .RequireAuthorization();
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SubmitRuleDisputeCommand.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/SubmitRuleDisputeCommandHandlerTests.cs
git commit -m "feat(game-night): add SubmitRuleDispute command for arbitro mode"
```

---

### Task 8b: Session Context Injection into Agent Prompt

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/SessionContextPromptBuilder.cs`

This is **Must-Have #8** from the spec. When a chat query includes a `sessionId`, the agent's system prompt must be augmented with session context.

- [ ] **Step 1: Write failing test**

Test that `SessionContextPromptBuilder.BuildSessionPrompt()` returns a prompt containing:
- Game name, player names, current turn/phase, scores
- Previous dispute verdicts (for consistency)

- [ ] **Step 2: Implement SessionContextPromptBuilder**

```csharp
public class SessionContextPromptBuilder
{
    public string BuildSessionPrompt(
        string gameName, List<string> playerNames, int currentTurn,
        string? currentPhase, Dictionary<string, decimal> scores,
        List<RuleDisputeEntry> previousDisputes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Sei l'assistente per \"{gameName}\". Sessione attiva con {playerNames.Count} giocatori: {string.Join(", ", playerNames)}.");
        sb.AppendLine($"Turno corrente: {currentTurn}. Fase: {currentPhase ?? "N/A"}.");
        sb.AppendLine($"Punteggi: {string.Join(", ", scores.Select(s => $"{s.Key} {s.Value}"))}.");
        sb.AppendLine("Quando rispondi su regole, cita SEMPRE la pagina del regolamento.");
        sb.AppendLine("Se c'è ambiguità nella regola, spiega le possibili interpretazioni.");

        if (previousDisputes.Any())
        {
            sb.AppendLine();
            sb.AppendLine("Verdetti precedenti in questa sessione:");
            foreach (var d in previousDisputes)
                sb.AppendLine($"- Disputa: \"{d.Description}\" → Verdetto: \"{d.Verdict}\"");
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 3: Integrate into existing chat/query pipeline**

Find where the agent system prompt is assembled (likely in the RAG query handler or chat session handler in KnowledgeBase). When `sessionId` is provided in the query:
1. Load `LiveGameSession` by ID
2. Call `SessionContextPromptBuilder.BuildSessionPrompt()`
3. Prepend to the agent's system prompt

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/SessionContextPromptBuilder.cs
git commit -m "feat(game-night): inject session context into agent prompt for live sessions"
```

---

### Task 9: CreatePauseSnapshot + ResumeSession Commands

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreatePauseSnapshotCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/ResumeSessionFromSnapshotCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/GenerateAgentSummaryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/CreatePauseSnapshotCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/ResumeSessionCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/GenerateAgentSummaryHandlerTests.cs`

- [ ] **Step 1: Write failing tests for CreatePauseSnapshotCommand**

Test cases:
1. Creates PauseSnapshot with current session state
2. Sets session status to Paused
3. Publishes SessionSaveRequestedEvent for async summary
4. Returns snapshot ID
5. Waits for pending score proposals (max 5s)

- [ ] **Step 2: Implement CreatePauseSnapshotCommand + handler**

```csharp
public sealed record CreatePauseSnapshotCommand(
    Guid SessionId,
    List<Guid>? FinalPhotoIds = null) : IRequest<Guid>;
```

Handler logic:
1. Load LiveGameSession with all related data
2. Wait for pending score proposals (configurable timeout, default 5s)
3. Capture: CurrentTurn, CurrentPhase, PlayerScores, Disputes, AttachmentIds
4. Create PauseSnapshot via factory (IsAutoSave = false)
5. Set session Status = Paused, PausedAt = UtcNow
6. Save snapshot via IPauseSnapshotRepository
7. Publish SessionSaveRequestedEvent (for async summary in KnowledgeBase)
8. Return snapshot ID

- [ ] **Step 3: Write failing tests for ResumeSessionFromSnapshotCommand**

Test cases:
1. Loads PauseSnapshot and restores session state
2. Sets status to InProgress, clears PausedAt
3. Creates new SessionInvite
4. Publishes SessionResumedEvent
5. Returns session ID + new invite code
6. Fails if session not Paused
7. Works without AgentConversationSummary (fallback)

- [ ] **Step 4: Implement ResumeSessionFromSnapshotCommand + handler**

```csharp
public sealed record ResumeSessionFromSnapshotCommand(
    Guid SessionId) : IRequest<ResumeSessionResponse>;

public sealed record ResumeSessionResponse(
    Guid SessionId, string InviteCode, string ShareLink,
    string? AgentRecap);
```

Handler logic:
1. Load latest PauseSnapshot for session
2. Restore session state (turn, phase)
3. Set Status = InProgress, PausedAt = null
4. Create new SessionInvite (old may be expired)
5. Delete auto-save snapshots for this session
6. If AgentConversationSummary exists → use for recap; else → fallback to raw messages
7. Return response

- [ ] **Step 5: Implement GenerateAgentSummaryHandler**

```csharp
// Handles SessionSaveRequestedEvent — generates summary asynchronously
public class GenerateAgentSummaryHandler : INotificationHandler<SessionSaveRequestedEvent>
{
    public async Task Handle(SessionSaveRequestedEvent notification, CancellationToken ct)
    {
        // 1. Get agent for the session
        // 2. Build summary prompt (spec Section 3C) with last 50 messages
        // 3. Query LLM for structured JSON summary
        // 4. Publish AgentSummaryGeneratedEvent with result
        // Timeout: if generation takes >60s, abandon (PauseSnapshot stays with null summary)
    }
}
```

Also need: handler for `AgentSummaryGeneratedEvent` that updates PauseSnapshot:
```csharp
public class UpdateSnapshotSummaryHandler : INotificationHandler<AgentSummaryGeneratedEvent>
{
    public async Task Handle(AgentSummaryGeneratedEvent notification, CancellationToken ct)
    {
        var snapshot = await _repo.GetByIdAsync(notification.PauseSnapshotId, ct);
        snapshot?.UpdateSummary(notification.Summary);
        if (snapshot != null) await _repo.UpdateAsync(snapshot, ct);
    }
}
```

- [ ] **Step 6: Run all tests — expect PASS**

- [ ] **Step 7: Add endpoints**

```csharp
// In GameNightImprovvisataEndpoints.cs
group.MapPost("/sessions/{sessionId:guid}/save", async (Guid sessionId, IMediator m) =>
    Results.Ok(await m.Send(new CreatePauseSnapshotCommand(sessionId))))
    .RequireAuthorization();

group.MapPost("/sessions/{sessionId:guid}/resume", async (Guid sessionId, IMediator m) =>
    Results.Ok(await m.Send(new ResumeSessionFromSnapshotCommand(sessionId))))
    .RequireAuthorization();
```

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/CreatePauseSnapshotCommand.cs \
       apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/ResumeSessionFromSnapshotCommand.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/GenerateAgentSummaryHandler.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/EventHandlers/UpdateSnapshotSummaryHandler.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/CreatePauseSnapshotCommandHandlerTests.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/ResumeSessionCommandHandlerTests.cs \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/GenerateAgentSummaryHandlerTests.cs
git commit -m "feat(game-night): add save/resume commands with async agent summary"
```

---

## Chunk 3: Backend SignalR + Background Services

### Task 10: Extend GameStateHub

**Files:**
- Modify: `apps/api/src/Api/Hubs/GameStateHub.cs`
- Test: `apps/api/tests/Api.Tests/Hubs/GameStateHub_ImprovvisataTests.cs`

- [ ] **Step 1: Write failing tests for new SignalR methods**

Test cases:
1. `DisputeResolved` broadcasts verdict to session group
2. `SessionPaused` notifies all participants, triggers graceful disconnect
3. `ScoreUpdated` broadcasts score change to session group
4. `AppBackgrounded` triggers auto-save (fire and forget)

- [ ] **Step 2: Add new methods to GameStateHub**

```csharp
// Add to GameStateHub.cs
public async Task NotifyDisputeResolved(string sessionId, RuleDisputeResponse verdict)
{
    await Clients.Group($"session:{sessionId}").SendAsync("DisputeResolved", verdict);
}

public async Task NotifySessionPaused(string sessionId)
{
    await Clients.Group($"session:{sessionId}").SendAsync("SessionPaused");
}

public async Task NotifyScoreUpdated(string sessionId, object scoreUpdate)
{
    await Clients.Group($"session:{sessionId}").SendAsync("ScoreUpdated", scoreUpdate);
}

public async Task AppBackgrounded(string sessionId)
{
    // Best-effort auto-save trigger
    await _mediator.Publish(new AppBackgroundedEvent(Guid.Parse(sessionId)));
}
```

- [ ] **Step 3: Create DisputeResolvedEvent SignalR handler**

This handler listens for `DisputeResolvedEvent` (MediatR notification) and calls `NotifyDisputeResolved` on the hub.

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Hubs/GameStateHub.cs \
       apps/api/tests/Api.Tests/Hubs/GameStateHub_ImprovvisataTests.cs
git commit -m "feat(game-night): extend GameStateHub with dispute, pause, score SignalR methods"
```

---

### Task 11: SessionAutoSaveBackgroundService

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/SessionAutoSaveBackgroundService.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/SessionAutoSaveBackgroundServiceTests.cs`
- Modify: `apps/api/src/Api/Program.cs` or service registration file

- [ ] **Step 1: Write failing test**

Test cases:
1. Service creates PauseSnapshot(IsAutoSave=true) for active sessions
2. Service skips sessions with Status != InProgress
3. Cleanup: deletes auto-saves when manual save occurs

- [ ] **Step 2: Implement background service**

```csharp
public class SessionAutoSaveBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionAutoSaveBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);
            await AutoSaveActiveSessions(stoppingToken);
        }
    }

    private async Task AutoSaveActiveSessions(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IPauseSnapshotRepository>();

        // Find all sessions with Status = InProgress
        // For each: create PauseSnapshot(IsAutoSave = true)
        // Log count of auto-saved sessions
    }
}
```

- [ ] **Step 2: Register in DI**

```csharp
services.AddHostedService<SessionAutoSaveBackgroundService>();
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/SessionAutoSaveBackgroundService.cs
git commit -m "feat(game-night): add auto-save background service (10-min interval)"
```

---

### Task 12: Add All New Endpoints to Routing

**Files:**
- Modify: `apps/api/src/Api/Routing/GameNightImprovvisataEndpoints.cs`

- [ ] **Step 1: Add all remaining endpoints**

Verify all endpoints are registered:
```csharp
// Existing:
// GET  /api/v1/game-night/bgg/search
// POST /api/v1/game-night/import-bgg

// New:
// POST /api/v1/game-night/start-session
// POST /api/v1/game-night/sessions/{id}/save
// POST /api/v1/game-night/sessions/{id}/resume
// POST /api/v1/game-night/sessions/{id}/disputes
// GET  /api/v1/game-night/sessions/{id}/disputes
// GET  /api/v1/game-night/games/{gameId}/disputes  (cross-session history)
```

- [ ] **Step 2: Run all backend tests**

```bash
cd apps/api && dotnet test --filter "Category=Unit"
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/GameNightImprovvisataEndpoints.cs
git commit -m "feat(game-night): complete endpoint routing for improvvisata API"
```

---

## Chunk 4: Frontend — Session UI Components

### Task 13: Live Session Zustand Store + SignalR Hook

**Files:**
- Create: `apps/web/src/lib/stores/live-session-store.ts`
- Create: `apps/web/src/lib/hooks/use-signalr-session.ts`
- Create: `apps/web/src/lib/hooks/use-session-scores.ts`
- Test: `apps/web/src/lib/hooks/__tests__/use-signalr-session.test.ts`

- [ ] **Step 1: Implement Zustand store**

```typescript
// live-session-store.ts
interface LiveSessionState {
  sessionId: string | null;
  gameName: string;
  status: 'InProgress' | 'Paused' | 'Completed';
  currentTurn: number;
  currentPhase: string | null;
  players: PlayerInfo[];
  scores: Record<string, number>;
  pendingProposals: ScoreProposal[];
  disputes: RuleDispute[];
  isConnected: boolean;
  isOffline: boolean;

  // Actions
  setSession: (session: LiveSessionData) => void;
  updateScore: (playerId: string, score: number) => void;
  addProposal: (proposal: ScoreProposal) => void;
  resolveProposal: (proposalId: string, accepted: boolean) => void;
  addDispute: (dispute: RuleDispute) => void;
  setConnected: (connected: boolean) => void;
  setOffline: (offline: boolean) => void;
}
```

- [ ] **Step 2: Implement SignalR hook**

```typescript
// use-signalr-session.ts
export function useSignalRSession(sessionId: string | null) {
  // 1. Create HubConnection to /hubs/game-state
  // 2. Join session group on connect
  // 3. Listen for: ScoreUpdated, DisputeResolved, SessionPaused, ProposeScore, ConfirmScore
  // 4. Update Zustand store on each event
  // 5. Handle reconnection (auto-rejoin group)
  // 6. Cleanup on unmount
  // Return: { connection, isConnected, sendScore, proposeScore, appBackgrounded }
}
```

- [ ] **Step 3: Write tests for hook lifecycle**

Test: connection established, events update store, cleanup on unmount, reconnection.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/stores/live-session-store.ts \
       apps/web/src/lib/hooks/use-signalr-session.ts \
       apps/web/src/lib/hooks/use-session-scores.ts \
       apps/web/src/lib/hooks/__tests__/use-signalr-session.test.ts
git commit -m "feat(game-night): add live session Zustand store and SignalR hook"
```

---

### Task 14: API Client Extensions

**Files:**
- Modify: `apps/web/src/lib/api/clients/liveSessionsClient.ts`

- [ ] **Step 1: Add new API methods**

```typescript
// Add to liveSessionsClient.ts
startImprovvisata: (privateGameId: string) =>
  httpClient.post<StartImprovvisataResponse>('/game-night/start-session', { privateGameId }),

submitDispute: (sessionId: string, description: string, raisedBy: string) =>
  httpClient.post<RuleDisputeResponse>(`/game-night/sessions/${sessionId}/disputes`, {
    description, raisedByPlayerName: raisedBy
  }),

getDisputeHistory: (sessionId: string) =>
  httpClient.get<RuleDispute[]>(`/game-night/sessions/${sessionId}/disputes`),

getGameDisputeHistory: (gameId: string) =>
  httpClient.get<RuleDispute[]>(`/game-night/games/${gameId}/disputes`),

createPauseSnapshot: (sessionId: string, photoIds?: string[]) =>
  httpClient.post<{ snapshotId: string }>(`/game-night/sessions/${sessionId}/save`, { finalPhotoIds: photoIds }),

resumeFromSnapshot: (sessionId: string) =>
  httpClient.post<ResumeSessionResponse>(`/game-night/sessions/${sessionId}/resume`),
```

- [ ] **Step 2: Add TypeScript types**

```typescript
interface StartImprovvisataResponse {
  sessionId: string;
  inviteCode: string;
  shareLink: string;
}
interface RuleDisputeResponse {
  id: string;
  verdict: string;
  ruleReferences: string[];
  note: string | null;
}
interface ResumeSessionResponse {
  sessionId: string;
  inviteCode: string;
  shareLink: string;
  agentRecap: string | null;
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/liveSessionsClient.ts
git commit -m "feat(game-night): extend liveSessionsClient with improvvisata API methods"
```

---

### Task 15: Session Layout + Card Parent

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx`
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx`
- Create: `apps/web/src/components/sessions/live/SessionCardParent.tsx`
- Create: `apps/web/src/components/sessions/live/SessionNavConfig.tsx`

- [ ] **Step 1: Create session layout with NavigationProvider**

Follow the pattern from Epic #5033 — `NavigationProvider` + `MiniNav` + `NavActionBar`.

```tsx
// layout.tsx
export default function LiveSessionLayout({ children, params }: { children: React.ReactNode; params: Promise<{ sessionId: string }> }) {
  const { sessionId } = use(params);
  return (
    <NavigationProvider>
      <SessionNavConfig sessionId={sessionId} />
      {children}
    </NavigationProvider>
  );
}
```

- [ ] **Step 2: Create SessionNavConfig**

Registers MiniNav tabs: Partita, Chat AI, Punteggi, Foto, Giocatori.

```tsx
// SessionNavConfig.tsx — renders null, calls useSetNavConfig()
export function SessionNavConfig({ sessionId }: { sessionId: string }) {
  const setNavConfig = useSetNavConfig();
  useEffect(() => {
    setNavConfig({
      miniNav: {
        tabs: [
          { label: 'Partita', href: `/sessions/live/${sessionId}` },
          { label: 'Chat AI', href: `/sessions/live/${sessionId}/agent` },
          { label: 'Punteggi', href: `/sessions/live/${sessionId}/scores` },
          { label: 'Foto', href: `/sessions/live/${sessionId}/photos` },
          { label: 'Giocatori', href: `/sessions/live/${sessionId}/players` },
        ]
      }
    });
  }, [sessionId, setNavConfig]);
  return null;
}
```

- [ ] **Step 3: Create SessionCardParent page**

Shows: game name, status badge, timer, player count, quick actions (Invita, Pausa, Salva & Esci).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/live/ \
       apps/web/src/components/sessions/live/SessionCardParent.tsx \
       apps/web/src/components/sessions/live/SessionNavConfig.tsx
git commit -m "feat(game-night): add session layout with CardStack navigation"
```

---

### Task 16: Scoreboard Child Card

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx`
- Create: `apps/web/src/components/sessions/live/ScoreBoard.tsx`
- Test: `apps/web/src/components/sessions/live/__tests__/ScoreBoard.test.tsx`

- [ ] **Step 1: Write tests for ScoreBoard component**

Test cases:
1. Renders player names and scores
2. Host sees +/- buttons, exact input field
3. Leader is highlighted
4. Pending proposals show approve/reject buttons
5. "Nuovo Round" button saves current scores

- [ ] **Step 2: Implement ScoreBoard component**

Uses `useSessionScores` hook (from Task 13) for real-time updates. Host actions call SignalR `sendScore`. Visual: colored player cards, leader indicator.

- [ ] **Step 3: Create page wrapper**

```tsx
// scores/page.tsx
export default function ScoresPage({ params }: { params: Promise<{ sessionId: string }> }) {
  const { sessionId } = use(params);
  return <ScoreBoard sessionId={sessionId} />;
}
```

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/scores/ \
       apps/web/src/components/sessions/live/ScoreBoard.tsx \
       apps/web/src/components/sessions/live/__tests__/ScoreBoard.test.tsx
git commit -m "feat(game-night): add real-time scoreboard child card"
```

---

### Task 17: Arbitro UI Components

**Files:**
- Create: `apps/web/src/components/sessions/live/ArbitroModal.tsx`
- Create: `apps/web/src/components/sessions/live/ArbitroVerdictCard.tsx`
- Create: `apps/web/src/components/sessions/live/DisputeHistory.tsx`
- Test: `apps/web/src/components/sessions/live/__tests__/ArbitroModal.test.tsx`

- [ ] **Step 1: Write tests for ArbitroModal**

Test cases:
1. Modal opens on button click
2. Form has description field + player dropdown
3. Submit calls `liveSessionsClient.submitDispute()`
4. Loading state shown during API call
5. Verdict card displayed on success

- [ ] **Step 2: Implement ArbitroModal**

```tsx
// ArbitroModal.tsx
export function ArbitroModal({ sessionId, players, onVerdictReceived }: ArbitroModalProps) {
  // shadcn Dialog with form
  // Description textarea + player select
  // Submit → loading "L'arbitro sta analizzando..." → verdict display
}
```

- [ ] **Step 3: Implement ArbitroVerdictCard**

Distinct styling: amber border, ⚖️ icon, structured layout (Verdict, Rule citation, Notes).

- [ ] **Step 4: Implement DisputeHistory**

Collapsible section showing past verdicts. Queries `getGameDisputeHistory` for cross-session history.

- [ ] **Step 5: Run tests — expect PASS**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/sessions/live/ArbitroModal.tsx \
       apps/web/src/components/sessions/live/ArbitroVerdictCard.tsx \
       apps/web/src/components/sessions/live/DisputeHistory.tsx \
       apps/web/src/components/sessions/live/__tests__/ArbitroModal.test.tsx
git commit -m "feat(game-night): add arbitro modal and verdict display components"
```

---

## Chunk 5: Frontend — Guest, Save/Resume, Integration

### Task 18: Guest Landing Page

**Files:**
- Create: `apps/web/src/app/join/[inviteToken]/page.tsx`
- Create: `apps/web/src/components/sessions/live/GuestScoreProposal.tsx`
- Test: `apps/web/src/app/join/__tests__/guest-join.test.tsx`

- [ ] **Step 1: Create guest page (public, no auth)**

```tsx
// join/[inviteToken]/page.tsx — NO (authenticated) route group
// This page is public (no session required)
export default function GuestJoinPage({ params }: { params: Promise<{ inviteToken: string }> }) {
  // 1. Check localStorage for participantToken → auto-rejoin
  // 2. Otherwise: show game name, host name, "Il tuo nome" input
  // 3. On submit: POST join session → get participantToken → save to localStorage
  // 4. After join: show minimal view with:
  //    - Current turn + phase
  //    - Live scoreboard (SignalR read-only)
  //    - "Proponi punteggio" button (self-only, max 3 pending)
}
```

- [ ] **Step 2: Implement GuestScoreProposal**

Simple form: score delta input, submit button. Business rules: self-only, -100 to +100, max 3 pending.

- [ ] **Step 3: Write tests for guest page**

Test cases:
1. Shows game name and name input when no participantToken in localStorage
2. Auto-rejoins when valid participantToken exists
3. After join: shows scoreboard (read-only) and propose button
4. Score proposal enforces self-only, delta limits, max 3 pending

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/join/ \
       apps/web/src/components/sessions/live/GuestScoreProposal.tsx
git commit -m "feat(game-night): add guest landing page with score proposals"
```

---

### Task 19: Invite Modal + QR Code

**Files:**
- Create: `apps/web/src/components/sessions/live/InviteModal.tsx`
- Test: `apps/web/src/components/sessions/live/__tests__/InviteModal.test.tsx`

- [ ] **Step 1: Install qrcode.react**

```bash
cd apps/web && pnpm add qrcode.react
```

- [ ] **Step 2: Implement InviteModal**

```tsx
// InviteModal.tsx
import { QRCodeSVG } from 'qrcode.react';

export function InviteModal({ inviteCode, shareLink, onClose }: InviteModalProps) {
  return (
    <Dialog>
      <QRCodeSVG value={shareLink} size={200} />
      <div>Codice sessione: <strong>{inviteCode}</strong></div>
      <Button onClick={() => navigator.clipboard.writeText(shareLink)}>
        Copia link
      </Button>
    </Dialog>
  );
}
```

- [ ] **Step 3: Write tests**

Test: QR rendered, session code displayed, copy button works.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/sessions/live/InviteModal.tsx \
       apps/web/src/components/sessions/live/__tests__/InviteModal.test.tsx \
       apps/web/pnpm-lock.yaml apps/web/package.json
git commit -m "feat(game-night): add invite modal with QR code and share link"
```

---

### Task 20: Save & Resume UI

**Files:**
- Create: `apps/web/src/components/sessions/live/SaveSessionModal.tsx`
- Create: `apps/web/src/components/sessions/live/PausedSessionCard.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx`
- Test: `apps/web/src/components/sessions/live/__tests__/SaveSessionModal.test.tsx`
- Test: `apps/web/src/components/sessions/live/__tests__/PausedSessionCard.test.tsx`

- [ ] **Step 1: Write tests for SaveSessionModal**

Test cases:
1. Confirmation modal shown on "Salva & Esci"
2. Optional photo capture prompt
3. Calls `createPauseSnapshot()` on confirm
4. Shows success message with redirect to library

- [ ] **Step 2: Implement SaveSessionModal**

```tsx
export function SaveSessionModal({ sessionId, onSaved }: SaveSessionModalProps) {
  // Confirmation dialog
  // Optional: "Scatta foto finale?" with camera capture
  // Submit → loading → "Sessione salvata!" → redirect
}
```

- [ ] **Step 3: Write tests for PausedSessionCard**

Test cases:
1. Shows game name, date saved, score preview
2. Photo thumbnail if available
3. "Riprendi" button calls `resumeFromSnapshot()`
4. On resume success → navigates to live session page

- [ ] **Step 4: Implement PausedSessionCard**

```tsx
export function PausedSessionCard({ session, snapshot }: PausedSessionCardProps) {
  // Card showing: game name, saved date, scores, photo preview
  // "▶️ Riprendi" button → resume API call → navigate to /sessions/live/{id}
}
```

- [ ] **Step 5: Add "Sessioni in pausa" section to PrivateGame detail page**

Modify `apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx`:
- Query paused sessions for this game
- Render `PausedSessionCard` for each
- "Avvia nuova sessione" button → `startImprovvisata()` API call

- [ ] **Step 6: Run tests — expect PASS**

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/sessions/live/SaveSessionModal.tsx \
       apps/web/src/components/sessions/live/PausedSessionCard.tsx \
       apps/web/src/components/sessions/live/__tests__/SaveSessionModal.test.tsx \
       apps/web/src/components/sessions/live/__tests__/PausedSessionCard.test.tsx \
       apps/web/src/app/\(authenticated\)/library/private/\[privateGameId\]/page.tsx
git commit -m "feat(game-night): add save/resume UI with paused session cards"
```

---

### Task 21: Players + Photos Child Cards

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/players/page.tsx`
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/photos/page.tsx`
- Create: `apps/web/src/components/sessions/live/PlayerList.tsx`

- [ ] **Step 1: Implement PlayerList component**

Shows: player name, online/offline indicator, invite button. Uses SignalR presence.

- [ ] **Step 2: Create players page**

```tsx
export default function PlayersPage({ params }: { params: Promise<{ sessionId: string }> }) {
  const { sessionId } = use(params);
  return (
    <div>
      <PlayerList sessionId={sessionId} />
      <InviteModal /> {/* Triggered by button */}
    </div>
  );
}
```

- [ ] **Step 3: Create photos page**

Camera capture via `<input type="file" accept="image/*" capture="environment">`. Grid display with timestamps. Upload to `SessionAttachment` endpoint.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/players/ \
       apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/photos/ \
       apps/web/src/components/sessions/live/PlayerList.tsx
git commit -m "feat(game-night): add players and photos child card pages"
```

---

### Task 22: Agent Chat Child Card + Session Context

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/agent/page.tsx`

- [ ] **Step 1: Create agent chat page**

Reuse existing toolkit chat experience. Pass `sessionId` as context parameter so the backend injects session context (players, turn, scores, previous disputes) into the agent's system prompt.

```tsx
// agent/page.tsx
export default function AgentPage({ params }: { params: Promise<{ sessionId: string }> }) {
  const { sessionId } = use(params);
  // Reuse existing chat component from toolkit
  // Add ArbitroModal button in action bar
  // Add DisputeHistory collapsible section
  // On resumed session: show recap as first message
}
```

- [ ] **Step 2: Verify agent prompt injection works**

The backend needs to inject session context into the agent system prompt when `sessionId` is provided in chat queries. Check how existing toolkit chat sends the session context — it may already pass `toolkitSessionId`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/agent/
git commit -m "feat(game-night): add agent chat child card with arbitro button"
```

---

### Task 23: Enhanced SuccessState + Processing Progress

**Files:**
- Modify: `apps/web/src/components/library/add-game-sheet/steps/SuccessState.tsx`
- Test: `apps/web/src/components/library/add-game-sheet/steps/__tests__/SuccessState.test.tsx` (extend existing)

- [ ] **Step 1: Write tests for new SuccessState behavior**

Test cases:
1. Shows progress indicator when PDF is processing
2. Shows toast on AgentAutoCreatedEvent
3. Shows error toast on PdfProcessingFailed with retry button

- [ ] **Step 2: Add processing progress indicator**

After "Game added + PDF uploaded", show:
- Progress stages: Chunking → Embedding → Indexing → Ready
- Animated progress bar
- Message: "Stiamo analizzando il regolamento... Ti avviseremo quando l'agente è pronto"
- On `AgentAutoCreatedEvent` via SSE: toast with "Apri" action

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/library/add-game-sheet/steps/SuccessState.tsx
git commit -m "feat(game-night): add PDF processing progress indicator to SuccessState"
```

---

### Task 24: Final Integration Test + Cleanup

- [ ] **Step 1: Run full backend test suite**

```bash
cd apps/api && dotnet test
```

- [ ] **Step 2: Run frontend tests**

```bash
cd apps/web && pnpm test
```

- [ ] **Step 3: Run frontend type check**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 4: Run frontend lint**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 5: Manual smoke test**

Start API + web locally. Walk through the complete flow:
1. Library → Add Game → BGG Search → Select Catan → Upload PDF → Accept disclaimer
2. Wait for notification "Agent ready"
3. Open PrivateGame → "Avvia Sessione"
4. Share invite link → open in incognito (guest)
5. Add scores, try Arbitro, take photo
6. Save & Exit → verify paused session in library
7. Resume → verify recap message

- [ ] **Step 6: Final commit**

```bash
git commit -m "feat(game-night): complete vertical slice integration"
```

---

## Dependencies Between Tasks

```
Task 1 (RuleDisputeEntry VO)
  └→ Task 2 (Disputes on LiveGameSession)
      └→ Task 4 (Migration)
Task 3 (PauseSnapshot entity)
  └→ Task 4 (Migration)
Task 5 (Domain events) → Task 6, 8, 8b, 9
Task 7 (StartSession) — independent after Task 4
Task 8 (Arbitro) — needs Task 2, 5
Task 8b (Session context) — needs Task 2, 5 (agent prompt injection)
Task 9 (Save/Resume) — needs Task 3, 5
Task 10 (SignalR) — needs Task 5
Task 11 (AutoSave) — needs Task 3, 9
Task 12 (Routing) — needs Task 7, 8, 9
Task 13 (Store + hooks) — independent (frontend)
Task 14 (API client) — independent (frontend)
Task 15 (Layout) — needs Task 13
Task 16 (Scoreboard) — needs Task 13, 14
Task 17 (Arbitro UI) — needs Task 14
Task 18 (Guest) — needs Task 14
Task 19 (Invite) — independent
Task 20 (Save/Resume UI) — needs Task 14
Task 21 (Players/Photos) — needs Task 13
Task 22 (Agent chat) — needs Task 17
Task 23 (SuccessState) — independent
Task 24 (Integration) — needs all
```

**Parallelization opportunities:**
- Tasks 1-3 can run in parallel (domain model)
- Tasks 13-14 can run in parallel with backend tasks
- Tasks 16-19 can run in parallel (independent UI components)
- Tasks 21-23 can run in parallel (independent pages)

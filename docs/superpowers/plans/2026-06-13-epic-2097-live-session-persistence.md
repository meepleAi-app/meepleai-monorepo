# EPIC #2097 — ADR-060 Live Session Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist `LiveGameSession` aggregate to PostgreSQL via existing `LiveGameSessionEntityConfiguration` so live board game sessions survive container restarts and can scale to multiple API instances.

**Architecture:** Replace `ConcurrentDictionary`-based `LiveSessionRepository` with EF Core implementation extending `RepositoryBase`. Add Domain `Reconstitute()` static factory + `ToEntity()` extension to map between Domain `LiveGameSession` (jsonb-backed value objects, 4 child collections) and Infrastructure `LiveGameSessionEntity` (relational with jsonb columns). All Command handlers inject `IUnitOfWork` and call `SaveChangesAsync` explicitly so EF domain events dispatch post-success transactionally.

**Tech Stack:** .NET 9 · EF Core 9 · PostgreSQL 16 · xUnit + Testcontainers + FluentAssertions + NSubstitute · MediatR · Prometheus-net.

**Branch:** `feature/issue-2097-live-session-persistence` (parent: `main-dev`).

**Effort estimate:** 4–6 days (ADR-060 estimated 1–3gg but underestimated mapper complexity and 5 missing Entity columns surfaced in Phase 0).

---

## Pre-flight: Branch safety check

Run from the repo root (`D:\Repositories\meepleai-monorepo-main`):

```pwsh
git branch --show-current   # MUST print "main-dev"
git status                  # MUST be clean
git pull --ff-only          # MUST succeed
git checkout -b feature/issue-2097-live-session-persistence
git config branch.feature/issue-2097-live-session-persistence.parent main-dev
```

If `git branch --show-current` prints anything other than `main-dev`, STOP and run `git checkout main-dev && git pull` first. This avoids the multi-checkout pitfall recorded in user memory `branch-hygiene` (#806).

---

## File Structure Overview

### Created files

| Path | Responsibility |
|---|---|
| `apps/api/src/Api/Infrastructure/Migrations/{Timestamp}_LiveSessionAddMissingColumns.cs` | EF migration: add `phase_names_json`, `current_phase_index`, `snapshot_trigger_config_json`, `last_snapshot_timestamp`, `turn_advance_policy` columns to `live_game_sessions`. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs` | Static class with `ToEntity(LiveGameSession)` + `ToDomain(LiveGameSessionEntity)` mapping. Owns JSON serialization for 5 jsonb columns and round-trips 4 child collections. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Observability/LiveSessionMetrics.cs` | Prometheus-net wrapper exposing `live_sessions_active_gauge`, `live_session_duration_histogram`, `live_session_writes_total{op}`. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/HealthChecks/LiveSessionPersistenceHealthCheck.cs` | `IHealthCheck` implementation that runs `SELECT 1 FROM live_game_sessions LIMIT 1` and measures latency. |
| `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs` | Testcontainers PostgreSQL integration tests covering AC-1..AC-5. |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Mappers/LiveGameSessionMapperTests.cs` | Round-trip mapper unit tests (no DB). |
| `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionReconstituteTests.cs` | Tests for the new `LiveGameSession.Reconstitute(...)` factory. |

### Modified files

| Path | Change |
|---|---|
| `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs` | Add 5 properties for the missing Domain fields. |
| `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs` | Add EF property mapping for the 5 new columns (jsonb where applicable). |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` | Add `Reconstitute(...)` static factory. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/LiveSessionRepository.cs` | Replace `ConcurrentDictionary` with EF-backed implementation extending `RepositoryBase`. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/ILiveSessionRepository.cs` | Update XML doc to reflect EF-backed implementation. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs` | Flip `AddSingleton` → `AddScoped` for `ILiveSessionRepository`. |
| `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/*.cs` (~20 handlers) | Inject `IUnitOfWork`; call `SaveChangesAsync` after `AddAsync` / `UpdateAsync`. |
| `apps/api/src/Api/Routing/MetricsEndpoints.cs` (if exists) **or** `Program.cs` | Register `LiveSessionMetrics` + health check. |
| `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md` | Status `Proposed` → `Accepted` at the end of Phase 5. |

### Untouched on purpose

- `apps/api/src/Api/Infrastructure/Entities/GameManagement/SessionPlayerEntity.cs`, `SessionTeamEntity.cs`, `LiveRoundScoreEntity.cs`, `LiveTurnRecordEntity.cs` — already exist as relational child tables.
- `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` — endpoints already use `IMediator.Send(...)` (CQRS rule satisfied).
- All `*EventHandler.cs` consuming `LiveSession*Event` — domain events flow unchanged via `IDomainEventCollector`.

---

## Phase summary table

| Phase | Priority | Deliverable | Effort |
|---|---|---|---|
| 0 | P0 | Entity schema audit + 5 missing columns + EF migration | 0.5gg |
| 1 | P0 | EF-backed `LiveSessionRepository` + mapper + Domain `Reconstitute()` + DI scope flip | 2–3gg |
| 2 | P0 | `IUnitOfWork.SaveChangesAsync` wired into ~20 Command handlers; domain event dispatch verified | 0.5–1gg |
| 3 | P0 | 5 integration tests (AC-1..AC-5) green via Testcontainers | 0.5gg |
| 4 | P2 | Prometheus metrics + health check | 0.5gg |
| 5 | P3 | Optional graceful-drain admin step + ADR status flip Proposed → Accepted | 1h |

---

# Phase 0 — Entity schema audit + missing columns

The Domain aggregate `LiveGameSession` has **5 fields with no Entity column**: `PhaseNames` (`string[]`), `CurrentPhaseIndex` (`int`), `SnapshotTriggerConfig` (object), `LastSnapshotTimestamp` (`DateTime?`), `TurnAdvancePolicy` (enum). Phase 1's mapper requires these columns or runtime data loss on restart. ADR-060 did not surface this — Phase 0 closes the gap before Phase 1 starts.

The Entity also has `TotalPausedDurationMs` (Issue #216) that the Domain doesn't expose — leave it alone for now; the mapper will preserve it on round-trip by reading it back from the DB and writing it back unchanged. Tracked as out-of-scope below.

### Task 0.1: Add missing properties to `LiveGameSessionEntity`

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs`

- [ ] **Step 1: Add the 5 properties**

Open the file and insert after the `Notes` property (line 52):

```csharp
    // Turn phase configuration (added by Issue #2097 / ADR-060 schema audit)
    public string? PhaseNamesJson { get; set; } // string[] serialized as jsonb
    public int CurrentPhaseIndex { get; set; }
    public int TurnAdvancePolicy { get; set; } // TurnAdvancePolicy enum: 0=Manual, 1=Auto

    // Snapshot debounce state (added by Issue #2097 / ADR-060 schema audit)
    public string? SnapshotTriggerConfigJson { get; set; } // SnapshotTriggerConfig serialized as jsonb
    public DateTime? LastSnapshotTimestamp { get; set; }
```

- [ ] **Step 2: Compile (expect: passes — pure C# add)**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: `Build succeeded`

### Task 0.2: Add EF column mappings

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs`

- [ ] **Step 1: Insert column mappings after the existing `Notes` block (line 82)**

```csharp
        builder.Property(e => e.CurrentPhaseIndex)
            .HasColumnName("current_phase_index")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.TurnAdvancePolicy)
            .HasColumnName("turn_advance_policy")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.LastSnapshotTimestamp)
            .HasColumnName("last_snapshot_timestamp");

        builder.Property(e => e.PhaseNamesJson)
            .HasColumnName("phase_names_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.SnapshotTriggerConfigJson)
            .HasColumnName("snapshot_trigger_config_json")
            .HasColumnType("jsonb");
```

- [ ] **Step 2: Build to confirm**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: `Build succeeded`

### Task 0.3: Create EF migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<auto>_LiveSessionAddMissingColumns.cs`

- [ ] **Step 1: Generate migration**

Run from `apps/api/src/Api/`:

```pwsh
dotnet ef migrations add LiveSessionAddMissingColumns
```

Expected: a new migration file appears under `Infrastructure/Migrations/`.

> If `dotnet ef` errors with "command not found" or "could not resolve types", the cache pitfall from memory `dotnet-ef-nuget-pitfall` applies: run `dotnet restore` first, then retry.

- [ ] **Step 2: Inspect the generated migration**

Run: `git diff apps/api/src/Api/Infrastructure/Migrations/`

Confirm it ONLY adds the 5 columns to `live_game_sessions`. If it touches unrelated tables, you have model drift — revert and reconcile before continuing.

- [ ] **Step 3: Apply migration to local DB**

Run: `dotnet ef database update`
Expected: `Done.`

Verify columns exist:

```pwsh
docker exec meepleai-postgres psql -U meeple -d meepleai -c "\d live_game_sessions" | findstr "current_phase_index turn_advance_policy phase_names_json"
```

Expected: 3+ matching lines.

- [ ] **Step 4: Commit**

```pwsh
git add apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs `
        apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs `
        apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(live-session): #2097 add 5 missing Entity columns + EF migration"
```

---

# Phase 1 — Refactor `LiveSessionRepository` to EF-backed

This is the largest phase. We do it TDD-style: write the mapper round-trip tests first (no DB), then the Domain `Reconstitute()` tests, then swap the repository implementation.

### Task 1.1: Add `LiveGameSession.Reconstitute(...)` factory

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionReconstituteTests.cs` (new)

- [ ] **Step 1: Write the failing test**

Create `LiveGameSessionReconstituteTests.cs` with this content (NSubstitute is already imported in the test project):

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

public class LiveGameSessionReconstituteTests
{
    [Fact]
    public void Reconstitute_PopulatesScalarsAndClearsDomainEvents()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddMinutes(5);
        var scoringConfig = SessionScoringConfig.CreateDefault();

        // Act
        var session = LiveGameSession.Reconstitute(
            id: id,
            sessionCode: "ABC123",
            gameId: null,
            gameName: "Mage Knight",
            toolkitId: null,
            createdByUserId: userId,
            visibility: PlayRecordVisibility.Private,
            groupId: null,
            status: LiveSessionStatus.InProgress,
            createdAt: createdAt,
            startedAt: createdAt.AddSeconds(30),
            pausedAt: null,
            completedAt: null,
            updatedAt: updatedAt,
            lastSavedAt: null,
            currentTurnIndex: 3,
            currentPhaseIndex: 1,
            phaseNames: new[] { "Setup", "Action", "End" },
            snapshotTriggerConfig: null,
            lastSnapshotTimestamp: null,
            scoringConfig: scoringConfig,
            gameState: null,
            notes: "first turn ok",
            agentMode: AgentSessionMode.None,
            chatSessionId: null,
            turnAdvancePolicy: TurnAdvancePolicy.Manual,
            rowVersion: new byte[] { 1, 2, 3, 4 },
            players: Array.Empty<LiveSessionPlayer>(),
            teams: Array.Empty<LiveSessionTeam>(),
            turnOrder: Array.Empty<Guid>(),
            roundScores: Array.Empty<RoundScore>(),
            turnRecords: Array.Empty<TurnRecord>(),
            disputes: Array.Empty<RuleDisputeEntry>(),
            setupChecklist: null);

        // Assert
        session.Id.Should().Be(id);
        session.SessionCode.Should().Be("ABC123");
        session.Status.Should().Be(LiveSessionStatus.InProgress);
        session.CurrentTurnIndex.Should().Be(3);
        session.CurrentPhaseIndex.Should().Be(1);
        session.PhaseNames.Should().BeEquivalentTo(new[] { "Setup", "Action", "End" });
        session.Notes.Should().Be("first turn ok");
        session.DomainEvents.Should().BeEmpty("Reconstitute MUST NOT raise events");
    }
}
```

- [ ] **Step 2: Run the test (expect: FAIL with 'Reconstitute' not defined)**

Run from `apps/api/`:

```pwsh
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionReconstitute"
```

Expected: compile error `'LiveGameSession' does not contain a definition for 'Reconstitute'`.

- [ ] **Step 3: Implement `Reconstitute(...)` on `LiveGameSession.cs`**

Open `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` and add this method right after `Create(...)` (around line 143):

```csharp
    /// <summary>
    /// Reconstitutes a <see cref="LiveGameSession"/> from persistence without raising
    /// domain events. Used by <see cref="Infrastructure.Persistence.LiveSessionRepository"/>
    /// to materialise an aggregate from the database. NOT for application-level callers —
    /// use <see cref="Create"/> for new sessions.
    /// </summary>
    internal static LiveGameSession Reconstitute(
        Guid id,
        string sessionCode,
        Guid? gameId,
        string gameName,
        Guid? toolkitId,
        Guid createdByUserId,
        PlayRecordVisibility visibility,
        Guid? groupId,
        LiveSessionStatus status,
        DateTime createdAt,
        DateTime? startedAt,
        DateTime? pausedAt,
        DateTime? completedAt,
        DateTime updatedAt,
        DateTime? lastSavedAt,
        int currentTurnIndex,
        int currentPhaseIndex,
        string[] phaseNames,
        SnapshotTriggerConfig? snapshotTriggerConfig,
        DateTime? lastSnapshotTimestamp,
        SessionScoringConfig scoringConfig,
        JsonDocument? gameState,
        string? notes,
        AgentSessionMode agentMode,
        Guid? chatSessionId,
        TurnAdvancePolicy turnAdvancePolicy,
        byte[] rowVersion,
        IEnumerable<LiveSessionPlayer> players,
        IEnumerable<LiveSessionTeam> teams,
        IEnumerable<Guid> turnOrder,
        IEnumerable<RoundScore> roundScores,
        IEnumerable<TurnRecord> turnRecords,
        IEnumerable<RuleDisputeEntry> disputes,
        SetupChecklistData? setupChecklist)
    {
        var session = new LiveGameSession
        {
            Id = id,
            SessionCode = sessionCode,
            GameId = gameId,
            GameName = gameName,
            ToolkitId = toolkitId,
            CreatedByUserId = createdByUserId,
            Visibility = visibility,
            GroupId = groupId,
            Status = status,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            PausedAt = pausedAt,
            CompletedAt = completedAt,
            UpdatedAt = updatedAt,
            LastSavedAt = lastSavedAt,
            CurrentTurnIndex = currentTurnIndex,
            CurrentPhaseIndex = currentPhaseIndex,
            PhaseNames = phaseNames ?? Array.Empty<string>(),
            SnapshotTriggerConfig = snapshotTriggerConfig,
            LastSnapshotTimestamp = lastSnapshotTimestamp,
            ScoringConfig = scoringConfig,
            GameState = gameState,
            Notes = notes,
            AgentMode = agentMode,
            ChatSessionId = chatSessionId,
            TurnAdvancePolicy = turnAdvancePolicy,
            RowVersion = rowVersion ?? Array.Empty<byte>(),
            _setupChecklist = setupChecklist
        };

        session._players.AddRange(players);
        session._teams.AddRange(teams);
        session._turnOrder.AddRange(turnOrder);
        session._roundScores.AddRange(roundScores);
        session._turnRecords.AddRange(turnRecords);
        session._disputes.AddRange(disputes);

        // Critical: Reconstitute MUST NOT raise events (we are not creating anything new).
        session.ClearDomainEvents();

        return session;
    }
```

Note: `_setupChecklist` is a private field — assigning it from the object initializer requires you to make the field internal-accessible OR pass it through a private constructor. The simplest path is to relax `_setupChecklist` to a property with a private setter and assign it via `SetupChecklistData = setupChecklist` in the initializer. Either approach is acceptable; keep the field name surface stable for the `Players`/`Teams` getters.

If the object initializer cannot reach the private field, use this alternative final block instead:

```csharp
        if (setupChecklist != null)
        {
            session.SetSetupChecklist(setupChecklist);
        }
```

(`SetSetupChecklist` is public, see line 625 of the Domain file.)

- [ ] **Step 4: Run the test (expect: PASS)**

```pwsh
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionReconstitute"
```

Expected: `Passed!`

- [ ] **Step 5: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs `
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/LiveGameSessionReconstituteTests.cs
git commit -m "feat(live-session): #2097 LiveGameSession.Reconstitute factory for EF mapper"
```

### Task 1.2: Create `LiveGameSessionMapper` (Domain ↔ Entity)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Mappers/LiveGameSessionMapperTests.cs` (new)

- [ ] **Step 1: Write the failing round-trip test**

Create `LiveGameSessionMapperTests.cs`:

```csharp
using System;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure.Mappers;

public class LiveGameSessionMapperTests
{
    [Fact]
    public void RoundTrip_NewlyCreatedSession_PreservesScalarsAndCollections()
    {
        // Arrange — start from a Domain aggregate created via factory
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var original = LiveGameSession.Create(
            id: sessionId,
            createdByUserId: userId,
            gameName: "Mage Knight",
            timeProvider: TimeProvider.System,
            gameId: null,
            visibility: PlayRecordVisibility.Private,
            groupId: null,
            scoringConfig: SessionScoringConfig.CreateDefault(),
            agentMode: AgentSessionMode.None,
            turnAdvancePolicy: TurnAdvancePolicy.Manual);
        original.AddPlayer(userId: userId, displayName: "Aaron", color: PlayerColor.Red);
        original.ConfigurePhases(new[] { "Movement", "Action", "End" });

        // Act — round-trip
        var entity = LiveGameSessionMapper.ToEntity(original);
        var roundTripped = LiveGameSessionMapper.ToDomain(entity);

        // Assert — scalars
        roundTripped.Id.Should().Be(original.Id);
        roundTripped.SessionCode.Should().Be(original.SessionCode);
        roundTripped.GameName.Should().Be("Mage Knight");
        roundTripped.Status.Should().Be(LiveSessionStatus.Created);
        roundTripped.PhaseNames.Should().BeEquivalentTo(new[] { "Movement", "Action", "End" });
        roundTripped.CurrentPhaseIndex.Should().Be(0);
        roundTripped.TurnAdvancePolicy.Should().Be(TurnAdvancePolicy.Manual);

        // Assert — child collections
        roundTripped.Players.Should().HaveCount(1);
        roundTripped.Players[0].DisplayName.Should().Be("Aaron");
        roundTripped.Players[0].Role.Should().Be(PlayerRole.Host, "first player is auto-host");
        roundTripped.TurnOrder.Should().ContainSingle(pid => pid == roundTripped.Players[0].Id);

        // Assert — domain events must be cleared after Reconstitute
        roundTripped.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ToEntity_PreservesRowVersion_ForOptimisticConcurrency()
    {
        var session = LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            gameName: "X");

        var entity = LiveGameSessionMapper.ToEntity(session);

        entity.RowVersion.Should().NotBeNull(
            "EF needs a non-null RowVersion to evaluate concurrency token equality on UPDATE");
    }
}
```

- [ ] **Step 2: Run the test (expect: FAIL with 'LiveGameSessionMapper' not found)**

```pwsh
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionMapper"
```

- [ ] **Step 3: Implement the mapper**

Create `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/LiveGameSessionMapper.cs`:

```csharp
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure.Entities.GameManagement;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;

/// <summary>
/// Bidirectional mapper between Domain <see cref="LiveGameSession"/> and Infrastructure
/// <see cref="LiveGameSessionEntity"/>. Owns JSON serialization for 5 jsonb columns
/// (ScoringConfigJson, GameStateJson, TurnOrderJson, DisputesJson, SetupChecklistJson,
/// PhaseNamesJson, SnapshotTriggerConfigJson) and round-trips 4 child collections.
/// Issue #2097 / ADR-060.
/// </summary>
internal static class LiveGameSessionMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static LiveGameSessionEntity ToEntity(LiveGameSession domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        var entity = new LiveGameSessionEntity
        {
            Id = domain.Id,
            SessionCode = domain.SessionCode,
            GameId = domain.GameId,
            GameName = domain.GameName,
            ToolkitId = domain.ToolkitId,
            CreatedByUserId = domain.CreatedByUserId,
            Visibility = (int)domain.Visibility,
            GroupId = domain.GroupId,
            Status = (int)domain.Status,
            CurrentTurnIndex = domain.CurrentTurnIndex,
            CurrentPhaseIndex = domain.CurrentPhaseIndex,
            TurnAdvancePolicy = (int)domain.TurnAdvancePolicy,
            CreatedAt = domain.CreatedAt,
            StartedAt = domain.StartedAt,
            PausedAt = domain.PausedAt,
            CompletedAt = domain.CompletedAt,
            UpdatedAt = domain.UpdatedAt,
            LastSavedAt = domain.LastSavedAt,
            LastSnapshotTimestamp = domain.LastSnapshotTimestamp,
            ScoringConfigJson = JsonSerializer.Serialize(domain.ScoringConfig, JsonOptions),
            GameStateJson = domain.GameState?.RootElement.GetRawText(),
            TurnOrderJson = JsonSerializer.Serialize(domain.TurnOrder, JsonOptions),
            DisputesJson = domain.Disputes.Count > 0
                ? JsonSerializer.Serialize(domain.Disputes, JsonOptions)
                : null,
            SetupChecklistJson = domain.SetupChecklist != null
                ? JsonSerializer.Serialize(domain.SetupChecklist, JsonOptions)
                : null,
            PhaseNamesJson = domain.PhaseNames.Length > 0
                ? JsonSerializer.Serialize(domain.PhaseNames, JsonOptions)
                : null,
            SnapshotTriggerConfigJson = domain.SnapshotTriggerConfig != null
                ? JsonSerializer.Serialize(domain.SnapshotTriggerConfig, JsonOptions)
                : null,
            Notes = domain.Notes,
            AgentMode = (int)domain.AgentMode,
            ChatSessionId = domain.ChatSessionId,
            RowVersion = domain.RowVersion.Length > 0 ? domain.RowVersion : Array.Empty<byte>()
        };

        // Child collections — relational tables, not jsonb
        foreach (var player in domain.Players)
        {
            entity.Players.Add(new SessionPlayerEntity
            {
                Id = player.Id,
                LiveGameSessionId = domain.Id,
                UserId = player.UserId,
                DisplayName = player.DisplayName,
                Color = (int)player.Color,
                Role = (int)player.Role,
                TeamId = player.TeamId,
                AvatarUrl = player.AvatarUrl,
                IsActive = player.IsActive,
                TotalScore = player.TotalScore,
                CurrentRank = player.CurrentRank,
                JoinedAt = player.JoinedAt
            });
        }

        foreach (var team in domain.Teams)
        {
            entity.Teams.Add(new SessionTeamEntity
            {
                Id = team.Id,
                LiveGameSessionId = domain.Id,
                Name = team.Name,
                Color = team.Color
            });
        }

        foreach (var score in domain.RoundScores)
        {
            entity.RoundScores.Add(new LiveRoundScoreEntity
            {
                Id = Guid.NewGuid(),
                LiveGameSessionId = domain.Id,
                PlayerId = score.PlayerId,
                Round = score.Round,
                Dimension = score.Dimension,
                Value = score.Value,
                Unit = score.Unit,
                RecordedAt = score.RecordedAt
            });
        }

        foreach (var record in domain.TurnRecords)
        {
            entity.TurnRecords.Add(new LiveTurnRecordEntity
            {
                Id = Guid.NewGuid(),
                LiveGameSessionId = domain.Id,
                TurnIndex = record.TurnIndex,
                PlayerId = record.PlayerId,
                StartedAt = record.StartedAt,
                EndedAt = record.EndedAt,
                PhaseIndex = record.PhaseIndex,
                PhaseName = record.PhaseName
            });
        }

        return entity;
    }

    public static LiveGameSession ToDomain(LiveGameSessionEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var scoringConfig = JsonSerializer.Deserialize<SessionScoringConfig>(
            entity.ScoringConfigJson, JsonOptions)
            ?? SessionScoringConfig.CreateDefault();

        var turnOrder = string.IsNullOrEmpty(entity.TurnOrderJson)
            ? Array.Empty<Guid>()
            : JsonSerializer.Deserialize<List<Guid>>(entity.TurnOrderJson, JsonOptions) ?? new List<Guid>();

        var disputes = string.IsNullOrEmpty(entity.DisputesJson)
            ? Array.Empty<RuleDisputeEntry>()
            : JsonSerializer.Deserialize<List<RuleDisputeEntry>>(entity.DisputesJson, JsonOptions)
                ?? new List<RuleDisputeEntry>();

        var setupChecklist = string.IsNullOrEmpty(entity.SetupChecklistJson)
            ? null
            : JsonSerializer.Deserialize<SetupChecklistData>(entity.SetupChecklistJson, JsonOptions);

        var phaseNames = string.IsNullOrEmpty(entity.PhaseNamesJson)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(entity.PhaseNamesJson, JsonOptions) ?? Array.Empty<string>();

        var snapshotTriggerConfig = string.IsNullOrEmpty(entity.SnapshotTriggerConfigJson)
            ? null
            : JsonSerializer.Deserialize<SnapshotTriggerConfig>(entity.SnapshotTriggerConfigJson, JsonOptions);

        JsonDocument? gameState = string.IsNullOrEmpty(entity.GameStateJson)
            ? null
            : JsonDocument.Parse(entity.GameStateJson);

        var players = entity.Players.Select(p => new LiveSessionPlayer(
            id: p.Id,
            sessionId: entity.Id,
            userId: p.UserId,
            displayName: p.DisplayName,
            color: (PlayerColor)p.Color,
            role: (PlayerRole)p.Role,
            joinedAt: p.JoinedAt,
            avatarUrl: p.AvatarUrl)).ToList();

        // Restore mutable player state (score / rank / team / active) — these aren't ctor params
        foreach (var entityPlayer in entity.Players)
        {
            var domainPlayer = players.First(dp => dp.Id == entityPlayer.Id);
            domainPlayer.UpdateScore(entityPlayer.TotalScore, entityPlayer.CurrentRank);
            if (entityPlayer.TeamId.HasValue) domainPlayer.AssignToTeam(entityPlayer.TeamId.Value);
            if (!entityPlayer.IsActive) domainPlayer.Deactivate();
        }

        var teams = entity.Teams.Select(t => new LiveSessionTeam(
            id: t.Id,
            sessionId: entity.Id,
            name: t.Name,
            color: t.Color)).ToList();

        var roundScores = entity.RoundScores.Select(s => new RoundScore(
            playerId: s.PlayerId,
            round: s.Round,
            dimension: s.Dimension,
            value: s.Value,
            recordedAt: s.RecordedAt,
            unit: s.Unit)).ToList();

        var turnRecords = entity.TurnRecords.Select(t => new TurnRecord(
            turnIndex: t.TurnIndex,
            playerId: t.PlayerId,
            startedAt: t.StartedAt,
            phaseIndex: t.PhaseIndex,
            phaseName: t.PhaseName,
            endedAt: t.EndedAt)).ToList();

        return LiveGameSession.Reconstitute(
            id: entity.Id,
            sessionCode: entity.SessionCode,
            gameId: entity.GameId,
            gameName: entity.GameName,
            toolkitId: entity.ToolkitId,
            createdByUserId: entity.CreatedByUserId,
            visibility: (PlayRecordVisibility)entity.Visibility,
            groupId: entity.GroupId,
            status: (LiveSessionStatus)entity.Status,
            createdAt: entity.CreatedAt,
            startedAt: entity.StartedAt,
            pausedAt: entity.PausedAt,
            completedAt: entity.CompletedAt,
            updatedAt: entity.UpdatedAt,
            lastSavedAt: entity.LastSavedAt,
            currentTurnIndex: entity.CurrentTurnIndex,
            currentPhaseIndex: entity.CurrentPhaseIndex,
            phaseNames: phaseNames,
            snapshotTriggerConfig: snapshotTriggerConfig,
            lastSnapshotTimestamp: entity.LastSnapshotTimestamp,
            scoringConfig: scoringConfig,
            gameState: gameState,
            notes: entity.Notes,
            agentMode: (AgentSessionMode)entity.AgentMode,
            chatSessionId: entity.ChatSessionId,
            turnAdvancePolicy: (TurnAdvancePolicy)entity.TurnAdvancePolicy,
            rowVersion: entity.RowVersion,
            players: players,
            teams: teams,
            turnOrder: turnOrder,
            roundScores: roundScores,
            turnRecords: turnRecords,
            disputes: disputes,
            setupChecklist: setupChecklist);
    }
}
```

> If `LiveSessionPlayer` / `LiveSessionTeam` / `RoundScore` / `TurnRecord` constructor signatures differ from above, open those files to align. Same for `SessionPlayerEntity` / `SessionTeamEntity` / `LiveRoundScoreEntity` / `LiveTurnRecordEntity` property names — adjust the mapper to whatever the entity already exposes.

- [ ] **Step 4: Run the round-trip tests (expect: PASS)**

```pwsh
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveGameSessionMapper"
```

If any test fails on field-name mismatch, fix the mapper to match the actual property names — DO NOT mutate Domain or Entity to satisfy the test.

- [ ] **Step 5: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/Mappers/ `
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Infrastructure/Mappers/
git commit -m "feat(live-session): #2097 LiveGameSessionMapper Domain<->Entity round-trip"
```

### Task 1.3: Replace `LiveSessionRepository` implementation

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/LiveSessionRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/ILiveSessionRepository.cs` (XML doc only)

- [ ] **Step 1: Rewrite `LiveSessionRepository.cs` completely**

Replace the entire file with:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed repository for live game sessions.
/// Issue #2097 / ADR-060: Replaced ConcurrentDictionary in-memory implementation
/// with persistent storage on the live_game_sessions table tree.
/// Live sessions now survive container restarts and are multi-instance ready.
/// </summary>
internal sealed class LiveSessionRepository : RepositoryBase, ILiveSessionRepository
{
    private readonly ILogger<LiveSessionRepository> _logger;

    public LiveSessionRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<LiveSessionRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LiveGameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.LiveGameSessions
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? LiveGameSessionMapper.ToDomain(entity) : null;
    }

    public async Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        var normalized = sessionCode?.ToUpperInvariant();
        var entity = await DbContext.LiveGameSessions
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .FirstOrDefaultAsync(e => e.SessionCode == normalized, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? LiveGameSessionMapper.ToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<LiveGameSession>> GetActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)LiveSessionStatus.Created,
            (int)LiveSessionStatus.Setup,
            (int)LiveSessionStatus.InProgress,
            (int)LiveSessionStatus.Paused
        };

        var entities = await DbContext.LiveGameSessions
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Where(e => e.CreatedByUserId == userId && activeStatuses.Contains(e.Status))
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(LiveGameSessionMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<LiveGameSession>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)LiveSessionStatus.Created,
            (int)LiveSessionStatus.Setup,
            (int)LiveSessionStatus.InProgress,
            (int)LiveSessionStatus.Paused
        };

        var entities = await DbContext.LiveGameSessions
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Where(e => activeStatuses.Contains(e.Status))
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(LiveGameSessionMapper.ToDomain).ToList();
    }

    public async Task AddAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);

        var entity = LiveGameSessionMapper.ToEntity(session);
        await DbContext.LiveGameSessions.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);

        var entity = LiveGameSessionMapper.ToEntity(session);
        DbContext.LiveGameSessions.Update(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.LiveGameSessions
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

> Note about `AsNoTracking`: GET methods do NOT use it because the mapper produces a Domain aggregate that the handler may mutate then save. With EF-tracked entities, the second call (Update) would conflict. The current shape mirrors `GameNightEventRepository.FindByLinkedSessionIdAsync` (which also drops `AsNoTracking` for the same reason). The performance cost is acceptable for our workload (≤ a few hundred active sessions).

- [ ] **Step 2: Update interface XML doc**

In `ILiveSessionRepository.cs`, replace the `<summary>` block at line 5-9:

```csharp
/// <summary>
/// Repository for the LiveGameSession aggregate, EF Core-backed.
/// Issue #2097 / ADR-060: Replaced in-memory ConcurrentDictionary with persistent
/// storage. Live sessions survive container restarts and scale multi-instance.
/// </summary>
```

- [ ] **Step 3: Confirm `DbContext.LiveGameSessions` exists**

```pwsh
grep -n "DbSet<LiveGameSessionEntity>" apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
```

If no match — the DbSet has to be added to `MeepleAiDbContext` (likely as `public DbSet<LiveGameSessionEntity> LiveGameSessions => Set<LiveGameSessionEntity>();`). Add it next to the other GameManagement DbSets.

- [ ] **Step 4: Build (expect: PASS)**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: `Build succeeded`

Compile errors will surface mismatches with the child-entity property names. Fix them in the mapper, not the repository.

- [ ] **Step 5: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/LiveSessionRepository.cs `
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/ILiveSessionRepository.cs `
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git commit -m "feat(live-session): #2097 EF-backed LiveSessionRepository implementation"
```

### Task 1.4: Flip DI scope `AddSingleton` → `AddScoped`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs:37`

- [ ] **Step 1: Change registration**

Replace line 37:

```diff
-        services.AddSingleton<ILiveSessionRepository, LiveSessionRepository>(); // Issue #4749: Live session in-memory store
+        services.AddScoped<ILiveSessionRepository, LiveSessionRepository>(); // Issue #2097 / ADR-060: EF-backed persistence
```

- [ ] **Step 2: Verify `SessionAutoSaveBackgroundService` already creates a scope**

Check line 67-69 of `apps/api/src/Api/Infrastructure/BackgroundServices/SessionAutoSaveBackgroundService.cs`:

```pwsh
grep -n "CreateScope\|GetRequiredService<ILiveSessionRepository>" apps/api/src/Api/Infrastructure/BackgroundServices/SessionAutoSaveBackgroundService.cs
```

Expected: shows `CreateScope` immediately before `GetRequiredService<ILiveSessionRepository>`. If not, the background service will throw `InvalidOperationException` ("Cannot resolve scoped service from singleton") at runtime — fix it now using the standard pattern:

```csharp
using var scope = _scopeFactory.CreateScope();
var sessionRepo = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
```

- [ ] **Step 3: Build (expect: PASS)**

Run: `dotnet build apps/api/src/Api/Api.csproj`

- [ ] **Step 4: Run a smoke unit test on the simplest command handler**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CreateLiveSessionCommandHandler"
```

Most likely fails because the handler test setup mocks the repository as singleton. We'll fix handler tests in Phase 2.

- [ ] **Step 5: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs `
        apps/api/src/Api/Infrastructure/BackgroundServices/SessionAutoSaveBackgroundService.cs
git commit -m "feat(live-session): #2097 flip DI to Scoped and verify background service uses scope"
```

---

# Phase 2 — `IUnitOfWork.SaveChangesAsync` in Command handlers

The legacy `LiveSessionRepository` persisted immediately on `AddAsync` / `UpdateAsync`. The new EF-backed implementation only tracks changes — the handler must call `SaveChangesAsync` explicitly. We update each Command handler to inject `IUnitOfWork` and call it after the repository write.

This phase is mechanical but touches ~20 handlers. Group them per file and commit per group to keep diffs reviewable.

### Task 2.1: Wire `IUnitOfWork` into the 6 lifecycle + Create handlers

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/CreateLiveSessionCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/LifecycleCommandHandlers.cs` (5 handlers: Start, Pause, Resume, Complete, Save)

- [ ] **Step 1: Pattern — apply to each of the 6 handlers**

For each handler class, do:

1. Add `IUnitOfWork _unitOfWork` field.
2. Add `IUnitOfWork unitOfWork` ctor parameter (after existing parameters).
3. Add null-check + assignment.
4. After the existing `AddAsync` / `UpdateAsync` call, append:

```csharp
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

5. Add `using Api.SharedKernel.Infrastructure.Persistence;` (where `IUnitOfWork` lives — verify path).

Example diff for `StartLiveSessionCommandHandler` (LifecycleCommandHandlers.cs:12-36):

```diff
 internal class StartLiveSessionCommandHandler : ICommandHandler<StartLiveSessionCommand>
 {
     private readonly ILiveSessionRepository _sessionRepository;
     private readonly TimeProvider _timeProvider;
+    private readonly IUnitOfWork _unitOfWork;

     public StartLiveSessionCommandHandler(
         ILiveSessionRepository sessionRepository,
-        TimeProvider timeProvider)
+        TimeProvider timeProvider,
+        IUnitOfWork unitOfWork)
     {
         _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
         _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
+        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
     }

     public async Task Handle(StartLiveSessionCommand command, CancellationToken cancellationToken)
     {
         ArgumentNullException.ThrowIfNull(command);

         var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
             .ConfigureAwait(false)
             ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

         session.Start(_timeProvider);
         await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
+        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
     }
 }
```

Apply identical pattern to: `PauseLiveSessionCommandHandler`, `ResumeLiveSessionCommandHandler`, `CompleteLiveSessionCommandHandler`, `SaveLiveSessionCommandHandler`, `CreateLiveSessionCommandHandler`.

- [ ] **Step 2: Build (expect: PASS)**

```pwsh
dotnet build apps/api/src/Api/Api.csproj
```

- [ ] **Step 3: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/CreateLiveSessionCommandHandler.cs `
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/LifecycleCommandHandlers.cs
git commit -m "feat(live-session): #2097 wire IUnitOfWork in 6 lifecycle + Create handlers"
```

### Task 2.2: Wire `IUnitOfWork` into Player + Team + TurnOrder + Score handlers

**Files (apply same pattern as Task 2.1 to each):**
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/PlayerCommandHandlers.cs` (AddPlayer, RemovePlayer)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/TeamCommandHandlers.cs` (CreateTeam, AssignPlayerToTeam)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/TurnOrder/TurnOrderCommandHandlers.cs` (UpdatePlayerOrder)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/ScoreAndTurnCommandHandlers.cs` (RecordScore, EditScore, AdvanceTurn)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/PhaseAndSnapshotCommandHandlers.cs` (AdvancePhase, ConfigurePhases, TriggerSnapshot)

- [ ] **Step 1: For each handler, apply the diff from Task 2.1 Step 1**

The pattern is mechanical: add `IUnitOfWork`, call `SaveChangesAsync` after the repository call.

- [ ] **Step 2: Build (expect: PASS)**

```pwsh
dotnet build apps/api/src/Api/Api.csproj
```

- [ ] **Step 3: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/
git commit -m "feat(live-session): #2097 wire IUnitOfWork in Player/Team/Score/Phase handlers"
```

### Task 2.3: Wire `IUnitOfWork` into Notes + Dispute + Confirm + SaveComplete handlers

**Files:**
- `UpdateLiveSessionNotesCommandHandler` (in Phase/Score handlers file or its own)
- `ConfirmScoreProposalCommandHandler.cs`
- `SaveCompleteSessionStateCommandHandler.cs`
- Dispute handlers in `GameNight/OpenStructuredDisputeCommandHandler.cs` + 4 vote handlers

- [ ] **Step 1: Apply same pattern**

The dispute handlers may already inject `IUnitOfWork` if they touch other aggregates — in that case, only confirm `SaveChangesAsync` is called once at the end.

- [ ] **Step 2: Run all unit tests under the GameManagement BC**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessions"
```

Many unit tests will fail because their mocks don't inject `IUnitOfWork`. Fix them inline: add `_unitOfWork = Substitute.For<IUnitOfWork>();` to each test fixture's ctor and pass it to the handler.

- [ ] **Step 3: Commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/ `
        apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Application/
git commit -m "feat(live-session): #2097 wire IUnitOfWork in remaining handlers + fix unit tests"
```

### Task 2.4: Verify domain events dispatch after SaveChangesAsync success (not before)

The `RepositoryBase.CollectDomainEvents` pattern enqueues events on the `IDomainEventCollector`; the actual dispatch happens via the EF Core `SavingChanges` interceptor (`DomainEventDispatchInterceptor`) or an MediatR pipeline behavior. We verify the order: `Update` → `SaveChanges` → events dispatched.

- [ ] **Step 1: Find the dispatch site**

```pwsh
grep -rn "IDomainEventDispatcher\|DomainEventDispatchInterceptor\|DispatchAsync" apps/api/src/Api/SharedKernel apps/api/src/Api/Infrastructure | head -10
```

- [ ] **Step 2: Open the dispatcher and confirm "after SaveChanges" semantics**

Read the file shown above. Verify:
- Events are dispatched AFTER `SaveChangesAsync` returns successfully.
- If `SaveChangesAsync` throws (e.g. `DbUpdateConcurrencyException`), events MUST NOT dispatch.

If the dispatch happens in a pre-save interceptor (`SavingChangesAsync`), file an issue and address in a follow-up — but for Phase 2 acceptance, the existing GameNightEvent flow already works correctly (validated by `GameNightEventRepositoryTests`). So if pattern is the same, we're good.

- [ ] **Step 3: Smoke test — assert events dispatch on success**

Add a single integration test (we'll formalise in Phase 3) that:
1. Creates a live session via `CreateLiveSessionCommand`.
2. Asserts a `LiveSessionCreatedEvent` was dispatched.
3. Asserts the row exists in `live_game_sessions`.

This test goes in `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs` — see Phase 3.

- [ ] **Step 4: Commit verification audit (no code change usually)**

If nothing to commit, skip — but document findings in the PR description.

---

# Phase 3 — Integration tests (5 acceptance criteria)

We write 5 Testcontainers-backed tests proving the ADR acceptance criteria. Follow the pattern in `apps/api/tests/Api.Tests/Integration/GameManagement/GameNightInvitationEndpointsTests.cs`.

**File:** `apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs` (new)

### Task 3.1: Skeleton test class + AC-1 (create + DB persist)

- [ ] **Step 1: Create the test class with AC-1 only**

```csharp
using System;
using System.Threading.Tasks;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "GameManagement")]
[Collection("Integration")] // Reuse existing IntegrationCollectionFixture for Testcontainers Postgres
public class LiveSessionRepositoryIntegrationTests
{
    private readonly IntegrationCollectionFixture _fx;

    public LiveSessionRepositoryIntegrationTests(IntegrationCollectionFixture fx)
    {
        _fx = fx;
    }

    [Fact(DisplayName = "AC-1: Create live session persists row in live_game_sessions")]
    public async Task AC1_Create_PersistsRow()
    {
        await using var scope = _fx.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = await _fx.SeedUserAsync(scope);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Mage Knight",
            GameId: null,
            Visibility: PlayRecordVisibility.Private,
            GroupId: null,
            ScoringDimensions: null,
            DimensionUnits: null,
            AgentMode: AgentSessionMode.None));

        sessionId.Should().NotBeEmpty();

        var row = await db.LiveGameSessions.AsNoTracking().FirstAsync(e => e.Id == sessionId);
        row.GameName.Should().Be("Mage Knight");
        row.CreatedByUserId.Should().Be(userId);
        row.Status.Should().Be((int)LiveSessionStatus.Created);
    }
}
```

> If `IntegrationCollectionFixture` does not exist, look at sibling integration tests — they typically use a `WebApplicationFactory`-based fixture. Adapt the constructor and `Services` access accordingly. Don't invent infrastructure; reuse what's there.

- [ ] **Step 2: Run AC-1**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionRepositoryIntegrationTests.AC1"
```

Expected: PASS. If it fails on "table doesn't exist", run `dotnet ef database update` against the test's Testcontainer config (usually the fixture does this in `InitializeAsync`).

- [ ] **Step 3: Commit**

```pwsh
git add apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs
git commit -m "test(live-session): #2097 AC-1 create persists row"
```

### Task 3.2: AC-2 restart-safe via Testcontainers

Restart the Postgres container mid-session, verify state survives.

- [ ] **Step 1: Add the test**

Append to `LiveSessionRepositoryIntegrationTests.cs`:

```csharp
    [Fact(DisplayName = "AC-2: Session survives Postgres container restart")]
    public async Task AC2_RestartSafe_StateSurvives()
    {
        Guid sessionId;
        Guid userId;
        await using (var scope = _fx.Services.CreateAsyncScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            userId = await _fx.SeedUserAsync(scope);
            sessionId = await mediator.Send(new CreateLiveSessionCommand(
                userId, "Test Game", null, PlayRecordVisibility.Private,
                null, null, null, AgentSessionMode.None));

            // Domain rule: Start requires at least one active player → add first
            await mediator.Send(new AddPlayerToLiveSessionCommand(
                sessionId, "Aaron", PlayerColor.Red, userId, null, null));
            await mediator.Send(new StartLiveSessionCommand(sessionId));
        }

        // Restart Postgres container (existing fixture helper, or call docker stop/start)
        await _fx.RestartPostgresAsync();

        // Re-resolve scope after restart and verify
        await using var verifyScope = _fx.Services.CreateAsyncScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var session = await repo.GetByIdAsync(sessionId);

        session.Should().NotBeNull();
        session!.Status.Should().Be(LiveSessionStatus.InProgress);
        session.Players.Should().ContainSingle(p => p.DisplayName == "Aaron");
    }
```

> If the fixture does not have `RestartPostgresAsync`, add it: call `await _postgresContainer.StopAsync(); await _postgresContainer.StartAsync();` — the connection pool will reconnect automatically.

- [ ] **Step 2: Run AC-2**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionRepositoryIntegrationTests.AC2"
```

Expected: PASS.

- [ ] **Step 3: Commit**

```pwsh
git add apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs
git commit -m "test(live-session): #2097 AC-2 restart-safe"
```

### Task 3.3: AC-3 multi-instance via two `WebApplicationFactory` instances

- [ ] **Step 1: Add the test**

```csharp
    [Fact(DisplayName = "AC-3: Multi-instance — session created on factory A is readable on factory B")]
    public async Task AC3_MultiInstance_StateShared()
    {
        // Both factories point to the same Postgres container (via the shared fixture)
        await using var factoryA = _fx.CreateScope();
        await using var factoryB = _fx.CreateScope();

        var mediatorA = factoryA.ServiceProvider.GetRequiredService<IMediator>();
        var repoB = factoryB.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var userId = await _fx.SeedUserAsync(factoryA);

        var sessionId = await mediatorA.Send(new CreateLiveSessionCommand(
            userId, "Multi-instance", null, PlayRecordVisibility.Private,
            null, null, null, AgentSessionMode.None));

        var fromB = await repoB.GetByIdAsync(sessionId);
        fromB.Should().NotBeNull();
        fromB!.GameName.Should().Be("Multi-instance");
    }
```

- [ ] **Step 2: Run AC-3**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionRepositoryIntegrationTests.AC3"
```

- [ ] **Step 3: Commit**

### Task 3.4: AC-4 concurrent updates → 409 via RowVersion

- [ ] **Step 1: Add the test**

```csharp
    [Fact(DisplayName = "AC-4: Concurrent updates → DbUpdateConcurrencyException (HTTP 409)")]
    public async Task AC4_ConcurrentUpdates_ThrowsConcurrencyException()
    {
        Guid sessionId;
        Guid userId;
        await using (var setupScope = _fx.Services.CreateAsyncScope())
        {
            userId = await _fx.SeedUserAsync(setupScope);
            var mediator = setupScope.ServiceProvider.GetRequiredService<IMediator>();
            sessionId = await mediator.Send(new CreateLiveSessionCommand(
                userId, "Concurrent", null, PlayRecordVisibility.Private,
                null, null, null, AgentSessionMode.None));
            await mediator.Send(new AddPlayerToLiveSessionCommand(
                sessionId, "P", PlayerColor.Red, userId, null, null));
        }

        // Two parallel scopes load the same session — both will read the same RowVersion.
        await using var scopeA = _fx.Services.CreateAsyncScope();
        await using var scopeB = _fx.Services.CreateAsyncScope();

        var repoA = scopeA.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var uowA = scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var uowB = scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var sessionA = await repoA.GetByIdAsync(sessionId);
        var sessionB = await repoB.GetByIdAsync(sessionId);

        sessionA!.UpdateNotes("From A", TimeProvider.System);
        sessionB!.UpdateNotes("From B", TimeProvider.System);

        await repoA.UpdateAsync(sessionA);
        await uowA.SaveChangesAsync();

        await repoB.UpdateAsync(sessionB);
        Func<Task> act = async () => await uowB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
```

- [ ] **Step 2: Run AC-4**

Expected: PASS — `DbUpdateConcurrencyException` is thrown on the second save. This proves optimistic concurrency works.

> If this test fails because both saves succeed, EF is NOT treating `RowVersion` as a concurrency token. Investigate: `IsRowVersion()` may not propagate to the `bytea` column on Postgres without an explicit `IsConcurrencyToken()` declaration. Add `.IsConcurrencyToken()` to the EntityConfiguration if needed.

- [ ] **Step 3: Commit**

### Task 3.5: AC-5 multi-update + restart persistence

- [ ] **Step 1: Add the test**

```csharp
    [Fact(DisplayName = "AC-5: 100 score updates + restart → all 100 RoundScores persist")]
    public async Task AC5_MultiUpdate_RestartSafe()
    {
        Guid sessionId;
        Guid playerId;
        Guid userId;
        await using (var scope = _fx.Services.CreateAsyncScope())
        {
            userId = await _fx.SeedUserAsync(scope);
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            sessionId = await mediator.Send(new CreateLiveSessionCommand(
                userId, "100-score", null, PlayRecordVisibility.Private,
                null, new() { "points" }, null, AgentSessionMode.None));
            playerId = await mediator.Send(new AddPlayerToLiveSessionCommand(
                sessionId, "Aaron", PlayerColor.Red, userId, null, null));
            await mediator.Send(new StartLiveSessionCommand(sessionId));

            for (int round = 1; round <= 100; round++)
            {
                await mediator.Send(new RecordLiveSessionScoreCommand(
                    sessionId, playerId, round, "points", round * 10, null));
            }
        }

        await _fx.RestartPostgresAsync();

        await using var verifyScope = _fx.Services.CreateAsyncScope();
        var repo = verifyScope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var session = await repo.GetByIdAsync(sessionId);

        session.Should().NotBeNull();
        session!.RoundScores.Should().HaveCount(100);
        session.RoundScores.Sum(s => s.Value).Should().Be((1 + 100) * 100 / 2 * 10); // 50500
    }
```

- [ ] **Step 2: Run AC-5**

Expected: PASS. Wall-clock irrelevant — Testcontainers Postgres is local + fast.

- [ ] **Step 3: Run the full integration suite for this file**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSessionRepositoryIntegrationTests"
```

Expected: all 5 tests PASS.

- [ ] **Step 4: Commit**

```pwsh
git add apps/api/tests/Api.Tests/Integration/GameManagement/LiveSessionRepositoryIntegrationTests.cs
git commit -m "test(live-session): #2097 AC-3/4/5 multi-instance + concurrency + restart"
```

---

# Phase 4 — Observability (P2)

### Task 4.1: Prometheus metrics

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Observability/LiveSessionMetrics.cs`

- [ ] **Step 1: Create the metrics wrapper**

```csharp
using Prometheus;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Observability;

/// <summary>
/// Prometheus metrics for live session persistence (Issue #2097 / ADR-060 Phase 4).
/// </summary>
public sealed class LiveSessionMetrics
{
    private static readonly Gauge ActiveSessions = Metrics.CreateGauge(
        "live_sessions_active_gauge",
        "Number of live game sessions currently in InProgress/Paused/Setup state");

    private static readonly Histogram SessionDuration = Metrics.CreateHistogram(
        "live_session_duration_histogram",
        "Live session duration in seconds (from start to complete)",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(60, 2, 10) // 1min, 2min, …, ~17h
        });

    private static readonly Counter Writes = Metrics.CreateCounter(
        "live_session_writes_total",
        "Number of live session write operations",
        new CounterConfiguration { LabelNames = new[] { "op" } });

    public void SetActiveSessionCount(int count) => ActiveSessions.Set(count);
    public void ObserveSessionDuration(TimeSpan duration) => SessionDuration.Observe(duration.TotalSeconds);
    public void IncrementWrite(string op) => Writes.WithLabels(op).Inc();
}
```

- [ ] **Step 2: Register in DI**

In `GameManagementServiceExtensions.cs`:

```csharp
services.AddSingleton<LiveSessionMetrics>();
```

- [ ] **Step 3: Increment counters in the repository**

Modify `LiveSessionRepository`:

```csharp
public LiveSessionRepository(
    MeepleAiDbContext dbContext,
    IDomainEventCollector eventCollector,
    ILogger<LiveSessionRepository> logger,
    LiveSessionMetrics metrics)
    : base(dbContext, eventCollector)
{
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
}

public async Task AddAsync(...) {
    // ... existing code
    _metrics.IncrementWrite("create");
}

public Task UpdateAsync(...) {
    // ... existing code
    _metrics.IncrementWrite("update");
    return Task.CompletedTask;
}
```

- [ ] **Step 4: Build + commit**

```pwsh
dotnet build apps/api/src/Api/Api.csproj
git add apps/api/src/Api/BoundedContexts/GameManagement/
git commit -m "feat(live-session): #2097 Prometheus metrics for active count + writes + duration"
```

### Task 4.2: Health check

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/HealthChecks/LiveSessionPersistenceHealthCheck.cs`

- [ ] **Step 1: Implement health check**

```csharp
using System.Diagnostics;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.BoundedContexts.GameManagement.Infrastructure.HealthChecks;

public sealed class LiveSessionPersistenceHealthCheck : IHealthCheck
{
    private readonly MeepleAiDbContext _dbContext;

    public LiveSessionPersistenceHealthCheck(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _ = await _dbContext.LiveGameSessions
                .AsNoTracking()
                .Take(1)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();

            var data = new Dictionary<string, object> { ["latency_ms"] = sw.ElapsedMilliseconds };
            return sw.ElapsedMilliseconds > 1000
                ? HealthCheckResult.Degraded("Live session persistence latency > 1s", data: data)
                : HealthCheckResult.Healthy("Live session persistence OK", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Live session persistence query failed", ex);
        }
    }
}
```

- [ ] **Step 2: Register in DI / health check pipeline**

Search for the existing health check registration:

```pwsh
grep -rn "AddHealthChecks" apps/api/src/Api/ | head -5
```

Add `.AddCheck<LiveSessionPersistenceHealthCheck>("live_sessions_persistence")` next to the existing checks.

- [ ] **Step 3: Build + commit**

```pwsh
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/HealthChecks/ `
        apps/api/src/Api/Program.cs # or wherever AddHealthChecks lives
git commit -m "feat(live-session): #2097 health check live_sessions_persistence"
```

---

# Phase 5 — Migration + ADR status flip (P3, 1h)

### Task 5.1: Optional admin warning endpoint

We do not migrate in-memory sessions (they're already lost on prior restarts). Skip the data migration step. Optional: expose an admin endpoint that lists active sessions, useful before deploys.

- [ ] **Step 1: Verify existing `/api/v1/live-sessions/active` is admin-accessible**

```pwsh
grep -n "MapGet(\"/live-sessions/active\"" apps/api/src/Api/Routing/LiveSessionEndpoints.cs
```

This is already a per-user endpoint, not admin-wide. Decide:
- (a) Accept as-is — admins call multiple users.
- (b) Add `/api/v1/admin/live-sessions/active` — out of scope for this PR; create follow-up issue.

Choose (a) for the epic. Document in PR description.

### Task 5.2: Flip ADR-060 status to Accepted

**Files:**
- Modify: `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md:3`

- [ ] **Step 1: Update ADR header**

```diff
-**Status**: Proposed
+**Status**: Accepted
 **Date**: 2026-06-09
+**Implemented**: 2026-06-13 (PR #ZZZ)
```

(Replace `#ZZZ` with the PR number after creating the PR.)

- [ ] **Step 2: Update CLAUDE.md Known Pitfalls table**

Append a row in `CLAUDE.md` § "Known Pitfalls (Issues)":

```markdown
| #2097 | Live sessions persist via EF Core. Handlers must call `IUnitOfWork.SaveChangesAsync` after `AddAsync`/`UpdateAsync`. DomainEvents dispatch post-SaveChanges only. |
```

- [ ] **Step 3: Commit**

```pwsh
git add docs/for-claude/architecture/adr/adr-060-live-session-persistence.md CLAUDE.md
git commit -m "docs(adr-060): #2097 flip status Proposed -> Accepted post-implementation"
```

---

# Final validation gate

Before opening the PR:

- [ ] **Verify all integration tests green**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~LiveSession"
```

- [ ] **Verify no other tests regressed**

```pwsh
dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit&BoundedContext=GameManagement"
```

Use the established no-Docker filter; if you must run the full integration suite locally, expect Docker contention per memory `full-integration-suite-local-unreliable` — trust CI instead.

- [ ] **Manual smoke test**

```pwsh
cd infra; make dev-core
```

Open browser:
1. `http://localhost:3000/sessions/new` — create session.
2. Note session ID from URL.
3. `docker restart meepleai-api`.
4. Navigate back to `/sessions/<id>` — must return 200, not 404.

- [ ] **Open PR targeting `main-dev`**

```pwsh
gh pr create --base main-dev --title "feat(live-session): #2097 EF-backed persistence (ADR-060)" --body "$(cat <<'EOF'
## Summary

Implements ADR-060 Option B: persist `LiveGameSession` aggregate to EF Core (existing `live_game_sessions` schema). Live sessions now survive container restarts and are multi-instance ready.

## What changes

- **Phase 0**: 5 missing Entity columns added + EF migration (`phase_names_json`, `current_phase_index`, `snapshot_trigger_config_json`, `last_snapshot_timestamp`, `turn_advance_policy`).
- **Phase 1**: `LiveGameSessionMapper` (Domain ↔ Entity, 7 collections, 7 jsonb columns); `LiveGameSession.Reconstitute(...)` factory; `LiveSessionRepository` extends `RepositoryBase` (no reflection); DI Singleton → Scoped.
- **Phase 2**: `IUnitOfWork.SaveChangesAsync` wired into ~20 Command handlers; domain events dispatch post-SaveChanges via existing collector pipeline.
- **Phase 3**: 5 integration tests (AC-1 create, AC-2 restart-safe, AC-3 multi-instance, AC-4 RowVersion → 409, AC-5 100 updates + restart).
- **Phase 4**: Prometheus metrics + `live_sessions_persistence` health check.
- **Phase 5**: ADR-060 status `Proposed` → `Accepted`.

## Test plan

- [x] All unit tests pass (`Category=Unit`)
- [x] All integration tests pass (`FullyQualifiedName~LiveSessionRepositoryIntegrationTests`)
- [x] Manual smoke: create session → restart container → GET → 200
- [x] No regression on `GameNightEvent*` (pattern reference)

Closes #2097
Closes #2090

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Update issue #2097 with PR link and tick each AC checkbox**

```pwsh
gh issue comment 2097 --body "Implementation merged in PR #ZZZ. All 5 ACs verified green. ADR-060 status flipped to Accepted."
```

---

## Out of scope (do NOT do in this PR)

- ❌ Redis caching layer (ADR-060 Option C) — separate future ADR.
- ❌ SignalR multi-device push (Q2 ADR-060) — separate ADR.
- ❌ Session retention policy (Q1 ADR-060) — separate tracking issue.
- ❌ `TotalPausedDurationMs` mapping (Domain has no field). The mapper round-trips it transparently via EF tracking; do not surface it to Domain in this PR.
- ❌ Admin-wide `/api/v1/admin/live-sessions/active` endpoint — follow-up issue.

---

## Self-review checklist (run before opening PR)

- [ ] Every Domain field has a corresponding Entity column OR is explicitly out-of-scope (`TotalPausedDurationMs`).
- [ ] `LiveGameSession.Reconstitute(...)` parameter order matches mapper invocation.
- [ ] Mapper `ToEntity` writes EVERY non-RowVersion entity property at least once.
- [ ] Every Command handler that mutates `LiveGameSession` calls `_unitOfWork.SaveChangesAsync` exactly once at the end.
- [ ] `LiveSessionRepository` is `internal sealed` and extends `RepositoryBase`.
- [ ] DI registration is `AddScoped`, not `AddSingleton`.
- [ ] All 5 integration tests pass.
- [ ] ADR-060 status flipped to Accepted.
- [ ] CLAUDE.md pitfall row added.
- [ ] No `// TODO` or `// FIXME` in committed code (would trip the SonarAnalyzer S1135 rule per memory `sonar-s1135-todo-blocks-build` — use `// Follow-up:` if needed).

# Game Night AI Assistant — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the AI agent from a passive rule consultant into an active "game master assistant" that tracks scores via natural language, orchestrates complete game state saves, provides recap on resume, and offers a frictionless quick-start wizard for improvised game nights.

**Architecture:** Backend-first approach. Phase 1 creates a new `AgentScoreTrackingService` that bridges the existing `IStateParser` NLU to `LiveGameSession.RecordScore()`. Phase 2 adds a `SaveCompleteSessionStateCommand` saga that orchestrates snapshot + agent persist + recap generation. Phase 3 builds a frontend `GameNightWizard` that chains existing flows. All phases are independently deployable.

**Tech Stack:** .NET 9 (MediatR, EF Core, FluentValidation) | Next.js 16 (React 19, Tailwind 4, shadcn/ui, Zustand) | Vitest + xUnit

**Prerequisites:** These existing plans should be completed first (or in parallel):
- `2026-03-09-game-night-implementation-plan.md` — GameSessionContext orchestrator, session-aware RAG (#5578)
- `2026-03-10-game-night-journey-plan.md` — BGG discover tab, copyright disclaimer, scoreboard page

**Branch Strategy:** `main-dev` → `game-night-ai-assistant` (umbrella) → `feature/issue-XXXX-*` per task

**Single device model:** Host controls everything. No WebSocket/real-time sync needed.

---

## Dependency Graph

```
Phase 1: AI Score Tracking
  #A1 PlayerNameResolver ──┐
  #A2 ScoreIntentDetector ─┤──→ #A4 ParseAndRecordScore endpoint
  #A3 ScoreConfirmation ───┘           │
                                       ↓
                              #A5 Frontend ScoreAssistant panel
                              #A6 Tests (unit + integration)

Phase 2: Enhanced Save/Resume (independent of Phase 1)
  #B1 SaveCompleteState command ──→ #B2 ResumeContext query
                                         │
                                         ↓
                                   #B3 Frontend Enhanced Pause/Resume
                                   #B4 Tests

Phase 3: Game Night Wizard (independent of Phase 1 & 2)
  #C1 Frontend GameNightWizard ──→ #C2 Degraded Mode banner
                                   #C3 Tests
```

---

## Phase 1: AI Score Tracking

### What exists (DO NOT rebuild)

| Component | Path | What it does |
|---|---|---|
| `IStateParser` | `KnowledgeBase/Domain/Services/IStateParser.cs` | NLU: parses text → `StateExtractionResult` with `ScoreChange` type |
| `StateExtractionResult` | `KnowledgeBase/Domain/ValueObjects/StateExtractionResult.cs` | Result: `ChangeType`, `PlayerName`, `ExtractedState`, `Confidence` |
| `ParseLedgerMessageCommand` | `KnowledgeBase/Application/Commands/ParseLedgerMessageCommand.cs` | Existing bridge: message → `IStateParser` → `LedgerParseResultDto` |
| `RecordLiveSessionScoreCommand` | `GameManagement/Application/Commands/LiveSessions/RecordLiveSessionScoreCommand.cs` | Records score: `SessionId, PlayerId, Round, Dimension, Value, Unit` |
| `LiveGameSession.RecordScore()` | `GameManagement/Domain/Entities/LiveGameSession.cs:416` | Domain: validates, upserts score, recalculates ranks |
| `GameSessionContextDto` | `GameManagement/Application/DTOs/GameSessionContext/GameSessionContextDto.cs` | Session context with players, phases, game IDs |
| `LiveScoreSheet` | `apps/web/src/components/session/LiveScoreSheet.tsx` | Bottom sheet for manual score entry |
| `VoiceMicButton` | `apps/web/src/components/chat-unified/VoiceMicButton.tsx` | Speech-to-text input |

### Task A1: PlayerNameResolutionService

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/PlayerNameResolutionService.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/IPlayerNameResolutionService.cs`
- Create: `apps/api/tests/Api.Tests/Unit/BoundedContexts/GameManagement/Application/Services/PlayerNameResolutionServiceTests.cs`

**Purpose:** Maps fuzzy player names from NLU ("Marco", "marco", "M.") to actual `LiveSessionPlayer.Id` GUIDs in the session.

- [ ] **Step 1: Write failing tests**

```csharp
// apps/api/tests/Api.Tests/Unit/BoundedContexts/GameManagement/Application/Services/PlayerNameResolutionServiceTests.cs
using Api.BoundedContexts.GameManagement.Application.Services;

namespace Api.Tests.Unit.BoundedContexts.GameManagement.Application.Services;

public class PlayerNameResolutionServiceTests
{
    private readonly PlayerNameResolutionService _sut = new();

    private static readonly Dictionary<Guid, string> Players = new()
    {
        [Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")] = "Marco Rossi",
        [Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002")] = "Sara Bianchi",
        [Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003")] = "Giulia Verdi",
    };

    [Theory]
    [InlineData("Marco", "aaaaaaaa-0000-0000-0000-000000000001")]
    [InlineData("marco", "aaaaaaaa-0000-0000-0000-000000000001")]
    [InlineData("Marco Rossi", "aaaaaaaa-0000-0000-0000-000000000001")]
    [InlineData("Sara", "aaaaaaaa-0000-0000-0000-000000000002")]
    public void ResolvePlayer_ExactOrFirstName_ReturnsCorrectId(string input, string expectedId)
    {
        var result = _sut.ResolvePlayer(input, Players);
        Assert.True(result.IsResolved);
        Assert.Equal(Guid.Parse(expectedId), result.PlayerId);
    }

    [Fact]
    public void ResolvePlayer_AmbiguousName_ReturnsAmbiguous()
    {
        var playersWithDuplicate = new Dictionary<Guid, string>(Players)
        {
            [Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004")] = "Marco Neri"
        };

        var result = _sut.ResolvePlayer("Marco", playersWithDuplicate);
        Assert.False(result.IsResolved);
        Assert.True(result.IsAmbiguous);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void ResolvePlayer_UnknownName_ReturnsNotFound()
    {
        var result = _sut.ResolvePlayer("Roberto", Players);
        Assert.False(result.IsResolved);
        Assert.False(result.IsAmbiguous);
    }
}
```

- [ ] **Step 2: Run test — expect FAIL (class not found)**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~PlayerNameResolutionServiceTests" --no-build 2>&1 | head -20
```
Expected: Build error — `PlayerNameResolutionService` not found

- [ ] **Step 3: Implement PlayerNameResolutionService**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/IPlayerNameResolutionService.cs
namespace Api.BoundedContexts.GameManagement.Application.Services;

internal interface IPlayerNameResolutionService
{
    PlayerResolutionResult ResolvePlayer(string playerName, IReadOnlyDictionary<Guid, string> sessionPlayers);
}

internal sealed record PlayerResolutionResult
{
    public bool IsResolved { get; init; }
    public Guid? PlayerId { get; init; }
    public string? ResolvedName { get; init; }
    public bool IsAmbiguous { get; init; }
    public IReadOnlyList<(Guid Id, string Name)> Candidates { get; init; } = [];

    public static PlayerResolutionResult Resolved(Guid id, string name) =>
        new() { IsResolved = true, PlayerId = id, ResolvedName = name };

    public static PlayerResolutionResult Ambiguous(IReadOnlyList<(Guid, string)> candidates) =>
        new() { IsAmbiguous = true, Candidates = candidates };

    public static PlayerResolutionResult NotFound() => new();
}
```

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/PlayerNameResolutionService.cs
namespace Api.BoundedContexts.GameManagement.Application.Services;

internal sealed class PlayerNameResolutionService : IPlayerNameResolutionService
{
    public PlayerResolutionResult ResolvePlayer(
        string playerName, IReadOnlyDictionary<Guid, string> sessionPlayers)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return PlayerResolutionResult.NotFound();

        var normalized = playerName.Trim();

        // 1. Exact match (case-insensitive)
        var exact = sessionPlayers
            .Where(p => p.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exact.Count == 1)
            return PlayerResolutionResult.Resolved(exact[0].Key, exact[0].Value);

        // 2. First name match
        var firstNameMatches = sessionPlayers
            .Where(p => p.Value.Split(' ')[0].Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (firstNameMatches.Count == 1)
            return PlayerResolutionResult.Resolved(firstNameMatches[0].Key, firstNameMatches[0].Value);
        if (firstNameMatches.Count > 1)
            return PlayerResolutionResult.Ambiguous(
                firstNameMatches.Select(p => (p.Key, p.Value)).ToList());

        // 3. Contains match
        var contains = sessionPlayers
            .Where(p => p.Value.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (contains.Count == 1)
            return PlayerResolutionResult.Resolved(contains[0].Key, contains[0].Value);
        if (contains.Count > 1)
            return PlayerResolutionResult.Ambiguous(
                contains.Select(p => (p.Key, p.Value)).ToList());

        return PlayerResolutionResult.NotFound();
    }
}
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~PlayerNameResolutionServiceTests" --verbosity normal
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/PlayerNameResolutionService.cs \
       apps/api/src/Api/BoundedContexts/GameManagement/Application/Services/IPlayerNameResolutionService.cs \
       apps/api/tests/Api.Tests/Unit/BoundedContexts/GameManagement/Application/Services/PlayerNameResolutionServiceTests.cs
git commit -m "feat(game-mgmt): add PlayerNameResolutionService for fuzzy player name matching"
```

---

### Task A2: ParseAndRecordScoreCommand (Backend Bridge)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ParseAndRecordScoreCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/ParseAndRecordScoreCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ScoreParseResultDto.cs`
- Create: `apps/api/tests/Api.Tests/Unit/BoundedContexts/KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandlerTests.cs`

**Purpose:** Takes natural language ("Marco 5 punti agricoltura"), parses via `IStateParser`, resolves player, and optionally records score on `LiveGameSession`.

- [ ] **Step 1: Create DTOs**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ScoreParseResultDto.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

public sealed record ScoreParseResultDto
{
    public required string Status { get; init; }  // "parsed", "recorded", "ambiguous", "unrecognized"
    public string? PlayerName { get; init; }
    public Guid? PlayerId { get; init; }
    public string? Dimension { get; init; }
    public int? Value { get; init; }
    public int? Round { get; init; }
    public float Confidence { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string> AmbiguousCandidates { get; init; } = [];
}
```

- [ ] **Step 2: Create Command + Validator**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ParseAndRecordScoreCommand.cs
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

public sealed record ParseAndRecordScoreCommand : IRequest<ScoreParseResultDto>
{
    public required Guid SessionId { get; init; }
    public required string Message { get; init; }
    public bool AutoRecord { get; init; } = false;  // true = record if confidence > 0.8
}
```

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/ParseAndRecordScoreCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

internal sealed class ParseAndRecordScoreCommandValidator
    : AbstractValidator<ParseAndRecordScoreCommand>
{
    public ParseAndRecordScoreCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty().MaximumLength(500);
    }
}
```

- [ ] **Step 3: Write failing handler test**

```csharp
// apps/api/tests/Api.Tests/Unit/BoundedContexts/KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandlerTests.cs
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Api.Tests.Unit.BoundedContexts.KnowledgeBase.Application.Handlers;

public class ParseAndRecordScoreCommandHandlerTests
{
    private readonly IStateParser _stateParser = Substitute.For<IStateParser>();
    private readonly IPlayerNameResolutionService _playerResolver = Substitute.For<IPlayerNameResolutionService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<ParseAndRecordScoreCommandHandler> _logger =
        Substitute.For<ILogger<ParseAndRecordScoreCommandHandler>>();

    private ParseAndRecordScoreCommandHandler CreateSut() =>
        new(_stateParser, _playerResolver, _mediator, _logger);

    [Fact]
    public async Task Handle_ScoreChange_HighConfidence_AutoRecord_RecordsScore()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var command = new ParseAndRecordScoreCommand
        {
            SessionId = sessionId,
            Message = "Marco 5 punti agricoltura",
            AutoRecord = true,
        };

        _stateParser.ParseAsync("Marco 5 punti agricoltura", null, Arg.Any<CancellationToken>())
            .Returns(StateExtractionResult.Create(
                StateChangeType.ScoreChange, "Marco",
                new Dictionary<string, object> { ["score"] = 5, ["dimension"] = "agricoltura" },
                0.95f, "Marco 5 punti agricoltura"));

        // Mock session players lookup via mediator
        _mediator.Send(Arg.Any<GetSessionPlayersQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [playerId] = "Marco Rossi" });

        _playerResolver.ResolvePlayer("Marco", Arg.Any<IReadOnlyDictionary<Guid, string>>())
            .Returns(PlayerResolutionResult.Resolved(playerId, "Marco Rossi"));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("recorded", result.Status);
        Assert.Equal(playerId, result.PlayerId);
        Assert.Equal("agricoltura", result.Dimension);
        Assert.Equal(5, result.Value);

        await _mediator.Received(1).Send(
            Arg.Is<RecordLiveSessionScoreCommand>(c =>
                c.SessionId == sessionId &&
                c.PlayerId == playerId &&
                c.Dimension == "agricoltura" &&
                c.Value == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ScoreChange_LowConfidence_ReturnsParsedNotRecorded()
    {
        var command = new ParseAndRecordScoreCommand
        {
            SessionId = Guid.NewGuid(),
            Message = "forse Marco ha fatto dei punti",
            AutoRecord = true,
        };

        _stateParser.ParseAsync(command.Message, null, Arg.Any<CancellationToken>())
            .Returns(StateExtractionResult.Create(
                StateChangeType.ScoreChange, "Marco",
                new Dictionary<string, object> { ["score"] = 0 },
                0.4f, command.Message));

        var sut = CreateSut();
        var result = await sut.Handle(command, CancellationToken.None);

        Assert.Equal("parsed", result.Status);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public async Task Handle_NoScoreDetected_ReturnsUnrecognized()
    {
        var command = new ParseAndRecordScoreCommand
        {
            SessionId = Guid.NewGuid(),
            Message = "che bella giornata",
            AutoRecord = true,
        };

        _stateParser.ParseAsync(command.Message, null, Arg.Any<CancellationToken>())
            .Returns(StateExtractionResult.NoChange(command.Message));

        var sut = CreateSut();
        var result = await sut.Handle(command, CancellationToken.None);

        Assert.Equal("unrecognized", result.Status);
    }
}
```

- [ ] **Step 4: Run test — expect FAIL (handler not found)**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ParseAndRecordScoreCommandHandlerTests" --no-build 2>&1 | head -10
```

- [ ] **Step 5: Implement handler**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandler.cs
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

internal sealed class ParseAndRecordScoreCommandHandler
    : IRequestHandler<ParseAndRecordScoreCommand, ScoreParseResultDto>
{
    private const float AutoRecordThreshold = 0.8f;

    private readonly IStateParser _stateParser;
    private readonly IPlayerNameResolutionService _playerResolver;
    private readonly IMediator _mediator;
    private readonly ILogger<ParseAndRecordScoreCommandHandler> _logger;

    public ParseAndRecordScoreCommandHandler(
        IStateParser stateParser,
        IPlayerNameResolutionService playerResolver,
        IMediator mediator,
        ILogger<ParseAndRecordScoreCommandHandler> logger)
    {
        _stateParser = stateParser;
        _playerResolver = playerResolver;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ScoreParseResultDto> Handle(
        ParseAndRecordScoreCommand command, CancellationToken ct)
    {
        // 1. Parse natural language
        var extraction = await _stateParser.ParseAsync(command.Message, null, ct);

        if (!extraction.HasStateChanges || extraction.ChangeType != StateChangeType.ScoreChange)
        {
            return new ScoreParseResultDto
            {
                Status = "unrecognized",
                Confidence = extraction.Confidence,
                Message = "Nessun punteggio rilevato nel messaggio.",
            };
        }

        // 2. Extract score data
        var scoreValue = extraction.ExtractedState.TryGetValue("score", out var sv)
            ? Convert.ToInt32(sv) : 0;
        var dimension = extraction.ExtractedState.TryGetValue("dimension", out var dim)
            ? dim.ToString() ?? "default" : "default";

        // 3. Resolve player
        var players = await _mediator.Send(
            new GetSessionPlayersQuery(command.SessionId), ct);

        var resolution = _playerResolver.ResolvePlayer(
            extraction.PlayerName ?? "", players);

        if (resolution.IsAmbiguous)
        {
            return new ScoreParseResultDto
            {
                Status = "ambiguous",
                PlayerName = extraction.PlayerName,
                Dimension = dimension,
                Value = scoreValue,
                Confidence = extraction.Confidence,
                RequiresConfirmation = true,
                Message = $"Più giocatori corrispondono a \"{extraction.PlayerName}\". Quale intendi?",
                AmbiguousCandidates = resolution.Candidates.Select(c => c.Name).ToList(),
            };
        }

        if (!resolution.IsResolved)
        {
            return new ScoreParseResultDto
            {
                Status = "parsed",
                PlayerName = extraction.PlayerName,
                Dimension = dimension,
                Value = scoreValue,
                Confidence = extraction.Confidence,
                RequiresConfirmation = true,
                Message = $"Giocatore \"{extraction.PlayerName}\" non trovato nella sessione.",
            };
        }

        // 4. Auto-record or require confirmation
        var shouldAutoRecord = command.AutoRecord && extraction.Confidence >= AutoRecordThreshold;

        if (shouldAutoRecord)
        {
            var currentRound = await GetCurrentRound(command.SessionId, ct);
            await _mediator.Send(new RecordLiveSessionScoreCommand(
                command.SessionId,
                resolution.PlayerId!.Value,
                currentRound,
                dimension,
                scoreValue), ct);

            _logger.LogInformation(
                "Auto-recorded score: {Player} +{Value} {Dimension} (confidence: {Confidence:F2})",
                resolution.ResolvedName, scoreValue, dimension, extraction.Confidence);

            return new ScoreParseResultDto
            {
                Status = "recorded",
                PlayerName = resolution.ResolvedName,
                PlayerId = resolution.PlayerId,
                Dimension = dimension,
                Value = scoreValue,
                Round = currentRound,
                Confidence = extraction.Confidence,
                Message = $"{resolution.ResolvedName}: +{scoreValue} {dimension} registrato.",
            };
        }

        return new ScoreParseResultDto
        {
            Status = "parsed",
            PlayerName = resolution.ResolvedName,
            PlayerId = resolution.PlayerId,
            Dimension = dimension,
            Value = scoreValue,
            Confidence = extraction.Confidence,
            RequiresConfirmation = true,
            Message = $"Confermi: {resolution.ResolvedName} +{scoreValue} {dimension}?",
        };
    }

    private async Task<int> GetCurrentRound(Guid sessionId, CancellationToken ct)
    {
        try
        {
            var session = await _mediator.Send(
                new GetLiveSessionQuery(sessionId), ct);
            return session?.CurrentTurnIndex ?? 1;
        }
        catch
        {
            return 1;
        }
    }
}
```

**Note:** `GetSessionPlayersQuery` and `GetLiveSessionQuery` — if these don't exist as exact queries, create thin wrappers that query the `ILiveSessionRepository`. Check existing queries in `GameManagement/Application/Queries/LiveSessions/` first.

- [ ] **Step 6: Run tests — expect PASS**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ParseAndRecordScoreCommandHandlerTests" --verbosity normal
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ParseAndRecordScoreCommand.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandler.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/ParseAndRecordScoreCommandValidator.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/ScoreParseResultDto.cs \
       apps/api/tests/Api.Tests/Unit/BoundedContexts/KnowledgeBase/Application/Handlers/ParseAndRecordScoreCommandHandlerTests.cs
git commit -m "feat(kb): add ParseAndRecordScoreCommand — NLU bridge to LiveGameSession scoring"
```

---

### Task A3: ConfirmScoreCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ConfirmScoreCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ConfirmScoreCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/Unit/BoundedContexts/KnowledgeBase/Application/Handlers/ConfirmScoreCommandHandlerTests.cs`

**Purpose:** When `ParseAndRecordScore` returns `"parsed"` with `RequiresConfirmation = true`, the frontend shows a confirm button. This command takes the pre-parsed data and records it.

- [ ] **Step 1: Write failing test**

```csharp
// apps/api/tests/Api.Tests/Unit/BoundedContexts/KnowledgeBase/Application/Handlers/ConfirmScoreCommandHandlerTests.cs
namespace Api.Tests.Unit.BoundedContexts.KnowledgeBase.Application.Handlers;

public class ConfirmScoreCommandHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    [Fact]
    public async Task Handle_ValidConfirmation_RecordsScore()
    {
        var playerId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var command = new ConfirmScoreCommand
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Dimension = "agricoltura",
            Value = 5,
            Round = 3,
        };

        var handler = new ConfirmScoreCommandHandler(_mediator);
        await handler.Handle(command, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<RecordLiveSessionScoreCommand>(c =>
                c.SessionId == sessionId &&
                c.PlayerId == playerId &&
                c.Dimension == "agricoltura" &&
                c.Value == 5 &&
                c.Round == 3),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Implement command + handler**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ConfirmScoreCommand.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

public sealed record ConfirmScoreCommand : IRequest
{
    public required Guid SessionId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string Dimension { get; init; }
    public required int Value { get; init; }
    public required int Round { get; init; }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/ConfirmScoreCommandHandler.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

internal sealed class ConfirmScoreCommandHandler : IRequestHandler<ConfirmScoreCommand>
{
    private readonly IMediator _mediator;

    public ConfirmScoreCommandHandler(IMediator mediator) => _mediator = mediator;

    public async Task Handle(ConfirmScoreCommand command, CancellationToken ct)
    {
        await _mediator.Send(new RecordLiveSessionScoreCommand(
            command.SessionId, command.PlayerId, command.Round,
            command.Dimension, command.Value), ct);
    }
}
```

- [ ] **Step 3: Run tests — expect PASS**
- [ ] **Step 4: Commit**

```bash
git commit -m "feat(kb): add ConfirmScoreCommand for user-confirmed score recording"
```

---

### Task A4: Score Tracking HTTP Endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/LiveSessionEndpoints.cs` (or create new `ScoreTrackingEndpoints.cs`)

**Purpose:** Expose `ParseAndRecordScore` and `ConfirmScore` as REST endpoints.

- [ ] **Step 1: Add endpoints**

Add to the live session routing group:

```csharp
// POST /api/v1/live-sessions/{sessionId}/scores/parse
// Body: { "message": "Marco 5 punti agricoltura", "autoRecord": true }
// Returns: ScoreParseResultDto
group.MapPost("/{sessionId:guid}/scores/parse", HandleParseScore)
    .RequireSession()
    .RequireAuthorization()
    .WithName("ParseScore")
    .WithDescription("Parse natural language score input and optionally auto-record");

// POST /api/v1/live-sessions/{sessionId}/scores/confirm
// Body: { "playerId": "...", "dimension": "agricoltura", "value": 5, "round": 3 }
group.MapPost("/{sessionId:guid}/scores/confirm", HandleConfirmScore)
    .RequireSession()
    .RequireAuthorization()
    .WithName("ConfirmScore")
    .WithDescription("Confirm and record a parsed score");
```

```csharp
private static async Task<IResult> HandleParseScore(
    Guid sessionId, ParseScoreRequest request, IMediator mediator, CancellationToken ct)
{
    var result = await mediator.Send(new ParseAndRecordScoreCommand
    {
        SessionId = sessionId,
        Message = request.Message,
        AutoRecord = request.AutoRecord,
    }, ct);
    return Results.Ok(result);
}

private sealed record ParseScoreRequest(string Message, bool AutoRecord = true);

private static async Task<IResult> HandleConfirmScore(
    Guid sessionId, ConfirmScoreRequest request, IMediator mediator, CancellationToken ct)
{
    await mediator.Send(new ConfirmScoreCommand
    {
        SessionId = sessionId,
        PlayerId = request.PlayerId,
        Dimension = request.Dimension,
        Value = request.Value,
        Round = request.Round,
    }, ct);
    return Results.NoContent();
}

private sealed record ConfirmScoreRequest(Guid PlayerId, string Dimension, int Value, int Round);
```

- [ ] **Step 2: Register DI for PlayerNameResolutionService**

In `GameManagementServiceExtensions.cs`:
```csharp
services.AddScoped<IPlayerNameResolutionService, PlayerNameResolutionService>();
```

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(routing): add /scores/parse and /scores/confirm endpoints for AI score tracking"
```

---

### Task A5: Frontend — ScoreAssistant Panel

**Files:**
- Create: `apps/web/src/components/session/ScoreAssistant.tsx`
- Create: `apps/web/src/components/session/ScoreConfirmationCard.tsx`
- Create: `apps/web/src/components/session/__tests__/ScoreAssistant.test.tsx`
- Create: `apps/web/src/lib/api/clients/scoreTrackingClient.ts`
- Modify: `apps/web/src/lib/api/index.ts` (register client)
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx` (add panel)

**Purpose:** A voice/text input panel in the session view where users can say "Marco 5 punti" and see it parsed → confirmed → recorded.

- [ ] **Step 1: Create API client**

```typescript
// apps/web/src/lib/api/clients/scoreTrackingClient.ts
import { httpClient } from '../http-client';

export interface ScoreParseResult {
  status: 'parsed' | 'recorded' | 'ambiguous' | 'unrecognized';
  playerName?: string;
  playerId?: string;
  dimension?: string;
  value?: number;
  round?: number;
  confidence: number;
  requiresConfirmation: boolean;
  message?: string;
  ambiguousCandidates: string[];
}

export function createScoreTrackingClient() {
  return {
    async parseScore(sessionId: string, message: string, autoRecord = true) {
      return httpClient.post<ScoreParseResult>(
        `/api/v1/live-sessions/${sessionId}/scores/parse`,
        { message, autoRecord }
      );
    },

    async confirmScore(sessionId: string, data: {
      playerId: string; dimension: string; value: number; round: number;
    }) {
      return httpClient.post<void>(
        `/api/v1/live-sessions/${sessionId}/scores/confirm`,
        data
      );
    },
  };
}
```

- [ ] **Step 2: Register in API client factory**

In `apps/web/src/lib/api/index.ts`, add `scoreTracking: createScoreTrackingClient()` to the `createApiClient()` factory.

- [ ] **Step 3: Write failing test for ScoreAssistant**

```typescript
// apps/web/src/components/session/__tests__/ScoreAssistant.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockParseScore = vi.fn();
const mockConfirmScore = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    scoreTracking: {
      parseScore: (...args: unknown[]) => mockParseScore(...args),
      confirmScore: (...args: unknown[]) => mockConfirmScore(...args),
    },
  },
}));

import { ScoreAssistant } from '../ScoreAssistant';

describe('ScoreAssistant', () => {
  const sessionId = 'session-123';

  beforeEach(() => vi.clearAllMocks());

  it('renders input field and mic button', () => {
    render(<ScoreAssistant sessionId={sessionId} />);
    expect(screen.getByPlaceholderText(/segna un punteggio/i)).toBeInTheDocument();
  });

  it('parses score on submit and shows confirmation', async () => {
    const user = userEvent.setup();
    mockParseScore.mockResolvedValue({
      status: 'parsed',
      playerName: 'Marco Rossi',
      playerId: 'player-1',
      dimension: 'agricoltura',
      value: 5,
      round: 3,
      confidence: 0.75,
      requiresConfirmation: true,
      message: 'Confermi: Marco Rossi +5 agricoltura?',
      ambiguousCandidates: [],
    });

    render(<ScoreAssistant sessionId={sessionId} />);
    const input = screen.getByPlaceholderText(/segna un punteggio/i);
    await user.type(input, 'Marco 5 punti agricoltura');
    await user.keyboard('{Enter}');

    await waitFor(() => {
      expect(mockParseScore).toHaveBeenCalledWith(sessionId, 'Marco 5 punti agricoltura', true);
    });

    await waitFor(() => {
      expect(screen.getByText(/confermi/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    });
  });

  it('shows recorded status when auto-recorded', async () => {
    const user = userEvent.setup();
    mockParseScore.mockResolvedValue({
      status: 'recorded',
      playerName: 'Marco Rossi',
      dimension: 'agricoltura',
      value: 5,
      confidence: 0.95,
      requiresConfirmation: false,
      message: 'Marco Rossi: +5 agricoltura registrato.',
      ambiguousCandidates: [],
    });

    render(<ScoreAssistant sessionId={sessionId} />);
    await user.type(screen.getByPlaceholderText(/segna un punteggio/i), 'Marco 5 agricoltura');
    await user.keyboard('{Enter}');

    await waitFor(() => {
      expect(screen.getByText(/registrato/i)).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 4: Implement ScoreAssistant component**

```tsx
// apps/web/src/components/session/ScoreAssistant.tsx
'use client';

import { useCallback, useState } from 'react';
import { Check, Loader2, Mic, Send, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type { ScoreParseResult } from '@/lib/api/clients/scoreTrackingClient';

interface ScoreAssistantProps {
  sessionId: string;
  onScoreRecorded?: () => void; // trigger scoreboard refresh
}

export function ScoreAssistant({ sessionId, onScoreRecorded }: ScoreAssistantProps) {
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ScoreParseResult | null>(null);
  const [confirming, setConfirming] = useState(false);

  const handleSubmit = useCallback(async () => {
    if (!input.trim() || loading) return;
    setLoading(true);
    setResult(null);
    try {
      const parsed = await api.scoreTracking.parseScore(sessionId, input.trim(), true);
      setResult(parsed);
      if (parsed.status === 'recorded') {
        setInput('');
        onScoreRecorded?.();
        // Auto-dismiss after 3s
        setTimeout(() => setResult(null), 3000);
      }
    } catch {
      setResult({
        status: 'unrecognized',
        confidence: 0,
        requiresConfirmation: false,
        message: 'Errore di connessione. Riprova.',
        ambiguousCandidates: [],
      });
    } finally {
      setLoading(false);
    }
  }, [input, loading, sessionId, onScoreRecorded]);

  const handleConfirm = useCallback(async () => {
    if (!result?.playerId || !result.dimension || result.value == null || result.round == null)
      return;
    setConfirming(true);
    try {
      await api.scoreTracking.confirmScore(sessionId, {
        playerId: result.playerId,
        dimension: result.dimension,
        value: result.value,
        round: result.round,
      });
      setResult({ ...result, status: 'recorded', message: `${result.playerName}: +${result.value} ${result.dimension} registrato.` });
      setInput('');
      onScoreRecorded?.();
      setTimeout(() => setResult(null), 3000);
    } catch {
      setResult({ ...result, message: 'Errore nel salvataggio. Riprova.' });
    } finally {
      setConfirming(false);
    }
  }, [result, sessionId, onScoreRecorded]);

  const handleDismiss = useCallback(() => {
    setResult(null);
    setInput('');
  }, []);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') handleSubmit();
    },
    [handleSubmit]
  );

  return (
    <div className="space-y-2">
      {/* Input row */}
      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Input
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder='Segna un punteggio... (es. "Marco 5 agricoltura")'
            disabled={loading}
            className="pr-10"
          />
        </div>
        <Button
          size="icon"
          variant="ghost"
          onClick={handleSubmit}
          disabled={!input.trim() || loading}
          aria-label="Invia punteggio"
        >
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        </Button>
      </div>

      {/* Result card */}
      {result && (
        <div
          className={`rounded-lg border p-3 text-sm ${
            result.status === 'recorded'
              ? 'border-green-500/30 bg-green-500/10 text-green-300'
              : result.status === 'unrecognized'
                ? 'border-red-500/30 bg-red-500/10 text-red-300'
                : 'border-amber-500/30 bg-amber-500/10 text-amber-200'
          }`}
        >
          <div className="flex items-start justify-between gap-2">
            <p>{result.message}</p>
            <Button
              size="icon"
              variant="ghost"
              className="h-5 w-5 shrink-0"
              onClick={handleDismiss}
              aria-label="Chiudi"
            >
              <X className="h-3 w-3" />
            </Button>
          </div>

          {result.requiresConfirmation && result.status === 'parsed' && result.playerId && (
            <div className="mt-2 flex gap-2">
              <Button
                size="sm"
                onClick={handleConfirm}
                disabled={confirming}
                aria-label="Conferma punteggio"
              >
                {confirming ? <Loader2 className="h-3 w-3 animate-spin mr-1" /> : <Check className="h-3 w-3 mr-1" />}
                Conferma
              </Button>
              <Button size="sm" variant="ghost" onClick={handleDismiss}>
                Annulla
              </Button>
            </div>
          )}

          {result.status === 'ambiguous' && result.ambiguousCandidates.length > 0 && (
            <p className="mt-1 text-xs">
              Possibili: {result.ambiguousCandidates.join(', ')}
            </p>
          )}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Integrate into session page**

In `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx`, add `<ScoreAssistant>` below the scoreboard:

```tsx
import { ScoreAssistant } from '@/components/session/ScoreAssistant';

// Inside the component, after <Scoreboard />:
<div className="mt-4">
  <ScoreAssistant
    sessionId={params.id}
    onScoreRecorded={() => queryClient.invalidateQueries({ queryKey: ['session-scores', params.id] })}
  />
</div>
```

- [ ] **Step 6: Run tests — expect PASS**

```bash
cd apps/web && pnpm vitest run src/components/session/__tests__/ScoreAssistant.test.tsx --reporter=verbose
```

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(session): add ScoreAssistant panel with NLU score parsing and confirmation"
```

---

## Phase 2: Enhanced Save/Resume

### What exists (DO NOT rebuild)

| Component | Path |
|---|---|
| `PauseSessionDialog` | `components/session/PauseSessionDialog.tsx` — has photo upload + "Skip & Pause" |
| `ResumePhotoReview` | `components/session/ResumePhotoReview.tsx` — full-screen photo review |
| `SessionSnapshot` | `GameManagement/Domain/Entities/SessionSnapshot/` — delta-based snapshots |
| `PauseLiveSessionCommand` | `GameManagement/Application/Commands/LiveSessions/PauseLiveSessionCommand.cs` |
| `SaveLiveSessionCommand` | `GameManagement/Application/Commands/LiveSessions/SaveLiveSessionCommand.cs` |
| `CreateSnapshotCommand` | `GameManagement/Application/Commands/SessionSnapshot/SessionSnapshotCommands.cs` |
| `UpdateAgentSessionStateCommand` | `KnowledgeBase/Application/Commands/UpdateAgentSessionStateCommand.cs` |

### Task B1: SaveCompleteSessionStateCommand (Backend Saga)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/SaveCompleteSessionStateCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/SaveCompleteSessionStateCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/SessionSaveResultDto.cs`
- Create: `apps/api/tests/Api.Tests/Unit/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/SaveCompleteSessionStateCommandHandlerTests.cs`

**Purpose:** One command that orchestrates: pause session + create snapshot + persist agent state + generate text recap.

- [ ] **Step 1: Create DTOs**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/SessionSaveResultDto.cs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

public sealed record SessionSaveResultDto
{
    public required Guid SessionId { get; init; }
    public required int SnapshotIndex { get; init; }
    public required string Recap { get; init; }  // Human-readable recap text
    public required int PhotoCount { get; init; }
    public required DateTime SavedAt { get; init; }
}
```

- [ ] **Step 2: Write failing test**

```csharp
namespace Api.Tests.Unit.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

public class SaveCompleteSessionStateCommandHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILiveSessionRepository _sessionRepo = Substitute.For<ILiveSessionRepository>();
    private readonly ILogger<SaveCompleteSessionStateCommandHandler> _logger =
        Substitute.For<ILogger<SaveCompleteSessionStateCommandHandler>>();

    [Fact]
    public async Task Handle_ActiveSession_PausesAndCreatesSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId, LiveSessionStatus.InProgress);

        _sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        _mediator.Send(Arg.Any<CreateSnapshotCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SessionSnapshotDto { SnapshotIndex = 5 });

        var handler = new SaveCompleteSessionStateCommandHandler(
            _mediator, _sessionRepo, _logger);

        var result = await handler.Handle(
            new SaveCompleteSessionStateCommand(sessionId), CancellationToken.None);

        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(5, result.SnapshotIndex);
        Assert.NotEmpty(result.Recap);

        // Verify pause was called
        await _mediator.Received(1).Send(
            Arg.Is<PauseLiveSessionCommand>(c => c.SessionId == sessionId),
            Arg.Any<CancellationToken>());

        // Verify snapshot was created
        await _mediator.Received(1).Send(
            Arg.Is<CreateSnapshotCommand>(c => c.SessionId == sessionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyPausedSession_SkipsPauseCreatesSnapshot()
    {
        var sessionId = Guid.NewGuid();
        var session = CreateTestSession(sessionId, LiveSessionStatus.Paused);

        _sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        _mediator.Send(Arg.Any<CreateSnapshotCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SessionSnapshotDto { SnapshotIndex = 3 });

        var handler = new SaveCompleteSessionStateCommandHandler(
            _mediator, _sessionRepo, _logger);

        var result = await handler.Handle(
            new SaveCompleteSessionStateCommand(sessionId), CancellationToken.None);

        // Should NOT call pause (already paused)
        await _mediator.DidNotReceive().Send(
            Arg.Any<PauseLiveSessionCommand>(), Arg.Any<CancellationToken>());

        // Should still create snapshot
        await _mediator.Received(1).Send(
            Arg.Any<CreateSnapshotCommand>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 3: Implement command + handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/LiveSessions/SaveCompleteSessionStateCommand.cs
namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

public sealed record SaveCompleteSessionStateCommand(Guid SessionId) : IRequest<SessionSaveResultDto>;
```

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/LiveSessions/SaveCompleteSessionStateCommandHandler.cs
namespace Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

internal sealed class SaveCompleteSessionStateCommandHandler
    : IRequestHandler<SaveCompleteSessionStateCommand, SessionSaveResultDto>
{
    private readonly IMediator _mediator;
    private readonly ILiveSessionRepository _sessionRepo;
    private readonly ILogger<SaveCompleteSessionStateCommandHandler> _logger;

    public SaveCompleteSessionStateCommandHandler(
        IMediator mediator,
        ILiveSessionRepository sessionRepo,
        ILogger<SaveCompleteSessionStateCommandHandler> logger)
    {
        _mediator = mediator;
        _sessionRepo = sessionRepo;
        _logger = logger;
    }

    public async Task<SessionSaveResultDto> Handle(
        SaveCompleteSessionStateCommand command, CancellationToken ct)
    {
        var session = await _sessionRepo.GetByIdAsync(command.SessionId, ct)
            ?? throw new NotFoundException($"Session {command.SessionId} not found");

        // 1. Pause if active
        if (session.Status == LiveSessionStatus.InProgress)
        {
            await _mediator.Send(new PauseLiveSessionCommand(command.SessionId), ct);
        }

        // 2. Save session state
        await _mediator.Send(new SaveLiveSessionCommand(command.SessionId), ct);

        // 3. Create snapshot
        var snapshot = await _mediator.Send(
            new CreateSnapshotCommand(command.SessionId, SnapshotTrigger.ManualSave,
                $"Salvataggio completo — turno {session.CurrentTurnIndex}"), ct);

        // 4. Persist agent state (if agent session exists)
        if (session.ChatSessionId.HasValue)
        {
            try
            {
                await _mediator.Send(
                    new SaveAgentSessionStateCommand(session.ChatSessionId.Value), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist agent state for session {SessionId}", command.SessionId);
            }
        }

        // 5. Count photos for this snapshot
        var photoCount = await _mediator.Send(
            new GetSnapshotPhotoCountQuery(command.SessionId, snapshot.SnapshotIndex), ct);

        // 6. Generate recap text
        var recap = GenerateRecap(session, snapshot.SnapshotIndex, photoCount);

        return new SessionSaveResultDto
        {
            SessionId = command.SessionId,
            SnapshotIndex = snapshot.SnapshotIndex,
            Recap = recap,
            PhotoCount = photoCount,
            SavedAt = DateTime.UtcNow,
        };
    }

    private static string GenerateRecap(LiveGameSession session, int snapshotIndex, int photoCount)
    {
        var players = session.Players;
        var scores = session.RoundScores;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Partita salvata al turno {session.CurrentTurnIndex}.");

        if (players.Count > 0)
        {
            sb.AppendLine($"Giocatori: {string.Join(", ", players.Select(p => p.Name))}.");
        }

        // Top scores summary
        var playerTotals = scores
            .GroupBy(s => s.PlayerId)
            .Select(g => new { PlayerId = g.Key, Total = g.Sum(s => s.Value) })
            .OrderByDescending(x => x.Total)
            .ToList();

        if (playerTotals.Count > 0)
        {
            var topPlayer = players.FirstOrDefault(p => p.Id == playerTotals[0].PlayerId);
            sb.AppendLine($"In testa: {topPlayer?.Name ?? "?"} con {playerTotals[0].Total} punti.");
        }

        if (photoCount > 0)
            sb.AppendLine($"{photoCount} foto salvate.");

        sb.AppendLine($"Snapshot #{snapshotIndex} creato.");

        return sb.ToString();
    }
}
```

**Note:** `SaveAgentSessionStateCommand` and `GetSnapshotPhotoCountQuery` may need to be created as thin wrappers. Check existing commands first. If `SaveAgentSessionStateCommand` doesn't exist, create it as a command that calls `agentSession.UpdateGameState()` with the current session state.

- [ ] **Step 4: Run tests — expect PASS**
- [ ] **Step 5: Add HTTP endpoint**

In `LiveSessionEndpoints.cs`:
```csharp
group.MapPost("/{sessionId:guid}/save-complete", HandleSaveComplete)
    .RequireSession()
    .RequireAuthorization()
    .WithName("SaveCompleteSessionState");
```

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(game-mgmt): add SaveCompleteSessionStateCommand — orchestrates pause+snapshot+agent persist"
```

---

### Task B2: GetSessionResumeContextQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetSessionResumeContextQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/SessionResumeContextDto.cs`

**Purpose:** Returns everything needed to resume: last snapshot, photos, scores, agent recap text.

- [ ] **Step 1: Create DTOs and query**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/SessionResumeContextDto.cs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

public sealed record SessionResumeContextDto
{
    public required Guid SessionId { get; init; }
    public required string GameTitle { get; init; }
    public required int LastSnapshotIndex { get; init; }
    public required int CurrentTurn { get; init; }
    public required string? CurrentPhase { get; init; }
    public required DateTime PausedAt { get; init; }
    public required string Recap { get; init; }
    public required IReadOnlyList<PlayerScoreSummary> PlayerScores { get; init; }
    public required IReadOnlyList<SessionPhotoSummary> Photos { get; init; }
}

public sealed record PlayerScoreSummary(Guid PlayerId, string Name, int TotalScore, int Rank);
public sealed record SessionPhotoSummary(Guid AttachmentId, string? ThumbnailUrl, string? Caption, string AttachmentType);
```

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/LiveSessions/GetSessionResumeContextQuery.cs
namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

public sealed record GetSessionResumeContextQuery(Guid SessionId) : IRequest<SessionResumeContextDto>;
```

- [ ] **Step 2: Implement handler** (loads session, scores, last snapshot photos, builds recap)

- [ ] **Step 3: Add endpoint**

```csharp
group.MapGet("/{sessionId:guid}/resume-context", HandleGetResumeContext)
    .RequireSession()
    .RequireAuthorization()
    .WithName("GetSessionResumeContext");
```

- [ ] **Step 4: Test + Commit**

```bash
git commit -m "feat(game-mgmt): add GetSessionResumeContextQuery for enhanced resume experience"
```

---

### Task B3: Frontend Enhanced Pause/Resume

**Files:**
- Modify: `apps/web/src/components/session/PauseSessionDialog.tsx`
- Create: `apps/web/src/components/session/SaveCompleteDialog.tsx`
- Modify: `apps/web/src/components/session/ResumePhotoReview.tsx`
- Create: `apps/web/src/components/session/ResumeSessionPanel.tsx`
- Create: `apps/web/src/components/session/__tests__/SaveCompleteDialog.test.tsx`
- Create: `apps/web/src/components/session/__tests__/ResumeSessionPanel.test.tsx`

**Purpose:** Replace basic pause dialog with "Salva Stato Completo" flow. Add resume panel with recap + photos.

- [ ] **Step 1: Write failing test for SaveCompleteDialog**

```typescript
// apps/web/src/components/session/__tests__/SaveCompleteDialog.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

const mockSaveComplete = vi.fn();
vi.mock('@/lib/api', () => ({
  api: { liveSessions: { saveComplete: (...args: unknown[]) => mockSaveComplete(...args) } },
}));

import { SaveCompleteDialog } from '../SaveCompleteDialog';

describe('SaveCompleteDialog', () => {
  it('shows save steps and progress', async () => {
    const user = userEvent.setup();
    mockSaveComplete.mockResolvedValue({
      sessionId: 'sess-1',
      snapshotIndex: 5,
      recap: 'Partita salvata al turno 5. In testa: Marco con 45 punti.',
      photoCount: 2,
      savedAt: new Date().toISOString(),
    });

    render(
      <SaveCompleteDialog
        open={true}
        onOpenChange={vi.fn()}
        sessionId="sess-1"
        onSaveComplete={vi.fn()}
      />
    );

    expect(screen.getByText(/salva stato completo/i)).toBeInTheDocument();

    const saveBtn = screen.getByRole('button', { name: /salva tutto/i });
    await user.click(saveBtn);

    await waitFor(() => {
      expect(screen.getByText(/partita salvata/i)).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 2: Implement SaveCompleteDialog**

```tsx
// apps/web/src/components/session/SaveCompleteDialog.tsx
'use client';

import { useCallback, useState } from 'react';
import { Camera, Check, Loader2, Save } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/button';
import { api } from '@/lib/api';
import { PhotoUploadModal } from './PhotoUploadModal';

interface SaveCompleteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sessionId: string;
  playerId?: string;
  snapshotIndex?: number;
  onSaveComplete: () => void;
}

type SavePhase = 'confirm' | 'photos' | 'saving' | 'done';

export function SaveCompleteDialog({
  open, onOpenChange, sessionId, playerId, snapshotIndex, onSaveComplete,
}: SaveCompleteDialogProps) {
  const [phase, setPhase] = useState<SavePhase>('confirm');
  const [recap, setRecap] = useState('');
  const [photoModalOpen, setPhotoModalOpen] = useState(false);

  const handleSave = useCallback(async () => {
    setPhase('saving');
    try {
      const result = await api.liveSessions.saveComplete(sessionId);
      setRecap(result.recap);
      setPhase('done');
    } catch {
      setPhase('confirm');
    }
  }, [sessionId]);

  const handleClose = useCallback(() => {
    onOpenChange(false);
    if (phase === 'done') onSaveComplete();
    setPhase('confirm');
    setRecap('');
  }, [onOpenChange, onSaveComplete, phase]);

  return (
    <>
      <AlertDialog open={open} onOpenChange={onOpenChange}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="flex items-center gap-2">
              <Save className="h-5 w-5" />
              Salva Stato Completo
            </AlertDialogTitle>
            <AlertDialogDescription>
              {phase === 'confirm' && 'Vuoi salvare lo stato della partita? Potrai riprendere in un secondo momento.'}
              {phase === 'saving' && 'Salvataggio in corso...'}
              {phase === 'done' && recap}
            </AlertDialogDescription>
          </AlertDialogHeader>

          {phase === 'confirm' && (
            <div className="space-y-3 py-2">
              <p className="text-sm text-muted-foreground">Il salvataggio include:</p>
              <ul className="text-sm space-y-1 ml-4 list-disc text-muted-foreground">
                <li>Punteggi e stato del turno</li>
                <li>Memoria dell&apos;agente AI</li>
                <li>Foto del tavolo (opzionale)</li>
              </ul>
            </div>
          )}

          {phase === 'saving' && (
            <div className="flex items-center justify-center py-6">
              <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
            </div>
          )}

          {phase === 'done' && (
            <div className="rounded-lg border border-green-500/30 bg-green-500/10 p-3 text-sm text-green-300">
              <Check className="inline h-4 w-4 mr-1" />
              {recap}
            </div>
          )}

          <AlertDialogFooter>
            {phase === 'confirm' && (
              <>
                <Button variant="outline" onClick={() => setPhotoModalOpen(true)}>
                  <Camera className="h-4 w-4 mr-2" /> Scatta Foto Prima
                </Button>
                <Button onClick={handleSave} aria-label="Salva tutto">
                  <Save className="h-4 w-4 mr-2" /> Salva Tutto
                </Button>
              </>
            )}
            {phase === 'done' && (
              <Button onClick={handleClose}>Chiudi</Button>
            )}
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <PhotoUploadModal
        open={photoModalOpen}
        onOpenChange={setPhotoModalOpen}
        sessionId={sessionId}
        playerId={playerId}
        snapshotIndex={snapshotIndex}
        onUploadComplete={() => setPhotoModalOpen(false)}
      />
    </>
  );
}
```

- [ ] **Step 3: Replace PauseSessionDialog usage with SaveCompleteDialog**

In `LiveSessionView.tsx` and `sessions/[id]/page.tsx`, replace `PauseSessionDialog` with `SaveCompleteDialog` when pausing.

- [ ] **Step 4: Create ResumeSessionPanel**

```tsx
// apps/web/src/components/session/ResumeSessionPanel.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { Camera, Clock, Play, Trophy, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { api } from '@/lib/api';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

interface ResumeSessionPanelProps {
  sessionId: string;
  onResume: () => void;
}

export function ResumeSessionPanel({ sessionId, onResume }: ResumeSessionPanelProps) {
  const { data: context, isLoading } = useQuery({
    queryKey: ['session-resume-context', sessionId],
    queryFn: () => api.liveSessions.getResumeContext(sessionId),
  });

  if (isLoading || !context) return null;

  return (
    <div className="rounded-xl border border-amber-500/30 bg-amber-500/5 p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="font-quicksand font-bold text-lg">{context.gameTitle}</h3>
        <span className="text-xs text-muted-foreground flex items-center gap-1">
          <Clock className="h-3 w-3" />
          {formatDistanceToNow(new Date(context.pausedAt), { addSuffix: true, locale: it })}
        </span>
      </div>

      <p className="text-sm text-muted-foreground">{context.recap}</p>

      {/* Score summary */}
      {context.playerScores.length > 0 && (
        <div className="flex items-center gap-2 flex-wrap">
          <Users className="h-4 w-4 text-muted-foreground" />
          {context.playerScores.map(p => (
            <span
              key={p.playerId}
              className={`text-sm px-2 py-0.5 rounded-full ${
                p.rank === 1
                  ? 'bg-amber-500/20 text-amber-300 font-medium'
                  : 'bg-slate-700/50 text-slate-300'
              }`}
            >
              {p.rank === 1 && <Trophy className="inline h-3 w-3 mr-1" />}
              {p.name}: {p.totalScore}
            </span>
          ))}
        </div>
      )}

      {/* Photo thumbnails */}
      {context.photos.length > 0 && (
        <div className="flex items-center gap-2">
          <Camera className="h-4 w-4 text-muted-foreground" />
          <div className="flex gap-1">
            {context.photos.slice(0, 4).map(photo => (
              <div
                key={photo.attachmentId}
                className="h-10 w-10 rounded bg-slate-700 overflow-hidden"
              >
                {photo.thumbnailUrl && (
                  <img
                    src={photo.thumbnailUrl}
                    alt={photo.caption ?? 'Foto sessione'}
                    className="h-full w-full object-cover"
                  />
                )}
              </div>
            ))}
            {context.photos.length > 4 && (
              <span className="text-xs text-muted-foreground self-center">
                +{context.photos.length - 4}
              </span>
            )}
          </div>
        </div>
      )}

      <Button onClick={onResume} className="w-full">
        <Play className="h-4 w-4 mr-2" /> Riprendi Partita (turno {context.currentTurn})
      </Button>
    </div>
  );
}
```

- [ ] **Step 5: Add ResumeSessionPanel to sessions list page**

In `apps/web/src/app/(authenticated)/sessions/page.tsx`, show `ResumeSessionPanel` for paused sessions at the top of the page, above the active/history tabs.

- [ ] **Step 6: Run tests — expect PASS**
- [ ] **Step 7: Commit**

```bash
git commit -m "feat(session): enhanced save/resume — SaveCompleteDialog + ResumeSessionPanel with recap and photos"
```

---

## Phase 3: Game Night Quick Start Wizard

### Task C1: GameNightWizard Component

**Files:**
- Create: `apps/web/src/components/game-night/GameNightWizard.tsx`
- Create: `apps/web/src/components/game-night/steps/SearchGameStep.tsx`
- Create: `apps/web/src/components/game-night/steps/UploadRulesStep.tsx`
- Create: `apps/web/src/components/game-night/steps/CreateSessionStep.tsx`
- Create: `apps/web/src/components/game-night/__tests__/GameNightWizard.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/new/page.tsx` (add "Quick Start" option)

**Purpose:** A streamlined 3-step wizard: (1) Find game (catalog → BGG fallback) → (2) Upload PDF + disclaimer → (3) Create session with players. All in one drawer.

**Key principle:** Reuse existing components — `GameSourceStep` pattern for search, `CopyrightDisclaimerModal` for disclaimer, `PdfUploadStep` pattern for upload.

- [ ] **Step 1: Write test**

```typescript
// apps/web/src/components/game-night/__tests__/GameNightWizard.test.tsx
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: { search: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }) },
    bgg: { search: vi.fn(), getGameDetails: vi.fn() },
    library: { addPrivateGame: vi.fn() },
    liveSessions: { create: vi.fn() },
  },
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { GameNightWizard } from '../GameNightWizard';

describe('GameNightWizard', () => {
  it('renders step 1: find game', () => {
    render(<GameNightWizard onComplete={vi.fn()} />);
    expect(screen.getByText(/trova il gioco/i)).toBeInTheDocument();
    expect(screen.getByText(/1.*3/)).toBeInTheDocument(); // Step 1 of 3
  });
});
```

- [ ] **Step 2: Implement GameNightWizard**

The wizard has 3 steps:
1. **SearchGameStep**: Catalog search with auto BGG fallback (reuses `GameSourceStep` search logic). Returns `{ gameId, gameTitle, isFromBgg }`.
2. **UploadRulesStep**: Shows `CopyrightDisclaimerModal` → then `PdfUploadStep` → then `PdfProcessingStatus`. Has "Salta" (skip) to proceed without PDF. Returns `{ pdfId? }`.
3. **CreateSessionStep**: Player name inputs (2-8 players), scoring dimensions from game metadata. Returns `{ sessionId }`.

On completion: redirects to `/sessions/{sessionId}` — the agent is ready (or shows degraded mode banner if PDF is still processing).

```tsx
// apps/web/src/components/game-night/GameNightWizard.tsx
'use client';

import { useCallback, useState } from 'react';
import { useRouter } from 'next/navigation';
import { SearchGameStep } from './steps/SearchGameStep';
import { UploadRulesStep } from './steps/UploadRulesStep';
import { CreateSessionStep } from './steps/CreateSessionStep';

type WizardStep = 'search' | 'upload' | 'session';

interface GameNightWizardProps {
  onComplete: (sessionId: string) => void;
}

interface WizardState {
  gameId?: string;
  gameTitle?: string;
  privateGameId?: string;
  pdfId?: string;
}

export function GameNightWizard({ onComplete }: GameNightWizardProps) {
  const [step, setStep] = useState<WizardStep>('search');
  const [state, setState] = useState<WizardState>({});
  const router = useRouter();

  const stepIndex = step === 'search' ? 1 : step === 'upload' ? 2 : 3;

  const handleGameFound = useCallback((data: {
    gameId?: string; privateGameId?: string; gameTitle: string;
  }) => {
    setState(prev => ({ ...prev, ...data }));
    setStep('upload');
  }, []);

  const handleUploadComplete = useCallback((pdfId?: string) => {
    setState(prev => ({ ...prev, pdfId }));
    setStep('session');
  }, []);

  const handleSessionCreated = useCallback((sessionId: string) => {
    onComplete(sessionId);
    router.push(`/sessions/${sessionId}`);
  }, [onComplete, router]);

  return (
    <div className="space-y-6">
      {/* Progress indicator */}
      <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
        <span className={stepIndex >= 1 ? 'text-amber-400 font-medium' : ''}>1. Trova il gioco</span>
        <span className="text-slate-600">→</span>
        <span className={stepIndex >= 2 ? 'text-amber-400 font-medium' : ''}>2. Regolamento</span>
        <span className="text-slate-600">→</span>
        <span className={stepIndex >= 3 ? 'text-amber-400 font-medium' : ''}>3. Giocatori</span>
      </div>

      {step === 'search' && (
        <SearchGameStep onGameFound={handleGameFound} />
      )}

      {step === 'upload' && (
        <UploadRulesStep
          gameId={state.gameId}
          privateGameId={state.privateGameId}
          gameTitle={state.gameTitle ?? ''}
          onComplete={handleUploadComplete}
          onSkip={() => handleUploadComplete()}
        />
      )}

      {step === 'session' && (
        <CreateSessionStep
          gameId={state.gameId ?? state.privateGameId ?? ''}
          gameTitle={state.gameTitle ?? ''}
          onSessionCreated={handleSessionCreated}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 3: Implement SearchGameStep** (reuses `GameSourceStep` search patterns — catalog search with BGG auto-fallback, existing `api.bgg.search` and `api.sharedGames.search`)

- [ ] **Step 4: Implement UploadRulesStep** (wraps `CopyrightDisclaimerModal` + existing `PdfUploadStep` + "Salta" skip button)

- [ ] **Step 5: Implement CreateSessionStep** (player name form + calls `api.liveSessions.create`)

- [ ] **Step 6: Add "Serata di Gioco" button to sessions/new page**

In `apps/web/src/app/(authenticated)/sessions/new/page.tsx`, add a prominent card at the top:

```tsx
<div className="rounded-xl border-2 border-amber-500/30 bg-amber-500/5 p-6 mb-6">
  <h2 className="font-quicksand font-bold text-xl mb-2">Serata di Gioco</h2>
  <p className="text-sm text-muted-foreground mb-4">
    Trova un gioco, carica il regolamento e inizia subito — l&apos;agente AI ti assiste.
  </p>
  <Button onClick={() => setShowWizard(true)}>
    Inizia Serata di Gioco
  </Button>
</div>
```

- [ ] **Step 7: Run tests — expect PASS**
- [ ] **Step 8: Commit**

```bash
git commit -m "feat(game-night): add GameNightWizard — 3-step quick start for improvised game nights"
```

---

### Task C2: Degraded Mode Banner

**Files:**
- Create: `apps/web/src/components/session/KbProcessingBanner.tsx`
- Create: `apps/web/src/components/session/__tests__/KbProcessingBanner.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/layout.tsx`

**Purpose:** When a session starts before KB processing completes, show a banner that auto-updates when ready.

- [ ] **Step 1: Implement KbProcessingBanner**

```tsx
// apps/web/src/components/session/KbProcessingBanner.tsx
'use client';

import { useEffect, useState } from 'react';
import { Bot, Check, Loader2 } from 'lucide-react';

interface KbProcessingBannerProps {
  gameId: string;
  onReady?: () => void;
}

export function KbProcessingBanner({ gameId, onReady }: KbProcessingBannerProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    // Poll KB status every 10 seconds
    const interval = setInterval(async () => {
      try {
        const response = await fetch(`/api/v1/documents/game/${gameId}/status`);
        const data = await response.json();
        if (data.isReady) {
          setReady(true);
          onReady?.();
          clearInterval(interval);
        }
      } catch { /* ignore polling errors */ }
    }, 10_000);

    return () => clearInterval(interval);
  }, [gameId, onReady]);

  if (ready) {
    return (
      <div className="rounded-lg border border-green-500/30 bg-green-500/10 px-4 py-2 text-sm text-green-300 flex items-center gap-2">
        <Check className="h-4 w-4" />
        Regolamento pronto! L&apos;agente AI conosce le regole.
      </div>
    );
  }

  return (
    <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-2 text-sm text-amber-200 flex items-center gap-2">
      <Loader2 className="h-4 w-4 animate-spin" />
      Regolamento in elaborazione... L&apos;agente avrà accesso alle regole tra poco.
    </div>
  );
}
```

- [ ] **Step 2: Integrate into session layout** (show banner when session's game has no indexed KB)
- [ ] **Step 3: Test + Commit**

```bash
git commit -m "feat(session): add KbProcessingBanner for degraded mode — auto-updates when KB ready"
```

---

## API Client Additions Summary

Add these methods to the frontend API client (`apps/web/src/lib/api/`):

| Client | Method | Endpoint |
|---|---|---|
| `scoreTrackingClient` (NEW) | `parseScore(sessionId, message, autoRecord)` | `POST /live-sessions/{id}/scores/parse` |
| `scoreTrackingClient` (NEW) | `confirmScore(sessionId, data)` | `POST /live-sessions/{id}/scores/confirm` |
| `liveSessionsClient` (MODIFY) | `saveComplete(sessionId)` | `POST /live-sessions/{id}/save-complete` |
| `liveSessionsClient` (MODIFY) | `getResumeContext(sessionId)` | `GET /live-sessions/{id}/resume-context` |

---

## DI Registrations Summary

| Service | Registration File | Lifetime |
|---|---|---|
| `IPlayerNameResolutionService` → `PlayerNameResolutionService` | `GameManagementServiceExtensions.cs` | Scoped |
| `ParseAndRecordScoreCommandHandler` | Auto-registered by MediatR assembly scan | — |
| `ConfirmScoreCommandHandler` | Auto-registered by MediatR assembly scan | — |
| `SaveCompleteSessionStateCommandHandler` | Auto-registered by MediatR assembly scan | — |

---

## Testing Strategy

### Unit Tests (per phase)

| Phase | Test Class | Count |
|---|---|---|
| A1 | `PlayerNameResolutionServiceTests` | 5 tests |
| A2 | `ParseAndRecordScoreCommandHandlerTests` | 5 tests |
| A3 | `ConfirmScoreCommandHandlerTests` | 2 tests |
| A5 | `ScoreAssistant.test.tsx` | 4 tests |
| B1 | `SaveCompleteSessionStateCommandHandlerTests` | 3 tests |
| B3 | `SaveCompleteDialog.test.tsx` | 3 tests |
| B3 | `ResumeSessionPanel.test.tsx` | 3 tests |
| C1 | `GameNightWizard.test.tsx` | 3 tests |
| C2 | `KbProcessingBanner.test.tsx` | 2 tests |

### Integration Tests

After all phases, create integration test verifying the full flow:
- Parse score → record → verify in scoreboard
- Save complete → verify snapshot + photos
- Resume → verify context returned

### E2E Test (Playwright)

One E2E test covering the happy path: search game → add → upload PDF → create session → score via assistant → pause → resume.

---

## Commit History Target

```
feat(game-mgmt): add PlayerNameResolutionService for fuzzy player name matching
feat(kb): add ParseAndRecordScoreCommand — NLU bridge to LiveGameSession scoring
feat(kb): add ConfirmScoreCommand for user-confirmed score recording
feat(routing): add /scores/parse and /scores/confirm endpoints for AI score tracking
feat(session): add ScoreAssistant panel with NLU score parsing and confirmation
feat(game-mgmt): add SaveCompleteSessionStateCommand — orchestrates pause+snapshot+agent persist
feat(game-mgmt): add GetSessionResumeContextQuery for enhanced resume experience
feat(session): enhanced save/resume — SaveCompleteDialog + ResumeSessionPanel with recap and photos
feat(game-night): add GameNightWizard — 3-step quick start for improvised game nights
feat(session): add KbProcessingBanner for degraded mode — auto-updates when KB ready
test: add integration tests for AI score tracking and save/resume flows
test(e2e): add game night journey happy path E2E test
```

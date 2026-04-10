# Default Game Toolkit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a 4-tab ToolkitDrawer (dice, notes, diary, scoreboard) accessible from MeepleCard NavFooter, with local-first state and optional SessionTracking sync.

**Architecture:** GameToolkit BC provides config/presets (read), SessionTracking BC persists runtime data (read/write). Frontend Zustand store handles local mode; promotion flow migrates to session. The drawer follows ExtraMeepleCardDrawer pattern with tabs.

**Tech Stack:** .NET 9 (MediatR, FluentValidation, EF Core, PostgreSQL) | Next.js (React 19, Zustand, TanStack Query, Tailwind, Framer Motion)

**Spec:** `docs/superpowers/specs/2026-04-08-default-game-toolkit-design.md`

---

## File Structure

### Backend (New Files)

```
apps/api/src/Api/BoundedContexts/
├── SessionTracking/
│   ├── Domain/
│   │   ├── Entities/SessionEvent.cs
│   │   └── Repositories/ISessionEventRepository.cs
│   ├── Application/
│   │   ├── Commands/AddSessionEventCommand.cs
│   │   ├── Commands/AddSessionEventCommandHandler.cs
│   │   ├── Commands/AddSessionEventCommandValidator.cs
│   │   ├── Queries/GetSessionEventsQuery.cs
│   │   ├── Queries/GetSessionEventsQueryHandler.cs
│   │   └── DTOs/SessionEventDtos.cs
│   └── Infrastructure/
│       ├── Persistence/SessionEventRepository.cs
│       └── Persistence/Configurations/SessionEventConfiguration.cs
│
└── GameToolkit/
    ├── Domain/Entities/GameToolkit.cs              ← MODIFY (add UserDicePreset)
    ├── Application/
    │   ├── Commands/UserDicePresetCommands.cs       ← NEW
    │   ├── Commands/UserDicePresetCommandHandlers.cs← NEW
    │   ├── Queries/GetUserDicePresetsQuery.cs       ← NEW
    │   └── DTOs/ToolkitDtos.cs                      ← MODIFY (add UserDicePresetDto)
    └── Infrastructure/
        └── Persistence/GameToolkitRepository.cs     ← MODIFY (serialize presets)
```

### Backend (Modified Files)

```
apps/api/src/Api/
├── Routing/SessionTrackingRoutes.cs                 ← ADD event endpoints
├── Routing/GameToolkitRoutes.cs                     ← ADD preset endpoints
├── BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/
│   └── SessionTrackingServiceExtensions.cs          ← REGISTER SessionEventRepository
└── Infrastructure/Persistence/Migrations/
    └── {timestamp}_AddSessionEventsAndUserDicePresets.cs ← NEW migration
```

### Frontend (New Files)

```
apps/web/src/
├── components/toolkit-drawer/
│   ├── ToolkitDrawer.tsx
│   ├── ToolkitDrawerProvider.tsx
│   ├── index.ts
│   ├── types.ts
│   ├── tabs/
│   │   ├── DiceRollerTab.tsx
│   │   ├── DicePresetRow.tsx
│   │   ├── DicePoolBuilder.tsx
│   │   ├── DiceResultDisplay.tsx
│   │   ├── NotesTab.tsx
│   │   ├── NoteCard.tsx
│   │   ├── EventDiaryTab.tsx
│   │   ├── DiaryEventRow.tsx
│   │   ├── DiaryFilters.tsx
│   │   ├── ScoreboardTab.tsx
│   │   ├── ScoreCell.tsx
│   │   ├── ScoreCategoryHeader.tsx
│   │   ├── RankingBar.tsx
│   │   └── RoundBreakdown.tsx
│   ├── shared/
│   │   ├── PlayerBar.tsx
│   │   ├── PlayerAvatar.tsx
│   │   ├── PlayerSetupModal.tsx
│   │   └── PromoteSessionModal.tsx
│   └── hooks/
│       ├── useToolkitConfig.ts
│       ├── usePlayerContext.ts
│       ├── useDiceRoller.ts
│       ├── useScoreboard.ts
│       ├── useNotes.ts
│       ├── useDiary.ts
│       └── useToolkitSession.ts
├── stores/toolkit-local-store.ts
└── lib/api/clients/toolkit.ts
```

---

## Task 1: SessionEvent Domain Entity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionEventRepository.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionEventTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionEventTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var evt = SessionEvent.Create(
            sessionId, "dice_roll",
            """{"formula":"2d6","results":[3,5],"total":8}""",
            participantId, roundNumber: 2);

        evt.Should().NotBeNull();
        evt.Id.Should().NotBe(Guid.Empty);
        evt.SessionId.Should().Be(sessionId);
        evt.EventType.Should().Be("dice_roll");
        evt.ParticipantId.Should().Be(participantId);
        evt.RoundNumber.Should().Be(2);
        evt.PayloadJson.Should().Contain("formula");
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptySessionId_ShouldThrow()
    {
        var act = () => SessionEvent.Create(Guid.Empty, "dice_roll", "{}");
        act.Should().Throw<ArgumentException>().WithMessage("*sessionId*");
    }

    [Fact]
    public void Create_WithEmptyEventType_ShouldThrow()
    {
        var act = () => SessionEvent.Create(Guid.NewGuid(), "", "{}");
        act.Should().Throw<ArgumentException>().WithMessage("*eventType*");
    }

    [Fact]
    public void Create_WithNullPayload_ShouldThrow()
    {
        var act = () => SessionEvent.Create(Guid.NewGuid(), "dice_roll", null!);
        act.Should().Throw<ArgumentException>().WithMessage("*payloadJson*");
    }

    [Fact]
    public void Create_WithNoParticipant_ShouldSucceed()
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "round_advance", """{"round":3}""");
        evt.ParticipantId.Should().BeNull();
        evt.RoundNumber.Should().BeNull();
    }

    [Fact]
    public void Create_EventType_ShouldBeTrimmedLowercase()
    {
        var evt = SessionEvent.Create(Guid.NewGuid(), "  Dice_Roll  ", "{}");
        evt.EventType.Should().Be("dice_roll");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~SessionEventTests" -v minimal`
Expected: FAIL — `SessionEvent` class does not exist.

- [ ] **Step 3: Write the domain entity**

```csharp
// SessionEvent.cs
using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

public class SessionEvent
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? ParticipantId { get; private set; }

    [MaxLength(50)]
    public string EventType { get; private set; } = string.Empty;

    public int? RoundNumber { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    private SessionEvent() { } // EF Core

    public static SessionEvent Create(
        Guid sessionId,
        string eventType,
        string payloadJson,
        Guid? participantId = null,
        int? roundNumber = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID is required.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type is required.", nameof(eventType));
        ArgumentNullException.ThrowIfNull(payloadJson, nameof(payloadJson));

        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            EventType = eventType.Trim().ToLowerInvariant(),
            PayloadJson = payloadJson,
            ParticipantId = participantId,
            RoundNumber = roundNumber,
            Timestamp = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 4: Write the repository interface**

```csharp
// ISessionEventRepository.cs
namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

public interface ISessionEventRepository
{
    Task<SessionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SessionEvent>> GetBySessionIdAsync(
        Guid sessionId,
        string? eventType = null,
        int? roundNumber = null,
        int limit = 50,
        DateTime? cursor = null,
        CancellationToken ct = default);
    Task AddAsync(SessionEvent sessionEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~SessionEventTests" -v minimal`
Expected: All 6 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionEvent.cs \
       apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionEventRepository.cs \
       apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionEventTests.cs
git commit -m "feat(session-tracking): add SessionEvent domain entity and repository interface"
```

---

## Task 2: SessionEvent Infrastructure (Repository + Config)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Persistence/SessionEventRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations/SessionEventConfiguration.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/SessionTrackingServiceExtensions.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (add DbSet)

- [ ] **Step 1: Write the entity configuration**

```csharp
// SessionEventConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

public class SessionEventConfiguration : IEntityTypeConfiguration<Api.BoundedContexts.SessionTracking.Domain.Entities.SessionEvent>
{
    public void Configure(EntityTypeBuilder<Api.BoundedContexts.SessionTracking.Domain.Entities.SessionEvent> builder)
    {
        builder.ToTable("session_tracking_session_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(e => e.ParticipantId)
            .HasColumnName("participant_id");

        builder.Property(e => e.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.RoundNumber)
            .HasColumnName("round_number");

        builder.Property(e => e.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.HasIndex(e => new { e.SessionId, e.Timestamp })
            .HasDatabaseName("idx_session_events_session_timestamp")
            .IsDescending(false, true);

        builder.HasIndex(e => new { e.SessionId, e.EventType })
            .HasDatabaseName("idx_session_events_session_type");

        builder.HasIndex(e => new { e.SessionId, e.RoundNumber })
            .HasDatabaseName("idx_session_events_session_round");
    }
}
```

- [ ] **Step 2: Write the repository implementation**

```csharp
// SessionEventRepository.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

public class SessionEventRepository : ISessionEventRepository
{
    private readonly MeepleAiDbContext _context;

    public SessionEventRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SessionEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Set<SessionEvent>()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<List<SessionEvent>> GetBySessionIdAsync(
        Guid sessionId,
        string? eventType = null,
        int? roundNumber = null,
        int limit = 50,
        DateTime? cursor = null,
        CancellationToken ct = default)
    {
        var query = _context.Set<SessionEvent>()
            .Where(e => e.SessionId == sessionId);

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType.Trim().ToLowerInvariant());

        if (roundNumber.HasValue)
            query = query.Where(e => e.RoundNumber == roundNumber.Value);

        if (cursor.HasValue)
            query = query.Where(e => e.Timestamp < cursor.Value);

        return await query
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SessionEvent sessionEvent, CancellationToken ct = default)
    {
        await _context.Set<SessionEvent>().AddAsync(sessionEvent, ct).ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Add DbSet to MeepleAiDbContext**

Find the `MeepleAiDbContext` class and add:

```csharp
public DbSet<Api.BoundedContexts.SessionTracking.Domain.Entities.SessionEvent> SessionTrackingSessionEvents => Set<Api.BoundedContexts.SessionTracking.Domain.Entities.SessionEvent>();
```

- [ ] **Step 4: Register in DI**

In `SessionTrackingServiceExtensions.cs`, add inside `AddSessionTrackingContext()`:

```csharp
services.AddScoped<ISessionEventRepository, SessionEventRepository>();
```

Add the using:

```csharp
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/ \
       apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git commit -m "feat(session-tracking): add SessionEvent repository, EF config, and DI registration"
```

---

## Task 3: SessionEvent Application Layer (Commands + Queries + DTOs)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionEventDtos.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AddSessionEventCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AddSessionEventCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/AddSessionEventCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionEventsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionEventsQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/AddSessionEventCommandHandlerTests.cs`

- [ ] **Step 1: Write DTOs**

```csharp
// SessionEventDtos.cs
namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public record SessionEventDto
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid? ParticipantId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public int? RoundNumber { get; init; }
    public string PayloadJson { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public record AddSessionEventRequest(
    string EventType,
    string PayloadJson,
    Guid? ParticipantId = null,
    int? RoundNumber = null);

public record GetSessionEventsResponse(
    Guid SessionId,
    IReadOnlyList<SessionEventDto> Events,
    DateTime? NextCursor);
```

- [ ] **Step 2: Write command + result**

```csharp
// AddSessionEventCommand.cs
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record AddSessionEventCommand(
    Guid SessionId,
    string EventType,
    string PayloadJson,
    Guid? ParticipantId = null,
    int? RoundNumber = null
) : IRequest<AddSessionEventResult>;

public record AddSessionEventResult(Guid EventId, DateTime Timestamp);
```

- [ ] **Step 3: Write validator**

```csharp
// AddSessionEventCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class AddSessionEventCommandValidator : AbstractValidator<AddSessionEventCommand>
{
    private static readonly HashSet<string> ValidEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "dice_roll", "score_change", "turn_change", "note_added",
        "manual_entry", "player_joined", "round_advance", "score_reset"
    };

    public AddSessionEventCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.EventType)
            .NotEmpty()
            .WithMessage("Event type is required.")
            .MaximumLength(50)
            .WithMessage("Event type cannot exceed 50 characters.")
            .Must(type => ValidEventTypes.Contains(type?.Trim() ?? ""))
            .WithMessage("Invalid event type. Valid types: dice_roll, score_change, turn_change, note_added, manual_entry, player_joined, round_advance, score_reset.");

        RuleFor(x => x.PayloadJson)
            .NotEmpty()
            .WithMessage("Payload JSON is required.");

        RuleFor(x => x.RoundNumber)
            .GreaterThan(0)
            .WithMessage("Round number must be positive.")
            .When(x => x.RoundNumber.HasValue);
    }
}
```

- [ ] **Step 4: Write the failing handler test**

```csharp
// AddSessionEventCommandHandlerTests.cs
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class AddSessionEventCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly AddSessionEventCommandHandler _handler;

    public AddSessionEventCommandHandlerTests()
    {
        _handler = new AddSessionEventCommandHandler(
            _sessionRepoMock.Object,
            _eventRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesEventAndReturnsResult()
    {
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), sessionId, SessionType.Generic);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddSessionEventCommand(
            sessionId, "dice_roll",
            """{"formula":"2d6","results":[3,5],"total":8}""",
            participantId, 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.EventId);
        _eventRepoMock.Verify(r => r.AddAsync(
            It.Is<SessionEvent>(e => e.EventType == "dice_roll" && e.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
        _eventRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new AddSessionEventCommand(sessionId, "dice_roll", "{}");

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
```

- [ ] **Step 5: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AddSessionEventCommandHandlerTests" -v minimal`
Expected: FAIL — handler does not exist.

- [ ] **Step 6: Write the command handler**

```csharp
// AddSessionEventCommandHandler.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class AddSessionEventCommandHandler : IRequestHandler<AddSessionEventCommand, AddSessionEventResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;

    public AddSessionEventCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
    }

    public async Task<AddSessionEventResult> Handle(
        AddSessionEventCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        var sessionEvent = SessionEvent.Create(
            request.SessionId,
            request.EventType,
            request.PayloadJson,
            request.ParticipantId,
            request.RoundNumber);

        await _eventRepository.AddAsync(sessionEvent, cancellationToken).ConfigureAwait(false);
        await _eventRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AddSessionEventResult(sessionEvent.Id, sessionEvent.Timestamp);
    }
}
```

- [ ] **Step 7: Write the query + handler**

```csharp
// GetSessionEventsQuery.cs
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetSessionEventsQuery(
    Guid SessionId,
    string? EventType = null,
    int? RoundNumber = null,
    int Limit = 50,
    DateTime? Cursor = null
) : IRequest<Api.BoundedContexts.SessionTracking.Application.DTOs.GetSessionEventsResponse>;
```

```csharp
// GetSessionEventsQueryHandler.cs
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetSessionEventsQueryHandler
    : IRequestHandler<GetSessionEventsQuery, GetSessionEventsResponse>
{
    private readonly ISessionEventRepository _repository;

    public GetSessionEventsQueryHandler(ISessionEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GetSessionEventsResponse> Handle(
        GetSessionEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await _repository.GetBySessionIdAsync(
            request.SessionId,
            request.EventType,
            request.RoundNumber,
            request.Limit,
            request.Cursor,
            cancellationToken).ConfigureAwait(false);

        var dtos = events.Select(e => new SessionEventDto
        {
            Id = e.Id,
            SessionId = e.SessionId,
            ParticipantId = e.ParticipantId,
            EventType = e.EventType,
            RoundNumber = e.RoundNumber,
            PayloadJson = e.PayloadJson,
            Timestamp = e.Timestamp
        }).ToList();

        var nextCursor = dtos.Count == request.Limit
            ? dtos[^1].Timestamp
            : (DateTime?)null;

        return new GetSessionEventsResponse(request.SessionId, dtos, nextCursor);
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AddSessionEventCommandHandlerTests" -v minimal`
Expected: All tests PASS.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/ \
       apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Handlers/AddSessionEventCommandHandlerTests.cs
git commit -m "feat(session-tracking): add SessionEvent commands, queries, DTOs, and handler"
```

---

## Task 4: SessionEvent API Endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/SessionTrackingRoutes.cs` (or the appropriate endpoint file for SessionTracking)

- [ ] **Step 1: Add event endpoints to the routing file**

Find the SessionTracking routing file. Add inside the route group:

```csharp
// Session Events
group.MapPost("/game-sessions/{sessionId:guid}/events", async (
    Guid sessionId,
    Api.BoundedContexts.SessionTracking.Application.DTOs.AddSessionEventRequest request,
    IMediator mediator,
    CancellationToken ct) =>
{
    var command = new Api.BoundedContexts.SessionTracking.Application.Commands.AddSessionEventCommand(
        sessionId, request.EventType, request.PayloadJson,
        request.ParticipantId, request.RoundNumber);
    var result = await mediator.Send(command, ct).ConfigureAwait(false);
    return Results.Created($"/api/v1/game-sessions/{sessionId}/events/{result.EventId}", result);
})
.RequireAuthenticatedUser()
.WithName("AddSessionEvent")
.WithTags("SessionTracking")
.WithSummary("Log an event to the session diary")
.Produces(201)
.Produces(400)
.Produces(404);

group.MapGet("/game-sessions/{sessionId:guid}/events", async (
    Guid sessionId,
    string? type,
    int? round,
    int? limit,
    DateTime? cursor,
    IMediator mediator,
    CancellationToken ct) =>
{
    var query = new Api.BoundedContexts.SessionTracking.Application.Queries.GetSessionEventsQuery(
        sessionId, type, round, limit ?? 50, cursor);
    var result = await mediator.Send(query, ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.RequireAuthenticatedUser()
.WithName("GetSessionEvents")
.WithTags("SessionTracking")
.WithSummary("Get session diary events with optional filters")
.Produces<Api.BoundedContexts.SessionTracking.Application.DTOs.GetSessionEventsResponse>(200);
```

- [ ] **Step 2: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/
git commit -m "feat(session-tracking): add session event API endpoints (POST + GET)"
```

---

## Task 5: UserDicePreset in GameToolkit BC

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/GameToolkit.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/UserDicePresetCommands.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/UserDicePresetCommandHandlers.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Queries/GetUserDicePresetsQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/DTOs/ToolkitDtos.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameToolkit/Infrastructure/Persistence/GameToolkitRepository.cs`
- Modify: `apps/api/src/Api/Routing/GameToolkitRoutes.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Domain/UserDicePresetTests.cs`

- [ ] **Step 1: Write the failing domain test**

```csharp
// UserDicePresetTests.cs
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameToolkit")]
public class UserDicePresetTests
{
    [Fact]
    public void AddUserDicePreset_WithValidParams_ShouldSucceed()
    {
        var toolkit = CreateTestToolkit();
        var userId = Guid.NewGuid();

        toolkit.AddUserDicePreset(userId, "Combat Roll", "2d6+1d8");

        toolkit.UserDicePresets.Should().HaveCount(1);
        toolkit.UserDicePresets[0].Name.Should().Be("Combat Roll");
        toolkit.UserDicePresets[0].Formula.Should().Be("2d6+1d8");
        toolkit.UserDicePresets[0].UserId.Should().Be(userId);
    }

    [Fact]
    public void AddUserDicePreset_OverLimit_ShouldThrow()
    {
        var toolkit = CreateTestToolkit();
        var userId = Guid.NewGuid();

        for (int i = 0; i < 20; i++)
            toolkit.AddUserDicePreset(userId, $"Preset {i}", $"{i + 1}d6");

        var act = () => toolkit.AddUserDicePreset(userId, "One more", "1d6");
        act.Should().Throw<InvalidOperationException>().WithMessage("*20*");
    }

    [Fact]
    public void RemoveUserDicePreset_Existing_ShouldSucceed()
    {
        var toolkit = CreateTestToolkit();
        var userId = Guid.NewGuid();
        toolkit.AddUserDicePreset(userId, "Test", "1d6");

        var result = toolkit.RemoveUserDicePreset(userId, "Test");

        result.Should().BeTrue();
        toolkit.UserDicePresets.Should().BeEmpty();
    }

    [Fact]
    public void RemoveUserDicePreset_WrongUser_ShouldReturnFalse()
    {
        var toolkit = CreateTestToolkit();
        toolkit.AddUserDicePreset(Guid.NewGuid(), "Test", "1d6");

        var result = toolkit.RemoveUserDicePreset(Guid.NewGuid(), "Test");
        result.Should().BeFalse();
    }

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateTestToolkit()
    {
        return Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit.Create(
            name: "Test Toolkit",
            gameId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~UserDicePresetTests" -v minimal`
Expected: FAIL — `AddUserDicePreset` method does not exist.

- [ ] **Step 3: Add UserDicePreset value object and methods to GameToolkit aggregate**

In `GameToolkit.cs`, add the value object class (inside or after the existing value objects):

```csharp
internal sealed class UserDicePreset
{
    public Guid UserId { get; }
    public string Name { get; }
    public string Formula { get; }
    public DateTime CreatedAt { get; }

    public UserDicePreset(Guid userId, string name, string formula)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Length > 50)
            throw new ArgumentException("Name cannot exceed 50 characters.", nameof(name));
        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException("Formula is required.", nameof(formula));
        if (formula.Length > 100)
            throw new ArgumentException("Formula cannot exceed 100 characters.", nameof(formula));

        UserId = userId;
        Name = name.Trim();
        Formula = formula.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
```

Add backing field and accessor to the GameToolkit class:

```csharp
private readonly List<UserDicePreset> _userDicePresets = new();
public IReadOnlyList<UserDicePreset> UserDicePresets => _userDicePresets.AsReadOnly();
```

Add mutation methods:

```csharp
public void AddUserDicePreset(Guid userId, string name, string formula)
{
    if (_userDicePresets.Count >= 20)
        throw new InvalidOperationException("Cannot add more than 20 user dice presets");

    _userDicePresets.Add(new UserDicePreset(userId, name, formula));
    UpdatedAt = DateTime.UtcNow;
}

public bool RemoveUserDicePreset(Guid userId, string name)
{
    var preset = _userDicePresets.Find(p =>
        p.UserId == userId &&
        string.Equals(p.Name, name, StringComparison.Ordinal));
    if (preset == null) return false;

    _userDicePresets.Remove(preset);
    UpdatedAt = DateTime.UtcNow;
    return true;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~UserDicePresetTests" -v minimal`
Expected: All 4 tests PASS.

- [ ] **Step 5: Add DTOs, commands, handlers, query, and routes**

Add to `ToolkitDtos.cs`:

```csharp
internal record UserDicePresetDto(
    Guid UserId,
    string Name,
    string Formula,
    DateTime CreatedAt);

internal record AddUserDicePresetRequest(
    string Name,
    string Formula);
```

Create `UserDicePresetCommands.cs`:

```csharp
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal record AddUserDicePresetCommand(
    Guid ToolkitId,
    Guid UserId,
    string Name,
    string Formula
) : IRequest<UserDicePresetDto>;

internal record RemoveUserDicePresetCommand(
    Guid ToolkitId,
    Guid UserId,
    string PresetName
) : IRequest<Unit>;
```

Create `UserDicePresetCommandHandlers.cs`:

```csharp
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

internal class AddUserDicePresetCommandHandler
    : IRequestHandler<AddUserDicePresetCommand, UserDicePresetDto>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddUserDicePresetCommandHandler(
        IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDicePresetDto> Handle(
        AddUserDicePresetCommand command, CancellationToken cancellationToken)
    {
        var toolkit = await _repository
            .GetByIdAsync(command.ToolkitId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        toolkit.AddUserDicePreset(command.UserId, command.Name, command.Formula);

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var preset = toolkit.UserDicePresets.Last();
        return new UserDicePresetDto(preset.UserId, preset.Name, preset.Formula, preset.CreatedAt);
    }
}

internal class RemoveUserDicePresetCommandHandler
    : IRequestHandler<RemoveUserDicePresetCommand, Unit>
{
    private readonly IGameToolkitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveUserDicePresetCommandHandler(
        IGameToolkitRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Unit> Handle(
        RemoveUserDicePresetCommand command, CancellationToken cancellationToken)
    {
        var toolkit = await _repository
            .GetByIdAsync(command.ToolkitId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", command.ToolkitId.ToString());

        var removed = toolkit.RemoveUserDicePreset(command.UserId, command.PresetName);
        if (!removed)
            throw new NotFoundException($"Preset '{command.PresetName}' not found for user");

        await _repository.UpdateAsync(toolkit, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
```

Create `GetUserDicePresetsQuery.cs`:

```csharp
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.GameToolkit.Application.Queries;

internal record GetUserDicePresetsQuery(
    Guid ToolkitId,
    Guid UserId
) : IRequest<List<UserDicePresetDto>>;

internal class GetUserDicePresetsQueryHandler
    : IRequestHandler<GetUserDicePresetsQuery, List<UserDicePresetDto>>
{
    private readonly IGameToolkitRepository _repository;

    public GetUserDicePresetsQueryHandler(IGameToolkitRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<UserDicePresetDto>> Handle(
        GetUserDicePresetsQuery request, CancellationToken cancellationToken)
    {
        var toolkit = await _repository
            .GetByIdAsync(request.ToolkitId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameToolkit", request.ToolkitId.ToString());

        return toolkit.UserDicePresets
            .Where(p => p.UserId == request.UserId)
            .Select(p => new UserDicePresetDto(p.UserId, p.Name, p.Formula, p.CreatedAt))
            .ToList();
    }
}
```

Add routes to `GameToolkitRoutes.cs`:

```csharp
// User Dice Presets
toolkits.MapPost("/{id:guid}/user-dice-presets", async (
    Guid id,
    AddUserDicePresetRequest request,
    HttpContext httpContext,
    IMediator m) =>
{
    var userId = httpContext.User.GetUserId();
    var command = new AddUserDicePresetCommand(id, userId, request.Name, request.Formula);
    var result = await m.Send(command).ConfigureAwait(false);
    return Results.Created($"/api/v1/game-toolkits/{id}/user-dice-presets", result);
})
.RequireAuthenticatedUser()
.WithName("AddUserDicePreset")
.WithSummary("Save a custom dice preset for the current user")
.Produces<UserDicePresetDto>(201);

toolkits.MapGet("/{id:guid}/user-dice-presets", async (
    Guid id,
    HttpContext httpContext,
    IMediator m) =>
{
    var userId = httpContext.User.GetUserId();
    var result = await m.Send(new GetUserDicePresetsQuery(id, userId)).ConfigureAwait(false);
    return Results.Ok(result);
})
.RequireAuthenticatedUser()
.WithName("GetUserDicePresets")
.WithSummary("Get custom dice presets for the current user")
.Produces<List<UserDicePresetDto>>(200);

toolkits.MapDelete("/{id:guid}/user-dice-presets/{presetName}", async (
    Guid id,
    string presetName,
    HttpContext httpContext,
    IMediator m) =>
{
    var userId = httpContext.User.GetUserId();
    await m.Send(new RemoveUserDicePresetCommand(id, userId, presetName)).ConfigureAwait(false);
    return Results.NoContent();
})
.RequireAuthenticatedUser()
.WithName("RemoveUserDicePreset")
.WithSummary("Remove a custom dice preset")
.Produces(204);
```

- [ ] **Step 6: Update GameToolkitRepository to serialize/deserialize UserDicePresets**

In `GameToolkitRepository.cs`, add JSON serialization for `_userDicePresets` following the same pattern as `DiceToolConfig` serialization. Add a `UserDicePresetJsonModel` record and serialize/deserialize alongside the existing tool configs.

- [ ] **Step 7: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameToolkit/ apps/api/src/Api/Routing/GameToolkitRoutes.cs \
       apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/
git commit -m "feat(game-toolkit): add UserDicePreset with CRUD commands, query, and API endpoints"
```

---

## Task 6: Database Migration

**Files:**
- Create: EF Core migration

- [ ] **Step 1: Create migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddSessionEventsAndUserDicePresets
```

- [ ] **Step 2: Review generated migration SQL**

Read the generated migration file. Verify:
- Table `session_tracking_session_events` with correct columns and indexes
- `UserDicePresetsJson` column (JSONB) added to GameToolkit entity table (if stored as JSON column)
- All column names are snake_case
- Indexes match the configuration

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Persistence/Migrations/
git commit -m "feat(db): add migration for SessionEvent table and UserDicePreset JSON column"
```

---

## Task 7: Frontend — Types and Zustand Local Store

**Files:**
- Create: `apps/web/src/components/toolkit-drawer/types.ts`
- Create: `apps/web/src/stores/toolkit-local-store.ts`

- [ ] **Step 1: Write shared types**

```typescript
// types.ts
import type { ReactNode } from 'react';

export type ToolkitTab = 'dice' | 'notes' | 'diary' | 'scores';

export type DiaryEventType =
  | 'dice_roll'
  | 'score_change'
  | 'turn_change'
  | 'note_added'
  | 'manual_entry'
  | 'player_joined'
  | 'round_advance'
  | 'score_reset';

export interface LocalPlayer {
  id: string;
  name: string;
  color: string;
  avatarUrl?: string;
}

export interface DiaryEvent {
  id: string;
  type: DiaryEventType;
  timestamp: Date;
  playerId?: string;
  playerName?: string;
  round?: number;
  payload: Record<string, unknown>;
}

export interface DiceResult {
  formula: string;
  rolls: { dieType: string; value: number }[];
  modifier: number;
  total: number;
}

export interface DicePreset {
  name: string;
  formula: string;
  source: 'universal' | 'ai' | 'custom';
  icon?: string;
}

export interface LocalNote {
  id: string;
  content: string;
  type: 'shared' | 'private';
  playerId?: string;
  playerName?: string;
  pinned: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface ToolkitDrawerProps {
  gameId: string;
  sessionId?: string;
  onClose: () => void;
  defaultTab?: ToolkitTab;
}

export const PLAYER_COLORS = [
  '#E67E22', '#9B59B6', '#2ECC71', '#3498DB',
  '#E74C3C', '#F1C40F', '#1ABC9C', '#E84393',
] as const;

export const UNIVERSAL_DICE_PRESETS: DicePreset[] = [
  { name: '1d6', formula: '1d6', source: 'universal' },
  { name: '2d6', formula: '2d6', source: 'universal' },
  { name: '1d20', formula: '1d20', source: 'universal' },
  { name: '3d6', formula: '3d6', source: 'universal' },
  { name: '1d100', formula: '1d100', source: 'universal' },
];
```

- [ ] **Step 2: Write Zustand store**

```typescript
// toolkit-local-store.ts
'use client';

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';
import { createJSONStorage } from 'zustand/middleware';

import type {
  LocalPlayer,
  DiaryEvent,
  LocalNote,
  DicePreset,
} from '@/components/toolkit-drawer/types';
import { PLAYER_COLORS } from '@/components/toolkit-drawer/types';

// ==== State ====

export interface ToolkitLocalState {
  players: LocalPlayer[];
  currentTurnIndex: number;
  currentRound: number;
  scores: Record<string, Record<string, number>>; // playerId → category → value
  scoreCategories: string[];
  notes: LocalNote[];
  diary: DiaryEvent[];
  customDicePresets: DicePreset[];
}

// ==== Actions ====

export interface ToolkitLocalActions {
  // Players
  addPlayer: (name: string) => void;
  removePlayer: (id: string) => void;
  reorderPlayers: (orderedIds: string[]) => void;
  setTurn: (playerId: string) => void;
  advanceTurn: () => void;
  advanceRound: () => void;

  // Scores
  setScore: (playerId: string, category: string, value: number) => void;
  addScoreCategory: (category: string) => void;
  removeScoreCategory: (category: string) => void;
  resetScores: () => void;

  // Notes
  addNote: (content: string, type: 'shared' | 'private', playerId?: string, playerName?: string) => void;
  updateNote: (noteId: string, content: string) => void;
  deleteNote: (noteId: string) => void;
  togglePin: (noteId: string) => void;

  // Diary
  logEvent: (event: Omit<DiaryEvent, 'id'>) => void;
  clearDiary: () => void;

  // Dice presets
  addCustomPreset: (name: string, formula: string) => void;
  removeCustomPreset: (name: string) => void;

  // Reset
  resetAll: () => void;
}

export type ToolkitLocalStore = ToolkitLocalState & ToolkitLocalActions;

// ==== Initial State ====

const initialState: ToolkitLocalState = {
  players: [],
  currentTurnIndex: 0,
  currentRound: 1,
  scores: {},
  scoreCategories: [],
  notes: [],
  diary: [],
  customDicePresets: [],
};

// ==== Store Factory ====

export function createToolkitLocalStore(gameId: string) {
  return create<ToolkitLocalStore>()(
    devtools(
      persist(
        immer((set, get) => ({
          ...initialState,

          // ---- Players ----

          addPlayer: (name: string) => {
            set((state) => {
              const color = PLAYER_COLORS[state.players.length % PLAYER_COLORS.length];
              const id = `local_${Date.now()}_${Math.random().toString(36).substring(7)}`;
              state.players.push({ id, name, color });
              state.scores[id] = {};
            });
          },

          removePlayer: (id: string) => {
            set((state) => {
              state.players = state.players.filter((p) => p.id !== id);
              delete state.scores[id];
              if (state.currentTurnIndex >= state.players.length) {
                state.currentTurnIndex = 0;
              }
            });
          },

          reorderPlayers: (orderedIds: string[]) => {
            set((state) => {
              const mapped = orderedIds
                .map((id) => state.players.find((p) => p.id === id))
                .filter(Boolean) as LocalPlayer[];
              state.players = mapped;
            });
          },

          setTurn: (playerId: string) => {
            set((state) => {
              const index = state.players.findIndex((p) => p.id === playerId);
              if (index >= 0) state.currentTurnIndex = index;
            });
          },

          advanceTurn: () => {
            set((state) => {
              if (state.players.length === 0) return;
              state.currentTurnIndex =
                (state.currentTurnIndex + 1) % state.players.length;
            });
          },

          advanceRound: () => {
            set((state) => {
              state.currentRound += 1;
              state.currentTurnIndex = 0;
            });
          },

          // ---- Scores ----

          setScore: (playerId: string, category: string, value: number) => {
            set((state) => {
              if (!state.scores[playerId]) state.scores[playerId] = {};
              state.scores[playerId][category] = value;
            });
          },

          addScoreCategory: (category: string) => {
            set((state) => {
              if (!state.scoreCategories.includes(category)) {
                state.scoreCategories.push(category);
              }
            });
          },

          removeScoreCategory: (category: string) => {
            set((state) => {
              state.scoreCategories = state.scoreCategories.filter((c) => c !== category);
              for (const playerId of Object.keys(state.scores)) {
                delete state.scores[playerId][category];
              }
            });
          },

          resetScores: () => {
            set((state) => {
              for (const playerId of Object.keys(state.scores)) {
                state.scores[playerId] = {};
              }
            });
          },

          // ---- Notes ----

          addNote: (content, type, playerId, playerName) => {
            set((state) => {
              const id = `note_${Date.now()}_${Math.random().toString(36).substring(7)}`;
              const now = new Date();
              state.notes.push({
                id, content, type, playerId, playerName,
                pinned: false, createdAt: now, updatedAt: now,
              });
            });
          },

          updateNote: (noteId: string, content: string) => {
            set((state) => {
              const note = state.notes.find((n) => n.id === noteId);
              if (note) {
                note.content = content;
                note.updatedAt = new Date();
              }
            });
          },

          deleteNote: (noteId: string) => {
            set((state) => {
              state.notes = state.notes.filter((n) => n.id !== noteId);
            });
          },

          togglePin: (noteId: string) => {
            set((state) => {
              const note = state.notes.find((n) => n.id === noteId);
              if (note) note.pinned = !note.pinned;
            });
          },

          // ---- Diary ----

          logEvent: (event) => {
            set((state) => {
              const id = `evt_${Date.now()}_${Math.random().toString(36).substring(7)}`;
              state.diary.unshift({ ...event, id });
            });
          },

          clearDiary: () => {
            set((state) => {
              state.diary = [];
            });
          },

          // ---- Dice Presets ----

          addCustomPreset: (name: string, formula: string) => {
            set((state) => {
              state.customDicePresets.push({ name, formula, source: 'custom' });
            });
          },

          removeCustomPreset: (name: string) => {
            set((state) => {
              state.customDicePresets = state.customDicePresets.filter(
                (p) => p.name !== name
              );
            });
          },

          // ---- Reset ----

          resetAll: () => {
            set(() => ({ ...initialState }));
          },
        })),
        {
          name: `meepleai-toolkit-${gameId}`,
          storage: createJSONStorage(() =>
            typeof window !== 'undefined'
              ? localStorage
              : {
                  getItem: () => null,
                  setItem: () => {},
                  removeItem: () => {},
                }
          ),
          partialize: (state) => ({
            players: state.players,
            currentTurnIndex: state.currentTurnIndex,
            currentRound: state.currentRound,
            scores: state.scores,
            scoreCategories: state.scoreCategories,
            notes: state.notes,
            diary: state.diary,
            customDicePresets: state.customDicePresets,
          }),
        }
      ),
      {
        name: `ToolkitLocalStore-${gameId}`,
        enabled: process.env.NODE_ENV === 'development',
      }
    )
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/toolkit-drawer/types.ts \
       apps/web/src/stores/toolkit-local-store.ts
git commit -m "feat(toolkit): add shared types and Zustand local store with localStorage persistence"
```

---

## Task 8: Frontend — API Client

**Files:**
- Create: `apps/web/src/lib/api/clients/toolkit.ts`

- [ ] **Step 1: Write the API client**

```typescript
// toolkit.ts
import type { HttpClient } from '../http-client';

// ==== Types ====

export interface SessionEventDto {
  id: string;
  sessionId: string;
  participantId?: string;
  eventType: string;
  roundNumber?: number;
  payloadJson: string;
  timestamp: string;
}

export interface GetSessionEventsResponse {
  sessionId: string;
  events: SessionEventDto[];
  nextCursor?: string;
}

export interface UserDicePresetDto {
  userId: string;
  name: string;
  formula: string;
  createdAt: string;
}

// ==== Client ====

export interface CreateToolkitClientParams {
  httpClient: HttpClient;
}

export interface ToolkitClient {
  // Session Events
  addSessionEvent(sessionId: string, event: {
    eventType: string;
    payloadJson: string;
    participantId?: string;
    roundNumber?: number;
  }): Promise<{ eventId: string; timestamp: string }>;

  getSessionEvents(sessionId: string, params?: {
    type?: string;
    round?: number;
    limit?: number;
    cursor?: string;
  }): Promise<GetSessionEventsResponse>;

  // User Dice Presets
  getUserDicePresets(toolkitId: string): Promise<UserDicePresetDto[]>;

  addUserDicePreset(toolkitId: string, preset: {
    name: string;
    formula: string;
  }): Promise<UserDicePresetDto>;

  removeUserDicePreset(toolkitId: string, presetName: string): Promise<void>;
}

export function createToolkitClient({ httpClient }: CreateToolkitClientParams): ToolkitClient {
  return {
    async addSessionEvent(sessionId, event) {
      return await httpClient.post(
        `/api/v1/game-sessions/${encodeURIComponent(sessionId)}/events`,
        event
      );
    },

    async getSessionEvents(sessionId, params = {}) {
      const searchParams = new URLSearchParams();
      if (params.type) searchParams.set('type', params.type);
      if (params.round) searchParams.set('round', String(params.round));
      if (params.limit) searchParams.set('limit', String(params.limit));
      if (params.cursor) searchParams.set('cursor', params.cursor);

      const qs = searchParams.toString();
      const url = `/api/v1/game-sessions/${encodeURIComponent(sessionId)}/events${qs ? `?${qs}` : ''}`;
      return await httpClient.get(url);
    },

    async getUserDicePresets(toolkitId) {
      const data = await httpClient.get<UserDicePresetDto[]>(
        `/api/v1/game-toolkits/${encodeURIComponent(toolkitId)}/user-dice-presets`
      );
      return data ?? [];
    },

    async addUserDicePreset(toolkitId, preset) {
      return await httpClient.post(
        `/api/v1/game-toolkits/${encodeURIComponent(toolkitId)}/user-dice-presets`,
        preset
      );
    },

    async removeUserDicePreset(toolkitId, presetName) {
      await httpClient.delete(
        `/api/v1/game-toolkits/${encodeURIComponent(toolkitId)}/user-dice-presets/${encodeURIComponent(presetName)}`
      );
    },
  };
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/clients/toolkit.ts
git commit -m "feat(toolkit): add toolkit API client for session events and user dice presets"
```

---

## Task 9: Frontend — ToolkitDrawer Container + Provider

**Files:**
- Create: `apps/web/src/components/toolkit-drawer/ToolkitDrawerProvider.tsx`
- Create: `apps/web/src/components/toolkit-drawer/ToolkitDrawer.tsx`
- Create: `apps/web/src/components/toolkit-drawer/index.ts`

- [ ] **Step 1: Write the Provider (context for all tabs)**

```typescript
// ToolkitDrawerProvider.tsx
'use client';

import React, { createContext, useContext, useMemo, useRef } from 'react';

import { createToolkitLocalStore, type ToolkitLocalStore } from '@/stores/toolkit-local-store';

import type { DiaryEvent, ToolkitTab } from './types';

interface ToolkitDrawerContextValue {
  gameId: string;
  sessionId?: string;
  mode: 'local' | 'session';
  store: ReturnType<typeof createToolkitLocalStore>;
  logEvent: (event: Omit<DiaryEvent, 'id'>) => void;
  activeTab: ToolkitTab;
  setActiveTab: (tab: ToolkitTab) => void;
}

const ToolkitDrawerContext = createContext<ToolkitDrawerContextValue | null>(null);

export function useToolkitDrawer() {
  const ctx = useContext(ToolkitDrawerContext);
  if (!ctx) throw new Error('useToolkitDrawer must be used within ToolkitDrawerProvider');
  return ctx;
}

interface ToolkitDrawerProviderProps {
  gameId: string;
  sessionId?: string;
  defaultTab?: ToolkitTab;
  children: React.ReactNode;
}

export function ToolkitDrawerProvider({
  gameId,
  sessionId,
  defaultTab = 'dice',
  children,
}: ToolkitDrawerProviderProps) {
  const storeRef = useRef<ReturnType<typeof createToolkitLocalStore>>();
  if (!storeRef.current) {
    storeRef.current = createToolkitLocalStore(gameId);
  }

  const [activeTab, setActiveTab] = React.useState<ToolkitTab>(defaultTab);

  const mode = sessionId ? 'session' : 'local';

  const logEvent = React.useCallback(
    (event: Omit<DiaryEvent, 'id'>) => {
      storeRef.current?.getState().logEvent(event);
      // TODO: if session mode, POST to /sessions/{id}/events in background
    },
    []
  );

  const value = useMemo<ToolkitDrawerContextValue>(
    () => ({
      gameId,
      sessionId,
      mode,
      store: storeRef.current!,
      logEvent,
      activeTab,
      setActiveTab,
    }),
    [gameId, sessionId, mode, logEvent, activeTab]
  );

  return (
    <ToolkitDrawerContext.Provider value={value}>
      {children}
    </ToolkitDrawerContext.Provider>
  );
}
```

- [ ] **Step 2: Write the Drawer container**

```typescript
// ToolkitDrawer.tsx
'use client';

import React, { useCallback, useEffect } from 'react';
import { AnimatePresence, motion } from 'framer-motion';

import { cn } from '@/lib/utils';

import { ToolkitDrawerProvider, useToolkitDrawer } from './ToolkitDrawerProvider';
import type { ToolkitDrawerProps, ToolkitTab } from './types';

const TAB_CONFIG: { key: ToolkitTab; icon: string; label: string }[] = [
  { key: 'dice', icon: '🎲', label: 'Dadi' },
  { key: 'notes', icon: '📝', label: 'Note' },
  { key: 'diary', icon: '📖', label: 'Diario' },
  { key: 'scores', icon: '🏆', label: 'Punti' },
];

function DrawerContent({ onClose }: { onClose: () => void }) {
  const { activeTab, setActiveTab } = useToolkitDrawer();

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    },
    [onClose]
  );

  useEffect(() => {
    document.addEventListener('keydown', handleKeyDown);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = '';
    };
  }, [handleKeyDown]);

  return (
    <>
      {/* Overlay */}
      <motion.div
        data-testid="toolkit-drawer-overlay"
        className="fixed inset-0 z-50 bg-black/30"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        transition={{ duration: 0.2 }}
        onClick={onClose}
      />

      {/* Panel */}
      <motion.div
        data-testid="toolkit-drawer-panel"
        role="dialog"
        aria-modal="true"
        aria-label="Game Toolkit"
        className={cn(
          'fixed inset-x-0 top-16 bottom-0 z-50',
          'flex flex-col overflow-hidden',
          'bg-white/95 backdrop-blur-xl saturate-[1.8]',
          'border-t border-gray-200',
          'shadow-[0_-8px_32px_rgba(0,0,0,0.12)]',
          'rounded-t-2xl'
        )}
        initial={{ y: '-100%' }}
        animate={{ y: 0 }}
        exit={{ y: '-100%' }}
        transition={{ type: 'spring', damping: 30, stiffness: 300 }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Drag Handle */}
        <div className="flex justify-center py-2.5">
          <div className="h-1 w-9 rounded-full bg-gray-300" />
        </div>

        {/* Tabs */}
        <div className="flex border-b border-gray-200 px-3 gap-0.5">
          {TAB_CONFIG.map((tab) => (
            <button
              key={tab.key}
              data-testid={`toolkit-tab-${tab.key}`}
              className={cn(
                'px-2.5 py-2 text-[11px] font-bold font-[Quicksand,sans-serif]',
                'border-b-2 transition-colors duration-200',
                'bg-transparent border-t-0 border-l-0 border-r-0 cursor-pointer',
                activeTab === tab.key
                  ? 'text-[hsl(142,70%,45%)] border-b-[hsl(142,70%,45%)]'
                  : 'text-gray-400 border-b-transparent hover:text-gray-700'
              )}
              onClick={() => setActiveTab(tab.key)}
            >
              {tab.icon} {tab.label}
            </button>
          ))}
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabContent tab={activeTab} />
        </div>

        {/* PlayerBar will go here */}
      </motion.div>
    </>
  );
}

function TabContent({ tab }: { tab: ToolkitTab }) {
  switch (tab) {
    case 'dice':
      return <div data-testid="dice-tab-placeholder">Dice Roller Tab</div>;
    case 'notes':
      return <div data-testid="notes-tab-placeholder">Notes Tab</div>;
    case 'diary':
      return <div data-testid="diary-tab-placeholder">Event Diary Tab</div>;
    case 'scores':
      return <div data-testid="scores-tab-placeholder">Scoreboard Tab</div>;
    default:
      return null;
  }
}

export function ToolkitDrawer({
  gameId,
  sessionId,
  onClose,
  defaultTab,
}: ToolkitDrawerProps) {
  return (
    <AnimatePresence>
      <ToolkitDrawerProvider
        gameId={gameId}
        sessionId={sessionId}
        defaultTab={defaultTab}
      >
        <DrawerContent onClose={onClose} />
      </ToolkitDrawerProvider>
    </AnimatePresence>
  );
}
```

- [ ] **Step 3: Write index.ts**

```typescript
// index.ts
export { ToolkitDrawer } from './ToolkitDrawer';
export type { ToolkitDrawerProps, ToolkitTab } from './types';
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/toolkit-drawer/
git commit -m "feat(toolkit): add ToolkitDrawer container with tabs, provider, and glassmorphism panel"
```

---

## Tasks 10-15: Remaining Frontend Components

> **Note:** Tasks 10-15 follow the same pattern. Each task creates one tab component or shared component with its hook. The detailed implementation for each tab follows the spec sections 4-7. For brevity, these tasks are described at a higher level — the implementing engineer should reference the spec for exact layouts and interactions.

### Task 10: PlayerBar + PlayerAvatar + PlayerSetupModal
- Create `shared/PlayerBar.tsx`, `shared/PlayerAvatar.tsx`, `shared/PlayerSetupModal.tsx`
- PlayerBar reads from Zustand store, renders color dots with initials, turn indicator, add button, promote button
- Wire `advanceTurn`, `addPlayer`, `removePlayer` actions

### Task 11: DiceRollerTab + sub-components
- Create `tabs/DiceRollerTab.tsx`, `tabs/DicePresetRow.tsx`, `tabs/DicePoolBuilder.tsx`, `tabs/DiceResultDisplay.tsx`
- Create `hooks/useDiceRoller.ts` with formula parsing and `crypto.getRandomValues()` client-side rolling
- Create `hooks/useToolkitConfig.ts` with React Query to fetch GameToolkit config (AI presets)
- Wire preset rows (universal, AI, custom), pool builder steppers, roll button, result display with shake animation
- Log `dice_roll` events to diary on each roll

### Task 12: NotesTab + NoteCard
- Create `tabs/NotesTab.tsx`, `tabs/NoteCard.tsx`
- Create `hooks/useNotes.ts` for local/session mode CRUD
- Grouped display: shared notes, then private notes
- Inline edit on tap, swipe-to-delete, pin toggle

### Task 13: EventDiaryTab + sub-components
- Create `tabs/EventDiaryTab.tsx`, `tabs/DiaryEventRow.tsx`, `tabs/DiaryFilters.tsx`
- Create `hooks/useDiary.ts` for local/session mode reading + manual entry
- Pill filter toggles, round grouping, manual entry input at bottom
- Event icons by type, timestamp formatting

### Task 14: ScoreboardTab + sub-components
- Create `tabs/ScoreboardTab.tsx`, `tabs/ScoreCell.tsx`, `tabs/ScoreCategoryHeader.tsx`, `tabs/RankingBar.tsx`, `tabs/RoundBreakdown.tsx`
- Create `hooks/useScoreboard.ts` for local/session mode CRUD
- Multi-dimension table with inline edit + stepper
- Drag handle for row reorder, ranking bar with medals
- Category add/remove, round breakdown expandable, reset with diary event

### Task 15: Integration + PromoteSessionModal
- Create `shared/PromoteSessionModal.tsx`
- Create `hooks/useToolkitSession.ts` for promotion flow (create session + bulk migrate)
- Modify consumer pages to add 🧰 Toolkit NavFooterItem on game MeepleCards
- Render `<ToolkitDrawer>` conditionally on drawer open state

---

## Task 16: Frontend Tests

**Files:**
- Create: `apps/web/src/__tests__/components/toolkit-drawer/ToolkitDrawer.test.tsx`
- Create: `apps/web/src/__tests__/stores/toolkit-local-store.test.ts`

- [ ] **Step 1: Write store tests**

```typescript
// toolkit-local-store.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { createToolkitLocalStore } from '@/stores/toolkit-local-store';

describe('ToolkitLocalStore', () => {
  let store: ReturnType<typeof createToolkitLocalStore>;

  beforeEach(() => {
    store = createToolkitLocalStore('test-game-id');
    store.getState().resetAll();
  });

  describe('Players', () => {
    it('adds a player with auto-assigned color', () => {
      store.getState().addPlayer('Marco');
      const { players } = store.getState();
      expect(players).toHaveLength(1);
      expect(players[0].name).toBe('Marco');
      expect(players[0].color).toBe('#E67E22');
    });

    it('advances turn cyclically', () => {
      store.getState().addPlayer('Marco');
      store.getState().addPlayer('Luca');
      expect(store.getState().currentTurnIndex).toBe(0);

      store.getState().advanceTurn();
      expect(store.getState().currentTurnIndex).toBe(1);

      store.getState().advanceTurn();
      expect(store.getState().currentTurnIndex).toBe(0);
    });

    it('advances round and resets turn', () => {
      store.getState().addPlayer('Marco');
      store.getState().addPlayer('Luca');
      store.getState().advanceTurn();

      store.getState().advanceRound();
      expect(store.getState().currentRound).toBe(2);
      expect(store.getState().currentTurnIndex).toBe(0);
    });
  });

  describe('Scores', () => {
    it('sets score for player and category', () => {
      store.getState().addPlayer('Marco');
      const playerId = store.getState().players[0].id;

      store.getState().addScoreCategory('PV');
      store.getState().setScore(playerId, 'PV', 12);

      expect(store.getState().scores[playerId]['PV']).toBe(12);
    });

    it('resets all scores to empty', () => {
      store.getState().addPlayer('Marco');
      const playerId = store.getState().players[0].id;
      store.getState().setScore(playerId, 'PV', 12);

      store.getState().resetScores();
      expect(store.getState().scores[playerId]).toEqual({});
    });
  });

  describe('Diary', () => {
    it('logs events in reverse chronological order', () => {
      store.getState().logEvent({
        type: 'dice_roll',
        timestamp: new Date(),
        payload: { formula: '2d6', total: 8 },
      });
      store.getState().logEvent({
        type: 'score_change',
        timestamp: new Date(),
        payload: { category: 'PV', delta: 3 },
      });

      const { diary } = store.getState();
      expect(diary).toHaveLength(2);
      expect(diary[0].type).toBe('score_change');
      expect(diary[1].type).toBe('dice_roll');
    });
  });
});
```

- [ ] **Step 2: Write drawer component test**

```typescript
// ToolkitDrawer.test.tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ToolkitDrawer } from '@/components/toolkit-drawer';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
}));

describe('ToolkitDrawer', () => {
  const defaultProps = {
    gameId: 'test-game-id',
    onClose: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all 4 tab buttons', () => {
    render(<ToolkitDrawer {...defaultProps} />);
    expect(screen.getByTestId('toolkit-tab-dice')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-tab-notes')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-tab-diary')).toBeInTheDocument();
    expect(screen.getByTestId('toolkit-tab-scores')).toBeInTheDocument();
  });

  it('shows dice tab by default', () => {
    render(<ToolkitDrawer {...defaultProps} />);
    expect(screen.getByTestId('dice-tab-placeholder')).toBeInTheDocument();
  });

  it('switches tabs on click', async () => {
    const user = userEvent.setup();
    render(<ToolkitDrawer {...defaultProps} />);

    await user.click(screen.getByTestId('toolkit-tab-scores'));
    expect(screen.getByTestId('scores-tab-placeholder')).toBeInTheDocument();
  });

  it('calls onClose when overlay is clicked', async () => {
    const user = userEvent.setup();
    render(<ToolkitDrawer {...defaultProps} />);

    await user.click(screen.getByTestId('toolkit-drawer-overlay'));
    expect(defaultProps.onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose on Escape key', async () => {
    const user = userEvent.setup();
    render(<ToolkitDrawer {...defaultProps} />);

    await user.keyboard('{Escape}');
    expect(defaultProps.onClose).toHaveBeenCalledOnce();
  });

  it('accepts defaultTab prop', () => {
    render(<ToolkitDrawer {...defaultProps} defaultTab="scores" />);
    expect(screen.getByTestId('scores-tab-placeholder')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm test -- --run --reporter=verbose toolkit-drawer toolkit-local-store`
Expected: All tests PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/__tests__/
git commit -m "test(toolkit): add unit tests for ToolkitDrawer component and Zustand local store"
```

---

## Summary

| Task | Description | Backend/Frontend | Files |
|------|-------------|-----------------|-------|
| 1 | SessionEvent domain entity | Backend | 3 |
| 2 | SessionEvent infrastructure | Backend | 4 |
| 3 | SessionEvent application layer | Backend | 8 |
| 4 | SessionEvent API endpoints | Backend | 1 |
| 5 | UserDicePreset in GameToolkit | Backend | 8 |
| 6 | Database migration | Backend | 1 |
| 7 | Types + Zustand store | Frontend | 2 |
| 8 | API client | Frontend | 1 |
| 9 | ToolkitDrawer container | Frontend | 3 |
| 10 | PlayerBar + shared | Frontend | 3 |
| 11 | DiceRollerTab + hooks | Frontend | 6 |
| 12 | NotesTab | Frontend | 3 |
| 13 | EventDiaryTab | Frontend | 4 |
| 14 | ScoreboardTab | Frontend | 6 |
| 15 | Integration + promotion | Frontend | 3 |
| 16 | Frontend tests | Frontend | 2 |
| **Total** | | | **~58** |

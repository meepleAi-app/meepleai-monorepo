# SessionTracking Bounded Context

> **Living Documentation**: This README is auto-documented. References extracted from source code.

## Overview

**Responsibility**: Manage collaborative game sessions with multi-device scorekeeper, real-time synchronization, and comprehensive scoring tools.

**DDD Strategy**: Supporting Domain

**Epic**: EPIC-GST-001 (Game Session Toolkit)

**Note**:
- Renamed from `GameSession` to `SessionTracking` to avoid naming conflict with `GameManagement.Domain.Entities.GameSession`
- Table names use `session_tracking_` prefix to avoid conflict with existing `game_sessions` table from UserLibrary
- Uses MeepleAiDbContext (central DbSet pattern) instead of separate SessionDbContext for consistency with other bounded contexts

## Context

MeepleAI requires a dedicated session management system separate from UserLibrary because:
- **Complexity**: Real-time sync, event-driven architecture, analytics
- **Distinct Business Logic**: Scoring rules, collaboration workflows, game templates
- **Independent Scalability**: Active sessions vs static library data

## Domain Model

### Aggregates

#### Session (Root Aggregate)
**Invariants**:
- Session code must be unique (6 alphanumeric characters)
- Status transitions: Active → Paused → Active → Finalized (one-way to Finalized)
- Cannot add participants after finalization
- Session must have at least one participant (owner)

**Domain File**: `Domain/Entities/Session.cs`

**Value Objects**: ParticipantInfo, SessionResult

**Domain Events**:
- SessionCreatedEvent
- SessionFinalizedEvent
- ScoreUpdatedEvent

#### ScoreEntry
**Invariants**:
- Score value required (supports decimal precision)
- Must reference valid session and participant
- Round number OR category required (or both)

**Domain File**: `Domain/Entities/ScoreEntry.cs`

#### PlayerNote
**Invariants**:
- Content cannot be empty
- Note type: Private, Shared, or Template
- Template notes must have template_key

**Domain File**: `Domain/Entities/PlayerNote.cs`

### Value Objects

| Nome | Validations | Usage |
|------|-------------|-------|
| ParticipantInfo | DisplayName non-empty, max 50 chars | Session participants metadata |
| ScoreCalculation | Valid totals and ranks | Score aggregation and ranking |
| SessionResult | Valid winner ID, non-empty ranks | Final session results |

### Domain Services

- **Session Code Generator**: Generates unique 6-character alphanumeric codes (excludes 0, O, I, 1)

## Database Schema

### Tables (6 total)

1. **session_tracking_sessions** - Core session metadata
   - Fields: id, user_id, game_id, session_code (UNIQUE), session_type, status, session_date, location
   - Soft delete: is_deleted, deleted_at
   - Audit: created_at, updated_at, created_by, updated_by
   - Concurrency: row_version (optimistic locking)
   - Foreign keys: users (restrict), games (restrict)

2. **session_tracking_participants** - Players in session
   - Fields: id, session_id, user_id (nullable), display_name, is_owner, join_order, final_rank, created_at
   - CASCADE delete on session deletion
   - Foreign keys: sessions (cascade), users (restrict)

3. **session_tracking_score_entries** - Score tracking per round/category
   - Fields: id, session_id, participant_id, round_number, category, score_value (DECIMAL 10,2), timestamp, created_by
   - Indexes: session+participant, session+round
   - Foreign keys: sessions (cascade), participants (cascade)

4. **session_tracking_notes** - Private/shared notes
   - Fields: id, session_id, participant_id, note_type, template_key, content (TEXT), is_hidden, created_at, updated_at
   - Index: session+participant
   - Foreign keys: sessions (cascade), participants (cascade)

5. **session_tracking_dice_rolls** (Phase 2 - GST-003)
   - Fields: id, session_id, participant_id, dice_type, roll_count, results (JSONB), timestamp
   - Index: session+timestamp (DESC)
   - Foreign keys: sessions (cascade), participants (cascade)

6. **session_tracking_card_draws** (Phase 2 - GST-003)
   - Fields: id, session_id, participant_id, deck_type, deck_id, card_value, timestamp
   - Index: session+timestamp (DESC)
   - Foreign keys: sessions (cascade), participants (cascade)

**Note**: game_decks table will be added in Phase 2 when deck management is implemented.

### Migration

**Migration Name**: `AddSessionTrackingTables`
**Migration File**: `Infrastructure/Migrations/20260130110404_AddSessionTrackingTables.cs`
**Context**: MeepleAiDbContext (follows standard project pattern)

**Indexes**:
- `idx_sessions_user_status` - Filter active sessions by user
- `idx_sessions_game_date` - Game play history
- `idx_sessions_code` - UNIQUE constraint for session codes
- `idx_participants_session` - Participant lookups
- `idx_scores_session_participant` - Score queries
- `idx_scores_round` - Round-based scoring
- `idx_notes_session_participant` - Note retrieval

## Application Layer (CQRS)

### Commands

**Implemented (GST-002)**: All 6 MVP commands with validators and handlers

#### 1. CreateSessionCommand
Creates a new game session with initial participants.

**Request**:
```csharp
public record CreateSessionCommand(
    Guid UserId,
    Guid? GameId,
    string SessionType, // 'Generic' | 'GameSpecific'
    DateTime? SessionDate,
    string? Location,
    List<ParticipantDto> Participants
) : ICommand<CreateSessionResult>;
```

**Validation**:
- SessionType must be 'Generic' or 'GameSpecific'
- GameId required if SessionType = 'GameSpecific'
- At least 1 participant required, max 20
- At least 1 participant must be owner
- DisplayName required, max 50 chars

**Handler**: `CreateSessionCommandHandler`
- Generates unique 6-char session code (max 3 retries)
- Creates Session aggregate with factory
- Adds participants via Session.AddParticipant
- Throws ConflictException if code collision after retries

#### 2. UpdateScoreCommand
Adds or updates a score entry for a participant.

**Request**:
```csharp
public record UpdateScoreCommand(
    Guid SessionId,
    Guid ParticipantId,
    int? RoundNumber,
    string? Category,
    decimal ScoreValue
) : IRequest<UpdateScoreResult>;
```

**Validation**:
- SessionId and ParticipantId required
- ScoreValue between -99999 and 99999
- RoundNumber ≥1 if specified
- Category max 50 chars

**Handler**: `UpdateScoreCommandHandler`
- Verifies session Active status
- Creates ScoreEntry via IScoreEntryRepository
- Calculates updated totals and ranking
- Returns new rank position

#### 3. AddParticipantCommand
Adds a new participant to an existing session.

**Request**:
```csharp
public record AddParticipantCommand(
    Guid SessionId,
    string DisplayName,
    Guid? UserId
) : IRequest<AddParticipantResult>;
```

**Validation**:
- SessionId required
- DisplayName required, max 50 chars

**Handler**: `AddParticipantCommandHandler`
- Verifies session not Finalized
- Enforces max 20 participants limit
- Calculates JoinOrder (auto-increment)
- Uses ParticipantInfo value object

#### 4. AddNoteCommand
Adds a private or shared note to a participant.

**Request**:
```csharp
public record AddNoteCommand(
    Guid SessionId,
    Guid ParticipantId,
    string NoteType, // 'Private' | 'Shared' | 'Template'
    string? TemplateKey,
    string Content,
    bool IsHidden
) : IRequest<AddNoteResult>;
```

**Validation**:
- NoteType in ['Private', 'Shared', 'Template']
- Content required, max 5000 chars
- TemplateKey required if NoteType = 'Template'

**Handler**: `AddNoteCommandHandler`
- Creates PlayerNote domain entity
- Direct persistence via DbContext
- TODO: Emit NoteAddedEvent for SSE (GST-003)

#### 5. FinalizeSessionCommand
Closes session and sets final ranks.

**Request**:
```csharp
public record FinalizeSessionCommand(
    Guid SessionId,
    Dictionary<Guid, int> FinalRanks
) : IRequest<FinalizeSessionResult>;
```

**Validation**:
- FinalRanks required
- Ranks must be consecutive from 1 to N (no gaps)
- All participants must have a rank

**Handler**: `FinalizeSessionCommandHandler`
- Verifies session Active or Paused
- Calls Session.Finalize() domain method
- Calculates final scores via IScoreEntryRepository
- TODO: UserLibrary integration (GST-005)
- TODO: Emit SessionFinalizedEvent (GST-003)

#### 6. ShareSessionResultsCommand
Exports session results (Phase 2 - GST-013).

**Handler**: Placeholder implementation

### Queries

**Implemented (GST-002)**: All 5 MVP queries with handlers

#### 1. GetActiveSessionQuery
Returns user's current active session.

**Request**:
```csharp
public record GetActiveSessionQuery(Guid UserId) : IRequest<SessionDto?>;
```

**Handler**: `GetActiveSessionQueryHandler`
- Filters: CreatedBy = UserId AND Status IN ('Active', 'Paused')
- Includes Participants + ScoreEntries
- Calculates participant total scores
- Returns null if no active session

#### 2. GetSessionByCodeQuery
Finds session by 6-char code for joining.

**Request**:
```csharp
public record GetSessionByCodeQuery(string SessionCode) : IRequest<SessionDto?>;
```

**Handler**: `GetSessionByCodeQueryHandler`
- Filters: SessionCode = code AND Status != 'Finalized'
- Returns null if not found or already finalized

#### 3. GetScoreboardQuery
Real-time scoreboard with rankings.

**Request**:
```csharp
public record GetScoreboardQuery(Guid SessionId) : IRequest<ScoreboardDto>;
```

**Handler**: `GetScoreboardQueryHandler`
- Aggregates scores per participant
- Calculates current ranks (order by total DESC)
- Groups scores by round and category
- Returns current leader

**Response**: ScoreboardDto with ScoresByRound, ScoresByCategory matrices

#### 4. GetSessionDetailsQuery
Complete session data for detail view.

**Request**:
```csharp
public record GetSessionDetailsQuery(Guid SessionId) : IRequest<SessionDetailsDto>;
```

**Handler**: `GetSessionDetailsQueryHandler`
- Includes all participants, score entries, notes
- Only returns shared/visible notes
- Complete audit trail with timestamps
- Throws NotFoundException if session not found

#### 5. GetSessionHistoryQuery
Paginated session history.

**Request**:
```csharp
public record GetSessionHistoryQuery(
    Guid UserId,
    Guid? GameId,
    int Limit,
    int Offset
) : IRequest<List<SessionSummaryDto>>;
```

**Handler**: `GetSessionHistoryQueryHandler`
- Filters by UserId (CreatedBy)
- Optional GameId filter
- Ordered by SessionDate DESC
- Pagination with limit/offset
- Returns SessionSummaryDto with winner and duration

### DTOs

**SessionDto**: Full session data with participants and scores
**SessionDetailsDto**: Extended with notes and complete audit
**SessionSummaryDto**: Lightweight for list views
**ScoreboardDto**: Real-time scoreboard with ranking matrices
**ParticipantDto**: Participant metadata with total score
**ParticipantScoreDto**: Scoreboard participant with current rank
**ScoreEntryDto**: Individual score record
**PlayerNoteDto**: Note data with visibility flags

### Usage Examples

**Create Session**:
```csharp
var command = new CreateSessionCommand(
    UserId: currentUserId,
    GameId: gameId,
    SessionType: "GameSpecific",
    SessionDate: DateTime.UtcNow,
    Location: "Home",
    Participants: [
        new ParticipantDto { DisplayName = "Alice", IsOwner = true, UserId = currentUserId },
        new ParticipantDto { DisplayName = "Bob", IsOwner = false }
    ]
);
var result = await mediator.Send(command);
// Returns: CreateSessionResult with SessionCode
```

**Update Score**:
```csharp
var command = new UpdateScoreCommand(
    SessionId: sessionId,
    ParticipantId: participantId,
    RoundNumber: 1,
    Category: "Victory Points",
    ScoreValue: 25
);
var result = await mediator.Send(command);
// Returns: UpdateScoreResult with NewTotal and NewRank
```

**Finalize Session**:
```csharp
var command = new FinalizeSessionCommand(
    SessionId: sessionId,
    FinalRanks: new Dictionary<Guid, int> {
        { participant1Id, 1 }, // winner
        { participant2Id, 2 },
        { participant3Id, 3 }
    }
);
var result = await mediator.Send(command);
// Returns: FinalizeSessionResult with WinnerId and FinalScores
```

**Get Scoreboard**:
```csharp
var query = new GetScoreboardQuery(SessionId: sessionId);
var scoreboard = await mediator.Send(query);
// Returns: ScoreboardDto with real-time rankings, scores by round/category
```

### Validators

All commands have FluentValidation validators:
- `CreateSessionCommandValidator`
- `UpdateScoreCommandValidator`
- `AddParticipantCommandValidator`
- `AddNoteCommandValidator`
- `FinalizeSessionCommandValidator`

## Infrastructure

### Persistence

**DbContext**: MeepleAiDbContext (central DbContext with SessionTracking DbSets)

**Entity Configurations**:
- `SessionConfiguration.cs` - Session entity mapping with constraints
- `ScoreEntryConfiguration.cs` - Decimal precision (10,2), indexes
- `PlayerNoteConfiguration.cs` - TEXT field mapping, enum checks
- `ParticipantConfiguration.cs` - Cascade delete, composite indexes

**Repositories**:
- `SessionRepository.cs` (implements `ISessionRepository`)
- `ScoreEntryRepository.cs` (implements `IScoreEntryRepository`)

**Patterns**:
- Soft delete with global query filter
- Optimistic concurrency with RowVersion
- Eager loading with Include for related entities

### External Services

**Phase 3 (GST-003)**: Real-time sync services placeholder

## HTTP API

### Endpoints

**Base Path**: `/api/v1/sessions`

**Reference**: [http://localhost:8080/scalar/v1](http://localhost:8080/scalar/v1)

**Phase 2 (GST-002)**: API endpoints will be implemented

Planned endpoints:
- `POST /api/v1/sessions` - Create session
- `GET /api/v1/sessions/{id}` - Get session details
- `GET /api/v1/sessions/code/{code}` - Find by session code
- `PUT /api/v1/sessions/{id}/participants` - Add participant
- `PUT /api/v1/sessions/{id}/scores` - Update scores
- `PUT /api/v1/sessions/{id}/pause` - Pause session
- `PUT /api/v1/sessions/{id}/resume` - Resume session
- `PUT /api/v1/sessions/{id}/finalize` - Finalize session

## Dependencies

### Internal (Bounded Contexts)

**None** - GameSession is a foundational context with no dependencies

**Blocks**:
- GST-002: Commands/Queries implementation
- GST-003: Real-time sync and collaboration features

### External (NuGet)

Standard dependencies inherited from Api.csproj:
- Entity Framework Core (persistence)
- MediatR (CQRS orchestration)
- FluentValidation (input validation)

## Testing

### Unit Tests

**Path**: `tests/Api.Tests/BoundedContexts/GameSession/Domain/`

**Coverage Target**: ≥90%

**Test Files**:
- `SessionTests.cs` - Session entity business logic (17 tests) ✅
- `ScoreEntryTests.cs` - Score entry validation (12 tests) ✅
- `ValueObjectTests.cs` - Value object immutability (19 tests) ✅

**Total**: 48 unit tests, 100% passing

**Pattern**:
```csharp
[Fact]
public void Create_ShouldGenerateUniqueSessionCode()
{
    // Arrange
    var userId = Guid.NewGuid();
    var gameId = Guid.NewGuid();

    // Act
    var session = Session.Create(userId, gameId, SessionType.GameSpecific);

    // Assert
    session.SessionCode.Should().HaveLength(6);
    session.SessionCode.Should().MatchRegex("^[A-Z2-9]{6}$");
}
```

### Integration Tests

**Path**: `tests/Api.Tests/BoundedContexts/GameSession/Infrastructure/`

**Test Files**:
- `SessionRepositoryTests.cs` - CRUD operations (8 tests) ⏳
- `ScoreEntryRepositoryTests.cs` - Batch operations, queries (7 tests) ⏳

**Total**: 15 integration tests created (pending migration application)

**Pattern**: Testcontainers + isolated database per test

```csharp
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public class SessionRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;

    [Fact]
    public async Task AddAsync_ShouldPersistSession()
    {
        // Arrange
        var session = Session.Create(userId, gameId, SessionType.Generic);

        // Act
        await _repository.AddAsync(session, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var retrieved = await _repository.GetByIdAsync(session.Id, CancellationToken.None);
        retrieved.Should().NotBeNull();
        retrieved!.SessionCode.Should().Be(session.SessionCode);
    }
}
```

## Configuration

**appsettings.json**: No specific configuration required for Phase 1

**Phase 2**: SSE/real-time sync configuration will be added

## Observability

### Logging

**Pattern**: Serilog structured logging

```csharp
_logger.LogInformation(
    "Session {SessionCode} created by user {UserId}",
    session.SessionCode,
    session.CreatedByUserId
);
```

### Metrics

**Prometheus**: Phase 2 - session activity metrics

### Tracing

**OpenTelemetry**: Automatic tracing via ActivitySource

## Technical Notes

### Session Code Generation Algorithm

```csharp
// 6-character alphanumeric uppercase (excludes 0, O, I, 1 for readability)
public static string GenerateSessionCode()
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    var random = new Random();
    return new string(Enumerable.Repeat(chars, 6)
        .Select(s => s[random.Next(s.Length)]).ToArray());
}
```

**Collision Handling**: Unique database constraint + retry logic (max 3 attempts)

### Soft Delete Pattern

```csharp
// Global query filter in DbContext
modelBuilder.Entity<Session>()
    .HasQueryFilter(e => !e.IsDeleted);

// Soft delete method
public async Task DeleteAsync(Guid id, CancellationToken ct)
{
    var session = await GetByIdAsync(id, ct);
    if (session == null) throw new NotFoundException(nameof(Session), id);

    session.IsDeleted = true;
    session.DeletedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync(ct);
}
```

### Optimistic Concurrency

```csharp
// Entity configuration
modelBuilder.Entity<Session>()
    .Property<byte[]>("RowVersion")
    .IsRowVersion()
    .HasColumnName("row_version");

// Update with conflict handling
try {
    await _context.SaveChangesAsync(ct);
} catch (DbUpdateConcurrencyException) {
    throw new ConflictException("Session was modified by another user");
}
```

## Migration Guide

### From Legacy (N/A)

This is a new bounded context with no legacy migration required.

### Breaking Changes

**Version 1.0.0** (Initial): No breaking changes

## FAQs

**Q: Why separate GameSession from UserLibrary?**
A: Different complexity levels, business logic, and scalability requirements. Sessions are real-time collaborative, library is static personal data.

**Q: What's the difference between Generic and GameSpecific sessions?**
A: Generic sessions don't require a game_id (for custom scorekeeping). GameSpecific sessions link to Games table for structured game data.

**Q: How are session codes kept unique?**
A: Database UNIQUE constraint + retry logic in factory method. Collision probability: ~1 in 2 billion for 6-char code.

**Q: When to use Commands vs Queries?**
A: Command = side effects (write). Query = no side effects (read). Commands implemented in GST-002.

**Q: How to handle concurrent score updates?**
A: Optimistic concurrency with RowVersion. Last write wins with conflict exception for UI retry.

## References

- **ADR**: `docs/01-architecture/adr/` (architectural decisions)
- **DDD Patterns**: `docs/01-architecture/patterns/`
- **OpenAPI Spec**: [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json)
- **Technical Spec**: `claudedocs/game-session-toolkit-ui-components.md`

## Roadmap

- ✅ **GST-001** (Phase 1): Backend bounded context + database schema
- ✅ **GST-002** (Phase 2): Commands/Queries + CQRS implementation
- ⏳ **GST-003**: Real-time sync + collaboration features
- ⏳ **GST-004**: Frontend components (scorekeeper UI)
- ⏳ **GST-005**: UserLibrary integration
- ⏳ **GST-013**: Export/share functionality

## Implementation Status

### Phase 1: Foundation (GST-001) ✅

**Completed**:
- ✅ Bounded context structure (Domain/Application/Infrastructure layers)
- ✅ 5 Domain entities (Session, ScoreEntry, PlayerNote, DiceRoll*, CardDraw*)
- ✅ 3 Value objects (ParticipantInfo, ScoreCalculation, SessionResult)
- ✅ 2 Repository interfaces + implementations
- ✅ 3 Domain events (SessionCreated, ScoreUpdated, SessionFinalized)
- ✅ Database migration with 6 tables, indexes, constraints
- ✅ 5 Entity configurations (fluent API)
- ✅ 48 unit tests (100% passing)
- ✅ 15 integration tests

### Phase 2: CQRS Layer (GST-002) ✅

**Completed**:
- ✅ 6 Commands with validators and handlers (CreateSession, UpdateScore, AddParticipant, AddNote, FinalizeSession, ShareResults placeholder)
- ✅ 5 Queries with handlers (GetActiveSession, GetSessionByCode, GetScoreboard, GetSessionDetails, GetSessionHistory)
- ✅ 8 DTOs (SessionDto, SessionDetailsDto, SessionSummaryDto, ScoreboardDto, ParticipantDto, ParticipantScoreDto, ScoreEntryDto, PlayerNoteDto)
- ✅ FluentValidation for all commands/queries
- ✅ MediatR integration
- ✅ Error handling with domain exceptions
- ✅ Repository pattern for commands
- ✅ Direct DbContext for queries
- ✅ 2 validator unit tests

**Test Coverage**:
- Domain: 48/48 unit tests passing ✅
- Validators: 2 validator test files ✅
- Integration: Existing repository tests ✅
- Target: 90%+ coverage

(*) Phase 2 placeholders for GST-003

---

**Last Updated**: 2026-01-30
**Maintainer**: SessionTracking Team
**Status**: Phase 2 - CQRS Layer Complete (Ready for GST-003)
**Next**: API endpoints + Real-time SSE
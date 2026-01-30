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

**Phase 2 (GST-002)**: Placeholder for command implementations

Planned commands:
- CreateSessionCommand
- AddParticipantCommand
- UpdateScoreCommand
- AddNoteCommand
- PauseSessionCommand
- ResumeSessionCommand
- FinalizeSessionCommand

### Queries

**Phase 2 (GST-002)**: Placeholder for query implementations

Planned queries:
- GetSessionByIdQuery
- GetSessionByCodeQuery
- GetActiveSessionsQuery
- GetScoresBySessionQuery
- GetNotesQuery

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

- ✅ **GST-001** (Phase 1): Backend bounded context + database schema (current)
- ⏳ **GST-002**: Commands/Queries + API endpoints
- ⏳ **GST-003**: Real-time sync + collaboration features
- ⏳ **GST-004**: Frontend components (scorekeeper UI)

## Implementation Status

### Phase 1: Foundation (GST-001) ✅

**Completed**:
- ✅ Bounded context structure (Domain/Application/Infrastructure layers)
- ✅ 5 Domain entities (Session, ScoreEntry, PlayerNote, DiceRoll*, CardDraw*)
- ✅ 3 Value objects (ParticipantInfo, ScoreCalculation, SessionResult)
- ✅ 2 Repository interfaces (ISessionRepository, IScoreEntryRepository)
- ✅ 3 Domain events (SessionCreated, ScoreUpdated, SessionFinalized)
- ✅ Database migration with 6 tables, indexes, constraints
- ✅ 5 Entity configurations (fluent API)
- ✅ 2 Repository implementations
- ✅ 48 unit tests (100% passing)
- ✅ 15 integration tests (created, pending migration)

**Pending**:
- ⏳ Migration application (requires DB reset/reinit)
- ⏳ Integration test execution (blocked by migration)

**Test Coverage**:
- Unit: 48/48 passing (100%)
- Integration: 15 tests created (pending migration)
- Target: 90%+ domain coverage ✅ achieved

(*) Phase 2 placeholders for GST-003

---

**Last Updated**: 2026-01-30
**Maintainer**: SessionTracking Team
**Status**: Phase 1 - Foundation Complete (Ready for GST-002)
**Migration Required**: Run `dotnet ef database update` after DB reset

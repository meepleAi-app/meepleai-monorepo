# Play Sessions - Implementation Guide

## Overview

This document provides implementation guidance for the Play Sessions feature development across 5 sub-issues.

**Epic**: #3874
**Bounded Context**: GameManagement
**Estimated Effort**: 6 days (1.2 weeks)

---

## Implementation Roadmap

### Phase 1: Foundation (Day 1) - Issue #3875

**Objective**: Establish domain model, database schema, and foundational tests.

#### Tasks
1. Create domain entities and value objects
2. Implement aggregate behaviors
3. Design database schema
4. Create EF Core migration
5. Write unit tests for PlaySession aggregate

#### Deliverables
- `PlaySession.cs` - Aggregate root with factory methods and behaviors
- `SessionPlayer.cs` - Owned entity for player management
- `SessionScore.cs` - Value object for multi-dimensional scoring
- `SessionScoringConfig.cs` - Value object for scoring configuration
- `PlaySessionStatus.cs`, `PlaySessionVisibility.cs` - Enumerations
- Migration: `YYYYMMDDHHMMSS_AddPlaySessions.cs`
- Tests: `PlaySessionTests.cs` (≥90% coverage)

#### Key Design Patterns
- **Hybrid Player Model**: User reference + external guests
- **Multi-Dimensional Scoring**: Flexible SessionScore value objects
- **Optional Game Association**: Nullable FK with free-form fallback
- **Editable Post-Completion**: Allow corrections after session completes

#### Acceptance Criteria
- ✅ Domain model compiles without errors
- ✅ Migration applies successfully to dev database
- ✅ All aggregate behaviors have unit test coverage
- ✅ Tests verify business rules (no duplicate players, state transitions)

---

### Phase 2: Commands (Days 2-3) - Issue #3876

**Objective**: Implement CQRS commands with validation and event handling.

#### Commands to Implement

1. **CreatePlaySessionCommand**
   - Validates: GameName required, SessionDate not in future
   - Creates: New PlaySession aggregate
   - Events: PlaySessionCreatedEvent

2. **AddPlayerToSessionCommand**
   - Validates: DisplayName required, no duplicate UserId
   - Modifies: Adds SessionPlayer to aggregate
   - Events: PlayerAddedToSessionEvent

3. **RecordSessionScoreCommand**
   - Validates: PlayerId exists, Dimension in ScoringConfig
   - Modifies: Adds/updates SessionScore
   - Events: SessionScoreRecordedEvent

4. **StartPlaySessionCommand**
   - Validates: Status is Planned
   - Modifies: Status → InProgress, sets StartTime
   - Events: PlaySessionStartedEvent

5. **CompletePlaySessionCommand**
   - Validates: Session not already Completed
   - Modifies: Status → Completed, calculates/sets Duration
   - Events: PlaySessionCompletedEvent

6. **UpdatePlaySessionCommand**
   - Validates: User is creator
   - Modifies: SessionDate, Notes, Location
   - Events: PlaySessionUpdatedEvent

#### Deliverables
- Commands: 6 command classes with records
- Validators: 6 FluentValidation validators
- Handlers: 6 command handlers with repository integration
- Tests: Integration tests with Testcontainers (≥90% coverage)

#### Key Patterns
- **CQRS**: Commands modify state, queries read state
- **Domain Events**: Raised on state changes, dispatched after SaveChanges
- **FluentValidation**: Declarative validation rules
- **Repository Pattern**: Domain layer interfaces, Infrastructure implementation

#### Acceptance Criteria
- ✅ All commands have validators with comprehensive rules
- ✅ Handlers persist changes and raise domain events
- ✅ Integration tests verify end-to-end command execution
- ✅ Event dispatching works correctly

---

### Phase 3: Queries (Day 4) - Issue #3877

**Objective**: Implement CQRS queries for session retrieval and statistics.

#### Queries to Implement

1. **GetPlaySessionQuery**
   - Returns: Full session with players and scores
   - Includes: Game details, creator info, scoring config

2. **GetUserPlayHistoryQuery**
   - Returns: Paginated list of user's sessions
   - Filters: Optional gameId, date range
   - Sorting: SessionDate descending

3. **GetPlayerStatisticsQuery**
   - Returns: Cross-game stats (MVP requirement)
   - Includes: Total sessions, wins, play counts, avg scores
   - Filters: Optional date range

#### DTOs

```csharp
PlaySessionDto
├─ SessionPlayerDto (List)
│  └─ SessionScoreDto (List)
└─ SessionScoringConfig

PlaySessionSummaryDto (for history)

PlayerStatisticsDto
├─ GamePlayCounts (Dictionary<string, int>)
└─ AverageScoresByGame (Dictionary<string, double>)
```

#### Deliverables
- Queries: 3 query classes
- DTOs: 5 DTO classes with mapping logic
- Handlers: 3 query handlers with optimized queries
- Tests: Integration tests with test data setup

#### Key Patterns
- **Read Models**: Optimized DTOs for query responses
- **Projection**: EF Core `Select()` for performance
- **Pagination**: `PagedResult<T>` wrapper
- **Statistics Aggregation**: Group by, Sum, Average queries

#### Acceptance Criteria
- ✅ Queries return correct data matching test scenarios
- ✅ Pagination works correctly with expected page sizes
- ✅ Statistics calculations are accurate
- ✅ Query performance is acceptable (<500ms for history)

---

### Phase 4: Security (Day 5) - Issue #3878

**Objective**: Implement permission system and authorization middleware.

#### Permission Checker

**PlaySessionPermissionChecker.cs**
- `CanViewSessionAsync(Guid userId, PlaySession session)`
  - Creator: ✅
  - Group member (if Group visibility): ✅
  - Player in session: ✅
  - Others: ❌

- `CanEditSessionAsync(Guid userId, PlaySession session)`
  - Creator: ✅
  - Others: ❌

#### Middleware Integration

Apply authorization checks to all endpoints:
- GET endpoints: `CanViewSessionAsync`
- PUT/POST/DELETE endpoints: `CanEditSessionAsync`

#### Deliverables
- `PlaySessionPermissionChecker.cs` - Permission logic
- `PlaySessionAuthorizationMiddleware.cs` - HTTP middleware
- Tests: Authorization integration tests with various user scenarios

#### Key Patterns
- **Permission Checker Pattern**: Follows PrivateGames pattern (#3570-#3580)
- **Middleware Authorization**: Apply before handler execution
- **Group Integration**: Leverage existing Group repository

#### Acceptance Criteria
- ✅ Unauthorized users receive 403 Forbidden
- ✅ Group members can view group sessions
- ✅ Players can view their sessions
- ✅ Only creators can edit sessions
- ✅ Tests cover all permission scenarios

---

### Phase 5: Frontend UI (Days 6-7) - Issue #3879

**Objective**: Build user-facing interface for session management.

#### UI Components

1. **Session Creation Form** (`SessionCreateForm.tsx`)
   - Game selection (catalog or free-form)
   - Date/time picker
   - Visibility selector (Private/Group)
   - Scoring configuration

2. **Player Management** (`PlayerManager.tsx`)
   - Add/remove players
   - User search for registered players
   - Guest player input
   - Player list display

3. **Scoring Interface** (`ScoringInterface.tsx`)
   - Dynamic score input per dimension
   - Multi-dimensional score grid
   - Real-time validation

4. **History View** (`PlayHistory.tsx`)
   - Paginated session list
   - Game filter
   - Date range filter
   - Session details modal

5. **Statistics Dashboard** (`PlayerStatistics.tsx`)
   - Total sessions count
   - Win rate visualization
   - Game play counts (chart)
   - Average scores per game

#### Pages

- `/sessions` - Play history list
- `/sessions/new` - Create new session
- `/sessions/[id]` - Session details
- `/sessions/[id]/edit` - Edit session (creator only)
- `/profile/statistics` - Player statistics

#### API Integration

**services/play-sessions.api.ts**
```typescript
export const playSessionsApi = {
  createSession: (data: CreateSessionRequest) => Promise<string>,
  addPlayer: (sessionId: string, player: AddPlayerRequest) => Promise<void>,
  recordScore: (sessionId: string, score: ScoreRequest) => Promise<void>,
  startSession: (sessionId: string) => Promise<void>,
  completeSession: (sessionId: string, duration?: string) => Promise<void>,
  getSession: (sessionId: string) => Promise<PlaySessionDto>,
  getUserHistory: (params: HistoryParams) => Promise<PagedResult<SessionSummary>>,
  getStatistics: (userId: string, params?: StatParams) => Promise<PlayerStats>
}
```

#### State Management

**stores/play-sessions.store.ts** (Zustand)
```typescript
interface PlaySessionStore {
  currentSession: PlaySessionDto | null;
  history: SessionSummary[];
  statistics: PlayerStats | null;

  createSession: (data: CreateSessionRequest) => Promise<string>;
  loadSession: (id: string) => Promise<void>;
  loadHistory: (params: HistoryParams) => Promise<void>;
  loadStatistics: (userId: string) => Promise<void>;
}
```

#### Deliverables
- Components: 5 core components with TypeScript types
- Pages: 4 Next.js pages with proper routing
- API client: Typed service layer with React Query hooks
- Store: Zustand store with async actions
- Tests: Component tests (Vitest) + E2E tests (Playwright)

#### Key Patterns
- **MeepleCard Component**: Use for session display cards (entity="session")
- **Form Validation**: React Hook Form + Zod schemas
- **Optimistic Updates**: Update UI before server confirmation
- **Error Boundaries**: Graceful error handling

#### Acceptance Criteria
- ✅ Users can create sessions (catalog and free-form games)
- ✅ Players can be added (users and guests)
- ✅ Scores can be recorded with multiple dimensions
- ✅ Sessions can be started and completed
- ✅ History displays paginated sessions
- ✅ Statistics show cross-game data
- ✅ UI matches design system (shadcn/ui + Tailwind)
- ✅ E2E tests cover critical flows

---

## Testing Strategy

### Unit Tests (Backend)
- **Domain Logic**: PlaySession aggregate behaviors
- **Validators**: FluentValidation rules
- **Target Coverage**: ≥90%

### Integration Tests (Backend)
- **Command Handlers**: Database persistence and events
- **Query Handlers**: Data retrieval accuracy
- **Authorization**: Permission checker scenarios
- **Infrastructure**: Testcontainers for PostgreSQL

### Component Tests (Frontend)
- **Forms**: Validation and submission
- **Components**: Rendering and interactions
- **Framework**: Vitest + React Testing Library

### E2E Tests (Frontend)
- **Critical Flows**: Session creation → player add → score → complete
- **Framework**: Playwright
- **Target**: 85% critical path coverage

---

## Performance Considerations

### Backend Optimizations
- **Indexes**: game_id, user_id, session_date, status
- **Query Optimization**: Use `Select()` projections for DTOs
- **Pagination**: Limit query result sets
- **Caching**: Consider HybridCache for statistics queries

### Frontend Optimizations
- **Data Fetching**: React Query for caching and deduplication
- **Lazy Loading**: Code-split pages and components
- **Optimistic Updates**: Immediate UI feedback
- **Pagination**: Virtual scrolling for large history lists

---

## Migration Strategy

### Database Migration
```bash
cd apps/api/src/Api
dotnet ef migrations add AddPlaySessions
dotnet ef database update
```

### Data Seeding (Optional)
- Sample sessions for testing
- Guest players for examples
- Multi-dimensional scores showcase

---

## Rollout Plan

### Stage 1: Backend Foundation (Issues #3875-#3878)
1. Merge domain model and schema (#3875)
2. Deploy commands and queries (#3876-#3877)
3. Enable authorization (#3878)
4. Test in dev environment

### Stage 2: Frontend Development (Issue #3879)
1. Build core components
2. Integrate with API
3. Test user flows
4. Deploy to staging

### Stage 3: Production Release
1. Run final E2E tests
2. Monitor performance
3. Gather user feedback
4. Iterate based on analytics

---

## Success Metrics

### Functionality
- ✅ Users can create and manage play sessions
- ✅ Multi-dimensional scoring works correctly
- ✅ Cross-game statistics are accurate
- ✅ Permissions prevent unauthorized access

### Quality
- ✅ Backend test coverage ≥90%
- ✅ Frontend test coverage ≥85%
- ✅ Zero critical bugs in production
- ✅ API response times <500ms (p95)

### User Adoption
- Target: 50% of active users create ≥1 session in first month
- Target: Average 3 sessions per week per active user

---

## Related Documentation

- [Play Sessions API Docs](../../03-api/bounded-contexts/game-management/play-sessions.md)
- [CQRS Pattern](../../01-architecture/patterns/cqrs.md)
- [Testing Standards](../../05-testing/backend/backend-testing-patterns.md)
- [Frontend Guidelines](../coding-standards.md)

---

**Last Updated**: 2024-02-08
**Status**: Implementation Ready

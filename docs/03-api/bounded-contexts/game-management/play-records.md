# Play Records API Documentation

## Overview

The Play Records feature enables users to record and track their board game play history with flexible player management, multi-dimensional scoring, and comprehensive statistics.

**Bounded Context**: GameManagement
**Parent Epic**: #3887
**Implementation Status**: Planned

**Note**: This is distinct from `GameSession` (real-time AI agent play state). `PlayRecord` is for historical play logging.

---

## Domain Model

### PlayRecord (Aggregate Root)

The core entity representing a single game play session.

#### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Unique session identifier |
| `GameId` | `Guid?` | Optional reference to Game in catalog |
| `GameName` | `string` | Game name (from catalog or user input) |
| `CreatedByUserId` | `Guid` | Session creator |
| `Visibility` | `PlayRecordVisibility` | Private or Group visibility |
| `GroupId` | `Guid?` | Optional group for shared sessions |
| `SessionDate` | `DateTime` | Date of play session |
| `StartTime` | `DateTime?` | Optional session start time |
| `EndTime` | `DateTime?` | Optional session end time |
| `Duration` | `TimeSpan?` | Calculated or manual duration |
| `Status` | `PlayRecordStatus` | Planned, InProgress, Completed, Archived |
| `Notes` | `string?` | Optional session notes |
| `Location` | `string?` | Optional play location |
| `Players` | `List<SessionPlayer>` | Participants in session |
| `ScoringConfig` | `SessionScoringConfig` | Scoring configuration |

#### Factory Methods

```csharp
// Create session with catalog game
public static PlayRecord CreateWithGame(
    Guid gameId,
    string gameName,
    Guid userId,
    DateTime sessionDate,
    PlayRecordVisibility visibility,
    SessionScoringConfig? scoringConfig = null)

// Create session without catalog game (free-form)
public static PlayRecord CreateFreeForm(
    string gameName,
    Guid userId,
    DateTime sessionDate,
    PlayRecordVisibility visibility,
    SessionScoringConfig scoringConfig)
```

#### Behaviors

```csharp
// Player management
void AddPlayer(Guid? userId, string displayName)

// Scoring
void RecordScore(Guid playerId, SessionScore score)

// Lifecycle
void StartSession()
void CompleteSession(TimeSpan? manualDuration = null)
void UpdateDetails(DateTime? sessionDate, string? notes, string? location)
```

---

### SessionPlayer (Entity)

Represents a participant in a play session (registered user or guest).

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Player identifier within session |
| `UserId` | `Guid?` | Optional User reference (null for guests) |
| `DisplayName` | `string` | Player display name |
| `Scores` | `List<SessionScore>` | Multi-dimensional scores |

---

### SessionScore (Value Object)

Multi-dimensional score representation.

| Property | Type | Description |
|----------|------|-------------|
| `Dimension` | `string` | Score dimension (e.g., "points", "ranking", "wins") |
| `Value` | `int` | Score value |
| `Unit` | `string?` | Optional display unit (e.g., "pts", "1st", "W") |

**Factory Methods**:
```csharp
SessionScore.Points(int points)      // Standard points scoring
SessionScore.Ranking(int rank)       // Position-based (1st, 2nd, 3rd)
SessionScore.Wins(int wins)          // Win/loss tracking
```

---

### SessionScoringConfig (Value Object)

Configuration for scoring dimensions available in the session.

| Property | Type | Description |
|----------|------|-------------|
| `EnabledDimensions` | `List<string>` | Active scoring dimensions |
| `DimensionUnits` | `Dictionary<string, string>` | Display units per dimension |

**Factory Methods**:
```csharp
SessionScoringConfig.CreateDefault()           // Single "points" dimension
SessionScoringConfig.CreateFromGame(Game game) // Extract from game metadata
```

---

### Enumerations

#### PlayRecordStatus
- `Planned` - Session scheduled but not started
- `InProgress` - Session currently active
- `Completed` - Session finished
- `Archived` - Completed session archived

#### PlayRecordVisibility
- `Private` - Only creator can view
- `Group` - Visible to group members

---

## Database Schema

### PlayRecords Table

```sql
CREATE TABLE PlayRecords (
    Id UUID PRIMARY KEY,
    GameId UUID NULL,                    -- FK to Games (nullable)
    GameName VARCHAR(255) NOT NULL,
    CreatedByUserId UUID NOT NULL,
    Visibility INT NOT NULL,             -- 0=Private, 1=Group
    GroupId UUID NULL,

    SessionDate TIMESTAMP NOT NULL,
    StartTime TIMESTAMP NULL,
    EndTime TIMESTAMP NULL,
    Duration INTERVAL NULL,
    Status INT NOT NULL,
    Notes TEXT NULL,
    Location VARCHAR(255) NULL,

    ScoringConfigJson JSONB NOT NULL,    -- SessionScoringConfig serialized

    CreatedAt TIMESTAMP NOT NULL,
    UpdatedAt TIMESTAMP NOT NULL,

    FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE SET NULL,
    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    FOREIGN KEY (GroupId) REFERENCES Groups(Id) ON DELETE CASCADE
);
```

**Indexes**:
```sql
CREATE INDEX idx_playsessions_game ON PlayRecords(GameId) WHERE GameId IS NOT NULL;
CREATE INDEX idx_playsessions_user ON PlayRecords(CreatedByUserId);
CREATE INDEX idx_playsessions_date ON PlayRecords(SessionDate DESC);
CREATE INDEX idx_playsessions_status ON PlayRecords(Status);
```

### SessionPlayers Table

```sql
CREATE TABLE SessionPlayers (
    Id UUID PRIMARY KEY,
    PlayRecordId UUID NOT NULL,
    UserId UUID NULL,                    -- FK to Users (nullable for guests)
    DisplayName VARCHAR(255) NOT NULL,

    FOREIGN KEY (PlayRecordId) REFERENCES PlayRecords(Id) ON DELETE CASCADE,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX idx_sessionplayers_user ON SessionPlayers(UserId) WHERE UserId IS NOT NULL;
```

### SessionScores Table

```sql
CREATE TABLE SessionScores (
    Id UUID PRIMARY KEY,
    SessionPlayerId UUID NOT NULL,
    Dimension VARCHAR(50) NOT NULL,
    Value INT NOT NULL,
    Unit VARCHAR(20) NULL,

    FOREIGN KEY (SessionPlayerId) REFERENCES SessionPlayers(Id) ON DELETE CASCADE,
    UNIQUE (SessionPlayerId, Dimension)  -- One score per dimension per player
);
```

---

## CQRS Operations

### Commands

#### CreatePlayRecordCommand

Creates a new play session.

**Request**:
```csharp
{
    "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // Optional
    "gameName": "Catan",
    "sessionDate": "2024-02-08T19:00:00Z",
    "visibility": 0,                                    // 0=Private, 1=Group
    "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", // Optional
    "scoringConfig": {
        "enabledDimensions": ["points"],
        "dimensionUnits": {
            "points": "pts"
        }
    }
}
```

**Response**: `Guid` (Session ID)

**Endpoint**: `POST /api/v1/game-management/play-sessions`

---

#### AddPlayerToSessionCommand

Adds a player to an existing session.

**Request**:
```csharp
{
    "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // Optional (null for guests)
    "displayName": "Alice"
}
```

**Endpoint**: `POST /api/v1/game-management/play-sessions/{sessionId}/players`

---

#### RecordSessionScoreCommand

Records a score for a player in a session.

**Request**:
```csharp
{
    "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "playerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "dimension": "points",
    "value": 42,
    "unit": "pts"
}
```

**Endpoint**: `POST /api/v1/game-management/play-sessions/{sessionId}/scores`

---

#### StartPlayRecordCommand

Marks a session as in-progress.

**Endpoint**: `POST /api/v1/game-management/play-sessions/{sessionId}/start`

---

#### CompletePlayRecordCommand

Completes a session and calculates duration.

**Request**:
```csharp
{
    "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "manualDuration": "02:30:00"  // Optional: override calculated duration
}
```

**Endpoint**: `POST /api/v1/game-management/play-sessions/{sessionId}/complete`

---

#### UpdatePlayRecordCommand

Updates session details (allowed post-completion).

**Request**:
```csharp
{
    "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "sessionDate": "2024-02-08T19:00:00Z",  // Optional
    "notes": "Great game!",                  // Optional
    "location": "Bob's house"                // Optional
}
```

**Endpoint**: `PUT /api/v1/game-management/play-sessions/{sessionId}`

---

### Queries

#### GetPlayRecordQuery

Retrieves full session details including players and scores.

**Request**: `GET /api/v1/game-management/play-sessions/{sessionId}`

**Response**:
```json
{
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "gameName": "Catan",
    "sessionDate": "2024-02-08T19:00:00Z",
    "duration": "02:30:00",
    "status": 2,
    "players": [
        {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "displayName": "Alice",
            "scores": [
                {
                    "dimension": "points",
                    "value": 42,
                    "unit": "pts"
                }
            ]
        }
    ],
    "scoringConfig": {
        "enabledDimensions": ["points"],
        "dimensionUnits": {
            "points": "pts"
        }
    }
}
```

---

#### GetUserPlayHistoryQuery

Retrieves paginated play history for a user.

**Request**: `GET /api/v1/game-management/play-sessions/history?userId={userId}&page=1&pageSize=20&gameId={gameId}`

**Query Parameters**:
- `userId` (required): User ID
- `page` (optional, default: 1): Page number
- `pageSize` (optional, default: 20): Results per page
- `gameId` (optional): Filter by specific game

**Response**:
```json
{
    "items": [
        {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "gameName": "Catan",
            "sessionDate": "2024-02-08T19:00:00Z",
            "duration": "02:30:00",
            "playerCount": 4,
            "myScore": 42
        }
    ],
    "totalCount": 100,
    "page": 1,
    "pageSize": 20,
    "totalPages": 5
}
```

---

#### GetPlayerStatisticsQuery

Retrieves cross-game statistics for a player.

**Request**: `GET /api/v1/game-management/play-sessions/statistics?userId={userId}&startDate={startDate}&endDate={endDate}`

**Query Parameters**:
- `userId` (required): User ID
- `startDate` (optional): Filter start date
- `endDate` (optional): Filter end date

**Response**:
```json
{
    "totalSessions": 42,
    "totalWins": 15,
    "gamePlayCounts": {
        "Catan": 10,
        "Ticket to Ride": 8,
        "Pandemic": 5
    },
    "averageScoresByGame": {
        "Catan": 38.5,
        "Ticket to Ride": 102.3
    }
}
```

---

## Permission System

### PlayRecordPermissionChecker

Authorization logic for play sessions.

#### View Permissions

Users can view a session if:
1. They are the session creator
2. Session is `Group` visibility AND user is in the group
3. User is a player in the session

```csharp
public async Task<bool> CanViewSessionAsync(Guid userId, PlayRecord session)
{
    // Creator always can view
    if (session.CreatedByUserId == userId)
        return true;

    // Group members can view group sessions
    if (session.Visibility == PlayRecordVisibility.Group
        && session.GroupId.HasValue)
    {
        return await _groupRepository.IsUserInGroupAsync(
            userId,
            session.GroupId.Value);
    }

    // Check if user is a player
    return session.Players.Any(p => p.UserId == userId);
}
```

#### Edit Permissions

Only the session creator can edit sessions.

```csharp
public async Task<bool> CanEditSessionAsync(Guid userId, PlayRecord session)
{
    return session.CreatedByUserId == userId;
}
```

---

## Domain Events

### PlayRecordCreatedEvent
- **Trigger**: New session created
- **Consumers**: Analytics, notifications

### PlayerAddedToSessionEvent
- **Trigger**: Player joins session
- **Consumers**: Session tracking, notifications

### SessionScoreRecordedEvent
- **Trigger**: Score recorded for player
- **Consumers**: Leaderboard updates, statistics

### PlayRecordStartedEvent
- **Trigger**: Session marked as in-progress
- **Consumers**: Activity tracking

### PlayRecordCompletedEvent
- **Trigger**: Session completed
- **Consumers**: Statistics updates, achievements, notifications

### PlayRecordUpdatedEvent
- **Trigger**: Session details modified
- **Consumers**: Audit log

---

## Validation Rules

### CreatePlayRecordCommand
- `GameName` is required (max 255 characters)
- `SessionDate` cannot be in the future
- If `GameId` provided, must exist in catalog
- If `Visibility` is `Group`, `GroupId` is required

### AddPlayerToSessionCommand
- `DisplayName` is required (max 255 characters)
- Cannot add duplicate players (same UserId)
- Session must be in `Planned` or `InProgress` status

### RecordSessionScoreCommand
- `PlayerId` must exist in session
- `Dimension` must be in session's `ScoringConfig.EnabledDimensions`
- `Value` must be non-negative for most dimensions

### CompletePlayRecordCommand
- Session must not already be `Completed`
- If `ManualDuration` not provided, `StartTime` must be set

---

## Error Responses

| Status Code | Error | Description |
|-------------|-------|-------------|
| 400 | `ValidationError` | Invalid request data |
| 403 | `ForbiddenError` | Insufficient permissions |
| 404 | `NotFoundError` | Session/Game/Player not found |
| 409 | `ConflictError` | Duplicate player, invalid state transition |

**Example Error Response**:
```json
{
    "error": "ValidationError",
    "message": "GameName is required",
    "details": {
        "field": "gameName",
        "code": "REQUIRED"
    }
}
```

---

## Usage Examples

### Creating a Session with Catalog Game

```http
POST /api/v1/game-management/play-sessions
Authorization: Bearer {token}
Content-Type: application/json

{
    "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "gameName": "Catan",
    "sessionDate": "2024-02-08T19:00:00Z",
    "visibility": 0
}
```

### Creating a Free-Form Session

```http
POST /api/v1/game-management/play-sessions
Authorization: Bearer {token}
Content-Type: application/json

{
    "gameName": "Poker",
    "sessionDate": "2024-02-08T20:00:00Z",
    "visibility": 0,
    "scoringConfig": {
        "enabledDimensions": ["wins", "chips"],
        "dimensionUnits": {
            "wins": "W",
            "chips": "$"
        }
    }
}
```

### Recording a Complete Game Flow

```http
# 1. Create session
POST /api/v1/game-management/play-sessions
{
    "gameName": "Catan",
    "sessionDate": "2024-02-08T19:00:00Z",
    "visibility": 0
}
# Response: { "sessionId": "abc-123" }

# 2. Add players
POST /api/v1/game-management/play-sessions/abc-123/players
{
    "userId": "user-1",
    "displayName": "Alice"
}

POST /api/v1/game-management/play-sessions/abc-123/players
{
    "displayName": "Bob"  # Guest player (no userId)
}

# 3. Start session
POST /api/v1/game-management/play-sessions/abc-123/start

# 4. Record scores
POST /api/v1/game-management/play-sessions/abc-123/scores
{
    "playerId": "player-1",
    "dimension": "points",
    "value": 42
}

# 5. Complete session
POST /api/v1/game-management/play-sessions/abc-123/complete
```

---

## Related Documentation

- [GameManagement Bounded Context](./game-management.md)
- [Permission System](../../05-testing/authorization.md)
- [CQRS Pattern](../../01-architecture/patterns/cqrs.md)
- [Domain Events](../../01-architecture/patterns/domain-events.md)

---

## Implementation Status

- [ ] Domain Model (#3875)
- [ ] CQRS Commands (#3876)
- [ ] CQRS Queries (#3877)
- [ ] Permission System (#3878)
- [ ] Frontend UI (#3879)

**Parent Epic**: #3874
**Target Release**: TBD

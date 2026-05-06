# Sprint 2 — API Contracts (#211)

> Defines endpoint signatures, request/response schemas, and error codes for Sprint 2 features.
> All endpoints follow MeepleAI CQRS patterns (MediatR only, no direct service injection).

---

## Table of Contents

1. [Existing Endpoints (Sprint 2 Depends On)](#existing-endpoints-sprint-2-depends-on)
2. [QuickView Endpoints (#214)](#quickview-endpoints-214)
3. [Responsive Matrix (#215)](#responsive-matrix-215)
4. [Server-Side Timer Endpoints (#216)](#server-side-timer-endpoints-216)
5. [Common Types](#common-types)
6. [Error Responses](#error-responses)
7. [Authorization](#authorization)

---

## Existing Endpoints (Sprint 2 Depends On)

Sprint 2 features build on top of the existing Game Night and Live Session infrastructure.

### Game Night Endpoints (`/api/v1/game-nights`)

| Method | Path | Description | Status Codes |
|--------|------|-------------|-------------|
| `POST` | `/game-nights/` | Create game night | 201, 400, 401 |
| `GET` | `/game-nights/` | Get upcoming game nights | 200, 401 |
| `GET` | `/game-nights/mine` | Get my game nights | 200, 401 |
| `GET` | `/game-nights/{id}` | Get game night by ID | 200, 401, 404 |
| `PUT` | `/game-nights/{id}` | Update game night | 204, 400, 401, 404 |
| `POST` | `/game-nights/{id}/publish` | Publish game night | 204, 401, 404, 409 |
| `POST` | `/game-nights/{id}/cancel` | Cancel game night | 204, 401, 404, 409 |
| `POST` | `/game-nights/{id}/invite` | Invite users | 204, 401, 404, 409 |
| `GET` | `/game-nights/{id}/rsvps` | Get RSVPs | 200, 401, 404 |
| `POST` | `/game-nights/{id}/rsvp` | Respond to invitation | 204, 400, 401, 404 |

### Live Session Endpoints (`/api/v1/live-sessions`)

| Method | Path | Description | Status Codes |
|--------|------|-------------|-------------|
| `POST` | `/live-sessions` | Create session | 201, 400, 401 |
| `POST` | `/live-sessions/{sessionId}/start` | Start session | 204, 404, 409 |
| `POST` | `/live-sessions/{sessionId}/pause` | Pause session | 204, 404, 409 |
| `POST` | `/live-sessions/{sessionId}/resume` | Resume session | 204, 404, 409 |
| `POST` | `/live-sessions/{sessionId}/complete` | Complete session | 204, 404, 409 |
| `POST` | `/live-sessions/{sessionId}/players` | Add player | 201, 400, 404 |
| `DELETE` | `/live-sessions/{sessionId}/players/{playerId}` | Remove player | 204, 404, 409 |
| `POST` | `/live-sessions/{sessionId}/scores` | Record score | 204, 400, 404 |
| `GET` | `/live-sessions/{sessionId}` | Get session details | 200, 404 |
| `GET` | `/live-sessions/{sessionId}/scores` | Get scores | 200, 404 |
| `GET` | `/live-sessions/{sessionId}/players` | Get players | 200, 404 |

---

## QuickView Endpoints (#214)

### `GET /api/v1/games/{gameId}/quick-view`

Returns a game summary with dual context: shared catalog data merged with user library context (if available).

**Authorization**: Authenticated user (session required)

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `gameId` | `Guid` | Shared catalog game ID |

**Response** `200 OK`:

```typescript
interface QuickViewResponse {
  game: GameSummaryDto;
  sharedContext: SharedGameContextDto | null;
  userContext: UserGameContextDto | null;
}

interface GameSummaryDto {
  id: string;                    // Guid
  name: string;
  description: string | null;
  imageUrl: string | null;
  yearPublished: number | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  playingTimeMinutes: number | null;
  bggId: number | null;
}

interface SharedGameContextDto {
  averageRating: number | null;   // Community average (1-10 scale)
  totalRatings: number;
  publisher: string | null;
  categories: string[];
  mechanics: string[];
  weight: number | null;          // BGG complexity weight (1-5)
}

interface UserGameContextDto {
  libraryEntryId: string;         // Guid
  playCount: number;
  lastPlayedAt: string | null;    // ISO 8601 datetime
  personalRating: number | null;  // User's own rating (1-10)
  notes: string | null;
  addedToLibraryAt: string;       // ISO 8601 datetime
}
```

**Error Responses**:

| Code | Condition |
|------|-----------|
| 401 | Not authenticated |
| 404 | Game not found in shared catalog |

---

### `GET /api/v1/library/games/{gameId}/quick-view`

Returns a user-specific quick view with play history, oriented from the user library perspective. Used when the user navigates from their library rather than the shared catalog.

**Authorization**: Authenticated user (session required)

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `gameId` | `Guid` | Game ID (shared catalog ID or private game ID) |

**Response** `200 OK`:

```typescript
interface LibraryQuickViewResponse {
  game: GameSummaryDto;
  userContext: UserGameContextDto;         // Always present (user owns it)
  sharedContext: SharedGameContextDto | null; // null for private games
  recentPlays: RecentPlayDto[];
  isPrivateGame: boolean;
}

interface RecentPlayDto {
  playedAt: string;               // ISO 8601 datetime
  duration: number | null;        // minutes
  playerCount: number;
  winner: string | null;          // display name
  notes: string | null;
}
```

**Error Responses**:

| Code | Condition |
|------|-----------|
| 401 | Not authenticated |
| 404 | Game not in user's library |

---

## Responsive Matrix (#215)

No new API endpoints are introduced for the responsive matrix. This section documents which existing endpoints are consumed at each viewport breakpoint and what component behavior changes.

### Breakpoint Definitions

| Breakpoint | Width Range | Layout |
|------------|-------------|--------|
| Desktop | >= 1024px | 3-column layout |
| Tablet | 768px - 1023px | 2-column layout |
| Mobile | < 768px | Single-column stacked |

### Endpoint Consumption by Breakpoint

| Endpoint | Desktop | Tablet | Mobile | Notes |
|----------|---------|--------|--------|-------|
| `GET /game-nights/{id}` | Full detail | Full detail | Full detail | Same data, different layout |
| `GET /game-nights/{id}/rsvps` | Inline list | Collapsed accordion | Separate page/modal | |
| `GET /games/{gameId}/quick-view` | Side panel | Bottom sheet | Full-screen modal | QuickView presentation |
| `GET /library/games/{gameId}/quick-view` | Side panel | Bottom sheet | Full-screen modal | Library QuickView |
| `GET /live-sessions/{sessionId}` | Full dashboard | Adapted dashboard | Card stack | |
| `GET /live-sessions/{sessionId}/scores` | Full table | Scrollable table | Card per player | |
| `GET /live-sessions/{sessionId}/timer` | Persistent header | Persistent header | Floating overlay | Timer always visible |

### Component Behavior Matrix

| Component | Desktop (>= 1024px) | Tablet (768-1023px) | Mobile (< 768px) |
|-----------|---------------------|---------------------|-------------------|
| QuickView | Side panel (right) | Bottom sheet (50vh) | Full-screen modal |
| Game list | Grid (3 cols) | Grid (2 cols) | List (1 col) |
| Score table | Full table | Horizontal scroll | Card per player |
| Timer display | Header bar | Header bar | Floating pill |
| RSVP list | Inline below event | Collapsible section | Separate view |
| Player avatars | Full row | Compact row | Stacked |

---

## Server-Side Timer Endpoints (#216)

### `POST /api/v1/sessions/{sessionId}/timer/start`

Starts the session timer. Only the session host can start the timer.

**Authorization**: Authenticated user, must be session host

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `sessionId` | `Guid` | Live session ID |

**Response** `204 No Content`

**Side Effects**:
- Begins emitting `timer_tick` SSE events every 1 second to all connected session clients
- Emits `timer_state` SSE event with initial state

**Error Responses**:

| Code | Condition |
|------|-----------|
| 401 | Not authenticated |
| 403 | User is not the session host |
| 404 | Session not found |
| 409 | Timer already running, or session not in `InProgress` state |

---

### `POST /api/v1/sessions/{sessionId}/timer/pause`

Pauses the running timer. Records elapsed time server-side.

**Authorization**: Authenticated user, must be session host

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `sessionId` | `Guid` | Live session ID |

**Response** `204 No Content`

**Side Effects**:
- Stops `timer_tick` SSE events
- Emits `timer_state` SSE event with `paused: true`

**Error Responses**:

| Code | Condition |
|------|-----------|
| 401 | Not authenticated |
| 403 | User is not the session host |
| 404 | Session not found |
| 409 | Timer not running |

---

### `POST /api/v1/sessions/{sessionId}/timer/reset`

Resets the timer to 0:00. Can be called whether timer is running or paused.

**Authorization**: Authenticated user, must be session host

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `sessionId` | `Guid` | Live session ID |

**Response** `204 No Content`

**Side Effects**:
- Resets elapsed time to 0
- Emits `timer_state` SSE event with `elapsed: 0`
- If timer was running, continues running from 0

**Error Responses**:

| Code | Condition |
|------|-----------|
| 401 | Not authenticated |
| 403 | User is not the session host |
| 404 | Session not found |

---

### `GET /api/v1/sessions/{sessionId}/timer`

Returns the current timer state. Used on initial page load and after reconnection.

**Authorization**: Authenticated user, session participant

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `sessionId` | `Guid` | Live session ID |

**Response** `200 OK`:

```typescript
interface TimerStateResponse {
  sessionId: string;              // Guid
  elapsed: number;                // Elapsed time in milliseconds
  paused: boolean;
  startedAt: string | null;       // ISO 8601 datetime, null if never started
  pausedAt: string | null;        // ISO 8601 datetime, null if not paused
  serverTime: string;             // ISO 8601 datetime (for client clock sync)
}
```

**Error Responses**:

| Code | Condition |
|------|-----------|
| 401 | Not authenticated |
| 403 | User is not a session participant |
| 404 | Session not found |

---

## Common Types

### Shared Enums

```typescript
// Used across QuickView and session endpoints
type RsvpStatus = 'Pending' | 'Accepted' | 'Declined' | 'Maybe';

// Session states relevant to timer operations
type SessionState = 'Created' | 'Setup' | 'InProgress' | 'Paused' | 'Completed';

// Player roles
type PlayerRole = 'Host' | 'Player' | 'Spectator';
```

### Standard Error Response

All error responses follow this structure:

```typescript
interface ErrorResponse {
  error: string;                  // Human-readable message
  code?: string;                  // Machine-readable error code
  details?: Record<string, string[]>; // Field-level validation errors
}
```

---

## Error Responses

### Standard HTTP Status Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| 200 | OK | Successful GET or data-returning POST |
| 201 | Created | Resource created (returns Location header) |
| 204 | No Content | Successful mutation with no body |
| 400 | Bad Request | Validation failure (FluentValidation) |
| 401 | Unauthorized | Missing or invalid session |
| 403 | Forbidden | Authenticated but insufficient permissions (not host, not owner) |
| 404 | Not Found | Resource does not exist |
| 409 | Conflict | Invalid state transition (e.g., starting already-running timer) |
| 429 | Too Many Requests | Rate limit exceeded |

### Domain-Specific Error Codes

| Code | Endpoint | Description |
|------|----------|-------------|
| `TIMER_ALREADY_RUNNING` | `POST .../timer/start` | Timer is already active |
| `TIMER_NOT_RUNNING` | `POST .../timer/pause` | Timer is not active |
| `SESSION_NOT_IN_PROGRESS` | `POST .../timer/*` | Session must be in `InProgress` state |
| `NOT_SESSION_HOST` | `POST .../timer/*` | Only the host can control the timer |
| `GAME_NOT_IN_LIBRARY` | `GET .../library/.../quick-view` | Game not found in user's library |
| `GAME_NOT_FOUND` | `GET .../games/.../quick-view` | Game not found in shared catalog |

---

## Authorization

All Sprint 2 endpoints require authentication via session cookie (existing MeepleAI auth flow).

### Permission Matrix

| Endpoint | Any Authenticated | Session Participant | Session Host Only |
|----------|-------------------|--------------------|--------------------|
| `GET /games/{id}/quick-view` | Yes | - | - |
| `GET /library/games/{id}/quick-view` | Yes (own library) | - | - |
| `GET /sessions/{id}/timer` | - | Yes | - |
| `POST /sessions/{id}/timer/start` | - | - | Yes |
| `POST /sessions/{id}/timer/pause` | - | - | Yes |
| `POST /sessions/{id}/timer/reset` | - | - | Yes |

### Rate Limits

| Endpoint Group | Limit | Window |
|---------------|-------|--------|
| QuickView endpoints | 60 requests | 1 minute |
| Timer control (start/pause/reset) | 10 requests | 1 minute |
| Timer state (GET) | 30 requests | 1 minute |

# Sprint 2 — SSE Operational Requirements (#213)

> Defines connection management, event types, backpressure, error handling, and testing
> requirements for Sprint 2 real-time features.

---

## Table of Contents

1. [Existing SSE Infrastructure](#existing-sse-infrastructure)
2. [Connection Management](#connection-management)
3. [Event Types (Sprint 2)](#event-types-sprint-2)
4. [Backpressure Strategy](#backpressure-strategy)
5. [Error Handling](#error-handling)
6. [Reconnection Protocol](#reconnection-protocol)
7. [Testing Requirements](#testing-requirements)
8. [Implementation Notes](#implementation-notes)

---

## Existing SSE Infrastructure

Sprint 2 builds on two existing SSE patterns in MeepleAI:

### Agent Chat SSE (`POST /api/v1/agents/{id}/chat`)

- **Pattern**: POST-initiated SSE stream (fetch + ReadableStream)
- **Format**: `data: { type: <number>, data: <payload>, timestamp: <iso8601> }\n\n`
- **Event types**: Numeric enum (`StreamingEventType` — 23 types, 0-22)
- **Consumer**: `useAgentChatStream.ts` — uses `fetch()` with `ReadableStream` reader
- **Lifecycle**: Single request/response — stream ends when LLM generation completes or errors

### Session Sync SSE (`GET /api/v1/game-sessions/{sessionId}/stream`)

- **Pattern**: EventSource-based persistent connection
- **Format**: Named SSE events (`event: ScoreUpdatedEvent\ndata: {...}\n\n`)
- **Event types**: String enum (`SessionEventType`)
- **Consumer**: `useSessionSync.ts` — uses `EventSource` with `withCredentials: true`
- **Lifecycle**: Long-lived connection for the duration of the session
- **Reconnection**: Exponential backoff (1s, 2s, 4s, 8s, 16s), max 5 attempts, cap 30s

### Pattern Decision for Sprint 2

Sprint 2 timer events use the **Session Sync pattern** (EventSource, long-lived connection) because:
- Timer requires persistent real-time updates (every 1 second)
- Multiple event types over the session lifetime
- Reconnection with state recovery is critical
- Aligns with existing `useSessionSync.ts` infrastructure

---

## Connection Management

### Connection Limits

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Max SSE connections per user | 3 | One per browser tab |
| Max SSE connections per session | 20 | Max players + spectators |
| Connection idle timeout | 30 minutes | Matches typical game session length |
| Heartbeat interval | 15 seconds | Keep-alive to prevent proxy/LB timeouts |

### Heartbeat

The server sends a comment-based keep-alive to prevent intermediate proxies from closing idle connections:

```
:keep-alive\n\n
```

- Interval: every 15 seconds
- Does not count toward backpressure buffer
- Client ignores comments per SSE spec (lines starting with `:`)

### Connection Lifecycle

```
Client                                  Server
  |                                       |
  |--- GET /sessions/{id}/stream -------->|
  |                                       | Validate auth + session membership
  |<-- 200 OK (text/event-stream) --------|
  |                                       |
  |<-- :keep-alive ----------------------|  (every 15s)
  |<-- event: timer_state --------------|  (on connect, current state)
  |<-- event: timer_tick ----------------|  (every 1s when timer active)
  |<-- event: session_update ------------|  (on player join/leave)
  |<-- event: score_update --------------|  (on score recorded)
  |                                       |
  |--- (connection dropped) ------------>|  (network issue)
  |                                       | Server detects close, cleans up
  |                                       |
  |--- GET /sessions/{id}/stream -------->| (reconnect with Last-Event-ID)
  |    Last-Event-ID: 42                  |
  |<-- 200 OK ----------------------------|
  |<-- event: timer_state (catch-up) ----|  (current state, not full replay)
  |<-- event: timer_tick ----------------|  (resumes normal flow)
```

### `Last-Event-ID` Support

Each SSE event includes an `id` field for resumable streams:

```
id: 42
event: timer_tick
data: {"elapsed":195000,"paused":false,"serverTime":"2026-03-13T20:15:30Z"}

```

On reconnection, the client sends `Last-Event-ID: 42`. The server:
1. Validates the event ID belongs to the session
2. Sends a `timer_state` event with the current state (no replay of missed ticks)
3. Resumes normal event flow

**Rationale**: Timer ticks are ephemeral — replaying missed ticks has no value. The current state is sufficient for client synchronization.

---

## Event Types (Sprint 2)

### `timer_tick`

Emitted every 1 second while the timer is active (not paused).

```
id: 142
event: timer_tick
data: {"elapsed":195000,"paused":false,"serverTime":"2026-03-13T20:15:30.000Z"}

```

| Field | Type | Description |
|-------|------|-------------|
| `elapsed` | `number` | Elapsed time in milliseconds |
| `paused` | `boolean` | Always `false` for tick events |
| `serverTime` | `string` | ISO 8601 server timestamp for client clock drift compensation |

**Priority**: Low (can be dropped under backpressure)

### `timer_state`

Emitted on connect/reconnect, and on any timer state change (start, pause, reset).

```
id: 143
event: timer_state
data: {"elapsed":195000,"paused":true,"startedAt":"2026-03-13T20:12:15.000Z","pausedAt":"2026-03-13T20:15:30.000Z"}

```

| Field | Type | Description |
|-------|------|-------------|
| `elapsed` | `number` | Elapsed time in milliseconds |
| `paused` | `boolean` | Whether the timer is paused |
| `startedAt` | `string \| null` | ISO 8601 when timer was first started |
| `pausedAt` | `string \| null` | ISO 8601 when timer was paused (null if running) |

**Priority**: High (must not be dropped)

### `session_update`

Emitted when session state changes (player join, player leave, state transition).

```
id: 144
event: session_update
data: {"sessionId":"abc-123","state":"InProgress","playerCount":4,"change":"player_joined","playerId":"def-456","playerName":"Charlie"}

```

| Field | Type | Description |
|-------|------|-------------|
| `sessionId` | `string` | Session GUID |
| `state` | `string` | Current session state |
| `playerCount` | `number` | Current player count |
| `change` | `string` | What changed (`player_joined`, `player_left`, `state_changed`) |
| `playerId` | `string \| null` | Affected player ID |
| `playerName` | `string \| null` | Affected player display name |

**Priority**: High (must not be dropped)

### `score_update`

Emitted when a score is recorded or edited.

```
id: 145
event: score_update
data: {"playerId":"def-456","playerName":"Charlie","score":10,"round":2,"dimension":"points","timestamp":"2026-03-13T20:16:00.000Z"}

```

| Field | Type | Description |
|-------|------|-------------|
| `playerId` | `string` | Player GUID |
| `playerName` | `string` | Player display name |
| `score` | `number` | Score value |
| `round` | `number` | Round number |
| `dimension` | `string` | Scoring dimension name |
| `timestamp` | `string` | ISO 8601 when score was recorded |

**Priority**: Medium (should not be dropped, but not as critical as state events)

---

## Backpressure Strategy

### Server-Side Buffer

| Parameter | Value |
|-----------|-------|
| Max buffer size per connection | 100 events |
| Buffer overflow policy | Drop oldest low-priority events |

### Priority Levels

| Priority | Events | Drop Policy |
|----------|--------|-------------|
| **High** | `timer_state`, `session_update` | Never dropped |
| **Medium** | `score_update` | Dropped only under severe backpressure (buffer > 90%) |
| **Low** | `timer_tick` | Dropped first when buffer exceeds 80% |

### Overflow Behavior

When the server-side buffer reaches capacity:

1. **80% full**: Drop oldest `timer_tick` events (client will self-correct on next tick)
2. **90% full**: Drop oldest `score_update` events (client can re-fetch via REST)
3. **100% full**: Drop oldest low-priority events; log warning; never drop high-priority events
4. **Persistent overflow (>30s)**: Force-close the connection (client will reconnect)

### Client-Side Handling

- **`timer_tick` staleness**: Discard any `timer_tick` event where `serverTime` is older than 5 seconds (client missed ticks and should use `timer_state` for resync)
- **Score deduplication**: Use `(playerId, round, dimension)` as a composite key; newer events replace older ones
- **Optimistic UI**: Apply events immediately; if a REST re-fetch disagrees, prefer REST data

---

## Error Handling

### HTTP Error Responses on Connection

| Code | Behavior | Client Action |
|------|----------|---------------|
| 200 | Connection established | Begin processing events |
| 401 | Unauthorized (session expired) | Close connection, redirect to login |
| 403 | Not a session participant | Show "Access denied" message, do not retry |
| 404 | Session not found | Show "Session not found" message, do not retry |
| 429 | Rate limited | Back off, show "reconnecting" banner |
| 500 | Server error | Retry with exponential backoff |

### Mid-Stream Error Handling

| Error Type | Detection | Client Action |
|------------|-----------|---------------|
| Network disconnect | EventSource `onerror` | Show offline indicator, auto-reconnect |
| Malformed event data | JSON parse failure | Log warning, skip event, continue listening |
| Unknown event type | No matching handler | Log debug, ignore event (forward-compatible) |
| Server close | EventSource `onerror` with readyState CLOSED | Auto-reconnect with backoff |

### Error Event

The server may emit an error event before closing the connection:

```
event: error
data: {"code":"SESSION_COMPLETED","message":"Session has ended"}

```

Known error codes:
- `SESSION_COMPLETED` — session was completed, no more events
- `SESSION_CANCELLED` — session was cancelled
- `CONNECTION_LIMIT_EXCEEDED` — user exceeded max SSE connections
- `INTERNAL_ERROR` — server-side failure

---

## Reconnection Protocol

### Exponential Backoff

| Attempt | Delay | Cumulative |
|---------|-------|------------|
| 1 | 1 second | 1s |
| 2 | 2 seconds | 3s |
| 3 | 4 seconds | 7s |
| 4 | 8 seconds | 15s |
| 5 | 16 seconds | 31s |
| 6+ | 30 seconds (max) | +30s each |

### Max Attempts

- **Default**: 10 attempts before giving up
- **After max attempts**: Show persistent "Connection lost" banner with manual "Reconnect" button
- **On manual reconnect**: Reset attempt counter

### State Recovery on Reconnect

1. Client sends `Last-Event-ID` header
2. Server validates the ID and session membership
3. Server sends `timer_state` with current timer values
4. Server sends `session_update` with current player list
5. Normal event flow resumes

**No tick replay**: Missed `timer_tick` events are not replayed. The `timer_state` event on reconnect provides the authoritative current state.

### UI During Reconnection

| State | UI Indicator |
|-------|-------------|
| Connected | Green dot / no indicator |
| Reconnecting (attempts 1-3) | Amber "Reconnecting..." banner (subtle) |
| Reconnecting (attempts 4+) | Amber banner with attempt count |
| Disconnected (max attempts) | Red "Connection lost" banner + "Reconnect" button |
| Offline (navigator.onLine = false) | Gray "Offline" indicator |

---

## Testing Requirements

### Connection Tests

| Test | Method | Acceptance Criteria |
|------|--------|---------------------|
| SSE connection establishes | Integration | Client receives initial `timer_state` within 2 seconds of connecting |
| Heartbeat prevents timeout | Integration | Connection stays alive for > 2 minutes with no events |
| `Last-Event-ID` recovery | Integration | After reconnect, client receives `timer_state` with current values |
| Multiple tabs | Manual/E2E | 3 tabs with SSE connections; all receive events; no connection storms |
| Connection limit enforcement | Integration | 4th tab receives 429 or connection refused |

### Timer Synchronization Tests

| Test | Method | Acceptance Criteria |
|------|--------|---------------------|
| Cross-client sync | E2E (Playwright) | 3 clients show timer within 500ms of each other |
| Refresh recovery | E2E | After page refresh, timer resumes within 1 second of server time |
| Pause propagation | E2E | All clients show paused state within 1 second of host pausing |
| Reset propagation | E2E | All clients reset to 0:00 within 1 second of host resetting |

### Backpressure Tests

| Test | Method | Acceptance Criteria |
|------|--------|---------------------|
| Buffer overflow handling | Integration | High-priority events preserved when buffer is full |
| Stale tick discard | Unit | `timer_tick` with serverTime > 5s ago is discarded by client |
| Score deduplication | Unit | Duplicate `score_update` for same `(playerId, round, dimension)` uses latest |

### Error Recovery Tests

| Test | Method | Acceptance Criteria |
|------|--------|---------------------|
| Network drop recovery | E2E (DevTools throttle) | Reconnects and resumes within 10 seconds |
| 401 handling | Integration | Redirects to login, does not retry |
| 500 handling | Integration | Retries with backoff, succeeds on recovery |
| Malformed event skip | Unit | JSON parse failure does not crash the stream handler |

### Memory Leak Tests

| Test | Method | Acceptance Criteria |
|------|--------|---------------------|
| Long-running session | Manual | 30 minutes of SSE: no event listener accumulation |
| Reconnection cycle | Integration | 10 disconnect/reconnect cycles: no memory growth > 5MB |
| Tab close cleanup | E2E | EventSource closed on unmount; no orphaned connections |

### Performance Budgets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Time to first event | < 500ms | From connection open to first `timer_state` |
| Timer display latency | < 100ms | From SSE event receipt to DOM update |
| Reconnection time | < 3s | From network recovery to receiving events |
| Memory per connection (client) | < 2MB | Heap snapshot after 10 minutes |
| CPU during idle timer | < 1% | Performance profiler during steady-state ticks |

---

## Implementation Notes

### Server-Side Architecture

The timer SSE stream should integrate with the existing session stream endpoint (`GET /api/v1/game-sessions/{sessionId}/stream`) rather than creating a new endpoint. This means:

1. Extend `SessionEventType` enum with `TimerTick`, `TimerState` event types
2. Use the same `EventSource`-based connection from `useSessionSync.ts`
3. Extend `useSessionSync` hook with `onTimerTick` and `onTimerState` callbacks

### Timer State Storage

- **Primary**: In-memory (Redis) for sub-second reads during tick emission
- **Backup**: PostgreSQL for persistence across server restarts
- **Sync**: Write to both on start/pause/reset; read from Redis for ticks

### Event ID Generation

- Use monotonically increasing per-session counter (not global UUID)
- Format: simple integer (`id: 142`)
- Reset counter on session completion
- Store last emitted ID per connection for `Last-Event-ID` validation

### Thread Safety

Timer tick emission must be thread-safe:
- Use `System.Timers.Timer` or `PeriodicTimer` (.NET 6+) for 1-second intervals
- Lock around elapsed time calculation to prevent race conditions between tick and pause/reset
- Consider `TimeProvider` for testability (existing MeepleAI pattern)

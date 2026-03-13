# Server-Side Timer — Live Session Reliability

**Issue**: #216
**Status**: Draft — Pending backend team review
**Date**: 2026-03-13

## Overview

Replace client-only timer management with server-authoritative timing to survive browser crashes, reconnections, and multi-device access during live game sessions.

## Server State

### Session Timing Fields

| Field | Type | Description |
|-------|------|-------------|
| `sessionStartedAt` | `DateTime?` | UTC timestamp when host starts session |
| `pausedAt` | `DateTime?` | UTC timestamp of current pause (null if running) |
| `totalPausedDuration` | `TimeSpan` | Cumulative pause time across all pause/resume cycles |
| `status` | `enum` | `planned`, `active`, `paused`, `completed` |

### Elapsed Time Calculation

```
If status == active:
  elapsed = now() - sessionStartedAt - totalPausedDuration

If status == paused:
  elapsed = pausedAt - sessionStartedAt - totalPausedDuration
```

## API

### `GET /api/v1/game-nights/{id}/timer`

Returns current timer state for client reconstruction.

**Response** `200 OK`:
```json
{
  "sessionStartedAt": "2026-03-14T19:05:00Z",
  "pausedAt": null,
  "totalPausedDuration": "00:05:30",
  "status": "active",
  "serverNow": "2026-03-14T20:15:00Z",
  "elapsedSeconds": 3870
}
```

`serverNow` included so the client can calculate clock skew.

### `POST /api/v1/game-nights/{id}/timer/pause`

**Response** `200 OK`: Updated timer state.

### `POST /api/v1/game-nights/{id}/timer/resume`

**Response** `200 OK`: Updated timer state. Adds `(now - pausedAt)` to `totalPausedDuration`.

## Client Implementation

### Timer Hook

```typescript
function useSessionTimer(sessionId: string) {
  // 1. Fetch timer state from server on mount/reconnect
  // 2. Calculate elapsed locally using: serverElapsed + (Date.now() - fetchTime)
  // 3. Update display every second via setInterval
  // 4. On SSE pause/resume events, refetch timer state
  // 5. On visibility change (tab focus), refetch to correct drift

  return {
    elapsedSeconds: number,
    isPaused: boolean,
    pause: () => void,
    resume: () => void,
  };
}
```

### Clock Skew Handling

```
clientOffset = serverNow - Date.now()
correctedNow = Date.now() + clientOffset
elapsed = correctedNow - sessionStartedAt - totalPausedDuration
```

Recalibrate offset on each server response. Accuracy target: ±1 second.

### Reconnection Behavior

| Event | Action |
|-------|--------|
| SSE reconnect | Refetch `/timer` to resync |
| Tab regains focus | Refetch `/timer` to correct drift |
| Browser crash + reopen | Refetch `/timer` on mount |

## Domain Model

```csharp
// GameNight entity additions
public DateTime? SessionStartedAt { get; private set; }
public DateTime? PausedAt { get; private set; }
public TimeSpan TotalPausedDuration { get; private set; }

public void StartSession()
{
    SessionStartedAt = DateTime.UtcNow;
    Status = GameNightStatus.Active;
}

public void PauseSession()
{
    Guard.Against.Null(SessionStartedAt);
    if (PausedAt is not null)
        throw new InvalidOperationException("Session is already paused.");
    PausedAt = DateTime.UtcNow;
    Status = GameNightStatus.Paused;
}

public void ResumeSession()
{
    Guard.Against.Null(PausedAt, "Not paused");
    TotalPausedDuration += DateTime.UtcNow - PausedAt.Value;
    PausedAt = null;
    Status = GameNightStatus.Active;
}
```

## Testing

| Test | Type | Assertion |
|------|------|-----------|
| Timer starts on session start | Unit | `elapsedSeconds == 0`, `status == active` |
| Pause stops elapsed counter | Unit | Elapsed frozen at pause moment |
| Resume adds pause gap to total | Unit | `totalPausedDuration` increases by pause interval |
| Client reconstructs timer on mount | Integration | Display matches server elapsed ±1s |
| Clock skew correction | Unit | `clientOffset` applied correctly |
| Multiple pause/resume cycles | Unit | `totalPausedDuration` accumulates correctly |

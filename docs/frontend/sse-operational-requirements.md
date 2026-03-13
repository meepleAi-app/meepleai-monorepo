# SSE Operational Requirements — Live Session Feed

**Issue**: #213
**Status**: Draft — Pending backend team review
**Date**: 2026-03-13

## Overview

Operational requirements for Server-Sent Events (SSE) powering the live game session activity feed. Covers reconnection strategy, event replay, throughput limits, and conflict resolution.

## Reconnection Strategy

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Initial retry delay | 1s | Fast recovery for transient drops |
| Backoff multiplier | 2x | Exponential: 1s → 2s → 4s → 8s → 16s |
| Max retry delay | 16s | Cap prevents excessive wait |
| Max offline duration | 5 min | After 5min, show "session stale" prompt |
| Jitter | ±500ms | Prevents thundering herd on server recovery |

### State Machine

```
Connected ──[drop]──→ Reconnecting ──[success]──→ Connected
                           │                          ↑
                           ├──[retry < max]──→ Waiting ─┘
                           │
                           └──[timeout 5min]──→ Stale
                                                  │
                                            [user action]
                                                  ↓
                                            Reconnecting
```

### Client Implementation

```typescript
// Hook: useSessionEventStream(sessionId: string)
// Returns: { events, connectionState, reconnect }
// connectionState: 'connected' | 'reconnecting' | 'stale' | 'error'
```

## Event Replay

| Mechanism | Detail |
|-----------|--------|
| Header | `Last-Event-ID` sent on reconnect |
| Server behavior | Replays all events after the given ID |
| ID format | Server-generated UUID v7 (sortable by time) |
| Replay window | Server retains last 1000 events per session |
| Beyond window | Client receives `replay_incomplete` event, must full-refresh |

## Throughput Limits

| Metric | Limit | Enforcement |
|--------|-------|-------------|
| Max events/sec (client render) | 20 | `requestAnimationFrame` batching |
| Max events/sec (server emit) | 50 | Server-side rate limiter |
| Client event buffer | 500 events (~2MB) | Ring buffer, oldest evicted |
| Older events | Paginated from server | `GET /api/v1/game-nights/{id}/events?before={eventId}&limit=50` |

### RAF Batching

```typescript
// Batch incoming SSE events per animation frame
// Instead of rendering each event immediately:
const pendingEvents: SessionEvent[] = [];
eventSource.onmessage = (e) => pendingEvents.push(parse(e));

function flush() {
  if (pendingEvents.length > 0) {
    dispatch(pendingEvents.splice(0));
  }
  requestAnimationFrame(flush);
}
requestAnimationFrame(flush);
```

## Deduplication

| Field | Purpose |
|-------|---------|
| `eventId` | UUID v7, server-generated, globally unique |
| Client dedup | Set of last 200 event IDs, ignore if seen |
| Server dedup | Idempotency key on write path |

## Conflict Resolution

| Principle | Detail |
|-----------|--------|
| Authority | Server is authoritative for all state |
| Offline events | Queued client-side, sent on reconnect |
| Timestamps | Server assigns canonical timestamps |
| Idempotency | Client sends idempotency key with each action |
| Merge strategy | Last-write-wins for simple fields, append-only for feed |

### Offline Queue

```typescript
interface OfflineAction {
  idempotencyKey: string;  // UUID v4, generated client-side
  action: string;          // e.g. 'add_note', 'roll_dice'
  payload: unknown;
  queuedAt: string;        // Client timestamp (informational only)
}
// On reconnect: POST each queued action, server deduplicates by idempotency key
```

## Event Types

| Type | Payload | Source |
|------|---------|--------|
| `player_joined` | `{ userId, displayName }` | Server |
| `player_left` | `{ userId, displayName }` | Server |
| `dice_roll` | `{ userId, dice, results, total }` | User action |
| `note_added` | `{ userId, text }` | User action |
| `photo_shared` | `{ userId, imageUrl, caption }` | User action |
| `turn_changed` | `{ previousPlayer, currentPlayer }` | Server |
| `session_paused` | `{ reason }` | Host action |
| `session_resumed` | `{}` | Host action |
| `session_ended` | `{ endedBy, summary }` | Host action |
| `system` | `{ message }` | Server (connection info) |
| `replay_incomplete` | `{ oldestAvailable }` | Server (on reconnect) |

## Monitoring

| Metric | Alert Threshold |
|--------|----------------|
| SSE connection drops/min | > 10 across all sessions |
| Event delivery latency p99 | > 500ms |
| Replay requests/min | > 50 (indicates instability) |
| Stale session prompts/hour | > 5 per user |

## Reference

- Existing pattern: `useAgentChatStream` hook in `apps/web/src/hooks/`
- Spec: `docs/superpowers/specs/2026-03-11-game-table-layout-design.md`

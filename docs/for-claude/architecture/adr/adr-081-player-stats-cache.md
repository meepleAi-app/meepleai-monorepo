# ADR-081 — Player Statistics Server-Side Cache (HybridCache) + Per-User Invalidation

**Status**: Proposed
**Date**: 2026-06-20
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2438](https://github.com/meepleAi-app/meepleai-monorepo/issues/2438) — `[#2350 follow-up] Play Records stats — deferred Path B features (trend, leaderboard, CSV, Redis)` (PR-B = Redis cache slice)
**Related**: [#2346](https://github.com/meepleAi-app/meepleai-monorepo/issues/2346) (Tier 2 umbrella) · ADR-062 (KB-flag cache propagation) · `GetGameLeaderboardQueryHandler` (#1467 — reference cache pattern)

## Context

`GetPlayerStatistics` (`/play-records/statistics`) is the read model behind the `/players/[id]` v2 surface. Each call runs several EF aggregations over a user's `Completed` play records (totals, win-by-game, most-played, a cross-user leaderboard-rank scan, a favorite-agent lookup, and a 6-month win-rate trend). Today the only caching is the FE React Query `staleTime` (client-side, per-browser). Concurrent or repeat requests recompute the whole projection server-side every time.

Issue #2438 (PR-B) asks for a **server-side cache (5-min TTL) + invalidation on new play record activity** to match the FE `staleTime` and remove redundant recomputation across replicas.

The codebase already has the canonical pattern: `GetGameLeaderboardQueryHandler` wraps its compute in `IHybridCacheService.GetOrCreateAsync` (L1 in-memory + L2 Redis, cache-stampede protection, tag-based invalidation). We reuse it verbatim.

## Decision

Wrap `GetPlayerStatisticsQueryHandler.Handle` in `IHybridCacheService.GetOrCreateAsync` and evict per-user via a MediatR `INotificationHandler` on the events that change which `Completed` records exist for a user.

### Cache key

```
player-stats:{userId}:{startDate.Ticks ?? 0}:{endDate.Ticks ?? 0}
```

- **Per-user** (`userId`) — stats are scoped to one player.
- **Per-range** (`Ticks` of the optional `StartDate`/`EndDate`) — the query accepts an optional date window; different windows are different results. `Ticks` (not a formatted date string) is timezone-stable and serialization-safe, mirroring `GetGameLeaderboard`'s `since:{Ticks}` key segment. `null` → `0`.

### Cache tag

```
player-stats:{userId}
```

One tag per user. A single `RemoveByTagAsync("player-stats:{userId}")` evicts **all** of that user's ranged entries at once, so invalidation never has to enumerate date-window combinations.

### TTL

5 minutes (`TimeSpan.FromMinutes(5)`) — matches the FE React Query `staleTime` and the `GetGameLeaderboard` precedent.

### Invalidation triggers

| Event | Trigger? | Rationale |
|---|---|---|
| `PlayRecordCompletedEvent` | **Yes** | A record transitioning to `Completed` is exactly what `GetPlayerStatistics` counts (it filters `Status == Completed`). This is the "new play record" activity #2438 refers to. |
| `PlayRecordCreatedEvent` | **No** | A freshly created record is `Planned`. `GetPlayerStatistics` ignores non-`Completed` records, so a Created event cannot change any cached value. Evicting on Created would be a pure waste (cold cache + re-scan) for a no-op delta. |
| `PlayRecordStartedEvent` / `PlayRecordUpdatedEvent` | **No** | `Started` moves `Planned → InProgress` (still not counted). `Updated` edits notes/location/session-date on an existing record; it does not change the `Completed`-set membership that drives the cached aggregates. (If a future change lets `UpdateDetails` move a record's `SessionDate` across a cached date-window boundary for an already-`Completed` record, revisit — see Consequences.) |
| Deletion | **N/A (no capability)** | PlayRecord has **no** delete command, no `PlayRecordDeletedEvent`, and no soft-delete (`IsDeleted`/`DeletedAt`) as of this ADR. The endpoint surface is Create/Start/Complete/AddPlayer/RecordScore/Update + two GETs — no DELETE verb. When deletion is introduced, add a `PlayRecordDeletedEvent` trigger to the same handler (it carries the owning userId directly, so no lookup is needed). |

> The plan that seeded this work assumed a `PlayRecordDeletedEvent(RecordId, DeletedByUserId)`. That event does not exist in the codebase and deletion is out of scope for #2438 ("invalidation subscriber on **new** play record"). The Deleted branch is intentionally **not** implemented here; this row documents the future hook.

### Completed-event userId resolution

`PlayRecordCompletedEvent` carries only `RecordId` + `Duration` (no userId). The invalidation handler resolves the owner with a single indexed lookup:

```csharp
var userId = await _context.PlayRecords
    .Where(r => r.Id == notification.RecordId)
    .Select(r => r.CreatedByUserId)
    .FirstOrDefaultAsync(ct);
```

If the lookup returns `Guid.Empty` (record not found — should not happen for a just-completed record, but guards re-dispatch races), the handler no-ops.

### Failure mode (best-effort invalidation)

Invalidation is **best-effort**, mirroring `UserActivityCacheInvalidationEventHandler`: the `RemoveByTagAsync` call is wrapped in try/catch + structured logging so a cache/Redis hiccup never propagates into — and never rolls back — the `CompletePlayRecordCommand` that raised the event. A missed eviction self-heals at the 5-min TTL. On the read path, HybridCache L2 (Redis) failures fall back to L1 per the service contract, so stats stay available.

## Consequences

**Positive**
- Repeat / concurrent `/play-records/statistics` calls within the TTL serve from L1/L2 instead of re-running the aggregation + the cross-user leaderboard scan.
- Cross-replica correctness: L2 (Redis) is shared; a completion on replica A invalidates the shared L2 entry so replica B recomputes on its next miss.
- Cache-stampede protection: the first miss after an eviction runs the factory once; concurrent callers wait and share the result.

**Negative / trade-offs**
- One extra indexed `SELECT CreatedByUserId` per record completion (the userId lookup). Negligible relative to a completion write.
- Up to 5 minutes of staleness for changes that are **not** completion events but could in principle affect a cached value — specifically a future `UpdateDetails` that moves an already-`Completed` record's `SessionDate` across a cached date-window boundary. Today the only callers query the default (unbounded) window where this is a non-issue; flagged here so the trigger set is revisited if ranged queries + post-completion date edits both become common.

## Alternatives considered

- **Per-(user,range) tag** — would require the invalidation handler to know every cached window. Rejected; the single per-user tag evicts all windows in one call.
- **Evict on every PlayRecord event** — simplest to reason about but wastes the cache on `Created`/`Started`/`Updated` deltas that cannot change a `Completed`-filtered projection. Rejected for the documented per-event analysis above.
- **No server cache (status quo)** — leaves the cross-user leaderboard scan running on every call. Rejected; that scan is the most expensive part of the projection.

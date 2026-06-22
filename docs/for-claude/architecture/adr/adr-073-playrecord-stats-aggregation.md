# ADR-073 — PlayRecord Stats Aggregation Refresh Strategy

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 4 — US-INT-2 (PlayRecord stats surface)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · mockup `sp4-play-records-stats.html` · issue #3890 (CQRS play record queries)

## Context

The `GameManagement` bounded context contains the `PlayRecord` aggregate (`Domain/Entities/PlayRecord.cs`) and a `GetPlayerStatisticsQueryHandler` (`Application/Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs`) that already implements per-user statistics aggregation over completed play records.

**Current implementation** of `GetPlayerStatisticsQueryHandler`:
- Fetches all completed play records for a user via `_context.PlayRecords.AsNoTracking().Include(r => r.Players).ThenInclude(p => p.Scores).Where(r => r.Status == Completed)` — a full table scan over the user's records, loaded in-memory.
- Performs in-memory LINQ aggregation: `totalSessions`, `totalWins`, `gamePlayCounts`, `averageScoresByGame`, `totalDurationMinutes`, `winByGame`, `mostPlayedGames`, `leaderboardRank`, `favoriteAgentName`, `winRateTrend`.
- `leaderboardRank` additionally executes a cross-user aggregate SQL query (`CountAsync`) over all completed records.
- Contains an inline scale note: "Acceptable for community scale < 100k users; revisit with a materialized leaderboard view when scale demands."

The `sp4-play-records-stats.fidelity.json` mockup (`design_intent: "current"`) defines a stats dashboard that includes total sessions, win rate, average duration, most-played games, and the win-rate trend chart — all derived from completed `PlayRecord` rows.

**Key constraints**:
- `PlayRecord.UpdatedAt` and `PlayRecord.Status` are updated by command handlers (`CompletePlayRecordCommandHandler`). These raise `PlayRecordCompletedEvent` domain events.
- The domain entity uses private setters + factory methods — any stats materialisation must read from the EF entity model, not the domain aggregate (per DDD infrastructure pattern: entities are reconstituted from EF, not from domain aggregate directly).
- Memory pitfall `notracking-default-update-gotcha.md`: always use `AsNoTracking()` for read-only stats queries — this is already in place in the current handler.
- The `MeepleAiDbContext` is used directly in `GetPlayerStatisticsQueryHandler` (via `_context.PlayRecords`), which is the accepted cross-context pattern for complex read queries in this codebase.

**Volume estimate**: the play records feature is MVP-stage. The community is invite-only with a small user base. However, a per-user stats page load that executes a full cross-user `CountAsync` for leaderboard rank will not scale past ~10k records without index support.

## Problem

The specific architectural question: **how should per-user play record statistics be refreshed — real-time on every `/stats` GET, via Redis TTL cache, via nightly batch into a materialised table, or via event-driven invalidation — given MVP scale constraints and the mockup's requirement for stats currency?**

The stats dashboard is user-facing (not admin-only) and expected to be checked after each game session. Staleness tolerance must be defined.

## Options Considered

### Option A — Real-Time Aggregate Query on Every GET (current behaviour)

Every `GET /api/v1/play-records/stats` executes `GetPlayerStatisticsQueryHandler.Handle()` — full in-memory aggregation over all completed play records plus the cross-user leaderboard rank query.

**Pros**:
- Always fresh — reflects the latest completed play record immediately.
- Zero infrastructure dependency beyond the DB.
- Already implemented and in production (`GetPlayerStatisticsQueryHandler` is wired via CQRS routing).

**Cons**:
- `_context.PlayRecords.Include(Players).ThenInclude(Scores)` loads all player/score rows for every stats request. For a user with 500 play records × 4 players × 3 score dimensions = 6,000 rows loaded per request. O(n) in play record count.
- The cross-user leaderboard `CountAsync` is a full scan of all completed records for all users — no per-user index on `(status, created_by_user_id)`. Degrades O(N_total_records) with community growth.
- Repeated identical queries on the same data (stats don't change between game sessions) waste DB I/O.

**Risks**: Performance degrades predictably as play record volume grows. Acceptable at MVP scale (<1,000 records per user, <10k total); unacceptable at 100k+ total records without index additions.

**Impact**: 0 additional implementation. Already live.

---

### Option B — Redis TTL Cache (5-minute window)

`GetPlayerStatisticsQueryHandler` checks a Redis key `play-stats:{userId}` before executing the DB query. On cache miss, execute the query, cache the result as JSON with TTL = 5 minutes. Invalidate on `PlayRecordCompletedEvent` if Redis is available.

**Pros**:
- Dramatically reduces DB load for users who check their stats repeatedly within a session (common pattern: play a game → immediately check stats → share screenshot).
- Redis is already present in the infrastructure (`docker-compose.yml` includes `redis` service; `StackExchange.Redis` is a dependency).
- 5-minute staleness is acceptable for a game-night stats dashboard.

**Cons**:
- Redis adds a hard runtime dependency to the stats read path. If Redis is down, the handler must fall back to DB query (adding fallback logic complexity).
- TTL-based staleness: after completing a play record, the stats cache will show the old data for up to 5 minutes unless explicitly invalidated. Users who complete a game and immediately check stats may see stale data — confusing UX.
- Explicit invalidation on `PlayRecordCompletedEvent` requires an `INotificationHandler` that calls Redis — cross-cutting infrastructure concern added to the GameManagement bounded context.
- Cache key must be scoped to query parameters (`userId`, `startDate`, `endDate`) — cache key explosion if date filters are varied.

**Risks**: Redis downtime = stats endpoint degrades to real-time query (Option A fallback). Cache invalidation complexity on query-parameter variations.

**Impact**: ~2 days. Redis caching in `GetPlayerStatisticsQueryHandler` + invalidation handler.

---

### Option C — Nightly Batch + Materialised View

A scheduled job (Hangfire/Quartz, or a GHA cron workflow) runs nightly at 02:00 UTC, computing per-user aggregates and writing to a `play_record_stats` denormalisation table. `GetPlayerStatisticsQueryHandler` reads from this table instead of computing live.

**Pros**:
- Fastest possible read: a single-row lookup by `user_id`.
- Eliminates the leaderboard cross-user scan from the stats request path — the rank is pre-computed.

**Cons**:
- Stats are up to 23h stale. A user who completes a game at 20:00 and checks stats at 21:00 sees yesterday's data — unacceptable for a feature marketed as "track your progress".
- Requires a new `play_record_stats` table + migration + nightly job infrastructure.
- The existing codebase uses Hangfire for scheduled jobs (`CooldownEndReminderJob`, `StaleShareRequestWarningJob`) but adding a stats materialisation job increases job scheduler complexity.
- Invalidation on-demand (user clicks "refresh") bypasses the batch job, requiring a live fallback anyway.

**Risks**: Staleness unacceptable for post-session stats review. The mockup's stats dashboard is expected to be consulted immediately after a game — a 23h stale result would be misleading.

**Impact**: ~3 days. New migration + Hangfire job + fallback for live refresh. Out of scope for US-INT-2 MVP.

---

### Option D — Hybrid: Redis Cache + Explicit Invalidation on Write (recommended)

Extend Option A with targeted cache invalidation: when `CompletePlayRecordCommandHandler` completes (post `SaveChangesAsync`), publish a `PlayRecordCompletedEvent`. A `PlayRecordStatsInvalidationHandler : INotificationHandler<PlayRecordCompletedEvent>` calls `IDistributedCache.RemoveAsync($"play-stats:{userId}")`.

`GetPlayerStatisticsQueryHandler` uses `IDistributedCache.GetOrCreateAsync($"play-stats:{userId}", async () => { <execute DB query> }, absoluteExpiration: TimeSpan.FromMinutes(30))`.

On cache miss (first request, or post-completion): full DB query. On cache hit: JSON deserialise (sub-millisecond). On completion event: Redis key removed immediately, next request re-populates.

**Pros**:
- Stats are always fresh immediately after a play record completion: the invalidation handler removes the cache key in the same MediatR publish cycle as the completion event.
- 30-minute TTL is a safety net against leaked cache entries (e.g. if the invalidation handler throws and the key is never cleared).
- Reduces DB round-trips for users who view stats multiple times between sessions (most common case: check stats once after a game, then again an hour later — second request is cache hit).
- `IDistributedCache` is an abstraction layer that can use Redis in staging/prod and `IMemoryCache`-backed in dev/test without changing handler code.
- Avoids the leaderboard scan staleness issue (invalidation ensures the rank recalculates on next request).

**Cons**:
- `IDistributedCache` adds a dependency to `GetPlayerStatisticsQueryHandler` (currently depends only on `MeepleAiDbContext`). Small scope increase.
- Cache invalidation handler (`PlayRecordStatsInvalidationHandler`) is a new event handler class.
- JSON serialisation round-trip for `PlayerStatisticsDto` (which contains nested collections) requires explicit `System.Text.Json` configuration (record types with `IReadOnlyList` must be serialisable).

**Risks**: Low. `IDistributedCache` uses Redis in production (already available) and in-memory in test environments. If Redis is unavailable, `GetOrCreateAsync` throws and must be caught — fall back to direct DB query.

**Impact**: ~1.5 days. `IDistributedCache` injection in the query handler, `PlayRecordStatsInvalidationHandler`, JSON serialisation config for `PlayerStatisticsDto`.

## Decision

**Adopt Option D**: hybrid Redis cache with explicit invalidation on `PlayRecordCompletedEvent`, with a 30-minute TTL safety net.

**Rationale**: Option A's real-time approach is acceptable at MVP scale but has a documented scaling concern (the inline comment in `GetPlayerStatisticsQueryHandler:123` acknowledges this). Option D extends the existing architecture minimally (one new event handler, one `IDistributedCache` injection) while providing immediate freshness after game completion — the most important user-facing freshness requirement. Option C's 23h staleness is incompatible with the post-session use case. Option B's TTL-only approach would show stale data immediately after a game is completed.

## Consequences

**Positive**:
- Stats always reflect the most recently completed play record within the same MediatR publish cycle.
- DB query frequency drops from "every page load" to "once after each game completion + first load after 30 minutes of inactivity".
- The `leaderboardRank` cross-user scan is amortised over cache hits — the expensive query runs at most once per completion event per user.

**Negative**:
- `PlayerStatisticsDto` must be JSON-serialisable. The DTO uses C# records with `IReadOnlyList<GameWinStats>`, `IReadOnlyList<GamePlayCount>`, `IReadOnlyList<MonthlyWinRate>` — these require `System.Text.Json` source generation or explicit converter configuration to avoid runtime `JsonException` on complex nested types.
- The invalidation handler (`PlayRecordStatsInvalidationHandler`) runs in the same MediatR pipeline as the completion event — if it throws, the exception propagates to the command caller. Must be wrapped in a try/catch with `LogWarning` (cache invalidation failure is non-fatal; the 30-minute TTL covers it).

**Trade-offs**:
- Date-filtered stats (`startDate`/`endDate` query parameters) cannot be efficiently cached with a single `play-stats:{userId}` key — the cache key must include the filter parameters. For the MVP stats dashboard (no date filter), this is not an issue. If date-filtered stats are added later, the cache key schema must be updated.

## Implementation Guidance

1. **Cache injection**: add `IDistributedCache _cache` to `GetPlayerStatisticsQueryHandler` constructor. Wrap the existing LINQ aggregation in `GetOrCreateAsync($"play-stats:{query.UserId}", async () => { ... }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) })`.

2. **Invalidation handler**: `PlayRecordStatsInvalidationHandler : INotificationHandler<PlayRecordCompletedEvent>` at `apps/api/src/Api/BoundedContexts/GameManagement/Application/EventHandlers/PlayRecordStatsInvalidationHandler.cs`. Calls `_cache.RemoveAsync($"play-stats:{evt.UserId}", ct)`. Wrap in try/catch — cache miss on key removal is not an error (key may have already expired).

3. **Serialisation**: annotate `PlayerStatisticsDto` and its nested records (`GameWinStats`, `GamePlayCount`, `MonthlyWinRate`) with `[JsonSerializable]` in a `PlayRecordsJsonContext : JsonSerializerContext` source-generation context. Use `JsonSerializer.Serialize(dto, PlayRecordsJsonContext.Default.PlayerStatisticsDto)` for cache write and `JsonSerializer.Deserialize` for cache read.

4. **Dev environment**: in `appsettings.Development.json`, `IDistributedCache` resolves to the in-memory distributed cache (`services.AddDistributedMemoryCache()` — already registered in `Program.cs` for dev). No Redis required in dev.

5. **Scale note update**: remove or update the comment in `GetPlayerStatisticsQueryHandler:123` after this ADR is implemented to reflect the caching strategy.

## Rollback / Reversibility

Removing `IDistributedCache` injection and the `PlayRecordStatsInvalidationHandler` reverts to the current real-time Option A behaviour. No schema migration is involved. Cache entries expire naturally after 30 minutes. The rollback is fully additive (add/remove a handler class and a cache injection).

## References

- `GetPlayerStatisticsQueryHandler` — `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetPlayerStatisticsQueryHandler.cs`
- `PlayRecordCompletedEvent` — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/PlayRecordCompletedEvent.cs`
- `PlayRecord` aggregate — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs`
- `IPlayRecordRepository` — `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IPlayRecordRepository.cs`
- Mockup `sp4-play-records-stats.fidelity.json` (`design_intent: "current"`)
- Memory: `notracking-default-update-gotcha.md` (always `AsNoTracking()` on read queries)
- ADR-060: `SaveChangesAsync`-then-publish contract governs when `PlayRecordCompletedEvent` fires

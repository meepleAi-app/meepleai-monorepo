# ADR-069 follow-up — AiToolkitSuggestion cache + event-driven invalidation

**Status**: Approved (2026-06-16 brainstorm session)
**Driver**: ADR-069 open question #2 (PR #2381) — "on-demand LLM generation vs pre-generated cached suggestion"
**Tracking**: umbrella [#2383](https://github.com/meepleAi-app/meepleai-monorepo/issues/2383)
**Related**: ADR-069 itself · `GenerateToolkitFromKbCommand` · existing `kb_chunks.usage_count` event-driven pattern (PR #2323)

## Decision

**Cached approach with event-driven invalidation** (Option D from #2383 analysis):

1. Pre-compute the suggestion after PDF indexing succeeds; persist to a new `ai_toolkit_suggestion_cache` table.
2. `GenerateToolkitFromKbCommand` becomes **cache-aside**: check cache first, return if hit; only call the LLM on miss.
3. Cache invalidation is event-driven: an `INotificationHandler<PdfReindexedEvent>` (or the equivalent existing `KbDocIndexedEvent` if `PdfReindexedEvent` does not exist yet) deletes the cached entry for the affected `gameId`. The next user request triggers an LLM regeneration.

## Why this over the alternatives

| Alternative | Why rejected |
|---|---|
| **On-demand only** (status quo) | Latency 5-30s on every toolkit panel open is unacceptable UX. LLM cost scales with M (requests) instead of N (games). |
| **TTL fixed 24h + event hybrid** | Robust but pays 1 LLM call per popular game per day even with no PDF change. Premature optimization for an event delivery reliability concern we have not observed. |
| **Manual invalidation + lazy fallback** | Adds FE "regenerate" button + admin endpoint to scope. Loses automatic freshness guarantee. Higher cognitive load on users. |

The chosen approach matches a pattern already shipped in the codebase: `kb_chunks.usage_count` is bumped via `INotificationHandler` post-RAG citation (PR #2323). The same event-handler-as-cache-invalidator shape applies here.

## Architecture

### New entity

```csharp
// apps/api/src/Api/BoundedContexts/GameToolkit/Domain/Entities/AiToolkitSuggestionCacheEntry.cs
internal sealed class AiToolkitSuggestionCacheEntry : Entity<Guid>
{
    public Guid GameId { get; private set; }
    public string SuggestionJson { get; private set; } = string.Empty; // serialized AiToolkitSuggestionResponseDto
    public DateTimeOffset GeneratedAt { get; private set; }
    public int? KbVersion { get; private set; } // optional — null if no PDF version tracking exists yet

    public static AiToolkitSuggestionCacheEntry Create(Guid gameId, string suggestionJson, int? kbVersion) { ... }
    internal void Refresh(string suggestionJson, int? kbVersion) { ... }
}
```

Persistence: `ai_toolkit_suggestion_cache` table.
- `id UUID PRIMARY KEY`
- `game_id UUID NOT NULL UNIQUE` (one cached suggestion per game)
- `suggestion_json TEXT NOT NULL`
- `generated_at TIMESTAMPTZ NOT NULL`
- `kb_version INT NULL`

The UNIQUE on `game_id` makes "cache invalidation = DELETE WHERE game_id = X" trivial; "regenerate" is a single UPSERT.

### Cache-aside handler change

Modify `GenerateToolkitFromKbCommandHandler`:

```csharp
public async Task<AiToolkitSuggestionResponseDto> Handle(GenerateToolkitFromKbCommand cmd, CancellationToken ct)
{
    var cached = await _cacheRepo.GetByGameIdAsync(cmd.GameId, ct).ConfigureAwait(false);
    if (cached is not null)
    {
        MeepleAiMetrics.RecordAiToolkitCacheHit(cmd.GameId);
        return JsonSerializer.Deserialize<AiToolkitSuggestionResponseDto>(cached.SuggestionJson)!;
    }

    MeepleAiMetrics.RecordAiToolkitCacheMiss(cmd.GameId);
    var generated = await _llmService.GenerateSuggestionAsync(cmd.GameId, ct).ConfigureAwait(false);
    var json = JsonSerializer.Serialize(generated);
    var entry = AiToolkitSuggestionCacheEntry.Create(cmd.GameId, json, kbVersion: null);
    await _cacheRepo.AddAsync(entry, ct).ConfigureAwait(false);
    return generated;
}
```

### Invalidation handler

```csharp
// apps/api/src/Api/BoundedContexts/GameToolkit/Application/EventHandlers/InvalidateToolkitSuggestionCacheOnPdfReindexedHandler.cs
internal sealed class InvalidateToolkitSuggestionCacheOnPdfReindexedHandler
    : INotificationHandler<PdfReindexedEvent> // verify exact event name during impl
{
    public async Task Handle(PdfReindexedEvent evt, CancellationToken ct)
    {
        await _cacheRepo.DeleteByGameIdAsync(evt.GameId, ct).ConfigureAwait(false);
        _logger.LogInformation("Invalidated AiToolkit cache for game {GameId} post-PDF-reindex", evt.GameId);
    }
}
```

### Telemetry

Counters added to `MeepleAiMetrics`:

- `meepleai_aitoolkit_cache_hit_total{game_id}` — increments on cache-aside hit
- `meepleai_aitoolkit_cache_miss_total{game_id}` — increments on cache-aside miss (LLM call follows)
- `meepleai_aitoolkit_cache_invalidated_total{game_id}` — increments on event-driven delete

These let us observe hit ratio (target: >80% after warm-up) and validate that the invalidation handler is firing.

## Component responsibilities

| Unit | What it does | How you use it | Dependencies |
|---|---|---|---|
| `AiToolkitSuggestionCacheEntry` (domain) | Holds the cached suggestion + metadata | `Create(gameId, json, kbVersion)` / `Refresh(json, kbVersion)` | none (pure POCO) |
| `IAiToolkitSuggestionCacheRepository` | Persistence contract | `GetByGameIdAsync` / `AddAsync` / `DeleteByGameIdAsync` | DbContext |
| `GenerateToolkitFromKbCommandHandler` (modified) | Cache-aside read + LLM fallback | Existing MediatR send | `IAiToolkitSuggestionCacheRepository`, LLM service |
| `InvalidateToolkitSuggestionCacheOnPdfReindexedHandler` | Cache delete on PDF reindex | Auto-discovered via MediatR | `IAiToolkitSuggestionCacheRepository` |

Each unit has one purpose. Internal changes (e.g. switch JSON serializer, add KbVersion check) do not leak to consumers.

## Error handling

- **LLM call fails** (timeout, rate limit, 5xx): caller of `GenerateToolkitFromKbCommand` already handles this today; cache is not written on failure (no partial state).
- **Cache repository fails** (DB down): log + fall through to LLM (degraded mode = original on-demand behavior). Do NOT propagate to client — UX should never degrade because of cache infrastructure.
- **Invalidation handler fails**: log error; cache entry becomes stale until next PDF reindex (acceptable — bounded by reindex cadence). Do NOT throw (handler is post-commit, throw would not roll back the reindex).
- **Concurrent writes** (two users open toolkit panel simultaneously on cold cache): both hit LLM. The second `AddAsync` may hit a UNIQUE violation on `game_id`. Wrap the `AddAsync` in a try/catch on `DbUpdateException` SQLSTATE 23505 + constraint `UX_ai_toolkit_suggestion_cache_game_id` → ignore (the other call already populated the cache).

## Testing strategy

| Test type | Coverage |
|---|---|
| **Unit** (handler, in-memory mocked repo) | Cache hit returns cached; cache miss calls LLM; LLM result is cached post-call; LLM failure does not cache; cache repo failure falls back to LLM (degraded mode). |
| **Unit** (invalidation handler) | Event triggers delete; delete failure is logged but does not throw. |
| **Integration** (Testcontainers Postgres) | UNIQUE constraint on `game_id` enforced; UPSERT semantics work; concurrent insert race → 23505 caught silently. |
| **Telemetry** | Counters increment in correct branches (hit / miss / invalidated). |

## Migration

`apps/api/src/Api/Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAiToolkitSuggestionCache.cs`:

```sql
CREATE TABLE ai_toolkit_suggestion_cache (
    id UUID PRIMARY KEY,
    game_id UUID NOT NULL,
    suggestion_json TEXT NOT NULL,
    generated_at TIMESTAMPTZ NOT NULL,
    kb_version INT NULL,
    CONSTRAINT UX_ai_toolkit_suggestion_cache_game_id UNIQUE (game_id)
);

CREATE INDEX IX_ai_toolkit_suggestion_cache_generated_at ON ai_toolkit_suggestion_cache (generated_at);
-- Allows pruning old entries via background job if storage becomes a concern.
```

No backfill: existing games have no cached suggestion until first user request OR next PDF reindex triggers regeneration.

## Out of scope (explicit YAGNI)

- **Admin "force regenerate" endpoint** (`POST /api/v1/games/{id}/toolkit/regenerate`). Deferred until user feedback shows the auto-invalidation is insufficient.
- **KbVersion tracking enforcement**. The field is nullable in this iteration; populated only if PDF versioning lands separately. Allows cache to work today without blocking on a larger feature.
- **Telemetry dashboard** (Grafana panel for hit rate). Counters are exported; dashboard is a separate operational task.
- **Cache pruning job** (delete stale entries older than 90 days). Deferred until storage > 1 GB.

## Rollback

`git revert <merge-commit>` plus migration rollback:

```bash
dotnet ef database update <PreviousMigrationName>
# OR if not yet applied to prod:
dotnet ef migrations remove
```

Drop table is safe — cached suggestions are pure derived data, regeneratable on demand.

## References

- ADR-069 source: `docs/for-claude/architecture/adr/adr-069-aitoolkitsuggestion-polymorphic-dto.md` (shipped PR #2381)
- Open question source: PR #2381 body §"Open questions per ADR" item #2; refined in [#2383 analysis comment-4715365924](https://github.com/meepleAi-app/meepleai-monorepo/issues/2383#issuecomment-4715365924)
- Pattern reference: `kb_chunks.usage_count` event-driven increment via `INotificationHandler<RagCitationEvent>` (PR #2323 ADR-066/067 wave)
- Existing handler: `apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/InstallToolkit/...` (verify exact path for `GenerateToolkitFromKbCommandHandler` during impl)
- Brainstorm session: 2026-06-16 (decision: Latency UX priority + event-driven invalidation, both Recommended options selected)

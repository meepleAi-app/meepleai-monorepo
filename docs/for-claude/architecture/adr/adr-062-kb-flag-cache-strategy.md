# ADR-062 — KB-flag cache propagation strategy

**Status**: Accepted (2026-06-13, supersedes 2026-06-12 Proposed)
**Context**: Epic [#2242](https://github.com/meepleAi-app/meepleai-monorepo/issues/2242) — PDF indexing flow repair.
**Sub-issue**: [#2248](https://github.com/meepleAi-app/meepleai-monorepo/issues/2248) Sub #6 Block C (Quality Gates).
**Authors**: KB pipeline workstream.

## Context

`#2243` Block A is the tactical fix that flips `shared_games.has_knowledge_base` to `true` after a successful PDF indexing pipeline run, by publishing `VectorDocumentIndexedEvent` from `FinalizeProcessingAsync`. `VectorDocumentIndexedForKbFlagHandler` consumes that event and:

1. Updates `shared_games.has_knowledge_base`.
2. Invalidates the HybridCache tag `search-games` AND `shared-game:{id}`.

Tag invalidation on HybridCache evicts the **L2 distributed entry** (Redis, shared across nodes) but only the **L1 in-memory entry of the local API replica**. Other replicas keep serving their L1 cached `SharedGameDto`s (with `HasKnowledgeBase=false`) for up to **15-30 minutes** — the configured `LocalCacheExpiration` on:

- `SearchSharedGamesQueryHandler.cs` (the catalog list endpoint, 15 min)
- `GetSharedGameByIdQueryHandler.cs` (the catalog detail endpoint, 30 min)

For Wave A.4 (single-replica staging) this was acceptable. For multi-replica production (and the catalog Phase E F4 SSE event stream shipped in #2227) it produces a multi-minute window where:

- Replica A (where the upload happened) sees `HasKnowledgeBase=true` immediately.
- Replicas B/C still serve `HasKnowledgeBase=false` until their L1 expires.

Resulting UX: a sticky-session user on replica B reports "agente non pronto" while the same user, refreshing through replica A, sees "agente pronto". Hard to debug, surfaces as flaky behaviour.

Documented at `VectorDocumentIndexedForKbFlagHandler.cs:88-94`; mitigation tracked here.

## Options considered

### Option 1 — Reduce `LocalCacheExpiration` to 60 seconds

Change `SearchSharedGamesQueryHandler` and `GetSharedGameByIdQueryHandler` to use a 60-second L1 TTL (was 15-30 min).

**Pros**: trivial code change (2 numeric constants + their tests). Cache stampede is still bounded by the L2 layer. Operationally simple — no new infrastructure.

**Cons**: 60-second blind window on multi-replica. Slightly higher Redis L2 hit rate (acceptable per the existing benchmarks). Does not fundamentally solve the cross-replica propagation problem — it only narrows it.

**Effort**: ~1h including a rebaseline test for the L1 expiry assertion if any exists.

### Option 2 — Redis pub/sub L1 invalidation broadcast (without wrapper)

Publish a "kb-flag-changed:{sharedGameId}" message on a Redis channel from `VectorDocumentIndexedForKbFlagHandler`. Each replica subscribes (directly via `IConnectionMultiplexer`) and evicts the L1 entry on receipt.

**Pros**: sub-second propagation. No L1 TTL change needed.

**Cons**: introduces new infra surface (pub/sub subscription on startup, reconnect logic), risk of missed messages during connection blips. Requires per-replica subscription manager. The HybridCache APIs do not expose a native L1-only eviction primitive, so the subscriber has to invoke `RemoveByTagAsync` (idempotent but re-touches L2). Plumbing duplicated at every event handler that needs the broadcast.

**Effort**: ~2-3 days (subscriber service + reconnect handling + integration test with Testcontainers Redis).

### Option 3 — HybridCache wrapper with built-in cross-replica broadcast (CHOSEN)

Extend the existing `IHybridCacheService` (CLAUDE.md #2620 — wrapper around `HybridCache` already used by `ProviderQuotaService`, `InvalidateCacheCommandHandler`) with a new method `RemoveByTagAcrossReplicasAsync(tag)` that:

1. Performs the local `RemoveByTagAsync(tag)` (evicts L1 of the calling replica + L2 distributed).
2. Publishes the tag to a Redis Pub/Sub channel `meepleai:cache-invalidate:tag`.

A dedicated `BackgroundService` (`HybridCacheInvalidationSubscriber`) subscribes to that channel on every replica at startup. On receive it calls `_hybridCache.RemoveByTagAsync(tag)` locally — evicting the stale L1 entry on the receiving replica. Subscription is idempotent: the publishing replica also receives its own message and re-runs the eviction as a no-op.

**Pros**: sub-second propagation (Redis Pub/Sub latency ~1ms intra-DC). Cross-replica invalidation logic encapsulated in **one** place. Other event handlers that need the same pattern (toolkit, agent, mechanic-metrics) can adopt it incrementally without re-implementing pub/sub plumbing. Centralised observability (one subscriber → one log site → one Prometheus counter to add later if needed). Plays well with the existing `IHybridCacheService` consumers (ProviderQuotaService, InvalidateCacheCommandHandler) that benefit from the same wrapper.

**Cons**: introduces a `BackgroundService` startup dependency (Redis must be reachable at boot or subscription retry kicks in). Cross-cutting refactor scope: the chosen event handler (`VectorDocumentIndexedForKbFlagHandler`) migrates from `HybridCache` direct to `IHybridCacheService`. Reader code paths (`SearchSharedGamesQueryHandler`, `GetSharedGameByIdQueryHandler`) keep their existing `HybridCache` direct usage — only **writers** of the invalidation event need to switch to the wrapper.

**Effort**: ~1 day for the minimum-viable cross-replica broadcast (extension + subscriber + handler migration + unit tests). Future incremental adoption by other writer handlers is opt-in.

### Variants explicitly rejected

- **Wholesale wrapper migration of all SharedGameCatalog query handlers**: deferred. The reader-side wrapper migration has no functional value for this epic (HybridCache direct already works for reads). Scope creep risk too high — covered by a separate tracking issue if a cross-cutting policy emerges.
- **Tag tracking via Microsoft.Extensions.Caching.Hybrid native API**: HybridCache 9.x does not expose a `GetKeysByTag` primitive, so the wrapper would have to maintain its own tag → key Redis Set tracking. The existing `HybridCacheService` already does this; we reuse it.

## Decision

**Adopt Option 3** for this epic. Rationale:

- Cross-replica propagation drops from 15-30 min to sub-second — definitively closes the UX flake reported in #2242 across all read paths.
- The Sub #6 audit job (`KbFlagDriftAuditJob` Block B, this same epic) keeps catching residual drift within 10 minutes regardless, providing defence-in-depth.
- Encapsulating the broadcast in `IHybridCacheService` keeps the writer-side surface small: only `VectorDocumentIndexedForKbFlagHandler` changes in this epic. Future writers (toolkit, mechanic-metrics, BG Twin cache invalidation) can adopt incrementally without re-architecting.
- Option 1 was rejected because narrowing the window from 15 min to 60 s leaves a fundamentally unresolved propagation gap; multi-tab users still observe the flake within the 60 s window. The audit job exists for a reason — to surface drift the cache layer itself failed to propagate. With Option 3 the audit job should remain perpetually at SLO=0 in healthy operation.
- Option 2 was rejected because the same plumbing repeated across event handlers degrades long-term maintainability. The wrapper centralises it.

## Implementation

Shipped in this PR (#2266):

1. **Extension** — `IHybridCacheService.RemoveByTagAcrossReplicasAsync(tag, ct)` interface + `HybridCacheService` impl (apps/api/src/Api/Services/HybridCacheService.cs):
   - Step 1: `await this.RemoveByTagAsync(tag, ct)` — evicts L1 of this replica + L2 (Redis).
   - Step 2: `await _redisDb.PublishAsync("meepleai:cache-invalidate:tag", tag)` — broadcast.
2. **Subscriber** — `HybridCacheInvalidationSubscriber : BackgroundService` (apps/api/src/Api/Services/HybridCacheInvalidationSubscriber.cs):
   - `ExecuteAsync`: subscribes to `meepleai:cache-invalidate:tag` channel.
   - On message: `await _hybridCache.RemoveByTagAsync(tag)` to evict L1 locally.
   - Registered via `AddHostedService<HybridCacheInvalidationSubscriber>()` in InfrastructureServiceExtensions.
3. **Handler refactor** — `VectorDocumentIndexedForKbFlagHandler`:
   - Replace `HybridCache _cache` dependency with `IHybridCacheService _cacheService`.
   - Replace `_retryPolicy.ExecuteAsync(token => _cache.RemoveByTagAsync(...))` with `_retryPolicy.ExecuteAsync(token => _cacheService.RemoveByTagAcrossReplicasAsync(...))`.
4. **Cross-instance behaviour comment** at `VectorDocumentIndexedForKbFlagHandler.cs:88-94`: updated to reference this ADR and the new cross-replica invariant ("sub-second propagation via cache-invalidate channel").
5. **Unit tests** — `HybridCacheServiceTests` adds:
   - `RemoveByTagAcrossReplicasAsync_InvokesLocalRemoveAndRedisPublish`.
   - `HybridCacheInvalidationSubscriberTests` (new file): `OnMessage_InvokesLocalHybridCacheRemoveByTag`.

## Consequences

- **Latency**: multi-replica HasKnowledgeBase flip surfaces within sub-second (Redis Pub/Sub typical 1ms intra-DC); SSE consumers (#2227) see the flip immediately on next polling tick.
- **Operational footprint**: one new Redis subscription per API replica (constant overhead, negligible).
- **Redis load**: Pub/Sub messages tag-sized (string ≤ 32 bytes per tag) — negligible vs existing tag tracking write load.
- **Failure mode**: if Redis is unreachable at boot, subscriber retries (Polly-style backoff). Existing `ICacheInvalidationRetryPolicy` continues to guard the publish step. Both layers degrade independently; if both fail, behaviour falls back to existing L1 TTL expiry (15-30 min) — i.e. legacy Option 1 worst-case behaviour, never worse.
- **SLO**: `meepleai.pdf.indexed.no.kb.flag.total` (Sub #6 Block B) remains the canary; it should stay at zero in healthy operation since the broadcast eliminates the cross-replica window. Any non-zero increment now signals either (a) a handler bypass regression (Block A pattern returns), (b) Redis Pub/Sub outage masking, or (c) a new ingestion path; all three are operational P1 signals.
- **Forward extension**: other writer handlers (toolkit, mechanic-metrics, agent contributors) can adopt `RemoveByTagAcrossReplicasAsync` opportunistically. No epic-level migration required.

## References

- Epic [#2242](https://github.com/meepleAi-app/meepleai-monorepo/issues/2242)
- Sub #1 PR [#2263](https://github.com/meepleAi-app/meepleai-monorepo/pull/2263) (Block A shipped)
- Sub #2 PR [#2278](https://github.com/meepleAi-app/meepleai-monorepo/pull/2278) (BE refactor factory + pipeline)
- Sub #6 [#2248](https://github.com/meepleAi-app/meepleai-monorepo/issues/2248) (this Block C)
- IHybridCacheService reference: `apps/api/src/Api/Services/IHybridCacheService.cs`
- HybridCache invalidation semantics: Microsoft .NET 9 `Microsoft.Extensions.Caching.Hybrid` docs.
- CLAUDE.md known pitfall #2620 — HybridCache via IHybridCacheService for event handlers.

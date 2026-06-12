# ADR-062 — KB-flag cache propagation strategy

**Status**: Proposed (2026-06-12)
**Context**: Epic [#2242](https://github.com/meepleAi-app/meepleai-monorepo/issues/2242) — PDF indexing flow repair.
**Sub-issue**: [#2248](https://github.com/meepleAi-app/meepleai-monorepo/issues/2248) Sub #6 Block C (Quality Gates).
**Authors**: KB pipeline workstream.

## Context

`#2243` Block A is the tactical fix that flips `shared_games.has_knowledge_base` to `true` after a successful PDF indexing pipeline run, by publishing `VectorDocumentIndexedEvent` from `FinalizeProcessingAsync`. `VectorDocumentIndexedForKbFlagHandler` consumes that event and:

1. Updates `shared_games.has_knowledge_base`.
2. Invalidates the HybridCache tag `search-games` AND `shared-game:{id}`.

Tag invalidation on HybridCache evicts the **L2 distributed entry** (Redis, shared across nodes) but only the **L1 in-memory entry of the local API replica**. Other replicas keep serving their L1 cached `SharedGameDto`s (with `HasKnowledgeBase=false`) for up to **15 minutes** — the configured `LocalCacheExpiration` on:

- `SearchSharedGamesQueryHandler.cs` (the catalog list endpoint)
- `GetSharedGameByIdQueryHandler.cs` (the catalog detail endpoint)

For Wave A.4 (single-replica staging) this was acceptable. For multi-replica production (and the catalog Phase E F4 SSE event stream shipped in #2227) it produces a 15-minute window where:

- Replica A (where the upload happened) sees `HasKnowledgeBase=true` immediately.
- Replicas B/C still serve `HasKnowledgeBase=false` until their L1 expires.

Resulting UX: a sticky-session user on replica B reports "agente non pronto" while the same user, refreshing through replica A, sees "agente pronto". Hard to debug, surfaces as flaky behaviour.

Documented at `VectorDocumentIndexedForKbFlagHandler.cs:88-94`; mitigation tracked here.

## Options considered

### Option 1 — Reduce `LocalCacheExpiration` to 60 seconds (RECOMMENDED)

Change `SearchSharedGamesQueryHandler` and `GetSharedGameByIdQueryHandler` to use a 60-second L1 TTL (was 15 min).

**Pros**: trivial code change (2 numeric constants + their tests). Cache stampede is still bounded by the L2 layer. Operationally simple — no new infrastructure.

**Cons**: 60-second blind window on multi-replica. Slightly higher Redis L2 hit rate (acceptable per the existing benchmarks).

**Effort**: ~1h including a rebaseline test for the L1 expiry assertion if any exists.

### Option 2 — Redis pub/sub L1 invalidation broadcast

Publish a "kb-flag-changed:{sharedGameId}" message on a Redis channel from `VectorDocumentIndexedForKbFlagHandler`. Each replica subscribes and evicts the L1 entry on receipt.

**Pros**: sub-second propagation. No L1 TTL change needed.

**Cons**: introduces new infra surface (pub/sub subscription on startup, reconnect logic), risk of missed messages during connection blips. Requires per-replica subscription manager. The HybridCache APIs do not expose a native L1 eviction primitive, so the subscriber has to invoke a private API or use a parallel `IMemoryCache`. Maintenance debt rises.

**Effort**: ~2-3 days (subscriber service + reconnect handling + integration test with Testcontainers Redis).

### Option 3 — HybridCache stampede protection via `IHybridCacheService` wrapper

Wrap `HybridCache` calls in an `IHybridCacheService` (already exists per CLAUDE.md #2620) and centralise cache-stampede protection + invalidation. The wrapper publishes/subscribes to a per-key invalidation event internally.

**Pros**: keeps the cache-stampede mitigation that Option 1 implicitly preserves; cleaner abstraction long-term.

**Cons**: cross-cutting refactor — touches every handler that reads `SharedGames`. Risk of accidental behaviour drift in unrelated query paths.

**Effort**: ~5+ days (refactor + regression test suite).

## Decision

**Adopt Option 1** for this epic. Rationale:

- Closes the UX gap reported in #2242 with the smallest code change and the lowest operational risk.
- The Sub #6 audit job (`KbFlagDriftAuditJob` Block B, this same epic) catches any residual drift within 10 minutes regardless of the L1 TTL choice, so 60s is already over-engineered for the recovery window.
- Option 2/3 remain on the table if the catalog SSE work (#2227) grows enough that 60s propagation becomes a UX issue — at that point pub/sub graduates from "nice to have" to "load-bearing", and the team has more signal to choose between Option 2 and Option 3 from production telemetry.

## Implementation

To be shipped in a follow-up PR of #2248:

1. In `SearchSharedGamesQueryHandler.cs`, change the `HybridCacheEntryOptions.LocalCacheExpiration` (or equivalent constant) from `TimeSpan.FromMinutes(15)` to `TimeSpan.FromSeconds(60)`.
2. Same change in `GetSharedGameByIdQueryHandler.cs`.
3. Update the cross-instance behaviour comment in `VectorDocumentIndexedForKbFlagHandler.cs:88-94` to point at this ADR.
4. Verify existing cache-related unit tests still pass; if any asserts on a >60s expiry, rebaseline.
5. Manually verify in staging that flushing `meepleai_local_cache_evictions_total` (or equivalent gauge) follows the expected ~60-second cycle under load.

## Consequences

- Multi-replica propagation window drops from 15 min to ≤60 sec.
- Redis L2 read pressure on `search-games:*` rises modestly. The L2 layer is sized for catalog peak, so the increase is well within headroom (already verified during Wave A.3 sizing).
- The SLO=0 on `meepleai.pdf.indexed.no.kb.flag.total` (Sub #6 Block B) keeps catching any residual drift if Block A regresses or a new ingestion path is added.

## References

- Epic [#2242](https://github.com/meepleAi-app/meepleai-monorepo/issues/2242)
- Sub #1 PR [#2263](https://github.com/meepleAi-app/meepleai-monorepo/pull/2263) (Block A shipped)
- Sub #6 [#2248](https://github.com/meepleAi-app/meepleai-monorepo/issues/2248) (this Block C)
- Existing 15-min limitation comment: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/VectorDocumentIndexedForKbFlagHandler.cs:88-94`
- HybridCache invalidation semantics: Microsoft .NET 9 `Microsoft.Extensions.Caching.Hybrid` docs.

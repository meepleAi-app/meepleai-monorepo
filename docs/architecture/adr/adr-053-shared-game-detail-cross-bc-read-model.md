# ADR-053 — Shared Game Detail Cross-BC Read Model

**Status**: Accepted
**Date**: 2026-04-29
**Deciders**: @badsworm
**Spec-panel ref**: Wave A.4 spec §11 (originally numbered "ADR 0014" in the V2 migration spec at `docs/superpowers/specs/2026-04-28-v2-migration-wave-a-4-shared-game-detail.md`)

## Context

Wave A.4 (Issue #603) introduced the public detail page `/shared-games/[id]` which surfaces a single `SharedGameDetailDto` aggregating data from **three additional bounded contexts** beyond the owning `SharedGameCatalog`:

- `GameToolkit` — top-N published toolkit previews + counts
- `KnowledgeBase` (Agents) — top-N agent previews + counts via `AgentDefinition._gameId`
- `KnowledgeBase` (Vectors) — top-N indexed PDF previews + KB counts via `VectorDocument.SharedGameId`

The handler also derives composite flags (`HasKnowledgeBase`, `IsTopRated`, `IsNew`) and `ContributorsCount` (currently approximated as `DISTINCT Toolkit.OwnerUserId` since `AgentDefinition` and `VectorDocument` lack a `CreatedBy` column).

This raises the question of how cross-BC data should be assembled while respecting the **monolith modulare** boundary rules established in [ADR-047](./adr-047-crossbc-fk-policy.md).

## Problem

The detail projection requires reading entities owned by other BCs (`Toolkits`, `AgentDefinitions`, `VectorDocuments`, `Users`). Three patterns were considered:

1. **Inject other BCs' repositories or services** into `GetSharedGameByIdQueryHandler`.
2. **Materialised view / event-sourced read model** that other BCs publish into.
3. **Read-only LINQ queries against the shared `MeepleAiDbContext`** scoped to the calling BC's handler.

Pattern 1 is forbidden by ADR-047 §2 (BCs do not expose repositories or services to each other). Pattern 2 is the long-term ideal but introduces operational complexity (eventual consistency, projection rebuilds, dual-write reconciliation) disproportionate to the current single-instance Postgres + monolith deployment.

## Decision

**Cross-BC aggregation in `GetSharedGameByIdQueryHandler` is performed via direct `MeepleAiDbContext` queries against other BCs' tables, treated as read-only.**

This is consistent with ADR-047 §2 communication rule:

> The BCs do not expose repositories or services directly to each other. Communication occurs via:
> - Domain events (MediatR `INotificationHandler`)
> - Direct queries to the DB through their own `DbContext` (read-only)

Concrete implementation rules:

1. **Read-only**: the handler MUST NOT mutate entities owned by other BCs. All cross-BC LINQ uses `AsNoTracking()`.
2. **No cross-BC entity references in domain code**: aggregation lives in the *application* layer (`Application/Queries/`), never in the `Domain/` layer of `SharedGameCatalog`.
3. **Constants over enum imports**: cross-BC enum values (e.g. `ApprovalStatus.Approved == 2`) are inlined as `private const int` to avoid pulling cross-BC type references into the LINQ tree (see `ApprovedStatus` constant in the handler).
4. **Cache invalidation via tags**: each detail entry is tagged `shared-game:{id}` in `HybridCache`. Cross-BC event handlers (toolkit/agent/KB changes) invalidate by tag, achieving eventual consistency without restructuring the read path.
5. **Per-fan-out telemetry**: each cross-BC query records `MeepleAiMetrics.RecordSharedGameDetailCrossBcQuery` with a `BoundedContext` label so we can observe which BC dominates p95 latency (Issue #614).
6. **Top-N caps**: nested previews are capped (`MaxToolkitPreviews=20`, `MaxAgentPreviews=10`, `MaxKbPreviews=30`) to bound payload size and avoid unbounded fan-out for popular games.

## Consequences

### Positive
- **BC isolation preserved at code level**: no cross-BC repository injection, no shared application services. The dependency graph among BCs stays acyclic at the C# project level.
- **Single round-trip for aggregates**: counts are combined into one `_context.SharedGames.Where(...).Select(new { ... }).FirstOrDefaultAsync()` query, so all four counts execute as parallel sub-selects in a single DB round-trip rather than four sequential calls.
- **Surgical cache invalidation**: `HybridCache` tag `shared-game:{id}` lets toolkit/agent/KB event handlers invalidate one entry without flushing the whole `search-shared-games` namespace.
- **No new infrastructure**: avoids the operational cost of materialised views or event-sourced projections for a feature whose read volume is bounded by the public catalog page.

### Negative
- **Schema coupling at query-tree level**: changes to `Toolkit.GameId` / `AgentDefinition._gameId` / `VectorDocument.SharedGameId` shape force handler updates here. Mitigation: cross-BC integration test `GetSharedGameByIdQueryHandlerCrossBcTests` (PR #627) seeds a 5-BC graph and asserts the projection contract, so schema drift in any owning BC fails CI.
- **Approximation in `ContributorsCount`**: only `Toolkit.OwnerUserId` contributes today because agents and vectors lack `CreatedBy`. This is a known limitation tracked in the spec; resolving it requires schema additions in `KnowledgeBase`, deferred to a future wave.
- **No real-time freshness guarantee**: detail entries can serve stale data for up to 30 min (L1) / 2 h (L2) when the invalidation tag is missed. Acceptable for the public catalog use case; not acceptable for personal user data (out of scope here).

## Alternatives considered

### A. Direct repository injection (rejected)
Injecting `IToolkitRepository`, `IAgentDefinitionRepository`, `IVectorDocumentRepository` into the SharedGameCatalog handler. Rejected because it violates ADR-047 §2 — repositories are owning-BC private contracts, not cross-BC integration surfaces.

### B. Event-sourced read model (rejected for now)
A `SharedGameDetailReadModel` table populated by `INotificationHandler`s on `ToolkitPublished`, `AgentDefinitionAttachedToGame`, `VectorDocumentIndexed`, etc. This is the long-term direction if read volume grows or if we need cross-region replication, but introduces:
- Dual-write reconciliation complexity
- Projection rebuild tooling for backfill / schema migration
- Test surface area for eventual-consistency edge cases

Disproportionate cost for the current single-region monolith. Revisit when one of: (i) detail-page p95 exceeds 500 ms after cache miss, (ii) we adopt multi-region per ADR-045, or (iii) cross-BC count drift becomes a recurring bug class.

### C. Single-purpose `ISharedGameDetailReadModel` abstraction (rejected)
Wrapping the cross-BC queries behind an interface owned by SharedGameCatalog. Considered briefly, but adds an abstraction with a single implementation and no reuse, while not changing the fact that the implementation still queries other BCs' tables. YAGNI; revisit if a second consumer appears.

## References

- [ADR-046](./adr-046-game-sharedgame-data-ownership.md) — `Game` vs `SharedGame` data ownership boundary
- [ADR-047](./adr-047-crossbc-fk-policy.md) — Cross-BC FK policy (monolith modulare)
- [ADR-048](./adr-048-sharedgame-soft-delete.md) — `SharedGame` soft-delete (informs filter shape)
- Wave A.3a §6 — `SearchSharedGamesQueryHandler` precedent for cross-BC aggregate counts
- Wave A.4 spec — `docs/superpowers/specs/2026-04-28-v2-migration-wave-a-4-shared-game-detail.md`
- Issue #603, Issue #614, Issue #617
- Implementation: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandler.cs`
- Cross-BC integration test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Handlers/GetSharedGameByIdQueryHandlerCrossBcTests.cs`

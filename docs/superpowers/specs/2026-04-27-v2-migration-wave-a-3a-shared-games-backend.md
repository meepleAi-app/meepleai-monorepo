# V2 Migration — Wave A.3a — `/shared-games` backend extension

**Status**: Draft
**Date**: 2026-04-27
**Umbrella**: #579 (V2 Design Migration)
**Mockup**: `admin-mockups/design_files/sp3-shared-games.jsx` (1108 LOC)
**Prerequisite for**: A.3b (frontend `/shared-games` page migration)
**Author**: pre-impl spec (consensus pending)

## 1. Context

Wave A.3 migrates the `/shared-games` public catalog index to the V2 design system. The mockup
(`sp3-shared-games.jsx`) requires per-game **toolkit / agent / KB counts**, **"newWeek" /
"top-rated" badges**, and a **top contributors sidebar** — none of which the current public API
exposes.

This spec covers **A.3a (backend-first)**. A.3b (frontend impl) lands in a separate PR after A.3a
ships and the new fields stabilize.

### 1.1 Decision trail

Three options were considered for Wave A.3:

| Option | Approach | Pros | Cons | Decision |
|--------|----------|------|------|----------|
| A | Frontend with mock data, BE later | Fast 1:1 mockup | Tech debt, future migration churn | ❌ |
| **B** | **Backend-first, then FE on real data** | **Real data from day 1, no fake fixtures** | **Larger scope, 2 PRs** | **✅ chosen** |
| C | Reduced FE scope (no counts, no top-contrib) | Smallest delta | Significant deviation from mockup | ❌ |

User confirmed Option B. This document plans **A.3a** (backend extension only).

## 2. Goals

- Extend `SharedGameDto` with the aggregate counts and feature flags the mockup needs.
- Add a new `GET /shared-games/top-contributors` public endpoint.
- Add filter parameters to `SearchSharedGamesQuery` for the 4 mockup chips.
- Add sort options for `contrib` and `new` (mockup line 372-373).
- All changes **public** (`AllowAnonymous` + rate-limited via `SharedGamesPublic`).
- Cache invalidation strategy preserved (HybridCache tag `search-games`).

## 3. Non-goals

- **No new database columns** — counts are computed at query time via existing FK relationships.
  This avoids EF migrations and double-bookkeeping.
- **No frontend work** — A.3b is a separate spec/PR.
- **No changes to admin endpoints** — `SharedGameCatalogAdminEndpoints.cs` untouched.
- **No new BC** — extensions live in existing `SharedGameCatalog/Application/Queries/`.
- **No `IsTopRated` precomputation** — stays a derived flag (`AverageRating >= 4.5`).

## 4. Architectural facts (audit results)

```
SharedGame (SharedGameCatalog BC)              ← MASTER catalog
  ↑ FK SharedGameId
Game (GameManagement BC)                        ← per-instance, has ApprovalStatus
  ↑ FK GameId
  ├── Toolbox       (GameToolbox BC)            ← Toolbox.GameId
  ├── AgentDefinition (KnowledgeBase BC)        ← AgentDefinition.GameId
  └── Session       (SessionTracking BC)        ← Session.GameId, Session.WinnerId

VectorDocument (KnowledgeBase BC)               ← has DIRECT SharedGameId FK
                                                  (HasKnowledgeBase already exposed)
```

**Implication**: Toolkit/Agent counts require `Game.SharedGameId == sg.Id AND ApprovalStatus = Approved`
join. KB count is a direct lookup.

## 5. API changes

### 5.1 `SharedGameDto` — 7 new fields (additive, non-breaking)

```csharp
public sealed record SharedGameDto(
    Guid Id, int? BggId, string Title, int YearPublished, string Description,
    int MinPlayers, int MaxPlayers, int PlayingTimeMinutes, int MinAge,
    decimal? ComplexityRating, decimal? AverageRating,
    string ImageUrl, string ThumbnailUrl,
    GameStatus Status, DateTime CreatedAt, DateTime? ModifiedAt,
    bool IsRagPublic = false,
    bool HasKnowledgeBase = false,
    // NEW (A.3a):
    int ToolkitsCount = 0,        // count of Toolbox via Game.SharedGameId
    int AgentsCount = 0,          // count of AgentDefinition via Game.SharedGameId
    int KbsCount = 0,             // count of VectorDocument.SharedGameId == sg.Id
    int NewThisWeekCount = 0,     // count of (toolkits+agents+KBs) created in last 7 days
    int ContributorsCount = 0,    // count of distinct UserIds that authored toolkits/agents/KBs
    bool IsTopRated = false,      // derived: AverageRating >= 4.5
    bool IsNew = false);          // derived: NewThisWeekCount >= 2
```

**Defaulted parameters** = no client breakage. Existing tests/serializers continue to work.

### 5.2 `SearchSharedGamesQuery` — 4 new filter parameters

```csharp
internal record SearchSharedGamesQuery(
    string? SearchTerm,
    List<Guid>? CategoryIds, List<Guid>? MechanicIds,
    int? MinPlayers, int? MaxPlayers, int? MaxPlayingTime,
    decimal? MinComplexity, decimal? MaxComplexity,
    GameStatus? Status,
    int PageNumber = 1, int PageSize = 20,
    string SortBy = "Title", bool SortDescending = false,
    bool? HasKnowledgeBase = null,
    // NEW (A.3a):
    bool? HasToolkit = null,         // chip "with-toolkit"
    bool? HasAgent = null,           // chip "with-agent"
    bool? IsTopRated = null,         // chip "top-rated" → AverageRating >= 4.5
    bool? IsNew = null               // chip "new" → NewThisWeekCount >= 2
) : IQuery<PagedResult<SharedGameDto>>;
```

### 5.3 New `SortBy` values

`SortBy` accepted values extended:
- `"Contrib"` → ORDER BY ContributorsCount DESC NULLS LAST (mockup `<option value="contrib">`)
- `"New"` → ORDER BY NewThisWeekCount DESC, CreatedAt DESC (mockup `<option value="new">`)

Existing values (`Title`, `YearPublished`, `AverageRating`, `CreatedAt`, `ComplexityRating`) preserved.

### 5.4 New endpoint: `GET /shared-games/top-contributors`

```http
GET /shared-games/top-contributors?limit=5
```

**Auth**: `AllowAnonymous` + `RequireRateLimiting("SharedGamesPublic")`
**Response**: `200 OK` with `IReadOnlyList<TopContributorDto>`
**Cache**: HybridCache key `top-contributors:{limit}`, TTL L1 15min / L2 1h, tag `top-contributors`

```csharp
public sealed record TopContributorDto(
    Guid UserId,
    string DisplayName,           // from user profile
    string? AvatarUrl,
    int TotalSessions,            // count Sessions.UserId == u.Id
    int TotalWins,                // count Sessions where Winner == u.Id
    int Score);                   // TotalSessions + TotalWins * 2 (mockup formula)
```

`limit` validated 1..20 (FluentValidation), default 5.

## 6. Implementation plan

### 6.1 PR strategy: single monolithic PR

A.3a ships in **one PR** covering all 3 scope areas atomically:

| Scope area | Files touched | Approx LOC |
|------------|---------------|------------|
| DTO extension + counts projection | `SharedGameDto.cs`, `SearchSharedGamesQueryHandler.cs` | ~200 |
| Filter params + new sort options | `SearchSharedGamesQuery.cs`, validator, handler | ~250 |
| `/shared-games/top-contributors` endpoint | new query/handler/validator/DTO/endpoint, 3 cache invalidation handlers | ~300 |
| **Total** | | **~750** |

**Rationale**: avoids intermediate states (e.g. counts in DTO before filters wired) which would
cause confusing partial responses to FE during the A.3b implementation window. Single rollback
point if perf gate fails.

**Reviewer aid**: PR description will include 3 commit boundaries:
1. `feat(shared-games): extend SharedGameDto with toolkit/agent/KB aggregate counts`
2. `feat(shared-games): add hasToolkit/hasAgent/isTopRated/isNew filters + new sort options`
3. `feat(shared-games): add GET /shared-games/top-contributors public endpoint`

Each commit individually buildable + tested. Reviewer can read commits sequentially.

### 6.2 Counts query strategy

**Option 1 (chosen)**: Single LINQ query with sub-selects projected into `SharedGameDto`.

```csharp
var query = _dbContext.SharedGames
    .Where(/* status filter */)
    .Select(sg => new {
        Game = sg,
        ToolkitsCount = _dbContext.Toolboxes
            .Where(t => !t.IsDeleted)
            .Join(_dbContext.Games, t => t.GameId, g => g.Id, (t, g) => g.SharedGameId)
            .Count(sgId => sgId == sg.Id),
        AgentsCount = /* analogous join Games × AgentDefinitions */,
        KbsCount = _dbContext.VectorDocuments.Count(vd => vd.SharedGameId == sg.Id),
        // ...
    });
```

**Tradeoff**: 5 sub-queries per row. With pagination (20/page) + HybridCache (L1 15min / L2 1h),
this is acceptable. Benchmark in PR review; if p95 > 200ms, fall back to denormalized `LATERAL`
view (PostgreSQL).

**Alternative considered (rejected for A.3a)**: New `shared_games_aggregates` materialized view.
Adds migration overhead and refresh-strategy decision. Defer to A.3b+ if perf demands it.

### 6.3 `NewThisWeekCount` semantics

Sum of:
- Toolboxes where `CreatedAt >= NOW() - INTERVAL 7 DAY` AND `Game.SharedGameId = sg.Id`
- AgentDefinitions where same time predicate via `Game.SharedGameId`
- VectorDocuments where same time predicate via `SharedGameId`

`IsNew = NewThisWeekCount >= 2` (mockup line 127).

### 6.4 `ContributorsCount` semantics

Distinct count of `UserId` (or `CreatedBy`) across:
- Toolboxes joined to Games where `SharedGameId = sg.Id`
- AgentDefinitions joined analogously
- VectorDocuments where `SharedGameId = sg.Id`

`COUNT(DISTINCT UserId)` over the union. Implemented as 3 sub-selects + `.Distinct().Count()`.

### 6.5 Cache invalidation

Existing `VectorDocumentIndexedForKbFlagHandler` already invalidates tag `search-games` when a
KB is indexed. Audit needed for:
- New event handlers when `Toolbox` / `AgentDefinition` change → invalidate `search-games`
- `Session` finalization (winner declared) → invalidate `top-contributors`

Implementation: 3 new domain event handlers in `SharedGameCatalog/Application/EventHandlers/`:
- `ToolboxChangedForCatalogAggregatesHandler` (listens to `ToolboxCreatedEvent`, `ToolboxDeletedEvent`)
- `AgentDefinitionChangedForCatalogAggregatesHandler`
- `SessionCompletedForContributorsHandler`

All thin: `await _hybridCache.RemoveByTagAsync("search-games", ct)` etc.

## 7. Testing strategy

### 7.1 Per-PR test matrix

| Layer | A.3a-1 | A.3a-2 | A.3a-3 |
|-------|--------|--------|--------|
| Domain | — | — | — (no new aggregates) |
| Application unit | Handler returns correct counts (in-memory fakes) | Filter combinations (HasToolkit ⊕ IsTopRated etc.) | TopContributors handler ranking + limit clamping |
| Integration (Testcontainers) | Counts on real Postgres + seeded games | Filter accuracy + pagination invariants | Anonymous access + rate-limit + cache hit/miss |
| Validator | — | New params validated | `limit` 1..20 |

### 7.2 Edge cases (must-cover)

- `SharedGame` with 0 linked Games → all counts = 0, no NULL in projection.
- `SharedGame` with `Status != Published` → must NOT appear in public search (existing behavior, preserved).
- Toolbox/Agent linked to a `Game` whose `ApprovalStatus != Approved` → catalog counts EXCLUDE
  these (`ToolkitsCount`/`AgentsCount`/`KbsCount`/`NewThisWeekCount`/`ContributorsCount`).
  *Rationale: catalog counts must reflect what an unauthenticated user could actually access.*
- **`TopContributorDto.TotalSessions`/`TotalWins` count ALL sessions** including unpublished games
  (per §9 decision 4 — contributor reputation reflects total play activity, not catalog visibility).
- `IsTopRated = true` filter when `AverageRating IS NULL` → excluded (NULL is not >= 4.5).
- Top-contributors with ties → secondary sort by `UserId` (deterministic).

### 7.3 Performance gate

- p95 `/shared-games?pageSize=20` < 250ms (cold cache), < 50ms (warm L1).
- p95 `/shared-games/top-contributors?limit=5` < 100ms warm.

EXPLAIN ANALYZE captured in PR description for reviewer evidence.

## 8. Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Sub-query count perf on large catalogs | Slow public page | Bench in PR; fallback to MATERIALIZED VIEW in A.3a-bis if needed |
| Cache stampede on `/top-contributors` | DB load spike | HybridCache L2 1h + jittered refresh; rate limit `SharedGamesPublic` already in place |
| `NewThisWeekCount` includes deleted entities | Wrong UX badges | Apply `IsDeleted = false` filter on each sub-query |
| `ContributorsCount` user privacy | Leak inactive users | Only published `Game` ApprovalStatus considered; no PII in count |
| Cross-BC query couples bounded contexts | DDD concern | Read-only projection; no command crossing boundaries; documented in this spec |

## 9. Resolved decisions (2026-04-27 review)

1. **`IsTopRated` threshold = 4.5** with config flag `SharedGameCatalog:TopRatedThreshold` (default 4.5).
   `IsTopRated = AverageRating.HasValue && AverageRating >= threshold`.

2. **`NewThisWeekCount` window = 7 days** with config flag `SharedGameCatalog:NewWindowDays` (default 7).

3. **Top contributors scope = global** (not per-game, not per-genre). Single ranking across the whole
   platform, used by the sidebar in `/shared-games`.

4. **`TotalWins` counts ALL sessions** (no filter on `Game.ApprovalStatus`). Rationale: contributor
   reputation reflects total play activity, not catalog visibility. Sessions of unpublished/private
   games still count toward the contributor's score.

5. **PR strategy: monolithic** — single PR for A.3a covering all 3 sub-scopes (DTO + filters + endpoint).
   Trade-off accepted: bigger review surface in exchange for atomic migration of the BE layer; reduces
   intermediate states where `SharedGameDto` exposes counts but filters aren't wired yet (avoids
   FE/BE mismatch windows).

## 10. Acceptance criteria

- [ ] `SharedGameDto` exposes 7 new fields (defaulted, non-breaking).
- [ ] `GET /shared-games?hasToolkit=true&isNew=true&sort=Contrib` returns filtered+sorted results.
- [ ] `GET /shared-games/top-contributors?limit=5` returns top 5 by `TotalSessions + TotalWins * 2`.
- [ ] All endpoints `AllowAnonymous` + rate-limited.
- [ ] HybridCache tag invalidation works (verified via integration test: write event → next read fresh).
- [ ] Test coverage: handler ≥ 90%, validator ≥ 95%, endpoint integration ≥ 1 happy path + 1 edge per scenario.
- [ ] EXPLAIN ANALYZE artifacts attached to each PR.
- [ ] No EF migration in A.3a (computed at query time).
- [ ] DDD: no command crosses BC boundary; only read projections via `_dbContext` direct access.

## 11. Out of scope (deferred)

- **A.3b**: frontend `/shared-games` impl with the new fields + visual baselines bootstrap.
- **A.3a-bis** (conditional): MATERIALIZED VIEW if A.3a perf gate fails post-merge in staging.
- **A.4**: `/shared-games/[id]` v2 migration (existing route, separate spec).
- **A.5**: `/invites/[token]` v2 migration.

---

**Next step**: create single child issue under #579, branch from `main-dev`, implement in 3
sequential commits matching the §6.1 boundaries, open one PR targeting `main-dev`.

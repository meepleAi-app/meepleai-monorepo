# `/discover` Composite Endpoint — Design

**Issue**: #728 (Backend audit prerequisite for #687 `/discover` Wave 3 v2 migration)
**Branch**: `feature/issue-728-discover-endpoints` (parent: `main-dev`)
**Author**: brainstorming session 2026-05-06
**Status**: Draft → User review pending

---

## 1. Context & Problem Statement

V2 migration Wave 3 child #687 must implement the route `/discover` from mockup `sp4-discover.{html,jsx}`: a discovery dashboard with 7 cross-entity rows. Existing endpoints cover 2 rows (Trending, Eventi vicini); the audit (#681 Wave 3 Step 1) revealed **5 rows missing** backend support:

| # | Row | Source data |
|---|-----|------------|
| 1 | **Giochi nuovi** (New games) | `SharedGameCatalog` ordered by `createdAt desc` |
| 2 | **Agenti più installati** (Top agents) | Aggregate `count(installations) desc` |
| 3 | **Toolkit consigliati** (Recommended toolkits) | Public toolkits ordered by `installCount desc` |
| 4 | **KB recenti** (Recent KB docs) | Public KB docs ordered by `lastIngestedAt desc` cross-game |
| 5 | **Top contributor** | Aggregate users by `count(faqs + kb_uploads + agents)` |

This document specifies the design before implementation begins.

### Key constraint: cross-BC composition

The 5 rows source data from **5 distinct bounded contexts**: SharedGameCatalog, GameManagement (agents), GameToolkit, KnowledgeBase, Authentication (users). The composition layer must orchestrate these without polluting any existing BC with cross-cutting read concerns.

This is the root reason behind decision **D4** (new `Discover` BC) below.

---

## 2. Architectural Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| **D1** | Primary actor | **Mix** of newcomer + returning user + power user — uniform DTO, no per-user personalization in MVP | Discover serves all logged-in users uniformly; personalization is a separate feature scope |
| **D2** | Ranking algorithm for "Toolkit consigliati" + "Top contributors" | **MVP semplice (count-based)**: `ORDER BY install_count DESC` for toolkits; `ORDER BY (count(faqs) + count(kb_uploads) + count(agents)) DESC` equal-weight sum for contributors | YAGNI — time-decay/weights add complexity without measurable MVP value; refinable when metrics show need |
| **D3** | Failure semantics | **Partial success (lenient)**: each row try-catch in composer; failure → empty array for that row + structured `ILogger.LogError`. Composite endpoint never returns 500 due to one sub-query failure | Graceful degradation; UX resilient (1 broken row ≠ broken page); errors observable via logs + Prometheus |
| **D4** | Bounded context location | **Nuovo `Discover` BC** in `BoundedContexts/Discover/`. Composer dispatches 5 MediatR sub-queries (one per BC); each sub-query lives in its native BC; Discover BC owns only orchestration + DTO assembly | Single Responsibility (read-only composition); sub-queries reusable for future per-row pages; isolation prevents BC pollution |
| **D5** | Endpoint structure | **Single composite endpoint** `GET /api/v1/discover` returning all 5 rows in one response | FE single fetch (no waterfall); coherent with mockup `sp4-discover` page-render-once UX |
| **D6** | Pagination | **Top-N fixed**: default `limit=10`, max `limit=20` via query param `?limit=N`. No cursor — discover is a browse page, not infinite scroll | Coherent with codebase (small dashboards); avoids over-engineering |
| **D7** | Caching | **HybridCache per row** with row-specific TTL: newGames 10min · topAgents 30min · recommendedToolkits 30min · recentKb 5min · topContributors 1h. Tags: `discover:newGames`, `discover:topAgents`, etc., for granular invalidation | Different rows have different freshness characteristics (KB ingest is dynamic; contributors changes slowly); per-row cache amortizes load |
| **D8** | Authorization | `RequireSession()` only; **no admin gating** of fields. All 5 rows are user-facing, no admin-only diagnostic fields like in #730 | Discover serves all users equally; nothing privileged to hide |
| **D9** | Privacy filtering | All sub-queries filter `IsPublic=true` where applicable + `IsDeleted=false`. User PII (email, full name) never exposed; only `displayName` + optional `avatarUrl` | Standard data-minimization; respect existing privacy boundaries |
| **D10** | Sub-query independence | Each sub-query is a self-contained `IQuery<T>` + Handler in its native BC, callable independently of the composer | Reusable for future per-row dedicated pages; testable in isolation; composer is thin orchestrator |

---

## 3. SMART User Goals

### G1 — Composite endpoint orchestration with resilient partial success (cross-cutting)

> **As** a logged-in user opening `/discover`,
> **I want** to receive all 5 new rows in a single HTTP fetch, with graceful degradation if one sub-query fails (other 4 still render),
> **so that** the discovery page is not blocked by isolated sub-system issues.

**SMART criteria**:
- **S**: `GET /api/v1/discover` returns `DiscoverDto { newGames, topAgents, recommendedToolkits, recentKb, topContributors }`. Composer dispatches 5 MediatR sub-queries in parallel via `Task.WhenAll`, each wrapped in try-catch; failure → empty array + `_logger.LogError(ex, "Discover row {RowName} failed", ...)`
- **M**: P95 latency < 400ms (worst single sub-query dominates after parallelism); zero unhandled exceptions; per-row failure rate exposed via Prometheus `discover_row_failure_total{row=...}`
- **A**: composer in `BoundedContexts/Discover/Application/Queries/GetDiscoverDataHandler.cs`; sub-queries in respective BCs
- **R**: powers `/discover` mockup `sp4-discover` 5 missing rows; unblocks FE Wave 3 child #687
- **T**: this PR (~2 days)

**Gherkin scenarios**:

```gherkin
Feature: Discover composite endpoint resilient orchestration

  Scenario: All 5 queries succeed - full payload
    Given the user is authenticated as a regular user
    And the platform has data in all 5 source BCs
    When the user calls GET /api/v1/discover
    Then the response is 200 OK in <400ms (P95)
    And contains 5 non-null arrays: newGames, topAgents, recommendedToolkits, recentKb, topContributors
    And each array has up to 10 items (default top-N)

  Scenario: One sub-query fails - partial success
    Given the user is authenticated
    And the topAgents sub-query throws (simulated DB error)
    When the user calls GET /api/v1/discover
    Then the response is 200 OK (NOT 500)
    And contains topAgents=[] (empty array, not omitted)
    And the other 4 arrays are populated normally
    And a structured log entry exists with level=Error, row=topAgents, exception details
    And the Prometheus metric discover_row_failure_total{row="topAgents"} is incremented by 1

  Scenario: Unauthenticated request
    Given no session cookie
    When the request hits GET /api/v1/discover
    Then the response is 401 Unauthorized (RequireSession middleware)

  Scenario: Custom limit query param
    When the user calls GET /api/v1/discover?limit=5
    Then each of the 5 arrays contains at most 5 items

  Scenario: Limit out of range
    When the user calls GET /api/v1/discover?limit=50
    Then the response is 400 Bad Request "limit must be between 1 and 20"
```

---

### G2 — Catalog rows: New games + Top agents

> **As** a logged-in user looking for "what to play",
> **I want** to see the latest games added to the community catalog AND the AI agents most adopted by the community,
> **so that** I can explore catalog freshness AND discover popularity-validated agents.

**SMART criteria**:
- **S**:
  - `GetNewGamesQuery(int Limit)` in `BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/` → `NewGameDto { id, name, imageUrl?, publisher?, createdAt, ratingAverage? }`. SQL: `ORDER BY created_at DESC LIMIT @limit` on `shared_games` (filter `IsDeleted=false`)
  - `GetTopAgentsQuery(int Limit)` in `BoundedContexts/GameManagement/Application/Queries/GetTopAgents/` → `TopAgentDto { id, name, gameName, agentType, installCount, createdAt }`. SQL: `LEFT JOIN agent_installations` group-by + `ORDER BY install_count DESC, created_at DESC`
- **M**: each sub-query P95 < 80ms (cold), <30ms (cached); newGames freshness ≤ 10min stale; topAgents freshness ≤ 30min stale
- **A**: existing entities (`SharedGameEntity`; for topAgents — `AgentSessionEntity` as install-count proxy, see §5 verification table); HybridCache keys `discover:newGames:{limit}` and `discover:topAgents:{limit}` with tags `gameCatalog` and `agents` for invalidation
- **R**: powers "Giochi nuovi" + "Agenti più installati" rows
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: New games + top agents discovery rows

  Scenario: New games ordering
    Given 30 shared games exist with createdAt spanning 90 days
    When GetNewGamesQuery(Limit=10) executes
    Then the returned array has exactly 10 items
    And ordered by createdAt descending (newest first)
    And NO soft-deleted games (IsDeleted=true) are included

  Scenario: Top agents with zero installs handled gracefully
    Given 5 agents exist, 3 with installations and 2 without
    When GetTopAgentsQuery(Limit=10) executes
    Then 5 items returned
    And the 2 zero-install agents appear at the bottom (installCount=0, ordered by createdAt desc among ties)

  Scenario: Empty catalog state
    Given no shared games exist (fresh deployment)
    When GetNewGamesQuery executes
    Then returns empty array []
    And does NOT throw

  Scenario: Cache hit
    Given GetNewGamesQuery(Limit=10) was called 3 minutes ago
    When the same query fires
    Then it hits HybridCache (no DB round trip; verifiable via _logger.LogDebug "cache hit")
```

---

### G3 — Content rows: Recommended toolkits + Recent KB

> **As** a logged-in user wanting to "learn more" about games of interest,
> **I want** to see AI toolkit recommendations (ranked by install count) AND recently ingested KB documents cross-game,
> **so that** I can find AI-generated content (toolkit) AND human-curated content (KB) that's fresh.

**SMART criteria**:
- **S**:
  - `GetRecommendedToolkitsQuery(int Limit)` in `BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkits/` → `RecommendedToolkitDto { id, name, gameName, version, installCount, createdAt }`. Filter `IsPublic=true` only. SQL: `ORDER BY install_count DESC, created_at DESC` (MVP simple, see D2)
  - `GetRecentKbDocumentsQuery(int Limit)` in `BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocuments/` → `RecentKbDocDto { id, title, gameName, documentCategory, indexedAt, language }`. Filter `IsPublic=true` + `processingState='Ready'`. SQL: `ORDER BY indexed_at DESC`
- **M**: each P95 < 100ms (cold), <40ms (cached); recommendedToolkits freshness ≤ 30min, recentKb freshness ≤ 5min (KB more dynamic)
- **A**: existing entities (`GameToolkitEntity`, `PdfDocumentEntity` + `VectorDocumentEntity`); HybridCache tags `toolkits` and `kbDocs`
- **R**: powers "Toolkit consigliati" + "KB recenti" rows
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: Recommended toolkits + recent KB documents

  Scenario: Recommended toolkits filter out private
    Given 20 toolkits exist, 12 IsPublic=true, 8 IsPublic=false
    When GetRecommendedToolkitsQuery(Limit=10) executes
    Then returns up to 10 items, ALL with IsPublic=true
    And NO IsPublic=false toolkit leaks in result

  Scenario: Recent KB filters out non-Ready docs
    Given 30 KB docs exist: 20 Ready, 5 Embedding, 5 Failed
    When GetRecentKbDocumentsQuery(Limit=10) executes
    Then returns up to 10 items, ALL with processingState=Ready
    And ordered by indexedAt DESC

  Scenario: Recent KB cross-game aggregation
    Given KB docs span 5 different games (gameA: 5 docs, gameB: 3 docs, gameC: 2 docs, etc.)
    When GetRecentKbDocumentsQuery(Limit=10) executes
    Then returned docs may belong to any of the 5 games (no per-game grouping)
    And ordered purely by indexedAt DESC
    And gameName field is populated for each item (join PdfDocument → Game)
```

---

### G4 — Community row: Top contributors

> **As** a logged-in user (especially newcomer) wanting to identify "who are the community protagonists",
> **I want** to see the top 10 platform contributors ranked by sum of contributions (FAQs, KB uploads, agents created),
> **so that** I can identify community authorities and follow their work.

**SMART criteria**:
- **S**: `GetTopContributorsQuery(int Limit)` in `BoundedContexts/Authentication/Application/Queries/GetTopContributors/` → `TopContributorDto { userId, displayName, avatarUrl?, contributionCount, faqCount, kbUploadCount, agentCount }`. LINQ aggregation across 3 tables:
  ```csharp
  var query = from u in _dbContext.Users.AsNoTracking()
              where !u.IsDeleted
              let faqCount = _dbContext.GameFaqs.Count(f => f.AuthorId == u.Id)
              let kbCount = _dbContext.PdfDocuments.Count(p => p.UploadedByUserId == u.Id && p.IsPublic)
              let agentCount = _dbContext.AgentSessions.Where(a => a.CreatedBy == u.Id).Select(a => a.AgentId).Distinct().Count()
              let total = faqCount + kbCount + agentCount
              where total > 0  // exclude zero-contribution users
              orderby total descending, u.DisplayName
              select new TopContributorDto(u.Id, u.DisplayName, u.AvatarUrl, total, faqCount, kbCount, agentCount);
  return await query.Take(limit).ToListAsync(cancellationToken);
  ```
- **M**: P95 < 200ms (cold), <60ms (cached); cache TTL 1h (top contributors change slowly); MVP equal-weight sum (D2)
- **A**: existing entities (`UserEntity`, `GameFaqEntity`, `PdfDocumentEntity`, `AgentSessionEntity` proxy for `agentCount`); HybridCache tag `topContributors`. Final entity choices verified in Plan Task 0
- **R**: powers "Top contributor" row — particularly valuable for newcomer onboarding
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: Top contributors community ranking

  Scenario: Equal-weight sum ranking
    Given 5 users with contributions:
      | userId | faqs | kbUploads | agents | total |
      | u1     | 10   | 2         | 1      | 13    |
      | u2     | 0    | 8         | 5      | 13    |
      | u3     | 5    | 3         | 4      | 12    |
      | u4     | 1    | 0         | 0      | 1     |
      | u5     | 0    | 0         | 0      | 0     |
    When GetTopContributorsQuery(Limit=10) executes
    Then 4 items returned (u5 excluded by total > 0 filter)
    And first two items are u1 and u2 (tie at 13, internal ordering by displayName)
    And u3 is third (12)
    And u4 is fourth (1)

  Scenario: Soft-deleted users excluded
    Given user u6 has 50 contributions but IsDeleted=true
    When GetTopContributorsQuery executes
    Then u6 does NOT appear in result

  Scenario: Privacy: avatarUrl null when user has no avatar
    Given user u1 has displayName="Marco" but no avatarUrl set
    When the query returns u1
    Then u1.avatarUrl is null (not empty string, not omitted)
    And no email/full name fields exposed

  Scenario: Cache invalidation on new contribution
    Given GetTopContributorsQuery cached 30 minutes ago
    When a new FAQ is published by a user
    Then the cache tag "topContributors" is invalidated (via domain event)
    And next call hits DB fresh
```

---

## 4. API Contract — Endpoint Specification

### 4.1 `GET /api/v1/discover?limit=N`

**Auth**: `RequireSession()` (any authenticated user)
**Query params**:
- `limit` (optional, default `10`, range `[1, 20]`)

**Response 200** (`DiscoverDto`):

```ts
{
  newGames: NewGameDto[],
  topAgents: TopAgentDto[],
  recommendedToolkits: RecommendedToolkitDto[],
  recentKb: RecentKbDocDto[],
  topContributors: TopContributorDto[]
}

NewGameDto = {
  id: string (guid),
  name: string,
  imageUrl: string?,
  publisher: string?,
  createdAt: string (iso8601),
  ratingAverage: number?
}

TopAgentDto = {
  id: string (guid),
  name: string,
  gameName: string,
  agentType: string,
  installCount: number,
  createdAt: string (iso8601)
}

RecommendedToolkitDto = {
  id: string (guid),
  name: string,
  gameName: string,
  version: string,
  installCount: number,
  createdAt: string (iso8601)
}

RecentKbDocDto = {
  id: string (guid),
  title: string,
  gameName: string,
  documentCategory: string,
  indexedAt: string (iso8601),
  language: string
}

TopContributorDto = {
  userId: string (guid),
  displayName: string,
  avatarUrl: string?,
  contributionCount: number,
  faqCount: number,
  kbUploadCount: number,
  agentCount: number
}
```

**Errors**:
- `400 Bad Request` — `limit` out of range
- `401 Unauthorized` — no session
- (no `500` due to row failure — partial success per D3)

---

## 5. Data Model Changes

**None required.** All 5 sub-queries use existing entities. **Verified entity inventory** (confirmed via repo grep):
- `SharedGameEntity` (SharedGameCatalog BC) ✅
- `GameToolkitEntity` (GameToolkit BC) ✅
- `PdfDocumentEntity` + `VectorDocumentEntity` (KnowledgeBase BC + DocumentProcessing) ✅
- `UserEntity` (Authentication BC) ✅
- `GameFaqEntity` (SharedGameCatalog BC, not standalone `FaqEntity`) ✅
- `AgentSessionEntity` (KnowledgeBase BC) — **proxy for "agent installs"** since no dedicated `AgentInstallation` entity exists; see assumption below

### ⚠️ Pre-implementation verification (Plan Task 0)

The following entity assumptions need verification before implementing the corresponding sub-query:

| Sub-query | Cited entity | Reality | Mitigation |
|-----------|--------------|---------|-----------|
| `GetTopAgentsQuery` (G2) | `AgentEntity` + `AgentInstallationEntity` | **Neither exists** as standalone entity. `AgentSessionEntity` exists (chat sessions) | **Approach A**: aggregate `AgentSessionEntity` distinct `UserId × AgentId` count as install proxy. **Approach B**: skip this row in MVP, defer to follow-up issue once Agent BC introduces a proper definition + install model. Decision in Plan Task 0 after investigation. |
| `GetTopContributorsQuery` (G4) | `FaqEntity` | **Renamed `GameFaqEntity`** (in SharedGameCatalog BC) | Rename references in handler. Aggregate by `AuthorId` field on `GameFaqEntity` |
| `GetTopContributorsQuery` (G4) | `AgentEntity` for `agentCount` | Same as G2 | Use `AgentSessionEntity` distinct by `CreatedBy` user, OR skip the agent contribution count if no clean entity exists |

This is a major scope advantage over #730 (which required a 4-column migration), but the agent-install model gap means **the topAgents row may need MVP scope adjustment** during Plan Task 0 (investigation step).

---

## 6. Implementation Strategy

### 6.1 Sequencing (TDD-driven)

1. **Discover BC scaffolding**
   - Create `apps/api/src/Api/BoundedContexts/Discover/` directory tree (`Application/Queries/GetDiscoverData/`)
   - Create `DiscoverDto.cs` (composite wrapper)
   - Create `GetDiscoverDataQuery.cs` and skeleton `GetDiscoverDataHandler.cs` (returns empty DTO initially)

2. **G2 sub-query: GetNewGamesQuery**
   - `BoundedContexts/SharedGameCatalog/Application/Queries/GetNewGames/`
   - Tests + handler + DTO
   - Wire in composer (replace empty array with `_mediator.Send(new GetNewGamesQuery(limit))`)

3. **G2 sub-query: GetTopAgentsQuery**
   - `BoundedContexts/GameManagement/Application/Queries/GetTopAgents/`
   - LEFT JOIN with installations, group-by handling
   - Tests + handler + DTO
   - Wire in composer

4. **G3 sub-query: GetRecommendedToolkitsQuery**
   - `BoundedContexts/GameToolkit/Application/Queries/GetRecommendedToolkits/`
   - Tests + handler + DTO
   - Wire in composer

5. **G3 sub-query: GetRecentKbDocumentsQuery**
   - `BoundedContexts/KnowledgeBase/Application/Queries/GetRecentKbDocuments/`
   - Tests + handler + DTO (similar to existing `GetGameDocumentsQuery` pattern but cross-game)
   - Wire in composer

6. **G4 sub-query: GetTopContributorsQuery**
   - `BoundedContexts/Authentication/Application/Queries/GetTopContributors/`
   - LINQ aggregation across Users + Faqs + PdfDocuments + Agents
   - Tests + handler + DTO
   - Wire in composer

7. **G1 composer with Task.WhenAll + try-catch + structured logging**
   - Implement `GetDiscoverDataHandler.Handle` with parallel dispatch
   - Add Prometheus counter `discover_row_failure_total`
   - Unit test with NSubstitute mocks for IMediator (verify each row fail-tolerance)

8. **HybridCache wrapping per row**
   - Wrap each sub-query call in `IHybridCacheService.GetOrCreateAsync` with per-row TTL + tags
   - Tag invalidation hooks (deferred — see Out of Scope)

9. **Routing wiring**
   - Add `MapDiscoverEndpoints` to `KnowledgeBaseEndpoints.cs` OR new `DiscoverEndpoints.cs` partial (decide based on file size)
   - `RequireSession()`, OpenAPI/Scalar annotations

10. **Frontend Zod schemas + hook**
    - `apps/web/src/lib/api/schemas/discover.schemas.ts` (6 Zod schemas + composite)
    - `apps/web/src/lib/api/clients/discoverClient.ts` (1 method `getDiscover`)
    - `apps/web/src/hooks/queries/useDiscover.ts` (single hook returning composite DTO)

### 6.2 Test strategy

- **Unit tests**: each sub-query handler tested in isolation with `TestDbContextFactory.CreateInMemoryDbContext()` (the pattern established in #730)
- **Composer test**: NSubstitute mock `IMediator`, simulate each sub-query independently (success/failure combinations)
- **Integration test** (Testcontainers): single happy-path E2E hitting `/api/v1/discover` with seeded data across 5 BCs
- Target coverage: 90%+ for new code

### 6.3 Observability

- Structured log per sub-query: `_logger.LogDebug("Discover row {RowName} loaded {Count} items in {Ms}ms", ...)`
- Prometheus counter: `discover_row_failure_total{row="newGames|topAgents|...|topContributors"}`
- HybridCache hit/miss metrics already exposed

---

## 7. Risks & Mitigations

| # | Risk | Likelihood | Impact | Mitigation |
|---|------|-----------|--------|------------|
| R1 | `Task.WhenAll` parallel dispatch overloads DB on cold cache | Low | Medium | Each sub-query is a single LIMIT 20 query with index hits; max 5 parallel queries per request is well within DB pool capacity |
| R2 | Top contributors LINQ generates expensive cross-table sub-queries | Medium | Medium | Cache 1h TTL + tag invalidation amortizes. If query > 200ms, refactor to materialized view (follow-up issue) |
| R3 | DTO contract divergence with parallel migration wave terminal (FE child #687) | Medium | High | Publish Zod schemas early in the same PR; coordinate via PR comment |
| R4 | Privacy leak: user PII (email, full name) accidentally in TopContributorDto | Low | High | DTO is `internal sealed record` with explicit fields (no entity passthrough); test asserts no email field exists |
| R5 | Cache stampede on TTL expiry | Low | Medium | HybridCache library handles single-flight semantics by default |
| R6 | Empty platform state (fresh deploy, no users/games) | Medium | Low | Each sub-query handles empty result gracefully → empty array, not exception |
| R7 | Tag-based cache invalidation not wired (e.g., new FAQ doesn't invalidate topContributors) | High | Low | Acknowledged in DoD — invalidation is a follow-up. Until then, 1h TTL provides bounded staleness |

---

## 8. Out of Scope (follow-up issues)

| Topic | Tracked as |
|-------|------------|
| Cache invalidation hooks (domain events → tag invalidation) | New issue: "Discover cache invalidation via domain events" |
| KB-context aware toolkit ranking (per-user game context) | Future issue when ML/personalization rollout starts |
| Time-decayed contributor ranking | Future issue if metrics show stagnant top-list |
| Per-row dedicated pages (e.g., `/discover/new-games`) | Future Wave (sub-queries already callable independently — D10) |
| WebSocket / SSE realtime push of discover data | YAGNI for browse use case |
| Personalization based on user library / preferences | Distinct feature scope (separate epic) |
| Materialized view for top contributors if query > 200ms | Reactive — only if R2 manifests |

---

## 9. Definition of Done

- [ ] Nuovo `Discover` BC created with `GetDiscoverDataQuery` + DTO + Handler
- [ ] 5 sub-queries implemented in their respective BCs (SharedGameCatalog, GameManagement, GameToolkit, KnowledgeBase, Authentication)
- [ ] Unit tests for each sub-query handler (using `TestDbContextFactory.CreateInMemoryDbContext()`)
- [ ] Composer unit test verifying partial-success semantics (NSubstitute mock IMediator)
- [ ] Integration test (Testcontainers) covering happy-path 5-row response
- [ ] HybridCache wrapping with per-row TTL + tags
- [ ] Endpoint wired in `KnowledgeBaseEndpoints.cs` (or new `DiscoverEndpoints.cs` if file growth warrants split)
- [ ] OpenAPI/Scalar annotations present (`Produces<DiscoverDto>()`, `WithSummary`, `WithDescription`)
- [ ] Prometheus counter `discover_row_failure_total` registered
- [ ] Frontend Zod schemas in `apps/web/src/lib/api/schemas/discover.schemas.ts`
- [ ] Frontend `discoverClient.getDiscover()` method
- [ ] Frontend `useDiscover` React Query hook
- [ ] Coverage ≥ 90% on new code
- [ ] PR opened against `main-dev` (parent branch)
- [ ] Close-out comment on #687 with endpoint URL + sample response
- [ ] Issue #728 closed

---

## 10. References

- Issue #728 — backend audit
- Issue #687 — Wave 3 child `/discover`
- Issue #681 — Wave 3 umbrella
- Migration spec — `docs/superpowers/specs/2026-04-26-v2-design-migration.md` §3.4 (Phase 0.5 contract pattern)
- Mockup — `admin-mockups/design_files/sp4-discover.{html,jsx}`
- Stub components — `apps/web/src/components/v2/discover/`
- Pattern reference (similar issue) — `docs/superpowers/specs/2026-05-06-kb-chunk-endpoints-design.md` (issue #730)

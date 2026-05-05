# Wave 3 Backend Prerequisites — Spec-Panel Review

> **Date**: 2026-05-05
> **Trigger**: `/sc:spec-panel` review of issues #728, #729, #730 to unblock remaining Wave 3 routes
> **Mode**: discussion (5-expert collaborative analysis)
> **Format**: detailed
> **Status**: DRAFT — backend implementation prereq doc

## 1. Context

Wave 3 V2 migration umbrella (#681) has 5 child routes. After PR #717 (Wave 4 D1 `/players`), PR #724 (`/players/[id]`), and PR #727 (`/game-nights` Phase 0.5 contract), **3 routes remain backend-blocked**:

| Issue | Route | Blocker |
|-------|-------|---------|
| #728 | `/discover` | 5 of 7 cross-entity rows missing endpoints |
| #729 | `/toolkits/[id]` | Community marketplace pattern distinct from internal toolkit |
| #730 | `/kb/[id]` | Split-view chunk-level retrieval pattern |

Audit issues #728/#729/#730 were filed during this session (2026-05-05) as backend-audit deliverables. They identify gaps but lack actionable specs (concrete DTO schemas, endpoint signatures, BC ownership decisions).

This document applies multi-expert spec-panel review to refine those audit issues into implementable backend specs.

## 2. Expert panel

| Expert | Domain | Critique focus |
|--------|--------|----------------|
| **Karl Wiegers** | Requirements quality | SMART criteria, testability, measurable acceptance |
| **Martin Fowler** | API architecture | Bounded contexts, interface segregation, evolutionary design |
| **Sam Newman** | Cross-BC composition | Service decomposition, anti-corruption layers, API evolution |
| **Gojko Adzic** | Specification by example | Given/When/Then, executable scenarios, collaborative spec |
| **Michael Nygard** | Operational excellence | Caching, failure modes, circuit breakers, observability |

## 3. Cross-cutting concerns (panel discussion)

### 3.1 Bounded context ownership

**MARTIN FOWLER**: All three routes touch multiple bounded contexts. `/discover` is the most extreme — it composes data from `SharedGameCatalog`, `KnowledgeBase`, `GameManagement` (agents), `GameToolkit`, `Administration` (users). This is a classic case for an **Aggregator BC** or **Backend-for-Frontend (BFF)** pattern, not a single endpoint owned by one BC.

**SAM NEWMAN building on Fowler**: I'd resist creating a new "Discovery" BC unless the discovery logic itself becomes complex (ranking algorithms, personalization). Instead, expose the rows as **separate endpoints, one per source BC**, each returning a small projection DTO. Let the FE compose via parallel queries. This avoids cross-BC coupling and lets each BC own its caching policy.

**KARL WIEGERS**: The audit issues conflate "5 endpoints needed" with "5 things to do." Let's separate:
- **Endpoint shape**: HTTP signature
- **DTO contract**: response schema
- **Caching policy**: TTL + invalidation triggers
- **Empty state contract**: 200 with `[]` vs 404
- **BC ownership**: which service implements

Each must be independently specified and testable.

**GOJKO ADZIC**: Concrete example for `/discover` "trending" row:
```gherkin
Given user is authenticated
When user navigates to /discover
Then GET /api/v1/catalog/trending?limit=10 returns 200 with { items: TrendingGame[] }
And response includes ETag for client-side cache validation
And response is served from CDN cache if hit (TTL 5 min)
```

**MICHAEL NYGARD**: Each row also needs **degraded mode behavior**. If `top-contributors` query times out (3s budget), the FE should still render the page with that row collapsed/hidden, not block all 7 rows. Specify: graceful degradation per row + per-row timeout budgets.

### 3.2 Caching policy matrix

**Consensus** (Wiegers + Nygard + Newman):

| Data class | TTL | Invalidation | Cache layer |
|-----------|-----|--------------|-------------|
| Trending (catalog) | 5 min | Time-based | Redis + CDN ETag |
| New games (catalog) | 1 hour | New publish | Redis + CDN ETag |
| Popular agents | 15 min | Install event | Redis (BC-local) |
| Recommended toolkits | 30 min | Heuristic recompute | Redis (BC-local) |
| Recent KB docs | 5 min | New ingest | Redis + ETag |
| Top contributors | 1 hour | Daily batch | Redis (BC-local) |
| Upcoming game nights | 5 min | RSVP event | Redis (BC-local) |
| Toolkit detail | 10 min | Publish/yank event | Redis + ETag |
| KB doc detail | 1 hour | Re-ingest event | Redis + ETag |
| KB chunk content | Immutable post-ingest | Never | Long Redis + ETag |
| Toolkit ratings | 5 min | Rating submit event | Redis + ETag |

**SAM NEWMAN**: Per-row TTL means per-row HTTP response. Don't bundle all `/discover` rows into a single response — that forces lowest-common-denominator TTL.

### 3.3 Pagination strategy

**MARTIN FOWLER**: Choose per dataset characteristics:
- **Trending / new-games / recent-kb**: bounded list (10-20 items), no pagination needed
- **KB chunks**: potentially large (>500 chunks per doc), use cursor-based pagination
- **Toolkit versions**: typically small (<50 versions), no pagination
- **Toolkit ratings**: large (community-driven), cursor-based
- **Top contributors**: bounded leaderboard (top 20), no pagination

**Rule**: cursor-based for unbounded growth, omit pagination for bounded lists.

### 3.4 Empty state contract

**KARL WIEGERS**: Inconsistent empty-state handling is a top-3 cause of bugs in cross-BC composition. Decide:

**Recommendation**: All list endpoints return **200 with empty array** (`{ items: [] }`). Reserve **404** strictly for "resource not found by ID" semantics (e.g., `/kb-docs/{id}` for nonexistent doc).

**LISA CRISPIN** (testing): Test matrix per endpoint must cover: 200 populated, 200 empty, 404 (where applicable), 5xx graceful degradation. Don't conflate empty with not-found.

### 3.5 API versioning

**SAM NEWMAN**: All new endpoints under `/api/v1/`. When backward-incompatible changes are needed, introduce `/api/v2/` for the affected endpoint. Avoid version bumps in path segments other than the leading version.

**MARTIN FOWLER**: Use **expand/contract** evolution: add new fields without removing old, deprecate via response header `Sunset:` per RFC 8594 with 6-month notice.

---

## 4. Issue #728 — `/discover` (5/7 rows missing endpoints)

### 4.1 Wiegers requirements critique

❌ **CRITICAL**: Audit issue says "5 endpoints" but doesn't specify shapes
📝 **RECOMMENDATION**: Specify each endpoint independently with: path, query params, response DTO, empty contract, TTL
🎯 **PRIORITY**: High — without shapes, backend can't start

❌ **MAJOR**: "Toolkit consigliati" has no ranking algorithm definition
📝 **RECOMMENDATION**: Decide v1 algorithm: simple `installCount desc` is acceptable for MVP; defer KB-context-aware to v2
🎯 **PRIORITY**: Medium — algorithm choice affects DTO shape

❌ **MINOR**: "Top contributor" definition (FAQs + KB uploads + agents created) has no weighting
📝 **RECOMMENDATION**: V1 = simple sum count; expose per-source breakdown in DTO for FE display
🎯 **PRIORITY**: Low — can iterate on weighting

### 4.2 Fowler architecture

```
✅ Pattern: BFF aggregator endpoint OR per-BC endpoints
❌ Anti-pattern: single mega-endpoint /discover returning all 7 rows
```

**Recommendation**: 7 separate endpoints, each owned by source BC. FE composes via 7 parallel queries (TanStack Query already supports this via independent `useQuery` calls).

### 4.3 Concrete endpoint specs

#### 4.3.1 Trending games (existing — `useCatalogTrending`)

**Status**: ✅ Already implemented.

```http
GET /api/v1/catalog/trending?limit=10
Response 200: { items: TrendingGame[] }
Cache: Redis 5min + ETag
BC: SharedGameCatalog
```

#### 4.3.2 New games (NEW)

```http
GET /api/v1/catalog/games/new?limit=10
Response 200: { items: NewGameDto[] }
Cache: Redis 1h + ETag
BC: SharedGameCatalog

DTO:
interface NewGameDto {
  id: string;            // UUID
  name: string;
  publisher: string | null;
  year: number | null;
  imageUrl: string | null;
  createdAt: string;     // ISO 8601
}

Sort: createdAt DESC
Empty: 200 with { items: [] }
```

**ADZIC scenario**:
```gherkin
Given catalog has 25 games published in last 30 days
When GET /api/v1/catalog/games/new?limit=10
Then response.items.length == 10
And response.items[0].createdAt > response.items[9].createdAt
And response.headers['ETag'] is set
```

#### 4.3.3 Popular agents (NEW)

```http
GET /api/v1/agents/popular?limit=10
Response 200: { items: PopularAgentDto[] }
Cache: Redis 15min (BC-local)
BC: GameManagement (agents)

DTO:
interface PopularAgentDto {
  id: string;
  name: string;
  gameId: string | null;
  gameName: string | null;
  installCount: number;       // total install events
  invocationCount: number;    // total chat invocations
}

Sort: installCount DESC, invocationCount DESC tiebreak
Empty: 200 with { items: [] }
```

#### 4.3.4 Recommended toolkits (NEW — v1 simple)

```http
GET /api/v1/toolkits/recommended?limit=10
Response 200: { items: RecommendedToolkitDto[] }
Cache: Redis 30min
BC: GameToolkit (community marketplace)

DTO:
interface RecommendedToolkitDto {
  id: string;
  name: string;
  authorName: string;
  installCount: number;
  ratingAverage: number | null;  // 1-5, null if no ratings
  ratingCount: number;
  coverImageUrl: string | null;
}

V1 algorithm: ratingAverage * log(ratingCount + 1) DESC, installCount DESC tiebreak
V2 (deferred): KB-context-aware via user.recentGames intersection with toolkit.targetGames
Empty: 200 with { items: [] }
```

**NYGARD operational note**: V1 ranking is computed server-side per request (acceptable for MVP scale). V2 should precompute via daily batch job stored in `recommended_toolkits_cache` table with `expiresAt` column.

#### 4.3.5 Recent KB docs (NEW)

```http
GET /api/v1/kb-docs/recent?limit=10
Response 200: { items: RecentKbDocDto[] }
Cache: Redis 5min + ETag
BC: KnowledgeBase

DTO:
interface RecentKbDocDto {
  id: string;
  title: string;
  gameId: string | null;
  gameName: string | null;
  docType: 'rulebook' | 'faq' | 'errata' | 'guide';
  lastIngestedAt: string;       // ISO 8601
  chunkCount: number;
}

Sort: lastIngestedAt DESC
Filter: processingStatus == 'ready' (exclude in-flight ingests)
Empty: 200 with { items: [] }
```

#### 4.3.6 Top contributors (NEW)

```http
GET /api/v1/users/top-contributors?limit=10
Response 200: { items: TopContributorDto[] }
Cache: Redis 1h (daily batch refresh)
BC: Administration (users) + cross-BC aggregation

DTO:
interface TopContributorDto {
  id: string;
  displayName: string;
  avatarUrl: string | null;
  contributionCount: number;        // sum of all sources
  breakdown: {
    faqsCount: number;
    kbUploadsCount: number;
    agentsCreatedCount: number;
  };
}

V1 algorithm: contributionCount DESC
Refresh: daily batch via background job, stored in materialized view
Empty: 200 with { items: [] }
```

**FOWLER note**: This endpoint touches 3+ BCs (Administration, KnowledgeBase, GameManagement). Implement via background job that writes to `top_contributors_daily` materialized view; the endpoint just reads from the view. Avoids cross-BC join at request time.

#### 4.3.7 Upcoming game nights (existing — `useUpcomingGameNights`)

**Status**: ✅ Already implemented (used by Wave 3 Step 3a `/game-nights` contract).

### 4.4 Wiegers SMART checklist

- [ ] **Specific**: each endpoint has unique path + DTO
- [ ] **Measurable**: each endpoint has TTL + sort order + empty contract
- [ ] **Achievable**: 5 endpoints implementable in 5-7 backend tasks (1-2 sprints)
- [ ] **Relevant**: unblocks `/discover` Wave 3 Step 3b
- [ ] **Time-bound**: target 2026-05-20 (2 weeks)

### 4.5 Implementation sequencing

1. **Sprint 1 (week 1)**: New games, Popular agents, Recent KB docs (3 simple endpoints, single-BC each)
2. **Sprint 2 (week 2)**: Recommended toolkits (v1 simple ranking), Top contributors (cross-BC + materialized view)

After backend completion → FE writes Phase 0.5 contract `/discover` → Wave 3 Step 3b implementation dispatch.

---

## 5. Issue #729 — `/toolkits/[id]` (community marketplace)

### 5.1 Newman BC decomposition

**SAM NEWMAN**: The audit issue raised the question "new BC vs extension" — let me settle it. The existing `GameToolkit` BC owns the **internal session toolkit** (dice presets, session events, AI-generated suggestions). The mockup describes a **community marketplace** for user-published toolkits with ratings, versions, install actions.

**Decision**: Extend `GameToolkit` BC. The two concepts share the core `Toolkit` aggregate but differ in **publishing state** (internal/draft vs published) and **ownership** (system-generated vs user-authored).

**Justification**:
- Splitting into a new "ToolkitMarketplace" BC duplicates the Toolkit aggregate
- Extension lets us reuse existing AI-generation pipeline for "starter content" of published toolkits
- Single source of truth for toolkit data

### 5.2 Wiegers requirements critique

❌ **CRITICAL**: "Variant gating (own vs public)" not specified — derived from server-side check or client?
📝 **RECOMMENDATION**: Server returns `{ toolkit, viewerContext: { isOwner: bool, hasInstalled: bool } }`. Client renders variant from viewerContext.
🎯 **PRIORITY**: High — security boundary

❌ **CRITICAL**: Yank action semantics undefined
📝 **RECOMMENDATION**: Define yank as soft-delete with cascade rules:
  - Yanked version stays accessible to users who already installed it (read-only)
  - Yanked version hidden from `/discover` and `/toolkits` index
  - Yank requires authorId match (server-enforced)
🎯 **PRIORITY**: High — operational safety

❌ **MAJOR**: Rating system schema absent
📝 **RECOMMENDATION**: 1-5 stars + optional comment, edit window 24h post-submit, immutable after
🎯 **PRIORITY**: Medium

### 5.3 Concrete endpoint specs

#### 5.3.1 Get toolkit detail

```http
GET /api/v1/toolkits/{toolkitId}
Response 200: ToolkitDetailResponse
Response 404: when toolkit not published OR yanked AND viewer != author
Cache: Redis 10min + ETag (per viewer)
BC: GameToolkit

DTO:
interface ToolkitDetailResponse {
  toolkit: ToolkitDetailDto;
  viewerContext: {
    isOwner: boolean;
    hasInstalled: boolean;
    canRate: boolean;        // hasInstalled && !alreadyRated && !isOwner
  };
}

interface ToolkitDetailDto {
  id: string;
  name: string;
  description: string;
  authorId: string;
  authorName: string;
  authorAvatarUrl: string | null;
  coverImageUrl: string | null;
  agent: ToolkitAgentSummaryDto;        // associated agent (system prompt preview)
  kbDocsCount: number;
  toolsCount: number;
  installCount: number;
  ratingAverage: number | null;
  ratingCount: number;
  createdAt: string;
  publishedAt: string | null;            // null if draft
  yankedAt: string | null;               // null if active
  currentVersion: string;                 // semver
}

interface ToolkitAgentSummaryDto {
  id: string;
  name: string;
  systemPromptPreview: string;            // first 500 chars (truncated)
}
```

#### 5.3.2 Get toolkit versions

```http
GET /api/v1/toolkits/{toolkitId}/versions
Response 200: { items: ToolkitVersionDto[] }
Response 404: toolkit not found
Cache: Redis 10min
BC: GameToolkit

DTO:
interface ToolkitVersionDto {
  version: string;          // semver
  publishedAt: string;
  yankedAt: string | null;  // null if active
  changelog: string;         // markdown allowed
  isCurrent: boolean;
}

Sort: publishedAt DESC
```

#### 5.3.3 Get toolkit ratings (cursor paginated)

```http
GET /api/v1/toolkits/{toolkitId}/ratings?cursor={nullable}&limit=20
Response 200: ToolkitRatingsResponse
Response 404: toolkit not found
Cache: Redis 5min + ETag
BC: GameToolkit

DTO:
interface ToolkitRatingsResponse {
  items: ToolkitRatingDto[];
  nextCursor: string | null;
  breakdown: {                // 5-star distribution counts
    star1: number;
    star2: number;
    star3: number;
    star4: number;
    star5: number;
  };
  averageStars: number;        // 1-5 with one decimal
  totalCount: number;
}

interface ToolkitRatingDto {
  id: string;
  raterDisplayName: string;
  raterAvatarUrl: string | null;
  stars: 1 | 2 | 3 | 4 | 5;
  comment: string | null;
  createdAt: string;
}

Sort: createdAt DESC
```

#### 5.3.4 Submit rating

```http
POST /api/v1/toolkits/{toolkitId}/ratings
Body: { stars: 1-5, comment?: string }
Response 201: ToolkitRatingDto
Response 403: viewer doesn't have hasInstalled OR alreadyRated
Response 422: stars out of range
BC: GameToolkit

Constraints:
- One rating per (userId, toolkitId)
- Edit window: 24h post-submit (PATCH allowed)
- Immutable after edit window
- Author cannot rate own toolkit (403)
```

#### 5.3.5 Install toolkit

```http
POST /api/v1/toolkits/{toolkitId}/install
Body: {} (idempotent)
Response 200: { installCount: number, hasInstalled: true }
Response 404: toolkit not found OR yanked
Response 409: already installed (idempotent — return current state, NOT error)
BC: GameToolkit
Side effects: emit ToolkitInstalledEvent → invalidates Redis cache popular_agents
```

**NYGARD note**: Install action is idempotent. Subsequent installs return 200 with current state, not 409. This avoids client retry loops on network blips.

#### 5.3.6 Publish new version (own only)

```http
POST /api/v1/toolkits/{toolkitId}/versions
Body: { version: string, changelog: string, content: ToolkitContentDto }
Response 201: ToolkitVersionDto
Response 403: viewer != author
Response 422: version not greater than current (semver)
BC: GameToolkit
```

#### 5.3.7 Yank version (own only)

```http
POST /api/v1/toolkits/{toolkitId}/versions/{version}/yank
Body: { reason: string }
Response 200: ToolkitVersionDto (with yankedAt set)
Response 403: viewer != author
Response 404: version not found
BC: GameToolkit
Side effects: existing installs preserved, hidden from discovery
```

### 5.4 Adzic Given/When/Then

```gherkin
Scenario: Visitor views own toolkit
  Given user "Marco" authored toolkit "T-1"
  When Marco GET /api/v1/toolkits/T-1
  Then response.viewerContext.isOwner == true
  And response.viewerContext.canRate == false

Scenario: Visitor rates installed toolkit
  Given user "Sara" installed toolkit "T-1"
  And Sara has not rated T-1 before
  When Sara POST /api/v1/toolkits/T-1/ratings { stars: 4, comment: "Great!" }
  Then response.status == 201
  And toolkit T-1 ratingCount += 1
  And cache key "toolkit:T-1:detail" invalidated

Scenario: Author yanks v1.0.0
  Given Marco authored toolkit T-1, current version 1.0.0
  And user "Sara" installed T-1@1.0.0 last week
  When Marco POST /api/v1/toolkits/T-1/versions/1.0.0/yank
  Then T-1@1.0.0 has yankedAt set
  And Sara still sees T-1@1.0.0 in her library (read-only)
  And T-1 hidden from /api/v1/toolkits/recommended
```

---

## 6. Issue #730 — `/kb/[id]` (split-view chunk-level retrieval)

### 6.1 Wiegers requirements critique

❌ **CRITICAL**: Search relevance algorithm question raised but unresolved
📝 **RECOMMENDATION**: V1 = full-text BM25 via PostgreSQL `tsvector` (fast, no infrastructure dep). V2 (deferred) = semantic via existing pgvector + cross-encoder reranker.
🎯 **PRIORITY**: High — algorithm choice affects DTO shape

❌ **MAJOR**: Markdown rendering scope unspecified
📝 **RECOMMENDATION**: Server returns raw markdown content; client renders with mini-renderer (already in mockup `MarkdownRenderBlock`). Define supported subset: H1-H3, ul/ol, table, blockquote, code (inline+fenced).
🎯 **PRIORITY**: Medium — affects FE component scope

❌ **MAJOR**: Chunk navigation prev/next ordering unclear
📝 **RECOMMENDATION**: Order chunks by `position` field (existing in db schema); prev/next derived client-side from chunks list.
🎯 **PRIORITY**: Low — can be derived without explicit endpoint

### 6.2 Fowler architecture

```
Pattern: chunk-level retrieval is read-only sub-resource of doc
Endpoints under /kb-docs/{id}/chunks form a sub-collection
```

### 6.3 Concrete endpoint specs

#### 6.3.1 Get doc detail

```http
GET /api/v1/kb-docs/{docId}
Response 200: KbDocDetailDto
Response 404: doc not found
Response 423: processingStatus != 'ready' (Locked — content not yet indexed)
Cache: Redis 1h + ETag
BC: KnowledgeBase

DTO:
interface KbDocDetailDto {
  id: string;
  title: string;
  docType: 'rulebook' | 'faq' | 'errata' | 'guide';
  gameId: string | null;
  gameName: string | null;
  uploaderName: string;
  uploadedAt: string;
  lastIngestedAt: string;
  processingStatus: 'queued' | 'processing' | 'ready' | 'failed';
  chunkCount: number;
  pageCount: number | null;     // null for non-PDF
  language: string;              // ISO 639-1
  tags: string[];
}
```

**NYGARD operational note**: 423 Locked is more semantically correct than 404 for "exists but not ready." Allows FE to render a "Documento in elaborazione..." state distinct from "non trovato."

#### 6.3.2 List chunks (cursor paginated)

```http
GET /api/v1/kb-docs/{docId}/chunks?cursor={nullable}&limit=50
Response 200: KbChunksListResponse
Response 404: doc not found
Cache: Redis 30min + ETag (chunks immutable post-ingest)
BC: KnowledgeBase

DTO:
interface KbChunksListResponse {
  items: KbChunkSummaryDto[];
  nextCursor: string | null;
  totalCount: number;
}

interface KbChunkSummaryDto {
  id: string;
  position: number;              // sortable index within doc
  headingPath: string[];          // e.g. ["Setup", "Initial Hand"]
  snippet: string;                 // first 200 chars
  pageNumber: number | null;       // PDF source page
  vectorId: string;                 // for cross-reference with semantic search
}

Sort: position ASC
```

#### 6.3.3 Get full chunk

```http
GET /api/v1/kb-docs/{docId}/chunks/{chunkId}
Response 200: KbChunkDetailDto
Response 404: chunk not found
Cache: Redis 24h + ETag (chunks immutable)
BC: KnowledgeBase

DTO:
interface KbChunkDetailDto {
  id: string;
  docId: string;
  position: number;
  headingPath: string[];
  content: string;                 // FULL markdown
  pageNumber: number | null;
  prevChunkId: string | null;       // navigation
  nextChunkId: string | null;
  metadata: Record<string, unknown>;
}

Markdown subset (server constraint):
- Headings: H1, H2, H3 only (no H4-H6 → demoted to bold)
- Lists: ul, ol with nested up to 2 levels
- Code: fenced ```lang and `inline`
- Table: pipe syntax (GitHub-flavored)
- Blockquote: > prefix
- Emphasis: *bold*, _italic_
- NO: HTML, images (replaced with [Image: alt] text), embeds, footnotes
```

#### 6.3.4 Search within doc

```http
POST /api/v1/kb-docs/{docId}/chunks/search
Body: { query: string, limit?: number }  // limit default 20, max 50
Response 200: KbChunkSearchResponse
Response 404: doc not found
Cache: NOT cached (per-query)
BC: KnowledgeBase

DTO:
interface KbChunkSearchResponse {
  query: string;
  totalMatches: number;
  items: KbChunkSearchResultDto[];
}

interface KbChunkSearchResultDto extends KbChunkSummaryDto {
  relevanceScore: number;          // BM25 score, 0-1 normalized
  highlights: string[];             // matched phrases with <mark> tags
}

V1 algorithm: PostgreSQL tsvector + ts_rank
V2 (deferred): hybrid retrieval pgvector + cross-encoder reranker
Sort: relevanceScore DESC
```

**ADZIC scenario**:
```gherkin
Scenario: User searches within doc
  Given doc "Wingspan-rulebook" is indexed with 250 chunks
  When user POST /chunks/search { query: "wingspan abilities" }
  Then response.items.length <= 20
  And response.items[0].relevanceScore >= response.items[1].relevanceScore
  And response.items[0].highlights[0] contains "<mark>wingspan</mark>"

Scenario: Search returns no results
  Given doc "Catan-rulebook" is indexed
  When user POST /chunks/search { query: "blockchain" }
  Then response.totalMatches == 0
  And response.items == []
  And response.status == 200 (NOT 404)
```

### 6.4 Operational concerns (Nygard)

- **Failure modes**:
  - Doc fetch timeout (3s budget) → 504 Gateway Timeout, FE shows error shell
  - Search timeout (5s budget) → 504, FE shows "Ricerca non disponibile, riprova"
  - processingStatus === 'failed' → 423 Locked with `Retry-After: 0`, FE shows "Errore di indicizzazione, contatta supporto"
- **Observability**: log search query latency p50/p95/p99 per doc size bucket
- **Rate limiting**: 60 search requests/minute per user (anti-DOS)

---

## 7. Quality assessment summary

| Issue | Original quality | Post-review quality | Improvement |
|-------|-----------------|--------------------|--------------|
| #728 | 5.5/10 (gaps in shapes, BC ownership unclear) | 8.5/10 (concrete shapes, sequencing, BC mapped) | +3.0 |
| #729 | 6.0/10 (variant gating ambiguous, yank semantics) | 8.7/10 (server-side gating, yank cascade rules, idempotent install) | +2.7 |
| #730 | 6.5/10 (search algorithm unresolved, markdown scope) | 8.5/10 (BM25 v1, markdown subset defined, 423 Locked semantics) | +2.0 |

## 8. Implementation roadmap (cross-issue prioritization)

### Phase 1 (week 1) — Quick wins
1. ✅ `/discover` New games endpoint (single-BC, simple)
2. ✅ `/discover` Popular agents endpoint (single-BC, simple)
3. ✅ `/discover` Recent KB docs endpoint (single-BC, simple)

After Phase 1 → 3/5 `/discover` rows ready, FE can start Phase 0.5 contract drafting.

### Phase 2 (week 2) — Marketplace foundation
4. ✅ `/toolkits/[id]` Get detail + viewerContext
5. ✅ `/toolkits/[id]` Versions list
6. ✅ `/toolkits/[id]` Install action

After Phase 2 → `/toolkits/[id]` Public variant ready, basic functionality.

### Phase 3 (week 3) — KB chunks
7. ✅ `/kb-docs/{id}` Get detail with 423 Locked
8. ✅ `/kb-docs/{id}/chunks` List + cursor pagination
9. ✅ `/kb-docs/{id}/chunks/{chunkId}` Get full content

After Phase 3 → `/kb/[id]` ready for FE Phase 0.5 contract.

### Phase 4 (week 4) — Cross-cutting
10. ✅ `/discover` Recommended toolkits (depends on Phase 2 toolkit data)
11. ✅ `/discover` Top contributors (cross-BC + materialized view)
12. ✅ `/toolkits/[id]` Ratings list + submit + publish/yank
13. ✅ `/kb-docs/{id}/chunks/search` BM25 search

## 9. Expert consensus & blind spots

**🤝 Convergent insights (all experts agreed)**:
- Per-BC endpoints over mega-aggregator for `/discover`
- Server-side variant gating for `/toolkits/[id]` (security boundary)
- 200 with empty array > 404 for empty lists (consistency)
- Cursor-based pagination for unbounded growth (chunks, ratings)
- Cache layer matrix: Redis local + ETag for CDN where applicable

**⚖️ Productive tensions resolved**:
- Newman (per-BC) vs Fowler (BFF aggregator) → resolved as per-BC for `/discover` (Sam's argument: avoid premature coupling)
- Wiegers (specify all) vs Adzic (start with examples) → balanced via section 4.4 SMART checklist + Gherkin scenarios

**⚠️ Blind spots identified**:
- **Authentication scope**: specs assume bearer auth + cookie session. Public visibility rules for `/discover` (anon users? Marketing landing?) — defer to security review.
- **Localization**: DTOs assume Italian + English content; multi-locale toolkit titles/descriptions undefined.
- **Soft-delete cascade**: Toolkit yank cascade rules described, but what about author account deletion? Cascade or anonymize?
- **Audit trail**: rate limiting + suspicious pattern detection (rapid install/uninstall, fake ratings) needs separate spec.

**🤔 Strategic questions for follow-up**:
1. Is `/discover` accessible to anonymous users? (Marketing implications)
2. Do we need per-game scoping for `/discover` rows? (e.g., "Trending in Wingspan players")
3. Should rating system support replies/threading? (Out of scope v1)
4. KB chunk search: support filter by docType/gameId? (Easy v1.5 addition)

---

## 10. References

- Issue #728 (`/discover` backend prerequisite)
- Issue #729 (`/toolkits/[id]` backend prerequisite)
- Issue #730 (`/kb/[id]` backend prerequisite)
- Umbrella #681 (Wave 3 V2 migration)
- Spec V2 migration sez. 3.4: `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Phase 0.5 FE contract templates: `docs/frontend/contracts/{games-id,agents-id,game-nights}-hooks.md`
- Wave 3 child issues #683 (✅ shipped), #684, #685 (✅ contract shipped), #686, #687

---

**Status**: DRAFT — pending backend team review
**Next steps**:
1. Backend team validates BC ownership decisions (sez. 3.1, 5.1)
2. Backend dev tickets opened per Phase 1-4 roadmap (sez. 8)
3. After Phase 1 endpoints land → FE writes `/discover` Phase 0.5 contract (Wave 3 Step 3b unblock)
4. After Phase 3 endpoints land → FE writes `/kb/[id]` Phase 0.5 contract
5. After Phase 4 endpoints land → FE writes `/toolkits/[id]` Phase 0.5 contract

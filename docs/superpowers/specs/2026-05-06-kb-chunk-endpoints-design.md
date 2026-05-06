# KB Chunk-Level Retrieval Endpoints — Design

**Issue**: #730 (Backend audit prerequisite for #684 `/kb/[id]` Wave 3 v2 migration)
**Branch**: `feature/issue-730-kb-chunk-endpoints` (parent: `main-dev`)
**Author**: brainstorming session 2026-05-06
**Status**: Draft → User review pending

---

## 1. Context & Problem Statement

V2 migration Wave 3 child #684 must implement the route `/kb/[id]` from mockup `sp4-kb-detail.{html,jsx}`: a split-view UI (left panel: chunks list; right panel: markdown preview) for inspecting KB documents at chunk granularity.

The audit (#681 Wave 3 Step 1) revealed that **5 backend capabilities are missing** for this UI:

| # | Capability | Endpoint |
|---|-----------|----------|
| 1 | Single document metadata fetch | `GET /api/v1/kb-docs/{id}` |
| 2 | Paginated chunks list with hierarchical breadcrumbs | `GET /api/v1/kb-docs/{id}/chunks` |
| 3 | Single chunk full content with prev/next navigation | `GET /api/v1/kb-docs/{id}/chunks/{chunkId}` |
| 4 | In-document keyword search with highlighted snippets | `POST /api/v1/kb-docs/{id}/chunks/search` |
| 5 | Admin diagnostics fields (vectorId, embeddingStatus, ...) | embedded in (2) and (3) via DTO gating |

This document specifies the design before implementation begins.

### Key constraint: data layer asymmetry

The chunking pipeline already produces full hierarchical metadata (`Heading`, `ParentChunkId`, `Level`, `ElementType`) and persists it to **Qdrant** via `ChunkPayload`. However, **PostgreSQL `TextChunkEntity` only persists `ChunkIndex`, `PageNumber`, `Content`, `search_vector`** — heading/parent/level data is missing from the relational store.

This is the root reason behind the architectural decisions below.

---

## 2. Architectural Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| **D1** | Primary actor | **Mix of admin + game owner + reader** with server-side field gating | Real usage spans all three personas; single API with conditional fields beats role-specific endpoints |
| **D2** | Job-to-be-done scope | **Combo 1 — Full feature** (5 JTBD: verify chunking, browse-as-book, citation jump, keyword search, admin diagnostics) | Wave 3 needs full unblocking; partial delivery would force a follow-up PR before #684 is mergeable |
| **D3** | Migration scope | **Option B — FULL normalized**: add `heading varchar`, `parent_chunk_id uuid?`, `level smallint`, `element_type varchar(20)` to `TextChunkEntity` | Combo 1 requires nested headingPath; normalized model avoids drift vs Qdrant payload (single source of truth = Qdrant) |
| **D4** | DTO gating strategy | **Option A — Single DTO with nullable admin fields**, populated server-side based on `currentUser.IsAdmin OR currentUser.OwnsGame(gameId)`. Admin-only fields are decorated with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` (per-field, since the global `ConfigureHttpJsonOptions` does **not** set `DefaultIgnoreCondition`). | Single contract for FE; coherent with existing pattern (`GameDocumentDto.VersionLabel?`); per-field annotation avoids changing global JSON behavior for other endpoints |
| **D5** | Search ranking | PostgreSQL `ts_rank_cd` + `plainto_tsquery('simple', query)` + `ts_headline` | Built-in (no new infra); `simple` config avoids stemming false-positives across languages |
| **D6** | Pagination | Offset-based `?skip=&take=` with `take ∈ [1, 100]` | Coherent with codebase (`HandleGetMyChatHistory`); CTE-recursive joins handle 1000+ chunks fine |
| **D7** | `vectorId` mapping | Alias of `TextChunkEntity.Id` (Guid) | Already the foreign key into `PgVectorEmbeddingEntity`; no separate identifier exists |
| **D8** | Caching | HybridCache: G1 list 5min · G2 single-chunk 10min · G3 search no-cache · G4 metadata 60s. Tags: `gameId:{guid}`, `pdfDocumentId:{guid}` | Chunks are immutable post-ingest (long TTL safe); metadata may change during ingest (short TTL); search results vary per query |

---

## 3. SMART User Goals

### G1 — Browse-and-jump as a book (JTBD-B)

> **As** a reader (logged in, with access to a public or owned KB doc),
> **I want** to scroll a paginated list of chunks ordered by narrative position, with a hierarchical breadcrumb (`Chapter → Section → Subsection`) for each,
> **so that** I can find a chunk of interest in **≤ 3 clicks** without reading the entire document.

**SMART criteria**:
- **S**: `GET /api/v1/kb-docs/{id}/chunks` returns `chunkId`, `headingPath: string[]`, `snippet` (≤200 char), `pageNumber`, `position`, `level`
- **M**: P95 latency < 200ms for 50-chunk page; `headingPath` populated (≥1 element) for ≥80% of chunks in typical rulebooks
- **A**: backfill from Qdrant payload + recursive CTE on `parent_chunk_id`
- **R**: unblocks `KbChunkListPanel` mockup `sp4-kb-detail`
- **T**: this PR (5–7 days)

**Gherkin scenarios**:

```gherkin
Feature: Browse-and-jump chunks as a book

  Scenario: Reader scrolls paginated chunks with hierarchical breadcrumbs
    Given a PDF "Catan rulebook" was ingested with processingState=Ready
    And the document has 120 chunks distributed across 32 pages
    And chunks have heading nested up to level=2
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks?skip=0&take=50
    Then the response is 200 OK in <200ms (P95)
    And contains exactly 50 chunks ordered by chunkIndex asc
    And each chunk has headingPath as array (e.g. ["3. Setup", "Card distribution"])
    And each chunk has snippet of at most 200 characters
    And the response includes hasMore=true (skip+take < totalCount)
    And totalCount=120

  Scenario: Reader navigates to second page
    Given the state from the previous scenario
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks?skip=50&take=50
    Then the response contains 50 chunks with chunkIndex 50-99

  Scenario: Reader views final partial page
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks?skip=100&take=50
    Then the response contains 20 chunks (chunkIndex 100-119)
    And hasMore=false

  Scenario: Document still being processed
    Given a PDF is in state processingState=Embedding
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks
    Then the response is 200 OK with empty array
    And the response includes processingState="embedding" so FE renders empty state

  Scenario: take exceeds maximum limit
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks?take=500
    Then the response is 400 Bad Request
    And the message is "take must be between 1 and 100"
```

---

### G2 — Citation jump (trust-but-verify) (JTBD-C)

> **As** a reader who just received a citation from the AI agent in chat,
> **I want** to click the citation and see the **specific cited chunk** with full markdown content, breadcrumb, and prev/next navigation,
> **so that** I can verify in **1 click** that the AI's answer matches the original text.

**SMART criteria**:
- **S**: `GET /api/v1/kb-docs/{id}/chunks/{chunkId}` returns `content` (markdown), `headingPath`, `pageNumber`, `level`, `prevChunkId?`, `nextChunkId?`, `vectorId` (admin only)
- **M**: P95 < 100ms (cached), < 250ms (cold); cached in HybridCache 10min (immutable post-ingest)
- **A**: direct lookup `TextChunkEntity` by Id + 2 auxiliary queries for prev/next chunkId
- **R**: enables `KbChunkPreview` panel + chat citation deep-link
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: Citation jump to specific chunk

  Scenario: Reader clicks citation and sees full markdown content
    Given a "Mage Knight" doc ingested with 200 chunks
    And the chat agent answered citing chunkId=abc-123 (chunkIndex=42)
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks/abc-123
    Then the response is 200 OK in <250ms (P95 cold), <100ms (cached)
    And contains content as markdown (heading, ul/ol, table preserved)
    And contains headingPath ["Combat", "Attack phase", "Resolving damage"]
    And contains prevChunkId (chunkIndex=41), nextChunkId (chunkIndex=43)
    And vectorId equals chunkId (alias of the ID)

  Scenario: First chunk of doc (no prev)
    When the reader calls GET with chunkId of chunkIndex=0
    Then prevChunkId is null
    And nextChunkId is populated

  Scenario: Last chunk of doc (no next)
    When the reader calls GET with chunkId of last chunkIndex
    Then nextChunkId is null

  Scenario: chunkId does not exist or belongs to another doc
    Given a valid chunkId from another doc
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks/{otherDocChunkId}
    Then the response is 404 Not Found
    And the message does not leak chunk existence in the other doc

  Scenario: Reader without permission on private doc
    Given the doc is private (PrivateGameId set, owned by another user)
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks/{chunkId}
    Then the response is 403 Forbidden
```

---

### G3 — Search keyword in-document with highlight (JTBD-D)

> **As** a reader or admin who wants to find a specific term inside a document (e.g. "initiative" in Mage Knight),
> **I want** a search box with results ranked by relevance and snippets with the keyword highlighted,
> **so that** I can find the relevant chunk in **≤ 5 seconds** without scrolling the full list.

**SMART criteria**:
- **S**: `POST /api/v1/kb-docs/{id}/chunks/search` body `{ query, skip, take }` returns `{ matches: [{ chunkId, headingPath, snippet (with `<mark>` tags), rank }], totalCount }`
- **M**: P95 < 300ms on 500-chunk doc; deterministic rank ordering (`ts_rank_cd`); ≥1 match returned if keyword exists in ≥1 chunk
- **A**: `plainto_tsquery('simple', query)` + `ts_rank_cd(search_vector, ts_query)` + `ts_headline(content, ts_query)` for highlighted snippet
- **R**: powers `ChunkSearchBox` mockup
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: In-document keyword search with relevance ranking

  Scenario: Search keyword present in multiple chunks
    Given a "Mage Knight" doc with 500 chunks ingested
    And the keyword "initiative" appears in 7 chunks
    When the reader posts to /api/v1/kb-docs/{docId}/chunks/search body {"query":"initiative","skip":0,"take":20}
    Then the response is 200 OK in <300ms (P95)
    And contains matches=7 chunks ordered by rank desc (ts_rank_cd)
    And each snippet contains the keyword wrapped in <mark>...</mark>
    And totalCount=7

  Scenario: Search keyword does not exist
    When the reader posts with query="xyzzyx"
    Then the response is 200 OK with matches=[] and totalCount=0

  Scenario: Multi-word query with stemming
    Given the doc is language="it"
    And the keyword "initial cards" appears with variants
    When the reader posts with query="initial card"
    Then ts_query uses configuration "simple" (no language-specific stemming) to avoid cross-language false positives

  Scenario: Query too long
    When the reader posts with query of 250 characters
    Then the response is 400 Bad Request
    And the message is "query must be between 1 and 200 characters"

  Scenario: Query with special tsquery characters (operator injection)
    When the reader posts with query="initiative | drop"
    Then the backend escapes the query using plainto_tsquery (no operator injection)
    And the response treats "|" as a word separator, not the OR operator
```

---

### G4 — Document metadata overview (JTBD-A foundation)

> **As** a game owner or admin opening a freshly ingested document,
> **I want** to see in **a single call** the full doc metadata (title, gameId, granular processing state, total chunks, page count, lastIngestedAt, language),
> **so that** I can decide if the KB is ready or needs to be retried/diagnosed.

**SMART criteria**:
- **S**: `GET /api/v1/kb-docs/{id}` returns `KbDocumentDto { id, title, gameId, sharedGameId?, documentCategory, processingState, totalChunks, pageCount, indexedAt?, uploadedAt, language, versionLabel?, processingError? (admin), retryCount? (admin) }`
- **M**: P95 < 80ms (cache hit), < 200ms (cold); cache TTL 60s
- **A**: join `VectorDocument` + `PdfDocument` + count `TextChunkEntity` (denormalized `TotalChunks` already in `VectorDocument`)
- **R**: powers `KbHeader` mockup
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: Document metadata overview

  Scenario: Game owner views Ready doc
    Given a "Catan rulebook" doc just ingested with state Ready
    When the game owner calls GET /api/v1/kb-docs/{docId}
    Then the response is 200 OK in <200ms (P95 cold)
    And contains title, gameId, processingState="ready", totalChunks=120, pageCount=32
    And processingError is null/omitted (Ready state)

  Scenario: Owner views in-progress doc
    Given a doc is in state processingState="Embedding"
    When the game owner calls GET /api/v1/kb-docs/{docId}
    Then the response contains processingState="embedding" (lowercase canonical)
    And totalChunks may be 0 or partial count
    And the FE can render a progress indicator

  Scenario: Admin views Failed doc
    Given a doc is in state processingState="Failed" with processingError="Embedding API timeout"
    When the admin calls GET /api/v1/kb-docs/{docId}
    Then the response contains processingError="Embedding API timeout"
    And contains retryCount=2, failedAtState="Embedding"

  Scenario: Reader (non-admin) views Failed doc
    Given the same Failed doc
    When the reader (no admin) calls GET /api/v1/kb-docs/{docId}
    Then the response does NOT contain processingError, retryCount, failedAtState (null/omitted)
    And contains processingState="failed" (state-level info is OK to share)

  Scenario: Doc does not exist
    When calling GET /api/v1/kb-docs/{nonExistentId}
    Then the response is 404 Not Found
```

---

### G5 — Admin diagnostics: chunk-level processing state (JTBD-E)

> **As** an admin diagnosing a failed or low-quality ingestion,
> **I want** to see diagnostic fields on chunks (`vectorId`, `embeddingStatus`, `characterCount`, `elementType`),
> **so that** I can identify in **≤ 30 seconds** which chunk caused issues (e.g. embedding mismatch, OCR parsing failure).

**SMART criteria**:
- **S**: G1/G2 endpoints include `vectorId`, `characterCount`, `elementType`, `embeddingStatus` ONLY if `currentUser.IsAdmin === true`. Server-side field gating; fields `null` in DTO for non-admin
- **M**: zero leak of admin fields to non-admin (audit log on attempts); 100% test coverage of gating logic
- **A**: handler checks `currentUser.HasRole("Admin")` post-MediatR, populates/null-ifies fields in projection
- **R**: enables admin debug UI without creating duplicate endpoints
- **T**: this PR

**Gherkin scenarios**:

```gherkin
Feature: Admin chunk-level diagnostics

  Scenario: Admin sees full diagnostic fields
    Given a "Tainted Grail" doc with 250 chunks
    And currentUser has role=Admin
    When the admin calls GET /api/v1/kb-docs/{docId}/chunks?skip=0&take=10
    Then each chunk DTO contains vectorId, characterCount, elementType, embeddingStatus

  Scenario: Reader (non-admin) does NOT see admin fields
    Given the same doc as G5.1
    And currentUser has role=User (not admin)
    When the reader calls GET /api/v1/kb-docs/{docId}/chunks?skip=0&take=10
    Then each chunk DTO has vectorId=null, characterCount=null, elementType=null, embeddingStatus=null
    And the JSON serializer omits null fields (JsonIgnoreCondition.WhenWritingNull is global)

  Scenario: Admin views chunk with failed embedding
    Given a doc with processingState=Failed
    And 12 chunks of 250 have embeddingStatus="failed"
    When the admin calls GET /api/v1/kb-docs/{docId}/chunks/{failedChunkId}
    Then the chunk DTO contains embeddingStatus="failed"
    And the FE can render a diagnostic indicator
```

---

## 4. API Contract — Endpoint Specifications

### 4.1 `GET /api/v1/kb-docs/{id}`

**Auth**: `RequireSession()` + handler `userOwnsGame OR docIsPublic OR userIsAdmin`
**Response 200** (`KbDocumentDto`):

```ts
{
  id: string (guid),
  title: string,            // pdf.FileName
  gameId: string (guid)?,   // private game
  sharedGameId: string (guid)?, // community
  documentCategory: "Rulebook" | "Expansion" | "Errata" | "Homerule",
  processingState: "pending" | "uploading" | "extracting" | "chunking" |
                   "embedding" | "indexing" | "ready" | "failed",  // lowercase canonical
  totalChunks: number,
  pageCount: number,
  indexedAt: string (iso8601)?,
  uploadedAt: string (iso8601),
  language: string,         // e.g. "it", "en"
  versionLabel: string?,
  // Admin-only fields:
  processingError: string?,
  retryCount: number?,
  failedAtState: string?
}
```

**Errors**: 404 (not found) · 403 (no access)

---

### 4.2 `GET /api/v1/kb-docs/{id}/chunks?skip&take`

**Query params**: `skip` (default 0), `take` (default 50, max 100)
**Auth**: same as 4.1
**Response 200** (`KbChunkListDto`):

```ts
{
  chunks: KbChunkSummaryDto[],
  totalCount: number,
  skip: number,
  take: number,
  hasMore: boolean,
  processingState: string  // mirrored from doc, for FE empty state UX
}

KbChunkSummaryDto = {
  chunkId: string (guid),
  pageNumber: number?,
  position: number,         // chunkIndex
  level: number,            // 0=Title, 1=Section, 2=Paragraph
  headingPath: string[],    // recursive parent traversal, may be []
  snippet: string,          // first 200 chars of content
  // Admin-only:
  vectorId: string (guid)?,         // alias of chunkId
  characterCount: number?,
  elementType: string?,             // "NarrativeText" | "Title" | "Table" | "List"
  embeddingStatus: string?          // "indexed" | "pending" | "failed"
}
```

**Errors**: 400 (`take` out of range) · 404 (doc not found) · 403 (no access)

---

### 4.3 `GET /api/v1/kb-docs/{id}/chunks/{chunkId}`

**Auth**: same as 4.1
**Response 200** (`KbChunkDetailDto`):

```ts
{
  chunkId: string (guid),
  content: string,          // full markdown
  pageNumber: number?,
  position: number,
  level: number,
  headingPath: string[],
  prevChunkId: string (guid)?,
  nextChunkId: string (guid)?,
  // Admin-only:
  vectorId: string (guid)?,
  characterCount: number?,
  elementType: string?,
  embeddingStatus: string?,
  parentChunkId: string (guid)?
}
```

**Errors**: 404 (chunk not found OR cross-doc) · 403 (no access)

---

### 4.4 `POST /api/v1/kb-docs/{id}/chunks/search`

**Auth**: same as 4.1
**Request body**:

```ts
{
  query: string,    // 1-200 chars
  skip: number?,    // default 0
  take: number?     // default 20, max 100
}
```

**Response 200** (`KbChunkSearchResultDto`):

```ts
{
  matches: KbChunkMatchDto[],
  totalCount: number,
  skip: number,
  take: number
}

KbChunkMatchDto = {
  chunkId: string (guid),
  headingPath: string[],
  snippet: string,         // ts_headline output with <mark> tags
  rank: number,            // ts_rank_cd score (0..1)
  pageNumber: number?,
  position: number
}
```

**Errors**: 400 (query length out of range) · 404 (doc not found) · 403 (no access)

---

## 5. Data Model Changes

### 5.1 Migration: `20260506_AddChunkHierarchyToTextChunkEntity`

```sql
ALTER TABLE text_chunks
  ADD COLUMN heading varchar(500) NULL,
  ADD COLUMN parent_chunk_id uuid NULL,
  ADD COLUMN level smallint NOT NULL DEFAULT 1,
  ADD COLUMN element_type varchar(20) NOT NULL DEFAULT 'NarrativeText';

CREATE INDEX ix_text_chunks_parent_chunk_id ON text_chunks(parent_chunk_id);
CREATE INDEX ix_text_chunks_pdf_chunk_index ON text_chunks(pdf_document_id, chunk_index);
-- existing search_vector GIN index remains
```

**Note on `headingPath` (DTO array) vs `heading` (column)**: the database stores a single `heading` per chunk (the nearest section heading, as already produced by `ChunkMetadata.Heading`). The DTO field `headingPath: string[]` is **derived at query time** via a recursive CTE that traverses `parent_chunk_id` from the chunk up to the root, collecting the `heading` of each ancestor. Example projection:

```sql
WITH RECURSIVE chunk_path AS (
  SELECT id, heading, parent_chunk_id, 1 AS depth
  FROM text_chunks WHERE id = @chunkId
  UNION ALL
  SELECT t.id, t.heading, t.parent_chunk_id, cp.depth + 1
  FROM text_chunks t
  JOIN chunk_path cp ON t.id = cp.parent_chunk_id
  WHERE cp.depth < 10  -- depth cap (R2 mitigation)
)
SELECT array_agg(heading ORDER BY depth DESC)
       FILTER (WHERE heading IS NOT NULL) AS heading_path
FROM chunk_path;
```

The application layer materializes this into a `string[]` (empty array when no headings exist along the chain).

### 5.2 Backfill strategy

A one-shot backfill job (idempotent, runnable as a hosted service or a CLI command) reads `ChunkPayload` from Qdrant for each `pdf_document_id` and projects:

- `Heading` → `heading`
- `Level` → `level`
- `ParentChunkId` (string in Qdrant) → `parent_chunk_id` (Guid in PostgreSQL, after parsing)
- `ElementType` → `element_type`

If Qdrant payload is missing for a chunk (legacy data), fallback values: `heading = null`, `parent_chunk_id = null`, `level = 1`, `element_type = "NarrativeText"`.

Backfill is **out of scope of the API PR** but tracked as a follow-up issue (Section 8).

### 5.3 Pipeline going forward

`AdvancedChunkingService` already populates `ChunkMetadata` with `Heading`, `Level`, `ParentChunkId`, `ElementType`. The persistence layer (currently writes only to Qdrant via `ChunkPayload`) must be extended to also write the new columns to `TextChunkEntity`.

This change is small and lives in the existing chunking service. Tracked as part of this PR.

---

## 6. Implementation Strategy

### 6.1 Sequencing (TDD-driven)

1. **Migration + entity update**
   - Add columns to `TextChunkEntity`
   - Update EF Core mapping configuration
   - `dotnet ef migrations add AddChunkHierarchyToTextChunkEntity`
   - Unit test: entity round-trip with new fields

2. **Chunking pipeline writes new fields**
   - Extend `AdvancedChunkingService` (or its persistence collaborator) to populate `heading`, `parent_chunk_id`, `level`, `element_type` when writing to PostgreSQL
   - Integration test (Testcontainers): ingest a synthetic PDF, verify all four columns are populated

3. **G4 — `GetKbDocumentByIdQuery`** (smallest endpoint, sets the auth/gating pattern)
4. **G1 — `GetKbChunksQuery`** with offset pagination + headingPath CTE
5. **G2 — `GetKbChunkByIdQuery`** with prev/next computation
6. **G3 — `SearchKbChunksQuery`** with `plainto_tsquery` + `ts_rank_cd` + `ts_headline`
7. **G5 — DTO field gating** (cross-cutting; applied in handlers G1/G2 projections)
8. **Routing wiring** in `KnowledgeBaseEndpoints.cs` (or new `KbDocumentEndpoints.cs` partial)
9. **Frontend Zod schemas + hook templates** (`apps/web/src/lib/api/schemas/kb-document.ts`, `apps/web/src/hooks/queries/useKbChunks.ts` etc.) — coordinate with the parallel terminal to avoid divergence

### 6.2 Test strategy

- **Unit tests** (Domain + Application): handler logic with mocked `DbContext` (`InMemoryDb` or `Substitute`)
- **Integration tests** (Testcontainers PostgreSQL): full CTE recursive query, full-text search, pagination boundaries
- **Contract tests**: DTO field gating for admin vs non-admin
- Target coverage: 90%+ for new code (project standard)

### 6.3 Observability

- Structured log per query: `_logger.LogDebug("Fetching KB chunks for doc {DocId} by user {UserId} skip={Skip} take={Take}")`
- HybridCache metrics already exposed
- New metric: `kb_chunk_search_duration_seconds` histogram (Prometheus) for G3 latency tracking

---

## 7. Risks & Mitigations

| # | Risk | Likelihood | Impact | Mitigation |
|---|------|-----------|--------|------------|
| R1 | Backfill fails for legacy docs (Qdrant payload missing/corrupted) | Medium | Medium | Fallback values (`heading=null`, `level=1`); follow-up issue tracks re-ingestion of broken docs |
| R2 | Recursive CTE on `parent_chunk_id` performs poorly on deeply-nested chunks (>10 levels) | Low | Medium | Cap recursion depth at 10 in CTE; index `parent_chunk_id`; rulebook headings rarely exceed 4 levels |
| R3 | `ts_headline` produces oversized snippets that bloat response | Low | Low | Configure `MaxFragments=2, MaxWords=20` in `ts_headline` options |
| R4 | DTO field gating leaks admin fields due to handler bug | Low | High | 100% test coverage of gating; integration test asserting non-admin response is sanitized |
| R5 | DTO contract divergence with parallel terminal (FE migration wave) | Medium | High | Publish schema contract early; Zod schemas committed in this PR; coordinate via PR comment + Slack ping when DTOs land |
| R6 | Response payload too large for docs with 1000+ chunks (when listing) | Low | Medium | `take` capped at 100; FE always paginates |
| R7 | Search query operator injection via `to_tsquery` syntax | Low | High | Use `plainto_tsquery` (sanitizes user input) instead of `to_tsquery` |

---

## 8. Out of Scope (follow-up issues)

The following are intentionally not part of this PR:

| Topic | Tracked as |
|-------|------------|
| Backfill job for legacy `text_chunks` rows | New issue: "Backfill chunk hierarchy from Qdrant to PostgreSQL" |
| Admin diagnostic search filter (e.g., `?embeddingStatus=failed`) | Future issue when admin UI requires it |
| Cursor-based pagination (only if offset becomes a perf problem) | Future issue (reactive) |
| Vector semantic search inside a single doc | Already covered by `SearchQuery` at game-level; in-doc semantic is YAGNI for now |
| Real-time chunk update via SSE/WebSocket (e.g., during ingestion) | Future, depends on processing pipeline progress events |
| Visual highlighting/diff of chunks (admin debug) | Future admin UI epic |

---

## 9. Definition of Done

- [ ] EF Core migration `AddChunkHierarchyToTextChunkEntity` applied (dev DB upgraded)
- [ ] `AdvancedChunkingService` writes 4 new columns on chunk persistence
- [ ] 4 query handlers + 4 DTOs implemented (G1, G2, G3, G4)
- [ ] DTO field gating for admin vs non-admin (G5) tested with both roles
- [ ] All endpoints wired in `KnowledgeBaseEndpoints.cs` with `RequireSession()` + access control
- [ ] OpenAPI/Scalar annotations present (`Produces<...>()`, `WithSummary`, `WithDescription`)
- [ ] Unit + integration test coverage ≥ 90% on new code
- [ ] Frontend Zod schemas in `apps/web/src/lib/api/schemas/kb-document.ts`
- [ ] Frontend hook templates in `apps/web/src/hooks/queries/useKbChunks.ts` (and siblings)
- [ ] PR opened against `main-dev` (parent branch)
- [ ] Close-out comment on #684 with endpoint list + sample response payloads
- [ ] Issue #730 closed

---

## 10. References

- Issue #730 — backend audit
- Issue #684 — Wave 3 child `/kb/[id]`
- Issue #681 — Wave 3 umbrella
- Migration spec — `docs/superpowers/specs/2026-04-26-v2-design-migration.md` §3.4 (Phase 0.5 contract pattern)
- Mockup — `admin-mockups/design_files/sp4-kb-detail.{html,jsx}`
- Stub components — `apps/web/src/components/v2/kb-detail/`
- Existing chunk pipeline — `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/Chunking/AdvancedChunkingService.cs`
- Existing entity — `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/TextChunkEntity.cs`
- Existing query pattern — `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/`

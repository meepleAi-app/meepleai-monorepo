# Spec-Panel Decisions — Issue #1686 (KB Global Facets BE)

**Date**: 2026-05-31
**Issue**: [#1686](https://github.com/meepleAi-app/meepleai-monorepo/issues/1686) — server-side facets for `/knowledge-base/search/global`
**Baseline PR**: [#1672](https://github.com/meepleAi-app/meepleai-monorepo/pull/1672) (no-facet shape)
**Panel personas**: Wiegers (requirements), Fowler (refactoring), Nygard (release/it depends), Adzic (specification by example), Doumont (clarity).

## Context

PR #1672 shipped `POST /api/v1/knowledge-base/search/global` accepting `{ Query, Limit, Cursor, Mode, MinScore }` only — no facet params. The FE FilterAccordion contract (docType / gameId / language) on `/knowledge-base/global` has no server-side backing. Panel verdict (Nygard, 2026-05-29): client-side filtering on cursor-paginated results is a UX trap (filters would lie about the dataset). FilterAccordion was deferred from Foundation v1 awaiting this BE work.

## Decisions

### D-1 — `DocType` allowlist

**Decision**: Accept exactly `["base", "expansion", "errata", "homerule"]` (matches `PdfDocumentEntity.DocumentType` line 61, comment in entity file). Case-insensitive comparison server-side. Each list element MUST be from the allowlist or the request returns 422.

**Rationale (Adzic)**: enumerate the universe — the entity comment is the source of truth, no implicit allowlisting via "anything goes". Closed set means clear API contract, smaller attack surface, predictable indexing.

### D-2 — `Language` allowlist

**Decision**: Accept exactly `["en", "it", "de", "fr", "es"]` (matches `PdfDocumentEntity.Language` line 48 comment). Unknown → 422.

**Rationale (Adzic)**: same as D-1. The entity comment enumerates ISO 639-1 codes supported by extraction services. Forward-compatible: add new codes here when the entity is extended.

### D-3 — Pre-existing endpoint shape preserved byte-identical

**Decision**: All 3 facet fields (`DocType`, `GameId`, `Language`) are optional/nullable. When all three are absent/null, behaviour and JSON response shape are byte-identical to PR #1672. **Captured via dedicated snapshot test.**

**Rationale (Fowler)**: this is a refactoring-with-extension. The "no facets" path is a regression risk for FE consumers parsing the response shape; an explicit snapshot test pins behaviour and lets future PRs detect drift.

### D-4 — Push-down strategy

**Decision**: When ANY facet is non-null, compute an allowlist of `PdfDocumentEntity.Id` (filtered by facets + accessibleGameIds) via EF BEFORE vector search. Pass this allowlist to `IMultiGameHybridSearchService.SearchAsync` via a new `documentIds` parameter. The vector layer filters its results to that allowlist.

**Rationale (Nygard)**: push the filter as close to the data as possible. Vector search over a pre-filtered candidate set is cheaper than post-filtering N vector hits. EF query is bounded by the existing `(SharedGameId, DocumentType, Language)` columns (composite filter is cheap).

### D-5 — `GameId` (single, nullable) intersects with accessibleGameIds

**Decision**: `GameId` is a single nullable Guid. When provided and `GameId ∈ accessibleGameIds`, the effective accessible set narrows to `{GameId}`. When `GameId ∉ accessibleGameIds` (e.g. user tries to filter to a private game they don't own), return 200 empty (NOT 403). NOT an error.

**Rationale (Wiegers/Nygard)**: avoid information leak — a 403 on a private gameId would tell the user "this game exists" even if they have no access. 200 empty is RBAC-safe and matches the existing EC-1 pattern (no accessible games → 200 empty).

### D-6 — Cursor stability preserved with facets

**Decision**: Facets do NOT change cursor ordering invariants (score DESC, chunkId ASC). The cursor is opaque and re-applying the same cursor + same facet set returns the next page deterministically.

**Rationale (Nygard)**: cursor pagination + filters is a known sharp edge. Document explicitly that mutating facets mid-pagination is undefined (FE responsibility to reset the cursor when facets change).

### D-7 — `DocType` is a list (max 10 items)

**Decision**: `DocType: IReadOnlyList<string>?`. Empty list treated as null (no filter — defensive). Hard cap of 10 elements (input bounding; allowlist has only 4 anyway).

**Rationale (Wiegers)**: input validation defense in depth. The list shape mirrors the FE accordion semantics (multi-select within docType category).

### D-8 — Case-insensitive comparison

**Decision**: All facet string values are normalised to lower-case for comparison against entity values. Validator enforces non-empty strings on each list element.

**Rationale (Doumont)**: clear input contract — clients can send "Base" or "BASE" and the match works. Reduces FE/BE coupling on casing.

### D-9 — OpenAPI documentation

**Decision**: XML doc-comments on `GlobalKbSearchRequest` record params + summary on the endpoint. No separate OpenAPI annotations needed (existing convention).

**Rationale (Doumont)**: minimize documentation surface. The C# record IS the OpenAPI source.

### D-10 — RBAC composes on top of facets

**Decision**: RBAC remains the outer guarantee. Facets narrow the candidate set further; never broaden. Order of operations: `accessibleGameIds = RBAC()` → `narrowedIds = facets(accessibleGameIds, gameId?)` → `documentIds = facets(narrowedIds, docType?, language?)` → `vectorSearch(query, documentIds)`.

**Rationale (Nygard)**: explicit composition. RBAC failure means empty result; facet failure means empty result. Both safe.

### D-11 — Empty documentIds short-circuit

**Decision**: When facet computation yields an empty `documentIds` allowlist (e.g. no docs match `docType=expansion`+`language=de`), short-circuit to 200 empty WITHOUT calling the vector search.

**Rationale (Fowler)**: performance optimisation + symmetry with EC-1 (no accessible games early exit). Avoids a wasted RPC to the embedding service.

### D-12 — Validator error messages enumerate allowed values

**Decision**: `DocType must be one of: base, expansion, errata, homerule.` and `Language must be one of: en, it, de, fr, es.` Emitted by FluentValidation, mapped to 422 by `ApiExceptionHandlerMiddleware`.

**Rationale (Doumont)**: actionable error messages. The FE doesn't need a separate "discover allowed values" endpoint.

### D-13 — Snapshot test asserts JSON shape preserved

**Decision**: A dedicated unit test serialises a `GlobalKbSearchResponseDto` produced by a no-facet request and asserts the JSON shape matches a frozen baseline (keys, types, ordering). Detects accidental shape drift in PRs.

**Rationale (Adzic/Fowler)**: specification by example. The response shape IS the FE contract — pin it.

### D-14 — Prometheus counter for facet usage observability

**Decision** (added in follow-up #1731): Extend `MeepleAiMetrics.Rag` partial class with a counter `kb_global_search_facet_total{facet="docType|gameId|language", state="applied|rejected"}`.

- `applied`: increments when a facet is provided AND survives RBAC intersection (narrows the candidate set).
- `rejected`: increments when a GameId facet is provided but rejected by RBAC (D-5 silent drop → 200 empty).

Cardinality: 3 facet × 2 state = 6 series (bounded, safe). NO label of facet value (would explode cardinality with gameId UUIDs).

**Rationale (Nygard, Fowler)**: ops needs 30-day visibility on facet adoption. Counter is best-effort (P116/P117 mapper-convention pattern from BE-3 #1641). Extending the existing `Rag.cs` partial class preserves convention with 28 existing metric files.

**Test pattern**: counter idempotency via delta-assertion (read counter value pre-call + post-call; assert delta == expected). No global registry reset → safe in parallel xUnit runs.

**Mapping vs issue text**: Issue #1731 calls this "D-12" using local-version numbering — canonical D-12 is "Validator error messages enumerate allowed values" (unrelated). The canonical decision number is **D-14**.

### D-15 — OpenAPI/Scalar request-body example

**Decision** (added in follow-up #1731): Extend `MapGlobalSearchEndpoint` (in `KnowledgeBaseEndpoints.cs`) with a Scalar request-body example via `WithOpenApi(op => op.RequestBody.Content["application/json"].Example = ...)`.

Example payload uses **canonical vocab** (matches D-1/D-2 allowlists):

```json
{
  "query": "movement rules",
  "limit": 20,
  "docType": ["base", "expansion"],
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "language": "it"
}
```

**Rationale (Doumont, Wiegers)**: Scalar examples ARE part of the API contract — testers copy-paste from the UI. Using canonical vocab (`base`, `expansion`, NOT `Rulebook`/`Errata` from the issue text's local-vocab drift) keeps API doc aligned with the actual allowlist.

**AC**: `dotnet run` → `/scalar/v1` renders the example with all 3 facets populated. Manual verify + screenshot in PR body.

**Mapping vs issue text**: Issue #1731 calls this "D-10" using local-version numbering — canonical D-10 is "RBAC composes on top of facets" (unrelated). The canonical decision number is **D-15**.

## Out of scope (deferred to follow-up issues)

- **Facet aggregation counts** (e.g. "Rulebook (12)"): would require an extra COUNT query per facet. Separate issue (likely #1687).
- **Free-text tag filters / date filters** (Q6 from Phase 0.5 contract): separate issue.
- **FE FilterAccordion implementation**: owned by FE issue #1482 once this BE lands.

## Test coverage commitment

- **Unit** (~30): validator allowlists, push-down logic (mocked `IMultiGameHybridSearchService` capturing `documentIds`), snapshot BC.
- **Integration** (~12): RBAC × facet, multi-facet combos, cursor stability, empty-allowlist short-circuit (Testcontainers Postgres).
- **Snapshot BC** (~3): pre-existing endpoint shape preserved when no facets passed.

Total: ~45 tests.

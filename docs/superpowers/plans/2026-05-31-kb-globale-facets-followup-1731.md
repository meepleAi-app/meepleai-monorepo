# Plan — #1731 KB Global Facets Follow-up (Prometheus + Scalar + 9 integration tests)

**Date**: 2026-05-31
**Issue**: [#1731](https://github.com/meepleAi-app/meepleai-monorepo/issues/1731) (follow-up of #1686 / PR #1730)
**Branch**: `feature/issue-1731-kb-globale-facets-followup`
**Base**: `main-dev`
**Parent decisions (canonical)**: `claudedocs/spec-panel-1686-decisions.md` (D-1..D-13 shipped in PR #1730)
**This follow-up adds**: D-14 (Prometheus counter) + D-15 (Scalar example) to the canonical decisions doc.

## Spec-panel critique session 2026-05-31

Panel: Wiegers, Crispin, Nygard, Doumont, Fowler. Mode: CRITIQUE on follow-up scope (NOT brainstorm).

**Critical findings resolved**:

| # | Finding (expert) | Resolution (user-approved) |
|---|---|---|
| C-1 | Decision-ID drift: issue cites D-10/D-12 but canonical has different mapping (Doumont) | Amend canonical with D-14 (Prometheus) + D-15 (Scalar). NO renumber. Mapping table in PR body. |
| C-2 | Scenario #2 uses "Rulebook" (local vocab); canonical allowlist is `["base","expansion","errata","homerule"]` (Wiegers) | Rename to `Returns_only_Base_when_DocType_base`. Commit message + PR body explain vocab drift. |
| C-3 | Scenario #11 cursor stability needs ≥6 cross-page chunks; current mock returns 1 per game (Crispin) | Extend `BuildVectorSearchMock` to produce N=4 deterministic chunks per game (total 12 across 3 games). |
| M-1 | Coverage gap: D-11 short-circuit (empty post-intersection) + D-5 ∩ facet=zero (Crispin) | Add 2 micro-scenarios → 9 integration tests total. |
| M-2 | Counter idempotency under parallel tests (Fowler) | Delta-assertion pattern: read counter pre-call + post-call, assert delta. No global reset. |
| M-3 | Counter zero-result dimension (Nygard) | Acceptable for v1 (cardinality stays 3×2=6 series). Track in PR body as "future enhancement only if dashboards show gap". |
| m-1 | Scalar pattern: WithOpenApi extension already used elsewhere in repo (Fowler) | Verify pattern in `Endpoints.cs` via grep `Example =`; use it. Fallback: `WithDescription` extended. |
| m-2 | AC explicit for Part C (Wiegers) | "Scalar `/scalar/v1` UI renders example with all 3 facets populated" — verified manually + screenshot in PR body. |

**Out of scope (NOT in this PR)**:
- Defense-in-depth post-filter in `BuildEnrichmentMapAsync` (D-2.a from local plan — NOT shipped in PR #1730 either; deferred).
- Structured logging facet pipeline (logInfo applied/rejected with userId+queryHash).
- Counter dimension `applied_zero` (deferred; revisit if dashboards show insight gap).

## Decisions (new, append to canonical)

### D-14 — Prometheus counter for facet usage observability

**Decision**: Add a Prometheus counter `kb_global_search_facet_total` to `MeepleAiMetrics.Rag.cs` (extend partial class — pattern coerente con 28 existing metric files):

```
kb_global_search_facet_total{facet="docType|gameId|language", state="applied|rejected"}
```

- `applied`: increments when a facet is provided AND survives RBAC intersection (i.e., narrows the candidate set).
- `rejected`: increments when a GameId facet is provided but rejected by RBAC (D-5 silent drop → 200 empty).

Cardinality: 3 facet × 2 state = 6 series (bounded, safe).

**Rationale (Nygard, Fowler)**: ops needs 30-day visibility on facet adoption. Extending the existing `Rag.cs` partial class preserves convention. NO label of facet value (cardinality explosion with gameId UUIDs). Counter is best-effort (P116/P117 mapper-convention pattern from BE-3 #1641).

**Test pattern (Fowler)**: counter idempotency via **delta-assertion** (read `counter.GetValueOrDefault()` pre-call + post-call; assert delta == expected). No global registry reset → safe in parallel xUnit runs.

### D-15 — OpenAPI/Scalar request-body example

**Decision**: Extend `MapGlobalSearchEndpoint` (line 87, `KnowledgeBaseEndpoints.cs`) with a Scalar request-body example via `WithOpenApi(op => op.RequestBody.Content["application/json"].Example = OpenApiAnyFactory.CreateFromJson(...))` (pattern existing elsewhere in repo per Fowler finding).

Example payload:
```json
{
  "query": "movement rules",
  "limit": 20,
  "docType": ["base", "expansion"],
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "language": "it"
}
```

**Rationale (Doumont, Wiegers)**: Scalar examples ARE part of the API contract. The example uses **canonical vocab** ("base","expansion" NOT "Rulebook","Errata") — vocab drift from issue body explicitly documented in commit message.

**AC**: Manual verify via `dotnet run` → `/scalar/v1` renders the example with 3 facets populated. Screenshot in PR body.

### Issue → Canonical decision mapping (PR body table)

| Issue #1731 cites | Maps to canonical | Notes |
|---|---|---|
| D-10 (OpenAPI Scalar example) | **D-15** (NEW) | Issue text uses local-version numbering; canonical D-10 = "RBAC composes on top of facets" (unrelated) |
| D-12 (Prometheus counter) | **D-14** (NEW) | Issue text uses local-version numbering; canonical D-12 = "Validator error messages enumerate allowed values" (unrelated) |

## Files to modify

### Production code (3 files)

1. `claudedocs/spec-panel-1686-decisions.md` — APPEND D-14 + D-15 sections (NO renumber).
2. `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs` — extend partial class with `KbGlobalSearchFacetTotal` counter.
3. `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearch/GlobalKbSearchQueryHandler.cs` — wire counter increments (D-14) at facet pipeline entry/exit points.
4. `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs` — `MapGlobalSearchEndpoint` extend with `WithOpenApi` example (D-15).

### Test code (1 file extended + helpers)

5. `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GlobalKbSearchEndpointTests.cs` — extend:
   - Extend `BuildVectorSearchMock`: parametrize N chunks per game (default 4, total 12 for 3 games). Deterministic order via ChunkIndex.
   - Add helper `SeedFacetedDocsAsync(db, gameId, fileName, docType, language)` to seed PdfDocuments with varied DocumentType + Language for facet tests.
   - Add **9 new test cases** (Part A + 2 extra from M-1).
6. `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearchQueryHandlerTests.cs` — extend with **2 counter unit tests** (Part B).

## TDD task list (8 tasks, 1 commit each)

Pattern: 1 task = 1 commit. Reviewer = `superpowers:code-reviewer` after Task 8.

### Task 1 — Amend canonical decisions doc with D-14 + D-15

**Test**: none (doc-only).

**Code**: `claudedocs/spec-panel-1686-decisions.md` — append D-14 (Prometheus counter) + D-15 (Scalar example) sections AFTER D-13. NO renumber D-1..D-13.

**Commit**: `docs(kb-globale): #1731 add D-14 Prometheus + D-15 Scalar to canonical decisions`

### Task 2 — Add Prometheus counter to MeepleAiMetrics.Rag partial class

**Test**: NEW unit suite (extend `GlobalKbSearchQueryHandlerTests.cs`):
- `Counter_increments_applied_when_DocType_facet_provided` — handler called with `DocType=["base"]` → counter `{facet="docType",state="applied"}` delta == 1.
- `Counter_increments_rejected_when_GameId_not_in_accessible` — handler called with `GameId` ∉ accessibleGameIds → counter `{facet="gameId",state="rejected"}` delta == 1, NO `applied` increment.

**Pattern (idempotency)**: delta-assertion. Read `KbGlobalSearchFacetTotal.WithLabels("docType","applied").Value` pre-call + post-call; assert `(post - pre) == 1`.

**Code**:
- Extend `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Rag.cs` with:
  ```csharp
  internal static readonly Counter KbGlobalSearchFacetTotal = Metrics.CreateCounter(
      "kb_global_search_facet_total",
      "Count of facet activations on /knowledge-base/search/global (Issue #1731 / #1686 D-14).",
      new CounterConfiguration { LabelNames = new[] { "facet", "state" } });
  ```

**Commit**: `feat(kb-globale): #1731 add kb_global_search_facet_total Prometheus counter`

### Task 3 — Wire counter increments in GlobalKbSearchQueryHandler

**Test**: covered by Task 2 unit tests. Add 1 extra: `Counter_NoFacets_NoIncrement` (handler with no facets → all 3 facet counters delta == 0).

**Code**: in `GlobalKbSearchQueryHandler.Handle`, after the existing D-5 / D-4 pipeline:
- After D-5 GameId rejection (line 70): increment `{facet="gameId",state="rejected"}` BEFORE the early return.
- After D-5 GameId accepted (line 79) when narrowing: increment `{facet="gameId",state="applied"}`.
- After D-4 facet pipeline (line 91) when `hasDocTypeFacet` or `hasLanguageFacet`: increment respective counters with `state="applied"` only if `facetDocumentIds.Count > 0` (i.e., D-11 short-circuit does NOT count as applied — the facet matched zero docs).

**Verification (Crispin)**: `dotnet test --filter "FullyQualifiedName~GlobalKbSearchQueryHandler"` → existing handler tests still pass + 3 new counter tests pass.

**Commit**: `feat(kb-globale): #1731 wire facet counter increments in handler`

### Task 4 — Extend BuildVectorSearchMock to N chunks per game (for cursor stability test)

**Test**: covered by Task 7 below (cursor stability scenario #11).

**Code**: in `GlobalKbSearchEndpointTests.cs`, parametrize `BuildVectorSearchMock(int chunksPerGame = 4)`:
- Loop `chunksPerGame` times per game, generating chunks with `ChunkId = $"{pdfDocId}_{chunkIdx}"`, `HybridScore = 0.95f - chunkIdx*0.05f - gameIdx*0.001f` (deterministic, decreasing). Ensures stable cursor ordering.
- Update existing helper signature: callers with no arg get default 1 (backwards compat with AC-1/AC-2/AC-3 existing tests).
- BUT: parametrize via test class field `_mockChunksPerGame = 1` by default, override to 4 in fixture for facet tests. Avoid breaking existing AC-1/AC-2 assertions.

**Approach (Crispin)**: avoid touching existing AC-1/AC-2 tests. Add a separate `BuildFacetVectorSearchMock(int chunksPerGame)` and let the new facet tests use it via test-scoped factory rebuild OR via a per-test override of `_mockChunksPerGame`.

**Decision**: simpler path → make `_mockChunksPerGame` a class field, default 1, override via `[Fact]` setup attribute. Reuse `BuildVectorSearchMock` reading the field.

**Commit**: `test(kb-globale): #1731 extend mock with parametrized chunks-per-game for cursor tests`

### Task 5 — Helper SeedFacetedDocsAsync + extend seed for facet integration tests

**Test**: covered by Tasks 6-8 integration scenarios.

**Code**: in `GlobalKbSearchEndpointTests.cs`, add a private helper:
```csharp
private static Guid SeedFacetedDoc(
    MeepleAiDbContext db,
    Guid gameId,
    Guid uploadedBy,
    string fileName,
    string documentType,  // "base"|"expansion"|"errata"|"homerule"
    string language)      // "en"|"it"|"de"|"fr"|"es"
{
    // mirrors existing SeedIndexedDoc but with parametrized docType + language
    ...
}
```

Extend `SeedAsync` (line 412) to seed additional PdfDocuments for facet coverage:
- 12 docs total (4 per game) with mixed `DocumentType` (base×4, expansion×3, errata×3, homerule×2) and `Language` (en×8, it×4).
- Keep existing 3 docs from SeedIndexedDoc unchanged (AC-1/AC-2/AC-3 baseline).

**Verification**: existing tests AC-1/AC-2/AC-3 still pass; new docs available for facet tests via PdfDocument metadata.

**Commit**: `test(kb-globale): #1731 add SeedFacetedDoc helper + 12-doc seed for facet coverage`

### Task 6 — Integration Part A scenarios 1, 2, 4 (single-facet narrowing)

**Tests** (in `GlobalKbSearchEndpointTests.cs`):

- **#1** `Returns_baseline_when_no_facets`: Alice with no facet → response shape byte-identical to `Alice_SearchesCrossGame_GetsResultsFromAccessibleGames` (D-3 backwards-compat).
- **#2** `Returns_only_Base_when_DocType_base`: Alice with `DocType=["base"]` → handler's `BuildFacetDocumentIdsAsync` returns only docs with `DocumentType="base"`. Assert via `_capturedDocumentIds` bag (NEW capture in mock) — all docs in allowlist have `DocumentType="base"`. **Renamed from issue-body "Rulebook" → canonical "base"**.
- **#4** `Returns_only_Italian_when_Language_it`: Alice with `Language="it"` → similar assertion on `_capturedDocumentIds` matching only `Language="it"` PDFs.

**Mock extension**: add `_capturedDocumentIds = new ConcurrentBag<IReadOnlyList<Guid>?>()` and capture the `documentIds` arg in `BuildVectorSearchMock` callback (already passes 7-arg).

**Commit**: `test(kb-globale): #1731 Part A #1/#2/#4 — baseline + DocType + Language facet integration`

### Task 7 — Integration Part A scenarios 5, 9, 10, 11 (combined + edge + cursor)

**Tests**:

- **#5** `Returns_combined_AND_when_all_three_facets_set`: `DocType=["base"]` + `GameId=_gameAliceOwnedId` + `Language="en"` → `_capturedDocumentIds` matches AND predicate (single doc).
- **#9** `Empty_facet_list_equals_no_facet`: request with `DocType=[]` and `Language=""` and `GameId=null` → byte-identical to scenario #1 (D-3 empty-list normalization).
- **#10** `Case_insensitive_facet_inputs_normalized`: `DocType=["BASE","Base"]`, `Language="IT"` → handler accepts (validator allowlist case-insensitive), normalizes to `["base"]` + `"it"`. Captured documentIds same as scenario #2 + #4 combined.
- **#11** `Cursor_stable_when_facets_unchanged_across_pages`: with `_mockChunksPerGame=4` (Task 4 override), call with `DocType=["base"]` + `Limit=3` → page 1 has 3 results. Call again with cursor + same facets → page 2 has remaining docs. Assert disjoint chunk IDs across pages.

**Commit**: `test(kb-globale): #1731 Part A #5/#9/#10/#11 — combined + empty-list + case-insensitive + cursor`

### Task 8 — Integration extra coverage (M-1) + Scalar example + final wiring

**Tests** (2 extra scenarios from MAJOR finding M-1):

- **D-11 short-circuit**: `Returns_empty_when_facets_yield_zero_documentIds`: Alice with `DocType=["homerule"]` + `Language="de"` → no docs match → 200 empty + `_capturedGameIds` is empty (mock NEVER called per D-11 short-circuit).
- **D-5 ∩ facet=zero**: `Returns_empty_when_GameId_accessible_but_facets_zero`: Alice with `GameId=_gameAliceOwnedId` (accessible) + `DocType=["errata"]` (zero docs match the combination) → 200 empty, search NOT called. Counter `{facet="docType",state="applied"}` NOT incremented (D-11 path), but `{facet="gameId",state="applied"}` IS incremented.

**Code (Part C, D-15)**: extend `MapGlobalSearchEndpoint` (line 87, `KnowledgeBaseEndpoints.cs`):
```csharp
.WithOpenApi(operation =>
{
    operation.RequestBody.Content["application/json"].Example = new OpenApiObject
    {
        ["query"] = new OpenApiString("movement rules"),
        ["limit"] = new OpenApiInteger(20),
        ["docType"] = new OpenApiArray
        {
            new OpenApiString("base"),
            new OpenApiString("expansion")
        },
        ["gameId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        ["language"] = new OpenApiString("it")
    };
    return operation;
});
```

**AC (D-15)**: `dotnet run` → `/scalar/v1` shows the example. Manual + screenshot in PR body.

**Verification**:
- `dotnet test --filter "FullyQualifiedName~GlobalKbSearchEndpointTests"` → 5 existing + 9 new = 14 integration tests pass.
- `dotnet test --filter "FullyQualifiedName~GlobalKbSearchQueryHandlerTests"` → existing + 3 new counter tests pass.

**Commit**: `feat(kb-globale): #1731 Part B+C — extra coverage + Scalar example + final wiring`

## Final verification

```bash
# All KB BC tests
dotnet test --filter "BoundedContext=KnowledgeBase"
# Expected: existing ~50 (baseline from #1730) + 9 new integration + 3 new counter = ~62 KB tests

# Smoke: backwards-compat baseline preserved
# AC-1/AC-2/AC-3 + 5 existing facet integration tests (Returns_422_*, GameId_*) MUST remain green

# Manual Scalar verification
dotnet run --project apps/api/src/Api
# Open browser http://localhost:8080/scalar/v1 → /knowledge-base/search/global → verify example renders
```

**Required-status-check (P110)**: only `GitGuardian Security Checks` blocks on main-dev. CodeQL + Backend Fast are advisory — if exit code 139 (P119), rerun with `gh run rerun --failed`.

## Risk + Mitigation (Nygard pessimist)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Counter test parallel run flake (shared Prometheus registry) | medium | low (test flake) | Delta-assertion pattern (M-2): read counter pre+post, assert delta. No global reset. |
| Mock extension breaks AC-1/AC-2 backwards-compat | low (field default = 1) | medium (red CI on baseline) | Task 4 uses test-class field `_mockChunksPerGame` defaulting to 1; only facet tests opt-in to 4. |
| Scalar `OpenApiAnyFactory.CreateFromJson` API mismatch | low (Microsoft.OpenApi well-documented) | low (compile fail) | Use direct `OpenApiObject` / `OpenApiArray` constructors (Task 8 sample); fallback `WithDescription` extended if Scalar UI doesn't pick up example. |
| Decision-ID drift fix breaks links in other docs | very low (canonical only referenced from #1731 + PR #1730 body) | low | Append-only D-14/D-15 (no renumber); grep `"D-10\|D-12"` repo-wide before commit to verify no breakage. |
| Counter cardinality explosion (future contributor adds `gameId` value label) | low | medium | D-14 explicit note "NO facet value labels". Code review enforces. |
| #11 cursor stability false-positive (mock determinism) | medium | high (silent test regression) | Mock sets `HybridScore = 0.95 - chunkIdx*0.05 - gameIdx*0.001` (deterministic decreasing); cross-page assertion is "disjoint chunkIds" (provable). |

## Estimated effort

**~3-4h BE**:
- Task 1: 15min (doc only)
- Task 2: 30min (counter + 2 unit tests)
- Task 3: 30min (handler wiring + 1 extra unit test)
- Task 4: 30min (mock extension)
- Task 5: 20min (seed helper)
- Task 6: 45min (3 integration scenarios)
- Task 7: 60min (4 integration scenarios, #11 cursor most complex)
- Task 8: 60min (2 extra + Scalar + final verify)
- Final review (`superpowers:code-reviewer`): 20min
- CI rerun buffer (P67/P119): 20min

**Total**: ~4h optimistic, 5h with one reviewer iteration.

## Out of scope (NOT in this PR)

- Defense-in-depth post-filter in `BuildEnrichmentMapAsync` (locals D-2.a; NOT shipped in #1730 either).
- Structured logging facet pipeline (logInfo with userId+queryHash applied/rejected fields).
- Counter dimension `applied_zero` (revisit if 30-day dashboards show insight gap, NOT now).
- Facet aggregation counts ("Rulebook (12)") — already deferred in #1686 D-9.
- FE Zod schema patch / FilterAccordion impl — FE-owned (#1482).

## Handoff

This plan is the Stage A deliverable. Stage B (implementation) executes the 8 tasks above sequentially as commits.

**Branch**: `feature/issue-1731-kb-globale-facets-followup`
**PR base**: `main-dev`
**PR title**: `feat(kb-globale): #1731 — facet counter + Scalar example + 9 integration scenarios`

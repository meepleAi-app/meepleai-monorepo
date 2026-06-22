# Plan — KB Global Facets BE (Issue #1686)

**Date**: 2026-05-31
**Branch**: `feature/issue-1686-kb-globale-facets-be`
**Base**: `main-dev`
**Decisions**: `claudedocs/spec-panel-1686-decisions.md` (D-1..D-13)

## Goal

Extend `POST /api/v1/knowledge-base/search/global` with optional facets (`DocType`, `GameId`, `Language`) using push-down (filter `PdfDocument` candidates pre-vector-search). Preserve byte-identical legacy behaviour when no facets passed.

## Files touched

### Production code (8 files)

1. `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearch/GlobalKbSearchQuery.cs` — add 3 fields.
2. `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearch/GlobalKbSearchQueryValidator.cs` — allowlist validators for docType (list) + language; bound list size; lower-case normalisation.
3. `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearch/GlobalKbSearchQueryHandler.cs` — push-down: compute `documentIds` from `PdfDocuments` before calling search; short-circuit empty allowlist.
4. `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IMultiGameHybridSearchService.cs` — extend `SearchAsync` with optional `documentIds` parameter.
5. `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/MultiGameHybridSearchService.cs` — filter aggregated results to `documentIds` (post-aggregation client-side filter; cheap because pre-filtered candidate set is already small).
6. `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs` — add 3 fields to `GlobalKbSearchRequest` record + forward to query; update endpoint XML docs.

### Test code (5 files; ~50 tests)

7. `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearchQueryHandlerTests.cs` — extend with facet tests (push-down, empty short-circuit, RBAC×facet, list normalisation).
8. `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GlobalKbSearchQueryValidatorTests.cs` (NEW) — validator allowlist + bounds + normalisation.
9. `apps/api/tests/Api.Tests/Integration/KnowledgeBase/GlobalKbSearchEndpointTests.cs` — extend with end-to-end facet scenarios.
10. `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/DTOs/GlobalKbSearchResponseDtoSnapshotTests.cs` (NEW) — frozen response shape.

## TDD task list (10 tasks, 1 commit each)

### Task 1 — Validator: `DocType` list allowlist + length cap

**Test file**: NEW `GlobalKbSearchQueryValidatorTests.cs`.

**Tests (~6 unit)**:
- `EmptyDocTypeList_TreatedAsNull_Passes` — `DocType = []` → valid.
- `DocTypeWithUnknownValue_FailsValidation` — `DocType = ["unknown"]` → fail, error mentions allowed values.
- `DocTypeWithDuplicates_Allowed` — `DocType = ["base", "BASE"]` → valid (normalised).
- `DocTypeListSizeAboveCap_FailsValidation` — 11 elements → fail.
- `DocTypeWithMixedCase_Passes` — `["Base", "EXPANSION"]` → valid.
- `DocTypeWithEmptyString_FailsValidation` — `["base", ""]` → fail.

**Code change**: extend `GlobalKbSearchQueryValidator` — add `RuleForEach(x => x.DocType)` with allowlist `["base", "expansion", "errata", "homerule"]` (case-insensitive); `RuleFor(x => x.DocType).Must(d => d == null || d.Count <= 10)`.

**Commit**: `feat(kb): #1686 Task 1 — validator allowlist + cap for DocType`

### Task 2 — Validator: `Language` allowlist

**Tests (~5 unit)**:
- `NullLanguage_Passes` — `Language = null` → valid.
- `LanguageEn_Passes`, `LanguageIt_Passes`, etc. — each ISO code → valid.
- `LanguageUnknown_FailsValidation` — `"xx"` → fail.
- `LanguageMixedCase_Passes` — `"IT"` → valid (normalised).
- `LanguageEmpty_FailsValidation` — `""` → fail.

**Code**: add `RuleFor(x => x.Language).Must(l => l == null || AllowedLanguages.Contains(l.ToLowerInvariant()))`.

**Commit**: `feat(kb): #1686 Task 2 — validator allowlist for Language`

### Task 3 — Query: add facet fields

**Test**: extend handler test `BuildQuery` helper signature; verify backward compat — existing tests using default args still pass.

**Code**: extend `GlobalKbSearchQuery` record with `IReadOnlyList<string>? DocType`, `Guid? GameId`, `string? Language`.

**Commit**: `feat(kb): #1686 Task 3 — extend GlobalKbSearchQuery with facets`

### Task 4 — Service interface: `documentIds` parameter

**Test**: `MultiGameHybridSearchService` test (if exists) verifying new param behaviour OR a contract test on `IMultiGameHybridSearchService` ensuring the new overload semantics. Stub-level if implementation is complex.

**Code**: extend `IMultiGameHybridSearchService.SearchAsync` with optional `IReadOnlyList<Guid>? documentIds = null`; extend implementation to post-filter `aggregated.Where(r => documentIds.Contains(parsedPdfId))` when non-null.

**Commit**: `feat(kb): #1686 Task 4 — IMultiGameHybridSearchService documentIds param`

### Task 5 — Handler: `GameId` intersection with RBAC

**Tests (~3 unit)**:
- `GameIdInAccessibleSet_NarrowsToThatGame` — `GameId = gameA` ∈ accessible → search called with `[gameA]` only.
- `GameIdNotInAccessibleSet_ReturnsEmpty_NoCallToSearch` — `GameId = gameC` ∉ accessible → 200 empty, search never called.
- `GameIdNull_KeepsAllAccessible` — backward compat.

**Code**: in handler, after `accessibleGameIds = RBAC(...)`, if `query.GameId.HasValue`, intersect: `if (!accessibleGameIds.Contains(query.GameId.Value)) → empty 200; else accessibleGameIds = new[] { query.GameId.Value }`.

**Commit**: `feat(kb): #1686 Task 5 — handler GameId intersection with RBAC`

### Task 6 — Handler: push-down docType + language → documentIds

**Tests (~4 unit)**:
- `DocTypeFacet_FiltersPdfDocsBeforeSearch` — seeds PDFs with mixed types, expects search called with documentIds matching only "base".
- `LanguageFacet_FiltersPdfDocs` — same pattern for `Language = "it"`.
- `DocTypeAndLanguage_BothApplied` — combination AND filter.
- `NoFacets_PassesNullDocumentIds` — backward compat: no facets → search called with `documentIds: null`.

**Code**: in handler, when ANY of `DocType`/`Language` set, compute `documentIds` via single EF query against `_db.PdfDocuments` filtered by `(SharedGameId ∈ accessibleGameIds) AND (DocumentType IN docTypeNormalised) AND (Language == languageNormalised)`. Pass to search via new param.

**Commit**: `feat(kb): #1686 Task 6 — handler push-down DocType+Language facets`

### Task 7 — Handler: empty documentIds short-circuit (EC short-circuit)

**Tests (~2 unit)**:
- `FacetsYieldEmptyDocumentIds_ReturnsEmpty_NoCallToSearch` — facets that match zero PDFs → 200 empty, search never called.
- `FacetsYieldEmptyDocumentIds_LoggedAtDebugLevel` — debug log emitted (best effort, may verify via NullLogger inspection or skip).

**Code**: in handler, if `documentIds` computed and is empty (post-facet narrowing), short-circuit (D-11).

**Commit**: `feat(kb): #1686 Task 7 — handler empty allowlist short-circuit`

### Task 8 — Endpoint: extend `GlobalKbSearchRequest` + forward

**Tests (~4 integration)** in `GlobalKbSearchEndpointTests.cs`:
- `Returns_422_when_DocType_unknown` — `DocType = ["banana"]` → 422.
- `Returns_422_when_Language_unknown` — `Language = "xx"` → 422.
- `Facets_NarrowResults_EndToEnd` — seeds + asserts only matching results returned.
- `GameId_OfPrivateUnowned_Returns_200_Empty` — RBAC composition: requesting GameId of a private game not owned → 200 empty, NO 403.

**Code**: add facet fields to `GlobalKbSearchRequest` record + forward to query construction in `HandleGlobalSearch`.

**Commit**: `feat(kb): #1686 Task 8 — endpoint forwards facets to query`

### Task 9 — Snapshot BC: response shape preserved when no facets

**Tests (~3 unit + snapshot)** in NEW `GlobalKbSearchResponseDtoSnapshotTests.cs`:
- `ResponseShape_NoFacets_MatchesBaseline` — serialise a known `GlobalKbSearchResponseDto`, assert JSON keys/order match a frozen string baseline (results / hasMore / nextCursor + nested item shape).
- `ResponseShape_NoFacets_Empty_MatchesBaseline` — empty results case.
- `ResponseShape_HasNoFacetEcho` — verify response DTO has NO facet echo fields (request-only).

**Code**: no production code changes — pure pin-down of the contract.

**Commit**: `test(kb): #1686 Task 9 — snapshot BC for response shape`

### Task 10 — Doc: update XML docs + decisions log

**Tests**: none (doc-only).

**Code**: update XML doc-comments on `GlobalKbSearchRequest` to describe facets (D-9), point to decisions doc.

**Commit**: `docs(kb): #1686 Task 10 — XML docs for facets + decisions log`

## Verification milestones

- After Task 2: `dotnet test --filter "FullyQualifiedName~GlobalKbSearchQueryValidator"` → 11 passing.
- After Task 7: `dotnet test --filter "FullyQualifiedName~GlobalKbSearchQueryHandler"` → ~17 passing (existing 10 + new 7 facet).
- After Task 8: `dotnet test --filter "FullyQualifiedName~GlobalKbSearch"` integration → ~9 passing (existing 5 + 4 new).
- After Task 9: snapshot test green.

Final test count goal: **~45 new tests** across unit + integration + snapshot.

## Final review

Invoke `superpowers:code-reviewer` subagent on diff. Address Critical / Major. Push, open PR base `main-dev`.

## CI expectations

- GitGuardian: SUCCESS (only required check on main-dev).
- Backend Fast: SUCCESS (or P119 catastrophic exit 139 → rerun).
- Migration Safety Gate: SUCCESS (no schema changes — additive query/handler/validator changes only).
- CodeQL csharp: PENDING/advisory.

Merge: normal `--squash` once GitGuardian green and no real regression.

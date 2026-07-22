# Unified Hybrid Fusion — Design Spec

**Date**: 2026-07-22 · **Epic**: RAG retrieval heading-aware answer-quality (#3266) · **Relates to**: SP4 (#3270 ranking)

> Revised after adversarial spec review (10 findings applied): the core is a merge-and-score primitive; each adapter re-joins its own arm by key for type-specific output fields; `Domain.Entities.SearchResult` gains `PdfDocumentId`+`ChunkIndex`+`RoleTags`; the primary path preserves the #2712 cosine relevance and the #2051 documentIds/phrase filters.

## Problem

MeepleAI's RAG retrieval has **two divergent hybrid-fusion implementations**, and the signal-rich one runs in an admin tool while the signal-poor one serves real users.

- **`RrfFusionDomainService.FuseResults`** (`Domain/Services/VectorSearch/RrfFusionDomainService.cs:22-92`) — **UNWEIGHTED** RRF (both arms `1.0/(rrfK+rank)`, k=60), **no legend-demotion, no role-boost, no heading**. Sole caller: `SearchQueryHandler.cs:212`, which is the **user-facing chat path** (`/agents/qa` and `/agents/qa/stream`). It deliberately preserves the source **cosine** as `RelevanceScore` (RRF drives order only) — issue #2712, `RrfFusionDomainService.cs:81-85`.
- **`HybridSearchService.FuseSearchResults`** (`Services/HybridSearchService.cs:394-527`) — **WEIGHTED** RRF (0.7 vector / 0.3 keyword, per-call params), **legend-demotion** (`ComputeLegendPenaltyFactor`, `:488`) + **role-boost** (`ComputeRoleMatchBoost`, additive 0.15, `:470,:489`). Reached only via `SearchAsync(SearchMode.Hybrid)`, whose primary consumer is the **admin playground** (`PlaygroundChatCommandHandler`) plus 6 other Hybrid-mode callers.

Consequences (verified against source):
1. **Legend-demotion (#3243, "retrieval epic 2/3") never reaches production chat.** `ComputeLegendPenaltyFactor` has exactly one production call site — `HybridSearchService.cs:488` — unreachable from `/agents/qa[/stream]`. PR #3243's author appears to have assumed `FuseSearchResults` was *the* fusion path; no ADR/comment scopes it to the playground. **Oversight, not design.**
2. **Role-boost inconsistent on the primary path.** `/agents/qa` gets role-boost only as a side effect of its keyword sub-call (`SearchAsync(SearchMode.Keyword, roleHint)` applies the boost + re-sorts inside `SearchKeywordOnlyAsync`), so it rides in on the keyword rank — **keyword-arm only, not the vector arm**. `/agents/qa/stream` gets **no** role-boost (`StreamQaQueryHandler.cs:284-292` never sets `QueryRoleHint`).
3. The primary path's vector arm **already fetches `role_tags`** (scored pgvector SQL → `Embedding.RoleTags`, `PgVectorStoreAdapter.cs:141-149`) and its keyword arm too (`KeywordSearchService.cs:106` → `KeywordSearchResult.RoleTags`), but both are dropped because `Domain.Entities.SearchResult` has no `RoleTags` field.

## Goal

Replace the two fusion implementations with **one canonical fusion core** applying weighted RRF + legend-demotion + role-boost consistently, so the user-facing chat path receives the same ranking signals as the admin playground. Standardize on **weighted 0.7/0.3** (per-call configurable) everywhere.

**Out of scope**: heading-based boost (SP4's other item — types don't carry `Heading`; deferred), the corpus re-index that materializes headings/role_tags on existing rows (SP3, #3269), and the EN/IT non-regression suite (SP3).

## Design

### 1. `HybridFusionCore` — the single canonical fusion

New pure/static Domain component (`Domain/Services/VectorSearch/HybridFusionCore.cs`). It **owns the arm merge + scoring + ordering**; it does NOT know the caller's I/O types. Each adapter maps its arm items into the neutral input and re-joins its own arm items for type-specific output fields (see §3/§4).

**Input** — per arm, a neutral candidate with only what scoring needs:
```csharp
internal readonly record struct FusionCandidate(
    string Key,             // stable identity "{PdfDocumentId}_{ChunkIndex}" (the #3262 RRF key); built by the adapter
    string Content,         // for the legend regex
    GameBookRole RoleTags,  // for the role boost
    int Rank,               // 1-based rank within THIS arm (source order: cosine desc / ts_rank_cd desc)
    float SourceScore);     // arm-native score (cosine or ts_rank_cd) — carried to output, not fused

internal sealed record FusionOptions(
    float VectorWeight = 0.7f, float KeywordWeight = 0.3f, int RrfK = 60,
    GameBookRole QueryRoleHint = GameBookRole.None);
```

**Method** `static IReadOnlyList<FusedCandidate> Fuse(IReadOnlyList<FusionCandidate> vectorArm, IReadOnlyList<FusionCandidate> keywordArm, FusionOptions options)`. **Merge + score semantics (specified verbatim from the current `FuseSearchResults`, `:404-505`):**
- **Dedup within an arm**: group by `Key`, keep the **best (lowest) rank** occurrence (matches the current keyword-arm `GroupBy(Key).First()` at `:419-422`; apply the same to the vector arm rather than the current throw-on-duplicate `ToDictionary` so the core is total).
- **Cross-arm union**: a chunk may appear in one or both arms. `vectorRrf = present ? VectorWeight/(RrfK + vectorRank) : 0`; `keywordRrf` likewise. `rrfSum = vectorRrf + keywordRrf`.
- **Merged content**: **prefer the vector arm's** content when the chunk is in both (`:478-480`) — this is load-bearing because `legendFactor` is computed from it.
- **Merged RoleTags**: `vectorRoleTags | keywordRoleTags` (OR-union, `:469`).
- **Score**: `legendFactor = FusionSignals.ComputeLegendPenaltyFactor(mergedContent)` (`[0,0.5]`, 0 for <2 cross-ref pointers); `roleBoost = FusionSignals.ComputeRoleMatchBoost(options.QueryRoleHint, mergedRoleTags)` (additive 0.15); `hybridScore = (rrfSum * (1 - legendFactor)) + roleBoost` (role stays additive on top — `:486-489`).
- **Order**: by `hybridScore` **desc**, with a **deterministic secondary tie-break by `Key` (ordinal)** so results are reproducible (the current code returns HashSet-enumeration order then the caller sorts, `:436/SearchHybridAsync:314`; the core makes the sort explicit and deterministic).

**Output** — the scoring result keyed by `Key`; adapters re-join their arm items for everything else:
```csharp
internal readonly record struct FusedCandidate(
    string Key, string Content, GameBookRole RoleTags,
    float HybridScore, float? VectorScore, float? KeywordScore, int? VectorRank, int? KeywordRank,
    int Rank);          // 1-based rank in the fused order
```
`VectorScore`/`KeywordScore` carry the arm-native `SourceScore` (cosine / ts_rank_cd); `VectorRank`/`KeywordRank` the arm ranks (needed by `MultiGameHybridSearchService`'s cross-game tie-break). Pure — no logging, no injected state.

### 2. Shared signal helpers → `FusionSignals`

Move `ComputeLegendPenaltyFactor`, `ComputeRoleMatchBoost`, the `CrossReferencePointer` legend regex, and `RoleMatchBoost = 0.15f` / `DefaultRrfK = 60` out of `HybridSearchService.cs` (`:35,:29,:536-544,:548-551,:560-580`) into a `FusionSignals` static (Domain, sibling of `HybridFusionCore`). They are already `internal static`, pure, zero injected state; only cross-BC ref is `GameBookRole` (KB Domain already references it). **Move + repoint every reference, don't delete**:
- `HybridSearchService.SearchKeywordOnlyAsync` (`:206/:215`) also calls `ComputeRoleMatchBoost` → repoint to `FusionSignals.ComputeRoleMatchBoost`.
- **`apps/api/tests/Api.Tests/Services/HybridSearchServiceRoleBoostTests.cs`** references `HybridSearchService.ComputeRoleMatchBoost` / `.ComputeLegendPenaltyFactor` / `.RoleMatchBoost` directly in 10+ places (`:36,50,65,80,94,108,109,130,141,154,165,167,184,206`) — repoint all to `FusionSignals.*` (or keep thin `internal static` forwarders on `HybridSearchService`; prefer repointing the tests).

### 3. `HybridSearchService.FuseSearchResults` → thin adapter (behavior-preserving)

Rewrite the private `FuseSearchResults` (`:394-527`) to:
1. Map its `SearchResultItem` vector + keyword arms → `FusionCandidate` (Key `"{PdfId}_{ChunkIndex}"`, Content=`Text`, RoleTags, Rank, SourceScore=`Score`).
2. Call `HybridFusionCore.Fuse` with the per-call `vectorWeight`/`keywordWeight`/`rrfK` it already receives.
3. **Re-join by `Key`** to recover the type-specific fields the core doesn't carry, then build each `HybridSearchResult`:
   - `MatchedTerms` = keyword arm item's `MatchedTerms` (`:473-475,520`) — **must be preserved** (consumed by `HybridSearchEngine.cs:215` + `MultiGameHybridSearchService.cs:203`).
   - `GameId` = keyword arm's `GameId` else the query `gameId` (`:495-497`).
   - `PdfDocumentId` = **string** (keep as-is end-to-end; do NOT round-trip through Guid); `ChunkIndex`, `PageNumber` **coalesced to 0** when keyword-only null (`:505`); `Mode = SearchMode.Hybrid`; `VectorScore`/`KeywordScore`/`VectorRank`/`KeywordRank`/`HybridScore` from `FusedCandidate`.

**Behavior must be observably identical to today** — same weights, same formula, same output fields including `MatchedTerms`/`GameId`/`PageNumber`. Guarded by the parity test (§Risk). `FuseSearchResults` is private with one internal caller (`SearchHybridAsync`, `:305`); `SearchAsync`'s public signature + `List<HybridSearchResult>` return are unchanged → **all Hybrid-mode callers unaffected**: `PlaygroundChatCommandHandler`, `AskArbiterCommandHandler`, `GenerateToolkitFromKbHandler`, `HybridSearchEngine` (A/B weights 0.8/0.2, 0.5/0.5, 0.3/0.7 — **weights MUST stay per-call inputs**), `ResilientRetrievalService`, `MultiGameHybridSearchService` (HybridScore order + VectorScore/KeywordScore tie-break), `RagService.ExecuteHybridRetrievalAsync`, and indirectly `CrossGameStreamQaQueryHandler` + `PromptEvaluationService`.

### 4. `RrfFusionDomainService.FuseResults` → adapter for the primary path (behavioral change)

Rewrite it to: map its `Domain.Entities.SearchResult` vector + keyword arms → `FusionCandidate`, call `HybridFusionCore.Fuse` with `FusionOptions(0.7f, 0.3f, 60, queryRoleHint)`, re-join by `Key` to the original `SearchResult`s, and map `FusedCandidate` back to `Domain.Entities.SearchResult` in fused order. The signature gains a `GameBookRole queryRoleHint = GameBookRole.None` param (default keeps existing callers/tests compiling).

**#2712 preservation (mandatory)**: `SearchResult.RelevanceScore` = the carried **cosine**, i.e. `new Confidence(FusedCandidate.VectorScore ?? FusedCandidate.KeywordScore)` — **NOT** `HybridScore`. `HybridScore` drives **order only**. (Feeding the RRF/hybrid score into confidence made it degenerate ~0.53 and hid grounded answers behind the "Non sono certo" card — `RrfFusionDomainService.cs:81-84`.) A primary-path test asserts a both-arm result keeps its cosine, not the RRF score.

This is where the **primary chat path changes**: unweighted → weighted 0.7/0.3, and it gains legend-demotion + role-boost on both arms (order only; confidence unchanged).

### 5. `Domain.Entities.SearchResult` gains `PdfDocumentId` + `ChunkIndex` + `RoleTags`

`Domain.Entities.SearchResult` (`SearchResult.cs:10-55`) today carries `VectorDocumentId`, `TextContent`, `PageNumber`, `RelevanceScore`, `Rank`, `SearchMethod` — **no PdfDocumentId, no ChunkIndex, no RoleTags**. The canonical `Key = "{PdfDocumentId}_{ChunkIndex}"` (the #3262 intersecting key) needs the first two; role-boost needs the third. Add all three as properties + **defaulted ctor params** (so the ~8 existing construction sites keep compiling). Populate at the two primary-path arm sources where the data already arrives but is dropped:
- **Vector arm** — `SearchQueryHandler.PerformVectorSearchAsync` (`:159-167`): set `PdfDocumentId = scored.Embedding.PdfDocumentId`, `ChunkIndex = scored.Embedding.ChunkIndex`, `RoleTags = (GameBookRole)scored.Embedding.RoleTags`.
- **Keyword arm** — the new `KeywordSearchResult` → `SearchResult` mapper (§6): set `PdfDocumentId = Guid.Parse(kr.PdfDocumentId)`, `ChunkIndex = kr.ChunkIndex`, `RoleTags = kr.RoleTags`.

(`Content`/`TextContent` already present for the legend regex.)

### 6. Primary path arm sourcing — raw keyword arm, filter parity, no double-boost

Today `SearchQueryHandler.PerformHybridSearchAsync` (`:177-213`) sources its keyword arm via `HybridSearchService.SearchAsync(SearchMode.Keyword, roleHint)`, which **already role-boosts + re-sorts** (`SearchKeywordOnlyAsync`, `:215`). Feeding that into a core that ALSO applies role-boost double-counts it. Fix: source a **raw** keyword ranking and let the core apply legend + role exactly once.

**Chosen approach**: call `IKeywordSearchService` **directly** (returns `KeywordSearchResult` in raw ts_rank_cd order with `RoleTags` + `Content` + `PdfDocumentId` + `ChunkIndex`), map via the new §5 mapper. **But `SearchAsync(SearchMode.Keyword)` also applies three things `IKeywordSearchService.SearchAsync` does not — all MUST be reproduced in `SearchQueryHandler` to avoid a scope/behavior regression**:
- **`keywordMinScore = 0.01`** filter (drops ToC noise) — currently passed at `SearchQueryHandler.cs:196-204`.
- **`documentIds` filter** (Issue #2051, `HybridSearchService.cs:195-197`) — document-scoped retrieval; `IKeywordSearchService.SearchAsync` has **no** `documentIds` parameter, so the filter must be applied in `SearchQueryHandler` (with the correct `VectorDocument.Id → PdfDocumentId` id basis). If `SearchQuery` carries no documentIds today on this path, confirm and preserve current behavior.
- **`phraseSearch`** = `query.Contains('"')` (`HybridSearchService.cs:189/:263`) — pass through to `IKeywordSearchService` if it supports it, else preserve the current phrase behavior.

Then `PerformHybridSearchAsync` passes the classified `queryRoleHint` into `RrfFusionDomainService.FuseResults(..., queryRoleHint)`. The plan verifies `IKeywordSearchService` is injectable into `SearchQueryHandler` and adds a documentIds-filter primary-path test.

### 7. `StreamQaQueryHandler` role-hint parity

`StreamQaQueryHandler` (`:278-347`) must classify intent and set `QueryRoleHint` on its `SearchQuery` (mirroring `AskQuestionQueryHandler.cs:429-443`), so the stream path gets role-boost parity. Small isolated change with its own test.

### Data flow (primary path, after)

```
query → IntentClassifier → roleHint
      → [ vector arm: raw pgvector cosine ranking + PdfDocumentId/ChunkIndex/RoleTags ]
      → [ keyword arm: raw ts_rank_cd ranking (IKeywordSearchService) + PdfDocumentId/ChunkIndex/RoleTags, minScore+documentIds+phrase preserved ]
      → HybridFusionCore.Fuse(0.7, 0.3, 60, roleHint)      // weighted RRF + legend + role, order only
      → RelevanceScore = cosine (#2712)                     // confidence unchanged
      → cross-encoder reranker (unchanged)
      → answer
```

## Risk & validation

**This changes production chat ranking** (`/agents/qa` + `/stream`): unweighted → weighted 0.7/0.3 + legend + two-arm role-boost (order only; confidence still cosine). No EN/IT retrieval non-regression suite exists yet (SP3), so real-data quality can't be fully gated here. Mitigation:

1. **Parity test (mandatory)** — prove `HybridSearchService.FuseSearchResults` is observably identical before/after, asserting **post-sort order + HybridScore + VectorScore + KeywordScore + `MatchedTerms` + `GameId` + `PdfDocumentId` (string form) + `PageNumber` (null→0)**. Cases: both-arm, vector-only, keyword-only, duplicate-key, keyword-only-null-page, tie-break-by-Key.
2. **Core unit tests** (`HybridFusionCoreTests`) — weighted RRF math (incl. A/B weights 0.8/0.2, 0.5/0.5, 0.3/0.7), content-prefer-vector, RoleTags OR-union, legend-demotion (≥2-pointer chunk demotes below real content), role-boost (matching-role rises; additive-on-top), deterministic tie-break, empty-arm handling.
3. **Primary-path integration test** — legend-demotion + role-boost now reach `/agents/qa`; `RelevanceScore` equals cosine (#2712, not RRF); a `documentIds`-scoped query still excludes out-of-scope chunks (#2051); `StreamQa` sets the role hint.
4. **`FusionSignals` helper tests** — the moved `HybridSearchServiceRoleBoostTests` assertions still pass against `FusionSignals.*`.
5. **TM "setup per N giocatori" (#3243) repro** — qualitative check; fully validatable only after SP3 re-index materializes headings/role_tags on the corpus. Documented, not automated here.
6. **No new SQL / no migration** — role_tags already fetched, heading out of scope; `dotnet ef migrations has-pending-model-changes` stays clean.

## File map

**Create**:
- `Domain/Services/VectorSearch/HybridFusionCore.cs` — `Fuse` + `FusionCandidate`/`FusionOptions`/`FusedCandidate`.
- `Domain/Services/VectorSearch/FusionSignals.cs` — moved `ComputeLegendPenaltyFactor`/`ComputeRoleMatchBoost`/regex/constants.
- Tests: `HybridFusionCoreTests.cs`, a `FuseSearchResults` parity test, primary-path signal-reach + #2712 + #2051 handler tests, `StreamQaQueryHandler` role-hint test.

**Modify**:
- `Services/HybridSearchService.cs` — `FuseSearchResults` → adapter (re-join by Key for MatchedTerms/GameId/page/PdfId string); repoint `SearchKeywordOnlyAsync`'s `ComputeRoleMatchBoost` to `FusionSignals`; remove the moved members.
- `Domain/Services/VectorSearch/RrfFusionDomainService.cs` — → adapter + `queryRoleHint` param + **RelevanceScore = cosine (#2712)**.
- `Domain/Entities/SearchResult.cs` — add `PdfDocumentId` (Guid) + `ChunkIndex` (int) + `RoleTags` (GameBookRole), defaulted ctor params.
- `Application/Queries/SearchQueryHandler.cs` — raw keyword arm via `IKeywordSearchService` (preserve `keywordMinScore` + `documentIds` #2051 + `phraseSearch`); thread vector-arm PdfId/ChunkIndex/RoleTags + `queryRoleHint` into `FuseResults`.
- `Application/Mappers/KnowledgeBaseMappers.cs` — add `KeywordSearchResult` → `SearchResult` mapper carrying PdfId/ChunkIndex/RoleTags (sibling to `ToDomainSearchResult`).
- `Application/Queries/StreamQaQueryHandler.cs` — classify intent + set `QueryRoleHint`.
- `apps/api/tests/Api.Tests/Services/HybridSearchServiceRoleBoostTests.cs` — repoint helper references to `FusionSignals.*`.

## Testing

Backend xUnit + Moq + FluentAssertions. Core + parity + `FusionSignals` tests are pure unit (no infra). Primary-path signal-reach / #2712 / #2051 tests are handler-level (mocked search services). `dotnet build` 0 warnings (`TreatWarningsAsErrors`). No Testcontainers for the fusion itself (arms mocked); existing hybrid integration tests stay green.

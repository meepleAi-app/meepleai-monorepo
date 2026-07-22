# Unified Hybrid Fusion — Design Spec

**Date**: 2026-07-22 · **Epic**: RAG retrieval heading-aware answer-quality (#3266) · **Relates to**: SP4 (#3270 ranking)

## Problem

MeepleAI's RAG retrieval has **two divergent hybrid-fusion implementations**, and the signal-rich one runs in an admin tool while the signal-poor one serves real users.

- **`RrfFusionDomainService.FuseResults`** (`Domain/Services/VectorSearch/RrfFusionDomainService.cs:22-92`) — **UNWEIGHTED** RRF (both arms `1.0/(rrfK+rank)`, k=60), **no legend-demotion, no role-boost, no heading**. Sole caller: `SearchQueryHandler.cs:212`, which is the **user-facing chat path** (`/agents/qa` and `/agents/qa/stream`).
- **`HybridSearchService.FuseSearchResults`** (`Services/HybridSearchService.cs:394-527`) — **WEIGHTED** RRF (0.7 vector / 0.3 keyword, per-call params), **legend-demotion** (`ComputeLegendPenaltyFactor`, `:488`) + **role-boost** (`ComputeRoleMatchBoost`, additive 0.15, `:470,:489`). Reached only via `SearchAsync(SearchMode.Hybrid)`, whose primary consumer is the **admin playground** (`PlaygroundChatCommandHandler`) plus 6 other Hybrid-mode callers.

Consequences (verified against source):
1. **Legend-demotion (#3243, "retrieval epic 2/3") never reaches production chat.** `ComputeLegendPenaltyFactor` has exactly one production call site — `HybridSearchService.cs:488` — unreachable from `/agents/qa[/stream]`. PR #3243 framed it as a general answer-quality fix and its author appears to have assumed `FuseSearchResults` was *the* fusion path; there is no ADR or comment scoping it to the playground. This is an **oversight, not a deliberate design decision**.
2. **Role-boost is inconsistent on the primary path.** `/agents/qa` gets role-boost only via a side effect: its keyword sub-call (`SearchAsync(SearchMode.Keyword, roleHint)`) applies the boost inside `SearchKeywordOnlyAsync` and re-sorts, so the boost rides in on the keyword rank fed to RRF — **keyword-arm only, not the vector arm**. `/agents/qa/stream` gets **no** role-boost at all because `StreamQaQueryHandler.cs:284-292` never sets `QueryRoleHint` (defaults to `GameBookRole.None`).
3. The primary path's vector arm **already fetches `role_tags`** (the scored pgvector SQL selects them → `Embedding.RoleTags`, `PgVectorStoreAdapter.cs:141-149`), and the keyword arm fetches them too (`KeywordSearchService.cs:106` → `KeywordSearchResult.RoleTags`), but **both are silently dropped** because `Domain.Entities.SearchResult` has no `RoleTags` field. So the data is present; only the plumbing is missing.

## Goal

Replace the two fusion implementations with **one canonical fusion core** that applies weighted RRF + legend-demotion + role-boost consistently, so the user-facing chat path receives the same ranking signals as the admin playground. Standardize on the **weighted 0.7/0.3** behavior (per-call configurable) everywhere.

**Non-goal / explicitly out of scope**: heading-based boost (SP4's other remaining item — `SearchResult`/`HybridSearchResult` don't carry `Heading`; deferred), the actual corpus re-index that materializes headings/role_tags on existing rows (SP3, #3269), and the EN/IT retrieval non-regression suite (SP3). This spec unifies the fusion CODE and its signal set; it does not build the corpus-level quality gate.

## Design

### 1. `HybridFusionCore` — the single canonical fusion

New pure/static Domain component under `Domain/Services/VectorSearch/`. One implementation of the fusion formula, extracted verbatim from `HybridSearchService.FuseSearchResults` (`:447-489`):

```
vectorRrf  = vectorWeight  / (rrfK + vectorRank)
keywordRrf = keywordWeight / (rrfK + keywordRank)
legendFactor = ComputeLegendPenaltyFactor(content)            // [0, 0.5], 0 for <2 cross-ref pointers
roleBoost    = ComputeRoleMatchBoost(queryRoleHint, roleTags) // additive 0.15 when (hint & tags) != None
hybridScore  = ((vectorRrf + keywordRrf) * (1 - legendFactor)) + roleBoost   // role stays additive on top
```

**Input contract** — a neutral per-arm candidate carrying exactly what the formula needs, so both call sites can adapt into it:

```csharp
internal readonly record struct FusionCandidate(
    string Key,               // stable identity: {PdfDocumentId}_{ChunkIndex} (matches the #3262 RRF key fix)
    string Content,           // for the legend regex
    GameBookRole RoleTags,    // for the role boost
    Guid PdfDocumentId,
    int ChunkIndex,
    int? PageNumber,
    int Rank,                 // 1-based rank within this arm
    float SourceScore);       // arm-native score (cosine for vector, ts_rank_cd for keyword) — carried through, not fused

internal sealed record FusionOptions(
    float VectorWeight = 0.7f,
    float KeywordWeight = 0.3f,
    int RrfK = 60,
    GameBookRole QueryRoleHint = GameBookRole.None);
```

**Output** — one fused, ranked list carrying every field the two current outputs need (superset), so each adapter can project its own return type:

```csharp
internal readonly record struct FusedCandidate(
    string Key, string Content, Guid PdfDocumentId, int ChunkIndex, int? PageNumber,
    GameBookRole RoleTags,
    float HybridScore, float? VectorScore, float? KeywordScore, int? VectorRank, int? KeywordRank,
    int Rank);            // 1-based rank in the fused order
```

**Method**: `static IReadOnlyList<FusedCandidate> Fuse(IReadOnlyList<FusionCandidate> vectorArm, IReadOnlyList<FusionCandidate> keywordArm, FusionOptions options)`. It keys both arms on `Key`, sums the weighted RRF contributions, applies legend + role per candidate, orders by `HybridScore` desc, and preserves `VectorScore`/`KeywordScore`/`VectorRank`/`KeywordRank` (needed by `MultiGameHybridSearchService`'s cross-game tie-break). Pure — no logging, no injected state.

### 2. Shared signal helpers → Domain

Move `ComputeLegendPenaltyFactor`, `ComputeRoleMatchBoost`, the `CrossReferencePointer` legend regex, and the `RoleMatchBoost = 0.15f` / `DefaultRrfK = 60` constants out of `HybridSearchService.cs` (`:35,:29,:536-544,:548-551,:560-580`) into a `FusionSignals` static (sibling of `HybridFusionCore` in Domain). They are already `internal static` and pure with zero injected state; the only cross-BC reference is `GameBookRole`, which KB Domain already references. **Move, don't delete** — `ComputeRoleMatchBoost` has a second caller inside `HybridSearchService.SearchKeywordOnlyAsync` (`:206/:215`, the `SearchMode.Keyword` role re-sort); repoint that reference (and any other) to `FusionSignals.ComputeRoleMatchBoost` so no behavior changes for the standalone Keyword mode. `FuseSearchResults`'s three `_logger.LogDebug` calls (`:429-431`) are dropped so the core stays pure.

### 3. `HybridSearchService.FuseSearchResults` → thin adapter (pure refactor)

Rewrite the private `FuseSearchResults` (`:394-527`) to: map its `SearchResultItem` vector + keyword arms → `FusionCandidate`, call `HybridFusionCore.Fuse` with the same per-call `vectorWeight`/`keywordWeight`/`rrfK` it already receives, and map `FusedCandidate` → `HybridSearchResult`. **Behavior must be byte-identical to today** — same weights, same formula, same output fields. Guarded by a **parity test** (below). Because `FuseSearchResults` is a private method with a single internal caller (`SearchHybridAsync`, `:305`) and `SearchAsync`'s public signature + `List<HybridSearchResult>` return are unchanged, **all Hybrid-mode callers are unaffected**:

`PlaygroundChatCommandHandler`, `AskArbiterCommandHandler`, `GenerateToolkitFromKbHandler`, `HybridSearchEngine` (passes A/B-variant weights 0.8/0.2, 0.5/0.5, 0.3/0.7 — **so weights MUST stay per-call inputs**), `ResilientRetrievalService`, `MultiGameHybridSearchService` (depends on `HybridScore` ordering + `VectorScore`/`KeywordScore` tie-break), `RagService.ExecuteHybridRetrievalAsync`, and indirectly `CrossGameStreamQaQueryHandler` + `PromptEvaluationService`.

### 4. `RrfFusionDomainService.FuseResults` → adapter for the primary path (behavioral change)

Rewrite it to map its `Domain.Entities.SearchResult` vector + keyword arms → `FusionCandidate`, call `HybridFusionCore.Fuse` with `FusionOptions(0.7f, 0.3f, 60, queryRoleHint)`, and map `FusedCandidate` back to `Domain.Entities.SearchResult`. This is where the **primary chat path changes**: unweighted → weighted 0.7/0.3, and it gains legend-demotion + role-boost on both arms. The method signature gains a `GameBookRole queryRoleHint` parameter (default `None` for backward-compatible callers/tests).

### 5. `Domain.Entities.SearchResult` gains `RoleTags`

Add a `GameBookRole RoleTags` property (default `None`) to `Domain.Entities.SearchResult` (`SearchResult.cs:10-55`) + ctor param. Populate it at the two primary-path arm sites where the data already arrives but is dropped:
- **Vector arm**: `SearchQueryHandler.PerformVectorSearchAsync` (`:159-167`) — set from `(GameBookRole)scored.Embedding.RoleTags`.
- **Keyword arm**: the new `KeywordSearchResult` → `SearchResult` mapper (per §6) — set from `KeywordSearchResult.RoleTags`.

(`Content`/`TextContent` is already present for the legend regex.) Note: the existing `KnowledgeBaseMappers.ToDomainSearchResult` (`HybridSearchResult` → `SearchResult`, `:64-83`) is no longer on the primary keyword path once §6 switches to `IKeywordSearchService` direct; leave it in place for any other consumers but it need not carry `RoleTags`.

### 6. Primary path arm sourcing — avoid double role-boost

Today `SearchQueryHandler.PerformHybridSearchAsync` (`:177-213`) sources its keyword arm via `HybridSearchService.SearchAsync(SearchMode.Keyword, roleHint)`, which **already applies role-boost internally** (`SearchKeywordOnlyAsync`, `:215`) and re-sorts. If the canonical core ALSO applies role-boost, the keyword arm is boosted twice. Fix: the primary path must feed the core **raw (unboosted) arm rankings** and let the core apply legend + role exactly once. Concretely:
- Keyword arm: obtain a **raw** keyword ranking (ts_rank_cd order) that still carries `RoleTags` + content, WITHOUT the in-service role re-sort. **Chosen approach**: call `IKeywordSearchService` directly (it returns `KeywordSearchResult` with `RoleTags` + `Content`, in raw ts_rank_cd order — no role re-sort), instead of routing through `HybridSearchService.SearchAsync(SearchMode.Keyword)` which boosts. The plan verifies `IKeywordSearchService` is injectable into `SearchQueryHandler` and reconciles the `keywordMinScore=0.01` filter (currently applied by the Keyword-mode path at `SearchQueryHandler.cs:196-204`) so it is preserved. Map `KeywordSearchResult` → `Domain.Entities.SearchResult` carrying `RoleTags` (a new mapper, sibling to `ToDomainSearchResult`).
- Vector arm: already raw (cosine order); §5 threads its `RoleTags`.
- `SearchQueryHandler.PerformHybridSearchAsync` passes the classified `queryRoleHint` into `RrfFusionDomainService.FuseResults(..., queryRoleHint)`.

### 7. `StreamQaQueryHandler` role-hint parity

`StreamQaQueryHandler` (`:278-347`) must classify intent and set `QueryRoleHint` on its `SearchQuery` (mirroring `AskQuestionQueryHandler.cs:429-443`), so the stream path gets role-boost parity with non-stream. Small, isolated handler change with its own test.

### Data flow (primary path, after)

```
query → IntentClassifier → roleHint
      → [ vector arm: raw pgvector cosine ranking + RoleTags ]
      → [ keyword arm: raw ts_rank_cd ranking + RoleTags ]
      → HybridFusionCore.Fuse(vectorWeight 0.7, keywordWeight 0.3, rrfK 60, roleHint)   // weighted RRF + legend + role
      → cross-encoder reranker (unchanged)
      → answer
```

## Risk & validation

**This changes production chat ranking** (`/agents/qa` + `/stream`): unweighted → weighted 0.7/0.3, plus legend-demotion and two-arm role-boost. There is **no EN/IT retrieval non-regression suite** yet (that is SP3's deliverable), so real-data ranking quality cannot be fully gated here. Mitigation:

1. **Parity test (mandatory)**: prove `HybridSearchService.FuseSearchResults` produces identical output before/after the extraction — the Hybrid path is a pure refactor and must not drift. Table-drive representative inputs; assert order + `HybridScore`/`VectorScore`/`KeywordScore` equality.
2. **Core unit tests**: weighted RRF math (incl. A/B weights 0.8/0.2, 0.5/0.5, 0.3/0.7), legend-demotion (a ≥2-pointer legend chunk demotes below real content), role-boost (matching-role chunk rises; additive-on-top semantics preserved), stable-key fusion, empty-arm handling.
3. **Primary-path integration test**: an `AskQuestion`/`SearchQueryHandler`-level test proving legend-demotion + role-boost now reach the primary path (the signals that were previously absent), and a `StreamQa` test proving the role hint is now set.
4. **TM "setup per N giocatori" regression intent**: document the original #3243 repro as the qualitative check; note it can only be fully validated after SP3 re-index materializes headings/role_tags on the corpus.
5. **No new SQL / no migration** — role_tags are already fetched; heading is out of scope. `dotnet ef migrations has-pending-model-changes` must stay clean.

## File map

**Create**:
- `Domain/Services/VectorSearch/HybridFusionCore.cs` — the canonical fusion + `FusionCandidate`/`FusionOptions`/`FusedCandidate` + the moved signal helpers/constants.
- Tests: `HybridFusionCoreTests.cs` (core math + signals), a `FuseSearchResults` parity test, primary-path handler tests, `StreamQaQueryHandler` role-hint test.

**Modify**:
- `Services/HybridSearchService.cs` — `FuseSearchResults` → adapter; delete the moved helpers/constants.
- `Domain/Services/VectorSearch/RrfFusionDomainService.cs` — → adapter + `queryRoleHint` param.
- `Domain/Entities/SearchResult.cs` — add `RoleTags`.
- `Application/Queries/SearchQueryHandler.cs` — raw keyword arm via `IKeywordSearchService` (preserve `keywordMinScore`), thread `RoleTags` (vector arm) + `queryRoleHint` into `FuseResults`.
- `Application/Mappers/KnowledgeBaseMappers.cs` — add a `KeywordSearchResult` → `Domain.Entities.SearchResult` mapper carrying `RoleTags` (sibling to `ToDomainSearchResult`).
- `Application/Queries/StreamQaQueryHandler.cs` — classify intent + set `QueryRoleHint`.

## Testing

Backend xUnit + Moq + FluentAssertions. Core + parity + helper tests are pure unit (no infra). Primary-path signal-reach tests are handler-level (InMemory or mocked search services). `dotnet build` 0 warnings (`TreatWarningsAsErrors`). No Testcontainers required for the fusion itself (the arms are mocked); the existing hybrid integration tests must stay green.

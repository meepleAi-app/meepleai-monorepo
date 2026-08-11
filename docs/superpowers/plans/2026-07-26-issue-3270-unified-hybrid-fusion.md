# Unified Hybrid Fusion (#3270 · SP4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace MeepleAI's two divergent hybrid-fusion implementations with one canonical `HybridFusionCore`, so the user-facing chat path (`/agents/qa` + `/agents/qa/stream`) gets the same weighted-RRF + legend-demotion + role-boost ranking the admin playground already gets.

**Architecture:** A pure/static Domain fusion core (merge + score + order, I/O-type agnostic) plus a shared `FusionSignals` static (legend/role helpers + constants). The existing `HybridSearchService.FuseSearchResults` and `RrfFusionDomainService.FuseResults` become thin adapters that map their arms into the neutral `FusionCandidate`, call the core, and re-join by key for type-specific output. The primary chat path additionally carries the `{PdfDocumentId}_{ChunkIndex}` identity + `RoleTags` end-to-end and forwards its role hint.

**Tech Stack:** .NET 9, C# (nullable enabled, `TreatWarningsAsErrors`), xUnit + Moq + FluentAssertions. No new SQL, no EF migration.

## Global Constraints

- **Namespace ≠ folder.** New files under `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/` declare namespace `Api.BoundedContexts.KnowledgeBase.Domain.Services` (parent — matches siblings `RrfFusionDomainService.cs:4`, `CosineSimilarityCalculator.cs`, `VectorSearchDomainService.cs:4`).
- **`internal` visibility everywhere.** All new types/members are `internal` (record structs / static). `InternalsVisibleTo` is already granted to `Api.Tests` (`Api.csproj:47`) + `DynamicProxyGenAssembly2` (`:49`, Moq). Do NOT add new grants.
- **Cross-context type:** `GameBookRole` is `[Flags] public enum { None=0, Tutorial=1, RulesReference=2, Narrative=4, Encounter=8, Lore=16, Setup=32 }` in `Api.BoundedContexts.GameManagement.Domain.ValueObjects`. Every new/edited KnowledgeBase file referencing it needs `using Api.BoundedContexts.GameManagement.Domain.ValueObjects;`.
- **Weights stay per-call inputs.** `HybridSearchEngine` runs A/B weights (0.8/0.2, 0.5/0.5, 0.3/0.7). Never hardcode 0.7/0.3 inside the core call for the `HybridSearchService` adapter — thread the per-call weights through.
- **#2712 (mandatory):** on the primary path, fused `SearchResult.RelevanceScore` = the carried **cosine** (vector arm's source score, else keyword source score) — NEVER the hybrid/RRF score. The hybrid score drives **order only**. Regression: feeding RRF into confidence degenerates it ~0.53 and hides grounded answers behind the "Non sono certo" card.
- **Guid/string split is intentional.** `HybridSearchResult.PdfDocumentId` stays **string** end-to-end in `HybridSearchService`. `Domain.Entities.SearchResult.PdfDocumentId` is **Guid** (`Guid.Parse(...)` at the keyword-arm mapper; already-Guid on the vector arm). Do not unify these.
- **Build:** `dotnet build` must stay 0-warning. Run from `apps/api/src/Api`.
- **Test host hygiene:** before `dotnet test`, kill stray hosts (`tasklist | grep testhost` → `taskkill //PID <PID> //F`); use `$"{val*100:0}%"` style culture-independent formatting if any new formatting appears (it should not here).

---

## Verified current state (grounding — trust these over the spec's line numbers)

- `RrfFusionDomainService.cs` — `internal class`; `FuseResults(List<SearchResult> vectorResults, List<SearchResult> keywordResults, int rrfK = DefaultRrfK)` at **:22-92**; `DefaultRrfK = 60` at :12; UNWEIGHTED `1.0/(rrfK+rank)` both arms; `GetChunkKey` at **:100-104** = `$"{VectorDocumentId}:{PageNumber}:{TextContent.GetHashCode(Ordinal)}"` (called 4× at :38,:49,:59,:68); #2712 `relevanceScore: item.Result.RelevanceScore` at :85; sole production caller `SearchQueryHandler.cs:212` (2 positional args).
- `HybridSearchService.cs` — `DefaultRrfK = 60` at **:29**, `RoleMatchBoost = 0.15f` at **:35** (spec transposed these); `FuseSearchResults` (private) at **:394-527**, final score `((vectorRrfScore + keywordRrfScore) * (1f - legendFactor)) + roleBoost` at :489, RoleTags OR-union :469, sole caller `SearchHybridAsync` at :305; `ComputeRoleMatchBoost` at **:536-544**; `CrossReferencePointer` regex at **:546-551**; `ComputeLegendPenaltyFactor` at **:560-580**; 4 role-boost call sites (`:134` semantic, `:206` keyword, `:470` fusion).
- `SearchResult.cs` — `internal sealed class SearchResult : Entity<Guid>` at **:10-77** (NOT a record); `RelevanceScore` is a `Confidence` value object; public ctor at **:31-55** = `(Guid id, Guid vectorDocumentId, string textContent, int pageNumber, Confidence relevanceScore, int rank, string? searchMethod = null)`. **21 construction sites** (4 prod: `StructuredRagFusionService.cs:127,:149`, `RrfFusionDomainService.cs:76`, `VectorSearchDomainService.cs:54`; 17 test — enumerated in Task 4).
- `HybridSearchResult` (`IHybridSearchService.cs:68`) already carries `ChunkId, Content, PdfDocumentId (string), GameId, ChunkIndex, PageNumber, HybridScore, VectorScore, KeywordScore, VectorRank, KeywordRank, MatchedTerms, Mode, RoleTags`.
- `SearchKeywordOnlyAsync` (`HybridSearchService.cs:176-235`) applies `phraseSearch = query.Contains('"')`, `minScore`, #2051 documentIds filter, sets `KeywordScore = r.RelevanceScore` (raw ts_rank_cd), `HybridScore = r.RelevanceScore + roleBoost`, and **re-sorts only if `queryRoleHint != None`**.
- `SearchQueryHandler.PerformVectorSearchAsync` (**:133-175**) builds the vector-arm `SearchResult` at **:159-167** from `scored.Embedding` (which — via `SearchByVectorWithScoresAsync` → `PgVectorStoreAdapter.SearchWithScoresAsync:127` — JOINs `vector_documents` and populates real `PdfDocumentId` [col 7], `ChunkIndex` [col 4], `RoleTags` [col 6]). `PerformHybridSearchAsync` (**:177-213**) sources the keyword arm via `_hybridSearchService.SearchAsync(SearchMode.Keyword, …, queryRoleHint)` at :196-204, maps via `kr.ToDomainSearchResult(index+1)` at :207-209, calls `_rrfFusionService.FuseResults(vectorResults, keywordResults)` at :212.
- `ToDomainSearchResult` (`Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs:64-83`) maps `HybridSearchResult → SearchResult`; currently `score = (double)result.HybridScore`, drops PdfDocumentId(Guid)/ChunkIndex/RoleTags. Sole caller: `SearchQueryHandler.cs:208` (VERIFY in Task 6 Step 0).
- `StreamQaQueryHandler` (`:278-347`) builds `SearchQuery` at :284-292 with hardcoded `SearchMode:"hybrid"`, omits `QueryRoleHint`; ctor (**:31-80**) does **NOT** inject `IIntentClassifierService`. Mirror pattern: `AskQuestionQueryHandler` (`_intentClassifier` field :57, ctor :84, null-guard :105; classify + set `QueryRoleHint` :429-443).
- `SearchQuery` (`SearchQuery.cs`) — `QueryRoleHint` is the LAST positional record param (`= GameBookRole.None`); pass BY NAME.

**Design decision — keyword arm sourcing (spec §6 raw-arm, chosen by user directive 2026-07-26):** The primary path sources a *raw* keyword arm directly from `IKeywordSearchService.SearchAsync` (returns `List<KeywordSearchResult>` in raw ts_rank_cd order, un-boosted) so `HybridFusionCore` applies legend + role-boost exactly once (no double-boost). Because `IKeywordSearchService.SearchAsync` has **no** `documentIds` param and **no** role hint, the handler must reproduce what `HybridSearchService.SearchKeywordOnlyAsync` applied on top: the `#2051` documentIds post-filter (`r => documentIds.Any(id => string.Equals(id.ToString(), r.PdfDocumentId, Ordinal))`), the `phraseSearch = query.Contains('"')` derivation, and `minScore = 0.01`. A new `ToDomainSearchResult(this KeywordSearchResult, int)` mapper carries `PdfDocumentId`/`ChunkIndex`/`RoleTags` into `SearchResult`. `IKeywordSearchService` **replaces** `IHybridSearchService` in `SearchQueryHandler`'s ctor (the hybrid service was used *only* for this keyword sub-call at `:196`, so it becomes unused after the swap — removing it avoids a CS0414/dead-injection). Both are `AddScoped`, so the swap is lifetime-safe (consumer `SearchQueryHandler` is `AddScoped`, `KnowledgeBaseServiceExtensions.cs:463`).

---

## Task graph

```
Task 1 (FusionSignals) ──▶ Task 2 (HybridFusionCore) ──▶ Task 3 (HybridSearchService adapter + parity)
Task 4 (SearchResult +3 fields) ──▶ Task 5 (RrfFusionDomainService adapter + #2712) ──▶ Task 6 (SearchQueryHandler wire)
Task 7 (StreamQa role hint) ── standalone
```
Tasks 4 and 7 can start in parallel with Task 1. Task 5 needs Tasks 2 + 4. Task 6 needs Tasks 4 + 5.

---

### Task 1: `FusionSignals` — extract shared legend/role helpers + constants

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/FusionSignals.cs`
- Modify: `apps/api/src/Api/Services/HybridSearchService.cs` (remove moved members; repoint 4 call sites + local refs)
- Modify: `apps/api/tests/Api.Tests/Services/HybridSearchServiceRoleBoostTests.cs` (repoint 22 qualified refs)
- Test: `apps/api/tests/Api.Tests/Domain/Services/VectorSearch/FusionSignalsTests.cs`

**Interfaces:**
- Produces: `FusionSignals.ComputeRoleMatchBoost(GameBookRole queryRoleHint, GameBookRole chunkRoleTags) → float`; `FusionSignals.ComputeLegendPenaltyFactor(string? content) → float`; `internal const float FusionSignals.RoleMatchBoost = 0.15f`; `internal const int FusionSignals.DefaultRrfK = 60`.

- [ ] **Step 1: Write the failing test** — `FusionSignalsTests.cs`

```csharp
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Domain.Services.VectorSearch;

public class FusionSignalsTests
{
    [Fact]
    public void ComputeRoleMatchBoost_WhenRolesIntersect_ReturnsBoost()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.Setup, GameBookRole.Setup | GameBookRole.RulesReference)
            .Should().Be(0.15f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_WhenHintIsNone_ReturnsZero()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.None, GameBookRole.Setup).Should().Be(0f);
    }

    [Fact]
    public void ComputeRoleMatchBoost_WhenNoIntersection_ReturnsZero()
    {
        FusionSignals.ComputeRoleMatchBoost(GameBookRole.Setup, GameBookRole.Narrative).Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_WithFewerThanTwoPointers_ReturnsZero()
    {
        FusionSignals.ComputeLegendPenaltyFactor("See page 12 for details.").Should().Be(0f);
    }

    [Fact]
    public void ComputeLegendPenaltyFactor_WithDenseCrossReferences_ReturnsPenaltyInRange()
    {
        var legendy = "See p. 3. See p. 5. See p. 7. See p. 9.";
        var factor = FusionSignals.ComputeLegendPenaltyFactor(legendy);
        factor.Should().BeInRange(0f, 0.5f);
        factor.Should().BeGreaterThan(0f);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet build` then from repo root `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~FusionSignalsTests"`
Expected: FAIL — `The type or namespace name 'FusionSignals' does not exist`.

- [ ] **Step 3: Create `FusionSignals.cs`** — move the exact bodies from `HybridSearchService.cs` (`ComputeRoleMatchBoost` :536-544, `CrossReferencePointer` regex :546-551, `ComputeLegendPenaltyFactor` :560-580, `RoleMatchBoost` :35, `DefaultRrfK` :29). Copy the current implementations verbatim; do not re-derive the math. Template:

```csharp
using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Pure, stateless retrieval-ranking signals shared by every hybrid-fusion path
/// (issue #3270). Moved out of <c>HybridSearchService</c> so the primary chat path
/// and the admin playground apply identical legend-demotion + role-boost.
/// </summary>
internal static class FusionSignals
{
    /// <summary>Additive role-match boost (issue #1391 Phase D6).</summary>
    internal const float RoleMatchBoost = 0.15f;

    /// <summary>Default reciprocal-rank-fusion constant.</summary>
    internal const int DefaultRrfK = 60;

    // COPY the exact regex from HybridSearchService.cs:546-551.
    private static readonly Regex CrossReferencePointer = /* verbatim from source */;

    /// <summary>
    /// Additive boost when the query's role hint overlaps a chunk's role tags.
    /// Verbatim move of HybridSearchService.ComputeRoleMatchBoost (:536-544).
    /// </summary>
    internal static float ComputeRoleMatchBoost(GameBookRole queryRoleHint, GameBookRole chunkRoleTags)
    {
        // COPY body verbatim from HybridSearchService.cs:536-544.
    }

    /// <summary>
    /// Legend-demotion factor in [0, 0.5] (verbatim move of :560-580).
    /// </summary>
    internal static float ComputeLegendPenaltyFactor(string? content)
    {
        // COPY body verbatim from HybridSearchService.cs:560-580.
    }
}
```

- [ ] **Step 4: Repoint `HybridSearchService.cs`** — delete the 5 moved members; add `using` if needed (same assembly, so only a `using Api.BoundedContexts.KnowledgeBase.Domain.Services;` if the file isn't already in scope — it's in `Api.Services`, so ADD the using). Repoint every in-file reference:
  - `ComputeRoleMatchBoost(...)` call sites at `:134` (semantic), `:206` (keyword), `:470` (fusion) → `FusionSignals.ComputeRoleMatchBoost(...)`.
  - `ComputeLegendPenaltyFactor(...)` call site at `:488` → `FusionSignals.ComputeLegendPenaltyFactor(...)`.
  - `RoleMatchBoost` const references (property-style) at `:39,:97,:116,:186,:309,:402,:474` → `FusionSignals.RoleMatchBoost`.
  - `DefaultRrfK` references → `FusionSignals.DefaultRrfK` (keep `HybridSearchConfiguration.RrfConstant = 60` at :606 as-is; it's a separate config default).
  - Leave `SearchSemanticOnlyAsync`/`SearchKeywordOnlyAsync`/`SearchHybridAsync` behavior otherwise untouched.

- [ ] **Step 5: Repoint `HybridSearchServiceRoleBoostTests.cs`** — 22 qualified edits (spec undercounted at 14). Replace `HybridSearchService.` → `FusionSignals.` on these qualified references ONLY:
  - Method calls: `.ComputeRoleMatchBoost` / `.ComputeLegendPenaltyFactor` at lines 36,50,65,80,94,108,109,130,141,154,165,167,184,206.
  - `.RoleMatchBoost` const at lines 39,97,116,186,309,402,474.
  - XML `<see cref="HybridSearchService.RoleMatchBoost"/>` at line 18 → `FusionSignals.RoleMatchBoost`.
  - Do NOT touch test-method NAME identifiers (29,43,57,72,87,101,124,134,148,161,198) or prose comments (215,306,371,374). Add `using Api.BoundedContexts.KnowledgeBase.Domain.Services;`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~FusionSignalsTests|FullyQualifiedName~HybridSearchServiceRoleBoost"`
Expected: PASS (build 0 warnings; FusionSignals tests green; all repointed RoleBoost tests green — behavior unchanged).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/FusionSignals.cs apps/api/src/Api/Services/HybridSearchService.cs apps/api/tests/Api.Tests/Services/HybridSearchServiceRoleBoostTests.cs apps/api/tests/Api.Tests/Domain/Services/VectorSearch/FusionSignalsTests.cs
git commit -m "refactor(rag): extract FusionSignals from HybridSearchService (#3270)"
```

---

### Task 2: `HybridFusionCore` — the single canonical fusion

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/HybridFusionCore.cs`
- Test: `apps/api/tests/Api.Tests/Domain/Services/VectorSearch/HybridFusionCoreTests.cs`

**Interfaces:**
- Consumes: `FusionSignals.*` (Task 1).
- Produces: `FusionCandidate(string Key, string Content, GameBookRole RoleTags, int Rank, float SourceScore)`; `FusionOptions(float VectorWeight=0.7f, float KeywordWeight=0.3f, int RrfK=60, GameBookRole QueryRoleHint=None)`; `FusedCandidate(string Key, string Content, GameBookRole RoleTags, float HybridScore, float? VectorScore, float? KeywordScore, int? VectorRank, int? KeywordRank, int Rank)`; `HybridFusionCore.Fuse(IReadOnlyList<FusionCandidate> vectorArm, IReadOnlyList<FusionCandidate> keywordArm, FusionOptions options) → IReadOnlyList<FusedCandidate>`.

- [ ] **Step 1: Write the failing tests** — `HybridFusionCoreTests.cs`

```csharp
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Domain.Services.VectorSearch;

public class HybridFusionCoreTests
{
    private static FusionCandidate V(string key, int rank, float score, string content = "content", GameBookRole roles = GameBookRole.None)
        => new(key, content, roles, rank, score);

    [Fact]
    public void Fuse_BothArms_WeightedRrf_OrdersByHybridScore()
    {
        var vec = new[] { V("a", 1, 0.9f), V("b", 2, 0.8f) };
        var kw = new[] { V("b", 1, 0.3f), V("a", 2, 0.2f) };
        var opts = new FusionOptions(0.7f, 0.3f, 60, GameBookRole.None);

        var fused = HybridFusionCore.Fuse(vec, kw, opts);

        fused.Should().HaveCount(2);
        // a: 0.7/61 + 0.3/62 ≈ 0.01631 ; b: 0.7/62 + 0.3/61 ≈ 0.01621 → a first
        fused[0].Key.Should().Be("a");
        fused[0].Rank.Should().Be(1);
        fused[0].VectorScore.Should().Be(0.9f);
        fused[0].KeywordScore.Should().Be(0.2f);
        fused[0].VectorRank.Should().Be(1);
        fused[0].KeywordRank.Should().Be(2);
    }

    [Fact]
    public void Fuse_PrefersVectorArmContent_WhenChunkInBothArms()
    {
        var vec = new[] { V("a", 1, 0.9f, content: "VECTOR") };
        var kw = new[] { V("a", 1, 0.3f, content: "KEYWORD") };
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused.Single().Content.Should().Be("VECTOR");
    }

    [Fact]
    public void Fuse_RoleTags_AreOrUnionedAcrossArms()
    {
        var vec = new[] { V("a", 1, 0.9f, roles: GameBookRole.Setup) };
        var kw = new[] { V("a", 1, 0.3f, roles: GameBookRole.RulesReference) };
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused.Single().RoleTags.Should().Be(GameBookRole.Setup | GameBookRole.RulesReference);
    }

    [Fact]
    public void Fuse_LegendDenseChunk_IsDemotedBelowRealContent()
    {
        var realC = "The setup phase: place 3 tiles per player and shuffle the deck.";
        var legend = "See p. 3. See p. 5. See p. 7. See p. 9.";
        // Give legend the BETTER raw ranks so only legend-demotion can reorder them.
        var vec = new[] { V("legend", 1, 0.95f, content: legend), V("real", 2, 0.90f, content: realC) };
        var kw = new[] { V("legend", 1, 0.30f, content: legend), V("real", 2, 0.20f, content: realC) };
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused[0].Key.Should().Be("real");
    }

    [Fact]
    public void Fuse_RoleBoost_IsAdditiveOnTop_AndLiftsMatchingRole()
    {
        var vec = new[] { V("plain", 1, 0.9f, roles: GameBookRole.None), V("setup", 2, 0.8f, roles: GameBookRole.Setup) };
        var kw = System.Array.Empty<FusionCandidate>();
        var opts = new FusionOptions(0.7f, 0.3f, 60, GameBookRole.Setup);
        var fused = HybridFusionCore.Fuse(vec, kw, opts);
        // 'setup' gets +0.15 additive → overtakes 'plain'
        fused[0].Key.Should().Be("setup");
    }

    [Fact]
    public void Fuse_DuplicateKeyWithinArm_KeepsBestRank_DoesNotThrow()
    {
        var vec = new[] { V("a", 3, 0.5f), V("a", 1, 0.9f) };
        var kw = System.Array.Empty<FusionCandidate>();
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused.Single().VectorRank.Should().Be(1); // best (lowest) rank wins
    }

    [Fact]
    public void Fuse_TieBreak_IsDeterministicByKeyOrdinal()
    {
        // Identical scores → deterministic order by Key ordinal.
        var vec = new[] { V("b", 1, 0.5f), V("a", 1, 0.5f) };
        var kw = System.Array.Empty<FusionCandidate>();
        var fused = HybridFusionCore.Fuse(vec, kw, new FusionOptions());
        fused[0].Key.Should().Be("a");
        fused[1].Key.Should().Be("b");
    }

    [Fact]
    public void Fuse_EmptyArms_ReturnsEmpty()
    {
        HybridFusionCore.Fuse(System.Array.Empty<FusionCandidate>(), System.Array.Empty<FusionCandidate>(), new FusionOptions())
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.8f, 0.2f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.3f, 0.7f)]
    public void Fuse_HonorsPerCallWeights(float vw, float kw)
    {
        var vec = new[] { V("a", 1, 0.9f) };
        var key = new[] { V("b", 1, 0.3f) };
        var fused = HybridFusionCore.Fuse(vec, key, new FusionOptions(vw, kw, 60, GameBookRole.None));
        // Higher-weighted arm's sole item ranks first.
        fused[0].Key.Should().Be(vw >= kw ? "a" : "b");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~HybridFusionCoreTests"`
Expected: FAIL — `HybridFusionCore` / `FusionCandidate` do not exist.

- [ ] **Step 3: Create `HybridFusionCore.cs`** — full implementation:

```csharp
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>Neutral per-arm candidate — only what scoring needs. Adapters build the Key.</summary>
internal readonly record struct FusionCandidate(
    string Key,
    string Content,
    GameBookRole RoleTags,
    int Rank,          // 1-based within THIS arm (cosine desc / ts_rank_cd desc)
    float SourceScore);

internal sealed record FusionOptions(
    float VectorWeight = 0.7f,
    float KeywordWeight = 0.3f,
    int RrfK = FusionSignals.DefaultRrfK,
    GameBookRole QueryRoleHint = GameBookRole.None);

/// <summary>Scoring result keyed by Key; adapters re-join their arm items for I/O-specific fields.</summary>
internal readonly record struct FusedCandidate(
    string Key,
    string Content,
    GameBookRole RoleTags,
    float HybridScore,
    float? VectorScore,
    float? KeywordScore,
    int? VectorRank,
    int? KeywordRank,
    int Rank);         // 1-based rank in fused order

/// <summary>
/// The single canonical hybrid fusion (#3270): weighted RRF + legend-demotion + role-boost,
/// I/O-type agnostic. Pure — no logging, no injected state.
/// </summary>
internal static class HybridFusionCore
{
    internal static IReadOnlyList<FusedCandidate> Fuse(
        IReadOnlyList<FusionCandidate> vectorArm,
        IReadOnlyList<FusionCandidate> keywordArm,
        FusionOptions options)
    {
        // Dedup within each arm: keep the best (lowest) rank per Key. Total (never throws).
        var vec = BestPerKey(vectorArm);
        var kw = BestPerKey(keywordArm);

        var scored = new List<FusedCandidate>();
        foreach (var key in vec.Keys.Union(kw.Keys))
        {
            var hasV = vec.TryGetValue(key, out var v);
            var hasK = kw.TryGetValue(key, out var k);

            float vectorRrf = hasV ? options.VectorWeight / (options.RrfK + v.Rank) : 0f;
            float keywordRrf = hasK ? options.KeywordWeight / (options.RrfK + k.Rank) : 0f;
            float rrfSum = vectorRrf + keywordRrf;

            // Prefer vector arm content (load-bearing: legend factor is computed from it).
            string content = hasV ? v.Content : k.Content;
            GameBookRole roleTags = (hasV ? v.RoleTags : GameBookRole.None) | (hasK ? k.RoleTags : GameBookRole.None);

            float legendFactor = FusionSignals.ComputeLegendPenaltyFactor(content);
            float roleBoost = FusionSignals.ComputeRoleMatchBoost(options.QueryRoleHint, roleTags);
            float hybridScore = (rrfSum * (1f - legendFactor)) + roleBoost;

            scored.Add(new FusedCandidate(
                Key: key,
                Content: content,
                RoleTags: roleTags,
                HybridScore: hybridScore,
                VectorScore: hasV ? v.SourceScore : (float?)null,
                KeywordScore: hasK ? k.SourceScore : (float?)null,
                VectorRank: hasV ? v.Rank : (int?)null,
                KeywordRank: hasK ? k.Rank : (int?)null,
                Rank: 0)); // assigned after sort
        }

        // Order by hybridScore desc; deterministic tie-break by Key ordinal.
        var ordered = scored
            .OrderByDescending(c => c.HybridScore)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select((c, i) => c with { Rank = i + 1 })
            .ToList();

        return ordered;
    }

    private static Dictionary<string, FusionCandidate> BestPerKey(IReadOnlyList<FusionCandidate> arm)
    {
        var best = new Dictionary<string, FusionCandidate>(StringComparer.Ordinal);
        foreach (var c in arm)
        {
            if (!best.TryGetValue(c.Key, out var existing) || c.Rank < existing.Rank)
            {
                best[c.Key] = c;
            }
        }
        return best;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~HybridFusionCoreTests"`
Expected: PASS (all 8 facts + 3 theory cases; build 0 warnings).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/HybridFusionCore.cs apps/api/tests/Api.Tests/Domain/Services/VectorSearch/HybridFusionCoreTests.cs
git commit -m "feat(rag): add canonical HybridFusionCore (weighted RRF + legend + role) (#3270)"
```

---

### Task 3: `HybridSearchService.FuseSearchResults` → thin adapter (behavior-preserving)

**Files:**
- Modify: `apps/api/src/Api/Services/HybridSearchService.cs` (`FuseSearchResults` :394-527, sole caller `SearchHybridAsync` :305)
- Test: `apps/api/tests/Api.Tests/Services/HybridSearchServiceFusionParityTests.cs`

**Interfaces:**
- Consumes: `HybridFusionCore.Fuse`, `FusionCandidate`, `FusionOptions`, `FusedCandidate` (Task 2).
- Produces: no signature change — `FuseSearchResults` stays private; `SearchAsync` public signature unchanged (8 callers unaffected: `RagService.cs:683`, `GenerateToolkitFromKbHandler.cs:129`, `AskArbiterCommandHandler.cs:91`, `PlaygroundChatCommandHandler.cs:248`, `HybridSearchEngine.cs:80`, `MultiGameHybridSearchService.cs:161`, `ResilientRetrievalService.cs:129`, `SearchQueryHandler.cs:196`).

- [ ] **Step 1: Write the failing parity test** — captures observable output BEFORE the refactor. Build a real `HybridSearchService` (its collaborators mocked), drive `SearchAsync(SearchMode.Hybrid)` with a fixed both-arm dataset, and pin: post-sort order + `HybridScore` + `VectorScore` + `KeywordScore` + `MatchedTerms` + `GameId` + `PdfDocumentId` (string form) + `PageNumber` (null→0). Cases: both-arm, vector-only, keyword-only, duplicate-key, keyword-only-null-page, tie-break-by-Key.

```csharp
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.Services;

public class HybridSearchServiceFusionParityTests
{
    // Arrange a HybridSearchService with mocked IKeywordSearchService + vector store so
    // SearchAsync(SearchMode.Hybrid) exercises FuseSearchResults over a KNOWN dataset.
    // (Follow the arrange pattern already used in HybridSearchServiceRoleBoostTests.cs.)

    [Fact]
    public async Task Hybrid_BothArms_PreservesOrderScoresAndOutputFields()
    {
        // ... arrange fixed vector arm [(pdfA,chunk0,cos0.9),(pdfB,chunk1,cos0.8)]
        //     + keyword arm [(pdfB,chunk1,ts0.3,terms=["x"]),(pdfA,chunk0,ts0.2,terms=["y"])]
        // var svc = BuildService(vectorArm, keywordArm);
        // var results = await svc.SearchAsync("q", gameId, SearchMode.Hybrid, 10);

        // Pin the pre-refactor expected values captured from a first green run:
        // results.Select(r => r.PdfDocumentId).Should().ContainInOrder("pdfA", "pdfB");
        // results[0].HybridScore.Should().BeApproximately(<captured>, 1e-6f);
        // results[0].MatchedTerms.Should().BeEquivalentTo(new[] { "y" });
        // results[0].PageNumber.Should().Be(<captured or 0>);
        // ... assert VectorScore/KeywordScore/GameId per item.
    }

    // Repeat for: vector-only, keyword-only, duplicate-key, keyword-only-null-page (→0), tie-break-by-Key.
}
```

- [ ] **Step 2: Run the parity test against the CURRENT implementation to capture golden values**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~HybridSearchServiceFusionParityTests"`
Expected: The test currently drives the OLD `FuseSearchResults`. Fill the `<captured>` values from this run so the test is GREEN against today's behavior. This freezes the contract before refactoring. (If arrange is nontrivial, first `dotnet test ... -v n` to inspect actual values, then hardcode them.)

- [ ] **Step 3: Rewrite `FuseSearchResults` as an adapter** — map arms → `FusionCandidate`, call the core with the per-call weights, re-join by Key. Key = `$"{PdfId}_{ChunkIndex}"` (matches the #3262 identity already used here). Preserve `MatchedTerms` (keyword arm), `GameId` (keyword arm else query `gameId`), `PdfDocumentId` (string, as-is), `ChunkIndex`/`PageNumber` (coalesce null→0), `Mode = SearchMode.Hybrid`. Skeleton (fill from the existing `:394-527` field mapping):

```csharp
private List<HybridSearchResult> FuseSearchResults(
    List<SearchResultItem> vectorItems,
    List<SearchResultItem> keywordItems,
    Guid gameId,
    float vectorWeight,
    float keywordWeight,
    int rrfK)
{
    static string KeyOf(SearchResultItem it) => $"{it.PdfDocumentId}_{it.ChunkIndex}";

    var vectorArm = vectorItems.Select((it, i) => new FusionCandidate(
        KeyOf(it), it.Text, it.RoleTags, i + 1, it.Score)).ToList();
    var keywordArm = keywordItems.Select((it, i) => new FusionCandidate(
        KeyOf(it), it.Text, it.RoleTags, i + 1, it.Score)).ToList();

    var fused = HybridFusionCore.Fuse(vectorArm, keywordArm,
        new FusionOptions(vectorWeight, keywordWeight, rrfK, GameBookRole.None));

    // Role-boost on THIS path already rides in via the keyword sub-call's re-rank, so the
    // core is called with QueryRoleHint.None to preserve today's exact behavior (parity).
    var vByKey = vectorItems.ToLookup(KeyOf);   // total; may have dup keys — take First()
    var kByKey = keywordItems.ToLookup(KeyOf);

    return fused.Select(f =>
    {
        var v = vByKey[f.Key].FirstOrDefault();
        var k = kByKey[f.Key].FirstOrDefault();
        var src = k ?? v; // keyword carries MatchedTerms/GameId/page
        return new HybridSearchResult
        {
            ChunkId = (src?.ChunkId) ?? f.Key,
            Content = f.Content,
            PdfDocumentId = src?.PdfDocumentId ?? string.Empty,
            GameId = k?.GameId ?? gameId,
            ChunkIndex = src?.ChunkIndex ?? 0,
            PageNumber = src?.PageNumber ?? 0,
            HybridScore = f.HybridScore,
            VectorScore = f.VectorScore,
            KeywordScore = f.KeywordScore,
            VectorRank = f.VectorRank,
            KeywordRank = f.KeywordRank,
            MatchedTerms = k?.MatchedTerms ?? new List<string>(),
            Mode = SearchMode.Hybrid,
            RoleTags = f.RoleTags,
        };
    }).ToList();
}
```

> **Parity note:** the OLD formula applied role-boost inside fusion (`:470`). Because the primary chat path is the behavioral target (Task 5) and the `HybridSearchService` path is meant to be **observably identical**, call the core here with `QueryRoleHint.None`. If the pre-refactor arms were role-boosted upstream (they are, via `SearchKeywordOnlyAsync`), the parity test in Step 2 will confirm identical output; if it does not, thread the real hint into `FusionOptions` here and re-capture the golden values (documenting the one-time delta).

- [ ] **Step 4: Update the sole caller `SearchHybridAsync:305`** — pass the per-call `vectorWeight`, `keywordWeight`, and `_config.RrfConstant ?? FusionSignals.DefaultRrfK` into the new `FuseSearchResults` signature. Verify no other call site exists (`grep -n "FuseSearchResults" HybridSearchService.cs` → only :305 + the definition).

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~HybridSearchServiceFusionParityTests|FullyQualifiedName~HybridSearchServiceRoleBoost|FullyQualifiedName~HybridSearchEngine|FullyQualifiedName~MultiGameHybridSearch"`
Expected: PASS — parity identical; A/B-weight engine tests + multi-game tie-break unaffected.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Services/HybridSearchService.cs apps/api/tests/Api.Tests/Services/HybridSearchServiceFusionParityTests.cs
git commit -m "refactor(rag): HybridSearchService.FuseSearchResults delegates to HybridFusionCore (#3270)"
```

---

### Task 4: `SearchResult` gains `PdfDocumentId` + `ChunkIndex` + `RoleTags`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/SearchResult.cs:31-55`
- Test: `apps/api/tests/Api.Tests/.../SearchResultTests.cs` (add a construction+accessor test)

**Interfaces:**
- Produces: `SearchResult` ctor gains 3 trailing DEFAULTED params `Guid pdfDocumentId = default, int chunkIndex = 0, GameBookRole roleTags = GameBookRole.None` (AFTER `searchMethod`), + read-only props `PdfDocumentId`, `ChunkIndex`, `RoleTags`. All 21 existing construction sites keep compiling (they stop at/before `searchMethod`).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void SearchResult_CarriesFusionIdentity_WhenProvided()
{
    var pdf = Guid.NewGuid();
    var r = new Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult(
        id: Guid.NewGuid(),
        vectorDocumentId: Guid.NewGuid(),
        textContent: "text",
        pageNumber: 1,
        relevanceScore: new Confidence(0.9),
        rank: 1,
        searchMethod: "vector",
        pdfDocumentId: pdf,
        chunkIndex: 7,
        roleTags: GameBookRole.Setup);

    r.PdfDocumentId.Should().Be(pdf);
    r.ChunkIndex.Should().Be(7);
    r.RoleTags.Should().Be(GameBookRole.Setup);
}

[Fact]
public void SearchResult_FusionIdentity_DefaultsAreEmpty()
{
    var r = new Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult(
        Guid.NewGuid(), Guid.NewGuid(), "text", 1, new Confidence(0.5), 1);
    r.PdfDocumentId.Should().Be(Guid.Empty);
    r.ChunkIndex.Should().Be(0);
    r.RoleTags.Should().Be(GameBookRole.None);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SearchResultTests"`
Expected: FAIL — ctor has no `pdfDocumentId`/`chunkIndex`/`roleTags` params; no such properties.

- [ ] **Step 3: Add the 3 properties + defaulted ctor params** — edit `SearchResult.cs`:

```csharp
// add using at top:
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

// add 3 read-only properties beside the existing ones (private setters):
public Guid PdfDocumentId { get; private set; }
public int ChunkIndex { get; private set; }
public GameBookRole RoleTags { get; private set; }

// extend the public ctor signature (append AFTER searchMethod):
public SearchResult(
    Guid id,
    Guid vectorDocumentId,
    string textContent,
    int pageNumber,
    Confidence relevanceScore,
    int rank,
    string? searchMethod = null,
    Guid pdfDocumentId = default,
    int chunkIndex = 0,
    GameBookRole roleTags = GameBookRole.None) : base(id)
{
    // ... existing validation unchanged ...
    // at the end of the ctor body, assign:
    PdfDocumentId = pdfDocumentId;
    ChunkIndex = chunkIndex;
    RoleTags = roleTags;
}
```

> Do NOT add validation for the new params (ChunkIndex 0 is legal; PdfDocumentId.Empty is legal on non-hybrid paths). Keep the private EF ctor (`:23-26`) untouched.

- [ ] **Step 4: Run tests + full compile to confirm all 21 sites still build**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SearchResultTests"`
Expected: build 0 warnings (all 4 prod sites `StructuredRagFusionService.cs:127,:149`, `RrfFusionDomainService.cs:76`, `VectorSearchDomainService.cs:54` + 17 test sites compile untouched); new tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/SearchResult.cs apps/api/tests/Api.Tests
git commit -m "feat(rag): SearchResult carries PdfDocumentId+ChunkIndex+RoleTags for fusion identity (#3270)"
```

---

### Task 5: `RrfFusionDomainService.FuseResults` → adapter (primary-path behavioral change + #2712)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/RrfFusionDomainService.cs:22-104`
- Modify (Moq): `AskQuestionQueryHandlerSecurityTests.cs:105,:552`, `StreamQaQueryHandlerTests.cs:648,:753,:1045`, `AskQuestionQueryHandlerPhase2Tests.cs:81,:605`
- Test: `apps/api/tests/Api.Tests/.../RrfFusionDomainServiceTests.cs` (extend)

**Interfaces:**
- Consumes: `HybridFusionCore.Fuse` (Task 2); `SearchResult.PdfDocumentId/ChunkIndex/RoleTags` (Task 4).
- Produces: `FuseResults(List<SearchResult> vectorResults, List<SearchResult> keywordResults, int rrfK = FusionSignals.DefaultRrfK, GameBookRole queryRoleHint = GameBookRole.None) → List<SearchResult>`. New trailing `queryRoleHint` param (defaulted → existing callers/tests keep compiling). Still returns fused-order `SearchResult`s with `searchMethod = "hybrid"`, fresh Guid ids, and **`RelevanceScore` = cosine (#2712)**.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void FuseResults_BothArms_KeepsCosineInRelevanceScore_NotHybridScore()
{
    var pdf = Guid.NewGuid();
    var vector = new List<SearchResult> {
        new(Guid.NewGuid(), Guid.NewGuid(), "content", 1, new Confidence(0.91), 1, "vector",
            pdfDocumentId: pdf, chunkIndex: 0, roleTags: GameBookRole.None) };
    var keyword = new List<SearchResult> {
        new(Guid.NewGuid(), Guid.NewGuid(), "content", 1, new Confidence(0.20), 1, "keyword",
            pdfDocumentId: pdf, chunkIndex: 0, roleTags: GameBookRole.None) };

    var fused = new RrfFusionDomainService().FuseResults(vector, keyword);

    fused.Should().HaveCount(1);
    // #2712: RelevanceScore is the carried cosine (0.91), NOT an RRF/hybrid value.
    fused[0].RelevanceScore.Value.Should().BeApproximately(0.91, 1e-6);
    fused[0].SearchMethod.Should().Be("hybrid");
}

[Fact]
public void FuseResults_RoleHint_ReordersByRoleBoost_OrderOnly()
{
    var setupPdf = Guid.NewGuid(); var plainPdf = Guid.NewGuid();
    var vector = new List<SearchResult> {
        new(Guid.NewGuid(), Guid.NewGuid(), "plain", 1, new Confidence(0.90), 1, "vector",
            pdfDocumentId: plainPdf, chunkIndex: 0, roleTags: GameBookRole.None),
        new(Guid.NewGuid(), Guid.NewGuid(), "setup", 1, new Confidence(0.80), 2, "vector",
            pdfDocumentId: setupPdf, chunkIndex: 0, roleTags: GameBookRole.Setup) };
    var keyword = new List<SearchResult>();

    var fused = new RrfFusionDomainService().FuseResults(vector, keyword, queryRoleHint: GameBookRole.Setup);

    fused[0].TextContent.Should().Be("setup");            // role-boost lifted it
    fused[0].RelevanceScore.Value.Should().BeApproximately(0.80, 1e-6); // cosine preserved
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~RrfFusionDomainServiceTests"`
Expected: FAIL — no `queryRoleHint` param; current unweighted fusion won't apply role-boost.

- [ ] **Step 3: Rewrite `FuseResults` as an adapter + change `GetChunkKey`** — key becomes `{PdfDocumentId}_{ChunkIndex}` (unified identity; both arms now carry it via Task 4/6). Re-join to originals, **RelevanceScore = the vector-preferred original's RelevanceScore (cosine)**:

```csharp
public virtual List<SearchResult> FuseResults(
    List<SearchResult> vectorResults,
    List<SearchResult> keywordResults,
    int rrfK = FusionSignals.DefaultRrfK,
    GameBookRole queryRoleHint = GameBookRole.None)
{
    if (rrfK <= 0) throw new ArgumentOutOfRangeException(nameof(rrfK));

    var vectorArm = vectorResults.Select((r, i) => new FusionCandidate(
        GetChunkKey(r), r.TextContent, r.RoleTags, i + 1, (float)r.RelevanceScore.Value)).ToList();
    var keywordArm = keywordResults.Select((r, i) => new FusionCandidate(
        GetChunkKey(r), r.TextContent, r.RoleTags, i + 1, (float)r.RelevanceScore.Value)).ToList();

    var fused = HybridFusionCore.Fuse(vectorArm, keywordArm,
        new FusionOptions(0.7f, 0.3f, rrfK, queryRoleHint));

    var vByKey = vectorResults.ToLookup(GetChunkKey, StringComparer.Ordinal);
    var kByKey = keywordResults.ToLookup(GetChunkKey, StringComparer.Ordinal);

    return fused.Select(f =>
    {
        var original = vByKey[f.Key].FirstOrDefault() ?? kByKey[f.Key].First(); // prefer vector
        return new SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: original.VectorDocumentId,
            textContent: f.Content,
            pageNumber: original.PageNumber,
            relevanceScore: original.RelevanceScore, // #2712: carried cosine, order-only fusion
            rank: f.Rank,
            searchMethod: "hybrid",
            pdfDocumentId: original.PdfDocumentId,
            chunkIndex: original.ChunkIndex,
            roleTags: f.RoleTags);
    }).ToList();
}

private static string GetChunkKey(SearchResult r) => $"{r.PdfDocumentId}_{r.ChunkIndex}";
```

> Remove the old `NormalizeRrfScore`/inline RRF math from `FuseResults` (the core owns it). Keep `CalculateRrfScore` only if still referenced by tests (`grep -n CalculateRrfScore`); otherwise delete. Add `using Api.BoundedContexts.KnowledgeBase.Domain.Services;` (for `HybridFusionCore`) + `using Api.BoundedContexts.GameManagement.Domain.ValueObjects;`.

- [ ] **Step 4: Update the 7 Moq setups** — each currently uses `It.IsAny<int>()` as the 3rd arg and will NOT match calls that now pass a 4th `GameBookRole`. Where the production call passes the hint (Task 6), broaden the setup:

```csharp
// before:
mock.Setup(x => x.FuseResults(It.IsAny<List<SearchResult>>(), It.IsAny<List<SearchResult>>(), It.IsAny<int>()))
// after:
mock.Setup(x => x.FuseResults(It.IsAny<List<SearchResult>>(), It.IsAny<List<SearchResult>>(), It.IsAny<int>(), It.IsAny<GameBookRole>()))
```
Apply at `AskQuestionQueryHandlerSecurityTests.cs:105,:552`, `StreamQaQueryHandlerTests.cs:648,:753,:1045`, `AskQuestionQueryHandlerPhase2Tests.cs:81,:605`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~RrfFusionDomainServiceTests|FullyQualifiedName~AskQuestionQueryHandler|FullyQualifiedName~StreamQaQueryHandler"`
Expected: PASS — new #2712 + role-hint tests green; Moq-based handler tests green.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/RrfFusionDomainService.cs apps/api/tests/Api.Tests
git commit -m "feat(rag): RrfFusionDomainService delegates to HybridFusionCore + role hint, preserves cosine #2712 (#3270)"
```

---

### Task 6: `SearchQueryHandler` — raw keyword arm (spec §6) + identity + role hint

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQueryHandler.cs` (ctor `:20-46` — swap `IHybridSearchService`→`IKeywordSearchService`; `PerformVectorSearchAsync:159-167`; `PerformHybridSearchAsync:177-213`)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs` (ADD a `KeywordSearchResult → SearchResult` mapper; leave the existing `HybridSearchResult` overload untouched)
- Modify (test ctors): the 5 files that construct `new SearchQueryHandler(...)` — `SearchQueryHandlerTests.cs`, `AskQuestionQueryHandlerSecurityTests.cs`, `StreamQaQueryHandlerTests.cs`, `AskQuestionQueryHandlerIntentRoutingTests.cs`, `AskQuestionQueryHandlerPhase2Tests.cs` — swap the `Mock<IHybridSearchService>` arg for `Mock<IKeywordSearchService>`.
- Test: `apps/api/tests/Api.Tests/.../SearchQueryHandlerTests.cs` (extend for #2051 + signal reach) + a mapper test.

**Interfaces:**
- Consumes: `SearchResult` fusion fields (Task 4); `FuseResults(..., queryRoleHint)` (Task 5); `IKeywordSearchService.SearchAsync(string query, Guid gameId, int limit=10, bool phraseSearch=false, List<string>? boostTerms=null, string language="en", double minScore=0.0, CancellationToken) → List<KeywordSearchResult>`; `KeywordSearchResult{ChunkId, Content, PdfDocumentId (string), GameId, ChunkIndex, PageNumber (int?), RelevanceScore (float ts_rank_cd), MatchedTerms, RoleTags}`.
- Produces: new extension `ToDomainSearchResult(this KeywordSearchResult result, int rank) → SearchResult`.

- [ ] **Step 0: Confirm the swap is safe** (verify before editing):
  - `grep -n "_hybridSearchService" SearchQueryHandler.cs` → the ONLY use is the keyword sub-call at `:196` (confirmed) ⇒ `IHybridSearchService` is fully removable from this handler once the raw arm lands. If any other use appears, keep the field instead of removing it.
  - `IKeywordSearchService` is `AddScoped` (`ApplicationServiceExtensions.cs:93`); `SearchQueryHandler` is `AddScoped` (`KnowledgeBaseServiceExtensions.cs:463`) ⇒ lifetime-safe, no DI registration change (memory `frontendsdk-di-lifetime-mismatch`).

- [ ] **Step 1: Write the failing tests** (`SearchQueryHandlerTests.cs` + a mapper test)

```csharp
[Fact]
public void ToDomainSearchResult_FromKeyword_CarriesIdentityAndRawScore()
{
    var pdf = Guid.NewGuid();
    var kr = new KeywordSearchResult {
        ChunkId = "c", Content = "text", PdfDocumentId = pdf.ToString(), GameId = Guid.NewGuid(),
        ChunkIndex = 4, PageNumber = 2, RelevanceScore = 0.22f, RoleTags = GameBookRole.Setup };

    var sr = kr.ToDomainSearchResult(1);

    sr.PdfDocumentId.Should().Be(pdf);
    sr.ChunkIndex.Should().Be(4);
    sr.RoleTags.Should().Be(GameBookRole.Setup);
    sr.RelevanceScore.Value.Should().BeApproximately(0.22, 1e-6); // raw ts_rank_cd
    sr.SearchMethod.Should().Be("keyword");
}

[Fact]
public async Task Hybrid_PrimaryPath_AppliesRoleBoostAndKeepsCosine()
{
    // Arrange handler with a REAL RrfFusionDomainService + mocked IEmbeddingRepository (vector arm)
    // + mocked IKeywordSearchService (keyword arm). Vector arm returns a Setup-tagged chunk with
    // LOWER cosine than a plain chunk; SearchQuery.QueryRoleHint = Setup.
    // Assert: fused[0] is the Setup chunk (role-boost lifted it) AND its RelevanceScore == its cosine.
}

[Fact]
public async Task Hybrid_PrimaryPath_DocumentIdsFilter_ExcludesOutOfScope()
{
    // Issue #2051: mock IKeywordSearchService to return an in-scope + an out-of-scope PdfDocumentId.
    // Run hybrid with SearchQuery.DocumentIds = [inScopePdf]; assert only the in-scope chunk survives.
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SearchQueryHandlerTests"`
Expected: FAIL — no `KeywordSearchResult` mapper; role-boost not reaching the primary path.

- [ ] **Step 3: Add the `KeywordSearchResult → SearchResult` mapper** in `KnowledgeBaseMappers.cs` (sibling to the existing `HybridSearchResult` overload; add `using Api.Services;` if not present):

```csharp
/// <summary>
/// Maps a RAW keyword-search result (issue #3270 §6) to a domain SearchResult, carrying the
/// {PdfDocumentId}_{ChunkIndex} fusion identity + RoleTags. RelevanceScore is the raw ts_rank_cd
/// (clamped to Confidence's [0,1]); HybridFusionCore applies role-boost/legend downstream.
/// </summary>
public static Domain.Entities.SearchResult ToDomainSearchResult(this KeywordSearchResult result, int rank)
{
    var pdfDocId = Guid.Parse(result.PdfDocumentId);
    var score = Math.Clamp((double)result.RelevanceScore, 0.0, 1.0);
    return new Domain.Entities.SearchResult(
        id: Guid.NewGuid(),
        vectorDocumentId: pdfDocId,          // same convention the HybridSearchResult mapper uses
        textContent: result.Content,
        pageNumber: result.PageNumber ?? 1,
        relevanceScore: new Confidence(score),
        rank: rank,
        searchMethod: "keyword",
        pdfDocumentId: pdfDocId,
        chunkIndex: result.ChunkIndex,
        roleTags: result.RoleTags);
}
```

- [ ] **Step 4: Swap the ctor dependency** — in `SearchQueryHandler.cs`, replace the `IHybridSearchService _hybridSearchService` field/param/assignment (`:26/:35/:43`) with `IKeywordSearchService _keywordSearchService`:

```csharp
private readonly IKeywordSearchService _keywordSearchService;
// ...ctor param... IKeywordSearchService keywordSearchService,
_keywordSearchService = keywordSearchService ?? throw new ArgumentNullException(nameof(keywordSearchService));
```
Add `using Api.Services;` (for `IKeywordSearchService`/`KeywordSearchResult`) if not already present.

- [ ] **Step 5: Thread the vector arm identity** — in `PerformVectorSearchAsync:159-167`, populate the 3 new `SearchResult` fields from `scored.Embedding`:

```csharp
return new Domain.Entities.SearchResult(
    id: Guid.NewGuid(),
    vectorDocumentId: scored.Embedding.VectorDocumentId,
    textContent: scored.Embedding.TextContent,
    pageNumber: scored.Embedding.PageNumber,
    relevanceScore: new Confidence(cosine),
    rank: index + 1,
    searchMethod: "vector",
    pdfDocumentId: scored.Embedding.PdfDocumentId,                       // real (JOIN-resolved)
    chunkIndex: scored.Embedding.ChunkIndex,
    roleTags: (GameBookRole)scored.Embedding.RoleTags);                  // int → enum
```

- [ ] **Step 6: Rewrite `PerformHybridSearchAsync`'s keyword arm** as the raw §6 arm:

```csharp
private async Task<List<Domain.Entities.SearchResult>> PerformHybridSearchAsync(
    Guid gameId, Vector queryVector, string query, int topK, double minScore,
    IReadOnlyList<Guid>? documentIds, GameBookRole queryRoleHint, CancellationToken cancellationToken)
{
    var vectorResults = await PerformVectorSearchAsync(
        gameId, queryVector, topK, minScore, documentIds, cancellationToken).ConfigureAwait(false);

    // Issue #423: ts_rank_cd scores ~0-0.3; 0.01 filters ToC noise.
    const double KeywordMinScore = 0.01;

    // Spec §6: RAW keyword arm sourced directly from IKeywordSearchService (un-boosted, raw ts_rank_cd
    // order) so HybridFusionCore applies role-boost + legend exactly once.
    var rawKeyword = await _keywordSearchService.SearchAsync(
        query,
        gameId,
        topK,
        phraseSearch: query.Contains('"'),            // reproduce HybridSearchService.cs:189 derivation
        boostTerms: null,                             // was _config.BoostTerms — a no-op on the tsquery
        minScore: KeywordMinScore,
        cancellationToken: cancellationToken).ConfigureAwait(false);

    // Issue #2051: reproduce the documentIds post-filter that SearchAsync(Keyword) applied internally.
    var filteredKeyword = documentIds is null
        ? (IReadOnlyList<KeywordSearchResult>)rawKeyword
        : rawKeyword
            .Where(r => documentIds.Any(id => string.Equals(id.ToString(), r.PdfDocumentId, StringComparison.Ordinal)))
            .ToList();

    var keywordResults = filteredKeyword
        .Select((kr, index) => kr.ToDomainSearchResult(index + 1))
        .ToList();

    return _rrfFusionService.FuseResults(vectorResults, keywordResults, queryRoleHint: queryRoleHint);
}
```

- [ ] **Step 7: Update the 5 test files' `new SearchQueryHandler(...)`** — replace the `Mock<IHybridSearchService>` constructor argument with a `Mock<IKeywordSearchService>` (default `SearchAsync` → empty `List<KeywordSearchResult>()`). Files: `SearchQueryHandlerTests.cs`, `AskQuestionQueryHandlerSecurityTests.cs`, `StreamQaQueryHandlerTests.cs`, `AskQuestionQueryHandlerIntentRoutingTests.cs`, `AskQuestionQueryHandlerPhase2Tests.cs`. (Some of these construct SearchQueryHandler indirectly — only edit the `new SearchQueryHandler(...)` sites; leave other handlers' ctors alone.)

- [ ] **Step 8: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SearchQueryHandler|FullyQualifiedName~KnowledgeBaseMappers"`
Expected: PASS — mapper carries identity + raw ts_rank_cd; role-boost reaches `/agents/qa`; RelevanceScore = cosine (#2712); #2051 scoping intact; build 0 warnings (no unused `IHybridSearchService`).

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQueryHandler.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs apps/api/tests/Api.Tests
git commit -m "feat(rag): primary chat path — raw keyword arm + fusion identity + role boost via unified core (#3270)"
```

---

### Task 7: `StreamQaQueryHandler` — role-hint parity for `/agents/qa/stream`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs` (ctor :31-80; `PerformSearchAndBuildCitationsAsync:284-292`)
- Modify: `apps/api/tests/Api.Tests/.../StreamQaQueryHandlerTests.cs` (add `IIntentClassifierService` mock to every ctor construction)
- Test: same file (add a role-hint assertion)

**Interfaces:**
- Consumes: `IIntentClassifierService` (already DI-registered; mirror `AskQuestionQueryHandler` usage `:57/:84/:105/:429-432`).

- [ ] **Step 1: Write the failing test** — assert the stream path sets `QueryRoleHint` from the classifier:

```csharp
[Fact]
public async Task StreamQa_SetsQueryRoleHint_FromIntentClassifier()
{
    // Arrange StreamQaQueryHandler with a mock IIntentClassifierService returning GameBookRole.Setup.
    // Capture the SearchQuery passed to the search collaborator; assert QueryRoleHint == Setup.
    // intentClassifierMock.Setup(x => x.ClassifyIntent(It.IsAny<string>())).Returns(GameBookRole.Setup);
    // ... assert captured.QueryRoleHint == GameBookRole.Setup;
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~StreamQaQueryHandlerTests"`
Expected: FAIL — ctor doesn't take `IIntentClassifierService`; `QueryRoleHint` defaults `None`.

- [ ] **Step 3: Inject the classifier + set the hint** — mirror `AskQuestionQueryHandler`:
  - Add `private readonly IIntentClassifierService _intentClassifier;` field.
  - Add ctor param + null-guard assignment (`?? throw new ArgumentNullException(...)`).
  - In `PerformSearchAndBuildCitationsAsync`, classify before building the query and pass BY NAME:

```csharp
var queryRoleHint = _intentClassifier.ClassifyIntent(queryText); // same call AskQuestionQueryHandler:429-432 uses
var searchQuery = new SearchQuery(
    // ... existing positional args (SearchMode "hybrid", Language "en", etc.) ...
    QueryRoleHint: queryRoleHint);
```

- [ ] **Step 4: Update every `StreamQaQueryHandlerTests` construction** — add a `Mock<IIntentClassifierService>` (default `ClassifyIntent` → `GameBookRole.None`) to each `new StreamQaQueryHandler(...)`. Confirm no new DI registration is needed (`IIntentClassifierService` is already registered — mirror `AskQuestionQueryHandler`'s registration).

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet build` then `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~StreamQaQueryHandlerTests"`
Expected: PASS — stream path sets the role hint; existing stream tests green.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs apps/api/tests/Api.Tests
git commit -m "feat(rag): StreamQa classifies intent + sets QueryRoleHint for role-boost parity (#3270)"
```

---

## Final verification (before PR)

- [ ] `cd apps/api/src/Api && dotnet build` → 0 warnings.
- [ ] Kill test hosts, then run the KnowledgeBase + fusion slice: `dotnet test apps/api/tests/Api.Tests --filter "BoundedContext=KnowledgeBase|FullyQualifiedName~Fusion|FullyQualifiedName~HybridSearch|FullyQualifiedName~SearchQueryHandler|FullyQualifiedName~RrfFusion|FullyQualifiedName~StreamQa|FullyQualifiedName~AskQuestion"` → all green.
- [ ] `dotnet ef migrations has-pending-model-changes` → clean (no schema change).
- [ ] Dispatch `feature-dev:code-reviewer` on the whole PR diff (memory `holistic-review-catches-cross-cutting-bugs`) — focus: #2712 cosine preservation on every fused path, double-boost absence on the primary path, key-intersection correctness (`{PdfDocumentId}_{ChunkIndex}` on both arms), the 8 `SearchAsync` callers + parity, Moq 4th-param matches, `TreatWarningsAsErrors` clean.
- [ ] Rebase onto `origin/main-dev` (stash `.vscode/settings.json` first): `git stash push -- .vscode/settings.json 2>/dev/null; git rebase origin/main-dev; git stash pop 2>/dev/null || true`.
- [ ] PR → `main-dev`, referencing #3270. Do NOT `gh pr merge --auto` (memory `gh-pr-merge-auto-stale-sha`).

## Out of scope (per spec §Goal — do NOT implement here)

- Heading-based boost (types don't carry `Heading` yet — deferred).
- Corpus re-index materializing headings/role_tags on existing rows (SP3, #3269).
- EN/IT retrieval non-regression suite (SP3). The TM "setup per N giocatori" (#3243) repro is a qualitative check only until SP3 re-index.

## Self-review

- **Spec coverage:** §1 HybridFusionCore→Task 2; §2 FusionSignals→Task 1; §3 HybridSearchService adapter→Task 3; §4 RrfFusionDomainService adapter + #2712→Task 5; §5 SearchResult fields→Task 4; §6 primary-path RAW keyword arm from `IKeywordSearchService` (reproducing #2051 documentIds filter + phraseSearch + minScore 0.01 + new `KeywordSearchResult→SearchResult` mapper; swap `IHybridSearchService`→`IKeywordSearchService`)→Task 6; §7 StreamQa role hint→Task 7. Risk/validation parity + core + #2712 + #2051 tests all placed. ✅
- **Placeholder scan:** the only "COPY verbatim from source" markers (Task 1 Step 3) are deliberate — the exact bodies are stable at `HybridSearchService.cs:536-580` and must be moved unchanged, not re-derived; every other step shows complete code. Parity golden values (Task 3 Step 2) are captured empirically before the refactor, by design. ✅
- **Type consistency:** `FusionCandidate(Key,Content,RoleTags,Rank,SourceScore)` / `FusionOptions(VectorWeight,KeywordWeight,RrfK,QueryRoleHint)` / `FusedCandidate(Key,Content,RoleTags,HybridScore,VectorScore,KeywordScore,VectorRank,KeywordRank,Rank)` / `HybridFusionCore.Fuse` / `FusionSignals.{ComputeRoleMatchBoost,ComputeLegendPenaltyFactor,RoleMatchBoost,DefaultRrfK}` — names identical across Tasks 2/3/5. `FuseResults(...,int rrfK,GameBookRole queryRoleHint)` consistent Tasks 5/6. ✅

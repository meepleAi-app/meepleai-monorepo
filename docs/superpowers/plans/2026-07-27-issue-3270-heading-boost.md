# SP4 Heading-Based Ranking Boost Implementation Plan (#3270)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a query-dependent heading-match ranking boost to the RAG hybrid fusion so a chunk whose persisted `Heading` matches the user's query terms is promoted — closing the SP4 deliverable of epic #3266 and fixing the motivating case ("Setup per N giocatori" surfacing the wrong section).

**Architecture:** `text_chunks.Heading` is already persisted (SP2 #3268). Thread `Heading` from the two DB read arms (keyword FTS SQL + vector `JOIN text_chunks`) through the shared neutral fusion plumbing (`FusionCandidate` → `HybridFusionCore` → arm adapters), mirroring exactly how `RoleTags` already flows. Add a query-side `QueryTerms` signal (normalized query tokens) to `FusionOptions`, and a new additive `FusionSignals.ComputeHeadingMatchBoost` consumed in the single canonical scorer `HybridFusionCore.Fuse` — the exact structural twin of the existing `ComputeRoleMatchBoost` role-boost.

**Tech Stack:** .NET 9, EF Core + Npgsql (raw SQL for FTS + pgvector), xUnit + Testcontainers. Bounded context: `KnowledgeBase`.

## Global Constraints

- **Build the SOLUTION, not the project**, after any ctor/record-signature change: `cd apps/api/src/Api && dotnet build Api.sln` (a project-only build misses call-site breakages in `Api.Tests`).
- **Kill testhost before running tests** (Windows): `tasklist | grep testhost` → `taskkill //PID <PID> //F`.
- **Tests must be culture-independent** — no locale-dependent string/number formatting in assertions.
- `FusionSignals`, `HybridFusionCore`, `FusionCandidate`, `FusionOptions`, `FusedCandidate`, `SearchResult`, `SearchQuery` are `internal` — unit tests reach them via the existing `InternalsVisibleTo("Api.Tests")` (the role-boost tests already depend on it; do not add a new one).
- **Vector arm strategy = JOIN** (locked decision): `PgVectorStoreAdapter.SearchWithScoresAsync` gains `JOIN text_chunks tc ON tc."Id" = e.source_chunk_id` and selects `tc."Heading"`. NO migration, NO re-embed — this reuses the rows the in-flight SP3 re-index is populating.
- **Boost semantics = query-dependent** (locked): `ComputeHeadingMatchBoost(queryTerms, heading)`, additive, magnitude `0.15f` (equal to `RoleMatchBoost`). It affects ORDER only — `MinScore` gates on the preserved cosine `RelevanceScore` (#2712), never on `hybridScore`.
- **Scope = unified core** (locked): boost is applied once in `HybridFusionCore.Fuse`, covering the primary chat path (`/agents/qa` + `/stream` via `RrfFusionDomainService`) and the admin playground (`HybridSearchService.FuseSearchResults`). Single-arm modes (`SearchSemanticOnlyAsync`/`SearchKeywordOnlyAsync`) and `RagPromptAssemblyService` are OUT of scope (documented as follow-up).
- **`Heading` carrier type is `string?`** everywhere (nullable — chunks pre-SP3 or imported may have no heading).
- **`QueryTerms` contract**: already lowercased, de-duplicated, length ≥ 3, produced ONLY by `FusionSignals.ExtractHeadingMatchTerms`. `ComputeHeadingMatchBoost` assumes lowercase terms and lowercases only the heading.

---

## Known limitation (documented, deferred)

The MVP boost is a **pure lexical** query-term↔heading substring match. The cross-lingual synonym case (query "setup" → heading "Preparazione") is NOT boosted by lexical match alone — only "setup" → a heading literally containing "setup" fires. Italian rulebooks frequently title the section "Setup" (English loanword, see `KeywordSearchService.SynonymTablesByConfig` note), so the motivating case is still improved. Wiring the curated synonym expansion (`SynonymTablesByConfig`) into `ExtractHeadingMatchTerms` is a follow-up, tracked in the closing comment of #3270.

---

## Task 1: Heading-match boost signal (pure function)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/FusionSignals.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/FusionSignalsTests.cs` (extend existing role-boost test file if present; else create)

**Interfaces:**
- Produces: `FusionSignals.HeadingMatchBoost` (const `float` = 0.15f); `FusionSignals.ComputeHeadingMatchBoost(IReadOnlyList<string>? queryTerms, string? chunkHeading) -> float`; `FusionSignals.ExtractHeadingMatchTerms(string? query) -> IReadOnlyList<string>`.

- [ ] **Step 1: Write the failing tests**

Add to the test file (create the file with the standard test header if it does not exist; locate it first with `Glob "**/FusionSignalsTests.cs"`):

```csharp
[Fact]
public void ComputeHeadingMatchBoost_TermInHeading_ReturnsBoost()
{
    var terms = new[] { "setup", "giocatori" };
    var boost = FusionSignals.ComputeHeadingMatchBoost(terms, "Setup");
    Assert.Equal(FusionSignals.HeadingMatchBoost, boost);
}

[Fact]
public void ComputeHeadingMatchBoost_NoTermInHeading_ReturnsZero()
{
    var terms = new[] { "scoring", "endgame" };
    Assert.Equal(0f, FusionSignals.ComputeHeadingMatchBoost(terms, "Setup"));
}

[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public void ComputeHeadingMatchBoost_BlankHeading_ReturnsZero(string? heading)
{
    Assert.Equal(0f, FusionSignals.ComputeHeadingMatchBoost(new[] { "setup" }, heading));
}

[Fact]
public void ComputeHeadingMatchBoost_EmptyTerms_ReturnsZero()
{
    Assert.Equal(0f, FusionSignals.ComputeHeadingMatchBoost(Array.Empty<string>(), "Setup"));
}

[Fact]
public void ComputeHeadingMatchBoost_IsCaseInsensitiveOnHeadingOnly()
{
    // terms are contract-lowercased; heading may be any case
    Assert.Equal(FusionSignals.HeadingMatchBoost,
        FusionSignals.ComputeHeadingMatchBoost(new[] { "preparazione" }, "PREPARAZIONE del gioco"));
}

[Fact]
public void ExtractHeadingMatchTerms_NormalizesLowercasesFiltersShortAndDedups()
{
    var terms = FusionSignals.ExtractHeadingMatchTerms("Setup per N giocatori, setup!");
    Assert.Equal(new[] { "setup", "per", "giocatori" }, terms); // "N" dropped (len<3), "setup" deduped
}

[Fact]
public void ExtractHeadingMatchTerms_Blank_ReturnsEmpty()
{
    Assert.Empty(FusionSignals.ExtractHeadingMatchTerms("  "));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FusionSignalsTests" -v minimal`
Expected: FAIL — `ComputeHeadingMatchBoost` / `ExtractHeadingMatchTerms` / `HeadingMatchBoost` do not exist.

- [ ] **Step 3: Implement the signal in `FusionSignals.cs`**

Add after the `RoleMatchBoost` const (line 14) and after `ComputeRoleMatchBoost` (line 38). Add `using System.Linq;` is already implied; add `using System.Collections.Generic;` if not present (file currently uses only Regex + GameBookRole — add both usings at top).

```csharp
    /// <summary>Additive heading-match boost (#3270). Same magnitude/shape as the role boost.</summary>
    internal const float HeadingMatchBoost = 0.15f;

    /// <summary>
    /// Additive boost when any normalized query term appears in the chunk's heading (#3270).
    /// Query-dependent, structural twin of <see cref="ComputeRoleMatchBoost"/>. <paramref name="queryTerms"/>
    /// MUST be lowercased (contract of <see cref="ExtractHeadingMatchTerms"/>); the heading is lowercased here.
    /// </summary>
    internal static float ComputeHeadingMatchBoost(IReadOnlyList<string>? queryTerms, string? chunkHeading)
    {
        if (queryTerms is null || queryTerms.Count == 0 || string.IsNullOrWhiteSpace(chunkHeading))
        {
            return 0f;
        }

        var headingLower = chunkHeading.ToLowerInvariant();
        for (var i = 0; i < queryTerms.Count; i++)
        {
            var term = queryTerms[i];
            if (term.Length >= 3 && headingLower.Contains(term, StringComparison.Ordinal))
            {
                return HeadingMatchBoost;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Normalizes a raw query into heading-match terms: lowercased, punctuation-split, length ≥ 3,
    /// de-duplicated (order-preserving). This is the ONLY producer of the query-term contract consumed
    /// by <see cref="ComputeHeadingMatchBoost"/>.
    /// </summary>
    internal static IReadOnlyList<string> ExtractHeadingMatchTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var terms = new List<string>();
        foreach (var raw in query.Split(
            new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '?', '!', '"', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            var t = raw.Trim().ToLowerInvariant();
            if (t.Length >= 3 && seen.Add(t))
            {
                terms.Add(t);
            }
        }

        return terms;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FusionSignalsTests" -v minimal`
Expected: PASS (all Task-1 tests green).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/FusionSignals.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/FusionSignalsTests.cs
git commit -m "feat(rag): add ComputeHeadingMatchBoost + ExtractHeadingMatchTerms signal (#3270)"
```

---

## Task 2: Thread Heading + QueryTerms through the fusion core and apply the boost

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/HybridFusionCore.cs`
- Test: `apps/api/tests/Api.Tests/**/HybridFusionCoreTests.cs` (locate with `Glob "**/HybridFusionCoreTests.cs"`)

**Interfaces:**
- Consumes: `FusionSignals.ComputeHeadingMatchBoost` (Task 1).
- Produces: `FusionCandidate(string Key, string Content, GameBookRole RoleTags, string? Heading, int Rank, float SourceScore)`; `FusionOptions(..., IReadOnlyList<string>? QueryTerms = null)`; `FusedCandidate(..., string? Heading, ...)`. **These signatures are consumed by Tasks 8 and 9 — keep them exact.**

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Fuse_ChunkHeadingMatchesQueryTerm_RanksAboveNonMatchingPeer()
{
    // Two vector-arm chunks at equal rank; only "kA" has a matching heading.
    var vector = new List<FusionCandidate>
    {
        new("kA", "content A", GameBookRole.None, "Setup", 1, 0.9f),
        new("kB", "content B", GameBookRole.None, "Scoring", 1, 0.9f),
    };
    var keyword = new List<FusionCandidate>();
    var opts = new FusionOptions(0.7f, 0.3f, 60, GameBookRole.None, new[] { "setup" });

    var fused = HybridFusionCore.Fuse(vector, keyword, opts);

    Assert.Equal("kA", fused[0].Key);      // heading-boosted chunk ranks first
    Assert.Equal("Setup", fused[0].Heading); // heading carried through to FusedCandidate
}

[Fact]
public void Fuse_NoQueryTerms_HeadingBoostIsNoOp()
{
    var vector = new List<FusionCandidate>
    {
        new("kA", "content A", GameBookRole.None, "Setup", 1, 0.9f),
        new("kB", "content B", GameBookRole.None, "Scoring", 2, 0.8f),
    };
    var fused = HybridFusionCore.Fuse(vector, new List<FusionCandidate>(), new FusionOptions());
    Assert.Equal("kA", fused[0].Key); // pure RRF order (rank 1 wins), no heading influence
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: FAIL — `FusionCandidate` has no 6-arg ctor, `FusionOptions` has no `QueryTerms`, `FusedCandidate` has no `Heading`.

- [ ] **Step 3: Implement — records + Fuse**

In `HybridFusionCore.cs`, change the three records (lines 6-29) and the scoring loop (lines 56-73):

```csharp
internal readonly record struct FusionCandidate(
    string Key,
    string Content,
    GameBookRole RoleTags,
    string? Heading,
    int Rank,          // 1-based within THIS arm (cosine desc / ts_rank_cd desc)
    float SourceScore);

internal sealed record FusionOptions(
    float VectorWeight = 0.7f,
    float KeywordWeight = 0.3f,
    int RrfK = FusionSignals.DefaultRrfK,
    GameBookRole QueryRoleHint = GameBookRole.None,
    IReadOnlyList<string>? QueryTerms = null);

internal readonly record struct FusedCandidate(
    string Key,
    string Content,
    GameBookRole RoleTags,
    string? Heading,
    float HybridScore,
    float? VectorScore,
    float? KeywordScore,
    int? VectorRank,
    int? KeywordRank,
    int Rank);
```

In the `Fuse` loop, after the `roleTags` union line (line 58) add the heading merge, then extend the score and the `FusedCandidate` emission:

```csharp
            // Prefer vector-arm content (load-bearing: legend factor is computed from it).
            string content = hasV ? v.Content : k.Content;
            GameBookRole roleTags = (hasV ? v.RoleTags : GameBookRole.None) | (hasK ? k.RoleTags : GameBookRole.None);
            string? heading = (hasV ? v.Heading : null) ?? (hasK ? k.Heading : null);

            float legendFactor = FusionSignals.ComputeLegendPenaltyFactor(content);
            float roleBoost = FusionSignals.ComputeRoleMatchBoost(options.QueryRoleHint, roleTags);
            float headingBoost = FusionSignals.ComputeHeadingMatchBoost(options.QueryTerms, heading);
            float hybridScore = (rrfSum * (1f - legendFactor)) + roleBoost + headingBoost;

            scored.Add(new FusedCandidate(
                Key: key,
                Content: content,
                RoleTags: roleTags,
                Heading: heading,
                HybridScore: hybridScore,
                VectorScore: hasV ? v.SourceScore : (float?)null,
                KeywordScore: hasK ? k.SourceScore : (float?)null,
                VectorRank: hasV ? v.Rank : (int?)null,
                KeywordRank: hasK ? k.Rank : (int?)null,
                Rank: 0)); // assigned after sort
```

- [ ] **Step 4: Build + run the fusion-core tests**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: FAIL in `RrfFusionDomainService.cs:38,41` and `HybridSearchService.cs:401,408` (5-arg `FusionCandidate` call sites) — this is EXPECTED and fixed in Tasks 8/9. To green Task 2's own tests in isolation, temporarily add `null` as the `Heading` arg at those 4 call sites (Tasks 8/9 replace `null` with the real value). After that: `dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~HybridFusionCoreTests" -v minimal` → PASS.

> **Note:** because the record change ripples to adapters, Tasks 8 and 9 are the natural same-commit companions. If executing task-by-task with a green build gate, fold Tasks 2/8/9 into one commit; the tests for each remain separate.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/HybridFusionCore.cs apps/api/tests/Api.Tests/**/HybridFusionCoreTests.cs
git commit -m "feat(rag): carry Heading + QueryTerms through HybridFusionCore + apply heading boost (#3270)"
```

---

## Task 3: Keyword arm — SELECT and carry Heading

**Files:**
- Modify: `apps/api/src/Api/Services/KeywordSearchService.cs` (SQL 97-115, map 139-151, `KeywordSearchRawResult` 506-525)
- Modify: `apps/api/src/Api/Services/IKeywordSearchService.cs` (`KeywordSearchResult` record, RoleTags ~line 80)

**Interfaces:**
- Produces: `KeywordSearchResult.Heading` (`string?`) — consumed by Tasks 6/9.

- [ ] **Step 1: Add `"Heading"` to the FTS SQL projection**

In the inner `SELECT` (after `role_tags AS "RoleTags",` line 106), add:

```
                        ""Heading"",
```

(text_chunks."Heading" already exists — no schema change. It flows through the `ranked` subquery automatically.)

- [ ] **Step 2: Add `Heading` to `KeywordSearchRawResult`**

After `public int RoleTags { get; set; }` (line 524):

```csharp
    /// <summary>#3270: heading-path label from text_chunks."Heading" (nullable).</summary>
    public string? Heading { get; set; }
```

- [ ] **Step 3: Map it onto `KeywordSearchResult`**

In the `.Select(r => new KeywordSearchResult { ... })` (line 139), after `RoleTags = (GameBookRole)r.RoleTags`:

```csharp
                RoleTags = (GameBookRole)r.RoleTags,
                Heading = r.Heading
```

- [ ] **Step 4: Add `Heading` to the `KeywordSearchResult` record in `IKeywordSearchService.cs`**

Mirror `RoleTags` (the record's last property). Add:

```csharp
    /// <summary>#3270: chunk heading-path label for the heading-match boost (nullable).</summary>
    public string? Heading { get; init; }
```

- [ ] **Step 5: Build**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: PASS (additive, no call-site breakage).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Services/KeywordSearchService.cs apps/api/src/Api/Services/IKeywordSearchService.cs
git commit -m "feat(rag): project + carry Heading on the keyword search arm (#3270)"
```

---

## Task 4: Vector arm — JOIN text_chunks and carry Heading on Embedding

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs` (`SearchWithScoresAsync`, SQL ~142-149, reader ~185-206)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/Embedding.cs` (add `Heading`, mirror `RoleTags` at line 30 + ctor)
- Test: `apps/api/tests/Api.Tests/Integration/**/PgVector*IntegrationTests.cs` (extend an existing hybrid-search integration test to assert Heading is populated; locate with `Glob "**/*PgVector*IntegrationTests.cs"`)

**Interfaces:**
- Consumes: `text_chunks."Heading"`, `pgvector_embeddings.source_chunk_id`.
- Produces: `Embedding.Heading` (`string?`) — consumed by Tasks 6/9 (`SearchQueryHandler.PerformVectorSearchAsync`, `HybridSearchService.SearchHybridAsync`).

- [ ] **Step 1: Add `Heading` to `Embedding` (mirror RoleTags)**

Read `Embedding.cs` first. Add a `public string? Heading { get; private set; }` next to `RoleTags` (line 30), a ctor param `string? heading = null` next to `roleTags` (line 63), and assignment `Heading = heading;` next to line 88.

- [ ] **Step 2: JOIN text_chunks + SELECT tc."Heading" in `SearchWithScoresAsync`**

Read the method first (SELECT ~142-149, reader loop ~185-206). The method already `JOIN`s `vector_documents vd`. Add:

```sql
JOIN text_chunks tc ON tc."Id" = e.source_chunk_id
```

and add `tc."Heading"` as the LAST selected column (append after the current final column to avoid re-indexing the existing ordinal reads). Then in the reader loop, read the new trailing column: `var heading = reader.IsDBNull(N) ? null : reader.GetString(N);` (N = the new last ordinal) and pass `heading: heading` to the `Embedding` ctor.

> **JOIN safety:** `source_chunk_id` is the FK from `pgvector_embeddings` to `text_chunks."Id"`. Use `JOIN` (not LEFT JOIN) ONLY if every embedding has a source chunk; if pre-SP2 embeddings may lack `source_chunk_id`, use `LEFT JOIN` so a missing chunk yields `Heading = null` rather than dropping the row. **Verify** with: `Grep "source_chunk_id" apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs` and the CREATE TABLE (nullable?) before choosing. Default to **LEFT JOIN** for safety.

- [ ] **Step 3: Build**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: PASS.

- [ ] **Step 4: Integration test — Heading populated on a vector hit**

Extend an existing hybrid-search integration test (Testcontainers Postgres + pgvector): seed a `text_chunks` row with `Heading = "Setup"` + a matching `pgvector_embeddings` row with the same `source_chunk_id`, run `SearchWithScoresAsync`, assert the returned `ScoredEmbedding.Embedding.Heading == "Setup"`.

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PgVector" -v minimal` (requires Docker).
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/PgVectorStoreAdapter.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/Embedding.cs apps/api/tests/Api.Tests/Integration/**
git commit -m "feat(rag): join text_chunks to carry Heading on the vector search arm (#3270)"
```

---

## Task 5: Add Heading to the result-type chain

**Files (each = add `string? Heading`, mirror the existing `RoleTags` property exactly):**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/SearchResult.cs` — add `public GameBookRole RoleTags`-twin `public string? Heading { get; private set; }` (property near line 21) + ctor param `string? heading = null` (after `roleTags` line 45) + assignment `Heading = heading;` (after line 64).
- Modify: `apps/api/src/Api/Services/VectorSearchModels.cs` — `SearchResultItem` (RoleTags ~line 71): add `public string? Heading { get; init; }`.
- Modify: `apps/api/src/Api/Services/IHybridSearchService.cs` — `HybridSearchResult` (RoleTags ~line 124): add `public string? Heading { get; init; }`.

**Interfaces:**
- Produces: `SearchResult.Heading`, `SearchResultItem.Heading`, `HybridSearchResult.Heading` — all `string?`, consumed by Tasks 6/8/9.

- [ ] **Step 1: Add the three properties** (see file list above — read each file, add the property mirroring RoleTags; `SearchResult` also needs the ctor param + assignment).

- [ ] **Step 2: Build**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: PASS (additive; nullable defaults keep existing callers valid).

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/SearchResult.cs apps/api/src/Api/Services/VectorSearchModels.cs apps/api/src/Api/Services/IHybridSearchService.cs
git commit -m "feat(rag): add Heading to SearchResult/SearchResultItem/HybridSearchResult (#3270)"
```

---

## Task 6: Populate Heading at the producers/mappers

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQueryHandler.cs` — `PerformVectorSearchAsync` (`new SearchResult(...)` ~line 169): add `heading: scored.Embedding.Heading` to the ctor call.
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs` — `KeywordSearchResult.ToDomainSearchResult` (~line 104): add `heading: result.Heading` to the `new SearchResult(...)` call.
- Modify: `apps/api/src/Api/Services/HybridSearchService.cs` — `SearchHybridAsync` (Embedding→SearchResultItem, ~line 285): add `Heading = se.Embedding.Heading` in the `new SearchResultItem { ... }` initializer.

**Interfaces:**
- Consumes: `Embedding.Heading` (Task 4), `KeywordSearchResult.Heading` (Task 3), `SearchResult.Heading`/`SearchResultItem.Heading` (Task 5).

- [ ] **Step 1: Add the three population sites** (read each; add the `heading:`/`Heading =` line mirroring the adjacent `roleTags:`/`RoleTags =`).

- [ ] **Step 2: Build**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQueryHandler.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Mappers/KnowledgeBaseMappers.cs apps/api/src/Api/Services/HybridSearchService.cs
git commit -m "feat(rag): populate Heading at vector/keyword result producers (#3270)"
```

---

## Task 7: Query-terms plumbing (SearchQuery → handlers)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQuery.cs` — add `IReadOnlyList<string>? QueryTerms = null` as the last record parameter (after `QueryRoleHint`).
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs` — where `QueryRoleHint` is set (~line 442): also set `QueryTerms = FusionSignals.ExtractHeadingMatchTerms(query.Question)`.
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs` — identical change at its `QueryRoleHint` site.

**Interfaces:**
- Consumes: `FusionSignals.ExtractHeadingMatchTerms` (Task 1).
- Produces: `SearchQuery.QueryTerms` — consumed by Task 8.

- [ ] **Step 1: Add `QueryTerms` to `SearchQuery`**

```csharp
    GameBookRole QueryRoleHint = GameBookRole.None,
    // #3270: normalized heading-match terms (lowercased, len≥3, deduped) for the heading boost.
    IReadOnlyList<string>? QueryTerms = null
) : IQuery<List<SearchResultDto>>;
```

- [ ] **Step 2: Populate in both handlers** — read each handler's `SearchQuery` construction; add `QueryTerms = FusionSignals.ExtractHeadingMatchTerms(<the raw question string used for QueryRoleHint>)`. Add `using Api.BoundedContexts.KnowledgeBase.Domain.Services;` if not present.

- [ ] **Step 3: Build**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQuery.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs
git commit -m "feat(rag): plumb normalized QueryTerms from chat handlers into SearchQuery (#3270)"
```

---

## Task 8: Chat-path adapter — wire Heading + QueryTerms into fusion

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/RrfFusionDomainService.cs` (`FuseResults` 28-72)
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQueryHandler.cs` (`PerformHybridSearchAsync` call to `FuseResults` ~line 220)
- Test: `apps/api/tests/Api.Tests/**/RrfFusionDomainServiceTests.cs`

**Interfaces:**
- Consumes: `FusionCandidate` 6-arg ctor + `FusionOptions.QueryTerms` (Task 2), `SearchResult.Heading` (Task 5), `SearchQuery.QueryTerms` (Task 7).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void FuseResults_QueryTermMatchesHeading_PromotesChunk()
{
    var svc = new RrfFusionDomainService();
    var conf = Confidence.Create(0.9);
    var a = new SearchResult(Guid.NewGuid(), Guid.NewGuid(), "content A", 1, conf, 1, "vector", Guid.NewGuid(), 0, GameBookRole.None, heading: "Setup");
    var b = new SearchResult(Guid.NewGuid(), Guid.NewGuid(), "content B", 1, conf, 1, "vector", Guid.NewGuid(), 1, GameBookRole.None, heading: "Scoring");

    var fused = svc.FuseResults(new List<SearchResult> { a, b }, new List<SearchResult>(), queryTerms: new[] { "setup" });

    Assert.Equal("Setup", fused[0].Heading);            // "Setup"-headed chunk first
    Assert.Equal(a.PdfDocumentId, fused[0].PdfDocumentId);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: FAIL — `FuseResults` has no `queryTerms` param; `SearchResult` ctor has no `heading` (fixed only if Task 5 landed) — ensure Tasks 2 and 5 are done first.

- [ ] **Step 3: Extend `FuseResults`**

Change the signature and the two arm projections + the rebuild + the `FusionOptions` construction:

```csharp
    public virtual List<SearchResult> FuseResults(
        List<SearchResult> vectorResults,
        List<SearchResult> keywordResults,
        int rrfK = DefaultRrfK,
        GameBookRole queryRoleHint = GameBookRole.None,
        IReadOnlyList<string>? queryTerms = null)
    {
        if (rrfK <= 0)
            throw new ArgumentException("RRF K must be positive", nameof(rrfK));

        var vectorArm = vectorResults
            .Select((r, i) => new FusionCandidate(GetChunkKey(r), r.TextContent, r.RoleTags, r.Heading, i + 1, (float)r.RelevanceScore.Value))
            .ToList();
        var keywordArm = keywordResults
            .Select((r, i) => new FusionCandidate(GetChunkKey(r), r.TextContent, r.RoleTags, r.Heading, i + 1, (float)r.RelevanceScore.Value))
            .ToList();

        var fused = HybridFusionCore.Fuse(vectorArm, keywordArm, new FusionOptions(0.7f, 0.3f, rrfK, queryRoleHint, queryTerms));
```

and in the rebuild `new SearchResult(...)` (lines 54-69) add `heading: f.Heading` after `roleTags: f.RoleTags`.

- [ ] **Step 4: Forward `queryTerms` from the handler**

In `SearchQueryHandler.PerformHybridSearchAsync`, change the `_rrfFusionService.FuseResults(vectorResults, keywordResults, ..., queryRoleHint)` call (~line 220) to also pass `queryTerms: request.QueryTerms` (thread `SearchQuery.QueryTerms` into the method if not already available in scope — read the method signature; it receives the `SearchQuery`).

- [ ] **Step 5: Build + test**

Run: `cd apps/api/src/Api && dotnet build Api.sln && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~RrfFusionDomainServiceTests" -v minimal`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/VectorSearch/RrfFusionDomainService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/SearchQueryHandler.cs apps/api/tests/Api.Tests/**/RrfFusionDomainServiceTests.cs
git commit -m "feat(rag): wire Heading + QueryTerms into chat-path RRF fusion (#3270)"
```

---

## Task 9: Playground adapter — wire Heading + QueryTerms

**Files:**
- Modify: `apps/api/src/Api/Services/HybridSearchService.cs` — `FuseSearchResults` (FusionCandidate builds 401/408, rebuild 441-457) + the `FusionOptions` construction (415-418) + derive `queryTerms` in `SearchHybridAsync` from the query.
- Test: `apps/api/tests/Api.Tests/**/HybridSearchServiceTests.cs` (extend if a fusion test exists).

**Interfaces:**
- Consumes: `FusionCandidate` 6-arg ctor + `FusionOptions.QueryTerms` (Task 2), `SearchResultItem.Heading` + `KeywordSearchResult.Heading` (Tasks 3/5), `HybridSearchResult.Heading` (Task 5), `FusionSignals.ExtractHeadingMatchTerms` (Task 1).

- [ ] **Step 1: Add Heading to both FusionCandidate builds** — line 401 (`new FusionCandidate(VectorKeyOf(r), r.Text, r.RoleTags, r.Heading, index+1, r.Score)`) and line 408 (`new FusionCandidate(KeywordKeyOf(r), r.Content, r.RoleTags, r.Heading, index+1, r.RelevanceScore)`).

- [ ] **Step 2: Pass QueryTerms in FusionOptions** — at the `new FusionOptions(vectorWeight, keywordWeight, rrfK, queryRoleHint)` (415-418), append `, FusionSignals.ExtractHeadingMatchTerms(query)` where `query` is the raw search string in scope (read `SearchHybridAsync` to find the query variable name).

- [ ] **Step 3: Carry Heading onto the rebuilt HybridSearchResult** — in `FuseSearchResults` rebuild (~line 456), add `Heading = f.Heading` after `RoleTags = f.RoleTags`.

- [ ] **Step 4: Build + test**

Run: `cd apps/api/src/Api && dotnet build Api.sln && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~HybridSearchServiceTests" -v minimal`
Expected: PASS (whole solution now builds — all `FusionCandidate` call sites carry Heading).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Services/HybridSearchService.cs apps/api/tests/Api.Tests/**/HybridSearchServiceTests.cs
git commit -m "feat(rag): wire Heading + QueryTerms into playground hybrid fusion (#3270)"
```

---

## Task 10: Secondary write gap — ImportRagData carries Heading

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandHandler.cs` (`new TextChunkEntity { ... }` ~line 226-240 — omits Heading today)
- Possibly modify: the `ExportedChunkDto`/import model if it does not already carry `Heading` (read it; add `string? Heading` + export/import wiring if missing).

**Interfaces:**
- Consumes: `TextChunkEntity.Heading` (already persisted).

- [ ] **Step 1: Set Heading on restore** — in the `new TextChunkEntity { ... }` initializer add `Heading = chunk.Heading,` (verify the export DTO carries it; if not, add it to the export in the matching `ExportRagData` handler + DTO so round-trip is lossless).

- [ ] **Step 2: Build**

Run: `cd apps/api/src/Api && dotnet build Api.sln`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/*
git commit -m "fix(rag): carry Heading through ImportRagData restore path (#3270)"
```

---

## Task 11: End-to-end heading-boost integration test

**Files:**
- Test: `apps/api/tests/Api.Tests/Integration/KnowledgeBase/HeadingBoostIntegrationTests.cs` (create)

**Interfaces:**
- Consumes: the full pipeline (Tasks 1-9).

- [ ] **Step 1: Write the integration test**

Seed (Testcontainers Postgres + pgvector): one game, two `text_chunks` for the same PDF — chunk X with `Heading = "Setup"` + content about player-count setup, chunk Y with `Heading = "Scoring"` + unrelated content — plus matching `pgvector_embeddings` rows (same `source_chunk_id`, embeddings close enough that both are retrieved). Issue a hybrid `SearchQuery` with `Query = "setup per N giocatori"`, `QueryTerms = FusionSignals.ExtractHeadingMatchTerms("setup per N giocatori")`. Assert the "Setup"-headed chunk X ranks above Y, and that with `QueryTerms = null` the order reverts to the pure-RRF baseline (guards against the boost firing unconditionally).

- [ ] **Step 2: Run**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~HeadingBoostIntegrationTests" -v minimal` (requires Docker).
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/KnowledgeBase/HeadingBoostIntegrationTests.cs
git commit -m "test(rag): end-to-end heading-boost ranking integration test (#3270)"
```

---

## Final verification (before PR)

- [ ] `cd apps/api/src/Api && dotnet build Api.sln` → 0 warnings, 0 errors.
- [ ] `dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=KnowledgeBase" -v minimal` → green (kill testhost first).
- [ ] No EF migration was added (JOIN strategy — confirm `git status` shows no `Migrations/` changes).
- [ ] PR to `main-dev` with base = parent branch; body references #3270 and notes: heading-boost active on chat + playground; single-arm modes + RagPromptAssembly + synonym-expansion for headings are documented follow-ups; runtime effect materializes as the SP3 re-index populates `text_chunks.Heading`.
- [ ] Post the "heading-boost shipped" comment on #3270 with the deferred-scope list; keep #3270 open only if the follow-ups warrant, else close.

---

## Self-review notes

- **Spec coverage:** SP4 heading-boost deliverable = vector-arm boost via heading (Tasks 2/4/8/9), heading persistence read (Tasks 3/4), query signal (Tasks 1/7). ✅ All three #3270-remaining items covered. Query-expansion piece already retired (PR #3307) — not in scope. Reranker-in-playground already shipped (Slice B #3258) — not in scope.
- **Type consistency:** `Heading` is `string?` at every hop (FusionCandidate/FusedCandidate/SearchResult/SearchResultItem/HybridSearchResult/KeywordSearchResult/Embedding). `QueryTerms` is `IReadOnlyList<string>?` on both `FusionOptions` and `SearchQuery`. `ComputeHeadingMatchBoost(IReadOnlyList<string>?, string?)` and `ExtractHeadingMatchTerms(string?)` signatures are used identically in Tasks 1/2/7/9.
- **Ordering dependency:** Tasks 2/5 must precede 6/8/9 (they define the fields the later tasks populate). The record-signature change in Task 2 breaks the build until Tasks 8/9 update the 4 `FusionCandidate` call sites — the plan flags folding 2/8/9 into one green commit.

# Dynamic Retrieval Profile + Multilingual Query Complexity Classifier

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the RAG retrieval pipeline adaptive — more chunks for complex questions, scaled by user tier and LLM context window — and make the query classifier work in Italian and English.

**Architecture:** Introduce a `RetrievalProfile` value object that replaces the hardcoded `RerankedTopK=5` / `DefaultMinScore=0.55` / `FtsTopK=10` constants. The profile is resolved from `QueryComplexity × LlmUserTier` via a pure function. The `QueryComplexityClassifier` is extended with multilingual heuristic patterns (IT+EN) and a language-agnostic structural analysis layer, plus an explicitly multilingual LLM prompt.

**Tech Stack:** C# .NET 9, xUnit + FluentAssertions + Moq, existing KnowledgeBase BC

**Parent branch:** `main-dev`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/RetrievalProfile.cs` | Immutable record with TopK, MinScore, FtsTopK, WindowRadius |
| Create | `Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/RetrievalProfileResolver.cs` | Pure static function: (QueryComplexity, LlmUserTier) → RetrievalProfile |
| Modify | `Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/QueryComplexity.cs` | Add `RecommendedTopK` helper (unused by resolver but useful for logging) |
| Modify | `Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryComplexityClassifier.cs` | Multilingual patterns (IT+EN), structural heuristics, multilingual LLM prompt |
| Modify | `Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs` | Replace const TopK/MinScore with RetrievalProfile from resolver |
| Create | `tests/.../Unit/RetrievalProfileResolverTests.cs` | Test all tier×complexity combinations |
| Modify | `tests/.../Unit/QueryComplexityClassifierTests.cs` | Add IT tests, structural heuristic tests, interaction-pattern tests |
| Modify | `tests/.../Application/Services/RagPromptAssemblyServiceTests.cs` | Test dynamic TopK propagation |

---

### Task 1: RetrievalProfile Value Object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/RetrievalProfile.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RetrievalProfileResolverTests.cs`

- [ ] **Step 1: Write the failing test for RetrievalProfile defaults**

```csharp
// File: apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RetrievalProfileResolverTests.cs
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RetrievalProfileResolverTests
{
    [Fact]
    public void Default_ShouldHaveBaselineValues()
    {
        var profile = RetrievalProfile.Default;

        profile.TopK.Should().Be(5);
        profile.MinScore.Should().Be(0.55f);
        profile.FtsTopK.Should().Be(10);
        profile.WindowRadius.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "RetrievalProfileResolverTests.Default_ShouldHaveBaselineValues" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL — `RetrievalProfile` does not exist.

- [ ] **Step 3: Write RetrievalProfile record**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/RetrievalProfile.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Immutable retrieval parameters resolved from query complexity and user tier.
/// Replaces hardcoded RAG search constants for adaptive retrieval depth.
/// </summary>
internal sealed record RetrievalProfile(
    int TopK,
    float MinScore,
    int FtsTopK,
    int WindowRadius)
{
    /// <summary>Baseline profile matching the original hardcoded constants.</summary>
    public static RetrievalProfile Default => new(TopK: 5, MinScore: 0.55f, FtsTopK: 10, WindowRadius: 1);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "RetrievalProfileResolverTests.Default_ShouldHaveBaselineValues" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/RetrievalProfile.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RetrievalProfileResolverTests.cs
git commit -m "feat(kb): add RetrievalProfile value object for adaptive RAG depth"
```

---

### Task 2: RetrievalProfileResolver — Tier × Complexity Matrix

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/RetrievalProfileResolver.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RetrievalProfileResolverTests.cs`

- [ ] **Step 1: Write failing tests for the resolution matrix**

Append to `RetrievalProfileResolverTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.SharedKernel.Domain.Enums;

// ... inside the class:

// --- Simple queries: always baseline regardless of tier ---

[Theory]
[InlineData(LlmUserTier.Anonymous)]
[InlineData(LlmUserTier.User)]
[InlineData(LlmUserTier.Premium)]
public void Resolve_SimpleQuery_AnyTier_ShouldReturnDefault(LlmUserTier tier)
{
    var complexity = QueryComplexity.Simple("test", 0.9f);

    var profile = RetrievalProfileResolver.Resolve(complexity, tier);

    profile.TopK.Should().Be(5);
    profile.MinScore.Should().Be(0.55f);
}

// --- Moderate queries: scale by tier ---

[Fact]
public void Resolve_ModerateQuery_AnonymousTier_ShouldReturnDefault()
{
    var complexity = QueryComplexity.Moderate("test", 0.8f);

    var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Anonymous);

    profile.TopK.Should().Be(5);
    profile.MinScore.Should().Be(0.55f);
}

[Fact]
public void Resolve_ModerateQuery_UserTier_ShouldScaleUp()
{
    var complexity = QueryComplexity.Moderate("test", 0.8f);

    var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.User);

    profile.TopK.Should().Be(8);
    profile.MinScore.Should().Be(0.50f);
    profile.FtsTopK.Should().Be(15);
}

[Fact]
public void Resolve_ModerateQuery_PremiumTier_ShouldScaleHigher()
{
    var complexity = QueryComplexity.Moderate("test", 0.8f);

    var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Premium);

    profile.TopK.Should().Be(10);
    profile.MinScore.Should().Be(0.45f);
    profile.FtsTopK.Should().Be(20);
}

// --- Complex queries: maximum scaling ---

[Fact]
public void Resolve_ComplexQuery_AnonymousTier_ShouldReturnDefault()
{
    var complexity = QueryComplexity.Complex("test", 0.85f);

    var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Anonymous);

    profile.TopK.Should().Be(5);
    profile.MinScore.Should().Be(0.55f);
}

[Fact]
public void Resolve_ComplexQuery_UserTier_ShouldScaleSignificantly()
{
    var complexity = QueryComplexity.Complex("test", 0.85f);

    var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.User);

    profile.TopK.Should().Be(10);
    profile.MinScore.Should().Be(0.45f);
    profile.FtsTopK.Should().Be(20);
}

[Fact]
public void Resolve_ComplexQuery_PremiumTier_ShouldMaximize()
{
    var complexity = QueryComplexity.Complex("test", 0.85f);

    var profile = RetrievalProfileResolver.Resolve(complexity, LlmUserTier.Premium);

    profile.TopK.Should().Be(15);
    profile.MinScore.Should().Be(0.40f);
    profile.FtsTopK.Should().Be(25);
    profile.WindowRadius.Should().Be(2);
}

// --- Editor and Admin tiers should behave like Premium ---

[Theory]
[InlineData(LlmUserTier.Editor)]
[InlineData(LlmUserTier.Admin)]
public void Resolve_ComplexQuery_ElevatedTiers_ShouldMatchPremium(LlmUserTier tier)
{
    var complexity = QueryComplexity.Complex("test", 0.85f);

    var profile = RetrievalProfileResolver.Resolve(complexity, tier);

    profile.TopK.Should().Be(15);
    profile.MinScore.Should().Be(0.40f);
}

// --- Null tier (unauthenticated) defaults to Anonymous ---

[Fact]
public void Resolve_NullTier_ShouldTreatAsAnonymous()
{
    var complexity = QueryComplexity.Moderate("test", 0.8f);

    var profile = RetrievalProfileResolver.Resolve(complexity, null);

    profile.TopK.Should().Be(5);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test --filter "RetrievalProfileResolverTests" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL — `RetrievalProfileResolver` does not exist.

- [ ] **Step 3: Implement RetrievalProfileResolver**

```csharp
// File: apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/RetrievalProfileResolver.cs
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

/// <summary>
/// Resolves retrieval parameters from query complexity and user tier.
/// Pure static function — no dependencies, no state, fully testable.
/// </summary>
internal static class RetrievalProfileResolver
{
    /// <summary>
    /// Resolve the retrieval profile for a given query complexity and user tier.
    /// Anonymous and Simple queries always get baseline parameters.
    /// Higher tiers + higher complexity = more chunks, lower score threshold.
    /// </summary>
    public static RetrievalProfile Resolve(QueryComplexity complexity, LlmUserTier? tier)
    {
        var effectiveTier = tier ?? LlmUserTier.Anonymous;

        // Simple queries: always baseline (cheap, fast)
        if (complexity.Level == QueryComplexityLevel.Simple)
            return RetrievalProfile.Default;

        // Anonymous: always baseline (limited context window)
        if (effectiveTier == LlmUserTier.Anonymous)
            return RetrievalProfile.Default;

        // Map Editor/Admin to same bucket as Premium (elevated tiers)
        var tierBucket = effectiveTier switch
        {
            LlmUserTier.User => TierBucket.Standard,
            _ => TierBucket.Elevated // Editor, Admin, Premium
        };

        return (complexity.Level, tierBucket) switch
        {
            (QueryComplexityLevel.Moderate, TierBucket.Standard) =>
                new RetrievalProfile(TopK: 8, MinScore: 0.50f, FtsTopK: 15, WindowRadius: 1),

            (QueryComplexityLevel.Moderate, TierBucket.Elevated) =>
                new RetrievalProfile(TopK: 10, MinScore: 0.45f, FtsTopK: 20, WindowRadius: 1),

            (QueryComplexityLevel.Complex, TierBucket.Standard) =>
                new RetrievalProfile(TopK: 10, MinScore: 0.45f, FtsTopK: 20, WindowRadius: 1),

            (QueryComplexityLevel.Complex, TierBucket.Elevated) =>
                new RetrievalProfile(TopK: 15, MinScore: 0.40f, FtsTopK: 25, WindowRadius: 2),

            _ => RetrievalProfile.Default
        };
    }

    private enum TierBucket { Standard, Elevated }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "RetrievalProfileResolverTests" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/RetrievalProfileResolver.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RetrievalProfileResolverTests.cs
git commit -m "feat(kb): add RetrievalProfileResolver — complexity×tier matrix for adaptive RAG"
```

---

### Task 3: Multilingual QueryComplexityClassifier — Heuristics

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryComplexityClassifier.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityClassifierTests.cs`

- [ ] **Step 1: Write failing tests for Italian simple queries**

Append to `QueryComplexityClassifierTests.cs`:

```csharp
// --- Italian heuristics: Simple ---

[Theory]
[InlineData("cos'è Catan?")]
[InlineData("quanti giocatori?")]
[InlineData("quando esce?")]
[InlineData("dove si compra?")]
[InlineData("chi è l'autore?")]
public async Task ClassifyAsync_ItalianShortSimpleQuery_ShouldReturnSimple(string query)
{
    var result = await _sut.ClassifyAsync(query);

    result.Level.Should().Be(QueryComplexityLevel.Simple);
    result.Confidence.Should().BeGreaterOrEqualTo(0.9f);
    _llmServiceMock.VerifyNoOtherCalls();
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test --filter "ClassifyAsync_ItalianShortSimpleQuery" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL — Italian patterns not recognized, falls through to LLM.

- [ ] **Step 3: Write failing tests for Italian complex and interaction queries**

Append to `QueryComplexityClassifierTests.cs`:

```csharp
// --- Italian heuristics: Complex (interaction patterns) ---

[Theory]
[InlineData("se gioco una carta e si attiva un effetto durante il mio turno, come si risolve l'interazione?")]
[InlineData("confronta le strategie e dimmi quale conviene se ho poche risorse")]
[InlineData("quando pesco una carta che innesca un effetto combinato con il turno extra, in che ordine si risolvono?")]
public async Task ClassifyAsync_ItalianComplexInteraction_ShouldReturnComplex(string query)
{
    var result = await _sut.ClassifyAsync(query);

    result.Level.Should().Be(QueryComplexityLevel.Complex);
    result.Confidence.Should().BeGreaterOrEqualTo(0.8f);
    _llmServiceMock.VerifyNoOtherCalls();
}

// --- English interaction patterns (new) ---

[Theory]
[InlineData("if I play a card that triggers an effect during combat, how does the interaction resolve with sustain damage?")]
[InlineData("when I activate this ability and it triggers another effect simultaneously, what is the resolution order?")]
public async Task ClassifyAsync_EnglishInteractionPatterns_ShouldReturnComplex(string query)
{
    var result = await _sut.ClassifyAsync(query);

    result.Level.Should().Be(QueryComplexityLevel.Complex);
    result.Confidence.Should().BeGreaterOrEqualTo(0.8f);
    _llmServiceMock.VerifyNoOtherCalls();
}

// --- Structural complexity (language-agnostic) ---

[Theory]
[InlineData("Se uso la carta azione, pesco 2 carte, una innesca un effetto immediato che modifica i punti, e nel frattempo il turno extra si attiva, come funziona tutto?")]
[InlineData("If I use the action card, draw 2 cards, one triggers an immediate effect that modifies points, and meanwhile the extra turn activates, how does everything work?")]
public async Task ClassifyAsync_LongMultiClauseQuery_ShouldReturnComplex(string query)
{
    var result = await _sut.ClassifyAsync(query);

    result.Level.Should().Be(QueryComplexityLevel.Complex);
    _llmServiceMock.VerifyNoOtherCalls();
}
```

- [ ] **Step 4: Run all new tests to confirm they fail**

Run: `cd apps/api/src/Api && dotnet test --filter "ClassifyAsync_Italian|ClassifyAsync_EnglishInteraction|ClassifyAsync_LongMultiClause" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL

- [ ] **Step 5: Implement multilingual classifier**

Replace the pattern arrays and `ClassifyByHeuristic` method in `QueryComplexityClassifier.cs`:

```csharp
// Replace existing SimplePatterns and ComplexIndicators arrays with:

private static readonly string[] SimplePatterns =
[
    // English
    "what is", "what's", "define", "who is", "who's",
    "how many", "when was", "when did", "where is", "where's",
    // Italian
    "cos'è", "che cos'è", "quanti", "quante", "quando",
    "dove", "chi è", "chi ha"
];

private static readonly string[] ComplexIndicators =
[
    // English — strategy/comparison
    "compare", "versus", "vs", "difference between",
    "pros and cons", "better than", "recommend", "best way",
    // English — rule interactions
    "interaction", "triggers", "resolve", "resolution order",
    "in combination with", "simultaneously", "at the same time",
    "sequence of", "priority", "override", "conflict between",
    // Italian — strategy/comparison
    "confronta", "differenza tra", "meglio", "consiglia",
    "pro e contro", "rispetto a",
    // Italian — rule interactions
    "interazione", "innesca", "si attiva", "si risolve",
    "in combinazione con", "contemporaneamente", "nello stesso momento",
    "in che ordine", "priorità", "sovrascrive", "conflitto tra",
    // Shared conditional patterns (both languages)
    "if", "should i", "can i", "after", "before",
    "se ", "posso", "devo", "durante", "quando"
];

// Replace ClassifyByHeuristic with:

internal static QueryComplexity? ClassifyByHeuristic(string query)
{
    var normalized = query.Trim().ToLowerInvariant();
    var wordCount = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    // Short queries with simple question prefixes -> Simple
    if (wordCount <= 6 && SimplePatterns.Any(p => normalized.StartsWith(p, StringComparison.Ordinal)))
        return QueryComplexity.Simple("Short definitional query", 0.95f);

    // Count complex indicators present in the query
    var complexCount = ComplexIndicators.Count(i =>
        normalized.Contains(i, StringComparison.Ordinal));

    if (complexCount >= 2)
        return QueryComplexity.Complex($"Multiple complex indicators ({complexCount})", 0.85f);

    // Structural complexity: long multi-clause queries (language-agnostic)
    var commaCount = normalized.Count(c => c == ',');
    var hasConditional = normalized.Contains(" se ", StringComparison.Ordinal)
                      || normalized.Contains("if ", StringComparison.Ordinal);

    if (wordCount >= 25 && commaCount >= 3 && hasConditional)
        return QueryComplexity.Complex("Long multi-clause conditional query", 0.80f);

    if (wordCount >= 20 && complexCount >= 1 && commaCount >= 2)
        return QueryComplexity.Complex("Structural complexity with interaction indicator", 0.80f);

    // No strong signal -> null means fall through to LLM
    return null;
}
```

- [ ] **Step 6: Run ALL classifier tests (old + new)**

Run: `cd apps/api/src/Api && dotnet test --filter "QueryComplexityClassifierTests" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS (old English tests still pass, new Italian + structural tests pass)

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryComplexityClassifier.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityClassifierTests.cs
git commit -m "feat(kb): multilingual query classifier — IT+EN patterns + structural heuristics"
```

---

### Task 4: Multilingual LLM Fallback Prompt

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryComplexityClassifier.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityClassifierTests.cs`

- [ ] **Step 1: Write failing test for multilingual LLM prompt**

Append to `QueryComplexityClassifierTests.cs`:

```csharp
[Fact]
public async Task ClassifyAsync_ItalianAmbiguousQuery_ShouldCallLlmWithMultilingualPrompt()
{
    _llmServiceMock
        .Setup(x => x.GenerateJsonAsync<ComplexityClassification>(
            It.Is<string>(s => s.Contains("any language", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ComplexityClassification("Complex", 0.85f, "Multi-step rule interaction"));

    // Italian query that doesn't match heuristics (only 1 complex indicator)
    var result = await _sut.ClassifyAsync("Come funziona il punteggio con l'espansione attiva?");

    result.Level.Should().Be(QueryComplexityLevel.Complex);
    _llmServiceMock.Verify(
        x => x.GenerateJsonAsync<ComplexityClassification>(
            It.Is<string>(s => s.Contains("any language", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(), RequestSource.RagClassification, It.IsAny<CancellationToken>()),
        Times.Once);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "ClassifyAsync_ItalianAmbiguousQuery_ShouldCallLlmWithMultilingualPrompt" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL — prompt doesn't contain "any language".

- [ ] **Step 3: Update LLM prompt to be multilingual**

In `QueryComplexityClassifier.cs`, replace the `systemPrompt` constant in `ClassifyByLlmAsync`:

```csharp
const string systemPrompt = """
    Classify the complexity of the following board-game question.
    The question may be in any language (Italian, English, or others). Classify regardless of language.
    Respond with JSON: {"Level":"Simple|Moderate|Complex","Confidence":0.0-1.0,"Reason":"brief reason"}
    - Simple: single-fact lookup (e.g. "What is the max player count?" / "Quanti giocatori al massimo?")
    - Moderate: needs context retrieval from rules (e.g. "How does scoring work?" / "Come funziona il punteggio?")
    - Complex: multi-step reasoning, rule interactions, comparisons, or strategy advice
      (e.g. "If I play card X and it triggers effect Y during combat, how does it resolve with Z?")
    """;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "ClassifyAsync_ItalianAmbiguousQuery_ShouldCallLlmWithMultilingualPrompt" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: PASS

- [ ] **Step 5: Run ALL classifier tests**

Run: `cd apps/api/src/Api && dotnet test --filter "QueryComplexityClassifierTests" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryComplexityClassifier.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityClassifierTests.cs
git commit -m "feat(kb): multilingual LLM classification prompt — supports IT/EN and future languages"
```

---

### Task 5: Wire RetrievalProfile into RagPromptAssemblyService

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceTests.cs`

- [ ] **Step 1: Write failing test for dynamic TopK in assembly service**

Append to `RagPromptAssemblyServiceTests.cs`:

```csharp
[Fact]
public async Task AssemblePromptAsync_ComplexQuery_PremiumTier_ShouldUseMoreChunks()
{
    // Arrange: enable adaptive routing
    _ragEnhancementMock
        .Setup(r => r.GetActiveEnhancementsAsync(It.IsAny<UserTier>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(RagEnhancement.AdaptiveRouting);

    _complexityClassifierMock
        .Setup(c => c.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(QueryComplexity.Complex("Multi-section rule interaction", 0.85f));

    // Provide 20 chunks from FTS to verify more than 5 are kept
    var manyChunks = Enumerable.Range(0, 20).Select(i => new TextChunkMatch(
        Guid.NewGuid(), $"Chunk {i} text content about game rules section {i}", i, i + 1, 0.7f - (i * 0.01f)
    )).ToList();

    _textSearchMock
        .Setup(t => t.FullTextSearchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(manyChunks);

    _embeddingMock
        .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = [TestEmbedding] });

    _expansionResolverMock
        .Setup(e => e.GetExpansionGameIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<Guid>());

    // Reranker returns all chunks passed to it (no filtering)
    _rerankerMock
        .Setup(r => r.RerankAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<RerankChunk>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((string _, IReadOnlyList<RerankChunk> chunks, int topK, CancellationToken _) =>
            new RerankResult(chunks.Take(topK).Select(c => new RerankedChunk(c.Id, 0.9f, c.OriginalScore)).ToList(), 10));

    var premiumTier = UserTier.Create("premium");

    // Act
    var result = await _sut.AssemblePromptAsync(
        "rules-expert", "Complex Game", null, "If I play X and it triggers Y during Z, how does it resolve?",
        TestGameId, null, premiumTier, "en", CancellationToken.None);

    // Assert: Premium + Complex should retrieve more than default 5 chunks
    result.Citations.Count.Should().BeGreaterThan(5);

    // Verify FTS was called with higher limit than default 10
    _textSearchMock.Verify(t => t.FullTextSearchAsync(
        It.IsAny<Guid>(), It.IsAny<string>(),
        It.Is<int>(limit => limit > 10),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "AssemblePromptAsync_ComplexQuery_PremiumTier_ShouldUseMoreChunks" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: FAIL — still uses hardcoded TopK=5.

- [ ] **Step 3: Integrate RetrievalProfile into RagPromptAssemblyService**

In `RagPromptAssemblyService.cs`, make these changes:

**a) Remove const declarations (lines 39-40, 59, 62):**
```csharp
// DELETE these lines:
// private const int RerankedTopK = 5;
// private const float DefaultMinScore = 0.55f;
// private const int FtsTopK = 10;
```

**b) In `RetrieveRagContextAsync`, after the complexity classification block (~line 210), add profile resolution:**
```csharp
// After: if (!complexity.RequiresRetrieval) { ... return ... }
// Add:
var profile = RetrievalProfileResolver.Resolve(complexity, MapToLlmUserTier(userTier));
_logger.LogInformation("Retrieval profile: TopK={TopK}, MinScore={MinScore:F2}, FtsTopK={FtsTopK} (complexity={Level}, tier={Tier})",
    profile.TopK, profile.MinScore, profile.FtsTopK, complexity.Level, userTier?.Value ?? "anonymous");
```

When adaptive routing is NOT active (no classification), use default:
```csharp
// Before the hybrid search call, declare a local profile variable at method start:
var profile = RetrievalProfile.Default;

// In the adaptive routing block, after classification, overwrite it:
profile = RetrievalProfileResolver.Resolve(complexity, MapToLlmUserTier(userTier));
```

**c) Replace all `RerankedTopK` references with `profile.TopK`:**
- Line ~262: `.Where(r => r.Score >= DefaultMinScore)` → `.Where(r => r.Score >= profile.MinScore)`
- Line ~318: `.Take(RerankedTopK * 2)` → `.Take(profile.TopK * 2)`
- Line ~325: `.Where(c => c.Score >= DefaultMinScore)` → `.Where(c => c.Score >= profile.MinScore)`
- Line ~327: `.Take(RerankedTopK)` → `.Take(profile.TopK)`
- Line ~368: `.Take(RerankedTopK + 2)` → `.Take(profile.TopK + 2)`
- Line ~453-454: `if (chunks.Count <= RerankedTopK)` / `.Take(RerankedTopK)` → `profile.TopK`
- Line ~464: `RerankAsync(..., RerankedTopK, ...)` → `profile.TopK`
- Line ~479: `.Take(RerankedTopK)` → `.Take(profile.TopK)`

**d) Replace `FtsTopK` in `TryHybridSearchAsync`:**
- Line ~488: `FullTextSearchAsync(gameId, userQuestion, FtsTopK, ct)` → pass `profile.FtsTopK`

To make profile available in private methods, change `TryRerankAsync` and `TryHybridSearchAsync` to accept `RetrievalProfile`:

```csharp
private async Task<List<SearchResultItem>> TryRerankAsync(
    string userQuestion, List<SearchResultItem> chunks, RetrievalProfile profile, CancellationToken ct)
{
    if (chunks.Count <= profile.TopK)
        return chunks.Take(profile.TopK).ToList();
    // ... rest uses profile.TopK instead of RerankedTopK
}

private async Task<List<SearchResultItem>> TryHybridSearchAsync(
    string userQuestion, Guid gameId, List<SearchResultItem> vectorChunks, RetrievalProfile profile, CancellationToken ct)
{
    // ... FullTextSearchAsync(gameId, userQuestion, profile.FtsTopK, ct)
}
```

**e) Add tier mapping helper at bottom of class:**
```csharp
private static LlmUserTier MapToLlmUserTier(UserTier? userTier) => userTier?.Value?.ToLowerInvariant() switch
{
    "anonymous" => LlmUserTier.Anonymous,
    "user" => LlmUserTier.User,
    "editor" => LlmUserTier.Editor,
    "admin" => LlmUserTier.Admin,
    "premium" => LlmUserTier.Premium,
    null => LlmUserTier.Anonymous,
    _ => LlmUserTier.User
};
```

- [ ] **Step 4: Run all RAG assembly tests**

Run: `cd apps/api/src/Api && dotnet test --filter "RagPromptAssemblyServiceTests" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS

- [ ] **Step 5: Run ALL KnowledgeBase tests**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=KnowledgeBase" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceTests.cs
git commit -m "feat(kb): wire dynamic RetrievalProfile into RAG pipeline — adaptive TopK by complexity×tier"
```

---

### Task 6: Telemetry — Log Retrieval Profile in Metrics

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`

- [ ] **Step 1: Add retrieval profile metrics after profile resolution**

In `RetrieveRagContextAsync`, after the profile is resolved, add:

```csharp
MeepleAiMetrics.RagAdaptiveRoutingDecisions.Add(1,
    new KeyValuePair<string, object?>("level", complexity.Level.ToString()),
    new KeyValuePair<string, object?>("skipped_retrieval", (!complexity.RequiresRetrieval).ToString()),
    new KeyValuePair<string, object?>("topk", profile.TopK.ToString()),
    new KeyValuePair<string, object?>("min_score", profile.MinScore.ToString("F2")));
```

Also add a debug event for the retrieval profile:

```csharp
debugCollector?.Add(StreamingEventType.DebugAdaptiveRouting,
    new DebugAdaptiveRoutingData(
        ComplexityLevel: complexity.Level.ToString(),
        Confidence: complexity.Confidence,
        Reason: complexity.Reason,
        SkippedRetrieval: !complexity.RequiresRetrieval,
        TopK: profile.TopK,
        MinScore: profile.MinScore));
```

Note: This requires adding `TopK` and `MinScore` optional fields to `DebugAdaptiveRoutingData`. If it's a record, add `int? TopK = null, float? MinScore = null` to the end.

- [ ] **Step 2: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 3: Run full KnowledgeBase test suite**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=KnowledgeBase" ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs
git commit -m "feat(kb): add retrieval profile telemetry — topk and min_score in adaptive routing metrics"
```

---

### Task 7: Final Integration Verification

- [ ] **Step 1: Run full backend test suite**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj`
Expected: ALL PASS (or pre-existing failures only)

- [ ] **Step 2: Verify build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 warnings from changed files

- [ ] **Step 3: Final commit if any adjustments**

```bash
git status
# If clean: no action needed
# If adjustments: git add ... && git commit -m "fix(kb): integration adjustments for dynamic retrieval profile"
```

---

## Summary Matrix

| Complexity \ Tier | Anonymous | User | Editor/Admin/Premium |
|-------------------|-----------|------|----------------------|
| **Simple** | 5 / 0.55 | 5 / 0.55 | 5 / 0.55 |
| **Moderate** | 5 / 0.55 | 8 / 0.50 | 10 / 0.45 |
| **Complex** | 5 / 0.55 | 10 / 0.45 | 15 / 0.40 |

Format: TopK / MinScore. FTS and WindowRadius scale proportionally.

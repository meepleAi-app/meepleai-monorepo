# RAG Enhancements Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Adaptive RAG, CRAG, RAPTOR, RAG-Fusion optimization, and Graph RAG as modular, admin-toggleable enhancements with user-facing cost transparency.

**Architecture:** Each enhancement is a composable module behind a feature flag (`IFeatureFlagService`). Admin toggles control global/tier availability. The existing `RagPromptAssemblyService` pipeline is extended with optional middleware stages. FAST (free) model is used for auxiliary LLM calls by default, with BALANCED as a configurable upgrade.

**Tech Stack:** .NET 9 / C# / MediatR / EF Core / Qdrant / FluentValidation / xUnit + Testcontainers

**Scope Note:** This plan is split into 6 independent chunks. Each chunk produces working, testable software. Chunks 1-3 are P0-P1 (highest value). Chunks 4-6 are P2-P4 (can be deferred).

---

## File Structure

### New Files

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
  Domain/
    Enums/
      RagEnhancement.cs                    # [Flags] enum for enhancement types
    Services/
      Enhancements/
        IQueryComplexityClassifier.cs      # Adaptive RAG: classify query complexity
        QueryComplexityClassifier.cs       # Implementation (heuristic + LLM fallback)
        IRetrievalRelevanceEvaluator.cs    # CRAG: evaluate chunk relevance
        RetrievalRelevanceEvaluator.cs     # Implementation
        IRaptorIndexer.cs                  # RAPTOR: hierarchical summarization
        RaptorIndexer.cs                   # Implementation
        IQueryExpander.cs                  # RAG-Fusion: multi-query generation
        QueryExpander.cs                   # Implementation (FAST model by default)
        IEntityExtractor.cs               # Graph RAG: entity/relation extraction
        EntityExtractor.cs                 # Implementation
    ValueObjects/
      QueryComplexity.cs                   # Simple/Moderate/Complex classification
      RelevanceEvaluation.cs               # Correct/Ambiguous/Incorrect assessment
  Application/
    Commands/
      BuildRaptorTreeCommand.cs            # RAPTOR indexing command
      BuildRaptorTreeCommandHandler.cs     # Handler
      ExtractGameEntitiesCommand.cs        # Graph RAG entity extraction
      ExtractGameEntitiesCommandHandler.cs # Handler
    Services/
      IRagEnhancementService.cs            # Orchestrates active enhancements
      RagEnhancementService.cs             # Implementation
  Infrastructure/
    Persistence/
      RaptorSummaryEntity.cs               # RAPTOR tree node storage
      GameEntityRelationEntity.cs          # Graph RAG entity storage

apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/
  Unit/
    QueryComplexityClassifierTests.cs
    RetrievalRelevanceEvaluatorTests.cs
    RaptorIndexerTests.cs
    QueryExpanderTests.cs
    RagEnhancementServiceTests.cs
  Integration/
    RagEnhancementIntegrationTests.cs
    RaptorIndexingIntegrationTests.cs
```

### Modified Files

```
apps/api/src/Api/BoundedContexts/KnowledgeBase/
  Application/Services/RagPromptAssemblyService.cs   # Add enhancement pipeline hooks
  Application/Commands/AskAgentQuestionCommandHandler.cs  # Integrate enhancements
  Domain/Configuration/DefaultStrategyModelMappings.cs  # Add FAST model for aux calls
  Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs  # Register services

apps/api/src/Api/Services/FeatureFlagService.cs      # Add enhancement flag constants
apps/api/src/Api/Infrastructure/Persistence/AppDbContext.cs  # Add new DbSets
```

---

## Chunk 1: Foundation - RagEnhancement Enum + Feature Flags + Admin Toggles

### Task 1.1: RagEnhancement Flags Enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/RagEnhancement.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagEnhancementTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagEnhancementTests
{
    [Fact]
    public void RagEnhancement_ShouldBeFlagsEnum()
    {
        var combined = RagEnhancement.AdaptiveRouting | RagEnhancement.CragEvaluation;
        Assert.True(combined.HasFlag(RagEnhancement.AdaptiveRouting));
        Assert.True(combined.HasFlag(RagEnhancement.CragEvaluation));
        Assert.False(combined.HasFlag(RagEnhancement.RaptorRetrieval));
    }

    [Fact]
    public void None_ShouldBeZero()
    {
        Assert.Equal(0, (int)RagEnhancement.None);
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, "rag.enhancement.adaptive-routing")]
    [InlineData(RagEnhancement.CragEvaluation, "rag.enhancement.crag-evaluation")]
    [InlineData(RagEnhancement.RaptorRetrieval, "rag.enhancement.raptor-retrieval")]
    [InlineData(RagEnhancement.RagFusionQueries, "rag.enhancement.rag-fusion-queries")]
    [InlineData(RagEnhancement.GraphTraversal, "rag.enhancement.graph-traversal")]
    public void ToFeatureFlagKey_ShouldReturnCorrectKey(
        RagEnhancement enhancement, string expected)
    {
        Assert.Equal(expected, enhancement.ToFeatureFlagKey());
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting, 0)]
    [InlineData(RagEnhancement.CragEvaluation, 40)]
    [InlineData(RagEnhancement.RagFusionQueries, 60)]
    [InlineData(RagEnhancement.RaptorRetrieval, 0)]
    [InlineData(RagEnhancement.GraphTraversal, 0)]
    public void GetExtraCredits_WithBalanced_ShouldReturnCorrectCost(
        RagEnhancement enhancement, int expectedCredits)
    {
        Assert.Equal(expectedCredits, enhancement.GetExtraCredits(useBalancedForAux: true));
    }

    [Theory]
    [InlineData(RagEnhancement.AdaptiveRouting)]
    [InlineData(RagEnhancement.CragEvaluation)]
    [InlineData(RagEnhancement.RagFusionQueries)]
    [InlineData(RagEnhancement.RaptorRetrieval)]
    [InlineData(RagEnhancement.GraphTraversal)]
    public void GetExtraCredits_WithFast_ShouldBeZero(RagEnhancement enhancement)
    {
        Assert.Equal(0, enhancement.GetExtraCredits(useBalancedForAux: false));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RagEnhancementTests" --no-build 2>&1 | head -20`
Expected: Build error - `RagEnhancement` does not exist

- [ ] **Step 3: Write the implementation**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

[Flags]
public enum RagEnhancement
{
    None = 0,
    AdaptiveRouting = 1,      // P0: Query complexity routing (saves tokens)
    CragEvaluation = 2,       // P2: Pre-generation relevance check
    RaptorRetrieval = 4,      // P1: Hierarchical summary retrieval
    RagFusionQueries = 8,     // P3: Multi-query generation with FAST model
    GraphTraversal = 16       // P4: Entity relationship queries
}

public static class RagEnhancementExtensions
{
    private static readonly Dictionary<RagEnhancement, string> FeatureFlagKeys = new()
    {
        [RagEnhancement.AdaptiveRouting] = "rag.enhancement.adaptive-routing",
        [RagEnhancement.CragEvaluation] = "rag.enhancement.crag-evaluation",
        [RagEnhancement.RaptorRetrieval] = "rag.enhancement.raptor-retrieval",
        [RagEnhancement.RagFusionQueries] = "rag.enhancement.rag-fusion-queries",
        [RagEnhancement.GraphTraversal] = "rag.enhancement.graph-traversal",
    };

    private static readonly Dictionary<RagEnhancement, int> BalancedExtraCredits = new()
    {
        [RagEnhancement.AdaptiveRouting] = 0,
        [RagEnhancement.CragEvaluation] = 40,
        [RagEnhancement.RaptorRetrieval] = 0,
        [RagEnhancement.RagFusionQueries] = 60,
        [RagEnhancement.GraphTraversal] = 0,
    };

    public static string ToFeatureFlagKey(this RagEnhancement enhancement)
        => FeatureFlagKeys.TryGetValue(enhancement, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(nameof(enhancement));

    public static int GetExtraCredits(this RagEnhancement enhancement, bool useBalancedForAux)
        => useBalancedForAux && BalancedExtraCredits.TryGetValue(enhancement, out var credits)
            ? credits
            : 0;

    public static IEnumerable<RagEnhancement> GetIndividualFlags(
        this RagEnhancement enhancements)
    {
        foreach (var flag in FeatureFlagKeys.Keys)
        {
            if (enhancements.HasFlag(flag))
                yield return flag;
        }
    }
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~RagEnhancementTests" -v minimal`
Expected: All 14 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/RagEnhancement.cs \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagEnhancementTests.cs
git commit -m "feat(rag): add RagEnhancement flags enum with feature flag keys and cost mapping"
```

### Task 1.2: Feature Flag Constants

**Files:**
- Modify: `apps/api/src/Api/Services/FeatureFlagService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagEnhancementFeatureFlagTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagEnhancementFeatureFlagTests
{
    [Fact]
    public void AllEnhancements_ShouldHaveFeatureFlagConstants()
    {
        var allFlags = new[]
        {
            RagEnhancement.AdaptiveRouting,
            RagEnhancement.CragEvaluation,
            RagEnhancement.RaptorRetrieval,
            RagEnhancement.RagFusionQueries,
            RagEnhancement.GraphTraversal,
        };

        foreach (var flag in allFlags)
        {
            var key = flag.ToFeatureFlagKey();
            Assert.StartsWith("rag.enhancement.", key);
            Assert.Contains(FeatureFlagConstants.RagEnhancements, k => k == key);
        }
    }
}
```

- [ ] **Step 2: Run test - expect fail** (FeatureFlagConstants.RagEnhancements missing)

- [ ] **Step 3: Add constants to FeatureFlagService.cs**

```csharp
public static class FeatureFlagConstants
{
    public static readonly string[] RagEnhancements =
    [
        "rag.enhancement.adaptive-routing",
        "rag.enhancement.crag-evaluation",
        "rag.enhancement.raptor-retrieval",
        "rag.enhancement.rag-fusion-queries",
        "rag.enhancement.graph-traversal",
    ];

    public const string RagAuxModelKey = "rag.enhancement.aux-model";
}
```

- [ ] **Step 4: Run tests - expect pass**
- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Services/FeatureFlagService.cs \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagEnhancementFeatureFlagTests.cs
git commit -m "feat(rag): add feature flag constants for RAG enhancements"
```

### Task 1.3: IRagEnhancementService - Orchestrator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagEnhancementService.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagEnhancementService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagEnhancementServiceTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagEnhancementServiceTests
{
    private readonly Mock<IFeatureFlagService> _featureFlagsMock = new();
    private readonly Mock<ILogger<RagEnhancementService>> _loggerMock = new();
    private readonly RagEnhancementService _sut;

    public RagEnhancementServiceTests()
    {
        _sut = new RagEnhancementService(_featureFlagsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetActiveEnhancements_WhenAllDisabled_ReturnsNone()
    {
        _featureFlagsMock
            .Setup(f => f.IsEnabledForTierAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _sut.GetActiveEnhancementsAsync("Free", CancellationToken.None);

        Assert.Equal(RagEnhancement.None, result);
    }

    [Fact]
    public async Task GetActiveEnhancements_WhenAdaptiveEnabled_ReturnsAdaptive()
    {
        _featureFlagsMock
            .Setup(f => f.IsEnabledForTierAsync("rag.enhancement.adaptive-routing", "Pro"))
            .ReturnsAsync(true);
        _featureFlagsMock
            .Setup(f => f.IsEnabledForTierAsync(
                It.Is<string>(s => s != "rag.enhancement.adaptive-routing"),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _sut.GetActiveEnhancementsAsync("Pro", CancellationToken.None);

        Assert.True(result.HasFlag(RagEnhancement.AdaptiveRouting));
        Assert.False(result.HasFlag(RagEnhancement.CragEvaluation));
    }

    [Fact]
    public async Task GetActiveEnhancements_WhenMultipleEnabled_ReturnsCombined()
    {
        _featureFlagsMock
            .Setup(f => f.IsEnabledForTierAsync("rag.enhancement.adaptive-routing", "Pro"))
            .ReturnsAsync(true);
        _featureFlagsMock
            .Setup(f => f.IsEnabledForTierAsync("rag.enhancement.crag-evaluation", "Pro"))
            .ReturnsAsync(true);
        _featureFlagsMock
            .Setup(f => f.IsEnabledForTierAsync(
                It.Is<string>(s => s != "rag.enhancement.adaptive-routing"
                    && s != "rag.enhancement.crag-evaluation"),
                It.IsAny<string>()))
            .ReturnsAsync(false);

        var result = await _sut.GetActiveEnhancementsAsync("Pro", CancellationToken.None);

        Assert.True(result.HasFlag(RagEnhancement.AdaptiveRouting));
        Assert.True(result.HasFlag(RagEnhancement.CragEvaluation));
    }

    [Fact]
    public async Task EstimateExtraCredits_WithFastAux_ReturnsZero()
    {
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(false);

        var enhancements = RagEnhancement.AdaptiveRouting
            | RagEnhancement.CragEvaluation
            | RagEnhancement.RagFusionQueries;

        var credits = await _sut.EstimateExtraCreditsAsync(enhancements, CancellationToken.None);

        Assert.Equal(0, credits);
    }

    [Fact]
    public async Task EstimateExtraCredits_WithBalancedAux_ReturnsSumOfCosts()
    {
        _featureFlagsMock
            .Setup(f => f.IsEnabledAsync(FeatureFlagConstants.RagAuxModelKey, null))
            .ReturnsAsync(true);

        var enhancements = RagEnhancement.CragEvaluation | RagEnhancement.RagFusionQueries;

        var credits = await _sut.EstimateExtraCreditsAsync(enhancements, CancellationToken.None);

        Assert.Equal(100, credits); // 40 + 60
    }
}
```

- [ ] **Step 2: Run test - expect fail**

- [ ] **Step 3: Write the interface**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal interface IRagEnhancementService
{
    Task<RagEnhancement> GetActiveEnhancementsAsync(string userTier, CancellationToken ct);
    Task<int> EstimateExtraCreditsAsync(RagEnhancement enhancements, CancellationToken ct);
    Task<bool> UseBalancedAuxModelAsync(CancellationToken ct);
}
```

- [ ] **Step 4: Write the implementation**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal sealed class RagEnhancementService : IRagEnhancementService
{
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<RagEnhancementService> _logger;

    public RagEnhancementService(
        IFeatureFlagService featureFlags,
        ILogger<RagEnhancementService> logger)
    {
        _featureFlags = featureFlags;
        _logger = logger;
    }

    public async Task<RagEnhancement> GetActiveEnhancementsAsync(
        string userTier, CancellationToken ct)
    {
        var result = RagEnhancement.None;

        foreach (var enhancement in System.Enum.GetValues<RagEnhancement>())
        {
            if (enhancement == RagEnhancement.None) continue;

            var flagKey = enhancement.ToFeatureFlagKey();
            if (await _featureFlags.IsEnabledForTierAsync(flagKey, userTier))
            {
                result |= enhancement;
            }
        }

        _logger.LogDebug(
            "Active RAG enhancements for tier {Tier}: {Enhancements}",
            userTier, result);
        return result;
    }

    public async Task<int> EstimateExtraCreditsAsync(
        RagEnhancement enhancements, CancellationToken ct)
    {
        var useBalanced = await UseBalancedAuxModelAsync(ct);
        return enhancements
            .GetIndividualFlags()
            .Sum(e => e.GetExtraCredits(useBalanced));
    }

    public async Task<bool> UseBalancedAuxModelAsync(CancellationToken ct)
    {
        return await _featureFlags.IsEnabledAsync(
            FeatureFlagConstants.RagAuxModelKey, null);
    }
}
```

- [ ] **Step 5: Run tests - expect pass**
- [ ] **Step 6: Register in DI** (KnowledgeBaseServiceExtensions.cs)

```csharp
services.AddScoped<IRagEnhancementService, RagEnhancementService>();
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/IRagEnhancementService.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagEnhancementService.cs \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RagEnhancementServiceTests.cs \
       apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs
git commit -m "feat(rag): add IRagEnhancementService orchestrator with feature flag integration"
```

---

## Chunk 2: Adaptive RAG - Query Complexity Classifier

### Task 2.1: QueryComplexity Value Object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/QueryComplexity.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class QueryComplexityTests
{
    [Theory]
    [InlineData(QueryComplexityLevel.Simple, false, true)]
    [InlineData(QueryComplexityLevel.Moderate, true, false)]
    [InlineData(QueryComplexityLevel.Complex, true, false)]
    public void QueryComplexity_ShouldHaveCorrectBehavior(
        QueryComplexityLevel level, bool requiresRetrieval, bool canDowngrade)
    {
        var qc = new QueryComplexity(level, 0.85f, "test reason");
        Assert.Equal(requiresRetrieval, qc.RequiresRetrieval);
        Assert.Equal(canDowngrade, qc.CanDowngradeToFast);
    }

    [Fact]
    public void Simple_ShouldSkipRetrieval()
    {
        var qc = QueryComplexity.Simple("Definition query", 0.92f);
        Assert.Equal(QueryComplexityLevel.Simple, qc.Level);
        Assert.False(qc.RequiresRetrieval);
        Assert.True(qc.CanDowngradeToFast);
    }

    [Fact]
    public void Complex_ShouldRequireMultiStep()
    {
        var qc = QueryComplexity.Complex("Multi-hop reasoning", 0.88f);
        Assert.Equal(QueryComplexityLevel.Complex, qc.Level);
        Assert.True(qc.RequiresRetrieval);
        Assert.True(qc.RequiresMultiStep);
    }
}
```

- [ ] **Step 2: Run test - expect fail**
- [ ] **Step 3: Implement**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

public enum QueryComplexityLevel
{
    Simple = 0,     // LLM-only, no retrieval needed
    Moderate = 1,   // Single retrieval pass
    Complex = 2     // Multi-step retrieval
}

public sealed record QueryComplexity(
    QueryComplexityLevel Level,
    float Confidence,
    string Reason)
{
    public bool RequiresRetrieval => Level != QueryComplexityLevel.Simple;
    public bool RequiresMultiStep => Level == QueryComplexityLevel.Complex;
    public bool CanDowngradeToFast => Level == QueryComplexityLevel.Simple;

    public static QueryComplexity Simple(string reason, float confidence)
        => new(QueryComplexityLevel.Simple, confidence, reason);

    public static QueryComplexity Moderate(string reason, float confidence)
        => new(QueryComplexityLevel.Moderate, confidence, reason);

    public static QueryComplexity Complex(string reason, float confidence)
        => new(QueryComplexityLevel.Complex, confidence, reason);
}
```

- [ ] **Step 4: Run tests - expect pass**
- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/QueryComplexity.cs \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityTests.cs
git commit -m "feat(rag): add QueryComplexity value object for adaptive routing"
```

### Task 2.2: QueryComplexityClassifier

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/IQueryComplexityClassifier.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryComplexityClassifier.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityClassifierTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class QueryComplexityClassifierTests
{
    private readonly Mock<ILlmService> _llmMock = new();
    private readonly Mock<ILogger<QueryComplexityClassifier>> _loggerMock = new();
    private readonly QueryComplexityClassifier _sut;

    public QueryComplexityClassifierTests()
    {
        _sut = new QueryComplexityClassifier(_llmMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData("What is Catan?")]
    [InlineData("Define worker placement")]
    public async Task Classify_SimpleQueries_ReturnsSimpleWithoutLlm(string query)
    {
        var result = await _sut.ClassifyAsync(query, CancellationToken.None);

        Assert.Equal(QueryComplexityLevel.Simple, result.Level);
        Assert.True(result.Confidence >= 0.8f);
        _llmMock.Verify(l => l.GenerateJsonAsync<ComplexityClassification>(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Classify_AmbiguousQuery_FallsBackToLlm()
    {
        var query = "How does combat interact with retreating in the expansion?";

        _llmMock
            .Setup(l => l.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplexityClassification(
                "complex", 0.85f, "multi-hop reasoning"));

        var result = await _sut.ClassifyAsync(query, CancellationToken.None);

        Assert.Equal(QueryComplexityLevel.Complex, result.Level);
    }

    [Fact]
    public async Task Classify_WhenLlmFails_DefaultsToModerate()
    {
        var query = "Something ambiguous enough to need LLM classification";

        _llmMock
            .Setup(l => l.GenerateJsonAsync<ComplexityClassification>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("LLM unavailable"));

        var result = await _sut.ClassifyAsync(query, CancellationToken.None);

        Assert.Equal(QueryComplexityLevel.Moderate, result.Level);
    }
}
```

- [ ] **Step 2: Run test - expect fail**

- [ ] **Step 3: Write the interface**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal interface IQueryComplexityClassifier
{
    Task<QueryComplexity> ClassifyAsync(string query, CancellationToken ct);
}

internal sealed record ComplexityClassification(
    string Level, float Confidence, string Reason);
```

- [ ] **Step 4: Write the implementation**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;

internal sealed class QueryComplexityClassifier : IQueryComplexityClassifier
{
    private readonly ILlmService _llmService;
    private readonly ILogger<QueryComplexityClassifier> _logger;

    private static readonly string[] SimplePatterns =
    [
        "what is", "what are", "define", "who is", "when was", "how many",
    ];

    private static readonly string[] ComplexIndicators =
    [
        "after", "before", "if", "when", "while", "during",
        "expansion", "interact", "combine", "together",
        "can i", "is it possible",
    ];

    private const string ClassificationPrompt =
        "Classify this board game question complexity as one of:\n"
        + "- \"simple\": definitional, single-fact (e.g. \"What is Catan?\")\n"
        + "- \"moderate\": requires rule lookup (e.g. \"How does trading work?\")\n"
        + "- \"complex\": multi-step reasoning (e.g. \"Can I trade after building if...\")\n\n"
        + "Respond ONLY with JSON: "
        + "{\"level\":\"simple|moderate|complex\",\"confidence\":0.0-1.0,\"reason\":\"brief\"}\n\n"
        + "Question: {0}";

    public QueryComplexityClassifier(
        ILlmService llmService,
        ILogger<QueryComplexityClassifier> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<QueryComplexity> ClassifyAsync(string query, CancellationToken ct)
    {
        var heuristic = TryHeuristicClassification(query);
        if (heuristic is not null)
        {
            _logger.LogDebug("Heuristic classification: {Level} for query: {Query}",
                heuristic.Level, query[..Math.Min(50, query.Length)]);
            return heuristic;
        }

        try
        {
            var prompt = string.Format(ClassificationPrompt, query);
            var result = await _llmService.GenerateJsonAsync<ComplexityClassification>(
                "You are a query complexity classifier.",
                prompt, RequestSource.Internal, ct);

            if (result is not null)
            {
                var level = result.Level.ToLowerInvariant() switch
                {
                    "simple" => QueryComplexityLevel.Simple,
                    "complex" => QueryComplexityLevel.Complex,
                    _ => QueryComplexityLevel.Moderate,
                };
                return new QueryComplexity(level, result.Confidence, result.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM classification failed, defaulting to Moderate");
        }

        return QueryComplexity.Moderate("LLM classification unavailable", 0.5f);
    }

    private static QueryComplexity? TryHeuristicClassification(string query)
    {
        var lower = query.ToLowerInvariant().Trim();

        if (lower.Length < 40 && SimplePatterns.Any(p => lower.StartsWith(p)))
            return QueryComplexity.Simple("Short definitional query", 0.90f);

        var complexCount = ComplexIndicators.Count(i => lower.Contains(i));
        if (complexCount >= 2)
            return QueryComplexity.Complex(
                $"{complexCount} complexity indicators detected", 0.80f);

        return null;
    }
}
```

- [ ] **Step 5: Run tests - expect pass**
- [ ] **Step 6: Register in DI, commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/ \
       apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryComplexityClassifierTests.cs
git commit -m "feat(rag): add adaptive query complexity classifier with heuristic + LLM"
```

### Task 2.3: Integrate Adaptive RAG into Pipeline

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`

- [ ] **Step 1: Add adaptive routing hook before retrieval**

In `RagPromptAssemblyService.RetrieveRagContextAsync()`, add before the hybrid search:

```csharp
// === ADAPTIVE RAG ENHANCEMENT ===
if (activeEnhancements.HasFlag(RagEnhancement.AdaptiveRouting))
{
    var complexity = await _queryComplexityClassifier.ClassifyAsync(query, ct);
    _logger.LogInformation(
        "Adaptive RAG: {Level} (confidence: {Confidence})",
        complexity.Level, complexity.Confidence);

    if (!complexity.RequiresRetrieval)
    {
        return new RagContext(
            Chunks: [],
            Complexity: complexity,
            SkippedRetrieval: true);
    }
}
```

- [ ] **Step 2: Run full KnowledgeBase test suite**

Run: `cd apps/api && dotnet test --filter "BoundedContext=KnowledgeBase" -v minimal`
Expected: All existing + new tests PASS

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(rag): integrate adaptive routing into retrieval pipeline"
```

---

## Chunk 3: CRAG - Corrective Retrieval Evaluation

### Task 3.1: RelevanceEvaluation Value Object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/ValueObjects/RelevanceEvaluation.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RelevanceEvaluationTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

[Trait("Category", "Unit")]
public class RelevanceEvaluationTests
{
    [Theory]
    [InlineData(RelevanceVerdict.Correct, true, false)]
    [InlineData(RelevanceVerdict.Ambiguous, true, true)]
    [InlineData(RelevanceVerdict.Incorrect, false, true)]
    public void Verdict_ShouldDetermineActions(
        RelevanceVerdict verdict, bool useDocs, bool shouldRequery)
    {
        var result = new RelevanceEvaluation(verdict, 0.7f, "test");
        Assert.Equal(useDocs, result.UseRetrievedDocuments);
        Assert.Equal(shouldRequery, result.ShouldRequery);
    }
}
```

- [ ] **Step 2: Implement**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

public enum RelevanceVerdict
{
    Correct = 0,
    Ambiguous = 1,
    Incorrect = 2
}

public sealed record RelevanceEvaluation(
    RelevanceVerdict Verdict,
    float Confidence,
    string Reason)
{
    public bool UseRetrievedDocuments => Verdict != RelevanceVerdict.Incorrect;
    public bool ShouldRequery => Verdict != RelevanceVerdict.Correct;
}
```

- [ ] **Step 3: Run tests, commit**

```bash
git commit -m "feat(rag): add RelevanceEvaluation value object for CRAG"
```

### Task 3.2: RetrievalRelevanceEvaluator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/IRetrievalRelevanceEvaluator.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/RetrievalRelevanceEvaluator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/RetrievalRelevanceEvaluatorTests.cs`

- [ ] **Step 1: Write the test**

```csharp
[Trait("Category", "Unit")]
public class RetrievalRelevanceEvaluatorTests
{
    private readonly Mock<ILlmService> _llmMock = new();
    private readonly RetrievalRelevanceEvaluator _sut;

    public RetrievalRelevanceEvaluatorTests()
    {
        _sut = new RetrievalRelevanceEvaluator(
            _llmMock.Object,
            Mock.Of<ILogger<RetrievalRelevanceEvaluator>>());
    }

    [Fact]
    public async Task HighScoreChunks_ReturnsCorrectWithoutLlm()
    {
        var chunks = new List<ScoredChunk>
        {
            new("c1", "Catan trading rules...", 0.92f),
            new("c2", "Trading phase details...", 0.88f),
        };

        var result = await _sut.EvaluateAsync(
            "How does trading work?", chunks, CancellationToken.None);

        Assert.Equal(RelevanceVerdict.Correct, result.Verdict);
        _llmMock.Verify(l => l.GenerateJsonAsync<RelevanceClassification>(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LowScoreChunks_UsesLlmEvaluation()
    {
        var chunks = new List<ScoredChunk>
        {
            new("c1", "Unrelated content about setup...", 0.55f),
        };

        _llmMock
            .Setup(l => l.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceClassification(
                "incorrect", 0.80f, "chunks about setup, not trading"));

        var result = await _sut.EvaluateAsync(
            "How does trading work?", chunks, CancellationToken.None);

        Assert.Equal(RelevanceVerdict.Incorrect, result.Verdict);
    }

    [Fact]
    public async Task WhenLlmFails_DefaultsToCorrect()
    {
        var chunks = new List<ScoredChunk> { new("c1", "some text", 0.65f) };

        _llmMock
            .Setup(l => l.GenerateJsonAsync<RelevanceClassification>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timeout"));

        var result = await _sut.EvaluateAsync(
            "query", chunks, CancellationToken.None);

        Assert.Equal(RelevanceVerdict.Correct, result.Verdict);
    }
}
```

- [ ] **Step 2: Implement**

Two-tier evaluation: heuristic (reranker scores) then LLM for borderline cases.
- avgScore >= 0.85 -> Correct (no LLM call)
- avgScore < 0.55 -> Incorrect (no LLM call)
- 0.55-0.85 -> LLM classification
- LLM failure -> fallback to Correct (safe default: use what was retrieved)

```csharp
internal sealed record ScoredChunk(string Id, string Text, float Score);
internal sealed record RelevanceClassification(
    string Verdict, float Confidence, string Reason);

internal interface IRetrievalRelevanceEvaluator
{
    Task<RelevanceEvaluation> EvaluateAsync(
        string query,
        IReadOnlyList<ScoredChunk> chunks,
        CancellationToken ct);
}

internal sealed class RetrievalRelevanceEvaluator : IRetrievalRelevanceEvaluator
{
    private readonly ILlmService _llmService;
    private readonly ILogger<RetrievalRelevanceEvaluator> _logger;
    private const float HighThreshold = 0.85f;
    private const float LowThreshold = 0.55f;

    private const string Prompt =
        "Given this question and retrieved chunks, classify relevance:\n"
        + "- \"correct\": chunks directly answer the question\n"
        + "- \"ambiguous\": partially relevant, need supplementation\n"
        + "- \"incorrect\": not about the topic at all\n\n"
        + "Respond ONLY JSON: "
        + "{\"verdict\":\"correct|ambiguous|incorrect\","
        + "\"confidence\":0.0-1.0,\"reason\":\"brief\"}\n\n"
        + "Question: {0}\n\nChunks:\n{1}";

    public RetrievalRelevanceEvaluator(
        ILlmService llmService,
        ILogger<RetrievalRelevanceEvaluator> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<RelevanceEvaluation> EvaluateAsync(
        string query,
        IReadOnlyList<ScoredChunk> chunks,
        CancellationToken ct)
    {
        if (chunks.Count == 0)
            return new RelevanceEvaluation(
                RelevanceVerdict.Incorrect, 1.0f, "No chunks retrieved");

        var avgScore = chunks.Average(c => c.Score);

        if (avgScore >= HighThreshold)
            return new RelevanceEvaluation(
                RelevanceVerdict.Correct, avgScore, "High reranker scores");

        if (avgScore < LowThreshold)
            return new RelevanceEvaluation(
                RelevanceVerdict.Incorrect, avgScore, "Very low reranker scores");

        try
        {
            var chunksText = string.Join("\n---\n",
                chunks.Select(c => c.Text[..Math.Min(200, c.Text.Length)]));
            var prompt = string.Format(Prompt, query, chunksText);

            var result = await _llmService.GenerateJsonAsync<RelevanceClassification>(
                "You are a retrieval quality evaluator.",
                prompt, RequestSource.Internal, ct);

            if (result is not null)
            {
                var verdict = result.Verdict.ToLowerInvariant() switch
                {
                    "incorrect" => RelevanceVerdict.Incorrect,
                    "ambiguous" => RelevanceVerdict.Ambiguous,
                    _ => RelevanceVerdict.Correct,
                };
                return new RelevanceEvaluation(
                    verdict, result.Confidence, result.Reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CRAG evaluation failed, defaulting to Correct");
        }

        return new RelevanceEvaluation(
            RelevanceVerdict.Correct, avgScore,
            "LLM evaluation unavailable");
    }
}
```

- [ ] **Step 3: Run tests, register in DI, commit**

```bash
git commit -m "feat(rag): add CRAG retrieval relevance evaluator"
```

### Task 3.3: Integrate CRAG into Pipeline

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`

- [ ] **Step 1: Add CRAG hook after retrieval, before context assembly**

```csharp
// === CRAG ENHANCEMENT ===
if (activeEnhancements.HasFlag(RagEnhancement.CragEvaluation)
    && retrievedChunks.Count > 0)
{
    var scoredChunks = retrievedChunks
        .Select(c => new ScoredChunk(c.Id, c.Text, c.Score))
        .ToList();

    var relevance = await _relevanceEvaluator.EvaluateAsync(
        query, scoredChunks, ct);

    if (relevance.ShouldRequery)
    {
        _logger.LogInformation(
            "CRAG: {Verdict} - expanding retrieval", relevance.Verdict);
        var expanded = await ExpandRetrievalAsync(query, ct);

        retrievedChunks = relevance.UseRetrievedDocuments
            ? MergeChunks(retrievedChunks, expanded)
            : expanded;
    }
}
```

- [ ] **Step 2: Run tests, commit**

```bash
git commit -m "feat(rag): integrate CRAG evaluation into retrieval pipeline"
```

---

## Chunk 4: RAG-Fusion Optimization

### Task 4.1: QueryExpander with FAST Model

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/IQueryExpander.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/QueryExpander.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/QueryExpanderTests.cs`

- [ ] **Step 1: Write the test**

```csharp
[Trait("Category", "Unit")]
public class QueryExpanderTests
{
    private readonly Mock<ILlmService> _llmMock = new();
    private readonly QueryExpander _sut;

    public QueryExpanderTests()
    {
        _sut = new QueryExpander(
            _llmMock.Object,
            Mock.Of<ILogger<QueryExpander>>());
    }

    [Fact]
    public async Task Expand_ShouldGenerate3To4Variants()
    {
        _llmMock
            .Setup(l => l.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryVariations([
                "Catan winning strategies",
                "How to get victory points in Catan",
                "Catan endgame tactics"
            ]));

        var result = await _sut.ExpandAsync(
            "How do I win at Catan?", CancellationToken.None);

        Assert.InRange(result.Count, 3, 5);
        Assert.Contains("How do I win at Catan?", result);
    }

    [Fact]
    public async Task Expand_WhenLlmFails_ReturnsOriginalOnly()
    {
        _llmMock
            .Setup(l => l.GenerateJsonAsync<QueryVariations>(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timeout"));

        var result = await _sut.ExpandAsync(
            "How do I win?", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("How do I win?", result[0]);
    }
}
```

- [ ] **Step 2: Implement**

Key: uses FAST model (free) for query generation instead of user's strategy model.

```csharp
internal sealed record QueryVariations(List<string> Queries);

internal interface IQueryExpander
{
    Task<List<string>> ExpandAsync(string query, CancellationToken ct);
}

internal sealed class QueryExpander : IQueryExpander
{
    private readonly ILlmService _llmService;
    private readonly ILogger<QueryExpander> _logger;

    private const string Prompt =
        "Generate 3 alternative phrasings of this board game question.\n"
        + "Each should capture a different angle or terminology.\n"
        + "Respond ONLY JSON: {\"queries\":[\"v1\",\"v2\",\"v3\"]}\n\n"
        + "Original: {0}";

    public QueryExpander(ILlmService llmService, ILogger<QueryExpander> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<List<string>> ExpandAsync(string query, CancellationToken ct)
    {
        var result = new List<string> { query };

        try
        {
            var prompt = string.Format(Prompt, query);
            var variations = await _llmService.GenerateJsonAsync<QueryVariations>(
                "You generate search query variations.",
                prompt, RequestSource.Internal, ct);

            if (variations?.Queries is { Count: > 0 })
                result.AddRange(variations.Queries.Take(4));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query expansion failed, using original only");
        }

        return result;
    }
}
```

- [ ] **Step 3: Integrate into pipeline** - parallel retrieval for each variant, merge with existing RRF
- [ ] **Step 4: Run tests, commit**

```bash
git commit -m "feat(rag): add FAST-model query expander for cost-optimized RAG-Fusion"
```

---

## Chunk 5: RAPTOR - Hierarchical Indexing

### Task 5.1: RaptorSummaryEntity + Migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/RaptorSummaryEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Persistence/AppDbContext.cs`

- [ ] **Step 1: Define entity**

```csharp
public sealed class RaptorSummaryEntity
{
    public Guid Id { get; private set; }
    public Guid PdfDocumentId { get; private set; }
    public Guid GameId { get; private set; }
    public int TreeLevel { get; private set; }       // 0=leaf, 1=section, 2=overview
    public int ClusterIndex { get; private set; }
    public string SummaryText { get; private set; } = string.Empty;
    public int SourceChunkCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static RaptorSummaryEntity Create(
        Guid pdfDocumentId, Guid gameId, int treeLevel,
        int clusterIndex, string summaryText, int sourceChunkCount)
        => new()
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfDocumentId,
            GameId = gameId,
            TreeLevel = treeLevel,
            ClusterIndex = clusterIndex,
            SummaryText = summaryText,
            SourceChunkCount = sourceChunkCount,
            CreatedAt = DateTime.UtcNow,
        };
}
```

- [ ] **Step 2: Add DbSet, create migration**

```bash
cd apps/api/src/Api && dotnet ef migrations add AddRaptorSummaries
```

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(rag): add RAPTOR summary entity and migration"
```

### Task 5.2: RaptorIndexer Service

- [ ] **Step 1: Write tests for clustering + summarization**
- [ ] **Step 2: Implement IRaptorIndexer** (cluster chunks by embedding similarity, summarize clusters, recurse)
- [ ] **Step 3: Add BuildRaptorTreeCommand + Handler (MediatR)**
- [ ] **Step 4: Hook into PdfProcessingPipelineService** (optional step after embedding)
- [ ] **Step 5: Modify HybridSearchEngine** to search across RAPTOR levels when enhancement active
- [ ] **Step 6: Run tests, commit**

```bash
git commit -m "feat(rag): add RAPTOR hierarchical indexer with multi-level retrieval"
```

---

## Chunk 6: Graph RAG - Entity Extraction + Traversal

### Task 6.1: GameEntityRelationEntity + Migration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/GameEntityRelationEntity.cs`

- [ ] **Step 1: Define entity**

```csharp
public sealed class GameEntityRelationEntity
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public string SourceEntity { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public string Relation { get; private set; } = string.Empty;
    public string TargetEntity { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public float Confidence { get; private set; }
    public DateTime ExtractedAt { get; private set; }

    public static GameEntityRelationEntity Create(
        Guid gameId, string sourceEntity, string sourceType,
        string relation, string targetEntity, string targetType,
        float confidence)
        => new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            SourceEntity = sourceEntity,
            SourceType = sourceType,
            Relation = relation,
            TargetEntity = targetEntity,
            TargetType = targetType,
            Confidence = confidence,
            ExtractedAt = DateTime.UtcNow,
        };
}
```

- [ ] **Step 2: Migration, commit**

### Task 6.2: EntityExtractor + Graph Traversal

- [ ] **Step 1: Implement LLM-based entity/relation extraction**
- [ ] **Step 2: Build graph traversal queries** (SQL-based, no Neo4j)
- [ ] **Step 3: Integrate as optional retrieval source**
- [ ] **Step 4: Tests, commit**

```bash
git commit -m "feat(rag): add Graph RAG entity extraction and traversal"
```

---

## Admin Endpoints (Cross-Cutting, after Chunks 1-6)

### Task A.1: GET /admin/rag-enhancements

```csharp
app.MapGet("/api/v1/admin/rag-enhancements", async (
    IRagEnhancementService enhancementService,
    IFeatureFlagService featureFlags,
    CancellationToken ct) =>
{
    var result = new List<RagEnhancementStatusDto>();
    foreach (var enhancement in Enum.GetValues<RagEnhancement>())
    {
        if (enhancement == RagEnhancement.None) continue;
        var flagKey = enhancement.ToFeatureFlagKey();
        var isEnabled = await featureFlags.IsEnabledAsync(flagKey, null);
        result.Add(new RagEnhancementStatusDto(
            enhancement.ToString(),
            flagKey,
            isEnabled,
            enhancement.GetExtraCredits(useBalancedForAux: true),
            enhancement.GetExtraCredits(useBalancedForAux: false)));
    }
    return Results.Ok(result);
}).RequireAuthorization("Admin");
```

### Task A.2: POST /admin/rag-enhancements/{key}/toggle
### Task A.3: POST /admin/rag-enhancements/{key}/tier/{tier}/toggle
### Task A.4: GET /api/v1/rag/enhancements/estimate (user-facing cost preview)

---

## Implementation Priority

| Order | Chunk | Enhancement | Net Cost | Accuracy Gain |
|-------|-------|-------------|----------|---------------|
| 1 | Chunk 1 | Foundation (enum + flags + service) | 0 | Enables all |
| 2 | Chunk 2 | Adaptive RAG | -cost (saves) | +7-10% |
| 3 | Chunk 3 | CRAG | 0 (FAST) | +3-5% |
| 4 | Chunk 4 | RAG-Fusion optimization | 0 (FAST) | Existing cheaper |
| 5 | Chunk 5 | RAPTOR | 0.50 once | +5-8% broad |
| 6 | Chunk 6 | Graph RAG | 0 (self-hosted) | New query types |

## Testing Strategy

- **Unit tests**: Each service + value object (per chunk)
- **Integration tests**: Full pipeline with feature flags toggled
- **Golden dataset**: Extend GoldenDatasetAccuracyIntegrationTests
- **A/B metrics**: Log RagEnhancement flags per query for comparison

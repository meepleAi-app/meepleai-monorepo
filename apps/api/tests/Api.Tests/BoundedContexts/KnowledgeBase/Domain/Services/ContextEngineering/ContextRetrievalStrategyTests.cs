using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Unit tests for context retrieval strategies.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "ContextEngineering")]
public class ContextRetrievalStrategyTests
{
    #region TemporalScoringStrategy Tests

    [Fact]
    public void TemporalScoringStrategy_Constructor_WithValidWeights_ShouldSucceed()
    {
        // Arrange & Act
        var strategy = new TemporalScoringStrategy(
            recencyWeight: 0.4,
            relevanceWeight: 0.6);

        // Assert
        strategy.StrategyId.Should().Be("temporal_scoring");
        strategy.SupportedSourceTypes.Should().Contain("conversation_memory");
    }

    [Fact]
    public void TemporalScoringStrategy_Constructor_WithInvalidWeights_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new TemporalScoringStrategy(
            recencyWeight: 0.5,
            relevanceWeight: 0.6); // Sum = 1.1

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("recencyWeight");
    }

    [Fact]
    public void TemporalScoringStrategy_Apply_ShouldScoreRecentItemsHigher()
    {
        // Arrange
        var strategy = new TemporalScoringStrategy(
            recencyWeight: 0.3,
            relevanceWeight: 0.7);

        var now = DateTime.UtcNow;
        var items = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "Recent", Relevance = 0.8, Timestamp = now.AddMinutes(-5), TokenCount = 10, ContentType = "message" },
            new() { Id = "2", Content = "Old", Relevance = 0.8, Timestamp = now.AddHours(-20), TokenCount = 10, ContentType = "message" }
        };

        var context = new StrategyContext
        {
            Query = "test",
            ReferenceTime = now,
            MaxItems = 10
        };

        // Act
        var result = strategy.Apply(items, context);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("1"); // Recent item should be first
        result[0].Relevance.Should().BeGreaterThan(result[1].Relevance);
    }

    [Fact]
    public void TemporalScoringStrategy_Apply_ShouldFilterByMinScore()
    {
        // Arrange
        var strategy = new TemporalScoringStrategy();
        var now = DateTime.UtcNow;
        var items = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "High", Relevance = 0.9, Timestamp = now.AddMinutes(-5), TokenCount = 10, ContentType = "message" },
            new() { Id = "2", Content = "Low", Relevance = 0.1, Timestamp = now.AddDays(-30), TokenCount = 10, ContentType = "message" }
        };

        var context = new StrategyContext
        {
            Query = "test",
            ReferenceTime = now,
            MinScore = 0.5,
            MaxItems = 10
        };

        // Act
        var result = strategy.Apply(items, context);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
    }

    [Fact]
    public void TemporalScoringStrategy_Apply_ShouldRespectMaxItems()
    {
        // Arrange
        var strategy = new TemporalScoringStrategy();
        var now = DateTime.UtcNow;
        var items = Enumerable.Range(1, 20)
            .Select(i => new RetrievedContextItem
            {
                Id = i.ToString(),
                Content = $"Item {i}",
                Relevance = 0.9,
                Timestamp = now.AddMinutes(-i),
                TokenCount = 10,
                ContentType = "message"
            })
            .ToList();

        var context = new StrategyContext
        {
            Query = "test",
            ReferenceTime = now,
            MaxItems = 5
        };

        // Act
        var result = strategy.Apply(items, context);

        // Assert
        result.Should().HaveCount(5);
    }

    #endregion

    #region PositionSimilarityStrategy Tests

    [Fact]
    public void PositionSimilarityStrategy_Constructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var strategy = new PositionSimilarityStrategy();

        // Assert
        strategy.StrategyId.Should().Be("position_similarity");
        strategy.SupportedSourceTypes.Should().Contain("game_state");
    }

    [Fact]
    public void PositionSimilarityStrategy_Apply_ShouldFilterBySimilarityThreshold()
    {
        // Arrange
        var strategy = new PositionSimilarityStrategy(similarityThreshold: 0.7);
        var items = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "High", Relevance = 0.9, TokenCount = 10, ContentType = "state" },
            new() { Id = "2", Content = "Low", Relevance = 0.5, TokenCount = 10, ContentType = "state" }
        };

        var context = new StrategyContext
        {
            Query = "test",
            MaxItems = 10
        };

        // Act
        var result = strategy.Apply(items, context);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("1");
    }

    #endregion

    #region HybridSearchStrategy Tests

    [Fact]
    public void HybridSearchStrategy_Constructor_WithValidWeights_ShouldSucceed()
    {
        // Arrange & Act
        var strategy = new HybridSearchStrategy(
            semanticWeight: 0.6,
            keywordWeight: 0.4);

        // Assert
        strategy.StrategyId.Should().Be("hybrid_search");
        strategy.SupportedSourceTypes.Should().Contain("static_knowledge");
        strategy.SupportedSourceTypes.Should().Contain("rules");
        strategy.SemanticWeight.Should().Be(0.6);
        strategy.KeywordWeight.Should().Be(0.4);
    }

    [Fact]
    public void HybridSearchStrategy_Constructor_WithInvalidWeights_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new HybridSearchStrategy(
            semanticWeight: 0.8,
            keywordWeight: 0.4); // Sum = 1.2

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("semanticWeight");
    }

    [Fact]
    public void HybridSearchStrategy_Apply_ShouldOrderByRelevance()
    {
        // Arrange
        var strategy = new HybridSearchStrategy();
        var items = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "Low", Relevance = 0.5, TokenCount = 10, ContentType = "knowledge" },
            new() { Id = "2", Content = "High", Relevance = 0.9, TokenCount = 10, ContentType = "knowledge" }
        };

        var context = new StrategyContext
        {
            Query = "test",
            MaxItems = 10
        };

        // Act
        var result = strategy.Apply(items, context);

        // Assert
        result[0].Id.Should().Be("2");
        result[1].Id.Should().Be("1");
    }

    #endregion

    #region CapabilityMatchingStrategy Tests

    [Fact]
    public void CapabilityMatchingStrategy_Constructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var strategy = new CapabilityMatchingStrategy();

        // Assert
        strategy.StrategyId.Should().Be("capability_matching");
        strategy.SupportedSourceTypes.Should().Contain("tool_metadata");
        strategy.SupportedSourceTypes.Should().Contain("actions");
    }

    [Fact]
    public void CapabilityMatchingStrategy_Apply_ShouldFilterAndOrderByRelevance()
    {
        // Arrange
        var strategy = new CapabilityMatchingStrategy();
        var items = new List<RetrievedContextItem>
        {
            new() { Id = "1", Content = "Tool A", Relevance = 0.7, TokenCount = 10, ContentType = "tool" },
            new() { Id = "2", Content = "Tool B", Relevance = 0.3, TokenCount = 10, ContentType = "tool" },
            new() { Id = "3", Content = "Tool C", Relevance = 0.9, TokenCount = 10, ContentType = "tool" }
        };

        var context = new StrategyContext
        {
            Query = "find tools",
            MinScore = 0.5,
            MaxItems = 10
        };

        // Act
        var result = strategy.Apply(items, context);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("3"); // Highest relevance first
        result[1].Id.Should().Be("1");
    }

    #endregion
}

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for GetStrategyComparisonQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class GetStrategyComparisonQueryHandlerTests
{
    private readonly Mock<ILogger<GetStrategyComparisonQueryHandler>> _mockLogger;
    private readonly Mock<IHybridCacheService> _mockCacheService;
    private readonly GetStrategyComparisonQueryHandler _handler;

    public GetStrategyComparisonQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetStrategyComparisonQueryHandler>>();
        _mockCacheService = new Mock<IHybridCacheService>();

        _mockCacheService.Setup(c => c.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HybridCacheStats { TotalHits = 80, TotalMisses = 20 });

        _handler = new GetStrategyComparisonQueryHandler(
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNonNullComparison()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Strategies);
        Assert.NotNull(result.LatencyRanking);
        Assert.NotNull(result.QualityRanking);
        Assert.NotNull(result.CostEfficiencyRanking);
    }

    [Fact]
    public async Task Handle_WithNoStrategyIds_ReturnsAllStrategies()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery { StrategyIds = null };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Strategies.Count >= 6); // All strategies
    }

    [Fact]
    public async Task Handle_WithSpecificStrategyIds_ReturnsFilteredStrategies()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery
        {
            StrategyIds = new[] { "Hybrid", "Semantic" }
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Strategies.Count);
        Assert.Contains(result.Strategies, s => s.StrategyId == "Hybrid");
        Assert.Contains(result.Strategies, s => s.StrategyId == "Semantic");
    }

    [Fact]
    public async Task Handle_ReturnsRankings()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.LatencyRanking);
        Assert.NotEmpty(result.QualityRanking);
        Assert.NotEmpty(result.CostEfficiencyRanking);
    }

    [Fact]
    public async Task Handle_ReturnsRecommendation()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.RecommendedStrategy);
        Assert.NotNull(result.RecommendationReason);
    }

    [Fact]
    public async Task Handle_StrategiesHaveValidMetrics()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var strategy in result.Strategies)
        {
            Assert.NotNull(strategy.StrategyId);
            Assert.True(strategy.TotalQueries >= 0);
            Assert.True(strategy.AverageLatencyMs >= 0);
            Assert.InRange(strategy.AverageRelevanceScore, 0.0, 1.0);
            Assert.True(strategy.TotalCost >= 0);
        }
    }

    [Fact]
    public async Task Handle_WithDateRange_FiltersResults()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_LogsInformation()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Comparing strategies")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

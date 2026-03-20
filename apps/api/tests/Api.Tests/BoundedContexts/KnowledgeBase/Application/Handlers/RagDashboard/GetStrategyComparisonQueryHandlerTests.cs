using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.Strategies.Should().NotBeNull();
        result.LatencyRanking.Should().NotBeNull();
        result.QualityRanking.Should().NotBeNull();
        result.CostEfficiencyRanking.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithNoStrategyIds_ReturnsAllStrategies()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery { StrategyIds = null };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        (result.Strategies.Count >= 6).Should().BeTrue(); // All strategies
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
        result.Strategies.Count.Should().Be(2);
        result.Strategies.Should().Contain(s => s.StrategyId == "Hybrid");
        result.Strategies.Should().Contain(s => s.StrategyId == "Semantic");
    }

    [Fact]
    public async Task Handle_ReturnsRankings()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.LatencyRanking.Should().NotBeEmpty();
        result.QualityRanking.Should().NotBeEmpty();
        result.CostEfficiencyRanking.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsRecommendation()
    {
        // Arrange
        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.RecommendedStrategy.Should().NotBeNull();
        result.RecommendationReason.Should().NotBeNull();
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
            strategy.StrategyId.Should().NotBeNull();
            (strategy.TotalQueries >= 0).Should().BeTrue();
            (strategy.AverageLatencyMs >= 0).Should().BeTrue();
            strategy.AverageRelevanceScore.Should().BeInRange(0.0, 1.0);
            (strategy.TotalCost >= 0).Should().BeTrue();
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
        result.Should().NotBeNull();
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

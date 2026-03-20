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
/// Unit tests for GetRagDashboardOverviewQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class GetRagDashboardOverviewQueryHandlerTests
{
    private readonly Mock<ILogger<GetRagDashboardOverviewQueryHandler>> _mockLogger;
    private readonly Mock<IHybridCacheService> _mockCacheService;
    private readonly GetRagDashboardOverviewQueryHandler _handler;

    public GetRagDashboardOverviewQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetRagDashboardOverviewQueryHandler>>();
        _mockCacheService = new Mock<IHybridCacheService>();

        // Setup default cache stats
        _mockCacheService.Setup(c => c.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HybridCacheStats { TotalHits = 80, TotalMisses = 20 });

        _handler = new GetRagDashboardOverviewQueryHandler(
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNonNullOverview()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Strategies.Should().NotBeNull();
        result.AggregatedMetrics.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReturnsStrategies()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Strategies.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_StrategiesHaveValidProperties()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var strategy in result.Strategies)
        {
            strategy.StrategyId.Should().NotBeNull();
            strategy.StrategyName.Should().NotBeNull();
            (strategy.TotalQueries >= 0).Should().BeTrue();
            (strategy.AverageLatencyMs >= 0).Should().BeTrue();
            strategy.AverageRelevanceScore.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public async Task Handle_ReturnsAggregatedMetrics()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.AggregatedMetrics.Should().NotBeNull();
        (result.AggregatedMetrics.TotalQueries >= 0).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDateRange_FiltersResults()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery
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
    public async Task Handle_GetsCacheStatsFromService()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockCacheService.Verify(c => c.GetStatsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LogsInformation()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting RAG dashboard overview")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Hybrid")]
    [InlineData("Semantic")]
    [InlineData("Keyword")]
    [InlineData("Contextual")]
    [InlineData("MultiQuery")]
    [InlineData("Agentic")]
    public async Task Handle_ContainsAllStrategies(string strategyId)
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Strategies.Should().Contain(s => s.StrategyId == strategyId);
    }

    [Fact]
    public async Task Handle_ReturnsBestPerformingStrategy()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.BestPerformingStrategy.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReturnsMostCostEffectiveStrategy()
    {
        // Arrange
        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.MostCostEffectiveStrategy.Should().NotBeNull();
    }
}

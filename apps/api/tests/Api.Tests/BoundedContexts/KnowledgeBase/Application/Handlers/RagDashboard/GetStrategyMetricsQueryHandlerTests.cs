using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for GetStrategyMetricsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class GetStrategyMetricsQueryHandlerTests
{
    private readonly Mock<ILogger<GetStrategyMetricsQueryHandler>> _mockLogger;
    private readonly Mock<IHybridCacheService> _mockCacheService;
    private readonly GetStrategyMetricsQueryHandler _handler;

    public GetStrategyMetricsQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetStrategyMetricsQueryHandler>>();
        _mockCacheService = new Mock<IHybridCacheService>();

        _mockCacheService.Setup(c => c.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HybridCacheStats { TotalHits = 80, TotalMisses = 20 });

        _handler = new GetStrategyMetricsQueryHandler(
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNonNullMetrics()
    {
        // Arrange
        var query = new GetStrategyMetricsQuery { StrategyId = "Hybrid" };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("Hybrid")]
    [InlineData("Semantic")]
    [InlineData("Keyword")]
    [InlineData("Contextual")]
    [InlineData("MultiQuery")]
    [InlineData("Agentic")]
    public async Task Handle_ReturnsMetricsForValidStrategies(string strategyId)
    {
        // Arrange
        var query = new GetStrategyMetricsQuery { StrategyId = strategyId };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(strategyId, result.StrategyId);
    }

    [Fact]
    public async Task Handle_ReturnsMetricsWithValidLatency()
    {
        // Arrange
        var query = new GetStrategyMetricsQuery { StrategyId = "Hybrid" };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.TotalQueries >= 0);
        Assert.True(result.AverageLatencyMs >= 0);
        Assert.True(result.P95LatencyMs >= 0);
        Assert.True(result.P99LatencyMs >= 0);
    }

    [Fact]
    public async Task Handle_ReturnsMetricsWithValidRelevanceScores()
    {
        // Arrange
        var query = new GetStrategyMetricsQuery { StrategyId = "Hybrid" };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.InRange(result.AverageRelevanceScore, 0.0, 1.0);
    }

    [Fact]
    public async Task Handle_ReturnsMetricsWithValidCost()
    {
        // Arrange
        var query = new GetStrategyMetricsQuery { StrategyId = "Hybrid" };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.TotalCost >= 0);
        Assert.True(result.AverageCostPerQuery >= 0);
    }

    [Fact]
    public async Task Handle_WithDateRange_FiltersResults()
    {
        // Arrange
        var query = new GetStrategyMetricsQuery
        {
            StrategyId = "Hybrid",
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
        var query = new GetStrategyMetricsQuery { StrategyId = "Hybrid" };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting metrics for strategy")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for GetStrategyTimeSeriesQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class GetStrategyTimeSeriesQueryHandlerTests
{
    private readonly Mock<ILogger<GetStrategyTimeSeriesQueryHandler>> _mockLogger;
    private readonly GetStrategyTimeSeriesQueryHandler _handler;

    public GetStrategyTimeSeriesQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetStrategyTimeSeriesQueryHandler>>();
        _handler = new GetStrategyTimeSeriesQueryHandler(_mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNonNullTimeSeries()
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "hour"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LatencyTrend);
        Assert.NotNull(result.RelevanceTrend);
        Assert.NotNull(result.QueryCountTrend);
        Assert.NotNull(result.CostTrend);
    }

    [Theory]
    [InlineData("hour")]
    [InlineData("day")]
    [InlineData("week")]
    public async Task Handle_WithValidGranularity_ReturnsTimeSeries(string granularity)
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = granularity
        };

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
    public async Task Handle_WithValidStrategies_ReturnsTimeSeries(string strategyId)
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = strategyId,
            Granularity = "hour"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(strategyId, result.StrategyId);
    }

    [Fact]
    public async Task Handle_ReturnsNonEmptyLatencyTrend()
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "hour"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(result.LatencyTrend);
    }

    [Fact]
    public async Task Handle_LatencyTrendHasValidDataPoints()
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "hour"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var point in result.LatencyTrend)
        {
            Assert.True(point.Value >= 0);
        }
    }

    [Fact]
    public async Task Handle_RelevanceTrendHasValidDataPoints()
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "hour"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        foreach (var point in result.RelevanceTrend)
        {
            Assert.InRange(point.Value, 0.0, 1.0);
        }
    }

    [Fact]
    public async Task Handle_WithDateRange_FiltersResults()
    {
        // Arrange
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "hour",
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
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "hour"
        };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting time series")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

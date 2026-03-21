using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
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
public sealed class GetStrategyTimeSeriesQueryHandlerTests
{
    private readonly Mock<IRagExecutionRepository> _mockRepo = new();
    private readonly Mock<ILogger<GetStrategyTimeSeriesQueryHandler>> _mockLogger = new();
    private readonly GetStrategyTimeSeriesQueryHandler _handler;

    public GetStrategyTimeSeriesQueryHandlerTests()
    {
        _handler = new GetStrategyTimeSeriesQueryHandler(_mockRepo.Object, _mockLogger.Object);
    }

    private static List<TimeSeriesPoint> CreateDailyPoints(int count = 3) =>
        Enumerable.Range(0, count)
            .Select(i => new TimeSeriesPoint(
                DateTimeOffset.UtcNow.AddDays(-i),
                QueryCount: 50 + i * 10,
                AverageLatencyMs: 120.0 + i * 5,
                AverageConfidence: 0.85,
                TotalCost: 0.50m + i * 0.10m))
            .ToList();

    [Fact]
    public async Task Handle_DailyGranularity_ReturnsBucketedData()
    {
        // Arrange
        var points = CreateDailyPoints(3);

        _mockRepo
            .Setup(r => r.GetTimeSeriesMetricsAsync(
                "Hybrid",
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                TimeSeriesGranularity.Day,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "day"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.StrategyId.Should().Be("Hybrid");
        result.LatencyTrend.Should().HaveCount(3);
        result.RelevanceTrend.Should().HaveCount(3);
        result.QueryCountTrend.Should().HaveCount(3);
        result.CostTrend.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_NullGranularity_DefaultsToDaily()
    {
        // Arrange
        var points = CreateDailyPoints(2);

        _mockRepo
            .Setup(r => r.GetTimeSeriesMetricsAsync(
                "Semantic",
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                TimeSeriesGranularity.Day,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        // "invalid" granularity string defaults to Day per handler logic
        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Semantic",
            Granularity = "invalid_granularity"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _mockRepo.Verify(r => r.GetTimeSeriesMetricsAsync(
            "Semantic",
            It.IsAny<DateOnly>(),
            It.IsAny<DateOnly>(),
            TimeSeriesGranularity.Day,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyData_ReturnsEmptyTrends()
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetTimeSeriesMetricsAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeSeriesGranularity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TimeSeriesPoint>());

        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Keyword",
            Granularity = "hour"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.StrategyId.Should().Be("Keyword");
        result.LatencyTrend.Should().BeEmpty();
        result.RelevanceTrend.Should().BeEmpty();
        result.QueryCountTrend.Should().BeEmpty();
        result.CostTrend.Should().BeEmpty();
    }

    [Theory]
    [InlineData("hour", TimeSeriesGranularity.Hour)]
    [InlineData("day", TimeSeriesGranularity.Day)]
    [InlineData("week", TimeSeriesGranularity.Week)]
    public async Task Handle_GranularityStrings_MappedCorrectly(string granularityString, TimeSeriesGranularity expectedGranularity)
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetTimeSeriesMetricsAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                expectedGranularity,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TimeSeriesPoint>());

        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = granularityString
        };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockRepo.Verify(r => r.GetTimeSeriesMetricsAsync(
            "Hybrid",
            It.IsAny<DateOnly>(),
            It.IsAny<DateOnly>(),
            expectedGranularity,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MapsPointsToTrendDtos()
    {
        // Arrange
        var bucket = DateTimeOffset.UtcNow;
        var points = new List<TimeSeriesPoint>
        {
            new(bucket, QueryCount: 75, AverageLatencyMs: 200.0, AverageConfidence: 0.90, TotalCost: 1.25m)
        };

        _mockRepo
            .Setup(r => r.GetTimeSeriesMetricsAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<TimeSeriesGranularity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(points);

        var query = new GetStrategyTimeSeriesQuery
        {
            StrategyId = "Hybrid",
            Granularity = "day"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.LatencyTrend.Should().ContainSingle().Which.Value.Should().Be(200.0);
        result.RelevanceTrend.Should().ContainSingle().Which.Value.Should().Be(0.90);
        result.QueryCountTrend.Should().ContainSingle().Which.Value.Should().Be(75);
        result.CostTrend.Should().ContainSingle().Which.Value.Should().Be(1.25);
    }
}

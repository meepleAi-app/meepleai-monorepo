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
/// Unit tests for GetRagDashboardOverviewQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public sealed class GetRagDashboardOverviewQueryHandlerTests
{
    private readonly Mock<IRagExecutionRepository> _mockRepo = new();
    private readonly Mock<ILogger<GetRagDashboardOverviewQueryHandler>> _mockLogger = new();
    private readonly GetRagDashboardOverviewQueryHandler _handler;

    public GetRagDashboardOverviewQueryHandlerTests()
    {
        _handler = new GetRagDashboardOverviewQueryHandler(_mockRepo.Object, _mockLogger.Object);
    }

    private static StrategyAggregateMetrics CreateMetrics(
        string strategy,
        int queries = 100,
        double confidence = 0.85,
        decimal costPerQuery = 0.05m,
        double avgLatencyMs = 150.0,
        int cacheHits = 80,
        int cacheMisses = 20) =>
        new(strategy, queries, avgLatencyMs, 300.0, 500.0, confidence,
            cacheHits, cacheMisses, 50000, costPerQuery * queries, costPerQuery,
            2, 0.02, 10, 1, 5, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_WithData_ReturnsStrategyMetrics()
    {
        // Arrange
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid", queries: 200, confidence: 0.88),
            CreateMetrics("Semantic", queries: 150, confidence: 0.80)
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Strategies.Should().HaveCount(2);
        result.AggregatedMetrics.Should().NotBeNull();
        result.AggregatedMetrics.TotalQueries.Should().Be(350);
        result.StartDate.Should().NotBe(default);
        result.EndDate.Should().NotBe(default);
    }

    [Fact]
    public async Task Handle_WithNoData_ReturnsEmptyStrategies()
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StrategyAggregateMetrics>());

        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Strategies.Should().BeEmpty();
        result.AggregatedMetrics.TotalQueries.Should().Be(0);
        result.AggregatedMetrics.AverageLatencyMs.Should().Be(0);
        result.AggregatedMetrics.AverageRelevanceScore.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CalculatesBestPerformingStrategy()
    {
        // Arrange
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid", confidence: 0.75),
            CreateMetrics("Semantic", confidence: 0.92)
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.BestPerformingStrategy.Should().Be("Semantic");
    }

    [Fact]
    public async Task Handle_MapsConfidenceToBothScores()
    {
        // Arrange
        const double expectedConfidence = 0.91;
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid", confidence: expectedConfidence)
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetRagDashboardOverviewQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var strategy = result.Strategies.Should().ContainSingle().Subject;
        strategy.AverageRelevanceScore.Should().Be(expectedConfidence);
        strategy.AverageConfidenceScore.Should().Be(expectedConfidence);
    }

    [Fact]
    public async Task Handle_WithDateRange_PassesDateRangeToRepository()
    {
        // Arrange
        var startDate = new DateOnly(2026, 1, 1);
        var endDate = new DateOnly(2026, 3, 1);

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StrategyAggregateMetrics>());

        var query = new GetRagDashboardOverviewQuery
        {
            StartDate = startDate,
            EndDate = endDate
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        _mockRepo.Verify(r => r.GetAggregatedMetricsAsync(
            startDate,
            endDate,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CallsRepositoryOnce()
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StrategyAggregateMetrics>());

        var query = new GetRagDashboardOverviewQuery();

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockRepo.Verify(r => r.GetAggregatedMetricsAsync(
            It.IsAny<DateOnly?>(),
            It.IsAny<DateOnly?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

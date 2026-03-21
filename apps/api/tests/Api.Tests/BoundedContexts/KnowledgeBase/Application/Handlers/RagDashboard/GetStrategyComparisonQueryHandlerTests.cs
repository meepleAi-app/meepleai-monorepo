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
/// Unit tests for GetStrategyComparisonQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public sealed class GetStrategyComparisonQueryHandlerTests
{
    private readonly Mock<IRagExecutionRepository> _mockRepo = new();
    private readonly Mock<ILogger<GetStrategyComparisonQueryHandler>> _mockLogger = new();
    private readonly GetStrategyComparisonQueryHandler _handler;

    public GetStrategyComparisonQueryHandlerTests()
    {
        _handler = new GetStrategyComparisonQueryHandler(_mockRepo.Object, _mockLogger.Object);
    }

    private static StrategyAggregateMetrics CreateMetrics(
        string strategy,
        int queries = 100,
        double confidence = 0.85,
        decimal costPerQuery = 0.05m,
        double avgLatencyMs = 150.0) =>
        new(strategy, queries, avgLatencyMs, 300.0, 500.0, confidence,
            80, 20, 50000, costPerQuery * queries, costPerQuery,
            2, 0.02, 10, 1, 5, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_MultipleStrategies_RanksCorrectly()
    {
        // Arrange
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid", queries: 100, confidence: 0.85, avgLatencyMs: 150.0),
            CreateMetrics("Semantic", queries: 80, confidence: 0.78, avgLatencyMs: 100.0),
            CreateMetrics("Keyword", queries: 120, confidence: 0.70, avgLatencyMs: 80.0)
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Strategies.Should().HaveCount(3);
        result.LatencyRanking.Should().HaveCount(3);
        result.QualityRanking.Should().HaveCount(3);
        result.CostEfficiencyRanking.Should().HaveCount(3);

        // Keyword has lowest latency → should rank highest in latency
        result.LatencyRanking["Keyword"].Should().BeGreaterThan(result.LatencyRanking["Hybrid"]);

        // Hybrid has highest confidence → should rank highest in quality
        result.QualityRanking["Hybrid"].Should().BeGreaterThan(result.QualityRanking["Semantic"]);
        result.QualityRanking["Hybrid"].Should().BeGreaterThan(result.QualityRanking["Keyword"]);
    }

    [Fact]
    public async Task Handle_EmptyData_ReturnsNoRecommendation()
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StrategyAggregateMetrics>());

        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Strategies.Should().BeEmpty();
        result.LatencyRanking.Should().BeEmpty();
        result.QualityRanking.Should().BeEmpty();
        result.CostEfficiencyRanking.Should().BeEmpty();
        result.RecommendedStrategy.Should().BeEmpty();
        result.RecommendationReason.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WeightedScoring_RecommendsCorrectStrategy()
    {
        // Arrange — "Quality" strategy: highest confidence (0.95), moderate latency, moderate cost
        //           "Fast" strategy: low latency but low confidence (0.65)
        //           "Cheap" strategy: lowest cost but low confidence (0.60)
        // Weights: quality 50%, latency 30%, cost 20% → "Quality" should win
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Quality", queries: 100, confidence: 0.95, costPerQuery: 0.08m, avgLatencyMs: 200.0),
            CreateMetrics("Fast", queries: 100, confidence: 0.65, costPerQuery: 0.06m, avgLatencyMs: 50.0),
            CreateMetrics("Cheap", queries: 100, confidence: 0.60, costPerQuery: 0.02m, avgLatencyMs: 180.0)
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.RecommendedStrategy.Should().Be("Quality");
        result.RecommendationReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WithStrategyIdFilter_ReturnsOnlyRequestedStrategies()
    {
        // Arrange
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid"),
            CreateMetrics("Semantic"),
            CreateMetrics("Keyword")
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetStrategyComparisonQuery
        {
            StrategyIds = new[] { "Hybrid", "Semantic" }
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Strategies.Should().HaveCount(2);
        result.Strategies.Should().Contain(s => s.StrategyId == "Hybrid");
        result.Strategies.Should().Contain(s => s.StrategyId == "Semantic");
        result.Strategies.Should().NotContain(s => s.StrategyId == "Keyword");
    }

    [Fact]
    public async Task Handle_SingleStrategy_ReturnsScoreOfOne()
    {
        // Arrange
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid")
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetStrategyComparisonQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.RecommendedStrategy.Should().Be("Hybrid");
        result.LatencyRanking["Hybrid"].Should().Be(1.0);
        result.QualityRanking["Hybrid"].Should().Be(1.0);
        result.CostEfficiencyRanking["Hybrid"].Should().Be(1.0);
    }
}

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
/// Unit tests for GetStrategyMetricsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public sealed class GetStrategyMetricsQueryHandlerTests
{
    private readonly Mock<IRagExecutionRepository> _mockRepo = new();
    private readonly Mock<ILogger<GetStrategyMetricsQueryHandler>> _mockLogger = new();
    private readonly GetStrategyMetricsQueryHandler _handler;

    public GetStrategyMetricsQueryHandlerTests()
    {
        _handler = new GetStrategyMetricsQueryHandler(_mockRepo.Object, _mockLogger.Object);
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
    public async Task Handle_ValidStrategy_ReturnsMetrics()
    {
        // Arrange
        var metrics = new List<StrategyAggregateMetrics>
        {
            CreateMetrics("Hybrid", queries: 200, confidence: 0.88, avgLatencyMs: 120.0)
        };

        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                "Hybrid",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);

        var query = new GetStrategyMetricsQuery { StrategyId = "Hybrid" };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.StrategyId.Should().Be("Hybrid");
        result.TotalQueries.Should().Be(200);
        result.AverageLatencyMs.Should().Be(120.0);
        result.AverageRelevanceScore.Should().Be(0.88);
        result.AverageConfidenceScore.Should().Be(0.88);
        result.TotalCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_UnknownStrategy_ReturnsEmptyDto()
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                "UnknownStrategy",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StrategyAggregateMetrics>());

        var query = new GetStrategyMetricsQuery { StrategyId = "UnknownStrategy" };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.StrategyId.Should().Be("UnknownStrategy");
        result.TotalQueries.Should().Be(0);
        result.AverageLatencyMs.Should().Be(0);
        result.AverageRelevanceScore.Should().Be(0);
        result.AverageConfidenceScore.Should().Be(0);
        result.TotalCost.Should().Be(0);
        result.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesStrategyIdToRepository()
    {
        // Arrange
        _mockRepo
            .Setup(r => r.GetAggregatedMetricsAsync(
                It.IsAny<DateOnly?>(),
                It.IsAny<DateOnly?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StrategyAggregateMetrics>());

        var query = new GetStrategyMetricsQuery { StrategyId = "Semantic" };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockRepo.Verify(r => r.GetAggregatedMetricsAsync(
            It.IsAny<DateOnly?>(),
            It.IsAny<DateOnly?>(),
            "Semantic",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

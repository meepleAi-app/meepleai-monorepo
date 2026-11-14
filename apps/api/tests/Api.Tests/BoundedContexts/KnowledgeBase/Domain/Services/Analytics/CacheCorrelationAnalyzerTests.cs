using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// Tests for the CacheCorrelationAnalyzer.
/// Issue #4978: the k6 smoke run, once it could log in, found GET /admin/llm/monthly-report answering 400 for every
/// month without LLM spend. The efficiency score was NaN, and System.Text.Json refuses to write it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CacheCorrelationAnalyzerTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateOnly End = new(2026, 11, 30);

    private readonly Mock<ILlmCostLogRepository> _costLogRepositoryMock = new();
    private readonly CacheCorrelationAnalyzer _analyzer;

    public CacheCorrelationAnalyzerTests()
    {
        _analyzer = new CacheCorrelationAnalyzer(
            _costLogRepositoryMock.Object,
            new Mock<ILogger<CacheCorrelationAnalyzer>>().Object);
    }

    private void GivenTotalCost(decimal cost) =>
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(Start, End, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cost);

    [Theory]
    [InlineData(0)]       // no spend: 0/0
    [InlineData(0.005)]   // spend under one estimated query: no requests, actual cost over a zero baseline
    public async Task AnalyzeCacheEffectivenessAsync_WithoutAnEstimatedRequest_ReturnsAZeroScoreThatSerializes(double cost)
    {
        GivenTotalCost((decimal)cost);

        var report = await _analyzer.AnalyzeCacheEffectivenessAsync(Start, End);

        report.TotalRequests.Should().Be(0);
        report.CacheEfficiencyScore.Should().Be(0);
        var serialize = () => JsonSerializer.Serialize(report);
        serialize.Should().NotThrow();
    }

    [Fact]
    public async Task AnalyzeCacheEffectivenessAsync_WithSpend_ScoresHitRateTimesTheShareOfCostAvoided()
    {
        // $1.00 at $0.01 a query: 100 misses, an estimated 300 requests, so 200 hits and $3.00 without the cache.
        GivenTotalCost(1.00m);

        var report = await _analyzer.AnalyzeCacheEffectivenessAsync(Start, End);

        report.TotalRequests.Should().Be(300);
        report.CacheHits.Should().Be(200);
        report.CostWithoutCache.Should().Be(3.00m);
        report.CacheEfficiencyScore.Should().BeApproximately(200.0 / 300 * (1.0 - 1.0 / 3), 1e-9);
    }
}

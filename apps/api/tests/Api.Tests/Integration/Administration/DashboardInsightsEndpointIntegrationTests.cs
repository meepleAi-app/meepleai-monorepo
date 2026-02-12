using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for Dashboard Insights endpoint (Issue #3916).
/// Tests AI insights query handler with caching and error handling.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Administration")]
public sealed class DashboardInsightsEndpointIntegrationTests
{
    private readonly Mock<IAiInsightsService> _mockInsightsService;
    private readonly Mock<HybridCache> _mockCache;
    private readonly Mock<ILogger<GetDashboardInsightsQueryHandler>> _mockLogger;

    public DashboardInsightsEndpointIntegrationTests()
    {
        _mockInsightsService = new Mock<IAiInsightsService>();
        _mockCache = new Mock<HybridCache>();
        _mockLogger = new Mock<ILogger<GetDashboardInsightsQueryHandler>>();

        // Configure cache to bypass for tests
        _mockCache
            .Setup(c => c.GetOrCreateAsync<AiInsightsDto>(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, ValueTask<AiInsightsDto>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, ValueTask<AiInsightsDto>>, HybridCacheEntryOptions, IEnumerable<string>, CancellationToken>(
                (key, factory, options, tags, ct) => factory(ct));
    }

    private GetDashboardInsightsQueryHandler CreateHandler() =>
        new(_mockInsightsService.Object, _mockCache.Object, _mockLogger.Object);

    [Fact]
    public async Task Handle_ValidUserId_ReturnsInsights()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedInsights = new AiInsightsDto(
            new List<DashboardInsightDto>
            {
                new(
                    Id: "rec-1",
                    Type: InsightType.Recommendation,
                    Icon: "🎲",
                    Title: "Spirit Island",
                    Description: "Similar to games you've played",
                    ActionUrl: "/games/123",
                    ActionLabel: "View Game",
                    Priority: 1,
                    Metadata: null)
            }.AsReadOnly(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(15));

        _mockInsightsService
            .Setup(s => s.GetInsightsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInsights);

        var handler = CreateHandler();
        var query = new GetDashboardInsightsQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Insights.Should().HaveCount(1);
        result.Insights.First().Title.Should().Be("Spirit Island");
        result.NextRefresh.Should().BeAfter(result.GeneratedAt);

        _mockInsightsService.Verify(
            s => s.GetInsightsAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceThrowsException_ReturnsEmptyInsights()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockInsightsService
            .Setup(s => s.GetInsightsAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var handler = CreateHandler();
        var query = new GetDashboardInsightsQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - graceful degradation
        result.Should().NotBeNull();
        result.Insights.Should().BeEmpty();
        result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_MultipleInsightTypes_ReturnsAllTypes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var insights = new AiInsightsDto(
            new List<DashboardInsightDto>
            {
                new("rec-1", InsightType.Recommendation, "🎲", "Game 1", "desc", "/url1", "View", 1),
                new("backlog-1", InsightType.Backlog, "⏰", "Game 2", "desc", "/url2", "Schedule", 2),
                new("rules-1", InsightType.RulesReminder, "📖", "Chat 1", "desc", "/url3", "Review", 3)
            }.AsReadOnly(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(15));

        _mockInsightsService
            .Setup(s => s.GetInsightsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insights);

        var handler = CreateHandler();
        var query = new GetDashboardInsightsQuery(userId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Insights.Should().HaveCount(3);
        result.Insights.Should().Contain(i => i.Type == InsightType.Recommendation);
        result.Insights.Should().Contain(i => i.Type == InsightType.Backlog);
        result.Insights.Should().Contain(i => i.Type == InsightType.RulesReminder);
    }

    [Fact]
    public async Task Handle_PerformanceRequirement_CompletesUnder1Second()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var insights = new AiInsightsDto(
            Array.Empty<DashboardInsightDto>(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(15));

        _mockInsightsService
            .Setup(s => s.GetInsightsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(insights);

        var handler = CreateHandler();
        var query = new GetDashboardInsightsQuery(userId);

        // Act - measure execution time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await handler.Handle(query, CancellationToken.None);
        stopwatch.Stop();

        // Assert - performance requirement < 1s (p95)
        result.Should().NotBeNull();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            $"Expected <1000ms, got {stopwatch.ElapsedMilliseconds}ms");
    }
}

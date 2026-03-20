using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetOpenRouterStatusQueryHandler.
/// Issue #5077: Admin usage page — KPI cards data source.
/// Covers: happy path, null account (Redis cold start), and correct DTO mapping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetOpenRouterStatusQueryHandlerTests
{
    private readonly Mock<IOpenRouterUsageService> _mockUsageService;
    private readonly Mock<IOpenRouterRateLimitTracker> _mockRateLimitTracker;
    private readonly Mock<ILlmRequestLogRepository> _mockLogRepository;
    private readonly Mock<ILogger<GetOpenRouterStatusQueryHandler>> _mockLogger;
    private readonly GetOpenRouterStatusQueryHandler _handler;

    public GetOpenRouterStatusQueryHandlerTests()
    {
        _mockUsageService = new Mock<IOpenRouterUsageService>();
        _mockRateLimitTracker = new Mock<IOpenRouterRateLimitTracker>();
        _mockLogRepository = new Mock<ILlmRequestLogRepository>();
        _mockLogger = new Mock<ILogger<GetOpenRouterStatusQueryHandler>>();

        _handler = new GetOpenRouterStatusQueryHandler(
            _mockUsageService.Object,
            _mockRateLimitTracker.Object,
            _mockLogRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithAllServicesAvailable_ReturnsMappedDto()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var account = new OpenRouterAccountStatus
        {
            BalanceUsd = 4.50m,
            IsFreeTier = false,
            RateLimitInterval = "minute",
            LastUpdated = now,
        };
        var rateLimit = new RateLimitStatus
        {
            CurrentRpm = 80,
            LimitRpm = 200,
            UtilizationPercent = 0.4,
            IsThrottled = false,
        };

        _mockUsageService.Setup(s => s.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _mockUsageService.Setup(s => s.GetDailySpendAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.0025m);
        _mockRateLimitTracker.Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimit);
        _mockLogRepository.Setup(r => r.GetTodayCountAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var result = await _handler.Handle(new GetOpenRouterStatusQuery(), CancellationToken.None);

        // Assert
        result.BalanceUsd.Should().Be(4.50m);
        result.DailySpendUsd.Should().Be(0.0025m);
        result.TodayRequestCount.Should().Be(42);
        result.CurrentRpm.Should().Be(80);
        result.LimitRpm.Should().Be(200);
        result.UtilizationPercent.Should().Be(0.4);
        Assert.False(result.IsThrottled);
        Assert.False(result.IsFreeTier);
        result.RateLimitInterval.Should().Be("minute");
        result.LastUpdated.Should().Be(now);
    }

    [Fact]
    public async Task Handle_WhenAccountStatusIsNull_ReturnsSafeDefaults()
    {
        // Arrange — Redis cold start: GetAccountStatusAsync returns null
        var rateLimit = new RateLimitStatus
        {
            CurrentRpm = 5,
            LimitRpm = 200,
            UtilizationPercent = 0.025,
            IsThrottled = false,
        };

        _mockUsageService.Setup(s => s.GetAccountStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((OpenRouterAccountStatus?)null);
        _mockUsageService.Setup(s => s.GetDailySpendAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _mockRateLimitTracker.Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimit);
        _mockLogRepository.Setup(r => r.GetTodayCountAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var result = await _handler.Handle(new GetOpenRouterStatusQuery(), CancellationToken.None);

        // Assert — null-coalescing fallbacks
        result.BalanceUsd.Should().Be(0m);
        Assert.False(result.IsFreeTier);
        result.RateLimitInterval.Should().Be(string.Empty);
        Assert.Null(result.LastUpdated);
        // Rate limit and request count still populated
        result.CurrentRpm.Should().Be(5);
        result.TodayRequestCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenThrottled_SetsIsThrottledTrue()
    {
        // Arrange
        var account = new OpenRouterAccountStatus { BalanceUsd = 1m, RateLimitInterval = "minute" };
        var rateLimit = new RateLimitStatus
        {
            CurrentRpm = 200,
            LimitRpm = 200,
            UtilizationPercent = 1.0,
            IsThrottled = true,
        };

        _mockUsageService.Setup(s => s.GetAccountStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockUsageService.Setup(s => s.GetDailySpendAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        _mockRateLimitTracker.Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimit);
        _mockLogRepository.Setup(r => r.GetTodayCountAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(new GetOpenRouterStatusQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsThrottled);
        result.UtilizationPercent.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_FansOutAllFourTasksInParallel()
    {
        // Arrange — verify each dependency is called exactly once
        var account = new OpenRouterAccountStatus { BalanceUsd = 0m, RateLimitInterval = string.Empty };
        var rateLimit = new RateLimitStatus();

        _mockUsageService.Setup(s => s.GetAccountStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _mockUsageService.Setup(s => s.GetDailySpendAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        _mockRateLimitTracker.Setup(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rateLimit);
        _mockLogRepository.Setup(r => r.GetTodayCountAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(new GetOpenRouterStatusQuery(), CancellationToken.None);

        // Assert — each of the four data sources called exactly once
        _mockUsageService.Verify(s => s.GetAccountStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUsageService.Verify(s => s.GetDailySpendAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRateLimitTracker.Verify(t => t.GetCurrentStatusAsync("openrouter", It.IsAny<CancellationToken>()), Times.Once);
        _mockLogRepository.Verify(r => r.GetTodayCountAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

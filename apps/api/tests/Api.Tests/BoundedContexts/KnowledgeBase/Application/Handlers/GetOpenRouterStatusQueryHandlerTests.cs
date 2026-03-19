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
        Assert.Equal(4.50m, result.BalanceUsd);
        Assert.Equal(0.0025m, result.DailySpendUsd);
        Assert.Equal(42, result.TodayRequestCount);
        Assert.Equal(80, result.CurrentRpm);
        Assert.Equal(200, result.LimitRpm);
        Assert.Equal(0.4, result.UtilizationPercent);
        Assert.False(result.IsThrottled);
        Assert.False(result.IsFreeTier);
        Assert.Equal("minute", result.RateLimitInterval);
        Assert.Equal(now, result.LastUpdated);
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
        Assert.Equal(0m, result.BalanceUsd);
        Assert.False(result.IsFreeTier);
        Assert.Equal(string.Empty, result.RateLimitInterval);
        Assert.Null(result.LastUpdated);
        // Rate limit and request count still populated
        Assert.Equal(5, result.CurrentRpm);
        Assert.Equal(3, result.TodayRequestCount);
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
        Assert.Equal(1.0, result.UtilizationPercent);
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

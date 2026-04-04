using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CheckLlmCostAlertsCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CheckLlmCostAlertsCommandHandlerTests
{
    private readonly LlmCostAlertService _alertService;
    private readonly CheckLlmCostAlertsCommandHandler _handler;
    private readonly Mock<ILlmCostLogRepository> _mockCostLogRepository;
    private readonly Mock<IAlertingService> _mockAlertingService;

    public CheckLlmCostAlertsCommandHandlerTests()
    {
        _mockCostLogRepository = new Mock<ILlmCostLogRepository>();
        _mockAlertingService = new Mock<IAlertingService>();
        var mockLogger = new Mock<ILogger<LlmCostAlertService>>();

        _alertService = new LlmCostAlertService(
            _mockCostLogRepository.Object,
            _mockAlertingService.Object,
            mockLogger.Object);

        _handler = new CheckLlmCostAlertsCommandHandler(_alertService);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_CostsWithinThresholds_ReturnsSuccessWithoutSendingAlerts()
    {
        // All costs well below the $100/day, $500/week, $3000/month thresholds
        _mockCostLogRepository
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _mockCostLogRepository
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var result = await _handler.Handle(new CheckLlmCostAlertsCommand(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Cost threshold checks completed");
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DailyCostExceedsThreshold_SendsDailyAlert()
    {
        // $101 daily cost — exceeds the $100 threshold; weekly/monthly return 0 to isolate the daily path
        _mockCostLogRepository
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(101m);
        _mockCostLogRepository
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _mockCostLogRepository
            .Setup(r => r.GetCostsByProviderAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        var result = await _handler.Handle(new CheckLlmCostAlertsCommand(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                "LLM_COST_THRESHOLD", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                It.Is<string>(t => t != "LLM_COST_THRESHOLD"), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WeeklyCostExceedsThreshold_SendsWeeklyAlert()
    {
        // $501 weekly cost — exceeds the $500 threshold; daily returns 0 to isolate the weekly path
        _mockCostLogRepository
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _mockCostLogRepository
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(501m);

        var result = await _handler.Handle(new CheckLlmCostAlertsCommand(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                "LLM_WEEKLY_COST_THRESHOLD", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_MonthlyCostProjectionExceedsThreshold_SendsMonthlyAlert()
    {
        // Daily cost 0 so daily alert does not fire.
        // GetTotalCostAsync returns a high month-to-date cost that projects over $3000.
        // The projection is: (monthCost / daysElapsed) * daysInMonth.
        // We use 10000m so that even on the last day of the month (31/31) * 10000 = 10000 > 3000.
        _mockCostLogRepository
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        _mockCostLogRepository
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10000m);

        var result = await _handler.Handle(new CheckLlmCostAlertsCommand(), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                "LLM_MONTHLY_COST_PROJECTION", It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}

using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// Tests for the LlmCostAlertService.
/// Issue #3025: Backend 90% Coverage Target - Phase 16
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmCostAlertServiceTests
{
    private readonly Mock<ILlmCostLogRepository> _costLogRepositoryMock;
    private readonly Mock<IAlertingService> _alertingServiceMock;
    private readonly Mock<ILogger<LlmCostAlertService>> _loggerMock;
    private readonly LlmCostAlertService _service;

    // Threshold constants matching the service
    private const decimal DailyThreshold = 100m;
    private const decimal WeeklyThreshold = 500m;
    private const decimal MonthlyThreshold = 3000m;

    public LlmCostAlertServiceTests()
    {
        _costLogRepositoryMock = new Mock<ILlmCostLogRepository>();
        _alertingServiceMock = new Mock<IAlertingService>();
        _loggerMock = new Mock<ILogger<LlmCostAlertService>>();
        _service = new LlmCostAlertService(
            _costLogRepositoryMock.Object,
            _alertingServiceMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCostLogRepository_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new LlmCostAlertService(
            null!,
            _alertingServiceMock.Object,
            _loggerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("costLogRepository");
    }

    [Fact]
    public void Constructor_WithNullAlertingService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new LlmCostAlertService(
            _costLogRepositoryMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("alertingService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new LlmCostAlertService(
            _costLogRepositoryMock.Object,
            _alertingServiceMock.Object,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region CheckDailyCostThresholdAsync Tests

    [Fact]
    public async Task CheckDailyCostThresholdAsync_WhenCostBelowThreshold_DoesNotSendAlert()
    {
        // Arrange
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m); // Below $100 threshold

        // Act
        await _service.CheckDailyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckDailyCostThresholdAsync_WhenCostAtThreshold_DoesNotSendAlert()
    {
        // Arrange
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DailyThreshold); // Exactly at threshold

        // Act
        await _service.CheckDailyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckDailyCostThresholdAsync_WhenCostAboveThreshold_SendsAlert()
    {
        // Arrange
        var dailyCost = 150m; // Above $100 threshold
        var providerCosts = new Dictionary<string, decimal>
        {
            ["OpenRouter"] = 100m,
            ["DeepSeek"] = 50m
        };

        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dailyCost);

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerCosts);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckDailyCostThresholdAsync();

        // Assert - Use culture-invariant check (message contains the amounts)
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                "LLM_COST_THRESHOLD",
                "warning",
                It.Is<string>(s => s.Contains("150") && s.Contains("100") && s.Contains("exceeds threshold")),
                It.Is<IDictionary<string, object>>(m =>
                    (decimal)m["daily_cost"] == dailyCost &&
                    (decimal)m["threshold"] == DailyThreshold),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckDailyCostThresholdAsync_IncludesProviderBreakdown()
    {
        // Arrange
        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(200m);

        var providerCosts = new Dictionary<string, decimal>
        {
            ["OpenAI"] = 150m,
            ["Anthropic"] = 50m
        };

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerCosts);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckDailyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("OpenAI") && s.Contains("Anthropic")),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckDailyCostThresholdAsync_CalculatesExceededBy()
    {
        // Arrange
        var dailyCost = 175m;
        var expectedExceededBy = dailyCost - DailyThreshold; // 75m

        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dailyCost);

        _costLogRepositoryMock
            .Setup(r => r.GetCostsByProviderAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckDailyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<IDictionary<string, object>>(m =>
                    (decimal)m["exceeded_by"] == expectedExceededBy),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CheckWeeklyCostThresholdAsync Tests

    [Fact]
    public async Task CheckWeeklyCostThresholdAsync_WhenCostBelowThreshold_DoesNotSendAlert()
    {
        // Arrange
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(400m); // Below $500 threshold

        // Act
        await _service.CheckWeeklyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckWeeklyCostThresholdAsync_WhenCostAtThreshold_DoesNotSendAlert()
    {
        // Arrange
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WeeklyThreshold); // Exactly at threshold

        // Act
        await _service.CheckWeeklyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckWeeklyCostThresholdAsync_WhenCostAboveThreshold_SendsAlert()
    {
        // Arrange
        var weeklyCost = 750m; // Above $500 threshold

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(weeklyCost);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckWeeklyCostThresholdAsync();

        // Assert - Use culture-invariant check
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                "LLM_WEEKLY_COST_THRESHOLD",
                "warning",
                It.Is<string>(s => s.Contains("750") && s.Contains("500") && s.Contains("exceeds threshold")),
                It.Is<IDictionary<string, object>>(m =>
                    (decimal)m["weekly_cost"] == weeklyCost &&
                    (decimal)m["threshold"] == WeeklyThreshold),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckWeeklyCostThresholdAsync_QueriesCorrectDateRange()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-7);

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(
                It.Is<DateOnly>(d => d == weekStart),
                It.Is<DateOnly>(d => d == today),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        // Act
        await _service.CheckWeeklyCostThresholdAsync();

        // Assert
        _costLogRepositoryMock.Verify(
            r => r.GetTotalCostAsync(weekStart, today, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckWeeklyCostThresholdAsync_IncludesDateRangeInMessage()
    {
        // Arrange
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(600m);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckWeeklyCostThresholdAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("Period:")),
                It.Is<IDictionary<string, object>>(m =>
                    m.ContainsKey("start_date") &&
                    m.ContainsKey("end_date")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CheckMonthlyCostProjectionAsync Tests

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_WhenProjectionBelowThreshold_DoesNotSendAlert()
    {
        // Arrange - Set up cost so projection is below threshold
        // If we're mid-month, set cost low enough that projection stays under $3000
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m); // Low cost = low projection

        // Act
        await _service.CheckMonthlyCostProjectionAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_WhenProjectionAboveThreshold_SendsAlert()
    {
        // Arrange - Set cost high enough that projection exceeds $3000
        // If we're on day 1, cost of 150 projects to 150*30 = $4500
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var daysElapsed = (today.DayNumber - monthStart.DayNumber) + 1;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        // Calculate cost needed to exceed threshold
        // Projected = (cost / daysElapsed) * daysInMonth > 3000
        // cost > 3000 * daysElapsed / daysInMonth
        var costNeeded = (MonthlyThreshold * daysElapsed / daysInMonth) + 100m;

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(costNeeded);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckMonthlyCostProjectionAsync();

        // Assert - Use culture-invariant check (contains "Projected" and "3000" or "3,000" or "3.000")
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                "LLM_MONTHLY_COST_PROJECTION",
                "warning",
                It.Is<string>(s => s.Contains("Projected") && s.Contains("3000")),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_CalculatesProjectionCorrectly()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var daysElapsed = (today.DayNumber - monthStart.DayNumber) + 1;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        var currentCost = 200m * daysElapsed; // High daily average
        var expectedProjection = (currentCost / daysElapsed) * daysInMonth;

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCost);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckMonthlyCostProjectionAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<IDictionary<string, object>>(m =>
                    Math.Abs((decimal)m["projected_cost"] - expectedProjection) < 1m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_IncludesDailyAverage()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var daysElapsed = (today.DayNumber - monthStart.DayNumber) + 1;

        var currentCost = 200m * daysElapsed;
        var expectedDailyAverage = currentCost / daysElapsed;

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentCost);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckMonthlyCostProjectionAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<IDictionary<string, object>>(m =>
                    m.ContainsKey("daily_average") &&
                    Math.Abs((decimal)m["daily_average"] - expectedDailyAverage) < 0.01m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_QueriesFromMonthStart()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expectedMonthStart = new DateOnly(today.Year, today.Month, 1);

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(
                It.Is<DateOnly>(d => d == expectedMonthStart),
                It.Is<DateOnly>(d => d == today),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);

        // Act
        await _service.CheckMonthlyCostProjectionAsync();

        // Assert
        _costLogRepositoryMock.Verify(
            r => r.GetTotalCostAsync(expectedMonthStart, today, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_IncludesDaysElapsedAndTotal()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var expectedDaysElapsed = (today.DayNumber - monthStart.DayNumber) + 1;
        var expectedDaysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        // Need high cost to trigger alert
        var highCost = 200m * expectedDaysElapsed;

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(highCost);

        _alertingServiceMock
            .Setup(a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAlertDto());

        // Act
        await _service.CheckMonthlyCostProjectionAsync();

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains($"{expectedDaysElapsed}/{expectedDaysInMonth} days")),
                It.Is<IDictionary<string, object>>(m =>
                    (int)m["days_elapsed"] == expectedDaysElapsed &&
                    (int)m["days_in_month"] == expectedDaysInMonth),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task CheckDailyCostThresholdAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _costLogRepositoryMock
            .Setup(r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), token))
            .ReturnsAsync(50m);

        // Act
        await _service.CheckDailyCostThresholdAsync(token);

        // Assert
        _costLogRepositoryMock.Verify(
            r => r.GetDailyCostAsync(It.IsAny<DateOnly>(), token),
            Times.Once);
    }

    [Fact]
    public async Task CheckWeeklyCostThresholdAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), token))
            .ReturnsAsync(50m);

        // Act
        await _service.CheckWeeklyCostThresholdAsync(token);

        // Assert
        _costLogRepositoryMock.Verify(
            r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), token),
            Times.Once);
    }

    [Fact]
    public async Task CheckMonthlyCostProjectionAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), token))
            .ReturnsAsync(50m);

        // Act
        await _service.CheckMonthlyCostProjectionAsync(token);

        // Assert
        _costLogRepositoryMock.Verify(
            r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), token),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static AlertDto CreateAlertDto()
    {
        return new AlertDto(
            Id: Guid.NewGuid(),
            AlertType: "LLM_COST_THRESHOLD",
            Severity: "warning",
            Message: "Test alert",
            Metadata: null,
            TriggeredAt: DateTime.UtcNow,
            ResolvedAt: null,
            IsActive: true,
            ChannelSent: null);
    }

    #endregion
}

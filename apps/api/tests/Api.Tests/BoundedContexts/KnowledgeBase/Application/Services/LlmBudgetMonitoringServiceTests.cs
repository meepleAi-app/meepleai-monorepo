using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ISSUE-1725: Tests for LLM budget monitoring and alerting
/// </summary>
public class LlmBudgetMonitoringServiceTests
{
    private readonly Mock<ILlmCostLogRepository> _costLogRepositoryMock;
    private readonly Mock<IAlertingService> _alertingServiceMock;
    private readonly Mock<ILogger<LlmBudgetMonitoringService>> _loggerMock;
    private readonly IConfiguration _config;

    public LlmBudgetMonitoringServiceTests()
    {
        _costLogRepositoryMock = new Mock<ILlmCostLogRepository>();
        _alertingServiceMock = new Mock<IAlertingService>();
        _loggerMock = new Mock<ILogger<LlmBudgetMonitoringService>>();

        var configData = new Dictionary<string, string?>
        {
            { "LlmBudgetAlerts:DailyBudgetUsd", "50.00" },
            { "LlmBudgetAlerts:MonthlyBudgetUsd", "1000.00" },
            { "LlmBudgetAlerts:Thresholds:Warning", "0.80" },
            { "LlmBudgetAlerts:Thresholds:Critical", "0.95" },
            { "LlmBudgetAlerts:CheckIntervalMinutes", "1" }
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Act
        var sut = new LlmBudgetMonitoringService(
            _costLogRepositoryMock.Object,
            _alertingServiceMock.Object,
            _config,
            _loggerMock.Object);

        // Assert
        Assert.NotNull(sut);
    }

    [Fact]
    public async Task CheckBudgetThresholds_BelowWarning_NoAlertsTriggered()
    {
        // Arrange
        var sut = new LlmBudgetMonitoringService(
            _costLogRepositoryMock.Object,
            _alertingServiceMock.Object,
            _config,
            _loggerMock.Object);

        // Mock spend below warning threshold (30 / 50 = 60%)
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(30.00m);

        // Act - use reflection to call private method
        var method = typeof(LlmBudgetMonitoringService).GetMethod(
            "CheckBudgetThresholdsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;

        // Assert
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckBudgetThresholds_AtWarningLevel_SendsWarningAlert()
    {
        // Arrange
        var sut = new LlmBudgetMonitoringService(
            _costLogRepositoryMock.Object,
            _alertingServiceMock.Object,
            _config,
            _loggerMock.Object);

        // Mock spend at warning threshold (41 / 50 = 82% > 80% warning for daily)
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(41.00m);

        // Act
        var method = typeof(LlmBudgetMonitoringService).GetMethod(
            "CheckBudgetThresholdsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;

        // Assert - should send at least 1 warning alert (daily budget at 82%)
        // Monthly budget (41/1000 = 4.1%) is below warning threshold
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.Is<string>(s => s.Contains("Budget")),
                "Warning",
                It.Is<string>(m => m.Contains("Budget Alert")),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckBudgetThresholds_AtCriticalLevel_SendsErrorAlert()
    {
        // Arrange
        var sut = new LlmBudgetMonitoringService(
            _costLogRepositoryMock.Object,
            _alertingServiceMock.Object,
            _config,
            _loggerMock.Object);

        // Mock spend at critical threshold (48 / 50 = 96% > 95% critical for daily)
        _costLogRepositoryMock
            .Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(48.00m);

        // Act
        var method = typeof(LlmBudgetMonitoringService).GetMethod(
            "CheckBudgetThresholdsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;

        // Assert - should send at least 1 critical alert (daily budget at 96%)
        // Monthly budget (48/1000 = 4.8%) is below warning threshold
        _alertingServiceMock.Verify(
            a => a.SendAlertAsync(
                It.Is<string>(s => s.Contains("Critical")),
                "Error",
                It.Is<string>(m => m.Contains("Critical")),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}

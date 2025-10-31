using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for AlertingService (OPS-07).
/// Tests alert throttling, multi-channel distribution, and resolution logic.
/// </summary>
public class AlertingServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IAlertChannel> _emailChannelMock;
    private readonly Mock<IAlertChannel> _slackChannelMock;
    private readonly Mock<IAlertChannel> _pagerDutyChannelMock;
    private readonly Mock<ILogger<AlertingService>> _mockLogger;
    private readonly AlertingConfiguration _config;
    private readonly AlertingService _service;

    public AlertingServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Setup in-memory SQLite database
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup mocks
        _emailChannelMock = new Mock<IAlertChannel>();
        _emailChannelMock.Setup(c => c.ChannelName).Returns("Email");
        _emailChannelMock.Setup(c => c.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _slackChannelMock = new Mock<IAlertChannel>();
        _slackChannelMock.Setup(c => c.ChannelName).Returns("Slack");
        _slackChannelMock.Setup(c => c.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _pagerDutyChannelMock = new Mock<IAlertChannel>();
        _pagerDutyChannelMock.Setup(c => c.ChannelName).Returns("PagerDuty");
        _pagerDutyChannelMock.Setup(c => c.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _mockLogger = new Mock<ILogger<AlertingService>>();

        _config = new AlertingConfiguration
        {
            Enabled = true,
            ThrottleMinutes = 60
        };

        var channels = new List<IAlertChannel>
        {
            _emailChannelMock.Object,
            _slackChannelMock.Object,
            _pagerDutyChannelMock.Object
        };

        _service = new AlertingService(
            _dbContext,
            channels,
            Options.Create(_config),
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SendAlertAsync_CreatesAlert_WhenNotThrottled()
    {
        // Arrange
        var alertType = "HighErrorRate";
        var severity = "critical";
        var message = "Error rate exceeds threshold";

        // Act
        var result = await _service.SendAlertAsync(alertType, severity, message);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal(alertType, result.AlertType);
        Assert.Equal(severity, result.Severity);
        Assert.Equal(message, result.Message);
        Assert.True(result.IsActive);
        result.ResolvedAt.Should().BeNull();

        // Verify alert was saved to database
        var savedAlert = await _dbContext.Alerts.FirstOrDefaultAsync(a => a.AlertType == alertType);
        savedAlert.Should().NotBeNull();
        Assert.True(savedAlert.IsActive);
    }

    [Fact]
    public async Task SendAlertAsync_SendsToAllChannels()
    {
        // Arrange
        var alertType = "DatabaseDown";
        var severity = "critical";
        var message = "PostgreSQL connection failed";

        // Act
        await _service.SendAlertAsync(alertType, severity, message);

        // Assert
        _emailChannelMock.Verify(c => c.SendAsync(
            alertType,
            severity,
            message,
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _slackChannelMock.Verify(c => c.SendAsync(
            alertType,
            severity,
            message,
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _pagerDutyChannelMock.Verify(c => c.SendAsync(
            alertType,
            severity,
            message,
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAlertAsync_TracksChannelResults()
    {
        // Arrange
        var alertType = "QdrantDown";
        var severity = "critical";

        // Make Slack fail
        _slackChannelMock.Setup(c => c.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _service.SendAlertAsync(alertType, severity, "Qdrant is down");

        // Assert
        result.ChannelSent.Should().NotBeNull();
        Assert.True(result.ChannelSent["Email"]);
        Assert.False(result.ChannelSent["Slack"]);
        Assert.True(result.ChannelSent["PagerDuty"]);
    }

    [Fact]
    public async Task SendAlertAsync_ReturnsExisting_WhenThrottled()
    {
        // Arrange
        var alertType = "HighLatency";
        var severity = "warning";

        // Send first alert
        var firstAlert = await _service.SendAlertAsync(alertType, severity, "First alert");

        // Act - Second alert should be throttled and return the existing one
        var secondResult = await _service.SendAlertAsync(alertType, severity, "Second alert");

        // Assert - Returns the same alert (by ID)
        Assert.Equal(firstAlert.Id, secondResult.Id);
        Assert.Equal(firstAlert.Message, secondResult.Message);

        // Verify channels were called only once (for first alert)
        _emailChannelMock.Verify(c => c.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsThrottledAsync_ReturnsTrue_WhenRecentAlertExists()
    {
        // Arrange
        var alertType = "TestAlert";
        await _service.SendAlertAsync(alertType, "info", "Test");

        // Act
        var isThrottled = await _service.IsThrottledAsync(alertType);

        // Assert
        Assert.True(isThrottled);
    }

    [Fact]
    public async Task IsThrottledAsync_ReturnsFalse_WhenNoRecentAlert()
    {
        // Arrange
        var alertType = "NewAlert";

        // Act
        var isThrottled = await _service.IsThrottledAsync(alertType);

        // Assert
        Assert.False(isThrottled);
    }

    [Fact]
    public async Task ResolveAlertAsync_ResolvesActiveAlert()
    {
        // Arrange
        var alertType = "RedisDown";
        await _service.SendAlertAsync(alertType, "critical", "Redis is down");

        // Act
        var resolved = await _service.ResolveAlertAsync(alertType);

        // Assert
        Assert.True(resolved);

        var alert = await _dbContext.Alerts.FirstOrDefaultAsync(a => a.AlertType == alertType);
        alert.Should().NotBeNull();
        Assert.False(alert.IsActive);
        alert.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAlertAsync_ReturnsFalse_WhenNoActiveAlert()
    {
        // Arrange
        var alertType = "NonExistentAlert";

        // Act
        var resolved = await _service.ResolveAlertAsync(alertType);

        // Assert
        Assert.False(resolved);
    }

    [Fact]
    public async Task GetActiveAlertsAsync_ReturnsOnlyActiveAlerts()
    {
        // Arrange
        await _service.SendAlertAsync("Alert1", "critical", "Message 1");
        await _service.SendAlertAsync("Alert2", "warning", "Message 2");
        await _service.ResolveAlertAsync("Alert1");

        // Act
        var activeAlerts = await _service.GetActiveAlertsAsync();

        // Assert
        Assert.Single(activeAlerts);
        Assert.Equal("Alert2", activeAlerts[0].AlertType);
    }

    [Fact]
    public async Task GetAlertHistoryAsync_ReturnsAlertsInDateRange()
    {
        // Arrange
        await _service.SendAlertAsync("HistoricalAlert", "info", "Test");

        var fromDate = DateTime.UtcNow.AddHours(-1);
        var toDate = DateTime.UtcNow.AddHours(1);

        // Act
        var history = await _service.GetAlertHistoryAsync(fromDate, toDate);

        // Assert
        Assert.Single(history);
        Assert.Equal("HistoricalAlert", history[0].AlertType);
    }

    [Fact]
    public async Task SendAlertAsync_ThrowsException_WhenAlertingDisabled()
    {
        // Arrange
        _config.Enabled = false;
        var alertType = "TestAlert";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SendAlertAsync(alertType, "critical", "Test"));
    }
}

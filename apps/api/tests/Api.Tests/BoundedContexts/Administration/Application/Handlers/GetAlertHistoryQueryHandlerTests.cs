using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetAlertHistoryQueryHandler.
/// Tests retrieval of historical alerts within a date range.
/// </summary>
public class GetAlertHistoryQueryHandlerTests
{
    private readonly Mock<IAlertingService> _mockAlertingService;
    private readonly GetAlertHistoryQueryHandler _handler;

    public GetAlertHistoryQueryHandlerTests()
    {
        _mockAlertingService = new Mock<IAlertingService>();
        _handler = new GetAlertHistoryQueryHandler(_mockAlertingService.Object);
    }

    [Fact]
    public async Task Handle_WithDateRange_ReturnsHistoricalAlerts()
    {
        // Arrange
        var fromDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var query = new GetAlertHistoryQuery(fromDate, toDate);

        var expectedAlerts = new List<AlertDto>
        {
            new AlertDto
            {
                Id = Guid.NewGuid().ToString(),
                AlertType = "DatabaseError",
                Severity = "High",
                Message = "DB connection failed",
                IsActive = false,
                ResolvedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new AlertDto
            {
                Id = Guid.NewGuid().ToString(),
                AlertType = "ApiError",
                Severity = "Medium",
                Message = "API timeout",
                IsActive = false,
                ResolvedAt = new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        _mockAlertingService
            .Setup(s => s.GetAlertHistoryAsync(fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, alert => Assert.False(alert.IsActive));
        Assert.All(result, alert => Assert.NotNull(alert.ResolvedAt));
        _mockAlertingService.Verify(
            s => s.GetAlertHistoryAsync(fromDate, toDate, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoAlertsInRange_ReturnsEmptyList()
    {
        // Arrange
        var fromDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2025, 2, 28, 23, 59, 59, DateTimeKind.Utc);
        var query = new GetAlertHistoryQuery(fromDate, toDate);

        _mockAlertingService
            .Setup(s => s.GetAlertHistoryAsync(fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertDto>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithSingleDayRange_ReturnsAlertsForThatDay()
    {
        // Arrange
        var singleDay = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var query = new GetAlertHistoryQuery(singleDay, singleDay.AddDays(1).AddTicks(-1));

        var expectedAlerts = new List<AlertDto>
        {
            new AlertDto
            {
                Id = Guid.NewGuid().ToString(),
                AlertType = "CriticalError",
                Severity = "Critical",
                Message = "System failure",
                IsActive = false,
                ResolvedAt = singleDay.AddHours(12)
            }
        };

        _mockAlertingService
            .Setup(s => s.GetAlertHistoryAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("CriticalError", result[0].AlertType);
    }
}

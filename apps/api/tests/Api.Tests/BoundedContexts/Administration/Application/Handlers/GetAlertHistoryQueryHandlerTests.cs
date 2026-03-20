using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetAlertHistoryQueryHandler.
/// Tests retrieval of historical alerts within a date range.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
            new AlertDto(
                Id: Guid.NewGuid(),
                AlertType: "DatabaseError",
                Severity: "High",
                Message: "DB connection failed",
                Metadata: null,
                TriggeredAt: new DateTime(2025, 1, 14, 0, 0, 0, DateTimeKind.Utc),
                ResolvedAt: new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive: false,
                ChannelSent: null
            ),
            new AlertDto(
                Id: Guid.NewGuid(),
                AlertType: "ApiError",
                Severity: "Medium",
                Message: "API timeout",
                Metadata: null,
                TriggeredAt: new DateTime(2025, 1, 19, 0, 0, 0, DateTimeKind.Utc),
                ResolvedAt: new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                IsActive: false,
                ChannelSent: null
            )
        };

        _mockAlertingService
            .Setup(s => s.GetAlertHistoryAsync(fromDate, toDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().AllSatisfy(alert => alert.IsActive.Should().BeFalse());
        result.Should().AllSatisfy(alert => alert.ResolvedAt.Should().NotBeNull());
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
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithSingleDayRange_ReturnsAlertsForThatDay()
    {
        // Arrange
        var singleDay = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var query = new GetAlertHistoryQuery(singleDay, singleDay.AddDays(1).AddTicks(-1));

        var expectedAlerts = new List<AlertDto>
        {
            new AlertDto(
                Id: Guid.NewGuid(),
                AlertType: "CriticalError",
                Severity: "Critical",
                Message: "System failure",
                Metadata: null,
                TriggeredAt: singleDay,
                ResolvedAt: singleDay.AddHours(12),
                IsActive: false,
                ChannelSent: null
            )
        };

        _mockAlertingService
            .Setup(s => s.GetAlertHistoryAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].AlertType.Should().Be("CriticalError");
    }
}
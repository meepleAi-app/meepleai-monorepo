using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Tests for GetActiveAlertsQueryHandler.
/// Tests retrieval of active alerts.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetActiveAlertsQueryHandlerTests
{
    private readonly Mock<IAlertingService> _mockAlertingService;
    private readonly GetActiveAlertsQueryHandler _handler;

    public GetActiveAlertsQueryHandlerTests()
    {
        _mockAlertingService = new Mock<IAlertingService>();
        _handler = new GetActiveAlertsQueryHandler(_mockAlertingService.Object);
    }

    [Fact]
    public async Task Handle_ReturnsActiveAlerts()
    {
        // Arrange
        var query = new GetActiveAlertsQuery();
        var expectedAlerts = new List<AlertDto>
        {
            new AlertDto(
                Id: Guid.NewGuid(),
                AlertType: "DatabaseError",
                Severity: "High",
                Message: "DB connection failed",
                Metadata: null,
                TriggeredAt: DateTime.UtcNow,
                ResolvedAt: null,
                IsActive: true,
                ChannelSent: null
            ),
            new AlertDto(
                Id: Guid.NewGuid(),
                AlertType: "ApiError",
                Severity: "Medium",
                Message: "API timeout",
                Metadata: null,
                TriggeredAt: DateTime.UtcNow,
                ResolvedAt: null,
                IsActive: true,
                ChannelSent: null
            )
        };

        _mockAlertingService
            .Setup(s => s.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlerts);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, alert => Assert.True(alert.IsActive));
        _mockAlertingService.Verify(
            s => s.GetActiveAlertsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoActiveAlerts_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetActiveAlertsQuery();
        _mockAlertingService
            .Setup(s => s.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertDto>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for SendAlertCommandHandler.
/// Tests handler delegation to AlertingService.
/// </summary>
public class SendAlertCommandHandlerTests
{
    private readonly Mock<AlertingService> _mockAlertingService;
    private readonly SendAlertCommandHandler _handler;

    public SendAlertCommandHandlerTests()
    {
        _mockAlertingService = new Mock<AlertingService>();
        _handler = new SendAlertCommandHandler(_mockAlertingService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsAlertingService()
    {
        // Arrange
        var command = new SendAlertCommand(
            "DatabaseError",
            "Critical",
            "Database connection failed",
            new Dictionary<string, object> { ["server"] = "db-01" }
        );

        var expectedAlert = new AlertDto(
            Id: Guid.NewGuid(),
            AlertType: command.AlertType,
            Severity: command.Severity.ToString(),
            Message: command.Message,
            Metadata: command.Metadata,
            TriggeredAt: DateTime.UtcNow,
            ResolvedAt: null,
            IsActive: true,
            ChannelSent: null
        );

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                command.AlertType,
                command.Severity,
                command.Message,
                command.Metadata,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlert);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAlert.AlertType, result.AlertType);
        Assert.Equal(expectedAlert.Message, result.Message);
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                command.AlertType,
                command.Severity,
                command.Message,
                command.Metadata,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCriticalSeverity_CreatesAlert()
    {
        // Arrange
        var command = new SendAlertCommand(
            "CriticalError",
            "Critical",
            "System failure",
            null
        );

        var expectedAlert = new AlertDto(
            Id: Guid.NewGuid(),
            AlertType: "CriticalError",
            Severity: "Critical",
            Message: "System failure",
            Metadata: null,
            TriggeredAt: DateTime.UtcNow,
            ResolvedAt: null,
            IsActive: true,
            ChannelSent: null
        );

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAlert);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Critical", result.Severity);
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                command.AlertType,
                command.Severity.ToString(),
                command.Message,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

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
            AlertSeverity.High,
            "Database connection failed",
            "{\"server\": \"db-01\"}"
        );

        var expectedAlert = new AlertDto
        {
            Id = Guid.NewGuid().ToString(),
            AlertType = command.AlertType,
            Severity = command.Severity.ToString(),
            Message = command.Message,
            Metadata = command.Metadata,
            IsActive = true
        };

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
            AlertSeverity.Critical,
            "System failure"
        );

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                It.IsAny<string>(),
                AlertSeverity.Critical,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlertDto { Severity = "Critical" });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Critical", result.Severity);
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                command.AlertType,
                AlertSeverity.Critical,
                command.Message,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

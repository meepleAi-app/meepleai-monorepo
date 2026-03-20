using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for TestAlertCommandHandler.
/// Tests alert testing functionality with service integration and error handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class TestAlertCommandHandlerTests
{
    private readonly Mock<IAlertingService> _mockAlertingService;
    private readonly Mock<ILogger<TestAlertCommandHandler>> _mockLogger;
    private readonly TestAlertCommandHandler _handler;

    public TestAlertCommandHandlerTests()
    {
        _mockAlertingService = new Mock<IAlertingService>();
        _mockLogger = new Mock<ILogger<TestAlertCommandHandler>>();
        _handler = new TestAlertCommandHandler(_mockAlertingService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SendsTestAlertSuccessfully()
    {
        // Arrange
        var command = new TestAlertCommand("DatabaseError", "email");

        var alertDto = new AlertDto(
            Guid.NewGuid(),
            "DatabaseError",
            "Critical",
            "Test alert",
            null,
            DateTime.UtcNow,
            null,
            true,
            null
        );

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                "DatabaseError",
                "Critical",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(alertDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _mockAlertingService.Verify(s => s.SendAlertAsync(
            "DatabaseError",
            "Critical",
            It.Is<string>(msg => msg.Contains("DatabaseError") && msg.Contains("email")),
            It.Is<Dictionary<string, object>>(dict =>
                dict.ContainsKey("test") && dict["test"].Equals(true) &&
                dict.ContainsKey("channel") && dict["channel"].Equals("email")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlertingServiceThrows_ReturnsFalse()
    {
        // Arrange
        var command = new TestAlertCommand("ApiError", "slack");

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send test alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDifferentAlertTypes_SendsWithCorrectParameters()
    {
        // Arrange
        var command = new TestAlertCommand("PerformanceIssue", "webhook");

        var alertDto = new AlertDto(
            Guid.NewGuid(),
            "PerformanceIssue",
            "Critical",
            "Test alert",
            null,
            DateTime.UtcNow,
            null,
            true,
            null
        );

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                "PerformanceIssue",
                "Critical",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(alertDto);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _mockAlertingService.Verify(s => s.SendAlertAsync(
            "PerformanceIssue",
            "Critical",
            It.Is<string>(msg => msg.Contains("PerformanceIssue") && msg.Contains("webhook")),
            It.Is<Dictionary<string, object>>(dict => dict["channel"].Equals("webhook")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        TestAlertCommand? command = null;

        // Act & Assert
        var act = () =>
            _handler.Handle(command!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockAlertingService.Verify(s => s.SendAlertAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, object>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
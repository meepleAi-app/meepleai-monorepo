using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Models;
using Api.Services;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;
using System.Globalization;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for SendAlertCommandHandler.
/// Tests handler delegation to AlertingService.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SendAlertCommandHandlerTests
{
    private readonly Mock<IAlertingService> _mockAlertingService;
    private readonly SendAlertCommandHandler _handler;

    public SendAlertCommandHandlerTests()
    {
        _mockAlertingService = new Mock<IAlertingService>();
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.AlertType.Should().Be(expectedAlert.AlertType);
        result.Message.Should().Be(expectedAlert.Message);
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Severity.Should().Be("Critical");
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                command.AlertType,
                command.Severity.ToString(),
                command.Message,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithQualityEvaluationAlert_IncludesMetadata()
    {
        // Arrange - Scenario: Weekly evaluation triggers quality alert
        var metadata = new Dictionary<string, object>
        {
            { "Issues", new List<string> { "Low quality percentage high", "Overall confidence low" } },
            { "IssueCount", 2 },
            { "StartDate", DateTime.Parse("2025-01-08", CultureInfo.InvariantCulture) },
            { "EndDate", DateTime.Parse("2025-01-15", CultureInfo.InvariantCulture) },
            { "LowQualityPercentage", 15.0 }
        };

        var command = new SendAlertCommand(
            "QualityEvaluation",
            "Warning",
            "Weekly quality evaluation detected 2 issue(s)",
            metadata
        );

        var expectedAlert = new AlertDto(
            Id: Guid.NewGuid(),
            AlertType: "QualityEvaluation",
            Severity: "Warning",
            Message: command.Message,
            Metadata: metadata,
            TriggeredAt: DateTime.UtcNow,
            ResolvedAt: null,
            IsActive: true,
            ChannelSent: new Dictionary<string, bool> { { "Email", true }, { "Slack", true } }
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.AlertType.Should().Be("QualityEvaluation");
        result.Severity.Should().Be("Warning");
        result.Metadata.Should().NotBeNull();
        result.Metadata["IssueCount"].Should().Be(2);
        result.ChannelSent.Should().NotBeNull();
        result.ChannelSent["Email"].Should().BeTrue();
        result.ChannelSent["Slack"].Should().BeTrue();

        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                "QualityEvaluation",
                "Warning",
                It.Is<string>(msg => msg.Contains("2 issue(s)")),
                It.Is<Dictionary<string, object>>(m =>
                    m.ContainsKey("Issues") &&
                    m.ContainsKey("IssueCount") &&
                    (int)m["IssueCount"] == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullMetadata_SuccessfullyCreatesAlert()
    {
        // Arrange
        var command = new SendAlertCommand(
            "SimpleAlert",
            "Info",
            "Simple alert without metadata",
            null
        );

        var expectedAlert = new AlertDto(
            Id: Guid.NewGuid(),
            AlertType: "SimpleAlert",
            Severity: "Info",
            Message: command.Message,
            Metadata: null,
            TriggeredAt: DateTime.UtcNow,
            ResolvedAt: null,
            IsActive: true,
            ChannelSent: new Dictionary<string, bool> { { "Email", true } }
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().BeNull();
        result.Severity.Should().Be("Info");
    }

    [Fact]
    public async Task Handle_PropagatesException_WhenAlertingServiceFails()
    {
        // Arrange
        var command = new SendAlertCommand(
            "TestAlert",
            "Error",
            "Test alert",
            null
        );

        _mockAlertingService
            .Setup(s => s.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Alerting system is disabled"));

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullAlertingService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new SendAlertCommandHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Critical")]
    [InlineData("Error")]
    [InlineData("Warning")]
    [InlineData("Info")]
    public async Task Handle_WithDifferentSeverityLevels_CreatesCorrectAlert(string severity)
    {
        // Arrange
        var command = new SendAlertCommand(
            "TestAlert",
            severity,
            $"Alert with {severity} severity",
            null
        );

        var expectedAlert = new AlertDto(
            Id: Guid.NewGuid(),
            AlertType: "TestAlert",
            Severity: severity,
            Message: command.Message,
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
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Severity.Should().Be(severity);
        _mockAlertingService.Verify(
            s => s.SendAlertAsync(
                "TestAlert",
                severity,
                It.IsAny<string>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

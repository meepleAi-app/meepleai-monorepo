using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Handlers.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for CreateAlertRuleCommandHandler.
/// Tests alert rule creation with domain validation and repository interaction.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateAlertRuleCommandHandlerTests
{
    private readonly Mock<IAlertRuleRepository> _mockRepository;
    private readonly CreateAlertRuleCommandHandler _handler;

    public CreateAlertRuleCommandHandlerTests()
    {
        _mockRepository = new Mock<IAlertRuleRepository>();
        _handler = new CreateAlertRuleCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesAlertRuleSuccessfully()
    {
        // Arrange
        var command = new CreateAlertRuleCommand(
            Name: "High CPU Usage",
            AlertType: "SystemMetric",
            Severity: "Critical",
            ThresholdValue: 85.0,
            ThresholdUnit: "percentage",
            DurationMinutes: 5,
            Description: "Triggers when CPU exceeds 85% for 5 minutes",
            CreatedBy: "admin@test.com"
        );

        AlertRule? capturedRule = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .Callback<AlertRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedRule.Should().NotBeNull();
        capturedRule!.Name.Should().Be("High CPU Usage");
        capturedRule.AlertType.Should().Be("SystemMetric");
        capturedRule.Severity.Should().Be(AlertSeverity.Critical);
        capturedRule.Description.Should().Be("Triggers when CPU exceeds 85% for 5 minutes");
        capturedRule.IsEnabled.Should().BeTrue();
        capturedRule.CreatedBy.Should().Be("admin@test.com");

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInfoSeverity_CreatesAlertRuleWithCorrectSeverity()
    {
        // Arrange
        var command = new CreateAlertRuleCommand(
            Name: "User Login",
            AlertType: "AuditEvent",
            Severity: "Info",
            ThresholdValue: 1.0,
            ThresholdUnit: "count",
            DurationMinutes: 1,
            Description: null,
            CreatedBy: "system@test.com"
        );

        AlertRule? capturedRule = null;
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .Callback<AlertRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedRule.Should().NotBeNull();
        capturedRule!.Severity.Should().Be(AlertSeverity.Info);
        capturedRule.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        CreateAlertRuleCommand? command = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(command!, CancellationToken.None));

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

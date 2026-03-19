using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Handlers.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Tests for UpdateAlertRuleCommandHandler.
/// Tests alert rule updates with validation and not found scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateAlertRuleCommandHandlerTests
{
    private readonly Mock<IAlertRuleRepository> _mockRepository;
    private readonly UpdateAlertRuleCommandHandler _handler;

    public UpdateAlertRuleCommandHandlerTests()
    {
        _mockRepository = new Mock<IAlertRuleRepository>();
        _handler = new UpdateAlertRuleCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesAlertRuleSuccessfully()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var existingRule = AlertRule.Create(
            "Old Name",
            "SystemMetric",
            AlertSeverity.Warning,
            new AlertThreshold(50.0, "percentage"),
            new AlertDuration(10),
            "creator@test.com"
        );

        var command = new UpdateAlertRuleCommand(
            Id: ruleId,
            Name: "Updated High CPU Usage",
            Severity: "Critical",
            ThresholdValue: 90.0,
            ThresholdUnit: "percentage",
            DurationMinutes: 3,
            Description: "Updated description",
            UpdatedBy: "admin@test.com"
        );

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);

        AlertRule? capturedRule = null;
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .Callback<AlertRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        capturedRule.Should().NotBeNull();
        capturedRule!.Name.Should().Be("Updated High CPU Usage");
        capturedRule.Severity.Should().Be(AlertSeverity.Critical);
        capturedRule.Description.Should().Be("Updated description");

        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlertRuleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var command = new UpdateAlertRuleCommand(
            Id: ruleId,
            Name: "Updated Name",
            Severity: "Error",
            ThresholdValue: 70.0,
            ThresholdUnit: "percentage",
            DurationMinutes: 5,
            Description: null,
            UpdatedBy: "admin@test.com"
        );

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain(ruleId.ToString());
        exception.Message.Should().Contain("not found");

        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        UpdateAlertRuleCommand? command = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(command!, CancellationToken.None));

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

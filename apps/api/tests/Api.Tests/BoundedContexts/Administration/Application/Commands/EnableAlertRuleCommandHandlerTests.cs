using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Tests for EnableAlertRuleCommandHandler.
/// Tests alert rule enable/disable toggle functionality.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EnableAlertRuleCommandHandlerTests
{
    private readonly Mock<IAlertRuleRepository> _mockRepository;
    private readonly EnableAlertRuleCommandHandler _handler;

    public EnableAlertRuleCommandHandlerTests()
    {
        _mockRepository = new Mock<IAlertRuleRepository>();
        _handler = new EnableAlertRuleCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WhenRuleIsEnabled_DisablesRule()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var enabledRule = AlertRule.Create(
            "Test Rule",
            "SystemMetric",
            AlertSeverity.Warning,
            new AlertThreshold(70.0, "percentage"),
            new AlertDuration(5),
            "creator@test.com"
        );

        var command = new EnableAlertRuleCommand(ruleId, "admin@test.com");

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(enabledRule);

        AlertRule? capturedRule = null;
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .Callback<AlertRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        capturedRule.Should().NotBeNull();
        capturedRule!.IsEnabled.Should().BeFalse();

        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRuleIsDisabled_EnablesRule()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var disabledRule = AlertRule.Create(
            "Test Rule",
            "SystemMetric",
            AlertSeverity.Error,
            new AlertThreshold(80.0, "percentage"),
            new AlertDuration(10),
            "creator@test.com"
        );
        disabledRule.Disable("admin@test.com");

        var command = new EnableAlertRuleCommand(ruleId, "admin@test.com");

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(disabledRule);

        AlertRule? capturedRule = null;
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .Callback<AlertRule, CancellationToken>((rule, _) => capturedRule = rule)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        capturedRule.Should().NotBeNull();
        capturedRule!.IsEnabled.Should().BeTrue();

        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlertRuleNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var command = new EnableAlertRuleCommand(ruleId, "admin@test.com");

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
        EnableAlertRuleCommand? command = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(command!, CancellationToken.None));

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // === VALIDATION FAILURE TESTS (Week 10-11: Validation branch coverage) ===

    [Fact]
    public async Task Handle_EmptyRuleId_ThrowsValidationException()
    {
        // Arrange
        var command = new EnableAlertRuleCommand(Guid.Empty, "admin@test.com");

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_EmptyUpdatedBy_ThrowsValidationException(string emptyUpdatedBy)
    {
        // Arrange
        var command = new EnableAlertRuleCommand(Guid.NewGuid(), emptyUpdatedBy);

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

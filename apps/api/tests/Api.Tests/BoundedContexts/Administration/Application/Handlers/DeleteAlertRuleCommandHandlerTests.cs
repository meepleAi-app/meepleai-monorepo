using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Handlers.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for DeleteAlertRuleCommandHandler.
/// Tests alert rule deletion with repository interaction.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DeleteAlertRuleCommandHandlerTests
{
    private readonly Mock<IAlertRuleRepository> _mockRepository;
    private readonly DeleteAlertRuleCommandHandler _handler;

    public DeleteAlertRuleCommandHandlerTests()
    {
        _mockRepository = new Mock<IAlertRuleRepository>();
        _handler = new DeleteAlertRuleCommandHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_DeletesAlertRuleSuccessfully()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var command = new DeleteAlertRuleCommand(ruleId);

        var existingRule = AlertRule.Create(
            "Test Rule",
            "SystemMetric",
            AlertSeverity.Warning,
            new AlertThreshold(70.0, "percentage"),
            new AlertDuration(5),
            "creator@test.com"
        );

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);
        _mockRepository.Setup(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDifferentId_CallsRepositoryWithCorrectId()
    {
        // Arrange
        var ruleId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var command = new DeleteAlertRuleCommand(ruleId);

        var existingRule = AlertRule.Create(
            "Another Rule",
            "SystemMetric",
            AlertSeverity.Error,
            new AlertThreshold(80.0, "percentage"),
            new AlertDuration(10),
            "creator@test.com"
        );

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);
        _mockRepository.Setup(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        DeleteAlertRuleCommand? command = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(command!, CancellationToken.None));

        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // === VALIDATION FAILURE TESTS (Week 10-11: Validation branch coverage) ===

    [Fact]
    public async Task Handle_EmptyRuleId_ThrowsValidationException()
    {
        // Arrange
        var command = new DeleteAlertRuleCommand(Guid.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<Api.SharedKernel.Domain.Exceptions.ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _mockRepository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RuleNotFound_ThrowsDomainException()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var command = new DeleteAlertRuleCommand(ruleId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule?)null);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _mockRepository.Verify(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()), Times.Never);
    }
}

using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Queries.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetAlertRuleByIdQueryHandler.
/// Tests alert rule retrieval by ID with null handling and mapping validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAlertRuleByIdQueryHandlerTests
{
    private readonly Mock<IAlertRuleRepository> _mockRepository;
    private readonly GetAlertRuleByIdQueryHandler _handler;

    public GetAlertRuleByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IAlertRuleRepository>();
        _handler = new GetAlertRuleByIdQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidId_ReturnsAlertRuleDto()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var rule = AlertRule.Create(
            "High CPU Usage",
            "SystemMetric",
            AlertSeverity.Critical,
            new AlertThreshold(85.0, "percentage"),
            new AlertDuration(5),
            "admin@test.com"
        );

        var query = new GetAlertRuleByIdQuery(ruleId);

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(rule.Id);
        result.Name.Should().Be("High CPU Usage");
        result.AlertType.Should().Be("SystemMetric");
        result.Severity.Should().Be("Critical");
        result.ThresholdValue.Should().Be(85.0);
        result.ThresholdUnit.Should().Be("percentage");
        result.DurationMinutes.Should().Be(5);
        result.IsEnabled.Should().BeTrue();

        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var query = new GetAlertRuleByIdQuery(ruleId);

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();

        _mockRepository.Verify(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDisabledRule_ReturnsDtoWithCorrectEnabledStatus()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var rule = AlertRule.Create(
            "Memory Alert",
            "SystemMetric",
            AlertSeverity.Warning,
            new AlertThreshold(70.0, "percentage"),
            new AlertDuration(10),
            "admin@test.com"
        );
        rule.Disable("admin@test.com");

        var query = new GetAlertRuleByIdQuery(ruleId);

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithRuleWithDescription_ReturnsDtoWithDescription()
    {
        // Arrange
        var ruleId = Guid.NewGuid();
        var rule = AlertRule.Create(
            "Test Rule",
            "AuditEvent",
            AlertSeverity.Info,
            new AlertThreshold(1.0, "count"),
            new AlertDuration(1),
            "system@test.com"
        );

        var query = new GetAlertRuleByIdQuery(ruleId);

        _mockRepository.Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rule);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be(rule.Description);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        GetAlertRuleByIdQuery? query = null;

        // Act & Assert
        var act = () =>
            _handler.Handle(query!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GetAlertRuleByIdQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

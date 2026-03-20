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
/// Tests for GetAllAlertRulesQueryHandler.
/// Tests alert rule listing with various rule configurations and empty scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAllAlertRulesQueryHandlerTests
{
    private readonly Mock<IAlertRuleRepository> _mockRepository;
    private readonly GetAllAlertRulesQueryHandler _handler;

    public GetAllAlertRulesQueryHandlerTests()
    {
        _mockRepository = new Mock<IAlertRuleRepository>();
        _handler = new GetAllAlertRulesQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithMultipleRules_ReturnsAllRulesAsDtos()
    {
        // Arrange
        var rule1 = AlertRule.Create(
            "High CPU",
            "SystemMetric",
            AlertSeverity.Critical,
            new AlertThreshold(90.0, "percentage"),
            new AlertDuration(5),
            "admin@test.com"
        );

        var rule2 = AlertRule.Create(
            "Memory Warning",
            "SystemMetric",
            AlertSeverity.Warning,
            new AlertThreshold(75.0, "percentage"),
            new AlertDuration(10),
            "admin@test.com"
        );

        var rule3 = AlertRule.Create(
            "Disk Space",
            "SystemMetric",
            AlertSeverity.Error,
            new AlertThreshold(80.0, "percentage"),
            new AlertDuration(15),
            "system@test.com"
        );

        var rules = new List<AlertRule> { rule1, rule2, rule3 };
        var query = new GetAllAlertRulesQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("High CPU");
        result[0].Severity.Should().Be("Critical");
        result[0].ThresholdValue.Should().Be(90.0);
        result[1].Name.Should().Be("Memory Warning");
        result[1].Severity.Should().Be("Warning");
        result[2].Name.Should().Be("Disk Space");
        result[2].Severity.Should().Be("Error");

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyRuleSet_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetAllAlertRulesQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertRule>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();

        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEnabledAndDisabledRules_ReturnsBothWithCorrectStatus()
    {
        // Arrange
        var enabledRule = AlertRule.Create(
            "Enabled Rule",
            "SystemMetric",
            AlertSeverity.Warning,
            new AlertThreshold(70.0, "percentage"),
            new AlertDuration(5),
            "admin@test.com"
        );

        var disabledRule = AlertRule.Create(
            "Disabled Rule",
            "AuditEvent",
            AlertSeverity.Info,
            new AlertThreshold(1.0, "count"),
            new AlertDuration(1),
            "admin@test.com"
        );
        disabledRule.Disable("admin@test.com");

        var rules = new List<AlertRule> { enabledRule, disabledRule };
        var query = new GetAllAlertRulesQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].IsEnabled.Should().BeTrue();
        result[1].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithSingleRule_ReturnsListWithOneElement()
    {
        // Arrange
        var rule = AlertRule.Create(
            "Only Rule",
            "SystemMetric",
            AlertSeverity.Critical,
            new AlertThreshold(95.0, "percentage"),
            new AlertDuration(3),
            "admin@test.com"
        );

        var rules = new List<AlertRule> { rule };
        var query = new GetAllAlertRulesQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("Only Rule");
        result[0].Severity.Should().Be("Critical");
        result[0].ThresholdValue.Should().Be(95.0);
        result[0].DurationMinutes.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithRulesOfDifferentTypes_ReturnsAllWithCorrectMapping()
    {
        // Arrange
        var systemRule = AlertRule.Create(
            "System Rule",
            "SystemMetric",
            AlertSeverity.Critical,
            new AlertThreshold(90.0, "percentage"),
            new AlertDuration(5),
            "admin@test.com"
        );

        var auditRule = AlertRule.Create(
            "Audit Rule",
            "AuditEvent",
            AlertSeverity.Info,
            new AlertThreshold(1.0, "count"),
            new AlertDuration(1),
            "system@test.com"
        );

        var rules = new List<AlertRule> { systemRule, auditRule };
        var query = new GetAllAlertRulesQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].AlertType.Should().Be("SystemMetric");
        result[1].AlertType.Should().Be("AuditEvent");
    }

    [Fact]
    public async Task Handle_VerifiesMappingOfAllProperties()
    {
        // Arrange
        var rule = AlertRule.Create(
            "Test Mapping",
            "CustomType",
            AlertSeverity.Error,
            new AlertThreshold(50.5, "custom"),
            new AlertDuration(20),
            "creator@test.com"
        );

        var rules = new List<AlertRule> { rule };
        var query = new GetAllAlertRulesQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var dto = result[0];
        dto.Id.Should().Be(rule.Id);
        dto.Name.Should().Be("Test Mapping");
        dto.AlertType.Should().Be("CustomType");
        dto.Severity.Should().Be("Error");
        dto.ThresholdValue.Should().Be(50.5);
        dto.ThresholdUnit.Should().Be("custom");
        dto.DurationMinutes.Should().Be(20);
        dto.IsEnabled.Should().BeTrue();
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        dto.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GetAllAlertRulesQueryHandler(null!);
act.Should().Throw<ArgumentNullException>();
    }
}
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Aggregates;

/// <summary>
/// Tests for the AlertRule aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 21
/// </summary>
[Trait("Category", "Unit")]
public sealed class AlertRuleTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesRule()
    {
        // Act
        var rule = AlertRule.Create(
            name: "High CPU Usage",
            alertType: "cpu_usage",
            severity: AlertSeverity.Warning,
            threshold: AlertThreshold.Percentage(80),
            duration: AlertDuration.FiveMinutes,
            createdBy: "admin",
            description: "Alert when CPU exceeds 80%");

        // Assert
        rule.Id.Should().NotBe(Guid.Empty);
        rule.Name.Should().Be("High CPU Usage");
        rule.AlertType.Should().Be("cpu_usage");
        rule.Severity.Should().Be(AlertSeverity.Warning);
        rule.Threshold.Value.Should().Be(80);
        rule.Duration.Minutes.Should().Be(5);
        rule.IsEnabled.Should().BeTrue();
        rule.CreatedBy.Should().Be("admin");
        rule.UpdatedBy.Should().Be("admin");
        rule.Description.Should().Be("Alert when CPU exceeds 80%");
        rule.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        rule.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithoutDescription_CreatesRuleWithNullDescription()
    {
        // Act
        var rule = AlertRule.Create(
            name: "Memory Alert",
            alertType: "memory_usage",
            severity: AlertSeverity.Error,
            threshold: AlertThreshold.Percentage(90),
            duration: AlertDuration.TenMinutes,
            createdBy: "admin");

        // Assert
        rule.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Act
        var action = () => AlertRule.Create(
            name: "",
            alertType: "cpu_usage",
            severity: AlertSeverity.Warning,
            threshold: AlertThreshold.Percentage(80),
            duration: AlertDuration.FiveMinutes,
            createdBy: "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Alert rule name cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceName_ThrowsArgumentException()
    {
        // Act
        var action = () => AlertRule.Create(
            name: "   ",
            alertType: "cpu_usage",
            severity: AlertSeverity.Warning,
            threshold: AlertThreshold.Percentage(80),
            duration: AlertDuration.FiveMinutes,
            createdBy: "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Alert rule name cannot be empty*");
    }

    #endregion

    #region Reconstitute Tests

    [Fact]
    public void Reconstitute_RestoresAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-7);
        var updatedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var rule = AlertRule.Reconstitute(
            id: id,
            name: "Restored Rule",
            alertType: "api_latency",
            severity: AlertSeverity.Critical,
            threshold: AlertThreshold.Milliseconds(500),
            duration: AlertDuration.FifteenMinutes,
            isEnabled: false,
            description: "Restored description",
            metadata: "{\"key\":\"value\"}",
            createdAt: createdAt,
            updatedAt: updatedAt,
            createdBy: "original-admin",
            updatedBy: "update-admin");

        // Assert
        rule.Id.Should().Be(id);
        rule.Name.Should().Be("Restored Rule");
        rule.AlertType.Should().Be("api_latency");
        rule.Severity.Should().Be(AlertSeverity.Critical);
        rule.Threshold.Value.Should().Be(500);
        rule.Duration.Minutes.Should().Be(15);
        rule.IsEnabled.Should().BeFalse();
        rule.Description.Should().Be("Restored description");
        rule.Metadata.Should().Be("{\"key\":\"value\"}");
        rule.CreatedAt.Should().Be(createdAt);
        rule.UpdatedAt.Should().Be(updatedAt);
        rule.CreatedBy.Should().Be("original-admin");
        rule.UpdatedBy.Should().Be("update-admin");
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_UpdatesAllFields()
    {
        // Arrange
        var rule = CreateTestRule();
        var originalUpdatedAt = rule.UpdatedAt;
        Thread.Sleep(10);

        // Act
        rule.Update(
            name: "Updated Name",
            severity: AlertSeverity.Critical,
            threshold: AlertThreshold.Percentage(95),
            duration: AlertDuration.TenMinutes,
            updatedBy: "admin2",
            description: "Updated description");

        // Assert
        rule.Name.Should().Be("Updated Name");
        rule.Severity.Should().Be(AlertSeverity.Critical);
        rule.Threshold.Value.Should().Be(95);
        rule.Duration.Minutes.Should().Be(10);
        rule.Description.Should().Be("Updated description");
        rule.UpdatedBy.Should().Be("admin2");
        rule.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region Enable/Disable Tests

    [Fact]
    public void Enable_WhenDisabled_EnablesRule()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.Disable("admin");
        var originalUpdatedAt = rule.UpdatedAt;
        Thread.Sleep(10);

        // Act
        rule.Enable("admin2");

        // Assert
        rule.IsEnabled.Should().BeTrue();
        rule.UpdatedBy.Should().Be("admin2");
        rule.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_DoesNotUpdateTimestamp()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.IsEnabled.Should().BeTrue();
        var originalUpdatedAt = rule.UpdatedAt;
        Thread.Sleep(10);

        // Act
        rule.Enable("admin2");

        // Assert
        rule.IsEnabled.Should().BeTrue();
        rule.UpdatedAt.Should().Be(originalUpdatedAt); // No change
    }

    [Fact]
    public void Disable_WhenEnabled_DisablesRule()
    {
        // Arrange
        var rule = CreateTestRule();
        var originalUpdatedAt = rule.UpdatedAt;
        Thread.Sleep(10);

        // Act
        rule.Disable("admin2");

        // Assert
        rule.IsEnabled.Should().BeFalse();
        rule.UpdatedBy.Should().Be("admin2");
        rule.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_DoesNotUpdateTimestamp()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.Disable("admin");
        var originalUpdatedAt = rule.UpdatedAt;
        Thread.Sleep(10);

        // Act
        rule.Disable("admin2");

        // Assert
        rule.IsEnabled.Should().BeFalse();
        rule.UpdatedAt.Should().Be(originalUpdatedAt); // No change
    }

    #endregion

    #region SetMetadata Tests

    [Fact]
    public void SetMetadata_SetsMetadataValue()
    {
        // Arrange
        var rule = CreateTestRule();
        var originalUpdatedAt = rule.UpdatedAt;
        Thread.Sleep(10);

        // Act
        rule.SetMetadata("{\"team\":\"platform\"}", "admin");

        // Assert
        rule.Metadata.Should().Be("{\"team\":\"platform\"}");
        rule.UpdatedBy.Should().Be("admin");
        rule.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void SetMetadata_WithNull_ClearsMetadata()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.SetMetadata("{\"key\":\"value\"}", "admin");

        // Act
        rule.SetMetadata(null, "admin2");

        // Assert
        rule.Metadata.Should().BeNull();
    }

    #endregion

    #region EvaluateThreshold Tests

    [Fact]
    public void EvaluateThreshold_WhenMetricExceedsThreshold_ReturnsTrue()
    {
        // Arrange
        var rule = AlertRule.Create(
            "CPU Alert", "cpu", AlertSeverity.Warning,
            AlertThreshold.Percentage(80),
            AlertDuration.FiveMinutes, "admin");

        // Act
        var result = rule.EvaluateThreshold(85);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateThreshold_WhenMetricEqualsThreshold_ReturnsTrue()
    {
        // Arrange
        var rule = AlertRule.Create(
            "CPU Alert", "cpu", AlertSeverity.Warning,
            AlertThreshold.Percentage(80),
            AlertDuration.FiveMinutes, "admin");

        // Act
        var result = rule.EvaluateThreshold(80);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EvaluateThreshold_WhenMetricBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var rule = AlertRule.Create(
            "CPU Alert", "cpu", AlertSeverity.Warning,
            AlertThreshold.Percentage(80),
            AlertDuration.FiveMinutes, "admin");

        // Act
        var result = rule.EvaluateThreshold(75);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ShouldTrigger Tests

    [Fact]
    public void ShouldTrigger_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var rule = CreateTestRule();
        rule.Disable("admin");

        // Act
        var result = rule.ShouldTrigger(DateTime.UtcNow.AddMinutes(-10));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldTrigger_WhenEnabledAndDurationExceeded_ReturnsTrue()
    {
        // Arrange
        var rule = AlertRule.Create(
            "Test Alert", "test", AlertSeverity.Warning,
            AlertThreshold.Percentage(80),
            AlertDuration.FiveMinutes, "admin");

        // Act - violation started 10 minutes ago (exceeds 5 minute duration)
        var result = rule.ShouldTrigger(DateTime.UtcNow.AddMinutes(-10));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldTrigger_WhenEnabledButDurationNotExceeded_ReturnsFalse()
    {
        // Arrange
        var rule = AlertRule.Create(
            "Test Alert", "test", AlertSeverity.Warning,
            AlertThreshold.Percentage(80),
            AlertDuration.TenMinutes, "admin");

        // Act - violation started 5 minutes ago (does not exceed 10 minute duration)
        var result = rule.ShouldTrigger(DateTime.UtcNow.AddMinutes(-5));

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private static AlertRule CreateTestRule()
    {
        return AlertRule.Create(
            name: "Test Rule",
            alertType: "test_type",
            severity: AlertSeverity.Warning,
            threshold: AlertThreshold.Percentage(80),
            duration: AlertDuration.FiveMinutes,
            createdBy: "admin");
    }

    #endregion
}
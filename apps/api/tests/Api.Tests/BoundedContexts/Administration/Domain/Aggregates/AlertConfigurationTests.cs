using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Aggregates;

/// <summary>
/// Tests for the AlertConfiguration aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 21
/// </summary>
[Trait("Category", "Unit")]
public sealed class AlertConfigurationTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesConfiguration()
    {
        // Act
        var config = AlertConfiguration.Create(
            configKey: "alerts.email.enabled",
            configValue: "true",
            category: ConfigCategory.Email,
            updatedBy: "admin",
            description: "Enable email alerts");

        // Assert
        config.Id.Should().NotBe(Guid.Empty);
        config.ConfigKey.Should().Be("alerts.email.enabled");
        config.ConfigValue.Should().Be("true");
        config.Category.Should().Be(ConfigCategory.Email);
        config.UpdatedBy.Should().Be("admin");
        config.Description.Should().Be("Enable email alerts");
        config.IsEncrypted.Should().BeFalse();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithoutDescription_CreatesConfigurationWithNullDescription()
    {
        // Act
        var config = AlertConfiguration.Create(
            configKey: "alerts.slack.webhook",
            configValue: "https://hooks.slack.com/test",
            category: ConfigCategory.Slack,
            updatedBy: "admin");

        // Assert
        config.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyConfigKey_ThrowsArgumentException()
    {
        // Act
        var action = () => AlertConfiguration.Create(
            configKey: "",
            configValue: "value",
            category: ConfigCategory.Global,
            updatedBy: "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("configKey")
            .WithMessage("*ConfigKey cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespaceConfigKey_ThrowsArgumentException()
    {
        // Act
        var action = () => AlertConfiguration.Create(
            configKey: "   ",
            configValue: "value",
            category: ConfigCategory.Global,
            updatedBy: "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ConfigKey cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyConfigValue_ThrowsArgumentException()
    {
        // Act
        var action = () => AlertConfiguration.Create(
            configKey: "alerts.key",
            configValue: "",
            category: ConfigCategory.Global,
            updatedBy: "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("configValue")
            .WithMessage("*ConfigValue cannot be empty*");
    }

    [Fact]
    public void Create_WithGlobalCategory_Succeeds()
    {
        // Act
        var config = AlertConfiguration.Create(
            "test.key", "test.value", ConfigCategory.Global, "admin");

        // Assert
        config.Category.Should().Be(ConfigCategory.Global);
    }

    [Fact]
    public void Create_WithEmailCategory_Succeeds()
    {
        // Act
        var config = AlertConfiguration.Create(
            "test.key", "test.value", ConfigCategory.Email, "admin");

        // Assert
        config.Category.Should().Be(ConfigCategory.Email);
    }

    [Fact]
    public void Create_WithSlackCategory_Succeeds()
    {
        // Act
        var config = AlertConfiguration.Create(
            "test.key", "test.value", ConfigCategory.Slack, "admin");

        // Assert
        config.Category.Should().Be(ConfigCategory.Slack);
    }

    [Fact]
    public void Create_WithPagerDutyCategory_Succeeds()
    {
        // Act
        var config = AlertConfiguration.Create(
            "test.key", "test.value", ConfigCategory.PagerDuty, "admin");

        // Assert
        config.Category.Should().Be(ConfigCategory.PagerDuty);
    }

    #endregion

    #region UpdateValue Tests

    [Fact]
    public void UpdateValue_WithValidValue_UpdatesConfiguration()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "alerts.enabled", "false", ConfigCategory.Global, "admin1");
        var originalUpdatedAt = config.UpdatedAt;
        Thread.Sleep(10);

        // Act
        config.UpdateValue("true", "admin2");

        // Assert
        config.ConfigValue.Should().Be("true");
        config.UpdatedBy.Should().Be("admin2");
        config.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdateValue_WithEmptyValue_ThrowsArgumentException()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "alerts.enabled", "true", ConfigCategory.Global, "admin");

        // Act
        var action = () => config.UpdateValue("", "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("configValue")
            .WithMessage("*ConfigValue cannot be empty*");
    }

    [Fact]
    public void UpdateValue_WithWhitespaceValue_ThrowsArgumentException()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "alerts.enabled", "true", ConfigCategory.Global, "admin");

        // Act
        var action = () => config.UpdateValue("   ", "admin");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ConfigValue cannot be empty*");
    }

    #endregion

    #region Encrypt Tests

    [Fact]
    public void Encrypt_SetsIsEncryptedTrue()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "secrets.api_key", "plain-value", ConfigCategory.Global, "admin");

        // Act
        config.Encrypt("encrypted-value", "security-admin");

        // Assert
        config.ConfigValue.Should().Be("encrypted-value");
        config.IsEncrypted.Should().BeTrue();
        config.UpdatedBy.Should().Be("security-admin");
    }

    [Fact]
    public void Encrypt_UpdatesTimestamp()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "secrets.api_key", "plain-value", ConfigCategory.Global, "admin");
        var originalUpdatedAt = config.UpdatedAt;
        Thread.Sleep(10);

        // Act
        config.Encrypt("encrypted-value", "security-admin");

        // Assert
        config.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region Decrypt Tests

    [Fact]
    public void Decrypt_SetsIsEncryptedFalse()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "secrets.api_key", "plain-value", ConfigCategory.Global, "admin");
        config.Encrypt("encrypted-value", "security-admin");

        // Act
        config.Decrypt("decrypted-plain-value", "security-admin");

        // Assert
        config.ConfigValue.Should().Be("decrypted-plain-value");
        config.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public void Decrypt_UpdatesTimestamp()
    {
        // Arrange
        var config = AlertConfiguration.Create(
            "secrets.api_key", "plain-value", ConfigCategory.Global, "admin");
        config.Encrypt("encrypted-value", "security-admin");
        var originalUpdatedAt = config.UpdatedAt;
        Thread.Sleep(10);

        // Act
        config.Decrypt("decrypted-value", "security-admin");

        // Assert
        config.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion
}
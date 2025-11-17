using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Domain tests for SystemConfiguration aggregate.
/// Tests configuration creation, updates, and lifecycle.
/// </summary>
public class SystemConfigurationTests
{
    [Fact]
    public void SystemConfiguration_Create_WithRequiredFields_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var key = new ConfigKey("feature.ai.enabled");
        var value = "true";
        var valueType = "bool";
        var userId = Guid.NewGuid();

        // Act
        var config = new SystemConfiguration(id, key, value, valueType, userId);

        // Assert
        Assert.Equal(id, config.Id);
        Assert.Equal(key, config.Key);
        Assert.Equal(value, config.Value);
        Assert.Equal(valueType, config.ValueType);
        Assert.True(config.IsActive);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void SystemConfiguration_UpdateValue_IncrementsVersionAndStoresPrevious()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfiguration(
            Guid.NewGuid(),
            new ConfigKey("max.connections"),
            "100",
            "int",
            userId);

        // Act
        config.UpdateValue("200", userId);

        // Assert
        Assert.Equal("200", config.Value);
        Assert.Equal("100", config.PreviousValue);
        Assert.Equal(2, config.Version);
    }

    [Fact]
    public void SystemConfiguration_UpdateValue_WithEmptyValue_ThrowsException()
    {
        // Arrange
        var config = new SystemConfiguration(
            Guid.NewGuid(),
            new ConfigKey("test.key"),
            "value",
            "string",
            Guid.NewGuid());

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            config.UpdateValue("", Guid.NewGuid()));
    }

    [Fact]
    public void SystemConfiguration_Activate_WhenInactive_SetsActiveAndRecordsTime()
    {
        // Arrange
        var config = new SystemConfiguration(
            Guid.NewGuid(),
            new ConfigKey("test.key"),
            "value",
            "string",
            Guid.NewGuid());

        config.Deactivate(); // First deactivate
        var beforeActivate = DateTime.UtcNow;

        // Act
        config.Activate();

        // Assert
        Assert.True(config.IsActive);
        Assert.NotNull(config.LastToggledAt);
        Assert.True(config.LastToggledAt >= beforeActivate);
    }
}

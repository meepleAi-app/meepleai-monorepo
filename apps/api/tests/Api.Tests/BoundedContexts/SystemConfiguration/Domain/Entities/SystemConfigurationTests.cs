using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;
using SystemConfigEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Domain tests for SystemConfiguration aggregate.
/// Tests configuration creation, updates, and lifecycle.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        var config = new SystemConfigEntity(id, key, value, valueType, userId);

        // Assert
        config.Id.Should().Be(id);
        config.Key.Should().Be(key);
        config.Value.Should().Be(value);
        config.ValueType.Should().Be(valueType);
        config.IsActive.Should().BeTrue();
        config.Version.Should().Be(1);
    }

    [Fact]
    public void SystemConfiguration_UpdateValue_IncrementsVersionAndStoresPrevious()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = new SystemConfigEntity(
            Guid.NewGuid(),
            new ConfigKey("max.connections"),
            "100",
            "int",
            userId);

        // Act
        config.UpdateValue("200", userId);

        // Assert
        config.Value.Should().Be("200");
        config.PreviousValue.Should().Be("100");
        config.Version.Should().Be(2);
    }

    [Fact]
    public void SystemConfiguration_UpdateValue_WithEmptyValue_ThrowsException()
    {
        // Arrange
        var config = new SystemConfigEntity(
            Guid.NewGuid(),
            new ConfigKey("test.key"),
            "value",
            "string",
            Guid.NewGuid());

        // Act & Assert
        var act = () =>
            config.UpdateValue("", Guid.NewGuid());
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Should().NotBeNull();
    }

    [Fact]
    public void SystemConfiguration_Activate_WhenInactive_SetsActiveAndRecordsTime()
    {
        // Arrange
        var config = new SystemConfigEntity(
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
        config.IsActive.Should().BeTrue();
        config.LastToggledAt.Should().NotBeNull();
        (config.LastToggledAt >= beforeActivate).Should().BeTrue();
    }
}


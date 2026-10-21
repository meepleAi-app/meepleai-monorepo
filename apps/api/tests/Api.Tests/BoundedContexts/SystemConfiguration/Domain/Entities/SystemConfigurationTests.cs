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

    // ===== #4608: ricostruzione di una riga salvata =====

    private static SystemConfigEntity ReconstituteWithSentinels(out DateTime createdAt, out DateTime updatedAt, out DateTime toggledAt)
    {
        createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        updatedAt = createdAt.AddDays(1);
        toggledAt = createdAt.AddDays(2);

        return SystemConfigEntity.Reconstitute(
            id: new Guid("00000000-0000-0000-0000-0000000000a1"),
            key: new ConfigKey("stored.key"),
            value: "stored-value",
            valueType: "string",
            description: "stored-description",
            category: "stored-category",
            isActive: false,
            requiresRestart: true,
            environment: "Staging",
            version: 7,
            previousValue: "stored-previous",
            createdAt: createdAt,
            updatedAt: updatedAt,
            createdByUserId: new Guid("00000000-0000-0000-0000-0000000000b1"),
            updatedByUserId: new Guid("00000000-0000-0000-0000-0000000000b2"),
            lastToggledAt: toggledAt);
    }

    [Fact]
    public void Reconstitute_KeepsEveryStoredValue_AndRaisesNoEvent()
    {
        var config = ReconstituteWithSentinels(out var createdAt, out var updatedAt, out var toggledAt);

        config.Id.Should().Be(new Guid("00000000-0000-0000-0000-0000000000a1"));
        config.Key.Value.Should().Be("stored.key");
        config.Value.Should().Be("stored-value");
        config.ValueType.Should().Be("string");
        config.Description.Should().Be("stored-description");
        config.Category.Should().Be("stored-category");
        config.IsActive.Should().BeFalse();
        config.RequiresRestart.Should().BeTrue();
        config.Environment.Should().Be("Staging");
        config.Version.Should().Be(7, "the stored version, not the 1 of a new configuration");
        config.PreviousValue.Should().Be("stored-previous");
        config.CreatedAt.Should().Be(createdAt);
        config.UpdatedAt.Should().Be(updatedAt);
        config.CreatedByUserId.Should().Be(new Guid("00000000-0000-0000-0000-0000000000b1"));
        config.UpdatedByUserId.Should().Be(new Guid("00000000-0000-0000-0000-0000000000b2"));
        config.LastToggledAt.Should().Be(toggledAt);
        config.DomainEvents.Should().BeEmpty("nothing is being created: the configuration already exists");
    }

    [Fact]
    public void Reconstitute_ThenUpdateValue_RaisesOnlyTheUpdatedEvent()
    {
        var config = ReconstituteWithSentinels(out _, out _, out _);

        config.UpdateValue("new-value", Guid.NewGuid());

        config.DomainEvents.Should().ContainSingle()
            .Which.GetType().Name.Should().Be("ConfigurationUpdatedEvent");
        config.Version.Should().Be(8);
        config.PreviousValue.Should().Be("stored-value");
    }
}

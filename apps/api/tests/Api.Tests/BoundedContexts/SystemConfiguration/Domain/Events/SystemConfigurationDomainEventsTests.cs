using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Events;

/// <summary>
/// Tests for SystemConfiguration domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 25
/// </summary>
[Trait("Category", "Unit")]
public sealed class SystemConfigurationDomainEventsTests
{
    #region ConfigurationCreatedEvent Tests

    [Fact]
    public void ConfigurationCreatedEvent_SetsAllProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = new ConfigKey("RateLimit:Admin:MaxTokens");

        // Act
        var evt = new ConfigurationCreatedEvent(
            configId,
            key,
            value: "10000",
            valueType: "integer",
            category: "RateLimit",
            environment: "production",
            requiresRestart: false,
            userId);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Key.Should().Be(key);
        evt.Value.Should().Be("10000");
        evt.ValueType.Should().Be("integer");
        evt.Category.Should().Be("RateLimit");
        evt.Environment.Should().Be("production");
        evt.RequiresRestart.Should().BeFalse();
        evt.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void ConfigurationCreatedEvent_WithRestartRequired_SetsFlag()
    {
        // Arrange
        var key = new ConfigKey("Database:ConnectionString");

        // Act
        var evt = new ConfigurationCreatedEvent(
            Guid.NewGuid(),
            key,
            value: "Server=localhost",
            valueType: "string",
            category: "Database",
            environment: "development",
            requiresRestart: true,
            Guid.NewGuid());

        // Assert
        evt.RequiresRestart.Should().BeTrue();
    }

    #endregion

    #region ConfigurationDeletedEvent Tests

    [Fact]
    public void ConfigurationDeletedEvent_SetsAllProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var key = new ConfigKey("Feature:OldFlag");

        // Act
        var evt = new ConfigurationDeletedEvent(
            configId,
            key,
            category: "Feature",
            environment: "staging");

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Key.Should().Be(key);
        evt.Category.Should().Be("Feature");
        evt.Environment.Should().Be("staging");
    }

    #endregion

    #region ConfigurationToggledEvent Tests

    [Fact]
    public void ConfigurationToggledEvent_WhenActivated_SetsIsActiveTrue()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = new ConfigKey("Feature:NewUI");

        // Act
        var evt = new ConfigurationToggledEvent(
            configId,
            key,
            isActive: true,
            userId);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Key.Should().Be(key);
        evt.IsActive.Should().BeTrue();
        evt.ToggledByUserId.Should().Be(userId);
    }

    [Fact]
    public void ConfigurationToggledEvent_WhenDeactivated_SetsIsActiveFalse()
    {
        // Arrange
        var key = new ConfigKey("Feature:BetaFeature");

        // Act
        var evt = new ConfigurationToggledEvent(
            Guid.NewGuid(),
            key,
            isActive: false,
            Guid.NewGuid());

        // Assert
        evt.IsActive.Should().BeFalse();
    }

    #endregion

    #region ConfigurationUpdatedEvent Tests

    [Fact]
    public void ConfigurationUpdatedEvent_SetsAllProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = new ConfigKey("Cache:TTL");

        // Act
        var evt = new ConfigurationUpdatedEvent(
            configId,
            key,
            previousValue: "300",
            newValue: "600",
            version: 3,
            userId);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.Key.Should().Be(key);
        evt.PreviousValue.Should().Be("300");
        evt.NewValue.Should().Be("600");
        evt.Version.Should().Be(3);
        evt.UpdatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void ConfigurationUpdatedEvent_TracksVersionIncrement()
    {
        // Arrange
        var key = new ConfigKey("App:Setting");

        // Act
        var evt = new ConfigurationUpdatedEvent(
            Guid.NewGuid(),
            key,
            previousValue: "value1",
            newValue: "value2",
            version: 5,
            Guid.NewGuid());

        // Assert
        evt.Version.Should().Be(5);
    }

    #endregion

    #region RateLimitConfigUpdatedEvent Tests

    [Fact]
    public void RateLimitConfigUpdatedEvent_SetsAllProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();

        // Act
        var evt = new RateLimitConfigUpdatedEvent(configId, UserTier.Premium);

        // Assert
        evt.ConfigId.Should().Be(configId);
        evt.Tier.Should().Be(UserTier.Premium);
    }

    [Fact]
    public void RateLimitConfigUpdatedEvent_ForFreeTier_SetsTierCorrectly()
    {
        // Act
        var evt = new RateLimitConfigUpdatedEvent(Guid.NewGuid(), UserTier.Free);

        // Assert
        evt.Tier.Should().Be(UserTier.Free);
    }

    [Fact]
    public void RateLimitConfigUpdatedEvent_ForAdminTier_SetsTierCorrectly()
    {
        // Act
        var evt = new RateLimitConfigUpdatedEvent(Guid.NewGuid(), UserTier.Admin);

        // Assert
        evt.Tier.Should().Be(UserTier.Admin);
    }

    #endregion

    #region UserRateLimitOverrideCreatedEvent Tests

    [Fact]
    public void UserRateLimitOverrideCreatedEvent_SetsAllProperties()
    {
        // Arrange
        var overrideId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var evt = new UserRateLimitOverrideCreatedEvent(overrideId, userId, adminId);

        // Assert
        evt.OverrideId.Should().Be(overrideId);
        evt.UserId.Should().Be(userId);
        evt.AdminId.Should().Be(adminId);
    }

    #endregion

    #region UserRateLimitOverrideRemovedEvent Tests

    [Fact]
    public void UserRateLimitOverrideRemovedEvent_SetsAllProperties()
    {
        // Arrange
        var overrideId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var evt = new UserRateLimitOverrideRemovedEvent(overrideId, userId, adminId);

        // Assert
        evt.OverrideId.Should().Be(overrideId);
        evt.UserId.Should().Be(userId);
        evt.AdminId.Should().Be(adminId);
    }

    #endregion

    #region UserRateLimitReachedEvent Tests

    [Fact]
    public void UserRateLimitReachedEvent_ForPendingLimit_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserRateLimitReachedEvent(userId, UserRateLimitReachedEvent.PendingLimit);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.LimitType.Should().Be("pending");
    }

    [Fact]
    public void UserRateLimitReachedEvent_ForMonthlyLimit_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserRateLimitReachedEvent(userId, UserRateLimitReachedEvent.MonthlyLimit);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.LimitType.Should().Be("monthly");
    }

    [Fact]
    public void UserRateLimitReachedEvent_LimitTypeConstants_HaveCorrectValues()
    {
        // Assert
        UserRateLimitReachedEvent.PendingLimit.Should().Be("pending");
        UserRateLimitReachedEvent.MonthlyLimit.Should().Be("monthly");
    }

    #endregion
}

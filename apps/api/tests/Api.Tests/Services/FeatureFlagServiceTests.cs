using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Tests for FeatureFlagService, focusing on Issue #3073: Tier-based feature flags.
/// Verifies tier-based checking, combined role+tier access, and enable/disable operations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class FeatureFlagServiceTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<FeatureFlagService>> _mockLogger;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<FeatureFlagService>>();

        _service = new FeatureFlagService(
            _mockConfigService.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    #region IsEnabledForTierAsync Tests

    [Fact]
    public async Task IsEnabledForTierAsync_WhenTierSpecificFlagExists_ReturnsTierValue()
    {
        // Arrange
        var featureName = "Features.RAG";
        var tier = UserTier.Premium;
        var expectedKey = $"{featureName}.Tier.{tier.Value}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(expectedKey, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        Assert.True(result);
        _mockConfigService.Verify(c => c.GetValueAsync<bool?>(expectedKey, null, null), Times.Once);
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WhenTierSpecificFlagIsFalse_ReturnsFalse()
    {
        // Arrange
        var featureName = "Features.AdvancedAnalytics";
        var tier = UserTier.Free;
        var expectedKey = $"{featureName}.Tier.{tier.Value}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(expectedKey, null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WhenNoTierSpecificFlag_FallsBackToGlobal()
    {
        // Arrange
        var featureName = "Features.Chat";
        var tier = UserTier.Normal;
        var tierKey = $"{featureName}.Tier.{tier.Value}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(tierKey, null, null))
            .ReturnsAsync((bool?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(featureName, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        Assert.True(result);
        _mockConfigService.Verify(c => c.GetValueAsync<bool?>(tierKey, null, null), Times.Once);
        _mockConfigService.Verify(c => c.GetValueAsync<bool?>(featureName, null, null), Times.Once);
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WhenNoFlagExists_ReturnsTrue_ForBackwardCompatibility()
    {
        // Arrange
        var featureName = "Features.NewFeature";
        var tier = UserTier.Premium;

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(It.IsAny<string>(), null, null))
            .ReturnsAsync((bool?)null);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert - Default true for backward compatibility
        Assert.True(result);
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.IsEnabledForTierAsync("Features.Test", null!));
    }

    [Theory]
    [InlineData("free")]
    [InlineData("normal")]
    [InlineData("premium")]
    public async Task IsEnabledForTierAsync_AllTiers_UseCorrectKeyFormat(string tierValue)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);
        var featureName = "Features.MultiTier";
        var expectedKey = $"{featureName}.Tier.{tierValue}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(expectedKey, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        Assert.True(result);
        _mockConfigService.Verify(c => c.GetValueAsync<bool?>(expectedKey, null, null), Times.Once);
    }

    #endregion

    #region EnableFeatureForTierAsync Tests

    [Fact]
    public async Task EnableFeatureForTierAsync_WhenConfigNotExists_CreatesNew()
    {
        // Arrange
        var featureName = "Features.NewTierFeature";
        var tier = UserTier.Premium;
        var expectedKey = $"{featureName}.Tier.{tier.Value}";
        var userId = Guid.NewGuid().ToString();

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        await _service.EnableFeatureForTierAsync(featureName, tier, userId);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd =>
                cmd.Key == expectedKey &&
                cmd.Value == "true" &&
                cmd.ValueType == "Boolean" &&
                cmd.Category == "FeatureFlags"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureForTierAsync_WhenConfigExists_UpdatesValue()
    {
        // Arrange
        var featureName = "Features.ExistingFeature";
        var tier = UserTier.Normal;
        var expectedKey = $"{featureName}.Tier.{tier.Value}";
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        var existingConfig = CreateSystemConfigDto(configId.ToString(), expectedKey, "false");

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        await _service.EnableFeatureForTierAsync(featureName, tier, userId);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<UpdateConfigValueCommand>(cmd =>
                cmd.ConfigId == configId &&
                cmd.NewValue == "true"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureForTierAsync_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.EnableFeatureForTierAsync("Features.Test", null!, "user-id"));
    }

    #endregion

    #region DisableFeatureForTierAsync Tests

    [Fact]
    public async Task DisableFeatureForTierAsync_WhenConfigNotExists_CreatesDisabled()
    {
        // Arrange
        var featureName = "Features.DisableTest";
        var tier = UserTier.Free;
        var expectedKey = $"{featureName}.Tier.{tier.Value}";
        var userId = Guid.NewGuid().ToString();

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        // Act
        await _service.DisableFeatureForTierAsync(featureName, tier, userId);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd =>
                cmd.Key == expectedKey &&
                cmd.Value == "false" &&
                cmd.Category == "FeatureFlags"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureForTierAsync_WhenConfigExists_UpdatesToFalse()
    {
        // Arrange
        var featureName = "Features.ExistingToDisable";
        var tier = UserTier.Premium;
        var expectedKey = $"{featureName}.Tier.{tier.Value}";
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();

        var existingConfig = CreateSystemConfigDto(configId.ToString(), expectedKey, "true");

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConfig);

        // Act
        await _service.DisableFeatureForTierAsync(featureName, tier, userId);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<UpdateConfigValueCommand>(cmd =>
                cmd.ConfigId == configId &&
                cmd.NewValue == "false"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetAllFeatureFlagsAsync Tests

    [Fact]
    public async Task GetAllFeatureFlagsAsync_ParsesTierRestrictionCorrectly()
    {
        // Arrange
        var configs = new PagedConfigurationResult(
            Items: new List<ConfigurationDto>
            {
                CreateConfigDto(Guid.NewGuid(), "Features.RAG.Tier.premium", "true"),
                CreateConfigDto(Guid.NewGuid(), "Features.RAG.Tier.free", "false"),
                CreateConfigDto(Guid.NewGuid(), "Features.Chat", "true")
            },
            Total: 3,
            Page: 1,
            PageSize: 100);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetAllConfigsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        Assert.Equal(3, result.Count);

        var premiumFlag = result.First(f => f.TierRestriction == "premium");
        Assert.Equal("Features.RAG", premiumFlag.FeatureName);
        Assert.True(premiumFlag.IsEnabled);
        Assert.Null(premiumFlag.RoleRestriction);

        var freeFlag = result.First(f => f.TierRestriction == "free");
        Assert.Equal("Features.RAG", freeFlag.FeatureName);
        Assert.False(freeFlag.IsEnabled);

        var globalFlag = result.First(f => f.FeatureName == "Features.Chat");
        Assert.Null(globalFlag.TierRestriction);
        Assert.Null(globalFlag.RoleRestriction);
        Assert.True(globalFlag.IsEnabled);
    }

    [Fact]
    public async Task GetAllFeatureFlagsAsync_ParsesRoleRestrictionCorrectly()
    {
        // Arrange
        var configs = new PagedConfigurationResult(
            Items: new List<ConfigurationDto>
            {
                CreateConfigDto(Guid.NewGuid(), "Features.AdminDashboard.Admin", "true"),
                CreateConfigDto(Guid.NewGuid(), "Features.EditContent.Editor", "true")
            },
            Total: 2,
            Page: 1,
            PageSize: 100);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetAllConfigsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        var adminFlag = result.First(f => f.RoleRestriction == "Admin");
        Assert.Equal("Features.AdminDashboard", adminFlag.FeatureName);
        Assert.Null(adminFlag.TierRestriction);

        var editorFlag = result.First(f => f.RoleRestriction == "Editor");
        Assert.Equal("Features.EditContent", editorFlag.FeatureName);
    }

    [Fact]
    public async Task GetAllFeatureFlagsAsync_DistinguishesTierFromRole()
    {
        // Arrange - Test that "Tier" prefix is correctly identified
        var configs = new PagedConfigurationResult(
            Items: new List<ConfigurationDto>
            {
                CreateConfigDto(Guid.NewGuid(), "Features.Test.Tier.premium", "true"),  // Tier-based
                CreateConfigDto(Guid.NewGuid(), "Features.Test.Admin", "true")           // Role-based
            },
            Total: 2,
            Page: 1,
            PageSize: 100);

        _mockMediator.Setup(m => m.Send(It.IsAny<GetAllConfigsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        var tierFlag = result.First(f => f.TierRestriction != null);
        Assert.Equal("Features.Test", tierFlag.FeatureName);
        Assert.Equal("premium", tierFlag.TierRestriction);
        Assert.Null(tierFlag.RoleRestriction);

        var roleFlag = result.First(f => f.RoleRestriction != null);
        Assert.Equal("Features.Test", roleFlag.FeatureName);
        Assert.Equal("Admin", roleFlag.RoleRestriction);
        Assert.Null(roleFlag.TierRestriction);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a SystemConfigurationDto for IConfigurationService mocking.
    /// </summary>
    private static SystemConfigurationDto CreateSystemConfigDto(string id, string key, string value)
    {
        return new SystemConfigurationDto(
            Id: id,
            Key: key,
            Value: value,
            ValueType: "Boolean",
            Description: $"Feature flag: {key}",
            Category: "FeatureFlags",
            IsActive: true,
            RequiresRestart: false,
            Environment: "Production",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "system",
            UpdatedByUserId: null,
            LastToggledAt: null);
    }

    /// <summary>
    /// Creates a ConfigurationDto for PagedConfigurationResult mocking.
    /// </summary>
    private static ConfigurationDto CreateConfigDto(Guid id, string key, string value)
    {
        return new ConfigurationDto(
            Id: id,
            Key: key,
            Value: value,
            ValueType: "Boolean",
            Description: $"Feature flag: {key}",
            Category: "FeatureFlags",
            IsActive: true,
            RequiresRestart: false,
            Environment: "Production",
            Version: 1,
            CreatedAt: DateTime.UtcNow.AddDays(-1),
            UpdatedAt: DateTime.UtcNow);
    }

    #endregion
}

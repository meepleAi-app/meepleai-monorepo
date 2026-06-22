using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly Mock<ILogger<FeatureFlagService>> _mockLogger;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockMediator = new Mock<IMediator>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        _mockLogger = new Mock<ILogger<FeatureFlagService>>();

        _service = new FeatureFlagService(
            _mockConfigService.Object,
            _mockMediator.Object,
            _mockEnvironment.Object,
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
        result.Should().BeTrue();
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
        result.Should().BeFalse();
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
        result.Should().BeTrue();
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
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => _service.IsEnabledForTierAsync("Features.Test", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().BeTrue();
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

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, "Production", It.IsAny<CancellationToken>()))
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

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, "Production", It.IsAny<CancellationToken>()))
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
        var act2 = () => _service.EnableFeatureForTierAsync("Features.Test", null!, "user-id");
        await act2.Should().ThrowAsync<ArgumentNullException>();
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

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, "Production", It.IsAny<CancellationToken>()))
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

        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(expectedKey, "Production", It.IsAny<CancellationToken>()))
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

    #region #2162 regression — Environment asymmetry (mirror of #2116 / PR #2159)

    // For each of the 4 mutating methods, three regression tests guard against
    // re-introducing the hardcoded Environment="Production" literal:
    //   1. In Development env, the CREATE branch persists Environment="Development".
    //   2. In Production env, the CREATE branch persists Environment="Production"
    //      (derived from IWebHostEnvironment, NOT a literal).
    //   3. The LOOKUP and the CREATE pass the SAME environment string (symmetric).

    [Fact]
    public async Task EnableFeatureAsync_InDevelopmentEnv_CreatesConfigWithDevelopmentEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        const string featureName = "Features.NewFlag";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(featureName, "Development", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.EnableFeatureAsync(featureName, null, Guid.NewGuid().ToString());

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd =>
                cmd.Key == featureName &&
                cmd.Value == "true" &&
                cmd.Environment == "Development"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureAsync_InProductionEnv_PassesProductionDerivedFromIWebHostEnvironment()
    {
        // Regression guard: prevent re-introducing a hardcoded "Production" string literal.
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        const string featureName = "Features.AnotherFlag";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(featureName, "Production", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.EnableFeatureAsync(featureName);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Production"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockEnvironment.Verify(e => e.EnvironmentName, Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnableFeatureAsync_LookupAndCreate_PassTheSameEnvironmentString()
    {
        // Symmetric lookup + create: any non-Production env exposes the asymmetry.
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Staging");
        const string featureName = "Features.Symmetric";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(featureName, "Staging", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.EnableFeatureAsync(featureName);

        _mockConfigService.Verify(c => c.GetConfigurationByKeyAsync(featureName, "Staging", It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Staging"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureAsync_InDevelopmentEnv_CreatesConfigWithDevelopmentEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        const string featureName = "Features.OffFlag";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(featureName, "Development", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.DisableFeatureAsync(featureName);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd =>
                cmd.Value == "false" &&
                cmd.Environment == "Development"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureAsync_InProductionEnv_PassesProductionDerivedFromIWebHostEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        const string featureName = "Features.ProdOff";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(featureName, "Production", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.DisableFeatureAsync(featureName);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Production"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureAsync_LookupAndCreate_PassTheSameEnvironmentString()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Staging");
        const string featureName = "Features.SymOff";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(featureName, "Staging", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.DisableFeatureAsync(featureName);

        _mockConfigService.Verify(c => c.GetConfigurationByKeyAsync(featureName, "Staging", It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Staging"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureForTierAsync_InDevelopmentEnv_CreatesConfigWithDevelopmentEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        const string featureName = "Features.TierOn";
        var tier = UserTier.Premium;
        var key = $"{featureName}.Tier.{tier.Value}";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(key, "Development", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.EnableFeatureForTierAsync(featureName, tier);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd =>
                cmd.Key == key &&
                cmd.Value == "true" &&
                cmd.Environment == "Development"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureForTierAsync_InProductionEnv_PassesProductionDerivedFromIWebHostEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        const string featureName = "Features.TierProd";
        var tier = UserTier.Normal;
        var key = $"{featureName}.Tier.{tier.Value}";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(key, "Production", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.EnableFeatureForTierAsync(featureName, tier);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Production"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureForTierAsync_LookupAndCreate_PassTheSameEnvironmentString()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Staging");
        const string featureName = "Features.TierSym";
        var tier = UserTier.Free;
        var key = $"{featureName}.Tier.{tier.Value}";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(key, "Staging", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.EnableFeatureForTierAsync(featureName, tier);

        _mockConfigService.Verify(c => c.GetConfigurationByKeyAsync(key, "Staging", It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Staging"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureForTierAsync_InDevelopmentEnv_CreatesConfigWithDevelopmentEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        const string featureName = "Features.TierOff";
        var tier = UserTier.Premium;
        var key = $"{featureName}.Tier.{tier.Value}";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(key, "Development", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.DisableFeatureForTierAsync(featureName, tier);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd =>
                cmd.Value == "false" &&
                cmd.Environment == "Development"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureForTierAsync_InProductionEnv_PassesProductionDerivedFromIWebHostEnvironment()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        const string featureName = "Features.TierProdOff";
        var tier = UserTier.Normal;
        var key = $"{featureName}.Tier.{tier.Value}";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(key, "Production", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.DisableFeatureForTierAsync(featureName, tier);

        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Production"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureForTierAsync_LookupAndCreate_PassTheSameEnvironmentString()
    {
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Staging");
        const string featureName = "Features.TierSymOff";
        var tier = UserTier.Free;
        var key = $"{featureName}.Tier.{tier.Value}";
        _mockConfigService.Setup(c => c.GetConfigurationByKeyAsync(key, "Staging", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationDto?)null);

        await _service.DisableFeatureForTierAsync(featureName, tier);

        _mockConfigService.Verify(c => c.GetConfigurationByKeyAsync(key, "Staging", It.IsAny<CancellationToken>()), Times.Once);
        _mockMediator.Verify(m => m.Send(
            It.Is<CreateConfigurationCommand>(cmd => cmd.Environment == "Staging"),
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
        result.Count.Should().Be(3);

        var premiumFlag = result.First(f => f.TierRestriction == "premium");
        premiumFlag.FeatureName.Should().Be("Features.RAG");
        premiumFlag.IsEnabled.Should().BeTrue();
        premiumFlag.RoleRestriction.Should().BeNull();

        var freeFlag = result.First(f => f.TierRestriction == "free");
        freeFlag.FeatureName.Should().Be("Features.RAG");
        freeFlag.IsEnabled.Should().BeFalse();

        var globalFlag = result.First(f => f.FeatureName == "Features.Chat");
        globalFlag.TierRestriction.Should().BeNull();
        globalFlag.RoleRestriction.Should().BeNull();
        globalFlag.IsEnabled.Should().BeTrue();
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
        adminFlag.FeatureName.Should().Be("Features.AdminDashboard");
        adminFlag.TierRestriction.Should().BeNull();

        var editorFlag = result.First(f => f.RoleRestriction == "Editor");
        editorFlag.FeatureName.Should().Be("Features.EditContent");
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
        tierFlag.FeatureName.Should().Be("Features.Test");
        tierFlag.TierRestriction.Should().Be("premium");
        tierFlag.RoleRestriction.Should().BeNull();

        var roleFlag = result.First(f => f.RoleRestriction != null);
        roleFlag.FeatureName.Should().Be("Features.Test");
        roleFlag.RoleRestriction.Should().Be("Admin");
        roleFlag.TierRestriction.Should().BeNull();
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

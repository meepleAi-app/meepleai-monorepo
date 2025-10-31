using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// CONFIG-05: Unit tests for FeatureFlagService.
/// Tests feature flag checks, role-based hierarchy, and enable/disable operations.
/// </summary>
public class FeatureFlagServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly Mock<ILogger<FeatureFlagService>> _loggerMock;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _configServiceMock = new Mock<IConfigurationService>();
        _loggerMock = new Mock<ILogger<FeatureFlagService>>();
        _service = new FeatureFlagService(_configServiceMock.Object, _loggerMock.Object);
    }

    #region IsEnabledAsync Tests

    [Fact]
    public async Task IsEnabledAsync_ReturnsTrue_WhenGlobalFlagIsEnabled()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>("Features.TestFeature", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledAsync("Features.TestFeature");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsFalse_WhenGlobalFlagIsDisabled()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>("Features.TestFeature", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsEnabledAsync("Features.TestFeature");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsEnabledAsync_ReturnsFalse_WhenFlagNotFound()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>("Features.UnknownFeature", null, null))
            .ReturnsAsync((bool?)null);

        // Act
        var result = await _service.IsEnabledAsync("Features.UnknownFeature");

        // Assert
        Assert.False(result); // Default: feature disabled (fail-safe)
    }

    [Fact]
    public async Task IsEnabledAsync_UsesRoleSpecificFlag_WhenRoleProvided()
    {
        // Arrange
        var featureName = "Features.AdvancedFeature";
        var role = UserRole.Admin;

        // Role-specific flag enabled
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>($"{featureName}.{role}", null, null))
            .ReturnsAsync(true);

        // Global flag disabled
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>(featureName, null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsEnabledAsync(featureName, role);

        // Assert
        Assert.True(result); // Role-specific flag takes precedence
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>($"{featureName}.{role}", null, null), Times.Once);
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>(featureName, null, null), Times.Never); // Should not check global
    }

    [Fact]
    public async Task IsEnabledAsync_FallsBackToGlobal_WhenRoleSpecificNotFound()
    {
        // Arrange
        var featureName = "Features.StandardFeature";
        var role = UserRole.User;

        // Role-specific flag not found
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>($"{featureName}.{role}", null, null))
            .ReturnsAsync((bool?)null);

        // Global flag enabled
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>(featureName, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledAsync(featureName, role);

        // Assert
        Assert.True(result); // Falls back to global flag
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>($"{featureName}.{role}", null, null), Times.Once);
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>(featureName, null, null), Times.Once);
    }

    [Fact]
    public async Task IsEnabledAsync_RoleSpecificOverridesGlobal()
    {
        // Arrange - Role-specific DISABLED, but global ENABLED (role-specific wins)
        var featureName = "Features.BetaFeature";
        var role = UserRole.User;

        _configServiceMock.Setup(x => x.GetValueAsync<bool?>($"{featureName}.{role}", null, null))
            .ReturnsAsync(false); // Role-specific: disabled

        _configServiceMock.Setup(x => x.GetValueAsync<bool?>(featureName, null, null))
            .ReturnsAsync(true); // Global: enabled

        // Act
        var result = await _service.IsEnabledAsync(featureName, role);

        // Assert
        Assert.False(result); // Role-specific override takes precedence
    }

    #endregion

    #region EnableFeatureAsync Tests

    [Fact]
    public async Task EnableFeatureAsync_UpdatesExistingConfiguration_WhenConfigExists()
    {
        // Arrange
        var featureName = "Features.ExistingFeature";
        var existingConfig = new SystemConfigurationDto(
            Id: "config-123",
            Key: featureName,
            Value: "false",
            ValueType: "Boolean",
            Description: "Test feature",
            Category: "FeatureFlags",
            IsActive: true,
            RequiresRestart: false,
            Environment: "Production",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "system",
            UpdatedByUserId: "system",
            LastToggledAt: null);

        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(featureName, null))
            .ReturnsAsync(existingConfig);

        _configServiceMock.Setup(x => x.UpdateConfigurationAsync(
            "config-123",
            It.Is<UpdateConfigurationRequest>(r => r.Value == "true"),
            "admin-user"))
            .ReturnsAsync(existingConfig);

        // Act
        await _service.EnableFeatureAsync(featureName, userId: "admin-user");

        // Assert
        _configServiceMock.Verify(x => x.UpdateConfigurationAsync(
            "config-123",
            It.Is<UpdateConfigurationRequest>(r => r.Value == "true" && r.IsActive == true),
            "admin-user"), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureAsync_CreatesNewConfiguration_WhenConfigNotFound()
    {
        // Arrange
        var featureName = "Features.NewFeature";

        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(featureName, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _configServiceMock.Setup(x => x.CreateConfigurationAsync(
            It.Is<CreateConfigurationRequest>(r => r.Key == featureName && r.Value == "true"),
            "admin-user"))
            .ReturnsAsync(new SystemConfigurationDto(
                Id: "new-config",
                Key: featureName,
                Value: "true",
                ValueType: "Boolean",
                Description: $"Feature flag: {featureName}",
                Category: "FeatureFlags",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Production",
                Version: 1,
                PreviousValue: null,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                CreatedByUserId: "admin-user",
                UpdatedByUserId: "admin-user",
                LastToggledAt: null));

        // Act
        await _service.EnableFeatureAsync(featureName, userId: "admin-user");

        // Assert
        _configServiceMock.Verify(x => x.CreateConfigurationAsync(
            It.Is<CreateConfigurationRequest>(r =>
                r.Key == featureName &&
                r.Value == "true" &&
                r.ValueType == "Boolean" &&
                r.Category == "FeatureFlags"),
            "admin-user"), Times.Once);
    }

    [Fact]
    public async Task EnableFeatureAsync_CreatesRoleSpecificKey_WhenRoleProvided()
    {
        // Arrange
        var featureName = "Features.AdminFeature";
        var role = UserRole.Admin;
        var expectedKey = $"{featureName}.{role}";

        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(expectedKey, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _configServiceMock.Setup(x => x.CreateConfigurationAsync(
            It.Is<CreateConfigurationRequest>(r => r.Key == expectedKey),
            "admin-user"))
            .ReturnsAsync(new SystemConfigurationDto(
                Id: "role-config",
                Key: expectedKey,
                Value: "true",
                ValueType: "Boolean",
                Description: $"Feature flag: {featureName}",
                Category: "FeatureFlags",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Production",
                Version: 1,
                PreviousValue: null,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                CreatedByUserId: "admin-user",
                UpdatedByUserId: "admin-user",
                LastToggledAt: null));

        // Act
        await _service.EnableFeatureAsync(featureName, role, "admin-user");

        // Assert
        _configServiceMock.Verify(x => x.CreateConfigurationAsync(
            It.Is<CreateConfigurationRequest>(r => r.Key == expectedKey),
            "admin-user"), Times.Once);
    }

    #endregion

    #region DisableFeatureAsync Tests

    [Fact]
    public async Task DisableFeatureAsync_UpdatesExistingConfiguration_WhenConfigExists()
    {
        // Arrange
        var featureName = "Features.DisableTest";
        var existingConfig = new SystemConfigurationDto(
            Id: "config-456",
            Key: featureName,
            Value: "true",
            ValueType: "Boolean",
            Description: "Test feature",
            Category: "FeatureFlags",
            IsActive: true,
            RequiresRestart: false,
            Environment: "Production",
            Version: 1,
            PreviousValue: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CreatedByUserId: "system",
            UpdatedByUserId: "system",
            LastToggledAt: null);

        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(featureName, null))
            .ReturnsAsync(existingConfig);

        _configServiceMock.Setup(x => x.UpdateConfigurationAsync(
            "config-456",
            It.Is<UpdateConfigurationRequest>(r => r.Value == "false"),
            "admin-user"))
            .ReturnsAsync(existingConfig);

        // Act
        await _service.DisableFeatureAsync(featureName, userId: "admin-user");

        // Assert
        _configServiceMock.Verify(x => x.UpdateConfigurationAsync(
            "config-456",
            It.Is<UpdateConfigurationRequest>(r => r.Value == "false"),
            "admin-user"), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureAsync_CreatesDisabledConfiguration_WhenConfigNotFound()
    {
        // Arrange
        var featureName = "Features.NewDisabledFeature";

        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(featureName, null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _configServiceMock.Setup(x => x.CreateConfigurationAsync(
            It.Is<CreateConfigurationRequest>(r => r.Key == featureName && r.Value == "false"),
            "system"))
            .ReturnsAsync(new SystemConfigurationDto(
                Id: "new-disabled",
                Key: featureName,
                Value: "false",
                ValueType: "Boolean",
                Description: $"Feature flag: {featureName}",
                Category: "FeatureFlags",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Production",
                Version: 1,
                PreviousValue: null,
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: DateTime.UtcNow,
                CreatedByUserId: "system",
                UpdatedByUserId: "system",
                LastToggledAt: null));

        // Act
        await _service.DisableFeatureAsync(featureName);

        // Assert
        _configServiceMock.Verify(x => x.CreateConfigurationAsync(
            It.Is<CreateConfigurationRequest>(r =>
                r.Key == featureName &&
                r.Value == "false" &&
                r.Category == "FeatureFlags"),
            "system"), Times.Once);
    }

    #endregion

    #region GetAllFeatureFlagsAsync Tests

    [Fact]
    public async Task GetAllFeatureFlagsAsync_ReturnsAllFlags_FromConfigurationService()
    {
        // Arrange
        var configs = new PagedResult<SystemConfigurationDto>(
            Items: new List<SystemConfigurationDto>
            {
                new(Id: "1", Key: "Features.Feature1", Value: "true", ValueType: "Boolean",
                    Description: "Feature 1", Category: "FeatureFlags", IsActive: true,
                    RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                    CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                    CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null),
                new(Id: "2", Key: "Features.Feature2", Value: "false", ValueType: "Boolean",
                    Description: "Feature 2", Category: "FeatureFlags", IsActive: true,
                    RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                    CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                    CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null),
                new(Id: "3", Key: "Features.AdminFeature.Admin", Value: "true", ValueType: "Boolean",
                    Description: "Admin feature", Category: "FeatureFlags", IsActive: true,
                    RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                    CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                    CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null)
            },
            Total: 3,
            Page: 1,
            PageSize: 100);

        _configServiceMock.Setup(x => x.GetConfigurationsAsync(
            "FeatureFlags", null, true, 1, 100))
            .ReturnsAsync(configs);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        Assert.Equal(3, result.Count);

        // Global flags
        Assert.Contains(result, f => f.FeatureName == "Features.Feature1" && f.IsEnabled && f.RoleRestriction == null);
        Assert.Contains(result, f => f.FeatureName == "Features.Feature2" && !f.IsEnabled && f.RoleRestriction == null);

        // Role-specific flag
        var adminFlag = result.FirstOrDefault(f => f.RoleRestriction == "Admin");
        Assert.NotNull(adminFlag);
        Assert.Equal("Features.AdminFeature", adminFlag.FeatureName);
        Assert.True(adminFlag.IsEnabled);
    }

    [Fact]
    public async Task GetAllFeatureFlagsAsync_ParsesRoleRestriction_FromKey()
    {
        // Arrange
        var configs = new PagedResult<SystemConfigurationDto>(
            Items: new List<SystemConfigurationDto>
            {
                new(Id: "1", Key: "Features.TestFeature.Editor", Value: "true", ValueType: "Boolean",
                    Description: "Editor feature", Category: "FeatureFlags", IsActive: true,
                    RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                    CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                    CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null)
            },
            Total: 1,
            Page: 1,
            PageSize: 100);

        _configServiceMock.Setup(x => x.GetConfigurationsAsync(
            "FeatureFlags", null, true, 1, 100))
            .ReturnsAsync(configs);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Features.TestFeature", result[0].FeatureName);
        Assert.Equal("Editor", result[0].RoleRestriction);
        Assert.True(result[0].IsEnabled);
    }

    [Fact]
    public async Task GetAllFeatureFlagsAsync_HandlesInvalidBooleanValues()
    {
        // Arrange
        var configs = new PagedResult<SystemConfigurationDto>(
            Items: new List<SystemConfigurationDto>
            {
                new(Id: "1", Key: "Features.InvalidFlag", Value: "not-a-boolean", ValueType: "Boolean",
                    Description: "Invalid flag", Category: "FeatureFlags", IsActive: true,
                    RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                    CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                    CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null)
            },
            Total: 1,
            Page: 1,
            PageSize: 100);

        _configServiceMock.Setup(x => x.GetConfigurationsAsync(
            "FeatureFlags", null, true, 1, 100))
            .ReturnsAsync(configs);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        Assert.Single(result);
        Assert.False(result[0].IsEnabled); // Invalid value treated as false
    }

    #endregion

    #region Hierarchy Tests

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Editor)]
    [InlineData(UserRole.User)]
    public async Task IsEnabledAsync_SupportsAllUserRoles(UserRole role)
    {
        // Arrange
        var featureName = "Features.RoleTest";
        var roleKey = $"{featureName}.{role}";

        _configServiceMock.Setup(x => x.GetValueAsync<bool?>(roleKey, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledAsync(featureName, role);

        // Assert
        Assert.True(result);
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>(roleKey, null, null), Times.Once);
    }

    [Fact]
    public async Task IsEnabledAsync_ChecksGlobalFirst_WhenNoRoleProvided()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>("Features.GlobalFeature", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledAsync("Features.GlobalFeature", role: null);

        // Assert
        Assert.True(result);
        // Should only check global, not any role-specific keys
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>("Features.GlobalFeature", null, null), Times.Once);
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>(It.Is<string>(k => k.Contains(".")), null, null), Times.Once); // Only one call
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EnableFeatureAsync_DefaultsToSystemUser_WhenUserIdNotProvided()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(It.IsAny<string>(), null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _configServiceMock.Setup(x => x.CreateConfigurationAsync(It.IsAny<CreateConfigurationRequest>(), "system"))
            .ReturnsAsync(new SystemConfigurationDto(
                Id: "new", Key: "Features.Test", Value: "true", ValueType: "Boolean",
                Description: "Test", Category: "FeatureFlags", IsActive: true,
                RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null));

        // Act
        await _service.EnableFeatureAsync("Features.Test"); // No userId provided

        // Assert
        _configServiceMock.Verify(x => x.CreateConfigurationAsync(
            It.IsAny<CreateConfigurationRequest>(), "system"), Times.Once);
    }

    [Fact]
    public async Task DisableFeatureAsync_DefaultsToSystemUser_WhenUserIdNotProvided()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetConfigurationByKeyAsync(It.IsAny<string>(), null))
            .ReturnsAsync((SystemConfigurationDto?)null);

        _configServiceMock.Setup(x => x.CreateConfigurationAsync(It.IsAny<CreateConfigurationRequest>(), "system"))
            .ReturnsAsync(new SystemConfigurationDto(
                Id: "new", Key: "Features.Test", Value: "false", ValueType: "Boolean",
                Description: "Test", Category: "FeatureFlags", IsActive: true,
                RequiresRestart: false, Environment: "Production", Version: 1, PreviousValue: null,
                CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
                CreatedByUserId: "system", UpdatedByUserId: "system", LastToggledAt: null));

        // Act
        await _service.DisableFeatureAsync("Features.Test"); // No userId provided

        // Assert
        _configServiceMock.Verify(x => x.CreateConfigurationAsync(
            It.IsAny<CreateConfigurationRequest>(), "system"), Times.Once);
    }

    [Fact]
    public async Task GetAllFeatureFlagsAsync_ReturnsEmptyList_WhenNoFlagsExist()
    {
        // Arrange
        var emptyResult = new PagedResult<SystemConfigurationDto>(
            Items: new List<SystemConfigurationDto>(),
            Total: 0,
            Page: 1,
            PageSize: 100);

        _configServiceMock.Setup(x => x.GetConfigurationsAsync(
            "FeatureFlags", null, true, 1, 100))
            .ReturnsAsync(emptyResult);

        // Act
        var result = await _service.GetAllFeatureFlagsAsync();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task IsEnabledAsync_HandlesConcurrentCalls_Correctly()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<bool?>("Features.ConcurrentTest", null, null))
            .ReturnsAsync(true);

        // Act - Simulate concurrent calls
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _service.IsEnabledAsync("Features.ConcurrentTest"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, r => Assert.True(r));
        // Each call should check the configuration
        _configServiceMock.Verify(x => x.GetValueAsync<bool?>("Features.ConcurrentTest", null, null), Times.Exactly(10));
    }

    #endregion
}

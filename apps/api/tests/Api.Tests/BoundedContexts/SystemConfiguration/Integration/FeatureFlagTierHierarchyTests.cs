using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Integration;

/// <summary>
/// Tests for tier hierarchy behavior in feature flag access control.
/// Verifies that higher tiers include lower tier access, and combined role+tier logic.
/// Issue #3674: Feature Flag Tier-Based Access Verification
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class FeatureFlagTierHierarchyTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<FeatureFlagService>> _mockLogger;
    private readonly FeatureFlagService _service;

    public FeatureFlagTierHierarchyTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<FeatureFlagService>>();

        _service = new FeatureFlagService(
            _mockConfigService.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    #region Tier Hierarchy Tests

    [Theory]
    [InlineData("free", true)]
    [InlineData("normal", true)]
    [InlineData("premium", true)]
    public async Task BasicChat_AvailableToAllTiers(string tierValue, bool expected)
    {
        // Arrange - basic_chat enabled for all tiers
        var tier = UserTier.Parse(tierValue);
        var featureName = "basic_chat";
        var tierKey = $"{featureName}.Tier.{tierValue}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(tierKey, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("free", false)]
    [InlineData("normal", true)]
    [InlineData("premium", true)]
    public async Task AdvancedRag_NotAvailableToFree(string tierValue, bool expected)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);
        var featureName = "advanced_rag";
        var tierKey = $"{featureName}.Tier.{tierValue}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(tierKey, null, null))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("free", false)]
    [InlineData("normal", false)]
    [InlineData("premium", true)]
    public async Task MultiAgent_PremiumOnly(string tierValue, bool expected)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);
        var featureName = "multi_agent";
        var tierKey = $"{featureName}.Tier.{tierValue}";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(tierKey, null, null))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Combined Role + Tier Tests

    [Fact]
    public async Task CanAccessFeature_UserRoleDenied_ReturnsFalse_EvenIfTierAllows()
    {
        // Arrange - Role check fails, tier check would succeed
        var user = CreateUser(UserTier.Premium, Role.User);
        var feature = "admin_only_feature";

        // Role-specific flag: User role denied
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.User", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CanAccessFeatureAsync(user, feature);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessFeature_TierDenied_ReturnsFalse_EvenIfRoleAllows()
    {
        // Arrange - Role check succeeds, tier check fails
        var user = CreateUser(UserTier.Free, Role.User);
        var feature = "premium_feature";

        // Global flag enabled (role passes)
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(feature, null, null))
            .ReturnsAsync(true);
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.User", null, null))
            .ReturnsAsync((bool?)null);

        // Tier denied
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.Tier.free", null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CanAccessFeatureAsync(user, feature);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessFeature_BothAllow_ReturnsTrue()
    {
        // Arrange
        var user = CreateUser(UserTier.Premium, Role.User);
        var feature = "advanced_rag";

        // Role passes (global flag)
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(feature, null, null))
            .ReturnsAsync(true);
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.User", null, null))
            .ReturnsAsync((bool?)null);

        // Tier passes
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.Tier.premium", null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CanAccessFeatureAsync(user, feature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessFeature_AdminRole_GlobalFlagEnabled_Succeeds()
    {
        // Arrange - Admin user with free tier should still get role access
        var user = CreateUser(UserTier.Free, Role.Admin);
        var feature = "admin_dashboard";

        // Admin role specific flag
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.Admin", null, null))
            .ReturnsAsync(true);

        // Tier check - free tier not set, defaults to true
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{feature}.Tier.free", null, null))
            .ReturnsAsync((bool?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(feature, null, null))
            .ReturnsAsync((bool?)null);

        // Act
        var result = await _service.CanAccessFeatureAsync(user, feature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessFeature_WithNullUser_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CanAccessFeatureAsync(null!, "feature"));
    }

    [Fact]
    public async Task CanAccessFeature_WithEmptyFeatureName_ThrowsArgumentException()
    {
        var user = CreateUser(UserTier.Free, Role.User);
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CanAccessFeatureAsync(user, ""));
    }

    [Fact]
    public async Task CanAccessFeature_WithWhitespaceFeatureName_ThrowsArgumentException()
    {
        var user = CreateUser(UserTier.Free, Role.User);
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CanAccessFeatureAsync(user, "   "));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task IsEnabledForTierAsync_GlobalFlagDisabled_TierNotSet_ReturnsFalse()
    {
        // Arrange - Global flag explicitly disabled, no tier-specific flag
        var tier = UserTier.Premium;
        var featureName = "disabled_feature";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{featureName}.Tier.premium", null, null))
            .ReturnsAsync((bool?)null);
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(featureName, null, null))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledForTierAsync_TierSpecificOverridesGlobal()
    {
        // Arrange - Global enabled, but tier-specific disabled
        var tier = UserTier.Free;
        var featureName = "premium_only";

        _mockConfigService.Setup(c => c.GetValueAsync<bool?>($"{featureName}.Tier.free", null, null))
            .ReturnsAsync(false);
        // Global would return true, but shouldn't be reached
        _mockConfigService.Setup(c => c.GetValueAsync<bool?>(featureName, null, null))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsEnabledForTierAsync(featureName, tier);

        // Assert
        result.Should().BeFalse("Tier-specific flag should override global flag");
    }

    #endregion

    #region Helper Methods

    private static User CreateUser(UserTier tier, Role role)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email($"test-{Guid.NewGuid():N}@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Test123!"),
            role: role,
            tier: tier
        );
    }

    #endregion
}

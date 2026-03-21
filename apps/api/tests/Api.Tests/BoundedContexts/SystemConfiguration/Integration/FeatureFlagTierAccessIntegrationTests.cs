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
/// Integration tests for tier-based feature flag access.
/// Issue #3674: Feature Flags Verification
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SystemConfiguration")]
public class FeatureFlagTierAccessIntegrationTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<FeatureFlagService>> _mockLogger;
    private readonly FeatureFlagService _featureFlagService;

    public FeatureFlagTierAccessIntegrationTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<FeatureFlagService>>();
        _featureFlagService = new FeatureFlagService(
            _mockConfigService.Object,
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task CanAccessFeatureAsync_FreeUserAccessingPremiumFeature_ReturnsFalse()
    {
        // Arrange
        var freeUser = CreateUser(UserTier.Free, Role.User);
        var premiumFeature = "Features.MultiAgent";

        // Mock: Feature disabled for free tier
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(
                $"{premiumFeature}.Tier.free",
                It.IsAny<bool?>(),
                null))
            .ReturnsAsync(false);

        // Act
        var canAccess = await _featureFlagService.CanAccessFeatureAsync(freeUser, premiumFeature);

        // Assert
        canAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessFeatureAsync_PremiumUserAccessingPremiumFeature_ReturnsTrue()
    {
        // Arrange
        var premiumUser = CreateUser(UserTier.Premium, Role.User);
        var premiumFeature = "Features.MultiAgent";

        // Mock: Role-based access granted (global feature enabled)
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(
                premiumFeature,
                It.IsAny<bool?>(),
                null))
            .ReturnsAsync(true);

        // Mock: Feature enabled for premium tier
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(
                $"{premiumFeature}.Tier.premium",
                It.IsAny<bool?>(),
                null))
            .ReturnsAsync(true);

        // Act
        var canAccess = await _featureFlagService.CanAccessFeatureAsync(premiumUser, premiumFeature);

        // Assert
        canAccess.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessFeatureAsync_AdminBypassesTierRestrictions_ReturnsTrue()
    {
        // Arrange
        var adminUser = CreateUser(UserTier.Free, Role.Admin);
        var premiumFeature = "Features.MultiAgent";

        // Mock: Feature disabled for free tier (but admin should bypass)
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(
                It.IsAny<string>(),
                It.IsAny<bool?>(),
                null))
            .ReturnsAsync((bool?)null); // Default behavior

        // Act
        var canAccess = await _featureFlagService.CanAccessFeatureAsync(adminUser, premiumFeature);

        // Assert
        canAccess.Should().BeTrue(); // Admin bypasses tier restrictions
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WhenTierFlagNotSet_ReturnsTrue()
    {
        // Arrange - No configuration exists (backward compatibility)
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(It.IsAny<string>(), It.IsAny<bool?>(), null))
            .ReturnsAsync((bool?)null);

        // Act
        var isEnabled = await _featureFlagService.IsEnabledForTierAsync("Features.NewFeature", UserTier.Free);

        // Assert
        isEnabled.Should().BeTrue(); // Default true for backward compatibility
    }

    [Fact]
    public async Task IsEnabledForTierAsync_WithTierSpecificFlag_ReturnsTierSpecificValue()
    {
        // Arrange
        var feature = "Features.AdvancedRAG";
        var tier = UserTier.Normal;

        // Mock: Tier-specific flag exists and is enabled
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(
                $"{feature}.Tier.normal",
                It.IsAny<bool?>(),
                null))
            .ReturnsAsync(true);

        // Act
        var isEnabled = await _featureFlagService.IsEnabledForTierAsync(feature, tier);

        // Assert
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessFeatureAsync_CombinesRoleAndTier_BothMustBeTrue()
    {
        // Arrange
        var normalUser = CreateUser(UserTier.Normal, Role.User);
        var feature = "Features.AdvancedRAG";

        // Mock: Role access granted (global feature enabled)
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>(feature, It.IsAny<bool?>(), null))
            .ReturnsAsync(true);

        // Mock: Tier access denied
        _mockConfigService
            .Setup(s => s.GetValueAsync<bool?>($"{feature}.Tier.normal", It.IsAny<bool?>(), null))
            .ReturnsAsync(false);

        // Act
        var canAccess = await _featureFlagService.CanAccessFeatureAsync(normalUser, feature);

        // Assert
        canAccess.Should().BeFalse(); // Tier denial overrides role access
    }

    private static User CreateUser(UserTier tier, Role role)
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email($"test-{Guid.NewGuid()}@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Test123!"),
            role: role,
            tier: tier
        );
    }
}

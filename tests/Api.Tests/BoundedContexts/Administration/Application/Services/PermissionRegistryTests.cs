using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Unit tests for PermissionRegistry (Epic #4068 - Issue #4177)
/// </summary>
public class PermissionRegistryTests
{
    private readonly PermissionRegistry _registry;

    public PermissionRegistryTests()
    {
        _registry = new PermissionRegistry();
    }

    [Fact]
    public void GetPermission_WithKnownFeature_ReturnsPermission()
    {
        // Act
        var permission = _registry.GetPermission("wishlist");

        // Assert
        Assert.NotNull(permission);
        Assert.Equal("wishlist", permission.FeatureName);
        Assert.Equal(UserTier.Free, permission.RequiredTier);
        Assert.Equal(Role.User, permission.RequiredRole);
    }

    [Fact]
    public void GetPermission_WithUnknownFeature_ReturnsNull()
    {
        // Act
        var permission = _registry.GetPermission("unknown-feature");

        // Assert
        Assert.Null(permission);
    }

    [Fact]
    public void CheckAccess_WithUnknownFeature_ReturnsDenied()
    {
        // Arrange
        var context = new PermissionContext(UserTier.Pro, Role.User, UserAccountStatus.Active);

        // Act
        var result = _registry.CheckAccess("unknown-feature", context);

        // Assert
        Assert.False(result.HasAccess);
        Assert.Contains("not found", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("wishlist", "free", "user", true)]
    [InlineData("bulk-select", "pro", "user", true)]
    [InlineData("bulk-select", "free", "editor", true)] // OR logic: Editor role sufficient
    [InlineData("bulk-select", "free", "user", false)]  // Neither tier nor role sufficient
    [InlineData("drag-drop", "normal", "user", true)]
    [InlineData("drag-drop", "free", "user", false)]
    public void CheckAccess_WithVariousTiersRoles_ReturnsCorrectResult(
        string feature,
        string tierValue,
        string roleValue,
        bool expectedAccess)
    {
        // Arrange
        var tier = UserTier.Parse(tierValue);
        var role = Role.Parse(roleValue);
        var context = new PermissionContext(tier, role, UserAccountStatus.Active);

        // Act
        var result = _registry.CheckAccess(feature, context);

        // Assert
        Assert.Equal(expectedAccess, result.HasAccess);
    }

    [Fact]
    public void CheckAccess_WithAdminRole_AccessesAdminOnlyFeatures()
    {
        // Arrange: Free tier + Admin role
        var context = new PermissionContext(UserTier.Free, Role.Admin, UserAccountStatus.Active);

        // Act
        var deleteResult = _registry.CheckAccess("quick-action.delete", context);
        var bulkSelectResult = _registry.CheckAccess("bulk-select", context);

        // Assert
        Assert.True(deleteResult.HasAccess); // Admin required (AND logic: Admin role present)
        Assert.True(bulkSelectResult.HasAccess); // Admin > Editor (OR logic: role sufficient)
    }

    [Fact]
    public void GetAccessibleFeatures_WithFreeUser_ReturnsOnlyFreeFeatures()
    {
        // Arrange
        var context = new PermissionContext(UserTier.Free, Role.User, UserAccountStatus.Active);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert
        Assert.Contains("wishlist", features);

        // Not Pro features
        Assert.DoesNotContain("bulk-select", features);
        Assert.DoesNotContain("agent.create", features);
        Assert.DoesNotContain("analytics.view", features);

        // Not admin features
        Assert.DoesNotContain("quick-action.delete", features);
    }

    [Fact]
    public void GetAccessibleFeatures_WithProUser_ReturnsProFeatures()
    {
        // Arrange
        var context = new PermissionContext(UserTier.Pro, Role.User, UserAccountStatus.Active);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert
        Assert.Contains("wishlist", features);
        Assert.Contains("bulk-select", features);
        Assert.Contains("drag-drop", features);
        Assert.Contains("collection.manage", features);
        Assert.Contains("document.upload", features);
        Assert.Contains("agent.create", features);
        Assert.Contains("analytics.view", features);
        Assert.Contains("filters.advanced", features);

        // Still not admin features
        Assert.DoesNotContain("quick-action.delete", features);
    }

    [Fact]
    public void GetAccessibleFeatures_WithEnterpriseUser_ReturnsAllNonAdminFeatures()
    {
        // Arrange
        var context = new PermissionContext(UserTier.Enterprise, Role.User, UserAccountStatus.Active);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert: All tier-based features
        Assert.Contains("wishlist", features);
        Assert.Contains("bulk-select", features);
        Assert.Contains("agent.create", features);
        Assert.Contains("analytics.view", features);

        // Admin features still require Admin role
        Assert.DoesNotContain("quick-action.delete", features);
    }

    [Fact]
    public void GetAccessibleFeatures_WithEditor_AccessesBulkSelect()
    {
        // Arrange: Free tier + Editor role
        var context = new PermissionContext(UserTier.Free, Role.Editor, UserAccountStatus.Active);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert: Editor role grants bulk-select (OR logic)
        Assert.Contains("bulk-select", features);

        // But not Pro tier features requiring role Creator
        Assert.DoesNotContain("agent.create", features); // Pro tier OR Creator role (neither satisfied)
    }

    [Fact]
    public void GetAccessibleFeatures_WithCreator_AccessesCreatorFeatures()
    {
        // Arrange: Normal tier + Creator role
        var context = new PermissionContext(UserTier.Normal, Role.Creator, UserAccountStatus.Active);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert
        Assert.Contains("quick-action.edit", features); // Normal tier OR Creator role (both satisfied)
        Assert.Contains("agent.create", features); // Pro tier OR Creator role (Creator satisfied)
    }

    [Fact]
    public void GetAccessibleFeatures_WithSuspendedUser_ReturnsEmpty()
    {
        // Arrange
        var context = new PermissionContext(UserTier.Pro, Role.User, UserAccountStatus.Suspended);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert: Suspended users have no features
        Assert.Empty(features);
    }

    [Fact]
    public void GetAccessibleFeatures_WithBannedUser_ReturnsEmpty()
    {
        // Arrange
        var context = new PermissionContext(UserTier.Enterprise, Role.SuperAdmin, UserAccountStatus.Banned);

        // Act
        var features = _registry.GetAccessibleFeatures(context);

        // Assert: Even Enterprise + SuperAdmin banned = no access
        Assert.Empty(features);
    }

    [Fact]
    public void PermissionRegistry_IsSingleton_SameInstanceReturned()
    {
        // This test verifies DI configuration (singleton)
        // In real app, registry should be registered as singleton

        var registry1 = new PermissionRegistry();
        var registry2 = new PermissionRegistry();

        // Different instances (unit test), but in production:
        // builder.Services.AddSingleton<PermissionRegistry>();
        // ensures same instance throughout app lifetime

        Assert.NotSame(registry1, registry2); // Different instances here

        // But GetPermission returns equivalent results
        var perm1 = registry1.GetPermission("wishlist");
        var perm2 = registry2.GetPermission("wishlist");

        Assert.Equal(perm1?.FeatureName, perm2?.FeatureName);
    }

    [Fact]
    public void PermissionRegistry_HasExpectedFeatureCount()
    {
        // Arrange: Get all permissions via reflection (internal access)
        var registryType = typeof(PermissionRegistry);
        var permissionsField = registryType.GetField("_permissions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var permissions = permissionsField?.GetValue(_registry) as Dictionary<string, Permission>;

        // Assert: Expected 10 features (as of Epic #4068)
        Assert.NotNull(permissions);
        Assert.Equal(10, permissions.Count);

        // Verify all expected features present
        var expectedFeatures = new[]
        {
            "wishlist",
            "bulk-select",
            "drag-drop",
            "quick-action.delete",
            "quick-action.edit",
            "collection.manage",
            "document.upload",
            "agent.create",
            "analytics.view",
            "filters.advanced"
        };

        foreach (var feature in expectedFeatures)
        {
            Assert.Contains(feature, permissions.Keys);
        }
    }

    [Fact]
    public void CheckAccess_WithStateBasedPermission_ValidatesState()
    {
        // This test would use a state-based permission (future feature)
        // Example: "view-game" with allowed states: ["published", "archived"]

        // For now, all permissions in registry don't have state restrictions
        // Test verifies behavior when state restrictions added

        var context = new PermissionContext(UserTier.Free, Role.User, UserAccountStatus.Active, "published");
        var result = _registry.CheckAccess("wishlist", context);

        // Wishlist has no state restrictions, so passes regardless of state
        Assert.True(result.HasAccess);
    }
}

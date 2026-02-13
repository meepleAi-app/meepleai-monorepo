using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Tests for Permission value object (Epic #4068 - Issue #4177)
/// </summary>
public class PermissionTests
{
    [Fact]
    public void CreateOr_WithTierAndRole_CreatesOrLogicPermission()
    {
        var permission = Permission.CreateOr("test-feature", UserTier.Pro, Role.Editor);

        Assert.Equal("test-feature", permission.FeatureName);
        Assert.Equal(UserTier.Pro, permission.RequiredTier);
        Assert.Equal(Role.Editor, permission.RequiredRole);
        Assert.Equal(PermissionLogic.Or, permission.Logic);
    }

    [Theory]
    [InlineData("pro", "user", true)]      // Tier sufficient
    [InlineData("normal", "editor", true)] // Role sufficient
    [InlineData("pro", "editor", true)]    // Both sufficient
    [InlineData("normal", "user", false)]  // Neither sufficient
    public void Check_WithOrLogic_ReturnsCorrectResult(string tierValue, string roleValue, bool expectedAccess)
    {
        var permission = Permission.CreateOr("bulk-select", UserTier.Pro, Role.Editor);
        var context = new PermissionContext(
            UserTier.Parse(tierValue),
            Role.Parse(roleValue),
            UserAccountStatus.Active);

        var result = permission.Check(context);

        Assert.Equal(expectedAccess, result.HasAccess);
    }

    [Fact]
    public void Check_WithBannedUser_AlwaysDenies()
    {
        var permission = Permission.CreateOr("any-feature", UserTier.Free, Role.User);
        var context = new PermissionContext(UserTier.Enterprise, Role.SuperAdmin, UserAccountStatus.Banned);

        var result = permission.Check(context);

        Assert.False(result.HasAccess);
        Assert.Contains("banned", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Check_WithSuspendedUser_Denies()
    {
        var permission = Permission.CreateOr("feature", UserTier.Pro, Role.User);
        var context = new PermissionContext(UserTier.Pro, Role.User, UserAccountStatus.Suspended);

        var result = permission.Check(context);

        Assert.False(result.HasAccess);
    }

    [Fact]
    public void Check_WithStateRestriction_ValidatesResourceState()
    {
        var allowedStates = new HashSet<string> { "published", "archived" };
        var permission = Permission.CreateOr("view-game", UserTier.Free, Role.User, allowedStates);

        var publishedCtx = new PermissionContext(UserTier.Free, Role.User, UserAccountStatus.Active, "published");
        var draftCtx = new PermissionContext(UserTier.Free, Role.User, UserAccountStatus.Active, "draft");

        Assert.True(permission.Check(publishedCtx).HasAccess);
        Assert.False(permission.Check(draftCtx).HasAccess);
    }

    [Theory]
    [InlineData("pro", "admin", true)]     // Both sufficient
    [InlineData("pro", "user", false)]     // Role insufficient
    [InlineData("normal", "admin", false)] // Tier insufficient
    public void Check_WithAndLogic_RequiresBothConditions(string tierValue, string roleValue, bool expected)
    {
        var permission = Permission.CreateAnd("admin-feature", UserTier.Pro, Role.Admin);
        var context = new PermissionContext(
            UserTier.Parse(tierValue),
            Role.Parse(roleValue),
            UserAccountStatus.Active);

        Assert.Equal(expected, permission.Check(context).HasAccess);
    }

    [Fact]
    public void CreateOr_WithNullTierAndRole_AllowsEveryone()
    {
        var permission = Permission.CreateOr("public-feature");
        var context = new PermissionContext(UserTier.Free, Role.User, UserAccountStatus.Active);

        var result = permission.Check(context);

        Assert.True(result.HasAccess);
        Assert.Equal("No restrictions", result.Reason);
    }
}

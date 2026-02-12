using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Central registry for all feature permissions
/// Epic #4068 - Issue #4177
/// </summary>
public sealed class PermissionRegistry
{
    private readonly Dictionary<string, Permission> _permissions;

    public PermissionRegistry()
    {
        _permissions = new Dictionary<string, Permission>
        {
            // Wishlist - available to all tiers
            ["wishlist"] = Permission.CreateOr("wishlist", UserTier.Free, Role.User),

            // Bulk selection - Pro tier or Editor role
            ["bulk-select"] = Permission.CreateOr("bulk-select", UserTier.Pro, Role.Editor),

            // Drag & drop - Normal tier minimum
            ["drag-drop"] = Permission.CreateOr("drag-drop", UserTier.Normal, Role.User),

            // Quick action: Delete - Admin only
            ["quick-action.delete"] = Permission.CreateAnd("quick-action.delete", UserTier.Free, Role.Admin),

            // Quick action: Edit - Creator or higher
            ["quick-action.edit"] = Permission.CreateOr("quick-action.edit", UserTier.Normal, Role.Creator),

            // Collection management - Normal tier minimum
            ["collection.manage"] = Permission.CreateOr("collection.manage", UserTier.Normal, Role.User),

            // PDF upload - Normal tier minimum
            ["document.upload"] = Permission.CreateOr("document.upload", UserTier.Normal, Role.User),

            // Agent creation - Pro tier or Creator
            ["agent.create"] = Permission.CreateOr("agent.create", UserTier.Pro, Role.Creator),

            // Analytics access - Pro tier or Admin
            ["analytics.view"] = Permission.CreateOr("analytics.view", UserTier.Pro, Role.Admin),

            // Advanced filters - Normal tier minimum
            ["filters.advanced"] = Permission.CreateOr("filters.advanced", UserTier.Normal, Role.User)
        };
    }

    public Permission? GetPermission(string featureName)
    {
        return _permissions.GetValueOrDefault(featureName);
    }

    public PermissionCheckResult CheckAccess(string featureName, PermissionContext context)
    {
        var permission = GetPermission(featureName);

        if (permission == null)
            return PermissionCheckResult.Denied($"Feature '{featureName}' not found in registry");

        return permission.Check(context);
    }

    public IReadOnlyList<string> GetAccessibleFeatures(PermissionContext context)
    {
        return _permissions
            .Where(kvp => kvp.Value.Check(context).HasAccess)
            .Select(kvp => kvp.Key)
            .ToList();
    }
}

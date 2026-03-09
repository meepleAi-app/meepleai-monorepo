using Api.BoundedContexts.Administration.Domain.Enums;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Permission configuration for features
/// Epic #4068 - Issue #4177
/// </summary>
public sealed class Permission
{
    public string FeatureName { get; }
    public UserTier? RequiredTier { get; }
    public Role? RequiredRole { get; }
    public PermissionLogic Logic { get; }
    public HashSet<string>? AllowedStates { get; }

    private Permission(
        string featureName,
        UserTier? requiredTier,
        Role? requiredRole,
        PermissionLogic logic,
        HashSet<string>? allowedStates = null)
    {
        FeatureName = featureName;
        RequiredTier = requiredTier;
        RequiredRole = requiredRole;
        Logic = logic;
        AllowedStates = allowedStates;
    }

    public static Permission CreateOr(
        string featureName,
        UserTier? tier = null,
        Role? role = null,
        HashSet<string>? allowedStates = null)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name required", nameof(featureName));

        return new Permission(featureName, tier, role, PermissionLogic.Or, allowedStates);
    }

    public static Permission CreateAnd(
        string featureName,
        UserTier tier,
        Role role,
        HashSet<string>? allowedStates = null)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name required", nameof(featureName));

        return new Permission(featureName, tier, role, PermissionLogic.And, allowedStates);
    }

    public PermissionCheckResult Check(PermissionContext context)
    {
        if (context.UserStatus == UserAccountStatus.Banned)
            return PermissionCheckResult.Denied("User account is banned");

        if (context.UserStatus == UserAccountStatus.Suspended)
            return PermissionCheckResult.Denied("User account is suspended");

        if (AllowedStates != null &&
            !string.IsNullOrEmpty(context.ResourceState) &&
            !AllowedStates.Contains(context.ResourceState))
        {
            return PermissionCheckResult.Denied($"Resource state '{context.ResourceState}' not allowed");
        }

        if (RequiredTier == null && RequiredRole == null)
            return PermissionCheckResult.Allowed("No restrictions");

        bool tierSufficient = RequiredTier == null || context.UserTier.HasLevel(RequiredTier);
        bool roleSufficient = RequiredRole == null || context.UserRole.HasPermission(RequiredRole);

        return Logic switch
        {
            PermissionLogic.Or => (tierSufficient || roleSufficient)
                ? PermissionCheckResult.Allowed(tierSufficient ? "Tier sufficient" : "Role sufficient")
                : PermissionCheckResult.Denied("Neither tier nor role sufficient"),

            PermissionLogic.And => (tierSufficient && roleSufficient)
                ? PermissionCheckResult.Allowed("Both tier and role sufficient")
                : PermissionCheckResult.Denied("Both tier and role required"),

            _ => throw new InvalidOperationException($"Invalid permission logic: {Logic}")
        };
    }
}

public enum PermissionLogic
{
    Or,
    And
}

public record PermissionContext(
    UserTier UserTier,
    Role UserRole,
    UserAccountStatus UserStatus,
    string? ResourceState = null);

public record PermissionCheckResult(bool HasAccess, string Reason)
{
    public static PermissionCheckResult Allowed(string reason) => new(true, reason);
    public static PermissionCheckResult Denied(string reason) => new(false, reason);
}

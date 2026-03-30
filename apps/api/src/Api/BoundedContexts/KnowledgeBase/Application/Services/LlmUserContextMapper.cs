using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Maps Authentication domain User entities to KB Domain LlmUserContext value objects.
/// Extracted from HybridAdaptiveRoutingStrategy (Issue #5487 refactoring).
/// Lives in Application layer to avoid KB Domain importing Authentication BC.
/// </summary>
internal static class LlmUserContextMapper
{
    /// <summary>
    /// Map from UserId and role name string to LlmUserContext.
    /// Used when the full User entity is not available (e.g., from query parameters).
    /// Determines tier from role name only (not subscription tier).
    /// </summary>
    public static LlmUserContext FromRoleString(Guid? userId, string? roleName)
    {
        if (userId == null || string.IsNullOrEmpty(roleName))
            return LlmUserContext.Internal;

        var tier = MapTierFromRoleName(roleName);
        return new LlmUserContext(userId, roleName, tier);
    }

    private static LlmUserTier MapTierFromRoleName(string roleName)
    {
        if (string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, "superadmin", StringComparison.OrdinalIgnoreCase))
            return LlmUserTier.Admin;

        if (string.Equals(roleName, "editor", StringComparison.OrdinalIgnoreCase))
            return LlmUserTier.Editor;

        return LlmUserTier.User;
    }

    /// <summary>
    /// Map a User entity to an LlmUserContext value object.
    /// Null user (internal pipeline calls like query rewriting) defaults to User tier.
    /// </summary>
    public static LlmUserContext FromUser(User? user)
    {
        if (user is null)
        {
            // Internal pipeline calls (e.g., query rewriting) pass user=null.
            // Use User tier as default so they can access basic strategies.
            return LlmUserContext.Internal;
        }

        return new LlmUserContext(
            UserId: user.Id,
            RoleName: user.Role.Value,
            Tier: MapTier(user));
    }

    /// <summary>
    /// Map user role and subscription tier to LlmUserTier.
    /// E4-2: Tier-aware LLM routing — premium subscribers get Premium tier
    /// regardless of their role. Admin role always wins.
    /// </summary>
    private static LlmUserTier MapTier(User user)
    {
        // Admin and superadmin roles always get Admin tier (highest access)
        if (string.Equals(user.Role.Value, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Role.Value, "superadmin", StringComparison.OrdinalIgnoreCase))
        {
            return LlmUserTier.Admin;
        }

        // Editor role gets Editor tier
        if (string.Equals(user.Role.Value, "editor", StringComparison.OrdinalIgnoreCase))
        {
            return LlmUserTier.Editor;
        }

        // E4-2: Check subscription tier for premium/enterprise users
        if (user.Tier.IsPremium() || user.Tier.IsEnterprise())
        {
            return LlmUserTier.Premium;
        }

        // Free/normal subscription tier → User LLM tier
        return LlmUserTier.User;
    }
}

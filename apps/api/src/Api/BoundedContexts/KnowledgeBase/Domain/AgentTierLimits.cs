using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Centralized agent slot limits per user tier.
/// Issue #4771: Agent Slots Endpoint + Quota System.
/// </summary>
internal static class AgentTierLimits
{
    /// <summary>
    /// Maximum number of agents a user can create, keyed by tier name (case-insensitive lookup).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> MaxAgentsPerTier =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["free"] = 3,
            ["normal"] = 10,
            ["premium"] = 50,
            ["pro"] = 50,
            ["enterprise"] = 200
        };

    public const int DefaultMaxAgents = 3;

    /// <summary>
    /// Returns the maximum number of agents for a given tier and role.
    /// Editor, admin and superadmin get unrestricted access.
    /// </summary>
    public static int GetMaxAgents(string? tier, string? role)
    {
        if (IsAdminOrEditor(role))
            return int.MaxValue;

        return MaxAgentsPerTier.GetValueOrDefault(tier?.ToLowerInvariant() ?? "free", DefaultMaxAgents);
    }

    /// <summary>
    /// True when the role is unrestricted: editor, admin or superadmin — the same set as
    /// <c>Role.HasPermission(Role.Editor)</c>, and the set that
    /// <c>ClaimsPrincipalExtensions.IsAdminOrEditor</c> already mirrors from
    /// "AdminOrEditorPolicy". Creator is deliberately excluded and pays the tier limit.
    /// <para>
    /// #3994: this used to enumerate "Admin" and "Editor" as literals, so superadmin fell
    /// through to the per-tier lookup. The bootstrap account is created as
    /// <c>Role.SuperAdmin</c> and inherits the default <c>UserTier.Free</c>, so it was the
    /// account getting free-tier limits.
    /// </para>
    /// An absent or unknown role fails closed, i.e. pays the tier limit.
    /// </summary>
    public static bool IsAdminOrEditor(string? role) =>
        Role.TryParse(role, out var parsed) && parsed.HasPermission(Role.Editor);
}

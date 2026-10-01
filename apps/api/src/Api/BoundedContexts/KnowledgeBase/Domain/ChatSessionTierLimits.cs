using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Centralized chat session history limits per user tier.
/// Issue #4913: Tier-based session history with sliding window auto-archive.
/// </summary>
internal static class ChatSessionTierLimits
{
    /// <summary>
    /// Maximum number of active chat sessions per tier name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> MaxSessionsPerTier =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["anonymous"] = 10,
            ["free"] = 10,
            ["user"] = 100,
            ["normal"] = 100,
            ["premium"] = 1000,
            ["pro"] = 1000,
            ["editor"] = 0,       // unlimited
            ["admin"] = 0,        // unlimited
            ["enterprise"] = 0    // unlimited
        };

    public const int DefaultLimit = 10;

    /// <summary>
    /// Returns the maximum number of active chat sessions for a given tier and role.
    /// Editor, admin and superadmin get unrestricted access (returns 0 = unlimited).
    /// </summary>
    public static int GetLimit(string? tier, string? role)
    {
        if (IsAdminOrEditor(role))
            return 0; // unlimited

        var key = tier?.ToLowerInvariant() ?? "anonymous";
        return MaxSessionsPerTier.GetValueOrDefault(key, DefaultLimit);
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

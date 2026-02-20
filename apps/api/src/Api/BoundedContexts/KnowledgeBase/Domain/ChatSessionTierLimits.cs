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
    /// Admin/Editor roles get unrestricted access (returns 0 = unlimited).
    /// </summary>
    public static int GetLimit(string? tier, string? role)
    {
        if (IsAdminOrEditor(role))
            return 0; // unlimited

        var key = tier?.ToLowerInvariant() ?? "anonymous";
        return MaxSessionsPerTier.GetValueOrDefault(key, DefaultLimit);
    }

    /// <summary>
    /// Returns true if the role is Admin or Editor (unrestricted).
    /// </summary>
    public static bool IsAdminOrEditor(string? role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase);
}

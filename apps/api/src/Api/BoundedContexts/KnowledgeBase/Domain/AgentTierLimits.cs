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
    /// Admin/Editor roles get unrestricted access.
    /// </summary>
    public static int GetMaxAgents(string? tier, string? role)
    {
        if (IsAdminOrEditor(role))
            return int.MaxValue;

        return MaxAgentsPerTier.GetValueOrDefault(tier?.ToLowerInvariant() ?? "free", DefaultMaxAgents);
    }

    /// <summary>
    /// Returns true if the role is Admin or Editor (unrestricted).
    /// </summary>
    public static bool IsAdminOrEditor(string? role)
    {
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase);
    }
}

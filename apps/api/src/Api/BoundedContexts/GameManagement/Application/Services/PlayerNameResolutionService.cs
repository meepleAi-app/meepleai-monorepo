namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Resolves player names using fuzzy matching strategies:
/// 1. Exact full name match (case-insensitive)
/// 2. First name match (first word of display name)
/// 3. Contains match (substring)
/// Returns Ambiguous if multiple matches found at any level, NotFound if none.
/// </summary>
internal sealed class PlayerNameResolutionService : IPlayerNameResolutionService
{
    public PlayerResolutionResult ResolvePlayer(string playerName, IReadOnlyDictionary<Guid, string> sessionPlayers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
        ArgumentNullException.ThrowIfNull(sessionPlayers);

        var trimmedName = playerName.Trim();

        // Strategy 1: Exact full name match (case-insensitive)
        var exactMatches = sessionPlayers
            .Where(p => string.Equals(p.Value, trimmedName, StringComparison.OrdinalIgnoreCase))
            .Select(p => (p.Key, p.Value))
            .ToList();

        if (exactMatches.Count == 1)
            return PlayerResolutionResult.Resolved(exactMatches[0].Key, exactMatches[0].Value);

        if (exactMatches.Count > 1)
            return PlayerResolutionResult.Ambiguous(exactMatches);

        // Strategy 2: First name match (first word of display name)
        var firstNameMatches = sessionPlayers
            .Where(p => GetFirstName(p.Value).Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
            .Select(p => (p.Key, p.Value))
            .ToList();

        if (firstNameMatches.Count == 1)
            return PlayerResolutionResult.Resolved(firstNameMatches[0].Key, firstNameMatches[0].Value);

        if (firstNameMatches.Count > 1)
            return PlayerResolutionResult.Ambiguous(firstNameMatches);

        // Strategy 3: Contains match (substring)
        var containsMatches = sessionPlayers
            .Where(p => p.Value.Contains(trimmedName, StringComparison.OrdinalIgnoreCase))
            .Select(p => (p.Key, p.Value))
            .ToList();

        if (containsMatches.Count == 1)
            return PlayerResolutionResult.Resolved(containsMatches[0].Key, containsMatches[0].Value);

        if (containsMatches.Count > 1)
            return PlayerResolutionResult.Ambiguous(containsMatches);

        return PlayerResolutionResult.NotFound();
    }

    private static string GetFirstName(string displayName)
    {
        var spaceIndex = displayName.IndexOf(' ');
        return spaceIndex > 0 ? displayName[..spaceIndex] : displayName;
    }
}

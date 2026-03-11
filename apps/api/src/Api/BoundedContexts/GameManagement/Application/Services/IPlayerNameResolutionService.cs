namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Service for resolving player names from natural language input to session player IDs.
/// Supports fuzzy matching with exact, first-name, and contains strategies.
/// </summary>
internal interface IPlayerNameResolutionService
{
    PlayerResolutionResult ResolvePlayer(string playerName, IReadOnlyDictionary<Guid, string> sessionPlayers);
}

/// <summary>
/// Result of attempting to resolve a player name against session players.
/// </summary>
internal sealed record PlayerResolutionResult
{
    public bool IsResolved { get; init; }
    public Guid? PlayerId { get; init; }
    public string? ResolvedName { get; init; }
    public bool IsAmbiguous { get; init; }
    public IReadOnlyList<(Guid Id, string Name)> Candidates { get; init; } = [];

    public static PlayerResolutionResult Resolved(Guid id, string name) =>
        new() { IsResolved = true, PlayerId = id, ResolvedName = name };

    public static PlayerResolutionResult Ambiguous(IReadOnlyList<(Guid, string)> candidates) =>
        new() { IsAmbiguous = true, Candidates = candidates };

    public static PlayerResolutionResult NotFound() => new();
}

namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Lightweight DTO carrying live session state for agent context injection.
/// Avoids direct dependency on GameManagement domain entities.
/// </summary>
internal sealed record LiveSessionContext(
    Guid SessionId,
    string GameName,
    int CurrentTurnIndex,
    int CurrentPhaseIndex,
    string? CurrentPhaseName,
    string[] PhaseNames,
    string? CurrentPlayerDisplayName,
    IReadOnlyList<string> ActivePlayerNames,
    string TurnAdvancePolicy);

#pragma warning disable MA0048 // File name must match type name - the assistant's view of a session
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Lo stato di una partita come lo vede l'assistente (spec §5.1). Lo costruisce un
/// <see cref="ISessionAssistantReader"/>: dalla sessione live in produzione, da uno scenario nello spike.
/// </summary>
internal sealed record AssistantSessionState(
    Guid? GameId,
    string? GameName,
    string Status,
    bool CanRecordScores,
    bool CanAdvanceTurn,
    IReadOnlyList<AssistantPlayer> Players,
    Guid? CurrentTurnPlayerId,
    int CurrentTurnIndex,
    int CurrentRound,
    string? CurrentPhase,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<AssistantScoreCell> Scores,
    IReadOnlyList<string> HouseRules);

internal sealed record AssistantPlayer(Guid Id, string Name, int Total, bool IsActive);

internal sealed record AssistantScoreCell(Guid PlayerId, int Round, string Dimension, int Value);

internal sealed record AssistantRulePassage(string Text, int? Page, string? Document);

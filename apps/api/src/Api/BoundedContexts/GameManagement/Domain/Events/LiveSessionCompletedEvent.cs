using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a live game session is completed.
/// Carries a snapshot of session data needed for PlayRecord generation.
/// </summary>
internal sealed class LiveSessionCompletedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public DateTime CompletedAt { get; }
    public int TotalTurns { get; }

    // Session snapshot for PlayRecord generation
    public Guid? GameId { get; }
    public string GameName { get; }
    public Guid CreatedByUserId { get; }
    public PlayRecordVisibility Visibility { get; }
    public Guid? GroupId { get; }
    public DateTime SessionDate { get; }
    public DateTime? StartedAt { get; }
    public string? Notes { get; }
    public IReadOnlyList<CompletedPlayerSnapshot> Players { get; }
    public IReadOnlyList<CompletedScoreSnapshot> Scores { get; }

    public LiveSessionCompletedEvent(
        Guid sessionId,
        DateTime completedAt,
        int totalTurns,
        Guid? gameId,
        string gameName,
        Guid createdByUserId,
        PlayRecordVisibility visibility,
        Guid? groupId,
        DateTime sessionDate,
        DateTime? startedAt,
        string? notes,
        IReadOnlyList<CompletedPlayerSnapshot> players,
        IReadOnlyList<CompletedScoreSnapshot> scores)
    {
        SessionId = sessionId;
        CompletedAt = completedAt;
        TotalTurns = totalTurns;
        GameId = gameId;
        GameName = gameName;
        CreatedByUserId = createdByUserId;
        Visibility = visibility;
        GroupId = groupId;
        SessionDate = sessionDate;
        StartedAt = startedAt;
        Notes = notes;
        Players = players;
        Scores = scores;
    }
}

/// <summary>
/// Snapshot of a player at session completion time for PlayRecord generation.
/// </summary>
internal sealed record CompletedPlayerSnapshot(
    Guid PlayerId,
    Guid? UserId,
    string DisplayName,
    int TotalScore,
    int CurrentRank);

/// <summary>
/// Snapshot of a round score at session completion time for PlayRecord generation.
/// </summary>
internal sealed record CompletedScoreSnapshot(
    Guid PlayerId,
    string Dimension,
    int Value,
    string? Unit);

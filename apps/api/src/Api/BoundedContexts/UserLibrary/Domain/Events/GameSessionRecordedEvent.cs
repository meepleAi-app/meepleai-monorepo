using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a game session is recorded.
/// </summary>
internal sealed class GameSessionRecordedEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry for which the session was recorded.
    /// </summary>
    public Guid LibraryEntryId { get; }

    /// <summary>
    /// The user who owns the library entry.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The game that was played.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// The ID of the recorded session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// When the session was played.
    /// </summary>
    public DateTime PlayedAt { get; }

    /// <summary>
    /// Duration of the session in minutes.
    /// </summary>
    public int DurationMinutes { get; }

    /// <summary>
    /// Whether the user won (null for non-competitive).
    /// </summary>
    public bool? DidWin { get; }

    public GameSessionRecordedEvent(
        Guid libraryEntryId,
        Guid userId,
        Guid gameId,
        Guid sessionId,
        DateTime playedAt,
        int durationMinutes,
        bool? didWin,
        DateTime occurredAt)
    {
        LibraryEntryId = libraryEntryId;
        UserId = userId;
        GameId = gameId;
        SessionId = sessionId;
        PlayedAt = playedAt;
        DurationMinutes = durationMinutes;
        DidWin = didWin;
    }
}

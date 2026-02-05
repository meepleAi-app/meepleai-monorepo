using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when dice are rolled in a session.
/// Broadcasted via SSE for real-time updates to all participants.
/// </summary>
public record DiceRolledEvent : INotification
{
    /// <summary>
    /// Dice roll unique identifier.
    /// </summary>
    public Guid DiceRollId { get; init; }

    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Participant who performed the roll.
    /// </summary>
    public Guid ParticipantId { get; init; }

    /// <summary>
    /// Display name of the participant who rolled.
    /// </summary>
    public string ParticipantName { get; init; } = string.Empty;

    /// <summary>
    /// Dice formula used (e.g., "2D6+3").
    /// </summary>
    public string Formula { get; init; } = string.Empty;

    /// <summary>
    /// Optional label for the roll.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Individual dice results.
    /// </summary>
    public int[] Rolls { get; init; } = [];

    /// <summary>
    /// Modifier applied to the roll.
    /// </summary>
    public int Modifier { get; init; }

    /// <summary>
    /// Total result (sum of rolls + modifier).
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// When the roll occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

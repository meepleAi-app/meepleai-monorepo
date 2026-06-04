using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to start a second live (InProgress) session on a
/// GameNightEvent that already has an active live session. Enforces invariante #10
/// of the GameNight/Session domain model: a GameNightEvent can have at most 1
/// GameNightSession in InProgress at a time.
/// </summary>
public sealed class MaxLiveSessionsExceededException : DomainException
{
    public const string Code = "MAX_LIVE_SESSIONS_EXCEEDED";

    public Guid GameNightEventId { get; }

    public MaxLiveSessionsExceededException(Guid gameNightEventId)
        : base($"GameNightEvent {gameNightEventId} already has an active live session. At most 1 session may be InProgress at a time (invariant #10).")
    {
        GameNightEventId = gameNightEventId;
    }
}

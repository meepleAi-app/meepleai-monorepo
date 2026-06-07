using System.Diagnostics.CodeAnalysis;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to start a second live (InProgress) session on a
/// GameNightEvent that already has an active live session. Enforces invariante #10
/// of the GameNight/Session domain model: a GameNightEvent can have at most 1
/// GameNightSession in InProgress at a time.
///
/// <para>Extends <see cref="ConflictException"/> so the HTTP layer auto-maps this
/// to <c>409 Conflict</c> via the exception middleware (final code review IMPORTANT fix —
/// previously extended <see cref="Api.SharedKernel.Domain.Exceptions.DomainException"/>
/// which maps to 400 Bad Request, violating spec WP1 T1 contract).</para>
/// </summary>
public sealed class MaxLiveSessionsExceededException : ConflictException
{
    public const string Code = "MAX_LIVE_SESSIONS_EXCEEDED";

    public Guid GameNightEventId { get; }

    [SetsRequiredMembers]
    public MaxLiveSessionsExceededException(Guid gameNightEventId)
        : base($"GameNightEvent {gameNightEventId} already has an active live session. At most 1 session may be InProgress at a time (invariant #10).")
    {
        GameNightEventId = gameNightEventId;
    }
}

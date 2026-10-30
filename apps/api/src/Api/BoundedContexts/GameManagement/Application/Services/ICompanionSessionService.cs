namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Anti-corruption boundary (ADR-083 SP0): GameManagement creates a SessionTracking.Session
/// companion at-creation without referencing SessionTracking types in its Application layer.
/// The companion's id becomes LiveGameSession.TrackingSessionId (cross-BC correlation bridge).
/// </summary>
public interface ICompanionSessionService
{
    /// <summary>
    /// Adds (no SaveChanges) a companion Session and returns its id.
    /// Caller commits it atomically together with the LiveGameSession via IUnitOfWork.
    /// </summary>
    Task<Guid> CreateCompanionAsync(Guid userId, Guid gameId, CancellationToken ct);

    /// <summary>
    /// True iff the tracking Session exists and was created by <paramref name="userId"/>. #4752: a live
    /// session that ADOPTS an existing tracking Session (the game night → live game bridge) checks this
    /// before taking it, so only its owner — or the organizer of the night it belongs to — can.
    /// </summary>
    Task<bool> IsOwnedByAsync(Guid trackingSessionId, Guid userId, CancellationToken ct);
}

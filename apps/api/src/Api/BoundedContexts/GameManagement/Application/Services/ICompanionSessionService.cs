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
    /// <param name="userId">The creator of the live game, owner of the companion.</param>
    /// <param name="gameId">The game, which must exist.</param>
    /// <param name="ownerDisplayName">
    /// #4780: the name the owner has at the table. The history and the dice name the companion's owner by it; with
    /// none, they read «Owner».
    /// </param>
    /// <param name="ct">Cancellation.</param>
    Task<Guid> CreateCompanionAsync(Guid userId, Guid gameId, string? ownerDisplayName, CancellationToken ct);

    /// <summary>
    /// True iff the tracking Session exists and was created by <paramref name="userId"/>. #4752: a live
    /// session that ADOPTS an existing tracking Session (the game night → live game bridge) checks this
    /// before taking it, so only its owner — or the organizer of the night it belongs to — can.
    /// </summary>
    Task<bool> IsOwnedByAsync(Guid trackingSessionId, Guid userId, CancellationToken ct);
}

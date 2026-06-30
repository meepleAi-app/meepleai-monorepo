using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of the ADR-083 SP0 anti-corruption layer.
/// Creates a SessionTracking.Session companion at-creation of a LiveGameSession.
/// The coupling to SessionTracking types lives exclusively in this Infrastructure class
/// so the Application layer (ICompanionSessionService) stays free of cross-BC references.
/// Issue #2501 SP0.
/// </summary>
internal sealed class CompanionSessionService : ICompanionSessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public CompanionSessionService(
        ISessionRepository sessionRepository,
        ISharedGameRepository sharedGameRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    /// <inheritdoc />
    public async Task<Guid> CreateCompanionAsync(Guid userId, Guid gameId, CancellationToken ct)
    {
        // #2552: the companion's game_id is an FK to shared_games (Restrict). Session.Create only
        // validates Guid != Empty, so a valid-but-nonexistent GameId would otherwise surface as a
        // 500 FK violation at the caller's SaveChanges. Pre-flight the existence check and map a
        // missing game to a 404 (respects #2568 "never InvalidOperationException for constraint failures").
        if (!await _sharedGameRepository.ExistsByIdAsync(gameId, ct).ConfigureAwait(false))
        {
            throw new NotFoundException("Game", gameId.ToString());
        }

        var companion = Session.Create(userId, gameId, SessionType.GameSpecific);
        await _sessionRepository.AddAsync(companion, ct).ConfigureAwait(false);
        return companion.Id;
    }
}

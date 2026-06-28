using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;

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

    public CompanionSessionService(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    /// <inheritdoc />
    public async Task<Guid> CreateCompanionAsync(Guid userId, Guid gameId, CancellationToken ct)
    {
        var companion = Session.Create(userId, gameId, SessionType.GameSpecific);
        await _sessionRepository.AddAsync(companion, ct).ConfigureAwait(false);
        return companion.Id;
    }
}

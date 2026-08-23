using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Implementazione di <see cref="ISessionAccessGuard"/>. Issue #3756.
/// </summary>
internal sealed class SessionAccessGuard : ISessionAccessGuard
{
    private readonly ISessionRepository _sessionRepository;

    public SessionAccessGuard(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task EnsureOwnerOrParticipantAsync(
        Guid sessionId,
        Guid requestedBy,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {sessionId} not found");

        // Fail-closed: un requestedBy vuoto non e' un'identita', ed e' cio' che si ottiene se un
        // endpoint dimentica di derivarlo dal principal. Non deve mai combaciare con nulla — un
        // partecipante guest ha UserId null, quindi il confronto con Guid.Empty non lo salva.
        if (requestedBy == Guid.Empty ||
            (session.UserId != requestedBy &&
             !session.Participants.Any(p => p.UserId == requestedBy)))
        {
            throw new ForbiddenException(
                $"User {requestedBy} is not authorized to write to session {sessionId}.");
        }
    }
}

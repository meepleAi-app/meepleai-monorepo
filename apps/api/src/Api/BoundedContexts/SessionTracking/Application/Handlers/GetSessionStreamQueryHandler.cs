using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for GetSessionStreamQuery - Provides SSE event stream with authorization.
/// Verifies user has access to session before returning subscription.
/// </summary>
public class GetSessionStreamQueryHandler : IRequestHandler<GetSessionStreamQuery, IAsyncEnumerable<INotification>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionSyncService _syncService;

    public GetSessionStreamQueryHandler(
        ISessionRepository sessionRepository,
        ISessionSyncService syncService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    public async Task<IAsyncEnumerable<INotification>> Handle(GetSessionStreamQuery request, CancellationToken cancellationToken)
    {
        // Verify session exists
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Verify user has access (owner or participant)
        var isOwner = session.UserId == request.UserId;
        var isParticipant = session.Participants.Any(p => p.UserId == request.UserId);

        if (!isOwner && !isParticipant)
        {
            throw new UnauthorizedAccessException($"User {request.UserId} is not authorized to access session {request.SessionId}");
        }

        // Return subscription stream
        return _syncService.SubscribeToSessionEvents(request.SessionId, cancellationToken);
    }
}

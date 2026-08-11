using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Handles <see cref="GetLiveSessionStreamContextQuery"/>.
/// Resolves session-found, caller-authorized, and companion-presence without throwing —
/// returns a thin context result so the SSE endpoint controls the HTTP response shape.
/// Issue #2561 SP2 T4.
/// </summary>
internal sealed class GetLiveSessionStreamContextQueryHandler
    : IQueryHandler<GetLiveSessionStreamContextQuery, LiveSessionStreamContextResult>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetLiveSessionStreamContextQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<LiveSessionStreamContextResult> Handle(
        GetLiveSessionStreamContextQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository
            .GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
            return new LiveSessionStreamContextResult(Found: false, Authorized: false, HasCompanion: false);

        // Authz: creator or any active linked player (registered user) may subscribe.
        // Inactive (removed/kicked) players lose stream access. Single source of truth (#2573).
        var isAuthorized = session.IsAuthorizedParticipant(query.UserId);

        var hasCompanion = session.TrackingSessionId.HasValue;

        return new LiveSessionStreamContextResult(
            Found: true,
            Authorized: isAuthorized,
            HasCompanion: hasCompanion);
    }
}

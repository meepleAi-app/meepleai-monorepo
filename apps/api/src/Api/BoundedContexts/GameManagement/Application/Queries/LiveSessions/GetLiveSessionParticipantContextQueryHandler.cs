using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Handles <see cref="GetLiveSessionParticipantContextQuery"/>. Resolves session-found and
/// caller-authorized without throwing, so the RequireLiveSessionParticipant endpoint filter
/// controls the HTTP response shape. Mirrors <see cref="GetLiveSessionStreamContextQueryHandler"/>.
/// Issue #2573.
/// </summary>
internal sealed class GetLiveSessionParticipantContextQueryHandler
    : IQueryHandler<GetLiveSessionParticipantContextQuery, LiveSessionParticipantContextResult>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetLiveSessionParticipantContextQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<LiveSessionParticipantContextResult> Handle(
        GetLiveSessionParticipantContextQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository
            .GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return new LiveSessionParticipantContextResult(Found: false, Authorized: false);
        }

        return new LiveSessionParticipantContextResult(
            Found: true,
            Authorized: session.IsAuthorizedParticipant(query.UserId));
    }
}

using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Handles <see cref="GetLiveSessionDiaryQuery"/>.
/// Returns all diary entries for the session ordered by <c>CreatedAt</c> ascending.
/// Issue #2570 SP3 T4.
/// </summary>
internal sealed class GetLiveSessionDiaryQueryHandler
    : IQueryHandler<GetLiveSessionDiaryQuery, IReadOnlyList<DiaryEntryDto>>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetLiveSessionDiaryQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<IReadOnlyList<DiaryEntryDto>> Handle(
        GetLiveSessionDiaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository
            .GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        // Authz: caller must be the session creator or an active linked player.
        // Mirrors GetLiveSessionStreamContextQueryHandler (SP2 T4, issue #2561).
        var isParticipant = session.IsAuthorizedParticipant(query.UserId);
        if (!isParticipant)
            throw new ForbiddenException("Only the session creator or an active participant may read diary entries.");

        return session.DiaryEntries
            .OrderBy(e => e.CreatedAt)
            .Select(e => new DiaryEntryDto(e.Id, e.AuthorId, e.CreatedAt, e.Text))
            .ToList();
    }
}

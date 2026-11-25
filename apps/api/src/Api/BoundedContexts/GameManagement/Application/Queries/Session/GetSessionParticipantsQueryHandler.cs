using Api.BoundedContexts.GameManagement.Application.Queries.Session;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.SharedKernel.Domain.Validation;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Session;

/// <summary>
/// Handles querying session participants ordered by join time.
/// E3-1: Session Invite Flow.
/// </summary>
internal sealed class GetSessionParticipantsQueryHandler : IRequestHandler<GetSessionParticipantsQuery, IReadOnlyList<SessionParticipantDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetSessionParticipantsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SessionParticipantDto>> Handle(GetSessionParticipantsQuery request, CancellationToken cancellationToken)
    {
        var participants = await _dbContext.SessionParticipants
            .Where(p => p.SessionId == request.SessionId)
            .OrderBy(p => p.JoinedAt)
            .Select(p => new
            {
                p.Id,
                p.SessionId,
                p.UserId,
                p.GuestName,
                UserDisplayName = p.User != null ? p.User.DisplayName : null,
                UserEmail = p.User != null ? p.User.Email : null,
                p.Role,
                p.AgentAccessEnabled,
                p.JoinedAt,
                p.LeftAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // #4780: the name every participant sees — a guest's own, or the user's, never the user's address. The
        // rule cannot run in SQL, so the projection brings the parts and the name is made here.
        return participants.Select(p => new SessionParticipantDto(
                p.Id,
                p.SessionId,
                p.UserId,
                p.GuestName,
                p.GuestName ?? PlayerDisplayNames.ForOthers(p.UserDisplayName, p.UserEmail, PlayerDisplayNames.Player),
                p.Role,
                p.AgentAccessEnabled,
                p.JoinedAt,
                p.LeftAt))
            .ToList();
    }
}

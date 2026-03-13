using Api.BoundedContexts.GameManagement.Application.Queries.Session;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.Session;

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
        return await _dbContext.SessionParticipants
            .Where(p => p.SessionId == request.SessionId)
            .OrderBy(p => p.JoinedAt)
            .Select(p => new SessionParticipantDto(
                p.Id,
                p.SessionId,
                p.UserId,
                p.GuestName,
                p.GuestName ?? "User",
                p.Role,
                p.AgentAccessEnabled,
                p.JoinedAt,
                p.LeftAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

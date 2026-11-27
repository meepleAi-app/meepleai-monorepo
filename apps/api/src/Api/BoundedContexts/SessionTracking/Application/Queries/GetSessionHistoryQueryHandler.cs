using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetSessionHistoryQueryHandler : IRequestHandler<GetSessionHistoryQuery, List<SessionSummaryDto>>
{
    private readonly MeepleAiDbContext _context;

    public GetSessionHistoryQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<List<SessionSummaryDto>> Handle(GetSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SessionTrackingSessions
            .Include(s => s.Participants)
            .Where(s => s.CreatedBy == request.UserId && !s.IsDeleted);

        // Optional GameId filter
        if (request.GameId.HasValue)
        {
            query = query.Where(s => s.GameId == request.GameId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.SessionDate)
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(s => new SessionSummaryDto
            {
                Id = s.Id,
                SessionCode = s.SessionCode,
                SessionDate = s.SessionDate,
                GameName = null,
                GameIcon = null,
                ParticipantsNames = string.Join(", ", s.Participants
                    .OrderBy(p => p.JoinOrder)
                    .Select(p => p.DisplayName)),
                // #4780: a tie at the top (1, 1, 3) has no winner; FirstOrDefault named whichever of the two came first.
                WinnerName = s.Participants.Count(p => p.FinalRank == 1) == 1
                    ? s.Participants
                        .Where(p => p.FinalRank == 1)
                        .Select(p => p.DisplayName)
                        .FirstOrDefault()
                    : null,
                DurationMinutes = s.FinalizedAt.HasValue
                    ? (int)(s.FinalizedAt.Value - s.SessionDate).TotalMinutes
                    : 0,
                Status = s.Status
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return sessions;
    }
}

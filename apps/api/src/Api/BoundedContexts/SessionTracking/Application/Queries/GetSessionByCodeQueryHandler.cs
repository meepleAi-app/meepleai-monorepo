using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetSessionByCodeQueryHandler : IRequestHandler<GetSessionByCodeQuery, SessionDto?>
{
    private readonly MeepleAiDbContext _context;

    public GetSessionByCodeQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDto?> Handle(GetSessionByCodeQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.SessionTrackingSessions
            .Include(s => s.Participants)
            .Where(s => s.SessionCode == request.SessionCode &&
                       s.Status != "Finalized" &&
                       !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (session == null)
            return null;

        // #4939: the same rule as GET /game-sessions/{id} (#4883), answered as «not found» rather than 403: a
        // stranger holding a guessed code must not learn from this route that the code is live. Fail-closed on
        // an empty caller (SessionAccessPolicy never matches it).
        if (!SessionAccessPolicy.IsAccessible(
                session.UserId, session.Participants.Select(p => p.UserId), request.RequestedBy))
        {
            return null;
        }

        // Get score entries
        var scoreEntries = await _context.SessionTrackingScoreEntries
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Map participants with scores
        var participants = session.Participants.Select(p => new ParticipantDto
        {
            Id = p.Id,
            DisplayName = p.DisplayName,
            UserId = p.UserId,
            IsOwner = p.IsOwner,
            JoinOrder = p.JoinOrder,
            FinalRank = p.FinalRank,
            TotalScore = scoreEntries
                .Where(e => e.ParticipantId == p.Id)
                .Sum(e => e.ScoreValue)
        }).ToList();

        // Map score entries
        var scores = scoreEntries.Select(e => new ScoreEntryDto
        {
            Id = e.Id,
            ParticipantId = e.ParticipantId,
            RoundNumber = e.RoundNumber,
            Category = e.Category,
            ScoreValue = e.ScoreValue,
            Timestamp = e.Timestamp
        }).ToList();

        return new SessionDto
        {
            Id = session.Id,
            UserId = session.UserId,
            SessionCode = session.SessionCode,
            GameId = session.GameId,
            SessionType = session.SessionType,
            SessionDate = session.SessionDate,
            Location = session.Location,
            Status = session.Status,
            FinalizedAt = session.FinalizedAt,
            Participants = participants,
            Scores = scores
        };
    }
}

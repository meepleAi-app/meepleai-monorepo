using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Middleware.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class GetScoreboardQueryHandler : IRequestHandler<GetScoreboardQuery, ScoreboardDto>
{
    private readonly MeepleAiDbContext _context;

    public GetScoreboardQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<ScoreboardDto> Handle(GetScoreboardQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.SessionTrackingSessions
            .Include(s => s.Participants)
            .Where(s => s.Id == request.SessionId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Get score entries
        var scoreEntries = await _context.SessionTrackingScoreEntries
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Calculate scores per participant
        var participantScores = session.Participants.Select(p =>
        {
            var total = scoreEntries
                .Where(e => e.ParticipantId == p.Id)
                .Sum(e => e.ScoreValue);

            return new ParticipantScoreDto
            {
                ParticipantId = p.Id,
                DisplayName = p.DisplayName,
                TotalScore = total,
                CurrentRank = 0
            };
        })
        .OrderByDescending(p => p.TotalScore)
        .ToList();

        // Assign ranks
        for (int i = 0; i < participantScores.Count; i++)
        {
            participantScores[i] = participantScores[i] with { CurrentRank = i + 1 };
        }

        // Group scores by round
        var scoresByRound = scoreEntries
            .Where(e => e.RoundNumber.HasValue)
            .GroupBy(e => e.RoundNumber!.Value)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(e => e.ParticipantId)
                     .ToDictionary(pg => pg.Key, pg => pg.Sum(e => e.ScoreValue))
            );

        // Group scores by category
        var scoresByCategory = scoreEntries
            .Where(e => !string.IsNullOrEmpty(e.Category))
            .GroupBy(e => e.Category!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(e => e.ParticipantId)
                     .ToDictionary(pg => pg.Key, pg => pg.Sum(e => e.ScoreValue)),
                StringComparer.Ordinal
            );

        var currentLeaderId = participantScores.FirstOrDefault()?.ParticipantId;

        return new ScoreboardDto
        {
            SessionId = session.Id,
            Participants = participantScores,
            ScoresByRound = scoresByRound,
            ScoresByCategory = scoresByCategory,
            CurrentLeaderId = currentLeaderId
        };
    }
}
using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Middleware.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class GetSessionDetailsQueryHandler : IRequestHandler<GetSessionDetailsQuery, SessionDetailsDto>
{
    private readonly MeepleAiDbContext _context;

    public GetSessionDetailsQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDetailsDto> Handle(GetSessionDetailsQuery request, CancellationToken cancellationToken)
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

        // Get player notes
        var playerNotes = await _context.SessionTrackingPlayerNotes
            .Where(n => n.SessionId == session.Id && 
                       n.NoteType == "Shared" && 
                       !n.IsHidden)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Map participants with total scores
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
        var scores = scoreEntries
            .OrderBy(e => e.Timestamp)
            .Select(e => new ScoreEntryDto
            {
                Id = e.Id,
                ParticipantId = e.ParticipantId,
                ScoreValue = e.ScoreValue,
                RoundNumber = e.RoundNumber,
                Category = e.Category,
                Timestamp = e.Timestamp
            }).ToList();

        // Map notes
        var notes = playerNotes.Select(n => new PlayerNoteDto
        {
            Id = n.Id,
            ParticipantId = n.ParticipantId,
            NoteType = n.NoteType,
            Content = n.Content,
            TemplateKey = n.TemplateKey,
            IsHidden = n.IsHidden,
            CreatedAt = n.CreatedAt
        }).ToList();

        return new SessionDetailsDto
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
            Scores = scores,
            Notes = notes
        };
    }
}
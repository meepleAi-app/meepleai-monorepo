using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetSessionDetailsQueryHandler : IRequestHandler<GetSessionDetailsQuery, SessionDetailsDto>
{
    private readonly MeepleAiDbContext _context;

    public GetSessionDetailsQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDetailsDto> Handle(GetSessionDetailsQuery request, CancellationToken cancellationToken)
    {
        var session = await SessionDetailsReader.LoadSessionAsync(_context, request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        // #4883: the details carry the session code, so the read is owner/participant-only. The rule is
        // the domain's (Session.IsAccessibleBy, #3119), evaluated on the persisted row this handler loaded.
        if (!SessionAccessPolicy.IsAccessible(
                session.UserId, session.Participants.Select(p => p.UserId), request.RequestedBy))
        {
            throw new ForbiddenException("You do not have access to this session.");
        }

        return await SessionDetailsReader.BuildAsync(_context, session, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handler for <see cref="GetSessionDetailsForSystemQuery"/>: the same details, no caller check.
/// </summary>
public class GetSessionDetailsForSystemQueryHandler : IRequestHandler<GetSessionDetailsForSystemQuery, SessionDetailsDto>
{
    private readonly MeepleAiDbContext _context;

    public GetSessionDetailsForSystemQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDetailsDto> Handle(GetSessionDetailsForSystemQuery request, CancellationToken cancellationToken)
    {
        var session = await SessionDetailsReader.LoadSessionAsync(_context, request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        return await SessionDetailsReader.BuildAsync(_context, session, cancellationToken).ConfigureAwait(false);
    }
}

internal static class SessionDetailsReader
{
    public static async Task<SessionEntity> LoadSessionAsync(
        MeepleAiDbContext context, Guid sessionId, CancellationToken cancellationToken)
    {
        return await context.SessionTrackingSessions
            .Include(s => s.Participants)
            .Where(s => s.Id == sessionId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {sessionId} not found");
    }

    public static async Task<SessionDetailsDto> BuildAsync(
        MeepleAiDbContext context, SessionEntity session, CancellationToken cancellationToken)
    {
        // Get score entries
        var scoreEntries = await context.SessionTrackingScoreEntries
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Get player notes
        var playerNotes = await context.SessionTrackingPlayerNotes
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

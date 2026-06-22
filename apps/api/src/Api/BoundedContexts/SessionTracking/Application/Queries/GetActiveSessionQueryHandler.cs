using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetActiveSessionQueryHandler : IRequestHandler<GetActiveSessionQuery, SessionDto?>
{
    private readonly MeepleAiDbContext _context;

    public GetActiveSessionQueryHandler(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SessionDto?> Handle(GetActiveSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.SessionTrackingSessions
            .Include(s => s.Participants)
            .Where(s => s.CreatedBy == request.UserId &&
                       (s.Status == "Active" || s.Status == "Paused") &&
                       !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (session == null)
            return null;

        // Get score entries for this session
        var scoreEntries = await _context.SessionTrackingScoreEntries
            .Where(e => e.SessionId == session.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Map participants with their scores
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

        // Path B (#2483): derive TurnOrderType from the game's published toolkit.
        // Static for the session lifetime — no new Session field, no migration, no SignalR event.
        var turnOrderType = await ResolveTurnOrderTypeAsync(session.GameId, cancellationToken)
            .ConfigureAwait(false);

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
            ScoringType = session.ScoringType,
            ScoreData = session.ScoreData,
            TurnOrderType = turnOrderType,
            Participants = participants,
            Scores = scores
        };
    }

    /// <summary>
    /// Looks up the TurnOrderType from the game's first published GameToolkit TurnTemplate.
    /// Returns null when no game is linked, no published toolkit exists, or the toolkit
    /// has no TurnTemplate configured.
    /// </summary>
    private async Task<string?> ResolveTurnOrderTypeAsync(Guid? gameId, CancellationToken cancellationToken)
    {
        if (gameId == null)
            return null;

        var turnTemplateJson = await _context.GameToolkits
            .Where(t => t.GameId == gameId && t.IsPublished && t.TurnTemplateJson != null)
            .Select(t => t.TurnTemplateJson)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(turnTemplateJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(turnTemplateJson);
            if (doc.RootElement.TryGetProperty("turnOrderType", out var prop) &&
                prop.TryGetInt32(out var value) &&
                Enum.IsDefined(typeof(TurnOrderType), value))
            {
                return ((TurnOrderType)value).ToString();
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — treat as no turn template rather than throwing.
        }

        return null;
    }
}

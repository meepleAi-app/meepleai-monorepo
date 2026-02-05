// Handlers for session export queries (Issue #3347).

using System.Globalization;
using System.Text;

using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

using MediatR;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

// ============================================================================
// Export Session PDF Handler
// ============================================================================

public sealed class ExportSessionPdfQueryHandler : IRequestHandler<ExportSessionPdfQuery, ExportSessionPdfResponse>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly ISessionDeckRepository _deckRepository;
    private readonly ILogger<ExportSessionPdfQueryHandler> _logger;

    public ExportSessionPdfQueryHandler(
        ISessionRepository sessionRepository,
        IScoreEntryRepository scoreEntryRepository,
        IDiceRollRepository diceRollRepository,
        ISessionDeckRepository deckRepository,
        ILogger<ExportSessionPdfQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _scoreEntryRepository = scoreEntryRepository;
        _diceRollRepository = diceRollRepository;
        _deckRepository = deckRepository;
        _logger = logger;
    }

    public async Task<ExportSessionPdfResponse> Handle(ExportSessionPdfQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            throw new NotFoundException($"Session {request.SessionId} not found.");
        }

        // Get related data
        var scores = await _scoreEntryRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var diceRolls = request.IncludeDiceHistory
            ? await _diceRollRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            : [];
        var decks = request.IncludeCardHistory
            ? await _deckRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            : [];

        // Calculate participant totals
        var participantScores = CalculateParticipantScores(session.Participants, scores);

        // Generate PDF content (simplified HTML-to-PDF style content)
        var pdfBytes = GenerateSessionPdf(session, participantScores, diceRolls, decks, request.IncludeScoreChart);

        var fileName = $"session_{session.SessionCode}_{session.SessionDate:yyyyMMdd}.pdf";

        _logger.LogInformation(
            "Exported session {SessionId} as PDF for user {UserId}",
            request.SessionId,
            request.RequestedBy);

        return new ExportSessionPdfResponse(
            PdfContent: pdfBytes,
            FileName: fileName,
            ContentType: "application/pdf");
    }

    private static Dictionary<Guid, decimal> CalculateParticipantScores(
        IEnumerable<Participant> participants,
        IEnumerable<ScoreEntry> scores)
    {
        return participants.ToDictionary(
            p => p.Id,
            p => scores.Where(s => s.ParticipantId == p.Id).Sum(s => s.ScoreValue));
    }

    private static byte[] GenerateSessionPdf(
        Session session,
        Dictionary<Guid, decimal> participantScores,
        IEnumerable<DiceRoll> diceRolls,
        IEnumerable<SessionDeck> decks,
        bool includeChart)
    {
        // Generate a simple text-based PDF content
        // In production, use a PDF library like QuestPDF, iTextSharp, or PdfSharpCore
        var content = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;

        content.AppendLine(culture, $"╔════════════════════════════════════════════════════════════╗");
        content.AppendLine(culture, $"║              MEEPLEAI SESSION REPORT                       ║");
        content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");
        content.AppendLine(culture, $"║ Session Code: {session.SessionCode,-44} ║");
        content.AppendLine(culture, $"║ Date: {session.SessionDate:yyyy-MM-dd HH:mm,-47} ║");
        if (!string.IsNullOrEmpty(session.Location))
        {
            content.AppendLine(culture, $"║ Location: {session.Location,-48} ║");
        }
        content.AppendLine(culture, $"║ Status: {session.Status,-50} ║");
        if (session.FinalizedAt.HasValue)
        {
            var duration = session.FinalizedAt.Value - session.SessionDate;
            content.AppendLine(culture, $"║ Duration: {FormatDuration(duration),-48} ║");
        }
        content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");
        content.AppendLine(culture, $"║                    FINAL STANDINGS                         ║");
        content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");

        // Sort participants by score descending
        var rankedParticipants = session.Participants
            .Select(p => new
            {
                Participant = p,
                Score = participantScores.GetValueOrDefault(p.Id, 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Participant.FinalRank ?? int.MaxValue)
            .ToList();

        for (var i = 0; i < rankedParticipants.Count; i++)
        {
            var entry = rankedParticipants[i];
            var medal = GetMedal(i + 1);
            var name = entry.Participant.DisplayName.Length > 30
                ? entry.Participant.DisplayName[..30] + "..."
                : entry.Participant.DisplayName;
            content.AppendLine(culture, $"║ {i + 1,2}. {medal} {name,-35} {entry.Score,10:N0} pts ║");
        }

        content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");

        // Dice rolls summary
        var diceRollList = diceRolls.ToList();
        if (diceRollList.Count > 0)
        {
            content.AppendLine(culture, $"║                    DICE ROLL SUMMARY                       ║");
            content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");
            content.AppendLine(culture, $"║ Total Rolls: {diceRollList.Count,-46} ║");

            var diceStats = diceRollList
                .SelectMany(r => r.GetRolls())
                .GroupBy(r => r)
                .OrderBy(g => g.Key);

            foreach (var stat in diceStats.Take(6))
            {
                content.AppendLine(culture, $"║   Face {stat.Key}: {stat.Count()} times ║");
            }
            content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");
        }

        // Card draws summary
        var deckList = decks.ToList();
        if (deckList.Count > 0)
        {
            var totalDraws = deckList.Sum(d => d.DiscardPile.Count);
            content.AppendLine(culture, $"║                    CARD GAME SUMMARY                       ║");
            content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");
            content.AppendLine(culture, $"║ Total Decks: {deckList.Count,-46} ║");
            content.AppendLine(culture, $"║ Total Draws: {totalDraws,-46} ║");
            content.AppendLine(culture, $"╠════════════════════════════════════════════════════════════╣");
        }

        content.AppendLine(culture, $"║                                                            ║");
        content.AppendLine(culture, $"║              Generated by MeepleAI                         ║");
        content.AppendLine(culture, $"║              {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC                   ║");
        content.AppendLine(culture, $"╚════════════════════════════════════════════════════════════╝");

        // Convert to bytes (in production, use a proper PDF library)
        return Encoding.UTF8.GetBytes(content.ToString());
    }

    private static string GetMedal(int rank) => rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => "  "
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }
        return $"{duration.Minutes}m";
    }
}

// ============================================================================
// Get Shareable Session Handler
// ============================================================================

public sealed class GetShareableSessionQueryHandler : IRequestHandler<GetShareableSessionQuery, ShareableSessionResponse>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly ISessionDeckRepository _deckRepository;
    private readonly ILogger<GetShareableSessionQueryHandler> _logger;

    public GetShareableSessionQueryHandler(
        ISessionRepository sessionRepository,
        IScoreEntryRepository scoreEntryRepository,
        IDiceRollRepository diceRollRepository,
        ISessionDeckRepository deckRepository,
        ILogger<GetShareableSessionQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _scoreEntryRepository = scoreEntryRepository;
        _diceRollRepository = diceRollRepository;
        _deckRepository = deckRepository;
        _logger = logger;
    }

    public async Task<ShareableSessionResponse> Handle(GetShareableSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            throw new NotFoundException($"Session {request.SessionId} not found.");
        }

        // Get statistics
        var scores = await _scoreEntryRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var diceRolls = await _diceRollRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var decks = await _deckRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var diceRollCount = diceRolls.Count();
        var cardDrawCount = decks.Sum(d => d.DiscardPile.Count);

        // Calculate participant scores and determine winner
        var participantScores = session.Participants
            .Select(p => new
            {
                Participant = p,
                TotalScore = scores.Where(s => s.ParticipantId == p.Id).Sum(s => s.ScoreValue)
            })
            .OrderByDescending(x => x.TotalScore)
            .ThenBy(x => x.Participant.FinalRank ?? int.MaxValue)
            .ToList();

        var winnerId = participantScores.FirstOrDefault()?.Participant.Id;

        var participants = participantScores.Select((ps, index) => new ShareableParticipantDto(
            DisplayName: ps.Participant.DisplayName,
            FinalRank: ps.Participant.FinalRank ?? (index + 1),
            TotalScore: ps.TotalScore,
            IsWinner: ps.Participant.Id == winnerId && ps.TotalScore > 0
        )).ToList();

        var stats = new ShareableStatsDto(
            TotalParticipants: session.Participants.Count,
            TotalScoreEntries: scores.Count(),
            TotalDiceRolls: diceRollCount,
            TotalCardDraws: cardDrawCount
        );

        // Calculate duration if finalized
        TimeSpan? duration = session.FinalizedAt.HasValue
            ? session.FinalizedAt.Value - session.SessionDate
            : null;

        _logger.LogInformation("Retrieved shareable session {SessionId}", request.SessionId);

        return new ShareableSessionResponse(
            SessionId: session.Id,
            SessionCode: session.SessionCode,
            GameName: null, // Would need to join with Game table
            GameImageUrl: null,
            SessionDate: session.SessionDate,
            Duration: duration,
            Location: session.Location,
            Status: session.Status.ToString(),
            Participants: participants,
            Stats: stats,
            FinalizedAt: session.FinalizedAt
        );
    }
}

// ============================================================================
// Generate Session Share Link Handler
// ============================================================================

public sealed class GenerateSessionShareLinkQueryHandler : IRequestHandler<GenerateSessionShareLinkQuery, SessionShareLinkResponse>
{
    // Default base URL used when App:BaseUrl is not configured (production URL)
#pragma warning disable S1075 // URIs should not be hardcoded - this is a fallback default
    private const string DefaultBaseUrl = "https://meepleai.com";
#pragma warning restore S1075

    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GenerateSessionShareLinkQueryHandler> _logger;

    public GenerateSessionShareLinkQueryHandler(
        ISessionRepository sessionRepository,
        IScoreEntryRepository scoreEntryRepository,
        IConfiguration configuration,
        ILogger<GenerateSessionShareLinkQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _scoreEntryRepository = scoreEntryRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SessionShareLinkResponse> Handle(GenerateSessionShareLinkQuery request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
        {
            throw new NotFoundException($"Session {request.SessionId} not found.");
        }

        // Verify user has access to the session
        if (session.UserId != request.RequestedBy &&
            !session.Participants.Any(p => p.UserId == request.RequestedBy))
        {
            throw new ForbiddenException("You do not have access to this session.");
        }

        var baseUrl = _configuration["App:BaseUrl"] ?? DefaultBaseUrl;
        var shareUrl = $"{baseUrl}/sessions/share/{session.Id}";

        // Get winner for description
        var scores = await _scoreEntryRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var winnerParticipant = session.Participants
            .Select(p => new
            {
                Participant = p,
                Score = scores.Where(s => s.ParticipantId == p.Id).Sum(s => s.ScoreValue)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        var title = $"MeepleAI Session - {session.SessionCode}";
        var description = session.Participants.Count == 1
            ? $"Solo session on {session.SessionDate:MMMM d, yyyy}"
            : winnerParticipant?.Score > 0
                ? $"{winnerParticipant.Participant.DisplayName} won with {winnerParticipant.Score:N0} points!"
                : $"Game session with {session.Participants.Count} players on {session.SessionDate:MMMM d, yyyy}";

        var ogMetadata = new OpenGraphMetadata(
            Title: title,
            Description: description,
            ImageUrl: $"{baseUrl}/api/v1/sessions/{session.Id}/og-image",
            Url: shareUrl,
            Type: "website"
        );

        _logger.LogInformation(
            "Generated share link for session {SessionId} by user {UserId}",
            request.SessionId,
            request.RequestedBy);

        return new SessionShareLinkResponse(
            ShareUrl: shareUrl,
            OgMetadata: ogMetadata
        );
    }
}

using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Returns snapshot + photos + scores + recap for resume experience.
/// Issue #122 — Enhanced Save/Resume.
/// </summary>
internal sealed class GetSessionResumeContextQueryHandler
    : IQueryHandler<GetSessionResumeContextQuery, SessionResumeContextDto>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly ISessionSnapshotRepository _snapshotRepository;
    private readonly ISessionAttachmentRepository _attachmentRepository;

    public GetSessionResumeContextQueryHandler(
        ILiveSessionRepository sessionRepository,
        ISessionSnapshotRepository snapshotRepository,
        ISessionAttachmentRepository attachmentRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
    }

    public async Task<SessionResumeContextDto> Handle(
        GetSessionResumeContextQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. Get session
        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        // 2. Game title from session
        var gameTitle = session.GameName;

        // 3. Get latest snapshot
        var latestSnapshot = await _snapshotRepository.GetLatestBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        var lastSnapshotIndex = latestSnapshot?.SnapshotIndex ?? 0;

        // 4. Build player score summaries from session players (already ranked)
        var playerScores = session.Players
            .Where(p => p.IsActive)
            .OrderBy(p => p.CurrentRank == 0 ? int.MaxValue : p.CurrentRank)
            .Select(p => new PlayerScoreSummary(
                p.Id,
                p.DisplayName,
                p.TotalScore,
                p.CurrentRank))
            .ToList();

        // 5. Get session attachments/photos
        var attachments = await _attachmentRepository.GetBySessionIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false);

        var photos = attachments
            .Where(a => !a.IsDeleted)
            .Select(a => new SessionPhotoSummary(
                a.Id,
                a.ThumbnailUrl,
                a.Caption,
                a.AttachmentType.ToString()))
            .ToList();

        // 6. Build recap text
        var activePlayers = session.Players.Where(p => p.IsActive).ToList();
        var playerNames = string.Join(", ", activePlayers.Select(p => p.DisplayName));

        var topPlayer = activePlayers
            .OrderByDescending(p => p.TotalScore)
            .FirstOrDefault();

        var snapshotInfo = latestSnapshot != null
            ? $"Ultimo snapshot #{latestSnapshot.SnapshotIndex} ({latestSnapshot.TriggerDescription ?? latestSnapshot.TriggerType.ToString()}) al turno {latestSnapshot.TurnIndex}."
            : "Nessuno snapshot disponibile.";

        var recap = topPlayer != null && topPlayer.TotalScore > 0
            ? $"Partita in pausa al turno {session.CurrentTurnIndex}. Giocatori: {playerNames}. In testa: {topPlayer.DisplayName} con {topPlayer.TotalScore} punti. {snapshotInfo}"
            : $"Partita in pausa al turno {session.CurrentTurnIndex}. Giocatori: {playerNames}. {snapshotInfo}";

        // 7. Current phase name
        var currentPhase = session.PhaseNames.Length > session.CurrentPhaseIndex
            ? session.PhaseNames[session.CurrentPhaseIndex]
            : null;

        // 8. Return DTO
        return new SessionResumeContextDto
        {
            SessionId = session.Id,
            GameTitle = gameTitle,
            LastSnapshotIndex = lastSnapshotIndex,
            CurrentTurn = session.CurrentTurnIndex,
            CurrentPhase = currentPhase,
            PausedAt = session.PausedAt ?? session.UpdatedAt,
            Recap = recap,
            PlayerScores = playerScores,
            Photos = photos
        };
    }
}

using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Exceptions;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Orchestrates: pause session + save + create snapshot + persist agent state + generate recap.
/// Issue #122 — Enhanced Save/Resume.
/// </summary>
internal sealed class SaveCompleteSessionStateCommandHandler
    : ICommandHandler<SaveCompleteSessionStateCommand, SessionSaveResultDto>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly ILogger<SaveCompleteSessionStateCommandHandler> _logger;

    public SaveCompleteSessionStateCommandHandler(
        ILiveSessionRepository sessionRepository,
        IMediator mediator,
        ITierEnforcementService tierEnforcementService,
        ILogger<SaveCompleteSessionStateCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SessionSaveResultDto> Handle(
        SaveCompleteSessionStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Get session from repository
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // E2-3: Tier enforcement — check if session save is enabled for this user's tier
        var canSave = await _tierEnforcementService
            .CanPerformAsync(session.CreatedByUserId, TierAction.SaveSession, cancellationToken)
            .ConfigureAwait(false);

        if (!canSave)
        {
            throw new TierLimitExceededException(
                nameof(TierAction.SaveSession),
                "Session save is not available on your current plan. Upgrade to Premium to save sessions.");
        }

        // 2. If status is InProgress, pause first
        if (session.Status == LiveSessionStatus.InProgress)
        {
            await _mediator.Send(new PauseLiveSessionCommand(command.SessionId), cancellationToken)
                .ConfigureAwait(false);
        }

        // 3. Save session state
        await _mediator.Send(new SaveLiveSessionCommand(command.SessionId), cancellationToken)
            .ConfigureAwait(false);

        // 4. Create snapshot with ManualSave trigger
        var snapshotDto = await _mediator.Send(
            new CreateSnapshotCommand(command.SessionId, SnapshotTrigger.ManualSave, "Complete session state save", null),
            cancellationToken).ConfigureAwait(false);

        // 5. Persist agent state if ChatSessionId has value (Issue #122 — agent state persistence placeholder)
        if (session.ChatSessionId.HasValue)
        {
            _logger.LogDebug(
                "Agent state persistence not yet wired for session {SessionId}, chat session {ChatSessionId}",
                command.SessionId, session.ChatSessionId.Value);
        }

        // 6. Photo count from snapshot DTO
        var photoCount = snapshotDto.AttachmentCount;

        // 7. Generate recap text
        var activePlayers = session.Players.Where(p => p.IsActive).ToList();
        var playerNames = string.Join(", ", activePlayers.Select(p => p.DisplayName));

        var topPlayer = activePlayers
            .OrderByDescending(p => p.TotalScore)
            .FirstOrDefault();

        var recap = topPlayer != null
            ? $"Partita salvata al turno {session.CurrentTurnIndex}. Giocatori: {playerNames}. In testa: {topPlayer.DisplayName} con {topPlayer.TotalScore} punti. Snapshot #{snapshotDto.SnapshotIndex} creato."
            : $"Partita salvata al turno {session.CurrentTurnIndex}. Giocatori: {playerNames}. Snapshot #{snapshotDto.SnapshotIndex} creato.";

        // 8. Return result
        return new SessionSaveResultDto
        {
            SessionId = command.SessionId,
            SnapshotIndex = snapshotDto.SnapshotIndex,
            Recap = recap,
            PhotoCount = photoCount,
            SavedAt = snapshotDto.Timestamp
        };
    }
}

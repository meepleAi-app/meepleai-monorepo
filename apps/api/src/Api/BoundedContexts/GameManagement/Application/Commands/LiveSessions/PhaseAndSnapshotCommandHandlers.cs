using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles advancing the turn phase in a live session.
/// Optionally triggers an auto-snapshot on phase change.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal class AdvanceLiveSessionPhaseCommandHandler : ICommandHandler<AdvanceLiveSessionPhaseCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IMediator _mediator;

    public AdvanceLiveSessionPhaseCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IMediator mediator)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task Handle(AdvanceLiveSessionPhaseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.AdvancePhase(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Auto-trigger snapshot on phase change if configured
        var triggerConfig = session.SnapshotTriggerConfig;
        if (triggerConfig != null && triggerConfig.IsTriggerEnabled(
                Domain.Entities.SessionSnapshot.SnapshotTrigger.PhaseAdvanced))
        {
            await _mediator.Send(new TriggerEventSnapshotCommand(
                command.SessionId,
                Domain.Entities.SessionSnapshot.SnapshotTrigger.PhaseAdvanced,
                $"Phase advanced to {session.GetCurrentPhaseName() ?? $"phase {session.CurrentPhaseIndex}"}",
                null), cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Handles configuring turn phases on a live session.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal class ConfigureLiveSessionPhasesCommandHandler : ICommandHandler<ConfigureLiveSessionPhasesCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public ConfigureLiveSessionPhasesCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(ConfigureLiveSessionPhasesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.ConfigurePhases(command.PhaseNames, _timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles triggering an event-based snapshot with debounce protection.
/// Returns null if debounced (skipped), or the snapshot DTO if created.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal class TriggerEventSnapshotCommandHandler
    : ICommandHandler<TriggerEventSnapshotCommand, SessionSnapshotDto?>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly TimeProvider _timeProvider;

    public TriggerEventSnapshotCommandHandler(
        ILiveSessionRepository sessionRepository,
        IMediator mediator,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<SessionSnapshotDto?> Handle(
        TriggerEventSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // Check if trigger is enabled
        var config = session.SnapshotTriggerConfig;
        if (config != null && !config.IsTriggerEnabled(command.TriggerType))
            return null;

        // Debounce check
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (config != null && !config.ShouldCreateSnapshot(session.LastSnapshotTimestamp, now))
            return null;

        // Create the snapshot via existing handler
        var result = await _mediator.Send(new CreateSnapshotCommand(
            command.SessionId,
            command.TriggerType,
            command.Description,
            command.CreatedByPlayerId), cancellationToken).ConfigureAwait(false);

        // Record the snapshot timestamp for debounce
        session.RecordSnapshotTimestamp(now);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return result;
    }
}

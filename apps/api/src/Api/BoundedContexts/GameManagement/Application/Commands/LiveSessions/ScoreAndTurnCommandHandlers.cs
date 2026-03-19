using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles recording a new score in a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class RecordLiveSessionScoreCommandHandler : ICommandHandler<RecordLiveSessionScoreCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public RecordLiveSessionScoreCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(RecordLiveSessionScoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.RecordScore(command.PlayerId, command.Round, command.Dimension, command.Value, _timeProvider, command.Unit);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles editing an existing score in a live session (upsert behavior).
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class EditLiveSessionScoreCommandHandler : ICommandHandler<EditLiveSessionScoreCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public EditLiveSessionScoreCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(EditLiveSessionScoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.RecordScore(command.PlayerId, command.Round, command.Dimension, command.Value, _timeProvider, command.Unit);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles advancing the turn in a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class AdvanceLiveSessionTurnCommandHandler : ICommandHandler<AdvanceLiveSessionTurnCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public AdvanceLiveSessionTurnCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(AdvanceLiveSessionTurnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.AdvanceTurn(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles updating notes on a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class UpdateLiveSessionNotesCommandHandler : ICommandHandler<UpdateLiveSessionNotesCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public UpdateLiveSessionNotesCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(UpdateLiveSessionNotesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.UpdateNotes(command.Notes, _timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

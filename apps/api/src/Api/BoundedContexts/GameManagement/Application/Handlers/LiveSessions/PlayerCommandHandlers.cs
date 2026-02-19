using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Handles adding a player to a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class AddPlayerToLiveSessionCommandHandler : ICommandHandler<AddPlayerToLiveSessionCommand, Guid>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public AddPlayerToLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Guid> Handle(AddPlayerToLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        var player = session.AddPlayer(
            command.UserId,
            command.DisplayName,
            command.Color,
            _timeProvider,
            command.Role,
            command.AvatarUrl);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return player.Id;
    }
}

/// <summary>
/// Handles removing a player from a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class RemovePlayerFromLiveSessionCommandHandler : ICommandHandler<RemovePlayerFromLiveSessionCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public RemovePlayerFromLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(RemovePlayerFromLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.RemovePlayer(command.PlayerId, _timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles updating player turn order in a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class UpdatePlayerOrderCommandHandler : ICommandHandler<UpdatePlayerOrderCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public UpdatePlayerOrderCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(UpdatePlayerOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.SetTurnOrder(command.PlayerIds, _timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
    }
}

using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles live session creation.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class CreateLiveSessionCommandHandler : ICommandHandler<CreateLiveSessionCommand, Guid>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;

    public CreateLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Guid> Handle(CreateLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        SessionScoringConfig scoringConfig;
        if (command.ScoringDimensions != null && command.ScoringDimensions.Count > 0)
        {
            scoringConfig = new SessionScoringConfig(
                command.ScoringDimensions,
                command.DimensionUnits);
        }
        else
        {
            scoringConfig = SessionScoringConfig.CreateDefault();
        }

        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            command.UserId,
            command.GameName,
            _timeProvider,
            command.GameId,
            command.Visibility,
            command.GroupId,
            scoringConfig,
            command.AgentMode);

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

        return session.Id;
    }
}

using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles live session creation.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class CreateLiveSessionCommandHandler : ICommandHandler<CreateLiveSessionCommand, Guid>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanionSessionService _companionSessionService;

    public CreateLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork,
        ICompanionSessionService companionSessionService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _companionSessionService = companionSessionService ?? throw new ArgumentNullException(nameof(companionSessionService));
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

        // ADR-083 SP0: create the SessionTracking.Session companion at-creation (Saga).
        // Add-only (no SaveChanges here): the single SaveChangesAsync below commits the
        // companion and the LiveGameSession atomically in one EF transaction — a companion
        // failure rolls back the LiveGameSession, so no orphan is ever persisted.
        Guid? trackingSessionId = null;
        if (command.GameId.HasValue)
        {
            trackingSessionId = await _companionSessionService
                .CreateCompanionAsync(command.UserId, command.GameId.Value, cancellationToken)
                .ConfigureAwait(false);
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
            command.AgentMode,
            trackingSessionId: trackingSessionId);

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return session.Id;
    }
}

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers.Shared;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles SessionTracking.ScoreUpdatedEvent to sync AgentSession state.
/// Issue #3189 (AGT-015): GST Integration - Agent State Sync with Game Events.
/// </summary>
internal sealed class ScoreUpdatedEventHandler : INotificationHandler<ScoreUpdatedEvent>
{
    private readonly IAgentSessionRepository _repository;
    private readonly IMediator _mediator;
    private readonly ILogger<ScoreUpdatedEventHandler> _logger;

    public ScoreUpdatedEventHandler(
        IAgentSessionRepository repository,
        IMediator mediator,
        ILogger<ScoreUpdatedEventHandler> logger)
    {
        _repository = repository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(ScoreUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing ScoreUpdatedEvent for Session {SessionId}, Participant {ParticipantId}, NewScore {NewScore}",
            notification.SessionId,
            notification.ParticipantId,
            notification.NewScore);

        // Find all active AgentSessions for this game session
        var activeSessions = await _repository.GetActiveByGameSessionAsync(
            notification.SessionId,
            cancellationToken).ConfigureAwait(false);

        if (activeSessions.Count == 0)
        {
            _logger.LogDebug(
                "No active AgentSessions found for Session {SessionId}. Skipping state sync.",
                notification.SessionId);
            return;
        }

        _logger.LogInformation(
            "Found {Count} active AgentSession(s) for Session {SessionId}. Updating state...",
            activeSessions.Count,
            notification.SessionId);

        // Update each active session
        foreach (var session in activeSessions)
        {
            try
            {
                // Map score update to GameState
                var updatedState = AgentStateMapper.UpdateScores(session.CurrentGameState, notification);

                // Send command to update session state
                await _mediator.Send(
                    new UpdateAgentSessionStateCommand(
                        session.Id,
                        updatedState.ToJson()),
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Updated AgentSession {AgentSessionId} with new score for Participant {ParticipantId}",
                    session.Id,
                    notification.ParticipantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update AgentSession {AgentSessionId} for ScoreUpdatedEvent. Session {SessionId}",
                    session.Id,
                    notification.SessionId);

                // Continue processing other sessions (no rethrow - partial failure tolerance)
            }
        }
    }
}

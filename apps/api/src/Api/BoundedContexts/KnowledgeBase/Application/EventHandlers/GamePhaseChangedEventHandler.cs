using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers.Shared;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles GameManagement.GamePhaseChangedEvent to update AgentSession phase state.
/// Issue #3189 (AGT-015): GST Integration - Agent State Sync with Game Events.
/// </summary>
internal sealed class GamePhaseChangedEventHandler : DomainEventHandlerBase<GamePhaseChangedEvent>
{
    private readonly IAgentSessionRepository _repository;
    private readonly IMediator _mediator;

    public GamePhaseChangedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GamePhaseChangedEventHandler> logger,
        IAgentSessionRepository repository,
        IMediator mediator)
        : base(dbContext, logger)
    {
        _repository = repository;
        _mediator = mediator;
    }

    protected override async Task HandleEventAsync(GamePhaseChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Processing GamePhaseChangedEvent for Session {SessionId}, Phase {OldPhase} → {NewPhase}",
            domainEvent.SessionId,
            domainEvent.OldPhase,
            domainEvent.NewPhase);

        // Find all active AgentSessions for this game session
        var activeSessions = await _repository.GetActiveByGameSessionAsync(
            domainEvent.SessionId,
            cancellationToken).ConfigureAwait(false);

        if (activeSessions.Count == 0)
        {
            Logger.LogDebug(
                "No active AgentSessions found for Session {SessionId}. Skipping state sync.",
                domainEvent.SessionId);
            return;
        }

        Logger.LogInformation(
            "Found {Count} active AgentSession(s) for Session {SessionId}. Updating phase state...",
            activeSessions.Count,
            domainEvent.SessionId);

        // Update each active session
        foreach (var session in activeSessions)
        {
            try
            {
                // Map phase change to GameState
                var updatedState = AgentStateMapper.UpdatePhase(session.CurrentGameState, domainEvent);

                // Send command to update session state
                await _mediator.Send(
                    new UpdateAgentSessionStateCommand(
                        AgentSessionId: session.Id,
                        GameStateJson: updatedState.ToJson(),
                        // #4282: the system syncs the session on its owner's behalf, so it passes
                        // the session's own owner and game session to the ownership check.
                        GameSessionId: session.GameSessionId,
                        UserId: session.UserId),
                    cancellationToken).ConfigureAwait(false);

                Logger.LogInformation(
                    "Updated AgentSession {AgentSessionId} with phase change to {NewPhase}",
                    session.Id,
                    domainEvent.NewPhase);
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex,
                    "Failed to update AgentSession {AgentSessionId} for GamePhaseChangedEvent. Session {SessionId}",
                    session.Id,
                    domainEvent.SessionId);

                // Continue processing other sessions (no rethrow - partial failure tolerance)
            }
        }
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GamePhaseChangedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            { "Action", "AgentStateSync_GamePhaseChanged" },
            { "SessionId", domainEvent.SessionId },
            { "OldPhase", domainEvent.OldPhase.ToString() },
            { "NewPhase", domainEvent.NewPhase.ToString() }
        };
}

using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Infrastructure;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Handles SessionTracking.SessionFinalizedEvent to cascade cleanup AgentSessions.
/// Issue #3189 (AGT-015): GST Integration - Agent State Sync with Game Events.
/// </summary>
internal sealed class SessionFinalizedEventHandler : INotificationHandler<SessionFinalizedEvent>
{
    private readonly IAgentSessionRepository _repository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SessionFinalizedEventHandler> _logger;

    public SessionFinalizedEventHandler(
        IAgentSessionRepository repository,
        MeepleAiDbContext dbContext,
        ILogger<SessionFinalizedEventHandler> logger)
    {
        _repository = repository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(SessionFinalizedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing SessionFinalizedEvent for Session {SessionId}. Ending all active AgentSessions...",
            notification.SessionId);

        // Find all active AgentSessions for this game session
        var activeSessions = await _repository.GetActiveByGameSessionAsync(
            notification.SessionId,
            cancellationToken).ConfigureAwait(false);

        if (activeSessions.Count == 0)
        {
            _logger.LogDebug(
                "No active AgentSessions found for Session {SessionId}. No cleanup needed.",
                notification.SessionId);
            return;
        }

        _logger.LogInformation(
            "Found {Count} active AgentSession(s) for Session {SessionId}. Ending sessions (cascade cleanup)...",
            activeSessions.Count,
            notification.SessionId);

        var endedCount = 0;
        var failedCount = 0;

        // End each active session (cascade cleanup)
        foreach (var session in activeSessions)
        {
            try
            {
                session.End(); // Sets IsActive = false, EndedAt = DateTime.UtcNow
                await _repository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

                endedCount++;

                _logger.LogInformation(
                    "Ended AgentSession {AgentSessionId} (cascade from SessionFinalizedEvent)",
                    session.Id);
            }
            catch (Exception ex)
            {
                failedCount++;

                _logger.LogError(
                    ex,
                    "Failed to end AgentSession {AgentSessionId} for SessionFinalizedEvent. Session {SessionId}",
                    session.Id,
                    notification.SessionId);

                // Continue processing other sessions (no rethrow - partial failure tolerance)
            }
        }

        // Persist all changes in single transaction
        if (endedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Cascade cleanup complete for Session {SessionId}. Ended: {EndedCount}, Failed: {FailedCount}",
                notification.SessionId,
                endedCount,
                failedCount);
        }
    }
}

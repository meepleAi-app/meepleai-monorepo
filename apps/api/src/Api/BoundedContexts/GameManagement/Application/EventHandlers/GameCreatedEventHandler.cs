using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Handles the GameCreatedEvent domain event.
/// Creates an audit log entry automatically via the base class.
/// </summary>
internal sealed class GameCreatedEventHandler : DomainEventHandlerBase<GameCreatedEvent>
{
    public GameCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override Task HandleEventAsync(GameCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Audit logging is handled by the base class (see GetAuditMetadata). The integration-event
        // publish was removed with the n8n / WorkflowIntegration decommission — GameCreatedIntegrationEvent
        // had no remaining subscribers, so its publish was a dead no-op.
        return Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
            ["Name"] = domainEvent.Name,
            ["BggId"] = domainEvent.BggId,
            ["Action"] = "GameCreated"
        };
    }
}

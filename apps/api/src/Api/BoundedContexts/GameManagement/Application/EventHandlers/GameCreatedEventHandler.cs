using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Handles the GameCreatedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class GameCreatedEventHandler : DomainEventHandlerBase<GameCreatedEvent>
{
    public GameCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameCreatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., index game for search)
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["GameId"] = domainEvent.GameId,
            ["Name"] = domainEvent.Name,
            ["BggId"] = domainEvent.BggId,
            ["Action"] = "GameCreated"
        };
    }
}

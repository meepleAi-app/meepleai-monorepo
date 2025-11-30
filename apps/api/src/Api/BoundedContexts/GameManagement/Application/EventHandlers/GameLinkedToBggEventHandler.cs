using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Handles the GameLinkedToBggEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class GameLinkedToBggEventHandler : DomainEventHandlerBase<GameLinkedToBggEvent>
{
    public GameLinkedToBggEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameLinkedToBggEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameLinkedToBggEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., trigger BGG metadata sync)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameLinkedToBggEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["GameId"] = domainEvent.GameId,
            ["BggId"] = domainEvent.BggId,
            ["Action"] = "GameLinkedToBgg"
        };
    }
}

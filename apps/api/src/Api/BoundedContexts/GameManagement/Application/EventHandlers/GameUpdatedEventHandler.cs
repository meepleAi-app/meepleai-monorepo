using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Handles the GameUpdatedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
internal sealed class GameUpdatedEventHandler : DomainEventHandlerBase<GameUpdatedEvent>
{
    public GameUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameUpdatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., update search index)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["GameId"] = domainEvent.GameId,
            ["Name"] = domainEvent.Name,
            ["Action"] = "GameUpdated"
        };
    }
}

using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

public sealed class GameSessionCompletedEventHandler : DomainEventHandlerBase<GameSessionCompletedEvent>
{
    public GameSessionCompletedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameSessionCompletedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameSessionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., update game statistics)
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionCompletedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["SessionId"] = domainEvent.SessionId,
            ["GameId"] = domainEvent.GameId,
            ["CompletedAt"] = domainEvent.CompletedAt,
            ["Duration"] = domainEvent.Duration.ToString(),
            ["Action"] = "GameSessionCompleted"
        };
    }
}

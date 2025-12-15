using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

internal sealed class GameSessionAbandonedEventHandler : DomainEventHandlerBase<GameSessionAbandonedEvent>
{
    public GameSessionAbandonedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameSessionAbandonedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameSessionAbandonedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionAbandonedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["AbandonedAt"] = domainEvent.AbandonedAt,
            ["Reason"] = domainEvent.Reason,
            ["Action"] = "GameSessionAbandoned"
        };
    }
}

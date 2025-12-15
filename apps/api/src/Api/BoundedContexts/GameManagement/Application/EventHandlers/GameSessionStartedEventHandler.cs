using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

internal sealed class GameSessionStartedEventHandler : DomainEventHandlerBase<GameSessionStartedEvent>
{
    public GameSessionStartedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameSessionStartedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameSessionStartedEvent domainEvent, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionStartedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["GameId"] = domainEvent.GameId,
            ["StartedAt"] = domainEvent.StartedAt,
            ["Action"] = "GameSessionStarted"
        };
    }
}

using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

internal sealed class GameSessionCreatedEventHandler : DomainEventHandlerBase<GameSessionCreatedEvent>
{
    public GameSessionCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameSessionCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameSessionCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["GameId"] = domainEvent.GameId,
            ["PlayerCount"] = domainEvent.PlayerCount,
            ["Action"] = "GameSessionCreated"
        };
    }
}

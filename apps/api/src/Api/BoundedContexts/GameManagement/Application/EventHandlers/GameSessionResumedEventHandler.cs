using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

internal sealed class GameSessionResumedEventHandler : DomainEventHandlerBase<GameSessionResumedEvent>
{
    public GameSessionResumedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameSessionResumedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameSessionResumedEvent domainEvent, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionResumedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["ResumedAt"] = domainEvent.ResumedAt,
            ["Action"] = "GameSessionResumed"
        };
    }
}

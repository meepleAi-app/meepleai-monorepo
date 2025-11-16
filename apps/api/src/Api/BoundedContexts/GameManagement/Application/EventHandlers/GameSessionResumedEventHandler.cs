using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

public sealed class GameSessionResumedEventHandler : DomainEventHandlerBase<GameSessionResumedEvent>
{
    public GameSessionResumedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<GameSessionResumedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(GameSessionResumedEvent domainEvent, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionResumedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["SessionId"] = domainEvent.SessionId,
            ["ResumedAt"] = domainEvent.ResumedAt,
            ["Action"] = "GameSessionResumed"
        };
    }
}

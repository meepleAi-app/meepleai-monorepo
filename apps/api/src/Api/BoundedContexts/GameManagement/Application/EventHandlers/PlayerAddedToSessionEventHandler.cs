using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

public sealed class PlayerAddedToSessionEventHandler : DomainEventHandlerBase<PlayerAddedToSessionEvent>
{
    public PlayerAddedToSessionEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<PlayerAddedToSessionEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(PlayerAddedToSessionEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(PlayerAddedToSessionEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["SessionId"] = domainEvent.SessionId,
            ["PlayerName"] = domainEvent.PlayerName,
            ["PlayerNumber"] = domainEvent.PlayerNumber,
            ["Action"] = "PlayerAddedToSession"
        };
    }
}

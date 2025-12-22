using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

internal sealed class PlayerAddedToSessionEventHandler : DomainEventHandlerBase<PlayerAddedToSessionEvent>
{
    public PlayerAddedToSessionEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<PlayerAddedToSessionEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(PlayerAddedToSessionEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(PlayerAddedToSessionEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["PlayerName"] = domainEvent.PlayerName,
            ["PlayerNumber"] = domainEvent.PlayerNumber,
            ["Action"] = "PlayerAddedToSession"
        };
    }
}

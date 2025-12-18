using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the OAuthAccountUnlinkedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
internal sealed class OAuthAccountUnlinkedEventHandler : DomainEventHandlerBase<OAuthAccountUnlinkedEvent>
{
    public OAuthAccountUnlinkedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<OAuthAccountUnlinkedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(OAuthAccountUnlinkedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send OAuth unlinked security alert)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(OAuthAccountUnlinkedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(OAuthAccountUnlinkedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["UserId"] = domainEvent.UserId,
            ["Provider"] = domainEvent.Provider,
            ["Action"] = "OAuthAccountUnlinked"
        };
    }
}

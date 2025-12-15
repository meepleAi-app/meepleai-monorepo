using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the OAuthAccountLinkedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
internal sealed class OAuthAccountLinkedEventHandler : DomainEventHandlerBase<OAuthAccountLinkedEvent>
{
    public OAuthAccountLinkedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<OAuthAccountLinkedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(OAuthAccountLinkedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send OAuth linked notification)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(OAuthAccountLinkedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(OAuthAccountLinkedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["UserId"] = domainEvent.UserId,
            ["Provider"] = domainEvent.Provider,
            ["ProviderUserId"] = domainEvent.ProviderUserId,
            ["Action"] = "OAuthAccountLinked"
        };
    }
}

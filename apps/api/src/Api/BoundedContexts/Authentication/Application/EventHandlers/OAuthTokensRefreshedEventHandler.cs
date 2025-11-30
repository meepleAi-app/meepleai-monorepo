using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the OAuthTokensRefreshedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class OAuthTokensRefreshedEventHandler : DomainEventHandlerBase<OAuthTokensRefreshedEvent>
{
    public OAuthTokensRefreshedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<OAuthTokensRefreshedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(OAuthTokensRefreshedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Token refresh is a routine operation, no additional business logic needed
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(OAuthTokensRefreshedEvent domainEvent) => null; // OAuth account event, not user-specific

    protected override Dictionary<string, object?>? GetAuditMetadata(OAuthTokensRefreshedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["OAuthAccountId"] = domainEvent.OAuthAccountId,
            ["Provider"] = domainEvent.Provider,
            ["ExpiresAt"] = domainEvent.ExpiresAt,
            ["Action"] = "OAuthTokensRefreshed"
        };
    }
}

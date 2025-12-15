using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the ApiKeyRevokedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
internal sealed class ApiKeyRevokedEventHandler : DomainEventHandlerBase<ApiKeyRevokedEvent>
{
    public ApiKeyRevokedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<ApiKeyRevokedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(ApiKeyRevokedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send API key revoked notification)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(ApiKeyRevokedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(ApiKeyRevokedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ApiKeyId"] = domainEvent.ApiKeyId,
            ["UserId"] = domainEvent.UserId,
            ["Reason"] = domainEvent.Reason,
            ["Action"] = "ApiKeyRevoked"
        };
    }
}

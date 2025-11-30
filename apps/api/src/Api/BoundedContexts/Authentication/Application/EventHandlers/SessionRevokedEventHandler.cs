using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the SessionRevokedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class SessionRevokedEventHandler : DomainEventHandlerBase<SessionRevokedEvent>
{
    public SessionRevokedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<SessionRevokedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(SessionRevokedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send session revoked security alert)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(SessionRevokedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(SessionRevokedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["SessionId"] = domainEvent.SessionId,
            ["UserId"] = domainEvent.UserId,
            ["Reason"] = domainEvent.Reason,
            ["Action"] = "SessionRevoked"
        };
    }
}

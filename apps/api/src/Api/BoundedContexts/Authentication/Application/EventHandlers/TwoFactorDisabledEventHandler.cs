using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the TwoFactorDisabledEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class TwoFactorDisabledEventHandler : DomainEventHandlerBase<TwoFactorDisabledEvent>
{
    public TwoFactorDisabledEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<TwoFactorDisabledEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(TwoFactorDisabledEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send 2FA disabled security alert)
        await Task.CompletedTask;
    }

    protected override Guid? GetUserId(TwoFactorDisabledEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(TwoFactorDisabledEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["UserId"] = domainEvent.UserId,
            ["Action"] = "TwoFactorDisabled"
        };
    }
}

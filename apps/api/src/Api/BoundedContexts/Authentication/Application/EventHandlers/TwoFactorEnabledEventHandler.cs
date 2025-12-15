using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the TwoFactorEnabledEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
internal sealed class TwoFactorEnabledEventHandler : DomainEventHandlerBase<TwoFactorEnabledEvent>
{
    public TwoFactorEnabledEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<TwoFactorEnabledEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(TwoFactorEnabledEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send 2FA enabled confirmation email)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(TwoFactorEnabledEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(TwoFactorEnabledEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["UserId"] = domainEvent.UserId,
            ["BackupCodesCount"] = domainEvent.BackupCodesCount,
            ["Action"] = "TwoFactorEnabled"
        };
    }
}

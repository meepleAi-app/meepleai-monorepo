using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the RoleChangedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
internal sealed class RoleChangedEventHandler : DomainEventHandlerBase<RoleChangedEvent>
{
    public RoleChangedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<RoleChangedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(RoleChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., invalidate user sessions if demoted)
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(RoleChangedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(RoleChangedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["UserId"] = domainEvent.UserId,
            ["OldRole"] = domainEvent.OldRole,
            ["NewRole"] = domainEvent.NewRole,
            ["Action"] = "RoleChanged"
        };
    }
}

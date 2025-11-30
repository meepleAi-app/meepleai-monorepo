using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the PasswordChangedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class PasswordChangedEventHandler : DomainEventHandlerBase<PasswordChangedEvent>
{
    public PasswordChangedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<PasswordChangedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(PasswordChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add any additional business logic here if needed (e.g., send password change notification email)

        // For now, just logging via base class is sufficient
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Guid? GetUserId(PasswordChangedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(PasswordChangedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["UserId"] = domainEvent.UserId,
            ["Action"] = "PasswordChanged"
        };
    }
}

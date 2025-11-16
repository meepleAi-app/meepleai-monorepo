using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the EmailChangedEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class EmailChangedEventHandler : DomainEventHandlerBase<EmailChangedEvent>
{
    public EmailChangedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<EmailChangedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(EmailChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send email verification to new address)
        await Task.CompletedTask;
    }

    protected override Guid? GetUserId(EmailChangedEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(EmailChangedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["UserId"] = domainEvent.UserId,
            ["OldEmail"] = domainEvent.OldEmail,
            ["NewEmail"] = domainEvent.NewEmail,
            ["Action"] = "EmailChanged"
        };
    }
}

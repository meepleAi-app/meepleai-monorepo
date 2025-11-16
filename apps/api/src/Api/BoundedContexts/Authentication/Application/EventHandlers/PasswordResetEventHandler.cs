using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

/// <summary>
/// Handles the PasswordResetEvent domain event.
/// Creates audit log entry automatically via base class.
/// </summary>
public sealed class PasswordResetEventHandler : DomainEventHandlerBase<PasswordResetEvent>
{
    public PasswordResetEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<PasswordResetEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(PasswordResetEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        // Add additional business logic here if needed (e.g., send password reset notification email)
        await Task.CompletedTask;
    }

    protected override Guid? GetUserId(PasswordResetEvent domainEvent) => domainEvent.UserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(PasswordResetEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["UserId"] = domainEvent.UserId,
            ["ResetByUserId"] = domainEvent.ResetByUserId,
            ["Action"] = "PasswordReset"
        };
    }
}

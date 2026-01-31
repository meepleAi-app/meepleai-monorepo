using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for UserRateLimitOverrideCreatedEvent domain event.
/// Automatically creates audit log entry via base class.
/// No cache invalidation needed (overrides are not cached).
/// </summary>
internal sealed class UserRateLimitOverrideCreatedEventHandler
    : DomainEventHandlerBase<UserRateLimitOverrideCreatedEvent>
{
    public UserRateLimitOverrideCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<UserRateLimitOverrideCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(
        UserRateLimitOverrideCreatedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // No cache invalidation needed - user status queries don't cache user overrides
        // Audit logging is handled automatically by base class
        Logger.LogInformation(
            "User rate limit override created for user {UserId} by admin {AdminId}",
            domainEvent.UserId,
            domainEvent.AdminId);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(UserRateLimitOverrideCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["OverrideId"] = domainEvent.OverrideId,
            ["UserId"] = domainEvent.UserId,
            ["AdminId"] = domainEvent.AdminId,
            ["Action"] = "UserRateLimitOverrideCreated"
        };
    }

    protected override Guid? GetUserId(UserRateLimitOverrideCreatedEvent domainEvent)
    {
        return domainEvent.AdminId;
    }
}

using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for UserRateLimitOverrideRemovedEvent domain event.
/// Automatically creates audit log entry via base class.
/// No cache invalidation needed (overrides are not cached).
/// </summary>
internal sealed class UserRateLimitOverrideRemovedEventHandler
    : DomainEventHandlerBase<UserRateLimitOverrideRemovedEvent>
{
    public UserRateLimitOverrideRemovedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<UserRateLimitOverrideRemovedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(
        UserRateLimitOverrideRemovedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // No cache invalidation needed - user status queries don't cache user overrides
        // Audit logging is handled automatically by base class
        Logger.LogInformation(
            "User rate limit override removed for user {UserId} by admin {AdminId}",
            domainEvent.UserId,
            domainEvent.AdminId);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(UserRateLimitOverrideRemovedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["OverrideId"] = domainEvent.OverrideId,
            ["UserId"] = domainEvent.UserId,
            ["AdminId"] = domainEvent.AdminId,
            ["Action"] = "UserRateLimitOverrideRemoved"
        };
    }

    protected override Guid? GetUserId(UserRateLimitOverrideRemovedEvent domainEvent)
    {
        return domainEvent.AdminId;
    }
}

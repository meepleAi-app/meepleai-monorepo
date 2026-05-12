using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Authentication.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Flushes the <see cref="IStagingAccessGuard"/> in-memory cache whenever an allowlist
/// entry is added or removed. Keeps the cache TTL low-impact while still giving
/// admins immediate effect for add/remove operations.
/// </summary>
internal sealed class StagingAllowlistCacheInvalidator
    : INotificationHandler<StagingAllowlistEntryAddedEvent>,
      INotificationHandler<StagingAllowlistEntryRemovedEvent>
{
    private readonly IStagingAccessGuard _guard;
    private readonly ILogger<StagingAllowlistCacheInvalidator> _logger;

    public StagingAllowlistCacheInvalidator(
        IStagingAccessGuard guard,
        ILogger<StagingAllowlistCacheInvalidator> logger)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(StagingAllowlistEntryAddedEvent notification, CancellationToken cancellationToken)
    {
        _guard.InvalidateCache();
        _logger.LogDebug("Staging allowlist cache invalidated after add of {Email}", notification.Email);
        return Task.CompletedTask;
    }

    public Task Handle(StagingAllowlistEntryRemovedEvent notification, CancellationToken cancellationToken)
    {
        _guard.InvalidateCache();
        _logger.LogDebug("Staging allowlist cache invalidated after removal of {Email}", notification.Email);
        return Task.CompletedTask;
    }
}

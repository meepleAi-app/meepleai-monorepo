using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials.Events;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Reacts to <see cref="ProviderKeyRotatedEvent"/> by publishing a Redis pub/sub message via
/// <see cref="IProviderCacheInvalidator"/>. The publish is best-effort: any failure is logged but
/// MUST NOT bubble out — the rotation transaction has already committed and we cannot undo it.
///
/// Issue #1859.
/// </summary>
internal sealed class ProviderKeyRotatedEventHandler : INotificationHandler<ProviderKeyRotatedEvent>
{
    private readonly IProviderCacheInvalidator _invalidator;
    private readonly ILogger<ProviderKeyRotatedEventHandler> _logger;

    public ProviderKeyRotatedEventHandler(
        IProviderCacheInvalidator invalidator,
        ILogger<ProviderKeyRotatedEventHandler> logger)
    {
        _invalidator = invalidator ?? throw new ArgumentNullException(nameof(invalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ProviderKeyRotatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            await _invalidator
                .PublishInvalidationAsync(notification.ProviderName, cancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Event handler MUST NOT break the (already-committed) rotation tx
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "Failed to publish cache invalidation for provider '{Provider}' after rotation; " +
                "other pods will only refresh after the 5-minute resolver TTL expires",
                notification.ProviderName);
        }
    }
}

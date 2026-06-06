namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Publishes a cross-pod cache invalidation message for a provider's credential cache. The
/// Redis-backed implementation broadcasts on channel <c>provider:cache-invalidate</c>; each pod's
/// <c>ProviderCacheInvalidationSubscriber</c> picks the message up and calls
/// <see cref="IProviderCredentialResolver.Invalidate(string)"/> from a fresh DI scope.
///
/// Invoked from <c>ProviderKeyRotatedEventHandler</c> after a successful rotation transaction so
/// every pod (including the originator) refreshes its credential resolver cache.
///
/// Issue #1859.
/// </summary>
public interface IProviderCacheInvalidator
{
    /// <summary>
    /// Publish a cache-invalidate notification for the given provider name. Implementations MUST
    /// normalise the provider name to lowercase before serialising so subscribers can pattern
    /// match without case-folding.
    /// </summary>
    Task PublishInvalidationAsync(string providerName, CancellationToken ct);
}

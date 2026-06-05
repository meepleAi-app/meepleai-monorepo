using StackExchange.Redis;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Redis-backed <see cref="IProviderCacheInvalidator"/>. Publishes the provider name (lowercase)
/// as the message payload on the literal channel <see cref="ChannelName"/>; subscribers receive
/// the message via StackExchange.Redis pub/sub.
///
/// Issue #1859. Pattern reference: <c>SessionBroadcastService</c> in BC SessionTracking.
/// </summary>
internal sealed class RedisProviderCacheInvalidator : IProviderCacheInvalidator
{
    /// <summary>
    /// Literal Redis channel for provider credential cache invalidation messages.
    /// Pods subscribe via <c>ProviderCacheInvalidationSubscriber</c>.
    /// </summary>
    internal const string ChannelName = "provider:cache-invalidate";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisProviderCacheInvalidator> _logger;

    public RedisProviderCacheInvalidator(
        IConnectionMultiplexer redis,
        ILogger<RedisProviderCacheInvalidator> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishInvalidationAsync(string providerName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var normalized = providerName.ToLowerInvariant();

        var subscriber = _redis.GetSubscriber();
        var subscriberCount = await subscriber
            .PublishAsync(RedisChannel.Literal(ChannelName), normalized)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Published provider cache invalidation for '{Provider}' to {SubscriberCount} subscriber(s)",
            normalized, subscriberCount);
    }
}

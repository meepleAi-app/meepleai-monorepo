using Api.BoundedContexts.Administration.Infrastructure.Services;
using StackExchange.Redis;

namespace Api.BoundedContexts.Administration.Infrastructure.HostedServices;

/// <summary>
/// Background service that subscribes to the Redis channel
/// <c>provider:cache-invalidate</c> at startup. Each message carries the lowercase provider name
/// to invalidate; the handler creates a fresh DI scope, resolves the per-pod
/// <see cref="IProviderCredentialResolver"/>, and calls <c>Invalidate</c>.
///
/// Why a scope per message: the resolver is registered as a singleton, but resolving it via
/// <see cref="IServiceScopeFactory"/> keeps the contract uniform with other scoped consumers and
/// allows future migration to a scoped lifetime without touching this code.
///
/// Issue #1859.
/// </summary>
internal sealed class ProviderCacheInvalidationSubscriber : IHostedService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderCacheInvalidationSubscriber> _logger;
    private ChannelMessageQueue? _queue;

    public ProviderCacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopeFactory,
        ILogger<ProviderCacheInvalidationSubscriber> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = _redis.GetSubscriber();
        _queue = await subscriber
            .SubscribeAsync(RedisChannel.Literal(RedisProviderCacheInvalidator.ChannelName))
            .ConfigureAwait(false);

        _queue.OnMessage(message =>
        {
            var providerName = message.Message.ToString();
            if (string.IsNullOrWhiteSpace(providerName))
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var resolver = scope.ServiceProvider.GetRequiredService<IProviderCredentialResolver>();
                resolver.Invalidate(providerName);
                _logger.LogInformation(
                    "Received Redis cache invalidation for provider '{Provider}'",
                    providerName);
            }
#pragma warning disable CA1031 // Pub/sub callback must not throw — would break the channel queue
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "Failed to invalidate provider cache for '{Provider}' after Redis message",
                    providerName);
            }
        });

        _logger.LogInformation(
            "Subscribed to Redis channel '{Channel}' for provider cache invalidation",
            RedisProviderCacheInvalidator.ChannelName);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_queue is not null)
        {
            await _queue.UnsubscribeAsync().ConfigureAwait(false);
            _queue = null;
            _logger.LogInformation(
                "Unsubscribed from Redis channel '{Channel}' for provider cache invalidation",
                RedisProviderCacheInvalidator.ChannelName);
        }
    }
}

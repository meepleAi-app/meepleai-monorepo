using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Cached value wrapper for review lock duration.
/// Required because HybridCache only supports reference types (where T : class).
/// </summary>
internal sealed record CachedLockDuration(int Minutes);

/// <summary>
/// Service for retrieving review lock configuration from SystemConfiguration with caching.
/// Issue #2729: Application - Review Lock Management
/// Uses HybridCache with 5-minute TTL following pattern from LlmTierRoutingService.
/// </summary>
internal sealed class ReviewLockConfigService : IReviewLockConfigService
{
    private const string ConfigKey = "ReviewLock:DefaultDurationMinutes";
    private const int DefaultDurationMinutes = 30;
    private const string CacheTag = "review-lock-config";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<ReviewLockConfigService> _logger;

    public ReviewLockConfigService(
        IServiceScopeFactory scopeFactory,
        IHybridCacheService cache,
        ILogger<ReviewLockConfigService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> GetDefaultLockDurationMinutesAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"review-lock:duration:{ConfigKey}";

        var cached = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                try
                {
                    // Create scope for MediatR query (Singleton service accessing Scoped dependencies)
#pragma warning disable MA0004 // CreateAsyncScope is not async, ConfigureAwait not applicable
                    await using var scope = _scopeFactory.CreateAsyncScope();
#pragma warning restore MA0004
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    var query = new GetConfigByKeyQuery(
                        Key: ConfigKey,
                        Environment: null,  // Use default environment
                        ActiveOnly: true);

                    var config = await mediator.Send(query, ct).ConfigureAwait(false);

                    if (config != null && int.TryParse(config.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0)
                    {
                        _logger.LogDebug(
                            "Using configured review lock duration: {Minutes} minutes from key: {ConfigKey}",
                            minutes, ConfigKey);
                        return new CachedLockDuration(minutes);
                    }

                    _logger.LogDebug(
                        "Review lock configuration not found or invalid, using default: {DefaultMinutes} minutes",
                        DefaultDurationMinutes);

                    return new CachedLockDuration(DefaultDurationMinutes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to retrieve review lock configuration, using default: {DefaultMinutes} minutes",
                        DefaultDurationMinutes);

                    return new CachedLockDuration(DefaultDurationMinutes);
                }
            },
            new[] { CacheTag },
            CacheDuration,
            cancellationToken).ConfigureAwait(false);

        return cached.Minutes;
    }
}

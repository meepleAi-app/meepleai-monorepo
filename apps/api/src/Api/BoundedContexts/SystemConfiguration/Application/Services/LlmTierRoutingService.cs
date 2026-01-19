using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Implementation of ILlmTierRoutingService with caching.
/// Issue #2596: Database-driven LLM tier routing with test/production separation.
/// </summary>
/// <remarks>
/// This service is designed to be registered as Singleton for use by HybridAdaptiveRoutingStrategy.
/// Uses IServiceScopeFactory to create scoped repository instances for database access.
/// HybridCache ensures repository is only called on cache misses (every 5 minutes max).
/// </remarks>
internal sealed class LlmTierRoutingService : ILlmTierRoutingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHybridCacheService _cache;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LlmTierRoutingService> _logger;

    private const string CacheTagTierRouting = "tier-routing";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public LlmTierRoutingService(
        IServiceScopeFactory scopeFactory,
        IHybridCacheService cache,
        IHostEnvironment hostEnvironment,
        ILogger<LlmTierRoutingService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiModelConfiguration?> GetModelForTierAsync(LlmUserTier tier, CancellationToken ct = default)
    {
        var environment = _hostEnvironment.IsProduction()
            ? LlmEnvironmentType.Production
            : LlmEnvironmentType.Test;

        var cacheKey = $"tier-model:{tier}:{environment}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>();

                var model = await repository.GetDefaultForTierAsync(tier, environment, cancellationToken)
                    .ConfigureAwait(false);

                if (model == null)
                {
                    _logger.LogWarning(
                        "No default model configured for tier {Tier} in environment {Environment}. Falling back to appsettings.",
                        tier, environment);
                }
                else
                {
                    _logger.LogDebug(
                        "Retrieved tier routing: {Tier}/{Environment} -> {ModelId} ({Provider})",
                        tier, environment, model.ModelId, model.Provider);
                }

                return model!;
            },
            [CacheTagTierRouting, $"tier:{tier}", $"env:{environment}"],
            CacheDuration,
            ct).ConfigureAwait(false);
    }

    public async Task<AiModelConfiguration?> GetTestModelForTierAsync(LlmUserTier tier, CancellationToken ct = default)
    {
        var cacheKey = $"tier-model:{tier}:{LlmEnvironmentType.Test}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>();

                var model = await repository.GetDefaultForTierAsync(tier, LlmEnvironmentType.Test, cancellationToken)
                    .ConfigureAwait(false);

                if (model == null)
                {
                    _logger.LogWarning(
                        "No test model configured for tier {Tier}. Falling back to production model.",
                        tier);
                }

                return model!;
            },
            [CacheTagTierRouting, $"tier:{tier}", "env:test"],
            CacheDuration,
            ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TierRoutingInfo>> GetAllTierRoutingsAsync(CancellationToken ct = default)
    {
        var cacheKey = "tier-routing:all";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancellationToken =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>();

                var allConfigs = await repository.GetAllTierRoutingsAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Group by tier and build TierRoutingInfo
                var tiers = Enum.GetValues<LlmUserTier>();
                var result = new List<TierRoutingInfo>();

                foreach (var tier in tiers)
                {
                    var prodModel = allConfigs.FirstOrDefault(c =>
                        c.ApplicableTier == tier &&
                        c.EnvironmentType == LlmEnvironmentType.Production &&
                        c.IsDefaultForTier);

                    var testModel = allConfigs.FirstOrDefault(c =>
                        c.ApplicableTier == tier &&
                        c.EnvironmentType == LlmEnvironmentType.Test &&
                        c.IsDefaultForTier);

                    // Calculate estimated monthly cost based on usage stats
                    var estimatedCost = (prodModel?.Usage?.TotalCostUsd ?? 0) +
                                       (testModel?.Usage?.TotalCostUsd ?? 0);

                    result.Add(new TierRoutingInfo
                    {
                        Tier = tier,
                        ProductionModelId = prodModel?.ModelId,
                        ProductionModelName = prodModel?.DisplayName,
                        ProductionProvider = prodModel?.Provider,
                        TestModelId = testModel?.ModelId,
                        TestModelName = testModel?.DisplayName,
                        TestProvider = testModel?.Provider,
                        EstimatedMonthlyCostUsd = estimatedCost
                    });
                }

                return result;
            },
            [CacheTagTierRouting],
            CacheDuration,
            ct).ConfigureAwait(false);
    }

    public async Task InvalidateCacheAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Invalidating tier routing cache");
        await _cache.RemoveByTagAsync(CacheTagTierRouting, ct).ConfigureAwait(false);
    }
}

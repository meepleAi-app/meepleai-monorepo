using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Service for retrieving strategy-to-model mappings with caching.
/// Issue #3435: Part of tier-strategy-model architecture.
/// </summary>
/// <remarks>
/// This service is designed to be registered as Singleton for use by HybridAdaptiveRoutingStrategy.
/// Uses IServiceScopeFactory to create scoped repository instances for database access.
/// HybridCache ensures repository is only called on cache misses (every 5 minutes max).
/// </remarks>
internal sealed class StrategyModelMappingService : IStrategyModelMappingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<StrategyModelMappingService> _logger;

    private const string CacheTagStrategyMapping = "strategy-model-mapping";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public StrategyModelMappingService(
        IServiceScopeFactory scopeFactory,
        IHybridCacheService cache,
        ILogger<StrategyModelMappingService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<StrategyModelMapping?> GetModelMappingAsync(
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.GetDisplayName();
        var cacheKey = $"strategy-model:{strategyName}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IStrategyModelMappingRepository>();

                var entry = await repository.GetByStrategyAsync(strategy, ct)
                    .ConfigureAwait(false);

                if (entry == null)
                {
                    _logger.LogDebug(
                        "No database mapping for strategy {Strategy}, using default",
                        strategyName);
                    return StrategyModelMapping.Default(strategy);
                }

                _logger.LogDebug(
                    "Retrieved strategy mapping: {Strategy} -> {Provider}/{Model}",
                    strategyName, entry.Provider, entry.PrimaryModel);

                return new StrategyModelMapping(
                    strategy,
                    NormalizeProvider(entry.Provider),
                    entry.PrimaryModel,
                    entry.FallbackModels,
                    entry.IsCustomizable);
            },
            [CacheTagStrategyMapping, $"strategy:{strategyName}"],
            CacheDuration,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<(string Provider, string ModelId)> GetModelForStrategyAsync(
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var mapping = await GetModelMappingAsync(strategy, cancellationToken)
            .ConfigureAwait(false);

        // GetModelMappingAsync never returns null - it returns default mapping
        var effectiveMapping = mapping ?? StrategyModelMapping.Default(strategy);

        _logger.LogDebug(
            "Model for strategy {Strategy}: {Provider}/{Model}",
            strategy.GetDisplayName(),
            effectiveMapping.Provider,
            effectiveMapping.PrimaryModel);

        return (effectiveMapping.Provider, effectiveMapping.PrimaryModel);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetFallbackModelsAsync(
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var mapping = await GetModelMappingAsync(strategy, cancellationToken)
            .ConfigureAwait(false);

        var effectiveMapping = mapping ?? StrategyModelMapping.Default(strategy);

        return effectiveMapping.FallbackModels;
    }

    /// <summary>
    /// Normalizes provider name to consistent casing.
    /// Database stores lowercase (openrouter, ollama), we normalize to PascalCase.
    /// </summary>
    private static string NormalizeProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "openrouter" => "OpenRouter",
        "ollama" => "Ollama",
        _ => provider
    };
}

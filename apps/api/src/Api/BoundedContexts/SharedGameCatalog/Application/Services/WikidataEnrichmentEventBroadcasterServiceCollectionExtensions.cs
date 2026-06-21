using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2256 — DI factory wiring for
/// <see cref="IWikidataEnrichmentEventBroadcaster"/>.
///
/// Selects between <see cref="InMemoryWikidataEnrichmentEventBroadcaster"/>
/// (default, single-pod / local dev) and
/// <see cref="RedisWikidataEnrichmentEventBroadcaster"/> (multi-pod) based on
/// the <c>WIKIDATA_SSE_BACKPLANE</c> configuration key.
/// </summary>
/// <remarks>
/// <para>The factory pattern keeps the env-var routing in one place so
/// integration tests (which spin up the host with their own <see cref="IConfiguration"/>)
/// can flip the backplane without recompiling.</para>
///
/// <para>Default is <c>in-memory</c> — backwards-compatible with the F4 PR
/// #2227 single-pod assumption (ADR DEC-3e). When the operator opts into
/// <c>redis</c>, an <see cref="IConnectionMultiplexer"/> must already be
/// registered (it is, via <c>AddInfrastructureServices</c> → Redis cache).</para>
/// </remarks>
internal static class WikidataEnrichmentEventBroadcasterServiceCollectionExtensions
{
    /// <summary>Configuration key controlling the broadcaster backplane.</summary>
    public const string BackplaneConfigKey = "WIKIDATA_SSE_BACKPLANE";

    /// <summary>Legal values for <see cref="BackplaneConfigKey"/>.</summary>
    public const string BackplaneInMemory = "in-memory";
    public const string BackplaneRedis = "redis";

    /// <summary>
    /// Registers <see cref="IWikidataEnrichmentEventBroadcaster"/> as a singleton
    /// using the factory described in the class remarks. The implementation is
    /// resolved lazily on first DI resolve so configuration changes between
    /// service registration and host start are honoured.
    /// </summary>
    public static IServiceCollection AddWikidataEnrichmentEventBroadcaster(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IWikidataEnrichmentEventBroadcaster>(sp =>
        {
            var rawValue = configuration[BackplaneConfigKey];
            var normalized = (rawValue ?? string.Empty).Trim();

            // Default → in-memory. Backwards-compatible with the F4 PR #2227
            // single-pod default.
            if (string.IsNullOrEmpty(normalized) ||
                string.Equals(normalized, BackplaneInMemory, StringComparison.OrdinalIgnoreCase))
            {
                var logger = sp.GetRequiredService<ILogger<InMemoryWikidataEnrichmentEventBroadcaster>>();
                return new InMemoryWikidataEnrichmentEventBroadcaster(logger);
            }

            if (string.Equals(normalized, BackplaneRedis, StringComparison.OrdinalIgnoreCase))
            {
                var multiplexer = sp.GetService<IConnectionMultiplexer>()
                    ?? throw new InvalidOperationException(
                        $"IConnectionMultiplexer is not registered but {BackplaneConfigKey}=redis was requested. " +
                        "Ensure AddInfrastructureServices() has run before AddSharedGameCatalogContext().");

                var logger = sp.GetRequiredService<ILogger<RedisWikidataEnrichmentEventBroadcaster>>();
                return new RedisWikidataEnrichmentEventBroadcaster(multiplexer, logger);
            }

            throw new InvalidOperationException(
                $"Invalid {BackplaneConfigKey} value '{rawValue}'. " +
                $"Allowed values: '{BackplaneInMemory}' (default), '{BackplaneRedis}'.");
        });

        return services;
    }
}

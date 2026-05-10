using System.Diagnostics.Metrics;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Models;
using Microsoft.Extensions.Logging;

namespace Api.Observability;

/// <summary>
/// Issue #985 (G4): static facade providing measurement callbacks for ObservableGauge metrics.
///
/// Why static: Meter.CreateObservableGauge requires the callback at field-initialization time,
/// which must reference a static method.
///
/// Initialization happens once at startup via <see cref="Initialize"/> from the DI container
/// (see <see cref="ProviderQuotaMetricsHostedService"/>).
///
/// Callbacks invoke <see cref="IProviderQuotaService.GetQuotaAsync"/> which uses HybridCache
/// (5min TTL) — Prometheus scrape (~15s interval) hits cache 99% of the time.
/// </summary>
internal static class ProviderQuotaMetricsRegistrar
{
    /// <summary>Mirror of backend DI provider names — must update when adding providers to <c>InfrastructureServiceExtensions</c>.</summary>
    private static readonly string[] SupportedProviders = ["openrouter", "deepseek"];

    private static IProviderQuotaService? _quotaService;
    private static ILogger? _logger;

    public static void Initialize(IProviderQuotaService quotaService, ILogger logger)
    {
        _quotaService = quotaService;
        _logger = logger;
    }

    /// <summary>Callback for <c>meepleai.provider.quota_remaining_usd</c> gauge.</summary>
    public static IEnumerable<Measurement<double>> ObserveRemainingUsd()
        => Observe(q => q.RemainingUsd);

    /// <summary>Callback for <c>meepleai.provider.quota_used_usd</c> gauge.</summary>
    public static IEnumerable<Measurement<double>> ObserveUsedUsd()
        => Observe(q => q.UsedUsd);

    /// <summary>Callback for <c>meepleai.provider.quota_limit_usd</c> gauge.</summary>
    public static IEnumerable<Measurement<double>> ObserveLimitUsd()
        => Observe(q => q.LimitUsd);

    /// <summary>
    /// Sync-over-async is SAFE here because:
    /// 1. Prometheus scrape callback runs on a threadpool thread (no SynchronizationContext deadlock risk).
    /// 2. IHybridCacheService.GetOrCreateAsync uses ConfigureAwait(false) internally.
    /// 3. ProviderQuotaService.GetQuotaAsync awaits with ConfigureAwait(false).
    /// If any of these change, restructure to async-snapshot (background timer updates a static dict).
    /// </summary>
    private static IEnumerable<Measurement<double>> Observe(Func<ProviderQuotaDto, decimal?> selector)
    {
        if (_quotaService is null)
            yield break;

        foreach (var provider in SupportedProviders)
        {
            ProviderQuotaDto? dto = null;
            try
            {
#pragma warning disable VSTHRD002 // sync-over-async — see XML comment above for safety rationale
                dto = _quotaService.GetQuotaAsync(provider, CancellationToken.None).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }
#pragma warning disable CA1031 // catch general exception — metric callback must never throw to OTEL pipeline
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Provider quota metric callback failed for {Provider}", provider);
            }
#pragma warning restore CA1031

            if (dto is null || !dto.QuotaSupported || !dto.TokenConfigured)
                continue;

            var value = selector(dto);
            if (value.HasValue)
                yield return new Measurement<double>((double)value.Value,
                    new KeyValuePair<string, object?>("provider", provider));
        }
    }
}

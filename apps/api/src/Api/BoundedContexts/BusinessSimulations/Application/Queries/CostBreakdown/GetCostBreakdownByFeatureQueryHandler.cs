using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Handler for <see cref="GetCostBreakdownByFeatureQuery"/> (Issue #1838 SP5 F4-C5).
///
/// <para>Aggregates expense ledger entries within the chosen range into
/// <c>feature → cost</c> buckets keyed by <see cref="LedgerCategory"/>, with
/// a secondary breakdown by provider for the drill-down row. Provider
/// derivation reuses <see cref="ProviderResolver"/> for consistency with the
/// by-provider query.</para>
///
/// <para>HybridCache: 5min TTL, keyed by range, tagged with
/// <c>business:cost-breakdown</c> + <c>business:cost-breakdown:by-feature</c>.</para>
/// </summary>
internal sealed class GetCostBreakdownByFeatureQueryHandler
    : IRequestHandler<GetCostBreakdownByFeatureQuery, CostBreakdownByFeatureDto>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly string[] CacheTags =
    {
        "business:cost-breakdown",
        "business:cost-breakdown:by-feature",
    };

    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;

    public GetCostBreakdownByFeatureQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<CostBreakdownByFeatureDto> Handle(
        GetCostBreakdownByFeatureQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"business:cost-breakdown:by-feature:{request.Range.ToWireValue()}";

        return _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(request.Range, ct).ConfigureAwait(false),
            new HybridCacheEntryOptions { Expiration = CacheDuration },
            CacheTags,
            cancellationToken).AsTask();
    }

    private async Task<CostBreakdownByFeatureDto> ComputeAsync(
        CostBreakdownRange range,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var toDate = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        var fromDate = toDate.AddDays(-range.Days());

        var rows = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Type == LedgerEntryType.Expense
                        && e.Date >= fromDate
                        && e.Date < toDate)
            .Select(e => new
            {
                Amount = e.Amount.Amount,
                e.Category,
                e.Metadata,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // featureBuckets[category] => providerName => total cost
        var featureBuckets = new Dictionary<LedgerCategory, Dictionary<string, decimal>>();
        decimal grandTotal = 0m;

        foreach (var row in rows)
        {
            if (!featureBuckets.TryGetValue(row.Category, out var providerMap))
            {
                providerMap = new Dictionary<string, decimal>(StringComparer.Ordinal);
                featureBuckets[row.Category] = providerMap;
            }
            var provider = ProviderResolver.Resolve(row.Category, row.Metadata);
            providerMap.TryGetValue(provider, out var tot);
            providerMap[provider] = tot + row.Amount;

            grandTotal += row.Amount;
        }

        var features = featureBuckets
            .Select(kvp =>
            {
                var totalForFeature = kvp.Value.Values.Sum();
                var percentage = grandTotal > 0m
                    ? Math.Round(totalForFeature / grandTotal * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;
                var providers = kvp.Value
                    .OrderByDescending(p => p.Value)
                    .Select(p => new CostBreakdownProviderEntryDto(p.Key, p.Value))
                    .ToList();

                return new CostBreakdownFeatureDto(
                    Feature: ProviderResolver.ResolveFeatureName(kvp.Key),
                    TotalCost: totalForFeature,
                    PercentageOfTotal: percentage,
                    Providers: providers);
            })
            .OrderByDescending(f => f.TotalCost)
            .ToList();

        return new CostBreakdownByFeatureDto(
            Range: range.ToWireValue(),
            FromDate: fromDate,
            ToDate: toDate,
            Features: features,
            GrandTotal: grandTotal);
    }
}

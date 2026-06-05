using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Handler for <see cref="GetCostBreakdownByProviderQuery"/> (Issue #1838 SP5 F4-C5).
///
/// <para>Aggregates expense ledger entries within the chosen range into
/// <c>(date, provider) → cost</c> buckets. Provider derivation uses
/// <see cref="ProviderResolver"/> on the in-memory rows so JSON parsing
/// stays out of the SQL layer. The DB query projects only the fields needed
/// (<c>Date</c>, <c>Amount</c>, <c>Category</c>, <c>Metadata</c>) to keep
/// memory bounded for the 1y window.</para>
///
/// <para>HybridCache: 5min TTL, keyed by range, with tags
/// <c>business:cost-breakdown</c> + <c>business:cost-breakdown:by-provider</c>
/// so the FE invalidation hook can purge both per-provider and per-feature
/// snapshots when ledger data churns.</para>
/// </summary>
internal sealed class GetCostBreakdownByProviderQueryHandler
    : IRequestHandler<GetCostBreakdownByProviderQuery, CostBreakdownByProviderDto>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly string[] CacheTags =
    {
        "business:cost-breakdown",
        "business:cost-breakdown:by-provider",
    };

    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;

    public GetCostBreakdownByProviderQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<CostBreakdownByProviderDto> Handle(
        GetCostBreakdownByProviderQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"business:cost-breakdown:by-provider:{request.Range.ToWireValue()}";

        return _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAsync(request.Range, ct).ConfigureAwait(false),
            new HybridCacheEntryOptions { Expiration = CacheDuration },
            CacheTags,
            cancellationToken).AsTask();
    }

    private async Task<CostBreakdownByProviderDto> ComputeAsync(
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
                e.Date,
                Amount = e.Amount.Amount,
                e.Category,
                e.Metadata,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var dayBuckets = new Dictionary<DateTime, Dictionary<string, decimal>>();
        var providerTotals = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal grandTotal = 0m;

        foreach (var row in rows)
        {
            var dayKey = new DateTime(row.Date.Year, row.Date.Month, row.Date.Day, 0, 0, 0, DateTimeKind.Utc);
            var provider = ProviderResolver.Resolve(row.Category, row.Metadata);

            if (!dayBuckets.TryGetValue(dayKey, out var providerMap))
            {
                providerMap = new Dictionary<string, decimal>(StringComparer.Ordinal);
                dayBuckets[dayKey] = providerMap;
            }
            providerMap.TryGetValue(provider, out var dayProviderTotal);
            providerMap[provider] = dayProviderTotal + row.Amount;

            providerTotals.TryGetValue(provider, out var tot);
            providerTotals[provider] = tot + row.Amount;

            grandTotal += row.Amount;
        }

        var daysDto = dayBuckets
            .OrderBy(kvp => kvp.Key)
            .Select(kvp =>
            {
                var providers = kvp.Value
                    .OrderByDescending(p => p.Value)
                    .Select(p => new CostBreakdownProviderEntryDto(p.Key, p.Value))
                    .ToList();
                var dayTotal = kvp.Value.Values.Sum();
                return new CostBreakdownByProviderDayDto(kvp.Key, providers, dayTotal);
            })
            .ToList();

        var totalsDto = providerTotals
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new CostBreakdownProviderTotalDto(kvp.Key, kvp.Value))
            .ToList();

        return new CostBreakdownByProviderDto(
            Range: range.ToWireValue(),
            FromDate: fromDate,
            ToDate: toDate,
            Days: daysDto,
            ProviderTotals: totalsDto,
            GrandTotal: grandTotal);
    }
}

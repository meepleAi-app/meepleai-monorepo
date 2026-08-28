using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository implementation for UserTokenUsage (Issue #3692)
/// </summary>
public sealed class UserTokenUsageRepository : RepositoryBase, IUserTokenUsageRepository
{

    public UserTokenUsageRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<UserTokenUsage?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserTokenUsage>()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserTokenUsage>> GetTopConsumersAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserTokenUsage>()
            .OrderByDescending(u => u.TokensUsed)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DailyUsageAggregate>> GetUsageHistoryAsync(
        DateTime from,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        var usages = await DbContext.Set<UserTokenUsage>()
            .Where(u => u.UpdatedAt >= from && u.UpdatedAt <= until)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Group by date and aggregate
        var dailyAggregates = usages
            .SelectMany(u => u.History.Where(h =>
            {
                var monthDate = DateTime.ParseExact(h.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                return monthDate >= from && monthDate <= until;
            })
            .Select(h => new
            {
                Date = DateTime.ParseExact(h.Month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                h.TokensUsed,
                h.Cost
            }))
            .GroupBy(x => x.Date.Date)
            .Select(g => new DailyUsageAggregate(
                g.Key,
                g.Sum(x => x.TokensUsed),
                g.Sum(x => x.Cost)))
            .OrderBy(d => d.Date)
            .ToList();

        return dailyAggregates;
    }

    public async Task<Dictionary<Guid, int>> GetUsageByTierAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserTokenUsage>()
            .GroupBy(u => u.TierId)
            .Select(g => new { TierId = g.Key, TotalUsage = g.Sum(u => u.TokensUsed) })
            .ToDictionaryAsync(x => x.TierId, x => x.TotalUsage, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountUsersByTierAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserTokenUsage>()
            .CountAsync(u => u.TierId == tierId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(UserTokenUsage usage, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<UserTokenUsage>().AddAsync(usage, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserTokenUsage usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);

        // Issue #3866: reads run with the production NoTracking default (PERF-06), so every call
        // hands us a FRESH instance of the row. TokenTrackingService does read-modify-write once per
        // tracked usage, and it runs more than once inside a single scope: the first call leaves its
        // instance tracked by Update(), and the second one attaches a second instance of the same
        // key — 'cannot be tracked because another instance with the same key value is already being
        // tracked'. Resolve the identity first and copy the values onto the tracked instance; every
        // property of this aggregate is scalar (the two lists are jsonb converters), so SetValues
        // carries all of them. Same shape as ReportExecutionRepository (#2541).
        var tracked = DbContext.Set<UserTokenUsage>().Local.FirstOrDefault(e => e.Id == usage.Id);
        if (tracked is not null && !ReferenceEquals(tracked, usage))
        {
            DbContext.Entry(tracked).CurrentValues.SetValues(usage);
        }
        else
        {
            DbContext.Set<UserTokenUsage>().Update(usage);
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserTokenUsage>()
            .AnyAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
    }
}
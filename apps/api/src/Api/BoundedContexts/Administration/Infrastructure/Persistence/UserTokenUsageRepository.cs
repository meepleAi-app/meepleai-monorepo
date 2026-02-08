using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// EF Core repository implementation for UserTokenUsage (Issue #3692)
/// </summary>
public sealed class UserTokenUsageRepository : IUserTokenUsageRepository
{
    private readonly MeepleAiDbContext _context;

    public UserTokenUsageRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<UserTokenUsage?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserTokenUsage>()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserTokenUsage>> GetTopConsumersAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserTokenUsage>()
            .OrderByDescending(u => u.TokensUsed)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DailyUsageAggregate>> GetUsageHistoryAsync(
        DateTime from,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        var usages = await _context.Set<UserTokenUsage>()
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
        return await _context.Set<UserTokenUsage>()
            .GroupBy(u => u.TierId)
            .Select(g => new { TierId = g.Key, TotalUsage = g.Sum(u => u.TokensUsed) })
            .ToDictionaryAsync(x => x.TierId, x => x.TotalUsage, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountUsersByTierAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserTokenUsage>()
            .CountAsync(u => u.TierId == tierId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(UserTokenUsage usage, CancellationToken cancellationToken = default)
    {
        await _context.Set<UserTokenUsage>().AddAsync(usage, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserTokenUsage usage, CancellationToken cancellationToken = default)
    {
        _context.Set<UserTokenUsage>().Update(usage);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<UserTokenUsage>()
            .AnyAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
    }
}
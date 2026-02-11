using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for LedgerEntry aggregate.
/// Issue #3720: Financial Ledger Data Model
/// </summary>
internal class LedgerEntryRepository : RepositoryBase, ILedgerEntryRepository
{
    public LedgerEntryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<LedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.LedgerEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.LedgerEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(LedgerEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        await DbContext.LedgerEntries
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(LedgerEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        DbContext.LedgerEntries.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(LedgerEntry entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        DbContext.LedgerEntries.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.LedgerEntries
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await query
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entries, total);
    }

    public async Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetByTypeAsync(
        LedgerEntryType type,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Type == type);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await query
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entries, total);
    }

    public async Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetByCategoryAsync(
        LedgerCategory category,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Category == category);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await query
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entries, total);
    }

    public async Task<(decimal TotalIncome, decimal TotalExpense)> GetSummaryByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var summary = await DbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Date >= from && e.Date <= to)
            .GroupBy(e => e.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(e => e.Amount.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalIncome = summary
            .FirstOrDefault(s => s.Type == LedgerEntryType.Income)?.Total ?? 0m;

        var totalExpense = summary
            .FirstOrDefault(s => s.Type == LedgerEntryType.Expense)?.Total ?? 0m;

        return (totalIncome, totalExpense);
    }
}

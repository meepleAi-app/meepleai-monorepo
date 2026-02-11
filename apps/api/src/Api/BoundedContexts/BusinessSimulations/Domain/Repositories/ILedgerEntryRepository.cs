using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Repositories;

/// <summary>
/// Repository interface for LedgerEntry aggregate.
/// Issue #3720: Financial Ledger Data Model
/// </summary>
internal interface ILedgerEntryRepository : IRepository<LedgerEntry, Guid>
{
    /// <summary>
    /// Gets ledger entries within a date range, ordered by date descending.
    /// </summary>
    Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ledger entries filtered by type (Income/Expense).
    /// </summary>
    Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetByTypeAsync(
        LedgerEntryType type,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets ledger entries filtered by category.
    /// </summary>
    Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetByCategoryAsync(
        LedgerCategory category,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sum of amounts grouped by type for a given date range.
    /// Returns (totalIncome, totalExpense).
    /// </summary>
    Task<(decimal TotalIncome, decimal TotalExpense)> GetSummaryByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}

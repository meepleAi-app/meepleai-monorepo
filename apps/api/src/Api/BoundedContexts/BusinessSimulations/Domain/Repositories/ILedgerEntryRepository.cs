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

    /// <summary>
    /// Gets ledger entries with combined filters (type, category, source, date range).
    /// Issue #3722: Manual Ledger CRUD
    /// </summary>
    Task<(IReadOnlyList<LedgerEntry> Entries, int Total)> GetFilteredAsync(
        LedgerEntryType? type = null,
        LedgerCategory? category = null,
        LedgerEntrySource? source = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the LedgerEntry and commits the change immediately. Returns
    /// <c>false</c> when the insert violates the <c>(source_event_id)</c> UNIQUE
    /// partial index — an outbox replay or concurrent pod already wrote the
    /// row for this <c>IDomainEvent.EventId</c> (CF-2 / #1938). On <c>false</c>
    /// the tracked entity is detached so subsequent <c>SaveChanges</c> on the
    /// same scoped context don't retry it.
    /// </summary>
    /// <remarks>
    /// Inline-commit pattern. Used by <see cref="Api.BoundedContexts.BusinessSimulations.Application.Interfaces.ILedgerTrackingService.TrackTokenUsageAsync"/>
    /// where the rest of the service relies on a downstream UoW pipeline that
    /// would otherwise turn a 23505 into an unhandled exception.
    /// </remarks>
    Task<bool> AddAndCommitAsync(LedgerEntry entry, CancellationToken cancellationToken = default);
}

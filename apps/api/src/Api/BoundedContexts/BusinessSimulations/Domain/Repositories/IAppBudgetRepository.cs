using Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Repositories;

/// <summary>
/// Repository contract for the <see cref="AppBudget"/> aggregate
/// (Issue #1838 SP5 F4-C5). Singleton aggregate: there is at most one row per
/// environment; <see cref="GetCurrentAsync"/> returns <c>null</c> for first-time
/// admin visits and the upsert flow handles both insert and update branches.
/// </summary>
internal interface IAppBudgetRepository
{
    /// <summary>
    /// Returns the current singleton AppBudget row or <c>null</c> if the budget
    /// has never been configured (empty-state).
    /// </summary>
    Task<AppBudget?> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the singleton AppBudget. When a row already exists the
    /// implementation MUST enforce optimistic concurrency via the aggregate's
    /// <see cref="AppBudget.RowVersion"/> token (translated to
    /// <c>DbUpdateConcurrencyException</c> by EF Core).
    /// </summary>
    Task UpsertAsync(AppBudget budget, CancellationToken cancellationToken = default);
}

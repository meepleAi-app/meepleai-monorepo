using Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.BusinessSimulations;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;

/// <summary>
/// EF-Core backed implementation of <see cref="IAppBudgetRepository"/>
/// (Issue #1838 SP5 F4-C5). The singleton AppBudget row is detected
/// application-side: <see cref="UpsertAsync"/> looks for an existing row and
/// either inserts (first-time config) or in-place updates (admin edit), with
/// optimistic concurrency enforced by PostgreSQL's xmin system column (ADR-060).
/// </summary>
internal sealed class AppBudgetRepository : RepositoryBase, IAppBudgetRepository
{
    public AppBudgetRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AppBudget?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AppBudgets
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpsertAsync(AppBudget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(budget);

        // We deliberately re-query without AsNoTracking so EF can attach the
        // existing row for an in-place update with concurrency checking.
        var tracked = await DbContext.AppBudgets
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tracked is null)
        {
            var entity = new AppBudgetEntity
            {
                Id = budget.Id,
                MonthlyLimitAmount = budget.MonthlyLimit.Amount,
                MonthlyLimitCurrency = budget.MonthlyLimit.Currency,
                AlertThresholdPct = budget.AlertThresholdPct,
                CriticalThresholdPct = budget.CriticalThresholdPct,
                IsEnabled = budget.IsEnabled,
                CreatedAt = budget.CreatedAt,
                UpdatedAt = budget.UpdatedAt,
                CreatedBy = budget.CreatedBy,
                UpdatedBy = budget.UpdatedBy,
            };
            await DbContext.AppBudgets.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Force EF to check the concurrency token: assigning the client-supplied
            // xmin via Entry.OriginalValue makes DbUpdateConcurrencyException fire when
            // another admin has bumped xmin since the aggregate was loaded (the freshly
            // re-queried `tracked` row already carries the newer xmin).
            DbContext.Entry(tracked).Property(p => p.Xmin).OriginalValue = budget.Xmin;

            tracked.MonthlyLimitAmount = budget.MonthlyLimit.Amount;
            tracked.MonthlyLimitCurrency = budget.MonthlyLimit.Currency;
            tracked.AlertThresholdPct = budget.AlertThresholdPct;
            tracked.CriticalThresholdPct = budget.CriticalThresholdPct;
            tracked.IsEnabled = budget.IsEnabled;
            tracked.UpdatedAt = budget.UpdatedAt;
            tracked.UpdatedBy = budget.UpdatedBy;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static AppBudget MapToDomain(AppBudgetEntity e) =>
        AppBudget.Reconstitute(
            e.Id,
            Money.Create(e.MonthlyLimitAmount, e.MonthlyLimitCurrency),
            e.AlertThresholdPct,
            e.CriticalThresholdPct,
            e.IsEnabled,
            e.CreatedAt,
            e.UpdatedAt,
            e.CreatedBy,
            e.UpdatedBy,
            e.Xmin);
}

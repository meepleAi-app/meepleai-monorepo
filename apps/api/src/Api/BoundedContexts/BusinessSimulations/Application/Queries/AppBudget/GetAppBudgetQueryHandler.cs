using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries.AppBudget;

/// <summary>
/// Handler for <see cref="GetAppBudgetQuery"/> (Issue #1838 SP5 F4-C5).
///
/// <para>Returns <c>null</c> when no AppBudget singleton exists yet (first-visit
/// empty state on the admin Business page). Otherwise loads the singleton
/// from <see cref="IAppBudgetRepository"/> and computes the spend KPIs from
/// <c>LedgerEntries</c> on the fly. The DB query reads only the Expense rows
/// for the current calendar month to keep the work bounded; projection is a
/// simple linear extrapolation of month-to-date average daily spend.</para>
/// </summary>
internal sealed class GetAppBudgetQueryHandler : IRequestHandler<GetAppBudgetQuery, AppBudgetDto?>
{
    private readonly IAppBudgetRepository _repository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public GetAppBudgetQueryHandler(
        IAppBudgetRepository repository,
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AppBudgetDto?> Handle(GetAppBudgetQuery request, CancellationToken cancellationToken)
    {
        var budget = await _repository.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        if (budget is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var spent = await ComputeSpendBreakdownAsync(now, cancellationToken).ConfigureAwait(false);
        var daysRemaining = ComputeDaysRemaining(now);

        return new AppBudgetDto(
            Id: budget.Id,
            MonthlyLimit: budget.MonthlyLimit.Amount,
            Currency: budget.MonthlyLimit.Currency,
            AlertThresholdPct: budget.AlertThresholdPct,
            CriticalThresholdPct: budget.CriticalThresholdPct,
            IsEnabled: budget.IsEnabled,
            Spent: spent,
            DaysRemaining: daysRemaining,
            UpdatedAt: budget.UpdatedAt,
            UpdatedBy: budget.UpdatedBy,
            RowVersion: Convert.ToBase64String(budget.RowVersion ?? Array.Empty<byte>()));
    }

    private async Task<SpendBreakdownDto> ComputeSpendBreakdownAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Day boundaries (UTC).
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var todayEnd = todayStart.AddDays(1);

        var thisMonth = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Type == LedgerEntryType.Expense
                        && e.Date >= monthStart
                        && e.Date < todayEnd)
            .SumAsync(e => (decimal?)e.Amount.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var today = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.Type == LedgerEntryType.Expense
                        && e.Date >= todayStart
                        && e.Date < todayEnd)
            .SumAsync(e => (decimal?)e.Amount.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        // Projection: linear extrapolation of month-to-date average daily spend
        // across the full month length. Day 1 falls back to today's spend × month
        // length to avoid divide-by-zero on the first of the month.
        var dayOfMonth = Math.Max(1, now.Day);
        var monthLength = DateTime.DaysInMonth(now.Year, now.Month);
        var projectedMonthEnd = Math.Round(thisMonth / dayOfMonth * monthLength, 2, MidpointRounding.AwayFromZero);

        return new SpendBreakdownDto(
            Today: today,
            ThisMonth: thisMonth,
            ProjectedMonthEnd: projectedMonthEnd);
    }

    private static int ComputeDaysRemaining(DateTime now)
    {
        var monthLength = DateTime.DaysInMonth(now.Year, now.Month);
        return Math.Max(0, monthLength - now.Day);
    }
}

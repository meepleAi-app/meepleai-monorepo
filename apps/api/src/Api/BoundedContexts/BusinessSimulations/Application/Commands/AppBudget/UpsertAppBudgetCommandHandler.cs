using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AppBudgetAggregate = Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets.AppBudget;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;

internal sealed class UpsertAppBudgetCommandHandler
    : IRequestHandler<UpsertAppBudgetCommand, AppBudgetUpsertResult>
{
    private readonly IAppBudgetRepository _repository;

    public UpsertAppBudgetCommandHandler(IAppBudgetRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<AppBudgetUpsertResult> Handle(
        UpsertAppBudgetCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var newLimit = Money.Create(request.MonthlyLimitAmount, request.MonthlyLimitCurrency);

        var existing = await _repository.GetCurrentAsync(cancellationToken).ConfigureAwait(false);

        AppBudgetAggregate aggregate;
        if (existing is null)
        {
            aggregate = AppBudgetAggregate.Create(
                newLimit,
                request.AlertThresholdPct,
                request.CriticalThresholdPct,
                request.UpdatedBy);
        }
        else
        {
            existing.UpdateLimit(
                newLimit,
                request.AlertThresholdPct,
                request.CriticalThresholdPct,
                request.UpdatedBy);

            if (request.Xmin.HasValue)
            {
                // Carry the client-supplied token so the repository can detect a
                // concurrent update via Entry.OriginalValue — UpdateLimit doesn't
                // touch Xmin.
                existing.SetXmin(request.Xmin.Value);
            }
            aggregate = existing;
        }

        try
        {
            await _repository.UpsertAsync(aggregate, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(
                "App budget was modified by another admin. Reload and retry.",
                ex);
        }

        // Re-fetch to surface the freshly-bumped xmin to the client.
        var refreshed = await _repository.GetCurrentAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AppBudget");

        return new AppBudgetUpsertResult(
            Id: refreshed.Id,
            UpdatedAt: refreshed.UpdatedAt,
            Xmin: refreshed.Xmin);
    }
}

using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;

/// <summary>
/// Creates or updates the singleton AppBudget (Issue #1838 SP5 F4-C5).
///
/// <para>The <see cref="Xmin"/> field carries the Postgres xmin token
/// returned by GET; pass <c>null</c> for first-time creation.
/// Returning a stale token on update surfaces as a 409 ConflictException via
/// <see cref="Api.Middleware.Exceptions.ConflictException"/>.</para>
/// </summary>
internal sealed record UpsertAppBudgetCommand(
    decimal MonthlyLimitAmount,
    string MonthlyLimitCurrency,
    int AlertThresholdPct,
    int CriticalThresholdPct,
    uint? Xmin,
    string UpdatedBy) : IRequest<AppBudgetUpsertResult>;

/// <summary>Result returned to the endpoint after a successful upsert.</summary>
internal sealed record AppBudgetUpsertResult(
    Guid Id,
    DateTime UpdatedAt,
    uint Xmin);

internal sealed class UpsertAppBudgetCommandValidator : AbstractValidator<UpsertAppBudgetCommand>
{
    // USD only per MVP — multi-currency tracked as out-of-scope follow-up.
    private static readonly string[] SupportedCurrencies = { "USD" };

    public UpsertAppBudgetCommandValidator()
    {
        RuleFor(x => x.MonthlyLimitAmount)
            .GreaterThan(0m)
            .WithMessage("MonthlyLimitAmount must be greater than zero");

        RuleFor(x => x.MonthlyLimitCurrency)
            .NotEmpty()
            .Must(c => SupportedCurrencies.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage("MonthlyLimitCurrency must be USD (multi-currency is out of scope for MVP)");

        RuleFor(x => x.AlertThresholdPct)
            .InclusiveBetween(1, 99)
            .WithMessage("AlertThresholdPct must be between 1 and 99");

        RuleFor(x => x.CriticalThresholdPct)
            .InclusiveBetween(1, 100)
            .WithMessage("CriticalThresholdPct must be between 1 and 100");

        RuleFor(x => x.CriticalThresholdPct)
            .GreaterThan(x => x.AlertThresholdPct)
            .WithMessage("CriticalThresholdPct must be strictly greater than AlertThresholdPct");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty()
            .MaximumLength(200);
    }
}

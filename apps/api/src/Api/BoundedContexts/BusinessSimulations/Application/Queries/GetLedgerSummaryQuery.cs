using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to get income/expense summary for a date range (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed record GetLedgerSummaryQuery(
    DateTime DateFrom,
    DateTime DateTo) : IQuery<LedgerSummaryDto>;

internal sealed class GetLedgerSummaryQueryValidator : AbstractValidator<GetLedgerSummaryQuery>
{
    public GetLedgerSummaryQueryValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotEmpty()
            .WithMessage("DateFrom is required");

        RuleFor(x => x.DateTo)
            .NotEmpty()
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .WithMessage("DateTo must be after or equal to DateFrom");
    }
}

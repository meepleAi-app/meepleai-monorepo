using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to get a single ledger entry by ID (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed record GetLedgerEntryByIdQuery(Guid Id) : IQuery<LedgerEntryDto>;

internal sealed class GetLedgerEntryByIdQueryValidator : AbstractValidator<GetLedgerEntryByIdQuery>
{
    public GetLedgerEntryByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Ledger entry ID is required");
    }
}

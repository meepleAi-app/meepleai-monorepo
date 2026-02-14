using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to get paginated and filtered ledger entries (Issue #3722: Manual Ledger CRUD)
/// </summary>
internal sealed record GetLedgerEntriesQuery(
    int Page,
    int PageSize,
    LedgerEntryType? Type,
    LedgerCategory? Category,
    LedgerEntrySource? Source,
    DateTime? DateFrom,
    DateTime? DateTo) : IQuery<LedgerEntriesResponseDto>;

internal sealed class GetLedgerEntriesQueryValidator : AbstractValidator<GetLedgerEntriesQuery>
{
    public GetLedgerEntriesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");

        RuleFor(x => x.Type)
            .IsInEnum()
            .When(x => x.Type.HasValue)
            .WithMessage("Type must be a valid LedgerEntryType");

        RuleFor(x => x.Category)
            .IsInEnum()
            .When(x => x.Category.HasValue)
            .WithMessage("Category must be a valid LedgerCategory");

        RuleFor(x => x.Source)
            .IsInEnum()
            .When(x => x.Source.HasValue)
            .WithMessage("Source must be a valid LedgerEntrySource");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo must be after or equal to DateFrom");
    }
}

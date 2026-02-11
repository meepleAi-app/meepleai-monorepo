using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to export ledger entries in CSV, Excel, or PDF format.
/// Issue #3724: Export Ledger (PDF/CSV/Excel) (Epic #3688)
/// </summary>
internal sealed record ExportLedgerQuery(
    LedgerExportFormat Format,
    DateTime? DateFrom,
    DateTime? DateTo,
    LedgerEntryType? Type,
    LedgerCategory? Category) : IQuery<ExportLedgerResult>;

internal sealed record ExportLedgerResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Supported export formats for ledger data
/// </summary>
internal enum LedgerExportFormat
{
    Csv = 0,
    Excel = 1,
    Pdf = 2
}

internal sealed class ExportLedgerQueryValidator : AbstractValidator<ExportLedgerQuery>
{
    public ExportLedgerQueryValidator()
    {
        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("Format must be Csv (0), Excel (1), or Pdf (2)");

        RuleFor(x => x.Type)
            .IsInEnum()
            .When(x => x.Type.HasValue)
            .WithMessage("Type must be a valid LedgerEntryType");

        RuleFor(x => x.Category)
            .IsInEnum()
            .When(x => x.Category.HasValue)
            .WithMessage("Category must be a valid LedgerCategory");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo must be after or equal to DateFrom");
    }
}

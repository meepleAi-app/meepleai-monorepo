using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ExportGamesToExcelCommand.
/// No required parameters — all filters are optional.
/// </summary>
internal sealed class ExportGamesToExcelCommandValidator : AbstractValidator<ExportGamesToExcelCommand>
{
    public ExportGamesToExcelCommandValidator()
    {
        When(x => x.StatusFilter != null, () =>
        {
            RuleFor(x => x.StatusFilter!)
                .Must(list => list.Count > 0)
                .WithMessage("StatusFilter must contain at least one status when provided");
        });
    }
}

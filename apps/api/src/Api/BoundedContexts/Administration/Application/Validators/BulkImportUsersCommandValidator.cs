using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for BulkImportUsersCommand.
/// Hotfix: Code review finding - missing validator
/// </summary>
internal class BulkImportUsersCommandValidator : AbstractValidator<BulkImportUsersCommand>
{
    public BulkImportUsersCommandValidator()
    {
        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.CsvContent)
            .NotNull()
            .NotEmpty()
            .WithMessage("CSV content is required")
            .Must(csv => csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length <= 1001) // Header + 1000 rows
            .WithMessage("CSV cannot exceed 1000 user rows (excluding header)")
            .Must(csv => csv.Contains("email") && csv.Contains(','))
            .WithMessage("CSV must contain header row with 'email' column");
    }
}

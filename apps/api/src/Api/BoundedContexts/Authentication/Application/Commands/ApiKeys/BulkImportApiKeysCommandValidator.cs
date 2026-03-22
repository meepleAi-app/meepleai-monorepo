using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;

/// <summary>
/// Validates BulkImportApiKeysCommand.
/// Ensures CSV content is provided and requester ID is valid.
/// </summary>
internal sealed class BulkImportApiKeysCommandValidator : AbstractValidator<BulkImportApiKeysCommand>
{
    private const int MaxCsvLength = 1_048_576; // 1 MB

    public BulkImportApiKeysCommandValidator()
    {
        RuleFor(x => x.CsvContent)
            .NotEmpty()
            .WithMessage("CSV content is required")
            .MaximumLength(MaxCsvLength)
            .WithMessage($"CSV content must not exceed {MaxCsvLength} characters");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("Requester ID is required");
    }
}

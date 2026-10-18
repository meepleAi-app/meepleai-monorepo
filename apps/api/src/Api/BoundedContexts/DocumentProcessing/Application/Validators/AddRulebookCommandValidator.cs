using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for AddRulebookCommand.
/// Ensures GameId, UserId are non-empty and File is a valid PDF.
/// </summary>
internal sealed class AddRulebookCommandValidator : AbstractValidator<AddRulebookCommand>
{
    public AddRulebookCommandValidator(IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        // #4449: the single PDF size limit (PdfProcessing:MaxFileSizeBytes), not a number of its own.
        var maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("A PDF file is required.");

        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length)
                .GreaterThan(0)
                .WithMessage("File must not be empty.")
                .LessThanOrEqualTo(maxFileSizeBytes)
                .WithMessage($"File size cannot exceed {PdfSizeLimit.Describe(maxFileSizeBytes)}.");

            RuleFor(x => x.File.ContentType)
                .Equal("application/pdf")
                .WithMessage("File must be a PDF.");
        });
    }
}

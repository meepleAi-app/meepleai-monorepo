using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for UploadPdfForGameExtractionCommand.
/// Validates PDF file for wizard metadata extraction: format, size, structure.
/// Issue #4154: Upload PDF Command for Game Metadata Extraction Wizard
/// </summary>
internal sealed class UploadPdfForGameExtractionCommandValidator : AbstractValidator<UploadPdfForGameExtractionCommand>
{
    private const long MaxFileSizeBytes = 52_428_800; // 50 MB
    private const string AllowedContentType = "application/pdf";
    private const string AllowedExtension = ".pdf";

    public UploadPdfForGameExtractionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("PDF file is required.");

        When(x => x.File != null, () =>
        {
            RuleFor(x => x.File!.Length)
                .GreaterThan(0)
                .WithMessage("PDF file cannot be empty.")
                .LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage($"PDF file size cannot exceed {MaxFileSizeBytes / 1024 / 1024} MB.");

            RuleFor(x => x.File!.ContentType)
                .Equal(AllowedContentType)
                .WithMessage($"Only PDF files are allowed. Received content type: {{PropertyValue}}");

            RuleFor(x => x.File!.FileName)
                .Must(HavePdfExtension)
                .WithMessage("File must have a .pdf extension.");
        });
    }

    private static bool HavePdfExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return Path.GetExtension(fileName)
            .Equals(AllowedExtension, StringComparison.OrdinalIgnoreCase);
    }
}

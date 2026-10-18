using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for UploadPdfForGameExtractionCommand.
/// Validates PDF file for wizard metadata extraction: format, size, structure.
/// Issue #4154: Upload PDF Command for Game Metadata Extraction Wizard
/// </summary>
internal sealed class UploadPdfForGameExtractionCommandValidator : AbstractValidator<UploadPdfForGameExtractionCommand>
{
    private const string AllowedContentType = "application/pdf";
    private const string AllowedExtension = ".pdf";

    public UploadPdfForGameExtractionCommandValidator(IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        // #4449: the single PDF size limit (PdfProcessing:MaxFileSizeBytes), not a number of its own.
        var maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;

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
                .LessThanOrEqualTo(maxFileSizeBytes)
                .WithMessage($"PDF file size cannot exceed {PdfSizeLimit.Describe(maxFileSizeBytes)}.");

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

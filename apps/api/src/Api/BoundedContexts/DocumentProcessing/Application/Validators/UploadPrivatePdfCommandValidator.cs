using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for UploadPrivatePdfCommand.
/// Issue #3479: Private PDF Upload Endpoint
/// </summary>
internal sealed class UploadPrivatePdfCommandValidator : AbstractValidator<UploadPrivatePdfCommand>
{
    private const string AllowedContentType = "application/pdf";
    private const string AllowedExtension = ".pdf";

    public UploadPrivatePdfCommandValidator(IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        // #4449: the single PDF size limit (PdfProcessing:MaxFileSizeBytes), not a number of its own.
        var maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.UserLibraryEntryId)
            .NotEmpty()
            .WithMessage("User library entry ID is required.");

        RuleFor(x => x.PdfFile)
            .NotNull()
            .WithMessage("PDF file is required.");

        When(x => x.PdfFile != null, () =>
        {
            RuleFor(x => x.PdfFile.Length)
                .GreaterThan(0)
                .WithMessage("PDF file cannot be empty.")
                .LessThanOrEqualTo(maxFileSizeBytes)
                .WithMessage($"PDF file size cannot exceed {PdfSizeLimit.Describe(maxFileSizeBytes)}.");

            RuleFor(x => x.PdfFile.ContentType)
                .Equal(AllowedContentType)
                .WithMessage($"Only PDF files are allowed. Received content type: {{PropertyValue}}");

            RuleFor(x => x.PdfFile.FileName)
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

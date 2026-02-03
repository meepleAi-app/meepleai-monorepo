using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for UploadPrivatePdfCommand.
/// Issue #3479: Private PDF Upload Endpoint
/// </summary>
internal sealed class UploadPrivatePdfCommandValidator : AbstractValidator<UploadPrivatePdfCommand>
{
    private const long MaxFileSizeBytes = 52_428_800; // 50 MB
    private const string AllowedContentType = "application/pdf";
    private const string AllowedExtension = ".pdf";

    public UploadPrivatePdfCommandValidator()
    {
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
                .LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage($"PDF file size cannot exceed {MaxFileSizeBytes / 1024 / 1024} MB.");

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

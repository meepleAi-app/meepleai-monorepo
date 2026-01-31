using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for UpdatePdfUploadLimitsCommand.
/// Enforces reasonable limits for PDF upload configuration.
/// Issue #3072: PDF Upload Limits - Admin API
/// </summary>
internal sealed class UpdatePdfUploadLimitsCommandValidator : AbstractValidator<UpdatePdfUploadLimitsCommand>
{
    // File size limits: 1MB to 500MB
    private const long MinFileSizeBytes = 1048576; // 1MB
    private const long MaxFileSizeBytes = 524288000; // 500MB

    // Page limits: 1 to 2000 pages
    private const int MinPages = 1;
    private const int MaxPages = 2000;

    // Document limits: 1 to 100 documents per game
    private const int MinDocuments = 1;
    private const int MaxDocuments = 100;

    // Valid PDF MIME types
    private static readonly string[] ValidPdfMimeTypes =
    [
        "application/pdf",
        "application/x-pdf"
    ];

    public UpdatePdfUploadLimitsCommandValidator()
    {
        RuleFor(x => x.MaxFileSizeBytes)
            .InclusiveBetween(MinFileSizeBytes, MaxFileSizeBytes)
            .WithMessage($"Maximum file size must be between {MinFileSizeBytes / 1048576}MB and {MaxFileSizeBytes / 1048576}MB");

        RuleFor(x => x.MaxPagesPerDocument)
            .InclusiveBetween(MinPages, MaxPages)
            .WithMessage($"Maximum pages per document must be between {MinPages} and {MaxPages}");

        RuleFor(x => x.MaxDocumentsPerGame)
            .InclusiveBetween(MinDocuments, MaxDocuments)
            .WithMessage($"Maximum documents per game must be between {MinDocuments} and {MaxDocuments}");

        RuleFor(x => x.AllowedMimeTypes)
            .NotEmpty()
            .WithMessage("At least one MIME type must be specified");

        RuleForEach(x => x.AllowedMimeTypes)
            .Must(BeValidPdfMimeType)
            .WithMessage("'{PropertyValue}' is not a valid PDF MIME type. Allowed types: application/pdf, application/x-pdf");

        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty()
            .WithMessage("UpdatedByUserId is required");
    }

    private static bool BeValidPdfMimeType(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
            return false;

        return Array.Exists(ValidPdfMimeTypes, t => t.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
    }
}

using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UploadSharedGamePdfCommand.
/// Issue #4922
/// </summary>
internal sealed class UploadSharedGamePdfCommandValidator : AbstractValidator<UploadSharedGamePdfCommand>
{
    public UploadSharedGamePdfCommandValidator(IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        // #4449: the single PDF size limit (PdfProcessing:MaxFileSizeBytes), not a number of its own.
        var maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.SharedGameId)
            .NotEmpty().WithMessage("SharedGameId is required.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required.");

        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(maxFileSizeBytes)
            .WithMessage($"File size cannot exceed {PdfSizeLimit.Describe(maxFileSizeBytes)}.")
            .When(x => x.File is not null);

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required.")
            .MaximumLength(50).WithMessage("Version cannot exceed 50 characters.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("AdminUserId is required.");
    }
}

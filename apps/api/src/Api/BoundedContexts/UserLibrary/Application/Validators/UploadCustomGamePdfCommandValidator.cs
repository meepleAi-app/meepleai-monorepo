using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UploadCustomGamePdfCommand.
/// </summary>
internal sealed class UploadCustomGamePdfCommandValidator : AbstractValidator<UploadCustomGamePdfCommand>
{
    private const long MaxFileSizeBytes = 100_000_000; // 100 MB

    public UploadCustomGamePdfCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.PdfUrl)
            .NotEmpty()
            .WithMessage("PDF URL is required")
            .Must(BeValidUrl)
            .WithMessage("Invalid PDF URL format");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File size must be greater than 0")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File size cannot exceed {MaxFileSizeBytes / 1_000_000} MB");

        RuleFor(x => x.OriginalFileName)
            .NotEmpty()
            .WithMessage("Original file name is required")
            .MaximumLength(255)
            .WithMessage("File name cannot exceed 255 characters")
            .Must(BeValidFileName)
            .WithMessage("Invalid file name format");
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _) || Uri.TryCreate(url, UriKind.Relative, out _);
    }

    private static bool BeValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var invalidChars = Path.GetInvalidFileNameChars();
        return !fileName.Any(c => invalidChars.Contains(c));
    }
}

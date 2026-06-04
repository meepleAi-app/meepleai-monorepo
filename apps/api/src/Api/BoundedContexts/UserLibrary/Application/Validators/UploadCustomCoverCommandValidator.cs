using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UploadCustomCoverCommand.
/// Enforces: GameId/UserId not empty, FileSize > 0 AND ≤ 10MB, MIME in whitelist.
/// </summary>
internal sealed class UploadCustomCoverCommandValidator : AbstractValidator<UploadCustomCoverCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic"
    };

    public UploadCustomCoverCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty().WithMessage("UserId is required");
        RuleFor(c => c.GameId).NotEmpty().WithMessage("GameId is required");
        RuleFor(c => c.FileSizeBytes)
            .GreaterThan(0).WithMessage("File cannot be empty")
            .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage($"File size cannot exceed {MaxFileSizeBytes} bytes (10MB)");
        RuleFor(c => c.MimeType)
            .NotEmpty().WithMessage("MIME type is required")
            .Must(mime => AllowedMimeTypes.Contains(mime))
            .WithMessage($"MIME type must be one of: {string.Join(", ", AllowedMimeTypes)}");
    }
}

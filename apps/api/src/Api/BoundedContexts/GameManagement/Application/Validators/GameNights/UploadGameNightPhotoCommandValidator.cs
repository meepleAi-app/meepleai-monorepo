using System;
using System.Collections.Generic;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

internal sealed class UploadGameNightPhotoCommandValidator : AbstractValidator<UploadGameNightPhotoCommand>
{
    internal const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB (mirrors PlayRecord photo cap)
    private static readonly HashSet<string> AllowedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public UploadGameNightPhotoCommandValidator()
    {
        RuleFor(c => c.GameNightId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.FileSizeBytes)
            .GreaterThan(0).WithMessage("File cannot be empty")
            .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage("File cannot exceed 5MB");
        RuleFor(c => c.MimeType)
            .NotEmpty()
            .Must(m => AllowedMimeTypes.Contains(m))
            .WithMessage("Only JPEG, PNG, and WebP images are allowed");
        RuleFor(c => c.Caption).MaximumLength(500).When(c => c.Caption != null);
    }
}

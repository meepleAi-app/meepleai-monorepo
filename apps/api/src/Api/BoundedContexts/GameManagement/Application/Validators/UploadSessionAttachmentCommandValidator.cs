using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// FluentValidation validator for UploadSessionAttachmentCommand.
/// Issue #5362 - UploadSessionAttachment CQRS command.
/// </summary>
internal sealed class UploadSessionAttachmentCommandValidator : AbstractValidator<UploadSessionAttachmentCommand>
{
    public UploadSessionAttachmentCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage("Player ID is required.");

        RuleFor(x => x.ContentType)
            .Must(ct => ct is "image/jpeg" or "image/png")
            .WithMessage("Only image/jpeg and image/png are allowed.");

        RuleFor(x => x.FileSizeBytes)
            .InclusiveBetween(1024, 10_485_760)
            .WithMessage("File size must be between 1KB and 10MB.");

        RuleFor(x => x.Caption)
            .MaximumLength(200)
            .When(x => x.Caption != null)
            .WithMessage("Caption cannot exceed 200 characters.");

        RuleFor(x => x.AttachmentType)
            .IsInEnum()
            .WithMessage("Invalid attachment type.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required.");
    }
}

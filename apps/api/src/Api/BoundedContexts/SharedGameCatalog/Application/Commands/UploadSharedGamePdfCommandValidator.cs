using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UploadSharedGamePdfCommand.
/// Issue #4922
/// </summary>
internal sealed class UploadSharedGamePdfCommandValidator : AbstractValidator<UploadSharedGamePdfCommand>
{
    public UploadSharedGamePdfCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty().WithMessage("SharedGameId is required.");

        RuleFor(x => x.File)
            .NotNull().WithMessage("File is required.");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required.")
            .MaximumLength(50).WithMessage("Version cannot exceed 50 characters.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("AdminUserId is required.");
    }
}

using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for GenerateRuleSpecFromPdfCommand.
/// Ensures PdfDocumentId is a non-empty GUID.
/// </summary>
internal sealed class GenerateRuleSpecFromPdfCommandValidator : AbstractValidator<GenerateRuleSpecFromPdfCommand>
{
    public GenerateRuleSpecFromPdfCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty().WithMessage("PDF document ID is required");
    }
}

/// <summary>
/// Validator for RefreshEditorLockCommand.
/// Ensures GameId and UserId are non-empty GUIDs.
/// </summary>
internal sealed class RefreshEditorLockCommandValidator : AbstractValidator<RefreshEditorLockCommand>
{
    public RefreshEditorLockCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>
/// Validator for ReleaseEditorLockCommand.
/// Ensures GameId and UserId are non-empty GUIDs.
/// </summary>
internal sealed class ReleaseEditorLockCommandValidator : AbstractValidator<ReleaseEditorLockCommand>
{
    public ReleaseEditorLockCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>
/// Validator for UpdateRuleSpecCommand.
/// Ensures GameId and UserId are non-empty, Atoms list is provided and within bounds.
/// </summary>
internal sealed class UpdateRuleSpecCommandValidator : AbstractValidator<UpdateRuleSpecCommand>
{
    public UpdateRuleSpecCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.Version)
            .MaximumLength(200).WithMessage("Version must not exceed 200 characters")
            .When(x => x.Version is not null);

        RuleFor(x => x.Atoms)
            .NotEmpty().WithMessage("At least one rule atom is required")
            .Must(a => a.Count <= 500).WithMessage("Atoms list must not exceed 500 entries")
            .When(x => x.Atoms is not null);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.ExpectedETag)
            .MaximumLength(200).WithMessage("Expected ETag must not exceed 200 characters")
            .When(x => x.ExpectedETag is not null);
    }
}

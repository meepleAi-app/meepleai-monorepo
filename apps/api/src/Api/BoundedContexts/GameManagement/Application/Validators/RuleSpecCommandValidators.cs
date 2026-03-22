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

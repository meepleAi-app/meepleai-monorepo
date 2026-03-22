using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ActivatePromptVersionCommand.
/// Ensures template, version, and user IDs are valid.
/// </summary>
internal sealed class ActivatePromptVersionCommandValidator : AbstractValidator<ActivatePromptVersionCommand>
{
    public ActivatePromptVersionCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("TemplateId is required");

        RuleFor(x => x.VersionId)
            .NotEmpty()
            .WithMessage("VersionId is required");

        RuleFor(x => x.ActivatedByUserId)
            .NotEmpty()
            .WithMessage("ActivatedByUserId is required");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters")
            .When(x => x.Reason != null);
    }
}

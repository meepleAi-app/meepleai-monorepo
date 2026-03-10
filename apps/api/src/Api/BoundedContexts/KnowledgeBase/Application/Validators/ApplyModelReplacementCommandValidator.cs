using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ApplyModelReplacementCommand.
/// Issue #5499: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class ApplyModelReplacementCommandValidator
    : AbstractValidator<ApplyModelReplacementCommand>
{
    public ApplyModelReplacementCommandValidator()
    {
        RuleFor(x => x.DeprecatedModelId)
            .NotEmpty().WithMessage("Deprecated model ID is required")
            .MaximumLength(200).WithMessage("Deprecated model ID must not exceed 200 characters");

        RuleFor(x => x.ReplacementModelId)
            .NotEmpty().WithMessage("Replacement model ID is required")
            .MaximumLength(200).WithMessage("Replacement model ID must not exceed 200 characters");

        RuleFor(x => x)
            .Must(x => !string.Equals(x.DeprecatedModelId, x.ReplacementModelId, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Replacement model must be different from the deprecated model");
    }
}

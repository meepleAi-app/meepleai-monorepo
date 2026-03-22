using Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for EvaluatePromptCommand.
/// Ensures template ID, version ID, and dataset path are provided.
/// </summary>
internal sealed class EvaluatePromptCommandValidator : AbstractValidator<EvaluatePromptCommand>
{
    public EvaluatePromptCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("TemplateId is required");

        RuleFor(x => x.VersionId)
            .NotEmpty()
            .WithMessage("VersionId is required");

        RuleFor(x => x.DatasetPath)
            .NotEmpty()
            .WithMessage("DatasetPath is required")
            .MaximumLength(500)
            .WithMessage("DatasetPath must not exceed 500 characters");
    }
}

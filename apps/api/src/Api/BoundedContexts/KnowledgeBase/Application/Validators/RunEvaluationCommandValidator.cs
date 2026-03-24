using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for RunEvaluationCommand.
/// </summary>
internal sealed class RunEvaluationCommandValidator : AbstractValidator<RunEvaluationCommand>
{
    public RunEvaluationCommandValidator()
    {
        RuleFor(x => x.DatasetPath)
            .NotEmpty()
            .WithMessage("DatasetPath is required")
            .MaximumLength(500)
            .WithMessage("DatasetPath cannot exceed 500 characters");

        RuleFor(x => x.Configuration)
            .NotEmpty()
            .WithMessage("Configuration is required")
            .MaximumLength(200)
            .WithMessage("Configuration cannot exceed 200 characters");

        RuleFor(x => x.MaxSamples)
            .GreaterThan(0)
            .When(x => x.MaxSamples.HasValue)
            .WithMessage("MaxSamples must be greater than 0");

        RuleFor(x => x.TopK)
            .GreaterThan(0)
            .WithMessage("TopK must be greater than 0");
    }
}

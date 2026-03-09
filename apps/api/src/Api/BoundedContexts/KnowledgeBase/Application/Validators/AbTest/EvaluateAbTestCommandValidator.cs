using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AbTest;

/// <summary>
/// Validator for EvaluateAbTestCommand.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed class EvaluateAbTestCommandValidator : AbstractValidator<EvaluateAbTestCommand>
{
    public EvaluateAbTestCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId is required");

        RuleFor(x => x.EvaluatorId)
            .NotEmpty().WithMessage("EvaluatorId is required");

        RuleFor(x => x.Evaluations)
            .NotEmpty().WithMessage("At least one evaluation is required");

        RuleForEach(x => x.Evaluations)
            .ChildRules(eval =>
            {
                eval.RuleFor(e => e.Label)
                    .NotEmpty().WithMessage("Variant label is required");

                eval.RuleFor(e => e.Accuracy)
                    .InclusiveBetween(1, 5).WithMessage("Accuracy must be between 1 and 5");

                eval.RuleFor(e => e.Completeness)
                    .InclusiveBetween(1, 5).WithMessage("Completeness must be between 1 and 5");

                eval.RuleFor(e => e.Clarity)
                    .InclusiveBetween(1, 5).WithMessage("Clarity must be between 1 and 5");

                eval.RuleFor(e => e.Tone)
                    .InclusiveBetween(1, 5).WithMessage("Tone must be between 1 and 5");

                eval.RuleFor(e => e.Notes)
                    .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
                    .When(e => e.Notes is not null);
            });
    }
}

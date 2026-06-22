using FluentValidation;

namespace Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;

public sealed class StartEvaluationCommandValidator : AbstractValidator<StartEvaluationCommand>
{
    public StartEvaluationCommandValidator()
    {
        RuleFor(x => x.DocId).NotEmpty();
        RuleFor(x => x.GoldsetVersion)
            .MaximumLength(64)
            .When(x => x.GoldsetVersion is not null);
    }
}

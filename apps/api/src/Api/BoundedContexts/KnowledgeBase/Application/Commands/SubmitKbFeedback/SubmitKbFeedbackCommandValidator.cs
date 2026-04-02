using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed class SubmitKbFeedbackCommandValidator : AbstractValidator<SubmitKbFeedbackCommand>
{
    public SubmitKbFeedbackCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.ChatSessionId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.Outcome)
            .NotEmpty()
            .Must(o => o is "helpful" or "not_helpful")
            .WithMessage("Outcome must be 'helpful' or 'not_helpful'");
        RuleFor(x => x.Comment)
            .MaximumLength(500)
            .When(x => x.Comment is not null);
    }
}

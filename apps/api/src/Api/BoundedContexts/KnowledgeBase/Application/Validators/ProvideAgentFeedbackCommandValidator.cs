using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ProvideAgentFeedbackCommand.
/// </summary>
internal sealed class ProvideAgentFeedbackCommandValidator : AbstractValidator<ProvideAgentFeedbackCommand>
{
    public ProvideAgentFeedbackCommandValidator()
    {
        RuleFor(x => x.MessageId)
            .NotEmpty()
            .WithMessage("MessageId is required");

        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .WithMessage("Endpoint is required")
            .MaximumLength(200)
            .WithMessage("Endpoint cannot exceed 200 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Outcome)
            .MaximumLength(200)
            .When(x => x.Outcome != null)
            .WithMessage("Outcome cannot exceed 200 characters");

        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .When(x => x.Comment != null)
            .WithMessage("Comment cannot exceed 2000 characters");
    }
}

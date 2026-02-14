using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SendAgentMessageCommand
/// Issue #4126
/// </summary>
internal sealed class SendAgentMessageCommandValidator : AbstractValidator<SendAgentMessageCommand>
{
    public SendAgentMessageCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("Agent ID is required");

        RuleFor(x => x.UserQuestion)
            .NotEmpty()
            .WithMessage("User question cannot be empty")
            .MaximumLength(2000)
            .WithMessage("Question must be 2000 characters or less");
    }
}

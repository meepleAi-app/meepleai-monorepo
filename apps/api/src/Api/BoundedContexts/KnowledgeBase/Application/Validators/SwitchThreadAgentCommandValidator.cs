using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SwitchThreadAgentCommand.
/// </summary>
internal sealed class SwitchThreadAgentCommandValidator : AbstractValidator<SwitchThreadAgentCommand>
{
    public SwitchThreadAgentCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("ThreadId is required");

        RuleFor(x => x.AgentType)
            .NotEmpty()
            .WithMessage("AgentType is required")
            .MaximumLength(200)
            .WithMessage("AgentType cannot exceed 200 characters");
    }
}

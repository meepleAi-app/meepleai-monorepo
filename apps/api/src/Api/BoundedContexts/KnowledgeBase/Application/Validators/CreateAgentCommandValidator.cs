using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateAgentCommand.
/// </summary>
internal sealed class CreateAgentCommandValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.AgentType)
            .NotEmpty()
            .WithMessage("AgentType is required")
            .MaximumLength(200)
            .WithMessage("AgentType cannot exceed 200 characters");

        RuleFor(x => x.StrategyName)
            .NotEmpty()
            .WithMessage("StrategyName is required")
            .MaximumLength(200)
            .WithMessage("StrategyName cannot exceed 200 characters");

        RuleFor(x => x.StrategyParameters)
            .NotNull()
            .WithMessage("StrategyParameters is required");
    }
}

using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ConfigureAgentCommand.
/// </summary>
internal sealed class ConfigureAgentCommandValidator : AbstractValidator<ConfigureAgentCommand>
{
    public ConfigureAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("AgentId is required");

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

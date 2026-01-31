using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ProposeAgentTypologyCommand.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal sealed class ProposeAgentTypologyCommandValidator : AbstractValidator<ProposeAgentTypologyCommand>
{
    public ProposeAgentTypologyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Typology name is required")
            .MaximumLength(200).WithMessage("Typology name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.BasePrompt)
            .NotEmpty().WithMessage("Base prompt is required")
            .MinimumLength(10).WithMessage("Base prompt must be at least 10 characters");

        RuleFor(x => x.DefaultStrategyName)
            .NotEmpty().WithMessage("Default strategy name is required")
            .MaximumLength(100).WithMessage("Strategy name must not exceed 100 characters");

        RuleFor(x => x.DefaultStrategyParameters)
            .NotNull().WithMessage("Default strategy parameters are required");

        RuleFor(x => x.ProposedBy)
            .NotEmpty().WithMessage("ProposedBy user ID is required");
    }
}

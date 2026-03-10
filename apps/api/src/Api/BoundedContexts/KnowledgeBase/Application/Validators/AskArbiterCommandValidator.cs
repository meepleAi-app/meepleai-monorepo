using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for AskArbiterCommand.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
internal sealed class AskArbiterCommandValidator : AbstractValidator<AskArbiterCommand>
{
    public AskArbiterCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("Agent ID is required");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.Situation)
            .NotEmpty()
            .WithMessage("Situation description cannot be empty")
            .MaximumLength(2000)
            .WithMessage("Situation must be 2000 characters or less");

        RuleFor(x => x.PositionA)
            .NotEmpty()
            .WithMessage("Position A cannot be empty")
            .MaximumLength(1000)
            .WithMessage("Position A must be 1000 characters or less");

        RuleFor(x => x.PositionB)
            .NotEmpty()
            .WithMessage("Position B cannot be empty")
            .MaximumLength(1000)
            .WithMessage("Position B must be 1000 characters or less");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}

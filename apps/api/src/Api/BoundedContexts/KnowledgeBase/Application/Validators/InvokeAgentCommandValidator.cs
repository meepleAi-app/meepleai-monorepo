using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for InvokeAgentCommand.
/// </summary>
internal sealed class InvokeAgentCommandValidator : AbstractValidator<InvokeAgentCommand>
{
    public InvokeAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("AgentId is required");

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required")
            .MaximumLength(2000)
            .WithMessage("Query cannot exceed 2000 characters");

        RuleFor(x => x.UserRole)
            .MaximumLength(50)
            .When(x => x.UserRole != null)
            .WithMessage("UserRole cannot exceed 50 characters");
    }
}

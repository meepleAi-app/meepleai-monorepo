using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateAgentSessionDefinitionCommand.
/// </summary>
internal sealed class UpdateAgentSessionDefinitionCommandValidator : AbstractValidator<UpdateAgentSessionDefinitionCommand>
{
    public UpdateAgentSessionDefinitionCommandValidator()
    {
        RuleFor(x => x.AgentSessionId)
            .NotEqual(Guid.Empty).WithMessage("AgentSessionId is required");

        RuleFor(x => x.NewAgentDefinitionId)
            .NotEqual(Guid.Empty).WithMessage("NewAgentDefinitionId is required");
    }
}

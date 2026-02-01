using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateAgentSessionTypologyCommand.
/// Issue #3252 (BACK-AGT-001): PATCH Endpoint - Update Agent Typology.
/// </summary>
internal sealed class UpdateAgentSessionTypologyCommandValidator : AbstractValidator<UpdateAgentSessionTypologyCommand>
{
    public UpdateAgentSessionTypologyCommandValidator()
    {
        RuleFor(x => x.AgentSessionId)
            .NotEqual(Guid.Empty).WithMessage("AgentSessionId is required");

        RuleFor(x => x.NewTypologyId)
            .NotEqual(Guid.Empty).WithMessage("NewTypologyId is required");
    }
}

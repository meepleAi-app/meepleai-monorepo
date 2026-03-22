using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;

/// <summary>
/// Validator for DeleteAgentDefinitionCommand.
/// </summary>
internal sealed class DeleteAgentDefinitionCommandValidator : AbstractValidator<DeleteAgentDefinitionCommand>
{
    public DeleteAgentDefinitionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}

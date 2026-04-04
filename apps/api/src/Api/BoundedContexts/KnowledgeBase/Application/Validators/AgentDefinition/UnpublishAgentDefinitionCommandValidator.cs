using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;

/// <summary>
/// Validator for UnpublishAgentDefinitionCommand.
/// </summary>
internal sealed class UnpublishAgentDefinitionCommandValidator : AbstractValidator<UnpublishAgentDefinitionCommand>
{
    public UnpublishAgentDefinitionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}

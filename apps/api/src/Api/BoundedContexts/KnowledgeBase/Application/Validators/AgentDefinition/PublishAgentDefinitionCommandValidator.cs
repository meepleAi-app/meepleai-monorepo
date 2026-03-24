using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;

/// <summary>
/// Validator for PublishAgentDefinitionCommand.
/// </summary>
internal sealed class PublishAgentDefinitionCommandValidator : AbstractValidator<PublishAgentDefinitionCommand>
{
    public PublishAgentDefinitionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}

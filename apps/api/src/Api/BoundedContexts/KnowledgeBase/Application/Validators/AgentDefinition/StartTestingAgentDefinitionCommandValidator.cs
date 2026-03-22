using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;

/// <summary>
/// Validator for StartTestingAgentDefinitionCommand.
/// </summary>
internal sealed class StartTestingAgentDefinitionCommandValidator : AbstractValidator<StartTestingAgentDefinitionCommand>
{
    public StartTestingAgentDefinitionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}

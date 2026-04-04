using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for LinkUserAgentDocumentsCommand.
/// </summary>
internal sealed class LinkUserAgentDocumentsCommandValidator : AbstractValidator<LinkUserAgentDocumentsCommand>
{
    public LinkUserAgentDocumentsCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.AgentDefinitionId)
            .NotEmpty()
            .WithMessage("AgentDefinitionId is required");
    }
}

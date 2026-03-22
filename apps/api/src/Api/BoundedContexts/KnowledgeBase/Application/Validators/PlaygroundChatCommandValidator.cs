using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for PlaygroundChatCommand.
/// </summary>
internal sealed class PlaygroundChatCommandValidator : AbstractValidator<PlaygroundChatCommand>
{
    public PlaygroundChatCommandValidator()
    {
        RuleFor(x => x.AgentDefinitionId)
            .NotEmpty()
            .WithMessage("AgentDefinitionId is required");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required")
            .MaximumLength(2000)
            .WithMessage("Message cannot exceed 2000 characters");

        RuleFor(x => x.Strategy)
            .MaximumLength(200)
            .When(x => x.Strategy != null)
            .WithMessage("Strategy cannot exceed 200 characters");

        RuleFor(x => x.ModelOverride)
            .MaximumLength(200)
            .When(x => x.ModelOverride != null)
            .WithMessage("ModelOverride cannot exceed 200 characters");

        RuleFor(x => x.ProviderOverride)
            .MaximumLength(200)
            .When(x => x.ProviderOverride != null)
            .WithMessage("ProviderOverride cannot exceed 200 characters");
    }
}

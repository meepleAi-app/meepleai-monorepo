using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;

/// <summary>
/// Validator for UpdateAgentDefinitionCommand.
/// Issue #3808 (Epic #3687)
/// Issue #3708: Extended with Type field validation.
/// </summary>
internal sealed class UpdateAgentDefinitionCommandValidator : AbstractValidator<UpdateAgentDefinitionCommand>
{
    private static readonly string[] s_allowedRoles = { "system", "user", "assistant", "function" };
    private static readonly string[] s_allowedTypes = { "RAG", "Citation", "Confidence", "RulesInterpreter", "Conversation" };

    public UpdateAgentDefinitionCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required")
            .Must(t => AgentType.TryParse(t, out _))
            .WithMessage($"Type must be one of: {string.Join(", ", s_allowedTypes)}, or a custom value");

        RuleFor(x => x.StrategyName)
            .MaximumLength(100).WithMessage("StrategyName must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.StrategyName));

        RuleFor(x => x.StrategyParameters)
            .Must(p => p == null || p.Count <= 50).WithMessage("StrategyParameters cannot have more than 50 entries")
            .When(x => x.StrategyParameters != null);

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required")
            .MaximumLength(200).WithMessage("Model must not exceed 200 characters");

        RuleFor(x => x.MaxTokens)
            .InclusiveBetween(100, 32000).WithMessage("MaxTokens must be between 100 and 32000");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0f, 2.0f).WithMessage("Temperature must be between 0.0 and 2.0");

        RuleFor(x => x.Prompts)
            .Must(p => p == null || p.Count <= 20).WithMessage("Cannot have more than 20 prompts");

        RuleForEach(x => x.Prompts)
            .ChildRules(prompt =>
            {
                prompt.RuleFor(p => p.Role)
                    .NotEmpty().WithMessage("Prompt role is required")
                    .Must(r => s_allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
                    .WithMessage("Prompt role must be one of: system, user, assistant, function");

                prompt.RuleFor(p => p.Content)
                    .NotEmpty().WithMessage("Prompt content is required")
                    .MaximumLength(10000).WithMessage("Prompt content must not exceed 10000 characters");
            });

        RuleFor(x => x.Tools)
            .Must(t => t == null || t.Count <= 50).WithMessage("Cannot have more than 50 tools");

        RuleForEach(x => x.Tools)
            .ChildRules(tool =>
            {
                tool.RuleFor(t => t.Name)
                    .NotEmpty().WithMessage("Tool name is required")
                    .MaximumLength(100).WithMessage("Tool name must not exceed 100 characters");
            });
    }
}

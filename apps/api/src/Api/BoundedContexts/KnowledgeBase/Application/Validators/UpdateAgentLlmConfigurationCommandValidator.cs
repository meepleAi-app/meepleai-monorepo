using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateAgentLlmConfigurationCommand.
/// Validates field ranges when provided (partial update).
/// </summary>
internal sealed class UpdateAgentLlmConfigurationCommandValidator
    : AbstractValidator<UpdateAgentLlmConfigurationCommand>
{
    public UpdateAgentLlmConfigurationCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("AgentId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.ModelId)
            .MaximumLength(200)
            .WithMessage("ModelId cannot exceed 200 characters")
            .When(x => x.ModelId != null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0m, 2.0m)
            .WithMessage("Temperature must be between 0.0 and 2.0")
            .When(x => x.Temperature.HasValue);

        RuleFor(x => x.MaxTokens)
            .InclusiveBetween(1, 32000)
            .WithMessage("MaxTokens must be between 1 and 32000")
            .When(x => x.MaxTokens.HasValue);
    }
}

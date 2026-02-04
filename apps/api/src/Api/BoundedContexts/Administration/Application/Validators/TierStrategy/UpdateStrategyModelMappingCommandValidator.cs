using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.TierStrategy;

/// <summary>
/// Validator for UpdateStrategyModelMappingCommand.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class UpdateStrategyModelMappingCommandValidator : AbstractValidator<UpdateStrategyModelMappingCommand>
{
    private static readonly string[] ValidProviders = { "OpenRouter", "Anthropic", "DeepSeek", "Mixed", "Ollama" };

    public UpdateStrategyModelMappingCommandValidator()
    {
        RuleFor(x => x.Strategy)
            .NotEmpty()
            .WithMessage("Strategy is required")
            .Must(strategy => RagStrategyExtensions.TryParse(strategy, out _))
            .WithMessage("Invalid strategy. Valid strategies are: FAST, BALANCED, PRECISE, EXPERT, CONSENSUS, CUSTOM");

        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("Provider is required")
            .Must(provider => ValidProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Invalid provider. Valid providers are: {string.Join(", ", ValidProviders)}");

        RuleFor(x => x.PrimaryModel)
            .NotEmpty()
            .WithMessage("Primary model is required")
            .MaximumLength(200)
            .WithMessage("Primary model must be 200 characters or less");

        RuleForEach(x => x.FallbackModels)
            .MaximumLength(200)
            .WithMessage("Each fallback model must be 200 characters or less")
            .When(x => x.FallbackModels != null && x.FallbackModels.Any());
    }
}

using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// Validator for CreateAiModelCommand
/// </summary>
/// <remarks>
/// Issue #2567: Validation rules for AI model creation
/// </remarks>
internal sealed class CreateAiModelCommandValidator : AbstractValidator<CreateAiModelCommand>
{
    private static readonly string[] ValidProviders = { "OpenRouter", "Ollama", "Azure", "OpenAI" };

    public CreateAiModelCommandValidator()
    {
        RuleFor(x => x.ModelId)
            .NotEmpty()
            .WithMessage("ModelId is required")
            .MaximumLength(200)
            .WithMessage("ModelId must not exceed 200 characters");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("DisplayName is required")
            .MaximumLength(200)
            .WithMessage("DisplayName must not exceed 200 characters");

        RuleFor(x => x.Provider)
            .NotEmpty()
            .WithMessage("Provider is required")
            .Must(p => ValidProviders.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Provider must be one of: {string.Join(", ", ValidProviders)}");

        RuleFor(x => x.Priority)
            .GreaterThan(0)
            .WithMessage("Priority must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Priority must not exceed 100");

        RuleFor(x => x.Settings)
            .NotNull()
            .WithMessage("Settings are required");

        RuleFor(x => x.Settings.MaxTokens)
            .GreaterThan(0)
            .WithMessage("MaxTokens must be greater than 0")
            .LessThanOrEqualTo(32000)
            .WithMessage("MaxTokens must not exceed 32000")
            .When(x => x.Settings != null);

        RuleFor(x => x.Settings.Temperature)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Temperature must be >= 0")
            .LessThanOrEqualTo(2)
            .WithMessage("Temperature must be <= 2")
            .When(x => x.Settings != null);
    }
}

using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for ConfigureGameAgentCommand.
/// </summary>
internal sealed class ConfigureGameAgentCommandValidator : AbstractValidator<ConfigureGameAgentCommand>
{
    private static readonly HashSet<string> ValidPersonalities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Amichevole", "Professionale", "Umoristico", "Conciso", "Dettagliato"
    };

    private static readonly HashSet<string> ValidDetailLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Breve", "Normale", "Dettagliato", "Esaustivo"
    };

    public ConfigureGameAgentCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.AgentConfig)
            .NotNull()
            .WithMessage("AgentConfig is required");

        When(x => x.AgentConfig != null, () =>
        {
            RuleFor(x => x.AgentConfig.LlmModel)
                .NotEmpty()
                .WithMessage("LlmModel is required")
                .MaximumLength(100)
                .WithMessage("LlmModel cannot exceed 100 characters");

            RuleFor(x => x.AgentConfig.Temperature)
                .InclusiveBetween(0.0, 2.0)
                .WithMessage("Temperature must be between 0.0 and 2.0");

            RuleFor(x => x.AgentConfig.MaxTokens)
                .InclusiveBetween(100, 32000)
                .WithMessage("MaxTokens must be between 100 and 32000");

            RuleFor(x => x.AgentConfig.Personality)
                .NotEmpty()
                .WithMessage("Personality is required")
                .Must(p => ValidPersonalities.Contains(p))
                .WithMessage($"Personality must be one of: {string.Join(", ", ValidPersonalities)}");

            RuleFor(x => x.AgentConfig.DetailLevel)
                .NotEmpty()
                .WithMessage("DetailLevel is required")
                .Must(d => ValidDetailLevels.Contains(d))
                .WithMessage($"DetailLevel must be one of: {string.Join(", ", ValidDetailLevels)}");

            RuleFor(x => x.AgentConfig.PersonalNotes)
                .MaximumLength(1000)
                .WithMessage("PersonalNotes cannot exceed 1000 characters")
                .When(x => x.AgentConfig.PersonalNotes != null);
        });
    }
}

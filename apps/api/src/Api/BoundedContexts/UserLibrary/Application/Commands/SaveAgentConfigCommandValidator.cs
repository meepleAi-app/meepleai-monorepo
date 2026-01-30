using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Validator for SaveAgentConfigCommand (Issue #3212)
/// Validates typology exists, is approved, model name allowed, and cost non-negative
/// </summary>
internal sealed class SaveAgentConfigCommandValidator : AbstractValidator<SaveAgentConfigCommand>
{
    // Allowed models per tier (must match frontend MODEL_CONFIG)
    private static readonly HashSet<string> AllowedModels =
    [
        "GPT-4o-mini",
        "Llama-3.3-70b",
        "Claude-3.5-Haiku",
        "GPT-4o"
    ];

    public SaveAgentConfigCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.TypologyId)
            .NotEmpty()
            .WithMessage("TypologyId is required");

        RuleFor(x => x.ModelName)
            .NotEmpty()
            .WithMessage("ModelName is required")
            .Must(BeAllowedModel)
            .WithMessage($"ModelName must be one of: {string.Join(", ", AllowedModels)}");

        RuleFor(x => x.CostEstimate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CostEstimate must be non-negative");
    }

    private static bool BeAllowedModel(string modelName) =>
        AllowedModels.Contains(modelName);
}

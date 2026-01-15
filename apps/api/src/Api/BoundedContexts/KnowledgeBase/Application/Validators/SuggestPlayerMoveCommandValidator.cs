using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SuggestPlayerMoveCommand.
/// Issue #2421: Player Mode UI Controls - Backend endpoint validation.
/// </summary>
internal sealed class SuggestPlayerMoveCommandValidator : AbstractValidator<SuggestPlayerMoveCommand>
{
    private const int MaxQueryLength = 1000;

    public SuggestPlayerMoveCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required")
            .Must(BeValidGameId).WithMessage("GameId must be a valid identifier");

        RuleFor(x => x.GameState)
            .NotNull().WithMessage("GameState cannot be null")
            .NotEmpty().WithMessage("GameState cannot be empty");

        RuleFor(x => x.Query)
            .MaximumLength(MaxQueryLength)
            .WithMessage($"Query cannot exceed {MaxQueryLength} characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Query));
    }

    private static bool BeValidGameId(string gameId)
    {
        // Accept UUIDs or alphanumeric IDs (e.g., "catan", "123e4567-...")
        return !string.IsNullOrWhiteSpace(gameId) &&
               (Guid.TryParse(gameId, out _) || gameId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
    }
}

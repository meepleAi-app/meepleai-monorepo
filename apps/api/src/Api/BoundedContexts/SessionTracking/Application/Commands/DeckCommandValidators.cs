using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Validator for CreateDeckCommand.
/// </summary>
public class CreateDeckCommandValidator : AbstractValidator<CreateDeckCommand>
{
    public CreateDeckCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Deck name is required.")
            .MaximumLength(200)
            .WithMessage("Deck name cannot exceed 200 characters.");

        RuleFor(x => x.DeckType)
            .NotEmpty()
            .WithMessage("Deck type is required.")
            .Must(type => type is "standard" or "custom")
            .WithMessage("Deck type must be 'standard' or 'custom'.");

        RuleFor(x => x.CustomCards)
            .NotEmpty()
            .When(x => string.Equals(x.DeckType, "custom", StringComparison.Ordinal))
            .WithMessage("Custom cards are required for custom deck.");

        RuleForEach(x => x.CustomCards)
            .ChildRules(card =>
            {
                card.RuleFor(c => c.Name)
                    .NotEmpty()
                    .WithMessage("Card name is required.")
                    .MaximumLength(100)
                    .WithMessage("Card name cannot exceed 100 characters.");

                card.RuleFor(c => c.ImageUrl)
                    .MaximumLength(500)
                    .When(c => !string.IsNullOrEmpty(c.ImageUrl))
                    .WithMessage("Image URL cannot exceed 500 characters.");
            })
            .When(x => x.CustomCards != null && x.CustomCards.Count > 0);
    }
}

/// <summary>
/// Validator for ShuffleDeckCommand.
/// </summary>
public class ShuffleDeckCommandValidator : AbstractValidator<ShuffleDeckCommand>
{
    public ShuffleDeckCommandValidator()
    {
        RuleFor(x => x.DeckId)
            .NotEmpty()
            .WithMessage("Deck ID is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");
    }
}

/// <summary>
/// Validator for DrawCardsCommand.
/// </summary>
public class DrawCardsCommandValidator : AbstractValidator<DrawCardsCommand>
{
    public DrawCardsCommandValidator()
    {
        RuleFor(x => x.DeckId)
            .NotEmpty()
            .WithMessage("Deck ID is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");

        RuleFor(x => x.Count)
            .GreaterThan(0)
            .WithMessage("Count must be greater than 0.")
            .LessThanOrEqualTo(52)
            .WithMessage("Cannot draw more than 52 cards at once.");
    }
}

/// <summary>
/// Validator for DiscardCardsCommand.
/// </summary>
public class DiscardCardsCommandValidator : AbstractValidator<DiscardCardsCommand>
{
    public DiscardCardsCommandValidator()
    {
        RuleFor(x => x.DeckId)
            .NotEmpty()
            .WithMessage("Deck ID is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");

        RuleFor(x => x.CardIds)
            .NotEmpty()
            .WithMessage("At least one card ID is required.");
    }
}

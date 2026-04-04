#pragma warning disable MA0048 // File name must match type name - Contains related validators
using Api.BoundedContexts.GameToolbox.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameToolbox.Application.Validators;

internal class CreateToolboxCommandValidator : AbstractValidator<CreateToolboxCommand>
{
    public CreateToolboxCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Mode)
            .Must(m => m is "Freeform" or "Phased")
            .WithMessage("Mode must be 'Freeform' or 'Phased'.");
    }
}

internal class UpdateToolboxModeCommandValidator : AbstractValidator<UpdateToolboxModeCommand>
{
    public UpdateToolboxModeCommandValidator()
    {
        RuleFor(x => x.ToolboxId).NotEmpty();
        RuleFor(x => x.Mode)
            .Must(m => m is "Freeform" or "Phased")
            .WithMessage("Mode must be 'Freeform' or 'Phased'.");
    }
}

internal class AddToolToToolboxCommandValidator : AbstractValidator<AddToolToToolboxCommand>
{
    private static readonly HashSet<string> ValidTypes =
    [
        "DiceRoller", "ScoreTracker", "TurnManager",
        "ResourceManager", "Notes", "Whiteboard", "CardDeck"
    ];

    public AddToolToToolboxCommandValidator()
    {
        RuleFor(x => x.ToolboxId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Invalid tool type. Valid: " + string.Join(", ", ValidTypes));
    }
}

internal class AddPhaseCommandValidator : AbstractValidator<AddPhaseCommand>
{
    public AddPhaseCommandValidator()
    {
        RuleFor(x => x.ToolboxId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

internal class DrawCardsCommandValidator : AbstractValidator<DrawCardsCommand>
{
    public DrawCardsCommandValidator()
    {
        RuleFor(x => x.ToolboxId).NotEmpty();
        RuleFor(x => x.DeckId).NotEmpty();
        RuleFor(x => x.Count).GreaterThan(0).LessThanOrEqualTo(52);
    }
}

internal class CreateCardDeckCommandValidator : AbstractValidator<CreateCardDeckCommand>
{
    public CreateCardDeckCommandValidator()
    {
        RuleFor(x => x.ToolboxId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DeckType)
            .Must(d => d is "Standard52" or "Standard52WithJokers" or "Custom")
            .WithMessage("DeckType must be 'Standard52', 'Standard52WithJokers', or 'Custom'.");
        RuleFor(x => x.CustomCards)
            .NotEmpty()
            .When(x => string.Equals(x.DeckType, "Custom", StringComparison.Ordinal))
            .WithMessage("Custom decks must include card definitions.");
    }
}

internal class CreateToolboxTemplateCommandValidator : AbstractValidator<CreateToolboxTemplateCommand>
{
    public CreateToolboxTemplateCommandValidator()
    {
        RuleFor(x => x.ToolboxId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Validator for RollDiceCommand.
/// </summary>
public class RollDiceCommandValidator : AbstractValidator<RollDiceCommand>
{
    public RollDiceCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");

        RuleFor(x => x.Formula)
            .NotEmpty()
            .WithMessage("Dice formula is required.")
            .MaximumLength(50)
            .WithMessage("Formula cannot exceed 50 characters.")
            .Must(BeValidDiceFormula)
            .WithMessage("Invalid dice formula. Use format like '2d6+3', '1d20-2', or 'd100'.");

        RuleFor(x => x.Label)
            .MaximumLength(100)
            .WithMessage("Label cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Label));
    }

    private static bool BeValidDiceFormula(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return false;

        try
        {
            DiceFormulaParser.Parse(formula);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

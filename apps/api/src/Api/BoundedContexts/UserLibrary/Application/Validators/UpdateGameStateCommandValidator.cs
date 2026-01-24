using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UpdateGameStateCommand.
/// </summary>
internal class UpdateGameStateCommandValidator : AbstractValidator<UpdateGameStateCommand>
{
    private static readonly string[] ValidStates = { "Nuovo", "InPrestito", "Wishlist", "Owned" };

    public UpdateGameStateCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.NewState)
            .NotEmpty()
            .WithMessage("NewState is required")
            .Must(state => ValidStates.Contains(state, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"NewState must be one of: {string.Join(", ", ValidStates)}");

        RuleFor(x => x.StateNotes)
            .MaximumLength(500)
            .When(x => x.StateNotes != null)
            .WithMessage("StateNotes cannot exceed 500 characters");
    }
}

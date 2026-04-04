using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for AbandonGameSessionCommand.
/// Ensures SessionId is a non-empty GUID.
/// </summary>
internal sealed class AbandonGameSessionCommandValidator : AbstractValidator<AbandonGameSessionCommand>
{
    public AbandonGameSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters")
            .When(x => x.Reason is not null);
    }
}

/// <summary>
/// Validator for CompleteGameSessionCommand.
/// Ensures SessionId is a non-empty GUID.
/// </summary>
internal sealed class CompleteGameSessionCommandValidator : AbstractValidator<CompleteGameSessionCommand>
{
    public CompleteGameSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.WinnerName)
            .MaximumLength(200).WithMessage("Winner name must not exceed 200 characters")
            .When(x => x.WinnerName is not null);
    }
}

/// <summary>
/// Validator for EndGameSessionCommand.
/// Ensures SessionId is a non-empty GUID.
/// </summary>
internal sealed class EndGameSessionCommandValidator : AbstractValidator<EndGameSessionCommand>
{
    public EndGameSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.WinnerName)
            .MaximumLength(200).WithMessage("Winner name must not exceed 200 characters")
            .When(x => x.WinnerName is not null);
    }
}

/// <summary>
/// Validator for StartGameSessionCommand.
/// Ensures GameId and UserId are non-empty and Players list is provided.
/// </summary>
internal sealed class StartGameSessionCommandValidator : AbstractValidator<StartGameSessionCommand>
{
    public StartGameSessionCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.Players)
            .NotEmpty().WithMessage("At least one player is required")
            .Must(p => p.Count <= 100).WithMessage("Players list must not exceed 100 entries")
            .When(x => x.Players is not null);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>
/// Validator for AddPlayerToSessionCommand.
/// Ensures SessionId is non-empty and PlayerName is provided.
/// </summary>
internal sealed class AddPlayerToSessionCommandValidator : AbstractValidator<AddPlayerToSessionCommand>
{
    public AddPlayerToSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.PlayerName)
            .NotEmpty().WithMessage("Player name is required")
            .MaximumLength(200).WithMessage("Player name must not exceed 200 characters");

        RuleFor(x => x.PlayerOrder)
            .GreaterThan(0).WithMessage("Player order must be greater than 0");

        RuleFor(x => x.Color)
            .MaximumLength(50).WithMessage("Color must not exceed 50 characters")
            .When(x => x.Color is not null);
    }
}

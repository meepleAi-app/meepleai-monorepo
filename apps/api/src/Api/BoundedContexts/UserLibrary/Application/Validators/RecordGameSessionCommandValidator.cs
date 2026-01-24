using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for RecordGameSessionCommand.
/// </summary>
internal class RecordGameSessionCommandValidator : AbstractValidator<RecordGameSessionCommand>
{
    public RecordGameSessionCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.PlayedAt)
            .NotEmpty()
            .WithMessage("PlayedAt is required")
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("PlayedAt cannot be in the future");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0)
            .WithMessage("DurationMinutes must be positive")
            .LessThanOrEqualTo(1440)
            .WithMessage("DurationMinutes cannot exceed 24 hours");

        RuleFor(x => x.Players)
            .MaximumLength(500)
            .When(x => x.Players != null)
            .WithMessage("Players cannot exceed 500 characters");

        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => x.Notes != null)
            .WithMessage("Notes cannot exceed 1000 characters");
    }
}

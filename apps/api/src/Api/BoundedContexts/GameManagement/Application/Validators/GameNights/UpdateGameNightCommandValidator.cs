using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for UpdateGameNightCommand.
/// Issue #43: Game Night CRUD validators.
/// </summary>
internal sealed class UpdateGameNightCommandValidator : AbstractValidator<UpdateGameNightCommand>
{
    public UpdateGameNightCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.GameNightId)
            .NotEmpty()
            .WithMessage("Game night ID is required");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MinimumLength(3)
            .WithMessage("Title must be at least 3 characters")
            .MaximumLength(200)
            .WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.ScheduledAt)
            .Must(scheduledAt => scheduledAt > DateTimeOffset.UtcNow.AddHours(1))
            .WithMessage("Scheduled time must be at least 1 hour in the future");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => x.Description != null)
            .WithMessage("Description cannot exceed 2000 characters");

        RuleFor(x => x.Location)
            .MaximumLength(500)
            .When(x => x.Location != null)
            .WithMessage("Location cannot exceed 500 characters");

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(2, 50)
            .When(x => x.MaxPlayers.HasValue)
            .WithMessage("Max players must be between 2 and 50");

        RuleFor(x => x.GameIds)
            .Must(ids => ids == null || ids.Count <= 20)
            .WithMessage("Cannot include more than 20 games");
    }
}

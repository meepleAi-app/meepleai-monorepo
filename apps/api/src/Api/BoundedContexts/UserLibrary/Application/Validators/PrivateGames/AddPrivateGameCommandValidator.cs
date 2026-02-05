using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Validator for AddPrivateGameCommand.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
internal sealed class AddPrivateGameCommandValidator : AbstractValidator<AddPrivateGameCommand>
{
    public AddPrivateGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Source)
            .NotEmpty()
            .WithMessage("Source is required")
            .Must(s => s.Equals("Manual", StringComparison.OrdinalIgnoreCase) ||
                      s.Equals("BoardGameGeek", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Source must be 'Manual' or 'BoardGameGeek'");

        RuleFor(x => x.BggId)
            .NotNull()
            .WithMessage("BggId is required for BoardGameGeek source")
            .GreaterThan(0)
            .WithMessage("BggId must be a positive integer")
            .When(x => x.Source.Equals("BoardGameGeek", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MaximumLength(200)
            .WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.MinPlayers)
            .GreaterThan(0)
            .WithMessage("MinPlayers must be at least 1")
            .LessThanOrEqualTo(100)
            .WithMessage("MinPlayers cannot exceed 100");

        RuleFor(x => x.MaxPlayers)
            .GreaterThanOrEqualTo(x => x.MinPlayers)
            .WithMessage("MaxPlayers must be greater than or equal to MinPlayers")
            .LessThanOrEqualTo(100)
            .WithMessage("MaxPlayers cannot exceed 100");

        RuleFor(x => x.YearPublished)
            .GreaterThan(1900)
            .WithMessage("YearPublished must be after 1900")
            .LessThanOrEqualTo(2100)
            .WithMessage("YearPublished cannot be after 2100")
            .When(x => x.YearPublished.HasValue);

        RuleFor(x => x.ComplexityRating)
            .InclusiveBetween(1.0m, 5.0m)
            .WithMessage("ComplexityRating must be between 1.0 and 5.0")
            .When(x => x.ComplexityRating.HasValue);

        RuleFor(x => x.PlayingTimeMinutes)
            .GreaterThan(0)
            .WithMessage("PlayingTimeMinutes must be positive")
            .When(x => x.PlayingTimeMinutes.HasValue);

        RuleFor(x => x.MinAge)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MinAge cannot be negative")
            .When(x => x.MinAge.HasValue);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(500)
            .WithMessage("ImageUrl cannot exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));

        RuleFor(x => x.ThumbnailUrl)
            .MaximumLength(500)
            .WithMessage("ThumbnailUrl cannot exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.ThumbnailUrl));

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Description cannot exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}

using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpdateSharedGameCommand.
/// </summary>
internal sealed class UpdateSharedGameCommandValidator : AbstractValidator<UpdateSharedGameCommand>
{
    public UpdateSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty).WithMessage("GameId is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(500).WithMessage("Title cannot exceed 500 characters");

        RuleFor(x => x.YearPublished)
            .GreaterThan(1900).WithMessage("Year must be greater than 1900")
            .LessThanOrEqualTo(DateTime.UtcNow.Year + 1).WithMessage("Year cannot be in the distant future");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.MinPlayers)
            .GreaterThan(0).WithMessage("Minimum players must be greater than 0");

        RuleFor(x => x.MaxPlayers)
            .GreaterThanOrEqualTo(x => x.MinPlayers)
            .WithMessage("Maximum players must be greater than or equal to minimum players");

        RuleFor(x => x.PlayingTimeMinutes)
            .GreaterThan(0).WithMessage("Playing time must be greater than 0");

        RuleFor(x => x.MinAge)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum age cannot be negative");

        RuleFor(x => x.ComplexityRating)
            .InclusiveBetween(1.0m, 5.0m)
            .When(x => x.ComplexityRating.HasValue)
            .WithMessage("Complexity rating must be between 1.0 and 5.0");

        RuleFor(x => x.AverageRating)
            .InclusiveBetween(1.0m, 10.0m)
            .When(x => x.AverageRating.HasValue)
            .WithMessage("Average rating must be between 1.0 and 10.0");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required")
            .Must(BeValidUrl).WithMessage("Image URL must be a valid absolute URL");

        RuleFor(x => x.ThumbnailUrl)
            .NotEmpty().WithMessage("Thumbnail URL is required")
            .Must(BeValidUrl).WithMessage("Thumbnail URL must be a valid absolute URL");

        RuleFor(x => x.ModifiedBy)
            .NotEqual(Guid.Empty).WithMessage("ModifiedBy is required");

        RuleFor(x => x.Rules)
            .SetValidator(new GameRulesDtoValidator()!)
            .When(x => x.Rules is not null);
    }

    private static bool BeValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}

using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for UpdateGameCommand.
/// Ensures GameId is provided and optional fields are within valid ranges.
/// </summary>
internal sealed class UpdateGameCommandValidator : AbstractValidator<UpdateGameCommand>
{
    public UpdateGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.Title)
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters")
            .When(x => x.Title is not null);

        RuleFor(x => x.Publisher)
            .MaximumLength(200).WithMessage("Publisher must not exceed 200 characters")
            .When(x => x.Publisher is not null);

        RuleFor(x => x.YearPublished)
            .InclusiveBetween(1900, 2100).WithMessage("Year published must be between 1900 and 2100")
            .When(x => x.YearPublished.HasValue);

        RuleFor(x => x.MinPlayers)
            .GreaterThan(0).WithMessage("Minimum players must be greater than 0")
            .When(x => x.MinPlayers.HasValue);

        RuleFor(x => x.MaxPlayers)
            .GreaterThan(0).WithMessage("Maximum players must be greater than 0")
            .When(x => x.MaxPlayers.HasValue);

        RuleFor(x => x.MaxPlayers)
            .GreaterThanOrEqualTo(x => x.MinPlayers!.Value)
            .WithMessage("Maximum players must be greater than or equal to minimum players")
            .When(x => x.MinPlayers.HasValue && x.MaxPlayers.HasValue);

        RuleFor(x => x.MinPlayTimeMinutes)
            .GreaterThan(0).WithMessage("Minimum play time must be greater than 0")
            .When(x => x.MinPlayTimeMinutes.HasValue);

        RuleFor(x => x.MaxPlayTimeMinutes)
            .GreaterThan(0).WithMessage("Maximum play time must be greater than 0")
            .When(x => x.MaxPlayTimeMinutes.HasValue);

        RuleFor(x => x.MaxPlayTimeMinutes)
            .GreaterThanOrEqualTo(x => x.MinPlayTimeMinutes!.Value)
            .WithMessage("Maximum play time must be greater than or equal to minimum play time")
            .When(x => x.MinPlayTimeMinutes.HasValue && x.MaxPlayTimeMinutes.HasValue);

        RuleFor(x => x.IconUrl)
            .MaximumLength(2000).WithMessage("Icon URL must not exceed 2000 characters")
            .When(x => x.IconUrl is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2000).WithMessage("Image URL must not exceed 2000 characters")
            .When(x => x.ImageUrl is not null);
    }
}

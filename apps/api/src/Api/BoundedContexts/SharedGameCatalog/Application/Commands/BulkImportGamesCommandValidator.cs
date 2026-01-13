using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for BulkImportGamesCommand.
/// Ensures list is not empty and each item has either BggId or manual data.
/// </summary>
public class BulkImportGamesCommandValidator : AbstractValidator<BulkImportGamesCommand>
{
    public BulkImportGamesCommandValidator()
    {
        RuleFor(x => x.Games)
            .NotNull()
            .WithMessage("Games list cannot be null")
            .NotEmpty()
            .WithMessage("Games list cannot be empty");

        When(x => x.Games != null, () =>
        {
            RuleFor(x => x.Games)
                .Must(games => games.Count <= 100)
                .WithMessage("Maximum 100 games per bulk import");
        });

        RuleForEach(x => x.Games)
            .Must(game => game.BggId.HasValue || !string.IsNullOrWhiteSpace(game.Title))
            .WithMessage("Each game must have either a BGG ID or manual Title data");
    }
}

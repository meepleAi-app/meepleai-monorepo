using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNight;

/// <summary>
/// Validator for SearchBggGamesForGameNightQuery.
/// Game Night Improvvisata - E1-1: BGG search input validation.
/// </summary>
internal sealed class SearchBggGamesForGameNightQueryValidator
    : AbstractValidator<SearchBggGamesForGameNightQuery>
{
    public SearchBggGamesForGameNightQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty()
            .WithMessage("Search term is required")
            .MinimumLength(2)
            .WithMessage("Search term must be at least 2 characters")
            .MaximumLength(200)
            .WithMessage("Search term must not exceed 200 characters");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50");
    }
}

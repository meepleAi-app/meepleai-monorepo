using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for SearchBggGamesQuery.
/// Issue: Admin Add Shared Game from BGG flow
/// </summary>
internal sealed class SearchBggGamesQueryValidator : AbstractValidator<SearchBggGamesQuery>
{
    public SearchBggGamesQueryValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty()
            .WithMessage("Search term is required")
            .MaximumLength(200)
            .WithMessage("Search term must not exceed 200 characters");
    }
}

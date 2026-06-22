using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Validator for <see cref="GetNewGamesQuery"/>.
/// Wave 3 Phase 1, PR #732 §4.3.2 / Issue #805.
/// </summary>
internal sealed class GetNewGamesQueryValidator : AbstractValidator<GetNewGamesQuery>
{
    public GetNewGamesQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 50)
            .WithMessage("Limit must be between 1 and 50.");
    }
}

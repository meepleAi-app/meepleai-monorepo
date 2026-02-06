using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;

/// <summary>
/// Validator for CheckPrivateGameDuplicatesQuery.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
internal class CheckPrivateGameDuplicatesQueryValidator : AbstractValidator<CheckPrivateGameDuplicatesQuery>
{
    public CheckPrivateGameDuplicatesQueryValidator()
    {
        RuleFor(x => x.PrivateGameId)
            .NotEmpty()
            .WithMessage("PrivateGameId is required");
    }
}

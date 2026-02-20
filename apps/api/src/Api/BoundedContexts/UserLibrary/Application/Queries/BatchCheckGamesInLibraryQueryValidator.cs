using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Validator for BatchCheckGamesInLibraryQuery.
/// Enforces max 100 game IDs per request to prevent abuse.
/// </summary>
internal class BatchCheckGamesInLibraryQueryValidator : AbstractValidator<BatchCheckGamesInLibraryQuery>
{
    public BatchCheckGamesInLibraryQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.GameIds)
            .NotNull()
            .WithMessage("Game IDs list is required")
            .Must(ids => ids.Count > 0)
            .WithMessage("At least one game ID is required")
            .Must(ids => ids.Count <= 100)
            .WithMessage("Maximum 100 game IDs allowed per request");

        RuleForEach(x => x.GameIds)
            .NotEmpty()
            .WithMessage("Game ID cannot be empty");
    }
}

using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for GetGameInLibraryStatusQuery.
/// Issue #4259: Collection Quick Actions for MeepleCard
/// </summary>
internal class GetGameInLibraryStatusQueryValidator : AbstractValidator<GetGameInLibraryStatusQuery>
{
    public GetGameInLibraryStatusQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");
    }
}

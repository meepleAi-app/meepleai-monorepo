using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Validator for GetPrivateGameQuery.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
internal sealed class GetPrivateGameQueryValidator : AbstractValidator<GetPrivateGameQuery>
{
    public GetPrivateGameQueryValidator()
    {
        RuleFor(x => x.PrivateGameId)
            .NotEmpty()
            .WithMessage("PrivateGameId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

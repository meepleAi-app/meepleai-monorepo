using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for GetGamePdfsQuery.
/// Issue #3152: Validates game ID and user ID parameters.
/// </summary>
internal class GetGamePdfsQueryValidator : AbstractValidator<GetGamePdfsQuery>
{
    public GetGamePdfsQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}

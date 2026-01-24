using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for GetGameDetailQuery.
/// Ensures valid GUIDs are provided for UserId and GameId.
/// </summary>
internal class GetGameDetailQueryValidator : AbstractValidator<GetGameDetailQuery>
{
    public GetGameDetailQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");
    }
}

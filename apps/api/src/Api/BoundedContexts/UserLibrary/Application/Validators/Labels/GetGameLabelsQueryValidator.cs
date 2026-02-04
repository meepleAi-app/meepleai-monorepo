using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.Labels;

/// <summary>
/// Validator for GetGameLabelsQuery.
/// </summary>
internal sealed class GetGameLabelsQueryValidator : AbstractValidator<GetGameLabelsQuery>
{
    public GetGameLabelsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");
    }
}

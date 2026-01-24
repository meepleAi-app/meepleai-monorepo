using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for GetGameChecklistQuery.
/// </summary>
internal class GetGameChecklistQueryValidator : AbstractValidator<GetGameChecklistQuery>
{
    public GetGameChecklistQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");
    }
}

using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.Labels;

/// <summary>
/// Validator for GetLabelsQuery.
/// </summary>
internal sealed class GetLabelsQueryValidator : AbstractValidator<GetLabelsQuery>
{
    public GetLabelsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

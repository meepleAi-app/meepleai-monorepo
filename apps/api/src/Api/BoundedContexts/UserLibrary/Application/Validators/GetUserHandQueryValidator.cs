using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class GetUserHandQueryValidator : AbstractValidator<GetUserHandQuery>
{
    public GetUserHandQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

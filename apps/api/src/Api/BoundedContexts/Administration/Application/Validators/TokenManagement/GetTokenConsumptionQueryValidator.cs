using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.TokenManagement;

public sealed class GetTokenConsumptionQueryValidator : AbstractValidator<GetTokenConsumptionQuery>
{
    public GetTokenConsumptionQueryValidator()
    {
        RuleFor(x => x.Days).Must(d => d == 7 || d == 30)
            .WithMessage("Days must be either 7 or 30");
    }
}

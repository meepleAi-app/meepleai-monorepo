using Api.BoundedContexts.Administration.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for GetDashboardInsightsQuery (Issue #3319).
/// </summary>
internal sealed class GetDashboardInsightsQueryValidator : AbstractValidator<GetDashboardInsightsQuery>
{
    public GetDashboardInsightsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

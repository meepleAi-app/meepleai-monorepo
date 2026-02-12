using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Validator for GetDashboardInsightsQuery.
/// Issue #3916: AI insights validation.
/// </summary>
internal class GetDashboardInsightsQueryValidator : AbstractValidator<GetDashboardInsightsQuery>
{
    public GetDashboardInsightsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}

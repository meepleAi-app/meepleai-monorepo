using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Validator for GetApiRequestsByDayQuery.
/// Issue #2790: Admin Dashboard Charts
/// </summary>
internal class GetApiRequestsByDayQueryValidator : AbstractValidator<GetApiRequestsByDayQuery>
{
    public GetApiRequestsByDayQueryValidator()
    {
        RuleFor(x => x.Days)
            .GreaterThan(0)
            .WithMessage("Days must be greater than 0")
            .LessThanOrEqualTo(90)
            .WithMessage("Days must not exceed 90");
    }
}
